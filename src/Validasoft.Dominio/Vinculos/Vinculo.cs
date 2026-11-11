using System.Text.Json;
using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Dominio.Vinculos;

public sealed record Bloqueo(string Motivo, DateTimeOffset Desde, DateTimeOffset? Hasta);

public sealed record InvitacionVigente(string Correo, DateTimeOffset Expira, int Envios);

/// <summary>
/// Relación <c>(Empresa, Tercero, TipoDeVinculo)</c> — agregado event-sourced de vida larga (docs/03 §5).
/// Los métodos de decisión validan las reglas y devuelven el evento; los <c>Apply</c> solo evolucionan el
/// estado y los usa Marten para reconstruirlo. Cubre creación, invitación, aceptación, atributos, bloqueo, ciclo de
/// terminación y la relación con su expediente (<c>EnValidacion</c> al abrirlo, <c>Activo</c> al aprobarse); los
/// vencimientos llegan con H6.
/// </summary>
public sealed partial class Vinculo
{
    private static readonly EstadoVinculo[] Terminales = [EstadoVinculo.Anulado, EstadoVinculo.Desvinculado];

    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid TerceroId { get; set; }

    public Guid TipoVinculoId { get; set; }

    public Guid? FlujoId { get; set; }

    public EstadoVinculo Estado { get; set; }

    public EstadoVinculo? EstadoPrevioAlBloqueo { get; set; }

    public Dictionary<string, JsonElement> Atributos { get; set; } = [];

    public OrigenVinculo Origen { get; set; }

    /// <summary>Una referencia por sistema integrado que comparte esta validación (I-0).</summary>
    public List<ReferenciaExterna> Referencias { get; set; } = [];

    /// <summary>Copia plana de <see cref="Referencias"/> (<c>SISTEMA|id</c>) para buscar por referencia en Marten.</summary>
    public List<string> ClavesReferencia { get; set; } = [];

    /// <summary>Correo del tercero al que se dirigen las invitaciones de este vínculo.</summary>
    public string CorreoTercero { get; set; } = string.Empty;

    public InvitacionVigente? Invitacion { get; set; }

    public bool InvitacionAceptada { get; set; }

    public DateTimeOffset? InvitacionAceptadaEn { get; set; }

    /// <summary>Consentimientos registrados al aceptar (versión y hash de cada texto); la evidencia está en el evento.</summary>
    public List<ConsentimientoRegistrado> Consentimientos { get; set; } = [];

    public Bloqueo? Bloqueo { get; set; }

    /// <summary>Si alguna vez tuvo un expediente aprobado (habilita renovación al reintegrar).</summary>
    public bool TuvoAprobacion { get; set; }

    /// <summary>Expediente abierto (a lo sumo uno, invariante 1 de docs/03 §5.3).</summary>
    public Guid? ExpedienteAbiertoId { get; set; }

    /// <summary>Tipo del expediente abierto (nulo sin expediente abierto o si se abrió antes de que el vínculo lo registrara).</summary>
    public TipoExpediente? ExpedienteAbiertoTipo { get; set; }

    /// <summary>¿El expediente abierto es una renovación? (la de pre-vencimiento o la del vencimiento: la misma se reutiliza).</summary>
    [JsonIgnore]
    public bool TieneRenovacionAbierta => ExpedienteAbiertoId is not null && ExpedienteAbiertoTipo == TipoExpediente.Renovacion;

    /// <summary>Último expediente cerrado y su resultado.</summary>
    public Guid? UltimoExpedienteId { get; set; }

    public ResultadoExpediente? UltimoResultado { get; set; }

    /// <summary>
    /// El tercero aceptó pero no se pudo abrir el expediente (no había flujo explícito ni por defecto publicado): un
    /// administrador debe abrirlo (FL-6). Derivado; no es un estado ni un evento.
    /// </summary>
    [JsonIgnore]
    public bool AperturaDeExpedientePendiente =>
        InvitacionAceptada && Estado == EstadoVinculo.Invitado && ExpedienteAbiertoId is null && UltimoExpedienteId is null;

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    // ---------- Decisiones ----------

    public static VinculoCreado Crear(Guid empresaId, Guid terceroId, Guid tipoVinculoId, Guid? flujoId,
        Dictionary<string, JsonElement> atributos, OrigenVinculo origen, ReferenciaExterna? referencia,
        string correoInvitacion, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(correoInvitacion) || !correoInvitacion.Contains('@'))
            throw ReglaDeNegocioException.Validacion("correo-invitacion-requerido",
                "El vínculo necesita un correo válido del tercero para enviarle la invitación.");

        if (referencia is not null)
            ValidarReferencia(referencia);

        return new VinculoCreado(Guid.CreateVersion7(), empresaId, terceroId, tipoVinculoId, flujoId, atributos,
            origen, referencia, correoInvitacion.Trim().ToLowerInvariant(), ahora);
    }

    /// <summary>
    /// Otro sistema pide el mismo vínculo <c>(empresa, tercero, tipo)</c>: en lugar de duplicarlo, se agrega su
    /// referencia y ambos comparten la validación (I-0). Devuelve <c>null</c> si esa referencia ya estaba
    /// (idempotente); el mismo sistema con otro id es conflicto.
    /// </summary>
    public ReferenciaExternaAgregada? AgregarReferencia(ReferenciaExterna referencia, DateTimeOffset ahora)
    {
        ValidarReferencia(referencia);

        var delSistema = Referencias.FirstOrDefault(r => r.Sistema == referencia.Sistema);
        if (delSistema is not null)
        {
            return delSistema.Id == referencia.Id
                ? null
                : throw ReglaDeNegocioException.Conflicto("referencia-en-conflicto",
                    $"El vínculo ya tiene la referencia {delSistema.Id} del sistema {delSistema.Sistema}.");
        }

        return new ReferenciaExternaAgregada(referencia.Sistema, referencia.Id, ahora);
    }

    /// <summary>Solo en <c>Invitado</c> y mientras el tercero no haya aceptado (docs/03 §5.3).</summary>
    public InvitacionEnviada EnviarInvitacion(string? correoDestino, TimeSpan vigencia, Guid? loteId, DateTimeOffset ahora)
    {
        if (Estado != EstadoVinculo.Invitado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido",
                $"Solo se invita un vínculo en estado Invitado; este está {Estado}.");

        if (InvitacionAceptada)
            throw ReglaDeNegocioException.Conflicto("invitacion-ya-aceptada", "El tercero ya aceptó la invitación de esta empresa.");

        if (vigencia <= TimeSpan.Zero || vigencia > TimeSpan.FromDays(30))
            throw ReglaDeNegocioException.Validacion("vigencia-invalida", "La vigencia del enlace debe estar entre 1 hora y 30 días.");

        var correo = string.IsNullOrWhiteSpace(correoDestino) ? CorreoTercero : correoDestino.Trim().ToLowerInvariant();
        if (string.IsNullOrWhiteSpace(correo) || !correo.Contains('@'))
            throw ReglaDeNegocioException.Validacion("correo-invitacion-requerido", "Falta un correo válido para la invitación.");

        return new InvitacionEnviada("Correo", correo, ahora + vigencia, loteId, ahora);
    }

    /// <summary>
    /// El tercero acepta (primer acceso). Desde aquí la bóveda se puede compartir con esta empresa (B-1,
    /// ADR-0009). No cambia el estado: <c>EnValidacion</c> llega con el expediente inicial.
    /// </summary>
    public InvitacionAceptada AceptarInvitacion(string correo, DateTimeOffset ahora)
    {
        AsegurarAceptable(ahora);
        return new InvitacionAceptada(correo.Trim().ToLowerInvariant(), ahora);
    }

    /// <summary>
    /// Reglas comunes para aceptar: no aceptada aún, ni bloqueada ni terminal, con invitación vigente. El llamador la
    /// usa antes de mirar los textos legales para que <c>invitacion-ya-aceptada</c> tenga prioridad.
    /// </summary>
    public void AsegurarAceptable(DateTimeOffset ahora)
    {
        if (InvitacionAceptada)
            throw ReglaDeNegocioException.Conflicto("invitacion-ya-aceptada", "La invitación ya fue aceptada.");

        if (Estado is EstadoVinculo.Bloqueado or EstadoVinculo.Anulado or EstadoVinculo.Desvinculado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"No se puede aceptar una invitación de un vínculo {Estado}.");

        if (Invitacion is null)
            throw ReglaDeNegocioException.Conflicto("sin-invitacion", "Este vínculo no tiene una invitación enviada.");

        if (Invitacion.Expira <= ahora)
            throw ReglaDeNegocioException.Conflicto("invitacion-expirada", "El enlace de invitación expiró; pide a la empresa que lo reenvíe.");
    }

    /// <summary>
    /// Consentimientos que acompañan la aceptación (B-1, ADR-0009): la autorización y los términos <b>vigentes</b> de
    /// esta empresa siempre, y el aviso de la plataforma si <paramref name="requeridos"/> lo incluye (el tercero no ha
    /// aceptado su versión vigente). Cada uno debe venir aceptado y con la versión vigente; la evidencia (IP,
    /// user-agent, OTP, canal, correo) queda en el evento. Se registra antes de <see cref="AceptarInvitacion"/>.
    /// </summary>
    public ConsentimientosRegistrados RegistrarConsentimientos(IReadOnlyList<TextoLegal> requeridos,
        IReadOnlyList<ConsentimientoOtorgado> otorgados, EvidenciaDeConsentimiento evidencia, DateTimeOffset ahora)
    {
        AsegurarAceptable(ahora);

        if (requeridos.Any(t => t.Ambito == AmbitoTextoLegal.Empresa && t.EmpresaId != EmpresaId))
            throw new InvalidOperationException("Un texto legal requerido es de otra empresa.");

        var faltantes = new[] { TipoTextoLegal.AutorizacionEmpresa, TipoTextoLegal.TerminosEmpresa }
            .Where(tipo => requeridos.All(t => t.Tipo != tipo)).ToList();
        if (faltantes.Count > 0)
            throw ReglaDeNegocioException.Conflicto("textos-legales-faltantes",
                $"La empresa aún no ha publicado: {string.Join(", ", faltantes)}. No se puede aceptar la invitación todavía.");

        var duplicado = otorgados.GroupBy(o => o.Tipo).FirstOrDefault(g => g.Count() > 1);
        if (duplicado is not null)
            throw ReglaDeNegocioException.Validacion("consentimiento-duplicado", $"El consentimiento {duplicado.Key} viene repetido.");

        var registrados = new List<ConsentimientoRegistrado>();
        foreach (var texto in requeridos)
        {
            var otorgado = otorgados.FirstOrDefault(o => o.Tipo == texto.Tipo)
                ?? throw ReglaDeNegocioException.Validacion("consentimiento-faltante", $"Falta el consentimiento {texto.Tipo}.");

            if (!otorgado.Aceptado)
                throw ReglaDeNegocioException.Validacion("consentimiento-rechazado",
                    $"Para aceptar la invitación debes aceptar {texto.Tipo}.");

            if (otorgado.TextoVersion != texto.Version)
                throw ReglaDeNegocioException.Conflicto("texto-desactualizado",
                    $"{texto.Tipo} cambió: la versión vigente es la {texto.Version}. Vuelve a leerlo y acéptalo.");

            registrados.Add(new ConsentimientoRegistrado(texto.Tipo, texto.Id, texto.Version, texto.Hash));
        }

        if (string.IsNullOrWhiteSpace(evidencia.Correo))
            throw ReglaDeNegocioException.Validacion("correo-requerido", "La evidencia del consentimiento necesita el correo del tercero.");

        return new ConsentimientosRegistrados(registrados, evidencia, ahora);
    }

    public AtributosDeVinculoActualizados ActualizarAtributos(Dictionary<string, JsonElement> nuevos, DateTimeOffset ahora)
    {
        AsegurarNoTerminal("actualizar atributos");
        return new AtributosDeVinculoActualizados(Atributos, nuevos, ahora);
    }

    /// <summary>Privado de la empresa (P-5-3). Suspende sin borrar historial (B-5).</summary>
    public VinculoBloqueado Bloquear(string motivo, DateTimeOffset? hasta, DateTimeOffset ahora)
    {
        RequerirTexto(motivo, "motivo-requerido", "El bloqueo necesita un motivo.");
        if (Estado == EstadoVinculo.Bloqueado)
            throw ReglaDeNegocioException.Conflicto("ya-bloqueado", "El vínculo ya está bloqueado.");
        AsegurarNoTerminal("bloquear");

        if (hasta is not null && hasta <= ahora)
            throw ReglaDeNegocioException.Validacion("fecha-invalida", "La fecha de fin del bloqueo debe ser futura.");

        return new VinculoBloqueado(Estado, motivo.Trim(), hasta, ahora);
    }

    /// <summary>Restituye el estado previo al bloqueo (B-5).</summary>
    public VinculoDesbloqueado Desbloquear(string justificacion, DateTimeOffset ahora)
    {
        RequerirTexto(justificacion, "justificacion-requerida", "El desbloqueo necesita una justificación.");
        if (Estado != EstadoVinculo.Bloqueado)
            throw ReglaDeNegocioException.Conflicto("no-bloqueado", "El vínculo no está bloqueado.");

        return new VinculoDesbloqueado(EstadoPrevioAlBloqueo ?? EstadoVinculo.Invitado, justificacion.Trim(), ahora);
    }

    public VinculoDesvinculado Desvincular(string motivo, DateTimeOffset ahora)
    {
        RequerirTexto(motivo, "motivo-requerido", "La desvinculación necesita un motivo.");
        AsegurarNoTerminal("desvincular");
        return new VinculoDesvinculado(Estado, motivo.Trim(), ahora);
    }

    /// <summary>
    /// Desde <c>Desvinculado</c>: si alguna vez fue aprobado vuelve a validación (renovación); si no, a
    /// <c>Invitado</c> para empezar de nuevo.
    /// </summary>
    public VinculoReintegrado Reintegrar(string justificacion, DateTimeOffset ahora)
    {
        RequerirTexto(justificacion, "justificacion-requerida", "El reintegro necesita una justificación.");
        if (Estado != EstadoVinculo.Desvinculado)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", "Solo se reintegra un vínculo desvinculado.");

        return new VinculoReintegrado(TuvoAprobacion ? EstadoVinculo.EnValidacion : EstadoVinculo.Invitado, justificacion.Trim(), ahora);
    }

    /// <summary>
    /// Vence por un documento obligatorio vencido (RN-1): solo un vínculo que ya fue aprobado y está <c>Activo</c> o
    /// <c>PorVencer</c>; si ya está vencido, bloqueado o terminado, nada.
    /// </summary>
    public VinculoVencido? Vencer(string documento, DateTimeOffset ahora) =>
        TuvoAprobacion && Estado is EstadoVinculo.Activo or EstadoVinculo.PorVencer ? new VinculoVencido(documento, ahora) : null;

    /// <summary>Terminal; desde cualquier estado no terminal, incluido Bloqueado.</summary>
    public VinculoAnulado Anular(string motivo, DateTimeOffset ahora)
    {
        RequerirTexto(motivo, "motivo-requerido", "La anulación necesita un motivo.");
        AsegurarNoTerminal("anular");
        return new VinculoAnulado(Estado, motivo.Trim(), ahora);
    }

    /// <summary>
    /// Registra el expediente que se abre. Un solo expediente abierto; nunca bloqueado ni terminado (invariante 4). Un
    /// <c>Inicial</c> se abre desde <c>Invitado</c> sin aprobación previa (aceptación o apertura manual); una
    /// <c>Actualizacion</c> (RV-1), sobre un vínculo que ya tuvo aprobación (Activo, PorVencer o Vencido) y sin cambiar su
    /// estado mientras se revisa (A-2).
    /// </summary>
    public ExpedienteVinculado VincularExpediente(Guid expedienteId, TipoExpediente tipo, string folio, DateTimeOffset ahora)
    {
        if (Estado == EstadoVinculo.Bloqueado)
            throw ReglaDeNegocioException.Conflicto("vinculo-bloqueado", "El vínculo está bloqueado: no se abren expedientes.");
        AsegurarNoTerminal("abrir un expediente en");

        if (ExpedienteAbiertoId is { } abierto)
            throw ReglaDeNegocioException.Conflicto("expediente-abierto-existente", $"El vínculo ya tiene el expediente {abierto} abierto.");

        if (tipo is TipoExpediente.Actualizacion or TipoExpediente.Renovacion)
        {
            if (!TuvoAprobacion || Estado is not (EstadoVinculo.Activo or EstadoVinculo.PorVencer or EstadoVinculo.Vencido))
                throw ReglaDeNegocioException.Conflicto("estado-invalido",
                    $"Una actualización o renovación se abre sobre un vínculo que ya fue aprobado; este está {Estado}.");
            return new ExpedienteVinculado(expedienteId, tipo, folio, ahora);
        }


        if (Estado != EstadoVinculo.Invitado || TuvoAprobacion)
            throw ReglaDeNegocioException.Conflicto("estado-invalido",
                $"Un expediente inicial se abre sobre un vínculo Invitado sin aprobación previa; este está {Estado}.");

        return new ExpedienteVinculado(expedienteId, tipo, folio, ahora);
    }

    /// <summary>
    /// Cierra el expediente abierto (lo dispara el proceso de validación en la misma transacción). Aprobado ⇒ además
    /// <see cref="VinculoActivado"/>.
    /// </summary>
    public IReadOnlyList<object> CerrarExpediente(Guid expedienteId, ResultadoExpediente resultado, DateTimeOffset ahora)
    {
        if (ExpedienteAbiertoId != expedienteId)
            throw ReglaDeNegocioException.Conflicto("expediente-no-abierto", "Ese expediente no es el abierto del vínculo.");

        return resultado == ResultadoExpediente.Aprobado
            ? [new ExpedienteCerrado(expedienteId, resultado, ahora), new VinculoActivado(expedienteId, ahora)]
            : [new ExpedienteCerrado(expedienteId, resultado, ahora)];
    }

    // ---------- Evolución (Marten) ----------

    public static Vinculo Create(VinculoCreado e) => new()
    {
        Id = e.VinculoId,
        EmpresaId = e.EmpresaId,
        TerceroId = e.TerceroId,
        TipoVinculoId = e.TipoVinculoId,
        FlujoId = e.FlujoId,
        Estado = EstadoVinculo.Invitado,
        Atributos = e.Atributos,
        Origen = e.Origen,
        Referencias = e.ReferenciaExterna is null ? [] : [e.ReferenciaExterna],
        ClavesReferencia = e.ReferenciaExterna is null ? [] : [e.ReferenciaExterna.Clave()],
        CorreoTercero = e.CorreoInvitacion,
        CreadoEn = e.Fecha,
        ActualizadoEn = e.Fecha
    };

    public void Apply(ReferenciaExternaAgregada e)
    {
        var referencia = new ReferenciaExterna(e.Sistema, e.Id);
        Referencias.Add(referencia);
        ClavesReferencia.Add(referencia.Clave());
        ActualizadoEn = e.Fecha;
    }

    public void Apply(InvitacionEnviada e)
    {
        Invitacion = new InvitacionVigente(e.CorreoDestino, e.Expira, (Invitacion?.Envios ?? 0) + 1);
        CorreoTercero = e.CorreoDestino;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ConsentimientosRegistrados e)
    {
        Consentimientos.AddRange(e.Consentimientos);
        ActualizadoEn = e.Fecha;
    }

    public void Apply(InvitacionAceptada e)
    {
        InvitacionAceptada = true;
        InvitacionAceptadaEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(AtributosDeVinculoActualizados e)
    {
        Atributos = e.Despues;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExpedienteVinculado e)
    {
        ExpedienteAbiertoId = e.ExpedienteId;
        ExpedienteAbiertoTipo = e.Tipo;
        if (Estado == EstadoVinculo.Invitado)
            Estado = EstadoVinculo.EnValidacion;
        if (e.Tipo == TipoExpediente.Renovacion)
            RenovacionPorVigenciaPendiente = false;
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(ExpedienteCerrado e)
    {
        ExpedienteAbiertoId = null;
        ExpedienteAbiertoTipo = null;
        UltimoExpedienteId = e.ExpedienteId;
        UltimoResultado = e.Resultado;
        if (e.Resultado != ResultadoExpediente.Aprobado && Estado == EstadoVinculo.EnValidacion)
            Estado = EstadoVinculo.Invitado;
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoActivado e)
    {
        Estado = EstadoVinculo.Activo;
        TuvoAprobacion = true;
        UltimaAprobacionEn = e.Fecha;
        RenovacionPorVigenciaPendiente = false;
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoVencido e)
    {
        Estado = EstadoVinculo.Vencido;
        // Con la renovación ya abierta (la del pre-vencimiento) no hay otra por abrir: la misma sigue su curso (VV-3).
        RenovacionPorVigenciaPendiente = e.Causa == CausaDeVencimiento.Vigencia && !TieneRenovacionAbierta;
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoBloqueado e)
    {
        EstadoPrevioAlBloqueo = e.EstadoPrevio;
        Estado = EstadoVinculo.Bloqueado;
        Bloqueo = new Bloqueo(e.Motivo, e.Fecha, e.Hasta);
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoDesbloqueado e)
    {
        Estado = e.EstadoRestituido;
        EstadoPrevioAlBloqueo = null;
        Bloqueo = null;
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoDesvinculado e)
    {
        CerrarExpedientePorTerminacion();
        Estado = EstadoVinculo.Desvinculado;
        EstadoPrevioAlBloqueo = null;
        Bloqueo = null;
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoReintegrado e)
    {
        Estado = e.EstadoResultante;
        if (e.EstadoResultante == EstadoVinculo.Invitado)
        {
            InvitacionAceptada = false;
            InvitacionAceptadaEn = null;
        }

        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    public void Apply(VinculoAnulado e)
    {
        CerrarExpedientePorTerminacion();
        Estado = EstadoVinculo.Anulado;
        EstadoPrevioAlBloqueo = null;
        Bloqueo = null;
        RecalcularProximaRevision();
        ActualizadoEn = e.Fecha;
    }

    // ---------- Reglas comunes ----------

    /// <summary>Un vínculo terminado anula su expediente abierto (el proceso emite <c>ExpedienteAnulado</c> en su stream).</summary>
    private void CerrarExpedientePorTerminacion()
    {
        if (ExpedienteAbiertoId is not { } abierto)
            return;
        UltimoExpedienteId = abierto;
        UltimoResultado = ResultadoExpediente.Anulado;
        ExpedienteAbiertoId = null;
        ExpedienteAbiertoTipo = null;
    }

    private void AsegurarNoTerminal(string accion)
    {
        if (Terminales.Contains(Estado))
            throw ReglaDeNegocioException.Conflicto("estado-terminal", $"No se puede {accion} un vínculo {Estado}.");
    }

    private static void ValidarReferencia(ReferenciaExterna referencia)
    {
        if (string.IsNullOrWhiteSpace(referencia.Sistema) || string.IsNullOrWhiteSpace(referencia.Id))
            throw ReglaDeNegocioException.Validacion("referencia-externa-invalida", "La referencia externa necesita sistema e id.");
    }

    private static void RequerirTexto(string? valor, string codigo, string mensaje)
    {
        if (string.IsNullOrWhiteSpace(valor))
            throw ReglaDeNegocioException.Validacion(codigo, mensaje);
    }
}
