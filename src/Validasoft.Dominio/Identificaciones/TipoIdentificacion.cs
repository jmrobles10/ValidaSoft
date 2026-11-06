namespace Validasoft.Dominio.Identificaciones;

public enum AplicaA
{
    Natural,
    Juridica,
    Ambas
}

/// <summary>Algoritmo del dígito de verificación de un tipo de identificación.</summary>
public enum AlgoritmoDv
{
    Ninguno,
    NitColombia,
    RutChile,
    CuitArgentina
}

/// <summary>Tipo de identificación de un país (docs/05 <c>TipoIdentificacionDto</c>).</summary>
public sealed record TipoIdentificacion(
    string Pais,
    string Codigo,
    string Nombre,
    AplicaA AplicaA,
    AlgoritmoDv AlgoritmoDv,
    string Patron)
{
    public bool TieneDv => AlgoritmoDv != AlgoritmoDv.Ninguno;
}

public sealed record Pais(string Codigo, string Nombre, IReadOnlyList<TipoIdentificacion> TiposIdentificacion);
