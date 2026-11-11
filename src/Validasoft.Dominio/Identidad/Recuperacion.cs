using System.Security.Cryptography;
using System.Text;

namespace Validasoft.Dominio.Identidad;

/// <summary>Un código de recuperación: solo se guarda su hash y cuándo se usó.</summary>
public sealed class CodigoDeRecuperacion
{
    public string Hash { get; set; } = string.Empty;

    public DateTimeOffset? UsadoEn { get; set; }
}

/// <summary>
/// Códigos de recuperación de una cuenta (PI-18): respaldo de la passkey cuando se pierde el dispositivo. Son <see cref="Cantidad"/> códigos de un solo
/// uso que la persona guarda aparte; se muestran <b>una sola vez</b> al generarlos y de ahí en adelante solo existe su hash. Un código solo sirve
/// <b>después</b> de verificar el código del correo del mismo ingreso (son dos factores distintos) y, al usarlo, la persona registra una passkey nueva en
/// ese ingreso; generar un juego nuevo anula el anterior. Global (esquema <c>identidad</c>); id <c>{tipo}:{grupo|-}:{sujeto}</c>.
/// </summary>
public sealed class CodigosDeRecuperacion
{
    public const int Cantidad = 10;
    public const int Longitud = 10;

    /// <summary>Sin letras que se confunden entre sí (I, L, O, 0, 1): el código se copia a mano.</summary>
    private const string Alfabeto = "ABCDEFGHJKMNPQRSTUVWXYZ23456789";

    public string Id { get; set; } = string.Empty;

    public List<CodigoDeRecuperacion> Codigos { get; set; } = [];

    public DateTimeOffset GeneradosEn { get; set; }

    public int Disponibles => Codigos.Count(c => c.UsadoEn is null);

    public static string IdDe(CuentaDeIngreso cuenta) =>
        $"{(cuenta.Tipo == TipoDePropietario.Usuario ? "u" : "o")}:{(cuenta.TenantId is { } t ? t.ToString("N") : "-")}:{cuenta.SujetoId}";

    /// <summary>Un juego nuevo de <see cref="Cantidad"/> códigos para la cuenta; devuelve el documento (con los hashes) y los códigos en claro, que no se guardan.</summary>
    public static (CodigosDeRecuperacion Documento, IReadOnlyList<string> EnClaro) Generar(CuentaDeIngreso cuenta, DateTimeOffset ahora)
    {
        var documento = new CodigosDeRecuperacion { Id = IdDe(cuenta), GeneradosEn = ahora };
        var claros = new List<string>(Cantidad);
        for (var i = 0; i < Cantidad; i++)
        {
            var codigo = Nuevo();
            claros.Add(codigo);
            documento.Codigos.Add(new CodigoDeRecuperacion { Hash = documento.HashDe(Normalizar(codigo)) });
        }

        return (documento, claros);
    }

    /// <summary>Mayúsculas y sin separadores: <c>abcde-fghjk</c> y <c>ABCDE FGHJK</c> son el mismo código.</summary>
    public static string Normalizar(string? codigo) =>
        new((codigo ?? string.Empty).Where(char.IsLetterOrDigit).Select(char.ToUpperInvariant).ToArray());

    /// <summary>
    /// <c>true</c> y lo marca como usado si el código es uno de los vigentes; <c>false</c> si no. El llamador guarda el documento (concurrencia optimista: dos usos
    /// simultáneos del mismo código no pasan los dos).
    /// </summary>
    public bool Consumir(string? codigo, DateTimeOffset ahora)
    {
        var normalizado = Normalizar(codigo);
        if (normalizado.Length != Longitud)
            return false;

        var buscado = Encoding.ASCII.GetBytes(HashDe(normalizado));
        CodigoDeRecuperacion? encontrado = null;
        // Se recorren todos, sin cortar al primero, para que el tiempo no diga cuántos códigos hay ni en qué lugar está.
        foreach (var candidato in Codigos)
        {
            if (candidato.UsadoEn is null && CryptographicOperations.FixedTimeEquals(buscado, Encoding.ASCII.GetBytes(candidato.Hash)))
                encontrado = candidato;
        }

        if (encontrado is null)
            return false;
        encontrado.UsadoEn = ahora;
        return true;
    }

    private string HashDe(string normalizado) => Convert.ToHexStringLower(SHA256.HashData(Encoding.UTF8.GetBytes($"{Id}:{normalizado}")));

    private static string Nuevo()
    {
        var letras = new char[Longitud];
        for (var i = 0; i < Longitud; i++)
            letras[i] = Alfabeto[RandomNumberGenerator.GetInt32(Alfabeto.Length)];
        return $"{new string(letras, 0, 5)}-{new string(letras, 5, 5)}";
    }
}
