namespace Validasoft.Aplicacion.Comun;

/// <summary>Permisos (scopes) de docs/07 §3.2 usados en esta entrega. Cada uno es una política de autorización.</summary>
public static class Permisos
{
    public const string PlataformaTenants = "plataforma:tenants";
    public const string EmpresasAdministrar = "empresas:administrar";
    public const string ApiKeysAdministrar = "api-keys:administrar";
    public const string ConfiguracionLeer = "configuracion:leer";
    public const string ConfiguracionEscribir = "configuracion:escribir";
    public const string TercerosLeer = "terceros:leer";
    public const string TercerosEscribir = "terceros:escribir";
    public const string VinculosLeer = "vinculos:leer";
    public const string VinculosEscribir = "vinculos:escribir";
    public const string VinculosBloquear = "vinculos:bloquear";
    public const string AuditoriaLeer = "auditoria:leer";

    /// <summary>Todo lo que puede tener una credencial de tenant (la de Plataforma no entra aquí).</summary>
    public static readonly IReadOnlyList<string> DelTenant =
    [
        EmpresasAdministrar, ApiKeysAdministrar, ConfiguracionLeer, ConfiguracionEscribir,
        TercerosLeer, TercerosEscribir, VinculosLeer, VinculosEscribir, VinculosBloquear, AuditoriaLeer
    ];

    public static readonly IReadOnlyList<string> Todos = [PlataformaTenants, .. DelTenant];
}
