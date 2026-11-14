using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Ficha.Importacion;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Importacion;

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

/// <summary>Resultado de importar (o sincronizar) un tercero de un sistema de origen.</summary>
public sealed record TerceroImportado(
    string Sistema, string Clave, Guid? TerceroId, bool? Creado, IReadOnlyList<SeccionImportada> Secciones, int Aceptaciones, int AceptacionesExistentes,
    int SinVinculo, IReadOnlyList<AvisoDeTraduccion> Advertencias, IReadOnlyList<AvisoDeTraduccion> Rechazos)
{
    public string ClaveLegado => IdDeLegado.Clave(Sistema, Clave);

    public bool Rechazado => TerceroId is null;
}

/// <summary>
/// Cómo se importa: <see cref="Simular"/> (dry-run: lee y calcula, no escribe), el <see cref="Sistema"/> de origen (código del
/// adaptador), el origen de las versiones (<c>Importada</c> en la corrida; <c>SistemaExterno</c> en el sincronizador), el mapa unidad
/// de origen ⇒ empresa para las aceptaciones, el actor para la auditoría y el bus del outbox (para pedir la sincronización de
/// requisitos de F2 en la misma transacción).
/// </summary>
public sealed record OpcionesDeImportacion(
    bool Simular, string Sistema, OrigenDeSeccion Origen, IReadOnlyDictionary<string, Empresa> Unidades, ActorTipo ActorTipo, string ActorId, IMessageBus? Mensajes);

/// <summary>
/// El importador genérico de la ficha (F4, FM-39…FM-43): por tercero de un sistema de origen (el adaptador lo lee y lo traduce, sin
/// E/S), crea o encuentra el <see cref="Tercero"/> (id UUID v5 de <c>{sistema}:{clave}</c>; por identificación normalizada si ya
/// existía), le anota su referencia externa del sistema (<see cref="ReferenciaDeTercero"/> de la plataforma), registra cada sección
/// como versión con el mismo guardado del wizard (misma huella ⇒ nada; sin soporte: las versiones importadas no lo exigen) y, con el
/// mapa de unidades, deja aceptadas por la empresa las secciones del tercero que en el origen estaba aprobado en su unidad.
/// Idempotente. No sabe nada de ningún sistema concreto.
/// </summary>
public static class ImportadorDeTerceros
{
    public static async Task<TerceroImportado> ImportarTerceroAsync(ISesiones sesiones, TerceroDeOrigen origen, ICatalogosDeFicha plataforma,
        OpcionesDeImportacion opciones, TimeProvider reloj, CancellationToken ct)
    {
        var hoy = reloj.Hoy();
        var traduccion = origen.Traducir(plataforma, hoy, null);
        if (traduccion.Tercero is null)
            return Rechazado(traduccion);

        var idDeLegado = IdDeLegado.Tercero(traduccion.Sistema, traduccion.Clave);
        var sistemaDeReferencia = ReferenciaDeTercero.NormalizarSistema(traduccion.Sistema);

        // El tercero: por su id del legado (reimportación) o por su identificación (ya lo registró una empresa).
        Tercero? tercero;
        List<ReferenciaDeTercero> referencias = [];
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            tercero = await consulta.LoadAsync<Tercero>(idDeLegado, ct);
            var clave = traduccion.Tercero.Identificacion.Clave;
            tercero ??= await consulta.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == clave, ct);
        }

        if (tercero is not null)
        {
            await using var boveda = sesiones.ConsultaBoveda();
            var terceroId = tercero.Id;
            referencias = [.. await boveda.Query<ReferenciaDeTercero>().Where(r => r.TerceroId == terceroId && r.Sistema == sistemaDeReferencia).ToListAsync(ct)];
        }

        var rechazosExtra = new List<AvisoDeTraduccion>();
        if (tercero is not null)
        {
            if (tercero.ClaveIdentificacion != traduccion.Tercero.Identificacion.Clave)
                return Rechazado(traduccion, new AvisoDeTraduccion(null, null, "numero", "identificacion-cambiada",
                    $"La clave {origen.Clave} de {traduccion.Sistema} ya se importó como {tercero.ClaveIdentificacion} y el origen hoy dice {traduccion.Tercero.Identificacion.Clave}.",
                    traduccion.Tercero.Identificacion.Clave, "Revisar en el sistema de origen: un tercero no cambia de identificación."));
            if (referencias.FirstOrDefault(r => !string.Equals(r.Clave, traduccion.Clave, StringComparison.Ordinal)) is { } otra)
                return Rechazado(traduccion, new AvisoDeTraduccion(null, null, "numero", "tercero-duplicado-en-origen",
                    $"La identificación {tercero.ClaveIdentificacion} ya se importó desde {traduccion.Sistema}:{otra.Clave}: el origen tiene el tercero dos veces.",
                    traduccion.ClaveLegado, "Fusionar los terceros en el sistema de origen (o descartar el duplicado) y volver a importar."));
            if (tercero.TipoPersona != traduccion.Tercero.TipoPersona)
            {
                traduccion = origen.Traducir(plataforma, hoy, tercero.TipoPersona);
                if (traduccion.Tercero is null)
                    return Rechazado(traduccion);
            }
        }

        var creado = tercero is null;
        if (tercero is null)
        {
            tercero = Tercero.Crear(traduccion.Tercero.Identificacion, traduccion.Tercero.TipoPersona, traduccion.Tercero.Nombre, null, null, reloj.GetUtcNow());
            tercero.Id = idDeLegado;
            if (!opciones.Simular)
                await GuardarTerceroAsync(sesiones, tercero, opciones, ct);
        }

        // La clave del tercero en el sistema de origen queda como su referencia externa (idempotente: una sola por sistema y clave).
        if (!opciones.Simular && referencias.All(r => !string.Equals(r.Clave, traduccion.Clave, StringComparison.Ordinal)))
            await GuardarReferenciaAsync(sesiones, tercero, traduccion, opciones, reloj, ct);

        // El correo que trae el origen es un contacto del tercero, verificado por la importación (la corre la plataforma, no una empresa).
        if (!opciones.Simular)
            await RegistrarContactoImportadoAsync(sesiones, tercero, traduccion, opciones, reloj, ct);

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
                    e.Codigo, e.Message, null, AccionCorregirEnOrigen));
                secciones.Add(new SeccionImportada(seccion.Definicion.Codigo, seccion.Periodo, null, ResultadoDeSeccionImportada.Rechazada));
            }
        }

        var (aceptaciones, existentes, sinVinculo) = opciones.Unidades.Count == 0
            ? (0, 0, 0)
            : await AceptarAsync(sesiones, origen, traduccion, tercero, creado, secciones, opciones, reloj, ct);

        return new TerceroImportado(traduccion.Sistema, traduccion.Clave, tercero.Id, creado, secciones, aceptaciones, existentes, sinVinculo,
            traduccion.Advertencias, [.. traduccion.Rechazos, .. rechazosExtra]);
    }

    private static TerceroImportado Rechazado(ResultadoDeTraduccion traduccion, AvisoDeTraduccion? extra = null) =>
        new(traduccion.Sistema, traduccion.Clave, null, null, [], 0, 0, 0, traduccion.Advertencias,
            extra is null ? traduccion.Rechazos : [.. traduccion.Rechazos, extra]);

    /// <summary>Acción sugerida de un rechazo por un dato del origen.</summary>
    public const string AccionCorregirEnOrigen = "Corregir el dato en el sistema de origen y volver a sincronizar el tercero.";

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
    /// El correo de la información general que trae el sistema de origen pasa a ser contacto <b>verificado</b> del tercero (medio <c>Importacion</c>): lo importa la
    /// plataforma desde el sistema donde el tercero se registró, no una empresa que podría poner el suyo. Si ya era contacto sin verificar, se verifica. Si por eso otro
    /// contacto sin verificar queda desplazado, se pide poner al día el acceso (decisión AV). Idempotente: repetir la importación no cambia nada.
    /// </summary>
    private static async Task RegistrarContactoImportadoAsync(ISesiones sesiones, Tercero tercero, ResultadoDeTraduccion traduccion, OpcionesDeImportacion opciones,
        TimeProvider reloj, CancellationToken ct)
    {
        var correo = SolicitudDeAcceso.Normalizar((traduccion.Secciones.FirstOrDefault(s => s.Definicion.Codigo == CodigosDeSeccion.General)?.Datos as DatosGenerales)?.Correo);
        if (correo.Length == 0 || !correo.Contains('@'))
            return;

        await using var sesion = ConActor(sesiones.Global(opciones.Mensajes), opciones);
        var actual = await sesion.LoadAsync<Tercero>(tercero.Id, ct);
        if (actual is null)
            return;

        var ahora = reloj.GetUtcNow();
        var por = $"importador:{traduccion.Sistema}";
        var cambio = ContactosDelTercero.Agregar(sesion, actual, correo, actual.Nombre, "Principal", ContactosDelTercero.OrigenImportacion, traduccion.ClaveLegado,
                         MedioDeVerificacion.Importacion, por, ahora) ||
                     ContactosDelTercero.Verificar(sesion, actual, correo, MedioDeVerificacion.Importacion, por, ahora);
        if (!cambio)
            return;

        if (opciones.Mensajes is { } mensajes)
            await mensajes.PublishAsync(new SincronizarAccesoDelTercero(actual.Id));
        await sesion.SaveChangesAsync(ct);
    }

    private static async Task GuardarReferenciaAsync(ISesiones sesiones, Tercero tercero, ResultadoDeTraduccion traduccion, OpcionesDeImportacion opciones,
        TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = ConActor(sesiones.Boveda(), opciones);
        sesion.Store(ReferenciaDeTercero.Crear(tercero.Id, traduccion.Sistema, traduccion.Clave, reloj.GetUtcNow()));
        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>
    /// La versión con el mismo guardado del wizard (<see cref="GuardadoDeSecciones.RegistrarAsync"/>): sin soportes (las versiones
    /// importadas no exigen el soporte del cambio: su respaldo es el legado), origen <c>Importada</c> o <c>SistemaExterno</c> y, con
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
    /// Aceptación importada (FM-42): por cada unidad de origen mapeada donde el tercero estaba aprobado, en cada vínculo del tercero
    /// con esa empresa, la versión vigente de cada sección (si salió de un sistema de origen: origen
    /// <c>Importada</c> o <c>SistemaExterno</c>) queda en <see cref="SeccionAceptada"/> con origen <c>Importacion</c>, sin expediente.
    /// Solo si el vínculo no tenía nada aceptado de esa sección: una aceptación existente (de un expediente o de una corrida
    /// anterior) no se toca, y los cambios posteriores se revalidan con F2. Sin vínculo en la empresa ⇒ se cuenta y se
    /// acepta en una corrida posterior, cuando exista.
    /// </summary>
    private static async Task<(int Creadas, int Existentes, int SinVinculo)> AceptarAsync(ISesiones sesiones, TerceroDeOrigen origen,
        ResultadoDeTraduccion traduccion, Tercero tercero, bool creado, IReadOnlyList<SeccionImportada> importadas, OpcionesDeImportacion opciones,
        TimeProvider reloj, CancellationToken ct)
    {
        var empresas = origen.UnidadesAceptadas
            .Where(opciones.Unidades.ContainsKey)
            .Select(u => (Unidad: u, Empresa: opciones.Unidades[u]))
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
                .Where(s => s.Vigente is { Origen: OrigenDeSeccion.Importada or OrigenDeSeccion.SistemaExterno })
                .Select(s => (s.Seccion, s.Periodo, s.Id, s.Vigente!.Numero, s.Vigente.Huella))];
        }

        int creadas = 0, existentes = 0, sinVinculo = 0;
        foreach (var (unidad, empresa) in empresas)
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
                        ClaveLegado = $"{traduccion.ClaveLegado}|unidad:{unidad}"
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
    /// Catálogos para traducir y evaluar: los de plataforma de la base, más (si la corrida los importa) los del origen ya
    /// validados. Así un dry-run, que no los guarda, evalúa igual que la corrida real.
    /// </summary>
    public static async Task<ICatalogosDeFicha> CatalogosAsync(ISesiones sesiones, IReadOnlyList<CatalogoImportable>? delOrigen, DateTimeOffset ahora, CancellationToken ct)
    {
        List<EntradaDeCatalogo> entradas;
        await using (var consulta = sesiones.ConsultaGlobal())
            entradas = [.. await consulta.Query<EntradaDeCatalogo>().ToListAsync(ct)];
        return Combinar(entradas, delOrigen, ahora);
    }

    /// <summary>Función pura: las entradas de plataforma más las filas válidas de los catálogos del origen (estas mandan).</summary>
    public static ICatalogosDeFicha Combinar(IEnumerable<EntradaDeCatalogo> entradas, IReadOnlyList<CatalogoImportable>? delOrigen, DateTimeOffset ahora)
    {
        if (delOrigen is null)
            return new CatalogosEnMemoria(entradas);

        var porId = entradas.ToDictionary(e => e.Id, StringComparer.Ordinal);
        foreach (var (catalogo, filas, _) in delOrigen)
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
    /// Importa los catálogos del origen a los de plataforma con la importación idempotente de F1 (upsert por código). En un
    /// dry-run solo cuenta. Las filas del origen que no se pudieron convertir (sin DANE, CIIU inválido…) vuelven como rechazadas.
    /// </summary>
    public static async Task<IReadOnlyList<ResultadoImportacionCatalogoDto>> ImportarCatalogosAsync(ISesiones sesiones, IReadOnlyList<CatalogoImportable> catalogos,
        bool simular, DateTimeOffset ahora, CancellationToken ct)
    {
        var resultados = new List<ResultadoImportacionCatalogoDto>();
        foreach (var (catalogo, filas, rechazos) in catalogos)
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

    public static AvisoImportacionDto ADto(this AvisoDeTraduccion a, string sistema, string clave) =>
        new(IdDeLegado.Clave(sistema, clave), clave, a.Seccion, a.Periodo, a.Campo, a.Codigo, a.Mensaje, a.Dato, a.AccionSugerida);

    public static SincronizacionDto ADto(this TerceroImportado t) =>
        new(t.Sistema, t.Clave, t.ClaveLegado, t.TerceroId, t.Creado == true,
            [.. t.Secciones.Select(s => new SeccionSincronizadaDto(s.Seccion, s.Periodo, s.Version, s.Resultado == ResultadoDeSeccionImportada.Creada,
                s.Resultado == ResultadoDeSeccionImportada.Rechazada))],
            [.. t.Advertencias.Select(a => a.ADto(t.Sistema, t.Clave))], [.. t.Rechazos.Select(a => a.ADto(t.Sistema, t.Clave))]);
}
