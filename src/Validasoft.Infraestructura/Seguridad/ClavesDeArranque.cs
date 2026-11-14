using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Barrera de las claves de desarrollo (CL-1): <c>appsettings.Development.json</c> y <c>infra/compose/compose.yaml</c> publican
/// en el repositorio la clave de firma de sesiones, la clave de arranque de Plataforma, la clave maestra de los secretos y la
/// semilla Ed25519 de los certificados (rotuladas «solo local»). Quien las conoce firma sesiones, descifra secretos y sella
/// certificados, así que fuera de <c>Development</c> el proceso <b>no arranca</b> si alguna de ellas conserva un valor de
/// desarrollo conocido o si falta una obligatoria. Los mensajes nombran la clave de configuración, nunca su valor.
/// </summary>
public static class ClavesDeArranque
{
    public const string ClaveFirma = "Validasoft:Sesiones:ClaveFirma";
    public const string ClaveBootstrap = "Validasoft:Plataforma:ClaveBootstrap";
    public const string ClaveMaestra = "Validasoft:Secretos:ClaveMaestra";
    public const string ClavePrivada = "Validasoft:Certificados:ClavePrivada";

    /// <summary>Todo valor de texto que contenga esto es de desarrollo (los de <c>appsettings.Development.json</c> y del compose lo llevan).</summary>
    private const string Rotulo = "solo-local";

    /// <summary>
    /// Valores de desarrollo publicados en el repositorio. Las claves en base64 no llevan rótulo, así que se reconocen por valor;
    /// <c>ClavesDeArranqueTests</c> compara esta lista con <c>appsettings.Development.json</c> y el compose para que no se desfase.
    /// </summary>
    internal static readonly IReadOnlyDictionary<string, string[]> ConocidasDeDesarrollo = new Dictionary<string, string[]>(StringComparer.Ordinal)
    {
        [ClaveFirma] = ["dev-sesiones-solo-local-cambiar-000000000001"],
        [ClaveBootstrap] = ["dev-plataforma-solo-local-cambiar-0001"],
        [ClaveMaestra] = ["HMyhDz1MEG6gvVomJ8uD3Ntu6cOs8mREw+8Cmf1Qufo="],
        [ClavePrivada] = ["zybXoUU6IPsWHMTsM/gbpz67JaDMupk6X3HtUPKcmgM="]
    };

    /// <summary>
    /// Problemas de las claves de arranque en un entorno que no es de desarrollo; vacío si todo está bien. En
    /// <c>Development</c> no se exige nada: ahí las claves publicadas son las que se quieren.
    /// </summary>
    public static IReadOnlyList<string> Problemas(IConfiguration configuracion, IHostEnvironment entorno)
    {
        if (entorno.IsDevelopment())
            return [];

        var problemas = new List<string>();
        Revisar(configuracion, ClaveFirma, obligatoria: true, problemas);
        // Si falta o es corta no se firman sesiones (IdentidadDelegada.ClaveDeFirma): se avisa al arrancar y no en el primer ingreso.
        if (configuracion[ClaveFirma] is { Length: > 0 } firma && Encoding.UTF8.GetByteCount(firma) < 32)
            problemas.Add($"{ClaveFirma} debe tener al menos 32 bytes.");
        // La de arranque de Plataforma es opcional (sin ella no hay acceso por clave de arranque; se puede desactivar desde la consola).
        Revisar(configuracion, ClaveBootstrap, obligatoria: false, problemas);
        Revisar(configuracion, ClaveMaestra, obligatoria: true, problemas);
        Revisar(configuracion, ClavePrivada, obligatoria: true, problemas);
        return problemas;
    }

    /// <summary>Lanza <see cref="InvalidOperationException"/> con todos los problemas juntos y cómo corregirlos.</summary>
    public static void Exigir(IConfiguration configuracion, IHostEnvironment entorno)
    {
        var problemas = Problemas(configuracion, entorno);
        if (problemas.Count == 0)
            return;

        throw new InvalidOperationException(
            $"Validasoft no arranca en el entorno «{entorno.EnvironmentName}» con claves de desarrollo o sin las claves obligatorias:{Environment.NewLine}" +
            string.Join(Environment.NewLine, problemas.Select(p => $"  - {p}")) + Environment.NewLine +
            "Genera claves propias para este entorno y entrégalas como secretos de configuración (variables de entorno, archivo _FILE o Key Vault); " +
            "cómo se generan está en docs/manual/guias/variables-de-entorno.md, sección «Claves propias del entorno». " +
            "Solo ASPNETCORE_ENVIRONMENT=Development admite las claves del repositorio.");
    }

    /// <summary>¿El valor es una clave de desarrollo conocida de <paramref name="clave"/> (por valor o por el rótulo «solo-local»)?</summary>
    internal static bool EsDeDesarrollo(string clave, string valor)
    {
        var limpio = valor.Trim();
        if (limpio.Contains(Rotulo, StringComparison.OrdinalIgnoreCase) || limpio.Contains("solo local", StringComparison.OrdinalIgnoreCase))
            return true;

        return ConocidasDeDesarrollo.TryGetValue(clave, out var conocidas) && conocidas.Any(c => Iguales(c, limpio));
    }

    private static void Revisar(IConfiguration configuracion, string clave, bool obligatoria, List<string> problemas)
    {
        var valor = configuracion[clave];
        if (string.IsNullOrWhiteSpace(valor))
        {
            if (obligatoria)
                problemas.Add($"{clave} falta (es obligatoria fuera de desarrollo).");
            return;
        }

        if (EsDeDesarrollo(clave, valor))
            problemas.Add($"{clave} tiene un valor de desarrollo conocido (está publicado en el repositorio): reemplázalo por uno propio.");
    }

    /// <summary>Igual como texto o, si las dos son base64, como bytes (un salto de línea o el relleno distinto no esconde la clave).</summary>
    private static bool Iguales(string conocida, string valor)
    {
        if (string.Equals(conocida, valor, StringComparison.Ordinal))
            return true;

        Span<byte> a = stackalloc byte[64];
        Span<byte> b = stackalloc byte[64];
        return Convert.TryFromBase64String(conocida, a, out var largoA) && Convert.TryFromBase64String(valor, b, out var largoB) &&
               a[..largoA].SequenceEqual(b[..largoB]);
    }
}

/// <summary>
/// Servicio de arranque que aplica <see cref="ClavesDeArranque"/>. Se registra primero en <c>AgregarValidasoft</c>: los servicios
/// de fondo arrancan en orden de registro y antes que el servidor web, así que ni siquiera se abre el puerto con claves de
/// desarrollo. Cualquier proceso que componga Validasoft (la API o uno de fondo) la hereda.
/// </summary>
public sealed class VerificacionDeClavesDeArranque(IConfiguration configuracion, IHostEnvironment entorno) : IHostedService
{
    public Task StartAsync(CancellationToken cancellationToken)
    {
        ClavesDeArranque.Exigir(configuracion, entorno);
        return Task.CompletedTask;
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
