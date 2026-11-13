using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- cuentas bancarias y cuenta designada (CB-1, CB-2) ----------

    [Fact]
    public async Task El_tercero_designa_su_cuenta_y_la_empresa_la_aprueba_con_dos_personas_distintas()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var otraAprobadora = await OficialAsync(e, "Olga Tesorería");

        // El tercero registra su cuenta: solo ve los últimos cuatro dígitos.
        var cuenta = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Bancolombia", "Ahorros", "031-123456-78", "Aceros del Norte S.A.S.", "NIT 900123456"), 201);
        cuenta.NumeroEnmascarado.ShouldBe("****5678");
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Bancolombia", "Nómina", "0311234567", "Aceros", "NIT 900123456"), 422))).ShouldBe("tipo-cuenta-invalido");

        // Designarla exige la certificación bancaria en la bóveda.
        var designar = $"/public/v1/vinculos/{vinculo.Id}/cuenta-designada";
        (await CodigoAsync(await TerceroAsync("POST", designar, sesion.AccessToken, new DesignarCuentaDto(cuenta.Id), 422))).ShouldBe("certificacion-requerida");
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación Bancolombia"));
        var propuesta = await TerceroAsync<CuentaBancariaPublicaDto>("POST", designar, sesion.AccessToken, new DesignarCuentaDto(cuenta.Id), 200);
        propuesta.DesignadaEn.ShouldHaveSingleItem().Estado.ShouldBe("Propuesta");

        // Quien no aprueba cuentas la ve enmascarada y no puede aprobarla.
        var url = $"/api/v1/vinculos/{vinculo.Id}/cuenta-designada";
        var vistaRevisor = await ApiAsync<CuentaDesignadaDelVinculoDto>("GET", url, Delegado(e.Norte, e.Laura), null, 200);
        vistaRevisor.Propuesta!.NumeroEnmascarado.ShouldBe("****5678");
        vistaRevisor.Propuesta.Numero.ShouldBeNull();
        vistaRevisor.PuedesAprobar.ShouldBeFalse();
        vistaRevisor.AprobadoresRequeridos.ShouldBe(2);
        await ApiAsync("POST", $"{url}/aprobar", Delegado(e.Norte, e.Laura), new { }, 403);
        (await CodigoAsync(await ApiAsync("POST", $"{url}/aprobar", Admin(e.Norte), new { }, 422))).ShouldBe("usuario-delegado-requerido");

        // Primera aprobación: ve el número completo para cotejarlo con la certificación; no puede aprobar dos veces.
        var vistaCarlos = await ApiAsync<CuentaDesignadaDelVinculoDto>("GET", url, Delegado(e.Norte, e.Carlos), null, 200);
        vistaCarlos.Propuesta!.Numero.ShouldBe("03112345678");
        vistaCarlos.PuedesAprobar.ShouldBeTrue();
        var una = await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, e.Carlos), new { }, 200);
        una.Actual.ShouldBeNull();
        una.Propuesta!.Aprobaciones.ShouldHaveSingleItem().Nombre.ShouldBe("Carlos Cumplimiento");
        una.PuedesAprobar.ShouldBeFalse();
        (await CodigoAsync(await ApiAsync("POST", $"{url}/aprobar", Delegado(e.Norte, e.Carlos), new { }, 409))).ShouldBe("aprobador-repetido");

        // La segunda persona la deja designada.
        var designada = await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, otraAprobadora), new { }, 200);
        designada.Propuesta.ShouldBeNull();
        designada.Actual!.Estado.ShouldBe("Aprobada");
        designada.Actual.Aprobaciones.Select(a => a.Nombre).ShouldBe(["Carlos Cumplimiento", "Olga Tesorería"]);

        var cuentas = await TerceroAsync<List<CuentaBancariaPublicaDto>>("GET", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken, null, 200);
        cuentas.ShouldHaveSingleItem().DesignadaEn.ShouldHaveSingleItem().Estado.ShouldBe("Aprobada");
        (await CodigoAsync(await TerceroAsync("POST", $"/public/v1/boveda/cuentas-bancarias/{cuenta.Id}/retirar", sesion.AccessToken, new { }, 409)))
            .ShouldBe("cuenta-en-uso");

        // Una cuenta nueva se rechaza con motivo: el tercero lo ve y la designada sigue siendo la anterior.
        var nueva = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Davivienda", "Corriente", "4700 1234 9999", "Aceros del Norte S.A.S.", "NIT 900123456"), 201);
        await TerceroAsync<CuentaBancariaPublicaDto>("POST", designar, sesion.AccessToken, new DesignarCuentaDto(nueva.Id), 200);
        (await CodigoAsync(await ApiAsync("POST", $"{url}/rechazar", Delegado(e.Norte, e.Carlos), new MotivoDto(" "), 422))).ShouldBe("motivo-requerido");
        var rechazada = await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/rechazar", Delegado(e.Norte, e.Carlos),
            new MotivoDto("El titular no coincide con la certificación."), 200);
        rechazada.Actual!.CuentaId.ShouldBe(cuenta.Id);
        rechazada.Rechazada!.MotivoRechazo.ShouldBe("El titular no coincide con la certificación.");

        cuentas = await TerceroAsync<List<CuentaBancariaPublicaDto>>("GET", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken, null, 200);
        var vistaNueva = cuentas.Single(c => c.Id == nueva.Id).DesignadaEn.ShouldHaveSingleItem();
        vistaNueva.Estado.ShouldBe("Rechazada");
        vistaNueva.MotivoRechazo.ShouldBe("El titular no coincide con la certificación.");
        (await TerceroAsync<CuentaBancariaPublicaDto>("POST", $"/public/v1/boveda/cuentas-bancarias/{nueva.Id}/retirar", sesion.AccessToken, new { }, 200))
            .Estado.ShouldBe("Retirada");
    }

    [Fact]
    public async Task El_expediente_de_un_tipo_que_exige_cuenta_espera_en_revision_y_se_aprueba_solo_cuando_la_empresa_aprueba_la_cuenta()
    {
        var e = await PrepararAsync();
        await ExigirCuentaDelProveedorAsync(e.Norte.Admin, true); // CB-4: PROVEEDOR exige la cuenta designada aprobada
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        await AceptarAsync(sesion, vinculo.Id);
        var olga = await OficialAsync(e, "Olga Tesorería");

        var expedienteId = await ExpedienteAbiertoDelVinculoAsync(e, vinculo.Id);

        // Todas las áreas aprueban, pero sin cuenta designada aprobada el expediente no se aprueba: espera en revisión.
        await AreasAprueban(e, expedienteId);
        var enEspera = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        enEspera.Estado.ShouldBe("EnRevision");
        enEspera.Etapas.ShouldAllBe(etapa => etapa.Estado == "Aprobada");
        enEspera.EsperandoCuentaDesignada.ShouldBeTrue();
        enEspera.EsperandoVerificaciones.ShouldBeFalse(); // no falta cumplimiento: solo la cuenta
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).Estado.ShouldNotBe("Activo");
        // El tercero también ve que falta su cuenta designada aprobada.
        (await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200))
            .EsperandoCuentaDesignada.ShouldBeTrue();

        // El tercero designa su cuenta y la empresa la rechaza: el expediente sigue esperando (no se rechaza).
        var url = $"/api/v1/vinculos/{vinculo.Id}/cuenta-designada";
        var designar = $"/public/v1/vinculos/{vinculo.Id}/cuenta-designada";
        var primera = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Bancolombia", "Ahorros", "031-123456-78", "Aceros del Norte S.A.S.", "NIT 900123456"), 201);
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación Bancolombia"));
        await TerceroAsync<CuentaBancariaPublicaDto>("POST", designar, sesion.AccessToken, new DesignarCuentaDto(primera.Id), 200);
        await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/rechazar", Delegado(e.Norte, e.Carlos), new MotivoDto("El titular no coincide con la certificación."), 200);
        var tras = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        (tras.Estado, tras.EsperandoCuentaDesignada).ShouldBe(("EnRevision", true));

        // Designa otra: con una sola aprobación aún espera; con la segunda persona la cuenta queda designada y el expediente se aprueba solo.
        var segunda = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Davivienda", "Corriente", "4700 1234 9999", "Aceros del Norte S.A.S.", "NIT 900123456"), 201);
        await TerceroAsync<CuentaBancariaPublicaDto>("POST", designar, sesion.AccessToken, new DesignarCuentaDto(segunda.Id), 200);
        await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, e.Carlos), new { }, 200);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200)).Estado.ShouldBe("EnRevision");

        await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, olga), new { }, 200);

        var aprobado = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        aprobado.Estado.ShouldBe("Aprobado");
        aprobado.EsperandoCuentaDesignada.ShouldBeFalse();
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Activo");
        (await ApiAsync<CertificadoDelVinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/certificado", Admin(e.Norte), null, 200)).Actual!.Estado.ShouldBe("Vigente");
        (await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200))
            .EsperandoCuentaDesignada.ShouldBeFalse();
    }

    [Fact]
    public async Task Si_la_cuenta_ya_esta_aprobada_cuando_las_areas_aprueban_el_expediente_cierra_sin_esperar()
    {
        var e = await PrepararAsync();
        await ExigirCuentaDelProveedorAsync(e.Norte.Admin, true);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        await AceptarAsync(sesion, vinculo.Id);
        var olga = await OficialAsync(e, "Olga Tesorería");

        // La cuenta se designa y se aprueba con el expediente en revisión, antes de que las áreas decidan.
        var url = $"/api/v1/vinculos/{vinculo.Id}/cuenta-designada";
        var cuenta = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Bancolombia", "Ahorros", "031-123456-78", "Aceros del Norte S.A.S.", "NIT 900123456"), 201);
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación Bancolombia"));
        await TerceroAsync<CuentaBancariaPublicaDto>("POST", $"/public/v1/vinculos/{vinculo.Id}/cuenta-designada", sesion.AccessToken, new DesignarCuentaDto(cuenta.Id), 200);
        await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, e.Carlos), new { }, 200);
        await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, olga), new { }, 200);

        var expedienteId = await ExpedienteAbiertoDelVinculoAsync(e, vinculo.Id);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200)).Estado.ShouldBe("EnRevision");
        await AreasAprueban(e, expedienteId);

        var cerrado = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e.Norte), null, 200);
        (cerrado.Estado, cerrado.EsperandoCuentaDesignada).ShouldBe(("Aprobado", false));
    }

    /// <summary>El expediente abierto del vínculo (espera a que se abra, hasta 20 s).</summary>
    private async Task<Guid> ExpedienteAbiertoDelVinculoAsync(Escenario e, Guid vinculoId)
    {
        var limite = DateTime.UtcNow.AddSeconds(20);
        while (true)
        {
            if ((await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculoId}", Admin(e.Norte), null, 200)).ExpedienteAbiertoId is { } id)
                return id;
            DateTime.UtcNow.ShouldBeLessThan(limite);
            await Task.Delay(200);
        }
    }

    /// <summary>Cada área aprueba los documentos que le tocan.</summary>
    private async Task AreasAprueban(Escenario e, Guid expedienteId)
    {
        foreach (var (area, persona, codigo) in new[] { (e.Compras, e.Laura, "COMPRAS"), (e.Cumplimiento, e.Carlos, "CUMPLIMIENTO") })
        {
            var leido = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Delegado(e.Norte, persona), null, 200);
            var porDecidir = Area(leido, codigo).DocumentosPorDecidir ?? [];
            if (porDecidir.Count > 0)
                await AreaAsync<ExpedienteDto>(e, expedienteId, area, persona,
                    new VeredictoDto("Aprobado", Versiones: porDecidir.ToDictionary(d => d.RequisitoId, d => d.VersionVigente)));
        }
    }

    /// <summary>Otra persona de Norte con el rol de oficial de cumplimiento (aprueba cuentas).</summary>
    private async Task<Persona> OficialAsync(Escenario e, string nombre)
    {
        var correo = $"{nombre.Split(' ')[0].ToLowerInvariant()}.{Guid.NewGuid():N}@constructora.co";
        var usuario = await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", new Credencial(e.Norte.Admin),
            new CrearUsuarioDto(nombre, correo, null, [new RolAsignadoDto("OficialCumplimiento", e.Norte.Datos.Id)], null), 201);
        return new Persona(usuario, "srm-" + Guid.NewGuid().ToString("N")[..8]);
    }
}
