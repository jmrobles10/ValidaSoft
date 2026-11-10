using System.Globalization;

namespace Validasoft.Dominio.Ficha;

/// <summary>De dónde sale lo que una empresa tiene aceptado de una sección.</summary>
public enum OrigenDeAceptacion
{
    /// <summary>La aprobación de un expediente (F2).</summary>
    Expediente,

    /// <summary>La importación del Portal (F4): el tercero estaba Completada o en Actualizacion para la constructora.</summary>
    Importacion
}

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

    /// <summary>Expediente que la aprobó; <see cref="Guid.Empty"/> si la aceptación viene de la importación del Portal.</summary>
    public Guid ExpedienteId { get; set; }

    /// <summary>Folio del expediente; vacío si viene de la importación.</summary>
    public string Folio { get; set; } = string.Empty;

    /// <summary>
    /// De dónde sale: la aprobación de un expediente (F2) o la importación del Portal (F4, FM-42), cuando el tercero estaba
    /// <c>Completada</c> o en <c>Actualizacion</c> para la constructora que corresponde a la empresa. La importada no publica
    /// <c>seccion.aceptada</c> (el SRM ya la aplicó en AyF) ni se mueve en las corridas siguientes.
    /// </summary>
    public OrigenDeAceptacion Origen { get; set; } = OrigenDeAceptacion.Expediente;

    /// <summary>Clave del legado de la que sale una aceptación importada (p. ej. <c>portal:48112|constructora:7</c>).</summary>
    public string? ClaveLegado { get; set; }

    public static string Clave(Guid vinculoId, string seccion, int? periodo) =>
        $"{vinculoId:N}|{seccion}|{periodo?.ToString(CultureInfo.InvariantCulture) ?? "-"}";
}
