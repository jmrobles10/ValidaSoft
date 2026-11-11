using System.Text.Json;

namespace Validasoft.Contratos;

// Ingreso directo sin contraseñas (ING-1…ING-6, docs/07 §2.4): correo → código de 6 dígitos → cuenta → passkey, o solo passkey.
// Las rutas de /api/v1/ingreso son anónimas (el ingreso es lo que da la credencial) y limitadas por IP.

/// <summary>Pide un código de ingreso al correo. Siempre responde igual, tenga o no cuenta ese correo.</summary>
public sealed record SolicitarCodigoDto(string Correo);

/// <summary>
/// Respuesta de pedir el código: el identificador opaco del ingreso (<c>desafio</c>) y cuándo vence el código. Idéntica exista o no el
/// correo: un correo sin cuenta recibe un desafío que nunca acepta un código.
/// </summary>
public sealed record CodigoSolicitadoDto(Guid Desafio, DateTimeOffset Expira, string Mensaje);

public sealed record VerificarCodigoDto(Guid Desafio, string Codigo);

public sealed record ElegirCuentaDto(Guid Desafio, string Cuenta);

/// <summary>Cuenta con la que la persona puede entrar con ese correo: un usuario de un grupo o un operador de Plataforma.</summary>
public sealed record CuentaDeIngresoDto(string Id, string Tipo, string Nombre, string? Grupo, bool ExigePasskey, bool TienePasskey);

/// <summary>
/// Siguiente paso del ingreso: <c>ElegirCuenta</c> (varias cuentas con ese correo), <c>PasskeyRequerida</c> (la cuenta tiene passkey: se
/// verifica con <see cref="Opciones"/>), <c>RegistrarPasskey</c> (la cuenta la exige y aún no tiene: se registra con
/// <see cref="Opciones"/>) o <c>Sesion</c> (ingreso completo; el token de refresco va en la cookie <c>vs_usuario</c>).
/// </summary>
public sealed record PasoDeIngresoDto(
    string Estado, Guid Desafio, IReadOnlyList<CuentaDeIngresoDto>? Cuentas = null, JsonElement? Opciones = null, SesionDirectaDto? Sesion = null);

/// <summary>
/// Sesión de un ingreso directo: el token de acceso (<c>Authorization: Bearer</c>, vence en minutos y se renueva con la cookie),
/// cómo se autenticó y si conviene ofrecer registrar una passkey (la cuenta no la exige y aún no tiene).
/// </summary>
public sealed record SesionDirectaDto(string Token, DateTimeOffset Expira, Guid SesionId, string Metodo, bool SugerirPasskey);

/// <summary>Opciones de WebAuthn (<c>navigator.credentials.create/get</c>, con los binarios en base64url) y el desafío que las guarda.</summary>
public sealed record OpcionesDePasskeyDto(Guid Desafio, JsonElement Opciones);

/// <summary>
/// Respuesta del navegador a una verificación de passkey. <see cref="Respuesta"/> es la credencial serializada
/// (<c>PublicKeyCredential.toJSON()</c>, binarios en base64url).
/// </summary>
public sealed record VerificarPasskeyDto(Guid Desafio, JsonElement Respuesta);

public sealed record RegistrarPasskeyDto(Guid Desafio, JsonElement Respuesta, string? Nombre);

public sealed record RenombrarPasskeyDto(string Nombre);

public sealed record RevocarPasskeyDto(string? Motivo);

public sealed record PasskeyDto(
    Guid Id, string Nombre, DateTimeOffset CreadaEn, DateTimeOffset? UltimoUso, bool Sincronizada, IReadOnlyList<string> Transportes, bool Activa,
    DateTimeOffset? RevocadaEn, string? RevocadaPor, string? MotivoRevocacion);

public sealed record SesionDeUsuarioDto(
    Guid Id, string Metodo, DateTimeOffset CreadaEn, DateTimeOffset UltimaActividad, DateTimeOffset ExpiraEn, string? Ip, string? Dispositivo, bool Actual,
    bool Activa, DateTimeOffset? RevocadaEn, string? MotivoRevocacion);

/// <summary>Quién es la persona de una sesión directa y qué se le exige (<c>GET /mi-cuenta</c>).</summary>
public sealed record MiCuentaDto(
    string Tipo, string Id, string Nombre, string Correo, string? Grupo, IReadOnlyList<RolAsignadoDto> Roles, bool ExigePasskey, string Metodo, Guid SesionId);

/// <summary>Política de ingreso de un grupo (<c>GET/PUT /configuracion/ingreso</c>): modos admitidos, duración e inactividad de la sesión directa y passkey para todos.</summary>
public sealed record ConfiguracionDeIngresoDto(IReadOnlyList<string> Modos, int SesionHoras, int InactividadMinutos, bool PasskeyParaTodos);

/// <summary>Un operador de Plataforma de ingreso directo (<c>POST /plataforma/operadores</c>).</summary>
public sealed record CrearOperadorDto(string Nombre, string Correo);

public sealed record OperadorDePlataformaDto(
    string Id, string Sistema, string Nombre, string Correo, bool Activo, bool Directo, DateTimeOffset CreadoEn, DateTimeOffset? UltimoAcceso, int Passkeys);

/// <summary>Una línea de la auditoría de identidad (ingresos, passkeys y sesiones) de un grupo.</summary>
public sealed record IngresoAuditadoDto(
    Guid Id, DateTimeOffset Fecha, string Evento, bool Exitoso, string? Correo, string? Metodo, string? Motivo, string? Ip, string? Dispositivo, string ActorTipo,
    string ActorId, Guid? SesionId, Guid? PasskeyId);
