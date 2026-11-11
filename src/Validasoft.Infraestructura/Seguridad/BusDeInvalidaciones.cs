using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Hosting;
using Microsoft.Extensions.Logging;
using Npgsql;
using Validasoft.Aplicacion.Comun;

namespace Validasoft.Infraestructura.Seguridad;

/// <summary>
/// Aviso entre servidores con PostgreSQL <c>LISTEN/NOTIFY</c> (PI-8): cuando una API key se revoca o rota, un sistema se desactiva o una sesión se cierra,
/// <b>todos</b> los servidores descartan lo que tienen en memoria de inmediato, no solo el que atendió el cambio. El canal es
/// <see cref="Canal"/> y el mensaje <c>{nodo}|{tipo}|{id}</c>; un servidor ignora lo que él mismo publicó (ya olvidó por su lado).
/// <para>
/// Publicar nunca falla la petición: si la base no responde, se registra y las cachés siguen venciendo por su cuenta (5 minutos las llaves,
/// segundos lo demás), que es como era antes de este aviso.
/// </para>
/// </summary>
public sealed class BusDeInvalidaciones(IConfiguration configuracion, ILogger<BusDeInvalidaciones> logger) : IBusDeInvalidaciones
{
    public const string Canal = "validasoft_invalidaciones";

    /// <summary>Identifica a este servidor para no atender su propio aviso.</summary>
    public string Nodo { get; } = Guid.NewGuid().ToString("N");

    public string CadenaDeConexion =>
        configuracion.GetConnectionString("Validasoft") ?? throw new InvalidOperationException("Falta ConnectionStrings:Validasoft.");

    public void Publicar(string tipo, string id) => _ = PublicarSinFallarAsync(tipo, id, CancellationToken.None);

    public Task PublicarAsync(string tipo, string id, CancellationToken ct = default) => PublicarSinFallarAsync(tipo, id, ct);

    private async Task PublicarSinFallarAsync(string tipo, string id, CancellationToken ct)
    {
        try
        {
            await EnviarAsync(Nodo, tipo, id, ct);
        }
        catch (Exception e) when (e is not OperationCanceledException)
        {
            logger.LogWarning(e, "No se pudo avisar a los demás servidores que cambió {Tipo} {Id}; sus cachés vencerán solas", tipo, id);
        }
    }

    /// <summary>Envía el aviso a los demás con el nodo indicado (las pruebas lo envían como si fuera otro servidor).</summary>
    public async Task EnviarAsync(string nodo, string tipo, string id, CancellationToken ct)
    {
        await using var conexion = new NpgsqlConnection(CadenaDeConexion);
        await conexion.OpenAsync(ct);
        await using var comando = new NpgsqlCommand("select pg_notify(@canal, @mensaje)", conexion);
        comando.Parameters.AddWithValue("canal", Canal);
        comando.Parameters.AddWithValue("mensaje", $"{nodo}|{tipo}|{id}");
        await comando.ExecuteNonQueryAsync(ct);
    }
}

/// <summary>
/// Escucha el canal de <see cref="BusDeInvalidaciones"/> en una conexión propia y entrega cada aviso de <b>otro</b> servidor al
/// <see cref="IReceptorDeInvalidaciones"/> de su tipo. Si la conexión se cae reconecta con espera creciente y, como pudo perder avisos, pide a cada
/// receptor que descarte lo anterior (<see cref="IReceptorDeInvalidaciones.Reiniciar"/>).
/// </summary>
public sealed class EscuchaDeInvalidaciones(BusDeInvalidaciones bus, IEnumerable<IReceptorDeInvalidaciones> receptores, IConfiguration configuracion,
    ILogger<EscuchaDeInvalidaciones> logger) : BackgroundService
{
    private readonly Dictionary<string, List<IReceptorDeInvalidaciones>> _porTipo =
        receptores.GroupBy(r => r.Tipo).ToDictionary(g => g.Key, g => g.ToList());

    protected override async Task ExecuteAsync(CancellationToken stoppingToken)
    {
        if (!configuracion.GetValue("Validasoft:Invalidaciones:Activo", true))
            return;

        var primera = true;
        var espera = TimeSpan.FromSeconds(1);
        while (!stoppingToken.IsCancellationRequested)
        {
            try
            {
                var cadena = new NpgsqlConnectionStringBuilder(bus.CadenaDeConexion) { Pooling = false, KeepAlive = 30 }.ConnectionString;
                await using var conexion = new NpgsqlConnection(cadena);
                conexion.Notification += (_, evento) => Atender(evento.Channel, evento.Payload);
                await conexion.OpenAsync(stoppingToken);
                await using (var escuchar = new NpgsqlCommand($"listen {BusDeInvalidaciones.Canal}", conexion))
                    await escuchar.ExecuteNonQueryAsync(stoppingToken);

                if (!primera)
                    Reiniciar();
                primera = false;
                espera = TimeSpan.FromSeconds(1);

                while (!stoppingToken.IsCancellationRequested)
                    await conexion.WaitAsync(stoppingToken);
            }
            catch (OperationCanceledException) when (stoppingToken.IsCancellationRequested)
            {
                return;
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Se perdió la escucha de avisos entre servidores; se reintenta en {Espera}", espera);
                primera = false;
                try
                {
                    await Task.Delay(espera, stoppingToken);
                }
                catch (OperationCanceledException)
                {
                    return;
                }

                espera = TimeSpan.FromSeconds(Math.Min(30, espera.TotalSeconds * 2));
            }
        }
    }

    private void Atender(string canal, string mensaje)
    {
        if (canal != BusDeInvalidaciones.Canal)
            return;

        var partes = mensaje.Split('|', 3);
        if (partes.Length != 3 || partes[0] == bus.Nodo || !_porTipo.TryGetValue(partes[1], out var atienden))
            return;

        foreach (var receptor in atienden)
        {
            try
            {
                receptor.Recibir(partes[2]);
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Un receptor de avisos {Tipo} falló al descartar {Id}", partes[1], partes[2]);
            }
        }
    }

    private void Reiniciar()
    {
        foreach (var receptor in _porTipo.Values.SelectMany(r => r))
        {
            try
            {
                receptor.Reiniciar();
            }
            catch (Exception e)
            {
                logger.LogWarning(e, "Un receptor de avisos {Tipo} falló al reiniciarse", receptor.Tipo);
            }
        }
    }
}
