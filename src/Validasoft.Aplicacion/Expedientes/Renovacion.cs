using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Certificados;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Expedientes;

/// <summary>
/// Lo pide la bóveda al vencer una versión para un vínculo (al pasar la gracia, TD-6): si el vínculo pide ese documento
/// como obligatorio, vence y se abre su renovación (RN-1).
/// </summary>
public sealed record RenovarPorVencimiento(Guid TenantId, Guid VinculoId, Guid DocumentoId);

/// <summary>
/// Renovación por vencimiento (RN-1, docs/02 §8.2, docs/03 §5.2). Un documento <b>obligatorio</b> del flujo que vence para
/// un vínculo ya aprobado lo deja <c>Vencido</c> y, si no tiene expediente abierto, abre un expediente de
/// <c>Renovacion</c> solo con ese documento, que nace pendiente: la versión vencida no lo cubre. El tercero sube una
/// versión nueva (que revisan las áreas) o, si el tipo lo permite, ratifica que no cambió (RN-2). Al aprobarse, el vínculo
/// vuelve a <c>Activo</c> y el certificado se reemite. Un documento opcional que vence no cambia el vínculo.
/// </summary>
public static class RenovarPorVencimientoHandler
{
    public static async Task Handle(RenovarPorVencimiento mensaje, IDocumentStore store, EmisorDeCertificados certificados, TimeProvider reloj,
        CancellationToken ct)
    {
        await using var sesion = store.LightweightSession(mensaje.TenantId.ToString("N"));
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", "vencimiento");
        var vinculoStream = await sesion.Events.FetchForWriting<Vinculo>(mensaje.VinculoId, ct);
        if (vinculoStream.Aggregate is not { TuvoAprobacion: true } vinculo ||
            vinculo.Estado is not (EstadoVinculo.Activo or EstadoVinculo.PorVencer or EstadoVinculo.Vencido))
            return;

        var documento = await sesion.ForTenant(TenantsTecnicos.Boveda).LoadAsync<Documento>(mensaje.DocumentoId, ct);
        var flujo = await ProcesoDeValidacion.ResolverFlujoAsync(sesion, vinculo, null, ct);
        if (documento is null || flujo is null)
            return;

        var pedidos = await RequisitosDeExpediente.DelFlujoAsync(sesion, flujo.Version, await RequisitosDeExpediente.ContextoAsync(sesion, vinculo, ct), ct);
        var vencidos = pedidos.Where(p => p.Requisito.Obligatorio && Pide(p, documento, vinculo.Id)).ToList();
        if (vencidos.Count == 0)
            return;

        // Lo que el vínculo tiene eximido (EX-4) no lo vence ni abre su renovación: la regla única de las exenciones. Si todo lo vencido está eximido, no pasa nada;
        // si hay otra cosa, el vínculo vence por ella y lo eximido va en la renovación ya Eximido (EX-3), sin pedírselo al tercero.
        var ahora = reloj.GetUtcNow();
        var hoy = CalculadoraDeVigencia.Fecha(ahora);
        var pendientes = vencidos.Where(p => vinculo.ExencionDeDocumento(p.Tipo.Id, hoy) is null).ToList();
        if (pendientes.Count == 0)
            return;

        if (vinculo.Vencer(pendientes[0].Tipo.Nombre, ahora) is { } vencido)
        {
            vinculoStream.AppendOne(vencido);
            vinculo.Apply(vencido);
        }

        if (vinculo.ExpedienteAbiertoId is null)
            await ProcesoDeValidacion.AbrirAsync(sesion, vinculoStream, flujo, null, certificados, ahora, ct, tipoExpediente: TipoExpediente.Renovacion,
                soloRequisitos: vencidos.Select(p => p.Requisito.Id).ToHashSet(), actor: new ActorDeExpediente(ActorTipo.Sistema, "vencimiento"));
        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>¿El requisito pedido es el de ese documento? Global por canónico; <c>PorVinculo</c> por tipo y vínculo.</summary>
    private static bool Pide(RequisitoPedido pedido, Documento documento, Guid vinculoId) =>
        documento.Ambito == AmbitoDocumento.Global
            ? pedido.Tipo.Ambito == AmbitoDocumento.Global && pedido.Tipo.TipoCanonico is not null && pedido.Tipo.TipoCanonico == documento.TipoCanonico
            : documento.VinculoId == vinculoId && documento.TipoDocumentoTenantId == pedido.Tipo.Id;
}

/// <summary>Ratificación del tercero en una renovación (RN-2).</summary>
public static class RatificacionEndpoints
{
    /// <summary>
    /// El tercero declara que el documento vencido no cambió (condición única I-14): el tipo canónico lo permite, la
    /// versión es la actual y no trae una fecha de vencimiento ya cumplida. La bóveda registra la ratificación y recalcula la
    /// vigencia desde hoy; el requisito de la renovación queda cubierto y sus áreas lo aprueban sin revisarlo (A-7). Si era
    /// lo único, la renovación se aprueba, el vínculo vuelve a <c>Activo</c> y el certificado se reemite. En una sección de la
    /// ficha (renovación periódica del vínculo, VV-1) ratificar es confirmar que su información sigue igual: no cambia ningún dato
    /// de la ficha, queda como <c>RequisitoRatificado</c> en el expediente y la sección vigente completa cubre el requisito.
    /// </summary>
    [WolverinePost("/public/v1/expedientes/{id}/requisitos/{requisitoId}/ratificar")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<ExpedientePublicoDto> Ratificar(Guid id, Guid requisitoId, HttpContext http, ISesiones sesiones, IMessageBus mensajes,
        ReglasDeCarga reglas, EmisorDeCertificados certificados, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User, GrupoDeAcceso.Relacion);
        Expediente? ubicado;
        await using (var consulta = sesiones.ConsultaGlobal())
            ubicado = await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.Id == id).SingleOrDefaultAsync(ct);
        var enSesion = ubicado is null || ubicado.TerceroId != tercero.TerceroId
            ? null
            : tercero.Vinculos.FirstOrDefault(v => v.VinculoId == ubicado.VinculoId);
        if (ubicado is null || enSesion is null)
            throw ReglaDeNegocioException.NoEncontrado("expediente-no-encontrado", "No existe ese expediente.");

        var requisito = ubicado.Requisitos.FirstOrDefault(r => r.Id == requisitoId)
                        ?? throw ReglaDeNegocioException.NoEncontrado("requisito-no-encontrado", "Ese requisito no está en el expediente.");
        var ahora = reloj.GetUtcNow();
        var por = $"tercero:{tercero.Correo}";

        // La bóveda primero: la ratificación y la vigencia recalculada deben estar antes de cerrar la renovación, porque el
        // certificado reemitido toma la vigencia de cada documento. Una sección de la ficha no escribe en la bóveda.
        Guid? documentoId = null;
        VersionDeRequisito version;
        if (requisito.EsSeccion)
        {
            version = await VersionDeSeccionAsync(sesiones, ubicado, requisito, ct);
            ubicado.RatificarRequisito(requisitoId, version, por, ahora);
        }
        else
        await using (var boveda = sesiones.Boveda())
        {
            var documento = await BuscarDocumentoAsync(boveda, ubicado, requisito, ct)
                            ?? throw ReglaDeNegocioException.Conflicto("documento-no-encontrado", $"No tienes {requisito.Nombre} en tu bóveda.");
            var stream = await boveda.Events.FetchForWriting<Documento>(documento.Id, ct);
            documento = stream.Aggregate!;
            if (documento.VersionActual is not { } numero)
                throw ReglaDeNegocioException.Conflicto("documento-no-encontrado", $"No tienes una versión vigente de {requisito.Nombre}.");
            var permite = documento.Ambito == AmbitoDocumento.Global && CatalogoCanonico.Buscar(documento.TipoCanonico)?.PermiteRatificacion == true;
            // Valida la renovación antes de escribir en la bóveda (el expediente se escribe después, en su tenant).
            var actual = documento.VersionRequerida(numero);
            version = new VersionDeRequisito(documento.Id, numero, actual.Sha256, actual.CargadaEn);
            ubicado.RatificarRequisito(requisitoId, version, por, ahora);
            stream.AppendOne(documento.Ratificar(numero, ubicado.VinculoId, permite, por, ahora));
            await boveda.SaveChangesAsync(ct);
            documentoId = documento.Id;
        }

        if (documentoId is { } docId)
            await mensajes.InvokeAsync(new CalcularVigenciasDelDocumento(docId), ct);

        // El expediente, con reintentos: al aprobarse reemite el certificado, que otro proceso puede estar tocando (su PDF).
        return await Peticiones.ConReintentosAsync(async () =>
        {
            await using var sesion = sesiones.ParaTenant(enSesion.TenantId);
            var expedienteStream = await sesion.Events.FetchForExclusiveWriting<Expediente>(id, ct);
            var expediente = expedienteStream.Aggregate ?? throw ReglaDeNegocioException.NoEncontrado("expediente-no-encontrado", "No existe ese expediente.");
            var eventos = expediente.RatificarRequisito(requisitoId, version, por, ahora);
            var estado = await ProcesoDeValidacion.AnexarAsync(sesion, expedienteStream, eventos, null, certificados, ahora, ct);
            await sesion.SaveChangesAsync(ct);

            await using var consultaBoveda = sesiones.ConsultaBoveda();
            var documentacion = await DocumentacionDelExpediente.CargarAsync(sesion, consultaBoveda, estado, reglas, CalculadoraDeVigencia.Fecha(ahora), ct);
            return estado.APublico(await sesion.LoadAsync<Empresa>(estado.EmpresaId, ct), documentacion);
        }, 5);
    }

    /// <summary>
    /// La versión vigente de la sección que el tercero ratifica (VV-1): solo si la renovación la exige (la empresa ya la había aceptado) y
    /// la versión es completa para la empresa; si no, 422 <c>ratificacion-no-permitida</c> o 409 <c>seccion-incompleta</c>.
    /// </summary>
    private static async Task<VersionDeRequisito> VersionDeSeccionAsync(ISesiones sesiones, Expediente expediente, RequisitoDeExpediente requisito,
        CancellationToken ct)
    {
        if (!requisito.ExigeRatificacion || requisito.VersionAceptadaPrevia is null || Dominio.Ficha.CatalogoDeSecciones.Buscar(requisito.Seccion) is not { } definicion)
            throw ReglaDeNegocioException.Validacion("ratificacion-no-permitida",
                $"{requisito.Nombre} no se ratifica: la empresa aún no la había aceptado. Diligénciala en Mi información.");

        await using var boveda = sesiones.ConsultaBoveda();
        var seccion = await boveda.LoadAsync<Dominio.Ficha.SeccionDeFicha>(
            Dominio.Ficha.SeccionDeFicha.IdPara(expediente.TerceroId, definicion.Codigo, requisito.Periodo), ct);
        return RequisitosDeSeccion.Evaluar(definicion, requisito.Periodo, requisito.ExigeSoporte, seccion).Disponible
               ?? throw ReglaDeNegocioException.Conflicto("seccion-incompleta",
                   $"{requisito.Nombre} está incompleta: complétala en Mi información antes de ratificar.");
    }

    /// <summary>El documento de la bóveda que cubre el requisito: global por canónico o el <c>PorVinculo</c> de ese tipo y vínculo.</summary>
    private static async Task<Documento?> BuscarDocumentoAsync(IQuerySession boveda, Expediente expediente, RequisitoDeExpediente requisito,
        CancellationToken ct)
    {
        var terceroId = expediente.TerceroId;
        var documentos = await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
        return requisito.Ambito == AmbitoDocumento.Global
            ? documentos.FirstOrDefault(d => d.Ambito == AmbitoDocumento.Global && requisito.TipoCanonico is not null && d.TipoCanonico == requisito.TipoCanonico)
            : documentos.FirstOrDefault(d => d.Ambito == AmbitoDocumento.PorVinculo && d.VinculoId == expediente.VinculoId &&
                                             d.TipoDocumentoTenantId == requisito.TipoDocumentoTenantId);
    }
}
