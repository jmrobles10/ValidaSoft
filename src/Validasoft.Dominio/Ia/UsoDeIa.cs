using System.Globalization;
using Validasoft.Dominio.Integraciones;

namespace Validasoft.Dominio.Ia;

/// <summary>De qué cupo salió una validación (IA-3, IA-4).</summary>
public enum TipoValidacionIa
{
    Incluida,
    Adicional
}

/// <summary>
/// Una validación de IA que consumió una empresa (IA-3): el análisis de una versión de un documento que esa empresa
/// recibe. Esquema <c>validacion</c>, multi-tenant; id determinista por (empresa, documento, versión), así la misma
/// empresa nunca paga dos veces la misma versión. Que exista es lo que habilita a la empresa a ver los hechos de IA.
/// </summary>
public sealed class ValidacionIaConsumida
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid DocumentoId { get; set; }

    public int Numero { get; set; }

    /// <summary>Mes calendario (<c>yyyy-MM</c>) en la zona del tenant.</summary>
    public string Mes { get; set; } = string.Empty;

    public TipoValidacionIa Tipo { get; set; }

    /// <summary>La llamada a OpenAI se pagó con la llave de esta empresa.</summary>
    public bool PagoLaLlamada { get; set; }

    public DateTimeOffset Fecha { get; set; }

    public static Guid Clave(Guid empresaId, Guid documentoId, int numero) => Deterministas.Guid($"ia-validacion:{empresaId:N}:{documentoId:N}:{numero}");
}

/// <summary>Uso de un día del mes (serie diaria de <c>GET /ia/uso</c>).</summary>
public sealed class UsoDiarioIa
{
    public int Incluidas { get; set; }

    public int Adicionales { get; set; }

    public int AnalisisPagados { get; set; }

    public long TokensEntrada { get; set; }

    public long TokensSalida { get; set; }

    public decimal CostoUsd { get; set; }
}

/// <summary>Resultado de pedir una validación: el cupo del que salió (nulo = sin cupo) y los umbrales de aviso que cruzó.</summary>
public sealed record ConsumoDeValidacion(TipoValidacionIa? Tipo, IReadOnlyList<int> AvisosCruzados)
{
    public bool Consumida => Tipo is not null;

    public static readonly ConsumoDeValidacion SinCupo = new(null, []);
}

/// <summary>
/// Contador de uso de IA de una empresa en un mes (IA-3, IA-4). Esquema <c>validacion</c>, multi-tenant, con concurrencia
/// optimista (los manejadores reintentan si pierden la carrera); id <c>{empresa:N}:{yyyy-MM}</c>. Cuenta validaciones
/// incluidas y adicionales consumidas, los análisis que pagó su llave (tokens y costo real) y los avisos de 80 % y 100 %
/// ya enviados (uno por umbral y mes).
/// </summary>
public sealed class UsoMensualIa
{
    public static readonly IReadOnlyList<int> Umbrales = [80, 100];

    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public string Mes { get; set; } = string.Empty;

    public int IncluidasConsumidas { get; set; }

    public int AdicionalesConsumidas { get; set; }

    public int AnalisisPagados { get; set; }

    public long TokensEntrada { get; set; }

    public long TokensEntradaEnCache { get; set; }

    public long TokensSalida { get; set; }

    public decimal CostoUsd { get; set; }

    /// <summary>Umbrales (80, 100) cuyo aviso ya salió este mes.</summary>
    public List<int> AvisosEnviados { get; set; } = [];

    /// <summary>Serie por día (<c>yyyy-MM-dd</c> en la zona del tenant).</summary>
    public Dictionary<string, UsoDiarioIa> PorDia { get; set; } = [];

    public static string Clave(Guid empresaId, string mes) => $"{empresaId:N}:{mes}";

    public static UsoMensualIa Nuevo(Guid empresaId, string mes) => new() { Id = Clave(empresaId, mes), EmpresaId = empresaId, Mes = mes };

    public static string MesDe(DateOnly dia) => dia.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    /// <summary>
    /// Regla de cupo (IA-4) sin cambiar nada: con validaciones incluidas disponibles ⇒ <c>Incluida</c>; si no, con adicional
    /// habilitado y bajo el tope ⇒ <c>Adicional</c>; si no (o sin plan activo) ⇒ nulo.
    /// </summary>
    public TipoValidacionIa? CupoDisponible(PlanDeIaEmpresa? plan)
    {
        if (plan is not { Activo: true })
            return null;
        if (IncluidasConsumidas < plan.ValidacionesIncluidasMes)
            return TipoValidacionIa.Incluida;
        if (plan.AdicionalHabilitado && (plan.TopeAdicionalMes is not { } tope || AdicionalesConsumidas < tope))
            return TipoValidacionIa.Adicional;
        return null;
    }

    /// <summary>Consume una validación según <see cref="CupoDisponible"/> y marca los avisos de 80 % y 100 % de las incluidas que cruce.</summary>
    public ConsumoDeValidacion Consumir(PlanDeIaEmpresa? plan, DateOnly dia)
    {
        if (CupoDisponible(plan) is not { } tipo)
            return ConsumoDeValidacion.SinCupo;

        var delDia = Dia(dia);
        if (tipo == TipoValidacionIa.Incluida)
        {
            IncluidasConsumidas++;
            delDia.Incluidas++;
        }
        else
        {
            AdicionalesConsumidas++;
            delDia.Adicionales++;
        }

        // Si una sola validación cruza los dos umbrales (cupo pequeño), sale solo el aviso del mayor; ambos quedan enviados.
        var cruzados = new List<int>();
        var incluidas = plan!.ValidacionesIncluidasMes;
        if (incluidas > 0)
        {
            foreach (var umbral in Umbrales)
            {
                if (IncluidasConsumidas * 100L >= (long)incluidas * umbral && !AvisosEnviados.Contains(umbral))
                {
                    AvisosEnviados.Add(umbral);
                    cruzados.Add(umbral);
                }
            }
        }

        return new ConsumoDeValidacion(tipo, cruzados.Count == 0 ? [] : [cruzados.Max()]);
    }

    /// <summary>Devuelve una validación consumida (el análisis falló: no se cobra, IA-6). Los avisos ya enviados no se retiran.</summary>
    public void Revertir(TipoValidacionIa tipo, DateOnly dia)
    {
        var delDia = Dia(dia);
        if (tipo == TipoValidacionIa.Incluida)
        {
            IncluidasConsumidas = Math.Max(0, IncluidasConsumidas - 1);
            delDia.Incluidas = Math.Max(0, delDia.Incluidas - 1);
        }
        else
        {
            AdicionalesConsumidas = Math.Max(0, AdicionalesConsumidas - 1);
            delDia.Adicionales = Math.Max(0, delDia.Adicionales - 1);
        }
    }

    /// <summary>Suma una llamada a OpenAI pagada con la llave de la empresa (tokens y costo real, IA-3).</summary>
    public void RegistrarCosto(long tokensEntrada, long tokensEnCache, long tokensSalida, decimal costoUsd, DateOnly dia, bool analisis)
    {
        var delDia = Dia(dia);
        if (analisis)
        {
            AnalisisPagados++;
            delDia.AnalisisPagados++;
        }

        TokensEntrada += tokensEntrada;
        TokensEntradaEnCache += tokensEnCache;
        TokensSalida += tokensSalida;
        CostoUsd += costoUsd;
        delDia.TokensEntrada += tokensEntrada;
        delDia.TokensSalida += tokensSalida;
        delDia.CostoUsd += costoUsd;
    }

    private UsoDiarioIa Dia(DateOnly dia)
    {
        var clave = dia.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture);
        if (!PorDia.TryGetValue(clave, out var uso))
            PorDia[clave] = uso = new UsoDiarioIa();
        return uso;
    }
}
