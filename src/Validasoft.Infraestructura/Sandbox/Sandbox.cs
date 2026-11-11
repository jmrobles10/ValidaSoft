using System.Collections.Concurrent;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Ia;
using Validasoft.Dominio.Plataforma;
using Validasoft.Infraestructura.IA;

namespace Validasoft.Infraestructura.Sandbox;

/// <inheritdoc cref="IEntornos"/>
public sealed class Entornos(IDocumentStore store) : IEntornos
{
    private readonly ConcurrentDictionary<Guid, bool> _sandbox = new();

    public async ValueTask<bool> EsSandboxAsync(Guid tenantId, CancellationToken ct = default)
    {
        if (_sandbox.TryGetValue(tenantId, out var esSandbox))
            return esSandbox;

        await using var consulta = store.QuerySession();
        if (await consulta.LoadAsync<Tenant>(tenantId, ct) is not { } tenant)
            return false; // aún no existe (o no es un tenant): no se recuerda, por si se crea después

        _sandbox[tenantId] = tenant.EsSandbox;
        return tenant.EsSandbox;
    }
}

/// <summary>
/// Decide el destino de cada correo (SB-4): el de un tenant sandbox se guarda en su buzón de pruebas y nunca sale a su
/// destinatario; los demás van al transporte real (SMTP). Un correo sin tenant (el acceso del tercero sin empresa) es de
/// producción.
/// </summary>
public sealed class EnviadorDeCorreo(ITransporteDeCorreo transporte, IEntornos entornos, IDocumentStore store, TimeProvider reloj,
    ILogger<EnviadorDeCorreo> logger) : IEnviadorDeCorreo
{
    public async Task EnviarAsync(MensajeDeCorreo mensaje, CancellationToken ct)
    {
        if (mensaje.TenantId is { } tenantId && await entornos.EsSandboxAsync(tenantId, ct))
        {
            await using var sesion = store.LightweightSession(tenantId.ToString("N"));
            sesion.Store(CorreoDePrueba.De(mensaje.Para, mensaje.Asunto, mensaje.Texto, mensaje.Html, reloj.GetUtcNow()));
            await sesion.SaveChangesAsync(ct);
            logger.LogInformation("Correo retenido en el buzón del sandbox: {Asunto}", mensaje.Asunto);
            return;
        }

        await transporte.EnviarAsync(mensaje, ct);
    }
}

/// <summary>
/// El analizador de documentos que usa toda la aplicación (SB-4): con <c>Validasoft:IA:Proveedor = Simulado</c> siempre el
/// simulado; si no, OpenAI con la llave de cada empresa, salvo las llaves de prueba del sandbox
/// (<see cref="AislamientoDeSandbox.PrefijoDeLlaveDeIa"/>), que van al proveedor simulado: un documento de prueba nunca sale a la
/// red ni cuesta.
/// </summary>
public sealed class AnalizadorPorEntorno(AnalizadorOpenAi real, AnalizadorSimulado simulado, IConfiguration configuracion) : IAnalizadorDeDocumentos
{
    private readonly bool _todoSimulado =
        string.Equals(configuracion["Validasoft:IA:Proveedor"], AnalizadorSimulado.Proveedor, StringComparison.OrdinalIgnoreCase);

    private IAnalizadorDeDocumentos Elegir(string apiKey) =>
        _todoSimulado || AislamientoDeSandbox.EsLlaveDeIaDeSandbox(apiKey) ? simulado : real;

    public Task<ResultadoDeAnalisis> AnalizarAsync(SolicitudDeAnalisis solicitud, string apiKey, CancellationToken ct) =>
        Elegir(apiKey).AnalizarAsync(solicitud, apiKey, ct);

    public Task<ResultadoDePruebaDeLlave> ProbarLlaveAsync(string apiKey, CancellationToken ct) => Elegir(apiKey).ProbarLlaveAsync(apiKey, ct);
}
