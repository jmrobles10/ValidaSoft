using System.Text.Json;
using Validasoft.Dominio.Ia;

namespace Validasoft.Dominio.Documentos;

/// <summary>Estado de una versión (docs/03 §3.1). Un solo enum textual.</summary>
public enum EstadoVersion
{
    Recibida,
    EnAnalisis,
    Actual,
    Superada,
    Devuelta,
    Retirada,

    /// <summary>
    /// La versión actual venció (pasó <c>vigenteHasta + diasGracia</c>) para todos los vínculos con vigencia (TD-6). Sigue
    /// siendo la actual del documento: renovarla o abrir la renovación es H6.
    /// </summary>
    Vencida
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
/// por magic bytes, páginas, antivirus y comparación por hash. El análisis con OpenAI (ADR-0012) los marca <c>ConIa</c>
/// y ajusta la revisión humana y la comparación; quien no consumió su validación los sigue viendo sin IA.
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
    DateTimeOffset Fecha,
    bool EnCuarentena = false);

/// <summary>
/// El binario ya está en su clave definitiva (confirmado por hash); la versión nace <c>Recibida</c>. Con datos declarados
/// (TD-4) llegan <see cref="Datos"/> y su huella <see cref="HashDatos"/>; una versión solo de formulario no tiene binario
/// (<see cref="Bucket"/> y <see cref="ClaveObjeto"/> vacíos) y su <see cref="Sha256"/> es la huella de los datos.
/// </summary>
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
    DateTimeOffset Fecha,
    string? HashDatos = null,
    IReadOnlyList<DatoDeclarado>? Datos = null);

/// <summary>
/// Vigencia de la versión actual para un vínculo, con la regla del tipo del tenant de ese vínculo (TD-6). Se recalcula si
/// cambia la regla o la versión; idempotente (mismo resultado ⇒ sin evento).
/// </summary>
public sealed record VigenciaDeVersionCalculada(
    int Numero,
    Guid TenantId,
    Guid EmpresaId,
    Guid VinculoId,
    Guid TipoDocumentoTenantId,
    DateOnly? VigenteHasta,
    BaseVigencia Base,
    IReadOnlyList<int> DiasAviso,
    int DiasGracia,
    DateTimeOffset Fecha);

/// <summary>Pasó <c>vigenteHasta + diasGracia</c> de la versión para ese vínculo (TD-6). No cambia el estado del vínculo (H6).</summary>
public sealed record VersionVencida(int Numero, Guid VinculoId, DateOnly VigenteHasta, int DiasGracia, DateTimeOffset Fecha);

/// <summary>
/// El tercero declaró que la versión actual no cambió (RN-2, I-14) para ese vínculo: su frescura vuelve a correr desde
/// hoy. No cambia la versión actual ni alerta a las otras empresas; la recalcula <c>CalcularVigencias</c>.
/// </summary>
public sealed record VersionRatificada(int Numero, Guid VinculoId, string Por, DateTimeOffset Fecha);

/// <summary>
/// Se avisó al tercero que la versión vence para ese vínculo (aviso consolidado del día, TD-7). Solo deja constancia del
/// hecho para el evento público <c>documento.por_vencer</c> y las notificaciones (NW-1); no cambia el estado del documento.
/// </summary>
public sealed record VencimientoAvisado(int Numero, Guid VinculoId, DateOnly VigenteHasta, int DiasRestantes, DateTimeOffset Fecha);

/// <summary>El mismo archivo (mismo SHA-256) ya es una versión del documento: no se crea otra (docs/02 §5 regla 1).</summary>
public sealed record VersionDuplicadaDetectada(int NumeroExistente, string Sha256, DateTimeOffset Fecha, string? HashDatos = null);

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

/// <summary>
/// El titular pidió suprimir sus datos (habeas data, OD-6) y esta versión no la referencia ninguna aceptación ni certificado: se
/// eliminó su binario y se limpiaron sus datos declarados, su nombre original y su análisis. El evento anterior no se reescribe.
/// </summary>
public sealed record VersionSuprimida(int Numero, string Motivo, DateTimeOffset Fecha);

/// <summary>
/// Una solicitud de supresión no pudo eliminar esta versión porque la referencia una aceptación o un certificado (OD-6): se conserva
/// íntegra hasta <see cref="RetenidaHasta"/> (la retención más larga entre quienes la referencian), con su base legal, y no se vuelve
/// a compartir.
/// </summary>
public sealed record RetencionLegalMarcada(int Numero, string BaseLegal, DateOnly? RetenidaHasta, Guid SolicitudId, DateTimeOffset Fecha);

/// <summary>Se retiró la actual y la superada más reciente vuelve a serlo (I-11).</summary>
public sealed record VersionActualRestituida(int Numero, int NumeroRetirado, DateTimeOffset Fecha);

/// <summary>
/// Lo que una persona con acceso aislado subió para una empresa (documento en cuarentena) tuvo su desenlace cuando los contactos del tercero decidieron
/// el acceso: con la aprobación se <b>promovió</b> a la bóveda global (<see cref="DocumentoGlobalId"/>, <see cref="NumeroGlobal"/>) o se dejó sin promover
/// porque la bóveda ya tenía algo igual o más reciente (<see cref="Motivo"/>); con el rechazo queda solo en el vínculo.
/// </summary>
public sealed record CuarentenaDeDocumentoResuelta(bool Promovida, Guid? DocumentoGlobalId, int? NumeroGlobal, string Motivo, DateTimeOffset Fecha);

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

/// <summary>
/// Análisis de la versión con OpenAI (ADR-0012, IA-1…IA-6): se hace una sola vez por versión y se paga con la llave de
/// <see cref="EmpresaQuePago"/>. Valen los hechos de la última llamada (la de escalamiento si la hubo); <see cref="Llamadas"/>
/// guarda ambas y los tokens y el costo son la suma. <see cref="Campos"/> son los datos extraídos según el esquema del tipo;
/// las páginas omitidas no se enviaron (IA-5). La comparación es la de los campos clave con el análisis de la versión
/// anterior, sin otra llamada. Nunca guarda el contenido enviado.
/// </summary>
public sealed record AnalisisDeIaRegistrado(
    int Numero,
    string Modelo,
    bool Escalado,
    decimal Confianza,
    bool Legible,
    string TipoDetectado,
    IReadOnlyDictionary<string, JsonElement> Campos,
    IReadOnlyList<string> Alertas,
    int? PaginasEnviadas,
    int? PaginasOmitidas,
    long TokensEntrada,
    long TokensSalida,
    decimal CostoUsd,
    Guid TenantQuePago,
    Guid EmpresaQuePago,
    DateTimeOffset Fecha,
    string? Observaciones = null,
    bool PendienteRevisionHumana = true,
    Comparacion? ComparacionConAnterior = null,
    IReadOnlyList<string>? CamposCambiados = null,
    IReadOnlyList<string>? CamposCriticosCambiados = null,
    IReadOnlyList<LlamadaDeIa>? Llamadas = null,
    long TokensEntradaEnCache = 0);

/// <summary>
/// El análisis con OpenAI falló (error, tiempo agotado, respuesta incompleta o fuera del esquema, llave inválida): la
/// versión queda sin IA para revisión humana y no se vuelve a intentar (fail-closed, IA-6). <see cref="Motivo"/> es un
/// código estable, nunca el contenido del documento.
/// </summary>
public sealed record AnalisisDeIaFallido(int Numero, string Motivo, DateTimeOffset Fecha, Guid? EmpresaQuePago = null);
