using System.Security.Cryptography;
using System.Text;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Integraciones;

/// <summary>
/// Suscripción de un sistema integrado a los eventos públicos (docs/05 §6, NW-4). Esquema <c>validacion</c>, multi-tenant.
/// El secreto de la firma se guarda <b>cifrado</b> (nunca en claro) y se muestra una sola vez al crear o rotar; durante la
/// rotación el anterior sigue firmando hasta <see cref="SecretoAnteriorVence"/>.
/// </summary>
public sealed class SuscripcionWebhook
{
    /// <summary>Comodín de <see cref="Tipos"/> y <see cref="Empresas"/>: todos los tipos o todas las empresas del alcance.</summary>
    public const string Todos = "*";

    public const int MaximoTipos = 100;

    public Guid Id { get; set; }

    /// <summary>Sistema integrado dueño de la suscripción (el de la credencial o el indicado por un administrador).</summary>
    public Guid SistemaId { get; set; }

    /// <summary>Código del sistema (copia para mostrar).</summary>
    public string Sistema { get; set; } = string.Empty;

    public string Url { get; set; } = string.Empty;

    public string? Descripcion { get; set; }

    /// <summary>Tipos de evento público (<c>vinculo.creado</c>…) o <c>*</c>.</summary>
    public List<string> Tipos { get; set; } = [];

    /// <summary>Empresas (ids) o <c>*</c> (todas las del alcance de quien la creó).</summary>
    public List<string> Empresas { get; set; } = [];

    /// <summary>Versión del sobre de los eventos (docs/05 §9); hoy solo 1.</summary>
    public int Version { get; set; } = 1;

    public bool Activa { get; set; } = true;

    /// <summary>Secreto de firma cifrado (IN-3).</summary>
    public string SecretoProtegido { get; set; } = string.Empty;

    /// <summary>Secreto anterior (cifrado) que sigue firmando durante la rotación.</summary>
    public string? SecretoAnteriorProtegido { get; set; }

    public DateTimeOffset? SecretoAnteriorVence { get; set; }

    /// <summary>Intentos de entrega fallidos seguidos (se reinicia con cada entrega exitosa).</summary>
    public int FallosSeguidos { get; set; }

    public DateTimeOffset? DeshabilitadaEn { get; set; }

    /// <summary><c>fallos-consecutivos</c> o <c>manual</c>.</summary>
    public string? CausaDeshabilitacion { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset ActualizadaEn { get; set; }

    public DateTimeOffset? UltimaEntregaEn { get; set; }

    public static SuscripcionWebhook Crear(Guid sistemaId, string sistema, string url, IEnumerable<string>? tipos, IEnumerable<string>? empresas,
        string? descripcion, string secretoProtegido, DateTimeOffset ahora) =>
        new()
        {
            Id = Guid.CreateVersion7(),
            SistemaId = sistemaId,
            Sistema = sistema,
            Url = url,
            Descripcion = Descripcion_(descripcion),
            Tipos = NormalizarTipos(tipos),
            Empresas = NormalizarEmpresas(empresas),
            SecretoProtegido = secretoProtegido,
            CreadaEn = ahora,
            ActualizadaEn = ahora
        };

    /// <summary>Cambia URL, tipos, empresas o descripción (nulo = sin cambio). La URL llega ya validada por <see cref="DestinoDeWebhook"/>.</summary>
    public void Editar(string? url, IEnumerable<string>? tipos, IEnumerable<string>? empresas, string? descripcion, DateTimeOffset ahora)
    {
        if (url is not null)
            Url = url;
        if (tipos is not null)
            Tipos = NormalizarTipos(tipos);
        if (empresas is not null)
            Empresas = NormalizarEmpresas(empresas);
        if (descripcion is not null)
            Descripcion = Descripcion_(descripcion);
        ActualizadaEn = ahora;
    }

    /// <summary>El secreto vigente pasa a ser el anterior y firma junto al nuevo durante <paramref name="solapamiento"/>.</summary>
    public void RotarSecreto(string nuevoProtegido, TimeSpan solapamiento, DateTimeOffset ahora)
    {
        SecretoAnteriorProtegido = SecretoProtegido;
        SecretoAnteriorVence = ahora + solapamiento;
        SecretoProtegido = nuevoProtegido;
        ActualizadaEn = ahora;
    }

    /// <summary>Secretos (cifrados) que firman ahora: el vigente y, durante la rotación, el anterior.</summary>
    public IReadOnlyList<string> SecretosVigentes(DateTimeOffset ahora) =>
        SecretoAnteriorProtegido is { } anterior && SecretoAnteriorVence > ahora ? [SecretoProtegido, anterior] : [SecretoProtegido];

    /// <summary>Reactiva y reinicia la cuenta de fallos (el administrador corrigió el receptor).</summary>
    public void Activar(DateTimeOffset ahora)
    {
        Activa = true;
        FallosSeguidos = 0;
        DeshabilitadaEn = null;
        CausaDeshabilitacion = null;
        ActualizadaEn = ahora;
    }

    public void Desactivar(string causa, DateTimeOffset ahora)
    {
        if (!Activa)
            throw ReglaDeNegocioException.Conflicto("webhook-inactivo", "La suscripción ya está desactivada.");
        Activa = false;
        DeshabilitadaEn = ahora;
        CausaDeshabilitacion = causa;
        ActualizadaEn = ahora;
    }

    /// <summary>Si el evento (tipo y empresa) le corresponde. Un evento sin empresa (de tenant) solo llega con empresas <c>*</c>.</summary>
    public bool Acepta(string tipo, Guid? empresaId) =>
        (Tipos.Contains(Todos) || Tipos.Contains(tipo)) &&
        (Empresas.Contains(Todos) || (empresaId is { } id && Empresas.Contains(id.ToString())));

    /// <summary>Entrega exitosa: reinicia la cuenta de fallos.</summary>
    public void RegistrarExito(DateTimeOffset ahora)
    {
        FallosSeguidos = 0;
        UltimaEntregaEn = ahora;
    }

    /// <summary>
    /// Un intento fallido más. Devuelve <c>true</c> si con este fallo la suscripción queda deshabilitada (al llegar a
    /// <paramref name="umbral"/> fallos seguidos estando activa): el llamador avisa al administrador.
    /// </summary>
    public bool RegistrarFallo(int umbral, DateTimeOffset ahora)
    {
        FallosSeguidos++;
        if (!Activa || FallosSeguidos < Math.Max(1, umbral))
            return false;

        Activa = false;
        DeshabilitadaEn = ahora;
        CausaDeshabilitacion = "fallos-consecutivos";
        ActualizadaEn = ahora;
        return true;
    }

    private static List<string> NormalizarTipos(IEnumerable<string>? tipos)
    {
        var lista = (tipos ?? []).Select(t => (t ?? string.Empty).Trim().ToLowerInvariant()).Where(t => t.Length > 0).Distinct().ToList();
        if (lista.Count == 0)
            throw ReglaDeNegocioException.Validacion("tipos-requeridos", "Indica al menos un tipo de evento (o \"*\" para todos).");
        if (lista.Count > MaximoTipos)
            throw ReglaDeNegocioException.Validacion("tipos-invalidos", $"Una suscripción admite hasta {MaximoTipos} tipos.");
        return lista.Contains(Todos) ? [Todos] : lista;
    }

    private static List<string> NormalizarEmpresas(IEnumerable<string>? empresas)
    {
        var lista = (empresas ?? []).Select(e => (e ?? string.Empty).Trim().ToLowerInvariant()).Where(e => e.Length > 0).Distinct().ToList();
        if (lista.Count == 0 || lista.Contains(Todos))
            return [Todos];
        if (lista.Any(e => !Guid.TryParse(e, out _)))
            throw ReglaDeNegocioException.Validacion("empresas-invalidas", "Las empresas se indican por su id (o \"*\" para todas).");
        return [.. lista.Select(e => Guid.Parse(e).ToString())];
    }

    private static string? Descripcion_(string? descripcion) =>
        string.IsNullOrWhiteSpace(descripcion) ? null
        : descripcion.Trim().Length > 200 ? throw ReglaDeNegocioException.Validacion("descripcion-invalida", "La descripción admite hasta 200 caracteres.")
        : descripcion.Trim();
}

public enum EstadoEntrega
{
    /// <summary>Encolada, aún sin intento.</summary>
    Pendiente,

    Entregada,

    /// <summary>Falló y tiene un reintento programado (<see cref="EntregaWebhook.ProximoIntentoEn"/>).</summary>
    Reintentando,

    /// <summary>Agotó los reintentos (o falló un reenvío manual); se puede reenviar a mano.</summary>
    Fallida,

    /// <summary>La suscripción estaba desactivada cuando tocaba enviarla.</summary>
    Cancelada
}

/// <summary>Un intento de entrega: resultado HTTP (o error de red), duración y si lo pidió una persona.</summary>
public sealed record IntentoDeEntrega(int Numero, Guid IntentoId, DateTimeOffset Fecha, int? CodigoHttp, long DuracionMs, bool Exitoso,
    string? Error, bool Manual);

/// <summary>
/// Entrega de un evento público a una suscripción (docs/05 §2.8, <c>EntregaWebhookDto</c>). Id determinista por
/// <c>(evento, suscripción)</c>: procesar dos veces el mismo evento no crea dos entregas. Esquema <c>validacion</c>, multi-tenant.
/// </summary>
public sealed class EntregaWebhook
{
    public Guid Id { get; set; }

    public Guid SuscripcionId { get; set; }

    public Guid EventoId { get; set; }

    public string TipoEvento { get; set; } = string.Empty;

    /// <summary>Cursor del evento (orden del tenant).</summary>
    public long Cursor { get; set; }

    public Guid? EmpresaId { get; set; }

    public EstadoEntrega Estado { get; set; }

    public List<IntentoDeEntrega> Intentos { get; set; } = [];

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset? EntregadaEn { get; set; }

    public DateTimeOffset? ProximoIntentoEn { get; set; }

    public static Guid IdPara(Guid eventoId, Guid suscripcionId) => Deterministas.Guid($"entrega:{eventoId:N}:{suscripcionId:N}");

    public static EntregaWebhook Nueva(SuscripcionWebhook suscripcion, Guid eventoId, string tipo, long cursor, Guid? empresaId, DateTimeOffset ahora) =>
        new()
        {
            Id = IdPara(eventoId, suscripcion.Id),
            SuscripcionId = suscripcion.Id,
            EventoId = eventoId,
            TipoEvento = tipo,
            Cursor = cursor,
            EmpresaId = empresaId,
            Estado = EstadoEntrega.Pendiente,
            CreadaEn = ahora
        };

    /// <summary>Registra el intento y fija el estado: entregada, reintento programado (<paramref name="proximo"/>) o fallida.</summary>
    public void Registrar(IntentoDeEntrega intento, DateTimeOffset? proximo)
    {
        Intentos.Add(intento);
        if (intento.Exitoso)
        {
            Estado = EstadoEntrega.Entregada;
            EntregadaEn = intento.Fecha;
            ProximoIntentoEn = null;
        }
        else
        {
            Estado = proximo is null ? EstadoEntrega.Fallida : EstadoEntrega.Reintentando;
            ProximoIntentoEn = proximo;
        }
    }

    public void Cancelar()
    {
        Estado = EstadoEntrega.Cancelada;
        ProximoIntentoEn = null;
    }
}

/// <summary>Esperas entre intentos fallidos (backoff). Tras el último, la entrega queda <c>Fallida</c>.</summary>
public sealed class PoliticaDeReintentos(IReadOnlyList<TimeSpan> esperas)
{
    /// <summary>1 min, 5 min, 30 min, 2 h y 6 h: seis intentos en total en algo más de 8 horas.</summary>
    public static readonly IReadOnlyList<TimeSpan> PorDefecto =
        [TimeSpan.FromMinutes(1), TimeSpan.FromMinutes(5), TimeSpan.FromMinutes(30), TimeSpan.FromHours(2), TimeSpan.FromHours(6)];

    public IReadOnlyList<TimeSpan> Esperas { get; } = esperas.Count > 0 ? esperas : PorDefecto;

    public int IntentosMaximos => Esperas.Count + 1;

    /// <summary>Espera antes del intento siguiente al número <paramref name="intentoFallido"/> (1 = el primero); nula si no hay más.</summary>
    public TimeSpan? EsperaTras(int intentoFallido) =>
        intentoFallido >= 1 && intentoFallido <= Esperas.Count ? Esperas[intentoFallido - 1] : null;
}

/// <summary>
/// Firma de las entregas (docs/05 §6.1, NW-5): <c>Validasoft-Signature: t={ts},v1={hmac}</c> con
/// <c>hmac = hex(HMAC-SHA256(secreto, "{ts}.{cuerpo}"))</c>; durante la rotación va un <c>v1</c> por cada secreto vigente.
/// </summary>
public static class FirmaDeWebhook
{
    public const string PrefijoSecreto = "whsec_";

    /// <summary>Secreto nuevo de 256 bits (<c>whsec_</c> + base64url).</summary>
    public static string GenerarSecreto() =>
        PrefijoSecreto + Convert.ToBase64String(RandomNumberGenerator.GetBytes(32)).TrimEnd('=').Replace('+', '-').Replace('/', '_');

    public static string Calcular(string secreto, long timestamp, string cuerpo)
    {
        var mensaje = Encoding.UTF8.GetBytes($"{timestamp}.{cuerpo}");
        return Convert.ToHexStringLower(HMACSHA256.HashData(Encoding.UTF8.GetBytes(secreto), mensaje));
    }

    public static string Cabecera(IEnumerable<string> secretos, long timestamp, string cuerpo) =>
        $"t={timestamp}," + string.Join(",", secretos.Select(s => "v1=" + Calcular(s, timestamp, cuerpo)));

    /// <summary>
    /// Verificación del lado del receptor (también la usan las pruebas y los SDK): alguna firma <c>v1</c> coincide en tiempo
    /// constante y el timestamp está dentro de la <paramref name="tolerancia"/>.
    /// </summary>
    public static bool Verificar(string secreto, string cabecera, string cuerpo, DateTimeOffset ahora, TimeSpan tolerancia)
    {
        long? timestamp = null;
        var firmas = new List<string>();
        foreach (var parte in (cabecera ?? string.Empty).Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
        {
            var igual = parte.IndexOf('=');
            if (igual <= 0)
                continue;
            var (clave, valor) = (parte[..igual], parte[(igual + 1)..]);
            if (clave == "t" && long.TryParse(valor, out var t))
                timestamp = t;
            else if (clave == "v1")
                firmas.Add(valor);
        }

        if (timestamp is not { } ts || Math.Abs(ahora.ToUnixTimeSeconds() - ts) > tolerancia.TotalSeconds)
            return false;

        var esperada = Encoding.ASCII.GetBytes(Calcular(secreto, ts, cuerpo));
        return firmas.Any(f => CryptographicOperations.FixedTimeEquals(esperada, Encoding.ASCII.GetBytes(f)));
    }
}

/// <summary>
/// Destinos permitidos para los webhooks (protección SSRF, docs/07 §7, NW-6): solo <c>https</c> hacia direcciones públicas.
/// <c>http</c> solo hacia <c>localhost</c>/<c>127.0.0.1</c>/<c>[::1]</c> con <c>PermitirHttpLocal</c> (desarrollo). La
/// IP real se vuelve a comprobar al conectar (contra DNS rebinding), en infraestructura.
/// </summary>
public static class DestinoDeWebhook
{
    private static readonly string[] HostsLocales = ["localhost", "127.0.0.1", "[::1]", "::1"];

    /// <summary>URL normalizada o 422 <c>url-invalida</c> / <c>url-no-permitida</c>.</summary>
    public static string Validar(string? url, bool permitirHttpLocal, bool permitirPrivados)
    {
        var texto = (url ?? string.Empty).Trim();
        if (texto.Length is 0 or > 2000 || !Uri.TryCreate(texto, UriKind.Absolute, out var uri) || uri.UserInfo.Length > 0 ||
            uri.Fragment.Length > 0 || uri.HostNameType is not (UriHostNameType.Dns or UriHostNameType.IPv4 or UriHostNameType.IPv6))
            throw ReglaDeNegocioException.Validacion("url-invalida", "La URL del webhook debe ser absoluta (https://host/ruta), sin usuario ni fragmento.");

        var host = uri.Host.ToLowerInvariant();
        var local = HostsLocales.Contains(host);
        if (uri.Scheme != Uri.UriSchemeHttps && !(uri.Scheme == Uri.UriSchemeHttp && local && permitirHttpLocal))
            throw ReglaDeNegocioException.Validacion("url-no-permitida", "La URL del webhook debe usar https (http solo hacia localhost en desarrollo).");

        if (local && !permitirHttpLocal && !permitirPrivados)
            throw ReglaDeNegocioException.Validacion("url-no-permitida", "El webhook no puede apuntar a la propia máquina.");

        if (uri.HostNameType is UriHostNameType.IPv4 or UriHostNameType.IPv6 &&
            System.Net.IPAddress.TryParse(uri.Host.Trim('[', ']'), out var ip) && !Permitida(ip, permitirHttpLocal, permitirPrivados))
            throw ReglaDeNegocioException.Validacion("url-no-permitida", "El webhook no puede apuntar a una dirección privada, local o reservada.");

        return uri.AbsoluteUri;
    }

    /// <summary>Si se puede conectar a <paramref name="ip"/>: pública, o loopback con http local, o privada si se permiten (desarrollo).</summary>
    public static bool Permitida(System.Net.IPAddress ip, bool permitirLoopback, bool permitirPrivados) =>
        DireccionesDeRed.EsPublica(ip) || permitirPrivados || (permitirLoopback && System.Net.IPAddress.IsLoopback(ip));
}

/// <summary>Clasificación de direcciones IP para la protección SSRF.</summary>
public static class DireccionesDeRed
{
    private static readonly (byte[] Red, int Bits)[] ReservadasV4 =
    [
        ([0, 0, 0, 0], 8), ([10, 0, 0, 0], 8), ([100, 64, 0, 0], 10), ([127, 0, 0, 0], 8), ([169, 254, 0, 0], 16),
        ([172, 16, 0, 0], 12), ([192, 0, 0, 0], 24), ([192, 0, 2, 0], 24), ([192, 88, 99, 0], 24), ([192, 168, 0, 0], 16),
        ([198, 18, 0, 0], 15), ([198, 51, 100, 0], 24), ([203, 0, 113, 0], 24), ([224, 0, 0, 0], 4), ([240, 0, 0, 0], 4)
    ];

    /// <summary>
    /// Pública = enrutable en Internet: no loopback, privada (RFC 1918, ULA), enlace local (incluye 169.254.169.254, la
    /// metadata de las nubes), CGNAT, multicast, documentación ni reservada. Las IPv4 embebidas en IPv6 se evalúan como IPv4.
    /// </summary>
    public static bool EsPublica(System.Net.IPAddress ip)
    {
        if (ip.IsIPv4MappedToIPv6)
            ip = ip.MapToIPv4();

        if (ip.AddressFamily == System.Net.Sockets.AddressFamily.InterNetwork)
        {
            var bytes = ip.GetAddressBytes();
            return !ReservadasV4.Any(r => EnRed(bytes, r.Red, r.Bits));
        }

        if (ip.AddressFamily != System.Net.Sockets.AddressFamily.InterNetworkV6)
            return false;

        var v6 = ip.GetAddressBytes();
        if (System.Net.IPAddress.IsLoopback(ip) || ip.Equals(System.Net.IPAddress.IPv6None) || ip.IsIPv6LinkLocal || ip.IsIPv6SiteLocal ||
            ip.IsIPv6Multicast || ip.IsIPv6UniqueLocal)
            return false;

        // 64:ff9b::/96 (NAT64) y 2002::/16 (6to4) llevan una IPv4 dentro: se evalúa esa.
        if (v6[0] == 0x00 && v6[1] == 0x64 && v6[2] == 0xff && v6[3] == 0x9b && v6[4..12].All(b => b == 0))
            return EsPublica(new System.Net.IPAddress(v6[12..16]));
        if (v6[0] == 0x20 && v6[1] == 0x02)
            return EsPublica(new System.Net.IPAddress(v6[2..6]));

        // 2001:db8::/32 es de documentación.
        return !(v6[0] == 0x20 && v6[1] == 0x01 && v6[2] == 0x0d && v6[3] == 0xb8);
    }

    private static bool EnRed(byte[] ip, byte[] red, int bits)
    {
        for (var i = 0; i < 4; i++)
        {
            var mascara = bits >= 8 ? 0xFF : bits <= 0 ? 0 : (byte)(0xFF << (8 - bits));
            if ((ip[i] & mascara) != (red[i] & mascara))
                return false;
            bits -= 8;
        }

        return true;
    }
}

/// <summary>Ids deterministas (los primeros 16 bytes de SHA-256 de una clave): procesar dos veces lo mismo no duplica.</summary>
public static class Deterministas
{
    public static Guid Guid(string clave) => new(SHA256.HashData(Encoding.UTF8.GetBytes("validasoft:" + clave))[..16]);
}
