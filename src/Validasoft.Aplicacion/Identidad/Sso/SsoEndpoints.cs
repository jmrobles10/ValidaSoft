using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>
/// Inicio de sesión con el proveedor de identidad del cliente, lado de la pantalla de ingreso (SSO, PI-3): anónimo y con el límite por IP de
/// <c>/api/v1/ingreso</c>. El navegador nunca ve el código del proveedor: el canje y la validación ocurren en el servidor, en el <i>callback</i>.
/// </summary>
public static class SsoIngresoEndpoints
{
    /// <summary>Si el correo es de un dominio verificado con el SSO activo. Es lo único que dice: no revela si el correo tiene cuenta.</summary>
    [WolverinePost("/api/v1/ingreso/sso/descubrir")]
    [AllowAnonymous]
    public static Task<SsoDescubiertoDto> Descubrir(DescubrirSsoDto dto, ServicioDeIngreso servicio, CancellationToken ct) =>
        servicio.DescubrirSsoAsync(dto.Correo, ct);

    /// <summary>
    /// Empieza el inicio de sesión de la empresa: responde la dirección del proveedor (con <c>state</c>, <c>nonce</c> y PKCE) y deja una cookie corta que liga el
    /// regreso a este navegador. 403 <c>sso-no-disponible</c> si el correo no es de un dominio con SSO; 503 si el proveedor no responde.
    /// </summary>
    [WolverinePost("/api/v1/ingreso/sso/iniciar")]
    [AllowAnonymous]
    public static Task<SsoIniciadoDto> Iniciar(IniciarSsoDto dto, HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio, IMessageBus mensajes,
        CancellationToken ct) =>
        servicio.IniciarSsoAsync(dto, ContextoDeCliente.De(http, contexto.CorrelationId), mensajes, http.Response, ct);

    /// <summary>
    /// Regreso del proveedor (la URI de redirección que se registra en él): canjea el código, valida la identidad y redirige a la app —con <c>?sso={desafio}</c> si
    /// salió bien o <c>?sso_error={código}</c> si no—. Siempre responde 302 a la app.
    /// </summary>
    [WolverineGet("/api/v1/ingreso/sso/callback")]
    [AllowAnonymous]
    public static async Task<IResult> Retorno(string? code, string? state, string? error, HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio,
        CancellationToken ct) =>
        Results.Redirect(await servicio.RetornoDeSsoAsync(code, state, error, CookieDeSso.Leer(http.Request), ContextoDeCliente.De(http, contexto.CorrelationId), http.Response, ct));

    /// <summary>
    /// Completa el ingreso con la identidad ya verificada: crea a la persona si el grupo aprovisiona (JIT), pone al día sus roles con sus grupos del proveedor y
    /// devuelve el siguiente paso, como el código del correo (<c>Sesion</c>, <c>PasskeyRequerida</c> o <c>RegistrarPasskey</c>). 403 <c>sso-sin-cuenta</c>,
    /// <c>cuenta-no-disponible</c>, <c>modo-de-ingreso-no-admitido</c>.
    /// </summary>
    [WolverinePost("/api/v1/ingreso/sso/completar")]
    [AllowAnonymous]
    public static Task<PasoDeIngresoDto> Completar(CompletarSsoDto dto, HttpContext http, IContextoSolicitud contexto, ServicioDeIngreso servicio, CancellationToken ct) =>
        servicio.CompletarSsoAsync(dto, ContextoDeCliente.De(http, contexto.CorrelationId), http.Response, ct);
}

/// <summary>
/// Configuración del SSO del grupo (<c>usuarios:administrar</c>, PI-1…PI-5): proveedor, dominios verificados, aprovisionamiento JIT, reglas de grupos a roles y si
/// convive con el código por correo o lo reemplaza. Cambiar el SSO es una acción sensible: quien entró directo la confirma con su passkey (PI-13) y todo queda en
/// la auditoría de configuración (sin el secreto del cliente).
/// </summary>
public static class SsoConfiguracionEndpoints
{
    public const string Entidad = "SsoDelGrupo";

    [WolverineGet("/api/v1/configuracion/sso")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<ConfiguracionDeSsoDto> Obtener(ISesiones sesiones, IContextoSolicitud contexto, OpcionesDeSso opciones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var configuracion = await consulta.LoadAsync<ConfiguracionDeSso>(contexto.TenantRequerido(), ct);
        return (configuracion ?? ConfiguracionDeSso.Nueva(contexto.TenantRequerido(), default)).ADto(opciones, configuracion is not null);
    }

    /// <summary>
    /// Crea o reemplaza el SSO del grupo. <c>secretoDelCliente</c> nulo conserva el actual y vacío lo quita; nunca vuelve. 409 <c>ingreso-directo-apagado</c> (el SSO
    /// abre sesiones directas), <c>workos-no-configurado</c>; 422 <c>proveedor-sso-invalido</c>, <c>autoridad-invalida</c>, <c>client-id-requerido</c>,
    /// <c>sso-sin-dominios</c>, <c>rol-por-omision-requerido</c>, <c>regla-sso-invalida</c>, <c>empresas-invalidas</c>…
    /// </summary>
    [WolverinePut("/api/v1/configuracion/sso")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<ConfiguracionDeSsoDto> Fijar(FijarSsoDto dto, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ConfiguracionesDeSso configuraciones,
        PoliticaDeIngreso politica, IProtectorDeSecretos protector, OpcionesDeSso opciones, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "configurar-sso", ct);
        var tenantId = contexto.TenantRequerido();
        var ahora = reloj.GetUtcNow();

        await using var sesion = sesiones.ParaTenant(tenantId);
        var tenant = await sesion.LoadAsync<Tenant>(tenantId, ct) ?? throw ReglaDeNegocioException.NoEncontrado("tenant-no-encontrado", "No existe ese grupo.");
        if (dto.Activo && !tenant.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo))
            throw ReglaDeNegocioException.Conflicto("ingreso-directo-apagado", "El inicio de sesión de la empresa abre sesiones directas: activa el modo directo en Configuración › Ingreso.");
        if (string.Equals(dto.Proveedor, ProveedoresSso.WorkOs, StringComparison.OrdinalIgnoreCase) && !opciones.WorkOsDisponible)
            throw ReglaDeNegocioException.Conflicto("workos-no-configurado", "Esta instalación no tiene configurado el adaptador de WorkOS.");

        var roles = (dto.RolesPorOmision ?? []).Select(r => r.ADominio()).ToList();
        var reglas = (dto.Reglas ?? []).Select(AReglaDeDominio).ToList();
        var empresasUsadas = roles.Concat(reglas.Select(r => new RolAsignado(r.Rol, r.EmpresaId))).Where(r => r.EmpresaId is not null).Select(r => r.EmpresaId!.Value).Distinct().ToList();
        if (empresasUsadas.Count > 0)
        {
            await using var consulta = sesiones.ConsultaGlobal();
            var propias = await consulta.Query<Empresa>().Where(e => e.TenantId == tenantId).Select(e => e.Id).ToListAsync(ct);
            if (empresasUsadas.Any(e => !propias.Contains(e)))
                throw ReglaDeNegocioException.Validacion("empresas-invalidas", "Alguna empresa de los roles no pertenece al grupo.");
        }

        var existente = await sesion.LoadAsync<ConfiguracionDeSso>(tenantId, ct);
        var configuracion = existente ?? ConfiguracionDeSso.Nueva(tenantId, ahora);
        var antes = existente?.ADto(opciones, true);
        configuracion.Actualizar(new CambiosDeSso(dto.Activo, dto.Proveedor, dto.Nombre, dto.Autoridad, dto.ClientId, dto.SecretoDelCliente, dto.Alcances, dto.ReclamoDeGrupos,
            dto.OrganizacionWorkOs, dto.ConexionWorkOs, dto.AprovisionamientoJit, roles, reglas, dto.SincronizarRoles,
            Enum.TryParse<ModoDeSso>(dto.Modo, ignoreCase: true, out var modo) ? modo : throw ReglaDeNegocioException.Validacion("modo-sso-invalido", "El modo debe ser Complementario o Exclusivo.")),
            protector.Proteger, opciones.PermitirHttpLocal, ahora);

        sesion.Store(configuracion);
        Auditoria.Registrar(sesion, contexto, Entidad, tenantId.ToString(), existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes,
            configuracion.ADto(opciones, true), ahora);
        await sesion.SaveChangesAsync(ct);
        configuraciones.Olvidar(tenantId, configuracion.Dominios.Select(d => d.Dominio));
        politica.Olvidar(tenantId);
        return configuracion.ADto(opciones, true);
    }

    /// <summary>
    /// Registra un dominio de correo del grupo y devuelve lo que hay que publicar para demostrar que es suyo. Hasta que se verifica no sirve para iniciar sesión.
    /// 422 <c>dominio-invalido</c>; 409 <c>dominio-publico</c> (gmail.com y similares no son de nadie).
    /// </summary>
    [WolverinePost("/api/v1/configuracion/sso/dominios")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<DominioDeSsoDto> AgregarDominio(AgregarDominioSsoDto dto, ISesiones sesiones, IContextoSolicitud contexto, ConfiguracionesDeSso configuraciones,
        TimeProvider reloj, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.ParaTenant(tenantId);
        var existente = await sesion.LoadAsync<ConfiguracionDeSso>(tenantId, ct);
        var configuracion = existente ?? ConfiguracionDeSso.Nueva(tenantId, ahora);
        var antes = existente?.Dominios.Select(d => d.Dominio).ToList();
        var dominio = configuracion.AgregarDominio(dto.Dominio, ahora);
        sesion.Store(configuracion);
        if (antes is null || !antes.Contains(dominio.Dominio))
            Auditoria.Registrar(sesion, contexto, Entidad, tenantId.ToString(), AccionConfiguracion.Modificado, antes, configuracion.Dominios.Select(d => d.Dominio).ToList(), ahora);
        await sesion.SaveChangesAsync(ct);
        configuraciones.Olvidar(tenantId, [dominio.Dominio]);
        return dominio.ADto();
    }

    /// <summary>
    /// Comprueba que el grupo controla el dominio (busca el token en <c>https://{dominio}/.well-known/validasoft-verificacion.txt</c>) y lo da por verificado. Un dominio
    /// verificado es de un solo grupo. 409 <c>dominio-no-verificado</c> (aún no se ve el token), <c>dominio-en-uso</c> (otro grupo ya lo verificó); 404
    /// <c>dominio-no-encontrado</c>.
    /// </summary>
    [WolverinePost("/api/v1/configuracion/sso/dominios/{dominio}/verificar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<DominioDeSsoDto> VerificarDominio(string dominio, ISesiones sesiones, IContextoSolicitud contexto, ConfiguracionesDeSso configuraciones,
        IComprobadorDeDominios comprobador, TimeProvider reloj, CancellationToken ct)
    {
        var tenantId = contexto.TenantRequerido();
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.ParaTenant(tenantId);
        var configuracion = await sesion.LoadAsync<ConfiguracionDeSso>(tenantId, ct) ?? throw ReglaDeNegocioException.NoEncontrado("dominio-no-encontrado", "Ese dominio no está registrado en el grupo.");
        var buscado = configuracion.DominioRequerido(dominio);
        if (buscado.Verificado)
            return buscado.ADto();

        if (!await comprobador.ComprobarAsync(buscado.Dominio, buscado.Token, ct))
            throw ReglaDeNegocioException.Conflicto("dominio-no-verificado",
                $"No encontramos el texto en https://{buscado.Dominio}{ComprobadorDeRuta.Ruta}. Publícalo tal cual (una sola línea) y vuelve a verificar.");

        var dueno = await sesion.LoadAsync<DominioVerificadoDeSso>(buscado.Dominio, ct);
        if (dueno is not null && dueno.TenantId != tenantId)
            throw ReglaDeNegocioException.Conflicto("dominio-en-uso", "Otro grupo ya verificó ese dominio. Si es de tu empresa, escribe a soporte.");
        if (dueno is null)
            sesion.Insert(new DominioVerificadoDeSso { Id = buscado.Dominio, TenantId = tenantId, VerificadoEn = ahora });

        var antes = buscado.ADto();
        configuracion.MarcarVerificado(buscado, ahora);
        sesion.Store(configuracion);
        Auditoria.Registrar(sesion, contexto, Entidad, tenantId.ToString(), AccionConfiguracion.Modificado, antes, buscado.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);
        configuraciones.Olvidar(tenantId, [buscado.Dominio]);
        return buscado.ADto();
    }

    /// <summary>Quita el dominio del grupo (y su verificación). Con el SSO activo no puede quedar sin ningún dominio verificado: 409 <c>sso-sin-dominios</c>.</summary>
    [WolverineDelete("/api/v1/configuracion/sso/dominios/{dominio}")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<IResult> QuitarDominio(string dominio, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, ConfiguracionesDeSso configuraciones,
        ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "configurar-sso", ct);
        var tenantId = contexto.TenantRequerido();
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.ParaTenant(tenantId);
        var configuracion = await sesion.LoadAsync<ConfiguracionDeSso>(tenantId, ct) ?? throw ReglaDeNegocioException.NoEncontrado("dominio-no-encontrado", "Ese dominio no está registrado en el grupo.");
        var buscado = configuracion.DominioRequerido(dominio);
        var antes = configuracion.Dominios.Select(d => d.Dominio).ToList();
        configuracion.QuitarDominio(dominio, ahora);
        if (await sesion.LoadAsync<DominioVerificadoDeSso>(buscado.Dominio, ct) is { } verificado && verificado.TenantId == tenantId)
            sesion.Delete(verificado);
        sesion.Store(configuracion);
        Auditoria.Registrar(sesion, contexto, Entidad, tenantId.ToString(), AccionConfiguracion.Modificado, antes, configuracion.Dominios.Select(d => d.Dominio).ToList(), ahora);
        await sesion.SaveChangesAsync(ct);
        configuraciones.Olvidar(tenantId, [buscado.Dominio]);
        return Results.NoContent();
    }

    /// <summary>
    /// «Probar conexión»: con lo ya guardado, comprueba que el proveedor responde y publica su descubrimiento con el emisor esperado. No inicia sesión ni cambia nada;
    /// responde siempre 200 con el resultado. 404 <c>sso-no-configurado</c>.
    /// </summary>
    [WolverinePost("/api/v1/configuracion/sso/probar")]
    [Authorize(Policy = Permisos.UsuariosAdministrar)]
    public static async Task<PruebaDeSsoDto> Probar(ISesiones sesiones, IContextoSolicitud contexto, IEnumerable<IProveedorDeIdentidad> proveedores, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var configuracion = await consulta.LoadAsync<ConfiguracionDeSso>(contexto.TenantRequerido(), ct)
                            ?? throw ReglaDeNegocioException.NoEncontrado("sso-no-configurado", "Este grupo aún no configuró el inicio de sesión de su empresa.");
        var proveedor = proveedores.FirstOrDefault(p => p.Id == configuracion.Proveedor);
        if (proveedor is null)
            return new PruebaDeSsoDto(false, "Este proveedor no está disponible en esta instalación.", null, null);
        try
        {
            return await proveedor.ProbarAsync(configuracion, ct);
        }
        catch (ReglaDeNegocioException e)
        {
            return new PruebaDeSsoDto(false, e.Message, null, null);
        }
    }

    /// <summary>Desactiva el SSO de un grupo desde Plataforma (válvula de emergencia: si el proveedor del cliente no responde y su administrador no puede entrar).</summary>
    [WolverinePost("/api/v1/plataforma/tenants/{id}/sso/desactivar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ConfiguracionDeSsoDto> DesactivarDesdePlataforma(Guid id, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto,
        ConfiguracionesDeSso configuraciones, PoliticaDeIngreso politica, OpcionesDeSso opciones, ServicioDeIngreso servicio, TimeProvider reloj, CancellationToken ct)
    {
        await servicio.ExigirReautenticacionAsync(http.User, "desactivar-sso", ct);
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.ParaTenant(id);
        var configuracion = await sesion.LoadAsync<ConfiguracionDeSso>(id, ct) ?? throw ReglaDeNegocioException.NoEncontrado("sso-no-configurado", "Este grupo no tiene SSO.");
        if (!configuracion.Activo)
            throw ReglaDeNegocioException.Conflicto("sso-inactivo", "El SSO de este grupo ya está desactivado.");

        var antes = configuracion.ADto(opciones, true);
        configuracion.Activo = false;
        configuracion.ActualizadoEn = ahora;
        sesion.Store(configuracion);
        Auditoria.Registrar(sesion, contexto, Entidad, id.ToString(), AccionConfiguracion.Desactivado, antes, configuracion.ADto(opciones, true), ahora);
        await sesion.SaveChangesAsync(ct);
        configuraciones.Olvidar(id, configuracion.Dominios.Select(d => d.Dominio));
        politica.Olvidar(id);
        return configuracion.ADto(opciones, true);
    }

    private static ReglaDeGrupoSso AReglaDeDominio(ReglaDeGrupoSsoDto r) =>
        new()
        {
            Grupo = r.Grupo,
            Rol = Enum.TryParse<Rol>(r.Rol, ignoreCase: true, out var rol) ? rol : throw ReglaDeNegocioException.Validacion("regla-sso-invalida", $"«{r.Rol}» no es un rol de Validasoft."),
            EmpresaId = r.EmpresaId
        };

    public static ConfiguracionDeSsoDto ADto(this ConfiguracionDeSso c, OpcionesDeSso opciones, bool configurado) =>
        new(configurado, c.Activo, c.Proveedor, c.Nombre, c.Autoridad, c.ClientId, c.LlaveConfigurada, c.LlaveConfigurada ? c.UltimosCuatroSecreto : null, c.Alcances,
            c.ReclamoDeGrupos, c.OrganizacionWorkOs, c.ConexionWorkOs, [.. c.Dominios.Select(d => d.ADto())], c.AprovisionamientoJit,
            [.. c.RolesPorOmision.Select(r => r.ADto())], [.. c.Reglas.Select(r => new ReglaDeGrupoSsoDto(r.Grupo, r.Rol.ToString(), r.EmpresaId))], c.SincronizarRoles,
            c.Modo.ToString(), opciones.UrlDeRetorno, opciones.WorkOsDisponible, configurado ? c.ActualizadoEn : null);

    public static DominioDeSsoDto ADto(this DominioDeSso d) =>
        new(d.Dominio, d.Verificado, d.Verificado ? null : d.Token,
            d.Verificado ? null : $"Publica este texto, tal cual y en una sola línea, en https://{d.Dominio}{ComprobadorDeRuta.Ruta} y pulsa «Verificar»: {d.Token}", d.CreadoEn, d.VerificadoEn);
}

/// <summary>La ruta con la que se demuestra el dominio (la usa el comprobador real y los textos de ayuda).</summary>
public static class ComprobadorDeRuta
{
    public const string Ruta = "/.well-known/validasoft-verificacion.txt";
}
