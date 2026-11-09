using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Consentimientos;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>
/// Canje del enlace de acceso y del OTP (públicos, sin credencial, con límite de peticiones por IP). El token viaja en la
/// ruta: nunca se registra en logs (el manejador de errores enmascara ese segmento). Abrir la sesión además abre una
/// sesión de refresco y pone la cookie <c>vs_tercero</c> (RE-3).
/// </summary>
public static class EnlacesPublicosEndpoints
{
    /// <summary>
    /// Sin OTP ⇒ 200 con la sesión y la cookie de refresco. Con OTP ⇒ 202 y el código al mismo correo. Usado/vencido ⇒
    /// 410; inexistente ⇒ 404.
    /// </summary>
    [WolverinePost("/public/v1/enlaces/{token}/canjear")]
    [AllowAnonymous]
    public static async Task<IResult> Canjear(string token, HttpContext http, ISesiones sesiones, IMessageBus mensajes,
        IProtectorDeSecretos protector, IEmisorDeSesionesTercero emisor, CookieDeSesionTercero cookie, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global(mensajes);
        var enlace = await EnlacesDeAcceso.CargarAsync(sesion, token, ct);
        var resultado = enlace.Canjear(ahora);
        sesion.Store(enlace);

        if (resultado.SesionAbierta)
        {
            var abierta = await EnlacesDeAcceso.AbrirSesionAsync(sesion, emisor, enlace, otpVerificado: false, ahora, ct);
            var refresco = SesionesDeRefresco.Abrir(sesion, enlace, otpVerificado: false, ahora);
            await sesion.SaveChangesAsync(ct);
            cookie.Poner(http.Response, refresco.Token, refresco.Expira);
            return Results.Ok(abierta);
        }

        await mensajes.PublishAsync(new CorreoDeAccesoPendiente(PlantillaCorreo.CodigoOtp, enlace.CorreoDestino,
            protector.Proteger(resultado.CodigoOtp!), resultado.OtpExpira!.Value, null, null));
        await sesion.SaveChangesAsync(ct);
        return Results.Json(new OtpRequeridoDto(true, resultado.OtpExpira.Value), statusCode: StatusCodes.Status202Accepted);
    }

    /// <summary>
    /// Código correcto ⇒ sesión con <c>otpVerificado</c> y la cookie de refresco. Incorrecto ⇒ 422 <c>otp-invalido</c> (con
    /// <c>intentosRestantes</c>); agotado ⇒ 429.
    /// </summary>
    [WolverinePost("/public/v1/enlaces/{token}/otp")]
    [AllowAnonymous]
    public static async Task<SesionTerceroDto> VerificarOtp(string token, VerificarOtpDto dto, HttpContext http, ISesiones sesiones,
        IEmisorDeSesionesTercero emisor, CookieDeSesionTercero cookie, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var enlace = await EnlacesDeAcceso.CargarAsync(sesion, token, ct);
        var valido = enlace.VerificarOtp(dto.Codigo, ahora);
        sesion.Store(enlace);

        if (!valido)
        {
            // El intento fallido se guarda antes de responder; el enlace usa concurrencia optimista (IN-5).
            await sesion.SaveChangesAsync(ct);
            throw new ReglaDeNegocioException(TipoFalla.Validacion, "otp-invalido",
                $"El código no es correcto. Te quedan {enlace.IntentosRestantes} intentos.")
            {
                Datos = new Dictionary<string, object?> { ["intentosRestantes"] = enlace.IntentosRestantes }
            };
        }

        var abierta = await EnlacesDeAcceso.AbrirSesionAsync(sesion, emisor, enlace, otpVerificado: true, ahora, ct);
        var refresco = SesionesDeRefresco.Abrir(sesion, enlace, otpVerificado: true, ahora);
        await sesion.SaveChangesAsync(ct);
        cookie.Poner(http.Response, refresco.Token, refresco.Expira);
        return abierta;
    }
}

/// <summary>Vistas e invitaciones del tercero (sesión <c>Tercero</c>; solo los vínculos de su sesión, el resto 404).</summary>
public static class InvitacionesPublicasEndpoints
{
    [WolverineGet("/public/v1/vinculos")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IReadOnlyList<VinculoPublicoDto>> ListarVinculos(HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var consulta = sesiones.ConsultaGlobal();
        var vistos = await VistaDelTercero.CargarAsync(consulta, tercero, [.. tercero.Vinculos.Select(v => v.VinculoId)], ct);
        return [.. vistos.Select(v => v.ADto())];
    }

    /// <summary>Empresa, tipo, estado y los textos vigentes por aceptar; <c>textosFaltantes</c> si la empresa no los ha publicado.</summary>
    [WolverineGet("/public/v1/invitaciones/{vinculoId}")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<InvitacionPublicaDto> ObtenerInvitacion(Guid vinculoId, HttpContext http, ISesiones sesiones, TimeProvider reloj,
        CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        await using var consulta = sesiones.ConsultaGlobal();
        var visto = await VistaDelTercero.CargarUnoAsync(consulta, tercero, vinculoId, ct);
        var textos = await VistaDelTercero.TextosAsync(consulta, tercero.TerceroId, visto.Vinculo.EmpresaId, ct);

        return new InvitacionPublicaDto(visto.ADto(),
            visto.Vinculo.InvitacionAceptada ? [] : [.. textos.Requeridos.Select(t => t.APublico())],
            textos.Faltantes,
            textos.Faltantes.Count == 0 && EsAceptable(visto.Vinculo, reloj.GetUtcNow()));
    }

    /// <summary>
    /// Registra <c>ConsentimientosRegistrados</c> y luego <c>InvitacionAceptada</c> en el stream del vínculo (actor =
    /// tercero) y comparte con la empresa la versión actual de cada documento global que piden los requisitos del flujo
    /// resuelto para el vínculo (RQ-3; más los <c>PorVinculo</c> ya cargados para él); la respuesta trae esos
    /// <c>compartidos</c>. Idempotente por su propia regla: repetir ⇒ 409 <c>invitacion-ya-aceptada</c>. En la misma
    /// transacción abre el expediente inicial con ese flujo (FL-5), con los requisitos cubiertos ya <c>Cargado</c>; si no
    /// hay flujo publicado no se comparte ningún global, la aceptación se registra igual y el vínculo queda con apertura
    /// pendiente (FL-6): nunca falla por configuración de la empresa.
    /// </summary>
    [WolverinePost("/public/v1/invitaciones/{vinculoId}/aceptar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<InvitacionAceptadaDto> Aceptar(Guid vinculoId, AceptarInvitacionDto dto, HttpContext http, ISesiones sesiones,
        IContextoSolicitud contexto, IMessageBus mensajes, Certificados.EmisorDeCertificados certificados, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var enSesion = tercero.VinculoRequerido(vinculoId);
        // Si pierde una carrera (folio del expediente o el propio vínculo), se repite completa con una sesión nueva.
        var aceptada = await Peticiones.ConReintentosAsync(() => AceptarUnaVezAsync(vinculoId, dto, http, sesiones, contexto, tercero, enSesion, certificados, reloj, ct));
        // Lo compartido al aceptar (sin outbox de la bóveda) calcula sus vigencias por mensaje (TD-6).
        if (aceptada.Compartidos.Count > 0)
            await mensajes.PublishAsync(new Documentos.CalcularVigenciasDelVinculo(tercero.TerceroId, vinculoId));
        return aceptada;
    }

    private static async Task<InvitacionAceptadaDto> AceptarUnaVezAsync(Guid vinculoId, AceptarInvitacionDto dto, HttpContext http,
        ISesiones sesiones, IContextoSolicitud contexto, SesionDeTercero tercero, VinculoEnSesion enSesion, Certificados.EmisorDeCertificados certificados,
        TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.ParaTenant(enSesion.TenantId);
        var stream = await sesion.Events.FetchForWriting<Vinculo>(vinculoId, ct);
        var vinculo = stream.Aggregate;
        if (vinculo is null || vinculo.TerceroId != tercero.TerceroId || vinculo.EmpresaId != enSesion.EmpresaId)
            throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");

        vinculo.AsegurarAceptable(ahora);

        var tipo = await sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct);
        if (tipo is { ExigeOtpEnEnlace: true } && !tercero.OtpVerificado)
            throw ReglaDeNegocioException.Prohibido("otp-requerido",
                "Este vínculo exige verificar el código enviado a tu correo: abre la sesión con el enlace de esta invitación.");

        var textos = await VistaDelTercero.TextosAsync(sesion, tercero.TerceroId, vinculo.EmpresaId, ct);
        if (textos.Faltantes.Count > 0)
            throw ReglaDeNegocioException.Conflicto("textos-legales-faltantes",
                $"La empresa aún no ha publicado: {string.Join(", ", textos.Faltantes)}. Inténtalo más tarde.");

        var evidencia = new EvidenciaDeConsentimiento(tercero.TerceroId, tercero.Correo,
            http.Connection.RemoteIpAddress?.ToString() ?? "desconocida",
            http.Request.Headers.UserAgent.ToString(),
            tercero.OtpVerificado,
            string.Equals(http.Request.Headers["X-Canal"].ToString().Trim(), "Embed", StringComparison.OrdinalIgnoreCase)
                ? CanalAceptacion.Embed
                : CanalAceptacion.Web);

        var consentimientos = vinculo.RegistrarConsentimientos(textos.Requeridos, Otorgados(dto), evidencia, ahora);
        var aceptada = vinculo.AceptarInvitacion(tercero.Correo, ahora);
        stream.AppendMany(consentimientos, aceptada);

        if (textos.AvisoRequerido is not null && textos.Tercero is not null)
        {
            textos.Tercero.AceptarAvisoPlataforma(textos.AvisoRequerido, ahora);
            sesion.Store(textos.Tercero);
        }

        // Desde este momento (y no antes, B-1) la empresa recibe la versión actual de lo que pide su flujo: misma transacción.
        var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct);
        var pedidos = flujo is null ? [] : await RequisitosDeExpediente.DelFlujoAsync(sesion, flujo.Version, ct);
        var documentos = await ComparticionDeDocumentos.CompartirConVinculoAsync(sesion, vinculo, enSesion.TenantId,
            RequisitosDeExpediente.CodigosGlobales(pedidos), OrigenComparticion.AlAceptarInvitacion, ahora, ct);

        if (flujo is not null)
            await ProcesoDeValidacion.AbrirAsync(sesion, stream, flujo, contexto, certificados, ahora, ct, documentos);

        await sesion.SaveChangesAsync(ct);

        var actualizado = await sesion.LoadAsync<Vinculo>(vinculoId, ct) ?? vinculo;
        var empresa = await sesion.LoadAsync<Empresa>(vinculo.EmpresaId, ct);
        var aceptadoPor = new AceptadoPorDto(tercero.TerceroId, tercero.Correo);
        return new InvitacionAceptadaDto(
            [.. consentimientos.Consentimientos.Select(c => new AceptacionDto(c.Tipo.ToString(), c.TextoLegalId, c.Version, c.Hash,
                consentimientos.Fecha, evidencia.Ip, evidencia.UserAgent, evidencia.OtpVerificado, evidencia.Canal.ToString(), aceptadoPor))],
            actualizado.APublico(empresa, tipo),
            documentos.Compartidos);
    }

    private static List<ConsentimientoOtorgado> Otorgados(AceptarInvitacionDto dto) =>
    [
        .. (dto.Consentimientos ?? []).Select(c => Enum.TryParse<TipoTextoLegal>(c.Tipo, true, out var tipo) && Enum.IsDefined(tipo)
            ? new ConsentimientoOtorgado(tipo, c.TextoVersion, c.Aceptado)
            : throw ReglaDeNegocioException.Validacion("tipo-consentimiento-invalido",
                $"'{c.Tipo}' no es un consentimiento ({string.Join(", ", Enum.GetNames<TipoTextoLegal>())})."))
    ];

    private static bool EsAceptable(Vinculo vinculo, DateTimeOffset ahora)
    {
        try
        {
            vinculo.AsegurarAceptable(ahora);
            return true;
        }
        catch (ReglaDeNegocioException)
        {
            return false;
        }
    }
}

/// <summary>Autorregistro invisible (docs/05 §2.9): el tercero existe en la plataforma pero ninguna empresa lo ve hasta vincularlo.</summary>
public static class AutorregistroEndpoints
{
    public const string MensajeRecibido =
        "Recibimos tu solicitud. Si los datos corresponden a un registro válido, te enviaremos un enlace de acceso a tu correo.";

    /// <summary>
    /// Responde siempre 202 con el mismo cuerpo, exista o no el tercero. Si no existe lo crea (sin tenant) con el aviso
    /// de la plataforma aceptado; envía un enlace sin vínculo solo si el correo es de un contacto del tercero.
    /// </summary>
    [WolverinePost("/public/v1/autorregistro")]
    [AllowAnonymous]
    public static async Task<IResult> Autorregistrar(AutorregistroDto dto, ISesiones sesiones, IMessageBus mensajes, IProtectorDeSecretos protector,
        TimeProvider reloj, CancellationToken ct)
    {
        if (!dto.ConsentimientoPlataforma)
            throw ReglaDeNegocioException.Validacion("consentimiento-requerido",
                "Para registrarte debes aceptar el aviso de tratamiento de datos de la plataforma.");

        var ahora = reloj.GetUtcNow();
        var correo = (dto.Correo ?? string.Empty).Trim().ToLowerInvariant();
        if (!Enum.TryParse<TipoPersona>(dto.TipoPersona, true, out var tipoPersona) || !Enum.IsDefined(tipoPersona))
            throw ReglaDeNegocioException.Validacion("tipo-persona-invalido", "El tipo de persona debe ser Natural, Juridica o Consorcio.");

        // Se valida todo antes de buscar: un dato inválido responde igual exista o no el tercero.
        var candidato = Tercero.Crear(dto.Identificacion.ADominio(), tipoPersona, dto.Nombre, null,
            [new Contacto((dto.Nombre ?? string.Empty).Trim(), correo, null, "Principal")], ahora);

        await using var sesion = sesiones.Global(mensajes);
        var clave = candidato.ClaveIdentificacion;
        var tercero = await sesion.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == clave, ct);
        if (tercero is null)
        {
            tercero = candidato;
            if (await TextosLegales.VigenteAsync(sesion, TipoTextoLegal.AvisoPlataforma, null, ct) is { } aviso)
                tercero.AceptarAvisoPlataforma(aviso, ahora);
            sesion.Insert(tercero);
        }

        if (tercero.TieneContacto(correo))
            await EnlacesDeAcceso.AutorregistroAsync(sesion, mensajes, protector, tercero.Id, correo, ahora);

        await sesion.SaveChangesAsync(ct);
        return Results.Json(new SolicitudRecibidaDto(MensajeRecibido), statusCode: StatusCodes.Status202Accepted);
    }
}
