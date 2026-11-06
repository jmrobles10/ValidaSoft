using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
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
            v.CreadaEn, v.PublicadaEn, v.RetiradaEn,
            [.. v.Requisitos.Select(r => new RequisitoDelFlujoDto(r.Id, r.TipoDocumentoTenantId, r.CodigoTipo, r.Obligatorio, r.AreasQueRevisan,
                r.AprobadoresRequeridos))]);

    /// <summary>
    /// Requisito del cuerpo a dominio: el tipo por id o por código (del tenant). Un código desconocido queda con id vacío y
    /// el dominio responde 422 <c>requisito-invalido</c>.
    /// </summary>
    public static RequisitoDelFlujo ADominio(this RequisitoDelFlujoDto r, IReadOnlyCollection<TipoDocumentoTenant> tipos)
    {
        var codigo = (r.TipoDocumento ?? string.Empty).Trim().ToUpperInvariant();
        var tipoId = r.TipoDocumentoTenantId ?? tipos.FirstOrDefault(t => t.Codigo == codigo)?.Id ?? Guid.Empty;
        if (tipoId == Guid.Empty && codigo.Length == 0)
            throw ReglaDeNegocioException.Validacion("requisito-invalido", "Cada requisito necesita el tipo de documento (id o código).");
        return new RequisitoDelFlujo(r.Id ?? Guid.Empty, tipoId, codigo, r.Obligatorio, [.. r.AreasQueRevisan ?? []], r.AprobadoresRequeridos);
    }

    public static FlujoDto ADto(this Flujo f, TipoVinculo? tipo, bool esPorDefecto) =>
        new(f.Id, f.EmpresaId, f.Codigo, f.Nombre,
            new TipoVinculoResumenDto(f.TipoVinculoId, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty),
            esPorDefecto, f.VersionPublicada?.ADto(), f.Borrador?.ADto(), f.CreadoEn, f.ActualizadoEn);

    public static VeredictoEmitidoDto ADto(this VeredictoRegistrado v) =>
        new(v.Id, v.Veredicto.ToString(), v.Origen.ToString(), v.RevisorId, v.Fecha, [.. v.Motivos.Select(m => m.Codigo)], v.Observacion,
            v.Justificacion, v.Vigente, v.Superado, v.Anulado, v.AnuladoPor, v.SobreVersion, v.Invalidado, v.Derivado);

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
            e.MotivoAnulacion, e.AbiertoEn, e.CerradoEn, e.ActualizadoEn, e.Version,
            [.. e.Requisitos.Select(r => r.ADto(e))]);

    /// <summary>Requisito con la decisión de cada área que lo revisa (estado, aprobaciones y veredictos de esa área).</summary>
    private static RequisitoExpedienteDto ADto(this RequisitoDeExpediente r, Expediente e) =>
        new(r.Id, r.Tipo(), r.Obligatorio, r.Estado.ToString(), r.VersionActual?.ADto(), r.Reenvios,
            r.Eximido ? new ExencionDto(r.EximidoPor ?? string.Empty, r.JustificacionExencion ?? string.Empty, r.EximidoEn ?? e.ActualizadoEn) : null,
            [
                .. r.AreasQueRevisan.Select(id => (Id: id, Area: e.BuscarArea(id))).Where(x => x.Area is not null).Select(x =>
                    new AreaDeRequisitoDto(x.Id, x.Area!.Codigo, EstadoEnArea(r, x.Area, e), r.AprobadoresPara(x.Area), r.AprobacionesDe(x.Area),
                        [.. r.Veredictos.Where(v => v.AreaId == x.Id).Select(ADto)]))
            ]);

    private static string EstadoEnArea(RequisitoDeExpediente r, AreaDeExpediente area, Expediente e) =>
        r.Eximido ? "Eximido"
        : r.ObservadoPor(area.AreaId) ? "Observado"
        : r.AprobadoPor(area) ? "Aprobado"
        : e.Etapas.First(et => et.Areas.Contains(area)).Estado == EstadoEtapa.Esperando || e.Estado == EstadoExpediente.Abierto ? "Esperando"
        : "Pendiente";

    private static TipoDocumentoRequisitoDto Tipo(this RequisitoDeExpediente r) =>
        new(r.TipoDocumentoTenantId, r.Codigo, r.Nombre, r.TipoCanonico, r.Ambito.ToString());

    private static VersionRequisitoDto ADto(this VersionDeRequisito v) => new(v.DocumentoId, v.Numero, v.Sha256, v.CargadaEn);

    private static AreaExpedienteDto ADto(this AreaDeExpediente a, IReadOnlyList<Guid> miembros)
    {
        var yaSePronunciaron = a.RevisoresVigentes;
        IReadOnlyList<Guid> pendientes = a.Estado == EstadoAreaExpediente.Pendiente
            ? [.. miembros.Where(m => !yaSePronunciaron.Contains(m.ToString()))]
            : [];
        return new AreaExpedienteDto(a.AreaId, a.Codigo, a.Nombre, a.Estado.ToString(), a.AprobadoresRequeridos, a.Aprobaciones,
            Math.Max(0, a.AprobadoresRequeridos - a.Aprobaciones), pendientes, a.PuedeRechazarExpediente, a.Automatica, a.PendienteDesde,
            a.SlaVence, [.. a.Veredictos.Select(ADto)], a.RequisitosPorDecidir);
    }

    public static FilaBandejaDto ADto(this FilaBandeja f) =>
        new(f.ExpedienteId, f.Folio, f.VinculoId, new TerceroResumenDto(f.TerceroId, f.TerceroNombre, f.TerceroIdentificacion),
            new TipoVinculoResumenDto(f.TipoVinculoId, f.TipoVinculoCodigo, f.TipoVinculoNombre), f.AreaId, f.AreaCodigo, f.AreaNombre,
            f.Etapa, f.EstadoArea.ToString(), f.EstadoExpediente.ToString(), f.Aprobaciones, f.AprobadoresRequeridos, f.Desde, f.ActualizadoEn,
            f.RequisitosPendientes);

    /// <summary>
    /// Vista del tercero: estado, observaciones vigentes con el texto del motivo (y el documento observado) y los
    /// requisitos que le piden con su estado y sus observaciones; nunca áreas, revisores ni veredictos internos.
    /// </summary>
    public static ExpedientePublicoDto APublico(this Expediente e, Empresa? empresa) =>
        new(e.Id, e.Folio, empresa.APublica(e.EmpresaId), new TipoVinculoResumenDto(e.TipoVinculoId, e.TipoVinculoCodigo, e.TipoVinculoNombre),
            e.Estado.ToString(),
            [.. e.ObservacionesVigentes.Select(v => v.APublica(v.RequisitoId is { } id ? e.BuscarRequisito(id)?.Codigo : null))],
            e.Estado == EstadoExpediente.Observado, e.Reenvios, e.ActualizadoEn,
            [
                // Una observación solo llega al tercero cuando el expediente se le devuelve (con AlCerrarArea puede esperar).
                .. e.Requisitos.Select(r => new RequisitoPublicoDto(r.Id, r.Tipo(), r.Obligatorio,
                    (r.Estado == EstadoRequisito.Observado && e.Estado != EstadoExpediente.Observado ? EstadoRequisito.EnRevision : r.Estado).ToString(),
                    r.VersionActual?.ADto(),
                    e.Estado == EstadoExpediente.Observado
                        ? [.. r.Veredictos.Where(v => v.Vigente && v.Veredicto == Veredicto.Observado).OrderBy(v => v.Fecha).Select(v => v.APublica(r.Codigo))]
                        : []))
            ],
            e.Requisitos.Count(r => r.Obligatorio && r.Estado == EstadoRequisito.Pendiente));

    private static ObservacionPublicaDto APublica(this VeredictoRegistrado v, string? requisito) =>
        new([.. v.Motivos.Select(m => new MotivoPublicoDto(m.Codigo, m.TextoAlTercero))], v.Observacion, v.Fecha, requisito);
}
