using Marten;
using Microsoft.AspNetCore.Authorization;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Configuracion;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Bandeja de revalidaciones de la empresa activa (RV-5, docs/06 <c>/revalidaciones</c>): los documentos de los
/// expedientes de actualización abiertos que la empresa aún no aprueba, con la versión que aceptó antes, la actual, la
/// comparación de la bóveda y el plazo. Se calcula al consultar (no es proyección): son pocos y cruzan expediente,
/// certificado y bóveda. Un usuario delegado ve solo los documentos que revisa alguna de sus áreas.
/// </summary>
public static class RevalidacionesEndpoints
{
    /// <summary>Tope de la aprobación en lote.</summary>
    public const int MaximoPorLote = 100;

    /// <summary>Intentos de cada veredicto del lote cuando pierde una carrera de concurrencia.</summary>
    private const int ReintentosPorCarrera = 5;

    public const string ObservacionDeLote = "Aprobada en lote: sin cambios frente a la versión aceptada.";

    private static readonly EstadoExpediente[] Abiertos = [EstadoExpediente.Abierto, EstadoExpediente.EnRevision, EstadoExpediente.Observado];

    [WolverineGet("/api/v1/revalidaciones")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<PaginaDto<RevalidacionDto>> Listar(SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj, Guid? area,
        int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 50, 1, 200);
        var desde = Peticiones.LeerDesplazamiento(cursor);
        var ahora = reloj.GetUtcNow();

        var abiertos = await empresa.Expedientes()
            .Where(e => e.Tipo == TipoExpediente.Actualizacion && e.Estado.IsOneOf(Abiertos))
            .ToListAsync(ct);
        var mias = await AreasDelUsuarioAsync(empresa, contexto, ct);

        var pendientes = abiertos
            .SelectMany(e => e.Requisitos
                .Where(r => !r.Eximido && r.VersionActual is not null && r.Estado != EstadoRequisito.Aprobado)
                .Where(r => area is not { } a || r.LoRevisa(a))
                .Where(r => mias is null || r.AreasQueRevisan.Any(mias.Contains))
                .Select(r => (Expediente: e, Requisito: r)))
            .OrderBy(x => x.Expediente.AbiertoEn).ThenBy(x => x.Expediente.Folio).ThenBy(x => x.Requisito.Nombre)
            .ToList();
        var pagina = pendientes.Skip(desde).Take(tamano).ToList();
        if (pagina.Count == 0)
            return new PaginaDto<RevalidacionDto>([], null);

        var vinculos = pagina.Select(x => x.Expediente.VinculoId).Distinct().ToArray();
        var aprobados = (await empresa.Expedientes().Where(e => e.VinculoId.IsOneOf(vinculos) && e.Estado == EstadoExpediente.Aprobado).ToListAsync(ct))
            .GroupBy(e => e.VinculoId)
            .ToDictionary(g => g.Key, g => g.OrderByDescending(e => e.ActualizadoEn).First());
        var certificados = (await empresa.Sesion.LoadManyAsync<Certificado>(ct, vinculos.Select(Certificado.IdPara).ToArray()))
            .ToDictionary(c => c.VinculoId);
        await using var boveda = empresa.ConsultaBoveda();
        var documentos = (await boveda.LoadManyAsync<Documento>(ct, pagina.Select(x => x.Requisito.VersionActual!.DocumentoId).Distinct().ToArray()))
            .ToDictionary(d => d.Id);

        var filas = pagina.Select(x =>
        {
            var (e, r) = x;
            var actual = r.VersionActual!;
            var previa = aprobados.TryGetValue(e.VinculoId, out var aprobado) ? Revalidacion.AprobadoEn(aprobado, r) : r.VersionAceptadaPrevia;
            var comparacion = documentos.GetValueOrDefault(actual.DocumentoId)?.Buscar(actual.Numero)?.Hechos?.Comparacion;
            var plazo = Revalidacion.PlazoHasta(e);
            var certificado = certificados.GetValueOrDefault(e.VinculoId);
            return new RevalidacionDto(e.Id, e.Folio, e.VinculoId, r.Id, new TerceroResumenDto(e.TerceroId, e.TerceroNombre, e.TerceroIdentificacion),
                new TipoVinculoResumenDto(e.TipoVinculoId, e.TipoVinculoCodigo, e.TipoVinculoNombre), r.Codigo, r.Nombre,
                previa is null ? null : new VersionResumenDto(previa.Numero, previa.CargadaEn), new VersionResumenDto(actual.Numero, actual.CargadaEn),
                (comparacion ?? Comparacion.Distinta).ToString(), Revalidacion.AprobableEnLote(comparacion), r.Estado.ToString(), r.AreasQueRevisan,
                e.AbiertoEn, plazo, Math.Max(0, (int)Math.Ceiling((plazo - ahora).TotalDays)),
                certificado is { Estado: EstadoCertificado.ConPendientes, PendientesBloqueantes: true });
        });
        var siguiente = pendientes.Count > desde + tamano ? Peticiones.EscribirDesplazamiento(desde + tamano) : null;
        return new PaginaDto<RevalidacionDto>([.. filas], siguiente);
    }

    /// <summary>
    /// Aprobación en lote (A-18): cada revalidación se aprueba con un veredicto propio del revisor en cada área suya que
    /// la revisa, como si la aprobara una a una. Solo las que la bóveda ve sin cambios (<c>Identica</c> o
    /// <c>SinCambios</c>); las demás vuelven en <c>fallidas</c> con su código y no frenan al resto.
    /// </summary>
    [WolverinePost("/api/v1/revalidaciones/aprobar")]
    [Authorize(Policy = Permisos.ExpedientesRevisar)]
    public static async Task<ResultadoDeLoteDto> Aprobar(AprobarRevalidacionesDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        var items = dto.Revalidaciones ?? [];
        if (items.Count == 0)
            throw ReglaDeNegocioException.Validacion("lote-vacio", "Elige al menos una revalidación.");
        if (items.Count > MaximoPorLote)
            throw ReglaDeNegocioException.Validacion("lote-demasiado-grande", $"Se aprueban hasta {MaximoPorLote} revalidaciones por lote.");
        // La mensajería se fija antes de abrir la sesión: los veredictos pueden cerrar el expediente y avisar al tercero.
        empresa.UsarMensajeria(mensajes);
        var mias = await AreasDelUsuarioAsync(empresa, contexto, ct)
                   ?? throw ReglaDeNegocioException.Validacion("usuario-delegado-requerido",
                       "Los veredictos los emite una persona: actúa con la identidad delegada (X-Usuario-Delegado) o con su sesión.");

        await using var boveda = empresa.ConsultaBoveda();
        var aprobadas = 0;
        var fallidas = new List<FalloDeLoteDto>();
        foreach (var item in items.DistinctBy(i => (i.ExpedienteId, i.RequisitoId)))
        {
            try
            {
                await AprobarUnaAsync(empresa, contexto, boveda, mias, item, mensajes, protector, reloj, ct);
                aprobadas++;
            }
            catch (ReglaDeNegocioException ex)
            {
                fallidas.Add(new FalloDeLoteDto(item.ExpedienteId, item.RequisitoId, ex.Codigo, ex.Message));
            }
            catch (Exception ex) when (Peticiones.EsCarrera(ex))
            {
                // Siguió perdiendo la carrera tras los reintentos: falla esta, no el lote.
                await empresa.DescartarAsync();
                fallidas.Add(new FalloDeLoteDto(item.ExpedienteId, item.RequisitoId, "version-desactualizada",
                    "La revalidación cambió mientras se aprobaba; vuelve a intentarlo."));
            }
        }

        return new ResultadoDeLoteDto(aprobadas, fallidas);
    }

    private static async Task AprobarUnaAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, IQuerySession boveda, IReadOnlySet<Guid> mias,
        RevalidacionAAprobarDto item, IMessageBus mensajes, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        var expediente = await empresa.CargarExpedienteAsync(item.ExpedienteId, ct);
        if (expediente is not { Tipo: TipoExpediente.Actualizacion, EstaAbierto: true })
            throw ReglaDeNegocioException.Conflicto("no-es-revalidacion", $"El expediente {expediente.Folio} no es una revalidación abierta.");
        var requisito = expediente.Requisitos.FirstOrDefault(r => r.Id == item.RequisitoId)
                        ?? throw ReglaDeNegocioException.NoEncontrado("requisito-no-encontrado", "Ese requisito no está en el expediente.");
        if (requisito.VersionActual is not { } actual || actual.Numero != item.SobreVersion)
            throw ReglaDeNegocioException.Conflicto("version-superada", $"{requisito.Nombre} ya no está en la versión {item.SobreVersion}.");

        var documento = await boveda.LoadAsync<Documento>(actual.DocumentoId, ct);
        if (!Revalidacion.AprobableEnLote(documento?.Buscar(actual.Numero)?.Hechos?.Comparacion))
            throw ReglaDeNegocioException.Validacion("revalidacion-con-cambios",
                $"La versión {actual.Numero} de {requisito.Nombre} no se puede aprobar en lote: revísala en su expediente.");

        var areas = requisito.AreasQueRevisan.Where(mias.Contains).ToList();
        if (areas.Count == 0)
            throw ReglaDeNegocioException.Prohibido("no-es-miembro-del-area", $"Ninguna de tus áreas revisa {requisito.Nombre}.");

        // Un veredicto por cada área tuya que aún no lo aprobó; basta con que una lo acepte para contar la revalidación.
        ReglaDeNegocioException? primera = null;
        var alguna = false;
        foreach (var areaId in areas)
        {
            try
            {
                // El veredicto que cierra la revalidación reemite el certificado, que otro proceso puede estar tocando
                // (su PDF): si pierde esa carrera se reintenta con una sesión nueva.
                for (var intento = 1; ; intento++)
                {
                    try
                    {
                        var stream = await empresa.ExpedienteParaEscribirAsync(item.ExpedienteId, null, ct);
                        await ExpedientesEndpoints.AsegurarVinculoNoBloqueadoAsync(empresa, stream.Aggregate!, ct);
                        var revisor = await Revisores.DelAreaAsync(empresa, contexto, areaId, ct);
                        var eventos = stream.Aggregate!.EmitirVeredictoDeRequisito(item.RequisitoId, areaId, revisor, Veredicto.Aprobado,
                            item.SobreVersion, [], ObservacionDeLote, reloj.GetUtcNow());
                        await ExpedientesEndpoints.AnexarYGuardarAsync(empresa, stream, eventos, mensajes, protector, reloj, ct);
                        break;
                    }
                    catch (Exception ex) when (intento < ReintentosPorCarrera && Peticiones.EsCarrera(ex))
                    {
                        await empresa.DescartarAsync();
                        await Task.Delay(Random.Shared.Next(5, 25 * intento), ct);
                    }
                }
                alguna = true;
            }
            catch (ReglaDeNegocioException ex)
            {
                primera ??= ex;
            }
        }

        if (!alguna)
            throw primera!;
    }

    /// <summary>Áreas de la empresa donde el usuario es miembro; nulo si actúa una integración (ve todo, no decide).</summary>
    private static async Task<IReadOnlySet<Guid>?> AreasDelUsuarioAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, CancellationToken ct)
    {
        if (contexto.ActorTipo != ActorTipo.Usuario || !Guid.TryParse(contexto.ActorId, out var usuarioId))
            return null;
        return (await empresa.Areas().Where(a => a.Miembros.Contains(usuarioId)).Select(a => a.Id).ToListAsync(ct)).ToHashSet();
    }
}
