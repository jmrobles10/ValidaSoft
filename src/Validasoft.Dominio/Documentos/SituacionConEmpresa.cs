namespace Validasoft.Dominio.Documentos;

/// <summary>
/// Qué tiene una empresa de un documento del tercero y qué falta (RT-5): una sola lectura para la tarjeta de la bóveda, sin
/// contradecir la versión vigente del tercero. <see cref="SituacionConEmpresa"/> la nombra; la versión que importa y, si hay,
/// «la otra» (la que aprobó, la que retiraste o la vigente que no recibe) las da <see cref="SituacionDelDocumento"/>.
/// </summary>
public enum SituacionConEmpresa
{
    /// <summary>No tiene ninguna versión de este documento: no lo exige o el tercero no aceptó su invitación.</summary>
    NoRecibe,

    /// <summary>Tenía una versión que el tercero retiró y ya no hay vigente: falta subir una nueva.</summary>
    SinVigente,

    /// <summary>Tiene la versión vigente y aún no la ha aprobado ni la está revisando.</summary>
    TieneVigente,

    /// <summary>Aprobó la versión vigente.</summary>
    AproboVigente,

    /// <summary>Revisa la versión vigente en un expediente abierto.</summary>
    Revisando,

    /// <summary>Observó la versión vigente: el tercero debe corregirla.</summary>
    Observada,

    /// <summary>Aprobó una versión que el tercero retiró y revisa la vigente en una revalidación abierta.</summary>
    RevalidaRetirada,

    /// <summary>Aprobó otra versión y revisa la vigente en una revalidación abierta.</summary>
    Revalida,

    /// <summary>Aprobó una versión que el tercero retiró y tiene la vigente: la revalidación está por abrirse.</summary>
    PorRevalidar,

    /// <summary>Tiene una versión que ya no es la vigente y no recibe la vigente (hoy no se la exige).</summary>
    TieneOtra
}

/// <summary>
/// Hechos de un documento con una empresa. <see cref="Vigente"/>: la versión vigente del tercero; <see cref="Recibida"/>: la
/// versión más reciente que la empresa recibió y no está retirada; <see cref="RetiradaRecibida"/>: la más reciente que recibió y
/// el tercero retiró; <see cref="Aprobada"/>: la que aprobó (en su último expediente aprobado o en el abierto);
/// <see cref="EnRevision"/>: la que revisa hoy en un expediente abierto (<see cref="RevisionObservada"/> si ya la observó).
/// </summary>
public sealed record HechosConEmpresa(int? Vigente, int? Recibida, int? RetiradaRecibida, int? Aprobada, bool AprobadaRetirada, int? EnRevision,
    bool RevisionObservada);

/// <summary>Resultado de <see cref="SituacionDelDocumento.Calcular"/>: la situación y la «otra» versión de su texto, si la lleva.</summary>
public sealed record LecturaDeSituacion(SituacionConEmpresa Situacion, int? Otra);

public static class SituacionDelDocumento
{
    /// <summary>
    /// Elige la situación. Siempre habla de la versión que la empresa tiene (<see cref="HechosConEmpresa.Recibida"/>, que es la
    /// vigente salvo en <c>TieneOtra</c>) y, cuando hay una segunda versión de por medio, la nombra en <c>Otra</c>: la aprobada que
    /// el tercero retiró o que ya no es la vigente, la retirada sin reemplazo o la vigente que la empresa no recibe.
    /// </summary>
    public static LecturaDeSituacion Calcular(HechosConEmpresa h)
    {
        if (h.Vigente is not { } vigente)
            return h.Recibida is null && h.RetiradaRecibida is { } retirada
                ? new LecturaDeSituacion(SituacionConEmpresa.SinVigente, retirada)
                : new LecturaDeSituacion(SituacionConEmpresa.NoRecibe, null);

        if (h.Recibida is not { } recibida)
            return new LecturaDeSituacion(SituacionConEmpresa.NoRecibe, null);
        if (recibida != vigente)
            return new LecturaDeSituacion(SituacionConEmpresa.TieneOtra, vigente);

        if (h.EnRevision == vigente)
        {
            if (h.RevisionObservada)
                return new LecturaDeSituacion(SituacionConEmpresa.Observada, null);
            if (h.Aprobada is { } antes && antes != vigente)
                return new LecturaDeSituacion(h.AprobadaRetirada ? SituacionConEmpresa.RevalidaRetirada : SituacionConEmpresa.Revalida, antes);
            return new LecturaDeSituacion(SituacionConEmpresa.Revisando, null);
        }

        if (h.Aprobada == vigente)
            return new LecturaDeSituacion(SituacionConEmpresa.AproboVigente, null);
        if (h.Aprobada is { } retiradaAprobada && h.AprobadaRetirada)
            return new LecturaDeSituacion(SituacionConEmpresa.PorRevalidar, retiradaAprobada);
        return new LecturaDeSituacion(SituacionConEmpresa.TieneVigente, null);
    }
}
