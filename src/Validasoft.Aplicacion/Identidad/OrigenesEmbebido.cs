using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Validasoft.Contratos;
using Validasoft.Dominio.Identidad;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>
/// Unión de los orígenes de los sistemas integrados <b>activos</b> de todos los tenants y de los sistemas de Plataforma
/// (SE-1, SE-2, PT-4): la usan la
/// cabecera <c>frame-ancestors</c> de las pantallas embebibles y el endpoint público con el que la app embebida decide a
/// qué contenedor responder por <c>postMessage</c>. Se guarda en memoria <see cref="Vigencia"/>; editar orígenes,
/// crear o desactivar un sistema la invalida en este nodo (los demás nodos la ven al vencer la caché).
/// </summary>
public sealed class OrigenesDeEmbebido(IDocumentStore store, IMemoryCache cache, IConfiguration configuracion)
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(60);

    private const string ClaveCache = "embebido:origenes";

    /// <summary><c>http://localhost:*</c> como origen (solo desarrollo: <c>Validasoft:Embebido:PermitirHttpLocal</c>).</summary>
    public bool PermiteHttpLocal => configuracion.GetValue("Validasoft:Embebido:PermitirHttpLocal", false);

    public async Task<IReadOnlyList<string>> ObtenerAsync(CancellationToken ct)
    {
        if (cache.TryGetValue(ClaveCache, out IReadOnlyList<string>? origenes) && origenes is not null)
            return origenes;

        await using var consulta = store.QuerySession();
        var sistemas = await consulta.Query<SistemaIntegrado>().Where(s => s.AnyTenant() && s.Activo).ToListAsync(ct);
        // Los sistemas de Plataforma también alojan la app (la consola, PT-4).
        var dePlataforma = await consulta.Query<SistemaDePlataforma>().Where(s => s.Activo).ToListAsync(ct);
        origenes =
        [
            .. sistemas.SelectMany(s => s.OrigenesPermitidos).Concat(dePlataforma.SelectMany(s => s.OrigenesPermitidos))
                .Distinct(StringComparer.Ordinal).Order(StringComparer.Ordinal)
        ];
        cache.Set(ClaveCache, origenes, Vigencia);
        return origenes;
    }

    public void Invalidar() => cache.Remove(ClaveCache);
}

/// <summary>Lo que la app embebida consulta en tiempo de ejecución, en lugar de una lista fija al compilar (SE-2).</summary>
public static class OrigenesEmbebidoEndpoints
{
    /// <summary>
    /// Público y sin credencial (lo pide el iframe antes de tener sesión). Devuelve exactamente lo que ya publica la
    /// cabecera <c>frame-ancestors</c>: orígenes sin tenant, sistema ni nombre, así que no revela nada nuevo. Pasa por el
    /// límite por IP de <c>/public/v1</c>.
    /// </summary>
    [WolverineGet("/public/v1/embed/origenes-permitidos")]
    [AllowAnonymous]
    public static async Task<OrigenesEmbebidoDto> Obtener(HttpContext http, OrigenesDeEmbebido origenes, CancellationToken ct)
    {
        var vigencia = (int)OrigenesDeEmbebido.Vigencia.TotalSeconds;
        http.Response.Headers.CacheControl = $"public, max-age={vigencia}";
        return new OrigenesEmbebidoDto(await origenes.ObtenerAsync(ct), vigencia);
    }
}
