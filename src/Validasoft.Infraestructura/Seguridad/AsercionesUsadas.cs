using JasperFx;
using Marten;
using Marten.Exceptions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Validasoft.Dominio.Comun;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Aserción <c>X-Usuario-Delegado</c> ya presentada (SE-4). Global, esquema <c>identidad</c>; <c>Id</c> =
/// <c>{sistemaId:N}:{jti}</c>, así la llave primaria impide registrar dos veces la misma. Se borra al vencer.
/// </summary>
public sealed class AsercionUsada
{
    public string Id { get; set; } = string.Empty;

    public Guid SistemaId { get; set; }

    public DateTimeOffset UsadaEn { get; set; }

    /// <summary><c>exp</c> de la aserción más la tolerancia de reloj: hasta entonces podría volver a ser válida.</summary>
    public DateTimeOffset ExpiraEn { get; set; }

    public static string Clave(Guid sistemaId, string jti) => $"{sistemaId:N}:{jti}";
}

/// <summary>
/// Anti-repetición de aserciones (SE-4): cada <c>(sistema, jti)</c> vale una sola vez. Se inserta con su propia
/// transacción; si la clave ya existe (otra petición la usó, aunque sea en paralelo) ⇒ 401 <c>asercion-reutilizada</c>.
/// </summary>
public sealed class RegistroDeAsercionesUsadas(IDocumentStore store)
{
    public async Task RegistrarAsync(Guid sistemaId, string jti, DateTimeOffset ahora, DateTimeOffset expiraEn, CancellationToken ct)
    {
        await using var sesion = store.LightweightSession();
        sesion.Insert(new AsercionUsada { Id = AsercionUsada.Clave(sistemaId, jti), SistemaId = sistemaId, UsadaEn = ahora, ExpiraEn = expiraEn });
        try
        {
            await sesion.SaveChangesAsync(ct);
        }
        catch (Exception e) when (e is DocumentAlreadyExistsException or
                                       MartenCommandException { InnerException: PostgresException { SqlState: PostgresErrorCodes.UniqueViolation } })
        {
            throw ReglaDeNegocioException.NoAutenticado("asercion-reutilizada",
                "Esta aserción X-Usuario-Delegado ya se usó: firma una nueva (con otro jti) para cada petición.");
        }
    }

    /// <summary>Borra las vencidas (ya no pueden volver a ser válidas: su <c>exp</c> pasó).</summary>
    public async Task LimpiarVencidasAsync(DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = store.LightweightSession();
        sesion.DeleteWhere<AsercionUsada>(a => a.ExpiraEn < ahora);
        await sesion.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Borra periódicamente las aserciones vencidas (<c>Validasoft:AsercionesUsadas:MinutosLimpieza</c>, 10 por defecto).
/// En varias réplicas cada una limpia: el borrado es idempotente y usa el índice por <c>ExpiraEn</c>.
/// </summary>
public sealed class LimpiezaDeAsercionesUsadas(
    RegistroDeAsercionesUsadas registro, TimeProvider reloj, IConfiguration configuracion, ILogger<LimpiezaDeAsercionesUsadas> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(Math.Max(1, configuracion.GetValue("Validasoft:AsercionesUsadas:MinutosLimpieza", 10)));
        using var temporizador = new PeriodicTimer(intervalo, reloj);
        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await registro.LimpiarVencidasAsync(reloj.GetUtcNow(), stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "No se pudieron borrar las aserciones delegadas vencidas; se reintenta en {Intervalo}", intervalo);
            }
        }
    }
}
