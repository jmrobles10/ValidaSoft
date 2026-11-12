namespace Validasoft.Contratos;

// ---------- Consulta de certificación (docs/05 §2.6 y §5.6; docs/08 §5; CN-1, CN-2) ----------

/// <summary>Documento del certificado que la empresa debe revalidar (expediente de actualización abierto, RV-1…RV-4).</summary>
/// <param name="Tipo">Código del requisito (tipo de documento o sección de la ficha).</param>
/// <param name="Plazo">Hasta cuándo hay plazo antes de que los pendientes bloqueen (A-2).</param>
/// <param name="Bloqueante">El plazo ya venció: el certificado no debe aceptarse.</param>
public sealed record RevalidacionPendienteDto(string Tipo, DateOnly? Plazo, bool Bloqueante);

/// <summary>Cuenta bancaria designada y aprobada del vínculo, sin el número completo.</summary>
public sealed record CuentaDesignadaResumenDto(string NumeroEnmascarado, string Banco, string? BancoCodigo = null, string? BancoSwift = null);

/// <summary>Estado equivalente en el legado (SRM/Portal) mientras dura la coexistencia.</summary>
public sealed record LegadoDto(int EstadoTercero);

/// <summary>
/// Respuesta compacta de <c>GET /consultas/certificacion</c> para Marco y los ERP (CN-1): ¿se le puede pagar o contratar a
/// este tercero en esta empresa? Sin vínculo responde <c>Certificado: false</c> y <c>EstadoVinculo: "SinVinculo"</c> (sin
/// revelar si el tercero existe en la plataforma).
/// </summary>
/// <param name="Certificado">El vínculo tiene un certificado emitido (en cualquier estado).</param>
/// <param name="Folio">Folio de la emisión actual (<c>{Prefijo}-C-{Año}-{Secuencia}-{n}</c>).</param>
/// <param name="Estado"><c>Vigente | ConPendientes | Suspendido | Vencido | Revocado</c> (el de la emisión actual).</param>
/// <param name="EstadoVinculo">Estado del vínculo (<c>Invitado | EnValidacion | Activo | PorVencer | Vencido | Bloqueado |
/// Desvinculado | Anulado</c>) o <c>SinVinculo</c>.</param>
/// <param name="VigenteHasta">Vigencia del certificado (la menor de sus documentos).</param>
/// <param name="DocumentosVencidos">Códigos de los documentos del certificado cuya vigencia ya pasó.</param>
/// <param name="Bloqueado">No se le debe pagar ni contratar: vínculo bloqueado o vencido, o certificado suspendido,
/// vencido, revocado o con pendientes vencidos.</param>
/// <param name="MotivoBloqueo"><c>Bloqueo | PlazoRevalidacion | Vencido | Revocado</c> (CN-2), o nulo.</param>
/// <param name="Legacy">Estado del legado durante la coexistencia (pendiente: va nulo hasta que exista el mapeo).</param>
/// <param name="FuenteRespaldo">Siempre <c>false</c> desde Validasoft; el integrador lo pone en <c>true</c> cuando responde
/// desde su copia local (docs/08 §5).</param>
/// <param name="AntiguedadSegundos">Solo con <see cref="FuenteRespaldo"/>: edad de la copia local.</param>
/// <param name="EnRevision">Campo nuevo (VR-1), solo informativo: el tercero actualizó información que la empresa ya había aprobado y su
/// actualización sigue abierta (es lo que la empresa y el tercero ven como «En revisión»). <b>No cambia nada de lo demás</b>:
/// <see cref="EstadoVinculo"/> sigue siendo <c>Activo</c>, <c>PorVencer</c> o <c>Vencido</c> y el certificado, la vigencia, <see cref="Bloqueado"/> y
/// <see cref="RevalidacionPendiente"/> (que lista lo que falta revisar) se calculan como siempre. Quien no lo conoce lo ignora.</param>
public sealed record ConsultaCertificacionDto(
    bool Certificado,
    string? Folio,
    string? Estado,
    string EstadoVinculo,
    Guid? VinculoId,
    Guid? TerceroId,
    DateOnly? VigenteHasta,
    IReadOnlyList<RevalidacionPendienteDto> RevalidacionPendiente,
    IReadOnlyList<string> DocumentosVencidos,
    bool Bloqueado,
    string? MotivoBloqueo,
    CuentaDesignadaResumenDto? CuentaDesignada,
    string? UrlVerificacion,
    LegadoDto? Legacy,
    bool FuenteRespaldo = false,
    int? AntiguedadSegundos = null,
    bool EnRevision = false);
