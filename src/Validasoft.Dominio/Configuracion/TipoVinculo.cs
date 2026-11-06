using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Configuracion;

public enum TipoAtributo
{
    Texto,
    Numero,
    Fecha,
    Booleano,
    Opcion,
    ListaOpciones
}

/// <summary>Atributo que la empresa puede registrar en un vínculo de este tipo (alimenta las reglas de exigencia).</summary>
public sealed record AtributoPermitido(string Clave, TipoAtributo Tipo, bool Obligatorio, IReadOnlyList<string>? Opciones);

/// <summary>
/// Naturaleza de la relación (proveedor, cliente, empleado, usuario…). Catálogo por tenant, esquema
/// <c>validacion</c>, multi-tenant.
/// </summary>
public sealed class TipoVinculo
{
    public Guid Id { get; set; }

    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public List<AtributoPermitido> AtributosPermitidos { get; set; } = [];

    /// <summary>Si el cierre aprobado exige cuenta bancaria designada (proveedor por defecto, esc. 1).</summary>
    public bool ExigeCuentaDesignada { get; set; }

    /// <summary>Si el enlace de acceso de sus invitaciones exige además un código OTP enviado al correo (docs/07 §2.2).</summary>
    public bool ExigeOtpEnEnlace { get; set; }

    public bool Activo { get; set; } = true;

    public static TipoVinculo Crear(string codigo, string nombre, IEnumerable<AtributoPermitido>? atributos, bool exigeCuentaDesignada,
        bool exigeOtpEnEnlace = false)
    {
        var codigoNormalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (!Regex.IsMatch(codigoNormalizado, "^[A-Z][A-Z0-9_]{1,39}$"))
            throw ReglaDeNegocioException.Validacion("codigo-invalido",
                "El código debe empezar con letra y usar solo mayúsculas, números y guion bajo (máx. 40).");

        if (string.IsNullOrWhiteSpace(nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del tipo de vínculo es obligatorio.");

        var lista = (atributos ?? []).ToList();
        var duplicada = lista.GroupBy(a => a.Clave).FirstOrDefault(g => g.Count() > 1);
        if (duplicada is not null)
            throw ReglaDeNegocioException.Validacion("atributo-duplicado", $"El atributo '{duplicada.Key}' está repetido.");

        foreach (var a in lista.Where(a => a.Tipo is TipoAtributo.Opcion or TipoAtributo.ListaOpciones))
        {
            if (a.Opciones is null || a.Opciones.Count == 0)
                throw ReglaDeNegocioException.Validacion("opciones-requeridas", $"El atributo '{a.Clave}' necesita opciones.");
        }

        return new TipoVinculo
        {
            Id = Guid.CreateVersion7(),
            Codigo = codigoNormalizado,
            Nombre = nombre.Trim(),
            AtributosPermitidos = lista,
            ExigeCuentaDesignada = exigeCuentaDesignada,
            ExigeOtpEnEnlace = exigeOtpEnEnlace
        };
    }

    /// <summary>Tipos que se siembran al crear un tenant; la empresa puede agregar más. Solo PROVEEDOR exige OTP en el enlace.</summary>
    public static IReadOnlyList<TipoVinculo> Semilla() =>
    [
        Crear("PROVEEDOR", "Proveedor",
        [
            new("montoEstimado", TipoAtributo.Numero, false, null),
            new("nivelRiesgo", TipoAtributo.Opcion, false, ["Bajo", "Medio", "Alto"]),
            new("categorias", TipoAtributo.ListaOpciones, false, ["Materiales", "Servicios", "Equipos", "Mano de obra"])
        ], exigeCuentaDesignada: true, exigeOtpEnEnlace: true),
        Crear("CLIENTE", "Cliente", [new("nivelRiesgo", TipoAtributo.Opcion, false, ["Bajo", "Medio", "Alto"])], false),
        Crear("EMPLEADO", "Empleado", [new("cargo", TipoAtributo.Texto, false, null)], false),
        Crear("USUARIO", "Usuario", [], false)
    ];
}
