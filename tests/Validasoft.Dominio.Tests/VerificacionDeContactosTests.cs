using System.Text.Json;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Verificación de los contactos del tercero (decisión AV): quien registra primero un NIT ajeno deja su correo como contacto sin verificar; solo un contacto verificado decide
/// los accesos aislados; si el tercero no tiene ninguno decide la plataforma; y si otro contacto se verifica, el verificado prevalece y el sin verificar pasa a acceso aislado.
/// </summary>
public class VerificacionDeContactosTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();
    private static readonly Guid TenantC = Guid.NewGuid();

    private static Tercero Tercero(Guid? creadoPor = null, params string[] contactos)
    {
        var tercero = Dominio.Terceros.Tercero.Crear(Identificacion.Crear("CO", "NIT", "900123456"), TipoPersona.Juridica, "Proveedores del Norte S.A.S.", null,
            contactos.Select(c => new Contacto("Contacto", c, null, "Principal")), Ahora);
        tercero.CreadoPorTenant = creadoPor;
        return tercero;
    }

    private static IReadOnlyDictionary<string, JsonElement> Campos(object datos) =>
        JsonSerializer.SerializeToElement(datos).EnumerateObject().ToDictionary(p => p.Name, p => p.Value.Clone());

    // ---------- el contacto y su estado ----------

    [Fact]
    public void Un_contacto_guardado_antes_de_la_decision_se_lee_sin_verificar()
    {
        // Lo que hay en la base no trae los campos de verificación: se lee como sin verificar (y la migración lo pone al día).
        var contacto = JsonSerializer.Deserialize<Contacto>("""{"Nombre":"Ana","Correo":"ana@norte.co","Telefono":null,"Rol":"Principal"}""")!;

        contacto.Verificacion.ShouldBe(EstadoDeVerificacion.SinVerificar);
        contacto.Medio.ShouldBeNull();
        contacto.EstaVerificado.ShouldBeFalse();
    }

    [Fact]
    public void El_contacto_verificado_conserva_su_medio_y_su_fecha_al_guardarse_y_leerse()
    {
        var tercero = Tercero(TenantA);
        tercero.AgregarContacto("Ana@Norte.co", "Ana", "Principal", MedioDeVerificacion.CorreoDelRut, Ahora).ShouldBeTrue();

        var leido = JsonSerializer.Deserialize<Contacto>(JsonSerializer.Serialize(tercero.Contactos.Single()))!;
        (leido.Verificacion, leido.Medio, leido.VerificadoEn).ShouldBe((EstadoDeVerificacion.Verificado, MedioDeVerificacion.CorreoDelRut, Ahora));
        tercero.TieneContactoVerificado.ShouldBeTrue();
    }

    [Fact]
    public void Un_contacto_sin_verificar_se_verifica_una_sola_vez_y_uno_que_no_existe_no_se_crea()
    {
        var tercero = Tercero(TenantA, "ana@norte.co");

        tercero.Verificar(" Ana@norte.co ", MedioDeVerificacion.Plataforma, Ahora).ShouldBeTrue();
        tercero.Verificar("ana@norte.co", MedioDeVerificacion.CorreoDelRut, Ahora.AddDays(1)).ShouldBeFalse();
        tercero.Verificar("otra@norte.co", MedioDeVerificacion.Plataforma, Ahora).ShouldBeFalse();
        tercero.Contactos.ShouldHaveSingleItem().Medio.ShouldBe(MedioDeVerificacion.Plataforma);
    }

    // ---------- quién decide y quién entra completo ----------

    [Fact]
    public void Solo_un_contacto_verificado_decide_los_accesos_y_sin_ninguno_decide_la_plataforma()
    {
        var tercero = Tercero(TenantA, "creador@a.co");

        AccesoDelTercero.PuedeDecidir(tercero, "creador@a.co").ShouldBeFalse();
        AccesoDelTercero.DecideLaPlataforma(tercero).ShouldBeTrue();
        AccesoDelTercero.CorreosQueDecidenLosAccesos(tercero).ShouldBeEmpty();

        tercero.AgregarContacto("gerente@norte.co", "Gerente", "Principal", MedioDeVerificacion.CorreoDelRut, Ahora);
        AccesoDelTercero.PuedeDecidir(tercero, "GERENTE@norte.co").ShouldBeTrue();
        AccesoDelTercero.PuedeDecidir(tercero, "creador@a.co").ShouldBeFalse();
        AccesoDelTercero.PuedeDecidir(tercero, "otro@x.co").ShouldBeFalse();
        AccesoDelTercero.DecideLaPlataforma(tercero).ShouldBeFalse();
        AccesoDelTercero.CorreosQueDecidenLosAccesos(tercero).ShouldBe(["gerente@norte.co"]);
    }

    [Fact]
    public void El_primer_contacto_sin_verificar_entra_completo_mientras_nadie_mas_este_verificado()
    {
        var tercero = Tercero(TenantA, "creador@a.co");

        AccesoDelTercero.Nivel(tercero, "creador@a.co").ShouldBe(NivelDeAcceso.Completo);
        AccesoDelTercero.EstaDesplazado(tercero, "creador@a.co").ShouldBeFalse();
        AccesoDelTercero.Desplazados(tercero).ShouldBeEmpty();
    }

    [Fact]
    public void Secuestro_del_NIT_A_crea_el_tercero_con_su_correo_y_cuando_se_verifica_el_correo_del_RUT_el_de_A_pasa_a_aislado()
    {
        // A registra primero el NIT de otro y deja su correo como contacto: entra completo, pero sin verificar y sin poder decidir.
        var tercero = Tercero(TenantA, "creador@a.co");
        AccesoDelTercero.PuedeDecidir(tercero, "creador@a.co").ShouldBeFalse();

        // B invita al correo del RUT y se verifica: el verificado prevalece.
        tercero.AgregarContacto("gerente@norte.co", "Gerente", "Principal", MedioDeVerificacion.CorreoDelRut, Ahora).ShouldBeTrue();
        AccesoDelTercero.Nivel(tercero, "gerente@norte.co").ShouldBe(NivelDeAcceso.Completo);
        AccesoDelTercero.Nivel(tercero, "creador@a.co").ShouldBe(NivelDeAcceso.Aislado);
        AccesoDelTercero.EstaDesplazado(tercero, "creador@a.co").ShouldBeTrue();
        AccesoDelTercero.Desplazados(tercero).Select(c => c.Correo).ShouldBe(["creador@a.co"]);
        // Y el de A ya no puede rechazar al de B.
        AccesoDelTercero.PuedeDecidir(tercero, "creador@a.co").ShouldBeFalse();
    }

    [Fact]
    public void En_un_tercero_de_pruebas_los_contactos_siempre_entran_completos_y_deciden()
    {
        var tercero = Tercero(TenantA, "a@a.co");
        tercero.AislarEnSandbox(TenantA);
        tercero.AgregarContacto("b@a.co", "B", "Principal", MedioDeVerificacion.Plataforma, Ahora);

        AccesoDelTercero.Nivel(tercero, "a@a.co").ShouldBe(NivelDeAcceso.Completo);
        AccesoDelTercero.PuedeDecidir(tercero, "a@a.co").ShouldBeTrue();
        AccesoDelTercero.DecideLaPlataforma(tercero).ShouldBeFalse();
        AccesoDelTercero.Desplazados(tercero).ShouldBeEmpty();
    }

    // ---------- con qué se verifica ----------

    [Fact]
    public void El_correo_del_RUT_y_el_de_la_Camara_son_correos_oficiales_si_el_analisis_es_legible_y_sin_alertas()
    {
        var rut = VerificacionDeContactos.DeDocumento("RUT", 2, true, [], Campos(new { correo = "  Gerente@Norte.CO " }));
        rut.ShouldBe(new CorreoOficial("gerente@norte.co", MedioDeVerificacion.CorreoDelRut, "RUT", 2));
        VerificacionDeContactos.DeDocumento("CAMARA_COMERCIO", 1, true, [], Campos(new { correo = "juridico@norte.co" }))!.Medio.ShouldBe(MedioDeVerificacion.CorreoDeLaCamara);

        // Ilegible, con alertas de manipulación, sin correo, con un correo mal formado o de otro documento: no verifica a nadie.
        VerificacionDeContactos.DeDocumento("RUT", 1, false, [], Campos(new { correo = "a@norte.co" })).ShouldBeNull();
        VerificacionDeContactos.DeDocumento("RUT", 1, true, ["Texto alterado"], Campos(new { correo = "a@norte.co" })).ShouldBeNull();
        VerificacionDeContactos.DeDocumento("RUT", 1, true, [], Campos(new { correo = (string?)null })).ShouldBeNull();
        VerificacionDeContactos.DeDocumento("RUT", 1, true, [], Campos(new { correo = "no-es-correo" })).ShouldBeNull();
        VerificacionDeContactos.DeDocumento("CEDULA", 1, true, [], Campos(new { correo = "a@norte.co" })).ShouldBeNull();
    }

    [Fact]
    public void Un_contacto_se_verifica_si_su_correo_es_el_oficial_y_el_RUT_pesa_mas_que_la_Camara()
    {
        CorreoOficial[] oficiales =
        [
            new("juridico@norte.co", MedioDeVerificacion.CorreoDeLaCamara, "CAMARA_COMERCIO", 1),
            new("gerente@norte.co", MedioDeVerificacion.CorreoDelRut, "RUT", 1),
            new("ambos@norte.co", MedioDeVerificacion.CorreoDeLaCamara, "CAMARA_COMERCIO", 1),
            new("ambos@norte.co", MedioDeVerificacion.CorreoDelRut, "RUT", 1)
        ];

        VerificacionDeContactos.PorDocumento(" Gerente@Norte.co", oficiales).ShouldBe(MedioDeVerificacion.CorreoDelRut);
        VerificacionDeContactos.PorDocumento("juridico@norte.co", oficiales).ShouldBe(MedioDeVerificacion.CorreoDeLaCamara);
        VerificacionDeContactos.PorDocumento("ambos@norte.co", oficiales).ShouldBe(MedioDeVerificacion.CorreoDelRut);
        VerificacionDeContactos.PorDocumento("creador@a.co", oficiales).ShouldBeNull();
        VerificacionDeContactos.PorDocumento(null, oficiales).ShouldBeNull();
    }

    [Fact]
    public void Dos_grupos_distintos_que_invitan_el_mismo_correo_lo_corroboran_pero_dos_vinculos_del_mismo_grupo_no()
    {
        VerificacionDeContactos.Corroborado([TenantA, TenantB]).ShouldBeTrue();
        VerificacionDeContactos.Corroborado([TenantA, TenantA, TenantA]).ShouldBeFalse();
        VerificacionDeContactos.Corroborado([TenantA]).ShouldBeFalse();
        VerificacionDeContactos.Corroborado([]).ShouldBeFalse();
    }

    [Fact]
    public void La_corroboracion_no_desplaza_a_un_contacto_ya_verificado_pero_el_correo_oficial_si_vale()
    {
        // Sin nadie verificado, dos grupos que se ponen de acuerdo verifican el correo.
        VerificacionDeContactos.Medio("b@b.co", [], [TenantA, TenantB], terceroTieneVerificado: false).ShouldBe(MedioDeVerificacion.Corroborado);
        // Con un contacto verificado manda él: la corroboración sola no basta (dos grupos no vencen el veto de quien ya es del tercero).
        VerificacionDeContactos.Medio("b@b.co", [], [TenantA, TenantB, TenantC], terceroTieneVerificado: true).ShouldBeNull();
        // El correo oficial sí verifica siempre.
        VerificacionDeContactos.Medio("b@b.co", [new CorreoOficial("b@b.co", MedioDeVerificacion.CorreoDelRut, "RUT", 1)], [TenantA], terceroTieneVerificado: true)
            .ShouldBe(MedioDeVerificacion.CorreoDelRut);
        VerificacionDeContactos.Medio("b@b.co", [], [TenantA], terceroTieneVerificado: false).ShouldBeNull();
    }

    // ---------- la solicitud: quién decide ----------

    private static SolicitudDeAcceso Solicitud(string correo = "intruso@empresa-a.co", string? origen = null) =>
        SolicitudDeAcceso.Create(SolicitudDeAcceso.Crear(Guid.NewGuid(), correo, Ahora, origen));

    [Fact]
    public void Un_operador_aprueba_con_el_medio_plataforma_y_un_contacto_con_el_medio_aprobado_por_contacto()
    {
        var porOperador = Solicitud();
        var aprobada = porOperador.Aprobar("plataforma:op-1", Ahora, ViaDeDecision.Plataforma).ShouldNotBeNull();
        (aprobada.Via, aprobada.Medio).ShouldBe((ViaDeDecision.Plataforma, MedioDeVerificacion.Plataforma));
        porOperador.Apply(aprobada);
        (porOperador.DecididoPor, porOperador.DecididoVia, porOperador.MedioAlAprobar).ShouldBe(("plataforma:op-1", ViaDeDecision.Plataforma, MedioDeVerificacion.Plataforma));

        var porContacto = Solicitud();
        porContacto.Aprobar("gerente@norte.co", Ahora).ShouldNotBeNull().Medio.ShouldBe(MedioDeVerificacion.AprobadoPorContacto);
    }

    [Fact]
    public void La_verificacion_aprueba_sola_con_el_medio_que_verifico_el_correo_y_necesita_ese_medio()
    {
        var solicitud = Solicitud();

        Should.Throw<ArgumentException>(() => solicitud.Aprobar("verificacion", Ahora, ViaDeDecision.Verificacion));
        var aprobada = solicitud.Aprobar("verificacion", Ahora, ViaDeDecision.Verificacion, MedioDeVerificacion.CorreoDelRut).ShouldNotBeNull();
        solicitud.Apply(aprobada);
        (solicitud.DecididoVia, solicitud.MedioAlAprobar).ShouldBe((ViaDeDecision.Verificacion, MedioDeVerificacion.CorreoDelRut));
        solicitud.Aprobar("verificacion", Ahora, ViaDeDecision.Verificacion, MedioDeVerificacion.CorreoDelRut).ShouldBeNull(); // idempotente
    }

    [Fact]
    public void La_regla_de_no_decidir_sobre_el_propio_acceso_vale_para_un_contacto_y_no_para_un_operador()
    {
        var solicitud = Solicitud("ana@norte.co");

        Should.Throw<ReglaDeNegocioException>(() => solicitud.Aprobar("ana@norte.co", Ahora)).Codigo.ShouldBe("acceso-propio");
        solicitud.Rechazar("plataforma:op-1", "No es del tercero", Ahora, ViaDeDecision.Plataforma).ShouldNotBeNull().Via.ShouldBe(ViaDeDecision.Plataforma);
    }

    [Fact]
    public void La_solicitud_de_un_contacto_desplazado_lo_dice()
    {
        Solicitud("creador@a.co", SolicitudDeAcceso.OrigenContactoDesplazado).Origen.ShouldBe(SolicitudDeAcceso.OrigenContactoDesplazado);
        Solicitud().Origen.ShouldBeNull();
    }

    [Fact]
    public void El_vinculo_de_un_contacto_desplazado_recuerda_el_motivo()
    {
        var creado = Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, "creador@a.co", Ahora);
        var vinculo = Vinculo.Create(creado);
        var solicitud = Guid.NewGuid();

        var solicitado = vinculo.SolicitarAccesoAislado(solicitud, "creador@a.co", Ahora, SolicitudDeAcceso.OrigenContactoDesplazado).ShouldNotBeNull();
        vinculo.Apply(solicitado);

        vinculo.AccesoAislado.ShouldBe(EstadoAccesoAislado.Pendiente);
        vinculo.AccesoAisladoMotivo.ShouldBe(SolicitudDeAcceso.OrigenContactoDesplazado);
        vinculo.SolicitarAccesoAislado(solicitud, "creador@a.co", Ahora).ShouldBeNull(); // idempotente
    }

    // ---------- el historial de los contactos ----------

    [Fact]
    public void El_historial_cuenta_quien_entro_como_contacto_como_se_verifico_y_quien_paso_a_aislado()
    {
        var terceroId = Guid.NewGuid();
        var historial = HistorialDeContactos.Create(new ContactoRegistrado(terceroId, "creador@a.co", "Empresa", "Empresa A", Ahora));
        historial.Id.ShouldBe(HistorialDeContactos.IdPara(terceroId));
        historial.Apply(new ContactoRegistrado(terceroId, "gerente@norte.co", "Aprobacion", null, Ahora.AddHours(1)));
        historial.Apply(new ContactoVerificado(terceroId, "gerente@norte.co", MedioDeVerificacion.CorreoDelRut, null, Ahora.AddHours(1)));
        historial.Apply(new ContactoPasoAAislado(terceroId, "creador@a.co", "Otro contacto se verificó.", Ahora.AddHours(2)));

        historial.Entradas.Select(e => (e.Tipo, e.Correo)).ShouldBe(
        [
            (HistorialDeContactos.Registrado, "creador@a.co"), (HistorialDeContactos.Registrado, "gerente@norte.co"),
            (HistorialDeContactos.Verificado, "gerente@norte.co"), (HistorialDeContactos.PasoAAislado, "creador@a.co")
        ]);
        historial.Entradas[0].Detalle.ShouldBe("Empresa: Empresa A");
        historial.Entradas[2].Detalle.ShouldBe("CorreoDelRut");
        HistorialDeContactos.IdPara(terceroId).ShouldNotBe(HistorialDeContactos.IdPara(Guid.NewGuid()));
    }

    [Fact]
    public void El_historial_puede_nacer_con_cualquiera_de_sus_eventos()
    {
        // Un tercero con contactos de antes del historial: su primera línea puede ser una verificación o un desplazamiento.
        var terceroId = Guid.NewGuid();
        HistorialDeContactos.Create(new ContactoVerificado(terceroId, "a@norte.co", MedioDeVerificacion.Plataforma, "plataforma:op-1", Ahora)).Entradas.ShouldHaveSingleItem()
            .Detalle.ShouldBe("Plataforma (plataforma:op-1)");
        HistorialDeContactos.Create(new ContactoPasoAAislado(terceroId, "b@norte.co", "Motivo", Ahora)).Entradas.ShouldHaveSingleItem().Tipo.ShouldBe(HistorialDeContactos.PasoAAislado);
    }

    // ---------- las versiones del contacto desplazado ----------

    private static Documento RutConVersionesDe(string correoA, string? correoB)
    {
        var terceroId = Guid.NewGuid();
        var documento = Documento.Create(Documento.CrearGlobal(terceroId, CatalogoCanonico.Requerido("RUT"), Ahora));
        foreach (var (sha, correo) in new[] { ("a", correoA), ("b", correoB) })
        {
            var archivo = new ArchivoVerificado(documento.SiguienteNumero, sha, "boveda", $"terceros/{sha}", "application/pdf", "application/pdf", 1000, "rut.pdf", 1, null,
                terceroId.ToString(), correo);
            foreach (var evento in documento.RegistrarVersionArchivo(archivo, null, Antivirus.NoDisponible, false, Ahora))
                documento.Aplicar(evento);
        }

        return documento;
    }

    [Fact]
    public void Las_versiones_globales_que_subio_el_contacto_desplazado_se_marcan_para_revision_y_se_confirman_al_aprobarlo_de_nuevo()
    {
        var rut = RutConVersionesDe("creador@a.co", "gerente@norte.co");

        var marcas = rut.MarcarParaRevision(" Creador@A.co ", "Desplazado", Ahora);
        marcas.ShouldHaveSingleItem().Numero.ShouldBe(1);
        foreach (var marca in marcas)
            rut.Aplicar(marca);
        rut.Versiones.Single(v => v.Numero == 1).EnRevisionDesde.ShouldBe(Ahora);
        rut.Versiones.Single(v => v.Numero == 2).EnRevisionDesde.ShouldBeNull();

        // Marcar dos veces no repite la marca.
        rut.MarcarParaRevision("creador@a.co", "Desplazado", Ahora).ShouldBeEmpty();

        var resueltas = rut.ResolverRevision("creador@a.co", "gerente@norte.co", Ahora.AddDays(1));
        resueltas.ShouldHaveSingleItem().Numero.ShouldBe(1);
        foreach (var resuelta in resueltas)
            rut.Aplicar(resuelta);
        rut.Versiones.Single(v => v.Numero == 1).EnRevisionDesde.ShouldBeNull();
        rut.ResolverRevision("creador@a.co", "x", Ahora).ShouldBeEmpty();
    }

    [Fact]
    public void Una_version_sin_el_correo_de_quien_la_subio_o_un_documento_por_vinculo_no_se_marca()
    {
        // Las versiones anteriores a la decisión no traen el correo: no hay a quién atribuirlas.
        RutConVersionesDe("creador@a.co", null).MarcarParaRevision("otro@a.co", "x", Ahora).ShouldBeEmpty();

        var tipo = TipoDocumentoTenant.Crear(Guid.NewGuid(), "POLIZA_A", null, "POLIZA", null, null, null, null, null, null, Ahora);
        var porVinculo = Documento.Create(Documento.CrearPorVinculo(Guid.NewGuid(), tipo, tipo.TenantId, Guid.NewGuid(), Guid.NewGuid(), Ahora));
        porVinculo.MarcarParaRevision("creador@a.co", "x", Ahora).ShouldBeEmpty();
    }
}
