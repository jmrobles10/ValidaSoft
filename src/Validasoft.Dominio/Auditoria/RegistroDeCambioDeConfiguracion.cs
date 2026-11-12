using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Auditoria;

public enum AccionConfiguracion
{
    Creado,
    Modificado,
    Publicado,
    Archivado,
    Desactivado,
    Rotado,
    Revocado,

    /// <summary>Un sistema o un operador desactivado volvió a estar activo (PI-7); sus llaves revocadas no se restauran.</summary>
    Reactivado
}

/// <summary>
/// Registro append-only de un cambio de configuración (R-1, docs/03 §2.1): ninguna entidad de
/// configuración cambia sin una fila aquí, escrita en la misma transacción que el cambio.
/// </summary>
public sealed class RegistroDeCambioDeConfiguracion
{
    public Guid Id { get; set; }

    public Guid? EmpresaId { get; set; }

    public string Entidad { get; set; } = string.Empty;

    public string EntidadId { get; set; } = string.Empty;

    public AccionConfiguracion Accion { get; set; }

    public object? Antes { get; set; }

    public object? Despues { get; set; }

    public ActorTipo ActorTipo { get; set; }

    public string ActorId { get; set; } = string.Empty;

    /// <summary>Sistema integrado desde el que actuó (ADR-0011), si aplica.</summary>
    public string? Sistema { get; set; }

    public string? Justificacion { get; set; }

    public string? CorrelationId { get; set; }

    public DateTimeOffset Fecha { get; set; }
}
