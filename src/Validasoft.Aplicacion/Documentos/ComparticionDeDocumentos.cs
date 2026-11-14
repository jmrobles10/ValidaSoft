using JasperFx.Events;
using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Documentos;

/// <summary>
/// Documentos del tercero que tocan a un vínculo (globales que pide su flujo y los <c>PorVinculo</c> del vínculo) ya con
/// las comparticiones nuevas aplicadas, y las comparticiones que se registraron.
/// </summary>
public sealed record DocumentosDelVinculo(IReadOnlyList<CompartidoDto> Compartidos, IReadOnlyList<Documento> Documentos);

/// <summary>
/// Compartición de la bóveda con las empresas (B-1, ADR-0009, A-8). Nada se comparte antes de que el tercero acepte la
/// invitación. Qué recibe cada vínculo lo deciden los <b>requisitos de su flujo</b> (RQ-3, reemplaza las exigencias por
/// tipo de BO-3): al aceptar —o al abrir el expediente de un vínculo ya aceptado— la empresa recibe la versión actual de
/// cada tipo global que pide el flujo resuelto para ese vínculo (y los <c>PorVinculo</c> ya cargados para él); cuando una
/// versión nueva pasa a <c>Actual</c>, se comparte con los vínculos aceptados del tercero cuyo expediente abierto o
/// flujo vigente pide ese tipo. El tercero no excluye empresas (A-8). Las reglas viven en <see cref="Documento.Compartir"/>;
/// aquí solo se resuelven los candidatos entre tenants.
/// </summary>
public static class ComparticionDeDocumentos
{
    /// <summary>
    /// Comparte con <paramref name="vinculo"/> (ya aceptado, o aceptándose en esta misma transacción) la versión actual de
    /// cada documento global cuyo canónico está en <paramref name="codigosGlobales"/> y de sus <c>PorVinculo</c>. La sesión
    /// es la del tenant del vínculo; los streams de la bóveda se escriben con <c>ForTenant("boveda")</c> sobre la misma
    /// unidad de trabajo, cada uno con concurrencia optimista: el llamador reintenta si pierde la carrera (BO-6).
    /// </summary>
    public static async Task<DocumentosDelVinculo> CompartirConVinculoAsync(IDocumentSession sesionDelTenant, Vinculo vinculo, Guid tenantId,
        IReadOnlySet<string> codigosGlobales, OrigenComparticion origen, DateTimeOffset ahora, CancellationToken ct)
    {
        var boveda = sesionDelTenant.ForTenant(TenantsTecnicos.Boveda);
        var terceroId = vinculo.TerceroId;
        var documentos = await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);

        var compartidos = new List<CompartidoDto>();
        var resultado = new List<Documento>();
        foreach (var candidato in documentos.OrderBy(d => d.CreadoEn))
        {
            var esDelVinculo = candidato.Ambito == AmbitoDocumento.PorVinculo && candidato.VinculoId == vinculo.Id;
            var pedido = candidato.Ambito == AmbitoDocumento.Global && candidato.TipoCanonico is { } codigo && codigosGlobales.Contains(codigo);
            if (!esDelVinculo && !pedido)
                continue;

            var destino = new VinculoCandidato(vinculo.Id, tenantId, vinculo.EmpresaId, terceroId, InvitacionAceptada: true, vinculo.Estado,
                ExigeElTipo: pedido, vinculo.BovedaDisponible);
            if (candidato.Compartir(destino, origen, ahora) is null)
            {
                resultado.Add(candidato);
                continue;
            }

            // Concurrencia optimista (no FetchForExclusiveWriting: la operación por tenant tomaría el bloqueo en otra conexión
            // y el guardado de esta sesión esperaría por él). Si una confirmación cambió el documento entretanto, la operación
            // entera se reintenta y comparte la versión que quedó actual.
            var stream = await boveda.Events.FetchForWriting<Documento>(candidato.Id, ct);
            var documento = stream.Aggregate!;
            if (documento.Compartir(destino, origen, ahora) is { } evento)
            {
                stream.AppendOne(evento);
                compartidos.Add(evento.ADto(documento));
                resultado.Add(documento.Proyectar([evento]));
            }
            else
            {
                resultado.Add(documento);
            }
        }

        return new DocumentosDelVinculo(compartidos, resultado);
    }

    /// <summary>
    /// La versión actual de <paramref name="proyectado"/> (el documento con los eventos nuevos ya aplicados) se comparte con
    /// cada vínculo aceptado que la pida. Se llama con el stream ya tomado con bloqueo: los vínculos se leen después, así
    /// una aceptación simultánea o la ve en su propia transacción o la ve esta. Con <paramref name="mensajes"/> (sesión
    /// enlistada en el outbox) cada vínculo que la recibe pide adjuntarla a su expediente abierto (RQ-2).
    /// </summary>
    public static async Task<IReadOnlyList<CompartidoDto>> CompartirActualAsync(IDocumentSession sesionBoveda, IEventStream<Documento> stream,
        Documento proyectado, OrigenComparticion origen, DateTimeOffset ahora, CancellationToken ct, IMessageBus? mensajes = null,
        IContextoSolicitud? contexto = null)
    {
        if (proyectado.VersionActual is null)
            return [];

        var candidatos = await CandidatosAsync(sesionBoveda, proyectado, ct);
        var eventos = proyectado.CompartirActual(candidatos, origen, ahora);
        foreach (var evento in eventos)
            stream.AppendOne(evento);
        if (mensajes is not null && contexto is not null)
        {
            await PedirSincronizacionAsync(mensajes, eventos.Select(e => (e.TenantId, e.VinculoId)), contexto);
            if (eventos.Count > 0)
                await mensajes.PublishAsync(new CalcularVigenciasDelDocumento(proyectado.Id));
            // Análisis con IA de la versión actual y consumo de las empresas que la reciben (ADR-0012); idempotente.
            await Ia.AnalisisConIa.PedirAsync(mensajes, proyectado);
        }

        return [.. eventos.Select(e => e.ADto(proyectado))];
    }

    /// <summary>
    /// Publica en el outbox (cola local durable <c>requisitos</c>) un <see cref="SincronizarRequisitosDelVinculo"/> por
    /// vínculo: sale solo si la transacción de la bóveda se confirma (RQ-2).
    /// </summary>
    public static async Task PedirSincronizacionAsync(IMessageBus mensajes, IEnumerable<(Guid TenantId, Guid VinculoId)> vinculos,
        IContextoSolicitud contexto)
    {
        foreach (var (tenantId, vinculoId) in vinculos.Distinct())
            await mensajes.PublishAsync(new SincronizarRequisitosDelVinculo(tenantId, vinculoId, contexto.ActorTipo.ToString(), contexto.ActorId));
    }

    private static async Task<IReadOnlyList<VinculoCandidato>> CandidatosAsync(IQuerySession consulta, Documento documento, CancellationToken ct)
    {
        var terceroId = documento.TerceroId;
        if (documento.Ambito == AmbitoDocumento.PorVinculo)
        {
            var vinculoId = documento.VinculoId!.Value;
            var propio = await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.Id == vinculoId).FirstOrDefaultAsync(ct);
            return propio is null
                ? []
                : [new VinculoCandidato(propio.Id, documento.TenantId!.Value, propio.EmpresaId, propio.TerceroId, propio.InvitacionAceptada,
                    propio.Estado, ExigeElTipo: false, propio.BovedaDisponible)];
        }

        var vinculos = await consulta.Query<Vinculo>()
            .Where(v => v.AnyTenant() && v.TerceroId == terceroId && v.InvitacionAceptada)
            .ToListAsync(ct);
        if (vinculos.Count == 0)
            return [];

        var empresaIds = vinculos.Select(v => v.EmpresaId).Distinct().ToArray();
        var empresas = (await consulta.LoadManyAsync<Empresa>(ct, empresaIds)).ToDictionary(e => e.Id);
        var piden = await RequisitosDeExpediente.VinculosQuePidenAsync(consulta, vinculos, documento.TipoCanonico!, ct);

        return
        [
            .. vinculos
                .Where(v => empresas.ContainsKey(v.EmpresaId))
                .Select(v => new VinculoCandidato(v.Id, empresas[v.EmpresaId].TenantId, v.EmpresaId, v.TerceroId, v.InvitacionAceptada, v.Estado,
                    piden.Contains(v.Id), v.BovedaDisponible))
        ];
    }
}
