using System.Globalization;
using System.Text.Json;
using System.Text.Json.Nodes;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha;

/// <summary>Un cambio que propone el documento: ruta snake_case del campo, etiqueta y los valores actual y del documento como texto.</summary>
public sealed record CampoPropuesto(string Campo, string Etiqueta, string? Actual, string? Propuesto);

/// <summary>
/// Lo que el documento propone para una sección (FM-14): los campos que cambian y los datos completos de la sección con esos
/// cambios aplicados sobre la versión vigente (para prellenar el wizard). Nunca se guarda sola: el tercero la confirma.
/// </summary>
public sealed record PropuestaDeSeccion(IReadOnlyList<CampoPropuesto> Campos, JsonElement Datos)
{
    public bool HayCambios => Campos.Count > 0;
}

/// <summary>
/// Actualización desde el documento (FM-14, docs/14 §5.3): con los campos que la IA extrajo de la versión nueva del documento,
/// propone los cambios de una sección que ese documento respalda (mapa de §5.1). Solo propone lo que el tercero puede editar
/// (la identificación, el DV y el tipo de persona salen de su registro) y lo que difiere de lo declarado según la misma regla
/// del cruce; lo que el documento no trae se deja como está. Las personas de la legal solo se agregan (nunca se quitan).
/// Función pura; la aplicación descarta lo que no pase la regla única de la sección (<paramref name="excluidos"/>).
/// </summary>
public static class PropuestaDesdeDocumento
{
    public static PropuestaDeSeccion Proponer(string seccion, JsonElement? actuales, TipoPersona tipoPersona, int? periodo, string tipoDocumento,
        IReadOnlyDictionary<string, JsonElement> extraidos, IReadOnlySet<string>? excluidos = null)
    {
        var datos = actuales is { ValueKind: JsonValueKind.Object } json ? (JsonObject)JsonNode.Parse(json.GetRawText())! : new JsonObject();
        var propuesta = new Constructor(datos, excluidos ?? new HashSet<string>());
        var tipo = tipoDocumento.ToUpperInvariant();
        if (MapaDeRespaldo.Respalda(tipo, seccion))
        {
            switch (seccion)
            {
                case CodigosDeSeccion.General:
                    General(propuesta, tipoPersona, tipo, extraidos);
                    break;
                case CodigosDeSeccion.Tributaria when tipo == "RUT":
                    var responsabilidades = Normalizacion.Codigos(LecturaDeHechos.Lista(extraidos, "responsabilidades"), 2).Order(StringComparer.Ordinal).ToList();
                    propuesta.Conjunto("responsabilidades", "Responsabilidades (casilla 53 del RUT)", responsabilidades, 2);
                    break;
                case CodigosDeSeccion.Legal when tipoPersona != TipoPersona.Natural:
                    Legal(propuesta, tipo, extraidos);
                    break;
                case CodigosDeSeccion.Financiera when tipo == "ESTADOS_FINANCIEROS":
                    Financiera(propuesta, periodo, extraidos);
                    break;
            }
        }

        return new PropuestaDeSeccion(propuesta.Campos, JsonSerializer.SerializeToElement(datos));
    }

    private static void General(Constructor p, TipoPersona tipoPersona, string tipo, IReadOnlyDictionary<string, JsonElement> x)
    {
        var natural = tipoPersona == TipoPersona.Natural;
        if (tipo == "CEDULA")
        {
            if (!natural)
                return;
            p.Texto("nombres", "Nombres", LecturaDeHechos.Texto(x, "nombres"), CruceConDocumento.Comparador.Nombre);
            p.Texto("apellidos", "Apellidos", LecturaDeHechos.Texto(x, "apellidos"), CruceConDocumento.Comparador.Nombre);
            return;
        }

        if (!natural)
            p.Texto("razon_social", "Razón social", LecturaDeHechos.Texto(x, "razonSocial"), CruceConDocumento.Comparador.Nombre);
        if (tipo != "RUT")
            return;

        p.Texto("direccion", "Dirección", LecturaDeHechos.Texto(x, "direccion"), CruceConDocumento.Comparador.Direccion);
        if (Digitos(LecturaDeHechos.Texto(x, "codigoMunicipio")) is { Length: 4 or 5 } dane)
            p.Texto("ciudad", "Ciudad (código DANE)", dane.PadLeft(5, '0'), CruceConDocumento.Comparador.Codigo);
        if (LecturaDeHechos.Texto(x, "correo") is { } correo && correo.Contains('@', StringComparison.Ordinal))
            p.Texto("correo", "Correo electrónico", correo.ToLowerInvariant(), CruceConDocumento.Comparador.Correo);
        if (Digitos(LecturaDeHechos.Texto(x, "telefono")) is { Length: >= 7 } telefono)
            p.Texto("telefono", "Teléfono", telefono, CruceConDocumento.Comparador.Telefono);
        if (Digitos(LecturaDeHechos.Texto(x, "actividadEconomicaPrincipal")) is { Length: >= 1 and <= 4 } ciiu)
            p.Texto("ciiu_principal", "Actividad económica principal (CIIU)", ciiu.PadLeft(4, '0'), CruceConDocumento.Comparador.Codigo);
        if (LecturaDeHechos.Lista(x, "actividadesSecundarias") is { } secundarias)
            p.Conjunto("ciiu_secundarias", "Actividades económicas secundarias", [.. Normalizacion.Codigos(secundarias, 4).Order(StringComparer.Ordinal)], 4);
    }

    private static void Legal(Constructor p, string tipo, IReadOnlyDictionary<string, JsonElement> x)
    {
        var enDocumento = tipo switch
        {
            "CAMARA_COMERCIO" => LecturaDeHechos.Personas(x, "representantesLegales") ?? [],
            "RUT" when LecturaDeHechos.Texto(x, "representanteLegalNombre") is not null || LecturaDeHechos.Texto(x, "representanteLegalIdentificacion") is not null =>
                [new PersonaExtraida(LecturaDeHechos.Texto(x, "representanteLegalNombre"), LecturaDeHechos.Texto(x, "representanteLegalIdentificacion"), "Principal")],
            _ => []
        };
        if (enDocumento.Count == 0)
            return;

        var personas = p.Datos["personas"] as JsonArray;
        var declaradas = (personas ?? new JsonArray()).OfType<JsonObject>()
            .Where(o => o["rol"]?.GetValue<string>() is "RepresentanteLegal" or "Suplente")
            .Select(o => (Nombre: CruceConDocumento.NombreCompleto(o["nombres"]?.GetValue<string>(), o["apellidos"]?.GetValue<string>()),
                Numero: o["numero"]?.GetValue<string>()))
            .ToList();
        foreach (var persona in enDocumento)
        {
            if (declaradas.Any(d => CruceConDocumento.MismaPersona(d.Nombre, d.Numero, persona.Nombre, persona.Identificacion)))
                continue;
            var indice = (personas?.Count ?? 0);
            var campo = $"personas[{indice}]";
            if (p.Excluidos.Contains(campo))
                continue;

            var suplente = Normalizacion.Texto(persona.Cargo).Contains("SUPLENTE", StringComparison.Ordinal);
            var numero = Digitos(persona.Identificacion);
            var fila = new JsonObject
            {
                ["rol"] = suplente ? "Suplente" : "RepresentanteLegal",
                ["tipo_representacion"] = suplente ? "Suplente" : "Principal",
                ["tipo_identificacion"] = numero is { Length: > 0 } ? "CC" : null,
                ["numero"] = numero is { Length: > 0 } ? numero : null,
                ["nombres"] = persona.Nombre,
                ["cargo"] = persona.Cargo
            };
            foreach (var vacio in fila.Where(par => par.Value is null).Select(par => par.Key).ToList())
                fila.Remove(vacio);
            personas ??= new JsonArray();
            if (p.Datos["personas"] is null)
                p.Datos["personas"] = personas;
            personas.Add(fila);
            declaradas.Add((persona.Nombre, numero));
            p.Agregar(new CampoPropuesto(campo, suplente ? "Representante legal suplente" : "Representante legal", null,
                CruceConDocumento.Resumen(persona.Nombre, persona.Identificacion, persona.Cargo)));
        }
    }

    private static void Financiera(Constructor p, int? periodo, IReadOnlyDictionary<string, JsonElement> x)
    {
        var delDocumento = MapaDeRespaldo.Periodo(x);
        if (delDocumento is { } d && periodo is { } s && d != s)
            return; // Los estados financieros de otro año no proponen nada para este periodo.

        p.Cifra("activos", "Activos", LecturaDeHechos.Texto(x, "activoTotal"));
        p.Cifra("pasivos", "Pasivos", LecturaDeHechos.Texto(x, "pasivoTotal"));
        p.Cifra("patrimonio", "Patrimonio", LecturaDeHechos.Texto(x, "patrimonio"));
        p.Cifra("ingresos_brutos", "Ingresos brutos", LecturaDeHechos.Texto(x, "ingresosBrutos"));
        if (LecturaDeHechos.Texto(x, "fechaCorte") is { } corte &&
            DateOnly.TryParseExact(corte, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var fecha))
            p.Texto("fecha_corte", "Fecha de corte", fecha.ToString("yyyy-MM-dd", CultureInfo.InvariantCulture), CruceConDocumento.Comparador.Texto);
    }

    private static string? Digitos(string? texto)
    {
        var digitos = new string((texto ?? string.Empty).Where(char.IsDigit).ToArray());
        return digitos.Length == 0 ? null : digitos;
    }

    /// <summary>Acumula los campos que cambian y los aplica sobre los datos.</summary>
    private sealed class Constructor(JsonObject datos, IReadOnlySet<string> excluidos)
    {
        private readonly List<CampoPropuesto> _campos = [];

        public JsonObject Datos { get; } = datos;

        public IReadOnlySet<string> Excluidos { get; } = excluidos;

        public IReadOnlyList<CampoPropuesto> Campos => _campos;

        public void Agregar(CampoPropuesto campo) => _campos.Add(campo);

        public void Texto(string campo, string etiqueta, string? propuesto, CruceConDocumento.Comparador comparador)
        {
            if (string.IsNullOrWhiteSpace(propuesto) || Excluidos.Contains(campo))
                return;
            var actual = Actual(campo);
            if (actual is not null && CruceConDocumento.Comparar(actual, propuesto, comparador).Coincide)
                return;
            Datos[campo] = propuesto.Trim();
            _campos.Add(new CampoPropuesto(campo, etiqueta, actual, propuesto.Trim()));
        }

        public void Cifra(string campo, string etiqueta, string? propuesto)
        {
            if (!Normalizacion.Decimal(propuesto, out var valor) || Excluidos.Contains(campo))
                return;
            valor = Math.Round(valor, 2, MidpointRounding.AwayFromZero);
            var texto = valor.ToString(CultureInfo.InvariantCulture);
            var actual = Actual(campo);
            if (actual is not null && Normalizacion.Decimal(actual, out var declarado) && declarado == valor)
                return;
            Datos[campo] = valor;
            _campos.Add(new CampoPropuesto(campo, etiqueta, actual, texto));
        }

        public void Conjunto(string campo, string etiqueta, IReadOnlyList<string> propuesto, int digitos)
        {
            if (propuesto.Count == 0 || Excluidos.Contains(campo))
                return;
            var actuales = Datos[campo] is JsonArray lista ? lista.Select(n => n?.GetValue<string>()).OfType<string>().ToList() : [];
            if (Normalizacion.Codigos(actuales, digitos).SetEquals(Normalizacion.Codigos(propuesto, digitos)))
                return;
            Datos[campo] = new JsonArray([.. propuesto.Select(c => (JsonNode?)JsonValue.Create(c))]);
            _campos.Add(new CampoPropuesto(campo, etiqueta, actuales.Count == 0 ? null : string.Join(", ", actuales), string.Join(", ", propuesto)));
        }

        private string? Actual(string campo) => Datos[campo] switch
        {
            null => null,
            JsonValue v when v.GetValueKind() == JsonValueKind.String => v.GetValue<string>(),
            JsonValue v => v.ToJsonString(),
            var otro => otro.ToJsonString()
        };
    }
}
