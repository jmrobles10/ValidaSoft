using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Expedientes;

/// <summary>Coincidencia de una verificación de listas con su decisión (si ya la hay).</summary>
public sealed class CoincidenciaDeVerificacion
{
    public Guid Id { get; set; }

    public string ClavePersona { get; set; } = string.Empty;

    public string NombrePersona { get; set; } = string.Empty;

    public string Rol { get; set; } = string.Empty;

    public string Lista { get; set; } = string.Empty;

    public string NombreEncontrado { get; set; } = string.Empty;

    public string? Detalle { get; set; }

    public decimal? Puntaje { get; set; }

    public ResolucionDeCoincidencia? Resolucion { get; set; }

    /// <summary>La decisión viene de una consulta anterior (sigue vigente).</summary>
    public bool Heredada { get; set; }

    [JsonIgnore]
    public bool Resuelta => Resolucion is not null;

    /// <summary>Misma persona, lista y nombre encontrado: es "la misma" coincidencia en otra consulta.</summary>
    public bool EsLaMisma(string clavePersona, string lista, string nombreEncontrado) =>
        string.Equals(ClavePersona, clavePersona, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(Lista, lista, StringComparison.OrdinalIgnoreCase) &&
        string.Equals(NombreEncontrado, nombreEncontrado, StringComparison.OrdinalIgnoreCase);
}

/// <summary>Consulta anterior reemplazada por una reconsulta (su evidencia sigue descargable).</summary>
public sealed record ConsultaAnterior(string Proveedor, string ReferenciaExterna, ResultadoVerificacion Resultado, DateTimeOffset Fecha,
    EvidenciaDeVerificacion? Evidencia, int Coincidencias);

/// <summary>Estudio registrado por el oficial.</summary>
public sealed record EstudioRegistrado(DatosDeEstudio Datos, bool VerificadoEnListas, string Por, DateTimeOffset Fecha);

/// <summary>Una verificación del expediente (docs/03 §6.2 <c>Verificaciones[]</c>).</summary>
public sealed class VerificacionDeExpediente
{
    public TipoVerificacion Tipo { get; set; }

    public bool Bloqueante { get; set; }

    /// <summary>La pidió el flujo (si no, alguien la lanzó a mano y no bloquea).</summary>
    public bool RequeridaPorElFlujo { get; set; }

    public int? VigenciaDias { get; set; }

    public int? FrecuenciaReconsultaMeses { get; set; }

    public EstadoVerificacion Estado { get; set; }

    public ResultadoVerificacion? Resultado { get; set; }

    public string? Proveedor { get; set; }

    public string? ReferenciaExterna { get; set; }

    public DateTimeOffset? RegistradaEn { get; set; }

    public string? RegistradaPor { get; set; }

    public DateTimeOffset? VigenteHasta { get; set; }

    public EvidenciaDeVerificacion? Evidencia { get; set; }

    public List<PersonaConsultada> Personas { get; set; } = [];

    public List<CoincidenciaDeVerificacion> Coincidencias { get; set; } = [];

    public EstudioRegistrado? Estudio { get; set; }

    public List<ConsultaAnterior> Anteriores { get; set; } = [];

    /// <summary>Consultas automáticas fallidas seguidas (se reinicia al registrar un resultado).</summary>
    public int Fallos { get; set; }

    public string? UltimoError { get; set; }

    public DateTimeOffset? ReintentarDesde { get; set; }

    public DateTimeOffset? ExpiroEn { get; set; }

    /// <summary>Cuenta para el cierre: satisfecha y dentro de su vigencia.</summary>
    public bool SatisfechaEn(DateTimeOffset ahora) => Estado == EstadoVerificacion.Satisfecha && (VigenteHasta is not { } hasta || hasta > ahora);

    /// <summary>Espera algo de Cumplimiento (o de una consulta): todo lo que no está satisfecho.</summary>
    [JsonIgnore]
    public bool RequiereAtencion => Estado != EstadoVerificacion.Satisfecha;
}

/// <summary>
/// Verificaciones externas del expediente (H7, VE-5…VE-12). El flujo dice cuáles pide; el expediente no se aprueba mientras
/// una bloqueante no esté <c>Satisfecha</c> y vigente (<see cref="VerificacionesSatisfechas"/>), aunque todas las áreas
/// hayan aprobado: queda <c>EnRevision</c> con todas sus etapas aprobadas (<see cref="EsperandoVerificaciones"/>) y se cierra
/// en cuanto la última se satisface. Cada decisión pasa por la misma cascada (<c>Evaluar</c>) que los veredictos.
/// </summary>
public sealed partial class Expediente
{
    /// <summary>Vigencia de una decisión sobre una coincidencia (docs/03 §6.3: +12 meses).</summary>
    public const int MesesVigenciaDecisionCoincidencia = 12;

    /// <summary>Reconsulta de listas por omisión (docs/05 <c>frecuenciaReconsultaMeses</c>).</summary>
    public const int MesesReconsultaPorOmision = 12;

    public List<VerificacionDeExpediente> Verificaciones { get; set; } = [];

    /// <summary>Cuándo debe correr la consulta automática de listas (nulo = ninguna pendiente). Lo lee el procesador.</summary>
    public DateTimeOffset? ProximaConsultaDeListas { get; set; }

    /// <summary>Primer vencimiento de una verificación satisfecha mientras el expediente sigue abierto (lo lee el procesador).</summary>
    public DateTimeOffset? ProximoVencimientoDeVerificacion { get; set; }

    /// <summary>Expediente abierto con alguna verificación que no está satisfecha (bandeja del oficial).</summary>
    public bool PendienteDeCumplimiento { get; set; }

    /// <summary>Tiene verificaciones (informe SAGRILAFT).</summary>
    public bool ConVerificaciones { get; set; }

    /// <summary>Todas las áreas aprobaron pero falta algo para cerrar: una verificación bloqueante o la cuenta designada (CB-4, <see cref="EsperandoCuentaDesignada"/>).</summary>
    [JsonIgnore]
    public bool EsperandoVerificaciones => Estado == EstadoExpediente.EnRevision && Etapas.Count > 0 && Etapas.All(e => e.Estado == EstadoEtapa.Aprobada);

    public VerificacionDeExpediente? BuscarVerificacion(TipoVerificacion tipo) => Verificaciones.FirstOrDefault(v => v.Tipo == tipo);

    /// <summary>Todas las verificaciones bloqueantes satisfechas y vigentes en <paramref name="ahora"/>.</summary>
    public bool VerificacionesSatisfechas(DateTimeOffset ahora) => Verificaciones.Where(v => v.Bloqueante).All(v => v.SatisfechaEn(ahora));

    // ---------- Decisiones ----------

    /// <summary>
    /// Registra el resultado de una consulta de listas (automática o «Reconsultar»). Idempotente: una referencia externa ya
    /// registrada no produce eventos. Las coincidencias ya descartadas con decisión vigente se heredan; si queda alguna sin
    /// decidir, la verificación pasa a <c>CoincidenciaPorAnalizar</c>. Si el flujo no pedía listas, queda como verificación no
    /// bloqueante. 409 <c>estado-invalido</c> con el expediente cerrado.
    /// </summary>
    public IReadOnlyList<object> RegistrarConsultaDeListas(ResultadoDeListas resultado, string por, DateTimeOffset ahora)
    {
        AsegurarAbiertoParaVerificar();
        var actual = BuscarVerificacion(TipoVerificacion.ListasRestrictivas);
        if (actual is not null && (actual.ReferenciaExterna == resultado.ReferenciaExterna ||
                                   actual.Anteriores.Any(a => a.ReferenciaExterna == resultado.ReferenciaExterna)))
            return [];

        var coincidencias = ReglasDeListas.Coincidencias(resultado, actual?.Coincidencias ?? [], ahora);
        var resultadoConsulta = coincidencias.Count == 0 ? ResultadoVerificacion.SinCoincidencias : ResultadoVerificacion.Coincidencias;
        return Simular(s => s.Emitir(new VerificacionExternaRegistrada(TipoVerificacion.ListasRestrictivas, resultado.Proveedor, resultadoConsulta,
            resultado.ReferenciaExterna, [.. resultado.Personas], coincidencias, resultado.Evidencia, resultado.VigenteHasta, por, ahora)), ahora);
    }

    /// <summary>
    /// Cumplimiento descarta o confirma una coincidencia, siempre con justificación. Descartadas todas ⇒ <c>Satisfecha</c>;
    /// una confirmada ⇒ <c>Fallida</c> (f-4: el expediente no cierra; un área lo rechaza o se bloquea el vínculo, nunca
    /// automático). 404 <c>coincidencia-no-encontrada</c>; 409 <c>coincidencia-ya-resuelta</c>; 403 <c>conflicto-de-intereses</c>.
    /// </summary>
    public IReadOnlyList<object> ResolverCoincidencia(Guid coincidenciaId, DecisionCoincidencia decision, string? justificacion, Revisor oficial,
        DateTimeOffset ahora)
    {
        AsegurarAbiertoParaVerificar();
        var verificacion = BuscarVerificacion(TipoVerificacion.ListasRestrictivas);
        var coincidencia = verificacion?.Coincidencias.FirstOrDefault(c => c.Id == coincidenciaId)
                           ?? throw ReglaDeNegocioException.NoEncontrado("coincidencia-no-encontrada", "No hay una coincidencia con ese id en la consulta vigente.");
        if (coincidencia.Resuelta)
            throw ReglaDeNegocioException.Conflicto("coincidencia-ya-resuelta", "Esa coincidencia ya se resolvió; si cambió algo, reconsulta las listas.");
        AsegurarSinConflictoDeIntereses(oficial, verificacion!);

        var texto = Requerido(justificacion, "justificacion-requerida", "Resolver una coincidencia necesita una justificación.");
        return Simular(s => s.Emitir(new CoincidenciaResuelta(TipoVerificacion.ListasRestrictivas, coincidenciaId, decision, texto,
            oficial.UsuarioId.ToString(), ahora.AddMonths(MesesVigenciaDecisionCoincidencia), ahora)), ahora);
    }

    /// <summary>
    /// Registra el estudio del Oficial de Cumplimiento con vigencia hasta <paramref name="vigenteHasta"/>. 422
    /// <c>nivel-riesgo-invalido</c> (1–3), <c>debida-diligencia-ampliada-requerida</c> (riesgo alto),
    /// <c>observaciones-requeridas</c> (conflicto de interés o concepto desfavorable), <c>evidencias-repetidas</c>; 403
    /// <c>conflicto-de-intereses</c>.
    /// </summary>
    public IReadOnlyList<object> RegistrarEstudio(DatosDeEstudio datos, Revisor oficial, DateTimeOffset vigenteHasta, DateTimeOffset ahora)
    {
        AsegurarAbiertoParaVerificar();
        if (datos.NivelRiesgo is < 1 or > 3)
            throw ReglaDeNegocioException.Validacion("nivel-riesgo-invalido", "El nivel de riesgo va de 1 (bajo) a 3 (alto).");
        if (datos.NivelRiesgo == 3 && datos.DebidaDiligencia != DebidaDiligencia.Ampliada)
            throw ReglaDeNegocioException.Validacion("debida-diligencia-ampliada-requerida",
                "Con riesgo alto la debida diligencia debe ser ampliada.");
        var observaciones = Texto(datos.Observaciones, "observaciones");
        if (observaciones is null && (datos.ConflictoDeInteres || datos.Concepto == ConceptoCumplimiento.Desfavorable))
            throw ReglaDeNegocioException.Validacion("observaciones-requeridas",
                "Explica en las observaciones el conflicto de interés o el concepto desfavorable.");
        var evidencias = datos.Evidencias ?? [];
        if (evidencias.GroupBy(e => (e.DocumentoId, e.Version)).Any(g => g.Count() > 1))
            throw ReglaDeNegocioException.Validacion("evidencias-repetidas", "Una evidencia aparece dos veces.");
        var adjuntos = (datos.Adjuntos ?? []).Distinct().ToList();
        if (adjuntos.Any(id => BuscarAdjuntoDeCumplimiento(id) is not { Proposito: PropositoDeAdjunto.Estudio }))
            throw ReglaDeNegocioException.Validacion("adjunto-invalido", "Un archivo adjunto del estudio no está en el expediente.");
        // Con matriz (CU-17): ajustar el nivel que salió del cálculo exige explicarlo (la debida diligencia mínima del nivel la verifica el endpoint con la configuración).
        var justificacionDeNivel = Texto(datos.JustificacionDeNivel, "justificacionDeNivel");
        if (datos.Matriz is { } matriz)
        {
            if (datos.NivelRiesgo != matriz.Nivel && (justificacionDeNivel is null || justificacionDeNivel.Length < LongitudMinimaJustificacion))
                throw ReglaDeNegocioException.Validacion("justificacion-de-nivel-requerida",
                    $"La matriz dio nivel {matriz.Nivel} y registras {datos.NivelRiesgo}: explica el ajuste (al menos {LongitudMinimaJustificacion} caracteres).");
        }
        if (vigenteHasta <= ahora)
            throw ReglaDeNegocioException.Validacion("vigencia-invalida", "La vigencia del estudio debe ser futura.");
        var listas = BuscarVerificacion(TipoVerificacion.ListasRestrictivas);
        if (listas is not null)
            AsegurarSinConflictoDeIntereses(oficial, listas);
        else
            AsegurarSinConflictoDeIntereses(oficial, null);

        var verificadoEnListas = listas?.SatisfechaEn(ahora) ?? false;
        return Simular(s => s.Emitir(new EstudioDeCumplimientoRegistrado(datos with
            {
                Observaciones = observaciones, Evidencias = [.. evidencias], Adjuntos = adjuntos, JustificacionDeNivel = justificacionDeNivel
            }, verificadoEnListas, oficial.UsuarioId.ToString(), vigenteHasta, ahora)), ahora);
    }

    /// <summary>Anota una consulta automática fallida; la verificación sigue pendiente y se reintenta desde <paramref name="reintentarDesde"/>.</summary>
    public IReadOnlyList<object> RegistrarFalloDeConsulta(TipoVerificacion tipo, string proveedor, string error, DateTimeOffset reintentarDesde,
        DateTimeOffset ahora)
    {
        if (!EstaAbierto)
            return [];
        var texto = error.Length > 500 ? error[..500] : error;
        return [new ConsultaDeVerificacionFallida(tipo, proveedor, texto, reintentarDesde, ahora)];
    }

    /// <summary>
    /// Expira las verificaciones satisfechas cuya vigencia ya pasó con el expediente abierto (las listas se reconsultan solas;
    /// el estudio vuelve a la bandeja del oficial). Sin nada vencido no emite eventos.
    /// </summary>
    public IReadOnlyList<object> ExpirarVerificaciones(DateTimeOffset ahora)
    {
        if (!EstaAbierto)
            return [];
        return
        [
            .. Verificaciones.Where(v => v.Estado == EstadoVerificacion.Satisfecha && v.VigenteHasta is { } hasta && hasta <= ahora)
                .Select(v => new VerificacionExpirada(v.Tipo, v.VigenteHasta!.Value, ahora))
        ];
    }

    /// <summary>
    /// La evidencia de la consulta vigente (o de una anterior por su <paramref name="referencia"/>) y el evento que deja la
    /// descarga en el historial. 404 <c>evidencia-no-encontrada</c>.
    /// </summary>
    public (EvidenciaDeVerificacion Evidencia, EvidenciaDeVerificacionDescargada Evento) DescargarEvidencia(TipoVerificacion tipo, string? referencia,
        string actorTipo, string actorId, string? ip, string? userAgent, DateTimeOffset ahora)
    {
        var verificacion = BuscarVerificacion(tipo);
        var (evidencia, referenciaExterna) = referencia is null || referencia == verificacion?.ReferenciaExterna
            ? (verificacion?.Evidencia, verificacion?.ReferenciaExterna)
            : (verificacion?.Anteriores.FirstOrDefault(a => a.ReferenciaExterna == referencia)?.Evidencia, referencia);
        if (evidencia is null || referenciaExterna is null)
            throw ReglaDeNegocioException.NoEncontrado("evidencia-no-encontrada", "Esa verificación no tiene evidencia guardada.");
        return (evidencia, new EvidenciaDeVerificacionDescargada(tipo, referenciaExterna, actorTipo, actorId, ip, userAgent, ahora));
    }

    private void AsegurarAbiertoParaVerificar()
    {
        if (!EstaAbierto)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"El expediente está {Estado}: ya no recibe verificaciones.");
    }

    /// <summary>Quien decide no puede ser el tercero ni una de las personas consultadas (I-21).</summary>
    private void AsegurarSinConflictoDeIntereses(Revisor oficial, VerificacionDeExpediente? listas)
    {
        if (string.IsNullOrWhiteSpace(oficial.ClaveIdentificacion))
            return;
        if (oficial.ClaveIdentificacion == TerceroIdentificacion ||
            (listas?.Personas.Any(p => string.Equals(p.Clave, oficial.ClaveIdentificacion, StringComparison.OrdinalIgnoreCase)) ?? false))
            throw ReglaDeNegocioException.Prohibido("conflicto-de-intereses",
                "Tu identificación coincide con la del tercero o con una de sus personas relacionadas: no puedes decidir esta verificación.");
    }

    // ---------- Evolución ----------

    public void Apply(VerificacionesRequeridas e)
    {
        foreach (var requerida in e.Verificaciones)
        {
            var verificacion = BuscarVerificacion(requerida.Tipo);
            if (verificacion is null)
                Verificaciones.Add(verificacion = new VerificacionDeExpediente { Tipo = requerida.Tipo, Estado = EstadoVerificacion.Pendiente });
            verificacion.Bloqueante = requerida.Bloqueante;
            verificacion.RequeridaPorElFlujo = true;
            verificacion.VigenciaDias = requerida.VigenciaDias;
            verificacion.FrecuenciaReconsultaMeses = requerida.FrecuenciaReconsultaMeses;
        }

        RecalcularVerificaciones();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VerificacionExternaRegistrada e)
    {
        var verificacion = BuscarVerificacion(e.Tipo);
        if (verificacion is null)
            Verificaciones.Add(verificacion = new VerificacionDeExpediente { Tipo = e.Tipo, Bloqueante = false });

        if (verificacion.ReferenciaExterna is { } anterior)
            verificacion.Anteriores.Add(new ConsultaAnterior(verificacion.Proveedor ?? string.Empty, anterior,
                verificacion.Resultado ?? ResultadoVerificacion.SinCoincidencias, verificacion.RegistradaEn ?? e.Fecha, verificacion.Evidencia,
                verificacion.Coincidencias.Count));

        verificacion.Proveedor = e.Proveedor;
        verificacion.ReferenciaExterna = e.ReferenciaExterna;
        verificacion.Resultado = e.Resultado;
        verificacion.RegistradaEn = e.Fecha;
        verificacion.RegistradaPor = e.Por;
        verificacion.VigenteHasta = e.VigenteHasta;
        verificacion.Evidencia = e.Evidencia;
        verificacion.Personas = [.. e.Personas];
        verificacion.Coincidencias = [.. e.Coincidencias.Select(ReglasDeListas.Detalle)];
        verificacion.Fallos = 0;
        verificacion.UltimoError = null;
        verificacion.ReintentarDesde = null;
        verificacion.ExpiroEn = null;
        verificacion.Estado = EstadoDeListas(verificacion);
        RecalcularVerificaciones();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(CoincidenciaResuelta e)
    {
        var verificacion = BuscarVerificacion(e.Tipo)!;
        var coincidencia = verificacion.Coincidencias.First(c => c.Id == e.CoincidenciaId);
        coincidencia.Resolucion = new ResolucionDeCoincidencia(e.Decision, e.Justificacion, e.Por, e.Fecha, e.VigenteHasta);
        coincidencia.Heredada = false;
        verificacion.Estado = EstadoDeListas(verificacion);
        RecalcularVerificaciones();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(EstudioDeCumplimientoRegistrado e)
    {
        var verificacion = BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento);
        if (verificacion is null)
            Verificaciones.Add(verificacion = new VerificacionDeExpediente { Tipo = TipoVerificacion.EstudioDeCumplimiento, Bloqueante = false });

        if (verificacion.Estudio is { } previo)
            verificacion.Anteriores.Add(new ConsultaAnterior("Oficial de cumplimiento", verificacion.ReferenciaExterna ?? string.Empty,
                verificacion.Resultado ?? ResultadoVerificacion.Aprobado, previo.Fecha, null, 0));

        var favorable = e.Estudio.Concepto == ConceptoCumplimiento.Favorable;
        verificacion.Estudio = new EstudioRegistrado(e.Estudio, e.VerificadoEnListas, e.Por, e.Fecha);
        verificacion.Proveedor = "Oficial de cumplimiento";
        verificacion.ReferenciaExterna = $"estudio-{e.Fecha.ToUnixTimeMilliseconds()}";
        verificacion.Resultado = favorable ? ResultadoVerificacion.Aprobado : ResultadoVerificacion.Rechazado;
        verificacion.RegistradaEn = e.Fecha;
        verificacion.RegistradaPor = e.Por;
        verificacion.VigenteHasta = e.VigenteHasta;
        verificacion.ExpiroEn = null;
        verificacion.Estado = favorable ? EstadoVerificacion.Satisfecha : EstadoVerificacion.Fallida;
        RecalcularVerificaciones();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VerificacionExpirada e)
    {
        var verificacion = BuscarVerificacion(e.Tipo)!;
        verificacion.Estado = EstadoVerificacion.Expirada;
        verificacion.ExpiroEn = e.Fecha;
        ActualizadoEn = e.Fecha;
        // Las listas vencidas se reconsultan solas (docs/11 §2.7.8 ReconsultaListas): ProximaConsultaDeListas = ahora.
        RecalcularVerificaciones();
    }

    public void Apply(ConsultaDeVerificacionFallida e)
    {
        var verificacion = BuscarVerificacion(e.Tipo);
        if (verificacion is null)
            return;
        verificacion.Fallos++;
        verificacion.UltimoError = e.Error;
        verificacion.ReintentarDesde = e.ReintentarDesde;
        RecalcularVerificaciones();
        ActualizadoEn = e.Fecha;
    }

    /// <summary>La descarga no cambia el estado: queda en el historial del expediente.</summary>
    public void Apply(EvidenciaDeVerificacionDescargada e)
    {
    }

    private static EstadoVerificacion EstadoDeListas(VerificacionDeExpediente verificacion) => ReglasDeListas.Estado(verificacion.Coincidencias);

    /// <summary>
    /// Campos planos que leen el procesador y las bandejas: la próxima consulta automática de listas (pendiente, en revisión o
    /// ya devuelto, y sin un reintento futuro), el primer vencimiento y si Cumplimiento tiene algo por hacer.
    /// </summary>
    private void RecalcularVerificaciones()
    {
        ConVerificaciones = Verificaciones.Count > 0;
        var abierto = EstaAbierto;
        PendienteDeCumplimiento = abierto && Verificaciones.Any(v => v.RequiereAtencion);

        var listas = BuscarVerificacion(TipoVerificacion.ListasRestrictivas);
        var porConsultar = abierto && Estado != EstadoExpediente.Abierto && listas is { Estado: EstadoVerificacion.Pendiente or EstadoVerificacion.Expirada };
        ProximaConsultaDeListas = porConsultar ? listas!.ReintentarDesde ?? listas.ExpiroEn ?? ActualizadoEn : null;

        ProximoVencimientoDeVerificacion = abierto
            ? Verificaciones.Where(v => v.Estado == EstadoVerificacion.Satisfecha && v.VigenteHasta is not null).Select(v => v.VigenteHasta).Min()
            : null;
    }
}

/// <summary>Reglas de una consulta de listas compartidas por el expediente y el monitoreo del vínculo (VE-5, VE-12).</summary>
public static class ReglasDeListas
{
    /// <summary>
    /// Da id a cada coincidencia hallada y hereda la decisión de una coincidencia igual (persona, lista y nombre encontrado)
    /// ya descartada cuya decisión sigue vigente: una reconsulta no vuelve a pedir lo que Cumplimiento ya analizó. Una
    /// confirmada nunca se hereda como resuelta: vuelve a pedirse (nunca se aprueba sola).
    /// </summary>
    public static List<CoincidenciaEncontrada> Coincidencias(ResultadoDeListas resultado, IReadOnlyList<CoincidenciaDeVerificacion> previas,
        DateTimeOffset ahora)
    {
        var personas = resultado.Personas.GroupBy(p => p.Clave, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(),
            StringComparer.OrdinalIgnoreCase);
        return
        [
            .. resultado.Coincidencias.Select(c =>
            {
                var persona = personas.GetValueOrDefault(c.ClavePersona);
                var heredada = previas.FirstOrDefault(p => p.EsLaMisma(c.ClavePersona, c.Lista, c.NombreEncontrado) &&
                                                           p.Resolucion is { Decision: DecisionCoincidencia.Descartada } r && r.VigenteHasta > ahora);
                return new CoincidenciaEncontrada(Guid.CreateVersion7(), c.ClavePersona, persona?.Nombre ?? c.ClavePersona, persona?.Rol ?? "Tercero",
                    c.Lista, c.NombreEncontrado, c.Detalle, c.Puntaje, heredada?.Resolucion);
            })
        ];
    }

    public static CoincidenciaDeVerificacion Detalle(CoincidenciaEncontrada c) => new()
    {
        Id = c.Id,
        ClavePersona = c.ClavePersona,
        NombrePersona = c.NombrePersona,
        Rol = c.Rol,
        Lista = c.Lista,
        NombreEncontrado = c.NombreEncontrado,
        Detalle = c.Detalle,
        Puntaje = c.Puntaje,
        Resolucion = c.Heredada,
        Heredada = c.Heredada is not null
    };

    /// <summary>Una confirmada ⇒ <c>Fallida</c>; alguna sin decidir ⇒ <c>CoincidenciaPorAnalizar</c>; si no ⇒ <c>Satisfecha</c>.</summary>
    public static EstadoVerificacion Estado(IReadOnlyList<CoincidenciaDeVerificacion> coincidencias) =>
        coincidencias.Any(c => c.Resolucion is { Decision: DecisionCoincidencia.Confirmada }) ? EstadoVerificacion.Fallida
        : coincidencias.Any(c => !c.Resuelta) ? EstadoVerificacion.CoincidenciaPorAnalizar
        : EstadoVerificacion.Satisfecha;
}
