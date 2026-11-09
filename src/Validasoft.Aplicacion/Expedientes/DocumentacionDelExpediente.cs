using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Lo que la bóveda y la configuración dicen de los requisitos de un expediente (TD-1…TD-6): el tipo del tenant de cada
/// requisito (modo, campos, reglas de archivo, plantilla) y el documento de la versión que lo cubre (datos declarados y
/// vigencia para el vínculo del expediente). Solo lectura: enriquece <c>ExpedienteDto</c> y <c>ExpedientePublicoDto</c>.
/// </summary>
public sealed record DocumentacionDelExpediente(
    IReadOnlyDictionary<Guid, TipoDocumentoTenant> Tipos, IReadOnlyDictionary<Guid, Documento> Documentos, ReglasDeCarga Reglas, DateOnly Hoy)
{
    /// <summary><paramref name="tipos"/>: cualquier sesión (los tipos se buscan con <c>AnyTenant</c>); <paramref name="boveda"/>: el tenant técnico <c>boveda</c>.</summary>
    public static async Task<DocumentacionDelExpediente> CargarAsync(IQuerySession tipos, IQuerySession boveda, Expediente expediente,
        ReglasDeCarga reglas, DateOnly hoy, CancellationToken ct)
    {
        var tipoIds = expediente.Requisitos.Select(r => r.TipoDocumentoTenantId).Distinct().ToArray();
        var documentoIds = expediente.Requisitos.Select(r => r.VersionActual?.DocumentoId).OfType<Guid>().Distinct().ToArray();
        var cargados = tipoIds.Length == 0
            ? []
            : await tipos.Query<TipoDocumentoTenant>().Where(t => t.AnyTenant() && t.Id.IsOneOf(tipoIds)).ToListAsync(ct);
        var documentos = documentoIds.Length == 0 ? [] : await boveda.LoadManyAsync<Documento>(ct, documentoIds);
        return new DocumentacionDelExpediente(cargados.ToDictionary(t => t.Id), documentos.ToDictionary(d => d.Id), reglas, hoy);
    }

    /// <summary>Tipo del requisito para el DTO, con lo que el tercero necesita para entregarlo.</summary>
    public TipoDocumentoRequisitoDto Tipo(RequisitoDeExpediente r) =>
        Tipos.TryGetValue(r.TipoDocumentoTenantId, out var tipo)
            ? new TipoDocumentoRequisitoDto(r.TipoDocumentoTenantId, r.Codigo, r.Nombre, r.TipoCanonico, r.Ambito.ToString(), tipo.Modo.ToString(),
                [.. tipo.Campos.Select(c => c.ADto())],
                tipo.PideArchivo ? tipo.ReglasEfectivas(Reglas.TamanoMaximoBytes, Reglas.MaximoPaginas).ADto() : null,
                tipo.Plantilla is not null, tipo.TextoAyuda)
            : new TipoDocumentoRequisitoDto(r.TipoDocumentoTenantId, r.Codigo, r.Nombre, r.TipoCanonico, r.Ambito.ToString());

    /// <summary>
    /// Versión que cubre el requisito con sus datos declarados y su vigencia para el vínculo: la registrada en la bóveda o,
    /// si aún no se calculó (llega por mensaje), la que da la regla del tipo.
    /// </summary>
    public VersionRequisitoDto Version(VersionDeRequisito v, RequisitoDeExpediente r, Guid vinculoId)
    {
        if (!Documentos.TryGetValue(v.DocumentoId, out var documento) || documento.Buscar(v.Numero) is not { } version)
            return new VersionRequisitoDto(v.DocumentoId, v.Numero, v.Sha256, v.CargadaEn);

        var vigencia = version.VigenciaPara(vinculoId)?.ADto(Hoy)
                       ?? (Tipos.TryGetValue(r.TipoDocumentoTenantId, out var tipo) ? tipo.Vigencia(version, Hoy) : null);
        return new VersionRequisitoDto(v.DocumentoId, v.Numero, v.Sha256, v.CargadaEn, version.Datos?.Select(d => d.ADto()).ToList(), vigencia,
            version.Estado.ToString(), version.Clase.ToString());
    }
}
