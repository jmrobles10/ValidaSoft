using System.Text;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Invitaciones;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Invitaciones;
using Validasoft.Dominio.Vinculos;
using Wolverine.Tracking;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Invitación masiva por lote (H8, LT-5…LT-10) con PostgreSQL y la cola durable de Wolverine: vista previa y lote con filas
/// válidas, inválidas, duplicadas y un vínculo que ya existía; pausa y reanudación por tandas; idempotencia por fila y por
/// ejecución; reintento de las fallidas e informe en Excel.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private static string CsvDeLote(IEnumerable<string> filas) =>
        "Tipo de identificación;Número de identificación;Razón social o nombres;Correo;Tipo de vínculo\r\n" + string.Join("\r\n", filas) + "\r\n";

    private static ArchivoDeLoteDto Archivo(string csv) => new("proveedores.csv", Convert.ToBase64String(Encoding.UTF8.GetBytes(csv)));

    private static async Task<byte[]> BytesAsync(Alba.IScenarioResult resultado)
    {
        var cuerpo = resultado.Context.Response.Body;
        if (cuerpo.CanSeek)
            cuerpo.Position = 0;
        using var memoria = new MemoryStream();
        await cuerpo.CopyToAsync(memoria);
        return memoria.ToArray();
    }

    private async Task<LoteDto> EsperarLoteAsync(Empresa empresa, Guid loteId, Func<LoteDto, bool> condicion, int segundos = 30)
    {
        var limite = DateTime.UtcNow.AddSeconds(segundos);
        while (true)
        {
            var lote = await ApiAsync<LoteDto>("GET", $"/api/v1/invitaciones/lotes/{loteId}", Admin(empresa), null, 200);
            if (condicion(lote))
                return lote;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"El lote {loteId} no llegó al estado esperado (está {lote.Estado}, {lote.Totales}).");
            await Task.Delay(150);
        }
    }

    private async Task<List<FilaLoteDto>> FilasAsync(Empresa empresa, Guid loteId, string? estado = null)
    {
        var filas = new List<FilaLoteDto>();
        string? cursor = null;
        do
        {
            var url = $"/api/v1/invitaciones/lotes/{loteId}/filas?limite=7" + (estado is null ? "" : $"&estado={estado}") + (cursor is null ? "" : $"&cursor={cursor}");
            var pagina = await ApiAsync<PaginaDto<FilaLoteDto>>("GET", url, Admin(empresa), null, 200);
            filas.AddRange(pagina.Datos);
            cursor = pagina.SiguienteCursor;
        } while (cursor is not null);

        return filas;
    }

    [Fact]
    public async Task Lote_con_filas_validas_invalidas_duplicadas_y_un_vinculo_existente_invita_a_las_validas_y_deja_su_informe()
    {
        var e = await PrepararAsync();
        var nit0 = Nit();
        var existente = await CrearVinculoAsync(e.Norte, "PROVEEDOR", CorreoNuevo(), nit0);
        var (nit1, nit4, nit5) = (Nit(), Nit(), Nit());
        var cedula = Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        var (c1, c2, c6) = (CorreoNuevo(), CorreoNuevo(), CorreoNuevo());
        var csv = CsvDeLote(
        [
            $"NIT;{nit1};Aceros del Norte S.A.S.;{c1};PROVEEDOR",
            $"CC;{cedula};Ana Gómez;{c2};proveedor",
            $"NIT;{nit1};Aceros repetido;{CorreoNuevo()};PROVEEDOR",
            $"NIT;{nit4};Correo malo;no-es-un-correo;PROVEEDOR",
            $"NIT;{nit5};Tipo malo;{CorreoNuevo()};NO_EXISTE",
            $"NIT;{nit0};Ya vinculado;{c6};PROVEEDOR"
        ]);

        // Vista previa: valida por fila sin crear nada.
        var vista = await ApiAsync<VistaPreviaLoteDto>("POST", "/api/v1/invitaciones/lotes/vista-previa", Admin(e.Norte),
            new CrearLoteDto(Archivo: Archivo(csv)), 200);
        (vista.Total, vista.Validas, vista.Invalidas, vista.Duplicadas).ShouldBe((6, 3, 2, 1));
        vista.Filas[0].AccionPrevista.ShouldBe("Invitado");
        vista.Filas[2].Estado.ShouldBe("Duplicada");
        vista.Filas[2].DuplicadaDe.ShouldBe(1);
        vista.Filas[3].Errores.ShouldHaveSingleItem().Campo.ShouldBe("correo");
        vista.Filas[4].Errores.ShouldHaveSingleItem().Codigo.ShouldBe("tipo-vinculo-invalido");
        vista.Filas[5].AccionPrevista.ShouldBe("Reinvitado");
        (await ApiAsync<PaginaDto<LoteDto>>("GET", "/api/v1/invitaciones/lotes", Admin(e.Norte), null, 200)).Datos.ShouldBeEmpty();

        // Se crea (202) y se procesa en segundo plano.
        var creado = await ApiAsync<LoteDto>("POST", "/api/v1/invitaciones/lotes", Admin(e.Norte),
            new CrearLoteDto("Proveedores de noviembre", Archivo(csv)), 202);
        creado.Totales.Filas.ShouldBe(6);
        var lote = await EsperarLoteAsync(e.Norte, creado.Id, l => l.Estado is "Completado" or "CompletadoConErrores");
        lote.Estado.ShouldBe("Completado");
        lote.Totales.ShouldBe(new TotalesLoteDto(6, 3, 0, 3, 0, 1, 2));

        var filas = await FilasAsync(e.Norte, creado.Id);
        filas.Select(f => f.Estado).ShouldBe(["Exitosa", "Exitosa", "Duplicada", "Invalida", "Invalida", "Exitosa"]);
        filas[0].Accion.ShouldBe("Invitado");
        filas[0].TerceroCreado.ShouldBeTrue();
        filas[5].Accion.ShouldBe("Reinvitado");
        filas[5].VinculoId.ShouldBe(existente.Id);
        (await FilasAsync(e.Norte, creado.Id, "Invalida")).Select(f => f.N).ShouldBe([4, 5]);

        // Las invitaciones salieron (la reenviada, al correo de la fila) y el vínculo nuevo lleva el lote en su invitación.
        await fixture.Correos.EsperarAsync(c1, m => m.Asunto.Contains("te invita"));
        await fixture.Correos.EsperarAsync(c2, m => m.Asunto.Contains("te invita"));
        await fixture.Correos.EsperarAsync(c6, m => m.Asunto.Contains("te invita"));
        var nuevo = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{filas[0].VinculoId}", Admin(e.Norte), null, 200);
        nuevo.Estado.ShouldBe("Invitado");
        (await EventosAsync(e.Norte.Datos.TenantId, nuevo.Id)).OfType<InvitacionEnviada>().ShouldHaveSingleItem().LoteId.ShouldBe(creado.Id);

        // El tercero invitado por el lote entra con el enlace de su correo y acepta.
        await AceptarAsync(await SesionAsync(c1), nuevo.Id);

        // Informe por fila en Excel.
        var informe = await ApiAsync("GET", $"/api/v1/invitaciones/lotes/{creado.Id}/informe", Admin(e.Norte), null, 200);
        informe.Context.Response.ContentType.ShouldBe(HojaDeCalculo.TipoContenido);
        var hoja = LectorDeHojaDeCalculo.Leer(await BytesAsync(informe));
        hoja.Count.ShouldBe(7);
        hoja[4][5].ShouldBe("Invalida");

        // Evento público invitaciones.lote_terminado (suscripción asíncrona).
        var limite = DateTime.UtcNow.AddSeconds(20);
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession();
        var empresaId = e.Norte.Datos.Id;
        while (!await consulta.Query<EventoPublico>().AnyAsync(x => x.EmpresaId == empresaId && x.Tipo == "invitaciones.lote_terminado"))
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException("No se registró invitaciones.lote_terminado.");
            await Task.Delay(200);
        }

        // Sin filas válidas no hay lote; y no se puede reintentar lo que no falló.
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/invitaciones/lotes", Admin(e.Norte),
            new CrearLoteDto(Archivo: Archivo(CsvDeLote([$"NIT;{Nit()};Sin correo;;PROVEEDOR"]))), 422))).ShouldBe("lote-sin-filas-validas");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/reintentar", Admin(e.Norte), null, 409)))
            .ShouldBe("sin-filas-fallidas");
    }

    [Fact]
    public async Task El_lote_se_pausa_entre_tandas_se_reanuda_y_cada_fila_invita_una_sola_vez_aunque_se_repita_su_tanda_o_se_reintente()
    {
        var e = await PrepararAsync();
        var filas = Enumerable.Range(1, 40).Select(_ => new FilaDeLoteEntradaDto(TipoIdentificacion: "NIT", NumeroIdentificacion: Nit(),
            Nombre: "Proveedor masivo", Correo: CorreoNuevo(), TipoVinculo: "PROVEEDOR")).ToList();

        var creado = await ApiAsync<LoteDto>("POST", "/api/v1/invitaciones/lotes", Admin(e.Norte), new CrearLoteDto("Masivo", Filas: filas), 202);
        var pausado = await ApiAsync<LoteDto>("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/pausar", Admin(e.Norte), null, 200);
        pausado.Estado.ShouldBe("Pausado");

        // La tanda en curso termina y ya no avanza.
        await Task.Delay(1500);
        var detenido = await ApiAsync<LoteDto>("GET", $"/api/v1/invitaciones/lotes/{creado.Id}", Admin(e.Norte), null, 200);
        detenido.Totales.Pendientes.ShouldBeGreaterThan(0);
        await Task.Delay(1000);
        (await ApiAsync<LoteDto>("GET", $"/api/v1/invitaciones/lotes/{creado.Id}", Admin(e.Norte), null, 200)).Totales.ShouldBe(detenido.Totales);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/pausar", Admin(e.Norte), null, 409))).ShouldBe("estado-invalido");

        // La tanda vieja (ejecución anterior) que siguiera en la cola no hace nada.
        LoteDeInvitacion snapshot;
        await using (var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(e.Norte.Datos.TenantId.ToString("N")))
            snapshot = (await consulta.Events.AggregateStreamAsync<LoteDeInvitacion>(creado.Id))!;
        var primera = (await EventosAsync(e.Norte.Datos.TenantId, creado.Id)).OfType<LoteDeInvitacionCreado>().Single().Ejecucion;
        await Host.TrackActivity().Timeout(TimeSpan.FromSeconds(30))
            .SendMessageAndWaitAsync(new ProcesarTandaDeLote(e.Norte.Datos.TenantId, creado.Id, primera));
        (await ApiAsync<LoteDto>("GET", $"/api/v1/invitaciones/lotes/{creado.Id}", Admin(e.Norte), null, 200)).Version.ShouldBe(snapshot.Version);

        // Reanudado, termina todo.
        (await ApiAsync<LoteDto>("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/reanudar", Admin(e.Norte), null, 200)).Estado.ShouldBe("Procesando");
        var completo = await EsperarLoteAsync(e.Norte, creado.Id, l => l.Estado == "Completado");
        completo.Totales.Exitosas.ShouldBe(40);
        var resultado = await FilasAsync(e.Norte, creado.Id);
        resultado.Select(f => f.VinculoId).Distinct().Count().ShouldBe(40);

        // Una fila que hubiera fallado se reintenta sola: el vínculo ya existe y no se duplica (se reenvía la invitación).
        var fila = resultado[7];
        await using (var sesion = Host.Services.GetRequiredService<IDocumentStore>().LightweightSession(e.Norte.Datos.TenantId.ToString("N")))
        {
            var documento = (await sesion.LoadAsync<FilaDeLote>(FilaDeLote.Clave(creado.Id, fila.N)))!;
            documento.MarcarFallida(new Validasoft.Dominio.Invitaciones.ErrorDeFila(null, "error-inesperado", "Simulado"), DateTimeOffset.UtcNow);
            sesion.Store(documento);
            await sesion.SaveChangesAsync();
        }

        (await ApiAsync<LoteDto>("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/reintentar", Admin(e.Norte), null, 200)).Estado.ShouldBe("Procesando");
        var reintentado = await EsperarLoteAsync(e.Norte, creado.Id, l => l.Estado == "Completado");
        reintentado.Reintentos.ShouldBe(1);
        var otraVez = (await FilasAsync(e.Norte, creado.Id)).Single(f => f.N == fila.N);
        otraVez.Estado.ShouldBe("Exitosa");
        otraVez.Accion.ShouldBe("Reinvitado");
        otraVez.VinculoId.ShouldBe(fila.VinculoId);
        (await EventosAsync(e.Norte.Datos.TenantId, fila.VinculoId!.Value)).OfType<VinculoCreado>().ShouldHaveSingleItem();

        // Cancelado es terminal.
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/invitaciones/lotes/{creado.Id}/cancelar", Admin(e.Norte), null, 409)))
            .ShouldBe("lote-en-estado-terminal");
    }

    [Fact]
    public async Task La_plantilla_se_descarga_en_excel_y_csv_y_un_archivo_sin_sus_columnas_se_rechaza()
    {
        var e = await PrepararAsync();
        var xlsx = await ApiAsync("GET", "/api/v1/invitaciones/lotes/plantilla", Admin(e.Norte), null, 200);
        LectorDeHojaDeCalculo.Leer(await BytesAsync(xlsx)).ShouldHaveSingleItem().ShouldContain("Número de identificación");
        var csv = await ApiAsync("GET", "/api/v1/invitaciones/lotes/plantilla?formato=csv", Admin(e.Norte), null, 200);
        (await csv.ReadAsTextAsync()).ShouldContain("Tipo de vínculo");

        (await CodigoAsync(await ApiAsync("POST", "/api/v1/invitaciones/lotes/vista-previa", Admin(e.Norte),
            new CrearLoteDto(Archivo: Archivo("nit;correo\r\n1;a@b.co\r\n")), 422))).ShouldBe("plantilla-invalida");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/invitaciones/lotes/vista-previa", Admin(e.Norte), new CrearLoteDto(), 422)))
            .ShouldBe("archivo-o-filas");
    }
}
