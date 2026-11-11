using System.Security.Cryptography;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Identidad;

/// <summary>Tipo de proveedor de identidad del cliente (SSO, PI-1).</summary>
public static class ProveedoresSso
{
    /// <summary>OpenID Connect genérico: Microsoft Entra ID (Azure AD), Google Workspace o cualquier proveedor que publique su descubrimiento OIDC.</summary>
    public const string Oidc = "oidc";

    /// <summary>WorkOS (adaptador detrás de configuración): el proveedor de identidad del cliente se conecta a WorkOS y Validasoft habla solo con él.</summary>
    public const string WorkOs = "workos";

    public static readonly IReadOnlyList<string> Todos = [Oidc, WorkOs];
}

/// <summary>
/// Cómo conviven el SSO y el código por correo en un grupo (PI-5): <see cref="Complementario"/> deja ambos, <see cref="Exclusivo"/> hace que las personas de
/// los dominios verificados solo entren con el inicio de sesión de su empresa (ni el código del correo ni una passkey sola abren sesión).
/// </summary>
public enum ModoDeSso
{
    Complementario,
    Exclusivo
}

/// <summary>Un dominio de correo que el grupo dice ser suyo (PI-2): hasta que se comprueba, no sirve para iniciar sesión.</summary>
public sealed class DominioDeSso
{
    public string Dominio { get; set; } = string.Empty;

    /// <summary>Lo que el grupo publica en <c>https://{dominio}/.well-known/validasoft-verificacion.txt</c> para demostrar que controla el dominio.</summary>
    public string Token { get; set; } = string.Empty;

    public bool Verificado { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset? VerificadoEn { get; set; }
}

/// <summary>
/// Un dominio ya verificado y de qué grupo es (PI-2). Global (esquema <c>identidad</c>); su id es el dominio, así ningún otro grupo puede verificarlo: insertar
/// dos veces el mismo falla, también con altas simultáneas.
/// </summary>
public sealed class DominioVerificadoDeSso
{
    public string Id { get; set; } = string.Empty;

    public Guid TenantId { get; set; }

    public DateTimeOffset VerificadoEn { get; set; }
}

/// <summary>Un grupo del proveedor de identidad y el rol que da en Validasoft (PI-4); sin empresa, el rol vale en todo el grupo.</summary>
public sealed class ReglaDeGrupoSso
{
    /// <summary>Valor tal como llega en el reclamo de grupos (sin distinguir mayúsculas): un nombre, o el identificador del grupo en el proveedor.</summary>
    public string Grupo { get; set; } = string.Empty;

    public Rol Rol { get; set; }

    public Guid? EmpresaId { get; set; }
}

/// <summary>Lo que el administrador del grupo cambia de su SSO (PI-1). <c>SecretoDelCliente</c> nulo conserva el actual; vacío lo quita.</summary>
public sealed record CambiosDeSso(
    bool Activo, string Proveedor, string Nombre, string? Autoridad, string? ClientId, string? SecretoDelCliente, IReadOnlyList<string>? Alcances,
    string? ReclamoDeGrupos, string? OrganizacionWorkOs, string? ConexionWorkOs, bool AprovisionamientoJit, IReadOnlyList<RolAsignado> RolesPorOmision,
    IReadOnlyList<ReglaDeGrupoSso> Reglas, bool SincronizarRoles, ModoDeSso Modo);

/// <summary>
/// Inicio de sesión con el proveedor de identidad del cliente (SSO, PI-1…PI-6): uno por grupo, en el esquema <c>identidad</c> con el id del grupo. Dice con qué
/// proveedor se entra (OIDC genérico o WorkOS), qué dominios de correo son del grupo (solo los verificados valen), si se crea a las personas en su primer ingreso
/// (aprovisionamiento JIT) con qué rol, cómo los grupos del proveedor se vuelven roles de Validasoft y si el SSO acompaña o reemplaza al código por correo.
/// El secreto del cliente solo se guarda cifrado y nunca se devuelve.
/// </summary>
public sealed partial class ConfiguracionDeSso
{
    public const int MaximoDeDominios = 20;
    public const int MaximoDeReglas = 100;

    public const string ReclamoDeGruposPorOmision = "groups";

    public static readonly IReadOnlyList<string> AlcancesPorOmision = ["openid", "email", "profile"];

    /// <summary>El grupo (tenant) dueño de la configuración.</summary>
    public Guid Id { get; set; }

    public bool Activo { get; set; }

    /// <summary><see cref="ProveedoresSso"/>.</summary>
    public string Proveedor { get; set; } = ProveedoresSso.Oidc;

    /// <summary>Cómo se llama el inicio de sesión de la empresa en la pantalla de ingreso («Ingresar con Acme»).</summary>
    public string Nombre { get; set; } = string.Empty;

    // ---------- OIDC ----------

    /// <summary>El emisor (<c>issuer</c>) del proveedor: su descubrimiento está en <c>{Autoridad}/.well-known/openid-configuration</c>.</summary>
    public string? Autoridad { get; set; }

    public string? ClientId { get; set; }

    /// <summary>El secreto del cliente, cifrado con <c>IProtectorDeSecretos</c>; nulo si el cliente es público (solo PKCE).</summary>
    public string? SecretoProtegido { get; set; }

    public string? UltimosCuatroSecreto { get; set; }

    public List<string> Alcances { get; set; } = [.. AlcancesPorOmision];

    /// <summary>Reclamo del token del que salen los grupos (<c>groups</c> en Entra y la mayoría; <c>roles</c> en otros).</summary>
    public string ReclamoDeGrupos { get; set; } = ReclamoDeGruposPorOmision;

    // ---------- WorkOS ----------

    public string? OrganizacionWorkOs { get; set; }

    public string? ConexionWorkOs { get; set; }

    // ---------- dominios ----------

    public List<DominioDeSso> Dominios { get; set; } = [];

    // ---------- aprovisionamiento y roles ----------

    /// <summary>Crear a la persona en su primer ingreso, con los roles que le tocan (<see cref="RolesPara"/>).</summary>
    public bool AprovisionamientoJit { get; set; }

    /// <summary>Roles de quien llega sin un grupo que dé alguno (JIT) o sin reglas.</summary>
    public List<RolAsignado> RolesPorOmision { get; set; } = [];

    public List<ReglaDeGrupoSso> Reglas { get; set; } = [];

    /// <summary>En cada ingreso con SSO los roles de la persona se ponen al día con sus grupos del proveedor: el proveedor es la fuente de los roles.</summary>
    public bool SincronizarRoles { get; set; }

    public ModoDeSso Modo { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    public bool LlaveConfigurada => !string.IsNullOrEmpty(SecretoProtegido);

    public static ConfiguracionDeSso Nueva(Guid tenantId, DateTimeOffset ahora) => new() { Id = tenantId, CreadoEn = ahora, ActualizadoEn = ahora };

    /// <summary>Los dominios ya comprobados, en minúsculas.</summary>
    public IEnumerable<string> DominiosVerificados => Dominios.Where(d => d.Verificado).Select(d => d.Dominio);

    /// <summary>El dominio es de este grupo y está verificado (el del correo, sin distinguir mayúsculas).</summary>
    public bool CubreCorreo(string correo) => DominioDe(correo) is { } dominio && DominiosVerificados.Contains(dominio, StringComparer.Ordinal);

    /// <summary>El SSO vale para el correo: está activo y su dominio es uno verificado del grupo.</summary>
    public bool AplicaA(string correo) => Activo && CubreCorreo(correo);

    /// <summary>Con el modo exclusivo, quien tiene un correo de un dominio verificado solo entra con el SSO (PI-5).</summary>
    public bool ReemplazaElCodigoPara(string correo) => AplicaA(correo) && Modo == ModoDeSso.Exclusivo;

    public static string? DominioDe(string? correo)
    {
        var texto = (correo ?? string.Empty).Trim().ToLowerInvariant();
        var arroba = texto.LastIndexOf('@');
        return arroba > 0 && arroba < texto.Length - 1 ? texto[(arroba + 1)..] : null;
    }

    /// <summary>
    /// Aplica los cambios validados. 422: <c>proveedor-sso-invalido</c>, <c>nombre-sso-requerido</c>, <c>autoridad-invalida</c>, <c>client-id-requerido</c>,
    /// <c>secreto-sso-invalido</c>, <c>conexion-workos-requerida</c>, <c>reclamo-de-grupos-invalido</c>, <c>rol-por-omision-requerido</c>, <c>regla-sso-invalida</c>,
    /// <c>reglas-excesivas</c>. <paramref name="proteger"/> cifra el secreto nuevo (no se llama si no cambia); <paramref name="permitirHttpLocal"/> admite un
    /// proveedor en <c>http://localhost</c> (desarrollo y pruebas).
    /// </summary>
    public void Actualizar(CambiosDeSso cambios, Func<string, string> proteger, bool permitirHttpLocal, DateTimeOffset ahora)
    {
        var proveedor = (cambios.Proveedor ?? string.Empty).Trim().ToLowerInvariant();
        if (!ProveedoresSso.Todos.Contains(proveedor))
            throw ReglaDeNegocioException.Validacion("proveedor-sso-invalido", $"El proveedor debe ser uno de: {string.Join(", ", ProveedoresSso.Todos)}.");
        var nombre = (cambios.Nombre ?? string.Empty).Trim();
        if (nombre.Length is 0 or > 60)
            throw ReglaDeNegocioException.Validacion("nombre-sso-requerido", "El inicio de sesión necesita un nombre de hasta 60 caracteres (p. ej. «Acme»).");

        string? autoridad = null;
        string? clientId = null;
        if (proveedor == ProveedoresSso.Oidc)
        {
            autoridad = ValidarAutoridad(cambios.Autoridad, permitirHttpLocal);
            clientId = (cambios.ClientId ?? string.Empty).Trim();
            if (clientId.Length is 0 or > 300 || clientId.Any(char.IsWhiteSpace))
                throw ReglaDeNegocioException.Validacion("client-id-requerido", "Escribe el client id que el proveedor le dio a Validasoft (sin espacios).");
        }

        var organizacion = Limpio(cambios.OrganizacionWorkOs, 100);
        var conexion = Limpio(cambios.ConexionWorkOs, 100);
        if (proveedor == ProveedoresSso.WorkOs && organizacion is null && conexion is null)
            throw ReglaDeNegocioException.Validacion("conexion-workos-requerida", "Con WorkOS indica la organización o la conexión del cliente.");

        var reclamo = (cambios.ReclamoDeGrupos ?? ReclamoDeGruposPorOmision).Trim();
        if (!RegexReclamo().IsMatch(reclamo))
            throw ReglaDeNegocioException.Validacion("reclamo-de-grupos-invalido", "El reclamo de grupos admite letras, números, puntos, guiones, dos puntos y guion bajo (hasta 100).");

        var alcances = (cambios.Alcances is { Count: > 0 } a ? a : AlcancesPorOmision).Select(x => x.Trim()).Where(x => x.Length > 0).Distinct(StringComparer.Ordinal).ToList();
        if (!alcances.Contains("openid") || alcances.Count > 20 || alcances.Any(x => x.Length > 100 || x.Any(char.IsWhiteSpace)))
            throw ReglaDeNegocioException.Validacion("alcances-sso-invalidos", "Los alcances deben incluir openid y no llevar espacios.");

        var roles = cambios.RolesPorOmision.Distinct().ToList();
        if (roles.Count > 20)
            throw ReglaDeNegocioException.Validacion("rol-por-omision-requerido", "El rol por omisión admite hasta 20 roles.");
        var reglas = cambios.Reglas.Select(Regla).DistinctBy(r => (r.Grupo.ToUpperInvariant(), r.Rol, r.EmpresaId)).ToList();
        if (reglas.Count > MaximoDeReglas)
            throw ReglaDeNegocioException.Validacion("reglas-excesivas", $"Se admiten hasta {MaximoDeReglas} reglas de grupos.");
        if (cambios.AprovisionamientoJit && roles.Count == 0 && reglas.Count == 0)
            throw ReglaDeNegocioException.Validacion("rol-por-omision-requerido",
                "Para crear a las personas en su primer ingreso indica el rol que reciben (o las reglas de grupos que lo dan).");

        if (cambios.SecretoDelCliente is { } secreto)
        {
            var limpio = secreto.Trim();
            if (limpio.Length == 0)
            {
                SecretoProtegido = null;
                UltimosCuatroSecreto = null;
            }
            else
            {
                if (limpio.Length is < 8 or > 2000 || limpio.Any(char.IsWhiteSpace))
                    throw ReglaDeNegocioException.Validacion("secreto-sso-invalido", "El secreto del cliente debe tener entre 8 y 2000 caracteres y no llevar espacios.");
                SecretoProtegido = proteger(limpio);
                UltimosCuatroSecreto = limpio[^4..];
            }
        }

        if (cambios.Activo && !Dominios.Any(d => d.Verificado))
            throw ReglaDeNegocioException.Validacion("sso-sin-dominios", "Verifica al menos un dominio de correo antes de activar el inicio de sesión de la empresa.");

        Activo = cambios.Activo;
        Proveedor = proveedor;
        Nombre = nombre;
        Autoridad = autoridad;
        ClientId = clientId;
        Alcances = alcances;
        ReclamoDeGrupos = reclamo;
        OrganizacionWorkOs = organizacion;
        ConexionWorkOs = conexion;
        AprovisionamientoJit = cambios.AprovisionamientoJit;
        RolesPorOmision = roles;
        Reglas = reglas;
        SincronizarRoles = cambios.SincronizarRoles;
        Modo = cambios.Modo;
        ActualizadoEn = ahora;
    }

    /// <summary>
    /// Registra un dominio por verificar y devuelve su desafío (lo que hay que publicar). Repetirlo con uno ya registrado devuelve el mismo, sin cambiarlo.
    /// 422 <c>dominio-invalido</c>, <c>dominio-excesivo</c>; 409 <c>dominio-publico</c> (un proveedor de correo abierto no es de nadie).
    /// </summary>
    public DominioDeSso AgregarDominio(string? dominio, DateTimeOffset ahora)
    {
        var normalizado = NormalizarDominio(dominio);
        if (Dominios.FirstOrDefault(d => d.Dominio == normalizado) is { } existente)
            return existente;
        if (Dominios.Count >= MaximoDeDominios)
            throw ReglaDeNegocioException.Validacion("dominio-excesivo", $"Se admiten hasta {MaximoDeDominios} dominios por grupo.");

        var nuevo = new DominioDeSso
        {
            Dominio = normalizado,
            Token = $"validasoft-verificacion={Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(20))}",
            CreadoEn = ahora
        };
        Dominios.Add(nuevo);
        ActualizadoEn = ahora;
        return nuevo;
    }

    public DominioDeSso DominioRequerido(string? dominio) =>
        Dominios.FirstOrDefault(d => d.Dominio == NormalizarDominio(dominio))
        ?? throw ReglaDeNegocioException.NoEncontrado("dominio-no-encontrado", "Ese dominio no está registrado en el grupo.");

    public void MarcarVerificado(DominioDeSso dominio, DateTimeOffset ahora)
    {
        dominio.Verificado = true;
        dominio.VerificadoEn = ahora;
        ActualizadoEn = ahora;
    }

    /// <summary>
    /// Quita el dominio. Con el SSO activo no puede quedar sin ningún dominio verificado (409 <c>sso-sin-dominios</c>): desactiva el SSO primero.
    /// </summary>
    public void QuitarDominio(string? dominio, DateTimeOffset ahora)
    {
        var buscado = DominioRequerido(dominio);
        if (Activo && buscado.Verificado && Dominios.Count(d => d.Verificado) == 1)
            throw ReglaDeNegocioException.Conflicto("sso-sin-dominios", "El SSO está activo y este es su único dominio verificado: desactiva el SSO antes de quitarlo.");
        Dominios.Remove(buscado);
        ActualizadoEn = ahora;
    }

    /// <summary>
    /// Los roles que le tocan a quien llegó con estos grupos del proveedor: la unión de los que dan las reglas que coinciden (sin distinguir mayúsculas) o, si
    /// ninguna coincide, los roles por omisión.
    /// </summary>
    public IReadOnlyList<RolAsignado> RolesPara(IEnumerable<string> gruposDelProveedor)
    {
        var grupos = gruposDelProveedor.Select(g => g.Trim()).Where(g => g.Length > 0).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var dados = Reglas.Where(r => grupos.Contains(r.Grupo)).Select(r => new RolAsignado(r.Rol, r.EmpresaId)).Distinct().ToList();
        var roles = dados.Count > 0 ? dados : RolesPorOmision.Distinct().ToList();
        // Un rol de todo el grupo hace redundante el mismo rol por empresa (como al asignarlos a mano).
        var deGrupo = roles.Where(r => r.EmpresaId is null).Select(r => r.Rol).ToHashSet();
        return [.. roles.Where(r => r.EmpresaId is null || !deGrupo.Contains(r.Rol))];
    }

    public static string NormalizarDominio(string? dominio)
    {
        var texto = (dominio ?? string.Empty).Trim().ToLowerInvariant().TrimEnd('.');
        if (texto.StartsWith('@'))
            texto = texto[1..];
        if (texto.Length is < 3 or > 253 || !RegexDominio().IsMatch(texto))
            throw ReglaDeNegocioException.Validacion("dominio-invalido", "Escribe un dominio válido, como empresa.com (sin @ ni rutas).");
        if (DominiosPublicos.Contains(texto))
            throw ReglaDeNegocioException.Conflicto("dominio-publico", $"{texto} es un proveedor de correo abierto: no se puede reclamar para el inicio de sesión de una empresa.");
        return texto;
    }

    private static readonly HashSet<string> DominiosPublicos = new(StringComparer.Ordinal)
    {
        "gmail.com", "googlemail.com", "hotmail.com", "outlook.com", "live.com", "msn.com", "yahoo.com", "yahoo.es", "icloud.com", "me.com", "aol.com",
        "proton.me", "protonmail.com", "gmx.com", "mail.com", "yandex.com", "zoho.com"
    };

    private static ReglaDeGrupoSso Regla(ReglaDeGrupoSso r)
    {
        var grupo = (r.Grupo ?? string.Empty).Trim();
        if (grupo.Length is 0 or > 300 || !Enum.IsDefined(r.Rol))
            throw ReglaDeNegocioException.Validacion("regla-sso-invalida", "Cada regla necesita el grupo del proveedor (hasta 300 caracteres) y un rol de Validasoft.");
        return new ReglaDeGrupoSso { Grupo = grupo, Rol = r.Rol, EmpresaId = r.EmpresaId };
    }

    private static string? Limpio(string? texto, int maximo)
    {
        var limpio = (texto ?? string.Empty).Trim();
        return limpio.Length == 0 ? null : limpio.Length <= maximo ? limpio : throw ReglaDeNegocioException.Validacion("conexion-workos-requerida", $"Hasta {maximo} caracteres.");
    }

    /// <summary>El emisor debe ser https; <c>http</c> solo hacia localhost y solo si <paramref name="permitirHttpLocal"/>. Sin barra final, sin consulta ni fragmento.</summary>
    private static string ValidarAutoridad(string? autoridad, bool permitirHttpLocal)
    {
        var texto = (autoridad ?? string.Empty).Trim().TrimEnd('/');
        if (texto.Length is 0 or > 500 || !Uri.TryCreate(texto, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 || uri.Query.Length > 0 || uri.Fragment.Length > 0)
            throw ReglaDeNegocioException.Validacion("autoridad-invalida", "Escribe el emisor (issuer) del proveedor, como https://login.microsoftonline.com/{inquilino}/v2.0.");

        var local = uri.IsLoopback;
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && local && permitirHttpLocal))
            throw ReglaDeNegocioException.Validacion("autoridad-invalida", "El emisor debe usar https (http solo se admite hacia localhost en desarrollo).");
        return texto;
    }

    [GeneratedRegex("^[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?(\\.[a-z0-9]([a-z0-9-]{0,61}[a-z0-9])?)+$")]
    private static partial Regex RegexDominio();

    [GeneratedRegex("^[A-Za-z0-9_.:\\-/]{1,100}$")]
    private static partial Regex RegexReclamo();
}
