using JasperFx.Events;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Invitaciones;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>
/// Campana de la aprobación antes de invitar (decisión AI): a quienes pueden aprobar cuando una invitación o un lote queda retenido, y a quien la pidió cuando se decide. Solo
/// campana: es un aviso de trabajo pendiente dentro de la aplicación, no un correo.
/// </summary>
public sealed partial class PlanificadorDeNotificaciones
{
    /// <summary>
    /// Una invitación quedó retenida: campana a las personas que pueden aprobarla (las que el tipo designó en la empresa, sin quien la pidió), con el enlace a la bandeja
    /// «Invitaciones por aprobar».
    /// </summary>
    private static async Task<PlanDeNotificaciones> InvitacionPorAprobarAsync(IEvent evento, InvitacionPorAprobarSolicitada solicitada, IContextoDeEventos contexto,
        long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo ||
            await contexto.TipoVinculoAsync(tenantId, vinculo.TipoVinculoId, ct) is not { } tipo)
            return PlanDeNotificaciones.Vacio;

        var aprobadores = await AprobadoresAsync(contexto, tenantId, vinculo.EmpresaId, [tipo], solicitada.SolicitadaPor, ct);
        var tercero = (await contexto.TerceroAsync(vinculo.TerceroId, ct))?.Nombre ?? "El tercero";
        var empresa = (await contexto.EmpresaAsync(vinculo.EmpresaId, ct))?.Nombre ?? "la empresa";
        var mensaje = $"{tercero} ({tipo.Nombre}) espera tu aprobación para recibir la invitación de {ConPunto(empresa)}";
        return Campana(evento, tenantId, vinculo.EmpresaId, aprobadores, TiposNotificacion.InvitacionPorAprobar, "Invitación por aprobar", mensaje, vinculo.Id,
            $"/e/{vinculo.EmpresaId}/invitaciones/por-aprobar", orden);
    }

    /// <summary>Se aprobó o se rechazó una invitación: campana a quien la pidió, si es una persona del tenant, con el motivo del rechazo.</summary>
    private static async Task<PlanDeNotificaciones> InvitacionDecididaAsync(IEvent evento, ActorDeInvitacion solicitante, string? motivo, IContextoDeEventos contexto,
        long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo)
            return PlanDeNotificaciones.Vacio;

        var quien = await PersonaAsync(contexto, tenantId, vinculo.EmpresaId, solicitante, ct);
        if (quien is null)
            return PlanDeNotificaciones.Vacio;

        var tercero = (await contexto.TerceroAsync(vinculo.TerceroId, ct))?.Nombre ?? "el tercero";
        var (titulo, mensaje) = motivo is null
            ? ("Invitación aprobada", $"Se aprobó la invitación a {tercero}: ya salió el correo.")
            : ("Invitación rechazada", $"Se rechazó la invitación a {tercero} y el vínculo quedó anulado: {ConPunto(motivo)}");
        return Campana(evento, tenantId, vinculo.EmpresaId, [quien], TiposNotificacion.InvitacionDecidida, titulo, mensaje, vinculo.Id,
            $"/e/{vinculo.EmpresaId}/vinculos/{vinculo.Id}", orden);
    }

    /// <summary>Un lote quedó por aprobar: campana a quienes pueden aprobar <b>todos</b> sus tipos (sin quien lo creó), con el enlace a su detalle.</summary>
    private static async Task<PlanDeNotificaciones> LotePorAprobarAsync(IEvent evento, LoteDeInvitacionCreado creado, IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId))
            return PlanDeNotificaciones.Vacio;

        var tipos = new List<TipoVinculo>();
        foreach (var id in creado.TiposPorAprobar ?? [])
            if (await contexto.TipoVinculoAsync(tenantId, id, ct) is { } tipo)
                tipos.Add(tipo);
        if (tipos.Count == 0)
            return PlanDeNotificaciones.Vacio;

        var aprobadores = await AprobadoresAsync(contexto, tenantId, creado.EmpresaId, tipos, new ActorDeInvitacion(creado.CreadoPor.Tipo, creado.CreadoPor.Id), ct);
        var mensaje = $"El lote «{creado.Nombre}» ({Plural.Contar(creado.Totales.Pendientes, "fila", "filas")}) espera tu aprobación antes de enviar las invitaciones.";
        return Campana(evento, tenantId, creado.EmpresaId, aprobadores, TiposNotificacion.InvitacionPorAprobar, "Lote de invitaciones por aprobar", mensaje, null,
            $"/e/{creado.EmpresaId}/invitaciones/lotes/{creado.LoteId}", orden);
    }

    /// <summary>Se aprobó o se rechazó un lote: campana a quien lo creó, si es una persona del tenant.</summary>
    private static async Task<PlanDeNotificaciones> LoteDecididoAsync(IEvent evento, string? motivo, IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.LoteAsync(tenantId, evento.StreamId, ct) is not { } lote)
            return PlanDeNotificaciones.Vacio;

        var quien = await PersonaAsync(contexto, tenantId, lote.EmpresaId, new ActorDeInvitacion(lote.CreadoPor.Tipo, lote.CreadoPor.Id), ct);
        if (quien is null)
            return PlanDeNotificaciones.Vacio;

        var (titulo, mensaje) = motivo is null
            ? ("Lote de invitaciones aprobado", $"Se aprobó el lote «{lote.Nombre}»: empezó a enviar las invitaciones.")
            : ("Lote de invitaciones rechazado", $"Se rechazó el lote «{lote.Nombre}» y no se envió ninguna invitación: {ConPunto(motivo)}");
        return Campana(evento, tenantId, lote.EmpresaId, [quien], TiposNotificacion.InvitacionDecidida, titulo, mensaje, null,
            $"/e/{lote.EmpresaId}/invitaciones/lotes/{lote.Id}", orden);
    }

    /// <summary>Las personas que pueden aprobar <b>todos</b> los tipos en la empresa, sin quien pidió.</summary>
    private static async Task<IReadOnlyList<Dominio.Identidad.Usuario>> AprobadoresAsync(IContextoDeEventos contexto, Guid tenantId, Guid empresaId,
        IReadOnlyList<TipoVinculo> tipos, ActorDeInvitacion pidio, CancellationToken ct)
    {
        var usuarios = await contexto.UsuariosDelTenantAsync(tenantId, ct);
        var areas = await contexto.AreasDeLaEmpresaAsync(tenantId, empresaId, ct);
        IEnumerable<Dominio.Identidad.Usuario>? comunes = null;
        foreach (var tipo in tipos)
        {
            var elegibles = AprobadoresDeInvitaciones.Elegibles(tipo, empresaId, usuarios, areas);
            comunes = comunes is null ? elegibles : comunes.Where(u => elegibles.Any(e => e.Id == u.Id));
        }

        return [.. (comunes ?? [])
            .Where(u => !(pidio.Tipo == ActorTipo.Usuario && string.Equals(pidio.Id, u.Id.ToString(), StringComparison.OrdinalIgnoreCase)))
            .OrderBy(u => u.Id)];
    }

    /// <summary>La persona activa con rol en la empresa que es el actor; nulo si es una integración o ya no está.</summary>
    private static async Task<Dominio.Identidad.Usuario?> PersonaAsync(IContextoDeEventos contexto, Guid tenantId, Guid empresaId, ActorDeInvitacion actor, CancellationToken ct) =>
        actor.Tipo == ActorTipo.Usuario && Guid.TryParse(actor.Id, out var usuarioId)
            ? (await contexto.UsuariosAsync(tenantId, [usuarioId], ct)).FirstOrDefault(u => u.Activo && u.TieneRolEn(empresaId))
            : null;

    private static PlanDeNotificaciones Campana(IEvent evento, Guid tenantId, Guid empresaId, IReadOnlyList<Dominio.Identidad.Usuario> destinatarios, string tipo, string titulo,
        string mensaje, Guid? vinculoId, string ruta, long orden) =>
        new([.. destinatarios.Select(u => (tenantId, new Notificacion
        {
            Id = Deterministas.Guid($"notificacion:{evento.Id:N}:{u.Id:N}:{tipo}"),
            UsuarioId = u.Id,
            EmpresaId = empresaId,
            Tipo = tipo,
            Titulo = titulo,
            Mensaje = mensaje,
            VinculoId = vinculoId,
            Enlace = ruta,
            CreadaEn = evento.Timestamp,
            Orden = orden
        }))], []);
}
