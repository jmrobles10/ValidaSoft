namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// El plazo de revisión de un área (SLA, OP-9) está por vencer: ya consumió <see cref="PorcentajeConsumido"/> % del tiempo que tenía
/// para decidir en esta ronda. Va al historial, a la campana y al correo de quienes revisan por el área.
/// </summary>
public sealed record SlaPorVencer(Guid AreaId, DateTimeOffset Vence, int PorcentajeConsumido, DateTimeOffset Fecha);

/// <summary>El área no decidió dentro de su plazo (SLA, OP-9): avisa a quienes revisan por ella y a los administradores de la empresa.</summary>
public sealed record SlaIncumplido(Guid AreaId, DateTimeOffset Vence, DateTimeOffset Fecha);

/// <summary>
/// Alertas del SLA de revisión (OP-9). El plazo de un área corre mientras el expediente está en revisión y ella tiene la decisión pendiente
/// (se reinicia cada vez que le llega: activación, respuesta del tercero, reapertura). Un barrido periódico pide
/// <see cref="RevisarSla"/>: por cada área que cruzó el umbral de aviso emite <see cref="SlaPorVencer"/> y por cada una que pasó su plazo,
/// <see cref="SlaIncumplido"/>, a lo sumo una vez por ronda.
/// </summary>
public sealed partial class Expediente
{
    public const int PorcentajeDeAvisoPorOmision = 80;
    public const int PorcentajeDeAvisoMinimo = 50;
    public const int PorcentajeDeAvisoMaximo = 95;

    /// <summary>Estado del plazo de un área a <paramref name="ahora"/>: nulo si no corre (sin plazo, sin decisión pendiente o fuera de revisión).</summary>
    public static EstadoDeSla? EstadoDelSla(Expediente expediente, AreaDeExpediente area, DateTimeOffset ahora, int porcentajeAviso)
    {
        if (expediente.Estado != EstadoExpediente.EnRevision || area.Estado != EstadoAreaExpediente.Pendiente || area.SlaVence is not { } vence ||
            area.PendienteDesde is not { } desde || expediente.EtapaActiva?.Areas.Contains(area) != true)
            return null;
        if (ahora >= vence)
            return EstadoDeSla.Vencido;
        return ahora >= UmbralDeAviso(desde, vence, porcentajeAviso) ? EstadoDeSla.PorVencer : EstadoDeSla.EnTiempo;
    }

    /// <summary>Momento en que la ronda consumió <paramref name="porcentaje"/> % de su plazo.</summary>
    public static DateTimeOffset UmbralDeAviso(DateTimeOffset desde, DateTimeOffset vence, int porcentaje) =>
        desde + (vence - desde) * (Math.Clamp(porcentaje, 1, 100) / 100d);

    /// <summary>Lo que hay que anotar hoy: avisos y vencimientos que aún no se registraron en la ronda en curso de cada área.</summary>
    public IReadOnlyList<object> RevisarSla(DateTimeOffset ahora, int porcentajeAviso)
    {
        var eventos = new List<object>();
        foreach (var area in EtapaActiva?.Areas ?? [])
        {
            switch (EstadoDelSla(this, area, ahora, porcentajeAviso))
            {
                case EstadoDeSla.Vencido when area.SlaIncumplidoEn is null:
                    eventos.Add(new SlaIncumplido(area.AreaId, area.SlaVence!.Value, ahora));
                    break;
                case EstadoDeSla.PorVencer when area.SlaAvisadoEn is null:
                    eventos.Add(new SlaPorVencer(area.AreaId, area.SlaVence!.Value, Math.Clamp(porcentajeAviso, 1, 100), ahora));
                    break;
            }
        }

        return eventos;
    }

    /// <summary>
    /// Índice del barrido de SLA (OP-9): el primer momento en que puede haber un aviso o un vencimiento por registrar, calculado con el umbral
    /// de aviso más bajo (la configuración de la empresa solo lo atrasa, y el barrido decide con ella). Nulo si ningún área tiene plazo corriendo.
    /// </summary>
    public DateTimeOffset? ProximaRevisionDeSla => ProximoMomentoDeSla(PorcentajeDeAvisoMinimo);

    /// <summary>Instante en que el barrido debe volver a mirar este expediente (el más próximo de sus áreas), o nulo si ninguna corre.</summary>
    public DateTimeOffset? ProximoMomentoDeSla(int porcentajeAviso)
    {
        if (Estado != EstadoExpediente.EnRevision)
            return null;
        DateTimeOffset? proximo = null;
        foreach (var area in EtapaActiva?.Areas.Where(a => a.Estado == EstadoAreaExpediente.Pendiente && a.SlaVence is not null && a.PendienteDesde is not null) ?? [])
        {
            var candidatos = new List<DateTimeOffset>();
            if (area.SlaAvisadoEn is null && area.SlaIncumplidoEn is null)
                candidatos.Add(UmbralDeAviso(area.PendienteDesde!.Value, area.SlaVence!.Value, porcentajeAviso));
            if (area.SlaIncumplidoEn is null)
                candidatos.Add(area.SlaVence!.Value);
            foreach (var c in candidatos)
                proximo = proximo is { } p && p <= c ? p : c;
        }

        return proximo;
    }

    public void Apply(SlaPorVencer e)
    {
        if (BuscarArea(e.AreaId) is { } area)
            area.SlaAvisadoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(SlaIncumplido e)
    {
        if (BuscarArea(e.AreaId) is { } area)
            area.SlaIncumplidoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }
}

/// <summary>Cómo va el plazo de un área: dentro del plazo, ya consumió el umbral de aviso o se pasó.</summary>
public enum EstadoDeSla
{
    EnTiempo,
    PorVencer,
    Vencido
}
