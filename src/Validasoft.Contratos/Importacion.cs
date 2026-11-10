using System.Text.Json;

namespace Validasoft.Contratos;

// DTOs del importador de la ficha desde el Portal de Proveedores y del sincronizador de coexistencia (F4, FM-39…, docs/08 §6.6).

/// <summary>Constructora del Portal (<c>Constructoras.ConstId</c>) ⇒ empresa de Validasoft que acepta lo que esa constructora tenía Completada o en Actualizacion.</summary>
public sealed record ConstructoraEmpresaDto(int Constructora, Guid EmpresaId);

/// <summary>
/// Corrida de importación. <see cref="Origen"/>: <c>Archivo</c> (el JSON va en <see cref="Archivo"/>, con la forma de
/// <c>IOrigenPortal</c>) o <c>SqlServer</c> (la BD del Portal de la configuración <c>Validasoft:Importacion:Portal</c>).
/// <see cref="DryRun"/>: no escribe nada y reporta lo que haría. <see cref="Constructoras"/>: si viene, las secciones de los
/// terceros Completada (3) o Actualizacion (4) en esa constructora quedan aceptadas por la empresa. <see cref="TamanoLote"/>:
/// terceros por lote (1–500, 100 por omisión); tras cada lote se guarda el cursor. <see cref="ImportarCatalogos"/>: ciudades,
/// CIIU, bancos y especialidades del Portal al catálogo de plataforma (por omisión, sí).
/// </summary>
public sealed record ImportarPortalDto(
    bool? DryRun = null, string? Origen = null, JsonElement? Archivo = null, IReadOnlyList<ConstructoraEmpresaDto>? Constructoras = null,
    int? TamanoLote = null, bool? ImportarCatalogos = null);

/// <summary>
/// Algo que pasó con un tercero o una sección: <c>Rechazo</c> (un dato que no se pudo convertir y quedó fuera; va al CSV) o
/// <c>Advertencia</c> (una corrección aplicada: DV recalculado, calidad sin código DIAN, persona duplicada…).
/// </summary>
public sealed record AvisoImportacionDto(
    string ClaveLegado, int Terid, string? Seccion, int? Periodo, string Campo, string Codigo, string Mensaje, string? Dato, string? AccionSugerida);

/// <summary>Versiones de una sección: creadas (versión 1 o la siguiente si cambió la huella), sin cambios (misma huella) y rechazadas.</summary>
public sealed record ResumenSeccionImportadaDto(string Seccion, int Creadas, int SinCambios, int Rechazadas);

/// <summary>
/// Resumen acumulado de la corrida. En un dry-run, «creados» y «creadas» es lo que se crearía. <see cref="Aceptaciones"/>:
/// secciones aceptadas por importación; <see cref="AceptacionesExistentes"/>: ya tenían aceptación (no se tocan);
/// <see cref="SinVinculo"/>: terceros Completada en una constructora mapeada sin vínculo en la empresa (se aceptan al volver a
/// correr cuando exista). Los detalles traen hasta 500 avisos; el CSV, todos los rechazos.
/// </summary>
public sealed record ResumenImportacionPortalDto(
    int TercerosLeidos, int TercerosCreados, int TercerosEncontrados, int TercerosRechazados, IReadOnlyList<ResumenSeccionImportadaDto> Secciones,
    int Aceptaciones, int AceptacionesExistentes, int SinVinculo, IReadOnlyList<ResultadoImportacionCatalogoDto> Catalogos, int Advertencias,
    int Rechazos, IReadOnlyList<AvisoImportacionDto> DetalleAdvertencias, IReadOnlyList<AvisoImportacionDto> DetalleRechazos);

/// <summary>
/// Estado de la corrida: <c>Encolada</c>, <c>EnCurso</c>, <c>Completada</c> o <c>Fallida</c> (con <see cref="Error"/>; se
/// reanuda desde <see cref="Cursor"/>, el último <c>Terid</c> procesado).
/// </summary>
public sealed record CorridaImportacionPortalDto(
    Guid Id, string Estado, bool DryRun, string Origen, int TamanoLote, bool ImportarCatalogos, IReadOnlyList<ConstructoraEmpresaDto> Constructoras,
    int? Cursor, int Lotes, DateTimeOffset CreadaEn, DateTimeOffset ActualizadaEn, DateTimeOffset? TerminadaEn, string? Error,
    ResumenImportacionPortalDto Resumen);

/// <summary>Una sección del tercero sincronizado: versión vigente, si nació una versión nueva o se rechazó.</summary>
public sealed record SeccionSincronizadaDto(string Seccion, int? Periodo, int? Version, bool Creada, bool Rechazada);

/// <summary>
/// Resultado de sincronizar un tercero del Portal (coexistencia, FM-11): el tercero de Validasoft (creado o encontrado), cada
/// sección (versión nueva con origen <c>Portal</c> si cambió; sin cambios si no) y los avisos. Una versión nueva de una sección
/// ya aceptada por una empresa abre su revalidación (F2).
/// </summary>
public sealed record SincronizacionPortalDto(
    int Terid, string ClaveLegado, Guid? TerceroId, bool TerceroCreado, IReadOnlyList<SeccionSincronizadaDto> Secciones,
    IReadOnlyList<AvisoImportacionDto> Advertencias, IReadOnlyList<AvisoImportacionDto> Rechazos);
