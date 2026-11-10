using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Ficha.Portal;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>Qué pasó con una sección (y periodo) de un tercero importado.</summary>
public enum ResultadoDeSeccionImportada
{
    /// <summary>Nació una versión (la 1, o la siguiente porque cambió la huella). En un dry-run: nacería.</summary>
    Creada,

    /// <summary>Misma huella que la vigente: nada.</summary>
    SinCambios,

    /// <summary>No se pudo registrar (los rechazos dicen por qué).</summary>
    Rechazada
}

public sealed record SeccionImportada(string Seccion, int? Periodo, int? Version, ResultadoDeSeccionImportada Resultado);

/// <summary>Resultado de importar (o sincronizar) un tercero del Portal.</summary>
public sealed record TerceroImportado(
    int Terid, string ClaveLegado, Guid? TerceroId, bool? Creado, IReadOnlyList<SeccionImportada> Secciones, int Aceptaciones, int AceptacionesExistentes,
    int SinVinculo, IReadOnlyList<AvisoDeTraduccion> Advertencias, IReadOnlyList<AvisoDeTraduccion> Rechazos)
{
    public bool Rechazado => TerceroId is null;
}

/// <summary>
/// Cómo se importa: <see cref="Simular"/> (dry-run: lee y calcula, no escribe), el origen de las versiones (<c>Importada</c> en
/// la corrida; <c>Portal</c> en el sincronizador), el mapa constructora ⇒ empresa para las aceptaciones, el actor para la
/// auditoría y el bus del outbox (para pedir la sincronización de requisitos de F2 en la misma transacción).
/// </summary>
public sealed record OpcionesDeImportacion(
    bool Simular, OrigenDeSeccion Origen, IReadOnlyDictionary<int, Empresa> Constructoras, ActorTipo ActorTipo, string ActorId, IMessageBus? Mensajes);

/// <summary>
/// El importador de la ficha desde el Portal (F4, FM-39…FM-43): por tercero, traduce (puro), crea o encuentra el
/// <see cref="Tercero"/> (id UUID v5 de <c>portal:{Terid}</c>; por identificación normalizada si ya existía), registra cada
/// sección como versión con el mismo guardado del wizard (misma huella ⇒ nada; sin soporte: las versiones importadas no lo
/// exigen) y, con el mapa de constructoras, deja aceptadas por la empresa las secciones del tercero que en el Portal estaba
/// Completada o en Actualizacion. Idempotente.
/// </summary>
public static class ImportacionDePortal
{
    /// <summary>Estados del Portal (<c>EstadoTercero</c>) que se importan como aceptados: Completada (3) y Actualizacion (4).</summary>
    public static readonly IReadOnlySet<int> EstadosAceptados = new HashSet<int> { 3, 4 };

    public static async Task<TerceroImportado> ImportarTerceroAsync(ISesiones sesiones, TerceroDelPortal origen, CatalogosDelPortal catalogos,
        ICatalogosDeFicha plataforma, OpcionesDeImportacion opciones, TimeProvider reloj, CancellationToken ct)
    {
        var hoy = reloj.Hoy();
        var traduccion = TraductorDelPortal.Traducir(origen, catalogos, plataforma, hoy);
        if (traduccion.Tercero is null)
            return Rechazado(traduccion);

        // El tercero: por su id del legado (reimportación) o por su identificación (ya lo registró una empresa).
        Tercero? tercero;
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            tercero = await consulta.LoadAsync<Tercero>(IdDeLegado.TerceroPortal(origen.Terid), ct);
            var clave = traduccion.Tercero.Identificacion.Clave;
            tercero ??= await consulta.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == clave, ct);
        }

        var rechazosExtra = new List<AvisoDeTraduccion>();
        if (tercero is not null)
        {
            if (tercero.ClaveIdentificacion != traduccion.Tercero.Identificacion.Clave)
                return Rechazado(traduccion, new AvisoDeTraduccion(null, null, "numero", "identificacion-cambiada",
                    $"El Terid {origen.Terid} ya se importó como {tercero.ClaveIdentificacion} y el Portal hoy dice {traduccion.Tercero.Identificacion.Clave}.",
                    traduccion.Tercero.Identificacion.Clave, "Revisar en el Portal: un tercero no cambia de identificación."));
            if (tercero.ClaveLegado is { } otra && otra != traduccion.ClaveLegado)
                return Rechazado(traduccion, new AvisoDeTraduccion(null, null, "numero", "tercero-duplicado-en-portal",
                    $"La identificación {tercero.ClaveIdentificacion} ya se importó desde {otra}: el Portal tiene el tercero dos veces.",
                    traduccion.ClaveLegado, "Fusionar los terceros en el Portal (o descartar el duplicado) y volver a importar."));
            if (tercero.TipoPersona != traduccion.Tercero.TipoPersona)
            {
                traduccion = TraductorDelPortal.Traducir(origen, catalogos, plataforma, hoy, tercero.TipoPersona);
                if (traduccion.Tercero is null)
                    return Rechazado(traduccion);
            }
        }

        var creado = tercero is null;
        if (tercero is null)
        {
            tercero = Tercero.Crear(traduccion.Tercero.Identificacion, traduccion.Tercero.TipoPersona, traduccion.Tercero.Nombre, null, null, reloj.GetUtcNow());
            tercero.Id = IdDeLegado.TerceroPortal(origen.Terid);
            tercero.ClaveLegado = traduccion.ClaveLegado;
            if (!opciones.Simular)
                await GuardarTerceroAsync(sesiones, tercero, opciones, ct);
        }
        else if (tercero.ClaveLegado is null && !opciones.Simular)
        {
            tercero.ClaveLegado = traduccion.ClaveLegado;
            await GuardarTerceroAsync(sesiones, tercero, opciones, ct);
        }

        var secciones = new List<SeccionImportada>();
        foreach (var seccion in traduccion.Secciones)
        {
            var contexto = new ContextoDeSeccion(tercero.TipoPersona, tercero.Identificacion, seccion.Periodo, hoy);
            try
            {
                secciones.Add(opciones.Simular
                    ? await SimularSeccionAsync(sesiones, tercero, seccion, contexto, plataforma, opciones, reloj, ct)
                    : await RegistrarSeccionAsync(sesiones, tercero, seccion, contexto, plataforma, opciones, reloj, ct));
            }
            catch (ReglaDeNegocioException e)
            {
                rechazosExtra.Add(new AvisoDeTraduccion(seccion.Definicion.Codigo, seccion.Periodo, e.Datos?.GetValueOrDefault("campo") as string ?? "datos",
                    e.Codigo, e.Message, null, TraductorDelPortal.AccionCorregirEnPortal));
                secciones.Add(new SeccionImportada(seccion.Definicion.Codigo, seccion.Periodo, null, ResultadoDeSeccionImportada.Rechazada));
            }
        }

        var (aceptaciones, existentes, sinVinculo) = opciones.Constructoras.Count == 0
            ? (0, 0, 0)
            : await AceptarAsync(sesiones, origen, tercero, creado, secciones, opciones, reloj, ct);

        return new TerceroImportado(origen.Terid, traduccion.ClaveLegado, tercero.Id, creado, secciones, aceptaciones, existentes, sinVinculo,
            traduccion.Advertencias, [.. traduccion.Rechazos, .. rechazosExtra]);
    }

    private static TerceroImportado Rechazado(ResultadoDeTraduccion traduccion, AvisoDeTraduccion? extra = null) =>
        new(traduccion.Terid, traduccion.ClaveLegado, null, null, [], 0, 0, 0, traduccion.Advertencias,
            extra is null ? traduccion.Rechazos : [.. traduccion.Rechazos, extra]);

    private static IDocumentSession ConActor(IDocumentSession sesion, OpcionesDeImportacion opciones)
    {
        sesion.SetHeader("actorTipo", opciones.ActorTipo.ToString());
        sesion.SetHeader("actorId", opciones.ActorId);
        return sesion;
    }

    private static async Task GuardarTerceroAsync(ISesiones sesiones, Tercero tercero, OpcionesDeImportacion opciones, CancellationToken ct)
    {
        await using var sesion = ConActor(sesiones.Global(), opciones);
        sesion.Store(tercero);
        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>
    /// La versión con el mismo guardado del wizard (<see cref="GuardadoDeSecciones.RegistrarAsync"/>): sin soportes (las versiones
    /// importadas no exigen el soporte del cambio: su respaldo es el legado), origen <c>Importada</c> o <c>Portal</c> y, con
    /// el bus, la sincronización de requisitos de los vínculos aceptados en la misma transacción (revalidación de F2).
    /// </summary>
    private static async Task<SeccionImportada> RegistrarSeccionAsync(ISesiones sesiones, Tercero tercero, SeccionTraducida seccion,
        ContextoDeSeccion contexto, ICatalogosDeFicha plataforma, OpcionesDeImportacion opciones, TimeProvider reloj, CancellationToken ct)
    {
        var resultado = await GuardadoDeSecciones.RegistrarAsync(() => ConActor(sesiones.Boveda(opciones.Mensajes), opciones), tercero, seccion.Definicion,
            seccion.Periodo, seccion.Datos, [], contexto, plataforma, opciones.Origen, opciones.ActorId, reloj, ct, opciones.Mensajes,
            opciones.Mensajes is null ? null : (opciones.ActorTipo.ToString(), opciones.ActorId));
        return new SeccionImportada(seccion.Definicion.Codigo, seccion.Periodo, resultado.Seccion.VersionVigente,
            resultado.Creada ? ResultadoDeSeccionImportada.Creada : ResultadoDeSeccionImportada.SinCambios);
    }

    /// <summary>Dry-run: la misma decisión del agregado sobre el stream actual, sin escribir.</summary>
    private static async Task<SeccionImportada> SimularSeccionAsync(ISesiones sesiones, Tercero tercero, SeccionTraducida seccion, ContextoDeSeccion contexto,
        ICatalogosDeFicha plataforma, OpcionesDeImportacion opciones, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        SeccionDeFicha? actual;
        await using (var boveda = sesiones.ConsultaBoveda())
            actual = await boveda.LoadAsync<SeccionDeFicha>(SeccionDeFicha.IdPara(tercero.Id, seccion.Definicion.Codigo, seccion.Periodo), ct);
        actual ??= SeccionDeFicha.Create(SeccionDeFicha.Iniciar(tercero.Id, seccion.Definicion, seccion.Periodo, ahora));
        var eventos = actual.Registrar(seccion.Definicion, seccion.Datos, [], contexto, plataforma, opciones.Origen, opciones.ActorId, ahora);
        return eventos.Count > 0
            ? new SeccionImportada(seccion.Definicion.Codigo, seccion.Periodo, actual.SiguienteNumero, ResultadoDeSeccionImportada.Creada)
            : new SeccionImportada(seccion.Definicion.Codigo, seccion.Periodo, actual.VersionVigente, ResultadoDeSeccionImportada.SinCambios);
    }

    /// <summary>
    /// Aceptación importada (FM-42): por cada constructora mapeada donde el tercero estaba Completada (3) o Actualizacion (4),
    /// en cada vínculo del tercero con esa empresa, la versión vigente de cada sección (si salió del legado: origen
    /// <c>Importada</c> o <c>Portal</c>) queda en <see cref="SeccionAceptada"/> con origen <c>Importacion</c>, sin expediente.
    /// Solo si el vínculo no tenía nada aceptado de esa sección: una aceptación existente (de un expediente o de una corrida
    /// anterior) no se toca, y los cambios posteriores se revalidan con F2. Sin vínculo en la empresa ⇒ se cuenta y se
    /// acepta en una corrida posterior, cuando exista.
    /// </summary>
    private static async Task<(int Creadas, int Existentes, int SinVinculo)> AceptarAsync(ISesiones sesiones, TerceroDelPortal origen, Tercero tercero,
        bool creado, IReadOnlyList<SeccionImportada> importadas, OpcionesDeImportacion opciones, TimeProvider reloj, CancellationToken ct)
    {
        var empresas = (origen.Constructoras ?? [])
            .Where(c => EstadosAceptados.Contains(c.Estado) && opciones.Constructoras.ContainsKey(c.IdConstructora))
            .Select(c => (c.IdConstructora, Empresa: opciones.Constructoras[c.IdConstructora]))
            .DistinctBy(c => c.Empresa.Id)
            .ToList();
        if (empresas.Count == 0)
            return (0, 0, 0);

        // Lo que se aceptaría: la vigente de cada sección (en un dry-run, la que quedaría tras importar).
        List<(string Seccion, int? Periodo, Guid SeccionId, int Version, string Huella)> vigentes;
        if (opciones.Simular)
        {
            vigentes = [.. importadas.Where(s => s.Resultado != ResultadoDeSeccionImportada.Rechazada && s.Version is not null)
                .Select(s => (s.Seccion, s.Periodo, SeccionDeFicha.IdPara(tercero.Id, s.Seccion, s.Periodo), s.Version!.Value, string.Empty))];
        }
        else
        {
            await using var boveda = sesiones.ConsultaBoveda();
            var terceroId = tercero.Id;
            vigentes = [.. (await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).ToListAsync(ct))
                .Where(s => s.Vigente is { Origen: OrigenDeSeccion.Importada or OrigenDeSeccion.Portal })
                .Select(s => (s.Seccion, s.Periodo, s.Id, s.Vigente!.Numero, s.Vigente.Huella))];
        }

        int creadas = 0, existentes = 0, sinVinculo = 0;
        foreach (var (constructora, empresa) in empresas)
        {
            List<Vinculo> vinculos;
            await using (var consulta = sesiones.ConsultaGlobal())
            {
                var terceroId = tercero.Id;
                var empresaId = empresa.Id;
                vinculos = creado
                    ? []
                    : [.. await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.TerceroId == terceroId && v.EmpresaId == empresaId).ToListAsync(ct)];
            }

            if (vinculos.Count == 0)
            {
                sinVinculo++;
                continue;
            }

            await using var sesion = ConActor(sesiones.ParaTenant(empresa.TenantId), opciones);
            foreach (var vinculo in vinculos)
            {
                var vinculoId = vinculo.Id;
                var aceptadas = (await sesion.Query<SeccionAceptada>().Where(a => a.VinculoId == vinculoId).ToListAsync(ct)).Select(a => a.Id).ToHashSet();
                foreach (var (seccion, periodo, seccionId, version, huella) in vigentes)
                {
                    var id = SeccionAceptada.Clave(vinculoId, seccion, periodo);
                    if (aceptadas.Contains(id))
                    {
                        existentes++;
                        continue;
                    }

                    creadas++;
                    if (opciones.Simular)
                        continue;
                    sesion.Store(new SeccionAceptada
                    {
                        Id = id,
                        EmpresaId = empresa.Id,
                        VinculoId = vinculoId,
                        TerceroId = tercero.Id,
                        Seccion = seccion,
                        Periodo = periodo,
                        SeccionId = seccionId,
                        Version = version,
                        Huella = huella,
                        AceptadaEn = reloj.GetUtcNow(),
                        ExpedienteId = Guid.Empty,
                        Folio = string.Empty,
                        Origen = OrigenDeAceptacion.Importacion,
                        ClaveLegado = $"{IdDeLegado.ClaveTerceroPortal(origen.Terid)}|constructora:{constructora}"
                    });
                }
            }

            if (!opciones.Simular)
                await sesion.SaveChangesAsync(ct);
        }

        return (creadas, existentes, sinVinculo);
    }

    // ---------- catálogos ----------

    /// <summary>
    /// Catálogos para traducir y evaluar: los de plataforma de la base, más (si la corrida los importa) los del Portal ya
    /// validados. Así un dry-run, que no los guarda, evalúa igual que la corrida real.
    /// </summary>
    public static async Task<ICatalogosDeFicha> CatalogosAsync(ISesiones sesiones, CatalogosDelPortal? delPortal, DateTimeOffset ahora, CancellationToken ct)
    {
        List<EntradaDeCatalogo> entradas;
        await using (var consulta = sesiones.ConsultaGlobal())
            entradas = [.. await consulta.Query<EntradaDeCatalogo>().ToListAsync(ct)];
        return Combinar(entradas, delPortal, ahora);
    }

    /// <summary>Función pura: las entradas de plataforma más las filas válidas de los catálogos del Portal (estas mandan).</summary>
    public static ICatalogosDeFicha Combinar(IEnumerable<EntradaDeCatalogo> entradas, CatalogosDelPortal? delPortal, DateTimeOffset ahora)
    {
        if (delPortal is null)
            return new CatalogosEnMemoria(entradas);

        var porId = entradas.ToDictionary(e => e.Id, StringComparer.Ordinal);
        foreach (var (catalogo, filas, _) in CodigosDelPortal.Filas(delPortal))
        {
            var niveles = porId.Values.Where(e => e.Catalogo == catalogo && e.Nivel is not null).ToDictionary(e => e.Codigo, e => e.Nivel!.Value, StringComparer.Ordinal);
            foreach (var fila in filas.OrderBy(f => f.Nivel ?? 0))
            {
                var (entrada, _) = ReglasDeCatalogo.Validar(catalogo, fila, p => niveles.TryGetValue(p, out var n) ? n : null, ahora);
                if (entrada is null)
                    continue;
                if (entrada.Nivel is { } nivel)
                    niveles[entrada.Codigo] = nivel;
                porId[entrada.Id] = entrada;
            }
        }

        return new CatalogosEnMemoria(porId.Values);
    }

    /// <summary>
    /// Importa los catálogos del Portal a los de plataforma con la importación idempotente de F1 (upsert por código). En un
    /// dry-run solo cuenta. Las filas del Portal que no se pudieron convertir (sin DANE, CIIU inválido…) vuelven como rechazadas.
    /// </summary>
    public static async Task<IReadOnlyList<ResultadoImportacionCatalogoDto>> ImportarCatalogosAsync(ISesiones sesiones, CatalogosDelPortal catalogos,
        bool simular, DateTimeOffset ahora, CancellationToken ct)
    {
        var resultados = new List<ResultadoImportacionCatalogoDto>();
        foreach (var (catalogo, filas, rechazos) in CodigosDelPortal.Filas(catalogos))
        {
            var noConvertidas = rechazos.Select(r => new FilaRechazadaDto(-1, null, r)).ToList();
            if (filas.Count == 0)
            {
                if (noConvertidas.Count > 0)
                    resultados.Add(new ResultadoImportacionCatalogoDto(ReglasDeCatalogo.Ruta(catalogo), 0, 0, 0, noConvertidas));
                continue;
            }

            ResultadoImportacionCatalogoDto resultado;
            if (simular)
            {
                resultado = await SimularCatalogoAsync(sesiones, catalogo, filas, ahora, ct);
            }
            else
            {
                await using var sesion = sesiones.Global();
                resultado = await ImportacionDeCatalogos.ImportarAsync(sesion, catalogo, filas, ahora, ct);
            }

            resultados.Add(resultado with { Rechazados = [.. noConvertidas, .. resultado.Rechazados] });
        }

        return resultados;
    }

    private static async Task<ResultadoImportacionCatalogoDto> SimularCatalogoAsync(ISesiones sesiones, CatalogoDePlataforma catalogo,
        IReadOnlyList<FilaDeCatalogo> filas, DateTimeOffset ahora, CancellationToken ct)
    {
        Dictionary<string, EntradaDeCatalogo> existentes;
        await using (var consulta = sesiones.ConsultaGlobal())
            existentes = (await consulta.Query<EntradaDeCatalogo>().Where(e => e.Catalogo == catalogo).ToListAsync(ct)).ToDictionary(e => e.Codigo, StringComparer.Ordinal);
        var niveles = existentes.Values.Where(e => e.Nivel is not null).ToDictionary(e => e.Codigo, e => e.Nivel!.Value, StringComparer.Ordinal);
        int creados = 0, actualizados = 0, sinCambios = 0;
        var rechazados = new List<FilaRechazadaDto>();
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        foreach (var (fila, indice) in filas.Select((f, i) => (f, i)).OrderBy(x => x.f.Nivel ?? 0))
        {
            var (entrada, rechazo) = ReglasDeCatalogo.Validar(catalogo, fila, p => niveles.TryGetValue(p, out var n) ? n : null, ahora);
            if (entrada is null || !vistos.Add(entrada.Codigo))
            {
                rechazados.Add(new FilaRechazadaDto(indice, fila.Codigo, rechazo ?? $"El código {fila.Codigo} está repetido en la importación."));
                continue;
            }

            if (entrada.Nivel is { } nivel)
                niveles[entrada.Codigo] = nivel;
            if (!existentes.TryGetValue(entrada.Codigo, out var previa))
                creados++;
            else if (previa.MismoContenido(entrada))
                sinCambios++;
            else
                actualizados++;
        }

        return new ResultadoImportacionCatalogoDto(ReglasDeCatalogo.Ruta(catalogo), creados, actualizados, sinCambios, [.. rechazados.OrderBy(r => r.Fila)]);
    }

    // ---------- traducción a DTO ----------

    public static AvisoImportacionDto ADto(this AvisoDeTraduccion a, int terid) =>
        new(IdDeLegado.ClaveTerceroPortal(terid), terid, a.Seccion, a.Periodo, a.Campo, a.Codigo, a.Mensaje, a.Dato, a.AccionSugerida);

    public static SincronizacionPortalDto ADto(this TerceroImportado t) =>
        new(t.Terid, t.ClaveLegado, t.TerceroId, t.Creado == true,
            [.. t.Secciones.Select(s => new SeccionSincronizadaDto(s.Seccion, s.Periodo, s.Version, s.Resultado == ResultadoDeSeccionImportada.Creada,
                s.Resultado == ResultadoDeSeccionImportada.Rechazada))],
            [.. t.Advertencias.Select(a => a.ADto(t.Terid))], [.. t.Rechazos.Select(a => a.ADto(t.Terid))]);
}
