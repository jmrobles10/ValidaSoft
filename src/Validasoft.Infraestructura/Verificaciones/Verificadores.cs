using System.Globalization;
using System.Net;
using System.Net.Http.Headers;
using System.Net.Http.Json;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Infraestructura.Verificaciones;

/// <summary>
/// Proveedor simulado y determinista (VE-7), solo para desarrollo y pruebas (<c>PermitirSimulado</c>): una persona cuyo
/// nombre contiene «coincidencia» (sin importar mayúsculas ni tildes) aparece en la lista OFAC (SDN); nadie más. No llama a
/// nadie y no guarda respuesta cruda.
/// </summary>
public sealed class VerificadorSimulado : IVerificadorDeListas
{
    public const string Disparador = "coincidencia";

    public static readonly IReadOnlyList<string> Listas = ["OFAC (SDN)", "ONU (Consejo de Seguridad)", "Procuraduría (simulada)"];

    public string Proveedor => ProveedoresDeListas.Simulado;

    public Task<RespuestaDeListas> ConsultarAsync(SolicitudDeListas solicitud, CancellationToken ct)
    {
        var coincidencias = solicitud.Personas
            .Where(p => SinTildes(p.Nombre).Contains(Disparador, StringComparison.OrdinalIgnoreCase))
            .Select(p => new CoincidenciaHallada(p.Clave, Listas[0], p.Nombre.ToUpperInvariant(), "Coincidencia simulada por nombre de prueba.", 100m))
            .ToList();
        return Task.FromResult(new RespuestaDeListas($"sim-{Guid.CreateVersion7():N}", coincidencias,
            "Simulado (desarrollo y pruebas): coincide si el nombre contiene «coincidencia».", Listas, null, null));
    }

    private static string SinTildes(string texto) =>
        new([.. texto.Normalize(NormalizationForm.FormD).Where(c => CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark)]);
}

/// <summary>
/// Infolaft, proveedor principal de Sinco (VE-8). Autenticación HTTP Basic con el usuario y la clave de la empresa; una
/// búsqueda por persona con <c>POST Search/searchPaginated?accuracyLevel=EXACTA&amp;forceSearch=true&amp;queryByName={nombre}&amp;searchMode=SearchByName</c>
/// (por documento, <c>queryByDocument</c>/<c>SearchByDocument</c>, es un <b>supuesto</b> sin confirmar). El formato de la
/// respuesta es un supuesto: se lee con <see cref="LectorDeRespuestaDeListas"/>. Infolaft no entrega PDF por API (supuesto):
/// Validasoft genera el suyo y guarda la respuesta cruda con su hash. Riesgo de Habeas Data: el nombre viaja en la query
/// string (queda en los registros del proveedor y de los proxies); la consulta va amparada por la autorización de
/// tratamiento que el tercero aceptó. Apagado por omisión.
/// </summary>
public sealed class VerificadorInfolaft(IHttpClientFactory clientes, ILogger<VerificadorInfolaft> registro) : IVerificadorDeListas
{
    public const string Cliente = "listas-infolaft";

    public string Proveedor => ProveedoresDeListas.Infolaft;

    public async Task<RespuestaDeListas> ConsultarAsync(SolicitudDeListas solicitud, CancellationToken ct)
    {
        var credenciales = solicitud.Credenciales ?? throw new ProveedorDeListasNoDisponibleException("Infolaft necesita las credenciales de la empresa.");
        var url = Base(credenciales.UrlBase ?? OpcionesDeVerificaciones.UrlPorOmision(Proveedor)!);
        var cliente = clientes.CreateClient(Cliente);
        var crudas = new JsonArray();
        var coincidencias = new List<CoincidenciaHallada>();
        foreach (var persona in solicitud.Personas)
        {
            var consulta = credenciales.ModoBusqueda == ModoBusquedaListas.PorDocumento
                ? $"Search/searchPaginated?accuracyLevel=EXACTA&forceSearch=true&queryByDocument={Uri.EscapeDataString(persona.Numero)}&searchMode=SearchByDocument"
                : $"Search/searchPaginated?accuracyLevel=EXACTA&forceSearch=true&queryByName={Uri.EscapeDataString(persona.Nombre)}&searchMode=SearchByName";
            using var peticion = new HttpRequestMessage(HttpMethod.Post, new Uri(url, consulta)) { Content = new StringContent(string.Empty) };
            peticion.Headers.Authorization = Basica(credenciales);
            peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var respuesta = await cliente.SendAsync(peticion, ct);
            var texto = await respuesta.Content.ReadAsStringAsync(ct);
            if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ProveedorDeListasNoDisponibleException("Infolaft rechazó las credenciales de la empresa.");
            if (!respuesta.IsSuccessStatusCode)
                throw new ProveedorDeListasNoDisponibleException($"Infolaft respondió HTTP {(int)respuesta.StatusCode}.");

            try
            {
                coincidencias.AddRange(LectorDeRespuestaDeListas.Leer(texto, persona));
                crudas.Add(new JsonObject { ["persona"] = persona.Clave, ["respuesta"] = JsonNode.Parse(texto) });
            }
            catch (JsonException e)
            {
                registro.LogWarning("Infolaft devolvió algo que no es JSON para una persona ({Longitud} caracteres).", texto.Length);
                throw new ProveedorDeListasNoDisponibleException("Infolaft devolvió una respuesta que no es JSON.", e);
            }
        }

        var criterio = credenciales.ModoBusqueda == ModoBusquedaListas.PorDocumento
            ? "Infolaft · búsqueda por número de documento, exactitud EXACTA"
            : "Infolaft · búsqueda por nombre (SearchByName), exactitud EXACTA";
        return new RespuestaDeListas($"infolaft-{Guid.CreateVersion7():N}", coincidencias, criterio, ["Listas de Infolaft (según el plan contratado)"], null,
            new JsonObject { ["proveedor"] = Proveedor, ["consultas"] = crudas }.ToJsonString());
    }

    internal static Uri Base(string url) => new(url.EndsWith('/') ? url : url + "/");

    internal static AuthenticationHeaderValue Basica(CredencialesEnClaro c) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.Usuario}:{c.Clave}")));
}

/// <summary>
/// Stradata (alternativa del legado, VE-8): <c>GET buscador?nom=&amp;id=&amp;min=90&amp;min_id=90&amp;format=json</c> con Basic, como
/// en el SRM (<c>ApiStradataBL</c>). Apagado por omisión.
/// </summary>
public sealed class VerificadorStradata(IHttpClientFactory clientes) : IVerificadorDeListas
{
    public const string Cliente = "listas-stradata";

    public string Proveedor => ProveedoresDeListas.Stradata;

    public async Task<RespuestaDeListas> ConsultarAsync(SolicitudDeListas solicitud, CancellationToken ct)
    {
        var credenciales = solicitud.Credenciales ?? throw new ProveedorDeListasNoDisponibleException("Stradata necesita las credenciales de la empresa.");
        var url = VerificadorInfolaft.Base(credenciales.UrlBase ?? OpcionesDeVerificaciones.UrlPorOmision(Proveedor)!);
        var cliente = clientes.CreateClient(Cliente);
        var crudas = new JsonArray();
        var coincidencias = new List<CoincidenciaHallada>();
        foreach (var persona in solicitud.Personas)
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Get, new Uri(url,
                $"buscador?nom={Uri.EscapeDataString(persona.Nombre)}&min=90&id={Uri.EscapeDataString(persona.Numero)}&format=json&min_id=90"));
            peticion.Headers.Authorization = VerificadorInfolaft.Basica(credenciales);
            using var respuesta = await cliente.SendAsync(peticion, ct);
            if (!respuesta.IsSuccessStatusCode)
                throw new ProveedorDeListasNoDisponibleException($"Stradata respondió HTTP {(int)respuesta.StatusCode}.");
            var texto = await respuesta.Content.ReadAsStringAsync(ct);
            coincidencias.AddRange(LectorDeRespuestaDeListas.Leer(texto, persona, Proveedor));
            crudas.Add(new JsonObject { ["persona"] = persona.Clave, ["respuesta"] = JsonNode.Parse(texto) });
        }

        return new RespuestaDeListas($"stradata-{Guid.CreateVersion7():N}", coincidencias, "Stradata · buscador por nombre e identificación (mínimo 90 %)",
            ["Listas de Stradata"], null, new JsonObject { ["proveedor"] = Proveedor, ["consultas"] = crudas }.ToJsonString());
    }
}

/// <summary>
/// Compliance (alternativa del legado, VE-8): token con Basic en <c>apiPublica/TokenService/token</c> y consulta consolidada
/// con Bearer en <c>ws/ConsultaConsolidadaService/consultaConsolidada/soloRiesgo/true</c>, como en el SRM (<c>ApiCompliance</c>).
/// Apagado por omisión.
/// </summary>
public sealed class VerificadorCompliance(IHttpClientFactory clientes) : IVerificadorDeListas
{
    public const string Cliente = "listas-compliance";

    public string Proveedor => ProveedoresDeListas.Compliance;

    public async Task<RespuestaDeListas> ConsultarAsync(SolicitudDeListas solicitud, CancellationToken ct)
    {
        var credenciales = solicitud.Credenciales ?? throw new ProveedorDeListasNoDisponibleException("Compliance necesita las credenciales de la empresa.");
        var url = VerificadorInfolaft.Base(credenciales.UrlBase ?? OpcionesDeVerificaciones.UrlPorOmision(Proveedor)!);
        var cliente = clientes.CreateClient(Cliente);

        using var pedirToken = new HttpRequestMessage(HttpMethod.Get, new Uri(url, "apiPublica/TokenService/token"));
        pedirToken.Headers.Authorization = VerificadorInfolaft.Basica(credenciales);
        using var respuestaToken = await cliente.SendAsync(pedirToken, ct);
        if (!respuestaToken.IsSuccessStatusCode)
            throw new ProveedorDeListasNoDisponibleException($"Compliance no entregó el token (HTTP {(int)respuestaToken.StatusCode}).");
        var token = (await respuestaToken.Content.ReadFromJsonAsync<JsonElement>(ct)).TryGetProperty("token", out var t) ? t.GetString() : null;
        if (string.IsNullOrWhiteSpace(token))
            throw new ProveedorDeListasNoDisponibleException("Compliance no entregó el token.");

        var crudas = new JsonArray();
        var coincidencias = new List<CoincidenciaHallada>();
        foreach (var persona in solicitud.Personas)
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Post, new Uri(url, "ws/ConsultaConsolidadaService/consultaConsolidada/soloRiesgo/true"))
            {
                Content = JsonContent.Create(new { datoConsultar = persona.Numero, tipoDocumento = persona.TipoIdentificacion.ToLowerInvariant(), nombre = persona.Nombre, modoRapido = true })
            };
            peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
            using var respuesta = await cliente.SendAsync(peticion, ct);
            if (!respuesta.IsSuccessStatusCode)
                throw new ProveedorDeListasNoDisponibleException($"Compliance respondió HTTP {(int)respuesta.StatusCode}.");
            var texto = await respuesta.Content.ReadAsStringAsync(ct);
            coincidencias.AddRange(LectorDeRespuestaDeListas.Leer(texto, persona, Proveedor));
            crudas.Add(new JsonObject { ["persona"] = persona.Clave, ["respuesta"] = JsonNode.Parse(texto) });
        }

        return new RespuestaDeListas($"compliance-{Guid.CreateVersion7():N}", coincidencias, "Compliance · consulta consolidada (solo riesgo)",
            ["Listas de Compliance"], null, new JsonObject { ["proveedor"] = Proveedor, ["consultas"] = crudas }.ToJsonString());
    }
}

/// <summary>
/// Cada <c>Validasoft:Verificadores:SegundosProceso</c> (30 por omisión) corre las consultas automáticas de listas, las
/// expiraciones y el monitoreo de vínculos activos (VE-12). Lo pendiente vive en los propios expedientes y monitoreos, así que
/// una caída no pierde nada.
/// </summary>
public sealed class ProcesadorDeVerificaciones(TareasDeVerificaciones tareas, OpcionesDeVerificaciones opciones, TimeProvider reloj,
    ILogger<ProcesadorDeVerificaciones> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        using var temporizador = new PeriodicTimer(opciones.IntervaloProceso, reloj);
        while (await temporizador.WaitForNextTickAsync(stoppingToken))
        {
            try
            {
                await tareas.ProcesarPendientesAsync(stoppingToken);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "No se pudieron procesar las verificaciones pendientes; se reintenta en {Intervalo}", opciones.IntervaloProceso);
            }
        }
    }
}
