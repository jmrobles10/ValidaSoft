using Marten;
using Microsoft.Extensions.Caching.Memory;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Lo que el contexto de la petición necesita de la empresa de <c>X-Empresa</c>: su tenant y si está activa.</summary>
public sealed record EmpresaDeAcceso(Guid Id, Guid TenantId, bool Activa);

/// <summary>
/// Caché por nodo de la empresa de <c>X-Empresa</c> (T-4): sin ella, cada petición de una empresa paga un viaje a la base solo para
/// comprobar que existe, es del tenant y está activa. Vive 30 segundos; al desactivar o cambiar una empresa se olvida en el nodo
/// que lo hizo y se avisa a los demás (PI-8). Una empresa que no existe no se guarda.
/// </summary>
public sealed class CacheDeEmpresas(IMemoryCache cache, IBusDeInvalidaciones? bus = null) : IReceptorDeInvalidaciones
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(30);

    private static string Llave(Guid id) => "empresa-de-acceso:" + id.ToString("N");

    public async Task<EmpresaDeAcceso?> ObtenerAsync(IDocumentStore store, Guid id, CancellationToken ct)
    {
        if (cache.TryGetValue(Llave(id), out EmpresaDeAcceso? guardada) && guardada is not null)
            return guardada;

        await using var sesion = store.QuerySession();
        if (await sesion.LoadAsync<Empresa>(id, ct) is not { } empresa)
            return null;

        var datos = new EmpresaDeAcceso(empresa.Id, empresa.TenantId, empresa.Activa);
        cache.Set(Llave(id), datos, Vigencia);
        return datos;
    }

    public string Tipo => TiposDeInvalidacion.Empresa;

    public void Olvidar(Guid id)
    {
        cache.Remove(Llave(id));
        bus?.Publicar(Tipo, id.ToString());
    }

    public void Recibir(string id)
    {
        if (Guid.TryParse(id, out var empresaId))
            cache.Remove(Llave(empresaId));
    }

    /// <summary>Se perdió la escucha de avisos: las empresas guardadas vencen solas en 30 segundos.</summary>
    public void Reiniciar()
    {
    }
}
