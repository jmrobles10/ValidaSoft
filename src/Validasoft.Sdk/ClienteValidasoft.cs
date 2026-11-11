using System.Globalization;
using System.Runtime.CompilerServices;
using Validasoft.Contratos;

namespace Validasoft.Sdk;

/// <summary>
/// Cliente de la API v1 para un sistema integrador (H9, docs/08 §2). Cada grupo cubre lo que usan el SRM, Marco y el Portal:
/// vínculos, invitaciones, expedientes, certificados, consulta de certificación, eventos (pull) y sesiones para pantallas
/// embebidas. Lógica de negocio: ninguna; decide la API.
/// </summary>
public interface IClienteValidasoft
{
    VinculosApi Vinculos { get; }

    InvitacionesApi Invitaciones { get; }

    ExpedientesApi Expedientes { get; }

    CertificadosApi Certificados { get; }

    ConsultasApi Consultas { get; }

    EventosApi Eventos { get; }

    SesionesApi Sesiones { get; }

    /// <summary>El sandbox del grupo y su buzón de correo de pruebas (docs/08 §2.2).</summary>
    SandboxApi Sandbox { get; }

    /// <summary>El mismo cliente sobre otra empresa interna (<c>X-Empresa</c>), para llaves con alcance a varias.</summary>
    IClienteValidasoft ParaEmpresa(Guid empresaId);

    /// <summary>
    /// El mismo cliente actuando en nombre de una persona del sistema (identidad delegada, ADR-0011): cada petición lleva
    /// una aserción <c>X-Usuario-Delegado</c> nueva y el permiso efectivo es la intersección de la llave y los roles de la
    /// persona. Requiere <see cref="OpcionesValidasoft.Firmante"/>.
    /// </summary>
    IClienteValidasoft ComoUsuario(UsuarioDelegado usuario);
}

/// <inheritdoc cref="IClienteValidasoft"/>
public sealed class ClienteValidasoft : IClienteValidasoft
{
    private readonly Transporte _transporte;

    /// <param name="http">Cliente HTTP (de <c>IHttpClientFactory</c> en producción); su <c>Timeout</c> aplica a cada intento.</param>
    public ClienteValidasoft(HttpClient http, OpcionesValidasoft opciones)
        : this(new Transporte(
            http ?? throw new ArgumentNullException(nameof(http)),
            Validar(opciones),
            opciones.EmpresaId,
            null))
    {
    }

    private ClienteValidasoft(Transporte transporte)
    {
        _transporte = transporte;
        Vinculos = new VinculosApi(transporte);
        Invitaciones = new InvitacionesApi(transporte);
        Expedientes = new ExpedientesApi(transporte);
        Certificados = new CertificadosApi(transporte);
        Consultas = new ConsultasApi(transporte);
        Eventos = new EventosApi(transporte);
        Sesiones = new SesionesApi(transporte);
        Sandbox = new SandboxApi(transporte);
    }

    public VinculosApi Vinculos { get; }

    public InvitacionesApi Invitaciones { get; }

    public ExpedientesApi Expedientes { get; }

    public CertificadosApi Certificados { get; }

    public ConsultasApi Consultas { get; }

    public EventosApi Eventos { get; }

    public SesionesApi Sesiones { get; }

    public SandboxApi Sandbox { get; }

    public IClienteValidasoft ParaEmpresa(Guid empresaId) => new ClienteValidasoft(_transporte.Con(empresaId, _transporte.Usuario));

    public IClienteValidasoft ComoUsuario(UsuarioDelegado usuario) =>
        new ClienteValidasoft(_transporte.Con(_transporte.EmpresaId, usuario ?? throw new ArgumentNullException(nameof(usuario))));

    private static OpcionesValidasoft Validar(OpcionesValidasoft opciones)
    {
        if (opciones is null)
            throw new ArgumentNullException(nameof(opciones));
        if (string.IsNullOrWhiteSpace(opciones.ApiKey))
            throw new ArgumentException("Configura la API key (OpcionesValidasoft.ApiKey).", nameof(opciones));
        return opciones;
    }

    internal static string Escapar(string valor) => Uri.EscapeDataString(valor);
}

/// <summary>Vínculos empresa ↔ tercero (docs/05 §2.3).</summary>
public sealed class VinculosApi
{
    private readonly Transporte _t;

    internal VinculosApi(Transporte transporte) => _t = transporte;

    /// <summary>
    /// Crea el vínculo (y el tercero si no existe) y, por defecto, envía la invitación. Si el vínculo ya existe y lo pide otro
    /// sistema, se le suma la referencia (200). Usa una <paramref name="claveIdempotencia"/> estable (p. ej. el id de la
    /// solicitud en el SRM) para que un reintento no duplique.
    /// </summary>
    public Task<VinculoDto> CrearAsync(CrearVinculoDto vinculo, string? claveIdempotencia = null, CancellationToken ct = default) =>
        _t.EnviarAsync<VinculoDto>(new Peticion(HttpMethod.Post, "api/v1/vinculos", vinculo, claveIdempotencia, Reintentable: claveIdempotencia is not null), ct);

    public Task<VinculoDto> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        _t.EnviarAsync<VinculoDto>(new Peticion(HttpMethod.Get, $"api/v1/vinculos/{id}"), ct);

    /// <summary>Busca por la referencia del sistema (<c>sistema</c> = código, <c>id</c> = el del sistema). 404 si no existe.</summary>
    public Task<VinculoDto> PorReferenciaAsync(string sistema, string id, CancellationToken ct = default) =>
        _t.EnviarAsync<VinculoDto>(new Peticion(HttpMethod.Get,
            $"api/v1/vinculos/por-referencia?sistema={ClienteValidasoft.Escapar(sistema)}&id={ClienteValidasoft.Escapar(id)}"), ct);

    public Task<VinculoDto> BloquearAsync(Guid id, BloquearVinculoDto bloqueo, CancellationToken ct = default) =>
        _t.EnviarAsync<VinculoDto>(new Peticion(HttpMethod.Post, $"api/v1/vinculos/{id}/bloquear", bloqueo), ct);

    public Task<VinculoDto> DesbloquearAsync(Guid id, JustificacionDto justificacion, CancellationToken ct = default) =>
        _t.EnviarAsync<VinculoDto>(new Peticion(HttpMethod.Post, $"api/v1/vinculos/{id}/desbloquear", justificacion), ct);
}

/// <summary>Invitaciones del tercero a un vínculo (enlace por correo).</summary>
public sealed class InvitacionesApi
{
    private readonly Transporte _t;

    internal InvitacionesApi(Transporte transporte) => _t = transporte;

    /// <summary>Envía (o reenvía) la invitación; el correo sale después de confirmar la transacción.</summary>
    public Task<EnlaceInvitacionDto> EnviarAsync(Guid vinculoId, InvitacionDto invitacion, CancellationToken ct = default) =>
        _t.EnviarAsync<EnlaceInvitacionDto>(new Peticion(HttpMethod.Post, $"api/v1/vinculos/{vinculoId}/invitaciones", invitacion), ct);
}

/// <summary>Expedientes de validación (docs/05 §2.4).</summary>
public sealed class ExpedientesApi
{
    private readonly Transporte _t;

    internal ExpedientesApi(Transporte transporte) => _t = transporte;

    public Task<ExpedienteDto> AbrirAsync(Guid vinculoId, AbrirExpedienteDto? expediente = null, string? claveIdempotencia = null,
        CancellationToken ct = default) =>
        _t.EnviarAsync<ExpedienteDto>(new Peticion(HttpMethod.Post, $"api/v1/vinculos/{vinculoId}/expedientes", expediente ?? new AbrirExpedienteDto(),
            claveIdempotencia, Reintentable: claveIdempotencia is not null), ct);

    public Task<ExpedienteDto> ObtenerAsync(Guid id, CancellationToken ct = default) =>
        _t.EnviarAsync<ExpedienteDto>(new Peticion(HttpMethod.Get, $"api/v1/expedientes/{id}"), ct);
}

/// <summary>Certificados del vínculo (docs/05 §2.6.1).</summary>
public sealed class CertificadosApi
{
    private readonly Transporte _t;

    internal CertificadosApi(Transporte transporte) => _t = transporte;

    /// <summary>Emisión actual e historial; sin certificado, <c>Actual</c> es nulo.</summary>
    public Task<CertificadoDelVinculoDto> DelVinculoAsync(Guid vinculoId, CancellationToken ct = default) =>
        _t.EnviarAsync<CertificadoDelVinculoDto>(new Peticion(HttpMethod.Get, $"api/v1/vinculos/{vinculoId}/certificado"), ct);

    /// <summary>Folio con emisión (<c>…-2</c>) ⇒ esa emisión; folio raíz ⇒ la actual.</summary>
    public Task<CertificadoDto> ObtenerAsync(string folio, CancellationToken ct = default) =>
        _t.EnviarAsync<CertificadoDto>(new Peticion(HttpMethod.Get, $"api/v1/certificados/{ClienteValidasoft.Escapar(folio)}"), ct);

    /// <summary>URL prefirmada del PDF (5 minutos); cada descarga queda auditada.</summary>
    public Task<DescargaDto> DescargarAsync(string folio, CancellationToken ct = default) =>
        _t.EnviarAsync<DescargaDto>(new Peticion(HttpMethod.Post, $"api/v1/certificados/{ClienteValidasoft.Escapar(folio)}/descarga"), ct);
}

/// <summary>Qué tercero consultar: identificación (el número se normaliza; el DV es opcional) y tipo de vínculo (código).</summary>
public sealed record ConsultaDeCertificacion(string TipoIdentificacion, string Numero, string? TipoVinculo = null, string Pais = "CO");

/// <summary>Consulta rápida de certificación para Marco y los ERP (scope <c>consultas:leer</c>, CN-1).</summary>
public sealed class ConsultasApi
{
    private readonly Transporte _t;

    internal ConsultasApi(Transporte transporte) => _t = transporte;

    /// <summary>
    /// Con <paramref name="etag"/> (el de la respuesta anterior) la API responde 304 si nada cambió:
    /// <see cref="RespuestaCondicional{T}.NoModificado"/> y se sigue usando la copia local. Respeta
    /// <c>Cache-Control: max-age=300</c> antes de volver a preguntar.
    /// </summary>
    public Task<RespuestaCondicional<ConsultaCertificacionDto>> CertificacionAsync(ConsultaDeCertificacion consulta, string? etag = null,
        CancellationToken ct = default)
    {
        if (consulta is null)
            throw new ArgumentNullException(nameof(consulta));
        var ruta = "api/v1/consultas/certificacion?pais=" + ClienteValidasoft.Escapar(consulta.Pais) +
                   "&tipoIdentificacion=" + ClienteValidasoft.Escapar(consulta.TipoIdentificacion) +
                   "&numero=" + ClienteValidasoft.Escapar(consulta.Numero) +
                   (consulta.TipoVinculo is null ? string.Empty : "&tipoVinculo=" + ClienteValidasoft.Escapar(consulta.TipoVinculo));
        return ConsultarAsync(ruta, etag, ct);
    }

    /// <summary>La misma consulta por id del vínculo.</summary>
    public Task<RespuestaCondicional<ConsultaCertificacionDto>> CertificacionDelVinculoAsync(Guid vinculoId, string? etag = null,
        CancellationToken ct = default) =>
        ConsultarAsync($"api/v1/consultas/certificacion?vinculoId={vinculoId}", etag, ct);

    private async Task<RespuestaCondicional<ConsultaCertificacionDto>> ConsultarAsync(string ruta, string? etag, CancellationToken ct)
    {
        var (datos, nueva, noModificado) = await _t.EnviarCondicionalAsync<ConsultaCertificacionDto>(
            new Peticion(HttpMethod.Get, ruta, SiNoCoincide: etag), ct);
        return new RespuestaCondicional<ConsultaCertificacionDto>(datos, nueva, noModificado);
    }
}

/// <summary>
/// Eventos públicos en modo pull (docs/05 §6, alternativa a los webhooks para redes cerradas): los mismos sobres, en orden
/// de cursor. Guarda el <see cref="EventoPublicoDto.Cursor"/> del último evento procesado y retoma desde ahí.
/// </summary>
public sealed class EventosApi
{
    private readonly Transporte _t;

    internal EventosApi(Transporte transporte) => _t = transporte;

    /// <param name="desde">Cursor del último evento procesado (nulo = desde el primero).</param>
    /// <param name="tipos">Filtro por tipo (<c>certificado.emitido</c>…); vacío = todos.</param>
    /// <param name="limite">1–200 (50 por defecto).</param>
    /// <param name="empresaId">Solo esa empresa (equivale a <c>X-Empresa</c>; útil con una llave de varias empresas).</param>
    public Task<PaginaEventosDto> ListarAsync(string? desde = null, IEnumerable<string>? tipos = null, int? limite = null, Guid? empresaId = null,
        CancellationToken ct = default)
    {
        var parametros = new List<string>();
        if (!string.IsNullOrEmpty(desde))
            parametros.Add("desde=" + ClienteValidasoft.Escapar(desde));
        var lista = tipos?.Where(t => !string.IsNullOrWhiteSpace(t)).ToList();
        if (lista is { Count: > 0 })
            parametros.Add("tipos=" + ClienteValidasoft.Escapar(string.Join(",", lista)));
        if (limite is { } l)
            parametros.Add("limite=" + l.ToString(CultureInfo.InvariantCulture));
        if (empresaId is { } e)
            parametros.Add("empresaId=" + e);
        var ruta = "api/v1/eventos" + (parametros.Count == 0 ? string.Empty : "?" + string.Join("&", parametros));
        return _t.EnviarAsync<PaginaEventosDto>(new Peticion(HttpMethod.Get, ruta, ConEmpresa: empresaId is null), ct);
    }

    /// <summary>Recorre todas las páginas desde <paramref name="desde"/> hasta alcanzar el último evento disponible.</summary>
    public async IAsyncEnumerable<EventoPublicoDto> RecorrerAsync(string? desde = null, IEnumerable<string>? tipos = null, int limite = 100,
        [EnumeratorCancellation] CancellationToken ct = default)
    {
        var lista = tipos?.ToList();
        var cursor = desde;
        while (true)
        {
            var pagina = await ListarAsync(cursor, lista, limite, null, ct);
            foreach (var evento in pagina.Datos)
                yield return evento;
            if (!pagina.HayMas || pagina.Datos.Count == 0)
                yield break;
            cursor = pagina.Cursor;
        }
    }

    /// <summary>Catálogo v1 de tipos de evento.</summary>
    public Task<IReadOnlyList<TipoEventoPublicoDto>> TiposAsync(CancellationToken ct = default) =>
        _t.EnviarAsync<IReadOnlyList<TipoEventoPublicoDto>>(new Peticion(HttpMethod.Get, "api/v1/eventos/tipos"), ct);
}

/// <summary>Sesiones de Validasoft para las pantallas embebidas (ID-3).</summary>
public sealed class SesionesApi
{
    private readonly Transporte _t;

    internal SesionesApi(Transporte transporte) => _t = transporte;

    /// <summary>
    /// Canjea la identidad delegada por un token de 30 minutos que el iframe recibe por <c>postMessage</c> y envía como
    /// <c>Authorization: Bearer</c>. Solo desde <see cref="IClienteValidasoft.ComoUsuario"/>.
    /// </summary>
    public Task<SesionUsuarioDto> CrearAsync(CancellationToken ct = default)
    {
        if (_t.Usuario is null)
            throw new InvalidOperationException("La sesión es de una persona: usa cliente.ComoUsuario(usuario).Sesiones.CrearAsync().");
        return _t.EnviarAsync<SesionUsuarioDto>(new Peticion(HttpMethod.Post, "api/v1/sesiones-usuario", ConEmpresa: false), ct);
    }
}

/// <summary>
/// El sandbox del grupo (docs/08 §2.2): un entorno de pruebas aislado al que apuntan las API keys <c>vsk_test_…</c>, con la
/// configuración del grupo, ningún dato real, los correos retenidos en un buzón consultable y los proveedores de IA y de listas
/// simulados. Piden el permiso <c>sandbox:administrar</c>; funcionan con la llave de producción y con la de prueba, siempre sobre
/// el sandbox.
/// </summary>
public sealed class SandboxApi
{
    private readonly Transporte _t;

    internal SandboxApi(Transporte transporte) => _t = transporte;

    /// <summary>Si existe, sus empresas (el <c>X-Empresa</c> de una llave de prueba es el <see cref="EmpresaDeSandboxDto.Id"/>) y cuántos correos tiene.</summary>
    public Task<SandboxDto> ObtenerAsync(CancellationToken ct = default) =>
        _t.EnviarAsync<SandboxDto>(new Peticion(HttpMethod.Get, "api/v1/sandbox", ConEmpresa: false), ct);

    /// <summary>Lo crea con la configuración del grupo o, si ya existe, le agrega lo que le falte de producción. Idempotente.</summary>
    public Task<SandboxDto> AsegurarAsync(CancellationToken ct = default) =>
        _t.EnviarAsync<SandboxDto>(new Peticion(HttpMethod.Post, "api/v1/sandbox", ConEmpresa: false, Reintentable: true), ct);

    /// <summary>Borra todo lo hecho en el sandbox y lo siembra de nuevo con la configuración de producción de ahora.</summary>
    public Task<SandboxDto> ReiniciarAsync(CancellationToken ct = default) =>
        _t.EnviarAsync<SandboxDto>(new Peticion(HttpMethod.Post, "api/v1/sandbox/reiniciar", ConEmpresa: false), ct);

    /// <summary>Los correos que el sandbox habría enviado, los más nuevos primero.</summary>
    public Task<PaginaDto<CorreoDePruebaDto>> CorreosAsync(string? para = null, string? asunto = null, int? limite = null, string? cursor = null,
        CancellationToken ct = default)
    {
        var parametros = new List<string>();
        if (!string.IsNullOrWhiteSpace(para))
            parametros.Add("para=" + ClienteValidasoft.Escapar(para));
        if (!string.IsNullOrWhiteSpace(asunto))
            parametros.Add("asunto=" + ClienteValidasoft.Escapar(asunto));
        if (limite is { } l)
            parametros.Add("limite=" + l.ToString(CultureInfo.InvariantCulture));
        if (!string.IsNullOrWhiteSpace(cursor))
            parametros.Add("cursor=" + ClienteValidasoft.Escapar(cursor));
        var ruta = "api/v1/sandbox/correos" + (parametros.Count == 0 ? string.Empty : "?" + string.Join("&", parametros));
        return _t.EnviarAsync<PaginaDto<CorreoDePruebaDto>>(new Peticion(HttpMethod.Get, ruta, ConEmpresa: false), ct);
    }

    /// <summary>
    /// Espera a que llegue al buzón un correo para <paramref name="para"/> (y, si se indica, cuyo asunto contenga
    /// <paramref name="asunto"/>) y lo devuelve; <see cref="TimeoutException"/> si no llega en <paramref name="espera"/> (20 s por omisión).
    /// El de la invitación trae el enlace del tercero en <see cref="CorreoDePruebaDto.Enlaces"/>.
    /// </summary>
    public async Task<CorreoDePruebaDto> EsperarCorreoAsync(string para, string? asunto = null, TimeSpan? espera = null, CancellationToken ct = default)
    {
        var limite = DateTime.UtcNow + (espera ?? TimeSpan.FromSeconds(20));
        while (true)
        {
            var pagina = await CorreosAsync(para, asunto, 1, null, ct);
            if (pagina.Datos.Count > 0)
                return pagina.Datos[0];
            if (DateTime.UtcNow >= limite)
                throw new TimeoutException($"No llegó al buzón del sandbox ningún correo para {para}.");
            await Task.Delay(250, ct);
        }
    }

    /// <summary>Un correo del buzón.</summary>
    public Task<CorreoDePruebaDto> CorreoAsync(Guid id, CancellationToken ct = default) =>
        _t.EnviarAsync<CorreoDePruebaDto>(new Peticion(HttpMethod.Get, $"api/v1/sandbox/correos/{id}", ConEmpresa: false), ct);

    /// <summary>Vacía el buzón.</summary>
    public Task VaciarCorreosAsync(CancellationToken ct = default) =>
        _t.EnviarAsync<SinContenido>(new Peticion(HttpMethod.Delete, "api/v1/sandbox/correos", ConEmpresa: false), ct);
}
