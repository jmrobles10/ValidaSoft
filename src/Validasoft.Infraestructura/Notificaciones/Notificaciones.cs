using System.Diagnostics;
using System.Net;
using System.Net.Sockets;
using System.Text;
using JasperFx.Events.Daemon;
using JasperFx.Events.Projections;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Integraciones;
using Wolverine;
using Wolverine.Marten.Subscriptions;

namespace Validasoft.Infraestructura.Notificaciones;

/// <summary>
/// Suscripción de Marten (daemon asíncrono) que alimenta eventos públicos, webhooks y notificaciones (NW-3). Wolverine la
/// ejecuta en lotes en el orden del event store y confirma en una transacción lo escrito con <c>operations</c>, los
/// mensajes del <c>bus</c> (outbox) y el avance de la suscripción.
/// </summary>
public sealed class SuscripcionDeEventosPublicos : BatchSubscription
{
    public const string Nombre = "eventos-publicos";

    private readonly IServiceProvider _servicios;

    public SuscripcionDeEventosPublicos(IServiceProvider servicios) : base(Nombre)
    {
        _servicios = servicios;
        // Lotes chicos: la latencia de los webhooks importa más que el rendimiento de reconstrucción.
        Options.BatchSize = 200;
    }

    public override async Task ProcessEventsAsync(EventRange page, ISubscriptionController controller, IDocumentOperations operations, IMessageBus bus,
        CancellationToken cancellationToken)
    {
        // El store y el procesador se resuelven al procesar (el store se construye con esta suscripción registrada).
        var procesador = _servicios.GetRequiredService<ProcesadorDeEventos>();
        await using var contexto = new ContextoDeEventos(_servicios.GetRequiredService<IDocumentStore>());
        await procesador.ProcesarAsync(page.Events, operations, bus, contexto, cancellationToken);
    }
}

/// <summary>El destino resolvió a una dirección no permitida (privada, local o reservada): no se conecta (SSRF).</summary>
public sealed class DestinoNoPermitidoException(string host) : Exception($"El destino {host} resolvió a una dirección no permitida.");

/// <summary>
/// Envío HTTP de las entregas por <see cref="IHttpClientFactory"/> (cliente con nombre <see cref="Cliente"/>). Sin
/// redirecciones ni cookies, con timeout corto, y con la IP comprobada <b>al conectar</b> (<see cref="CrearManejador"/>):
/// así un DNS que cambia entre la validación y el envío (rebinding) no lleva a una red privada (NW-6).
/// </summary>
public sealed class EnviadorDeWebhooksHttp(IHttpClientFactory fabrica, OpcionesDeWebhooks opciones) : IEnviadorDeWebhooks
{
    public const string Cliente = "validasoft-webhooks";

    public async Task<ResultadoDeWebhook> EnviarAsync(SolicitudDeWebhook solicitud, CancellationToken ct)
    {
        var cliente = fabrica.CreateClient(Cliente);
        using var peticion = new HttpRequestMessage(HttpMethod.Post, solicitud.Url)
        {
            Content = new StringContent(solicitud.Cuerpo, Encoding.UTF8, "application/json")
        };
        peticion.Headers.UserAgent.ParseAdd("Validasoft-Webhooks/1.0");
        foreach (var (nombre, valor) in solicitud.Cabeceras)
            peticion.Headers.TryAddWithoutValidation(nombre, valor);

        using var limite = CancellationTokenSource.CreateLinkedTokenSource(ct);
        limite.CancelAfter(opciones.Timeout);
        var cronometro = Stopwatch.StartNew();
        try
        {
            using var respuesta = await cliente.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead, limite.Token);
            var codigo = (int)respuesta.StatusCode;
            var exito = codigo is >= 200 and < 300;
            return new ResultadoDeWebhook(exito, codigo, cronometro.ElapsedMilliseconds, exito ? null : $"HTTP {codigo}");
        }
        catch (OperationCanceledException) when (!ct.IsCancellationRequested)
        {
            return new ResultadoDeWebhook(false, null, cronometro.ElapsedMilliseconds, "timeout");
        }
        catch (HttpRequestException e)
        {
            var error = Causa(e) is DestinoNoPermitidoException ? "destino-no-permitido" : "error-de-conexion";
            return new ResultadoDeWebhook(false, null, cronometro.ElapsedMilliseconds, error);
        }
    }

    private static Exception? Causa(Exception e)
    {
        var actual = e.InnerException;
        while (actual?.InnerException is not null && actual is not DestinoNoPermitidoException)
            actual = actual.InnerException;
        return actual;
    }

    /// <summary>
    /// Manejador primario: resuelve el host y solo conecta si <b>todas</b> sus direcciones están permitidas
    /// (<see cref="DestinoDeWebhook.Permitida"/>), conectando a esas mismas direcciones (DNS pinning).
    /// </summary>
    public static SocketsHttpHandler CrearManejador(OpcionesDeWebhooks opciones) => new()
    {
        AllowAutoRedirect = false,
        UseCookies = false,
        UseProxy = false,
        ConnectTimeout = opciones.Timeout,
        PooledConnectionLifetime = TimeSpan.FromMinutes(2),
        ConnectCallback = async (contexto, ct) =>
        {
            var host = contexto.DnsEndPoint.Host;
            var direcciones = IPAddress.TryParse(host.Trim('[', ']'), out var literal) ? [literal] : await Dns.GetHostAddressesAsync(host, ct);
            if (direcciones.Length == 0 ||
                direcciones.Any(ip => !DestinoDeWebhook.Permitida(ip, opciones.PermitirHttpLocal, opciones.PermitirDestinosPrivados)))
                throw new DestinoNoPermitidoException(host);

            var socket = new Socket(SocketType.Stream, ProtocolType.Tcp) { NoDelay = true };
            try
            {
                await socket.ConnectAsync(direcciones, contexto.DnsEndPoint.Port, ct);
                return new NetworkStream(socket, ownsSocket: true);
            }
            catch
            {
                socket.Dispose();
                throw;
            }
        }
    };
}
