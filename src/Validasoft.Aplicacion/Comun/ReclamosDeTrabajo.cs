using Marten;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Reclamo de un trabajo que cuesta dinero o tiene efectos fuera de la base (OC-4): una consulta a un proveedor que se factura, por ejemplo.
/// Un documento por trabajo (id = <see cref="Id"/>, en el tenant que lo ejecuta); <see cref="Instancia"/> identifica cuál ocurrencia del
/// trabajo se reclamó (la fecha en que estaba programada) y <see cref="ExpiraEn"/> es el arrendamiento: si quien lo tomó cae a medias, otro
/// puede retomarlo cuando vence.
/// </summary>
public sealed class ReclamoDeTrabajo
{
    public string Id { get; set; } = string.Empty;

    /// <summary>La ocurrencia reclamada (p. ej. los ticks de la fecha programada): otra ocurrencia del mismo trabajo se reclama de nuevo.</summary>
    public string Instancia { get; set; } = string.Empty;

    public DateTimeOffset TomadoEn { get; set; }

    public DateTimeOffset ExpiraEn { get; set; }
}

/// <summary>
/// Idempotencia por clave de los trabajos pagados (OC-4): antes de llamar al proveedor, el nodo <i>reclama</i> el trabajo con un insert o una
/// actualización con concurrencia optimista; solo uno de los nodos (o de los hilos) que lo intentan a la vez lo obtiene y los demás lo dejan.
/// Una réplica que cae a medias no bloquea el trabajo para siempre: el reclamo es un arrendamiento que vence.
/// </summary>
public sealed class ReclamosDeTrabajo(IDocumentStore store)
{
    /// <summary>
    /// Reclama la ocurrencia <paramref name="instancia"/> del <paramref name="trabajo"/> del tenant hasta <paramref name="ahora"/> +
    /// <paramref name="arrendamiento"/>. Falso si otro ya la reclamó y su arrendamiento sigue vigente (o si perdió la carrera por reclamarla).
    /// </summary>
    public async Task<bool> TomarAsync(Guid tenantId, string trabajo, string instancia, DateTimeOffset ahora, TimeSpan arrendamiento, CancellationToken ct)
    {
        await using var sesion = store.LightweightSession(tenantId.ToString("N"));
        var existente = await sesion.LoadAsync<ReclamoDeTrabajo>(trabajo, ct);
        if (existente is not null && existente.Instancia == instancia && existente.ExpiraEn > ahora)
            return false;

        var reclamo = existente ?? new ReclamoDeTrabajo { Id = trabajo };
        reclamo.Instancia = instancia;
        reclamo.TomadoEn = ahora;
        reclamo.ExpiraEn = ahora + arrendamiento;
        try
        {
            // Insert si no existía (una clave repetida pierde) y Store con concurrencia optimista si se retoma uno vencido o de otra ocurrencia.
            if (existente is null)
                sesion.Insert(reclamo);
            else
                sesion.Store(reclamo);
            await sesion.SaveChangesAsync(ct);
            return true;
        }
        catch (Exception e) when (Peticiones.EsCarrera(e))
        {
            return false;
        }
    }
}
