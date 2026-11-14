using System.Security.Claims;
using Validasoft.Aplicacion.Interesado;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Identificaciones;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Acceso aislado: un correo que no es contacto conocido de un tercero que ya existía entra aislado (solo lo de su relación con la empresa que lo invitó) hasta que
/// un contacto del tercero lo apruebe. Cubre la regla de quién es contacto, la solicitud de acceso, lo que el vínculo comparte, la cuarentena de lo que sube y el
/// alcance de la sesión.
/// </summary>
public class AccesoAisladoTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 9, 0, 0, TimeSpan.FromHours(-5));
    private static readonly Guid TenantA = Guid.NewGuid();
    private static readonly Guid TenantB = Guid.NewGuid();

    private static Tercero Tercero(Guid? creadoPor = null, params string[] contactos)
    {
        var tercero = Dominio.Terceros.Tercero.Crear(Identificacion.Crear("CO", "NIT", "900123456"), TipoPersona.Juridica, "Proveedores del Norte S.A.S.", null,
            contactos.Select(c => new Contacto("Contacto", c, null, "Principal")), Ahora);
        tercero.CreadoPorTenant = creadoPor;
        return tercero;
    }

    // ---------- quién es contacto y quién es el primer contacto ----------

    [Fact]
    public void Un_contacto_del_tercero_entra_completo_sin_importar_mayusculas_ni_espacios()
    {
        var tercero = Tercero(TenantB, "gerente@norte.co");

        AccesoDelTercero.Nivel(tercero, "  Gerente@Norte.CO ", []).ShouldBe(NivelDeAcceso.Completo);
        AccesoDelTercero.Nivel(tercero, "otro@norte.co", []).ShouldBe(NivelDeAcceso.Aislado);
        AccesoDelTercero.Nivel(tercero, string.Empty, []).ShouldBe(NivelDeAcceso.Aislado);
    }

    [Fact]
    public void El_correo_de_un_vinculo_ya_aceptado_se_reconoce_como_contacto_aunque_no_este_en_la_lista()
    {
        // Los terceros que aceptaron cuando el correo bastaba siguen entrando completos: se les reconoce, no se les bloquea.
        var tercero = Tercero(TenantB);

        AccesoDelTercero.Nivel(tercero, "ana@norte.co", ["Ana@Norte.co"]).ShouldBe(NivelDeAcceso.Completo);
        AccesoDelTercero.CorreosConocidos(tercero, ["Ana@Norte.co", "ana@norte.co"]).ShouldBe(["ana@norte.co"]);
    }

    [Fact]
    public void La_empresa_A_con_el_NIT_de_un_tercero_ajeno_y_su_propio_correo_no_es_contacto()
    {
        // El ataque: A crea un vínculo con el NIT del proveedor de B y pone su propio correo.
        var tercero = Tercero(TenantB, "gerente@norte.co");
        tercero.CreadoPorTenant.ShouldBe(TenantB);

        AccesoDelTercero.EsPrimerContacto(tercero, TenantA, terceroNuevo: false, hayVinculosDeOtroGrupo: true, hayDocumentos: true, hayContactoProbado: true)
            .ShouldBeFalse();
        AccesoDelTercero.Nivel(tercero, "compras@empresa-a.co", []).ShouldBe(NivelDeAcceso.Aislado);
    }

    [Fact]
    public void Un_tercero_nuevo_o_de_pruebas_toma_el_primer_correo_como_contacto()
    {
        AccesoDelTercero.EsPrimerContacto(Tercero(TenantA), TenantA, terceroNuevo: true, hayVinculosDeOtroGrupo: false, hayDocumentos: false, hayContactoProbado: false)
            .ShouldBeTrue();

        var deSandbox = Tercero(Guid.NewGuid(), "otro@a.co");
        deSandbox.AislarEnSandbox(TenantA);
        AccesoDelTercero.EsPrimerContacto(deSandbox, Guid.NewGuid(), terceroNuevo: false, hayVinculosDeOtroGrupo: true, hayDocumentos: true, hayContactoProbado: true)
            .ShouldBeTrue();
    }

    [Theory]
    [InlineData(true, false, false, false, true)] // de esta empresa, sin historia ajena, sin contactos probados
    [InlineData(true, true, false, false, false)] // otro grupo ya tiene vínculo
    [InlineData(true, false, true, false, false)] // ya hay documentos en la bóveda
    [InlineData(true, false, false, true, false)] // alguien ya aceptó una invitación: hay un contacto probado
    [InlineData(false, false, false, false, false)] // lo creó otra empresa
    public void Un_tercero_de_esta_empresa_sin_historia_toma_el_primer_correo(bool propio, bool otroGrupo, bool documentos, bool contactoProbado, bool esperado)
    {
        var tercero = Tercero(propio ? TenantA : TenantB);

        AccesoDelTercero.EsPrimerContacto(tercero, TenantA, terceroNuevo: false, otroGrupo, documentos, contactoProbado).ShouldBe(esperado);
    }

    [Fact]
    public void Un_correo_equivocado_de_la_empresa_se_corrige_invitando_a_otro_mientras_nadie_haya_aceptado()
    {
        // El tercero lo creó la empresa con un contacto que escribió mal: invitar a otro correo no queda aislado, nadie ha probado ser contacto.
        var tercero = Tercero(TenantA, "equivocado@a.co");

        AccesoDelTercero.EsPrimerContacto(tercero, TenantA, terceroNuevo: false, hayVinculosDeOtroGrupo: false, hayDocumentos: false, hayContactoProbado: false)
            .ShouldBeTrue();
    }

    [Fact]
    public void Agregar_un_contacto_es_idempotente_y_valida_el_correo()
    {
        var tercero = Tercero(TenantA);

        tercero.AgregarContacto(" Ana@Norte.co ", null, "Contacto").ShouldBeTrue();
        tercero.AgregarContacto("ana@norte.co", "Ana", "Contacto").ShouldBeFalse();
        tercero.Contactos.ShouldHaveSingleItem().Correo.ShouldBe("ana@norte.co");
        tercero.Contactos[0].Nombre.ShouldBe("ana@norte.co");
        Should.Throw<ReglaDeNegocioException>(() => tercero.AgregarContacto("sin-arroba", null, "Contacto")).Codigo.ShouldBe("correo-invalido");
    }

    [Fact]
    public void Al_aislado_solo_se_le_dice_el_nombre_del_tercero_con_las_letras_tapadas()
    {
        var enmascarado = AccesoDelTercero.EnmascararNombre("Proveedores del Norte S.A.S.");

        enmascarado.ShouldNotContain("roveedores");
        enmascarado.ShouldNotContain("orte");
        enmascarado.ShouldStartWith("P");
        enmascarado.Split(' ').Length.ShouldBe(4);
        AccesoDelTercero.EnmascararNombre(null).ShouldBe(string.Empty);
        AccesoDelTercero.EnmascararNombre("X").ShouldBe("X");
    }

    // ---------- la solicitud de acceso ----------

    [Fact]
    public void La_solicitud_de_un_correo_tiene_un_id_determinista_que_no_distingue_mayusculas()
    {
        var tercero = Guid.NewGuid();

        SolicitudDeAcceso.IdPara(tercero, "Ana@Norte.co ").ShouldBe(SolicitudDeAcceso.IdPara(tercero, "ana@norte.co"));
        SolicitudDeAcceso.IdPara(tercero, "ana@norte.co").ShouldNotBe(SolicitudDeAcceso.IdPara(Guid.NewGuid(), "ana@norte.co"));
        SolicitudDeAcceso.IdPara(tercero, "ana@norte.co").ShouldNotBe(SolicitudDeAcceso.IdPara(tercero, "luis@norte.co"));
    }

    private static SolicitudDeAcceso Solicitud(Guid? terceroId = null, string correo = "intruso@empresa-a.co")
    {
        var creada = SolicitudDeAcceso.Crear(terceroId ?? Guid.NewGuid(), correo, Ahora);
        return SolicitudDeAcceso.Create(creada);
    }

    [Fact]
    public void Nace_pendiente_con_el_correo_normalizado_y_solo_una_invitacion_por_vinculo()
    {
        var solicitud = Solicitud(correo: " Intruso@Empresa-A.co ");
        solicitud.Estado.ShouldBe(EstadoSolicitudDeAcceso.Pendiente);
        solicitud.Correo.ShouldBe("intruso@empresa-a.co");
        Should.Throw<ReglaDeNegocioException>(() => SolicitudDeAcceso.Crear(Guid.NewGuid(), "sin-arroba", Ahora)).Codigo.ShouldBe("correo-invalido");

        var vinculo = Guid.NewGuid();
        var pedida = solicitud.SolicitarPor(TenantA, Guid.NewGuid(), vinculo, "Empresa A", Ahora).ShouldNotBeNull();
        solicitud.Apply(pedida);
        solicitud.Invitaciones.ShouldHaveSingleItem().Empresa.ShouldBe("Empresa A");

        // Reenviar la invitación (mismo vínculo) no repite el aviso a los contactos.
        solicitud.SolicitarPor(TenantA, Guid.NewGuid(), vinculo, "Empresa A", Ahora).ShouldBeNull();
        solicitud.SolicitarPor(TenantA, Guid.NewGuid(), Guid.NewGuid(), "  ", Ahora).ShouldNotBeNull().Empresa.ShouldBe("Una empresa");
    }

    [Fact]
    public void Un_contacto_aprueba_pero_nadie_aprueba_su_propio_acceso()
    {
        var solicitud = Solicitud();

        Should.Throw<ReglaDeNegocioException>(() => solicitud.Aprobar("Intruso@empresa-a.co", Ahora)).Codigo.ShouldBe("acceso-propio");
        Should.Throw<ReglaDeNegocioException>(() => solicitud.Rechazar("intruso@empresa-a.co", null, Ahora)).Codigo.ShouldBe("acceso-propio");

        var aprobada = solicitud.Aprobar("gerente@norte.co", Ahora).ShouldNotBeNull();
        solicitud.Apply(aprobada);
        solicitud.Estado.ShouldBe(EstadoSolicitudDeAcceso.Aprobada);
        solicitud.DecididoPor.ShouldBe("gerente@norte.co");
        solicitud.DecididoEn.ShouldBe(Ahora);

        // Repetir no cambia nada; una aprobada ya es contacto y no se rechaza.
        solicitud.Aprobar("gerente@norte.co", Ahora).ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => solicitud.Rechazar("gerente@norte.co", null, Ahora)).Codigo.ShouldBe("solicitud-ya-aprobada");
    }

    [Fact]
    public void Un_rechazo_bloquea_el_correo_y_un_contacto_puede_corregirlo()
    {
        var solicitud = Solicitud();

        var rechazada = solicitud.Rechazar("gerente@norte.co", "  No lo conozco  ", Ahora).ShouldNotBeNull();
        solicitud.Apply(rechazada);
        solicitud.Estado.ShouldBe(EstadoSolicitudDeAcceso.Rechazada);
        solicitud.EstaRechazada.ShouldBeTrue();
        solicitud.MotivoDelRechazo.ShouldBe("No lo conozco");
        solicitud.Rechazar("gerente@norte.co", null, Ahora).ShouldBeNull(); // idempotente
        Should.Throw<ReglaDeNegocioException>(() => Solicitud().Rechazar("a@norte.co", new string('x', 501), Ahora)).Codigo.ShouldBe("motivo-demasiado-largo");

        // Un contacto puede corregir su decisión: aprobar lo rechazado.
        solicitud.Apply(solicitud.Aprobar("otro@norte.co", Ahora.AddHours(1)).ShouldNotBeNull());
        solicitud.Estado.ShouldBe(EstadoSolicitudDeAcceso.Aprobada);
        solicitud.MotivoDelRechazo.ShouldBeNull();
    }

    // ---------- el vínculo ----------

    private static Vinculo Vinculo(string correo = "intruso@empresa-a.co")
    {
        var creado = Dominio.Vinculos.Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, correo, Ahora);
        return Dominio.Vinculos.Vinculo.Create(creado);
    }

    private static void Invitar(Vinculo vinculo, string? correo = null) =>
        vinculo.Apply(vinculo.EnviarInvitacion(correo, TimeSpan.FromHours(72), null, Ahora));

    [Fact]
    public void Un_vinculo_con_acceso_aislado_pendiente_o_rechazado_no_recibe_la_boveda_global()
    {
        var vinculo = Vinculo();
        vinculo.AccesoAislado.ShouldBeNull();
        vinculo.BovedaDisponible.ShouldBeTrue();

        var solicitud = SolicitudDeAcceso.IdPara(vinculo.TerceroId, vinculo.CorreoTercero);
        var solicitado = vinculo.SolicitarAccesoAislado(solicitud, vinculo.CorreoTercero, Ahora).ShouldNotBeNull();
        vinculo.Apply(solicitado);
        vinculo.AccesoAislado.ShouldBe(EstadoAccesoAislado.Pendiente);
        vinculo.BovedaDisponible.ShouldBeFalse();
        vinculo.SolicitudDeAccesoId.ShouldBe(solicitud);
        vinculo.AccesoAisladoDesde.ShouldBe(Ahora);

        // Reenviar al mismo correo no repite el evento.
        vinculo.SolicitarAccesoAislado(solicitud, vinculo.CorreoTercero, Ahora).ShouldBeNull();

        vinculo.Apply(vinculo.RechazarAccesoAislado(solicitud, Ahora).ShouldNotBeNull());
        vinculo.AccesoAislado.ShouldBe(EstadoAccesoAislado.Rechazado);
        vinculo.AccesoRechazado.ShouldBeTrue();
        vinculo.BovedaDisponible.ShouldBeFalse();
        vinculo.RechazarAccesoAislado(solicitud, Ahora).ShouldBeNull();

        vinculo.Apply(vinculo.AprobarAccesoAislado(solicitud, Ahora.AddHours(1)).ShouldNotBeNull());
        vinculo.AccesoAislado.ShouldBe(EstadoAccesoAislado.Aprobado);
        vinculo.BovedaDisponible.ShouldBeTrue();
        vinculo.AccesoAisladoResueltoEn.ShouldBe(Ahora.AddHours(1));
    }

    [Fact]
    public void La_decision_de_otra_solicitud_o_de_un_vinculo_sin_acceso_aislado_no_le_afecta()
    {
        var vinculo = Vinculo();
        var otra = Guid.NewGuid();

        vinculo.AprobarAccesoAislado(otra, Ahora).ShouldBeNull();
        vinculo.RechazarAccesoAislado(otra, Ahora).ShouldBeNull();

        var propia = SolicitudDeAcceso.IdPara(vinculo.TerceroId, vinculo.CorreoTercero);
        vinculo.Apply(vinculo.SolicitarAccesoAislado(propia, vinculo.CorreoTercero, Ahora)!);
        vinculo.AprobarAccesoAislado(otra, Ahora).ShouldBeNull();
        Should.Throw<ReglaDeNegocioException>(() => vinculo.SolicitarAccesoAislado(otra, " ", Ahora)).Codigo.ShouldBe("correo-invitacion-requerido");
    }

    [Fact]
    public void Invitar_a_otro_correo_empieza_de_cero_y_el_aprobado_ya_no_vale_para_el_nuevo()
    {
        var vinculo = Vinculo();
        Invitar(vinculo);
        var primera = SolicitudDeAcceso.IdPara(vinculo.TerceroId, "intruso@empresa-a.co");
        vinculo.Apply(vinculo.SolicitarAccesoAislado(primera, "intruso@empresa-a.co", Ahora)!);
        vinculo.Apply(vinculo.AprobarAccesoAislado(primera, Ahora)!);
        vinculo.AccesoAislado.ShouldBe(EstadoAccesoAislado.Aprobado);

        Invitar(vinculo, "OTRO@empresa-a.co");

        vinculo.CorreoTercero.ShouldBe("otro@empresa-a.co");
        vinculo.AccesoAislado.ShouldBeNull();
        vinculo.SolicitudDeAccesoId.ShouldBeNull();
        var segunda = SolicitudDeAcceso.IdPara(vinculo.TerceroId, "otro@empresa-a.co");
        vinculo.SolicitarAccesoAislado(segunda, "otro@empresa-a.co", Ahora).ShouldNotBeNull();
    }

    [Fact]
    public void Un_correo_rechazado_se_reemplaza_invitando_a_otro_aunque_ya_hubiera_aceptado_y_la_persona_nueva_acepta_ella_misma()
    {
        var vinculo = Vinculo();
        Invitar(vinculo);
        vinculo.Apply(vinculo.AceptarInvitacion("intruso@empresa-a.co", Ahora));
        var solicitud = SolicitudDeAcceso.IdPara(vinculo.TerceroId, vinculo.CorreoTercero);
        vinculo.Apply(vinculo.SolicitarAccesoAislado(solicitud, vinculo.CorreoTercero, Ahora)!);

        // Pendiente y aceptado: no se reinvita (la empresa espera la decisión).
        Should.Throw<ReglaDeNegocioException>(() => vinculo.EnviarInvitacion("otro@empresa-a.co", TimeSpan.FromHours(72), null, Ahora))
            .Codigo.ShouldBe("invitacion-ya-aceptada");

        vinculo.Apply(vinculo.RechazarAccesoAislado(solicitud, Ahora)!);
        // Rechazado, al mismo correo: nada que reemplazar.
        Should.Throw<ReglaDeNegocioException>(() => vinculo.EnviarInvitacion(null, TimeSpan.FromHours(72), null, Ahora)).Codigo.ShouldBe("invitacion-ya-aceptada");

        Invitar(vinculo, "otro@empresa-a.co");
        vinculo.InvitacionAceptada.ShouldBeFalse();
        vinculo.InvitacionAceptadaEn.ShouldBeNull();
        vinculo.AccesoAislado.ShouldBeNull();
        vinculo.CorreoTercero.ShouldBe("otro@empresa-a.co");
    }

    [Fact]
    public void Un_vinculo_bloqueado_o_terminado_no_se_reinvita_ni_con_el_acceso_rechazado()
    {
        var vinculo = Vinculo();
        Invitar(vinculo);
        var solicitud = SolicitudDeAcceso.IdPara(vinculo.TerceroId, vinculo.CorreoTercero);
        vinculo.Apply(vinculo.SolicitarAccesoAislado(solicitud, vinculo.CorreoTercero, Ahora)!);
        vinculo.Apply(vinculo.RechazarAccesoAislado(solicitud, Ahora)!);
        vinculo.Apply(vinculo.Anular("Se anula", Ahora));

        Should.Throw<ReglaDeNegocioException>(() => vinculo.EnviarInvitacion("otro@empresa-a.co", TimeSpan.FromHours(72), null, Ahora))
            .Codigo.ShouldBe("estado-invalido");
    }

    // ---------- lo que se comparte y la cuarentena ----------

    private static VinculoCandidato Candidato(Guid terceroId, Guid vinculoId, bool bovedaDisponible, bool exige = true) =>
        new(vinculoId, TenantA, Guid.NewGuid(), terceroId, true, EstadoVinculo.EnValidacion, exige, bovedaDisponible);

    private static Documento Rut(Guid terceroId) => Documento.Create(Documento.CrearGlobal(terceroId, CatalogoCanonico.Requerido("RUT"), Ahora));

    private static void Cargar(Documento d, string sha, Guid? vinculo = null)
    {
        var archivo = new ArchivoVerificado(d.SiguienteNumero, sha, "b", $"k/{sha}", "application/pdf", "application/pdf", 10, "rut.pdf", 1, vinculo, "tercero");
        foreach (var e in d.RegistrarVersionArchivo(archivo, null, Antivirus.NoDisponible, false, Ahora))
            d.Aplicar(e);
    }

    [Fact]
    public void Un_documento_global_no_se_comparte_con_un_vinculo_de_acceso_aislado_pero_si_con_uno_aprobado()
    {
        var terceroId = Guid.NewGuid();
        var rut = Rut(terceroId);
        Cargar(rut, "a1");
        var aislado = new VinculoCandidato(Guid.NewGuid(), TenantA, Guid.NewGuid(), terceroId, true, EstadoVinculo.EnValidacion, true, BovedaDisponible: false);
        var aprobado = aislado with { VinculoId = Guid.NewGuid(), BovedaDisponible = true };

        rut.Compartir(aislado, OrigenComparticion.Automatica, Ahora).ShouldBeNull();
        rut.CompartirActual([aislado], OrigenComparticion.Automatica, Ahora).ShouldBeEmpty();
        rut.Compartir(aprobado, OrigenComparticion.Automatica, Ahora).ShouldNotBeNull().VinculoId.ShouldBe(aprobado.VinculoId);
    }

    private static TipoDocumentoTenant TipoGlobalRut(Guid tenantId) =>
        TipoDocumentoTenant.Crear(tenantId, "RUT_A", null, "RUT", null, null, null, null, null, null, Ahora);

    [Fact]
    public void Lo_que_sube_un_aislado_para_un_documento_global_queda_en_cuarentena_ligado_solo_a_su_vinculo()
    {
        var terceroId = Guid.NewGuid();
        var tipo = TipoGlobalRut(TenantA);
        var vinculo = Guid.NewGuid();
        var empresa = Guid.NewGuid();

        var creado = Documento.CrearEnCuarentena(terceroId, tipo, TenantA, empresa, vinculo, Ahora);
        var cuarentena = Documento.Create(creado);

        creado.Ambito.ShouldBe(AmbitoDocumento.PorVinculo);
        cuarentena.EnCuarentena.ShouldBeTrue();
        cuarentena.TipoCanonico.ShouldBe("RUT");
        cuarentena.VinculoId.ShouldBe(vinculo);
        cuarentena.ClaveNatural.ShouldBe(Documento.ClavePorVinculo(vinculo, tipo.Id));
        cuarentena.ClaveNatural.ShouldNotBe(Documento.ClaveGlobal(terceroId, "RUT"));
        Cargar(cuarentena, "q1", vinculo);
        cuarentena.VersionActual.ShouldBe(1);

        // Se comparte solo con su vínculo (aunque otro también pida el RUT), tenga o no la bóveda disponible; nunca con otras empresas.
        cuarentena.Compartir(Candidato(terceroId, Guid.NewGuid(), true), OrigenComparticion.Automatica, Ahora).ShouldBeNull();
        cuarentena.Compartir(Candidato(terceroId, vinculo, false, exige: false), OrigenComparticion.Automatica, Ahora).ShouldNotBeNull().VinculoId.ShouldBe(vinculo);
    }

    [Fact]
    public void La_cuarentena_solo_admite_tipos_globales_de_la_empresa_y_se_resuelve_una_sola_vez()
    {
        var poliza = TipoDocumentoTenant.Crear(TenantA, "POLIZA_A", null, "POLIZA", null, null, null, null, null, null, Ahora);
        Should.Throw<InvalidOperationException>(() => Documento.CrearEnCuarentena(Guid.NewGuid(), poliza, TenantA, Guid.NewGuid(), Guid.NewGuid(), Ahora));
        Should.Throw<InvalidOperationException>(() =>
            Documento.CrearEnCuarentena(Guid.NewGuid(), TipoGlobalRut(TenantA), TenantB, Guid.NewGuid(), Guid.NewGuid(), Ahora));

        var terceroId = Guid.NewGuid();
        var cuarentena = Documento.Create(Documento.CrearEnCuarentena(terceroId, TipoGlobalRut(TenantA), TenantA, Guid.NewGuid(), Guid.NewGuid(), Ahora));
        var global = Guid.NewGuid();
        var resuelta = cuarentena.ResolverCuarentena(true, global, 3, "Se aprobó", Ahora).ShouldNotBeNull();
        cuarentena.Aplicar(resuelta);
        cuarentena.CuarentenaResueltaEn.ShouldBe(Ahora);
        cuarentena.PromovidoAGlobalId.ShouldBe(global);
        cuarentena.ResolverCuarentena(false, null, null, "Otra vez", Ahora).ShouldBeNull();

        Should.Throw<InvalidOperationException>(() => Rut(terceroId).ResolverCuarentena(true, null, null, "No aplica", Ahora));
    }

    // ---------- alcance de la sesión ----------

    private static ClaimsPrincipal Principal(bool aislada = false, string? componente = null, Guid? vinculo = null)
    {
        var reclamos = new List<Claim>
        {
            new(ReclamosTercero.Tipo, ReclamosTercero.ValorTipo),
            new(ReclamosTercero.TerceroId, Guid.NewGuid().ToString()),
            new(ReclamosTercero.Correo, "a@a.co")
        };
        if (vinculo is { } v)
            reclamos.Add(new Claim(ReclamosTercero.Vinculos, new VinculoEnSesion(v, TenantA, Guid.NewGuid()).Reclamo()));
        if (aislada)
            reclamos.Add(new Claim(ReclamosTercero.Aislado, "true"));
        if (componente is not null)
        {
            reclamos.Add(new Claim(ReclamosTercero.Embebido, "true"));
            reclamos.Add(new Claim(ReclamosTercero.Componente, componente));
        }

        return new ClaimsPrincipal(new ClaimsIdentity(reclamos, "prueba"));
    }

    [Theory]
    [InlineData(GrupoDeAcceso.Relacion, true)]
    [InlineData(GrupoDeAcceso.Subida, true)]
    [InlineData(GrupoDeAcceso.Descarga, true)]
    [InlineData(GrupoDeAcceso.Boveda, false)]
    [InlineData(GrupoDeAcceso.Cuentas, false)]
    [InlineData(GrupoDeAcceso.CuentaDesignada, false)]
    [InlineData(GrupoDeAcceso.Ficha, false)]
    [InlineData(GrupoDeAcceso.Privacidad, false)]
    [InlineData(GrupoDeAcceso.Cuenta, false)]
    public void Una_sesion_aislada_solo_alcanza_lo_de_su_relacion(GrupoDeAcceso grupo, bool permitido)
    {
        var principal = Principal(aislada: true);

        if (permitido)
        {
            var sesion = SesionDeTercero.De(principal, grupo);
            sesion.Aislada.ShouldBeTrue();
        }
        else
        {
            Should.Throw<ReglaDeNegocioException>(() => SesionDeTercero.De(principal, grupo)).Codigo.ShouldBe("acceso-aislado");
        }
    }

    [Fact]
    public void Una_sesion_completa_alcanza_todos_los_grupos()
    {
        var principal = Principal();

        foreach (var grupo in Enum.GetValues<GrupoDeAcceso>())
            SesionDeTercero.De(principal, grupo).Aislada.ShouldBeFalse();
    }

    [Theory]
    [InlineData(ComponentesEmbebibles.Expediente, GrupoDeAcceso.Boveda, false)]
    [InlineData(ComponentesEmbebibles.Expediente, GrupoDeAcceso.Privacidad, false)]
    [InlineData(ComponentesEmbebibles.Expediente, GrupoDeAcceso.Cuenta, false)]
    [InlineData(ComponentesEmbebibles.Expediente, GrupoDeAcceso.Subida, true)]
    [InlineData(ComponentesEmbebibles.Expediente, GrupoDeAcceso.Relacion, true)]
    [InlineData(ComponentesEmbebibles.Expediente, GrupoDeAcceso.Ficha, true)]
    [InlineData(ComponentesEmbebibles.Boveda, GrupoDeAcceso.Boveda, true)]
    [InlineData(ComponentesEmbebibles.Boveda, GrupoDeAcceso.Ficha, false)]
    [InlineData(ComponentesEmbebibles.Boveda, GrupoDeAcceso.Privacidad, false)]
    [InlineData(ComponentesEmbebibles.Ficha, GrupoDeAcceso.Ficha, true)]
    [InlineData(ComponentesEmbebibles.Ficha, GrupoDeAcceso.Boveda, false)]
    [InlineData(ComponentesEmbebibles.Ficha, GrupoDeAcceso.Cuentas, false)]
    [InlineData("otro", GrupoDeAcceso.Relacion, false)]
    public void Un_token_embebido_solo_alcanza_lo_de_su_componente(string componente, GrupoDeAcceso grupo, bool permitido)
    {
        var principal = Principal(componente: componente);

        if (permitido)
            SesionDeTercero.De(principal, grupo).Embebida!.Componente.ShouldBe(componente);
        else
            Should.Throw<ReglaDeNegocioException>(() => SesionDeTercero.De(principal, grupo)).Codigo.ShouldBe("alcance-de-sesion");
    }

    [Fact]
    public void Un_token_embebido_de_un_expediente_solo_trabaja_con_el_vinculo_de_su_emision()
    {
        var vinculo = Guid.NewGuid();
        var expediente = SesionDeTercero.SinAlcance(Principal(componente: ComponentesEmbebibles.Expediente, vinculo: vinculo));
        expediente.EsDeUnExpediente.ShouldBeTrue();

        expediente.ExigirVinculoDelToken(vinculo);
        Should.Throw<ReglaDeNegocioException>(() => expediente.ExigirVinculoDelToken(Guid.NewGuid())).Codigo.ShouldBe("vinculo-no-encontrado");
        Should.Throw<ReglaDeNegocioException>(() => expediente.ExigirVinculoDelToken(null)).Codigo.ShouldBe("vinculo-no-encontrado");

        // El de la bóveda y la sesión normal no son de un vínculo: no se limitan por aquí.
        SesionDeTercero.SinAlcance(Principal(componente: ComponentesEmbebibles.Boveda, vinculo: vinculo)).ExigirVinculoDelToken(Guid.NewGuid());
        SesionDeTercero.SinAlcance(Principal(vinculo: vinculo)).ExigirVinculoDelToken(null);
    }

    [Fact]
    public void El_reclamo_de_sesion_y_el_de_aislada_llegan_a_la_sesion()
    {
        var id = Guid.NewGuid();
        var principal = Principal(aislada: true);
        ((ClaimsIdentity)principal.Identity!).AddClaim(new Claim(ReclamosTercero.SesionId, id.ToString("N")));

        var sesion = SesionDeTercero.SinAlcance(principal);

        sesion.SesionId.ShouldBe(id);
        sesion.Aislada.ShouldBeTrue();
        SesionDeTercero.SinAlcance(Principal()).SesionId.ShouldBeNull();
    }

    // ---------- sesiones de refresco ----------

    [Fact]
    public void Una_sesion_cerrada_individualmente_o_por_rechazo_deja_de_estar_vigente_y_no_renueva()
    {
        var (sesion, token) = SesionDeRefresco.Abrir(Guid.NewGuid(), "a@a.co", false, Ahora, "  Mozilla/5.0  ", "10.0.0.7");
        sesion.Dispositivo.ShouldBe("Mozilla/5.0");
        sesion.Ip.ShouldBe("10.0.0.7");
        sesion.UltimoUsoEn.ShouldBe(Ahora);
        sesion.VigenteEn(Ahora.AddHours(1)).ShouldBeTrue();
        sesion.VigenteEn(Ahora.AddHours(9)).ShouldBeFalse();

        sesion.Renovar(token, Ahora.AddMinutes(20)).Renovada.ShouldBeTrue();
        sesion.UltimoUsoEn.ShouldBe(Ahora.AddMinutes(20));

        sesion.Revocar(SesionDeRefresco.MotivoCierreIndividual, Ahora.AddMinutes(30));
        sesion.VigenteEn(Ahora.AddMinutes(31)).ShouldBeFalse();
        sesion.MotivoRevocacion.ShouldBe(SesionDeRefresco.MotivoCierreIndividual);
        Should.Throw<ReglaDeNegocioException>(() => sesion.Renovar("x.y", Ahora.AddMinutes(40))).Codigo.ShouldBe("sesion-revocada");

        // La primera revocación manda.
        sesion.Revocar(SesionDeRefresco.MotivoAccesoRechazado, Ahora.AddHours(2));
        sesion.MotivoRevocacion.ShouldBe(SesionDeRefresco.MotivoCierreIndividual);
    }

    [Fact]
    public void El_dispositivo_de_una_sesion_se_recorta_y_si_no_hay_se_queda_vacio()
    {
        SesionDeRefresco.Abrir(Guid.NewGuid(), "a@a.co", false, Ahora, new string('x', 500), null).Sesion.Dispositivo!.Length.ShouldBe(200);
        var sin = SesionDeRefresco.Abrir(Guid.NewGuid(), "a@a.co", false, Ahora).Sesion;
        sin.Dispositivo.ShouldBeNull();
        sin.Ip.ShouldBeNull();
    }
}
