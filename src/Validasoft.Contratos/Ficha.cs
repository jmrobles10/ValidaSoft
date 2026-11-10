using System.Text.Json;

namespace Validasoft.Contratos;

// DTOs de la ficha maestra del tercero (docs/14, ADR-0013, FM-17…). JSON en camelCase; los datos de cada sección van
// en snake_case dentro de `datos` (esquemas de docs/14 §4).

// ---------- Catálogos de plataforma ----------

/// <summary>
/// Entrada de un catálogo de plataforma (<c>ciudades</c>, <c>actividades-economicas</c>, <c>bancos</c>,
/// <c>responsabilidades-dian</c>, <c>especialidades</c>). <see cref="Padre"/>: departamento (ciudades) o nivel anterior
/// (especialidades); <see cref="Nivel"/> solo en especialidades (1 grupo, 2 categoría, 3 especialidad).
/// </summary>
public sealed record EntradaCatalogoDto(
    string Codigo, string Nombre, string? Padre = null, int? Nivel = null, string? Departamento = null, string? Pais = null, bool Activo = true);

/// <summary>Fila de la importación: <see cref="Departamento"/> y <see cref="Pais"/> (ISO, CO por omisión) en ciudades; <see cref="Nivel"/> y <see cref="Padre"/> en especialidades.</summary>
public sealed record FilaCatalogoDto(
    string? Codigo, string? Nombre, string? Departamento = null, string? Pais = null, string? Padre = null, int? Nivel = null, bool? Activo = null);

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
/// JSON normalizado en snake_case; <see cref="Huella"/> el SHA-256 de datos + soportes.
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
    int EsquemaVersion);

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
