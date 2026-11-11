using System.Text.Json;
using System.Text.Json.Serialization;
using JasperFx.Events;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>
/// Evento público v1 ya registrado (docs/05 §6.2, NW-1). Lo escribe la suscripción de eventos de Marten en la misma
/// transacción que su avance, en el orden del event store: <see cref="Cursor"/> es la secuencia global del evento de
/// dominio que lo originó, así que es monotónica por tenant y los registrados forman siempre un prefijo (el modo pull no
/// se salta ninguno). Documento global (esquema <c>validacion</c>) con <see cref="TenantId"/> e índice por tenant y cursor.
/// </summary>
public sealed class EventoPublico
{
    /// <summary>Id del evento público = id del evento de dominio de origen (estable: sirve para deduplicar).</summary>
    public Guid Id { get; set; }

    public Guid TenantId { get; set; }

    public Guid? EmpresaId { get; set; }

    public long Cursor { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public int Version { get; set; } = 1;

    public DateTimeOffset OcurridoEn { get; set; }

    public JsonElement Datos { get; set; }

    public DateTimeOffset RegistradoEn { get; set; }

    public EventoPublicoDto ADto() => new(Id, Tipo, Version, OcurridoEn, TenantId, EmpresaId, Cursor.ToString(System.Globalization.CultureInfo.InvariantCulture), Datos);

    public static EventoPublico Desde(IEvent origen, EventoPublicoMapeado mapeado, DateTimeOffset ahora) => new()
    {
        Id = origen.Id,
        TenantId = mapeado.TenantId,
        EmpresaId = mapeado.EmpresaId,
        Cursor = origen.Sequence,
        Tipo = mapeado.Tipo,
        OcurridoEn = origen.Timestamp,
        Datos = JsonSerializer.SerializeToElement(mapeado.Datos, mapeado.Datos.GetType(), JsonEventos.Opciones),
        RegistradoEn = ahora
    };
}

/// <summary>Resultado de un mapeo: tipo público, tenant, empresa y el <c>Datos*Dto</c> del tipo.</summary>
public sealed record EventoPublicoMapeado(string Tipo, Guid TenantId, Guid? EmpresaId, object Datos);

/// <summary>JSON de los eventos públicos: el mismo en webhooks y en el modo pull (camelCase, enums como texto).</summary>
public static class JsonEventos
{
    public static readonly JsonSerializerOptions Opciones = Crear();

    private static JsonSerializerOptions Crear()
    {
        var opciones = new JsonSerializerOptions(JsonSerializerDefaults.Web) { DefaultIgnoreCondition = JsonIgnoreCondition.Never };
        opciones.Converters.Add(new JsonStringEnumConverter());
        opciones.MakeReadOnly(populateMissingResolver: true);
        return opciones;
    }

    /// <summary>Cuerpo exacto que se firma y se envía al webhook.</summary>
    public static string Cuerpo(EventoPublicoDto evento) => JsonSerializer.Serialize(evento, Opciones);
}

/// <summary>
/// Traduce eventos de dominio a eventos públicos. Cada evento de dominio produce a lo sumo uno; los mapeadores se
/// registran en DI y la suscripción usa el primero que responda. Para agregar un catálogo (p. ej. <c>certificado.emitido</c>)
/// basta otro <see cref="IMapeadorDeEventos"/> registrado con <c>AddSingleton&lt;IMapeadorDeEventos, …&gt;()</c> (NW-2).
/// </summary>
public interface IMapeadorDeEventos
{
    /// <summary>Tipos públicos que puede producir (validan las suscripciones y se listan en <c>GET /eventos/tipos</c>).</summary>
    IReadOnlyList<TipoEventoPublicoDto> Catalogo { get; }

    ValueTask<EventoPublicoMapeado?> MapearAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct);
}

/// <summary>Catálogo v1 de los eventos del proceso de validación (docs/05 §7).</summary>
public static class CatalogoEventosPublicos
{
    public static readonly IReadOnlyList<TipoEventoPublicoDto> DelProceso =
    [
        new("vinculo.creado", "Se creó el vínculo (empresa, tercero, tipo)."),
        new("vinculo.invitacion_enviada", "Se envió (o reenvió) la invitación al tercero."),
        new("vinculo.invitacion_aceptada", "El tercero aceptó la invitación y los textos legales."),
        new("vinculo.activado", "El vínculo quedó Activo al aprobarse su expediente."),
        new("vinculo.bloqueado", "El vínculo se bloqueó."),
        new("vinculo.desbloqueado", "El vínculo se desbloqueó."),
        new("vinculo.por_vencer", "Llegó un aviso de la vigencia del vínculo (pre-vencimiento): vence pronto y el tercero debe ratificar su información antes de la fecha."),
        new("vinculo.vencido", "El vínculo venció (por su vigencia o por un documento obligatorio) y tiene su renovación abierta."),
        new("vinculo.desvinculado", "El vínculo terminó (desvinculación)."),
        new("vinculo.reintegrado", "Se reintegró un vínculo desvinculado (revalida o restituye su estado)."),
        new("vinculo.anulado", "El vínculo se anuló."),
        new("vinculo.referencia_agregada", "Otro sistema integrado comparte el vínculo."),
        new("vinculo.contactos_actualizados", "Cambiaron los contactos del tercero con la empresa (comercial, pagos, cumplimiento, notificaciones)."),
        new("vinculo.cuenta_designada_propuesta", "El tercero eligió una cuenta bancaria para cobrarle a la empresa; espera aprobación."),
        new("vinculo.cuenta_designada", "La empresa aprobó la cuenta bancaria designada (cuatro ojos)."),
        new("vinculo.cuenta_designada_rechazada", "La empresa rechazó la cuenta bancaria propuesta."),
        new("expediente.abierto", "Se abrió un expediente de validación."),
        new("expediente.en_revision", "El expediente pasó a revisión de las áreas."),
        new("expediente.observado", "El expediente volvió al tercero con observaciones."),
        new("expediente.reenviado", "El tercero (o el integrador) respondió las observaciones."),
        new("expediente.aprobado", "El expediente se aprobó."),
        new("expediente.rechazado", "El expediente se rechazó."),
        new("expediente.caducado", "Venció el plazo de cargue con el expediente en manos del tercero."),
        new("expediente.reabierto", "La empresa reabrió un expediente caducado con un plazo nuevo."),
        new("expediente.desaprobado", "Un administrador devolvió a revisión lo ya aprobado: una etapa, la fase inicial o el expediente completo."),
        new("expediente.sla_por_vencer", "El plazo de revisión de un área (SLA) está por vencer: ya consumió el porcentaje de aviso (campo area y slaVence)."),
        new("expediente.sla_incumplido", "Un área no decidió dentro de su plazo de revisión (SLA) (campo area y slaVence)."),
        new("documento.compartido", "Una versión de un documento quedó compartida con el vínculo."),
        new("documento.por_vencer", "Un documento compartido está por vencer para el vínculo."),
        new("documento.vencido", "Un documento compartido venció para el vínculo.")
    ];
}

/// <summary>
/// Mapeo de los eventos de <c>Vinculo</c>, <c>Expediente</c> y <c>Documento</c> (NW-1). Los datos públicos nunca llevan
/// revisores, correos, IP ni nombres de personas: solo ids, estados, códigos, motivos tal como se comunicaron al tercero y
/// las referencias externas del vínculo para que cada sistema ubique su registro.
/// </summary>
public sealed class MapeadorDelProcesoDeValidacion : IMapeadorDeEventos
{
    public IReadOnlyList<TipoEventoPublicoDto> Catalogo => CatalogoEventosPublicos.DelProceso;

    public async ValueTask<EventoPublicoMapeado?> MapearAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct) =>
        evento.Data switch
        {
            VinculoCreado or InvitacionEnviada or InvitacionAceptada or VinculoActivado or VinculoBloqueado or VinculoDesbloqueado
                or VinculoDesvinculado or VinculoReintegrado or VinculoAnulado or ReferenciaExternaAgregada or ContactosDeVinculoActualizados or CuentaDesignadaPropuesta
                or CuentaDesignadaAprobada or CuentaDesignadaRechazada or VinculoPorVencer or VinculoVencido => await DeVinculoAsync(evento, contexto, ct),
            ExpedienteAbierto or ExpedienteEnviadoARevision or RevisionReanudada or ExpedienteObservado or ExpedienteReenviado
                or ExpedienteAprobado or ExpedienteRechazado or ExpedienteCaducado or ExpedienteReabierto or ExpedienteDesaprobado or EtapaReabierta or SlaPorVencer or SlaIncumplido
                => await DeExpedienteAsync(evento, contexto, ct),
            VersionCompartida or VencimientoAvisado or VersionVencida => await DeDocumentoAsync(evento, contexto, ct),
            _ => null
        };

    private static async Task<EventoPublicoMapeado?> DeVinculoAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId))
            return null;

        var vinculo = await contexto.VinculoAsync(tenantId, evento.StreamId, ct);
        var creado = evento.Data as VinculoCreado;
        var empresaId = vinculo?.EmpresaId ?? creado?.EmpresaId;
        if (empresaId is null)
            return null;

        var id = evento.StreamId;
        var referencias = Referencias(vinculo, creado?.ReferenciaExterna);
        object? datos = evento.Data switch
        {
            VinculoCreado c => new DatosVinculoCreadoDto(id, referencias, c.TerceroId,
                await contexto.TerceroAsync(c.TerceroId, ct) is { } tercero
                    ? new IdentificacionDto(tercero.Identificacion.Pais, tercero.Identificacion.Tipo, tercero.Identificacion.Numero,
                        tercero.Identificacion.DigitoVerificacion)
                    : null,
                (await contexto.TipoVinculoAsync(tenantId, c.TipoVinculoId, ct))?.Codigo ?? string.Empty, c.FlujoId, c.Origen.ToString(),
                nameof(EstadoVinculo.Invitado)),
            InvitacionEnviada i => new DatosInvitacionEnviadaDto(id, referencias, i.Canal, i.Expira),
            InvitacionAceptada a => new DatosInvitacionAceptadaDto(id, referencias, a.Fecha,
                [.. (vinculo?.Consentimientos ?? []).Select(x => new ConsentimientoEnEventoDto(x.Tipo.ToString(), x.Version))]),
            VinculoActivado a => Estado(id, referencias, EstadoVinculo.Activo, null, null, null, a.ExpedienteId),
            VinculoBloqueado b => Estado(id, referencias, EstadoVinculo.Bloqueado, b.EstadoPrevio, b.Motivo, b.Hasta, null),
            VinculoDesbloqueado d => Estado(id, referencias, d.EstadoRestituido, EstadoVinculo.Bloqueado, d.Justificacion, null, null),
            VinculoPorVencer p => Estado(id, referencias, EstadoVinculo.PorVencer, EstadoVinculo.Activo, null,
                new DateTimeOffset(p.VigenteHasta.ToDateTime(TimeOnly.MinValue), TimeSpan.Zero), null),
            VinculoVencido v => Estado(id, referencias, EstadoVinculo.Vencido, null, v.Documento, null, null),
            VinculoDesvinculado d => Estado(id, referencias, EstadoVinculo.Desvinculado, d.EstadoPrevio, d.Motivo, null, null),
            VinculoReintegrado r => Estado(id, referencias, r.EstadoResultante, EstadoVinculo.Desvinculado, r.Justificacion, null, null),
            VinculoAnulado a => Estado(id, referencias, EstadoVinculo.Anulado, a.EstadoPrevio, a.Motivo, null, null),
            ReferenciaExternaAgregada r => new DatosReferenciaAgregadaDto(id, referencias, new ReferenciaExternaDto(r.Sistema, r.Id)),
            ContactosDeVinculoActualizados c => new DatosContactosActualizadosDto(id, referencias, c.Despues.ADto()),
            CuentaDesignadaPropuesta p => Cuenta(id, referencias, p.Cuenta, EstadoCuentaDesignada.Propuesta, null),
            CuentaDesignadaAprobada a => (a.Cuenta ?? CuentaDe(vinculo, a.CuentaId)) is { } aprobada
                ? Cuenta(id, referencias, aprobada, EstadoCuentaDesignada.Aprobada, null)
                : null,
            CuentaDesignadaRechazada r => (r.Cuenta ?? CuentaDe(vinculo, r.CuentaId)) is { } rechazada
                ? Cuenta(id, referencias, rechazada, EstadoCuentaDesignada.Rechazada, r.Motivo)
                : null,
            _ => null
        };

        var tipo = evento.Data switch
        {
            VinculoCreado => "vinculo.creado",
            InvitacionEnviada => "vinculo.invitacion_enviada",
            InvitacionAceptada => "vinculo.invitacion_aceptada",
            VinculoActivado => "vinculo.activado",
            VinculoBloqueado => "vinculo.bloqueado",
            VinculoDesbloqueado => "vinculo.desbloqueado",
            VinculoPorVencer => "vinculo.por_vencer",
            VinculoVencido => "vinculo.vencido",
            VinculoDesvinculado => "vinculo.desvinculado",
            VinculoReintegrado => "vinculo.reintegrado",
            VinculoAnulado => "vinculo.anulado",
            ContactosDeVinculoActualizados => "vinculo.contactos_actualizados",
            CuentaDesignadaPropuesta => "vinculo.cuenta_designada_propuesta",
            CuentaDesignadaAprobada => "vinculo.cuenta_designada",
            CuentaDesignadaRechazada => "vinculo.cuenta_designada_rechazada",
            _ => "vinculo.referencia_agregada"
        };
        return datos is null ? null : new EventoPublicoMapeado(tipo, tenantId, empresaId, datos);
    }

    /// <summary>La cuenta del evento según el vínculo ya proyectado: la designada o la última rechazada (para el ERP, enmascarada).</summary>
    private static CuentaParaDesignar? CuentaDe(Vinculo? vinculo, Guid cuentaId) =>
        new[] { vinculo?.CuentaDesignada, vinculo?.CuentaRechazada, vinculo?.CuentaPropuesta }.FirstOrDefault(c => c?.Cuenta.CuentaId == cuentaId)?.Cuenta;

    private static DatosCuentaDesignadaDto Cuenta(Guid id, IReadOnlyList<ReferenciaExternaDto> referencias, CuentaParaDesignar cuenta,
        EstadoCuentaDesignada estado, string? motivo) =>
        new(id, referencias, cuenta.CuentaId, cuenta.Banco, cuenta.TipoCuenta, Dominio.Terceros.CuentaBancaria.Enmascarar(cuenta.Ultimos4), cuenta.Titular,
            cuenta.IdentificacionTitular, cuenta.Moneda, estado.ToString(), motivo, cuenta.BancoCodigo, cuenta.BancoSwift);

    private static DatosEstadoVinculoDto Estado(Guid id, IReadOnlyList<ReferenciaExternaDto> referencias, EstadoVinculo estado, EstadoVinculo? anterior,
        string? motivo, DateTimeOffset? hasta, Guid? expedienteId) =>
        new(id, referencias, estado.ToString(), anterior?.ToString(), motivo, hasta, expedienteId);

    private static async Task<EventoPublicoMapeado?> DeExpedienteAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct)
    {
        if (!Guid.TryParse(evento.TenantId, out var tenantId))
            return null;

        var expediente = await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct);
        var abierto = evento.Data as ExpedienteAbierto;
        var empresaId = expediente?.EmpresaId ?? abierto?.EmpresaId;
        var vinculoId = expediente?.VinculoId ?? abierto?.VinculoId;
        if (empresaId is null || vinculoId is not { } idVinculo)
            return null;

        var referencias = Referencias(await contexto.VinculoAsync(tenantId, idVinculo, ct), null);
        var folio = expediente?.Folio ?? abierto?.Folio ?? string.Empty;
        var tipoExpediente = (expediente?.Tipo ?? abierto?.Tipo ?? TipoExpediente.Inicial).ToString();
        var id = evento.StreamId;

        IReadOnlyList<RequisitoEnEventoDto> Requisitos(IReadOnlyList<Guid>? ids) =>
            [.. (ids ?? []).Select(r => new RequisitoEnEventoDto(r, expediente?.Requisitos.FirstOrDefault(x => x.Id == r)?.Codigo ?? string.Empty))];

        DatosExpedienteDto Datos(EstadoExpediente estado) => new(id, folio, idVinculo, referencias, tipoExpediente, estado.ToString());

        (string Tipo, DatosExpedienteDto Datos)? resultado = evento.Data switch
        {
            ExpedienteAbierto a => ("expediente.abierto", Datos(EstadoExpediente.Abierto) with
            {
                TerceroId = a.TerceroId,
                TipoVinculo = a.TipoVinculoCodigo
            }),
            ExpedienteEnviadoARevision => ("expediente.en_revision", Datos(EstadoExpediente.EnRevision) with { Causa = "CargaCompleta" }),
            RevisionReanudada => ("expediente.en_revision", Datos(EstadoExpediente.EnRevision) with { Causa = "RevisionReanudada" }),
            ExpedienteObservado o => ("expediente.observado", Datos(EstadoExpediente.Observado) with { Requisitos = Requisitos(o.RequisitosObservados) }),
            ExpedienteReenviado r => ("expediente.reenviado", Datos(EstadoExpediente.EnRevision) with
            {
                Causa = r.Origen.ToString(),
                Requisitos = Requisitos(r.RequisitosReabiertos)
            }),
            ExpedienteAprobado => ("expediente.aprobado", Datos(EstadoExpediente.Aprobado)),
            ExpedienteCaducado c => ("expediente.caducado", Datos(EstadoExpediente.Caducado) with { Causa = "PlazoDeCargue", PlazoHasta = c.PlazoHasta }),
            ExpedienteReabierto r => ("expediente.reabierto", Datos(r.EstadoRestituido) with { PlazoHasta = r.PlazoHasta }),
            ExpedienteDesaprobado d => ("expediente.desaprobado", Datos(EstadoExpediente.EnRevision) with
            {
                Causa = d.FaseInicial ? "FaseInicial" : "ExpedienteAprobado",
                Justificacion = d.Justificacion,
                Etapa = d.Orden
            }),
            EtapaReabierta d => ("expediente.desaprobado", Datos(EstadoExpediente.EnRevision) with
            {
                Causa = d.FaseInicial ? "FaseInicial" : "EtapaReabierta",
                Justificacion = d.Justificacion,
                Etapa = d.Orden
            }),
            SlaPorVencer s => ("expediente.sla_por_vencer", Datos(EstadoExpediente.EnRevision) with { Area = expediente?.BuscarArea(s.AreaId)?.Codigo, SlaVence = s.Vence }),
            SlaIncumplido s => ("expediente.sla_incumplido", Datos(EstadoExpediente.EnRevision) with { Area = expediente?.BuscarArea(s.AreaId)?.Codigo, SlaVence = s.Vence }),
            ExpedienteRechazado r => ("expediente.rechazado", Datos(EstadoExpediente.Rechazado) with
            {
                Motivos = [.. r.Motivos.Select(m => new MotivoEnEventoDto(m.Codigo, m.TextoAlTercero))],
                Justificacion = r.Justificacion
            }),
            _ => null
        };

        return resultado is { } r2 ? new EventoPublicoMapeado(r2.Tipo, tenantId, empresaId, r2.Datos) : null;
    }

    private static async Task<EventoPublicoMapeado?> DeDocumentoAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct)
    {
        var documento = await contexto.DocumentoAsync(evento.StreamId, ct);
        if (documento is null)
            return null;

        var (numero, vinculoId) = evento.Data switch
        {
            VersionCompartida c => (c.Numero, c.VinculoId),
            VencimientoAvisado a => (a.Numero, a.VinculoId),
            VersionVencida v => (v.Numero, v.VinculoId),
            _ => (0, Guid.Empty)
        };

        // Tenant y empresa del vínculo: los trae la compartición (la del evento o la registrada en el documento).
        var destino = evento.Data is VersionCompartida compartida
            ? new Comparticion(compartida.Numero, compartida.TenantId, compartida.EmpresaId, compartida.VinculoId, compartida.Origen, compartida.Fecha)
            : documento.Comparticiones.LastOrDefault(c => c.VinculoId == vinculoId);
        if (destino is null)
            return null;

        var tipoDocumento = documento.TipoCanonico
                            ?? (documento.TipoDocumentoTenantId is { } tipoId ? (await contexto.TipoDocumentoAsync(destino.TenantId, tipoId, ct))?.Codigo : null)
                            ?? documento.NombreTipo;
        var referencias = Referencias(await contexto.VinculoAsync(destino.TenantId, vinculoId, ct), null);
        var datos = new DatosDocumentoDto(documento.Id, documento.TerceroId, tipoDocumento, numero, vinculoId, referencias);

        (string Tipo, DatosDocumentoDto Datos) resultado = evento.Data switch
        {
            VersionCompartida c => ("documento.compartido", datos with { Causa = Causa(c.Origen) }),
            VencimientoAvisado a => ("documento.por_vencer", datos with { VigenteHasta = a.VigenteHasta, DiasRestantes = a.DiasRestantes }),
            VersionVencida v => ("documento.vencido", datos with { VigenteHasta = v.VigenteHasta, DiasGracia = v.DiasGracia }),
            _ => ("", datos)
        };
        return new EventoPublicoMapeado(resultado.Tipo, destino.TenantId, destino.EmpresaId, resultado.Datos);
    }

    private static string Causa(OrigenComparticion origen) => origen switch
    {
        OrigenComparticion.AlAceptarInvitacion => "InvitacionAceptada",
        OrigenComparticion.Automatica => "VersionNueva",
        _ => origen.ToString()
    };

    private static IReadOnlyList<ReferenciaExternaDto> Referencias(Vinculo? vinculo, ReferenciaExterna? delEvento) =>
        vinculo is not null
            ? [.. vinculo.Referencias.Select(r => new ReferenciaExternaDto(r.Sistema, r.Id))]
            : delEvento is null ? [] : [new ReferenciaExternaDto(delEvento.Sistema, delEvento.Id)];
}
