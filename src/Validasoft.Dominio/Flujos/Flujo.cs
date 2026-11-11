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
/// <see cref="Condicion"/> nula ⇒ se pide siempre; si no, solo al tercero y vínculo que la cumplen (RQ-12).
/// Con <see cref="Seccion"/> el requisito pide una sección de la ficha maestra (F2, FM-25) en vez de un documento:
/// <see cref="TipoDocumentoTenantId"/> va vacío y <see cref="CodigoTipo"/> es el código de la sección; <see cref="Periodos"/>
/// (solo la financiera, 1 por omisión = el último cierre) y <see cref="ExigeSoporte"/> (la versión debe traer soporte).
/// En una sección, <see cref="VigenciaDias"/> (opcional) es cuántos días vale lo que la empresa aceptó: pasados, la sección vence y
/// entra a la renovación del vínculo para que el tercero la ratifique o la actualice (VV-1); <see cref="DiasRevalidacion"/>
/// (opcional) es el plazo para resolver una actualización que la incluye (RV-4; en un documento lo da su tipo).
/// </summary>
public sealed record RequisitoDelFlujo(
    Guid Id, Guid TipoDocumentoTenantId, string CodigoTipo, bool Obligatorio, IReadOnlyList<Guid> AreasQueRevisan, int? AprobadoresRequeridos,
    CondicionDeExigencia? Condicion = null, string? Seccion = null, int? Periodos = null, bool ExigeSoporte = false, bool? UsarComoSoporte = null,
    int? VigenciaDias = null, int? DiasRevalidacion = null)
{
    [JsonIgnore]
    public bool EsSeccion => Seccion is not null;

    /// <summary>
    /// «Usar como soporte de las secciones» (F5, FM-13): un requisito de documento cuyo tipo canónico respalda secciones de la
    /// ficha (RUT, Cámara, estados financieros, cédula) también sirve de soporte de esas secciones y se cruza con ellas. Activa
    /// por omisión (<see cref="UsarComoSoporte"/> nulo); la empresa la apaga con <c>false</c>.
    /// </summary>
    public bool UsaComoSoporte(string? tipoCanonico) =>
        !EsSeccion && UsarComoSoporte != false && Ficha.MapaDeRespaldo.RespaldaAlguna(tipoCanonico);
}

/// <summary>Contenido versionado de un flujo (lo que se congela al publicar). Sin requisitos, cada área decide sobre el expediente (FL-1).</summary>
public sealed record DefinicionDeFlujo(
    IReadOnlyList<EtapaDeFlujo> Etapas,
    PoliticaDevolucion PoliticaDevolucion,
    bool SeparacionDeFunciones,
    IReadOnlyList<RequisitoDelFlujo>? Requisitos = null,
    IReadOnlyList<Expedientes.VerificacionRequeridaDelFlujo>? Verificaciones = null,
    PlazoDeCargue? PlazoCargue = null,
    ReintentoTrasRechazo? Reintento = null,
    VigenciaDelVinculo? VigenciaVinculo = null,
    int? RecordatorioObservacionesDias = null)
{
    public const int MaximoAprobadores = 10;

    /// <summary>Vigencia máxima configurable de una verificación (10 años).</summary>
    public const int MaximoVigenciaDias = 3650;

    public const int MaximoMesesReconsulta = 60;

    /// <summary>Plazo máximo configurable para resolver una revalidación (RV-4).</summary>
    public const int MaximoDiasRevalidacion = 365;

    /// <summary>Por defecto (P-4-4): una sola etapa con todas las áreas activas en paralelo, un aprobador cada una.</summary>
    public static DefinicionDeFlujo TodasEnParalelo(IEnumerable<Area> areas, PoliticaDevolucion politica = PoliticaDevolucion.Inmediata,
        bool separacionDeFunciones = false) =>
        new([new EtapaDeFlujo(1, [.. areas.Where(a => a.Activa).OrderBy(a => a.Codigo).Select(a => new AreaDeEtapa(a.Id, 1, false, false, null))])],
            politica, separacionDeFunciones);

    /// <summary>
    /// Valida contra las áreas de la empresa (deben existir, estar activas y ser de <paramref name="empresaId"/>) y
    /// normaliza: etapas ordenadas y numeradas 1..n; un área automática cuenta como un aprobador. Los requisitos se
    /// validan contra <paramref name="tiposDelTenant"/> (ver <see cref="ValidarRequisitos"/>) y sus condiciones contra los
    /// atributos de <paramref name="tipoVinculo"/>, el tipo de vínculo del flujo.
    /// </summary>
    public DefinicionDeFlujo Validar(Guid empresaId, IReadOnlyCollection<Area> areasDeLaEmpresa,
        IReadOnlyCollection<TipoDocumentoTenant>? tiposDelTenant = null, TipoVinculo? tipoVinculo = null)
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

        // Sin plazo indicado, el de P-4-2 (30 días, recordatorios a 15/7/1); 0 días es «sin plazo» (LT-1).
        var requisitos = ValidarRequisitos(etapas, tiposDelTenant ?? [], tipoVinculo);
        var vigencia = (VigenciaVinculo ?? VigenciaDelVinculo.Sin).Validar();
        // La vigencia periódica se renueva ratificando secciones de la ficha: sin ninguna no habría qué ratificar (VV-2).
        if (vigencia.Activa && !requisitos.Any(r => r.EsSeccion))
            throw ReglaDeNegocioException.Validacion("vigencia-sin-secciones",
                "La vigencia del vínculo se renueva ratificando las secciones de la ficha: el flujo debe pedir al menos una.");
        return this with
        {
            Etapas = etapas,
            Requisitos = requisitos,
            Verificaciones = ValidarVerificaciones(),
            PlazoCargue = (PlazoCargue ?? PlazoDeCargue.PorDefecto).Validar(),
            Reintento = (Reintento ?? ReintentoTrasRechazo.PorDefecto).Validar(),
            VigenciaVinculo = vigencia,
            RecordatorioObservacionesDias = ValidarRecordatorio(RecordatorioObservacionesDias)
        };
    }

    /// <summary>Recordatorio automático de observaciones del flujo (OP-19): nulo = el de la empresa; 0 = apagado; 1 a 60 = cada tantos días (422 <c>dias-invalidos</c>).</summary>
    public static int? ValidarRecordatorio(int? dias) =>
        dias is null or 0 ? dias : dias is >= 1 and <= 60 ? dias : throw ReglaDeNegocioException.Validacion("dias-invalidos", "El recordatorio automático sale cada 1 a 60 días.");

    /// <summary>
    /// Verificaciones externas que pide el flujo (VE-6): una por tipo (422 <c>verificacion-duplicada</c>), vigencia de 1 a
    /// <see cref="MaximoVigenciaDias"/> días y reconsulta de 1 a <see cref="MaximoMesesReconsulta"/> meses
    /// (<c>vigencia-invalida</c>, <c>reconsulta-invalida</c>; la reconsulta solo aplica a listas: <c>reconsulta-no-aplica</c>).
    /// </summary>
    private IReadOnlyList<Expedientes.VerificacionRequeridaDelFlujo> ValidarVerificaciones()
    {
        if (Verificaciones is null || Verificaciones.Count == 0)
            return [];

        var resultado = new List<Expedientes.VerificacionRequeridaDelFlujo>();
        foreach (var verificacion in Verificaciones)
        {
            if (!Enum.IsDefined(verificacion.Tipo))
                throw ReglaDeNegocioException.Validacion("verificacion-invalida", "Tipo de verificación desconocido.");
            if (resultado.Any(v => v.Tipo == verificacion.Tipo))
                throw ReglaDeNegocioException.Validacion("verificacion-duplicada", $"El flujo ya pide {verificacion.Tipo}.");
            if (verificacion.VigenciaDias is < 1 or > MaximoVigenciaDias)
                throw ReglaDeNegocioException.Validacion("vigencia-invalida",
                    $"La vigencia de {verificacion.Tipo} va de 1 a {MaximoVigenciaDias} días (vacía = la de la configuración).");
            if (verificacion.FrecuenciaReconsultaMeses is not null && verificacion.Tipo != Expedientes.TipoVerificacion.ListasRestrictivas)
                throw ReglaDeNegocioException.Validacion("reconsulta-no-aplica", "La frecuencia de reconsulta solo aplica a las listas restrictivas.");
            if (verificacion.FrecuenciaReconsultaMeses is < 1 or > MaximoMesesReconsulta)
                throw ReglaDeNegocioException.Validacion("reconsulta-invalida",
                    $"La reconsulta de listas va de 1 a {MaximoMesesReconsulta} meses (vacía = 12).");
            resultado.Add(verificacion);
        }

        return resultado;
    }

    /// <summary>
    /// Cada requisito: un tipo activo del tenant que el tercero pueda entregar (TD-4; 422 <c>requisito-invalido</c> /
    /// <c>clase-no-soportada</c>), sin repetir tipo ni id (<c>requisito-duplicado</c>), revisado por áreas del flujo
    /// (<c>requisito-area-invalida</c>) y con 1–10 aprobadores si se indican. Con requisitos, toda área del flujo debe
    /// revisar al menos uno (<c>area-sin-requisitos</c>): su aprobación se deriva de ellos (RQ-4). Cada condición se valida
    /// (<c>condicion-invalida</c>) y al menos un requisito se pide siempre (<c>requisito-siempre-requerido</c>, RQ-12): así
    /// ningún tercero cae en un expediente sin requisitos, donde las áreas decidirían sobre el expediente completo.
    /// </summary>
    private IReadOnlyList<RequisitoDelFlujo> ValidarRequisitos(IReadOnlyList<EtapaDeFlujo> etapas, IReadOnlyCollection<TipoDocumentoTenant> tipos,
        TipoVinculo? tipoVinculo)
    {
        if (Requisitos is null || Requisitos.Count == 0)
            return [];

        var areasDelFlujo = etapas.SelectMany(e => e.Areas).Select(a => a.AreaId).ToHashSet();
        var tiposVistos = new HashSet<Guid>();
        var seccionesVistas = new HashSet<string>(StringComparer.Ordinal);
        var idsVistos = new HashSet<Guid>();
        var resultado = new List<RequisitoDelFlujo>();
        var indice = -1;
        foreach (var requisito in Requisitos)
        {
            indice++;
            try
            {
                if (requisito.EsSeccion)
                {
                    resultado.Add(ValidarSeccion(requisito, areasDelFlujo, seccionesVistas, idsVistos, tipoVinculo));
                    continue;
                }

                if (requisito.VigenciaDias is not null || requisito.DiasRevalidacion is not null)
                    throw ReglaDeNegocioException.Validacion("vigencia-no-aplica",
                        "La vigencia y el plazo de revalidación se configuran en el tipo de documento; en el flujo solo valen para las secciones de la ficha.");

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

                // «Usar como soporte» (FM-13) solo tiene sentido si el tipo respalda alguna sección; se guarda solo el «no» explícito.
                if (requisito.UsarComoSoporte == true && !Ficha.MapaDeRespaldo.RespaldaAlguna(tipo.TipoCanonico))
                    throw ReglaDeNegocioException.Validacion("soporte-no-aplica",
                        $"{tipo.Nombre} no respalda ninguna sección de la ficha: no se puede usar como soporte.");

                var condicion = AtributosDeExigencia.Validar(requisito.Condicion, tipoVinculo, tipo.Nombre);
                resultado.Add(requisito with
                {
                    Id = id, CodigoTipo = tipo.Codigo, AreasQueRevisan = areas, Condicion = condicion,
                    UsarComoSoporte = Ficha.MapaDeRespaldo.RespaldaAlguna(tipo.TipoCanonico) && requisito.UsarComoSoporte == false ? false : null
                });
            }
            catch (ReglaDeNegocioException ex) when (ex.Tipo == TipoFalla.Validacion && ex.Datos?.ContainsKey("requisito") != true)
            {
                // El 422 dice en qué requisito está el problema (su posición en el flujo), para mostrarlo en su campo.
                throw ConRequisito(ex, indice);
            }
        }

        if (resultado.All(r => r.Condicion is not null))
            throw ReglaDeNegocioException.Validacion("requisito-siempre-requerido",
                "Al menos un documento del flujo debe pedirse siempre (sin condición).");

        var sinRequisitos = areasDelFlujo.FirstOrDefault(a => !resultado.Any(r => r.AreasQueRevisan.Count == 0 || r.AreasQueRevisan.Contains(a)));
        if (sinRequisitos != Guid.Empty)
            throw ReglaDeNegocioException.Validacion("area-sin-requisitos",
                $"El área {sinRequisitos} no revisa ningún requisito: con requisitos, cada área aprueba los de su alcance.");

        return resultado;
    }

    private static ReglaDeNegocioException ConRequisito(ReglaDeNegocioException ex, int indice)
    {
        var datos = new Dictionary<string, object?>(ex.Datos ?? new Dictionary<string, object?>()) { ["requisito"] = indice };
        return new ReglaDeNegocioException(ex.Tipo, ex.Codigo, ex.Message) { Datos = datos };
    }

    /// <summary>
    /// Requisito de sección (F2, FM-25): una sección canónica de la ficha (422 <c>seccion-invalida</c>), sin repetir
    /// (<c>requisito-duplicado</c>), periodos solo en la financiera (<c>periodos-no-aplica</c>) y entre 1 y
    /// <see cref="Expedientes.RequisitosDeSeccion.MaximoPeriodos"/> (<c>periodos-invalidos</c>); áreas, aprobadores y condición
    /// como en un documento.
    /// </summary>
    private static RequisitoDelFlujo ValidarSeccion(RequisitoDelFlujo requisito, IReadOnlySet<Guid> areasDelFlujo, HashSet<string> vistas,
        HashSet<Guid> idsVistos, TipoVinculo? tipoVinculo)
    {
        var definicion = Ficha.CatalogoDeSecciones.Buscar(requisito.Seccion)
                         ?? throw ReglaDeNegocioException.Validacion("seccion-invalida",
                             $"La sección «{requisito.Seccion}» no existe. Secciones: {string.Join(", ", Ficha.CatalogoDeSecciones.Todas.Select(s => s.Codigo))}.");
        if (!vistas.Add(definicion.Codigo))
            throw ReglaDeNegocioException.Validacion("requisito-duplicado", $"El flujo ya pide {definicion.Codigo}.");
        if (requisito.Periodos is not null && !definicion.PorPeriodo)
            throw ReglaDeNegocioException.Validacion("periodos-no-aplica", $"{definicion.Nombre} no va por periodo.");
        if (requisito.UsarComoSoporte is not null)
            throw ReglaDeNegocioException.Validacion("soporte-no-aplica", "«Usar como soporte» es de los requisitos de documento, no de las secciones.");
        if (requisito.Periodos is < 1 or > Expedientes.RequisitosDeSeccion.MaximoPeriodos)
            throw ReglaDeNegocioException.Validacion("periodos-invalidos",
                $"Se piden entre 1 y {Expedientes.RequisitosDeSeccion.MaximoPeriodos} periodos de {definicion.Nombre}.");
        if (requisito.VigenciaDias is < 1 or > VigenciaDelVinculo.DiasMaximos)
            throw ReglaDeNegocioException.Validacion("vigencia-seccion-invalida",
                $"La vigencia de {definicion.Nombre} va de 1 a {VigenciaDelVinculo.DiasMaximos} días (vacía = no vence).");
        if (requisito.DiasRevalidacion is < 1 or > MaximoDiasRevalidacion)
            throw ReglaDeNegocioException.Validacion("dias-revalidacion-invalidos",
                $"El plazo de revalidación de {definicion.Nombre} va de 1 a {MaximoDiasRevalidacion} días (vacío = el del flujo, 15).");

        var id = requisito.Id == Guid.Empty ? Guid.CreateVersion7() : requisito.Id;
        if (!idsVistos.Add(id))
            throw ReglaDeNegocioException.Validacion("requisito-duplicado", $"Hay dos requisitos con el id {id}.");

        var areas = (requisito.AreasQueRevisan ?? []).Distinct().ToList();
        if (areas.Any(a => !areasDelFlujo.Contains(a)))
            throw ReglaDeNegocioException.Validacion("requisito-area-invalida", $"Un área que revisa {definicion.Codigo} no participa en el flujo.");
        if (requisito.AprobadoresRequeridos is < 1 or > MaximoAprobadores)
            throw ReglaDeNegocioException.Validacion("aprobadores-invalidos",
                $"El requisito {definicion.Codigo} necesita entre 1 y {MaximoAprobadores} aprobadores.");

        var condicion = AtributosDeExigencia.Validar(requisito.Condicion, tipoVinculo, definicion.Nombre);
        return requisito with
        {
            Id = id, TipoDocumentoTenantId = Guid.Empty, CodigoTipo = definicion.Codigo, Seccion = definicion.Codigo,
            Periodos = definicion.PorPeriodo ? requisito.Periodos ?? 1 : null, AreasQueRevisan = areas, Condicion = condicion
        };
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

    /// <summary>Verificaciones externas que pide esta versión (VE-6): listas restrictivas y estudio de cumplimiento.</summary>
    public List<Expedientes.VerificacionRequeridaDelFlujo> Verificaciones { get; set; } = [];
    /// <summary>
    /// Plazo del tercero para completar el expediente inicial (LT-1). Nulo en las versiones guardadas antes: vale el de
    /// P-4-2 (<see cref="PlazoDeCargue.PorDefecto"/>).
    /// </summary>
    public PlazoDeCargue? PlazoCargue { get; set; }

    /// <summary>El plazo que rige (el guardado o el de P-4-2).</summary>
    public PlazoDeCargue PlazoEfectivo() => PlazoCargue ?? PlazoDeCargue.PorDefecto;

    /// <summary>
    /// Si el tercero rechazado puede volver a postularse por su cuenta y desde cuándo (DS-4). Nulo en las versiones guardadas
    /// antes: vale <see cref="ReintentoTrasRechazo.PorDefecto"/> (no se permite).
    /// </summary>
    public ReintentoTrasRechazo? Reintento { get; set; }

    public ReintentoTrasRechazo ReintentoEfectivo() => Reintento ?? ReintentoTrasRechazo.PorDefecto;

    /// <summary>
    /// Vigencia periódica del vínculo (VV-1). Nula en las versiones guardadas antes: sin vencimiento periódico.
    /// </summary>
    public VigenciaDelVinculo? VigenciaVinculo { get; set; }

    /// <summary>La vigencia que rige (la guardada o «sin vencimiento periódico»).</summary>
    public VigenciaDelVinculo VigenciaEfectiva() => VigenciaVinculo ?? VigenciaDelVinculo.Sin;

    /// <summary>Recordatorio automático de observaciones de este flujo (OP-19): nulo = el de la empresa; 0 = apagado; 1 a 60 = cada tantos días.</summary>
    public int? RecordatorioObservacionesDias { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset? PublicadaEn { get; set; }

    public DateTimeOffset? RetiradaEn { get; set; }

    public DefinicionDeFlujo Definicion() =>
        new(Etapas, PoliticaDevolucion, SeparacionDeFunciones, Requisitos, Verificaciones, PlazoEfectivo(), ReintentoEfectivo(), VigenciaEfectiva(), RecordatorioObservacionesDias);
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
        IReadOnlyCollection<Area> areasDeLaEmpresa, DateTimeOffset ahora, IReadOnlyCollection<TipoDocumentoTenant>? tiposDelTenant = null,
        TipoVinculo? tipoVinculo = null)
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
        flujo.Versiones.Add(NuevaVersion(1, definicion.Validar(empresaId, areasDeLaEmpresa, tiposDelTenant, tipoVinculo), ahora));
        return flujo;
    }

    /// <summary>
    /// Edita el borrador; si no hay, crea uno nuevo (versión siguiente a la mayor). Lo publicado no cambia nunca.
    /// </summary>
    public FlujoVersion EditarBorrador(string? nombre, DefinicionDeFlujo definicion, IReadOnlyCollection<Area> areasDeLaEmpresa,
        DateTimeOffset ahora, IReadOnlyCollection<TipoDocumentoTenant>? tiposDelTenant = null, TipoVinculo? tipoVinculo = null)
    {
        var validada = definicion.Validar(EmpresaId, areasDeLaEmpresa, tiposDelTenant, tipoVinculo);
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
            borrador.Verificaciones = [.. validada.Verificaciones ?? []];
            borrador.PlazoCargue = validada.PlazoCargue;
            borrador.Reintento = validada.Reintento;
            borrador.VigenciaVinculo = validada.VigenciaVinculo;
            borrador.RecordatorioObservacionesDias = validada.RecordatorioObservacionesDias;
        }

        ActualizadoEn = ahora;
        return borrador;
    }

    /// <summary>
    /// Publica el borrador (vuelve a validar áreas y tipos de documento: pudieron desactivarse) y retira la publicada
    /// anterior. Los requisitos quedan congelados con la versión. Los expedientes abiertos conservan la versión que copiaron.
    /// </summary>
    public FlujoVersion Publicar(IReadOnlyCollection<Area> areasDeLaEmpresa, DateTimeOffset ahora,
        IReadOnlyCollection<TipoDocumentoTenant>? tiposDelTenant = null, TipoVinculo? tipoVinculo = null)
    {
        var borrador = Borrador
                       ?? throw ReglaDeNegocioException.Conflicto("sin-borrador", "No hay un borrador por publicar; edita el flujo primero.");
        var validada = borrador.Definicion().Validar(EmpresaId, areasDeLaEmpresa, tiposDelTenant, tipoVinculo);

        if (VersionPublicada is { } anterior)
        {
            anterior.Estado = EstadoFlujoVersion.Retirada;
            anterior.RetiradaEn = ahora;
        }

        borrador.Etapas = [.. validada.Etapas];
        borrador.Requisitos = [.. validada.Requisitos ?? []];
        borrador.Verificaciones = [.. validada.Verificaciones ?? []];
        borrador.PlazoCargue = validada.PlazoCargue;
        borrador.Reintento = validada.Reintento;
        borrador.VigenciaVinculo = validada.VigenciaVinculo;
        borrador.RecordatorioObservacionesDias = validada.RecordatorioObservacionesDias;
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
        Verificaciones = [.. definicion.Verificaciones ?? []],
        PlazoCargue = definicion.PlazoCargue,
        Reintento = definicion.Reintento,
        VigenciaVinculo = definicion.VigenciaVinculo,
        RecordatorioObservacionesDias = definicion.RecordatorioObservacionesDias,
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
