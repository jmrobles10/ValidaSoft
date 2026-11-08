using System.Security.Cryptography;
using Marten;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;

namespace Validasoft.Aplicacion.Certificados;

/// <summary>Lo que el PDF muestra de una emisión: el contenido canónico más folio, hash, sello y URL de verificación (QR).</summary>
public sealed record DatosDelPdf(ContenidoDeCertificado Contenido, string Folio, string HashContenido, string Sello, string Kid, string UrlVerificacion);

/// <summary>Genera el PDF de un certificado (la implementación, con el QR, vive en infraestructura). Debe ser determinista.</summary>
public interface IGeneradorDePdfDeCertificados
{
    byte[] Generar(DatosDelPdf datos);
}

/// <summary>
/// PDF y vencimiento de los certificados después del commit (CE-5, CE-7). Lo pendiente vive en el propio stream
/// (<see cref="Certificado.PdfPendiente"/>, <see cref="Certificado.VenceDesde"/>), así que no hace falta otra cola: el
/// procesador lo recorre cada pocos segundos y la descarga lo genera en línea si aún falta. Cada operación toma el stream
/// con bloqueo de fila y vuelve a mirar el estado: con varias réplicas o reintentos no se genera dos veces ni se pierde.
/// </summary>
public sealed class ArchivoDeCertificados(IDocumentStore store, IAlmacenDeArchivos almacen, IGeneradorDePdfDeCertificados generador,
    TimeProvider reloj, ILogger<ArchivoDeCertificados> registro)
{
    public const string TipoContenido = "application/pdf";

    /// <summary>Genera, guarda y registra el PDF de la emisión si aún no lo tiene; devuelve el que quedó registrado.</summary>
    public async Task<PdfDeCertificado?> AsegurarPdfAsync(Guid tenantId, Guid certificadoId, int emision, CancellationToken ct)
    {
        await using var sesion = SesionDeSistema(tenantId);
        var stream = await sesion.Events.FetchForExclusiveWriting<Certificado>(certificadoId, ct);
        if (stream.Aggregate?.Buscar(emision) is not { } objetivo)
            return null;
        if (objetivo.Pdf is not null)
            return objetivo.Pdf;

        var certificado = stream.Aggregate;
        var bytes = generador.Generar(new DatosDelPdf(ContenidoDeCertificado.Leer(objetivo.ContenidoCanonico), objetivo.Folio, objetivo.HashContenido,
            objetivo.Sello, objetivo.Kid, objetivo.UrlVerificacion));
        var sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes));
        var bucket = ClavesDeAlmacen.BucketDeTenant(tenantId);
        var clave = ClavesDeAlmacen.Certificado(certificado.EmpresaId, objetivo.Folio, sha256);

        await almacen.AsegurarBucketAsync(bucket, ct);
        await almacen.GuardarAsync(bucket, clave, bytes, TipoContenido,
            new Dictionary<string, string> { ["folio"] = objetivo.Folio, ["hash-contenido"] = objetivo.HashContenido }, ct);

        var ahora = reloj.GetUtcNow();
        if (certificado.RegistrarPdf(emision, bucket, clave, sha256, bytes.LongLength, ahora) is { } registrado)
        {
            stream.AppendOne(registrado);
            await sesion.SaveChangesAsync(ct);
        }

        return new PdfDeCertificado(bucket, clave, sha256, bytes.LongLength, ahora);
    }

    /// <summary>Vence la emisión actual si su vigencia ya pasó (idempotente: con otro estado o aún vigente no hace nada).</summary>
    public async Task<bool> VencerAsync(Guid tenantId, Guid certificadoId, CancellationToken ct)
    {
        await using var sesion = SesionDeSistema(tenantId);
        var stream = await sesion.Events.FetchForExclusiveWriting<Certificado>(certificadoId, ct);
        var ahora = reloj.GetUtcNow();
        if (stream.Aggregate is not { Estado: EstadoCertificado.Vigente or EstadoCertificado.ConPendientes, VenceDesde: { } vence } certificado ||
            ahora < vence)
            return false;

        stream.AppendOne(certificado.Vencer(ahora));
        await sesion.SaveChangesAsync(ct);
        return true;
    }

    /// <summary>Una pasada del procesador: PDFs pendientes y certificados cuya vigencia terminó, en todos los tenants.</summary>
    public async Task ProcesarPendientesAsync(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        List<Certificado> sinPdf, vencidos;
        await using (var consulta = store.QuerySession())
        {
            sinPdf = [.. await consulta.Query<Certificado>().Where(c => c.AnyTenant() && c.PdfPendiente).OrderBy(c => c.ActualizadoEn).Take(50).ToListAsync(ct)];
            vencidos =
            [
                .. await consulta.Query<Certificado>()
                    .Where(c => c.AnyTenant() && (c.Estado == EstadoCertificado.Vigente || c.Estado == EstadoCertificado.ConPendientes) &&
                                c.VenceDesde != null && c.VenceDesde <= ahora)
                    .Take(100)
                    .ToListAsync(ct)
            ];
        }

        foreach (var certificado in sinPdf)
            await IntentarAsync(() => AsegurarPdfAsync(certificado.Tenant, certificado.Id, certificado.EmisionActual, ct), certificado, "generar el PDF");
        foreach (var certificado in vencidos)
            await IntentarAsync(() => VencerAsync(certificado.Tenant, certificado.Id, ct), certificado, "vencer");
    }

    private async Task IntentarAsync<T>(Func<Task<T>> accion, Certificado certificado, string que)
    {
        try
        {
            await accion();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Queda pendiente: la siguiente pasada lo reintenta.
            registro.LogWarning(e, "No se pudo {Que} del certificado {Folio}; se reintentará.", que, certificado.FolioRaiz);
        }
    }

    private IDocumentSession SesionDeSistema(Guid tenantId)
    {
        var sesion = store.LightweightSession(tenantId.ToString("N"));
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", "certificados");
        return sesion;
    }
}
