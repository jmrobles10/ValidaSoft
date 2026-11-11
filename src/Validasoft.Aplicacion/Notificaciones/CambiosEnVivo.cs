using System.Security.Claims;
using System.Text.Json;
using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Notificaciones;

// Cambios en vivo (NT-12): además de las notificaciones, el SSE lleva avisos de «cambió X» (tipo de recurso + id) para que la
// pantalla abierta refresque sola lo que muestra. El procesador de eventos —que ve todos los hechos de dominio en orden— escribe un
// `AvisoDeCambio` por destinatario (la empresa del recurso, el tercero) y recurso por lote; los streams de la web de empresa y del
// portal del tercero los leen y los mandan solo a quien corresponde. No llevan datos: el cliente vuelve a pedir lo que ya veía por
// la API de siempre, con sus permisos.

/// <summary>A quién va un aviso: a la web de una empresa (su tenant y su empresa) o al portal de un tercero.</summary>
public enum DestinoDeCambio
{
    Empresa,
    Tercero
}

/// <summary>Tipos de recurso de un aviso de cambio (los que el cliente sabe refrescar).</summary>
public static class RecursosDeCambio
{
    public const string Expediente = "expediente";
    public const string Vinculo = "vinculo";
    public const string Cuenta = "cuenta";
    public const string Documento = "documento";
    public const string Ficha = "ficha";
    public const string Certificado = "certificado";
}

/// <summary>
/// «Cambió algo» para una empresa o un tercero. Documento global de vida corta (el procesador borra los de más de
/// <see cref="Vida"/>): un stream abierto lo lee con <see cref="Orden"/> (el mismo orden de las notificaciones: instante del hecho más su
/// secuencia). Una empresa solo recibe los de su <see cref="TenantId"/> y <see cref="EmpresaId"/>; un tercero, los suyos
/// (<see cref="TerceroId"/>) de los vínculos de su sesión, o sin vínculo.
/// </summary>
public sealed class AvisoDeCambio
{
    /// <summary>Lo que se conserva un aviso: un stream que se cae y vuelve a conectar refresca todo, no recupera lo que se perdió.</summary>
    public static readonly TimeSpan Vida = TimeSpan.FromMinutes(30);

    public Guid Id { get; set; }

    public long Orden { get; set; }

    public DestinoDeCambio Destino { get; set; }

    public Guid? TenantId { get; set; }

    public Guid? EmpresaId { get; set; }

    public Guid? TerceroId { get; set; }

    public Guid? VinculoId { get; set; }

    public string Recurso { get; set; } = string.Empty;

    public Guid RecursoId { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    /// <summary>Lo que viaja por el stream (<c>event: cambio</c>): tipo de recurso, id y vínculo; nunca datos.</summary>
    public CambioEnVivoDto ADto() => new(Recurso, RecursoId, VinculoId);
}

/// <summary>Cuerpo del evento SSE <c>cambio</c>: qué cambió (tipo e id) y, si es de un vínculo, cuál.</summary>
public sealed record CambioEnVivoDto(string Recurso, Guid Id, Guid? VinculoId);

/// <summary>
/// Qué avisos de cambio produce un hecho de dominio. La familia del hecho (expediente, vínculo, documento, ficha, certificado) sale del
/// espacio de nombres de su tipo; el resto (invitaciones masivas, secuencias de folio) no se refleja en pantallas de datos.
/// </summary>
public static class PlanificadorDeCambios
{
    private static readonly IReadOnlyDictionary<string, string> FamiliaPorEspacio = new Dictionary<string, string>(StringComparer.Ordinal)
    {
        ["Validasoft.Dominio.Expedientes"] = RecursosDeCambio.Expediente,
        ["Validasoft.Dominio.Vinculos"] = RecursosDeCambio.Vinculo,
        ["Validasoft.Dominio.Consentimientos"] = RecursosDeCambio.Vinculo,
        ["Validasoft.Dominio.Documentos"] = RecursosDeCambio.Documento,
        ["Validasoft.Dominio.Ficha"] = RecursosDeCambio.Ficha,
        ["Validasoft.Dominio.Certificados"] = RecursosDeCambio.Certificado
    };

    public static async Task<IReadOnlyList<AvisoDeCambio>> PlanearAsync(IEvent evento, IContextoDeEventos contexto, long orden, CancellationToken ct)
    {
        var tipo = evento.Data.GetType();
        if (tipo.Namespace is not { } espacio || !FamiliaPorEspacio.TryGetValue(espacio, out var familia) ||
            tipo.Name is nameof(Dominio.Certificados.SecuenciaDeFoliosIniciada) or nameof(Dominio.Certificados.FolioConsumido))
            return [];

        var avisos = new List<AvisoDeCambio>();
        void Agregar(DestinoDeCambio destino, Guid? tenantId, Guid? empresaId, Guid? terceroId, Guid? vinculoId, string recurso, Guid recursoId) =>
            avisos.Add(new AvisoDeCambio
            {
                Id = Deterministas.Guid($"cambio:{evento.Sequence}:{evento.Id:N}:{destino}:{tenantId:N}:{empresaId:N}:{terceroId:N}:{vinculoId:N}:{recurso}:{recursoId:N}"),
                Orden = orden,
                Destino = destino,
                TenantId = tenantId,
                EmpresaId = empresaId,
                TerceroId = terceroId,
                VinculoId = vinculoId,
                Recurso = recurso,
                RecursoId = recursoId,
                CreadaEn = evento.Timestamp
            });

        // Un recurso de un vínculo (expediente, certificado, el vínculo mismo): lo ve su empresa y su tercero.
        void DeUnVinculo(Guid tenantId, Guid empresaId, Guid terceroId, Guid vinculoId, string recurso, Guid recursoId)
        {
            Agregar(DestinoDeCambio.Empresa, tenantId, empresaId, null, vinculoId, recurso, recursoId);
            Agregar(DestinoDeCambio.Tercero, tenantId, empresaId, terceroId, vinculoId, recurso, recursoId);
        }

        switch (familia)
        {
            case RecursosDeCambio.Expediente:
                if (Guid.TryParse(evento.TenantId, out var tenantExpediente) && await contexto.ExpedienteAsync(tenantExpediente, evento.StreamId, ct) is { } expediente)
                    DeUnVinculo(tenantExpediente, expediente.EmpresaId, expediente.TerceroId, expediente.VinculoId, RecursosDeCambio.Expediente, expediente.Id);
                break;
            case RecursosDeCambio.Vinculo:
                if (Guid.TryParse(evento.TenantId, out var tenantVinculo) && await contexto.VinculoAsync(tenantVinculo, evento.StreamId, ct) is { } vinculo)
                {
                    DeUnVinculo(tenantVinculo, vinculo.EmpresaId, vinculo.TerceroId, vinculo.Id, RecursosDeCambio.Vinculo, vinculo.Id);
                    if (CuentaDe(evento.Data) is { } cuentaId)
                        DeUnVinculo(tenantVinculo, vinculo.EmpresaId, vinculo.TerceroId, vinculo.Id, RecursosDeCambio.Cuenta, cuentaId);
                }

                break;
            case RecursosDeCambio.Certificado:
                if (Guid.TryParse(evento.TenantId, out var tenantCertificado) && await contexto.CertificadoAsync(tenantCertificado, evento.StreamId, ct) is { } certificado)
                    DeUnVinculo(tenantCertificado, certificado.EmpresaId, certificado.TerceroId, certificado.VinculoId, RecursosDeCambio.Certificado, certificado.Id);
                break;
            case RecursosDeCambio.Documento:
                if (await contexto.DocumentoAsync(evento.StreamId, ct) is { } documento)
                {
                    Agregar(DestinoDeCambio.Tercero, null, null, documento.TerceroId, null, RecursosDeCambio.Documento, documento.Id);
                    // Cada empresa con la que se compartió (o a la que pertenece, si es de un vínculo) ve el cambio de lo que ve de ese documento.
                    var destinos = documento.Comparticiones.Select(c => (c.TenantId, c.EmpresaId, c.VinculoId)).ToList();
                    if (documento is { TenantId: { } tenantDoc, EmpresaId: { } empresaDoc, VinculoId: { } vinculoDoc })
                        destinos.Add((tenantDoc, empresaDoc, vinculoDoc));
                    foreach (var (tenantId, empresaId, vinculoId) in destinos.Distinct())
                        Agregar(DestinoDeCambio.Empresa, tenantId, empresaId, null, vinculoId, RecursosDeCambio.Documento, documento.Id);
                }

                break;
            case RecursosDeCambio.Ficha:
                if (await contexto.SeccionDeFichaAsync(evento.StreamId, ct) is { } seccion)
                {
                    Agregar(DestinoDeCambio.Tercero, null, null, seccion.TerceroId, null, RecursosDeCambio.Ficha, seccion.Id);
                    // La ficha es del tercero y la ven todas las empresas con las que tiene un vínculo.
                    foreach (var propio in await contexto.VinculosDelTerceroAsync(seccion.TerceroId, ct))
                        if (await contexto.EmpresaAsync(propio.EmpresaId, ct) is { } empresa)
                            Agregar(DestinoDeCambio.Empresa, empresa.TenantId, propio.EmpresaId, null, propio.Id, RecursosDeCambio.Ficha, seccion.Id);
                }

                break;
        }

        return avisos;
    }

    /// <summary>El id de la cuenta bancaria a la que se refiere un hecho de la cuenta designada del vínculo, si lo es.</summary>
    private static Guid? CuentaDe(object datos) => datos switch
    {
        CuentaDesignadaPropuesta p => p.Cuenta.CuentaId,
        CuentaDesignadaAprobada a => a.CuentaId,
        CuentaDesignadaRechazada r => r.CuentaId,
        _ => null
    };

    /// <summary>Los avisos de un lote sin repetir: un recurso que cambió varias veces en el lote se avisa una vez a cada destinatario.</summary>
    public static IReadOnlyList<AvisoDeCambio> Coalescer(IEnumerable<AvisoDeCambio> avisos) =>
    [
        .. avisos.GroupBy(a => (a.Destino, a.TenantId, a.EmpresaId, a.TerceroId, a.VinculoId, a.Recurso, a.RecursoId)).Select(g => g.OrderBy(a => a.Orden).Last())
    ];
}

/// <summary>Lo que el stream de un tercero necesita saber de su sesión: quién es, a qué vínculos tiene acceso y cuándo vence el token.</summary>
public sealed record AlcanceDelStream(Guid TerceroId, IReadOnlyList<Guid> Vinculos, DateTimeOffset? Vence)
{
    public static AlcanceDelStream De(ClaimsPrincipal usuario)
    {
        var sesion = SesionDeTercero.De(usuario);
        var exp = usuario.FindFirstValue("exp");
        return new AlcanceDelStream(sesion.TerceroId, [.. sesion.Vinculos.Select(v => v.VinculoId)],
            long.TryParse(exp, out var segundos) ? DateTimeOffset.FromUnixTimeSeconds(segundos) : null);
    }
}

/// <summary>
/// Dónde va un stream leyendo los avisos de cambio. El orden de un aviso es el instante de su hecho, y el procesador de eventos los
/// escribe en orden de secuencia, no de instante: un aviso puede aparecer con un orden algo menor que el último que ya se mandó. Por eso
/// se lee con un <see cref="Solape"/> hacia atrás y se recuerda qué se mandó (un aviso repetido no sale dos veces).
/// </summary>
public sealed class CursorDeCambios(DateTimeOffset desde)
{
    public static readonly TimeSpan Solape = TimeSpan.FromSeconds(5);

    private long _ultimo = desde.UtcTicks;
    private readonly Dictionary<Guid, long> _vistos = [];

    /// <summary>Desde dónde leer en la siguiente vuelta.</summary>
    public long Desde => _ultimo - Solape.Ticks;

    /// <summary>De lo leído, lo que aún no se mandó; avanza el cursor y olvida lo que ya quedó fuera del solape.</summary>
    public IReadOnlyList<AvisoDeCambio> Nuevos(IEnumerable<AvisoDeCambio> leidos)
    {
        var nuevos = new List<AvisoDeCambio>();
        foreach (var aviso in leidos)
        {
            if (_vistos.TryAdd(aviso.Id, aviso.Orden))
                nuevos.Add(aviso);
            _ultimo = Math.Max(_ultimo, aviso.Orden);
        }

        foreach (var id in _vistos.Where(v => v.Value < Desde).Select(v => v.Key).ToList())
            _vistos.Remove(id);
        return nuevos;
    }
}

/// <summary>Escritura y lectura de los avisos de cambio para los streams SSE.</summary>
public static class CambiosEnVivo
{
    public static readonly JsonSerializerOptions Json = new(JsonSerializerDefaults.Web);

    /// <summary>Desde cuándo lee un stream recién abierto: unos segundos antes de ahora, por lo que el procesador aún no había escrito.</summary>
    public static DateTimeOffset InicioDeUnStream(TimeProvider reloj) => reloj.GetUtcNow() - TimeSpan.FromSeconds(10);

    /// <summary>Los avisos más nuevos que <paramref name="desde"/> para una empresa (su tenant y su empresa), en orden.</summary>
    public static async Task<IReadOnlyList<AvisoDeCambio>> DeLaEmpresaAsync(IQuerySession consulta, Guid tenantId, Guid empresaId, long desde, CancellationToken ct)
    {
        Guid? empresa = empresaId;
        Guid? tenant = tenantId;
        return await consulta.Query<AvisoDeCambio>()
            .Where(a => a.Destino == DestinoDeCambio.Empresa && a.TenantId == tenant && a.EmpresaId == empresa && a.Orden > desde)
            .OrderBy(a => a.Orden).Take(200).ToListAsync(ct);
    }

    /// <summary>Los avisos más nuevos que <paramref name="desde"/> de un tercero: los suyos, de los vínculos de su sesión o sin vínculo.</summary>
    public static async Task<IReadOnlyList<AvisoDeCambio>> DelTerceroAsync(IQuerySession consulta, AlcanceDelStream alcance, long desde, CancellationToken ct)
    {
        var terceroId = alcance.TerceroId;
        Guid?[] vinculos = [.. alcance.Vinculos.Select(v => (Guid?)v)];
        return await consulta.Query<AvisoDeCambio>()
            .Where(a => a.Destino == DestinoDeCambio.Tercero && a.TerceroId == terceroId && a.Orden > desde && (a.VinculoId == null || a.VinculoId.IsOneOf(vinculos)))
            .OrderBy(a => a.Orden).Take(200).ToListAsync(ct);
    }

    /// <summary>Escribe los avisos como eventos <c>cambio</c> (sin <c>id</c>: reanudar no los repite, el cliente refresca todo al reconectar).</summary>
    public static async Task EscribirAsync(HttpContext http, IEnumerable<AvisoDeCambio> avisos)
    {
        foreach (var aviso in avisos.DistinctBy(a => (a.Recurso, a.RecursoId, a.VinculoId)))
            await Sse.EscribirSinIdAsync(http, "cambio", JsonSerializer.Serialize(aviso.ADto(), Json));
    }
}
