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

/// <summary>Identificador del vínculo en el sistema que lo originó (SRM, Marco, Portal…).</summary>
public sealed record ReferenciaExterna(string Sistema, string Id);

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
