using System.Net;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Dominio.Configuracion;

namespace Validasoft.Aplicacion.Comun;

/// <summary>
/// Aplica la plantilla de la empresa (PL-1) a un correo ya redactado antes de enviarlo: asunto propio con variables,
/// cabecera antes del cuerpo y pie después. Sin tenant, sin empresa o sin plantilla, el correo sale tal cual.
/// </summary>
public sealed class PlantillasDeCorreo(ISesiones sesiones)
{
    public async Task<MensajeDeCorreo> AplicarAsync(MensajeDeCorreo mensaje, string evento, Guid? tenantId, Guid? empresaId,
        IReadOnlyDictionary<string, string> variables, CancellationToken ct)
    {
        if (tenantId is not { } tenant || empresaId is not { } empresa)
            return mensaje;

        await using var sesion = sesiones.ParaTenant(tenant);
        var plantilla = await sesion.LoadAsync<PlantillaNotificacion>(PlantillaNotificacion.Clave(empresa, evento, CatalogoDePlantillas.Correo), ct);
        return plantilla is { Personalizada: true }
            ? ComposicionDeCorreo.Aplicar(mensaje, new TextosDePlantilla(plantilla.Asunto, plantilla.Cabecera, plantilla.Pie), variables)
            : mensaje;
    }
}

/// <summary>Composición pura del correo con la plantilla (PL-1); el texto del usuario nunca entra como HTML crudo.</summary>
public static class ComposicionDeCorreo
{
    public static MensajeDeCorreo Aplicar(MensajeDeCorreo mensaje, TextosDePlantilla textos, IReadOnlyDictionary<string, string> variables)
    {
        var asunto = textos.Asunto is null
            ? mensaje.Asunto
            : PlantillaNotificacion.Reemplazar(textos.Asunto, variables).Replace('\n', ' ').Replace('\r', ' ').Trim();
        var cabecera = textos.Cabecera is null ? null : PlantillaNotificacion.Reemplazar(textos.Cabecera, variables);
        var pie = textos.Pie is null ? null : PlantillaNotificacion.Reemplazar(textos.Pie, variables);

        var texto = string.Join("\n\n", new[] { cabecera, mensaje.Texto, pie }.OfType<string>());
        var html = Parrafo(cabecera) + mensaje.Html + Parrafo(pie);
        return mensaje with { Asunto = asunto.Length == 0 ? mensaje.Asunto : asunto, Texto = texto, Html = html };
    }

    private static string Parrafo(string? texto) =>
        texto is null ? string.Empty : "<p>" + WebUtility.HtmlEncode(texto).Replace("\n", "<br>") + "</p>";
}

/// <summary>El correo de un evento redactado con datos de ejemplo y sus variables (asunto por defecto y vista previa).</summary>
public sealed record EjemploDeCorreo(MensajeDeCorreo Mensaje, IReadOnlyDictionary<string, string> Variables);

/// <summary>
/// Correos de ejemplo con el redactor real (PL-1): la empresa con su nombre, folio <c>{PREFIJO}-E-{año}-000001</c> (el
/// certificado, <c>-C-</c>) y un tercero y un documento de ejemplo. Resultado: la aprobación; certificado: la revocación.
/// </summary>
public static class EjemplosDeCorreo
{
    private const string Para = "tercero@ejemplo.co";
    private const string TipoVinculo = "Proveedor";
    private const string Documento = "RUT";

    public static EjemploDeCorreo Redactar(RedactorDeCorreos redactor, string evento, string empresa, string prefijo, DateTimeOffset ahora)
    {
        var folio = $"{prefijo}-E-{ahora.Year}-000001";
        return evento switch
        {
            CatalogoDePlantillas.Invitacion => new(
                redactor.Redactar(new CorreoDeAccesoPendiente(PlantillaCorreo.Invitacion, Para, string.Empty, ahora.AddDays(15), empresa, TipoVinculo),
                    "enlace-de-ejemplo"),
                VariablesDe(empresa, tipoVinculo: TipoVinculo)),
            CatalogoDePlantillas.Observacion => new(
                redactor.RedactarObservacion(new CorreoDeObservacionPendiente(Para, empresa, folio,
                    [new ObservacionParaCorreo(["El documento está vencido."], "Adjunta el RUT actualizado.", Documento)]), "enlace-de-ejemplo"),
                VariablesDe(empresa, folio)),
            CatalogoDePlantillas.Resultado => new(
                redactor.RedactarResultado(new CorreoDeResultadoPendiente(Para, empresa, folio, true, [])), VariablesDe(empresa, folio)),
            CatalogoDePlantillas.ConsentimientoEmpresa => new(
                redactor.RedactarConstancia(new CorreoDeConstanciaPendiente(Para, empresa,
                    [new TextoAceptadoParaCorreo($"Autorización de tratamiento de datos de {empresa}", 1, "3f2a9c41d07e8b65a1c4f0e2d9b87a13")], ahora)),
                VariablesDe(empresa)),
            CatalogoDePlantillas.Vencido => new(
                redactor.RedactarVencido(new CorreoDeVencidoPendiente(Para, empresa, Documento, DateOnly.FromDateTime(ahora.UtcDateTime))),
                VariablesDe(empresa, documento: Documento)),
            CatalogoDePlantillas.Certificado => new(
                redactor.RedactarCertificado(new CorreoDeCertificadoPendiente(Para, empresa, $"{prefijo}-C-{ahora.Year}-000001", true, ahora)),
                VariablesDe(empresa, $"{prefijo}-C-{ahora.Year}-000001")),
            _ => throw new ArgumentOutOfRangeException(nameof(evento), evento, "Evento sin plantilla.")
        };
    }

    /// <summary>Variables de un correo; las nulas no se incluyen (quedan vacías al reemplazar).</summary>
    public static IReadOnlyDictionary<string, string> VariablesDe(string? empresa, string? folio = null, string? tipoVinculo = null,
        string? documento = null)
    {
        var variables = new Dictionary<string, string>(StringComparer.Ordinal);
        if (empresa is not null)
            variables["empresa"] = empresa;
        if (folio is not null)
            variables["folio"] = folio;
        if (tipoVinculo is not null)
            variables["tipoVinculo"] = tipoVinculo;
        if (documento is not null)
            variables["documento"] = documento;
        return variables;
    }
}
