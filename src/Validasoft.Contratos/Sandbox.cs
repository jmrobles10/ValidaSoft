namespace Validasoft.Contratos;

/// <summary>Empresa del sandbox (SB-3): la copia de pruebas de una empresa real. Con una llave de prueba, <c>X-Empresa</c> es <see cref="Id"/>.</summary>
public sealed record EmpresaDeSandboxDto(Guid Id, Guid ProduccionId, string Nombre, string PrefijoFolio, bool Activa);

/// <summary>
/// Estado del sandbox del grupo (<c>GET /api/v1/sandbox</c>, SB-6). <see cref="Entorno"/> es el de la credencial que pregunta
/// (<c>produccion</c> o <c>sandbox</c>). Sin sandbox (<see cref="Existe"/> falso) se crea con <c>POST /api/v1/sandbox</c> o al emitir
/// la primera API key de prueba.
/// </summary>
public sealed record SandboxDto(
    bool Existe, string Entorno, Guid? TenantId, DateTimeOffset? CreadoEn, DateTimeOffset? ReiniciadoEn,
    IReadOnlyList<EmpresaDeSandboxDto> Empresas, IReadOnlyList<string> Sistemas, int Correos);

/// <summary>
/// Correo que Validasoft habría enviado desde el sandbox, retenido en el buzón de pruebas (SB-4): el destinatario, el asunto, el
/// texto, el HTML y los enlaces que trae (el de acceso del tercero, el de la app) para seguir el flujo en una prueba.
/// </summary>
public sealed record CorreoDePruebaDto(
    Guid Id, string Para, string Asunto, string Texto, string Html, IReadOnlyList<string> Enlaces, DateTimeOffset RecibidoEn);
