namespace Validasoft.Dominio.Identidad;

/// <summary>
/// Llave de integración de un tenant (docs/07 §4). Solo se guarda el hash Argon2id; el valor completo
/// se muestra una única vez al crearla. Esquema <c>identidad</c>, multi-tenant.
/// </summary>
public sealed class ApiKey
{
    public Guid Id { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Primeros 8 caracteres del secreto: permite buscar la llave sin exponerla.</summary>
    public string Prefijo { get; set; } = string.Empty;

    public string Hash { get; set; } = string.Empty;

    public List<string> Scopes { get; set; } = [];

    /// <summary>Empresas internas permitidas; <c>*</c> = todas las del tenant.</summary>
    public List<string> Empresas { get; set; } = [];

    /// <summary>
    /// Sistema integrado dueño de la llave (ADR-0011): fija el sistema de las referencias externas y la clave pública
    /// con la que se verifican sus aserciones delegadas. Nulo en llaves de administración.
    /// </summary>
    public Guid? SistemaId { get; set; }

    public bool Sandbox { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset? ExpiraEn { get; set; }

    public DateTimeOffset? RevocadaEn { get; set; }

    public bool PermiteEmpresa(Guid empresaId) =>
        Empresas.Contains("*") || Empresas.Contains(empresaId.ToString());

    public bool VigenteEn(DateTimeOffset ahora) =>
        RevocadaEn is null && (ExpiraEn is null || ExpiraEn > ahora);
}
