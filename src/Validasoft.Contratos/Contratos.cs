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

public sealed record CrearApiKeyDto(
    string Nombre, IReadOnlyList<string> Scopes, IReadOnlyList<string>? Empresas, DateTimeOffset? ExpiraEn, bool Sandbox = false);

public sealed record ApiKeyDto(
    Guid Id, string Prefijo, string Nombre, IReadOnlyList<string> Scopes, IReadOnlyList<string> Empresas,
    DateTimeOffset? ExpiraEn, DateTimeOffset CreadaEn, bool Sandbox, string? Valor);

// ---------- Tipos de vínculo ----------

public sealed record AtributoPermitidoDto(string Clave, string Tipo, bool Obligatorio, IReadOnlyList<string>? Opciones);

public sealed record CrearTipoVinculoDto(
    string Codigo, string Nombre, IReadOnlyList<AtributoPermitidoDto>? AtributosPermitidos, bool ExigeCuentaDesignada);

public sealed record TipoVinculoDto(
    Guid Id, string Codigo, string Nombre, IReadOnlyList<AtributoPermitidoDto> AtributosPermitidos, bool ExigeCuentaDesignada, bool Activo);

public sealed record TipoVinculoResumenDto(Guid Id, string Codigo, string Nombre);

// ---------- Terceros ----------

public sealed record ContactoDto(string Nombre, string Correo, string? Telefono, string Rol);

public sealed record CrearTerceroDto(
    IdentificacionDto Identificacion, string TipoPersona, string Nombre, string? NombreComercial, IReadOnlyList<ContactoDto>? Contactos);

public sealed record TerceroDto(
    Guid Id, IdentificacionDto Identificacion, string TipoPersona, string Nombre, string? NombreComercial,
    IReadOnlyList<ContactoDto> Contactos, bool EsExtranjero, DateTimeOffset CreadoEn);

// ---------- Vínculos ----------

public sealed record ReferenciaExternaDto(string Sistema, string Id);

public sealed record InvitacionDto(string? CorreoDestino, int VigenciaHoras = 72);

/// <summary>
/// Alta de vínculo. Se identifica al tercero por <see cref="TerceroId"/> o por sus datos; si ya existe en la
/// plataforma se reutiliza sin modificar sus datos maestros (P-1-7).
/// </summary>
public sealed record CrearVinculoDto(
    Guid? TerceroId,
    CrearTerceroDto? Tercero,
    string TipoVinculo,
    Dictionary<string, JsonElement>? Atributos,
    string CorreoInvitacion,
    ReferenciaExternaDto? ReferenciaExterna,
    bool EnviarInvitacion = true,
    int VigenciaInvitacionHoras = 72);

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
    ReferenciaExternaDto? ReferenciaExterna,
    string Origen,
    DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn,
    long Version);

public sealed record ActualizarAtributosDto(Dictionary<string, JsonElement> Atributos);

public sealed record BloquearVinculoDto(string Motivo, DateTimeOffset? Hasta);

public sealed record MotivoDto(string Motivo);

public sealed record JustificacionDto(string Justificacion);

public sealed record EventoHistorialDto(long Version, string Tipo, DateTimeOffset Fecha, string? ActorTipo, string? ActorId, JsonElement Datos);
