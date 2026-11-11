using System.Text.Json;

namespace Validasoft.Contratos;

// DTOs del importador genérico de la ficha (adaptadores de origen) y del sincronizador de coexistencia (F4, FM-39…, docs/08 §6.6).

/// <summary>
/// Unidad del sistema de origen (en cada sistema, algo como «la cuenta de cada empresa») ⇒ empresa de Validasoft que acepta lo que
/// esa unidad tenía aprobado. El adaptador dice cuáles son sus unidades.
/// </summary>
public sealed record UnidadEmpresaDto(string Unidad, Guid EmpresaId);

/// <summary>
/// Corrida de importación de un sistema de origen (el adaptador va en la ruta). <see cref="Fuente"/>: <c>Archivo</c> (el JSON va en
/// <see cref="Archivo"/>, con la forma que defina el adaptador) o <c>Conectado</c> (el origen configurado del adaptador: base de datos
/// o API). <see cref="DryRun"/>: no escribe nada y reporta lo que haría. <see cref="Unidades"/>: si viene, las secciones de los
/// terceros aprobados en esa unidad quedan aceptadas por la empresa. <see cref="TamanoLote"/>: terceros por lote (1–500, 100 por
/// omisión); tras cada lote se guarda el cursor. <see cref="ImportarCatalogos"/>: los catálogos del origen (ciudades, CIIU, bancos y
/// especialidades) al catálogo de plataforma (por omisión, sí).
/// </summary>
public sealed record ImportarTercerosDto(
    bool? DryRun = null, string? Fuente = null, JsonElement? Archivo = null, IReadOnlyList<UnidadEmpresaDto>? Unidades = null,
    int? TamanoLote = null, bool? ImportarCatalogos = null);

/// <summary>
/// Algo que pasó con un tercero o una sección: <c>Rechazo</c> (un dato que no se pudo convertir y quedó fuera; va al CSV) o
/// <c>Advertencia</c> (una corrección aplicada: DV recalculado, calidad sin código DIAN, persona duplicada…).
/// <see cref="Clave"/>: la clave del tercero en el sistema de origen; <see cref="ClaveLegado"/>: <c>{sistema}:{clave}</c>.
/// </summary>
public sealed record AvisoImportacionDto(
    string ClaveLegado, string Clave, string? Seccion, int? Periodo, string Campo, string Codigo, string Mensaje, string? Dato, string? AccionSugerida);

/// <summary>Versiones de una sección: creadas (versión 1 o la siguiente si cambió la huella), sin cambios (misma huella) y rechazadas.</summary>
public sealed record ResumenSeccionImportadaDto(string Seccion, int Creadas, int SinCambios, int Rechazadas);

/// <summary>
/// Resumen acumulado de la corrida. En un dry-run, «creados» y «creadas» es lo que se crearía. <see cref="Aceptaciones"/>:
/// secciones aceptadas por importación; <see cref="AceptacionesExistentes"/>: ya tenían aceptación (no se tocan);
/// <see cref="SinVinculo"/>: terceros aprobados en una unidad mapeada sin vínculo en la empresa (se aceptan al volver a
/// correr cuando exista). Los detalles traen hasta 500 avisos; el CSV, todos los rechazos.
/// </summary>
public sealed record ResumenImportacionDto(
    int TercerosLeidos, int TercerosCreados, int TercerosEncontrados, int TercerosRechazados, IReadOnlyList<ResumenSeccionImportadaDto> Secciones,
    int Aceptaciones, int AceptacionesExistentes, int SinVinculo, IReadOnlyList<ResultadoImportacionCatalogoDto> Catalogos, int Advertencias,
    int Rechazos, IReadOnlyList<AvisoImportacionDto> DetalleAdvertencias, IReadOnlyList<AvisoImportacionDto> DetalleRechazos);

/// <summary>
/// Estado de la corrida: <c>Encolada</c>, <c>EnCurso</c>, <c>Completada</c> o <c>Fallida</c> (con <see cref="Error"/>; se
/// reanuda desde <see cref="Cursor"/>, la clave del último tercero procesado). <see cref="Origen"/>: código del adaptador.
/// </summary>
public sealed record CorridaImportacionDto(
    Guid Id, string Origen, string Estado, bool DryRun, string Fuente, int TamanoLote, bool ImportarCatalogos, IReadOnlyList<UnidadEmpresaDto> Unidades,
    string? Cursor, int Lotes, DateTimeOffset CreadaEn, DateTimeOffset ActualizadaEn, DateTimeOffset? TerminadaEn, string? Error,
    ResumenImportacionDto Resumen);

/// <summary>Una sección del tercero sincronizado: versión vigente, si nació una versión nueva o se rechazó.</summary>
public sealed record SeccionSincronizadaDto(string Seccion, int? Periodo, int? Version, bool Creada, bool Rechazada);

/// <summary>
/// Resultado de sincronizar un tercero del sistema de origen (coexistencia, FM-11): el tercero de Validasoft (creado o encontrado),
/// cada sección (versión nueva con origen <c>SistemaExterno</c> si cambió; sin cambios si no) y los avisos. Una versión nueva de una
/// sección ya aceptada por una empresa abre su revalidación (F2).
/// </summary>
public sealed record SincronizacionDto(
    string Origen, string Clave, string ClaveLegado, Guid? TerceroId, bool TerceroCreado, IReadOnlyList<SeccionSincronizadaDto> Secciones,
    IReadOnlyList<AvisoImportacionDto> Advertencias, IReadOnlyList<AvisoImportacionDto> Rechazos);
