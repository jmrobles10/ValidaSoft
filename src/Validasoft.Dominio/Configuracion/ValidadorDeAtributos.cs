using System.Globalization;
using System.Text.Json;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Configuracion;

/// <summary>Valida los atributos de un vínculo contra los permitidos por su tipo.</summary>
public static class ValidadorDeAtributos
{
    public static Dictionary<string, JsonElement> Validar(TipoVinculo tipo, IReadOnlyDictionary<string, JsonElement>? atributos)
    {
        var valores = new Dictionary<string, JsonElement>(atributos ?? new Dictionary<string, JsonElement>());

        var desconocido = valores.Keys.FirstOrDefault(k => tipo.AtributosPermitidos.All(a => a.Clave != k));
        if (desconocido is not null)
            throw ReglaDeNegocioException.Validacion("atributo-no-permitido",
                $"El tipo de vínculo {tipo.Codigo} no admite el atributo '{desconocido}'.");

        foreach (var atributo in tipo.AtributosPermitidos)
        {
            if (!valores.TryGetValue(atributo.Clave, out var valor) || valor.ValueKind is JsonValueKind.Null or JsonValueKind.Undefined)
            {
                if (atributo.Obligatorio)
                    throw ReglaDeNegocioException.Validacion("atributo-requerido", $"El atributo '{atributo.Clave}' es obligatorio.");
                valores.Remove(atributo.Clave);
                continue;
            }

            if (!EsValido(atributo, valor))
                throw ReglaDeNegocioException.Validacion("atributo-invalido",
                    $"El valor del atributo '{atributo.Clave}' no es un {atributo.Tipo.ToString().ToLowerInvariant()} válido.");
        }

        return valores;
    }

    private static bool EsValido(AtributoPermitido atributo, JsonElement valor) => atributo.Tipo switch
    {
        TipoAtributo.Texto => valor.ValueKind == JsonValueKind.String,
        TipoAtributo.Numero => valor.ValueKind == JsonValueKind.Number,
        TipoAtributo.Booleano => valor.ValueKind is JsonValueKind.True or JsonValueKind.False,
        TipoAtributo.Fecha => valor.ValueKind == JsonValueKind.String &&
                              DateOnly.TryParseExact(valor.GetString(), "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out _),
        TipoAtributo.Opcion => valor.ValueKind == JsonValueKind.String && atributo.Opciones!.Contains(valor.GetString()!),
        TipoAtributo.ListaOpciones => valor.ValueKind == JsonValueKind.Array &&
                                      valor.EnumerateArray().All(e => e.ValueKind == JsonValueKind.String && atributo.Opciones!.Contains(e.GetString()!)),
        _ => false
    };
}
