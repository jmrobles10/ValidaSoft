using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Identidad;

/// <summary>
/// Acceso temporal de soporte de la Plataforma a una empresa del tenant (B-6): nunca existe por defecto; lo concede un
/// administrador del tenant con justificación y una duración de 1 a 72 horas, expira solo y se puede revocar. Mientras
/// está vigente, la credencial de Plataforma que lo presenta (<c>X-Acceso-Soporte</c>) lee la configuración, los vínculos
/// y los expedientes de esa empresa, sin documentos. Esquema <c>identidad</c>, multi-tenant.
/// </summary>
public sealed class AccesoSoporte
{
    public const int HorasMinimas = 1;
    public const int HorasMaximas = 72;

    public Guid Id { get; set; }

    /// <summary>Copia del tenant (además del <c>tenant_id</c> de Marten) para resolverlo al autenticar a la Plataforma.</summary>
    public Guid TenantId { get; set; }

    public Guid EmpresaId { get; set; }

    public string Justificacion { get; set; } = string.Empty;

    public string ConcedidoPor { get; set; } = string.Empty;

    public DateTimeOffset ConcedidoEn { get; set; }

    public DateTimeOffset ExpiraEn { get; set; }

    public DateTimeOffset? RevocadoEn { get; set; }

    public string? RevocadoPor { get; set; }

    public bool VigenteEn(DateTimeOffset ahora) => RevocadoEn is null && ahora < ExpiraEn;

    public static AccesoSoporte Conceder(Guid tenantId, Guid empresaId, int horas, string? justificacion, string concedidoPor, DateTimeOffset ahora)
    {
        if (horas is < HorasMinimas or > HorasMaximas)
            throw ReglaDeNegocioException.Validacion("duracion-invalida", $"El acceso de soporte dura entre {HorasMinimas} y {HorasMaximas} horas.");
        var texto = (justificacion ?? string.Empty).Trim();
        if (texto.Length < 10)
            throw ReglaDeNegocioException.Validacion("justificacion-requerida", "Explica en al menos 10 caracteres para qué necesita soporte el acceso.");
        if (texto.Length > 500)
            throw ReglaDeNegocioException.Validacion("texto-demasiado-largo", "La justificación admite hasta 500 caracteres.");

        return new AccesoSoporte
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            EmpresaId = empresaId,
            Justificacion = texto,
            ConcedidoPor = concedidoPor,
            ConcedidoEn = ahora,
            ExpiraEn = ahora.AddHours(horas)
        };
    }

    /// <summary>Revoca un acceso vigente (409 <c>acceso-no-vigente</c> si ya expiró o se revocó).</summary>
    public void Revocar(string por, DateTimeOffset ahora)
    {
        if (!VigenteEn(ahora))
            throw ReglaDeNegocioException.Conflicto("acceso-no-vigente", "El acceso de soporte ya expiró o fue revocado.");
        RevocadoEn = ahora;
        RevocadoPor = por;
    }
}
