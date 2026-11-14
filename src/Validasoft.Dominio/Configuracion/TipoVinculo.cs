using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;

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

/// <summary>Cómo se identifica a quienes aprueban las invitaciones de un tipo de vínculo: por un rol o por un área de la empresa.</summary>
public enum TipoDeAprobadores
{
    Rol,
    Area
}

/// <summary>
/// Quién aprueba que se invite a un tercero antes de que salga el correo (decisión AI). <see cref="Valor"/> es el nombre del rol
/// (<c>Administrador</c>, <c>OficialCumplimiento</c> o <c>Revisor</c>) o el código de un área, que se busca en la empresa del vínculo (los tipos son
/// del grupo; las áreas, de cada empresa).
/// </summary>
public sealed record AprobadoresDeInvitacion(TipoDeAprobadores Tipo, string Valor)
{
    /// <summary>Roles que pueden designarse (todos tienen <c>vinculos:escribir</c>; el auditor, que solo lee, no).</summary>
    public static readonly IReadOnlyList<Rol> RolesPermitidos = [Rol.Administrador, Rol.OficialCumplimiento, Rol.Revisor];

    /// <summary>El rol designado; nulo si son los miembros de un área.</summary>
    public Rol? RolDesignado => Tipo == TipoDeAprobadores.Rol && Enum.TryParse<Rol>(Valor, true, out var rol) ? rol : null;

    /// <summary>Valida y normaliza (rol con su nombre canónico, código de área en mayúsculas). 422 <c>aprobadores-invalidos</c>.</summary>
    public static AprobadoresDeInvitacion Validar(AprobadoresDeInvitacion aprobadores)
    {
        var valor = (aprobadores.Valor ?? string.Empty).Trim();
        if (aprobadores.Tipo == TipoDeAprobadores.Rol)
        {
            return Enum.TryParse<Rol>(valor, true, out var rol) && RolesPermitidos.Contains(rol)
                ? new AprobadoresDeInvitacion(TipoDeAprobadores.Rol, rol.ToString())
                : throw ReglaDeNegocioException.Validacion("aprobadores-invalidos",
                    $"El rol que aprueba las invitaciones debe ser uno de: {string.Join(", ", RolesPermitidos)}.");
        }

        var codigo = valor.ToUpperInvariant();
        return Regex.IsMatch(codigo, "^[A-Z][A-Z0-9_]{1,39}$")
            ? new AprobadoresDeInvitacion(TipoDeAprobadores.Area, codigo)
            : throw ReglaDeNegocioException.Validacion("aprobadores-invalidos", "El código del área que aprueba las invitaciones no es válido.");
    }
}

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

    /// <summary>
    /// Si invitar a un tercero con este tipo necesita la aprobación de otra persona antes de que salga el correo (equivale al estado -5 del SRM,
    /// decisión AI): el vínculo nace con la invitación retenida hasta que quien aprueba la decida.
    /// </summary>
    public bool RequiereAprobacionParaInvitar { get; set; }

    /// <summary>Quién aprueba (un rol o un área de la empresa del vínculo); obligatorio con <see cref="RequiereAprobacionParaInvitar"/>.</summary>
    public AprobadoresDeInvitacion? AprobadoresDeInvitacion { get; set; }

    public bool Activo { get; set; } = true;

    public static TipoVinculo Crear(string codigo, string nombre, IEnumerable<AtributoPermitido>? atributos, bool exigeCuentaDesignada,
        bool exigeOtpEnEnlace = false, bool requiereAprobacionParaInvitar = false, AprobadoresDeInvitacion? aprobadores = null)
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

        if (requiereAprobacionParaInvitar && aprobadores is null)
            throw ReglaDeNegocioException.Validacion("aprobadores-requeridos", "Indica quién aprueba las invitaciones: un rol o un área de la empresa.");

        return new TipoVinculo
        {
            Id = Guid.CreateVersion7(),
            Codigo = codigoNormalizado,
            Nombre = nombre.Trim(),
            AtributosPermitidos = lista,
            ExigeCuentaDesignada = exigeCuentaDesignada,
            ExigeOtpEnEnlace = exigeOtpEnEnlace,
            RequiereAprobacionParaInvitar = requiereAprobacionParaInvitar,
            AprobadoresDeInvitacion = aprobadores is null ? null : Configuracion.AprobadoresDeInvitacion.Validar(aprobadores)
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
