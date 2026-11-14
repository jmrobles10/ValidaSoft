using JasperFx.Events;
using Marten;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Microsoft.Extensions.DependencyInjection;
using Wolverine;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Lo que necesita el proceso para escribirle al tercero: el bus del outbox de la petición y el protector con el que viaja
/// el token del enlace de acceso del correo de observación (IN-3, RE-4).
/// </summary>
/// <remarks>
/// <see cref="Publicar"/> es lo que recibe cada mensaje: en una petición, el bus del outbox (misma transacción); en segundo
/// plano, <see cref="CorreosPendientesDeCierre"/>, que los junta y los publica al confirmar la transacción (CU-19).
/// </remarks>
public sealed record CorreoAlTercero(Func<object, ValueTask> Publicar, IProtectorDeSecretos Protector)
{
    public CorreoAlTercero(IMessageBus mensajes, IProtectorDeSecretos protector)
        : this(mensaje => mensajes.PublishAsync(mensaje), protector)
    {
    }
}

/// <summary>
/// Correos al tercero que produce un cierre hecho <b>en segundo plano</b> (la consulta automática de listas o la revisión
/// automática aprueban o rechazan el expediente sin una petición): se juntan durante la transacción y se publican después de
/// confirmarla, así el tercero recibe el correo de resultado aunque no lo haya cerrado una persona (CU-19). Si la transacción
/// falla, no se publica nada.
/// </summary>
public sealed class CorreosPendientesDeCierre(IProtectorDeSecretos protector)
{
    private readonly List<object> _mensajes = [];

    public CorreoAlTercero Correo => new(mensaje =>
    {
        _mensajes.Add(mensaje);
        return ValueTask.CompletedTask;
    }, protector);

    public int Cantidad => _mensajes.Count;

    /// <summary>Publica lo reunido con un bus propio del alcance (después de <c>SaveChangesAsync</c>).</summary>
    public async Task PublicarAsync(IServiceScopeFactory alcances)
    {
        if (_mensajes.Count == 0)
            return;
        await using var alcance = alcances.CreateAsyncScope();
        var bus = alcance.ServiceProvider.GetRequiredService<IMessageBus>();
        foreach (var mensaje in _mensajes)
            await bus.PublishAsync(mensaje);
        _mensajes.Clear();
    }
}

/// <summary>Versión de flujo elegida para un expediente y cómo se eligió.</summary>
public sealed record FlujoResuelto(Flujo Flujo, FlujoVersion Version, OrigenFlujo Origen);

/// <summary>
/// Proceso de validación vínculo ↔ expediente (docs/03 §8) resuelto <b>en la misma transacción</b> (FL-5): abrir el
/// expediente registra <c>ExpedienteVinculado</c> en el vínculo; aprobarlo, rechazarlo o anularlo registra
/// <c>ExpedienteCerrado</c> (y <c>VinculoActivado</c> si se aprobó). Los dos streams son del mismo tenant y se escriben
/// con la misma sesión, de modo que nunca quedan desalineados. También es el único punto que escribe la bandeja (FL-8),
/// publica el correo de observación al tercero y emite el certificado al activarse el vínculo (CE-2, misma transacción).
/// </summary>
public static class ProcesoDeValidacion
{
    /// <summary>
    /// Días entre reconsultas del monitoreo de listas de un vínculo activo (VE-12; <c>Validasoft:Verificadores:ListasRestrictivas:DiasMonitoreo</c>,
    /// 30; 0 lo apaga). Se fija al arrancar, como la zona del negocio (FE-1).
    /// </summary>
    public static int DiasMonitoreoDeListas { get; set; } = 30;

    /// <summary>
    /// Flujo explícito (el indicado o el del vínculo) o el por defecto de <c>(empresa, tipo)</c>; nulo si no hay ninguno
    /// publicado. Un flujo indicado por el llamador que no sirve es un error; el del vínculo sin publicar cae al por defecto.
    /// </summary>
    public static async Task<FlujoResuelto?> ResolverFlujoAsync(IQuerySession sesion, Vinculo vinculo, Guid? explicito, CancellationToken ct)
    {
        if (explicito is { } indicado)
        {
            var flujo = await sesion.LoadAsync<Flujo>(indicado, ct);
            if (flujo is null || flujo.EmpresaId != vinculo.EmpresaId)
                throw ReglaDeNegocioException.Validacion("flujo-invalido", "El flujo no existe en la empresa.");
            if (flujo.TipoVinculoId != vinculo.TipoVinculoId)
                throw ReglaDeNegocioException.Validacion("flujo-invalido", "El flujo es de otro tipo de vínculo.");
            return flujo.VersionPublicada is { } version
                ? new FlujoResuelto(flujo, version, OrigenFlujo.Explicito)
                : throw ReglaDeNegocioException.Validacion("flujo-sin-version-publicada", "El flujo no tiene una versión publicada.");
        }

        if (vinculo.FlujoId is { } delVinculo && await sesion.LoadAsync<Flujo>(delVinculo, ct) is { } propio &&
            propio.EmpresaId == vinculo.EmpresaId && propio.VersionPublicada is { } versionPropia)
            return new FlujoResuelto(propio, versionPropia, OrigenFlujo.Explicito);

        var porDefecto = await sesion.LoadAsync<FlujoPorDefecto>(FlujoPorDefecto.Clave(vinculo.EmpresaId, vinculo.TipoVinculoId), ct);
        if (porDefecto is null || await sesion.LoadAsync<Flujo>(porDefecto.FlujoId, ct) is not { VersionPublicada: { } publicada } elegido)
            return null;
        return new FlujoResuelto(elegido, publicada, OrigenFlujo.PorDefecto);
    }

    /// <summary>
    /// La misma resolución que <see cref="ResolverFlujoAsync"/> sin flujo indicado, sobre flujos ya cargados (consultas
    /// entre tenants): la versión publicada del flujo del vínculo o, si no, la del por defecto de <c>(empresa, tipo)</c>.
    /// </summary>
    public static FlujoVersion? FlujoVigente(Vinculo vinculo, IEnumerable<Flujo> flujos, IEnumerable<FlujoPorDefecto> porDefecto)
    {
        var lista = flujos as IReadOnlyCollection<Flujo> ?? [.. flujos];
        if (vinculo.FlujoId is { } propio && lista.FirstOrDefault(f => f.Id == propio) is { } delVinculo &&
            delVinculo.EmpresaId == vinculo.EmpresaId && delVinculo.VersionPublicada is { } version)
            return version;

        var clave = FlujoPorDefecto.Clave(vinculo.EmpresaId, vinculo.TipoVinculoId);
        return porDefecto.FirstOrDefault(p => p.Id == clave) is { } elegido
            ? lista.FirstOrDefault(f => f.Id == elegido.FlujoId)?.VersionPublicada
            : null;
    }

    /// <summary>
    /// Abre el expediente inicial del vínculo con el flujo resuelto: consume el folio, inicia el stream del expediente,
    /// registra <c>ExpedienteVinculado</c> en <paramref name="vinculoStream"/> y, si todo fue automático, lo cierra aprobado.
    /// Con requisitos, cada uno nace <c>Cargado</c> si la bóveda ya compartió con el vínculo la versión actual de su tipo:
    /// <paramref name="documentos"/> los trae la aceptación (que acaba de compartir); en una apertura manual de un vínculo
    /// ya aceptado se comparte aquí lo que pide el flujo (RQ-3). Sin aceptar, todos nacen <c>Pendiente</c> (B-1). Una
    /// <c>Actualizacion</c> (RV-1) lleva solo los requisitos de <paramref name="soloRequisitos"/> y la abre el sistema en
    /// nombre de <paramref name="actor"/> (quien subió la versión). <paramref name="retiros"/>: por requisito, la versión que la
    /// empresa había aprobado y el tercero retiró (RT-3), para que la revalidación lleve el motivo. Con <paramref name="exigirRatificacion"/>
    /// (la renovación periódica del vínculo, VV-1) las secciones que la empresa ya había aceptado nacen pendientes de que el tercero las
    /// ratifique o las actualice.
    /// </summary>
    public static async Task<Expediente> AbrirAsync(IDocumentSession sesion, IEventStream<Vinculo> vinculoStream, FlujoResuelto flujo,
        IContextoSolicitud? contexto, EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct, DocumentosDelVinculo? documentos = null,
        TipoExpediente tipoExpediente = TipoExpediente.Inicial, IReadOnlySet<Guid>? soloRequisitos = null, ActorDeExpediente? actor = null,
        IReadOnlyDictionary<Guid, RetiroDeVersion>? retiros = null, bool exigirRatificacion = false)
    {
        var vinculo = vinculoStream.Aggregate ?? throw new InvalidOperationException("El vínculo no existe.");
        var tercero = await sesion.LoadAsync<Tercero>(vinculo.TerceroId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero del vínculo no existe.");
        var tipo = await sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct);
        var empresa = await sesion.LoadAsync<Empresa>(vinculo.EmpresaId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("empresa-no-encontrada", "La empresa del vínculo no existe.");
        var tenant = await sesion.LoadAsync<Tenant>(empresa.TenantId, ct);

        var areaIds = flujo.Version.Etapas.SelectMany(e => e.Areas).Select(a => a.AreaId).Distinct().ToArray();
        var areas = (await sesion.LoadManyAsync<Area>(ct, areaIds)).ToDictionary(a => a.Id);
        // El plazo del flujo manda; si no pone uno, el plazo por defecto del área en la empresa (SLA, OP-10).
        var sla = await SlaDeLaEmpresa.CargarAsync(sesion, vinculo.EmpresaId, ct);
        var etapas = flujo.Version.Etapas.Select(e => new EtapaDelFlujo(e.Orden, [.. e.Areas.Select(a => new AreaDelFlujo(a.AreaId,
            areas.TryGetValue(a.AreaId, out var area) ? area.Codigo : a.AreaId.ToString("N"),
            areas.TryGetValue(a.AreaId, out var conNombre) ? conNombre.Nombre : "Área", a.AprobadoresRequeridos, a.PuedeRechazarExpediente,
            a.Automatica, a.SlaHoras ?? sla.HorasDe(a.AreaId)))])).ToList();

        var exigencia = ContextoDeExigencia.De(tercero, tipo?.Codigo ?? string.Empty, vinculo.Atributos);
        var requisitos = await CalcularRequisitosAsync(sesion, vinculo, tercero, empresa.TenantId, flujo.Version, exigencia, soloRequisitos, documentos, ahora, ct,
            retiros, exigirRatificacion);

        var folio = await SiguienteFolioAsync(sesion, empresa, tenant, ahora, ct);
        var abiertoPor = actor ?? new ActorDeExpediente(contexto?.ActorTipo ?? ActorTipo.Sistema, contexto?.ActorId ?? "sistema");
        var datos = new DatosDeApertura(vinculo.EmpresaId, vinculo.Id, tipoExpediente, folio, tercero.Id, tercero.Nombre,
            tercero.Identificacion.Clave, vinculo.TipoVinculoId, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty,
            abiertoPor, await QuienesInvitaronAsync(sesion, vinculo.Id, ct),
            // Si el tipo de vínculo la exige, el expediente no se aprueba sin la cuenta designada aprobada (CB-4).
            ExigeCuentaDesignada: tipo?.ExigeCuentaDesignada == true, CuentaDesignadaAprobada: vinculo.CuentaDesignada is not null);
        var eventos = Expediente.Abrir(datos, new FlujoParaExpediente(flujo.Flujo.Id, flujo.Version.Id, flujo.Flujo.Codigo, flujo.Flujo.Nombre,
            flujo.Version.Version, flujo.Origen, etapas, flujo.Version.PoliticaDevolucion, flujo.Version.SeparacionDeFunciones,
            // Una actualización (un documento cambió) no repite las listas ni el estudio (VE-6).
            tipoExpediente == TipoExpediente.Actualizacion ? [] : flujo.Version.Verificaciones, flujo.Version.PlazoEfectivo(), flujo.Version.RecordatorioObservacionesDias), ahora, requisitos);

        var expedienteId = ((ExpedienteAbierto)eventos[0]).ExpedienteId;
        var vinculado = vinculo.VincularExpediente(expedienteId, tipoExpediente, folio, ahora);
        vinculoStream.AppendOne(vinculado);
        if (vinculo.ExpedienteAbiertoId != expedienteId)
            vinculo.Apply(vinculado); // El agregado del stream decide el cierre inmediato (todo automático) con el estado al día.

        sesion.Events.StartStream<Expediente>(expedienteId, eventos);
        var estado = Expediente.Reproducir(null, eventos);
        await DespuesDeEscribirAsync(sesion, estado, eventos, vinculoStream, null, certificados, ahora, ct);
        return estado;
    }

    /// <summary>
    /// Foto de los requisitos que la versión del flujo le pide a este tercero en este vínculo (los que cumplen su condición,
    /// RQ-12), en el orden del flujo, y solo los de <paramref name="soloRequisitos"/> si se indican: cada documento con la versión
    /// compartida que ya lo cubre, cada sección con la versión vigente si es completa para la empresa y lo que la empresa ya
    /// aceptó o aprobó (RQ-11). La usan la apertura del expediente y la ampliación de una actualización abierta, para que ambos
    /// calculen igual. Con <paramref name="documentos"/> nulo y la invitación aceptada, comparte con el vínculo lo que el flujo pide (RQ-3).
    /// Con <paramref name="exigirRatificacion"/> una sección cuya versión vigente es la misma que la empresa ya aceptó no la cubre
    /// (nace pendiente, <see cref="RequisitoCalculado.ExigeRatificacion"/>): el tercero la ratifica o la actualiza (VV-1).
    /// </summary>
    public static async Task<IReadOnlyList<RequisitoCalculado>> CalcularRequisitosAsync(IDocumentSession sesion, Vinculo vinculo, Tercero tercero,
        Guid tenantId, FlujoVersion version, ContextoDeExigencia exigencia, IReadOnlySet<Guid>? soloRequisitos, DocumentosDelVinculo? documentos,
        DateTimeOffset ahora, CancellationToken ct, IReadOnlyDictionary<Guid, RetiroDeVersion>? retiros = null, bool exigirRatificacion = false)
    {
        var pedidos = (await RequisitosDeExpediente.DelFlujoAsync(sesion, version, exigencia, ct))
            .Where(p => soloRequisitos is null || soloRequisitos.Contains(p.Requisito.Id))
            .ToList();
        var secciones = RequisitosDeExpediente.SeccionesDelFlujo(version, exigencia, CalculadoraDeVigencia.Fecha(ahora))
            .Where(s => soloRequisitos is null || soloRequisitos.Contains(s.RequisitoId))
            .ToList();
        // La ficha queda disponible para la empresa desde la aceptación (la de esta transacción trae los documentos ya compartidos).
        // (Con acceso aislado pendiente o rechazado, la ficha del tercero no está disponible para la empresa: nada global se le comparte.)
        var aceptado = (documentos is not null || vinculo.InvitacionAceptada) && vinculo.BovedaDisponible;
        if (pedidos.Count > 0 && documentos is null && vinculo.InvitacionAceptada)
            documentos = await ComparticionDeDocumentos.CompartirConVinculoAsync(sesion, vinculo, tenantId,
                RequisitosDeExpediente.CodigosGlobales(pedidos), OrigenComparticion.Automatica, ahora, ct);
        var ficha = secciones.Count > 0 && aceptado ? await RequisitosDeExpediente.FichaAsync(sesion, tercero.Id, ct) : null;
        var aceptadas = secciones.Count > 0
            ? await RequisitosDeExpediente.AceptadasAsync(sesion, vinculo.Id, ct)
            : new Dictionary<string, SeccionAceptada>();
        var calculados = pedidos.Select(p => (p.Requisito.Id, Calculado: RequisitosDeExpediente.Calcular(p, documentos?.Documentos ?? [], vinculo.Id)))
            .Concat(secciones.Select(s => (s.Requisito.Id, Calculado: ConRatificacion(RequisitosDeExpediente.CalcularSeccion(s, tercero.Id, ficha,
                RequisitosDeExpediente.Aceptada(aceptadas, vinculo.Id, s)), exigirRatificacion))))
            .ToList();
        // En el orden del flujo (los periodos de la financiera, del más reciente al más antiguo).
        var orden = version.Requisitos.Select(r => r.Id).ToList();
        var ordenados = await RequisitosDeExpediente.ConAprobacionesPreviasAsync(sesion, vinculo,
            [.. calculados.OrderBy(c => orden.IndexOf(c.Id)).Select(c => c.Calculado)], ct);
        // Lo que la empresa había aprobado y el tercero retiró (RT-3): el requisito nace con el motivo del retiro.
        var conRetiros = retiros is null || retiros.Count == 0
            ? ordenados
            : [.. ordenados.Select(r => retiros.TryGetValue(r.RequisitoId, out var retiro) ? r with { Retiro = retiro } : r)];
        // Lo que la empresa tiene eximido para este vínculo (EX-3) nace Eximido citando la exención, sea cual sea el tipo de expediente.
        return ExencionesDeVinculos.Aplicar(vinculo, conRetiros, ahora);
    }

    /// <summary>Renovación periódica: la sección cuya versión completa es la ya aceptada no queda cubierta; hace falta ratificarla (VV-1).</summary>
    private static RequisitoCalculado ConRatificacion(RequisitoCalculado calculado, bool exigir) =>
        exigir && calculado is { TipoRequisito: TipoDeRequisito.Seccion, Version: { } version, Aceptada: { } aceptada } &&
        version.DocumentoId == aceptada.DocumentoId && version.Numero == aceptada.Numero
            ? calculado with { Version = null, ExigeRatificacion = true }
            : calculado;

    /// <summary>
    /// Anexa los eventos de una decisión al expediente y propaga sus efectos en la misma sesión: bandeja, cierre del
    /// vínculo, certificado si se aprobó y, con <paramref name="correo"/>, el correo de observación con su enlace de acceso.
    /// </summary>
    public static async Task<Expediente> AnexarAsync(IDocumentSession sesion, IEventStream<Expediente> stream, IReadOnlyList<object> eventos,
        CorreoAlTercero? correo, EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct, IEventStream<Vinculo>? vinculoStream = null)
    {
        var estado = Expediente.Reproducir(stream.Aggregate, eventos);
        stream.AppendMany(eventos);
        await DespuesDeEscribirAsync(sesion, estado, eventos, vinculoStream, correo, certificados, ahora, ct);
        return estado;
    }

    /// <summary>
    /// La empresa aprobó la cuenta designada del vínculo (CB-4): si el expediente abierto del vínculo la exigía y esperaba por ella,
    /// se cierra aprobado <b>en la misma transacción</b> (con su certificado y el correo al tercero, si lo hubo). Si no hay expediente
    /// abierto, no la exigía o ya la tenía, no hace nada. <paramref name="vinculoStream"/> es el stream del vínculo, ya con los
    /// eventos de la aprobación anexados.
    /// </summary>
    public static async Task ConfirmarCuentaDesignadaAsync(IDocumentSession sesion, IEventStream<Vinculo> vinculoStream, CorreoAlTercero? correo,
        EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct)
    {
        if (vinculoStream.Aggregate?.ExpedienteAbiertoId is not { } expedienteId)
            return;
        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(expedienteId, ct);
        if (stream.Aggregate is not { } expediente)
            return;
        var eventos = expediente.ConfirmarCuentaDesignada(ahora);
        if (eventos.Count > 0)
            await AnexarAsync(sesion, stream, eventos, correo, certificados, ahora, ct, vinculoStream);
    }

    /// <summary>
    /// Desaprueba un expediente ya aprobado (DS-2) <b>en la misma transacción</b>: el vínculo vuelve a <c>EnValidacion</c> con
    /// este mismo expediente abierto (<c>VinculoDesaprobado</c>), el certificado se revoca y lo que la empresa había aceptado de
    /// sus secciones (FM-3) se retira hasta que se apruebe de nuevo. <paramref name="eventos"/> trae <c>ExpedienteDesaprobado</c>
    /// y lo que la cascada produjo; si esta lo aprobó otra vez en el acto (todas sus áreas son automáticas), el certificado y
    /// lo aceptado se conservan. 409 del vínculo: bloqueado, terminado, con otro expediente abierto o este no es el último aprobado.
    /// </summary>
    public static async Task<Expediente> DesaprobarAsync(IDocumentSession sesion, IEventStream<Expediente> stream, IReadOnlyList<object> eventos,
        string actor, CorreoAlTercero? correo, EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct)
    {
        var expediente = stream.Aggregate ?? throw new InvalidOperationException("El expediente no existe.");
        var desaprobado = eventos.OfType<ExpedienteDesaprobado>().Single();

        var vinculoStream = await sesion.Events.FetchForWriting<Vinculo>(expediente.VinculoId, ct);
        var vinculo = vinculoStream.Aggregate ?? throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "El vínculo del expediente no existe.");
        var evento = vinculo.Desaprobar(expediente.Id, desaprobado.Justificacion, ahora);
        vinculoStream.AppendOne(evento);
        if (vinculo.ExpedienteAbiertoId != expediente.Id)
            vinculo.Apply(evento); // El agregado del stream decide el cierre inmediato (todo automático) con el estado al día.

        if (!eventos.OfType<ExpedienteAprobado>().Any())
        {
            await CicloDeCertificados.AlDesaprobarAsync(sesion, vinculo.Id, expediente.Folio, desaprobado.Justificacion, actor, ahora, ct);
            var expedienteId = expediente.Id;
            foreach (var aceptada in await sesion.Query<SeccionAceptada>().Where(s => s.ExpedienteId == expedienteId).ToListAsync(ct))
                sesion.Delete(aceptada);
        }

        var estado = Expediente.Reproducir(expediente, eventos);
        stream.AppendMany(eventos);
        await DespuesDeEscribirAsync(sesion, estado, eventos, vinculoStream, correo, certificados, ahora, ct);
        return estado;
    }

    /// <summary>Un vínculo anulado o desvinculado anula su expediente abierto (el vínculo ya lo soltó en su propio evento).</summary>
    public static async Task AnularPorTerminacionDelVinculoAsync(IDocumentSession sesion, Guid expedienteId, string motivo, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var stream = await sesion.Events.FetchForWriting<Expediente>(expedienteId, ct);
        if (stream.Aggregate is not { EstaAbierto: true } expediente)
            return;

        var anulado = expediente.Anular($"Vínculo terminado: {motivo}", ahora);
        stream.AppendOne(anulado);
        foreach (var fila in ProyeccionBandejaRevision.Filas(Expediente.Reproducir(expediente, [anulado])))
            sesion.Store(fila);
    }

    private static async Task DespuesDeEscribirAsync(IDocumentSession sesion, Expediente estado, IReadOnlyList<object> eventos,
        IEventStream<Vinculo>? vinculoStream, CorreoAlTercero? correo, EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct)
    {
        foreach (var fila in ProyeccionBandejaRevision.Filas(estado))
            sesion.Store(fila);

        // Lo aceptado de cada sección por el vínculo (FM-3): misma transacción que la aprobación.
        foreach (var aceptada in eventos.OfType<SeccionAceptadaPorEmpresa>())
            sesion.Store(new SeccionAceptada
            {
                Id = SeccionAceptada.Clave(estado.VinculoId, aceptada.Seccion, aceptada.Periodo),
                EmpresaId = estado.EmpresaId,
                VinculoId = estado.VinculoId,
                TerceroId = estado.TerceroId,
                Seccion = aceptada.Seccion,
                Periodo = aceptada.Periodo,
                SeccionId = aceptada.SeccionId,
                Version = aceptada.Version,
                Huella = aceptada.Huella,
                AceptadaEn = aceptada.Fecha,
                ExpedienteId = estado.Id,
                Folio = estado.Folio
            });

        ResultadoExpediente? resultado = eventos.OfType<ExpedienteAprobado>().Any() ? ResultadoExpediente.Aprobado
            : eventos.OfType<ExpedienteRechazado>().Any() ? ResultadoExpediente.Rechazado
            : eventos.OfType<ExpedienteAnulado>().Any() ? ResultadoExpediente.Anulado
            : eventos.OfType<ExpedienteCaducado>().Any() ? ResultadoExpediente.Caducado
            : null;

        if (resultado is { } cierre)
        {
            vinculoStream ??= await sesion.Events.FetchForWriting<Vinculo>(estado.VinculoId, ct);
            if (vinculoStream.Aggregate is { } vinculo && vinculo.ExpedienteAbiertoId == estado.Id)
            {
                var cerrado = vinculo.CerrarExpediente(estado.Id, cierre, ahora);
                vinculoStream.AppendMany(cerrado);
                // Vínculo Activo por un expediente aprobado ⇒ certificado en la misma transacción (docs/03 §8, CE-2), con la vigencia
                // periódica del vínculo (VV-1) que esa aprobación fija.
                if (cerrado.OfType<VinculoActivado>().Any())
                {
                    var vigenteHasta = await VigenciasDeVinculos.FijarAlAprobarAsync(sesion, vinculoStream, vinculo, estado, ahora, ct);
                    await certificados.EmitirAsync(sesion, estado, vinculo, ahora, ct, vigenteHastaDelVinculo: vigenteHasta);
                }

                // Una actualización que se cierra sin aprobarse (VR-13): lo ya aprobado sigue valiendo, así que el certificado deja de estar
                // «En verificación» y vuelve a Vigente (el vínculo ya conserva su estado, RV-2).
                if (cierre is ResultadoExpediente.Rechazado or ResultadoExpediente.Anulado && estado.Tipo == TipoExpediente.Actualizacion &&
                    vinculo.Estado is not (EstadoVinculo.Desvinculado or EstadoVinculo.Anulado))
                {
                    var certificado = await sesion.Events.FetchForWriting<Certificado>(Certificado.IdPara(estado.VinculoId), ct);
                    if (certificado.Aggregate?.ResolverPendientes(estado.Id, ahora) is { } resueltos)
                        certificado.AppendOne(resueltos);
                }
            }

            // Aprobado con listas consultadas ⇒ el vínculo queda en monitoreo periódico (VE-12), heredando las decisiones.
            if (cierre == ResultadoExpediente.Aprobado &&
                MonitoreoDeListas.Iniciar(estado, await sesion.LoadAsync<MonitoreoDeListas>(estado.VinculoId, ct), DiasMonitoreoDeListas, ahora) is { } monitoreo)
                sesion.Store(monitoreo);
        }

        if (correo is not null && eventos.OfType<ExpedienteObservado>().Any())
            await PublicarObservacionAsync(sesion, estado, correo, ahora, ct);
        if (correo is not null && resultado is ResultadoExpediente.Aprobado or ResultadoExpediente.Rechazado)
            await PublicarResultadoAsync(sesion, estado, eventos, correo, ct);
    }

    /// <summary>
    /// Aviso al tercero de que su validación se cerró (docs/05 §8): aprobada o rechazada con los textos al tercero de los
    /// motivos. Antes solo se enteraban las áreas: el tercero no sabía del rechazo si no volvía a entrar.
    /// </summary>
    private static async Task PublicarResultadoAsync(IDocumentSession sesion, Expediente estado, IReadOnlyList<object> eventos, CorreoAlTercero correo,
        CancellationToken ct)
    {
        var vinculo = await sesion.LoadAsync<Vinculo>(estado.VinculoId, ct);
        if (vinculo is null || string.IsNullOrWhiteSpace(vinculo.CorreoTercero))
            return;

        var empresa = await sesion.LoadAsync<Empresa>(estado.EmpresaId, ct);
        var rechazo = eventos.OfType<ExpedienteRechazado>().FirstOrDefault();
        await correo.Publicar(new CorreoDeResultadoPendiente(vinculo.CorreoTercero, empresa?.Nombre ?? "La empresa", estado.Folio,
            rechazo is null, rechazo is null ? [] : [.. rechazo.Motivos.Select(m => m.TextoAlTercero)], empresa?.TenantId, estado.EmpresaId,
            vinculo.TerceroId, estado.Tipo == TipoExpediente.Actualizacion));
    }

    /// <summary>
    /// Correo al tercero con motivos y observaciones vigentes, sin revisores ni áreas (outbox), y un enlace de acceso de un
    /// solo uso creado en esta misma transacción (RE-4): sin él el tercero no tenía cómo volver a entrar.
    /// </summary>
    private static async Task PublicarObservacionAsync(IDocumentSession sesion, Expediente estado, CorreoAlTercero correo, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var vinculo = await sesion.LoadAsync<Vinculo>(estado.VinculoId, ct);
        if (vinculo is null || string.IsNullOrWhiteSpace(vinculo.CorreoTercero))
            return;

        var empresa = await sesion.LoadAsync<Empresa>(estado.EmpresaId, ct);
        var tipo = await sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct);
        var observaciones = estado.ObservacionesVigentes
            .Select(v => new ObservacionParaCorreo([.. v.Motivos.Select(m => m.TextoAlTercero)], v.Observacion,
                v.RequisitoId is { } requisitoId ? estado.BuscarRequisito(requisitoId)?.Nombre : null))
            .ToList();

        var (enlace, token) = EnlaceDeAcceso.Emitir(empresa?.TenantId, vinculo.Id, vinculo.TerceroId, vinculo.CorreoTercero,
            ahora + EnlaceDeAcceso.VigenciaObservacion, tipo?.ExigeOtpEnEnlace ?? false, ahora, MotivoEnlace.Observacion);
        sesion.Store(enlace);
        await correo.Publicar(new CorreoDeObservacionPendiente(enlace.CorreoDestino, empresa?.Nombre ?? "La empresa", estado.Folio,
            observaciones, correo.Protector.Proteger(token), enlace.Expira, empresa?.TenantId, estado.EmpresaId, vinculo.TerceroId));
    }

    /// <summary>
    /// Siguiente folio de la empresa en el año (zona horaria del tenant). El contador se escribe con concurrencia
    /// optimista en esta transacción: sin huecos (si se revierte, no se consume) ni duplicados (el perdedor reintenta).
    /// </summary>
    private static async Task<string> SiguienteFolioAsync(IDocumentSession sesion, Empresa empresa, Tenant? tenant, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var zona = TimeZoneInfo.TryFindSystemTimeZoneById(tenant?.ZonaHoraria ?? "America/Bogota", out var encontrada) ? encontrada : TimeZoneInfo.Utc;
        var anio = TimeZoneInfo.ConvertTime(ahora, zona).Year;
        var clave = ContadorDeFolio.Clave(empresa.Id, "E", anio);

        var contador = await sesion.LoadAsync<ContadorDeFolio>(clave, ct);
        if (contador is null)
        {
            contador = ContadorDeFolio.Nuevo(empresa.Id, "E", anio);
            contador.Siguiente();
            sesion.Insert(contador);
        }
        else
        {
            contador.Siguiente();
            sesion.Store(contador);
        }

        return Folios.DeExpediente(empresa.PrefijoFolio, anio, contador.Ultimo);
    }

    /// <summary>Usuarios que crearon el vínculo o enviaron sus invitaciones (separación de funciones, P-4-8).</summary>
    private static async Task<IReadOnlyList<string>> QuienesInvitaronAsync(IQuerySession sesion, Guid vinculoId, CancellationToken ct)
    {
        var eventos = await sesion.Events.FetchStreamAsync(vinculoId, token: ct);
        return
        [
            .. eventos.Where(e => e.Data is VinculoCreado or InvitacionEnviada)
                .Where(e => e.Headers is not null && e.Headers.TryGetValue("actorTipo", out var tipo) && tipo?.ToString() == nameof(ActorTipo.Usuario))
                .Select(e => e.Headers!["actorId"]?.ToString())
                .OfType<string>()
                .Distinct()
        ];
    }
}

/// <summary>Quién revisa: persona (identidad delegada o su sesión), miembro activo del área y con rol en la empresa.</summary>
public static class Revisores
{
    public static async Task<Revisor> DelAreaAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, Guid areaId, CancellationToken ct)
    {
        if (contexto.ActorTipo != ActorTipo.Usuario || !Guid.TryParse(contexto.ActorId, out var usuarioId))
            throw ReglaDeNegocioException.Validacion("usuario-delegado-requerido",
                "Los veredictos los emite una persona: actúa con la identidad delegada (X-Usuario-Delegado) o con su sesión.");

        var area = await empresa.CargarAreaAsync(areaId, ct);
        var usuario = await empresa.Sesion.LoadAsync<Usuario>(usuarioId, ct);
        // Revisa quien es miembro del área o quien tiene una delegación vigente de uno de sus miembros (OP-8): sigue siendo él ante las reglas.
        var delegacion = area.Miembros.Contains(usuarioId) ? null : await Delegaciones.DelegacionesDeRevision.VigenteParaAreaAsync(empresa.Sesion, area, usuarioId, empresa.Hoy, ct);
        if (usuario is not { Activo: true } || (!area.Miembros.Contains(usuarioId) && delegacion is null) || !usuario.TieneRolEn(empresa.EmpresaId))
            throw ReglaDeNegocioException.Prohibido("no-es-miembro-del-area", $"No eres miembro activo del área {area.Codigo}.");
        if (!area.Activa)
            throw ReglaDeNegocioException.Conflicto("area-inactiva", $"El área {area.Codigo} está inactiva.");

        if (delegacion is not null)
        {
            // Queda en los eventos de esta decisión: en nombre de quién revisa y con qué delegación.
            empresa.Sesion.SetHeader("delegadoDe", delegacion.DeUsuarioId.ToString());
            empresa.Sesion.SetHeader("delegacionId", delegacion.Id.ToString());
        }

        return new Revisor(usuarioId, usuario.Identificacion?.Clave);
    }
}
