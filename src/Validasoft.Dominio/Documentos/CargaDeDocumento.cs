using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Documentos;

public enum EstadoCarga
{
    Pendiente,
    Confirmada,
    Duplicada,
    Reemplazada
}

/// <summary>
/// Subida en curso (paso 1 de la carga, esquema <c>boveda</c>): reserva el número de la próxima versión del documento y la
/// clave temporal <c>cargas/{id}</c> del bucket. Hay a lo sumo una pendiente por documento: iniciar otra la reemplaza
/// (BO-5), así los números de versión no tienen huecos. No es un evento del documento: la versión nace al confirmar.
/// </summary>
public sealed class CargaDeDocumento
{
    public Guid Id { get; set; }

    public Guid DocumentoId { get; set; }

    public Guid TerceroId { get; set; }

    public int Numero { get; set; }

    public string Bucket { get; set; } = string.Empty;

    public string ClaveTemporal { get; set; } = string.Empty;

    public string NombreOriginal { get; set; } = string.Empty;

    public long TamanoDeclarado { get; set; }

    public string MimeDeclarado { get; set; } = string.Empty;

    public string? Sha256Declarado { get; set; }

    public Guid? ContextoVinculoId { get; set; }

    public EstadoCarga Estado { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset Expira { get; set; }

    /// <summary>Límites del tipo al iniciar (TD-1): se vuelven a aplicar al confirmar con el tipo real, el tamaño y las páginas. Nulo ⇒ globales.</summary>
    public ReglasDeArchivo? Reglas { get; set; }

    /// <summary>Datos declarados ya validados (tipos <c>ArchivoYFormulario</c>, TD-4); viajan a la versión al confirmar.</summary>
    public List<DatoDeclarado>? Datos { get; set; }

    public string? HashDatos { get; set; }

    [System.Text.Json.Serialization.JsonIgnore]
    public DatosVerificados? DatosVerificados => Datos is null || HashDatos is null ? null : new DatosVerificados(Datos, HashDatos);

    /// <summary>Nombre que se guarda: sin rutas ni caracteres de control (el original va solo en metadatos, nunca en la clave).</summary>
    public static string NormalizarNombre(string? nombre)
    {
        var limpio = Path.GetFileName((nombre ?? string.Empty).Replace('\\', '/')).Trim();
        limpio = new string([.. limpio.Where(c => !char.IsControl(c))]);
        if (limpio.Length == 0)
            throw ReglaDeNegocioException.Validacion("nombre-archivo-requerido", "Indica el nombre del archivo.");
        return limpio.Length > 200 ? limpio[..200] : limpio;
    }

    /// <summary>SHA-256 declarado en hexadecimal (64 caracteres, minúsculas); nulo si no llegó.</summary>
    public static string? NormalizarSha256(string? sha256)
    {
        if (string.IsNullOrWhiteSpace(sha256))
            return null;
        var valor = sha256.Trim().ToLowerInvariant();
        return valor.Length == 64 && valor.All(Uri.IsHexDigit)
            ? valor
            : throw ReglaDeNegocioException.Validacion("sha256-invalido", "El SHA-256 debe ser hexadecimal de 64 caracteres.");
    }
}
