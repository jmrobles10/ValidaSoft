using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Revision;

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
            [.. v.Requisitos.Select(r => r.EsSeccion
                ? new RequisitoDelFlujoDto(r.Id, null, null, r.Obligatorio, r.AreasQueRevisan, r.AprobadoresRequeridos, r.Condicion?.ADto(), r.Seccion,
                    r.Periodos, r.ExigeSoporte, VigenciaDias: r.VigenciaDias, DiasRevalidacion: r.DiasRevalidacion)
                : new RequisitoDelFlujoDto(r.Id, r.TipoDocumentoTenantId, r.CodigoTipo, r.Obligatorio, r.AreasQueRevisan,
                    r.AprobadoresRequeridos, r.Condicion?.ADto(), UsarComoSoporte: r.UsarComoSoporte))],
            [.. v.Verificaciones.Select(x => new VerificacionRequeridaDto(x.Tipo.ToString(), x.Bloqueante, x.VigenciaDias, x.FrecuenciaReconsultaMeses))],
            v.PlazoEfectivo().ADto(), v.ReintentoEfectivo().ADto(), v.VigenciaEfectiva().ADto(), RecordatorioADto(v.RecordatorioObservacionesDias));

    /// <summary>Verificación del cuerpo a dominio; un tipo desconocido responde 422 <c>verificacion-invalida</c>.</summary>
    public static VerificacionRequeridaDelFlujo ADominio(this VerificacionRequeridaDto v) =>
        new(Enum.TryParse<TipoVerificacion>(v.Tipo, true, out var tipo) && Enum.IsDefined(tipo)
                ? tipo
                : throw ReglaDeNegocioException.Validacion("verificacion-invalida",
                    $"«{v.Tipo}» no es una verificación ({string.Join(", ", Enum.GetNames<TipoVerificacion>())})."),
            v.Bloqueante, v.VigenciaDias, v.FrecuenciaReconsultaMeses);

    public static ReintentoDto ADto(this ReintentoTrasRechazo r) => new(r.Permitido, r.DiasEspera);

    public static ReintentoTrasRechazo ADominio(this ReintentoDto r) => new(r.Permitido, r.DiasEspera);

    public static VigenciaVinculoDto ADto(this VigenciaDelVinculo v) => new(v.Dias, [.. v.DiasAviso], v.DiasGracia);

    /// <summary>
    /// Vigencia del cuerpo a dominio: sin avisos indicados, los de por omisión (30, 15 y 5) que quepan antes de vencer; con ellos,
    /// los indicados. El dominio valida el resto (422 <c>vigencia-vinculo-invalida</c>, <c>aviso-vigencia-invalido</c>…).
    /// </summary>
    public static VigenciaDelVinculo ADominio(this VigenciaVinculoDto v) =>
        new(v.Dias, v.DiasAviso ?? [.. VigenciaDelVinculo.AvisosPorOmision.Where(a => v.Dias <= 0 || a < v.Dias)], v.DiasGracia);

    public static RecordatorioDeFlujoDto RecordatorioADto(int? dias) =>
        dias switch { null => new RecordatorioDeFlujoDto("Empresa"), 0 => new RecordatorioDeFlujoDto("Apagado"), var d => new RecordatorioDeFlujoDto("CadaDias", d) };

    /// <summary>Del cuerpo al dominio: <c>Empresa</c> = nulo, <c>Apagado</c> = 0 y <c>CadaDias</c> = los días (422 <c>dias-invalidos</c> si faltan o no son de 1 a 60).</summary>
    public static int? ADominio(this RecordatorioDeFlujoDto r) => r.Modo switch
    {
        "Empresa" => null,
        "Apagado" => 0,
        "CadaDias" => DefinicionDeFlujo.ValidarRecordatorio(r.CadaDias ?? 0) is { } d and > 0 ? d : throw ReglaDeNegocioException.Validacion("dias-invalidos", "El recordatorio automático sale cada 1 a 60 días."),
        _ => throw ReglaDeNegocioException.Validacion("modo-invalido", "El recordatorio del flujo es Empresa, Apagado o CadaDias.")
    };

    public static PlazoCargueDto ADto(this PlazoDeCargue p) => new(p.Dias, [.. p.Recordatorios]);

    public static PlazoDeCargue ADominio(this PlazoCargueDto p) => new(p.Dias, p.Recordatorios ?? (p.Dias > 0 ? PlazoDeCargue.PorDefecto.Recordatorios.Where(r => r < p.Dias).ToList() : []));

    public static CondicionDto ADto(this CondicionDeExigencia c) =>
        new(c.Tipo.ToString(), c.Nodos?.Select(ADto).ToList(), c.Atributo, c.Operador?.ToString(), c.Valores);

    /// <summary>Condición del cuerpo a dominio; un tipo u operador desconocido responde 422 <c>condicion-invalida</c>.</summary>
    public static CondicionDeExigencia ADominio(this CondicionDto c) =>
        new(Enum.TryParse<TipoNodoCondicion>(c.Tipo, true, out var tipo) && Enum.IsDefined(tipo)
                ? tipo
                : throw ReglaDeNegocioException.Validacion("condicion-invalida", $"«{c.Tipo}» no es un tipo de condición (Y, O, No o Predicado)."),
            c.Nodos?.Select(ADominio).ToList(), c.Atributo,
            c.Operador is null ? null
            : Enum.TryParse<OperadorCondicion>(c.Operador, true, out var operador) && Enum.IsDefined(operador) ? operador
            : throw ReglaDeNegocioException.Validacion("condicion-invalida", $"«{c.Operador}» no es un operador de condición."),
            c.Valores);

    /// <summary>
    /// Requisito del cuerpo a dominio: el tipo por id o por código (del tenant). Un código desconocido queda con id vacío y
    /// el dominio responde 422 <c>requisito-invalido</c>.
    /// </summary>
    public static RequisitoDelFlujo ADominio(this RequisitoDelFlujoDto r, IReadOnlyCollection<TipoDocumentoTenant> tipos)
    {
        if (!string.IsNullOrWhiteSpace(r.Seccion))
        {
            // Sección de la ficha (F2): el dominio valida el código, los periodos y el resto (422 seccion-invalida…).
            if (r.TipoDocumentoTenantId is not null || !string.IsNullOrWhiteSpace(r.TipoDocumento))
                throw ReglaDeNegocioException.Validacion("requisito-invalido", "Un requisito pide un documento o una sección de la ficha, no ambos.");
            if (r.UsarComoSoporte is not null)
                throw ReglaDeNegocioException.Validacion("soporte-no-aplica", "«Usar como soporte» es de los requisitos de documento, no de las secciones.");
            var seccion = r.Seccion.Trim().ToUpperInvariant();
            return new RequisitoDelFlujo(r.Id ?? Guid.Empty, Guid.Empty, seccion, r.Obligatorio, [.. r.AreasQueRevisan ?? []], r.AprobadoresRequeridos,
                r.Condicion?.ADominio(), seccion, r.Periodos, r.ExigeSoporte, VigenciaDias: r.VigenciaDias, DiasRevalidacion: r.DiasRevalidacion);
        }

        if (r.Periodos is not null || r.ExigeSoporte)
            throw ReglaDeNegocioException.Validacion("requisito-invalido", "Periodos y soporte exigido solo aplican a un requisito de sección.");
        var codigo = (r.TipoDocumento ?? string.Empty).Trim().ToUpperInvariant();
        var tipoId = r.TipoDocumentoTenantId ?? tipos.FirstOrDefault(t => t.Codigo == codigo)?.Id ?? Guid.Empty;
        if (tipoId == Guid.Empty && codigo.Length == 0)
            throw ReglaDeNegocioException.Validacion("requisito-invalido", "Cada requisito necesita el tipo de documento (id o código).");
        return new RequisitoDelFlujo(r.Id ?? Guid.Empty, tipoId, codigo, r.Obligatorio, [.. r.AreasQueRevisan ?? []], r.AprobadoresRequeridos,
            r.Condicion?.ADominio(), UsarComoSoporte: r.UsarComoSoporte, VigenciaDias: r.VigenciaDias, DiasRevalidacion: r.DiasRevalidacion);
    }

    public static FlujoDto ADto(this Flujo f, TipoVinculo? tipo, bool esPorDefecto) =>
        new(f.Id, f.EmpresaId, f.Codigo, f.Nombre,
            new TipoVinculoResumenDto(f.TipoVinculoId, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty),
            esPorDefecto, f.VersionPublicada?.ADto(), f.Borrador?.ADto(), f.CreadoEn, f.ActualizadoEn);

    public static VeredictoEmitidoDto ADto(this VeredictoRegistrado v) =>
        new(v.Id, v.Veredicto.ToString(), v.Origen.ToString(), v.RevisorId, v.Fecha, [.. v.Motivos.Select(m => m.Codigo)], v.Observacion,
            v.Justificacion, v.Vigente, v.Superado, v.Anulado, v.AnuladoPor, v.SobreVersion, v.Invalidado, v.Derivado,
            v.Campos.Count > 0 ? [.. v.Campos] : null, v.Revertido);

    public static DesaprobacionDto ADto(this DesaprobacionDeExpediente d) =>
        new(d.Etapa, d.ExpedienteAprobado ? "ExpedienteAprobado" : d.FaseInicial ? "FaseInicial" : "Etapa", d.AdministradorId, d.Justificacion, d.Fecha);

    /// <summary>
    /// <paramref name="miembros"/>: miembros actuales de cada área (para listar quién falta por pronunciarse).
    /// <paramref name="consultante"/>: usuario que consulta (si es una persona), para calcular lo que «Aprobar área» le
    /// aprobaría (<see cref="AreaExpedienteDto.DocumentosPorDecidir"/>).
    /// </summary>
    public static ExpedienteDto ADto(this Expediente e, IReadOnlyDictionary<Guid, List<Guid>> miembros, string? consultante = null,
        DocumentacionDelExpediente? documentacion = null) =>
        new(e.Id, e.Folio, e.EmpresaId, e.VinculoId,
            new TerceroResumenDto(e.TerceroId, e.TerceroNombre, e.TerceroIdentificacion),
            new TipoVinculoResumenDto(e.TipoVinculoId, e.TipoVinculoCodigo, e.TipoVinculoNombre),
            e.Tipo.ToString(), e.Estado.ToString(),
            e.Flujo is null ? null : new FlujoDeExpedienteDto(e.Flujo.FlujoId, e.Flujo.FlujoVersionId, e.Flujo.Codigo, e.Flujo.Nombre, e.Flujo.Version,
                e.Flujo.Origen.ToString()),
            e.PoliticaDevolucion.ToString(), e.SeparacionDeFunciones, e.EtapaActiva?.Orden,
            [.. e.Etapas.Select(et => new EtapaExpedienteDto(et.Orden, et.Estado.ToString(), et.ActivadaEn, et.AprobadaEn,
                [.. et.Areas.Select(a => a.ADto(miembros.TryGetValue(a.AreaId, out var m) ? m : [], PorDecidir(e, et, a, consultante)))]))],
            e.Reenvios, e.UltimaRespuesta,
            e.Rechazo is null ? null : new RechazoDto(e.Rechazo.AreaId, e.Rechazo.RevisorId, [.. e.Rechazo.Motivos.Select(m => m.Codigo)],
                e.Rechazo.Justificacion, e.Rechazo.Fecha, e.Rechazo.Origen),
            e.MotivoAnulacion, e.AbiertoEn, e.CerradoEn, e.ActualizadoEn, e.Version,
            [.. e.Requisitos.Select(r => r.ADto(e, documentacion))],
            [.. e.Verificaciones.Select(v => v.ADto(e.AdjuntosDeCumplimiento))],
            // «Falta cumplimiento» solo si hay una verificación sin satisfacer: si lo único que falta es la cuenta designada, lo dice EsperandoCuentaDesignada (CB-4).
            e.EsperandoVerificaciones && !e.EsperandoSoloLaCuentaDesignada,
            e.PlazoDto(),
            e.Desaprobaciones,
            e.UltimaDesaprobacion?.ADto(),
            e.UltimoRecordatorioDeObservaciones is { } ultimo ? new RecordatorioObservacionesDto(ultimo, e.RecordatoriosDeObservaciones) : null,
            [.. e.AdjuntosDeCumplimiento.Select(a => a.ADto())],
            e.EsperandoCuentaDesignada);

    /// <summary>Plazo de cargue del expediente inicial (LT-2); nulo si no tiene (otros tipos, o un flujo sin plazo).</summary>
    public static PlazoCargueExpedienteDto? PlazoDto(this Expediente e) =>
        e.PlazoCargue is not { Activo: true } p
            ? null
            : new(p.Dias, [.. p.Recordatorios], e.PlazoCargueHasta, [.. e.RecordatoriosEnviados.Order()], e.CaducadoEn, e.EstadoAlCaducar?.ToString(),
                e.Reaperturas);

    /// <summary>Requisito con la decisión de cada área que lo revisa (estado, aprobaciones y veredictos de esa área).</summary>
    private static RequisitoExpedienteDto ADto(this RequisitoDeExpediente r, Expediente e, DocumentacionDelExpediente? documentacion) =>
        new(r.Id, r.Tipo(documentacion), r.Obligatorio, r.Estado.ToString(), r.Version(e, documentacion), r.Reenvios,
            r.Eximido ? new ExencionDto(r.EximidoPor ?? string.Empty, r.JustificacionExencion ?? string.Empty, r.EximidoEn ?? e.ActualizadoEn, r.ExencionDeVinculoId) : null,
            [
                .. r.AreasQueRevisan.Select(id => (Id: id, Area: e.BuscarArea(id))).Where(x => x.Area is not null).Select(x =>
                    new AreaDeRequisitoDto(x.Id, x.Area!.Codigo, EstadoEnArea(r, x.Area, e), r.AprobadoresPara(x.Area), r.AprobacionesDe(x.Area),
                        [.. r.Veredictos.Where(v => v.AreaId == x.Id).Select(ADto)]))
            ], r.TipoRequisito.ToString(), r.DetalleDeSeccion(documentacion), documentacion?.Cruces?.DeDocumento(r.Id),
            r.Retiro is { } retiro ? new RetiroDeVersionDto(retiro.Numero, retiro.Motivo, retiro.Fecha) : null, r.RevisionesDto(e));

    /// <summary>Lo que la revisión automática registró del documento para su versión vigente (nulo si no registró nada, ADR-0012).</summary>
    private static IReadOnlyList<RevisionAutomaticaDto>? RevisionesDto(this RequisitoDeExpediente r, Expediente e)
    {
        var vigentes = r.Revisiones.Where(v => r.VersionActual?.Numero == v.SobreVersion || v.Resultado == ResultadoDeRevision.AprobadaConMuestreo)
            .OrderBy(v => v.Fecha).ToList();
        return vigentes.Count == 0
            ? null
            : [.. vigentes.Select(v => new RevisionAutomaticaDto(v.AreaId, e.BuscarArea(v.AreaId)?.Codigo ?? string.Empty, v.SobreVersion, v.Nivel.ToString(),
                v.Resultado.ToString(), v.Sugerencia?.ToString(), [.. v.Motivos.Select(m => new MotivoDeRevisionDto(m.Codigo, m.Texto))], v.Confianza, v.Fecha,
                e.RevisionAbierta(v, r), v.Resolucion?.Decision.ToString(), v.Resolucion?.Por, v.Resolucion?.Fecha, v.Resolucion?.Justificacion))];
    }

    /// <summary>Detalle de la sección (F2): con la documentación, datos, soportes y cambios; sin ella, la completitud guardada.</summary>
    private static SeccionDeRequisitoDto? DetalleDeSeccion(this RequisitoDeExpediente r, DocumentacionDelExpediente? documentacion) =>
        !r.EsSeccion ? null : documentacion?.Seccion(r) ?? DocumentacionDelExpediente.SeccionSinDatos(r);

    private static string EstadoEnArea(RequisitoDeExpediente r, AreaDeExpediente area, Expediente e) =>
        r.Eximido ? "Eximido"
        : r.ObservadoPor(area.AreaId) ? "Observado"
        : r.AprobadoPor(area) ? "Aprobado"
        : e.Etapas.First(et => et.Areas.Contains(area)).Estado == EstadoEtapa.Esperando || e.Estado == EstadoExpediente.Abierto ? "Esperando"
        : "Pendiente";

    private static TipoDocumentoRequisitoDto Tipo(this RequisitoDeExpediente r, DocumentacionDelExpediente? documentacion) =>
        r.EsSeccion
            ? new(r.TipoDocumentoTenantId, r.Codigo, r.Nombre, r.TipoCanonico, r.Ambito.ToString(), "Formulario")
            : documentacion?.Tipo(r) ?? new(r.TipoDocumentoTenantId, r.Codigo, r.Nombre, r.TipoCanonico, r.Ambito.ToString());

    /// <summary>Versión que cubre el requisito; con la documentación, además sus datos declarados y su vigencia para el vínculo (TD-4, TD-6).</summary>
    private static VersionRequisitoDto? Version(this RequisitoDeExpediente r, Expediente e, DocumentacionDelExpediente? documentacion) =>
        r.VersionActual is not { } v
            ? null
            : documentacion?.Version(v, r, e.VinculoId) ??
              new VersionRequisitoDto(v.DocumentoId, v.Numero, v.Sha256, v.CargadaEn, Clase: r.EsSeccion ? "Formulario" : null);

    /// <summary>Solo en revisión, en la etapa activa y en áreas no automáticas: lo que «Aprobar área» aprobaría (DA-3).</summary>
    private static IReadOnlyList<DocumentoPorDecidirDto> PorDecidir(Expediente e, EtapaDeExpediente etapa, AreaDeExpediente area, string? consultante) =>
        e.Estado != EstadoExpediente.EnRevision || etapa.Estado != EstadoEtapa.Activa || area.Automatica
            ? []
            :
            [
                .. e.DocumentosPorDecidir(area.AreaId, consultante).Select(d => e.BuscarRequisito(d.RequisitoId) is { EsSeccion: true } s
                    ? new DocumentoPorDecidirDto(d.RequisitoId, d.VersionVigente, nameof(TipoDeRequisito.Seccion), s.Seccion, s.Periodo)
                    : new DocumentoPorDecidirDto(d.RequisitoId, d.VersionVigente))
            ];

    private static AreaExpedienteDto ADto(this AreaDeExpediente a, IReadOnlyList<Guid> miembros, IReadOnlyList<DocumentoPorDecidirDto> porDecidir)
    {
        var yaSePronunciaron = a.RevisoresVigentes;
        IReadOnlyList<Guid> pendientes = a.Estado == EstadoAreaExpediente.Pendiente
            ? [.. miembros.Where(m => !yaSePronunciaron.Contains(m.ToString()))]
            : [];
        return new AreaExpedienteDto(a.AreaId, a.Codigo, a.Nombre, a.Estado.ToString(), a.AprobadoresRequeridos, a.Aprobaciones,
            Math.Max(0, a.AprobadoresRequeridos - a.Aprobaciones), pendientes, a.PuedeRechazarExpediente, a.Automatica, a.PendienteDesde,
            a.SlaVence, [.. a.Veredictos.Select(ADto)], a.RequisitosPorDecidir, porDecidir);
    }

    /// <summary>
    /// La fila de la bandeja. <paramref name="ahora"/> y <paramref name="porcentajeAviso"/> dicen cómo va su plazo (SLA, OP-12): <c>EnTiempo</c>,
    /// <c>PorVencer</c> o <c>Vencido</c>; nulo si el área no tiene plazo corriendo.
    /// </summary>
    public static FilaBandejaDto ADto(this FilaBandeja f, DateTimeOffset? ahora = null, int porcentajeAviso = Expediente.PorcentajeDeAvisoPorOmision) =>
        new(f.ExpedienteId, f.Folio, f.VinculoId, new TerceroResumenDto(f.TerceroId, f.TerceroNombre, f.TerceroIdentificacion),
            new TipoVinculoResumenDto(f.TipoVinculoId, f.TipoVinculoCodigo, f.TipoVinculoNombre), f.AreaId, f.AreaCodigo, f.AreaNombre,
            f.Etapa, f.EstadoArea.ToString(), f.EstadoExpediente.ToString(), f.Aprobaciones, f.AprobadoresRequeridos, f.Desde, f.ActualizadoEn,
            f.RequisitosPendientes, f.Desaprobaciones > 0, f.SlaHoras, f.SlaVence, EstadoSla(f, ahora, porcentajeAviso));

    private static string? EstadoSla(FilaBandeja f, DateTimeOffset? ahora, int porcentajeAviso)
    {
        if (ahora is not { } hoy || f.EstadoArea != EstadoAreaExpediente.Pendiente || f.EstadoExpediente != EstadoExpediente.EnRevision ||
            f.SlaVence is not { } vence || f.Desde is not { } desde)
            return null;
        return (hoy >= vence ? EstadoDeSla.Vencido : hoy >= Expediente.UmbralDeAviso(desde, vence, porcentajeAviso) ? EstadoDeSla.PorVencer : EstadoDeSla.EnTiempo).ToString();
    }

    /// <summary>
    /// Vista del tercero: estado, observaciones vigentes con el texto del motivo (y el documento observado) y los
    /// requisitos que le piden con su estado y sus observaciones; nunca áreas, revisores ni veredictos internos.
    /// </summary>
    public static ExpedientePublicoDto APublico(this Expediente e, Empresa? empresa, DocumentacionDelExpediente? documentacion = null) =>
        new(e.Id, e.Folio, empresa.APublica(e.EmpresaId), new TipoVinculoResumenDto(e.TipoVinculoId, e.TipoVinculoCodigo, e.TipoVinculoNombre),
            e.Estado.ToString(),
            [.. e.ObservacionesVigentes.Select(v => v.APublica(v.RequisitoId is { } id ? e.BuscarRequisito(id)?.Codigo : null))],
            e.Estado == EstadoExpediente.Observado, e.Reenvios, e.ActualizadoEn,
            [
                // Una observación solo llega al tercero cuando el expediente se le devuelve (con AlCerrarArea puede esperar).
                .. e.Requisitos.Select(r => new RequisitoPublicoDto(r.Id, r.Tipo(documentacion), r.Obligatorio,
                    (r.Estado == EstadoRequisito.Observado && e.Estado != EstadoExpediente.Observado ? EstadoRequisito.EnRevision : r.Estado).ToString(),
                    r.Version(e, documentacion),
                    e.Estado == EstadoExpediente.Observado
                        ? [.. r.Veredictos.Where(v => v.Vigente && v.Veredicto == Veredicto.Observado).OrderBy(v => v.Fecha).Select(v => v.APublica(r.Codigo))]
                        : [],
                    PuedeRatificar(e, r), r.Ratificado ? r.Ratificacion!.Fecha : null, r.TipoRequisito.ToString(), r.DetalleDeSeccion(documentacion)))
            ],
            e.Requisitos.Count(r => r.Obligatorio && r.Estado == EstadoRequisito.Pendiente), e.Tipo.ToString(), e.PlazoCargueHasta, e.EsperandoCuentaDesignada);

    /// <summary>
    /// ¿Puede el tercero ratificar este requisito (RN-2, I-14)? En una renovación abierta, sobre un documento global sin
    /// versión en ella cuyo tipo canónico lo permite (RUT sí; Cámara y estados financieros, no), o sobre una sección de la ficha
    /// que la renovación periódica del vínculo le pide ratificar (VV-1: la empresa ya la había aceptado y aún no hay versión nueva).
    /// </summary>
    private static bool PuedeRatificar(Expediente e, RequisitoDeExpediente r) =>
        e.EstaAbierto && e.Tipo == TipoExpediente.Renovacion && !r.Eximido && r.VersionActual is null &&
        (r.EsSeccion
            ? r.ExigeRatificacion && r.VersionAceptadaPrevia is not null
            : r.Ambito == AmbitoDocumento.Global && Dominio.Documentos.CatalogoCanonico.Buscar(r.TipoCanonico)?.PermiteRatificacion == true);

    internal static ObservacionPublicaDto APublica(this VeredictoRegistrado v, string? requisito) =>
        new([.. v.Motivos.Select(m => new MotivoPublicoDto(m.Codigo, m.TextoAlTercero))], v.Observacion, v.Fecha, requisito,
            v.Campos.Count > 0 ? [.. v.Campos] : null);
}
