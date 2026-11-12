using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Ia;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Revision;

namespace Validasoft.Aplicacion.Revision;

/// <summary>
/// Configuración del procesador de la revisión automática (<c>Validasoft:RevisionAutomatica</c>): cada cuántos segundos corre
/// (30 por omisión; 0 lo apaga) y cuántos expedientes de una empresa revisa por pasada.
/// </summary>
public sealed class OpcionesDeRevisionAutomatica(IConfiguration configuracion)
{
    private const string Raiz = "Validasoft:RevisionAutomatica";

    public int SegundosProceso { get; } = Math.Clamp(configuracion.GetValue($"{Raiz}:SegundosProceso", 30), 0, 3600);

    public int MaximoPorPasada { get; } = Math.Clamp(configuracion.GetValue($"{Raiz}:MaximoPorPasada", 20), 1, 500);

    public TimeSpan Intervalo => TimeSpan.FromSeconds(Math.Max(1, SegundosProceso));

    public bool Habilitada => SegundosProceso > 0;
}

/// <summary>
/// Revisión automática con IA (ADR-0012, ítems 13 y 14). Por cada empresa con nivel Asistido o Automático busca los expedientes
/// en revisión con documentos sin revisar, arma lo que el motor necesita (el análisis de IA que <b>la empresa</b> recibió, el cruce
/// con la ficha de F5 y el uso del mes) y registra la decisión con el stream bloqueado. Todo es idempotente: lo ya registrado para
/// una versión no se vuelve a decidir, y un análisis que aún no llega se espera (hasta <c>EsperaIaMinutos</c>) sin registrar nada.
/// Las llamadas a OpenAI no pasan por aquí: el análisis ya está en la bóveda.
/// </summary>
public sealed class TareasDeRevisionAutomatica(IDocumentStore store, EmisorDeCertificados certificados, OpcionesDeIa opcionesIa,
    OpcionesDeRevisionAutomatica opciones, TimeProvider reloj, IProtectorDeSecretos protector, IServiceScopeFactory alcances,
    ILogger<TareasDeRevisionAutomatica> registro)
{
    public const string Actor = "revision-automatica";

    /// <summary>Barre las empresas con revisión automática y revisa sus expedientes pendientes.</summary>
    public async Task ProcesarPendientesAsync(CancellationToken ct)
    {
        List<RevisionAutomaticaEmpresa> configuraciones;
        Dictionary<Guid, Guid> tenants;
        await using (var global = store.QuerySession())
        {
            configuraciones = [.. await global.Query<RevisionAutomaticaEmpresa>()
                .Where(c => c.AnyTenant() && c.Nivel != NivelDeRevisionAutomatica.Manual).Take(500).ToListAsync(ct)];
            tenants = configuraciones.Count == 0
                ? []
                : (await global.LoadManyAsync<Empresa>(ct, configuraciones.Select(c => c.Id).Distinct().ToArray())).ToDictionary(e => e.Id, e => e.TenantId);
        }

        foreach (var configuracion in configuraciones.Where(c => tenants.ContainsKey(c.Id)))
        {
            var tenantId = tenants[configuracion.Id];
            List<(Guid Id, string Folio)> pendientes;
            await using (var consulta = store.QuerySession(tenantId.ToString("N")))
            {
                pendientes = [.. (await consulta.Query<Expediente>().Where(e => e.EmpresaId == configuracion.Id && e.PorRevisarAutomaticamente)
                    .OrderBy(e => e.ActualizadoEn).Take(opciones.MaximoPorPasada).ToListAsync(ct)).Select(e => (e.Id, e.Folio))];
            }

            foreach (var (id, folio) in pendientes)
                await IntentarAsync(() => RevisarExpedienteAsync(tenantId, id, ct), folio);
        }
    }

    /// <summary>
    /// Revisa un expediente: devuelve cuántos documentos registró (0 si no había nada que decidir o aún se espera el análisis).
    /// </summary>
    public async Task<int> RevisarExpedienteAsync(Guid tenantId, Guid expedienteId, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var mes = UsoRevisionAutomatica.MesDe(CalculadoraDeVigencia.Fecha(ahora));
        RevisionAutomaticaEmpresa config;
        List<PropuestaDeRevision> propuestas;

        await using (var lectura = store.QuerySession(tenantId.ToString("N")))
        await using (var boveda = store.QuerySession(TenantsTecnicos.Boveda))
        {
            var expediente = await lectura.LoadAsync<Expediente>(expedienteId, ct);
            if (expediente is null || expediente.Estado != EstadoExpediente.EnRevision)
                return 0;
            config = await lectura.LoadAsync<RevisionAutomaticaEmpresa>(expediente.EmpresaId, ct)
                     ?? RevisionAutomaticaEmpresa.PorDefecto(expediente.EmpresaId);
            if (config.Nivel == NivelDeRevisionAutomatica.Manual)
                return 0;
            var candidatas = expediente.CandidatasDeRevisionAutomatica();
            if (candidatas.Count == 0)
                return 0;

            var plan = await lectura.LoadAsync<PlanDeIaEmpresa>(expediente.EmpresaId, ct);
            var usoIa = await lectura.LoadAsync<UsoMensualIa>(UsoMensualIa.Clave(expediente.EmpresaId, UsoMensualIa.MesDe(CalculadoraDeVigencia.Fecha(ahora))), ct)
                        ?? UsoMensualIa.Nuevo(expediente.EmpresaId, UsoMensualIa.MesDe(CalculadoraDeVigencia.Fecha(ahora)));
            var uso = await lectura.LoadAsync<UsoRevisionAutomatica>(UsoRevisionAutomatica.Clave(expediente.EmpresaId, mes), ct);
            var documentoIds = candidatas.Select(c => c.Requisito.VersionActual!.DocumentoId).Distinct().ToArray();
            var documentos = (await boveda.LoadManyAsync<Documento>(ct, documentoIds)).ToDictionary(d => d.Id);
            var empresaId = expediente.EmpresaId;
            var consumidas = (await lectura.Query<ValidacionIaConsumida>().Where(v => v.EmpresaId == empresaId && v.DocumentoId.IsOneOf(documentoIds))
                .ToListAsync(ct)).Select(v => (v.DocumentoId, v.Numero)).ToHashSet();
            var cruces = await CrucesDelExpediente.CargarAsync(lectura, boveda, expediente, empresaId, protector, ct);

            propuestas = [];
            var aprobadasEnLaPasada = 0;
            foreach (var candidata in candidatas)
            {
                var entrada = Entrada(candidata, documentos, consumidas, cruces, config, plan, usoIa, ahora, out var esperando);
                var politica = new PoliticaDeRevision(config.Nivel, config.UmbralEfectivo(plan?.UmbralConfianza ?? PlanDeIaEmpresa.UmbralConfianzaInicial),
                    config.PorcentajeMuestreo, config.TiposAutorizados, (uso?.Aprobadas ?? 0) + aprobadasEnLaPasada, config.TopeMensualAprobaciones);
                // Lo que está fuera del alcance de la revisión automática se anota ya: no hay análisis que esperar.
                if (esperando && MotorDeRevision.FueraDeAlcance(entrada, politica) is null)
                    continue;
                if (MotorDeRevision.Decidir(entrada, politica) is not { } decision)
                    continue;
                if (decision.Aprueba)
                    aprobadasEnLaPasada++;
                propuestas.Add(new PropuestaDeRevision(candidata.Requisito.Id, candidata.Area.AreaId, candidata.Version, decision));
            }
        }

        if (propuestas.Count == 0)
            return 0;
        return await EscribirAsync(tenantId, expedienteId, config.Nivel, propuestas, mes, ahora, ct);
    }

    /// <summary>
    /// Arma la entrada del motor. <paramref name="esperando"/>: el análisis de IA (o el cruce con la ficha) aún puede llegar y no
    /// venció la espera de la empresa: no se decide todavía.
    /// </summary>
    private EntradaDeRevision Entrada(CandidataDeRevision candidata, IReadOnlyDictionary<Guid, Documento> documentos, IReadOnlySet<(Guid, int)> consumidas,
        CrucesDelExpediente cruces, RevisionAutomaticaEmpresa config, PlanDeIaEmpresa? plan, UsoMensualIa usoIa, DateTimeOffset ahora, out bool esperando)
    {
        var requisito = candidata.Requisito;
        var version = requisito.VersionActual!;
        var visible = documentos.TryGetValue(version.DocumentoId, out var documento) && documento.Buscar(version.Numero) is { Ia: not null } &&
                      consumidas.Contains((version.DocumentoId, version.Numero));
        var versionDocumento = documento?.Buscar(version.Numero);
        var ia = visible ? versionDocumento!.Ia : null;
        AnalisisParaRevisar? analisis = ia is null ? null : new AnalisisParaRevisar(ia.Confianza, ia.Legible, ia.Alertas.Count, ia.TipoDetectado, ia.CamposCriticosCambiados.Count);

        // F5: el cruce de lo que la empresa ve de las secciones que respalda el documento.
        var soporte = cruces.DeDocumento(requisito.Id);
        var crucePendiente = analisis is not null && soporte is { SinAnalisis: false } && soporte.Secciones.Any(s => s.Version is not null && s.Cruce is null);
        // Sin cupo (incluidas y adicional agotados) la empresa no consumirá la validación de esta versión: el análisis no le va a llegar, así que no se
        // espera a que venza la espera de la empresa y la versión pasa de una vez a una persona (IA-4, IA-6). Si ya la consumió, el análisis viene en camino.
        var sinCupo = !consumidas.Contains((version.DocumentoId, version.Numero)) && usoIa.CupoDisponible(plan) is null;
        var iaPuedeLlegar = analisis is null && opcionesIa.Habilitada && plan is { Activo: true } && !sinCupo && versionDocumento is not null && versionDocumento.FalloIa is null;

        var inicio = candidata.Area.PendienteDesde is { } pendienteDesde && pendienteDesde > version.CargadaEn ? pendienteDesde : version.CargadaEn;
        var vencida = ahora >= inicio.AddMinutes(config.EsperaIaMinutos);
        esperando = (iaPuedeLlegar || crucePendiente) && !vencida;

        return new EntradaDeRevision(version.DocumentoId, version.Numero, requisito.TipoCanonico, requisito.Reenvios,
            config.AreasAutorizadas.Contains(candidata.Area.AreaId), requisito.AprobadoresPara(candidata.Area) > 1, analisis, crucePendiente,
            soporte?.DiscrepanciaCritica ?? false, soporte?.Secciones.Any(s => s.Cruce is { Discrepancias: > 0 }) ?? false);
    }

    /// <summary>Registra las decisiones con el stream bloqueado y cuenta el uso del mes en la misma transacción.</summary>
    private async Task<int> EscribirAsync(Guid tenantId, Guid expedienteId, NivelDeRevisionAutomatica nivel, IReadOnlyList<PropuestaDeRevision> propuestas,
        string mes, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = store.LightweightSession(tenantId.ToString("N"));
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", Actor);
        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(expedienteId, ct);
        if (stream.Aggregate is not { } expediente)
            return 0;
        var eventos = expediente.RegistrarRevisionesAutomaticas(propuestas, nivel, ahora);
        var registradas = eventos.OfType<RevisionAutomaticaRegistrada>().ToList();
        if (registradas.Count == 0)
            return 0;

        // Un cierre por aprobación automática también le llega al tercero por correo (CU-19).
        var correos = new CorreosPendientesDeCierre(protector);
        await ProcesoDeValidacion.AnexarAsync(sesion, stream, eventos, correos.Correo, certificados, ahora, ct);
        var clave = UsoRevisionAutomatica.Clave(expediente.EmpresaId, mes);
        var uso = await sesion.LoadAsync<UsoRevisionAutomatica>(clave, ct) ?? UsoRevisionAutomatica.Nuevo(expediente.EmpresaId, mes);
        foreach (var registrada in registradas)
            uso.Registrar(registrada.Resultado);
        sesion.Store(uso);
        await sesion.SaveChangesAsync(ct);
        await correos.PublicarAsync(alcances);
        return registradas.Count;
    }

    private async Task IntentarAsync(Func<Task> accion, string folio)
    {
        try
        {
            await accion();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            registro.LogWarning(e, "No se pudo revisar automáticamente {Folio}; se reintentará.", folio);
        }
    }
}
