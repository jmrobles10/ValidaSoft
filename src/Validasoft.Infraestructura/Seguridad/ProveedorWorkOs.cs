using System.Security.Cryptography;
using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.WebUtilities;
using Microsoft.Extensions.Logging;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Wolverine;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Adaptador de WorkOS (PI-6), detrás de configuración: el proveedor de identidad del cliente (SAML u OIDC, Azure AD, Okta, Google…) se conecta a WorkOS y
/// Validasoft habla solo con WorkOS. Se activa con <c>Validasoft:Ingreso:Sso:WorkOs:{ApiKey, ClientId}</c> (sin credenciales en el repositorio); sin ellas no está
/// disponible y el grupo no puede elegirlo. Cada grupo indica su organización o su conexión de WorkOS. El canje del código devuelve el perfil verificado por
/// WorkOS (correo, nombre, grupos); WorkOS no informa cuántos factores usó la persona, así que el ingreso cuenta como de un factor (con passkey donde se exija).
/// </summary>
public sealed class ProveedorWorkOs(ConfiguracionesDeSso configuraciones, OpcionesDeSso opciones, IHttpClientFactory http, ILogger<ProveedorWorkOs> logger)
    : IProveedorDeIdentidad
{
    public const string Codigo = ProveedoresSso.WorkOs;

    public string Id => Codigo;

    public Task<InicioDeIngreso> IniciarAsync(IDocumentSession sesion, IMessageBus mensajes, string correo, bool conCuenta, ContextoDeCliente cliente,
        DateTimeOffset ahora, CancellationToken ct)
    {
        if (!opciones.WorkOsDisponible)
            throw ReglaDeNegocioException.Prohibido("sso-no-disponible", "El inicio de sesión de tu empresa no está disponible.");

        return IniciarConAsync(sesion, correo, cliente, ahora, ct);
    }

    private async Task<InicioDeIngreso> IniciarConAsync(IDocumentSession sesion, string correo, ContextoDeCliente cliente, DateTimeOffset ahora, CancellationToken ct)
    {
        var configuracion = await configuraciones.DeCorreoAsync(correo, ct);
        if (configuracion is not { Proveedor: Codigo })
            throw ReglaDeNegocioException.Prohibido("sso-no-disponible", "El inicio de sesión de tu empresa no está disponible para ese correo.");

        var nonce = Base64UrlEncoder.Encode(RandomNumberGenerator.GetBytes(24));
        var (desafio, estado, secretoDeNavegador) = DesafioDeIngreso.ParaSso(configuracion.Id, Codigo, correo, nonce, verificadorProtegido: "-", opciones.UrlDeRetorno,
            ahora, cliente.Ip, cliente.Dispositivo);
        sesion.Store(desafio);

        var parametros = new Dictionary<string, string?>
        {
            ["response_type"] = "code",
            ["client_id"] = opciones.WorkOsClientId,
            ["redirect_uri"] = opciones.UrlDeRetorno,
            ["state"] = estado,
            ["login_hint"] = correo
        };
        if (configuracion.ConexionWorkOs is { } conexion)
            parametros["connection"] = conexion;
        else if (configuracion.OrganizacionWorkOs is { } organizacion)
            parametros["organization"] = organizacion;

        return new InicioDeIngreso(desafio.Id, desafio.Expira, ProveedorOidc.MensajeDeRedireccion,
            QueryHelpers.AddQueryString($"{opciones.WorkOsUrlBase}/sso/authorize", parametros), secretoDeNavegador);
    }

    public Task<PruebaDeSsoDto> ProbarAsync(ConfiguracionDeSso configuracion, CancellationToken ct) =>
        Task.FromResult(opciones.WorkOsDisponible
            ? new PruebaDeSsoDto(true, "El adaptador de WorkOS está configurado en esta instalación. La conexión se prueba iniciando sesión con una persona del grupo.", opciones.WorkOsUrlBase,
                $"{opciones.WorkOsUrlBase}/sso/authorize")
            : new PruebaDeSsoDto(false, "Esta instalación no tiene configurado el adaptador de WorkOS.", null, null));

    public async Task<IdentidadVerificada?> CompletarAsync(DesafioDeIngreso desafio, string respuesta, DateTimeOffset ahora, CancellationToken ct)
    {
        var sso = desafio.Sso ?? throw ReglaDeNegocioException.Expirado("desafio-usado", "Este inicio de sesión no es de SSO.");
        var configuracion = await configuraciones.DeGrupoAsync(sso.TenantId, ct);
        if (!opciones.WorkOsDisponible || configuracion is not { Activo: true, Proveedor: Codigo })
            throw ReglaDeNegocioException.Prohibido("sso-no-disponible", "El inicio de sesión de tu empresa dejó de estar disponible.");

        JsonElement perfil;
        try
        {
            using var cliente = http.CreateClient(ClienteSso.Nombre);
            cliente.Timeout = TimeSpan.FromSeconds(15);
            using var peticion = new HttpRequestMessage(HttpMethod.Post, $"{opciones.WorkOsUrlBase}/sso/token")
            {
                Content = new FormUrlEncodedContent(new Dictionary<string, string>
                {
                    ["client_id"] = opciones.WorkOsClientId!,
                    ["client_secret"] = opciones.WorkOsApiKey!,
                    ["grant_type"] = "authorization_code",
                    ["code"] = respuesta
                })
            };
            using var resultado = await cliente.SendAsync(peticion, ct);
            if (!resultado.IsSuccessStatusCode)
            {
                logger.LogWarning("WorkOS rechazó el canje del código: {Estado}", (int)resultado.StatusCode);
                throw ReglaDeNegocioException.NoAutenticado("sso-canje-fallido", "WorkOS no aceptó el inicio de sesión. Empieza de nuevo.");
            }

            using var json = JsonDocument.Parse(await resultado.Content.ReadAsStringAsync(ct));
            perfil = json.RootElement.TryGetProperty("profile", out var p) && p.ValueKind == JsonValueKind.Object
                ? p.Clone()
                : throw ReglaDeNegocioException.NoAutenticado("sso-sin-token", "WorkOS no entregó el perfil de la persona.");
        }
        catch (Exception e) when (e is HttpRequestException or TaskCanceledException or JsonException)
        {
            logger.LogWarning(e, "No se pudo canjear el código con WorkOS");
            throw ReglaDeNegocioException.NoDisponible("sso-proveedor-no-responde", "El proveedor de identidad de tu empresa no respondió. Intenta de nuevo en un momento.");
        }

        // La organización del perfil debe ser la del grupo: otro cliente de WorkOS no puede entrar a este grupo.
        if (configuracion.OrganizacionWorkOs is { } organizacion && !string.Equals(ProveedorOidc.Texto(perfil, "organization_id"), organizacion, StringComparison.Ordinal))
            throw ReglaDeNegocioException.NoAutenticado("sso-organizacion-invalida", "La persona no pertenece a la organización de tu grupo en WorkOS.");
        if (configuracion.ConexionWorkOs is { } conexion && !string.Equals(ProveedorOidc.Texto(perfil, "connection_id"), conexion, StringComparison.Ordinal))
            throw ReglaDeNegocioException.NoAutenticado("sso-organizacion-invalida", "La persona no entró por la conexión de tu grupo en WorkOS.");

        var correo = ProveedorOidc.Texto(perfil, "email");
        if (correo is not { Length: > 3 } || !correo.Contains('@', StringComparison.Ordinal))
            throw ReglaDeNegocioException.NoAutenticado("sso-sin-correo", "WorkOS no entregó el correo de la persona.");

        var nombre = string.Join(' ', new[] { ProveedorOidc.Texto(perfil, "first_name"), ProveedorOidc.Texto(perfil, "last_name") }.Where(n => !string.IsNullOrWhiteSpace(n)));
        var grupos = ProveedorOidc.LeerLista(perfil, "groups", out var declarados);
        if (!declarados && perfil.TryGetProperty("raw_attributes", out var crudos) && crudos.ValueKind == JsonValueKind.Object)
            grupos = ProveedorOidc.LeerLista(crudos, configuracion.ReclamoDeGrupos, out declarados);

        return new IdentidadVerificada(Codigo, correo, nombre.Length == 0 ? null : nombre, EsMultifactor: false, ProveedorOidc.Texto(perfil, "id"), grupos, declarados);
    }
}
