using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Plataforma;
using Validasoft.Infraestructura;
using Validasoft.Infraestructura.Persistencia;
using Validasoft.Infraestructura.Seguridad;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Con varias réplicas, los procesos periódicos corren solo en el nodo líder (OC-4). Esta prueba vigila el registro de servicios, sin base de datos:
/// todo <see cref="BackgroundService"/> de la solución pasa por <c>AgregarProcesoDelLider</c>, salvo los pocos que cada nodo necesita para sí. Un proceso
/// periódico nuevo registrado con <c>AddHostedService</c> hace fallar la prueba: hay que decidir si corre en todos los nodos o solo en el líder.
/// </summary>
public sealed class ProcesosPeriodicosTests
{
    /// <summary>Lo que cada nodo necesita para sí: la escucha de invalidaciones (LISTEN de PostgreSQL), el volcado de su conteo en memoria y la elección del líder.</summary>
    private static readonly Type[] EnTodosLosNodos = [typeof(EscuchaDeInvalidaciones), typeof(MedidorDeLlamadasApi), typeof(LiderDeProcesosPostgres)];

    private static ServiceCollection Servicios(bool conLider = true)
    {
        var configuracion = new ConfigurationBuilder().AddInMemoryCollection(new Dictionary<string, string?>
        {
            ["ConnectionStrings:Validasoft"] = "Host=localhost;Database=validasoft;Username=x;Password=x",
            ["Validasoft:Lider:Habilitado"] = conLider ? "true" : "false"
        }).Build();
        var servicios = new ServiceCollection();
        servicios.AddLogging();
        servicios.AddSingleton<IConfiguration>(configuracion);
        servicios.AgregarValidasoft(configuracion);
        return servicios;
    }

    private static IEnumerable<Type> ProcesosDeLaSolucion() =>
        new[] { typeof(ProgramadorDePlanes).Assembly, typeof(ServiciosInfraestructura).Assembly }
            .SelectMany(a => a.GetTypes())
            .Where(t => t is { IsAbstract: false, IsClass: true } && typeof(BackgroundService).IsAssignableFrom(t));

    [Fact]
    public void Todo_proceso_periodico_pasa_por_el_lider_salvo_los_que_cada_nodo_necesita()
    {
        var servicios = Servicios();
        var procesos = ProcesosDeLaSolucion().ToList();
        procesos.Count.ShouldBeGreaterThan(EnTodosLosNodos.Length + 8, "no se encontraron los procesos periódicos de la solución");

        foreach (var proceso in procesos.Where(p => !EnTodosLosNodos.Contains(p)))
        {
            servicios.Any(d => d.ServiceType == proceso).ShouldBeTrue($"{proceso.Name} no está registrado: usa AgregarProcesoDelLider");
            servicios.Any(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == proceso)
                .ShouldBeFalse($"{proceso.Name} está registrado con AddHostedService: corre en todas las réplicas; usa AgregarProcesoDelLider");
        }
    }

    [Fact]
    public void La_escucha_de_invalidaciones_y_el_volcado_del_conteo_siguen_corriendo_en_cada_nodo()
    {
        var servicios = Servicios();

        // La escucha de invalidaciones se registra directa (en todos los nodos) y el conteo de llamadas es un servicio propio de cada nodo.
        servicios.Count(d => d.ServiceType == typeof(IHostedService) && d.ImplementationType == typeof(EscuchaDeInvalidaciones)).ShouldBe(1);
        servicios.Any(d => d.ServiceType == typeof(MedidorDeLlamadasApi)).ShouldBeTrue();
    }

    [Fact]
    public void La_eleccion_del_lider_se_puede_apagar_y_entonces_el_nodo_es_siempre_el_lider()
    {
        var conElector = Servicios(conLider: true);
        conElector.Single(d => d.ServiceType == typeof(ILiderDeProcesos)).ImplementationFactory.ShouldNotBeNull();

        var sinElector = Servicios(conLider: false);
        sinElector.Single(d => d.ServiceType == typeof(ILiderDeProcesos)).ImplementationType.ShouldBe(typeof(LiderUnico));
        new LiderUnico().EsLider.ShouldBeTrue();
    }
}
