using Validasoft.Contratos;
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
        new(e.Id, e.TenantId, e.Identificacion.ADto(), e.Nombre, e.LogoUrl, e.ReplyTo, e.PrefijoFolio, e.Activa);

    public static ApiKeyDto ADto(this ApiKey k, string? valor = null) =>
        new(k.Id, k.Prefijo, k.Nombre, k.Scopes, k.Empresas, k.ExpiraEn, k.CreadaEn, k.Sandbox, valor);

    public static AtributoPermitidoDto ADto(this AtributoPermitido a) => new(a.Clave, a.Tipo.ToString(), a.Obligatorio, a.Opciones);

    public static AtributoPermitido ADominio(this AtributoPermitidoDto a) =>
        Enum.TryParse<TipoAtributo>(a.Tipo, true, out var tipo)
            ? new AtributoPermitido(a.Clave, tipo, a.Obligatorio, a.Opciones)
            : throw Dominio.Comun.ReglaDeNegocioException.Validacion("tipo-atributo-invalido",
                $"'{a.Tipo}' no es un tipo de atributo ({string.Join(", ", Enum.GetNames<TipoAtributo>())}).");

    public static TipoVinculoDto ADto(this TipoVinculo t) =>
        new(t.Id, t.Codigo, t.Nombre, [.. t.AtributosPermitidos.Select(ADto)], t.ExigeCuentaDesignada, t.Activo);

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
            v.ReferenciaExterna is null ? null : new ReferenciaExternaDto(v.ReferenciaExterna.Sistema, v.ReferenciaExterna.Id),
            v.Origen.ToString(), v.CreadoEn, v.ActualizadoEn, v.Version);
}
