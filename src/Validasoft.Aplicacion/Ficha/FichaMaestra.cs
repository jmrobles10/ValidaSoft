using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>Traducción de la ficha a DTO.</summary>
public static class MapeosFicha
{
    public static CompletitudSeccionDto ADto(this CompletitudDeSeccion c) =>
        new(c.Completa, [.. c.Faltantes.Select(f => new FaltanteSeccionDto(f.Campo, f.Regla, f.Mensaje))], c.ReglaVersion);

    public static SoporteSeccionDto ADto(this SoporteDeSeccion s) => new(s.DocumentoId, s.Version, s.TipoDocumento);

    /// <summary>Versión vigente de la sección (o vacía si aún no tiene).</summary>
    public static SeccionFichaDto ADto(this SeccionDeFicha? seccion, DefinicionDeSeccion definicion, int? periodo) =>
        seccion?.Vigente is { } v
            ? new SeccionFichaDto(definicion.Codigo, definicion.Nombre, seccion.Periodo, v.Numero, v.Datos, [.. v.Soportes.Select(s => s.ADto())],
                v.Completitud.ADto(), v.Huella, v.Origen.ToString(), v.Fecha, seccion.Versiones.Count, v.EsquemaVersion)
            : new SeccionFichaDto(definicion.Codigo, definicion.Nombre, periodo, null, null, [], null, null, null, null, seccion?.Versiones.Count ?? 0,
                definicion.VersionEsquema);

    /// <summary>Estado para el wizard: una fila por sección (en la financiera, una por periodo diligenciado).</summary>
    public static EstadoFichaDto ADto(this EstadoDeFicha estado, TipoPersona tipoPersona)
    {
        var filas = new List<EstadoSeccionFichaDto>();
        foreach (var definicion in CatalogoDeSecciones.Todas)
        {
            var obligatoria = definicion.Obligatoria(tipoPersona);
            var propias = estado.Secciones.Where(s => s.Seccion == definicion.Codigo).OrderByDescending(s => s.Periodo).ToList();
            if (propias.Count == 0)
                filas.Add(new EstadoSeccionFichaDto(definicion.Codigo, definicion.Nombre, null, obligatoria, null, false, [], null, null));
            filas.AddRange(propias.Select(s => new EstadoSeccionFichaDto(definicion.Codigo, definicion.Nombre, s.Periodo, obligatoria, s.Version,
                s.Completa, [.. s.Faltantes.Select(f => new FaltanteSeccionDto(f.Campo, f.Regla, f.Mensaje))], s.ReglaVersion, s.Actualizada)));
        }

        return new EstadoFichaDto(estado.Id, tipoPersona.ToString(), [.. CatalogoDeSecciones.OrdenDelWizard(tipoPersona).Select(s => s.Codigo)],
            estado.SiguienteSeccion(tipoPersona), estado.FichaCompleta(tipoPersona), filas);
    }
}

/// <summary>
/// Guardado de una sección (FM-16, FM-18): lee y normaliza los datos (422 de formato), verifica los soportes contra la bóveda
/// (FM-5), aplica la regla única y, si la huella cambió, agrega la versión al stream y refleja el <see cref="EstadoDeFicha"/>
/// en la misma transacción. Las carreras (dos guardados a la vez) se reintentan. Con <c>mensajes</c> (F2) la versión nueva
/// queda disponible para las empresas: por el outbox, cada vínculo aceptado del tercero pone al día su expediente abierto
/// o abre la actualización de la sección que ya tenía aceptada (<see cref="SincronizarRequisitosDelVinculo"/>).
/// </summary>
public static class GuardadoDeSecciones
{
    public const int MaximoSoportes = 10;

    /// <summary>Estados de una versión de documento que puede respaldar una sección.</summary>
    private static readonly EstadoVersion[] EstadosDeSoporte = [EstadoVersion.Actual, EstadoVersion.Superada, EstadoVersion.Vencida];

    public sealed record Resultado(SeccionDeFicha Seccion, bool Creada, EstadoDeFicha Estado, TipoPersona TipoPersona);

    public static async Task<Resultado> GuardarAsync(ISesiones sesiones, Guid terceroId, DefinicionDeSeccion definicion, int? periodo, GuardarSeccionDto dto,
        OrigenDeSeccion origen, string autor, TimeProvider reloj, CancellationToken ct, IMessageBus? mensajes = null, IContextoSolicitud? solicitud = null)
    {
        var hoy = reloj.Hoy();
        var periodoValido = CatalogoDeSecciones.PeriodoValido(definicion, periodo, hoy);
        var tercero = await TerceroAsync(sesiones, terceroId, ct);
        var contexto = new ContextoDeSeccion(tercero.TipoPersona, tercero.Identificacion, periodoValido, hoy);
        var datos = ReglasDeFicha.Leer(definicion, dto.Datos, contexto);

        ICatalogosDeFicha catalogos;
        await using (var global = sesiones.ConsultaGlobal())
            catalogos = await ImportacionDeCatalogos.CargarAsync(global, definicion.Esquema.Referencias(datos), ct);

        IReadOnlyList<SoporteDeSeccion> soportes;
        await using (var boveda = sesiones.ConsultaBoveda())
            soportes = await VerificarSoportesAsync(boveda, terceroId, definicion, dto.Soportes, ct);

        return await ConReintentosAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            await using var sesion = sesiones.Boveda(mensajes);
            var id = SeccionDeFicha.IdPara(terceroId, definicion.Codigo, periodoValido);
            var existente = await sesion.Events.FetchStreamStateAsync(id, ct) is not null;
            var stream = existente ? await sesion.Events.FetchForExclusiveWriting<SeccionDeFicha>(id, ct) : null;

            SeccionDeFichaIniciada? iniciada = null;
            var actual = stream?.Aggregate;
            if (actual is null)
            {
                iniciada = SeccionDeFicha.Iniciar(terceroId, definicion, periodoValido, ahora);
                actual = SeccionDeFicha.Create(iniciada);
            }

            var eventos = actual.Registrar(definicion, datos, soportes, contexto, catalogos, origen, autor, ahora);
            var estado = await sesion.LoadAsync<EstadoDeFicha>(terceroId, ct) ?? EstadoDeFicha.Nuevo(terceroId);
            if (eventos.Count == 0)
                return new Resultado(actual, false, estado, tercero.TipoPersona);

            if (iniciada is not null)
                sesion.Events.StartStream<SeccionDeFicha>(id, [iniciada, .. eventos]);
            else
                foreach (var evento in eventos)
                    stream!.AppendOne(evento);

            var proyectada = actual.Proyectar(eventos);
            estado.Reflejar(proyectada);
            sesion.Store(estado);
            if (mensajes is not null && solicitud is not null)
                await PedirSincronizacionAsync(sesion, terceroId, mensajes, solicitud, ct);
            await sesion.SaveChangesAsync(ct);
            return new Resultado(proyectada, true, estado, tercero.TipoPersona);
        }, mensajes);
    }

    /// <summary>
    /// Una versión nueva de la sección: cada vínculo aceptado del tercero (de cualquier tenant) pide poner al día su expediente
    /// (RQ-2) o abrir la actualización (RV-1). El manejador es idempotente y solo actúa si el flujo pide la sección.
    /// </summary>
    private static async Task PedirSincronizacionAsync(IDocumentSession sesion, Guid terceroId, IMessageBus mensajes, IContextoSolicitud contexto,
        CancellationToken ct)
    {
        var vinculos = await sesion.Query<Vinculo>().Where(v => v.AnyTenant() && v.TerceroId == terceroId && v.InvitacionAceptada).ToListAsync(ct);
        if (vinculos.Count == 0)
            return;
        var empresas = (await sesion.LoadManyAsync<Empresa>(ct, vinculos.Select(v => v.EmpresaId).Distinct().ToArray())).ToDictionary(e => e.Id);
        await ComparticionDeDocumentos.PedirSincronizacionAsync(mensajes,
            vinculos.Where(v => empresas.ContainsKey(v.EmpresaId)).Select(v => (empresas[v.EmpresaId].TenantId, v.Id)), contexto);
    }

    /// <summary>
    /// Cada soporte debe ser una versión utilizable (actual, superada o vencida) de un documento <b>global</b> de la bóveda
    /// del propio tercero, de un tipo que respalde la sección (docs/14 §5.1). 422 <c>soporte-invalido</c> (no existe o no es
    /// suyo: no se distingue), <c>soporte-tipo-invalido</c>, <c>soporte-version-invalida</c>, <c>soporte-duplicado</c> y
    /// <c>demasiados-soportes</c>, con <c>campo</c> = <c>soportes[i]</c>.
    /// </summary>
    public static async Task<IReadOnlyList<SoporteDeSeccion>> VerificarSoportesAsync(IQuerySession boveda, Guid terceroId, DefinicionDeSeccion definicion,
        IReadOnlyList<SoporteSeccionDto>? pedidos, CancellationToken ct)
    {
        var lista = pedidos ?? [];
        if (lista.Count > MaximoSoportes)
            throw Error("demasiados-soportes", "soportes", $"Una versión admite hasta {MaximoSoportes} soportes.");
        if (lista.Count == 0)
            return [];

        var documentos = (await boveda.LoadManyAsync<Documento>(ct, lista.Select(s => s.DocumentoId).Distinct().ToArray())).ToDictionary(d => d.Id);
        var vistos = new HashSet<(Guid, int)>();
        var soportes = new List<SoporteDeSeccion>();
        for (var i = 0; i < lista.Count; i++)
        {
            var pedido = lista[i];
            var campo = $"soportes[{i}]";
            if (!vistos.Add((pedido.DocumentoId, pedido.Version)))
                throw Error("soporte-duplicado", campo, "Ese soporte ya está en la lista.");
            if (!documentos.TryGetValue(pedido.DocumentoId, out var documento) || documento.TerceroId != terceroId ||
                documento.Ambito != AmbitoDocumento.Global)
                throw Error("soporte-invalido", campo, "El soporte debe ser un documento de tu bóveda.");
            if (!definicion.AdmiteSoporte(documento.TipoCanonico))
                throw Error("soporte-tipo-invalido", campo,
                    $"{documento.NombreTipo} no respalda «{definicion.Nombre}». Sirven: {string.Join(", ", definicion.TiposDeSoporte)}.");
            if (documento.Buscar(pedido.Version) is not { } version || !EstadosDeSoporte.Contains(version.Estado))
                throw Error("soporte-version-invalida", campo, $"La versión {pedido.Version} de {documento.NombreTipo} no se puede usar como soporte.");
            soportes.Add(new SoporteDeSeccion(documento.Id, pedido.Version, documento.TipoCanonico ?? documento.NombreTipo));
        }

        return soportes;
    }

    public static async Task<Tercero> TerceroAsync(ISesiones sesiones, Guid terceroId, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return await consulta.LoadAsync<Tercero>(terceroId, ct)
               ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero.");
    }

    /// <summary>
    /// Como <see cref="Peticiones.ConReintentosAsync{T}"/>, también cuando dos guardados inician el mismo stream a la vez. Con
    /// <paramref name="mensajes"/>, antes de reintentar descarta lo que el intento fallido dejó en el outbox.
    /// </summary>
    internal static async Task<T> ConReintentosAsync<T>(Func<Task<T>> accion, IMessageBus? mensajes = null, int intentos = 10)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await accion();
            }
            catch (Exception e) when (intento < intentos && (Peticiones.EsCarrera(e) || e is CarreraDeFicha ||
                                                         e.GetType().Name == "ExistingStreamIdCollisionException"))
            {
                if (mensajes is MessageContext pendientes)
                    await pendientes.ClearAllAsync();
                await Task.Delay(Random.Shared.Next(5, 25 * intento));
            }
        }
    }

    private static ReglaDeNegocioException Error(string codigo, string campo, string mensaje) =>
        new(TipoFalla.Validacion, codigo, mensaje) { Datos = new Dictionary<string, object?> { ["campo"] = campo } };
}

/// <summary>Otro proceso cambió la sección mientras se recalculaba: se reintenta.</summary>
internal sealed class CarreraDeFicha() : Exception("La sección cambió durante el recálculo.");

/// <summary>
/// La ficha maestra vista por el tercero (sesión pública, FM-19): el estado de cada sección para el wizard, la versión
/// vigente de una sección y el guardado (parcial permitido) que responde la siguiente sección del wizard.
/// </summary>
public static class FichaMaestraEndpoints
{
    [WolverineGet("/public/v1/ficha")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<EstadoFichaDto> Obtener(HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var terceroId = SesionDeTercero.De(http.User).TerceroId;
        var tercero = await GuardadoDeSecciones.TerceroAsync(sesiones, terceroId, ct);
        await using var boveda = sesiones.ConsultaBoveda();
        var estado = await boveda.LoadAsync<EstadoDeFicha>(terceroId, ct) ?? EstadoDeFicha.Nuevo(terceroId);
        return estado.ADto(tercero.TipoPersona);
    }

    /// <summary>Versión vigente de la sección (datos, soportes y completitud) o vacía. 404 <c>seccion-no-encontrada</c>; la financiera exige <c>?periodo=</c>.</summary>
    [WolverineGet("/public/v1/ficha/{seccion}")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<SeccionFichaDto> ObtenerSeccion(string seccion, int? periodo, HttpContext http, ISesiones sesiones, TimeProvider reloj,
        CancellationToken ct)
    {
        var terceroId = SesionDeTercero.De(http.User).TerceroId;
        var definicion = CatalogoDeSecciones.Requerida(seccion);
        var periodoValido = CatalogoDeSecciones.PeriodoValido(definicion, periodo, reloj.Hoy());
        await using var boveda = sesiones.ConsultaBoveda();
        var actual = await boveda.LoadAsync<SeccionDeFicha>(SeccionDeFicha.IdPara(terceroId, definicion.Codigo, periodoValido), ct);
        return actual.ADto(definicion, periodoValido);
    }

    /// <summary>
    /// Guarda una versión si cambiaron los datos o los soportes (misma huella ⇒ 200 con <c>creada: false</c>). 422 de
    /// formato con <c>campo</c> (y <c>errores</c>): <c>campo-desconocido</c>, <c>valor-invalido</c>, <c>codigo-invalido</c>,
    /// <c>identificacion-no-editable</c>, <c>dv-no-editable</c>, <c>persona-duplicada</c>, los de soportes y los de periodo.
    /// Lo incompleto se guarda (borrador) con sus faltantes.
    /// </summary>
    [WolverinePut("/public/v1/ficha/{seccion}")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<SeccionGuardadaDto> Guardar(string seccion, int? periodo, GuardarSeccionDto dto, HttpContext http, ISesiones sesiones,
        IMessageBus mensajes, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var terceroId = SesionDeTercero.De(http.User).TerceroId;
        var definicion = CatalogoDeSecciones.Requerida(seccion);
        var resultado = await GuardadoDeSecciones.GuardarAsync(sesiones, terceroId, definicion, periodo, dto, OrigenDeSeccion.Tercero, terceroId.ToString(),
            reloj, ct, mensajes, contexto);
        var vigente = resultado.Seccion.Vigente!;
        return new SeccionGuardadaDto(vigente.Numero, resultado.Creada, vigente.Completitud.ADto(),
            resultado.Estado.SiguienteSeccion(resultado.TipoPersona, definicion.Codigo), resultado.Seccion.ADto(definicion, resultado.Seccion.Periodo));
    }

    /// <summary>Secciones vigentes del tercero, en el orden del wizard (la financiera, del periodo más reciente al más antiguo).</summary>
    public static async Task<IReadOnlyList<SeccionFichaDto>> VigentesAsync(IQuerySession boveda, Guid terceroId, CancellationToken ct)
    {
        var secciones = await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).ToListAsync(ct);
        return
        [
            .. CatalogoDeSecciones.Todas.SelectMany(definicion => secciones
                .Where(s => s.Seccion == definicion.Codigo && s.Vigente is not null)
                .OrderByDescending(s => s.Periodo)
                .Select(s => s.ADto(definicion, s.Periodo)))
        ];
    }
}
