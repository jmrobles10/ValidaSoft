using System.Text.Json;

namespace Validasoft.Dominio.Vinculos;

/// <summary>Estado del vínculo (docs/03 §3.1). Un solo enum textual; nunca enteros.</summary>
public enum EstadoVinculo
{
    Invitado,
    EnValidacion,
    Activo,
    PorVencer,
    Vencido,
    Bloqueado,
    Desvinculado,
    Anulado
}

public enum OrigenVinculo
{
    Invitacion,
    Autorregistro,
    Migracion,
    Api
}

/// <summary>
/// Identificador del vínculo en un sistema integrado (SRM, Marco, Portal…). Un vínculo tiene a lo sumo una por
/// sistema (I-0); el sistema lo fija la API key, no el cuerpo de la petición.
/// </summary>
public sealed record ReferenciaExterna(string Sistema, string Id)
{
    /// <summary>Clave plana <c>SISTEMA|id</c> para buscar por referencia.</summary>
    public string Clave() => $"{Sistema}|{Id}";
}

// Eventos del stream Vinculo-{id}. Quién y desde dónde (actor, correlación) viaja en los metadatos del
// evento de Marten, no en el cuerpo (docs/03 §3).

public sealed record VinculoCreado(
    Guid VinculoId,
    Guid EmpresaId,
    Guid TerceroId,
    Guid TipoVinculoId,
    Guid? FlujoId,
    Dictionary<string, JsonElement> Atributos,
    OrigenVinculo Origen,
    ReferenciaExterna? ReferenciaExterna,
    string CorreoInvitacion,
    DateTimeOffset Fecha);

/// <summary>Otro sistema integrado pidió el mismo vínculo: comparte la validación y recibe sus eventos (I-0).</summary>
public sealed record ReferenciaExternaAgregada(string Sistema, string Id, DateTimeOffset Fecha);

public sealed record InvitacionEnviada(string Canal, string CorreoDestino, DateTimeOffset Expira, Guid? LoteId, DateTimeOffset Fecha);

public sealed record InvitacionAceptada(string Correo, DateTimeOffset Fecha);

public sealed record AtributosDeVinculoActualizados(
    Dictionary<string, JsonElement> Antes,
    Dictionary<string, JsonElement> Despues,
    DateTimeOffset Fecha);

public sealed record VinculoBloqueado(EstadoVinculo EstadoPrevio, string Motivo, DateTimeOffset? Hasta, DateTimeOffset Fecha);

public sealed record VinculoDesbloqueado(EstadoVinculo EstadoRestituido, string Justificacion, DateTimeOffset Fecha);

public sealed record VinculoDesvinculado(EstadoVinculo EstadoPrevio, string Motivo, DateTimeOffset Fecha);

public sealed record VinculoReintegrado(EstadoVinculo EstadoResultante, string Justificacion, DateTimeOffset Fecha);

public sealed record VinculoAnulado(EstadoVinculo EstadoPrevio, string Motivo, DateTimeOffset Fecha);
