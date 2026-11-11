using System.Text.Json;

namespace Validasoft.Contratos;

// DTOs de motivos, flujos aprobatorios, expedientes y bandeja (docs/05 §2.2, §2.4 y §4). Enums como texto.

// ---------- Motivos (docs/03 §13) ----------

/// <summary><see cref="Categoria"/>: Documento|Formulario|Contacto|Otro. <see cref="AplicaA"/>: Observacion|Rechazo|Ambos.</summary>
public sealed record CrearMotivoDto(string Codigo, string Nombre, string Categoria, string AplicaA, string TextoAlTercero);

public sealed record ActualizarMotivoDto(string Nombre, string Categoria, string AplicaA, string TextoAlTercero, bool? Activo = null);

/// <summary>
/// El <c>MotivoDto</c> de docs/05 §4 (aquí con otro nombre porque <see cref="MotivoDto"/> ya es el cuerpo de anular o
/// desvincular). <see cref="Base"/>: motivo de la plataforma (no editable).
/// </summary>
public sealed record MotivoTipificadoDto(
    Guid Id, string Codigo, string Nombre, string Categoria, string AplicaA, string TextoAlTercero, bool Base, bool Activo);

// ---------- Flujos ----------

/// <summary>Un área en una etapa. <see cref="Automatica"/>: aprueba sola al activarse la etapa.</summary>
public sealed record AreaDeEtapaDto(
    Guid AreaId, int AprobadoresRequeridos = 1, bool PuedeRechazarExpediente = false, bool Automatica = false, int? SlaHoras = null);

/// <summary>Las áreas de una etapa revisan en paralelo; las etapas van en secuencia por <see cref="Orden"/>.</summary>
public sealed record EtapaDto(int Orden, IReadOnlyList<AreaDeEtapaDto> Areas);

/// <summary>
/// Documento que pide el flujo (RQ-1). Al escribir, el tipo va por <see cref="TipoDocumentoTenantId"/> o por su código
/// (<see cref="TipoDocumento"/>); <see cref="Id"/> opcional (se conserva entre versiones si se reenvía).
/// <see cref="AreasQueRevisan"/> vacío o nulo ⇒ todas las áreas del flujo; <see cref="AprobadoresRequeridos"/> nulo ⇒ los
/// del área. Al leer vienen los tres identificadores. <see cref="Condicion"/> nula ⇒ se pide siempre (RQ-12).
/// En vez de un documento, el requisito puede pedir una <see cref="Seccion"/> de la ficha maestra (F2): <c>FICHA_GENERAL</c>,
/// <c>FICHA_TRIBUTARIA</c>, <c>FICHA_FINANCIERA</c>, <c>FICHA_LEGAL</c>, <c>FICHA_SST</c> o <c>FICHA_ESPECIALIDADES</c>
/// (sin tipo de documento), con <see cref="Periodos"/> (solo la financiera: cuántos cierres, 1 por omisión) y
/// <see cref="ExigeSoporte"/> (la versión debe traer soporte para estar completa para la empresa). Las áreas que revisan son
/// el «responsable por sector» del SRM. A la persona natural no se le piden Legal ni Financiera.
/// <see cref="UsarComoSoporte"/> (F5, FM-13): en un documento que respalda secciones (RUT, Cámara, estados financieros, cédula),
/// <c>false</c> lo deja de cruzar con ellas; nulo = por omisión (activo).
/// </summary>
public sealed record RequisitoDelFlujoDto(
    Guid? Id = null, Guid? TipoDocumentoTenantId = null, string? TipoDocumento = null, bool Obligatorio = true,
    IReadOnlyList<Guid>? AreasQueRevisan = null, int? AprobadoresRequeridos = null, CondicionDto? Condicion = null, string? Seccion = null,
    int? Periodos = null, bool ExigeSoporte = false, bool? UsarComoSoporte = null);

/// <summary>
/// «Se pide cuando» (docs/03 §12, RQ-12). <see cref="Tipo"/>: <c>Y</c>|<c>O</c>|<c>No</c> combinan <see cref="Nodos"/>
/// (<c>No</c>, exactamente uno); <c>Predicado</c> compara <see cref="Atributo"/> (<c>tercero.tipoPersona</c>,
/// <c>tercero.pais</c>, <c>tercero.tipoIdentificacion</c>, <c>tercero.esExtranjero</c>, <c>vinculo.tipo</c> o
/// <c>vinculo.atributos.{clave}</c>) con <see cref="Valores"/> según <see cref="Operador"/>: <c>Igual</c>|<c>Distinto</c>|
/// <c>En</c>|<c>NoEn</c>|<c>ContieneAlguno</c>|<c>ContieneTodos</c>|<c>Existe</c>|<c>Mayor</c>|<c>Menor</c>|<c>Entre</c>.
/// Valores como texto invariante: números con punto, fechas <c>yyyy-MM-dd</c>, booleanos <c>true</c>/<c>false</c>.
/// </summary>
public sealed record CondicionDto(
    string Tipo, IReadOnlyList<CondicionDto>? Nodos = null, string? Atributo = null, string? Operador = null, IReadOnlyList<string>? Valores = null);

/// <summary>
/// Sin <see cref="Etapas"/>: una sola etapa con todas las áreas activas de la empresa en paralelo (P-4-4).
/// <see cref="PoliticaDevolucion"/>: <c>Inmediata</c> (por defecto) o <c>AlCerrarArea</c>. Sin <see cref="Requisitos"/>
/// cada área decide sobre el expediente completo (FL-1).
/// </summary>
public sealed record CrearFlujoDto(
    string Codigo, string Nombre, string TipoVinculo, IReadOnlyList<EtapaDto>? Etapas = null, string? PoliticaDevolucion = null,
    bool SeparacionDeFunciones = false, IReadOnlyList<RequisitoDelFlujoDto>? Requisitos = null,
    IReadOnlyList<VerificacionRequeridaDto>? Verificaciones = null, PlazoCargueDto? PlazoCargue = null, ReintentoDto? Reintento = null);

/// <summary>
/// Edita el borrador (o crea uno nuevo si solo hay versión publicada). Lo que llega nulo conserva el valor de la última
/// versión; <c>requisitos: []</c> los quita y <c>verificaciones: []</c> también.
/// </summary>
public sealed record EditarFlujoDto(
    string? Nombre = null, IReadOnlyList<EtapaDto>? Etapas = null, string? PoliticaDevolucion = null, bool? SeparacionDeFunciones = null,
    IReadOnlyList<RequisitoDelFlujoDto>? Requisitos = null, IReadOnlyList<VerificacionRequeridaDto>? Verificaciones = null,
    PlazoCargueDto? PlazoCargue = null, ReintentoDto? Reintento = null);

public sealed record FlujoVersionDto(
    Guid Id, int Version, string Estado, IReadOnlyList<EtapaDto> Etapas, string PoliticaDevolucion, bool SeparacionDeFunciones,
    DateTimeOffset CreadaEn, DateTimeOffset? PublicadaEn, DateTimeOffset? RetiradaEn, IReadOnlyList<RequisitoDelFlujoDto>? Requisitos = null,
    IReadOnlyList<VerificacionRequeridaDto>? Verificaciones = null, PlazoCargueDto? PlazoCargue = null, ReintentoDto? Reintento = null);

/// <summary>
/// Reintento del tercero tras un rechazo (DS-4): con <see cref="Permitido"/> el tercero rechazado ve los motivos en su portal y
/// puede «Volver a postularse» pasados <see cref="DiasEspera"/> días desde el rechazo (0 = de inmediato). Por defecto no se permite.
/// </summary>
public sealed record ReintentoDto(bool Permitido, int DiasEspera = 0);

/// <summary>Flujo con su versión publicada y su borrador (si existen) y si es el flujo por defecto de su tipo de vínculo.</summary>
public sealed record FlujoDto(
    Guid Id, Guid EmpresaId, string Codigo, string Nombre, TipoVinculoResumenDto TipoVinculo, bool EsPorDefecto,
    FlujoVersionDto? Publicada, FlujoVersionDto? Borrador, DateTimeOffset CreadoEn, DateTimeOffset ActualizadoEn);

/// <summary>La empresa sale de <c>X-Empresa</c>; si <see cref="EmpresaId"/> viene, debe coincidir.</summary>
public sealed record FlujoPorDefectoDto(Guid TipoVinculoId, Guid FlujoId, Guid? EmpresaId = null);

// ---------- Expedientes ----------

/// <summary><see cref="FlujoId"/> opcional: sin él, el flujo del vínculo o el por defecto de <c>(empresa, tipo)</c>.</summary>
public sealed record AbrirExpedienteDto(Guid? FlujoId = null);

public sealed record FlujoDeExpedienteDto(Guid Id, Guid VersionId, string Codigo, string Nombre, int Version, string Origen);

public sealed record TerceroResumenDto(Guid Id, string Nombre, string Identificacion);

/// <summary>
/// Veredicto de un área. <see cref="Usuario"/>: id del revisor (o del administrador); nulo si lo emitió el sistema.
/// <see cref="Superado"/>: dejó de contar porque el tercero reenvió tras la observación (o se eximió el requisito).
/// En un requisito: <see cref="SobreVersion"/> (número de la versión decidida) e <see cref="Invalidado"/> (llegó una
/// versión nueva). <see cref="Derivado"/>: aprobación del área que el sistema dedujo de sus requisitos. <see cref="Campos"/>:
/// campos de la sección señalados al observarla (F2). <see cref="Revertido"/>: un administrador devolvió a revisión la etapa del
/// área (reapertura, fase inicial o desaprobación, DS-1): ya no cuenta.
/// </summary>
public sealed record VeredictoEmitidoDto(
    Guid Id, string Veredicto, string Origen, string? Usuario, DateTimeOffset Fecha, IReadOnlyList<string> Motivos, string? Observacion,
    string? Justificacion, bool Vigente, bool Superado, bool Anulado, string? AnuladoPor, int? SobreVersion = null, bool Invalidado = false,
    bool Derivado = false, IReadOnlyList<string>? Campos = null, bool Revertido = false);

/// <summary>
/// Documento (o sección de la ficha, F2) que «Aprobar área» decidiría y la versión vigente que debe acompañarlo en
/// <c>versiones</c>. <see cref="TipoRequisito"/>: <c>Documento|Seccion</c>; en una sección, su código y periodo.
/// </summary>
public sealed record DocumentoPorDecidirDto(Guid RequisitoId, int VersionVigente, string TipoRequisito = "Documento", string? Seccion = null,
    int? Periodo = null);

/// <summary>
/// Área del expediente con sus aprobaciones (<see cref="Aprobaciones"/> de <see cref="AprobadoresRequeridos"/>) y los
/// miembros que aún no se pronuncian (<see cref="MiembrosPendientes"/>, vacío si el área ya se pronunció). Con requisitos,
/// <see cref="RequisitosPorDecidir"/> cuenta los de su alcance que aún esperan su decisión y
/// <see cref="DocumentosPorDecidir"/> lista lo que «Aprobar área» aprobaría a quien consulta (si es una persona; si no,
/// lo que el área aún no decidió), con la versión vigente de cada uno; vacío fuera de revisión o sin requisitos.
/// </summary>
public sealed record AreaExpedienteDto(
    Guid AreaId, string Codigo, string Nombre, string Estado, int AprobadoresRequeridos, int Aprobaciones, int AprobacionesFaltantes,
    IReadOnlyList<Guid> MiembrosPendientes, bool PuedeRechazarExpediente, bool Automatica, DateTimeOffset? PendienteDesde,
    DateTimeOffset? SlaVence, IReadOnlyList<VeredictoEmitidoDto> Veredictos, int RequisitosPorDecidir = 0,
    IReadOnlyList<DocumentoPorDecidirDto>? DocumentosPorDecidir = null);

/// <summary>
/// Tipo de documento de un requisito: id y código del tipo del tenant, canónico (para cargarlo) y ámbito. Con TD-1…TD-5,
/// además lo que el tercero necesita para entregarlo: <see cref="Modo"/> (<c>Archivo|Formulario|ArchivoYFormulario</c>),
/// <see cref="Campos"/>, límites de archivo (<see cref="Archivo"/>), si hay plantilla para descargar y el texto de ayuda.
/// Nulos si el tipo ya no existe.
/// </summary>
public sealed record TipoDocumentoRequisitoDto(
    Guid Id, string Codigo, string Nombre, string? TipoCanonico, string Ambito, string? Modo = null, IReadOnlyList<CampoFormularioDto>? Campos = null,
    ReglasArchivoDto? Archivo = null, bool TienePlantilla = false, string? TextoAyuda = null);

/// <summary>
/// Versión de la bóveda que cubre el requisito (compartida con el vínculo), con lo que declaró el tercero
/// (<see cref="DatosDeclarados"/>), su vigencia para esta empresa (TD-6) y su estado en la bóveda (<see cref="Estado"/>).
/// </summary>
public sealed record VersionRequisitoDto(
    Guid DocumentoId, int N, string Sha256, DateTimeOffset CargadaEn, IReadOnlyList<DatoDeclaradoDto>? DatosDeclarados = null,
    VigenciaDto? Vigencia = null, string? Estado = null, string? Clase = null);

/// <summary>Decisión de un área sobre el requisito: <see cref="Estado"/> <c>Esperando|Pendiente|Aprobado|Observado|Eximido</c>.</summary>
public sealed record AreaDeRequisitoDto(
    Guid AreaId, string Codigo, string Estado, int AprobadoresRequeridos, int Aprobaciones, IReadOnlyList<VeredictoEmitidoDto> Veredictos);

public sealed record ExencionDto(string Por, string Justificacion, DateTimeOffset Fecha);

/// <summary>
/// Requisito del expediente (<c>RequisitoDto</c> de docs/05 §4, reducido). <see cref="Estado"/>:
/// <c>Pendiente|Cargado|EnRevision|Observado|Aprobado|Eximido</c>. <see cref="TipoRequisito"/>: <c>Documento|Seccion</c>; en una
/// sección de la ficha (F2) <see cref="TipoDocumento"/> lleva el código y nombre de la sección (modo <c>Formulario</c>),
/// <see cref="VersionActual"/> la versión de la sección (<c>documentoId</c> = id de la sección, <c>sha256</c> = huella) y
/// <see cref="Seccion"/> el detalle: completitud para la empresa, datos, soportes y cambios frente a lo aceptado.
/// </summary>
public sealed record RequisitoExpedienteDto(
    Guid Id, TipoDocumentoRequisitoDto TipoDocumento, bool Obligatorio, string Estado, VersionRequisitoDto? VersionActual, int Reenvios,
    ExencionDto? Exencion, IReadOnlyList<AreaDeRequisitoDto> Areas, string TipoRequisito = "Documento", SeccionDeRequisitoDto? Seccion = null,
    SoporteDeSeccionesDto? SoporteDeSecciones = null);

/// <summary>
/// Cambio de un campo o fila entre la versión que la empresa aceptó y la nueva (las Actualizaciones del SRM, F2):
/// <see cref="Campo"/> ruta snake_case (<c>direccion</c>, <c>personas[1].porcentaje</c>, <c>ica[0]</c>), etiqueta legible,
/// valores como texto (nulos si no había o ya no hay) y <see cref="TipoCambio"/> <c>Agregado|Quitado|Modificado</c>.
/// </summary>
public sealed record CambioDeCampoDto(string Campo, string Etiqueta, string? Anterior, string? Nuevo, string TipoCambio);

/// <summary>
/// Detalle del requisito de sección (F2). <see cref="Completitud"/>: completitud <b>para la empresa</b> de la última versión
/// evaluada (<see cref="VersionEvaluada"/>), guardada en el expediente, con el motivo legible de cada faltante (p. ej. «Falta el
/// soporte de la información general»); nula si el tercero aún no aceptó la invitación. <see cref="Datos"/> y
/// <see cref="Soportes"/> son los de la versión que cubre el requisito. <see cref="VersionAceptada"/>: la que la empresa tenía
/// aceptada en el vínculo; si la versión actual es otra, <see cref="Cambios"/> trae solo lo que cambió (anterior → nuevo).
/// </summary>
public sealed record SeccionDeRequisitoDto(
    string Seccion, string Nombre, int? Periodo, bool ExigeSoporte, CompletitudSeccionDto? Completitud, int? VersionEvaluada, JsonElement? Datos,
    IReadOnlyList<SoporteSeccionDto> Soportes, int? VersionAceptada, IReadOnlyList<CambioDeCampoDto> Cambios, CruceDeRequisitoDto? Cruce = null);

public sealed record EtapaExpedienteDto(int Orden, string Estado, DateTimeOffset? ActivadaEn, DateTimeOffset? AprobadaEn,
    IReadOnlyList<AreaExpedienteDto> Areas);

public sealed record RechazoDto(Guid AreaId, string Usuario, IReadOnlyList<string> Motivos, string Justificacion, DateTimeOffset Fecha);

public sealed record ExpedienteDto(
    Guid Id,
    string Folio,
    Guid EmpresaId,
    Guid VinculoId,
    TerceroResumenDto Tercero,
    TipoVinculoResumenDto TipoVinculo,
    string Tipo,
    string Estado,
    FlujoDeExpedienteDto? Flujo,
    string PoliticaDevolucion,
    bool SeparacionDeFunciones,
    int? EtapaActual,
    IReadOnlyList<EtapaExpedienteDto> Etapas,
    int Reenvios,
    string? UltimaRespuesta,
    RechazoDto? Rechazo,
    string? MotivoAnulacion,
    DateTimeOffset AbiertoEn,
    DateTimeOffset? CerradoEn,
    DateTimeOffset ActualizadoEn,
    long Version,
    IReadOnlyList<RequisitoExpedienteDto>? Requisitos = null,
    IReadOnlyList<VerificacionDto>? Verificaciones = null,
    bool EsperandoVerificaciones = false,
    PlazoCargueExpedienteDto? PlazoCargue = null,
    int Desaprobaciones = 0,
    DesaprobacionDto? UltimaDesaprobacion = null,
    RecordatorioObservacionesDto? RecordatorioObservaciones = null);

/// <summary>
/// Última vez que un administrador devolvió a revisión lo ya decidido del expediente (DS-1, DS-2). <see cref="Alcance"/>:
/// <c>Etapa</c> (reabrió una etapa aprobada), <c>FaseInicial</c> (repite todo el flujo) o <c>ExpedienteAprobado</c> (deshizo la
/// aprobación del expediente). <see cref="Etapa"/>: la etapa desde la que se revisa de nuevo.
/// </summary>
public sealed record DesaprobacionDto(int Etapa, string Alcance, string Usuario, string Justificacion, DateTimeOffset Fecha);

/// <summary>
/// Desaprueba un expediente ya aprobado (DS-2): vuelve a revisión desde <see cref="Etapa"/> (por omisión la última etapa, la que
/// lo cerró; la primera equivale a volver a la fase inicial). La justificación es obligatoria y queda en el historial.
/// </summary>
public sealed record DesaprobarExpedienteDto(string Justificacion, int? Etapa = null);

/// <summary>
/// <see cref="Veredicto"/>: <c>Aprobado</c> u <c>Observado</c>. El área (y el requisito) van en la ruta.
/// <list type="bullet">
/// <item>Flujo sin requisitos (FL-1): <see cref="Motivos"/> (al observar, al menos uno) y <see cref="Observacion"/>.</item>
/// <item>Veredicto de un requisito: además <see cref="SobreVersion"/> (número de la versión que se revisó).</item>
/// <item>Decisión de área con requisitos (DA-1): <c>Aprobado</c> lleva <see cref="Versiones"/> (requisitoId → versión que
/// vio el revisor de cada documento que decide); <c>Observado</c> lleva <see cref="Requisitos"/> (los documentos
/// marcados). <see cref="Motivos"/> y <see cref="Observacion"/> de primer nivel no se usan.</item>
/// <item>Al observar una sección de la ficha (F2), <see cref="Campos"/> (u <c>ObservacionDeRequisitoDto.Campos</c>) señala,
/// opcional, los campos que el tercero debe corregir (rutas snake_case).</item>
/// <item>Al aprobar un requisito cuyo cruce con el documento discrepa en un campo crítico (F5, FM-13), <see cref="Justificacion"/>
/// es obligatoria (422 <c>justificacion-por-discrepancias</c>).</item>
/// </list>
/// </summary>
public sealed record VeredictoDto(string Veredicto, IReadOnlyList<string>? Motivos = null, string? Observacion = null, int? SobreVersion = null,
    IReadOnlyDictionary<Guid, int>? Versiones = null, IReadOnlyList<ObservacionDeRequisitoDto>? Requisitos = null,
    IReadOnlyList<string>? Campos = null, string? Justificacion = null);

/// <summary>
/// Documento (o sección) marcado al observar el área: la versión que se revisó, al menos un motivo, la observación y, en una
/// sección, los campos señalados.
/// </summary>
public sealed record ObservacionDeRequisitoDto(Guid RequisitoId, int? SobreVersion, IReadOnlyList<string>? Motivos, string? Observacion = null,
    IReadOnlyList<string>? Campos = null);

/// <summary>Rechazo terminal: solo un área con <c>puedeRechazarExpediente</c>; motivos y justificación obligatorios.</summary>
public sealed record RechazarExpedienteDto(Guid AreaId, IReadOnlyList<string> Motivos, string Justificacion);

public sealed record ReenviarExpedienteDto(string? Respuesta = null);

/// <summary>Sin <see cref="VeredictoId"/> se anula el veredicto vigente más reciente del área.</summary>
public sealed record AnularVeredictoDto(string Justificacion, Guid? VeredictoId = null);

/// <summary>
/// Fila de la bandeja <c>BandejaRevision</c>: un expediente × un área. <see cref="RequisitosPendientes"/>: requisitos del
/// alcance del área que aún esperan su decisión (0 sin requisitos). <see cref="Desaprobado"/>: un administrador lo devolvió a
/// revisión alguna vez (DS-1, DS-2).
/// </summary>
public sealed record FilaBandejaDto(
    Guid ExpedienteId, string Folio, Guid VinculoId, TerceroResumenDto Tercero, TipoVinculoResumenDto TipoVinculo, Guid AreaId,
    string AreaCodigo, string AreaNombre, int Etapa, string EstadoArea, string EstadoExpediente, int Aprobaciones, int AprobadoresRequeridos,
    DateTimeOffset? Desde, DateTimeOffset ActualizadoEn, int RequisitosPendientes = 0, bool Desaprobado = false);

/// <summary>Número y fecha de carga de una versión de la bóveda.</summary>
public sealed record VersionResumenDto(int Numero, DateTimeOffset Fecha);

/// <summary>
/// Fila de la bandeja de revalidaciones (RV-5, docs/05 §4 <c>RevalidacionDto</c>): un documento de un expediente de
/// actualización abierto que la empresa aún no aprueba. <see cref="VersionAceptada"/> es la que aprobó en su último
/// expediente aprobado; <see cref="Comparacion"/> el hecho de la bóveda sobre la versión actual (sin IA solo
/// <c>Distinta</c>); <see cref="AprobableEnLote"/> si es <c>Identica</c> o <c>SinCambios</c> (A-18).
/// <see cref="Bloqueante"/>: venció el plazo y los pendientes del certificado ya bloquean (RV-4).
/// </summary>
public sealed record RevalidacionDto(
    Guid ExpedienteId, string Folio, Guid VinculoId, Guid RequisitoId, TerceroResumenDto Tercero, TipoVinculoResumenDto TipoVinculo,
    string TipoDocumentoCodigo, string TipoDocumento, VersionResumenDto? VersionAceptada, VersionResumenDto VersionActual, string Comparacion,
    bool AprobableEnLote, string EstadoRequisito, IReadOnlyList<Guid> Areas, DateTimeOffset Desde, DateTimeOffset PlazoHasta, int DiasRestantes,
    bool Bloqueante);

/// <summary>Una revalidación a aprobar en lote, sobre la versión que el revisor vio.</summary>
public sealed record RevalidacionAAprobarDto(Guid ExpedienteId, Guid RequisitoId, int SobreVersion);

/// <summary>Aprobación en lote (A-18): hasta 100 revalidaciones sin cambios; cada una queda registrada por separado.</summary>
public sealed record AprobarRevalidacionesDto(IReadOnlyList<RevalidacionAAprobarDto> Revalidaciones);

public sealed record FalloDeLoteDto(Guid ExpedienteId, Guid RequisitoId, string Codigo, string Detalle);

public sealed record ResultadoDeLoteDto(int Aprobadas, IReadOnlyList<FalloDeLoteDto> Fallidas);

// ---------- Público: el tercero ----------

public sealed record MotivoPublicoDto(string Codigo, string Texto);

/// <summary>
/// <see cref="Requisito"/>: código del documento (o sección) observado (nulo si la observación es sobre el expediente);
/// <see cref="Campos"/>: campos de la sección que hay que corregir (F2).
/// </summary>
public sealed record ObservacionPublicaDto(IReadOnlyList<MotivoPublicoDto> Motivos, string? Observacion, DateTimeOffset Fecha, string? Requisito = null,
    IReadOnlyList<string>? Campos = null);

/// <summary>
/// Requisito visto por el tercero, sin áreas ni revisores: si falta (<c>Pendiente</c>), si está observado y con qué
/// motivo (<see cref="Observaciones"/> vigentes), y la versión que lo cubre. En una sección de la ficha (F2,
/// <see cref="TipoRequisito"/> <c>Seccion</c>) <see cref="Seccion"/> dice qué le falta para la empresa (se corrige en el wizard).
/// </summary>
public sealed record RequisitoPublicoDto(
    Guid Id, TipoDocumentoRequisitoDto TipoDocumento, bool Obligatorio, string Estado, VersionRequisitoDto? VersionActual,
    IReadOnlyList<ObservacionPublicaDto> Observaciones, bool PuedeRatificar = false, DateTimeOffset? RatificadoEn = null,
    string TipoRequisito = "Documento", SeccionDeRequisitoDto? Seccion = null);

/// <summary>
/// Lo que el tercero ve de su expediente: estado, observaciones vigentes y los documentos que le piden
/// (<see cref="Requisitos"/>, <see cref="RequisitosFaltantes"/> obligatorios sin cargar), sin áreas ni revisores.
/// </summary>
public sealed record ExpedientePublicoDto(
    Guid Id, string Folio, EmpresaPublicaDto Empresa, TipoVinculoResumenDto TipoVinculo, string Estado,
    IReadOnlyList<ObservacionPublicaDto> Observaciones, bool PuedeReenviar, int Reenvios, DateTimeOffset ActualizadoEn,
    IReadOnlyList<RequisitoPublicoDto>? Requisitos = null, int RequisitosFaltantes = 0, string Tipo = "Inicial", DateTimeOffset? PlazoCargueHasta = null);

/// <summary>
/// Plazo del tercero para completar el expediente inicial (P-4-2, LT-1): <see cref="Dias"/> (0 = sin plazo; por defecto 30) y
/// los recordatorios en días antes de vencer (por defecto 15, 7 y 1; cada uno menor que <see cref="Dias"/>, hasta 5).
/// </summary>
public sealed record PlazoCargueDto(int Dias, IReadOnlyList<int>? Recordatorios = null);

/// <summary>
/// Plazo de cargue del expediente (LT-2): <see cref="Hasta"/> mientras está en manos del tercero (<c>Abierto</c> u
/// <c>Observado</c>) o la fecha en que venció si caducó; los
/// recordatorios ya enviados; <see cref="CaducadoEn"/> y el estado al que vuelve si se reabre.
/// </summary>
public sealed record PlazoCargueExpedienteDto(int Dias, IReadOnlyList<int> Recordatorios, DateTimeOffset? Hasta,
    IReadOnlyList<int> RecordatoriosEnviados, DateTimeOffset? CaducadoEn, string? EstadoAlReabrir, int Reaperturas);

/// <summary>Reabre un expediente caducado con un plazo nuevo (P-4-2); la justificación es opcional y queda en el historial.</summary>
public sealed record ReabrirExpedienteDto(string? Justificacion = null);

/// <summary>
/// Corre ya el job de plazos de cargue (Plataforma). <see cref="TenantId"/> lo limita a un tenant; <see cref="Fecha"/> simula
/// «ahora» (solo en Development, para pruebas de punta a punta).
/// </summary>
public sealed record RevisarPlazosDto(Guid? TenantId = null, DateTimeOffset? Fecha = null);

public sealed record ResultadoRevisionPlazosDto(int Revisados, int Recordatorios, int Caducados);
