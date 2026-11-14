namespace Validasoft.Dominio.Terceros;

/// <summary>Qué puede hacer con la bóveda del tercero la persona que entra con un correo.</summary>
public enum NivelDeAcceso
{
    /// <summary>El correo es de un contacto del tercero (y no lo desplazó uno verificado): ve y gestiona su bóveda y su ficha.</summary>
    Completo,

    /// <summary>
    /// El correo no es de un contacto del tercero, o es de uno sin verificar que desplazó otro verificado: ve y sube solo lo de la relación con la empresa que lo
    /// invitó y no toca la bóveda global del tercero hasta que un contacto verificado (o la plataforma) lo apruebe (acceso aislado).
    /// </summary>
    Aislado
}

/// <summary>
/// Quién entra con acceso completo a un tercero y quién con acceso aislado, y quién decide. Antes bastaba conocer el NIT: una empresa que creaba un
/// vínculo con el NIT de un tercero ajeno ponía su propio correo, abría el enlace y entraba a su bóveda (cosecha por NIT). Ahora el correo debe ser de un contacto;
/// si no lo es, solo entra aislado hasta que lo apruebe un contacto <b>verificado</b>. Y quien registra primero un NIT ajeno deja su correo como contacto sin
/// verificar: entra completo mientras nadie más esté involucrado, pero no decide y, si otro contacto se verifica, el verificado prevalece. Son funciones puras: la
/// aplicación les pasa lo que leyó de la base.
/// </summary>
public static class AccesoDelTercero
{
    /// <summary>¿El correo es un contacto del tercero? (sin importar si está verificado). Los correos de los vínculos que aceptaron antes de esta regla ya no cuentan solos.</summary>
    public static bool EsContactoConocido(Tercero tercero, string correo) => tercero.TieneContacto(SolicitudDeAcceso.Normalizar(correo));

    /// <summary>
    /// ¿Puede ese correo aprobar o rechazar los accesos aislados del tercero? Solo un contacto verificado. En un tercero de pruebas (sandbox), que no comparte nada con otro
    /// grupo, cualquier contacto.
    /// </summary>
    public static bool PuedeDecidir(Tercero tercero, string correo) =>
        tercero.BuscarContacto(correo) is { } contacto && (contacto.EstaVerificado || tercero.SandboxTenantId is not null);

    /// <summary>Los correos que hoy deciden los accesos aislados del tercero (los verificados; en un sandbox, todos): los que reciben el aviso de una solicitud.</summary>
    public static IReadOnlyList<string> CorreosQueDecidenLosAccesos(Tercero tercero) =>
    [
        .. tercero.Contactos.Where(c => c.EstaVerificado || tercero.SandboxTenantId is not null)
            .Select(c => SolicitudDeAcceso.Normalizar(c.Correo))
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>
    /// ¿Decide la plataforma los accesos aislados del tercero? Sí cuando no tiene ningún contacto verificado (y no es un sandbox): las solicitudes van a la cola de la
    /// consola de Plataforma.
    /// </summary>
    public static bool DecideLaPlataforma(Tercero tercero) => tercero.SandboxTenantId is null && !tercero.TieneContactoVerificado;

    /// <summary>
    /// Un contacto sin verificar que desplazó otro verificado: el tercero ya tiene un contacto verificado y este no lo está. Pierde la bóveda global y espera la
    /// decisión de un contacto verificado.
    /// </summary>
    public static bool EstaDesplazado(Tercero tercero, string correo) =>
        tercero.SandboxTenantId is null && tercero.TieneContactoVerificado && tercero.BuscarContacto(correo) is { EstaVerificado: false };

    /// <summary>Los contactos sin verificar mientras el tercero ya tiene uno verificado: los que deben pasar a acceso aislado.</summary>
    public static IReadOnlyList<Contacto> Desplazados(Tercero tercero) =>
        tercero.SandboxTenantId is null && tercero.TieneContactoVerificado ? [.. tercero.Contactos.Where(c => !c.EstaVerificado)] : [];

    /// <summary>
    /// ¿El correo que invita la empresa pasa como el primer contacto del tercero? Sí si el tercero es nuevo (lo crea esta misma empresa) o es
    /// <b>de pruebas</b> (un sandbox no comparte nada con otro grupo), o si es de esta empresa y sigue sin historia: ningún vínculo de otro grupo,
    /// ningún documento en la bóveda y ningún contacto verificado ni aceptación previa de una invitación (nadie ha probado todavía ser del tercero: un correo con el que se
    /// equivocó la empresa se corrige invitando a otro). Entonces el correo se vuelve contacto <b>sin verificar</b>. En cualquier otro caso (un tercero que ya existía con contactos
    /// verificados, aceptaciones o documentos) el correo desconocido entra aislado.
    /// </summary>
    public static bool EsPrimerContacto(Tercero tercero, Guid tenantId, bool terceroNuevo, bool hayVinculosDeOtroGrupo, bool hayDocumentos,
        bool hayContactoProbado)
    {
        if (terceroNuevo || tercero.SandboxTenantId is not null)
            return true;

        return tercero.CreadoPorTenant == tenantId && !hayVinculosDeOtroGrupo && !hayDocumentos && !hayContactoProbado;
    }

    /// <summary>
    /// El nombre del tercero con las letras tapadas salvo la primera de cada palabra (<c>Proveedores del Norte</c> se vuelve <c>P•••••••• d•• N•••••</c>): lo único
    /// que se le dice a quien entra aislado, que aún no es un contacto del tercero.
    /// </summary>
    public static string EnmascararNombre(string? nombre) =>
        string.Join(' ', (nombre ?? string.Empty).Split(' ', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(palabra => palabra.Length <= 1 ? palabra : palabra[0] + new string('•', Math.Min(palabra.Length - 1, 9))));

    /// <summary>
    /// Nivel de acceso con lo ya leído: completo si es contacto del tercero y no lo desplazó uno verificado; si no, aislado. Un contacto sin verificar entra completo mientras
    /// el tercero no tenga ningún contacto verificado (el flujo normal del primer contacto).
    /// </summary>
    public static NivelDeAcceso Nivel(Tercero tercero, string correo) =>
        EsContactoConocido(tercero, correo) && !EstaDesplazado(tercero, correo) ? NivelDeAcceso.Completo : NivelDeAcceso.Aislado;
}
