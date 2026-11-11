using System.Security.Cryptography;
using System.Text;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Flujos;

namespace Validasoft.Dominio.Expedientes;

/// <summary>Qué pide un requisito del expediente: un documento de la bóveda o una sección de la ficha maestra (F2, FM-25).</summary>
public enum TipoDeRequisito
{
    Documento,
    Seccion
}

/// <summary>
/// Completitud de una versión de la sección <b>para la empresa</b> (FM-16, segundo nivel): la completitud base de F1 más lo
/// que exige el requisito del flujo (hoy, el soporte). Se guarda en el expediente con la versión evaluada
/// (<c>CompletitudDeSeccionEvaluada</c>): nadie la deduce en pantalla. Sin versión: la sección no está diligenciada.
/// </summary>
public sealed record CompletitudParaEmpresa(int? Version, bool Completa, IReadOnlyList<FaltanteDeSeccion> Faltantes)
{
    public bool Igual(CompletitudParaEmpresa? otra) =>
        otra is not null && Version == otra.Version && Completa == otra.Completa && Faltantes.SequenceEqual(otra.Faltantes);
}

/// <summary>
/// Lo que la ficha del tercero ofrece a un requisito de sección: la versión vigente (si hay) y su completitud para la empresa.
/// Solo una versión completa para la empresa queda <see cref="Disponible"/> para el expediente (FM-25).
/// </summary>
public sealed record EvaluacionDeSeccion(VersionDeRequisito? Version, CompletitudParaEmpresa Completitud)
{
    public VersionDeRequisito? Disponible => Completitud.Completa ? Version : null;
}

/// <summary>
/// Requisito de sección del flujo ya resuelto para un expediente: id del requisito en el expediente (en la financiera, uno por
/// periodo exigido), la definición canónica y el periodo.
/// </summary>
public sealed record RequisitoDeSeccionPedido(Guid RequisitoId, RequisitoDelFlujo Requisito, DefinicionDeSeccion Definicion, int? Periodo)
{
    public string Nombre => RequisitosDeSeccion.Nombre(Definicion, Periodo);

    public Guid SeccionId(Guid terceroId) => SeccionDeFicha.IdPara(terceroId, Definicion.Codigo, Periodo);

    /// <summary>Foto para <c>RequisitosCalculados</c>: nace cubierto solo si la versión es completa para la empresa.</summary>
    public RequisitoCalculado Calcular(EvaluacionDeSeccion? evaluacion, VersionDeRequisito? aceptada) =>
        new(RequisitoId, Guid.Empty, Definicion.Codigo, Nombre, Definicion.Codigo, AmbitoDocumento.Global, Requisito.Obligatorio,
            Requisito.AreasQueRevisan, Requisito.AprobadoresRequeridos, evaluacion?.Disponible, null, TipoDeRequisito.Seccion, Definicion.Codigo,
            Periodo, Requisito.ExigeSoporte, aceptada, evaluacion?.Completitud, DiasRevalidacion: Requisito.DiasRevalidacion);
}

/// <summary>
/// Secciones de la ficha como requisitos del flujo (F2, FM-25…FM-27): expansión por periodo de la financiera, nombre legible y la
/// única regla de «completa para la empresa» (completitud base de F1 + soporte exigido por el requisito).
/// </summary>
public static class RequisitosDeSeccion
{
    public const int MaximoPeriodos = 5;
    public const string ReglaSinDiligenciar = "seccion-sin-diligenciar";
    public const string ReglaSoporteRequerido = "soporte-requerido";

    private static readonly Dictionary<string, string> ConArticulo = new(StringComparer.Ordinal)
    {
        [CodigosDeSeccion.General] = "la información general",
        [CodigosDeSeccion.Tributaria] = "la información tributaria",
        [CodigosDeSeccion.Legal] = "la información legal",
        [CodigosDeSeccion.Financiera] = "la información financiera",
        [CodigosDeSeccion.Sst] = "la información de seguridad y salud en el trabajo",
        [CodigosDeSeccion.Especialidades] = "las especialidades"
    };

    /// <summary>«Información financiera 2025» o «Información general».</summary>
    public static string Nombre(DefinicionDeSeccion definicion, int? periodo) => periodo is { } p ? $"{definicion.Nombre} {p}" : definicion.Nombre;

    /// <summary>«la información financiera de 2025», para los motivos legibles.</summary>
    public static string ConArticuloDe(DefinicionDeSeccion definicion, int? periodo)
    {
        var texto = ConArticulo.GetValueOrDefault(definicion.Codigo) ?? definicion.Nombre.ToLowerInvariant();
        return periodo is { } p ? $"{texto} de {p}" : texto;
    }

    /// <summary>
    /// Periodos que se exigen de una sección por periodo: los <paramref name="periodos"/> cierres más recientes antes del año de
    /// <paramref name="hoy"/> (1 por omisión = el último cierre; docs/14, decisión abierta 2).
    /// </summary>
    public static IReadOnlyList<int> PeriodosExigidos(int periodos, DateOnly hoy) =>
        [.. Enumerable.Range(1, Math.Clamp(periodos, 1, MaximoPeriodos)).Select(i => hoy.Year - i)];

    /// <summary>
    /// Requisitos de sección del flujo en el expediente: uno por sección y, en la financiera, uno por periodo exigido. El primero
    /// conserva el id del requisito del flujo; los demás periodos llevan un id determinista (requisito, periodo).
    /// </summary>
    public static IReadOnlyList<RequisitoDeSeccionPedido> Expandir(IEnumerable<RequisitoDelFlujo> requisitos, DateOnly hoy)
    {
        var resultado = new List<RequisitoDeSeccionPedido>();
        foreach (var requisito in requisitos)
        {
            if (requisito.Seccion is null || CatalogoDeSecciones.Buscar(requisito.Seccion) is not { } definicion)
                continue;
            if (!definicion.PorPeriodo)
            {
                resultado.Add(new RequisitoDeSeccionPedido(requisito.Id, requisito, definicion, null));
                continue;
            }

            var periodos = PeriodosExigidos(requisito.Periodos ?? 1, hoy);
            resultado.AddRange(periodos.Select((periodo, indice) =>
                new RequisitoDeSeccionPedido(indice == 0 ? requisito.Id : IdPorPeriodo(requisito.Id, periodo), requisito, definicion, periodo)));
        }

        return resultado;
    }

    /// <summary>Id determinista del requisito de un periodo adicional (SHA-256 de requisito y periodo).</summary>
    public static Guid IdPorPeriodo(Guid requisitoId, int periodo)
    {
        var bytes = SHA256.HashData(Encoding.UTF8.GetBytes($"requisito-periodo|{requisitoId:N}|{periodo}"))[..16];
        bytes[7] = (byte)((bytes[7] & 0x0F) | 0x80);
        bytes[8] = (byte)((bytes[8] & 0x3F) | 0x80);
        return new Guid(bytes);
    }

    /// <summary>
    /// La regla única de «completa para la empresa»: sin versión vigente ⇒ «Falta diligenciar…»; si no, los faltantes de la
    /// completitud base guardada con la versión (F1, FM-16) y, si el requisito exige soporte y la versión no trae, «Falta el
    /// soporte de…». Nada se recalcula de los datos: se parte de lo guardado.
    /// </summary>
    public static EvaluacionDeSeccion Evaluar(DefinicionDeSeccion definicion, int? periodo, bool exigeSoporte, SeccionDeFicha? seccion)
    {
        if (seccion?.Vigente is not { } vigente)
            return new EvaluacionDeSeccion(null, new CompletitudParaEmpresa(null, false,
                [new FaltanteDeSeccion(null, ReglaSinDiligenciar, $"Falta diligenciar {ConArticuloDe(definicion, periodo)}.")]));

        var faltantes = vigente.Completitud.Faltantes.ToList();
        if (exigeSoporte && vigente.Soportes.Count == 0)
            faltantes.Add(new FaltanteDeSeccion(null, ReglaSoporteRequerido, $"Falta el soporte de {ConArticuloDe(definicion, periodo)}."));

        var completa = vigente.Completitud.Completa && faltantes.Count == 0;
        return new EvaluacionDeSeccion(new VersionDeRequisito(seccion.Id, vigente.Numero, vigente.Huella, vigente.Fecha),
            new CompletitudParaEmpresa(vigente.Numero, completa, faltantes));
    }

    public static EvaluacionDeSeccion Evaluar(RequisitoDeSeccionPedido pedido, SeccionDeFicha? seccion) =>
        Evaluar(pedido.Definicion, pedido.Periodo, pedido.Requisito.ExigeSoporte, seccion);
}
