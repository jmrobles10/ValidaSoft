using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Ia;

/// <summary>
/// Medición del uso de IA por cliente para cobrar el adicional (PI-12): por grupo y empresa y por mes, las validaciones incluidas y adicionales consumidas, los análisis
/// que pagó la llave de la empresa con sus tokens, modelo y costo, y el valor de lo adicional con el precio configurable (el de la empresa o, si no tiene, la tarifa
/// general). Solo Plataforma; se ve en la consola y se exporta a Excel o CSV.
/// </summary>
public static partial class ConsumoDeIaEndpoints
{
    public const string EntidadTarifa = "TarifaDeIa";

    private const int TamanoPaginaPorOmision = 20;
    private const int MaximoPagina = 100;

    /// <summary>
    /// Una página de grupos con el consumo de sus empresas en <c>periodo</c> (<c>yyyy-MM</c>; por omisión el mes en curso en la zona del negocio), los totales de todos los
    /// grupos que cumplen el filtro y la tarifa con la que se valoró. Filtros: <c>grupoId</c> y <c>q</c> (parte del nombre o del identificador del grupo, o de la razón social
    /// de una de sus empresas). Orden estable por nombre del grupo. 422 <c>periodo-invalido</c>.
    /// </summary>
    [WolverineGet("/api/v1/plataforma/ia/consumo")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ConsumoDeIaDto> Consumo(ISesiones sesiones, TimeProvider reloj, string? periodo, Guid? grupoId, string? q, int? limite, string? cursor, CancellationToken ct)
    {
        var mes = Periodo(periodo, reloj);
        var tamano = Math.Clamp(limite ?? TamanoPaginaPorOmision, 1, MaximoPagina);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var (tarifa, grupos) = await CalcularAsync(sesiones, mes, grupoId, q, ct);

        var totales = new ConsumoDeIaTotalesDto(grupos.Count, grupos.Sum(g => g.Empresas.Count), grupos.Sum(g => g.IncluidasConsumidas), grupos.Sum(g => g.AdicionalesConsumidas),
            grupos.Sum(g => g.AnalisisPagados), grupos.Sum(g => g.TokensEntrada), grupos.Sum(g => g.TokensSalida), grupos.Sum(g => g.CostoUsd), grupos.Sum(g => g.ValorAdicional));
        return new ConsumoDeIaDto(mes, tarifa.Moneda, tarifa.PrecioPorValidacionAdicional, totales, [.. grupos.Skip(desde).Take(tamano)],
            grupos.Count > desde + tamano ? Peticiones.EscribirDesplazamiento(desde + tamano) : null);
    }

    /// <summary>
    /// El mismo consumo (todos los grupos que cumplen el filtro, sin paginar) en un archivo: <c>formato=xlsx</c> (por omisión; una hoja por empresa y otra por modelo) o
    /// <c>formato=csv</c> (una fila por empresa). 422 <c>periodo-invalido</c>, <c>formato-invalido</c>.
    /// </summary>
    [WolverineGet("/api/v1/plataforma/ia/consumo/exportar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Exportar(ISesiones sesiones, TimeProvider reloj, string? periodo, Guid? grupoId, string? q, string? formato, CancellationToken ct)
    {
        var tipo = (formato ?? "xlsx").Trim().ToLowerInvariant();
        if (tipo is not ("xlsx" or "csv"))
            throw ReglaDeNegocioException.Validacion("formato-invalido", "El formato debe ser xlsx o csv.");

        var mes = Periodo(periodo, reloj);
        var (tarifa, grupos) = await CalcularAsync(sesiones, mes, grupoId, q, ct);
        var empresas = grupos.SelectMany(g => g.Empresas.Select(e => (Grupo: g, Empresa: e))).ToList();
        string[] encabezado =
        [
            "Grupo", "Identificador del grupo", "Empresa", "Periodo", "Validaciones incluidas en el plan", "Incluidas consumidas", "Adicionales consumidas", "Adicional habilitado",
            "Precio por adicional", "Moneda", "Valor de lo adicional", "Precio propio", "Análisis pagados con su llave", "Tokens de entrada", "Tokens de entrada en caché",
            "Tokens de salida", "Costo real (USD)"
        ];
        IReadOnlyList<object?> Fila((ConsumoDeIaGrupoDto Grupo, ConsumoDeIaEmpresaDto Empresa) x) =>
        [
            x.Grupo.Grupo, x.Grupo.Slug, x.Empresa.Empresa, mes, x.Empresa.ValidacionesIncluidasMes, x.Empresa.IncluidasConsumidas, x.Empresa.AdicionalesConsumidas,
            x.Empresa.AdicionalHabilitado ? "Sí" : "No", x.Empresa.PrecioAdicional, tarifa.Moneda, x.Empresa.ValorAdicional, x.Empresa.PrecioPropio ? "Sí" : "No",
            x.Empresa.AnalisisPagados, x.Empresa.TokensEntrada, x.Empresa.TokensEntradaEnCache, x.Empresa.TokensSalida, x.Empresa.CostoUsd
        ];

        var nombre = $"consumo-ia-{mes}";
        if (tipo == "csv")
        {
            var csv = new StringBuilder();
            csv.AppendLine(string.Join(',', encabezado.Select(Csv)));
            foreach (var x in empresas)
                csv.AppendLine(string.Join(',', Fila(x).Select(v => Csv(Texto(v)))));
            return Results.File([.. Encoding.UTF8.GetPreamble(), .. Encoding.UTF8.GetBytes(csv.ToString())], "text/csv; charset=utf-8", nombre + ".csv");
        }

        var porModelo = empresas.SelectMany(x => x.Empresa.Modelos.Select(m => (IReadOnlyList<object?>)
        [
            x.Grupo.Grupo, x.Grupo.Slug, x.Empresa.Empresa, mes, m.Modelo, m.Llamadas, m.TokensEntrada, m.TokensEntradaEnCache, m.TokensSalida, m.CostoUsd
        ]));
        var bytes = HojaDeCalculo.CrearLibro(
            new Hoja("Por empresa", encabezado, empresas.Select(Fila)),
            new Hoja("Por modelo", ["Grupo", "Identificador del grupo", "Empresa", "Periodo", "Modelo", "Llamadas", "Tokens de entrada", "Tokens de entrada en caché", "Tokens de salida", "Costo real (USD)"],
                porModelo));
        return Results.File(bytes, HojaDeCalculo.TipoContenido, nombre + ".xlsx");
    }

    [WolverineGet("/api/v1/plataforma/ia/tarifa")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<TarifaDeIaDto> ObtenerTarifa(ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return (await consulta.LoadAsync<TarifaDeIa>(TarifaDeIa.IdUnico, ct) ?? TarifaDeIa.PorDefecto()).ADto();
    }

    /// <summary>
    /// Fija la tarifa general: lo que se cobra por cada validación adicional y la moneda. Rige para todas las empresas que no tengan un precio propio en su plan. Queda en la
    /// auditoría. 422 <c>precio-adicional-invalido</c>, <c>moneda-invalida</c>.
    /// </summary>
    [WolverinePut("/api/v1/plataforma/ia/tarifa")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<TarifaDeIaDto> FijarTarifa(FijarTarifaDeIaDto dto, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var existente = await sesion.LoadAsync<TarifaDeIa>(TarifaDeIa.IdUnico, ct);
        var tarifa = existente ?? TarifaDeIa.PorDefecto();
        var antes = tarifa.ADto();
        tarifa.Fijar(dto.PrecioPorValidacionAdicional, dto.Moneda, $"{contexto.ActorTipo}:{contexto.ActorId}", ahora);
        sesion.Store(tarifa);
        Auditoria.Registrar(sesion, contexto, EntidadTarifa, TarifaDeIa.IdUnico, existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes, tarifa.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);
        return tarifa.ADto();
    }

    // ---------- cálculo ----------

    private static async Task<(TarifaDeIa Tarifa, List<ConsumoDeIaGrupoDto> Grupos)> CalcularAsync(ISesiones sesiones, string periodo, Guid? grupoId, string? q, CancellationToken ct)
    {
        var texto = q?.Trim();
        await using var consulta = sesiones.ConsultaGlobal();
        var tarifa = await consulta.LoadAsync<TarifaDeIa>(TarifaDeIa.IdUnico, ct) ?? TarifaDeIa.PorDefecto();

        IQueryable<Tenant> tenants = consulta.Query<Tenant>().Where(t => t.TenantDeProduccionId == null); // sin los sandbox (SB-1)
        if (grupoId is { } id)
            tenants = tenants.Where(t => t.Id == id);
        var lista = await tenants.OrderBy(t => t.Nombre).ThenBy(t => t.Id).ToListAsync(ct);
        if (lista.Count == 0)
            return (tarifa, []);

        var ids = lista.Select(t => t.Id).ToArray();
        var empresas = await consulta.Query<Empresa>().Where(e => e.TenantId.IsOneOf(ids)).ToListAsync(ct);
        if (!string.IsNullOrEmpty(texto))
        {
            var conEmpresa = empresas.Where(e => e.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase)).Select(e => e.TenantId).ToHashSet();
            lista = [.. lista.Where(t => t.Nombre.Contains(texto, StringComparison.OrdinalIgnoreCase) || t.Slug.Contains(texto, StringComparison.OrdinalIgnoreCase) || conEmpresa.Contains(t.Id))];
        }

        // El uso y los planes viven en cada grupo: una consulta entre todos, y se reparten por empresa.
        var usos = (await consulta.Query<UsoMensualIa>().Where(u => u.AnyTenant() && u.Mes == periodo).ToListAsync(ct)).ToDictionary(u => u.EmpresaId);
        var planes = (await consulta.Query<PlanDeIaEmpresa>().Where(p => p.AnyTenant()).ToListAsync(ct)).ToDictionary(p => p.EmpresaId);

        var grupos = new List<ConsumoDeIaGrupoDto>(lista.Count);
        foreach (var tenant in lista)
        {
            var filas = new List<ConsumoDeIaEmpresaDto>();
            foreach (var empresa in empresas.Where(e => e.TenantId == tenant.Id).OrderBy(e => e.Nombre, StringComparer.CurrentCulture))
            {
                planes.TryGetValue(empresa.Id, out var plan);
                usos.TryGetValue(empresa.Id, out var uso);
                var precio = tarifa.PrecioPara(plan);
                var adicionales = uso?.AdicionalesConsumidas ?? 0;
                filas.Add(new ConsumoDeIaEmpresaDto(empresa.Id, empresa.Nombre, plan?.ValidacionesIncluidasMes ?? 0, uso?.IncluidasConsumidas ?? 0, adicionales,
                    plan?.AdicionalHabilitado ?? false, uso?.AnalisisPagados ?? 0, uso?.TokensEntrada ?? 0, uso?.TokensEntradaEnCache ?? 0, uso?.TokensSalida ?? 0,
                    uso?.CostoUsd ?? 0m, precio, plan?.PrecioAdicional is not null, TarifaDeIa.ValorDe(adicionales, precio),
                    [.. (uso?.PorModelo ?? []).OrderBy(m => m.Key, StringComparer.Ordinal)
                        .Select(m => new UsoPorModeloIaDto(m.Key, m.Value.Llamadas, m.Value.TokensEntrada, m.Value.TokensEntradaEnCache, m.Value.TokensSalida, m.Value.CostoUsd))]));
            }

            grupos.Add(new ConsumoDeIaGrupoDto(tenant.Id, tenant.Nombre, tenant.Slug, filas.Sum(f => f.IncluidasConsumidas), filas.Sum(f => f.AdicionalesConsumidas),
                filas.Sum(f => f.AnalisisPagados), filas.Sum(f => f.TokensEntrada), filas.Sum(f => f.TokensSalida), filas.Sum(f => f.CostoUsd), filas.Sum(f => f.ValorAdicional), filas));
        }

        return (tarifa, grupos);
    }

    private static string Periodo(string? periodo, TimeProvider reloj)
    {
        if (string.IsNullOrWhiteSpace(periodo))
            return UsoMensualIa.MesDe(CalculadoraDeVigencia.Fecha(reloj.GetUtcNow()));
        var limpio = periodo.Trim();
        return RegexPeriodo().IsMatch(limpio) ? limpio : throw ReglaDeNegocioException.Validacion("periodo-invalido", "El periodo debe tener la forma AAAA-MM (por ejemplo 2026-11).");
    }

    private static TarifaDeIaDto ADto(this TarifaDeIa t) => new(t.PrecioPorValidacionAdicional, t.Moneda, t.ActualizadaEn, t.ActualizadaPor);

    private static string Texto(object? valor) => valor switch
    {
        null => string.Empty,
        decimal d => d.ToString(CultureInfo.InvariantCulture),
        IFormattable f => f.ToString(null, CultureInfo.InvariantCulture),
        _ => valor.ToString() ?? string.Empty
    };

    /// <summary>Un valor de CSV: entre comillas si lleva coma, comillas o saltos de línea (y sin dejar que una celda se lea como fórmula).</summary>
    private static string Csv(string valor)
    {
        if (valor.Length > 0 && valor[0] is '=' or '+' or '-' or '@' && !decimal.TryParse(valor, NumberStyles.Any, CultureInfo.InvariantCulture, out _))
            valor = "'" + valor;
        return valor.Contains(',', StringComparison.Ordinal) || valor.Contains('"', StringComparison.Ordinal) || valor.Contains('\n', StringComparison.Ordinal)
            ? "\"" + valor.Replace("\"", "\"\"", StringComparison.Ordinal) + "\""
            : valor;
    }

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex RegexPeriodo();
}
