using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Plataforma;

/// <summary>
/// Solicitudes de acceso a terceros en la consola de Plataforma (decisión AV del registro): cuando un correo que no es contacto de un tercero pide acceso y el tercero
/// <b>no tiene ningún contacto verificado</b>, nadie del tercero puede decidir y la solicitud espera aquí, con la evidencia: las empresas (y grupos) que invitaron al correo, los
/// contactos del tercero con su verificación y los correos del RUT y de la Cámara de comercio si el tercero los tiene. Un operador aprueba o rechaza; queda en la auditoría
/// y en el historial de la solicitud. Un operador también puede verificar un contacto (medio <c>Plataforma</c>) —lo que devuelve la decisión a los contactos del tercero—.
/// </summary>
public static class SolicitudesDeAccesoDeLaPlataformaEndpoints
{
    public const string Entidad = "SolicitudDeAccesoATercero";
    public const string EntidadContacto = "ContactoDeTercero";

    private const int MaximoDeSolicitudes = 200;

    /// <summary>
    /// Las solicitudes de acceso a terceros. <c>estado</c>: <c>cola</c> (por omisión: pendientes de terceros sin contactos verificados, las más antiguas primero),
    /// <c>pendientes</c> (todas las pendientes, también las que decide un contacto), <c>resueltas</c> o <c>todas</c> (las más recientes primero).
    /// </summary>
    [WolverineGet("/api/v1/plataforma/solicitudes-de-acceso")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IReadOnlyList<SolicitudDeAccesoDePlataformaDto>> Listar(string? estado, ISesiones sesiones, CancellationToken ct)
    {
        var filtro = (estado ?? "cola").Trim().ToLowerInvariant();
        if (filtro is not ("cola" or "pendientes" or "resueltas" or "todas"))
            throw ReglaDeNegocioException.Validacion("estado-invalido", "El estado es cola, pendientes, resueltas o todas.");

        await using var boveda = sesiones.ConsultaBoveda();
        var solicitudes = await boveda.Query<SolicitudDeAcceso>().ToListAsync(ct);
        var terceroIds = solicitudes.Select(s => s.TerceroId).Distinct().ToArray();
        var terceros = terceroIds.Length == 0 ? [] : (await boveda.LoadManyAsync<Tercero>(ct, terceroIds)).ToDictionary(t => t.Id);

        var elegidas = solicitudes.Where(s => terceros.ContainsKey(s.TerceroId) && filtro switch
        {
            "cola" => s.EstaPendiente && AccesoDelTercero.DecideLaPlataforma(terceros[s.TerceroId]),
            "pendientes" => s.EstaPendiente,
            "resueltas" => !s.EstaPendiente,
            _ => true
        });
        var ordenadas = filtro == "cola"
            ? elegidas.OrderBy(s => s.CreadaEn)
            : elegidas.OrderByDescending(s => s.ActualizadaEn);
        var pagina = ordenadas.Take(MaximoDeSolicitudes).ToList();

        // La evidencia: grupos de las empresas que invitaron y correos oficiales de cada tercero.
        var tenantIds = pagina.SelectMany(s => s.Invitaciones.Select(i => i.TenantId)).Distinct().ToArray();
        await using var global = sesiones.ConsultaGlobal();
        var grupos = tenantIds.Length == 0 ? [] : (await global.LoadManyAsync<Tenant>(ct, tenantIds)).ToDictionary(t => t.Id, t => t.Nombre);
        var oficiales = new Dictionary<Guid, IReadOnlyList<CorreoOficial>>();
        foreach (var terceroId in pagina.Select(s => s.TerceroId).Distinct())
            oficiales[terceroId] = await ContactosDelTercero.CorreosOficialesAsync(boveda, terceroId, ct);

        return [.. pagina.Select(s => ADto(s, terceros[s.TerceroId], grupos, oficiales[s.TerceroId]))];
    }

    /// <summary>
    /// Un operador aprueba el correo: pasa a ser contacto verificado del tercero (medio <c>Plataforma</c>), las empresas que lo invitaron reciben lo que ya les corresponde y
    /// lo que ese correo subió se promueve. Solo si el tercero no tiene contactos verificados (409 <c>decide-un-contacto</c>); idempotente. 404 <c>solicitud-no-encontrada</c>.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/solicitudes-de-acceso/{id}/aprobar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SolicitudDeAccesoDePlataformaDto> Aprobar(Guid id, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, ICacheDeSesionesDeTercero cache, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var antes = await CargarAsync(sesiones, id, ct);
        var decidida = await DecisionDeAcceso.AprobarAsync(id, antes.TerceroId, QuienDecide.UnOperador(contexto.ActorId), sesiones, contexto, mensajes, almacen, reglas, cache,
            ahora, ct);
        await AuditarAsync(sesiones, contexto, antes, decidida, null, ahora, ct);
        return await ADtoAsync(sesiones, decidida, ct);
    }

    /// <summary>
    /// Un operador rechaza el correo, con un motivo opcional: se cortan sus sesiones, no vuelve a entrar a este tercero y la empresa ve el rechazo en su vínculo. Solo si el
    /// tercero no tiene contactos verificados (409 <c>decide-un-contacto</c>); un correo ya aprobado no se rechaza (409 <c>solicitud-ya-aprobada</c>).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/solicitudes-de-acceso/{id}/rechazar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SolicitudDeAccesoDePlataformaDto> Rechazar(Guid id, RechazarAccesoDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        ICacheDeSesionesDeTercero cache, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var antes = await CargarAsync(sesiones, id, ct);
        var decidida = await DecisionDeAcceso.RechazarAsync(id, dto.Motivo, antes.TerceroId, QuienDecide.UnOperador(contexto.ActorId), sesiones, cache, ahora, ct);
        await AuditarAsync(sesiones, contexto, antes, decidida, dto.Motivo, ahora, ct);
        return await ADtoAsync(sesiones, decidida, ct);
    }

    /// <summary>
    /// Un operador verifica el correo de un contacto del tercero (medio <c>Plataforma</c>): desde ahí decide los accesos aislados del tercero y desplaza a los contactos sin
    /// verificar (el verificado prevalece). Idempotente. 404 <c>contacto-no-encontrado</c> si ese correo no es contacto del tercero.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/terceros/{terceroId}/contactos/verificar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ContactoDelTerceroDto> VerificarContacto(Guid terceroId, VerificarContactoDto dto, ISesiones sesiones, IContextoSolicitud contexto, IMessageBus mensajes,
        TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Boveda(mensajes);
        var tercero = await sesion.LoadAsync<Tercero>(terceroId, ct) ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero.");
        var contacto = tercero.BuscarContacto(dto.Correo)
                       ?? throw ReglaDeNegocioException.NoEncontrado("contacto-no-encontrado", "Ese correo no es contacto del tercero.");
        var operador = $"plataforma:{contexto.ActorId}";
        var cambio = ContactosDelTercero.Verificar(sesion, tercero, contacto.Correo, MedioDeVerificacion.Plataforma, operador, ahora);
        if (cambio)
        {
            // Que otro contacto quede desplazado, o que una solicitud de este correo se apruebe sola, lo resuelve la sincronización del acceso.
            await mensajes.PublishAsync(new SincronizarAccesoDelTercero(terceroId));
            await sesion.SaveChangesAsync(ct);
            await using var auditoria = sesiones.Global();
            Auditoria.Registrar(auditoria, contexto, EntidadContacto, $"{terceroId:N}:{contacto.Correo}", AccionConfiguracion.Modificado,
                new { Verificacion = nameof(EstadoDeVerificacion.SinVerificar) },
                new { Verificacion = nameof(EstadoDeVerificacion.Verificado), Medio = nameof(MedioDeVerificacion.Plataforma), Nota = dto.Nota }, ahora);
            await auditoria.SaveChangesAsync(ct);
        }

        var actual = tercero.BuscarContacto(contacto.Correo)!;
        return new ContactoDelTerceroDto(actual.Nombre, actual.Correo, actual.Rol, actual.Verificacion.ToString(), actual.Medio?.ToString(), actual.VerificadoEn, false,
            AccesoDelTercero.EstaDesplazado(tercero, actual.Correo));
    }

    private static async Task<SolicitudDeAcceso> CargarAsync(ISesiones sesiones, Guid id, CancellationToken ct)
    {
        await using var boveda = sesiones.ConsultaBoveda();
        return await boveda.LoadAsync<SolicitudDeAcceso>(id, ct)
               ?? throw ReglaDeNegocioException.NoEncontrado("solicitud-no-encontrada", "No existe esa solicitud de acceso.");
    }

    private static async Task AuditarAsync(ISesiones sesiones, IContextoSolicitud contexto, SolicitudDeAcceso antes, SolicitudDeAcceso despues, string? motivo, DateTimeOffset ahora,
        CancellationToken ct)
    {
        await using var sesion = sesiones.Global();
        Auditoria.Registrar(sesion, contexto, Entidad, despues.Id.ToString(), AccionConfiguracion.Modificado,
            new { Estado = antes.Estado.ToString(), antes.Correo, antes.TerceroId },
            new { Estado = despues.Estado.ToString(), despues.Correo, despues.TerceroId, Via = nameof(ViaDeDecision.Plataforma), Motivo = motivo }, ahora);
        await sesion.SaveChangesAsync(ct);
    }

    private static async Task<SolicitudDeAccesoDePlataformaDto> ADtoAsync(ISesiones sesiones, SolicitudDeAcceso solicitud, CancellationToken ct)
    {
        await using var boveda = sesiones.ConsultaBoveda();
        var tercero = await boveda.LoadAsync<Tercero>(solicitud.TerceroId, ct) ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero.");
        var tenantIds = solicitud.Invitaciones.Select(i => i.TenantId).Distinct().ToArray();
        await using var global = sesiones.ConsultaGlobal();
        var grupos = tenantIds.Length == 0 ? [] : (await global.LoadManyAsync<Tenant>(ct, tenantIds)).ToDictionary(t => t.Id, t => t.Nombre);
        return ADto(solicitud, tercero, grupos, await ContactosDelTercero.CorreosOficialesAsync(boveda, solicitud.TerceroId, ct));
    }

    private static SolicitudDeAccesoDePlataformaDto ADto(SolicitudDeAcceso s, Tercero tercero, IReadOnlyDictionary<Guid, string> grupos, IReadOnlyList<CorreoOficial> oficiales) =>
        new(s.Id, tercero.Id, tercero.Nombre, $"{tercero.Identificacion.Tipo} {tercero.Identificacion.Numero}", s.Correo, s.Estado.ToString(), s.CreadaEn,
            [.. s.Invitaciones.OrderBy(i => i.Fecha).Select(i => new EmpresaQueInvitoPlataformaDto(i.Empresa, grupos.GetValueOrDefault(i.TenantId) ?? "—", i.TenantId, i.Fecha))],
            [.. tercero.Contactos.Select(c => new ContactoDelTerceroDto(c.Nombre, c.Correo, c.Rol, c.Verificacion.ToString(), c.Medio?.ToString(), c.VerificadoEn, false,
                AccesoDelTercero.EstaDesplazado(tercero, c.Correo)))],
            [.. oficiales.Select(o => new CorreoOficialDto(o.Correo, o.Documento, o.Version, o.CargadaPorCorreo,
                o.Respalda(tercero.EsContactoVerificado, [.. s.Invitaciones.Select(i => i.TenantId).Distinct()])))],
            s.EstaPendiente && AccesoDelTercero.DecideLaPlataforma(tercero), s.Origen == SolicitudDeAcceso.OrigenContactoDesplazado, s.DecididoPor, s.DecididoVia?.ToString(),
            s.DecididoEn, s.MotivoDelRechazo);
}
