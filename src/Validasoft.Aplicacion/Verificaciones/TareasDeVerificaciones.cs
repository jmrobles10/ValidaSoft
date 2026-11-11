using Marten;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Verificaciones;

/// <summary>
/// Trabajo en segundo plano de las verificaciones (docs/11 §2.7.8): la consulta automática de listas cuando el expediente pasa
/// a revisión o su resultado vence (VE-5), la expiración de lo satisfecho con el expediente abierto (estudio a los 30 días) y
/// el monitoreo periódico de los vínculos activos (VE-12). Cada paso es idempotente y toma el stream con bloqueo de fila antes
/// de escribir; la llamada al proveedor se hace antes, sin bloquear el expediente.
/// </summary>
public sealed class TareasDeVerificaciones(IDocumentStore store, ConsultaDeListas consulta, EmisorDeCertificados certificados, TimeProvider reloj,
    ILogger<TareasDeVerificaciones> registro)
{
    public const string ConsultadoPorElSistema = "Sistema (consulta automática)";

    public async Task ProcesarPendientesAsync(CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        List<Expediente> porConsultar, porVencer;
        List<MonitoreoDeListas> monitoreos;
        Dictionary<Guid, Guid> tenants;
        await using (var global = store.QuerySession())
        {
            porConsultar = [.. await global.Query<Expediente>().Where(e => e.AnyTenant() && e.ProximaConsultaDeListas != null && e.ProximaConsultaDeListas <= ahora)
                .OrderBy(e => e.ProximaConsultaDeListas).Take(20).ToListAsync(ct)];
            porVencer = [.. await global.Query<Expediente>().Where(e => e.AnyTenant() && e.ProximoVencimientoDeVerificacion != null && e.ProximoVencimientoDeVerificacion <= ahora)
                .Take(50).ToListAsync(ct)];
            monitoreos = [.. await global.Query<MonitoreoDeListas>().Where(m => m.AnyTenant() && m.ProximaConsulta != null && m.ProximaConsulta <= ahora)
                .OrderBy(m => m.ProximaConsulta).Take(20).ToListAsync(ct)];
            var empresas = porConsultar.Select(e => e.EmpresaId).Concat(porVencer.Select(e => e.EmpresaId)).Concat(monitoreos.Select(m => m.EmpresaId))
                .Distinct().ToArray();
            tenants = empresas.Length == 0 ? [] : (await global.LoadManyAsync<Empresa>(ct, empresas)).ToDictionary(e => e.Id, e => e.TenantId);
        }

        foreach (var expediente in porVencer.Where(e => tenants.ContainsKey(e.EmpresaId)))
            await IntentarAsync(() => ExpirarAsync(tenants[expediente.EmpresaId], expediente.Id, ct), expediente.Folio, "expirar las verificaciones");
        foreach (var expediente in porConsultar.Where(e => tenants.ContainsKey(e.EmpresaId)))
            await IntentarAsync(() => ConsultarExpedienteAsync(tenants[expediente.EmpresaId], expediente.Id, ct), expediente.Folio, "consultar las listas");
        foreach (var monitoreo in monitoreos.Where(m => tenants.ContainsKey(m.EmpresaId)))
            await IntentarAsync(() => MonitorearAsync(tenants[monitoreo.EmpresaId], monitoreo.Id, ct), monitoreo.Folio, "monitorear las listas");
    }

    /// <summary>Consulta automática de las listas de un expediente (pendientes o vencidas); si el proveedor falla, se reintenta luego.</summary>
    public async Task ConsultarExpedienteAsync(Guid tenantId, Guid expedienteId, CancellationToken ct)
    {
        await using var sesion = SesionDeSistema(tenantId);
        var ahora = reloj.GetUtcNow();
        var expediente = await sesion.LoadAsync<Expediente>(expedienteId, ct);
        if (expediente?.ProximaConsultaDeListas is not { } cuando || cuando > ahora)
            return;

        var listas = expediente.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)!;
        ResultadoDeListas resultado;
        try
        {
            resultado = await consulta.ConsultarAsync(sesion, tenantId, expediente.EmpresaId, expediente.TerceroId, expediente.Folio, ConsultadoPorElSistema,
                listas.FrecuenciaReconsultaMeses ?? Expediente.MesesReconsultaPorOmision, ct);
        }
        catch (ReglaDeNegocioException e)
        {
            var espera = TimeSpan.FromMinutes(consulta.Opciones.MinutosReintento * Math.Pow(2, Math.Min(listas.Fallos, 6)));
            var fallo = await sesion.Events.FetchForExclusiveWriting<Expediente>(expedienteId, ct);
            var eventos = fallo.Aggregate!.RegistrarFalloDeConsulta(TipoVerificacion.ListasRestrictivas, listas.Proveedor ?? "—", e.Message, ahora + espera, ahora);
            await ProcesoDeValidacion.AnexarAsync(sesion, fallo, eventos, null, certificados, ahora, ct);
            await sesion.SaveChangesAsync(ct);
            return;
        }

        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(expedienteId, ct);
        // Mientras se consultaba al proveedor alguien pudo consultar a mano: esa consulta manda (y sus coincidencias, con sus
        // ids, no se pisan). Solo se registra si la consulta automática sigue pendiente.
        if (stream.Aggregate is not { EstaAbierto: true } actual || actual.ProximaConsultaDeListas is not { } pendiente || pendiente > reloj.GetUtcNow())
            return;
        await ProcesoDeValidacion.AnexarAsync(sesion, stream, actual.RegistrarConsultaDeListas(resultado, "sistema", reloj.GetUtcNow()), null, certificados,
            reloj.GetUtcNow(), ct);
        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>Expira lo que venció con el expediente abierto (las listas quedan para reconsulta; el estudio, para el oficial).</summary>
    public async Task ExpirarAsync(Guid tenantId, Guid expedienteId, CancellationToken ct)
    {
        await using var sesion = SesionDeSistema(tenantId);
        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(expedienteId, ct);
        var ahora = reloj.GetUtcNow();
        if (stream.Aggregate is not { } expediente)
            return;
        var eventos = expediente.ExpirarVerificaciones(ahora);
        if (eventos.Count == 0)
            return;
        await ProcesoDeValidacion.AnexarAsync(sesion, stream, eventos, null, certificados, ahora, ct);
        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Reconsulta periódica de un vínculo activo (VE-12). Si el vínculo ya no está activo, el monitoreo se detiene; las
    /// coincidencias nuevas avisan a Cumplimiento por la campana (y quedan en su bandeja).
    /// </summary>
    public async Task MonitorearAsync(Guid tenantId, Guid vinculoId, CancellationToken ct)
    {
        await using var sesion = SesionDeSistema(tenantId);
        var ahora = reloj.GetUtcNow();
        var monitoreo = await sesion.LoadAsync<MonitoreoDeListas>(vinculoId, ct);
        if (monitoreo?.ProximaConsulta is not { } cuando || cuando > ahora)
            return;

        var vinculo = await sesion.LoadAsync<Vinculo>(vinculoId, ct);
        if (vinculo is not { Estado: EstadoVinculo.Activo or EstadoVinculo.PorVencer })
        {
            monitoreo.Detener(ahora);
            sesion.Store(monitoreo);
            await sesion.SaveChangesAsync(ct);
            return;
        }

        IReadOnlyList<CoincidenciaDeVerificacion> nuevas;
        try
        {
            var resultado = await consulta.ConsultarAsync(sesion, tenantId, monitoreo.EmpresaId, monitoreo.TerceroId, $"{monitoreo.Folio} (monitoreo)",
                "Sistema (monitoreo periódico)", Expediente.MesesReconsultaPorOmision, ct);
            nuevas = monitoreo.RegistrarConsulta(resultado, "sistema", reloj.GetUtcNow());
        }
        catch (ReglaDeNegocioException e)
        {
            monitoreo.RegistrarFallo(e.Message, ahora + TimeSpan.FromMinutes(consulta.Opciones.MinutosReintento * Math.Pow(2, Math.Min(monitoreo.Listas.Fallos, 6))),
                ahora);
            sesion.Store(monitoreo);
            await sesion.SaveChangesAsync(ct);
            return;
        }

        sesion.Store(monitoreo);
        if (nuevas.Count > 0)
        {
            var usuarios = await sesion.Query<Usuario>().Where(u => u.Activo).ToListAsync(ct);
            var empresa = await sesion.LoadAsync<Empresa>(monitoreo.EmpresaId, ct);
            var plan = NotificacionesDeCumplimiento.Plan(tenantId, monitoreo.EmpresaId, TiposNotificacion.CoincidenciaListas, "Coincidencia en el monitoreo de listas",
                $"La reconsulta periódica de {monitoreo.TerceroNombre} encontró {nuevas.Count} coincidencia(s) nueva(s) en listas restrictivas.",
                $"monitoreo:{vinculoId:N}:{monitoreo.Listas.ReferenciaExterna}", null, vinculoId, monitoreo.Folio, $"/e/{monitoreo.EmpresaId}/cumplimiento",
                empresa?.Nombre, usuarios, _ => null, ahora, ahora.UtcTicks);
            foreach (var (_, notificacion) in plan.Campana)
                sesion.Store(notificacion);
        }

        await sesion.SaveChangesAsync(ct);
    }

    private async Task IntentarAsync(Func<Task> accion, string folio, string que)
    {
        try
        {
            await accion();
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            registro.LogWarning(e, "No se pudo {Que} de {Folio}; se reintentará.", que, folio);
        }
    }

    private IDocumentSession SesionDeSistema(Guid tenantId)
    {
        var sesion = store.LightweightSession(tenantId.ToString("N"));
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", "verificaciones");
        return sesion;
    }
}

/// <summary>
/// A quién avisa Cumplimiento (VE-13): las personas activas con rol Oficial de cumplimiento en la empresa (o en todo el tenant);
/// si la empresa no tiene ninguno, sus administradores, para que el aviso no se pierda.
/// </summary>
public static class NotificacionesDeCumplimiento
{
    public static IReadOnlyList<Usuario> Destinatarios(IEnumerable<Usuario> usuarios, Guid empresaId)
    {
        var activos = usuarios.Where(u => u.Activo).ToList();
        bool Con(Usuario u, Rol rol) => u.Roles.Any(r => r.Rol == rol && (r.EmpresaId is null || r.EmpresaId == empresaId));
        var oficiales = activos.Where(u => Con(u, Rol.OficialCumplimiento)).OrderBy(u => u.Id).ToList();
        return oficiales.Count > 0 ? oficiales : [.. activos.Where(u => Con(u, Rol.Administrador)).OrderBy(u => u.Id)];
    }

    public static PlanDeNotificaciones Plan(Guid tenantId, Guid empresaId, string tipo, string titulo, string mensaje, string clave, Guid? expedienteId,
        Guid? vinculoId, string? folio, string ruta, string? empresa, IEnumerable<Usuario> usuarios, Func<Guid, PreferenciasNotificacion?> preferencias,
        DateTimeOffset ahora, long orden)
    {
        var destinatarios = Destinatarios(usuarios, empresaId);
        var texto = folio is null ? mensaje : $"{folio}: {mensaje}";
        var campana = destinatarios.Select(u => (tenantId, new Notificacion
        {
            Id = Deterministas.Guid($"notificacion:{clave}:{u.Id:N}:{tipo}"),
            UsuarioId = u.Id,
            EmpresaId = empresaId,
            Tipo = tipo,
            Titulo = titulo,
            Mensaje = texto,
            ExpedienteId = expedienteId,
            VinculoId = vinculoId,
            Folio = folio,
            Enlace = ruta,
            CreadaEn = ahora,
            Orden = orden
        })).ToList();
        var correos = destinatarios.Where(u => TiposNotificacion.QuiereCorreo(preferencias(u.Id), tipo))
            .Select(u => (object)new CorreoDeNotificacionPendiente(u.Correo, u.Nombre, $"{titulo}{(folio is null ? string.Empty : $": {folio}")}",
                [mensaje, empresa is null ? "Revisa la bandeja de cumplimiento." : $"Empresa: {empresa}. Revisa la bandeja de cumplimiento."], ruta, tenantId))
            .ToList();
        return new PlanDeNotificaciones(campana, correos);
    }
}
