using Validasoft.Dominio.Expedientes;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Fila de la proyección <c>BandejaRevision</c> (docs/03 §14): un expediente × un área. Se escribe en la <b>misma
/// transacción</b> que los eventos del expediente, desde el estado resultante (FL-8); su id
/// <c>{expedienteId:N}:{areaId:N}</c> la hace idempotente. Esquema <c>validacion</c>, multi-tenant, aislada por empresa.
/// </summary>
public sealed class FilaBandeja
{
    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public Guid ExpedienteId { get; set; }

    public string Folio { get; set; } = string.Empty;

    public Guid VinculoId { get; set; }

    public Guid TerceroId { get; set; }

    public string TerceroNombre { get; set; } = string.Empty;

    public string TerceroIdentificacion { get; set; } = string.Empty;

    public Guid TipoVinculoId { get; set; }

    public string TipoVinculoCodigo { get; set; } = string.Empty;

    public string TipoVinculoNombre { get; set; } = string.Empty;

    public Guid AreaId { get; set; }

    public string AreaCodigo { get; set; } = string.Empty;

    public string AreaNombre { get; set; } = string.Empty;

    public int Etapa { get; set; }

    public EstadoAreaExpediente EstadoArea { get; set; }

    public EstadoExpediente EstadoExpediente { get; set; }

    public int Aprobaciones { get; set; }

    public int AprobadoresRequeridos { get; set; }

    /// <summary>Con requisitos: los del alcance del área que aún esperan su decisión.</summary>
    public int RequisitosPendientes { get; set; }

    /// <summary>Desde cuándo espera el área (activación de su etapa o reenvío del tercero).</summary>
    public DateTimeOffset? Desde { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    public static string Clave(Guid expedienteId, Guid areaId) => $"{expedienteId:N}:{areaId:N}";
}

/// <summary>Proyecta el estado del expediente a sus filas de bandeja (una por área del flujo).</summary>
public static class ProyeccionBandejaRevision
{
    public static IEnumerable<FilaBandeja> Filas(Expediente e) =>
        e.Etapas.SelectMany(etapa => etapa.Areas.Select(area => new FilaBandeja
        {
            Id = FilaBandeja.Clave(e.Id, area.AreaId),
            EmpresaId = e.EmpresaId,
            ExpedienteId = e.Id,
            Folio = e.Folio,
            VinculoId = e.VinculoId,
            TerceroId = e.TerceroId,
            TerceroNombre = e.TerceroNombre,
            TerceroIdentificacion = e.TerceroIdentificacion,
            TipoVinculoId = e.TipoVinculoId,
            TipoVinculoCodigo = e.TipoVinculoCodigo,
            TipoVinculoNombre = e.TipoVinculoNombre,
            AreaId = area.AreaId,
            AreaCodigo = area.Codigo,
            AreaNombre = area.Nombre,
            Etapa = etapa.Orden,
            EstadoArea = area.Estado,
            EstadoExpediente = e.Estado,
            Aprobaciones = area.Aprobaciones,
            AprobadoresRequeridos = area.AprobadoresRequeridos,
            RequisitosPendientes = area.RequisitosPorDecidir,
            Desde = area.PendienteDesde,
            ActualizadoEn = e.ActualizadoEn
        }));
}
