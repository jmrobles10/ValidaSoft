using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// Desaprobar (DS-1, DS-2): devolver a revisión lo que ya se había aprobado, con justificación obligatoria y solo un
/// administrador. Reproduce lo que el SRM hace con <c>DesaprobarTercero</c>, <c>DesaprobarAFlujo</c> y
/// <c>DesaprobarAEstadoInicial</c>: (a) <see cref="ReabrirEtapa"/> reabre una etapa ya aprobada de un expediente abierto;
/// (b) <see cref="Desaprobar"/> deshace la aprobación de un expediente ya <c>Aprobado</c> y (c) <see cref="VolverAFaseInicial"/>
/// repite todo el flujo. Los veredictos revertidos no se borran: quedan en el historial marcados <c>Revertido</c>.
/// </summary>
public sealed partial class Expediente
{
    /// <summary>Veces que un administrador devolvió a revisión lo ya decidido (reapertura de etapa, fase inicial o desaprobación).</summary>
    public int Desaprobaciones { get; set; }

    public DesaprobacionDeExpediente? UltimaDesaprobacion { get; set; }

    /// <summary>
    /// ¿Está en el comienzo del flujo? Primera etapa activa, las demás esperando y nadie (salvo el sistema) ha decidido nada:
    /// volver a la fase inicial no cambiaría nada.
    /// </summary>
    [JsonIgnore]
    public bool EnFaseInicial
    {
        get
        {
            var primera = Etapas.MinBy(e => e.Orden);
            return primera is { Estado: EstadoEtapa.Activa } &&
                   Etapas.Where(e => e != primera).All(e => e.Estado == EstadoEtapa.Esperando) &&
                   !Etapas.SelectMany(e => e.Areas).SelectMany(a => a.Veredictos).Any(v => v.Vigente && v.Origen != OrigenVeredicto.Sistema) &&
                   !Requisitos.SelectMany(r => r.Veredictos).Any(v => v.Vigente && v.Origen != OrigenVeredicto.Sistema);
        }
    }

    /// <summary>
    /// (a) Reabre una etapa ya aprobada de un expediente en revisión o devuelto al tercero (equivale a <c>DesaprobarAFlujo</c>):
    /// sus áreas vuelven a quedar pendientes y las etapas posteriores esperan. Si estaba devuelto, la observación deja de
    /// contar (la etapa que observó volvió a esperar) y la revisión se reanuda. 409 <c>estado-invalido</c>, 404
    /// <c>etapa-no-encontrada</c>, 409 <c>etapa-no-aprobada</c> (una etapa activa se corrige anulando sus veredictos), 422
    /// <c>justificacion-requerida</c>.
    /// </summary>
    public IReadOnlyList<object> ReabrirEtapa(int orden, string administradorId, string? justificacion, DateTimeOffset ahora)
    {
        if (Estado is not (EstadoExpediente.EnRevision or EstadoExpediente.Observado))
            throw ReglaDeNegocioException.Conflicto("estado-invalido",
                $"Solo se reabre una etapa de un expediente en revisión o devuelto al tercero; este está {Estado}.");
        var etapa = Etapas.FirstOrDefault(e => e.Orden == orden)
                    ?? throw ReglaDeNegocioException.NoEncontrado("etapa-no-encontrada", $"El expediente no tiene una etapa {orden}.");
        if (etapa.Estado != EstadoEtapa.Aprobada)
            throw ReglaDeNegocioException.Conflicto("etapa-no-aprobada",
                $"La etapa {orden} está {etapa.Estado}: solo se reabre una etapa ya aprobada (en una activa se anulan sus veredictos).");

        var texto = Requerido(justificacion, "justificacion-requerida", "Reabrir una etapa necesita una justificación.");
        return Devolver(orden, false, administradorId, texto, ahora);
    }

    /// <summary>
    /// (c) Devuelve todo el expediente a su fase inicial (equivale a <c>DesaprobarAEstadoInicial</c>): todos los veredictos
    /// vigentes dejan de contar y las etapas se repiten desde la primera. En revisión, devuelto al tercero o ya aprobado (en ese
    /// caso es además una desaprobación, <see cref="Desaprobar"/>). 409 <c>ya-esta-en-fase-inicial</c> si no cambiaría nada.
    /// </summary>
    public IReadOnlyList<object> VolverAFaseInicial(string administradorId, string? justificacion, DateTimeOffset ahora)
    {
        if (Estado is not (EstadoExpediente.EnRevision or EstadoExpediente.Observado or EstadoExpediente.Aprobado))
            throw ReglaDeNegocioException.Conflicto("estado-invalido",
                $"Solo vuelve a la fase inicial un expediente en revisión, devuelto o aprobado; este está {Estado}.");
        if (Estado != EstadoExpediente.Aprobado && EnFaseInicial)
            throw ReglaDeNegocioException.Conflicto("ya-esta-en-fase-inicial", "El expediente ya está en su fase inicial.");

        var texto = Requerido(justificacion, "justificacion-requerida", "Volver a la fase inicial necesita una justificación.");
        return Devolver(Etapas.Min(e => e.Orden), true, administradorId, texto, ahora);
    }

    /// <summary>
    /// (b) Desaprueba un expediente ya <c>Aprobado</c> (equivale a <c>DesaprobarTercero</c>): vuelve a revisión desde la
    /// etapa <paramref name="orden"/> (por omisión la última, la que lo cerró). El vínculo vuelve a validación y el
    /// certificado se revoca en la misma transacción (lo hace el proceso de validación). 409 <c>estado-invalido</c>.
    /// </summary>
    public IReadOnlyList<object> Desaprobar(int? orden, string administradorId, string? justificacion, DateTimeOffset ahora)
    {
        if (Estado != EstadoExpediente.Aprobado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo se desaprueba un expediente aprobado; este está {Estado}.");
        var destino = orden ?? Etapas.Max(e => e.Orden);
        if (Etapas.All(e => e.Orden != destino))
            throw ReglaDeNegocioException.NoEncontrado("etapa-no-encontrada", $"El expediente no tiene una etapa {destino}.");

        var texto = Requerido(justificacion, "justificacion-requerida", "Desaprobar un expediente necesita una justificación.");
        return Devolver(destino, destino == Etapas.Min(e => e.Orden), administradorId, texto, ahora);
    }

    /// <summary>
    /// Cascada común: <c>ExpedienteDesaprobado</c> (si estaba aprobado) o <c>EtapaReabierta</c>, activación de la etapa devuelta
    /// (sus áreas automáticas aprueban de nuevo) y, si estaba devuelto al tercero, <c>RevisionReanudada</c>.
    /// </summary>
    private IReadOnlyList<object> Devolver(int orden, bool faseInicial, string administradorId, string justificacion, DateTimeOffset ahora)
    {
        var estabaAprobado = Estado == EstadoExpediente.Aprobado;
        var estabaDevuelto = Estado == EstadoExpediente.Observado;
        return Simular(s =>
        {
            s.Emitir(estabaAprobado
                ? new ExpedienteDesaprobado(orden, faseInicial, administradorId, justificacion, ahora)
                : new EtapaReabierta(orden, faseInicial, administradorId, justificacion, ahora));
            Activar(s, s.Estado!.Etapas.First(e => e.Orden == orden), ahora);
            if (estabaDevuelto)
                s.Emitir(new RevisionReanudada(Guid.Empty, ahora));
        }, ahora);
    }

    // ---------- Evolución ----------

    public void Apply(ExpedienteDesaprobado e)
    {
        Estado = EstadoExpediente.EnRevision;
        CerradoEn = null;
        ReabrirDesde(e.Orden, e.FaseInicial, true, e.AdministradorId, e.Justificacion, e.Fecha);
        RecalcularVerificaciones();
    }

    public void Apply(EtapaReabierta e) => ReabrirDesde(e.Orden, e.FaseInicial, false, e.AdministradorId, e.Justificacion, e.Fecha);

    /// <summary>
    /// La etapa <paramref name="orden"/> y las posteriores vuelven a esperar y sus veredictos vigentes dejan de contar; la
    /// activación de la etapa devuelta es el evento que sigue (<c>EtapaActivada</c>).
    /// </summary>
    private void ReabrirDesde(int orden, bool faseInicial, bool expedienteAprobado, string administradorId, string justificacion, DateTimeOffset fecha)
    {
        var devueltas = Etapas.Where(x => x.Orden >= orden).ToList();
        var areas = devueltas.SelectMany(x => x.Areas).ToList();
        var areaIds = areas.Select(a => a.AreaId).ToHashSet();

        foreach (var etapa in devueltas)
        {
            etapa.Estado = EstadoEtapa.Esperando;
            etapa.ActivadaEn = null;
            etapa.AprobadaEn = null;
        }

        foreach (var area in areas)
        {
            Revertir(area.Veredictos);
            area.Estado = EstadoAreaExpediente.Esperando;
            area.PendienteDesde = null;
            area.SlaVence = null;
            area.SlaAvisadoEn = null;
            area.SlaIncumplidoEn = null;
        }

        foreach (var requisito in Requisitos)
            Revertir(requisito.Veredictos.Where(v => v.AreaId is { } area && areaIds.Contains(area)));

        Desaprobaciones++;
        UltimaDesaprobacion = new DesaprobacionDeExpediente(orden, faseInicial, expedienteAprobado, administradorId, justificacion, fecha);
        RecalcularRequisitos(fecha);
        ActualizadoEn = fecha;
    }

    private static void Revertir(IEnumerable<VeredictoRegistrado> veredictos)
    {
        foreach (var veredicto in veredictos.Where(v => v.Vigente).ToList())
        {
            veredicto.Vigente = false;
            veredicto.Revertido = true;
        }
    }
}
