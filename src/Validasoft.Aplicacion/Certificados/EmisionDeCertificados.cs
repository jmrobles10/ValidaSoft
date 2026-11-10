using JasperFx.Events;
using Marten;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Certificados;

/// <summary>
/// Emite el certificado cuando el vínculo queda <c>Activo</c> por un expediente aprobado (docs/03 §8), <b>en la misma
/// transacción</b> que <c>VinculoActivado</c> (CE-2): lo llama <see cref="ProcesoDeValidacion"/>, único punto que cierra
/// expedientes. Así no se pierde (si la aprobación se confirma, el certificado también) ni se duplica (el stream tiene id
/// determinista por vínculo y una emisión por expediente). El PDF se genera después del commit (CE-5). Registrado por tipo.
/// </summary>
public sealed class EmisorDeCertificados(ClavesDeCertificados claves, ReglasDeCarga reglas, IConfiguration configuracion)
{
    public const string TipoFolio = "C";

    /// <summary>Base de la URL pública de verificación (<c>Validasoft:Correo:UrlPublica</c>, la misma de los enlaces al tercero).</summary>
    public string UrlPublica => configuracion["Validasoft:Correo:UrlPublica"] ?? string.Empty;

    public ClavesDeCertificados Claves => claves;

    /// <summary>
    /// Emite (o reemite, si el vínculo ya tenía certificado de otro expediente) con el estado del expediente recién aprobado.
    /// Idempotente: si el certificado ya tiene una emisión de ese expediente no hace nada.
    /// </summary>
    public async Task EmitirAsync(IDocumentSession sesion, Expediente expediente, Vinculo vinculo, DateTimeOffset ahora, CancellationToken ct)
    {
        var stream = await sesion.Events.FetchForWriting<Certificado>(Certificado.IdPara(vinculo.Id), ct);
        if (stream.Aggregate is { } existente && existente.Emisiones.Any(e => e.ExpedienteId == expediente.Id))
            return;

        var empresa = await sesion.LoadAsync<Empresa>(vinculo.EmpresaId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("empresa-no-encontrada", "La empresa del vínculo no existe.");
        var plantilla = await ContenidoAsync(sesion, expediente, vinculo, empresa, ahora, ct);
        var origen = new OrigenDeEmision(empresa.TenantId, empresa.Id, vinculo.Id, vinculo.TerceroId, expediente.Id, expediente.Folio);

        if (stream.Aggregate is { } certificado)
        {
            stream.AppendMany(certificado.Reemitir(origen, plantilla, claves, UrlPublica, $"Expediente {expediente.Folio} aprobado", ahora));
            return;
        }

        var folioRaiz = await SiguienteFolioAsync(sesion, empresa, ahora, ct);
        stream.AppendOne(Certificado.Emitir(origen, folioRaiz, plantilla, claves, UrlPublica, ahora));
    }

    /// <summary>
    /// Contenido canónico desde el expediente aprobado (docs/03 §7.1): empresa, titular enmascarado, tipo de vínculo, flujo y
    /// versión, documentos aceptados (versión, SHA-256, vigencia para el vínculo) o eximidos con justificación, y áreas con
    /// el origen de su aprobación. Sin revisores ni contactos.
    /// </summary>
    private async Task<ContenidoDeCertificado> ContenidoAsync(IDocumentSession sesion, Expediente expediente, Vinculo vinculo, Empresa empresa,
        DateTimeOffset ahora, CancellationToken ct)
    {
        var tercero = await sesion.LoadAsync<Tercero>(vinculo.TerceroId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero del vínculo no existe.");
        var tipo = await sesion.LoadAsync<TipoVinculo>(vinculo.TipoVinculoId, ct);

        await using var boveda = sesion.DocumentStore.QuerySession(TenantsTecnicos.Boveda);
        var documentacion = await DocumentacionDelExpediente.CargarAsync(sesion, boveda, expediente, reglas, CalculadoraDeVigencia.Fecha(ahora), ct);

        var requisitos = expediente.Requisitos
            .Where(r => r.Eximido || r.VersionActual is not null)
            .Select(r => r.Eximido
                ? new RequisitoCertificado(r.Codigo, r.Nombre, null, null, null, true, r.JustificacionExencion)
                : new RequisitoCertificado(r.Codigo, r.Nombre, r.VersionActual!.Numero, r.VersionActual.Sha256,
                    documentacion.Version(r.VersionActual, r, vinculo.Id).Vigencia?.VigenteHasta, false, null))
            .ToList();

        var areas = expediente.Etapas.OrderBy(e => e.Orden)
            .SelectMany(e => e.Areas.Select(a => Area(a, e.AprobadaEn ?? ahora)))
            .ToList();

        var identificacion = empresa.Identificacion;
        return new ContenidoDeCertificado(
            ContenidoDeCertificado.EsquemaV1,
            Folio: string.Empty,
            Emision: 0,
            new EmpresaCertificadora(empresa.Id, empresa.Nombre,
                new IdentificacionDeEmpresa(identificacion.Pais, identificacion.Tipo, identificacion.Numero, identificacion.DigitoVerificacion)),
            new TitularCertificado(tercero.Nombre, Enmascarado.Identificacion(tercero.Identificacion), tercero.Identificacion.Pais,
                tercero.TipoPersona.ToString()),
            new TipoVinculoCertificado(tipo?.Codigo ?? expediente.TipoVinculoCodigo, tipo?.Nombre ?? expediente.TipoVinculoNombre),
            new FlujoCertificado(expediente.Flujo?.Codigo ?? string.Empty, expediente.Flujo?.Nombre ?? string.Empty, expediente.Flujo?.Version ?? 0),
            requisitos,
            // Las verificaciones externas satisfechas al aprobar (H7): tipo, proveedor, resultado y fecha; sin personas ni listas.
            [
                .. expediente.Verificaciones.Where(v => v.Estado == EstadoVerificacion.Satisfecha && v.RegistradaEn is not null)
                    .OrderBy(v => v.Tipo)
                    .Select(v => new VerificacionCertificado(v.Tipo.ToString(), v.Proveedor ?? string.Empty, v.Resultado?.ToString() ?? string.Empty,
                        v.RegistradaEn!.Value))
            ],
            areas,
            ahora,
            null);
    }

    /// <summary>
    /// Cómo aprobó el área: el veredicto derivado de sus requisitos dice el origen (RQ-5); sin requisitos, <c>Administrador</c>
    /// si intervino, si no <c>Sistema</c> (área automática), si no <c>Humano</c>. La fecha es la de la última aprobación vigente.
    /// </summary>
    private static AreaCertificado Area(AreaDeExpediente area, DateTimeOffset respaldo)
    {
        var aprobaciones = area.Veredictos.Where(v => v.Vigente && v.Veredicto == Veredicto.Aprobado).ToList();
        var derivado = aprobaciones.LastOrDefault(v => v.Derivado);
        var origen = derivado?.Origen
                     ?? (aprobaciones.Any(v => v.Origen == OrigenVeredicto.Administrador) ? OrigenVeredicto.Administrador
                         : aprobaciones.Any(v => v.Origen == OrigenVeredicto.Sistema) ? OrigenVeredicto.Sistema
                         : OrigenVeredicto.Humano);
        var fecha = derivado?.Fecha ?? (aprobaciones.Count > 0 ? aprobaciones.Max(v => v.Fecha) : respaldo);
        return new AreaCertificado(area.Nombre, area.Codigo, origen.ToString(), fecha);
    }

    /// <summary>
    /// Folio raíz <c>{Prefijo}-C-{Año}-{Secuencia}</c> con el año en la zona del tenant. La secuencia es un stream que se toma
    /// con bloqueo de fila (CE-3): dos aprobaciones simultáneas de la empresa esperan su turno; un rollback no consume número.
    /// </summary>
    private static async Task<string> SiguienteFolioAsync(IDocumentSession sesion, Empresa empresa, DateTimeOffset ahora, CancellationToken ct)
    {
        var tenant = await sesion.LoadAsync<Tenant>(empresa.TenantId, ct);
        var zona = TimeZoneInfo.TryFindSystemTimeZoneById(tenant?.ZonaHoraria ?? "America/Bogota", out var encontrada) ? encontrada : TimeZoneInfo.Utc;
        var anio = TimeZoneInfo.ConvertTime(ahora, zona).Year;
        var id = SecuenciaDeFolios.IdPara(empresa.Id, TipoFolio, anio);

        await AsegurarSecuenciaAsync(sesion, id, new SecuenciaDeFoliosIniciada(empresa.Id, TipoFolio, anio, ahora), ct);
        var secuencia = await sesion.Events.FetchForExclusiveWriting<SecuenciaDeFolios>(id, ct);
        var consumido = secuencia.Aggregate!.Consumir(ahora);
        secuencia.AppendOne(consumido);
        return FoliosDeCertificado.Raiz(empresa.PrefijoFolio, anio, consumido.Numero);
    }

    /// <summary>
    /// Crea el stream de la secuencia en su propia transacción si aún no existe (la primera emisión del año): si dos lo crean
    /// a la vez, uno choca y simplemente usa el del otro. Así el bloqueo de fila siempre tiene una fila que bloquear.
    /// </summary>
    private static async Task AsegurarSecuenciaAsync(IDocumentSession sesion, Guid id, SecuenciaDeFoliosIniciada inicio, CancellationToken ct)
    {
        await using var propia = sesion.DocumentStore.LightweightSession(sesion.TenantId);
        if (await propia.Events.FetchStreamStateAsync(id, ct) is not null)
            return;

        propia.Events.StartStream<SecuenciaDeFolios>(id, inicio);
        try
        {
            await propia.SaveChangesAsync(ct);
        }
        catch (Exception)
        {
            await using var consulta = sesion.DocumentStore.QuerySession(sesion.TenantId);
            if (await consulta.Events.FetchStreamStateAsync(id, ct) is null)
                throw;
        }
    }
}

/// <summary>
/// El certificado sigue al vínculo en la misma transacción (docs/03 §8, B-5): bloquear ⇒ <c>Suspendido</c>; desbloquear ⇒
/// <c>Restablecido</c> (o <c>Vencido</c> si su vigencia pasó durante el bloqueo); anular o desvincular ⇒ <c>Revocado</c> por
/// el sistema con el motivo. Un vínculo sin certificado no hace nada. El stream se toma con bloqueo de fila (CE-12): el
/// generador del PDF escribe en él después del commit y una lectura optimista respondía 412 a quien no envió If-Match.
/// </summary>
public static class CicloDeCertificados
{
    public static async Task AlBloquearAsync(IDocumentSession sesion, Guid vinculoId, string motivo, DateTimeOffset ahora, CancellationToken ct)
    {
        var stream = await sesion.Events.FetchForExclusiveWriting<Certificado>(Certificado.IdPara(vinculoId), ct);
        if (stream.Aggregate is { Estado: EstadoCertificado.Vigente or EstadoCertificado.ConPendientes } certificado)
            stream.AppendOne(certificado.Suspender($"Vínculo bloqueado: {motivo}", ahora));
    }

    public static async Task AlDesbloquearAsync(IDocumentSession sesion, Guid vinculoId, string justificacion, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var stream = await sesion.Events.FetchForExclusiveWriting<Certificado>(Certificado.IdPara(vinculoId), ct);
        if (stream.Aggregate is { Estado: EstadoCertificado.Suspendido } certificado)
            stream.AppendOne(certificado.Restablecer($"Vínculo desbloqueado: {justificacion}", ahora));
    }

    /// <summary>Anulación o desvinculación: revocación por el sistema (irreversible) con la causa y el motivo del vínculo.</summary>
    public static async Task AlTerminarAsync(IDocumentSession sesion, Guid vinculoId, string causa, string motivo, string actor, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var stream = await sesion.Events.FetchForExclusiveWriting<Certificado>(Certificado.IdPara(vinculoId), ct);
        if (stream.Aggregate is { } certificado && certificado.Estado != EstadoCertificado.Revocado)
            stream.AppendOne(certificado.Revocar($"{causa}: {motivo}", OrigenRevocacion.Sistema, actor, ahora));
    }
}
