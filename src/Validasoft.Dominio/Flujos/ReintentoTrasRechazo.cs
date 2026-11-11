using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Flujos;

/// <summary>
/// Reintento del tercero tras un rechazo (DS-4): si la empresa lo <see cref="Permitido">permite</see>, el tercero ve el rechazo
/// con sus motivos en su portal y puede «Volver a postularme» (se abre un expediente inicial nuevo) una vez pasados
/// <see cref="DiasEspera"/> días desde el rechazo (0 = de inmediato). Por defecto no se permite: el rechazo es terminal para
/// el tercero y la empresa decide si reabre desde la web. La empresa puede abrir un expediente nuevo en cualquier momento.
/// </summary>
public sealed record ReintentoTrasRechazo(bool Permitido, int DiasEspera)
{
    public const int DiasEsperaMaximos = 365;

    public static readonly ReintentoTrasRechazo PorDefecto = new(false, 0);

    /// <summary>0–365 días de espera (<c>reintento-invalido</c>). Sin permiso no hay espera.</summary>
    public ReintentoTrasRechazo Validar()
    {
        if (DiasEspera is < 0 or > DiasEsperaMaximos)
            throw ReglaDeNegocioException.Validacion("reintento-invalido",
                $"La espera para volver a postularse va de 0 a {DiasEsperaMaximos} días.");
        return Permitido ? this : PorDefecto;
    }

    /// <summary>Desde cuándo puede volver a postularse un tercero rechazado en <paramref name="rechazadoEn"/>; nulo si no se permite.</summary>
    public DateTimeOffset? PuedeDesde(DateTimeOffset rechazadoEn) => Permitido ? rechazadoEn.AddDays(DiasEspera) : null;
}
