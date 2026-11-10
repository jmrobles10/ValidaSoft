using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>Importación idempotente (upsert por código) de un catálogo de plataforma y las consultas de búsqueda.</summary>
public static class ImportacionDeCatalogos
{
    public const int MaximoFilas = 20_000;

    /// <summary>
    /// Valida cada fila (formato del código, nombre, departamento, niveles y padres de especialidades) y la guarda si es
    /// nueva o cambió; las repetidas o inválidas van a <c>rechazados</c>. Las especialidades se procesan por nivel, así un
    /// lote puede traer el grupo, la categoría y la especialidad juntos.
    /// </summary>
    public static async Task<ResultadoImportacionCatalogoDto> ImportarAsync(IDocumentSession sesion, CatalogoDePlataforma catalogo,
        IReadOnlyList<FilaDeCatalogo> filas, DateTimeOffset ahora, CancellationToken ct)
    {
        if (filas.Count == 0)
            throw ReglaDeNegocioException.Validacion("filas-requeridas", "Envía al menos una fila para importar.");
        if (filas.Count > MaximoFilas)
            throw ReglaDeNegocioException.Validacion("demasiadas-filas", $"Una importación admite hasta {MaximoFilas} filas: divídela en lotes.");

        var existentes = (await sesion.Query<EntradaDeCatalogo>().Where(e => e.Catalogo == catalogo).ToListAsync(ct))
            .ToDictionary(e => e.Codigo, StringComparer.Ordinal);
        var nivelDe = existentes.Values.Where(e => e.Nivel is not null).ToDictionary(e => e.Codigo, e => e.Nivel!.Value, StringComparer.Ordinal);
        var vistos = new HashSet<string>(StringComparer.Ordinal);
        var rechazados = new List<FilaRechazadaDto>();
        int creados = 0, actualizados = 0, sinCambios = 0;

        var orden = filas.Select((fila, indice) => (fila, indice));
        if (catalogo == CatalogoDePlataforma.Especialidades)
            orden = orden.OrderBy(f => f.fila.Nivel ?? int.MaxValue).ThenBy(f => f.indice);

        foreach (var (fila, indice) in orden)
        {
            var (entrada, rechazo) = ReglasDeCatalogo.Validar(catalogo, fila, padre => nivelDe.TryGetValue(padre, out var n) ? n : null, ahora);
            if (entrada is null)
            {
                rechazados.Add(new FilaRechazadaDto(indice, fila.Codigo, rechazo!));
                continue;
            }

            if (!vistos.Add(entrada.Codigo))
            {
                rechazados.Add(new FilaRechazadaDto(indice, fila.Codigo, $"El código {entrada.Codigo} está repetido en la importación."));
                continue;
            }

            if (entrada.Nivel is { } nivel)
                nivelDe[entrada.Codigo] = nivel;
            if (existentes.TryGetValue(entrada.Codigo, out var previa))
            {
                if (previa.MismoContenido(entrada))
                {
                    sinCambios++;
                    continue;
                }

                actualizados++;
            }
            else
            {
                creados++;
            }

            sesion.Store(entrada);
        }

        await sesion.SaveChangesAsync(ct);
        return new ResultadoImportacionCatalogoDto(ReglasDeCatalogo.Ruta(catalogo), creados, actualizados, sinCambios,
            [.. rechazados.OrderBy(r => r.Fila)]);
    }

    /// <summary>Búsqueda paginada de entradas activas: por prefijo de código o por nombre sin tildes, y por padre.</summary>
    public static async Task<PaginaDto<EntradaCatalogoDto>> BuscarAsync(IQuerySession consulta, string catalogo, string? q, string? padre,
        int? limite, string? cursor, CancellationToken ct)
    {
        var tipo = ReglasDeCatalogo.DeRuta(catalogo);
        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var consultaBase = consulta.Query<EntradaDeCatalogo>().Where(e => e.Catalogo == tipo && e.Activo);
        if (!string.IsNullOrWhiteSpace(padre))
        {
            var codigoPadre = padre.Trim().ToUpperInvariant();
            consultaBase = consultaBase.Where(e => e.Padre == codigoPadre);
        }

        if (!string.IsNullOrWhiteSpace(q))
        {
            var codigo = q.Trim().ToUpperInvariant();
            var texto = ReglasDeCatalogo.NormalizarTexto(q);
            consultaBase = consultaBase.Where(e => e.Codigo.StartsWith(codigo) || e.NombreNormalizado.Contains(texto));
        }

        var pagina = await consultaBase.OrderBy(e => e.Codigo).Skip(desde).Take(tamano + 1).ToListAsync(ct);
        var hayMas = pagina.Count > tamano;
        return new PaginaDto<EntradaCatalogoDto>([.. pagina.Take(tamano).Select(ADto)], hayMas ? Peticiones.EscribirDesplazamiento(desde + tamano) : null);
    }

    /// <summary>Las entradas activas a las que refieren unos datos (una sola consulta por lote).</summary>
    public static async Task<ICatalogosDeFicha> CargarAsync(IQuerySession consulta, IEnumerable<ReferenciaACatalogo> referencias, CancellationToken ct)
    {
        var ids = referencias.Select(r => EntradaDeCatalogo.IdPara(r.Catalogo, r.Codigo)).Distinct().ToArray();
        return ids.Length == 0 ? CatalogosEnMemoria.Vacios : new CatalogosEnMemoria(await consulta.LoadManyAsync<EntradaDeCatalogo>(ct, ids));
    }

    public static EntradaCatalogoDto ADto(EntradaDeCatalogo e) => new(e.Codigo, e.Nombre, e.Padre, e.Nivel, e.Departamento, e.Pais, e.Activo);

    public static FilaDeCatalogo ADominio(FilaCatalogoDto f) => new(f.Codigo, f.Nombre, f.Departamento, f.Pais, f.Padre, f.Nivel, f.Activo);
}

/// <summary>Catálogos de plataforma de la ficha (FM-10, FM-22): importación por Plataforma y búsqueda desde la web y el portal.</summary>
public static class CatalogosDeFichaEndpoints
{
    /// <summary>
    /// Importa filas a un catálogo (<c>ciudades</c>, <c>actividades-economicas</c>, <c>bancos</c>, <c>responsabilidades-dian</c>,
    /// <c>especialidades</c>): upsert por código, idempotente. 404 <c>catalogo-no-encontrado</c>; 422 <c>filas-requeridas</c>,
    /// <c>demasiadas-filas</c>. Las filas inválidas no detienen el lote: vuelven en <c>rechazados</c>.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/catalogos/{catalogo}/importar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResultadoImportacionCatalogoDto> Importar(string catalogo, ImportarCatalogoDto dto, ISesiones sesiones, TimeProvider reloj,
        CancellationToken ct)
    {
        var tipo = ReglasDeCatalogo.DeRuta(catalogo);
        await using var sesion = sesiones.Global();
        return await ImportacionDeCatalogos.ImportarAsync(sesion, tipo, [.. (dto.Filas ?? []).Select(ImportacionDeCatalogos.ADominio)], reloj.GetUtcNow(), ct);
    }

    /// <summary>Busca en un catálogo (<c>q</c>: prefijo de código o parte del nombre sin tildes; <c>padre</c>; hasta 200 por página).</summary>
    [WolverineGet("/api/v1/catalogos/{catalogo}")]
    [Authorize]
    public static async Task<PaginaDto<EntradaCatalogoDto>> Buscar(string catalogo, string? q, string? padre, int? limite, string? cursor,
        ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return await ImportacionDeCatalogos.BuscarAsync(consulta, catalogo, q, padre, limite, cursor, ct);
    }

    /// <summary>La misma búsqueda para el portal del tercero (sesión del tercero).</summary>
    [WolverineGet("/public/v1/catalogos/{catalogo}")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<PaginaDto<EntradaCatalogoDto>> BuscarPublico(string catalogo, string? q, string? padre, int? limite, string? cursor,
        ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return await ImportacionDeCatalogos.BuscarAsync(consulta, catalogo, q, padre, limite, cursor, ct);
    }
}

/// <summary>
/// Semilla de desarrollo de los catálogos (solo en <c>Development</c>, idempotente): capitales DANE, CIIU comunes de
/// construcción y comercio, bancos con su código de compensación, responsabilidades DIAN y un árbol de especialidades de
/// ejemplo. Los catálogos completos se importan desde la BD del Portal en F4.
/// </summary>
public sealed class SembradoDeCatalogosDeDesarrollo(IDocumentStore store, IHostEnvironment entorno, TimeProvider reloj,
    ILogger<SembradoDeCatalogosDeDesarrollo> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        if (!entorno.IsDevelopment())
            return;
        try
        {
            foreach (var (catalogo, filas) in SemillaDeCatalogos.Todas)
            {
                await using var sesion = store.LightweightSession();
                var resultado = await ImportacionDeCatalogos.ImportarAsync(sesion, catalogo, filas, reloj.GetUtcNow(), cancellationToken);
                if (resultado.Rechazados.Count > 0)
                    logger.LogWarning("La semilla de {Catalogo} tiene {Rechazados} filas rechazadas.", resultado.Catalogo, resultado.Rechazados.Count);
            }
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Otra instancia sembró al mismo tiempo o el esquema aún no existe: la próxima vez se completa (es idempotente).
            logger.LogWarning(e, "No se pudo sembrar los catálogos de desarrollo.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
