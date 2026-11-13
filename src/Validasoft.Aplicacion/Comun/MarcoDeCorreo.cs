using System.Buffers.Binary;
using System.Net;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Una imagen que viaja dentro del correo y se refiere desde el HTML como <c>cid:{ContentId}</c>.</summary>
public sealed record RecursoDeCorreo(string ContentId, string Nombre, string TipoMime, byte[] Contenido);

/// <summary>
/// El marco de marca de todos los correos (MA-1, docs/marca.md): el logotipo de Validasoft arriba y el contenido del correo en una tarjeta,
/// con colores explícitos (un cliente en modo oscuro no los invierte). Las imágenes no se piden por URL —muchos clientes las bloquean— sino
/// que viajan <b>dentro del mensaje</b> (<c>cid:</c>): el logotipo siempre y el GIF de éxito solo en los correos que lo piden
/// (<see cref="ImagenDeExito"/>, p. ej. la aprobación). Los PNG y GIF son recursos incrustados que genera <c>marca/generar.mjs</c>.
/// </summary>
public static class MarcoDeCorreo
{
    /// <summary>Identificador del logotipo (<c>cid:validasoft-logotipo</c>).</summary>
    public const string IdLogotipo = "validasoft-logotipo";

    /// <summary>Identificador del GIF de éxito (<c>cid:validasoft-exito</c>).</summary>
    public const string IdExito = "validasoft-exito";

    private static readonly Dictionary<string, (string Recurso, string Nombre, string TipoMime)> Imagenes = new(StringComparer.Ordinal)
    {
        [IdLogotipo] = ("Validasoft.Marca.logotipo.png", "validasoft.png", "image/png"),
        [IdExito] = ("Validasoft.Marca.exito.gif", "exito.gif", "image/gif"),
    };

    private static readonly Lazy<Dictionary<string, RecursoDeCorreo>> Recursos = new(() =>
        Imagenes.ToDictionary(i => i.Key, i => new RecursoDeCorreo(i.Key, i.Value.Nombre, i.Value.TipoMime, Leer(i.Value.Recurso)), StringComparer.Ordinal));

    /// <summary>
    /// El HTML del correo dentro del marco de marca: el logotipo, y debajo el contenido. Es un fragmento (sin <c>html</c> ni <c>body</c>):
    /// los redactores siguen escribiendo solo párrafos.
    /// </summary>
    public static string Envolver(string html)
    {
        // Las imágenes están guardadas al doble de su tamaño (nitidez en pantallas densas): se muestran a la mitad.
        var (ancho, alto) = DimensionesPng(Recursos.Value[IdLogotipo].Contenido);
        return "<div style=\"margin:0;padding:24px 12px;background-color:#F1F4FA;\">" +
               "<div style=\"max-width:560px;margin:0 auto;padding:24px;background-color:#FFFFFF;border:1px solid #E3E8F1;border-radius:12px;" +
               "font-family:'Segoe UI',Arial,sans-serif;font-size:15px;line-height:22px;color:#0A0F1C;\">" +
               $"<img src=\"cid:{IdLogotipo}\" alt=\"Validasoft\" width=\"{ancho / 2}\" height=\"{alto / 2}\" style=\"display:block;border:0;margin:0 0 20px 0;\">" +
               html + "</div></div>";
    }

    /// <summary>
    /// El GIF de éxito de marca para el principio de un correo que cierra algo bueno (la aprobación). Anima una vez y se queda en el
    /// círculo verde con el check; un cliente que no anima GIF muestra el último cuadro.
    /// </summary>
    public static string ImagenDeExito(string descripcion = "Listo") =>
        $"<p style=\"margin:0 0 16px 0;\"><img src=\"cid:{IdExito}\" alt=\"{WebUtility.HtmlEncode(descripcion)}\" width=\"96\" height=\"96\" style=\"display:block;border:0;\"></p>";

    /// <summary>Las imágenes de marca que el HTML pide con <c>cid:</c> (cada una una sola vez), listas para adjuntar al mensaje.</summary>
    public static IReadOnlyList<RecursoDeCorreo> RecursosUsados(string html) =>
        [.. Recursos.Value.Where(r => html.Contains($"cid:{r.Key}", StringComparison.Ordinal)).Select(r => r.Value)];

    /// <summary>El HTML con las imágenes de marca incrustadas como <c>data:</c> (para verlo fuera de un correo, como la vista previa de una plantilla).</summary>
    public static string EnLinea(string html)
    {
        foreach (var recurso in RecursosUsados(html))
            html = html.Replace($"cid:{recurso.ContentId}", $"data:{recurso.TipoMime};base64,{Convert.ToBase64String(recurso.Contenido)}", StringComparison.Ordinal);
        return html;
    }

    private static byte[] Leer(string nombre)
    {
        using var flujo = typeof(MarcoDeCorreo).Assembly.GetManifestResourceStream(nombre)
                          ?? throw new InvalidOperationException($"Falta el recurso incrustado {nombre} (se genera con `pnpm --filter @validasoft/marca generar`).");
        using var memoria = new MemoryStream();
        flujo.CopyTo(memoria);
        return memoria.ToArray();
    }

    /// <summary>Ancho y alto en píxeles de un PNG (cabecera IHDR).</summary>
    private static (int Ancho, int Alto) DimensionesPng(byte[] png) =>
        ((int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(16, 4)), (int)BinaryPrimitives.ReadUInt32BigEndian(png.AsSpan(20, 4)));
}
