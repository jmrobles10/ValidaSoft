namespace Validasoft.Dominio.Documentos;

/// <summary>Estado de una versión (docs/03 §3.1). Un solo enum textual.</summary>
public enum EstadoVersion
{
    Recibida,
    EnAnalisis,
    Actual,
    Superada,
    Devuelta,
    Retirada
}

/// <summary>Comparación de una versión con la actual (sin IA solo por hash, docs/03 §3.1).</summary>
public enum Comparacion
{
    SinAnterior,
    Identica,
    Distinta,
    SinCambios,
    CambiosDetectados
}

/// <summary>Hecho del antivirus (T-11). Sin ClamAV todavía: <c>NoDisponible</c>, que solo continúa en desarrollo.</summary>
public enum Antivirus
{
    Limpio,
    Infectado,
    Cuarentena,
    NoDisponible
}

/// <summary>Cuándo nació una compartición (docs/02 §10.1, <c>comparticion_version.origen</c>).</summary>
public enum OrigenComparticion
{
    AlAceptarInvitacion,
    Automatica,
    Importada
}

/// <summary>
/// Hechos básicos de una versión, calculados por la plataforma una sola vez (P5) y sin IA: hash verificado, MIME real
/// por magic bytes, páginas, antivirus y comparación por hash. Los hechos de IA llegan con su módulo.
/// </summary>
public sealed record HechosBasicos(
    bool HashVerificado,
    string MimeReal,
    int? Paginas,
    Antivirus Antivirus,
    Comparacion Comparacion,
    bool ConIa,
    bool PendienteRevisionHumana);

/// <summary>Empresa (por su vínculo) con la que se comparte una versión.</summary>
public sealed record DestinoDeComparticion(Guid TenantId, Guid EmpresaId, Guid VinculoId);

// Eventos del stream Documento-{id} (tenant técnico "boveda", docs/03 §4.3). El actor y la correlación viajan en los
// metadatos del evento de Marten.

public sealed record DocumentoCreado(
    Guid DocumentoId,
    Guid TerceroId,
    AmbitoDocumento Ambito,
    ClaseDocumento Clase,
    string? TipoCanonico,
    Guid? TipoDocumentoTenantId,
    string NombreTipo,
    Guid? TenantId,
    Guid? EmpresaId,
    Guid? VinculoId,
    DateTimeOffset Fecha);

/// <summary>El binario ya está en su clave definitiva (confirmado por hash); la versión nace <c>Recibida</c>.</summary>
public sealed record VersionCargada(
    int Numero,
    ClaseDocumento Clase,
    string Sha256,
    string Bucket,
    string ClaveObjeto,
    string MimeDeclarado,
    long Tamano,
    string NombreOriginal,
    Guid? ContextoVinculoId,
    string CargadaPor,
    DateTimeOffset Fecha);

/// <summary>El mismo archivo (mismo SHA-256) ya es una versión del documento: no se crea otra (docs/02 §5 regla 1).</summary>
public sealed record VersionDuplicadaDetectada(int NumeroExistente, string Sha256, DateTimeOffset Fecha);

public sealed record AnalisisDeVersionIniciado(int Numero, bool ConIa, DateTimeOffset Fecha);

public sealed record HechosDeVersionRegistrados(int Numero, HechosBasicos Hechos, DateTimeOffset Fecha);

/// <summary>
/// La versión pasa a ser la actual (la anterior queda <c>Superada</c>). La bóveda emite el hecho; cada empresa decidirá si
/// revalida (T-7). Sin IA no hay campos cambiados.
/// </summary>
public sealed record VersionActualCambiada(
    int Numero,
    int? NumeroAnterior,
    Comparacion Comparacion,
    IReadOnlyList<string> CamposCambiados,
    IReadOnlyList<string> CamposCriticosCambiados,
    bool ConIa,
    DateTimeOffset Fecha);

/// <summary>La versión vuelve al tercero con un motivo; no reemplaza a la actual ni alerta a nadie.</summary>
public sealed record VersionDevueltaAlTercero(int Numero, string Motivo, string Detalle, DateTimeOffset Fecha);

public sealed record VersionRetirada(int Numero, string Motivo, DateTimeOffset Fecha);

/// <summary>Se retiró la actual y la superada más reciente vuelve a serlo (I-11).</summary>
public sealed record VersionActualRestituida(int Numero, int NumeroRetirado, DateTimeOffset Fecha);

/// <summary>Una versión queda visible para una empresa a través de su vínculo (solo tras <c>InvitacionAceptada</c>, B-1).</summary>
public sealed record VersionCompartida(int Numero, Guid TenantId, Guid EmpresaId, Guid VinculoId, OrigenComparticion Origen, DateTimeOffset Fecha);

/// <summary>
/// Se emitió una URL prefirmada de descarga (docs/07 §6.2). Empresa y vínculo nulos cuando descarga el propio tercero.
/// </summary>
public sealed record DocumentoAccedido(
    int Numero,
    Guid? TenantId,
    Guid? EmpresaId,
    Guid? VinculoId,
    string ActorTipo,
    string ActorId,
    string Ip,
    string UserAgent,
    string Motivo,
    DateTimeOffset Fecha);
