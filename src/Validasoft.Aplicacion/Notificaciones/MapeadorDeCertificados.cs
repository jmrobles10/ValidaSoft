using JasperFx.Events;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>
/// Eventos públicos del stream <c>Certificado-{id}</c> (NW-13). Se registra como otro <see cref="IMapeadorDeEventos"/>
/// (NW-2). La suscripción corre después del commit: los datos de cada emisión se toman del evento o de la emisión con ese
/// número (inmutable), no del estado actual del certificado, para que un reproceso publique lo mismo.
/// </summary>
public sealed class MapeadorDeCertificados : IMapeadorDeEventos
{
    public static readonly IReadOnlyList<TipoEventoPublicoDto> Tipos =
    [
        new("certificado.emitido", "Se emitió el certificado del vínculo (o una emisión nueva que reemplaza la anterior)."),
        new("certificado.reemplazado", "Una emisión quedó reemplazada por otra más reciente (sigue verificable)."),
        new("certificado.con_pendientes", "Una versión nueva de un documento aceptado está en revalidación; el certificado sigue válido."),
        new("certificado.suspendido", "El certificado se suspendió porque el vínculo se bloqueó."),
        new("certificado.restablecido", "El certificado se restableció al desbloquearse el vínculo."),
        new("certificado.revocado", "El certificado se revocó."),
        new("certificado.vencido", "El certificado llegó a su fecha de vigencia.")
    ];

    public IReadOnlyList<TipoEventoPublicoDto> Catalogo => Tipos;

    public async ValueTask<EventoPublicoMapeado?> MapearAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct)
    {
        if (evento.Data is not (CertificadoEmitido or CertificadoReemplazado or CertificadoConPendientes or CertificadoSuspendido or CertificadoRestablecido
                or CertificadoRevocado or CertificadoVencido))
            return null;
        if (!Guid.TryParse(evento.TenantId, out var tenantId))
            return null;

        var certificado = await contexto.CertificadoAsync(tenantId, evento.StreamId, ct);
        var emitido = evento.Data as CertificadoEmitido;
        var empresaId = emitido?.EmpresaId ?? certificado?.EmpresaId;
        var vinculoId = emitido?.VinculoId ?? certificado?.VinculoId;
        var terceroId = emitido?.TerceroId ?? certificado?.TerceroId;
        if (empresaId is null || vinculoId is not { } idVinculo || terceroId is not { } idTercero)
            return null;

        var referencias = Referencias(await contexto.VinculoAsync(tenantId, idVinculo, ct));

        CertificadoEnEventoDto? Emision(int numero, EstadoCertificado estado) =>
            certificado?.Emisiones.FirstOrDefault(x => x.Numero == numero) is { } e
                ? new CertificadoEnEventoDto(e.Folio, e.Numero, estado.ToString(), e.HashContenido, e.VigenteHasta, e.UrlVerificacion)
                : null;

        DatosCertificadoDto? Datos(CertificadoEnEventoDto? emision) =>
            emision is null ? null : new DatosCertificadoDto(evento.StreamId, idVinculo, referencias, idTercero, emision);

        (string Tipo, DatosCertificadoDto? Datos) resultado = evento.Data switch
        {
            CertificadoEmitido e => ("certificado.emitido", new DatosCertificadoDto(evento.StreamId, idVinculo, referencias, idTercero,
                new CertificadoEnEventoDto(e.Folio, e.Emision, nameof(EstadoCertificado.Vigente), e.HashContenido, e.VigenteHasta, e.UrlVerificacion),
                ExpedienteId: e.ExpedienteId, ExpedienteFolio: e.ExpedienteFolio)),
            CertificadoReemplazado r => ("certificado.reemplazado", Datos(Emision(r.EmisionAnterior, EstadoCertificado.Reemplazado)) is { } d
                ? d with
                {
                    EstadoAnterior = nameof(EstadoCertificado.Vigente),
                    Motivo = r.Motivo,
                    FolioReemplazo = certificado?.Emisiones.FirstOrDefault(x => x.Numero == r.EmisionNueva)?.Folio
                }
                : null),
            CertificadoConPendientes p => ("certificado.con_pendientes", Datos(Emision(p.Emision, EstadoCertificado.ConPendientes)) is { } d
                ? d with { EstadoAnterior = nameof(EstadoCertificado.Vigente), Motivo = p.Motivo, ExpedienteId = p.ExpedienteId }
                : null),
            CertificadoSuspendido s => ("certificado.suspendido", Datos(Emision(s.Emision, EstadoCertificado.Suspendido)) is { } d
                ? d with { EstadoAnterior = s.EstadoPrevio.ToString(), Motivo = s.Motivo }
                : null),
            CertificadoRestablecido r => ("certificado.restablecido", Datos(Emision(r.Emision, r.EstadoRestituido)) is { } d
                ? d with { EstadoAnterior = nameof(EstadoCertificado.Suspendido), Motivo = r.Motivo }
                : null),
            CertificadoRevocado r => ("certificado.revocado", Datos(Emision(r.Emision, EstadoCertificado.Revocado)) is { } d
                ? d with { Motivo = r.Motivo }
                : null),
            CertificadoVencido v => ("certificado.vencido", Datos(Emision(v.Emision, EstadoCertificado.Vencido)) is { } d
                ? d with { Certificado = d.Certificado with { VigenteHasta = v.VigenteHasta } }
                : null),
            _ => ("", null)
        };

        return resultado.Datos is null ? null : new EventoPublicoMapeado(resultado.Tipo, tenantId, empresaId, resultado.Datos);
    }

    private static IReadOnlyList<ReferenciaExternaDto> Referencias(Vinculo? vinculo) =>
        vinculo is null ? [] : [.. vinculo.Referencias.Select(r => new ReferenciaExternaDto(r.Sistema, r.Id))];
}
