using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Integracion;

/// <summary>
/// Consulta rápida de certificación para Marco y los ERP (H9, docs/05 §2.6 y §5.6, docs/08 §5; CN-1, CN-2): por
/// identificación del tercero y tipo de vínculo (o por id del vínculo) en la empresa de <c>X-Empresa</c>, ¿tiene certificado,
/// en qué estado, hasta cuándo y está bloqueado? Son tres viajes a la base por índices únicos o llave primaria (tercero y
/// tipo de vínculo en un lote; el vínculo por <c>ux_vinculo_natural</c>; certificado y expediente de actualización en otro
/// lote), sin proyección nueva. Responde <c>ETag</c> (huella del cuerpo) y <c>If-None-Match</c> ⇒ 304.
/// </summary>
public static class ConsultaCertificacionEndpoints
{
    /// <summary>Cuánto puede reutilizar el integrador la respuesta sin volver a preguntar (con <c>If-None-Match</c>, 304).</summary>
    public const int SegundosDeCache = 300;

    public const string SinVinculo = "SinVinculo";

    /// <summary>
    /// <c>?pais=CO&amp;tipoIdentificacion=NIT&amp;numero=900123456&amp;tipoVinculo=PROVEEDOR</c> o <c>?vinculoId=</c>. El número
    /// se normaliza (puntos, guiones y DV se aceptan). Sin <c>tipoVinculo</c> sirve si el tercero tiene un solo vínculo en la
    /// empresa (si tiene varios: 422 <c>tipo-vinculo-requerido</c>). Sin vínculo, o con un tercero que no existe: 200 con
    /// <c>estadoVinculo: "SinVinculo"</c> (no revela si el tercero está en la plataforma).
    /// </summary>
    [WolverineGet("/api/v1/consultas/certificacion")]
    [Authorize(Policy = Permisos.ConsultasLeer)]
    [ProducesResponseType<ConsultaCertificacionDto>(StatusCodes.Status200OK)]
    [ProducesResponseType(StatusCodes.Status304NotModified)]
    public static async Task<IResult> Consultar(HttpContext http, string? pais, string? tipoIdentificacion, string? numero, string? tipoVinculo,
        Guid? vinculoId, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var empresaId = contexto.EmpresaRequerida();
        await using var sesion = sesiones.DelTenant();
        var vinculo = vinculoId is { } id
            ? await PorIdAsync(sesion, empresaId, id, ct)
            : await PorIdentificacionAsync(sesion, empresaId, pais, tipoIdentificacion, numero, tipoVinculo, ct);

        var respuesta = vinculo is null ? SinVinculoDto() : await ArmarAsync(sesion, vinculo, Hoy(reloj), ct);
        return Responder(http, respuesta);
    }

    private static DateOnly Hoy(TimeProvider reloj) => Dominio.Documentos.CalculadoraDeVigencia.Fecha(reloj.GetUtcNow());

    private static async Task<Vinculo?> PorIdAsync(IDocumentSession sesion, Guid empresaId, Guid id, CancellationToken ct)
    {
        var vinculo = await sesion.LoadAsync<Vinculo>(id, ct);
        return vinculo is not null && vinculo.EmpresaId == empresaId
            ? vinculo
            : throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo en la empresa.");
    }

    private static async Task<Vinculo?> PorIdentificacionAsync(IDocumentSession sesion, Guid empresaId, string? pais, string? tipo, string? numero,
        string? tipoVinculo, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(tipo) || string.IsNullOrWhiteSpace(numero))
            throw ReglaDeNegocioException.Validacion("consulta-incompleta",
                "Indica la identificación del tercero (pais, tipoIdentificacion y numero) o el vinculoId.");

        var clave = new IdentificacionDto(string.IsNullOrWhiteSpace(pais) ? "CO" : pais, tipo, numero).ADominio().Clave;
        var codigoTipo = string.IsNullOrWhiteSpace(tipoVinculo) ? null : tipoVinculo.Trim().ToUpperInvariant();

        var lote = sesion.CreateBatchQuery();
        var terceroPedido = lote.Query<Tercero>().Where(t => t.ClaveIdentificacion == clave).FirstOrDefault();
        var tipoPedido = codigoTipo is null ? null : lote.Query<TipoVinculo>().Where(t => t.Codigo == codigoTipo).FirstOrDefault();
        await lote.Execute(ct);

        var tercero = await terceroPedido;
        var tipoDeVinculo = tipoPedido is null ? null : await tipoPedido;
        if (tercero is null || (codigoTipo is not null && tipoDeVinculo is null))
            return null;

        var terceroId = tercero.Id;
        if (tipoDeVinculo is not null)
        {
            var tipoId = tipoDeVinculo.Id;
            return await sesion.Query<Vinculo>()
                .Where(v => v.EmpresaId == empresaId && v.TerceroId == terceroId && v.TipoVinculoId == tipoId)
                .FirstOrDefaultAsync(ct);
        }

        var vinculos = await sesion.Query<Vinculo>().Where(v => v.EmpresaId == empresaId && v.TerceroId == terceroId).Take(2).ToListAsync(ct);
        return vinculos.Count > 1
            ? throw ReglaDeNegocioException.Validacion("tipo-vinculo-requerido",
                "El tercero tiene varios vínculos con la empresa: indica tipoVinculo (código) o vinculoId.")
            : vinculos.SingleOrDefault();
    }

    private static async Task<ConsultaCertificacionDto> ArmarAsync(IDocumentSession sesion, Vinculo vinculo, DateOnly hoy, CancellationToken ct)
    {
        var lote = sesion.CreateBatchQuery();
        var certificadoPedido = lote.Load<Certificado>(Certificado.IdPara(vinculo.Id));
        var expedientePedido = vinculo.ExpedienteAbiertoId is { } abierto ? lote.Load<Expediente>(abierto) : null;
        await lote.Execute(ct);
        var certificado = await certificadoPedido;
        if (certificado is not null && certificado.EmpresaId != vinculo.EmpresaId)
            certificado = null;
        var expediente = expedientePedido is null ? null : await expedientePedido;

        var actual = certificado?.Actual;
        var revalidacion = expediente is { Tipo: TipoExpediente.Actualizacion, EstaAbierto: true }
            ? Revalidaciones(expediente, certificado?.PendientesBloqueantes == true)
            : [];
        var motivo = MotivoDeBloqueo(vinculo, certificado);
        var cuenta = vinculo.CuentaDesignada is { Estado: EstadoCuentaDesignada.Aprobada } designada
            ? new CuentaDesignadaResumenDto("****" + designada.Cuenta.Ultimos4, designada.Cuenta.Banco)
            : null;

        return new ConsultaCertificacionDto(
            Certificado: certificado is not null,
            Folio: actual?.Folio,
            Estado: certificado?.Estado.ToString(),
            EstadoVinculo: vinculo.Estado.ToString(),
            VinculoId: vinculo.Id,
            TerceroId: vinculo.TerceroId,
            VigenteHasta: actual?.VigenteHasta,
            RevalidacionPendiente: revalidacion,
            DocumentosVencidos: actual is null ? [] : DocumentosVencidos(actual.ContenidoCanonico, hoy),
            Bloqueado: motivo is not null,
            MotivoBloqueo: motivo,
            CuentaDesignada: cuenta,
            UrlVerificacion: actual?.UrlVerificacion,
            Legacy: null);
    }

    /// <summary>
    /// CN-2: el bloqueo explícito manda (vínculo bloqueado o certificado suspendido), luego la revocación, el vencimiento y por
    /// último los pendientes de revalidación cuyo plazo venció (A-2, RV-4). Nulo = se puede pagar o contratar si hay
    /// certificado.
    /// </summary>
    public static string? MotivoDeBloqueo(Vinculo vinculo, Certificado? certificado)
    {
        if (vinculo.Estado == EstadoVinculo.Bloqueado || certificado?.Estado == EstadoCertificado.Suspendido)
            return "Bloqueo";
        if (certificado?.Estado == EstadoCertificado.Revocado || vinculo.Estado is EstadoVinculo.Anulado or EstadoVinculo.Desvinculado)
            return "Revocado";
        if (vinculo.Estado == EstadoVinculo.Vencido || certificado?.Estado == EstadoCertificado.Vencido)
            return "Vencido";
        if (certificado is { Estado: EstadoCertificado.ConPendientes, PendientesBloqueantes: true })
            return "PlazoRevalidacion";
        return null;
    }

    private static List<RevalidacionPendienteDto> Revalidaciones(Expediente actualizacion, bool bloqueante)
    {
        var plazo = DateOnly.FromDateTime(Revalidacion.PlazoHasta(actualizacion).UtcDateTime);
        return [.. actualizacion.Requisitos
            .Where(r => !r.Eximido && r.Estado != EstadoRequisito.Aprobado)
            .OrderBy(r => r.Codigo, StringComparer.Ordinal)
            .Select(r => new RevalidacionPendienteDto(r.Codigo, plazo, bloqueante))];
    }

    /// <summary>Códigos de los requisitos del certificado (contenido canónico) con vigencia anterior a hoy.</summary>
    private static List<string> DocumentosVencidos(string contenidoCanonico, DateOnly hoy)
    {
        if (string.IsNullOrEmpty(contenidoCanonico))
            return [];
        using var json = JsonDocument.Parse(contenidoCanonico);
        if (!json.RootElement.TryGetProperty("requisitos", out var requisitos) || requisitos.ValueKind != JsonValueKind.Array)
            return [];

        var vencidos = new List<string>();
        foreach (var requisito in requisitos.EnumerateArray())
        {
            if (requisito.TryGetProperty("vigenteHasta", out var vigencia) && vigencia.ValueKind == JsonValueKind.String &&
                DateOnly.TryParseExact(vigencia.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha) &&
                fecha < hoy && requisito.TryGetProperty("codigo", out var codigo) && codigo.GetString() is { } texto)
                vencidos.Add(texto);
        }

        return vencidos;
    }

    private static ConsultaCertificacionDto SinVinculoDto() =>
        new(false, null, null, SinVinculo, null, null, null, [], [], false, null, null, null, null);

    /// <summary>ETag fuerte = huella SHA-256 del cuerpo; con <c>If-None-Match</c> igual (o <c>*</c>) ⇒ 304 sin cuerpo.</summary>
    private static IResult Responder(HttpContext http, ConsultaCertificacionDto respuesta)
    {
        var cuerpo = JsonSerializer.SerializeToUtf8Bytes(respuesta, JsonEventos.Opciones);
        var etag = $"\"c-{Convert.ToHexStringLower(SHA256.HashData(cuerpo))[..32]}\"";
        http.Response.Headers.ETag = etag;
        http.Response.Headers.CacheControl = $"private, max-age={SegundosDeCache.ToString(CultureInfo.InvariantCulture)}";
        http.Response.Headers.Vary = "X-Empresa, X-Api-Key, Authorization";

        var pedidas = http.Request.Headers.IfNoneMatch.ToString();
        if (!string.IsNullOrWhiteSpace(pedidas) &&
            pedidas.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries)
                .Any(p => p == "*" || p == etag || p == "W/" + etag))
            return Results.StatusCode(StatusCodes.Status304NotModified);

        return Results.Bytes(cuerpo, "application/json; charset=utf-8");
    }
}
