using System.Text;
using System.Text.Json;
using System.Text.Json.Nodes;
using Validasoft.Dominio.Documentos;

namespace Validasoft.Aplicacion.Ia;

/// <summary>Forma de un campo extraído: todos admiten <c>null</c> (el dato no aparece en el documento).</summary>
public enum TipoDeCampoIa
{
    Texto,
    Fecha,
    Numero,
    Booleano,

    /// <summary>Lista de personas con nombre, identificación y cargo (representantes legales).</summary>
    Personas,

    /// <summary>Lista de pares clave/valor (tipos sin esquema propio).</summary>
    Datos,

    /// <summary>Lista de textos (códigos de responsabilidades, actividades secundarias); vacía si no aparece ninguno.</summary>
    Lista
}

public sealed record CampoDeEsquema(string Nombre, TipoDeCampoIa Tipo, string Descripcion);

/// <summary>
/// Esquema de salida estructurada de un tipo canónico (IA-1): campos comunes (<c>tipoDetectado</c>, <c>legible</c>,
/// <c>confianza</c>, <c>alertasDeManipulacion</c>, <c>observaciones</c>) y <c>campos</c> propios del tipo. Estricto: todo
/// es obligatorio (lo opcional admite <c>null</c>) y no hay propiedades adicionales. Los campos clave se comparan entre
/// versiones y los críticos piden escalamiento si cambian.
/// </summary>
public sealed record EsquemaDeAnalisis(
    string TipoCanonico,
    string NombreTipo,
    IReadOnlyList<CampoDeEsquema> Campos,
    IReadOnlyList<string> CamposClave,
    IReadOnlyList<string> CamposCriticos)
{
    public const string Otro = "OTRO";

    public static readonly IReadOnlyList<string> Comunes = ["tipoDetectado", "legible", "confianza", "alertasDeManipulacion", "observaciones", "campos"];

    /// <summary>Tipos que el modelo puede reconocer: los canónicos de archivo y <c>OTRO</c>.</summary>
    public static readonly IReadOnlyList<string> TiposDetectables =
        [.. CatalogoCanonico.Todos.Where(t => t.Clase == ClaseDocumento.Archivo).Select(t => t.Codigo), Otro];

    /// <summary>Nombre del formato en la API (<c>hechos_rut</c>).</summary>
    public string NombreFormato => "hechos_" + TipoCanonico.ToLowerInvariant();

    public bool Generico => Campos is [{ Tipo: TipoDeCampoIa.Datos }];

    /// <summary>El JSON Schema estricto de la respuesta.</summary>
    public JsonObject Json() => new()
    {
        ["type"] = "object",
        ["additionalProperties"] = false,
        ["required"] = Lista(Comunes),
        ["properties"] = new JsonObject
        {
            ["tipoDetectado"] = new JsonObject
            {
                ["type"] = "string",
                ["enum"] = Lista(TiposDetectables),
                ["description"] = "Código del tipo de documento que realmente es el archivo; OTRO si no es ninguno de la lista."
            },
            ["legible"] = new JsonObject { ["type"] = "boolean", ["description"] = "Falso si está borroso, cortado, incompleto o no se lee con seguridad." },
            ["confianza"] = new JsonObject { ["type"] = "number", ["description"] = "De 0 a 1: qué tan seguro estás de que los campos extraídos son correctos." },
            ["alertasDeManipulacion"] = new JsonObject
            {
                ["type"] = "array",
                ["items"] = new JsonObject { ["type"] = "string" },
                ["description"] = "Señales de alteración del documento; vacía si no hay."
            },
            ["observaciones"] = new JsonObject { ["type"] = Lista(["string", "null"]), ["description"] = "Notas breves para el revisor, o null." },
            ["campos"] = Objeto(Campos.ToDictionary(c => c.Nombre, Propiedad))
        }
    };

    /// <summary>Instrucciones en español: extraer solo lo que dice el documento, normalizar y evaluar legibilidad y manipulación.</summary>
    public string Instrucciones()
    {
        var texto = new StringBuilder()
            .AppendLine("Eres analista de documentos de terceros (proveedores, clientes, contratistas) en Colombia.")
            .AppendLine($"Analiza el archivo adjunto, que el tercero entregó como: {NombreTipo} ({TipoCanonico}).")
            .AppendLine("Reglas:")
            .AppendLine("1. Extrae solo lo que el documento dice. Si un dato no aparece o no se lee con certeza, usa null. No inventes, no deduzcas y no completes datos.")
            .AppendLine("2. Fechas en formato AAAA-MM-DD.")
            .AppendLine("3. Identificaciones y números de cuenta o matrícula: solo dígitos y letras, sin puntos, espacios ni guiones. El dígito de verificación del NIT va aparte cuando hay un campo para él.")
            .AppendLine("4. Valores monetarios como número, sin símbolos ni separadores de miles.")
            .AppendLine("5. Nombres y razones sociales tal como aparecen, sin abreviar ni corregir.")
            .AppendLine("6. tipoDetectado: el código del tipo de documento que realmente es el archivo; OTRO si no corresponde a ninguno.")
            .AppendLine("7. legible: false si el documento está borroso, cortado, incompleto o no se puede leer con seguridad.")
            .AppendLine("8. confianza: de 0 a 1, qué tan seguro estás de que los campos extraídos son correctos.")
            .AppendLine("9. alertasDeManipulacion: señales de alteración (tipografías o alineaciones inconsistentes, sellos o firmas superpuestos, fechas incoherentes, datos que se contradicen); lista vacía si no hay.")
            .AppendLine("10. observaciones: notas breves útiles para el revisor humano, o null.")
            .AppendLine("Campos a extraer (en \"campos\"):");
        foreach (var campo in Campos)
            texto.AppendLine($"- {campo.Nombre}: {campo.Descripcion}");
        return texto.ToString();
    }

    /// <summary>
    /// Interpreta el texto JSON que devolvió el modelo y verifica que cumpla el esquema: JSON inválido ⇒
    /// <c>json-invalido</c>; propiedades faltantes, sobrantes o de otro tipo, confianza fuera de [0, 1] o un tipo detectado
    /// fuera de la lista ⇒ <c>fuera-de-esquema</c>.
    /// </summary>
    public HechosExtraidos Interpretar(string texto)
    {
        JsonElement raiz;
        try
        {
            using var documento = JsonDocument.Parse(texto);
            raiz = documento.RootElement.Clone();
        }
        catch (JsonException e)
        {
            throw new AnalisisDeIaException("json-invalido", "La respuesta del modelo no es JSON válido.", e);
        }

        Objeto(raiz, Comunes, "la respuesta");
        var tipo = raiz.GetProperty("tipoDetectado");
        if (tipo.ValueKind != JsonValueKind.String || !TiposDetectables.Contains(tipo.GetString()))
            throw FueraDeEsquema("tipoDetectado no es un tipo conocido.");
        var legible = raiz.GetProperty("legible");
        if (legible.ValueKind is not (JsonValueKind.True or JsonValueKind.False))
            throw FueraDeEsquema("legible debe ser booleano.");
        var confianza = raiz.GetProperty("confianza");
        if (confianza.ValueKind != JsonValueKind.Number || !confianza.TryGetDecimal(out var valorConfianza) || valorConfianza is < 0m or > 1m)
            throw FueraDeEsquema("confianza debe ser un número entre 0 y 1.");
        var alertas = raiz.GetProperty("alertasDeManipulacion");
        if (alertas.ValueKind != JsonValueKind.Array || alertas.EnumerateArray().Any(a => a.ValueKind != JsonValueKind.String))
            throw FueraDeEsquema("alertasDeManipulacion debe ser una lista de textos.");
        var observaciones = raiz.GetProperty("observaciones");
        if (observaciones.ValueKind is not (JsonValueKind.String or JsonValueKind.Null))
            throw FueraDeEsquema("observaciones debe ser texto o null.");

        var campos = raiz.GetProperty("campos");
        Objeto(campos, [.. Campos.Select(c => c.Nombre)], "campos");
        var extraidos = new Dictionary<string, JsonElement>(StringComparer.Ordinal);
        foreach (var campo in Campos)
        {
            var valor = campos.GetProperty(campo.Nombre);
            if (!Cumple(valor, campo.Tipo))
                throw FueraDeEsquema($"El campo {campo.Nombre} no tiene la forma esperada.");
            if (campo.Tipo == TipoDeCampoIa.Datos)
            {
                foreach (var par in valor.EnumerateArray())
                {
                    var clave = par.GetProperty("clave").GetString()!.Trim();
                    if (clave.Length > 0)
                        extraidos[clave] = par.GetProperty("valor").Clone();
                }
            }
            else
            {
                extraidos[campo.Nombre] = valor.Clone();
            }
        }

        return new HechosExtraidos(tipo.GetString()!, legible.GetBoolean(), valorConfianza,
            [.. alertas.EnumerateArray().Select(a => a.GetString()!).Where(a => !string.IsNullOrWhiteSpace(a))],
            observaciones.ValueKind == JsonValueKind.String ? observaciones.GetString() : null, extraidos);
    }

    private static bool Cumple(JsonElement valor, TipoDeCampoIa tipo) => tipo switch
    {
        TipoDeCampoIa.Texto or TipoDeCampoIa.Fecha => valor.ValueKind is JsonValueKind.String or JsonValueKind.Null,
        TipoDeCampoIa.Numero => valor.ValueKind is JsonValueKind.Number or JsonValueKind.Null,
        TipoDeCampoIa.Booleano => valor.ValueKind is JsonValueKind.True or JsonValueKind.False or JsonValueKind.Null,
        TipoDeCampoIa.Personas => valor.ValueKind == JsonValueKind.Array && valor.EnumerateArray().All(p =>
            EsObjetoCon(p, ["nombre", "identificacion", "cargo"]) &&
            p.EnumerateObject().All(x => x.Value.ValueKind is JsonValueKind.String or JsonValueKind.Null)),
        TipoDeCampoIa.Lista => valor.ValueKind == JsonValueKind.Array && valor.EnumerateArray().All(p => p.ValueKind == JsonValueKind.String),
        TipoDeCampoIa.Datos => valor.ValueKind == JsonValueKind.Array && valor.EnumerateArray().All(p =>
            EsObjetoCon(p, ["clave", "valor"]) && p.GetProperty("clave").ValueKind == JsonValueKind.String &&
            p.GetProperty("valor").ValueKind is JsonValueKind.String or JsonValueKind.Null),
        _ => false
    };

    private static bool EsObjetoCon(JsonElement valor, IReadOnlyList<string> propiedades) =>
        valor.ValueKind == JsonValueKind.Object &&
        valor.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal).SequenceEqual(propiedades.Order(StringComparer.Ordinal));

    private static void Objeto(JsonElement valor, IReadOnlyList<string> propiedades, string que)
    {
        if (!EsObjetoCon(valor, propiedades))
            throw FueraDeEsquema($"{que} no tiene exactamente las propiedades del esquema.");
    }

    private static AnalisisDeIaException FueraDeEsquema(string detalle) => new("fuera-de-esquema", "La respuesta del modelo no cumple el esquema: " + detalle);

    private static JsonObject Propiedad(CampoDeEsquema campo) => campo.Tipo switch
    {
        TipoDeCampoIa.Numero => Tipado(["number", "null"], campo.Descripcion),
        TipoDeCampoIa.Booleano => Tipado(["boolean", "null"], campo.Descripcion),
        TipoDeCampoIa.Fecha => Tipado(["string", "null"], campo.Descripcion + " Formato AAAA-MM-DD."),
        TipoDeCampoIa.Personas => new JsonObject
        {
            ["type"] = "array",
            ["description"] = campo.Descripcion,
            ["items"] = Objeto(new Dictionary<string, JsonObject>
            {
                ["nombre"] = Tipado(["string", "null"], "Nombre completo."),
                ["identificacion"] = Tipado(["string", "null"], "Número de identificación, solo dígitos."),
                ["cargo"] = Tipado(["string", "null"], "Cargo (principal, suplente, gerente…).")
            })
        },
        TipoDeCampoIa.Lista => new JsonObject
        {
            ["type"] = "array",
            ["description"] = campo.Descripcion,
            ["items"] = new JsonObject { ["type"] = "string" }
        },
        TipoDeCampoIa.Datos => new JsonObject
        {
            ["type"] = "array",
            ["description"] = campo.Descripcion,
            ["items"] = Objeto(new Dictionary<string, JsonObject>
            {
                ["clave"] = Tipado(["string"], "Nombre del dato en camelCase (p. ej. numero, titular, fechaExpedicion)."),
                ["valor"] = Tipado(["string", "null"], "Valor tal como aparece, normalizado.")
            })
        },
        _ => Tipado(["string", "null"], campo.Descripcion)
    };

    private static JsonObject Tipado(IReadOnlyList<string> tipos, string descripcion) =>
        new() { ["type"] = tipos.Count == 1 ? tipos[0] : Lista(tipos), ["description"] = descripcion };

    private static JsonObject Objeto(IReadOnlyDictionary<string, JsonObject> propiedades)
    {
        var lista = new JsonObject();
        foreach (var (nombre, propiedad) in propiedades)
            lista[nombre] = propiedad;
        return new JsonObject
        {
            ["type"] = "object",
            ["additionalProperties"] = false,
            ["required"] = Lista([.. propiedades.Keys]),
            ["properties"] = lista
        };
    }

    private static JsonArray Lista(IEnumerable<string> valores) => new([.. valores.Select(v => (JsonNode?)JsonValue.Create(v))]);
}

/// <summary>Catálogo de esquemas por tipo canónico (IA-1). Los canónicos de archivo sin esquema propio usan el genérico de pares clave/valor.</summary>
public static class EsquemasDeAnalisis
{
    private static readonly IReadOnlyDictionary<string, EsquemaDeAnalisis> Propios = new Dictionary<string, EsquemaDeAnalisis>(StringComparer.OrdinalIgnoreCase)
    {
        ["RUT"] = new("RUT", "Registro Único Tributario",
        [
            Texto("nit", "NIT o número de identificación tributaria, sin dígito de verificación."),
            Texto("dv", "Dígito de verificación del NIT."),
            Texto("razonSocial", "Razón social o, si es persona natural, nombres y apellidos."),
            Texto("tipoContribuyente", "Tipo de contribuyente (persona jurídica o persona natural)."),
            Texto("representanteLegalNombre", "Nombre del representante legal principal."),
            Texto("representanteLegalIdentificacion", "Identificación del representante legal principal."),
            Texto("direccion", "Dirección principal."),
            Texto("municipio", "Municipio o ciudad de la dirección principal."),
            Texto("codigoMunicipio", "Código DANE de 5 dígitos del municipio de la dirección principal (2 del departamento y 3 del municipio)."),
            Texto("correo", "Correo electrónico de la sección de ubicación."),
            Texto("telefono", "Teléfono 1 de la sección de ubicación, solo dígitos."),
            Fecha("fechaExpedicion", "Fecha de generación o expedición del documento."),
            Texto("actividadEconomicaPrincipal", "Código CIIU de la actividad económica principal."),
            Lista("actividadesSecundarias", "Códigos CIIU de las actividades secundarias y otras actividades, de 4 dígitos."),
            Lista("responsabilidades", "Códigos de dos dígitos de las responsabilidades, calidades y atributos (casilla 53), por ejemplo 05, 07, 48.")
        ], ["nit", "dv", "razonSocial", "tipoContribuyente", "representanteLegalNombre", "representanteLegalIdentificacion", "direccion", "municipio",
            "codigoMunicipio", "actividadEconomicaPrincipal", "responsabilidades"], ["nit", "razonSocial", "representanteLegalNombre", "representanteLegalIdentificacion"]),
        ["CAMARA_COMERCIO"] = new("CAMARA_COMERCIO", "Certificado de existencia y representación legal",
        [
            Texto("razonSocial", "Razón social de la sociedad."),
            Texto("nit", "NIT de la sociedad."),
            Texto("matricula", "Número de matrícula mercantil."),
            Texto("estadoMatricula", "Estado de la matrícula (activa, cancelada, en liquidación…)."),
            Texto("domicilio", "Domicilio principal."),
            Texto("correo", "Correo electrónico comercial o de notificación judicial de la sociedad que figura en el certificado (el de la sociedad, no el de una persona)."),
            Fecha("fechaExpedicion", "Fecha de expedición del certificado."),
            new("representantesLegales", TipoDeCampoIa.Personas, "Representantes legales principales y suplentes que figuran en el certificado.")
        ], ["razonSocial", "nit", "matricula", "estadoMatricula", "domicilio", "representantesLegales"],
            ["razonSocial", "estadoMatricula", "representantesLegales"]),
        ["CEDULA"] = new("CEDULA", "Cédula de ciudadanía",
        [
            Texto("numero", "Número de la cédula."),
            Texto("nombres", "Nombres."),
            Texto("apellidos", "Apellidos."),
            Fecha("fechaNacimiento", "Fecha de nacimiento."),
            Fecha("fechaExpedicion", "Fecha de expedición.")
        ], ["numero", "nombres", "apellidos", "fechaNacimiento"], ["numero", "nombres", "apellidos"]),
        ["CERTIFICACION_BANCARIA"] = new("CERTIFICACION_BANCARIA", "Certificación bancaria",
        [
            Texto("banco", "Entidad financiera que certifica."),
            Texto("tipoCuenta", "Tipo de cuenta (ahorros, corriente…)."),
            Texto("numeroCuenta", "Número de la cuenta."),
            Texto("titular", "Titular de la cuenta."),
            Texto("identificacionTitular", "Identificación del titular."),
            Texto("estadoCuenta", "Estado de la cuenta (activa, inactiva…)."),
            Fecha("fechaExpedicion", "Fecha de expedición de la certificación.")
        ], ["banco", "tipoCuenta", "numeroCuenta", "titular", "identificacionTitular", "estadoCuenta"],
            ["banco", "numeroCuenta", "titular", "identificacionTitular"]),
        ["ESTADOS_FINANCIEROS"] = new("ESTADOS_FINANCIEROS", "Estados financieros",
        [
            Texto("razonSocial", "Razón social de la entidad que reporta."),
            Texto("nit", "NIT de la entidad."),
            Texto("periodo", "Periodo que cubren (por ejemplo 2025)."),
            Fecha("fechaCorte", "Fecha de corte."),
            Numero("activoTotal", "Activo total."),
            Numero("pasivoTotal", "Pasivo total."),
            Numero("patrimonio", "Patrimonio total."),
            Numero("ingresos", "Ingresos operacionales del periodo."),
            Numero("ingresosBrutos", "Ingresos brutos (totales) del periodo."),
            Numero("utilidadNeta", "Utilidad (o pérdida, negativa) neta del periodo."),
            new("firmadoPorContador", TipoDeCampoIa.Booleano, "Si los firma un contador público (con tarjeta profesional); null si no se puede saber.")
        ], ["razonSocial", "nit", "periodo", "activoTotal", "pasivoTotal", "patrimonio", "ingresos", "utilidadNeta"], ["periodo"]),
        ["POLIZA"] = new("POLIZA", "Póliza",
        [
            Texto("aseguradora", "Compañía aseguradora."),
            Texto("numeroPoliza", "Número de la póliza."),
            Texto("tomador", "Tomador."),
            Texto("asegurado", "Asegurado."),
            Texto("beneficiario", "Beneficiario."),
            Fecha("vigenciaDesde", "Inicio de la vigencia."),
            Fecha("vigenciaHasta", "Fin de la vigencia."),
            Numero("valorAsegurado", "Valor asegurado.")
        ], ["aseguradora", "numeroPoliza", "tomador", "asegurado", "beneficiario", "vigenciaDesde", "vigenciaHasta", "valorAsegurado"],
            ["numeroPoliza", "vigenciaHasta", "valorAsegurado"])
    };

    /// <summary>Esquema del tipo canónico de archivo; nulo si no es un canónico de archivo del catálogo.</summary>
    public static EsquemaDeAnalisis? Para(string? tipoCanonico)
    {
        if (CatalogoCanonico.Buscar(tipoCanonico) is not { Clase: ClaseDocumento.Archivo } canonico)
            return null;
        return Propios.TryGetValue(canonico.Codigo, out var propio)
            ? propio
            : new EsquemaDeAnalisis(canonico.Codigo, canonico.Nombre,
                [new("datos", TipoDeCampoIa.Datos, "Datos principales del documento: " + string.Join(", ", canonico.CamposClave) + ".")],
                [.. canonico.CamposClave], []);
    }

    public static IReadOnlyList<string> ConEsquemaPropio => [.. Propios.Keys];

    private static CampoDeEsquema Texto(string nombre, string descripcion) => new(nombre, TipoDeCampoIa.Texto, descripcion);

    private static CampoDeEsquema Fecha(string nombre, string descripcion) => new(nombre, TipoDeCampoIa.Fecha, descripcion);

    private static CampoDeEsquema Numero(string nombre, string descripcion) => new(nombre, TipoDeCampoIa.Numero, descripcion);

    private static CampoDeEsquema Lista(string nombre, string descripcion) => new(nombre, TipoDeCampoIa.Lista, descripcion);
}
