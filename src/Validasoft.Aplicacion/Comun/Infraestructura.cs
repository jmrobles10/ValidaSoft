using Marten;
using Wolverine;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;

namespace Validasoft.Aplicacion.Comun;

/// <summary>Quién hace la petición y en nombre de qué tenant y empresa (lo llena el pipeline HTTP).</summary>
public interface IContextoSolicitud
{
    Guid? TenantId { get; }

    Guid? EmpresaId { get; }

    ActorTipo ActorTipo { get; }

    string ActorId { get; }

    string CorrelationId { get; }

    /// <summary>Código del sistema integrado de la credencial (SRM, MARCO…); nulo en llaves de administración.</summary>
    string? Sistema { get; }

    IReadOnlySet<string> Scopes { get; }

    /// <summary>Empresas permitidas por la credencial; contiene <c>*</c> si son todas.</summary>
    IReadOnlySet<string> Empresas { get; }

    Guid TenantRequerido() => TenantId
        ?? throw new ReglaDeNegocioException(TipoFalla.Prohibido, "tenant-requerido", "Esta operación requiere una credencial de un tenant.");

    Guid EmpresaRequerida() => EmpresaId
        ?? throw ReglaDeNegocioException.Validacion("empresa-requerida", "Indica la empresa con la cabecera X-Empresa.");
}

/// <summary>Emite la sesión corta de Validasoft para las pantallas embebidas (ADR-0011, ID-3).</summary>
public interface IEmisorDeSesiones
{
    /// <summary>Solo a partir de una petición con API key + <c>X-Usuario-Delegado</c> ya verificada.</summary>
    Contratos.SesionUsuarioDto Emitir(System.Security.Claims.ClaimsPrincipal principal, DateTimeOffset ahora);

    /// <summary>Sesión de la consola de Plataforma para un operador ya verificado (PT-4): sin tenant, solo <c>plataforma:tenants</c>.</summary>
    Contratos.SesionUsuarioDto EmitirPlataforma(OperadorDePlataforma operador, DateTimeOffset ahora);

    /// <summary>
    /// Token de acceso de un usuario que entró directo (ING-1): los mismos reclamos que la sesión delegada (tenant, scopes efectivos de sus
    /// roles, empresas) más la familia de su sesión y cómo se autenticó. Vence en minutos; se renueva con la cookie de refresco.
    /// </summary>
    Contratos.SesionUsuarioDto EmitirDeUsuario(Usuario usuario, Guid tenantId, Guid sesionId, MetodoDeIngreso metodo, TimeSpan vigencia, DateTimeOffset ahora);

    /// <summary>Token de acceso de un operador de Plataforma de ingreso directo (ING-4): sin tenant, solo <c>plataforma:tenants</c>.</summary>
    Contratos.SesionUsuarioDto EmitirDeOperador(OperadorDePlataforma operador, Guid sesionId, MetodoDeIngreso metodo, TimeSpan vigencia, DateTimeOffset ahora);
}

/// <summary>
/// Verifica la aserción <c>X-Usuario-Delegado</c> de un operador de Plataforma contra la clave de su sistema de plataforma
/// (<c>iss</c>), la marca como usada (un solo uso) y registra o actualiza al operador (PT-4).
/// </summary>
public interface IVerificadorDeOperadores
{
    Task<OperadorDePlataforma> VerificarAsync(string asercion, string correlationId, CancellationToken ct);
}

/// <summary>
/// Abre sesiones de Marten con el tenant correcto y los metadatos de actor y correlación. Las variantes con
/// <see cref="IMessageBus"/> enlistan la sesión en el outbox de Wolverine: lo que se publique en ese bus se guarda en la
/// misma transacción y se despacha solo después de confirmarla (correos, IN-2).
/// </summary>
public interface ISesiones
{
    /// <summary>Sesión de escritura del tenant de la credencial (eventos y documentos <c>validacion</c>).</summary>
    IDocumentSession DelTenant();

    /// <summary>Como <see cref="DelTenant()"/>, enlistada en el outbox del bus de la petición.</summary>
    IDocumentSession DelTenant(IMessageBus mensajes);

    /// <summary>Sesión de escritura para un tenant explícito (alta de tenant desde Plataforma).</summary>
    IDocumentSession ParaTenant(Guid tenantId);

    /// <summary>Sesión de escritura de un tenant explícito; con <paramref name="mensajes"/>, enlistada en el outbox.</summary>
    IDocumentSession ParaTenant(Guid tenantId, IMessageBus? mensajes);

    /// <summary>Escritura de documentos globales (<c>plataforma</c>, <c>boveda</c>, <c>identidad</c>) sin tenant.</summary>
    IDocumentSession Global(IMessageBus? mensajes = null);

    /// <summary>Lectura de documentos globales (<c>plataforma</c>, <c>boveda</c>).</summary>
    IQuerySession ConsultaGlobal();

    /// <summary>
    /// Escritura en el tenant técnico <c>boveda</c> (streams <c>Documento-*</c>, T-2); con <paramref name="mensajes"/>,
    /// enlistada en el outbox.
    /// </summary>
    IDocumentSession Boveda(IMessageBus? mensajes = null);

    /// <summary>Lectura del tenant técnico <c>boveda</c> (documentos de la bóveda).</summary>
    IQuerySession ConsultaBoveda();
}

/// <summary>Tenants técnicos del único <c>DocumentStore</c> (T-2): los streams fuera de <c>validacion</c> no usan un tenant real.</summary>
public static class TenantsTecnicos
{
    /// <summary>Bóveda global de los terceros: <c>Documento-{id}</c> de ambos ámbitos (el <c>PorVinculo</c> guarda su tenant como dato).</summary>
    public const string Boveda = "boveda";
}

/// <summary>
/// Cifra secretos: los de corta vida que viajan en el outbox hasta el correo (token del enlace, código OTP, IN-3) y los
/// que quedan en reposo (llave de OpenAI, secretos de webhook, números de cuenta). Cifra con la clave maestra vigente y
/// descifra con la que indique el texto cifrado (PT-1).
/// </summary>
public interface IProtectorDeSecretos
{
    string Proteger(string secreto);

    string Desproteger(string protegido);

    /// <summary>Identificador (kid) de la clave maestra vigente: va en cada texto cifrado.</summary>
    string ClaveVigente { get; }

    /// <summary>Identificadores de las claves anteriores configuradas (solo descifran).</summary>
    IReadOnlyList<string> ClavesAnteriores { get; }

    /// <summary>El texto ya está cifrado con la clave maestra vigente (no hace falta re-cifrarlo).</summary>
    bool EstaConClaveActual(string protegido);

    /// <summary>El mismo secreto cifrado con la clave vigente; si ya lo está, lo devuelve tal cual.</summary>
    string Recifrar(string protegido) => EstaConClaveActual(protegido) ? protegido : Proteger(Desproteger(protegido));
}

/// <summary>
/// Caché de verificaciones de API keys (para no pagar Argon2 en cada petición). Al revocar o rotar una llave se olvida su
/// verificación en este nodo, así el cambio vale de inmediato; en los demás nodos, como mucho 5 minutos después (CN-3).
/// </summary>
public interface ICacheDeCredenciales
{
    void Olvidar(Guid apiKeyId);
}

/// <summary>Genera y verifica API keys; el algoritmo (Argon2id) vive en infraestructura.</summary>
public interface IGeneradorDeApiKeys
{
    (ApiKey Llave, string Valor) Generar(string slugTenant, string nombre, IEnumerable<string> scopes,
        IEnumerable<string> empresas, DateTimeOffset? expiraEn, bool sandbox, DateTimeOffset ahora);
}
