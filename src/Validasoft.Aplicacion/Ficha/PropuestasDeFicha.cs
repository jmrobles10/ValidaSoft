using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>
/// Actualizar desde el documento (F5, FM-14): lo que la versión actual de cada documento global del tercero que respalda
/// secciones (RUT, Cámara, estados financieros, cédula) propone para ellas, con los campos que extrajo su análisis de IA. Solo
/// lectura: el tercero confirma o corrige en el wizard y guarda como siempre (con el documento como soporte), lo que abre la
/// revalidación por sección en cada empresa (F2). El tercero ve los valores leídos de su propio documento, nunca la confianza,
/// las alertas, el costo ni quién pagó el análisis. Sin análisis (sin plan, sin cupo o fallido), la propuesta no está
/// disponible y el tercero diligencia con el documento a la vista.
/// </summary>
public static class PropuestasDeFichaEndpoints
{
    public const string MotivoSinLectura = "sin-lectura-automatica";

    /// <summary>Resumen para «Mi información»: una fila por documento y sección con cambios (o sin lectura automática y más nuevo que la sección).</summary>
    [WolverineGet("/public/v1/ficha/propuestas")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IReadOnlyList<PropuestaDeSeccionDto>> Listar(HttpContext http, ISesiones sesiones, TimeProvider reloj, CancellationToken ct)
    {
        var terceroId = SesionDeTercero.De(http.User).TerceroId;
        return await PropuestasAsync(sesiones, terceroId, null, null, false, reloj, ct);
    }

    /// <summary>Las propuestas para una sección (y periodo, en la financiera), con los datos de la sección ya cambiados para prellenar el formulario.</summary>
    [WolverineGet("/public/v1/ficha/{seccion}/propuestas")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<IReadOnlyList<PropuestaDeSeccionDto>> DeSeccion(string seccion, int? periodo, HttpContext http, ISesiones sesiones,
        TimeProvider reloj, CancellationToken ct)
    {
        var terceroId = SesionDeTercero.De(http.User).TerceroId;
        var definicion = CatalogoDeSecciones.Requerida(seccion);
        var periodoValido = CatalogoDeSecciones.PeriodoValido(definicion, periodo, reloj.Hoy());
        return await PropuestasAsync(sesiones, terceroId, definicion, periodoValido, true, reloj, ct);
    }

    public static async Task<IReadOnlyList<PropuestaDeSeccionDto>> PropuestasAsync(ISesiones sesiones, Guid terceroId, DefinicionDeSeccion? solo, int? periodo,
        bool conDatos, TimeProvider reloj, CancellationToken ct)
    {
        var tercero = await GuardadoDeSecciones.TerceroAsync(sesiones, terceroId, ct);
        List<Documento> documentos;
        Dictionary<Guid, SeccionDeFicha> secciones;
        await using (var boveda = sesiones.ConsultaBoveda())
        {
            documentos = [.. await boveda.Query<Documento>().Where(d => d.TerceroId == terceroId && d.Ambito == AmbitoDocumento.Global).ToListAsync(ct)];
            secciones = (await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).ToListAsync(ct)).ToDictionary(s => s.Id);
        }

        var propuestas = new List<PropuestaDeSeccionDto>();
        await using var global = sesiones.ConsultaGlobal();
        foreach (var documento in documentos.Where(d => MapaDeRespaldo.RespaldaAlguna(d.TipoCanonico)).OrderBy(d => d.NombreTipo))
        {
            if (documento.VersionActual is not { } numero || documento.Buscar(numero) is not { } version)
                continue;
            var ia = version.Ia;
            var referencia = new DocumentoDePropuestaDto(documento.Id, numero, documento.TipoCanonico!, documento.NombreTipo, version.CargadaEn);
            foreach (var codigo in MapaDeRespaldo.Secciones(documento.TipoCanonico))
            {
                var definicion = CatalogoDeSecciones.Buscar(codigo)!;
                if (!definicion.Obligatoria(tercero.TipoPersona) || (solo is not null && solo.Codigo != codigo))
                    continue;

                // La financiera del periodo del documento (los estados financieros dicen de qué año son).
                var periodoDelDocumento = definicion.PorPeriodo ? ia is null ? null : MapaDeRespaldo.Periodo(ia.Campos) : null;
                if (definicion.PorPeriodo && (periodoDelDocumento is null || (solo is not null && periodo != periodoDelDocumento)))
                    continue;
                var seccion = secciones.GetValueOrDefault(SeccionDeFicha.IdPara(terceroId, codigo, periodoDelDocumento));
                var vigente = seccion?.Vigente;

                if (ia is null || !ia.Legible)
                {
                    // Sin lectura automática: solo se avisa si el documento es más nuevo que la sección y no la respalda ya.
                    var yaLaRespalda = vigente?.Soportes.Any(s => s.DocumentoId == documento.Id && s.Version == numero) == true;
                    if (!yaLaRespalda && (vigente is null || vigente.Fecha < version.CargadaEn))
                        propuestas.Add(new PropuestaDeSeccionDto(codigo, definicion.Nombre, periodoDelDocumento, referencia, false, MotivoSinLectura, []));
                    continue;
                }

                var propuesta = await ValidadaAsync(global, definicion, vigente?.Datos, tercero, periodoDelDocumento, documento.TipoCanonico!, ia.Campos,
                    reloj.Hoy(), ct);
                if (!propuesta.HayCambios)
                    continue;
                propuestas.Add(new PropuestaDeSeccionDto(codigo, definicion.Nombre, periodoDelDocumento, referencia, true, null,
                    [.. propuesta.Campos.Select(c => new CampoPropuestoDto(c.Campo, c.Etiqueta, c.Actual, c.Propuesto))], conDatos ? propuesta.Datos : null));
            }
        }

        return propuestas;
    }

    /// <summary>
    /// La propuesta sin lo que no pasaría la regla única de la sección (FM-16): cada campo propuesto que produce un error de
    /// formato (un código fuera del catálogo, un correo inválido…) se deja como estaba y se vuelve a evaluar.
    /// </summary>
    private static async Task<PropuestaDeSeccion> ValidadaAsync(IQuerySession global, DefinicionDeSeccion definicion, JsonElement? actuales, Tercero tercero,
        int? periodo, string tipoDocumento, IReadOnlyDictionary<string, JsonElement> extraidos, DateOnly hoy, CancellationToken ct)
    {
        var contexto = new ContextoDeSeccion(tercero.TipoPersona, tercero.Identificacion, periodo, hoy);
        var excluidos = new HashSet<string>(StringComparer.Ordinal);
        while (true)
        {
            var propuesta = PropuestaDesdeDocumento.Proponer(definicion.Codigo, actuales, tercero.TipoPersona, periodo, tipoDocumento, extraidos, excluidos);
            if (!propuesta.HayCambios)
                return propuesta;

            string? campoConError;
            try
            {
                var datos = ReglasDeFicha.Leer(definicion, propuesta.Datos, contexto);
                var catalogos = await ImportacionDeCatalogos.CargarAsync(global, definicion.Esquema.Referencias(datos), ct);
                var errores = ReglasDeFicha.Evaluar(definicion, datos, contexto, catalogos, false, 0).Errores;
                campoConError = errores.Select(e => e.Campo).FirstOrDefault(e => Culpable(propuesta, e) is not null);
                if (errores.Count == 0 || campoConError is null)
                    return propuesta;
            }
            catch (ReglaDeNegocioException e)
            {
                campoConError = e.Datos?.GetValueOrDefault("campo") as string;
            }

            if (campoConError is null || Culpable(propuesta, campoConError) is not { } culpable)
                return new PropuestaDeSeccion([], actuales ?? JsonSerializer.SerializeToElement(new { })); // no se puede proponer sin romper la sección
            excluidos.Add(culpable);
        }
    }

    /// <summary>El campo propuesto que causa el error: el mismo o el que lo contiene (<c>personas[2]</c> en <c>personas[2].numero</c>).</summary>
    private static string? Culpable(PropuestaDeSeccion propuesta, string campoConError) =>
        propuesta.Campos.Select(c => c.Campo).FirstOrDefault(c => campoConError == c || campoConError.StartsWith(c + ".", StringComparison.Ordinal) ||
                                                                 campoConError.StartsWith(c + "[", StringComparison.Ordinal));
}
