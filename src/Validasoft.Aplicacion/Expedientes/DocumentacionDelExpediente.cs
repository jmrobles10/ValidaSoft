using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Ficha;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Lo que la bóveda y la configuración dicen de los requisitos de un expediente (TD-1…TD-6): el tipo del tenant de cada
/// requisito (modo, campos, reglas de archivo, plantilla) y el documento de la versión que lo cubre (datos declarados y
/// vigencia para el vínculo del expediente). En los requisitos de sección (F2), la sección de la ficha del tercero
/// (<see cref="Secciones"/>, por requisito): datos y soportes de la versión que lo cubre y la comparación con la que la empresa
/// había aceptado. Solo lectura: enriquece <c>ExpedienteDto</c> y <c>ExpedientePublicoDto</c>.
/// </summary>
public sealed record DocumentacionDelExpediente(
    IReadOnlyDictionary<Guid, TipoDocumentoTenant> Tipos, IReadOnlyDictionary<Guid, Documento> Documentos, ReglasDeCarga Reglas, DateOnly Hoy,
    IReadOnlyDictionary<Guid, SeccionDeFicha>? Secciones = null)
{
    /// <summary>Cruces sección ↔ documento que ve la empresa (F5); solo en la vista de la empresa, nunca en la del tercero.</summary>
    public CrucesDelExpediente? Cruces { get; init; }

    /// <summary>Ayuda que la empresa del expediente escribió para cada tipo (OP-15); manda sobre la del grupo (<c>TextoAyuda</c> del tipo).</summary>
    public IReadOnlyDictionary<Guid, string>? Ayudas { get; init; }

    /// <summary><paramref name="tipos"/>: cualquier sesión (los tipos se buscan con <c>AnyTenant</c>); <paramref name="boveda"/>: el tenant técnico <c>boveda</c>.</summary>
    public static async Task<DocumentacionDelExpediente> CargarAsync(IQuerySession tipos, IQuerySession boveda, Expediente expediente,
        ReglasDeCarga reglas, DateOnly hoy, CancellationToken ct)
    {
        var documentales = expediente.Requisitos.Where(r => !r.EsSeccion).ToList();
        var tipoIds = documentales.Select(r => r.TipoDocumentoTenantId).Distinct().ToArray();
        var documentoIds = documentales.Select(r => r.VersionActual?.DocumentoId).OfType<Guid>().Distinct().ToArray();
        var cargados = tipoIds.Length == 0
            ? []
            : await tipos.Query<TipoDocumentoTenant>().Where(t => t.AnyTenant() && t.Id.IsOneOf(tipoIds)).ToListAsync(ct);
        var documentos = documentoIds.Length == 0 ? [] : await boveda.LoadManyAsync<Documento>(ct, documentoIds);

        var deSeccion = expediente.Requisitos.Where(r => r.EsSeccion && r.Seccion is not null)
            .Select(r => (r.Id, SeccionId: SeccionDeFicha.IdPara(expediente.TerceroId, r.Seccion!, r.Periodo)))
            .ToList();
        var secciones = deSeccion.Count == 0
            ? new Dictionary<Guid, SeccionDeFicha>()
            : (await boveda.LoadManyAsync<SeccionDeFicha>(ct, deSeccion.Select(s => s.SeccionId).Distinct().ToArray())).ToDictionary(s => s.Id);
        var porRequisito = deSeccion.Where(s => secciones.ContainsKey(s.SeccionId)).ToDictionary(s => s.Id, s => secciones[s.SeccionId]);
        return new DocumentacionDelExpediente(cargados.ToDictionary(t => t.Id), documentos.ToDictionary(d => d.Id), reglas, hoy, porRequisito)
        {
            Ayudas = await Validasoft.Aplicacion.Documentos.AyudaDeDocumentoDeEmpresa.DeLaEmpresaAsync(tipos, expediente.EmpresaId, tipoIds, ct)
        };
    }

    /// <summary>Tipo del requisito para el DTO, con lo que el tercero necesita para entregarlo.</summary>
    public TipoDocumentoRequisitoDto Tipo(RequisitoDeExpediente r) =>
        Tipos.TryGetValue(r.TipoDocumentoTenantId, out var tipo)
            ? new TipoDocumentoRequisitoDto(r.TipoDocumentoTenantId, r.Codigo, r.Nombre, r.TipoCanonico, r.Ambito.ToString(), tipo.Modo.ToString(),
                [.. tipo.Campos.Select(c => c.ADto())],
                tipo.PideArchivo ? tipo.ReglasEfectivas(Reglas.TamanoMaximoBytes, Reglas.MaximoPaginas).ADto() : null,
                tipo.Plantilla is not null, Ayudas?.GetValueOrDefault(r.TipoDocumentoTenantId) ?? tipo.TextoAyuda)
            : new TipoDocumentoRequisitoDto(r.TipoDocumentoTenantId, r.Codigo, r.Nombre, r.TipoCanonico, r.Ambito.ToString());

    /// <summary>
    /// Versión que cubre el requisito con sus datos declarados y su vigencia para el vínculo: la registrada en la bóveda o,
    /// si aún no se calculó (llega por mensaje), la que da la regla del tipo. En una sección: su estado en la ficha
    /// (<c>Actual</c> si sigue vigente, si no <c>Superada</c>) y la clase <c>Formulario</c>.
    /// </summary>
    public VersionRequisitoDto Version(VersionDeRequisito v, RequisitoDeExpediente r, Guid vinculoId)
    {
        if (r.EsSeccion)
        {
            var estado = Secciones?.GetValueOrDefault(r.Id) is { } seccion ? seccion.VersionVigente == v.Numero ? "Actual" : "Superada" : null;
            return new VersionRequisitoDto(v.DocumentoId, v.Numero, v.Sha256, v.CargadaEn, null, null, estado, "Formulario");
        }

        if (!Documentos.TryGetValue(v.DocumentoId, out var documento) || documento.Buscar(v.Numero) is not { } version)
            return new VersionRequisitoDto(v.DocumentoId, v.Numero, v.Sha256, v.CargadaEn);

        var vigencia = version.VigenciaPara(vinculoId)?.ADto(Hoy)
                       ?? (Tipos.TryGetValue(r.TipoDocumentoTenantId, out var tipo) ? tipo.Vigencia(version, Hoy) : null);
        return new VersionRequisitoDto(v.DocumentoId, v.Numero, v.Sha256, v.CargadaEn, version.Datos?.Select(d => d.ADto()).ToList(), vigencia,
            version.Estado.ToString(), version.Clase.ToString());
    }

    /// <summary>
    /// Detalle del requisito de sección (F2): la completitud para la empresa guardada en el expediente, los datos y soportes
    /// de la versión que lo cubre y, si la empresa había aceptado otra, la comparación campo por campo (solo lo que cambió).
    /// </summary>
    public SeccionDeRequisitoDto? Seccion(RequisitoDeExpediente r)
    {
        if (!r.EsSeccion)
            return null;
        if (Secciones?.GetValueOrDefault(r.Id) is not { } seccion)
            return SeccionSinDatos(r);

        var actual = r.VersionActual is { } v ? seccion.Versiones.FirstOrDefault(x => x.Numero == v.Numero) : null;
        var aceptada = r.VersionAceptadaPrevia is { } a ? seccion.Versiones.FirstOrDefault(x => x.Numero == a.Numero) : null;
        var cambios = actual is not null && aceptada is not null && actual.Numero != aceptada.Numero
            ? ComparacionDeSecciones.Comparar(aceptada.Datos, actual.Datos)
            : [];
        return SeccionSinDatos(r) with
        {
            Datos = actual?.Datos,
            Soportes = actual is null ? [] : [.. actual.Soportes.Select(s => s.ADto())],
            Cambios = [.. cambios.Select(c => new CambioDeCampoDto(c.Campo, c.Etiqueta, c.Anterior, c.Nuevo, c.TipoCambio.ToString()))],
            Cruce = Cruces?.DeSeccion(r.Id)
        };
    }

    /// <summary>Lo que se sabe del requisito de sección sin leer la ficha: la completitud para la empresa guardada en el expediente.</summary>
    public static SeccionDeRequisitoDto SeccionSinDatos(RequisitoDeExpediente r) =>
        new(r.Seccion ?? r.Codigo, r.Nombre, r.Periodo, r.ExigeSoporte,
            r.Completitud is { } c
                ? new CompletitudSeccionDto(c.Completa, [.. c.Faltantes.Select(f => new FaltanteSeccionDto(f.Campo, f.Regla, f.Mensaje))],
                    CatalogoDeSecciones.Buscar(r.Seccion)?.VersionReglas ?? 0)
                : null,
            r.Completitud?.Version, null, [], r.VersionAceptadaPrevia?.Numero, []);
}
