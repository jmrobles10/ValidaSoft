using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// Ficha 360 del tercero para la empresa activa (docs/03 §14 <c>Tercero360</c>, docs/06 <c>/terceros/:id</c>): identificación,
/// contactos y cada vínculo con esta empresa con su certificado, su cuenta designada y los documentos compartidos con él.
/// De las otras empresas del grupo solo dice que hay vínculo y en qué estado. Incluye las secciones vigentes de la ficha
/// maestra (datos, completitud y soportes) y, por cada vínculo con la empresa activa, lo que aceptó de cada una, si hay una
/// versión nueva pendiente y su revalidación (F2). Se arma al consultar.
/// </summary>
public static class FichaTerceroEndpoints
{
    private static readonly EstadoExpediente[] Abiertos = [EstadoExpediente.Abierto, EstadoExpediente.EnRevision, EstadoExpediente.Observado];

    [WolverineGet("/api/v1/terceros/{id}/ficha")]
    [Authorize(Policy = Permisos.TercerosLeer)]
    public static async Task<FichaTerceroDto> Obtener(Guid id, SesionDeEmpresa empresa, ISesiones sesiones, IContextoSolicitud contexto,
        IProtectorDeCamposDelGrupo campos, EmisorDeCertificados emisor, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = await empresa.Sesion.LoadAsync<Tercero>(id, ct);
        var vinculosDelGrupo = tercero is null
            ? []
            : await empresa.Sesion.Query<Vinculo>().Where(v => v.TerceroId == id).OrderBy(v => v.CreadoEn).ToListAsync(ct);
        var propios = vinculosDelGrupo.Where(v => v.EmpresaId == empresa.EmpresaId).ToList();
        // Sin vínculo con la empresa activa la ficha no existe para ella (T-4): ni siquiera revela los de otras empresas.
        if (tercero is null || propios.Count == 0)
            throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero en la empresa.");

        // Secciones vigentes de la ficha maestra (FM-20): con terceros:leer y vínculo con la empresa activa (ya comprobado). Si todos sus vínculos tienen el acceso
        // aislado pendiente o rechazado (el correo invitado no es contacto del tercero), la ficha global del tercero no se le comparte: ni sus secciones ni sus contactos.
        var sinAccesoALaBoveda = propios.All(v => !v.BovedaDisponible);
        IReadOnlyList<SeccionFichaDto> secciones = [];
        if (!sinAccesoALaBoveda)
        {
            await using var boveda = sesiones.ConsultaBoveda();
            secciones = await Ficha.FichaMaestraEndpoints.VigentesAsync(boveda, id, ct);
            secciones = await ConAceptacionesAsync(empresa, propios, secciones, ct);
        }

        var terceroDto = sinAccesoALaBoveda && tercero.CreadoPorTenant != contexto.TenantId ? tercero.ADto() with { Contactos = [] } : tercero.ADto();

        var veVinculos = contexto.Scopes.Contains(Permisos.VinculosLeer);
        var otras = veVinculos
            ? vinculosDelGrupo.Where(v => v.EmpresaId != empresa.EmpresaId)
                .GroupBy(v => v.EmpresaId)
                .Select(g => new VinculoEnOtraEmpresaDto(g.Key, g.OrderByDescending(v => v.ActualizadoEn).First().Estado.ToString()))
                .ToList()
            : [];
        if (!veVinculos)
            return new FichaTerceroDto(terceroDto, [], otras, secciones);

        var tipos = (await empresa.Sesion.LoadManyAsync<TipoVinculo>(ct, propios.Select(v => v.TipoVinculoId).Distinct().ToArray()))
            .ToDictionary(t => t.Id);
        var veCertificados = contexto.Scopes.Contains(Permisos.CertificadosLeer);
        var veDocumentos = contexto.Scopes.Contains(Permisos.DocumentosLeer);
        var datosEmpresa = veCertificados ? await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct) : null;
        var documentos = veDocumentos ? await DocumentosDelTerceroAsync(sesiones, id, ct) : [];
        var conIa = veDocumentos ? await DocumentosDeVinculoEndpoints.ValidacionesDeIaAsync(empresa, documentos, ct) : new HashSet<(Guid, int)>();
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
                    ? [.. documentos.Select(d => DocumentosDeVinculoEndpoints.Vista(d, vinculo.Id, hoy, conIa)).OfType<DocumentoVistaEmpresaDto>()]
                    : [])
                : null;

            vinculos.Add(new VinculoEnFichaDto(vinculo.ADto(tipos.GetValueOrDefault(vinculo.TipoVinculoId)), certificado,
                await CuentaDesignadaEndpoints.DtoAsync(empresa, vinculo, contexto, campos, ct), compartidos));
        }

        return new FichaTerceroDto(terceroDto, vinculos, otras, secciones);
    }

    /// <summary>
    /// Por sección y vínculo de la empresa activa (FM-3, FM-29): la versión aceptada (privada de la empresa), si la vigente del
    /// tercero es otra y la revalidación abierta que la revisa.
    /// </summary>
    private static async Task<IReadOnlyList<SeccionFichaDto>> ConAceptacionesAsync(SesionDeEmpresa empresa, IReadOnlyList<Vinculo> propios,
        IReadOnlyList<SeccionFichaDto> secciones, CancellationToken ct)
    {
        if (secciones.Count == 0)
            return secciones;

        var ids = propios.Select(v => v.Id).ToArray();
        var aceptadas = await empresa.Sesion.Query<SeccionAceptada>().Where(a => a.VinculoId.IsOneOf(ids)).ToListAsync(ct);
        var revalidaciones = await empresa.Expedientes()
            .Where(e => e.VinculoId.IsOneOf(ids) && e.Tipo == TipoExpediente.Actualizacion && e.Estado.IsOneOf(Abiertos))
            .ToListAsync(ct);

        return
        [
            .. secciones.Select(s => s with
            {
                Aceptaciones =
                [
                    .. propios.Select(v =>
                    {
                        var aceptada = aceptadas.FirstOrDefault(a => a.VinculoId == v.Id && a.Seccion == s.Seccion && a.Periodo == s.Periodo);
                        var revision = revalidaciones.Where(e => e.VinculoId == v.Id)
                            .SelectMany(e => e.Requisitos.Where(r => r.EsSeccion && r.Seccion == s.Seccion && r.Periodo == s.Periodo)
                                .Select(r => new RevalidacionDeSeccionDto(e.Id, e.Folio, r.Estado.ToString(), r.VersionActual?.Numero,
                                    Revalidacion.PlazoHasta(e))))
                            .FirstOrDefault();
                        // La aceptación importada del Portal (F4, FM-42) no tiene expediente ni folio.
                        var expediente = aceptada?.ExpedienteId is { } e && e != Guid.Empty ? e : (Guid?)null;
                        return new AceptacionDeSeccionDto(v.Id, aceptada?.Version, aceptada?.AceptadaEn, expediente,
                            string.IsNullOrEmpty(aceptada?.Folio) ? null : aceptada.Folio,
                            aceptada is not null && s.Version is { } vigente && vigente != aceptada.Version, revision, aceptada?.Origen.ToString());
                    })
                ]
            })
        ];
    }

    private static async Task<IReadOnlyList<Documento>> DocumentosDelTerceroAsync(ISesiones sesiones, Guid terceroId, CancellationToken ct)
    {
        await using var boveda = sesiones.ConsultaBoveda();
        return await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).OrderBy(d => d.CreadoEn).ToListAsync(ct);
    }
}
