using System.Globalization;
using System.Text;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Invitaciones;

namespace Validasoft.Aplicacion.Invitaciones;

/// <summary>
/// Plantilla de la invitación masiva (LT-6): las columnas, el archivo descargable (.xlsx o .csv) y la lectura de lo que sube la
/// empresa. Los encabezados se reconocen sin importar mayúsculas, tildes, espacios ni guiones («Número de identificación»,
/// «numero_identificacion» o «NIT»); las columnas que no se reconocen se ignoran y las filas vacías se saltan.
/// </summary>
public static class ArchivoDeLote
{
    /// <summary>Columnas en el orden de la plantilla; las obligatorias van primero.</summary>
    public static readonly IReadOnlyList<(string Campo, string Encabezado, bool Obligatoria)> Columnas =
    [
        ("tipoIdentificacion", "Tipo de identificación", true),
        ("numeroIdentificacion", "Número de identificación", true),
        ("nombre", "Razón social o nombres", true),
        ("correo", "Correo", true),
        ("tipoVinculo", "Tipo de vínculo", true),
        ("pais", "País", false),
        ("digitoVerificacion", "DV", false),
        ("tipoPersona", "Tipo de persona", false),
        ("nombreComercial", "Nombre comercial", false),
        ("flujo", "Flujo", false),
        ("contacto", "Contacto", false),
        ("telefono", "Teléfono", false)
    ];

    /// <summary>Encabezado normalizado → campo.</summary>
    private static readonly Dictionary<string, string> Sinonimos = new(StringComparer.Ordinal)
    {
        ["tipoidentificacion"] = "tipoIdentificacion", ["tipodeidentificacion"] = "tipoIdentificacion", ["tipoid"] = "tipoIdentificacion",
        ["tipodocumento"] = "tipoIdentificacion", ["tipodedocumento"] = "tipoIdentificacion",
        ["numeroidentificacion"] = "numeroIdentificacion", ["numerodeidentificacion"] = "numeroIdentificacion", ["identificacion"] = "numeroIdentificacion",
        ["numero"] = "numeroIdentificacion", ["nit"] = "numeroIdentificacion", ["documento"] = "numeroIdentificacion",
        ["numerodocumento"] = "numeroIdentificacion", ["numerodedocumento"] = "numeroIdentificacion",
        ["razonsocialonombres"] = "nombre", ["razonsocial"] = "nombre", ["nombre"] = "nombre", ["nombres"] = "nombre",
        ["nombreorazonsocial"] = "nombre",
        ["correo"] = "correo", ["correoelectronico"] = "correo", ["email"] = "correo", ["correoinvitacion"] = "correo", ["correodeinvitacion"] = "correo",
        ["tipovinculo"] = "tipoVinculo", ["tipodevinculo"] = "tipoVinculo", ["vinculo"] = "tipoVinculo",
        ["pais"] = "pais",
        ["dv"] = "digitoVerificacion", ["digitoverificacion"] = "digitoVerificacion", ["digitodeverificacion"] = "digitoVerificacion",
        ["tipopersona"] = "tipoPersona", ["tipodepersona"] = "tipoPersona",
        ["nombrecomercial"] = "nombreComercial",
        ["flujo"] = "flujo", ["codigoflujo"] = "flujo", ["codigodeflujo"] = "flujo",
        ["contacto"] = "contacto", ["nombrecontacto"] = "contacto", ["nombredelcontacto"] = "contacto",
        ["telefono"] = "telefono", ["celular"] = "telefono"
    };

    public const string TipoCsv = "text/csv; charset=utf-8";

    /// <summary>La plantilla vacía (solo encabezados) en el formato pedido: <c>xlsx</c> (por omisión) o <c>csv</c>.</summary>
    public static (byte[] Contenido, string Tipo, string Nombre) Plantilla(string? formato)
    {
        var encabezado = Columnas.Select(c => c.Encabezado).ToList();
        if (string.Equals(formato, "csv", StringComparison.OrdinalIgnoreCase))
            return (Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(string.Join(';', encabezado) + "\r\n")).ToArray(), TipoCsv,
                "plantilla-invitacion-masiva.csv");
        if (formato is not null && !string.Equals(formato, "xlsx", StringComparison.OrdinalIgnoreCase))
            throw ReglaDeNegocioException.Validacion("formato-invalido", "El formato de la plantilla es xlsx o csv.");
        return (HojaDeCalculo.Crear("Invitaciones", encabezado, []), HojaDeCalculo.TipoContenido, "plantilla-invitacion-masiva.xlsx");
    }

    /// <summary>
    /// Filas del archivo (base64; .xlsx o .csv por la extensión del nombre). 422 <c>archivo-invalido</c> (no se puede leer),
    /// <c>formato-invalido</c> (otra extensión), <c>plantilla-invalida</c> (faltan columnas obligatorias) y
    /// <c>lote-sin-filas</c>.
    /// </summary>
    public static IReadOnlyList<FilaDeLoteEntrada> Leer(ArchivoDeLoteDto archivo)
    {
        byte[] contenido;
        try
        {
            contenido = Convert.FromBase64String(archivo.ContenidoBase64 ?? string.Empty);
        }
        catch (FormatException)
        {
            throw ReglaDeNegocioException.Validacion("archivo-invalido", "El contenido del archivo no está en base64.");
        }

        var nombre = (archivo.Nombre ?? string.Empty).Trim().ToLowerInvariant();
        var filas = nombre.EndsWith(".xlsx", StringComparison.Ordinal) ? LectorDeHojaDeCalculo.Leer(contenido)
            : nombre.EndsWith(".csv", StringComparison.Ordinal) || nombre.EndsWith(".txt", StringComparison.Ordinal) ? Csv(contenido)
            : throw ReglaDeNegocioException.Validacion("formato-invalido", "Sube la plantilla como .xlsx o .csv.");
        return DeFilas(filas);
    }

    /// <summary>Encabezado en la primera fila no vacía; cada fila siguiente con algún valor es una fila del lote.</summary>
    public static IReadOnlyList<FilaDeLoteEntrada> DeFilas(IReadOnlyList<IReadOnlyList<string>> filas)
    {
        var inicio = 0;
        while (inicio < filas.Count && filas[inicio].All(string.IsNullOrWhiteSpace))
            inicio++;
        if (inicio == filas.Count)
            throw ReglaDeNegocioException.Validacion("lote-sin-filas", "El archivo está vacío.");

        var indices = new Dictionary<string, int>(StringComparer.Ordinal);
        for (var i = 0; i < filas[inicio].Count; i++)
            if (Sinonimos.TryGetValue(Normalizar(filas[inicio][i]), out var campo))
                indices.TryAdd(campo, i);

        var faltantes = Columnas.Where(c => c.Obligatoria && !indices.ContainsKey(c.Campo)).Select(c => c.Encabezado).ToList();
        if (faltantes.Count > 0)
            throw ReglaDeNegocioException.Validacion("plantilla-invalida",
                $"Al archivo le faltan columnas de la plantilla: {string.Join(", ", faltantes)}. Descarga la plantilla y úsala como base.");

        string? Valor(IReadOnlyList<string> fila, string campo) =>
            indices.TryGetValue(campo, out var i) && i < fila.Count && !string.IsNullOrWhiteSpace(fila[i]) ? fila[i].Trim() : null;

        var resultado = new List<FilaDeLoteEntrada>();
        foreach (var fila in filas.Skip(inicio + 1).Where(f => f.Any(c => !string.IsNullOrWhiteSpace(c))))
            resultado.Add(new FilaDeLoteEntrada(Valor(fila, "pais"), Valor(fila, "tipoIdentificacion"), Valor(fila, "numeroIdentificacion"),
                Valor(fila, "digitoVerificacion"), Valor(fila, "tipoPersona"), Valor(fila, "nombre"), Valor(fila, "nombreComercial"), Valor(fila, "correo"),
                Valor(fila, "tipoVinculo"), Valor(fila, "flujo"), Valor(fila, "contacto"), Valor(fila, "telefono")));
        if (resultado.Count == 0)
            throw ReglaDeNegocioException.Validacion("lote-sin-filas", "El archivo no tiene filas debajo del encabezado.");
        return resultado;
    }

    /// <summary>Minúsculas, sin tildes y solo letras y dígitos («Número de identificación» → <c>numerodeidentificacion</c>).</summary>
    public static string Normalizar(string? texto)
    {
        var descompuesto = (texto ?? string.Empty).Trim().ToLowerInvariant().Normalize(NormalizationForm.FormD);
        var limpio = new StringBuilder();
        foreach (var c in descompuesto)
            if (CharUnicodeInfo.GetUnicodeCategory(c) != UnicodeCategory.NonSpacingMark && char.IsLetterOrDigit(c))
                limpio.Append(c);
        return limpio.ToString();
    }

    /// <summary>
    /// CSV en UTF-8 (con o sin BOM; si no es UTF-8 válido, Windows-1252 como lo guarda Excel en español), separado por <c>;</c>
    /// o <c>,</c> (el que más aparezca en el encabezado) y con comillas dobles (RFC 4180).
    /// </summary>
    public static IReadOnlyList<IReadOnlyList<string>> Csv(byte[] contenido)
    {
        string texto;
        try
        {
            texto = new UTF8Encoding(false, true).GetString(contenido);
        }
        catch (DecoderFallbackException)
        {
            texto = Encoding.Latin1.GetString(contenido);
        }

        texto = texto.TrimStart('﻿');
        var primeraLinea = texto.Split('\n', 2)[0];
        var separador = primeraLinea.Count(c => c == ';') >= primeraLinea.Count(c => c == ',') ? ';' : ',';

        var filas = new List<IReadOnlyList<string>>();
        var fila = new List<string>();
        var celda = new StringBuilder();
        var entreComillas = false;
        for (var i = 0; i < texto.Length; i++)
        {
            var c = texto[i];
            if (entreComillas)
            {
                if (c == '"' && i + 1 < texto.Length && texto[i + 1] == '"')
                {
                    celda.Append('"');
                    i++;
                }
                else if (c == '"')
                    entreComillas = false;
                else
                    celda.Append(c);
            }
            else if (c == '"')
                entreComillas = true;
            else if (c == separador)
            {
                fila.Add(celda.ToString());
                celda.Clear();
            }
            else if (c == '\n' || c == '\r')
            {
                if (c == '\r' && i + 1 < texto.Length && texto[i + 1] == '\n')
                    i++;
                fila.Add(celda.ToString());
                celda.Clear();
                filas.Add(fila);
                fila = [];
            }
            else
                celda.Append(c);
        }

        if (celda.Length > 0 || fila.Count > 0)
        {
            fila.Add(celda.ToString());
            filas.Add(fila);
        }

        return filas;
    }

    public static FilaDeLoteEntrada ADominio(this FilaDeLoteEntradaDto f) =>
        new(Recortar(f.Pais), Recortar(f.TipoIdentificacion), Recortar(f.NumeroIdentificacion), Recortar(f.DigitoVerificacion), Recortar(f.TipoPersona),
            Recortar(f.Nombre), Recortar(f.NombreComercial), Recortar(f.Correo), Recortar(f.TipoVinculo), Recortar(f.Flujo), Recortar(f.Contacto),
            Recortar(f.Telefono));

    public static FilaDeLoteEntradaDto ADto(this FilaDeLoteEntrada f) =>
        new(f.Pais, f.TipoIdentificacion, f.NumeroIdentificacion, f.DigitoVerificacion, f.TipoPersona, f.Nombre, f.NombreComercial, f.Correo,
            f.TipoVinculo, f.Flujo, f.Contacto, f.Telefono);

    private static string? Recortar(string? valor) => string.IsNullOrWhiteSpace(valor) ? null : valor.Trim();
}
