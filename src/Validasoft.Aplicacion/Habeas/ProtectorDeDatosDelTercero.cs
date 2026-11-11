using System.Security.Cryptography;
using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Habeas;

namespace Validasoft.Aplicacion.Habeas;

/// <summary>
/// Cifra con la llave de cada tercero lo personal que va a un evento inmutable y destruye esa llave cuando el titular pide su
/// supresión (OD-4): el evento sigue ahí, pero ilegible (crypto-shredding). La llave de datos vive envuelta con la clave maestra de
/// secretos (<see cref="IProtectorDeSecretos"/>), así que rotar la clave maestra no la invalida.
/// </summary>
public interface IProtectorDeDatosDelTercero
{
    /// <summary>Cifra <paramref name="texto"/> con la llave del tercero; la crea si aún no tiene.</summary>
    Task<string> ProtegerAsync(Guid terceroId, string texto, CancellationToken ct);

    /// <summary>
    /// El texto en claro, o nulo si ya no se puede leer: la llave se destruyó (o se destruyó y se creó otra) o el texto no es de un
    /// tercero. Un texto alterado lanza <see cref="CryptographicException"/>.
    /// </summary>
    Task<string?> RevelarAsync(string? protegido, CancellationToken ct);

    /// <summary>Destruye la llave del tercero: todo lo que cifró queda ilegible para siempre. Falso si no tenía llave.</summary>
    Task<bool> DestruirAsync(Guid terceroId, CancellationToken ct);

    /// <summary>¿Todavía se puede leer lo que cifró este tercero? (existe su llave).</summary>
    Task<bool> TieneLlaveAsync(Guid terceroId, CancellationToken ct);
}

/// <inheritdoc cref="IProtectorDeDatosDelTercero"/>
public sealed class ProtectorDeDatosDelTercero(ISesiones sesiones, IProtectorDeSecretos secretos, TimeProvider reloj) : IProtectorDeDatosDelTercero
{
    public async Task<string> ProtegerAsync(Guid terceroId, string texto, CancellationToken ct)
    {
        var (llave, kid) = await ObtenerOCrearAsync(terceroId, ct);
        return CifradoDePersonales.Cifrar(llave, terceroId, kid, texto);
    }

    public async Task<string?> RevelarAsync(string? protegido, CancellationToken ct)
    {
        if (CifradoDePersonales.Leer(protegido) is not { } leido)
            return null;
        var (terceroId, kid) = leido;

        await using var consulta = sesiones.ConsultaGlobal();
        var existente = await consulta.LoadAsync<LlaveDeDatosDelTercero>(terceroId, ct);
        return existente is null || existente.Kid != kid
            ? null
            : CifradoDePersonales.Descifrar(Convert.FromBase64String(secretos.Desproteger(existente.LlaveProtegida)), protegido!);
    }

    public async Task<bool> DestruirAsync(Guid terceroId, CancellationToken ct)
    {
        await using var sesion = sesiones.Global();
        if (await sesion.LoadAsync<LlaveDeDatosDelTercero>(terceroId, ct) is null)
            return false;
        sesion.Delete<LlaveDeDatosDelTercero>(terceroId);
        await sesion.SaveChangesAsync(ct);
        return true;
    }

    public async Task<bool> TieneLlaveAsync(Guid terceroId, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return await consulta.LoadAsync<LlaveDeDatosDelTercero>(terceroId, ct) is not null;
    }

    private async Task<(byte[] Llave, Guid Kid)> ObtenerOCrearAsync(Guid terceroId, CancellationToken ct)
    {
        for (var intento = 1; ; intento++)
        {
            await using var sesion = sesiones.Global();
            if (await sesion.LoadAsync<LlaveDeDatosDelTercero>(terceroId, ct) is { } existente)
                return (Convert.FromBase64String(secretos.Desproteger(existente.LlaveProtegida)), existente.Kid);

            var llave = CifradoDePersonales.NuevaLlave();
            var nueva = new LlaveDeDatosDelTercero
            {
                Id = terceroId,
                Kid = Guid.CreateVersion7(),
                LlaveProtegida = secretos.Proteger(Convert.ToBase64String(llave)),
                CreadaEn = reloj.GetUtcNow()
            };
            try
            {
                sesion.Insert(nueva);
                await sesion.SaveChangesAsync(ct);
                return (llave, nueva.Kid);
            }
            catch (Exception e) when (intento < 5 && Peticiones.EsCarrera(e))
            {
                // Otra petición creó la llave a la vez: se lee la suya.
            }
        }
    }
}
