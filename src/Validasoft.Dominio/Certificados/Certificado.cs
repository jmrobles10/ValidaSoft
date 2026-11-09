using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Certificados;

/// <summary>De dónde sale una emisión: el vínculo activado y el expediente aprobado que la sostiene.</summary>
public sealed record OrigenDeEmision(Guid TenantId, Guid EmpresaId, Guid VinculoId, Guid TerceroId, Guid ExpedienteId, string ExpedienteFolio);

public sealed record PdfDeCertificado(string Bucket, string Clave, string Sha256, long Tamano, DateTimeOffset GeneradoEn);

public sealed record RevocacionDeCertificado(string Motivo, OrigenRevocacion Origen, string Por, DateTimeOffset Fecha);

/// <summary>Una emisión <c>-{n}</c>: su contenido sellado no cambia nunca; solo su estado.</summary>
public sealed class EmisionDeCertificado
{
    public int Numero { get; set; }

    public string Folio { get; set; } = string.Empty;

    public Guid ExpedienteId { get; set; }

    public string ExpedienteFolio { get; set; } = string.Empty;

    public string ContenidoCanonico { get; set; } = string.Empty;

    public string HashContenido { get; set; } = string.Empty;

    public string Sello { get; set; } = string.Empty;

    public string Kid { get; set; } = string.Empty;

    public DateOnly? VigenteHasta { get; set; }

    public string UrlVerificacion { get; set; } = string.Empty;

    public DateTimeOffset EmitidoEn { get; set; }

    public EstadoCertificado Estado { get; set; }

    /// <summary>Desde cuándo está en <see cref="Estado"/>.</summary>
    public DateTimeOffset EstadoDesde { get; set; }

    public PdfDeCertificado? Pdf { get; set; }

    public RevocacionDeCertificado? Revocacion { get; set; }

    public int Descargas { get; set; }

    [JsonIgnore]
    public string Huella => HuellaDeCertificado.Formatear(HashContenido);
}

/// <summary>
/// Certificado verificable del vínculo (docs/03 §7) — agregado event-sourced, uno por vínculo (id determinista desde el
/// vínculo, así no puede haber dos) con su historial de emisiones: folio raíz <c>{Prefijo}-C-{Año}-{Secuencia}</c> y
/// sufijo <c>-{n}</c>. Cada emisión guarda el contenido canónico (RFC 8785), su SHA-256 y el sello Ed25519 de la
/// plataforma; lo que cambia después es solo el estado de la emisión vigente: suspendido por bloqueo, restablecido,
/// vencido, revocado (irreversible) o reemplazado por una emisión nueva. No hay certificado parcial (P-8-4).
/// </summary>
public sealed class Certificado
{
    private static readonly EstadoCertificado[] Validos = [EstadoCertificado.Vigente, EstadoCertificado.ConPendientes];

    public Guid Id { get; set; }

    public long Version { get; set; }

    /// <summary>Tenant real (Marten guarda el suyo aparte; este permite procesar entre tenants sin otra consulta).</summary>
    public Guid Tenant { get; set; }

    public Guid EmpresaId { get; set; }

    public Guid VinculoId { get; set; }

    public Guid TerceroId { get; set; }

    public string FolioRaiz { get; set; } = string.Empty;

    public List<EmisionDeCertificado> Emisiones { get; set; } = [];

    /// <summary>Copia del estado de la emisión actual (consultas por estado).</summary>
    public EstadoCertificado Estado { get; set; }

    public int EmisionActual { get; set; }

    /// <summary>Estado al que vuelve al desbloquear.</summary>
    public EstadoCertificado? EstadoPrevioASuspension { get; set; }

    public string? MotivoSuspension { get; set; }

    /// <summary>Primer instante (UTC) en que la emisión actual ya está vencida; nulo sin vigencia.</summary>
    public DateTimeOffset? VenceDesde { get; set; }

    /// <summary>La emisión actual aún no tiene PDF (lo genera el procesador de certificados, CE-5).</summary>
    public bool PdfPendiente { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    [JsonIgnore]
    public EmisionDeCertificado Actual => Emisiones.First(e => e.Numero == EmisionActual);

    /// <summary>
    /// El certificado no debe aceptarse (consulta <c>bloqueado: true</c>): suspendido por bloqueo (B-5), revocado o
    /// vencido. <c>ConPendientes</c> bloqueante (A-2) se sumará con la revalidación (H6).
    /// </summary>
    [JsonIgnore]
    public bool Bloqueante => Estado is EstadoCertificado.Suspendido or EstadoCertificado.Revocado or EstadoCertificado.Vencido;

    public EmisionDeCertificado? Buscar(int numero) => Emisiones.FirstOrDefault(e => e.Numero == numero);

    /// <summary>Id del stream del certificado de un vínculo: determinista, así una segunda emisión inicial choca en lugar de duplicarse.</summary>
    public static Guid IdPara(Guid vinculoId) => IdDeterminista.Para($"certificado|{vinculoId:N}");

    /// <summary><c>{UrlPublica}/verificar/{folio}?huella={12 hex}</c>: la huella desambigua folios iguales de tenants distintos (CE-8).</summary>
    public static string UrlDeVerificacion(string urlPublica, string folio, string hash) =>
        $"{urlPublica.TrimEnd('/')}/verificar/{Uri.EscapeDataString(folio)}?huella={hash[..12]}";

    /// <summary>
    /// Vigencia del certificado: la menor de los documentos aceptados que tienen una (sin ninguna, no vence por documentos).
    /// </summary>
    public static DateOnly? VigenciaMinima(IEnumerable<RequisitoCertificado> requisitos) =>
        requisitos.Where(r => !r.Eximido && r.VigenteHasta is not null).Select(r => r.VigenteHasta).Min();

    // ---------- Decisiones ----------

    /// <summary>
    /// Primera emisión (<c>-1</c>): completa el contenido con folio, emisión, fecha y vigencia mínima, lo canonicaliza, lo
    /// hashea y lo sella. Cada requisito debe tener versión y huella o estar eximido: no existe certificado parcial (P-8-4).
    /// </summary>
    public static CertificadoEmitido Emitir(OrigenDeEmision origen, string folioRaiz, ContenidoDeCertificado plantilla,
        ISelladorDeCertificados sellador, string urlPublica, DateTimeOffset ahora) =>
        Sellar(IdPara(origen.VinculoId), origen, folioRaiz, 1, plantilla, sellador, urlPublica, ahora);

    /// <summary>
    /// Emisión <c>-{n+1}</c> por un expediente aprobado nuevo (renovación, revalidación o ratificación que cambia contenido o
    /// vigencia): la actual queda <c>Reemplazado</c>, salvo que estuviera revocada (sigue revocada).
    /// </summary>
    public IReadOnlyList<object> Reemitir(OrigenDeEmision origen, ContenidoDeCertificado plantilla, ISelladorDeCertificados sellador,
        string urlPublica, string motivo, DateTimeOffset ahora)
    {
        if (origen.VinculoId != VinculoId)
            throw new InvalidOperationException("La emisión nueva es de otro vínculo.");
        var nueva = Emisiones.Max(e => e.Numero) + 1;
        var emitido = Sellar(Id, origen, FolioRaiz, nueva, plantilla, sellador, urlPublica, ahora);
        return Estado == EstadoCertificado.Revocado
            ? [emitido]
            : [new CertificadoReemplazado(EmisionActual, nueva, Requerido(motivo, "motivo-requerido", "La reemisión necesita un motivo."), ahora), emitido];
    }

    /// <summary>Revalidación abierta (RV-3): un certificado vigente pasa a «Con pendientes»; en cualquier otro estado, nada.</summary>
    public CertificadoConPendientes? MarcarConPendientes(string motivo, Guid expedienteId, DateTimeOffset ahora) =>
        Estado == EstadoCertificado.Vigente
            ? new CertificadoConPendientes(EmisionActual, Requerido(motivo, "motivo-requerido", "Los pendientes necesitan un motivo."), expedienteId, ahora)
            : null;

    /// <summary>Bloqueo del vínculo (B-5): solo un certificado válido se suspende.</summary>
    public CertificadoSuspendido Suspender(string motivo, DateTimeOffset ahora)
    {
        if (!Validos.Contains(Estado))
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo se suspende un certificado vigente; este está {Estado}.");
        return new CertificadoSuspendido(EmisionActual, Requerido(motivo, "motivo-requerido", "La suspensión necesita un motivo."), Estado, ahora);
    }

    /// <summary>
    /// Desbloqueo: vuelve al estado previo si sigue vigente; si su vigencia pasó durante el bloqueo, queda <c>Vencido</c>.
    /// </summary>
    public object Restablecer(string motivo, DateTimeOffset ahora)
    {
        if (Estado != EstadoCertificado.Suspendido)
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo se restablece un certificado suspendido; este está {Estado}.");
        if (VenceDesde is { } vence && ahora >= vence)
            return new CertificadoVencido(EmisionActual, Actual.VigenteHasta!.Value, ahora);
        return new CertificadoRestablecido(EmisionActual, EstadoPrevioASuspension ?? EstadoCertificado.Vigente,
            Requerido(motivo, "justificacion-requerida", "El restablecimiento necesita un motivo."), ahora);
    }

    /// <summary>Irreversible. Un administrador revoca con justificación; el sistema, al anular o desvincular el vínculo.</summary>
    public CertificadoRevocado Revocar(string? motivo, OrigenRevocacion origen, string por, DateTimeOffset ahora)
    {
        if (Estado == EstadoCertificado.Revocado)
            throw ReglaDeNegocioException.Conflicto("certificado-revocado", "El certificado ya está revocado.");
        return new CertificadoRevocado(EmisionActual, Requerido(motivo, "justificacion-requerida", "La revocación necesita una justificación."),
            origen, string.IsNullOrWhiteSpace(por) ? "sistema" : por, ahora);
    }

    /// <summary>Pasó su <c>VigenteHasta</c> (fecha UTC): solo un certificado válido vence; uno suspendido lo decide al desbloquearse.</summary>
    public CertificadoVencido Vencer(DateTimeOffset ahora)
    {
        if (!Validos.Contains(Estado))
            throw ReglaDeNegocioException.Conflicto("estado-invalido", $"Solo vence un certificado vigente; este está {Estado}.");
        if (VenceDesde is not { } vence || ahora < vence)
            throw ReglaDeNegocioException.Conflicto("certificado-no-vencido", "El certificado aún está dentro de su vigencia.");
        return new CertificadoVencido(EmisionActual, Actual.VigenteHasta!.Value, ahora);
    }

    /// <summary>Registra el PDF de una emisión; nulo si ya lo tenía (idempotente).</summary>
    public PdfDeCertificadoGenerado? RegistrarPdf(int emision, string bucket, string clave, string sha256, long tamano, DateTimeOffset ahora)
    {
        var objetivo = Buscar(emision) ?? throw ReglaDeNegocioException.NoEncontrado("emision-no-encontrada", $"No existe la emisión {emision}.");
        return objetivo.Pdf is null ? new PdfDeCertificadoGenerado(emision, bucket, clave, sha256, tamano, ahora) : null;
    }

    /// <summary>Auditoría de una descarga del PDF (solo de una emisión que ya lo tiene).</summary>
    public CertificadoDescargado RegistrarDescarga(int emision, string actorTipo, string? actorId, string ip, string userAgent, bool publico,
        DateTimeOffset ahora)
    {
        var objetivo = Buscar(emision) ?? throw ReglaDeNegocioException.NoEncontrado("emision-no-encontrada", $"No existe la emisión {emision}.");
        if (objetivo.Pdf is null)
            throw ReglaDeNegocioException.Conflicto("pdf-pendiente", "El PDF del certificado aún se está generando.");
        return new CertificadoDescargado(emision, actorTipo, actorId, ip, userAgent, publico, ahora);
    }

    // ---------- Evolución (Marten) ----------

    public static Certificado Create(CertificadoEmitido e)
    {
        var certificado = new Certificado
        {
            Id = e.CertificadoId,
            Tenant = e.TenantId,
            EmpresaId = e.EmpresaId,
            VinculoId = e.VinculoId,
            TerceroId = e.TerceroId,
            FolioRaiz = e.FolioRaiz
        };
        certificado.Apply(e);
        return certificado;
    }

    public void Apply(CertificadoEmitido e)
    {
        Emisiones.Add(new EmisionDeCertificado
        {
            Numero = e.Emision,
            Folio = e.Folio,
            ExpedienteId = e.ExpedienteId,
            ExpedienteFolio = e.ExpedienteFolio,
            ContenidoCanonico = e.ContenidoCanonico,
            HashContenido = e.HashContenido,
            Sello = e.Sello,
            Kid = e.Kid,
            VigenteHasta = e.VigenteHasta,
            UrlVerificacion = e.UrlVerificacion,
            EmitidoEn = e.Fecha,
            Estado = EstadoCertificado.Vigente,
            EstadoDesde = e.Fecha
        });
        EmisionActual = e.Emision;
        EstadoPrevioASuspension = null;
        MotivoSuspension = null;
        VenceDesde = e.VigenteHasta is { } hasta ? new DateTimeOffset(hasta.AddDays(1).ToDateTime(TimeOnly.MinValue), TimeSpan.Zero) : null;
        PdfPendiente = true;
        Sincronizar(e.Fecha);
    }

    public void Apply(CertificadoReemplazado e)
    {
        var anterior = Buscar(e.EmisionAnterior)!;
        anterior.Estado = EstadoCertificado.Reemplazado;
        anterior.EstadoDesde = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(CertificadoConPendientes e) => Cambiar(e.Emision, EstadoCertificado.ConPendientes, e.Fecha);

    public void Apply(CertificadoSuspendido e)
    {
        EstadoPrevioASuspension = e.EstadoPrevio;
        MotivoSuspension = e.Motivo;
        Cambiar(e.Emision, EstadoCertificado.Suspendido, e.Fecha);
    }

    public void Apply(CertificadoRestablecido e)
    {
        EstadoPrevioASuspension = null;
        MotivoSuspension = null;
        Cambiar(e.Emision, e.EstadoRestituido, e.Fecha);
    }

    public void Apply(CertificadoRevocado e)
    {
        Buscar(e.Emision)!.Revocacion = new RevocacionDeCertificado(e.Motivo, e.Origen, e.Por, e.Fecha);
        EstadoPrevioASuspension = null;
        Cambiar(e.Emision, EstadoCertificado.Revocado, e.Fecha);
    }

    public void Apply(CertificadoVencido e)
    {
        EstadoPrevioASuspension = null;
        MotivoSuspension = null;
        Cambiar(e.Emision, EstadoCertificado.Vencido, e.Fecha);
    }

    public void Apply(PdfDeCertificadoGenerado e)
    {
        Buscar(e.Emision)!.Pdf = new PdfDeCertificado(e.Bucket, e.Clave, e.Sha256, e.Tamano, e.Fecha);
        PdfPendiente = Actual.Pdf is null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(CertificadoDescargado e)
    {
        Buscar(e.Emision)!.Descargas++;
        ActualizadoEn = e.Fecha;
    }

    // ---------- Reglas internas ----------

    private void Cambiar(int emision, EstadoCertificado estado, DateTimeOffset fecha)
    {
        var objetivo = Buscar(emision)!;
        objetivo.Estado = estado;
        objetivo.EstadoDesde = fecha;
        Sincronizar(fecha);
    }

    private void Sincronizar(DateTimeOffset fecha)
    {
        Estado = Actual.Estado;
        ActualizadoEn = fecha;
    }

    private static CertificadoEmitido Sellar(Guid certificadoId, OrigenDeEmision origen, string folioRaiz, int emision,
        ContenidoDeCertificado plantilla, ISelladorDeCertificados sellador, string urlPublica, DateTimeOffset ahora)
    {
        if (plantilla.Requisitos.FirstOrDefault(r => !r.Eximido && (r.VersionN is null || string.IsNullOrWhiteSpace(r.Sha256))) is { } incompleto)
            throw ReglaDeNegocioException.Conflicto("certificado-parcial",
                $"El requisito {incompleto.Codigo} no tiene versión aceptada ni exención: no se emite un certificado parcial.");
        if (string.IsNullOrWhiteSpace(urlPublica))
            throw new InvalidOperationException("Falta la URL pública (Validasoft:Correo:UrlPublica) para la verificación del certificado.");

        var folio = FoliosDeCertificado.DeEmision(folioRaiz, emision);
        var contenido = plantilla with
        {
            Esquema = ContenidoDeCertificado.EsquemaV1,
            Folio = folio,
            Emision = emision,
            EmitidoEn = ahora,
            VigenteHasta = VigenciaMinima(plantilla.Requisitos)
        };
        var canonico = contenido.Canonico();
        var hash = HuellaDeCertificado.Hash(canonico);
        var sello = Convert.ToBase64String(sellador.Firmar(Convert.FromHexString(hash)));

        return new CertificadoEmitido(certificadoId, origen.TenantId, origen.EmpresaId, origen.VinculoId, origen.TerceroId, origen.ExpedienteId,
            origen.ExpedienteFolio, folioRaiz, emision, folio, canonico, hash, sello, sellador.Kid, contenido.VigenteHasta,
            UrlDeVerificacion(urlPublica, folio, hash), ahora);
    }

    private static string Requerido(string? valor, string codigo, string mensaje) =>
        string.IsNullOrWhiteSpace(valor) ? throw ReglaDeNegocioException.Validacion(codigo, mensaje) : valor.Trim();
}

/// <summary>Guid determinista (versión 8, RFC 9562) a partir de una clave: los primeros 16 bytes de su SHA-256.</summary>
public static class IdDeterminista
{
    public static Guid Para(string clave)
    {
        Span<byte> bytes = stackalloc byte[32];
        SHA256.HashData(Encoding.UTF8.GetBytes(clave), bytes);
        bytes[6] = (byte)((bytes[6] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes[..16], bigEndian: true);
    }
}

/// <summary>
/// Secuencia de folios por <c>(empresa, tipo, año)</c> como stream de eventos: la emisión toma el stream con bloqueo de fila
/// (<c>FetchForExclusiveWriting</c>), así dos aprobaciones simultáneas de la misma empresa esperan su turno en lugar de
/// fallar por concurrencia optimista; si la transacción se revierte, el número no se consume (sin huecos ni duplicados).
/// </summary>
public sealed class SecuenciaDeFolios
{
    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid EmpresaId { get; set; }

    public string Tipo { get; set; } = string.Empty;

    public int Anio { get; set; }

    public long Ultimo { get; set; }

    public static Guid IdPara(Guid empresaId, string tipo, int anio) => IdDeterminista.Para($"secuencia-folios|{empresaId:N}|{tipo}|{anio}");

    public FolioConsumido Consumir(DateTimeOffset ahora) => new(Ultimo + 1, ahora);

    public static SecuenciaDeFolios Create(SecuenciaDeFoliosIniciada e) => new() { EmpresaId = e.EmpresaId, Tipo = e.Tipo, Anio = e.Anio };

    public void Apply(FolioConsumido e) => Ultimo = e.Numero;
}

public sealed record SecuenciaDeFoliosIniciada(Guid EmpresaId, string Tipo, int Anio, DateTimeOffset Fecha);

public sealed record FolioConsumido(long Numero, DateTimeOffset Fecha);
