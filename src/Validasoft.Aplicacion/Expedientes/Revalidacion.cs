using Marten;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
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
    /// <summary>
    /// Días por omisión para resolver la revalidación antes de que los pendientes del certificado bloqueen (RV-4, A-2). Cada tipo
    /// de documento (<c>diasRevalidacion</c>) y cada requisito de sección del flujo pueden fijar el suyo: rige el menor de los
    /// requisitos de la actualización (<see cref="Expediente.DiasDePlazoDeRevalidacion"/>).
    /// </summary>
    public const int DiasDePlazo = Expediente.DiasDePlazoDeRevalidacionPorOmision;

    /// <summary>
    /// Abre la actualización si hace falta (idempotente: con un expediente abierto o sin cambios no hace nada; con una actualización
    /// ya abierta, <see cref="AmpliarAsync"/> le suma lo que cambió después). Escribe en <paramref name="sesion"/> sin guardar.
    /// Devuelve el expediente abierto, o nulo.
    /// </summary>
    public static async Task<Expediente?> AbrirSiCorrespondeAsync(IDocumentSession sesion, Guid vinculoId, ActorDeExpediente actor,
        EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct, IMessageBus? mensajes = null)
    {
        var vinculoStream = await sesion.Events.FetchForWriting<Vinculo>(vinculoId, ct);
        if (vinculoStream.Aggregate is not { } vinculo || vinculo.ExpedienteAbiertoId is not null || !vinculo.TuvoAprobacion ||
            vinculo.Estado is not (EstadoVinculo.Activo or EstadoVinculo.PorVencer or EstadoVinculo.Vencido))
            return null;

        var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct);
        if (flujo is null)
            return null;

        var contexto = await RequisitosDeExpediente.ContextoAsync(sesion, vinculo, ct);
        var cambios = await CambiosAsync(sesion, vinculo, flujo.Version, contexto, ahora, ct);
        if (cambios.Vacio)
            return null;

        var expediente = await ProcesoDeValidacion.AbrirAsync(sesion, vinculoStream, flujo, null, certificados, ahora, ct,
            tipoExpediente: TipoExpediente.Actualizacion, soloRequisitos: cambios.Ids, actor: actor, retiros: cambios.Retiros);

        var certificado = await sesion.Events.FetchForWriting<Certificado>(Certificado.IdPara(vinculoId), ct);
        if (expediente.EstaAbierto && certificado.Aggregate?.MarcarConPendientes(MotivoDePendientes(cambios.Nombres), expediente.Id, ahora) is { } pendientes)
            certificado.AppendOne(pendientes);
        if (expediente.EstaAbierto && mensajes is not null && Guid.TryParse(sesion.TenantId, out var tenantId))
            await mensajes.ScheduleAsync(new VencerPlazoDeRevalidacion(tenantId, vinculoId, expediente.Id), expediente.PlazoDeRevalidacionHasta);
        return expediente;
    }

    /// <summary>
    /// Amplía la actualización abierta del vínculo con lo que cambió después de abrirse: una versión nueva de una sección que la
    /// empresa tenía aceptada o de un documento que tenía aprobado y que el expediente no incluye entra a él con la versión que
    /// lo cubre, calculada igual que al abrir (<see cref="CambiosAsync"/>), y sus áreas vuelven a quedar pendientes
    /// (<see cref="Expediente.AgregarRequisitos"/>). Solo para <c>Actualizacion</c>: en un expediente inicial o de renovación el
    /// requisito ya está o lo pone al día la sincronización. Usa la versión del flujo con que se abrió el expediente. Idempotente:
    /// sin requisitos nuevos devuelve nulo. Los eventos se calculan sobre <paramref name="expediente"/> (con la sincronización ya
    /// aplicada); quien los llama los anexa y, con <see cref="Ampliacion.Motivo"/>, actualiza el certificado.
    /// </summary>
    public static async Task<Ampliacion?> AmpliarAsync(IDocumentSession sesion, Expediente expediente, DateTimeOffset ahora, CancellationToken ct)
    {
        if (expediente is not { Tipo: TipoExpediente.Actualizacion, EstaAbierto: true, Flujo: { } delExpediente } ||
            await sesion.LoadAsync<Vinculo>(expediente.VinculoId, ct) is not { InvitacionAceptada: true } vinculo ||
            await sesion.LoadAsync<Flujo>(delExpediente.FlujoId, ct) is not { } flujo ||
            flujo.Versiones.FirstOrDefault(v => v.Id == delExpediente.FlujoVersionId) is not { } version ||
            await sesion.LoadAsync<Tercero>(vinculo.TerceroId, ct) is not { } tercero ||
            await sesion.LoadAsync<Empresa>(vinculo.EmpresaId, ct) is not { } empresa)
            return null;

        var contexto = await RequisitosDeExpediente.ContextoAsync(sesion, vinculo, ct);
        var yaIncluidos = expediente.Requisitos.Select(r => r.Id).ToHashSet();
        var cambios = await CambiosAsync(sesion, vinculo, version, contexto, ahora, ct, yaIncluidos);
        if (cambios.Vacio)
            return null;

        var calculados = await ProcesoDeValidacion.CalcularRequisitosAsync(sesion, vinculo, tercero, empresa.TenantId, version, contexto, cambios.Ids, null,
            ahora, ct, cambios.Retiros);
        var eventos = expediente.AgregarRequisitos(calculados, ahora);
        if (eventos.Count == 0)
            return null;

        var despues = Expediente.Reproducir(expediente, eventos);
        return new Ampliacion(eventos, MotivoDePendientes([.. despues.Requisitos.Select(r => r.Nombre)]));
    }

    /// <summary>Lo que sumó la ampliación: sus eventos y el motivo de pendientes con todo lo que ahora revalida la actualización.</summary>
    public sealed record Ampliacion(IReadOnlyList<object> Eventos, string Motivo);

    /// <summary>
    /// Qué cambió para el vínculo desde lo que la empresa aprobó o aceptó, sin contar lo de <paramref name="yaIncluidos"/> (los
    /// requisitos que el expediente abierto ya tiene). Es la detección común de abrir la actualización y de ampliarla. Los
    /// documentos se comparan con lo que aprobó el último expediente (la versión que hoy los cubre es otra); las secciones, con lo
    /// aceptado (<see cref="SeccionAceptada"/>), que también puede venir de la importación del Portal sin expediente en Validasoft
    /// (F4, FM-42).
    /// </summary>
    public static async Task<RequisitosCambiados> CambiosAsync(IDocumentSession sesion, Vinculo vinculo, FlujoVersion version,
        ContextoDeExigencia contexto, DateTimeOffset ahora, CancellationToken ct, IReadOnlySet<Guid>? yaIncluidos = null)
    {
        var vinculoId = vinculo.Id;
        var aprobado = (await sesion.Query<Expediente>().Where(e => e.VinculoId == vinculoId && e.Estado == EstadoExpediente.Aprobado).ToListAsync(ct))
            .OrderByDescending(e => e.ActualizadoEn)
            .FirstOrDefault();

        List<RequisitoPedido> documentos = [];
        Dictionary<Guid, RetiroDeVersion> retiros = [];
        if (aprobado is not null)
        {
            var pedidos = await RequisitosDeExpediente.DelFlujoAsync(sesion, version, contexto, ct);
            var terceroId = vinculo.TerceroId;
            var delTercero = await sesion.ForTenant(TenantsTecnicos.Boveda).Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
            documentos = pedidos
                .Where(p => yaIncluidos?.Contains(p.Requisito.Id) != true &&
                            RequisitosDeExpediente.Cubre(p.Tipo.Id, p.Tipo.TipoCanonico, p.Tipo.Ambito, delTercero, vinculoId) is { } actual &&
                            AprobadoAntes(aprobado, p) is { } previa &&
                            (previa.DocumentoId != actual.DocumentoId || previa.Numero != actual.Numero))
                .ToList();

            // Si la que la empresa aprobó la retiró el tercero, la revalidación lleva el motivo (RT-3).
            foreach (var pedido in documentos)
            {
                if (AprobadoAntes(aprobado, pedido) is { } previa &&
                    delTercero.FirstOrDefault(d => d.Id == previa.DocumentoId)?.Buscar(previa.Numero) is { Estado: EstadoVersion.Retirada } retirada)
                    retiros[pedido.Requisito.Id] = RetiroDe(retirada, ahora);
            }
        }

        var secciones = await SeccionesCambiadasAsync(sesion, vinculo, version, contexto, ahora, ct, yaIncluidos);
        return new RequisitosCambiados(documentos, secciones, retiros);
    }

    /// <summary>El retiro de una versión de la bóveda como lo guarda el expediente (la fecha ausente de un retiro antiguo se reemplaza por <paramref name="ahora"/>).</summary>
    public static RetiroDeVersion RetiroDe(VersionDocumento version, DateTimeOffset ahora) =>
        new(version.Numero, version.MotivoRetiro ?? string.Empty, version.RetiradaEn ?? ahora);

    /// <summary>Las versiones retiradas de los documentos, por (documento, número): lo que el expediente necesita para explicar un retroceso.</summary>
    public static IReadOnlyDictionary<(Guid DocumentoId, int Numero), RetiroDeVersion> RetiradasDe(IEnumerable<Documento> documentos, DateTimeOffset ahora) =>
        documentos.SelectMany(d => d.Versiones.Where(v => v.Estado == EstadoVersion.Retirada).Select(v => (Clave: (d.Id, v.Numero), Retiro: RetiroDe(v, ahora))))
            .ToDictionary(x => x.Clave, x => x.Retiro);

    /// <summary>
    /// Secciones que el flujo pide al vínculo, que la empresa ya tenía aceptadas (<see cref="SeccionAceptada"/>) y cuya versión
    /// vigente, completa para la empresa, es otra: entran a la actualización. Una versión nueva incompleta (el tercero aún la
    /// diligencia) no abre nada.
    /// </summary>
    private static async Task<IReadOnlyList<RequisitoDeSeccionPedido>> SeccionesCambiadasAsync(IDocumentSession sesion, Vinculo vinculo,
        FlujoVersion version, ContextoDeExigencia contexto, DateTimeOffset ahora, CancellationToken ct, IReadOnlySet<Guid>? yaIncluidos)
    {
        var pedidas = RequisitosDeExpediente.SeccionesDelFlujo(version, contexto, CalculadoraDeVigencia.Fecha(ahora))
            .Where(p => yaIncluidos?.Contains(p.RequisitoId) != true)
            .ToList();
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

    /// <summary>Hasta cuándo puede resolverse la revalidación antes de que sus pendientes bloqueen (RV-4): el menor plazo de sus requisitos.</summary>
    public static DateTimeOffset PlazoHasta(Expediente actualizacion) => actualizacion.PlazoDeRevalidacionHasta;

    /// <summary>
    /// ¿Se puede aprobar en lote (A-18)? Solo si la bóveda no ve cambios frente a la anterior: <c>Identica</c> por hash o
    /// <c>SinCambios</c> en los campos clave (con IA). Sin IA toda versión nueva es <c>Distinta</c> y se revisa una a una.
    /// </summary>
    public static bool AprobableEnLote(Comparacion? comparacion) => comparacion is Comparacion.Identica or Comparacion.SinCambios;
}

/// <summary>Lo que cambió para un vínculo desde lo que la empresa aprobó o aceptó: documentos y secciones que entran a la actualización.</summary>
public sealed record RequisitosCambiados(IReadOnlyList<RequisitoPedido> Documentos, IReadOnlyList<RequisitoDeSeccionPedido> Secciones,
    IReadOnlyDictionary<Guid, RetiroDeVersion>? Retiros = null)
{
    public bool Vacio => Documentos.Count == 0 && Secciones.Count == 0;

    /// <summary>Ids de requisito del flujo (los de las secciones por periodo, los de su expansión).</summary>
    public IReadOnlySet<Guid> Ids => new HashSet<Guid>([.. Documentos.Select(p => p.Requisito.Id), .. Secciones.Select(s => s.RequisitoId)]);

    public IReadOnlyList<string> Nombres => [.. Documentos.Select(p => p.Tipo.Nombre), .. Secciones.Select(s => s.Nombre)];
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
