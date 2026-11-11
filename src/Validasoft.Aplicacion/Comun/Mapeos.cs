using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Traducción entidad → DTO público. Nunca expone hashes ni datos de otra empresa.</summary>
public static class Mapeos
{
    public static IdentificacionDto ADto(this Identificacion i) => new(i.Pais, i.Tipo, i.Numero, i.DigitoVerificacion);

    public static Identificacion ADominio(this IdentificacionDto i) =>
        Identificacion.Crear(i.Pais, i.Tipo, i.Numero, i.DigitoVerificacion);

    public static TipoIdentificacionDto ADto(this TipoIdentificacion t) =>
        new(t.Pais, t.Codigo, t.Nombre, t.AplicaA.ToString(), t.Patron, t.TieneDv);

    public static TenantDto ADto(this Tenant t) =>
        new(t.Id, t.Slug, t.Nombre, t.Region, t.Plan, t.ZonaHoraria, t.Idioma, t.Activo, t.CreadoEn);

    public static EmpresaDto ADto(this Empresa e) =>
        new(e.Id, e.TenantId, e.Identificacion.ADto(), e.Nombre, e.LogoUrl, e.ReplyTo, e.PrefijoFolio, e.Activa, e.ColorMarca);

    public static ApiKeyDto ADto(this ApiKey k, string? valor = null) =>
        new(k.Id, k.Prefijo, k.Nombre, k.Scopes, k.Empresas, k.ExpiraEn, k.CreadaEn, k.Sandbox, valor, k.SistemaId, k.RotadaDe, k.ReemplazadaPor);

    public static SistemaIntegradoDto ADto(this SistemaIntegrado s) =>
        new(s.Id, s.Codigo, s.Nombre, s.ClavePublicaPem, s.Activo, s.CreadoEn, [.. s.OrigenesPermitidos]);

    public static RolAsignadoDto ADto(this RolAsignado r) => new(r.Rol.ToString(), r.EmpresaId);

    public static RolAsignado ADominio(this RolAsignadoDto r) =>
        Enum.TryParse<Rol>(r.Rol, true, out var rol) && Enum.IsDefined(rol)
            ? new RolAsignado(rol, r.EmpresaId)
            : throw Dominio.Comun.ReglaDeNegocioException.Validacion("rol-invalido",
                $"'{r.Rol}' no es un rol ({string.Join(", ", Enum.GetNames<Rol>())}).");

    public static UsuarioDto ADto(this Usuario u) =>
        new(u.Id, u.Nombre, u.Correo, u.Identificacion?.ADto(), u.Activo,
            [.. u.Identidades.Select(i => new IdentidadExternaDto(i.SistemaId, i.IdExterno))],
            [.. u.Roles.Select(ADto)], u.CreadoEn, u.UltimoAcceso);

    public static AreaDto ADto(this Area a) => new(a.Id, a.EmpresaId, a.Codigo, a.Nombre, a.Miembros, a.Activa);

    public static AtributoPermitidoDto ADto(this AtributoPermitido a) => new(a.Clave, a.Tipo.ToString(), a.Obligatorio, a.Opciones);

    public static AtributoPermitido ADominio(this AtributoPermitidoDto a) =>
        Enum.TryParse<TipoAtributo>(a.Tipo, true, out var tipo)
            ? new AtributoPermitido(a.Clave, tipo, a.Obligatorio, a.Opciones)
            : throw Dominio.Comun.ReglaDeNegocioException.Validacion("tipo-atributo-invalido",
                $"'{a.Tipo}' no es un tipo de atributo ({string.Join(", ", Enum.GetNames<TipoAtributo>())}).");

    public static TipoVinculoDto ADto(this TipoVinculo t) =>
        new(t.Id, t.Codigo, t.Nombre, [.. t.AtributosPermitidos.Select(ADto)], t.ExigeCuentaDesignada, t.Activo, t.ExigeOtpEnEnlace);

    public static TerceroDto ADto(this Tercero t) =>
        new(t.Id, t.Identificacion.ADto(), t.TipoPersona.ToString(), t.Nombre, t.NombreComercial,
            [.. t.Contactos.Select(c => new ContactoDto(c.Nombre, c.Correo, c.Telefono, c.Rol))], t.EsExtranjero, t.CreadoEn);

    public static VinculoDto ADto(this Vinculo v, TipoVinculo? tipo) =>
        new(v.Id, v.EmpresaId, v.TerceroId,
            new TipoVinculoResumenDto(v.TipoVinculoId, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty),
            v.FlujoId, v.Estado.ToString(), v.Atributos, v.CorreoTercero,
            v.Invitacion is null ? null : new EnlaceInvitacionDto(v.Invitacion.Correo, v.Invitacion.Expira, v.Invitacion.Envios),
            v.InvitacionAceptadaEn,
            RequisitosCubribles: null,
            v.Bloqueo is null ? null : new BloqueoDto(v.Bloqueo.Motivo, v.Bloqueo.Desde, v.Bloqueo.Hasta),
            [.. v.Referencias.Select(r => new ReferenciaExternaDto(r.Sistema, r.Id))],
            v.Origen.ToString(), v.CreadoEn, v.ActualizadoEn, v.Version, v.ExpedienteAbiertoId, v.AperturaDeExpedientePendiente,
            v.UltimoResultado?.ToString(),
            v.Contactos.ADto());

    /// <summary>Los contactos del tercero con la empresa como los ve la API (rol en texto).</summary>
    public static IReadOnlyList<ContactoDeEmpresaDto> ADto(this IEnumerable<ContactoDeEmpresa> contactos) =>
        [.. contactos.Select(c => new ContactoDeEmpresaDto(c.Nombre, c.Cargo, c.Correo, c.Telefono, c.Rol.ToString()))];

    /// <summary>Los contactos que llegan por la API a dominio: el rol debe ser uno de los cuatro (422 si no).</summary>
    public static IReadOnlyList<ContactoDeEmpresa> ADominio(this IEnumerable<ContactoDeEmpresaDto>? contactos) =>
        [.. (contactos ?? []).Select(c => new ContactoDeEmpresa(c.Nombre, c.Cargo, c.Correo, c.Telefono,
            Enum.TryParse<RolDeContacto>(c.Rol, true, out var rol) && Enum.IsDefined(rol)
                ? rol
                : throw ReglaDeNegocioException.Validacion("contacto-rol-invalido", "El rol del contacto es Comercial, Pagos, Cumplimiento o Notificaciones.")))];

    public static TextoLegalDto ADto(this TextoLegal t) =>
        new(t.Id, t.Ambito.ToString(), t.EmpresaId, t.Tipo.ToString(), t.Version, t.Hash, t.Contenido, t.PublicadoEn, t.PublicadoPor);

    public static TextoLegalPublicoDto APublico(this TextoLegal t) => new(t.Tipo.ToString(), t.Version, t.Hash, t.Contenido, t.PublicadoEn);

    public static EmpresaPublicaDto APublica(this Empresa? e, Guid id) => new(id, e?.Nombre ?? string.Empty, e?.LogoUrl, e?.ColorMarca, e?.ReplyTo);

    public static VinculoPublicoDto APublico(this Vinculo v, Empresa? empresa, TipoVinculo? tipo) =>
        new(v.Id, empresa.APublica(v.EmpresaId),
            new TipoVinculoResumenDto(v.TipoVinculoId, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty),
            v.Estado.ToString(), v.InvitacionAceptada, v.InvitacionAceptadaEn, v.ExpedienteAbiertoId ?? v.UltimoExpedienteId, null, v.Contactos.ADto());
}
