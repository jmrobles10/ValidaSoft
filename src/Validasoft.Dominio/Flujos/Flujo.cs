using System.Text.Json.Serialization;
using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;

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

/// <summary>
/// Documento que pide el flujo (RQ-1): un tipo del tenant, obligatorio u opcional, y las áreas que lo revisan.
/// <see cref="AreasQueRevisan"/> vacío ⇒ lo revisan todas las áreas del flujo, cada una en su etapa.
/// <see cref="AprobadoresRequeridos"/> nulo ⇒ los del área. <see cref="CodigoTipo"/> es copia del código del tipo
/// (inmutable) para leer el flujo sin cruzarlo con el catálogo. Lo que el flujo pide decide qué se comparte (RQ-3).
/// </summary>
public sealed record RequisitoDelFlujo(
    Guid Id, Guid TipoDocumentoTenantId, string CodigoTipo, bool Obligatorio, IReadOnlyList<Guid> AreasQueRevisan, int? AprobadoresRequeridos);

/// <summary>Contenido versionado de un flujo (lo que se congela al publicar). Sin requisitos, cada área decide sobre el expediente (FL-1).</summary>
public sealed record DefinicionDeFlujo(
    IReadOnlyList<EtapaDeFlujo> Etapas,
    PoliticaDevolucion PoliticaDevolucion,
    bool SeparacionDeFunciones,
    IReadOnlyList<RequisitoDelFlujo>? Requisitos = null)
{
    public const int MaximoAprobadores = 10;

    /// <summary>Por defecto (P-4-4): una sola etapa con todas las áreas activas en paralelo, un aprobador cada una.</summary>
    public static DefinicionDeFlujo TodasEnParalelo(IEnumerable<Area> areas, PoliticaDevolucion politica = PoliticaDevolucion.Inmediata,
        bool separacionDeFunciones = false) =>
        new([new EtapaDeFlujo(1, [.. areas.Where(a => a.Activa).OrderBy(a => a.Codigo).Select(a => new AreaDeEtapa(a.Id, 1, false, false, null))])],
            politica, separacionDeFunciones);

    /// <summary>
    /// Valida contra las áreas de la empresa (deben existir, estar activas y ser de <paramref name="empresaId"/>) y
    /// normaliza: etapas ordenadas y numeradas 1..n; un área automática cuenta como un aprobador. Los requisitos se
    /// validan contra <paramref name="tiposDelTenant"/> (ver <see cref="ValidarRequisitos"/>).
    /// </summary>
    public DefinicionDeFlujo Validar(Guid empresaId, IReadOnlyCollection<Area> areasDeLaEmpresa,
        IReadOnlyCollection<TipoDocumentoTenant>? tiposDelTenant = null)
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

        return this with { Etapas = etapas, Requisitos = ValidarRequisitos(etapas, tiposDelTenant ?? []) };
    }

    /// <summary>
    /// Cada requisito: un tipo activo del tenant que el tercero pueda entregar (TD-4; 422 <c>requisito-invalido</c> /
    /// <c>clase-no-soportada</c>), sin repetir tipo ni id (<c>requisito-duplicado</c>), revisado por áreas del flujo
    /// (<c>requisito-area-invalida</c>) y con 1–10 aprobadores si se indican. Con requisitos, toda área del flujo debe
    /// revisar al menos uno (<c>area-sin-requisitos</c>): su aprobación se deriva de ellos (RQ-4).
    /// </summary>
    private IReadOnlyList<RequisitoDelFlujo> ValidarRequisitos(IReadOnlyList<EtapaDeFlujo> etapas, IReadOnlyCollection<TipoDocumentoTenant> tipos)
    {
        if (Requisitos is null || Requisitos.Count == 0)
            return [];

        var areasDelFlujo = etapas.SelectMany(e => e.Areas).Select(a => a.AreaId).ToHashSet();
        var tiposVistos = new HashSet<Guid>();
        var idsVistos = new HashSet<Guid>();
        var resultado = new List<RequisitoDelFlujo>();
        foreach (var requisito in Requisitos)
        {
            var tipo = tipos.FirstOrDefault(t => t.Id == requisito.TipoDocumentoTenantId);
            if (tipo is not { Activo: true })
                throw ReglaDeNegocioException.Validacion("requisito-invalido",
                    $"El tipo de documento {(string.IsNullOrWhiteSpace(requisito.CodigoTipo) ? requisito.TipoDocumentoTenantId.ToString() : requisito.CodigoTipo)} no existe en el tenant o está inactivo.");
            if (!tipo.CargaSoportada)
                throw ReglaDeNegocioException.Validacion("clase-no-soportada",
                    $"{tipo.Codigo} es un formulario sin campos (o global); defínele campos y déjalo PorVinculo para pedirlo en un flujo.");
            if (!tiposVistos.Add(tipo.Id))
                throw ReglaDeNegocioException.Validacion("requisito-duplicado", $"El flujo ya pide {tipo.Codigo}.");

            var id = requisito.Id == Guid.Empty ? Guid.CreateVersion7() : requisito.Id;
            if (!idsVistos.Add(id))
                throw ReglaDeNegocioException.Validacion("requisito-duplicado", $"Hay dos requisitos con el id {id}.");

            var areas = (requisito.AreasQueRevisan ?? []).Distinct().ToList();
            if (areas.Any(a => !areasDelFlujo.Contains(a)))
                throw ReglaDeNegocioException.Validacion("requisito-area-invalida", $"Un área que revisa {tipo.Codigo} no participa en el flujo.");
            if (requisito.AprobadoresRequeridos is < 1 or > MaximoAprobadores)
                throw ReglaDeNegocioException.Validacion("aprobadores-invalidos",
                    $"El requisito {tipo.Codigo} necesita entre 1 y {MaximoAprobadores} aprobadores.");

            resultado.Add(requisito with { Id = id, CodigoTipo = tipo.Codigo, AreasQueRevisan = areas });
        }

        var sinRequisitos = areasDelFlujo.FirstOrDefault(a => !resultado.Any(r => r.AreasQueRevisan.Count == 0 || r.AreasQueRevisan.Contains(a)));
        if (sinRequisitos != Guid.Empty)
            throw ReglaDeNegocioException.Validacion("area-sin-requisitos",
                $"El área {sinRequisitos} no revisa ningún requisito: con requisitos, cada área aprueba los de su alcance.");

        return resultado;
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

    /// <summary>Documentos que pide esta versión (RQ-1); vacío ⇒ cada área decide sobre el expediente completo (FL-1).</summary>
    public List<RequisitoDelFlujo> Requisitos { get; set; } = [];

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset? PublicadaEn { get; set; }

    public DateTimeOffset? RetiradaEn { get; set; }

    public DefinicionDeFlujo Definicion() => new(Etapas, PoliticaDevolucion, SeparacionDeFunciones, Requisitos);
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
        IReadOnlyCollection<Area> areasDeLaEmpresa, DateTimeOffset ahora, IReadOnlyCollection<TipoDocumentoTenant>? tiposDelTenant = null)
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
        flujo.Versiones.Add(NuevaVersion(1, definicion.Validar(empresaId, areasDeLaEmpresa, tiposDelTenant), ahora));
        return flujo;
    }

    /// <summary>
    /// Edita el borrador; si no hay, crea uno nuevo (versión siguiente a la mayor). Lo publicado no cambia nunca.
    /// </summary>
    public FlujoVersion EditarBorrador(string? nombre, DefinicionDeFlujo definicion, IReadOnlyCollection<Area> areasDeLaEmpresa,
        DateTimeOffset ahora, IReadOnlyCollection<TipoDocumentoTenant>? tiposDelTenant = null)
    {
        var validada = definicion.Validar(EmpresaId, areasDeLaEmpresa, tiposDelTenant);
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
            borrador.Requisitos = [.. validada.Requisitos ?? []];
        }

        ActualizadoEn = ahora;
        return borrador;
    }

    /// <summary>
    /// Publica el borrador (vuelve a validar áreas y tipos de documento: pudieron desactivarse) y retira la publicada
    /// anterior. Los requisitos quedan congelados con la versión. Los expedientes abiertos conservan la versión que copiaron.
    /// </summary>
    public FlujoVersion Publicar(IReadOnlyCollection<Area> areasDeLaEmpresa, DateTimeOffset ahora,
        IReadOnlyCollection<TipoDocumentoTenant>? tiposDelTenant = null)
    {
        var borrador = Borrador
                       ?? throw ReglaDeNegocioException.Conflicto("sin-borrador", "No hay un borrador por publicar; edita el flujo primero.");
        var validada = borrador.Definicion().Validar(EmpresaId, areasDeLaEmpresa, tiposDelTenant);

        if (VersionPublicada is { } anterior)
        {
            anterior.Estado = EstadoFlujoVersion.Retirada;
            anterior.RetiradaEn = ahora;
        }

        borrador.Etapas = [.. validada.Etapas];
        borrador.Requisitos = [.. validada.Requisitos ?? []];
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
        Requisitos = [.. definicion.Requisitos ?? []],
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
