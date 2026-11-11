using System.Net;
using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Infraestructura.Verificaciones;

/// <summary>
/// Cómo se le pregunta a Infolaft (CU-21): ruta, nombres de los parámetros y valores del modo de búsqueda. Lo único confirmado
/// es la búsqueda por nombre del SRM (<c>Search/searchPaginated</c>, <c>queryByName</c>, <c>SearchByName</c>, exactitud
/// <c>EXACTA</c>); lo demás —incluida la búsqueda por documento— es un <b>supuesto</b> que Infolaft debe confirmar. Todo se puede
/// cambiar por configuración (<c>Validasoft:Verificadores:ListasRestrictivas:Proveedores:Infolaft:*</c>) sin tocar el código
/// cuando llegue la documentación o el usuario de pruebas.
/// </summary>
public sealed record FormaDeBusquedaInfolaft(string Ruta, string Exactitud, string ParametroNombre, string ModoNombre, string ParametroDocumento,
    string ModoDocumento, string? ParametroTipoDocumento)
{
    public static FormaDeBusquedaInfolaft Desde(IConfiguration? configuracion)
    {
        var raiz = configuracion?.GetSection($"Validasoft:Verificadores:ListasRestrictivas:Proveedores:{ProveedoresDeListas.Infolaft}");
        string Valor(string clave, string porOmision) => raiz?[clave] is { Length: > 0 } valor ? valor : porOmision;
        return new FormaDeBusquedaInfolaft(Valor("RutaBusqueda", "Search/searchPaginated"), Valor("Exactitud", "EXACTA"), Valor("ParametroNombre", "queryByName"),
            Valor("ModoNombre", "SearchByName"), Valor("ParametroDocumento", "queryByDocument"), Valor("ModoDocumento", "SearchByDocument"),
            raiz?["ParametroTipoDocumento"] is { Length: > 0 } tipo ? tipo : null);
    }

    /// <summary>Ruta y consulta de una búsqueda por la persona, según el modo de la empresa (nombre o número de documento).</summary>
    public string Consulta(PersonaAConsultar persona, ModoBusquedaListas modo)
    {
        var comun = $"{Ruta}?accuracyLevel={Uri.EscapeDataString(Exactitud)}&forceSearch=true";
        if (modo == ModoBusquedaListas.PorDocumento)
        {
            var tipo = ParametroTipoDocumento is null ? string.Empty : $"&{ParametroTipoDocumento}={Uri.EscapeDataString(persona.TipoIdentificacion)}";
            return $"{comun}&{ParametroDocumento}={Uri.EscapeDataString(persona.Numero)}{tipo}&searchMode={Uri.EscapeDataString(ModoDocumento)}";
        }

        return $"{comun}&{ParametroNombre}={Uri.EscapeDataString(persona.Nombre)}&searchMode={Uri.EscapeDataString(ModoNombre)}";
    }
}

/// <summary>
/// Infolaft, proveedor principal de Sinco (VE-8, CU-21). Autenticación HTTP Basic con el usuario y la clave de la empresa; una
/// búsqueda por persona con <c>POST Search/searchPaginated?accuracyLevel=EXACTA&amp;forceSearch=true&amp;queryByName={nombre}&amp;searchMode=SearchByName</c>
/// o, en modo <c>PorDocumento</c>, por número (<c>queryByDocument</c>/<c>SearchByDocument</c>: <b>supuesto</b> sin confirmar; ver
/// <see cref="FormaDeBusquedaInfolaft"/>). El formato de la respuesta es un supuesto: se lee con <see cref="LectorDeRespuestaDeListas"/>
/// (lista de resultados en las claves habituales) y, si la respuesta trae un PDF (en base64 o por URL; otro supuesto), se guarda como
/// evidencia del proveedor cuando la consulta cubre a una sola persona: con varias, Validasoft genera el suyo (sin librería para
/// unir PDF) y conserva la respuesta cruda con su hash. Un 401/403 es de credenciales; un 429 es el límite del plan. Riesgo de
/// Habeas Data: el nombre o documento viaja en la query string (queda en los registros del proveedor y de los proxies); la consulta
/// va amparada por la autorización de tratamiento que el tercero aceptó. Apagado por omisión; nunca se llama sin credenciales de la
/// empresa. Con el simulado detrás de <see cref="IVerificadorDeListas"/> se desarrolla y se prueba todo sin tocar a Infolaft.
/// </summary>
public sealed class VerificadorInfolaft(IHttpClientFactory clientes, ILogger<VerificadorInfolaft> registro, IConfiguration? configuracion = null) : IVerificadorDeListas
{
    public const string Cliente = "listas-infolaft";

    private readonly FormaDeBusquedaInfolaft _forma = FormaDeBusquedaInfolaft.Desde(configuracion);

    public string Proveedor => ProveedoresDeListas.Infolaft;

    public async Task<RespuestaDeListas> ConsultarAsync(SolicitudDeListas solicitud, CancellationToken ct)
    {
        var credenciales = solicitud.Credenciales ?? throw new ProveedorDeListasNoDisponibleException("Infolaft necesita las credenciales de la empresa.");
        var url = Base(credenciales.UrlBase ?? OpcionesDeVerificaciones.UrlPorOmision(Proveedor)!);
        var cliente = clientes.CreateClient(Cliente);
        var crudas = new JsonArray();
        var coincidencias = new List<CoincidenciaHallada>();
        byte[]? pdf = null;
        foreach (var persona in solicitud.Personas)
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Post, new Uri(url, _forma.Consulta(persona, credenciales.ModoBusqueda)))
            {
                Content = new StringContent(string.Empty)
            };
            peticion.Headers.Authorization = Basica(credenciales);
            peticion.Headers.Accept.Add(new MediaTypeWithQualityHeaderValue("application/json"));
            using var respuesta = await cliente.SendAsync(peticion, ct);
            var texto = await respuesta.Content.ReadAsStringAsync(ct);
            if (respuesta.StatusCode is HttpStatusCode.Unauthorized or HttpStatusCode.Forbidden)
                throw new ProveedorDeListasNoDisponibleException("Infolaft rechazó las credenciales de la empresa.");
            if (respuesta.StatusCode == HttpStatusCode.TooManyRequests)
                throw new ProveedorDeListasNoDisponibleException("Infolaft respondió que se superó el límite de consultas del plan; se reintentará.");
            if (!respuesta.IsSuccessStatusCode)
                throw new ProveedorDeListasNoDisponibleException($"Infolaft respondió HTTP {(int)respuesta.StatusCode}.");

            try
            {
                coincidencias.AddRange(LectorDeRespuestaDeListas.Leer(texto, persona));
                crudas.Add(new JsonObject { ["persona"] = persona.Clave, ["respuesta"] = JsonNode.Parse(texto) });
                if (solicitud.Personas.Count == 1)
                    pdf = await PdfDeLaRespuestaAsync(cliente, credenciales, texto, ct);
            }
            catch (JsonException e)
            {
                registro.LogWarning("Infolaft devolvió algo que no es JSON para una persona ({Longitud} caracteres).", texto.Length);
                throw new ProveedorDeListasNoDisponibleException("Infolaft devolvió una respuesta que no es JSON.", e);
            }
        }

        var criterio = credenciales.ModoBusqueda == ModoBusquedaListas.PorDocumento
            ? $"Infolaft · búsqueda por número de documento ({_forma.ModoDocumento}), exactitud {_forma.Exactitud}"
            : $"Infolaft · búsqueda por nombre ({_forma.ModoNombre}), exactitud {_forma.Exactitud}";
        return new RespuestaDeListas($"infolaft-{Guid.CreateVersion7():N}", coincidencias, criterio, ["Listas de Infolaft (según el plan contratado)"], pdf,
            new JsonObject { ["proveedor"] = Proveedor, ["consultas"] = crudas }.ToJsonString());
    }

    /// <summary>El PDF que Infolaft entregue con la respuesta (base64 o URL https); nulo si no trae ninguno o no es un PDF.</summary>
    private static async Task<byte[]?> PdfDeLaRespuestaAsync(HttpClient cliente, CredencialesEnClaro credenciales, string json, CancellationToken ct)
    {
        var (pdf, enlace) = LectorDeRespuestaDeListas.PdfDe(json);
        if (pdf is not null)
            return pdf;
        if (enlace is null || !Uri.TryCreate(enlace, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps)
            return null;
        try
        {
            using var peticion = new HttpRequestMessage(HttpMethod.Get, uri);
            peticion.Headers.Authorization = Basica(credenciales);
            using var respuesta = await cliente.SendAsync(peticion, ct);
            if (!respuesta.IsSuccessStatusCode)
                return null;
            var bytes = await respuesta.Content.ReadAsByteArrayAsync(ct);
            return LectorDeRespuestaDeListas.EsPdf(bytes) ? bytes : null;
        }
        catch (HttpRequestException)
        {
            return null; // Sin el PDF del proveedor, Validasoft genera el suyo: la consulta no se pierde.
        }
    }

    internal static Uri Base(string url) => new(url.EndsWith('/') ? url : url + "/");

    internal static AuthenticationHeaderValue Basica(CredencialesEnClaro c) =>
        new("Basic", Convert.ToBase64String(Encoding.UTF8.GetBytes($"{c.Usuario}:{c.Clave}")));
}
