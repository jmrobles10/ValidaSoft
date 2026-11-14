namespace Validasoft.Contratos;

// ---------- Acceso aislado y sesiones del tercero ----------

/// <summary>Una empresa que invitó al correo de una solicitud de acceso, y cuándo.</summary>
public sealed record EmpresaQueInvitoDto(string Empresa, DateTimeOffset Fecha);

/// <summary>
/// Un correo que no es contacto conocido del tercero y fue invitado por una o más empresas (acceso aislado). Los contactos del tercero lo ven en su cuenta
/// (<c>GET /public/v1/accesos-solicitados</c>) y lo aprueban o rechazan. <see cref="Estado"/>: <c>Pendiente</c> (la persona entra aislada: solo ve y sube lo de
/// su relación con la empresa), <c>Aprobada</c> (ya es contacto del tercero, con acceso completo) o <c>Rechazada</c> (acceso bloqueado).
/// <see cref="DecididoPor"/> es el correo del contacto que decidió.
/// </summary>
public sealed record AccesoSolicitadoDto(
    Guid Id, string Correo, string Estado, DateTimeOffset Desde, IReadOnlyList<EmpresaQueInvitoDto> Empresas, string? DecididoPor = null,
    DateTimeOffset? DecididoEn = null, string? Motivo = null);

/// <summary><see cref="Motivo"/> es opcional y solo lo ven los contactos del tercero (no la empresa ni la persona rechazada).</summary>
public sealed record RechazarAccesoDto(string? Motivo = null);

/// <summary>
/// El acceso del correo invitado, como lo ve la empresa en su vínculo: <see cref="Estado"/> <c>Pendiente</c> (los contactos del tercero aún no deciden;
/// nada de la bóveda global del tercero se le comparte), <c>Aprobado</c> o <c>Rechazado</c> (la empresa puede invitar a otro correo). Nulo en un vínculo
/// cuyo correo es de confianza.
/// </summary>
public sealed record AccesoDelCorreoDto(string Estado, string Correo, DateTimeOffset Desde, DateTimeOffset? ResueltoEn);

/// <summary>Una sesión abierta del tercero (una por navegador): quién entró, desde dónde y cuándo la usó por última vez. <see cref="EsActual"/> es la de la petición.</summary>
public sealed record SesionActivaDto(
    Guid Id, string Correo, DateTimeOffset CreadaEn, DateTimeOffset ExpiraEn, DateTimeOffset? UltimoUsoEn, string? Dispositivo, string? Ip, bool EsActual);
