using Marten;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Comprueba en cada petición que la sesión directa de un token (<c>validasoft:sesion-id</c>) siga vigente: cerrarla, revocarla o desactivar a la
/// persona corta el token de acceso en segundos en vez de esperar a que venza. El resultado se guarda <see cref="Vigencia"/> en memoria; al
/// cerrar o revocar se olvida en este nodo (en los demás, al vencer la caché).
/// </summary>
public sealed class VerificadorDeSesionesDirectas(IDocumentStore store, IMemoryCache cache, TimeProvider reloj) : ICacheDeSesionesDirectas
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(15);

    private static string Clave(Guid sesionId) => "sesion-directa:" + sesionId.ToString("N");

    public async Task<bool> VigenteAsync(Guid sesionId, CancellationToken ct)
    {
        if (cache.TryGetValue(Clave(sesionId), out bool vigente))
            return vigente;

        await using var consulta = store.QuerySession();
        var sesion = await consulta.LoadAsync<SesionDeUsuario>(sesionId, ct);
        vigente = sesion is not null && sesion.VigenteEn(reloj.GetUtcNow());
        cache.Set(Clave(sesionId), vigente, Vigencia);
        return vigente;
    }

    public void Olvidar(Guid sesionId) => cache.Remove(Clave(sesionId));
}

/// <summary>
/// Borra periódicamente lo que el ingreso directo ya no necesita (<c>Validasoft:Ingreso:MinutosLimpieza</c>, 10 por defecto): los desafíos vencidos y las
/// sesiones que vencieron o se cerraron hace más de una semana. El registro de identidad no se borra (se conserva como la auditoría).
/// </summary>
public sealed class LimpiezaDeIngresos(IDocumentStore store, TimeProvider reloj, IConfiguration configuracion, ILogger<LimpiezaDeIngresos> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = TimeSpan.FromMinutes(Math.Max(1, configuracion.GetValue("Validasoft:Ingreso:MinutosLimpieza", 10)));
        using var temporizador = new PeriodicTimer(intervalo, reloj);
        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                var ahora = reloj.GetUtcNow();
                await using var sesion = store.LightweightSession();
                var desafios = ahora.AddHours(-1);
                var sesiones = ahora.AddDays(-7);
                sesion.DeleteWhere<DesafioDeIngreso>(d => d.Expira < desafios);
                sesion.DeleteWhere<SesionDeUsuario>(s => s.ExpiraEn < sesiones || (s.RevocadaEn != null && s.RevocadaEn < sesiones));
                await sesion.SaveChangesAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "No se pudieron borrar los desafíos y sesiones de ingreso vencidos; se reintenta en {Intervalo}", intervalo);
            }
        }
    }
}
