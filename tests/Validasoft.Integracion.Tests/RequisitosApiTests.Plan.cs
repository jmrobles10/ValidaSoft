using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Plan y límites por grupo (OD-14…OD-17) con PostgreSQL real: medición de usuarios, empresas, vínculos, almacenamiento y llamadas a la
/// API; bloqueo suave de las altas al llegar al límite (y de las escrituras con API key si se agotan las llamadas del mes); avisos al
/// 80 % y al 100 % a los administradores, una sola vez; y la auditoría del cambio de plan.
/// </summary>
public sealed partial class RequisitosApiTests
{
    private static LimitesDePlanDto Limites(int? usuarios = null, int? empresas = null, int? vinculos = null, long? almacenamientoMb = null, long? llamadas = null) =>
        new(usuarios, empresas, vinculos, almacenamientoMb, llamadas);

    private Task<UsoDelGrupoDto> FijarPlanAsync(Guid tenantId, string codigo, LimitesDePlanDto limites, bool bloqueoSuave) =>
        ApiAsync<UsoDelGrupoDto>("PUT", $"/api/v1/plataforma/tenants/{tenantId}/plan", Plataforma(), new ActualizarPlanDelGrupoDto(codigo, limites, bloqueoSuave), 200);

    private static MedicionDePlanDto Medicion(UsoDelGrupoDto uso, string metrica) => uso.Mediciones.Single(m => m.Metrica == metrica);

    private async Task<Persona> AdministradorDelGrupoAsync(Escenario e, string nombre = "Ana Administradora")
    {
        var correo = $"{nombre.Split(' ')[0].ToLowerInvariant()}.{Guid.NewGuid():N}@constructora.co";
        var usuario = await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", new Credencial(e.Norte.Admin),
            new CrearUsuarioDto(nombre, correo, null, [new RolAsignadoDto("Administrador", null)], null), 201);
        return new Persona(usuario, "srm-" + Guid.NewGuid().ToString("N")[..8]);
    }

    [Fact]
    public async Task Sin_plan_el_grupo_no_tiene_limites_y_su_uso_se_mide_igual()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;

        var plan = await ApiAsync<PlanDelGrupoDto>("GET", "/api/v1/tenant/plan", Admin(e.Norte), null, 200);
        (plan.Configurado, plan.BloqueoSuave, plan.Limites.Usuarios).ShouldBe((false, false, null));

        var uso = await ApiAsync<UsoDelGrupoDto>("GET", "/api/v1/tenant/uso", Admin(e.Norte), null, 200);
        uso.Bloqueado.ShouldBeFalse();
        uso.Alertas.ShouldBeEmpty();
        (Medicion(uso, "Usuarios").Usado, Medicion(uso, "Usuarios").Limite, Medicion(uso, "Usuarios").Nivel).ShouldBe((2, null, "Normal")); // Laura y Carlos
        Medicion(uso, "Empresas").Usado.ShouldBe(1);
        Medicion(uso, "VinculosActivos").Usado.ShouldBe(0);

        // Sin límites no se bloquea nada: más usuarios, empresas y vínculos.
        await AdministradorDelGrupoAsync(e);
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", CorreoNuevo());
        (Medicion(await ApiAsync<UsoDelGrupoDto>("GET", "/api/v1/tenant/uso", Admin(e.Norte), null, 200), "VinculosActivos").Usado).ShouldBe(1);
        (await ApiAsync<UsoDelGrupoDto>("GET", $"/api/v1/plataforma/tenants/{tenantId}/plan", Plataforma(), null, 200)).Plan.Configurado.ShouldBeFalse();

        (await CodigoAsync(await ApiAsync("GET", "/api/v1/tenant/uso?mes=2026-13", Admin(e.Norte), null, 422))).ShouldBe("mes-invalido");
        await ApiAsync("GET", $"/api/v1/plataforma/tenants/{Guid.NewGuid()}/plan", Plataforma(), null, 404);
        await ApiAsync("PUT", $"/api/v1/plataforma/tenants/{tenantId}/plan", Admin(e.Norte), new ActualizarPlanDelGrupoDto("Plan", Limites(), false), 403); // solo Plataforma
    }

    [Fact]
    public async Task Con_bloqueo_suave_no_se_crean_mas_usuarios_empresas_ni_vinculos_al_llegar_al_limite_y_lo_existente_sigue_funcionando()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", CorreoNuevo());

        // Usuarios: 2 de 3; empresas: 1 de 1; vínculos: 1 de 2.
        var uso = await FijarPlanAsync(tenantId, "Profesional", Limites(usuarios: 3, empresas: 1, vinculos: 2), bloqueoSuave: true);
        (uso.Plan.Configurado, uso.Plan.Codigo, uso.Plan.BloqueoSuave).ShouldBe((true, "Profesional", true));
        (Medicion(uso, "Usuarios").Porcentaje, Medicion(uso, "Usuarios").Nivel).ShouldBe((66, "Normal"));
        (Medicion(uso, "Empresas").Porcentaje, Medicion(uso, "Empresas").Nivel).ShouldBe((100, "Agotado"));
        (Medicion(uso, "VinculosActivos").Porcentaje, Medicion(uso, "VinculosActivos").Nivel).ShouldBe((50, "Normal"));
        uso.Bloqueado.ShouldBeTrue(); // hay un límite agotado y el bloqueo suave está activo
        uso.Alertas.ShouldHaveSingleItem().ShouldContain("Empresas");

        // El plan del grupo queda en su ficha y en la auditoría.
        (await ApiAsync<TenantDto>("GET", $"/api/v1/plataforma/tenants/{tenantId}", Plataforma(), null, 200)).Plan.ShouldBe("Profesional");
        (await ApiAsync<PlanDelGrupoDto>("GET", "/api/v1/tenant/plan", Admin(e.Norte), null, 200)).Limites.ShouldBe(Limites(3, 1, 2));

        // Empresas: al límite, una nueva responde 409 con lo usado y el límite; no se crea.
        var bloqueada = await ApiAsync("POST", "/api/v1/empresas", Admin(e.Norte),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "900123456-8"), "Segunda S.A.S.", "SEGUNDA", null, null), 409);
        (await CodigoAsync(bloqueada)).ShouldBe("limite-del-plan-alcanzado");
        var problema = await bloqueada.ReadAsJsonAsync<System.Text.Json.JsonElement>();
        (problema.GetProperty("metrica").GetString(), problema.GetProperty("usado").GetInt64(), problema.GetProperty("limite").GetInt64()).ShouldBe(("Empresas", 1, 1));
        problema.GetProperty("detail").GetString().ShouldNotBeNull().ShouldContain("Profesional");
        (await ApiAsync<List<EmpresaDto>>("GET", "/api/v1/empresas", Admin(e.Norte), null, 200)).Count.ShouldBe(1);

        // Usuarios: el tercero cabe, el cuarto no.
        await AdministradorDelGrupoAsync(e, "Tercera Persona");
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/usuarios", Admin(e.Norte),
            new CrearUsuarioDto("Cuarta Persona", $"cuarta.{Guid.NewGuid():N}@constructora.co", null, [new RolAsignadoDto("Revisor", e.Norte.Datos.Id)], null), 409))).ShouldBe("limite-del-plan-alcanzado");

        // Vínculos: el segundo cabe, el tercero no; el que ya existe sigue funcionando.
        var segundo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", CorreoNuevo());
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/vinculos", Admin(e.Norte), new CrearVinculoDto(null,
            new CrearTerceroDto(new IdentificacionDto("CO", "NIT", Nit()), "Juridica", "Tercero de más", null, [new ContactoDto("Ana", CorreoNuevo(), null, "Comercial")]),
            "PROVEEDOR", null, CorreoNuevo(), null), 409))).ShouldBe("limite-del-plan-alcanzado");
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{segundo.Id}", Admin(e.Norte), null, 200)).Estado.ShouldBe("Invitado");

        // El lote respeta el límite fila por fila: con el cupo lleno todas fallan con su código y se reintentan al ampliar el plan.
        var filas = Enumerable.Range(1, 2).Select(_ => new FilaDeLoteEntradaDto(TipoIdentificacion: "NIT", NumeroIdentificacion: Nit(), Nombre: "Proveedor del lote",
            Correo: CorreoNuevo(), TipoVinculo: "PROVEEDOR")).ToList();
        var lote = await ApiAsync<LoteDto>("POST", "/api/v1/invitaciones/lotes", Admin(e.Norte), new CrearLoteDto("Con el cupo lleno", Filas: filas), 202);
        var terminado = await EsperarLoteAsync(e.Norte, lote.Id, l => l.Estado is "Completado" or "CompletadoConErrores");
        terminado.Totales.Fallidas.ShouldBe(2);
        (await FilasAsync(e.Norte, lote.Id, "Fallida")).SelectMany(f => f.Errores).ShouldAllBe(x => x.Codigo == "limite-del-plan-alcanzado");

        // Quitar el bloqueo suave devuelve todo (el uso se sigue midiendo y avisando).
        await FijarPlanAsync(tenantId, "Profesional", Limites(usuarios: 3, empresas: 1, vinculos: 2), bloqueoSuave: false);
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", CorreoNuevo());
        Medicion(await ApiAsync<UsoDelGrupoDto>("GET", "/api/v1/tenant/uso", Admin(e.Norte), null, 200), "VinculosActivos").Nivel.ShouldBe("Agotado");
    }

    [Fact]
    public async Task Los_avisos_de_80_y_100_por_ciento_salen_a_los_administradores_una_sola_vez_por_umbral()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;
        var admin = await AdministradorDelGrupoAsync(e); // 3 usuarios con Laura y Carlos
        await FijarPlanAsync(tenantId, "Inicial", Limites(usuarios: 4), bloqueoSuave: false);

        // 3 de 4 = 75 %: nada que avisar.
        (await ApiAsync<ResultadoDeRevisionDePlanesDto>("POST", "/api/v1/plataforma/planes/revisar", Plataforma(), new RevisarPlanesDto(tenantId), 200)).Avisos.ShouldBe(0);

        // 4 de 4 = 100 %: un aviso (el del 80 % no sale: pasó directo) a los administradores, por campana y por correo.
        await AdministradorDelGrupoAsync(e, "Otra Persona");
        var revision = await ApiAsync<ResultadoDeRevisionDePlanesDto>("POST", "/api/v1/plataforma/planes/revisar", Plataforma(), new RevisarPlanesDto(tenantId), 200);
        (revision.Grupos, revision.Avisos).ShouldBe((1, 1));
        var campana = (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, admin), null, 200)).Datos.Where(n => n.Tipo == "plan.limite").ToList();
        var aviso = campana.ShouldHaveSingleItem();
        aviso.Titulo.ShouldBe("Usuarios: límite del plan alcanzado");
        aviso.Mensaje.ShouldContain("4 de 4");
        aviso.Mensaje.ShouldContain("Inicial");
        (await fixture.Correos.EsperarAsync(admin.Usuario.Correo, m => m.Asunto.Contains("límite del plan alcanzado"))).Texto.ShouldContain("Usuarios");
        // Quien no administra (el revisor) no recibe nada.
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, e.Laura), null, 200)).Datos.ShouldNotContain(n => n.Tipo == "plan.limite");

        // Repetir la revisión no repite el aviso.
        (await ApiAsync<ResultadoDeRevisionDePlanesDto>("POST", "/api/v1/plataforma/planes/revisar", Plataforma(), new RevisarPlanesDto(tenantId), 200)).Avisos.ShouldBe(0);

        // Con el límite en 5, otro usuario deja 4 de 5 = 80 %: aviso de «80 %» y, al subir el límite, vuelve a avisar si vuelve a cruzar.
        await FijarPlanAsync(tenantId, "Inicial", Limites(usuarios: 5), bloqueoSuave: false);
        var ochenta = await ApiAsync<ResultadoDeRevisionDePlanesDto>("POST", "/api/v1/plataforma/planes/revisar", Plataforma(), new RevisarPlanesDto(tenantId), 200);
        ochenta.Avisos.ShouldBe(1);
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, admin), null, 200)).Datos
            .ShouldContain(n => n.Tipo == "plan.limite" && n.Titulo == "Usuarios: 80 % del límite del plan");
    }

    [Fact]
    public async Task Las_llamadas_a_la_api_del_mes_se_miden_y_con_el_limite_agotado_se_bloquean_las_escrituras_pero_no_las_lecturas()
    {
        var e = await PrepararAsync();
        var tenantId = e.Norte.Datos.TenantId;

        var antes = Medicion(await ApiAsync<UsoDelGrupoDto>("GET", "/api/v1/tenant/uso", Admin(e.Norte), null, 200), "LlamadasApi").Usado;
        for (var i = 0; i < 5; i++)
            await ApiAsync("GET", "/api/v1/areas", Admin(e.Norte), null, 200);
        var despues = Medicion(await ApiAsync<UsoDelGrupoDto>("GET", "/api/v1/tenant/uso", Admin(e.Norte), null, 200), "LlamadasApi").Usado;
        (despues - antes).ShouldBeGreaterThanOrEqualTo(6); // las 5 lecturas y la lectura del uso anterior

        // La Plataforma y las pantallas (sesión del tercero) no cuentan.
        var enPlataforma = Medicion(await ApiAsync<UsoDelGrupoDto>("GET", $"/api/v1/plataforma/tenants/{tenantId}/plan", Plataforma(), null, 200), "LlamadasApi").Usado;
        Medicion(await ApiAsync<UsoDelGrupoDto>("GET", $"/api/v1/plataforma/tenants/{tenantId}/plan", Plataforma(), null, 200), "LlamadasApi").Usado.ShouldBe(enPlataforma);

        // Se agota el tope del mes: escribir con la llave responde 429 y leer sigue funcionando.
        await FijarPlanAsync(tenantId, "Inicial", Limites(llamadas: enPlataforma), bloqueoSuave: true);
        var escritura = await ApiAsync("POST", "/api/v1/areas", Admin(e.Norte), new CrearAreaDto("NUEVA", "Nueva"), 429);
        (await CodigoAsync(escritura)).ShouldBe("limite-del-plan-alcanzado");
        (await escritura.ReadAsJsonAsync<System.Text.Json.JsonElement>()).GetProperty("metrica").GetString().ShouldBe("LlamadasApi");
        await ApiAsync("GET", "/api/v1/areas", Admin(e.Norte), null, 200);
        var uso = await ApiAsync<UsoDelGrupoDto>("GET", "/api/v1/tenant/uso", Admin(e.Norte), null, 200);
        (uso.Bloqueado, Medicion(uso, "LlamadasApi").Nivel).ShouldBe((true, "Agotado"));

        // El mes pasado no cuenta para el de ahora y Plataforma levanta el tope al ampliar el plan.
        Medicion(await ApiAsync<UsoDelGrupoDto>("GET", "/api/v1/tenant/uso?mes=2020-01", Admin(e.Norte), null, 200), "LlamadasApi").Usado.ShouldBe(0);
        await FijarPlanAsync(tenantId, "Ampliado", Limites(llamadas: enPlataforma + 100_000), bloqueoSuave: true);
        await ApiAsync("POST", "/api/v1/areas", Admin(e.Norte), new CrearAreaDto("NUEVA", "Nueva"), 201);
    }

    [Fact]
    public async Task El_almacenamiento_mide_los_documentos_compartidos_con_el_grupo_y_avisa_al_llegar_al_limite()
    {
        var (e, _, sesion, _) = await TerceroConAprobacionAsync();
        var tenantId = e.Norte.Datos.TenantId;
        var admin = await AdministradorDelGrupoAsync(e);

        var uso = await ApiAsync<UsoDelGrupoDto>("GET", "/api/v1/tenant/uso", Admin(e.Norte), null, 200);
        var almacenamiento = Medicion(uso, "Almacenamiento");
        almacenamiento.Usado.ShouldBeGreaterThan(0); // el RUT y la Cámara que la empresa recibió
        almacenamiento.Limite.ShouldBeNull();
        uso.AlmacenamientoMedidoEn.ShouldBeNull(); // sin plan no se guarda la medida

        // Un límite de 0 MB lo deja agotado; el aviso sale por campana y correo al administrador del grupo.
        var conPlan = await FijarPlanAsync(tenantId, "Inicial", Limites(almacenamientoMb: 0), bloqueoSuave: false);
        Medicion(conPlan, "Almacenamiento").Nivel.ShouldBe("Agotado");
        (await ApiAsync<ResultadoDeRevisionDePlanesDto>("POST", "/api/v1/plataforma/planes/revisar", Plataforma(), new RevisarPlanesDto(tenantId), 200)).Avisos.ShouldBe(1);
        var aviso = (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, admin), null, 200)).Datos.Single(n => n.Tipo == "plan.limite");
        aviso.Titulo.ShouldBe("Almacenamiento: límite del plan alcanzado");
        aviso.Enlace.ShouldBe($"/e/{e.Norte.Datos.Id}/plan-grupo");

        // Una versión nueva comparte más bytes con la empresa y el uso crece.
        await SubirAsync(sesion, "RUT", Pdf("RUT más grande " + new string('x', 4000)));
        var creció = Medicion(await ApiAsync<UsoDelGrupoDto>("GET", "/api/v1/tenant/uso", Admin(e.Norte), null, 200), "Almacenamiento");
        creció.Usado.ShouldBeGreaterThan(almacenamiento.Usado);
    }
}
