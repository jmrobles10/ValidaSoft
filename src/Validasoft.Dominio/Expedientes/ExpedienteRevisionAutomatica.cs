using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Revision;

namespace Validasoft.Dominio.Expedientes;

/// <summary>Lo que una persona resolvió sobre una aprobación por muestreo: <c>Confirmada</c> o <c>Revertida</c> (vuelve a revisión humana).</summary>
public enum DecisionHumanaDeRevision
{
    Confirmada,
    Revertida
}

public sealed record ResolucionDeRevision(DecisionHumanaDeRevision Decision, string Por, string? Justificacion, DateTimeOffset Fecha);

/// <summary>
/// Lo que la revisión automática registró para un documento, un área y una versión (un registro por <c>(requisito, área, versión)</c>).
/// <see cref="VeredictoId"/>: el veredicto del sistema que emitió (solo si aprobó). <see cref="Resolucion"/>: lo que una persona resolvió
/// de un muestreo.
/// </summary>
public sealed class RevisionAutomaticaDeRequisito
{
    public Guid AreaId { get; set; }

    public int SobreVersion { get; set; }

    public NivelDeRevisionAutomatica Nivel { get; set; }

    public ResultadoDeRevision Resultado { get; set; }

    public SugerenciaDeRevision? Sugerencia { get; set; }

    public List<MotivoDeRevision> Motivos { get; set; } = [];

    public decimal? Confianza { get; set; }

    public Guid? VeredictoId { get; set; }

    public DateTimeOffset Fecha { get; set; }

    public ResolucionDeRevision? Resolucion { get; set; }
}

/// <summary>
/// La revisión automática resolvió un documento para un área (ADR-0012). Una aprobación (<c>AprobadaAutomaticamente</c> o
/// <c>AprobadaConMuestreo</c>) lleva el <see cref="VeredictoId"/> del veredicto del sistema que se emite junto con este evento.
/// </summary>
public sealed record RevisionAutomaticaRegistrada(
    Guid RequisitoId,
    Guid AreaId,
    int SobreVersion,
    NivelDeRevisionAutomatica Nivel,
    ResultadoDeRevision Resultado,
    SugerenciaDeRevision? Sugerencia,
    IReadOnlyList<MotivoDeRevision> Motivos,
    decimal? Confianza,
    Guid? VeredictoId,
    DateTimeOffset Fecha);

/// <summary>Una persona confirmó o revirtió una aprobación por muestreo (revertir anula el veredicto del sistema y devuelve el documento a revisión humana).</summary>
public sealed record RevisionAutomaticaResuelta(
    Guid RequisitoId,
    Guid AreaId,
    int SobreVersion,
    DecisionHumanaDeRevision Decision,
    string Por,
    string? Justificacion,
    DateTimeOffset Fecha);

/// <summary>Documento y área que esperan la revisión automática (y la versión que verá).</summary>
public sealed record CandidataDeRevision(RequisitoDeExpediente Requisito, AreaDeExpediente Area, int Version);

/// <summary>Lo que el evaluador propone registrar para un documento y un área.</summary>
public sealed record PropuestaDeRevision(Guid RequisitoId, Guid AreaId, int SobreVersion, DecisionDeRevision Decision);

/// <summary>
/// Revisión automática del expediente (ADR-0012): el sistema registra qué hizo con cada documento y área (sugerir, aprobar,
/// aprobar con muestreo, mandar a una persona). Las aprobaciones emiten además, en la misma cascada, el veredicto de requisito
/// con origen <c>Sistema</c> de las áreas autorizadas de la etapa activa; así el área, la etapa y el cierre avanzan solos. Las
/// secciones de la ficha nunca se aprueban solas.
/// </summary>
public sealed partial class Expediente
{
    /// <summary>Revisor que figura en los veredictos que emite la revisión automática.</summary>
    public const string RevisorAutomatico = "revision-automatica";

    /// <summary>En revisión con algún documento y área de la etapa activa sin registro de la revisión automática para su versión actual.</summary>
    public bool PorRevisarAutomaticamente { get; set; }

    /// <summary>Excepciones abiertas (una persona debe decidir) más aprobaciones por muestreo sin confirmar.</summary>
    public int ExcepcionesDeRevisionPendientes { get; set; }

    /// <summary>Tiene excepciones o muestreos registrados (abiertos o resueltos): alimenta la bandeja de excepciones.</summary>
    public bool ConRevisionAutomatica { get; set; }

    /// <summary>
    /// Documentos (no secciones) de las áreas de la etapa activa que esperan la revisión automática: con versión, sin decidir y sin
    /// registro para esa versión. Las áreas automáticas no entran (aprueban solas en la cascada).
    /// </summary>
    public IReadOnlyList<CandidataDeRevision> CandidatasDeRevisionAutomatica()
    {
        if (Estado != EstadoExpediente.EnRevision || !ConRequisitos || EtapaActiva is not { } etapa)
            return [];
        var candidatas = new List<CandidataDeRevision>();
        foreach (var area in etapa.Areas.Where(a => !a.Automatica))
        {
            foreach (var requisito in RequisitosDelArea(area.AreaId))
            {
                if (requisito.EsSeccion || requisito.Eximido || requisito.VersionActual is not { } version || requisito.DecididoPor(area))
                    continue;
                if (requisito.Revisiones.Any(r => r.AreaId == area.AreaId && r.SobreVersion == version.Numero))
                    continue;
                candidatas.Add(new CandidataDeRevision(requisito, area, version.Numero));
            }
        }

        return candidatas;
    }

    /// <summary>
    /// Registra lo que decidió la revisión automática. Cada propuesta se vuelve a validar contra el estado actual (sigue pendiente,
    /// la misma versión, el área en la etapa activa); las que ya no valen se ignoran, así que repetirlo no duplica nada. Sin
    /// propuestas válidas no emite eventos. Las aprobaciones emiten el veredicto del sistema y la cascada hace el resto.
    /// </summary>
    public IReadOnlyList<object> RegistrarRevisionesAutomaticas(IReadOnlyList<PropuestaDeRevision> propuestas, NivelDeRevisionAutomatica nivel,
        DateTimeOffset ahora)
    {
        if (Estado != EstadoExpediente.EnRevision || propuestas.Count == 0)
            return [];
        var vigentes = CandidatasDeRevisionAutomatica().Select(c => (c.Requisito.Id, c.Area.AreaId, c.Version)).ToHashSet();
        var validas = propuestas.Where(p => vigentes.Contains((p.RequisitoId, p.AreaId, p.SobreVersion))).DistinctBy(p => (p.RequisitoId, p.AreaId)).ToList();
        if (validas.Count == 0)
            return [];

        return Simular(s =>
        {
            foreach (var propuesta in validas)
            {
                var decision = propuesta.Decision;
                Guid? veredictoId = decision.Aprueba ? Guid.CreateVersion7() : null;
                s.Emitir(new RevisionAutomaticaRegistrada(propuesta.RequisitoId, propuesta.AreaId, propuesta.SobreVersion, nivel, decision.Resultado,
                    decision.Sugerencia, [.. decision.Motivos], decision.Confianza, veredictoId, ahora));
                if (veredictoId is { } id)
                    s.Emitir(new VeredictoDeRequisitoEmitido(id, propuesta.RequisitoId, propuesta.AreaId, Veredicto.Aprobado, OrigenVeredicto.Sistema,
                        RevisorAutomatico, propuesta.SobreVersion, [], null, JustificacionAutomatica(decision), ahora));
            }
        }, ahora);
    }

    /// <summary>
    /// Una persona del área confirma o revierte una aprobación por muestreo. Confirmar cierra el muestreo (también con el expediente ya
    /// cerrado). Revertir anula el veredicto del sistema y devuelve el documento a revisión humana: solo con el expediente en revisión
    /// y la etapa del área activa (si ya se cerró, se desaprueba el expediente o se reabre la etapa). 404 <c>revision-no-encontrada</c>;
    /// 409 <c>revision-no-resoluble</c>, <c>revision-ya-resuelta</c>, <c>veredicto-no-vigente</c>, <c>estado-invalido</c>,
    /// <c>area-no-activa</c>; 422 <c>justificacion-requerida</c> al revertir; 403 de conflicto de intereses o separación de funciones.
    /// </summary>
    public IReadOnlyList<object> ResolverRevisionAutomatica(Guid requisitoId, Guid areaId, DecisionHumanaDeRevision decision, Revisor revisor,
        string? justificacion, DateTimeOffset ahora)
    {
        var requisito = BuscarRequisito(requisitoId)
                        ?? throw ReglaDeNegocioException.NoEncontrado("requisito-no-encontrado", "Ese requisito no está en el expediente.");
        var revision = requisito.Revisiones.Where(r => r.AreaId == areaId && r.Resultado == ResultadoDeRevision.AprobadaConMuestreo)
                           .OrderByDescending(r => r.Fecha).FirstOrDefault()
                       ?? throw ReglaDeNegocioException.NoEncontrado("revision-no-encontrada",
                           "Ese documento no tiene una aprobación por muestreo pendiente en el área.");
        if (revision.Resolucion is not null)
            throw ReglaDeNegocioException.Conflicto("revision-ya-resuelta", "Esa aprobación por muestreo ya se resolvió.");
        AsegurarRevisorHabilitado(revisor);

        var texto = Texto(justificacion, "justificacion");
        var por = revisor.UsuarioId.ToString();
        if (decision == DecisionHumanaDeRevision.Confirmada)
            return Simular(s => s.Emitir(new RevisionAutomaticaResuelta(requisitoId, areaId, revision.SobreVersion, decision, por, texto, ahora)), ahora);

        if (texto is null)
            throw ReglaDeNegocioException.Validacion("justificacion-requerida", "Revertir la aprobación automática necesita una justificación.");
        if (Estado != EstadoExpediente.EnRevision)
            throw ReglaDeNegocioException.Conflicto("estado-invalido",
                $"El expediente está {Estado}: ya no se revierte la aprobación; confírmala o desaprueba el expediente.");
        AreaActiva(areaId);
        var veredicto = requisito.Veredictos.FirstOrDefault(v => v.Id == revision.VeredictoId && v.Vigente)
                        ?? throw ReglaDeNegocioException.Conflicto("veredicto-no-vigente",
                            "El veredicto del sistema ya no está vigente (lo invalidó una versión nueva o se anuló).");
        return Simular(s =>
        {
            s.Emitir(new VeredictoAnuladoPorAdministrador(veredicto.Id, areaId, por, $"Revertido tras el muestreo: {texto}", ahora));
            s.Emitir(new RevisionAutomaticaResuelta(requisitoId, areaId, revision.SobreVersion, decision, por, texto, ahora));
        }, ahora);
    }

    /// <summary>
    /// ¿Esta revisión sigue pidiendo a una persona? Una excepción, mientras el expediente esté abierto, la versión siga siendo la
    /// misma y nadie haya decidido el documento en el área; un muestreo, mientras no se resuelva y su veredicto siga vigente.
    /// </summary>
    public bool RevisionAbierta(RevisionAutomaticaDeRequisito revision, RequisitoDeExpediente requisito)
    {
        if (revision.Resolucion is not null)
            return false;
        return revision.Resultado switch
        {
            ResultadoDeRevision.Excepcion => EstaAbierto && !requisito.Eximido && requisito.VersionActual?.Numero == revision.SobreVersion &&
                                             BuscarArea(revision.AreaId) is { } area && !requisito.DecididoPor(area),
            ResultadoDeRevision.AprobadaConMuestreo => requisito.Veredictos.Any(v => v.Id == revision.VeredictoId && v.Vigente),
            _ => false
        };
    }

    private static string JustificacionAutomatica(DecisionDeRevision decision)
    {
        var confianza = decision.Confianza is { } c ? $" (confianza {(c * 100m):0.#} %)" : string.Empty;
        return decision.Resultado == ResultadoDeRevision.AprobadaConMuestreo
            ? $"Aprobado por la revisión automática{confianza}, sin excepciones; una persona lo confirma por muestreo."
            : $"Aprobado por la revisión automática{confianza}, sin excepciones.";
    }

    // ---------- Evolución ----------

    public void Apply(RevisionAutomaticaRegistrada e)
    {
        BuscarRequisito(e.RequisitoId)!.Revisiones.Add(new RevisionAutomaticaDeRequisito
        {
            AreaId = e.AreaId,
            SobreVersion = e.SobreVersion,
            Nivel = e.Nivel,
            Resultado = e.Resultado,
            Sugerencia = e.Sugerencia,
            Motivos = [.. e.Motivos],
            Confianza = e.Confianza,
            VeredictoId = e.VeredictoId,
            Fecha = e.Fecha
        });
        RecalcularRevisionAutomatica();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(RevisionAutomaticaResuelta e)
    {
        var revision = BuscarRequisito(e.RequisitoId)!.Revisiones
            .Where(r => r.AreaId == e.AreaId && r.SobreVersion == e.SobreVersion && r.Resolucion is null).OrderByDescending(r => r.Fecha).First();
        revision.Resolucion = new ResolucionDeRevision(e.Decision, e.Por, e.Justificacion, e.Fecha);
        RecalcularRevisionAutomatica();
        ActualizadoEn = e.Fecha;
    }

    /// <summary>Campos planos que leen el procesador de la revisión automática y la bandeja de excepciones.</summary>
    private void RecalcularRevisionAutomatica()
    {
        ConRevisionAutomatica = Requisitos.Any(r => r.Revisiones.Any(v => v.Resultado is ResultadoDeRevision.Excepcion or ResultadoDeRevision.AprobadaConMuestreo));
        ExcepcionesDeRevisionPendientes = Requisitos.Sum(r => r.Revisiones.Count(v => RevisionAbierta(v, r)));
        PorRevisarAutomaticamente = CandidatasDeRevisionAutomatica().Count > 0;
    }
}
