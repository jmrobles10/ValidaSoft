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
}

/// <summary>Cifra secretos de corta vida (token del enlace, código OTP) que viajan en el outbox hasta el correo (IN-3).</summary>
public interface IProtectorDeSecretos
{
    string Proteger(string secreto);

    string Desproteger(string protegido);
}

/// <summary>Genera y verifica API keys; el algoritmo (Argon2id) vive en infraestructura.</summary>
public interface IGeneradorDeApiKeys
{
    (ApiKey Llave, string Valor) Generar(string slugTenant, string nombre, IEnumerable<string> scopes,
        IEnumerable<string> empresas, DateTimeOffset? expiraEn, bool sandbox, DateTimeOffset ahora);
}
