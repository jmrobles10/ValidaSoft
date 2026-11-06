using Marten;
using Validasoft.Dominio.Auditoria;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Escribe el registro de cambio de configuración en la misma sesión (misma transacción) que el cambio (R-1).</summary>
public static class Auditoria
{
    public static void Registrar(IDocumentSession sesion, IContextoSolicitud contexto, string entidad, string entidadId,
        AccionConfiguracion accion, object? antes, object? despues, DateTimeOffset fecha, Guid? empresaId = null) =>
        sesion.Store(new RegistroDeCambioDeConfiguracion
        {
            Id = Guid.CreateVersion7(),
            EmpresaId = empresaId,
            Entidad = entidad,
            EntidadId = entidadId,
            Accion = accion,
            Antes = antes,
            Despues = despues,
            ActorTipo = contexto.ActorTipo,
            ActorId = contexto.ActorId,
            CorrelationId = contexto.CorrelationId,
            Fecha = fecha
        });
}
