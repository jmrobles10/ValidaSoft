using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Configuracion;

/// <summary>
/// Área revisora de una empresa interna (jurídica, compras, cumplimiento…): agrupa usuarios a los que se asignan
/// las tareas de revisión. Esquema <c>validacion</c>, multi-tenant, aislada por <see cref="EmpresaId"/>.
/// </summary>
public sealed class Area
{
    public Guid Id { get; set; }

    public Guid EmpresaId { get; set; }

    /// <summary>Mayúsculas, único en la empresa e inmutable.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public List<Guid> Miembros { get; set; } = [];

    public bool Activa { get; set; } = true;

    public static Area Crear(Guid empresaId, string codigo, string nombre)
    {
        var codigoNormalizado = (codigo ?? string.Empty).Trim().ToUpperInvariant();
        if (!Regex.IsMatch(codigoNormalizado, "^[A-Z][A-Z0-9_]{1,39}$"))
            throw ReglaDeNegocioException.Validacion("codigo-invalido",
                "El código del área debe empezar con letra y usar solo mayúsculas, números y guion bajo (máx. 40).");

        return new Area
        {
            Id = Guid.CreateVersion7(),
            EmpresaId = empresaId,
            Codigo = codigoNormalizado,
            Nombre = ValidarNombre(nombre)
        };
    }

    public void Actualizar(string nombre, bool? activa)
    {
        Nombre = ValidarNombre(nombre);
        Activa = activa ?? Activa;
    }

    /// <summary>Reemplaza los miembros. Quién puede serlo (usuario activo con rol en la empresa) lo verifica la aplicación.</summary>
    public void DefinirMiembros(IEnumerable<Guid> usuarios)
    {
        if (!Activa)
            throw ReglaDeNegocioException.Conflicto("area-inactiva", "No se cambian los miembros de un área inactiva.");

        var lista = usuarios.ToList();
        if (lista.Contains(Guid.Empty))
            throw ReglaDeNegocioException.Validacion("miembro-invalido", "Hay un miembro sin identificador.");

        Miembros = [.. lista.Distinct()];
    }

    private static string ValidarNombre(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre)
            ? throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del área es obligatorio.")
            : nombre.Trim();
}
