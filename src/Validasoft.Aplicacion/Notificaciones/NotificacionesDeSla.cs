using JasperFx.Events;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Integraciones;

namespace Validasoft.Aplicacion.Notificaciones;

public sealed partial class PlanificadorDeNotificaciones
{
    /// <summary>
    /// Terminó una campaña de renovación (OP-5): campana y correo a quien la creó, si es una persona del tenant, con los totales y el enlace a su detalle
    /// (como el aviso del lote de invitación, LT-9).
    /// </summary>
    private static async Task<PlanDeNotificaciones> CampaniaTerminadaAsync(IEvent evento, Dominio.Campanias.CampaniaTerminada terminada, IContextoDeEventos contexto,
        long orden, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.CampaniaAsync(tenantId, evento.StreamId, ct) is not { } campania ||
            campania.CreadaPor.Tipo != Dominio.Comun.ActorTipo.Usuario || !Guid.TryParse(campania.CreadaPor.Id, out var usuarioId))
            return PlanDeNotificaciones.Vacio;
        if ((await contexto.UsuariosAsync(tenantId, [usuarioId], ct)).FirstOrDefault(u => u.Activo && u.TieneRolEn(campania.EmpresaId)) is not { } usuario)
            return PlanDeNotificaciones.Vacio;

        var t = terminada.Totales;
        var titulo = t.Fallidas > 0 ? "Campaña de renovación terminada con errores" : "Campaña de renovación terminada";
        var partes = new List<string> { $"{t.Exitosas} de {t.Filas} vínculos renovados" };
        if (t.Fallidas > 0)
            partes.Add($"{t.Fallidas} fallidos");
        if (t.Omitidas > 0)
            partes.Add($"{t.Omitidas} omitidos");
        var mensaje = $"«{campania.Nombre}»: {string.Join(", ", partes)}.";
        var ruta = $"/e/{campania.EmpresaId}/vencimientos/campanias/{campania.Id}";
        var notificacion = new Notificacion
        {
            Id = Deterministas.Guid($"notificacion:{evento.Id:N}:{usuario.Id:N}:{TiposNotificacion.CampaniaTerminada}"),
            UsuarioId = usuario.Id,
            EmpresaId = campania.EmpresaId,
            Tipo = TiposNotificacion.CampaniaTerminada,
            Titulo = titulo,
            Mensaje = mensaje,
            Enlace = ruta,
            CreadaEn = evento.Timestamp,
            Orden = orden
        };
        List<object> correos = TiposNotificacion.QuiereCorreo(await contexto.PreferenciasAsync(tenantId, usuario.Id, ct), TiposNotificacion.CampaniaTerminada)
            ? [new CorreoDeNotificacionPendiente(usuario.Correo, usuario.Nombre, titulo, [mensaje], ruta, TenantId: tenantId)]
            : [];
        return new PlanDeNotificaciones([(tenantId, notificacion)], correos);
    }

    /// <summary>
    /// El plazo de revisión de un área (SLA, OP-12) está por vencer o venció: campana y correo a quienes revisan por ella (sus miembros y quienes
    /// la tienen delegada) y, cuando venció y la empresa lo pide, también a sus administradores. Una persona recibe un solo aviso aunque esté en
    /// los dos grupos. Respeta las preferencias de correo de cada persona.
    /// </summary>
    private static async Task<PlanDeNotificaciones> SlaAsync(IEvent evento, IContextoDeEventos contexto, Guid areaId, DateTimeOffset vence, bool vencido, long orden,
        CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct) is not { } expediente)
            return PlanDeNotificaciones.Vacio;
        var area = (await contexto.AreasAsync(tenantId, [areaId], ct)).FirstOrDefault(a => a.EmpresaId == expediente.EmpresaId);
        if (area is null)
            return PlanDeNotificaciones.Vacio;

        var empresa = await contexto.EmpresaAsync(expediente.EmpresaId, ct);
        var fecha = CalculadoraDeVigencia.Fecha(vence).ToString("yyyy-MM-dd");
        var (tipo, titulo, mensaje) = vencido
            ? (TiposNotificacion.SlaIncumplido, "Plazo de revisión vencido",
                $"El área {area.Nombre} no decidió a tiempo sobre la validación de {expediente.TerceroNombre}: el plazo venció el {fecha}.")
            : (TiposNotificacion.SlaPorVencer, "Plazo de revisión por vencer",
                $"El área {area.Nombre} tiene hasta el {fecha} para decidir sobre la validación de {expediente.TerceroNombre}: ya consumió casi todo su plazo.");

        var grupos = new List<(string Area, List<Guid> Miembros, Guid AreaId)> { (area.Nombre, area.Miembros, area.Id) };
        if (vencido && (await contexto.ConfiguracionDeSlaAsync(tenantId, expediente.EmpresaId, ct)).AvisarAdministradores)
        {
            var administradores = (await contexto.UsuariosDelTenantAsync(tenantId, ct))
                .Where(u => u.Activo && u.Roles.Any(r => r.Rol == Rol.Administrador && (r.EmpresaId is null || r.EmpresaId == expediente.EmpresaId)))
                .Select(u => u.Id).OrderBy(id => id).ToList();
            if (administradores.Count > 0)
                grupos.Add(("Administración de la empresa", administradores, Guid.Empty));
        }

        return await APersonasAsync(evento, contexto, tenantId, expediente.EmpresaId, empresa?.Nombre, tipo, titulo, mensaje, expediente.Id, expediente.VinculoId,
            expediente.Folio, grupos, orden, ct);
    }
}
