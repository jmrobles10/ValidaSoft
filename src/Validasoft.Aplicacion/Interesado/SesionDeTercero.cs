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

    /// <summary>Nombre de la política de autorización de las rutas <c>/public/v1</c> que exigen sesión de tercero.</summary>
    public const string Politica = "tercero";
}

/// <summary>Vínculo que la sesión autoriza a ver y aceptar (de cualquier tenant).</summary>
public sealed record VinculoEnSesion(Guid VinculoId, Guid TenantId, Guid EmpresaId)
{
    public string Reclamo() => $"{VinculoId:D}|{TenantId:D}|{EmpresaId:D}";
}

/// <summary>
/// Sesión del tercero leída del token: quién es, con qué correo entró, si verificó OTP y qué vínculos puede ver.
/// Los vínculos se fijan al abrir la sesión (los de ese tercero cuyo correo coincide con el del enlace); un vínculo que no
/// está aquí responde 404.
/// </summary>
public sealed record SesionDeTercero(Guid TerceroId, string Correo, bool OtpVerificado, IReadOnlyList<VinculoEnSesion> Vinculos)
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

        return new SesionDeTercero(terceroId, usuario.FindFirstValue(ReclamosTercero.Correo) ?? string.Empty,
            string.Equals(usuario.FindFirstValue(ReclamosTercero.OtpVerificado), "true", StringComparison.OrdinalIgnoreCase), vinculos);
    }

    public VinculoEnSesion VinculoRequerido(Guid vinculoId) =>
        Vinculos.FirstOrDefault(v => v.VinculoId == vinculoId)
        ?? throw ReglaDeNegocioException.NoEncontrado("vinculo-no-encontrado", "No existe ese vínculo.");
}

/// <summary>Emite el JWT de la sesión del tercero (HS256, 30 minutos, audiencia <c>validasoft:tercero</c>).</summary>
public interface IEmisorDeSesionesTercero
{
    (string Token, DateTimeOffset Expira) Emitir(Guid terceroId, string correo, bool otpVerificado, IReadOnlyList<VinculoEnSesion> vinculos,
        DateTimeOffset ahora);
}
