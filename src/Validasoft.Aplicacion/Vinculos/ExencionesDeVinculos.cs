using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// Exención del vínculo (EX-1…EX-12): la empresa decide no perseguir la renovación de un tercero ya aprobado. El dominio guarda la exención y dice si un
/// requisito está eximido (<see cref="Vinculo.ExencionDeRequisito"/>, la regla única); aquí se la aplica a lo que depende de ella: los requisitos de los
/// expedientes nuevos, el expediente que ya estaba abierto y el recálculo al terminar. Es privada de la empresa: vive en el stream del vínculo (del tenant) y
/// nunca llega a la bóveda, a otra empresa ni a una vista del tercero.
/// </summary>
public static class ExencionesDeVinculos
{
    /// <summary>
    /// Marca <c>Eximido</c> (citando la exención) los requisitos que el vínculo tiene eximidos a <paramref name="ahora"/> (EX-3). Lo usa
    /// <see cref="ProcesoDeValidacion.CalcularRequisitosAsync"/>, único punto que calcula los requisitos de un expediente nuevo o ampliado: inicial, renovación
    /// (la de un vencimiento, la periódica y la de una campaña) y actualización quedan iguales.
    /// </summary>
    public static IReadOnlyList<RequisitoCalculado> Aplicar(Vinculo vinculo, IReadOnlyList<RequisitoCalculado> requisitos, DateTimeOffset ahora)
    {
        if (vinculo.Exenciones.Count == 0)
            return requisitos;

        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        return
        [
            .. requisitos.Select(r => vinculo.ExencionDeRequisito(r.TipoRequisito, r.TipoRequisito == TipoDeRequisito.Documento ? r.TipoDocumentoTenantId : null,
                r.Seccion, hoy) is { } exencion
                ? r with { Exencion = new ExencionAplicada(exencion.Id, exencion.Justificacion, exencion.Por, ahora) }
                : r)
        ];
    }

    /// <summary>Filtra las secciones del flujo que el vínculo tiene eximidas: su vigencia propia no cuenta para el vencimiento del vínculo (EX-4).</summary>
    public static IEnumerable<RequisitoDelFlujo> SinSeccionesEximidas(Vinculo vinculo, IEnumerable<RequisitoDelFlujo> requisitos, DateOnly hoy) =>
        requisitos.Where(r => !(r.EsSeccion && vinculo.ExencionDeSeccion(r.Seccion, hoy) is not null));

    /// <summary>
    /// Al registrarse la exención con un expediente abierto, exime en él (con el mismo efecto de <see cref="Expediente.EximirRequisito"/>: sus veredictos dejan de
    /// contar y, si era lo último, el expediente pasa a revisión o se aprueba, con el vínculo, la vigencia y el certificado en la misma transacción) lo que la
    /// exención cubre y aún no está aprobado. Sin expediente abierto o sin nada cubierto, no hace nada.
    /// </summary>
    public static async Task AplicarAlExpedienteAbiertoAsync(IDocumentSession sesion, IEventStream<Vinculo> vinculoStream, ExencionDeVinculoRegistrada exencion,
        EmisorDeCertificados certificados, DateTimeOffset ahora, CancellationToken ct)
    {
        if (vinculoStream.Aggregate?.ExpedienteAbiertoId is not { } expedienteId)
            return;
        var stream = await sesion.Events.FetchForExclusiveWriting<Expediente>(expedienteId, ct);
        if (stream.Aggregate is not { EstaAbierto: true } expediente)
            return;

        var cubiertos = expediente.Requisitos
            .Where(r => !r.Eximido && r.Estado != EstadoRequisito.Aprobado && Cubre(exencion, r))
            .Select(r => r.Id)
            .ToList();
        if (cubiertos.Count == 0)
            return;

        var estado = expediente;
        var eventos = new List<object>();
        foreach (var id in cubiertos)
        {
            // Con el último requisito el expediente puede cerrarse solo: lo que sigue ya no está abierto.
            if (!estado.EstaAbierto || estado.BuscarRequisito(id) is not { Eximido: false } requisito || requisito.Estado == EstadoRequisito.Aprobado)
                continue;
            var nuevos = estado.EximirRequisito(id, exencion.Por, $"Exención del vínculo: {exencion.Justificacion}", ahora, exencion.ExencionId);
            eventos.AddRange(nuevos);
            estado = Expediente.Reproducir(estado, nuevos);
        }

        if (eventos.Count > 0)
            await ProcesoDeValidacion.AnexarAsync(sesion, stream, eventos, null, certificados, ahora, ct, vinculoStream);
    }

    private static bool Cubre(ExencionDeVinculoRegistrada exencion, RequisitoDeExpediente requisito) =>
        exencion.Todos || exencion.Objetivos.Any(o => o.Cubre(requisito.TipoRequisito,
            requisito.TipoRequisito == TipoDeRequisito.Documento ? requisito.TipoDocumentoTenantId : null, requisito.Seccion));

    /// <summary>
    /// Terminó una exención (se revocó o venció): el vínculo vuelve a regirse por las vigencias (EX-4). Los documentos cuya versión ya venció para él mientras
    /// la exención regía (la bóveda no los marcó vencidos para no avisar ni vencer al vínculo) se vencen ahora —cada uno por su mensaje de siempre— y de ahí sale su
    /// renovación; el resto (la vigencia periódica, los avisos) lo decide el job de vigencias con las fechas que ya tiene el vínculo. Idempotente: lo que ya
    /// venció o ya tiene su renovación no se duplica.
    /// </summary>
    public static async Task ReevaluarAlTerminarAsync(IDocumentSession sesion, IMessageBus mensajes, Vinculo vinculo, Guid tenantId, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        var terceroId = vinculo.TerceroId;
        var documentos = await sesion.ForTenant(TenantsTecnicos.Boveda).Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
        foreach (var documento in documentos.Where(d => d.VersionActual is not null))
        {
            var version = documento.Buscar(documento.VersionActual!.Value);
            if (version?.VigenciaPara(vinculo.Id) is not { VigenteHasta: { } hasta } vigencia)
                continue;

            if (vigencia.VencidaEn is not null)
                await mensajes.PublishAsync(new Expedientes.RenovarPorVencimiento(tenantId, vinculo.Id, documento.Id));
            else if (hoy > vigencia.FinDeGracia)
                await mensajes.PublishAsync(new VencerVersionDeDocumento(documento.Id, version.Numero, vinculo.Id, hasta, vigencia.DiasGracia));
        }
    }

    // ---------- Mapeos ----------

    public static ExencionDeVinculoDto ADto(this ExencionDeVinculo e, DateOnly hoy) =>
        new(e.Id, e.Todos, [.. e.Objetivos.Select(ADto)], e.Justificacion, e.Hasta, e.Por, e.CreadaEn, e.EstadoEn(hoy).ToString(), e.RevocadaPor,
            e.JustificacionDeRevocacion, e.RevocadaEn, e.VencidaEn);

    public static ObjetivoDeExencionDto ADto(this ObjetivoDeExencion o) => new(o.Tipo.ToString(), o.TipoDocumentoTenantId, o.Seccion, o.Codigo, o.Nombre);

    public static ExencionResumenDto Resumen(this ExencionDeVinculo e) => new(e.Id, e.Todos, e.Justificacion, e.Hasta);

    /// <summary>Las exenciones del vínculo como las ve la empresa: las vigentes primero, luego las demás, de la más reciente a la más antigua.</summary>
    public static IReadOnlyList<ExencionDeVinculoDto> ADtos(this Vinculo vinculo, DateOnly hoy) =>
    [
        .. vinculo.Exenciones.OrderByDescending(e => e.VigenteEn(hoy)).ThenByDescending(e => e.CreadaEn).Select(e => e.ADto(hoy))
    ];
}

/// <summary>
/// Endpoints de la exención del vínculo (EX-5): eximir y revocar piden <c>vinculos:eximir</c> (el administrador o una llave a la que se le marque); listar,
/// <c>vinculos:leer</c>. Un vínculo de otra empresa responde 404 (T-4): la exención es privada de la empresa, también dentro de un mismo grupo.
/// </summary>
public static class ExencionesEndpoints
{
    /// <summary>
    /// Exime al vínculo (201): de todo (<c>todos</c>) o de tipos de documento y secciones concretos, con justificación obligatoria y fecha final opcional. Lo que
    /// el vínculo tiene abierto y la exención cubre se exime en el acto (un expediente de renovación, por ejemplo, puede cerrarse aprobado). 409:
    /// <c>vinculo-bloqueado</c>, <c>estado-terminal</c>, <c>vinculo-sin-aprobacion</c>, <c>exencion-superpuesta</c>; 422: <c>justificacion-requerida</c>,
    /// <c>exencion-sin-alcance</c>, <c>exencion-alcance-invalido</c>, <c>objetivo-invalido</c>, <c>tipo-documento-invalido</c>, <c>seccion-invalida</c>,
    /// <c>fecha-invalida</c>.
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/exenciones")]
    [Authorize(Policy = Permisos.VinculosEximir)]
    public static async Task<IResult> Eximir(Guid id, EximirVinculoDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, IMessageBus mensajes,
        TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        var creada = await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            var stream = await empresa.VinculoParaEscribirAsync(id, null, ct);
            var vinculo = stream.Aggregate!;
            var objetivos = await ObjetivosAsync(empresa, dto, ct);
            var evento = vinculo.Eximir(dto.Todos, objetivos, dto.Justificacion, dto.Hasta, contexto.ActorId, ahora);
            stream.AppendOne(evento);

            // Lo abierto se pone al día en la misma transacción (EX-3) y, si no queda nada que renovar, el vínculo vuelve a Activo (EX-4).
            await ExencionesDeVinculos.AplicarAlExpedienteAbiertoAsync(empresa.Sesion, stream, evento, empresa.Certificados, ahora, ct);
            if (stream.Aggregate!.RestablecerPorExencion(ahora) is { } restablecido)
                stream.AppendOne(restablecido);
            await empresa.Sesion.SaveChangesAsync(ct);
            return (await empresa.CargarVinculoAsync(id, ct)).Exenciones.Single(e => e.Id == evento.ExencionId).ADto(empresa.Hoy);
        });

        return Results.Created($"/api/v1/vinculos/{id}/exenciones/{creada.Id}", creada);
    }

    /// <summary>
    /// Revoca una exención vigente con justificación (200): el vínculo vuelve a regirse por las vigencias desde ya (los documentos que vencieron mientras tanto
    /// vencen y abren su renovación). 404 <c>exencion-no-encontrada</c>; 409 <c>exencion-ya-revocada</c>, <c>exencion-vencida</c>, <c>estado-terminal</c>.
    /// </summary>
    [WolverinePost("/api/v1/vinculos/{id}/exenciones/{exencionId}/revocar")]
    [Authorize(Policy = Permisos.VinculosEximir)]
    public static async Task<ExencionDeVinculoDto> Revocar(Guid id, Guid exencionId, JustificacionDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        empresa.UsarMensajeria(mensajes);
        return await empresa.ReintentarAnteCarreraAsync(async () =>
        {
            var ahora = reloj.GetUtcNow();
            var stream = await empresa.VinculoParaEscribirAsync(id, null, ct);
            var evento = stream.Aggregate!.RevocarExencion(exencionId, dto.Justificacion, contexto.ActorId, ahora);
            stream.AppendOne(evento);

            var tenantId = contexto.TenantRequerido();
            await ExencionesDeVinculos.ReevaluarAlTerminarAsync(empresa.Sesion, mensajes, stream.Aggregate!, tenantId, ahora, ct);
            // La vigencia periódica y sus avisos se vuelven a mirar ya, sin esperar al siguiente barrido.
            await mensajes.PublishAsync(new RevisarVigenciaDeVinculo(tenantId, id));
            await empresa.Sesion.SaveChangesAsync(ct);
            return (await empresa.CargarVinculoAsync(id, ct)).Exenciones.Single(e => e.Id == exencionId).ADto(empresa.Hoy);
        });
    }

    /// <summary>Las exenciones del vínculo (vigentes primero); <c>vigentes=true</c> solo las que rigen hoy.</summary>
    [WolverineGet("/api/v1/vinculos/{id}/exenciones")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<IReadOnlyList<ExencionDeVinculoDto>> Listar(Guid id, bool? vigentes, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var vinculo = await empresa.CargarVinculoAsync(id, ct);
        var hoy = empresa.Hoy;
        return [.. vinculo.ADtos(hoy).Where(e => vigentes != true || e.Estado == nameof(EstadoDeExencion.Vigente))];
    }

    /// <summary>
    /// Los objetivos de la petición con su código y nombre de hoy: el tipo de documento debe existir en el grupo y la sección, en el catálogo de la ficha. 422
    /// <c>objetivo-invalido</c>, <c>tipo-documento-invalido</c>, <c>seccion-invalida</c>.
    /// </summary>
    private static async Task<IReadOnlyList<ObjetivoDeExencion>> ObjetivosAsync(SesionDeEmpresa empresa, EximirVinculoDto dto, CancellationToken ct)
    {
        var resueltos = new List<ObjetivoDeExencion>();
        foreach (var o in dto.Objetivos ?? [])
        {
            if (!Enum.TryParse<TipoDeRequisito>(o.Tipo, true, out var tipo) || !Enum.IsDefined(tipo))
                throw ReglaDeNegocioException.Validacion("objetivo-invalido", "Cada objetivo es un Documento (con su tipo de documento) o una Seccion de la ficha.");

            if (tipo == TipoDeRequisito.Documento)
            {
                var tipoDocumento = o.TipoDocumentoTenantId is { } tipoId ? await empresa.Sesion.LoadAsync<TipoDocumentoTenant>(tipoId, ct) : null;
                resueltos.Add(tipoDocumento is not null
                    ? ObjetivoDeExencion.DeDocumento(tipoDocumento.Id, tipoDocumento.Codigo, tipoDocumento.Nombre)
                    : throw ReglaDeNegocioException.Validacion("tipo-documento-invalido", "Ese tipo de documento no existe en el grupo."));
            }
            else
            {
                resueltos.Add(CatalogoDeSecciones.Buscar(o.Seccion) is { } definicion
                    ? ObjetivoDeExencion.DeSeccion(definicion.Codigo, definicion.Nombre)
                    : throw ReglaDeNegocioException.Validacion("seccion-invalida",
                        $"La sección '{o.Seccion}' no existe. Secciones: {string.Join(", ", CatalogoDeSecciones.Todas.Select(s => s.Codigo))}."));
            }
        }

        return resueltos;
    }
}
