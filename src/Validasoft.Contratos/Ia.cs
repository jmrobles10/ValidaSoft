namespace Validasoft.Contratos;

// Plan de IA por empresa y su uso (ADR-0012, IA-2…IA-4). La API key de OpenAI es de solo escritura: nunca vuelve.

/// <summary>
/// Plan de IA de una empresa como lo ve Plataforma. <see cref="Configurado"/> falso ⇒ aún no se creó (valores por defecto,
/// inactivo). De la llave solo se informa si está configurada y sus últimos cuatro caracteres.
/// </summary>
public sealed record PlanIaEmpresaDto(
    Guid EmpresaId, bool Configurado, int ValidacionesIncluidasMes, bool AdicionalHabilitado, int? TopeAdicionalMes, bool LlaveConfigurada,
    string? UltimosCuatro, string ModeloPorDefecto, string ModeloEscalamiento, decimal UmbralConfianza, bool Activo, DateTimeOffset? ActualizadoEn,
    decimal? PrecioAdicional = null);

/// <summary>
/// Crea o reemplaza el plan. <see cref="TopeAdicionalMes"/> nulo = sin tope. <see cref="ApiKeyOpenAi"/> nula conserva la
/// llave actual y vacía la quita. Modelos y umbral nulos conservan los actuales (o los por defecto).
/// <see cref="PrecioAdicional"/> es lo que se cobra por cada validación adicional de esta empresa (PI-12); nulo = el precio de la tarifa general.
/// </summary>
public sealed record ActualizarPlanIaEmpresaDto(
    int ValidacionesIncluidasMes, bool AdicionalHabilitado, int? TopeAdicionalMes = null, string? ApiKeyOpenAi = null, string? ModeloPorDefecto = null,
    string? ModeloEscalamiento = null, decimal? UmbralConfianza = null, bool Activo = true,
    decimal? PrecioAdicional = null);

/// <summary>El plan de la empresa activa, sin la llave (solo si está configurada).</summary>
public sealed record PlanIaResumenDto(
    bool Configurado, int ValidacionesIncluidasMes, bool AdicionalHabilitado, int? TopeAdicionalMes, bool LlaveConfigurada, string ModeloPorDefecto,
    string ModeloEscalamiento, decimal UmbralConfianza, bool Activo);

/// <summary>Uso de un día (fecha en la zona horaria del tenant).</summary>
public sealed record UsoIaDiaDto(DateOnly Fecha, int Incluidas, int Adicionales, int AnalisisPagados, long TokensEntrada, long TokensSalida, decimal CostoUsd);

/// <summary>
/// Uso de IA de la empresa en un mes (<c>yyyy-MM</c>, zona del tenant): validaciones incluidas y adicionales consumidas,
/// las que quedan (<see cref="AdicionalesRestantes"/> nulo = adicional sin tope), los análisis que pagó su llave con sus
/// tokens y su costo real en dólares, y la serie por día.
/// </summary>
public sealed record UsoIaDto(
    string Mes, PlanIaResumenDto Plan, int IncluidasConsumidas, int AdicionalesConsumidas, int IncluidasRestantes, int? AdicionalesRestantes,
    int AnalisisPagados, long TokensEntrada, long TokensEntradaEnCache, long TokensSalida, decimal CostoUsd, IReadOnlyList<UsoIaDiaDto> PorDia);
