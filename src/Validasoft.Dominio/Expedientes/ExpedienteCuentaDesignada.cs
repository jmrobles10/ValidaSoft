using System.Text.Json.Serialization;

namespace Validasoft.Dominio.Expedientes;

// Eventos del stream Expediente-{id} (CB-4). Actor, sistema y correlación viajan en los metadatos.

/// <summary>
/// El tipo de vínculo exige una cuenta bancaria designada y aprobada para aprobar el expediente (<c>TipoVinculo.ExigeCuentaDesignada</c>),
/// copiado al abrirlo. <see cref="Aprobada"/>: el vínculo ya tenía su cuenta designada aprobada en ese momento.
/// </summary>
public sealed record CuentaDesignadaRequerida(bool Aprobada, DateTimeOffset Fecha);

/// <summary>La empresa aprobó la cuenta designada del vínculo mientras este expediente esperaba (o podía esperar) por ella.</summary>
public sealed record CuentaDesignadaConfirmada(DateTimeOffset Fecha);

/// <summary>
/// Cuenta designada y cierre del expediente (CB-4). Si el tipo de vínculo la exige, el expediente no se aprueba mientras la cuenta
/// designada del vínculo no esté aprobada (<see cref="CuentaDesignadaSatisfecha"/>), aunque todas las áreas hayan aprobado: queda
/// <c>EnRevision</c> con todas sus etapas aprobadas (<see cref="EsperandoCuentaDesignada"/>), igual que con una verificación de
/// cumplimiento pendiente, y se cierra en cuanto la empresa aprueba la cuenta (<see cref="ConfirmarCuentaDesignada"/>). Rechazar una
/// propuesta de cuenta no lo rechaza: el expediente sigue esperando a que el tercero designe otra.
/// </summary>
public sealed partial class Expediente
{
    /// <summary>El tipo de vínculo exige una cuenta designada aprobada para cerrar (copiado al abrir; falso en los expedientes anteriores a CB-4).</summary>
    public bool ExigeCuentaDesignada { get; set; }

    /// <summary>La cuenta designada del vínculo ya está aprobada (solo tiene sentido con <see cref="ExigeCuentaDesignada"/>).</summary>
    public bool CuentaDesignadaAprobada { get; set; }

    /// <summary>No se exige cuenta o ya está aprobada: no frena el cierre.</summary>
    [JsonIgnore]
    public bool CuentaDesignadaSatisfecha => !ExigeCuentaDesignada || CuentaDesignadaAprobada;

    /// <summary>Todas las áreas aprobaron y lo que impide cerrar es la cuenta designada del vínculo (puede faltar también una verificación).</summary>
    [JsonIgnore]
    public bool EsperandoCuentaDesignada => EsperandoVerificaciones && !CuentaDesignadaSatisfecha;

    /// <summary>
    /// Todas las áreas aprobaron y <b>solo</b> falta la cuenta designada: las verificaciones bloqueantes, si hay, ya están satisfechas.
    /// Con esto la pantalla de la empresa no dice «falta cumplimiento» cuando lo que falta es la cuenta.
    /// </summary>
    [JsonIgnore]
    public bool EsperandoSoloLaCuentaDesignada =>
        EsperandoCuentaDesignada && Verificaciones.Where(v => v.Bloqueante).All(v => v.Estado == EstadoVerificacion.Satisfecha);

    /// <summary>
    /// La empresa aprobó la cuenta designada del vínculo. Si este expediente la exigía y aún no la tenía, lo registra y vuelve a
    /// evaluar el cierre: con todas las áreas aprobadas y nada más pendiente se aprueba solo. Sin exigencia, ya confirmada o con el
    /// expediente cerrado no hace nada (nunca falla: la aprobación de la cuenta es del vínculo, no del expediente).
    /// </summary>
    public IReadOnlyList<object> ConfirmarCuentaDesignada(DateTimeOffset ahora) =>
        EstaAbierto && ExigeCuentaDesignada && !CuentaDesignadaAprobada
            ? Simular(s => s.Emitir(new CuentaDesignadaConfirmada(ahora)), ahora)
            : [];

    // ---------- Evolución ----------

    public void Apply(CuentaDesignadaRequerida e)
    {
        ExigeCuentaDesignada = true;
        CuentaDesignadaAprobada = e.Aprobada;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(CuentaDesignadaConfirmada e)
    {
        CuentaDesignadaAprobada = true;
        ActualizadoEn = e.Fecha;
    }
}
