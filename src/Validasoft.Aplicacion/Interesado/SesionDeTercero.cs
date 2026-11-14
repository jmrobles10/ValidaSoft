using System.Security.Claims;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>Reclamos del token de sesión del tercero (docs/07 §2.2).</summary>
public static class ReclamosTercero
{
    public const string Tipo = "tipo";
    public const string ValorTipo = "tercero";
    public const string TerceroId = "terceroId";
    public const string Correo = "correo";
    public const string OtpVerificado = "otpVerificado";

    /// <summary>Un reclamo por vínculo: <c>vinculoId|tenantId|empresaId</c>.</summary>
    public const string Vinculos = "vinculos";

    /// <summary>Solo en las sesiones embebidas (IO-5): <c>true</c>, el código del sistema que la abrió, el origen de la página que embebe y el componente.</summary>
    public const string Embebido = "embebido";

    public const string Sistema = "sistema";

    public const string Origen = "origen";

    public const string Componente = "componente";

    /// <summary>Generación del sistema con la que se abrió la sesión embebida (PI-7): si el sistema se desactivó desde entonces, la sesión ya no vale.</summary>
    public const string GeneracionDelSistema = "generacion";

    /// <summary>
    /// <c>true</c> cuando el correo de la sesión no es contacto conocido de un tercero que ya existía: acceso aislado (solo lo de la relación con la empresa
    /// que lo invitó, nada de la bóveda global del tercero). Un token sin este reclamo es de un contacto: acceso completo.
    /// </summary>
    public const string Aislado = "aislado";

    /// <summary>Id de la sesión de refresco (la familia de la cookie) de la que salió el token: cerrarla desde «Mis sesiones» corta el token en segundos.</summary>
    public const string SesionId = "sesion";

    /// <summary>Nombre de la política de autorización de las rutas <c>/public/v1</c> que exigen sesión de tercero.</summary>
    public const string Politica = "tercero";
}

/// <summary>Vínculo que la sesión autoriza a ver y aceptar (de cualquier tenant).</summary>
public sealed record VinculoEnSesion(Guid VinculoId, Guid TenantId, Guid EmpresaId)
{
    public string Reclamo() => $"{VinculoId:D}|{TenantId:D}|{EmpresaId:D}";
}

/// <summary>
/// De dónde viene una sesión embebida (IO-5): el sistema integrador que la pidió con su API key, el origen exacto de la página que
/// aloja el iframe (uno de los permitidos de ese sistema) y qué componente se monta (<c>boveda</c>, <c>expediente</c> o <c>ficha</c>).
/// </summary>
public sealed record EmbebidoDeSesion(string Sistema, string Origen, string Componente, TimeSpan Duracion, int Generacion = 0);

/// <summary>
/// Qué parte de lo del tercero toca un endpoint de <c>/public/v1</c>. Cada endpoint declara el suyo y la sesión decide si su alcance lo cubre
/// (<see cref="SesionDeTercero.De(ClaimsPrincipal, GrupoDeAcceso)"/>): sin grupo no se lee la sesión, así una ruta nueva no queda abierta a una
/// sesión aislada ni a un token embebido por olvido.
/// </summary>
public enum GrupoDeAcceso
{
    /// <summary>Lo de la relación con una empresa: sus invitaciones, su expediente, sus contactos con ella, las novedades de sus vínculos.</summary>
    Relacion,

    /// <summary>Subir un documento o un formulario (en cuarentena, ligado solo al vínculo, si la sesión es aislada).</summary>
    Subida,

    /// <summary>Descargar una versión: la propia (aislado) o una compartida con el vínculo del token (embebido de expediente).</summary>
    Descarga,

    /// <summary>La bóveda global del tercero: sus documentos, sus versiones, su retiro.</summary>
    Boveda,

    /// <summary>Sus cuentas bancarias.</summary>
    Cuentas,

    /// <summary>Elegir qué cuenta cobra una empresa (<c>cuenta-designada</c> del vínculo).</summary>
    CuentaDesignada,

    /// <summary>Su ficha maestra: sus secciones, propuestas y la declaración PEP.</summary>
    Ficha,

    /// <summary>Sus derechos de datos personales: exportar la bóveda y las solicitudes de habeas data.</summary>
    Privacidad,

    /// <summary>Lo que es del tercero entero: sus preferencias de notificación, sus sesiones y las solicitudes de acceso de otros correos.</summary>
    Cuenta
}

/// <summary>
/// Sesión del tercero leída del token: quién es, con qué correo entró, si verificó OTP y qué vínculos puede ver.
/// Los vínculos se fijan al abrir la sesión (los de ese tercero cuyo correo coincide con el del enlace); un vínculo que no
/// está aquí responde 404. <see cref="Embebida"/>: la sesión la abrió un sistema integrador para un iframe (un solo vínculo, sin
/// cookie de refresco). <see cref="Aislada"/>: el correo no es contacto conocido de un tercero que ya existía, así que ve y sube solo lo de
/// la relación con su empresa (<see cref="GrupoDeAcceso"/>). <see cref="SesionId"/>: la sesión de refresco de la que salió, para poder cerrarla
/// individualmente (nula en las embebidas).
/// </summary>
public sealed record SesionDeTercero(Guid TerceroId, string Correo, bool OtpVerificado, IReadOnlyList<VinculoEnSesion> Vinculos, EmbebidoDeSesion? Embebida = null,
    bool Aislada = false, Guid? SesionId = null)
{
    /// <summary>
    /// La sesión del token si su alcance cubre <paramref name="grupo"/>; si no, 403 <c>acceso-aislado</c> (el correo aún no lo aprobó un contacto
    /// del tercero) o <c>alcance-de-sesion</c> (el token embebido es de otro componente). Es la entrada de todos los endpoints de <c>/public/v1</c>.
    /// </summary>
    public static SesionDeTercero De(ClaimsPrincipal usuario, GrupoDeAcceso grupo)
    {
        var sesion = SinAlcance(usuario);
        if (sesion.Aislada && !PermiteAislada(grupo))
            throw ReglaDeNegocioException.Prohibido("acceso-aislado",
                "Tu acceso está pendiente de que un contacto del tercero lo apruebe: mientras tanto solo ves y subes lo de la relación con la empresa que te invitó.");
        if (sesion.Embebida is { } embebida && !PermiteEmbebida(embebida.Componente, grupo))
            throw ReglaDeNegocioException.Prohibido("alcance-de-sesion",
                $"Esta sesión se abrió para el componente «{embebida.Componente}» y no alcanza lo que pides.");
        return sesion;
    }

    /// <summary>Quién es la sesión y qué vínculos tiene, sin comprobar su alcance (la autenticación y el stream de novedades ya acotan lo que leen).</summary>
    public static SesionDeTercero SinAlcance(ClaimsPrincipal usuario)
    {
        if (usuario.FindFirstValue(ReclamosTercero.Tipo) != ReclamosTercero.ValorTipo ||
            !Guid.TryParse(usuario.FindFirstValue(ReclamosTercero.TerceroId), out var terceroId))
            throw ReglaDeNegocioException.NoAutenticado("sesion-tercero-requerida", "Esta ruta requiere la sesión de un tercero.");

        var vinculos = usuario.FindAll(ReclamosTercero.Vinculos)
            .Select(c => c.Value.Split('|'))
            .Where(p => p.Length == 3 && Guid.TryParse(p[0], out _) && Guid.TryParse(p[1], out _) && Guid.TryParse(p[2], out _))
            .Select(p => new VinculoEnSesion(Guid.Parse(p[0]), Guid.Parse(p[1]), Guid.Parse(p[2])))
            .ToList();

        var embebida = string.Equals(usuario.FindFirstValue(ReclamosTercero.Embebido), "true", StringComparison.OrdinalIgnoreCase)
            ? new EmbebidoDeSesion(usuario.FindFirstValue(ReclamosTercero.Sistema) ?? string.Empty, usuario.FindFirstValue(ReclamosTercero.Origen) ?? string.Empty,
                usuario.FindFirstValue(ReclamosTercero.Componente) ?? string.Empty, TimeSpan.Zero,
                int.TryParse(usuario.FindFirstValue(ReclamosTercero.GeneracionDelSistema), out var generacion) ? generacion : 0)
            : null;

        return new SesionDeTercero(terceroId, usuario.FindFirstValue(ReclamosTercero.Correo) ?? string.Empty,
            string.Equals(usuario.FindFirstValue(ReclamosTercero.OtpVerificado), "true", StringComparison.OrdinalIgnoreCase), vinculos, embebida,
            string.Equals(usuario.FindFirstValue(ReclamosTercero.Aislado), "true", StringComparison.OrdinalIgnoreCase),
            Guid.TryParse(usuario.FindFirstValue(ReclamosTercero.SesionId), out var sesionId) ? sesionId : null);
    }

    /// <summary>Una sesión aislada ve y sube lo de su relación: invitaciones, expediente, subidas (en cuarentena) y la descarga de lo suyo. Nada del tercero entero.</summary>
    public static bool PermiteAislada(GrupoDeAcceso grupo) => grupo is GrupoDeAcceso.Relacion or GrupoDeAcceso.Subida or GrupoDeAcceso.Descarga;

    /// <summary>
    /// Qué grupos alcanza cada componente embebido (IO-5): la bóveda, sus documentos y sus cuentas; el expediente, lo de su vínculo (subir, descargar lo
    /// compartido con él, elegir su cuenta, las secciones de ficha que pide y las cuentas que necesita); la ficha, sus secciones y sus soportes. Ninguno
    /// toca los derechos de datos personales, las preferencias ni las sesiones: eso se hace en la app del tercero.
    /// </summary>
    public static bool PermiteEmbebida(string componente, GrupoDeAcceso grupo) => componente switch
    {
        ComponentesEmbebibles.Boveda => grupo is GrupoDeAcceso.Relacion or GrupoDeAcceso.Subida or GrupoDeAcceso.Descarga or GrupoDeAcceso.Boveda
            or GrupoDeAcceso.Cuentas or GrupoDeAcceso.CuentaDesignada,
        ComponentesEmbebibles.Expediente => grupo is GrupoDeAcceso.Relacion or GrupoDeAcceso.Subida or GrupoDeAcceso.Descarga or GrupoDeAcceso.Cuentas
            or GrupoDeAcceso.CuentaDesignada or GrupoDeAcceso.Ficha,
        ComponentesEmbebibles.Ficha => grupo is GrupoDeAcceso.Relacion or GrupoDeAcceso.Subida or GrupoDeAcceso.Descarga or GrupoDeAcceso.Ficha,
        _ => false
    };

    /// <summary>
    /// Un token embebido de un expediente solo trabaja con el vínculo para el que se emitió: 404 <c>vinculo-no-encontrado</c> con cualquier otro. Las demás
    /// sesiones (y los embebidos de bóveda o ficha, que no son de un vínculo) no se limitan por aquí.
    /// </summary>
    public void ExigirVinculoDelToken(Guid? vinculoId)
    {
        if (Embebida is { Componente: ComponentesEmbebibles.Expediente } && (vinculoId is null || Vinculos.All(v => v.VinculoId != vinculoId)))
            throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");
    }

    /// <summary>¿Es el token de un expediente embebido? Su alcance se acota al vínculo con el que se emitió.</summary>
    public bool EsDeUnExpediente => Embebida is { Componente: ComponentesEmbebibles.Expediente };

    public VinculoEnSesion VinculoRequerido(Guid vinculoId) =>
        Vinculos.FirstOrDefault(v => v.VinculoId == vinculoId)
        ?? throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");
}

/// <summary>Emite el JWT de la sesión del tercero (HS256, 30 minutos, audiencia <c>validasoft:tercero</c>).</summary>
public interface IEmisorDeSesionesTercero
{
    /// <summary>
    /// Con <paramref name="embebido"/> (IO-5) el token es de la sesión embebida: dura lo que dice y lleva sistema, origen y componente. Con
    /// <paramref name="aislada"/> lleva el reclamo de acceso aislado; con <paramref name="sesionId"/>, la sesión de refresco de la que sale.
    /// </summary>
    (string Token, DateTimeOffset Expira) Emitir(Guid terceroId, string correo, bool otpVerificado, IReadOnlyList<VinculoEnSesion> vinculos,
        DateTimeOffset ahora, EmbebidoDeSesion? embebido = null, bool aislada = false, Guid? sesionId = null);
}
