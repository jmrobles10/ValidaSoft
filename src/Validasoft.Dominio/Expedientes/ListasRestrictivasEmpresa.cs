using System.Globalization;
using Validasoft.Dominio.Comun;

namespace Validasoft.Dominio.Expedientes;

/// <summary>
/// Proveedores de listas restrictivas (VE-7). <see cref="Infolaft"/> es el principal entre los reales (suscripción de Sinco);
/// <see cref="Stradata"/> y <see cref="Compliance"/> quedan como alternativas del legado. <see cref="Simulado"/> es
/// determinista, solo para desarrollo y pruebas.
/// </summary>
public static class ProveedoresDeListas
{
    public const string Simulado = "Simulado";
    public const string Infolaft = "Infolaft";
    public const string Stradata = "Stradata";
    public const string Compliance = "Compliance";

    /// <summary>En orden de preferencia: el principal real primero.</summary>
    public static readonly IReadOnlyList<string> Todos = [Infolaft, Stradata, Compliance, Simulado];

    public static readonly IReadOnlyList<string> Reales = [Infolaft, Stradata, Compliance];

    /// <summary>
    /// Los que contrata la plataforma una sola vez para todos los clientes (G-2, PP-5): su cuenta se configura en la consola de Plataforma
    /// (<see cref="ListasRestrictivasDePlataforma"/>) y ninguna empresa ve ni carga sus credenciales. Stradata y Compliance, alternativas del
    /// legado, siguen configurándose por empresa.
    /// </summary>
    public static readonly IReadOnlyList<string> DeLaPlataforma = [Infolaft];

    public static bool EsDeLaPlataforma(string nombre) => DeLaPlataforma.Contains(nombre);

    /// <summary>Nombre canónico (sin importar mayúsculas) o nulo si no existe.</summary>
    public static string? Normalizar(string? nombre) =>
        string.IsNullOrWhiteSpace(nombre) ? null : Todos.FirstOrDefault(p => string.Equals(p, nombre.Trim(), StringComparison.OrdinalIgnoreCase));
}

/// <summary>Cómo se busca en el proveedor: por nombre (lo confirmado en Infolaft) o por número de documento (sin confirmar).</summary>
public enum ModoBusquedaListas
{
    PorNombre,
    PorDocumento
}

/// <summary>Credenciales de una empresa con un proveedor: la clave solo cifrada; se expone si está configurada y sus últimos 4.</summary>
public sealed class CredencialesDeListas
{
    public string Usuario { get; set; } = string.Empty;

    public string ClaveProtegida { get; set; } = string.Empty;

    public string UltimosCuatroClave { get; set; } = string.Empty;

    /// <summary>URL propia del contrato (nula = la de la instalación).</summary>
    public string? UrlBase { get; set; }

    /// <summary>Nombre del plan contratado (informativo).</summary>
    public string? Plan { get; set; }

    /// <summary>Búsquedas al mes que cubre la suscripción; al llegar, se dejan de hacer consultas (VE-11). Nulo = sin tope.</summary>
    public int? CuotaMensual { get; set; }

    public ModoBusquedaListas ModoBusqueda { get; set; }

    public DateTimeOffset ActualizadasEn { get; set; }

    /// <summary>
    /// Aplica un cambio (no el de quitar) a las credenciales actuales y devuelve las nuevas. 422 <c>usuario-requerido</c>, <c>clave-requerida</c>,
    /// <c>clave-invalida</c>, <c>url-invalida</c> (https), <c>cuota-invalida</c>, <c>plan-invalido</c>. <paramref name="proteger"/> cifra la clave nueva.
    /// Lo comparten la configuración de una empresa y la de la plataforma.
    /// </summary>
    public static CredencialesDeListas Combinar(CredencialesDeListas? actuales, CambioDeCredencialesDeListas cambio, Func<string, string> proteger,
        DateTimeOffset ahora)
    {
        var usuario = (cambio.Usuario ?? actuales?.Usuario ?? string.Empty).Trim();
        if (usuario.Length is 0 or > 200)
            throw ReglaDeNegocioException.Validacion("usuario-requerido", "El usuario del proveedor es obligatorio (hasta 200 caracteres).");
        if (cambio.Clave is null && actuales is null)
            throw ReglaDeNegocioException.Validacion("clave-requerida", "La clave del proveedor es obligatoria.");
        if (cambio.Clave is { } nueva && nueva.Trim().Length is < 4 or > 512)
            throw ReglaDeNegocioException.Validacion("clave-invalida", "La clave del proveedor debe tener entre 4 y 512 caracteres.");
        var url = string.IsNullOrWhiteSpace(cambio.UrlBase) ? null : cambio.UrlBase.Trim();
        if (url is not null && (!Uri.TryCreate(url, UriKind.Absolute, out var uri) || uri.Scheme != Uri.UriSchemeHttps))
            throw ReglaDeNegocioException.Validacion("url-invalida", "La URL del proveedor debe ser https.");
        if (cambio.CuotaMensual is < 1 or > ListasRestrictivasEmpresa.MaximoCuota)
            throw ReglaDeNegocioException.Validacion("cuota-invalida",
                $"La cuota mensual va de 1 a {ListasRestrictivasEmpresa.MaximoCuota} búsquedas (vacía = sin tope).");
        var plan = string.IsNullOrWhiteSpace(cambio.Plan) ? null : cambio.Plan.Trim();
        if (plan is { Length: > 100 })
            throw ReglaDeNegocioException.Validacion("plan-invalido", "El nombre del plan admite hasta 100 caracteres.");

        var credenciales = actuales ?? new CredencialesDeListas();
        credenciales.Usuario = usuario;
        if (cambio.Clave is { } claveNueva)
        {
            var limpia = claveNueva.Trim();
            credenciales.ClaveProtegida = proteger(limpia);
            credenciales.UltimosCuatroClave = limpia[^4..];
        }

        credenciales.UrlBase = url;
        credenciales.Plan = plan;
        credenciales.CuotaMensual = cambio.CuotaMensual;
        credenciales.ModoBusqueda = cambio.ModoBusqueda ?? credenciales.ModoBusqueda;
        credenciales.ActualizadasEn = ahora;
        return credenciales;
    }
}

/// <summary>Cambio de credenciales pedido por el administrador. <see cref="Clave"/>: nula conserva, vacía quita las credenciales.</summary>
public sealed record CambioDeCredencialesDeListas(string Proveedor, string? Usuario, string? Clave, string? UrlBase, string? Plan, int? CuotaMensual,
    ModoBusquedaListas? ModoBusqueda);

/// <summary>
/// Listas restrictivas de una empresa (VE-7): qué proveedor consulta y con qué credenciales (las de su contrato, cada empresa
/// con las suyas, P-9-3). Esquema <c>validacion</c>, multi-tenant; id = empresa. Proveedor nulo = el de la instalación.
/// </summary>
public sealed class ListasRestrictivasEmpresa
{
    public const int MaximoCuota = 1_000_000;

    public Guid Id { get; set; }

    public string? Proveedor { get; set; }

    public Dictionary<string, CredencialesDeListas> Credenciales { get; set; } = [];

    public DateTimeOffset ActualizadaEn { get; set; }

    public Guid EmpresaId => Id;

    public static ListasRestrictivasEmpresa Nueva(Guid empresaId) => new() { Id = empresaId };

    public CredencialesDeListas? CredencialesDe(string proveedor) => Credenciales.GetValueOrDefault(proveedor);

    /// <summary>
    /// Fija el proveedor (nulo = el de la instalación). 422 <c>proveedor-invalido</c>; con un proveedor real sin credenciales,
    /// <c>credenciales-requeridas</c>.
    /// </summary>
    public void ElegirProveedor(string? proveedor, DateTimeOffset ahora, bool cuentaDeLaPlataforma = false)
    {
        if (proveedor is null)
        {
            Proveedor = null;
            ActualizadaEn = ahora;
            return;
        }

        var nombre = ProveedoresDeListas.Normalizar(proveedor)
                     ?? throw ReglaDeNegocioException.Validacion("proveedor-invalido",
                         $"'{proveedor}' no es un proveedor de listas ({string.Join(", ", ProveedoresDeListas.Todos)}).");
        // El proveedor de la plataforma no lleva credenciales de la empresa: basta con que la plataforma haya configurado su cuenta (G-2).
        if (ProveedoresDeListas.EsDeLaPlataforma(nombre))
        {
            if (!cuentaDeLaPlataforma)
                throw ReglaDeNegocioException.Validacion("credenciales-requeridas", $"La plataforma aún no configuró la cuenta de {nombre}.");
        }
        else if (ProveedoresDeListas.Reales.Contains(nombre) && CredencialesDe(nombre) is null)
            throw ReglaDeNegocioException.Validacion("credenciales-requeridas", $"Configura primero el usuario y la clave de {nombre}.");
        Proveedor = nombre;
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Crea, cambia o quita las credenciales de un proveedor real. 422 <c>proveedor-invalido</c>, <c>usuario-requerido</c>,
    /// <c>clave-requerida</c>, <c>clave-invalida</c>, <c>url-invalida</c> (https), <c>cuota-invalida</c>. Quitar las del proveedor
    /// elegido deja la empresa con el de la instalación. <paramref name="proteger"/> cifra la clave nueva.
    /// </summary>
    public void CambiarCredenciales(CambioDeCredencialesDeListas cambio, Func<string, string> proteger, DateTimeOffset ahora)
    {
        var nombre = ProveedoresDeListas.Normalizar(cambio.Proveedor);
        if (nombre is null || !ProveedoresDeListas.Reales.Contains(nombre))
            throw ReglaDeNegocioException.Validacion("proveedor-invalido",
                $"Solo los proveedores reales llevan credenciales ({string.Join(", ", ProveedoresDeListas.Reales)}).");
        if (ProveedoresDeListas.EsDeLaPlataforma(nombre))
            throw ReglaDeNegocioException.Validacion("credenciales-de-plataforma",
                $"Las credenciales de {nombre} las administra la plataforma: no se cargan por empresa.");

        if (cambio.Clave is { } clave && clave.Trim().Length == 0)
        {
            Credenciales.Remove(nombre);
            if (Proveedor == nombre)
                Proveedor = null;
            ActualizadaEn = ahora;
            return;
        }

        Credenciales[nombre] = CredencialesDeListas.Combinar(CredencialesDe(nombre), cambio, proteger, ahora);
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Quita las credenciales de los proveedores que ahora son de la plataforma (G-2, migración): lo que la empresa había cargado antes de que
    /// Infolaft pasara a la cuenta única. Devuelve lo quitado. Conserva el proveedor elegido.
    /// </summary>
    public IReadOnlyDictionary<string, CredencialesDeListas> QuitarCredencialesDeLaPlataforma(DateTimeOffset ahora)
    {
        var quitadas = Credenciales.Where(c => ProveedoresDeListas.EsDeLaPlataforma(c.Key)).ToDictionary(c => c.Key, c => c.Value);
        foreach (var nombre in quitadas.Keys)
            Credenciales.Remove(nombre);
        if (quitadas.Count > 0)
            ActualizadaEn = ahora;
        return quitadas;
    }
}

/// <summary>
/// Cuenta de la plataforma con los proveedores de listas restrictivas que contrata una sola vez para todos los clientes (G-2, PP-5): hoy,
/// Infolaft. La configura Plataforma desde su consola; la clave solo se guarda cifrada con la clave maestra (<c>IProtectorDeSecretos.Proteger</c>)
/// y nunca sale. Las empresas no ven ni cargan estas credenciales: consultan con ellas y cada una conserva su consumo mensual (VE-11) para
/// atribuir el costo. <see cref="CredencialesDeListas.CuotaMensual"/> es la cuota de búsquedas al mes de <b>cada empresa</b> (nulo = sin tope).
/// Un solo documento (id <see cref="IdUnico"/>) en el esquema <c>plataforma</c>.
/// </summary>
public sealed class ListasRestrictivasDePlataforma
{
    public const string IdUnico = "listas-restrictivas";

    public string Id { get; set; } = IdUnico;

    public Dictionary<string, CredencialesDeListas> Credenciales { get; set; } = [];

    public DateTimeOffset ActualizadaEn { get; set; }

    /// <summary>Quién las cambió por última vez (el operador de Plataforma); nunca la clave.</summary>
    public string? ActualizadaPor { get; set; }

    public CredencialesDeListas? CredencialesDe(string proveedor) => Credenciales.GetValueOrDefault(proveedor);

    /// <summary>
    /// Crea, cambia o quita (<c>Clave</c> vacía) la cuenta de un proveedor de la plataforma. 422 <c>proveedor-invalido</c> si no es de la
    /// plataforma; el resto, como <see cref="CredencialesDeListas.Combinar"/>.
    /// </summary>
    public void CambiarCredenciales(CambioDeCredencialesDeListas cambio, Func<string, string> proteger, string actor, DateTimeOffset ahora)
    {
        var nombre = ProveedoresDeListas.Normalizar(cambio.Proveedor);
        if (nombre is null || !ProveedoresDeListas.EsDeLaPlataforma(nombre))
            throw ReglaDeNegocioException.Validacion("proveedor-invalido",
                $"Solo {string.Join(", ", ProveedoresDeListas.DeLaPlataforma)} es de la plataforma; los demás se configuran por empresa.");

        if (cambio.Clave is { } clave && clave.Trim().Length == 0)
            Credenciales.Remove(nombre);
        else
            Credenciales[nombre] = CredencialesDeListas.Combinar(CredencialesDe(nombre), cambio, proteger, ahora);
        ActualizadaPor = actor;
        ActualizadaEn = ahora;
    }

    /// <summary>
    /// Migración (G-2): la plataforma toma las credenciales de una empresa como su cuenta, con la clave ya cifrada con la clave maestra. Solo si
    /// la plataforma aún no tiene la de ese proveedor; devuelve si las tomó.
    /// </summary>
    public bool TomarCredencialesDeEmpresa(string proveedor, CredencialesDeListas deLaEmpresa, string claveProtegidaDeLaPlataforma, string actor,
        DateTimeOffset ahora)
    {
        var nombre = ProveedoresDeListas.Normalizar(proveedor);
        if (nombre is null || !ProveedoresDeListas.EsDeLaPlataforma(nombre) || CredencialesDe(nombre) is not null)
            return false;

        Credenciales[nombre] = new CredencialesDeListas
        {
            Usuario = deLaEmpresa.Usuario,
            ClaveProtegida = claveProtegidaDeLaPlataforma,
            UltimosCuatroClave = deLaEmpresa.UltimosCuatroClave,
            UrlBase = deLaEmpresa.UrlBase,
            Plan = deLaEmpresa.Plan,
            // La cuota que tenía la empresa no se hereda: es la cuota de cada empresa y la fija Plataforma (nulo = sin tope).
            CuotaMensual = null,
            ModoBusqueda = deLaEmpresa.ModoBusqueda,
            ActualizadasEn = ahora
        };
        ActualizadaPor = actor;
        ActualizadaEn = ahora;
        return true;
    }
}

/// <summary>
/// Consumo de listas de una empresa con un proveedor en un mes (VE-11): consultas (una por verificación o reconsulta) y
/// búsquedas (una por persona consultada, lo que factura el proveedor). Idempotente por referencia de la consulta: un
/// reintento no se cuenta dos veces. Esquema <c>validacion</c>, multi-tenant, concurrencia optimista; id
/// <c>{empresa:N}:{proveedor}:{yyyy-MM}</c>.
/// </summary>
public sealed class UsoMensualListas
{
    public string Id { get; set; } = string.Empty;

    public Guid EmpresaId { get; set; }

    public string Proveedor { get; set; } = string.Empty;

    public string Mes { get; set; } = string.Empty;

    public int Consultas { get; set; }

    public int Busquedas { get; set; }

    public int ConCoincidencias { get; set; }

    public List<string> Referencias { get; set; } = [];

    public static string Clave(Guid empresaId, string proveedor, string mes) => $"{empresaId:N}:{proveedor}:{mes}";

    public static string MesDe(DateOnly dia) => dia.ToString("yyyy-MM", CultureInfo.InvariantCulture);

    public static UsoMensualListas Nuevo(Guid empresaId, string proveedor, string mes) =>
        new() { Id = Clave(empresaId, proveedor, mes), EmpresaId = empresaId, Proveedor = proveedor, Mes = mes };

    /// <summary>Suma una consulta; devuelve falso si esa referencia ya estaba contada.</summary>
    public bool Registrar(string referencia, int busquedas, bool conCoincidencias)
    {
        if (Referencias.Contains(referencia))
            return false;
        Referencias.Add(referencia);
        Consultas++;
        Busquedas += busquedas;
        if (conCoincidencias)
            ConCoincidencias++;
        return true;
    }

    /// <summary>¿Cabe una consulta de <paramref name="busquedas"/> búsquedas en la cuota?</summary>
    public bool Cabe(int? cuota, int busquedas) => cuota is not { } tope || Busquedas + busquedas <= tope;
}
