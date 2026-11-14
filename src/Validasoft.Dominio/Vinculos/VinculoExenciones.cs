using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Vinculos;

/// <summary>
/// Una exención del vínculo tal como la guarda el agregado: qué cubre, por qué, hasta cuándo, quién la hizo y cómo terminó. Es un hecho
/// privado de la empresa (EX-1); el estado efectivo en una fecha lo da <see cref="EstadoEn"/>.
/// </summary>
public sealed class ExencionDeVinculo
{
    public Guid Id { get; set; }

    /// <summary>Exime de todos los requisitos del flujo (documentos y secciones) y de la vigencia periódica del vínculo.</summary>
    public bool Todos { get; set; }

    public List<ObjetivoDeExencion> Objetivos { get; set; } = [];

    public string Justificacion { get; set; } = string.Empty;

    /// <summary>Último día en que la exención vale (inclusive); nulo = hasta que se revoque.</summary>
    public DateOnly? Hasta { get; set; }

    public string Por { get; set; } = string.Empty;

    public DateTimeOffset CreadaEn { get; set; }

    public EstadoDeExencion Estado { get; set; }

    public string? RevocadaPor { get; set; }

    public string? JustificacionDeRevocacion { get; set; }

    public DateTimeOffset? RevocadaEn { get; set; }

    public DateTimeOffset? VencidaEn { get; set; }

    /// <summary>¿Rige en esa fecha? Vigente y sin haber pasado su <see cref="Hasta"/>: es la regla única de «estar eximido».</summary>
    public bool VigenteEn(DateOnly hoy) => Estado == EstadoDeExencion.Vigente && (Hasta is null || hoy <= Hasta);

    /// <summary>El estado a esa fecha: una vigente cuya fecha pasó se ve <c>Vencida</c> aunque el job aún no lo haya registrado.</summary>
    public EstadoDeExencion EstadoEn(DateOnly hoy) => Estado == EstadoDeExencion.Vigente && !VigenteEn(hoy) ? EstadoDeExencion.Vencida : Estado;

    /// <summary>¿Cubre este requisito? <see cref="Todos"/> cubre cualquiera; si no, solo los de su lista.</summary>
    public bool Cubre(TipoDeRequisito tipo, Guid? tipoDocumentoTenantId, string? seccion) =>
        Todos || Objetivos.Any(o => o.Cubre(tipo, tipoDocumentoTenantId, seccion));

    /// <summary>¿Cubre el requisito de un certificado, que se nombra por código? (<see cref="Todos"/> o un objetivo con ese código.)</summary>
    public bool CubreCodigo(string codigo) =>
        Todos || Objetivos.Any(o => string.Equals(o.Codigo, codigo, StringComparison.OrdinalIgnoreCase));
}

/// <summary>
/// Exención del vínculo (EX-1…EX-12): la empresa decide no perseguir la renovación de un tercero ya aprobado, de uno o varios tipos de
/// documento o secciones de la ficha o de todo. <b>Regla única</b>: lo que dice <see cref="ExencionDeRequisito"/> (y sus variantes) es lo que
/// consultan el expediente al abrirse, los avisos, el vencimiento de documentos y de la vigencia periódica, la bandeja de vencimientos y la
/// consulta de certificación; nadie más calcula «está eximido».
/// </summary>
public sealed partial class Vinculo
{
    /// <summary>Tope de objetivos de una exención.</summary>
    public const int MaximoDeObjetivosPorExencion = 50;

    /// <summary>Largo máximo de la justificación (de la exención y de su revocación).</summary>
    public const int MaximoDeJustificacionDeExencion = 1000;

    /// <summary>Hasta cuántos años adelante puede fijarse el fin de una exención.</summary>
    public const int MaximoDeAniosDeExencion = 10;

    /// <summary>Exenciones del vínculo, en el orden en que se hicieron (las revocadas y vencidas quedan: es la historia).</summary>
    public List<ExencionDeVinculo> Exenciones { get; set; } = [];

    // ---------- Regla única ----------

    /// <summary>La exención vigente a <paramref name="hoy"/> que cubre este requisito; nula si no lo está.</summary>
    public ExencionDeVinculo? ExencionDeRequisito(TipoDeRequisito tipo, Guid? tipoDocumentoTenantId, string? seccion, DateOnly hoy) =>
        Exenciones.FirstOrDefault(e => e.VigenteEn(hoy) && e.Cubre(tipo, tipoDocumentoTenantId, seccion));

    /// <summary>La exención vigente de un tipo de documento del tenant.</summary>
    public ExencionDeVinculo? ExencionDeDocumento(Guid tipoDocumentoTenantId, DateOnly hoy) =>
        ExencionDeRequisito(TipoDeRequisito.Documento, tipoDocumentoTenantId, null, hoy);

    /// <summary>La exención vigente de una sección de la ficha.</summary>
    public ExencionDeVinculo? ExencionDeSeccion(string? seccion, DateOnly hoy) =>
        ExencionDeRequisito(TipoDeRequisito.Seccion, null, seccion, hoy);

    /// <summary>La exención vigente de todo (requisitos y vigencia periódica); nula si no hay una.</summary>
    public ExencionDeVinculo? ExencionTotal(DateOnly hoy) => Exenciones.FirstOrDefault(e => e.Todos && e.VigenteEn(hoy));

    /// <summary>Las exenciones que rigen a esa fecha.</summary>
    public IEnumerable<ExencionDeVinculo> ExencionesVigentes(DateOnly hoy) => Exenciones.Where(e => e.VigenteEn(hoy));

    /// <summary>
    /// La exención vigente que cubre un requisito nombrado por su código: lo que hace falta para el contenido de un certificado, que nombra cada
    /// requisito así.
    /// </summary>
    public ExencionDeVinculo? ExencionDeCodigo(string codigo, DateOnly hoy) => Exenciones.FirstOrDefault(e => e.VigenteEn(hoy) && e.CubreCodigo(codigo));

    // ---------- Decisiones ----------

    /// <summary>
    /// Eximir al vínculo de requisitos: <paramref name="todos"/> o una lista de <paramref name="objetivos"/>, con justificación obligatoria y
    /// vigencia opcional (<paramref name="hasta"/>, inclusive). Solo un vínculo que ya fue aprobado (no hay nada que «dejar de perseguir» en
    /// uno que nunca se aprobó: eximirlo equivaldría a aprobarlo sin revisión) y que no esté bloqueado ni terminado. Una exención por
    /// requisito a la vez: 409 <c>exencion-superpuesta</c>.
    /// </summary>
    public ExencionDeVinculoRegistrada Eximir(bool todos, IReadOnlyList<ObjetivoDeExencion> objetivos, string? justificacion, DateOnly? hasta, string por,
        DateTimeOffset ahora)
    {
        if (Estado == EstadoVinculo.Bloqueado)
            throw ReglaDeNegocioException.Conflicto("vinculo-bloqueado", "El vínculo está bloqueado: desbloquéalo antes de eximirlo.");
        AsegurarNoTerminal("eximir");
        if (!TuvoAprobacion)
            throw ReglaDeNegocioException.Conflicto("vinculo-sin-aprobacion",
                "Solo se exime un vínculo que ya fue aprobado: eximir uno que nunca se validó equivaldría a aprobarlo sin revisión.");

        var texto = JustificacionDeExencion(justificacion, "Eximir al vínculo necesita una justificación.");
        var unicos = (objetivos ?? []).DistinctBy(o => o.Clave).ToList();
        if (todos && unicos.Count > 0)
            throw ReglaDeNegocioException.Validacion("exencion-alcance-invalido", "Una exención es de todo o de requisitos concretos, no de las dos cosas.");
        if (!todos && unicos.Count == 0)
            throw ReglaDeNegocioException.Validacion("exencion-sin-alcance", "Indica de qué se exime al vínculo: requisitos concretos o todo.");
        if (unicos.Count > MaximoDeObjetivosPorExencion)
            throw ReglaDeNegocioException.Validacion("exencion-alcance-invalido", $"Una exención cubre hasta {MaximoDeObjetivosPorExencion} requisitos.");

        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        if (hasta is { } fin && (fin < hoy || fin > hoy.AddYears(MaximoDeAniosDeExencion)))
            throw ReglaDeNegocioException.Validacion("fecha-invalida",
                $"La exención dura hasta hoy como mínimo y {MaximoDeAniosDeExencion} años como máximo.");

        var vigentes = ExencionesVigentes(hoy).ToList();
        var chocan = todos ? vigentes : vigentes.Where(v => v.Todos || v.Objetivos.Any(o => unicos.Any(u => u.Clave == o.Clave))).ToList();
        if (chocan.Count > 0)
            throw ReglaDeNegocioException.Conflicto("exencion-superpuesta",
                "Ya hay una exención vigente que cubre " + (todos ? "algo de esto" : "alguno de estos requisitos") +
                $" ({string.Join(", ", chocan.Select(Resumen))}): revócala antes de crear otra.");

        return new ExencionDeVinculoRegistrada(Guid.CreateVersion7(), todos, unicos, texto, hasta, QuienActua(por), ahora);
    }

    /// <summary>Revoca una exención vigente con justificación; el vínculo vuelve a regirse por las vigencias. Se puede revocar en un vínculo bloqueado.</summary>
    public ExencionDeVinculoRevocada RevocarExencion(Guid exencionId, string? justificacion, string por, DateTimeOffset ahora)
    {
        AsegurarNoTerminal("revocar una exención de");
        var texto = JustificacionDeExencion(justificacion, "Revocar la exención necesita una justificación.");
        var exencion = Exenciones.FirstOrDefault(e => e.Id == exencionId)
                       ?? throw ReglaDeNegocioException.NoEncontrado("exencion-no-encontrada", "El vínculo no tiene esa exención.");
        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        switch (exencion.EstadoEn(hoy))
        {
            case EstadoDeExencion.Revocada:
                throw ReglaDeNegocioException.Conflicto("exencion-ya-revocada", "Esa exención ya está revocada.");
            case EstadoDeExencion.Vencida:
                throw ReglaDeNegocioException.Conflicto("exencion-vencida", "Esa exención ya venció.");
        }

        return new ExencionDeVinculoRevocada(exencionId, texto, QuienActua(por), ahora);
    }

    /// <summary>
    /// Las exenciones vigentes cuya fecha ya pasó (<see cref="ExencionDeVinculo.Hasta"/> anterior a hoy): las marca <c>Vencida</c>. Vacío si no
    /// hay ninguna (idempotente). El job de vigencias las emite y recalcula el estado del vínculo con lo que sigue.
    /// </summary>
    public IReadOnlyList<ExencionDeVinculoVencida> VencerExenciones(DateTimeOffset ahora)
    {
        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        return
        [
            .. Exenciones.Where(e => e is { Estado: EstadoDeExencion.Vigente, Hasta: { } hasta } && hoy > hasta)
                .Select(e => new ExencionDeVinculoVencida(e.Id, e.Hasta!.Value, ahora))
        ];
    }

    /// <summary>
    /// Un vínculo <c>PorVencer</c> o <c>Vencido</c> sin expediente abierto, con una exención total vigente, vuelve a <c>Activo</c>: no hay nada que
    /// renovar. Nulo si no le corresponde (con un expediente abierto, el cierre de ese expediente es el camino; sin exención total, nada cambia).
    /// </summary>
    public VinculoRestablecidoPorExencion? RestablecerPorExencion(DateTimeOffset ahora) =>
        TuvoAprobacion && ExpedienteAbiertoId is null && Estado is (EstadoVinculo.PorVencer or EstadoVinculo.Vencido) &&
        ExencionTotal(CalculadoraDeVigencia.Fecha(ahora)) is { } total
            ? new VinculoRestablecidoPorExencion(Estado, total.Id, ahora)
            : null;

    // ---------- Evolución ----------

    public void Apply(ExencionDeVinculoRegistrada e)
    {
        // Idempotente: quien anexa el evento al stream puede aplicarlo también a mano sobre el agregado ya actualizado.
        if (Exenciones.Any(x => x.Id == e.ExencionId))
            return;
        Exenciones.Add(new ExencionDeVinculo
        {
            Id = e.ExencionId,
            Todos = e.Todos,
            Objetivos = [.. e.Objetivos],
            Justificacion = e.Justificacion,
            Hasta = e.Hasta,
            Por = e.Por,
            CreadaEn = e.Fecha,
            Estado = EstadoDeExencion.Vigente
        });
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExencionDeVinculoRevocada e)
    {
        if (Exenciones.FirstOrDefault(x => x.Id == e.ExencionId) is { } exencion)
        {
            exencion.Estado = EstadoDeExencion.Revocada;
            exencion.RevocadaPor = e.Por;
            exencion.JustificacionDeRevocacion = e.Justificacion;
            exencion.RevocadaEn = e.Fecha;
        }

        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExencionDeVinculoVencida e)
    {
        if (Exenciones.FirstOrDefault(x => x.Id == e.ExencionId) is { } exencion)
        {
            exencion.Estado = EstadoDeExencion.Vencida;
            exencion.VencidaEn = e.Fecha;
        }

        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoRestablecidoPorExencion e)
    {
        Estado = EstadoVinculo.Activo;
        RenovacionPorVigenciaPendiente = false;
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    // ---------- Reglas internas ----------

    private static string JustificacionDeExencion(string? justificacion, string mensajeSinTexto)
    {
        if (string.IsNullOrWhiteSpace(justificacion))
            throw ReglaDeNegocioException.Validacion("justificacion-requerida", mensajeSinTexto);
        var texto = justificacion.Trim();
        return texto.Length <= MaximoDeJustificacionDeExencion
            ? texto
            : throw ReglaDeNegocioException.Validacion("justificacion-demasiado-larga", $"La justificación admite hasta {MaximoDeJustificacionDeExencion} caracteres.");
    }

    private static string QuienActua(string? por) => string.IsNullOrWhiteSpace(por) ? "sistema" : por.Trim();

    private static string Resumen(ExencionDeVinculo e) =>
        e.Todos ? "todo" : string.Join(" y ", e.Objetivos.Select(o => o.Nombre));
}
