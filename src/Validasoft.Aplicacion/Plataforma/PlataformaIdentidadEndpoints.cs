using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Identidad;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Plataforma;

/// <summary>
/// Desactivar y reactivar un sistema integrador (PI-7). Al desactivarlo <b>se revocan todas sus API keys</b> y se cortan sus sesiones (en todos los servidores, PI-8);
/// reactivarlo no restaura las llaves: hay que emitir otras. Lo comparten el administrador del grupo y la consola de Plataforma.
/// </summary>
public static class GestionDeSistemas
{
    public static async Task<SistemaIntegrado> DesactivarIntegradorAsync(Guid tenantId, Guid sistemaId, ISesiones sesiones, IContextoSolicitud contexto,
        ICacheDeCredenciales cacheLlaves, ICacheDeSistemas cacheSistemas, OrigenesDeEmbebido embebido, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.ParaTenant(tenantId);
        var sistema = await sesion.LoadAsync<SistemaIntegrado>(sistemaId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("sistema-no-encontrado", "No existe ese sistema integrado.");
        var antes = sistema.ADto();
        sistema.Desactivar();
        sesion.Store(sistema);
        Auditoria.Registrar(sesion, contexto, "SistemaIntegrado", sistema.Id.ToString(), AccionConfiguracion.Desactivado, antes, sistema.ADto(), ahora);

        // Sus llaves (también las de prueba, que viven en el grupo de producción) se revocan en la misma transacción.
        var llaves = await sesion.Query<ApiKey>().Where(k => k.SistemaId == sistemaId && k.RevocadaEn == null).ToListAsync(ct);
        foreach (var llave in llaves)
        {
            llave.RevocadaEn = ahora;
            sesion.Store(llave);
            Auditoria.Registrar(sesion, contexto, "ApiKey", llave.Id.ToString(), AccionConfiguracion.Revocado, null, llave.ADto(), ahora);
        }

        await sesion.SaveChangesAsync(ct);
        foreach (var llave in llaves)
            cacheLlaves.Olvidar(llave.Id);
        cacheSistemas.OlvidarIntegrador(tenantId, sistema.Codigo);
        embebido.Invalidar();
        return sistema;
    }

    public static async Task<SistemaIntegrado> ReactivarIntegradorAsync(Guid tenantId, Guid sistemaId, ISesiones sesiones, IContextoSolicitud contexto,
        ICacheDeSistemas cacheSistemas, OrigenesDeEmbebido embebido, TimeProvider reloj, CancellationToken ct)
    {
        await using var sesion = sesiones.ParaTenant(tenantId);
        var sistema = await sesion.LoadAsync<SistemaIntegrado>(sistemaId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("sistema-no-encontrado", "No existe ese sistema integrado.");
        var antes = sistema.ADto();
        sistema.Reactivar();
        sesion.Store(sistema);
        Auditoria.Registrar(sesion, contexto, "SistemaIntegrado", sistema.Id.ToString(), AccionConfiguracion.Reactivado, antes, sistema.ADto(), reloj.GetUtcNow());
        await sesion.SaveChangesAsync(ct);
        cacheSistemas.OlvidarIntegrador(tenantId, sistema.Codigo);
        embebido.Invalidar();
        return sistema;
    }
}

/// <summary>Consola de Plataforma sobre los sistemas integradores de un grupo (PI-7): verlos, desactivarlos (con sus llaves y sesiones) y reactivarlos.</summary>
public static class SistemasDeGrupoEndpoints
{
    [WolverineGet("/api/v1/plataforma/tenants/{tenantId}/sistemas")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IReadOnlyList<SistemaIntegradoDto>> Listar(Guid tenantId, ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        if (await consulta.LoadAsync<Tenant>(tenantId, ct) is null)
            throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese tenant.");
        await using var sesion = sesiones.ParaTenant(tenantId);
        return [.. (await sesion.Query<SistemaIntegrado>().OrderBy(s => s.Codigo).ToListAsync(ct)).Select(s => s.ADto())];
    }

    /// <summary>Desactiva el sistema del grupo y revoca sus llaves y sesiones. 409 <c>sistema-inactivo</c> si ya estaba desactivado.</summary>
    [WolverinePost("/api/v1/plataforma/tenants/{tenantId}/sistemas/{id}/desactivar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SistemaIntegradoDto> Desactivar(Guid tenantId, Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeCredenciales cacheLlaves,
        ICacheDeSistemas cacheSistemas, OrigenesDeEmbebido embebido, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "desactivar-sistema", ct);
        await AsegurarTenantAsync(sesiones, tenantId, ct);
        return (await GestionDeSistemas.DesactivarIntegradorAsync(tenantId, id, sesiones, contexto, cacheLlaves, cacheSistemas, embebido, reloj, ct)).ADto();
    }

    [WolverinePost("/api/v1/plataforma/tenants/{tenantId}/sistemas/{id}/reactivar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SistemaIntegradoDto> Reactivar(Guid tenantId, Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeSistemas cacheSistemas,
        OrigenesDeEmbebido embebido, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "reactivar-sistema", ct);
        await AsegurarTenantAsync(sesiones, tenantId, ct);
        return (await GestionDeSistemas.ReactivarIntegradorAsync(tenantId, id, sesiones, contexto, cacheSistemas, embebido, reloj, ct)).ADto();
    }

    private static async Task AsegurarTenantAsync(ISesiones sesiones, Guid tenantId, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        if (await consulta.LoadAsync<Tenant>(tenantId, ct) is null)
            throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese tenant.");
    }
}

/// <summary>Desactivar y reactivar un sistema de Plataforma (el backoffice que firma las aserciones de sus operadores, PI-7): sus sesiones dejan de valer en segundos.</summary>
public static class SistemasDePlataformaEstadoEndpoints
{
    [WolverinePost("/api/v1/plataforma/sistemas/{codigo}/desactivar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SistemaDePlataformaDto> Desactivar(string codigo, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeSistemas cacheSistemas,
        OrigenesDeEmbebido embebido, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "desactivar-sistema-de-plataforma", ct);
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var sistema = await sesion.LoadAsync<SistemaDePlataforma>((codigo ?? string.Empty).Trim().ToUpperInvariant(), ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("sistema-no-encontrado", "No existe ese sistema de Plataforma.");
        var antes = sistema.ADto();
        sistema.Desactivar(ahora);
        sesion.Store(sistema);
        Auditoria.Registrar(sesion, contexto, OperadoresDePlataformaEndpoints.EntidadSistema, sistema.Id, AccionConfiguracion.Desactivado, antes, sistema.ADto(), ahora);
        // Las sesiones que ya emitió para sus operadores no sobreviven (ni siquiera si el sistema se reactiva): cada operador cambia de generación.
        var operadores = await sesion.Query<OperadorDePlataforma>().Where(o => o.Sistema == sistema.Id).ToListAsync(ct);
        foreach (var operador in operadores)
        {
            operador.InvalidarSesiones();
            sesion.Store(operador);
        }

        await sesion.SaveChangesAsync(ct);
        foreach (var operador in operadores)
            cacheSistemas.OlvidarOperador(operador.Id);
        embebido.Invalidar();
        return sistema.ADto();
    }

    [WolverinePost("/api/v1/plataforma/sistemas/{codigo}/reactivar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SistemaDePlataformaDto> Reactivar(string codigo, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ICacheDeSistemas cacheSistemas,
        OrigenesDeEmbebido embebido, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "reactivar-sistema-de-plataforma", ct);
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var sistema = await sesion.LoadAsync<SistemaDePlataforma>((codigo ?? string.Empty).Trim().ToUpperInvariant(), ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("sistema-no-encontrado", "No existe ese sistema de Plataforma.");
        var antes = sistema.ADto();
        sistema.Reactivar(ahora);
        sesion.Store(sistema);
        Auditoria.Registrar(sesion, contexto, OperadoresDePlataformaEndpoints.EntidadSistema, sistema.Id, AccionConfiguracion.Reactivado, antes, sistema.ADto(), ahora);
        var operadores = await sesion.Query<OperadorDePlataforma>().Where(o => o.Sistema == sistema.Id).Select(o => o.Id).ToListAsync(ct);
        await sesion.SaveChangesAsync(ct);
        foreach (var operador in operadores)
            cacheSistemas.OlvidarOperador(operador);
        embebido.Invalidar();
        return sistema.ADto();
    }
}

/// <summary>Agregar más empresas a un grupo que ya existe, desde la consola de Plataforma (PI-9).</summary>
public static class EmpresasDeGrupoEndpoints
{
    /// <summary>
    /// Crea una empresa en el grupo (con su auditoría, a nombre de Plataforma). Con <c>Idempotency-Key</c> repetir la petición no la duplica. 404
    /// <c>tenant-no-encontrado</c>; 409 <c>grupo-inactivo</c>, <c>empresa-duplicada</c> (misma identificación en el grupo), <c>prefijo-folio-duplicado</c>; 422
    /// <c>prefijo-folio-invalido</c>, <c>nombre-requerido</c> y los de la identificación.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/tenants/{tenantId}/empresas")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Crear(Guid tenantId, CrearEmpresaDto dto, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        Tenant tenant;
        await using (var consulta = sesiones.ConsultaGlobal())
            tenant = await consulta.LoadAsync<Tenant>(tenantId, ct) ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese tenant.");
        if (tenant.EsSandbox)
            throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese tenant.");
        if (!tenant.Activo)
            throw ReglaDeNegocioException.Conflicto("grupo-inactivo", "El grupo está desactivado: reactívalo antes de agregarle empresas.");

        var empresa = Empresa.Crear(tenantId, dto.Identificacion.ADominio(), dto.Nombre, dto.PrefijoFolio, dto.LogoUrl, dto.ReplyTo, ahora);
        await using var sesion = sesiones.ParaTenant(tenantId);
        var existentes = await sesion.Query<Empresa>()
            .Where(e => e.TenantId == tenantId && (e.ClaveIdentificacion == empresa.ClaveIdentificacion || e.PrefijoFolio == empresa.PrefijoFolio))
            .ToListAsync(ct);
        if (existentes.Any(e => e.ClaveIdentificacion == empresa.ClaveIdentificacion))
            throw ReglaDeNegocioException.Conflicto("empresa-duplicada", "Ya existe una empresa con esa identificación en el grupo.");
        if (existentes.Any(e => e.PrefijoFolio == empresa.PrefijoFolio))
            throw ReglaDeNegocioException.Conflicto("prefijo-folio-duplicado", $"El prefijo {empresa.PrefijoFolio} ya lo usa otra empresa del grupo.");

        sesion.Store(empresa);
        Auditoria.Registrar(sesion, contexto, "Empresa", empresa.Id.ToString(), AccionConfiguracion.Creado, null, empresa.ADto(), ahora, empresa.Id);
        await sesion.SaveChangesAsync(ct);
        return Results.Created($"/api/v1/empresas/{empresa.Id}", empresa.ADto());
    }
}

/// <summary>
/// La clave de arranque de Plataforma (PI-17): el servidor la tiene en <c>Validasoft:Plataforma:ClaveBootstrap</c> y sirve para dar de alta al primer operador. Un operador
/// con passkey puede <b>desactivarla desde la consola</b> (en todos los servidores a la vez, sin reiniciar), siempre que haya al menos un operador con passkey que pueda
/// entrar después; y reactivarla si hiciera falta. Quitarla de la configuración del servidor sigue siendo lo más seguro.
/// </summary>
public static class ClaveDeArranqueEndpoints
{
    public const string Entidad = "ClaveDeArranque";

    [WolverineGet("/api/v1/plataforma/clave-de-arranque")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ClaveDeArranqueDto> Estado(ISesiones sesiones, IConfiguration configuracion, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return await EstadoAsync(consulta, configuracion, ct);
    }

    /// <summary>
    /// Desactiva la clave de arranque: deja de autenticar de inmediato en todos los servidores. 409 <c>sin-operadores-con-passkey</c> si ningún operador activo tiene
    /// passkey (quedaría sin poder entrar a la consola), <c>clave-de-arranque-desactivada</c> si ya estaba apagada. Exige reautenticar con la passkey.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/clave-de-arranque/desactivar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ClaveDeArranqueDto> Desactivar(HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, IConfiguration configuracion,
        ICacheDeClaveDeArranque cache, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "desactivar-clave-de-arranque", ct);
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var antes = await EstadoAsync(sesion, configuracion, ct);
        if (antes.OperadoresConPasskey == 0)
            throw ReglaDeNegocioException.Conflicto("sin-operadores-con-passkey",
                "Antes de desactivar la clave de arranque, da de alta un operador y que registre su passkey: si no, nadie podría entrar a la consola.");

        var configuracionGlobal = await sesion.LoadAsync<ConfiguracionDePlataforma>(ConfiguracionDePlataforma.IdUnico, ct) ?? new ConfiguracionDePlataforma();
        configuracionGlobal.DesactivarClaveDeArranque($"{contexto.ActorTipo}:{contexto.ActorId}", ahora);
        sesion.Store(configuracionGlobal);
        var despues = antes with { Desactivada = true, DesactivadaEn = ahora, DesactivadaPor = configuracionGlobal.DesactivadaPor };
        Auditoria.Registrar(sesion, contexto, Entidad, ConfiguracionDePlataforma.IdUnico, AccionConfiguracion.Desactivado, antes, despues, ahora);
        await sesion.SaveChangesAsync(ct);
        cache.Olvidar();
        return despues;
    }

    /// <summary>Vuelve a permitir la clave de arranque (si el servidor aún la tiene configurada). 409 <c>clave-de-arranque-activa</c>.</summary>
    [WolverinePost("/api/v1/plataforma/clave-de-arranque/reactivar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ClaveDeArranqueDto> Reactivar(HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, IConfiguration configuracion,
        ICacheDeClaveDeArranque cache, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "reactivar-clave-de-arranque", ct);
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var antes = await EstadoAsync(sesion, configuracion, ct);
        var configuracionGlobal = await sesion.LoadAsync<ConfiguracionDePlataforma>(ConfiguracionDePlataforma.IdUnico, ct) ?? new ConfiguracionDePlataforma();
        configuracionGlobal.ReactivarClaveDeArranque($"{contexto.ActorTipo}:{contexto.ActorId}", ahora);
        sesion.Store(configuracionGlobal);
        var despues = antes with { Desactivada = false, DesactivadaEn = null, DesactivadaPor = null };
        Auditoria.Registrar(sesion, contexto, Entidad, ConfiguracionDePlataforma.IdUnico, AccionConfiguracion.Reactivado, antes, despues, ahora);
        await sesion.SaveChangesAsync(ct);
        cache.Olvidar();
        return despues;
    }

    private static async Task<ClaveDeArranqueDto> EstadoAsync(IQuerySession consulta, IConfiguration configuracion, CancellationToken ct)
    {
        var global = await consulta.LoadAsync<ConfiguracionDePlataforma>(ConfiguracionDePlataforma.IdUnico, ct);
        var operadores = await consulta.Query<OperadorDePlataforma>().Where(o => o.Activo && o.Sistema == OperadorDePlataforma.SistemaDirecto).Select(o => o.Id).ToListAsync(ct);
        var conPasskey = operadores.Count == 0
            ? 0
            : (await consulta.Query<Passkey>().Where(p => p.Propietario == TipoDePropietario.Operador && p.RevocadaEn == null).Select(p => p.SujetoId).ToListAsync(ct))
                .Distinct().Count(s => operadores.Contains(s));
        var configurada = configuracion["Validasoft:Plataforma:ClaveBootstrap"] is { Length: >= 32 };
        return new ClaveDeArranqueDto(configurada, global?.ClaveDeArranqueDesactivada ?? false, global?.DesactivadaEn, global?.DesactivadaPor, conPasskey);
    }
}

/// <summary>La auditoría de los ingresos de los operadores de Plataforma en la consola (PI-16): quién entró, cuándo, cómo y desde dónde; lo que falló y por qué. Nunca códigos ni tokens.</summary>
public static class AuditoriaDeOperadoresEndpoints
{
    /// <summary>
    /// Los registros de identidad de los operadores (ingresos exitosos y fallidos, passkeys, sesiones, reautenticaciones), de lo más reciente a lo más antiguo. Filtros:
    /// <c>operador</c> (id), <c>evento</c> (<c>IngresoExitoso</c>, <c>IngresoFallido</c>, <c>PasskeyRegistrada</c>…), <c>exitoso</c>, <c>desde</c>/<c>hasta</c>. 422
    /// <c>evento-invalido</c>, <c>rango-invalido</c>.
    /// </summary>
    [WolverineGet("/api/v1/plataforma/auditoria-ingresos")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<PaginaDto<IngresoDeOperadorDto>> Listar(ISesiones sesiones, string? operador, string? evento, bool? exitoso, DateTimeOffset? desde, DateTimeOffset? hasta,
        int? limite, string? cursor, CancellationToken ct)
    {
        if (desde is not null && hasta is not null && desde > hasta)
            throw ReglaDeNegocioException.Validacion("rango-invalido", "La fecha inicial es posterior a la final.");
        EventoDeIngreso? buscado = null;
        if (!string.IsNullOrWhiteSpace(evento))
            buscado = Enum.TryParse<EventoDeIngreso>(evento, ignoreCase: true, out var e) ? e : throw ReglaDeNegocioException.Validacion("evento-invalido", $"«{evento}» no es un evento de ingreso.");

        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var salto = Peticiones.LeerDesplazamiento(cursor);
        await using var consulta = sesiones.ConsultaGlobal();
        IQueryable<RegistroDeIngreso> filas = consulta.Query<RegistroDeIngreso>().Where(r => r.Propietario == TipoDePropietario.Operador);
        if (!string.IsNullOrWhiteSpace(operador))
            filas = filas.Where(r => r.SujetoId == operador);
        if (buscado is { } tipo)
            filas = filas.Where(r => r.Evento == tipo);
        if (exitoso is { } resultado)
            filas = filas.Where(r => r.Exitoso == resultado);
        if (desde is { } d)
            filas = filas.Where(r => r.Fecha >= d);
        if (hasta is { } h)
            filas = filas.Where(r => r.Fecha <= h);

        var pagina = await filas.OrderByDescending(r => r.Fecha).ThenBy(r => r.Id).Skip(salto).Take(tamano + 1).ToListAsync(ct);
        var hayMas = pagina.Count > tamano;
        return new PaginaDto<IngresoDeOperadorDto>(
            [.. pagina.Take(tamano).Select(r => new IngresoDeOperadorDto(r.Id, r.Fecha, r.Evento.ToString(), r.Exitoso, r.SujetoId, r.Correo, r.Metodo?.ToString(), r.Motivo, r.Ip,
                r.Dispositivo, r.ActorTipo.ToString(), r.ActorId, r.SesionId, r.PasskeyId))],
            hayMas ? Peticiones.EscribirDesplazamiento(salto + tamano) : null);
    }
}
