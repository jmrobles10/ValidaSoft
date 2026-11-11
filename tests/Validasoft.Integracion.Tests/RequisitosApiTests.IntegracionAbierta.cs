using System.Security.Cryptography;
using System.Text.Json;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- integración abierta: referencias externas, contactos por empresa, datos de pago y embebido genérico (IO) ----------

    /// <summary>Un sistema integrador cualquiera (ni SRM ni Portal) con su API key de los permisos dados, para la empresa.</summary>
    private async Task<Credencial> SistemaNuevoAsync(Empresa empresa, string codigo, params string[] scopes) =>
        await SistemaNuevoConOrigenesAsync(empresa, codigo, null, scopes);

    private async Task<Credencial> SistemaNuevoConOrigenesAsync(Empresa empresa, string codigo, IReadOnlyList<string>? origenes, params string[] scopes)
    {
        var rsa = RSA.Create(2048);
        var sistema = await ApiAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", new Credencial(empresa.Admin),
            new CrearSistemaIntegradoDto(codigo, "Sistema " + codigo, rsa.ExportSubjectPublicKeyInfoPem(), origenes), 201);
        var llave = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(empresa.Admin),
            new CrearApiKeyDto("Integración " + codigo, scopes, null, null, SistemaId: sistema.Id), 201);
        return new Credencial(llave.Valor!, empresa.Datos.Id);
    }

    private static readonly string[] ScopesDeReferencias = ["terceros:leer", "terceros:escribir", "vinculos:leer", "vinculos:escribir"];

    private Task<TerceroDto> TerceroConReferenciaAsync(Credencial sistema, string nit, string? claveExterna, int estado = 201) =>
        ApiAsync<TerceroDto>("POST", "/api/v1/terceros", sistema, new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null, null,
            claveExterna is null ? null : new ReferenciaExternaDto(string.Empty, claveExterna)), estado);

    [Fact]
    public async Task Referencias_externas_del_tercero_y_del_vinculo_son_idempotentes_por_sistema_y_privadas_de_cada_tenant()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var sur = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        var erpNorte = await SistemaNuevoAsync(norte, "ERPX", ScopesDeReferencias);
        var erpSur = await SistemaNuevoAsync(sur, "ERPX", ScopesDeReferencias); // el mismo código en otro tenant: no se mezclan
        var crmNorte = await SistemaNuevoAsync(norte, "CRMX", ScopesDeReferencias);

        // 1. Crear el tercero con su clave en el sistema: 201; repetir la misma creación es idempotente (200, el mismo tercero).
        var nit = Nit();
        var tercero = await TerceroConReferenciaAsync(erpNorte, nit, "C-77");
        (await TerceroConReferenciaAsync(erpNorte, nit, "C-77", 200)).Id.ShouldBe(tercero.Id);
        (await ApiAsync<List<ReferenciaExternaDto>>("GET", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, null, 200))
            .ShouldBe([new ReferenciaExternaDto("ERPX", "C-77")]);

        // La clave ya es de otro tercero: 409. Una llave sin sistema integrador o de otro sistema no puede registrar la referencia.
        var ocupada = await ApiAsync("POST", "/api/v1/terceros", erpNorte, new CrearTerceroDto(new IdentificacionDto("CO", "NIT", Nit()), "Juridica", "Otro", null, null,
            new ReferenciaExternaDto("ERPX", "C-77")), 409);
        (await CodigoAsync(ocupada)).ShouldBe("referencia-duplicada");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/terceros/{tercero.Id}/referencias", new Credencial(norte.Admin), new ReferenciaExternaDto("ERPX", "C-1"), 422)))
            .ShouldBe("referencia-sin-sistema");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("CRMX", "C-1"), 422)))
            .ShouldBe("referencia-sistema-invalido");
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("ERPX", "  "), 422)))
            .ShouldBe("referencia-externa-invalida");

        // 2. Un tercero puede tener varias claves en un sistema (p. ej. una por instancia); agregarla es 201 y repetirla, 200.
        var dos = await ApiAsync<List<ReferenciaExternaDto>>("POST", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("erpx", "C-78"), 201);
        dos.ShouldBe([new ReferenciaExternaDto("ERPX", "C-77"), new ReferenciaExternaDto("ERPX", "C-78")]);
        (await ApiAsync<List<ReferenciaExternaDto>>("POST", $"/api/v1/terceros/{tercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("ERPX", "C-78"), 200)).Count.ShouldBe(2);
        (await ApiAsync<TerceroDto>("GET", "/api/v1/terceros/por-referencia?sistema=erpx&clave=C-78", erpNorte, null, 200)).Id.ShouldBe(tercero.Id);
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/terceros/por-referencia?sistema=ERPX&clave=NO-EXISTE", erpNorte, null, 404))).ShouldBe("tercero-no-encontrado");
        // Otro sistema del mismo tenant tiene sus propias claves.
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/terceros/por-referencia?sistema=CRMX&clave=C-78", crmNorte, null, 404))).ShouldBe("tercero-no-encontrado");
        var otroTercero = await TerceroConReferenciaAsync(erpNorte, Nit(), null);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/terceros/{otroTercero.Id}/referencias", erpNorte, new ReferenciaExternaDto("ERPX", "C-77"), 409))).ShouldBe("referencia-duplicada");

        // 3. Las referencias son de cada tenant: el mismo código de sistema en otro tenant no ve las de Norte ni choca con ellas.
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/terceros/por-referencia?sistema=ERPX&clave=C-77", erpSur, null, 404))).ShouldBe("tercero-no-encontrado");
        await ApiAsync("GET", $"/api/v1/terceros/{tercero.Id}/referencias", erpSur, null, 404); // ni siquiera ve al tercero de Norte
        var deSur = await TerceroConReferenciaAsync(erpSur, Nit(), "C-77");
        (await ApiAsync<TerceroDto>("GET", "/api/v1/terceros/por-referencia?sistema=ERPX&clave=C-77", erpSur, null, 200)).Id.ShouldBe(deSur.Id);
        (await ApiAsync<TerceroDto>("GET", "/api/v1/terceros/por-referencia?sistema=ERPX&clave=C-77", erpNorte, null, 200)).Id.ShouldBe(tercero.Id);

        // 4. Vínculos: idempotente por referencia; otra clave del mismo sistema es conflicto; otro sistema comparte la validación.
        var correo = CorreoNuevo();
        var vinculo = await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", erpNorte, new CrearVinculoDto(TerceroId: tercero.Id, Tercero: null,
            TipoVinculo: "PROVEEDOR", Atributos: null, CorreoInvitacion: correo, ReferenciaExterna: new ReferenciaExternaDto("ERPX", "S-1")), 201);
        vinculo.Referencias.ShouldBe([new ReferenciaExternaDto("ERPX", "S-1")]);
        (await ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", erpNorte, new CrearVinculoDto(TerceroId: tercero.Id, Tercero: null, TipoVinculo: "PROVEEDOR",
            Atributos: null, CorreoInvitacion: correo, ReferenciaExterna: new ReferenciaExternaDto("ERPX", "S-1")), 200)).Id.ShouldBe(vinculo.Id);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", erpNorte, new ReferenciaExternaDto("ERPX", "S-2"), 409))).ShouldBe("referencia-en-conflicto");
        (await ApiAsync<VinculoDto>("GET", "/api/v1/vinculos/por-referencia?sistema=erpx&clave=S-1", erpNorte, null, 200)).Id.ShouldBe(vinculo.Id);
        (await ApiAsync<VinculoDto>("GET", "/api/v1/vinculos/por-referencia?sistema=ERPX&id=S-1", erpNorte, null, 200)).Id.ShouldBe(vinculo.Id); // el nombre anterior sigue valiendo

        var compartido = await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", crmNorte, new ReferenciaExternaDto("CRMX", "K-9"), 200);
        compartido.Referencias.ShouldBe([new ReferenciaExternaDto("ERPX", "S-1"), new ReferenciaExternaDto("CRMX", "K-9")]);
        (await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", crmNorte, new ReferenciaExternaDto("CRMX", "K-9"), 200)).Referencias.Count.ShouldBe(2);
        (await ApiAsync<VinculoDto>("GET", "/api/v1/vinculos/por-referencia?sistema=CRMX&clave=K-9", crmNorte, null, 200)).Id.ShouldBe(vinculo.Id);
    }
}

public sealed partial class RequisitosApiTests
{
    private static readonly string[] ScopesDeErp = [.. ScopesDeReferencias, "eventos:leer"];

    private const string LegalConLugarDeExpedicion = """
        { "personas": [
            { "rol": "RepresentanteLegal", "tipo_representacion": "Principal", "tipo_identificacion": "CC", "numero": "79123456", "nombres": "María", "apellidos": "López",
              "cargo": "Gerente", "fecha_expedicion": "2001-05-10", "lugar_expedicion": "  Bogotá D.C. ", "fecha_nacimiento": "1980-03-02", "nacionalidad": "CO",
              "residencia": "CO", "correo": "maria@aceros.co", "telefono": "+57 601 555 1234", "pep": false, "vinculo_pep": false },
            { "rol": "Socio", "tipo_identificacion": "CC", "numero": "52123456", "nombres": "Juan", "apellidos": "Pérez", "porcentaje": 100, "pep": false, "vinculo_pep": false }
        ] }
        """;

    [Fact]
    public async Task La_ficha_del_vinculo_trae_para_cualquier_erp_los_contactos_las_referencias_los_datos_de_pago_y_lo_declarado_en_la_ficha()
    {
        var e = await PrepararAsync();
        var erp = await SistemaNuevoAsync(e.Norte, "ERPX", ScopesDeErp);
        var oficial = await OficialAsync(e, "Olga Tesorería");
        await FlujoConAsync(e.Norte, "CLIENTE",
        [
            new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL", AreasQueRevisan: [e.Compras.Id]),
            new RequisitoDelFlujoDto(Seccion: "FICHA_TRIBUTARIA", AreasQueRevisan: [e.Cumplimiento.Id]),
            new RequisitoDelFlujoDto(Seccion: "FICHA_LEGAL", AreasQueRevisan: [e.Cumplimiento.Id])
        ]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "CLIENTE", correo);
        var rutaFicha = $"/api/v1/vinculos/{vinculo.Id}/ficha";

        // El sistema registra su clave del tercero y del vínculo; ambas salen en la ficha.
        await ApiAsync<List<ReferenciaExternaDto>>("POST", $"/api/v1/terceros/{vinculo.TerceroId}/referencias", erp, new ReferenciaExternaDto(string.Empty, "C-1"), 201);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", erp, new ReferenciaExternaDto("ERPX", "S-1"), 200);

        // Contactos: antes de aceptar la invitación el tercero no puede indicarlos; la empresa sí (vinculos:escribir).
        var sesion = await SesionAsync(correo);
        var contactosTercero = new ActualizarContactosDto(
        [
            new ContactoDeEmpresaDto("Ana Gómez", "Jefe de compras", "ANA@aceros.co", "3001234567", "Comercial"),
            new ContactoDeEmpresaDto("Luis Pérez", null, "pagos@aceros.co", null, "Pagos")
        ]);
        (await CodigoAsync(await TerceroAsync("PUT", $"/public/v1/vinculos/{vinculo.Id}/contactos", sesion.AccessToken, contactosTercero, 409))).ShouldBe("invitacion-no-aceptada");
        await AceptarAsync(sesion, vinculo.Id);
        (await CodigoAsync(await ApiAsync("PUT", $"/api/v1/vinculos/{vinculo.Id}/contactos", erp, new ActualizarContactosDto(
            [new ContactoDeEmpresaDto("Ana", null, "ana@aceros", null, "Comercial")]), 422))).ShouldBe("correo-invalido");
        (await CodigoAsync(await ApiAsync("PUT", $"/api/v1/vinculos/{vinculo.Id}/contactos", erp, new ActualizarContactosDto(
            [new ContactoDeEmpresaDto("Ana", null, "ana@aceros.co", null, "Marte")]), 422))).ShouldBe("contacto-rol-invalido");
        var delTercero = await TerceroAsync<List<ContactoDeEmpresaDto>>("PUT", $"/public/v1/vinculos/{vinculo.Id}/contactos", sesion.AccessToken, contactosTercero, 200);
        delTercero.ShouldBe(
        [
            new ContactoDeEmpresaDto("Ana Gómez", "Jefe de compras", "ana@aceros.co", "3001234567", "Comercial"),
            new ContactoDeEmpresaDto("Luis Pérez", null, "pagos@aceros.co", null, "Pagos")
        ]);
        // Repetir el mismo cambio no genera otro evento; la empresa ve y reemplaza los contactos desde el vínculo.
        await TerceroAsync<List<ContactoDeEmpresaDto>>("PUT", $"/public/v1/vinculos/{vinculo.Id}/contactos", sesion.AccessToken, contactosTercero, 200);
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", erp, null, 200)).Contactos.ShouldBe(delTercero);
        var porEmpresa = await ApiAsync<VinculoDto>("PUT", $"/api/v1/vinculos/{vinculo.Id}/contactos", erp, new ActualizarContactosDto(
            [.. delTercero, new ContactoDeEmpresaDto("Carla Ruiz", "Oficial", "carla@aceros.co", null, "Cumplimiento")]), 200);
        porEmpresa.Contactos!.Select(c => c.Rol).ShouldBe(["Comercial", "Pagos", "Cumplimiento"]);
        (await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", sesion.AccessToken, null, 200)).Single().Contactos!.Count.ShouldBe(3);

        // El evento del webhook y del pull lleva la lista completa de contactos.
        var eventos = await EventosDeAsync(e.Norte, "vinculo.contactos_actualizados", vinculo.Id, 2);
        eventos.Count.ShouldBe(2); // el segundo cambio del tercero era idéntico y no generó evento
        eventos[^1].Datos.GetProperty("contactos").EnumerateArray().Select(c => c.GetProperty("rol").GetString()).ShouldBe(["Comercial", "Pagos", "Cumplimiento"]);
        eventos[^1].Datos.GetProperty("referencias").EnumerateArray().Select(r => r.GetProperty("id").GetString()).ShouldContain("S-1");
        (await ApiAsync<List<TipoEventoPublicoDto>>("GET", "/api/v1/eventos/tipos", Admin(e.Norte), null, 200)).ShouldContain(t => t.Tipo == "vinculo.contactos_actualizados");

        // Ficha del tercero: la tributaria declara la exención de CREE y la legal el lugar de expedición.
        var expedienteId = await AbiertoEnAsync(e.Norte, vinculo.Id);
        await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        await GuardarSeccionAsync(sesion, "FICHA_TRIBUTARIA", TributariaCompleta.Replace("\"responsable_ica\": true,", "\"responsable_ica\": true, \"exento_rete_cree\": true,"));
        await GuardarSeccionAsync(sesion, "FICHA_LEGAL", LegalConLugarDeExpedicion);

        // Datos de pago: banco del catálogo (nombre, código de compensación y SWIFT) y contacto de pagos.
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("x", "Ahorros", "031-123456-78", "Aceros", "NIT 900123456", BancoCodigo: "9999"), 422))).ShouldBe("banco-no-encontrado");
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("x", "Ahorros", "031-123456-78", "Aceros", "NIT 900123456", BancoCodigo: "1007", BancoSwift: "XX"), 422))).ShouldBe("swift-invalido");
        (await CodigoAsync(await TerceroAsync("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("x", "Ahorros", "031-123456-78", "Aceros", "NIT 900123456", BancoCodigo: "1007",
                ContactoPagos: new ContactoDePagosDto("Marta", "no-es-correo", null)), 422))).ShouldBe("correo-invalido");
        var cuenta = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("ignorado", "Ahorros", "031-123456-78", "Aceros del Norte S.A.S.", "NIT 900123456", BancoCodigo: "1007",
                ContactoPagos: new ContactoDePagosDto(" Marta Ríos ", "PAGOS@Aceros.co", "6015551234")), 201);
        (cuenta.Banco, cuenta.BancoCodigo, cuenta.BancoSwift).ShouldBe(("Bancolombia", "1007", "COLOCOBM"));
        cuenta.ContactoPagos.ShouldBe(new ContactoDePagosDto("Marta Ríos", "pagos@aceros.co", "6015551234"));
        var extranjera = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Chase", "Corriente", "123456789", "Aceros del Norte S.A.S.", "NIT 900123456", "USD", "US", BancoSwift: "chasus33"), 201);
        (extranjera.Banco, extranjera.BancoCodigo, extranjera.BancoSwift).ShouldBe(("Chase", null, "CHASUS33"));

        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación Bancolombia"));
        await TerceroAsync<CuentaBancariaPublicaDto>("POST", $"/public/v1/vinculos/{vinculo.Id}/cuenta-designada", sesion.AccessToken, new DesignarCuentaDto(cuenta.Id), 200);
        var urlCuenta = $"/api/v1/vinculos/{vinculo.Id}/cuenta-designada";
        await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{urlCuenta}/aprobar", Delegado(e.Norte, e.Carlos), new { }, 200);
        var designada = await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{urlCuenta}/aprobar", Delegado(e.Norte, oficial), new { }, 200);
        (designada.Actual!.BancoCodigo, designada.Actual.BancoSwift, designada.Actual.ContactoPagos!.Correo).ShouldBe(("1007", "COLOCOBM", "pagos@aceros.co"));

        await EsperarAsync(e.Norte, expedienteId, x => x.Estado == "EnRevision");
        (await AprobarComoAdministradorAsync(e.Norte, expedienteId)).Estado.ShouldBe("Aprobado");

        // GET /vinculos/{id}/ficha: todo lo que un ERP necesita, sin nombres de sistemas concretos.
        var ficha = await ApiAsync<FichaDelVinculoDto>("GET", rutaFicha, erp, null, 200);
        ficha.Referencias.ShouldBe([new ReferenciaExternaDto("ERPX", "S-1")]);
        ficha.ReferenciasDelTercero.ShouldBe([new ReferenciaExternaDto("ERPX", "C-1")]);
        ficha.Contactos!.Select(c => (c.Nombre, c.Cargo, c.Correo, c.Rol)).ShouldBe(
        [
            ("Ana Gómez", "Jefe de compras", "ana@aceros.co", "Comercial"), ("Luis Pérez", null, "pagos@aceros.co", "Pagos"), ("Carla Ruiz", "Oficial", "carla@aceros.co", "Cumplimiento")
        ]);
        var banco = ficha.CuentaDesignada.ShouldNotBeNull();
        (banco.Banco, banco.BancoCodigo, banco.BancoSwift, banco.NumeroEnmascarado, banco.Numero).ShouldBe(("Bancolombia", "1007", "COLOCOBM", "****5678", null)); // sin cuentas:aprobar no ve el número
        banco.ContactoPagos.ShouldBe(new ContactoDePagosDto("Marta Ríos", "pagos@aceros.co", "6015551234"));

        var tributaria = ficha.Secciones.Single(s => s.Seccion == "FICHA_TRIBUTARIA").Contenido.Tributaria!;
        tributaria.Calidades.ExentoReteCree.ShouldBe(true);
        tributaria.CalidadesSinDerivar.Select(c => c.Calidad).ShouldNotContain("exentoReteCree");
        var representante = ficha.RepresentanteLegal.ShouldNotBeNull();
        (representante.Nombre, representante.LugarExpedicion, representante.FechaExpedicion).ShouldBe(("María López", "Bogotá D.C.", new DateOnly(2001, 5, 10)));
        ficha.Secciones.Single(s => s.Seccion == "FICHA_LEGAL").Contenido.Legal!.Personas.Select(p => p.LugarExpedicion).ShouldBe(["Bogotá D.C.", null]);

        // El evento de la cuenta designada lleva el código del banco y su SWIFT.
        var cuentaEvento = (await EventosDeAsync(e.Norte, "vinculo.cuenta_designada", vinculo.Id, 1)).Single().Datos;
        (cuentaEvento.GetProperty("bancoCodigo").GetString(), cuentaEvento.GetProperty("bancoSwift").GetString()).ShouldBe(("1007", "COLOCOBM"));

        // Sin los permisos de lectura no hay ficha.
        await ApiAsync("GET", rutaFicha, await SistemaNuevoAsync(e.Norte, "SOLOESCRIBE", "terceros:escribir"), null, 403);
    }

    /// <summary>Eventos públicos de un tipo y vínculo por el pull (la suscripción corre después del commit); espera a que lleguen los esperados.</summary>
    private async Task<List<EventoPublicoDto>> EventosDeAsync(Empresa empresa, string tipo, Guid vinculoId, int esperados)
    {
        var limite = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            var pagina = await ApiAsync<PaginaEventosDto>("GET", $"/api/v1/eventos?tipos={tipo}&limite=200", Admin(empresa), null, 200);
            var deEste = pagina.Datos.Where(x => x.Datos.GetProperty("vinculoId").GetGuid() == vinculoId).ToList();
            if (deEste.Count >= esperados)
                return deEste;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"Llegaron {deEste.Count} de {esperados} {tipo}.");
            await Task.Delay(200);
        }
    }
}

public sealed partial class RequisitosApiTests
{
    private const string OrigenAnfitrion = "https://anfitrion.ficticio.test";

    private static readonly string[] ScopesDeEmbebido = [.. ScopesDeReferencias, "sesiones-tercero:crear"];

    private static (JsonElement Cabecera, JsonElement Cuerpo) LeerJwt(string token)
    {
        static JsonElement Parte(string base64Url) =>
            JsonDocument.Parse(Convert.FromBase64String(base64Url.Replace('-', '+').Replace('_', '/').PadRight((base64Url.Length + 3) / 4 * 4, '='))).RootElement.Clone();
        var partes = token.Split('.');
        return (Parte(partes[0]), Parte(partes[1]));
    }

    [Fact]
    public async Task Sesion_embebida_del_tercero_para_cualquier_sistema_ligada_al_vinculo_el_origen_y_el_componente()
    {
        var e = await PrepararAsync();
        var erp = await SistemaNuevoConOrigenesAsync(e.Norte, "ERPX", [OrigenAnfitrion], ScopesDeEmbebido);
        var sinPermiso = await SistemaNuevoConOrigenesAsync(e.Norte, "SINPERMISO", [OrigenAnfitrion], ScopesDeReferencias);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var otroVinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", CorreoNuevo());
        var url = "/api/v1/sesiones-tercero";
        CrearSesionEmbebidaDto Pedir(Guid vinculoId, string origen = OrigenAnfitrion, string componente = "boveda", int? minutos = null) => new(vinculoId, componente, origen, minutos);

        // El vínculo debe llevar la referencia del sistema y el tercero haber aceptado la invitación.
        (await CodigoAsync(await ApiAsync("POST", url, erp, Pedir(vinculo.Id), 403))).ShouldBe("vinculo-ajeno");
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", erp, new ReferenciaExternaDto("ERPX", "S-1"), 200);
        (await CodigoAsync(await ApiAsync("POST", url, erp, Pedir(vinculo.Id), 409))).ShouldBe("invitacion-no-aceptada");
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);

        // Permisos y entradas: la llave sin el permiso, la de administración (sin sistema) y los datos inválidos no abren sesión.
        await ApiAsync("POST", url, sinPermiso, Pedir(vinculo.Id), 403);
        (await CodigoAsync(await ApiAsync("POST", url, new Credencial(e.Norte.Admin, e.Norte.Datos.Id), Pedir(vinculo.Id), 403))).ShouldBe("sistema-requerido");
        (await CodigoAsync(await ApiAsync("POST", url, erp, Pedir(vinculo.Id, componente: "bandeja"), 422))).ShouldBe("componente-invalido");
        (await CodigoAsync(await ApiAsync("POST", url, erp, Pedir(vinculo.Id, minutos: 16), 422))).ShouldBe("vigencia-invalida");
        (await CodigoAsync(await ApiAsync("POST", url, erp, Pedir(vinculo.Id, minutos: 0), 422))).ShouldBe("vigencia-invalida");
        (await CodigoAsync(await ApiAsync("POST", url, erp, Pedir(vinculo.Id, origen: "https://*.anfitrion.test"), 422))).ShouldBe("origen-invalido");
        await ApiAsync("POST", url, erp, Pedir(Guid.NewGuid()), 404);

        // Un origen que no es de los del sistema es 403, aunque sea de otro sistema del mismo tenant o esté bien escrito.
        (await CodigoAsync(await ApiAsync("POST", url, erp, Pedir(vinculo.Id, origen: "https://intruso.ficticio.test"), 403))).ShouldBe("origen-no-permitido");
        (await CodigoAsync(await ApiAsync("POST", url, erp, Pedir(vinculo.Id, origen: "http://anfitrion.ficticio.test"), 422))).ShouldBe("origen-invalido");
        // El origen se normaliza como lo reporta el navegador (esquema y host en minúsculas, sin barra final).
        (await ApiAsync<SesionEmbebidaDto>("POST", url, erp, Pedir(vinculo.Id, origen: "HTTPS://Anfitrion.Ficticio.TEST/"), 201)).Origen.ShouldBe(OrigenAnfitrion);

        // La sesión: token firmado de pocos minutos, ligado a un solo vínculo, al sistema, al origen y al componente.
        var embebida = await ApiAsync<SesionEmbebidaDto>("POST", url, erp, Pedir(vinculo.Id, componente: "Ficha", minutos: 5), 201);
        (embebida.Origen, embebida.Componente, embebida.VinculoId, embebida.TerceroId).ShouldBe((OrigenAnfitrion, "ficha", vinculo.Id, vinculo.TerceroId));
        embebida.Url.ShouldBe($"{ApiFixture.UrlPublica}/embed/ficha");
        embebida.Url.ShouldNotContain(embebida.Token); // el token nunca va en la URL
        (embebida.Expira - DateTimeOffset.UtcNow).ShouldBeInRange(TimeSpan.FromMinutes(4), TimeSpan.FromMinutes(5.1));
        var (cabecera, claims) = LeerJwt(embebida.Token);
        cabecera.GetProperty("alg").GetString().ShouldBe("HS256");
        (claims.GetProperty("embebido").GetString(), claims.GetProperty("sistema").GetString(), claims.GetProperty("origen").GetString(),
            claims.GetProperty("componente").GetString(), claims.GetProperty("terceroId").GetGuid()).ShouldBe(("true", "ERPX", OrigenAnfitrion, "ficha", vinculo.TerceroId));
        claims.GetProperty("vinculos").ValueKind.ShouldBe(JsonValueKind.String); // un solo vínculo (un reclamo)
        claims.GetProperty("vinculos").GetString()!.ShouldStartWith(vinculo.Id.ToString());

        // El token sirve como cualquier sesión del tercero, pero solo para ese vínculo: el otro de la misma empresa es 404 para él.
        var vinculos = await TerceroAsync<List<VinculoPublicoDto>>("GET", "/public/v1/vinculos", embebida.Token, null, 200);
        vinculos.Select(v => v.Id).ShouldBe([vinculo.Id]);
        await TerceroAsync("GET", $"/public/v1/invitaciones/{otroVinculo.Id}", embebida.Token, null, 404);
        var actual = await TerceroAsync<SesionTerceroDto>("GET", "/public/v1/sesion/actual", embebida.Token, null, 200);
        (actual.AccessToken, actual.TerceroId, actual.OtpVerificado, actual.Vinculos.Single().Id).ShouldBe((embebida.Token, vinculo.TerceroId, true, vinculo.Id));
        actual.Expira.ShouldBe(embebida.Expira, TimeSpan.FromSeconds(1));
        // Cada componente es un token distinto y no sirve en la API de empresa (otra audiencia).
        await ApiAsync("GET", "/api/v1/vinculos", new Credencial(embebida.Token, e.Norte.Datos.Id), null, 401);

        // Un sistema solo abre sesiones de lo que integra: la referencia de otro sistema no alcanza, y otra empresa no ve el vínculo.
        var otroSistema = await SistemaNuevoConOrigenesAsync(e.Norte, "CRMX", [OrigenAnfitrion], ScopesDeEmbebido);
        (await CodigoAsync(await ApiAsync("POST", url, otroSistema, Pedir(vinculo.Id), 403))).ShouldBe("vinculo-ajeno");
        var sur = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        var erpSur = await SistemaNuevoConOrigenesAsync(sur, "ERPX", [OrigenAnfitrion], ScopesDeEmbebido);
        await ApiAsync("POST", url, erpSur, Pedir(vinculo.Id), 404);

        // Un vínculo anulado no se embebe.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/anular", Admin(e.Norte), new MotivoDto("Se equivocaron."), 200);
        (await CodigoAsync(await ApiAsync("POST", url, erp, Pedir(vinculo.Id), 409))).ShouldBe("vinculo-anulado");
    }

    [Fact]
    public async Task La_sesion_embebida_no_se_guarda_en_el_registro_de_idempotencia_y_el_permiso_va_en_el_rol_de_administracion()
    {
        var e = await PrepararAsync();
        var erp = await SistemaNuevoConOrigenesAsync(e.Norte, "ERPX", [OrigenAnfitrion], ScopesDeEmbebido);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/referencias", erp, new ReferenciaExternaDto("ERPX", "S-1"), 200);
        await AceptarAsync(await SesionAsync(correo), vinculo.Id);

        // Con la misma Idempotency-Key el token no se repite (se emite uno nuevo): la respuesta con un secreto no se guarda 24 h.
        var cuerpo = new CrearSesionEmbebidaDto(vinculo.Id, "expediente", OrigenAnfitrion);
        async Task<SesionEmbebidaDto> PedirAsync() => (await Host.Scenario(s =>
        {
            s.Post.Json(cuerpo).ToUrl("/api/v1/sesiones-tercero");
            s.WithRequestHeader("X-Api-Key", erp.ApiKey);
            s.WithRequestHeader("X-Empresa", e.Norte.Datos.Id.ToString());
            s.WithRequestHeader("Idempotency-Key", "embebido-repetido");
            s.StatusCodeShouldBe(201);
        })).ReadAsJson<SesionEmbebidaDto>()!;
        var primera = await PedirAsync();
        var segunda = await PedirAsync();
        segunda.Token.ShouldNotBe(primera.Token);

        // El rol de administración la trae; las llaves ya emitidas sin ella no abren sesiones.
        Contratos_DelTenant().ShouldContain("sesiones-tercero:crear");
    }

    private static IReadOnlyList<string> Contratos_DelTenant() => Validasoft.Aplicacion.Comun.Permisos.DelTenant;
}
