using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;

namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// Documento que el flujo pide en este expediente (docs/03 §6.2, reducido): el tipo del tenant, las áreas que lo revisan,
/// la versión de la bóveda que lo cubre y los veredictos de cada área sobre esa versión. <see cref="Estado"/> se deriva
/// en cada evento (<see cref="EstadoRequisito"/>); nunca se escribe desde fuera.
/// </summary>
public sealed class RequisitoDeExpediente
{
    public Guid Id { get; set; }

    public Guid TipoDocumentoTenantId { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Canónico del tipo (casa con el documento global del tercero); nulo en un tipo propio.</summary>
    public string? TipoCanonico { get; set; }

    public AmbitoDocumento Ambito { get; set; }

    public bool Obligatorio { get; set; }

    /// <summary>Áreas que lo revisan, ya resueltas al abrir (vacío en el flujo ⇒ todas).</summary>
    public List<Guid> AreasQueRevisan { get; set; } = [];

    /// <summary>Aprobadores distintos que pide cada área para este requisito; nulo ⇒ los del área.</summary>
    public int? AprobadoresRequeridos { get; set; }

    public EstadoRequisito Estado { get; set; }

    public VersionDeRequisito? VersionActual { get; set; }

    /// <summary>Aprobación de esta empresa a esta misma versión en otro expediente del tercero (RQ-11); nula si no la hay.</summary>
    public AprobacionPrevia? AprobacionPrevia { get; set; }

    /// <summary>¿La versión actual es la que la empresa ya aprobó en otro expediente?</summary>
    public bool ReutilizaAprobacion => AprobacionPrevia is { } previa && VersionActual is { } version &&
                                       version.DocumentoId == previa.DocumentoId && version.Numero == previa.Version;

    /// <summary>Versiones que lo cubrieron antes (la más reciente al final).</summary>
    public List<VersionDeRequisito> Historial { get; set; } = [];

    /// <summary>Veces que llegó una versión nueva después de la primera.</summary>
    public int Reenvios { get; set; }

    public List<VeredictoRegistrado> Veredictos { get; set; } = [];

    public bool Eximido { get; set; }

    public string? EximidoPor { get; set; }

    public string? JustificacionExencion { get; set; }

    public DateTimeOffset? EximidoEn { get; set; }

    /// <summary>
    /// ¿Cuenta para el alcance de sus áreas? Un opcional sin versión no frena a nadie; si el tercero lo carga, se revisa.
    /// </summary>
    [JsonIgnore]
    public bool Cuenta => Obligatorio || VersionActual is not null || Eximido;

    public bool LoRevisa(Guid areaId) => AreasQueRevisan.Contains(areaId);

    public int AprobadoresPara(AreaDeExpediente area) => AprobadoresRequeridos ?? area.AprobadoresRequeridos;

    /// <summary>Aprobaciones vigentes del área: una del sistema o del administrador completa; si no, N personas distintas.</summary>
    public bool AprobadoPor(AreaDeExpediente area)
    {
        if (Eximido)
            return true;
        var aprobaciones = Veredictos.Where(v => v.Vigente && v.AreaId == area.AreaId && v.Veredicto == Veredicto.Aprobado).ToList();
        return aprobaciones.Any(v => v.Origen != OrigenVeredicto.Humano) ||
               aprobaciones.Where(v => v.RevisorId is not null).Select(v => v.RevisorId).Distinct().Count() >= AprobadoresPara(area);
    }

    public bool ObservadoPor(Guid areaId) => Veredictos.Any(v => v.Vigente && v.AreaId == areaId && v.Veredicto == Veredicto.Observado);

    public bool DecididoPor(AreaDeExpediente area) => AprobadoPor(area) || ObservadoPor(area.AreaId);

    /// <summary>¿Esta persona ya emitió, en esta ronda, un veredicto humano vigente sobre el requisito en el área?</summary>
    public bool DecidioPersona(Guid areaId, string revisorId) =>
        Veredictos.Any(v => v.Vigente && v.AreaId == areaId && v.Origen == OrigenVeredicto.Humano && v.RevisorId == revisorId);

    /// <summary>Personas distintas con aprobación humana vigente del área (x de <see cref="AprobadoresPara"/>).</summary>
    public int AprobacionesDe(AreaDeExpediente area) =>
        AprobadoPor(area) && !Eximido
            ? Math.Max(AprobadoresPara(area), 1)
            : Veredictos.Where(v => v.Vigente && v.AreaId == area.AreaId && v.Veredicto == Veredicto.Aprobado && v.RevisorId is not null)
                .Select(v => v.RevisorId).Distinct().Count();
}

public sealed partial class Expediente
{
    /// <summary>Con requisitos: todos los obligatorios cargados o eximidos. Sin requisitos: siempre (FL-1).</summary>
    [JsonIgnore]
    public bool ListoParaRevision => Requisitos.Where(r => r.Obligatorio).All(r => r.VersionActual is not null || r.Eximido);

    public RequisitoDeExpediente? BuscarRequisito(Guid requisitoId) => Requisitos.FirstOrDefault(r => r.Id == requisitoId);

    /// <summary>Requisitos que cuentan para el área (en su alcance y no opcionales sin cargar).</summary>
    public IEnumerable<RequisitoDeExpediente> RequisitosDelArea(Guid areaId) => Requisitos.Where(r => r.Cuenta && r.LoRevisa(areaId));

    /// <summary>Requisitos con alguna observación vigente.</summary>
    public IEnumerable<RequisitoDeExpediente> RequisitosConObservacion() =>
        Requisitos.Where(r => r.Veredictos.Any(v => v.Vigente && v.Veredicto == Veredicto.Observado));

    // ---------- Decisiones ----------

    /// <summary>
    /// Veredicto de un miembro del área sobre un requisito de su alcance y una versión concreta. La versión debe ser la
    /// actual del requisito (409 <c>version-superada</c>); cada persona decide una vez por requisito y área
    /// (<c>aprobador-repetido</c>) y el requisito queda decidido por el área al observarlo o al juntar
    /// <c>AprobadoresRequeridos</c> personas distintas (<c>requisito-ya-decidido</c>). La aprobación del área y el
    /// resto de la cascada se derivan (RQ-4, RQ-5). Las mismas reglas valen documento a documento en la decisión por
    /// área (<see cref="AprobarArea"/>, <see cref="ObservarArea"/>).
    /// </summary>
    public IReadOnlyList<object> EmitirVeredictoDeRequisito(Guid requisitoId, Guid areaId, Revisor revisor, Veredicto veredicto, int? sobreVersion,
        IReadOnlyList<MotivoAplicado> motivos, string? observacion, DateTimeOffset ahora)
    {
        var area = AreaParaDecidirPorRequisitos(areaId, revisor);
        var revisorId = revisor.UsuarioId.ToString();
        var requisito = RequisitoDecidible(requisitoId, area, revisorId, sobreVersion, superadas: null);
        var evento = VeredictoHumano(requisito, area, veredicto, revisorId, Motivos(veredicto, motivos), Texto(observacion, "observacion"), ahora);
        return Simular(s => s.Emitir(evento), ahora);
    }

    /// <summary>
    /// Un administrador exime el requisito (justificación obligatoria). Sus veredictos dejan de contar; si era lo único
    /// que faltaba cargar, el expediente pasa a revisión, y si era la última observación vigente de un expediente
    /// devuelto, la revisión se reanuda (RQ-6).
    /// </summary>
    public IReadOnlyList<object> EximirRequisito(Guid requisitoId, string por, string? justificacion, DateTimeOffset ahora)
    {
        if (!EstaAbierto)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"No se eximen requisitos de un expediente {Estado}.");
        var requisito = BuscarRequisito(requisitoId)
                        ?? throw ReglaDeNegocioException.NoEncontrado("requisito-no-encontrado", "Ese requisito no está en el expediente.");
        if (requisito.Eximido)
            throw ReglaDeNegocioException.Conflicto("requisito-eximido", $"{requisito.Nombre} ya está eximido.");
        if (requisito.Estado == EstadoRequisito.Aprobado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"{requisito.Nombre} ya está aprobado.");

        var texto = Requerido(justificacion, "justificacion-requerida", "Eximir un requisito necesita una justificación.");
        return Simular(s =>
        {
            s.Emitir(new RequisitoEximido(requisitoId, por, texto, ahora));
            if (s.Estado is { Estado: EstadoExpediente.Observado, EtapaActiva: { } etapa } &&
                etapa.Areas.All(a => a.Estado != EstadoAreaExpediente.Observada))
                s.Emitir(new RevisionReanudada(Guid.Empty, ahora, requisitoId));
        }, ahora);
    }

    /// <summary>
    /// Adjunta a cada requisito la versión actual que la bóveda compartió con el vínculo (<paramref name="disponibles"/>,
    /// por requisito). Idempotente: si el requisito ya tiene esa versión no hace nada. Si tenía veredictos vigentes, se
    /// invalidan (solo ese requisito; RQ-8). No toca requisitos eximidos ni aquellos cuyas áreas están todas en etapas
    /// ya aprobadas (nadie queda para revisarla: la versión nueva queda para la revalidación). Si alguna de sus áreas
    /// sigue en una etapa abierta —p. ej. una etapa posterior que lo observó—, la versión se adjunta e invalida solo los
    /// veredictos de esas áreas; lo aprobado por etapas cerradas queda como aprobado sobre la versión anterior (RQ-10).
    /// Solo en expedientes abiertos.
    /// </summary>
    public IReadOnlyList<object> SincronizarVersiones(IReadOnlyDictionary<Guid, VersionDeRequisito> disponibles, DateTimeOffset ahora)
    {
        if (!EstaAbierto || !ConRequisitos)
            return [];

        var cambios = new List<(RequisitoDeExpediente Requisito, VersionDeRequisito Version)>();
        foreach (var requisito in Requisitos)
        {
            if (requisito.Eximido || !disponibles.TryGetValue(requisito.Id, out var version))
                continue;
            if (requisito.VersionActual is { } actual && actual.DocumentoId == version.DocumentoId && actual.Numero == version.Numero)
                continue;
            if (requisito.AreasQueRevisan.All(EtapaCerrada))
                continue;
            cambios.Add((requisito, version));
        }

        if (cambios.Count == 0)
            return [];

        return Simular(s =>
        {
            foreach (var (requisito, version) in cambios)
            {
                var afectadas = requisito.Veredictos.Where(v => v.Vigente).Select(v => v.AreaId).OfType<Guid>()
                    .Where(a => !EtapaCerrada(a)).Distinct().ToList();
                s.Emitir(new VersionAdjuntadaARequisito(requisito.Id, version, requisito.VersionActual?.Numero, ahora));
                if (afectadas.Count > 0)
                    s.Emitir(new VeredictosInvalidadosPorNuevaVersion(requisito.Id, requisito.VersionActual!.Numero, afectadas, ahora));
            }
        }, ahora);
    }

    /// <summary>¿La etapa del área ya se aprobó? Sus veredictos quedan cerrados (no los invalida una versión nueva).</summary>
    private bool EtapaCerrada(Guid areaId) => EtapaDe(areaId) is { Estado: EstadoEtapa.Aprobada };

    /// <summary>El administrador aprueba en nombre del área todos los requisitos de su alcance que ella aún no decidió.</summary>
    private IReadOnlyList<object> AprobarRequisitosComoAdministrador(AreaDeExpediente area, string administradorId, string justificacion,
        DateTimeOffset ahora)
    {
        var pendientes = RequisitosDelArea(area.AreaId).Where(r => !r.DecididoPor(area)).ToList();
        if (pendientes.FirstOrDefault(r => r.VersionActual is null) is { } sinVersion)
            throw ReglaDeNegocioException.Conflicto("requisito-sin-version", $"El tercero aún no ha cargado {sinVersion.Nombre}.");

        return Simular(s =>
        {
            foreach (var requisito in pendientes)
                s.Emitir(new VeredictoDeRequisitoEmitido(Guid.CreateVersion7(), requisito.Id, area.AreaId, Veredicto.Aprobado,
                    OrigenVeredicto.Administrador, administradorId, requisito.VersionActual!.Numero, [], null, justificacion, ahora));
        }, ahora);
    }

    // ---------- Evolución ----------

    public void Apply(RequisitosCalculados e)
    {
        Requisitos = [.. e.Requisitos.Select(r => new RequisitoDeExpediente
        {
            Id = r.RequisitoId,
            TipoDocumentoTenantId = r.TipoDocumentoTenantId,
            Codigo = r.Codigo,
            Nombre = r.Nombre,
            TipoCanonico = r.TipoCanonico,
            Ambito = r.Ambito,
            Obligatorio = r.Obligatorio,
            AreasQueRevisan = [.. r.AreasQueRevisan],
            AprobadoresRequeridos = r.AprobadoresRequeridos,
            VersionActual = r.Version,
            AprobacionPrevia = r.Previa
        })];
        RecalcularRequisitos(e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VersionAdjuntadaARequisito e)
    {
        var requisito = BuscarRequisito(e.RequisitoId)!;
        if (requisito.VersionActual is { } anterior)
        {
            requisito.Historial.Add(anterior);
            requisito.Reenvios++;
        }

        requisito.VersionActual = e.Version;
        RecalcularRequisitos(e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VeredictosInvalidadosPorNuevaVersion e)
    {
        // Solo los de las áreas afectadas: lo aprobado por una etapa cerrada sigue en pie (RQ-10).
        var afectadas = e.AreasAfectadas.ToHashSet();
        foreach (var veredicto in BuscarRequisito(e.RequisitoId)!.Veredictos.Where(v => v.Vigente && v.AreaId is { } area && afectadas.Contains(area)))
        {
            veredicto.Vigente = false;
            veredicto.Invalidado = true;
        }

        RecalcularRequisitos(e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VeredictoDeRequisitoEmitido e)
    {
        BuscarRequisito(e.RequisitoId)!.Veredictos.Add(new VeredictoRegistrado
        {
            Id = e.VeredictoId,
            RequisitoId = e.RequisitoId,
            AreaId = e.AreaId,
            SobreVersion = e.SobreVersion,
            Veredicto = e.Veredicto,
            Origen = e.Origen,
            RevisorId = e.RevisorId,
            Motivos = [.. e.Motivos],
            Observacion = e.Observacion,
            Justificacion = e.Justificacion,
            Fecha = e.Fecha
        });
        RecalcularRequisitos(e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(RequisitoEximido e)
    {
        var requisito = BuscarRequisito(e.RequisitoId)!;
        requisito.Eximido = true;
        requisito.EximidoPor = e.Por;
        requisito.JustificacionExencion = e.Justificacion;
        requisito.EximidoEn = e.Fecha;
        foreach (var veredicto in requisito.Veredictos.Where(v => v.Vigente))
        {
            veredicto.Vigente = false;
            veredicto.Superado = true;
        }

        RecalcularRequisitos(e.Fecha);
        ActualizadoEn = e.Fecha;
    }

    // ---------- Reglas internas ----------

    /// <summary>Copia las áreas del flujo a los requisitos que no las indican (vacío ⇒ todas las áreas, cada una en su etapa).</summary>
    private static IReadOnlyList<RequisitoCalculado> ResolverAlcance(IReadOnlyList<RequisitoCalculado> requisitos, FlujoParaExpediente flujo)
    {
        var todas = flujo.Etapas.OrderBy(e => e.Orden).SelectMany(e => e.Areas).Select(a => a.AreaId).ToList();
        return [.. requisitos.Select(r => r.AreasQueRevisan is { Count: > 0 } ? r : r with { AreasQueRevisan = todas })];
    }

    private EtapaDeExpediente? EtapaDe(Guid areaId) => Etapas.FirstOrDefault(e => e.Areas.Any(a => a.AreaId == areaId));

    /// <summary>
    /// Deriva, tras cada evento, el estado de cada requisito y el de cada área: una observación vigente del área sobre
    /// un requisito de su alcance manda (<c>Observada</c>); todos aprobados por ella o eximidos ⇒ <c>Aprobada</c>; si no,
    /// <c>Pendiente</c> (o <c>Esperando</c> si su etapa no se activó). El veredicto de área derivado deja de contar en
    /// cuanto el área pierde su aprobación. No hace nada en un expediente sin requisitos.
    /// </summary>
    private void RecalcularRequisitos(DateTimeOffset fecha)
    {
        if (!ConRequisitos)
            return;

        foreach (var requisito in Requisitos)
            requisito.Estado = EstadoDe(requisito);

        foreach (var etapa in Etapas)
        {
            foreach (var area in etapa.Areas)
            {
                var previo = area.Estado;
                var propios = RequisitosDelArea(area.AreaId).ToList();
                area.RequisitosPorDecidir = propios.Count(r => !r.DecididoPor(area));
                area.Estado = etapa.Estado == EstadoEtapa.Esperando ? EstadoAreaExpediente.Esperando
                    : propios.Any(r => r.ObservadoPor(area.AreaId)) ? EstadoAreaExpediente.Observada
                    : propios.All(r => r.AprobadoPor(area)) ? EstadoAreaExpediente.Aprobada
                    : EstadoAreaExpediente.Pendiente;

                if (area.Estado == EstadoAreaExpediente.Pendiente && previo != EstadoAreaExpediente.Pendiente)
                    area.PendienteDesde = fecha;

                if (area.Estado != EstadoAreaExpediente.Aprobada)
                {
                    foreach (var derivado in area.Veredictos.Where(v => v.Vigente && v.Derivado))
                    {
                        derivado.Vigente = false;
                        derivado.Superado = true;
                    }
                }
            }
        }
    }

    private EstadoRequisito EstadoDe(RequisitoDeExpediente requisito)
    {
        if (requisito.Eximido)
            return EstadoRequisito.Eximido;
        if (requisito.VersionActual is null)
            return EstadoRequisito.Pendiente;
        if (Estado == EstadoExpediente.Abierto)
            return EstadoRequisito.Cargado;
        if (requisito.Veredictos.Any(v => v.Vigente && v.Veredicto == Veredicto.Observado))
            return EstadoRequisito.Observado;

        var areas = requisito.AreasQueRevisan.Select(BuscarArea).OfType<AreaDeExpediente>().ToList();
        if (areas.All(requisito.AprobadoPor))
            return EstadoRequisito.Aprobado;

        // Corregido con una versión nueva mientras el expediente sigue devuelto: espera el reenvío del tercero.
        return Estado == EstadoExpediente.Observado && RequisitosObservados.Contains(requisito.Id)
            ? EstadoRequisito.Cargado
            : EstadoRequisito.EnRevision;
    }

    /// <summary>
    /// Con requisitos, en la etapa activa: las áreas automáticas aprueban (origen <c>Sistema</c>) lo que les falte de
    /// su alcance; cualquier área aprueba igual, con la referencia, lo que la empresa ya aprobó en esa misma versión en
    /// otro expediente del tercero (RQ-11); y el área que quedó aprobada recibe su <c>VeredictoDeAreaEmitido</c>
    /// derivado. Devuelve si emitió algo (la cascada vuelve a evaluar).
    /// </summary>
    private static bool AprobarAutomaticasYDerivadas(Simulacion simulacion, EtapaDeExpediente etapa, DateTimeOffset ahora)
    {
        var expediente = simulacion.Estado!;
        var emitio = false;
        foreach (var area in etapa.Areas.ToList())
        {
            foreach (var requisito in expediente.RequisitosDelArea(area.AreaId).ToList())
            {
                if (requisito.Eximido || requisito.VersionActual is not { } version || requisito.DecididoPor(area))
                    continue;
                var reutiliza = requisito.ReutilizaAprobacion;
                if (!area.Automatica && !reutiliza)
                    continue;
                var nota = reutiliza && requisito.AprobacionPrevia is { } previa
                    ? $"La empresa ya aprobó esta versión en el expediente {previa.Folio} ({previa.TipoVinculo})."
                    : null;
                simulacion.Emitir(new VeredictoDeRequisitoEmitido(Guid.CreateVersion7(), requisito.Id, area.AreaId, Veredicto.Aprobado,
                    OrigenVeredicto.Sistema, null, version.Numero, [], null, nota, ahora));
                emitio = true;
            }

            if (area.Estado == EstadoAreaExpediente.Aprobada && !area.Veredictos.Any(v => v.Vigente && v.Derivado))
            {
                simulacion.Emitir(new VeredictoDeAreaEmitido(Guid.CreateVersion7(), area.AreaId, Veredicto.Aprobado, OrigenDerivado(expediente, area),
                    null, [], null, null, null, ahora, DerivadoDeRequisitos: true));
                emitio = true;
            }
        }

        return emitio;
    }

    /// <summary>Quién aprobó los requisitos del área: <c>Administrador</c> si intervino (o solo hubo exenciones), si no <c>Humano</c>, si no <c>Sistema</c>.</summary>
    private static OrigenVeredicto OrigenDerivado(Expediente expediente, AreaDeExpediente area)
    {
        var origenes = expediente.RequisitosDelArea(area.AreaId)
            .SelectMany(r => r.Veredictos.Where(v => v.Vigente && v.AreaId == area.AreaId && v.Veredicto == Veredicto.Aprobado))
            .Select(v => v.Origen)
            .ToHashSet();
        return origenes.Contains(OrigenVeredicto.Administrador) || origenes.Count == 0 ? OrigenVeredicto.Administrador
            : origenes.Contains(OrigenVeredicto.Humano) ? OrigenVeredicto.Humano
            : OrigenVeredicto.Sistema;
    }

    /// <summary><c>Observado</c> exige al menos un motivo; <c>Aprobado</c> no lleva motivos.</summary>
    private static IReadOnlyList<MotivoAplicado> Motivos(Veredicto veredicto, IReadOnlyList<MotivoAplicado>? motivos)
    {
        var lista = motivos ?? [];
        if (veredicto == Veredicto.Observado && lista.Count == 0)
            throw ReglaDeNegocioException.Validacion("motivo-requerido", "Para observar indica al menos un motivo.");
        if (veredicto == Veredicto.Aprobado && lista.Count > 0)
            throw ReglaDeNegocioException.Validacion("motivos-solo-al-observar", "Los motivos solo acompañan una observación.");
        return lista;
    }
}
