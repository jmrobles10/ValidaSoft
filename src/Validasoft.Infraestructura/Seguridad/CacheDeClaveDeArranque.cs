using Marten;
using Microsoft.Extensions.Caching.Memory;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>La clave de arranque de Plataforma puede apagarse desde la consola (PI-17): se lee de la base con una caché de 15 segundos y se descarta en todos los servidores al cambiar (PI-8).</summary>
public sealed class CacheDeClaveDeArranque(IDocumentStore store, IMemoryCache cache, IBusDeInvalidaciones? bus = null) : ICacheDeClaveDeArranque, IReceptorDeInvalidaciones
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(15);

    private const string Clave = "clave-de-arranque-desactivada";

    public string Tipo => TiposDeInvalidacion.Plataforma;

    public async Task<bool> DesactivadaAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(Clave, out bool desactivada))
            return desactivada;

        await using var consulta = store.QuerySession();
        desactivada = (await consulta.LoadAsync<ConfiguracionDePlataforma>(ConfiguracionDePlataforma.IdUnico, ct))?.ClaveDeArranqueDesactivada ?? false;
        cache.Set(Clave, desactivada, Vigencia);
        return desactivada;
    }

    public void Olvidar()
    {
        cache.Remove(Clave);
        bus?.Publicar(Tipo, "global");
    }

    public void Recibir(string id) => cache.Remove(Clave);

    /// <summary>Se perdió la escucha de avisos: lo guardado vence solo en segundos.</summary>
    public void Reiniciar() => cache.Remove(Clave);
}
