using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>Traducción de la bóveda a DTOs. Nunca expone claves de almacenamiento ni comparticiones de otras empresas.</summary>
public static class MapeosDocumentos
{
    public static ReglaFrescuraDto ADto(this ReglaFrescura r) => new(r.Tipo.ToString(), r.Dias, r.Regla);

    public static ReglaFrescura? ADominio(this ReglaFrescuraDto? r) =>
        r is null
            ? null
            : Enum.TryParse<TipoReglaFrescura>(r.Tipo, true, out var tipo) && Enum.IsDefined(tipo)
                ? new ReglaFrescura(tipo, r.Dias, string.IsNullOrWhiteSpace(r.Regla) ? null : r.Regla.Trim())
                : throw ReglaDeNegocioException.Validacion("frescura-invalida",
                    $"'{r.Tipo}' no es una regla de frescura ({string.Join(", ", Enum.GetNames<TipoReglaFrescura>())}).");

    public static AmbitoDocumento? AAmbito(string? ambito) =>
        string.IsNullOrWhiteSpace(ambito)
            ? null
            : Enum.TryParse<AmbitoDocumento>(ambito, true, out var valor) && Enum.IsDefined(valor)
                ? valor
                : throw ReglaDeNegocioException.Validacion("ambito-invalido", "El ámbito debe ser Global o PorVinculo.");

    public static TipoDocumentoCanonicoDto ADto(this TipoDocumentoCanonico t) =>
        new(t.Codigo, t.Version, t.Nombre, t.Ambito.ToString(), t.Clase.ToString(), t.AplicaA.ToString(), t.TieneVencimientoIntrinseco,
            t.TieneFechaExpedicion, t.EsPeriodico, t.Periodicidad, t.PermiteRatificacion, t.DePersonaRelacionada, t.CamposClave,
            t.CamposCriticos, t.Sensibilidad.ToString(), t.ReglaFrescuraSugerida?.ADto(), t.RetencionAniosSugerida, t.CargaSoportada);

    public static TipoDocumentoTenantDto ADto(this TipoDocumentoTenant t, IReadOnlyDictionary<Guid, TipoVinculo> tiposVinculo) =>
        new(t.Id, t.Codigo, t.Nombre, t.TipoCanonico, t.VersionCanonico, t.Ambito.ToString(), t.Clase.ToString(), t.TextoAyuda,
            t.ReglaFrescura.ADto(), t.DiasAviso, t.DiasGracia, t.DiasRevalidacion, t.Activo,
            [.. t.Exigencias.Select(e => new ExigenciaDocumentoDto(e.EmpresaId, e.TipoVinculoId,
                tiposVinculo.TryGetValue(e.TipoVinculoId, out var tv) ? tv.Codigo : string.Empty))],
            t.CreadoEn);

    public static TipoDocumentoResumenDto Resumen(this Documento d) => new(d.TipoCanonico ?? string.Empty, d.NombreTipo, d.TipoDocumentoTenantId);

    public static string Codigo(this Documento d) => d.TipoCanonico ?? d.NombreTipo;

    public static VersionDocumentoDto ADto(this VersionDocumento v, Guid documentoId) =>
        new(documentoId, v.Numero, v.Estado.ToString(), v.CargadaEn, v.CargadaPor, v.Clase.ToString(), v.NombreOriginal,
            v.Hechos?.MimeReal ?? v.MimeDeclarado, v.Tamano, v.Hechos?.Paginas, v.Sha256, v.MotivoDevolucion, v.DetalleDevolucion,
            v.Hechos is { } h
                ? new HechosDto(h.ConIa, h.HashVerificado, h.Antivirus.ToString(), h.MimeReal, h.Paginas, h.Comparacion.ToString(), [], [],
                    h.PendienteRevisionHumana)
                : null);

    public static CompartidoDto ADto(this VersionCompartida c, Documento d) => new(d.Id, d.Codigo(), c.Numero, c.VinculoId, c.EmpresaId);

    /// <summary>Estado del documento con cada vínculo de la sesión del tercero (empresa, versión compartida más reciente).</summary>
    public static IReadOnlyList<ComparticionPublicaDto> PorEmpresa(this Documento d, IReadOnlyList<(Guid VinculoId, Empresa? Empresa, Guid EmpresaId)> vinculos) =>
    [
        .. vinculos
            .Where(v => d.Ambito == AmbitoDocumento.Global || d.VinculoId == v.VinculoId)
            .Select(v =>
            {
                var ultima = d.Comparticiones.Where(c => c.VinculoId == v.VinculoId).MaxBy(c => c.Numero);
                return new ComparticionPublicaDto(v.VinculoId, v.Empresa.APublica(v.EmpresaId), ultima is not null, ultima?.Numero,
                    ultima is not null && ultima.Numero == d.VersionActual, ultima?.Fecha);
            })
    ];

    public static DocumentoDto ADto(this Documento d, IReadOnlyList<(Guid VinculoId, Empresa? Empresa, Guid EmpresaId)> vinculos) =>
        new(d.Id, d.TerceroId, d.Resumen(), d.Clase.ToString(), d.Ambito.ToString(), d.VinculoId, d.VersionActual,
            [.. d.Versiones.OrderBy(v => v.Numero).Select(v => v.ADto(d.Id))], d.PorEmpresa(vinculos));
}
