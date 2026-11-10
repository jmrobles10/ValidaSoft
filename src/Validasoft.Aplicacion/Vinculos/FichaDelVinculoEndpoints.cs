using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Contratos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// La ficha aceptada por la empresa en un vínculo (F3, FM-35), para que el SRM (o el Portal) cree o actualice el proveedor en
/// el ERP: por cada sección que la empresa aceptó (<see cref="SeccionAceptada"/>), la versión, la fecha y el contenido
/// normalizado (el mismo del evento <c>seccion.aceptada</c>), y si el tercero tiene una versión vigente más nueva. Además el
/// representante legal que va a AyF y la cuenta designada aprobada. Lo que la empresa no aceptó no aparece (FM-3).
/// </summary>
public static class FichaDelVinculoEndpoints
{
    [WolverineGet("/api/v1/vinculos/{id}/ficha")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    [Authorize(Policy = Permisos.TercerosLeer)]
    public static async Task<FichaDelVinculoDto> Obtener(Guid id, SesionDeEmpresa empresa, ISesiones sesiones, IContextoSolicitud contexto,
        IProtectorDeSecretos protector, CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        var tercero = await empresa.Sesion.LoadAsync<Tercero>(vinculo.TerceroId, ct)
                      ?? throw new InvalidOperationException($"El vínculo {id} apunta a un tercero que no existe.");
        var aceptadas = await empresa.Sesion.Query<SeccionAceptada>()
            .Where(a => a.VinculoId == id && a.EmpresaId == empresa.EmpresaId)
            .ToListAsync(ct);

        IReadOnlyDictionary<Guid, SeccionDeFicha> streams;
        await using (var boveda = sesiones.ConsultaBoveda())
        {
            streams = aceptadas.Count == 0
                ? new Dictionary<Guid, SeccionDeFicha>()
                : (await boveda.LoadManyAsync<SeccionDeFicha>(ct, aceptadas.Select(a => a.SeccionId).Distinct().ToArray())).ToDictionary(s => s.Id);
        }

        // La versión aceptada de cada sección (inmutable) y sus datos tipados.
        var orden = CatalogoDeSecciones.Todas.Select(s => s.Codigo).ToList();
        var filas = aceptadas
            .Select(a => (Aceptada: a, Seccion: streams.GetValueOrDefault(a.SeccionId)))
            .Select(x => (x.Aceptada, x.Seccion, Version: x.Seccion?.Versiones.FirstOrDefault(v => v.Numero == x.Aceptada.Version)))
            .Where(x => x.Version is not null)
            .Select(x => (x.Aceptada, x.Seccion, Datos: ContenidoParaErp.Datos(x.Aceptada.Seccion, x.Version!.Datos)))
            .OrderBy(x => orden.IndexOf(x.Aceptada.Seccion))
            .ThenByDescending(x => x.Aceptada.Periodo)
            .ToList();

        ICatalogosDeFicha catalogos;
        await using (var global = sesiones.ConsultaGlobal())
            catalogos = await ContenidoParaErp.CatalogosAsync(global, filas.Select(f => (f.Aceptada.Seccion, f.Datos)), ct);

        var secciones = filas.Select(f =>
        {
            var definicion = CatalogoDeSecciones.Requerida(f.Aceptada.Seccion);
            var vigente = f.Seccion?.VersionVigente;
            return new SeccionAceptadaDto(definicion.Codigo, definicion.Nombre, f.Aceptada.Periodo, f.Aceptada.Version, f.Aceptada.AceptadaEn,
                f.Aceptada.ExpedienteId, f.Aceptada.Folio, f.Aceptada.Huella, ContenidoParaErp.Normalizar(f.Datos, catalogos), vigente,
                vigente is { } v && v != f.Aceptada.Version);
        }).ToList();

        var representante = RepresentanteLegalPrincipal.Elegir(tercero.TipoPersona,
            filas.Select(f => f.Datos).OfType<DatosGenerales>().FirstOrDefault(), filas.Select(f => f.Datos).OfType<DatosLegales>().FirstOrDefault());
        var cuenta = (await CuentaDesignadaEndpoints.DtoAsync(empresa, vinculo, contexto, protector, ct)).Actual;

        return new FichaDelVinculoDto(vinculo.Id, vinculo.EmpresaId, vinculo.TerceroId,
            [.. vinculo.Referencias.Select(r => new ReferenciaExternaDto(r.Sistema, r.Id))], tercero.TipoPersona.ToString(), secciones,
            ContenidoParaErp.Representante(representante), cuenta);
    }
}
