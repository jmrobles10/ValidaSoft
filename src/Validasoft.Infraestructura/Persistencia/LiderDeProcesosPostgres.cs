using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Validasoft.Aplicacion.Comun;

namespace Validasoft.Infraestructura.Persistencia;

/// <summary>
/// Elige al nodo líder de los procesos periódicos con un <b>bloqueo consultivo de PostgreSQL</b> (<c>pg_try_advisory_lock</c>, OC-4): el nodo
/// que lo toma es el líder mientras conserve su conexión. Si el nodo cae (o pierde la conexión) PostgreSQL libera el bloqueo y otra réplica lo
/// toma en su siguiente intento. Es el mismo mecanismo con el que Marten elige al nodo del daemon asíncrono (<c>HotCold</c>), con una llave propia
/// para que ambos liderazgos no se confundan. La conexión es dedicada y sin pool: un bloqueo de sesión no debe devolverse a un pool.
/// </summary>
public sealed class LiderDeProcesosPostgres : BackgroundService, ILiderDeProcesos
{
    /// <summary>Llave del bloqueo consultivo de los procesos periódicos de Validasoft (distinta de las del daemon de Marten y de Wolverine).</summary>
    public const long LlavePorOmision = 0x56414C50524F4331; // «VALPROC1»

    private readonly string _cadena;
    private readonly long _llave;
    private readonly TimeSpan _espera;
    private readonly ILogger<LiderDeProcesosPostgres> _logger;
    private NpgsqlConnection? _conexion;
    private volatile bool _esLider;

    public LiderDeProcesosPostgres(string cadenaDeConexion, long llave, TimeSpan espera, ILogger<LiderDeProcesosPostgres> logger)
    {
        _cadena = new NpgsqlConnectionStringBuilder(cadenaDeConexion) { Pooling = false, KeepAlive = 10, Timeout = 10, CommandTimeout = 10 }.ConnectionString;
        _llave = llave;
        _espera = espera;
        _logger = logger;
    }

    /// <summary>Lee <c>Validasoft:Lider:SegundosEspera</c> (cada cuánto un nodo que no es líder reintenta; 10 por omisión) y la llave de <c>Validasoft:Lider:Llave</c>.</summary>
    public static LiderDeProcesosPostgres Crear(IConfiguration configuracion, ILogger<LiderDeProcesosPostgres> logger) => new(
        configuracion.GetConnectionString("Validasoft") ?? throw new InvalidOperationException("Falta ConnectionStrings:Validasoft."),
        configuracion.GetValue("Validasoft:Lider:Llave", LlavePorOmision),
        TimeSpan.FromSeconds(Math.Clamp(configuracion.GetValue("Validasoft:Lider:SegundosEspera", 10), 1, 300)), logger);

    public bool EsLider => _esLider;

    public event Action<bool>? LiderazgoCambio;

    /// <summary>El primer intento es del arranque: un nodo solo ya es líder antes de que los demás servicios hagan su primera pasada.</summary>
    public override async Task StartAsync(CancellationToken cancellationToken)
    {
        await IntentarAsync(cancellationToken);
        await base.StartAsync(cancellationToken);
    }

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        // El líder comprueba su conexión cada pocos segundos (no más de 2): es el tiempo máximo en que un líder que perdió su bloqueo cree seguir
        // siéndolo. Un nodo que no es líder reintenta cada <_espera>.
        using var temporizador = new PeriodicTimer(TimeSpan.FromMilliseconds(Math.Min(_espera.TotalMilliseconds, 2000)));
        var proximoIntento = DateTime.UtcNow + _espera;
        try
        {
            while (await temporizador.WaitForNextTickAsync(stoppingToken))
            {
                if (_conexion is null && DateTime.UtcNow < proximoIntento)
                    continue;
                await IntentarAsync(stoppingToken);
                proximoIntento = DateTime.UtcNow + _espera;
            }
        }
        catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
        {
            // Apagado del nodo.
        }
    }

    public override async Task StopAsync(CancellationToken cancellationToken)
    {
        await base.StopAsync(cancellationToken);
        await SoltarAsync();
    }

    /// <summary>Si es líder, comprueba que su conexión siga viva; si no, intenta tomar el bloqueo.</summary>
    private async Task IntentarAsync(CancellationToken ct)
    {
        try
        {
            if (_conexion is not null)
            {
                // Latido: una conexión caída pierde el bloqueo en el servidor, así que se deja el liderazgo y se vuelve a competir.
                await using var latido = new NpgsqlCommand("select 1", _conexion);
                await latido.ExecuteScalarAsync(ct);
                return;
            }

            var conexion = new NpgsqlConnection(_cadena);
            try
            {
                await conexion.OpenAsync(ct);
                await using var comando = new NpgsqlCommand("select pg_try_advisory_lock(@llave)", conexion);
                comando.Parameters.AddWithValue("llave", _llave);
                if (await comando.ExecuteScalarAsync(ct) is true)
                {
                    _conexion = conexion;
                    CambiarA(true);
                    return;
                }
            }
            catch
            {
                await conexion.DisposeAsync();
                throw;
            }

            await conexion.DisposeAsync(); // otro nodo es el líder
        }
        catch (Exception e) when (e is not OperationCanceledException || !ct.IsCancellationRequested)
        {
            if (_conexion is not null)
            {
                _logger.LogWarning(e, "El nodo líder de los procesos periódicos perdió su conexión con PostgreSQL y deja el liderazgo.");
                await SoltarAsync();
            }
            else
            {
                _logger.LogWarning(e, "No se pudo consultar el liderazgo de los procesos periódicos; se reintenta en {Espera}.", _espera);
            }
        }
    }

    private async Task SoltarAsync()
    {
        var conexion = Interlocked.Exchange(ref _conexion, null);
        if (conexion is null)
            return;
        CambiarA(false);
        try
        {
            await using var comando = new NpgsqlCommand("select pg_advisory_unlock(@llave)", conexion);
            comando.Parameters.AddWithValue("llave", _llave);
            await comando.ExecuteScalarAsync(CancellationToken.None);
        }
        catch (Exception e) when (e is NpgsqlException or InvalidOperationException)
        {
            // La conexión ya estaba caída: al cerrarse, PostgreSQL libera el bloqueo de sesión.
        }
        finally
        {
            await conexion.DisposeAsync();
        }
    }

    private void CambiarA(bool lider)
    {
        if (_esLider == lider)
            return;
        _esLider = lider;
        _logger.LogInformation(lider ? "Este nodo es ahora el líder de los procesos periódicos." : "Este nodo dejó de ser el líder de los procesos periódicos.");
        try
        {
            LiderazgoCambio?.Invoke(lider);
        }
        catch (Exception e)
        {
            _logger.LogError(e, "Falló un suscriptor del cambio de liderazgo.");
        }
    }
}
