using System.Globalization;
using System.Text.Json;

namespace Validasoft.Dominio.Ficha;

/// <summary>Cómo cambió un campo o una fila entre la versión aceptada y la nueva.</summary>
public enum TipoDeCambio
{
    Agregado,
    Quitado,
    Modificado
}

/// <summary>
/// Un cambio de la revisión por sección (las Actualizaciones del SRM, FM-28): ruta snake_case del campo (o de la fila),
/// etiqueta legible, valor anterior y nuevo como texto (nulos si no había o ya no hay) y el tipo de cambio.
/// </summary>
public sealed record CambioDeCampo(string Campo, string Etiqueta, string? Anterior, string? Nuevo, TipoDeCambio TipoCambio);

/// <summary>
/// Comparación campo por campo de dos versiones de una sección (JSON canónico de F1): solo lo que cambió. Los campos simples
/// se comparan por valor; los objetos anidados por sus campos; las listas de códigos (responsabilidades, CIIU secundarias,
/// especialidades) como conjuntos; y las filas (personas de la legal, municipios de ICA) por su identidad
/// (rol + identificación; municipio + CIIU): filas agregadas, quitadas y, en las que siguen, sus campos modificados.
/// </summary>
public static class ComparacionDeSecciones
{
    private static readonly Dictionary<string, string> Etiquetas = new(StringComparer.Ordinal)
    {
        ["razon_social"] = "Razón social",
        ["nombres"] = "Nombres",
        ["apellidos"] = "Apellidos",
        ["tipo_persona"] = "Tipo de persona",
        ["tipo_identificacion"] = "Tipo de identificación",
        ["numero"] = "Número",
        ["dv"] = "DV",
        ["correo"] = "Correo electrónico",
        ["telefono"] = "Teléfono",
        ["pais"] = "País",
        ["ciudad"] = "Ciudad",
        ["direccion"] = "Dirección",
        ["direccion_estructurada"] = "Dirección estructurada",
        ["tipo_via"] = "Tipo de vía",
        ["numero_via"] = "Número de vía",
        ["letra_via"] = "Letra de vía",
        ["bis"] = "Bis",
        ["cuadrante"] = "Cuadrante",
        ["numero_cruce"] = "Número de cruce",
        ["letra_cruce"] = "Letra de cruce",
        ["placa"] = "Placa",
        ["complemento"] = "Complemento",
        ["ciiu_principal"] = "Actividad económica principal (CIIU)",
        ["ciiu_secundarias"] = "Actividades económicas secundarias",
        ["pagina_web"] = "Página web",
        ["certificado_iso"] = "Certificado ISO",
        ["anio_inicio"] = "Año de inicio",
        ["responsabilidades"] = "Responsabilidades (casilla 53 del RUT)",
        ["resolucion_gran_contribuyente"] = "Resolución de gran contribuyente",
        ["resolucion_autorretenedor"] = "Resolución de autorretenedor",
        ["fecha"] = "Fecha",
        ["responsable_ica"] = "Contribuyente de ICA",
        ["autorretenedor_ica"] = "Autorretenedor de ICA",
        ["ica"] = "Municipio de ICA",
        ["municipio"] = "Municipio",
        ["ciiu"] = "Actividad (CIIU)",
        ["tarifa_por_mil"] = "Tarifa por mil",
        ["periodo"] = "Periodo",
        ["fecha_corte"] = "Fecha de corte",
        ["activos"] = "Activos",
        ["pasivos"] = "Pasivos",
        ["patrimonio"] = "Patrimonio",
        ["ingresos_brutos"] = "Ingresos brutos",
        ["ingresos_netos"] = "Ingresos netos",
        ["gastos_operacionales"] = "Gastos operacionales",
        ["gastos_no_operacionales"] = "Gastos no operacionales",
        ["otros_ingresos"] = "Otros ingresos",
        ["moneda_extranjera"] = "Moneda extranjera",
        ["realiza"] = "Realiza operaciones",
        ["tipos"] = "Tipos de operación",
        ["paises"] = "Países",
        ["personas"] = "Persona relacionada",
        ["rol"] = "Rol",
        ["tipo_representacion"] = "Tipo de representación",
        ["cargo"] = "Cargo",
        ["porcentaje"] = "Porcentaje de participación",
        ["pep"] = "PEP",
        ["vinculo_pep"] = "Vínculo con PEP",
        ["fecha_expedicion"] = "Fecha de expedición",
        ["fecha_nacimiento"] = "Fecha de nacimiento",
        ["nacionalidad"] = "Nacionalidad",
        ["residencia"] = "País de residencia",
        ["revisor_fiscal_firma"] = "Firma de revisoría fiscal",
        ["nit"] = "NIT",
        ["programa_salud_ocupacional"] = "Programa de salud ocupacional (SG-SST)",
        ["identifica_factores_de_riesgo"] = "Identifica y controla los factores de riesgo",
        ["comite_paritario"] = "Comité paritario (COPASST) o vigía",
        ["programa_higiene_y_seguridad"] = "Programa de higiene y seguridad industrial",
        ["gestion_ambiental"] = "Gestión ambiental",
        ["reglamento_interno"] = "Reglamento interno de trabajo",
        ["politica_hseq"] = "Política HSEQ",
        ["licencias_y_permisos"] = "Licencias y permisos",
        ["especialidades"] = "Especialidad"
    };

    private static readonly Dictionary<string, string> Roles = new(StringComparer.Ordinal)
    {
        ["RepresentanteLegal"] = "Representante legal",
        ["Suplente"] = "Suplente",
        ["Apoderado"] = "Apoderado",
        ["Socio"] = "Socio",
        ["JuntaDirectiva"] = "Junta directiva",
        ["RevisorFiscal"] = "Revisor fiscal",
        ["Contador"] = "Contador"
    };

    /// <summary>Identidad de una fila por lista: personas por rol e identificación; ICA por municipio y actividad.</summary>
    private static readonly Dictionary<string, string[]> Identidades = new(StringComparer.Ordinal)
    {
        ["personas"] = ["rol", "tipo_identificacion", "numero"],
        ["ica"] = ["municipio", "ciiu"]
    };

    /// <summary>Cambios de <paramref name="anterior"/> a <paramref name="nuevo"/> (nulo = sección vacía), en el orden de los campos.</summary>
    public static IReadOnlyList<CambioDeCampo> Comparar(JsonElement? anterior, JsonElement? nuevo)
    {
        var cambios = new List<CambioDeCampo>();
        CompararObjetos(Objeto(anterior), Objeto(nuevo), string.Empty, string.Empty, cambios);
        return cambios;
    }

    public static string Etiqueta(string clave) =>
        Etiquetas.TryGetValue(clave, out var etiqueta) ? etiqueta : char.ToUpperInvariant(clave[0]) + clave[1..].Replace('_', ' ');

    private static Dictionary<string, JsonElement> Objeto(JsonElement? json) =>
        json is { ValueKind: JsonValueKind.Object } objeto
            ? objeto.EnumerateObject().Where(p => p.Value.ValueKind != JsonValueKind.Null).ToDictionary(p => p.Name, p => p.Value)
            : [];

    private static void CompararObjetos(Dictionary<string, JsonElement> a, Dictionary<string, JsonElement> b, string ruta, string titulo,
        List<CambioDeCampo> cambios)
    {
        // Orden: el de la versión nueva y luego lo que solo tenía la anterior.
        foreach (var clave in b.Keys.Concat(a.Keys.Where(k => !b.ContainsKey(k))))
        {
            var campo = ruta.Length == 0 ? clave : $"{ruta}.{clave}";
            var etiqueta = titulo.Length == 0 ? Etiqueta(clave) : $"{titulo} · {Etiqueta(clave)}";
            var tieneA = a.TryGetValue(clave, out var va);
            var tieneB = b.TryGetValue(clave, out var vb);
            var tipo = tieneB ? vb.ValueKind : va.ValueKind;

            if (tipo == JsonValueKind.Object || (tieneA && va.ValueKind == JsonValueKind.Object))
            {
                CompararObjetos(Objeto(tieneA ? va : null), Objeto(tieneB ? vb : null), campo, etiqueta, cambios);
                continue;
            }

            if (tipo == JsonValueKind.Array || (tieneA && va.ValueKind == JsonValueKind.Array))
            {
                var filasA = tieneA && va.ValueKind == JsonValueKind.Array ? va.EnumerateArray().ToList() : [];
                var filasB = tieneB && vb.ValueKind == JsonValueKind.Array ? vb.EnumerateArray().ToList() : [];
                if (filasA.Concat(filasB).Any(f => f.ValueKind == JsonValueKind.Object))
                    CompararFilas(clave, filasA, filasB, campo, etiqueta, cambios);
                else
                    CompararConjuntos(filasA, filasB, campo, etiqueta, cambios);
                continue;
            }

            var textoA = tieneA ? Texto(va) : null;
            var textoB = tieneB ? Texto(vb) : null;
            if (textoA == textoB)
                continue;
            cambios.Add(new CambioDeCampo(campo, etiqueta, textoA, textoB,
                textoA is null ? TipoDeCambio.Agregado : textoB is null ? TipoDeCambio.Quitado : TipoDeCambio.Modificado));
        }
    }

    private static void CompararConjuntos(List<JsonElement> a, List<JsonElement> b, string campo, string etiqueta, List<CambioDeCampo> cambios)
    {
        var antes = a.Select(Texto).OfType<string>().ToList();
        var despues = b.Select(Texto).OfType<string>().ToList();
        foreach (var quitado in antes.Where(v => !despues.Contains(v)))
            cambios.Add(new CambioDeCampo(campo, etiqueta, quitado, null, TipoDeCambio.Quitado));
        foreach (var agregado in despues.Where(v => !antes.Contains(v)))
            cambios.Add(new CambioDeCampo(campo, etiqueta, null, agregado, TipoDeCambio.Agregado));
    }

    private static void CompararFilas(string lista, List<JsonElement> a, List<JsonElement> b, string campo, string etiqueta, List<CambioDeCampo> cambios)
    {
        var claves = Identidades.GetValueOrDefault(lista);
        string Identidad(JsonElement fila, int indice) =>
            claves is null ? indice.ToString(CultureInfo.InvariantCulture) : string.Join("|", claves.Select(c => Valor(fila, c) ?? string.Empty));

        var anteriores = a.Select((fila, i) => (Fila: fila, Indice: i, Clave: Identidad(fila, i))).ToList();
        var nuevas = b.Select((fila, i) => (Fila: fila, Indice: i, Clave: Identidad(fila, i))).ToList();

        foreach (var nueva in nuevas)
        {
            var ruta = $"{campo}[{nueva.Indice}]";
            var previa = anteriores.FirstOrDefault(x => x.Clave == nueva.Clave);
            if (previa.Clave is null)
            {
                cambios.Add(new CambioDeCampo(ruta, etiqueta, null, Resumen(lista, nueva.Fila), TipoDeCambio.Agregado));
                continue;
            }

            CompararObjetos(Objeto(previa.Fila), Objeto(nueva.Fila), ruta, Resumen(lista, nueva.Fila), cambios);
        }

        foreach (var quitada in anteriores.Where(x => nuevas.All(n => n.Clave != x.Clave)))
            cambios.Add(new CambioDeCampo($"{campo}[{quitada.Indice}]", etiqueta, Resumen(lista, quitada.Fila), null, TipoDeCambio.Quitado));
    }

    /// <summary>«Socio: Juan Pérez (CC 79123456)», «11001 · CIIU 4663» o los valores de la fila.</summary>
    private static string Resumen(string lista, JsonElement fila) => lista switch
    {
        "personas" => $"{Roles.GetValueOrDefault(Valor(fila, "rol") ?? string.Empty, Valor(fila, "rol") ?? "Persona")}: " +
                      $"{string.Join(' ', new[] { Valor(fila, "nombres"), Valor(fila, "apellidos") }.OfType<string>())} " +
                      $"({Valor(fila, "tipo_identificacion")} {Valor(fila, "numero")})",
        "ica" => $"{Valor(fila, "municipio")} · CIIU {Valor(fila, "ciiu")}",
        _ => string.Join(" · ", Objeto(fila).Values.Select(Texto).OfType<string>())
    };

    private static string? Valor(JsonElement fila, string clave) =>
        fila.ValueKind == JsonValueKind.Object && fila.TryGetProperty(clave, out var valor) ? Texto(valor) : null;

    /// <summary>Texto de un valor: cadenas tal cual, números con su texto JSON, sí/no y listas separadas por coma.</summary>
    private static string? Texto(JsonElement valor) => valor.ValueKind switch
    {
        JsonValueKind.String => valor.GetString(),
        JsonValueKind.Number => valor.GetRawText(),
        JsonValueKind.True => "Sí",
        JsonValueKind.False => "No",
        JsonValueKind.Array => string.Join(", ", valor.EnumerateArray().Select(Texto).OfType<string>()),
        JsonValueKind.Object => valor.GetRawText(),
        _ => null
    };
}
