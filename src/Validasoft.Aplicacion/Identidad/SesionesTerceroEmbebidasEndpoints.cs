using System.Globalization;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Identidad;

/// <summary>
/// Embebido genérico del tercero (IO-5, docs/06 §3.2 e integracion.md §8.1): cualquier sistema integrador con permiso puede abrir una sesión corta
/// del tercero para montar su bóveda, su expediente o su ficha en un iframe, sin que el núcleo sepa quién es el sistema. El
/// intercambio es de backend a backend (API key del sistema + <c>sesiones-tercero:crear</c>); el token resultante, de pocos minutos,
/// queda ligado al tercero, a un solo vínculo, al sistema, al origen exacto de la página que aloja el iframe y al componente. Se
/// entrega al iframe por <c>postMessage</c> (nunca en la URL) y la app embebida solo lo acepta del origen que el token dice.
/// </summary>
public static class SesionesTerceroEmbebidasEndpoints
{
    public static readonly TimeSpan VigenciaPorOmision = TimeSpan.FromMinutes(10);

    public static readonly TimeSpan VigenciaMaxima = TimeSpan.FromMinutes(15);

    /// <summary>
    /// Abre la sesión (201). 403 <c>sistema-requerido</c> (la llave no es de un sistema integrador), <c>sistema-inactivo</c>,
    /// <c>origen-no-permitido</c> (el origen no está entre los del sistema) y <c>vinculo-ajeno</c> (el vínculo no lleva la referencia del
    /// sistema); 404 si el vínculo no es de la empresa; 409 <c>invitacion-no-aceptada</c> y <c>vinculo-anulado</c>; 422
    /// <c>componente-invalido</c>, <c>vigencia-invalida</c> y <c>origen-invalido</c>.
    /// </summary>
    [WolverinePost("/api/v1/sesiones-tercero")]
    [Authorize(Policy = Permisos.SesionesTerceroCrear)]
    public static async Task<IResult> Crear(CrearSesionEmbebidaDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto, IEmisorDeSesionesTercero emisor,
        OrigenesDeEmbebido origenes, IConfiguration configuracion, TimeProvider reloj, CancellationToken ct)
    {
        if (string.IsNullOrWhiteSpace(contexto.Sistema))
            throw ReglaDeNegocioException.Prohibido("sistema-requerido", "Solo la API key de un sistema integrador abre sesiones del tercero.");

        var componente = (dto.Componente ?? string.Empty).Trim().ToLowerInvariant();
        if (!ComponentesEmbebibles.Todos.Contains(componente))
            throw ReglaDeNegocioException.Validacion("componente-invalido", $"El componente debe ser uno de: {string.Join(", ", ComponentesEmbebibles.Todos)}.");

        var vigencia = dto.Minutos is { } minutos ? TimeSpan.FromMinutes(minutos) : VigenciaPorOmision;
        if (vigencia <= TimeSpan.Zero || vigencia > VigenciaMaxima)
            throw ReglaDeNegocioException.Validacion("vigencia-invalida", $"La vigencia va de 1 a {(int)VigenciaMaxima.TotalMinutes} minutos.");

        var origen = OrigenWeb.Normalizar(dto.Origen, origenes.PermiteHttpLocal);
        var codigo = contexto.Sistema;
        var sistema = await Marten.QueryableExtensions.FirstOrDefaultAsync(empresa.Sesion.Query<SistemaIntegrado>().Where(s => s.Codigo == codigo), ct);
        if (sistema is not { Activo: true })
            throw ReglaDeNegocioException.Prohibido("sistema-inactivo", "El sistema integrador de la llave no existe o está desactivado.");
        if (!sistema.OrigenesPermitidos.Contains(origen, StringComparer.Ordinal))
            throw ReglaDeNegocioException.Prohibido("origen-no-permitido", $"El origen {origen} no está entre los permitidos del sistema {sistema.Codigo}.");

        var vinculo = await empresa.CargarVinculoAsync(dto.VinculoId, ct);
        if (!vinculo.Referencias.Any(r => string.Equals(r.Sistema, sistema.Codigo, StringComparison.OrdinalIgnoreCase)))
            throw ReglaDeNegocioException.Prohibido("vinculo-ajeno", $"El vínculo no lleva la referencia del sistema {sistema.Codigo}: solo se embebe lo que el sistema integra.");
        if (vinculo.Estado == EstadoVinculo.Anulado)
            throw ReglaDeNegocioException.Conflicto("vinculo-anulado", "Un vínculo anulado no se embebe.");
        if (!vinculo.InvitacionAceptada)
            throw ReglaDeNegocioException.Conflicto("invitacion-no-aceptada", "El tercero aún no aceptó la invitación de la empresa.");

        // Quien abre la sesión es el sistema que ya autenticó a la persona: equivale a la verificación de identidad (OTP) del enlace.
        var (token, expira) = emisor.Emitir(vinculo.TerceroId, vinculo.CorreoTercero, otpVerificado: true,
            [new VinculoEnSesion(vinculo.Id, contexto.TenantRequerido(), vinculo.EmpresaId)], reloj.GetUtcNow(),
            new EmbebidoDeSesion(sistema.Codigo, origen, componente, vigencia, sistema.Generacion));

        var url = $"{(configuracion["Validasoft:Correo:UrlPublica"] ?? string.Empty).TrimEnd('/')}/embed/{componente}";
        return Results.Created(url, new SesionEmbebidaDto(token, expira, url, origen, componente, vinculo.Id, vinculo.TerceroId));
    }
}

/// <summary>Lo que la app del tercero necesita al arrancar con un token que le entregaron (no con la cookie): quién es y qué vínculos ve.</summary>
public static class SesionActualDelTerceroEndpoints
{
    /// <summary>La sesión del token recibido (los vínculos con su empresa y estado). La usa la app embebida tras recibir el token por <c>postMessage</c>.</summary>
    [WolverineGet("/public/v1/sesion/actual")]
    [Authorize(Policy = ReclamosTercero.Politica)]
    public static async Task<SesionTerceroDto> Actual(HttpContext http, ISesiones sesiones, CancellationToken ct)
    {
        var tercero = SesionDeTercero.De(http.User);
        var autorizacion = http.Request.Headers.Authorization.ToString();
        var token = autorizacion.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase) ? autorizacion["Bearer ".Length..].Trim() : string.Empty;
        var expira = long.TryParse(http.User.FindFirstValue("exp"), NumberStyles.None, CultureInfo.InvariantCulture, out var segundos)
            ? DateTimeOffset.FromUnixTimeSeconds(segundos)
            : DateTimeOffset.UtcNow;

        await using var consulta = sesiones.ConsultaGlobal();
        var vistos = await VistaDelTercero.CargarAsync(consulta, tercero, [.. tercero.Vinculos.Select(v => v.VinculoId)], ct);
        return new SesionTerceroDto(token, expira, tercero.TerceroId, tercero.OtpVerificado,
            [.. vistos.Select(v => new VinculoDeSesionDto(v.Vinculo.Id, v.Empresa.APublica(v.Vinculo.EmpresaId), v.Vinculo.Estado.ToString()))]);
    }
}
