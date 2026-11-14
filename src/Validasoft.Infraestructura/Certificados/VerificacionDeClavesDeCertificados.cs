using Microsoft.Extensions.Hosting;
using Validasoft.Aplicacion.Certificados;

namespace Validasoft.Infraestructura.Certificados;

/// <summary>
/// Falla el arranque si la clave de sellado de certificados falta o es inválida (CE-11). Sin esta verificación la API
/// arrancaba y el error aparecía al aprobar el primer expediente, porque la emisión ocurre en la misma transacción que la
/// aprobación (CE-1): en producción eso deja a las áreas sin poder aprobar. También revisa las claves retiradas (CL-2): cada una
/// con su fecha de retiro, y ninguna retirada «en el futuro» (eso le daría a la clave un plazo sin fin).
/// </summary>
public sealed class VerificacionDeClavesDeCertificados(ClavesDeCertificados claves, TimeProvider reloj) : IHostedService
{
    /// <summary>Holgura para un reloj adelantado o una fecha puesta con la hora local del despliegue.</summary>
    private static readonly TimeSpan HolguraDelRetiro = TimeSpan.FromDays(1);

    public Task StartAsync(CancellationToken cancellationToken)
    {
        _ = claves.Kid; // Carga y valida la semilla Ed25519 y las claves retiradas; lanza InvalidOperationException con el motivo.
        var limite = reloj.GetUtcNow() + HolguraDelRetiro;
        foreach (var retirada in claves.Retiradas)
            if (retirada.RetiradaEn > limite)
                throw new InvalidOperationException(
                    $"Validasoft:Certificados:ClavesAnteriores (kid {retirada.Kid}): RetiradaEn está en el futuro. Es la fecha en que la clave dejó de sellar; no puede ser posterior a hoy.");

        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
