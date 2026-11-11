using Marten;
using Validasoft.Contratos;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>Una passkey recién verificada en un registro, lista para guardarse.</summary>
public sealed record RegistroDePasskeyVerificado(
    string CredencialId, string ManejadorDeUsuario, string ClavePublica, uint Contador, string? AaGuid, bool Sincronizada, IReadOnlyList<string> Transportes);

/// <summary>Una aserción verificada: la passkey que firmó y su contador nuevo (el llamador lo guarda con <see cref="Passkey.Usar"/>).</summary>
public sealed record AsertoVerificado(Passkey Passkey, uint Contador);

/// <summary>
/// Passkeys (WebAuthn): arma las opciones que el navegador pasa a <c>navigator.credentials.create/get</c> y verifica sus respuestas. La
/// implementación (Fido2NetLib) vive en infraestructura. Todas las opciones se piden como <b>credencial detectable con verificación de
/// usuario obligatoria</b>: la passkey sola, con su biometría o PIN, vale por dos factores (ING-2).
/// </summary>
public interface IServicioDePasskeys
{
    /// <summary>Opciones (JSON) para registrar una passkey. <paramref name="manejador"/> es el identificador del usuario que el autenticador guarda.</summary>
    string OpcionesDeRegistro(byte[] manejador, string nombre, string nombreVisible, IReadOnlyCollection<string> credencialesExistentes);

    /// <summary>Verifica la respuesta del navegador a <see cref="OpcionesDeRegistro"/>. Falla con 401 <c>passkey-invalida</c>.</summary>
    Task<RegistroDePasskeyVerificado> VerificarRegistroAsync(string opcionesJson, string respuestaJson, CancellationToken ct);

    /// <summary>Opciones (JSON) para firmar. Sin credenciales permitidas, el navegador ofrece las passkeys que tenga para el sitio.</summary>
    string OpcionesDeAutenticacion(IReadOnlyCollection<string>? credencialesPermitidas);

    /// <summary>
    /// Verifica la aserción: busca la passkey por su credencial (<paramref name="buscar"/>), valida firma, origen, desafío y verificación de
    /// usuario, y devuelve el contador nuevo. Falla con 401 <c>passkey-invalida</c> o <c>passkey-revocada</c>.
    /// </summary>
    Task<AsertoVerificado> VerificarAsertoAsync(string opcionesJson, string respuestaJson, Func<string, CancellationToken, Task<Passkey?>> buscar,
        CancellationToken ct);
}

/// <summary>
/// Lo que el proveedor de identidad entrega cuando una persona se verificó: quién es y si el proveedor ya exigió más de un factor. Un proveedor federado
/// (SSO, PI-3) agrega además su identificador estable de la persona (<paramref name="Sujeto"/>) y los grupos que declaró (<paramref name="Grupos"/>, con
/// <paramref name="GruposDeclarados"/> falso si el token no traía el reclamo: así no se confunde «sin grupos» con «no los informó»).
/// </summary>
public sealed record IdentidadVerificada(
    string Proveedor, string Correo, string? Nombre, bool EsMultifactor, string? Sujeto = null, IReadOnlyList<string>? Grupos = null, bool GruposDeclarados = false);

/// <summary>
/// Primer paso de un proveedor de identidad: lo que recibe el navegador para continuar. Un proveedor federado agrega <paramref name="UrlDeAutorizacion"/> (a dónde
/// va el navegador) y <paramref name="SecretoDeNavegador"/> (va en una cookie para ligar el regreso al mismo navegador).
/// </summary>
public sealed record InicioDeIngreso(Guid Desafio, DateTimeOffset Expira, string Mensaje, string? UrlDeAutorizacion = null, string? SecretoDeNavegador = null);

/// <summary>
/// Proveedor de identidad del ingreso directo (ING-7): verifica <b>quién es</b> la persona y nada más; no sabe de grupos, roles ni sesiones.
/// Hay tres: el propio (<c>local</c>: código de 6 dígitos al correo) y dos federados (PI-3, PI-6) que implementan esta misma interfaz —<see cref="IniciarAsync"/>
/// devuelve la URL de autorización y <see cref="CompletarAsync"/> canjea el código de su <i>callback</i>—: <c>oidc</c> (Azure AD/Entra, Google Workspace o cualquier
/// OIDC) y <c>workos</c>. Todos entregan la misma <see cref="IdentidadVerificada"/> y todo lo demás (cuentas por correo, política del grupo, passkeys, sesiones y
/// tokens) no cambia.
/// </summary>
public interface IProveedorDeIdentidad
{
    string Id { get; }

    /// <summary>
    /// Empieza la verificación de <paramref name="correo"/>: crea el desafío en <paramref name="sesion"/> (el llamador la guarda) y avisa a la
    /// persona. <paramref name="conCuenta"/> dice si ese correo puede entrar; si no, el proveedor simula el inicio sin enviar nada.
    /// </summary>
    Task<InicioDeIngreso> IniciarAsync(IDocumentSession sesion, Wolverine.IMessageBus mensajes, string correo, bool conCuenta, ContextoDeCliente cliente,
        DateTimeOffset ahora, CancellationToken ct);

    /// <summary>
    /// Verifica la respuesta de la persona sobre un desafío ya cargado: la identidad verificada, o <c>null</c> si no es correcta (el desafío cuenta
    /// el intento y el llamador lo guarda). Vencido o agotado: falla con la regla del proveedor.
    /// </summary>
    Task<IdentidadVerificada?> CompletarAsync(DesafioDeIngreso desafio, string respuesta, DateTimeOffset ahora, CancellationToken ct);

    /// <summary>
    /// «Probar conexión» de un proveedor federado (PI-1): comprueba, sin iniciar sesión ni cambiar nada, que el proveedor responde y está bien configurado. El proveedor
    /// propio no tiene qué probar.
    /// </summary>
    Task<PruebaDeSsoDto> ProbarAsync(ConfiguracionDeSso configuracion, CancellationToken ct) =>
        Task.FromResult(new PruebaDeSsoDto(false, "Este proveedor no tiene prueba de conexión.", null, null));
}
