using System.Diagnostics;
using System.Diagnostics.Metrics;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Flujos;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Invitaciones;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Invitaciones;

/// <summary>
/// Procesa la siguiente tanda de filas pendientes del lote (cola local durable y secuencial <c>lotes</c>, LT-8). Lleva la
/// ejecución vigente del lote: si se pausó, se canceló o se reanudó con otra ejecución, el mensaje viejo no hace nada.
/// </summary>
public sealed record ProcesarTandaDeLote(Guid TenantId, Guid LoteId, Guid Ejecucion);

/// <summary>
/// Métricas de la invitación masiva (OD-24), publicadas con <see cref="Meter"/> (<c>Validasoft.Lotes</c>; OpenTelemetry las recoge y las
/// pruebas de rendimiento las leen con un <see cref="MeterListener"/>): filas procesadas por resultado, reintentos por carrera de
/// concurrencia, tandas y la duración de cada fila.
/// </summary>
public static class MetricasDeLotes
{
    public const string NombreDelMedidor = "Validasoft.Lotes";

    private static readonly Meter Medidor = new(NombreDelMedidor);

    /// <summary>Filas que terminaron, con la etiqueta <c>resultado</c>: <c>exitosa</c> o <c>fallida</c> (con su <c>codigo</c>).</summary>
    public static readonly Counter<long> Filas = Medidor.CreateCounter<long>("lotes.filas", description: "Filas del lote que terminaron.");

    /// <summary>Veces que una fila se repitió porque perdió una carrera (otro proceso creó el mismo tercero o escribió en el vínculo).</summary>
    public static readonly Counter<long> Reintentos = Medidor.CreateCounter<long>("lotes.filas_reintentadas", description: "Reintentos de una fila por carrera.");

    public static readonly Counter<long> Tandas = Medidor.CreateCounter<long>("lotes.tandas", description: "Tandas procesadas.");

    public static readonly Histogram<double> DuracionDeFila = Medidor.CreateHistogram<double>("lotes.duracion_fila", unit: "ms", description: "Duración de procesar una fila.");
}

/// <summary>Lo que dejó una fila exitosa.</summary>
public sealed record ResultadoDeFila(AccionFilaLote Accion, Guid TerceroId, Guid VinculoId, bool TerceroCreado, string? Detalle);

/// <summary>
/// Saga <c>InvitacionMasiva</c> (docs/11 §H8) por tandas: lee <c>Validasoft:Lotes:TamanoTanda</c> filas pendientes (100 por
/// omisión), procesa cada una en <b>su propia transacción</b> (tercero, vínculo, invitación con su correo por el outbox y el
/// estado de la fila juntos: idempotente por fila), registra el avance y, si quedan pendientes y el lote sigue en proceso, se
/// encola a sí misma en la misma transacción. Nunca carga el lote completo en memoria ni lo procesa en una sola transacción.
/// Una regla de negocio deja la fila <c>Fallida</c> con su código; un error inesperado se reintenta dos veces antes.
/// </summary>
public static class ProcesarTandaDeLoteHandler
{
    public const int TandaPorOmision = 100;
    private const int Intentos = 3;

    public static async Task Handle(ProcesarTandaDeLote mensaje, IDocumentStore store, ISesiones sesiones, IWolverineRuntime runtime,
        IMessageContext mensajes, IProtectorDeSecretos protector, IConfiguration configuracion, TimeProvider reloj,
        ILogger<LoteDeInvitacion> logger, IEntornos entornos, Validasoft.Aplicacion.Plataforma.ControlDePlan plan, CancellationToken ct)
    {
        var tamano = Math.Clamp(configuracion.GetValue("Validasoft:Lotes:TamanoTanda", TandaPorOmision), 1, 1000);
        LoteDeInvitacion? lote;
        IReadOnlyList<FilaDeLote> filas;
        await using (var consulta = store.QuerySession(mensaje.TenantId.ToString("N")))
        {
            lote = await consulta.LoadAsync<LoteDeInvitacion>(mensaje.LoteId, ct);
            if (lote is not { Estado: EstadoLote.Procesando } || lote.Ejecucion != mensaje.Ejecucion)
                return;
            var loteId = lote.Id;
            filas = await consulta.Query<FilaDeLote>()
                .Where(f => f.LoteId == loteId && f.Estado == EstadoFilaLote.Pendiente)
                .OrderBy(f => f.N).Take(tamano).ToListAsync(ct);
        }

        var catalogo = await CatalogoDeLote.CargarAsync(store, mensaje.TenantId, lote.EmpresaId, ct);
        Guid? sandbox = await entornos.EsSandboxAsync(mensaje.TenantId, ct) ? mensaje.TenantId : null; // terceros de pruebas (SB-4)
        MetricasDeLotes.Tandas.Add(1);
        for (var i = 0; i < filas.Count; i++)
        {
            // Cada 10 filas mira si lo pausaron o cancelaron: la pausa no espera a que termine la tanda.
            if (i > 0 && i % 10 == 0 && !await SigueVigenteAsync(store, mensaje, ct))
                break;
            await ProcesarFilaAsync(sesiones, runtime, protector, plan, mensaje.TenantId, sandbox, lote, filas[i], catalogo, reloj, logger, ct);
        }

        // Avance de la tanda y, si queda trabajo, la siguiente (por el outbox de este mensaje).
        mensajes.TenantId = mensaje.TenantId.ToString("N");
        await using var sesion = sesiones.ParaTenant(mensaje.TenantId, mensajes);
        sesion.SetHeader("actorTipo", nameof(ActorTipo.Sistema));
        sesion.SetHeader("actorId", "invitacion-masiva");
        var stream = await sesion.Events.FetchForExclusiveWriting<LoteDeInvitacion>(mensaje.LoteId, ct);
        if (stream.Aggregate is not { } actual)
            return;
        var eventos = actual.RegistrarAvance(await ContarAsync(sesion, actual.Id, ct), reloj.GetUtcNow());
        stream.AppendMany(eventos);
        if (eventos.OfType<LoteTerminado>().Any() is false && actual is { Estado: EstadoLote.Procesando } && actual.Ejecucion == mensaje.Ejecucion)
            await mensajes.PublishAsync(mensaje);
        await sesion.SaveChangesAsync(ct);
    }

    private static async Task<bool> SigueVigenteAsync(IDocumentStore store, ProcesarTandaDeLote mensaje, CancellationToken ct)
    {
        await using var consulta = store.QuerySession(mensaje.TenantId.ToString("N"));
        return await consulta.LoadAsync<LoteDeInvitacion>(mensaje.LoteId, ct) is { Estado: EstadoLote.Procesando } lote && lote.Ejecucion == mensaje.Ejecucion;
    }

    /// <summary>Conteo de filas por estado (sin cargarlas).</summary>
    public static async Task<TotalesDeLote> ContarAsync(IQuerySession sesion, Guid loteId, CancellationToken ct)
    {
        async Task<int> Contar(EstadoFilaLote estado) => await sesion.Query<FilaDeLote>().CountAsync(f => f.LoteId == loteId && f.Estado == estado, ct);
        var pendientes = await Contar(EstadoFilaLote.Pendiente);
        var exitosas = await Contar(EstadoFilaLote.Exitosa);
        var fallidas = await Contar(EstadoFilaLote.Fallida);
        var duplicadas = await Contar(EstadoFilaLote.Duplicada);
        var invalidas = await Contar(EstadoFilaLote.Invalida);
        return new TotalesDeLote(pendientes + exitosas + fallidas + duplicadas + invalidas, pendientes, exitosas, fallidas, duplicadas, invalidas);
    }

    private static async Task ProcesarFilaAsync(ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector, Validasoft.Aplicacion.Plataforma.ControlDePlan plan, Guid tenantId,
        Guid? sandbox, LoteDeInvitacion lote, FilaDeLote fila, CatalogoDeLote catalogo, TimeProvider reloj, ILogger logger, CancellationToken ct)
    {
        var cronometro = Stopwatch.StartNew();
        for (var intento = 1; ; intento++)
        {
            var ahora = reloj.GetUtcNow();
            try
            {
                // Un contexto de mensajería por fila: el correo de su invitación sale solo si su transacción se confirma.
                var bus = new MessageContext(runtime) { TenantId = tenantId.ToString("N") };
                await using var sesion = sesiones.ParaTenant(tenantId, bus);
                // En nombre de quien creó el lote: el historial del vínculo y la separación de funciones lo ven como su invitación.
                sesion.SetHeader("actorTipo", lote.CreadoPor.Tipo.ToString());
                sesion.SetHeader("actorId", lote.CreadoPor.Id);
                sesion.SetHeader("loteId", lote.Id.ToString());
                var actual = await sesion.LoadAsync<FilaDeLote>(fila.Id, ct);
                if (actual is not { Estado: EstadoFilaLote.Pendiente })
                    return;

                var resultado = await InvitarAsync(sesion, bus, protector, tenantId, sandbox, lote, actual.Datos, catalogo, ahora, ct, plan);
                actual.MarcarExitosa(resultado.Accion, resultado.TerceroId, resultado.VinculoId, resultado.TerceroCreado, resultado.Detalle, ahora);
                sesion.Store(actual);
                await sesion.SaveChangesAsync(ct);
                MetricasDeLotes.Filas.Add(1, new KeyValuePair<string, object?>("resultado", "exitosa"));
                MetricasDeLotes.DuracionDeFila.Record(cronometro.Elapsed.TotalMilliseconds);
                return;
            }
            catch (ReglaDeNegocioException e)
            {
                await MarcarFallidaAsync(sesiones, tenantId, fila.Id, new ErrorDeFila(null, e.Codigo, e.Message), ahora, ct);
                MetricasDeLotes.Filas.Add(1, new KeyValuePair<string, object?>("resultado", "fallida"), new KeyValuePair<string, object?>("codigo", e.Codigo));
                return;
            }
            catch (Exception e) when (e is not OperationCanceledException && intento < Intentos)
            {
                // Una carrera (otro proceso creó el mismo tercero o escribió en el vínculo): se repite con datos frescos.
                logger.LogDebug(e, "Reintento de la fila {Fila} del lote {Lote}.", fila.N, lote.Id);
                MetricasDeLotes.Reintentos.Add(1, new KeyValuePair<string, object?>("excepcion", e.GetType().Name));
                await Task.Delay(Random.Shared.Next(20, 100 * intento), ct);
            }
            catch (Exception e) when (e is not OperationCanceledException)
            {
                logger.LogWarning(e, "La fila {Fila} del lote {Lote} falló.", fila.N, lote.Id);
                await MarcarFallidaAsync(sesiones, tenantId, fila.Id, new ErrorDeFila(null, "error-inesperado", "No se pudo procesar la fila; reinténtala."),
                    ahora, ct);
                MetricasDeLotes.Filas.Add(1, new KeyValuePair<string, object?>("resultado", "fallida"), new KeyValuePair<string, object?>("codigo", "error-inesperado"));
                return;
            }
        }
    }

    private static async Task MarcarFallidaAsync(ISesiones sesiones, Guid tenantId, string filaId, ErrorDeFila error, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.ParaTenant(tenantId);
        if (await sesion.LoadAsync<FilaDeLote>(filaId, ct) is not { Estado: EstadoFilaLote.Pendiente } fila)
            return;
        fila.MarcarFallida(error, ahora);
        sesion.Store(fila);
        await sesion.SaveChangesAsync(ct);
    }

    /// <summary>
    /// Crea o reutiliza el tercero (por su identificación normalizada; si ya existe no se tocan sus datos maestros, P-1-7) y el
    /// vínculo de la empresa, e invita: vínculo nuevo ⇒ <c>VinculoCreado</c> + <c>InvitacionEnviada(LoteId)</c>; vínculo que aún
    /// no acepta ⇒ reenvío al correo de la fila; tercero que ya aceptó ⇒ nada (<c>SinCambios</c>). Bloqueado o terminado ⇒ 409.
    /// </summary>
    public static async Task<ResultadoDeFila> InvitarAsync(IDocumentSession sesion, IMessageBus mensajes, IProtectorDeSecretos protector, Guid tenantId,
        Guid? sandbox, LoteDeInvitacion lote, FilaDeLoteEntrada datos, CatalogoDeLote catalogo, DateTimeOffset ahora, CancellationToken ct,
        Validasoft.Aplicacion.Plataforma.ControlDePlan? plan = null)
    {
        var tipo = catalogo.Tipo(datos.TipoVinculo)
                   ?? throw ReglaDeNegocioException.Validacion("tipo-vinculo-invalido", $"El tipo de vínculo «{datos.TipoVinculo}» no existe o está inactivo.");
        var identificacion = Identificacion.Crear(datos.Pais ?? "CO", datos.TipoIdentificacion ?? string.Empty, datos.NumeroIdentificacion ?? string.Empty,
            datos.DigitoVerificacion);
        var clave = AislamientoDeSandbox.ClaveDeTercero(identificacion.Clave, sandbox);
        var vigencia = TimeSpan.FromHours(lote.VigenciaHoras);

        var tercero = await sesion.Query<Tercero>().FirstOrDefaultAsync(t => t.ClaveIdentificacion == clave, ct);
        var terceroCreado = tercero is null;
        if (tercero is null)
        {
            var tipoPersona = ValidacionDeLote.TipoPersonaDe(datos, identificacion)
                              ?? throw ReglaDeNegocioException.Validacion("tipo-persona-invalido", "El tipo de persona es Natural, Juridica o Consorcio.");
            var contactos = datos.Contacto is null ? null : new[] { new ContactoDto(datos.Contacto, datos.Correo!, datos.Telefono, "Contacto") };
            tercero = TercerosEndpoints.ConstruirTercero(new CrearTerceroDto(new IdentificacionDto(identificacion.Pais, identificacion.Tipo,
                identificacion.Numero, identificacion.DigitoVerificacion), tipoPersona.ToString(), datos.Nombre ?? string.Empty, datos.NombreComercial, contactos),
                tenantId, sandbox is not null, ahora);
            sesion.Store(tercero);
            ContactosDelTercero.RegistrarIniciales(sesion, tercero, ahora);
        }

        var terceroId = tercero.Id;
        var empresaId = lote.EmpresaId;
        var tipoId = tipo.Id;
        var existente = await sesion.Query<Vinculo>().FirstOrDefaultAsync(v => v.EmpresaId == empresaId && v.TerceroId == terceroId && v.TipoVinculoId == tipoId, ct);
        if (existente is null)
        {
            // El vínculo nuevo cuenta para el límite del plan: con el bloqueo suave y el límite alcanzado, la fila falla (se reintenta al ampliar el plan, OD-17).
            if (plan is not null)
                await plan.ExigirAsync(tenantId, Validasoft.Dominio.Plataforma.MetricaDePlan.VinculosActivos, 1, ct);
            var creado = Vinculo.Crear(empresaId, terceroId, tipoId, catalogo.Flujo(datos.Flujo, tipoId), ValidadorDeAtributos.Validar(tipo, null),
                OrigenVinculo.Invitacion, null,
                datos.Correo ?? string.Empty, ahora);
            var vinculo = Vinculo.Create(creado);
            var invitacion = vinculo.EnviarInvitacion(null, vigencia, lote.Id, ahora);
            await EnlacesDeAcceso.InvitarAsync(sesion, mensajes, protector, tenantId, vinculo, tipo, invitacion, ct);
            // El correo de la fila: contacto conocido, primer contacto del tercero o desconocido (acceso aislado hasta que lo apruebe un contacto).
            List<object> eventos = [creado, invitacion];
            await AccesoAislado.RegistrarInvitacionAsync(sesion, mensajes, protector, tenantId, tercero, terceroCreado, vinculo, creado.CorreoInvitacion, eventos.Add,
                ahora, ct);
            sesion.Events.StartStream<Vinculo>(creado.VinculoId, eventos);
            return new ResultadoDeFila(AccionFilaLote.Invitado, terceroId, creado.VinculoId, terceroCreado, null);
        }

        var stream = await sesion.Events.FetchForExclusiveWriting<Vinculo>(existente.Id, ct);
        var actual = stream.Aggregate!;
        var (accion, error) = ValidacionDeLote.AccionPrevista(actual);
        if (error is not null)
            throw ReglaDeNegocioException.Conflicto(error.Codigo, error.Mensaje);
        if (accion == nameof(AccionFilaLote.Reinvitado))
        {
            var invitacion = actual.EnviarInvitacion(datos.Correo, vigencia, lote.Id, ahora);
            await EnlacesDeAcceso.InvitarAsync(sesion, mensajes, protector, tenantId, actual, tipo, invitacion, ct);
            stream.AppendOne(invitacion);
            // Reinvitar a otro correo vuelve a decidir su acceso (aislado si no es contacto conocido del tercero).
            await AccesoAislado.RegistrarInvitacionAsync(sesion, mensajes, protector, tenantId, tercero, terceroCreado, actual, invitacion.CorreoDestino, stream.AppendOne,
                ahora, ct);
            return new ResultadoDeFila(AccionFilaLote.Reinvitado, terceroId, actual.Id, terceroCreado, null);
        }

        return new ResultadoDeFila(AccionFilaLote.SinCambios, terceroId, actual.Id, terceroCreado,
            $"El tercero ya aceptó la invitación (vínculo {actual.Estado}); no se le reenvía.");
    }
}

/// <summary>Tipos de vínculo del tenant y flujos de la empresa, cargados una vez por tanda.</summary>
public sealed class CatalogoDeLote(IReadOnlyDictionary<string, TipoVinculo> tipos, IReadOnlyDictionary<string, Flujo> flujos)
{
    public static async Task<CatalogoDeLote> CargarAsync(IDocumentStore store, Guid tenantId, Guid empresaId, CancellationToken ct)
    {
        await using var consulta = store.QuerySession(tenantId.ToString("N"));
        var tipos = (await consulta.Query<TipoVinculo>().ToListAsync(ct)).Where(t => t.Activo).ToDictionary(t => t.Codigo, StringComparer.OrdinalIgnoreCase);
        var flujos = (await consulta.Query<Flujo>().Where(f => f.EmpresaId == empresaId).ToListAsync(ct)).ToDictionary(f => f.Codigo, StringComparer.OrdinalIgnoreCase);
        return new CatalogoDeLote(tipos, flujos);
    }

    public TipoVinculo? Tipo(string? codigo) => codigo is not null && tipos.TryGetValue(codigo, out var tipo) ? tipo : null;

    /// <summary>El flujo indicado (de la empresa, del tipo y publicado); 422 <c>flujo-invalido</c> si no sirve; nulo si no se indicó.</summary>
    public Guid? Flujo(string? codigo, Guid tipoVinculoId) =>
        codigo is null ? null
        : flujos.TryGetValue(codigo, out var flujo) && flujo.TipoVinculoId == tipoVinculoId && flujo.VersionPublicada is not null ? flujo.Id
        : throw ReglaDeNegocioException.Validacion("flujo-invalido", $"El flujo «{codigo}» no existe en la empresa para ese tipo de vínculo o no está publicado.");
}
