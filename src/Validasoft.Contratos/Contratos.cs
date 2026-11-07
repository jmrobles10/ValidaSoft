using System.Text.Json;

namespace Validasoft.Contratos;

// DTOs públicos de la API v1 (docs/05 §4). Los enums viajan como texto; JSON en camelCase.

public sealed record PaginaDto<T>(IReadOnlyList<T> Datos, string? SiguienteCursor);

// ---------- Catálogos ----------

public sealed record IdentificacionDto(string Pais, string Tipo, string Numero, string? DigitoVerificacion = null);

public sealed record TipoIdentificacionDto(string Pais, string Codigo, string Nombre, string AplicaA, string Patron, bool TieneDv);

public sealed record PaisDto(string Codigo, string Nombre, IReadOnlyList<TipoIdentificacionDto> TiposIdentificacion);

// ---------- Plataforma ----------

public sealed record CrearTenantDto(string Slug, string Nombre, string? Region, string? ZonaHoraria);

public sealed record TenantDto(
    Guid Id, string Slug, string Nombre, string Region, string Plan, string ZonaHoraria, string Idioma, bool Activo, DateTimeOffset CreadoEn);

/// <summary>Respuesta del alta de tenant: incluye la primera API key de administración (su valor se muestra una sola vez).</summary>
public sealed record TenantCreadoDto(TenantDto Tenant, ApiKeyDto ApiKeyAdministrador);

// ---------- Empresas ----------

public sealed record CrearEmpresaDto(IdentificacionDto Identificacion, string Nombre, string PrefijoFolio, string? LogoUrl, string? ReplyTo);

public sealed record EmpresaDto(
    Guid Id, Guid TenantId, IdentificacionDto Identificacion, string Nombre, string? LogoUrl, string? ReplyTo, string PrefijoFolio, bool Activa);

// ---------- API keys ----------

/// <summary><see cref="SistemaId"/>: sistema integrado dueño de la llave (ADR-0011); sin él la llave no puede delegar ni fijar referencias.</summary>
public sealed record CrearApiKeyDto(
    string Nombre, IReadOnlyList<string> Scopes, IReadOnlyList<string>? Empresas, DateTimeOffset? ExpiraEn, bool Sandbox = false,
    Guid? SistemaId = null);

public sealed record ApiKeyDto(
    Guid Id, string Prefijo, string Nombre, IReadOnlyList<string> Scopes, IReadOnlyList<string> Empresas,
    DateTimeOffset? ExpiraEn, DateTimeOffset CreadaEn, bool Sandbox, string? Valor, Guid? SistemaId = null);

// ---------- Sistemas integrados, usuarios y sesiones (ADR-0011) ----------

public sealed record CrearSistemaIntegradoDto(string Codigo, string Nombre, string ClavePublicaPem);

public sealed record RotarClavePublicaDto(string ClavePublicaPem);

public sealed record SistemaIntegradoDto(Guid Id, string Codigo, string Nombre, string ClavePublicaPem, bool Activo, DateTimeOffset CreadoEn);

/// <summary><see cref="EmpresaId"/> nulo = el rol vale en todo el tenant.</summary>
public sealed record RolAsignadoDto(string Rol, Guid? EmpresaId);

public sealed record IdentidadExternaDto(Guid SistemaId, string IdExterno);

/// <summary>Alta anticipada: permite asignar roles antes del primer acceso; la aserción delegada lo enlaza por correo.</summary>
public sealed record CrearUsuarioDto(
    string Nombre, string Correo, IdentificacionDto? Identificacion, IReadOnlyList<RolAsignadoDto>? Roles,
    IReadOnlyList<IdentidadExternaDto>? Identidades);

public sealed record AsignarRolesDto(IReadOnlyList<RolAsignadoDto> Roles);

public sealed record UsuarioDto(
    Guid Id, string Nombre, string Correo, IdentificacionDto? Identificacion, bool Activo,
    IReadOnlyList<IdentidadExternaDto> Identidades, IReadOnlyList<RolAsignadoDto> Roles, DateTimeOffset CreadoEn,
    DateTimeOffset? UltimoAcceso);

/// <summary>Sesión corta de Validasoft para pantallas embebidas: se envía como <c>Authorization: Bearer {token}</c>.</summary>
public sealed record SesionUsuarioDto(string Token, DateTimeOffset Expira);

/// <summary>Nombre visible de un usuario del tenant (sin correo ni roles): para mostrar quién emitió un veredicto.</summary>
public sealed record UsuarioResumenDto(Guid Id, string Nombre);

public sealed record EmpresaResumenDto(Guid Id, string Nombre);

/// <summary>
/// Quién hace la petición (<c>GET /yo</c>): el actor, el usuario si es una persona (identidad delegada o sesión), sus
/// roles en las empresas a su alcance, los permisos efectivos en la empresa activa, el sistema integrado y las empresas
/// que puede usar.
/// </summary>
public sealed record YoDto(
    string ActorTipo, string ActorId, Guid? TenantId, UsuarioResumenDto? Usuario, IReadOnlyList<RolAsignadoDto> Roles,
    IReadOnlyList<string> Permisos, string? Sistema, IReadOnlyList<EmpresaResumenDto> Empresas, Guid? EmpresaActiva);

// ---------- Áreas ----------

public sealed record CrearAreaDto(string Codigo, string Nombre);

public sealed record ActualizarAreaDto(string Nombre, bool? Activa);

public sealed record MiembrosAreaDto(IReadOnlyList<Guid> Usuarios);

public sealed record AreaDto(Guid Id, Guid EmpresaId, string Codigo, string Nombre, IReadOnlyList<Guid> Miembros, bool Activa);

// ---------- Tipos de vínculo ----------

public sealed record AtributoPermitidoDto(string Clave, string Tipo, bool Obligatorio, IReadOnlyList<string>? Opciones);

/// <summary><see cref="ExigeOtpEnEnlace"/>: el enlace de acceso de sus invitaciones pide además un código enviado al correo.</summary>
public sealed record CrearTipoVinculoDto(
    string Codigo, string Nombre, IReadOnlyList<AtributoPermitidoDto>? AtributosPermitidos, bool ExigeCuentaDesignada,
    bool ExigeOtpEnEnlace = false);

public sealed record TipoVinculoDto(
    Guid Id, string Codigo, string Nombre, IReadOnlyList<AtributoPermitidoDto> AtributosPermitidos, bool ExigeCuentaDesignada, bool Activo,
    bool ExigeOtpEnEnlace = false);

public sealed record TipoVinculoResumenDto(Guid Id, string Codigo, string Nombre);

// ---------- Terceros ----------

public sealed record ContactoDto(string Nombre, string Correo, string? Telefono, string Rol);

public sealed record CrearTerceroDto(
    IdentificacionDto Identificacion, string TipoPersona, string Nombre, string? NombreComercial, IReadOnlyList<ContactoDto>? Contactos);

public sealed record TerceroDto(
    Guid Id, IdentificacionDto Identificacion, string TipoPersona, string Nombre, string? NombreComercial,
    IReadOnlyList<ContactoDto> Contactos, bool EsExtranjero, DateTimeOffset CreadoEn);

// ---------- Vínculos ----------

/// <summary>Al crear, <see cref="Sistema"/> puede omitirse: lo fija la API key; si viene y no coincide, 422.</summary>
public sealed record ReferenciaExternaDto(string Sistema, string Id);

public sealed record InvitacionDto(string? CorreoDestino, int VigenciaHoras = 72);

/// <summary>
/// Alta de vínculo. Se identifica al tercero por <see cref="TerceroId"/> o por sus datos; si ya existe en la
/// plataforma se reutiliza sin modificar sus datos maestros (P-1-7). Si el vínculo ya existe y lo pide otro sistema
/// integrado, se le agrega su referencia y responde 200 con el vínculo existente (I-0).
/// </summary>
public sealed record CrearVinculoDto(
    Guid? TerceroId,
    CrearTerceroDto? Tercero,
    string TipoVinculo,
    Dictionary<string, JsonElement>? Atributos,
    string CorreoInvitacion,
    ReferenciaExternaDto? ReferenciaExterna,
    bool EnviarInvitacion = true,
    int VigenciaInvitacionHoras = 72,
    Guid? FlujoId = null);

public sealed record EnlaceInvitacionDto(string CorreoDestino, DateTimeOffset Expira, int Envios);

public sealed record BloqueoDto(string Motivo, DateTimeOffset Desde, DateTimeOffset? Hasta);

public sealed record VinculoDto(
    Guid Id,
    Guid EmpresaId,
    Guid TerceroId,
    TipoVinculoResumenDto TipoVinculo,
    Guid? FlujoId,
    string Estado,
    Dictionary<string, JsonElement> Atributos,
    string CorreoTercero,
    EnlaceInvitacionDto? Invitacion,
    DateTimeOffset? InvitacionAceptadaEn,
    int? RequisitosCubribles,
    BloqueoDto? Bloqueo,
    IReadOnlyList<ReferenciaExternaDto> Referencias,
    string Origen,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn,
    long Version,
    Guid? ExpedienteAbiertoId = null,
    bool AperturaDeExpedientePendiente = false);

public sealed record ActualizarAtributosDto(Dictionary<string, JsonElement> Atributos);

public sealed record BloquearVinculoDto(string Motivo, DateTimeOffset? Hasta);

public sealed record MotivoDto(string Motivo);

public sealed record JustificacionDto(string Justificacion);

/// <summary><see cref="Sistema"/>: sistema integrado desde el que se actuó (ADR-0011), si aplica.</summary>
public sealed record EventoHistorialDto(
    long Version, string Tipo, DateTimeOffset Fecha, string? ActorTipo, string? ActorId, string? Sistema, JsonElement Datos);

// ---------- Textos legales (docs/07 §2.2 y §8) ----------

/// <summary>Publica una versión nueva (inmutable) del texto; la anterior deja de ser la vigente.</summary>
public sealed record PublicarTextoLegalDto(string Contenido);

/// <summary><see cref="Tipo"/>: <c>AvisoPlataforma</c>, <c>AutorizacionEmpresa</c> o <c>TerminosEmpresa</c>; <see cref="Hash"/> = SHA-256 del contenido.</summary>
public sealed record TextoLegalDto(
    Guid Id, string Ambito, Guid? EmpresaId, string Tipo, int Version, string Hash, string Contenido, DateTimeOffset PublicadoEn, string PublicadoPor);

/// <summary>Textos vigentes de la empresa y los que le faltan publicar para que sus invitaciones se puedan aceptar.</summary>
public sealed record TextosLegalesEmpresaDto(Guid EmpresaId, IReadOnlyList<TextoLegalDto> Vigentes, IReadOnlyList<string> Faltantes);

// ---------- Público: el tercero (docs/05 §2.9) ----------

public sealed record EmpresaPublicaDto(Guid Id, string Nombre, string? LogoUrl);

public sealed record VinculoDeSesionDto(Guid Id, EmpresaPublicaDto Empresa, string Estado);

/// <summary>
/// Sesión del tercero tras canjear el enlace (y el OTP si aplica) o renovarla: JWT de 30 minutos que viaja en el cuerpo y
/// se envía como <c>Authorization: Bearer</c> en <c>/public/v1</c>. El token de refresco va aparte, en la cookie
/// <c>HttpOnly</c> <c>vs_tercero</c> (<c>POST /public/v1/sesion/renovar</c>).
/// </summary>
public sealed record SesionTerceroDto(
    string AccessToken, DateTimeOffset Expira, Guid TerceroId, bool OtpVerificado, IReadOnlyList<VinculoDeSesionDto> Vinculos,
    Guid? ExpedienteOrigen = null);

/// <summary>Respuesta 202 del canje cuando el enlace exige OTP: el código llegó al correo y vence en <see cref="Expira"/>.</summary>
public sealed record OtpRequeridoDto(bool RequiereOtp, DateTimeOffset Expira);

public sealed record VerificarOtpDto(string Codigo);

/// <summary>Vínculo visto por el tercero (sin decisiones de la empresa). Certificado, pendientes y cuenta designada llegan con H4/H6.</summary>
public sealed record VinculoPublicoDto(
    Guid Id, EmpresaPublicaDto Empresa, TipoVinculoResumenDto TipoVinculo, string Estado, bool InvitacionAceptada,
    DateTimeOffset? InvitacionAceptadaEn, Guid? ExpedienteId = null);

public sealed record TextoLegalPublicoDto(string Tipo, int TextoVersion, string HashTexto, string Contenido, DateTimeOffset PublicadoEn);

/// <summary>
/// Lo que el tercero debe aceptar en una invitación: los textos vigentes (el aviso de la plataforma solo si no ha
/// aceptado su versión vigente). <see cref="TextosFaltantes"/> no vacío ⇒ la empresa aún no publica sus textos y
/// <see cref="PuedeAceptar"/> es falso.
/// </summary>
public sealed record InvitacionPublicaDto(
    VinculoPublicoDto Vinculo, IReadOnlyList<TextoLegalPublicoDto> TextosPorAceptar, IReadOnlyList<string> TextosFaltantes, bool PuedeAceptar);

public sealed record ConsentimientoDto(string Tipo, int TextoVersion, bool Aceptado);

/// <summary><see cref="OtpCodigo"/>: reservado; hoy el OTP se verifica al canjear el enlace (IN-6).</summary>
public sealed record AceptarInvitacionDto(IReadOnlyList<ConsentimientoDto> Consentimientos, string? OtpCodigo = null);

public sealed record AceptadoPorDto(Guid TerceroId, string Correo);

/// <summary>
/// Evidencia de una aceptación (P-3-5, P-16-4). <see cref="DocumentoVersionId"/> (versión <c>PorVinculo</c> que la
/// registra) llega con el módulo de documentos; mientras tanto la evidencia vive en el evento <c>ConsentimientosRegistrados</c>.
/// </summary>
public sealed record AceptacionDto(
    string Tipo, Guid TextoLegalId, int TextoVersion, string HashTexto, DateTimeOffset Fecha, string Ip, string UserAgent,
    bool OtpVerificado, string Canal, AceptadoPorDto AceptadoPor, Guid? DocumentoVersionId = null);

/// <summary>
/// Respuesta de aceptar: la evidencia, el vínculo y los documentos que se compartieron con la empresa en ese momento (la
/// versión actual de cada tipo global que exige y los <c>PorVinculo</c> ya cargados para el vínculo). Sin
/// <c>requisitosCubiertos</c> hasta que el expediente tenga requisitos.
/// </summary>
public sealed record InvitacionAceptadaDto(IReadOnlyList<AceptacionDto> Aceptacion, VinculoPublicoDto Vinculo, IReadOnlyList<CompartidoDto> Compartidos);

/// <summary>Autorregistro invisible (sin cargas de documentos todavía).</summary>
public sealed record AutorregistroDto(
    IdentificacionDto Identificacion, string TipoPersona, string Nombre, string Correo, bool ConsentimientoPlataforma);

/// <summary>Respuesta idéntica exista o no el tercero (no revela nada).</summary>
public sealed record SolicitudRecibidaDto(string Mensaje);

/// <summary>Pedir un enlace para volver a entrar (<c>POST /public/v1/acceso</c>); la respuesta es la misma exista o no el correo.</summary>
public sealed record SolicitarAccesoDto(string? Correo);
