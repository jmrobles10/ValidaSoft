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
using Validasoft.Dominio.Ficha.Importacion;
using Validasoft.Dominio.Plataforma;
using Wolverine;
using Wolverine.Http;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Importacion;

public enum EstadoCorrida
{
    Encolada,
    EnCurso,
    Completada,
    Fallida
}

public sealed record UnidadEmpresa(string Unidad, Guid EmpresaId);

public sealed class ContadorDeSeccion
{
    public int Creadas { get; set; }

    public int SinCambios { get; set; }

    public int Rechazadas { get; set; }
}

/// <summary>
/// Corrida del importador genérico (esquema <c>plataforma</c>, global). Guarda el adaptador (<see cref="Origen"/>), su
/// configuración, el <see cref="Cursor"/> (la clave del último tercero de un lote terminado: la corrida se reanuda desde ahí), el
/// resumen acumulado y todos los avisos (el CSV de rechazos sale de aquí). Un dry-run también la guarda: es su informe, no un dato
/// importado.
/// </summary>
public sealed class CorridaDeImportacion
{
    public Guid Id { get; set; }

    /// <summary>Código del adaptador (<c>portal</c>).</summary>
    public string Origen { get; set; } = string.Empty;

    public EstadoCorrida Estado { get; set; }

    public bool DryRun { get; set; }

    /// <summary><c>Archivo</c> o <c>Conectado</c>.</summary>
    public string Fuente { get; set; } = string.Empty;

    public int TamanoLote { get; set; }

    public bool ImportarCatalogos { get; set; }

    public List<UnidadEmpresa> Unidades { get; set; } = [];

    public string? Cursor { get; set; }

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
        Advertencias.AddRange(t.Advertencias.Select(a => a.ADto(t.Sistema, t.Clave)));
        Rechazos.AddRange(t.Rechazos.Select(a => a.ADto(t.Sistema, t.Clave)));
    }
}

/// <summary>El JSON de una corrida de fuente archivo (aparte, para que el estado no lo cargue en cada lectura).</summary>
public sealed class ArchivoDeCorrida
{
    public Guid Id { get; set; }

    public string Json { get; set; } = string.Empty;
}

/// <summary>Ejecuta (o reanuda desde su cursor) una corrida del importador. Cola local durable y secuencial <c>importacion</c>.</summary>
public sealed record EjecutarImportacion(Guid CorridaId);

public static class EjecutarImportacionHandler
{
    public const string Actor = "importador";

    /// <summary>
    /// Idempotente y reanudable: una corrida completada no hace nada; una interrumpida sigue desde su cursor. Tras cada lote
    /// guarda el cursor y el resumen. Un error de un tercero queda como su rechazo y no detiene la corrida; uno general la deja
    /// <c>Fallida</c> (se reintenta y se puede reanudar con <c>POST …/{id}/reanudar</c>).
    /// </summary>
    public static async Task Handle(EjecutarImportacion mensaje, ISesiones sesiones, RegistroDeAdaptadores adaptadores, IWolverineRuntime runtime,
        TimeProvider reloj, ILogger<CorridaDeImportacion> logger, CancellationToken ct)
    {
        var corrida = await CargarAsync(sesiones, mensaje.CorridaId, ct);
        if (corrida is null || corrida.Estado == EstadoCorrida.Completada)
            return;

        corrida.Estado = EstadoCorrida.EnCurso;
        corrida.Error = null;
        await GuardarAsync(sesiones, corrida, reloj, ct);
        try
        {
            await EjecutarAsync(corrida, adaptadores.Obtener(corrida.Origen), sesiones, runtime, reloj, logger, ct);
            corrida.Estado = EstadoCorrida.Completada;
            corrida.TerminadaEn = reloj.GetUtcNow();
            await GuardarAsync(sesiones, corrida, reloj, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogError(e, "La corrida {Corrida} del importador de {Origen} falló en el cursor {Cursor}.", corrida.Id, corrida.Origen, corrida.Cursor);
            corrida.Estado = EstadoCorrida.Fallida;
            corrida.Error = e.Message;
            await GuardarAsync(sesiones, corrida, reloj, ct);
            throw;
        }
    }

    private static async Task EjecutarAsync(CorridaDeImportacion corrida, IAdaptadorDeOrigen adaptador, ISesiones sesiones, IWolverineRuntime runtime,
        TimeProvider reloj, ILogger logger, CancellationToken ct)
    {
        IOrigenDeTerceros origen;
        if (corrida.Fuente == ImportacionEndpoints.FuenteArchivo)
        {
            await using var consulta = sesiones.ConsultaGlobal();
            var archivo = await consulta.LoadAsync<ArchivoDeCorrida>(corrida.Id, ct)
                          ?? throw new InvalidOperationException("La corrida no tiene su archivo.");
            origen = adaptador.DesdeArchivo(archivo.Json);
        }
        else
        {
            origen = adaptador.Conectado();
        }

        var catalogosDelOrigen = await origen.CatalogosAsync(ct);
        if (corrida.ImportarCatalogos && !corrida.CatalogosListos)
        {
            corrida.Catalogos = [.. await ImportadorDeTerceros.ImportarCatalogosAsync(sesiones, catalogosDelOrigen, corrida.DryRun, reloj.GetUtcNow(), ct)];
            corrida.CatalogosListos = true;
            await GuardarAsync(sesiones, corrida, reloj, ct);
        }

        var plataforma = await ImportadorDeTerceros.CatalogosAsync(sesiones, corrida.ImportarCatalogos ? catalogosDelOrigen : null, reloj.GetUtcNow(), ct);
        var empresas = new Dictionary<string, Empresa>(StringComparer.Ordinal);
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            foreach (var u in corrida.Unidades)
                empresas[u.Unidad] = await consulta.LoadAsync<Empresa>(u.EmpresaId, ct)
                                     ?? throw new InvalidOperationException($"La empresa {u.EmpresaId} ya no existe.");
        }

        var opciones = new OpcionesDeImportacion(corrida.DryRun, adaptador.Codigo, OrigenDeSeccion.Importada, empresas, ActorTipo.Migracion,
            $"{Actor}:{adaptador.Codigo}:{corrida.Id:N}", null);
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
                    corrida.Sumar(await ImportadorDeTerceros.ImportarTerceroAsync(sesiones, tercero, plataforma, opciones with { Mensajes = bus }, reloj, ct));
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "No se pudo importar el tercero {Clave} de {Origen}.", tercero.Clave, adaptador.Codigo);
                    corrida.Sumar(new TerceroImportado(adaptador.Codigo, tercero.Clave, null, null, [], 0, 0, 0, [],
                        [new AvisoDeTraduccion(null, null, "tercero", "error-inesperado", $"No se pudo importar: {e.Message}", null,
                            "Revisar el error y volver a correr (la importación es idempotente).")]));
                }
            }

            corrida.Cursor = lote[^1].Clave;
            corrida.Lotes++;
            await GuardarAsync(sesiones, corrida, reloj, ct);
        }
    }

    internal static async Task<CorridaDeImportacion?> CargarAsync(ISesiones sesiones, Guid id, CancellationToken ct)
    {
        await using var consulta = sesiones.ConsultaGlobal();
        return await consulta.LoadAsync<CorridaDeImportacion>(id, ct);
    }

    private static async Task GuardarAsync(ISesiones sesiones, CorridaDeImportacion corrida, TimeProvider reloj, CancellationToken ct)
    {
        corrida.ActualizadaEn = reloj.GetUtcNow();
        await using var sesion = sesiones.Global();
        sesion.Store(corrida);
        await sesion.SaveChangesAsync(ct);
    }
}

/// <summary>
/// Importador genérico de la ficha y sincronizador de coexistencia (F4, FM-39…FM-43; docs/08 §6.6). Rol Plataforma. El sistema de
/// origen es el <c>{origen}</c> de la ruta: el código de un adaptador registrado (<see cref="IAdaptadorDeOrigen"/>); sin ese
/// adaptador, 404 <c>origen-no-registrado</c>.
/// </summary>
public static class ImportacionEndpoints
{
    public const string FuenteArchivo = "Archivo";
    public const string FuenteConectado = "Conectado";
    public const int LotePorOmision = 100;
    public const int LoteMaximo = 500;
    public const int MaximoDetalle = 500;

    /// <summary>
    /// Encola una corrida (202 con su estado): <c>dryRun</c>, fuente <c>Archivo</c> (con el JSON en <c>archivo</c>) o <c>Conectado</c>
    /// (el origen configurado del adaptador), mapa de unidades ⇒ empresas para las aceptaciones y tamaño de lote. 422
    /// <c>fuente-invalida</c>, <c>archivo-requerido</c>, <c>archivo-invalido</c>, <c>tamano-lote-invalido</c>, <c>unidad-duplicada</c>,
    /// <c>empresa-no-encontrada</c>; 404 <c>origen-no-registrado</c>; 409 <c>origen-no-configurado</c>.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/importacion/{origen}")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Encolar(string origen, ImportarTercerosDto dto, ISesiones sesiones, RegistroDeAdaptadores adaptadores, IMessageBus mensajes,
        IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var adaptador = adaptadores.Obtener(origen);
        var fuente = (dto.Fuente ?? (dto.Archivo is not null ? FuenteArchivo : string.Empty)).Trim();
        fuente = string.Equals(fuente, FuenteArchivo, StringComparison.OrdinalIgnoreCase) ? FuenteArchivo
            : string.Equals(fuente, FuenteConectado, StringComparison.OrdinalIgnoreCase) ? FuenteConectado
            : throw ReglaDeNegocioException.Validacion("fuente-invalida", "La fuente debe ser Archivo (con el JSON en «archivo») o Conectado.");

        string? json = null;
        if (fuente == FuenteArchivo)
        {
            if (dto.Archivo is not { ValueKind: JsonValueKind.Object } archivo)
                throw ReglaDeNegocioException.Validacion("archivo-requerido", "Con la fuente Archivo envía el JSON del origen en «archivo».");
            json = archivo.GetRawText();
            adaptador.ValidarArchivo(json); // 422 si no tiene la forma que espera el adaptador
        }
        else if (!adaptador.ConectadoConfigurado)
        {
            throw ReglaDeNegocioException.Conflicto("origen-no-configurado", $"El origen «{adaptador.Codigo}» no tiene un origen conectado configurado.");
        }

        var lote = dto.TamanoLote ?? LotePorOmision;
        if (lote is < 1 or > LoteMaximo)
            throw ReglaDeNegocioException.Validacion("tamano-lote-invalido", $"El tamaño del lote va de 1 a {LoteMaximo}.");

        var unidades = (dto.Unidades ?? []).ToList();
        if (unidades.GroupBy(u => u.Unidad).FirstOrDefault(g => g.Count() > 1) is { } repetida)
            throw ReglaDeNegocioException.Validacion("unidad-duplicada", $"La unidad {repetida.Key} está dos veces en el mapa.");
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            foreach (var u in unidades)
                if (await consulta.LoadAsync<Empresa>(u.EmpresaId, ct) is null)
                    throw ReglaDeNegocioException.Validacion("empresa-no-encontrada", $"No existe la empresa {u.EmpresaId} (unidad {u.Unidad}).");
        }

        var ahora = reloj.GetUtcNow();
        var corrida = new CorridaDeImportacion
        {
            Id = Guid.CreateVersion7(),
            Origen = adaptador.Codigo,
            Estado = EstadoCorrida.Encolada,
            DryRun = dto.DryRun ?? false,
            Fuente = fuente,
            TamanoLote = lote,
            ImportarCatalogos = dto.ImportarCatalogos ?? true,
            Unidades = [.. unidades.Select(u => new UnidadEmpresa(u.Unidad, u.EmpresaId))],
            CreadaPor = $"{contexto.ActorTipo}:{contexto.ActorId}",
            CreadaEn = ahora,
            ActualizadaEn = ahora
        };

        await using var sesion = sesiones.Global(mensajes);
        sesion.Store(corrida);
        if (json is not null)
            sesion.Store(new ArchivoDeCorrida { Id = corrida.Id, Json = json });
        await mensajes.PublishAsync(new EjecutarImportacion(corrida.Id));
        await sesion.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/plataforma/importacion/{adaptador.Codigo}/{corrida.Id}", ADto(corrida));
    }

    /// <summary>Estado y resumen de la corrida (404 <c>corrida-no-encontrada</c>).</summary>
    [WolverineGet("/api/v1/plataforma/importacion/{origen}/{id}")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<CorridaImportacionDto> Obtener(string origen, Guid id, ISesiones sesiones, CancellationToken ct) =>
        ADto(await RequeridaAsync(sesiones, origen, id, ct));

    /// <summary>
    /// CSV de rechazos (UTF-8 con BOM, separado por comas): <c>claveLegado, clave, seccion, periodo, campo, codigo, motivo, dato,
    /// accionSugerida</c>, una fila por dato que no se pudo importar.
    /// </summary>
    [WolverineGet("/api/v1/plataforma/importacion/{origen}/{id}/rechazos")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Rechazos(string origen, Guid id, ISesiones sesiones, CancellationToken ct)
    {
        var corrida = await RequeridaAsync(sesiones, origen, id, ct);
        return Results.File(Encoding.UTF8.GetPreamble().Concat(Encoding.UTF8.GetBytes(Csv(corrida.Rechazos))).ToArray(), "text/csv; charset=utf-8",
            $"rechazos-{corrida.Origen}-{corrida.Id:N}.csv");
    }

    /// <summary>Vuelve a encolar una corrida fallida o interrumpida; sigue desde su cursor (202). 409 <c>corrida-completada</c>.</summary>
    [WolverinePost("/api/v1/plataforma/importacion/{origen}/{id}/reanudar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<IResult> Reanudar(string origen, Guid id, ISesiones sesiones, IMessageBus mensajes, TimeProvider reloj, CancellationToken ct)
    {
        var corrida = await RequeridaAsync(sesiones, origen, id, ct);
        if (corrida.Estado == EstadoCorrida.Completada)
            throw ReglaDeNegocioException.Conflicto("corrida-completada", "La corrida ya terminó: para volver a importar, encola otra (es idempotente).");

        corrida.Estado = EstadoCorrida.Encolada;
        corrida.Error = null;
        corrida.ActualizadaEn = reloj.GetUtcNow();
        await using var sesion = sesiones.Global(mensajes);
        sesion.Store(corrida);
        await mensajes.PublishAsync(new EjecutarImportacion(corrida.Id));
        await sesion.SaveChangesAsync(ct);
        return Results.Accepted($"/api/v1/plataforma/importacion/{corrida.Origen}/{corrida.Id}", ADto(corrida));
    }

    /// <summary>
    /// Sincronizador de coexistencia (FM-11, FM-43), sistema de origen ⇒ Validasoft, un solo sentido: reimporta un tercero desde el
    /// origen configurado del adaptador con su mismo traductor. Si una sección cambió nace una versión con origen
    /// <c>SistemaExterno</c> y, en la misma transacción, se piden las sincronizaciones de requisitos de F2: la empresa que ya la
    /// aceptó abre su revalidación. Sin cambios no hace nada (idempotente). No importa catálogos ni acepta: usa los catálogos de
    /// plataforma ya cargados. 404 <c>origen-no-registrado</c>, <c>tercero-origen-no-encontrado</c>; 409 <c>origen-no-configurado</c>.
    /// </summary>
    [WolverinePost("/api/v1/plataforma/importacion/{origen}/terceros/{clave}/sincronizar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<SincronizacionDto> Sincronizar(string origen, string clave, ISesiones sesiones, RegistroDeAdaptadores adaptadores, IMessageBus mensajes,
        TimeProvider reloj, CancellationToken ct)
    {
        var adaptador = adaptadores.Obtener(origen);
        var fuente = adaptador.Configurado();
        var tercero = await fuente.TerceroAsync(clave, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-origen-no-encontrado", $"El origen «{adaptador.Codigo}» no tiene el tercero {clave}.");
        var plataforma = await ImportadorDeTerceros.CatalogosAsync(sesiones, null, reloj.GetUtcNow(), ct);
        var opciones = new OpcionesDeImportacion(false, adaptador.Codigo, OrigenDeSeccion.SistemaExterno, new Dictionary<string, Empresa>(), ActorTipo.Integracion,
            $"sincronizador:{adaptador.Codigo}", mensajes);
        return (await ImportadorDeTerceros.ImportarTerceroAsync(sesiones, tercero, plataforma, opciones, reloj, ct)).ADto();
    }

    private static async Task<CorridaDeImportacion> RequeridaAsync(ISesiones sesiones, string origen, Guid id, CancellationToken ct)
    {
        var corrida = await EjecutarImportacionHandler.CargarAsync(sesiones, id, ct);
        return corrida is not null && string.Equals(corrida.Origen, origen.Trim(), StringComparison.OrdinalIgnoreCase)
            ? corrida
            : throw ReglaDeNegocioException.NoEncontrado("corrida-no-encontrada", "No existe esa corrida de importación.");
    }

    public static CorridaImportacionDto ADto(CorridaDeImportacion c) =>
        new(c.Id, c.Origen, c.Estado.ToString(), c.DryRun, c.Fuente, c.TamanoLote, c.ImportarCatalogos,
            [.. c.Unidades.Select(x => new UnidadEmpresaDto(x.Unidad, x.EmpresaId))], c.Cursor, c.Lotes, c.CreadaEn, c.ActualizadaEn,
            c.TerminadaEn, c.Error,
            new ResumenImportacionDto(c.TercerosLeidos, c.TercerosCreados, c.TercerosEncontrados, c.TercerosRechazados,
                [.. CatalogoDeSecciones.Todas.Where(s => c.Secciones.ContainsKey(s.Codigo))
                    .Select(s => new ResumenSeccionImportadaDto(s.Codigo, c.Secciones[s.Codigo].Creadas, c.Secciones[s.Codigo].SinCambios,
                        c.Secciones[s.Codigo].Rechazadas))],
                c.Aceptaciones, c.AceptacionesExistentes, c.SinVinculo, c.Catalogos, c.Advertencias.Count, c.Rechazos.Count,
                [.. c.Advertencias.Take(MaximoDetalle)], [.. c.Rechazos.Take(MaximoDetalle)]));

    /// <summary>El CSV de rechazos (RFC 4180: comillas dobles alrededor de los campos con coma, comilla o salto de línea).</summary>
    public static string Csv(IEnumerable<AvisoImportacionDto> rechazos)
    {
        var csv = new StringBuilder("claveLegado,clave,seccion,periodo,campo,codigo,motivo,dato,accionSugerida\r\n");
        foreach (var r in rechazos)
        {
            string?[] campos = [r.ClaveLegado, r.Clave, r.Seccion, r.Periodo?.ToString(CultureInfo.InvariantCulture), r.Campo,
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
