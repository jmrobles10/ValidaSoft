using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- plantillas de notificación (PL-1) ----------

    private const string RutaPlantillas = "/api/v1/plantillas-notificacion";

    [Fact]
    public async Task Las_plantillas_personalizan_los_correos_de_la_empresa_con_vista_previa_y_auditoria()
    {
        var e = await PrepararAsync();
        var norte = e.Norte;
        Task<List<PlantillaNotificacionDto>> CatalogoAsync(Credencial credencial) =>
            ApiAsync<List<PlantillaNotificacionDto>>("GET", RutaPlantillas, credencial, null, 200);

        // El catálogo: los 6 correos sin personalizar, con el asunto que sale hoy y sus variables.
        var catalogo = await CatalogoAsync(Admin(norte));
        catalogo.Select(p => p.Evento).ShouldBe(["invitacion", "observacion", "resultado", "consentimiento-empresa", "vencido", "certificado", "caducidad"]);
        catalogo.ShouldAllBe(p => p.Canal == "correo" && p.Version == 0 && p.Asunto == null && p.Cabecera == null && p.Pie == null);
        var invitacion = catalogo[0];
        invitacion.AsuntoPorDefecto.ShouldBe("Norte S.A.S. te invita a validar tu vínculo como proveedor");
        invitacion.VariablesPermitidas.ShouldBe(["empresa", "tipoVinculo"]);
        catalogo[1].AsuntoPorDefecto.ShouldBe($"Norte S.A.S. tiene observaciones sobre tu validación NORTE-E-{DateTimeOffset.UtcNow.Year}-000001");

        // Vista previa de lo que se está editando, sin guardar: el HTML del usuario sale escapado.
        var cambios = new ActualizarPlantillaNotificacionDto("{{empresa}} te espera", "Bienvenido al registro de proveedores de {{empresa}}\n<b>Gracias</b>",
            "Compras · compras@norte.co");
        var previa = await ApiAsync<VistaPreviaCorreoDto>("POST", $"{RutaPlantillas}/invitacion/correo/vista-previa", Admin(norte), cambios, 200);
        previa.Asunto.ShouldBe("Norte S.A.S. te espera");
        previa.Html.ShouldStartWith("<p>Bienvenido al registro de proveedores de Norte S.A.S.<br>&lt;b&gt;Gracias&lt;/b&gt;</p>");
        previa.Texto.ShouldEndWith("Compras · compras@norte.co");
        (await CatalogoAsync(Admin(norte)))[0].Version.ShouldBe(0);

        // Guardar: versión 1 y auditoría de la empresa.
        var guardada = await ApiAsync<PlantillaNotificacionDto>("PUT", $"{RutaPlantillas}/invitacion/correo", Admin(norte), cambios, 200);
        guardada.Version.ShouldBe(1);
        guardada.Asunto.ShouldBe("{{empresa}} te espera");
        guardada.ActualizadaEn.ShouldNotBeNull();
        var auditoria = (await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?entidad=PlantillaNotificacion", Admin(norte), null, 200))
            .Datos.ShouldHaveSingleItem();
        auditoria.Accion.ShouldBe("Creado");
        auditoria.EmpresaId.ShouldBe(norte.Datos.Id);
        auditoria.Entidad.Id.ShouldBe($"{norte.Datos.Id:N}:invitacion:correo");

        // Una invitación nueva llega con el asunto, la cabecera y el pie (y el enlace de siempre).
        var correo = CorreoNuevo();
        await CrearVinculoAsync(norte, "PROVEEDOR", correo);
        var mensaje = await fixture.Correos.EsperarAsync(correo);
        mensaje.Asunto.ShouldBe("Norte S.A.S. te espera");
        mensaje.Texto.ShouldStartWith("Bienvenido al registro de proveedores de Norte S.A.S.\n<b>Gracias</b>\n\nNorte S.A.S. te invitó a Validasoft");
        mensaje.Texto.ShouldEndWith("\n\nCompras · compras@norte.co");
        mensaje.Html.ShouldContain("&lt;b&gt;Gracias&lt;/b&gt;</p>");
        mensaje.Html.ShouldNotContain("<b>Gracias</b>");
        RegexToken().IsMatch(mensaje.Texto).ShouldBeTrue();

        // Otra empresa del mismo grupo no usa la plantilla de Norte.
        var sur = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(norte.Admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "899999068"), "Sur S.A.S.", "SUR", null, null), 201);
        var adminSur = new Credencial(norte.Admin, sur.Id);
        (await CatalogoAsync(adminSur)).ShouldAllBe(p => p.Version == 0);
        var correoSur = CorreoNuevo();
        await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", adminSur, new CrearVinculoDto(TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", Nit()), "Juridica", "Tercero de Sur", null,
                [new ContactoDto("Ana Gómez", correoSur, null, "Comercial")]),
            TipoVinculo: "PROVEEDOR", Atributos: null, CorreoInvitacion: correoSur, ReferenciaExterna: null), 201);
        var deSur = await fixture.Correos.EsperarAsync(correoSur);
        deSur.Asunto.ShouldBe("Sur S.A.S. te invita a validar tu vínculo como proveedor");
        deSur.Texto.ShouldNotContain("Bienvenido");

        // Validaciones.
        (await CodigoAsync(await ApiAsync("PUT", $"{RutaPlantillas}/otp/correo", Admin(norte), cambios, 404))).ShouldBe("plantilla-no-encontrada");
        (await CodigoAsync(await ApiAsync("PUT", $"{RutaPlantillas}/invitacion/sms", Admin(norte), cambios, 422))).ShouldBe("canal-invalido");
        (await CodigoAsync(await ApiAsync("PUT", $"{RutaPlantillas}/invitacion/correo", Admin(norte),
            new ActualizarPlantillaNotificacionDto("Tu {{folio}}", null, null), 422))).ShouldBe("variable-no-permitida");
        (await CodigoAsync(await ApiAsync("POST", $"{RutaPlantillas}/invitacion/correo/vista-previa", Admin(norte),
            new ActualizarPlantillaNotificacionDto(new string('a', 151), null, null), 422))).ShouldBe("asunto-demasiado-largo");
        (await CatalogoAsync(Admin(norte)))[0].Version.ShouldBe(1);

        // Todo nulo vuelve al predeterminado (la versión igual sube).
        var restaurada = await ApiAsync<PlantillaNotificacionDto>("PUT", $"{RutaPlantillas}/invitacion/correo", Admin(norte),
            new ActualizarPlantillaNotificacionDto("", "  ", null), 200);
        restaurada.Version.ShouldBe(2);
        restaurada.Asunto.ShouldBeNull();
        restaurada.Cabecera.ShouldBeNull();
        var otroCorreo = CorreoNuevo();
        await CrearVinculoAsync(norte, "PROVEEDOR", otroCorreo);
        var predeterminado = await fixture.Correos.EsperarAsync(otroCorreo);
        predeterminado.Asunto.ShouldBe("Norte S.A.S. te invita a validar tu vínculo como proveedor");
        predeterminado.Texto.ShouldStartWith("Norte S.A.S. te invitó a Validasoft");
        (await ApiAsync<PaginaDto<RegistroAuditoriaDto>>("GET", "/api/v1/auditoria?entidad=PlantillaNotificacion", Admin(norte), null, 200))
            .Datos.Select(r => r.Accion).ShouldBe(["Modificado", "Creado"]);
    }
}
