using System.Collections.Concurrent;
using System.Net;
using Amazon.Runtime;
using Amazon.S3;
using Amazon.S3.Model;
using Amazon.S3.Util;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;

namespace Validasoft.Infraestructura.Almacenamiento;

/// <summary>
/// Clientes S3 (<c>Validasoft:Almacen</c>): uno interno para leer, copiar y borrar, y otro con el endpoint público para
/// firmar las URLs que usa el navegador (en Compose el API ve <c>minio:9000</c> y el navegador <c>localhost:9000</c>). Se
/// crean al primer uso: sin almacenamiento configurado solo fallan las operaciones de documentos. Registrada por tipo
/// (Wolverine no admite fábricas lambda).
/// </summary>
public sealed class FabricaClienteS3(IConfiguration configuracion) : IDisposable
{
    private readonly Lazy<AmazonS3Client> _interno = new(() => Crear(configuracion, publico: false));
    private readonly Lazy<AmazonS3Client> _publico = new(() => Crear(configuracion, publico: true));

    public IAmazonS3 Interno => _interno.Value;

    public IAmazonS3 Publico => _publico.Value;

    /// <summary>Las URLs prefirmadas usan el esquema del endpoint público (MinIO local va por HTTP).</summary>
    public Protocol ProtocoloPublico =>
        Endpoint(configuracion, publico: true).StartsWith("http://", StringComparison.OrdinalIgnoreCase) ? Protocol.HTTP : Protocol.HTTPS;

    private static string Endpoint(IConfiguration configuracion, bool publico)
    {
        var interno = configuracion["Validasoft:Almacen:Endpoint"];
        var externo = configuracion["Validasoft:Almacen:EndpointPublico"];
        var valor = publico && !string.IsNullOrWhiteSpace(externo) ? externo : interno;
        return string.IsNullOrWhiteSpace(valor)
            ? throw new InvalidOperationException("Falta Validasoft:Almacen:Endpoint (MinIO o S3) para la bóveda de documentos.")
            : valor.TrimEnd('/');
    }

    private static AmazonS3Client Crear(IConfiguration configuracion, bool publico)
    {
        var config = new AmazonS3Config
        {
            ServiceURL = Endpoint(configuracion, publico),
            ForcePathStyle = true,
            AuthenticationRegion = configuracion["Validasoft:Almacen:Region"] is { Length: > 0 } region ? region : "us-east-1",
            // MinIO y S3 aceptan ambas; los checksums solo cuando la operación los exige evita cabeceras extra en las prefirmadas.
            RequestChecksumCalculation = RequestChecksumCalculation.WHEN_REQUIRED,
            ResponseChecksumValidation = ResponseChecksumValidation.WHEN_REQUIRED
        };
        var credenciales = new BasicAWSCredentials(
            configuracion["Validasoft:Almacen:AccessKey"] ?? string.Empty,
            configuracion["Validasoft:Almacen:SecretKey"] ?? string.Empty);
        return new AmazonS3Client(credenciales, config);
    }

    public void Dispose()
    {
        if (_interno.IsValueCreated)
            _interno.Value.Dispose();
        if (_publico.IsValueCreated)
            _publico.Value.Dispose();
    }
}

/// <summary>Implementación de <see cref="IAlmacenDeArchivos"/> sobre un servicio compatible con S3 (MinIO, AWS S3).</summary>
public sealed class AlmacenS3(FabricaClienteS3 clientes, TimeProvider reloj) : IAlmacenDeArchivos
{
    private readonly ConcurrentDictionary<string, bool> _buckets = new(StringComparer.Ordinal);

    public async Task<UrlPrefirmada> UrlDeSubidaAsync(string bucket, string clave, string tipoContenido, TimeSpan vigencia, CancellationToken ct)
    {
        var expira = reloj.GetUtcNow() + vigencia;
        var url = await clientes.Publico.GetPreSignedURLAsync(new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = clave,
            Verb = HttpVerb.PUT,
            ContentType = tipoContenido,
            Expires = expira.UtcDateTime,
            Protocol = clientes.ProtocoloPublico
        });
        return new UrlPrefirmada("PUT", url, new Dictionary<string, string> { ["Content-Type"] = tipoContenido }, expira);
    }

    public async Task<UrlPrefirmada> UrlDeDescargaAsync(string bucket, string clave, string nombreArchivo, string tipoContenido, TimeSpan vigencia,
        CancellationToken ct)
    {
        var expira = reloj.GetUtcNow() + vigencia;
        var solicitud = new GetPreSignedUrlRequest
        {
            BucketName = bucket,
            Key = clave,
            Verb = HttpVerb.GET,
            Expires = expira.UtcDateTime,
            Protocol = clientes.ProtocoloPublico
        };
        // Content-Disposition fijo (docs/07 §6.2): siempre descarga, con el nombre original codificado (RFC 6266).
        solicitud.ResponseHeaderOverrides.ContentDisposition =
            $"attachment; filename=\"{NombreAscii(nombreArchivo)}\"; filename*=UTF-8''{Uri.EscapeDataString(nombreArchivo)}";
        solicitud.ResponseHeaderOverrides.ContentType = tipoContenido;
        var url = await clientes.Publico.GetPreSignedURLAsync(solicitud);
        return new UrlPrefirmada("GET", url, new Dictionary<string, string>(), expira);
    }

    public async Task<ObjetoAlmacenado?> ConsultarAsync(string bucket, string clave, CancellationToken ct)
    {
        try
        {
            var metadatos = await clientes.Interno.GetObjectMetadataAsync(bucket, clave, ct);
            return new ObjetoAlmacenado(metadatos.ContentLength, metadatos.Headers.ContentType);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            return null;
        }
    }

    public async Task<bool> ExisteAsync(string bucket, string clave, CancellationToken ct) => await ConsultarAsync(bucket, clave, ct) is not null;

    public async Task<Stream> AbrirLecturaAsync(string bucket, string clave, CancellationToken ct)
    {
        var respuesta = await clientes.Interno.GetObjectAsync(bucket, clave, ct);
        return new FlujoDeObjeto(respuesta);
    }

    public async Task CopiarAsync(string bucketOrigen, string claveOrigen, string bucketDestino, string claveDestino, string tipoContenido,
        IReadOnlyDictionary<string, string> metadatos, CancellationToken ct)
    {
        var solicitud = new CopyObjectRequest
        {
            SourceBucket = bucketOrigen,
            SourceKey = claveOrigen,
            DestinationBucket = bucketDestino,
            DestinationKey = claveDestino,
            ContentType = tipoContenido,
            MetadataDirective = S3MetadataDirective.REPLACE
        };
        foreach (var (clave, valor) in metadatos)
            solicitud.Metadata.Add(clave, valor);
        await clientes.Interno.CopyObjectAsync(solicitud, ct);
    }

    public async Task EliminarAsync(string bucket, string clave, CancellationToken ct)
    {
        try
        {
            await clientes.Interno.DeleteObjectAsync(bucket, clave, ct);
        }
        catch (AmazonS3Exception e) when (e.StatusCode == HttpStatusCode.NotFound)
        {
            // Ya no existe: eliminar es idempotente.
        }
    }

    public async Task AsegurarBucketAsync(string bucket, CancellationToken ct)
    {
        if (_buckets.ContainsKey(bucket))
            return;

        if (!await AmazonS3Util.DoesS3BucketExistV2Async(clientes.Interno, bucket))
        {
            try
            {
                await clientes.Interno.PutBucketAsync(new PutBucketRequest { BucketName = bucket }, ct);
            }
            catch (AmazonS3Exception e) when (e.ErrorCode is "BucketAlreadyOwnedByYou" or "BucketAlreadyExists")
            {
                // Otra petición lo creó a la vez.
            }
        }

        _buckets[bucket] = true;
    }

    /// <summary>Nombre para <c>filename=</c>: ASCII imprimible sin comillas ni barras (el nombre real va en <c>filename*</c>).</summary>
    private static string NombreAscii(string nombre)
    {
        var limpio = new string([.. nombre.Select(c => c is >= ' ' and <= '~' and not '"' and not '\\' and not '/' ? c : '_')]);
        return limpio.Length == 0 ? "documento" : limpio;
    }

    /// <summary>Flujo del objeto que libera la respuesta HTTP de S3 al cerrarse.</summary>
    private sealed class FlujoDeObjeto(GetObjectResponse respuesta) : Stream
    {
        private readonly Stream _interno = respuesta.ResponseStream;

        public override bool CanRead => true;

        public override bool CanSeek => false;

        public override bool CanWrite => false;

        public override long Length => throw new NotSupportedException();

        public override long Position
        {
            get => throw new NotSupportedException();
            set => throw new NotSupportedException();
        }

        public override int Read(byte[] buffer, int offset, int count) => _interno.Read(buffer, offset, count);

        public override ValueTask<int> ReadAsync(Memory<byte> buffer, CancellationToken cancellationToken = default) =>
            _interno.ReadAsync(buffer, cancellationToken);

        public override Task<int> ReadAsync(byte[] buffer, int offset, int count, CancellationToken cancellationToken) =>
            _interno.ReadAsync(buffer, offset, count, cancellationToken);

        public override void Flush()
        {
        }

        public override long Seek(long offset, SeekOrigin origin) => throw new NotSupportedException();

        public override void SetLength(long value) => throw new NotSupportedException();

        public override void Write(byte[] buffer, int offset, int count) => throw new NotSupportedException();

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _interno.Dispose();
                respuesta.Dispose();
            }

            base.Dispose(disposing);
        }
    }
}

/// <summary>
/// Crea el bucket global <c>boveda</c> al arrancar cuando <c>Validasoft:Almacen:CrearBucketsAlIniciar</c> está activo
/// (desarrollo y pruebas). Los buckets <c>tenant-{id}</c> se crean con la primera carga <c>PorVinculo</c>. Si el
/// almacenamiento no responde, se registra y el API arranca igual.
/// </summary>
public sealed class InicializadorDeAlmacen(IConfiguration configuracion, IAlmacenDeArchivos almacen, ILogger<InicializadorDeAlmacen> logger)
    : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!configuracion.GetValue("Validasoft:Almacen:CrearBucketsAlIniciar", false))
            return;

        try
        {
            await almacen.AsegurarBucketAsync(ClavesDeAlmacen.BucketBoveda, cancellationToken);
        }
        catch (Exception e) when (e is AmazonServiceException or HttpRequestException or InvalidOperationException)
        {
            logger.LogWarning(e, "No se pudo crear el bucket {Bucket}; la bóveda no funcionará hasta que el almacenamiento responda.",
                ClavesDeAlmacen.BucketBoveda);
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
