using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Plataforma;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Sandbox;

/// <summary>
/// El sandbox del grupo y su buzón de correo (SB-1…SB-6, docs/08 §2.2). Funcionan con las credenciales de producción y con las de
/// prueba del mismo grupo, siempre sobre el sandbox: nunca tocan datos de producción. Piden <c>sandbox:administrar</c>.
/// </summary>
public static class SandboxEndpoints
{
    private const int TamanoPagina = 50;
    private const int MaximoPagina = 200;

    /// <summary>
    /// Estado del sandbox: si existe, sus empresas (con el id que se usa en <c>X-Empresa</c> con una llave de prueba), los códigos de
    /// sus sistemas y cuántos correos tiene en el buzón. Sin sandbox responde <c>existe: false</c>.
    /// </summary>
    [WolverineGet("/api/v1/sandbox")]
    [Authorize(Policy = Permisos.SandboxAdministrar)]
    public static async Task<SandboxDto> Obtener(ServicioDeSandbox servicio, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var (_, sandbox) = await servicio.ResolverAsync(contexto.TenantRequerido(), ct);
        return await EstadoAsync(sesiones, contexto, sandbox, ct);
    }

    /// <summary>
    /// Crea el sandbox —un tenant hermano con la configuración del grupo y ningún dato— o, si ya existe, le agrega lo que le falte
    /// de producción (empresas nuevas, flujos…) sin tocar lo que ya tiene. 201 al crearlo, 200 si ya existía. 409
    /// <c>tenant-inactivo</c>. Emitir la primera API key de prueba también lo crea.
    /// </summary>
    [WolverinePost("/api/v1/sandbox")]
    [Authorize(Policy = Permisos.SandboxAdministrar)]
    public static async Task<IResult> Crear(ServicioDeSandbox servicio, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var (sandbox, creado) = await servicio.AsegurarAsync(contexto.TenantRequerido(), ct);
        if (creado)
            await AuditarAsync(servicio, sesiones, contexto, sandbox, AccionConfiguracion.Creado, reloj.GetUtcNow(), ct);

        var estado = await EstadoAsync(sesiones, contexto, sandbox, ct);
        return creado ? Results.Created("/api/v1/sandbox", estado) : Results.Ok(estado);
    }

    /// <summary>
    /// Reinicia el sandbox: borra todo lo hecho en él y lo siembra de nuevo con la configuración de producción de ahora. Las empresas
    /// conservan su id y las llaves de prueba siguen valiendo. 404 <c>sandbox-no-existe</c>.
    /// </summary>
    [WolverinePost("/api/v1/sandbox/reiniciar")]
    [Authorize(Policy = Permisos.SandboxAdministrar)]
    public static async Task<SandboxDto> Reiniciar(ServicioDeSandbox servicio, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        CancellationToken ct)
    {
        var sandbox = await servicio.ReiniciarAsync(contexto.TenantRequerido(), ct);
        await AuditarAsync(servicio, sesiones, contexto, sandbox, AccionConfiguracion.Modificado, reloj.GetUtcNow(), ct);
        return await EstadoAsync(sesiones, contexto, sandbox, ct);
    }

    /// <summary>
    /// Los correos que el sandbox habría enviado, los más nuevos primero. <paramref name="para"/>: solo los de ese destinatario;
    /// <paramref name="asunto"/>: solo los que lo contienen. <paramref name="limite"/> por omisión 50 (máximo 200); <paramref name="cursor"/>
    /// es el opaco de la página anterior. 404 <c>sandbox-no-existe</c>.
    /// </summary>
    [WolverineGet("/api/v1/sandbox/correos")]
    [Authorize(Policy = Permisos.SandboxAdministrar)]
    public static async Task<PaginaDto<CorreoDePruebaDto>> Correos(ServicioDeSandbox servicio, ISesiones sesiones, IContextoSolicitud contexto,
        string? para, string? asunto, int? limite, string? cursor, CancellationToken ct)
    {
        var sandbox = await ExigirAsync(servicio, contexto, ct);
        var tamano = Math.Clamp(limite ?? TamanoPagina, 1, MaximoPagina);
        var desde = Peticiones.LeerDesplazamiento(cursor);

        await using var sesion = sesiones.ParaTenant(sandbox.Id);
        IQueryable<CorreoDePrueba> consulta = sesion.Query<CorreoDePrueba>();
        if (!string.IsNullOrWhiteSpace(para))
        {
            var destinatario = para.Trim();
            consulta = consulta.Where(c => c.Para.Equals(destinatario, StringComparison.OrdinalIgnoreCase));
        }

        if (!string.IsNullOrWhiteSpace(asunto))
        {
            var texto = asunto.Trim();
            consulta = consulta.Where(c => c.Asunto.Contains(texto, StringComparison.OrdinalIgnoreCase));
        }

        var pagina = await consulta.OrderByDescending(c => c.RecibidoEn).ThenByDescending(c => c.Id).Skip(desde).Take(tamano + 1).ToListAsync(ct);
        var hayMas = pagina.Count > tamano;
        return new PaginaDto<CorreoDePruebaDto>([.. pagina.Take(tamano).Select(c => c.ADto())], hayMas ? Peticiones.EscribirDesplazamiento(desde + tamano) : null);
    }

    /// <summary>Un correo del buzón. 404 <c>correo-no-encontrado</c>.</summary>
    [WolverineGet("/api/v1/sandbox/correos/{id}")]
    [Authorize(Policy = Permisos.SandboxAdministrar)]
    public static async Task<CorreoDePruebaDto> Correo(Guid id, ServicioDeSandbox servicio, ISesiones sesiones, IContextoSolicitud contexto,
        CancellationToken ct)
    {
        var sandbox = await ExigirAsync(servicio, contexto, ct);
        await using var sesion = sesiones.ParaTenant(sandbox.Id);
        return (await sesion.LoadAsync<CorreoDePrueba>(id, ct))?.ADto()
               ?? throw ReglaDeNegocioException.NoEncontrado("correo-no-encontrado", "No existe ese correo en el buzón del sandbox.");
    }

    /// <summary>Vacía el buzón (los correos no se recuperan). 204.</summary>
    [WolverineDelete("/api/v1/sandbox/correos")]
    [Authorize(Policy = Permisos.SandboxAdministrar)]
    public static async Task<IResult> VaciarCorreos(ServicioDeSandbox servicio, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var sandbox = await ExigirAsync(servicio, contexto, ct);
        await using var sesion = sesiones.ParaTenant(sandbox.Id);
        sesion.DeleteWhere<CorreoDePrueba>(_ => true);
        await sesion.SaveChangesAsync(ct);
        return Results.NoContent();
    }

    private static async Task<Tenant> ExigirAsync(ServicioDeSandbox servicio, IContextoSolicitud contexto, CancellationToken ct) =>
        (await servicio.ResolverAsync(contexto.TenantRequerido(), ct)).Sandbox
        ?? throw ReglaDeNegocioException.NoEncontrado("sandbox-no-existe", "El grupo aún no tiene sandbox: créalo con POST /api/v1/sandbox.");

    private static async Task<SandboxDto> EstadoAsync(ISesiones sesiones, IContextoSolicitud contexto, Tenant? sandbox, CancellationToken ct)
    {
        var entorno = contexto.EsSandbox ? AislamientoDeSandbox.Entorno : "produccion";
        if (sandbox is null)
            return new SandboxDto(false, entorno, null, null, null, [], [], 0);

        List<Empresa> empresas;
        await using (var consulta = sesiones.ConsultaGlobal())
            empresas = [.. await consulta.Query<Empresa>().Where(e => e.TenantId == sandbox.Id).OrderBy(e => e.Nombre).ToListAsync(ct)];

        await using var sesion = sesiones.ParaTenant(sandbox.Id);
        var sistemas = await sesion.Query<Dominio.Identidad.SistemaIntegrado>().OrderBy(s => s.Codigo).Select(s => s.Codigo).ToListAsync(ct);
        var correos = await sesion.Query<CorreoDePrueba>().CountAsync(ct);

        return new SandboxDto(true, entorno, sandbox.Id, sandbox.CreadoEn, sandbox.ReiniciadoEn,
            [.. empresas.Select(e => new EmpresaDeSandboxDto(e.Id, e.EmpresaDeProduccionId ?? Guid.Empty, e.Nombre, e.PrefijoFolio, e.Activa))],
            [.. sistemas], correos);
    }

    /// <summary>Queda en la auditoría del grupo de producción, aunque la haya pedido una llave de prueba.</summary>
    private static async Task AuditarAsync(ServicioDeSandbox servicio, ISesiones sesiones, IContextoSolicitud contexto, Tenant sandbox,
        AccionConfiguracion accion, DateTimeOffset ahora, CancellationToken ct)
    {
        var (produccion, _) = await servicio.ResolverAsync(contexto.TenantRequerido(), ct);
        await using var sesion = sesiones.ParaTenant(produccion.Id);
        Auditoria.Registrar(sesion, contexto, "Sandbox", sandbox.Id.ToString(), accion, null,
            new { sandbox.Id, sandbox.CreadoEn, sandbox.ReiniciadoEn, EntornoDeLaCredencial = contexto.EsSandbox ? "sandbox" : "produccion" }, ahora);
        await sesion.SaveChangesAsync(ct);
    }

    public static CorreoDePruebaDto ADto(this CorreoDePrueba c) => new(c.Id, c.Para, c.Asunto, c.Texto, c.Html, c.Enlaces, c.RecibidoEn);
}
