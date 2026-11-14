using Shouldly;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Identidad;
using Validasoft.Dominio.Invitaciones;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Aprobación antes de invitar (decisión AI, equivale al estado -5 del SRM): la invitación queda retenida como sub-estado del vínculo (sigue <c>Invitado</c>), la decide otra
/// persona —no quien la pidió ni quien tiene la identificación del tercero—, aprobar la envía y rechazar la anula; los lotes se deciden completos; el tipo de vínculo dice quién aprueba.
/// </summary>
public sealed class AprobacionDeInvitacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 14, 9, 0, 0, TimeSpan.Zero);
    private static readonly ActorDeInvitacion Ana = new(ActorTipo.Usuario, Guid.NewGuid().ToString());
    private static readonly Revisor Beto = new(Guid.NewGuid(), null);

    private static Vinculo Nuevo(string correo = "compras@proveedor.co")
    {
        var creado = Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, [], OrigenVinculo.Api, null, correo, Ahora);
        return Vinculo.Create(creado);
    }

    private static Vinculo Retenido(ActorDeInvitacion? pidio = null, string? correo = null, int horas = 48)
    {
        var vinculo = Nuevo();
        vinculo.Apply(vinculo.SolicitarAprobacionDeInvitacion(correo, TimeSpan.FromHours(horas), pidio ?? Ana, Ahora));
        return vinculo;
    }

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    // ---------- el vínculo ----------

    [Fact]
    public void Retener_la_invitacion_deja_el_vinculo_Invitado_sin_invitacion_y_conserva_a_quien_la_pidio()
    {
        var vinculo = Retenido(correo: "Otro@Proveedor.co", horas: 72);

        vinculo.Estado.ShouldBe(EstadoVinculo.Invitado); // no es un estado nuevo: es un sub-estado
        vinculo.InvitacionPorAprobar.ShouldBeTrue();
        vinculo.AprobacionDeInvitacion.ShouldBe(EstadoAprobacionDeInvitacion.Pendiente);
        vinculo.Invitacion.ShouldBeNull(); // sin enlace ni invitación vigente
        vinculo.InvitacionSolicitadaPor.ShouldBe(Ana);
        vinculo.InvitacionPendienteCorreo.ShouldBe("otro@proveedor.co");
        vinculo.InvitacionPendienteVigenciaHoras.ShouldBe(72);
        vinculo.CorreoTercero.ShouldBe("compras@proveedor.co"); // el correo del vínculo no cambia hasta que salga la invitación
    }

    [Fact]
    public void Mientras_esta_retenida_nadie_envia_ni_acepta_ni_abre_un_expediente_ni_la_retiene_otra_vez()
    {
        var vinculo = Retenido();

        Codigo(() => vinculo.EnviarInvitacion(null, TimeSpan.FromHours(72), null, Ahora)).ShouldBe("invitacion-por-aprobar");
        Codigo(() => vinculo.SolicitarAprobacionDeInvitacion(null, TimeSpan.FromHours(72), Ana, Ahora)).ShouldBe("invitacion-por-aprobar");
        Codigo(() => vinculo.AceptarInvitacion("compras@proveedor.co", Ahora)).ShouldBe("invitacion-por-aprobar");
        Codigo(() => vinculo.VincularExpediente(Guid.NewGuid(), TipoExpediente.Inicial, "N-E-1", Ahora)).ShouldBe("invitacion-por-aprobar");
    }

    [Fact]
    public void Aprobarla_la_envia_al_correo_y_con_la_vigencia_pedidos()
    {
        var vinculo = Retenido(correo: "otro@proveedor.co", horas: 48);

        var (aprobada, enviada) = vinculo.AprobarInvitacion(Beto, "CO|NIT|900123456", null, Ahora.AddHours(2));
        vinculo.Apply(aprobada);
        vinculo.Apply(enviada);

        aprobada.AprobadaPor.ShouldBe(new ActorDeInvitacion(ActorTipo.Usuario, Beto.UsuarioId.ToString()));
        aprobada.SolicitadaPor.ShouldBe(Ana);
        enviada.CorreoDestino.ShouldBe("otro@proveedor.co");
        enviada.Expira.ShouldBe(Ahora.AddHours(2).AddHours(48)); // la vigencia corre desde que sale, no desde que se pidió
        vinculo.InvitacionPorAprobar.ShouldBeFalse();
        vinculo.AprobacionDeInvitacion.ShouldBe(EstadoAprobacionDeInvitacion.Aprobada);
        vinculo.InvitacionDecididaPor.ShouldBe(aprobada.AprobadaPor);
        vinculo.Invitacion!.Correo.ShouldBe("otro@proveedor.co");
        vinculo.CorreoTercero.ShouldBe("otro@proveedor.co");
        vinculo.Estado.ShouldBe(EstadoVinculo.Invitado);
        Codigo(() => vinculo.AprobarInvitacion(Beto, "CO|NIT|1", null, Ahora)).ShouldBe("sin-invitacion-por-aprobar");
    }

    [Fact]
    public void Quien_la_pidio_no_puede_aprobarla_ni_rechazarla_y_tampoco_quien_tiene_la_identificacion_del_tercero()
    {
        var vinculo = Retenido();
        var pidio = new Revisor(Guid.Parse(Ana.Id), null);

        Codigo(() => vinculo.AprobarInvitacion(pidio, "CO|NIT|900123456", null, Ahora)).ShouldBe("separacion-de-funciones");
        Codigo(() => vinculo.RechazarInvitacion(pidio, "CO|NIT|900123456", "No la conozco", Ahora)).ShouldBe("separacion-de-funciones");

        var esElTercero = new Revisor(Guid.NewGuid(), "CO|NIT|900123456");
        Codigo(() => vinculo.AprobarInvitacion(esElTercero, "CO|NIT|900123456", null, Ahora)).ShouldBe("conflicto-de-intereses");
        vinculo.AprobarInvitacion(new Revisor(Guid.NewGuid(), "CO|CC|1"), "CO|NIT|900123456", null, Ahora).Aprobada.ShouldNotBeNull();
    }

    [Fact]
    public void Una_integracion_que_pidio_la_invitacion_no_impide_que_la_apruebe_cualquier_persona()
    {
        var vinculo = Retenido(new ActorDeInvitacion(ActorTipo.Integracion, Guid.NewGuid().ToString()));

        vinculo.AprobarInvitacion(Beto, "CO|NIT|1", null, Ahora).Aprobada.SolicitadaPor.Tipo.ShouldBe(ActorTipo.Integracion);
    }

    [Fact]
    public void Rechazarla_exige_motivo_y_deja_el_vinculo_Anulado_con_ese_motivo()
    {
        var vinculo = Retenido();

        Codigo(() => vinculo.RechazarInvitacion(Beto, "CO|NIT|1", "  ", Ahora)).ShouldBe("motivo-requerido");
        var (rechazada, anulado) = vinculo.RechazarInvitacion(Beto, "CO|NIT|1", " Proveedor no autorizado ", Ahora);
        vinculo.Apply(rechazada);
        vinculo.Apply(anulado);

        rechazada.Motivo.ShouldBe("Proveedor no autorizado");
        anulado.Motivo.ShouldBe("Proveedor no autorizado");
        vinculo.Estado.ShouldBe(EstadoVinculo.Anulado);
        vinculo.AprobacionDeInvitacion.ShouldBe(EstadoAprobacionDeInvitacion.Rechazada);
        vinculo.InvitacionRechazadaMotivo.ShouldBe("Proveedor no autorizado");
        vinculo.Invitacion.ShouldBeNull(); // nunca se le escribió al tercero
        Codigo(() => vinculo.RechazarInvitacion(Beto, "CO|NIT|1", "otra vez", Ahora)).ShouldBe("sin-invitacion-por-aprobar");
    }

    [Fact]
    public void Un_vinculo_bloqueado_no_se_decide_hasta_desbloquearlo()
    {
        var vinculo = Retenido();
        vinculo.Apply(vinculo.Bloquear("Revisión interna", null, Ahora));

        Codigo(() => vinculo.AprobarInvitacion(Beto, "CO|NIT|1", null, Ahora)).ShouldBe("estado-invalido");
        vinculo.Apply(vinculo.Desbloquear("Todo en orden", Ahora));
        vinculo.AprobarInvitacion(Beto, "CO|NIT|1", null, Ahora).Aprobada.ShouldNotBeNull();
    }

    [Fact]
    public void La_aprobacion_vale_para_el_correo_aprobado_reenviarle_no_la_pide_otra_vez_pero_invitar_a_otro_correo_si()
    {
        var vinculo = Retenido();
        var (aprobada, enviada) = vinculo.AprobarInvitacion(Beto, "CO|NIT|1", null, Ahora);
        vinculo.Apply(aprobada);
        vinculo.Apply(enviada);

        vinculo.NecesitaAprobacionParaInvitar(tipoLaExige: true, null).ShouldBeFalse();
        vinculo.NecesitaAprobacionParaInvitar(true, "Compras@Proveedor.co").ShouldBeFalse();
        vinculo.NecesitaAprobacionParaInvitar(true, "otro@proveedor.co").ShouldBeTrue();
        vinculo.NecesitaAprobacionParaInvitar(tipoLaExige: false, "otro@proveedor.co").ShouldBeFalse();
        Nuevo().NecesitaAprobacionParaInvitar(true, null).ShouldBeTrue(); // sin invitación enviada todavía

        // Reenviar al mismo correo sale como siempre.
        vinculo.EnviarInvitacion(null, TimeSpan.FromHours(24), null, Ahora.AddDays(1)).CorreoDestino.ShouldBe("compras@proveedor.co");
    }

    [Fact]
    public void Retener_aplica_las_mismas_reglas_que_invitar()
    {
        var anulado = Nuevo();
        anulado.Apply(anulado.Anular("Error de captura", Ahora));
        Codigo(() => anulado.SolicitarAprobacionDeInvitacion(null, TimeSpan.FromHours(72), Ana, Ahora)).ShouldBe("estado-invalido");
        Codigo(() => Nuevo().SolicitarAprobacionDeInvitacion(null, TimeSpan.FromDays(31), Ana, Ahora)).ShouldBe("vigencia-invalida");
        Codigo(() => Nuevo().SolicitarAprobacionDeInvitacion("sin-arroba", TimeSpan.FromHours(72), Ana, Ahora)).ShouldBe("correo-invitacion-requerido");
    }

    // ---------- el tipo de vínculo: quién aprueba ----------

    [Fact]
    public void El_tipo_que_exige_aprobacion_necesita_decir_quien_aprueba_y_lo_normaliza()
    {
        Codigo(() => TipoVinculo.Crear("PROVEEDOR", "Proveedor", null, false, requiereAprobacionParaInvitar: true)).ShouldBe("aprobadores-requeridos");
        Codigo(() => TipoVinculo.Crear("PROVEEDOR", "Proveedor", null, false, false, true, new AprobadoresDeInvitacion(TipoDeAprobadores.Rol, "Auditor")))
            .ShouldBe("aprobadores-invalidos");
        Codigo(() => TipoVinculo.Crear("PROVEEDOR", "Proveedor", null, false, false, true, new AprobadoresDeInvitacion(TipoDeAprobadores.Rol, "Inventado")))
            .ShouldBe("aprobadores-invalidos");
        Codigo(() => TipoVinculo.Crear("PROVEEDOR", "Proveedor", null, false, false, true, new AprobadoresDeInvitacion(TipoDeAprobadores.Area, "1 con espacio")))
            .ShouldBe("aprobadores-invalidos");

        var porRol = TipoVinculo.Crear("PROVEEDOR", "Proveedor", null, false, false, true, new AprobadoresDeInvitacion(TipoDeAprobadores.Rol, "oficialcumplimiento"));
        porRol.RequiereAprobacionParaInvitar.ShouldBeTrue();
        porRol.AprobadoresDeInvitacion.ShouldBe(new AprobadoresDeInvitacion(TipoDeAprobadores.Rol, "OficialCumplimiento"));

        var porArea = TipoVinculo.Crear("CLIENTE", "Cliente", null, false, false, true, new AprobadoresDeInvitacion(TipoDeAprobadores.Area, " cumplimiento "));
        porArea.AprobadoresDeInvitacion.ShouldBe(new AprobadoresDeInvitacion(TipoDeAprobadores.Area, "CUMPLIMIENTO"));

        // Sin exigirla no hace falta (y los tipos de siempre no la piden).
        var libre = TipoVinculo.Crear("USUARIO", "Usuario", null, false);
        libre.RequiereAprobacionParaInvitar.ShouldBeFalse();
        libre.AprobadoresDeInvitacion.ShouldBeNull();
        TipoVinculo.Semilla().ShouldAllBe(t => !t.RequiereAprobacionParaInvitar);
    }

    [Fact]
    public void Aprueban_las_personas_activas_del_rol_o_del_area_con_permiso_de_invitar_en_esa_empresa()
    {
        var empresa = Guid.NewGuid();
        var otra = Guid.NewGuid();
        Usuario Persona(string nombre, params RolAsignado[] roles)
        {
            var usuario = Usuario.Crear(nombre, nombre.ToLowerInvariant() + "@empresa.co", null, Ahora);
            usuario.AsignarRoles(roles);
            return usuario;
        }

        var administrador = Persona("Admin", new RolAsignado(Rol.Administrador, null));
        var oficial = Persona("Oficial", new RolAsignado(Rol.OficialCumplimiento, empresa));
        var revisor = Persona("Revisor", new RolAsignado(Rol.Revisor, empresa));
        var auditor = Persona("Auditor", new RolAsignado(Rol.Auditor, empresa)); // solo lee: no tiene vinculos:escribir
        var deOtraEmpresa = Persona("Otra", new RolAsignado(Rol.OficialCumplimiento, otra));
        var inactivo = Persona("Inactivo", new RolAsignado(Rol.OficialCumplimiento, empresa));
        inactivo.Desactivar();
        var usuarios = new[] { administrador, oficial, revisor, auditor, deOtraEmpresa, inactivo };

        var area = Area.Crear(empresa, "CUMPLIMIENTO", "Cumplimiento");
        area.DefinirMiembros([revisor.Id, auditor.Id, deOtraEmpresa.Id]);
        var areaDeOtra = Area.Crear(otra, "CUMPLIMIENTO", "Cumplimiento de otra");
        areaDeOtra.DefinirMiembros([oficial.Id]);
        var areas = new[] { area, areaDeOtra };

        var porRol = TipoVinculo.Crear("PROVEEDOR", "Proveedor", null, false, false, true, new AprobadoresDeInvitacion(TipoDeAprobadores.Rol, "OficialCumplimiento"));
        AprobadoresDeInvitaciones.Elegibles(porRol, empresa, usuarios, areas).Select(u => u.Nombre).ShouldBe(["Oficial"]);

        var porAdministrador = TipoVinculo.Crear("CLIENTE", "Cliente", null, false, false, true, new AprobadoresDeInvitacion(TipoDeAprobadores.Rol, "Administrador"));
        AprobadoresDeInvitaciones.Elegibles(porAdministrador, empresa, usuarios, areas).Select(u => u.Nombre).ShouldBe(["Admin"]); // rol de todo el tenant

        // Por área: sus miembros con permiso de invitar en la empresa (el auditor es miembro pero solo lee; «Otra» no tiene rol aquí).
        var porArea = TipoVinculo.Crear("EMPLEADO", "Empleado", null, false, false, true, new AprobadoresDeInvitacion(TipoDeAprobadores.Area, "CUMPLIMIENTO"));
        AprobadoresDeInvitaciones.Elegibles(porArea, empresa, usuarios, areas).Select(u => u.Nombre).ShouldBe(["Revisor"]);

        // Un área que no existe en la empresa, o un tipo que no designó a nadie, no tiene quién apruebe.
        var sinArea = TipoVinculo.Crear("USUARIO", "Usuario", null, false, false, true, new AprobadoresDeInvitacion(TipoDeAprobadores.Area, "COMPRAS"));
        AprobadoresDeInvitaciones.Elegibles(sinArea, empresa, usuarios, areas).ShouldBeEmpty();
        AprobadoresDeInvitaciones.Elegibles(TipoVinculo.Crear("OTRO", "Otro", null, false), empresa, usuarios, areas).ShouldBeEmpty();
    }

    // ---------- el lote ----------

    private static LoteDeInvitacion LoteNuevo(IReadOnlyList<Guid>? tiposPorAprobar, ActorDeLote? creadoPor = null)
    {
        var creado = LoteDeInvitacion.Crear(Guid.NewGuid(), "Proveedores de noviembre", null, 168, new TotalesDeLote(5, 5, 0, 0, 0, 0),
            creadoPor ?? new ActorDeLote(ActorTipo.Usuario, Ana.Id), Ahora, tiposPorAprobar);
        return LoteDeInvitacion.Create(creado);
    }

    [Fact]
    public void El_lote_con_un_tipo_que_exige_aprobacion_nace_por_aprobar_sin_ejecucion()
    {
        var tipo = Guid.NewGuid();
        var lote = LoteNuevo([tipo, tipo]);

        lote.Estado.ShouldBe(EstadoLote.PorAprobar);
        lote.Ejecucion.ShouldBeNull(); // ninguna tanda corre
        lote.TiposPorAprobar.ShouldBe([tipo]);
        lote.EsTerminal.ShouldBeFalse();
        LoteNuevo(null).Estado.ShouldBe(EstadoLote.Procesando);
        LoteNuevo([]).Estado.ShouldBe(EstadoLote.Procesando);
        Codigo(() => lote.Pausar(Ahora)).ShouldBe("estado-invalido");
        Codigo(() => lote.Reanudar(Ahora)).ShouldBe("estado-invalido");
    }

    [Fact]
    public void Aprobar_el_lote_lo_pasa_a_procesando_con_una_ejecucion_nueva_y_no_lo_decide_quien_lo_creo()
    {
        var lote = LoteNuevo([Guid.NewGuid()]);

        Codigo(() => lote.Aprobar(Guid.Parse(Ana.Id), Ahora)).ShouldBe("separacion-de-funciones");
        var aprobado = lote.Aprobar(Guid.NewGuid(), Ahora.AddMinutes(5));
        lote.Apply(aprobado);

        lote.Estado.ShouldBe(EstadoLote.Procesando);
        lote.Ejecucion.ShouldBe(aprobado.Ejecucion);
        lote.AprobadoPor.ShouldBe(aprobado.AprobadoPor);
        lote.AprobadoEn.ShouldBe(Ahora.AddMinutes(5));
        Codigo(() => lote.Aprobar(Guid.NewGuid(), Ahora)).ShouldBe("sin-lote-por-aprobar");
        Codigo(() => LoteNuevo(null).Aprobar(Guid.NewGuid(), Ahora)).ShouldBe("sin-lote-por-aprobar"); // un lote de siempre no espera nada
    }

    [Fact]
    public void Rechazar_el_lote_exige_motivo_lo_deja_cancelado_y_no_se_procesa_ninguna_fila()
    {
        var lote = LoteNuevo([Guid.NewGuid()]);
        var totales = new TotalesDeLote(5, 5, 0, 0, 0, 0);

        Codigo(() => lote.Rechazar(Guid.NewGuid(), " ", totales, Ahora)).ShouldBe("motivo-requerido");
        Codigo(() => lote.Rechazar(Guid.Parse(Ana.Id), "No procede", totales, Ahora)).ShouldBe("separacion-de-funciones");
        var rechazado = lote.Rechazar(Guid.NewGuid(), "No están autorizados", totales, Ahora);
        lote.Apply(rechazado);

        lote.Estado.ShouldBe(EstadoLote.Cancelado);
        lote.EsTerminal.ShouldBeTrue();
        lote.MotivoDeRechazo.ShouldBe("No están autorizados");
        lote.Ejecucion.ShouldBeNull();
        lote.TerminadoEn.ShouldBe(Ahora);
        Codigo(() => lote.Aprobar(Guid.NewGuid(), Ahora)).ShouldBe("sin-lote-por-aprobar");
    }

    [Fact]
    public void Quien_creo_el_lote_puede_cancelarlo_mientras_espera_pero_los_lotes_de_siempre_no_se_ven_afectados()
    {
        var lote = LoteNuevo([Guid.NewGuid()]);
        lote.Apply(lote.Cancelar(new TotalesDeLote(5, 5, 0, 0, 0, 0), Ahora));
        lote.Estado.ShouldBe(EstadoLote.Cancelado);

        // El lote de siempre: sin decisión, con las mismas reglas.
        var normal = LoteNuevo(null);
        Codigo(() => normal.Rechazar(Guid.NewGuid(), "x", normal.Totales, Ahora)).ShouldBe("sin-lote-por-aprobar");
        normal.Estado.ShouldBe(EstadoLote.Procesando);
    }
}
