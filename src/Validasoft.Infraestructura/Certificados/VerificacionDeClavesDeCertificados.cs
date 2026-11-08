using Microsoft.Extensions.Hosting;
using Validasoft.Aplicacion.Certificados;

namespace Validasoft.Infraestructura.Certificados;

/// <summary>
/// Falla el arranque si la clave de sellado de certificados falta o es inválida (CE-11). Sin esta verificación la API
/// arrancaba y el error aparecía al aprobar el primer expediente, porque la emisión ocurre en la misma transacción que la
/// aprobación (CE-1): en producción eso deja a las áreas sin poder aprobar.
/// </summary>
public sealed class VerificacionDeClavesDeCertificados(ClavesDeCertificados claves) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = claves.Kid; // Carga y valida la semilla Ed25519; lanza InvalidOperationException con el motivo.
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
