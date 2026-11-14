using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine;
using Wolverine.Http;
using Wolverine.Runtime;

namespace Validasoft.Aplicacion.Interesado;

/// <summary>Una migración de datos que ya corrió (una fila por migración, esquema <c>plataforma</c>): las de arranque corren una sola vez.</summary>
public sealed class MigracionDeDatosEjecutada
{
    public string Id { get; set; } = string.Empty;

    public DateTimeOffset EjecutadaEn { get; set; }

    public string Resumen { get; set; } = string.Empty;
}

/// <summary>Lo que hizo la migración de los vínculos aceptados antes del acceso aislado.</summary>
public sealed record ResultadoDeMigracionDeAcceso(int TercerosRevisados, int ContactosVerificados, int ContactosSinVerificar, int VinculosAislados, int Sincronizados)
{
    public string Resumen =>
        $"{TercerosRevisados} terceros revisados: {ContactosVerificados} contactos verificados, {ContactosSinVerificar} primeros contactos, {VinculosAislados} vínculos en acceso aislado, " +
        $"{Sincronizados} terceros puestos al día.";
}

/// <summary>Pide correr la migración de los vínculos aceptados antes del acceso aislado (la publica el arranque y el endpoint de Plataforma).</summary>
public sealed record MigrarAccesoAislado;

public static class MigrarAccesoAisladoHandler
{
    public static async Task Handle(MigrarAccesoAislado mensaje, ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector, ICacheDeSesionesDeTercero cache,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var resultado = await MigracionDeAccesoAislado.EjecutarAsync(sesiones, runtime, protector, cache, almacen, reglas, contexto, reloj.GetUtcNow(), ct);
        await MigracionDeAccesoAislado.MarcarEjecutadaAsync(sesiones, resultado, reloj.GetUtcNow(), ct);
    }
}

/// <summary>
/// Migración de datos, única e idempotente, de lo que quedó de <b>antes</b> del acceso aislado (decisión AV del registro). Hasta entonces el correo de cualquier vínculo que
/// había aceptado su invitación se reconocía solo como contacto del tercero (no se bloqueaba a quien ya entraba): quien hubiera invitado a su propio correo sobre el NIT de otro
/// quedó con acceso completo. Ya no se reconoce solo; esta migración revisa cada tercero y, por cada correo de un vínculo aceptado que no es contacto:
/// <list type="bullet">
/// <item>si es <b>legítimo según las reglas nuevas</b> —es verificable (el correo del RUT o de la Cámara, o dos grupos lo invitaron por su cuenta) o es el primer contacto real del
/// tercero (el tercero no tiene contactos y es el correo del primer vínculo aceptado del grupo que lo creó)—, lo convierte en contacto, con la verificación que corresponda;</item>
/// <item>si no, deja ese vínculo en <b>acceso aislado pendiente</b> y avisa como en el flujo normal (los contactos verificados, o la cola de la consola de Plataforma si no hay ninguno).</item>
/// </list>
/// Después pone al día cada tercero con contactos (<see cref="SincronizacionDeAccesoDelTercero"/>): verifica los contactos que ya tienen con qué y desplaza a los sin verificar.
/// Repetirla no cambia nada: un correo que ya es contacto o ya está aislado no se toca otra vez.
/// </summary>
public static class MigracionDeAccesoAislado
{
    public const string Nombre = "acceso-aislado-vinculos-aceptados";

    private const int TamanoDeLote = 200;

    public static async Task<bool> YaCorrioAsync(IQuerySession consulta, CancellationToken ct) =>
        await consulta.LoadAsync<MigracionDeDatosEjecutada>(Nombre, ct) is not null;

    public static async Task MarcarEjecutadaAsync(ISesiones sesiones, ResultadoDeMigracionDeAcceso resultado, DateTimeOffset ahora, CancellationToken ct)
    {
        await using var sesion = sesiones.Global();
        sesion.Store(new MigracionDeDatosEjecutada { Id = Nombre, EjecutadaEn = ahora, Resumen = resultado.Resumen });
        await sesion.SaveChangesAsync(ct);
    }

    public static async Task<ResultadoDeMigracionDeAcceso> EjecutarAsync(ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector, ICacheDeSesionesDeTercero cache,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, IContextoSolicitud contexto, DateTimeOffset ahora, CancellationToken ct)
    {
        // Los vínculos aceptados que entraban solo por su correo (sin acceso aislado), de todos los grupos, y las empresas para saber de qué grupo es cada uno.
        Dictionary<Guid, List<Vinculo>> aceptadosPorTercero;
        Dictionary<Guid, Empresa> empresas;
        await using (var consulta = sesiones.ConsultaGlobal())
        {
            var aceptados = (await consulta.Query<Vinculo>().Where(v => v.AnyTenant() && v.InvitacionAceptada).ToListAsync(ct)).Where(v => v.AccesoAislado is null).ToList();
            aceptadosPorTercero = aceptados.GroupBy(v => v.TerceroId).ToDictionary(g => g.Key, g => g.OrderBy(v => v.InvitacionAceptadaEn ?? v.CreadoEn).ToList());
            var empresaIds = aceptados.Select(v => v.EmpresaId).Distinct().ToArray();
            empresas = empresaIds.Length == 0 ? [] : (await consulta.LoadManyAsync<Empresa>(ct, empresaIds)).ToDictionary(e => e.Id);
        }

        int revisados = 0, verificados = 0, primeros = 0, aislados = 0, sincronizados = 0;
        for (var pagina = 0; ; pagina++)
        {
            List<Tercero> lote;
            await using (var consulta = sesiones.ConsultaGlobal())
                lote = [.. await consulta.Query<Tercero>().OrderBy(t => t.Id).Skip(pagina * TamanoDeLote).Take(TamanoDeLote).ToListAsync(ct)];
            if (lote.Count == 0)
                break;

            foreach (var tercero in lote)
            {
                var vinculos = aceptadosPorTercero.GetValueOrDefault(tercero.Id, []);
                if (tercero.SandboxTenantId is not null || (vinculos.Count == 0 && tercero.Contactos.Count == 0))
                    continue;

                revisados++;

                var (v, p, a) = await MigrarTerceroAsync(tercero.Id, vinculos, empresas, sesiones, runtime, protector, ahora, ct);
                verificados += v;
                primeros += p;
                aislados += a;

                var resultado = await SincronizacionDeAccesoDelTercero.EjecutarAsync(tercero.Id, sesiones, runtime, protector, cache, almacen, reglas, contexto, ahora, ct);
                if (resultado != ResultadoDeSincronizacion.Vacio)
                    sincronizados++;
            }
        }

        return new ResultadoDeMigracionDeAcceso(revisados, verificados, primeros, aislados, sincronizados);
    }

    private static async Task<(int Verificados, int Primeros, int Aislados)> MigrarTerceroAsync(Guid terceroId, IReadOnlyList<Vinculo> vinculos, IReadOnlyDictionary<Guid, Empresa> empresas,
        ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector, DateTimeOffset ahora, CancellationToken ct)
    {
        int verificados = 0, primeros = 0;
        List<Vinculo> sinContacto;
        await using (var sesion = sesiones.Boveda())
        {
            var tercero = await sesion.LoadAsync<Tercero>(terceroId, ct);
            if (tercero is null)
                return (0, 0, 0);

            // Los correos de vínculos aceptados que no son contacto del tercero, del más antiguo al más nuevo.
            sinContacto = [.. vinculos.Where(v => !tercero.TieneContacto(v.CorreoTercero))];
            if (sinContacto.Count > 0)
            {
                var oficiales = await ContactosDelTercero.CorreosOficialesAsync(sesion, terceroId, ct);
                foreach (var correo in sinContacto.Select(v => SolicitudDeAcceso.Normalizar(v.CorreoTercero)).Distinct(StringComparer.Ordinal).ToList())
                {
                    var grupos = await ContactosDelTercero.GruposQueInvitaronAsync(sesion, terceroId, correo, null, ct);
                    if (VerificacionDeContactos.Medio(correo, oficiales, grupos, tercero.TieneContactoVerificado) is { } medio &&
                        ContactosDelTercero.Agregar(sesion, tercero, correo, tercero.Nombre, "Principal", ContactosDelTercero.OrigenEmpresa, "Migración de vínculos aceptados", medio, null, ahora))
                        verificados++;
                }

                // El primer contacto real: el tercero no tiene ninguno y este es el correo del primer vínculo aceptado del grupo que lo creó.
                if (tercero.Contactos.Count == 0 && tercero.CreadoPorTenant is { } creador)
                {
                    var primero = sinContacto.FirstOrDefault(v => empresas.TryGetValue(v.EmpresaId, out var e) && e.TenantId == creador);
                    if (primero is not null && ContactosDelTercero.Agregar(sesion, tercero, primero.CorreoTercero, tercero.Nombre, "Principal", ContactosDelTercero.OrigenEmpresa,
                            "Migración de vínculos aceptados", null, null, ahora))
                        primeros++;
                }

                await sesion.SaveChangesAsync(ct);
                sinContacto = [.. sinContacto.Where(v => !tercero.TieneContacto(v.CorreoTercero))];
            }
        }

        // Los demás quedan en acceso aislado pendiente, con el aviso de siempre (la misma regla que una invitación nueva).
        var aislados = 0;
        foreach (var vinculo in sinContacto)
            if (empresas.TryGetValue(vinculo.EmpresaId, out var empresa) && await AislarAsync(vinculo, empresa.TenantId, sesiones, runtime, protector, ahora, ct))
                aislados++;
        return (verificados, primeros, aislados);
    }

    /// <summary>El vínculo aceptado pasa a acceso aislado pendiente: la misma decisión que al invitar ese correo hoy. Idempotente: uno que ya está aislado no se toca.</summary>
    private static Task<bool> AislarAsync(Vinculo vinculo, Guid tenantId, ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector, DateTimeOffset ahora, CancellationToken ct) =>
        Peticiones.ConReintentosAsync(async () =>
        {
            var mensajes = new MessageContext(runtime); // cada intento con su contexto: los correos salen por el outbox de su transacción
            await using var sesion = sesiones.ParaTenant(tenantId, mensajes);
            var stream = await sesion.Events.FetchForWriting<Vinculo>(vinculo.Id, ct);
            if (stream.Aggregate is not { AccesoAislado: null } actual)
                return false;

            var tercero = await sesion.LoadAsync<Tercero>(actual.TerceroId, ct);
            if (tercero is null || tercero.TieneContacto(actual.CorreoTercero))
                return false;

            var nivel = await AccesoAislado.RegistrarInvitacionAsync(sesion, mensajes, protector, tenantId, tercero, terceroNuevo: false, actual, actual.CorreoTercero, stream.AppendOne,
                ahora, ct);
            await sesion.SaveChangesAsync(ct);
            return nivel == NivelDeAcceso.Aislado;
        });
}

/// <summary>
/// Al arrancar corre una sola vez la migración de los vínculos aceptados antes del acceso aislado (<see cref="MigracionDeAccesoAislado"/>): si no hay marca de que ya corrió,
/// pide la migración al bus; el manejador la ejecuta y deja la marca. Solo el nodo líder; si el esquema aún no existe (entornos sin creación automática), lo advierte y la
/// pide en el siguiente arranque. Apagable con <c>Validasoft:Migraciones:AccesoAislado:Habilitada</c> en <c>false</c>.
/// </summary>
public sealed class MigracionDeAccesoAisladoAlArrancar(IServiceProvider servicios, IConfiguration configuracion, ILogger<MigracionDeAccesoAisladoAlArrancar> logger) : BackgroundService
{
    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuracion.GetValue("Validasoft:Migraciones:AccesoAislado:Habilitada", true))
            return;

        // Wolverine y la base necesitan haber arrancado: se espera un poco y, si aún no están, se reintenta unas veces.
        var espera = configuracion.GetValue("Validasoft:Migraciones:AccesoAislado:Espera", TimeSpan.FromSeconds(15));
        try
        {
            for (var intento = 1; intento <= 6; intento++)
            {
                await Task.Delay(espera, stoppingToken);
                try
                {
                    await using var alcance = servicios.CreateAsyncScope();
                    var store = alcance.ServiceProvider.GetRequiredService<IDocumentStore>();
                    await using (var consulta = store.QuerySession())
                        if (await MigracionDeAccesoAislado.YaCorrioAsync(consulta, stoppingToken))
                            return;

                    await alcance.ServiceProvider.GetRequiredService<IMessageBus>().PublishAsync(new MigrarAccesoAislado());
                    logger.LogInformation("Se pidió la migración de los vínculos aceptados antes del acceso aislado.");
                    return;
                }
                catch (Exception e) when (e is not OperationCanceledException)
                {
                    logger.LogWarning(e, "No se pudo pedir la migración de los vínculos aceptados antes del acceso aislado (intento {Intento}).", intento);
                }
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado de la API.
        }
    }
}

/// <summary>Ejecución inmediata de la migración (Plataforma): para repetirla a mano (es idempotente) y para operar.</summary>
public static class MigracionDeAccesoAisladoEndpoints
{
    /// <summary>Corre ya la migración de los vínculos aceptados antes del acceso aislado y devuelve lo que hizo. Idempotente.</summary>
    [WolverinePost("/api/v1/plataforma/acceso-aislado/migrar")]
    [Authorize(Policy = Permisos.PlataformaTenants)]
    public static async Task<ResultadoDeMigracionDeAccesoDto> Migrar(ISesiones sesiones, IWolverineRuntime runtime, IProtectorDeSecretos protector, ICacheDeSesionesDeTercero cache,
        IAlmacenDeArchivos almacen, ReglasDeCarga reglas, IContextoSolicitud contexto, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var resultado = await MigracionDeAccesoAislado.EjecutarAsync(sesiones, runtime, protector, cache, almacen, reglas, contexto, ahora, ct);
        await MigracionDeAccesoAislado.MarcarEjecutadaAsync(sesiones, resultado, ahora, ct);
        await using var sesion = sesiones.Global();
        Auditoria.Registrar(sesion, contexto, "MigracionDeDatos", MigracionDeAccesoAislado.Nombre, AccionConfiguracion.Modificado, null, resultado, ahora);
        await sesion.SaveChangesAsync(ct);
        return new ResultadoDeMigracionDeAccesoDto(resultado.TercerosRevisados, resultado.ContactosVerificados, resultado.ContactosSinVerificar, resultado.VinculosAislados,
            resultado.Sincronizados);
    }
}
