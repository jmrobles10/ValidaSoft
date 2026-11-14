namespace Validasoft.Dominio.Terceros;

/// <summary>Qué puede hacer con la bóveda del tercero la persona que entra con un correo.</summary>
public enum NivelDeAcceso
{
    /// <summary>El correo es de un contacto conocido del tercero: ve y gestiona su bóveda y su ficha.</summary>
    Completo,

    /// <summary>
    /// El correo no es de un contacto conocido de un tercero que ya existía: ve y sube solo lo de la relación con la empresa que lo
    /// invitó y no toca la bóveda global del tercero hasta que un contacto lo apruebe (acceso aislado).
    /// </summary>
    Aislado
}

/// <summary>
/// Quién entra con acceso completo a un tercero y quién con acceso aislado. Antes bastaba conocer el NIT: una empresa que creaba un
/// vínculo con el NIT de un tercero ajeno ponía su propio correo, abría el enlace y entraba a su bóveda (cosecha por NIT). Ahora el
/// correo debe ser de un contacto conocido; si no lo es, solo entra aislado hasta que un contacto lo apruebe. Son funciones puras: la
/// aplicación les pasa lo que leyó de la base.
/// </summary>
public static class AccesoDelTercero
{
    /// <summary>
    /// ¿El correo es un contacto conocido del tercero? Lo es si está en sus contactos o si es el de un vínculo que ya aceptó su invitación
    /// sin pasar por el acceso aislado (esos terceros entraron cuando el correo bastaba: se les reconoce, no se les bloquea).
    /// </summary>
    public static bool EsContactoConocido(Tercero tercero, string correo, IEnumerable<string> correosDeVinculosAceptados)
    {
        var normalizado = SolicitudDeAcceso.Normalizar(correo);
        return normalizado.Length > 0 &&
               (tercero.TieneContacto(normalizado) ||
                correosDeVinculosAceptados.Any(c => string.Equals(SolicitudDeAcceso.Normalizar(c), normalizado, StringComparison.Ordinal)));
    }

    /// <summary>Todos los correos que hoy tienen acceso completo al tercero (los que reciben el aviso de una solicitud de acceso).</summary>
    public static IReadOnlyList<string> CorreosConocidos(Tercero tercero, IEnumerable<string> correosDeVinculosAceptados) =>
    [
        .. tercero.Contactos.Select(c => SolicitudDeAcceso.Normalizar(c.Correo))
            .Concat(correosDeVinculosAceptados.Select(SolicitudDeAcceso.Normalizar))
            .Where(c => c.Length > 0)
            .Distinct(StringComparer.Ordinal)
    ];

    /// <summary>
    /// ¿El correo que invita la empresa pasa como el primer contacto del tercero? Sí si el tercero es nuevo (lo crea esta misma empresa) o es
    /// <b>de pruebas</b> (un sandbox no comparte nada con otro grupo), o si es de esta empresa y sigue sin historia: ningún vínculo de otro grupo,
    /// ningún documento en la bóveda y ningún contacto probado (nadie ha aceptado todavía una invitación suya: un correo con el que se equivocó
    /// la empresa se corrige invitando a otro). Entonces el correo se vuelve contacto, como siempre. En cualquier otro caso (un tercero que ya
    /// existía con contactos probados o documentos) el correo desconocido entra aislado.
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

    /// <summary>Nivel de acceso con lo ya leído: completo si es contacto conocido; si no, aislado.</summary>
    public static NivelDeAcceso Nivel(Tercero tercero, string correo, IEnumerable<string> correosDeVinculosAceptados) =>
        EsContactoConocido(tercero, correo, correosDeVinculosAceptados) ? NivelDeAcceso.Completo : NivelDeAcceso.Aislado;
}
