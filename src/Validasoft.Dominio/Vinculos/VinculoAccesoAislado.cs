using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Vinculos;

/// <summary>
/// Qué pasa con el acceso del correo invitado cuando no es un contacto conocido de un tercero que ya existía (acceso aislado).
/// Un vínculo sin este estado es el de siempre: su correo es de confianza.
/// </summary>
public enum EstadoAccesoAislado
{
    /// <summary>Los contactos del tercero aún no deciden: el correo ve y sube solo lo de este vínculo y la bóveda global no se comparte con la empresa.</summary>
    Pendiente,

    /// <summary>Un contacto del tercero aprobó el correo: acceso completo, la bóveda se comparte como en cualquier vínculo.</summary>
    Aprobado,

    /// <summary>Un contacto del tercero rechazó el correo: la sesión de esa persona se corta; la empresa puede invitar a otro correo.</summary>
    Rechazado
}

/// <summary>La invitación fue a un correo que no es contacto conocido del tercero: el vínculo queda con acceso aislado hasta que un contacto lo decida.</summary>
public sealed record AccesoAisladoSolicitado(Guid SolicitudId, string Correo, DateTimeOffset Fecha);

/// <summary>Un contacto del tercero aprobó el correo del vínculo: desde aquí tiene acceso completo.</summary>
public sealed record AccesoAisladoAprobado(Guid SolicitudId, string Correo, DateTimeOffset Fecha);

/// <summary>Un contacto del tercero rechazó el correo del vínculo (sin su motivo: es del tercero).</summary>
public sealed record AccesoAisladoRechazado(Guid SolicitudId, string Correo, DateTimeOffset Fecha);

/// <summary>
/// Acceso aislado del vínculo: la empresa invitó a un correo que no es contacto conocido de un tercero que ya existía. Mientras no lo apruebe un
/// contacto del tercero, nada de la bóveda global del tercero se comparte con la empresa por este vínculo (documentos, ficha); lo que el correo suba
/// para esta empresa queda en cuarentena, ligado solo a este vínculo.
/// </summary>
public sealed partial class Vinculo
{
    /// <summary>Nulo si el correo es de confianza (lo normal); si no, en qué va la decisión de los contactos del tercero.</summary>
    public EstadoAccesoAislado? AccesoAislado { get; set; }

    /// <summary>Solicitud de acceso (stream de la bóveda) que decide el acceso de este vínculo.</summary>
    public Guid? SolicitudDeAccesoId { get; set; }

    public DateTimeOffset? AccesoAisladoDesde { get; set; }

    public DateTimeOffset? AccesoAisladoResueltoEn { get; set; }

    /// <summary>
    /// ¿Se puede compartir con la empresa la bóveda global del tercero por este vínculo? No mientras el acceso esté pendiente o rechazado. Con
    /// <c>Aprobado</c> o sin acceso aislado, sí (siempre con la regla de B-1: solo tras aceptar la invitación).
    /// </summary>
    [JsonIgnore]
    public bool BovedaDisponible => AccesoAislado is null or EstadoAccesoAislado.Aprobado;

    /// <summary>El acceso aislado se rechazó: la empresa puede invitar a otro correo aunque ya se hubiera aceptado la invitación.</summary>
    [JsonIgnore]
    public bool AccesoRechazado => AccesoAislado == EstadoAccesoAislado.Rechazado;

    /// <summary>
    /// Deja el vínculo con acceso aislado (el correo invitado no es contacto conocido). <paramref name="correo"/> es el invitado: el de la invitación
    /// que se está enviando, que puede no ser aún el de <see cref="CorreoTercero"/>. Si el vínculo ya espera la misma solicitud, nulo (idempotente:
    /// reenviar la invitación al mismo correo no repite el evento); otra solicitud (otro correo) empieza de cero.
    /// </summary>
    public AccesoAisladoSolicitado? SolicitarAccesoAislado(Guid solicitudId, string correo, DateTimeOffset ahora)
    {
        if (AccesoAislado is not null && SolicitudDeAccesoId == solicitudId)
            return null;
        if (string.IsNullOrWhiteSpace(correo))
            throw ReglaDeNegocioException.Validacion("correo-invitacion-requerido", "El vínculo necesita el correo invitado para decidir su acceso.");
        return new AccesoAisladoSolicitado(solicitudId, correo.Trim().ToLowerInvariant(), ahora);
    }

    /// <summary>La decisión de los contactos llegó al vínculo: aprobada. Sin acceso aislado, ya aprobada o de otra solicitud ⇒ nulo.</summary>
    public AccesoAisladoAprobado? AprobarAccesoAislado(Guid solicitudId, DateTimeOffset ahora) =>
        AccesoAislado is null or EstadoAccesoAislado.Aprobado || SolicitudDeAccesoId != solicitudId
            ? null
            : new AccesoAisladoAprobado(solicitudId, CorreoTercero, ahora);

    /// <summary>La decisión de los contactos llegó al vínculo: rechazada. Sin acceso aislado, ya rechazada o de otra solicitud ⇒ nulo.</summary>
    public AccesoAisladoRechazado? RechazarAccesoAislado(Guid solicitudId, DateTimeOffset ahora) =>
        AccesoAislado is null or EstadoAccesoAislado.Rechazado || SolicitudDeAccesoId != solicitudId
            ? null
            : new AccesoAisladoRechazado(solicitudId, CorreoTercero, ahora);

    public void Apply(AccesoAisladoSolicitado e)
    {
        AccesoAislado = EstadoAccesoAislado.Pendiente;
        SolicitudDeAccesoId = e.SolicitudId;
        AccesoAisladoDesde = e.Fecha;
        AccesoAisladoResueltoEn = null;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(AccesoAisladoAprobado e)
    {
        AccesoAislado = EstadoAccesoAislado.Aprobado;
        AccesoAisladoResueltoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    public void Apply(AccesoAisladoRechazado e)
    {
        AccesoAislado = EstadoAccesoAislado.Rechazado;
        AccesoAisladoResueltoEn = e.Fecha;
        ActualizadoEn = e.Fecha;
    }

    /// <summary>
    /// Un correo nuevo (otra invitación) empieza de cero: lo decidido del anterior no vale. Si el anterior se había rechazado y ya había aceptado, la
    /// aceptación se anula: la persona nueva debe aceptar los textos legales ella misma (B-1).
    /// </summary>
    private void ReiniciarAccesoPorCorreoNuevo()
    {
        if (AccesoAislado == EstadoAccesoAislado.Rechazado && InvitacionAceptada)
        {
            InvitacionAceptada = false;
            InvitacionAceptadaEn = null;
        }

        AccesoAislado = null;
        SolicitudDeAccesoId = null;
        AccesoAisladoDesde = null;
        AccesoAisladoResueltoEn = null;
    }
}
