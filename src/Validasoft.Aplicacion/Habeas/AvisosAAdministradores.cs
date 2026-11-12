using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Integraciones;
using Wolverine;

namespace Validasoft.Aplicacion.Habeas;

/// <summary>
/// Quién recibe un aviso de operación (habeas data, límites del plan): los administradores —del grupo o, si el aviso es de una
/// empresa, también los de esa empresa— y, si <see cref="ConOficial"/>, el oficial de cumplimiento de la empresa.
/// </summary>
public sealed record DestinatariosDeAviso(Guid? EmpresaId, bool ConOficial = false)
{
    public bool Incluye(Usuario u) => u.Activo && u.Roles.Any(r =>
        (r.Rol == Rol.Administrador && (r.EmpresaId is null || EmpresaId is null || r.EmpresaId == EmpresaId)) ||
        (ConOficial && r.Rol == Rol.OficialCumplimiento && (r.EmpresaId is null || r.EmpresaId == EmpresaId)));
}

/// <summary>
/// Avisos a las personas de un grupo que no salen de un evento de un stream sino de un barrido o de una petición del tercero (habeas
/// data, límites del plan): campana en la misma transacción y correo por el outbox, uno por persona y hecho (id determinista: repetir
/// el aviso no lo duplica) y solo por correo si la persona no lo apagó.
/// </summary>
public static class AvisosAAdministradores
{
    /// <summary>
    /// Avisa a <paramref name="destinatarios"/> del tenant. <paramref name="claveDelHecho"/> identifica el hecho (para no repetirlo).
    /// Devuelve a cuántas personas avisó (0 si no hay ninguna). <paramref name="mensajes"/> debe ser el contexto de mensajería de la
    /// operación (de Wolverine); si es nulo no sale correo, solo campana.
    /// </summary>
    public static async Task<int> AvisarAsync(ISesiones sesiones, IMessageBus? mensajes, Guid tenantId, DestinatariosDeAviso destinatarios, string tipo, string titulo,
        string mensaje, string? ruta, string claveDelHecho, string? nombreDeLaEmpresa, DateTimeOffset ahora, CancellationToken ct)
    {
        // La sesión del outbox toma el tenant del contexto del mensaje (TD-8): se fija para esta transacción y se devuelve, así otra sesión
        // del mismo contexto (la de un stream de la bóveda) no hereda el tenant del grupo.
        var contexto = mensajes as IMessageContext;
        var tenantPrevio = contexto?.TenantId;
        if (contexto is not null)
            contexto.TenantId = tenantId.ToString("N");
        try
        {
            return await AvisarEnElTenantAsync(sesiones, mensajes, tenantId, destinatarios, tipo, titulo, mensaje, ruta, claveDelHecho, nombreDeLaEmpresa, ahora, ct);
        }
        finally
        {
            if (contexto is not null)
                contexto.TenantId = tenantPrevio;
        }
    }

    private static async Task<int> AvisarEnElTenantAsync(ISesiones sesiones, IMessageBus? mensajes, Guid tenantId, DestinatariosDeAviso destinatarios, string tipo, string titulo,
        string mensaje, string? ruta, string claveDelHecho, string? nombreDeLaEmpresa, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = mensajes is null ? sesiones.ParaTenant(tenantId) : sesiones.ParaTenant(tenantId, mensajes);
        var personas = (await sesion.Query<Usuario>().Where(u => u.Activo).ToListAsync(ct)).Where(destinatarios.Incluye).OrderBy(u => u.Id).ToList();
        if (personas.Count == 0)
            return 0;

        var preferencias = (await sesion.LoadManyAsync<PreferenciasNotificacion>(ct, personas.Select(u => u.Id).ToArray())).ToDictionary(p => p.Id);
        foreach (var persona in personas)
        {
            sesion.Store(new Notificacion
            {
                Id = Deterministas.Guid($"notificacion:{tipo}:{claveDelHecho}:{persona.Id:N}"),
                UsuarioId = persona.Id,
                EmpresaId = destinatarios.EmpresaId,
                Tipo = tipo,
                Titulo = titulo,
                Mensaje = mensaje,
                Enlace = ruta,
                CreadaEn = ahora,
                Orden = ahora.UtcTicks
            });
            if (mensajes is not null && TiposNotificacion.QuiereCorreo(preferencias.GetValueOrDefault(persona.Id), tipo))
                await mensajes.PublishAsync(new CorreoDeNotificacionPendiente(persona.Correo, persona.Nombre, titulo, [mensaje], ruta, TenantId: tenantId,
                    EmpresaId: destinatarios.EmpresaId, Empresa: nombreDeLaEmpresa));
        }

        await sesion.SaveChangesAsync(ct);
        return personas.Count;
    }
}
