using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Configuracion;

public enum EstadoDeDominioDeCorreo
{
    /// <summary>Se registró el dominio y falta que la empresa cree sus registros DNS y pida verificarlos.</summary>
    Pendiente,

    Verificado,

    /// <summary>La última verificación no encontró todos los registros; el correo sigue saliendo con el remitente de la plataforma.</summary>
    Fallido
}

/// <summary>Para qué sirve un registro DNS del dominio de correo: SPF (quién puede enviar), DKIM (firma) o DMARC (política).</summary>
public enum FinalidadDeRegistroDns
{
    Spf,
    Dkim,
    Dmarc
}

/// <summary>Un registro que la empresa debe crear en su DNS: tipo (<c>TXT</c> o <c>CNAME</c>), nombre completo y valor.</summary>
public sealed record RegistroDns(FinalidadDeRegistroDns Finalidad, string Tipo, string Nombre, string Valor);

/// <summary>Resultado de comprobar un registro en el DNS: si coincide y qué valor se encontró (nulo si no hay registro).</summary>
public sealed record ComprobacionDeRegistroDns(FinalidadDeRegistroDns Finalidad, bool Correcto, string? Encontrado);

/// <summary>Remitente con el que sale un correo: nombre para mostrar y dirección.</summary>
public sealed record RemitenteDeCorreo(string Nombre, string Direccion);

/// <summary>
/// Dominio de correo propio de una empresa (OD-18, docs/11 §H8): sus correos a terceros salen desde una dirección de su dominio
/// (<c>no-responder@proveedores.empresa.com</c>) cuando los registros DNS (SPF, DKIM y DMARC) están verificados. Mientras no lo estén
/// —o si se quita— salen con el remitente de la plataforma. Esquema <c>validacion</c>, por tenant (id = empresa).
/// </summary>
public sealed partial class DominioDeCorreoEmpresa
{
    public const int LongitudMaximaDominio = 253;
    public const string ParteLocalPorDefecto = "no-responder";
    public const int LongitudMaximaNombre = 80;

    [GeneratedRegex("^(?:[a-z0-9](?:[a-z0-9-]{0,61}[a-z0-9])?\\.)+[a-z]{2,24}$")]
    private static partial Regex RegexDominio();

    [GeneratedRegex("^[a-z0-9](?:[a-z0-9._+-]{0,62}[a-z0-9])?$")]
    private static partial Regex RegexParteLocal();

    /// <summary>La empresa dueña del dominio.</summary>
    public Guid Id { get; set; }

    public string Dominio { get; set; } = string.Empty;

    /// <summary>Lo que va antes de la arroba (por omisión <c>no-responder</c>).</summary>
    public string ParteLocal { get; set; } = ParteLocalPorDefecto;

    /// <summary>Nombre que ve quien recibe el correo; nulo = el de la empresa.</summary>
    public string? NombreRemitente { get; set; }

    public EstadoDeDominioDeCorreo Estado { get; set; }

    /// <summary>Los registros que la empresa debe crear, tal como los dio el proveedor de correo al registrar el dominio.</summary>
    public List<RegistroDns> Registros { get; set; } = [];

    /// <summary>Lo que encontró la última verificación, registro por registro.</summary>
    public List<ComprobacionDeRegistroDns> UltimaVerificacion { get; set; } = [];

    public int Verificaciones { get; set; }

    public DateTimeOffset? VerificadoEn { get; set; }

    public DateTimeOffset? UltimaVerificacionEn { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    /// <summary>La dirección desde la que sale el correo (<c>parte@dominio</c>).</summary>
    public string Direccion => $"{ParteLocal}@{Dominio}";

    /// <summary>El remitente propio si el dominio está verificado; nulo si el correo debe salir con el de la plataforma.</summary>
    public RemitenteDeCorreo? RemitenteSiVerificado(string nombreDeLaEmpresa) =>
        Estado == EstadoDeDominioDeCorreo.Verificado ? new RemitenteDeCorreo(NombreRemitente ?? nombreDeLaEmpresa, Direccion) : null;

    /// <summary>
    /// El dominio en minúsculas, sin espacios y sin <c>@</c> inicial; 422 <c>dominio-invalido</c> si no es un nombre de dominio
    /// público (con punto, sin direcciones IP ni <c>localhost</c>).
    /// </summary>
    public static string NormalizarDominio(string? dominio)
    {
        var limpio = (dominio ?? string.Empty).Trim().TrimStart('@').TrimEnd('.').ToLowerInvariant();
        return limpio.Length <= LongitudMaximaDominio && RegexDominio().IsMatch(limpio)
            ? limpio
            : throw ReglaDeNegocioException.Validacion("dominio-invalido", "Escribe un dominio como proveedores.miempresa.com (sin http:// ni la arroba).");
    }

    /// <summary>La parte local (antes de la arroba); nula o vacía = <c>no-responder</c>. 422 <c>parte-local-invalida</c>.</summary>
    public static string NormalizarParteLocal(string? parteLocal)
    {
        var limpio = (parteLocal ?? string.Empty).Trim().ToLowerInvariant();
        if (limpio.Length == 0)
            return ParteLocalPorDefecto;
        return RegexParteLocal().IsMatch(limpio)
            ? limpio
            : throw ReglaDeNegocioException.Validacion("parte-local-invalida", "La parte antes de la arroba admite letras, números, punto, guion y más.");
    }

    public static string? NormalizarNombre(string? nombre)
    {
        var limpio = (nombre ?? string.Empty).Trim();
        if (limpio.Length == 0)
            return null;
        return limpio.Length <= LongitudMaximaNombre && !limpio.Any(char.IsControl) && !limpio.Contains('<') && !limpio.Contains('>')
            ? limpio
            : throw ReglaDeNegocioException.Validacion("nombre-remitente-invalido",
                $"El nombre del remitente admite hasta {LongitudMaximaNombre} caracteres, sin < ni >.");
    }

    /// <summary>
    /// Registra o cambia el dominio: queda <c>Pendiente</c> con los registros que dio el proveedor. Si el dominio es el mismo y solo
    /// cambian el nombre o la parte local, conserva su estado y sus registros.
    /// </summary>
    public static DominioDeCorreoEmpresa Registrar(DominioDeCorreoEmpresa? actual, Guid empresaId, string dominio, string? parteLocal, string? nombre,
        IReadOnlyList<RegistroDns> registros, DateTimeOffset ahora)
    {
        var dominioNormal = NormalizarDominio(dominio);
        var local = NormalizarParteLocal(parteLocal);
        var nombreNormal = NormalizarNombre(nombre);
        if (actual is not null && actual.Dominio == dominioNormal)
        {
            actual.ParteLocal = local;
            actual.NombreRemitente = nombreNormal;
            actual.ActualizadoEn = ahora;
            return actual;
        }

        return new DominioDeCorreoEmpresa
        {
            Id = empresaId,
            Dominio = dominioNormal,
            ParteLocal = local,
            NombreRemitente = nombreNormal,
            Estado = EstadoDeDominioDeCorreo.Pendiente,
            Registros = [.. registros],
            CreadoEn = actual?.CreadoEn ?? ahora,
            ActualizadoEn = ahora
        };
    }

    /// <summary>Guarda el resultado de verificar el DNS: verificado si todos los registros coinciden; si no, fallido con lo que se encontró.</summary>
    public void RegistrarVerificacion(IReadOnlyList<ComprobacionDeRegistroDns> comprobaciones, DateTimeOffset ahora)
    {
        UltimaVerificacion = [.. comprobaciones];
        UltimaVerificacionEn = ahora;
        Verificaciones++;
        var completo = Registros.Count > 0 && Registros.All(r => comprobaciones.Any(c => c.Finalidad == r.Finalidad && c.Correcto));
        if (completo)
        {
            Estado = EstadoDeDominioDeCorreo.Verificado;
            VerificadoEn ??= ahora;
        }
        else
        {
            Estado = EstadoDeDominioDeCorreo.Fallido;
            VerificadoEn = null;
        }

        ActualizadoEn = ahora;
    }
}
