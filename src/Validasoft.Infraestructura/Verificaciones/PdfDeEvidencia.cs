using System.Globalization;
using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Infraestructura.Certificados;

namespace Validasoft.Infraestructura.Verificaciones;

/// <summary>
/// PDF de la evidencia de una consulta de listas (VE-10), con el mismo lienzo del certificado (PDF 1.7, fuentes estándar, sin
/// dependencias): empresa y tercero, proveedor, referencia, fecha, quién la hizo (o el sistema), criterio, listas, personas
/// consultadas, coincidencias y el SHA-256 de la respuesta cruda del proveedor si se guardó.
/// </summary>
public sealed class GeneradorPdfDeEvidencia : IGeneradorDePdfDeEvidencia
{
    private const double Ancho = 595.28;
    private const double Alto = 841.89;
    private const double Margen = 48;
    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    public byte[] Generar(DatosEvidenciaDeListas d)
    {
        var pdf = new LienzoPdf();
        var y = Alto - Margen;
        pdf.Texto(Margen, y - 14, Fuente.HelveticaBold, 16, d.Empresa);
        pdf.Texto(Margen, y - 30, Fuente.Helvetica, 9, "Evidencia de consulta en listas restrictivas", 0.35);
        y -= 42;
        pdf.Linea(Margen, y, Ancho - Margen, y, 0.6);
        y -= 8;

        y = Dato(pdf, y, "Tercero", d.Tercero);
        y = Dato(pdf, y, "Expediente", d.Contexto);
        y = Dato(pdf, y, "Proveedor", d.Proveedor);
        y = Dato(pdf, y, "Referencia", d.ReferenciaExterna);
        y = Dato(pdf, y, "Fecha (UTC)", d.Fecha.UtcDateTime.ToString("yyyy-MM-dd HH:mm:ss", Invariante));
        y = Dato(pdf, y, "Consultado por", d.ConsultadoPor);
        y = Dato(pdf, y, "Criterio", d.Criterio);
        y = Dato(pdf, y, "Listas", d.ListasConsultadas.Count == 0 ? "Las que cubre el contrato con el proveedor" : string.Join(", ", d.ListasConsultadas));
        if (d.Sha256Respuesta is { } sha)
            y = Dato(pdf, y, "Respuesta (SHA-256)", sha);

        y = Seccion(pdf, y, "Personas consultadas");
        y = Fila(pdf, y, true, ("Rol", 110), ("Nombre", 250), ("Identificación", 139));
        foreach (var p in d.Personas)
            y = Fila(pdf, y, false, (Rol(p.Rol), 110), (p.Nombre, 250), (p.Identificacion, 139));

        y = Seccion(pdf, y, "Coincidencias");
        if (d.Coincidencias.Count == 0)
        {
            y = Fila(pdf, y, false, ("Sin coincidencias en las listas consultadas.", 499));
        }
        else
        {
            var nombres = d.Personas.ToDictionary(p => p.Clave, p => p.Nombre, StringComparer.OrdinalIgnoreCase);
            y = Fila(pdf, y, true, ("Persona", 130), ("Lista", 110), ("Nombre encontrado", 140), ("Detalle", 119));
            foreach (var c in d.Coincidencias)
                y = Fila(pdf, y, false, (nombres.GetValueOrDefault(c.ClavePersona, c.ClavePersona), 130), (c.Lista, 110),
                    (c.Puntaje is { } puntaje ? $"{c.NombreEncontrado} ({puntaje.ToString("0.##", Invariante)} %)" : c.NombreEncontrado, 140), (c.Detalle ?? "—", 119));
        }

        y -= 10;
        foreach (var linea in Medidas.Partir("Una coincidencia no es una decisión: la analiza el Oficial de Cumplimiento, que la descarta o la confirma " +
                                             "con su justificación. La consulta se hizo amparada en la autorización de tratamiento de datos del tercero.",
                     Fuente.Helvetica, 8, Ancho - 2 * Margen))
        {
            y = Espacio(pdf, y, 10);
            y -= 10;
            pdf.Texto(Margen, y, Fuente.Helvetica, 8, linea, 0.35);
        }

        return pdf.Construir($"Evidencia de listas {d.ReferenciaExterna}", $"{d.Proveedor} · {d.Tercero}", d.Fecha,
            (lienzo, n) => lienzo.Texto(Margen, Margen - 14, Fuente.Helvetica, 7, $"{d.ReferenciaExterna} · página {n.Item1} de {n.Item2}", 0.45));
    }

    private static string Rol(string rol) => rol switch
    {
        "RepresentanteLegal" => "Representante legal",
        "Pep" => "Persona expuesta (PEP)",
        _ => rol
    };

    private static double Dato(LienzoPdf pdf, double y, string etiqueta, string valor)
    {
        var lineas = Medidas.Partir(valor, Fuente.Helvetica, 10, Ancho - 2 * Margen - 120, cortarPalabras: true);
        y = Espacio(pdf, y, 14 * lineas.Count + 2);
        pdf.Texto(Margen, y - 12, Fuente.HelveticaBold, 9, etiqueta, 0.25);
        foreach (var linea in lineas)
        {
            y -= 14;
            pdf.Texto(Margen + 120, y + 2, Fuente.Helvetica, 10, linea);
        }

        return y - 2;
    }

    private static double Seccion(LienzoPdf pdf, double y, string titulo)
    {
        y = Espacio(pdf, y, 34);
        y -= 22;
        pdf.Texto(Margen, y, Fuente.HelveticaBold, 11, titulo);
        y -= 5;
        pdf.Linea(Margen, y, Ancho - Margen, y, 0.3);
        return y - 4;
    }

    private static double Fila(LienzoPdf pdf, double y, bool encabezado, params (string Texto, double Ancho)[] celdas)
    {
        var fuente = encabezado ? Fuente.HelveticaBold : Fuente.Helvetica;
        const double tamano = 8.5;
        var lineas = celdas.Select(c => Medidas.Partir(c.Texto, fuente, tamano, c.Ancho - 6, cortarPalabras: true)).ToList();
        var alto = 11 * lineas.Max(l => Math.Max(1, l.Count)) + 5;
        y = Espacio(pdf, y, alto);
        var x = Margen;
        for (var i = 0; i < celdas.Length; i++)
        {
            var linea = y - 10;
            foreach (var texto in lineas[i])
            {
                pdf.Texto(x + 2, linea, fuente, tamano, texto, encabezado ? 0.25 : 0);
                linea -= 11;
            }

            x += celdas[i].Ancho;
        }

        y -= alto;
        pdf.Linea(Margen, y + 2, Ancho - Margen, y + 2, encabezado ? 0.5 : 0.15);
        return y;
    }

    private static double Espacio(LienzoPdf pdf, double y, double necesario)
    {
        if (y - necesario >= Margen + 20)
            return y;
        pdf.NuevaPagina();
        return Alto - Margen;
    }
}
