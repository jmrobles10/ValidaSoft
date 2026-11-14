namespace Validasoft.Contratos;

// ---------- Acceso aislado y sesiones del tercero ----------

/// <summary>Una empresa que invitó al correo de una solicitud de acceso, y cuándo.</summary>
public sealed record EmpresaQueInvitoDto(string Empresa, DateTimeOffset Fecha);

/// <summary>
/// Un correo que no es contacto del tercero (o es un contacto sin verificar que desplazó otro verificado) y fue invitado por una o más empresas (acceso aislado). Los contactos
/// del tercero lo ven en su cuenta (<c>GET /public/v1/accesos-solicitados</c>); lo aprueba o rechaza un contacto <b>verificado</b> o, si el tercero no tiene ninguno, la
/// plataforma. <see cref="Estado"/>: <c>Pendiente</c> (la persona entra aislada: solo ve y sube lo de su relación con la empresa), <c>Aprobada</c> (ya es contacto verificado
/// del tercero, con acceso completo) o <c>Rechazada</c> (acceso bloqueado). <see cref="DecididoPor"/> es el correo del contacto, <c>plataforma:{operador}</c> o
/// <c>verificacion</c>; <see cref="DecididoVia"/> es <c>Contacto</c>, <c>Plataforma</c> o <c>Verificacion</c>. <see cref="PuedesDecidir"/>: el correo de la sesión es un
/// contacto verificado y no el de la solicitud. <see cref="DecideLaPlataforma"/>: el tercero no tiene contactos verificados, así que la solicitud espera en la cola de
/// la consola de Plataforma. <see cref="ContactoDesplazado"/>: nació porque un contacto sin verificar lo desplazó otro verificado.
/// </summary>
public sealed record AccesoSolicitadoDto(
    Guid Id, string Correo, string Estado, DateTimeOffset Desde, IReadOnlyList<EmpresaQueInvitoDto> Empresas, string? DecididoPor = null,
    DateTimeOffset? DecididoEn = null, string? Motivo = null, bool PuedesDecidir = true, bool DecideLaPlataforma = false, string? DecididoVia = null,
    bool ContactoDesplazado = false);

/// <summary>
/// Un contacto del tercero con su verificación. <see cref="Verificacion"/>: <c>SinVerificar</c> o <c>Verificado</c>; <see cref="Medio"/>: <c>CorreoDelRut</c>,
/// <c>CorreoDeLaCamara</c>, <c>Corroborado</c>, <c>Plataforma</c>, <c>Importacion</c> o <c>AprobadoPorContacto</c>. <see cref="Desplazado"/>: sin verificar mientras otro contacto
/// está verificado, así que entra con acceso aislado. <see cref="EsTuCorreo"/>: es el correo de la sesión.
/// </summary>
public sealed record ContactoDelTerceroDto(
    string Nombre, string Correo, string Rol, string Verificacion, string? Medio, DateTimeOffset? VerificadoEn, bool EsTuCorreo, bool Desplazado);

/// <summary>Una línea del historial de los contactos del tercero: <see cref="Tipo"/> <c>registrado</c>, <c>verificado</c> o <c>paso_a_aislado</c>.</summary>
public sealed record EntradaDeHistorialDeContactoDto(DateTimeOffset Fecha, string Tipo, string Correo, string? Detalle);

/// <summary>
/// Los contactos del tercero (<c>GET /public/v1/contactos-del-tercero</c>) con su historial. <see cref="PuedesDecidir"/>: el correo de la sesión es un contacto verificado y
/// aprueba o rechaza los accesos de otros correos. <see cref="DecideLaPlataforma"/>: el tercero no tiene contactos verificados y la plataforma decide esos accesos.
/// </summary>
public sealed record ContactosDelTerceroDto(
    IReadOnlyList<ContactoDelTerceroDto> Contactos, IReadOnlyList<EntradaDeHistorialDeContactoDto> Historial, bool PuedesDecidir, bool DecideLaPlataforma);

// ---------- Consola de Plataforma: solicitudes de acceso a terceros ----------

/// <summary>Una empresa que invitó al correo de una solicitud, con su grupo (tenant), para la evidencia de la consola.</summary>
public sealed record EmpresaQueInvitoPlataformaDto(string Empresa, string Grupo, Guid TenantId, DateTimeOffset Fecha);

/// <summary>Un correo electrónico oficial del tercero (el del RUT o el de la Cámara de comercio), con el documento y la versión de donde se leyó.</summary>
public sealed record CorreoOficialDto(string Correo, string Documento, int Version);

/// <summary>
/// Una solicitud de acceso a un tercero como la ve la consola de Plataforma (<c>GET /api/v1/plataforma/solicitudes-de-acceso</c>), con la evidencia para decidir: las empresas que
/// invitaron al correo, los contactos del tercero con su verificación y los correos del RUT y de la Cámara si el tercero los tiene. <see cref="EnCola"/>: pendiente y sin contactos
/// verificados, así que la decide un operador.
/// </summary>
public sealed record SolicitudDeAccesoDePlataformaDto(
    Guid Id, Guid TerceroId, string Tercero, string Identificacion, string Correo, string Estado, DateTimeOffset Desde,
    IReadOnlyList<EmpresaQueInvitoPlataformaDto> Empresas, IReadOnlyList<ContactoDelTerceroDto> Contactos, IReadOnlyList<CorreoOficialDto> CorreosOficiales, bool EnCola,
    bool ContactoDesplazado, string? DecididoPor, string? DecididoVia, DateTimeOffset? DecididoEn, string? Motivo);

/// <summary>
/// Lo que hizo la migración de los vínculos aceptados antes del acceso aislado (<c>POST /api/v1/plataforma/acceso-aislado/migrar</c>): terceros revisados, correos que quedaron
/// como contactos verificados o como primeros contactos, vínculos que pasaron a acceso aislado pendiente y terceros puestos al día. Repetirla no cambia nada.
/// </summary>
public sealed record ResultadoDeMigracionDeAccesoDto(int TercerosRevisados, int ContactosVerificados, int PrimerosContactos, int VinculosAislados, int TercerosSincronizados);

/// <summary>Un operador verifica el correo de un contacto del tercero. <see cref="Nota"/> es opcional y queda en la auditoría.</summary>
public sealed record VerificarContactoDto(string Correo, string? Nota = null);

/// <summary><see cref="Motivo"/> es opcional y solo lo ven los contactos del tercero (no la empresa ni la persona rechazada).</summary>
public sealed record RechazarAccesoDto(string? Motivo = null);

/// <summary>
/// El acceso del correo invitado, como lo ve la empresa en su vínculo: <see cref="Estado"/> <c>Pendiente</c> (los contactos del tercero aún no deciden;
/// nada de la bóveda global del tercero se le comparte), <c>Aprobado</c> o <c>Rechazado</c> (la empresa puede invitar a otro correo). Nulo en un vínculo
/// cuyo correo es de confianza.
/// </summary>
public sealed record AccesoDelCorreoDto(string Estado, string Correo, DateTimeOffset Desde, DateTimeOffset? ResueltoEn, string? Motivo = null);

/// <summary>
/// Si el correo que invitó la empresa en un vínculo está probado como del tercero, como lo ve la empresa en el detalle del vínculo. <see cref="Estado"/>:
/// <c>Verificado</c> (con su <see cref="Medio"/>: <c>CorreoDelRut</c>, <c>CorreoDeLaCamara</c>, <c>Corroborado</c>, <c>Plataforma</c>, <c>Importacion</c> o
/// <c>AprobadoPorContacto</c>), <c>SinVerificar</c> (es contacto del tercero, pero nada prueba que sea suyo) o <c>NoEsContacto</c> (aún no es contacto: acceso aislado).
/// </summary>
public sealed record VerificacionDelCorreoDto(string Estado, string? Medio);

/// <summary>Una sesión abierta del tercero (una por navegador): quién entró, desde dónde y cuándo la usó por última vez. <see cref="EsActual"/> es la de la petición.</summary>
public sealed record SesionActivaDto(
    Guid Id, string Correo, DateTimeOffset CreadaEn, DateTimeOffset ExpiraEn, DateTimeOffset? UltimoUsoEn, string? Dispositivo, string? Ip, bool EsActual);
