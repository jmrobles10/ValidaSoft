using Marten;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Lo que una empresa ve del cruce sección ↔ documento en un expediente (F5, FM-8, FM-13). El cruce es un hecho compartido de
/// la versión de la sección, pero la empresa solo ve los cruces con versiones de documento cuyo análisis de IA recibió (consumió
/// su validación, IA-3/IA-11); los demás documentos que respaldan la sección quedan «sin análisis» (revisión manual). Un
/// requisito de documento con «Usar como soporte» apagado no se cruza: sus cruces no se muestran ni cuentan.
/// </summary>
public sealed class CrucesDelExpediente
{
    public const string EstadoCruzado = "Cruzado";
    public const string EstadoManual = "Manual";
    public const string EstadoSinDocumento = "SinDocumento";
    public const string MotivoSinAnalisis = "sin-analisis";

    private readonly Dictionary<Guid, CruceDeRequisitoDto> _secciones = [];
    private readonly Dictionary<Guid, SoporteDeSeccionesDto> _documentos = [];

    public static readonly CrucesDelExpediente Vacio = new();

    public CruceDeRequisitoDto? DeSeccion(Guid requisitoId) => _secciones.GetValueOrDefault(requisitoId);

    public SoporteDeSeccionesDto? DeDocumento(Guid requisitoId) => _documentos.GetValueOrDefault(requisitoId);

    /// <summary>Requisitos (de sección o de documento) con una discrepancia crítica que la empresa ve: no se aprueban sin justificación.</summary>
    public IReadOnlySet<Guid> ConDiscrepanciaCritica =>
        _secciones.Where(s => s.Value.DiscrepanciaCritica).Select(s => s.Key)
            .Concat(_documentos.Where(d => d.Value.DiscrepanciaCritica).Select(d => d.Key)).ToHashSet();

    /// <param name="sesionDelTenant">Sesión del tenant de la empresa (validaciones consumidas, flujo, tercero).</param>
    /// <param name="boveda">Consulta del tenant técnico <c>boveda</c> (secciones y documentos del tercero).</param>
    public static async Task<CrucesDelExpediente> CargarAsync(IQuerySession sesionDelTenant, IQuerySession boveda, Expediente expediente, Guid empresaId,
        CancellationToken ct)
    {
        if (!expediente.ConRequisitos)
            return Vacio;
        var porDocumento = expediente.Requisitos.Where(r => !r.EsSeccion && MapaDeRespaldo.RespaldaAlguna(r.TipoCanonico)).ToList();
        var porSeccion = expediente.Requisitos.Where(r => r.EsSeccion && r.Seccion is not null).ToList();
        if (porDocumento.Count == 0 && porSeccion.Count == 0)
            return Vacio;

        var terceroId = expediente.TerceroId;
        var tercero = await sesionDelTenant.LoadAsync<Tercero>(terceroId, ct);
        var secciones = (await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).ToListAsync(ct)).ToDictionary(s => s.Id);
        var documentos = (await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct)).ToList();
        var ids = documentos.Select(d => d.Id).ToArray();
        var consumidas = ids.Length == 0
            ? new HashSet<(Guid, int)>()
            : (await sesionDelTenant.Query<ValidacionIaConsumida>().Where(v => v.EmpresaId == empresaId && v.DocumentoId.IsOneOf(ids)).ToListAsync(ct))
                .Select(v => (v.DocumentoId, v.Numero)).ToHashSet();

        // «Usar como soporte» de cada requisito de documento según la versión del flujo del expediente (activo por omisión).
        var requisitosDelFlujo = new Dictionary<Guid, RequisitoDelFlujo>();
        if (expediente.Flujo is { } delFlujo && await sesionDelTenant.LoadAsync<Flujo>(delFlujo.FlujoId, ct) is { } flujo &&
            flujo.Versiones.FirstOrDefault(v => v.Id == delFlujo.FlujoVersionId) is { } version)
        {
            foreach (var r in version.Requisitos)
                requisitosDelFlujo[r.Id] = r;
        }

        bool UsaComoSoporte(RequisitoDeExpediente r) =>
            requisitosDelFlujo.TryGetValue(r.Id, out var delFlujo) ? delFlujo.UsaComoSoporte(r.TipoCanonico) : MapaDeRespaldo.RespaldaAlguna(r.TipoCanonico);

        var apagados = porDocumento.Where(r => !UsaComoSoporte(r)).Select(r => r.TipoCanonico!).ToHashSet(StringComparer.OrdinalIgnoreCase);
        var resultado = new CrucesDelExpediente();
        var nombres = documentos.ToDictionary(d => d.Id, d => d.NombreTipo);

        foreach (var requisito in porSeccion)
        {
            var seccion = secciones.GetValueOrDefault(SeccionDeFicha.IdPara(terceroId, requisito.Seccion!, requisito.Periodo));
            var versionSeccion = requisito.VersionActual is { } v
                ? seccion?.Versiones.FirstOrDefault(x => x.Numero == v.Numero)
                : seccion?.Vigente;
            if (seccion is null || versionSeccion is null)
                continue;

            var respaldos = CrucesDeFicha.Respaldos(seccion, versionSeccion, documentos).Where(r => !apagados.Contains(r.Documento.TipoCanonico!)).ToList();
            var visibles = versionSeccion.Cruces
                .Where(c => consumidas.Contains((c.DocumentoId, c.VersionDocumento)) && !apagados.Contains(c.TipoDocumento))
                .GroupBy(c => c.DocumentoId).Select(g => g.MaxBy(c => c.VersionDocumento)!)
                .ToList();
            var sinCruce = respaldos.Where(r => visibles.All(c => c.DocumentoId != r.Documento.Id))
                .GroupBy(r => r.Documento.Id).Select(g => g.MaxBy(r => r.Version))
                .Select(r => new DocumentoSinCruceDto(r.Documento.Id, r.Version, r.Documento.TipoCanonico ?? r.Documento.NombreTipo, r.Documento.NombreTipo,
                    MotivoSinAnalisis))
                .ToList();
            var estado = visibles.Count > 0 ? EstadoCruzado : sinCruce.Count > 0 ? EstadoManual : EstadoSinDocumento;
            resultado._secciones[requisito.Id] = new CruceDeRequisitoDto(estado, visibles.Any(c => c.DiscrepanciaCritica),
                [.. visibles.Select(c => ADto(c, nombres))], sinCruce);
        }

        foreach (var requisito in porDocumento.Where(UsaComoSoporte))
        {
            if (requisito.VersionActual is not { } v)
                continue;
            var visible = consumidas.Contains((v.DocumentoId, v.Numero));
            var filas = new List<SeccionRespaldadaDto>();
            foreach (var codigo in MapaDeRespaldo.Secciones(requisito.TipoCanonico))
            {
                var definicion = CatalogoDeSecciones.Buscar(codigo)!;
                if (tercero is not null && !definicion.Obligatoria(tercero.TipoPersona))
                    continue;
                var deLaSeccion = secciones.Values.Where(s => s.Seccion == codigo && s.Vigente is not null).OrderByDescending(s => s.Periodo).ToList();
                if (deLaSeccion.Count == 0)
                {
                    filas.Add(new SeccionRespaldadaDto(codigo, definicion.Nombre, null, null, null));
                    continue;
                }

                foreach (var seccion in deLaSeccion)
                {
                    var cruce = visible ? seccion.Vigente!.Cruces.FirstOrDefault(c => c.Es(v.DocumentoId, v.Numero)) : null;
                    if (definicion.PorPeriodo && cruce is null)
                        continue; // Los estados financieros respaldan solo la financiera de su periodo.
                    filas.Add(new SeccionRespaldadaDto(codigo, RequisitosDeSeccion.Nombre(definicion, seccion.Periodo), seccion.Periodo,
                        seccion.VersionVigente, cruce is null ? null : ADto(cruce, nombres)));
                }
            }

            resultado._documentos[requisito.Id] = new SoporteDeSeccionesDto(!visible, filas.Any(f => f.Cruce?.DiscrepanciaCritica == true), filas);
        }

        return resultado;
    }

    public static CruceDeSeccionDto ADto(CruceDeSeccion c, IReadOnlyDictionary<Guid, string> nombres) =>
        new(c.DocumentoId, c.VersionDocumento, c.TipoDocumento, nombres.GetValueOrDefault(c.DocumentoId) ?? c.TipoDocumento, c.Confianza, c.Legible, c.Fecha,
            c.Coincidencias, c.Discrepancias, c.NoVerificables, c.DiscrepanciaCritica,
            [.. c.Campos.Select(x => new CampoCruzadoDto(x.Campo, x.Etiqueta, x.Declarado, x.Extraido, x.Resultado.ToString(), x.Critico, x.Detalle))]);
}
