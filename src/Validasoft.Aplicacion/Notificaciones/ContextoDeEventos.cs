using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Integraciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>
/// Lecturas que necesitan el mapeo de eventos públicos y el plan de notificaciones (instantáneas de los agregados, áreas,
/// usuarios, suscripciones…). Una instancia vive lo que dura un lote de la suscripción: guarda en memoria lo leído.
/// </summary>
public interface IContextoDeEventos
{
    Task<Vinculo?> VinculoAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<Expediente?> ExpedienteAsync(Guid tenantId, Guid id, CancellationToken ct);

    /// <summary>Lote de invitación masiva (LT-9); por omisión ninguno (los contextos de prueba no lo necesitan).</summary>
    Task<Dominio.Invitaciones.LoteDeInvitacion?> LoteAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult<Dominio.Invitaciones.LoteDeInvitacion?>(null);

    /// <summary>Campaña de renovación (OP-1); por omisión ninguna (los contextos de prueba no la necesitan).</summary>
    Task<Dominio.Campanias.CampaniaDeRenovacion?> CampaniaAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        Task.FromResult<Dominio.Campanias.CampaniaDeRenovacion?>(null);

    Task<TipoVinculo?> TipoVinculoAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<TipoDocumentoTenant?> TipoDocumentoAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<Tercero?> TerceroAsync(Guid id, CancellationToken ct);

    Task<Documento?> DocumentoAsync(Guid id, CancellationToken ct);

    /// <summary>Sección de la ficha del tercero (stream <c>SeccionDeFicha</c> de la bóveda), con todas sus versiones (inmutables).</summary>
    Task<SeccionDeFicha?> SeccionDeFichaAsync(Guid id, CancellationToken ct);

    /// <summary>Las entradas de los catálogos de plataforma a las que refieren unos datos de la ficha (nombres de códigos).</summary>
    Task<ICatalogosDeFicha> CatalogosDeFichaAsync(IEnumerable<ReferenciaACatalogo> referencias, CancellationToken ct);

    Task<Certificado?> CertificadoAsync(Guid tenantId, Guid id, CancellationToken ct);

    Task<Empresa?> EmpresaAsync(Guid id, CancellationToken ct);

    Task<IReadOnlyList<Area>> AreasAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Las áreas de una empresa (quién aprueba las invitaciones se designa por el código de un área, decisión AI); por omisión ninguna (los contextos de prueba no la necesitan).</summary>
    Task<IReadOnlyList<Area>> AreasDeLaEmpresaAsync(Guid tenantId, Guid empresaId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Area>>([]);

    Task<IReadOnlyList<Usuario>> UsuariosAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Delegaciones de revisión no revocadas de la empresa (OP-7): quienes las reciben se enteran de lo que le llega al área.</summary>
    Task<IReadOnlyList<Dominio.Delegaciones.DelegacionDeRevision>> DelegacionesAsync(Guid tenantId, Guid empresaId, CancellationToken ct) =>
        Task.FromResult<IReadOnlyList<Dominio.Delegaciones.DelegacionDeRevision>>([]);

    /// <summary>Configuración del SLA de la empresa (OP-10); por omisión la de fábrica.</summary>
    Task<Expedientes.ConfiguracionDeSla> ConfiguracionDeSlaAsync(Guid tenantId, Guid empresaId, CancellationToken ct) =>
        Task.FromResult(new Expedientes.ConfiguracionDeSla { Id = empresaId });

    Task<PreferenciasNotificacion?> PreferenciasAsync(Guid tenantId, Guid usuarioId, CancellationToken ct);

    /// <summary>Los vínculos de un tercero con todas las empresas (de cualquier tenant): a quiénes les cambia su ficha (NT-12).</summary>
    Task<IReadOnlyList<Vinculo>> VinculosDelTerceroAsync(Guid terceroId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Vinculo>>([]);

    /// <summary>Usuarios activos del tenant (los avisos de cumplimiento van por rol, VE-13).</summary>
    Task<IReadOnlyList<Usuario>> UsuariosDelTenantAsync(Guid tenantId, CancellationToken ct) => Task.FromResult<IReadOnlyList<Usuario>>([]);

    Task<IReadOnlyList<TextoLegal>> TextosLegalesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct);

    /// <summary>Suscripciones activas del tenant (las desactivadas no reciben entregas nuevas).</summary>
    Task<IReadOnlyList<SuscripcionWebhook>> SuscripcionesActivasAsync(Guid tenantId, CancellationToken ct);

    /// <summary>De <paramref name="ids"/>, los que ya existen como documentos <typeparamref name="T"/> del tenant (reproceso idempotente).</summary>
    Task<IReadOnlySet<Guid>> ExistentesAsync<T>(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) where T : notnull;
}

/// <summary>Implementación sobre el <c>DocumentStore</c>: una sesión de consulta por tenant (y la global y la de la bóveda) por lote.</summary>
public sealed class ContextoDeEventos(IDocumentStore store) : IContextoDeEventos, IAsyncDisposable
{
    private readonly Dictionary<string, IQuerySession> _sesiones = [];
    private readonly Dictionary<(Type, Guid), object?> _cache = [];
    private readonly Dictionary<Guid, IReadOnlyList<SuscripcionWebhook>> _suscripciones = [];

    private IQuerySession Sesion(string? tenant)
    {
        var clave = tenant ?? string.Empty;
        if (!_sesiones.TryGetValue(clave, out var sesion))
            _sesiones[clave] = sesion = tenant is null ? store.QuerySession() : store.QuerySession(tenant);
        return sesion;
    }

    private static string Tenant(Guid tenantId) => tenantId.ToString("N");

    private async Task<T?> CargarAsync<T>(string? tenant, Guid id, CancellationToken ct) where T : class
    {
        if (_cache.TryGetValue((typeof(T), id), out var guardado))
            return (T?)guardado;
        var valor = await Sesion(tenant).LoadAsync<T>(id, ct);
        _cache[(typeof(T), id)] = valor;
        return valor;
    }

    public Task<Vinculo?> VinculoAsync(Guid tenantId, Guid id, CancellationToken ct) => CargarAsync<Vinculo>(Tenant(tenantId), id, ct);

    public Task<Expediente?> ExpedienteAsync(Guid tenantId, Guid id, CancellationToken ct) => CargarAsync<Expediente>(Tenant(tenantId), id, ct);

    public Task<Dominio.Invitaciones.LoteDeInvitacion?> LoteAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        CargarAsync<Dominio.Invitaciones.LoteDeInvitacion>(Tenant(tenantId), id, ct);

    public Task<Dominio.Campanias.CampaniaDeRenovacion?> CampaniaAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        CargarAsync<Dominio.Campanias.CampaniaDeRenovacion>(Tenant(tenantId), id, ct);

    public Task<TipoVinculo?> TipoVinculoAsync(Guid tenantId, Guid id, CancellationToken ct) => CargarAsync<TipoVinculo>(Tenant(tenantId), id, ct);

    public Task<TipoDocumentoTenant?> TipoDocumentoAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        CargarAsync<TipoDocumentoTenant>(Tenant(tenantId), id, ct);

    public Task<Tercero?> TerceroAsync(Guid id, CancellationToken ct) => CargarAsync<Tercero>(null, id, ct);

    public Task<Documento?> DocumentoAsync(Guid id, CancellationToken ct) => CargarAsync<Documento>(TenantsTecnicos.Boveda, id, ct);

    public Task<Empresa?> EmpresaAsync(Guid id, CancellationToken ct) => CargarAsync<Empresa>(null, id, ct);

    public Task<SeccionDeFicha?> SeccionDeFichaAsync(Guid id, CancellationToken ct) => CargarAsync<SeccionDeFicha>(TenantsTecnicos.Boveda, id, ct);

    public Task<ICatalogosDeFicha> CatalogosDeFichaAsync(IEnumerable<ReferenciaACatalogo> referencias, CancellationToken ct) =>
        Aplicacion.Ficha.ImportacionDeCatalogos.CargarAsync(Sesion(null), referencias, ct);

    public Task<Certificado?> CertificadoAsync(Guid tenantId, Guid id, CancellationToken ct) =>
        CargarAsync<Certificado>(Tenant(tenantId), id, ct);

    public async Task<IReadOnlyList<Area>> AreasAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : [.. await Sesion(Tenant(tenantId)).LoadManyAsync<Area>(ct, ids.Distinct().ToArray())];

    public async Task<IReadOnlyList<Area>> AreasDeLaEmpresaAsync(Guid tenantId, Guid empresaId, CancellationToken ct) =>
        [.. await Sesion(Tenant(tenantId)).Query<Area>().Where(a => a.EmpresaId == empresaId).ToListAsync(ct)];

    public async Task<IReadOnlyList<Usuario>> UsuariosAsync(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : [.. await Sesion(Tenant(tenantId)).LoadManyAsync<Usuario>(ct, ids.Distinct().ToArray())];

    public async Task<IReadOnlyList<Dominio.Delegaciones.DelegacionDeRevision>> DelegacionesAsync(Guid tenantId, Guid empresaId, CancellationToken ct) =>
        [.. await Sesion(Tenant(tenantId)).Query<Dominio.Delegaciones.DelegacionDeRevision>().Where(d => d.EmpresaId == empresaId && d.RevocadaEn == null).ToListAsync(ct)];

    public async Task<Expedientes.ConfiguracionDeSla> ConfiguracionDeSlaAsync(Guid tenantId, Guid empresaId, CancellationToken ct) =>
        await CargarAsync<Expedientes.ConfiguracionDeSla>(Tenant(tenantId), empresaId, ct) ?? new Expedientes.ConfiguracionDeSla { Id = empresaId };

    public async Task<IReadOnlyList<Usuario>> UsuariosDelTenantAsync(Guid tenantId, CancellationToken ct) =>
        [.. await Sesion(Tenant(tenantId)).Query<Usuario>().Where(u => u.Activo).ToListAsync(ct)];

    public async Task<IReadOnlyList<Vinculo>> VinculosDelTerceroAsync(Guid terceroId, CancellationToken ct) =>
        [.. await Sesion(null).Query<Vinculo>().Where(v => v.AnyTenant() && v.TerceroId == terceroId).ToListAsync(ct)];

    public Task<PreferenciasNotificacion?> PreferenciasAsync(Guid tenantId, Guid usuarioId, CancellationToken ct) =>
        CargarAsync<PreferenciasNotificacion>(Tenant(tenantId), usuarioId, ct);

    public async Task<IReadOnlyList<TextoLegal>> TextosLegalesAsync(IReadOnlyCollection<Guid> ids, CancellationToken ct) =>
        ids.Count == 0 ? [] : [.. await Sesion(null).LoadManyAsync<TextoLegal>(ct, ids.Distinct().ToArray())];

    public async Task<IReadOnlyList<SuscripcionWebhook>> SuscripcionesActivasAsync(Guid tenantId, CancellationToken ct)
    {
        if (_suscripciones.TryGetValue(tenantId, out var guardadas))
            return guardadas;
        var activas = await Sesion(Tenant(tenantId)).Query<SuscripcionWebhook>().Where(s => s.Activa).OrderBy(s => s.CreadaEn).ToListAsync(ct);
        return _suscripciones[tenantId] = [.. activas];
    }

    public async Task<IReadOnlySet<Guid>> ExistentesAsync<T>(Guid tenantId, IReadOnlyCollection<Guid> ids, CancellationToken ct) where T : notnull
    {
        if (ids.Count == 0)
            return new HashSet<Guid>();
        var encontrados = await Sesion(Tenant(tenantId)).LoadManyAsync<T>(ct, ids.Distinct().ToArray());
        return encontrados.Select(d => (Guid)typeof(T).GetProperty("Id")!.GetValue(d)!).ToHashSet();
    }

    public async ValueTask DisposeAsync()
    {
        foreach (var sesion in _sesiones.Values)
            await sesion.DisposeAsync();
        _sesiones.Clear();
    }
}
