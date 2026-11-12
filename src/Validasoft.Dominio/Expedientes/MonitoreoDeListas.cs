using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// Monitoreo de listas de un vínculo activo (VE-12): tras aprobarse un expediente que consultó listas, el vínculo se vuelve
/// a consultar cada <see cref="FrecuenciaDias"/> días (30 por omisión; Infolaft alerta sobre lo consultado en el último año).
/// Hereda las decisiones del expediente; una coincidencia nueva va a la bandeja del oficial y nunca se aprueba sola; si
/// Cumplimiento la confirma, el administrador decide bloquear o desvincular (f-4). Esquema <c>validacion</c>, multi-tenant;
/// id = vínculo. Las decisiones quedan en la auditoría.
/// </summary>
public sealed class MonitoreoDeListas
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TerceroId { get; set; }

    public string TerceroNombre { get; set; } = string.Empty;

    public string TerceroIdentificacion { get; set; } = string.Empty;

    /// <summary>Expediente aprobado que lo originó (o el último que lo renovó).</summary>
    public Guid ExpedienteId { get; set; }

    public string Folio { get; set; } = string.Empty;

    public int FrecuenciaDias { get; set; }

    /// <summary>Nulo = detenido (vínculo inactivo o monitoreo apagado).</summary>
    public DateTimeOffset? ProximaConsulta { get; set; }

    public VerificacionDeExpediente Listas { get; set; } = new() { Tipo = TipoVerificacion.ListasRestrictivas };

    /// <summary>
    /// Hay algo que decidir para el oficial: alguna coincidencia de la última consulta sin resolver (HI-5, PP-1). Descartar o
    /// confirmar la última la resuelve y la fila sale de su bandeja: confirmada, lo que sigue es del administrador (bloquear o
    /// desvincular el vínculo) y <see cref="Listas"/> conserva el estado «Fallida» para que se vea.
    /// </summary>
    public bool PendienteDeCumplimiento { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    public Guid VinculoId => Id;

    /// <summary>Alguna coincidencia de la última consulta espera la decisión de Cumplimiento.</summary>
    public bool TieneCoincidenciasSinResolver => Listas.Coincidencias.Any(c => !c.Resuelta);

    /// <summary>
    /// Inicia (o renueva) el monitoreo con la consulta del expediente aprobado: copia personas, coincidencias resueltas y
    /// evidencia, y programa la siguiente consulta. Sin consulta de listas en el expediente devuelve nulo.
    /// </summary>
    public static MonitoreoDeListas? Iniciar(Expediente expediente, MonitoreoDeListas? anterior, int frecuenciaDias, DateTimeOffset ahora)
    {
        if (expediente.BuscarVerificacion(TipoVerificacion.ListasRestrictivas) is not { ReferenciaExterna: not null } listas || frecuenciaDias <= 0)
            return null;

        var monitoreo = anterior ?? new MonitoreoDeListas { Id = expediente.VinculoId, CreadoEn = ahora };
        monitoreo.EmpresaId = expediente.EmpresaId;
        monitoreo.TerceroId = expediente.TerceroId;
        monitoreo.TerceroNombre = expediente.TerceroNombre;
        monitoreo.TerceroIdentificacion = expediente.TerceroIdentificacion;
        monitoreo.ExpedienteId = expediente.Id;
        monitoreo.Folio = expediente.Folio;
        monitoreo.FrecuenciaDias = frecuenciaDias;
        monitoreo.Listas = new VerificacionDeExpediente
        {
            Tipo = TipoVerificacion.ListasRestrictivas,
            Bloqueante = false,
            Estado = listas.Estado,
            Resultado = listas.Resultado,
            Proveedor = listas.Proveedor,
            ReferenciaExterna = listas.ReferenciaExterna,
            RegistradaEn = listas.RegistradaEn,
            RegistradaPor = listas.RegistradaPor,
            VigenteHasta = listas.VigenteHasta,
            Evidencia = listas.Evidencia,
            Personas = [.. listas.Personas],
            Coincidencias = [.. listas.Coincidencias],
            Anteriores = [.. monitoreo.Listas.Anteriores]
        };
        monitoreo.ProximaConsulta = ahora.AddDays(frecuenciaDias);
        monitoreo.PendienteDeCumplimiento = monitoreo.TieneCoincidenciasSinResolver;
        monitoreo.ActualizadoEn = ahora;
        return monitoreo;
    }

    /// <summary>
    /// Registra una reconsulta periódica: hereda las coincidencias descartadas vigentes y programa la siguiente. Devuelve las
    /// coincidencias nuevas (sin decisión) para avisar a Cumplimiento. Idempotente por referencia.
    /// </summary>
    public IReadOnlyList<CoincidenciaDeVerificacion> RegistrarConsulta(ResultadoDeListas resultado, string por, DateTimeOffset ahora)
    {
        if (Listas.ReferenciaExterna == resultado.ReferenciaExterna || Listas.Anteriores.Any(a => a.ReferenciaExterna == resultado.ReferenciaExterna))
            return [];

        var coincidencias = ReglasDeListas.Coincidencias(resultado, Listas.Coincidencias, ahora).Select(ReglasDeListas.Detalle).ToList();
        if (Listas.ReferenciaExterna is { } anterior)
            Listas.Anteriores.Add(new ConsultaAnterior(Listas.Proveedor ?? string.Empty, anterior, Listas.Resultado ?? ResultadoVerificacion.SinCoincidencias,
                Listas.RegistradaEn ?? ahora, Listas.Evidencia, Listas.Coincidencias.Count));

        Listas.Proveedor = resultado.Proveedor;
        Listas.ReferenciaExterna = resultado.ReferenciaExterna;
        Listas.Resultado = coincidencias.Count == 0 ? ResultadoVerificacion.SinCoincidencias : ResultadoVerificacion.Coincidencias;
        Listas.RegistradaEn = ahora;
        Listas.RegistradaPor = por;
        Listas.VigenteHasta = resultado.VigenteHasta;
        Listas.Evidencia = resultado.Evidencia;
        Listas.Personas = [.. resultado.Personas];
        Listas.Coincidencias = coincidencias;
        Listas.Fallos = 0;
        Listas.UltimoError = null;
        Listas.ReintentarDesde = null;
        Listas.Estado = ReglasDeListas.Estado(coincidencias);
        PendienteDeCumplimiento = TieneCoincidenciasSinResolver;
        ProximaConsulta = ahora.AddDays(FrecuenciaDias);
        ActualizadoEn = ahora;
        return [.. coincidencias.Where(c => !c.Resuelta)];
    }

    /// <summary>Cumplimiento decide una coincidencia del monitoreo (mismas reglas que en el expediente: justificación, sin conflicto).</summary>
    public CoincidenciaDeVerificacion ResolverCoincidencia(Guid coincidenciaId, DecisionCoincidencia decision, string? justificacion, Revisor oficial,
        DateTimeOffset ahora)
    {
        var coincidencia = Listas.Coincidencias.FirstOrDefault(c => c.Id == coincidenciaId)
                           ?? throw ReglaDeNegocioException.NoEncontrado("coincidencia-no-encontrada", "No hay una coincidencia con ese id en la última consulta.");
        if (coincidencia.Resuelta)
            throw ReglaDeNegocioException.Conflicto("coincidencia-ya-resuelta", "Esa coincidencia ya se resolvió.");
        if (!string.IsNullOrWhiteSpace(oficial.ClaveIdentificacion) &&
            (oficial.ClaveIdentificacion == TerceroIdentificacion ||
             Listas.Personas.Any(p => string.Equals(p.Clave, oficial.ClaveIdentificacion, StringComparison.OrdinalIgnoreCase))))
            throw ReglaDeNegocioException.Prohibido("conflicto-de-intereses",
                "Tu identificación coincide con la del tercero o con una de sus personas relacionadas: no puedes decidir esta verificación.");
        var texto = string.IsNullOrWhiteSpace(justificacion)
            ? throw ReglaDeNegocioException.Validacion("justificacion-requerida", "Resolver una coincidencia necesita una justificación.")
            : justificacion.Trim();
        if (texto.Length > 4000)
            throw ReglaDeNegocioException.Validacion("texto-demasiado-largo", "La justificación admite hasta 4000 caracteres.");

        coincidencia.Resolucion = new ResolucionDeCoincidencia(decision, texto, oficial.UsuarioId.ToString(), ahora,
            ahora.AddMonths(Expediente.MesesVigenciaDecisionCoincidencia));
        coincidencia.Heredada = false;
        Listas.Estado = ReglasDeListas.Estado(Listas.Coincidencias);
        PendienteDeCumplimiento = TieneCoincidenciasSinResolver;
        ActualizadoEn = ahora;
        return coincidencia;
    }

    /// <summary>La reconsulta falló: se reintenta más tarde sin perder el último resultado.</summary>
    public void RegistrarFallo(string error, DateTimeOffset reintentarDesde, DateTimeOffset ahora)
    {
        Listas.Fallos++;
        Listas.UltimoError = error.Length > 500 ? error[..500] : error;
        Listas.ReintentarDesde = reintentarDesde;
        ProximaConsulta = reintentarDesde;
        ActualizadoEn = ahora;
    }

    /// <summary>
    /// Cambiaron las personas relacionadas del tercero (CU-15): si alguna actual no estaba entre las consultadas, la próxima
    /// reconsulta pasa a ser ya. Devuelve si cambió algo (un monitoreo detenido no se reactiva).
    /// </summary>
    public bool ReconsultarPorCambioDePersonas(IReadOnlyCollection<string> clavesActuales, DateTimeOffset ahora)
    {
        if (ProximaConsulta is null || Listas.ReferenciaExterna is null)
            return false;
        var consultadas = Listas.Personas.Select(p => p.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);
        if (clavesActuales.All(consultadas.Contains) || ProximaConsulta <= ahora)
            return false;
        ProximaConsulta = ahora;
        ActualizadoEn = ahora;
        return true;
    }

    /// <summary>El vínculo dejó de estar activo: no se consulta más (el historial se conserva).</summary>
    public void Detener(DateTimeOffset ahora)
    {
        ProximaConsulta = null;
        ActualizadoEn = ahora;
    }
}
