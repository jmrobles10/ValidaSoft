using JasperFx.Events;
using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>Versión de flujo elegida para un expediente y cómo se eligió.</summary>
public sealed record FlujoResuelto(Flujo Flujo, FlujoVersion Version, OrigenFlujo Origen);

/// <summary>
/// Proceso de validación vínculo ↔ expediente (docs/03 §8) resuelto <b>en la misma transacción</b> (FL-5): abrir el
/// expediente registra <c>ExpedienteVinculado</c> en el vínculo; aprobarlo, rechazarlo o anularlo registra
/// <c>ExpedienteCerrado</c> (y <c>VinculoActivado</c> si se aprobó). Los dos streams son del mismo tenant y se escriben
/// con la misma sesión, de modo que nunca quedan desalineados. También es el único punto que escribe la bandeja (FL-8) y
/// publica el correo de observación al tercero.
/// </summary>
public static class ProcesoDeValidacion
{
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
    /// Abre el expediente inicial del vínculo con el flujo resuelto: consume el folio, inicia el stream del expediente,
    /// registra <c>ExpedienteVinculado</c> en <paramref name="vinculoStream"/> y, si todo fue automático, lo cierra aprobado.
    /// </summary>
    public static async Task<Expediente> AbrirAsync(IDocumentSession sesion, IEventStream<Vinculo> vinculoStream, FlujoResuelto flujo,
        IContextoSolicitud contexto, DateTimeOffset ahora, CancellationToken ct)
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
        var etapas = flujo.Version.Etapas.Select(e => new EtapaDelFlujo(e.Orden, [.. e.Areas.Select(a => new AreaDelFlujo(a.AreaId,
            areas.TryGetValue(a.AreaId, out var area) ? area.Codigo : a.AreaId.ToString("N"),
            areas.TryGetValue(a.AreaId, out var conNombre) ? conNombre.Nombre : "Área", a.AprobadoresRequeridos, a.PuedeRechazarExpediente,
            a.Automatica, a.SlaHoras))])).ToList();

        var folio = await SiguienteFolioAsync(sesion, empresa, tenant, ahora, ct);
        var datos = new DatosDeApertura(vinculo.EmpresaId, vinculo.Id, TipoExpediente.Inicial, folio, tercero.Id, tercero.Nombre,
            tercero.ClaveIdentificacion, vinculo.TipoVinculoId, tipo?.Codigo ?? string.Empty, tipo?.Nombre ?? string.Empty,
            new ActorDeExpediente(contexto.ActorTipo, contexto.ActorId), await QuienesInvitaronAsync(sesion, vinculo.Id, ct));
        var eventos = Expediente.Abrir(datos, new FlujoParaExpediente(flujo.Flujo.Id, flujo.Version.Id, flujo.Flujo.Codigo, flujo.Flujo.Nombre,
            flujo.Version.Version, flujo.Origen, etapas, flujo.Version.PoliticaDevolucion, flujo.Version.SeparacionDeFunciones), ahora);

        var expedienteId = ((ExpedienteAbierto)eventos[0]).ExpedienteId;
        var vinculado = vinculo.VincularExpediente(expedienteId, TipoExpediente.Inicial, folio, ahora);
        vinculoStream.AppendOne(vinculado);
        if (vinculo.ExpedienteAbiertoId != expedienteId)
            vinculo.Apply(vinculado); // El agregado del stream decide el cierre inmediato (todo automático) con el estado al día.

        sesion.Events.StartStream<Expediente>(expedienteId, eventos);
        var estado = Expediente.Reproducir(null, eventos);
        await DespuesDeEscribirAsync(sesion, estado, eventos, vinculoStream, null, ahora, ct);
        return estado;
    }

    /// <summary>
    /// Anexa los eventos de una decisión al expediente y propaga sus efectos en la misma sesión: bandeja, cierre del
    /// vínculo y, con <paramref name="mensajes"/>, el correo de observación.
    /// </summary>
    public static async Task<Expediente> AnexarAsync(IDocumentSession sesion, IEventStream<Expediente> stream, IReadOnlyList<object> eventos,
        IMessageBus? mensajes, DateTimeOffset ahora, CancellationToken ct)
    {
        var estado = Expediente.Reproducir(stream.Aggregate, eventos);
        stream.AppendMany(eventos);
        await DespuesDeEscribirAsync(sesion, estado, eventos, null, mensajes, ahora, ct);
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
        IEventStream<Vinculo>? vinculoStream, IMessageBus? mensajes, DateTimeOffset ahora, CancellationToken ct)
    {
        foreach (var fila in ProyeccionBandejaRevision.Filas(estado))
            sesion.Store(fila);

        ResultadoExpediente? resultado = eventos.OfType<ExpedienteAprobado>().Any() ? ResultadoExpediente.Aprobado
            : eventos.OfType<ExpedienteRechazado>().Any() ? ResultadoExpediente.Rechazado
            : eventos.OfType<ExpedienteAnulado>().Any() ? ResultadoExpediente.Anulado
            : null;

        if (resultado is { } cierre)
        {
            vinculoStream ??= await sesion.Events.FetchForWriting<Vinculo>(estado.VinculoId, ct);
            if (vinculoStream.Aggregate is { } vinculo && vinculo.ExpedienteAbiertoId == estado.Id)
                vinculoStream.AppendMany(vinculo.CerrarExpediente(estado.Id, cierre, ahora));
        }

        if (mensajes is not null && eventos.OfType<ExpedienteObservado>().Any())
            await PublicarObservacionAsync(sesion, estado, mensajes, ct);
    }

    /// <summary>Correo al tercero con motivos y observaciones vigentes, sin revisores ni áreas (outbox).</summary>
    private static async Task PublicarObservacionAsync(IDocumentSession sesion, Expediente estado, IMessageBus mensajes, CancellationToken ct)
    {
        var vinculo = await sesion.LoadAsync<Vinculo>(estado.VinculoId, ct);
        if (vinculo is null || string.IsNullOrWhiteSpace(vinculo.CorreoTercero))
            return;

        var empresa = await sesion.LoadAsync<Empresa>(estado.EmpresaId, ct);
        var observaciones = estado.ObservacionesVigentes
            .Select(v => new ObservacionParaCorreo([.. v.Motivos.Select(m => m.TextoAlTercero)], v.Observacion))
            .ToList();
        await mensajes.PublishAsync(new CorreoDeObservacionPendiente(vinculo.CorreoTercero, empresa?.Nombre ?? "La empresa", estado.Folio,
            observaciones));
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
        if (usuario is not { Activo: true } || !area.Miembros.Contains(usuarioId) || !usuario.TieneRolEn(empresa.EmpresaId))
            throw ReglaDeNegocioException.Prohibido("no-es-miembro-del-area", $"No eres miembro activo del área {area.Codigo}.");
        if (!area.Activa)
            throw ReglaDeNegocioException.Conflicto("area-inactiva", $"El área {area.Codigo} está inactiva.");

        return new Revisor(usuarioId, usuario.Identificacion?.Clave);
    }
}
