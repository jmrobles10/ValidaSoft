using System.Security.Claims;
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
/// Sesión del tercero leída del token: quién es, con qué correo entró, si verificó OTP y qué vínculos puede ver.
/// Los vínculos se fijan al abrir la sesión (los de ese tercero cuyo correo coincide con el del enlace); un vínculo que no
/// está aquí responde 404. <see cref="Embebida"/>: la sesión la abrió un sistema integrador para un iframe (un solo vínculo, sin
/// cookie de refresco).
/// </summary>
public sealed record SesionDeTercero(Guid TerceroId, string Correo, bool OtpVerificado, IReadOnlyList<VinculoEnSesion> Vinculos, EmbebidoDeSesion? Embebida = null)
{
    public static SesionDeTercero De(ClaimsPrincipal usuario)
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
            string.Equals(usuario.FindFirstValue(ReclamosTercero.OtpVerificado), "true", StringComparison.OrdinalIgnoreCase), vinculos, embebida);
    }

    public VinculoEnSesion VinculoRequerido(Guid vinculoId) =>
        Vinculos.FirstOrDefault(v => v.VinculoId == vinculoId)
        ?? throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");
}

/// <summary>Emite el JWT de la sesión del tercero (HS256, 30 minutos, audiencia <c>validasoft:tercero</c>).</summary>
public interface IEmisorDeSesionesTercero
{
    /// <summary>Con <paramref name="embebido"/> (IO-5) el token es de la sesión embebida: dura lo que dice y lleva sistema, origen y componente.</summary>
    (string Token, DateTimeOffset Expira) Emitir(Guid terceroId, string correo, bool otpVerificado, IReadOnlyList<VinculoEnSesion> vinculos,
        DateTimeOffset ahora, EmbebidoDeSesion? embebido = null);
}
