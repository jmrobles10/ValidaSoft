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
        new(t.Id, t.Slug, t.Nombre, t.Region, t.Plan, t.ZonaHoraria, t.Idioma, t.Activo, t.CreadoEn, t.IngresoADto());

    public static ConfiguracionDeIngresoDto IngresoADto(this Tenant t) =>
        new([.. Enum.GetValues<ModoDeIngreso>().Where(m => m != ModoDeIngreso.Ninguno && t.ModosDeIngreso.HasFlag(m)).Select(m => m.ToString())],
            t.SesionHoras, t.InactividadMinutos, t.PasskeyParaTodos);

    /// <summary>Modos de ingreso del DTO (<c>Delegado</c>, <c>Directo</c>) como banderas; un nombre desconocido ⇒ 422.</summary>
    public static ModoDeIngreso ModosADominio(IEnumerable<string>? modos)
    {
        var resultado = ModoDeIngreso.Ninguno;
        foreach (var modo in modos ?? [])
        {
            if (!Enum.TryParse<ModoDeIngreso>(modo, true, out var m) || m == ModoDeIngreso.Ninguno || !Enum.IsDefined(m))
                throw Dominio.Comun.ReglaDeNegocioException.Validacion("modos-de-ingreso-invalidos",
                    $"'{modo}' no es un modo de ingreso (Delegado, Directo).");
            resultado |= m;
        }

        return resultado;
    }

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

    public static UsuarioDto ADto(this Usuario u, bool passkeyParaTodos = false, int passkeys = 0) =>
        new(u.Id, u.Nombre, u.Correo, u.Identificacion?.ADto(), u.Activo,
            [.. u.Identidades.Select(i => new IdentidadExternaDto(i.SistemaId, i.IdExterno))],
            [.. u.Roles.Select(ADto)], u.CreadoEn, u.UltimoAcceso, u.ExigePasskey(passkeyParaTodos), passkeys, u.InvitadoEn);

    public static AreaDto ADto(this Area a) => new(a.Id, a.EmpresaId, a.Codigo, a.Nombre, a.Miembros, a.Activa);

    public static AtributoPermitidoDto ADto(this AtributoPermitido a) => new(a.Clave, a.Tipo.ToString(), a.Obligatorio, a.Opciones);

    public static AtributoPermitido ADominio(this AtributoPermitidoDto a) =>
        Enum.TryParse<TipoAtributo>(a.Tipo, true, out var tipo)
            ? new AtributoPermitido(a.Clave, tipo, a.Obligatorio, a.Opciones)
            : throw Dominio.Comun.ReglaDeNegocioException.Validacion("tipo-atributo-invalido",
                $"'{a.Tipo}' no es un tipo de atributo ({string.Join(", ", Enum.GetNames<TipoAtributo>())}).");

    public static TipoVinculoDto ADto(this TipoVinculo t) =>
        new(t.Id, t.Codigo, t.Nombre, [.. t.AtributosPermitidos.Select(ADto)], t.ExigeCuentaDesignada, t.Activo, t.ExigeOtpEnEnlace,
            t.RequiereAprobacionParaInvitar, t.AprobadoresDeInvitacion?.ADto());

    public static AprobadoresDeInvitacionDto ADto(this AprobadoresDeInvitacion a) => new(a.Tipo.ToString(), a.Valor);

    /// <summary>Quién aprueba las invitaciones, como llega por la API: el tipo debe ser <c>Rol</c> o <c>Area</c> (422 si no); la validación del valor es del dominio.</summary>
    public static AprobadoresDeInvitacion? ADominio(this AprobadoresDeInvitacionDto? a) =>
        a is null
            ? null
            : Enum.TryParse<TipoDeAprobadores>(a.Tipo, true, out var tipo) && Enum.IsDefined(tipo)
                ? new AprobadoresDeInvitacion(tipo, a.Valor)
                : throw ReglaDeNegocioException.Validacion("aprobadores-invalidos", "Quienes aprueban las invitaciones se indican por un Rol o por un Area.");

    /// <summary>El actor como texto <c>{tipo}:{id}</c> (el mismo formato de <c>LoteDto.CreadoPor</c>).</summary>
    public static string? ATexto(this ActorDeInvitacion? actor) => actor is null ? null : $"{actor.Tipo}:{actor.Id}";

    /// <summary>
    /// La aprobación previa a invitar del vínculo (decisión AI); nula si no pasó por ella. Los nombres de las personas y <c>PuedesDecidir</c> los pone quien consulta
    /// (el detalle y la bandeja) con <c>with</c>: aquí solo va lo que dice el vínculo.
    /// </summary>
    public static InvitacionPorAprobarDto? ADtoDeAprobacion(this Vinculo v) =>
        v.AprobacionDeInvitacion is { } estado
            ? new InvitacionPorAprobarDto(estado.ToString(), v.InvitacionPendienteCorreo ?? v.CorreoTercero, v.InvitacionPendienteVigenciaHoras,
                v.InvitacionSolicitadaEn ?? v.CreadoEn, v.InvitacionSolicitadaPor.ATexto() ?? string.Empty, DecididaPor: v.InvitacionDecididaPor.ATexto(),
                DecididaEn: v.InvitacionDecididaEn, MotivoDeRechazo: v.InvitacionRechazadaMotivo)
            : null;

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
            v.Contactos.ADto(), v.VigenteHasta, v.EnRevisionPorActualizacion,
            v.ActualizacionNoAprobada ? new ActualizacionNoAprobadaDto(v.UltimoExpedienteId, v.UltimoResultado.ToString()!, v.UltimoExpedienteCerradoEn) : null,
            v.AccesoAislado is { } acceso
                ? new AccesoDelCorreoDto(acceso.ToString(), v.CorreoTercero, v.AccesoAisladoDesde ?? v.CreadoEn, v.AccesoAisladoResueltoEn, v.AccesoAisladoMotivo)
                : null,
            InvitacionPorAprobar: v.ADtoDeAprobacion());

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
            v.Estado.ToString(), v.InvitacionAceptada, v.InvitacionAceptadaEn, v.ExpedienteAbiertoId ?? v.UltimoExpedienteId, null, v.Contactos.ADto(), v.VigenteHasta,
            v.EnRevisionPorActualizacion);
}
