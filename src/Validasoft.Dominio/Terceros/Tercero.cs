using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;

namespace Validasoft.Dominio.Terceros;

public enum TipoPersona
{
    Natural,
    Juridica,
    Consorcio
}

public sealed record Contacto(string Nombre, string Correo, string? Telefono, string Rol);

/// <summary>
/// Persona natural o jurídica, única en toda la plataforma por <see cref="Identificacion"/> (esquema
/// <c>boveda</c>, global). Sus datos maestros los propone el tercero (P-1-7): una empresa que registra un
/// tercero ya existente no los sobreescribe.
/// </summary>
public sealed class Tercero
{
    public Guid Id { get; set; }

    public Identificacion Identificacion { get; set; } = null!;

    /// <summary>Copia de <see cref="Identificacion.Clave"/> para el índice único global.</summary>
    public string ClaveIdentificacion { get; set; } = string.Empty;

    public TipoPersona TipoPersona { get; set; }

    public string Nombre { get; set; } = string.Empty;

    public string? NombreComercial { get; set; }

    public List<Contacto> Contactos { get; set; } = [];

    public bool EsExtranjero { get; set; }

    public Guid? SucesorDe { get; set; }

    /// <summary>Tenant que lo registró; junto con los vínculos define quién puede leer sus datos (anti-cosecha, docs/07 §10).</summary>
    public Guid? CreadoPorTenant { get; set; }

    public DateTimeOffset CreadoEn { get; set; }

    public static Tercero Crear(Identificacion identificacion, TipoPersona tipoPersona, string nombre,
        string? nombreComercial, IEnumerable<Contacto>? contactos, DateTimeOffset ahora)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre o razón social del tercero es obligatorio.");

        var tipoId = CatalogoIdentificacion.Buscar(identificacion.Pais, identificacion.Tipo)!;
        var aplica = tipoId.AplicaA;
        if ((aplica == AplicaA.Natural && tipoPersona != TipoPersona.Natural) ||
            (aplica == AplicaA.Juridica && tipoPersona == TipoPersona.Natural))
            throw ReglaDeNegocioException.Validacion("tipo-identificacion-no-aplica",
                $"{tipoId.Nombre} no aplica a una persona {tipoPersona.ToString().ToLowerInvariant()}.");

        var listaContactos = (contactos ?? []).ToList();
        foreach (var c in listaContactos)
        {
            if (string.IsNullOrWhiteSpace(c.Correo) || !c.Correo.Contains('@'))
                throw ReglaDeNegocioException.Validacion("correo-invalido", $"El correo '{c.Correo}' del contacto {c.Nombre} no es válido.");
        }

        return new Tercero
        {
            Id = Guid.CreateVersion7(),
            Identificacion = identificacion,
            ClaveIdentificacion = identificacion.Clave,
            TipoPersona = tipoPersona,
            Nombre = nombre.Trim(),
            NombreComercial = string.IsNullOrWhiteSpace(nombreComercial) ? null : nombreComercial.Trim(),
            Contactos = listaContactos,
            EsExtranjero = identificacion.Pais != "CO",
            CreadoEn = ahora
        };
    }
}
