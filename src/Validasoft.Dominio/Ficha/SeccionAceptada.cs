using System.Globalization;

namespace Validasoft.Dominio.Ficha;

/// <summary>
/// Lo que una empresa tiene aceptado de una sección de la ficha en un vínculo (FM-3, F2): la versión, cuándo y en qué
/// expediente. Una por (vínculo, sección, periodo), en el esquema <c>validacion</c> del tenant (privada de la empresa). Nace o
/// cambia en la misma transacción que aprueba el expediente (<c>SeccionAceptadaPorEmpresa</c>); lo que la empresa observa no
/// la toca: sigue viendo la versión que aceptó hasta aprobar otra.
/// </summary>
public sealed class SeccionAceptada
{
    /// <summary><c>{vínculo:N}|{sección}|{periodo o -}</c>.</summary>
    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public Guid VinculoId { get; set; }

    public Guid TerceroId { get; set; }

    public string Seccion { get; set; } = string.Empty;

    public int? Periodo { get; set; }

    /// <summary>Stream de la sección en la bóveda.</summary>
    public Guid SeccionId { get; set; }

    public int Version { get; set; }

    /// <summary>Huella de la versión aceptada (datos + soportes).</summary>
    public string Huella { get; set; } = string.Empty;

    public DateTimeOffset AceptadaEn { get; set; }

    public Guid ExpedienteId { get; set; }

    public string Folio { get; set; } = string.Empty;

    public static string Clave(Guid vinculoId, string seccion, int? periodo) =>
        $"{vinculoId:N}|{seccion}|{periodo?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
}
