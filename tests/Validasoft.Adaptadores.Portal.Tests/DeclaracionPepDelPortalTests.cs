using Validasoft.Adaptadores.Portal;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Ficha.Importacion;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Adaptadores.Portal.Tests;

/// <summary>
/// La declaración jurada PEP del Portal (HU 49093) al importarla: solo la de un contacto que hoy responde «Sí» a «¿Es PEP?», con la huella de la firma
/// (la imagen está en el disco del Portal), países y tipos de documento convertidos (lo que no se pueda, vacío con advertencia) y, sin declaración, el faltante.
/// </summary>
public class DeclaracionPepDelPortalTests
{
    private static readonly DateOnly Hoy = new(2026, 11, 12);

    private static TerDeclaracionPep Declaracion(int terCamId) => new(
        TerCamId: terCamId, RelacionEmpresa: 1, Nacionalidad: "Colombia", Direccion: "Cra 7 # 1-2", CargoDesempenado: "Alcalde municipal",
        Institucion: "Alcaldía de Sopó", PaisInstitucion: "Colombia", FechaVinculacion: new DateTime(2018, 1, 1), FechaDesvinculacion: new DateTime(2022, 1, 1),
        CargoVigente: false, TieneCuentasExterior: true, PaisesCuentasExterior: "[\"Estados Unidos\",\"Narnia\"]", ConyugeNombre: "Luis Ruiz",
        ConyugeTipoDocumento: "CC", ConyugeNumeroDocumento: "80111222", TieneVinculoPep: true, VinculoPepNombre: "Carlos Ruiz", VinculoPepParentesco: "Cuñado",
        VinculoPepCargoInstitucion: "Senador, Senado", VinculoPepFechaVinculacion: new DateTime(2022, 8, 1), VinculoPepVigente: true, AceptaJuramento: true,
        FirmaEnCalidad: 1, FirmanteNombre: "María López Díaz", FirmanteTipoDocumento: "CC", FirmanteNumeroDocumento: "52987654",
        FechaFirma: new DateTime(2026, 10, 5), FirmaHash: new string('A', 64),
        Familiares: [new TerDeclaracionPepFamiliar("Marta López", "Hermana", "CC", "52111222"), new TerDeclaracionPepFamiliar("Pedro López", "Padre", "XX", "79111222")],
        Sociedades: [new TerDeclaracionPepSociedad("Inversiones SAS", "900111222", 4, "Fiduciante"), new TerDeclaracionPepSociedad("Fiducia X", "900333444", 3)],
        Vinculos: [new TerDeclaracionPepVinculo(1, "Cuñado(a) · 2.° afinidad", "Carlos Ruiz", "Senador", "Senado", "Colombia", new DateTime(2022, 8, 1), null, true)]);

    private static TerceroDelPortal Natural(bool pep, params TerDeclaracionPep[] declaraciones) => new(new TercerosPortal(202, 3),
        new TerInformacionGeneral(2, "María", "López Díaz", "N", "CC", "52.987.654", null, "maria@correo.co", 2, "Cra 7 # 1-2", 11, "3001234567", null, false, null, "Colombia"),
        InfoTributaria: new TerInfoTributaria(),
        CamaraComercio: [new TerCamaraComercio(1, 1, "CC", "52987654", "María", "López", TerCamEsPep: pep, TerCamVinculoPep: false, TerFechaExpedicion: "20/05/2001", TerLugarExpedicion: "Medellín")],
        Siso: new TerSISO(),
        DeclaracionesPep: declaraciones);

    private static ResultadoDeTraduccion Traducir(TerceroDelPortal t) =>
        TraductorDelPortal.Traducir(t, TraductorDelPortalTests.Catalogos, TraductorDelPortalTests.Plataforma(), Hoy);

    private static T Datos<T>(ResultadoDeTraduccion r, string seccion) => (T)r.Secciones.Single(s => s.Definicion.Codigo == seccion).Datos;

    private static ResultadoDeRegla Regla(ResultadoDeTraduccion r, string seccion)
    {
        var s = r.Secciones.Single(x => x.Definicion.Codigo == seccion);
        return ReglasDeFicha.Evaluar(s.Definicion, s.Datos, new ContextoDeSeccion(r.Tercero!.TipoPersona, r.Tercero.Identificacion, null, Hoy),
            TraductorDelPortalTests.Plataforma(), false, 0);
    }

    [Fact]
    public void La_declaracion_del_natural_se_importa_con_la_huella_de_su_firma_y_sin_imagen()
    {
        var r = Traducir(Natural(pep: true, Declaracion(1)));
        var general = Datos<DatosGenerales>(r, CodigosDeSeccion.General);

        general.Pep.ShouldBe(true);
        var d = general.DeclaracionPep.ShouldNotBeNull();
        (d.Nombre, d.TipoIdentificacion, d.Numero, d.Direccion, d.Nacionalidad).ShouldBe(("María López Díaz", "CC", "52987654", "Cra 7 # 1-2", "CO"));
        (d.RelacionEmpresa, d.Cargo, d.Institucion, d.Pais).ShouldBe((RelacionConLaEmpresa.Proveedor, "Alcalde municipal", "Alcaldía de Sopó", "CO"));
        (d.FechaVinculacion, d.Vigente, d.FechaDesvinculacion).ShouldBe((new DateOnly(2018, 1, 1), false, new DateOnly(2022, 1, 1)));
        d.CuentasExterior.ShouldBe(true);
        d.PaisesCuentasExterior.ShouldBe(["US"]); // «Narnia» no es un país ISO: queda fuera con su advertencia
        d.Conyuge.ShouldBe(new ConyugePep { Nombre = "Luis Ruiz", TipoIdentificacion = "CC", Numero = "80111222" });
        d.Sociedades!.Select(s => (s.Nombre, s.Calidad, s.CalidadOtra)).ShouldBe([("Inversiones SAS", CalidadEnSociedad.Otra, "Fiduciante"), ("Fiducia X", CalidadEnSociedad.Fideicomitente, null)]);
        d.VinculoConPep!.Vinculos.ShouldHaveSingleItem().ShouldBe(new PepRelacionada
        {
            Tipo = TipoVinculoPep.Familiar, Relacion = "Cuñado(a) · 2.° afinidad", Nombre = "Carlos Ruiz", Cargo = "Senador", Entidad = "Senado", Pais = "CO", FechaVinculacion = new DateOnly(2022, 8, 1), Vigente = true
        });
        (d.AceptaJuramento, d.FirmaEnCalidad, d.FirmanteNombre, d.FirmanteNumero).ShouldBe((true, FirmaEnCalidad.NombrePropio, "María López Díaz", "52987654"));
        (d.FirmaPng, d.FirmaHuella, d.FechaFirma, d.FirmaIp).ShouldBe((null, new string('a', 64), new DateOnly(2026, 10, 5), null));

        r.Advertencias.ShouldContain(a => a.Codigo == "pais-no-reconocido" && a.Campo == "declaracion_pep.paises_cuentas_exterior");
        r.Advertencias.ShouldContain(a => a.Codigo == "tipo-documento-sin-equivalente" && a.Campo == "declaracion_pep.familiares");
    }

    [Fact]
    public void Lo_que_no_se_pudo_convertir_deja_la_declaracion_como_faltante_y_lo_demas_la_completa()
    {
        var r = Traducir(Natural(pep: true, Declaracion(1)));
        // El familiar con el tipo «XX» quedó sin tipo de documento: es un faltante de la sección, no un rechazo.
        var regla = Regla(r, CodigosDeSeccion.General);
        regla.Errores.ShouldBeEmpty();
        regla.Completitud.Faltantes.Select(f => f.Campo).ShouldBe(["declaracion_pep.familiares[1].tipo_identificacion"]);

        var sinFamiliares = Natural(pep: true, Declaracion(1) with { Familiares = null });
        var completa = Regla(Traducir(sinFamiliares), CodigosDeSeccion.General);
        completa.Completitud.Faltantes.ShouldBeEmpty();
        completa.Completitud.Completa.ShouldBeTrue(); // sin imagen: vale la huella de la firma que guardó el Portal
    }

    [Fact]
    public void Un_si_a_pep_sin_declaracion_queda_como_faltante_y_la_de_un_no_no_se_importa()
    {
        var sin = Traducir(Natural(pep: true));
        Datos<DatosGenerales>(sin, CodigosDeSeccion.General).DeclaracionPep.ShouldBeNull();
        Regla(sin, CodigosDeSeccion.General).Completitud.Faltantes.Select(f => f.Campo).ShouldBe(["declaracion_pep"]);

        // Una declaración que el Portal conserva de cuando era «Sí» (el contacto ya responde «No») no viaja.
        var inactiva = Traducir(Natural(pep: false, Declaracion(1)));
        Datos<DatosGenerales>(inactiva, CodigosDeSeccion.General).DeclaracionPep.ShouldBeNull();
        Regla(inactiva, CodigosDeSeccion.General).Completitud.Faltantes.ShouldBeEmpty();
    }

    [Fact]
    public void En_la_juridica_cada_persona_pep_lleva_la_suya_y_la_relacion_sale_de_su_rol()
    {
        var tercero = new TerceroDelPortal(new TercerosPortal(101, 3),
            new TerInformacionGeneral(1, "Aceros del Norte S.A.S.", null, "J", "NIT", "900123456", 0, "COMPRAS@ACEROS.CO", 1, "Calle 45 # 12-08", 10, "6015551234", "aceros.co", true, null, "Colombia", 2005),
            InfoTributaria: new TerInfoTributaria(),
            CamaraComercio:
            [
                new TerCamaraComercio(1, 1, "CC", "79123456", "Juan", "Pérez", "Gerente", null, true, false, 1, TerNacionalidad: "Colombia"),
                new TerCamaraComercio(2, 2, "CC", "80111222", "Luis", "Ruiz", null, "100", true, false),
                new TerCamaraComercio(3, 3, "CC", "52111333", "Ana", "Mora", TerCamEsPep: false, TerCamVinculoPep: false)
            ],
            Siso: new TerSISO(),
            DeclaracionesPep: [Declaracion(2)]);

        var r = Traducir(tercero);
        var personas = Datos<DatosLegales>(r, CodigosDeSeccion.Legal).Personas!;
        personas[0].DeclaracionPep.ShouldBeNull(); // el representante es PEP y no tiene declaración
        var socio = personas[1].DeclaracionPep.ShouldNotBeNull();
        (socio.RelacionEmpresa, socio.Nombre, socio.Numero, socio.FirmaHuella).ShouldBe((RelacionConLaEmpresa.Socio, "Luis Ruiz", "80111222", new string('a', 64)));
        personas[2].DeclaracionPep.ShouldBeNull();

        Regla(r, CodigosDeSeccion.Legal).Completitud.Faltantes.Select(f => f.Campo ?? f.Regla)
            .ShouldContain("personas[0].declaracion_pep");
        r.Advertencias.ShouldContain(a => a.Codigo == "pais-no-reconocido" && a.Campo == "personas[1].declaracion_pep.paises_cuentas_exterior");
    }
}
