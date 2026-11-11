using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Wolverine;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>
/// Cookie <c>vs_usuario</c> con el token de refresco de la sesión directa (ING-1): <c>HttpOnly; SameSite=Strict; Path=/api/v1/ingreso/sesion</c> y
/// <c>Secure</c> salvo que <c>Validasoft:Ingreso:CookieSegura</c> (o, si falta, <c>Validasoft:SesionTercero:CookieSegura</c>) sea falso. Vence con la
/// sesión (el máximo del grupo); el navegador solo la envía a renovar y cerrar.
/// </summary>
public sealed class CookieDeSesionDeUsuario(IConfiguration configuracion)
{
    public const string Nombre = "vs_usuario";
    public const string Ruta = "/api/v1/ingreso/sesion";

    public bool Segura =>
        !bool.TryParse(configuracion["Validasoft:Ingreso:CookieSegura"] ?? configuracion["Validasoft:SesionTercero:CookieSegura"], out var segura) || segura;

    public void Poner(HttpResponse respuesta, string token, DateTimeOffset expira) => respuesta.Cookies.Append(Nombre, token, Opciones(expira));

    public void Borrar(HttpResponse respuesta) => respuesta.Cookies.Delete(Nombre, Opciones(null));

    public static string? Leer(HttpRequest peticion) =>
        peticion.Cookies.TryGetValue(Nombre, out var valor) && !string.IsNullOrWhiteSpace(valor) ? valor : null;

    private CookieOptions Opciones(DateTimeOffset? expira) => new()
    {
        HttpOnly = true,
        Secure = Segura,
        SameSite = SameSiteMode.Strict,
        Path = Ruta,
        Expires = expira,
        IsEssential = true
    };
}

/// <summary>
/// Proveedor de identidad propio (<c>local</c>, ING-7): código de 6 dígitos de un solo uso enviado al correo por el outbox (cifrado en tránsito
/// hasta el SMTP, como el del tercero), de 5 minutos y 5 intentos. Un correo sin cuenta recibe un desafío señuelo y no se envía nada.
/// </summary>
public sealed class ProveedorLocalDeCodigos(IProtectorDeSecretos protector) : IProveedorDeIdentidad
{
    public const string Codigo = "local";

    public const string MensajeEnviado = "Si ese correo tiene una cuenta, te enviamos un código de 6 dígitos. Vence en 5 minutos.";

    public string Id => Codigo;

    public async Task<InicioDeIngreso> IniciarAsync(IDocumentSession sesion, IMessageBus mensajes, string correo, bool conCuenta, ContextoDeCliente cliente,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var (desafio, codigo) = DesafioDeIngreso.ParaCorreo(correo, conCuenta, ahora, cliente.Ip, cliente.Dispositivo);
        sesion.Store(desafio);
        if (codigo is not null)
            await mensajes.PublishAsync(new CorreoDeAccesoPendiente(PlantillaCorreo.CodigoDeIngreso, desafio.Correo!, protector.Proteger(codigo), desafio.Expira, null, null));
        return new InicioDeIngreso(desafio.Id, desafio.Expira, MensajeEnviado);
    }

    public Task<IdentidadVerificada?> CompletarAsync(DesafioDeIngreso desafio, string respuesta, DateTimeOffset ahora, CancellationToken ct) =>
        Task.FromResult(desafio.VerificarCodigo(respuesta, ahora) ? new IdentidadVerificada(Codigo, desafio.Correo!, null, EsMultifactor: false) : null);
}

/// <summary>
/// Ingreso directo sin contraseñas (ING-1…ING-4, ING-7): correo → código → cuenta → passkey, o solo passkey. Decisiones: (1) la passkey
/// (credencial detectable con verificación de usuario obligatoria) vale por dos factores, así que con una passkey registrada se entra solo con
/// ella (o con el código y luego la passkey si se empezó por el correo); el código del correo es un solo factor y, si la cuenta tiene passkey o
/// su rol la exige, nunca abre sesión por sí solo; (2) sin passkey y con rol que la exige, el código solo sirve para registrar la primera; (3)
/// un correo sin cuenta (o de un grupo que no admite el modo directo) recibe la misma respuesta que uno con cuenta. La sesión es la misma que
/// emite la identidad delegada (JWT HS256 con los scopes efectivos del usuario) más un token de refresco en cookie.
/// </summary>
public sealed partial class ServicioDeIngreso(
    ISesiones sesiones, IEnumerable<IProveedorDeIdentidad> proveedores, CuentasDeIngreso cuentas, IServicioDePasskeys passkeys, IEmisorDeSesiones emisor,
    CookieDeSesionDeUsuario cookie, ICacheDeSesionesDirectas cache, RegistroDeIngresos registro, IConfiguration configuracion, TimeProvider reloj,
    ConfiguracionesDeSso sso, PoliticaDeIngreso politica, CookieDeSso cookieSso, OpcionesDeSso opcionesDeSso)
{
    /// <summary>El proveedor propio (código por correo); los federados (SSO) se eligen por la configuración del grupo.</summary>
    private IProveedorDeIdentidad Local => proveedores.First(p => p.Id == ProveedorLocalDeCodigos.Codigo);

    public const string CabeceraCliente = "X-Validasoft-Cliente";
    public const string ValorCliente = "web";

    // ---------- código ----------

    /// <summary>Pide un código. Siempre responde igual (y 422 <c>correo-invalido</c> solo por el formato), tenga o no cuenta el correo.</summary>
    public async Task<CodigoSolicitadoDto> SolicitarCodigoAsync(string? correo, ContextoDeCliente cliente, IMessageBus mensajes, CancellationToken ct)
    {
        var normalizado = NormalizarCorreo(correo);
        var ahora = reloj.GetUtcNow();
        var disponibles = await PorCorreoParaCodigoAsync(normalizado, ct);

        InicioDeIngreso inicio;
        try
        {
            inicio = await IniciarAsync(normalizado, disponibles, cliente, mensajes, ahora, ct);
        }
        catch (Exception e) when (Peticiones.EsCarrera(e))
        {
            // Dos solicitudes simultáneas del mismo correo: la perdedora no envía nada y responde igual (sin oráculo).
            if (mensajes is MessageContext contexto)
                await contexto.ClearAllAsync();
            await using var senuelo = sesiones.Global();
            inicio = await Local.IniciarAsync(senuelo, mensajes, normalizado, conCuenta: false, cliente, ahora, ct);
            await senuelo.SaveChangesAsync(ct);
        }

        return new CodigoSolicitadoDto(inicio.Desafio, inicio.Expira, inicio.Mensaje);
    }

    private async Task<InicioDeIngreso> IniciarAsync(string correo, IReadOnlyList<CuentaResuelta> disponibles, ContextoDeCliente cliente, IMessageBus mensajes,
        DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.Global(mensajes);
        var enviar = disponibles.Count > 0 && await RegistrarEnvioAsync(sesion, correo, ahora, ct);
        var inicio = await Local.IniciarAsync(sesion, mensajes, correo, enviar, cliente, ahora, ct);

        if (enviar)
        {
            foreach (var cuenta in disponibles)
                RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.CodigoEnviado, true, cliente, ahora, cuenta.Cuenta.TenantId,
                    cuenta.Cuenta.Tipo, cuenta.Cuenta.SujetoId, correo));
        }
        else
        {
            RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.IngresoFallido, false, cliente, ahora, correo: correo,
                motivo: disponibles.Count == 0 ? "correo-sin-cuenta" : "limite-de-envios"));
        }

        await sesion.SaveChangesAsync(ct);
        return inicio;
    }

    /// <summary>Cabe un código más para ese correo en la última hora (5); registra el envío.</summary>
    private static async Task<bool> RegistrarEnvioAsync(IDocumentSession sesion, string correo, DateTimeOffset ahora, CancellationToken ct)
    {
        var limite = await sesion.LoadAsync<LimiteDeAccesoPorCorreo>(LimiteDeAccesoPorCorreo.IdDe(correo, "ingreso"), ct);
        var esNuevo = limite is null;
        limite ??= LimiteDeAccesoPorCorreo.Nuevo(correo, "ingreso");
        if (!limite.IntentarRegistrar(ahora, LimiteDeAccesoPorCorreo.MaximoDeIngreso))
            return false;

        if (esNuevo)
            sesion.Insert(limite);
        else
            sesion.Store(limite);
        return true;
    }

    /// <summary>
    /// Verifica el código. Incorrecto ⇒ 422 <c>codigo-invalido</c> con <c>intentosRestantes</c>; vencido ⇒ 410 <c>codigo-vencido</c>; agotado ⇒ 429
    /// <c>codigo-bloqueado</c>. Correcto ⇒ el siguiente paso: elegir cuenta, verificar o registrar una passkey, o la sesión.
    /// </summary>
    public async Task<PasoDeIngresoDto> VerificarCodigoAsync(VerificarCodigoDto dto, ContextoDeCliente cliente, HttpResponse respuesta, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var desafio = await CargarAsync(sesion, dto.Desafio, ct);

        IdentidadVerificada? identidad;
        try
        {
            identidad = await Local.CompletarAsync(desafio, dto.Codigo, ahora, ct);
        }
        catch (ReglaDeNegocioException e)
        {
            await RegistrarFalloDeCodigoAsync(EventoDeIngreso.IngresoFallido, desafio.Correo, e.Codigo, cliente, ct);
            throw;
        }

        if (identidad is null)
        {
            // El intento fallido se guarda antes de responder; el desafío usa concurrencia optimista.
            sesion.Store(desafio);
            await sesion.SaveChangesAsync(ct);
            await RegistrarFalloDeCodigoAsync(EventoDeIngreso.CodigoIncorrecto, desafio.Correo, "codigo-incorrecto", cliente, ct);
            throw new ReglaDeNegocioException(TipoFalla.Validacion, "codigo-invalido", $"El código no es correcto. Te quedan {desafio.IntentosRestantes} intentos.")
            {
                Datos = new Dictionary<string, object?> { ["intentosRestantes"] = desafio.IntentosRestantes }
            };
        }

        var disponibles = await PorCorreoParaCodigoAsync(identidad.Correo, ct);
        if (disponibles.Count == 0)
        {
            await registro.RegistrarAsync(EventoDeIngreso.IngresoFallido, false, cliente, correo: identidad.Correo, motivo: "sin-cuenta", ct: ct);
            throw ReglaDeNegocioException.Validacion("sin-cuenta", "Ese correo ya no tiene una cuenta con ingreso directo. Pide ayuda a un administrador de tu grupo.");
        }

        if (disponibles.Count > 1)
        {
            sesion.Store(desafio);
            await sesion.SaveChangesAsync(ct);
            var lista = new List<CuentaDeIngresoDto>();
            foreach (var c in disponibles)
                lista.Add(c.ADto((await cuentas.PasskeysAsync(c.Cuenta, ct)).Count > 0));
            return new PasoDeIngresoDto("ElegirCuenta", desafio.Id, Cuentas: lista);
        }

        return await ContinuarAsync(sesion, desafio, disponibles[0], cliente, respuesta, ahora, ct);
    }

    /// <summary>
    /// Deja el fallo de un código en la auditoría de cada cuenta que tenga ese correo (así lo ve el administrador de su grupo); si el correo no
    /// tiene cuenta, queda sin grupo.
    /// </summary>
    private async Task RegistrarFalloDeCodigoAsync(EventoDeIngreso evento, string? correo, string motivo, ContextoDeCliente cliente, CancellationToken ct)
    {
        var propias = string.IsNullOrEmpty(correo) ? [] : await cuentas.PorCorreoAsync(correo, ct);
        if (propias.Count == 0)
        {
            await registro.RegistrarAsync(evento, false, cliente, correo: correo, motivo: motivo, ct: ct);
            return;
        }

        foreach (var cuenta in propias)
            await registro.RegistrarAsync(evento, false, cliente, cuenta.Cuenta.TenantId, cuenta.Cuenta.Tipo, cuenta.Cuenta.SujetoId, cuenta.Correo, motivo: motivo, ct: ct);
    }

    /// <summary>Con varias cuentas para el mismo correo, la persona elige con cuál entra.</summary>
    public async Task<PasoDeIngresoDto> ElegirCuentaAsync(ElegirCuentaDto dto, ContextoDeCliente cliente, HttpResponse respuesta, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var desafio = await CargarAsync(sesion, dto.Desafio, ct);
        // El inicio de sesión de una empresa solo entra a esa empresa: su proveedor demuestra la identidad dentro de sus dominios, no en otros grupos.
        if (desafio.Sso is not null)
            throw ReglaDeNegocioException.Prohibido("sso-cuenta-fija", "Este ingreso es del inicio de sesión de tu empresa: solo entra a su grupo.");
        desafio.AsegurarEtapa(EtapaDeIngreso.CodigoVerificado, ahora);
        if (desafio.Cuenta is not null)
            throw ReglaDeNegocioException.Conflicto("cuenta-ya-elegida", "Ya elegiste la cuenta de este ingreso.");

        var disponibles = await PorCorreoParaCodigoAsync(desafio.Correo ?? string.Empty, ct);
        var elegida = disponibles.FirstOrDefault(c => c.Id == dto.Cuenta)
                      ?? throw ReglaDeNegocioException.Validacion("cuenta-invalida", "Esa cuenta no corresponde a tu correo.");
        return await ContinuarAsync(sesion, desafio, elegida, cliente, respuesta, ahora, ct);
    }

    /// <summary>
    /// Con la cuenta ya elegida: si tiene passkey, pide verificarla; si la exige y no la tiene, pide registrar una; si no, abre la sesión con
    /// solo el código (un factor) y sugiere registrar una.
    /// </summary>
    private async Task<PasoDeIngresoDto> ContinuarAsync(IDocumentSession sesion, DesafioDeIngreso desafio, CuentaResuelta cuenta, ContextoDeCliente cliente,
        HttpResponse respuesta, DateTimeOffset ahora, CancellationToken ct)
    {
        desafio.ElegirCuenta(cuenta.Cuenta, ahora);

        // El proveedor de identidad del cliente declaró más de un factor (PI-3, PI-4): vale por sí solo, también para los roles que exigen passkey.
        if (desafio.Sso is { Multifactor: true })
            return await AbrirSesionDeSsoAsync(sesion, desafio, cuenta, cliente, respuesta, ahora, ct);

        var registradas = await cuentas.PasskeysAsync(cuenta.Cuenta, ct);

        if (registradas.Count > 0)
        {
            var opciones = passkeys.OpcionesDeAutenticacion([.. registradas.Select(p => p.CredencialId)]);
            desafio.GuardarOpciones(opciones, ahora);
            sesion.Store(desafio);
            await sesion.SaveChangesAsync(ct);
            return new PasoDeIngresoDto("PasskeyRequerida", desafio.Id, Opciones: MapeosDeIngreso.AJson(opciones));
        }

        if (cuenta.ExigePasskey)
        {
            var opciones = passkeys.OpcionesDeRegistro(ManejadorDe(cuenta.Cuenta), cuenta.Correo, cuenta.Nombre, []);
            desafio.GuardarOpciones(opciones, ahora);
            sesion.Store(desafio);
            await sesion.SaveChangesAsync(ct);
            return new PasoDeIngresoDto("RegistrarPasskey", desafio.Id, Opciones: MapeosDeIngreso.AJson(opciones));
        }

        desafio.Consumir(ahora);
        sesion.Store(desafio);
        var abierta = AbrirSesion(sesion, cuenta, desafio.Sso is not null ? MetodoDeIngreso.Sso : MetodoDeIngreso.Codigo, cliente, respuesta, ahora);
        await sesion.SaveChangesAsync(ct);
        await RegistrarUltimoAccesoAsync(cuenta, ahora, ct);
        return new PasoDeIngresoDto("Sesion", desafio.Id, Sesion: abierta);
    }

    // ---------- passkey ----------

    /// <summary>Opciones para entrar solo con passkey (credencial detectable: el navegador ofrece las del sitio, sin pedir el correo).</summary>
    public async Task<OpcionesDePasskeyDto> OpcionesDeIngresoAsync(ContextoDeCliente cliente, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var desafio = DesafioDeIngreso.ParaPasskey(ahora, cliente.Ip, cliente.Dispositivo);
        var opciones = passkeys.OpcionesDeAutenticacion(null);
        desafio.GuardarOpciones(opciones, ahora);
        await using var sesion = sesiones.Global();
        sesion.Store(desafio);
        await sesion.SaveChangesAsync(ct);
        return new OpcionesDePasskeyDto(desafio.Id, MapeosDeIngreso.AJson(opciones));
    }

    /// <summary>
    /// Verifica la aserción del navegador y abre la sesión: <c>Passkey</c> si se entró solo con ella, <c>CodigoYPasskey</c> si antes se verificó
    /// el código. 401 <c>passkey-invalida</c>, <c>passkey-revocada</c> o <c>passkey-clonada</c>; 403 <c>cuenta-no-disponible</c>.
    /// </summary>
    public async Task<PasoDeIngresoDto> VerificarPasskeyAsync(VerificarPasskeyDto dto, ContextoDeCliente cliente, HttpResponse respuesta, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var desafio = await CargarAsync(sesion, dto.Desafio, ct);
        // Un desafío de reautenticación (step-up) solo confirma una acción: nunca abre una sesión.
        if (desafio.EsReautenticacion || desafio.Etapa is not (EtapaDeIngreso.PasskeyPendiente or EtapaDeIngreso.CodigoVerificado) || desafio.OpcionesDePasskey is null)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este ingreso ya se usó o no espera una passkey. Empieza de nuevo.");
        desafio.AsegurarVigente(ahora);

        Passkey? usada = null;
        try
        {
            var aserto = await passkeys.VerificarAsertoAsync(desafio.OpcionesDePasskey, dto.Respuesta.GetRawText(),
                async (credencial, c) => usada = await sesion.Query<Passkey>().FirstOrDefaultAsync(p => p.CredencialId == credencial, c), ct);
            var passkey = aserto.Passkey;
            passkey.AsegurarActiva();
            var cuentaPasskey = new CuentaDeIngreso(passkey.Propietario, passkey.TenantId, passkey.SujetoId);
            if (desafio.Cuenta is not null && desafio.Cuenta != cuentaPasskey)
                throw ReglaDeNegocioException.NoAutenticado("passkey-invalida", "Esa passkey no es de la cuenta con la que empezaste el ingreso.");

            var cuenta = await cuentas.CargarAsync(cuentaPasskey, ct)
                         ?? throw ReglaDeNegocioException.Prohibido("cuenta-no-disponible", "Esa cuenta ya no puede ingresar directo. Pide ayuda a un administrador.");
            // Con el SSO exclusivo (PI-5) tampoco se entra solo con la passkey: la persona pasa antes por el inicio de sesión de su empresa.
            if (desafio.Sso is null)
                await AsegurarSinSsoExclusivoAsync(cuenta, ct);
            passkey.Usar(aserto.Contador, ahora, cliente.Ip);
            sesion.Store(passkey);
            desafio.Consumir(ahora);
            sesion.Store(desafio);

            var metodo = desafio.Sso is not null ? MetodoDeIngreso.SsoYPasskey : desafio.Cuenta is null ? MetodoDeIngreso.Passkey : MetodoDeIngreso.CodigoYPasskey;
            var abierta = AbrirSesion(sesion, cuenta, metodo, cliente, respuesta, ahora);
            await sesion.SaveChangesAsync(ct);
            await RegistrarUltimoAccesoAsync(cuenta, ahora, ct);
            return new PasoDeIngresoDto("Sesion", desafio.Id, Sesion: abierta);
        }
        catch (ReglaDeNegocioException e)
        {
            await registro.RegistrarAsync(EventoDeIngreso.IngresoFallido, false, cliente, usada?.TenantId, usada?.Propietario, usada?.SujetoId, desafio.Correo,
                motivo: e.Codigo, passkeyId: usada?.Id, ct: ct);
            throw;
        }
    }

    /// <summary>
    /// Registra la passkey de un ingreso que la exige y aún no tenía (alta o recuperación tras revocar la anterior) y abre la sesión
    /// (<c>CodigoYPasskey</c>). 401 <c>passkey-invalida</c>; 409 <c>passkey-duplicada</c>.
    /// </summary>
    public async Task<PasoDeIngresoDto> RegistrarPasskeyAsync(RegistrarPasskeyDto dto, ContextoDeCliente cliente, HttpResponse respuesta, IMessageBus mensajes,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var desafio = await CargarAsync(sesion, dto.Desafio, ct);
        desafio.AsegurarEtapa(EtapaDeIngreso.CodigoVerificado, ahora);
        var cuenta = desafio.Cuenta is null ? null : await cuentas.CargarAsync(desafio.Cuenta, ct);
        if (cuenta is null || desafio.OpcionesDePasskey is null)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este ingreso no espera registrar una passkey. Empieza de nuevo.");

        Passkey nueva;
        try
        {
            nueva = await RegistrarVerificadaAsync(sesion, desafio, cuenta, dto.Respuesta, dto.Nombre, cliente, ahora, ct);
        }
        catch (ReglaDeNegocioException e)
        {
            await registro.RegistrarAsync(EventoDeIngreso.IngresoFallido, false, cliente, cuenta.Cuenta.TenantId, cuenta.Cuenta.Tipo, cuenta.Cuenta.SujetoId,
                cuenta.Correo, motivo: e.Codigo, ct: ct);
            throw;
        }

        desafio.Consumir(ahora);
        sesion.Store(desafio);
        var abierta = AbrirSesion(sesion, cuenta, desafio.Sso is not null ? MetodoDeIngreso.SsoYPasskey : MetodoDeIngreso.CodigoYPasskey, cliente, respuesta, ahora);
        await sesion.SaveChangesAsync(ct);
        await RegistrarUltimoAccesoAsync(cuenta, ahora, ct);
        await AvisosDeSeguridad.PasskeyRegistradaAsync(mensajes, cuenta, nueva, cliente, ahora);
        return new PasoDeIngresoDto("Sesion", desafio.Id, Sesion: abierta);
    }

    /// <summary>Opciones para que una persona ya dentro registre otra passkey (Mi cuenta).</summary>
    public async Task<OpcionesDePasskeyDto> PrepararRegistroAsync(CuentaResuelta cuenta, ContextoDeCliente cliente, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var existentes = await cuentas.PasskeysAsync(cuenta.Cuenta, ct);
        if (existentes.Count >= Passkey.MaximoPorPropietario)
            throw ReglaDeNegocioException.Conflicto("passkeys-excesivas", $"Puedes tener hasta {Passkey.MaximoPorPropietario} passkeys: elimina alguna para registrar otra.");

        var desafio = DesafioDeIngreso.ParaRegistro(cuenta.Cuenta, cuenta.Correo, ahora, cliente.Ip, cliente.Dispositivo);
        var opciones = passkeys.OpcionesDeRegistro(ManejadorDe(cuenta.Cuenta), cuenta.Correo, cuenta.Nombre, [.. existentes.Select(p => p.CredencialId)]);
        desafio.GuardarOpciones(opciones, ahora);
        await using var sesion = sesiones.Global();
        sesion.Store(desafio);
        await sesion.SaveChangesAsync(ct);
        return new OpcionesDePasskeyDto(desafio.Id, MapeosDeIngreso.AJson(opciones));
    }

    /// <summary>Verifica y guarda la passkey de una persona ya dentro (Mi cuenta).</summary>
    public async Task<Passkey> ConfirmarRegistroAsync(CuentaResuelta cuenta, RegistrarPasskeyDto dto, ContextoDeCliente cliente, IMessageBus mensajes,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var desafio = await CargarAsync(sesion, dto.Desafio, ct);
        desafio.AsegurarEtapa(EtapaDeIngreso.CodigoVerificado, ahora);
        if (desafio.Cuenta != cuenta.Cuenta || desafio.OpcionesDePasskey is null)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este registro no es de tu cuenta o ya se usó. Empieza de nuevo.");

        var nueva = await RegistrarVerificadaAsync(sesion, desafio, cuenta, dto.Respuesta, dto.Nombre, cliente, ahora, ct);
        desafio.Consumir(ahora);
        sesion.Store(desafio);
        await sesion.SaveChangesAsync(ct);
        await AvisosDeSeguridad.PasskeyRegistradaAsync(mensajes, cuenta, nueva, cliente, ahora);
        return nueva;
    }

    /// <summary>Verifica la respuesta de registro y deja la passkey (y su auditoría) en la sesión, sin guardar.</summary>
    private async Task<Passkey> RegistrarVerificadaAsync(IDocumentSession sesion, DesafioDeIngreso desafio, CuentaResuelta cuenta, JsonElement respuestaJson,
        string? nombre, ContextoDeCliente cliente, DateTimeOffset ahora, CancellationToken ct)
    {
        var verificada = await passkeys.VerificarRegistroAsync(desafio.OpcionesDePasskey!, respuestaJson.GetRawText(), ct);
        var sujeto = cuenta.Cuenta.SujetoId;
        var tipo = cuenta.Cuenta.Tipo;
        var credencial = verificada.CredencialId;
        if (await sesion.Query<Passkey>().AnyAsync(p => p.CredencialId == credencial, ct))
            throw ReglaDeNegocioException.Conflicto("passkey-duplicada", "Esa passkey ya está registrada.");
        if (await sesion.Query<Passkey>().CountAsync(p => p.Propietario == tipo && p.SujetoId == sujeto && p.RevocadaEn == null, ct) >= Passkey.MaximoPorPropietario)
            throw ReglaDeNegocioException.Conflicto("passkeys-excesivas", $"Puedes tener hasta {Passkey.MaximoPorPropietario} passkeys: elimina alguna para registrar otra.");

        var passkey = Passkey.Registrar(tipo, cuenta.Cuenta.TenantId, sujeto, credencial, verificada.ManejadorDeUsuario, verificada.ClavePublica,
            verificada.Contador, nombre, verificada.Transportes, verificada.AaGuid, verificada.Sincronizada, ahora);
        sesion.Store(passkey);
        RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.PasskeyRegistrada, true, cliente, ahora, cuenta.Cuenta.TenantId, tipo, sujeto,
            cuenta.Correo, passkeyId: passkey.Id, detalle: passkey.Nombre));
        return passkey;
    }

    // ---------- sesión ----------

    /// <summary>
    /// Abre la sesión directa de <paramref name="cuenta"/> (familia de refresco + token de acceso + cookie) dentro de la transacción del llamador,
    /// con la duración y la inactividad de su grupo (las de <c>Validasoft:Ingreso:Plataforma</c> para un operador) y el ingreso exitoso auditado.
    /// </summary>
    private SesionDirectaDto AbrirSesion(IDocumentSession sesion, CuentaResuelta cuenta, MetodoDeIngreso metodo, ContextoDeCliente cliente,
        HttpResponse respuesta, DateTimeOffset ahora)
    {
        var (horas, inactividad) = cuenta.Tenant is { } t
            ? (t.SesionHoras, t.InactividadMinutos)
            : (configuracion.GetValue("Validasoft:Ingreso:Plataforma:SesionHoras", 8), configuracion.GetValue("Validasoft:Ingreso:Plataforma:InactividadMinutos", 30));
        var (documento, token) = SesionDeUsuario.Abrir(cuenta.Cuenta.Tipo, cuenta.Cuenta.TenantId, cuenta.Cuenta.SujetoId, cuenta.Correo, metodo,
            TimeSpan.FromHours(horas), inactividad, ahora, cliente.Ip, cliente.Dispositivo);
        sesion.Insert(documento);
        RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.IngresoExitoso, true, cliente, ahora, cuenta.Cuenta.TenantId, cuenta.Cuenta.Tipo,
            cuenta.Cuenta.SujetoId, cuenta.Correo, metodo, sesionId: documento.Id));

        var acceso = EmitirAcceso(cuenta, documento.Id, metodo, inactividad, ahora);
        cookie.Poner(respuesta, token, documento.ExpiraEn);
        return new SesionDirectaDto(acceso.Token, acceso.Expira, documento.Id, metodo.ToString(),
            SugerirPasskey: metodo == MetodoDeIngreso.Codigo && !cuenta.ExigePasskey);
    }

    /// <summary>Token de acceso: vence en <c>Validasoft:Ingreso:MinutosAcceso</c> (15) y nunca después de la inactividad, para que renovar a tiempo mantenga la sesión.</summary>
    private SesionUsuarioDto EmitirAcceso(CuentaResuelta cuenta, Guid sesionId, MetodoDeIngreso metodo, int inactividadMinutos, DateTimeOffset ahora)
    {
        var vigencia = TimeSpan.FromMinutes(Math.Min(Math.Max(1, configuracion.GetValue("Validasoft:Ingreso:MinutosAcceso", 15)), inactividadMinutos));
        return cuenta.Usuario is { } usuario
            ? emisor.EmitirDeUsuario(usuario, cuenta.Cuenta.TenantId!.Value, sesionId, metodo, vigencia, ahora)
            : emisor.EmitirDeOperador(cuenta.Operador!, sesionId, metodo, vigencia, ahora);
    }

    /// <summary>
    /// Renueva el token de acceso con la cookie <c>vs_usuario</c> y la rota. Sin cookie o con una desconocida ⇒ 401 <c>sin-sesion</c>; reusar una cookie ya
    /// rotada revoca la familia (401 <c>sesion-revocada</c>); revocada ⇒ 401 <c>sesion-revocada</c>; pasada la duración del grupo ⇒ 401 <c>sesion-expirada</c>; sin
    /// renovar durante la inactividad ⇒ 401 <c>sesion-inactiva</c>. Los permisos del token se recalculan con los roles de ahora; si la persona ya no puede entrar
    /// (desactivada, grupo sin modo directo, ahora exige passkey) la sesión se cierra.
    /// </summary>
    public async Task<SesionDirectaDto> RenovarAsync(HttpContext http, ContextoDeCliente cliente, CancellationToken ct)
    {
        AsegurarCliente(http);
        var token = CookieDeSesionDeUsuario.Leer(http.Request);
        if (SesionDeRefresco.FamiliaDe(token) is not { } familia)
            throw ReglaDeNegocioException.NoAutenticado("sin-sesion", "No hay una sesión abierta. Ingresa de nuevo.");

        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var documento = await sesion.LoadAsync<SesionDeUsuario>(familia, ct)
                        ?? throw ReglaDeNegocioException.NoAutenticado("sin-sesion", "No hay una sesión abierta. Ingresa de nuevo.");
        var resultado = documento.Renovar(token!, ahora, cliente.Ip);
        sesion.Store(documento);

        if (!resultado.Renovada)
        {
            // Reuso: la revocación se guarda antes de responder (también invalida el token que tenga quien lo robó).
            RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.ReusoDeSesion, false, cliente, ahora, documento.TenantId, documento.Propietario,
                documento.SujetoId, documento.Correo, documento.Metodo, SesionDeUsuario.MotivoReuso, sesionId: documento.Id));
            try
            {
                await sesion.SaveChangesAsync(ct);
            }
            catch (Exception e) when (Peticiones.EsCarrera(e))
            {
                await RevocarDeNuevoAsync(familia, SesionDeUsuario.MotivoReuso, ahora, ct);
            }

            cache.Olvidar(familia);
            throw ReglaDeNegocioException.NoAutenticado("sesion-revocada", "Detectamos un uso repetido de tu sesión y la cerramos por seguridad. Ingresa de nuevo.");
        }

        var cuenta = await cuentas.CargarAsync(new CuentaDeIngreso(documento.Propietario, documento.TenantId, documento.SujetoId), ct);
        var motivo = cuenta is null ? SesionDeUsuario.MotivoUsuarioDesactivado
            : cuenta.ExigePasskey && documento.Metodo == MetodoDeIngreso.Codigo ? "passkey-requerida" : null;
        if (cuenta is null || motivo is not null)
        {
            documento.Revocar(motivo ?? SesionDeUsuario.MotivoUsuarioDesactivado, "sistema", ahora);
            sesion.Store(documento);
            await sesion.SaveChangesAsync(ct);
            cache.Olvidar(familia);
            cookie.Borrar(http.Response);
            throw ReglaDeNegocioException.NoAutenticado(motivo ?? "cuenta-no-disponible", motivo == "passkey-requerida"
                ? "Tu rol ahora exige passkey: ingresa de nuevo y registra una."
                : "Tu cuenta ya no puede ingresar directo. Pide ayuda a un administrador.");
        }

        var acceso = EmitirAcceso(cuenta, documento.Id, documento.Metodo, documento.InactividadMinutos, ahora);
        try
        {
            await sesion.SaveChangesAsync(ct);
        }
        catch (Exception e) when (Peticiones.EsCarrera(e))
        {
            // Concurrencia optimista: otra renovación con el mismo token ganó; nunca quedan dos tokens válidos.
            throw ReglaDeNegocioException.Conflicto("renovacion-concurrente", "La sesión se renovó al mismo tiempo en otra pestaña; vuelve a intentarlo.");
        }

        cookie.Poner(http.Response, resultado.Token!, documento.ExpiraEn);
        return new SesionDirectaDto(acceso.Token, acceso.Expira, documento.Id, documento.Metodo.ToString(), SugerirPasskey: false);
    }

    /// <summary>204: revoca la familia de la cookie (si la hay) y la borra. Idempotente.</summary>
    public async Task CerrarAsync(HttpContext http, ContextoDeCliente cliente, CancellationToken ct)
    {
        AsegurarCliente(http);
        var ahora = reloj.GetUtcNow();
        if (SesionDeRefresco.FamiliaDe(CookieDeSesionDeUsuario.Leer(http.Request)) is { } familia)
        {
            await using var sesion = sesiones.Global();
            if (await sesion.LoadAsync<SesionDeUsuario>(familia, ct) is { Revocada: false } documento)
            {
                documento.Revocar(SesionDeUsuario.MotivoCierre, documento.SujetoId, ahora);
                sesion.Store(documento);
                RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.SesionCerrada, true, cliente, ahora, documento.TenantId, documento.Propietario,
                    documento.SujetoId, documento.Correo, documento.Metodo, SesionDeUsuario.MotivoCierre, sesionId: documento.Id));
                try
                {
                    await sesion.SaveChangesAsync(ct);
                }
                catch (Exception e) when (Peticiones.EsCarrera(e))
                {
                    await RevocarDeNuevoAsync(familia, SesionDeUsuario.MotivoCierre, ahora, ct);
                }

                cache.Olvidar(familia);
            }
        }

        cookie.Borrar(http.Response);
    }

    private async Task RevocarDeNuevoAsync(Guid familia, string motivo, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.Global();
        if (await sesion.LoadAsync<SesionDeUsuario>(familia, ct) is { Revocada: false } documento)
        {
            documento.Revocar(motivo, "sistema", ahora);
            sesion.Store(documento);
            await sesion.SaveChangesAsync(ct);
        }
    }

    // ---------- utilidades ----------

    private static void AsegurarCliente(HttpContext http)
    {
        if (!string.Equals(http.Request.Headers[CabeceraCliente].ToString().Trim(), ValorCliente, StringComparison.OrdinalIgnoreCase))
            throw ReglaDeNegocioException.Prohibido("cliente-invalido", $"Esta ruta solo la usa la aplicación web de Validasoft (falta la cabecera {CabeceraCliente}).");
    }

    private static async Task<DesafioDeIngreso> CargarAsync(IDocumentSession sesion, Guid id, CancellationToken ct) =>
        await sesion.LoadAsync<DesafioDeIngreso>(id, ct)
        ?? throw ReglaDeNegocioException.NoEncontrado("desafio-no-encontrado", "Ese ingreso no existe o ya venció. Empieza de nuevo.");

    /// <summary>El último acceso del usuario del grupo (un documento del grupo, fuera de la transacción global del ingreso).</summary>
    private async Task RegistrarUltimoAccesoAsync(CuentaResuelta cuenta, DateTimeOffset ahora, CancellationToken ct)
    {
        if (cuenta.Cuenta is { Tipo: TipoDePropietario.Usuario, TenantId: { } tenantId } && Guid.TryParse(cuenta.Cuenta.SujetoId, out var usuarioId))
        {
            await using var deTenant = sesiones.ParaTenant(tenantId);
            if (await deTenant.LoadAsync<Usuario>(usuarioId, ct) is { } usuario)
            {
                usuario.UltimoAcceso = ahora;
                deTenant.Store(usuario);
                await deTenant.SaveChangesAsync(ct);
            }

            return;
        }

        if (cuenta.Operador is not null)
        {
            await using var global = sesiones.Global();
            if (await global.LoadAsync<OperadorDePlataforma>(cuenta.Operador.Id, ct) is { } operador)
            {
                operador.UltimoAcceso = ahora;
                global.Store(operador);
                await global.SaveChangesAsync(ct);
            }
        }
    }

    /// <summary>Identificador de usuario que el autenticador guarda con la credencial: 32 bytes estables y sin datos personales.</summary>
    public static byte[] ManejadorDe(CuentaDeIngreso cuenta) =>
        cuenta.Tipo == TipoDePropietario.Usuario && cuenta.TenantId is { } tenantId && Guid.TryParse(cuenta.SujetoId, out var usuarioId)
            ? [.. tenantId.ToByteArray(), .. usuarioId.ToByteArray()]
            : SHA256.HashData(Encoding.UTF8.GetBytes("validasoft:operador:" + cuenta.SujetoId));

    public static string NormalizarCorreo(string? correo)
    {
        var limpio = (correo ?? string.Empty).Trim().ToLowerInvariant();
        var arroba = limpio.IndexOf('@', StringComparison.Ordinal);
        if (limpio.Length > 254 || arroba <= 0 || arroba == limpio.Length - 1 || limpio.Count(c => c == '@') != 1 || limpio.Any(char.IsWhiteSpace))
            throw ReglaDeNegocioException.Validacion("correo-invalido", "Escribe un correo válido.");
        return limpio;
    }
}
