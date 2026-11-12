using System.Globalization;
using System.Text.RegularExpressions;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Ia;

/// <summary>
/// Plan de IA de cada empresa, lo configura Plataforma (ADR-0012, IA-2). La API key de OpenAI es de solo escritura: se
/// guarda cifrada y solo vuelve si está configurada y sus últimos cuatro caracteres; la auditoría tampoco la lleva.
/// </summary>
public static class PlanIaEndpoints
{
    public const string Entidad = "PlanIaEmpresa";

    [WolverineGet("/api/v1/plataforma/tenants/{tenantId}/empresas/{empresaId}/plan-ia")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<PlanIaEmpresaDto> Obtener(Guid tenantId, Guid empresaId, ISesiones sesiones, CancellationToken ct)
    {
        await EmpresaDelTenantAsync(sesiones, tenantId, empresaId, ct);
        await using var sesion = sesiones.ParaTenant(tenantId);
        var plan = await sesion.LoadAsync<PlanDeIaEmpresa>(empresaId, ct);
        return (plan ?? PlanDeIaEmpresa.PorDefecto(empresaId)).ADto(plan is not null);
    }

    /// <summary>
    /// Crea o reemplaza el plan. <c>apiKeyOpenAi</c> nulo conserva la llave, <c>""</c> la quita. 404
    /// <c>tenant-no-encontrado</c> / <c>empresa-no-encontrada</c>; 422 <c>validaciones-incluidas-invalidas</c>,
    /// <c>tope-adicional-invalido</c>, <c>modelo-invalido</c>, <c>umbral-confianza-invalido</c>, <c>api-key-invalida</c>.
    /// </summary>
    [WolverinePut("/api/v1/plataforma/tenants/{tenantId}/empresas/{empresaId}/plan-ia")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<PlanIaEmpresaDto> Actualizar(Guid tenantId, Guid empresaId, ActualizarPlanIaEmpresaDto dto, ISesiones sesiones,
        IContextoSolicitud contexto, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        await EmpresaDelTenantAsync(sesiones, tenantId, empresaId, ct);
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.ParaTenant(tenantId);
        var existente = await sesion.LoadAsync<PlanDeIaEmpresa>(empresaId, ct);
        var antes = existente?.ADto(true);
        var plan = existente ?? PlanDeIaEmpresa.Nuevo(empresaId, ahora);
        plan.Actualizar(new CambiosDePlanIa(dto.ValidacionesIncluidasMes, dto.AdicionalHabilitado, dto.TopeAdicionalMes, dto.ApiKeyOpenAi,
            dto.ModeloPorDefecto, dto.ModeloEscalamiento, dto.UmbralConfianza, dto.Activo, dto.PrecioAdicional), protector.Proteger, ahora);
        var despues = plan.ADto(true);

        sesion.Store(plan);
        Auditoria.Registrar(sesion, contexto, Entidad, empresaId.ToString(), existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado,
            antes, despues, ahora, empresaId);
        await sesion.SaveChangesAsync(ct);
        return despues;
    }

    /// <summary>
    /// «Probar llave» (PT-6): verifica contra el proveedor la llave del cuerpo (aún sin guardar) o, si viene nula, la guardada
    /// en el plan. Solo cuando Plataforma lo pide; no cambia el plan ni consume validaciones. Responde siempre 200 con el
    /// resultado (<c>valida</c>, <c>invalida</c>, <c>limite</c>, <c>sin-llave</c>, <c>error</c>) y solo los últimos cuatro de la
    /// llave. 404 <c>tenant-no-encontrado</c> / <c>empresa-no-encontrada</c>; 422 <c>api-key-invalida</c> (forma).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/tenants/{tenantId}/empresas/{empresaId}/plan-ia/probar-llave")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<PruebaDeLlaveIaDto> ProbarLlave(Guid tenantId, Guid empresaId, ProbarLlaveIaDto dto, ISesiones sesiones,
        IProtectorDeSecretos protector, IAnalizadorDeDocumentos analizador, TimeProvider reloj, CancellationToken ct)
    {
        await EmpresaDelTenantAsync(sesiones, tenantId, empresaId, ct);
        string? llave;
        if (!string.IsNullOrWhiteSpace(dto.ApiKeyOpenAi))
            llave = PlanDeIaEmpresa.ValidarLlave(dto.ApiKeyOpenAi);
        else
        {
            await using var sesion = sesiones.ParaTenant(tenantId);
            var plan = await sesion.LoadAsync<PlanDeIaEmpresa>(empresaId, ct);
            llave = plan is { LlaveConfigurada: true } ? protector.Desproteger(plan.ApiKeyOpenAiProtegida!) : null;
        }

        if (llave is null)
            return new PruebaDeLlaveIaDto(ResultadoDePruebaDeLlave.SinLlave, "La empresa no tiene una llave de OpenAI configurada.", null,
                reloj.GetUtcNow());

        var resultado = await analizador.ProbarLlaveAsync(llave, ct);
        return new PruebaDeLlaveIaDto(resultado.Resultado, resultado.Mensaje, llave[^4..], reloj.GetUtcNow());
    }

    public static PlanIaEmpresaDto ADto(this PlanDeIaEmpresa p, bool configurado) =>
        new(p.EmpresaId, configurado, p.ValidacionesIncluidasMes, p.AdicionalHabilitado, p.TopeAdicionalMes, p.LlaveConfigurada,
            p.LlaveConfigurada ? p.UltimosCuatroLlave : null, p.ModeloPorDefecto, p.ModeloEscalamiento, p.UmbralConfianza, p.Activo,
            configurado ? p.ActualizadoEn : null, p.PrecioAdicional);

    public static PlanIaResumenDto Resumen(this PlanDeIaEmpresa p, bool configurado) =>
        new(configurado, p.ValidacionesIncluidasMes, p.AdicionalHabilitado, p.TopeAdicionalMes, p.LlaveConfigurada, p.ModeloPorDefecto,
            p.ModeloEscalamiento, p.UmbralConfianza, p.Activo);

    private static async Task EmpresaDelTenantAsync(ISesiones sesiones, Guid tenantId, Guid empresaId, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        if (await consulta.LoadAsync<Tenant>(tenantId, ct) is null)
            throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese tenant.");
        if (await consulta.LoadAsync<Empresa>(empresaId, ct) is not { } empresa || empresa.TenantId != tenantId)
            throw ReglaDeNegocioException.NoEncontrado("empresa-no-encontrada", "No existe esa empresa en el tenant.");
    }
}

/// <summary>Plan y uso de IA de la empresa activa (pantalla «Plan y uso», ADR-0012).</summary>
public static partial class UsoIaEndpoints
{
    /// <summary>
    /// Plan resumido (sin la llave) y uso del mes <c>mes</c> (<c>yyyy-MM</c>; por defecto el actual en la zona horaria del
    /// tenant): validaciones incluidas y adicionales consumidas y restantes, análisis pagados con su llave, tokens, costo
    /// real en dólares y serie por día. 422 <c>mes-invalido</c>.
    /// </summary>
    [WolverineGet("/api/v1/ia/uso")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<UsoIaDto> Obtener(string? mes, SesionDeEmpresa empresa, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        string periodo;
        if (string.IsNullOrWhiteSpace(mes))
        {
            await using var consulta = sesiones.ConsultaGlobal();
            var tenant = await consulta.LoadAsync<Tenant>(contexto.TenantRequerido(), ct);
            periodo = UsoMensualIa.MesDe(CalendarioDeIa.Dia(reloj.GetUtcNow(), tenant?.ZonaHoraria));
        }
        else
        {
            periodo = mes.Trim();
            if (!RegexMes().IsMatch(periodo))
                throw ReglaDeNegocioException.Validacion("mes-invalido", "El mes debe tener la forma AAAA-MM (por ejemplo 2026-11).");
        }

        var plan = await empresa.Sesion.LoadAsync<PlanDeIaEmpresa>(empresaId, ct);
        var vigente = plan ?? PlanDeIaEmpresa.PorDefecto(empresaId);
        var uso = await empresa.Sesion.LoadAsync<UsoMensualIa>(UsoMensualIa.Clave(empresaId, periodo), ct) ?? UsoMensualIa.Nuevo(empresaId, periodo);
        int? adicionalesRestantes = !vigente.AdicionalHabilitado
            ? 0
            : vigente.TopeAdicionalMes is { } tope ? Math.Max(0, tope - uso.AdicionalesConsumidas) : null;

        return new UsoIaDto(periodo, vigente.Resumen(plan is not null), uso.IncluidasConsumidas, uso.AdicionalesConsumidas,
            Math.Max(0, vigente.ValidacionesIncluidasMes - uso.IncluidasConsumidas), adicionalesRestantes, uso.AnalisisPagados, uso.TokensEntrada,
            uso.TokensEntradaEnCache, uso.TokensSalida, uso.CostoUsd,
            [
                .. uso.PorDia.OrderBy(d => d.Key, StringComparer.Ordinal)
                    .Select(d => new UsoIaDiaDto(DateOnly.ParseExact(d.Key, "yyyy-MM-dd", CultureInfo.InvariantCulture), d.Value.Incluidas,
                        d.Value.Adicionales, d.Value.AnalisisPagados, d.Value.TokensEntrada, d.Value.TokensSalida, d.Value.CostoUsd))
            ]);
    }

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex RegexMes();
}
