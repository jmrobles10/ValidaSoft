using System.Security.Cryptography;
using System.Text;
using System.Text.Json.Serialization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Terceros;

public enum EstadoSolicitudDeAcceso
{
    /// <summary>Los contactos del tercero aún no deciden: el correo entra con acceso aislado.</summary>
    Pendiente,

    /// <summary>Un contacto del tercero lo aprobó: el correo pasó a ser contacto y tiene acceso completo.</summary>
    Aprobada,

    /// <summary>Un contacto del tercero lo rechazó: el correo queda bloqueado para ese tercero.</summary>
    Rechazada
}

/// <summary>Una empresa que invitó al correo (una por vínculo): con ella se avisa a los contactos y se le refleja la decisión.</summary>
public sealed record InvitacionDeAcceso(Guid TenantId, Guid EmpresaId, Guid VinculoId, string Empresa, DateTimeOffset Fecha);

// ---------- Eventos (stream SolicitudDeAcceso-{id}, tenant técnico «boveda») ----------

/// <summary>Un correo que no es contacto del tercero fue invitado por primera vez: nace la solicitud de acceso, con su decisión pendiente.</summary>
public sealed record SolicitudDeAccesoCreada(Guid SolicitudId, Guid TerceroId, string Correo, DateTimeOffset Fecha);

/// <summary>Una empresa invitó (con ese vínculo) al correo de la solicitud. Una invitación por vínculo: repetirla no agrega otra.</summary>
public sealed record AccesoSolicitadoPorEmpresa(Guid SolicitudId, Guid TenantId, Guid EmpresaId, Guid VinculoId, string Empresa, DateTimeOffset Fecha);

/// <summary>Un contacto del tercero aprobó el correo: desde aquí es contacto del tercero (<paramref name="AprobadoPor"/> es el correo de quien decidió).</summary>
public sealed record AccesoDeCorreoAprobado(Guid SolicitudId, string AprobadoPor, DateTimeOffset Fecha);

/// <summary>Un contacto del tercero rechazó el correo: no vuelve a entrar a este tercero salvo que otro contacto lo apruebe.</summary>
public sealed record AccesoDeCorreoRechazado(Guid SolicitudId, string RechazadoPor, string? Motivo, DateTimeOffset Fecha);

/// <summary>
/// Solicitud de acceso de un correo que **no es contacto conocido** de un tercero que ya existía (docs/02 §6.1, B-1; decisión AA-1…AA-9 del
/// registro). Un stream por (tercero, correo) en la bóveda: guarda qué empresas invitaron a ese correo y qué decidieron los contactos del
/// tercero. Mientras esté <see cref="EstadoSolicitudDeAcceso.Pendiente"/> —o <see cref="EstadoSolicitudDeAcceso.Rechazada"/>— la persona
/// que entra con ese correo tiene <b>acceso aislado</b>: ve y sube solo lo de la relación con la empresa que la invitó y nada de la bóveda
/// global del tercero. Los métodos de decisión validan y devuelven eventos; los <c>Apply</c> solo evolucionan el estado.
/// </summary>
public sealed class SolicitudDeAcceso
{
    public const int LongitudMaximaDelMotivo = 500;

    public Guid Id { get; set; }

    public long Version { get; set; }

    public Guid TerceroId { get; set; }

    /// <summary>Correo normalizado (minúsculas, sin espacios).</summary>
    public string Correo { get; set; } = string.Empty;

    public EstadoSolicitudDeAcceso Estado { get; set; }

    public List<InvitacionDeAcceso> Invitaciones { get; set; } = [];

    /// <summary>Correo del contacto que aprobó o rechazó.</summary>
    public string? DecididoPor { get; set; }

    public DateTimeOffset? DecididoEn { get; set; }

    public string? MotivoDelRechazo { get; set; }

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset ActualizadaEn { get; set; }

    [JsonIgnore]
    public bool EstaPendiente => Estado == EstadoSolicitudDeAcceso.Pendiente;

    [JsonIgnore]
    public bool EstaRechazada => Estado == EstadoSolicitudDeAcceso.Rechazada;

    /// <summary>Correo en su forma única: la misma persona escrita con otras mayúsculas o espacios es la misma solicitud.</summary>
    public static string Normalizar(string? correo) => (correo ?? string.Empty).Trim().ToLowerInvariant();

    /// <summary>Id determinista de la solicitud de un correo en un tercero: pedirla dos veces no crea dos.</summary>
    public static Guid IdPara(Guid terceroId, string correo) =>
        new(SHA256.HashData(Encoding.UTF8.GetBytes($"validasoft:solicitud-de-acceso:{terceroId:N}:{Normalizar(correo)}"))[..16]);

    // ---------- Decisiones ----------

    public static SolicitudDeAccesoCreada Crear(Guid terceroId, string correo, DateTimeOffset ahora)
    {
        var normalizado = Normalizar(correo);
        if (normalizado.Length == 0 || !normalizado.Contains('@'))
            throw ReglaDeNegocioException.Validacion("correo-invalido", "La solicitud de acceso necesita un correo válido.");
        return new SolicitudDeAccesoCreada(IdPara(terceroId, normalizado), terceroId, normalizado, ahora);
    }

    /// <summary>
    /// Una empresa invitó al correo con este vínculo. Un vínculo ya registrado no repite el evento (idempotente: reenviar la invitación no
    /// vuelve a avisar a los contactos).
    /// </summary>
    public AccesoSolicitadoPorEmpresa? SolicitarPor(Guid tenantId, Guid empresaId, Guid vinculoId, string empresa, DateTimeOffset ahora) =>
        Invitaciones.Any(i => i.VinculoId == vinculoId)
            ? null
            : new AccesoSolicitadoPorEmpresa(Id, tenantId, empresaId, vinculoId, string.IsNullOrWhiteSpace(empresa) ? "Una empresa" : empresa.Trim(), ahora);

    /// <summary>
    /// Un contacto del tercero aprueba el correo. Pendiente o rechazada (un contacto puede corregir su decisión) ⇒ evento; ya aprobada ⇒ nulo
    /// (idempotente). Nadie aprueba su propio acceso: <paramref name="aprobadoPor"/> no puede ser el correo de la solicitud.
    /// </summary>
    public AccesoDeCorreoAprobado? Aprobar(string aprobadoPor, DateTimeOffset ahora)
    {
        var quien = Normalizar(aprobadoPor);
        AsegurarOtraPersona(quien);
        return Estado == EstadoSolicitudDeAcceso.Aprobada ? null : new AccesoDeCorreoAprobado(Id, quien, ahora);
    }

    /// <summary>
    /// Un contacto del tercero rechaza el correo, con un motivo opcional. Solo una solicitud pendiente se rechaza; ya rechazada ⇒ nulo
    /// (idempotente); aprobada ⇒ 409 <c>solicitud-ya-aprobada</c> (el correo ya es contacto).
    /// </summary>
    public AccesoDeCorreoRechazado? Rechazar(string rechazadoPor, string? motivo, DateTimeOffset ahora)
    {
        var quien = Normalizar(rechazadoPor);
        AsegurarOtraPersona(quien);
        if (Estado == EstadoSolicitudDeAcceso.Aprobada)
            throw ReglaDeNegocioException.Conflicto("solicitud-ya-aprobada", "Ese correo ya es contacto del tercero: no se puede rechazar.");
        if (Estado == EstadoSolicitudDeAcceso.Rechazada)
            return null;

        var texto = string.IsNullOrWhiteSpace(motivo) ? null : motivo.Trim();
        if (texto is { Length: > LongitudMaximaDelMotivo })
            throw ReglaDeNegocioException.Validacion("motivo-demasiado-largo", $"El motivo admite hasta {LongitudMaximaDelMotivo} caracteres.");
        return new AccesoDeCorreoRechazado(Id, quien, texto, ahora);
    }

    private void AsegurarOtraPersona(string quien)
    {
        if (string.Equals(quien, Correo, StringComparison.Ordinal))
            throw ReglaDeNegocioException.Prohibido("acceso-propio", "Nadie decide sobre su propio acceso: lo decide otro contacto del tercero.");
    }

    // ---------- Evolución ----------

    public static SolicitudDeAcceso Create(SolicitudDeAccesoCreada e) => new()
    {
        Id = e.SolicitudId,
        TerceroId = e.TerceroId,
        Correo = e.Correo,
        Estado = EstadoSolicitudDeAcceso.Pendiente,
        CreadaEn = e.Fecha,
        ActualizadaEn = e.Fecha
    };

    public void Apply(AccesoSolicitadoPorEmpresa e)
    {
        Invitaciones.Add(new InvitacionDeAcceso(e.TenantId, e.EmpresaId, e.VinculoId, e.Empresa, e.Fecha));
        ActualizadaEn = e.Fecha;
    }

    public void Apply(AccesoDeCorreoAprobado e)
    {
        Estado = EstadoSolicitudDeAcceso.Aprobada;
        DecididoPor = e.AprobadoPor;
        DecididoEn = e.Fecha;
        MotivoDelRechazo = null;
        ActualizadaEn = e.Fecha;
    }

    public void Apply(AccesoDeCorreoRechazado e)
    {
        Estado = EstadoSolicitudDeAcceso.Rechazada;
        DecididoPor = e.RechazadoPor;
        DecididoEn = e.Fecha;
        MotivoDelRechazo = e.Motivo;
        ActualizadaEn = e.Fecha;
    }
}
