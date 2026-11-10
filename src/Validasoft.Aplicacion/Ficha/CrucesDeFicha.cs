using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Aplicacion.Ficha;

/// <summary>
/// Cruza las secciones vigentes del tercero con los documentos que las respaldan (F5, FM-8). Lo publican el análisis de IA de
/// una versión de documento (al registrarse) y el guardado de una versión de sección (en su outbox). Idempotente: cada par
/// (versión de la sección, versión del documento) se cruza una sola vez.
/// </summary>
public sealed record CruzarFichaConDocumentos(Guid TerceroId);

public static class CruzarFichaConDocumentosHandler
{
    public static Task Handle(CruzarFichaConDocumentos mensaje, ISesiones sesiones, TimeProvider reloj, CancellationToken ct) =>
        CrucesDeFicha.CruzarAsync(sesiones, mensaje.TerceroId, reloj, ct);
}

/// <summary>
/// El cruce sección ↔ documento como hecho compartido (FM-8): se guarda en la versión de la sección (evento
/// <see cref="CruceDeSeccionRegistrado"/>), una vez por versión de documento, con los hechos de IA del documento (ADR-0012). No
/// llama a OpenAI ni consume cupo por sí mismo: usa el análisis que ya se hizo (y cobró) por la versión del documento; cada
/// empresa lo ve solo si recibió ese análisis (IA-11). Sin análisis no hay cruce: la revisión es manual (fail-closed).
/// </summary>
public static class CrucesDeFicha
{
    /// <summary>
    /// Documentos que respaldan una versión de la sección: sus soportes explícitos (de un tipo que la respalda, FM-20) y la versión
    /// actual de cada documento global del tercero cuyo tipo la respalda (mapa de §5.1, «usar como soporte»). En la financiera, un
    /// documento que no es soporte explícito cuenta solo si su análisis dice que es del mismo periodo.
    /// </summary>
    public static IReadOnlyList<(Documento Documento, int Version)> Respaldos(SeccionDeFicha seccion, VersionDeSeccion version,
        IReadOnlyCollection<Documento> documentos)
    {
        var porId = documentos.ToDictionary(d => d.Id);
        var respaldos = new List<(Documento, int)>();
        foreach (var soporte in version.Soportes)
        {
            if (porId.TryGetValue(soporte.DocumentoId, out var documento) && MapaDeRespaldo.Respalda(documento.TipoCanonico, seccion.Seccion))
                respaldos.Add((documento, soporte.Version));
        }

        foreach (var documento in documentos.Where(d => d.Ambito == AmbitoDocumento.Global && MapaDeRespaldo.Respalda(d.TipoCanonico, seccion.Seccion)))
        {
            if (documento.VersionActual is not { } actual)
                continue;
            if (seccion.Periodo is { } periodo && (documento.Buscar(actual)?.Ia is not { } ia || MapaDeRespaldo.Periodo(ia.Campos) != periodo))
                continue;
            respaldos.Add((documento, actual));
        }

        return [.. respaldos.DistinctBy(r => (r.Item1.Id, r.Item2))];
    }

    /// <summary>Calcula y registra los cruces que falten de las versiones vigentes del tercero. Devuelve cuántos registró.</summary>
    public static async Task<int> CruzarAsync(ISesiones sesiones, Guid terceroId, TimeProvider reloj, CancellationToken ct)
    {
        Tercero? tercero;
        await using (var global = sesiones.ConsultaGlobal())
            tercero = await global.LoadAsync<Tercero>(terceroId, ct);
        if (tercero is null)
            return 0;

        List<SeccionDeFicha> secciones;
        List<Documento> documentos;
        await using (var consulta = sesiones.ConsultaBoveda())
        {
            secciones = [.. await consulta.Query<SeccionDeFicha>().Where(s => s.TerceroId == terceroId).ToListAsync(ct)];
            documentos = [.. await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct)];
        }

        var total = 0;
        foreach (var seccion in secciones.Where(s => s.Vigente is not null))
        {
            var pendientes = Pendientes(seccion, documentos, tercero.TipoPersona, reloj.GetUtcNow());
            if (pendientes.Count == 0)
                continue;

            total += await Peticiones.ConReintentosAsync(async () =>
            {
                await using var sesion = sesiones.Boveda();
                var stream = await sesion.Events.FetchForExclusiveWriting<SeccionDeFicha>(seccion.Id, ct);
                if (stream.Aggregate is not { } actual)
                    return 0;
                var registrados = 0;
                foreach (var (numero, cruce) in pendientes)
                {
                    if (actual.RegistrarCruce(numero, cruce) is not { } evento)
                        continue;
                    stream.AppendOne(evento);
                    actual.Apply(evento);
                    registrados++;
                }

                if (registrados > 0)
                    await sesion.SaveChangesAsync(ct);
                return registrados;
            });
        }

        return total;
    }

    /// <summary>Cruces que faltan en la versión vigente: los de los respaldos con análisis de IA que aún no se cruzaron.</summary>
    private static List<(int Numero, CruceDeSeccion Cruce)> Pendientes(SeccionDeFicha seccion, IReadOnlyCollection<Documento> documentos,
        TipoPersona tipoPersona, DateTimeOffset ahora)
    {
        var vigente = seccion.Vigente!;
        var pendientes = new List<(int, CruceDeSeccion)>();
        foreach (var (documento, numero) in Respaldos(seccion, vigente, documentos))
        {
            if (vigente.Cruces.Any(c => c.Es(documento.Id, numero)) || documento.Buscar(numero)?.Ia is not { } ia)
                continue;
            if (CruceConDocumento.Cruzar(seccion.Seccion, vigente.Datos, tipoPersona, seccion.Periodo, documento.TipoCanonico!, documento.Id, numero,
                    ia.Campos, ia.Confianza, ia.Legible, ahora) is { } cruce)
                pendientes.Add((vigente.Numero, cruce));
        }

        return pendientes;
    }
}
