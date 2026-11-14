using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Documentos;

// Vigencias, avisos y vencimientos de versiones (TD-6, docs/02 §8). Todo viaja por la cola local durable "vigencias":
// el cálculo lo piden el outbox de la carga/compartición y los mensajes programados (ScheduleAsync) quedan guardados en la
// misma transacción que el evento de vigencia, así sobreviven a reinicios. Los manejadores son idempotentes y vuelven a
// mirar el estado al dispararse (una vigencia recalculada deja sin efecto los mensajes viejos).

/// <summary>Calcula (o recalcula) las vigencias de la versión actual del documento para cada vínculo con el que está compartida.</summary>
public sealed record CalcularVigenciasDelDocumento(Guid DocumentoId);

/// <summary>El vínculo recibió documentos fuera del outbox de la bóveda (al aceptar o al abrir el expediente): calcula los suyos.</summary>
public sealed record CalcularVigenciasDelVinculo(Guid TerceroId, Guid VinculoId);

/// <summary>Cambió la frescura, los avisos o la gracia del tipo: recalcula los documentos que lo usan.</summary>
public sealed record RecalcularVigenciasDelTipo(Guid TenantId, Guid TipoDocumentoTenantId);

/// <summary>Programado para el día de aviso: un solo correo por tercero y fecha con todos sus documentos que vencen (docs/02 §8.3).</summary>
public sealed record EnviarAvisosDeVencimiento(Guid TerceroId, DateOnly Fecha);

/// <summary>Programado para el día siguiente al fin de la gracia: marca la versión <c>Vencida</c> para el vínculo si sigue igual.</summary>
public sealed record VencerVersionDeDocumento(Guid DocumentoId, int Numero, Guid VinculoId, DateOnly VigenteHasta, int DiasGracia);

public enum EstadoAviso
{
    Programado,
    Enviado,
    SinDocumentos
}

/// <summary>
/// Aviso de vencimiento programado para un tercero y una fecha (tenant técnico <c>boveda</c>). Su id es la clave
/// <c>{tercero}|{fecha}</c>: varias vigencias que avisan el mismo día comparten un solo mensaje y un solo correo. Lleva concurrencia
/// optimista: aunque la cola procese en paralelo varios mensajes del mismo aviso, solo uno lo marca y envía (OC-1).
/// </summary>
public sealed class AvisoDeVencimiento
{
    public string Id { get; set; } = string.Empty;

    public Guid TerceroId { get; set; }

    public DateOnly Fecha { get; set; }

    public EstadoAviso Estado { get; set; }

    public DateTimeOffset ProgramadoEn { get; set; }

    public DateTimeOffset? EnviadoEn { get; set; }

    public int Documentos { get; set; }

    public static string Clave(Guid terceroId, DateOnly fecha) => $"{terceroId:N}|{fecha:yyyy-MM-dd}";
}

/// <summary>Cuándo se dispara cada mensaje: avisos a las 8:00 y vencimientos a las 00:00 del día siguiente a la gracia, ambos en la zona de negocio configurada (Bogotá: 13:00 y 05:00 UTC).</summary>
public static class CalendarioDeVigencias
{
    public static DateTimeOffset MomentoDeAviso(DateOnly fecha) => CalculadoraDeVigencia.MomentoDeAviso(fecha);

    public static DateTimeOffset MomentoDeVencimiento(DateOnly finDeGracia) => CalculadoraDeVigencia.MomentoDeVencimiento(finDeGracia);

    /// <summary>
    /// Sesión de escritura del tenant técnico <c>boveda</c> enlistada en el outbox del mensaje que se está manejando. Dentro
    /// de un manejador, Wolverine abre la sesión del outbox con el tenant del contexto del mensaje (<c>*DEFAULT*</c> si el
    /// sobre no trae tenant) en lugar del pedido: se fija antes el del contexto (y así lo heredan los mensajes que publique).
    /// </summary>
    public static Marten.IDocumentSession SesionDeBoveda(ISesiones sesiones, IMessageContext mensajes)
    {
        mensajes.TenantId = TenantsTecnicos.Boveda;
        return sesiones.Boveda(mensajes);
    }

    /// <summary>Programa en la cola durable; si el momento ya pasó, se publica de una vez.</summary>
    public static async Task ProgramarAsync<T>(IMessageBus mensajes, T mensaje, DateTimeOffset cuando, DateTimeOffset ahora) where T : class
    {
        if (cuando <= ahora)
            await mensajes.PublishAsync(mensaje);
        else
            await mensajes.ScheduleAsync(mensaje, cuando);
    }
}

public static class CalcularVigenciasDelDocumentoHandler
{
    /// <summary>
    /// Toma el stream con concurrencia optimista (si pierde, Wolverine reintenta el mensaje), resuelve el tipo del tenant de
    /// cada vínculo con el que está compartida la versión actual, registra las vigencias que cambiaron y programa sus avisos
    /// (uno por tercero y fecha) y su vencimiento, todo en la misma transacción (outbox).
    /// </summary>
    public static async Task Handle(CalcularVigenciasDelDocumento mensaje, ISesiones sesiones, IMessageContext mensajes, TimeProvider reloj,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = CalendarioDeVigencias.SesionDeBoveda(sesiones, mensajes);
        var stream = await sesion.Events.FetchForWriting<Documento>(mensaje.DocumentoId, ct);
        if (stream.Aggregate is not { VersionActual: not null } documento)
            return;

        var destinos = await DestinosAsync(sesion, documento, ct);
        var eventos = documento.CalcularVigencias(destinos, ahora);
        if (eventos.Count == 0)
            return;

        foreach (var evento in eventos)
            stream.AppendOne(evento);

        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        var programados = new HashSet<DateOnly>();
        foreach (var evento in eventos.Where(e => e.VigenteHasta is not null))
        {
            var hasta = evento.VigenteHasta!.Value;
            foreach (var fecha in CalculadoraDeVigencia.FechasDeAviso(hasta, evento.DiasAviso).Where(f => f >= hoy && programados.Add(f)))
            {
                var clave = AvisoDeVencimiento.Clave(documento.TerceroId, fecha);
                if (await sesion.LoadAsync<AvisoDeVencimiento>(clave, ct) is not null)
                    continue;
                // Insert (no Store): si otro cálculo del mismo tercero programó ese día a la vez, este guardado falla completo (incluido el
                // mensaje programado del outbox) y Wolverine lo reintenta; al volver ya ve el aviso y no programa un segundo mensaje.
                sesion.Insert(new AvisoDeVencimiento { Id = clave, TerceroId = documento.TerceroId, Fecha = fecha, ProgramadoEn = ahora });
                await CalendarioDeVigencias.ProgramarAsync(mensajes, new EnviarAvisosDeVencimiento(documento.TerceroId, fecha),
                    CalendarioDeVigencias.MomentoDeAviso(fecha), ahora);
            }

            var fin = CalculadoraDeVigencia.FinDeGracia(hasta, evento.DiasGracia)!.Value;
            await CalendarioDeVigencias.ProgramarAsync(mensajes,
                new VencerVersionDeDocumento(documento.Id, evento.Numero, evento.VinculoId, hasta, evento.DiasGracia),
                CalendarioDeVigencias.MomentoDeVencimiento(fin), ahora);
        }

        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Vínculos con los que está compartida la versión actual y el tipo del tenant que define su vigencia: el del documento
    /// <c>PorVinculo</c> o, en un global, el tipo activo y global del canónico en el tenant de cada vínculo (si hay varios, el
    /// que lo hace vencer antes).
    /// </summary>
    internal static async Task<IReadOnlyList<DestinoDeVigencia>> DestinosAsync(IQuerySession consulta, Documento documento, CancellationToken ct)
    {
        var numero = documento.VersionActual!.Value;
        var comparticiones = documento.Comparticiones.Where(c => c.Numero == numero).DistinctBy(c => c.VinculoId).ToList();
        if (comparticiones.Count == 0)
            return [];

        List<TipoDocumentoTenant> tipos;
        if (documento.Ambito == AmbitoDocumento.PorVinculo)
        {
            var tipoId = documento.TipoDocumentoTenantId!.Value;
            tipos = [.. await consulta.Query<TipoDocumentoTenant>().Where(t => t.AnyTenant() && t.Id == tipoId).ToListAsync(ct)];
        }
        else
        {
            var codigo = documento.TipoCanonico!;
            var tenants = comparticiones.Select(c => c.TenantId).Distinct().ToArray();
            tipos = [.. (await consulta.Query<TipoDocumentoTenant>()
                    .Where(t => t.AnyTenant() && t.TipoCanonico == codigo && t.Activo && t.TenantId.IsOneOf(tenants))
                    .ToListAsync(ct))
                .Where(t => t.Ambito == AmbitoDocumento.Global)];
        }

        var version = documento.VersionRequerida(numero);
        var destinos = new List<DestinoDeVigencia>();
        foreach (var comparticion in comparticiones)
        {
            var tipo = tipos.Where(t => t.TenantId == comparticion.TenantId)
                .OrderBy(t => CalculadoraDeVigencia.Calcular(t.ReglaFrescura, version.CargadaEn, version.Datos).VigenteHasta ?? DateOnly.MaxValue)
                .ThenBy(t => t.Codigo, StringComparer.Ordinal)
                .FirstOrDefault();
            if (tipo is not null)
                destinos.Add(new DestinoDeVigencia(comparticion.TenantId, comparticion.EmpresaId, comparticion.VinculoId, tipo));
        }

        return destinos;
    }
}

public static class CalcularVigenciasDelVinculoHandler
{
    /// <summary>Pide el cálculo de cada documento del tercero cuya versión actual está compartida con el vínculo.</summary>
    public static async Task Handle(CalcularVigenciasDelVinculo mensaje, ISesiones sesiones, IMessageContext mensajes, CancellationToken ct)
    {
        var terceroId = mensaje.TerceroId;
        await using var consulta = sesiones.ConsultaBoveda();
        var documentos = await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
        foreach (var documento in documentos.Where(d => d.VersionActual is { } n && d.EstaCompartida(n, mensaje.VinculoId)))
            await mensajes.PublishAsync(new CalcularVigenciasDelDocumento(documento.Id));
    }
}

public static class RecalcularVigenciasDelTipoHandler
{
    /// <summary>Documentos <c>PorVinculo</c> del tipo y globales de su canónico compartidos con algún vínculo del tenant.</summary>
    public static async Task Handle(RecalcularVigenciasDelTipo mensaje, ISesiones sesiones, IMessageContext mensajes, CancellationToken ct)
    {
        var tipoId = mensaje.TipoDocumentoTenantId;
        var tenantId = mensaje.TenantId;
        await using var consulta = sesiones.ConsultaBoveda();
        var tipo = await consulta.Query<TipoDocumentoTenant>().Where(t => t.AnyTenant() && t.Id == tipoId).FirstOrDefaultAsync(ct);
        if (tipo is null)
            return;

        var documentos = (await consulta.Query<Documento>().Where(d => d.TipoDocumentoTenantId == tipoId).ToListAsync(ct)).ToList();
        if (tipo.Ambito == AmbitoDocumento.Global && tipo.TipoCanonico is { } codigo)
        {
            var globales = await consulta.Query<Documento>().Where(d => d.TipoCanonico == codigo).ToListAsync(ct);
            documentos.AddRange(globales.Where(d => d.Ambito == AmbitoDocumento.Global && d.Comparticiones.Any(c => c.TenantId == tenantId)));
        }

        foreach (var documento in documentos.Where(d => d.VersionActual is not null).DistinctBy(d => d.Id))
            await mensajes.PublishAsync(new CalcularVigenciasDelDocumento(documento.Id));
    }
}

public static class EnviarAvisosDeVencimientoHandler
{
    /// <summary>
    /// Arma un solo correo por tercero (y correo de destino) con cada documento que tiene un aviso ese día y, por documento,
    /// todas sus fechas por empresa («vence para A el 31/10 y para B el 30/12», docs/02 §8.3). Idempotente: un aviso enviado
    /// no se repite. Si llega antes de su fecha (reloj), no hace nada: el programado llegará a su hora.
    /// </summary>
    public static async Task Handle(EnviarAvisosDeVencimiento mensaje, ISesiones sesiones, IMessageContext mensajes, TimeProvider reloj,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        if (CalculadoraDeVigencia.Fecha(ahora) < mensaje.Fecha)
            return;

        await using var sesion = CalendarioDeVigencias.SesionDeBoveda(sesiones, mensajes);
        var clave = AvisoDeVencimiento.Clave(mensaje.TerceroId, mensaje.Fecha);
        var existente = await sesion.LoadAsync<AvisoDeVencimiento>(clave, ct);
        var aviso = existente ?? new AvisoDeVencimiento { Id = clave, TerceroId = mensaje.TerceroId, Fecha = mensaje.Fecha, ProgramadoEn = ahora };
        if (aviso.Estado != EstadoAviso.Programado)
            return;

        var terceroId = mensaje.TerceroId;
        var documentos = await sesion.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
        var conAviso = documentos
            .Select(d => (Documento: d, Version: d.VersionActual is { } n ? d.VersionRequerida(n) : null))
            .Where(x => x.Version is not null && x.Version.Vigencias.Any(v =>
                v is { VigenteHasta: { } hasta, VencidaEn: null } && CalculadoraDeVigencia.FechasDeAviso(hasta, v.DiasAviso).Contains(mensaje.Fecha)))
            .ToList();

        var porAvisar = conAviso;
        if (conAviso.Count > 0)
        {
            var vinculoIds = conAviso.SelectMany(x => x.Version!.Vigencias.Select(v => v.VinculoId)).Distinct().ToArray();
            var vinculos = (await sesion.Query<Vinculo>().Where(v => v.AnyTenant() && v.Id.IsOneOf(vinculoIds)).ToListAsync(ct)).ToDictionary(v => v.Id);
            var empresaIds = vinculos.Values.Select(v => v.EmpresaId).Distinct().ToArray();
            var empresas = (await sesion.LoadManyAsync<Empresa>(ct, empresaIds)).ToDictionary(e => e.Id);

            // Lo que el vínculo tiene eximido (EX-2) no se avisa: ni el correo al tercero ni el evento documento.por_vencer ni la campana de la empresa.
            var hoy = CalculadoraDeVigencia.Fecha(ahora);
            bool Eximido(VigenciaDeVersion v) => vinculos.TryGetValue(v.VinculoId, out var vinculo) && vinculo.ExencionDeDocumento(v.TipoDocumentoTenantId, hoy) is not null;
            porAvisar = [.. conAviso.Where(x => x.Version!.Vigencias.Any(v =>
                v is { VigenteHasta: { } hasta, VencidaEn: null } && CalculadoraDeVigencia.FechasDeAviso(hasta, v.DiasAviso).Contains(mensaje.Fecha) && !Eximido(v)))];

            // Un correo por destinatario: cada uno con los documentos de sus vínculos.
            var porCorreo = porAvisar
                .SelectMany(x => x.Version!.Vigencias
                    .Where(v => v.VigenteHasta is not null && v.VencidaEn is null && vinculos.ContainsKey(v.VinculoId) && !Eximido(v))
                    .Select(v => (Correo: vinculos[v.VinculoId].CorreoTercero, x.Documento, Vigencia: v)))
                .Where(x => !string.IsNullOrWhiteSpace(x.Correo))
                .GroupBy(x => x.Correo.Trim().ToLowerInvariant());
            foreach (var grupo in porCorreo)
            {
                var lineas = grupo.GroupBy(x => x.Documento.Id)
                    .Select(d => new DocumentoPorVencer(d.First().Documento.NombreTipo,
                    [
                        .. d.OrderBy(x => x.Vigencia.VigenteHasta).Select(x => new VencimientoPorEmpresa(
                            empresas.TryGetValue(vinculos[x.Vigencia.VinculoId].EmpresaId, out var empresa) ? empresa.Nombre : "La empresa",
                            x.Vigencia.VigenteHasta!.Value))
                    ]))
                    .OrderBy(l => l.Vencimientos.Min(v => v.VigenteHasta))
                    .ToList();
                // El tenant de uno de sus vínculos: un tercero de pruebas solo tiene vínculos en su sandbox, y el correo va a su buzón (SB-4).
                var tenantDelAviso = empresas.TryGetValue(vinculos[grupo.First().Vigencia.VinculoId].EmpresaId, out var deAviso) ? deAviso.TenantId : (Guid?)null;
                await mensajes.PublishAsync(new CorreoDeVencimientoPendiente(grupo.Key, mensaje.Fecha, lineas, terceroId, tenantDelAviso));
            }

            // Constancia por documento y vínculo para el evento público documento.por_vencer y las notificaciones (NW-1).
            foreach (var (documento, version) in porAvisar)
                foreach (var vigencia in version!.Vigencias.Where(v => v.VigenteHasta is not null && v.VencidaEn is null &&
                                                                       vinculos.ContainsKey(v.VinculoId) && !Eximido(v) &&
                                                                       CalculadoraDeVigencia.FechasDeAviso(v.VigenteHasta!.Value, v.DiasAviso).Contains(mensaje.Fecha)))
                    sesion.Events.Append(documento.Id, new VencimientoAvisado(version.Numero, vigencia.VinculoId, vigencia.VigenteHasta!.Value,
                        vigencia.VigenteHasta!.Value.DayNumber - mensaje.Fecha.DayNumber, ahora));
        }

        aviso.Estado = porAvisar.Count > 0 ? EstadoAviso.Enviado : EstadoAviso.SinDocumentos;
        aviso.Documentos = porAvisar.Count;
        aviso.EnviadoEn = ahora;
        // Un solo ganador (OC-1): el aviso tiene concurrencia optimista y el correo, los eventos y el aviso viajan en esta misma transacción
        // (outbox). Si otro manejador lo marcó mientras este armaba el correo, este guardado falla completo (nada sale) y Wolverine
        // reintenta el mensaje, que ya encuentra el aviso enviado y no hace nada. Si el aviso no existía se inserta: una clave duplicada también pierde.
        if (existente is null)
            sesion.Insert(aviso);
        else
            sesion.Store(aviso);
        await sesion.SaveChangesAsync(ct);
    }
}

public static class VencerVersionDeDocumentoHandler
{
    /// <summary>
    /// Marca la versión <c>Vencida</c> para el vínculo (<see cref="VersionVencida"/>) si sigue siendo la actual, su vigencia no
    /// cambió desde que se programó y ya pasó la gracia; en la misma transacción pide al tenant del vínculo que decida la
    /// renovación (RN-1).
    /// </summary>
    public static async Task Handle(VencerVersionDeDocumento mensaje, ISesiones sesiones, IMessageContext mensajes, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = CalendarioDeVigencias.SesionDeBoveda(sesiones, mensajes);
        var stream = await sesion.Events.FetchForWriting<Documento>(mensaje.DocumentoId, ct);
        var vigencia = stream.Aggregate?.Buscar(mensaje.Numero)?.VigenciaPara(mensaje.VinculoId);
        if (vigencia is null || vigencia.VigenteHasta != mensaje.VigenteHasta || vigencia.DiasGracia != mensaje.DiasGracia)
            return;

        // Lo que el vínculo tiene eximido (EX-4) no se vence para él: sin la marca no hay aviso de vencido, no se abre su renovación ni vence el vínculo. Al terminar la
        // exención se vuelve a pedir este mismo mensaje (ExencionesDeVinculos.ReevaluarAlTerminarAsync) y entonces sí.
        var vinculo = await sesion.Query<Vinculo>().Where(v => v.AnyTenant() && v.Id == mensaje.VinculoId).FirstOrDefaultAsync(ct);
        if (vinculo?.ExencionDeDocumento(vigencia.TipoDocumentoTenantId, CalculadoraDeVigencia.Fecha(reloj.GetUtcNow())) is not null)
            return;

        if (stream.Aggregate!.Vencer(mensaje.Numero, mensaje.VinculoId, reloj.GetUtcNow()) is not { } vencida)
            return;

        stream.AppendOne(vencida);
        await mensajes.PublishAsync(new Expedientes.RenovarPorVencimiento(vigencia.TenantId, mensaje.VinculoId, mensaje.DocumentoId));
        await sesion.SaveChangesAsync(ct);
    }
}

/// <summary>Un documento en el aviso y su fecha de vencimiento con cada empresa.</summary>
public sealed record DocumentoPorVencer(string Documento, IReadOnlyList<VencimientoPorEmpresa> Vencimientos);

public sealed record VencimientoPorEmpresa(string Empresa, DateOnly VigenteHasta);

/// <summary>Correo de aviso de vencimiento (cola durable <c>correo</c>, por el outbox de la transacción del aviso).</summary>
public sealed record CorreoDeVencimientoPendiente(string Para, DateOnly Fecha, IReadOnlyList<DocumentoPorVencer> Documentos, Guid? TerceroId = null,
    Guid? TenantId = null);

public static class CorreoDeVencimientoPendienteHandler
{
    public static async Task Handle(CorreoDeVencimientoPendiente correo, IEnviadorDeCorreo enviador, RedactorDeCorreos redactor,
        Notificaciones.PreferenciasDeCorreoDelTercero preferencias, CancellationToken ct)
    {
        if (!await preferencias.QuiereAsync(correo.TerceroId, Notificaciones.TiposNotificacionTercero.DocumentoPorVencer, ct))
            return;
        await enviador.EnviarAsync(redactor.RedactarAvisoDeVencimiento(correo).DeTenant(correo.TenantId), ct);
    }
}
