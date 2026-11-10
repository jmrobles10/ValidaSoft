namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// Tiempo que un área tuvo el expediente en su mano: la suma de las rondas en que estuvo <c>Pendiente</c> con el
/// expediente en revisión. <see cref="Rondas"/> cuenta las veces que le llegó (activación, reenvío tras observar, versión
/// nueva que invalidó su decisión). <see cref="SlaIncumplido"/>: alguna ronda superó <see cref="SlaHoras"/> (también la que
/// sigue abierta).
/// </summary>
public sealed record TiempoDeArea(Guid AreaId, string Codigo, string Nombre, int Etapa, double Horas, int Rondas, int? SlaHoras, bool SlaIncumplido,
    EstadoAreaExpediente Estado);

/// <summary>
/// Tiempos de un expediente medidos sobre sus eventos (IF-2). <see cref="HorasTotales"/>: de la apertura al cierre (o a
/// <c>ahora</c> si sigue abierto). <see cref="HorasEnCarga"/>: el tercero aún cargaba (estado <c>Abierto</c>).
/// <see cref="HorasConTercero"/>: devuelto por observaciones (estado <c>Observado</c>). <see cref="HorasEnRevision"/>: en
/// manos de la empresa. <see cref="HorasHastaAprobacion"/>: solo si se aprobó (igual a <see cref="HorasTotales"/>).
/// </summary>
public sealed record TiemposDeExpediente(
    double HorasTotales,
    double HorasEnCarga,
    double HorasConTercero,
    double HorasEnRevision,
    double? HorasHastaAprobacion,
    int Devoluciones,
    IReadOnlyList<TiempoDeArea> Areas);

public sealed partial class Expediente
{
    /// <summary>
    /// Mide los tiempos reproduciendo los eventos del stream con las mismas reglas del agregado: cada intervalo entre dos
    /// eventos se atribuye al estado que había al empezar (carga, revisión o devuelto al tercero) y, en revisión, a cada
    /// área que estaba <c>Pendiente</c> en la etapa activa. Usa la <c>Fecha</c> de cada evento (la del negocio, la misma
    /// que guarda el agregado); los eventos que no son del expediente se ignoran.
    /// </summary>
    public static TiemposDeExpediente MedirTiempos(IEnumerable<object> eventos, DateTimeOffset ahora)
    {
        Expediente? estado = null;
        DateTimeOffset? anterior = null;
        var carga = 0d;
        var conTercero = 0d;
        var revision = 0d;
        var devoluciones = 0;
        var horasPorArea = new Dictionary<Guid, double>();
        var rondas = new Dictionary<Guid, int>();
        var inicioRonda = new Dictionary<Guid, DateTimeOffset>();
        var incumplido = new HashSet<Guid>();
        IReadOnlySet<Guid> pendientes = new HashSet<Guid>();

        void Acumular(DateTimeOffset hasta)
        {
            if (estado is null || anterior is not { } desde || hasta <= desde)
                return;
            var horas = (hasta - desde).TotalHours;
            switch (estado.Estado)
            {
                case EstadoExpediente.Abierto: carga += horas; break;
                case EstadoExpediente.Observado: conTercero += horas; break;
                case EstadoExpediente.EnRevision:
                    revision += horas;
                    foreach (var area in pendientes)
                        horasPorArea[area] = horasPorArea.GetValueOrDefault(area) + horas;
                    break;
            }
        }

        void CerrarRonda(Guid areaId, DateTimeOffset hasta)
        {
            if (inicioRonda.Remove(areaId, out var inicio) && estado?.BuscarArea(areaId) is { SlaHoras: { } sla } && (hasta - inicio).TotalHours > sla)
                incumplido.Add(areaId);
        }

        foreach (var evento in eventos)
        {
            if (FechaDe(evento) is not { } fecha)
                continue;
            if (evento is ExpedienteAbierto abierto)
            {
                estado = Create(abierto);
                anterior = fecha;
                continue;
            }
            if (estado is null)
                continue;

            Acumular(fecha);
            if (evento is ExpedienteObservado)
                devoluciones++;
            estado.Aplicar(evento);
            anterior = fecha;

            var ahoraPendientes = PendientesEnRevision(estado);
            foreach (var area in ahoraPendientes.Where(a => !pendientes.Contains(a)))
            {
                rondas[area] = rondas.GetValueOrDefault(area) + 1;
                inicioRonda[area] = fecha;
            }
            foreach (var area in pendientes.Where(a => !ahoraPendientes.Contains(a)))
                CerrarRonda(area, fecha);
            pendientes = ahoraPendientes;
        }

        if (estado is null)
            return new TiemposDeExpediente(0, 0, 0, 0, null, 0, []);

        var fin = estado.CerradoEn ?? ahora;
        if (estado.CerradoEn is null)
        {
            Acumular(ahora);
            foreach (var area in pendientes)
                CerrarRonda(area, ahora);
        }

        var total = Math.Max(0, (fin - estado.AbiertoEn).TotalHours);
        var areas = estado.Etapas.OrderBy(e => e.Orden)
            .SelectMany(e => e.Areas.Select(a => new TiempoDeArea(a.AreaId, a.Codigo, a.Nombre, e.Orden, horasPorArea.GetValueOrDefault(a.AreaId),
                rondas.GetValueOrDefault(a.AreaId), a.SlaHoras, incumplido.Contains(a.AreaId), a.Estado)))
            .ToList();
        return new TiemposDeExpediente(total, carga, conTercero, revision, estado.Estado == EstadoExpediente.Aprobado ? total : null, devoluciones, areas);
    }

    /// <summary>Áreas de la etapa activa que esperan su decisión mientras el expediente está en revisión.</summary>
    private static HashSet<Guid> PendientesEnRevision(Expediente expediente) =>
        expediente is { Estado: EstadoExpediente.EnRevision, EtapaActiva: { } etapa }
            ? [.. etapa.Areas.Where(a => a.Estado == EstadoAreaExpediente.Pendiente).Select(a => a.AreaId)]
            : [];

    /// <summary>Fecha de negocio de un evento del expediente; nula si no es uno de sus eventos.</summary>
    private static DateTimeOffset? FechaDe(object evento) => evento switch
    {
        ExpedienteAbierto e => e.Fecha,
        FlujoSeleccionado e => e.Fecha,
        RequisitosCalculados e => e.Fecha,
        RequisitosAgregados e => e.Fecha,
        VersionAdjuntadaARequisito e => e.Fecha,
        VeredictosInvalidadosPorNuevaVersion e => e.Fecha,
        VeredictoDeRequisitoEmitido e => e.Fecha,
        RequisitoEximido e => e.Fecha,
        RequisitoRatificado e => e.Fecha,
        CompletitudDeSeccionEvaluada e => e.Fecha,
        SeccionAceptadaPorEmpresa e => e.Fecha,
        ExpedienteEnviadoARevision e => e.Fecha,
        EtapaActivada e => e.Fecha,
        VeredictoDeAreaEmitido e => e.Fecha,
        ExpedienteObservado e => e.Fecha,
        ExpedienteReenviado e => e.Fecha,
        RevisionReanudada e => e.Fecha,
        EtapaAprobada e => e.Fecha,
        ExpedienteAprobado e => e.Fecha,
        ExpedienteRechazado e => e.Fecha,
        ExpedienteAnulado e => e.Fecha,
        VeredictoAnuladoPorAdministrador e => e.Fecha,
        _ => null
    };
}
