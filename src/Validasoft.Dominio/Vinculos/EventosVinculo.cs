using System.Text.Json;
using Validasoft.Dominio.Consentimientos;

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

/// <summary>Desde dónde aceptó el tercero: la página pública o una pantalla embebida en un sistema integrado.</summary>
public enum CanalAceptacion
{
    Web,
    Embed
}

/// <summary>Lo que el tercero marcó en la pantalla de aceptación (entrada de <see cref="Vinculo.RegistrarConsentimientos"/>).</summary>
public sealed record ConsentimientoOtorgado(TipoTextoLegal Tipo, int TextoVersion, bool Aceptado);

/// <summary>Texto aceptado: versión e id exactos más su hash SHA-256 (P-3-5, P-16-4).</summary>
public sealed record ConsentimientoRegistrado(TipoTextoLegal Tipo, Guid TextoLegalId, int Version, string Hash);

/// <summary>Evidencia de la aceptación. El actor (tercero) también viaja en los metadatos del evento.</summary>
public sealed record EvidenciaDeConsentimiento(Guid TerceroId, string Correo, string Ip, string UserAgent, bool OtpVerificado, CanalAceptacion Canal);

/// <summary>El tercero aceptó los textos legales de esta empresa (y el aviso de la plataforma si le faltaba); precede a <see cref="InvitacionAceptada"/>.</summary>
public sealed record ConsentimientosRegistrados(
    IReadOnlyList<ConsentimientoRegistrado> Consentimientos,
    EvidenciaDeConsentimiento Evidencia,
    DateTimeOffset Fecha);

public sealed record AtributosDeVinculoActualizados(
    Dictionary<string, JsonElement> Antes,
    Dictionary<string, JsonElement> Despues,
    DateTimeOffset Fecha);

public sealed record VinculoBloqueado(EstadoVinculo EstadoPrevio, string Motivo, DateTimeOffset? Hasta, DateTimeOffset Fecha);

public sealed record VinculoDesbloqueado(EstadoVinculo EstadoRestituido, string Justificacion, DateTimeOffset Fecha);

public sealed record VinculoDesvinculado(EstadoVinculo EstadoPrevio, string Motivo, DateTimeOffset Fecha);

public sealed record VinculoReintegrado(EstadoVinculo EstadoResultante, string Justificacion, DateTimeOffset Fecha);

public sealed record VinculoAnulado(EstadoVinculo EstadoPrevio, string Motivo, DateTimeOffset Fecha);
