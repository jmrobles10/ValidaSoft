using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Integración abierta (IO-3, IO-4): contactos del tercero por empresa, datos de pago genéricos (banco del catálogo con su código de
/// compensación local y su SWIFT, contacto de pagos) y el lugar de expedición del documento del representante.
/// </summary>
public class IntegracionAbiertaTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 9, 0, 0, TimeSpan.FromHours(-5));

    private static Vinculo NuevoVinculo() =>
        Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "compras@proveedor.co", Ahora));

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    // ---------- contactos por empresa ----------

    [Fact]
    public void Los_contactos_se_normalizan_se_guardan_en_el_vinculo_y_repetir_el_mismo_cambio_no_hace_nada()
    {
        var vinculo = NuevoVinculo();
        var evento = vinculo.ActualizarContactos(
        [
            new ContactoDeEmpresa("  Ana Gómez ", "Jefe de compras", " ANA@Proveedor.CO ", " 3001234567 ", RolDeContacto.Comercial),
            new ContactoDeEmpresa("Luis Pérez", "  ", "pagos@proveedor.co", null, RolDeContacto.Pagos)
        ], Ahora).ShouldNotBeNull();

        evento.Antes.ShouldBeEmpty();
        evento.Despues.ShouldBe(
        [
            new ContactoDeEmpresa("Ana Gómez", "Jefe de compras", "ana@proveedor.co", "3001234567", RolDeContacto.Comercial),
            new ContactoDeEmpresa("Luis Pérez", null, "pagos@proveedor.co", null, RolDeContacto.Pagos)
        ]);
        vinculo.Apply(evento);
        vinculo.Contactos.Count.ShouldBe(2);

        // Idempotente: la misma lista (aunque llegue sin normalizar) no genera evento.
        vinculo.ActualizarContactos(
        [
            new ContactoDeEmpresa("Ana Gómez", "Jefe de compras", "ANA@proveedor.co", "3001234567", RolDeContacto.Comercial),
            new ContactoDeEmpresa("Luis Pérez", null, "pagos@proveedor.co", null, RolDeContacto.Pagos)
        ], Ahora).ShouldBeNull();

        // Reemplaza la lista completa y deja la anterior en el evento; la lista vacía los quita todos.
        var quitar = vinculo.ActualizarContactos([], Ahora.AddDays(1)).ShouldNotBeNull();
        quitar.Antes.Count.ShouldBe(2);
        quitar.Despues.ShouldBeEmpty();
        vinculo.Apply(quitar);
        vinculo.Contactos.ShouldBeEmpty();
        vinculo.ActualizarContactos(null, Ahora).ShouldBeNull();
    }

    [Fact]
    public void Un_contacto_necesita_nombre_y_correo_validos_un_rol_conocido_y_no_se_repite_con_el_mismo_rol()
    {
        var vinculo = NuevoVinculo();
        Codigo(() => vinculo.ActualizarContactos([new ContactoDeEmpresa(" ", null, "a@b.co", null, RolDeContacto.Comercial)], Ahora)).ShouldBe("contacto-nombre-requerido");
        foreach (var correo in new[] { "", "sin-arroba", "@b.co", "a@", "a@b", "a b@c.co", "a@@b.co", "a@b@c.co" })
            Codigo(() => vinculo.ActualizarContactos([new ContactoDeEmpresa("Ana", null, correo, null, RolDeContacto.Comercial)], Ahora)).ShouldBe("correo-invalido", correo);
        Codigo(() => vinculo.ActualizarContactos([new ContactoDeEmpresa("Ana", null, "a@b.co", null, (RolDeContacto)99)], Ahora)).ShouldBe("contacto-rol-invalido");
        Codigo(() => vinculo.ActualizarContactos(
        [
            new ContactoDeEmpresa("Ana", null, "a@b.co", null, RolDeContacto.Pagos), new ContactoDeEmpresa("Ana G.", null, "A@B.co", null, RolDeContacto.Pagos)
        ], Ahora)).ShouldBe("contacto-repetido");
        // El mismo correo con otro rol sí: una persona puede atender dos funciones.
        vinculo.ActualizarContactos(
        [
            new ContactoDeEmpresa("Ana", null, "a@b.co", null, RolDeContacto.Pagos), new ContactoDeEmpresa("Ana", null, "a@b.co", null, RolDeContacto.Cumplimiento)
        ], Ahora).ShouldNotBeNull();
    }

    [Fact]
    public void Los_contactos_tienen_limites_y_un_vinculo_anulado_ya_no_los_cambia()
    {
        var vinculo = NuevoVinculo();
        Codigo(() => vinculo.ActualizarContactos(
            [.. Enumerable.Range(0, Vinculo.MaximoContactos + 1).Select(i => new ContactoDeEmpresa("C" + i, null, $"c{i}@b.co", null, RolDeContacto.Notificaciones))], Ahora))
            .ShouldBe("contactos-demasiados");
        Codigo(() => vinculo.ActualizarContactos([new ContactoDeEmpresa(new string('N', Vinculo.MaximoNombreDeContacto + 1), null, "a@b.co", null, RolDeContacto.Comercial)], Ahora))
            .ShouldBe("contacto-demasiado-largo");
        Codigo(() => vinculo.ActualizarContactos([new ContactoDeEmpresa("Ana", new string('C', Vinculo.MaximoCargoDeContacto + 1), "a@b.co", null, RolDeContacto.Comercial)], Ahora))
            .ShouldBe("contacto-demasiado-largo");
        Codigo(() => vinculo.ActualizarContactos([new ContactoDeEmpresa("Ana", null, "a@b.co", new string('9', Vinculo.MaximoTelefonoDeContacto + 1), RolDeContacto.Comercial)], Ahora))
            .ShouldBe("contacto-demasiado-largo");

        vinculo.Apply(vinculo.Anular("Se equivocaron de empresa.", Ahora));
        Codigo(() => vinculo.ActualizarContactos([new ContactoDeEmpresa("Ana", null, "a@b.co", null, RolDeContacto.Comercial)], Ahora)).ShouldBe("vinculo-anulado");
    }

    [Fact]
    public void Los_roles_de_contacto_son_los_cuatro_del_diseno()
    {
        Enum.GetNames<RolDeContacto>().ShouldBe(["Comercial", "Pagos", "Cumplimiento", "Notificaciones"]);
    }

    // ---------- datos de pago ----------

    private static DatosDeCuentaBancaria Cuenta(string? codigo = null, string? swift = null, ContactoDePagos? pagos = null) =>
        new("CO", "Bancolombia", "Ahorros", "031-123456-78", "Aceros S.A.S.", "NIT 900123456", "COP", codigo, swift, pagos);

    [Fact]
    public void La_cuenta_guarda_el_codigo_del_banco_su_swift_normalizado_y_el_contacto_de_pagos()
    {
        var cuenta = CuentaBancaria.Registrar(Guid.NewGuid(),
            Cuenta(" 1007 ", " colo cobm ", new ContactoDePagos(" Marta Ríos ", " PAGOS@Aceros.CO ", " 6015551234 ")), n => n, Ahora);

        (cuenta.BancoCodigo, cuenta.BancoSwift).ShouldBe(("1007", "COLOCOBM"));
        cuenta.ContactoPagos.ShouldBe(new ContactoDePagos("Marta Ríos", "pagos@aceros.co", "6015551234"));

        var simple = CuentaBancaria.Registrar(Guid.NewGuid(), Cuenta(), n => n, Ahora);
        (simple.BancoCodigo, simple.BancoSwift, simple.ContactoPagos).ShouldBe(((string?)null, (string?)null, (ContactoDePagos?)null));
        CuentaBancaria.Registrar(Guid.NewGuid(), Cuenta(pagos: new ContactoDePagos("", "", null)), n => n, Ahora).ContactoPagos.ShouldBeNull();
    }

    [Theory]
    [InlineData("COLOCOB")]
    [InlineData("COLOCOBM1")]
    [InlineData("1OLOCOBM")]
    [InlineData("COLO-COBM")]
    public void Un_swift_con_formato_invalido_es_422(string swift) =>
        Codigo(() => CuentaBancaria.Registrar(Guid.NewGuid(), Cuenta(swift: swift), n => n, Ahora)).ShouldBe("swift-invalido");

    [Fact]
    public void El_contacto_de_pagos_pide_nombre_y_un_correo_valido()
    {
        Codigo(() => CuentaBancaria.Registrar(Guid.NewGuid(), Cuenta(pagos: new ContactoDePagos("", "a@b.co", null)), n => n, Ahora)).ShouldBe("contacto-pagos-nombre-requerido");
        Codigo(() => CuentaBancaria.Registrar(Guid.NewGuid(), Cuenta(pagos: new ContactoDePagos("Marta", "no-es-correo", null)), n => n, Ahora)).ShouldBe("correo-invalido");
        Codigo(() => CuentaBancaria.Registrar(Guid.NewGuid(), Cuenta(pagos: new ContactoDePagos("Marta", "", null)), n => n, Ahora)).ShouldBe("correo-invalido");
    }

    [Theory]
    [InlineData("colo cobm", "COLOCOBM", true)]
    [InlineData("DEUTDEFF500", "DEUTDEFF500", true)]
    [InlineData("  ", null, true)]
    [InlineData(null, null, true)]
    [InlineData("COLOCO", "COLOCO", false)]
    public void El_swift_se_normaliza_a_mayusculas_sin_espacios_y_se_valida(string? entrada, string? esperado, bool valido)
    {
        ReglasDeCatalogo.NormalizarSwift(entrada, out var ok).ShouldBe(esperado);
        ok.ShouldBe(valido);
    }

    [Fact]
    public void El_catalogo_de_bancos_trae_el_pais_del_codigo_local_y_el_swift_y_los_valida()
    {
        var (entrada, rechazo) = ReglasDeCatalogo.Validar(CatalogoDePlataforma.Bancos, new FilaDeCatalogo("1007", "Bancolombia", Swift: "colocobm"), _ => null, Ahora);
        rechazo.ShouldBeNull();
        (entrada!.Codigo, entrada.Pais, entrada.Swift).ShouldBe(("1007", "CO", "COLOCOBM"));

        ReglasDeCatalogo.Validar(CatalogoDePlataforma.Bancos, new FilaDeCatalogo("1007", "Bancolombia", Swift: "XX"), _ => null, Ahora).Rechazo!.ShouldContain("SWIFT");
        ReglasDeCatalogo.Validar(CatalogoDePlataforma.Bancos, new FilaDeCatalogo("1007", "Bancolombia", Pais: "COL"), _ => null, Ahora).Rechazo!.ShouldContain("ISO 3166");
        ReglasDeCatalogo.Validar(CatalogoDePlataforma.Bancos, new FilaDeCatalogo("ABC", "Banco"), _ => null, Ahora).Rechazo!.ShouldContain("código");

        // El SWIFT cuenta como contenido: cambiarlo es una actualización de la entrada y no pasa inadvertido.
        var sin = ReglasDeCatalogo.Validar(CatalogoDePlataforma.Bancos, new FilaDeCatalogo("1007", "Bancolombia"), _ => null, Ahora).Entrada!;
        sin.MismoContenido(entrada).ShouldBeFalse();
        sin.MismoContenido(ReglasDeCatalogo.Validar(CatalogoDePlataforma.Bancos, new FilaDeCatalogo("1007", "Bancolombia"), _ => null, Ahora.AddDays(1)).Entrada!).ShouldBeTrue();
    }

    // ---------- lugar de expedición del representante ----------

    [Fact]
    public void El_lugar_de_expedicion_del_representante_se_normaliza_y_admite_hasta_cien_caracteres()
    {
        var contexto = new ContextoDeSeccion(TipoPersona.Juridica, Identificacion.Crear("CO", "NIT", "900373913"), null, new DateOnly(2026, 11, 11));
        var definicion = CatalogoDeSecciones.Requerida(CodigosDeSeccion.Legal);
        string Json(string lugar) => """
            { "personas": [ { "rol": "RepresentanteLegal", "tipo_representacion": "Principal", "tipo_identificacion": "CC", "numero": "79123456",
              "nombres": "María", "apellidos": "López", "lugar_expedicion": "
            """ + lugar + "\" } ] }";

        var datos = (DatosLegales)ReglasDeFicha.Leer(definicion, JsonDocument.Parse(Json("  Bogotá D.C.  ")).RootElement.Clone(), contexto);
        datos.Personas!.Single().LugarExpedicion.ShouldBe("Bogotá D.C.");
        ReglasDeFicha.Evaluar(definicion, datos, contexto, CatalogosEnMemoria.Vacios, false, 0).Errores.ShouldBeEmpty();

        var larga = (DatosLegales)ReglasDeFicha.Leer(definicion, JsonDocument.Parse(Json(new string('x', 101))).RootElement.Clone(), contexto);
        ReglasDeFicha.Evaluar(definicion, larga, contexto, CatalogosEnMemoria.Vacios, false, 0).Errores
            .ShouldContain(e => e.Campo == "personas[0].lugar_expedicion");
    }
}
