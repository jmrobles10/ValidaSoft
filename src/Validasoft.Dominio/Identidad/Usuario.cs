using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identificaciones;

namespace Validasoft.Dominio.Identidad;

/// <summary>Roles del tenant (docs/07 §3.1). Se asignan en Validasoft, iguales para todos los sistemas (ID-2).</summary>
public enum Rol
{
    Administrador,
    Revisor,
    OficialCumplimiento,
    Auditor
}

/// <summary>Rol en una empresa interna o, con <see cref="EmpresaId"/> nulo, en todo el tenant (ADR-0008).</summary>
public sealed record RolAsignado(Rol Rol, Guid? EmpresaId);

/// <summary>Quién es la persona en un sistema integrado: <c>(sistema, sub)</c> de la aserción delegada.</summary>
public sealed record IdentidadExterna(Guid SistemaId, string IdExterno);

/// <summary>
/// Persona que revisa, aprueba o administra (ADR-0011). Validasoft no guarda contraseñas: la autentica el sistema
/// integrado y la reconoce por sus identidades externas. Esquema <c>identidad</c>, multi-tenant.
/// </summary>
public sealed class Usuario
{
    public Guid Id { get; set; }

    /// <summary>
    /// Grupo al que pertenece (copia del <c>tenant_id</c> de Marten): el ingreso directo busca la cuenta por correo en todos los
    /// grupos y necesita saber de cuál es cada una. Nulo en los usuarios anteriores al ingreso directo, que se completan al
    /// habilitar ese modo en su grupo.
    /// </summary>
    public Guid? TenantId { get; set; }

    public string Nombre { get; set; } = string.Empty;

    /// <summary>Normalizado (minúsculas, sin espacios); único en el tenant.</summary>
    public string Correo { get; set; } = string.Empty;

    /// <summary>Opcional; sirve para detectar conflicto de intereses con terceros.</summary>
    public Identificacion? Identificacion { get; set; }

    public bool Activo { get; set; } = true;

    public List<IdentidadExterna> Identidades { get; set; } = [];

    public List<RolAsignado> Roles { get; set; } = [];

    public DateTimeOffset CreadoEn { get; set; }

    public DateTimeOffset? UltimoAcceso { get; set; }

    /// <summary>Cuándo se le envió la última invitación a ingresar con su correo (ING-5).</summary>
    public DateTimeOffset? InvitadoEn { get; set; }

    public static Usuario Crear(string nombre, string correo, Identificacion? identificacion, DateTimeOffset ahora, Guid? tenantId = null)
    {
        if (string.IsNullOrWhiteSpace(nombre))
            throw ReglaDeNegocioException.Validacion("nombre-requerido", "El nombre del usuario es obligatorio.");

        return new Usuario
        {
            Id = Guid.CreateVersion7(),
            TenantId = tenantId,
            Nombre = nombre.Trim(),
            Correo = NormalizarCorreo(correo),
            Identificacion = identificacion,
            CreadoEn = ahora
        };
    }

    /// <summary>Alta automática desde una aserción delegada: nace <b>sin roles</b> (ID-2).</summary>
    public static Usuario CrearDesdeSistema(Guid sistemaId, string idExterno, string nombre, string correo, DateTimeOffset ahora, Guid? tenantId = null)
    {
        var usuario = Crear(nombre, correo, null, ahora, tenantId);
        usuario.EnlazarIdentidad(sistemaId, idExterno);
        usuario.UltimoAcceso = ahora;
        return usuario;
    }

    public static string NormalizarCorreo(string? correo)
    {
        var normalizado = (correo ?? string.Empty).Trim().ToLowerInvariant();
        if (normalizado.Length is < 3 or > 254 || normalizado.Count(c => c == '@') != 1 || normalizado.StartsWith('@') ||
            normalizado.EndsWith('@') || normalizado.Any(char.IsWhiteSpace))
            throw ReglaDeNegocioException.Validacion("correo-invalido", "El correo del usuario no es válido.");
        return normalizado;
    }

    /// <summary>Una sola identidad por sistema: el mismo <c>sub</c> es idempotente; otro distinto es conflicto.</summary>
    public void EnlazarIdentidad(Guid sistemaId, string idExterno)
    {
        if (string.IsNullOrWhiteSpace(idExterno))
            throw ReglaDeNegocioException.Validacion("identidad-externa-invalida", "La identidad externa necesita el id del usuario en el sistema.");

        var id = idExterno.Trim();
        var existente = Identidades.FirstOrDefault(i => i.SistemaId == sistemaId);
        if (existente is not null && existente.IdExterno != id)
            throw ReglaDeNegocioException.Conflicto("identidad-en-conflicto",
                "El usuario ya está enlazado a otra identidad de ese sistema.");

        if (existente is null)
            Identidades.Add(new IdentidadExterna(sistemaId, id));
    }

    /// <summary>Reemplaza los roles. Un rol de todo el tenant hace redundante el mismo rol por empresa.</summary>
    public void AsignarRoles(IEnumerable<RolAsignado> roles)
    {
        if (!Activo)
            throw ReglaDeNegocioException.Conflicto("usuario-inactivo", "No se asignan roles a un usuario desactivado.");

        var lista = roles.Distinct().ToList();
        var deTenant = lista.Where(r => r.EmpresaId is null).Select(r => r.Rol).ToHashSet();
        Roles = [.. lista.Where(r => r.EmpresaId is null || !deTenant.Contains(r.Rol))];
    }

    public void Desactivar()
    {
        if (!Activo)
            throw ReglaDeNegocioException.Conflicto("usuario-inactivo", "El usuario ya está desactivado.");
        Activo = false;
    }

    /// <summary>
    /// Debe tener passkey: lo exige algún rol (<see cref="PoliticaDePasskeys"/>) o el grupo lo pide a todos (ING-2). Sin passkey, el código
    /// del correo (un solo factor) no abre sesión: solo sirve para registrar una.
    /// </summary>
    public bool ExigePasskey(bool paraTodosEnElGrupo) => paraTodosEnElGrupo || Roles.Any(r => PoliticaDePasskeys.RolExige(r.Rol));

    /// <summary>Si tiene algún rol en la empresa, directamente o para todo el tenant.</summary>
    public bool TieneRolEn(Guid empresaId) => Roles.Any(r => r.EmpresaId is null || r.EmpresaId == empresaId);
}

/// <summary>
/// Índice único <c>(sistema, sub) → usuario</c> por tenant. Su id es <c>{sistemaId:N}:{sub}</c>: insertarlo dos veces
/// falla, lo que hace única la identidad externa aun con altas concurrentes. Esquema <c>identidad</c>, multi-tenant.
/// </summary>
public sealed class IndiceIdentidadExterna
{
    public string Id { get; set; } = string.Empty;

    public Guid UsuarioId { get; set; }

    public static string Clave(Guid sistemaId, string idExterno) => $"{sistemaId:N}:{idExterno.Trim()}";
}
