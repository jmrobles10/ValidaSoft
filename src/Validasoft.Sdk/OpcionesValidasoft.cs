namespace Validasoft.Sdk;

/// <summary>
/// Configuración del cliente. La API key fija el tenant (y el sistema integrado, si es la llave de uno); la empresa por
/// defecto viaja en <c>X-Empresa</c> en cada llamada (una instancia del SRM = una empresa, ADR-0008).
/// </summary>
public sealed class OpcionesValidasoft
{
    /// <summary>Origen de la API, p. ej. <c>https://valida.acme.co</c> (sin <c>/api/v1</c>).</summary>
    public Uri? UrlBase { get; set; }

    /// <summary>Valor de <c>X-Api-Key</c> (<c>vsk_live_…</c>). Guárdalo en el gestor de secretos, nunca en claro.</summary>
    public string ApiKey { get; set; } = string.Empty;

    /// <summary>Empresa interna por defecto (<c>X-Empresa</c>). <see cref="IClienteValidasoft.ParaEmpresa"/> la cambia.</summary>
    public Guid? EmpresaId { get; set; }

    /// <summary>
    /// Firma las aserciones <c>X-Usuario-Delegado</c> cuando el sistema actúa en nombre de una persona
    /// (<see cref="IClienteValidasoft.ComoUsuario"/>). Necesita la clave privada del sistema integrado.
    /// </summary>
    public FirmanteDeAsercionesDelegadas? Firmante { get; set; }

    /// <summary>Reintentos de las operaciones idempotentes (GET, PUT y POST con <c>Idempotency-Key</c>) ante 5xx, 408, 429 o error de red.</summary>
    public int Reintentos { get; set; } = 2;

    /// <summary>Espera base entre reintentos (se duplica en cada intento).</summary>
    public TimeSpan EsperaEntreReintentos { get; set; } = TimeSpan.FromMilliseconds(300);
}
