using System.Security.Cryptography;
using System.Text;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Identidad;

/// <summary>
/// Cómo entran las personas de un grupo (ING-1): <see cref="Delegado"/> = identidad delegada desde el sistema integrado
/// (<c>X-Usuario-Delegado</c>, <c>POST /sesiones-usuario</c>, embebido); <see cref="Directo"/> = ingreso propio de
/// Validasoft con correo, código y passkey. Un grupo admite uno o ambos; nunca ninguno.
/// </summary>
[Flags]
public enum ModoDeIngreso
{
    Ninguno = 0,
    Delegado = 1,
    Directo = 2
}

/// <summary>Cómo se autenticó una sesión directa (claim <c>validasoft:metodo</c>).</summary>
public enum MetodoDeIngreso
{
    /// <summary>Solo el código del correo: un único factor, no permitido a los roles que exigen passkey.</summary>
    Codigo,

    /// <summary>Passkey con verificación de usuario (biometría o PIN): posesión y verificación en un solo gesto (dos factores).</summary>
    Passkey,

    /// <summary>Código del correo y, en el mismo ingreso, el registro de una passkey nueva (alta o recuperación).</summary>
    CodigoYPasskey,

    /// <summary>
    /// Inicio de sesión de la empresa del cliente (SSO, PI-3): su proveedor de identidad verificó a la persona y declaró que ya usó más de un factor
    /// (<c>amr</c> con <c>mfa</c>, llave o código de un solo uso). Vale por sí solo, también para los roles que exigen passkey (PI-4).
    /// </summary>
    Sso,

    /// <summary>Inicio de sesión de la empresa del cliente sin segundo factor declarado, más la passkey de Validasoft (verificada o recién registrada).</summary>
    SsoYPasskey
}

/// <summary>De quién es una passkey o una sesión directa: un usuario de un grupo o un operador de Plataforma.</summary>
public enum TipoDePropietario
{
    Usuario,
    Operador
}

/// <summary>
/// Quién debe tener passkey (P-14-4, ING-2): los roles <c>Administrador</c>, <c>OficialCumplimiento</c> y <c>Auditor</c>, todas las
/// personas de un grupo que así lo decida y siempre los operadores de Plataforma.
/// </summary>
public static class PoliticaDePasskeys
{
    public static bool RolExige(Rol rol) => rol is Rol.Administrador or Rol.OficialCumplimiento or Rol.Auditor;
}

/// <summary>La cuenta con la que se completa un ingreso: un usuario de un grupo o un operador de Plataforma.</summary>
public sealed record CuentaDeIngreso(TipoDePropietario Tipo, Guid? TenantId, string SujetoId);

public enum EtapaDeIngreso
{
    /// <summary>El código se envió (o, en un correo sin cuenta, parece haberse enviado) y espera ser digitado.</summary>
    CodigoEnviado,

    /// <summary>El código fue correcto: falta elegir la cuenta y, si aplica, registrar una passkey.</summary>
    CodigoVerificado,

    /// <summary>Ingreso solo con passkey: espera la aserción del navegador.</summary>
    PasskeyPendiente,

    /// <summary>Inicio de sesión con el proveedor de identidad del cliente (SSO): el navegador salió hacia él y se espera su regreso (PI-3).</summary>
    SsoPendiente,

    /// <summary>El proveedor de identidad devolvió a la persona y su identidad quedó verificada: falta que la app complete el ingreso (cuenta y passkey).</summary>
    SsoVerificado,

    /// <summary>La ceremonia terminó (con sesión o sin ella): no vuelve a servir.</summary>
    Consumido
}

/// <summary>
/// Ceremonia de un ingreso directo (ING-1): correo → código de 6 dígitos → cuenta → passkey, o solo passkey. Global (esquema
/// <c>identidad</c>). Su <see cref="Id"/> es la capacidad que recibe quien la pidió; el código se guarda solo como hash ligado
/// al desafío. Un correo sin cuenta recibe también un desafío (<see cref="EsSenuelo"/>) que nunca acepta un código, para que la
/// respuesta de pedir el código no revele si el correo existe. Vence a los 5 minutos y, una vez verificado el código, a los 10
/// para terminar; un solo uso; 5 intentos por código.
/// </summary>
public sealed class DesafioDeIngreso
{
    public const int IntentosMaximos = 5;
    public static readonly TimeSpan VigenciaDelCodigo = TimeSpan.FromMinutes(5);
    public static readonly TimeSpan VigenciaDeLaCeremonia = TimeSpan.FromMinutes(10);

    public Guid Id { get; set; }

    public string? Correo { get; set; }

    public EtapaDeIngreso Etapa { get; set; }

    public string? CodigoHash { get; set; }

    /// <summary>Códigos incorrectos acumulados; al llegar a <see cref="IntentosMaximos"/> el código queda anulado.</summary>
    public int Intentos { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset Expira { get; set; }

    public string? Ip { get; set; }

    public string? Dispositivo { get; set; }

    /// <summary>La cuenta elegida tras verificar el código.</summary>
    public CuentaDeIngreso? Cuenta { get; set; }

    /// <summary>Opciones de WebAuthn (JSON) de la ceremonia de passkey en curso, para verificar la respuesta del navegador.</summary>
    public string? OpcionesDePasskey { get; set; }

    /// <summary>Correo sin cuenta (o sin ingreso directo): simula el envío pero ningún código lo verifica.</summary>
    public bool EsSenuelo { get; set; }

    /// <summary>
    /// Desafío de una reautenticación con passkey (step-up, PI-13): solo sirve para confirmar una acción sensible en la sesión <see cref="SesionId"/>, nunca
    /// para abrir una sesión.
    /// </summary>
    public bool EsReautenticacion { get; set; }

    /// <summary>La sesión directa que se reautentica (solo en <see cref="EsReautenticacion"/>).</summary>
    public Guid? SesionId { get; set; }

    /// <summary>Intentos incorrectos con un código de recuperación en este ingreso (PI-18); al llegar a <see cref="IntentosMaximos"/> ya no se aceptan más.</summary>
    public int IntentosDeRecuperacion { get; set; }

    /// <summary>Datos del inicio de sesión con el proveedor de identidad del cliente (SSO, PI-3); nulo en los demás ingresos.</summary>
    public IngresoSso? Sso { get; set; }

    /// <summary>Código nuevo para un correo. Sin cuenta devuelve un señuelo y un código nulo (no se envía nada).</summary>
    public static (DesafioDeIngreso Desafio, string? Codigo) ParaCorreo(string correo, bool conCuenta, DateTimeOffset ahora, string? ip, string? dispositivo)
    {
        var desafio = new DesafioDeIngreso
        {
            Id = Guid.NewGuid(),
            Correo = Usuario.NormalizarCorreo(correo),
            Etapa = EtapaDeIngreso.CodigoEnviado,
            CreadoEn = ahora,
            Expira = ahora + VigenciaDelCodigo,
            Ip = Recortar(ip, 64),
            Dispositivo = Recortar(dispositivo, 300),
            EsSenuelo = !conCuenta
        };
        var codigo = RandomNumberGenerator.GetInt32(0, 1_000_000).ToString("D6", System.Globalization.CultureInfo.InvariantCulture);
        desafio.CodigoHash = desafio.HashDe(codigo);
        return (desafio, conCuenta ? codigo : null);
    }

    /// <summary>Desafío para registrar una passkey estando ya dentro (Mi cuenta): nace en la etapa posterior al código, con la cuenta fijada.</summary>
    public static DesafioDeIngreso ParaRegistro(CuentaDeIngreso cuenta, string correo, DateTimeOffset ahora, string? ip, string? dispositivo) => new()
    {
        Id = Guid.NewGuid(),
        Correo = correo,
        Etapa = EtapaDeIngreso.CodigoVerificado,
        Cuenta = cuenta,
        CreadoEn = ahora,
        Expira = ahora + VigenciaDeLaCeremonia,
        Ip = Recortar(ip, 64),
        Dispositivo = Recortar(dispositivo, 300)
    };

    /// <summary>Desafío de un ingreso solo con passkey (credencial detectable, sin correo previo).</summary>
    public static DesafioDeIngreso ParaPasskey(DateTimeOffset ahora, string? ip, string? dispositivo) => new()
    {
        Id = Guid.NewGuid(),
        Etapa = EtapaDeIngreso.PasskeyPendiente,
        CreadoEn = ahora,
        Expira = ahora + VigenciaDelCodigo,
        Ip = Recortar(ip, 64),
        Dispositivo = Recortar(dispositivo, 300)
    };

    /// <summary>
    /// Desafío para confirmar con la passkey una acción sensible de una sesión ya abierta (step-up, PI-13): nace en la etapa de la passkey, con la cuenta de la
    /// sesión fijada, y no abre ninguna sesión.
    /// </summary>
    public static DesafioDeIngreso ParaReautenticacion(CuentaDeIngreso cuenta, string correo, Guid sesionId, DateTimeOffset ahora, string? ip, string? dispositivo) => new()
    {
        Id = Guid.NewGuid(),
        Correo = correo,
        Etapa = EtapaDeIngreso.PasskeyPendiente,
        Cuenta = cuenta,
        EsReautenticacion = true,
        SesionId = sesionId,
        CreadoEn = ahora,
        Expira = ahora + VigenciaDelCodigo,
        Ip = Recortar(ip, 64),
        Dispositivo = Recortar(dispositivo, 300)
    };

    /// <summary>
    /// Inicio de sesión con el proveedor de identidad de un grupo (SSO, PI-3). Devuelve el desafío, el <c>state</c> que viaja a ese proveedor (<c>{id}.{secreto}</c>,
    /// solo se guarda su hash) y el secreto de navegador que va en una cookie para ligar el regreso al mismo navegador (evita que alguien te haga completar su
    /// propio inicio de sesión).
    /// </summary>
    public static (DesafioDeIngreso Desafio, string Estado, string SecretoDeNavegador) ParaSso(Guid tenantId, string proveedor, string? correo, string nonce,
        string verificadorProtegido, string retorno, DateTimeOffset ahora, string? ip, string? dispositivo)
    {
        var id = Guid.NewGuid();
        var secretoDeEstado = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        var secretoDeNavegador = Convert.ToHexStringLower(RandomNumberGenerator.GetBytes(24));
        var desafio = new DesafioDeIngreso
        {
            Id = id,
            Correo = string.IsNullOrWhiteSpace(correo) ? null : Usuario.NormalizarCorreo(correo),
            Etapa = EtapaDeIngreso.SsoPendiente,
            CreadoEn = ahora,
            Expira = ahora + VigenciaDeLaCeremonia,
            Ip = Recortar(ip, 64),
            Dispositivo = Recortar(dispositivo, 300),
            Sso = new IngresoSso
            {
                TenantId = tenantId,
                Proveedor = proveedor,
                EstadoHash = HashSimple(secretoDeEstado),
                Nonce = nonce,
                VerificadorProtegido = verificadorProtegido,
                Retorno = retorno,
                NavegadorHash = HashSimple(secretoDeNavegador)
            }
        };
        return (desafio, $"{id:N}.{secretoDeEstado}", secretoDeNavegador);
    }

    /// <summary>
    /// El proveedor devolvió a la persona: el <c>state</c> y la cookie del navegador deben ser los del inicio y el desafío seguir vigente y sin usar. 410
    /// <c>desafio-usado</c> / <c>desafio-vencido</c>; 403 <c>sso-estado-invalido</c> (otro navegador, o un <c>state</c> que no es el de este inicio).
    /// </summary>
    public void VerificarRetornoSso(string? estado, string? secretoDeNavegador, DateTimeOffset ahora)
    {
        if (Etapa != EtapaDeIngreso.SsoPendiente || Sso is null)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este inicio de sesión ya se usó. Empieza de nuevo.");
        AsegurarVigente(ahora);

        var partes = (estado ?? string.Empty).Split('.', 2);
        var estadoCorrecto = partes.Length == 2 && IgualesEnTiempoConstante(HashSimple(partes[1]), Sso.EstadoHash);
        var navegadorCorrecto = !string.IsNullOrEmpty(secretoDeNavegador) && IgualesEnTiempoConstante(HashSimple(secretoDeNavegador), Sso.NavegadorHash);
        if (!estadoCorrecto || !navegadorCorrecto)
            throw ReglaDeNegocioException.Prohibido("sso-estado-invalido",
                "Este inicio de sesión no lo empezaste en este navegador o ya no es válido. Empieza de nuevo desde Validasoft.");
    }

    /// <summary>La identidad que declaró el proveedor tras el regreso: el desafío queda a la espera de que la app complete el ingreso.</summary>
    public void RegistrarIdentidadSso(string correo, string? nombre, string? sujeto, IEnumerable<string> grupos, bool gruposDeclarados, bool multifactor, DateTimeOffset ahora)
    {
        if (Etapa != EtapaDeIngreso.SsoPendiente || Sso is null)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este inicio de sesión ya se usó. Empieza de nuevo.");
        AsegurarVigente(ahora);
        Sso.Correo = Usuario.NormalizarCorreo(correo);
        Sso.Nombre = Recortar(nombre, 200);
        Sso.Sujeto = Recortar(sujeto, 256);
        Sso.Grupos = [.. grupos.Where(g => !string.IsNullOrWhiteSpace(g)).Select(g => g.Trim()).Distinct(StringComparer.OrdinalIgnoreCase).Take(200)];
        Sso.GruposDeclarados = gruposDeclarados;
        Sso.Multifactor = multifactor;
        Correo = Sso.Correo;
        Etapa = EtapaDeIngreso.SsoVerificado;
        Expira = ahora + VigenciaDeLaCeremonia;
    }

    /// <summary>La app completa el ingreso con la identidad ya verificada: de aquí en adelante sigue como tras verificar el código (cuenta, passkey, sesión).</summary>
    public void ConfirmarSso(DateTimeOffset ahora)
    {
        if (Etapa != EtapaDeIngreso.SsoVerificado || Sso?.Correo is null)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este inicio de sesión ya se usó o no terminó. Empieza de nuevo.");
        AsegurarVigente(ahora);
        Etapa = EtapaDeIngreso.CodigoVerificado;
        Expira = ahora + VigenciaDeLaCeremonia;
    }

    /// <summary>Cierra el inicio de sesión con el proveedor tras un fallo: ya no sirve para nada (el siguiente intento empieza de nuevo).</summary>
    public void Anular() => Etapa = EtapaDeIngreso.Consumido;

    /// <summary>Cuenta un intento incorrecto con un código de recuperación (PI-18). Agotados ⇒ 429 <c>recuperacion-bloqueada</c>.</summary>
    public void ContarFalloDeRecuperacion()
    {
        IntentosDeRecuperacion++;
    }

    public void AsegurarRecuperacionDisponible()
    {
        if (IntentosDeRecuperacion >= IntentosMaximos)
            throw ReglaDeNegocioException.DemasiadosIntentos("recuperacion-bloqueada", "Se agotaron los intentos con códigos de recuperación en este ingreso. Empieza de nuevo.");
    }

    private static string HashSimple(string valor) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes(valor)));

    private static bool IgualesEnTiempoConstante(string a, string b) =>
        CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(a), Encoding.ASCII.GetBytes(b));

    /// <summary>
    /// <c>true</c> si el código es correcto (pasa a <see cref="EtapaDeIngreso.CodigoVerificado"/>); <c>false</c> si no lo es (cuenta un
    /// intento, que el llamador debe guardar antes de responder). Vencido ⇒ 410 <c>codigo-vencido</c>; agotado ⇒ 429
    /// <c>codigo-bloqueado</c>; fuera de etapa ⇒ 410 <c>desafio-usado</c>.
    /// </summary>
    public bool VerificarCodigo(string? codigo, DateTimeOffset ahora)
    {
        if (Etapa != EtapaDeIngreso.CodigoEnviado)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este ingreso ya se usó o no espera un código. Empieza de nuevo.");
        if (Expira <= ahora)
            throw ReglaDeNegocioException.Expirado("codigo-vencido", "El código venció. Pide uno nuevo.");
        if (Intentos >= IntentosMaximos)
            throw ReglaDeNegocioException.DemasiadosIntentos("codigo-bloqueado", "Se agotaron los intentos de este código. Pide uno nuevo.");

        var normalizado = (codigo ?? string.Empty).Trim();
        var esperado = Encoding.ASCII.GetBytes(CodigoHash ?? string.Empty);
        var recibido = Encoding.ASCII.GetBytes(HashDe(normalizado));
        if (!EsSenuelo && normalizado.Length == 6 && CryptographicOperations.FixedTimeEquals(esperado, recibido))
        {
            Etapa = EtapaDeIngreso.CodigoVerificado;
            CodigoHash = null;
            Expira = ahora + VigenciaDeLaCeremonia;
            return true;
        }

        Intentos++;
        return false;
    }

    public int IntentosRestantes => Math.Max(0, IntentosMaximos - Intentos);

    /// <summary>Tras el código: fija la cuenta con la que se continúa (sin passkey registrada falta registrarla).</summary>
    public void ElegirCuenta(CuentaDeIngreso cuenta, DateTimeOffset ahora)
    {
        AsegurarEtapa(EtapaDeIngreso.CodigoVerificado, ahora);
        Cuenta = cuenta;
    }

    /// <summary>Guarda las opciones de WebAuthn de la ceremonia en curso y renueva el plazo para completarla.</summary>
    public void GuardarOpciones(string opcionesJson, DateTimeOffset ahora)
    {
        if (Etapa is not (EtapaDeIngreso.CodigoVerificado or EtapaDeIngreso.PasskeyPendiente))
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este ingreso ya se usó o aún no verificó el código. Empieza de nuevo.");
        AsegurarVigente(ahora);
        OpcionesDePasskey = opcionesJson;
        Expira = ahora + VigenciaDeLaCeremonia;
    }

    /// <summary>Marca el desafío como usado: ya no sirve para otra aserción ni para otra sesión.</summary>
    public void Consumir(DateTimeOffset ahora)
    {
        if (Etapa is not (EtapaDeIngreso.CodigoVerificado or EtapaDeIngreso.PasskeyPendiente))
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este ingreso ya se usó. Empieza de nuevo.");
        AsegurarVigente(ahora);
        Etapa = EtapaDeIngreso.Consumido;
        OpcionesDePasskey = null;
    }

    public void AsegurarEtapa(EtapaDeIngreso esperada, DateTimeOffset ahora)
    {
        if (Etapa != esperada)
            throw ReglaDeNegocioException.Expirado("desafio-usado", "Este ingreso ya se usó o no está en ese paso. Empieza de nuevo.");
        AsegurarVigente(ahora);
    }

    public void AsegurarVigente(DateTimeOffset ahora)
    {
        if (Expira <= ahora)
            throw ReglaDeNegocioException.Expirado("desafio-vencido", "El ingreso venció. Empieza de nuevo.");
    }

    /// <summary>El código se guarda con hash ligado al desafío (dos desafíos con el mismo código no comparten hash).</summary>
    private string HashDe(string codigo) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{Id:N}:{codigo}")));

    public static string? Recortar(string? texto, int maximo) =>
        string.IsNullOrWhiteSpace(texto) ? null : texto.Length <= maximo ? texto : texto[..maximo];
}

/// <summary>
/// Lo que el inicio de sesión con el proveedor de identidad de un grupo (SSO, PI-3) guarda en su desafío: con qué grupo y proveedor se empezó, lo que
/// hay que comprobar al regreso (<c>state</c>, <c>nonce</c>, PKCE y el navegador) y, tras el regreso, la identidad que declaró el proveedor.
/// </summary>
public sealed class IngresoSso
{
    public Guid TenantId { get; set; }

    /// <summary>Código del proveedor (<c>oidc</c> o <c>workos</c>).</summary>
    public string Proveedor { get; set; } = string.Empty;

    /// <summary>SHA-256 del secreto del <c>state</c>; el valor nunca se guarda.</summary>
    public string EstadoHash { get; set; } = string.Empty;

    public string Nonce { get; set; } = string.Empty;

    /// <summary>El <c>code_verifier</c> de PKCE, cifrado con la clave maestra de secretos.</summary>
    public string VerificadorProtegido { get; set; } = string.Empty;

    /// <summary>La <c>redirect_uri</c> con la que se empezó (el canje del código exige la misma).</summary>
    public string Retorno { get; set; } = string.Empty;

    /// <summary>SHA-256 del secreto de la cookie del navegador que empezó el inicio de sesión.</summary>
    public string NavegadorHash { get; set; } = string.Empty;

    // Lo que declaró el proveedor tras el regreso.
    public string? Correo { get; set; }

    public string? Nombre { get; set; }

    /// <summary>El identificador estable de la persona en el proveedor (<c>sub</c>).</summary>
    public string? Sujeto { get; set; }

    public List<string> Grupos { get; set; } = [];

    /// <summary>El token traía el reclamo de grupos (aunque fuera vacío): sin él no se tocan los roles de la persona.</summary>
    public bool GruposDeclarados { get; set; }

    /// <summary>El proveedor declaró más de un factor (<c>amr</c>).</summary>
    public bool Multifactor { get; set; }
}

/// <summary>
/// Credencial WebAuthn (passkey) de un usuario de un grupo o de un operador de Plataforma (ING-2). Global (esquema
/// <c>identidad</c>): el ingreso solo con passkey no sabe aún de qué grupo es la persona. Se registra como credencial detectable
/// con verificación de usuario obligatoria. Revocarla (por su dueño o por un administrador del grupo) la deja sin efecto de
/// inmediato; recuperarse de una passkey perdida es revocarla y registrar otra.
/// </summary>
public sealed class Passkey
{
    public const int MaximoPorPropietario = 10;

    public Guid Id { get; set; }

    public TipoDePropietario Propietario { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>Id del usuario (<c>Guid</c>) o del operador de Plataforma.</summary>
    public string SujetoId { get; set; } = string.Empty;

    /// <summary>Identificador de la credencial (base64url); único.</summary>
    public string CredencialId { get; set; } = string.Empty;

    /// <summary>Identificador de usuario que el autenticador guarda con la credencial (base64url).</summary>
    public string ManejadorDeUsuario { get; set; } = string.Empty;

    /// <summary>Clave pública COSE (base64url).</summary>
    public string ClavePublica { get; set; } = string.Empty;

    public uint ContadorDeFirmas { get; set; }

    /// <summary>Nombre que le da su dueño («Mi teléfono», «Llave de la oficina»).</summary>
    public string Nombre { get; set; } = string.Empty;

    public List<string> Transportes { get; set; } = [];

    public string? AaGuid { get; set; }

    /// <summary>La credencial se sincroniza con la nube del proveedor (iCloud, Google…).</summary>
    public bool Sincronizada { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset? UltimoUso { get; set; }

    public string? UltimaIp { get; set; }

    public DateTimeOffset? RevocadaEn { get; set; }

    /// <summary>Actor que la revocó (<c>Usuario:{id}</c>, <c>Plataforma:{id}</c>…).</summary>
    public string? RevocadaPor { get; set; }

    public string? MotivoRevocacion { get; set; }

    public bool Activa => RevocadaEn is null;

    public static Passkey Registrar(TipoDePropietario propietario, Guid? tenantId, string sujetoId, string credencialId, string manejador,
        string clavePublica, uint contador, string? nombre, IEnumerable<string>? transportes, string? aaGuid, bool sincronizada, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(sujetoId) || string.IsNullOrWhiteSpace(credencialId) || string.IsNullOrWhiteSpace(clavePublica))
            throw ReglaDeNegocioException.Validacion("passkey-invalida", "La passkey no trae su identificador, su dueño o su clave.");

        return new Passkey
        {
            Id = Guid.CreateVersion7(),
            Propietario = propietario,
            TenantId = tenantId,
            SujetoId = sujetoId,
            CredencialId = credencialId,
            ManejadorDeUsuario = manejador,
            ClavePublica = clavePublica,
            ContadorDeFirmas = contador,
            Nombre = NombreLimpio(nombre),
            Transportes = [.. (transportes ?? []).Where(t => !string.IsNullOrWhiteSpace(t)).Distinct()],
            AaGuid = aaGuid,
            Sincronizada = sincronizada,
            CreadaEn = ahora
        };
    }

    public static string NombreLimpio(string? nombre)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        if (limpio.Length == 0)
            return "Mi passkey";
        if (limpio.Length > 60)
            throw ReglaDeNegocioException.Validacion("nombre-invalido", "El nombre de la passkey admite hasta 60 caracteres.");
        return limpio;
    }

    public void Renombrar(string? nombre)
    {
        AsegurarActiva();
        Nombre = NombreLimpio(nombre);
    }

    /// <summary>
    /// Una firma exitosa: el contador debe crecer (si el autenticador lo usa); uno que no crece indica una credencial clonada y
    /// se rechaza. Guarda el último uso y la IP.
    /// </summary>
    public void Usar(uint contador, DateTimeOffset ahora, string? ip)
    {
        AsegurarActiva();
        if ((contador != 0 || ContadorDeFirmas != 0) && contador <= ContadorDeFirmas)
            throw ReglaDeNegocioException.NoAutenticado("passkey-clonada",
                "El contador de la passkey no avanzó: podría estar clonada. Pide a un administrador que la revoque y registra una nueva.");

        ContadorDeFirmas = contador;
        UltimoUso = ahora;
        UltimaIp = DesafioDeIngreso.Recortar(ip, 64);
    }

    public void Revocar(string actor, string? motivo, DateTimeOffset ahora)
    {
        if (!Activa)
            throw ReglaDeNegocioException.Conflicto("passkey-revocada", "La passkey ya estaba revocada.");
        RevocadaEn = ahora;
        RevocadaPor = actor;
        MotivoRevocacion = DesafioDeIngreso.Recortar(motivo, 200);
    }

    public void AsegurarActiva()
    {
        if (!Activa)
            throw ReglaDeNegocioException.NoAutenticado("passkey-revocada", "Esa passkey fue revocada. Registra una nueva.");
    }
}

/// <summary>
/// Sesión directa de una persona (ING-1): una <b>familia</b> de tokens de refresco opacos que rotan en cada renovación, como la del
/// tercero (<see cref="SesionDeRefresco"/>, RE-3), con dos límites: un máximo absoluto (<see cref="ExpiraEn"/>, por grupo) y un
/// cierre por inactividad (<see cref="InactividadMinutos"/>: sin renovar durante ese tiempo ya no se puede renovar). Global (esquema
/// <c>identidad</c>). Reusar un token rotado revoca la familia. El token tiene la forma <c>{familia:N}.{secreto}</c>.
/// </summary>
public sealed class SesionDeUsuario
{
    public const string MotivoReuso = "reuso";
    public const string MotivoCierre = "cierre";

    /// <summary>
    /// Tras rotar, el token anterior se tolera este tiempo como una petición duplicada (dos pestañas o un reintento que no vio la cookie nueva):
    /// responde 409 <c>renovacion-concurrente</c> sin revocar. Pasado este plazo, presentarlo es un reuso y revoca la familia.
    /// </summary>
    public static readonly TimeSpan GraciaDeRotacion = TimeSpan.FromSeconds(10);

    public const string MotivoAdministrador = "administrador";
    public const string MotivoUsuarioDesactivado = "usuario-desactivado";
    public const string MotivoPasskeyRevocada = "passkey-revocada";

    public Guid Id { get; set; }

    public string TokenHash { get; set; } = string.Empty;

    public TipoDePropietario Propietario { get; set; }

    public Guid? TenantId { get; set; }

    /// <summary>Id del usuario o del operador de Plataforma.</summary>
    public string SujetoId { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public MetodoDeIngreso Metodo { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    /// <summary>Máximo absoluto: <see cref="CreadaEn"/> + la duración del grupo; ninguna renovación lo extiende.</summary>
    public DateTimeOffset ExpiraEn { get; set; }

    /// <summary>Última renovación (o apertura): de ella se cuenta la inactividad.</summary>
    public DateTimeOffset UltimaActividad { get; set; }

    /// <summary>Minutos sin renovar tras los cuales la sesión se cierra (el del grupo al abrirla).</summary>
    public int InactividadMinutos { get; set; }

    public int Rotaciones { get; set; }

    /// <summary>Hash del token anterior a la última rotación y cuándo ocurrió: solo para tolerar una petición duplicada (<see cref="GraciaDeRotacion"/>).</summary>
    public string? TokenHashAnterior { get; set; }

    public DateTimeOffset? RotadaEn { get; set; }

    public DateTimeOffset? RevocadaEn { get; set; }

    public string? MotivoRevocacion { get; set; }

    /// <summary>Actor que la cerró: el propio usuario, un administrador del grupo o el sistema.</summary>
    public string? RevocadaPor { get; set; }

    public string? Ip { get; set; }

    public string? UltimaIp { get; set; }

    public string? Dispositivo { get; set; }

    /// <summary>
    /// Cuándo la persona confirmó con su passkey una acción sensible en esta sesión (step-up, PI-13). Las acciones sensibles piden haberlo hecho en los últimos
    /// <see cref="VentanaDeReautenticacion"/>.
    /// </summary>
    public DateTimeOffset? ReautenticadaEn { get; set; }

    /// <summary>Cuánto vale una reautenticación con passkey para las acciones sensibles de la sesión.</summary>
    public static readonly TimeSpan VentanaDeReautenticacion = TimeSpan.FromMinutes(5);

    public bool ReautenticadaVigente(DateTimeOffset ahora) =>
        ReautenticadaEn is { } cuando && ahora >= cuando && ahora - cuando <= VentanaDeReautenticacion;

    public void Reautenticar(DateTimeOffset ahora) => ReautenticadaEn = ahora;

    public bool Revocada => RevocadaEn is not null;

    public bool VigenteEn(DateTimeOffset ahora) =>
        !Revocada && ExpiraEn > ahora && ahora - UltimaActividad <= TimeSpan.FromMinutes(InactividadMinutos);

    public static (SesionDeUsuario Sesion, string Token) Abrir(TipoDePropietario propietario, Guid? tenantId, string sujetoId, string correo,
        MetodoDeIngreso metodo, TimeSpan duracion, int inactividadMinutos, DateTimeOffset ahora, string? ip, string? dispositivo)
    {
        if (string.IsNullOrWhiteSpace(sujetoId) || string.IsNullOrWhiteSpace(correo))
            throw ReglaDeNegocioException.Validacion("sesion-invalida", "La sesión necesita la cuenta y el correo con los que se abrió.");

        var sesion = new SesionDeUsuario
        {
            Id = Guid.NewGuid(),
            Propietario = propietario,
            TenantId = tenantId,
            SujetoId = sujetoId,
            Correo = correo.Trim().ToLowerInvariant(),
            Metodo = metodo,
            CreadaEn = ahora,
            ExpiraEn = ahora + duracion,
            UltimaActividad = ahora,
            InactividadMinutos = inactividadMinutos,
            Ip = DesafioDeIngreso.Recortar(ip, 64),
            UltimaIp = DesafioDeIngreso.Recortar(ip, 64),
            Dispositivo = DesafioDeIngreso.Recortar(dispositivo, 300)
        };
        return (sesion, sesion.NuevoToken());
    }

    /// <summary>
    /// Con el token vigente rota (devuelve el siguiente y el anterior deja de servir). Con otro token de la familia revoca todo y
    /// devuelve <c>Renovada = false</c> (el llamador guarda antes de responder). Revocada ⇒ 401 <c>sesion-revocada</c>; pasado el máximo ⇒
    /// 401 <c>sesion-expirada</c>; sin renovar por más de la inactividad ⇒ 401 <c>sesion-inactiva</c>.
    /// </summary>
    public ResultadoRenovacion Renovar(string token, DateTimeOffset ahora, string? ip)
    {
        if (Revocada)
            throw ReglaDeNegocioException.NoAutenticado("sesion-revocada", "La sesión se cerró. Vuelve a ingresar.");
        if (ExpiraEn <= ahora)
            throw ReglaDeNegocioException.NoAutenticado("sesion-expirada", "La sesión venció. Vuelve a ingresar.");
        if (ahora - UltimaActividad > TimeSpan.FromMinutes(InactividadMinutos))
            throw ReglaDeNegocioException.NoAutenticado("sesion-inactiva", "La sesión se cerró por inactividad. Vuelve a ingresar.");

        var esperado = Encoding.ASCII.GetBytes(TokenHash);
        var hashRecibido = SesionDeRefresco.HashDeToken(token);
        var recibido = Encoding.ASCII.GetBytes(hashRecibido);
        if (!CryptographicOperations.FixedTimeEquals(esperado, recibido))
        {
            // El token anterior, recién rotado, es casi siempre una petición duplicada y no un robo: se pide reintentar con la cookie nueva.
            if (TokenHashAnterior is not null && RotadaEn is { } rotada && ahora - rotada <= GraciaDeRotacion &&
                CryptographicOperations.FixedTimeEquals(Encoding.ASCII.GetBytes(TokenHashAnterior), recibido))
                throw ReglaDeNegocioException.Conflicto("renovacion-concurrente", "La sesión se acaba de renovar en otra pestaña; vuelve a intentarlo.");

            Revocar(MotivoReuso, "sistema", ahora);
            return new ResultadoRenovacion(false, null);
        }

        TokenHashAnterior = TokenHash;
        RotadaEn = ahora;
        UltimaActividad = ahora;
        UltimaIp = DesafioDeIngreso.Recortar(ip, 64) ?? UltimaIp;
        Rotaciones++;
        return new ResultadoRenovacion(true, NuevoToken());
    }

    /// <summary>Revoca la familia; repetir no cambia la primera revocación.</summary>
    public void Revocar(string motivo, string? por, DateTimeOffset ahora)
    {
        if (Revocada)
            return;
        RevocadaEn = ahora;
        MotivoRevocacion = motivo;
        RevocadaPor = por;
    }

    private string NuevoToken()
    {
        var token = $"{Id:N}.{Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_')}";
        TokenHash = SesionDeRefresco.HashDeToken(token);
        return token;
    }
}

/// <summary>Qué pasó en un ingreso o en la gestión de sesiones y passkeys (auditoría de identidad, ING-6).</summary>
public enum EventoDeIngreso
{
    CodigoEnviado,
    CodigoIncorrecto,
    IngresoExitoso,
    IngresoFallido,
    PasskeyRegistrada,
    PasskeyRevocada,
    PasskeyEliminada,
    SesionCerrada,
    SesionRevocada,
    ReusoDeSesion,
    UsuarioInvitado,

    /// <summary>Confirmó con su passkey una acción sensible (step-up, PI-13).</summary>
    ReautenticacionExitosa,

    ReautenticacionFallida,

    /// <summary>Generó un juego nuevo de códigos de recuperación (los anteriores dejaron de valer, PI-18).</summary>
    CodigosDeRecuperacionGenerados,

    /// <summary>Usó un código de recuperación para volver a entrar sin su passkey (PI-18).</summary>
    CodigoDeRecuperacionUsado,

    /// <summary>Su proveedor de identidad (SSO) lo dio de alta en el grupo en su primer ingreso (PI-4).</summary>
    UsuarioAprovisionadoPorSso,

    /// <summary>Sus roles se pusieron al día con los grupos de su proveedor de identidad (SSO, PI-4).</summary>
    RolesSincronizadosPorSso
}

/// <summary>
/// Registro append-only de identidad (ING-6, docs/03 §2.1): cada ingreso exitoso o fallido, cada passkey registrada o revocada y cada
/// sesión cerrada, con el dispositivo y la IP. Global (esquema <c>identidad</c>); <see cref="TenantId"/> nulo en lo que no se puede
/// atribuir a un grupo (correos sin cuenta) o en los operadores de Plataforma. Nunca guarda códigos ni tokens.
/// </summary>
public sealed class RegistroDeIngreso
{
    public Guid Id { get; set; }

    public DateTimeOffset Fecha { get; set; }

    public EventoDeIngreso Evento { get; set; }

    public bool Exitoso { get; set; }

    public Guid? TenantId { get; set; }

    public TipoDePropietario? Propietario { get; set; }

    /// <summary>Id del usuario o del operador al que se refiere el registro.</summary>
    public string? SujetoId { get; set; }

    public string? Correo { get; set; }

    public MetodoDeIngreso? Metodo { get; set; }

    /// <summary>Código estable de la falla (<c>codigo-incorrecto</c>, <c>passkey-requerida</c>…) o el motivo de una revocación.</summary>
    public string? Motivo { get; set; }

    public string? Ip { get; set; }

    public string? Dispositivo { get; set; }

    public ActorTipo ActorTipo { get; set; }

    public string ActorId { get; set; } = string.Empty;

    public Guid? SesionId { get; set; }

    public Guid? PasskeyId { get; set; }

    public string? CorrelationId { get; set; }

    public string? Detalle { get; set; }
}
