using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using System.Text.Encodings.Web;
using Konscious.Security.Cryptography;
using Marten;
using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>Contexto de la petición; lo llena <see cref="MiddlewareDeContexto"/> a partir de la credencial y las cabeceras.</summary>
public sealed class ContextoSolicitud : IContextoSolicitud
{
    public Guid? TenantId { get; set; }

    public Guid? EmpresaId { get; set; }

    public ActorTipo ActorTipo { get; set; } = ActorTipo.Sistema;

    public string ActorId { get; set; } = "anonimo";

    public string CorrelationId { get; set; } = Guid.NewGuid().ToString("N");

    public string? Sistema { get; set; }

    public IReadOnlySet<string> Scopes { get; set; } = new HashSet<string>();

    public IReadOnlySet<string> Empresas { get; set; } = new HashSet<string>();
}

/// <summary>
/// <see cref="IContextoSolicitud"/> que lee el contexto guardado por <c>MiddlewareDeContexto</c> en la petición.
/// Wolverine construye las dependencias en el código que genera; leer del <c>HttpContext</c> garantiza que todos
/// (middleware, endpoints, sesiones) vean la misma instancia.
/// </summary>
public sealed class ContextoDesdeHttp(Microsoft.AspNetCore.Http.IHttpContextAccessor accesor) : IContextoSolicitud
{
    private static readonly object Clave = new();

    private ContextoSolicitud Actual => accesor.HttpContext is { } http ? De(http) : new ContextoSolicitud();

    public static ContextoSolicitud De(Microsoft.AspNetCore.Http.HttpContext http)
    {
        if (http.Items.TryGetValue(Clave, out var valor) && valor is ContextoSolicitud contexto)
            return contexto;

        var nuevo = new ContextoSolicitud();
        http.Items[Clave] = nuevo;
        return nuevo;
    }

    public Guid? TenantId => Actual.TenantId;

    public Guid? EmpresaId => Actual.EmpresaId;

    public ActorTipo ActorTipo => Actual.ActorTipo;

    public string ActorId => Actual.ActorId;

    public string CorrelationId => Actual.CorrelationId;

    public string? Sistema => Actual.Sistema;

    public IReadOnlySet<string> Scopes => Actual.Scopes;

    public IReadOnlySet<string> Empresas => Actual.Empresas;
}

public static class Reclamos
{
    public const string Tenant = "validasoft:tenant";
    public const string Scope = "scope";
    public const string Empresa = "validasoft:empresa";
    public const string ActorTipo = "validasoft:actor-tipo";

    /// <summary>Scope que solo vale con esa empresa activa: <c>{scope}|{empresaId}</c> (roles por empresa, ADR-0011).</summary>
    public const string ScopeEmpresa = "validasoft:scope-empresa";

    /// <summary>Código del sistema integrado de la credencial (SRM, MARCO…).</summary>
    public const string Sistema = "validasoft:sistema";

    public const string SistemaId = "validasoft:sistema-id";

    /// <summary>Identificador del actor en los tokens de sesión (usuario de Validasoft).</summary>
    public const string Sujeto = "sub";

    /// <summary>Nombre visible del operador en la sesión de Plataforma (PT-4).</summary>
    public const string Nombre = "name";

    /// <summary>Familia de la sesión directa (ING-1): cerrar o revocar la sesión corta el token de acceso en segundos.</summary>
    public const string SesionId = Aplicacion.Identidad.ReclamosDeIngreso.SesionId;

    /// <summary>Cómo se autenticó la sesión directa: <c>Codigo</c>, <c>Passkey</c> o <c>CodigoYPasskey</c>.</summary>
    public const string Metodo = Aplicacion.Identidad.ReclamosDeIngreso.Metodo;
}

/// <summary>
/// API keys con formato <c>vsk_{entorno}_{slugTenant}_{secreto}</c> (docs/07 §4). Solo se guarda el hash
/// Argon2id del valor completo y un prefijo de 8 caracteres para buscarla.
/// </summary>
public sealed class GeneradorDeApiKeys(IHostEnvironment entorno) : IGeneradorDeApiKeys
{
    private const int Memoria = 19 * 1024; // KiB — mínimo recomendado por OWASP para Argon2id
    private const int Iteraciones = 2;

    public (ApiKey Llave, string Valor) Generar(string slugTenant, string nombre, IEnumerable<string> scopes,
        IEnumerable<string> empresas, DateTimeOffset? expiraEn, bool sandbox, DateTimeOffset ahora)
    {
        var secreto = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(32));
        var prefijoEntorno = sandbox ? "test" : entorno.IsProduction() ? "live" : "dev";
        var valor = $"vsk_{prefijoEntorno}_{slugTenant}_{secreto}";

        var llave = new ApiKey
        {
            Id = Guid.CreateVersion7(),
            Nombre = nombre,
            Prefijo = secreto[..8],
            Hash = Hashear(valor),
            Scopes = [.. scopes.Distinct()],
            Empresas = [.. empresas.Distinct()],
            Sandbox = sandbox,
            CreadaEn = ahora,
            ExpiraEn = expiraEn
        };
        return (llave, valor);
    }

    public static string Hashear(string valor)
    {
        var sal = RandomNumberGenerator.GetBytes(16);
        return $"argon2id$m={Memoria},t={Iteraciones},p=1${Convert.ToBase64String(sal)}${Convert.ToBase64String(Calcular(valor, sal))}";
    }

    public static bool Verificar(string valor, string hash)
    {
        var partes = hash.Split('$');
        if (partes.Length != 4 || partes[0] != "argon2id")
            return false;

        var esperado = Convert.FromBase64String(partes[3]);
        return CryptographicOperations.FixedTimeEquals(Calcular(valor, Convert.FromBase64String(partes[2])), esperado);
    }

    private static byte[] Calcular(string valor, byte[] sal)
    {
        using var argon = new Argon2id(Encoding.UTF8.GetBytes(valor))
        {
            Salt = sal,
            DegreeOfParallelism = 1,
            MemorySize = Memoria,
            Iterations = Iteraciones
        };
        return argon.GetBytes(32);
    }
}

/// <summary>
/// Marca en la caché del nodo el momento en que cambió una API key (revocada o rotada, CN-3): una verificación guardada antes
/// de esa marca se descarta y la llave se vuelve a verificar contra la base.
/// </summary>
public sealed class CacheDeCredenciales(IMemoryCache cache, TimeProvider reloj) : ICacheDeCredenciales
{
    private static string Clave(string apiKeyId) => "apikey-cambio:" + apiKeyId;

    public void Olvidar(Guid apiKeyId) => cache.Set(Clave(apiKeyId.ToString()), reloj.GetUtcNow(), TimeSpan.FromMinutes(10));

    internal static bool CambioDespuesDe(IMemoryCache cache, string apiKeyId, DateTimeOffset verificadaEn) =>
        cache.TryGetValue(Clave(apiKeyId), out DateTimeOffset cambio) && cambio >= verificadaEn;
}

/// <summary>
/// Autenticación por <c>X-Api-Key</c>. La llave de arranque de Plataforma (<c>Validasoft:Plataforma:ClaveBootstrap</c>)
/// solo sirve para administrar tenants mientras llega el login OIDC (H1, ADR-0003). Las verificaciones exitosas se
/// guardan 5 minutos en memoria para no pagar Argon2 en cada petición. Con <c>X-Usuario-Delegado</c>, la llave de un
/// sistema integrado actúa en nombre de una persona (<see cref="VerificadorDeIdentidadDelegada"/>, ADR-0011).
/// </summary>
public sealed class AutenticacionPorApiKey(
    IOptionsMonitor<AuthenticationSchemeOptions> opciones,
    ILoggerFactory logger,
    UrlEncoder codificador,
    IDocumentStore store,
    IMemoryCache cache,
    IConfiguration configuracion,
    TimeProvider reloj,
    VerificadorDeIdentidadDelegada delegacion) : AuthenticationHandler<AuthenticationSchemeOptions>(opciones, logger, codificador)
{
    public const string Esquema = "ApiKey";
    public const string Cabecera = "X-Api-Key";

    private sealed record LlaveValidada(
        string TenantId, string ApiKeyId, IReadOnlyList<string> Scopes, IReadOnlyList<string> Empresas, DateTimeOffset? ExpiraEn,
        Guid? SistemaId, string? Sistema)
    {
        /// <summary>Cuándo se verificó contra la base (reloj de Validasoft).</summary>
        public DateTimeOffset VerificadaEn { get; init; }

        /// <summary>Hasta cuándo vale la verificación guardada, según el reloj de Validasoft (como mucho 5 minutos).</summary>
        public DateTimeOffset CacheadaHasta { get; init; }
    }

    protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
    {
        if (!Request.Headers.TryGetValue(Cabecera, out var encabezado) || string.IsNullOrWhiteSpace(encabezado))
            return AuthenticateResult.NoResult();

        var valor = encabezado.ToString().Trim();

        if (EsClaveDePlataforma(valor))
        {
            var soporte = Request.Headers[CabeceraAccesoSoporte].ToString();
            if (string.IsNullOrWhiteSpace(soporte))
                return Exito([new(Reclamos.Scope, Permisos.PlataformaTenants), new(Reclamos.ActorTipo, ActorTipo.Plataforma.ToString()),
                    new(ClaimTypes.NameIdentifier, Aplicacion.Plataforma.ActoresDePlataforma.Bootstrap)]);
            return await ConAccesoDeSoporteAsync(soporte.Trim());
        }

        var claveCache = "apikey:" + Convert.ToHexString(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));
        var ahora = reloj.GetUtcNow();
        // La verificación guardada vence con el reloj de Validasoft (no solo con el de la caché): una llave que expira o que se
        // rotó (CN-3) deja de valer a su hora, y como mucho 5 minutos después si cambió mientras estaba en caché.
        // También se vuelve a verificar si el reloj retrocedió desde la verificación guardada.
        if (!cache.TryGetValue(claveCache, out LlaveValidada? validada) || validada is null || validada.CacheadaHasta <= ahora ||
            validada.VerificadaEn > ahora || CacheDeCredenciales.CambioDespuesDe(cache, validada.ApiKeyId, validada.VerificadaEn))
        {
            validada = await ValidarAsync(valor);
            if (validada is null)
            {
                cache.Remove(claveCache);
                return AuthenticateResult.Fail("API key inválida, revocada o expirada.");
            }

            var vence = ahora.AddMinutes(5);
            validada = validada with { VerificadaEn = ahora, CacheadaHasta = validada.ExpiraEn is { } expira && expira < vence ? expira : vence };
            cache.Set(claveCache, validada, TimeSpan.FromMinutes(5));
        }

        var asercion = Request.Headers[VerificadorDeIdentidadDelegada.Cabecera].ToString();
        if (!string.IsNullOrWhiteSpace(asercion))
        {
            if (validada.SistemaId is not { } sistemaId)
                throw ReglaDeNegocioException.NoAutenticado("llave-sin-sistema",
                    "Solo la API key de un sistema integrado puede actuar en nombre de un usuario (X-Usuario-Delegado).");

            return Exito(await delegacion.VerificarAsync(asercion.Trim(), Guid.Parse(validada.TenantId), sistemaId, validada.ApiKeyId,
                validada.Scopes, validada.Empresas, Http.MiddlewareDeContexto.Correlacion(Context), Context.RequestAborted));
        }

        var reclamos = new List<Claim>
        {
            new(Reclamos.Tenant, validada.TenantId),
            new(Reclamos.ActorTipo, ActorTipo.Integracion.ToString()),
            new(ClaimTypes.NameIdentifier, validada.ApiKeyId)
        };
        if (validada.SistemaId is not null)
        {
            reclamos.Add(new(Reclamos.SistemaId, validada.SistemaId.Value.ToString()));
            reclamos.Add(new(Reclamos.Sistema, validada.Sistema!));
        }

        reclamos.AddRange(validada.Scopes.Select(s => new Claim(Reclamos.Scope, s)));
        reclamos.AddRange(validada.Empresas.Select(e => new Claim(Reclamos.Empresa, e)));
        return Exito(reclamos);
    }

    /// <summary>Id de un acceso de soporte vigente (B-6) con el que la Plataforma entra a una empresa de un tenant.</summary>
    public const string CabeceraAccesoSoporte = "X-Acceso-Soporte";

    /// <summary>
    /// La Plataforma con un acceso de soporte vigente actúa en el tenant y la empresa que se lo concedió, solo con los
    /// permisos de lectura de soporte (sin documentos). Expirado, revocado o inexistente: 401 sin pistas.
    /// </summary>
    private async Task<AuthenticateResult> ConAccesoDeSoporteAsync(string id)
    {
        if (!Guid.TryParse(id, out var accesoId))
            return AuthenticateResult.Fail("Acceso de soporte inválido.");

        await using var global = store.QuerySession();
        var acceso = await global.Query<AccesoSoporte>().Where(a => a.AnyTenant() && a.Id == accesoId).FirstOrDefaultAsync();
        if (acceso is null || !acceso.VigenteEn(reloj.GetUtcNow()))
            return AuthenticateResult.Fail("El acceso de soporte no existe, expiró o fue revocado.");

        var reclamos = new List<Claim>
        {
            new(Reclamos.Tenant, acceso.TenantId.ToString()),
            new(Reclamos.ActorTipo, ActorTipo.Plataforma.ToString()),
            new(ClaimTypes.NameIdentifier, $"plataforma-soporte:{acceso.Id}"),
            new(Reclamos.Empresa, acceso.EmpresaId.ToString())
        };
        reclamos.AddRange(Permisos.DeSoporte.Select(s => new Claim(Reclamos.Scope, s)));
        return Exito(reclamos);
    }

    private bool EsClaveDePlataforma(string valor)
    {
        var maestra = configuracion["Validasoft:Plataforma:ClaveBootstrap"];
        if (string.IsNullOrEmpty(maestra) || maestra.Length < 32)
            return false;

        return CryptographicOperations.FixedTimeEquals(
            SHA256.HashData(Encoding.UTF8.GetBytes(valor)), SHA256.HashData(Encoding.UTF8.GetBytes(maestra)));
    }

    private async Task<LlaveValidada?> ValidarAsync(string valor)
    {
        var partes = valor.Split('_');
        if (partes.Length != 4 || partes[0] != "vsk" || partes[3].Length != 64)
            return null;

        await using var global = store.QuerySession();
        var tenant = await global.Query<Tenant>().FirstOrDefaultAsync(t => t.Slug == partes[2]);
        if (tenant is null || !tenant.Activo)
            return null;

        await using var sesion = store.QuerySession(tenant.Id.ToString("N"));
        var prefijo = partes[3][..8];
        var candidatas = await sesion.Query<ApiKey>().Where(k => k.Prefijo == prefijo).ToListAsync();
        var llave = candidatas.FirstOrDefault(k => k.VigenteEn(reloj.GetUtcNow()) && GeneradorDeApiKeys.Verificar(valor, k.Hash));
        if (llave is null)
            return null;

        // La llave de un sistema desactivado deja de autenticar (como mucho 5 minutos después, por la caché).
        string? codigoSistema = null;
        if (llave.SistemaId is { } sistemaId)
        {
            var sistema = await sesion.LoadAsync<SistemaIntegrado>(sistemaId);
            if (sistema is null || !sistema.Activo)
                return null;
            codigoSistema = sistema.Codigo;
        }

        return new LlaveValidada(tenant.Id.ToString(), llave.Id.ToString(), llave.Scopes, llave.Empresas, llave.ExpiraEn,
            llave.SistemaId, codigoSistema);
    }

    private AuthenticateResult Exito(IEnumerable<Claim> reclamos)
    {
        var identidad = new ClaimsIdentity(reclamos, Esquema, ClaimTypes.NameIdentifier, ClaimTypes.Role);
        return AuthenticateResult.Success(new AuthenticationTicket(new ClaimsPrincipal(identidad), Esquema));
    }
}
