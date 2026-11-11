using System.Collections.Concurrent;
using System.Globalization;
using Marten;
using Marten.Patching;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Habeas;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Plataforma;

/// <summary>Revisa los planes de todos los grupos que tienen límites (cola durable <c>plan</c>, OD-16).</summary>
public sealed record EvaluarPlanesDeGrupos(Guid? TenantId = null);

/// <summary>Mide un grupo frente a su plan y avisa al 80 % y al 100 % de cada límite (una vez por umbral).</summary>
public sealed record EvaluarPlanDelGrupo(Guid TenantId);

/// <summary>
/// Cuenta las llamadas a la API de cada grupo (OD-15): cada petición con una API key de producción suma en memoria y, cada pocos
/// segundos (<c>Validasoft:Plan:SegundosDeVolcado</c>, 5 por omisión), cada nodo vuelca lo acumulado al contador mensual del grupo con
/// un incremento atómico. El mes es el calendario del negocio.
/// </summary>
public sealed class MedidorDeLlamadasApi(IDocumentStore store, TimeProvider reloj, IConfiguration configuracion, ILogger<MedidorDeLlamadasApi> logger) : BackgroundService
{
    private readonly ConcurrentDictionary<(Guid Tenant, string Mes), long> _pendientes = new();
    private readonly ConcurrentDictionary<string, bool> _existentes = new();
    private readonly ConcurrentDictionary<(Guid Tenant, string Mes), long> _volcadas = new();

    public static string MesActual(TimeProvider reloj) => UsoMensualDelGrupo.MesDe(CalculadoraDeVigencia.Fecha(reloj.GetUtcNow()));

    public void Registrar(Guid tenantId) => _pendientes.AddOrUpdate((tenantId, MesActual(reloj)), 1, (_, n) => n + 1);

    /// <summary>Lo que este nodo todavía no volcó a la base.</summary>
    public long Pendientes(Guid tenantId, string mes) => _pendientes.TryGetValue((tenantId, mes), out var n) ? Interlocked.Read(ref n) : 0;

    /// <summary>Lo que este nodo ya volcó a la base desde que arrancó (para no perder cuentas al leer el contador guardado).</summary>
    public long Volcadas(Guid tenantId, string mes) => _volcadas.TryGetValue((tenantId, mes), out var n) ? Interlocked.Read(ref n) : 0;

    /// <summary>Vuelca lo acumulado a la base (también al apagar la API). Lo que no se pueda guardar se conserva para el siguiente intento.</summary>
    public async Task VolcarAsync(CancellationToken ct)
    {
        foreach (var clave in _pendientes.Keys.ToList())
        {
            if (!_pendientes.TryRemove(clave, out var cuantas) || cuantas == 0)
                continue;
            try
            {
                await SumarAsync(clave.Tenant, clave.Mes, cuantas, ct);
                _volcadas.AddOrUpdate(clave, cuantas, (_, n) => n + cuantas);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                _pendientes.AddOrUpdate(clave, cuantas, (_, n) => n + cuantas);
                logger.LogWarning(e, "No se pudo volcar el conteo de llamadas del grupo {Grupo}; se reintenta.", clave.Tenant);
            }
        }
    }

    private async Task SumarAsync(Guid tenantId, string mes, long cuantas, CancellationToken ct)
    {
        var id = UsoMensualDelGrupo.Clave(tenantId, mes);
        if (!_existentes.ContainsKey(id))
        {
            try
            {
                await using var nueva = store.LightweightSession();
                nueva.Insert(new UsoMensualDelGrupo { Id = id, TenantId = tenantId, Mes = mes, LlamadasApi = cuantas });
                await nueva.SaveChangesAsync(ct);
                _existentes[id] = true;
                return;
            }
            catch (Exception e) when (Peticiones.EsCarrera(e))
            {
                _existentes[id] = true; // ya existía: se suma
            }
        }

        await using var sesion = store.LightweightSession();
        sesion.Patch<UsoMensualDelGrupo>(id).Increment(x => x.LlamadasApi, cuantas);
        await sesion.SaveChangesAsync(ct);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var segundos = Math.Clamp(configuracion.GetValue("Validasoft:Plan:SegundosDeVolcado", 5), 1, 300);
        using var temporizador = new PeriodicTimer(TimeSpan.FromSeconds(segundos));
        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
                await VolcarAsync(stoppingToken);
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la API: lo pendiente se vuelca en StopAsync.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        try
        {
            await VolcarAsync(CancellationToken.None);
        }
        catch (Exception e)
        {
            logger.LogWarning(e, "No se pudo volcar el conteo de llamadas al apagar.");
        }
    }
}

/// <summary>
/// Lo que el plan del grupo permite y cuánto lleva usado (OD-14…OD-17): mide usuarios, empresas, vínculos activos, almacenamiento y
/// llamadas del mes, y con el <b>bloqueo suave</b> activo impide las altas de lo que llegó a su límite. Singleton: cachea el plan 20 s
/// (el cambio vale ya en este nodo, <see cref="Olvidar"/>, y en los demás en 20 s).
/// </summary>
public sealed class ControlDePlan(IDocumentStore store, IMemoryCache cache, MedidorDeLlamadasApi llamadas, TimeProvider reloj, IConfiguration configuracion)
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(20);
    private sealed record PlanEnCache(PlanDelGrupo? Plan);

    private static string Llave(Guid tenantId) => "plan-del-grupo:" + tenantId.ToString("N");

    public async Task<PlanDelGrupo?> PlanAsync(Guid tenantId, CancellationToken ct)
    {
        if (cache.TryGetValue(Llave(tenantId), out PlanEnCache? guardado) && guardado is not null)
            return guardado.Plan;
        await using var consulta = store.QuerySession();
        var plan = await consulta.LoadAsync<PlanDelGrupo>(tenantId, ct);
        cache.Set(Llave(tenantId), new PlanEnCache(plan), Vigencia);
        return plan;
    }

    public void Olvidar(Guid tenantId) => cache.Remove(Llave(tenantId));

    private int MinutosDeMedicionDelAlmacenamiento => Math.Clamp(configuracion.GetValue("Validasoft:Plan:MinutosMedicionAlmacenamiento", 5), 0, 1440);

    /// <summary>La medición de una métrica. El almacenamiento sale de la última medida del plan (<paramref name="refrescar"/> la vuelve a medir).</summary>
    public async Task<MedicionDeMetrica> MedirAsync(Guid tenantId, MetricaDePlan metrica, PlanDelGrupo? plan, string mes, bool refrescar, CancellationToken ct)
    {
        var limite = plan?.Limites.De(metrica);
        long usado = metrica switch
        {
            MetricaDePlan.Usuarios => await ContarUsuariosAsync(tenantId, ct),
            MetricaDePlan.Empresas => await ContarEmpresasAsync(tenantId, ct),
            MetricaDePlan.VinculosActivos => await ContarVinculosAsync(tenantId, ct),
            MetricaDePlan.Almacenamiento => await AlmacenamientoAsync(tenantId, plan, refrescar, ct),
            _ => await LlamadasDelMesAsync(tenantId, mes, ct)
        };
        return new MedicionDeMetrica(metrica, usado, limite);
    }

    public async Task<IReadOnlyList<MedicionDeMetrica>> MedirTodoAsync(Guid tenantId, PlanDelGrupo? plan, string mes, bool refrescarAlmacenamiento, CancellationToken ct)
    {
        var mediciones = new List<MedicionDeMetrica>();
        foreach (var metrica in Enum.GetValues<MetricaDePlan>())
            mediciones.Add(await MedirAsync(tenantId, metrica, plan, mes, refrescarAlmacenamiento, ct));
        return mediciones;
    }

    /// <summary>
    /// Con el bloqueo suave activo, impide dar de alta <paramref name="cuantos"/> más de la métrica si pasaría el límite: 409
    /// <c>limite-del-plan-alcanzado</c>. Sin plan, sin límite o sin bloqueo suave no hace nada.
    /// </summary>
    public async Task ExigirAsync(Guid tenantId, MetricaDePlan metrica, long cuantos, CancellationToken ct)
    {
        if (await PlanAsync(tenantId, ct) is not { BloqueoSuave: true } plan || plan.Limites.De(metrica) is null)
            return;
        var medicion = await MedirAsync(tenantId, metrica, plan, MedidorDeLlamadasApi.MesActual(reloj), refrescar: false, ct);
        if (medicion.Cabe(cuantos))
            return;
        throw new ReglaDeNegocioException(TipoFalla.Conflicto, "limite-del-plan-alcanzado", MensajeDeLimite(plan, medicion))
        {
            Datos = new Dictionary<string, object?> { ["metrica"] = metrica.ToString(), ["usado"] = medicion.Usado, ["limite"] = medicion.Limite }
        };
    }

    /// <summary>
    /// Cuenta una llamada a la API con una API key del grupo (OD-15). Con el bloqueo suave activo y el tope del mes alcanzado, las
    /// <b>escrituras</b> responden 429 <c>limite-del-plan-alcanzado</c> (y no se cuentan); las lecturas siguen funcionando.
    /// </summary>
    public async Task RegistrarLlamadaAsync(Guid tenantId, bool esEscritura, CancellationToken ct)
    {
        if (esEscritura && await PlanAsync(tenantId, ct) is { BloqueoSuave: true, Limites.LlamadasApiMes: { } limite } plan)
        {
            var usadas = await LlamadasDelMesAsync(tenantId, MedidorDeLlamadasApi.MesActual(reloj), ct);
            if (usadas >= limite)
                throw new ReglaDeNegocioException(TipoFalla.DemasiadosIntentos, "limite-del-plan-alcanzado",
                    MensajeDeLimite(plan, new MedicionDeMetrica(MetricaDePlan.LlamadasApi, usadas, limite)))
                {
                    Datos = new Dictionary<string, object?> { ["metrica"] = nameof(MetricaDePlan.LlamadasApi), ["usado"] = usadas, ["limite"] = limite }
                };
        }

        llamadas.Registrar(tenantId);
    }

    public static string MensajeDeLimite(PlanDelGrupo plan, MedicionDeMetrica m) => m.Metrica switch
    {
        MetricaDePlan.Usuarios => $"El plan {plan.Codigo} permite {m.Limite} usuarios y el grupo ya los tiene. Pide a Plataforma ampliar el plan.",
        MetricaDePlan.Empresas => $"El plan {plan.Codigo} permite {m.Limite} empresas y el grupo ya las tiene. Pide a Plataforma ampliar el plan.",
        MetricaDePlan.VinculosActivos => $"El plan {plan.Codigo} permite {m.Limite} vínculos activos y el grupo ya los tiene. Pide a Plataforma ampliar el plan.",
        MetricaDePlan.Almacenamiento => $"El plan {plan.Codigo} permite {TextoDeTamano.Mb(m.Limite)} de documentos y el grupo ya los alcanzó. Pide a Plataforma ampliar el plan.",
        _ => $"El plan {plan.Codigo} permite {m.Limite:N0} llamadas a la API al mes y el grupo ya las usó; las lecturas siguen funcionando. Pide a Plataforma ampliar el plan."
    };

    private async Task<long> ContarUsuariosAsync(Guid tenantId, CancellationToken ct)
    {
        await using var consulta = store.QuerySession(tenantId.ToString("N"));
        return await consulta.Query<Usuario>().CountAsync(u => u.Activo, ct);
    }

    private async Task<long> ContarEmpresasAsync(Guid tenantId, CancellationToken ct)
    {
        await using var consulta = store.QuerySession();
        return await consulta.Query<Empresa>().CountAsync(e => e.TenantId == tenantId && e.Activa, ct);
    }

    private async Task<long> ContarVinculosAsync(Guid tenantId, CancellationToken ct)
    {
        await using var consulta = store.QuerySession(tenantId.ToString("N"));
        return await consulta.Query<Vinculo>().CountAsync(v => v.Estado != EstadoVinculo.Anulado && v.Estado != EstadoVinculo.Desvinculado, ct);
    }

    private async Task<long> LlamadasDelMesAsync(Guid tenantId, string mes, CancellationToken ct)
    {
        // Lo guardado se lee como mucho cada 20 s; mientras tanto se suma lo que este nodo ya volcó después de leerlo y lo que aún no volcó.
        var clave = $"llamadas-del-mes:{tenantId:N}:{mes}";
        if (!cache.TryGetValue(clave, out (long Persistidas, long VolcadasAlLeer) leido))
        {
            var volcadasAntes = llamadas.Volcadas(tenantId, mes);
            await using var consulta = store.QuerySession();
            leido = ((await consulta.LoadAsync<UsoMensualDelGrupo>(UsoMensualDelGrupo.Clave(tenantId, mes), ct))?.LlamadasApi ?? 0, volcadasAntes);
            cache.Set(clave, leido, Vigencia);
        }

        return leido.Persistidas + (llamadas.Volcadas(tenantId, mes) - leido.VolcadasAlLeer) + llamadas.Pendientes(tenantId, mes);
    }

    private async Task<long> AlmacenamientoAsync(Guid tenantId, PlanDelGrupo? plan, bool refrescar, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        if (plan is { AlmacenamientoMedidoEn: { } medido } && !refrescar && ahora - medido < TimeSpan.FromMinutes(MinutosDeMedicionDelAlmacenamiento))
            return plan.AlmacenamientoBytes;

        var bytes = await MedirAlmacenamientoAsync(tenantId, ct);
        if (plan is not null)
        {
            // El plan en caché es una copia: se actualiza la copia y el documento (sin tocar el resto de sus campos).
            plan.AlmacenamientoBytes = bytes;
            plan.AlmacenamientoMedidoEn = ahora;
            await using var sesion = store.LightweightSession();
            sesion.Patch<PlanDelGrupo>(plan.Id).Set(x => x.AlmacenamientoBytes, bytes);
            sesion.Patch<PlanDelGrupo>(plan.Id).Set(x => x.AlmacenamientoMedidoEn, ahora);
            await sesion.SaveChangesAsync(ct);
        }

        return bytes;
    }

    /// <summary>
    /// Bytes de los documentos que las empresas del grupo reciben o cargan: cada objeto del almacenamiento (por su clave) se cuenta una
    /// vez por grupo, aunque varias versiones apunten a él. No cuenta las versiones suprimidas ni las que no tienen archivo.
    /// </summary>
    public async Task<long> MedirAlmacenamientoAsync(Guid tenantId, CancellationToken ct)
    {
        await using var boveda = store.QuerySession(TenantsTecnicos.Boveda);
        var documentos = await boveda.Query<Documento>().Where(d => d.Comparticiones.Any(c => c.TenantId == tenantId)).ToListAsync(ct);
        var vistos = new HashSet<(string, string)>();
        long total = 0;
        foreach (var documento in documentos)
        {
            foreach (var version in documento.Versiones.Where(v => v.TieneArchivo && !v.Suprimida &&
                                                                  documento.Comparticiones.Any(c => c.TenantId == tenantId && c.Numero == v.Numero)))
            {
                if (vistos.Add((version.Bucket, version.ClaveObjeto)))
                    total += version.Tamano;
            }
        }

        return total;
    }
}

/// <summary>Textos de tamaño para los avisos.</summary>
public static class TextoDeTamano
{
    public static string Mb(long? bytes) => bytes is null
        ? "sin límite"
        : bytes.Value >= 1024L * 1024 * 1024
            ? $"{(bytes.Value / (1024d * 1024 * 1024)).ToString("0.#", CultureInfo.GetCultureInfo("es-CO"))} GB"
            : $"{(bytes.Value / (1024d * 1024)).ToString("0.#", CultureInfo.GetCultureInfo("es-CO"))} MB";
}

/// <summary>
/// Mide un grupo frente a su plan y, al llegar al 80 % o al 100 % de un límite, avisa a sus administradores (campana y correo,
/// OD-16). Cada umbral se avisa una sola vez por periodo (las llamadas, por mes); si la métrica baja y vuelve a subir, se avisa otra vez.
/// </summary>
public sealed class EvaluadorDePlan(ISesiones sesiones, ControlDePlan control, TimeProvider reloj)
{
    public static string TituloDeAviso(MedicionDeMetrica m, int umbral) => $"{Nombre(m.Metrica)}: {(umbral >= 100 ? "límite del plan alcanzado" : "80 % del límite del plan")}";

    public static string Nombre(MetricaDePlan metrica) => metrica switch
    {
        MetricaDePlan.Usuarios => "Usuarios",
        MetricaDePlan.Empresas => "Empresas",
        MetricaDePlan.VinculosActivos => "Vínculos activos",
        MetricaDePlan.Almacenamiento => "Almacenamiento",
        _ => "Llamadas a la API"
    };

    public static string Cuanto(MedicionDeMetrica m, long valor) => m.Metrica == MetricaDePlan.Almacenamiento ? TextoDeTamano.Mb(valor) : valor.ToString("N0", CultureInfo.GetCultureInfo("es-CO"));

    public async Task<int> EvaluarAsync(Guid tenantId, IMessageBus? mensajes, CancellationToken ct)
    {
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            // Siempre el plan guardado (no el de la caché): aquí se anotan los avisos que ya salieron.
            var plan = await consulta.LoadAsync<PlanDelGrupo>(tenantId, ct);
            if (plan is not { Limites.Tiene: true })
                return 0;

            var ahora = reloj.GetUtcNow();
            var mes = MedidorDeLlamadasApi.MesActual(reloj);
            var mediciones = await control.MedirTodoAsync(tenantId, plan, mes, refrescarAlmacenamiento: true, ct);
            var antes = string.Join('|', plan.AvisosEnviados);
            var pendientes = plan.AvisosPendientes(mediciones.Where(m => m.Limite is not null), mes);
            if (pendientes.Count == 0 && antes == string.Join('|', plan.AvisosEnviados))
                return 0;

            Guid? primeraEmpresa = (await consulta.Query<Empresa>().Where(e => e.TenantId == tenantId && e.Activa).OrderBy(e => e.Nombre).FirstOrDefaultAsync(ct))?.Id;
            foreach (var (medicion, umbral) in pendientes)
            {
                var mensaje = $"{Nombre(medicion.Metrica)}: {Cuanto(medicion, medicion.Usado)} de {Cuanto(medicion, medicion.Limite ?? 0)} del plan {plan.Codigo} ({medicion.Porcentaje} %). " +
                              (umbral >= 100
                                  ? plan.BloqueoSuave ? "Se bloquean las altas de esto hasta que Plataforma amplíe el plan; lo que ya existe sigue funcionando." : "El servicio sigue funcionando: Plataforma puede ampliar el plan."
                                  : "Cuando llegue al límite " + (plan.BloqueoSuave ? "se bloquearán las altas de esto." : "te avisaremos de nuevo."));
                await AvisosAAdministradores.AvisarAsync(sesiones, mensajes, tenantId, new DestinatariosDeAviso(null), TiposNotificacion.PlanLimite,
                    TituloDeAviso(medicion, umbral), mensaje, primeraEmpresa is { } e ? $"/e/{e}/plan-grupo" : null,
                    $"plan:{medicion.Metrica}:{umbral}:{(medicion.Metrica == MetricaDePlan.LlamadasApi ? mes : "-")}:{ahora.UtcTicks}", null, ahora, ct);
            }

            await using var sesion = sesiones.Global();
            sesion.Store(plan);
            await sesion.SaveChangesAsync(ct);
            control.Olvidar(tenantId);
            return pendientes.Count;
        }
    }
}

public static class EvaluarPlanesDeGruposHandler
{
    public static async Task Handle(EvaluarPlanesDeGrupos mensaje, ISesiones sesiones, IMessageContext mensajes, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var grupos = mensaje.TenantId is { } uno
            ? [uno]
            : (IReadOnlyList<Guid>)[.. await consulta.Query<PlanDelGrupo>().Select(p => p.Id).ToListAsync(ct)];
        foreach (var tenantId in grupos)
            await mensajes.PublishAsync(new EvaluarPlanDelGrupo(tenantId));
    }
}

public static class EvaluarPlanDelGrupoHandler
{
    public static Task Handle(EvaluarPlanDelGrupo mensaje, EvaluadorDePlan evaluador, IMessageContext mensajes, CancellationToken ct) =>
        evaluador.EvaluarAsync(mensaje.TenantId, mensajes, ct);
}

/// <summary>
/// Dispara la revisión de los planes cada <c>Validasoft:Plan:Intervalo</c> (por omisión, 15 minutos; <c>00:00:00</c> la apaga): mide a
/// cada grupo con límites y avisa los que llegaron al 80 % o al 100 %.
/// </summary>
public sealed class ProgramadorDePlanes(IServiceProvider servicios, IConfiguration configuracion, ILogger<ProgramadorDePlanes> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        var intervalo = configuracion.GetValue("Validasoft:Plan:Intervalo", TimeSpan.FromMinutes(15));
        if (intervalo <= TimeSpan.Zero)
            return;

        try
        {
            await Task.Delay(TimeSpan.FromMinutes(1) < intervalo ? TimeSpan.FromMinutes(1) : intervalo, stoppingToken);
            using var temporizador = new PeriodicTimer(intervalo);
            do
            {
                try
                {
                    await using var alcance = servicios.CreateAsyncScope();
                    await alcance.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new EvaluarPlanesDeGrupos());
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "No se pudo pedir la revisión de los planes.");
                }
            } while (await temporizador.WaitForNextTickAsync(stoppingToken));
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la API.
        }
    }
}

/// <summary>
/// Plan y límites de cada grupo (OD-14…OD-17): Plataforma los fija; el grupo ve su plan y su uso. Sin plan configurado el grupo no
/// tiene límites.
/// </summary>
public static class PlanDelGrupoEndpoints
{
    public const string Entidad = "PlanDelGrupo";

    public static PlanDelGrupoDto ADto(PlanDelGrupo? plan, Guid tenantId) => plan is null
        ? new PlanDelGrupoDto(tenantId, PlanDelGrupo.CodigoPorDefecto, new LimitesDePlanDto(null, null, null, null, null), false, false, null)
        : new PlanDelGrupoDto(tenantId, plan.Codigo, new LimitesDePlanDto(plan.Limites.Usuarios, plan.Limites.Empresas, plan.Limites.VinculosActivos,
            plan.Limites.AlmacenamientoMb, plan.Limites.LlamadasApiMes), plan.BloqueoSuave, true, plan.ActualizadoEn);

    public static async Task<UsoDelGrupoDto> UsoAsync(Guid tenantId, string? mes, ISesiones sesiones, ControlDePlan control, TimeProvider reloj, CancellationToken ct)
    {
        string periodo;
        if (string.IsNullOrWhiteSpace(mes))
            periodo = MedidorDeLlamadasApi.MesActual(reloj);
        else if (System.Text.RegularExpressions.Regex.IsMatch(mes.Trim(), @"^\d{4}-(0[1-9]|1[0-2])$"))
            periodo = mes.Trim();
        else
            throw ReglaDeNegocioException.Validacion("mes-invalido", "El mes debe tener la forma AAAA-MM (por ejemplo 2026-11).");

        await using var consulta = sesiones.ConsultaGlobal();
        var plan = await consulta.LoadAsync<PlanDelGrupo>(tenantId, ct);
        var mediciones = new List<MedicionDeMetrica>();
        foreach (var metrica in Enum.GetValues<MetricaDePlan>())
        {
            if (metrica == MetricaDePlan.LlamadasApi && periodo != MedidorDeLlamadasApi.MesActual(reloj))
            {
                var pasado = (await consulta.LoadAsync<UsoMensualDelGrupo>(UsoMensualDelGrupo.Clave(tenantId, periodo), ct))?.LlamadasApi ?? 0;
                mediciones.Add(new MedicionDeMetrica(metrica, pasado, plan?.Limites.De(metrica)));
            }
            else
                mediciones.Add(await control.MedirAsync(tenantId, metrica, plan, periodo, refrescar: false, ct));
        }

        var alertas = mediciones.Where(m => m.Nivel != NivelDeUso.Normal)
            .Select(m => $"{EvaluadorDePlan.Nombre(m.Metrica)}: {EvaluadorDePlan.Cuanto(m, m.Usado)} de {EvaluadorDePlan.Cuanto(m, m.Limite ?? 0)} ({m.Porcentaje} %)").ToList();
        return new UsoDelGrupoDto(periodo, ADto(plan, tenantId),
            [.. mediciones.Select(m => new MedicionDePlanDto(m.Metrica.ToString(), m.Usado, m.Limite, m.Porcentaje, m.Nivel.ToString()))], alertas,
            plan is { BloqueoSuave: true } && mediciones.Any(m => m.Nivel == NivelDeUso.Agotado), plan?.AlmacenamientoMedidoEn);
    }

    // ---------- Plataforma ----------

    [WolverineGet("/api/v1/plataforma/tenants/{tenantId}/plan")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<UsoDelGrupoDto> ObtenerDeUnGrupo(Guid tenantId, string? mes, ISesiones sesiones, ControlDePlan control, TimeProvider reloj, CancellationToken ct)
    {
        await ExigirTenantAsync(sesiones, tenantId, ct);
        return await UsoAsync(tenantId, mes, sesiones, control, reloj, ct);
    }

    /// <summary>
    /// Fija el plan del grupo: código, límites (vacío = sin límite; <c>0</c> = nada) y bloqueo suave. Queda en la auditoría y el
    /// código se refleja en el grupo. 404 <c>tenant-no-encontrado</c>; 422 <c>codigo-plan-invalido</c>, <c>limite-invalido</c>.
    /// </summary>
    [WolverinePut("/api/v1/plataforma/tenants/{tenantId}/plan")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<UsoDelGrupoDto> Actualizar(Guid tenantId, ActualizarPlanDelGrupoDto dto, ISesiones sesiones, IContextoSolicitud contexto, ControlDePlan control,
        TimeProvider reloj, CancellationToken ct)
    {
        await ExigirTenantAsync(sesiones, tenantId, ct);
        var ahora = reloj.GetUtcNow();
        await using (var sesion = sesiones.Global())
        {
            var existente = await sesion.LoadAsync<PlanDelGrupo>(tenantId, ct);
            var antes = existente is null ? null : ADto(existente, tenantId);
            var plan = existente ?? PlanDelGrupo.Nuevo(tenantId, ahora);
            plan.Actualizar(dto.Codigo, new LimitesDePlan(dto.Limites.Usuarios, dto.Limites.Empresas, dto.Limites.VinculosActivos, dto.Limites.AlmacenamientoMb,
                dto.Limites.LlamadasApiMes), dto.BloqueoSuave, ahora);
            // Cambiar los límites reinicia los avisos: lo que ya estaba al 80 % vuelve a avisarse si sigue así con el límite nuevo.
            plan.AvisosEnviados = [];
            sesion.Store(plan);
            if (await sesion.LoadAsync<Tenant>(tenantId, ct) is { } tenant)
            {
                tenant.Plan = plan.Codigo;
                sesion.Store(tenant);
            }

            Auditoria.Registrar(sesion, contexto, Entidad, tenantId.ToString(), existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes,
                ADto(plan, tenantId), ahora);
            await sesion.SaveChangesAsync(ct);
        }

        control.Olvidar(tenantId);
        return await UsoAsync(tenantId, null, sesiones, control, reloj, ct);
    }

    /// <summary>
    /// Mide ya a un grupo (o a todos) frente a su plan y avisa lo que toque (80 % y 100 %): lo mismo que hace el barrido periódico,
    /// para operar y para las pruebas.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/planes/revisar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResultadoDeRevisionDePlanesDto> Revisar(RevisarPlanesDto dto, ISesiones sesiones, EvaluadorDePlan evaluador, IMessageBus mensajes, CancellationToken ct)
    {
        List<Guid> grupos;
        if (dto.TenantId is { } uno)
        {
            await ExigirTenantAsync(sesiones, uno, ct);
            grupos = [uno];
        }
        else
        {
            await using var consulta = sesiones.ConsultaGlobal();
            grupos = [.. await consulta.Query<PlanDelGrupo>().Select(p => p.Id).ToListAsync(ct)];
        }

        var avisos = 0;
        foreach (var tenantId in grupos)
            avisos += await evaluador.EvaluarAsync(tenantId, mensajes, ct);
        return new ResultadoDeRevisionDePlanesDto(grupos.Count, avisos);
    }

    // ---------- El grupo ----------

    /// <summary>El plan del grupo de la credencial (límites y bloqueo suave); sin plan configurado, sin límites.</summary>
    [WolverineGet("/api/v1/tenant/plan")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<PlanDelGrupoDto> ObtenerPlan(ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        await using var consulta = sesiones.ConsultaGlobal();
        return ADto(await consulta.LoadAsync<PlanDelGrupo>(tenantId, ct), tenantId);
    }

    /// <summary>
    /// Lo que el grupo lleva usado frente a su plan en el mes <c>mes</c> (<c>AAAA-MM</c>; por omisión el actual): usuarios, empresas,
    /// vínculos activos, almacenamiento (medido hace como mucho 5 minutos) y llamadas a la API (solo las de las API keys, no las de las
    /// pantallas ni las del sandbox). 422 <c>mes-invalido</c>.
    /// </summary>
    [WolverineGet("/api/v1/tenant/uso")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static Task<UsoDelGrupoDto> ObtenerUso(string? mes, ISesiones sesiones, IContextoSolicitud contexto, ControlDePlan control, TimeProvider reloj, CancellationToken ct) =>
        UsoAsync(contexto.TenantRequerido(), mes, sesiones, control, reloj, ct);

    private static async Task ExigirTenantAsync(ISesiones sesiones, Guid tenantId, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        if (await consulta.LoadAsync<Tenant>(tenantId, ct) is not { } tenant || tenant.EsSandbox)
            throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese tenant.");
    }
}
