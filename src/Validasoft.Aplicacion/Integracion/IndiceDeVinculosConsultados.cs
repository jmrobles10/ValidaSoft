using Microsoft.Extensions.Caching.Memory;

namespace Validasoft.Aplicacion.Integracion;

/// <summary>
/// Índice en memoria del vínculo que corresponde a cada consulta de certificación por identificación (CN-1, H9): la clave del
/// tercero y el código del tipo de vínculo en una empresa llevan siempre al mismo vínculo (<c>ux_vinculo_natural</c>; un vínculo
/// no se borra ni se cambia de tercero o de tipo). Con él, la consulta de Marco lee el vínculo y su certificado en un solo viaje a la
/// base en lugar de resolver primero el tercero, el tipo y el vínculo (de cuatro viajes a uno; k6 a 500 rps, docs/11 H9).
/// <b>Solo guarda ids</b>: los datos del vínculo y del certificado se leen vivos en cada consulta, así que el índice nunca sirve un
/// estado viejo. Si el vínculo ya no existe (reinicio del sandbox), el llamador lo olvida y resuelve de nuevo. Por nodo; vence solo.
/// </summary>
public sealed class IndiceDeVinculosConsultados(IMemoryCache cache)
{
    private static readonly TimeSpan Vigencia = TimeSpan.FromMinutes(30);

    private static string Llave(Guid tenantId, Guid empresaId, string clave, string tipo) => $"vinculo-consultado:{tenantId:N}:{empresaId:N}:{tipo}:{clave}";

    public Guid? Buscar(Guid tenantId, Guid empresaId, string clave, string tipo) =>
        cache.TryGetValue(Llave(tenantId, empresaId, clave, tipo), out Guid id) ? id : null;

    public void Recordar(Guid tenantId, Guid empresaId, string clave, string tipo, Guid vinculoId) =>
        cache.Set(Llave(tenantId, empresaId, clave, tipo), vinculoId, Vigencia);

    public void Olvidar(Guid tenantId, Guid empresaId, string clave, string tipo) => cache.Remove(Llave(tenantId, empresaId, clave, tipo));
}
