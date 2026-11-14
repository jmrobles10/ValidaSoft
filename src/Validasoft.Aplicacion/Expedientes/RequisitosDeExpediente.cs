using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>Requisito de una versión de flujo con su tipo del tenant (código, nombre, canónico y ámbito).</summary>
public sealed record RequisitoPedido(RequisitoDelFlujo Requisito, TipoDocumentoTenant Tipo);

/// <summary>
/// Puente entre la bóveda y los requisitos del expediente (RQ-2, RQ-3): qué tipos pide el flujo, qué versión compartida
/// con el vínculo cubre cada requisito y qué vínculos piden un tipo (para compartir una versión nueva). El expediente
/// decide; aquí solo se leen los documentos y se casan con los requisitos.
/// </summary>
public static class RequisitosDeExpediente
{
    /// <summary>
    /// Requisitos de la versión del flujo que se le piden a este tercero en este vínculo (los que cumplen su condición,
    /// RQ-12), con su tipo del tenant y en el orden del flujo.
    /// </summary>
    public static async Task<IReadOnlyList<RequisitoPedido>> DelFlujoAsync(IQuerySession sesionDelTenant, FlujoVersion version,
        ContextoDeExigencia contexto, CancellationToken ct)
    {
        var aplican = EvaluadorDeExigencia.Filtrar(version.Requisitos.Where(r => !r.EsSeccion), contexto);
        if (aplican.Count == 0)
            return [];

        var ids = aplican.Select(r => r.TipoDocumentoTenantId).Distinct().ToArray();
        var tipos = (await sesionDelTenant.LoadManyAsync<TipoDocumentoTenant>(ct, ids)).ToDictionary(t => t.Id);
        return [.. aplican.Where(r => tipos.ContainsKey(r.TipoDocumentoTenantId)).Select(r => new RequisitoPedido(r, tipos[r.TipoDocumentoTenantId]))];
    }

    /// <summary>
    /// Requisitos de sección de la versión del flujo que se le piden a este tercero en este vínculo (condición y tipo de
    /// persona: la natural no tiene Legal ni Financiera), uno por sección y, en la financiera, uno por periodo exigido (F2).
    /// </summary>
    public static IReadOnlyList<RequisitoDeSeccionPedido> SeccionesDelFlujo(FlujoVersion version, ContextoDeExigencia contexto, DateOnly hoy) =>
        RequisitosDeSeccion.Expandir(EvaluadorDeExigencia.Filtrar(version.Requisitos.Where(r => r.EsSeccion), contexto), hoy);

    /// <summary>Las secciones de la ficha del tercero, por id de stream (tenant técnico <c>boveda</c> en la misma sesión).</summary>
    public static async Task<IReadOnlyDictionary<Guid, SeccionDeFicha>> FichaAsync(IDocumentSession sesionDelTenant, Guid terceroId,
        CancellationToken ct) =>
        (await sesionDelTenant.ForTenant(TenantsTecnicos.Boveda).Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).ToListAsync(ct))
        .ToDictionary(s => s.Id);

    /// <summary>Lo aceptado por la empresa de cada sección en el vínculo, por clave (vínculo, sección, periodo).</summary>
    public static async Task<IReadOnlyDictionary<string, SeccionAceptada>> AceptadasAsync(IQuerySession sesionDelTenant, Guid vinculoId,
        CancellationToken ct) =>
        (await sesionDelTenant.Query<SeccionAceptada>().Where(a => a.VinculoId == vinculoId).ToListAsync(ct)).ToDictionary(a => a.Id);

    /// <summary>Versión aceptada de la sección como versión de requisito (base de la comparación de una actualización).</summary>
    public static VersionDeRequisito? Aceptada(IReadOnlyDictionary<string, SeccionAceptada> aceptadas, Guid vinculoId, RequisitoDeSeccionPedido pedido) =>
        aceptadas.TryGetValue(SeccionAceptada.Clave(vinculoId, pedido.Definicion.Codigo, pedido.Periodo), out var a)
            ? new VersionDeRequisito(a.SeccionId, a.Version, a.Huella, a.AceptadaEn)
            : null;

    /// <summary>
    /// Foto del requisito de sección para <c>RequisitosCalculados</c>: con <paramref name="ficha"/> (el tercero ya aceptó la
    /// invitación: la ficha está disponible para la empresa) se evalúa la versión vigente con la regla de «completa para la
    /// empresa» y solo la cubre si lo es; sin ella, nace pendiente sin evaluar.
    /// </summary>
    public static RequisitoCalculado CalcularSeccion(RequisitoDeSeccionPedido pedido, Guid terceroId, IReadOnlyDictionary<Guid, SeccionDeFicha>? ficha,
        VersionDeRequisito? aceptada) =>
        pedido.Calcular(ficha is null ? null : RequisitosDeSeccion.Evaluar(pedido, ficha.GetValueOrDefault(pedido.SeccionId(terceroId))), aceptada);

    /// <summary>Tercero, tipo de vínculo y atributos del vínculo: con qué se evalúan las condiciones de sus requisitos.</summary>
    public static async Task<ContextoDeExigencia> ContextoAsync(IQuerySession sesionDelTenant, Vinculo vinculo, CancellationToken ct)
    {
        var tercero = await sesionDelTenant.LoadAsync<Tercero>(vinculo.TerceroId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero del vínculo no existe.");
        var tipo = await sesionDelTenant.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct);
        return ContextoDeExigencia.De(tercero, tipo?.Codigo ?? string.Empty, vinculo.Atributos);
    }

    /// <summary>Canónicos de los tipos globales que se piden: lo que la bóveda del tercero comparte con el vínculo.</summary>
    public static IReadOnlySet<string> CodigosGlobales(IEnumerable<RequisitoPedido> pedidos) =>
        pedidos.Where(p => p.Tipo.Ambito == AmbitoDocumento.Global && p.Tipo.TipoCanonico is not null)
            .Select(p => p.Tipo.TipoCanonico!)
            .ToHashSet(StringComparer.Ordinal);

    /// <summary>Foto del requisito para <c>RequisitosCalculados</c>, con la versión que ya lo cubre (si está compartida con el vínculo).</summary>
    public static RequisitoCalculado Calcular(RequisitoPedido pedido, IEnumerable<Documento> documentos, Guid vinculoId)
    {
        var tipo = pedido.Tipo;
        return new RequisitoCalculado(pedido.Requisito.Id, tipo.Id, tipo.Codigo, tipo.Nombre, tipo.TipoCanonico, tipo.Ambito,
            pedido.Requisito.Obligatorio, pedido.Requisito.AreasQueRevisan, pedido.Requisito.AprobadoresRequeridos,
            Cubre(tipo.Id, tipo.TipoCanonico, tipo.Ambito, documentos, vinculoId), DiasRevalidacion: tipo.DiasRevalidacion);
    }

    /// <summary>
    /// Marca en cada requisito ya cubierto si la misma empresa aprobó esa misma versión del documento en otro expediente
    /// aprobado del mismo tercero (otro tipo de vínculo, RQ-11): el expediente nuevo la aprueba sin volver a revisarla y
    /// solo pide lo que falte. Toma el expediente aprobado más reciente; la versión debe ser exactamente la misma.
    /// </summary>
    public static async Task<IReadOnlyList<RequisitoCalculado>> ConAprobacionesPreviasAsync(IQuerySession sesionDelTenant, Vinculo vinculo,
        IReadOnlyList<RequisitoCalculado> requisitos, CancellationToken ct)
    {
        if (requisitos.All(r => r.Version is null))
            return requisitos;

        var empresaId = vinculo.EmpresaId;
        var terceroId = vinculo.TerceroId;
        var vinculoId = vinculo.Id;
        var aprobados = (await sesionDelTenant.Query<Expediente>()
                .Where(e => e.EmpresaId == empresaId && e.TerceroId == terceroId && e.VinculoId != vinculoId && e.Estado == EstadoExpediente.Aprobado)
                .ToListAsync(ct))
            .OrderByDescending(e => e.ActualizadoEn)
            .ToList();
        if (aprobados.Count == 0)
            return requisitos;

        return [.. requisitos.Select(requisito =>
        {
            if (requisito.Version is not { } version)
                return requisito;
            var previa = aprobados
                .SelectMany(e => e.Requisitos
                    .Where(r => !r.Eximido && r.Estado == EstadoRequisito.Aprobado && r.VersionActual is { } v &&
                                v.DocumentoId == version.DocumentoId && v.Numero == version.Numero)
                    .Select(_ => new AprobacionPrevia(e.Id, e.Folio, e.TipoVinculoNombre, version.DocumentoId, version.Numero)))
                .FirstOrDefault();
            return previa is null ? requisito : requisito with { Previa = previa };
        })];
    }

    /// <summary>
    /// Versión que cubre un requisito: la actual del documento global del canónico (o del <c>PorVinculo</c> del vínculo y
    /// ese tipo), siempre que ya esté compartida con el vínculo y no esté vencida para él (RN-1: lo vencido se renueva con
    /// una versión nueva o se ratifica); si no, nula. Si el vínculo tiene lo que subió una persona con acceso aislado (el documento en cuarentena del
    /// canónico), lo cubre mientras la bóveda no tenga otra cosa compartida con el vínculo: con el acceso aprobado lo subido se promueve a la bóveda y,
    /// si su contenido es el mismo, el requisito no cambia de versión (no se revisa dos veces el mismo archivo); una versión distinta de la bóveda manda.
    /// </summary>
    public static VersionDeRequisito? Cubre(Guid tipoDocumentoTenantId, string? tipoCanonico, AmbitoDocumento ambito, IEnumerable<Documento> documentos,
        Guid vinculoId)
    {
        if (ambito != AmbitoDocumento.Global)
            return Vigente(documentos.FirstOrDefault(d => d.Ambito == AmbitoDocumento.PorVinculo && !d.EnCuarentena && d.VinculoId == vinculoId &&
                                                          d.TipoDocumentoTenantId == tipoDocumentoTenantId), vinculoId);

        var global = tipoCanonico is null
            ? null
            : Vigente(documentos.FirstOrDefault(d => d.Ambito == AmbitoDocumento.Global && d.TipoCanonico == tipoCanonico), vinculoId);
        var cuarentena = tipoCanonico is null
            ? null
            : Vigente(documentos.FirstOrDefault(d => d.EnCuarentena && d.VinculoId == vinculoId && d.TipoCanonico == tipoCanonico), vinculoId);
        return cuarentena is not null && (global is null || string.Equals(global.Sha256, cuarentena.Sha256, StringComparison.OrdinalIgnoreCase))
            ? cuarentena
            : global;
    }

    /// <summary>La versión actual del documento si ya está compartida con el vínculo y no venció para él.</summary>
    private static VersionDeRequisito? Vigente(Documento? documento, Guid vinculoId)
    {
        if (documento?.VersionActual is not { } numero || !documento.EstaCompartida(numero, vinculoId))
            return null;

        var version = documento.VersionRequerida(numero);
        return version.VigenciaPara(vinculoId)?.VencidaEn is not null
            ? null
            : new VersionDeRequisito(documento.Id, numero, version.Sha256, version.CargadaEn);
    }

    /// <summary>
    /// Eventos que ponen al día el expediente con la bóveda: a cada requisito, la versión actual compartida con su vínculo
    /// (<see cref="Expediente.SincronizarVersiones"/>; idempotente). Lee los documentos con <c>ForTenant("boveda")</c> en la
    /// misma sesión del tenant.
    /// </summary>
    public static async Task<IReadOnlyList<object>> SincronizarAsync(IDocumentSession sesionDelTenant, Expediente expediente, DateTimeOffset ahora,
        CancellationToken ct)
    {
        if (!expediente.EstaAbierto || !expediente.ConRequisitos)
            return [];

        var terceroId = expediente.TerceroId;
        var documentos = await sesionDelTenant.ForTenant(TenantsTecnicos.Boveda).Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
        var disponibles = new Dictionary<Guid, VersionDeRequisito>();
        foreach (var requisito in expediente.Requisitos.Where(r => !r.EsSeccion))
        {
            if (Cubre(requisito.TipoDocumentoTenantId, requisito.TipoCanonico, requisito.Ambito, documentos, expediente.VinculoId) is { } version)
                disponibles[requisito.Id] = version;
        }

        return expediente.SincronizarVersiones(disponibles, ahora, await EvaluarSeccionesAsync(sesionDelTenant, expediente, ct),
            Revalidacion.RetiradasDe(documentos, ahora));
    }

    /// <summary>
    /// Evaluación de la versión vigente de cada sección que pide el expediente (F2), solo si el tercero ya aceptó la invitación
    /// (antes, nada de su ficha está disponible para la empresa, B-1). La regla es la única de «completa para la empresa».
    /// </summary>
    private static async Task<IReadOnlyDictionary<Guid, EvaluacionDeSeccion>> EvaluarSeccionesAsync(IDocumentSession sesionDelTenant,
        Expediente expediente, CancellationToken ct)
    {
        var secciones = expediente.Requisitos.Where(r => r.EsSeccion && !r.Eximido).ToList();
        if (secciones.Count == 0 || await sesionDelTenant.LoadAsync<Vinculo>(expediente.VinculoId, ct) is not { InvitacionAceptada: true, BovedaDisponible: true })
            return new Dictionary<Guid, EvaluacionDeSeccion>();

        var ficha = await FichaAsync(sesionDelTenant, expediente.TerceroId, ct);
        var evaluaciones = new Dictionary<Guid, EvaluacionDeSeccion>();
        foreach (var requisito in secciones)
        {
            if (CatalogoDeSecciones.Buscar(requisito.Seccion) is not { } definicion)
                continue;
            var seccion = ficha.GetValueOrDefault(SeccionDeFicha.IdPara(expediente.TerceroId, definicion.Codigo, requisito.Periodo));
            evaluaciones[requisito.Id] = RequisitosDeSeccion.Evaluar(definicion, requisito.Periodo, requisito.ExigeSoporte, seccion);
        }

        return evaluaciones;
    }

    /// <summary>
    /// Vínculos (de cualquier tenant) que piden el canónico global <paramref name="codigo"/>: su expediente abierto tiene
    /// un requisito de ese tipo o su flujo vigente (el del vínculo publicado, si no el por defecto) lo pide a este tercero
    /// en ese vínculo (RQ-3, RQ-12).
    /// </summary>
    public static async Task<IReadOnlySet<Guid>> VinculosQuePidenAsync(IQuerySession consulta, IReadOnlyList<Vinculo> vinculos, string codigo,
        CancellationToken ct)
    {
        var piden = new HashSet<Guid>();
        if (vinculos.Count == 0)
            return piden;

        var ids = vinculos.Select(v => v.Id).ToArray();
        var expedientes = await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.VinculoId.IsOneOf(ids)).ToListAsync(ct);
        foreach (var expediente in expedientes.Where(e => e.EstaAbierto))
        {
            if (expediente.Requisitos.Any(r => r.Ambito == AmbitoDocumento.Global && r.TipoCanonico == codigo))
                piden.Add(expediente.VinculoId);
        }

        var claves = vinculos.Select(v => FlujoPorDefecto.Clave(v.EmpresaId, v.TipoVinculoId)).Distinct().ToArray();
        var porDefecto = await consulta.Query<FlujoPorDefecto>().Where(p => p.AnyTenant() && p.Id.IsOneOf(claves)).ToListAsync(ct);
        // Los flujos de cada (empresa, tipo): el del vínculo, el por defecto y los que se escogen por regla de selección (FR-1).
        var flujos = await ProcesoDeValidacion.CargarFlujosDeLosVinculosAsync(consulta, vinculos, ct);
        if (flujos.Count == 0)
            return piden;

        var tipoIds = flujos.SelectMany(f => f.VersionPublicada?.Requisitos ?? []).Select(r => r.TipoDocumentoTenantId).Distinct().ToArray();
        if (tipoIds.Length == 0)
            return piden;

        var tipos = await consulta.Query<TipoDocumentoTenant>().Where(t => t.AnyTenant() && t.Id.IsOneOf(tipoIds)).ToListAsync(ct);
        var delCodigo = tipos.Where(t => t.Ambito == AmbitoDocumento.Global && t.TipoCanonico == codigo).Select(t => t.Id).ToHashSet();
        if (delCodigo.Count == 0)
            return piden;

        var terceroIds = vinculos.Select(v => v.TerceroId).Distinct().ToArray();
        var terceros = (await consulta.LoadManyAsync<Tercero>(ct, terceroIds)).ToDictionary(t => t.Id);
        var tipoVinculoIds = vinculos.Select(v => v.TipoVinculoId).Distinct().ToArray();
        var tiposVinculo = (await consulta.Query<TipoVinculo>().Where(t => t.AnyTenant() && t.Id.IsOneOf(tipoVinculoIds)).ToListAsync(ct))
            .ToDictionary(t => t.Id);
        foreach (var vinculo in vinculos)
        {
            if (!terceros.TryGetValue(vinculo.TerceroId, out var tercero))
                continue;
            var contexto = ContextoDeExigencia.De(tercero, tiposVinculo.GetValueOrDefault(vinculo.TipoVinculoId)?.Codigo ?? string.Empty, vinculo.Atributos);
            if (ProcesoDeValidacion.FlujoVigente(vinculo, flujos, porDefecto, contexto) is not { } version)
                continue;
            if (EvaluadorDeExigencia.Filtrar(version.Requisitos.Where(r => delCodigo.Contains(r.TipoDocumentoTenantId)), contexto).Count > 0)
                piden.Add(vinculo.Id);
        }

        return piden;
    }
}
