using Validasoft.Dominio.Identidad;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Permisos (scopes) de docs/07 §3.2 usados en esta entrega. Cada uno es una política de autorización.</summary>
public static class Permisos
{
    public const string PlataformaTenants = "plataforma:tenants";
    public const string EmpresasAdministrar = "empresas:administrar";
    public const string ApiKeysAdministrar = "api-keys:administrar";
    public const string SistemasAdministrar = "sistemas:administrar";
    public const string UsuariosAdministrar = "usuarios:administrar";
    public const string ConfiguracionLeer = "configuracion:leer";
    public const string ConfiguracionEscribir = "configuracion:escribir";
    public const string TercerosLeer = "terceros:leer";
    public const string TercerosEscribir = "terceros:escribir";
    public const string VinculosLeer = "vinculos:leer";
    public const string VinculosEscribir = "vinculos:escribir";
    public const string VinculosBloquear = "vinculos:bloquear";

    /// <summary>Aprobar o rechazar la cuenta bancaria que designa el tercero (cuatro ojos, CB-2).</summary>
    public const string CuentasAprobar = "cuentas:aprobar";
    public const string AuditoriaLeer = "auditoria:leer";
    public const string FlujosLeer = "flujos:leer";
    public const string FlujosEscribir = "flujos:escribir";
    public const string ExpedientesLeer = "expedientes:leer";

    /// <summary>Emitir veredictos y rechazar (además hay que ser miembro activo del área).</summary>
    public const string ExpedientesRevisar = "expedientes:revisar";

    /// <summary>Abrir, anular, reenviar como integrador, aprobar en nombre de un área y anular veredictos.</summary>
    public const string ExpedientesAdministrar = "expedientes:administrar";

    /// <summary>Ver los documentos compartidos con un vínculo de la empresa (versiones, hechos).</summary>
    public const string DocumentosLeer = "documentos:leer";

    /// <summary>Emitir URLs prefirmadas de descarga; cada emisión queda como <c>DocumentoAccedido</c>.</summary>
    public const string DocumentosDescargar = "documentos:descargar";

    /// <summary>Ver los certificados de la empresa y descargar su PDF (cada descarga se audita). Revocar pide <c>expedientes:administrar</c>.</summary>
    public const string CertificadosLeer = "certificados:leer";

    /// <summary>Leer los eventos públicos en modo pull y por SSE (<c>GET /eventos</c>, NW-1).</summary>
    public const string EventosLeer = "eventos:leer";

    /// <summary>Crear y gestionar suscripciones de webhooks y sus entregas (NW-4).</summary>
    public const string WebhooksAdministrar = "webhooks:administrar";

    /// <summary>Todo lo que puede tener una credencial de tenant (la de Plataforma no entra aquí).</summary>
    public static readonly IReadOnlyList<string> DelTenant =
    [
        EmpresasAdministrar, ApiKeysAdministrar, SistemasAdministrar, UsuariosAdministrar, ConfiguracionLeer, ConfiguracionEscribir,
        TercerosLeer, TercerosEscribir, VinculosLeer, VinculosEscribir, VinculosBloquear, CuentasAprobar, AuditoriaLeer,
        FlujosLeer, FlujosEscribir, ExpedientesLeer, ExpedientesRevisar, ExpedientesAdministrar, DocumentosLeer, DocumentosDescargar,
        CertificadosLeer, EventosLeer, WebhooksAdministrar
    ];

    public static readonly IReadOnlyList<string> Todos = [PlataformaTenants, .. DelTenant];

    /// <summary>
    /// Lo que puede la Plataforma con un acceso de soporte vigente (B-6): leer configuración, flujos, vínculos, terceros,
    /// expedientes, certificados y auditoría de esa empresa. Nunca documentos (ni verlos ni descargarlos) ni escribir.
    /// </summary>
    public static readonly IReadOnlyList<string> DeSoporte =
        [ConfiguracionLeer, FlujosLeer, VinculosLeer, TercerosLeer, ExpedientesLeer, CertificadosLeer, AuditoriaLeer];

    /// <summary>
    /// Administración del tenant: solo se obtiene con un rol de todo el tenant. Un administrador de una empresa no
    /// gestiona usuarios, sistemas, llaves ni empresas (evita que se otorgue alcance de tenant a sí mismo).
    /// </summary>
    public static readonly IReadOnlySet<string> SoloDeTenant =
        new HashSet<string> { EmpresasAdministrar, ApiKeysAdministrar, SistemasAdministrar, UsuariosAdministrar };

    /// <summary>Lo que todo rol lee; los documentos compartidos y los certificados se leen y descargan con cualquier rol (docs/07 §3.2).</summary>
    private static readonly string[] Lecturas =
        [ConfiguracionLeer, TercerosLeer, VinculosLeer, FlujosLeer, ExpedientesLeer, DocumentosLeer, DocumentosDescargar, CertificadosLeer];

    /// <summary>Mapeo rol → scopes (docs/07 §3.1).</summary>
    public static IReadOnlyList<string> DeRol(Rol rol) => rol switch
    {
        Rol.Administrador => DelTenant,
        Rol.Revisor => [.. Lecturas, VinculosEscribir, ExpedientesRevisar],
        Rol.OficialCumplimiento => [.. Lecturas, VinculosEscribir, VinculosBloquear, CuentasAprobar, ExpedientesRevisar],
        Rol.Auditor => [.. Lecturas, AuditoriaLeer],
        _ => []
    };

    /// <summary>
    /// Permiso efectivo de una petición delegada (ADR-0011): <b>intersección</b> de lo que permite la API key del
    /// sistema y lo que dan los roles del usuario. Los scopes de roles por empresa solo valen en esa empresa
    /// (<see cref="PermisosEfectivos.PorEmpresa"/>); las empresas efectivas son las del usuario (<c>*</c> si tiene un
    /// rol de todo el tenant) recortadas por las de la llave.
    /// </summary>
    public static PermisosEfectivos ParaUsuario(IEnumerable<RolAsignado> roles, IEnumerable<string> scopesLlave, IEnumerable<string> empresasLlave)
    {
        var llave = scopesLlave.ToHashSet();
        var empresasDeLlave = empresasLlave.ToHashSet();
        var lista = roles.ToList();

        var deTenant = lista.Where(r => r.EmpresaId is null).SelectMany(r => DeRol(r.Rol)).Where(llave.Contains).ToHashSet();

        var porEmpresa = new Dictionary<Guid, IReadOnlySet<string>>();
        foreach (var grupo in lista.Where(r => r.EmpresaId is not null).GroupBy(r => r.EmpresaId!.Value))
        {
            if (!PermiteEmpresa(empresasDeLlave, grupo.Key))
                continue;

            var scopes = grupo.SelectMany(r => DeRol(r.Rol))
                .Where(s => llave.Contains(s) && !SoloDeTenant.Contains(s) && !deTenant.Contains(s))
                .ToHashSet();
            if (scopes.Count > 0)
                porEmpresa[grupo.Key] = scopes;
        }

        HashSet<string> empresasUsuario = lista.Any(r => r.EmpresaId is null)
            ? ["*"]
            : [.. lista.Select(r => r.EmpresaId!.Value.ToString())];
        var empresas = Intersectar(empresasDeLlave, empresasUsuario);

        return new PermisosEfectivos(deTenant, porEmpresa, empresas);
    }

    /// <summary>Intersección de alcances de empresa donde <c>*</c> significa "todas".</summary>
    public static IReadOnlySet<string> Intersectar(IReadOnlySet<string> a, IReadOnlySet<string> b)
    {
        if (a.Contains("*"))
            return b.ToHashSet();
        if (b.Contains("*"))
            return a.ToHashSet();
        return a.Where(b.Contains).ToHashSet();
    }

    private static bool PermiteEmpresa(IReadOnlySet<string> empresas, Guid empresaId) =>
        empresas.Contains("*") || empresas.Contains(empresaId.ToString());
}

/// <summary>Resultado de <see cref="Permisos.ParaUsuario"/>.</summary>
/// <param name="DeTenant">Scopes válidos en cualquier empresa efectiva y en las operaciones de tenant.</param>
/// <param name="PorEmpresa">Scopes adicionales que solo valen con <c>X-Empresa</c> igual a esa empresa.</param>
/// <param name="Empresas">Empresas efectivas; <c>*</c> = todas las del tenant.</param>
public sealed record PermisosEfectivos(
    IReadOnlySet<string> DeTenant,
    IReadOnlyDictionary<Guid, IReadOnlySet<string>> PorEmpresa,
    IReadOnlySet<string> Empresas)
{
    /// <summary>Scopes que aplican en una petición con la empresa activa indicada (o sin empresa).</summary>
    public IReadOnlySet<string> EnEmpresa(Guid? empresaId) =>
        empresaId is { } id && PorEmpresa.TryGetValue(id, out var adicionales)
            ? DeTenant.Union(adicionales).ToHashSet()
            : DeTenant;
}
