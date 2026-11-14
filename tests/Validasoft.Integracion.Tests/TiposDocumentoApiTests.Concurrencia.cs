using System.Text.Json;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Documentos;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Wolverine;

namespace Validasoft.Integracion.Tests;

public sealed partial class TiposDocumentoApiTests
{
    // ---------- avisos de vencimiento idempotentes (OC-1) ----------

    /// <summary>
    /// Un tercero con dos formularios que vencen el mismo día: cada documento es su propio stream, así que dos manejadores del mismo aviso
    /// no chocan entre sí al agregar el evento; solo el registro del aviso (concurrencia optimista) los distingue.
    /// </summary>
    [Fact]
    public async Task El_aviso_de_vencimiento_sale_una_sola_vez_aunque_varios_mensajes_lo_procesen_a_la_vez()
    {
        var empresa = await EmpresaNuevaAsync("Comercial " + Guid.NewGuid().ToString("N")[..6], "G" + Random.Shared.Next(10, 99));
        var primero = await TipoDeFormularioAsync(empresa, "DECLARACION_A", "Declaración A");
        var segundo = await TipoDeFormularioAsync(empresa, "DECLARACION_B", "Declaración B");
        await FlujoConAsync(empresa, [new RequisitoDelFlujoDto(TipoDocumentoTenantId: primero.Id), new RequisitoDelFlujoDto(TipoDocumentoTenantId: segundo.Id)]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(empresa, correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);

        var hoy = Hoy();
        var documentos = new List<Guid>();
        foreach (var tipo in new[] { primero, segundo })
        {
            var valores = new Dictionary<string, JsonElement>
            {
                ["fecha_expedicion"] = JsonSerializer.SerializeToElement(hoy.AddDays(-20).ToString("yyyy-MM-dd")),
                ["origen"] = JsonSerializer.SerializeToElement("Nacional")
            };
            var enviado = await TerceroAsync<ConfirmacionCargaDto>("POST", "/public/v1/boveda/formularios", sesion.AccessToken,
                new EnviarFormularioDto(vinculo.Id, valores, tipo.Id), 200);
            documentos.Add(enviado.Version.DocumentoId);
        }

        var hasta = (await EsperarEventoAsync<VigenciaDeVersionCalculada>(documentos[0])).VigenteHasta!.Value;
        await EsperarEventoAsync<VigenciaDeVersionCalculada>(documentos[1]);
        var fechaAviso = hasta.AddDays(-5);
        var clave = AvisoDeVencimiento.Clave(sesion.TerceroId, fechaAviso);

        try
        {
            fixture.Reloj.Desfase = TimeSpan.FromDays(5);
            // Ocho copias del mismo mensaje (varios mensajes programados para el mismo tercero y fecha) en la cola local, que las
            // procesa en paralelo: solo una puede enviar el correo y publicar los eventos; las demás vuelven a leer el aviso ya enviado.
            await using (var alcance = Host.Services.CreateAsyncScope())
            {
                var bus = alcance.ServiceProvider.GetRequiredService<IMessageBus>();
                for (var n = 0; n < 8; n++)
                    await bus.PublishAsync(new EnviarAvisosDeVencimiento(sesion.TerceroId, fechaAviso));
            }

            await EsperarAsync(async () =>
            {
                await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda);
                return (await consulta.LoadAsync<AvisoDeVencimiento>(clave))?.Estado == EstadoAviso.Enviado;
            }, "el aviso enviado");
            await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("vencer"));

            // Los perdedores de la carrera reintentan (espera de 1 s) y encuentran el aviso ya enviado: se da tiempo a que lo hagan.
            await Task.Delay(TimeSpan.FromSeconds(4));
            fixture.Correos.Para(correo).Count(m => m.Asunto.Contains("vencer")).ShouldBe(1);
            foreach (var documentoId in documentos)
                (await EventosAsync(documentoId)).OfType<VencimientoAvisado>().ShouldHaveSingleItem();
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    /// <summary>
    /// La garantía de fondo, sin depender de la suerte de los tiempos: dos manejadores que leyeron el aviso a la vez no pueden guardarlo los dos
    /// (el segundo falla y el mensaje se reintenta), y un aviso ya existente no se vuelve a programar.
    /// </summary>
    [Fact]
    public async Task El_registro_del_aviso_tiene_concurrencia_optimista_y_el_segundo_guardado_pierde()
    {
        var almacen = Host.Services.GetRequiredService<IDocumentStore>();
        var terceroId = Guid.NewGuid();
        var clave = AvisoDeVencimiento.Clave(terceroId, new DateOnly(2031, 3, 4));
        await using (var alta = almacen.LightweightSession(TenantsTecnicos.Boveda))
        {
            alta.Insert(new AvisoDeVencimiento { Id = clave, TerceroId = terceroId, Fecha = new DateOnly(2031, 3, 4), ProgramadoEn = DateTimeOffset.UtcNow });
            await alta.SaveChangesAsync();
        }

        await using var primero = almacen.LightweightSession(TenantsTecnicos.Boveda);
        await using var segundo = almacen.LightweightSession(TenantsTecnicos.Boveda);
        var deElPrimero = (await primero.LoadAsync<AvisoDeVencimiento>(clave)).ShouldNotBeNull();
        var deElSegundo = (await segundo.LoadAsync<AvisoDeVencimiento>(clave)).ShouldNotBeNull();
        deElPrimero.Estado = EstadoAviso.Enviado;
        primero.Store(deElPrimero);
        await primero.SaveChangesAsync();

        deElSegundo.Estado = EstadoAviso.Enviado;
        segundo.Store(deElSegundo);
        var perdida = await Should.ThrowAsync<Exception>(() => segundo.SaveChangesAsync());
        Peticiones.EsCarrera(perdida).ShouldBeTrue();

        await using var repetido = almacen.LightweightSession(TenantsTecnicos.Boveda);
        repetido.Insert(new AvisoDeVencimiento { Id = clave, TerceroId = terceroId, Fecha = new DateOnly(2031, 3, 4), ProgramadoEn = DateTimeOffset.UtcNow });
        Peticiones.EsCarrera(await Should.ThrowAsync<Exception>(() => repetido.SaveChangesAsync())).ShouldBeTrue();
    }

    private async Task<TipoDocumentoTenantDto> TipoDeFormularioAsync(Empresa empresa, string codigo, string nombre) =>
        await ApiAsync<TipoDocumentoTenantDto>("POST", "/api/v1/tipos-documento", Admin(empresa),
            new CrearTipoDocumentoDto(codigo, nombre, null, ReglaFrescura: new ReglaFrescuraDto("MaxDiasDesdeExpedicion", 30), DiasAviso: [5], DiasGracia: 2,
                Modo: "Formulario",
                Campos:
                [
                    new CampoFormularioDto("fecha_expedicion", "Fecha de expedición", "Fecha", true),
                    new CampoFormularioDto("origen", "Origen de los fondos", "Lista", true, ["Nacional", "Extranjero"])
                ]), 201);

    private static async Task EsperarAsync(Func<Task<bool>> condicion, string que)
    {
        var limite = DateTime.UtcNow.AddSeconds(20);
        while (!await condicion())
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"No llegó {que}.");
            await Task.Delay(100);
        }
    }
}
