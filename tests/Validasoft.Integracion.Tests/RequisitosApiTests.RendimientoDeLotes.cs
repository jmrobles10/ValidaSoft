using System.Diagnostics;
using System.Diagnostics.Metrics;
using Marten;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Invitaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Rendimiento de la invitación masiva (OD-23, OD-24, docs/11 §H8) con PostgreSQL y MinIO reales: un lote de 1.000 filas (940 válidas, 20
/// duplicadas y 40 inválidas) con la tanda de producción (100 filas). Mide el tiempo de la vista previa, de la creación, del procesamiento
/// por tandas, del envío de los 940 correos y del informe en Excel; los reintentos por carrera de concurrencia (con el <c>Meter</c>
/// <c>Validasoft.Lotes</c>) y la memoria del proceso (pico del conjunto de trabajo y del montón mientras se procesa). Los presupuestos son
/// holgados a propósito (el CI comparte máquina): fallan si algo deja de escalar, no por una décima.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private const int FilasDelLote = 1000;
    private const int FilasValidas = 940;
    private const int FilasDuplicadas = 20;
    private const int FilasInvalidas = 40;
    private const int TandaDeProduccion = 100;

    /// <summary>Presupuestos de la prueba de 1.000 filas (segundos, porcentaje de filas con reintento y megabytes de crecimiento del conjunto de trabajo).</summary>
    private static class Presupuesto
    {
        public const double VistaPreviaSegundos = 20;
        public const double CreacionSegundos = 30;
        public const double ProcesamientoSegundos = 240;
        public const double CorreosSegundos = 300;
        public const double InformeSegundos = 30;
        public const double ReintentosPorcentaje = 5;
        public const double CrecimientoDeMemoriaMb = 800;
    }

    private sealed class Mediciones : IDisposable
    {
        private readonly MeterListener _escucha = new();
        private long _exitosas;
        private long _fallidas;
        private long _reintentos;
        private long _tandas;
        private long _duraciones;
        private double _sumaDuraciones;
        private double _maximaDuracion;
        private readonly object _candado = new();

        public Mediciones()
        {
            _escucha.InstrumentPublished = (instrumento, escucha) =>
            {
                if (instrumento.Meter.Name == MetricasDeLotes.NombreDelMedidor)
                    escucha.EnableMeasurementEvents(instrumento);
            };
            _escucha.SetMeasurementEventCallback<long>((instrumento, valor, etiquetas, _) =>
            {
                switch (instrumento.Name)
                {
                    case "lotes.filas":
                        if (etiquetas.ToArray().Any(t => t.Key == "resultado" && (string?)t.Value == "exitosa"))
                            Interlocked.Add(ref _exitosas, valor);
                        else
                            Interlocked.Add(ref _fallidas, valor);
                        break;
                    case "lotes.filas_reintentadas":
                        Interlocked.Add(ref _reintentos, valor);
                        break;
                    case "lotes.tandas":
                        Interlocked.Add(ref _tandas, valor);
                        break;
                }
            });
            _escucha.SetMeasurementEventCallback<double>((_, valor, _, _) =>
            {
                lock (_candado)
                {
                    _duraciones++;
                    _sumaDuraciones += valor;
                    _maximaDuracion = Math.Max(_maximaDuracion, valor);
                }
            });
            _escucha.Start();
        }

        public long Exitosas => Interlocked.Read(ref _exitosas);

        public long Fallidas => Interlocked.Read(ref _fallidas);

        public long Reintentos => Interlocked.Read(ref _reintentos);

        public long Tandas => Interlocked.Read(ref _tandas);

        public double DuracionMediaDeFilaMs => _duraciones == 0 ? 0 : _sumaDuraciones / _duraciones;

        public double DuracionMaximaDeFilaMs => _maximaDuracion;

        public void Dispose() => _escucha.Dispose();
    }

    /// <summary>Muestrea la memoria del proceso mientras corre el lote: pico del conjunto de trabajo y del montón gestionado.</summary>
    private sealed class MuestreoDeMemoria : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancelar = new();
        private readonly Task _tarea;
        public long LineaBaseTrabajo { get; }
        public long LineaBaseMontonGestionado { get; }
        public long PicoTrabajo { get; private set; }
        public long PicoMontonGestionado { get; private set; }
        public int Colecciones2 { get; private set; }
        private readonly int _colecciones2Iniciales = GC.CollectionCount(2);

        public MuestreoDeMemoria()
        {
            GC.Collect();
            GC.WaitForPendingFinalizers();
            LineaBaseTrabajo = PicoTrabajo = Process.GetCurrentProcess().WorkingSet64;
            LineaBaseMontonGestionado = PicoMontonGestionado = GC.GetTotalMemory(forceFullCollection: false);
            _tarea = Task.Run(async () =>
            {
                while (!_cancelar.IsCancellationRequested)
                {
                    PicoTrabajo = Math.Max(PicoTrabajo, Process.GetCurrentProcess().WorkingSet64);
                    PicoMontonGestionado = Math.Max(PicoMontonGestionado, GC.GetTotalMemory(false));
                    try
                    {
                        await Task.Delay(100, _cancelar.Token);
                    }
                    catch (OperationCanceledException)
                    {
                        return;
                    }
                }
            });
        }

        public double CrecimientoDeTrabajoMb => (PicoTrabajo - LineaBaseTrabajo) / 1048576.0;

        public double CrecimientoDeMontonMb => (PicoMontonGestionado - LineaBaseMontonGestionado) / 1048576.0;

        public async ValueTask DisposeAsync()
        {
            await _cancelar.CancelAsync();
            await _tarea;
            Colecciones2 = GC.CollectionCount(2) - _colecciones2Iniciales;
        }
    }

    /// <summary>Deja cada fase en un archivo apenas termina (la salida de la prueba solo se ve al final).</summary>
    private static void Progreso(string texto) =>
        File.AppendAllText(Path.Combine(Path.GetTempPath(), "validasoft-rendimiento-progreso.txt"), $"{DateTime.Now:HH:mm:ss.fff} {texto}{Environment.NewLine}");

    [Fact]
    [Trait("Categoria", "Rendimiento")]
    public async Task Un_lote_de_1000_filas_se_valida_se_procesa_y_envia_sus_correos_dentro_de_los_presupuestos_de_tiempo_reintentos_y_memoria()
    {
        var e = await PrepararAsync();
        var configuracion = Host.Services.GetRequiredService<IConfiguration>();
        var tandaOriginal = configuracion["Validasoft:Lotes:TamanoTanda"];
        configuracion["Validasoft:Lotes:TamanoTanda"] = TandaDeProduccion.ToString(System.Globalization.CultureInfo.InvariantCulture);
        try
        {
            // 940 válidas con NIT único, 20 que repiten una anterior y 40 con el correo mal escrito.
            var nits = new HashSet<string>();
            while (nits.Count < FilasValidas)
                nits.Add(Nit());
            var validas = nits.Select((nit, i) => new FilaDeLoteEntradaDto(TipoIdentificacion: "NIT", NumeroIdentificacion: nit, Nombre: $"Proveedor masivo {i + 1}",
                Correo: CorreoNuevo(), TipoVinculo: "PROVEEDOR")).ToList();
            var duplicadas = validas.Take(FilasDuplicadas).Select(f => f with { Correo = CorreoNuevo() });
            var invalidas = Enumerable.Range(1, FilasInvalidas).Select(i => new FilaDeLoteEntradaDto(TipoIdentificacion: "NIT", NumeroIdentificacion: Nit(),
                Nombre: $"Proveedor con error {i}", Correo: "no-es-un-correo", TipoVinculo: "PROVEEDOR"));
            // Las repetidas van al final: la primera aparición de un NIT es la que se invita, y la prueba cuenta correos de esas.
            var filas = validas.Concat(invalidas).OrderBy(_ => Random.Shared.Next()).Concat(duplicadas).ToList();
            filas.Count.ShouldBe(FilasDelLote);

            using var medidas = new Mediciones();
            await using var memoria = new MuestreoDeMemoria();
            var cronometro = Stopwatch.StartNew();

            // 1. Vista previa: valida las 1.000 filas sin crear nada.
            var vista = await ApiAsync<VistaPreviaLoteDto>("POST", "/api/v1/invitaciones/lotes/vista-previa", Admin(e.Norte), new CrearLoteDto(Filas: filas), 200);
            var tVistaPrevia = cronometro.Elapsed.TotalSeconds;
            Progreso($"vista previa {tVistaPrevia:0.00}s");
            (vista.Total, vista.Validas, vista.Duplicadas, vista.Invalidas).ShouldBe((FilasDelLote, FilasValidas, FilasDuplicadas, FilasInvalidas));

            // 2. Creación: guarda las 1.000 filas y encola la primera tanda (202).
            cronometro.Restart();
            var creado = await ApiAsync<LoteDto>("POST", "/api/v1/invitaciones/lotes", Admin(e.Norte), new CrearLoteDto("Rendimiento: 1.000 filas", Filas: filas), 202);
            var tCreacion = cronometro.Elapsed.TotalSeconds;
            Progreso($"creacion {tCreacion:0.00}s");
            creado.Totales.Filas.ShouldBe(FilasDelLote);

            // 3. Procesamiento por tandas de 100 en segundo plano, hasta que termina.
            cronometro.Restart();
            var ultimoAvanceDelLote = DateTime.UtcNow;
            var lote = await EsperarLoteAsync(e.Norte, creado.Id, l =>
            {
                if (DateTime.UtcNow - ultimoAvanceDelLote > TimeSpan.FromSeconds(5))
                {
                    ultimoAvanceDelLote = DateTime.UtcNow;
                    Progreso($"lote {l.Estado}: {l.Totales}");
                }

                return l.Estado is "Completado" or "CompletadoConErrores";
            }, segundos: (int)Presupuesto.ProcesamientoSegundos);
            var tProcesamiento = cronometro.Elapsed.TotalSeconds;
            Progreso($"procesamiento {tProcesamiento:0.00}s");
            lote.Estado.ShouldBe("Completado");
            lote.Totales.ShouldBe(new TotalesLoteDto(FilasDelLote, FilasValidas, 0, FilasValidas, 0, FilasDuplicadas, FilasInvalidas));

            // 4. Los 940 correos de invitación salen (cola durable del outbox) y cada tercero recibe exactamente uno.
            cronometro.Restart();
            var pendientes = validas.Select(f => f.Correo!).ToHashSet();
            var limite = DateTime.UtcNow.AddSeconds(Presupuesto.CorreosSegundos);
            var ultimoAvance = DateTime.UtcNow;
            while (pendientes.Count > 0 && DateTime.UtcNow < limite)
            {
                pendientes.RemoveWhere(c => fixture.Correos.Para(c).Any(m => m.Asunto.Contains("te invita")));
                if (DateTime.UtcNow - ultimoAvance > TimeSpan.FromSeconds(10))
                {
                    ultimoAvance = DateTime.UtcNow;
                    Progreso($"correos enviados {fixture.Correos.Total}, pendientes {pendientes.Count}");
                }

                if (pendientes.Count > 0)
                    await Task.Delay(250);
            }

            var tCorreos = cronometro.Elapsed.TotalSeconds;
            Progreso($"correos {tCorreos:0.00}s");
            pendientes.Count.ShouldBe(0, "faltan correos de invitación por enviar");
            validas.Count(f => fixture.Correos.Para(f.Correo!).Count(m => m.Asunto.Contains("te invita")) != 1).ShouldBe(0);
            // Los de las filas duplicadas e inválidas no se invitaron.
            filas.Where(f => f.Correo == "no-es-un-correo").Count().ShouldBe(FilasInvalidas);

            // 5. Lectura paginada de las filas y el informe en Excel.
            cronometro.Restart();
            (await FilasAsyncPaginas(e.Norte, creado.Id)).Count.ShouldBe(FilasDelLote);
            var tFilas = cronometro.Elapsed.TotalSeconds;
            Progreso($"filas {tFilas:0.00}s");
            cronometro.Restart();
            var informe = await ApiAsync("GET", $"/api/v1/invitaciones/lotes/{creado.Id}/informe", Admin(e.Norte), null, 200);
            var hoja = Validasoft.Aplicacion.Comun.LectorDeHojaDeCalculo.Leer(await BytesAsync(informe));
            var tInforme = cronometro.Elapsed.TotalSeconds;
            Progreso($"informe {tInforme:0.00}s");
            hoja.Count.ShouldBe(FilasDelLote + 1);

            await memoria.DisposeAsync();

            // Cada fila exitosa creó un solo vínculo (idempotente aunque hubiera reintentos).
            await using (var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(e.Norte.Datos.TenantId.ToString("N")))
            {
                var empresaId = e.Norte.Datos.Id;
                (await consulta.Query<Vinculo>().CountAsync(v => v.EmpresaId == empresaId)).ShouldBe(FilasValidas);
            }

            var porcentajeDeReintentos = medidas.Reintentos * 100.0 / FilasDelLote;
            var resumen = new[]
            {
                $"filas={FilasDelLote} validas={FilasValidas} duplicadas={FilasDuplicadas} invalidas={FilasInvalidas} tanda={TandaDeProduccion}",
                $"vista previa={tVistaPrevia:0.00}s  creacion={tCreacion:0.00}s  procesamiento={tProcesamiento:0.00}s ({FilasValidas / tProcesamiento:0.0} filas/s)  correos={tCorreos:0.00}s",
                $"lectura de las 1.000 filas={tFilas:0.00}s  informe Excel={tInforme:0.00}s",
                $"tandas={medidas.Tandas}  filas exitosas={medidas.Exitosas}  fallidas={medidas.Fallidas}  reintentos por carrera={medidas.Reintentos} ({porcentajeDeReintentos:0.0}% de las filas)",
                $"duracion de una fila: media={medidas.DuracionMediaDeFilaMs:0.0} ms  maxima={medidas.DuracionMaximaDeFilaMs:0.0} ms",
                $"memoria: conjunto de trabajo base={memoria.LineaBaseTrabajo / 1048576.0:0} MB, pico={memoria.PicoTrabajo / 1048576.0:0} MB (+{memoria.CrecimientoDeTrabajoMb:0} MB); " +
                $"monton gestionado base={memoria.LineaBaseMontonGestionado / 1048576.0:0} MB, pico +{memoria.CrecimientoDeMontonMb:0} MB; colecciones de generacion 2={memoria.Colecciones2}"
            };
            foreach (var linea in resumen)
                salida.WriteLine(linea);
            File.WriteAllLines(Path.Combine(Path.GetTempPath(), "validasoft-rendimiento-lote.txt"), resumen);

            tVistaPrevia.ShouldBeLessThan(Presupuesto.VistaPreviaSegundos);
            tCreacion.ShouldBeLessThan(Presupuesto.CreacionSegundos);
            tProcesamiento.ShouldBeLessThan(Presupuesto.ProcesamientoSegundos);
            tCorreos.ShouldBeLessThan(Presupuesto.CorreosSegundos);
            tInforme.ShouldBeLessThan(Presupuesto.InformeSegundos);
            medidas.Exitosas.ShouldBe(FilasValidas);
            medidas.Fallidas.ShouldBe(0);
            porcentajeDeReintentos.ShouldBeLessThan(Presupuesto.ReintentosPorcentaje);
            memoria.CrecimientoDeTrabajoMb.ShouldBeLessThan(Presupuesto.CrecimientoDeMemoriaMb);
        }
        finally
        {
            configuracion["Validasoft:Lotes:TamanoTanda"] = tandaOriginal;
        }
    }

    /// <summary>Todas las filas del lote en páginas de 500 (el máximo del endpoint).</summary>
    private async Task<List<FilaLoteDto>> FilasAsyncPaginas(Empresa empresa, Guid loteId)
    {
        var filas = new List<FilaLoteDto>();
        string? cursor = null;
        do
        {
            var pagina = await ApiAsync<PaginaDto<FilaLoteDto>>("GET", $"/api/v1/invitaciones/lotes/{loteId}/filas?limite=500" + (cursor is null ? "" : $"&cursor={cursor}"),
                Admin(empresa), null, 200);
            filas.AddRange(pagina.Datos);
            cursor = pagina.SiguienteCursor;
        } while (cursor is not null);

        return filas;
    }
}
