using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>Orden de recalcular la completitud de todas las fichas (FM-16, FM-23); también se puede encolar.</summary>
public sealed record RecalcularCompletitudDeFichas;

/// <summary>Manejador del mensaje (para ejecutarlo en segundo plano tras desplegar reglas nuevas).</summary>
public static class RecalcularCompletitudDeFichasHandler
{
    public static Task Handle(RecalcularCompletitudDeFichas mensaje, IDocumentStore store, TimeProvider reloj, CancellationToken ct) =>
        RecalculoDeFichas.EjecutarAsync(store, reloj, ct);
}

/// <summary>
/// Recalcula, tercero por tercero, la completitud de la versión vigente de cada sección con las reglas actuales (y el
/// catálogo y el tipo de persona de hoy): emite <see cref="CompletitudDeSeccionRecalculada"/> solo donde cambió y
/// reconstruye el <see cref="EstadoDeFicha"/> desde las secciones, así nada queda desactualizado en silencio. Idempotente.
/// </summary>
public static class RecalculoDeFichas
{
    public static IReadOnlyList<ReglaSeccionDto> Reglas =>
        [.. CatalogoDeSecciones.Todas.Select(s => new ReglaSeccionDto(s.Codigo, s.VersionReglas, s.VersionEsquema))];

    public static async Task<RecalculoFichasDto> EjecutarAsync(IDocumentStore store, TimeProvider reloj, CancellationToken ct)
    {
        List<Guid> terceros;
        await using (var consulta = store.QuerySession(TenantsTecnicos.Boveda))
            terceros = [.. (await consulta.Query<SeccionDeFicha>().Select(s => s.TerceroId).ToListAsync(ct)).Distinct().Order()];

        int secciones = 0, recalculadas = 0;
        foreach (var terceroId in terceros)
        {
            var (revisadas, cambiadas) = await GuardadoDeSecciones.ConReintentosAsync(() => RecalcularTerceroAsync(store, terceroId, reloj, ct));
            secciones += revisadas;
            recalculadas += cambiadas;
        }

        return new RecalculoFichasDto(secciones, recalculadas, terceros.Count, Reglas);
    }

    private static async Task<(int, int)> RecalcularTerceroAsync(IDocumentStore store, Guid terceroId, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var hoy = reloj.Hoy();
        await using var sesion = store.LightweightSession(TenantsTecnicos.Boveda);
        sesion.SetHeader("actorTipo", ActorTipo.Plataforma.ToString());
        sesion.SetHeader("actorId", "recalculo-de-fichas");

        var tercero = await sesion.LoadAsync<Tercero>(terceroId, ct);
        var secciones = await sesion.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).ToListAsync(ct);
        if (tercero is null)
            return (0, 0);

        var referencias = secciones
            .Select(s => (Seccion: s, Definicion: CatalogoDeSecciones.Buscar(s.Seccion)))
            .Where(x => x.Definicion is not null && x.Seccion.Vigente is not null)
            .SelectMany(x => x.Definicion!.Esquema.Referencias(x.Seccion.DatosVigentes(x.Definicion)!));
        var catalogos = await ImportacionDeCatalogos.CargarAsync(sesion, referencias, ct);

        var cambiadas = 0;
        var proyectadas = new List<SeccionDeFicha>();
        foreach (var seccion in secciones)
        {
            var definicion = CatalogoDeSecciones.Buscar(seccion.Seccion);
            var evento = definicion is null
                ? null
                : seccion.Recalcular(definicion, new ContextoDeSeccion(tercero.TipoPersona, tercero.Identificacion, seccion.Periodo, hoy), catalogos, ahora);
            if (evento is null)
            {
                proyectadas.Add(seccion);
                continue;
            }

            // El stream se toma con bloqueo: un guardado simultáneo no se intercala con el recálculo.
            var stream = await sesion.Events.FetchForExclusiveWriting<SeccionDeFicha>(seccion.Id, ct);
            if (stream.Aggregate?.VersionVigente != seccion.VersionVigente)
                throw new CarreraDeFicha();
            stream.AppendOne(evento);
            proyectadas.Add(seccion.Proyectar([evento]));
            cambiadas++;
        }

        var estado = await sesion.LoadAsync<EstadoDeFicha>(terceroId, ct) ?? EstadoDeFicha.Nuevo(terceroId);
        var antes = System.Text.Json.JsonSerializer.Serialize(estado.Secciones);
        estado.Secciones.Clear();
        foreach (var seccion in proyectadas)
            estado.Reflejar(seccion);
        if (cambiadas > 0 || antes != System.Text.Json.JsonSerializer.Serialize(estado.Secciones))
        {
            estado.ActualizadoEn = ahora;
            sesion.Store(estado);
        }

        await sesion.SaveChangesAsync(ct);
        return (secciones.Count, cambiadas);
    }
}

/// <summary>Recalcular la completitud de todas las fichas con las reglas vigentes (rol Plataforma).</summary>
public static class RecalculoDeFichasEndpoints
{
    [WolverinePost("/api/v1/plataforma/fichas/recalcular")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static Task<RecalculoFichasDto> Recalcular(IDocumentStore store, TimeProvider reloj, CancellationToken ct) =>
        RecalculoDeFichas.EjecutarAsync(store, reloj, ct);
}
