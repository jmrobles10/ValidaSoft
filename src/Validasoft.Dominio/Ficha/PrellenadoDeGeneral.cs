using System.Text.Json;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Ficha;

/// <summary>
/// Lo que ya se sabe del tercero antes de que diligencie la información general (FD-1): su razón social (o su nombre, partido en
/// nombres y apellidos si es persona natural), la identificación (tipo, número y DV, que fija su registro) y el correo con que entró
/// desde la invitación. Es solo una <b>sugerencia</b> para el formulario vacío: nada se guarda hasta que el tercero la revisa y guarda,
/// y una vez que hay una versión de la sección ya no se ofrece. Función pura.
/// </summary>
public static class PrellenadoDeGeneral
{
    /// <summary>Partículas que van con el apellido que sigue («De la Cruz», «Del Río»).</summary>
    private static readonly HashSet<string> Particulas = new(StringComparer.OrdinalIgnoreCase) { "de", "del", "la", "las", "los", "y", "van", "von" };

    /// <summary>
    /// El JSON canónico (snake_case, sin nulos) de la General con lo que ya se sabe. Nulo si no hay nada que sugerir.
    /// <paramref name="correoDeLaInvitacion"/> es el correo de la sesión del tercero; si no tiene forma de correo se omite.
    /// </summary>
    public static JsonElement? Armar(Tercero tercero, string? correoDeLaInvitacion, ContextoDeSeccion contexto)
    {
        var nombre = Limpio(tercero.Nombre);
        var correo = Limpio(correoDeLaInvitacion);
        if (correo is not null && !correo.Contains('@', StringComparison.Ordinal))
            correo = null;

        var general = new DatosGenerales { Correo = correo };
        if (contexto.EsJuridica)
        {
            general = general with { RazonSocial = nombre };
        }
        else
        {
            var (nombres, apellidos) = DividirNombre(nombre);
            general = general with { Nombres = nombres, Apellidos = apellidos };
        }

        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.General);
        // Normalizar fija lo del registro del tercero (tipo de persona, identificación y DV calculado) y recorta lo demás.
        var normalizado = definicion.Esquema.Normalizar(general, contexto);
        return definicion.Esquema.Canonico(normalizado);
    }

    /// <summary>
    /// Parte el nombre de una persona natural en nombres y apellidos. Es una sugerencia (el registro guarda el nombre completo en un solo
    /// campo): una palabra ⇒ solo nombres; dos ⇒ uno y uno; tres ⇒ un nombre y dos apellidos (lo más común en Colombia); cuatro o más ⇒
    /// mitad y mitad, corriendo la frontera hacia atrás para que «de», «del», «la»… que cierran los nombres pasen al apellido.
    /// </summary>
    public static (string? Nombres, string? Apellidos) DividirNombre(string? nombreCompleto)
    {
        var palabras = (nombreCompleto ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries);
        switch (palabras.Length)
        {
            case 0:
                return (null, null);
            case 1:
                return (palabras[0], null);
        }

        var nombres = palabras.Length switch { 2 => 1, 3 => 1, _ => palabras.Length / 2 };
        while (nombres > 1 && Particulas.Contains(palabras[nombres - 1]))
            nombres--;
        return (string.Join(' ', palabras[..nombres]), string.Join(' ', palabras[nombres..]));
    }

    private static string? Limpio(string? texto) => string.IsNullOrWhiteSpace(texto) ? null : texto.Trim();
}
