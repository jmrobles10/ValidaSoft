using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// Ficha 360 del tercero para la empresa activa (docs/03 §14 <c>Tercero360</c>, docs/06 <c>/terceros/:id</c>): identificación,
/// contactos y cada vínculo con esta empresa con su certificado, su cuenta designada y los documentos compartidos con él.
/// De las otras empresas del grupo solo dice que hay vínculo y en qué estado. Se arma al consultar.
/// </summary>
public static class FichaTerceroEndpoints
{
    [WolverineGet("/api/v1/terceros/{id}/ficha")]
    [Authorize(Policy = Permisos.TercerosLeer)]
    public static async Task<FichaTerceroDto> Obtener(Guid id, SesionDeEmpresa empresa, ISesiones sesiones, IContextoSolicitud contexto,
        IProtectorDeSecretos protector, EmisorDeCertificados emisor, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = await empresa.Sesion.LoadAsync<Tercero>(id, ct);
        var vinculosDelGrupo = tercero is null
            ? []
            : await empresa.Sesion.Query<Vinculo>().Where(v => v.TerceroId == id).OrderBy(v => v.CreadoEn).ToListAsync(ct);
        var propios = vinculosDelGrupo.Where(v => v.EmpresaId == empresa.EmpresaId).ToList();
        // Sin vínculo con la empresa activa la ficha no existe para ella (T-4): ni siquiera revela los de otras empresas.
        if (tercero is null || propios.Count == 0)
            throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero en la empresa.");

        var veVinculos = contexto.Scopes.Contains(Permisos.VinculosLeer);
        var otras = veVinculos
            ? vinculosDelGrupo.Where(v => v.EmpresaId != empresa.EmpresaId)
                .GroupBy(v => v.EmpresaId)
                .Select(g => new VinculoEnOtraEmpresaDto(g.Key, g.OrderByDescending(v => v.ActualizadoEn).First().Estado.ToString()))
                .ToList()
            : [];
        if (!veVinculos)
            return new FichaTerceroDto(tercero.ADto(), [], otras);

        var tipos = (await empresa.Sesion.LoadManyAsync<TipoVinculo>(ct, propios.Select(v => v.TipoVinculoId).Distinct().ToArray()))
            .ToDictionary(t => t.Id);
        var veCertificados = contexto.Scopes.Contains(Permisos.CertificadosLeer);
        var veDocumentos = contexto.Scopes.Contains(Permisos.DocumentosLeer);
        var datosEmpresa = veCertificados ? await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct) : null;
        var documentos = veDocumentos ? await DocumentosDelTerceroAsync(sesiones, id, ct) : [];
        var hoy = reloj.Hoy();

        var vinculos = new List<VinculoEnFichaDto>();
        foreach (var vinculo in propios)
        {
            CertificadoDelVinculoDto? certificado = null;
            if (veCertificados)
            {
                var emitido = await empresa.Sesion.LoadAsync<Dominio.Certificados.Certificado>(Dominio.Certificados.Certificado.IdPara(vinculo.Id), ct);
                certificado = emitido is null || emitido.EmpresaId != empresa.EmpresaId
                    ? new CertificadoDelVinculoDto(vinculo.Id, null, [])
                    : new CertificadoDelVinculoDto(vinculo.Id, emitido.ADto(emitido.Actual, datosEmpresa, emisor),
                        [.. emitido.Emisiones.OrderByDescending(e => e.Numero).Select(e => e.AResumen())]);
            }

            var compartidos = veDocumentos
                ? (IReadOnlyList<DocumentoVistaEmpresaDto>)(DocumentosDeVinculoEndpoints.Visible(vinculo)
                    ? [.. documentos.Select(d => DocumentosDeVinculoEndpoints.Vista(d, vinculo.Id, hoy)).OfType<DocumentoVistaEmpresaDto>()]
                    : [])
                : null;

            vinculos.Add(new VinculoEnFichaDto(vinculo.ADto(tipos.GetValueOrDefault(vinculo.TipoVinculoId)), certificado,
                await CuentaDesignadaEndpoints.DtoAsync(empresa, vinculo, contexto, protector, ct), compartidos));
        }

        return new FichaTerceroDto(tercero.ADto(), vinculos, otras);
    }

    private static async Task<IReadOnlyList<Documento>> DocumentosDelTerceroAsync(ISesiones sesiones, Guid terceroId, CancellationToken ct)
    {
        await using var boveda = sesiones.ConsultaBoveda();
        return await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).OrderBy(d => d.CreadoEn).ToListAsync(ct);
    }
}
