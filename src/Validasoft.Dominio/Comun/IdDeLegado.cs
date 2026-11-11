using System.Security.Cryptography;
using System.Text;

namespace Validasoft.Dominio.Comun;

/// <summary>
/// Identidad de lo que se importa de un sistema de origen (docs/08 §6.2): <c>ClaveLegado</c> (<c>{sistema}:{clave}</c>) y su id en
/// Validasoft, un UUID v5 (RFC 9562, nombre con SHA-1) con el espacio de nombres fijo de Validasoft. Reimportar da el mismo
/// id, así que la importación es idempotente y las referencias se resuelven sin tabla de equivalencias.
/// </summary>
public static class IdDeLegado
{
    /// <summary>Espacio de nombres de Validasoft para los UUID v5 del legado. No cambia nunca: cambiarlo duplicaría lo importado.</summary>
    public static readonly Guid EspacioDeNombres = new("4c0b1f39-6a8e-4f7d-9d52-3e8a0c7b2f61");

    /// <summary>UUID v5 de <paramref name="nombre"/> en <paramref name="espacio"/> (por omisión, el de Validasoft).</summary>
    public static Guid UuidV5(string nombre, Guid? espacio = null)
    {
        Span<byte> bytesEspacio = stackalloc byte[16];
        (espacio ?? EspacioDeNombres).TryWriteBytes(bytesEspacio, bigEndian: true, out _);
        var bytesNombre = Encoding.UTF8.GetBytes(nombre);
        var datos = new byte[16 + bytesNombre.Length];
        bytesEspacio.CopyTo(datos);
        bytesNombre.CopyTo(datos, 16);

#pragma warning disable CA5350 // SHA-1 es el que exige el UUID v5 (identificador, no seguridad).
        var hash = SHA1.HashData(datos);
#pragma warning restore CA5350
        hash[6] = (byte)((hash[6] & 0x0F) | 0x50); // versión 5
        hash[8] = (byte)((hash[8] & 0x3F) | 0x80); // variante RFC 4122
        return new Guid(hash.AsSpan(0, 16), bigEndian: true);
    }

    /// <summary>Clave del legado de un tercero: <c>{sistema}:{clave}</c> con el sistema en minúsculas (p. ej. <c>portal:48112</c>).</summary>
    public static string Clave(string sistema, string clave) => $"{sistema.Trim().ToLowerInvariant()}:{clave.Trim()}";

    /// <summary>Id en Validasoft de un tercero importado de un sistema de origen (UUID v5 de su <see cref="Clave"/>).</summary>
    public static Guid Tercero(string sistema, string clave) => UuidV5(Clave(sistema, clave));
}
