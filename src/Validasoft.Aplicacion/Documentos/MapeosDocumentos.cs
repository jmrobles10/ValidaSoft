using System.Text.Json;
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

    public static ModoCaptura? AModo(string? modo) =>
        string.IsNullOrWhiteSpace(modo)
            ? null
            : Enum.TryParse<ModoCaptura>(modo, true, out var valor) && Enum.IsDefined(valor)
                ? valor
                : throw ReglaDeNegocioException.Validacion("modo-invalido", "El modo debe ser Archivo, Formulario o ArchivoYFormulario.");

    public static CampoFormulario ADominio(this CampoFormularioDto c) =>
        Enum.TryParse<TipoCampo>(c.Tipo, true, out var tipo) && Enum.IsDefined(tipo)
            ? new CampoFormulario(c.Clave, c.Etiqueta, tipo, c.Obligatorio, c.Opciones, c.Ayuda)
            : throw ReglaDeNegocioException.Validacion("campo-invalido",
                $"El campo '{c.Clave}' tiene un tipo desconocido ({string.Join(", ", Enum.GetNames<TipoCampo>())}).");

    public static CampoFormularioDto ADto(this CampoFormulario c) => new(c.Clave, c.Etiqueta, c.Tipo.ToString(), c.Obligatorio, c.Opciones, c.Ayuda);

    /// <summary>
    /// Valores JSON del tercero a texto para validarlos en el dominio: cadenas tal cual, números con su texto invariante,
    /// <c>true</c>/<c>false</c>; <c>null</c> ⇒ vacío. Objetos o arreglos ⇒ 422 <c>valor-invalido</c>.
    /// </summary>
    public static IReadOnlyDictionary<string, string?> ATexto(IReadOnlyDictionary<string, JsonElement>? datos) =>
        (datos ?? new Dictionary<string, JsonElement>()).ToDictionary(d => d.Key.Trim(), d => d.Value.ValueKind switch
        {
            JsonValueKind.String => d.Value.GetString(),
            JsonValueKind.Number => d.Value.GetRawText(),
            JsonValueKind.True => "true",
            JsonValueKind.False => "false",
            JsonValueKind.Null or JsonValueKind.Undefined => null,
            _ => throw new ReglaDeNegocioException(TipoFalla.Validacion, "valor-invalido", $"El campo '{d.Key}' debe ser un valor simple (texto, número, fecha o sí/no).")
            {
                Datos = new Dictionary<string, object?> { ["campo"] = d.Key }
            }
        }, StringComparer.Ordinal);

    public static DatoDeclaradoDto ADto(this DatoDeclarado d) => new(d.Clave, d.Etiqueta, d.Tipo.ToString(), d.Valor);

    public static ReglasArchivoDto ADto(this ReglasDeArchivo r) => new(r.Formatos, r.TamanoMaximoBytes, r.PaginasMaximas, r.Descripcion);

    public static TipoDocumentoCanonicoDto ADto(this TipoDocumentoCanonico t) =>
        new(t.Codigo, t.Version, t.Nombre, t.Ambito.ToString(), t.Clase.ToString(), t.AplicaA.ToString(), t.TieneVencimientoIntrinseco,
            t.TieneFechaExpedicion, t.EsPeriodico, t.Periodicidad, t.PermiteRatificacion, t.DePersonaRelacionada, t.CamposClave,
            t.CamposCriticos, t.Sensibilidad.ToString(), t.ReglaFrescuraSugerida?.ADto(), t.RetencionAniosSugerida, t.CargaSoportada,
            t.Formatos, t.TamanoMaximoMb, t.PaginasMaximas);

    /// <summary>Con <paramref name="reglas"/> incluye las reglas de archivo efectivas (con los límites globales).</summary>
    public static TipoDocumentoTenantDto ADto(this TipoDocumentoTenant t, ReglasDeCarga? reglas = null) =>
        new(t.Id, t.Codigo, t.Nombre, t.TipoCanonico, t.VersionCanonico, t.Ambito.ToString(), t.Clase.ToString(), t.TextoAyuda,
            t.ReglaFrescura.ADto(), t.DiasAviso, t.DiasGracia, t.DiasRevalidacion, t.Activo, t.CreadoEn, t.FormatosPermitidos, t.TamanoMaximoMb,
            t.PaginasMaximas, t.Modo.ToString(), [.. t.Campos.Select(ADto)],
            reglas is null ? null : t.ReglasEfectivas(reglas.TamanoMaximoBytes, reglas.MaximoPaginas).ADto(),
            t.Plantilla?.ADto(t.PlantillaVigente), t.CargaSoportada);

    public static PlantillaVersionDto ADto(this PlantillaDeTipo p, int? vigente) =>
        new(p.Version, p.NombreOriginal, p.Mime, p.Tamano, p.Sha256, p.CargadaEn, p.CargadaPor, p.Version == vigente, p.RetiradaEn);

    public static PlantillaTipoDocumentoDto PlantillaDto(this TipoDocumentoTenant t) =>
        new(t.Id, t.Plantilla?.ADto(t.PlantillaVigente), [.. t.Plantillas.OrderByDescending(p => p.Version).Select(p => p.ADto(t.PlantillaVigente))]);

    public static VigenciaDto ADto(this VigenciaDeVersion v, DateOnly hoy) =>
        new(v.VigenteHasta, v.Base.ToString(),
            CalculadoraDeVigencia.Estado(v.VigenteHasta, v.DiasAviso, v.DiasGracia, hoy, v.VencidaEn is not null).ToString(),
            v.DiasAviso, v.DiasGracia, v.FinDeGracia, v.VencidaEn);

    /// <summary>Vigencia calculada al vuelo con la regla del tipo (si la bóveda aún no la registró: se calcula por mensaje).</summary>
    public static VigenciaDto Vigencia(this TipoDocumentoTenant tipo, VersionDocumento version, DateOnly hoy)
    {
        var calculo = CalculadoraDeVigencia.Calcular(tipo.ReglaFrescura, version.CargadaEn, version.Datos);
        return new VigenciaDto(calculo.VigenteHasta, calculo.Base.ToString(),
            CalculadoraDeVigencia.Estado(calculo.VigenteHasta, tipo.DiasAviso, tipo.DiasGracia, hoy).ToString(), tipo.DiasAviso, tipo.DiasGracia,
            CalculadoraDeVigencia.FinDeGracia(calculo.VigenteHasta, tipo.DiasGracia), null);
    }

    public static DateOnly Hoy(this TimeProvider reloj) => CalculadoraDeVigencia.Fecha(reloj.GetUtcNow());

    public static TipoDocumentoResumenDto Resumen(this Documento d) => new(d.TipoCanonico ?? string.Empty, d.NombreTipo, d.TipoDocumentoTenantId);

    public static string Codigo(this Documento d) => d.TipoCanonico ?? d.NombreTipo;

    /// <summary>
    /// <paramref name="vinculoId"/>: la empresa que consulta ve la vigencia de su vínculo; sin él (el tercero) la más próxima
    /// a vencer. <paramref name="hoy"/> nulo ⇒ la fecha UTC del sistema.
    /// </summary>
    public static VersionDocumentoDto ADto(this VersionDocumento v, Guid documentoId, Guid? vinculoId = null, DateOnly? hoy = null) =>
        new(documentoId, v.Numero, v.Estado.ToString(), v.CargadaEn, v.CargadaPor, v.Clase.ToString(), v.NombreOriginal,
            v.Hechos?.MimeReal ?? v.MimeDeclarado, v.Tamano, v.Hechos?.Paginas, v.Sha256, v.MotivoDevolucion, v.DetalleDevolucion,
            v.Hechos is { } h
                ? new HechosDto(h.ConIa, h.HashVerificado, h.Antivirus.ToString(), h.MimeReal, h.Paginas, h.Comparacion.ToString(), [], [],
                    h.PendienteRevisionHumana)
                : null,
            v.Datos is null ? null : [.. v.Datos.Select(ADto)],
            (vinculoId is { } id ? v.VigenciaPara(id) : v.ProximaVigencia)?.ADto(hoy ?? CalculadoraDeVigencia.Fecha(TimeProvider.System.GetUtcNow())));

    public static CompartidoDto ADto(this VersionCompartida c, Documento d) => new(d.Id, d.Codigo(), c.Numero, c.VinculoId, c.EmpresaId);

    /// <summary>Estado del documento con cada vínculo de la sesión del tercero (empresa, versión compartida más reciente y su vigencia).</summary>
    public static IReadOnlyList<ComparticionPublicaDto> PorEmpresa(this Documento d, IReadOnlyList<(Guid VinculoId, Empresa? Empresa, Guid EmpresaId)> vinculos,
        DateOnly? hoy = null) =>
    [
        .. vinculos
            .Where(v => d.Ambito == AmbitoDocumento.Global || d.VinculoId == v.VinculoId)
            .Select(v =>
            {
                var ultima = d.Comparticiones.Where(c => c.VinculoId == v.VinculoId).MaxBy(c => c.Numero);
                var vigencia = ultima is null ? null : d.Buscar(ultima.Numero)?.VigenciaPara(v.VinculoId);
                return new ComparticionPublicaDto(v.VinculoId, v.Empresa.APublica(v.EmpresaId), ultima is not null, ultima?.Numero,
                    ultima is not null && ultima.Numero == d.VersionActual, ultima?.Fecha,
                    vigencia?.ADto(hoy ?? CalculadoraDeVigencia.Fecha(TimeProvider.System.GetUtcNow())));
            })
    ];

    public static DocumentoDto ADto(this Documento d, IReadOnlyList<(Guid VinculoId, Empresa? Empresa, Guid EmpresaId)> vinculos, DateOnly? hoy = null) =>
        new(d.Id, d.TerceroId, d.Resumen(), d.Clase.ToString(), d.Ambito.ToString(), d.VinculoId, d.VersionActual,
            [.. d.Versiones.OrderBy(v => v.Numero).Select(v => v.ADto(d.Id, null, hoy))], d.PorEmpresa(vinculos, hoy));
}
