using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;

namespace Validasoft.Dominio.Flujos;

public enum EstadoFlujoVersion
{
    Borrador,
    Publicada,
    Retirada
}

/// <summary>
/// Cuándo vuelve el expediente al tercero tras una observación: <see cref="Inmediata"/> (por defecto) con la primera
/// observación; <see cref="AlCerrarArea"/> cuando todas las áreas de la etapa se pronunciaron.
/// </summary>
public enum PoliticaDevolucion
{
    Inmediata,
    AlCerrarArea
}

/// <summary>
/// Cómo participa un área en una etapa. <see cref="Automatica"/>: aprueba sola al activarse la etapa (origen
/// <c>Sistema</c>); no puede rechazar y cuenta como un solo aprobador.
/// </summary>
public sealed record AreaDeEtapa(Guid AreaId, int AprobadoresRequeridos, bool PuedeRechazarExpediente, bool Automatica, int? SlaHoras);

/// <summary>Las áreas de una etapa revisan en paralelo; las etapas van en secuencia por <see cref="Orden"/>.</summary>
public sealed record EtapaDeFlujo(int Orden, IReadOnlyList<AreaDeEtapa> Areas);

/// <summary>Contenido versionado de un flujo (lo que se congela al publicar).</summary>
public sealed record DefinicionDeFlujo(IReadOnlyList<EtapaDeFlujo> Etapas, PoliticaDevolucion PoliticaDevolucion, bool SeparacionDeFunciones)
{
    public const int MaximoAprobadores = 10;

    /// <summary>Por defecto (P-4-4): una sola etapa con todas las áreas activas en paralelo, un aprobador cada una.</summary>
    public static DefinicionDeFlujo TodasEnParalelo(IEnumerable<Area> areas, PoliticaDevolucion politica = PoliticaDevolucion.Inmediata,
        bool separacionDeFunciones = false) =>
        new([new EtapaDeFlujo(1, [.. areas.Where(a => a.Activa).OrderBy(a => a.Codigo).Select(a => new AreaDeEtapa(a.Id, 1, false, false, null))])],
            politica, separacionDeFunciones);

    /// <summary>
    /// Valida contra las áreas de la empresa (deben existir, estar activas y ser de <paramref name="empresaId"/>) y
    /// normaliza: etapas ordenadas y numeradas 1..n; un área automática cuenta como un aprobador.
    /// </summary>
    public DefinicionDeFlujo Validar(Guid empresaId, IReadOnlyCollection<Area> areasDeLaEmpresa)
    {
        if (Etapas is null || Etapas.Count == 0)
            throw ReglaDeNegocioException.Validacion("etapas-requeridas", "El flujo necesita al menos una etapa con un área.");

        var repetido = Etapas.GroupBy(e => e.Orden).FirstOrDefault(g => g.Count() > 1);
        if (repetido is not null)
            throw ReglaDeNegocioException.Validacion("orden-duplicado", $"Hay dos etapas con el orden {repetido.Key}.");

        var vistas = new HashSet<Guid>();
        var etapas = new List<EtapaDeFlujo>();
        foreach (var (etapa, indice) in Etapas.OrderBy(e => e.Orden).Select((e, i) => (e, i)))
        {
            if (etapa.Areas is null || etapa.Areas.Count == 0)
                throw ReglaDeNegocioException.Validacion("etapa-sin-areas", $"La etapa {etapa.Orden} no tiene áreas.");

            var areas = new List<AreaDeEtapa>();
            foreach (var area in etapa.Areas)
            {
                if (!vistas.Add(area.AreaId))
                    throw ReglaDeNegocioException.Validacion("area-repetida", $"El área {area.AreaId} aparece más de una vez en el flujo.");

                var existente = areasDeLaEmpresa.FirstOrDefault(a => a.Id == area.AreaId);
                if (existente is null || existente.EmpresaId != empresaId || !existente.Activa)
                    throw ReglaDeNegocioException.Validacion("area-invalida",
                        $"El área {area.AreaId} no existe en la empresa o está inactiva.");

                if (area.SlaHoras is <= 0)
                    throw ReglaDeNegocioException.Validacion("sla-invalido", $"El SLA del área {existente.Codigo} debe ser positivo.");

                if (area.Automatica)
                {
                    if (area.PuedeRechazarExpediente)
                        throw ReglaDeNegocioException.Validacion("area-automatica-no-rechaza",
                            $"El área {existente.Codigo} es automática: no puede rechazar expedientes.");
                    areas.Add(area with { AprobadoresRequeridos = 1 });
                    continue;
                }

                if (area.AprobadoresRequeridos is < 1 or > MaximoAprobadores)
                    throw ReglaDeNegocioException.Validacion("aprobadores-invalidos",
                        $"El área {existente.Codigo} necesita entre 1 y {MaximoAprobadores} aprobadores.");
                areas.Add(area);
            }

            etapas.Add(new EtapaDeFlujo(indice + 1, areas));
        }

        return this with { Etapas = etapas };
    }
}

/// <summary>Versión de un flujo. Publicada es inmutable; <see cref="EstadoFlujoVersion.Retirada"/> cuando otra la reemplaza.</summary>
public sealed class FlujoVersion
{
    public Guid Id { get; set; }

    public int Version { get; set; }

    public EstadoFlujoVersion Estado { get; set; }

    public List<EtapaDeFlujo> Etapas { get; set; } = [];

    public PoliticaDevolucion PoliticaDevolucion { get; set; }

    public bool SeparacionDeFunciones { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset? PublicadaEn { get; set; }

    public DateTimeOffset? RetiradaEn { get; set; }

    public DefinicionDeFlujo Definicion() => new(Etapas, PoliticaDevolucion, SeparacionDeFunciones);
}

/// <summary>
/// Flujo aprobatorio de una empresa para un tipo de vínculo (docs/03 §12–13). Configuración versionada (esquema
/// <c>validacion</c>, multi-tenant, aislada por <see cref="EmpresaId"/>): publicar congela la versión; editar trabaja
/// sobre un borrador que nace como la versión siguiente. Los expedientes copian la versión publicada al abrirse.
/// </summary>
public sealed class Flujo
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    /// <summary>Mayúsculas, único en la empresa e inmutable.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public Guid TipoVinculoId { get; set; }

    public List<FlujoVersion> Versiones { get; set; } = [];

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    [JsonIgnore]
    public FlujoVersion? VersionPublicada => Versiones.FirstOrDefault(v => v.Estado == EstadoFlujoVersion.Publicada);

    [JsonIgnore]
    public FlujoVersion? Borrador => Versiones.FirstOrDefault(v => v.Estado == EstadoFlujoVersion.Borrador);

    /// <summary>La versión más reciente (borrador o la última publicada): base de una edición parcial.</summary>
    [JsonIgnore]
    public FlujoVersion Ultima => Versiones.OrderByDescending(v => v.Version).First();

    /// <summary>Crea el flujo con su primera versión en borrador.</summary>
    public static Flujo Crear(Guid empresaId, string codigo, string nombre, Guid tipoVinculoId, DefinicionDeFlujo definicion,
        IReadOnlyCollection<Area> areasDeLaEmpresa, DateTimeOffset ahora)
    {
        var codigoNormalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (!Regex.IsMatch(codigoNormalizado, "^[A-Z][A-Z0-9_]{1,39}$"))
            throw ReglaDeNegocioException.Validacion("codigo-invalido",
                "El código del flujo debe empezar con letra y usar solo mayúsculas, números y guion bajo (máx. 40).");

        var flujo = new Flujo
        {
            Id = Guid.CreateVersion7(),
            EmpresaId = empresaId,
            Codigo = codigoNormalizado,
            Nombre = ValidarNombre(nombre),
            TipoVinculoId = tipoVinculoId,
            CreadoEn = ahora,
            ActualizadoEn = ahora
        };
        flujo.Versiones.Add(NuevaVersion(1, definicion.Validar(empresaId, areasDeLaEmpresa), ahora));
        return flujo;
    }

    /// <summary>
    /// Edita el borrador; si no hay, crea uno nuevo (versión siguiente a la mayor). Lo publicado no cambia nunca.
    /// </summary>
    public FlujoVersion EditarBorrador(string? nombre, DefinicionDeFlujo definicion, IReadOnlyCollection<Area> areasDeLaEmpresa,
        DateTimeOffset ahora)
    {
        var validada = definicion.Validar(EmpresaId, areasDeLaEmpresa);
        if (nombre is not null)
            Nombre = ValidarNombre(nombre);

        var borrador = Borrador;
        if (borrador is null)
        {
            borrador = NuevaVersion(Versiones.Max(v => v.Version) + 1, validada, ahora);
            Versiones.Add(borrador);
        }
        else
        {
            borrador.Etapas = [.. validada.Etapas];
            borrador.PoliticaDevolucion = validada.PoliticaDevolucion;
            borrador.SeparacionDeFunciones = validada.SeparacionDeFunciones;
        }

        ActualizadoEn = ahora;
        return borrador;
    }

    /// <summary>
    /// Publica el borrador (vuelve a validar las áreas: pudieron desactivarse) y retira la publicada anterior. Los
    /// expedientes abiertos conservan la versión que copiaron.
    /// </summary>
    public FlujoVersion Publicar(IReadOnlyCollection<Area> areasDeLaEmpresa, DateTimeOffset ahora)
    {
        var borrador = Borrador
                       ?? throw ReglaDeNegocioException.Conflicto("sin-borrador", "No hay un borrador por publicar; edita el flujo primero.");
        var validada = borrador.Definicion().Validar(EmpresaId, areasDeLaEmpresa);

        if (VersionPublicada is { } anterior)
        {
            anterior.Estado = EstadoFlujoVersion.Retirada;
            anterior.RetiradaEn = ahora;
        }

        borrador.Etapas = [.. validada.Etapas];
        borrador.Estado = EstadoFlujoVersion.Publicada;
        borrador.PublicadaEn = ahora;
        ActualizadoEn = ahora;
        return borrador;
    }

    private static FlujoVersion NuevaVersion(int numero, DefinicionDeFlujo definicion, DateTimeOffset ahora) => new()
    {
        Id = Guid.CreateVersion7(),
        Version = numero,
        Estado = EstadoFlujoVersion.Borrador,
        Etapas = [.. definicion.Etapas],
        PoliticaDevolucion = definicion.PoliticaDevolucion,
        SeparacionDeFunciones = definicion.SeparacionDeFunciones,
        CreadaEn = ahora
    };

    private static string ValidarNombre(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre)
            ? throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del flujo es obligatorio.")
            : nombre.Trim();
}

/// <summary>
/// Flujo que se usa para <c>(empresa, tipo de vínculo)</c> cuando el vínculo no trae uno explícito (docs/03 §12.1). Su
/// id es <c>{empresaId:N}|{tipoVinculoId:N}</c>: hay uno solo por par.
/// </summary>
public sealed class FlujoPorDefecto
{
    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public Guid TipoVinculoId { get; set; }

    public Guid FlujoId { get; set; }

    public static string Clave(Guid empresaId, Guid tipoVinculoId) => $"{empresaId:N}|{tipoVinculoId:N}";
}
