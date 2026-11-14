using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>
/// Alcance fino de un token embebido de un <b>expediente</b> (IO-5): no sirve para toda la bóveda del tercero sino para lo de su vínculo. Los grupos que su componente
/// alcanza ya los acota <see cref="SesionDeTercero.PermiteEmbebida"/>; aquí se acota la ficha, que es global: solo las secciones que el expediente de ese vínculo pide
/// (las demás y los resúmenes de todo el tercero responden 403 <c>alcance-de-sesion</c>). Cualquier otra sesión no se limita por aquí.
/// </summary>
public static class AlcanceDeEmbebido
{
    /// <summary>La sección está entre los requisitos del expediente del vínculo del token (el abierto o, si no hay, el último); si no, 403.</summary>
    public static async Task ExigirSeccionAsync(SesionDeTercero tercero, string seccion, ISesiones sesiones, CancellationToken ct)
    {
        if (!tercero.EsDeUnExpediente)
            return;

        var vinculoId = tercero.Vinculos.Count > 0 ? tercero.Vinculos[0].VinculoId : Guid.Empty;
        await using var consulta = sesiones.ConsultaGlobal();
        var vinculo = await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.Id == vinculoId).FirstOrDefaultAsync(ct);
        var expedienteId = vinculo?.ExpedienteAbiertoId ?? vinculo?.UltimoExpedienteId;
        var expediente = expedienteId is { } id
            ? await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.Id == id).FirstOrDefaultAsync(ct)
            : null;
        if (expediente is null || !expediente.Requisitos.Any(r => r.EsSeccion && string.Equals(r.Seccion, seccion, StringComparison.OrdinalIgnoreCase)))
            throw ReglaDeNegocioException.Prohibido("alcance-de-sesion",
                "Esta sesión se abrió para un expediente y solo alcanza las secciones de información que ese expediente pide.");
    }

    /// <summary>Lo que resume todo el tercero (todas las propuestas de su ficha) no entra en el alcance de un expediente: 403 <c>alcance-de-sesion</c>.</summary>
    public static void ExigirSesionDelTercero(SesionDeTercero tercero)
    {
        if (tercero.EsDeUnExpediente)
            throw ReglaDeNegocioException.Prohibido("alcance-de-sesion",
                "Esta sesión se abrió para un expediente y no alcanza lo que resume toda tu información.");
    }
}
