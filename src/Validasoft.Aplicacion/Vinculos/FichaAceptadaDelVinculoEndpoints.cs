using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// La ficha aceptada de un vínculo, para la web de la empresa (FD-3): por cada sección que la empresa aceptó, la versión, cuándo y en qué
/// expediente, los datos de esa versión (inmutables) y el soporte con que se aceptó; y, <b>aparte</b>, lo que el tercero cambió después
/// (la versión vigente si es otra, campo por campo contra lo aceptado, con su soporte y la revalidación que la revisa). Es lo mismo que
/// <c>GET /api/v1/vinculos/{id}/ficha</c> (FM-35) pero pensado para leerse: datos tal cual los guardó el tercero, no normalizados para
/// un ERP. Lo que la empresa no aceptó no aparece (FM-3).
/// </summary>
public static class FichaAceptadaDelVinculoEndpoints
{
    private static readonly EstadoExpediente[] Abiertos = [EstadoExpediente.Abierto, EstadoExpediente.EnRevision, EstadoExpediente.Observado];

    [WolverineGet("/api/v1/vinculos/{id}/ficha-aceptada")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    [Authorize(Policy = Permisos.TercerosLeer)]
    public static async Task<FichaAceptadaDelVinculoDto> Obtener(Guid id, SesionDeEmpresa empresa, ISesiones sesiones, CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        var tercero = await empresa.Sesion.LoadAsync<Tercero>(vinculo.TerceroId, ct)
                      ?? throw new InvalidOperationException($"El vínculo {id} apunta a un tercero que no existe.");
        var aceptadas = await empresa.Sesion.Query<SeccionAceptada>().Where(a => a.VinculoId == id && a.EmpresaId == empresa.EmpresaId).ToListAsync(ct);
        if (aceptadas.Count == 0)
            return new FichaAceptadaDelVinculoDto(vinculo.Id, vinculo.TerceroId, tercero.TipoPersona.ToString(), []);

        IReadOnlyDictionary<Guid, SeccionDeFicha> streams;
        IReadOnlyDictionary<Guid, Documento> documentos;
        await using (var boveda = sesiones.ConsultaBoveda())
        {
            streams = (await boveda.LoadManyAsync<SeccionDeFicha>(ct, aceptadas.Select(a => a.SeccionId).Distinct().ToArray())).ToDictionary(s => s.Id);
            // Los soportes de lo aceptado y de lo que el tercero cambió después: sus nombres legibles y si el tercero los retiró (RT-4).
            var versiones = aceptadas.SelectMany(a => streams.GetValueOrDefault(a.SeccionId) is { } s
                ? new[] { s.Versiones.FirstOrDefault(v => v.Numero == a.Version), s.Vigente }
                : []).OfType<VersionDeSeccion>().ToList();
            var ids = versiones.SelectMany(v => v.Soportes).Select(s => s.DocumentoId).Distinct().ToArray();
            documentos = ids.Length == 0 ? new Dictionary<Guid, Documento>() : (await boveda.LoadManyAsync<Documento>(ct, ids)).ToDictionary(d => d.Id);
        }

        var revalidaciones = await empresa.Expedientes()
            .Where(e => e.VinculoId == id && e.Tipo == TipoExpediente.Actualizacion && e.Estado.IsOneOf(Abiertos))
            .ToListAsync(ct);

        var orden = CatalogoDeSecciones.Todas.Select(s => s.Codigo).ToList();
        var secciones = aceptadas
            .Select(a => (Aceptada: a, Stream: streams.GetValueOrDefault(a.SeccionId)))
            .Select(x => (x.Aceptada, x.Stream, Version: x.Stream?.Versiones.FirstOrDefault(v => v.Numero == x.Aceptada.Version)))
            .Where(x => x.Stream is not null && x.Version is not null)
            .OrderBy(x => orden.IndexOf(x.Aceptada.Seccion))
            .ThenByDescending(x => x.Aceptada.Periodo)
            .Select(x => Vista(x.Aceptada, x.Stream!, x.Version!, documentos, revalidaciones))
            .ToList();

        return new FichaAceptadaDelVinculoDto(vinculo.Id, vinculo.TerceroId, tercero.TipoPersona.ToString(), secciones);
    }

    private static SeccionAceptadaVistaDto Vista(SeccionAceptada aceptada, SeccionDeFicha seccion, VersionDeSeccion version,
        IReadOnlyDictionary<Guid, Documento> documentos, IReadOnlyList<Expediente> revalidaciones)
    {
        var definicion = CatalogoDeSecciones.Requerida(aceptada.Seccion);
        // La aceptación importada de un sistema de origen (F4, FM-42) no tiene expediente ni folio.
        var expediente = aceptada.ExpedienteId != Guid.Empty ? aceptada.ExpedienteId : (Guid?)null;
        var loAceptado = new LoAceptadoDeSeccionDto(version.Numero, aceptada.AceptadaEn, expediente, string.IsNullOrEmpty(aceptada.Folio) ? null : aceptada.Folio,
            aceptada.Origen.ToString(), version.Datos, [.. version.Soportes.Select(s => s.ADto(documentos))]);

        CambioPosteriorDelTerceroDto? posterior = null;
        if (seccion.Vigente is { } vigente && vigente.Numero > version.Numero)
        {
            var revision = revalidaciones.SelectMany(e => e.Requisitos
                    .Where(r => r.EsSeccion && r.Seccion == aceptada.Seccion && r.Periodo == aceptada.Periodo)
                    .Select(r => new RevalidacionDeSeccionDto(e.Id, e.Folio, r.Estado.ToString(), r.VersionActual?.Numero, Revalidacion.PlazoHasta(e))))
                .FirstOrDefault();
            posterior = new CambioPosteriorDelTerceroDto(vigente.Numero, vigente.Fecha,
                [.. ComparacionDeSecciones.Comparar(version.Datos, vigente.Datos).Select(c => new CambioDeCampoDto(c.Campo, c.Etiqueta, c.Anterior, c.Nuevo, c.TipoCambio.ToString()))],
                [.. vigente.Soportes.Select(s => s.ADto(documentos))], seccion.SinSoporte(vigente.Numero, version.Numero)?.ADto(), revision);
        }

        return new SeccionAceptadaVistaDto(definicion.Codigo, RequisitosDeSeccion.Nombre(definicion, aceptada.Periodo), aceptada.Periodo, loAceptado, posterior);
    }
}
