using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Identidad;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Comprueba la propiedad de un dominio (PI-2) buscando el token en <c>https://{dominio}/.well-known/validasoft-verificacion.txt</c>: solo quien controla el sitio
/// del dominio puede publicarlo. Pasa por el cliente de SSO (no entra a redes internas ni sigue redirecciones) y lee a lo sumo 512 bytes. Cualquier fallo es «no
/// verificado»; nunca revela por qué.
/// </summary>
public sealed class ComprobadorDeDominiosHttp(IHttpClientFactory http, ILogger<ComprobadorDeDominiosHttp> logger) : IComprobadorDeDominios
{
    public async Task<bool> ComprobarAsync(string dominio, string token, CancellationToken ct)
    {
        try
        {
            using var cliente = http.CreateClient(ClienteSso.Nombre);
            cliente.Timeout = TimeSpan.FromSeconds(8);
            using var respuesta = await cliente.GetAsync(new Uri($"https://{dominio}{ComprobadorDeRuta.Ruta}"), HttpCompletionOption.ResponseHeadersRead, ct);
            if (!respuesta.IsSuccessStatusCode)
                return false;

            await using var flujo = await respuesta.Content.ReadAsStreamAsync(ct);
            var buffer = new byte[512];
            var leidos = await flujo.ReadAtLeastAsync(buffer, buffer.Length, throwOnEndOfStream: false, ct);
            return System.Text.Encoding.UTF8.GetString(buffer, 0, leidos).Trim() == token;
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or InvalidOperationException or UriFormatException)
        {
            logger.LogInformation("No se pudo comprobar el dominio {Dominio}: {Motivo}", dominio, e.GetType().Name);
            return false;
        }
    }
}
