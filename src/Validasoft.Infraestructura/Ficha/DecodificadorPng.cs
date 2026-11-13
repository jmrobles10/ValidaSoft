using System.IO.Compression;

namespace Validasoft.Infraestructura.Ficha;

/// <summary>Imagen ya decodificada: píxeles RGB de 8 bits, fila por fila, compuestos sobre fondo blanco.</summary>
internal sealed record ImagenRgb(int Ancho, int Alto, byte[] Rgb);

/// <summary>
/// Decodificador de PNG para los logos que va a dibujar un PDF (sin dependencias, solo la inflación de .NET): todos los tipos de color
/// (gris, RGB, paleta, con canal alfa), 1, 2, 4, 8 y 16 bits por canal, transparencia <c>tRNS</c> y entrelazado Adam7. La
/// transparencia se compone sobre blanco (el PDF se imprime sobre papel). Un archivo dañado, truncado o demasiado grande devuelve
/// nulo: quien lo pide elige con qué lo reemplaza. No comprueba los CRC (como el de la firma).
/// </summary>
internal static class DecodificadorPng
{
    /// <summary>Tope de píxeles (un logo de unos cientos de píxeles por lado: 2 000 × 2 000 ya es de sobra).</summary>
    public const int MaximoPixeles = 4_000_000;

    private static readonly byte[] Cabecera = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    // Pasos de Adam7: columna y fila de inicio y salto de cada pasada.
    private static readonly (int X, int Y, int DX, int DY)[] Pasadas = [(0, 0, 8, 8), (4, 0, 8, 8), (0, 4, 4, 8), (2, 0, 4, 4), (0, 2, 2, 4), (1, 0, 2, 2), (0, 1, 1, 2)];

    public static ImagenRgb? Decodificar(byte[] png)
    {
        try
        {
            return Leer(png);
        }
        catch (Exception e) when (e is InvalidDataException or IOException or IndexOutOfRangeException or ArgumentException or OverflowException)
        {
            return null;
        }
    }

    private static ImagenRgb? Leer(byte[] png)
    {
        if (png.Length < Cabecera.Length + 25 || !png.AsSpan(0, Cabecera.Length).SequenceEqual(Cabecera))
            return null;

        int ancho = 0, alto = 0, profundidad = 0, tipoColor = -1, entrelazado = 0;
        byte[]? paleta = null, transparencia = null;
        using var datos = new MemoryStream();
        var posicion = Cabecera.Length;
        var cabeceraVista = false;
        while (posicion + 12 <= png.Length)
        {
            var largo = Entero(png, posicion);
            var tipo = System.Text.Encoding.ASCII.GetString(png, posicion + 4, 4);
            var inicio = posicion + 8;
            if (largo < 0 || (long)inicio + largo + 4 > png.Length)
                return null;

            switch (tipo)
            {
                case "IHDR" when largo == 13:
                    ancho = Entero(png, inicio);
                    alto = Entero(png, inicio + 4);
                    profundidad = png[inicio + 8];
                    tipoColor = png[inicio + 9];
                    if (png[inicio + 10] != 0 || png[inicio + 11] != 0)
                        return null;
                    entrelazado = png[inicio + 12];
                    cabeceraVista = true;
                    break;
                case "PLTE":
                    paleta = png[inicio..(inicio + largo)];
                    break;
                case "tRNS":
                    transparencia = png[inicio..(inicio + largo)];
                    break;
                case "IDAT":
                    datos.Write(png, inicio, largo);
                    break;
            }

            posicion = inicio + largo + 4;
            if (tipo == "IEND")
                break;
        }

        if (!cabeceraVista || ancho < 1 || alto < 1 || entrelazado > 1 || (long)ancho * alto > MaximoPixeles || !ProfundidadValida(tipoColor, profundidad))
            return null;
        if (tipoColor == 3 && (paleta is null || paleta.Length == 0 || paleta.Length % 3 != 0))
            return null;

        var canales = tipoColor switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, _ => 4 };
        var bitsPorPixel = canales * profundidad;
        var bytesPorPixel = Math.Max(1, bitsPorPixel / 8);
        var pasadas = entrelazado == 1 ? Pasadas : [(0, 0, 1, 1)];

        // Tamaño que debe tener lo inflado: cada fila lleva un byte de filtro al inicio.
        long esperado = 0;
        foreach (var (px, py, dx, dy) in pasadas)
        {
            var (columnas, filas) = (Cuenta(ancho, px, dx), Cuenta(alto, py, dy));
            if (columnas > 0 && filas > 0)
                esperado += (long)filas * (1 + (columnas * bitsPorPixel + 7) / 8);
        }

        var crudo = Inflar(datos, esperado);
        if (crudo is null)
            return null;

        var rgb = new byte[ancho * alto * 3];
        var desplazamiento = 0;
        foreach (var (px, py, dx, dy) in pasadas)
        {
            var (columnas, filas) = (Cuenta(ancho, px, dx), Cuenta(alto, py, dy));
            if (columnas == 0 || filas == 0)
                continue;
            var bytesFila = (columnas * bitsPorPixel + 7) / 8;
            var anterior = new byte[bytesFila];
            var actual = new byte[bytesFila];
            for (var f = 0; f < filas; f++)
            {
                var filtro = crudo[desplazamiento];
                Buffer.BlockCopy(crudo, desplazamiento + 1, actual, 0, bytesFila);
                desplazamiento += 1 + bytesFila;
                if (!Desfiltrar(filtro, actual, anterior, bytesPorPixel))
                    return null;
                for (var c = 0; c < columnas; c++)
                {
                    var x = px + c * dx;
                    var y = py + f * dy;
                    Pixel(actual, c, tipoColor, profundidad, paleta, transparencia, rgb, (y * ancho + x) * 3);
                }

                (anterior, actual) = (actual, anterior);
            }
        }

        return new ImagenRgb(ancho, alto, rgb);
    }

    private static bool ProfundidadValida(int tipoColor, int profundidad) => tipoColor switch
    {
        0 => profundidad is 1 or 2 or 4 or 8 or 16,
        2 or 4 or 6 => profundidad is 8 or 16,
        3 => profundidad is 1 or 2 or 4 or 8,
        _ => false
    };

    private static int Cuenta(int total, int inicio, int salto) => total <= inicio ? 0 : (total - inicio + salto - 1) / salto;

    private static int Entero(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

    private static byte[]? Inflar(MemoryStream datos, long esperado)
    {
        if (esperado is <= 0 or > int.MaxValue)
            return null;
        var crudo = new byte[esperado];
        datos.Position = 0;
        using var inflado = new ZLibStream(datos, CompressionMode.Decompress);
        var leidos = 0;
        while (leidos < crudo.Length)
        {
            var n = inflado.Read(crudo, leidos, crudo.Length - leidos);
            if (n == 0)
                break;
            leidos += n;
        }

        return leidos < crudo.Length ? null : crudo;
    }

    private static bool Desfiltrar(byte filtro, byte[] actual, byte[] anterior, int bpp)
    {
        switch (filtro)
        {
            case 0:
                return true;
            case 1:
                for (var i = bpp; i < actual.Length; i++)
                    actual[i] = (byte)(actual[i] + actual[i - bpp]);
                return true;
            case 2:
                for (var i = 0; i < actual.Length; i++)
                    actual[i] = (byte)(actual[i] + anterior[i]);
                return true;
            case 3:
                for (var i = 0; i < actual.Length; i++)
                    actual[i] = (byte)(actual[i] + (((i >= bpp ? actual[i - bpp] : 0) + anterior[i]) >> 1));
                return true;
            case 4:
                for (var i = 0; i < actual.Length; i++)
                {
                    int a = i >= bpp ? actual[i - bpp] : 0, b = anterior[i], c = i >= bpp ? anterior[i - bpp] : 0;
                    var p = a + b - c;
                    int pa = Math.Abs(p - a), pb = Math.Abs(p - b), pc = Math.Abs(p - c);
                    actual[i] = (byte)(actual[i] + (pa <= pb && pa <= pc ? a : pb <= pc ? b : c));
                }

                return true;
            default:
                return false;
        }
    }

    /// <summary>La muestra número <paramref name="indice"/> de la fila (de 1 a 16 bits), sin escalar.</summary>
    private static int Muestra(byte[] fila, int indice, int profundidad)
    {
        switch (profundidad)
        {
            case 8:
                return fila[indice];
            case 16:
                return (fila[indice * 2] << 8) | fila[indice * 2 + 1];
            default:
                var bit = indice * profundidad;
                var desplazamiento = 8 - profundidad - bit % 8;
                return (fila[bit / 8] >> desplazamiento) & ((1 << profundidad) - 1);
        }
    }

    /// <summary>Lleva una muestra de <paramref name="profundidad"/> bits a 0–255.</summary>
    private static byte A8(int muestra, int profundidad) => profundidad switch
    {
        8 => (byte)muestra,
        16 => (byte)(muestra >> 8),
        _ => (byte)(muestra * 255 / ((1 << profundidad) - 1))
    };

    private static void Pixel(byte[] fila, int columna, int tipoColor, int profundidad, byte[]? paleta, byte[]? transparencia, byte[] rgb, int destino)
    {
        int r, g, b, alfa = 255;
        switch (tipoColor)
        {
            case 0:
                var gris = Muestra(fila, columna, profundidad);
                (r, g, b) = (A8(gris, profundidad), A8(gris, profundidad), A8(gris, profundidad));
                if (transparencia is { Length: >= 2 } && gris == ((transparencia[0] << 8) | transparencia[1]))
                    alfa = 0;
                break;
            case 2:
                var (mr, mg, mb) = (Muestra(fila, columna * 3, profundidad), Muestra(fila, columna * 3 + 1, profundidad), Muestra(fila, columna * 3 + 2, profundidad));
                (r, g, b) = (A8(mr, profundidad), A8(mg, profundidad), A8(mb, profundidad));
                if (transparencia is { Length: >= 6 } && mr == ((transparencia[0] << 8) | transparencia[1]) && mg == ((transparencia[2] << 8) | transparencia[3]) &&
                    mb == ((transparencia[4] << 8) | transparencia[5]))
                    alfa = 0;
                break;
            case 3:
                var indice = Muestra(fila, columna, profundidad);
                var posicion = indice * 3;
                (r, g, b) = posicion + 2 < paleta!.Length ? (paleta[posicion], paleta[posicion + 1], paleta[posicion + 2]) : (255, 255, 255);
                if (transparencia is not null && indice < transparencia.Length)
                    alfa = transparencia[indice];
                break;
            case 4:
                var grisAlfa = A8(Muestra(fila, columna * 2, profundidad), profundidad);
                (r, g, b) = (grisAlfa, grisAlfa, grisAlfa);
                alfa = A8(Muestra(fila, columna * 2 + 1, profundidad), profundidad);
                break;
            default:
                (r, g, b) = (A8(Muestra(fila, columna * 4, profundidad), profundidad), A8(Muestra(fila, columna * 4 + 1, profundidad), profundidad),
                    A8(Muestra(fila, columna * 4 + 2, profundidad), profundidad));
                alfa = A8(Muestra(fila, columna * 4 + 3, profundidad), profundidad);
                break;
        }

        if (alfa < 255)
        {
            r = (r * alfa + 255 * (255 - alfa)) / 255;
            g = (g * alfa + 255 * (255 - alfa)) / 255;
            b = (b * alfa + 255 * (255 - alfa)) / 255;
        }

        (rgb[destino], rgb[destino + 1], rgb[destino + 2]) = ((byte)r, (byte)g, (byte)b);
    }
}
