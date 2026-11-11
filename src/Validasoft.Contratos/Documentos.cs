using System.Text.Json;

namespace Validasoft.Contratos;

// DTOs de la bóveda de documentos (docs/05 §2.2, §2.5, §2.9 y §4). Los enums viajan como texto; JSON en camelCase.

// ---------- Catálogo canónico y tipos del tenant ----------

/// <summary><see cref="Tipo"/>: <c>Ninguna</c>, <c>MaxDiasDesdeExpedicion</c>, <c>PeriodoMinimo</c> o <c>MaxDiasDesdeAprobacion</c>.</summary>
public sealed record ReglaFrescuraDto(string Tipo, int? Dias = null, string? Regla = null);

/// <summary>
/// Tipo canónico de la plataforma (Anexo A de docs/03). <see cref="CargaSoportada"/>: los de clase <c>Archivo</c> (un
/// formulario canónico global aún no tiene esquema). <see cref="FormatosPermitidos"/> (MIME; vacío = todos),
/// <see cref="TamanoMaximoMb"/> y <see cref="PaginasMaximas"/> (nulos = los globales) son el techo de los tipos del tenant (TD-1).
/// </summary>
public sealed record TipoDocumentoCanonicoDto(
    string Codigo, int Version, string Nombre, string Ambito, string Clase, string AplicaA, bool TieneVencimientoIntrinseco,
    bool TieneFechaExpedicion, bool EsPeriodico, string? Periodicidad, bool PermiteRatificacion, bool DePersonaRelacionada,
    IReadOnlyList<string> CamposClave, IReadOnlyList<string> CamposCriticos, string Sensibilidad, ReglaFrescuraDto? ReglaFrescuraSugerida,
    int RetencionAniosSugerida, bool CargaSoportada, IReadOnlyList<string>? FormatosPermitidos = null, int? TamanoMaximoMb = null,
    int? PaginasMaximas = null);

/// <summary>
/// Campo que llena el tercero (TD-4). <see cref="Clave"/> snake_case única; <see cref="Tipo"/>:
/// <c>Texto|Numero|Fecha|Lista|SiNo</c>; <see cref="Opciones"/> solo en <c>Lista</c>. Claves reservadas (tipo <c>Fecha</c>):
/// <c>fecha_expedicion</c> y <c>fecha_vencimiento</c>, que alimentan la vigencia.
/// </summary>
public sealed record CampoFormularioDto(
    string Clave, string Etiqueta, string Tipo, bool Obligatorio = false, IReadOnlyList<string>? Opciones = null, string? Ayuda = null);

/// <summary>Límites efectivos de archivo (lo más estricto entre global, canónico y tipo) y el texto para el tercero.</summary>
public sealed record ReglasArchivoDto(IReadOnlyList<string> Formatos, long TamanoMaximoBytes, int PaginasMaximas, string Descripcion);

/// <summary>
/// Alta de un tipo del tenant. <see cref="TipoCanonico"/> nulo ⇒ tipo propio (siempre <c>PorVinculo</c>). <see cref="Ambito"/>
/// puede restringir el del canónico, nunca ampliarlo (422 <c>ambito-no-ampliable</c>). Qué se pide a cada vínculo lo
/// deciden los requisitos del flujo (RQ-3): ya no hay <c>exigencias</c> por tipo.
/// </summary>
/// <remarks>
/// Reglas por tipo (TD-1, TD-4), todas opcionales: <see cref="FormatosPermitidos"/> (MIME o <c>pdf|jpg|png|webp</c>; vacío =
/// los del canónico o todos), <see cref="TamanoMaximoMb"/> y <see cref="PaginasMaximas"/> (nulos = los globales; nunca por
/// encima del global ni del canónico), <see cref="Modo"/> <c>Archivo|Formulario|ArchivoYFormulario</c> y
/// <see cref="Campos"/> (un tipo con campos es siempre <c>PorVinculo</c>).
/// </remarks>
public sealed record CrearTipoDocumentoDto(
    string Codigo, string? Nombre, string? TipoCanonico, string? Ambito = null, ReglaFrescuraDto? ReglaFrescura = null,
    IReadOnlyList<int>? DiasAviso = null, int? DiasGracia = null, int? DiasRevalidacion = null, string? TextoAyuda = null,
    IReadOnlyList<string>? FormatosPermitidos = null, int? TamanoMaximoMb = null, int? PaginasMaximas = null, string? Modo = null,
    IReadOnlyList<CampoFormularioDto>? Campos = null);

/// <summary>
/// Código y canónico son inmutables; los campos nulos conservan su valor. <see cref="TamanoMaximoMb"/> o
/// <see cref="PaginasMaximas"/> en 0 vuelven al límite global; <c>formatosPermitidos: []</c> vuelve a los del canónico;
/// <c>campos: []</c> los quita (solo con <c>modo: Archivo</c>).
/// </summary>
public sealed record ActualizarTipoDocumentoDto(
    string? Nombre = null, string? Ambito = null, ReglaFrescuraDto? ReglaFrescura = null, IReadOnlyList<int>? DiasAviso = null,
    int? DiasGracia = null, int? DiasRevalidacion = null, string? TextoAyuda = null, bool? Activo = null,
    IReadOnlyList<string>? FormatosPermitidos = null, int? TamanoMaximoMb = null, int? PaginasMaximas = null, string? Modo = null,
    IReadOnlyList<CampoFormularioDto>? Campos = null);

/// <summary>
/// Tipo del tenant (B-4) con sus reglas: lo configurado (<see cref="FormatosPermitidos"/>, <see cref="TamanoMaximoMb"/>,
/// <see cref="PaginasMaximas"/>, <see cref="Modo"/>, <see cref="Campos"/>), lo efectivo para el tercero
/// (<see cref="ReglasEfectivas"/>), la plantilla vigente y si el tercero puede entregarlo (<see cref="CargaSoportada"/>).
/// Frescura, avisos y gracia calculan la vigencia de cada versión (TD-6).
/// </summary>
public sealed record TipoDocumentoTenantDto(
    Guid Id, string Codigo, string Nombre, string? TipoCanonico, int? VersionCanonico, string Ambito, string Clase, string? TextoAyuda,
    ReglaFrescuraDto ReglaFrescura, IReadOnlyList<int> DiasAviso, int DiasGracia, int DiasRevalidacion, bool Activo, DateTimeOffset CreadoEn,
    IReadOnlyList<string>? FormatosPermitidos = null, int? TamanoMaximoMb = null, int? PaginasMaximas = null, string Modo = "Archivo",
    IReadOnlyList<CampoFormularioDto>? Campos = null, ReglasArchivoDto? ReglasEfectivas = null, PlantillaVersionDto? Plantilla = null,
    bool CargaSoportada = true);

// ---------- Plantilla descargable del tipo (TD-5) ----------

/// <summary>Paso 1 de la plantilla (<c>PUT /tipos-documento/{id}/plantilla</c>): PDF, DOCX o XLSX hasta el tamaño global.</summary>
public sealed record IniciarPlantillaDto(string NombreArchivo, long TamanoBytes, string TipoMime, string? Sha256 = null);

/// <summary>URL prefirmada PUT para subir la versión <see cref="Version"/> de la plantilla; luego <c>POST …/plantilla/{version}/confirmar</c>.</summary>
public sealed record CargaDePlantillaDto(Guid TipoDocumentoId, int Version, InstruccionCargaDto Carga);

public sealed record PlantillaVersionDto(
    int Version, string NombreOriginal, string Mime, long TamanoBytes, string Sha256, DateTimeOffset CargadaEn, string CargadaPor, bool Vigente,
    DateTimeOffset? RetiradaEn);

/// <summary>Plantilla vigente (nula si no hay) e historial completo, de la más reciente a la más antigua.</summary>
public sealed record PlantillaTipoDocumentoDto(Guid TipoDocumentoId, PlantillaVersionDto? Vigente, IReadOnlyList<PlantillaVersionDto> Historial);

/// <summary><see cref="Duplicado"/>: el archivo es el mismo de la vigente (no hubo versión nueva).</summary>
public sealed record ConfirmacionPlantillaDto(bool Duplicado, PlantillaVersionDto Plantilla);

// ---------- Carga en dos pasos ----------

/// <summary>
/// Paso 1 de la carga. Un tipo canónico global va a la bóveda del tercero; un tipo del tenant necesita
/// <see cref="VinculoId"/> (de qué empresa es) y, si es <c>PorVinculo</c>, solo existe para ese vínculo.
/// <see cref="Sha256"/> es opcional: si llega, detecta el duplicado antes de subir y se verifica al confirmar.
/// </summary>
/// <remarks>
/// <see cref="Datos"/>: solo para tipos <c>ArchivoYFormulario</c> (TD-4): los valores de los campos (texto, número, fecha
/// <c>aaaa-mm-dd</c>, opción o <c>true</c>/<c>false</c>), validados aquí y guardados con la versión al confirmar.
/// </remarks>
public sealed record IniciarCargaDto(
    string NombreArchivo, long TamanoBytes, string TipoMime, string? TipoCanonico = null, Guid? TipoDocumentoTenantId = null,
    Guid? VinculoId = null, string? Sha256 = null, IReadOnlyDictionary<string, JsonElement>? Datos = null);

/// <summary>
/// Versión solo de formulario (<c>POST /public/v1/boveda/formularios</c>, TD-4): el tipo del tenant (por id, o el
/// canónico configurado por la empresa) del vínculo <see cref="VinculoId"/> y los valores de sus campos.
/// </summary>
public sealed record EnviarFormularioDto(
    Guid VinculoId, IReadOnlyDictionary<string, JsonElement>? Datos, Guid? TipoDocumentoTenantId = null, string? TipoCanonico = null);

public sealed record InstruccionCargaDto(string Metodo, string Url, IReadOnlyDictionary<string, string> Cabeceras, DateTimeOffset Expira);

/// <summary><see cref="Duplicado"/> ⇒ sin <see cref="Carga"/>: el archivo ya es la versión <see cref="Version"/> del documento.</summary>
public sealed record CargaPrefirmadaDto(Guid DocumentoId, int Version, bool Duplicado, InstruccionCargaDto? Carga);

// ---------- Documentos y versiones ----------

public sealed record TipoDocumentoResumenDto(string Codigo, string Nombre, Guid? TipoDocumentoTenantId);

/// <summary>
/// Hechos de la versión (docs/02 §5.1). Con <see cref="ConIa"/> (la empresa consumió su validación de esta versión,
/// ADR-0012) traen el análisis en <see cref="Ia"/>, la comparación de campos con la versión anterior y la revisión
/// humana que pide el análisis; sin él, los básicos (hash, MIME, antivirus, comparación por hash) pendientes de revisión.
/// </summary>
public sealed record HechosDto(
    bool ConIa, bool HashVerificado, string Antivirus, string MimeReal, int? Paginas, string Comparacion, IReadOnlyList<string> CamposCambiados,
    IReadOnlyList<string> CamposCriticosCambiados, bool PendienteRevisionHumana, HechosIaDto? Ia = null);

/// <summary>
/// Lo que extrajo el análisis con IA (ADR-0012): modelo (y si se escaló), confianza de 0 a 1, legibilidad, tipo que
/// detectó (código canónico u <c>OTRO</c>), campos según el esquema del tipo, alertas de manipulación y páginas enviadas
/// y omitidas (IA-5). No incluye costo ni quién pagó.
/// </summary>
public sealed record HechosIaDto(
    string Modelo, bool Escalado, decimal Confianza, bool Legible, string TipoDetectado, IReadOnlyDictionary<string, JsonElement> Campos,
    IReadOnlyList<string> Alertas, string? Observaciones, int? PaginasEnviadas, int? PaginasOmitidas, DateTimeOffset Fecha);

/// <summary>Valor declarado por el tercero, normalizado (número invariante, fecha <c>aaaa-mm-dd</c>, <c>true</c>/<c>false</c>).</summary>
public sealed record DatoDeclaradoDto(string Clave, string Etiqueta, string Tipo, string Valor);

/// <summary>
/// Vigencia de una versión para una empresa (TD-6). <see cref="Base"/>: <c>FechaExpedicion|FechaCarga|VencimientoPropio|
/// SinVencimiento|PeriodoNoCalculado</c>. <see cref="Estado"/>: <c>Vigente|PorVencer|EnGracia|Vencida|SinVencimiento</c>
/// (hoy, en UTC). <see cref="FinDeGracia"/> = <see cref="VigenteHasta"/> + <see cref="DiasGracia"/>; al día siguiente la
/// versión queda <c>Vencida</c> para esa empresa (<see cref="VencidaEn"/>).
/// </summary>
public sealed record VigenciaDto(
    DateOnly? VigenteHasta, string Base, string Estado, IReadOnlyList<int> DiasAviso, int DiasGracia, DateOnly? FinDeGracia,
    DateTimeOffset? VencidaEn);

/// <summary>
/// <see cref="Estado"/> agrega <c>Vencida</c> (venció para todas las empresas con vigencia). <see cref="DatosDeclarados"/>:
/// lo que llenó el tercero (TD-4). <see cref="Vigencia"/>: la de la empresa que consulta o, en la bóveda del tercero, la
/// más próxima a vencer. Una versión solo de formulario tiene <see cref="Clase"/> <c>Formulario</c>, mime
/// <c>application/json</c> y no se descarga.
/// </summary>
public sealed record VersionDocumentoDto(
    Guid DocumentoId, int N, string Estado, DateTimeOffset CargadaEn, string CargadaPor, string Clase, string NombreOriginal, string Mime,
    long TamanoBytes, int? Paginas, string Sha256, string? MotivoDevolucion, string? DetalleDevolucion, HechosDto? Hechos,
    IReadOnlyList<DatoDeclaradoDto>? DatosDeclarados = null, VigenciaDto? Vigencia = null);

/// <summary>Versión compartida con una empresa a través de un vínculo.</summary>
public sealed record CompartidoDto(Guid DocumentoId, string Tipo, int Version, Guid VinculoId, Guid EmpresaId);

/// <summary>Resultado del paso 2. <see cref="Duplicado"/> ⇒ no hubo versión nueva y <see cref="Version"/> es la existente.</summary>
public sealed record ConfirmacionCargaDto(bool Duplicado, VersionDocumentoDto Version, IReadOnlyList<CompartidoDto> Compartidos);

/// <summary>
/// Estado del documento con una empresa del tercero (solo vínculos de su sesión). <see cref="Version"/> es la versión más
/// reciente que la empresa recibió y no está retirada (nula si no tiene ninguna) y <see cref="VersionActualCompartida"/> dice si es
/// la vigente del tercero. <see cref="Situacion"/> lo que hizo con ella (RT-5): <c>NoRecibe</c>, <c>SinVigente</c>,
/// <c>TieneVigente</c>, <c>AproboVigente</c>, <c>Revisando</c>, <c>Observada</c>, <c>RevalidaRetirada</c>, <c>Revalida</c>,
/// <c>PorRevalidar</c> o <c>TieneOtra</c>; <see cref="OtraVersion"/> es la segunda versión de su texto: la que aprobó y retiró o
/// cambió, la retirada sin reemplazo o la vigente que no recibe.
/// </summary>
public sealed record ComparticionPublicaDto(
    Guid VinculoId, EmpresaPublicaDto Empresa, bool Compartido, int? Version, bool VersionActualCompartida, DateTimeOffset? CompartidaEn,
    VigenciaDto? Vigencia = null, string Situacion = "NoRecibe", int? OtraVersion = null);

public sealed record DocumentoBovedaDto(
    Guid Id, TipoDocumentoResumenDto Tipo, string Ambito, string Clase, Guid? VinculoId, VersionDocumentoDto? VersionActual, int Versiones,
    IReadOnlyList<ComparticionPublicaDto> PorEmpresa);

/// <summary><c>BovedaTercero</c>: los documentos del tercero con su versión actual y, por empresa, si está compartido.</summary>
public sealed record BovedaDto(Guid TerceroId, IReadOnlyList<DocumentoBovedaDto> Documentos);

public sealed record DocumentoDto(
    Guid Id, Guid TerceroId, TipoDocumentoResumenDto Tipo, string Clase, string Ambito, Guid? VinculoId, int? VersionActual,
    IReadOnlyList<VersionDocumentoDto> Versiones, IReadOnlyList<ComparticionPublicaDto> PorEmpresa);

/// <summary>Bóveda vista por una empresa: solo las versiones compartidas con su vínculo. <see cref="VersionActual"/> nulo si la actual no le fue compartida.</summary>
public sealed record DocumentoVistaEmpresaDto(
    Guid DocumentoId, TipoDocumentoResumenDto Tipo, string Ambito, int? VersionActual, IReadOnlyList<VersionDocumentoDto> VersionesCompartidas);

public sealed record DescargaDto(string Url, DateTimeOffset Expira, string Nombre, string Mime);

public sealed record RetirarVersionDto(string Motivo);

public sealed record RetiroDto(DocumentoDto Documento, bool BinarioEliminado, IReadOnlyList<CompartidoDto> Compartidos);

// ---------- Impacto de retirar una versión (RT-1) ----------

/// <summary>
/// Una empresa a la que le cambia algo si el tercero retira la versión. <see cref="Situacion"/>: lo que la empresa hizo con ella,
/// <c>Recibio</c>, <c>Aprobo</c> o <c>Revisa</c> (nada de sus decisiones privadas). <see cref="Efecto"/>: <c>Revalidar</c> (la
/// aprobó: revisa de nuevo la que queda vigente), <c>VolverARevisar</c> (la revisa: su expediente cambia de versión y vuelve a
/// revisarla), <c>VuelveALaAnterior</c> (la recibió: vuelve a tener la anterior), <c>SinVersion</c> (no queda otra vigente) o
/// <c>SinCambios</c> (la versión retirada no era la vigente). <see cref="Folio"/> es el del expediente que revisa, si lo hay.
/// </summary>
public sealed record EmpresaAfectadaPorRetiroDto(Guid VinculoId, EmpresaPublicaDto Empresa, string Situacion, string Efecto, string? Folio = null);

/// <summary>Una sección de la información del tercero que usa la versión como soporte.</summary>
public sealed record SeccionAfectadaPorRetiroDto(string Seccion, string Nombre, int? Periodo);

/// <summary>
/// Lo que pasará si el tercero retira la versión <see cref="Numero"/> (<c>GET …/versiones/{n}/retiro/impacto</c>, solo lectura):
/// si era la vigente (<see cref="EsLaVigente"/>), cuál queda vigente (<see cref="VersionQueQuedaVigente"/>, nula si ninguna), las
/// empresas que la recibieron, aprobaron o revisan y las secciones de la ficha que la tienen de soporte.
/// </summary>
public sealed record ImpactoDeRetiroDto(
    Guid DocumentoId, int Numero, bool EsLaVigente, int? VersionQueQuedaVigente, IReadOnlyList<EmpresaAfectadaPorRetiroDto> Empresas,
    IReadOnlyList<SeccionAfectadaPorRetiroDto> Secciones);
