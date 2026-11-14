using Marten;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging.Abstractions;
using Npgsql;
using Validasoft.Aplicacion.Comun;
using Validasoft.Infraestructura.Persistencia;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Varias réplicas sobre la misma base (OC-4) con PostgreSQL real: los procesos periódicos corren solo en el nodo líder (bloqueo consultivo),
/// otra réplica toma el relevo si el líder cae, y un trabajo que se factura se reclama una sola vez.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed class ReplicasApiTests(ApiFixture fixture)
{
    private static LiderDeProcesosPostgres Nodo(string cadena, long llave, string nombre) =>
        new($"{cadena};Application Name={nombre}", llave, TimeSpan.FromSeconds(1), NullLogger<LiderDeProcesosPostgres>.Instance);

    [Fact]
    public void La_API_de_un_solo_nodo_es_la_lider_de_sus_procesos_periodicos()
    {
        fixture.Host.Services.GetRequiredService<ILiderDeProcesos>().EsLider.ShouldBeTrue();
    }

    [Fact]
    public async Task De_varias_replicas_solo_una_es_la_lider_y_si_cae_otra_toma_el_relevo()
    {
        var llave = Random.Shared.NextInt64(1, long.MaxValue);
        var sufijo = Guid.NewGuid().ToString("N")[..8];
        await using var admin = new NpgsqlConnection(fixture.CadenaDeConexion);
        await admin.OpenAsync();
        var a = Nodo(fixture.CadenaDeConexion, llave, "lider-a-" + sufijo);
        var b = Nodo(fixture.CadenaDeConexion, llave, "lider-b-" + sufijo);
        var c = Nodo(fixture.CadenaDeConexion, llave, "lider-c-" + sufijo);
        var cambios = new List<string>();
        a.LiderazgoCambio += esLider => cambios.Add("a:" + esLider);
        b.LiderazgoCambio += esLider => cambios.Add("b:" + esLider);
        try
        {
            // El primero en llegar es el líder; los otros esperan (el arranque ya hace el primer intento).
            await a.StartAsync(CancellationToken.None);
            await b.StartAsync(CancellationToken.None);
            await c.StartAsync(CancellationToken.None);
            (a.EsLider, b.EsLider, c.EsLider).ShouldBe((true, false, false));

            // El líder cae sin avisar (se mata su conexión): PostgreSQL libera el bloqueo, el líder lo nota en su siguiente latido y otro toma el relevo.
            await using (var matar = new NpgsqlCommand("select pg_terminate_backend(pid) from pg_stat_activity where application_name = @nombre", admin))
            {
                matar.Parameters.AddWithValue("nombre", "lider-a-" + sufijo);
                await matar.ExecuteScalarAsync();
            }

            await Esperar(() => !a.EsLider, "que el líder caído note que perdió el bloqueo");
            // El nodo caído también vuelve a competir; se apaga para que el relevo entre las otras dos réplicas no dependa de quién gane.
            await a.StopAsync(CancellationToken.None);
            await Esperar(() => b.EsLider || c.EsLider, "el relevo del líder caído");
            new[] { a, b, c }.Count(n => n.EsLider).ShouldBe(1);

            // Una parada ordenada también entrega el liderazgo.
            var nuevoLider = b.EsLider ? b : c;
            var otro = b.EsLider ? c : b;
            await nuevoLider.StopAsync(CancellationToken.None);
            nuevoLider.EsLider.ShouldBeFalse();
            await Esperar(() => otro.EsLider, "el relevo tras la parada ordenada");
            new[] { a, b, c }.Count(n => n.EsLider).ShouldBe(1);
            cambios.ShouldContain("a:True");
            cambios.ShouldContain("a:False");
        }
        finally
        {
            foreach (var nodo in new[] { a, b, c })
            {
                await nodo.StopAsync(CancellationToken.None);
                nodo.Dispose();
            }
        }
    }

    [Fact]
    public async Task Un_trabajo_se_reclama_una_sola_vez_mientras_vive_su_arrendamiento_y_se_retoma_al_vencer_o_con_otra_ocurrencia()
    {
        var reclamos = fixture.Host.Services.GetRequiredService<ReclamosDeTrabajo>();
        var tenant = Guid.NewGuid();
        var trabajo = "prueba:" + Guid.NewGuid().ToString("N");
        var ahora = DateTimeOffset.UtcNow;
        var lease = TimeSpan.FromMinutes(5);

        (await reclamos.TomarAsync(tenant, trabajo, "1", ahora, lease, default)).ShouldBeTrue();
        (await reclamos.TomarAsync(tenant, trabajo, "1", ahora.AddMinutes(1), lease, default)).ShouldBeFalse(); // sigue reclamado
        (await reclamos.TomarAsync(tenant, trabajo, "2", ahora.AddMinutes(1), lease, default)).ShouldBeTrue(); // otra ocurrencia: se reclama de nuevo
        (await reclamos.TomarAsync(tenant, trabajo, "2", ahora.AddMinutes(2), lease, default)).ShouldBeFalse();
        (await reclamos.TomarAsync(tenant, trabajo, "2", ahora.AddMinutes(7), lease, default)).ShouldBeTrue(); // venció: quien lo tomó cayó a medias
        (await reclamos.TomarAsync(Guid.NewGuid(), trabajo, "2", ahora.AddMinutes(7), lease, default)).ShouldBeTrue(); // otro tenant: otro trabajo
    }

    [Fact]
    public async Task De_veinte_intentos_simultaneos_por_reclamar_el_mismo_trabajo_solo_uno_lo_obtiene()
    {
        var reclamos = fixture.Host.Services.GetRequiredService<ReclamosDeTrabajo>();
        var tenant = Guid.NewGuid();
        var trabajo = "carrera:" + Guid.NewGuid().ToString("N");
        var ahora = DateTimeOffset.UtcNow;

        var resultados = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            Task.Run(() => reclamos.TomarAsync(tenant, trabajo, "unica", ahora, TimeSpan.FromMinutes(5), default))));

        resultados.Count(obtenido => obtenido).ShouldBe(1);
        // Y con el reclamo vencido, de nuevo gana uno solo.
        var despues = await Task.WhenAll(Enumerable.Range(0, 20).Select(_ =>
            Task.Run(() => reclamos.TomarAsync(tenant, trabajo, "unica", ahora.AddMinutes(10), TimeSpan.FromMinutes(5), default))));
        despues.Count(obtenido => obtenido).ShouldBe(1);
    }

    private static async Task Esperar(Func<bool> condicion, string que)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (!condicion())
        {
            DateTime.UtcNow.ShouldBeLessThan(limite, $"no llegó {que}");
            await Task.Delay(100);
        }
    }
}
