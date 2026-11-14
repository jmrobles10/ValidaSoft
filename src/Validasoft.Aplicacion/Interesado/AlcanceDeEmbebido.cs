using Marten;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>
/// Alcance fino de un token embebido de un <b>expediente</b> (IO-5): no sirve para toda la bóveda del tercero sino para lo de su vínculo. Los grupos que su componente
/// alcanza ya los acota <see cref="SesionDeTercero.PermiteEmbebida"/>; aquí se acotan la ficha, que es global (solo las secciones que el expediente de ese vínculo pide;
/// las demás y los resúmenes de todo el tercero responden 403 <c>alcance-de-sesion</c>) y las cuentas bancarias (solo la del vínculo, y solo si el expediente la exige o
/// ya la tiene: <see cref="CuentasDelExpedienteAsync"/>). Cualquier otra sesión no se limita por aquí.
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

    /// <summary>
    /// Lo que un token embebido de un <b>expediente</b> puede hacer con las cuentas bancarias (decisión AV): solo ver y designar la cuenta de <b>su vínculo</b> —la que ya
    /// designó, propuso o le rechazaron con esa empresa, o la que registró desde ese expediente— y solo si el expediente la exige o el vínculo ya tiene una. Nunca listar
    /// ni tocar las demás cuentas del tercero: designar una cuenta cualquiera le serviría a una empresa para leer su número completo desde su lado.
    /// </summary>
    public sealed record CuentasDelToken(Vinculo Vinculo, Expediente? Expediente)
    {
        /// <summary>El expediente del vínculo exige una cuenta designada y aprobada para aprobarse.</summary>
        public bool Exige => Expediente?.ExigeCuentaDesignada == true;

        /// <summary>La cuenta ya está ligada al vínculo: designada, propuesta o rechazada con esa empresa.</summary>
        public bool Referencia(Guid cuentaId) =>
            new[] { Vinculo.CuentaDesignada, Vinculo.CuentaPropuesta, Vinculo.CuentaRechazada }.Any(d => d?.Cuenta.CuentaId == cuentaId);

        /// <summary>La cuenta es del alcance del token: la ligada al vínculo o la que se registró desde su expediente.</summary>
        public bool EsDelAlcance(CuentaBancaria cuenta) => cuenta.VinculoDeOrigenId == Vinculo.Id || Referencia(cuenta.Id);
    }

    /// <summary>
    /// El vínculo y el expediente del token de un expediente, si puede tocar cuentas: el expediente la exige o el vínculo ya tiene una cuenta ligada. Si no, 403
    /// <c>alcance-de-sesion</c>.
    /// </summary>
    public static async Task<CuentasDelToken> CuentasDelExpedienteAsync(SesionDeTercero tercero, ISesiones sesiones, CancellationToken ct)
    {
        var vinculoId = tercero.Vinculos.Count > 0 ? tercero.Vinculos[0].VinculoId : Guid.Empty;
        await using var consulta = sesiones.ConsultaGlobal();
        var vinculo = await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.Id == vinculoId).FirstOrDefaultAsync(ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");
        var expedienteId = vinculo.ExpedienteAbiertoId ?? vinculo.UltimoExpedienteId;
        var expediente = expedienteId is { } id ? await consulta.Query<Expediente>().Where(e => e.AnyTenant() && e.Id == id).FirstOrDefaultAsync(ct) : null;
        var alcance = new CuentasDelToken(vinculo, expediente);
        if (!alcance.Exige && vinculo.CuentaDesignada is null && vinculo.CuentaPropuesta is null && vinculo.CuentaRechazada is null)
            throw ReglaDeNegocioException.Prohibido("alcance-de-sesion",
                "Esta sesión se abrió para un expediente que no pide cuenta bancaria: no alcanza las cuentas del tercero.");
        return alcance;
    }

    /// <summary>Lo que resume todo el tercero (todas las propuestas de su ficha) no entra en el alcance de un expediente: 403 <c>alcance-de-sesion</c>.</summary>
    public static void ExigirSesionDelTercero(SesionDeTercero tercero)
    {
        if (tercero.EsDeUnExpediente)
            throw ReglaDeNegocioException.Prohibido("alcance-de-sesion",
                "Esta sesión se abrió para un expediente y no alcanza lo que resume toda tu información.");
    }
}
