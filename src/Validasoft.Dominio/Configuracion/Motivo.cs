using System.Text.RegularExpressions;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Configuracion;

/// <summary>Sobre qué recae el motivo (docs/05 §4, <c>MotivoDto.categoria</c>).</summary>
public enum CategoriaMotivo
{
    Documento,
    Formulario,
    Contacto,
    Otro
}

/// <summary>Para qué decisión sirve: observar, rechazar el expediente o ambas.</summary>
public enum AplicaMotivo
{
    Observacion,
    Rechazo,
    Ambos
}

/// <summary>
/// Motivo tipificado de una observación o de un rechazo (P-5-1, docs/03 §13). Los de la plataforma son fijos
/// (<see cref="Base"/>); el tenant agrega los suyos (esquema <c>validacion</c>, multi-tenant, con auditoría). El
/// <see cref="TextoAlTercero"/> es lo que el tercero lee en el correo y en su vista: nunca nombra al revisor.
/// </summary>
public sealed class Motivo
{
    public Guid Id { get; set; }

    /// <summary>Minúsculas con guiones (<c>datos-no-coinciden</c>); único en el tenant junto con los de la plataforma; inmutable.</summary>
    public string Codigo { get; set; } = string.Empty;

    public string Nombre { get; set; } = string.Empty;

    public CategoriaMotivo Categoria { get; set; }

    public AplicaMotivo AplicaA { get; set; }

    public string TextoAlTercero { get; set; } = string.Empty;

    public bool Base { get; set; }

    public bool Activo { get; set; } = true;

    public static Motivo Crear(string codigo, string nombre, CategoriaMotivo categoria, AplicaMotivo aplicaA, string textoAlTercero)
    {
        var normalizado = (codigo ?? string.Empty).Trim().ToLowerInvariant();
        if (!Regex.IsMatch(normalizado, "^[a-z][a-z0-9]*(-[a-z0-9]+)*$") || normalizado.Length > 60)
            throw ReglaDeNegocioException.Validacion("codigo-invalido",
                "El código del motivo usa minúsculas, números y guiones (p. ej. 'poliza-sin-firma'), máximo 60 caracteres.");

        if (MotivosBase.Buscar(normalizado) is not null)
            throw ReglaDeNegocioException.Conflicto("motivo-duplicado", $"'{normalizado}' es un motivo de la plataforma.");

        var motivo = new Motivo { Id = Guid.CreateVersion7(), Codigo = normalizado };
        motivo.Actualizar(nombre, categoria, aplicaA, textoAlTercero, true);
        return motivo;
    }

    public void Actualizar(string nombre, CategoriaMotivo categoria, AplicaMotivo aplicaA, string textoAlTercero, bool? activo)
    {
        if (Base)
            throw ReglaDeNegocioException.Conflicto("motivo-base", "Los motivos de la plataforma no se modifican.");

        Nombre = string.IsNullOrWhiteSpace(nombre)
            ? throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del motivo es obligatorio.")
            : nombre.Trim();
        TextoAlTercero = string.IsNullOrWhiteSpace(textoAlTercero)
            ? throw ReglaDeNegocioException.Validacion("texto-requerido", "El motivo necesita el texto que leerá el tercero.")
            : textoAlTercero.Trim();
        Categoria = categoria;
        AplicaA = aplicaA;
        Activo = activo ?? Activo;
    }

    /// <summary>¿Sirve para esta decisión? <see cref="AplicaMotivo.Ambos"/> sirve para las dos.</summary>
    public bool SirvePara(AplicaMotivo decision) => AplicaA == AplicaMotivo.Ambos || AplicaA == decision;
}

/// <summary>Motivos base de la plataforma (docs/03 §13). Fijos en código: no se guardan ni se editan.</summary>
public static class MotivosBase
{
    public static readonly IReadOnlyList<Motivo> Todos =
    [
        Base("ilegible", "Ilegible", CategoriaMotivo.Documento, "El documento no se puede leer con claridad."),
        Base("incompleto", "Incompleto", CategoriaMotivo.Documento, "La información o el documento está incompleto."),
        Base("vencido", "Vencido", CategoriaMotivo.Documento, "El documento está vencido."),
        Base("no-corresponde-al-tercero", "No corresponde al tercero", CategoriaMotivo.Documento,
            "El documento no corresponde a la persona o empresa que se está validando."),
        Base("datos-no-coinciden", "Datos no coinciden", CategoriaMotivo.Formulario, "Los datos informados no coinciden con los soportes."),
        Base("formato-no-vigente", "Formato no vigente", CategoriaMotivo.Documento, "El formato usado no es el vigente."),
        Base("firma-faltante", "Firma faltante", CategoriaMotivo.Documento, "Falta la firma requerida."),
        Base("frescura-incumplida", "Frescura incumplida", CategoriaMotivo.Documento,
            "El documento no cumple la antigüedad máxima exigida.")
    ];

    public static Motivo? Buscar(string codigo) => Todos.FirstOrDefault(m => m.Codigo == codigo);

    /// <summary>Id determinista (16 primeros bytes de un SHA-256) para que la API exponga siempre el mismo id de cada motivo base.</summary>
    private static Motivo Base(string codigo, string nombre, CategoriaMotivo categoria, string texto) => new()
    {
        Id = new Guid(System.Security.Cryptography.SHA256.HashData(System.Text.Encoding.UTF8.GetBytes("validasoft:motivo-base:" + codigo))[..16]),
        Codigo = codigo,
        Nombre = nombre,
        Categoria = categoria,
        AplicaA = AplicaMotivo.Ambos,
        TextoAlTercero = texto,
        Base = true
    };
}
