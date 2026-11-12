using System.IO.Compression;

namespace Validasoft.Dominio.Ficha;

/// <summary>Imagen de la firma ya decodificada: píxeles RGB de 8 bits, fila por fila, compuestos sobre fondo blanco.</summary>
public sealed record ImagenDeFirma(int Ancho, int Alto, byte[] Rgb);

/// <summary>
/// La imagen de la firma de una declaración PEP: un PNG de 8 bits sin entrelazado (lo que genera el navegador al normalizarla a
/// 600×200), de hasta <see cref="MaximoBytes"/> y <see cref="MaximoLado"/> píxeles por lado. Se decodifica entero al guardar, así
/// un archivo dañado se rechaza ahí y no al descargar el PDF. Sin dependencias: solo la inflación de .NET.
/// </summary>
public static class FirmaPng
{
    public const int MaximoBytes = 98_304;
    public const int MaximoLado = 1200;
    public const string MensajeFormato = "la firma debe ser una imagen PNG válida de 8 bits, sin entrelazado.";

    private static readonly byte[] Cabecera = [0x89, 0x50, 0x4E, 0x47, 0x0D, 0x0A, 0x1A, 0x0A];

    /// <summary>Bytes de un base64 (con o sin saltos de línea); nulo si no es base64.</summary>
    public static byte[]? Bytes(string base64)
    {
        var buffer = new byte[base64.Length];
        return Convert.TryFromBase64String(base64, buffer, out var escritos) ? buffer[..escritos] : null;
    }

    /// <summary>Motivo por el que no sirve como firma, o nulo si es un PNG válido y decodificable dentro de los límites.</summary>
    public static string? Error(byte[] png) => Decodificar(png, out _);

    /// <summary>Decodifica el PNG. Devuelve el error (nulo si salió bien) y la imagen.</summary>
    public static string? Decodificar(byte[] png, out ImagenDeFirma? imagen)
    {
        imagen = null;
        if (png.Length > MaximoBytes)
            return $"la firma pesa más de {MaximoBytes / 1024} KB.";
        if (png.Length < Cabecera.Length + 25 || !png.AsSpan(0, Cabecera.Length).SequenceEqual(Cabecera))
            return MensajeFormato;

        int ancho = 0, alto = 0, tipoColor = -1;
        byte[]? paleta = null;
        using var datos = new MemoryStream();
        var posicion = Cabecera.Length;
        var visto = false;
        while (posicion + 12 <= png.Length)
        {
            var largo = Entero(png, posicion);
            var tipo = System.Text.Encoding.ASCII.GetString(png, posicion + 4, 4);
            var inicio = posicion + 8;
            if (largo < 0 || (long)inicio + largo + 4 > png.Length)
                return MensajeFormato;

            switch (tipo)
            {
                case "IHDR":
                    if (largo != 13)
                        return MensajeFormato;
                    ancho = Entero(png, inicio);
                    alto = Entero(png, inicio + 4);
                    if (png[inicio + 8] != 8 || png[inicio + 10] != 0 || png[inicio + 11] != 0 || png[inicio + 12] != 0)
                        return MensajeFormato;
                    tipoColor = png[inicio + 9];
                    visto = true;
                    break;
                case "PLTE":
                    paleta = png[inicio..(inicio + largo)];
                    break;
                case "IDAT":
                    datos.Write(png, inicio, largo);
                    break;
            }

            posicion = inicio + largo + 4;
            if (tipo == "IEND")
                break;
        }

        if (!visto || ancho is < 1 or > MaximoLado || alto is < 1 or > MaximoLado)
            return visto ? $"la firma debe medir entre 1 y {MaximoLado} píxeles por lado." : MensajeFormato;
        var canales = tipoColor switch { 0 => 1, 2 => 3, 3 => 1, 4 => 2, 6 => 4, _ => 0 };
        if (canales == 0 || (tipoColor == 3 && (paleta is null || paleta.Length % 3 != 0)))
            return MensajeFormato;

        var fila = ancho * canales;
        var crudo = new byte[alto * (fila + 1)];
        try
        {
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

            if (leidos < crudo.Length)
                return MensajeFormato;
        }
        catch (Exception e) when (e is InvalidDataException or IOException)
        {
            return MensajeFormato;
        }

        var rgb = new byte[ancho * alto * 3];
        var anterior = new byte[fila];
        var actual = new byte[fila];
        for (var y = 0; y < alto; y++)
        {
            var filtro = crudo[y * (fila + 1)];
            Buffer.BlockCopy(crudo, y * (fila + 1) + 1, actual, 0, fila);
            if (!Desfiltrar(filtro, actual, anterior, canales))
                return MensajeFormato;
            for (var x = 0; x < ancho; x++)
                Pixel(actual, x * canales, tipoColor, paleta, rgb, (y * ancho + x) * 3);
            (anterior, actual) = (actual, anterior);
        }

        imagen = new ImagenDeFirma(ancho, alto, rgb);
        return null;
    }

    private static int Entero(byte[] b, int i) => (b[i] << 24) | (b[i + 1] << 16) | (b[i + 2] << 8) | b[i + 3];

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

    /// <summary>Un píxel a RGB; con transparencia se compone sobre blanco (la firma se imprime sobre papel).</summary>
    private static void Pixel(byte[] fila, int i, int tipoColor, byte[]? paleta, byte[] rgb, int destino)
    {
        byte r, g, b;
        int alfa = 255;
        switch (tipoColor)
        {
            case 0:
                r = g = b = fila[i];
                break;
            case 2:
                (r, g, b) = (fila[i], fila[i + 1], fila[i + 2]);
                break;
            case 3:
                var indice = fila[i] * 3;
                (r, g, b) = indice + 2 < paleta!.Length ? (paleta[indice], paleta[indice + 1], paleta[indice + 2]) : ((byte)255, (byte)255, (byte)255);
                break;
            case 4:
                r = g = b = fila[i];
                alfa = fila[i + 1];
                break;
            default:
                (r, g, b) = (fila[i], fila[i + 1], fila[i + 2]);
                alfa = fila[i + 3];
                break;
        }

        if (alfa < 255)
        {
            r = (byte)((r * alfa + 255 * (255 - alfa)) / 255);
            g = (byte)((g * alfa + 255 * (255 - alfa)) / 255);
            b = (byte)((b * alfa + 255 * (255 - alfa)) / 255);
        }

        (rgb[destino], rgb[destino + 1], rgb[destino + 2]) = (r, g, b);
    }
}
