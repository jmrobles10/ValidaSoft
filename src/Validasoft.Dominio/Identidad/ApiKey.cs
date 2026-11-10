using Validasoft.Dominio.Comun;

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

    /// <summary>Llave que esta reemplazó al rotarse (CN-3).</summary>
    public Guid? RotadaDe { get; set; }

    /// <summary>Llave nueva que reemplaza a esta; desde la rotación esta solo vale hasta <see cref="ExpiraEn"/>.</summary>
    public Guid? ReemplazadaPor { get; set; }

    /// <summary>Lo más lejos que puede seguir valiendo la llave anterior tras una rotación.</summary>
    public static readonly TimeSpan SolapamientoMaximo = TimeSpan.FromDays(30);

    /// <summary>Solapamiento cuando no se indica fecha.</summary>
    public static readonly TimeSpan SolapamientoPorDefecto = TimeSpan.FromHours(24);

    /// <summary>
    /// Rotación con solapamiento (CN-3): la llave queda reemplazada por <paramref name="nueva"/> y vale hasta
    /// <paramref name="validaHasta"/> (nulo = 24 h; ahora = corte inmediato), sin pasar de 30 días ni de su propia expiración.
    /// Devuelve la fecha efectiva. 409 si ya estaba rotada, revocada o vencida; 422 si la fecha es pasada o excede el máximo.
    /// </summary>
    public DateTimeOffset RotarHacia(Guid nueva, DateTimeOffset? validaHasta, DateTimeOffset ahora)
    {
        if (ReemplazadaPor is not null)
            throw ReglaDeNegocioException.Conflicto("api-key-ya-rotada", "Esta API key ya se rotó: rota la nueva.");
        if (!VigenteEn(ahora))
            throw ReglaDeNegocioException.Conflicto("api-key-vencida", "La API key está vencida o revocada: crea una nueva.");

        var hasta = validaHasta ?? ahora + SolapamientoPorDefecto;
        if (hasta < ahora - TimeSpan.FromMinutes(1))
            throw ReglaDeNegocioException.Validacion("solapamiento-invalido", "La llave anterior no puede vencer en el pasado.");
        if (hasta > ahora + SolapamientoMaximo)
            throw ReglaDeNegocioException.Validacion("solapamiento-invalido", "La llave anterior puede seguir valiendo como mucho 30 días.");

        if (hasta < ahora)
            hasta = ahora;
        if (ExpiraEn is { } expira && expira < hasta)
            hasta = expira;

        ReemplazadaPor = nueva;
        ExpiraEn = hasta;
        return hasta;
    }

    public bool PermiteEmpresa(Guid empresaId) =>
        Empresas.Contains("*") || Empresas.Contains(empresaId.ToString());

    public bool VigenteEn(DateTimeOffset ahora) =>
        RevocadaEn is null && (ExpiraEn is null || ExpiraEn > ahora);
}
