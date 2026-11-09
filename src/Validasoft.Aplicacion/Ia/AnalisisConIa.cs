using Marten;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ia;
using Wolverine;

namespace Validasoft.Aplicacion.Ia;

/// <summary>
/// Analiza con IA la versión de un documento y reparte la validación entre las empresas que la reciben (ADR-0012). Lo
/// publica el outbox de la bóveda cuando una versión de archivo queda actual y se comparte, y cuando un vínculo recibe
/// documentos al aceptar o al abrir su expediente. Cola local durable y secuencial <c>ia</c>; el manejador es idempotente.
/// </summary>
public sealed record AnalizarVersionConIa(Guid DocumentoId, int Numero);

/// <summary>El vínculo recibió documentos fuera del outbox de la bóveda (al aceptar o al abrir su expediente): analiza o consume los suyos.</summary>
public sealed record AnalizarIaDelVinculo(Guid TerceroId, Guid VinculoId);

/// <summary>Qué versiones se analizan con IA y cómo se pide.</summary>
public static class AnalisisConIa
{
    /// <summary>MIME que se envían a OpenAI: PDF como archivo; JPEG y PNG como imagen. Los demás no se analizan (sin consumo).</summary>
    public static readonly IReadOnlyList<string> MimesAnalizables = [InspectorDeArchivos.Pdf, InspectorDeArchivos.Jpeg, InspectorDeArchivos.Png];

    /// <summary>Versión de archivo de un tipo canónico del catálogo (con su esquema de análisis).</summary>
    public static bool Analizable(Documento documento, int numero) =>
        documento.Buscar(numero) is { TieneArchivo: true } && EsquemasDeAnalisis.Para(documento.TipoCanonico) is not null;

    /// <summary>Publica en el outbox el análisis (o el consumo) de la versión actual si es analizable.</summary>
    public static async Task PedirAsync(IMessageBus mensajes, Documento documento)
    {
        if (documento.VersionActual is { } actual && Analizable(documento, actual))
            await mensajes.PublishAsync(new AnalizarVersionConIa(documento.Id, actual));
    }
}

public static class AnalizarIaDelVinculoHandler
{
    /// <summary>Pide el análisis (o el consumo) de cada documento del tercero cuya versión actual está compartida con el vínculo.</summary>
    public static async Task Handle(AnalizarIaDelVinculo mensaje, ISesiones sesiones, IMessageContext mensajes, CancellationToken ct)
    {
        var terceroId = mensaje.TerceroId;
        await using var consulta = sesiones.ConsultaBoveda();
        var documentos = await consulta.Query<Documento>().Where(d => d.TerceroId == terceroId).ToListAsync(ct);
        foreach (var documento in documentos.Where(d => d.VersionActual is { } n && d.EstaCompartida(n, mensaje.VinculoId)))
            await AnalisisConIa.PedirAsync(mensajes, documento);
    }
}

public static class AnalizarVersionConIaHandler
{
    /// <summary>
    /// (a) Si la versión ya tiene resultado de IA, no se vuelve a llamar. (b) Si no, paga la empresa del vínculo de contexto
    /// o, si no puede, la primera destinataria (por orden de compartición) con plan activo, llave y cupo; sin ninguna, la
    /// versión queda sin IA. (c) La pagadora consume su validación antes de la llamada. (d) Se llama al analizador con su
    /// llave (y al modelo de escalamiento si hace falta). (e) Se registra <see cref="AnalisisDeIaRegistrado"/> o, si falla,
    /// <see cref="AnalisisDeIaFallido"/> y se devuelve la validación (fail-closed, IA-6). (f) Tokens y costo van al uso de la
    /// pagadora. (g) Cada otra destinataria consume su validación (o queda sin cupo y sin ver los hechos de IA).
    /// </summary>
    public static async Task Handle(AnalizarVersionConIa mensaje, ISesiones sesiones, IMessageContext mensajes, IAnalizadorDeDocumentos analizador,
        IProtectorDeSecretos protector, IAlmacenDeArchivos almacen, OpcionesDeIa opciones, CupoDeIa cupo, TimeProvider reloj,
        ILogger<AnalizarVersionConIa> logger, CancellationToken ct)
    {
        if (!opciones.Habilitada)
            return;

        Documento? documento;
        await using (var consulta = sesiones.ConsultaBoveda())
            documento = await consulta.LoadAsync<Documento>(mensaje.DocumentoId, ct);
        if (documento is null || !AnalisisConIa.Analizable(documento, mensaje.Numero))
            return;

        var version = documento.VersionRequerida(mensaje.Numero);
        var destinatarias = Destinatarias(documento, version);
        if (destinatarias.Count == 0)
            return;

        if (!version.IaResuelta)
        {
            if (await AnalizarAsync(documento, version, destinatarias, sesiones, mensajes, analizador, protector, almacen, opciones, cupo, reloj,
                    logger, ct) is null)
                return;
            await using var consulta = sesiones.ConsultaBoveda();
            version = (await consulta.LoadAsync<Documento>(mensaje.DocumentoId, ct))?.Buscar(mensaje.Numero) ?? version;
        }

        if (version.Ia is null)
            return;

        // Idempotente: la pagadora (y quien ya la consumió) responde YaConsumida sin volver a contar.
        foreach (var destinataria in destinatarias)
            await cupo.ConsumirAsync(destinataria, documento.Id, version.Numero, pagaLaLlamada: false, mensajes, ct);
    }

    /// <summary>Empresas con las que está compartida la versión; primero la del vínculo de contexto y luego por orden de compartición.</summary>
    private static List<EmpresaDestinataria> Destinatarias(Documento documento, VersionDocumento version)
    {
        var comparticiones = documento.Comparticiones.Where(c => c.Numero == version.Numero).OrderBy(c => c.Fecha).ToList();
        var contexto = comparticiones.FirstOrDefault(c => c.VinculoId == version.ContextoVinculoId);
        return
        [
            .. (contexto is null ? comparticiones : [contexto, .. comparticiones.Where(c => c != contexto)])
                .Select(c => new EmpresaDestinataria(c.TenantId, c.EmpresaId))
                .Distinct()
        ];
    }

    /// <summary>Elige la pagadora, consume su validación y llama al analizador; devuelve la pagadora (nula si nadie pudo pagar o el MIME no se analiza).</summary>
    private static async Task<EmpresaDestinataria?> AnalizarAsync(Documento documento, VersionDocumento version, IReadOnlyList<EmpresaDestinataria> destinatarias,
        ISesiones sesiones, IMessageContext mensajes, IAnalizadorDeDocumentos analizador, IProtectorDeSecretos protector, IAlmacenDeArchivos almacen,
        OpcionesDeIa opciones, CupoDeIa cupo, TimeProvider reloj, ILogger logger, CancellationToken ct)
    {
        // Solo la versión que sigue siendo la actual provoca una llamada; el MIME real debe ser PDF, JPEG o PNG.
        var mime = version.Hechos?.MimeReal ?? version.MimeDeclarado;
        if (version.Estado != EstadoVersion.Actual || !AnalisisConIa.MimesAnalizables.Contains(mime))
            return null;

        var candidatas = new List<(EmpresaDestinataria Empresa, PlanDeIaEmpresa Plan)>();
        foreach (var destinataria in destinatarias)
        {
            if (await cupo.PagadorAsync(destinataria, documento.Id, version.Numero, ct) is { } plan)
                candidatas.Add((destinataria, plan));
        }

        if (candidatas.Count == 0)
            return null;

        var limites = LimitesDeAnalisis.Para(documento.TipoCanonico);
        if (mime != InspectorDeArchivos.Pdf && version.Tamano > limites.TamanoMaximoBytes)
            return null; // Una imagen no se recorta: si excede el tamaño no se envía (IA-5) y nadie consume.

        EmpresaDestinataria? pagadora = null;
        PlanDeIaEmpresa? planPagador = null;
        foreach (var (empresa, plan) in candidatas)
        {
            if (await cupo.ConsumirAsync(empresa, documento.Id, version.Numero, pagaLaLlamada: true, mensajes, ct) is ResultadoDeConsumo.SinCupo)
                continue;
            pagadora = empresa;
            planPagador = plan;
            break;
        }

        if (pagadora is null || planPagador is null)
            return null;

        var esquema = EsquemasDeAnalisis.Para(documento.TipoCanonico)!;
        var llamadas = new List<LlamadaDeIa>();
        AnalisisDeIaRegistrado? analisis = null;
        string? motivo = null;
        try
        {
            var contenido = await LeerAsync(almacen, version, ct);
            var llave = protector.Desproteger(planPagador.ApiKeyOpenAiProtegida!);
            var anterior = documento.AnteriorConIa(version.Numero)?.Ia?.Campos;

            SolicitudDeAnalisis Solicitud(string modelo) => new(documento.Id, version.Numero, esquema.TipoCanonico, mime, version.NombreOriginal, contenido,
                version.Hechos?.Paginas, modelo, limites);

            var resultado = await analizador.AnalizarAsync(Solicitud(planPagador.ModeloPorDefecto), llave, ct);
            llamadas.Add(Llamada(opciones, planPagador.ModeloPorDefecto, resultado));
            var escalado = false;
            var hechos = resultado.Hechos;
            var comparacion = ReglasDeAnalisis.Comparar(anterior, hechos.Campos, esquema.CamposClave, esquema.CamposCriticos);
            if (ReglasDeAnalisis.DebeEscalar(hechos.Confianza, hechos.Legible, hechos.Alertas.Count, planPagador.UmbralConfianza,
                    comparacion?.CamposCriticosCambiados.Count ?? 0) &&
                !string.Equals(planPagador.ModeloEscalamiento, planPagador.ModeloPorDefecto, StringComparison.OrdinalIgnoreCase))
            {
                resultado = await analizador.AnalizarAsync(Solicitud(planPagador.ModeloEscalamiento), llave, ct);
                llamadas.Add(Llamada(opciones, planPagador.ModeloEscalamiento, resultado));
                escalado = true;
                hechos = resultado.Hechos;
                comparacion = ReglasDeAnalisis.Comparar(anterior, hechos.Campos, esquema.CamposClave, esquema.CamposCriticos);
            }

            analisis = new AnalisisDeIaRegistrado(version.Numero, llamadas[^1].Modelo, escalado, hechos.Confianza, hechos.Legible, hechos.TipoDetectado,
                hechos.Campos, hechos.Alertas, resultado.PaginasEnviadas, resultado.PaginasOmitidas, llamadas.Sum(l => l.TokensEntrada),
                llamadas.Sum(l => l.TokensSalida), llamadas.Sum(l => l.CostoUsd), pagadora.TenantId, pagadora.EmpresaId, reloj.GetUtcNow(),
                hechos.Observaciones,
                ReglasDeAnalisis.PendienteRevisionHumana(hechos.Confianza, hechos.Legible, hechos.Alertas.Count, planPagador.UmbralConfianza),
                comparacion?.Comparacion, comparacion?.CamposCambiados ?? [], comparacion?.CamposCriticosCambiados ?? [], llamadas,
                llamadas.Sum(l => l.TokensEntradaEnCache));
        }
        catch (AnalisisDeIaException e)
        {
            motivo = e.Motivo;
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            // Fail-closed (IA-6): cualquier otra falla del proveedor o del archivo deja la versión sin IA; nunca se reintenta.
            motivo = "error-inesperado";
            logger.LogError("Error inesperado al analizar con IA el documento {DocumentoId} v{Numero}: {Tipo}", documento.Id, version.Numero,
                e.GetType().Name);
        }

        var registrado = await RegistrarAsync(sesiones, documento.Id, version.Numero, analisis, motivo ?? string.Empty, pagadora, reloj, ct);
        if (analisis is null || !registrado)
        {
            if (motivo is not null)
                logger.LogWarning("Análisis de IA fallido del documento {DocumentoId} v{Numero}: {Motivo}", documento.Id, version.Numero, motivo);
            // La empresa no recibe un análisis fallido: no se le cobra la validación. El costo real de llamadas exitosas sí se registra.
            if (analisis is null)
                await cupo.RevertirAsync(pagadora, documento.Id, version.Numero, ct);
        }

        await cupo.RegistrarCostoAsync(pagadora, llamadas, analisis is not null && registrado, ct);
        return pagadora;
    }

    private static LlamadaDeIa Llamada(OpcionesDeIa opciones, string modelo, ResultadoDeAnalisis resultado)
    {
        var uso = resultado.Uso;
        return new LlamadaDeIa(modelo, resultado.Hechos.Confianza, uso.Entrada, uso.EntradaEnCache, uso.Salida,
            opciones.Tarifa(modelo).Costo(uso.Entrada, uso.EntradaEnCache, uso.Salida));
    }

    /// <summary>Lee el binario de la versión (el que verificó la carga); nunca se registra ni se guarda en otra parte.</summary>
    private static async Task<byte[]> LeerAsync(IAlmacenDeArchivos almacen, VersionDocumento version, CancellationToken ct)
    {
        await using var flujo = await almacen.AbrirLecturaAsync(version.Bucket, version.ClaveObjeto, ct);
        using var memoria = new MemoryStream();
        await flujo.CopyToAsync(memoria, ct);
        return memoria.ToArray();
    }

    /// <summary>Escribe el análisis (o el fallo) en el stream del documento con bloqueo de fila; falso si la versión ya tenía resultado.</summary>
    private static Task<bool> RegistrarAsync(ISesiones sesiones, Guid documentoId, int numero, AnalisisDeIaRegistrado? analisis, string motivo,
        EmpresaDestinataria pagadora, TimeProvider reloj, CancellationToken ct) =>
        Peticiones.ConReintentosAsync(async () =>
        {
            await using var sesion = sesiones.Boveda();
            var stream = await sesion.Events.FetchForExclusiveWriting<Documento>(documentoId, ct);
            if (stream.Aggregate is not { } documento)
                return false;
            object? evento = analisis is not null
                ? documento.RegistrarAnalisisDeIa(analisis)
                : documento.RegistrarFalloDeIa(numero, motivo, pagadora.EmpresaId, reloj.GetUtcNow());
            if (evento is null)
                return false;
            stream.AppendOne(evento);
            await sesion.SaveChangesAsync(ct);
            return true;
        });
}
