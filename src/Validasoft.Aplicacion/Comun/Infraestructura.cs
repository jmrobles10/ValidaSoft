using Marten;
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

    IReadOnlySet<string> Scopes { get; }

    /// <summary>Empresas permitidas por la credencial; contiene <c>*</c> si son todas.</summary>
    IReadOnlySet<string> Empresas { get; }

    Guid TenantRequerido() => TenantId
        ?? throw new ReglaDeNegocioException(TipoFalla.Prohibido, "tenant-requerido", "Esta operación requiere una credencial de un tenant.");

    Guid EmpresaRequerida() => EmpresaId
        ?? throw ReglaDeNegocioException.Validacion("empresa-requerida", "Indica la empresa con la cabecera X-Empresa.");
}

/// <summary>Abre sesiones de Marten con el tenant correcto y los metadatos de actor y correlación.</summary>
public interface ISesiones
{
    /// <summary>Sesión de escritura del tenant de la credencial (eventos y documentos <c>validacion</c>).</summary>
    IDocumentSession DelTenant();

    /// <summary>Sesión de escritura para un tenant explícito (alta de tenant desde Plataforma).</summary>
    IDocumentSession ParaTenant(Guid tenantId);

    /// <summary>Lectura de documentos globales (<c>plataforma</c>, <c>boveda</c>).</summary>
    IQuerySession ConsultaGlobal();
}

/// <summary>Genera y verifica API keys; el algoritmo (Argon2id) vive en infraestructura.</summary>
public interface IGeneradorDeApiKeys
{
    (ApiKey Llave, string Valor) Generar(string slugTenant, string nombre, IEnumerable<string> scopes,
        IEnumerable<string> empresas, DateTimeOffset? expiraEn, bool sandbox, DateTimeOffset ahora);
}
