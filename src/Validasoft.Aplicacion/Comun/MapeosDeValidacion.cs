using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Traducción entidad → DTO de motivos, flujos, expedientes y bandeja. La vista del tercero nunca nombra áreas ni revisores.</summary>
public static class MapeosDeValidacion
{
    public static MotivoTipificadoDto ADto(this Motivo m) =>
        new(m.Id, m.Codigo, m.Nombre, m.Categoria.ToString(), m.AplicaA.ToString(), m.TextoAlTercero, m.Base, m.Activo);

    public static EtapaDto ADto(this EtapaDeFlujo e) =>
        new(e.Orden, [.. e.Areas.Select(a => new AreaDeEtapaDto(a.AreaId, a.AprobadoresRequeridos, a.PuedeRechazarExpediente, a.Automatica, a.SlaHoras))]);

    public static EtapaDeFlujo ADominio(this EtapaDto e) =>
        new(e.Orden, [.. (e.Areas ?? []).Select(a => new AreaDeEtapa(a.AreaId, a.AprobadoresRequeridos, a.PuedeRechazarExpediente, a.Automatica, a.SlaHoras))]);

    public static FlujoVersionDto ADto(this FlujoVersion v) =>
        new(v.Id, v.Version, v.Estado.ToString(), [.. v.Etapas.Select(ADto)], v.PoliticaDevolucion.ToString(), v.SeparacionDeFunciones,
            v.CreadaEn, v.PublicadaEn, v.RetiradaEn);

    public static FlujoDto ADto(this Flujo f, TipoVinculo? tipo, bool esPorDefecto) =>
        new(f.Id, f.EmpresaId, f.Codigo, f.Nombre,
            new TipoVinculoResumenDto(f.TipoVinculoId, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty),
            esPorDefecto, f.VersionPublicada?.ADto(), f.Borrador?.ADto(), f.CreadoEn, f.ActualizadoEn);

    public static VeredictoEmitidoDto ADto(this VeredictoRegistrado v) =>
        new(v.Id, v.Veredicto.ToString(), v.Origen.ToString(), v.RevisorId, v.Fecha, [.. v.Motivos.Select(m => m.Codigo)], v.Observacion,
            v.Justificacion, v.Vigente, v.Superado, v.Anulado, v.AnuladoPor);

    /// <summary><paramref name="miembros"/>: miembros actuales de cada área (para listar quién falta por pronunciarse).</summary>
    public static ExpedienteDto ADto(this Expediente e, IReadOnlyDictionary<Guid, List<Guid>> miembros) =>
        new(e.Id, e.Folio, e.EmpresaId, e.VinculoId,
            new TerceroResumenDto(e.TerceroId, e.TerceroNombre, e.TerceroIdentificacion),
            new TipoVinculoResumenDto(e.TipoVinculoId, e.TipoVinculoCodigo, e.TipoVinculoNombre),
            e.Tipo.ToString(), e.Estado.ToString(),
            e.Flujo is null ? null : new FlujoDeExpedienteDto(e.Flujo.FlujoId, e.Flujo.FlujoVersionId, e.Flujo.Codigo, e.Flujo.Nombre, e.Flujo.Version,
                e.Flujo.Origen.ToString()),
            e.PoliticaDevolucion.ToString(), e.SeparacionDeFunciones, e.EtapaActiva?.Orden,
            [.. e.Etapas.Select(et => new EtapaExpedienteDto(et.Orden, et.Estado.ToString(), et.ActivadaEn, et.AprobadaEn,
                [.. et.Areas.Select(a => a.ADto(miembros.TryGetValue(a.AreaId, out var m) ? m : []))]))],
            e.Reenvios, e.UltimaRespuesta,
            e.Rechazo is null ? null : new RechazoDto(e.Rechazo.AreaId, e.Rechazo.RevisorId, [.. e.Rechazo.Motivos.Select(m => m.Codigo)],
                e.Rechazo.Justificacion, e.Rechazo.Fecha),
            e.MotivoAnulacion, e.AbiertoEn, e.CerradoEn, e.ActualizadoEn, e.Version);

    private static AreaExpedienteDto ADto(this AreaDeExpediente a, IReadOnlyList<Guid> miembros)
    {
        var yaSePronunciaron = a.RevisoresVigentes;
        IReadOnlyList<Guid> pendientes = a.Estado == EstadoAreaExpediente.Pendiente
            ? [.. miembros.Where(m => !yaSePronunciaron.Contains(m.ToString()))]
            : [];
        return new AreaExpedienteDto(a.AreaId, a.Codigo, a.Nombre, a.Estado.ToString(), a.AprobadoresRequeridos, a.Aprobaciones,
            Math.Max(0, a.AprobadoresRequeridos - a.Aprobaciones), pendientes, a.PuedeRechazarExpediente, a.Automatica, a.PendienteDesde,
            a.SlaVence, [.. a.Veredictos.Select(ADto)]);
    }

    public static FilaBandejaDto ADto(this FilaBandeja f) =>
        new(f.ExpedienteId, f.Folio, f.VinculoId, new TerceroResumenDto(f.TerceroId, f.TerceroNombre, f.TerceroIdentificacion),
            new TipoVinculoResumenDto(f.TipoVinculoId, f.TipoVinculoCodigo, f.TipoVinculoNombre), f.AreaId, f.AreaCodigo, f.AreaNombre,
            f.Etapa, f.EstadoArea.ToString(), f.EstadoExpediente.ToString(), f.Aprobaciones, f.AprobadoresRequeridos, f.Desde, f.ActualizadoEn);

    /// <summary>Vista del tercero: estado y observaciones vigentes con el texto del motivo; sin áreas ni revisores.</summary>
    public static ExpedientePublicoDto APublico(this Expediente e, Empresa? empresa) =>
        new(e.Id, e.Folio, empresa.APublica(e.EmpresaId), new TipoVinculoResumenDto(e.TipoVinculoId, e.TipoVinculoCodigo, e.TipoVinculoNombre),
            e.Estado.ToString(),
            [.. e.ObservacionesVigentes.Select(v => new ObservacionPublicaDto([.. v.Motivos.Select(m => new MotivoPublicoDto(m.Codigo, m.TextoAlTercero))],
                v.Observacion, v.Fecha))],
            e.Estado == EstadoExpediente.Observado, e.Reenvios, e.ActualizadoEn);
}
