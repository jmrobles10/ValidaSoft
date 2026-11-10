using Validasoft.Aplicacion.Expedientes;
using Validasoft.Contratos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Vinculos;
using Wolverine.Tracking;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Plazo de cargue del expediente inicial (P-4-2, LT-1…LT-4) con el reloj simulado: recordatorio sin repetirse, caducidad con su
/// correo y el vínculo de vuelta a <c>Invitado</c>, y reapertura con un plazo nuevo y un enlace de acceso para el tercero.
/// </summary>
public sealed partial class RequisitosApiTests
{
    /// <summary>Corre el barrido del job para el tenant (como lo pide el programador periódico) y espera lo que encadene.</summary>
    private Task BarrerPlazosAsync(Guid tenantId) =>
        Host.TrackActivity().Timeout(TimeSpan.FromSeconds(30)).SendMessageAndWaitAsync(new RevisarPlazosDeCargue(tenantId));

    [Fact]
    public async Task El_expediente_inicial_recuerda_el_plazo_caduca_al_vencer_y_la_empresa_lo_reabre()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await ExpedienteAbiertoAsync(e, vinculo.Id);

        var abierto = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        abierto.Estado.ShouldBe("Abierto");
        var plazo = abierto.PlazoCargue.ShouldNotBeNull();
        plazo.Dias.ShouldBe(30);
        plazo.Recordatorios.ShouldBe([15, 7, 1]);
        plazo.Hasta.ShouldBe(abierto.AbiertoEn.AddDays(30));
        (await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200))
            .PlazoCargueHasta.ShouldBe(plazo.Hasta);

        try
        {
            // A 15 días de vencer: un recordatorio (y solo uno aunque el job vuelva a pasar).
            fixture.Reloj.Desfase = TimeSpan.FromDays(15.5);
            await BarrerPlazosAsync(tenantId);
            await BarrerPlazosAsync(tenantId);
            (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200))
                .PlazoCargue!.RecordatoriosEnviados.ShouldBe([15]);
            await fixture.Correos.EsperarAsync(correo, m => m.Asunto.StartsWith("Te quedan 15 días", StringComparison.Ordinal));
            fixture.Correos.Para(correo).Count(m => m.Asunto.StartsWith("Te quedan", StringComparison.Ordinal)).ShouldBe(1);

            // Vencido el plazo: caduca, el vínculo vuelve a Invitado y el tercero se entera.
            fixture.Reloj.Desfase = TimeSpan.FromDays(30.5);
            await BarrerPlazosAsync(tenantId);
            var caducado = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
            caducado.Estado.ShouldBe("Caducado");
            caducado.PlazoCargue!.CaducadoEn.ShouldNotBeNull();
            caducado.PlazoCargue.EstadoAlReabrir.ShouldBe("Abierto");
            var invitado = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200);
            invitado.Estado.ShouldBe("Invitado");
            invitado.ExpedienteAbiertoId.ShouldBeNull();
            await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("caducó"));
            var eventos = await EventosAsync(tenantId, expedienteId);
            eventos.OfType<RecordatorioDeCargueEnviado>().ShouldHaveSingleItem().DiasAntes.ShouldBe(15);
            eventos.OfType<ExpedienteCaducado>().ShouldHaveSingleItem().EstadoPrevio.ShouldBe(EstadoExpediente.Abierto);
            (await EventosAsync(tenantId, vinculo.Id)).OfType<ExpedienteCerrado>().ShouldHaveSingleItem().Resultado.ShouldBe(ResultadoExpediente.Caducado);
            var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{expedienteId}/historial", Admin(e.Norte), null, 200);
            historial.Select(h => h.Tipo).ShouldContain(t => t.Contains("caducado", StringComparison.OrdinalIgnoreCase));

            // La empresa lo reabre: plazo nuevo desde ahora, vínculo en validación y enlace nuevo para el tercero.
            var reabierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/reabrir", Admin(e.Norte),
                new ReabrirExpedienteDto("El proveedor pidió más tiempo."), 200);
            reabierto.Estado.ShouldBe("Abierto");
            reabierto.PlazoCargue!.Reaperturas.ShouldBe(1);
            reabierto.PlazoCargue.Hasta!.Value.ShouldBeGreaterThan(fixture.Reloj.GetUtcNow().AddDays(29));
            (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).ExpedienteAbiertoId.ShouldBe(expedienteId);
            var aviso = await fixture.Correos.EsperarAsync(correo, m => m.Asunto.Contains("reabrió"));
            RegexToken().IsMatch(aviso.Texto).ShouldBeTrue();
            (await CodigoAsync(await ApiAsync("POST", $"/api/v1/expedientes/{expedienteId}/reabrir", Admin(e.Norte), new ReabrirExpedienteDto(), 409)))
                .ShouldBe("estado-invalido");

            // El job de Plataforma con fecha simulada (Development) vuelve a caducarlo al vencer el plazo nuevo.
            var resultado = await ApiAsync<ResultadoRevisionPlazosDto>("POST", "/api/v1/plataforma/plazos-de-cargue/revisar",
                new Credencial(ApiFixture.ClavePlataforma), new RevisarPlazosDto(tenantId, reabierto.PlazoCargue.Hasta.Value.AddMinutes(1)), 200);
            resultado.Caducados.ShouldBe(1);
            (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Caducado");
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task El_plazo_del_flujo_se_configura_y_un_flujo_sin_plazo_no_caduca()
    {
        var e = await PrepararAsync();
        var editado = await ApiAsync<FlujoDto>("PUT", $"/api/v1/flujos/{e.Flujo.Id}", Admin(e.Norte), new EditarFlujoDto(PlazoCargue: new PlazoCargueDto(0)), 200);
        editado.Borrador!.PlazoCargue!.Dias.ShouldBe(0);
        editado.Borrador.PlazoCargue.Recordatorios!.ShouldBeEmpty();
        (await CodigoAsync(await ApiAsync("PUT", $"/api/v1/flujos/{e.Flujo.Id}", Admin(e.Norte),
            new EditarFlujoDto(PlazoCargue: new PlazoCargueDto(10, [10])), 422))).ShouldBe("recordatorio-invalido");
        (await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{e.Flujo.Id}/publicar", Admin(e.Norte), new { }, 200)).Publicada!.PlazoCargue!.Dias.ShouldBe(0);

        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await ExpedienteAbiertoAsync(e, vinculo.Id);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200)).PlazoCargue.ShouldBeNull();

        var resultado = await ApiAsync<ResultadoRevisionPlazosDto>("POST", "/api/v1/plataforma/plazos-de-cargue/revisar",
            new Credencial(ApiFixture.ClavePlataforma), new RevisarPlazosDto(e.Norte.Datos.TenantId, DateTimeOffset.UtcNow.AddYears(1)), 200);
        resultado.Revisados.ShouldBe(0);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Abierto");
    }
}
