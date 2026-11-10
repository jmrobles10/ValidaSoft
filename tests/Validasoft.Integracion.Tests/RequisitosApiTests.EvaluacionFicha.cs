using System.Text.Json;
using Alba;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- vista previa de la completitud (FM-46) ----------

    private Task<IScenarioResult> EvaluarSeccionAsync(SesionTerceroDto sesion, string seccion, string datos, int esperado, params SoporteSeccionDto[] soportes) =>
        TerceroAsync("POST", $"{RutaFicha}/{seccion}/evaluacion", sesion.AccessToken, new GuardarSeccionDto(Datos(datos), soportes), esperado);

    private async Task<CompletitudSeccionDto> EvaluarAsync(SesionTerceroDto sesion, string seccion, string datos, params SoporteSeccionDto[] soportes) =>
        (await (await EvaluarSeccionAsync(sesion, seccion, datos, 200, soportes)).ReadAsJsonAsync<CompletitudSeccionDto>())!;

    [Fact]
    public async Task La_evaluacion_anticipa_la_completitud_del_guardado_sin_escribir_nada()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo, "900373914");
        var sesion = await SesionAsync(correo);

        // Vacía: los mismos faltantes que tendría el guardado, y nada queda guardado.
        var vacia = await EvaluarAsync(sesion, "FICHA_GENERAL", "{}");
        vacia.Completa.ShouldBeFalse();
        vacia.Faltantes.Select(f => f.Campo).ShouldBe(["razon_social", "correo", "telefono", "pais", "direccion", "ciiu_principal"]);
        (await TerceroAsync<SeccionFichaDto>("GET", $"{RutaFicha}/FICHA_GENERAL", sesion.AccessToken, null, 200)).Version.ShouldBeNull();
        (await EstadoGuardadoAsync(vinculo.TerceroId)).ShouldBeNull();

        // Completa ⇒ completa, igual que el guardado que viene después.
        (await EvaluarAsync(sesion, "FICHA_GENERAL", GeneralCompleta)).Completa.ShouldBeTrue();
        (await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta)).Completitud.Completa.ShouldBeTrue();

        // Un cambio sobre lo completo pide soporte, como lo pediría el guardado.
        var cambio = await EvaluarAsync(sesion, "FICHA_GENERAL", GeneralCompleta.Replace("Calle 45 # 12-08", "Carrera 7 # 80-12"));
        cambio.Completa.ShouldBeFalse();
        cambio.Faltantes.ShouldHaveSingleItem().Regla.ShouldBe("soporte-del-cambio");
        (await TerceroAsync<SeccionFichaDto>("GET", $"{RutaFicha}/FICHA_GENERAL", sesion.AccessToken, null, 200)).Version.ShouldBe(1);

        // Formato inválido ⇒ el mismo 422 con el campo.
        var invalido = await (await EvaluarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta.Replace("compras@aceros.co", "no-es-correo"), 422))
            .ReadAsJsonAsync<JsonElement>();
        invalido.GetProperty("codigo").GetString().ShouldBe("valor-invalido");
        invalido.GetProperty("campo").GetString().ShouldBe("correo");
    }
}
