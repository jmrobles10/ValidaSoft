using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- reintegrar sin revalidar con documentos (DS-3, DS-6) ----------

    [Fact]
    public async Task Reintegrar_sin_revalidar_se_ofrece_mientras_lo_aprobado_esta_vigente_y_se_explica_cuando_una_version_vencio()
    {
        var a = await ProveedorAprobadoAsync();
        try
        {
            await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/desvincular", Admin(a.E.Norte), new MotivoDto("Fin del contrato"), 200);
            var url = $"/api/v1/vinculos/{a.Vinculo.Id}/reintegro";

            // La vigencia del RUT para Norte se calcula por mensaje: se espera a tenerla para poder adelantar el reloj.
            var expediente = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{a.Expediente}", Admin(a.E.Norte), null, 200);
            var documentoId = expediente.Requisitos!.Single(r => r.TipoDocumento.Codigo == "RUT").VersionActual!.DocumentoId;
            VigenciaDeVersion? vigencia = null;
            var limite = DateTime.UtcNow.AddSeconds(20);
            while (vigencia?.VigenteHasta is null)
            {
                await using (var boveda = Host.Services.GetRequiredService<IDocumentStore>().QuerySession(TenantsTecnicos.Boveda))
                    vigencia = (await boveda.LoadAsync<Documento>(documentoId))!.Buscar(1)!.VigenciaPara(a.Vinculo.Id);
                if (vigencia?.VigenteHasta is not null)
                    break;
                if (DateTime.UtcNow > limite)
                    throw new TimeoutException("El RUT no tiene vigencia para Norte.");
                await Task.Delay(200);
            }

            // Vigente: se puede reintegrar sin revalidar.
            var vigente = await ApiAsync<ViabilidadDeReintegroDto>("GET", url, Admin(a.E.Norte), null, 200);
            vigente.PuedeSinRevalidar.ShouldBeTrue();
            vigente.Motivos.ShouldBeEmpty();

            // Pasada la gracia del RUT ya no: el diálogo explica cuál y qué pasó.
            var hoy = CalculadoraDeVigencia.Fecha(DateTimeOffset.UtcNow);
            fixture.Reloj.Desfase = TimeSpan.FromDays(vigencia.FinDeGracia!.Value.AddDays(1).DayNumber - hoy.DayNumber);
            var vencido = await ApiAsync<ViabilidadDeReintegroDto>("GET", url, Admin(a.E.Norte), null, 200);
            vencido.PuedeSinRevalidar.ShouldBeFalse();
            vencido.Motivos.ShouldHaveSingleItem().ShouldContain("Registro Único Tributario");
            var sinRevalidar = await ApiAsync("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/reintegrar", Admin(a.E.Norte),
                new ReintegrarVinculoDto("Retoma la relación.", false), 409);
            (await CodigoAsync(sinRevalidar)).ShouldBe("reintegro-requiere-revalidacion");
            (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{a.Vinculo.Id}", Admin(a.E.Norte), null, 200)).Estado.ShouldBe("Desvinculado");

            // Revalidando sí: vuelve a validación con una renovación que pide todo el flujo (RUT y Cámara), no solo lo vencido.
            fixture.Reloj.Desfase = TimeSpan.Zero;
            var reintegrado = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{a.Vinculo.Id}/reintegrar", Admin(a.E.Norte),
                new ReintegrarVinculoDto("Retoma la relación."), 200);
            reintegrado.Estado.ShouldBe("EnValidacion");
            var renovacion = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{reintegrado.ExpedienteAbiertoId}", Admin(a.E.Norte), null, 200);
            renovacion.Tipo.ShouldBe("Renovacion");
            renovacion.Requisitos!.Select(r => r.TipoDocumento.Codigo).ShouldBe(["RUT", "CAMARA_COMERCIO"], ignoreOrder: true);
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }
}
