using System.Text.Json.Serialization;

namespace Validasoft.Dominio.Documentos;

/// <summary>De dónde salió el <c>vigenteHasta</c> (docs/02 §8.1, <c>motivo_vigencia</c>).</summary>
public enum BaseVigencia
{
    /// <summary>Regla <c>Ninguna</c> y sin fecha de vencimiento declarada: no vence.</summary>
    SinVencimiento,

    /// <summary>Fecha de expedición declarada (<c>fecha_expedicion</c>) + días de la regla.</summary>
    FechaExpedicion,

    /// <summary>Sin fecha de expedición declarada: fecha de carga + días de la regla.</summary>
    FechaCarga,

    /// <summary>Ganó la fecha de vencimiento propia del documento (<c>fecha_vencimiento</c>).</summary>
    VencimientoPropio,

    /// <summary>Regla <c>PeriodoMinimo</c>: el fin del periodo aún no se calcula (H6); sin fecha.</summary>
    PeriodoNoCalculado,

    /// <summary>El tercero ratificó que el documento no cambió: los días de la regla corren desde la ratificación (I-14).</summary>
    Ratificacion
}

/// <summary>
/// Estado de la vigencia en una fecha: <c>PorVencer</c> desde el primer día de aviso, <c>EnGracia</c> pasado
/// <c>vigenteHasta</c> y <c>Vencida</c> pasada la gracia (o marcada con <see cref="VersionVencida"/>).
/// </summary>
public enum EstadoVigencia
{
    SinVencimiento,
    Vigente,
    PorVencer,
    EnGracia,
    Vencida
}

public sealed record CalculoDeVigencia(DateOnly? VigenteHasta, BaseVigencia Base);

/// <summary>
/// Vigencia de una versión para un vínculo (docs/02 §8.1: «el hecho es uno; la exigencia es de cada una»): la calcula la
/// regla del tipo del tenant de ese vínculo. <see cref="VencidaEn"/> se llena con <see cref="VersionVencida"/>.
/// </summary>
public sealed class VigenciaDeVersion
{
    public Guid TenantId { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid VinculoId { get; set; }

    public Guid TipoDocumentoTenantId { get; set; }

    public DateOnly? VigenteHasta { get; set; }

    public BaseVigencia Base { get; set; }

    public List<int> DiasAviso { get; set; } = [];

    public int DiasGracia { get; set; }

    public DateTimeOffset CalculadaEn { get; set; }

    public DateTimeOffset? VencidaEn { get; set; }

    /// <summary>Última ratificación del tercero para este vínculo (RN-2): desde ella corre la frescura.</summary>
    public DateTimeOffset? RatificadaEn { get; set; }

    [JsonIgnore]
    public DateOnly? FinDeGracia => CalculadoraDeVigencia.FinDeGracia(VigenteHasta, DiasGracia);
}

/// <summary>
/// Cálculo de vigencias en dos capas (docs/02 §8.1, TD-6), en fechas UTC:
/// <list type="bullet">
/// <item>capa 1: <c>fecha_vencimiento</c> declarada (si el tipo la pide y el tercero la llenó);</item>
/// <item>capa 2: la regla de frescura del tipo — <c>MaxDiasDesdeExpedicion(n)</c> desde <c>fecha_expedicion</c> declarada o, si
/// no hay, desde la carga; <c>MaxDiasDesdeAprobacion(n)</c> desde la carga (la aceptación por empresa llega con H6);
/// <c>PeriodoMinimo</c> aún sin fecha; <c>Ninguna</c> no vence.</item>
/// </list>
/// <c>vigenteHasta = mín(capa1, capa2)</c> e inclusivo; la versión vence (<see cref="VersionVencida"/>) el día siguiente a
/// <c>vigenteHasta + diasGracia</c>.
/// </summary>
public static class CalculadoraDeVigencia
{
    /// <summary>
    /// Zona horaria del calendario del negocio (hoy, vencimientos, «expedición no futura»). Por omisión Bogotá; se fija al
    /// arrancar con <c>Validasoft:ZonaHoraria</c> (FE-1). En UTC, entre las 7 p. m. y la medianoche de Bogotá ya era «mañana».
    /// </summary>
    public static TimeZoneInfo ZonaDeNegocio { get; set; } = Zona("America/Bogota");

    /// <summary>La fecha de calendario del instante en la zona del negocio.</summary>
    public static DateOnly Fecha(DateTimeOffset instante) => DateOnly.FromDateTime(TimeZoneInfo.ConvertTime(instante, ZonaDeNegocio).DateTime);

    /// <summary>La zona IANA indicada, o UTC si el sistema no la conoce.</summary>
    public static TimeZoneInfo Zona(string? id) =>
        !string.IsNullOrWhiteSpace(id) && TimeZoneInfo.TryFindSystemTimeZoneById(id, out var zona) ? zona : TimeZoneInfo.Utc;

    /// <summary>
    /// Con <paramref name="ratificadaEn"/>, los días de la regla corren desde la ratificación (base <c>Ratificacion</c>,
    /// I-14) en vez de la expedición o la carga; la <c>fecha_vencimiento</c> declarada sigue acotando.
    /// </summary>
    public static CalculoDeVigencia Calcular(ReglaFrescura regla, DateTimeOffset cargadaEn, IReadOnlyList<DatoDeclarado>? datos,
        DateTimeOffset? ratificadaEn = null)
    {
        var vencimientoPropio = EsquemaDeFormulario.Fecha(datos, EsquemaDeFormulario.FechaVencimiento);
        var expedicion = EsquemaDeFormulario.Fecha(datos, EsquemaDeFormulario.FechaExpedicion);
        if (ratificadaEn is { } ratificada && regla.Dias is { } diasRegla &&
            regla.Tipo is TipoReglaFrescura.MaxDiasDesdeExpedicion or TipoReglaFrescura.MaxDiasDesdeAprobacion)
        {
            var desdeRatificacion = Fecha(ratificada).AddDays(diasRegla);
            return vencimientoPropio is { } tope && tope <= desdeRatificacion
                ? new CalculoDeVigencia(tope, BaseVigencia.VencimientoPropio)
                : new CalculoDeVigencia(desdeRatificacion, BaseVigencia.Ratificacion);
        }

        (DateOnly? Fecha, BaseVigencia Base) capa2 = regla.Tipo switch
        {
            TipoReglaFrescura.MaxDiasDesdeExpedicion when regla.Dias is { } dias =>
                expedicion is { } desde ? (desde.AddDays(dias), BaseVigencia.FechaExpedicion) : (Fecha(cargadaEn).AddDays(dias), BaseVigencia.FechaCarga),
            TipoReglaFrescura.MaxDiasDesdeAprobacion when regla.Dias is { } dias => (Fecha(cargadaEn).AddDays(dias), BaseVigencia.FechaCarga),
            TipoReglaFrescura.PeriodoMinimo => (null, BaseVigencia.PeriodoNoCalculado),
            _ => (null, BaseVigencia.SinVencimiento)
        };

        if (vencimientoPropio is { } propio && (capa2.Fecha is null || propio <= capa2.Fecha))
            return new CalculoDeVigencia(propio, BaseVigencia.VencimientoPropio);
        return new CalculoDeVigencia(capa2.Fecha, capa2.Base);
    }

    public static DateOnly? FinDeGracia(DateOnly? vigenteHasta, int diasGracia) => vigenteHasta?.AddDays(Math.Max(0, diasGracia));

    /// <summary>Cuándo se dispara el aviso de una fecha: 13:00 UTC (8:00 en Colombia). Lo usan los documentos y la vigencia del vínculo.</summary>
    public static DateTimeOffset MomentoDeAviso(DateOnly fecha) => new(fecha.ToDateTime(new TimeOnly(13, 0)), TimeSpan.Zero);

    /// <summary>Cuándo vence lo que terminó su gracia el día <paramref name="finDeGracia"/>: 05:00 UTC del día siguiente.</summary>
    public static DateTimeOffset MomentoDeVencimiento(DateOnly finDeGracia) => new(finDeGracia.AddDays(1).ToDateTime(new TimeOnly(5, 0)), TimeSpan.Zero);

    /// <summary>Días de aviso convertidos en fechas (sin repetir, de la más próxima a la más lejana del vencimiento).</summary>
    public static IReadOnlyList<DateOnly> FechasDeAviso(DateOnly vigenteHasta, IEnumerable<int> diasAviso) =>
        [.. diasAviso.Where(d => d > 0).Distinct().Select(d => vigenteHasta.AddDays(-d)).Order()];

    public static EstadoVigencia Estado(DateOnly? vigenteHasta, IReadOnlyCollection<int> diasAviso, int diasGracia, DateOnly hoy, bool marcadaVencida = false)
    {
        if (vigenteHasta is not { } hasta)
            return EstadoVigencia.SinVencimiento;
        if (marcadaVencida || hoy > FinDeGracia(hasta, diasGracia))
            return EstadoVigencia.Vencida;
        if (hoy > hasta)
            return EstadoVigencia.EnGracia;
        var aviso = diasAviso.Count == 0 ? 0 : Math.Max(0, diasAviso.Max());
        return hoy >= hasta.AddDays(-aviso) ? EstadoVigencia.PorVencer : EstadoVigencia.Vigente;
    }
}
