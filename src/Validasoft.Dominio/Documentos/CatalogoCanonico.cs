using Validasoft.Dominio.Identificaciones;

namespace Validasoft.Dominio.Documentos;

/// <summary>Archivo binario o datos estructurados con esquema (docs/03 §3.1).</summary>
public enum ClaseDocumento
{
    Archivo,
    Formulario
}

/// <summary><c>Global</c>: bóveda del tercero, compartido. <c>PorVinculo</c>: privado de una relación (docs/03 §3.1).</summary>
public enum AmbitoDocumento
{
    Global,
    PorVinculo
}

/// <summary>Naturaleza del dato (informativo para UI y retención, docs/02 §4.2).</summary>
public enum Sensibilidad
{
    Ninguna,
    Identidad,
    Financiera,
    Personal
}

public enum TipoReglaFrescura
{
    Ninguna,
    MaxDiasDesdeExpedicion,
    PeriodoMinimo,
    MaxDiasDesdeAprobacion
}

/// <summary>Exigencia de antigüedad de un tipo de documento (docs/02 §4.3). Se guarda; aún no se aplica (llega con H6).</summary>
public sealed record ReglaFrescura(TipoReglaFrescura Tipo, int? Dias = null, string? Regla = null)
{
    public static readonly ReglaFrescura Ninguna = new(TipoReglaFrescura.Ninguna);

    public static ReglaFrescura DiasDesdeExpedicion(int dias) => new(TipoReglaFrescura.MaxDiasDesdeExpedicion, dias);

    public static ReglaFrescura DiasDesdeAprobacion(int dias) => new(TipoReglaFrescura.MaxDiasDesdeAprobacion, dias);

    public static ReglaFrescura Periodo(string regla) => new(TipoReglaFrescura.PeriodoMinimo, null, regla);
}

/// <summary>
/// Tipo de documento canónico de la plataforma (docs/02 §4.2): define la naturaleza del documento y lo hace compartible
/// entre tenants ("RUT" en A y "Registro Único Tributario" en B son el mismo documento). Vive en código y no lo editan los
/// tenants; la semilla es el Anexo A de docs/03.
/// </summary>
public sealed record TipoDocumentoCanonico(
    string Codigo,
    string Nombre,
    ClaseDocumento Clase,
    AmbitoDocumento Ambito,
    Sensibilidad Sensibilidad,
    AplicaA AplicaA,
    bool TieneVencimientoIntrinseco,
    bool TieneFechaExpedicion,
    bool EsPeriodico,
    string? Periodicidad,
    bool PermiteRatificacion,
    bool DePersonaRelacionada,
    IReadOnlyList<string> CamposClave,
    IReadOnlyList<string> CamposCriticos,
    ReglaFrescura? ReglaFrescuraSugerida,
    int Version = 1,
    int RetencionAniosSugerida = 10)
{
    /// <summary>En esta fase solo se cargan archivos; los formularios responden 422 <c>clase-no-soportada</c>.</summary>
    public bool CargaSoportada => Clase == ClaseDocumento.Archivo;
}

/// <summary>Catálogo canónico (Anexo A de docs/03, fuente única de códigos). Los códigos retirados no existen aquí.</summary>
public static class CatalogoCanonico
{
    private static readonly IReadOnlyList<TipoDocumentoCanonico> Tipos =
    [
        Archivo("RUT", "Registro Único Tributario", AmbitoDocumento.Global, Sensibilidad.Identidad, AplicaA.Ambas,
            vencimiento: false, expedicion: true, ratificable: true, persona: false,
            ["nit", "razonSocial", "actividadCiiuPrincipal", "responsabilidades", "direccion", "representanteLegal"],
            ["nit", "razonSocial", "representanteLegal"], ReglaFrescura.DiasDesdeExpedicion(365)),
        Archivo("CAMARA_COMERCIO", "Certificado de existencia y representación legal", AmbitoDocumento.Global, Sensibilidad.Identidad,
            AplicaA.Juridica, vencimiento: false, expedicion: true, ratificable: false, persona: false,
            ["nit", "razonSocial", "matricula", "representanteLegal", "suplentes", "objeto", "capital", "estadoMatricula", "fechaExpedicion"],
            ["representanteLegal", "razonSocial", "estadoMatricula"], ReglaFrescura.DiasDesdeExpedicion(30)),
        Archivo("CEDULA", "Cédula de ciudadanía", AmbitoDocumento.Global, Sensibilidad.Identidad, AplicaA.Natural,
            vencimiento: false, expedicion: true, ratificable: true, persona: true,
            ["numero", "nombres", "fechaExpedicion"], ["numero", "nombres"], ReglaFrescura.Ninguna),
        Archivo("PASAPORTE", "Pasaporte", AmbitoDocumento.Global, Sensibilidad.Identidad, AplicaA.Natural,
            vencimiento: true, expedicion: true, ratificable: false, persona: true,
            ["numero", "nombres", "pais", "vencimiento"], ["numero"], null),
        Archivo("CERTIFICADO_ANTECEDENTES", "Certificado de antecedentes", AmbitoDocumento.Global, Sensibilidad.Personal, AplicaA.Natural,
            vencimiento: false, expedicion: true, ratificable: false, persona: true,
            ["numero", "nombre", "resultado", "fecha", "entidad"], ["resultado"], ReglaFrescura.DiasDesdeExpedicion(30)),
        Archivo("ESTADOS_FINANCIEROS", "Estados financieros", AmbitoDocumento.Global, Sensibilidad.Financiera, AplicaA.Ambas,
            vencimiento: false, expedicion: false, ratificable: false, persona: false,
            ["periodo", "activos", "pasivos", "patrimonio", "ingresos", "firmaContador"], ["periodo"],
            ReglaFrescura.Periodo("ultimo-anio-fiscal-cerrado"), periodicidad: "Anual"),
        Archivo("DECLARACION_RENTA", "Declaración de renta", AmbitoDocumento.Global, Sensibilidad.Financiera, AplicaA.Ambas,
            vencimiento: false, expedicion: false, ratificable: false, persona: false,
            ["anioGravable", "nit", "formulario"], ["anioGravable"], ReglaFrescura.Periodo("ultimo-anio-gravable"), periodicidad: "Anual"),
        Archivo("CERTIFICACION_BANCARIA", "Certificación bancaria", AmbitoDocumento.Global, Sensibilidad.Financiera, AplicaA.Ambas,
            vencimiento: false, expedicion: true, ratificable: false, persona: false,
            ["banco", "tipo", "numero", "titular", "identificacionTitular", "fecha"], ["numero", "titular"],
            ReglaFrescura.DiasDesdeExpedicion(90)),
        Formulario("CUENTA_BANCARIA", "Cuenta bancaria", AmbitoDocumento.Global, Sensibilidad.Financiera, ratificable: true, persona: false,
            ["banco", "tipo", "numero", "titular", "identificacionTitular", "moneda"], ["numero", "titular"], ReglaFrescura.Ninguna),
        Formulario("DATOS_MAESTROS", "Datos maestros", AmbitoDocumento.Global, Sensibilidad.Identidad, ratificable: true, persona: false,
            ["razonSocial", "nombreComercial", "direccion", "ciudad", "telefono", "correo", "representantes"],
            ["razonSocial", "representantes"], ReglaFrescura.Ninguna),
        Formulario("INFO_TRIBUTARIA", "Información tributaria", AmbitoDocumento.Global, Sensibilidad.Financiera, ratificable: true,
            persona: false, ["regimen", "responsabilidades", "retenciones", "granContribuyente"], ["regimen"],
            ReglaFrescura.DiasDesdeAprobacion(365)),
        Formulario("INFO_LEGAL", "Información legal", AmbitoDocumento.Global, Sensibilidad.Identidad, ratificable: true, persona: false,
            ["tipoSocietario", "representantes", "socios", "revisorFiscal"], ["representantes"], ReglaFrescura.DiasDesdeAprobacion(365)),
        Formulario("BENEFICIARIO_FINAL", "Beneficiario final", AmbitoDocumento.Global, Sensibilidad.Personal, ratificable: true, persona: true,
            ["identificacion", "porcentaje", "cadenaDeControl"], ["identificacion", "porcentaje"], ReglaFrescura.DiasDesdeAprobacion(365)),
        Formulario("DECLARACION_PEP", "Declaración PEP", AmbitoDocumento.Global, Sensibilidad.Personal, ratificable: true, persona: true,
            ["condicionPep", "cargo", "vinculados"], ["condicionPep"], ReglaFrescura.DiasDesdeAprobacion(365)),
        Archivo("SG_SST", "Evaluación SG-SST", AmbitoDocumento.Global, Sensibilidad.Identidad, AplicaA.Ambas,
            vencimiento: true, expedicion: true, ratificable: false, persona: false, ["empresa", "puntaje", "fecha"], ["puntaje"], null),
        Archivo("HOJA_DE_VIDA", "Hoja de vida", AmbitoDocumento.Global, Sensibilidad.Personal, AplicaA.Natural,
            vencimiento: false, expedicion: false, ratificable: true, persona: false, ["nombre", "identificacion"], [], ReglaFrescura.Ninguna),
        Archivo("TITULO_ACADEMICO", "Título académico", AmbitoDocumento.Global, Sensibilidad.Personal, AplicaA.Natural,
            vencimiento: false, expedicion: true, ratificable: true, persona: false, ["institucion", "titulo", "fecha"], ["titulo"],
            ReglaFrescura.Ninguna),
        Archivo("POLIZA", "Póliza", AmbitoDocumento.PorVinculo, Sensibilidad.Financiera, AplicaA.Ambas,
            vencimiento: true, expedicion: true, ratificable: false, persona: false,
            ["aseguradora", "numero", "tomador", "beneficiario", "amparos", "vigenciaDesde", "vigenciaHasta", "valor"],
            ["beneficiario", "vigencia", "amparos"], null),
        Formulario("AUTORIZACION_TRATAMIENTO_DATOS", "Autorización de tratamiento de datos", AmbitoDocumento.PorVinculo, Sensibilidad.Personal,
            ratificable: true, persona: false, ["firmante", "fecha", "versionTexto", "empresaDestinataria"], ["firmante"], ReglaFrescura.Ninguna),
        Formulario("TERMINOS_Y_CONDICIONES", "Términos y condiciones", AmbitoDocumento.PorVinculo, Sensibilidad.Ninguna, ratificable: true,
            persona: false, ["versionAceptada"], ["version"], ReglaFrescura.Ninguna),
        Formulario("FORMULARIO_SARLAFT", "Formulario SARLAFT", AmbitoDocumento.PorVinculo, Sensibilidad.Personal, ratificable: false,
            persona: false, ["origenDeFondos", "beneficiarioFinal", "declaracionPep"], ["beneficiarioFinal", "pep"],
            ReglaFrescura.DiasDesdeAprobacion(365)),
        Archivo("AFILIACION_SEGURIDAD_SOCIAL", "Afiliación a seguridad social", AmbitoDocumento.PorVinculo, Sensibilidad.Personal,
            AplicaA.Ambas, vencimiento: true, expedicion: false, ratificable: false, persona: false, ["periodo", "eps", "afp", "arl", "estado"],
            ["estado"], ReglaFrescura.Periodo("mes-en-curso"), periodicidad: "Mensual"),
        Archivo("PAZ_Y_SALVO", "Paz y salvo", AmbitoDocumento.PorVinculo, Sensibilidad.Ninguna, AplicaA.Ambas,
            vencimiento: false, expedicion: true, ratificable: false, persona: false, ["emisor", "fecha", "concepto"], [],
            ReglaFrescura.DiasDesdeExpedicion(30)),
        Archivo("FORMATO_VINCULACION", "Formato de vinculación", AmbitoDocumento.PorVinculo, Sensibilidad.Ninguna, AplicaA.Ambas,
            vencimiento: false, expedicion: false, ratificable: false, persona: false, ["versionPlantilla", "firma"], ["versionPlantilla"],
            ReglaFrescura.Ninguna)
    ];

    private static readonly Dictionary<string, TipoDocumentoCanonico> PorCodigo = Tipos.ToDictionary(t => t.Codigo, StringComparer.OrdinalIgnoreCase);

    /// <summary>Tipos canónicos de clase <c>Archivo</c> que se siembran en cada tenant nuevo (sin exigencias).</summary>
    public static readonly IReadOnlyList<string> SemillaDeTenant =
        ["RUT", "CAMARA_COMERCIO", "CEDULA", "ESTADOS_FINANCIEROS", "CERTIFICACION_BANCARIA", "POLIZA"];

    public static IReadOnlyList<TipoDocumentoCanonico> Todos => Tipos;

    public static TipoDocumentoCanonico? Buscar(string? codigo) =>
        codigo is not null && PorCodigo.TryGetValue(codigo.Trim(), out var tipo) ? tipo : null;

    public static TipoDocumentoCanonico Requerido(string? codigo) =>
        Buscar(codigo) ?? throw Comun.ReglaDeNegocioException.Validacion("tipo-documento-invalido",
            $"'{codigo}' no es un tipo de documento canónico.");

    private static TipoDocumentoCanonico Archivo(string codigo, string nombre, AmbitoDocumento ambito, Sensibilidad sensibilidad, AplicaA aplicaA,
        bool vencimiento, bool expedicion, bool ratificable, bool persona, string[] clave, string[] criticos, ReglaFrescura? frescura,
        string? periodicidad = null) =>
        new(codigo, nombre, ClaseDocumento.Archivo, ambito, sensibilidad, aplicaA, vencimiento, expedicion, periodicidad is not null,
            periodicidad, ratificable, persona, clave, criticos, frescura);

    private static TipoDocumentoCanonico Formulario(string codigo, string nombre, AmbitoDocumento ambito, Sensibilidad sensibilidad,
        bool ratificable, bool persona, string[] clave, string[] criticos, ReglaFrescura? frescura) =>
        new(codigo, nombre, ClaseDocumento.Formulario, ambito, sensibilidad, AplicaA.Ambas, false, false, false, null, ratificable, persona,
            clave, criticos, frescura);
}
