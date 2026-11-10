using JasperFx.Events;
using Validasoft.Contratos;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Aplicacion.Notificaciones;

/// <summary>
/// Eventos públicos de las verificaciones externas (H7, docs/05 §7, VE-13): <c>expediente.verificacion_registrada</c> (consulta
/// de listas o estudio de cumplimiento), <c>expediente.coincidencia_resuelta</c> y <c>expediente.verificacion_expirada</c>. Los
/// datos salen del evento (estado y resultado de esa consulta) y nunca llevan nombres de personas, listas ni justificaciones:
/// solo conteos, estado, resultado y, en el estudio, el nivel de riesgo.
/// </summary>
public sealed class MapeadorDeVerificaciones : IMapeadorDeEventos
{
    public const string Registrada = "expediente.verificacion_registrada";
    public const string CoincidenciaResuelta = "expediente.coincidencia_resuelta";
    public const string Expirada = "expediente.verificacion_expirada";

    public static readonly IReadOnlyList<TipoEventoPublicoDto> Tipos =
    [
        new(Registrada, "Se registró una verificación del expediente: consulta de listas restrictivas o estudio de cumplimiento."),
        new(CoincidenciaResuelta, "Cumplimiento descartó o confirmó una coincidencia en listas."),
        new(Expirada, "Venció una verificación con el expediente abierto (las listas se reconsultan; el estudio se repite).")
    ];

    public IReadOnlyList<TipoEventoPublicoDto> Catalogo => Tipos;

    public async ValueTask<EventoPublicoMapeado?> MapearAsync(IEvent evento, IContextoDeEventos contexto, CancellationToken ct)
    {
        if (evento.Data is not (VerificacionExternaRegistrada or EstudioDeCumplimientoRegistrado or Dominio.Expedientes.CoincidenciaResuelta or VerificacionExpirada) ||
            !Guid.TryParse(evento.TenantId, out var tenantId) || await contexto.ExpedienteAsync(tenantId, evento.StreamId, ct) is not { } expediente)
            return null;

        var vinculo = await contexto.VinculoAsync(tenantId, expediente.VinculoId, ct);
        IReadOnlyList<ReferenciaExternaDto> referencias = vinculo is null ? [] : [.. vinculo.Referencias.Select(r => new ReferenciaExternaDto(r.Sistema, r.Id))];
        bool Bloqueante(TipoVerificacion tipo) => expediente.BuscarVerificacion(tipo)?.Bloqueante ?? false;

        (string Tipo, DatosVerificacionDto Datos) resultado = evento.Data switch
        {
            VerificacionExternaRegistrada r => (Registrada, new DatosVerificacionDto(expediente.Id, expediente.Folio, expediente.VinculoId, referencias,
                r.Tipo.ToString(), r.Proveedor, r.Resultado.ToString(),
                (r.Coincidencias.Any(c => c.Heredada is null) ? EstadoVerificacion.CoincidenciaPorAnalizar : EstadoVerificacion.Satisfecha).ToString(),
                Bloqueante(r.Tipo), r.VigenteHasta, r.Coincidencias.Count, r.Coincidencias.Count(c => c.Heredada is null))),
            EstudioDeCumplimientoRegistrado e => (Registrada, new DatosVerificacionDto(expediente.Id, expediente.Folio, expediente.VinculoId, referencias,
                nameof(TipoVerificacion.EstudioDeCumplimiento), "Oficial de cumplimiento",
                (e.Estudio.Concepto == ConceptoCumplimiento.Favorable ? ResultadoVerificacion.Aprobado : ResultadoVerificacion.Rechazado).ToString(),
                (e.Estudio.Concepto == ConceptoCumplimiento.Favorable ? EstadoVerificacion.Satisfecha : EstadoVerificacion.Fallida).ToString(),
                Bloqueante(TipoVerificacion.EstudioDeCumplimiento), e.VigenteHasta, 0, 0, NivelRiesgo: e.Estudio.NivelRiesgo)),
            Dominio.Expedientes.CoincidenciaResuelta c => (CoincidenciaResuelta, Datos(c.Tipo, c.Decision.ToString())),
            VerificacionExpirada x => (Expirada, Datos(x.Tipo, null) with { Estado = nameof(EstadoVerificacion.Expirada), VigenteHasta = x.VencioEn }),
            _ => throw new InvalidOperationException()
        };
        return new EventoPublicoMapeado(resultado.Tipo, tenantId, expediente.EmpresaId, resultado.Datos);

        DatosVerificacionDto Datos(TipoVerificacion tipo, string? decision)
        {
            var v = expediente.BuscarVerificacion(tipo);
            return new DatosVerificacionDto(expediente.Id, expediente.Folio, expediente.VinculoId, referencias, tipo.ToString(), v?.Proveedor,
                v?.Resultado?.ToString(), (v?.Estado ?? EstadoVerificacion.Pendiente).ToString(), v?.Bloqueante ?? false, v?.VigenteHasta,
                v?.Coincidencias.Count ?? 0, v?.Coincidencias.Count(c => !c.Resuelta) ?? 0, decision);
        }
    }
}
