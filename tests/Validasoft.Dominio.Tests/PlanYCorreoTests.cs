using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Habeas;
using Validasoft.Dominio.Plataforma;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Plan y límites por grupo (OD-14…OD-17), dominio de correo propio por empresa (OD-18…OD-22) y exportación de la bóveda (OD-9, OD-10):
/// reglas puras del dominio.
/// </summary>
public class PlanYCorreoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 11, 10, 0, 0, TimeSpan.FromHours(-5));

    // ---------- plan ----------

    [Fact]
    public void El_plan_valida_el_codigo_y_que_los_limites_no_sean_negativos()
    {
        var plan = PlanDelGrupo.Nuevo(Guid.NewGuid(), Ahora);
        plan.Limites.Tiene.ShouldBeFalse();

        plan.Actualizar(" Profesional ", new LimitesDePlan(10, 2, 500, 2048, 100_000), true, Ahora);
        plan.Codigo.ShouldBe("Profesional");
        plan.BloqueoSuave.ShouldBeTrue();
        plan.Limites.De(MetricaDePlan.Almacenamiento).ShouldBe(2048L * 1024 * 1024);
        plan.Limites.De(MetricaDePlan.Usuarios).ShouldBe(10);

        Should.Throw<ReglaDeNegocioException>(() => plan.Actualizar("x", LimitesDePlan.SinLimites, false, Ahora)).Codigo.ShouldBe("codigo-plan-invalido");
        Should.Throw<ReglaDeNegocioException>(() => plan.Actualizar("Plan", new LimitesDePlan(-1, null, null, null, null), false, Ahora)).Codigo.ShouldBe("limite-invalido");
        Should.Throw<ReglaDeNegocioException>(() => plan.Actualizar("Plan", new LimitesDePlan(null, null, null, null, -5), false, Ahora)).Codigo.ShouldBe("limite-invalido");
        plan.Actualizar("Plan", new LimitesDePlan(0, null, null, null, null), false, Ahora); // cero es un límite de verdad
        plan.Limites.Usuarios.ShouldBe(0);
    }

    [Theory]
    [InlineData(79, 100, NivelDeUso.Normal, 79, true)]
    [InlineData(80, 100, NivelDeUso.PorAgotar, 80, true)]
    [InlineData(99, 100, NivelDeUso.PorAgotar, 99, true)]
    [InlineData(100, 100, NivelDeUso.Agotado, 100, false)]
    [InlineData(130, 100, NivelDeUso.Agotado, 130, false)]
    public void La_medicion_dice_el_nivel_el_porcentaje_y_si_cabe_uno_mas(long usado, long limite, NivelDeUso nivel, int porcentaje, bool cabe)
    {
        var medicion = new MedicionDeMetrica(MetricaDePlan.Usuarios, usado, limite);
        medicion.Nivel.ShouldBe(nivel);
        medicion.Porcentaje.ShouldBe(porcentaje);
        medicion.Cabe().ShouldBe(cabe && usado < limite);
    }

    [Fact]
    public void Sin_limite_no_hay_nivel_ni_porcentaje_y_con_limite_cero_ya_esta_agotado()
    {
        var sin = new MedicionDeMetrica(MetricaDePlan.Empresas, 1_000_000, null);
        (sin.Nivel, sin.Porcentaje, sin.Cabe(1000)).ShouldBe((NivelDeUso.Normal, null, true));
        var cero = new MedicionDeMetrica(MetricaDePlan.Empresas, 0, 0);
        (cero.Nivel, cero.Porcentaje, cero.Cabe()).ShouldBe((NivelDeUso.Agotado, 100, false));
    }

    private static IReadOnlyList<(MedicionDeMetrica, int)> Avisos(PlanDelGrupo plan, long usuarios, long llamadas = 0, string mes = "2026-11") =>
        plan.AvisosPendientes([new MedicionDeMetrica(MetricaDePlan.Usuarios, usuarios, 10), new MedicionDeMetrica(MetricaDePlan.LlamadasApi, llamadas, 1000)], mes);

    [Fact]
    public void Cada_umbral_se_avisa_una_sola_vez_y_si_la_metrica_baja_y_vuelve_a_subir_se_avisa_de_nuevo()
    {
        var plan = PlanDelGrupo.Nuevo(Guid.NewGuid(), Ahora);
        Avisos(plan, 7).ShouldBeEmpty();

        Avisos(plan, 8).ShouldHaveSingleItem().Item2.ShouldBe(80);
        Avisos(plan, 9).ShouldBeEmpty(); // sigue en el mismo umbral
        var alLimite = Avisos(plan, 10).ShouldHaveSingleItem();
        (alLimite.Item1.Metrica, alLimite.Item2).ShouldBe((MetricaDePlan.Usuarios, 100));
        Avisos(plan, 11).ShouldBeEmpty();

        // Baja por debajo del 80 %: se limpia y, al volver a subir, avisa otra vez.
        Avisos(plan, 5).ShouldBeEmpty();
        plan.AvisosEnviados.ShouldNotContain(a => a.StartsWith("Usuarios:"));
        Avisos(plan, 8).ShouldHaveSingleItem().Item2.ShouldBe(80);
    }

    [Fact]
    public void Pasar_directo_al_cien_por_ciento_avisa_solo_ese_y_deja_el_del_ochenta_como_dado()
    {
        var plan = PlanDelGrupo.Nuevo(Guid.NewGuid(), Ahora);
        Avisos(plan, 12).ShouldHaveSingleItem().Item2.ShouldBe(100);
        plan.AvisosEnviados.ShouldContain("Usuarios:80:-");
        plan.AvisosEnviados.ShouldContain("Usuarios:100:-");
    }

    [Fact]
    public void Las_llamadas_a_la_api_avisan_de_nuevo_cada_mes()
    {
        var plan = PlanDelGrupo.Nuevo(Guid.NewGuid(), Ahora);
        Avisos(plan, 0, 800, "2026-11").ShouldHaveSingleItem().Item1.Metrica.ShouldBe(MetricaDePlan.LlamadasApi);
        Avisos(plan, 0, 850, "2026-11").ShouldBeEmpty();
        plan.AvisosEnviados.ShouldContain("LlamadasApi:80:2026-11");

        // Mes nuevo con el contador otra vez alto: el aviso del mes anterior no cuenta.
        Avisos(plan, 0, 810, "2026-12").ShouldHaveSingleItem();
        plan.AvisosEnviados.ShouldNotContain("LlamadasApi:80:2026-11");
    }

    // ---------- dominio de correo ----------

    [Theory]
    [InlineData("Proveedores.MiEmpresa.com", "proveedores.miempresa.com")]
    [InlineData("  @correo.empresa.co.  ", "correo.empresa.co")]
    public void El_dominio_se_normaliza(string escrito, string esperado) => DominioDeCorreoEmpresa.NormalizarDominio(escrito).ShouldBe(esperado);

    [Theory]
    [InlineData("")]
    [InlineData("localhost")]
    [InlineData("192.168.0.1")]
    [InlineData("sin espacios.com")]
    [InlineData("http://empresa.com")]
    [InlineData("-malo.com")]
    [InlineData("empresa..com")]
    public void Un_dominio_que_no_es_publico_se_rechaza(string escrito) =>
        Should.Throw<ReglaDeNegocioException>(() => DominioDeCorreoEmpresa.NormalizarDominio(escrito)).Codigo.ShouldBe("dominio-invalido");

    private static readonly IReadOnlyList<RegistroDns> Registros =
    [
        new(FinalidadDeRegistroDns.Spf, "TXT", "empresa.com", "v=spf1 include:spf.x ~all"),
        new(FinalidadDeRegistroDns.Dkim, "TXT", "vs1._domainkey.empresa.com", "v=DKIM1; k=rsa; p=AAAA"),
        new(FinalidadDeRegistroDns.Dmarc, "TXT", "_dmarc.empresa.com", "v=DMARC1; p=none")
    ];

    [Fact]
    public void El_dominio_nace_pendiente_y_solo_sale_con_su_remitente_cuando_esta_verificado()
    {
        var empresa = Guid.NewGuid();
        var dominio = DominioDeCorreoEmpresa.Registrar(null, empresa, "Empresa.com", null, null, Registros, Ahora);
        (dominio.Estado, dominio.ParteLocal, dominio.Direccion).ShouldBe((EstadoDeDominioDeCorreo.Pendiente, "no-responder", "no-responder@empresa.com"));
        dominio.RemitenteSiVerificado("Constructora Norte").ShouldBeNull(); // respaldo: el de la plataforma

        dominio.RegistrarVerificacion([.. Registros.Select(r => new ComprobacionDeRegistroDns(r.Finalidad, true, r.Valor))], Ahora);
        dominio.Estado.ShouldBe(EstadoDeDominioDeCorreo.Verificado);
        dominio.VerificadoEn.ShouldBe(Ahora);
        dominio.RemitenteSiVerificado("Constructora Norte").ShouldBe(new RemitenteDeCorreo("Constructora Norte", "no-responder@empresa.com"));

        dominio.NombreRemitente = "Compras Norte";
        dominio.RemitenteSiVerificado("Constructora Norte")!.Nombre.ShouldBe("Compras Norte");
    }

    [Fact]
    public void Un_registro_que_falta_deja_el_dominio_fallido_y_vuelve_al_respaldo()
    {
        var dominio = DominioDeCorreoEmpresa.Registrar(null, Guid.NewGuid(), "empresa.com", "proveedores", null, Registros, Ahora);
        dominio.RegistrarVerificacion(
        [
            new ComprobacionDeRegistroDns(FinalidadDeRegistroDns.Spf, true, "v=spf1 include:spf.x ~all"),
            new ComprobacionDeRegistroDns(FinalidadDeRegistroDns.Dkim, false, null),
            new ComprobacionDeRegistroDns(FinalidadDeRegistroDns.Dmarc, true, "v=DMARC1; p=none")
        ], Ahora);

        dominio.Estado.ShouldBe(EstadoDeDominioDeCorreo.Fallido);
        dominio.Verificaciones.ShouldBe(1);
        dominio.RemitenteSiVerificado("X").ShouldBeNull();

        // Una verificación posterior correcta lo corrige; si luego se pierde un registro, vuelve a fallido.
        dominio.RegistrarVerificacion([.. Registros.Select(r => new ComprobacionDeRegistroDns(r.Finalidad, true, r.Valor))], Ahora.AddHours(1));
        dominio.Estado.ShouldBe(EstadoDeDominioDeCorreo.Verificado);
        dominio.RegistrarVerificacion([], Ahora.AddHours(2));
        dominio.Estado.ShouldBe(EstadoDeDominioDeCorreo.Fallido);
        dominio.VerificadoEn.ShouldBeNull();
    }

    [Fact]
    public void Cambiar_solo_el_nombre_conserva_el_estado_y_cambiar_el_dominio_empieza_de_cero()
    {
        var empresa = Guid.NewGuid();
        var dominio = DominioDeCorreoEmpresa.Registrar(null, empresa, "empresa.com", null, null, Registros, Ahora);
        dominio.RegistrarVerificacion([.. Registros.Select(r => new ComprobacionDeRegistroDns(r.Finalidad, true, r.Valor))], Ahora);

        var mismo = DominioDeCorreoEmpresa.Registrar(dominio, empresa, "EMPRESA.com", "compras", "Compras", [], Ahora.AddDays(1));
        (mismo.Estado, mismo.ParteLocal, mismo.NombreRemitente, mismo.Registros.Count).ShouldBe((EstadoDeDominioDeCorreo.Verificado, "compras", "Compras", 3));

        var otro = DominioDeCorreoEmpresa.Registrar(mismo, empresa, "otra-empresa.com", null, null, Registros, Ahora.AddDays(2));
        (otro.Estado, otro.Dominio, otro.VerificadoEn).ShouldBe((EstadoDeDominioDeCorreo.Pendiente, "otra-empresa.com", null));
    }

    [Theory]
    [InlineData("no-reply<script>")]
    [InlineData("a b")]
    public void La_parte_local_invalida_se_rechaza(string parte) =>
        Should.Throw<ReglaDeNegocioException>(() => DominioDeCorreoEmpresa.NormalizarParteLocal(parte)).Codigo.ShouldBe("parte-local-invalida");

    [Fact]
    public void El_nombre_del_remitente_no_admite_signos_de_direccion()
    {
        DominioDeCorreoEmpresa.NormalizarNombre("  ").ShouldBeNull();
        DominioDeCorreoEmpresa.NormalizarNombre(" Compras Norte ").ShouldBe("Compras Norte");
        Should.Throw<ReglaDeNegocioException>(() => DominioDeCorreoEmpresa.NormalizarNombre("Norte <a@b.co>")).Codigo.ShouldBe("nombre-remitente-invalido");
    }

    // ---------- exportación de la bóveda ----------

    [Fact]
    public void La_exportacion_pasa_de_pendiente_a_lista_y_solo_se_descarga_mientras_esta_vigente()
    {
        var exportacion = ExportacionDeBoveda.Solicitar(Guid.NewGuid(), "t@correo.co", "10.0.0.1", Ahora);
        exportacion.EnCurso.ShouldBeTrue();
        Should.Throw<ReglaDeNegocioException>(() => exportacion.AsegurarDescargable(Ahora)).Codigo.ShouldBe("exportacion-no-disponible");

        exportacion.Iniciar(Ahora);
        exportacion.Terminar("boveda", "exportaciones/x.zip", 12, 4096, "sha", "hashm", "kid", TimeSpan.FromDays(7), Ahora);
        exportacion.EnCurso.ShouldBeFalse();
        exportacion.AsegurarDescargable(Ahora.AddDays(6));
        Should.Throw<ReglaDeNegocioException>(() => exportacion.AsegurarDescargable(Ahora.AddDays(8))).Codigo.ShouldBe("exportacion-vencida");

        exportacion.RegistrarDescarga("1.2.3.4", "navegador", Ahora.AddDays(1));
        (exportacion.Descargas, exportacion.Accesos.Count).ShouldBe((1, 1));

        exportacion.Vencer();
        (exportacion.Estado, exportacion.Clave, exportacion.Correo).ShouldBe((EstadoDeExportacionDeBoveda.Vencida, null, null));
        Should.Throw<ReglaDeNegocioException>(() => exportacion.AsegurarDescargable(Ahora)).Codigo.ShouldBe("exportacion-vencida");
    }

    [Fact]
    public void Una_exportacion_fallida_guarda_un_codigo_estable_y_no_se_descarga()
    {
        var exportacion = ExportacionDeBoveda.Solicitar(Guid.NewGuid(), null, "ip", Ahora);
        exportacion.Fallar("generacion-fallida", Ahora);
        (exportacion.Estado, exportacion.CodigoDeError).ShouldBe((EstadoDeExportacionDeBoveda.Fallida, "generacion-fallida"));
        Should.Throw<ReglaDeNegocioException>(() => exportacion.AsegurarDescargable(Ahora)).Codigo.ShouldBe("exportacion-no-disponible");
    }
}
