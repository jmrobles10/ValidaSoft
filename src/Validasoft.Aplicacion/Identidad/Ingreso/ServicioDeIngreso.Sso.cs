using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Wolverine;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>Cookie <c>vs_sso</c> (PI-3): liga el regreso del proveedor de identidad al navegador que empezó el inicio de sesión. <c>HttpOnly; SameSite=Lax</c> (el regreso es una navegación de nivel superior desde otro sitio) y <c>Path</c> solo del <i>callback</i>.</summary>
public sealed class CookieDeSso(IConfiguration configuracion)
{
    public const string Nombre = "vs_sso";
    public const string Ruta = "/api/v1/ingreso/sso";

    public bool Segura =>
        !bool.TryParse(configuracion["Validasoft:Ingreso:CookieSegura"] ?? configuracion["Validasoft:SesionTercero:CookieSegura"], out var segura) || segura;

    public void Poner(HttpResponse respuesta, string secreto, DateTimeOffset expira) =>
        respuesta.Cookies.Append(Nombre, secreto, Opciones(expira));

    public void Borrar(HttpResponse respuesta) => respuesta.Cookies.Delete(Nombre, Opciones(null));

    public static string? Leer(HttpRequest peticion) =>
        peticion.Cookies.TryGetValue(Nombre, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor : null;

    private CookieOptions Opciones(DateTimeOffset? expira) => new()
    {
        HttpOnly = true,
        Secure = Segura,
        SameSite = SameSiteMode.Lax,
        Path = Ruta,
        Expires = expira,
        IsEssential = true
    };
}

/// <summary>
/// Inicio de sesión con el proveedor de identidad del cliente (SSO, PI-1…PI-6): descubrir si el correo es de un dominio con SSO, empezar (el navegador va al
/// proveedor), atender el regreso (<i>callback</i>: canje del código y validación, en el servidor) y completar el ingreso en la app, con el aprovisionamiento JIT
/// y los roles por los grupos del proveedor. De ahí en adelante sigue como cualquier ingreso directo: cuenta, passkey donde se exija y la misma sesión.
/// </summary>
public sealed partial class ServicioDeIngreso
{
    // ---------- descubrir y empezar ----------

    /// <summary>Si el correo es de un dominio verificado con el SSO activo: cómo se llama y si reemplaza al código. Nada más: no dice si el correo tiene cuenta.</summary>
    public async Task<SsoDescubiertoDto> DescubrirSsoAsync(string? correo, CancellationToken ct)
    {
        var normalizado = NormalizarCorreo(correo);
        return await sso.DeCorreoAsync(normalizado, ct) is { } configuracion && await AdmiteDirectoAsync(configuracion.Id, ct)
            ? new SsoDescubiertoDto(true, configuracion.Nombre, configuracion.ReemplazaElCodigoPara(normalizado))
            : new SsoDescubiertoDto(false, null, false);
    }

    /// <summary>Empieza el inicio de sesión con el proveedor: devuelve la dirección a la que va el navegador y deja la cookie que liga el regreso a este navegador.</summary>
    public async Task<SsoIniciadoDto> IniciarSsoAsync(IniciarSsoDto dto, ContextoDeCliente cliente, IMessageBus mensajes, HttpResponse respuesta, CancellationToken ct)
    {
        var correo = NormalizarCorreo(dto.Correo);
        var configuracion = await sso.DeCorreoAsync(correo, ct);
        if (configuracion is null || !await AdmiteDirectoAsync(configuracion.Id, ct))
            throw ReglaDeNegocioException.Prohibido("sso-no-disponible", "Tu correo no tiene un inicio de sesión de empresa. Usa el código que te enviamos por correo.");

        var proveedor = proveedores.FirstOrDefault(p => p.Id == configuracion.Proveedor)
                        ?? throw ReglaDeNegocioException.NoDisponible("sso-no-disponible", "El inicio de sesión de tu empresa no está disponible en este momento.");
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global(mensajes);
        var inicio = await proveedor.IniciarAsync(sesion, mensajes, correo, conCuenta: true, cliente, ahora, ct);
        await sesion.SaveChangesAsync(ct);
        cookieSso.Poner(respuesta, inicio.SecretoDeNavegador!, inicio.Expira);
        return new SsoIniciadoDto(inicio.Desafio, inicio.UrlDeAutorizacion!);
    }

    // ---------- regreso del proveedor ----------

    /// <summary>
    /// Atiende el regreso del proveedor (<c>GET /ingreso/sso/callback</c>): comprueba el <c>state</c> y el navegador, canjea el código y valida la identidad en el
    /// servidor, y guarda lo que declaró el proveedor en el desafío. Devuelve siempre una dirección de la app a la que redirigir: con <c>?sso={desafio}</c> si salió
    /// bien o <c>?sso_error={código}</c> si no (el navegador nunca ve un error crudo ni un código del proveedor).
    /// </summary>
    public async Task<string> RetornoDeSsoAsync(string? codigo, string? estado, string? error, string? cookieDeNavegador, ContextoDeCliente cliente, HttpResponse respuesta,
        CancellationToken ct)
    {
        cookieSso.Borrar(respuesta);
        string Hacia(string consulta) => $"{opcionesDeSso.UrlDeLaApp}#/ingreso?{consulta}";

        var partes = (estado ?? string.Empty).Split('.', 2);
        if (!Guid.TryParseExact(partes[0], "N", out var id))
            return Hacia("sso_error=sso-estado-invalido");

        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var desafio = await sesion.LoadAsync<DesafioDeIngreso>(id, ct);
        if (desafio?.Sso is null)
            return Hacia("sso_error=sso-estado-invalido");

        var tenantId = desafio.Sso.TenantId;
        try
        {
            desafio.VerificarRetornoSso(estado, cookieDeNavegador, ahora);
            if (!string.IsNullOrEmpty(error))
                throw ReglaDeNegocioException.NoAutenticado("sso-cancelado", "El inicio de sesión de tu empresa se canceló o no lo autorizaron.");
            if (string.IsNullOrWhiteSpace(codigo))
                throw ReglaDeNegocioException.NoAutenticado("sso-sin-codigo", "El proveedor no devolvió el código de autorización.");

            var proveedor = proveedores.FirstOrDefault(p => p.Id == desafio.Sso.Proveedor)
                            ?? throw ReglaDeNegocioException.NoDisponible("sso-no-disponible", "El inicio de sesión de tu empresa no está disponible.");
            var identidad = await proveedor.CompletarAsync(desafio, codigo, ahora, ct)
                            ?? throw ReglaDeNegocioException.NoAutenticado("sso-token-invalido", "El proveedor no pudo verificar tu identidad.");

            // Solo vale un correo de un dominio verificado de este grupo: el proveedor de una empresa no puede dar la identidad de personas de otras.
            var configuracion = await sso.DeGrupoAsync(tenantId, ct);
            if (configuracion is not { Activo: true } || !configuracion.CubreCorreo(identidad.Correo))
                throw ReglaDeNegocioException.Prohibido("sso-dominio-no-verificado", "Ese correo no es de un dominio verificado de tu grupo.");

            desafio.RegistrarIdentidadSso(identidad.Correo, identidad.Nombre, identidad.Sujeto, identidad.Grupos ?? [], identidad.GruposDeclarados, identidad.EsMultifactor, ahora);
            sesion.Store(desafio);
            await sesion.SaveChangesAsync(ct);
            return Hacia($"sso={id}");
        }
        catch (ReglaDeNegocioException e)
        {
            await FallarSsoAsync(sesion, desafio, tenantId, e.Codigo, cliente, ct);
            return Hacia($"sso_error={Uri.EscapeDataString(e.Codigo)}");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            await FallarSsoAsync(sesion, desafio, tenantId, "sso-error-interno", cliente, ct);
            return Hacia("sso_error=sso-error-interno");
        }
    }

    private async Task FallarSsoAsync(IDocumentSession sesion, DesafioDeIngreso desafio, Guid tenantId, string motivo, ContextoDeCliente cliente, CancellationToken ct)
    {
        // Un regreso que no pasó el estado ni el navegador no gasta el desafío (no es de quien lo empezó); cualquier otro fallo sí.
        if (motivo is not ("sso-estado-invalido" or "desafio-usado" or "desafio-vencido") && desafio.Etapa == EtapaDeIngreso.SsoPendiente)
        {
            desafio.Anular();
            sesion.Store(desafio);
            try
            {
                await sesion.SaveChangesAsync(ct);
            }
            catch (Exception e) when (Peticiones.EsCarrera(e))
            {
                // Otro regreso del mismo inicio ganó: no hay nada más que anular.
            }
        }

        await registro.RegistrarAsync(EventoDeIngreso.IngresoFallido, false, cliente, tenantId, correo: desafio.Correo, motivo: motivo, ct: ct);
    }

    // ---------- completar en la app ----------

    /// <summary>
    /// Completa el ingreso con la identidad que el proveedor ya verificó: encuentra a la persona en el grupo o la crea (aprovisionamiento JIT), pone al día sus roles
    /// con sus grupos del proveedor y sigue como cualquier ingreso (la sesión directa si el proveedor declaró varios factores; si no, la passkey donde se exija).
    /// 403 <c>sso-sin-cuenta</c> (sin JIT y sin cuenta), <c>cuenta-no-disponible</c>, <c>modo-de-ingreso-no-admitido</c>.
    /// </summary>
    public async Task<PasoDeIngresoDto> CompletarSsoAsync(CompletarSsoDto dto, ContextoDeCliente cliente, HttpResponse respuesta, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var desafio = await CargarAsync(sesion, dto.Desafio, ct);
        if (desafio.Etapa != EtapaDeIngreso.SsoVerificado || desafio.Sso?.Correo is null)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este inicio de sesión ya se usó o no terminó. Empieza de nuevo.");
        desafio.AsegurarVigente(ahora);

        var identidad = desafio.Sso;
        try
        {
            var configuracion = await sso.DeGrupoAsync(identidad.TenantId, ct);
            if (configuracion is not { Activo: true } || !configuracion.CubreCorreo(identidad.Correo!))
                throw ReglaDeNegocioException.Prohibido("sso-no-disponible", "El inicio de sesión de tu empresa dejó de estar disponible.");
            await politica.AsegurarModoAsync(identidad.TenantId, ModoDeIngreso.Directo, ct);

            var usuario = await AprovisionarAsync(configuracion, identidad, cliente, ahora, ct);
            var resuelta = await cuentas.CargarAsync(new CuentaDeIngreso(TipoDePropietario.Usuario, identidad.TenantId, usuario.Id.ToString()), ct)
                           ?? throw ReglaDeNegocioException.Prohibido("cuenta-no-disponible", "Tu cuenta ya no puede ingresar directo. Pide ayuda a un administrador.");
            desafio.ConfirmarSso(ahora);
            return await ContinuarAsync(sesion, desafio, resuelta, cliente, respuesta, ahora, ct);
        }
        catch (ReglaDeNegocioException e)
        {
            await registro.RegistrarAsync(EventoDeIngreso.IngresoFallido, false, cliente, identidad.TenantId, correo: identidad.Correo, motivo: e.Codigo, ct: ct);
            throw;
        }
    }

    private async Task<bool> AdmiteDirectoAsync(Guid tenantId, CancellationToken ct) =>
        await politica.TenantAsync(tenantId, ct) is { Activo: true } tenant && tenant.ModosDeIngreso.HasFlag(ModoDeIngreso.Directo);

    /// <summary>
    /// La persona del grupo con ese correo, creada si falta y el grupo aprovisiona (JIT), con los roles de sus grupos del proveedor: al crearla, los de las reglas
    /// que coincidan o, si ninguna, los por omisión; en cada ingreso siguiente, si el grupo sincroniza roles y el token trajo el reclamo de grupos, se ponen al día.
    /// </summary>
    private async Task<Usuario> AprovisionarAsync(ConfiguracionDeSso configuracion, IngresoSso identidad, ContextoDeCliente cliente, DateTimeOffset ahora, CancellationToken ct)
    {
        var correo = identidad.Correo!;
        var tenantId = identidad.TenantId;
        await using var deTenant = sesiones.ParaTenant(tenantId);
        var usuario = await deTenant.Query<Usuario>().FirstOrDefaultAsync(u => u.Correo == correo, ct);

        if (usuario is null)
        {
            if (!configuracion.AprovisionamientoJit)
                throw ReglaDeNegocioException.Prohibido("sso-sin-cuenta", "Tu empresa aún no te dio acceso a Validasoft. Pide a un administrador de tu grupo que te invite.");

            var roles = configuracion.RolesPara(identidad.Grupos);
            var nombre = string.IsNullOrWhiteSpace(identidad.Nombre) ? correo[..correo.IndexOf('@', StringComparison.Ordinal)] : identidad.Nombre;
            usuario = Usuario.Crear(nombre, correo, null, ahora, tenantId);
            usuario.AsignarRoles(roles);
            deTenant.Store(usuario);
            try
            {
                await deTenant.SaveChangesAsync(ct);
            }
            catch (Exception e) when (Peticiones.EsCarrera(e))
            {
                // Dos ingresos simultáneos de la misma persona: gana el primero y este solo la lee.
                await using var lectura = sesiones.ParaTenant(tenantId);
                return await lectura.Query<Usuario>().FirstOrDefaultAsync(u => u.Correo == correo, ct)
                       ?? throw ReglaDeNegocioException.Prohibido("cuenta-no-disponible", "No pudimos crear tu cuenta. Intenta de nuevo.");
            }

            await registro.RegistrarAsync(EventoDeIngreso.UsuarioAprovisionadoPorSso, true, cliente, tenantId, TipoDePropietario.Usuario, usuario.Id.ToString(), correo,
                MetodoDeIngreso.Sso, detalle: DescribirRoles(roles), ct: ct);
            return usuario;
        }

        if (!usuario.Activo)
            throw ReglaDeNegocioException.Prohibido("cuenta-no-disponible", "Tu cuenta está desactivada. Pide ayuda a un administrador de tu grupo.");

        if (configuracion.SincronizarRoles && identidad.GruposDeclarados)
        {
            var roles = configuracion.RolesPara(identidad.Grupos);
            if (!usuario.Roles.ToHashSet().SetEquals(roles))
            {
                var antes = DescribirRoles(usuario.Roles);
                usuario.AsignarRoles(roles);
                deTenant.Store(usuario);
                await deTenant.SaveChangesAsync(ct);
                await registro.RegistrarAsync(EventoDeIngreso.RolesSincronizadosPorSso, true, cliente, tenantId, TipoDePropietario.Usuario, usuario.Id.ToString(), correo,
                    MetodoDeIngreso.Sso, detalle: $"{antes} → {DescribirRoles(roles)}", ct: ct);
            }
        }

        return usuario;
    }

    private static string DescribirRoles(IEnumerable<RolAsignado> roles)
    {
        var lista = roles.Select(r => r.EmpresaId is null ? r.Rol.ToString() : $"{r.Rol}@{r.EmpresaId:N}").Order(StringComparer.Ordinal).ToList();
        return lista.Count == 0 ? "sin roles" : string.Join(", ", lista);
    }

    // ---------- SSO exclusivo (PI-5) ----------

    /// <summary>Las cuentas del correo con las que se puede entrar con el código: sin las de un grupo cuyo SSO exclusivo cubre ese correo.</summary>
    private async Task<IReadOnlyList<CuentaResuelta>> PorCorreoParaCodigoAsync(string correo, CancellationToken ct)
    {
        var todas = await cuentas.PorCorreoAsync(correo, ct);
        var permitidas = new List<CuentaResuelta>(todas.Count);
        foreach (var cuenta in todas)
        {
            if (cuenta.Cuenta.TenantId is { } tenantId && await sso.DeGrupoAsync(tenantId, ct) is { } configuracion && configuracion.ReemplazaElCodigoPara(correo))
                continue;
            permitidas.Add(cuenta);
        }

        return permitidas;
    }

    /// <summary>Con el SSO exclusivo del grupo, la persona no entra sin pasar por el inicio de sesión de su empresa. 403 <c>sso-obligatorio</c>.</summary>
    private async Task AsegurarSinSsoExclusivoAsync(CuentaResuelta cuenta, CancellationToken ct)
    {
        if (cuenta.Cuenta.TenantId is { } tenantId && await sso.DeGrupoAsync(tenantId, ct) is { } configuracion && configuracion.ReemplazaElCodigoPara(cuenta.Correo))
            throw ReglaDeNegocioException.Prohibido("sso-obligatorio", $"Tu grupo ingresa con el inicio de sesión de su empresa ({configuracion.Nombre}). Usa «Ingresar con {configuracion.Nombre}».");
    }

    /// <summary>La sesión directa tras un inicio de sesión de empresa con varios factores: no pide passkey, ni siquiera a los roles que la exigen.</summary>
    private async Task<PasoDeIngresoDto> AbrirSesionDeSsoAsync(IDocumentSession sesion, DesafioDeIngreso desafio, CuentaResuelta cuenta, ContextoDeCliente cliente,
        HttpResponse respuesta, DateTimeOffset ahora, CancellationToken ct)
    {
        desafio.Consumir(ahora);
        sesion.Store(desafio);
        var abierta = AbrirSesion(sesion, cuenta, MetodoDeIngreso.Sso, cliente, respuesta, ahora);
        await sesion.SaveChangesAsync(ct);
        await RegistrarUltimoAccesoAsync(cuenta, ahora, ct);
        return new PasoDeIngresoDto("Sesion", desafio.Id, Sesion: abierta);
    }
}
