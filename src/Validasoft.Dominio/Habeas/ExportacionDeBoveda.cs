using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Habeas;

public enum EstadoDeExportacionDeBoveda
{
    /// <summary>Pedida; espera su turno en la cola.</summary>
    Pendiente,

    Generando,

    /// <summary>El ZIP está listo y se descarga con una URL prefirmada hasta <see cref="ExportacionDeBoveda.ExpiraEn"/>.</summary>
    Lista,

    Fallida,

    /// <summary>Pasó su plazo de descarga: el ZIP se eliminó del almacenamiento.</summary>
    Vencida
}

/// <summary>Una descarga del ZIP: cuándo y desde dónde.</summary>
public sealed record DescargaDeExportacion(DateTimeOffset Fecha, string Ip, string UserAgent);

/// <summary>
/// Exportación de toda la bóveda de un tercero (OD-9, docs/07 §8): ZIP con sus archivos, sus datos y un manifiesto con el SHA-256 de
/// cada uno, firmado por Validasoft. Se genera en segundo plano y se baja con una URL prefirmada. Esquema <c>boveda</c>, global.
/// </summary>
public sealed class ExportacionDeBoveda
{
    public const int MaximoDescargasRegistradas = 50;

    public Guid Id { get; set; }

    public Guid TerceroId { get; set; }

    public EstadoDeExportacionDeBoveda Estado { get; set; }

    public DateTimeOffset SolicitadaEn { get; set; }

    public string Ip { get; set; } = string.Empty;

    /// <summary>Correo con el que el tercero entró: a él se avisa cuando el ZIP está listo; se borra al vencer la exportación.</summary>
    public string? Correo { get; set; }

    public DateTimeOffset? IniciadaEn { get; set; }

    public DateTimeOffset? TerminadaEn { get; set; }

    /// <summary>Hasta cuándo se puede descargar; después el ZIP se elimina.</summary>
    public DateTimeOffset? ExpiraEn { get; set; }

    public string? Bucket { get; set; }

    public string? Clave { get; set; }

    /// <summary>Archivos que lleva el ZIP (sin contar el manifiesto, su firma ni el LEEME).</summary>
    public int Archivos { get; set; }

    public long? Bytes { get; set; }

    /// <summary>SHA-256 del ZIP completo.</summary>
    public string? Sha256 { get; set; }

    /// <summary>SHA-256 del <c>manifiesto.json</c>, que es lo que firma Validasoft.</summary>
    public string? HashManifiesto { get; set; }

    /// <summary>Identificador (<c>kid</c>) de la clave con la que se firmó el manifiesto.</summary>
    public string? Kid { get; set; }

    /// <summary>Código estable del fallo (<c>generacion-fallida</c>, <c>almacenamiento-no-disponible</c>…); nunca un dato del tercero.</summary>
    public string? CodigoDeError { get; set; }

    public int Descargas { get; set; }

    public List<DescargaDeExportacion> Accesos { get; set; } = [];

    public static ExportacionDeBoveda Solicitar(Guid terceroId, string? correo, string ip, DateTimeOffset ahora) => new()
    {
        Id = Guid.CreateVersion7(),
        TerceroId = terceroId,
        Estado = EstadoDeExportacionDeBoveda.Pendiente,
        SolicitadaEn = ahora,
        Correo = correo,
        Ip = ip
    };

    public bool EnCurso => Estado is EstadoDeExportacionDeBoveda.Pendiente or EstadoDeExportacionDeBoveda.Generando;

    public void Iniciar(DateTimeOffset ahora)
    {
        Estado = EstadoDeExportacionDeBoveda.Generando;
        IniciadaEn = ahora;
        CodigoDeError = null;
    }

    public void Terminar(string bucket, string clave, int archivos, long bytes, string sha256, string hashManifiesto, string kid, TimeSpan vigencia,
        DateTimeOffset ahora)
    {
        Estado = EstadoDeExportacionDeBoveda.Lista;
        Bucket = bucket;
        Clave = clave;
        Archivos = archivos;
        Bytes = bytes;
        Sha256 = sha256;
        HashManifiesto = hashManifiesto;
        Kid = kid;
        TerminadaEn = ahora;
        ExpiraEn = ahora + vigencia;
    }

    public void Fallar(string codigo, DateTimeOffset ahora)
    {
        Estado = EstadoDeExportacionDeBoveda.Fallida;
        CodigoDeError = codigo;
        TerminadaEn = ahora;
    }

    public void Vencer()
    {
        Estado = EstadoDeExportacionDeBoveda.Vencida;
        Bucket = null;
        Clave = null;
        Correo = null;
    }

    /// <summary>Se puede descargar: está lista y no pasó su plazo (409 <c>exportacion-no-disponible</c> / 410 <c>exportacion-vencida</c>).</summary>
    public void AsegurarDescargable(DateTimeOffset ahora)
    {
        if (Estado == EstadoDeExportacionDeBoveda.Vencida || (Estado == EstadoDeExportacionDeBoveda.Lista && ExpiraEn <= ahora))
            throw ReglaDeNegocioException.Expirado("exportacion-vencida", "Esa exportación venció; pide una nueva.");
        if (Estado != EstadoDeExportacionDeBoveda.Lista || Clave is null)
            throw ReglaDeNegocioException.Conflicto("exportacion-no-disponible", "La exportación todavía no está lista.");
    }

    public void RegistrarDescarga(string ip, string userAgent, DateTimeOffset ahora)
    {
        Descargas++;
        Accesos.Add(new DescargaDeExportacion(ahora, ip, userAgent.Length > 200 ? userAgent[..200] : userAgent));
        if (Accesos.Count > MaximoDescargasRegistradas)
            Accesos.RemoveRange(0, Accesos.Count - MaximoDescargasRegistradas);
    }
}
