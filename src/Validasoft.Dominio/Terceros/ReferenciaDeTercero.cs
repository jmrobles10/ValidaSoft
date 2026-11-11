using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Terceros;

/// <summary>
/// Clave de un tercero en un sistema externo (un ERP, un portal, un CRM): <c>(sistema, clave)</c>. Reemplaza a la antigua
/// «clave del legado» del tercero: lo que un sistema importa o integra se registra igual, sin nombres de sistemas concretos.
/// </summary>
/// <remarks>
/// El tercero es global, pero cada sistema es de un tenant: dos tenants pueden tener un sistema <c>SRM</c> con claves distintas
/// para el mismo tercero. Por eso la referencia vive en el tenant que la registró (documento por tenant, id único por
/// <c>{SISTEMA}|{clave}</c> dentro de él) y solo ese tenant la ve. Las claves de un sistema de origen de la plataforma (el importador)
/// viven en el tenant técnico <c>boveda</c>. Registrar la misma referencia otra vez no hace nada (idempotente); otra clave del mismo
/// sistema para el mismo tercero es <c>referencia-en-conflicto</c>, y una clave que ya identifica a otro tercero,
/// <c>referencia-duplicada</c>.
/// </remarks>
public sealed class ReferenciaDeTercero
{
    public const int MaximoSistema = 40;

    public const int MaximoClave = 200;

    /// <summary><c>{SISTEMA}|{clave}</c>: único dentro del tenant.</summary>
    public string Id { get; set; } = string.Empty;

    public Guid TerceroId { get; set; }

    /// <summary>Código del sistema, en mayúsculas (como el de los sistemas integrados y el de las referencias de un vínculo).</summary>
    public string Sistema { get; set; } = string.Empty;

    public string Clave { get; set; } = string.Empty;

    public DateTimeOffset RegistradaEn { get; set; }

    public static string IdPara(string sistema, string clave) => $"{NormalizarSistema(sistema)}|{clave.Trim()}";

    public static string NormalizarSistema(string sistema) => sistema.Trim().ToUpperInvariant();

    /// <summary>Valida y arma la referencia: sistema y clave obligatorios y acotados.</summary>
    public static ReferenciaDeTercero Crear(Guid terceroId, string? sistema, string? clave, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(sistema) || string.IsNullOrWhiteSpace(clave))
            throw ReglaDeNegocioException.Validacion("referencia-externa-invalida", "La referencia externa necesita sistema y clave.");
        var s = NormalizarSistema(sistema);
        var c = clave.Trim();
        if (s.Length > MaximoSistema || c.Length > MaximoClave || s.Contains('|', StringComparison.Ordinal))
            throw ReglaDeNegocioException.Validacion("referencia-externa-invalida",
                $"El sistema admite hasta {MaximoSistema} caracteres (sin «|») y la clave hasta {MaximoClave}.");
        return new ReferenciaDeTercero { Id = $"{s}|{c}", TerceroId = terceroId, Sistema = s, Clave = c, RegistradaEn = ahora };
    }
}
