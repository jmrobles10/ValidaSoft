using System.Globalization;
using System.Security.Cryptography;
using System.Text;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Dominio.Documentos;

namespace Validasoft.Aplicacion.Informes;

/// <summary>
/// Registro de una exportación de la empresa (IF-6): un informe en Excel o PDF, la auditoría en Excel o el ZIP de un
/// expediente. Queda en el tenant, aislado por empresa, y la auditoría lo muestra en <c>Accesos</c> con quién, cuándo,
/// desde dónde, qué filtros y la huella SHA-256 del archivo entregado.
/// </summary>
public sealed class ExportacionRegistrada
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    /// <summary><c>InformeEstadoExpedientes</c>, <c>InformeVinculos</c>, <c>Auditoria</c> o <c>Expediente</c>.</summary>
    public string Tipo { get; set; } = string.Empty;

    /// <summary><c>Excel</c>, <c>Pdf</c> o <c>Zip</c>.</summary>
    public string Formato { get; set; } = string.Empty;

    /// <summary>Entidad exportada (el expediente del ZIP); nula en los informes.</summary>
    public string? EntidadId { get; set; }

    public string? EntidadNombre { get; set; }

    public Guid? VinculoId { get; set; }

    /// <summary>Filtros de la consulta tal como llegaron (<c>clave=valor</c> separados por <c>&amp;</c>).</summary>
    public string Filtros { get; set; } = string.Empty;

    public int Filas { get; set; }

    public long Bytes { get; set; }

    public string Sha256 { get; set; } = string.Empty;

    public string ActorTipo { get; set; } = string.Empty;

    public string ActorId { get; set; } = string.Empty;

    public string Ip { get; set; } = string.Empty;

    public string UserAgent { get; set; } = string.Empty;

    public DateTimeOffset Fecha { get; set; }
}

/// <summary>Lo que se entrega: el archivo y lo que se audita de él.</summary>
public sealed record ArchivoExportado(string Tipo, string Formato, byte[] Contenido, string TipoContenido, string Nombre, int Filas,
    string? EntidadId = null, string? EntidadNombre = null, Guid? VinculoId = null);

public static class Exportaciones
{
    public const string TipoPdf = "application/pdf";
    public const string TipoZip = "application/zip";

    /// <summary>Registra la exportación y devuelve el archivo (<c>Content-Disposition: attachment</c>).</summary>
    public static async Task<IResult> EntregarAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http, TimeProvider reloj,
        ArchivoExportado archivo, CancellationToken ct)
    {
        empresa.Sesion.Store(new ExportacionRegistrada
        {
            Id = Guid.CreateVersion7(),
            EmpresaId = empresa.EmpresaId,
            Tipo = archivo.Tipo,
            Formato = archivo.Formato,
            EntidadId = archivo.EntidadId,
            EntidadNombre = archivo.EntidadNombre,
            VinculoId = archivo.VinculoId,
            Filtros = http.Request.QueryString.HasValue ? Uri.UnescapeDataString(http.Request.QueryString.Value![1..]) : string.Empty,
            Filas = archivo.Filas,
            Bytes = archivo.Contenido.LongLength,
            Sha256 = Convert.ToHexStringLower(SHA256.HashData(archivo.Contenido)),
            ActorTipo = contexto.ActorTipo.ToString(),
            ActorId = contexto.ActorId,
            Ip = BovedaEndpoints.Ip(http),
            UserAgent = BovedaEndpoints.UserAgent(http),
            Fecha = reloj.GetUtcNow()
        });
        await empresa.Sesion.SaveChangesAsync(ct);
        return Results.File(archivo.Contenido, archivo.TipoContenido, archivo.Nombre);
    }

    /// <summary>Instante en la zona del negocio (FE-1), como lo leen las hojas de cálculo y los PDF.</summary>
    public static DateTime Local(DateTimeOffset instante) => TimeZoneInfo.ConvertTime(instante, CalculadoraDeVigencia.ZonaDeNegocio).DateTime;

    /// <summary><c>dd/mm/aaaa hh:mm</c> en la zona del negocio.</summary>
    public static string Momento(DateTimeOffset instante) => Local(instante).ToString("dd/MM/yyyy HH:mm", CultureInfo.InvariantCulture);

    public static string Fecha(DateOnly fecha) => fecha.ToString("dd/MM/yyyy", CultureInfo.InvariantCulture);

    /// <summary>Días con un decimal y coma decimal (es-CO).</summary>
    public static string Dias(double? dias) => dias is { } d ? d.ToString("0.0", CultureInfo.GetCultureInfo("es-CO")) : "—";

    /// <summary>Inicio (inclusive) del día <paramref name="fecha"/> en la zona del negocio.</summary>
    public static DateTimeOffset InicioDelDia(DateOnly fecha)
    {
        var local = fecha.ToDateTime(TimeOnly.MinValue, DateTimeKind.Unspecified);
        return new DateTimeOffset(local, CalculadoraDeVigencia.ZonaDeNegocio.GetUtcOffset(local));
    }

    /// <summary>Nombre de archivo sin tildes ni espacios: <c>Constructora Norte S.A.S.</c> → <c>constructora-norte-sas</c>.</summary>
    public static string Slug(string texto)
    {
        var limpio = new string([.. texto.ToLowerInvariant().Normalize(NormalizationForm.FormD).Where(c => char.IsAsciiLetterOrDigit(c) || c is ' ' or '-')]);
        return string.Join('-', limpio.Split([' ', '-'], StringSplitOptions.RemoveEmptyEntries));
    }
}

/// <summary>Promedio y mediana de días con un decimal: los mismos del informe de estado y de los indicadores de Inicio (IF-7).</summary>
public static class Estadistica
{
    public static double? Promedio(IReadOnlyCollection<double> valores) => valores.Count == 0 ? null : Math.Round(valores.Average(), 1);

    public static double? Mediana(IEnumerable<double> valores)
    {
        var ordenados = valores.Order().ToList();
        if (ordenados.Count == 0)
            return null;
        var medio = ordenados.Count / 2;
        return Math.Round(ordenados.Count % 2 == 1 ? ordenados[medio] : (ordenados[medio - 1] + ordenados[medio]) / 2, 1);
    }
}
