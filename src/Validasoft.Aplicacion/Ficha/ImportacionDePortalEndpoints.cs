using System.Globalization;
using System.Text;
using System.Text.Json;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Ficha.Portal;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Ficha;

public enum EstadoCorridaPortal
{
    Encolada,
    EnCurso,
    Completada,
    Fallida
}

public sealed record ConstructoraEmpresa(int Constructora, Guid EmpresaId);

public sealed class ContadorDeSeccion
{
    public int Creadas { get; set; }

    public int SinCambios { get; set; }

    public int Rechazadas { get; set; }
}

/// <summary>
/// Corrida del importador del Portal (esquema <c>plataforma</c>, global). Guarda su configuración, el <see cref="Cursor"/> (el
/// último <c>Terid</c> de un lote terminado: la corrida se reanuda desde ahí), el resumen acumulado y todos los avisos (el
/// CSV de rechazos sale de aquí). Un dry-run también la guarda: es su informe, no un dato importado.
/// </summary>
public sealed class CorridaDeImportacionPortal
{
    public Guid Id { get; set; }

    public EstadoCorridaPortal Estado { get; set; }

    public bool DryRun { get; set; }

    /// <summary><c>Archivo</c> o <c>SqlServer</c>.</summary>
    public string Origen { get; set; } = string.Empty;

    public int TamanoLote { get; set; }

    public bool ImportarCatalogos { get; set; }

    public List<ConstructoraEmpresa> Constructoras { get; set; } = [];

    public int? Cursor { get; set; }

    public int Lotes { get; set; }

    public bool CatalogosListos { get; set; }

    public List<ResultadoImportacionCatalogoDto> Catalogos { get; set; } = [];

    public int TercerosLeidos { get; set; }

    public int TercerosCreados { get; set; }

    public int TercerosEncontrados { get; set; }

    public int TercerosRechazados { get; set; }

    public Dictionary<string, ContadorDeSeccion> Secciones { get; set; } = [];

    public int Aceptaciones { get; set; }

    public int AceptacionesExistentes { get; set; }

    public int SinVinculo { get; set; }

    public List<AvisoImportacionDto> Advertencias { get; set; } = [];

    public List<AvisoImportacionDto> Rechazos { get; set; } = [];

    public string CreadaPor { get; set; } = string.Empty;

    public DateTimeOffset CreadaEn { get; set; }

    public DateTimeOffset ActualizadaEn { get; set; }

    public DateTimeOffset? TerminadaEn { get; set; }

    public string? Error { get; set; }

    /// <summary>Suma lo que pasó con un tercero.</summary>
    public void Sumar(TerceroImportado t)
    {
        TercerosLeidos++;
        if (t.Rechazado)
            TercerosRechazados++;
        else if (t.Creado == true)
            TercerosCreados++;
        else
            TercerosEncontrados++;

        foreach (var s in t.Secciones)
        {
            if (!Secciones.TryGetValue(s.Seccion, out var contador))
                Secciones[s.Seccion] = contador = new ContadorDeSeccion();
            switch (s.Resultado)
            {
                case ResultadoDeSeccionImportada.Creada: contador.Creadas++; break;
                case ResultadoDeSeccionImportada.SinCambios: contador.SinCambios++; break;
                default: contador.Rechazadas++; break;
            }
        }

        Aceptaciones += t.Aceptaciones;
        AceptacionesExistentes += t.AceptacionesExistentes;
        SinVinculo += t.SinVinculo;
        Advertencias.AddRange(t.Advertencias.Select(a => a.ADto(t.Terid)));
        Rechazos.AddRange(t.Rechazos.Select(a => a.ADto(t.Terid)));
    }
}

/// <summary>El JSON de una corrida con origen archivo (aparte, para que el estado no lo cargue en cada lectura).</summary>
public sealed class ArchivoDeCorridaPortal
{
    public Guid Id { get; set; }

    public string Json { get; set; } = string.Empty;
}

/// <summary>Ejecuta (o reanuda desde su cursor) una corrida del importador. Cola local durable y secuencial <c>importacion</c>.</summary>
public sealed record EjecutarImportacionDePortal(Guid CorridaId);

public static class EjecutarImportacionDePortalHandler
{
    public const string Actor = "importador-portal";

    /// <summary>
    /// Idempotente y reanudable: una corrida completada no hace nada; una interrumpida sigue desde su cursor. Tras cada lote
    /// guarda el cursor y el resumen. Un error de un tercero queda como su rechazo y no detiene la corrida; uno general la deja
    /// <c>Fallida</c> (se reintenta y se puede reanudar con <c>POST …/{id}/reanudar</c>).
    /// </summary>
    public static async Task Handle(EjecutarImportacionDePortal mensaje, ISesiones sesiones, IFabricaDeOrigenesPortal fabrica, IWolverineRuntime runtime,
        TimeProvider reloj, ILogger<CorridaDeImportacionPortal> logger, CancellationToken ct)
    {
        var corrida = await CargarAsync(sesiones, mensaje.CorridaId, ct);
        if (corrida is null || corrida.Estado == EstadoCorridaPortal.Completada)
            return;

        corrida.Estado = EstadoCorridaPortal.EnCurso;
        corrida.Error = null;
        await GuardarAsync(sesiones, corrida, reloj, ct);
        try
        {
            await EjecutarAsync(corrida, sesiones, fabrica, runtime, reloj, logger, ct);
            corrida.Estado = EstadoCorridaPortal.Completada;
            corrida.TerminadaEn = reloj.GetUtcNow();
            await GuardarAsync(sesiones, corrida, reloj, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "La corrida {Corrida} del importador del Portal falló en el cursor {Cursor}.", corrida.Id, corrida.Cursor);
            corrida.Estado = EstadoCorridaPortal.Fallida;
            corrida.Error = e.Message;
            await GuardarAsync(sesiones, corrida, reloj, ct);
            throw;
        }
    }

    private static async Task EjecutarAsync(CorridaDeImportacionPortal corrida, ISesiones sesiones, IFabricaDeOrigenesPortal fabrica, IWolverineRuntime runtime,
        TimeProvider reloj, ILogger logger, CancellationToken ct)
    {
        IOrigenPortal origen;
        if (corrida.Origen == ImportacionDePortalEndpoints.OrigenArchivo)
        {
            await using var consulta = sesiones.ConsultaGlobal();
            var archivo = await consulta.LoadAsync<ArchivoDeCorridaPortal>(corrida.Id, ct)
                          ?? throw new InvalidOperationException("La corrida no tiene su archivo.");
            origen = OrigenPortalArchivo.DesdeJson(archivo.Json);
        }
        else
        {
            origen = fabrica.SqlServer();
        }

        var catalogosDelPortal = await origen.CatalogosAsync(ct);
        if (corrida.ImportarCatalogos && !corrida.CatalogosListos)
        {
            corrida.Catalogos = [.. await ImportacionDePortal.ImportarCatalogosAsync(sesiones, catalogosDelPortal, corrida.DryRun, reloj.GetUtcNow(), ct)];
            corrida.CatalogosListos = true;
            await GuardarAsync(sesiones, corrida, reloj, ct);
        }

        var plataforma = await ImportacionDePortal.CatalogosAsync(sesiones, corrida.ImportarCatalogos ? catalogosDelPortal : null, reloj.GetUtcNow(), ct);
        var empresas = new Dictionary<int, Empresa>();
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            foreach (var c in corrida.Constructoras)
                empresas[c.Constructora] = await consulta.LoadAsync<Empresa>(c.EmpresaId, ct)
                                          ?? throw new InvalidOperationException($"La empresa {c.EmpresaId} ya no existe.");
        }

        var opciones = new OpcionesDeImportacion(corrida.DryRun, OrigenDeSeccion.Importada, empresas, ActorTipo.Migracion, $"{Actor}:{corrida.Id:N}", null);
        while (true)
        {
            var lote = await origen.TercerosAsync(corrida.Cursor, corrida.TamanoLote, ct);
            if (lote.Count == 0)
                return;

            foreach (var tercero in lote)
            {
                try
                {
                    // Un contexto de mensajería propio por tercero: el outbox de cada versión pide, en su misma transacción, poner al
                    // día los vínculos aceptados (F2). El del mensaje de la corrida no sirve: su sobre de entrada solo se confirma una vez.
                    var bus = corrida.DryRun ? null : new MessageContext(runtime);
                    corrida.Sumar(await ImportacionDePortal.ImportarTerceroAsync(sesiones, tercero, catalogosDelPortal, plataforma, opciones with { Mensajes = bus },
                        reloj, ct));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "No se pudo importar el tercero {Terid} del Portal.", tercero.Terid);
                    corrida.Sumar(new TerceroImportado(tercero.Terid, IdDeLegado.ClaveTerceroPortal(tercero.Terid), null, null, [], 0, 0, 0, [],
                        [new AvisoDeTraduccion(null, null, "tercero", "error-inesperado", $"No se pudo importar: {e.Message}", null,
                            "Revisar el error y volver a correr (la importación es idempotente).")]));
                }
            }

            corrida.Cursor = lote[^1].Terid;
            corrida.Lotes++;
            await GuardarAsync(sesiones, corrida, reloj, ct);
        }
    }

    internal static async Task<CorridaDeImportacionPortal?> CargarAsync(ISesiones sesiones, Guid id, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return await consulta.LoadAsync<CorridaDeImportacionPortal>(id, ct);
    }

    private static async Task GuardarAsync(ISesiones sesiones, CorridaDeImportacionPortal corrida, TimeProvider reloj, CancellationToken ct)
    {
        corrida.ActualizadaEn = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        sesion.Store(corrida);
        await sesion.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Importador de la ficha desde el Portal y sincronizador de coexistencia (F4, FM-39…FM-43; docs/08 §6.6). Rol Plataforma.
/// </summary>
public static class ImportacionDePortalEndpoints
{
    public const string OrigenArchivo = "Archivo";
    public const string OrigenSqlServer = "SqlServer";
    public const int LotePorOmision = 100;
    public const int LoteMaximo = 500;
    public const int MaximoDetalle = 500;

    /// <summary>
    /// Encola una corrida (202 con su estado): <c>dryRun</c>, origen <c>Archivo</c> (con el JSON en <c>archivo</c>) o
    /// <c>SqlServer</c> (configurado), mapa de constructoras ⇒ empresas para las aceptaciones y tamaño de lote. 422
    /// <c>origen-invalido</c>, <c>archivo-requerido</c>, <c>archivo-invalido</c>, <c>tamano-lote-invalido</c>,
    /// <c>constructora-duplicada</c>, <c>empresa-no-encontrada</c>; 409 <c>origen-portal-no-configurado</c>.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/importacion/portal")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Encolar(ImportarPortalDto dto, ISesiones sesiones, IFabricaDeOrigenesPortal fabrica, IMessageBus mensajes,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var origen = (dto.Origen ?? (dto.Archivo is not null ? OrigenArchivo : string.Empty)).Trim();
        origen = string.Equals(origen, OrigenArchivo, StringComparison.OrdinalIgnoreCase) ? OrigenArchivo
            : string.Equals(origen, OrigenSqlServer, StringComparison.OrdinalIgnoreCase) ? OrigenSqlServer
            : throw ReglaDeNegocioException.Validacion("origen-invalido", "El origen debe ser Archivo (con el JSON en «archivo») o SqlServer.");

        string? json = null;
        if (origen == OrigenArchivo)
        {
            if (dto.Archivo is not { ValueKind: JsonValueKind.Object } archivo)
                throw ReglaDeNegocioException.Validacion("archivo-requerido", "Con el origen Archivo envía el JSON del Portal en «archivo».");
            json = archivo.GetRawText();
            _ = OrigenPortalArchivo.DesdeJson(json); // 422 si no tiene la forma esperada
        }
        else if (!fabrica.SqlServerConfigurado)
        {
            throw ReglaDeNegocioException.Conflicto("origen-portal-no-configurado",
                "No hay cadena de conexión del Portal (Validasoft:Importacion:Portal:CadenaDeConexion).");
        }

        var lote = dto.TamanoLote ?? LotePorOmision;
        if (lote is < 1 or > LoteMaximo)
            throw ReglaDeNegocioException.Validacion("tamano-lote-invalido", $"El tamaño del lote va de 1 a {LoteMaximo}.");

        var constructoras = (dto.Constructoras ?? []).ToList();
        if (constructoras.GroupBy(c => c.Constructora).FirstOrDefault(g => g.Count() > 1) is { } repetida)
            throw ReglaDeNegocioException.Validacion("constructora-duplicada", $"La constructora {repetida.Key} está dos veces en el mapa.");
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            foreach (var c in constructoras)
                if (await consulta.LoadAsync<Empresa>(c.EmpresaId, ct) is null)
                    throw ReglaDeNegocioException.Validacion("empresa-no-encontrada", $"No existe la empresa {c.EmpresaId} (constructora {c.Constructora}).");
        }

        var ahora = reloj.GetUtcNow();
        var corrida = new CorridaDeImportacionPortal
        {
            Id = Guid.CreateVersion7(),
            Estado = EstadoCorridaPortal.Encolada,
            DryRun = dto.DryRun ?? false,
            Origen = origen,
            TamanoLote = lote,
            ImportarCatalogos = dto.ImportarCatalogos ?? true,
            Constructoras = [.. constructoras.Select(c => new ConstructoraEmpresa(c.Constructora, c.EmpresaId))],
            CreadaPor = $"{contexto.ActorTipo}:{contexto.ActorId}",
            CreadaEn = ahora,
            ActualizadaEn = ahora
        };

        await using var sesion = sesiones.Global(mensajes);
        sesion.Store(corrida);
        if (json is not null)
            sesion.Store(new ArchivoDeCorridaPortal { Id = corrida.Id, Json = json });
        await mensajes.PublishAsync(new EjecutarImportacionDePortal(corrida.Id));
        await sesion.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/plataforma/importacion/portal/{corrida.Id}", ADto(corrida));
    }

    /// <summary>Estado y resumen de la corrida (404 <c>corrida-no-encontrada</c>).</summary>
    [WolverineGet("/api/v1/plataforma/importacion/portal/{id}")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<CorridaImportacionPortalDto> Obtener(Guid id, ISesiones sesiones, CancellationToken ct) =>
        ADto(await RequeridaAsync(sesiones, id, ct));

    /// <summary>
    /// CSV de rechazos (UTF-8 con BOM, separado por comas): <c>claveLegado, terid, seccion, periodo, campo, codigo, motivo, dato,
    /// accionSugerida</c>, una fila por dato que no se pudo importar.
    /// </summary>
    [WolverineGet("/api/v1/plataforma/importacion/portal/{id}/rechazos")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Rechazos(Guid id, ISesiones sesiones, CancellationToken ct)
    {
        var corrida = await RequeridaAsync(sesiones, id, ct);
        return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Csv(corrida.Rechazos))).ToArray(), "text/csv; charset=utf-8",
            $"rechazos-portal-{corrida.Id:N}.csv");
    }

    /// <summary>Vuelve a encolar una corrida fallida o interrumpida; sigue desde su cursor (202). 409 <c>corrida-completada</c>.</summary>
    [WolverinePost("/api/v1/plataforma/importacion/portal/{id}/reanudar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Reanudar(Guid id, ISesiones sesiones, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        var corrida = await RequeridaAsync(sesiones, id, ct);
        if (corrida.Estado == EstadoCorridaPortal.Completada)
            throw ReglaDeNegocioException.Conflicto("corrida-completada", "La corrida ya terminó: para volver a importar, encola otra (es idempotente).");

        corrida.Estado = EstadoCorridaPortal.Encolada;
        corrida.Error = null;
        corrida.ActualizadaEn = reloj.GetUtcNow();
        await using var sesion = sesiones.Global(mensajes);
        sesion.Store(corrida);
        await mensajes.PublishAsync(new EjecutarImportacionDePortal(corrida.Id));
        await sesion.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/plataforma/importacion/portal/{corrida.Id}", ADto(corrida));
    }

    /// <summary>
    /// Sincronizador de coexistencia (FM-11, FM-43), Portal ⇒ Validasoft, un solo sentido: reimporta un tercero desde el origen
    /// configurado con el mismo traductor. Si una sección cambió nace una versión con origen <c>Portal</c> y, en la misma
    /// transacción, se piden las sincronizaciones de requisitos de F2: la empresa que ya la aceptó abre su revalidación. Sin
    /// cambios no hace nada (idempotente). No importa catálogos ni acepta: usa los catálogos de plataforma ya cargados. 404
    /// <c>tercero-portal-no-encontrado</c>; 409 <c>origen-portal-no-configurado</c>.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/importacion/portal/terceros/{terid}/sincronizar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SincronizacionPortalDto> Sincronizar(int terid, ISesiones sesiones, IFabricaDeOrigenesPortal fabrica, IMessageBus mensajes,
        TimeProvider reloj, CancellationToken ct)
    {
        var origen = fabrica.Configurado();
        var tercero = await origen.TerceroAsync(terid, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-portal-no-encontrado", $"El Portal no tiene el tercero {terid}.");
        var catalogos = await origen.CatalogosAsync(ct);
        var plataforma = await ImportacionDePortal.CatalogosAsync(sesiones, null, reloj.GetUtcNow(), ct);
        var opciones = new OpcionesDeImportacion(false, OrigenDeSeccion.Portal, new Dictionary<int, Empresa>(), ActorTipo.Integracion,
            "sincronizador-portal", mensajes);
        return (await ImportacionDePortal.ImportarTerceroAsync(sesiones, tercero, catalogos, plataforma, opciones, reloj, ct)).ADto();
    }

    private static async Task<CorridaDeImportacionPortal> RequeridaAsync(ISesiones sesiones, Guid id, CancellationToken ct) =>
        await EjecutarImportacionDePortalHandler.CargarAsync(sesiones, id, ct)
        ?? throw ReglaDeNegocioException.NoEncontrado("corrida-no-encontrada", "No existe esa corrida de importación.");

    public static CorridaImportacionPortalDto ADto(CorridaDeImportacionPortal c) =>
        new(c.Id, c.Estado.ToString(), c.DryRun, c.Origen, c.TamanoLote, c.ImportarCatalogos,
            [.. c.Constructoras.Select(x => new ConstructoraEmpresaDto(x.Constructora, x.EmpresaId))], c.Cursor, c.Lotes, c.CreadaEn, c.ActualizadaEn,
            c.TerminadaEn, c.Error,
            new ResumenImportacionPortalDto(c.TercerosLeidos, c.TercerosCreados, c.TercerosEncontrados, c.TercerosRechazados,
                [.. CatalogoDeSecciones.Todas.Where(s => c.Secciones.ContainsKey(s.Codigo))
                    .Select(s => new ResumenSeccionImportadaDto(s.Codigo, c.Secciones[s.Codigo].Creadas, c.Secciones[s.Codigo].SinCambios,
                        c.Secciones[s.Codigo].Rechazadas))],
                c.Aceptaciones, c.AceptacionesExistentes, c.SinVinculo, c.Catalogos, c.Advertencias.Count, c.Rechazos.Count,
                [.. c.Advertencias.Take(MaximoDetalle)], [.. c.Rechazos.Take(MaximoDetalle)]));

    /// <summary>El CSV de rechazos (RFC 4180: comillas dobles alrededor de los campos con coma, comilla o salto de línea).</summary>
    public static string Csv(IEnumerable<AvisoImportacionDto> rechazos)
    {
        var csv = new StringBuilder("claveLegado,terid,seccion,periodo,campo,codigo,motivo,dato,accionSugerida\r\n");
        foreach (var r in rechazos)
        {
            string?[] campos = [r.ClaveLegado, r.Terid.ToString(CultureInfo.InvariantCulture), r.Seccion, r.Periodo?.ToString(CultureInfo.InvariantCulture), r.Campo,
                r.Codigo, r.Mensaje, r.Dato, r.AccionSugerida];
            csv.Append(string.Join(',', campos.Select(Celda))).Append("\r\n");
        }

        return csv.ToString();
    }

    private static string Celda(string? valor)
    {
        var texto = valor ?? string.Empty;
        // Evita la inyección de fórmulas al abrir el CSV en una hoja de cálculo.
        if (texto.Length > 0 && "=+-@".Contains(texto[0], StringComparison.Ordinal))
            texto = "'" + texto;
        return texto.IndexOfAny([',', '"', '\r', '\n']) >= 0 ? "\"" + texto.Replace("\"", "\"\"", StringComparison.Ordinal) + "\"" : texto;
    }
}
