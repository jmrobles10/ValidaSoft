namespace Validasoft.Contratos;

// Consola de Plataforma (PT-1…PT-8): grupos con sus empresas, operadores por identidad delegada, prueba de la llave de
// OpenAI y estado de los secretos en reposo.

/// <summary>Una empresa del grupo como la ve Plataforma, con el estado de su plan de IA (sin la llave).</summary>
public sealed record EmpresaDeGrupoDto(
    Guid Id, string Nombre, IdentificacionDto Identificacion, string PrefijoFolio, bool Activa, bool PlanIaConfigurado, bool PlanIaActivo,
    bool LlaveConfigurada, string? UltimosCuatro);

/// <summary>Un grupo (tenant) con sus empresas.</summary>
public sealed record GrupoDto(TenantDto Tenant, IReadOnlyList<EmpresaDeGrupoDto> Empresas);

/// <summary>Alta o reemplazo de un sistema de Plataforma: nombre, clave pública PEM (RS256/ES256) y orígenes que alojan la consola.</summary>
public sealed record FijarSistemaDePlataformaDto(string Nombre, string ClavePublicaPem, IReadOnlyList<string>? OrigenesPermitidos = null);

public sealed record SistemaDePlataformaDto(
    string Codigo, string Nombre, string ClavePublicaPem, IReadOnlyList<string> OrigenesPermitidos, bool Activo, DateTimeOffset CreadoEn,
    DateTimeOffset ActualizadoEn);

/// <summary>Probar una llave de OpenAI: la del cuerpo (aún sin guardar) o, si viene nula, la guardada en el plan.</summary>
public sealed record ProbarLlaveIaDto(string? ApiKeyOpenAi = null);

/// <summary>
/// Resultado de la prueba: <c>valida</c>, <c>invalida</c> (401/403), <c>limite</c> (429: sin cuota o límite de uso),
/// <c>sin-llave</c> o <c>error</c> (red, tiempo agotado u otra respuesta). Nunca devuelve la llave; solo sus últimos cuatro.
/// </summary>
public sealed record PruebaDeLlaveIaDto(string Resultado, string Mensaje, string? UltimosCuatro, DateTimeOffset ProbadaEn);

/// <summary>Secretos de un tipo: cuántos hay, cuántos ya usan la clave vigente, cuántos se re-cifraron y cuántos no se pudieron leer.</summary>
public sealed record SecretosPorTipoDto(string Tipo, int Total, int Vigentes, int Recifrados, int Fallidos);

/// <summary>
/// Estado de los secretos en reposo (PT-3): identificador de la clave maestra vigente y de las anteriores configuradas, y por
/// tipo de secreto lo que está con cada una. <see cref="Simulado"/> verdadero = solo se contó (no se re-cifró nada).
/// </summary>
public sealed record EstadoDeSecretosDto(string ClaveVigente, IReadOnlyList<string> ClavesAnteriores, bool Simulado, IReadOnlyList<SecretosPorTipoDto> PorTipo);
