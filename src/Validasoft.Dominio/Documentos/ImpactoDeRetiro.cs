namespace Validasoft.Dominio.Documentos;

/// <summary>Lo que una empresa hizo con la versión que el tercero piensa retirar (solo esto se le cuenta al tercero, RT-1).</summary>
public enum SituacionAnteRetiro
{
    /// <summary>La recibió (la compartieron con su vínculo).</summary>
    Recibio,

    /// <summary>La aprobó en su último expediente aprobado (o en el abierto).</summary>
    Aprobo,

    /// <summary>Su expediente abierto la tiene y aún no la aprueba.</summary>
    Revisa
}

/// <summary>Qué le pasa a la empresa cuando el tercero retira la versión.</summary>
public enum EfectoDeRetiro
{
    /// <summary>Nada: la versión retirada no era la vigente, o ya no hay con qué moverla.</summary>
    SinCambios,

    /// <summary>La recibió: vuelve a tener la versión anterior, que pasa a ser la vigente.</summary>
    VuelveALaAnterior,

    /// <summary>La aprobó: se le abre una revalidación de la versión que queda vigente.</summary>
    Revalidar,

    /// <summary>La revisa: su expediente cambia a la versión que queda vigente y vuelve a revisarla.</summary>
    VolverARevisar,

    /// <summary>No queda otra versión vigente: se queda sin el documento hasta que el tercero suba uno nuevo.</summary>
    SinVersion
}

/// <summary>
/// Hechos de una empresa frente a la versión que se retira: <paramref name="EsLaVigente"/> y <paramref name="VersionQueQueda"/> son
/// del documento; <paramref name="Recibio"/>, <paramref name="Aprobo"/> y <paramref name="Revisa"/> de la empresa;
/// <paramref name="PuedeRevalidar"/>: su vínculo está activo con una aprobación previa y sin expediente abierto (las condiciones con
/// que la revalidación se abre, RV-1).
/// </summary>
public sealed record HechosDeRetiro(bool EsLaVigente, int? VersionQueQueda, bool Recibio, bool Aprobo, bool Revisa, bool PuedeRevalidar);

/// <summary>Resultado de <see cref="ImpactoDeRetiro.Calcular"/>: lo que la empresa hizo y qué le pasará.</summary>
public sealed record ImpactoParaEmpresa(SituacionAnteRetiro Situacion, EfectoDeRetiro Efecto);

public static class ImpactoDeRetiro
{
    /// <summary>
    /// La situación (la más avanzada: revisa, aprobó, recibió) y el efecto del retiro para una empresa; nulo si no la tocó y no
    /// hay nada que contarle al tercero. Coincide con lo que hace el sistema al retirar: si la versión era la vigente, el documento
    /// vuelve a la anterior y cada vínculo sincroniza su expediente abierto o abre la revalidación de lo que había aprobado.
    /// </summary>
    public static ImpactoParaEmpresa? Calcular(HechosDeRetiro h)
    {
        var situacion = h.Revisa ? SituacionAnteRetiro.Revisa
            : h.Aprobo ? SituacionAnteRetiro.Aprobo
            : h.Recibio ? SituacionAnteRetiro.Recibio
            : (SituacionAnteRetiro?)null;
        if (situacion is not { } encontrada)
            return null;

        var efecto = !h.EsLaVigente ? EfectoDeRetiro.SinCambios
            : h.VersionQueQueda is null ? EfectoDeRetiro.SinVersion
            : h.Revisa ? EfectoDeRetiro.VolverARevisar
            : h.Aprobo ? h.PuedeRevalidar ? EfectoDeRetiro.Revalidar : EfectoDeRetiro.SinCambios
            : EfectoDeRetiro.VuelveALaAnterior;
        return new ImpactoParaEmpresa(encontrada, efecto);
    }
}
