using System.Text.Json;

namespace Validasoft.Contratos;

// DTOs públicos de la API v1 (docs/05 §4). Los enums viajan como texto; JSON en camelCase.

public sealed record PaginaDto<T>(IReadOnlyList<T> Datos, string? SiguienteCursor);

// ---------- Catálogos ----------

public sealed record IdentificacionDto(string Pais, string Tipo, string Numero, string? DigitoVerificacion = null);

public sealed record TipoIdentificacionDto(string Pais, string Codigo, string Nombre, string AplicaA, string Patron, bool TieneDv);

public sealed record PaisDto(string Codigo, string Nombre, IReadOnlyList<TipoIdentificacionDto> TiposIdentificacion);

// ---------- Plataforma ----------

/// <summary>
/// Alta de un grupo (tenant). Con <see cref="Empresa"/>, <see cref="Administrador"/> y <see cref="Sistema"/> queda operable de
/// punta a punta en una sola transacción (PT-5): su primera empresa, su administrador de todo el grupo y el sistema integrado
/// con una API key que puede actuar en nombre del administrador.
/// </summary>
public sealed record CrearTenantDto(
    string Slug, string Nombre, string? Region, string? ZonaHoraria, CrearEmpresaDto? Empresa = null, AdministradorInicialDto? Administrador = null,
    CrearSistemaIntegradoDto? Sistema = null, ConfiguracionDeIngresoDto? Ingreso = null);

/// <summary>
/// Administrador inicial del grupo (rol <c>Administrador</c> de todo el tenant). <see cref="IdExterno"/> es su <c>sub</c> en el
/// sistema integrado inicial (exige <c>sistema</c>); sin él, su primer acceso delegado lo enlaza por correo.
/// </summary>
public sealed record AdministradorInicialDto(string Nombre, string Correo, string? IdExterno = null);

public sealed record TenantDto(
    Guid Id, string Slug, string Nombre, string Region, string Plan, string ZonaHoraria, string Idioma, bool Activo, DateTimeOffset CreadoEn,
    ConfiguracionDeIngresoDto? Ingreso = null);

/// <summary>
/// Respuesta del alta de tenant: incluye la primera API key de administración y, si se pidió el sistema integrado, su API
/// key (los valores se muestran una sola vez), además de la empresa y el administrador creados.
/// </summary>
public sealed record TenantCreadoDto(
    TenantDto Tenant, ApiKeyDto ApiKeyAdministrador, EmpresaDto? Empresa = null, UsuarioDto? Administrador = null, SistemaIntegradoDto? Sistema = null,
    ApiKeyDto? ApiKeySistema = null);

// ---------- Empresas ----------

public sealed record CrearEmpresaDto(IdentificacionDto Identificacion, string Nombre, string PrefijoFolio, string? LogoUrl, string? ReplyTo);

public sealed record EmpresaDto(
    Guid Id, Guid TenantId, IdentificacionDto Identificacion, string Nombre, string? LogoUrl, string? ReplyTo, string PrefijoFolio, bool Activa,
    string? ColorMarca = null);

/// <summary>
/// Datos y marca de la empresa: <see cref="ContactoSoporte"/> (correo; queda como <c>replyTo</c>), <see cref="ColorMarca"/>
/// (#RRGGBB) y <see cref="LogoUrl"/> (https o data URI SVG/PNG ≤ 200 KB). Nulo conserva; cadena vacía quita.
/// </summary>
public sealed record ActualizarEmpresaDto(string? ContactoSoporte = null, string? ColorMarca = null, string? LogoUrl = null);

// ---------- Accesos de soporte (B-6) ----------

/// <summary>Concede a la Plataforma acceso de lectura a una empresa por <see cref="Horas"/> (1–72), con justificación.</summary>
public sealed record ConcederAccesoSoporteDto(Guid EmpresaId, int Horas, string Justificacion);

/// <summary><see cref="ConcedidoPor"/>/<see cref="RevocadoPor"/>: id del actor (usuario o API key). <see cref="Vigente"/> a la hora de la consulta.</summary>
public sealed record AccesoSoporteDto(
    Guid Id, Guid EmpresaId, string Empresa, string Justificacion, string ConcedidoPor, DateTimeOffset ConcedidoEn, DateTimeOffset ExpiraEn,
    DateTimeOffset? RevocadoEn, string? RevocadoPor, bool Vigente);

/// <summary>Lo que ve la Plataforma: los accesos vigentes que le concedieron, con el grupo (tenant) y la empresa.</summary>
public sealed record AccesoSoporteConcedidoDto(Guid Id, Guid TenantId, string Grupo, Guid EmpresaId, string Empresa, string Justificacion, DateTimeOffset ExpiraEn);

// ---------- API keys ----------

/// <summary><see cref="SistemaId"/>: sistema integrado dueño de la llave (ADR-0011); sin él la llave no puede delegar ni fijar referencias.</summary>
public sealed record CrearApiKeyDto(
    string Nombre, IReadOnlyList<string> Scopes, IReadOnlyList<string>? Empresas, DateTimeOffset? ExpiraEn, bool Sandbox = false,
    Guid? SistemaId = null);

/// <summary>
/// <see cref="Valor"/>: la clave completa, solo en la respuesta de crear o rotar. <see cref="RotadaDe"/>: la llave que esta
/// reemplazó; <see cref="ReemplazadaPor"/>: la que la reemplaza (la rotada sigue valiendo hasta <see cref="ExpiraEn"/>, CN-3).
/// </summary>
public sealed record ApiKeyDto(
    Guid Id, string Prefijo, string Nombre, IReadOnlyList<string> Scopes, IReadOnlyList<string> Empresas,
    DateTimeOffset? ExpiraEn, DateTimeOffset CreadaEn, bool Sandbox, string? Valor, Guid? SistemaId = null,
    Guid? RotadaDe = null, Guid? ReemplazadaPor = null);

/// <summary>
/// Rotación con solapamiento (<c>POST /api-keys/{id}/rotar</c>, CN-3): la llave nueva copia nombre, permisos, alcance,
/// sistema, sandbox y expiración; la anterior sigue valiendo hasta <see cref="AnteriorValidaHasta"/> (nulo = 24 horas;
/// ahora = corte inmediato; como mucho 30 días y nunca más allá de su propia expiración).
/// </summary>
public sealed record RotarApiKeyDto(DateTimeOffset? AnteriorValidaHasta = null);

// ---------- Sistemas integrados, usuarios y sesiones (ADR-0011) ----------

/// <summary><see cref="OrigenesPermitidos"/>: opcional; orígenes <c>https://host[:puerto]</c> que pueden alojar las pantallas embebidas (SE-1).</summary>
public sealed record CrearSistemaIntegradoDto(string Codigo, string Nombre, string ClavePublicaPem, IReadOnlyList<string>? OrigenesPermitidos = null);

public sealed record RotarClavePublicaDto(string ClavePublicaPem);

/// <summary>Reemplaza la lista completa de orígenes del sistema (<c>PUT /sistemas/{id}/origenes</c>, SE-1).</summary>
public sealed record FijarOrigenesDto(IReadOnlyList<string> Origenes);

public sealed record SistemaIntegradoDto(
    Guid Id, string Codigo, string Nombre, string ClavePublicaPem, bool Activo, DateTimeOffset CreadoEn, IReadOnlyList<string> OrigenesPermitidos);

/// <summary>
/// <c>GET /public/v1/embed/origenes-permitidos</c> (SE-2): la unión de los orígenes de los sistemas activos, la misma
/// que va en <c>frame-ancestors</c>; sin tenant, sistema ni nombres.
/// </summary>
public sealed record OrigenesEmbebidoDto(IReadOnlyList<string> Origenes, int VigenciaSegundos);

/// <summary><see cref="EmpresaId"/> nulo = el rol vale en todo el tenant.</summary>
public sealed record RolAsignadoDto(string Rol, Guid? EmpresaId);

public sealed record IdentidadExternaDto(Guid SistemaId, string IdExterno);

/// <summary>
/// Alta anticipada: permite asignar roles antes del primer acceso; la aserción delegada lo enlaza por correo. Con
/// <see cref="Invitar"/> (y un grupo que admita el ingreso directo) se le envía un correo para que entre con el suyo (ING-5).
/// </summary>
public sealed record CrearUsuarioDto(
    string Nombre, string Correo, IdentificacionDto? Identificacion, IReadOnlyList<RolAsignadoDto>? Roles,
    IReadOnlyList<IdentidadExternaDto>? Identidades, bool? Invitar = null);

public sealed record AsignarRolesDto(IReadOnlyList<RolAsignadoDto> Roles);

/// <param name="ExigePasskey">Un rol suyo (o el grupo) le exige passkey para entrar directo.</param>
/// <param name="Passkeys">Passkeys activas que tiene registradas.</param>
public sealed record UsuarioDto(
    Guid Id, string Nombre, string Correo, IdentificacionDto? Identificacion, bool Activo,
    IReadOnlyList<IdentidadExternaDto> Identidades, IReadOnlyList<RolAsignadoDto> Roles, DateTimeOffset CreadoEn,
    DateTimeOffset? UltimoAcceso, bool ExigePasskey = false, int Passkeys = 0, DateTimeOffset? InvitadoEn = null);

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

/// <summary>
/// <see cref="ReferenciaExterna"/> (opcional): la clave del tercero en el sistema de la API key; con ella la creación es idempotente.
/// </summary>
public sealed record CrearTerceroDto(
    IdentificacionDto Identificacion, string TipoPersona, string Nombre, string? NombreComercial, IReadOnlyList<ContactoDto>? Contactos,
    ReferenciaExternaDto? ReferenciaExterna = null);

public sealed record TerceroDto(
    Guid Id, IdentificacionDto Identificacion, string TipoPersona, string Nombre, string? NombreComercial,
    IReadOnlyList<ContactoDto> Contactos, bool EsExtranjero, DateTimeOffset CreadoEn);

/// <summary>
/// Ficha del tercero para la empresa activa (<c>Tercero360</c>): sus vínculos con esta empresa y, de las demás empresas
/// del grupo, solo que existe el vínculo y su estado (sin decisiones ni documentos). <see cref="Secciones"/>: las versiones
/// vigentes de la ficha maestra (datos, completitud y soportes; FM-20); la aceptación por empresa llega con F2.
/// </summary>
public sealed record FichaTerceroDto(TerceroDto Tercero, IReadOnlyList<VinculoEnFichaDto> Vinculos,
    IReadOnlyList<VinculoEnOtraEmpresaDto> OtrasEmpresasDelGrupo, IReadOnlyList<SeccionFichaDto>? Secciones = null);

/// <summary>
/// Un vínculo del tercero con la empresa activa. <see cref="Certificado"/> y <see cref="Documentos"/> son nulos cuando
/// quien consulta no tiene <c>certificados:leer</c> o <c>documentos:leer</c>; el certificado sin emitir viene con
/// <c>Actual</c> nulo.
/// </summary>
public sealed record VinculoEnFichaDto(VinculoDto Vinculo, CertificadoDelVinculoDto? Certificado, CuentaDesignadaDelVinculoDto CuentaDesignada,
    IReadOnlyList<DocumentoVistaEmpresaDto>? Documentos);

public sealed record VinculoEnOtraEmpresaDto(Guid EmpresaId, string Estado);

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

/// <summary>
/// Contacto del tercero con una empresa (IO-3), distinto del de las demás empresas. <see cref="Rol"/>: Comercial, Pagos,
/// Cumplimiento o Notificaciones; <see cref="Cargo"/> y <see cref="Telefono"/> son opcionales.
/// </summary>
public sealed record ContactoDeEmpresaDto(string Nombre, string? Cargo, string Correo, string? Telefono, string Rol);

/// <summary>Reemplaza la lista de contactos del tercero con la empresa (hasta 20; lista vacía = quitarlos todos).</summary>
public sealed record ActualizarContactosDto(IReadOnlyList<ContactoDeEmpresaDto> Contactos);

/// <summary>
/// <see cref="EnRevision"/> (VR-1): el tercero actualizó información que la empresa ya había aprobado y el expediente de
/// <c>Actualizacion</c> sigue abierto (abierto, en revisión u observado). <b>No cambia <see cref="Estado"/></b> (sigue <c>Activo</c>,
/// <c>PorVencer</c> o <c>Vencido</c>, igual que el certificado y la vigencia): es lo que la pantalla muestra como «En revisión» y
/// lo que filtra <c>GET /vinculos?enRevision=</c>. <see cref="ExpedienteAbiertoId"/> es ese expediente.
/// </summary>
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
    bool AperturaDeExpedientePendiente = false,
    string? UltimoResultado = null,
    IReadOnlyList<ContactoDeEmpresaDto>? Contactos = null,
    DateOnly? VigenteHasta = null,
    bool EnRevision = false,
    ActualizacionNoAprobadaDto? ActualizacionNoAprobada = null);

/// <summary>
/// La última actualización del vínculo no se aprobó (VR-13): <see cref="Resultado"/> es <c>Rechazado</c> o <c>Anulado</c>, <see cref="Fecha"/> cuándo se
/// cerró y <see cref="ExpedienteId"/> el expediente (con sus motivos y su historial). El vínculo conserva su estado; el aviso dura hasta que se
/// abre otro expediente.
/// </summary>
public sealed record ActualizacionNoAprobadaDto(Guid? ExpedienteId, string Resultado, DateTimeOffset? Fecha);

public sealed record ActualizarAtributosDto(Dictionary<string, JsonElement> Atributos);

public sealed record BloquearVinculoDto(string Motivo, DateTimeOffset? Hasta);

public sealed record MotivoDto(string Motivo);

public sealed record JustificacionDto(string Justificacion);

/// <summary>
/// Reintegra un vínculo desvinculado (DS-3). Con <see cref="Revalidar"/> (por omisión) vuelve a validación y se abre una
/// renovación con todos los documentos del flujo; sin revalidar restituye el estado que tenía al desvincularse y emite el
/// certificado de nuevo con lo ya aprobado, solo si esas versiones siguen vigentes (<c>GET /vinculos/{id}/reintegro</c> dice si
/// se puede y, si no, por qué).
/// </summary>
public sealed record ReintegrarVinculoDto(string Justificacion, bool Revalidar = true);

/// <summary>
/// Si el vínculo desvinculado se puede reintegrar sin revalidar (DS-3). <see cref="Motivos"/>: por qué no (un documento
/// cambió o venció, el vínculo no estaba aprobado…), en texto para la persona; vacío cuando sí se puede.
/// </summary>
public sealed record ViabilidadDeReintegroDto(bool PuedeSinRevalidar, IReadOnlyList<string> Motivos);

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

/// <summary>Lo que el tercero ve de la empresa: nombre, logo, color de marca y correo de soporte.</summary>
public sealed record EmpresaPublicaDto(Guid Id, string Nombre, string? LogoUrl, string? ColorMarca = null, string? ContactoSoporte = null);

public sealed record VinculoDeSesionDto(Guid Id, EmpresaPublicaDto Empresa, string Estado);

/// <summary>
/// Sesión del tercero tras canjear el enlace (y el OTP si aplica) o renovarla: JWT de 30 minutos que viaja en el cuerpo y
/// se envía como <c>Authorization: Bearer</c> en <c>/public/v1</c>. El token de refresco va aparte, en la cookie
/// <c>HttpOnly</c> <c>vs_tercero</c> (<c>POST /public/v1/sesion/renovar</c>).
/// <see cref="VinculoOrigen"/>: el vínculo del enlace canjeado (la invitación de esa empresa) y
/// <see cref="ExpedienteOrigen"/>: su expediente abierto si el enlace vino en un correo de observación. La app del tercero
/// entra directo ahí aunque tenga otras invitaciones.
/// </summary>
public sealed record SesionTerceroDto(
    string AccessToken, DateTimeOffset Expira, Guid TerceroId, bool OtpVerificado, IReadOnlyList<VinculoDeSesionDto> Vinculos,
    Guid? ExpedienteOrigen = null, Guid? VinculoOrigen = null);

/// <summary>Respuesta 202 del canje cuando el enlace exige OTP: el código llegó al correo y vence en <see cref="Expira"/>.</summary>
public sealed record OtpRequeridoDto(bool RequiereOtp, DateTimeOffset Expira);

public sealed record VerificarOtpDto(string Codigo);

/// <summary>Componentes que un sistema integrador puede embeber del tercero (IO-5).</summary>
public static class ComponentesEmbebibles
{
    public const string Boveda = "boveda";

    public const string Expediente = "expediente";

    public const string Ficha = "ficha";

    public static readonly IReadOnlyList<string> Todos = [Boveda, Expediente, Ficha];
}

/// <summary>
/// Pide una sesión del tercero para un iframe (<c>POST /api/v1/sesiones-tercero</c>, scope <c>sesiones-tercero:crear</c>). El vínculo
/// debe ser de la empresa (<c>X-Empresa</c>), llevar la referencia del sistema de la API key y tener la invitación aceptada.
/// <see cref="Origen"/>: el origen exacto de la página que aloja el iframe; debe estar entre los orígenes permitidos del sistema (403
/// <c>origen-no-permitido</c>). <see cref="Componente"/>: <c>boveda</c>, <c>expediente</c> o <c>ficha</c>. <see cref="Minutos"/>: vigencia,
/// de 1 a 15 (10 por omisión).
/// </summary>
public sealed record CrearSesionEmbebidaDto(Guid VinculoId, string Componente, string Origen, int? Minutos = null);

/// <summary>
/// La sesión embebida: <see cref="Token"/> (JWT firmado, ligado al tercero, al vínculo, al origen y al componente; se pasa al iframe
/// por <c>postMessage</c>, nunca en la URL), <see cref="Url"/> (la del iframe, sin el token) y <see cref="Expira"/>.
/// </summary>
public sealed record SesionEmbebidaDto(string Token, DateTimeOffset Expira, string Url, string Origen, string Componente, Guid VinculoId, Guid TerceroId);

/// <summary>
/// Vínculo visto por el tercero (sin decisiones de la empresa). Certificado, pendientes y cuenta designada llegan con H4/H6.
/// <see cref="EnRevision"/> (VR-1): hay una actualización suya abierta con esa empresa (<see cref="ExpedienteId"/> es ese expediente);
/// <see cref="Estado"/> no cambia, pero la pantalla del tercero cuenta lo que le toca con ella, no «Aprobado».
/// </summary>
public sealed record VinculoPublicoDto(
    Guid Id, EmpresaPublicaDto Empresa, TipoVinculoResumenDto TipoVinculo, string Estado, bool InvitacionAceptada,
    DateTimeOffset? InvitacionAceptadaEn, Guid? ExpedienteId = null, ReintentoPublicoDto? Reintento = null,
    IReadOnlyList<ContactoDeEmpresaDto>? Contactos = null, DateOnly? VigenteHasta = null, bool EnRevision = false,
    ActualizacionNoAprobadaPublicaDto? ActualizacionNoAprobada = null);

/// <summary>
/// Lo que el tercero ve de una actualización suya que la empresa no aprobó (VR-13): <see cref="Resultado"/> <c>Rechazado</c> (con los
/// <see cref="Motivos"/> tal como se le comunicaron, sin áreas ni revisores) o <c>Anulado</c> (sin motivos: son internos). Lo que ya tenía aprobado
/// sigue vigente. Dura hasta que se abre otro expediente.
/// </summary>
public sealed record ActualizacionNoAprobadaPublicaDto(string Resultado, DateTimeOffset? Fecha, IReadOnlyList<MotivoPublicoDto> Motivos);

/// <summary>
/// Lo que el tercero ve de un rechazo (DS-4): cuándo y con qué motivos (los textos al tercero, sin áreas ni revisores). Con
/// <see cref="Permitido"/> la empresa deja que el tercero vuelva a postularse; <see cref="Disponible"/> es que ya puede
/// (<see cref="Desde"/> es la fecha en que cumple la espera del flujo).
/// </summary>
public sealed record ReintentoPublicoDto(DateTimeOffset RechazadoEn, IReadOnlyList<MotivoPublicoDto> Motivos, bool Permitido, bool Disponible,
    DateTimeOffset? Desde);

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

// ---------- Cuentas bancarias y cuenta designada (CB-1, CB-2) ----------

/// <summary>A quién escribir por los pagos de una cuenta (IO-4): nombre y correo obligatorios, teléfono opcional.</summary>
public sealed record ContactoDePagosDto(string Nombre, string Correo, string? Telefono);

/// <summary>
/// Lo que declara el tercero de una cuenta. <see cref="TipoCuenta"/>: Ahorros o Corriente; moneda ISO (COP por defecto). El banco se
/// toma del catálogo <c>bancos</c>: con <see cref="BancoCodigo"/> (código de compensación local, CENIT en Colombia) el nombre sale del
/// catálogo (422 <c>banco-no-encontrado</c> si no existe) y <see cref="Banco"/> se ignora; sin él, <see cref="Banco"/> es el nombre libre
/// de un banco fuera del catálogo. <see cref="BancoSwift"/>: SWIFT/BIC (8 u 11 caracteres); si se omite se usa el del catálogo.
/// <see cref="ContactoPagos"/>: a quién escribir por los pagos de esta cuenta.
/// </summary>
public sealed record RegistrarCuentaBancariaDto(string? Banco, string? TipoCuenta, string? Numero, string? Titular, string? IdentificacionTitular,
    string? Moneda = null, string? Pais = null, string? BancoCodigo = null, string? BancoSwift = null, ContactoDePagosDto? ContactoPagos = null);

/// <summary>Con qué empresa está designada (o propuesta, o rechazada) una cuenta del tercero.</summary>
public sealed record DesignacionDeCuentaDto(Guid VinculoId, Guid EmpresaId, string Empresa, string Estado, string? MotivoRechazo);

/// <summary>Cuenta del tercero, siempre enmascarada, con las empresas a las que la designó.</summary>
public sealed record CuentaBancariaPublicaDto(Guid Id, string Banco, string TipoCuenta, string NumeroEnmascarado, string Titular,
    string IdentificacionTitular, string Moneda, string Estado, DateTimeOffset RegistradaEn, IReadOnlyList<DesignacionDeCuentaDto> DesignadaEn,
    string? BancoCodigo = null, string? BancoSwift = null, ContactoDePagosDto? ContactoPagos = null);

public sealed record DesignarCuentaDto(Guid CuentaId);

public sealed record AprobacionDeCuentaDto(string UsuarioId, string? Nombre, DateTimeOffset Fecha);

/// <summary>
/// Una cuenta designada (o propuesta) vista por la empresa. <see cref="Numero"/> completo solo para quien puede aprobar
/// cuentas (<c>cuentas:aprobar</c>); para el resto, nulo.
/// </summary>
public sealed record CuentaDesignadaDto(Guid CuentaId, string Banco, string TipoCuenta, string NumeroEnmascarado, string? Numero, string Titular,
    string IdentificacionTitular, string Moneda, string Estado, DateTimeOffset PropuestaEn, IReadOnlyList<AprobacionDeCuentaDto> Aprobaciones,
    DateTimeOffset? AprobadaEn, string? MotivoRechazo, string? BancoCodigo = null, string? BancoSwift = null, ContactoDePagosDto? ContactoPagos = null);

/// <summary>
/// La cuenta designada del vínculo: la aprobada vigente, la propuesta pendiente y la última rechazada.
/// <see cref="PuedesAprobar"/>: quien consulta tiene <c>cuentas:aprobar</c>, es una persona y aún no aprobó la propuesta.
/// </summary>
public sealed record CuentaDesignadaDelVinculoDto(CuentaDesignadaDto? Actual, CuentaDesignadaDto? Propuesta, CuentaDesignadaDto? Rechazada,
    int AprobadoresRequeridos, bool PuedesAprobar);

// ---------- Vencimientos de la empresa (VE-1..VE-3) ----------

/// <summary>
/// Un documento compartido con un vínculo de la empresa y su vigencia para ella. <see cref="Estado"/>: Vigente,
/// PorVencer, EnGracia o Vencida; <see cref="Base"/>: de dónde sale la fecha (expedición, carga, vencimiento propio,
/// ratificación). <see cref="UltimoRecordatorio"/>: la última vez que la empresa se lo recordó al tercero.
/// </summary>
public sealed record VencimientoDto(Guid VinculoId, Guid DocumentoId, TerceroResumenDto Tercero, TipoVinculoResumenDto TipoVinculo, string Documento,
    int Version, DateOnly VigenteHasta, int DiasRestantes, string Estado, string Base, DateOnly? FinDeGracia, bool PermiteRatificar,
    DateTimeOffset? UltimoRecordatorio);

public sealed record VencimientoARecordarDto(Guid VinculoId, Guid DocumentoId);

/// <summary>Recordar al tercero los documentos elegidos (hasta 200); un correo por destinatario con todos los suyos.</summary>
public sealed record RecordarVencimientosDto(IReadOnlyList<VencimientoARecordarDto> Vencimientos);

/// <summary><see cref="Omitidos"/>: ya se recordaron hoy o el vínculo no tiene correo.</summary>
public sealed record RecordatorioEnviadoDto(int Correos, int Documentos, int Omitidos);

// ---------- Inicio de la empresa (INI-1, INI-2) ----------

/// <summary>Lo que espera a un área: expedientes en revisión con su decisión pendiente y cuántos días lleva el más antiguo.</summary>
public sealed record PendientesDeAreaDto(Guid AreaId, string Codigo, string Nombre, int Cantidad, int MasAntiguoDias);

/// <summary>
/// Lo que le toca a quien consulta en la empresa activa (INI-1, docs/03 §14 <c>MisPendientes</c>): por cada área suya (todas
/// para una integración), las revalidaciones de sus áreas, las cuentas por aprobar si puede aprobarlas, las aperturas
/// pendientes si administra expedientes y los documentos por vencer o vencidos en 30 días.
/// </summary>
public sealed record MisPendientesDto(IReadOnlyList<PendientesDeAreaDto> PorArea, int Revalidaciones, int RevalidacionesBloqueantes,
    int CuentasPorAprobar, int AperturasPendientes, int DocumentosPorVencer, int DocumentosVencidos);

/// <summary>
/// Indicadores de la empresa activa (INI-2). Los de flujo cuentan expedientes cerrados en los últimos <see cref="Dias"/>
/// días; los de estado son la foto de hoy. Tiempos en días corridos, de la apertura al cierre aprobado.
/// <see cref="VinculosEnRevision"/> (VR-1): vínculos con una actualización abierta; no cuentan en <see cref="VinculosActivos"/>,
/// <see cref="VinculosPorVencer"/> ni <see cref="VinculosVencidos"/> (cada vínculo está en una sola cifra, la misma que ve en su lista).
/// <see cref="CertificadosEnVerificacion"/> (VR-11): certificados vigentes que la empresa revisa de nuevo por una actualización; ya no cuentan en
/// <see cref="CertificadosConPendientes"/> (queda con los pendientes que ya bloquean, que siguen contando también en <see cref="CertificadosNoValidos"/>).
/// </summary>
public sealed record IndicadoresDto(int Dias, int VinculosActivos, int VinculosEnValidacion, int VinculosInvitados, int VinculosVencidos,
    int VinculosBloqueados, int ExpedientesEnCurso, int ExpedientesObservados, int ExpedientesAprobados, int ExpedientesRechazados,
    double? TiempoPromedioDias, double? TiempoMedianaDias, int CertificadosVigentes, int CertificadosConPendientes, int CertificadosNoValidos,
    int VinculosPorVencer = 0, int VinculosEnRevision = 0, int CertificadosEnVerificacion = 0);
