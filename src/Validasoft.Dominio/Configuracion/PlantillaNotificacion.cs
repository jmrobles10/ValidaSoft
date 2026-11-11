using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Configuracion;

/// <summary>
/// Un correo que Validasoft le manda al tercero en nombre de una empresa y que la empresa puede personalizar (PL-1):
/// <see cref="Evento"/> estable, nombre y descripción para la pantalla y las variables <c>{{nombre}}</c> que admite.
/// </summary>
public sealed record EventoDePlantilla(string Evento, string Nombre, string Descripcion, IReadOnlyList<string> Variables);

/// <summary>
/// Catálogo cerrado de correos personalizables (PL-1). OTP, enlaces de acceso, autorregistro, el aviso consolidado de
/// vencimiento (varias empresas) y las notificaciones internas no son de una empresa: no se personalizan. La excepción es
/// <c>retiro-version</c> (RT-2): llega al personal de la empresa, que puede ajustar su asunto, cabecera y pie.
/// </summary>
public static class CatalogoDePlantillas
{
    public const string Correo = "correo";

    public const string Invitacion = "invitacion";
    public const string Observacion = "observacion";
    public const string Resultado = "resultado";
    public const string ConsentimientoEmpresa = "consentimiento-empresa";
    public const string Vencido = "vencido";
    public const string Certificado = "certificado";
    public const string Caducidad = "caducidad";
    public const string RecordatorioObservaciones = "recordatorio-observaciones";
    public const string Desaprobacion = "desaprobacion";
    public const string Reintegro = "reintegro";
    public const string RetiroDeVersion = "retiro-version";

    public static readonly IReadOnlyList<EventoDePlantilla> Eventos =
    [
        new(Invitacion, "Invitación", "Invitación de la empresa con el enlace de acceso para validar el vínculo.", ["empresa", "tipoVinculo"]),
        new(Observacion, "Observaciones", "La empresa revisó la validación y pide corregir documentos o datos.", ["empresa", "folio"]),
        new(Resultado, "Resultado de la validación", "La validación se aprobó (con certificado) o se cerró sin aprobarla.", ["empresa", "folio"]),
        new(ConsentimientoEmpresa, "Constancia de aceptación", "Constancia de los textos legales que el tercero aceptó.", ["empresa"]),
        new(Vencido, "Documento vencido", "Un documento venció para la empresa y pasó el plazo de gracia.", ["empresa", "documento"]),
        new(Certificado, "Certificado revocado o suspendido", "El certificado del tercero dejó de valer.", ["empresa", "folio"]),
        new(Caducidad, "Plazo de cargue", "Recordatorio del plazo para completar el expediente, su caducidad y su reapertura.", ["empresa", "folio"]),
        new(RecordatorioObservaciones, "Recordatorio de observaciones", "La empresa le recuerda al tercero las observaciones que sigue teniendo pendientes.",
            ["empresa", "folio"]),
        new(Desaprobacion, "Validación devuelta a revisión", "La empresa devolvió a revisión una validación que ya estaba aprobada.", ["empresa", "folio"]),
        new(Reintegro, "Vínculo reintegrado", "La empresa reintegró el vínculo del tercero, con o sin validar de nuevo.", ["empresa", "folio"]),
        new(RetiroDeVersion, "Versión retirada por el tercero",
            "Aviso al personal de la empresa (no al tercero) cuando el tercero retira una versión que ya había aprobado o que está en revisión.",
            ["empresa", "folio", "documento"])
    ];

    public static EventoDePlantilla? Buscar(string? evento) =>
        Eventos.FirstOrDefault(e => e.Evento == (evento ?? string.Empty).Trim().ToLowerInvariant());
}

/// <summary>
/// Asunto, cabecera y pie que una empresa le pone a un correo del catálogo (PL-1). Esquema <c>validacion</c>, multi-tenant;
/// id determinista <c>{empresa:N}:{evento}:{canal}</c>. Todo nulo = el correo predeterminado (la versión igual sube).
/// Sin Liquid: solo variables <c>{{nombre}}</c> reemplazadas como texto.
/// </summary>
public sealed partial class PlantillaNotificacion
{
    public const int MaximoAsunto = 150;
    public const int MaximoTexto = 2000;

    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public string Evento { get; set; } = string.Empty;

    public string Canal { get; set; } = CatalogoDePlantillas.Correo;

    public string? Asunto { get; set; }

    public string? Cabecera { get; set; }

    public string? Pie { get; set; }

    /// <summary>Sube en cada cambio (también al volver al predeterminado).</summary>
    public int Version { get; set; }

    public DateTimeOffset ActualizadaEn { get; set; }

    public bool Personalizada => Asunto is not null || Cabecera is not null || Pie is not null;

    public static string Clave(Guid empresaId, string evento, string canal) => $"{empresaId:N}:{evento}:{canal}";

    public static PlantillaNotificacion Nueva(Guid empresaId, EventoDePlantilla evento, string canal) =>
        new() { Id = Clave(empresaId, evento.Evento, canal), EmpresaId = empresaId, Evento = evento.Evento, Canal = canal };

    /// <summary>Reemplaza los textos (ya validados con <see cref="Normalizar"/>) y sube la versión.</summary>
    public void Actualizar(TextosDePlantilla textos, DateTimeOffset ahora)
    {
        Asunto = textos.Asunto;
        Cabecera = textos.Cabecera;
        Pie = textos.Pie;
        Version++;
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Canal del catálogo (solo <c>correo</c>, 422 <c>canal-invalido</c>), textos vacíos ⇒ nulo, saltos de línea
    /// normalizados y validaciones de largo, del asunto en una sola línea y de las variables que admite el evento.
    /// </summary>
    public static TextosDePlantilla Normalizar(EventoDePlantilla evento, string? canal, string? asunto, string? cabecera, string? pie)
    {
        if ((canal ?? string.Empty).Trim().ToLowerInvariant() != CatalogoDePlantillas.Correo)
            throw ReglaDeNegocioException.Validacion("canal-invalido", $"'{canal}' no es un canal de plantillas. Por ahora solo existe 'correo'.");

        var textos = new TextosDePlantilla(Limpiar(asunto), Limpiar(cabecera), Limpiar(pie));
        if (textos.Asunto is { } a && a.Contains('\n'))
            throw Error("asunto-invalido", "asunto", "El asunto va en una sola línea.");
        if (textos.Asunto?.Length > MaximoAsunto)
            throw Error("asunto-demasiado-largo", "asunto", $"El asunto admite hasta {MaximoAsunto} caracteres.");
        if (textos.Cabecera?.Length > MaximoTexto)
            throw Error("texto-demasiado-largo", "cabecera", $"La cabecera admite hasta {MaximoTexto} caracteres.");
        if (textos.Pie?.Length > MaximoTexto)
            throw Error("texto-demasiado-largo", "pie", $"El pie admite hasta {MaximoTexto} caracteres.");

        foreach (var (campo, texto) in new[] { ("asunto", textos.Asunto), ("cabecera", textos.Cabecera), ("pie", textos.Pie) })
        {
            var noPermitidas = Variables(texto).Where(v => !evento.Variables.Contains(v)).Distinct().ToList();
            if (noPermitidas.Count > 0)
                throw new ReglaDeNegocioException(TipoFalla.Validacion, "variable-no-permitida",
                    $"Este correo no admite {string.Join(", ", noPermitidas.Select(v => "{{" + v + "}}"))}. " +
                    $"Se permiten: {string.Join(", ", evento.Variables.Select(v => "{{" + v + "}}"))}.")
                {
                    Datos = new Dictionary<string, object?> { ["campo"] = campo, ["permitidas"] = evento.Variables }
                };
        }

        return textos;
    }

    /// <summary>Nombres de las variables <c>{{nombre}}</c> (con espacios opcionales) que aparecen en el texto.</summary>
    public static IEnumerable<string> Variables(string? texto) =>
        texto is null ? [] : RegexVariable().Matches(texto).Select(m => m.Groups[1].Value);

    /// <summary>Reemplaza cada <c>{{nombre}}</c> por su valor como texto; una variable sin valor queda vacía.</summary>
    public static string Reemplazar(string texto, IReadOnlyDictionary<string, string> variables) =>
        RegexVariable().Replace(texto, m => variables.TryGetValue(m.Groups[1].Value, out var valor) ? valor : string.Empty);

    private static string? Limpiar(string? texto)
    {
        var limpio = (texto ?? string.Empty).Replace("\r\n", "\n").Replace('\r', '\n').Trim();
        return limpio.Length == 0 ? null : limpio;
    }

    private static ReglaDeNegocioException Error(string codigo, string campo, string mensaje) =>
        new(TipoFalla.Validacion, codigo, mensaje) { Datos = new Dictionary<string, object?> { ["campo"] = campo } };

    [GeneratedRegex(@"\{\{\s*([^{}]*?)\s*\}\}")]
    private static partial Regex RegexVariable();
}

/// <summary>Asunto, cabecera y pie ya normalizados (nulo = sin personalizar).</summary>
public sealed record TextosDePlantilla(string? Asunto, string? Cabecera, string? Pie);
