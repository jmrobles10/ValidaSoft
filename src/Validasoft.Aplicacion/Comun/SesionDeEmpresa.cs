using Marten;
using Validasoft.Aplicacion.Certificados;
using JasperFx.Events;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;
using Wolverine;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Acceso a los datos de la empresa activa (T-3): todo lo que devuelve ya viene filtrado por
/// <c>EmpresaId</c>, y un recurso de otra empresa responde 404 (T-4), nunca 403.
/// </summary>
public sealed class SesionDeEmpresa(ISesiones sesiones, IContextoSolicitud contexto, ReglasDeCarga reglas, TimeProvider reloj,
    EmisorDeCertificados certificados) : IAsyncDisposable
{
    private IDocumentSession? _sesion;
    private IMessageBus? _mensajes;

    /// <summary>Emite el certificado cuando una decisión deja el vínculo <c>Activo</c> (CE-2).</summary>
    public EmisorDeCertificados Certificados => certificados;

    /// <summary>Límites globales de carga (para las reglas efectivas de los tipos de documento, TD-1).</summary>
    public ReglasDeCarga Reglas => reglas;

    /// <summary>Hoy en UTC (para el estado de las vigencias, TD-6).</summary>
    public DateOnly Hoy => Dominio.Documentos.CalculadoraDeVigencia.Fecha(reloj.GetUtcNow());

    /// <summary>Lectura del tenant técnico <c>boveda</c> (documentos de los terceros).</summary>
    public IQuerySession ConsultaBoveda() => sesiones.ConsultaBoveda();

    public IDocumentSession Sesion => _sesion ??= _mensajes is null ? sesiones.DelTenant() : sesiones.DelTenant(_mensajes);

    /// <summary>
    /// Enlista la sesión en el outbox de <paramref name="mensajes"/> (debe llamarse antes del primer uso de
    /// <see cref="Sesion"/>): los mensajes publicados se despachan solo si la transacción se confirma.
    /// </summary>
    public void UsarMensajeria(IMessageBus mensajes)
    {
        if (_sesion is not null)
            throw new InvalidOperationException("La sesión de la empresa ya se abrió sin mensajería.");
        _mensajes = mensajes;
    }

    public Guid EmpresaId => contexto.EmpresaRequerida();

    /// <summary>Id del usuario que consulta si es una persona (identidad delegada o su sesión); nulo para integraciones.</summary>
    public string? UsuarioConsultante =>
        contexto.ActorTipo == ActorTipo.Usuario && Guid.TryParse(contexto.ActorId, out var usuarioId) ? usuarioId.ToString() : null;

    public IQueryable<Vinculo> Vinculos() => Sesion.Query<Vinculo>().Where(v => v.EmpresaId == EmpresaId);

    public async Task<Vinculo> CargarVinculoAsync(Guid id, CancellationToken ct)
    {
        var vinculo = await Sesion.LoadAsync<Vinculo>(id, ct);
        return vinculo is not null && vinculo.EmpresaId == EmpresaId
            ? vinculo
            : throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo en la empresa.");
    }

    public IQueryable<Area> Areas() => Sesion.Query<Area>().Where(a => a.EmpresaId == EmpresaId);

    public async Task<Area> CargarAreaAsync(Guid id, CancellationToken ct)
    {
        var area = await Sesion.LoadAsync<Area>(id, ct);
        return area is not null && area.EmpresaId == EmpresaId
            ? area
            : throw ReglaDeNegocioException.NoEncontrado("area-no-encontrada", "No existe esa área en la empresa.");
    }

    /// <summary>Carga el stream para escribir con concurrencia optimista (<c>If-Match</c> si llega).</summary>
    public async Task<IEventStream<Vinculo>> VinculoParaEscribirAsync(Guid id, long? versionEsperada, CancellationToken ct)
    {
        var stream = versionEsperada is null
            ? await Sesion.Events.FetchForWriting<Vinculo>(id, ct)
            : await Sesion.Events.FetchForWriting<Vinculo>(id, versionEsperada.Value, ct);

        if (stream.Aggregate is null || stream.Aggregate.EmpresaId != EmpresaId)
            throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo en la empresa.");

        return stream;
    }

    public IQueryable<Flujo> Flujos() => Sesion.Query<Flujo>().Where(f => f.EmpresaId == EmpresaId);

    public async Task<Flujo> CargarFlujoAsync(Guid id, CancellationToken ct)
    {
        var flujo = await Sesion.LoadAsync<Flujo>(id, ct);
        return flujo is not null && flujo.EmpresaId == EmpresaId
            ? flujo
            : throw ReglaDeNegocioException.NoEncontrado("flujo-no-encontrado", "No existe ese flujo en la empresa.");
    }

    public IQueryable<Expediente> Expedientes() => Sesion.Query<Expediente>().Where(e => e.EmpresaId == EmpresaId);

    public async Task<Expediente> CargarExpedienteAsync(Guid id, CancellationToken ct)
    {
        var expediente = await Sesion.LoadAsync<Expediente>(id, ct);
        return expediente is not null && expediente.EmpresaId == EmpresaId
            ? expediente
            : throw ReglaDeNegocioException.NoEncontrado("expediente-no-encontrado", "No existe ese expediente en la empresa.");
    }

    /// <summary>Stream del expediente para escribir con concurrencia optimista (<c>If-Match</c> si llega).</summary>
    public async Task<IEventStream<Expediente>> ExpedienteParaEscribirAsync(Guid id, long? versionEsperada, CancellationToken ct)
    {
        // Sin If-Match, las decisiones sobre un mismo expediente se serializan con un bloqueo de fila del stream (FL-12):
        // las áreas revisan en paralelo y dos veredictos simultáneos no deben responder 412 a quien no pidió esa versión.
        // Con If-Match se conserva la concurrencia optimista que el cliente pidió.
        var stream = versionEsperada is null
            ? await Sesion.Events.FetchForExclusiveWriting<Expediente>(id, ct)
            : await Sesion.Events.FetchForWriting<Expediente>(id, versionEsperada.Value, ct);

        if (stream.Aggregate is null || stream.Aggregate.EmpresaId != EmpresaId)
            throw ReglaDeNegocioException.NoEncontrado("expediente-no-encontrado", "No existe ese expediente en la empresa.");

        return stream;
    }

    public async ValueTask DisposeAsync()
    {
        if (_sesion is not null)
            await _sesion.DisposeAsync();
    }
}
