// Ejemplo mínimo de un sistema integrador con Validasoft.Sdk (docs/08 §2.1, CN-7). Muestra las cuatro piezas que necesitan el
// SRM y Marco para apagar su lógica documental: (1) receptor de webhooks que verifica la firma, deduplica por id de evento y
// responde 200 al instante; (2) el mismo procesamiento por pull, para redes que no aceptan webhooks entrantes; (3) consulta de
// certificación con caché por ETag y respaldo local si Validasoft no responde; (4) sesión para la pantalla embebida en nombre
// de una persona. Los secretos van en configuración (variables de entorno o gestor de secretos), nunca en el repositorio.
using System.Collections.Concurrent;
using System.Net;
using System.Threading.Channels;
using Validasoft.Contratos;
using Validasoft.Sdk;

var builder = WebApplication.CreateBuilder(args);
var config = builder.Configuration.GetSection("Validasoft");
var archivoClave = config["ClavePrivadaPemArchivo"];
builder.Services.AddSingleton(new OpcionesValidasoft
{
    UrlBase = new Uri(config["UrlBase"]!),
    ApiKey = config["ApiKey"]!,
    EmpresaId = Guid.TryParse(config["EmpresaId"], out var empresa) ? empresa : null,
    Firmante = string.IsNullOrEmpty(archivoClave) ? null : new FirmanteDeAsercionesDelegadas(config["CodigoSistema"]!, File.ReadAllText(archivoClave))
});
builder.Services.AddHttpClient<IClienteValidasoft, ClienteValidasoft>(http => http.Timeout = TimeSpan.FromSeconds(2));
builder.Services.AddSingleton(new VerificadorDeWebhooks(new[] { config["SecretoWebhook"] ?? "", config["SecretoWebhookAnterior"] ?? "" }
    .Where(s => s.Length > 0).DefaultIfEmpty("sin-configurar")));
builder.Services.AddSingleton(Channel.CreateUnbounded<EventoPublicoDto>());
builder.Services.AddSingleton<EventosProcesados>();
builder.Services.AddSingleton<CopiaLocalDeCertificacion>();
builder.Services.AddHostedService<ProcesadorDeEventos>();
if (config.GetValue<bool>("ModoPull"))
    builder.Services.AddHostedService<LectorPorPull>();

var app = builder.Build();

// (1) Receptor: firma y tolerancia de 5 minutos; 401 si no es auténtico; 200 rápido y se procesa en cola.
app.MapPost("/api/integracion/validasoft/webhook", async (HttpRequest request, VerificadorDeWebhooks verificador, EventosProcesados procesados,
    Channel<EventoPublicoDto> cola) =>
{
    using var lector = new StreamReader(request.Body);
    var cuerpo = await lector.ReadToEndAsync();
    try
    {
        var evento = verificador.Validar(n => request.Headers[n].FirstOrDefault(), cuerpo);
        if (procesados.Registrar(evento.Id))
            await cola.Writer.WriteAsync(evento);
        return Results.Ok();
    }
    catch (WebhookInvalidoException e)
    {
        app.Logger.LogWarning("Webhook rechazado: {Motivo}", e.Motivo);
        return Results.Unauthorized();
    }
});

// (3) Marco: ¿se le puede pagar a este proveedor? Caché de 5 minutos, 304 con ETag y respaldo si Validasoft no responde.
app.MapGet("/marco/proveedores/{nit}/certificacion", async (string nit, IClienteValidasoft validasoft, CopiaLocalDeCertificacion copia) =>
{
    var consulta = new ConsultaDeCertificacion("NIT", nit, "PROVEEDOR");
    var guardada = copia.Leer(nit);
    if (guardada is not null && DateTimeOffset.UtcNow - guardada.Fecha < TimeSpan.FromMinutes(5))
        return Results.Ok(guardada.Datos);
    try
    {
        var respuesta = await validasoft.Consultas.CertificacionAsync(consulta, guardada?.ETag);
        var datos = respuesta.NoModificado ? guardada!.Datos : respuesta.Datos!;
        copia.Guardar(nit, datos, respuesta.ETag);
        return Results.Ok(datos);
    }
    catch (Exception e) when (guardada is not null &&
                              (e is HttpRequestException or TaskCanceledException || e is ExcepcionValidasoft { Estado: >= HttpStatusCode.InternalServerError }))
    {
        // Mismo contrato, marcado como respaldo con su antigüedad (docs/08 §5): la pantalla avisa, no lo oculta.
        var antiguedad = (int)(DateTimeOffset.UtcNow - guardada!.Fecha).TotalSeconds;
        return Results.Ok(guardada.Datos with { FuenteRespaldo = true, AntiguedadSegundos = antiguedad });
    }
});

// (4) Embebido: el backend del SRM pide la sesión de 30 minutos de la persona y se la pasa al iframe por postMessage.
app.MapPost("/srm/validasoft/sesion", async (UsuarioDelegado usuario, IClienteValidasoft validasoft) =>
    Results.Ok(await validasoft.ComoUsuario(usuario).Sesiones.CrearAsync()));

app.Run();

/// <summary>Dedupe por <c>Validasoft-Event-Id</c> (T-6). En el SRM es la tabla <c>adp_srm.ValidasoftEntregas</c> con PK <c>evento_id</c>.</summary>
internal sealed class EventosProcesados
{
    private readonly ConcurrentDictionary<Guid, DateTimeOffset> _vistos = new();

    public bool Registrar(Guid eventoId) => _vistos.TryAdd(eventoId, DateTimeOffset.UtcNow);
}

/// <summary>Copia local de la consulta (en Marco, <c>adp_cs.validasoft_certificacion_local</c>).</summary>
internal sealed class CopiaLocalDeCertificacion
{
    internal sealed record Guardada(ConsultaCertificacionDto Datos, string? ETag, DateTimeOffset Fecha);

    private readonly ConcurrentDictionary<string, Guardada> _copias = new();

    public Guardada? Leer(string nit) => _copias.TryGetValue(nit, out var guardada) ? guardada : null;

    public void Guardar(string nit, ConsultaCertificacionDto datos, string? etag) => _copias[nit] = new Guardada(datos, etag, DateTimeOffset.UtcNow);
}

/// <summary>Aplica cada evento una sola vez y en orden de cursor (mapa evento → acción de docs/08 §3.2).</summary>
internal sealed class ProcesadorDeEventos(Channel<EventoPublicoDto> cola, ILogger<ProcesadorDeEventos> log) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        await foreach (var evento in cola.Reader.ReadAllAsync(ct))
        {
            var accion = evento.Tipo switch
            {
                "certificado.emitido" => "habilitar proveedor, guardar folio y aplicar cambios en AyF",
                "vinculo.bloqueado" or "certificado.suspendido" => "marcar proveedor bloqueado",
                "expediente.rechazado" => "cerrar la habilitación",
                _ => "registrar"
            };
            log.LogInformation("{Tipo} ({Cursor}) en {Empresa}: {Accion}", evento.Tipo, evento.Cursor, evento.EmpresaId, accion);
        }
    }
}

/// <summary>(2) Modo pull: cada minuto lee desde el último cursor y encola lo nuevo (el mismo procesador que el webhook).</summary>
internal sealed class LectorPorPull(IClienteValidasoft validasoft, Channel<EventoPublicoDto> cola, EventosProcesados procesados,
    ILogger<LectorPorPull> log) : BackgroundService
{
    // En el SRM el cursor se guarda en base de datos para retomar tras un reinicio.
    private string? _cursor;

    protected override async Task ExecuteAsync(CancellationToken ct)
    {
        while (!ct.IsCancellationRequested)
        {
            try
            {
                await foreach (var evento in validasoft.Eventos.RecorrerAsync(_cursor, ct: ct))
                {
                    if (procesados.Registrar(evento.Id))
                        await cola.Writer.WriteAsync(evento, ct);
                    _cursor = evento.Cursor;
                }
            }
            catch (Exception e) when (e is HttpRequestException or ExcepcionValidasoft)
            {
                log.LogWarning(e, "Validasoft no respondió; se reintenta en un minuto.");
            }

            await Task.Delay(TimeSpan.FromMinutes(1), ct);
        }
    }
}
