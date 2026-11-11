using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// Lo que el oficial de cumplimiento puede hacer además de decidir una coincidencia o registrar el estudio (CU-15…CU-18): subir
/// archivos al expediente, registrar una consulta de listas hecha por fuera con su PDF, rechazar directamente y expirar las
/// listas cuando cambian las personas relacionadas.
/// </summary>
public sealed partial class Expediente
{
    /// <summary>Tamaño máximo de un archivo de cumplimiento (CU-16).</summary>
    public const long TamanoMaximoDeAdjunto = 15L * 1024 * 1024;

    /// <summary>Una consulta hecha por fuera no puede ser más vieja que esto (si no, hay que repetirla).</summary>
    public const int DiasMaximosDeConsultaManual = 30;

    /// <summary>Archivos que subió el oficial (estudio y consultas manuales).</summary>
    public List<AdjuntoDeCumplimiento> AdjuntosDeCumplimiento { get; set; } = [];

    public AdjuntoDeCumplimiento? BuscarAdjuntoDeCumplimiento(Guid id) => AdjuntosDeCumplimiento.FirstOrDefault(a => a.Id == id);

    /// <summary>
    /// Registra un archivo subido por el oficial. 409 <c>estado-invalido</c> con el expediente cerrado; 422
    /// <c>adjunto-invalido</c> (vacío o con más de <see cref="TamanoMaximoDeAdjunto"/> bytes) y <c>adjuntos-demasiados</c> (hasta 20).
    /// </summary>
    public IReadOnlyList<object> RegistrarAdjuntoDeCumplimiento(AdjuntoDeCumplimiento adjunto)
    {
        AsegurarAbiertoParaVerificar();
        if (adjunto.Tamano is <= 0 or > TamanoMaximoDeAdjunto || string.IsNullOrWhiteSpace(adjunto.Nombre))
            throw ReglaDeNegocioException.Validacion("adjunto-invalido", "El archivo está vacío o pasa de 15 MB.");
        if (AdjuntosDeCumplimiento.Count >= 20)
            throw ReglaDeNegocioException.Validacion("adjuntos-demasiados", "El expediente admite hasta 20 archivos de cumplimiento.");
        return Simular(s => s.Emitir(new AdjuntoDeCumplimientoSubido(adjunto)), adjunto.Fecha);
    }

    /// <summary>
    /// Registra una consulta de listas que el oficial hizo por fuera (CU-16) con el PDF que obtuvo. Entra igual que una consulta
    /// automática: sin coincidencias queda satisfecha; con ellas, por analizar (y decide Cumplimiento). El archivo debe ser uno de los
    /// subidos para ese fin (422 <c>adjunto-invalido</c>); la consulta no puede ser futura ni de hace más de
    /// <see cref="DiasMaximosDeConsultaManual"/> días (422 <c>consulta-fuera-de-plazo</c>). 403 <c>conflicto-de-intereses</c>.
    /// </summary>
    public IReadOnlyList<object> RegistrarConsultaManualDeListas(ResultadoDeListas resultado, Guid adjuntoId, DateTimeOffset consultadaEn, Revisor oficial,
        DateTimeOffset ahora)
    {
        AsegurarAbiertoParaVerificar();
        var adjunto = BuscarAdjuntoDeCumplimiento(adjuntoId);
        if (adjunto is not { Proposito: PropositoDeAdjunto.ConsultaManual })
            throw ReglaDeNegocioException.Validacion("adjunto-invalido", "Sube primero el PDF de la consulta (archivo de consulta manual).");
        if (consultadaEn > ahora.AddMinutes(5) || consultadaEn < ahora.AddDays(-DiasMaximosDeConsultaManual))
            throw ReglaDeNegocioException.Validacion("consulta-fuera-de-plazo",
                $"La consulta debe ser de los últimos {DiasMaximosDeConsultaManual} días y no futura; si es más vieja, hazla de nuevo.");
        AsegurarSinConflictoDeIntereses(oficial, BuscarVerificacion(TipoVerificacion.ListasRestrictivas));
        return RegistrarConsultaDeListas(resultado, oficial.UsuarioId.ToString(), ahora);
    }

    /// <summary>
    /// El oficial rechaza el expediente directamente (CU-18), sin ser miembro de un área que pueda rechazar: solo con un hallazgo
    /// grave ya registrado (una verificación <c>Fallida</c>: coincidencia confirmada o estudio desfavorable; 409
    /// <c>rechazo-sin-hallazgo</c>), con motivos (422 <c>motivo-requerido</c>) y justificación (422 <c>justificacion-requerida</c>).
    /// Nunca es automático (f-4). 409 <c>estado-invalido</c> fuera de revisión; 403 <c>conflicto-de-intereses</c>.
    /// </summary>
    public IReadOnlyList<object> RechazarPorCumplimiento(Revisor oficial, IReadOnlyList<MotivoAplicado> motivos, string? justificacion, DateTimeOffset ahora)
    {
        if (Estado is not (EstadoExpediente.EnRevision or EstadoExpediente.Observado))
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"El expediente está {Estado}.");
        if (!Verificaciones.Any(v => v.Estado == EstadoVerificacion.Fallida))
            throw ReglaDeNegocioException.Conflicto("rechazo-sin-hallazgo",
                "Cumplimiento rechaza cuando hay un hallazgo: una coincidencia confirmada o un estudio desfavorable. Regístralo primero.");
        AsegurarSinConflictoDeIntereses(oficial, BuscarVerificacion(TipoVerificacion.ListasRestrictivas));
        if (motivos is null || motivos.Count == 0)
            throw ReglaDeNegocioException.Validacion("motivo-requerido", "Para rechazar indica al menos un motivo.");
        var texto = Requerido(justificacion, "justificacion-requerida", "El rechazo necesita una justificación.");
        return [new ExpedienteRechazado(Guid.Empty, oficial.UsuarioId.ToString(), [.. motivos.DistinctBy(m => m.Codigo)], texto, ahora, "Cumplimiento")];
    }

    /// <summary>
    /// Cambiaron las personas relacionadas del tercero (CU-15, p. ej. un representante legal nuevo): si alguna persona actual no
    /// estaba entre las consultadas, la consulta de listas ya no cubre al tercero y se expira para que se repita (las decisiones
    /// ya vigentes sobre coincidencias iguales se heredan). Sin consulta hecha, con la consulta ya expirada o sin personas nuevas no
    /// emite nada. <paramref name="clavesActuales"/>: las identificaciones normalizadas de las personas a consultar hoy.
    /// </summary>
    public IReadOnlyList<object> ExpirarListasPorCambioDePersonas(IReadOnlyCollection<string> clavesActuales, DateTimeOffset ahora)
    {
        if (!EstaAbierto || BuscarVerificacion(TipoVerificacion.ListasRestrictivas) is not { ReferenciaExterna: not null } listas ||
            listas.Estado is EstadoVerificacion.Pendiente or EstadoVerificacion.Expirada)
            return [];
        var consultadas = listas.Personas.Select(p => p.Clave).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var nuevas = clavesActuales.Where(c => !consultadas.Contains(c)).ToList();
        return nuevas.Count == 0
            ? []
            : [new VerificacionExpirada(TipoVerificacion.ListasRestrictivas, listas.VigenteHasta ?? ahora, ahora,
                "Cambió el representante legal o las personas relacionadas: hay que volver a consultar las listas.")];
    }

    public void Apply(AdjuntoDeCumplimientoSubido e)
    {
        AdjuntosDeCumplimiento.Add(e.Adjunto);
        ActualizadoEn = e.Adjunto.Fecha;
    }
}
