namespace Validasoft.Contratos;

// Inicio de sesión con el proveedor de identidad del cliente (SSO, PI-1…PI-6). El secreto del cliente es de solo escritura: nunca vuelve.

/// <summary>Un grupo del proveedor de identidad y el rol que da en Validasoft; sin <see cref="EmpresaId"/> el rol vale en todo el grupo.</summary>
public sealed record ReglaDeGrupoSsoDto(string Grupo, string Rol, Guid? EmpresaId = null);

/// <summary>
/// Un dominio de correo del grupo. Hasta que se verifica, <see cref="Token"/> es lo que hay que publicar en
/// <c>https://{dominio}/.well-known/validasoft-verificacion.txt</c> (<see cref="Instruccion"/> lo explica).
/// </summary>
public sealed record DominioDeSsoDto(
    string Dominio, bool Verificado, string? Token, string? Instruccion, DateTimeOffset CreadoEn, DateTimeOffset? VerificadoEn);

/// <summary>
/// El SSO del grupo (<c>GET/PUT /configuracion/sso</c>). <see cref="SecretoConfigurado"/> y <see cref="UltimosCuatroSecreto"/> dicen si hay secreto del cliente y
/// cómo termina; <see cref="UrlDeRetorno"/> es la dirección que se registra en el proveedor como URI de redirección; <see cref="WorkOsDisponible"/> dice si la
/// instalación tiene el adaptador de WorkOS configurado.
/// </summary>
public sealed record ConfiguracionDeSsoDto(
    bool Configurado, bool Activo, string Proveedor, string Nombre, string? Autoridad, string? ClientId, bool SecretoConfigurado, string? UltimosCuatroSecreto,
    IReadOnlyList<string> Alcances, string ReclamoDeGrupos, string? OrganizacionWorkOs, string? ConexionWorkOs, IReadOnlyList<DominioDeSsoDto> Dominios,
    bool AprovisionamientoJit, IReadOnlyList<RolAsignadoDto> RolesPorOmision, IReadOnlyList<ReglaDeGrupoSsoDto> Reglas, bool SincronizarRoles, string Modo,
    string UrlDeRetorno, bool WorkOsDisponible, DateTimeOffset? ActualizadoEn);

/// <summary>
/// Crea o reemplaza el SSO del grupo. <see cref="SecretoDelCliente"/> nulo conserva el actual y vacío lo quita. <see cref="Modo"/>: <c>Complementario</c> (el SSO y el
/// código por correo conviven) o <c>Exclusivo</c> (las personas de los dominios verificados solo entran con el SSO).
/// </summary>
public sealed record FijarSsoDto(
    bool Activo, string Proveedor, string Nombre, string? Autoridad = null, string? ClientId = null, string? SecretoDelCliente = null,
    IReadOnlyList<string>? Alcances = null, string? ReclamoDeGrupos = null, string? OrganizacionWorkOs = null, string? ConexionWorkOs = null,
    bool AprovisionamientoJit = false, IReadOnlyList<RolAsignadoDto>? RolesPorOmision = null, IReadOnlyList<ReglaDeGrupoSsoDto>? Reglas = null,
    bool SincronizarRoles = false, string Modo = "Complementario");

public sealed record AgregarDominioSsoDto(string Dominio);

/// <summary>
/// Resultado de «Probar conexión»: si el proveedor responde y publica el descubrimiento OIDC con el emisor esperado. No inicia sesión ni cambia nada.
/// </summary>
public sealed record PruebaDeSsoDto(bool Correcto, string Mensaje, string? Emisor, string? PuntoDeAutorizacion);

// ---------- Pantalla de ingreso ----------

public sealed record DescubrirSsoDto(string Correo);

/// <summary>
/// Si el correo es de un dominio con SSO activo: <see cref="Disponible"/>, cómo se llama y si reemplaza al código por correo. Es lo único que se sabe del
/// correo; no revela si tiene cuenta.
/// </summary>
public sealed record SsoDescubiertoDto(bool Disponible, string? Nombre, bool ReemplazaElCodigo);

public sealed record IniciarSsoDto(string Correo);

/// <summary>El navegador debe ir a <see cref="UrlDeAutorizacion"/>; al volver, la app completa el ingreso con <see cref="Desafio"/>.</summary>
public sealed record SsoIniciadoDto(Guid Desafio, string UrlDeAutorizacion);

public sealed record CompletarSsoDto(Guid Desafio);
