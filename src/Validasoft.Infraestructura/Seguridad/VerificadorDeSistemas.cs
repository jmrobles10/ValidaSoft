using Marten;
using Microsoft.Extensions.Caching.Memory;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Comprueba en cada petición con una sesión de Validasoft que el sistema o el operador que la originó siga activo (PI-7): desactivar un sistema integrador, un
/// sistema de Plataforma o un operador corta sus sesiones ya emitidas en segundos, en todos los servidores (PI-8), sin esperar a que venza el token de 30
/// minutos. Se guarda <see cref="Vigencia"/> en memoria.
/// </summary>
public sealed class VerificadorDeSistemas(IDocumentStore store, IMemoryCache cache, IBusDeInvalidaciones? bus = null) : ICacheDeSistemas, IReceptorDeInvalidaciones
{
    public static readonly TimeSpan Vigencia = TimeSpan.FromSeconds(15);

    private const string PrefijoIntegrador = "i|";
    private const string PrefijoOperador = "o|";

    public string Tipo => TiposDeInvalidacion.Sistema;

    private static string ClaveIntegrador(Guid tenantId, string codigo) => $"sistema-activo:{tenantId:N}:{codigo.ToUpperInvariant()}";

    private static string ClaveOperador(string operadorId) => "operador-activo:" + operadorId;

    /// <summary>Lo que se guarda de un sistema u operador: si sirve (activo y su grupo o sistema también) y su generación de sesiones.</summary>
    private sealed record Estado(bool Activo, int Generacion);

    public async Task<bool> IntegradorActivoAsync(Guid tenantId, string codigo, int generacion, CancellationToken ct)
    {
        var clave = ClaveIntegrador(tenantId, codigo);
        if (!cache.TryGetValue(clave, out Estado? estado) || estado is null)
        {
            await using var consulta = store.QuerySession(tenantId.ToString("N"));
            var normalizado = codigo.ToUpperInvariant();
            var sistema = await consulta.Query<SistemaIntegrado>().FirstOrDefaultAsync(s => s.Codigo == normalizado, ct);
            var tenant = await consulta.LoadAsync<Tenant>(tenantId, ct);
            estado = new Estado(sistema is { Activo: true } && tenant is { Activo: true }, sistema?.Generacion ?? 0);
            cache.Set(clave, estado, Vigencia);
        }

        return estado.Activo && estado.Generacion == generacion;
    }

    public async Task<bool> OperadorActivoAsync(string operadorId, int generacion, CancellationToken ct)
    {
        var clave = ClaveOperador(operadorId);
        if (!cache.TryGetValue(clave, out Estado? estado) || estado is null)
        {
            await using var consulta = store.QuerySession();
            var operador = await consulta.LoadAsync<OperadorDePlataforma>(operadorId, ct);
            var activo = operador is { Activo: true };
            if (activo && !operador!.EsDirecto && operador.Sistema.Length > 0)
                activo = await consulta.LoadAsync<SistemaDePlataforma>(operador.Sistema, ct) is { Activo: true };
            estado = new Estado(activo, operador?.Generacion ?? 0);
            cache.Set(clave, estado, Vigencia);
        }

        return estado.Activo && estado.Generacion == generacion;
    }

    public void OlvidarIntegrador(Guid tenantId, string codigo)
    {
        cache.Remove(ClaveIntegrador(tenantId, codigo));
        bus?.Publicar(Tipo, $"{PrefijoIntegrador}{tenantId:N}|{codigo.ToUpperInvariant()}");
    }

    public void OlvidarOperador(string operadorId)
    {
        cache.Remove(ClaveOperador(operadorId));
        bus?.Publicar(Tipo, PrefijoOperador + operadorId);
    }

    public void Recibir(string id)
    {
        if (id.StartsWith(PrefijoOperador, StringComparison.Ordinal))
            cache.Remove(ClaveOperador(id[PrefijoOperador.Length..]));
        else if (id.StartsWith(PrefijoIntegrador, StringComparison.Ordinal) && id[PrefijoIntegrador.Length..].Split('|', 2) is [var tenant, var codigo] &&
                 Guid.TryParseExact(tenant, "N", out var tenantId))
            cache.Remove(ClaveIntegrador(tenantId, codigo));
    }

    /// <summary>Se perdió la escucha de avisos: lo guardado vence solo en segundos.</summary>
    public void Reiniciar()
    {
    }
}
