using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Identificaciones;

/// <summary>
/// Catálogo de tipos de identificación por país (P-1-1, P-1-4). Los países listados tienen sus tipos
/// propios; cualquier otro país ISO 3166-1 alfa-2 admite <c>PASAPORTE</c> e <c>ID_FISCAL</c>.
/// En H1 vive en código; la tabla <c>plataforma.catalogo_tipo_identificacion</c> lo reemplaza cuando
/// Plataforma necesite editarlo sin desplegar.
/// </summary>
public static class CatalogoIdentificacion
{
    private const string Alfanumerico = "^[A-Z0-9]{3,20}$";

    private static readonly IReadOnlyList<Pais> PaisesConocidos =
    [
        new("CO", "Colombia",
        [
            new("CO", "NIT", "Número de Identificación Tributaria", AplicaA.Ambas, AlgoritmoDv.NitColombia, "^[0-9]{6,15}$"),
            new("CO", "CC", "Cédula de ciudadanía", AplicaA.Natural, AlgoritmoDv.Ninguno, "^[0-9]{3,10}$"),
            new("CO", "CE", "Cédula de extranjería", AplicaA.Natural, AlgoritmoDv.Ninguno, "^[0-9]{3,10}$"),
            new("CO", "TI", "Tarjeta de identidad", AplicaA.Natural, AlgoritmoDv.Ninguno, "^[0-9]{6,11}$"),
            new("CO", "PPT", "Permiso por protección temporal", AplicaA.Natural, AlgoritmoDv.Ninguno, "^[0-9]{4,15}$"),
            new("CO", "PA", "Pasaporte", AplicaA.Natural, AlgoritmoDv.Ninguno, Alfanumerico)
        ]),
        new("CL", "Chile",
        [
            new("CL", "RUT", "Rol Único Tributario", AplicaA.Ambas, AlgoritmoDv.RutChile, "^[0-9]{7,8}$")
        ]),
        new("AR", "Argentina",
        [
            new("AR", "CUIT", "Clave Única de Identificación Tributaria", AplicaA.Ambas, AlgoritmoDv.CuitArgentina, "^[0-9]{10}$"),
            new("AR", "DNI", "Documento Nacional de Identidad", AplicaA.Natural, AlgoritmoDv.Ninguno, "^[0-9]{7,8}$")
        ]),
        new("MX", "México",
        [
            new("MX", "RFC", "Registro Federal de Contribuyentes", AplicaA.Ambas, AlgoritmoDv.Ninguno, "^[A-ZÑ&]{3,4}[0-9]{6}[A-Z0-9]{3}$"),
            new("MX", "CURP", "Clave Única de Registro de Población", AplicaA.Natural, AlgoritmoDv.Ninguno, "^[A-Z]{4}[0-9]{6}[A-Z]{6}[A-Z0-9]{2}$")
        ]),
        new("PE", "Perú",
        [
            new("PE", "RUC", "Registro Único de Contribuyentes", AplicaA.Ambas, AlgoritmoDv.Ninguno, "^[0-9]{11}$"),
            new("PE", "DNI", "Documento Nacional de Identidad", AplicaA.Natural, AlgoritmoDv.Ninguno, "^[0-9]{8}$")
        ]),
        new("EC", "Ecuador",
        [
            new("EC", "RUC", "Registro Único de Contribuyentes", AplicaA.Ambas, AlgoritmoDv.Ninguno, "^[0-9]{13}$"),
            new("EC", "CI", "Cédula de identidad", AplicaA.Natural, AlgoritmoDv.Ninguno, "^[0-9]{10}$")
        ]),
        new("US", "Estados Unidos",
        [
            new("US", "EIN", "Employer Identification Number", AplicaA.Juridica, AlgoritmoDv.Ninguno, "^[0-9]{9}$")
        ])
    ];

    public static IReadOnlyList<Pais> Paises => PaisesConocidos;

    /// <summary>Tipos propios del país más los genéricos (sin duplicar códigos).</summary>
    public static IReadOnlyList<TipoIdentificacion> TiposDe(string pais)
    {
        var codigo = NormalizarPais(pais);
        var propios = PaisesConocidos.FirstOrDefault(p => p.Codigo == codigo)?.TiposIdentificacion ?? [];
        var genericos = new[]
        {
            new TipoIdentificacion(codigo, "PASAPORTE", "Pasaporte", AplicaA.Natural, AlgoritmoDv.Ninguno, Alfanumerico),
            new TipoIdentificacion(codigo, "ID_FISCAL", "Identificación fiscal del país", AplicaA.Ambas, AlgoritmoDv.Ninguno, Alfanumerico)
        };
        return [.. propios, .. genericos.Where(g => propios.All(p => p.Codigo != g.Codigo))];
    }

    public static TipoIdentificacion? Buscar(string pais, string tipo) =>
        TiposDe(pais).FirstOrDefault(t => t.Codigo == (tipo ?? string.Empty).Trim().ToUpperInvariant());

    public static string NormalizarPais(string pais)
    {
        var codigo = (pais ?? string.Empty).Trim().ToUpperInvariant();
        if (!Regex.IsMatch(codigo, "^[A-Z]{2}$"))
            throw ReglaDeNegocioException.Validacion("pais-invalido", $"'{pais}' no es un código de país ISO 3166-1 alfa-2.");
        return codigo;
    }
}
