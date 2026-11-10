using Marten;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Revalidación por versión nueva (RV-1…RV-3, H6). Cuando la bóveda comparte con un vínculo ya aprobado y sin expediente
/// abierto una versión distinta de un documento que la empresa aprobó en su último expediente, se abre un expediente de
/// <c>Actualizacion</c> solo con esos documentos (nacen cargados, así que pasa directo a revisión), el vínculo conserva su
/// estado y el certificado vigente queda «Con pendientes» hasta que la actualización se apruebe y se reemita. Lo mismo con
/// las secciones de la ficha (F2, las Actualizaciones del SRM): una versión nueva, completa para la empresa, de una sección
/// que el vínculo ya tenía aceptada abre (o entra en) la actualización solo con esa sección.
/// </summary>
public static class Revalidacion
{
    /// <summary>Días para resolver la revalidación antes de que los pendientes del certificado bloqueen (RV-4, A-2).</summary>
    public const int DiasDePlazo = 15;

    /// <summary>
    /// Abre la actualización si hace falta (idempotente: con un expediente abierto o sin cambios no hace nada). Escribe en
    /// <paramref name="sesion"/> sin guardar. Devuelve el expediente abierto, o nulo.
    /// </summary>
    public static async Task<Expediente?> AbrirSiCorrespondeAsync(IDocumentSession sesion, Guid vinculoId, ActorDeExpediente actor,
        EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct, IMessageBus? mensajes = null)
    {
        var vinculoStream = await sesion.Events.FetchForWriting<Vinculo>(vinculoId, ct);
        if (vinculoStream.Aggregate is not { } vinculo || vinculo.ExpedienteAbiertoId is not null || !vinculo.TuvoAprobacion ||
            vinculo.Estado is not (EstadoVinculo.Activo or EstadoVinculo.PorVencer or EstadoVinculo.Vencido))
            return null;

        var aprobado = (await sesion.Query<Expediente>().Where(e => e.VinculoId == vinculoId && e.Estado == EstadoExpediente.Aprobado).ToListAsync(ct))
            .OrderByDescending(e => e.ActualizadoEn)
            .FirstOrDefault();
        var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct);
        if (flujo is null)
            return null;

        var contexto = await RequisitosDeExpediente.ContextoAsync(sesion, vinculo, ct);
        // Los documentos se comparan con lo que aprobó el último expediente; las secciones, con lo aceptado (SeccionAceptada), que
        // también puede venir de la importación del Portal sin expediente en Validasoft (F4, FM-42).
        List<RequisitoPedido> afectados = [];
        if (aprobado is not null)
        {
            var pedidos = await RequisitosDeExpediente.DelFlujoAsync(sesion, flujo.Version, contexto, ct);
            var terceroId = vinculo.TerceroId;
            var documentos = await sesion.ForTenant(TenantsTecnicos.Boveda).Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
            afectados = pedidos
                .Where(p => RequisitosDeExpediente.Cubre(p.Tipo.Id, p.Tipo.TipoCanonico, p.Tipo.Ambito, documentos, vinculoId) is { } actual &&
                            AprobadoAntes(aprobado, p) is { } previa &&
                            (previa.DocumentoId != actual.DocumentoId || previa.Numero != actual.Numero))
                .ToList();
        }

        var secciones = await SeccionesCambiadasAsync(sesion, vinculo, flujo, contexto, ahora, ct);
        if (afectados.Count == 0 && secciones.Count == 0)
            return null;

        var expediente = await ProcesoDeValidacion.AbrirAsync(sesion, vinculoStream, flujo, null, certificados, ahora, ct,
            tipoExpediente: TipoExpediente.Actualizacion,
            soloRequisitos: afectados.Select(p => p.Requisito.Id).Concat(secciones.Select(s => s.RequisitoId)).ToHashSet(), actor: actor);

        var certificado = await sesion.Events.FetchForWriting<Certificado>(Certificado.IdPara(vinculoId), ct);
        var motivo = MotivoDePendientes([.. afectados.Select(p => p.Tipo.Nombre), .. secciones.Select(s => s.Nombre)]);
        if (expediente.EstaAbierto && certificado.Aggregate?.MarcarConPendientes(motivo, expediente.Id, ahora) is { } pendientes)
            certificado.AppendOne(pendientes);
        if (expediente.EstaAbierto && mensajes is not null && Guid.TryParse(sesion.TenantId, out var tenantId))
            await mensajes.ScheduleAsync(new VencerPlazoDeRevalidacion(tenantId, vinculoId, expediente.Id), ahora.AddDays(DiasDePlazo));
        return expediente;
    }

    /// <summary>
    /// Secciones que el flujo pide al vínculo, que la empresa ya tenía aceptadas (<see cref="SeccionAceptada"/>) y cuya versión
    /// vigente, completa para la empresa, es otra: entran a la actualización. Una versión nueva incompleta (el tercero aún la
    /// diligencia) no abre nada.
    /// </summary>
    private static async Task<IReadOnlyList<RequisitoDeSeccionPedido>> SeccionesCambiadasAsync(IDocumentSession sesion, Vinculo vinculo,
        FlujoResuelto flujo, Dominio.Flujos.ContextoDeExigencia contexto, DateTimeOffset ahora, CancellationToken ct)
    {
        var pedidas = RequisitosDeExpediente.SeccionesDelFlujo(flujo.Version, contexto, CalculadoraDeVigencia.Fecha(ahora));
        if (pedidas.Count == 0)
            return [];

        var aceptadas = await RequisitosDeExpediente.AceptadasAsync(sesion, vinculo.Id, ct);
        if (aceptadas.Count == 0)
            return [];

        var ficha = await RequisitosDeExpediente.FichaAsync(sesion, vinculo.TerceroId, ct);
        return
        [
            .. pedidas.Where(p => RequisitosDeExpediente.Aceptada(aceptadas, vinculo.Id, p) is { } aceptada &&
                                  RequisitosDeSeccion.Evaluar(p, ficha.GetValueOrDefault(p.SeccionId(vinculo.TerceroId))).Disponible is { } actual &&
                                  actual.Numero != aceptada.Numero)
        ];
    }

    /// <summary>«Revalidación de Registro Único Tributario y Cámara de comercio».</summary>
    public static string MotivoDePendientes(IReadOnlyList<string> nombres)
    {
        var lista = nombres.Count == 1 ? nombres[0] : $"{string.Join(", ", nombres.Take(nombres.Count - 1))} y {nombres[^1]}";
        return $"Revalidación de {lista}";
    }

    private static VersionDeRequisito? AprobadoAntes(Expediente aprobado, RequisitoPedido pedido) =>
        AprobadoEn(aprobado, pedido.Tipo.Id, pedido.Tipo.TipoCanonico, pedido.Tipo.Ambito);

    /// <summary>La versión que la empresa aprobó para ese tipo de documento en el expediente (no eximida); nula si no lo aprobó.</summary>
    public static VersionDeRequisito? AprobadoEn(Expediente aprobado, Guid tipoDocumentoTenantId, string? tipoCanonico, AmbitoDocumento ambito) =>
        aprobado.Requisitos
            .Where(r => !r.EsSeccion && !r.Eximido && r.Estado == EstadoRequisito.Aprobado && r.VersionActual is not null &&
                        (r.TipoDocumentoTenantId == tipoDocumentoTenantId ||
                         (r.Ambito == AmbitoDocumento.Global && ambito == AmbitoDocumento.Global &&
                          r.TipoCanonico is not null && r.TipoCanonico == tipoCanonico)))
            .Select(r => r.VersionActual)
            .FirstOrDefault();

    /// <summary>
    /// Lo que la empresa aprobó antes para el requisito: un documento por su tipo (<see cref="AprobadoEn(Expediente, Guid, string?, AmbitoDocumento)"/>);
    /// una sección por su sección y periodo.
    /// </summary>
    public static VersionDeRequisito? AprobadoEn(Expediente aprobado, RequisitoDeExpediente requisito) =>
        requisito.EsSeccion
            ? requisito.VersionAceptadaPrevia ?? aprobado.Requisitos
                .Where(r => r.EsSeccion && !r.Eximido && r.Estado == EstadoRequisito.Aprobado && r.Seccion == requisito.Seccion &&
                            r.Periodo == requisito.Periodo)
                .Select(r => r.VersionActual)
                .FirstOrDefault()
            : AprobadoEn(aprobado, requisito.TipoDocumentoTenantId, requisito.TipoCanonico, requisito.Ambito);

    /// <summary>Hasta cuándo puede resolverse la revalidación antes de que sus pendientes bloqueen (RV-4).</summary>
    public static DateTimeOffset PlazoHasta(Expediente actualizacion) => actualizacion.AbiertoEn.AddDays(DiasDePlazo);

    /// <summary>
    /// ¿Se puede aprobar en lote (A-18)? Solo si la bóveda no ve cambios frente a la anterior: <c>Identica</c> por hash o
    /// <c>SinCambios</c> en los campos clave (con IA). Sin IA toda versión nueva es <c>Distinta</c> y se revisa una a una.
    /// </summary>
    public static bool AprobableEnLote(Comparacion? comparacion) => comparacion is Comparacion.Identica or Comparacion.SinCambios;
}

/// <summary>Programado al abrir la revalidación: al cumplirse el plazo, si sigue abierta, los pendientes bloquean (RV-4).</summary>
public sealed record VencerPlazoDeRevalidacion(Guid TenantId, Guid VinculoId, Guid ExpedienteId);

public static class VencerPlazoDeRevalidacionHandler
{
    /// <summary>Idempotente: si la actualización ya se cerró o el certificado se reemitió o cambió de estado, no hace nada.</summary>
    public static async Task Handle(VencerPlazoDeRevalidacion mensaje, IDocumentStore store, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = store.LightweightSession(mensaje.TenantId.ToString("N"));
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", "plazo-revalidacion");
        if (await sesion.Events.AggregateStreamAsync<Expediente>(mensaje.ExpedienteId, token: ct) is not { EstaAbierto: true, Tipo: TipoExpediente.Actualizacion })
            return;

        var certificado = await sesion.Events.FetchForWriting<Certificado>(Certificado.IdPara(mensaje.VinculoId), ct);
        if (certificado.Aggregate?.VencerPlazoDeRevalidacion(mensaje.ExpedienteId, reloj.GetUtcNow()) is not { } vencido)
            return;
        certificado.AppendOne(vencido);
        await sesion.SaveChangesAsync(ct);
    }
}
