using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Aplicacion.Ia;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ia;
using Validasoft.Dominio.Revision;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Revision;

/// <summary>
/// Revisión automática con IA de la empresa activa (ADR-0012): su configuración, la bandeja de excepciones y la resolución humana
/// de las aprobaciones por muestreo. Las decisiones de una empresa son privadas.
/// </summary>
public static class RevisionAutomaticaEndpoints
{
    public const string Entidad = "RevisionAutomaticaEmpresa";
    public const int MaximoFilas = 200;

    /// <summary>
    /// Configuración, uso del mes, tipos analizables y áreas para elegir. Sin configuración guardada es Manual (el análisis solo
    /// informa).
    /// </summary>
    [WolverineGet("/api/v1/configuracion/revision-automatica")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<RevisionAutomaticaConfigDto> Obtener(SesionDeEmpresa empresa, TimeProvider reloj, CancellationToken ct) =>
        await DtoAsync(empresa, await empresa.Sesion.LoadAsync<RevisionAutomaticaEmpresa>(empresa.EmpresaId, ct), reloj.GetUtcNow(), ct);

    /// <summary>
    /// Reemplaza la configuración. 422 <c>nivel-invalido</c>, <c>umbral-invalido</c>, <c>muestreo-invalido</c>, <c>tope-invalido</c>,
    /// <c>espera-invalida</c>, <c>tipo-invalido</c> (no es un tipo analizable con IA), <c>area-invalida</c> (no es un área de la
    /// empresa), <c>automatico-sin-alcance</c> e <c>ia-no-disponible</c> (Automático exige el plan de IA activo y con llave).
    /// </summary>
    [WolverinePut("/api/v1/configuracion/revision-automatica")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<RevisionAutomaticaConfigDto> Actualizar(ActualizarRevisionAutomaticaDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var existente = await empresa.Sesion.LoadAsync<RevisionAutomaticaEmpresa>(empresa.EmpresaId, ct);
        var antes = existente is null ? null : await DtoAsync(empresa, existente, ahora, ct);
        var configuracion = existente ?? RevisionAutomaticaEmpresa.PorDefecto(empresa.EmpresaId);

        foreach (var tipo in dto.TiposAutorizados ?? [])
        {
            if (string.IsNullOrWhiteSpace(tipo) || EsquemasDeAnalisis.Para(tipo.Trim()) is null)
                throw ReglaDeNegocioException.Validacion("tipo-invalido", $"«{tipo}» no es un tipo de documento que la IA pueda analizar.");
        }

        var areasDeLaEmpresa = (await empresa.Areas().Select(a => a.Id).ToListAsync(ct)).ToHashSet();
        if ((dto.AreasAutorizadas ?? []).Any(a => !areasDeLaEmpresa.Contains(a)))
            throw ReglaDeNegocioException.Validacion("area-invalida", "Una de las áreas elegidas no existe en la empresa.");

        configuracion.Actualizar(new CambiosDeRevisionAutomatica(dto.Nivel, dto.UmbralConfianza, dto.PorcentajeMuestreo, dto.TopeMensualAprobaciones,
            dto.TiposAutorizados, dto.AreasAutorizadas, dto.EsperaIaMinutos), ahora);
        if (configuracion.Nivel == NivelDeRevisionAutomatica.Automatico)
        {
            var plan = await empresa.Sesion.LoadAsync<PlanDeIaEmpresa>(empresa.EmpresaId, ct);
            if (plan is not { PuedePagar: true })
                throw ReglaDeNegocioException.Validacion("ia-no-disponible",
                    "Para aprobar automáticamente la empresa necesita su plan de IA activo y con la llave de OpenAI (lo configura Plataforma).");
        }

        empresa.Sesion.Store(configuracion);
        var despues = await DtoAsync(empresa, configuracion, ahora, ct);
        Auditoria.Registrar(empresa.Sesion, contexto, Entidad, empresa.EmpresaId.ToString(),
            existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes, despues, ahora, empresa.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return despues;
    }

    /// <summary>
    /// Bandeja de excepciones de la empresa: documentos que el sistema no aprobó solo (<c>Excepcion</c>) y aprobaciones automáticas por
    /// confirmar (<c>Muestreo</c>). <c>estado</c>: <c>Pendientes</c> (por omisión) o <c>Resueltas</c> (las más recientes primero).
    /// Hasta 200 filas; lo más antiguo primero. 422 <c>estado-invalido</c>.
    /// </summary>
    [WolverineGet("/api/v1/revision-automatica/excepciones")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<IReadOnlyList<ExcepcionDeRevisionDto>> Excepciones(string? estado, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var resueltas = string.IsNullOrWhiteSpace(estado) || estado.Equals("Pendientes", StringComparison.OrdinalIgnoreCase)
            ? false
            : estado.Equals("Resueltas", StringComparison.OrdinalIgnoreCase)
                ? true
                : throw ReglaDeNegocioException.Validacion("estado-invalido", "El estado es Pendientes o Resueltas.");

        var expedientes = resueltas
            ? await empresa.Expedientes().Where(e => e.ConRevisionAutomatica).OrderByDescending(e => e.ActualizadoEn).Take(MaximoFilas).ToListAsync(ct)
            : await empresa.Expedientes().Where(e => e.ExcepcionesDeRevisionPendientes > 0).OrderBy(e => e.AbiertoEn).Take(MaximoFilas).ToListAsync(ct);

        var filas = new List<ExcepcionDeRevisionDto>();
        foreach (var e in expedientes)
        {
            foreach (var requisito in e.Requisitos)
            {
                foreach (var revision in requisito.Revisiones.Where(r => r.Resultado is ResultadoDeRevision.Excepcion or ResultadoDeRevision.AprobadaConMuestreo))
                {
                    if (e.RevisionAbierta(revision, requisito) == resueltas)
                        continue;
                    filas.Add(new ExcepcionDeRevisionDto(e.Id, e.Folio, e.VinculoId, new TerceroResumenDto(e.TerceroId, e.TerceroNombre, e.TerceroIdentificacion),
                        e.TipoVinculoNombre, e.Estado.ToString(), requisito.Id, requisito.Nombre, requisito.TipoCanonico, revision.SobreVersion, revision.AreaId,
                        e.BuscarArea(revision.AreaId)?.Codigo ?? string.Empty,
                        revision.Resultado == ResultadoDeRevision.Excepcion ? "Excepcion" : "Muestreo", revision.Resultado.ToString(),
                        [.. revision.Motivos.Select(m => new MotivoDeRevisionDto(m.Codigo, m.Texto))], revision.Confianza, revision.Fecha, resueltas,
                        revision.Resolucion?.Decision.ToString(), revision.Resolucion?.Por, revision.Resolucion?.Fecha));
                }
            }
        }

        return [.. (resueltas ? filas.OrderByDescending(f => f.ResueltaEn ?? f.Desde) : filas.OrderBy(f => f.Desde)).Take(MaximoFilas)];
    }

    /// <summary>
    /// Una persona del área confirma o revierte una aprobación por muestreo. Revertir anula el veredicto del sistema y devuelve el
    /// documento a revisión humana (solo con el expediente en revisión y la etapa del área activa). 404 <c>revision-no-encontrada</c>;
    /// 409 <c>revision-ya-resuelta</c>, <c>veredicto-no-vigente</c>, <c>estado-invalido</c>, <c>area-no-activa</c>; 422
    /// <c>decision-invalida</c>, <c>justificacion-requerida</c> (al revertir), <c>usuario-delegado-requerido</c>; 403
    /// <c>no-es-miembro-del-area</c>, <c>conflicto-de-intereses</c>, <c>separacion-de-funciones</c>.
    /// </summary>
    [WolverinePost("/api/v1/expedientes/{id}/requisitos/{requisitoId}/revision-automatica/resolver")]
    [Authorize(Policy = Permisos.ExpedientesRevisar)]
    public static async Task<ExpedienteDto> Resolver(Guid id, Guid requisitoId, ResolverRevisionAutomaticaDto dto, SesionDeEmpresa empresa,
        IContextoSolicitud contexto, HttpContext http, IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        var decision = dto.Decision?.Trim().ToLowerInvariant() switch
        {
            "confirmar" => DecisionHumanaDeRevision.Confirmada,
            "revertir" => DecisionHumanaDeRevision.Revertida,
            _ => throw ReglaDeNegocioException.Validacion("decision-invalida", "La decisión es Confirmar o Revertir.")
        };
        empresa.UsarMensajeria(mensajes);
        return await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var stream = await empresa.ExpedienteParaEscribirAsync(id, Peticiones.VersionEsperada(http), ct);
            var revisor = await Revisores.DelAreaAsync(empresa, contexto, dto.AreaId, ct);
            var eventos = stream.Aggregate!.ResolverRevisionAutomatica(requisitoId, dto.AreaId, decision, revisor, dto.Justificacion, reloj.GetUtcNow());
            await ExpedientesEndpoints.AnexarYGuardarAsync(empresa, stream, eventos, mensajes, protector, reloj, ct);
            var detalle = await ExpedientesEndpoints.DetalleAsync(empresa, stream.Id, ct);
            Peticiones.EscribirETag(http, detalle.Version);
            return detalle;
        });
    }

    private static async Task<RevisionAutomaticaConfigDto> DtoAsync(SesionDeEmpresa empresa, RevisionAutomaticaEmpresa? guardada, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var configuracion = guardada ?? RevisionAutomaticaEmpresa.PorDefecto(empresa.EmpresaId);
        var plan = await empresa.Sesion.LoadAsync<PlanDeIaEmpresa>(empresa.EmpresaId, ct);
        var mes = UsoRevisionAutomatica.MesDe(CalculadoraDeVigencia.Fecha(ahora));
        var uso = await empresa.Sesion.LoadAsync<UsoRevisionAutomatica>(UsoRevisionAutomatica.Clave(empresa.EmpresaId, mes), ct)
                  ?? UsoRevisionAutomatica.Nuevo(empresa.EmpresaId, mes);
        var areas = await empresa.Areas().Where(a => a.Activa).OrderBy(a => a.Codigo).ToListAsync(ct);
        var tipos = CatalogoCanonico.Todos.Where(t => EsquemasDeAnalisis.Para(t.Codigo) is not null).OrderBy(t => t.Codigo)
            .Select(t => new TipoAnalizableDto(t.Codigo, t.Nombre)).ToList();
        return new RevisionAutomaticaConfigDto(guardada is not null, configuracion.Nivel.ToString(), configuracion.UmbralConfianza, configuracion.PorcentajeMuestreo,
            configuracion.TopeMensualAprobaciones, [.. configuracion.TiposAutorizados], [.. configuracion.AreasAutorizadas], configuracion.EsperaIaMinutos,
            guardada?.ActualizadaEn, plan is { PuedePagar: true }, plan?.UmbralConfianza ?? PlanDeIaEmpresa.UmbralConfianzaInicial,
            new UsoRevisionAutomaticaDto(uso.Mes, uso.Aprobadas, uso.Muestreadas, uso.Excepciones, uso.Sugerencias, configuracion.TopeMensualAprobaciones),
            tipos, [.. areas.Select(a => new AreaDisponibleDto(a.Id, a.Codigo, a.Nombre))]);
    }
}
