namespace Validasoft.Contratos;

// DTOs de la bóveda de documentos (docs/05 §2.2, §2.5, §2.9 y §4). Los enums viajan como texto; JSON en camelCase.

// ---------- Catálogo canónico y tipos del tenant ----------

/// <summary><see cref="Tipo"/>: <c>Ninguna</c>, <c>MaxDiasDesdeExpedicion</c>, <c>PeriodoMinimo</c> o <c>MaxDiasDesdeAprobacion</c>.</summary>
public sealed record ReglaFrescuraDto(string Tipo, int? Dias = null, string? Regla = null);

/// <summary>Tipo canónico de la plataforma (Anexo A de docs/03). <see cref="CargaSoportada"/>: en esta fase solo los de clase <c>Archivo</c>.</summary>
public sealed record TipoDocumentoCanonicoDto(
    string Codigo, int Version, string Nombre, string Ambito, string Clase, string AplicaA, bool TieneVencimientoIntrinseco,
    bool TieneFechaExpedicion, bool EsPeriodico, string? Periodicidad, bool PermiteRatificacion, bool DePersonaRelacionada,
    IReadOnlyList<string> CamposClave, IReadOnlyList<string> CamposCriticos, string Sensibilidad, ReglaFrescuraDto? ReglaFrescuraSugerida,
    int RetencionAniosSugerida, bool CargaSoportada);

/// <summary>
/// Alta de un tipo del tenant. <see cref="TipoCanonico"/> nulo ⇒ tipo propio (siempre <c>PorVinculo</c>). <see cref="Ambito"/>
/// puede restringir el del canónico, nunca ampliarlo (422 <c>ambito-no-ampliable</c>). Qué se pide a cada vínculo lo
/// deciden los requisitos del flujo (RQ-3): ya no hay <c>exigencias</c> por tipo.
/// </summary>
public sealed record CrearTipoDocumentoDto(
    string Codigo, string? Nombre, string? TipoCanonico, string? Ambito = null, ReglaFrescuraDto? ReglaFrescura = null,
    IReadOnlyList<int>? DiasAviso = null, int? DiasGracia = null, int? DiasRevalidacion = null, string? TextoAyuda = null);

/// <summary>Código y canónico son inmutables; los campos nulos conservan su valor.</summary>
public sealed record ActualizarTipoDocumentoDto(
    string? Nombre = null, string? Ambito = null, ReglaFrescuraDto? ReglaFrescura = null, IReadOnlyList<int>? DiasAviso = null,
    int? DiasGracia = null, int? DiasRevalidacion = null, string? TextoAyuda = null, bool? Activo = null);

/// <summary>Defaults del tenant (B-4). Frescura y plazos se guardan; se aplican con vigencias (H6).</summary>
public sealed record TipoDocumentoTenantDto(
    Guid Id, string Codigo, string Nombre, string? TipoCanonico, int? VersionCanonico, string Ambito, string Clase, string? TextoAyuda,
    ReglaFrescuraDto ReglaFrescura, IReadOnlyList<int> DiasAviso, int DiasGracia, int DiasRevalidacion, bool Activo, DateTimeOffset CreadoEn);

// ---------- Carga en dos pasos ----------

/// <summary>
/// Paso 1 de la carga. Un tipo canónico global va a la bóveda del tercero; un tipo del tenant necesita
/// <see cref="VinculoId"/> (de qué empresa es) y, si es <c>PorVinculo</c>, solo existe para ese vínculo.
/// <see cref="Sha256"/> es opcional: si llega, detecta el duplicado antes de subir y se verifica al confirmar.
/// </summary>
public sealed record IniciarCargaDto(
    string NombreArchivo, long TamanoBytes, string TipoMime, string? TipoCanonico = null, Guid? TipoDocumentoTenantId = null,
    Guid? VinculoId = null, string? Sha256 = null);

public sealed record InstruccionCargaDto(string Metodo, string Url, IReadOnlyDictionary<string, string> Cabeceras, DateTimeOffset Expira);

/// <summary><see cref="Duplicado"/> ⇒ sin <see cref="Carga"/>: el archivo ya es la versión <see cref="Version"/> del documento.</summary>
public sealed record CargaPrefirmadaDto(Guid DocumentoId, int Version, bool Duplicado, InstruccionCargaDto? Carga);

// ---------- Documentos y versiones ----------

public sealed record TipoDocumentoResumenDto(string Codigo, string Nombre, Guid? TipoDocumentoTenantId);

/// <summary>Hechos básicos de la versión, sin IA (docs/02 §5.1).</summary>
public sealed record HechosDto(
    bool ConIa, bool HashVerificado, string Antivirus, string MimeReal, int? Paginas, string Comparacion, IReadOnlyList<string> CamposCambiados,
    IReadOnlyList<string> CamposCriticosCambiados, bool PendienteRevisionHumana);

public sealed record VersionDocumentoDto(
    Guid DocumentoId, int N, string Estado, DateTimeOffset CargadaEn, string CargadaPor, string Clase, string NombreOriginal, string Mime,
    long TamanoBytes, int? Paginas, string Sha256, string? MotivoDevolucion, string? DetalleDevolucion, HechosDto? Hechos);

/// <summary>Versión compartida con una empresa a través de un vínculo.</summary>
public sealed record CompartidoDto(Guid DocumentoId, string Tipo, int Version, Guid VinculoId, Guid EmpresaId);

/// <summary>Resultado del paso 2. <see cref="Duplicado"/> ⇒ no hubo versión nueva y <see cref="Version"/> es la existente.</summary>
public sealed record ConfirmacionCargaDto(bool Duplicado, VersionDocumentoDto Version, IReadOnlyList<CompartidoDto> Compartidos);

/// <summary>Estado del documento con una empresa del tercero (solo vínculos de su sesión).</summary>
public sealed record ComparticionPublicaDto(
    Guid VinculoId, EmpresaPublicaDto Empresa, bool Compartido, int? Version, bool VersionActualCompartida, DateTimeOffset? CompartidaEn);

public sealed record DocumentoBovedaDto(
    Guid Id, TipoDocumentoResumenDto Tipo, string Ambito, string Clase, Guid? VinculoId, VersionDocumentoDto? VersionActual, int Versiones,
    IReadOnlyList<ComparticionPublicaDto> PorEmpresa);

/// <summary><c>BovedaTercero</c>: los documentos del tercero con su versión actual y, por empresa, si está compartido.</summary>
public sealed record BovedaDto(Guid TerceroId, IReadOnlyList<DocumentoBovedaDto> Documentos);

public sealed record DocumentoDto(
    Guid Id, Guid TerceroId, TipoDocumentoResumenDto Tipo, string Clase, string Ambito, Guid? VinculoId, int? VersionActual,
    IReadOnlyList<VersionDocumentoDto> Versiones, IReadOnlyList<ComparticionPublicaDto> PorEmpresa);

/// <summary>Bóveda vista por una empresa: solo las versiones compartidas con su vínculo. <see cref="VersionActual"/> nulo si la actual no le fue compartida.</summary>
public sealed record DocumentoVistaEmpresaDto(
    Guid DocumentoId, TipoDocumentoResumenDto Tipo, string Ambito, int? VersionActual, IReadOnlyList<VersionDocumentoDto> VersionesCompartidas);

public sealed record DescargaDto(string Url, DateTimeOffset Expira, string Nombre, string Mime);

public sealed record RetirarVersionDto(string Motivo);

public sealed record RetiroDto(DocumentoDto Documento, bool BinarioEliminado, IReadOnlyList<CompartidoDto> Compartidos);
