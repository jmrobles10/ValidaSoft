using System.Globalization;
using System.Text;
using System.Text.RegularExpressions;
using System.Xml;
using System.Xml.Linq;
using Validasoft.Infraestructura.Certificados;

namespace Validasoft.Infraestructura.Ficha;

/// <summary>
/// Un SVG (el logo de una empresa) leído y listo para dibujarse como vectores en un PDF, sin librerías: formas básicas (<c>rect</c>,
/// <c>circle</c>, <c>ellipse</c>, <c>line</c>, <c>polyline</c>, <c>polygon</c>) y trazados (<c>path</c> con todos sus comandos, arcos
/// incluidos), grupos y <c>use</c>, transformaciones, colores sólidos y degradados lineales, trazos (ancho, extremos, uniones, guiones),
/// opacidad y las hojas de estilo sencillas (<c>.clase</c>, <c>etiqueta</c>, <c>#id</c>) que dejan los editores. No se leen <c>text</c>,
/// <c>image</c> ni <c>foreignObject</c>: un SVG que los usa no se puede dibujar fielmente y <see cref="Leer"/> devuelve nulo (quien lo
/// pide elige con qué lo reemplaza). Recortes, máscaras y filtros se ignoran (se dibuja sin ellos). El XML se lee sin DTD ni resolutor
/// externo y con topes de tamaño y de elementos.
/// </summary>
internal sealed partial class SvgDibujable
{
    private const int MaximoElementos = 5_000;
    private const int MaximoSegmentos = 60_000;
    private const int MaximaProfundidad = 24;
    private const double Kappa = 0.5522847498307936;

    private static readonly CultureInfo Invariante = CultureInfo.InvariantCulture;

    private static readonly HashSet<string> Heredables =
    [
        "fill", "stroke", "stroke-width", "fill-rule", "fill-opacity", "stroke-opacity", "stroke-linecap", "stroke-linejoin", "stroke-miterlimit",
        "stroke-dasharray", "visibility", "color"
    ];

    private static readonly HashSet<string> Presentacion = [.. Heredables, "opacity", "display", "stop-color", "stop-opacity"];

    private static readonly HashSet<string> NoSeDibujan =
    [
        "defs", "style", "title", "desc", "metadata", "clipPath", "mask", "filter", "pattern", "marker", "symbol", "linearGradient", "radialGradient", "script", "switch"
    ];

    private static readonly Dictionary<string, Color> Nombres = new(StringComparer.OrdinalIgnoreCase)
    {
        ["black"] = new(0, 0, 0), ["white"] = new(255, 255, 255), ["red"] = new(255, 0, 0), ["green"] = new(0, 128, 0), ["blue"] = new(0, 0, 255),
        ["yellow"] = new(255, 255, 0), ["orange"] = new(255, 165, 0), ["gray"] = new(128, 128, 128), ["grey"] = new(128, 128, 128),
        ["silver"] = new(192, 192, 192), ["navy"] = new(0, 0, 128), ["maroon"] = new(128, 0, 0), ["purple"] = new(128, 0, 128), ["teal"] = new(0, 128, 128),
        ["lime"] = new(0, 255, 0), ["aqua"] = new(0, 255, 255), ["cyan"] = new(0, 255, 255), ["fuchsia"] = new(255, 0, 255), ["magenta"] = new(255, 0, 255),
        ["olive"] = new(128, 128, 0), ["brown"] = new(165, 42, 42), ["pink"] = new(255, 192, 203), ["gold"] = new(255, 215, 0)
    };

    private readonly Grupo _raiz;
    private readonly double _origenX;
    private readonly double _origenY;

    private SvgDibujable(Grupo raiz, double origenX, double origenY, double ancho, double alto)
    {
        _raiz = raiz;
        _origenX = origenX;
        _origenY = origenY;
        Ancho = ancho;
        Alto = alto;
    }

    /// <summary>Medidas del lienzo del SVG (su <c>viewBox</c>, o su ancho y alto): dan la proporción con que se dibuja.</summary>
    public double Ancho { get; }

    public double Alto { get; }

    /// <summary>El SVG listo para dibujar, o nulo si no es un SVG legible, usa texto o imágenes, o no dibuja nada.</summary>
    public static SvgDibujable? Leer(string svg)
    {
        XDocument documento;
        try
        {
            using var lector = XmlReader.Create(new StringReader(svg.TrimStart('﻿')),
                new XmlReaderSettings { DtdProcessing = DtdProcessing.Prohibit, XmlResolver = null, MaxCharactersInDocument = 2_000_000 });
            documento = XDocument.Load(lector);
        }
        catch (XmlException)
        {
            return null;
        }

        if (documento.Root is not { } raiz || raiz.Name.LocalName != "svg")
            return null;

        double x = 0, y = 0, ancho, alto;
        var vista = Numeros(raiz.Attribute("viewBox")?.Value);
        if (vista.Count == 4 && vista[2] > 0 && vista[3] > 0)
        {
            (x, y, ancho, alto) = (vista[0], vista[1], vista[2], vista[3]);
        }
        else
        {
            (ancho, alto) = (Longitud(raiz.Attribute("width")?.Value), Longitud(raiz.Attribute("height")?.Value));
            if (ancho <= 0 || alto <= 0)
                return null;
        }

        var lectura = new Lectura(raiz);
        var hijos = lectura.Hijos(raiz, lectura.PropiedadesDeLaRaiz(), 1.0, 0);
        return lectura.Inservible || hijos.Count == 0 ? null : new SvgDibujable(new Grupo(Identidad, hijos), x, y, ancho, alto);
    }

    /// <summary>Dibuja el SVG dentro del rectángulo (esquina inferior izquierda en (<paramref name="x"/>, <paramref name="y"/>)), centrado y sin deformarlo.</summary>
    public void Dibujar(LienzoPdf pdf, double x, double y, double ancho, double alto)
    {
        var escala = Math.Min(ancho / Ancho, alto / Alto);
        var izquierda = x + (ancho - Ancho * escala) / 2;
        var arriba = y + (alto + Alto * escala) / 2;
        double[] inicial = [escala, 0, 0, -escala, izquierda - _origenX * escala, arriba + _origenY * escala];
        var salida = new StringBuilder();
        salida.Append("q ").Append(Matriz(inicial)).Append(" cm\n");
        foreach (var hijo in _raiz.Hijos)
            Emitir(salida, pdf, hijo, inicial);
        salida.Append("Q\n");
        pdf.Crudo(salida.ToString());
    }

    // ---------- Emisión ----------

    private static void Emitir(StringBuilder salida, LienzoPdf pdf, Nodo nodo, double[] acumulada)
    {
        var matriz = Mult(nodo.Matriz, acumulada);
        salida.Append("q\n");
        if (!EsIdentidad(nodo.Matriz))
            salida.Append(Matriz(nodo.Matriz)).Append(" cm\n");
        switch (nodo)
        {
            case Grupo grupo:
                foreach (var hijo in grupo.Hijos)
                    Emitir(salida, pdf, hijo, matriz);
                break;
            case Trazado trazado:
                Pintar(salida, pdf, trazado, matriz);
                break;
        }

        salida.Append("Q\n");
    }

    private static void Pintar(StringBuilder salida, LienzoPdf pdf, Trazado trazado, double[] matriz)
    {
        var estilo = trazado.Estilo;
        var relleno = estilo.Relleno;
        var trazo = estilo.Trazo;
        if (relleno is null && trazo is null)
            return;

        if (estilo.OpacidadRelleno < 1 || estilo.OpacidadTrazo < 1)
            salida.Append('/').Append(pdf.Opacidad(estilo.OpacidadRelleno, estilo.OpacidadTrazo)).Append(" gs\n");

        switch (relleno)
        {
            case PinturaSolida solida:
                salida.Append(Rgb(solida.Color)).Append(" rg\n");
                break;
            case PinturaDegradada degradada:
                var caja = Caja(trazado.Segmentos);
                if (caja is { } c && c.Ancho > 1e-9 && c.Alto > 1e-9)
                {
                    var (x1, y1, x2, y2) = degradada.EnUsuario
                        ? (degradada.X1, degradada.Y1, degradada.X2, degradada.Y2)
                        : (c.X + degradada.X1 * c.Ancho, c.Y + degradada.Y1 * c.Alto, c.X + degradada.X2 * c.Ancho, c.Y + degradada.Y2 * c.Alto);
                    var patron = pdf.PatronAxial(matriz, x1, y1, x2, y2, [.. degradada.Paradas.Select(p => (p.Posicion, p.Color.R, p.Color.G, p.Color.B))]);
                    salida.Append("/Pattern cs /").Append(patron).Append(" scn\n");
                }
                else
                {
                    salida.Append(Rgb(degradada.Paradas[0].Color)).Append(" rg\n");
                }

                break;
        }

        if (trazo is not null)
        {
            var color = trazo switch { PinturaSolida s => s.Color, PinturaDegradada d => d.Paradas[0].Color, _ => new Color(0, 0, 0) };
            salida.Append(Rgb(color)).Append(" RG\n")
                .Append(LienzoPdf.Preciso(estilo.AnchoTrazo)).Append(" w ").Append(estilo.Extremo).Append(" J ").Append(estilo.Union).Append(" j ")
                .Append(LienzoPdf.Preciso(estilo.Inglete)).Append(" M\n");
            if (estilo.Guiones is { Length: > 0 } guiones)
                salida.Append('[').Append(string.Join(' ', guiones.Select(LienzoPdf.Preciso))).Append("] 0 d\n");
        }

        foreach (var segmento in trazado.Segmentos)
        {
            switch (segmento.Orden)
            {
                case 'M':
                    salida.Append(Par(segmento.V, 0)).Append(" m\n");
                    break;
                case 'L':
                    salida.Append(Par(segmento.V, 0)).Append(" l\n");
                    break;
                case 'C':
                    salida.Append(Par(segmento.V, 0)).Append(' ').Append(Par(segmento.V, 2)).Append(' ').Append(Par(segmento.V, 4)).Append(" c\n");
                    break;
                default:
                    salida.Append("h\n");
                    break;
            }
        }

        salida.Append((relleno, trazo, estilo.ParImpar) switch
        {
            (not null, not null, false) => "B\n",
            (not null, not null, true) => "B*\n",
            (not null, null, false) => "f\n",
            (not null, null, true) => "f*\n",
            (null, not null, _) => "S\n",
            _ => "n\n"
        });
    }

    private static string Par(double[] v, int i) => $"{LienzoPdf.Preciso(v[i])} {LienzoPdf.Preciso(v[i + 1])}";

    private static string Rgb(Color c) => $"{LienzoPdf.Preciso(c.R / 255.0)} {LienzoPdf.Preciso(c.G / 255.0)} {LienzoPdf.Preciso(c.B / 255.0)}";

    private static string Matriz(double[] m) => string.Join(' ', m.Select(LienzoPdf.Preciso));

    // ---------- Álgebra de matrices ([a b c d e f], como en SVG y PDF) ----------

    private static readonly double[] Identidad = [1, 0, 0, 1, 0, 0];

    private static bool EsIdentidad(double[] m) => m.AsSpan().SequenceEqual(Identidad);

    /// <summary>La transformación que aplica <paramref name="a"/> y después <paramref name="b"/>.</summary>
    private static double[] Mult(double[] a, double[] b) =>
    [
        a[0] * b[0] + a[1] * b[2], a[0] * b[1] + a[1] * b[3], a[2] * b[0] + a[3] * b[2], a[2] * b[1] + a[3] * b[3],
        a[4] * b[0] + a[5] * b[2] + b[4], a[4] * b[1] + a[5] * b[3] + b[5]
    ];

    private static double[] Transformacion(string? texto)
    {
        var resultado = Identidad;
        if (string.IsNullOrWhiteSpace(texto))
            return resultado;
        foreach (Match m in RegexTransformacion().Matches(texto))
        {
            var n = Numeros(m.Groups[2].Value);
            double[]? paso = (m.Groups[1].Value, n.Count) switch
            {
                ("matrix", 6) => [n[0], n[1], n[2], n[3], n[4], n[5]],
                ("translate", 1) => [1, 0, 0, 1, n[0], 0],
                ("translate", 2) => [1, 0, 0, 1, n[0], n[1]],
                ("scale", 1) => [n[0], 0, 0, n[0], 0, 0],
                ("scale", 2) => [n[0], 0, 0, n[1], 0, 0],
                ("rotate", 1) => Giro(n[0], 0, 0),
                ("rotate", 3) => Giro(n[0], n[1], n[2]),
                ("skewX", 1) => [1, 0, Math.Tan(n[0] * Math.PI / 180), 1, 0, 0],
                ("skewY", 1) => [1, Math.Tan(n[0] * Math.PI / 180), 0, 1, 0, 0],
                _ => null
            };
            if (paso is not null)
                resultado = Mult(paso, resultado);
        }

        return resultado;
    }

    private static double[] Giro(double grados, double cx, double cy)
    {
        var (s, c) = Math.SinCos(grados * Math.PI / 180);
        return [c, s, -s, c, cx - c * cx + s * cy, cy - s * cx - c * cy];
    }

    [GeneratedRegex(@"(matrix|translate|scale|rotate|skewX|skewY)\s*\(([^)]*)\)")]
    private static partial Regex RegexTransformacion();

    [GeneratedRegex(@"[-+]?(?:\d+\.?\d*|\.\d+)(?:[eE][-+]?\d+)?")]
    private static partial Regex RegexNumero();

    [GeneratedRegex(@"([^{}]+)\{([^{}]*)\}")]
    private static partial Regex RegexRegla();

    [GeneratedRegex(@"/\*.*?\*/", RegexOptions.Singleline)]
    private static partial Regex RegexComentario();

    private static List<double> Numeros(string? texto) =>
        texto is null ? [] : [.. RegexNumero().Matches(texto).Select(m => double.Parse(m.Value, Invariante))];

    /// <summary>Primer número del texto (con unidad o sin ella); 0 si no hay o es un porcentaje.</summary>
    private static double Longitud(string? texto)
    {
        if (texto is null || texto.Contains('%', StringComparison.Ordinal))
            return 0;
        var n = Numeros(texto);
        return n.Count > 0 ? n[0] : 0;
    }

    // ---------- Modelo ----------

    private readonly record struct Color(byte R, byte G, byte B);

    private abstract record Pintura;

    private sealed record PinturaSolida(Color Color) : Pintura;

    private sealed record PinturaDegradada(IReadOnlyList<(double Posicion, Color Color)> Paradas, double X1, double Y1, double X2, double Y2, bool EnUsuario) : Pintura;

    private sealed record Estilo(Pintura? Relleno, Pintura? Trazo, double AnchoTrazo, bool ParImpar, double OpacidadRelleno, double OpacidadTrazo, int Extremo, int Union,
        double Inglete, double[]? Guiones);

    /// <summary>Un comando de trazado ya absoluto: <c>M</c> y <c>L</c> (2 valores), <c>C</c> (6) o <c>Z</c>.</summary>
    private readonly record struct Segmento(char Orden, double[] V);

    private abstract record Nodo(double[] Matriz);

    private sealed record Grupo(double[] Matriz, List<Nodo> Hijos) : Nodo(Matriz);

    private sealed record Trazado(double[] Matriz, Estilo Estilo, List<Segmento> Segmentos) : Nodo(Matriz);

    private readonly record struct Caja2(double X, double Y, double Ancho, double Alto);

    private static Caja2? Caja(List<Segmento> segmentos)
    {
        double minX = double.MaxValue, minY = double.MaxValue, maxX = double.MinValue, maxY = double.MinValue;
        foreach (var s in segmentos)
        {
            for (var i = 0; i + 1 < s.V.Length; i += 2)
            {
                minX = Math.Min(minX, s.V[i]);
                maxX = Math.Max(maxX, s.V[i]);
                minY = Math.Min(minY, s.V[i + 1]);
                maxY = Math.Max(maxY, s.V[i + 1]);
            }
        }

        return minX > maxX ? null : new Caja2(minX, minY, maxX - minX, maxY - minY);
    }

    // ---------- Lectura ----------

    private sealed record Regla(string? Etiqueta, string? Clase, string? Id, Dictionary<string, string> Declaraciones);

    /// <summary>El estado de una lectura: ids, reglas de estilo, contadores y si apareció algo que no se puede dibujar.</summary>
    private sealed class Lectura
    {
        private readonly Dictionary<string, XElement> _ids = new();
        private readonly List<Regla> _reglas = [];
        private readonly XElement _raiz;
        private int _elementos;
        private int _segmentos;

        public Lectura(XElement raiz)
        {
            _raiz = raiz;
            foreach (var e in raiz.DescendantsAndSelf())
            {
                if (e.Attribute("id")?.Value is { Length: > 0 } id)
                    _ids[id] = e;
                if (e.Name.LocalName == "style")
                    LeerReglas(e.Value);
            }
        }

        /// <summary>Apareció texto o una imagen incrustada, o el SVG excede los topes: no se puede dibujar fielmente.</summary>
        public bool Inservible { get; private set; }

        public Dictionary<string, string> PropiedadesDeLaRaiz() => Heredadas(Propias(_raiz), new Dictionary<string, string>());

        private void LeerReglas(string css)
        {
            foreach (Match m in RegexRegla().Matches(RegexComentario().Replace(css, string.Empty)))
            {
                var declaraciones = Declaraciones(m.Groups[2].Value);
                foreach (var selector in m.Groups[1].Value.Split(',', StringSplitOptions.TrimEntries | StringSplitOptions.RemoveEmptyEntries))
                {
                    var partes = Regex.Match(selector, @"^([A-Za-z][\w-]*)?(?:\.([\w-]+))?(?:#([\w-]+))?$");
                    if (partes.Success && partes.Value.Length > 0)
                        _reglas.Add(new Regla(partes.Groups[1].Success ? partes.Groups[1].Value : null, partes.Groups[2].Success ? partes.Groups[2].Value : null,
                            partes.Groups[3].Success ? partes.Groups[3].Value : null, declaraciones));
                }
            }
        }

        private static Dictionary<string, string> Declaraciones(string texto)
        {
            var resultado = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var declaracion in texto.Split(';', StringSplitOptions.RemoveEmptyEntries))
            {
                var dos = declaracion.IndexOf(':', StringComparison.Ordinal);
                if (dos <= 0)
                    continue;
                var clave = declaracion[..dos].Trim().ToLowerInvariant();
                var valor = declaracion[(dos + 1)..].Replace("!important", string.Empty, StringComparison.OrdinalIgnoreCase).Trim();
                if (Presentacion.Contains(clave))
                    resultado[clave] = valor;
            }

            return resultado;
        }

        /// <summary>Las propiedades que el elemento declara: atributos de presentación, luego la hoja de estilo y al final su <c>style</c>.</summary>
        private Dictionary<string, string> Propias(XElement e)
        {
            var propias = new Dictionary<string, string>(StringComparer.Ordinal);
            foreach (var a in e.Attributes().Where(a => a.Name.NamespaceName.Length == 0 && Presentacion.Contains(a.Name.LocalName)))
                propias[a.Name.LocalName] = a.Value.Trim();
            var clases = (e.Attribute("class")?.Value ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries);
            var id = e.Attribute("id")?.Value;
            foreach (var regla in _reglas.Where(r => (r.Etiqueta is null || r.Etiqueta == e.Name.LocalName) && (r.Clase is null || clases.Contains(r.Clase)) && (r.Id is null || r.Id == id)))
            {
                foreach (var (clave, valor) in regla.Declaraciones)
                    propias[clave] = valor;
            }

            if (e.Attribute("style")?.Value is { } estilo)
            {
                foreach (var (clave, valor) in Declaraciones(estilo))
                    propias[clave] = valor;
            }

            return propias;
        }

        private static Dictionary<string, string> Heredadas(Dictionary<string, string> propias, Dictionary<string, string> padre)
        {
            var resultado = new Dictionary<string, string>(padre, StringComparer.Ordinal);
            foreach (var (clave, valor) in propias.Where(p => Heredables.Contains(p.Key) && !string.Equals(p.Value, "inherit", StringComparison.Ordinal)))
                resultado[clave] = valor;
            return resultado;
        }

        public List<Nodo> Hijos(XElement padre, Dictionary<string, string> heredadas, double opacidad, int profundidad)
        {
            var nodos = new List<Nodo>();
            foreach (var hijo in padre.Elements())
            {
                if (Recorrer(hijo, heredadas, opacidad, profundidad + 1) is { } nodo)
                    nodos.Add(nodo);
            }

            return nodos;
        }

        private Nodo? Recorrer(XElement e, Dictionary<string, string> padre, double opacidadPadre, int profundidad)
        {
            var nombre = e.Name.LocalName;
            if (nombre is "text" or "tspan" or "image" or "foreignObject")
            {
                Inservible = true;
                return null;
            }

            if (NoSeDibujan.Contains(nombre))
                return null;
            if (profundidad > MaximaProfundidad || ++_elementos > MaximoElementos)
            {
                Inservible = true;
                return null;
            }

            var propias = Propias(e);
            if (propias.GetValueOrDefault("display") == "none" || propias.GetValueOrDefault("visibility") is "hidden" or "collapse")
                return null;
            var heredadas = Heredadas(propias, padre);
            var opacidad = opacidadPadre * Fraccion(propias.GetValueOrDefault("opacity"));
            var matriz = Transformacion(e.Attribute("transform")?.Value);

            switch (nombre)
            {
                case "g" or "a" or "svg":
                    return new Grupo(matriz, Hijos(e, heredadas, opacidad, profundidad));
                case "use":
                    return Usar(e, matriz, heredadas, opacidad, profundidad);
            }

            var segmentos = Forma(e, nombre);
            if (segmentos is null || segmentos.Count == 0)
                return null;
            if ((_segmentos += segmentos.Count) > MaximoSegmentos)
            {
                Inservible = true;
                return null;
            }

            return new Trazado(matriz, ResolverEstilo(heredadas, opacidad), segmentos);
        }

        private Nodo? Usar(XElement e, double[] matriz, Dictionary<string, string> heredadas, double opacidad, int profundidad)
        {
            var enlace = (e.Attribute("href") ?? e.Attributes().FirstOrDefault(a => a.Name.LocalName == "href"))?.Value;
            if (enlace is not { Length: > 1 } || enlace[0] != '#' || !_ids.TryGetValue(enlace[1..], out var destino) || destino == _raiz)
                return null;
            double[] desplazado = [1, 0, 0, 1, Numero(e, "x"), Numero(e, "y")];
            return Recorrer(destino, heredadas, opacidad, profundidad + 1) is { } nodo ? new Grupo(Mult(desplazado, matriz), [nodo]) : null;
        }

        private static double Numero(XElement e, string atributo, double porDefecto = 0) =>
            e.Attribute(atributo)?.Value is { } texto && Numeros(texto) is { Count: > 0 } n ? n[0] : porDefecto;

        // ---------- Formas ----------

        private static List<Segmento>? Forma(XElement e, string nombre)
        {
            switch (nombre)
            {
                case "path":
                    return Trazo(e.Attribute("d")?.Value ?? string.Empty);
                case "rect":
                    return Rectangulo(Numero(e, "x"), Numero(e, "y"), Numero(e, "width"), Numero(e, "height"), e.Attribute("rx") is null ? null : Numero(e, "rx"),
                        e.Attribute("ry") is null ? null : Numero(e, "ry"));
                case "circle":
                    return Elipse(Numero(e, "cx"), Numero(e, "cy"), Numero(e, "r"), Numero(e, "r"));
                case "ellipse":
                    return Elipse(Numero(e, "cx"), Numero(e, "cy"), Numero(e, "rx"), Numero(e, "ry"));
                case "line":
                    return [new Segmento('M', [Numero(e, "x1"), Numero(e, "y1")]), new Segmento('L', [Numero(e, "x2"), Numero(e, "y2")])];
                case "polyline" or "polygon":
                    var puntos = Numeros(e.Attribute("points")?.Value);
                    if (puntos.Count < 4)
                        return null;
                    var segmentos = new List<Segmento>();
                    for (var i = 0; i + 1 < puntos.Count; i += 2)
                        segmentos.Add(new Segmento(i == 0 ? 'M' : 'L', [puntos[i], puntos[i + 1]]));
                    if (nombre == "polygon")
                        segmentos.Add(new Segmento('Z', []));
                    return segmentos;
                default:
                    return null;
            }
        }

        private static List<Segmento>? Rectangulo(double x, double y, double ancho, double alto, double? rx, double? ry)
        {
            if (ancho <= 0 || alto <= 0)
                return null;
            var radioX = Math.Min(Math.Max(rx ?? ry ?? 0, 0), ancho / 2);
            var radioY = Math.Min(Math.Max(ry ?? rx ?? 0, 0), alto / 2);
            if (radioX == 0 || radioY == 0)
                return
                [
                    new Segmento('M', [x, y]), new Segmento('L', [x + ancho, y]), new Segmento('L', [x + ancho, y + alto]), new Segmento('L', [x, y + alto]), new Segmento('Z', [])
                ];

            var (kx, ky) = (radioX * Kappa, radioY * Kappa);
            var (x2, y2) = (x + ancho, y + alto);
            return
            [
                new Segmento('M', [x + radioX, y]),
                new Segmento('L', [x2 - radioX, y]),
                new Segmento('C', [x2 - radioX + kx, y, x2, y + radioY - ky, x2, y + radioY]),
                new Segmento('L', [x2, y2 - radioY]),
                new Segmento('C', [x2, y2 - radioY + ky, x2 - radioX + kx, y2, x2 - radioX, y2]),
                new Segmento('L', [x + radioX, y2]),
                new Segmento('C', [x + radioX - kx, y2, x, y2 - radioY + ky, x, y2 - radioY]),
                new Segmento('L', [x, y + radioY]),
                new Segmento('C', [x, y + radioY - ky, x + radioX - kx, y, x + radioX, y]),
                new Segmento('Z', [])
            ];
        }

        private static List<Segmento>? Elipse(double cx, double cy, double rx, double ry)
        {
            if (rx <= 0 || ry <= 0)
                return null;
            var (kx, ky) = (rx * Kappa, ry * Kappa);
            return
            [
                new Segmento('M', [cx + rx, cy]),
                new Segmento('C', [cx + rx, cy + ky, cx + kx, cy + ry, cx, cy + ry]),
                new Segmento('C', [cx - kx, cy + ry, cx - rx, cy + ky, cx - rx, cy]),
                new Segmento('C', [cx - rx, cy - ky, cx - kx, cy - ry, cx, cy - ry]),
                new Segmento('C', [cx + kx, cy - ry, cx + rx, cy - ky, cx + rx, cy]),
                new Segmento('Z', [])
            ];
        }

        // ---------- Trazados (atributo d) ----------

        /// <summary>Convierte el atributo <c>d</c> a comandos absolutos M, L, C y Z. Un error de sintaxis conserva lo leído hasta ahí (como los navegadores).</summary>
        private static List<Segmento> Trazo(string d)
        {
            var s = new List<Segmento>();
            var p = 0;
            double x = 0, y = 0, inicioX = 0, inicioY = 0, controlX = 0, controlY = 0;
            var orden = ' ';
            var anterior = ' ';

            bool Saltar()
            {
                while (p < d.Length && (char.IsWhiteSpace(d[p]) || d[p] == ','))
                    p++;
                return p < d.Length;
            }

            bool Numero(out double valor)
            {
                valor = 0;
                if (!Saltar())
                    return false;
                var m = RegexNumero().Match(d, p);
                if (!m.Success || m.Index != p)
                    return false;
                p += m.Length;
                valor = double.Parse(m.Value, Invariante);
                return true;
            }

            bool Bandera(out bool valor)
            {
                valor = false;
                if (!Saltar() || d[p] is not ('0' or '1'))
                    return false;
                valor = d[p++] == '1';
                return true;
            }

            while (Saltar() && s.Count < MaximoSegmentos)
            {
                if (char.IsLetter(d[p]))
                    orden = d[p++];
                else if (orden is ' ' or 'Z' or 'z')
                    break;

                var relativa = char.IsLower(orden);
                var (dx, dy) = relativa ? (x, y) : (0.0, 0.0);
                switch (char.ToUpperInvariant(orden))
                {
                    case 'M':
                        if (!Numero(out var mx) || !Numero(out var my))
                            return s;
                        (x, y) = (mx + dx, my + dy);
                        (inicioX, inicioY) = (x, y);
                        s.Add(new Segmento('M', [x, y]));
                        orden = relativa ? 'l' : 'L';
                        break;
                    case 'L':
                        if (!Numero(out var lx) || !Numero(out var ly))
                            return s;
                        (x, y) = (lx + dx, ly + dy);
                        s.Add(new Segmento('L', [x, y]));
                        break;
                    case 'H':
                        if (!Numero(out var hx))
                            return s;
                        x = hx + dx;
                        s.Add(new Segmento('L', [x, y]));
                        break;
                    case 'V':
                        if (!Numero(out var vy))
                            return s;
                        y = vy + dy;
                        s.Add(new Segmento('L', [x, y]));
                        break;
                    case 'C':
                        if (!Numero(out var c1x) || !Numero(out var c1y) || !Numero(out var c2x) || !Numero(out var c2y) || !Numero(out var cx) || !Numero(out var cy))
                            return s;
                        s.Add(new Segmento('C', [c1x + dx, c1y + dy, c2x + dx, c2y + dy, cx + dx, cy + dy]));
                        (controlX, controlY) = (c2x + dx, c2y + dy);
                        (x, y) = (cx + dx, cy + dy);
                        break;
                    case 'S':
                        if (!Numero(out var sx2) || !Numero(out var sy2) || !Numero(out var sx) || !Numero(out var sy))
                            return s;
                        var (r1x, r1y) = char.ToUpperInvariant(anterior) is 'C' or 'S' ? (2 * x - controlX, 2 * y - controlY) : (x, y);
                        s.Add(new Segmento('C', [r1x, r1y, sx2 + dx, sy2 + dy, sx + dx, sy + dy]));
                        (controlX, controlY) = (sx2 + dx, sy2 + dy);
                        (x, y) = (sx + dx, sy + dy);
                        break;
                    case 'Q':
                        if (!Numero(out var qx) || !Numero(out var qy) || !Numero(out var ex) || !Numero(out var ey))
                            return s;
                        (controlX, controlY) = (qx + dx, qy + dy);
                        Cuadratica(s, x, y, controlX, controlY, ex + dx, ey + dy);
                        (x, y) = (ex + dx, ey + dy);
                        break;
                    case 'T':
                        if (!Numero(out var tx) || !Numero(out var ty))
                            return s;
                        (controlX, controlY) = char.ToUpperInvariant(anterior) is 'Q' or 'T' ? (2 * x - controlX, 2 * y - controlY) : (x, y);
                        Cuadratica(s, x, y, controlX, controlY, tx + dx, ty + dy);
                        (x, y) = (tx + dx, ty + dy);
                        break;
                    case 'A':
                        if (!Numero(out var rx) || !Numero(out var ry) || !Numero(out var giro) || !Bandera(out var grande) || !Bandera(out var barrido) || !Numero(out var ax) || !Numero(out var ay))
                            return s;
                        Arco(s, x, y, rx, ry, giro, grande, barrido, ax + dx, ay + dy);
                        (x, y) = (ax + dx, ay + dy);
                        break;
                    case 'Z':
                        s.Add(new Segmento('Z', []));
                        (x, y) = (inicioX, inicioY);
                        break;
                    default:
                        return s;
                }

                anterior = orden;
            }

            return s;
        }

        private static void Cuadratica(List<Segmento> s, double x0, double y0, double qx, double qy, double x, double y) =>
            s.Add(new Segmento('C', [x0 + 2.0 / 3 * (qx - x0), y0 + 2.0 / 3 * (qy - y0), x + 2.0 / 3 * (qx - x), y + 2.0 / 3 * (qy - y), x, y]));

        /// <summary>Arco elíptico de SVG (apéndice F.6) como curvas de Bézier de a lo más un cuarto de vuelta.</summary>
        private static void Arco(List<Segmento> s, double x0, double y0, double rx, double ry, double giroGrados, bool grande, bool barrido, double x, double y)
        {
            if (x0 == x && y0 == y)
                return;
            (rx, ry) = (Math.Abs(rx), Math.Abs(ry));
            if (rx == 0 || ry == 0)
            {
                s.Add(new Segmento('L', [x, y]));
                return;
            }

            var (sen, cos) = Math.SinCos(giroGrados * Math.PI / 180);
            var (mx, my) = ((x0 - x) / 2, (y0 - y) / 2);
            var (x1, y1) = (cos * mx + sen * my, -sen * mx + cos * my);
            var lambda = x1 * x1 / (rx * rx) + y1 * y1 / (ry * ry);
            if (lambda > 1)
            {
                var k = Math.Sqrt(lambda);
                (rx, ry) = (rx * k, ry * k);
            }

            var numerador = rx * rx * ry * ry - rx * rx * y1 * y1 - ry * ry * x1 * x1;
            var denominador = rx * rx * y1 * y1 + ry * ry * x1 * x1;
            var coeficiente = Math.Sqrt(Math.Max(0, numerador / denominador)) * (grande == barrido ? -1 : 1);
            var (cxp, cyp) = (coeficiente * rx * y1 / ry, -coeficiente * ry * x1 / rx);
            var (cx, cy) = (cos * cxp - sen * cyp + (x0 + x) / 2, sen * cxp + cos * cyp + (y0 + y) / 2);
            var theta = Angulo(1, 0, (x1 - cxp) / rx, (y1 - cyp) / ry);
            var delta = Angulo((x1 - cxp) / rx, (y1 - cyp) / ry, (-x1 - cxp) / rx, (-y1 - cyp) / ry);
            if (!barrido && delta > 0)
                delta -= 2 * Math.PI;
            else if (barrido && delta < 0)
                delta += 2 * Math.PI;

            var tramos = Math.Max(1, (int)Math.Ceiling(Math.Abs(delta) / (Math.PI / 2) - 1e-9));
            var paso = delta / tramos;
            var t = 4.0 / 3 * Math.Tan(paso / 4);
            double[] Punto(double ux, double uy) => [cx + rx * ux * cos - ry * uy * sen, cy + rx * ux * sen + ry * uy * cos];
            for (var i = 0; i < tramos; i++)
            {
                var (a1, a2) = (theta + i * paso, theta + (i + 1) * paso);
                var (s1, c1) = Math.SinCos(a1);
                var (s2, c2) = Math.SinCos(a2);
                double[] p1 = Punto(c1 - t * s1, s1 + t * c1), p2 = Punto(c2 + t * s2, s2 - t * c2), p3 = i == tramos - 1 ? [x, y] : Punto(c2, s2);
                s.Add(new Segmento('C', [p1[0], p1[1], p2[0], p2[1], p3[0], p3[1]]));
            }
        }

        private static double Angulo(double ux, double uy, double vx, double vy)
        {
            var coseno = (ux * vx + uy * vy) / (Math.Sqrt(ux * ux + uy * uy) * Math.Sqrt(vx * vx + vy * vy));
            var angulo = Math.Acos(Math.Clamp(coseno, -1, 1));
            return ux * vy - uy * vx < 0 ? -angulo : angulo;
        }

        // ---------- Estilo ----------

        private Estilo ResolverEstilo(Dictionary<string, string> p, double opacidad)
        {
            var relleno = LeerPintura(p.GetValueOrDefault("fill", "black"), p);
            var trazo = LeerPintura(p.GetValueOrDefault("stroke", "none"), p);
            var ancho = p.TryGetValue("stroke-width", out var w) ? Longitud(w) : 1;
            var extremo = p.GetValueOrDefault("stroke-linecap") switch { "round" => 1, "square" => 2, _ => 0 };
            var union = p.GetValueOrDefault("stroke-linejoin") switch { "round" => 1, "bevel" => 2, _ => 0 };
            var guiones = p.TryGetValue("stroke-dasharray", out var g) && g != "none" ? Numeros(g).Where(n => n >= 0).ToArray() : null;
            return new Estilo(relleno, trazo, ancho <= 0 ? 1 : ancho, p.GetValueOrDefault("fill-rule") == "evenodd",
                opacidad * Fraccion(p.GetValueOrDefault("fill-opacity")), opacidad * Fraccion(p.GetValueOrDefault("stroke-opacity")),
                extremo, union, p.TryGetValue("stroke-miterlimit", out var m) && Longitud(m) is > 0 and var limite ? limite : 4, guiones is { Length: > 0 } && guiones.Sum() > 0 ? guiones : null);
        }

        /// <summary>Un número de 0 a 1 (o un porcentaje); 1 si falta.</summary>
        private static double Fraccion(string? texto)
        {
            if (texto is null || Numeros(texto) is not { Count: > 0 } n)
                return 1;
            return Math.Clamp(texto.Contains('%', StringComparison.Ordinal) ? n[0] / 100 : n[0], 0, 1);
        }

        /// <summary>Un valor de <c>fill</c> o <c>stroke</c>: nulo si es «none» o no se entiende, color sólido o degradado.</summary>
        private Pintura? LeerPintura(string valor, Dictionary<string, string> p)
        {
            valor = valor.Trim();
            if (valor is "none" or "transparent" or "")
                return null;
            if (valor.StartsWith("url(", StringComparison.OrdinalIgnoreCase))
            {
                var cierre = valor.IndexOf(')', StringComparison.Ordinal);
                var id = cierre > 5 ? valor[4..cierre].Trim().Trim('\'', '"').TrimStart('#') : string.Empty;
                if (_ids.TryGetValue(id, out var degradado) && Degradado(degradado, 0) is { } pintura)
                    return pintura;
                var alternativa = cierre > 0 ? valor[(cierre + 1)..].Trim() : string.Empty;
                return alternativa.Length > 0 ? LeerPintura(alternativa, p) : null;
            }

            return LeerColor(valor, p) is { } color ? new PinturaSolida(color) : null;
        }

        private static Color? LeerColor(string valor, Dictionary<string, string> p)
        {
            if (valor.Equals("currentColor", StringComparison.OrdinalIgnoreCase))
                return p.TryGetValue("color", out var actual) && !actual.Equals("currentColor", StringComparison.OrdinalIgnoreCase) ? LeerColor(actual, p) : new Color(0, 0, 0);
            if (valor.StartsWith('#'))
            {
                var hex = valor[1..];
                if (hex.Length is 3 or 4)
                    hex = string.Concat(hex.Take(3).Select(c => $"{c}{c}"));
                else if (hex.Length == 8)
                    hex = hex[..6];
                return hex.Length == 6 && int.TryParse(hex, NumberStyles.HexNumber, Invariante, out var rgb)
                    ? new Color((byte)(rgb >> 16), (byte)(rgb >> 8), (byte)rgb)
                    : null;
            }

            if (valor.StartsWith("rgb", StringComparison.OrdinalIgnoreCase))
            {
                var n = Numeros(valor);
                if (n.Count < 3)
                    return null;
                var porcentaje = valor.Contains('%', StringComparison.Ordinal);
                byte Canal(double c) => (byte)Math.Clamp(Math.Round(porcentaje ? c * 2.55 : c), 0, 255);
                return new Color(Canal(n[0]), Canal(n[1]), Canal(n[2]));
            }

            return Nombres.TryGetValue(valor, out var nombrado) ? nombrado : null;
        }

        /// <summary>Un degradado lineal con sus paradas (siguiendo <c>href</c> si las paradas están en otro); los radiales se reducen a su primera parada.</summary>
        private PinturaDegradada? Degradado(XElement e, int profundidad)
        {
            if (profundidad > 4)
                return null;
            var paradas = new List<(double Posicion, Color Color)>();
            var origen = e;
            for (var i = 0; i < 4 && origen is not null; i++)
            {
                foreach (var stop in origen.Elements().Where(h => h.Name.LocalName == "stop"))
                {
                    var propias = Propias(stop);
                    var offset = Fraccion(stop.Attribute("offset")?.Value);
                    if (LeerColor(propias.GetValueOrDefault("stop-color", "black"), propias) is { } color)
                        paradas.Add((Math.Max(offset, paradas.Count > 0 ? paradas[^1].Posicion : 0), color));
                }

                if (paradas.Count > 0)
                    break;
                var href = (origen.Attribute("href") ?? origen.Attributes().FirstOrDefault(a => a.Name.LocalName == "href"))?.Value;
                origen = href is { Length: > 1 } && href[0] == '#' && _ids.TryGetValue(href[1..], out var siguiente) ? siguiente : null;
            }

            if (paradas.Count == 0)
                return null;
            if (paradas.Count == 1)
                paradas.Add((1, paradas[0].Color));
            if (paradas[0].Posicion > 0)
                paradas.Insert(0, (0, paradas[0].Color));
            if (paradas[^1].Posicion < 1)
                paradas.Add((1, paradas[^1].Color));

            var enUsuario = e.Attribute("gradientUnits")?.Value == "userSpaceOnUse";
            if (e.Name.LocalName != "linearGradient")
                return new PinturaDegradada([paradas[0], paradas[0]], 0, 0, 1, 0, false);
            double Coordenada(string nombre, double porDefecto) => e.Attribute(nombre)?.Value is { } t && Numeros(t) is { Count: > 0 } n
                ? (t.Contains('%', StringComparison.Ordinal) && !enUsuario ? n[0] / 100 : n[0])
                : porDefecto;
            return new PinturaDegradada(paradas, Coordenada("x1", 0), Coordenada("y1", 0), Coordenada("x2", enUsuario ? 0 : 1), Coordenada("y2", 0), enUsuario);
        }
    }
}
