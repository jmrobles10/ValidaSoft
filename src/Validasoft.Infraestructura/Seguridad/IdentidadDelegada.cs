using System.Security.Claims;
using System.Security.Cryptography;
using System.Text;
using Microsoft.Extensions.Configuration;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Identidad delegada (ADR-0011): verifica la aserción <c>X-Usuario-Delegado</c> — JWT RS256/ES256 firmado por el
/// sistema integrado de la API key, <c>iss</c> = su código, <c>aud</c> = <c>validasoft</c>, vence en ≤ 5 minutos, con
/// <c>jti</c> de un solo uso (SE-4) — y arma el principal del <b>usuario</b> con el permiso efectivo (intersección de
/// llave y roles).
/// </summary>
public sealed class VerificadorDeIdentidadDelegada(RegistroDeUsuariosDelegados usuarios, RegistroDeAsercionesUsadas usadas, PoliticaDeIngreso politica, TimeProvider reloj)
{
    public const string Cabecera = "X-Usuario-Delegado";
    public const string Audiencia = "validasoft";

    /// <summary>Longitud máxima del <c>jti</c> (un UUID tiene 36).</summary>
    public const int LongitudMaximaJti = 128;

    internal static readonly TimeSpan Tolerancia = TimeSpan.FromSeconds(30);
    private static readonly TimeSpan VigenciaMaxima = TimeSpan.FromMinutes(5);
    private static readonly JsonWebTokenHandler Manejador = new() { MapInboundClaims = false };

    public async Task<List<Claim>> VerificarAsync(string asercion, Guid tenantId, Guid sistemaId, string apiKeyId,
        IReadOnlyList<string> scopesLlave, IReadOnlyList<string> empresasLlave, string correlationId, CancellationToken ct)
    {
        var sistema = await usuarios.CargarSistemaAsync(tenantId, sistemaId, ct);
        if (sistema is null || !sistema.Activo)
            throw ReglaDeNegocioException.NoAutenticado("sistema-inactivo", "El sistema integrado de la API key no existe o está desactivado.");
        // Cada grupo elige cómo entran sus personas (ING-1): sin el modo Delegado, la aserción del sistema no abre sesión.
        await politica.AsegurarModoAsync(tenantId, ModoDeIngreso.Delegado, ct);

        var (datos, jti, expira) = await ValidarAsync(asercion, sistema.Codigo, sistema.ClavePublicaPem, reloj.GetUtcNow());
        // Después de la firma (nadie llena el registro con aserciones falsas) y antes de tocar al usuario.
        await usadas.RegistrarAsync(sistema.Id, jti, reloj.GetUtcNow(), expira + Tolerancia, ct);
        var usuario = await usuarios.ResolverAsync(tenantId, sistema, apiKeyId, datos, correlationId, ct);
        var permisos = Permisos.ParaUsuario(usuario.Roles, scopesLlave, empresasLlave);

        var reclamos = new List<Claim>
        {
            new(Reclamos.Tenant, tenantId.ToString()),
            new(Reclamos.ActorTipo, ActorTipo.Usuario.ToString()),
            new(ClaimTypes.NameIdentifier, usuario.Id.ToString()),
            new(Reclamos.SistemaId, sistema.Id.ToString()),
            new(Reclamos.Sistema, sistema.Codigo)
        };
        reclamos.AddRange(permisos.DeTenant.Select(s => new Claim(Reclamos.Scope, s)));
        reclamos.AddRange(permisos.PorEmpresa.SelectMany(p => p.Value.Select(s => new Claim(Reclamos.ScopeEmpresa, $"{s}|{p.Key}"))));
        reclamos.AddRange(permisos.Empresas.Select(e => new Claim(Reclamos.Empresa, e)));
        return reclamos;
    }

    /// <summary>
    /// Verifica firma, emisor (<paramref name="codigo"/>), audiencia, vigencia (≤ 5 min), <c>jti</c> y <c>sub</c>/<c>name</c>/<c>email</c>.
    /// Lo usan también los operadores de Plataforma (PT-4), con la clave de su sistema de plataforma.
    /// </summary>
    internal static async Task<(AsercionDelegada Datos, string Jti, DateTimeOffset Expira)> ValidarAsync(string asercion, string codigo,
        string clavePublicaPem, DateTimeOffset ahora)
    {
        var parametros = new TokenValidationParameters
        {
            ValidIssuer = codigo,
            ValidAudience = Audiencia,
            IssuerSigningKey = ClaveDeVerificacion(clavePublicaPem),
            ValidAlgorithms = [SecurityAlgorithms.RsaSha256, SecurityAlgorithms.EcdsaSha256],
            // La vigencia se valida abajo con el reloj de Validasoft, para exigir además el máximo de 5 minutos.
            ValidateLifetime = false
        };

        var resultado = await Manejador.ValidateTokenAsync(asercion, parametros);
        if (!resultado.IsValid || resultado.SecurityToken is not JsonWebToken jwt)
        {
            throw resultado.Exception is SecurityTokenInvalidSignatureException
                ? ReglaDeNegocioException.NoAutenticado("asercion-firma-invalida",
                    "La firma de X-Usuario-Delegado no corresponde a la clave pública registrada del sistema.")
                : ReglaDeNegocioException.NoAutenticado("asercion-invalida",
                    $"X-Usuario-Delegado no es válido: debe ser un JWT RS256/ES256 con iss = {codigo} y aud = {Audiencia}.");
        }

        if (jwt.ValidTo == DateTime.MinValue)
            throw ReglaDeNegocioException.NoAutenticado("asercion-invalida", "La aserción necesita exp.");
        var expira = new DateTimeOffset(jwt.ValidTo, TimeSpan.Zero);
        if (expira < ahora - Tolerancia)
            throw ReglaDeNegocioException.NoAutenticado("asercion-vencida", "La aserción X-Usuario-Delegado ya venció.");
        if (expira > ahora + VigenciaMaxima + Tolerancia)
            throw ReglaDeNegocioException.NoAutenticado("asercion-vigencia-excesiva", "La aserción debe vencer en 5 minutos o menos.");
        if (jwt.ValidFrom != DateTime.MinValue && new DateTimeOffset(jwt.ValidFrom, TimeSpan.Zero) > ahora + Tolerancia)
            throw ReglaDeNegocioException.NoAutenticado("asercion-invalida", "La aserción aún no es válida (nbf).");

        var jti = jwt.Id;
        if (string.IsNullOrWhiteSpace(jti))
            throw ReglaDeNegocioException.NoAutenticado("asercion-sin-jti",
                "La aserción debe traer jti: un identificador único (p. ej. un UUID) por aserción; cada una vale una sola vez.");
        if (jti.Length > LongitudMaximaJti || jti.Any(char.IsControl))
            throw ReglaDeNegocioException.NoAutenticado("asercion-invalida", $"El jti admite hasta {LongitudMaximaJti} caracteres imprimibles.");

        var sub = jwt.Subject;
        jwt.TryGetPayloadValue<string>("name", out var nombre);
        jwt.TryGetPayloadValue<string>("email", out var correo);
        if (string.IsNullOrWhiteSpace(sub) || string.IsNullOrWhiteSpace(nombre) || string.IsNullOrWhiteSpace(correo))
            throw ReglaDeNegocioException.NoAutenticado("asercion-incompleta", "La aserción debe traer sub, name y email.");

        try
        {
            return (new AsercionDelegada(sub.Trim(), nombre, Usuario.NormalizarCorreo(correo)), jti, expira);
        }
        catch (ReglaDeNegocioException)
        {
            throw ReglaDeNegocioException.NoAutenticado("asercion-incompleta", "El email de la aserción no es válido.");
        }
    }

    /// <summary>La clave ya se validó al registrarla (RSA ≥ 2048 o EC P-256).</summary>
    private static SecurityKey ClaveDeVerificacion(string pem)
    {
        try
        {
            var rsa = RSA.Create();
            rsa.ImportFromPem(pem);
            return new RsaSecurityKey(rsa);
        }
        catch (CryptographicException)
        {
            var ec = ECDsa.Create();
            ec.ImportFromPem(pem);
            return new ECDsaSecurityKey(ec);
        }
    }
}

/// <summary>
/// Sesiones cortas de Validasoft para pantallas embebidas (ID-3): JWT HS256 de 30 minutos firmado con
/// <c>Validasoft:Sesiones:ClaveFirma</c> que copia el permiso efectivo del usuario delegado. Lo valida el esquema
/// <c>Bearer</c>.
/// </summary>
public sealed class EmisorDeSesiones(IConfiguration configuracion) : IEmisorDeSesiones
{
    public const string Emisor = "validasoft";
    public const string Audiencia = "validasoft:sesion";
    public static readonly TimeSpan Duracion = TimeSpan.FromMinutes(30);

    private static readonly string[] ReclamosCopiados =
        [Reclamos.Tenant, Reclamos.ActorTipo, Reclamos.Sistema, Reclamos.SistemaId, Reclamos.Scope, Reclamos.ScopeEmpresa, Reclamos.Empresa];

    public SesionUsuarioDto Emitir(ClaimsPrincipal principal, DateTimeOffset ahora)
    {
        var delegada = principal.Identity?.AuthenticationType == AutenticacionPorApiKey.Esquema &&
                       principal.FindFirstValue(Reclamos.ActorTipo) == ActorTipo.Usuario.ToString();
        if (!delegada)
            throw ReglaDeNegocioException.Validacion("usuario-delegado-requerido",
                "La sesión se emite con la API key del sistema integrado y la cabecera X-Usuario-Delegado.");

        var reclamos = principal.Claims.Where(c => ReclamosCopiados.Contains(c.Type)).Select(c => new Claim(c.Type, c.Value)).ToList();
        reclamos.Add(new Claim(Reclamos.Sujeto, principal.FindFirstValue(ClaimTypes.NameIdentifier)!));

        return Firmar(reclamos, ahora);
    }

    /// <summary>
    /// Sesión de la consola de Plataforma (PT-4): sin tenant ni empresas; actor <c>Plataforma</c> con el id del operador,
    /// su sistema, su nombre (<c>name</c>, para mostrarlo) y solo el scope <c>plataforma:tenants</c>.
    /// </summary>
    public SesionUsuarioDto EmitirPlataforma(OperadorDePlataforma operador, DateTimeOffset ahora) =>
        Firmar(
        [
            new Claim(Reclamos.Sujeto, operador.Id),
            new Claim(Reclamos.ActorTipo, ActorTipo.Plataforma.ToString()),
            new Claim(Reclamos.Sistema, operador.Sistema),
            new Claim(Reclamos.Nombre, operador.Nombre),
            new Claim(Reclamos.Scope, Permisos.PlataformaTenants)
        ], ahora);

    public SesionUsuarioDto EmitirDeUsuario(Usuario usuario, Guid tenantId, Guid sesionId, MetodoDeIngreso metodo, TimeSpan vigencia, DateTimeOffset ahora)
    {
        // Mismos reclamos que la sesión delegada: el permiso efectivo es el de los roles del usuario (la llave de un sistema no interviene).
        var permisos = Permisos.ParaUsuario(usuario.Roles, Permisos.DelTenant, ["*"]);
        var reclamos = new List<Claim>
        {
            new(Reclamos.Tenant, tenantId.ToString()),
            new(Reclamos.ActorTipo, ActorTipo.Usuario.ToString()),
            new(Reclamos.Sujeto, usuario.Id.ToString()),
            new(Reclamos.Nombre, usuario.Nombre),
            new(Reclamos.SesionId, sesionId.ToString()),
            new(Reclamos.Metodo, metodo.ToString())
        };
        reclamos.AddRange(permisos.DeTenant.Select(s => new Claim(Reclamos.Scope, s)));
        reclamos.AddRange(permisos.PorEmpresa.SelectMany(p => p.Value.Select(s => new Claim(Reclamos.ScopeEmpresa, $"{s}|{p.Key}"))));
        reclamos.AddRange(permisos.Empresas.Select(e => new Claim(Reclamos.Empresa, e)));
        return Firmar(reclamos, ahora, vigencia);
    }

    public SesionUsuarioDto EmitirDeOperador(OperadorDePlataforma operador, Guid sesionId, MetodoDeIngreso metodo, TimeSpan vigencia, DateTimeOffset ahora) =>
        Firmar(
        [
            new Claim(Reclamos.Sujeto, operador.Id),
            new Claim(Reclamos.ActorTipo, ActorTipo.Plataforma.ToString()),
            new Claim(Reclamos.Nombre, operador.Nombre),
            new Claim(Reclamos.Scope, Permisos.PlataformaTenants),
            new Claim(Reclamos.SesionId, sesionId.ToString()),
            new Claim(Reclamos.Metodo, metodo.ToString())
        ], ahora, vigencia);

    private SesionUsuarioDto Firmar(List<Claim> reclamos, DateTimeOffset ahora, TimeSpan? vigencia = null)
    {
        var expira = ahora + (vigencia ?? Duracion);
        var token = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = Emisor,
            Audience = Audiencia,
            Subject = new ClaimsIdentity(reclamos),
            IssuedAt = ahora.UtcDateTime,
            NotBefore = ahora.UtcDateTime,
            Expires = expira.UtcDateTime,
            SigningCredentials = new SigningCredentials(ClaveDeFirma(configuracion), SecurityAlgorithms.HmacSha256)
        });
        return new SesionUsuarioDto(token, expira);
    }

    /// <summary>Parámetros con los que el esquema <c>Bearer</c> valida los tokens emitidos aquí.</summary>
    public static TokenValidationParameters ParametrosDeValidacion(IConfiguration configuracion) => new()
    {
        ValidIssuer = Emisor,
        ValidAudience = Audiencia,
        IssuerSigningKey = ClaveDeFirma(configuracion),
        ValidAlgorithms = [SecurityAlgorithms.HmacSha256],
        ClockSkew = TimeSpan.FromSeconds(30),
        NameClaimType = Reclamos.Sujeto
    };

    internal static SymmetricSecurityKey ClaveDeFirma(IConfiguration configuracion)
    {
        var bytes = Encoding.UTF8.GetBytes(configuracion["Validasoft:Sesiones:ClaveFirma"] ?? string.Empty);
        return bytes.Length >= 32
            ? new SymmetricSecurityKey(bytes)
            : throw new InvalidOperationException("Validasoft:Sesiones:ClaveFirma debe tener al menos 32 bytes.");
    }
}
