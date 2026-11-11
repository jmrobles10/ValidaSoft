using System.Globalization;
using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Plataforma;

/// <summary>Lo que el plan del grupo limita y se mide (OD-14).</summary>
public enum MetricaDePlan
{
    /// <summary>Personas del grupo con acceso activo.</summary>
    Usuarios,

    /// <summary>Empresas activas del grupo.</summary>
    Empresas,

    /// <summary>Vínculos que no están anulados ni desvinculados.</summary>
    VinculosActivos,

    /// <summary>Megabytes de documentos de la bóveda que las empresas del grupo reciben o cargan.</summary>
    Almacenamiento,

    /// <summary>Peticiones del mes con una API key del grupo (no cuenta el sandbox ni las sesiones de las pantallas).</summary>
    LlamadasApi
}

/// <summary>En qué punto está una métrica frente a su límite: por debajo del 80 %, de 80 % a menos de 100 %, o en el límite o por encima.</summary>
public enum NivelDeUso
{
    Normal,
    PorAgotar,
    Agotado
}

/// <summary>
/// Límites del plan: cada uno es un número no negativo, o nulo si no hay límite. <c>0</c> es un límite de verdad (no se permite
/// nada). El almacenamiento va en megabytes y las llamadas a la API, por mes calendario en la zona del grupo.
/// </summary>
public sealed record LimitesDePlan(int? Usuarios, int? Empresas, int? VinculosActivos, long? AlmacenamientoMb, long? LlamadasApiMes)
{
    public const long Maximo = 1_000_000_000_000;

    public static readonly LimitesDePlan SinLimites = new(null, null, null, null, null);

    /// <summary>El límite de la métrica en sus unidades de medición (el almacenamiento, en bytes); nulo si no hay.</summary>
    public long? De(MetricaDePlan metrica) => metrica switch
    {
        MetricaDePlan.Usuarios => Usuarios,
        MetricaDePlan.Empresas => Empresas,
        MetricaDePlan.VinculosActivos => VinculosActivos,
        MetricaDePlan.Almacenamiento => AlmacenamientoMb is { } mb ? mb * PlanDelGrupo.BytesPorMb : null,
        MetricaDePlan.LlamadasApi => LlamadasApiMes,
        _ => null
    };

    public bool Tiene => this != SinLimites;
}

/// <summary>Una métrica con lo usado y su límite: el nivel y el porcentaje salen de aquí.</summary>
public sealed record MedicionDeMetrica(MetricaDePlan Metrica, long Usado, long? Limite)
{
    public const int UmbralDeAviso = 80;

    /// <summary>Porcentaje usado (redondeado hacia abajo); nulo sin límite. Un límite de cero con algo usado es 100.</summary>
    public int? Porcentaje => Limite is not { } limite ? null : limite == 0 ? 100 : (int)Math.Min(1000, Usado * 100 / limite);

    public NivelDeUso Nivel => Limite is not { } limite
        ? NivelDeUso.Normal
        : Usado >= limite
            ? NivelDeUso.Agotado
            : Usado * 100 >= limite * UmbralDeAviso
                ? NivelDeUso.PorAgotar
                : NivelDeUso.Normal;

    /// <summary>¿Cabrían <paramref name="cuantos"/> más sin pasar del límite?</summary>
    public bool Cabe(long cuantos = 1) => Limite is not { } limite || Usado + cuantos <= limite;
}

/// <summary>
/// Plan y límites de un grupo (OD-14, docs/11 §H8): código del plan, límites de usuarios, empresas, vínculos activos,
/// almacenamiento y llamadas a la API, y si al llegar al límite se bloquean las altas (<see cref="BloqueoSuave"/>). Lo fija
/// Plataforma. Sin documento, el grupo no tiene límites. Esquema <c>plataforma</c>, global (id = tenant).
/// </summary>
public sealed class PlanDelGrupo
{
    public const long BytesPorMb = 1024 * 1024;
    public const int LongitudMinimaCodigo = 2;
    public const int LongitudMaximaCodigo = 40;
    public const string CodigoPorDefecto = "Inicial";

    /// <summary>El tenant (grupo) dueño del plan.</summary>
    public Guid Id { get; set; }

    public string Codigo { get; set; } = CodigoPorDefecto;

    public LimitesDePlan Limites { get; set; } = LimitesDePlan.SinLimites;

    /// <summary>
    /// Al llegar al 100 % de un límite no se permiten más altas de eso (usuarios, empresas, vínculos, documentos nuevos y, en las
    /// llamadas a la API, las escrituras); todo lo que ya existe sigue funcionando y las lecturas nunca se bloquean. Con falso solo se
    /// avisa.
    /// </summary>
    public bool BloqueoSuave { get; set; }

    /// <summary>Avisos de 80 % y 100 % ya enviados (<c>{métrica}:{umbral}:{periodo}</c>); se retiran cuando la métrica baja.</summary>
    public List<string> AvisosEnviados { get; set; } = [];

    /// <summary>Último tamaño medido de los documentos del grupo (bytes) y cuándo; el almacenamiento no se cuenta en cada petición.</summary>
    public long AlmacenamientoBytes { get; set; }

    public DateTimeOffset? AlmacenamientoMedidoEn { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    [JsonIgnore]
    public Guid TenantId => Id;

    public static PlanDelGrupo Nuevo(Guid tenantId, DateTimeOffset ahora) => new() { Id = tenantId, CreadoEn = ahora, ActualizadoEn = ahora };

    /// <summary>
    /// Aplica el plan: código de 2 a 40 caracteres y límites no negativos (o nulos). 422 <c>codigo-plan-invalido</c> o
    /// <c>limite-invalido</c>.
    /// </summary>
    public void Actualizar(string codigo, LimitesDePlan limites, bool bloqueoSuave, DateTimeOffset ahora)
    {
        var limpio = (codigo ?? string.Empty).Trim();
        if (limpio.Length is < LongitudMinimaCodigo or > LongitudMaximaCodigo)
            throw ReglaDeNegocioException.Validacion("codigo-plan-invalido",
                $"El código del plan debe tener entre {LongitudMinimaCodigo} y {LongitudMaximaCodigo} caracteres.");
        foreach (var (nombre, valor) in new (string, long?)[]
                 {
                     ("usuarios", limites.Usuarios), ("empresas", limites.Empresas), ("vínculos activos", limites.VinculosActivos),
                     ("almacenamiento", limites.AlmacenamientoMb), ("llamadas a la API", limites.LlamadasApiMes)
                 })
        {
            if (valor is < 0 or > LimitesDePlan.Maximo)
                throw ReglaDeNegocioException.Validacion("limite-invalido",
                    $"El límite de {nombre} debe estar entre 0 y {LimitesDePlan.Maximo.ToString("N0", CultureInfo.InvariantCulture)}, o vacío si no tiene límite.");
        }

        Codigo = limpio;
        Limites = limites;
        BloqueoSuave = bloqueoSuave;
        ActualizadoEn = ahora;
    }

    /// <summary>Clave de un aviso: <c>{métrica}:{umbral}:{periodo}</c>; <c>-</c> si la métrica no se reinicia por mes.</summary>
    public static string ClaveDeAviso(MetricaDePlan metrica, int umbral, string periodo) =>
        $"{metrica}:{umbral.ToString(CultureInfo.InvariantCulture)}:{periodo}";

    /// <summary>
    /// Lo que hay que avisar con estas mediciones: el umbral cruzado de cada métrica (80 o 100, el mayor) que aún no se avisó. Retira
    /// del registro los avisos de las que bajaron del umbral, para avisar de nuevo si lo vuelven a cruzar.
    /// </summary>
    public IReadOnlyList<(MedicionDeMetrica Medicion, int Umbral)> AvisosPendientes(IEnumerable<MedicionDeMetrica> mediciones, string periodo)
    {
        var pendientes = new List<(MedicionDeMetrica, int)>();
        foreach (var medicion in mediciones)
        {
            var periodoDeLaMetrica = medicion.Metrica == MetricaDePlan.LlamadasApi ? periodo : "-";
            var umbralActual = medicion.Nivel switch { NivelDeUso.Agotado => 100, NivelDeUso.PorAgotar => 80, _ => 0 };

            // Lo avisado de esta métrica que ya no corresponde (otro mes, o bajó de ese umbral) se retira: si vuelve a cruzarlo, se avisa de nuevo.
            AvisosEnviados.RemoveAll(aviso =>
            {
                var partes = aviso.Split(':');
                return partes.Length == 3 && partes[0] == medicion.Metrica.ToString() &&
                       (partes[2] != periodoDeLaMetrica || !int.TryParse(partes[1], CultureInfo.InvariantCulture, out var umbral) || umbral > umbralActual);
            });
            if (umbralActual == 0 || AvisosEnviados.Contains(ClaveDeAviso(medicion.Metrica, umbralActual, periodoDeLaMetrica)))
                continue;

            AvisosEnviados.Add(ClaveDeAviso(medicion.Metrica, umbralActual, periodoDeLaMetrica));
            // Pasar directo al 100 % deja también el del 80 % como ya dado.
            if (umbralActual == 100 && !AvisosEnviados.Contains(ClaveDeAviso(medicion.Metrica, 80, periodoDeLaMetrica)))
                AvisosEnviados.Add(ClaveDeAviso(medicion.Metrica, 80, periodoDeLaMetrica));
            pendientes.Add((medicion, umbralActual));
        }

        return pendientes;
    }
}

/// <summary>
/// Llamadas a la API de un grupo en un mes (OD-15): contador que se suma en lotes (cada pocos segundos) desde cada nodo. Esquema
/// <c>plataforma</c>, global; id <c>{tenant:N}:{yyyy-MM}</c>.
/// </summary>
public sealed class UsoMensualDelGrupo
{
    public string Id { get; set; } = string.Empty;

    public Guid TenantId { get; set; }

    public string Mes { get; set; } = string.Empty;

    public long LlamadasApi { get; set; }

    public static string Clave(Guid tenantId, string mes) => $"{tenantId:N}:{mes}";

    public static string MesDe(DateOnly dia) => dia.ToString("yyyy-MM", CultureInfo.InvariantCulture);
}
