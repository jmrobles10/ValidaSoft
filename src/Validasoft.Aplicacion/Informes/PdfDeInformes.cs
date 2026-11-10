namespace Validasoft.Aplicacion.Informes;

/// <summary>Columna de una tabla del PDF: el ancho se reparte por <see cref="Peso"/>.</summary>
public sealed record ColumnaPdf(string Titulo, double Peso);

/// <summary>Bloque del cuerpo de un informe en PDF.</summary>
public abstract record BloquePdf;

/// <summary>Pares etiqueta–valor (resúmenes, cifras).</summary>
public sealed record DatosPdf(string Titulo, IReadOnlyList<DatoPdf> Datos) : BloquePdf;

public sealed record DatoPdf(string Etiqueta, string Valor);

/// <summary>Tabla con encabezado; <see cref="Vacio"/> se escribe si no hay filas.</summary>
public sealed record TablaPdf(string Titulo, IReadOnlyList<ColumnaPdf> Columnas, IReadOnlyList<IReadOnlyList<string>> Filas, string Vacio) : BloquePdf;

/// <summary>Texto aclaratorio en gris.</summary>
public sealed record NotaPdf(string Texto) : BloquePdf;

/// <summary>
/// Informe en PDF con el estilo de los PDF del proyecto (el del certificado, CE-6): cabecera con la empresa y su NIT, la
/// etiqueta arriba a la derecha, título, bloques y pie con <see cref="Pie"/> y «página n de m» (IF-3).
/// </summary>
public sealed record DocumentoPdf(
    string Empresa,
    string? IdentificacionEmpresa,
    string Etiqueta,
    string Titulo,
    string? Subtitulo,
    DateTimeOffset GeneradoEn,
    string Pie,
    bool Horizontal,
    IReadOnlyList<BloquePdf> Bloques);

/// <summary>Genera los PDF de informes y resúmenes (implementación en infraestructura, sobre el mismo lienzo del certificado).</summary>
public interface IGeneradorDePdfDeInformes
{
    byte[] Generar(DocumentoPdf documento);
}
