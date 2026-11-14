using System.Globalization;
using System.Net;
using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Vinculos;

// Vigencia periódica del vínculo y fin de los bloqueos con fecha (VV-1…VV-6). El dominio decide (Vinculo.AvisarVigencia,
// VencerPorVigencia, DesbloquearPorFecha) y guarda en el vínculo el próximo momento en que hay algo que hacer
// (ProximaRevisionDeVigencia, indexado). Un job periódico —como el de plazos de cargue— barre los vínculos con ese momento
// cumplido y pide por cada uno RevisarVigenciaDeVinculo en la cola durable «vigencias» (la de las vigencias de documentos): un
// aviso, el vencimiento o el fin de un bloqueo temporal. La renovación se abre en el primer aviso (pre-vencimiento): el vínculo pasa
// a PorVencer, con el certificado vigente, y se abre el expediente de Renovación que pide ratificar o actualizar las secciones y los
// documentos pendientes; si se aprueba antes de la fecha la vigencia se cuenta de nuevo desde esa aprobación y el vínculo nunca vence.
// Los avisos siguientes solo recuerdan (no abren otro expediente). Al vencer (Vencido, certificado vencido) la misma renovación
// abierta sigue su curso. El bloqueo con fecha además programa su propio mensaje (DesbloquearAlLlegarLaFecha), así no espera al
// siguiente barrido. Todo es idempotente: vuelve a mirar el estado al dispararse.

/// <summary>Barre los vínculos con algo pendiente a la hora <see cref="Ahora"/> (o la actual), de todos los tenants o de uno.</summary>
public sealed record RevisarVigenciasDeVinculos(Guid? TenantId = null, DateTimeOffset? Ahora = null);

/// <summary>Avisa, vence o desbloquea un vínculo (idempotente: si no hay nada pendiente no hace nada).</summary>
public sealed record RevisarVigenciaDeVinculo(Guid TenantId, Guid VinculoId, DateTimeOffset? Ahora = null);

/// <summary>
/// Programado al bloquear con fecha de fin (VV-5): al llegar <see cref="Hasta"/> restituye el estado previo. Si el bloqueo ya se
/// levantó a mano, o se cambió por otro con otra fecha, no hace nada.
/// </summary>
public sealed record DesbloquearAlLlegarLaFecha(Guid TenantId, Guid VinculoId, DateTimeOffset Hasta);

public enum AvisoDeVigencia
{
    PorVencer,
    Vencido,

    /// <summary>La empresa abrió la renovación antes de que venza (campaña de renovación, OP-1).</summary>
    Renovacion
}

/// <summary>
/// Correo al tercero sobre la vigencia de su validación con una empresa (cola <c>correo</c>): el aviso previo, el vencimiento o la renovación
/// que abrió la empresa, con el enlace de acceso a la renovación (cifrado, IN-3). Lleva la plantilla de la empresa (PL-1: <c>pre-vencimiento</c>,
/// <c>vigencia-vencida</c> o <c>renovacion</c>) y respeta que el tercero apague este tipo de correo (NT-5, <c>tercero.vigencia</c>).
/// </summary>
public sealed record CorreoDeVigenciaPendiente(string Para, string Empresa, AvisoDeVigencia Aviso, DateOnly VigenteHasta, int DiasRestantes,
    string? EnlaceProtegido = null, DateTimeOffset? EnlaceExpira = null, Guid? TenantId = null, Guid? EmpresaId = null, Guid? TerceroId = null);

public static class CorreoDeVigenciaPendienteHandler
{
    private static readonly CultureInfo Cultura = CultureInfo.GetCultureInfo("es-CO");

    public static async Task Handle(CorreoDeVigenciaPendiente correo, IEnviadorDeCorreo enviador, IProtectorDeSecretos protector, RedactorDeCorreos redactor,
        PlantillasDeCorreo plantillas, PreferenciasDeCorreoDelTercero preferencias, CancellationToken ct)
    {
        // El tercero puede apagar este correo: la campana de su portal avisa igual (NT-5).
        if (!await preferencias.QuiereAsync(correo.TerceroId, TiposNotificacionTercero.Vigencia, ct))
            return;
        var mensaje = Redactar(correo, redactor.UrlPublica, correo.EnlaceProtegido is null ? null : protector.Desproteger(correo.EnlaceProtegido));
        var evento = correo.Aviso switch
        {
            AvisoDeVigencia.PorVencer => CatalogoDePlantillas.PreVencimiento,
            AvisoDeVigencia.Vencido => CatalogoDePlantillas.VigenciaVencida,
            _ => CatalogoDePlantillas.Renovacion
        };
        await enviador.EnviarAsync((await plantillas.AplicarAsync(mensaje, evento, correo.TenantId, correo.EmpresaId,
            EjemplosDeCorreo.VariablesDe(correo.Empresa, fecha: FechaLarga(correo.VigenteHasta)), ct)).DeTenant(correo.TenantId, correo.EmpresaId), ct);
    }

    internal static string FechaLarga(DateOnly fecha) => fecha.ToString("d 'de' MMMM 'de' yyyy", Cultura);

    internal static MensajeDeCorreo Redactar(CorreoDeVigenciaPendiente correo, string urlPublica, string? token)
    {
        var fecha = FechaLarga(correo.VigenteHasta);
        var cuando = correo.DiasRestantes switch
        {
            <= 0 => "hoy",
            1 => "mañana",
            var dias => $"en {Dominio.Comun.Plural.Dias(dias)}"
        };
        var (asunto, parrafo) = correo.Aviso switch
        {
            AvisoDeVigencia.PorVencer => ($"Ratifica tu información con {correo.Empresa} antes del {fecha}",
                $"Tu validación con {correo.Empresa} está en pre-vencimiento: vence {cuando} ({fecha}). Antes de esa fecha ratifica (confirma que sigue igual) " +
                "o actualiza las secciones de tu información y sube los documentos pendientes para seguir al día sin interrupciones."),
            AvisoDeVigencia.Renovacion => ($"{correo.Empresa} abrió la renovación de tu validación",
                $"{correo.Empresa} abrió la renovación de tu validación, que vence {cuando} ({fecha}). Ratifica (confirma que sigue igual) o actualiza las " +
                "secciones de tu información y sube los documentos pendientes para seguir al día sin interrupciones."),
            _ => ($"Tu validación con {correo.Empresa} venció: ratifica tu información",
                $"Tu validación con {correo.Empresa} venció el {fecha}. Para seguir al día ratifica las secciones de tu información que no cambiaron " +
                "o actualiza las que sí.")
        };

        var entrar = token is null
            ? $"Entra a Validasoft ({urlPublica}) y pide un enlace con tu correo."
            : $"Abre este enlace (es personal, de un solo uso y vence el {correo.EnlaceExpira?.UtcDateTime.ToString("d 'de' MMMM 'de' yyyy", Cultura)}):\n{urlPublica}/e/{token}";
        var entrarHtml = token is null
            ? $"<p>Entra a <a href=\"{WebUtility.HtmlEncode(urlPublica)}\">Validasoft</a> y pide un enlace con tu correo.</p>"
            : $"<p><a href=\"{WebUtility.HtmlEncode($"{urlPublica}/e/{token}")}\">Ratificar mi información en Validasoft</a></p>" +
              "<p>El enlace es personal y de un solo uso.</p>";
        return new MensajeDeCorreo(correo.Para, asunto, parrafo + "\n\n" + entrar, $"<p>{WebUtility.HtmlEncode(parrafo)}</p>" + entrarHtml);
    }
}

public enum ResultadoDeVigencia
{
    SinCambios,
    Avisado,
    Vencido,
    Renovado,
    Desbloqueado,

    /// <summary>Terminó una exención del vínculo (EX-4) y no hubo otra transición: el estado se recalculó con las vigencias de siempre.</summary>
    ExencionVencida
}

/// <summary>
/// Lo que entra a una renovación (VV-3): <see cref="Ids"/> son todos los requisitos que le tocan —también los que el vínculo tiene eximidos (EX-3), que nacen
/// <c>Eximido</c>— y <see cref="Pendientes"/>, los que de verdad hay que pedirle al tercero (los demás no cuentan: sin pendientes no hay renovación que abrir).
/// </summary>
public sealed record RequisitosPorRenovar(IReadOnlySet<Guid> Ids, int Pendientes);

public static class VigenciasDeVinculos
{
    /// <summary>Actor de los eventos que emite el job (historial del vínculo y del expediente de renovación).</summary>
    public const string Actor = "vigencia-vinculo";

    public const string DescripcionDelVencimiento = "Vigencia del vínculo";

    /// <summary>Cuánto vale el enlace del correo de pre-vencimiento y vencimiento: más que el de una observación, porque el aviso llega con días de antelación.</summary>
    public static readonly TimeSpan VigenciaDelEnlace = TimeSpan.FromDays(7);

    /// <summary>Vínculos que revisa un barrido como máximo; el resto queda para el siguiente.</summary>
    public const int MaximoPorBarrido = 2000;

    /// <summary>
    /// Vínculos con algo pendiente a <paramref name="ahora"/>, los más atrasados primero. El tenant de cada uno sale de su empresa
    /// (global): la consulta entre tenants no lo devuelve.
    /// </summary>
    public static async Task<IReadOnlyList<RevisarVigenciaDeVinculo>> PendientesAsync(IDocumentStore store, Guid? tenantId, DateTimeOffset ahora,
        CancellationToken ct)
    {
        IReadOnlyList<Vinculo> vinculos;
        if (tenantId is { } tenant)
        {
            await using var consulta = store.QuerySession(tenant.ToString("N"));
            vinculos = await consulta.Query<Vinculo>()
                .Where(v => v.ProximaRevisionDeVigencia != null && v.ProximaRevisionDeVigencia <= ahora)
                .OrderBy(v => v.ProximaRevisionDeVigencia).Take(MaximoPorBarrido).ToListAsync(ct);
        }
        else
        {
            await using var consulta = store.QuerySession();
            vinculos = await consulta.Query<Vinculo>()
                .Where(v => v.AnyTenant() && v.ProximaRevisionDeVigencia != null && v.ProximaRevisionDeVigencia <= ahora)
                .OrderBy(v => v.ProximaRevisionDeVigencia).Take(MaximoPorBarrido).ToListAsync(ct);
        }

        if (vinculos.Count == 0)
            return [];

        await using var global = store.QuerySession();
        var empresas = (await global.LoadManyAsync<Empresa>(ct, vinculos.Select(v => v.EmpresaId).Distinct().ToArray())).ToDictionary(e => e.Id);
        return
        [
            .. vinculos.Where(v => empresas.ContainsKey(v.EmpresaId)).Select(v => new RevisarVigenciaDeVinculo(empresas[v.EmpresaId].TenantId, v.Id))
        ];
    }

    /// <summary>
    /// Fija la vigencia del vínculo al aprobarse un expediente (VV-1): la versión del flujo con la que se aprobó dice los días, los
    /// avisos y la gracia, y las secciones con vigencia propia la acortan. Anexa <see cref="VigenciaDelVinculoFijada"/> si cambió algo y
    /// devuelve hasta cuándo vale, para que el certificado la tome en su vigencia.
    /// </summary>
    public static async Task<DateOnly?> FijarAlAprobarAsync(IDocumentSession sesion, IEventStream<Vinculo> stream, Vinculo vinculo, Expediente aprobado,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var version = aprobado.Flujo is { } delExpediente && await sesion.LoadAsync<Flujo>(delExpediente.FlujoId, ct) is { } flujo
            ? flujo.Versiones.FirstOrDefault(v => v.Id == delExpediente.FlujoVersionId)
            : null;
        var vigencia = version?.VigenciaEfectiva() ?? VigenciaDelVinculo.Sin;
        // Una sección eximida (EX-4) no aporta su vigencia propia: no puede llevar al vínculo a vencer mientras la exención rija.
        var requisitos = ExencionesDeVinculos.SinSeccionesEximidas(vinculo, version?.Requisitos ?? [], CalculadoraDeVigencia.Fecha(ahora)).ToList();
        var aceptadas = requisitos.Any(r => r is { EsSeccion: true, VigenciaDias: > 0 })
            ? await AceptadasAsync(sesion, vinculo.Id, aprobado, ct)
            : [];
        var hasta = VigenciaDeAprobacion.VigenteHasta(vigencia, requisitos, aceptadas, ahora);
        if (vinculo.FijarVigencia(hasta, vigencia.DiasAviso, vigencia.DiasGracia, ahora) is { } fijada)
        {
            stream.AppendOne(fijada);
            vinculo.Apply(fijada);
        }

        return hasta;
    }

    /// <summary>
    /// Lo que el vínculo tiene aceptado de cada sección: lo guardado y, encima, lo que acaba de aceptar <paramref name="aprobado"/>
    /// (todavía no escrito en esta transacción).
    /// </summary>
    private static async Task<IReadOnlyList<SeccionAceptadaEn>> AceptadasAsync(IQuerySession sesion, Guid vinculoId, Expediente? aprobado,
        CancellationToken ct)
    {
        var porClave = (await RequisitosDeExpediente.AceptadasAsync(sesion, vinculoId, ct)).Values
            .ToDictionary(a => a.Id, a => new SeccionAceptadaEn(a.Seccion, a.Periodo, a.AceptadaEn));
        foreach (var requisito in aprobado?.Requisitos.Where(r => r is { EsSeccion: true, AceptadaEn: not null }) ?? [])
            porClave[SeccionAceptada.Clave(vinculoId, requisito.Seccion!, requisito.Periodo)] =
                new SeccionAceptadaEn(requisito.Seccion!, requisito.Periodo, requisito.AceptadaEn!.Value);
        return [.. porClave.Values];
    }

    /// <summary>
    /// Revisa un vínculo con su stream bloqueado (no compite con una decisión simultánea): fin de un bloqueo temporal, vencimiento
    /// (con su renovación) o aviso, todo en una transacción con el outbox de <paramref name="mensajes"/>. A lo sumo una transición
    /// de vigencia por pasada; si tras desbloquear el vínculo ya debía vencer, venció durante el bloqueo y se vence en la misma pasada.
    /// </summary>
    public static async Task<ResultadoDeVigencia> RevisarAsync(ISesiones sesiones, IMessageBus mensajes, EmisorDeCertificados certificados,
        IProtectorDeSecretos protector, RevisarVigenciaDeVinculo mensaje, DateTimeOffset ahora, CancellationToken ct)
    {
        // Dentro de un manejador, Wolverine abre la sesión del outbox con el tenant del contexto del mensaje (ver CalendarioDeVigencias).
        if (mensajes is IMessageContext contexto)
            contexto.TenantId = mensaje.TenantId.ToString("N");
        await using var sesion = sesiones.ParaTenant(mensaje.TenantId, mensajes);
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", Actor);

        var stream = await sesion.Events.FetchForExclusiveWriting<Vinculo>(mensaje.VinculoId, ct);
        if (stream.Aggregate is not { } vinculo)
            return ResultadoDeVigencia.SinCambios;

        var resultado = ResultadoDeVigencia.SinCambios;
        if (vinculo.DesbloquearPorFecha(ahora) is { } desbloqueado)
        {
            // El mismo efecto del desbloqueo manual: certificado restablecido (o vencido si su vigencia pasó) y el evento del vínculo.
            await CicloDeCertificados.AlDesbloquearAsync(sesion, vinculo.Id, desbloqueado.Justificacion, ahora, ct);
            stream.AppendOne(desbloqueado);
            vinculo.Apply(desbloqueado);
            // Como el desbloqueo manual (PP-9): se le restituye la compartición y se abre la actualización si lo aprobado ya no es lo actual.
            await mensajes.PublishAsync(new RevalidarVinculoDesbloqueado(mensaje.TenantId, vinculo.Id, nameof(ActorTipo.Sistema), Actor));
            resultado = ResultadoDeVigencia.Desbloqueado;
        }

        // Una exención cuya fecha pasó termina aquí (EX-4): se registra y el estado se recalcula en esta misma pasada, con la regla de siempre (sin la
        // exención, el vínculo puede vencer o avisar de inmediato) y los documentos que vencieron mientras rigió se vencen ahora.
        var vencidas = vinculo.VencerExenciones(ahora);
        if (vencidas.Count > 0)
        {
            foreach (var exencion in vencidas)
            {
                stream.AppendOne(exencion);
                vinculo.Apply(exencion); // idempotente: el agregado queda con la exención ya vencida para lo que sigue
            }

            await ExencionesDeVinculos.ReevaluarAlTerminarAsync(sesion, mensajes, vinculo, mensaje.TenantId, ahora, ct);
            resultado = ResultadoDeVigencia.ExencionVencida;
        }

        if (vinculo.VencerPorVigencia(ahora, DescripcionDelVencimiento) is { } vencido)
            resultado = await VencerAsync(sesion, stream, vinculo, vencido, certificados, mensajes, protector, ahora, ct);
        else if (vinculo.AvisarVigencia(ahora) is { } aviso)
            resultado = await AvisarAsync(sesion, stream, vinculo, aviso, certificados, mensajes, protector, ahora, ct);
        else if (vinculo is { Estado: EstadoVinculo.Vencido, RenovacionPorVigenciaPendiente: true, ExpedienteAbiertoId: null } &&
                 vinculo.ExencionTotal(CalculadoraDeVigencia.Fecha(ahora)) is null)
        {
            // Venció por su vigencia y no se pudo abrir la renovación (un expediente ocupaba el lugar, o no había flujo): se reintenta.
            if (await RenovarAsync(sesion, stream, vinculo, certificados, mensajes, protector, ahora, ct) is { } renovado)
                resultado = renovado;
        }

        if (resultado != ResultadoDeVigencia.SinCambios)
            await sesion.SaveChangesAsync(ct);
        return resultado;
    }

    /// <summary>
    /// Llegó un aviso (VV-3). El primero abre el pre-vencimiento: el vínculo queda <c>PorVencer</c> (el certificado sigue vigente) y
    /// se abre la renovación con lo que el tercero debe ratificar o actualizar antes de la fecha; los siguientes solo le recuerdan
    /// la renovación pendiente (la abierta no se duplica; si la anterior se cerró sin aprobarse se abre otra). Si no hay nada que
    /// pedirle, la vigencia se renueva sola desde ahora y el vínculo no llega a <c>PorVencer</c>.
    /// </summary>
    private static async Task<ResultadoDeVigencia> AvisarAsync(IDocumentSession sesion, IEventStream<Vinculo> stream, Vinculo vinculo, VinculoPorVencer aviso,
        EmisorDeCertificados certificados, IMessageBus mensajes, IProtectorDeSecretos protector, DateTimeOffset ahora, CancellationToken ct)
    {
        var flujo = vinculo.ExpedienteAbiertoId is null ? await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct) : null;
        RequisitosPorRenovar? porRatificar = null;
        if (flujo is not null)
        {
            porRatificar = await RequisitosDeRenovacionAsync(sesion, vinculo, flujo.Version, ahora, ct);
            if (porRatificar.Pendientes == 0)
            {
                await RenovarSinPedirNadaAsync(sesion, stream, vinculo, flujo.Version, ahora, ct);
                return ResultadoDeVigencia.Renovado;
            }
        }

        stream.AppendOne(aviso);
        vinculo.Apply(aviso);
        if (flujo is not null && porRatificar is not null &&
            !(await AbrirRenovacionAsync(sesion, stream, flujo, porRatificar.Ids, certificados, ahora, ct)).EstaAbierto)
            return ResultadoDeVigencia.Renovado; // Todo se aprobó solo: el vínculo quedó al día, no hay nada que avisar.

        await AvisarAlTerceroAsync(sesion, mensajes, protector, vinculo, AvisoDeVigencia.PorVencer, aviso.VigenteHasta, aviso.DiasRestantes, ahora, ct);
        return ResultadoDeVigencia.Avisado;
    }

    /// <summary>
    /// Terminó la gracia (VV-3): si hay secciones (o documentos) por ratificar, el vínculo pasa a <c>Vencido</c>, el certificado vence
    /// y la renovación ya abierta en el pre-vencimiento sigue siendo la misma (no se abre otra; si no la había, se abre ahora); si
    /// no hay nada que pedirle al tercero, la vigencia se renueva sola y el vínculo sigue como estaba.
    /// </summary>
    private static async Task<ResultadoDeVigencia> VencerAsync(IDocumentSession sesion, IEventStream<Vinculo> stream, Vinculo vinculo, VinculoVencido vencido,
        EmisorDeCertificados certificados, IMessageBus mensajes, IProtectorDeSecretos protector, DateTimeOffset ahora, CancellationToken ct)
    {
        var conRenovacion = vinculo.TieneRenovacionAbierta;
        if (!conRenovacion)
        {
            var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct);
            if (flujo is not null && (await RequisitosDeRenovacionAsync(sesion, vinculo, flujo.Version, ahora, ct)).Pendientes == 0)
            {
                await RenovarSinPedirNadaAsync(sesion, stream, vinculo, flujo.Version, ahora, ct);
                return ResultadoDeVigencia.Renovado;
            }
        }

        var hasta = vinculo.VigenteHasta!.Value;
        stream.AppendOne(vencido);
        vinculo.Apply(vencido);
        // El certificado sigue la vigencia del vínculo: pasó su fecha, así que vence (el procesador lo habría hecho por reloj).
        var certificado = await sesion.Events.FetchForExclusiveWriting<Certificado>(Certificado.IdPara(vinculo.Id), ct);
        if (certificado.Aggregate is { Estado: EstadoCertificado.Vigente or EstadoCertificado.ConPendientes, VenceDesde: { } vence } actual && ahora >= vence)
            certificado.AppendOne(actual.Vencer(ahora));

        if (conRenovacion)
            await AvisarAlTerceroAsync(sesion, mensajes, protector, vinculo, AvisoDeVigencia.Vencido, hasta, 0, ahora, ct);
        else
            await RenovarAsync(sesion, stream, vinculo, certificados, mensajes, protector, ahora, ct);
        return ResultadoDeVigencia.Vencido;
    }

    /// <summary>
    /// Abre la renovación del vínculo vencido por su vigencia cuando no se abrió en el pre-vencimiento (VV-3): un expediente de
    /// <c>Renovacion</c> con las secciones de la ficha que el flujo pide (el tercero las ratifica o las actualiza) y los documentos
    /// obligatorios que ya no están vigentes, abierto por el sistema. Devuelve nulo si no se pudo (sin flujo publicado o con otro
    /// expediente abierto), <see cref="ResultadoDeVigencia.Renovado"/> si no había nada que ratificar y
    /// <see cref="ResultadoDeVigencia.Vencido"/> si abrió la renovación.
    /// </summary>
    private static async Task<ResultadoDeVigencia?> RenovarAsync(IDocumentSession sesion, IEventStream<Vinculo> stream, Vinculo vinculo,
        EmisorDeCertificados certificados, IMessageBus mensajes, IProtectorDeSecretos protector, DateTimeOffset ahora, CancellationToken ct)
    {
        if (vinculo.ExpedienteAbiertoId is not null || await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct) is not { } flujo)
            return null;

        var ids = await RequisitosDeRenovacionAsync(sesion, vinculo, flujo.Version, ahora, ct);
        if (ids.Pendientes == 0)
        {
            await RenovarSinPedirNadaAsync(sesion, stream, vinculo, flujo.Version, ahora, ct);
            return ResultadoDeVigencia.Renovado;
        }

        var expediente = await AbrirRenovacionAsync(sesion, stream, flujo, ids.Ids, certificados, ahora, ct);
        if (expediente.EstaAbierto && vinculo.VigenteHasta is { } hasta)
            await AvisarAlTerceroAsync(sesion, mensajes, protector, vinculo, AvisoDeVigencia.Vencido, hasta, 0, ahora, ct);

        return ResultadoDeVigencia.Vencido;
    }

    /// <summary>
    /// Abre el expediente de <c>Renovacion</c> del vínculo con <paramref name="requisitos"/>: las secciones que la empresa ya aceptó
    /// nacen pendientes de ratificación y el sistema lo abre en nombre del job. Si todo se aprueba solo, el expediente nace cerrado.
    /// </summary>
    private static Task<Expediente> AbrirRenovacionAsync(IDocumentSession sesion, IEventStream<Vinculo> stream, FlujoResuelto flujo,
        IReadOnlySet<Guid> requisitos, EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct) =>
        ProcesoDeValidacion.AbrirAsync(sesion, stream, flujo, null, certificados, ahora, ct, tipoExpediente: TipoExpediente.Renovacion,
            soloRequisitos: requisitos, actor: new ActorDeExpediente(ActorTipo.Sistema, Actor), exigirRatificacion: true);

    /// <summary>
    /// Sin secciones ni documentos que pedirle al tercero (el flujo ya no los pide, o no aplican a este tercero) la vigencia no tiene
    /// qué renovar: se fija de nuevo desde ahora y el vínculo sigue (o vuelve a) <c>Activo</c>.
    /// </summary>
    private static async Task RenovarSinPedirNadaAsync(IDocumentSession sesion, IEventStream<Vinculo> stream, Vinculo vinculo, FlujoVersion version,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var vigencia = version.VigenciaEfectiva();
        var requisitos = ExencionesDeVinculos.SinSeccionesEximidas(vinculo, version.Requisitos, CalculadoraDeVigencia.Fecha(ahora));
        var hasta = VigenciaDeAprobacion.VigenteHasta(vigencia, requisitos, await AceptadasAsync(sesion, vinculo.Id, null, ct), ahora);
        // La vigencia desde ahora: si la fecha no cambia (sin vigencia periódica ni de secciones) el vínculo queda como estaba.
        if (vinculo.FijarVigencia(hasta, vigencia.DiasAviso, vigencia.DiasGracia, ahora) is { } fijada)
        {
            stream.AppendOne(fijada);
            vinculo.Apply(fijada);
        }
    }

    /// <summary>
    /// Requisitos de la renovación (VV-3): si lo que vence es la vigencia periódica, todas las secciones que el flujo pide y la
    /// empresa ya aceptó (o que son obligatorias y nunca se aceptaron) y los documentos obligatorios que ya no tienen una versión
    /// vigente; si lo que vence es la vigencia propia de una sección, solo esa sección. En el pre-vencimiento (antes de la fecha) se
    /// mira lo que habrá vencido el día siguiente al fin de la vigencia: es lo que el tercero debe ratificar o actualizar a tiempo.
    /// </summary>
    internal static async Task<RequisitosPorRenovar> RequisitosDeRenovacionAsync(IDocumentSession sesion, Vinculo vinculo, FlujoVersion version,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var contexto = await RequisitosDeExpediente.ContextoAsync(sesion, vinculo, ct);
        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        var referencia = vinculo.VigenteHasta is { } fin && fin.AddDays(1) > hoy ? fin.AddDays(1) : hoy;
        var periodica = VigenciaDeAprobacion.PeriodicaVencida(version.VigenciaEfectiva(), vinculo.UltimaAprobacionEn, referencia);
        var aceptadas = await RequisitosDeExpediente.AceptadasAsync(sesion, vinculo.Id, ct);
        HashSet<Guid> vencidas = periodica
            ? []
            : [.. VigenciaDeAprobacion.SeccionesVencidas(version.Requisitos, aceptadas.Values.Select(a => new SeccionAceptadaEn(a.Seccion, a.Periodo, a.AceptadaEn)), referencia)
                .Select(r => r.Id)];

        var ids = new HashSet<Guid>();
        var eximidos = new HashSet<Guid>();
        foreach (var seccion in RequisitosDeExpediente.SeccionesDelFlujo(version, contexto, hoy))
        {
            var aceptada = RequisitosDeExpediente.Aceptada(aceptadas, vinculo.Id, seccion) is not null;
            if (!(periodica ? aceptada || seccion.Requisito.Obligatorio : aceptada && vencidas.Contains(seccion.Requisito.Id)))
                continue;
            ids.Add(seccion.RequisitoId);
            if (vinculo.ExencionDeSeccion(seccion.Requisito.Seccion, hoy) is not null)
                eximidos.Add(seccion.RequisitoId); // entra a la renovación ya Eximido (EX-3) y no cuenta como algo por pedir
        }

        if (periodica)
        {
            var terceroId = vinculo.TerceroId;
            var delTercero = await sesion.ForTenant(TenantsTecnicos.Boveda).Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
            foreach (var pedido in await RequisitosDeExpediente.DelFlujoAsync(sesion, version, contexto, ct))
            {
                if (pedido.Requisito.Obligatorio &&
                    RequisitosDeExpediente.Cubre(pedido.Tipo.Id, pedido.Tipo.TipoCanonico, pedido.Tipo.Ambito, delTercero, vinculo.Id) is null)
                {
                    ids.Add(pedido.Requisito.Id);
                    if (vinculo.ExencionDeDocumento(pedido.Tipo.Id, hoy) is not null)
                        eximidos.Add(pedido.Requisito.Id);
                }
            }
        }

        return new RequisitosPorRenovar(ids, ids.Count - eximidos.Count);
    }

    /// <summary>
    /// Correo al tercero (aviso del pre-vencimiento, recordatorio o vencimiento) con un enlace de acceso a su renovación, creado en
    /// esta misma transacción (como el del correo de observación, RE-4). Sin correo del tercero no avisa.
    /// </summary>
    internal static async Task AvisarAlTerceroAsync(IDocumentSession sesion, IMessageBus mensajes, IProtectorDeSecretos protector, Vinculo vinculo,
        AvisoDeVigencia aviso, DateOnly hasta, int diasRestantes, DateTimeOffset ahora, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(vinculo.CorreoTercero))
            return;

        var empresa = await sesion.LoadAsync<Empresa>(vinculo.EmpresaId, ct);
        var tipo = await sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct);
        var (enlace, secreto) = EnlaceDeAcceso.Emitir(empresa?.TenantId, vinculo.Id, vinculo.TerceroId, vinculo.CorreoTercero,
            ahora + VigenciaDelEnlace, tipo?.ExigeOtpEnEnlace ?? false, ahora, MotivoEnlace.Observacion);
        sesion.Store(enlace);
        await mensajes.PublishAsync(new CorreoDeVigenciaPendiente(vinculo.CorreoTercero, empresa?.Nombre ?? "La empresa", aviso, hasta, diasRestantes,
            protector.Proteger(secreto), enlace.Expira, empresa?.TenantId, vinculo.EmpresaId, vinculo.TerceroId));
    }
}

public static class RevisarVigenciasDeVinculosHandler
{
    /// <summary>Pide la revisión de cada vínculo pendiente por la cola durable (cada uno en su transacción, con sus reintentos).</summary>
    public static async Task Handle(RevisarVigenciasDeVinculos mensaje, IDocumentStore store, IMessageContext mensajes, TimeProvider reloj, CancellationToken ct)
    {
        foreach (var pendiente in await VigenciasDeVinculos.PendientesAsync(store, mensaje.TenantId, mensaje.Ahora ?? reloj.GetUtcNow(), ct))
            await mensajes.PublishAsync(pendiente with { Ahora = mensaje.Ahora });
    }
}

public static class RevisarVigenciaDeVinculoHandler
{
    public static Task Handle(RevisarVigenciaDeVinculo mensaje, ISesiones sesiones, IMessageContext mensajes, EmisorDeCertificados certificados,
        IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct) =>
        VigenciasDeVinculos.RevisarAsync(sesiones, mensajes, certificados, protector, mensaje, mensaje.Ahora ?? reloj.GetUtcNow(), ct);
}

public static class DesbloquearAlLlegarLaFechaHandler
{
    /// <summary>Misma revisión que el barrido: <c>DesbloquearPorFecha</c> decide (un bloqueo ya levantado o con otra fecha no cambia).</summary>
    public static Task Handle(DesbloquearAlLlegarLaFecha mensaje, ISesiones sesiones, IMessageContext mensajes, EmisorDeCertificados certificados,
        IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct) =>
        VigenciasDeVinculos.RevisarAsync(sesiones, mensajes, certificados, protector, new RevisarVigenciaDeVinculo(mensaje.TenantId, mensaje.VinculoId),
            reloj.GetUtcNow(), ct);
}

/// <summary>
/// Dispara el barrido de vigencias de vínculos cada <c>Validasoft:VigenciasDeVinculos:Intervalo</c> (por omisión, una hora;
/// <c>00:00:00</c> lo apaga). Con varias réplicas cada una lo pide: el barrido es idempotente (cada vínculo se revisa con su stream
/// bloqueado).
/// </summary>
public sealed class ProgramadorDeVigenciasDeVinculos(IServiceProvider servicios, IConfiguration configuracion, ILogger<ProgramadorDeVigenciasDeVinculos> logger)
    : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = configuracion.GetValue("Validasoft:VigenciasDeVinculos:Intervalo", TimeSpan.FromHours(1));
        if (intervalo <= TimeSpan.Zero)
            return;

        // El primero al minuto de arrancar (lo que venció mientras la API estaba detenida) y luego cada intervalo.
        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1) < intervalo ? TimeSpan.FromMinutes(1) : intervalo, stoppingToken);
            using var temporizador = new PeriodicTimer(intervalo);
            do
            {
                try
                {
                    await using var alcance = servicios.CreateAsyncScope();
                    await alcance.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new RevisarVigenciasDeVinculos());
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "No se pudo pedir el barrido de vigencias de vínculos.");
                }
            } while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la API.
        }
    }
}

/// <summary>Ejecución inmediata del job de vigencias (Plataforma).</summary>
public static class VigenciasDeVinculosEndpoints
{
    /// <summary>
    /// Corre ya el job de vigencias de vínculos (Plataforma) y espera su resultado: de un tenant o de todos. <c>fecha</c> simula «ahora» y
    /// solo se admite en Development (pruebas de punta a punta; 422 <c>fecha-simulada-no-permitida</c> en otro entorno). Cada llamada
    /// avanza a lo sumo una transición por vínculo (aviso, vencimiento o fin de bloqueo).
    /// </summary>
    [WolverinePost("/api/v1/plataforma/vigencias-de-vinculos/revisar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResultadoRevisionVigenciasDto> Revisar(RevisarVigenciasDto dto, IDocumentStore store, ISesiones sesiones, IWolverineRuntime runtime,
        EmisorDeCertificados certificados, IProtectorDeSecretos protector, IHostEnvironment entorno, TimeProvider reloj, CancellationToken ct)
    {
        if (dto.Fecha is not null && !entorno.IsDevelopment())
            throw ReglaDeNegocioException.Validacion("fecha-simulada-no-permitida", "La fecha simulada solo se admite en Development.");

        if (dto.IncluirDocumentos && (dto.Fecha is null || dto.TenantId is null))
            throw ReglaDeNegocioException.Validacion("fecha-requerida", "Para incluir los documentos indica el tenant y la fecha simulada.");

        var ahora = dto.Fecha ?? reloj.GetUtcNow();
        // Primero los documentos: lo que vence aquí abre la renovación del vínculo por la cola, igual que su mensaje programado.
        var documentosVencidos = dto.IncluirDocumentos ? await VencerDocumentosAsync(store, sesiones, runtime, dto.TenantId!.Value, ahora, ct) : 0;
        var pendientes = await VigenciasDeVinculos.PendientesAsync(store, dto.TenantId, ahora, ct);
        int avisados = 0, vencidos = 0, desbloqueados = 0;
        foreach (var pendiente in pendientes)
        {
            // Un contexto de mensajería por vínculo: cada uno con su transacción y su outbox.
            var resultado = await VigenciasDeVinculos.RevisarAsync(sesiones, new MessageContext(runtime), certificados, protector, pendiente, ahora, ct);
            switch (resultado)
            {
                case ResultadoDeVigencia.Avisado:
                    avisados++;
                    break;
                case ResultadoDeVigencia.Vencido or ResultadoDeVigencia.Renovado:
                    vencidos++;
                    break;
                case ResultadoDeVigencia.Desbloqueado:
                    desbloqueados++;
                    break;
            }
        }

        return new ResultadoRevisionVigenciasDto(pendientes.Count, avisados, vencidos, desbloqueados, documentosVencidos);
    }

    /// <summary>
    /// Vence, a la fecha simulada <paramref name="ahora"/>, las versiones actuales de los documentos compartidos con el tenant cuya
    /// gracia ya terminó: el mismo manejador del mensaje programado <see cref="VencerVersionDeDocumento"/>, con un reloj fijo. Devuelve
    /// cuántas venció. Solo para pruebas (Development).
    /// </summary>
    private static async Task<int> VencerDocumentosAsync(IDocumentStore store, ISesiones sesiones, IWolverineRuntime runtime, Guid tenantId, DateTimeOffset ahora,
        CancellationToken ct)
    {
        List<Documento> documentos;
        await using (var consulta = store.QuerySession(TenantsTecnicos.Boveda))
            documentos = [.. await consulta.Query<Documento>().Where(d => d.Comparticiones.Any(c => c.TenantId == tenantId)).ToListAsync(ct)];

        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        var reloj = new RelojFijo(ahora);
        var vencidos = 0;
        foreach (var documento in documentos.Where(d => d.VersionActual is not null))
        {
            var version = documento.Buscar(documento.VersionActual!.Value)!;
            foreach (var vigencia in version.Vigencias.Where(v => v.TenantId == tenantId && v.VencidaEn is null && v.VigenteHasta is not null && hoy > v.FinDeGracia))
            {
                await VencerVersionDeDocumentoHandler.Handle(
                    new VencerVersionDeDocumento(documento.Id, version.Numero, vigencia.VinculoId, vigencia.VigenteHasta!.Value, vigencia.DiasGracia),
                    sesiones, new MessageContext(runtime), reloj, ct);
                vencidos++;
            }
        }

        return vencidos;
    }

    private sealed class RelojFijo(DateTimeOffset ahora) : TimeProvider
    {
        public override DateTimeOffset GetUtcNow() => ahora;
    }
}
