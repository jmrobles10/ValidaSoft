using Validasoft.Dominio.Documentos;

namespace Validasoft.Dominio.Flujos;

/// <summary>Una sección que el vínculo tiene aceptada (código y periodo) y desde cuándo (<see cref="Ficha.SeccionAceptada"/>).</summary>
public sealed record SeccionAceptadaEn(string Seccion, int? Periodo, DateTimeOffset AceptadaEn);

/// <summary>
/// Cálculo puro de la vigencia de un vínculo aprobado (VV-1): la fecha hasta la que vale lo aprobado y qué secciones vencieron.
/// Las fechas son de calendario en la zona del negocio (<see cref="CalculadoraDeVigencia.Fecha"/>), igual que los documentos.
/// </summary>
public static class VigenciaDeAprobacion
{
    /// <summary>
    /// Hasta cuándo es vigente el vínculo aprobado en <paramref name="aprobadaEn"/> (inclusive): la menor entre la aprobación más los
    /// días de <paramref name="vigencia"/> (si la tiene) y, por cada sección aceptada cuyo requisito del flujo trae
    /// <see cref="RequisitoDelFlujo.VigenciaDias"/>, su aceptación más esos días. Nulo si nada vence.
    /// </summary>
    public static DateOnly? VigenteHasta(VigenciaDelVinculo vigencia, IEnumerable<RequisitoDelFlujo> requisitos,
        IEnumerable<SeccionAceptadaEn> aceptadas, DateTimeOffset aprobadaEn)
    {
        DateOnly? hasta = vigencia.Activa ? CalculadoraDeVigencia.Fecha(aprobadaEn).AddDays(vigencia.Dias) : null;
        foreach (var (requisito, aceptada) in ConVigenciaPropia(requisitos, aceptadas))
        {
            var fin = CalculadoraDeVigencia.Fecha(aceptada.AceptadaEn).AddDays(requisito.VigenciaDias!.Value);
            if (hasta is null || fin < hasta)
                hasta = fin;
        }

        return hasta;
    }

    /// <summary>
    /// ¿Ya pasó la vigencia periódica? <paramref name="hoy"/> es posterior a la aprobación más los días (la gracia aparte: aquí se
    /// pregunta por la fecha, no por el vencimiento).
    /// </summary>
    public static bool PeriodicaVencida(VigenciaDelVinculo vigencia, DateTimeOffset? aprobadaEn, DateOnly hoy) =>
        vigencia.Activa && aprobadaEn is { } aprobada && hoy > CalculadoraDeVigencia.Fecha(aprobada).AddDays(vigencia.Dias);

    /// <summary>
    /// Requisitos de sección del flujo con vigencia propia cuya aceptación ya venció a <paramref name="hoy"/>: entran a la
    /// renovación para que el tercero los ratifique o los actualice.
    /// </summary>
    public static IReadOnlyList<RequisitoDelFlujo> SeccionesVencidas(IEnumerable<RequisitoDelFlujo> requisitos, IEnumerable<SeccionAceptadaEn> aceptadas,
        DateOnly hoy) =>
        [
            .. ConVigenciaPropia(requisitos, aceptadas)
                .Where(x => hoy > CalculadoraDeVigencia.Fecha(x.Aceptada.AceptadaEn).AddDays(x.Requisito.VigenciaDias!.Value))
                .Select(x => x.Requisito)
                .Distinct()
        ];

    private static IEnumerable<(RequisitoDelFlujo Requisito, SeccionAceptadaEn Aceptada)> ConVigenciaPropia(IEnumerable<RequisitoDelFlujo> requisitos,
        IEnumerable<SeccionAceptadaEn> aceptadas)
    {
        var conVigencia = requisitos.Where(r => r is { EsSeccion: true, VigenciaDias: > 0 }).ToList();
        return aceptadas.SelectMany(a => conVigencia.Where(r => r.Seccion == a.Seccion).Select(r => (r, a)));
    }
}
