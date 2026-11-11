using System.Text.Json;

namespace Validasoft.Contratos;

// DTOs de la ficha maestra del tercero (docs/14, ADR-0013, FM-17…). JSON en camelCase; los datos de cada sección van
// en snake_case dentro de `datos` (esquemas de docs/14 §4).

// ---------- Catálogos de plataforma ----------

/// <summary>
/// Entrada de un catálogo de plataforma (<c>ciudades</c>, <c>actividades-economicas</c>, <c>bancos</c>,
/// <c>responsabilidades-dian</c>, <c>especialidades</c>). <see cref="Padre"/>: departamento (ciudades) o nivel anterior
/// (especialidades); <see cref="Nivel"/> solo en especialidades (1 grupo, 2 categoría, 3 especialidad). En bancos, <see cref="Codigo"/>
/// es el código de compensación local (CENIT en Colombia), <see cref="Pais"/> el de ese código y <see cref="Swift"/> el SWIFT/BIC.
/// </summary>
public sealed record EntradaCatalogoDto(
    string Codigo, string Nombre, string? Padre = null, int? Nivel = null, string? Departamento = null, string? Pais = null, bool Activo = true,
    string? Swift = null);

/// <summary>Fila de la importación: <see cref="Departamento"/> y <see cref="Pais"/> (ISO, CO por omisión) en ciudades; <see cref="Nivel"/> y <see cref="Padre"/> en especialidades.</summary>
public sealed record FilaCatalogoDto(
    string? Codigo, string? Nombre, string? Departamento = null, string? Pais = null, string? Padre = null, int? Nivel = null, bool? Activo = null,
    string? Swift = null);

public sealed record ImportarCatalogoDto(IReadOnlyList<FilaCatalogoDto>? Filas);

/// <summary>Fila rechazada: posición (base 0) en la lista enviada, código y motivo.</summary>
public sealed record FilaRechazadaDto(int Fila, string? Codigo, string Motivo);

/// <summary>Resultado de la importación (upsert por código, idempotente): una segunda importación igual deja todo en <c>sinCambios</c>.</summary>
public sealed record ResultadoImportacionCatalogoDto(
    string Catalogo, int Creados, int Actualizados, int SinCambios, IReadOnlyList<FilaRechazadaDto> Rechazados);

// ---------- Secciones de la ficha ----------

/// <summary>Faltante de completitud: <see cref="Campo"/> (ruta snake_case, p. ej. <c>personas[0].pep</c>) o <see cref="Regla"/>, con el motivo legible.</summary>
public sealed record FaltanteSeccionDto(string? Campo, string? Regla, string Mensaje);

/// <summary>Completitud guardada con la versión (FM-16) y la versión de reglas con que se calculó.</summary>
public sealed record CompletitudSeccionDto(bool Completa, IReadOnlyList<FaltanteSeccionDto> Faltantes, int ReglaVersion);

/// <summary>Versión de un documento de la bóveda del tercero que respalda la sección (FM-5). <see cref="TipoDocumento"/> lo pone el servidor.</summary>
public sealed record SoporteSeccionDto(Guid DocumentoId, int Version, string? TipoDocumento = null);

/// <summary>
/// Versión vigente de una sección (o vacía: <see cref="Version"/> y <see cref="Datos"/> nulos). <see cref="Datos"/> es el
/// JSON normalizado en snake_case; <see cref="Huella"/> el SHA-256 de datos + soportes. En Tercero 360 (F2),
/// <see cref="Aceptaciones"/> dice, por cada vínculo de la empresa activa, qué versión aceptó, si hay una nueva pendiente y
/// el estado de su revalidación.
/// </summary>
public sealed record SeccionFichaDto(
    string Seccion,
    string Nombre,
    int? Periodo,
    int? Version,
    JsonElement? Datos,
    IReadOnlyList<SoporteSeccionDto> Soportes,
    CompletitudSeccionDto? Completitud,
    string? Huella,
    string? Origen,
    DateTimeOffset? Fecha,
    int Versiones,
    int EsquemaVersion,
    IReadOnlyList<AceptacionDeSeccionDto>? Aceptaciones = null);

/// <summary>
/// Lo que un vínculo de la empresa activa tiene aceptado de la sección (F2, FM-29): versión, fecha y expediente en que se aceptó
/// (nulos si nunca), si la versión vigente del tercero es otra (<see cref="VersionNuevaPendiente"/>) y la revalidación abierta
/// que la revisa, si la hay.
/// </summary>
public sealed record AceptacionDeSeccionDto(
    Guid VinculoId, int? VersionAceptada, DateTimeOffset? AceptadaEn, Guid? ExpedienteId, string? Folio, bool VersionNuevaPendiente,
    RevalidacionDeSeccionDto? Revalidacion, string? Origen = null);

/// <summary>
/// Revalidación (expediente de actualización abierto) de la sección: su folio, el estado del requisito
/// (<c>Pendiente|Cargado|EnRevision|Observado|Aprobado</c>), la versión en revisión y el plazo antes de que bloquee (RV-4).
/// </summary>
public sealed record RevalidacionDeSeccionDto(Guid ExpedienteId, string Folio, string Estado, int? Version, DateTimeOffset PlazoHasta);

/// <summary>Datos de la sección (objeto en snake_case; null = vacía) y los soportes de esta versión (se envían completos cada vez).</summary>
public sealed record GuardarSeccionDto(JsonElement? Datos, IReadOnlyList<SoporteSeccionDto>? Soportes = null);

/// <summary>
/// Resultado del guardado: <see cref="Creada"/> es falso si los datos y soportes eran los de la versión vigente (no nace
/// versión). <see cref="SiguienteSeccion"/>: la misma si quedó incompleta; si no, la siguiente incompleta del wizard (nula si
/// la ficha está completa).
/// </summary>
public sealed record SeccionGuardadaDto(int Version, bool Creada, CompletitudSeccionDto Completitud, string? SiguienteSeccion, SeccionFichaDto Seccion);

/// <summary>Estado de una sección en la proyección <c>EstadoDeFicha</c>; sin versión = sin diligenciar.</summary>
public sealed record EstadoSeccionFichaDto(
    string Seccion, string Nombre, int? Periodo, bool Obligatoria, int? Version, bool Completa, IReadOnlyList<FaltanteSeccionDto> Faltantes,
    int? ReglaVersion, DateTimeOffset? Actualizada);

/// <summary>Ficha del tercero para el wizard: orden por tipo de persona, la siguiente sección incompleta y el estado de cada una.</summary>
public sealed record EstadoFichaDto(
    Guid TerceroId, string TipoPersona, IReadOnlyList<string> Orden, string? SiguienteSeccion, bool Completa,
    IReadOnlyList<EstadoSeccionFichaDto> Secciones);

/// <summary>Versión vigente de reglas y esquema de cada sección.</summary>
public sealed record ReglaSeccionDto(string Seccion, int ReglaVersion, int EsquemaVersion);

/// <summary>Resultado del recálculo de completitud: secciones revisadas, las que cambiaron y terceros cuyo estado se reconstruyó.</summary>
public sealed record RecalculoFichasDto(int Secciones, int Recalculadas, int Terceros, IReadOnlyList<ReglaSeccionDto> Reglas);

// ---------- Lo aceptado, normalizado para el ERP (F3, FM-9, FM-32…) ----------

/// <summary>Código de un catálogo de plataforma con su nombre (nulo si el código ya no está en el catálogo).</summary>
public sealed record CodigoNombreDto(string Codigo, string? Nombre);

/// <summary>Municipio DANE: código de 5 dígitos, nombre, departamento (los dos primeros dígitos y su nombre) y país.</summary>
public sealed record CiudadErpDto(string Codigo, string? Nombre, string CodigoDepartamento, string? Departamento, string? Pais);

/// <summary>Dirección en el formato estructurado colombiano (tipo de vía, número, cuadrante, placa, complemento).</summary>
public sealed record DireccionEstructuradaDto(
    string? TipoVia, string? NumeroVia, string? LetraVia, bool? Bis, string? Cuadrante, string? NumeroCruce, string? LetraCruce, string? Placa,
    string? Complemento);

/// <summary>
/// El representante legal principal que recibe el ERP (FM-34): en persona natural, el propio tercero (<see cref="Origen"/> = <c>FICHA_GENERAL</c>);
/// en jurídica, el principal de menor orden de la sección legal (o el primero), con su posición en <see cref="Indice"/>.
/// <see cref="Nombre"/> = nombres y apellidos.
/// </summary>
public sealed record RepresentanteLegalErpDto(
    string Origen, int? Indice, string Nombre, string? Nombres, string? Apellidos, string? TipoIdentificacion, string? Numero, string? Dv,
    string? TipoRepresentacion, string? Cargo, DateOnly? FechaExpedicion, string? Correo, string? Telefono, string? LugarExpedicion = null);

/// <summary><c>FICHA_GENERAL</c> aceptada. <see cref="Nombre"/> = razón social o nombres y apellidos; el representante solo en persona natural.</summary>
public sealed record ContenidoGeneralDto(
    string? TipoPersona, string? TipoIdentificacion, string? NumeroIdentificacion, string? Dv, string? Nombre, string? RazonSocial, string? Nombres,
    string? Apellidos, string? Direccion, DireccionEstructuradaDto? DireccionEstructurada, CiudadErpDto? Ciudad, string? Pais, string? Telefono,
    string? Correo, CodigoNombreDto? CiiuPrincipal, IReadOnlyList<CodigoNombreDto> CiiuSecundarias, string? PaginaWeb, bool? CertificadoIso,
    int? AnioInicio, RepresentanteLegalErpDto? RepresentanteLegal);

/// <summary>
/// Calidades tributarias sí/no de un tercero en Colombia (FM-33), derivadas de los códigos DIAN y de lo declarado en la sección
/// tributaria (autorretenedor de ICA, exento de retención CREE; IO-4). <c>null</c> = no se puede derivar ni se declaró (ver
/// <c>calidadesSinDerivar</c>): el ERP conserva lo que ya tenga. Cada ERP las mapea a sus propios campos.
/// </summary>
public sealed record CalidadesTributariasDto(
    bool? ResponsableIva, bool? Autorretenedor, bool? DeclaranteRenta, bool? GranContribuyente, bool? AutorretenedorIca, bool? RegimenSimple,
    bool? GranContribuyenteBogota, bool? RegimenEspecial, bool? AutorretenedorRft, bool? DescuentoSenaFic, bool? ProfesionalIndependiente,
    bool? ExentoRetencion, bool? AgenteRetenedorIva, bool? ObligadoAFacturar, bool? ExentoReteCree = null);

/// <summary>Calidad que va como <c>null</c> y por qué (no está en la casilla 53 ni en la ficha).</summary>
public sealed record CalidadSinDerivarDto(string Calidad, string Nota);

public sealed record ResolucionDto(string? Numero, DateOnly? Fecha);

/// <summary>Municipio donde declara ICA, su actividad (CIIU) y la tarifa por mil, si la hay.</summary>
public sealed record IcaErpDto(CiudadErpDto? Municipio, CodigoNombreDto? Ciiu, decimal? TarifaPorMil);

/// <summary><c>FICHA_TRIBUTARIA</c> aceptada: responsabilidades DIAN (código y nombre), las calidades derivadas, resoluciones e ICA.</summary>
public sealed record ContenidoTributarioDto(
    IReadOnlyList<CodigoNombreDto> Responsabilidades, CalidadesTributariasDto Calidades, IReadOnlyList<CalidadSinDerivarDto> CalidadesSinDerivar,
    ResolucionDto? ResolucionGranContribuyente, ResolucionDto? ResolucionAutorretenedor, bool? ResponsableIca, bool? AutorretenedorIca,
    IReadOnlyList<IcaErpDto> Ica);

/// <summary>Persona de la sección legal (FM-7, fase 1). <see cref="Rol"/>: RepresentanteLegal, Suplente, Apoderado, Socio, JuntaDirectiva, RevisorFiscal, Contador.</summary>
public sealed record PersonaRelacionadaDto(
    string? Rol, string? TipoRepresentacion, string? TipoIdentificacion, string? Numero, string? Dv, string? Nombres, string? Apellidos, string? Cargo,
    decimal? Porcentaje, bool? Pep, bool? VinculoPep, DateOnly? FechaExpedicion, DateOnly? FechaNacimiento, string? Nacionalidad, string? Residencia,
    string? Correo, string? Telefono, string? LugarExpedicion = null);

public sealed record FirmaRevisoraDto(string? Nit, string? RazonSocial);

/// <summary><c>FICHA_LEGAL</c> aceptada: el representante legal principal y la lista completa de personas.</summary>
public sealed record ContenidoLegalDto(RepresentanteLegalErpDto? RepresentanteLegal, IReadOnlyList<PersonaRelacionadaDto> Personas,
    FirmaRevisoraDto? RevisorFiscalFirma);

public sealed record MonedaExtranjeraDto(bool? Realiza, IReadOnlyList<string> Tipos, IReadOnlyList<string> Paises);

/// <summary><c>FICHA_FINANCIERA</c> aceptada de un periodo.</summary>
public sealed record ContenidoFinancieroDto(
    int? Periodo, DateOnly? FechaCorte, decimal? Activos, decimal? Pasivos, decimal? Patrimonio, decimal? IngresosBrutos, decimal? IngresosNetos,
    decimal? GastosOperacionales, decimal? GastosNoOperacionales, decimal? OtrosIngresos, MonedaExtranjeraDto? MonedaExtranjera);

/// <summary><c>FICHA_SST</c> aceptada: las ocho preguntas sí/no.</summary>
public sealed record ContenidoSstDto(
    bool? ProgramaSaludOcupacional, bool? IdentificaFactoresDeRiesgo, bool? ComiteParitario, bool? ProgramaHigieneYSeguridad, bool? GestionAmbiental,
    bool? ReglamentoInterno, bool? PoliticaHseq, bool? LicenciasYPermisos);

/// <summary><c>FICHA_ESPECIALIDADES</c> aceptada: códigos de nivel 3 con su nombre.</summary>
public sealed record ContenidoEspecialidadesDto(IReadOnlyList<CodigoNombreDto> Especialidades);

/// <summary>
/// Contenido aceptado de una sección, normalizado para el ERP (FM-32): solo viene la propiedad de la sección (las demás, nulas).
/// Es el mismo en el evento <c>seccion.aceptada</c> y en <c>GET /api/v1/vinculos/{id}/ficha</c>.
/// </summary>
public sealed record ContenidoDeSeccionDto(
    ContenidoGeneralDto? General = null, ContenidoTributarioDto? Tributaria = null, ContenidoLegalDto? Legal = null,
    ContenidoFinancieroDto? Financiera = null, ContenidoSstDto? Sst = null, ContenidoEspecialidadesDto? Especialidades = null);

/// <summary>
/// Lo que la empresa tiene aceptado de una sección en el vínculo (FM-35): versión, fecha, expediente, huella y el contenido
/// normalizado; <see cref="VersionVigente"/> es la versión vigente del tercero y <see cref="VersionNuevaPendiente"/> dice si es
/// otra (aún no aceptada).
/// </summary>
public sealed record SeccionAceptadaDto(
    string Seccion, string Nombre, int? Periodo, int Version, DateTimeOffset AceptadaEn, Guid ExpedienteId, string Folio, string Huella,
    ContenidoDeSeccionDto Contenido, int? VersionVigente, bool VersionNuevaPendiente, string? Origen = null);

/// <summary>
/// La ficha aceptada por la empresa en un vínculo (FM-35), para que cualquier ERP cree o actualice al tercero: cada sección aceptada,
/// el representante legal principal (de la General y la Legal aceptadas), la cuenta designada aprobada (número completo solo con
/// <c>cuentas:aprobar</c>, CB-2), los contactos del tercero con la empresa por rol (IO-3) y las claves del tercero en los sistemas del
/// tenant (<see cref="ReferenciasDelTercero"/>, IO-2; <see cref="Referencias"/> son las del vínculo).
/// </summary>
public sealed record FichaDelVinculoDto(
    Guid VinculoId, Guid EmpresaId, Guid TerceroId, IReadOnlyList<ReferenciaExternaDto> Referencias, string TipoPersona,
    IReadOnlyList<SeccionAceptadaDto> Secciones, RepresentanteLegalErpDto? RepresentanteLegal, CuentaDesignadaDto? CuentaDesignada,
    IReadOnlyList<ContactoDeEmpresaDto>? Contactos = null, IReadOnlyList<ReferenciaExternaDto>? ReferenciasDelTercero = null);

// ---------- Cruce sección ↔ documento y actualización desde el documento (F5, FM-48…) ----------

/// <summary>
/// Un campo del cruce: ruta snake_case de la sección, etiqueta, lo declarado y lo que dice el documento (texto), el resultado
/// (<c>Coincide|Discrepa|NoVerificable</c>), si es crítico y una nota para el revisor.
/// </summary>
public sealed record CampoCruzadoDto(string Campo, string Etiqueta, string? Declarado, string? Extraido, string Resultado, bool Critico, string? Detalle);

/// <summary>
/// Cruce de la versión de la sección con una versión de documento (FM-8): el documento, la confianza del análisis de IA del
/// documento, los conteos y los campos. <see cref="DiscrepanciaCritica"/>: algún campo crítico discrepa.
/// </summary>
public sealed record CruceDeSeccionDto(
    Guid DocumentoId, int VersionDocumento, string TipoDocumento, string NombreDocumento, decimal Confianza, bool Legible, DateTimeOffset Fecha,
    int Coincidencias, int Discrepancias, int NoVerificables, bool DiscrepanciaCritica, IReadOnlyList<CampoCruzadoDto> Campos);

/// <summary>
/// Documento que respalda la sección pero sin cruce para la empresa: <see cref="Motivo"/> <c>sin-analisis</c> (la empresa no
/// recibió el análisis de IA de esa versión: sin plan, sin cupo o el análisis falló) ⇒ se revisa a mano.
/// </summary>
public sealed record DocumentoSinCruceDto(Guid DocumentoId, int Version, string TipoDocumento, string NombreDocumento, string Motivo);

/// <summary>
/// Cruce del requisito de sección para la empresa (F5): <see cref="Estado"/> <c>Cruzado</c> (hay al menos un cruce que la empresa
/// puede ver), <c>Manual</c> (hay documentos que la respaldan, ninguno con análisis para la empresa) o <c>SinDocumento</c>.
/// </summary>
public sealed record CruceDeRequisitoDto(string Estado, bool DiscrepanciaCritica, IReadOnlyList<CruceDeSeccionDto> Cruces, IReadOnlyList<DocumentoSinCruceDto> SinCruce);

/// <summary>Sección que respalda un documento del expediente, con su versión vigente y el cruce con la versión del documento (si la empresa lo ve).</summary>
public sealed record SeccionRespaldadaDto(string Seccion, string Nombre, int? Periodo, int? Version, CruceDeSeccionDto? Cruce);

/// <summary>
/// «Usar como soporte de las secciones» en un requisito de documento (FM-13): las secciones que respalda la versión que lo
/// cubre, con su cruce. <see cref="SinAnalisis"/>: la empresa no recibió el análisis de esa versión (revisión manual).
/// </summary>
public sealed record SoporteDeSeccionesDto(bool SinAnalisis, bool DiscrepanciaCritica, IReadOnlyList<SeccionRespaldadaDto> Secciones);

/// <summary>Versión de documento de la bóveda del tercero de la que sale una propuesta.</summary>
public sealed record DocumentoDePropuestaDto(Guid DocumentoId, int Version, string TipoDocumento, string NombreDocumento, DateTimeOffset CargadaEn);

/// <summary>Un cambio propuesto: campo (ruta snake_case), etiqueta, valor actual y el del documento.</summary>
public sealed record CampoPropuestoDto(string Campo, string Etiqueta, string? Actual, string? Propuesto);

/// <summary>
/// Actualización desde el documento (FM-14): lo que la versión actual de un documento del tercero propone para una sección.
/// <see cref="Disponible"/> falso con <see cref="Motivo"/> <c>sin-lectura-automatica</c> si el documento no tiene lectura
/// automática (se diligencia con el documento a la vista). <see cref="Datos"/>: los de la sección con los cambios aplicados,
/// para prellenar el formulario (nunca se guardan solos); solo en la consulta de una sección.
/// </summary>
public sealed record PropuestaDeSeccionDto(
    string Seccion, string NombreSeccion, int? Periodo, DocumentoDePropuestaDto Documento, bool Disponible, string? Motivo,
    IReadOnlyList<CampoPropuestoDto> Campos, JsonElement? Datos = null);

