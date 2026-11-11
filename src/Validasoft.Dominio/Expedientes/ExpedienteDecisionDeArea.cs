using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Expedientes;

/// <summary>Documento que «Aprobar área» decidiría: el requisito y su versión vigente (la que debió ver el revisor).</summary>
public sealed record DocumentoPorDecidir(Guid RequisitoId, int VersionVigente);

/// <summary>Un documento que el área observa en su decisión: la versión que vio el revisor, los motivos y la observación.</summary>
/// <remarks>En una sección de la ficha, <see cref="Campos"/> señala (opcional) los campos que el tercero debe corregir.</remarks>
public sealed record ObservacionDeRequisito(Guid RequisitoId, int? SobreVersion, IReadOnlyList<MotivoAplicado> Motivos, string? Observacion,
    IReadOnlyList<string>? Campos = null);

/// <summary>
/// Decisión por área con requisitos (DA-1…DA-4): en pantalla, como en el SRM, el área «aprueba» u «observa marcando
/// documentos» en un solo paso; por dentro se conserva el detalle por documento: un <see cref="VeredictoDeRequisitoEmitido"/>
/// por documento y versión, emitidos dentro de una misma simulación para que la derivación del área, la devolución al
/// tercero y el cierre se calculen una sola vez. Las reglas de cada documento son las del veredicto por documento (RQ-4).
/// </summary>
public sealed partial class Expediente
{
    /// <summary>
    /// Documentos del alcance del área con versión cargada, no eximidos, que el área aún no decidió y —si se indica
    /// <paramref name="revisorId"/>— que esa persona tampoco decidió: lo que «Aprobar área» le aprobaría.
    /// </summary>
    public IReadOnlyList<DocumentoPorDecidir> DocumentosPorDecidir(Guid areaId, string? revisorId = null)
    {
        if (!ConRequisitos || BuscarArea(areaId) is not { } area)
            return [];
        return
        [
            .. RequisitosDelArea(areaId)
                .Where(r => !r.Eximido && r.VersionActual is not null && !r.DecididoPor(area) &&
                            (revisorId is null || !r.DecidioPersona(areaId, revisorId)))
                .Select(r => new DocumentoPorDecidir(r.Id, r.VersionActual!.Numero))
        ];
    }

    /// <summary>
    /// «Aprobar área»: el revisor aprueba (origen <c>Humano</c>) todos los documentos que le quedan por decidir en el área
    /// (<see cref="DocumentosPorDecidir"/>), cada uno sobre la versión que vio (<paramref name="versiones"/>). Todo o
    /// nada: si falta la versión de alguno ⇒ 422 <c>versiones-requeridas</c>; si alguna ya no es la vigente ⇒ 409
    /// <c>version-superada</c> (ambos con <c>requisitos</c>). No aprueba encima de una observación vigente del área
    /// (409 <c>area-observada</c>) ni cuando no queda nada por decidir (409 <c>area-ya-se-pronuncio</c>). Las versiones de
    /// documentos que el área o la persona ya decidieron se ignoran (lectura anterior a otra decisión).
    /// </summary>
    public IReadOnlyList<object> AprobarArea(Guid areaId, Revisor revisor, IReadOnlyDictionary<Guid, int>? versiones, DateTimeOffset ahora,
        IReadOnlySet<Guid>? conDiscrepanciaCritica = null, string? justificacion = null)
    {
        var area = AreaParaDecidirPorRequisitos(areaId, revisor);
        var revisorId = revisor.UsuarioId.ToString();
        if (RequisitosDelArea(areaId).FirstOrDefault(r => r.ObservadoPor(areaId)) is { } observado)
            throw ReglaDeNegocioException.Conflicto("area-observada",
                $"El área {area.Codigo} observó {observado.Nombre}: no se aprueba encima de una observación vigente.");

        var pendientes = DocumentosPorDecidir(areaId, revisorId);
        if (pendientes.Count == 0)
            throw ReglaDeNegocioException.Conflicto("area-ya-se-pronuncio",
                $"No te quedan documentos por decidir en el área {area.Codigo}: ya los decidiste o el área ya los decidió.");

        var vistas = versiones ?? new Dictionary<Guid, int>();
        foreach (var requisitoId in vistas.Keys)
        {
            var requisito = BuscarRequisito(requisitoId)
                            ?? throw ReglaDeNegocioException.NoEncontrado("requisito-no-encontrado", "Ese requisito no está en el expediente.");
            if (!requisito.LoRevisa(areaId))
                throw ReglaDeNegocioException.Conflicto("requisito-no-en-alcance", $"El área {area.Codigo} no revisa {requisito.Nombre}.");
        }

        var faltantes = pendientes.Where(p => !vistas.ContainsKey(p.RequisitoId)).ToList();
        if (faltantes.Count > 0)
            throw new ReglaDeNegocioException(TipoFalla.Validacion, "versiones-requeridas",
                $"Indica la versión que revisaste de {Nombres(faltantes)} (versiones).") { Datos = DatosDeRequisitos(faltantes) };

        var superadas = pendientes.Where(p => vistas[p.RequisitoId] != p.VersionVigente).ToList();
        if (superadas.Count > 0)
            throw VersionSuperada(superadas);

        var conDiscrepancia = pendientes.Where(p => conDiscrepanciaCritica?.Contains(p.RequisitoId) == true).ToList();
        var texto = JustificacionPorDiscrepancias(conDiscrepancia.Select(p => BuscarRequisito(p.RequisitoId)!), justificacion);
        return Simular(s =>
        {
            foreach (var pendiente in pendientes)
                s.Emitir(VeredictoHumano(BuscarRequisito(pendiente.RequisitoId)!, area, Veredicto.Aprobado, revisorId, [], null, ahora,
                    justificacion: conDiscrepancia.Contains(pendiente) ? texto : null));
        }, ahora);
    }

    /// <summary>
    /// «Observar» el área marcando documentos: un <c>Observado</c> por cada documento listado (≥1 motivo y la versión
    /// vigente en cada uno), todos en un solo paso: una sola devolución al tercero y un solo correo con todas las
    /// observaciones. Los documentos no listados no se tocan. Cada documento sigue las reglas del veredicto por
    /// documento; las versiones superadas se informan juntas (409 <c>version-superada</c> con <c>requisitos</c>).
    /// </summary>
    public IReadOnlyList<object> ObservarArea(Guid areaId, Revisor revisor, IReadOnlyList<ObservacionDeRequisito>? observaciones, DateTimeOffset ahora)
    {
        var area = AreaParaDecidirPorRequisitos(areaId, revisor);
        var revisorId = revisor.UsuarioId.ToString();
        if (observaciones is not { Count: > 0 })
            throw ReglaDeNegocioException.Validacion("requisitos-requeridos",
                "Para observar el área indica al menos un documento con su versión y sus motivos (requisitos).");
        if (observaciones.GroupBy(o => o.RequisitoId).FirstOrDefault(g => g.Count() > 1) is { } repetido)
            throw ReglaDeNegocioException.Validacion("requisito-duplicado",
                $"El documento {BuscarRequisito(repetido.Key)?.Nombre ?? repetido.Key.ToString()} viene más de una vez.");

        var superadas = new List<DocumentoPorDecidir>();
        var eventos = new List<VeredictoDeRequisitoEmitido>();
        foreach (var observacion in observaciones)
        {
            var requisito = RequisitoDecidible(observacion.RequisitoId, area, revisorId, observacion.SobreVersion, superadas);
            eventos.Add(VeredictoHumano(requisito, area, Veredicto.Observado, revisorId, Motivos(Veredicto.Observado, observacion.Motivos),
                Texto(observacion.Observacion, "observacion"), ahora, Campos(requisito, Veredicto.Observado, observacion.Campos)));
        }

        if (superadas.Count > 0)
            throw VersionSuperada(superadas);

        return Simular(s =>
        {
            foreach (var evento in eventos)
                s.Emitir(evento);
        }, ahora);
    }

    // ---------- Reglas comunes al veredicto por documento y a la decisión por área ----------

    /// <summary>Expediente con requisitos en revisión, área de la etapa activa y no automática, revisor habilitado.</summary>
    private AreaDeExpediente AreaParaDecidirPorRequisitos(Guid areaId, Revisor revisor)
    {
        if (!ConRequisitos)
            throw ReglaDeNegocioException.Conflicto("sin-requisitos",
                "Este expediente no pide documentos: el área decide sobre el expediente completo (/areas/{areaId}/veredicto).");
        AsegurarEnRevision();
        var area = AreaActiva(areaId);
        if (area.Automatica)
            throw ReglaDeNegocioException.Conflicto("area-automatica", $"El área {area.Codigo} aprueba automáticamente.");
        AsegurarRevisorHabilitado(revisor);
        return area;
    }

    /// <summary>
    /// Un documento que esta persona puede decidir en el área: del alcance, no eximido, con versión, sobre la versión
    /// vigente, sin veredicto suyo vigente (<c>aprobador-repetido</c>) y aún no decidido por el área
    /// (<c>requisito-ya-decidido</c>). Con <paramref name="superadas"/> la versión desactualizada se acumula en vez de fallar.
    /// </summary>
    private RequisitoDeExpediente RequisitoDecidible(Guid requisitoId, AreaDeExpediente area, string revisorId, int? sobreVersion,
        List<DocumentoPorDecidir>? superadas)
    {
        var requisito = BuscarRequisito(requisitoId)
                        ?? throw ReglaDeNegocioException.NoEncontrado("requisito-no-encontrado", "Ese requisito no está en el expediente.");
        if (!requisito.LoRevisa(area.AreaId))
            throw ReglaDeNegocioException.Conflicto("requisito-no-en-alcance", $"El área {area.Codigo} no revisa {requisito.Nombre}.");
        if (requisito.Eximido)
            throw ReglaDeNegocioException.Conflicto("requisito-eximido", $"{requisito.Nombre} está eximido.");
        if (requisito.VersionActual is not { } version)
            throw ReglaDeNegocioException.Conflicto("requisito-sin-version", $"El tercero aún no ha cargado {requisito.Nombre}.");
        if (sobreVersion is not { } numero)
            throw ReglaDeNegocioException.Validacion("sobre-version-requerida",
                $"Indica sobre qué versión de {requisito.Nombre} decides (sobreVersion).");
        if (numero != version.Numero)
        {
            var superada = new DocumentoPorDecidir(requisito.Id, version.Numero);
            if (superadas is null)
                throw VersionSuperada([superada]);
            superadas.Add(superada);
        }

        if (requisito.DecidioPersona(area.AreaId, revisorId))
            throw ReglaDeNegocioException.Conflicto("aprobador-repetido",
                $"Ya emitiste tu veredicto sobre {requisito.Nombre} en el área {area.Codigo}; otra persona del área debe completar las aprobaciones.");
        if (requisito.DecididoPor(area))
            throw ReglaDeNegocioException.Conflicto("requisito-ya-decidido", $"El área {area.Codigo} ya decidió {requisito.Nombre} en esta ronda.");
        return requisito;
    }

    /// <summary>Veredicto humano sobre la versión vigente del requisito (ya validada).</summary>
    private static VeredictoDeRequisitoEmitido VeredictoHumano(RequisitoDeExpediente requisito, AreaDeExpediente area, Veredicto veredicto,
        string revisorId, IReadOnlyList<MotivoAplicado> motivos, string? observacion, DateTimeOffset ahora, IReadOnlyList<string>? campos = null,
        string? justificacion = null) =>
        new(Guid.CreateVersion7(), requisito.Id, area.AreaId, veredicto, OrigenVeredicto.Humano, revisorId, requisito.VersionActual!.Numero,
            [.. motivos.DistinctBy(m => m.Codigo)], observacion, justificacion, ahora, campos is { Count: > 0 } ? campos : null);

    /// <summary>
    /// Aprobar un requisito cuyo cruce con el documento discrepa en un campo crítico (identificación, razón social, representante
    /// legal, matrícula; FM-13) exige una justificación: el revisor la deja escrita junto al veredicto. Sin requisitos con
    /// discrepancia, nula. 422 <c>justificacion-por-discrepancias</c> con <c>requisitos</c>.
    /// </summary>
    private static string? JustificacionPorDiscrepancias(IEnumerable<RequisitoDeExpediente> conDiscrepancia, string? justificacion)
    {
        var requisitos = conDiscrepancia.ToList();
        if (requisitos.Count == 0)
            return null;
        var texto = Texto(justificacion, "justificacion");
        if (texto is null || texto.Length < LongitudMinimaJustificacion)
            throw new ReglaDeNegocioException(TipoFalla.Validacion, "justificacion-por-discrepancias",
                $"El cruce con el documento discrepa en campos críticos de {string.Join(", ", requisitos.Select(r => r.Nombre))}: para aprobar, " +
                $"escribe una justificación (al menos {LongitudMinimaJustificacion} caracteres) u observa el documento o la sección.")
            {
                Datos = new Dictionary<string, object?> { ["requisitos"] = requisitos.Select(r => r.Id).ToList() }
            };
        return texto;
    }

    /// <summary>Largo mínimo de la justificación para aprobar con discrepancias críticas.</summary>
    public const int LongitudMinimaJustificacion = 10;

    private ReglaDeNegocioException VersionSuperada(IReadOnlyList<DocumentoPorDecidir> superadas) =>
        new(TipoFalla.Conflicto, "version-superada",
            $"Cambió la versión vigente de {string.Join(", ", superadas.Select(s => $"{Nombre(s)} (ahora la {s.VersionVigente})"))}; " +
            "vuelve a abrir el expediente antes de decidir.") { Datos = DatosDeRequisitos(superadas) };

    private static Dictionary<string, object?> DatosDeRequisitos(IReadOnlyList<DocumentoPorDecidir> requisitos) => new() { ["requisitos"] = requisitos };

    private string Nombre(DocumentoPorDecidir documento) => BuscarRequisito(documento.RequisitoId)?.Nombre ?? documento.RequisitoId.ToString();

    private string Nombres(IEnumerable<DocumentoPorDecidir> documentos) => string.Join(", ", documentos.Select(Nombre));
}
