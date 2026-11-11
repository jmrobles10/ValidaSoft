using System.Security.Claims;
using Marten;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Wolverine;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>
/// Seguridad de la cuenta de quien entró directo (PI-13, PI-18): confirmar con la passkey una acción sensible (step-up) y los códigos de recuperación que
/// respaldan la passkey cuando se pierde el dispositivo.
/// </summary>
public sealed partial class ServicioDeIngreso
{
    // ---------- reautenticación (step-up) ----------

    /// <summary>
    /// Exige que la persona haya confirmado con su passkey en los últimos <see cref="SesionDeUsuario.VentanaDeReautenticacion"/> antes de una acción sensible (rotar
    /// o revocar llaves, cambiar roles, desactivar sistemas, personas u operadores, configurar el SSO…). Solo aplica a quien entró directo (reclamo
    /// <c>validasoft:sesion-id</c>): una API key o la identidad delegada de un sistema no tienen a una persona delante de un navegador. 403
    /// <c>reautenticacion-requerida</c> (con <c>minutos</c> y <c>accion</c>); la persona sin ninguna passkey que entró por un SSO con varios factores ya está
    /// reautenticada.
    /// </summary>
    public async Task ExigirReautenticacionAsync(ClaimsPrincipal usuario, string accion, CancellationToken ct)
    {
        if (!Guid.TryParse(usuario.FindFirstValue(ReclamosDeIngreso.SesionId), out var sesionId))
            return;

        var ahora = reloj.GetUtcNow();
        await using var consulta = sesiones.ConsultaGlobal();
        var sesion = await consulta.LoadAsync<SesionDeUsuario>(sesionId, ct);
        if (sesion is null || !sesion.VigenteEn(ahora))
            throw ReglaDeNegocioException.NoAutenticado("sesion-revocada", "Tu sesión se cerró. Ingresa de nuevo.");
        if (sesion.ReautenticadaVigente(ahora))
            return;

        var tienePasskey = (await cuentas.PasskeysAsync(new CuentaDeIngreso(sesion.Propietario, sesion.TenantId, sesion.SujetoId), ct)).Count > 0;
        if (!tienePasskey && sesion.Metodo == MetodoDeIngreso.Sso)
            return;

        throw new ReglaDeNegocioException(TipoFalla.Prohibido, "reautenticacion-requerida",
            tienePasskey ? "Confirma con tu passkey para continuar con esta acción." : "Esta acción exige una passkey: registra una en Mi cuenta.")
        {
            Datos = new Dictionary<string, object?>
            {
                ["accion"] = accion,
                ["minutos"] = (int)SesionDeUsuario.VentanaDeReautenticacion.TotalMinutes,
                ["tienePasskey"] = tienePasskey
            }
        };
    }

    /// <summary>Si la sesión de la persona está reautenticada ahora y hasta cuándo.</summary>
    public EstadoDeReautenticacionDto EstadoDeReautenticacion(SesionDeUsuario sesion)
    {
        var ahora = reloj.GetUtcNow();
        var vigente = sesion.ReautenticadaVigente(ahora);
        return new EstadoDeReautenticacionDto(vigente, vigente ? sesion.ReautenticadaEn + SesionDeUsuario.VentanaDeReautenticacion : null,
            (int)SesionDeUsuario.VentanaDeReautenticacion.TotalMinutes);
    }

    /// <summary>Opciones de WebAuthn para confirmar con una de las passkeys de la cuenta. 409 <c>sin-passkey</c> si no tiene ninguna.</summary>
    public async Task<OpcionesDePasskeyDto> OpcionesDeReautenticacionAsync(CuentaResuelta cuenta, SesionDeUsuario actual, ContextoDeCliente cliente, CancellationToken ct)
    {
        var registradas = await cuentas.PasskeysAsync(cuenta.Cuenta, ct);
        if (registradas.Count == 0)
            throw ReglaDeNegocioException.Conflicto("sin-passkey", "Registra una passkey en Mi cuenta para confirmar acciones sensibles.");

        var ahora = reloj.GetUtcNow();
        var desafio = DesafioDeIngreso.ParaReautenticacion(cuenta.Cuenta, cuenta.Correo, actual.Id, ahora, cliente.Ip, cliente.Dispositivo);
        var opciones = passkeys.OpcionesDeAutenticacion([.. registradas.Select(p => p.CredencialId)]);
        desafio.GuardarOpciones(opciones, ahora);
        await using var sesion = sesiones.Global();
        sesion.Store(desafio);
        await sesion.SaveChangesAsync(ct);
        return new OpcionesDePasskeyDto(desafio.Id, MapeosDeIngreso.AJson(opciones));
    }

    /// <summary>
    /// Verifica la aserción de la passkey y deja la sesión reautenticada por <see cref="SesionDeUsuario.VentanaDeReautenticacion"/>. La passkey debe ser de la misma
    /// cuenta y activa. 401 <c>passkey-invalida</c>, <c>passkey-revocada</c>, <c>passkey-clonada</c>; 410 <c>desafio-usado</c>.
    /// </summary>
    public async Task<ReautenticacionDto> ConfirmarReautenticacionAsync(CuentaResuelta cuenta, SesionDeUsuario actual, VerificarPasskeyDto dto, ContextoDeCliente cliente,
        CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var desafio = await CargarAsync(sesion, dto.Desafio, ct);
        if (!desafio.EsReautenticacion || desafio.SesionId != actual.Id || desafio.Cuenta != cuenta.Cuenta || desafio.Etapa != EtapaDeIngreso.PasskeyPendiente ||
            desafio.OpcionesDePasskey is null)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Esta confirmación ya se usó o no es de tu sesión. Empieza de nuevo.");
        desafio.AsegurarVigente(ahora);

        Passkey? usada = null;
        try
        {
            var aserto = await passkeys.VerificarAsertoAsync(desafio.OpcionesDePasskey, dto.Respuesta.GetRawText(),
                async (credencial, c) => usada = await sesion.Query<Passkey>().FirstOrDefaultAsync(p => p.CredencialId == credencial, c), ct);
            var passkey = aserto.Passkey;
            passkey.AsegurarActiva();
            if (passkey.Propietario != cuenta.Cuenta.Tipo || passkey.SujetoId != cuenta.Cuenta.SujetoId || passkey.TenantId != cuenta.Cuenta.TenantId)
                throw ReglaDeNegocioException.NoAutenticado("passkey-invalida", "Esa passkey no es de tu cuenta.");

            passkey.Usar(aserto.Contador, ahora, cliente.Ip);
            sesion.Store(passkey);
            desafio.Consumir(ahora);
            sesion.Store(desafio);
            var documento = await sesion.LoadAsync<SesionDeUsuario>(actual.Id, ct)
                            ?? throw ReglaDeNegocioException.NoAutenticado("sesion-revocada", "Tu sesión se cerró. Ingresa de nuevo.");
            documento.Reautenticar(ahora);
            sesion.Store(documento);
            RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.ReautenticacionExitosa, true, cliente, ahora, cuenta.Cuenta.TenantId, cuenta.Cuenta.Tipo,
                cuenta.Cuenta.SujetoId, cuenta.Correo, actual.Metodo, sesionId: actual.Id, passkeyId: passkey.Id, detalle: passkey.Nombre));
            await sesion.SaveChangesAsync(ct);
            return new ReautenticacionDto(ahora + SesionDeUsuario.VentanaDeReautenticacion, (int)SesionDeUsuario.VentanaDeReautenticacion.TotalMinutes);
        }
        catch (ReglaDeNegocioException e)
        {
            await registro.RegistrarAsync(EventoDeIngreso.ReautenticacionFallida, false, cliente, cuenta.Cuenta.TenantId, cuenta.Cuenta.Tipo, cuenta.Cuenta.SujetoId,
                cuenta.Correo, actual.Metodo, e.Codigo, sesionId: actual.Id, passkeyId: usada?.Id, ct: ct);
            throw;
        }
    }

    // ---------- códigos de recuperación ----------

    public async Task<EstadoDeRecuperacionDto> EstadoDeRecuperacionAsync(CuentaResuelta cuenta, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        var documento = await consulta.LoadAsync<CodigosDeRecuperacion>(CodigosDeRecuperacion.IdDe(cuenta.Cuenta), ct);
        return new EstadoDeRecuperacionDto(documento?.Disponibles ?? 0, CodigosDeRecuperacion.Cantidad, documento?.GeneradosEn);
    }

    /// <summary>
    /// Genera un juego nuevo de códigos de recuperación (los anteriores dejan de valer) y los devuelve <b>una sola vez</b>. Solo para cuentas con al menos una
    /// passkey: es su respaldo. 409 <c>sin-passkey</c>. Quien llama exige antes la reautenticación.
    /// </summary>
    public async Task<CodigosDeRecuperacionDto> GenerarCodigosDeRecuperacionAsync(CuentaResuelta cuenta, ContextoDeCliente cliente, IMessageBus mensajes, CancellationToken ct)
    {
        if ((await cuentas.PasskeysAsync(cuenta.Cuenta, ct)).Count == 0)
            throw ReglaDeNegocioException.Conflicto("sin-passkey", "Los códigos de recuperación respaldan tu passkey: registra una primero.");

        var ahora = reloj.GetUtcNow();
        var (documento, claros) = CodigosDeRecuperacion.Generar(cuenta.Cuenta, ahora);
        await using var sesion = sesiones.Global();
        _ = await sesion.LoadAsync<CodigosDeRecuperacion>(documento.Id, ct); // para que la concurrencia optimista vea la versión que reemplaza
        sesion.Store(documento);
        RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.CodigosDeRecuperacionGenerados, true, cliente, ahora, cuenta.Cuenta.TenantId,
            cuenta.Cuenta.Tipo, cuenta.Cuenta.SujetoId, cuenta.Correo));
        await sesion.SaveChangesAsync(ct);
        await AvisosDeSeguridad.CodigosGeneradosAsync(mensajes, cuenta, cliente, ahora);
        return new CodigosDeRecuperacionDto(claros, ahora);
    }

    /// <summary>
    /// Con el código del correo ya verificado y la cuenta elegida, usa un código de recuperación en lugar de la passkey que se perdió: el código se gasta y sigue el
    /// registro de una passkey nueva (<c>RegistrarPasskey</c>), que abre la sesión. 422 <c>codigo-de-recuperacion-invalido</c> (con <c>intentosRestantes</c>); 429
    /// <c>recuperacion-bloqueada</c>.
    /// </summary>
    public async Task<PasoDeIngresoDto> UsarCodigoDeRecuperacionAsync(UsarCodigoDeRecuperacionDto dto, ContextoDeCliente cliente, IMessageBus mensajes, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var desafio = await CargarAsync(sesion, dto.Desafio, ct);
        desafio.AsegurarEtapa(EtapaDeIngreso.CodigoVerificado, ahora);
        var cuenta = desafio.Cuenta is null || desafio.EsReautenticacion ? null : await cuentas.CargarAsync(desafio.Cuenta, ct);
        if (cuenta is null)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este ingreso no espera un código de recuperación. Empieza de nuevo.");
        desafio.AsegurarRecuperacionDisponible();

        var documento = await sesion.LoadAsync<CodigosDeRecuperacion>(CodigosDeRecuperacion.IdDe(cuenta.Cuenta), ct);
        if (documento is null || !documento.Consumir(dto.Codigo, ahora))
        {
            desafio.ContarFalloDeRecuperacion();
            sesion.Store(desafio);
            await sesion.SaveChangesAsync(ct);
            await registro.RegistrarAsync(EventoDeIngreso.IngresoFallido, false, cliente, cuenta.Cuenta.TenantId, cuenta.Cuenta.Tipo, cuenta.Cuenta.SujetoId, cuenta.Correo,
                motivo: "recuperacion-incorrecta", ct: ct);
            var quedan = Math.Max(0, DesafioDeIngreso.IntentosMaximos - desafio.IntentosDeRecuperacion);
            throw new ReglaDeNegocioException(TipoFalla.Validacion, "codigo-de-recuperacion-invalido", $"Ese código de recuperación no es válido o ya se usó. Te quedan {quedan} intentos.")
            {
                Datos = new Dictionary<string, object?> { ["intentosRestantes"] = quedan }
            };
        }

        sesion.Store(documento);
        var opciones = passkeys.OpcionesDeRegistro(ManejadorDe(cuenta.Cuenta), cuenta.Correo, cuenta.Nombre, []);
        desafio.GuardarOpciones(opciones, ahora);
        sesion.Store(desafio);
        RegistroDeIngresos.Agregar(sesion, RegistroDeIngresos.Crear(EventoDeIngreso.CodigoDeRecuperacionUsado, true, cliente, ahora, cuenta.Cuenta.TenantId, cuenta.Cuenta.Tipo,
            cuenta.Cuenta.SujetoId, cuenta.Correo, detalle: $"Quedan {documento.Disponibles}"));
        try
        {
            await sesion.SaveChangesAsync(ct);
        }
        catch (Exception e) when (Peticiones.EsCarrera(e))
        {
            // Dos usos simultáneos del mismo código: solo gana uno.
            throw ReglaDeNegocioException.Validacion("codigo-de-recuperacion-invalido", "Ese código de recuperación ya se usó.");
        }

        await AvisosDeSeguridad.RecuperacionUsadaAsync(mensajes, cuenta, documento.Disponibles, cliente, ahora);
        return new PasoDeIngresoDto("RegistrarPasskey", desafio.Id, Opciones: MapeosDeIngreso.AJson(opciones));
    }
}
