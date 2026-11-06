using System.Security.Cryptography;
using System.Text;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Consentimientos;

public enum AmbitoTextoLegal
{
    Plataforma,
    Empresa
}

/// <summary>Textos que el tercero acepta al aceptar una invitación (docs/07 §2.2 y §8).</summary>
public enum TipoTextoLegal
{
    /// <summary>Aviso de tratamiento de la plataforma (incluye IA externa y transferencia internacional, P-6-5). Una vez por versión.</summary>
    AvisoPlataforma,

    /// <summary>Autorización de tratamiento de datos de la empresa.</summary>
    AutorizacionEmpresa,

    /// <summary>Términos y condiciones de la empresa.</summary>
    TerminosEmpresa
}

/// <summary>
/// Versión inmutable de un texto legal (global, esquema <c>plataforma</c>). Publicar crea una versión nueva; la
/// vigente es la de mayor <see cref="Version"/> de su <see cref="Serie"/>. El <see cref="Hash"/> (SHA-256 del
/// contenido UTF-8) queda en cada consentimiento como evidencia de qué se aceptó exactamente.
/// </summary>
public sealed class TextoLegal
{
    public const int LongitudMaxima = 100_000;

    public Guid Id { get; set; }

    public AmbitoTextoLegal Ambito { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? EmpresaId { get; set; }

    public TipoTextoLegal Tipo { get; set; }

    /// <summary>Clave plana de la serie (<c>{Tipo}|{empresaId|plataforma}</c>) para el índice único con la versión.</summary>
    public string Serie { get; set; } = string.Empty;

    public int Version { get; set; }

    public string Contenido { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public DateTimeOffset PublicadoEn { get; set; }

    public string PublicadoPor { get; set; } = string.Empty;

    public static string SerieDe(TipoTextoLegal tipo, Guid? empresaId) =>
        $"{tipo}|{(empresaId is null ? "plataforma" : empresaId.Value.ToString("N"))}";

    public static string HashDe(string contenido) =>
        Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(contenido)));

    /// <summary>
    /// Nueva versión de la serie: <paramref name="vigente"/> es la versión actual (o nula si es la primera). El
    /// aviso es de plataforma; la autorización y los términos son de una empresa de un tenant.
    /// </summary>
    public static TextoLegal Publicar(TipoTextoLegal tipo, Guid? tenantId, Guid? empresaId, string contenido, TextoLegal? vigente,
        string publicadoPor, DateTimeOffset ahora)
    {
        var deEmpresa = tipo != TipoTextoLegal.AvisoPlataforma;
        if (deEmpresa && (tenantId is null || empresaId is null))
            throw ReglaDeNegocioException.Validacion("empresa-requerida", "La autorización y los términos pertenecen a una empresa.");
        if (!deEmpresa && (tenantId is not null || empresaId is not null))
            throw ReglaDeNegocioException.Validacion("ambito-invalido", "El aviso de la plataforma no pertenece a ningún tenant.");

        var texto = (contenido ?? string.Empty).Trim();
        if (texto.Length == 0)
            throw ReglaDeNegocioException.Validacion("contenido-requerido", "El texto legal no puede estar vacío.");
        if (texto.Length > LongitudMaxima)
            throw ReglaDeNegocioException.Validacion("contenido-demasiado-largo", $"El texto legal admite hasta {LongitudMaxima} caracteres.");

        var serie = SerieDe(tipo, empresaId);
        if (vigente is not null && vigente.Serie != serie)
            throw new InvalidOperationException("La versión vigente no es de la misma serie.");

        return new TextoLegal
        {
            Id = Guid.CreateVersion7(),
            Ambito = deEmpresa ? AmbitoTextoLegal.Empresa : AmbitoTextoLegal.Plataforma,
            TenantId = tenantId,
            EmpresaId = empresaId,
            Tipo = tipo,
            Serie = serie,
            Version = (vigente?.Version ?? 0) + 1,
            Contenido = texto,
            Hash = HashDe(texto),
            PublicadoEn = ahora,
            PublicadoPor = string.IsNullOrWhiteSpace(publicadoPor) ? "desconocido" : publicadoPor
        };
    }
}
