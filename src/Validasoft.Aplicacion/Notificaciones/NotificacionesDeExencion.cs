using JasperFx.Events;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Notificaciones;

public sealed partial class PlanificadorDeNotificaciones
{
    /// <summary>
    /// Terminó la fecha de una exención del vínculo (EX-4): la empresa se entera —campana y correo a sus administradores— de que el vínculo vuelve a regirse por las
    /// vigencias y, si algo ya había vencido mientras tanto, de que puede pasar a vencido. Solo la empresa: la exención es privada y el tercero no la ve ni se le avisa.
    /// </summary>
    private static async Task<PlanDeNotificaciones> ExencionVencidaAsync(IEvent evento, ExencionDeVinculoVencida vencida, IContextoDeEventos contexto, long orden,
        CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.VinculoAsync(tenantId, evento.StreamId, ct) is not { } vinculo)
            return PlanDeNotificaciones.Vacio;

        var tercero = (await contexto.TerceroAsync(vinculo.TerceroId, ct))?.Nombre ?? "el tercero";
        var exencion = vinculo.Exenciones.FirstOrDefault(e => e.Id == vencida.ExencionId);
        var alcance = exencion is null || exencion.Todos ? "todos sus requisitos" : string.Join(", ", exencion.Objetivos.Select(o => o.Nombre));
        var titulo = "Terminó una exención";
        var mensaje = $"La exención de {tercero} ({alcance}) terminó el {vencida.Hasta:dd/MM/yyyy}: el vínculo vuelve a regirse por las vigencias y sus " +
                      "vencimientos se avisan y se aplican de nuevo.";
        var ruta = $"/e/{vinculo.EmpresaId}/vinculos/{vinculo.Id}";

        var administradores = (await contexto.UsuariosDelTenantAsync(tenantId, ct))
            .Where(u => u.Activo && u.Roles.Any(r => r.Rol == Rol.Administrador && (r.EmpresaId is null || r.EmpresaId == vinculo.EmpresaId)))
            .OrderBy(u => u.Id)
            .ToList();
        var campana = new List<(Guid TenantId, Notificacion Notificacion)>();
        var correos = new List<object>();
        foreach (var usuario in administradores)
        {
            campana.Add((tenantId, new Notificacion
            {
                Id = Deterministas.Guid($"notificacion:{evento.Id:N}:{vinculo.Id:N}:{usuario.Id:N}:{TiposNotificacion.ExencionVencida}"),
                UsuarioId = usuario.Id,
                EmpresaId = vinculo.EmpresaId,
                Tipo = TiposNotificacion.ExencionVencida,
                Titulo = titulo,
                Mensaje = mensaje,
                VinculoId = vinculo.Id,
                Enlace = ruta,
                CreadaEn = evento.Timestamp,
                Orden = orden
            }));
            if (TiposNotificacion.QuiereCorreo(await contexto.PreferenciasAsync(tenantId, usuario.Id, ct), TiposNotificacion.ExencionVencida))
                correos.Add(new CorreoDeNotificacionPendiente(usuario.Correo, usuario.Nombre, titulo, [mensaje], ruta, TenantId: tenantId, EmpresaId: vinculo.EmpresaId));
        }

        return new PlanDeNotificaciones(campana, correos);
    }
}
