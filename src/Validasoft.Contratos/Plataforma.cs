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

// ---------- Consola de Plataforma, segunda tanda (PI-7, PI-9, PI-12, PI-16, PI-17) ----------

/// <summary>La clave de arranque de Plataforma (PI-17): si el servidor la tiene configurada y si ya se desactivó desde la consola.</summary>
public sealed record ClaveDeArranqueDto(bool Configurada, bool Desactivada, DateTimeOffset? DesactivadaEn, string? DesactivadaPor, int OperadoresConPasskey);

/// <summary>Una línea de la auditoría de ingresos de los operadores de Plataforma (PI-16).</summary>
public sealed record IngresoDeOperadorDto(
    Guid Id, DateTimeOffset Fecha, string Evento, bool Exitoso, string? OperadorId, string? Correo, string? Metodo, string? Motivo, string? Ip, string? Dispositivo,
    string ActorTipo, string ActorId, Guid? SesionId, Guid? PasskeyId);

// ---------- Consumo de IA por cliente (PI-12) ----------

/// <summary>Lo que gastó una empresa con un modelo en el mes: llamadas, tokens y costo real (con la llave de la propia empresa).</summary>
public sealed record UsoPorModeloIaDto(string Modelo, int Llamadas, long TokensEntrada, long TokensEntradaEnCache, long TokensSalida, decimal CostoUsd);

/// <summary>
/// El consumo de IA de una empresa en un mes y lo que se le cobra por las validaciones adicionales. <see cref="PrecioPropio"/> verdadero = el precio es el que
/// Plataforma fijó para esa empresa; falso = el de la tarifa general.
/// </summary>
public sealed record ConsumoDeIaEmpresaDto(
    Guid EmpresaId, string Empresa, int ValidacionesIncluidasMes, int IncluidasConsumidas, int AdicionalesConsumidas, bool AdicionalHabilitado, int AnalisisPagados,
    long TokensEntrada, long TokensEntradaEnCache, long TokensSalida, decimal CostoUsd, decimal PrecioAdicional, bool PrecioPropio, decimal ValorAdicional,
    IReadOnlyList<UsoPorModeloIaDto> Modelos);

/// <summary>El consumo de las empresas de un grupo (los totales del grupo suman sus empresas).</summary>
public sealed record ConsumoDeIaGrupoDto(
    Guid GrupoId, string Grupo, string Slug, int IncluidasConsumidas, int AdicionalesConsumidas, int AnalisisPagados, long TokensEntrada, long TokensSalida,
    decimal CostoUsd, decimal ValorAdicional, IReadOnlyList<ConsumoDeIaEmpresaDto> Empresas);

/// <summary>Totales del mes de todos los grupos que cumplen el filtro (no solo los de la página).</summary>
public sealed record ConsumoDeIaTotalesDto(
    int Grupos, int Empresas, int IncluidasConsumidas, int AdicionalesConsumidas, int AnalisisPagados, long TokensEntrada, long TokensSalida, decimal CostoUsd,
    decimal ValorAdicional);

/// <summary>
/// Consumo de IA por cliente en un mes (<c>yyyy-MM</c>): una página de grupos con sus empresas, los totales y la tarifa vigente con la que se calculó el valor
/// de las validaciones adicionales.
/// </summary>
public sealed record ConsumoDeIaDto(
    string Periodo, string Moneda, decimal PrecioPorValidacionAdicional, ConsumoDeIaTotalesDto Totales, IReadOnlyList<ConsumoDeIaGrupoDto> Grupos,
    string? SiguienteCursor);

/// <summary>La tarifa general de las validaciones adicionales de IA (<c>GET/PUT /plataforma/ia/tarifa</c>): lo que se cobra por cada una.</summary>
public sealed record TarifaDeIaDto(decimal PrecioPorValidacionAdicional, string Moneda, DateTimeOffset? ActualizadaEn, string? ActualizadaPor);

public sealed record FijarTarifaDeIaDto(decimal PrecioPorValidacionAdicional, string Moneda);
