using System.Text.Json;
using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Expedientes;

/// <summary>Persona que emite un veredicto: usuario de Validasoft y su identificación (para el conflicto de intereses).</summary>
public sealed record Revisor(Guid UsuarioId, string? ClaveIdentificacion);

/// <summary>Datos del vínculo y del tercero que el expediente copia al abrirse.</summary>
public sealed record DatosDeApertura(
    Guid EmpresaId,
    Guid VinculoId,
    TipoExpediente Tipo,
    string Folio,
    Guid TerceroId,
    string TerceroNombre,
    string TerceroIdentificacion,
    Guid TipoVinculoId,
    string TipoVinculoCodigo,
    string TipoVinculoNombre,
    ActorDeExpediente AbiertoPor,
    IReadOnlyList<string> InvitadoPor);

/// <summary>Versión de flujo resuelta (explícita o por defecto) con las áreas ya copiadas.</summary>
public sealed record FlujoParaExpediente(
    Guid FlujoId,
    Guid FlujoVersionId,
    string Codigo,
    string Nombre,
    int Version,
    OrigenFlujo Origen,
    IReadOnlyList<EtapaDelFlujo> Etapas,
    PoliticaDevolucion PoliticaDevolucion,
    bool SeparacionDeFunciones,
    IReadOnlyList<VerificacionRequeridaDelFlujo>? Verificaciones = null,
    PlazoDeCargue? PlazoCargue = null);

/// <summary>Copia del flujo que gobierna el expediente.</summary>
public sealed record FlujoDelExpediente(Guid FlujoId, Guid FlujoVersionId, string Codigo, string Nombre, int Version, OrigenFlujo Origen);

public sealed record RechazoDelExpediente(Guid AreaId, string RevisorId, IReadOnlyList<MotivoAplicado> Motivos, string Justificacion,
    DateTimeOffset Fecha, string? Origen = null);

/// <summary>
/// Veredicto guardado en el área (sobre el expediente) o en un requisito (con <see cref="AreaId"/> y
/// <see cref="SobreVersion"/>). <see cref="Vigente"/> pasa a falso cuando un administrador lo anula, cuando el tercero
/// reenvía tras una observación (<see cref="Superado"/>) o cuando llega una versión nueva del requisito
/// (<see cref="Invalidado"/>): queda en el historial, pero ya no cuenta.
/// </summary>
public sealed class VeredictoRegistrado
{
    public Guid Id { get; set; }

    /// <summary>Solo en veredictos de requisito: el requisito y el área que lo emitió.</summary>
    public Guid? RequisitoId { get; set; }

    public Guid? AreaId { get; set; }

    /// <summary>Número de la versión del documento sobre la que se decidió (veredictos de requisito).</summary>
    public int? SobreVersion { get; set; }

    /// <summary>Veredicto de área que el sistema derivó de los requisitos de su alcance (RQ-5).</summary>
    public bool Derivado { get; set; }

    public bool Invalidado { get; set; }

    public Veredicto Veredicto { get; set; }

    public OrigenVeredicto Origen { get; set; }

    public string? RevisorId { get; set; }

    public List<MotivoAplicado> Motivos { get; set; } = [];

    /// <summary>Campos de la sección señalados al observarla (F2); vacío en documentos.</summary>
    public List<string> Campos { get; set; } = [];

    public string? Observacion { get; set; }

    public string? Justificacion { get; set; }

    public DateTimeOffset Fecha { get; set; }

    public bool Vigente { get; set; } = true;

    public bool Superado { get; set; }

    public bool Anulado { get; set; }

    /// <summary>Un administrador devolvió a revisión la etapa de esta área (reapertura, fase inicial o desaprobación, DS-1): ya no cuenta.</summary>
    public bool Revertido { get; set; }

    public string? AnuladoPor { get; set; }

    public string? JustificacionAnulacion { get; set; }
}

public sealed class AreaDeExpediente
{
    public Guid AreaId { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public int AprobadoresRequeridos { get; set; } = 1;

    public bool PuedeRechazarExpediente { get; set; }

    public bool Automatica { get; set; }

    public int? SlaHoras { get; set; }

    public EstadoAreaExpediente Estado { get; set; }

    /// <summary>Desde cuándo espera el veredicto de esta área (activación o reapertura).</summary>
    public DateTimeOffset? PendienteDesde { get; set; }

    public DateTimeOffset? SlaVence { get; set; }

    public List<VeredictoRegistrado> Veredictos { get; set; } = [];

    /// <summary>Con requisitos: cuántos de su alcance esperan todavía su decisión (para la bandeja).</summary>
    public int RequisitosPorDecidir { get; set; }

    /// <summary>Usuarios distintos con aprobación humana vigente.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> AprobadoresVigentes =>
    [
        .. Veredictos.Where(v => v.Vigente && v.Veredicto == Veredicto.Aprobado && v.Origen == OrigenVeredicto.Humano && v.RevisorId is not null)
            .Select(v => v.RevisorId!).Distinct()
    ];

    /// <summary>Aprobaciones que cuentan (x de <see cref="AprobadoresRequeridos"/>); una aprobación del sistema o del administrador completa el área.</summary>
    [JsonIgnore]
    public int Aprobaciones => Estado == EstadoAreaExpediente.Aprobada ? AprobadoresRequeridos : AprobadoresVigentes.Count;

    /// <summary>Quién ya emitió un veredicto humano vigente en esta ronda.</summary>
    [JsonIgnore]
    public IReadOnlyList<string> RevisoresVigentes =>
        [.. Veredictos.Where(v => v.Vigente && v.Origen == OrigenVeredicto.Humano && v.RevisorId is not null).Select(v => v.RevisorId!).Distinct()];
}

public sealed class EtapaDeExpediente
{
    public int Orden { get; set; }

    public EstadoEtapa Estado { get; set; }

    public DateTimeOffset? ActivadaEn { get; set; }

    public DateTimeOffset? AprobadaEn { get; set; }

    public List<AreaDeExpediente> Areas { get; set; } = [];
}

/// <summary>
/// Instancia de una versión de flujo para un vínculo (docs/03 §6) — agregado event-sourced de vida corta. Las áreas de
/// la etapa activa revisan en paralelo. Sin requisitos (FL-1) cada área se pronuncia sobre el expediente completo y
/// necesita <c>AprobadoresRequeridos</c> usuarios distintos; con requisitos (RQ-*) cada área aprueba u observa los
/// documentos de su alcance —uno a uno o todos en una decisión de área (DA-*)— y su aprobación se deriva de ellos (ver
/// <c>ExpedienteRequisitos.cs</c> y <c>ExpedienteDecisionDeArea.cs</c>). Una observación
/// frena todo (política <c>Inmediata</c>) o al cerrar la etapa (<c>AlCerrarArea</c>); al reenviar, solo lo observado
/// vuelve a revisión. Los métodos de decisión devuelven los eventos (incluida la cascada: paso a revisión, etapa
/// aprobada, siguiente etapa, aprobaciones automáticas, cierre); los <c>Apply</c> solo evolucionan el estado.
/// </summary>
public sealed partial class Expediente
{
    private const int LongitudMaximaTexto = 4000;

    private static readonly EstadoExpediente[] Abiertos = [EstadoExpediente.Abierto, EstadoExpediente.EnRevision, EstadoExpediente.Observado];

    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid VinculoId { get; set; }

    public TipoExpediente Tipo { get; set; }

    public string Folio { get; set; } = string.Empty;

    public EstadoExpediente Estado { get; set; }

    public Guid TerceroId { get; set; }

    public string TerceroNombre { get; set; } = string.Empty;

    /// <summary>Clave de identificación del tercero (<c>CO|NIT|900123456</c>).</summary>
    public string TerceroIdentificacion { get; set; } = string.Empty;

    public Guid TipoVinculoId { get; set; }

    public string TipoVinculoCodigo { get; set; } = string.Empty;

    public string TipoVinculoNombre { get; set; } = string.Empty;

    public ActorDeExpediente AbiertoPor { get; set; } = new(ActorTipo.Sistema, "sistema");

    /// <summary>Usuarios que crearon el vínculo o enviaron la invitación (separación de funciones).</summary>
    public List<string> InvitadoPor { get; set; } = [];

    public FlujoDelExpediente? Flujo { get; set; }

    public PoliticaDevolucion PoliticaDevolucion { get; set; }

    public bool SeparacionDeFunciones { get; set; }

    public List<EtapaDeExpediente> Etapas { get; set; } = [];

    /// <summary>Documentos que pide el flujo (vacío ⇒ veredictos sobre el expediente completo, FL-1).</summary>
    public List<RequisitoDeExpediente> Requisitos { get; set; } = [];

    /// <summary>Requisitos observados en la devolución en curso (los que vuelven a revisión al reenviar).</summary>
    public List<Guid> RequisitosObservados { get; set; } = [];

    /// <summary>Copia plana de las áreas del flujo para filtrar en Marten.</summary>
    public List<Guid> AreaIds { get; set; } = [];

    public int Reenvios { get; set; }

    public string? UltimaRespuesta { get; set; }

    public RechazoDelExpediente? Rechazo { get; set; }

    public string? MotivoAnulacion { get; set; }

    public DateTimeOffset AbiertoEn { get; set; }

    public DateTimeOffset? ObservadoEn { get; set; }

    public DateTimeOffset? CerradoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    /// <summary>Plazo de cargue copiado del flujo (solo expedientes <c>Inicial</c>, LT-1); nulo = sin plazo.</summary>
    public PlazoDeCargue? PlazoCargue { get; set; }

    /// <summary>Hasta cuándo tiene el tercero para completar lo que está en sus manos (<c>Abierto</c> u <c>Observado</c>).</summary>
    public DateTimeOffset? PlazoCargueHasta { get; set; }

    /// <summary>Recordatorios (días antes) ya cumplidos en el plazo en curso.</summary>
    public List<int> RecordatoriosEnviados { get; set; } = [];

    /// <summary>Próximo momento en que el job de plazos debe mirar este expediente (recordatorio o caducidad); índice del barrido.</summary>
    public DateTimeOffset? ProximaRevisionDePlazo { get; set; }

    /// <summary>Estado en que caducó (<c>Abierto</c> u <c>Observado</c>): al reabrir vuelve a él.</summary>
    public EstadoExpediente? EstadoAlCaducar { get; set; }

    public DateTimeOffset? CaducadoEn { get; set; }

    public int Reaperturas { get; set; }

    [JsonIgnore]
    public EtapaDeExpediente? EtapaActiva => Etapas.FirstOrDefault(e => e.Estado == EstadoEtapa.Activa);

    [JsonIgnore]
    public bool EstaAbierto => Abiertos.Contains(Estado);

    /// <summary>Con requisitos, cada área decide por documento (uno a uno o en una decisión de área, DA-1) y su aprobación se deriva (RQ-4).</summary>
    [JsonIgnore]
    public bool ConRequisitos => Requisitos.Count > 0;

    /// <summary>
    /// Observaciones que el tercero debe atender (solo mientras el expediente está devuelto): las de las áreas y las de los
    /// requisitos (<see cref="VeredictoRegistrado.RequisitoId"/>) aún vigentes; una versión nueva del requisito las invalida.
    /// </summary>
    [JsonIgnore]
    public IReadOnlyList<VeredictoRegistrado> ObservacionesVigentes =>
        Estado != EstadoExpediente.Observado || EtapaActiva is null
            ? []
            : [.. EtapaActiva.Areas.SelectMany(a => a.Veredictos).Concat(Requisitos.SelectMany(r => r.Veredictos))
                .Where(v => v.Vigente && v.Veredicto == Veredicto.Observado).OrderBy(v => v.Fecha)];

    public AreaDeExpediente? BuscarArea(Guid areaId) => Etapas.SelectMany(e => e.Areas).FirstOrDefault(a => a.AreaId == areaId);

    // ---------- Decisiones ----------

    /// <summary>
    /// Abre el expediente con la versión de flujo copiada. Sin requisitos pasa directo a revisión (FL-1): activa la
    /// primera etapa (todas sus áreas a la vez) y las áreas automáticas aprueban de inmediato; si todo es automático, el
    /// expediente queda aprobado en la misma transacción. Con <paramref name="requisitos"/> emite
    /// <c>RequisitosCalculados</c> (los que el tercero ya tenía compartidos nacen <c>Cargado</c>) y queda <c>Abierto</c>
    /// en fase de carga hasta que todos los obligatorios estén cargados o eximidos.
    /// </summary>
    public static IReadOnlyList<object> Abrir(DatosDeApertura datos, FlujoParaExpediente flujo, DateTimeOffset ahora,
        IReadOnlyList<RequisitoCalculado>? requisitos = null)
    {
        if (string.IsNullOrWhiteSpace(datos.Folio))
            throw ReglaDeNegocioException.Validacion("folio-requerido", "El expediente necesita folio.");
        if (flujo.Etapas.Count == 0 || flujo.Etapas.Any(e => e.Areas.Count == 0))
            throw ReglaDeNegocioException.Validacion("flujo-sin-areas", "La versión del flujo no tiene áreas que revisen.");

        var simulacion = new Simulacion(null);
        simulacion.Emitir(new ExpedienteAbierto(Guid.CreateVersion7(), datos.EmpresaId, datos.VinculoId, datos.Tipo, datos.Folio,
            datos.TerceroId, datos.TerceroNombre, datos.TerceroIdentificacion, datos.TipoVinculoId, datos.TipoVinculoCodigo,
            datos.TipoVinculoNombre, datos.AbiertoPor, [.. datos.InvitadoPor.Distinct()], ahora));
        // El plazo de cargue solo corre en el expediente inicial (LT-1): renovaciones y actualizaciones tienen los suyos.
        var plazo = datos.Tipo == TipoExpediente.Inicial && flujo.PlazoCargue is { Activo: true } activo ? activo : null;
        simulacion.Emitir(new FlujoSeleccionado(flujo.FlujoId, flujo.FlujoVersionId, flujo.Codigo, flujo.Nombre, flujo.Version, flujo.Origen,
            [.. flujo.Etapas.OrderBy(e => e.Orden)], flujo.PoliticaDevolucion, flujo.SeparacionDeFunciones, ahora, plazo));
        if (requisitos is { Count: > 0 })
            simulacion.Emitir(new RequisitosCalculados(ResolverAlcance(requisitos, flujo), ahora));
        if (flujo.Verificaciones is { Count: > 0 } verificaciones)
            simulacion.Emitir(new VerificacionesRequeridas([.. verificaciones], ahora));
        Evaluar(simulacion, ahora);
        return simulacion.Eventos;
    }

    /// <summary>
    /// Veredicto humano de un miembro del área sobre el expediente completo (FL-1; la pertenencia la verifica la
    /// aplicación). Con requisitos la decisión del área se toma por sus documentos (<see cref="AprobarArea"/> u
    /// <see cref="ObservarArea"/>): sin versiones ni documentos marcados responde 422 <c>versiones-requeridas</c> o
    /// <c>requisitos-requeridos</c>.
    /// </summary>
    public IReadOnlyList<object> EmitirVeredicto(Guid areaId, Revisor revisor, Veredicto veredicto, IReadOnlyList<MotivoAplicado> motivos,
        string? observacion, DateTimeOffset ahora)
    {
        if (ConRequisitos)
            return veredicto == Veredicto.Aprobado ? AprobarArea(areaId, revisor, null, ahora) : ObservarArea(areaId, revisor, null, ahora);
        AsegurarEnRevision();
        var area = AreaActiva(areaId);
        if (area.Automatica)
            throw ReglaDeNegocioException.Conflicto("area-automatica", $"El área {area.Codigo} aprueba automáticamente.");
        AsegurarRevisorHabilitado(revisor);

        var revisorId = revisor.UsuarioId.ToString();
        if (area.RevisoresVigentes.Contains(revisorId))
            throw ReglaDeNegocioException.Conflicto("aprobador-repetido",
                $"Ya emitiste tu veredicto en el área {area.Codigo}; otra persona del área debe completar las aprobaciones.");
        if (area.Estado != EstadoAreaExpediente.Pendiente)
            throw ReglaDeNegocioException.Conflicto("area-ya-se-pronuncio", $"El área {area.Codigo} ya está {area.Estado} en esta ronda.");

        var lista = Motivos(veredicto, motivos);
        return Simular(s => s.Emitir(new VeredictoDeAreaEmitido(Guid.CreateVersion7(), areaId, veredicto, OrigenVeredicto.Humano, revisorId,
            [.. lista.DistinctBy(m => m.Codigo)], Texto(observacion, "observacion"), null, null, ahora)), ahora);
    }

    /// <summary>Rechazo terminal por un área con <c>PuedeRechazarExpediente</c> (B-7, P-5-5).</summary>
    public IReadOnlyList<object> Rechazar(Guid areaId, Revisor revisor, IReadOnlyList<MotivoAplicado> motivos, string? justificacion,
        DateTimeOffset ahora)
    {
        // También mientras está devuelto al tercero (FL-14): un hallazgo grave no espera la corrección.
        if (Estado is not (EstadoExpediente.EnRevision or EstadoExpediente.Observado))
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"El expediente está {Estado}.");
        // Con todas las áreas aprobadas y una verificación sin satisfacer (p. ej. una coincidencia confirmada, f-4), el
        // expediente espera: un área que puede rechazar lo cierra aunque su etapa ya no esté activa.
        var area = EsperandoVerificaciones
            ? BuscarArea(areaId) ?? throw ReglaDeNegocioException.NoEncontrado("area-no-encontrada", "Esa área no participa en el flujo del expediente.")
            : AreaActiva(areaId);
        if (!area.PuedeRechazarExpediente)
            throw ReglaDeNegocioException.Prohibido("area-no-puede-rechazar", $"El área {area.Codigo} no puede rechazar expedientes.");
        AsegurarRevisorHabilitado(revisor);

        if (motivos is null || motivos.Count == 0)
            throw ReglaDeNegocioException.Validacion("motivo-requerido", "Para rechazar indica al menos un motivo.");

        return [new ExpedienteRechazado(areaId, revisor.UsuarioId.ToString(), [.. motivos.DistinctBy(m => m.Codigo)],
            Requerido(justificacion, "justificacion-requerida", "El rechazo necesita una justificación."), ahora)];
    }

    /// <summary>
    /// Respuesta del tercero (o del integrador) a la observación: solo lo observado vuelve a revisar. Con requisitos,
    /// cada requisito observado necesita una versión nueva (que ya invalidó la observación) o, si no, una respuesta
    /// escrita (409 <c>requisitos-sin-corregir</c>); solo esos requisitos vuelven a revisión (RQ-7).
    /// </summary>
    public IReadOnlyList<object> Reenviar(OrigenReenvio origen, string? respuesta, DateTimeOffset ahora)
    {
        if (Estado != EstadoExpediente.Observado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo se reenvía un expediente observado; este está {Estado}.");

        var texto = Texto(respuesta, "respuesta");
        var reabiertas = EtapaActiva?.Areas.Where(a => a.Estado == EstadoAreaExpediente.Observada).Select(a => a.AreaId).ToList() ?? [];
        if (!ConRequisitos)
            return Simular(s => s.Emitir(new ExpedienteReenviado(origen, texto, reabiertas, ahora)), ahora);

        var observados = RequisitosObservados.Select(BuscarRequisito).OfType<RequisitoDeExpediente>().ToList();
        var sinCorregir = observados.Where(r => !r.Eximido && r.Veredictos.Any(v => v.Vigente && v.Veredicto == Veredicto.Observado)).ToList();
        if (sinCorregir.Count > 0 && texto is null)
            throw ReglaDeNegocioException.Conflicto("requisitos-sin-corregir",
                $"Carga una versión nueva de {string.Join(", ", sinCorregir.Select(r => r.Nombre))} o responde a la observación.");

        return Simular(s => s.Emitir(new ExpedienteReenviado(origen, texto, reabiertas, ahora, [.. observados.Select(r => r.Id)])), ahora);
    }

    /// <summary>Un administrador aprueba el área en su nombre (origen <c>Administrador</c>), siempre con justificación.</summary>
    public IReadOnlyList<object> AprobarComoAdministrador(Guid areaId, string administradorId, string? justificacion, DateTimeOffset ahora)
    {
        AsegurarEnRevision();
        var area = AreaActiva(areaId);
        if (area.Estado != EstadoAreaExpediente.Pendiente)
            throw ReglaDeNegocioException.Conflicto("area-ya-se-pronuncio",
                $"El área {area.Codigo} está {area.Estado}; si observó, anula primero la observación.");

        var texto = Requerido(justificacion, "justificacion-requerida", "Aprobar en nombre del área necesita una justificación.");
        if (ConRequisitos)
            return AprobarRequisitosComoAdministrador(area, administradorId, texto, ahora);

        return Simular(s => s.Emitir(new VeredictoDeAreaEmitido(Guid.CreateVersion7(), areaId, Veredicto.Aprobado, OrigenVeredicto.Administrador,
            administradorId, [], null, texto, null, ahora)), ahora);
    }

    /// <summary>
    /// Deja sin efecto un veredicto vigente del área (el indicado o el más reciente). No revierte un rechazo ni toca etapas
    /// ya aprobadas. Si era la última observación vigente de un expediente devuelto, la revisión se reanuda sin esperar
    /// un reenvío (FL-13).
    /// </summary>
    public IReadOnlyList<object> AnularVeredicto(Guid areaId, Guid? veredictoId, string administradorId, string? justificacion,
        DateTimeOffset ahora)
    {
        if (Estado is not (EstadoExpediente.EnRevision or EstadoExpediente.Observado))
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"No se anulan veredictos de un expediente {Estado}.");

        var area = AreaActiva(areaId);
        // Los del área sobre el expediente y los de sus requisitos; nunca el veredicto de área derivado (RQ-5).
        var vigentes = area.Veredictos.Concat(Requisitos.SelectMany(r => r.Veredictos).Where(v => v.AreaId == areaId))
            .Where(v => v.Vigente && !v.Derivado)
            .OrderBy(v => v.Fecha)
            .ToList();
        var veredicto = (veredictoId is null ? vigentes.LastOrDefault() : vigentes.FirstOrDefault(v => v.Id == veredictoId))
                        ?? throw ReglaDeNegocioException.NoEncontrado("veredicto-no-encontrado", "No hay un veredicto vigente con ese id en el área.");

        var texto = Requerido(justificacion, "justificacion-requerida", "Anular un veredicto necesita una justificación.");
        return Simular(s =>
        {
            s.Emitir(new VeredictoAnuladoPorAdministrador(veredicto.Id, areaId, administradorId, texto, ahora));
            if (s.Estado is { Estado: EstadoExpediente.Observado, EtapaActiva: { } etapa } &&
                etapa.Areas.All(a => a.Estado != EstadoAreaExpediente.Observada))
                s.Emitir(new RevisionReanudada(veredicto.Id, ahora));
        }, ahora);
    }

    public ExpedienteAnulado Anular(string? motivo, DateTimeOffset ahora)
    {
        if (!EstaAbierto)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"No se anula un expediente {Estado}.");
        return new ExpedienteAnulado(Estado, Requerido(motivo, "motivo-requerido", "La anulación necesita un motivo."), ahora);
    }

    /// <summary>Estado resultante de aplicar <paramref name="eventos"/> a una copia de <paramref name="actual"/> (o desde cero).</summary>
    public static Expediente Reproducir(Expediente? actual, IEnumerable<object> eventos)
    {
        var simulacion = new Simulacion(actual);
        foreach (var evento in eventos)
            simulacion.Emitir(evento);
        return simulacion.Estado ?? throw new InvalidOperationException("No hay eventos que reproducir.");
    }

    // ---------- Evolución (Marten) ----------

    public static Expediente Create(ExpedienteAbierto e) => new()
    {
        Id = e.ExpedienteId,
        EmpresaId = e.EmpresaId,
        VinculoId = e.VinculoId,
        Tipo = e.Tipo,
        Folio = e.Folio,
        Estado = EstadoExpediente.Abierto,
        TerceroId = e.TerceroId,
        TerceroNombre = e.TerceroNombre,
        TerceroIdentificacion = e.TerceroIdentificacion,
        TipoVinculoId = e.TipoVinculoId,
        TipoVinculoCodigo = e.TipoVinculoCodigo,
        TipoVinculoNombre = e.TipoVinculoNombre,
        AbiertoPor = e.AbiertoPor,
        InvitadoPor = [.. e.InvitadoPor],
        AbiertoEn = e.Fecha,
        ActualizadoEn = e.Fecha
    };

    public void Apply(FlujoSeleccionado e)
    {
        Flujo = new FlujoDelExpediente(e.FlujoId, e.FlujoVersionId, e.Codigo, e.Nombre, e.Version, e.Origen);
        PoliticaDevolucion = e.PoliticaDevolucion;
        SeparacionDeFunciones = e.SeparacionDeFunciones;
        Etapas = [.. e.Etapas.OrderBy(x => x.Orden).Select(x => new EtapaDeExpediente
        {
            Orden = x.Orden,
            Estado = EstadoEtapa.Esperando,
            Areas = [.. x.Areas.Select(a => new AreaDeExpediente
            {
                AreaId = a.AreaId,
                Codigo = a.Codigo,
                Nombre = a.Nombre,
                AprobadoresRequeridos = a.AprobadoresRequeridos,
                PuedeRechazarExpediente = a.PuedeRechazarExpediente,
                Automatica = a.Automatica,
                SlaHoras = a.SlaHoras,
                Estado = EstadoAreaExpediente.Esperando
            })]
        })];
        AreaIds = [.. Etapas.SelectMany(x => x.Areas).Select(a => a.AreaId)];
        PlazoCargue = e.PlazoCargue;
        if (Estado == EstadoExpediente.Abierto)
            IniciarPlazo(AbiertoEn);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExpedienteEnviadoARevision e)
    {
        Estado = EstadoExpediente.EnRevision;
        TerminarPlazo();
        RecalcularRequisitos(e.Fecha);
        ActualizadoEn = e.Fecha;
        RecalcularVerificaciones(); // las listas se consultan al pasar a revisión (con la ficha ya diligenciada)
    }

    public void Apply(EtapaActivada e)
    {
        var etapa = Etapas.First(x => x.Orden == e.Orden);
        etapa.Estado = EstadoEtapa.Activa;
        etapa.ActivadaEn = e.Fecha;
        foreach (var area in etapa.Areas)
        {
            area.Estado = EstadoAreaExpediente.Pendiente;
            area.PendienteDesde = e.Fecha;
            area.SlaVence = area.SlaHoras is { } horas ? e.Fecha.AddHours(horas) : null;
        }

        RecalcularRequisitos(e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VeredictoDeAreaEmitido e)
    {
        var area = BuscarArea(e.AreaId)!;
        area.Veredictos.Add(new VeredictoRegistrado
        {
            Id = e.VeredictoId,
            Veredicto = e.Veredicto,
            Origen = e.Origen,
            RevisorId = e.RevisorId,
            Motivos = [.. e.Motivos],
            Observacion = e.Observacion,
            Justificacion = e.Justificacion,
            Fecha = e.Fecha,
            Derivado = e.DerivadoDeRequisitos
        });
        Recalcular(area, e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExpedienteObservado e)
    {
        Estado = EstadoExpediente.Observado;
        IniciarPlazo(e.Fecha);
        RequisitosObservados = [.. e.RequisitosObservados ?? []];
        RecalcularRequisitos(e.Fecha);
        ObservadoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExpedienteReenviado e)
    {
        foreach (var area in e.AreasReabiertas.Select(BuscarArea).OfType<AreaDeExpediente>())
        {
            foreach (var veredicto in area.Veredictos.Where(v => v.Vigente))
            {
                veredicto.Vigente = false;
                veredicto.Superado = true;
            }

            Recalcular(area, e.Fecha);
            area.PendienteDesde = e.Fecha;
        }

        foreach (var requisito in (e.RequisitosReabiertos ?? []).Select(BuscarRequisito).OfType<RequisitoDeExpediente>())
        {
            foreach (var veredicto in requisito.Veredictos.Where(v => v.Vigente))
            {
                veredicto.Vigente = false;
                veredicto.Superado = true;
            }
        }

        Estado = EstadoExpediente.EnRevision;
        TerminarPlazo();
        RequisitosObservados = [];
        Reenvios++;
        UltimaRespuesta = e.Respuesta;
        RecalcularRequisitos(e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(RevisionReanudada e)
    {
        Estado = EstadoExpediente.EnRevision;
        TerminarPlazo();
        RequisitosObservados = [];
        RecalcularRequisitos(e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(EtapaAprobada e)
    {
        var etapa = Etapas.First(x => x.Orden == e.Orden);
        etapa.Estado = EstadoEtapa.Aprobada;
        etapa.AprobadaEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExpedienteAprobado e)
    {
        Estado = EstadoExpediente.Aprobado;
        TerminarPlazo();
        CerradoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
        RecalcularVerificaciones();
        RecalcularRevisionAutomatica();
    }

    public void Apply(ExpedienteRechazado e)
    {
        Estado = EstadoExpediente.Rechazado;
        TerminarPlazo();
        Rechazo = new RechazoDelExpediente(e.AreaId, e.RevisorId, e.Motivos, e.Justificacion, e.Fecha, e.Origen);
        CerradoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
        RecalcularVerificaciones();
        RecalcularRevisionAutomatica();
    }

    public void Apply(ExpedienteAnulado e)
    {
        Estado = EstadoExpediente.Anulado;
        TerminarPlazo();
        MotivoAnulacion = e.Motivo;
        CerradoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
        RecalcularVerificaciones();
        RecalcularRevisionAutomatica();
    }

    public void Apply(VeredictoAnuladoPorAdministrador e)
    {
        var area = BuscarArea(e.AreaId)!;
        var veredicto = area.Veredictos.Concat(Requisitos.SelectMany(r => r.Veredictos)).First(v => v.Id == e.VeredictoId);
        veredicto.Vigente = false;
        veredicto.Anulado = true;
        veredicto.AnuladoPor = e.AdministradorId;
        veredicto.JustificacionAnulacion = e.Justificacion;
        Recalcular(area, e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    // ---------- Reglas internas ----------

    /// <summary>El estado del área sale de sus veredictos vigentes: una observación manda; luego aprobación completa.</summary>
    private void Recalcular(AreaDeExpediente area, DateTimeOffset fecha)
    {
        if (ConRequisitos)
        {
            RecalcularRequisitos(fecha);
            return;
        }

        var previo = area.Estado;
        var vigentes = area.Veredictos.Where(v => v.Vigente).ToList();
        var etapaActiva = Etapas.First(e => e.Areas.Contains(area)).Estado != EstadoEtapa.Esperando;

        area.Estado = vigentes.Any(v => v.Veredicto == Veredicto.Observado) ? EstadoAreaExpediente.Observada
            : vigentes.Any(v => v.Veredicto == Veredicto.Aprobado && v.Origen != OrigenVeredicto.Humano) ? EstadoAreaExpediente.Aprobada
            : area.AprobadoresVigentes.Count >= area.AprobadoresRequeridos ? EstadoAreaExpediente.Aprobada
            : etapaActiva ? EstadoAreaExpediente.Pendiente
            : EstadoAreaExpediente.Esperando;

        if (area.Estado == EstadoAreaExpediente.Pendiente && previo != EstadoAreaExpediente.Pendiente)
            area.PendienteDesde = fecha;
    }

    private void AsegurarEnRevision()
    {
        if (Estado == EstadoExpediente.Observado)
            throw ReglaDeNegocioException.Conflicto("expediente-observado",
                "El expediente está devuelto al tercero por una observación; nadie emite veredictos hasta que lo reenvíe.");
        if (Estado != EstadoExpediente.EnRevision)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"El expediente está {Estado}.");
    }

    private AreaDeExpediente AreaActiva(Guid areaId)
    {
        var area = BuscarArea(areaId)
                   ?? throw ReglaDeNegocioException.NoEncontrado("area-no-encontrada", "Esa área no participa en el flujo del expediente.");
        if (EtapaActiva is not { } etapa || !etapa.Areas.Contains(area))
            throw ReglaDeNegocioException.Conflicto("area-no-activa", $"La etapa del área {area.Codigo} no está activa.");
        return area;
    }

    /// <summary>Conflicto de intereses (I-21) y separación de funciones (P-4-8): ambos impiden decidir.</summary>
    private void AsegurarRevisorHabilitado(Revisor revisor)
    {
        if (!string.IsNullOrWhiteSpace(revisor.ClaveIdentificacion) && revisor.ClaveIdentificacion == TerceroIdentificacion)
            throw ReglaDeNegocioException.Prohibido("conflicto-de-intereses",
                "Tu identificación coincide con la del tercero: no puedes decidir sobre este expediente.");

        var id = revisor.UsuarioId.ToString();
        if (SeparacionDeFunciones && ((AbiertoPor.Tipo == ActorTipo.Usuario && AbiertoPor.Id == id) || InvitadoPor.Contains(id)))
            throw ReglaDeNegocioException.Prohibido("separacion-de-funciones",
                "El flujo exige separación de funciones: quien invitó o abrió el expediente no puede decidir sobre él.");
    }

    private IReadOnlyList<object> Simular(Action<Simulacion> accion, DateTimeOffset ahora)
    {
        var simulacion = new Simulacion(this);
        accion(simulacion);
        Evaluar(simulacion, ahora);
        return simulacion.Eventos;
    }

    /// <summary>
    /// Cascada tras cada cambio: pasar a revisión al terminar la carga, aprobaciones automáticas y derivadas de los
    /// requisitos, devolver al tercero, aprobar la etapa, activar la siguiente o aprobar el expediente.
    /// </summary>
    private static void Evaluar(Simulacion simulacion, DateTimeOffset ahora)
    {
        if (simulacion.Estado is { Estado: EstadoExpediente.Abierto } abierto && abierto.ListoParaRevision)
        {
            simulacion.Emitir(new ExpedienteEnviadoARevision(ahora));
            Activar(simulacion, simulacion.Estado!.Etapas.OrderBy(e => e.Orden).First(), ahora);
        }

        // Todas las áreas ya aprobaron y solo faltaba una verificación (VE-5): se cierra en cuanto se satisface.
        if (simulacion.Estado is { EsperandoVerificaciones: true })
        {
            Cerrar(simulacion, ahora);
            return;
        }

        while (simulacion.Estado is { Estado: EstadoExpediente.EnRevision, EtapaActiva: { } etapa } expediente)
        {
            if (expediente.ConRequisitos && AprobarAutomaticasYDerivadas(simulacion, etapa, ahora))
                continue;

            var observadas = etapa.Areas.Where(a => a.Estado == EstadoAreaExpediente.Observada).Select(a => a.AreaId).ToList();
            if (observadas.Count > 0)
            {
                var todasSePronunciaron = etapa.Areas.All(a =>
                    (a.Estado is EstadoAreaExpediente.Aprobada or EstadoAreaExpediente.Observada) && a.RequisitosPorDecidir == 0);
                if (expediente.PoliticaDevolucion == PoliticaDevolucion.Inmediata || todasSePronunciaron)
                    simulacion.Emitir(new ExpedienteObservado(observadas, ahora,
                        expediente.ConRequisitos ? [.. expediente.RequisitosConObservacion().Select(r => r.Id)] : null));
                return;
            }

            if (etapa.Areas.Any(a => a.Estado != EstadoAreaExpediente.Aprobada))
                return;

            simulacion.Emitir(new EtapaAprobada(etapa.Orden, ahora));
            var siguiente = expediente.Etapas.Where(e => e.Orden > etapa.Orden).MinBy(e => e.Orden);
            if (siguiente is null)
            {
                Cerrar(simulacion, ahora);
                return;
            }

            Activar(simulacion, siguiente, ahora);
        }
    }

    /// <summary>
    /// Aprueba el expediente si todas las verificaciones bloqueantes están satisfechas y vigentes (docs/03 §6.3
    /// <c>EvaluarCierre</c>); si no, queda <c>EnRevision</c> con todas sus etapas aprobadas esperándolas.
    /// </summary>
    private static void Cerrar(Simulacion simulacion, DateTimeOffset ahora)
    {
        if (!simulacion.Estado!.VerificacionesSatisfechas(ahora))
            return;
        simulacion.Emitir(new ExpedienteAprobado(ahora));
        AceptarSecciones(simulacion, ahora);
    }

    /// <summary>
    /// Activa la etapa (todas sus áreas a la vez). Sin requisitos, sus áreas automáticas aprueban aquí mismo; con
    /// requisitos lo hacen requisito por requisito en la cascada (<see cref="AprobarAutomaticasYDerivadas"/>).
    /// </summary>
    private static void Activar(Simulacion simulacion, EtapaDeExpediente etapa, DateTimeOffset ahora)
    {
        List<Guid> automaticas = simulacion.Estado!.ConRequisitos ? [] : [.. etapa.Areas.Where(a => a.Automatica).Select(a => a.AreaId)];
        simulacion.Emitir(new EtapaActivada(etapa.Orden, [.. etapa.Areas.Select(a => a.AreaId)], ahora));
        foreach (var areaId in automaticas)
            simulacion.Emitir(new VeredictoDeAreaEmitido(Guid.CreateVersion7(), areaId, Veredicto.Aprobado, OrigenVeredicto.Sistema, null, [],
                null, null, null, ahora));
    }

    private void Aplicar(object evento)
    {
        switch (evento)
        {
            case FlujoSeleccionado e: Apply(e); break;
            case RequisitosCalculados e: Apply(e); break;
            case RequisitosAgregados e: Apply(e); break;
            case VersionAdjuntadaARequisito e: Apply(e); break;
            case VeredictosInvalidadosPorNuevaVersion e: Apply(e); break;
            case VeredictoDeRequisitoEmitido e: Apply(e); break;
            case RequisitoEximido e: Apply(e); break;
            case RequisitoRatificado e: Apply(e); break;
            case CompletitudDeSeccionEvaluada e: Apply(e); break;
            case SeccionAceptadaPorEmpresa e: Apply(e); break;
            case ExpedienteEnviadoARevision e: Apply(e); break;
            case EtapaActivada e: Apply(e); break;
            case VeredictoDeAreaEmitido e: Apply(e); break;
            case ExpedienteObservado e: Apply(e); break;
            case ExpedienteReenviado e: Apply(e); break;
            case RevisionReanudada e: Apply(e); break;
            case EtapaAprobada e: Apply(e); break;
            case ExpedienteAprobado e: Apply(e); break;
            case ExpedienteRechazado e: Apply(e); break;
            case ExpedienteAnulado e: Apply(e); break;
            case VeredictoAnuladoPorAdministrador e: Apply(e); break;
            case RevisionAutomaticaRegistrada e: Apply(e); break;
            case RevisionAutomaticaResuelta e: Apply(e); break;
            case VerificacionesRequeridas e: Apply(e); break;
            case VerificacionExternaRegistrada e: Apply(e); break;
            case CoincidenciaResuelta e: Apply(e); break;
            case EstudioDeCumplimientoRegistrado e: Apply(e); break;
            case VerificacionExpirada e: Apply(e); break;
            case AdjuntoDeCumplimientoSubido e: Apply(e); break;
            case ConsultaDeVerificacionFallida e: Apply(e); break;
            case EvidenciaDeVerificacionDescargada e: Apply(e); break;
            case RecordatorioDeCargueEnviado e: Apply(e); break;
            case ExpedienteCaducado e: Apply(e); break;
            case ExpedienteReabierto e: Apply(e); break;
            case ExpedienteDesaprobado e: Apply(e); break;
            case EtapaReabierta e: Apply(e); break;
            case RecordatorioDeObservacionesEnviado e: Apply(e); break;
            default: throw new InvalidOperationException($"Evento desconocido para el expediente: {evento.GetType().Name}.");
        }
    }

    private Expediente Copia() => JsonSerializer.Deserialize<Expediente>(JsonSerializer.Serialize(this))!;

    private static string? Texto(string? valor, string campo)
    {
        if (string.IsNullOrWhiteSpace(valor))
            return null;
        var texto = valor.Trim();
        return texto.Length <= LongitudMaximaTexto
            ? texto
            : throw ReglaDeNegocioException.Validacion("texto-demasiado-largo", $"El campo {campo} admite hasta {LongitudMaximaTexto} caracteres.");
    }

    private static string Requerido(string? valor, string codigo, string mensaje) =>
        Texto(valor, codigo) ?? throw ReglaDeNegocioException.Validacion(codigo, mensaje);

    /// <summary>Copia de trabajo: aplica cada evento emitido para que la cascada decida sobre el estado ya actualizado.</summary>
    private sealed class Simulacion(Expediente? inicial)
    {
        public Expediente? Estado { get; private set; } = inicial?.Copia();

        public List<object> Eventos { get; } = [];

        public void Emitir(object evento)
        {
            Eventos.Add(evento);
            if (evento is ExpedienteAbierto abierto)
                Estado = Create(abierto);
            else
                Estado!.Aplicar(evento);
        }
    }
}
