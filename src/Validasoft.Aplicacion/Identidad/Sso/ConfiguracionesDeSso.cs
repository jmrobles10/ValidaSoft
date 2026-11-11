using Marten;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>
/// Opciones del SSO que dependen de la instalación, no del grupo (PI-1, PI-6): si se admite un proveedor en <c>http://localhost</c> (desarrollo y pruebas), la
/// dirección a la que el proveedor devuelve a la persona y si el adaptador de WorkOS está configurado.
/// </summary>
public sealed class OpcionesDeSso(IConfiguration configuracion)
{
    public const string Seccion = "Validasoft:Ingreso:Sso";

    /// <summary>Un proveedor en <c>http://localhost</c> o en una red privada (solo desarrollo y pruebas); en producción, solo https público.</summary>
    public bool PermitirHttpLocal => configuracion.GetValue($"{Seccion}:PermitirHttpLocal", false);

    /// <summary>La URI de redirección que se registra en el proveedor de identidad: el <i>callback</i> de la API.</summary>
    public string UrlDeRetorno =>
        configuracion[$"{Seccion}:UrlDeRetorno"] is { Length: > 0 } propia
            ? propia
            : $"{(configuracion["Validasoft:Correo:UrlPublica"] ?? "http://localhost:5173").TrimEnd('/')}/api/v1/ingreso/sso/callback";

    /// <summary>Adaptador de WorkOS: <c>Validasoft:Ingreso:Sso:WorkOs:{ApiKey, ClientId}</c> (sin credenciales en el repositorio: se activa por configuración).</summary>
    public string? WorkOsApiKey => Vacio(configuracion[$"{Seccion}:WorkOs:ApiKey"]);

    public string? WorkOsClientId => Vacio(configuracion[$"{Seccion}:WorkOs:ClientId"]);

    public string WorkOsUrlBase => (Vacio(configuracion[$"{Seccion}:WorkOs:UrlBase"]) ?? "https://api.workos.com").TrimEnd('/');

    public bool WorkOsDisponible => WorkOsApiKey is not null && WorkOsClientId is not null;

    /// <summary>A dónde se devuelve a la persona con el resultado: la app web (<see cref="RedactorDeCorreos.UrlApp"/>).</summary>
    public string UrlDeLaApp => configuracion["Validasoft:Correo:UrlApp"] is { Length: > 0 } app
        ? app.TrimEnd('/') + "/"
        : (configuracion["Validasoft:Correo:UrlPublica"] ?? "http://localhost:5173").TrimEnd('/') + "/app/";

    private static string? Vacio(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}

/// <summary>
/// Lee la configuración de SSO de un grupo y resuelve a qué grupo pertenece un correo por su dominio verificado (PI-2). La respuesta se guarda 30 segundos en
/// memoria y se descarta en todos los servidores al cambiar (<see cref="IBusDeInvalidaciones"/>).
/// </summary>
public sealed class ConfiguracionesDeSso(IDocumentStore store, IMemoryCache cache, IBusDeInvalidaciones? bus = null) : IReceptorDeInvalidaciones
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(30);

    private static string ClaveDeGrupo(Guid tenantId) => "sso:grupo:" + tenantId.ToString("N");

    private static string ClaveDeDominio(string dominio) => "sso:dominio:" + dominio;

    public string Tipo => TiposDeInvalidacion.Sso;

    /// <summary>La configuración del grupo, o nula si nunca se configuró.</summary>
    public async Task<ConfiguracionDeSso?> DeGrupoAsync(Guid tenantId, CancellationToken ct)
    {
        if (cache.TryGetValue(ClaveDeGrupo(tenantId), out ConfiguracionDeSso? guardada))
            return guardada;

        await using var consulta = store.QuerySession();
        var configuracion = await consulta.LoadAsync<ConfiguracionDeSso>(tenantId, ct);
        cache.Set(ClaveDeGrupo(tenantId), configuracion, Vigencia);
        return configuracion;
    }

    /// <summary>
    /// La configuración que aplica a un correo: la del grupo dueño de su dominio verificado, con el SSO activo. Nula si el dominio no es de ningún grupo, el SSO
    /// está apagado o el correo no es de un dominio verificado de ese grupo.
    /// </summary>
    public async Task<ConfiguracionDeSso?> DeCorreoAsync(string? correo, CancellationToken ct)
    {
        var dominio = ConfiguracionDeSso.DominioDe(correo);
        if (dominio is null)
            return null;

        Guid? tenantId;
        if (cache.TryGetValue(ClaveDeDominio(dominio), out Guid? guardado))
            tenantId = guardado;
        else
        {
            await using var consulta = store.QuerySession();
            tenantId = (await consulta.LoadAsync<DominioVerificadoDeSso>(dominio, ct))?.TenantId;
            cache.Set(ClaveDeDominio(dominio), tenantId, Vigencia);
        }

        if (tenantId is not { } grupo)
            return null;
        var configuracion = await DeGrupoAsync(grupo, ct);
        return configuracion is not null && correo is not null && configuracion.AplicaA(correo) ? configuracion : null;
    }

    /// <summary>Avisa a todos los servidores de que cambió la configuración del grupo (y sus dominios).</summary>
    public void Olvidar(Guid tenantId, IEnumerable<string>? dominios = null)
    {
        Quitar(tenantId, dominios);
        bus?.Publicar(Tipo, tenantId.ToString("N") + (dominios is null ? string.Empty : "|" + string.Join(',', dominios)));
    }

    public void Recibir(string id)
    {
        var partes = id.Split('|', 2);
        if (Guid.TryParseExact(partes[0], "N", out var tenantId))
            Quitar(tenantId, partes.Length > 1 ? partes[1].Split(',', StringSplitOptions.RemoveEmptyEntries) : null);
    }

    /// <summary>Se perdió la escucha de avisos: lo guardado vence solo en 30 segundos.</summary>
    public void Reiniciar()
    {
    }

    private void Quitar(Guid tenantId, IEnumerable<string>? dominios)
    {
        cache.Remove(ClaveDeGrupo(tenantId));
        foreach (var dominio in dominios ?? [])
            cache.Remove(ClaveDeDominio(dominio));
    }
}

/// <summary>
/// Comprueba que quien pide verificar un dominio lo controla (PI-2): busca <paramref name="token"/> en
/// <c>https://{dominio}/.well-known/validasoft-verificacion.txt</c>. La implementación real vive en infraestructura; las pruebas la reemplazan.
/// </summary>
public interface IComprobadorDeDominios
{
    /// <summary><c>true</c> si el dominio publica el token. Nunca lanza por un fallo de red: lo que no se pueda comprobar es «no verificado».</summary>
    Task<bool> ComprobarAsync(string dominio, string token, CancellationToken ct);
}
