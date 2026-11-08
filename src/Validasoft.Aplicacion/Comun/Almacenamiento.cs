using Microsoft.Extensions.Configuration;

namespace Validasoft.Aplicacion.Comun;

/// <summary>URL prefirmada para que el cliente suba o descargue un binario directamente del almacenamiento.</summary>
public sealed record UrlPrefirmada(string Metodo, string Url, IReadOnlyDictionary<string, string> Cabeceras, DateTimeOffset Expira);

/// <summary>Metadatos de un objeto almacenado.</summary>
public sealed record ObjetoAlmacenado(long Tamano, string? TipoContenido);

/// <summary>
/// Almacenamiento de binarios compatible con S3 (MinIO en desarrollo y on-premise, S3 en la nube; docs/04 §6). El API
/// nunca proxea bytes del cliente: entrega URLs prefirmadas y solo lee los objetos para verificarlos al confirmar.
/// </summary>
public interface IAlmacenDeArchivos
{
    /// <summary>URL prefirmada <c>PUT</c> que fija el <c>Content-Type</c> (el cliente debe enviar las cabeceras devueltas).</summary>
    Task<UrlPrefirmada> UrlDeSubidaAsync(string bucket, string clave, string tipoContenido, TimeSpan vigencia, CancellationToken ct);

    /// <summary>URL prefirmada <c>GET</c> con <c>Content-Disposition</c> de descarga y el nombre original.</summary>
    Task<UrlPrefirmada> UrlDeDescargaAsync(string bucket, string clave, string nombreArchivo, string tipoContenido, TimeSpan vigencia,
        CancellationToken ct);

    /// <summary>Tamaño y tipo del objeto; nulo si no existe.</summary>
    Task<ObjetoAlmacenado?> ConsultarAsync(string bucket, string clave, CancellationToken ct);

    Task<bool> ExisteAsync(string bucket, string clave, CancellationToken ct);

    /// <summary>Lectura en streaming del objeto (el llamador cierra el flujo).</summary>
    Task<Stream> AbrirLecturaAsync(string bucket, string clave, CancellationToken ct);

    /// <summary>Copia en el servidor (sin pasar por el API) reemplazando tipo de contenido y metadatos.</summary>
    Task CopiarAsync(string bucketOrigen, string claveOrigen, string bucketDestino, string claveDestino, string tipoContenido,
        IReadOnlyDictionary<string, string> metadatos, CancellationToken ct);

    /// <summary>Elimina el objeto si existe (idempotente).</summary>
    Task EliminarAsync(string bucket, string clave, CancellationToken ct);

    /// <summary>Crea el bucket si no existe (los de tenant nacen con su primera carga <c>PorVinculo</c>).</summary>
    Task AsegurarBucketAsync(string bucket, CancellationToken ct);
}

/// <summary>Buckets y claves de docs/02 §10.2: content-addressed por SHA-256; el nombre original va en metadatos, nunca en la clave.</summary>
public static class ClavesDeAlmacen
{
    public const string BucketBoveda = "boveda";

    public static string BucketDeTenant(Guid tenantId) => $"tenant-{tenantId:N}";

    public static string Global(Guid terceroId, string sha256) => $"terceros/{terceroId:N}/{sha256}";

    public static string PorVinculo(Guid empresaId, Guid vinculoId, string sha256) => $"empresas/{empresaId:N}/vinculos/{vinculoId:N}/{sha256}";

    /// <summary>Zona temporal de la subida (paso 1); al confirmar se copia a la clave definitiva y se borra.</summary>
    public static string Carga(Guid cargaId) => $"cargas/{cargaId:N}";

    /// <summary>Plantilla de un tipo de documento (TD-5): bucket del tenant, content-addressed por tipo.</summary>
    public static string Plantilla(Guid tipoDocumentoTenantId, string sha256) => $"plantillas/{tipoDocumentoTenantId:N}/{sha256}";

    /// <summary>Versiones devueltas: nunca se comparten (docs/07 §6.1).</summary>
    public static string Cuarentena(Guid documentoId, int numero) => $"cuarentena/{documentoId:N}/v{numero}";
}

/// <summary>
/// Límites de la carga (<c>Validasoft:Boveda</c>): 20 MB y 50 páginas por defecto (P-16-2), vigencia de las URLs de
/// subida (15 min) y descarga (5 min) y si se exige antivirus para promover una versión (T-11; sin ClamAV todavía).
/// </summary>
public sealed class ReglasDeCarga(IConfiguration configuracion)
{
    public long TamanoMaximoBytes { get; } = Math.Max(1, configuracion.GetValue("Validasoft:Boveda:TamanoMaximoBytes", 20L * 1024 * 1024));

    public int MaximoPaginas { get; } = Math.Max(1, configuracion.GetValue("Validasoft:Boveda:MaximoPaginas", 50));

    public TimeSpan VigenciaSubida { get; } =
        TimeSpan.FromMinutes(Math.Clamp(configuracion.GetValue("Validasoft:Boveda:MinutosSubida", 15), 1, 15));

    public TimeSpan VigenciaDescarga { get; } =
        TimeSpan.FromMinutes(Math.Clamp(configuracion.GetValue("Validasoft:Boveda:MinutosDescarga", 5), 1, 15));

    public bool ExigirAntivirus { get; } = configuracion.GetValue("Validasoft:Boveda:ExigirAntivirus", false);
}
