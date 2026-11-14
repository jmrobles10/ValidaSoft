using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Vinculos;

/// <summary>
/// Reintegrar sin revalidar (DS-3, el <c>documental = 0</c> del SRM): un vínculo desvinculado que estaba aprobado y vigente
/// vuelve a su estado y recibe un certificado nuevo con lo que la empresa ya aprobó, sin pasar de nuevo por las áreas. Solo
/// si lo aprobado sigue en pie: que ninguna versión haya cambiado en la bóveda del tercero desde que se aprobó (esa versión
/// nueva nadie la revisó) y que ninguna esté vencida o en gracia. Si no, el reintegro exige revalidar y aquí se explica por
/// qué, en texto para la persona.
/// </summary>
public static class ReintegroDeVinculos
{
    /// <summary>
    /// ¿Se puede reintegrar <paramref name="vinculo"/> sin revalidar? Con los motivos legibles cuando no. Solo lectura: no
    /// escribe nada y sirve tanto para la pantalla (<c>GET /vinculos/{id}/reintegro</c>) como para validar el reintegro.
    /// </summary>
    public static async Task<ViabilidadDeReintegroDto> EvaluarAsync(SesionDeEmpresa empresa, Vinculo vinculo, DateTimeOffset ahora, CancellationToken ct)
    {
        if (vinculo.Estado != EstadoVinculo.Desvinculado)
            return No($"El vínculo está {vinculo.Estado}: solo se reintegra uno desvinculado.");
        if (!vinculo.TuvoAprobacion || vinculo.EstadoAlDesvincular is not (EstadoVinculo.Activo or EstadoVinculo.PorVencer))
            return No(!vinculo.TuvoAprobacion
                ? "El vínculo nunca fue aprobado: no hay nada que restituir, debe pasar por la validación."
                : vinculo.EstadoAlDesvincular is { } estado
                    ? $"Al desvincularse el vínculo estaba {estado}, no aprobado y vigente: debe revalidarse."
                    : "No se sabe si al desvincularse el vínculo estaba aprobado y vigente: debe revalidarse.");

        var sesion = empresa.Sesion;
        var aprobado = await UltimoAprobadoAsync(sesion, vinculo.Id, ct);
        if (aprobado is null)
            return No("No hay un expediente aprobado del que restituir el certificado: debe revalidarse.");

        var motivos = new List<string>();

        // Una versión nueva en la bóveda del tercero que la empresa no revisó (documento o sección).
        if (aprobado.Flujo is { } delExpediente &&
            await sesion.LoadAsync<Flujo>(delExpediente.FlujoId, ct) is { } flujo &&
            flujo.Versiones.FirstOrDefault(v => v.Id == delExpediente.FlujoVersionId) is { } version)
        {
            var contexto = await RequisitosDeExpediente.ContextoAsync(sesion, vinculo, ct);
            var cambios = await Revalidacion.CambiosAsync(sesion, vinculo, version, contexto, ahora, ct);
            if (!cambios.Vacio)
                motivos.Add($"Cambió en la bóveda del tercero, sin que la empresa lo revisara: {string.Join(", ", cambios.Nombres)}.");
        }

        // Una versión aprobada que ya venció (o está en gracia) para esta empresa.
        await using var boveda = empresa.ConsultaBoveda();
        var documentacion = await DocumentacionDelExpediente.CargarAsync(sesion, boveda, aprobado, empresa.Reglas, CalculadoraDeVigencia.Fecha(ahora), ct);
        var vencidos = aprobado.Requisitos
            // Lo que el vínculo tiene eximido (EX-3) no cuenta: su vencimiento no impide restituir lo aprobado.
            .Where(r => !r.EsSeccion && !r.Eximido && r.VersionActual is not null &&
                        vinculo.ExencionDeDocumento(r.TipoDocumentoTenantId, CalculadoraDeVigencia.Fecha(ahora)) is null)
            .Where(r => documentacion.Version(r.VersionActual!, r, vinculo.Id).Vigencia?.Estado is nameof(EstadoVigencia.Vencida) or nameof(EstadoVigencia.EnGracia))
            .Select(r => r.Nombre)
            .ToList();
        if (vencidos.Count > 0)
            motivos.Add($"Su vigencia terminó: {string.Join(", ", vencidos)}.");

        return motivos.Count == 0 ? new ViabilidadDeReintegroDto(true, []) : new ViabilidadDeReintegroDto(false, motivos);
    }

    /// <summary>El último expediente aprobado del vínculo (el que sostuvo su certificado); nulo si no hay.</summary>
    public static async Task<Expediente?> UltimoAprobadoAsync(IQuerySession sesion, Guid vinculoId, CancellationToken ct) =>
        (await sesion.Query<Expediente>().Where(e => e.VinculoId == vinculoId && e.Estado == EstadoExpediente.Aprobado).ToListAsync(ct))
        .OrderByDescending(e => e.ActualizadoEn)
        .FirstOrDefault();

    private static ViabilidadDeReintegroDto No(string motivo) => new(false, [motivo]);
}
