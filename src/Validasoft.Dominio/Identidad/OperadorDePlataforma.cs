using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Identidad;

/// <summary>
/// Sistema desde el que entran los operadores de Plataforma (PT-4): el backoffice del proveedor del SaaS autentica a su
/// persona y firma una aserción de identidad delegada (ADR-0011) que Validasoft verifica con esta clave pública. Es global
/// (esquema <c>identidad</c>, sin tenant): no da acceso a ningún tenant, solo a la consola de Plataforma. Sus orígenes entran
/// en <c>frame-ancestors</c> como los de los sistemas integrados, para que pueda alojar la consola en un iframe.
/// </summary>
public sealed partial class SistemaDePlataforma
{
    /// <summary>El código (mayúsculas): es el <c>iss</c> de sus aserciones.</summary>
    public string Id { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    /// <summary>SPKI en PEM: RSA ≥ 2048 bits o EC P-256 (como <see cref="SistemaIntegrado"/>).</summary>
    public string ClavePublicaPem { get; set; } = string.Empty;

    public List<string> OrigenesPermitidos { get; set; } = [];

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset ActualizadoEn { get; set; }

    public string Codigo => Id;

    /// <summary>Alta o reemplazo (nombre, clave y orígenes). Reemplazar la clave invalida al instante las aserciones con la anterior.</summary>
    public static SistemaDePlataforma Fijar(SistemaDePlataforma? existente, string codigo, string nombre, string clavePublicaPem,
        IEnumerable<string>? origenes, bool permitirHttpLocal, DateTimeOffset ahora)
    {
        var codigoNormalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (!RegexCodigo().IsMatch(codigoNormalizado))
            throw ReglaDeNegocioException.Validacion("codigo-invalido",
                "El código del sistema debe empezar con letra y usar solo mayúsculas, números y guion bajo (máx. 20).");
        if (string.IsNullOrWhiteSpace(nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del sistema es obligatorio.");

        var normalizados = (origenes ?? []).Select(o => OrigenWeb.Normalizar(o, permitirHttpLocal)).Distinct(StringComparer.Ordinal).ToList();
        if (normalizados.Count > OrigenWeb.MaximoPorSistema)
            throw ReglaDeNegocioException.Validacion("origenes-excesivos", $"Un sistema admite hasta {OrigenWeb.MaximoPorSistema} orígenes.");

        var sistema = existente ?? new SistemaDePlataforma { Id = codigoNormalizado, CreadoEn = ahora };
        sistema.Nombre = nombre.Trim();
        sistema.ClavePublicaPem = ClavePublica.Validar(clavePublicaPem);
        sistema.OrigenesPermitidos = normalizados;
        sistema.Activo = true;
        sistema.ActualizadoEn = ahora;
        return sistema;
    }

    public void Desactivar(DateTimeOffset ahora)
    {
        if (!Activo)
            throw ReglaDeNegocioException.Conflicto("sistema-inactivo", "El sistema ya está desactivado.");
        Activo = false;
        ActualizadoEn = ahora;
    }

    /// <summary>Vuelve a activar el sistema de Plataforma (PI-7). 409 <c>sistema-activo</c> si ya estaba activo.</summary>
    public void Reactivar(DateTimeOffset ahora)
    {
        if (Activo)
            throw ReglaDeNegocioException.Conflicto("sistema-activo", "El sistema ya está activo.");
        Activo = true;
        ActualizadoEn = ahora;
    }

    [GeneratedRegex("^[A-Z][A-Z0-9_]{1,19}$")]
    private static partial Regex RegexCodigo();
}

/// <summary>
/// Persona que opera la consola de Plataforma (PT-4), registrada en su primer acceso por identidad delegada para la
/// auditoría: Validasoft no guarda su contraseña. Id = <c>{sistema}:{sub}</c>. Un operador desactivado ya no abre sesión.
/// </summary>
public sealed class OperadorDePlataforma
{
    public const int LongitudMaximaSub = 128;

    /// <summary>Sistema de los operadores que entran directo (correo, código y passkey, ING-4): su <c>sub</c> es su correo.</summary>
    public const string SistemaDirecto = "DIRECTO";

    public bool EsDirecto => Sistema == SistemaDirecto;

    public string Id { get; set; } = string.Empty;

    public string Sistema { get; set; } = string.Empty;

    public string IdExterno { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public string Correo { get; set; } = string.Empty;

    public bool Activo { get; set; } = true;

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset? UltimoAcceso { get; set; }

    public static string Clave(string sistema, string idExterno) => $"{sistema}:{idExterno}";

    public static OperadorDePlataforma Registrar(string sistema, string idExterno, string nombre, string correo, DateTimeOffset ahora)
    {
        var sub = (idExterno ?? string.Empty).Trim();
        if (sub.Length is 0 or > LongitudMaximaSub)
            throw ReglaDeNegocioException.NoAutenticado("asercion-incompleta", $"El sub de la aserción debe tener entre 1 y {LongitudMaximaSub} caracteres.");
        return new OperadorDePlataforma
        {
            Id = Clave(sistema, sub),
            Sistema = sistema,
            IdExterno = sub,
            Nombre = nombre.Trim(),
            Correo = Usuario.NormalizarCorreo(correo),
            CreadoEn = ahora,
            UltimoAcceso = ahora
        };
    }

    /// <summary>
    /// Operador de ingreso directo (ING-4): se da de alta con la credencial de arranque o por otro operador y entra con correo, código
    /// y passkey (obligatoria). Su id es <c>DIRECTO:{correo}</c>.
    /// </summary>
    public static OperadorDePlataforma RegistrarDirecto(string nombre, string correo, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del operador es obligatorio.");
        var normalizado = Usuario.NormalizarCorreo(correo);
        if (normalizado.Length > LongitudMaximaSub)
            throw ReglaDeNegocioException.Validacion("correo-invalido", $"El correo del operador admite hasta {LongitudMaximaSub} caracteres.");
        return new OperadorDePlataforma
        {
            Id = Clave(SistemaDirecto, normalizado),
            Sistema = SistemaDirecto,
            IdExterno = normalizado,
            Nombre = nombre.Trim(),
            Correo = normalizado,
            CreadoEn = ahora
        };
    }

    public void Desactivar()
    {
        if (!Activo)
            throw ReglaDeNegocioException.Conflicto("operador-inactivo", "El operador ya está desactivado.");
        Activo = false;
    }

    /// <summary>Vuelve a activar al operador (PI-7): entra de nuevo con sus passkeys. 409 <c>operador-activo</c> si ya estaba activo.</summary>
    public void Reactivar()
    {
        if (Activo)
            throw ReglaDeNegocioException.Conflicto("operador-activo", "El operador ya está activo.");
        Activo = true;
    }

    /// <summary>El sistema es la fuente de nombre y correo; devuelve si cambiaron.</summary>
    public bool Acceder(string nombre, string correo, DateTimeOffset ahora)
    {
        if (!Activo)
            throw ReglaDeNegocioException.NoAutenticado("operador-inactivo", "El operador de Plataforma está desactivado.");
        var nuevoNombre = nombre.Trim();
        var nuevoCorreo = Usuario.NormalizarCorreo(correo);
        var cambio = nuevoNombre != Nombre || nuevoCorreo != Correo;
        Nombre = nuevoNombre;
        Correo = nuevoCorreo;
        UltimoAcceso = ahora;
        return cambio;
    }
}
