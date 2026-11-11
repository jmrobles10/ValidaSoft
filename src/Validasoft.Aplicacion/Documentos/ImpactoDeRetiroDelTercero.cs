using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>
/// Lo que pasará si el tercero retira una versión (RT-1), calculado con los mismos datos con que el sistema lo hace después: qué
/// versión queda vigente (<see cref="Documento.VersionTrasRetirar"/>), qué empresas la recibieron, aprobaron o revisan, y qué
/// secciones de la ficha la tienen de soporte. Solo lectura: no escribe nada. Del lado de las empresas solo cuenta
/// «la recibió / la aprobó / la revisa» y, de quien revisa, el folio; nunca sus decisiones por área ni sus observaciones.
/// </summary>
public static class ImpactoDeRetiroDelTercero
{
    public static async Task<ImpactoDeRetiroDto> CalcularAsync(IQuerySession global, IQuerySession boveda, Documento documento, int numero,
        IReadOnlyList<VinculoVisto> vinculos, CancellationToken ct)
    {
        documento.VersionRetirable(numero);
        var queda = documento.VersionTrasRetirar(numero);
        var esLaVigente = documento.VersionActual == numero;

        var visibles = vinculos.Where(v => documento.Ambito == AmbitoDocumento.Global || documento.VinculoId == v.Vinculo.Id).ToList();
        var expedientes = await ExpedientesDeLaBoveda.CargarAsync(global, visibles.Select(v => v.Vinculo.Id), ct);

        var empresas = new List<EmpresaAfectadaPorRetiroDto>();
        foreach (var visto in visibles)
        {
            var vinculo = visto.Vinculo;
            var delVinculo = expedientes.GetValueOrDefault(vinculo.Id);
            var abierto = ExpedientesDeLaBoveda.RequisitoDe(delVinculo?.Abierto, documento.Id);
            var aprobado = ExpedientesDeLaBoveda.RequisitoDe(delVinculo?.Aprobado, documento.Id);
            var enAbierto = abierto?.VersionActual?.Numero == numero;
            var hechos = new HechosDeRetiro(esLaVigente, queda,
                Recibio: documento.EstaCompartida(numero, vinculo.Id),
                Aprobo: (aprobado is { Estado: EstadoRequisito.Aprobado } && aprobado.VersionActual?.Numero == numero) ||
                        (enAbierto && abierto is { Estado: EstadoRequisito.Aprobado }),
                Revisa: enAbierto && abierto is { Estado: not (EstadoRequisito.Aprobado or EstadoRequisito.Pendiente or EstadoRequisito.NoAplica) },
                PuedeRevalidar: vinculo.ExpedienteAbiertoId is null && vinculo.TuvoAprobacion &&
                                vinculo.Estado is EstadoVinculo.Activo or EstadoVinculo.PorVencer or EstadoVinculo.Vencido);
            if (ImpactoDeRetiro.Calcular(hechos) is not { } impacto)
                continue;

            empresas.Add(new EmpresaAfectadaPorRetiroDto(vinculo.Id, visto.Empresa.APublica(vinculo.EmpresaId), impacto.Situacion.ToString(),
                impacto.Efecto.ToString(), impacto.Situacion == SituacionAnteRetiro.Revisa ? delVinculo?.Abierto?.Folio : null));
        }

        var terceroId = documento.TerceroId;
        var secciones = (await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).ToListAsync(ct))
            .Where(s => s.Vigente is { } v && v.Soportes.Any(x => x.DocumentoId == documento.Id && x.Version == numero))
            .Select(s => new SeccionAfectadaPorRetiroDto(s.Seccion, CatalogoDeSecciones.Buscar(s.Seccion)?.Nombre ?? s.Seccion, s.Periodo))
            .OrderBy(s => s.Nombre).ThenByDescending(s => s.Periodo)
            .ToList();

        return new ImpactoDeRetiroDto(documento.Id, numero, esLaVigente, queda, [.. empresas.OrderBy(e => e.Empresa.Nombre)], secciones);
    }
}
