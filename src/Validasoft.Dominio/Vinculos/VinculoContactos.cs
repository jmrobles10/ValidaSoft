using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Vinculos;

/// <summary>Para qué se contacta a la persona: lo que cada sistema integrador usa para escribirle a quien corresponde.</summary>
public enum RolDeContacto
{
    Comercial,
    Pagos,
    Cumplimiento,
    Notificaciones
}

/// <summary>
/// Contacto del tercero con <b>esta</b> empresa (IO-3): el mismo tercero tiene contactos distintos para cada empresa con la que se
/// vincula. Va en el vínculo, no en el tercero, así que cada empresa ve solo los suyos. <see cref="Cargo"/> y
/// <see cref="Telefono"/> son opcionales.
/// </summary>
public sealed record ContactoDeEmpresa(string Nombre, string? Cargo, string Correo, string? Telefono, RolDeContacto Rol);

/// <summary>Se reemplazó la lista de contactos del tercero con la empresa (la lista completa, antes y después).</summary>
public sealed record ContactosDeVinculoActualizados(IReadOnlyList<ContactoDeEmpresa> Antes, IReadOnlyList<ContactoDeEmpresa> Despues, DateTimeOffset Fecha);

/// <summary>Contactos del tercero con la empresa del vínculo (IO-3): los edita el tercero o la empresa y los consume cualquier sistema integrador.</summary>
public sealed partial class Vinculo
{
    public const int MaximoContactos = 20;

    public const int MaximoNombreDeContacto = 200;

    public const int MaximoCargoDeContacto = 120;

    public const int MaximoTelefonoDeContacto = 40;

    /// <summary>Contactos del tercero con esta empresa, en el orden en que se guardaron.</summary>
    public List<ContactoDeEmpresa> Contactos { get; set; } = [];

    /// <summary>
    /// Reemplaza la lista de contactos. Se normaliza (espacios, correo en minúsculas) y se valida: hasta
    /// <see cref="MaximoContactos"/>, nombre y correo obligatorios, el mismo correo no se repite con el mismo rol. Sin cambios ⇒ nulo
    /// (idempotente). Un vínculo anulado ya no cambia.
    /// </summary>
    public ContactosDeVinculoActualizados? ActualizarContactos(IEnumerable<ContactoDeEmpresa>? contactos, DateTimeOffset ahora)
    {
        if (Estado == EstadoVinculo.Anulado)
            throw ReglaDeNegocioException.Conflicto("vinculo-anulado", "Un vínculo anulado ya no se modifica.");

        var nuevos = Normalizar(contactos ?? []);
        return nuevos.SequenceEqual(Contactos) ? null : new ContactosDeVinculoActualizados([.. Contactos], nuevos, ahora);
    }

    /// <summary>Función pura: la lista normalizada o 422 con el motivo.</summary>
    public static IReadOnlyList<ContactoDeEmpresa> Normalizar(IEnumerable<ContactoDeEmpresa> contactos)
    {
        var lista = contactos.ToList();
        if (lista.Count > MaximoContactos)
            throw ReglaDeNegocioException.Validacion("contactos-demasiados", $"Una empresa admite hasta {MaximoContactos} contactos.");

        var normalizados = new List<ContactoDeEmpresa>();
        foreach (var c in lista)
        {
            var nombre = (c.Nombre ?? string.Empty).Trim();
            var correo = (c.Correo ?? string.Empty).Trim().ToLowerInvariant();
            var cargo = string.IsNullOrWhiteSpace(c.Cargo) ? null : c.Cargo.Trim();
            var telefono = string.IsNullOrWhiteSpace(c.Telefono) ? null : c.Telefono.Trim();
            if (nombre.Length == 0)
                throw ReglaDeNegocioException.Validacion("contacto-nombre-requerido", "Cada contacto necesita su nombre.");
            if (nombre.Length > MaximoNombreDeContacto || (cargo?.Length ?? 0) > MaximoCargoDeContacto || (telefono?.Length ?? 0) > MaximoTelefonoDeContacto)
                throw ReglaDeNegocioException.Validacion("contacto-demasiado-largo",
                    $"El nombre admite hasta {MaximoNombreDeContacto} caracteres, el cargo {MaximoCargoDeContacto} y el teléfono {MaximoTelefonoDeContacto}.");
            if (!CorreoValido(correo))
                throw ReglaDeNegocioException.Validacion("correo-invalido", $"El correo '{c.Correo}' del contacto {nombre} no es válido.");
            if (!Enum.IsDefined(c.Rol))
                throw ReglaDeNegocioException.Validacion("contacto-rol-invalido", "El rol del contacto es Comercial, Pagos, Cumplimiento o Notificaciones.");
            if (normalizados.Any(x => x.Correo == correo && x.Rol == c.Rol))
                throw ReglaDeNegocioException.Validacion("contacto-repetido", $"{correo} ya está como contacto de {c.Rol}.");
            normalizados.Add(new ContactoDeEmpresa(nombre, cargo, correo, telefono, c.Rol));
        }

        return normalizados;
    }

    private static bool CorreoValido(string correo)
    {
        var arroba = correo.IndexOf('@', StringComparison.Ordinal);
        return arroba > 0 && arroba < correo.Length - 1 && !correo.Contains(' ', StringComparison.Ordinal) &&
               correo.IndexOf('@', arroba + 1) < 0 && correo[(arroba + 1)..].Contains('.', StringComparison.Ordinal);
    }

    public void Apply(ContactosDeVinculoActualizados e) => Contactos = [.. e.Despues];
}
