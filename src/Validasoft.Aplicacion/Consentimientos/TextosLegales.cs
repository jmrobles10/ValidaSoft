using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Configuracion;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Consentimientos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Consentimientos;

/// <summary>Consultas de textos legales vigentes (la versión más alta de cada serie).</summary>
public static class TextosLegales
{
    public static readonly TipoTextoLegal[] DeEmpresa = [TipoTextoLegal.AutorizacionEmpresa, TipoTextoLegal.TerminosEmpresa];

    public static Task<TextoLegal?> VigenteAsync(IQuerySession sesion, TipoTextoLegal tipo, Guid? empresaId, CancellationToken ct)
    {
        var serie = TextoLegal.SerieDe(tipo, empresaId);
        return sesion.Query<TextoLegal>().Where(t => t.Serie == serie).OrderByDescending(t => t.Version).FirstOrDefaultAsync(ct);
    }

    /// <summary>Autorización y términos vigentes de la empresa (los que existan).</summary>
    public static async Task<IReadOnlyList<TextoLegal>> VigentesDeEmpresaAsync(IQuerySession sesion, Guid empresaId, CancellationToken ct)
    {
        var vigentes = new List<TextoLegal>();
        foreach (var tipo in DeEmpresa)
        {
            if (await VigenteAsync(sesion, tipo, empresaId, ct) is { } texto)
                vigentes.Add(texto);
        }

        return vigentes;
    }

    public static IReadOnlyList<string> Faltantes(IReadOnlyList<TextoLegal> vigentesDeEmpresa) =>
        [.. DeEmpresa.Where(tipo => vigentesDeEmpresa.All(t => t.Tipo != tipo)).Select(t => t.ToString())];

    /// <summary>Acepta el nombre del enum o el alias corto de la ruta (<c>autorizacion</c>, <c>terminos</c>).</summary>
    public static TipoTextoLegal TipoDeEmpresa(string? tipo) => (tipo ?? string.Empty).Trim().ToLowerInvariant() switch
    {
        "autorizacion" or "autorizacionempresa" => TipoTextoLegal.AutorizacionEmpresa,
        "terminos" or "terminosempresa" => TipoTextoLegal.TerminosEmpresa,
        _ => throw ReglaDeNegocioException.Validacion("tipo-texto-invalido",
            $"'{tipo}' no es un texto legal de empresa (AutorizacionEmpresa o TerminosEmpresa).")
    };

    /// <summary>
    /// Aviso inicial de la plataforma. <b>Texto provisional</b>: Legal debe reemplazarlo publicando una versión nueva
    /// (<c>PUT /api/v1/plataforma/textos-legales/aviso</c>).
    /// </summary>
    public const string AvisoPlataformaProvisional =
        "[TEXTO PROVISIONAL — pendiente de revisión por el área Legal; no usar en producción]\n\n" +
        "Aviso de tratamiento de datos personales de la plataforma Validasoft.\n\n" +
        "Validasoft actúa como encargado del tratamiento de los datos y documentos que cargas, por cuenta de cada empresa " +
        "con la que aceptes vincularte, que es la responsable (Ley 1581 de 2012 y Decreto 1377 de 2013). Tus documentos se " +
        "guardan una sola vez en tu bóveda y solo se comparten con las empresas cuya invitación aceptes.\n\n" +
        "Procesamiento con inteligencia artificial: para leer los hechos de tus documentos (tipo, fechas, legibilidad, " +
        "coincidencias) la plataforma puede enviarlos a un proveedor externo de inteligencia artificial, que no los usa para " +
        "entrenar modelos. Las decisiones sobre tu vínculo las toma cada empresa; puedes pedir revisión humana.\n\n" +
        "Transferencia internacional: ese proveedor puede procesar la información en servidores ubicados fuera de Colombia, " +
        "con garantías contractuales de protección equivalentes.\n\n" +
        "Puedes conocer, actualizar, rectificar y solicitar la supresión de tus datos desde tu bóveda.";
}

/// <summary>Textos legales versionados: aviso de la plataforma y autorización/términos de cada empresa.</summary>
public static class TextosLegalesEndpoints
{
    [WolverinePut("/api/v1/plataforma/textos-legales/aviso")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<TextoLegalDto> PublicarAviso(PublicarTextoLegalDto dto, ISesiones sesiones, IContextoSolicitud contexto,
        TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        var vigente = await TextosLegales.VigenteAsync(sesion, TipoTextoLegal.AvisoPlataforma, null, ct);
        var nuevo = TextoLegal.Publicar(TipoTextoLegal.AvisoPlataforma, null, null, dto.Contenido, vigente, contexto.ActorId, ahora);

        sesion.Insert(nuevo);
        Auditoria.Registrar(sesion, contexto, "TextoLegal", nuevo.Id.ToString(), AccionConfiguracion.Publicado, vigente?.ADto(), nuevo.ADto(), ahora);
        await sesion.SaveChangesAsync(ct);
        return nuevo.ADto();
    }

    [WolverineGet("/api/v1/plataforma/textos-legales/aviso")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<TextoLegalDto> ObtenerAviso(ISesiones sesiones, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return (await TextosLegales.VigenteAsync(consulta, TipoTextoLegal.AvisoPlataforma, null, ct)
                ?? throw ReglaDeNegocioException.NoEncontrado("texto-legal-no-encontrado", "Aún no hay aviso de la plataforma.")).ADto();
    }

    /// <summary><c>{tipo}</c>: <c>autorizacion</c> | <c>terminos</c> (o el nombre del enum). Cada publicación es una versión nueva.</summary>
    [WolverinePut("/api/v1/empresas/{id}/textos-legales/{tipo}")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<TextoLegalDto> PublicarDeEmpresa(Guid id, string tipo, PublicarTextoLegalDto dto, ISesiones sesiones,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var tipoTexto = TextosLegales.TipoDeEmpresa(tipo);
        var empresa = await EmpresasEndpoints.CargarAsync(id, sesiones, contexto, ct);
        var ahora = reloj.GetUtcNow();

        await using var sesion = sesiones.DelTenant();
        var vigente = await TextosLegales.VigenteAsync(sesion, tipoTexto, empresa.Id, ct);
        var nuevo = TextoLegal.Publicar(tipoTexto, empresa.TenantId, empresa.Id, dto.Contenido, vigente, contexto.ActorId, ahora);

        sesion.Insert(nuevo);
        Auditoria.Registrar(sesion, contexto, "TextoLegal", nuevo.Id.ToString(), AccionConfiguracion.Publicado, vigente?.ADto(), nuevo.ADto(),
            ahora, empresa.Id);
        await sesion.SaveChangesAsync(ct);
        return nuevo.ADto();
    }

    [WolverineGet("/api/v1/empresas/{id}/textos-legales")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<TextosLegalesEmpresaDto> ListarDeEmpresa(Guid id, ISesiones sesiones, IContextoSolicitud contexto, CancellationToken ct)
    {
        var empresa = await EmpresasEndpoints.CargarAsync(id, sesiones, contexto, ct);
        await using var consulta = sesiones.ConsultaGlobal();
        var vigentes = await TextosLegales.VigentesDeEmpresaAsync(consulta, empresa.Id, ct);
        return new TextosLegalesEmpresaDto(empresa.Id, [.. vigentes.Select(t => t.ADto())], TextosLegales.Faltantes(vigentes));
    }
}

/// <summary>
/// Al arrancar, siembra la versión 1 del aviso de la plataforma si no existe (texto provisional). Si el esquema aún no
/// está creado (entornos sin creación automática), solo lo advierte: se siembra en el siguiente arranque.
/// </summary>
public sealed class SembradoDeTextosLegales(IDocumentStore store, TimeProvider reloj, ILogger<SembradoDeTextosLegales> logger) : IHostedService
{
    public async Task StartAsync(CancellationToken cancellationToken)
    {
        try
        {
            await using var sesion = store.LightweightSession();
            if (await TextosLegales.VigenteAsync(sesion, TipoTextoLegal.AvisoPlataforma, null, cancellationToken) is not null)
                return;

            var aviso = TextoLegal.Publicar(TipoTextoLegal.AvisoPlataforma, null, null, TextosLegales.AvisoPlataformaProvisional, null,
                "sistema-siembra", reloj.GetUtcNow());
            sesion.Insert(aviso);
            await sesion.SaveChangesAsync(cancellationToken);
            logger.LogWarning("Se sembró el aviso de la plataforma v1 con texto PROVISIONAL; Legal debe publicar el definitivo.");
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            // Otra instancia lo sembró al mismo tiempo (índice único) o el esquema no existe todavía.
            logger.LogWarning(e, "No se pudo sembrar el aviso de la plataforma.");
        }
    }

    public Task StopAsync(CancellationToken cancellationToken) => Task.CompletedTask;
}
