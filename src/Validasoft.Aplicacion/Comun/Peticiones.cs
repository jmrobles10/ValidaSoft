using System.Globalization;
using System.Text;
using System.Text.Json;
using System.Text.Json.Serialization;
using JasperFx;
using JasperFx.Events;
using Marten.Exceptions;
using Microsoft.AspNetCore.Http;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Utilidades HTTP compartidas por los endpoints de agregados: <c>If-Match</c>, <c>ETag</c>, historial y paginación.</summary>
public static class Peticiones
{
    /// <summary>Mismas convenciones JSON que el resto de la API: camelCase y enums como texto.</summary>
    private static readonly JsonSerializerOptions OpcionesJson = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() }
    };

    /// <summary><c>If-Match: "7"</c> o <c>If-Match: 7</c> ⇒ concurrencia optimista sobre la versión del stream.</summary>
    public static long? VersionEsperada(HttpContext http)
    {
        var valor = http.Request.Headers.IfMatch.ToString().Trim('"', ' ', 'W', '/');
        if (string.IsNullOrEmpty(valor))
            return null;
        return long.TryParse(valor, out var version)
            ? version
            : throw ReglaDeNegocioException.Validacion("if-match-invalido", "If-Match debe ser la versión numérica del recurso.");
    }

    /// <summary><c>ETag: "{version}"</c>: el valor que el cliente devuelve en <c>If-Match</c>.</summary>
    public static void EscribirETag(HttpContext http, long version) =>
        http.Response.Headers.ETag = $"\"{version.ToString(CultureInfo.InvariantCulture)}\"";

    /// <summary>Línea de tiempo de un stream con el actor y el sistema de los metadatos de cada evento.</summary>
    public static IReadOnlyList<EventoHistorialDto> Historial(IEnumerable<IEvent> eventos) =>
    [
        .. eventos.Select(e => new EventoHistorialDto(
            e.Version,
            e.EventTypeName,
            e.Timestamp,
            Cabecera(e, "actorTipo"),
            Cabecera(e, "actorId"),
            Cabecera(e, "sistema"),
            JsonSerializer.SerializeToElement(e.Data, e.EventType, OpcionesJson)))
    ];

    /// <summary>Cursor opaco por posición (base64 del desplazamiento); estable mientras no cambie el orden de los datos.</summary>
    public static int LeerDesplazamiento(string? cursor)
    {
        if (string.IsNullOrWhiteSpace(cursor))
            return 0;
        try
        {
            var texto = Encoding.UTF8.GetString(Convert.FromBase64String(cursor));
            return texto.StartsWith("o:", StringComparison.Ordinal) && int.TryParse(texto[2..], CultureInfo.InvariantCulture, out var n) && n >= 0
                ? n
                : throw new FormatException();
        }
        catch (FormatException)
        {
            throw ReglaDeNegocioException.Validacion("cursor-invalido", "El cursor no es válido.");
        }
    }

    public static string EscribirDesplazamiento(int desplazamiento) =>
        Convert.ToBase64String(Encoding.UTF8.GetBytes("o:" + desplazamiento.ToString(CultureInfo.InvariantCulture)));

    /// <summary>
    /// Reintenta una operación que consume una secuencia o escribe varios streams cuando pierde una carrera de
    /// concurrencia optimista (folio, vínculo). Cada intento debe abrir su propia sesión.
    /// </summary>
    public static async Task<T> ConReintentosAsync<T>(Func<Task<T>> accion, int intentos = 10)
    {
        for (var intento = 1; ; intento++)
        {
            try
            {
                return await accion();
            }
            catch (Exception e) when (intento < intentos && EsCarrera(e))
            {
                await Task.Delay(Random.Shared.Next(5, 25 * intento));
            }
        }
    }

    /// <summary>La escritura perdió una carrera (concurrencia optimista, stream o clave única).</summary>
    public static bool EsCarrera(Exception e) =>
        e is ConcurrencyException or EventStreamUnexpectedMaxEventIdException or DocumentAlreadyExistsException ||
        e is MartenCommandException { InnerException: Npgsql.PostgresException { SqlState: Npgsql.PostgresErrorCodes.UniqueViolation } };

    private static string? Cabecera(IEvent evento, string nombre) =>
        evento.Headers is not null && evento.Headers.TryGetValue(nombre, out var valor) ? valor?.ToString() : null;
}
