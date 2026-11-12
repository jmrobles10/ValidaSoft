using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Informes;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>Lo que lleva el PDF de una declaración jurada PEP: quién la pide, de quién es, de qué versión de la ficha y la declaración tal como se firmó.</summary>
/// <param name="Rol">Rol de la persona en la información legal (su nombre en español); nulo en la persona natural.</param>
public sealed record DatosDelPdfDeDeclaracionPep(
    string Empresa, string Tercero, string Seccion, int Version, DateTimeOffset FechaVersion, string? Rol, DeclaracionPep Declaracion, DateTimeOffset Generado);

/// <summary>PDF de la declaración jurada PEP (implementación en infraestructura, sin dependencias, con el lienzo del certificado).</summary>
public interface IGeneradorDePdfDeDeclaracionPep
{
    byte[] Generar(DatosDelPdfDeDeclaracionPep datos);
}

/// <summary>
/// PDF de la declaración jurada PEP de una persona del tercero (HU 49093), para la empresa que lo tiene como proveedor: el comprador
/// y el oficial de cumplimiento lo descargan junto a la sección de la ficha. Solo existe si la persona respondió «Sí» a «¿Es PEP?» y
/// hay declaración en esa versión (la inactiva no se entrega). Cada descarga queda registrada como exportación.
/// </summary>
public static class DeclaracionesPepEndpoints
{
    public const string NombreDelTipo = "DeclaracionPep";

    private static readonly Dictionary<RolPersonaRelacionada, string> Roles = new()
    {
        [RolPersonaRelacionada.RepresentanteLegal] = "Representante legal",
        [RolPersonaRelacionada.Suplente] = "Suplente del representante legal",
        [RolPersonaRelacionada.Apoderado] = "Apoderado",
        [RolPersonaRelacionada.Socio] = "Socio o accionista",
        [RolPersonaRelacionada.JuntaDirectiva] = "Miembro de la junta directiva",
        [RolPersonaRelacionada.RevisorFiscal] = "Revisor fiscal",
        [RolPersonaRelacionada.Contador] = "Contador"
    };

    /// <summary>
    /// <c>GET /api/v1/terceros/{terceroId}/declaraciones-pep/pdf?seccion=FICHA_GENERAL|FICHA_LEGAL&amp;persona={índice}&amp;version={n}</c>. Sin
    /// <c>version</c>, la vigente. Con vínculo con el tercero y solo versiones que la empresa puede ver: la vigente, la que aceptó y las que
    /// revisa en sus expedientes. 404 si no hay declaración activa de esa persona en esa versión.
    /// </summary>
    [WolverineGet("/api/v1/terceros/{terceroId}/declaraciones-pep/pdf")]
    [Authorize(Policy = Permisos.TercerosLeer)]
    public static async Task<IResult> Pdf(Guid terceroId, string seccion, int? persona, int? version, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        HttpContext http, TimeProvider reloj, IGeneradorDePdfDeDeclaracionPep generador, CancellationToken ct)
    {
        var tercero = await empresa.Sesion.LoadAsync<Tercero>(terceroId, ct);
        var vinculos = tercero is null ? [] : await empresa.Vinculos().Where(v => v.TerceroId == terceroId).ToListAsync(ct);
        // Sin vínculo con la empresa activa el tercero no existe para ella (T-4).
        if (tercero is null || vinculos.Count == 0)
            throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "No existe ese tercero en la empresa.");

        var definicion = CatalogoDeSecciones.Requerida(seccion);
        if (definicion.Codigo is not (CodigosDeSeccion.General or CodigosDeSeccion.Legal))
            throw ReglaDeNegocioException.Validacion("seccion-sin-declaracion-pep", "Solo la información general y la legal llevan declaración PEP.");

        SeccionDeFicha? stream;
        await using (var boveda = empresa.ConsultaBoveda())
            stream = await boveda.LoadAsync<SeccionDeFicha>(SeccionDeFicha.IdPara(terceroId, definicion.Codigo, null), ct);
        var vigente = stream?.Vigente ?? throw SinDeclaracion();
        var numero = version ?? vigente.Numero;
        if (!await PuedeVerAsync(empresa, vinculos, definicion.Codigo, vigente.Numero, numero, ct) || stream.Versiones.FirstOrDefault(v => v.Numero == numero) is not { } elegida)
            throw SinDeclaracion();

        var (declaracion, rol) = DeclaracionDe(definicion.Codigo, elegida.Datos, persona);
        if (declaracion is null)
            throw SinDeclaracion();

        var datosEmpresa = await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct);
        var ahora = reloj.GetUtcNow();
        var bytes = generador.Generar(new DatosDelPdfDeDeclaracionPep(datosEmpresa?.Nombre ?? "Validasoft", tercero.Nombre, definicion.Nombre, elegida.Numero,
            elegida.Fecha, rol, declaracion, ahora));
        var nombre = $"declaracion-pep-{Exportaciones.Slug(declaracion.Nombre ?? "persona")}-{Exportaciones.Slug(declaracion.Numero ?? "sin-documento")}-{Exportaciones.Local(ahora):yyyyMMdd}.pdf";
        return await Exportaciones.EntregarAsync(empresa, contexto, http, reloj,
            new ArchivoExportado(NombreDelTipo, "Pdf", bytes, Exportaciones.TipoPdf, nombre, 1, terceroId.ToString(), tercero.Nombre,
                vinculos[0].Id), ct);
    }

    /// <summary>
    /// La declaración activa (con «Sí» a PEP) de la persona de esos datos y su rol (nulo en la natural). En la General no hay índice; en la
    /// Legal es la posición en <c>personas</c>. Nula si no hay o está inactiva.
    /// </summary>
    public static (DeclaracionPep? Declaracion, string? Rol) DeclaracionDe(string seccion, JsonElement datos, int? persona)
    {
        if (seccion == CodigosDeSeccion.General)
        {
            var general = JsonDeFicha.Deserializar<DatosGenerales>(datos);
            return (general.Pep == true ? general.DeclaracionPep : null, null);
        }

        var personas = JsonDeFicha.Deserializar<DatosLegales>(datos).Personas ?? [];
        if (persona is not { } indice || indice < 0 || indice >= personas.Count)
            return (null, null);
        var p = personas[indice];
        return (p.Pep == true ? p.DeclaracionPep : null, p.Rol is { } rol ? Roles.GetValueOrDefault(rol) : null);
    }

    /// <summary>
    /// La empresa ve la versión vigente de la ficha de un tercero con el que tiene vínculo (Tercero 360), la que aceptó y la que revisa en
    /// un expediente suyo; cualquier otra no.
    /// </summary>
    private static async Task<bool> PuedeVerAsync(SesionDeEmpresa empresa, IReadOnlyList<Vinculo> vinculos, string seccion, int vigente, int numero,
        CancellationToken ct)
    {
        if (numero == vigente)
            return true;
        var ids = vinculos.Select(v => v.Id).ToArray();
        if (await empresa.Sesion.Query<SeccionAceptada>().AnyAsync(a => a.VinculoId.IsOneOf(ids) && a.Seccion == seccion && a.Version == numero, ct))
            return true;
        var expedientes = await empresa.Expedientes().Where(e => e.VinculoId.IsOneOf(ids)).ToListAsync(ct);
        return expedientes.SelectMany(e => e.Requisitos).Any(r => r.EsSeccion && r.Seccion == seccion && r.VersionActual?.Numero == numero);
    }

    private static ReglaDeNegocioException SinDeclaracion() =>
        ReglaDeNegocioException.NoEncontrado("declaracion-pep-no-encontrada", "Esa persona no tiene una declaración PEP activa en esa versión de la ficha.");
}
