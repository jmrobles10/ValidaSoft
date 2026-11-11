using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Consentimientos;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Plataforma;

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

    /// <summary>
    /// Si es un tercero de pruebas (SB-4): el tenant sandbox al que pertenece. Su <see cref="ClaveIdentificacion"/> lleva ese
    /// tenant por delante, así no choca con el tercero real que tenga la misma identificación; la identificación sin prefijo
    /// sigue en <see cref="Identificacion"/>. Nulo en los terceros reales.
    /// </summary>
    public Guid? SandboxTenantId { get; set; }

    /// <summary>Marca al tercero como de pruebas de ese sandbox y namespacea su clave (SB-4).</summary>
    public void AislarEnSandbox(Guid tenantSandboxId)
    {
        SandboxTenantId = tenantSandboxId;
        ClaveIdentificacion = AislamientoDeSandbox.ClaveDeTercero(Identificacion.Clave, tenantSandboxId);
    }

    public DateTimeOffset CreadoEn { get; set; }

    /// <summary>Versión del aviso de la plataforma que aceptó (P-6-5); sin ella sus versiones se procesan sin IA (B-2/B-8).</summary>
    public int? AvisoPlataformaVersion { get; set; }

    public string? AvisoPlataformaHash { get; set; }

    public DateTimeOffset? AvisoPlataformaAceptadoEn { get; set; }

    /// <summary>¿Debe aceptar el aviso vigente? Sí, si nunca lo aceptó o aceptó una versión anterior.</summary>
    public bool RequiereAvisoPlataforma(TextoLegal? vigente) =>
        vigente is not null && AvisoPlataformaVersion != vigente.Version;

    public void AceptarAvisoPlataforma(TextoLegal aviso, DateTimeOffset ahora)
    {
        if (aviso.Tipo != TipoTextoLegal.AvisoPlataforma)
            throw new InvalidOperationException("Solo se registra aquí el aviso de la plataforma.");

        AvisoPlataformaVersion = aviso.Version;
        AvisoPlataformaHash = aviso.Hash;
        AvisoPlataformaAceptadoEn = ahora;
    }

    /// <summary>¿El correo es de alguno de sus contactos? (comparación sin mayúsculas ni espacios).</summary>
    public bool TieneContacto(string correo)
    {
        var normalizado = (correo ?? string.Empty).Trim().ToLowerInvariant();
        return normalizado.Length > 0 && Contactos.Any(c => string.Equals(c.Correo.Trim(), normalizado, StringComparison.OrdinalIgnoreCase));
    }

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
