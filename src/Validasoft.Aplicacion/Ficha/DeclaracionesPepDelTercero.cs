using System.Security.Cryptography;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Informes;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>
/// Cada vez que el tercero baja el PDF de su propia declaración jurada PEP queda esta fila (la de «quién, cuándo y desde dónde» que
/// dejan las demás descargas): el equivalente, del lado del tercero, de lo que la empresa registra como exportación. Es global, como el
/// propio tercero, y no lleva el contenido del PDF: solo su huella.
/// </summary>
public sealed class AccesoADeclaracionPep
{
    public Guid Id { get; set; }

    public Guid TerceroId { get; set; }

    /// <summary><c>FICHA_GENERAL</c> o <c>FICHA_LEGAL</c>.</summary>
    public string Seccion { get; set; } = string.Empty;

    /// <summary>Posición de la persona en la información legal; nulo en la general.</summary>
    public int? Persona { get; set; }

    /// <summary>Versión de la sección de la que salió el PDF.</summary>
    public int Version { get; set; }

    public string ActorTipo { get; set; } = string.Empty;

    public string ActorId { get; set; } = string.Empty;

    public string Ip { get; set; } = string.Empty;

    public string UserAgent { get; set; } = string.Empty;

    /// <summary>SHA-256 (hex) del PDF entregado.</summary>
    public string Sha256 { get; set; } = string.Empty;

    public DateTimeOffset Fecha { get; set; }
}

/// <summary>
/// El tercero descarga el PDF de su propia declaración jurada PEP (HU 49093, DP-16): solo la de la versión vigente de su ficha, solo de
/// quien responde «Sí» a «¿Es PEP?» y tiene declaración, y solo la suya (el tercero sale de su sesión: no hay forma de pedir la de otro,
/// ni siquiera con un identificador cambiado). El PDF no está dirigido a ninguna empresa: lleva el nombre y el logo de Validasoft. Cada
/// descarga queda en <see cref="AccesoADeclaracionPep"/>.
/// </summary>
public static class DeclaracionesPepDelTerceroEndpoints
{
    /// <summary>Nombre del emisor del PDF del tercero (la declaración es de su ficha, no de una empresa).</summary>
    public const string Emisor = "Validasoft";

    /// <summary>
    /// <c>GET /public/v1/ficha/{seccion}/declaracion-pep/pdf?persona={índice}</c> (<c>FICHA_GENERAL</c> sin índice, <c>FICHA_LEGAL</c> con el de la
    /// persona en <c>personas</c>). 404 <c>declaracion-pep-no-encontrada</c> si no hay declaración activa de esa persona en la versión vigente.
    /// </summary>
    [WolverineGet("/public/v1/ficha/{seccion}/declaracion-pep/pdf")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IResult> Pdf(string seccion, int? persona, HttpContext http, ISesiones sesiones, IContextoSolicitud contexto, TimeProvider reloj,
        IGeneradorDePdfDeDeclaracionPep generador, CancellationToken ct)
    {
        var terceroId = SesionDeTercero.De(http.User).TerceroId;
        var definicion = CatalogoDeSecciones.Requerida(seccion);
        if (definicion.Codigo is not (CodigosDeSeccion.General or CodigosDeSeccion.Legal))
            throw ReglaDeNegocioException.Validacion("seccion-sin-declaracion-pep", "Solo la información general y la legal llevan declaración PEP.");

        var tercero = await GuardadoDeSecciones.TerceroAsync(sesiones, terceroId, ct);
        SeccionDeFicha? ficha;
        await using (var boveda = sesiones.ConsultaBoveda())
            ficha = await boveda.LoadAsync<SeccionDeFicha>(SeccionDeFicha.IdPara(terceroId, definicion.Codigo, null), ct);
        var vigente = ficha?.Vigente;
        var (declaracion, rol) = vigente is null ? (null, null) : DeclaracionesPepEndpoints.DeclaracionDe(definicion.Codigo, vigente.Datos, persona);
        if (vigente is null || declaracion is null)
            throw ReglaDeNegocioException.NoEncontrado("declaracion-pep-no-encontrada", "No tienes una declaración PEP activa en la versión vigente de esa sección.");

        var ahora = reloj.GetUtcNow();
        var bytes = generador.Generar(new DatosDelPdfDeDeclaracionPep(Emisor, tercero.Nombre, definicion.Nombre, vigente.Numero, vigente.Fecha, rol, declaracion, ahora));
        await using (var sesion = sesiones.Global())
        {
            sesion.Store(new AccesoADeclaracionPep
            {
                Id = Guid.CreateVersion7(),
                TerceroId = terceroId,
                Seccion = definicion.Codigo,
                Persona = definicion.Codigo == CodigosDeSeccion.Legal ? persona : null,
                Version = vigente.Numero,
                ActorTipo = ActorTipo.Tercero.ToString(),
                ActorId = contexto.ActorId,
                Ip = BovedaEndpoints.Ip(http),
                UserAgent = BovedaEndpoints.UserAgent(http),
                Sha256 = Convert.ToHexStringLower(SHA256.HashData(bytes)),
                Fecha = ahora
            });
            await sesion.SaveChangesAsync(ct);
        }

        return Results.File(bytes, Exportaciones.TipoPdf, DeclaracionesPepEndpoints.NombreDelArchivo(declaracion, ahora));
    }
}
