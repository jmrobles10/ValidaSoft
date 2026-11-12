using System.Text.Json;
using Alba;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Verificaciones externas (H7, VE-5…VE-16) con el proveedor simulado: el expediente no se aprueba con una verificación
/// bloqueante pendiente aunque todas las áreas aprueben; coincidencia (también de una persona relacionada de la ficha)
/// descartada con justificación; estudio SAGRILAFT con vigencia que vence; evidencia auditada; eventos públicos; monitoreo;
/// configuración de listas por empresa con la clave cifrada; informe SAGRILAFT en JSON y Excel.
/// </summary>
public sealed partial class ExpedientesApiTests
{
    private static readonly VerificacionRequeridaDto ListasRequeridas = new("ListasRestrictivas");
    private static readonly VerificacionRequeridaDto EstudioRequerido = new("EstudioDeCumplimiento");

    [Fact]
    public async Task Sin_coincidencias_el_expediente_espera_el_estudio_del_oficial_aunque_las_areas_aprueben()
    {
        var e = await PrepararAsync();
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", ListasRequeridas, EstudioRequerido);
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Aceros del Norte S.A.S.");
        var abierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        abierto.Verificaciones!.Select(v => (v.Tipo, v.Bloqueante)).ShouldBe([("ListasRestrictivas", true), ("EstudioDeCumplimiento", true)]);

        // La consulta automática de listas corre sola al pasar a revisión.
        var consultado = await EsperarExpedienteAsync(e, abierto.Id, x => x.Verificaciones!.Any(v => v.Tipo == "ListasRestrictivas" && v.Estado == "Satisfecha"));
        var listas = consultado.Verificaciones!.Single(v => v.Tipo == "ListasRestrictivas");
        listas.Proveedor.ShouldBe("Simulado");
        listas.Resultado.ShouldBe("SinCoincidencias");
        listas.Evidencia.ShouldNotBeNull().TipoContenido.ShouldBe("application/pdf");
        listas.Personas.ShouldHaveSingleItem().Rol.ShouldBe("Tercero");

        // Todas las áreas aprueban, pero falta el estudio: no se aprueba y queda en la bandeja del oficial.
        var esperando = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura),
            new VeredictoDto("Aprobado"), 200);
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos),
            new VeredictoDto("Aprobado"), 200);
        esperando = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{abierto.Id}", Admin(e), null, 200);
        esperando.Estado.ShouldBe("EnRevision");
        esperando.EsperandoVerificaciones.ShouldBeTrue();
        var fila = (await ApiAsync<List<FilaCumplimientoDto>>("GET", "/api/v1/cumplimiento/bandeja", Delegado(e, e.Carlos), null, 200))
            .ShouldHaveSingleItem();
        fila.ExpedienteId.ShouldBe(abierto.Id);
        fila.EsperandoSoloVerificaciones.ShouldBeTrue();
        fila.Pendientes.ShouldHaveSingleItem().Tipo.ShouldBe("EstudioDeCumplimiento");

        // Un revisor sin rol de cumplimiento no registra el estudio; el oficial sí, con las reglas del legado.
        await ApiAsync("POST", $"/api/v1/expedientes/{abierto.Id}/verificaciones/estudio-cumplimiento", Delegado(e, e.Laura),
            new RegistrarEstudioCumplimientoDto(1, false, "Simple", "Favorable"), 403);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/expedientes/{abierto.Id}/verificaciones/estudio-cumplimiento", Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(3, false, "Simple", "Favorable"), 422))).ShouldBe("debida-diligencia-ampliada-requerida");
        var aprobado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/verificaciones/estudio-cumplimiento", Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(3, false, "Ampliada", "Favorable", "Sector de alto riesgo; sin hallazgos."), 200);
        aprobado.Estado.ShouldBe("Aprobado");
        var estudio = aprobado.Verificaciones!.Single(v => v.Tipo == "EstudioDeCumplimiento").Estudio.ShouldNotBeNull();
        (estudio.NivelRiesgo, estudio.DebidaDiligencia, estudio.VerificadoEnListas).ShouldBe((3, "Ampliada", true));
        estudio.VigenteHasta.ShouldBe(estudio.Fecha.AddDays(30), TimeSpan.FromSeconds(1));
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).Estado.ShouldBe("Activo");
        (await ApiAsync<List<FilaCumplimientoDto>>("GET", "/api/v1/cumplimiento/bandeja", Delegado(e, e.Carlos), null, 200)).ShouldBeEmpty();

        // El vínculo activo queda en monitoreo periódico de listas.
        var monitoreo = await ApiAsync<MonitoreoListasDto>("GET", $"/api/v1/vinculos/{vinculo.Id}/monitoreo-listas", Admin(e), null, 200);
        monitoreo.FrecuenciaDias.ShouldBe(30);
        monitoreo.Listas.Estado.ShouldBe("Satisfecha");

        // La evidencia se descarga con URL prefirmada y la descarga queda en el historial.
        var descarga = await ApiAsync<DescargaEvidenciaDto>("POST", $"/api/v1/expedientes/{abierto.Id}/verificaciones/ListasRestrictivas/evidencia/descarga",
            Admin(e), null, 200);
        descarga.NombreArchivo.ShouldEndWith(".pdf");
        using (var http = new HttpClient())
            (await http.GetByteArrayAsync(descarga.Url)).Take(5).ShouldBe("%PDF-"u8.ToArray());
        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{abierto.Id}/historial", Admin(e), null, 200);
        historial.Select(h => h.Tipo).ShouldContain("verificacion_externa_registrada");
        historial.Select(h => h.Tipo).ShouldContain("estudio_de_cumplimiento_registrado");
        historial.Select(h => h.Tipo).ShouldContain("evidencia_de_verificacion_descargada");

        // Eventos públicos: verificacion_registrada por las listas y por el estudio.
        var eventos = await EsperarEventosAsync(e, "expediente.verificacion_registrada", 2);
        eventos.Select(x => x.GetProperty("datos").GetProperty("tipo").GetString()).ShouldBe(["ListasRestrictivas", "EstudioDeCumplimiento"], ignoreOrder: true);
        eventos.ShouldAllBe(x => !x.GetRawText().Contains("Aceros del Norte"));
    }

    [Fact]
    public async Task Una_coincidencia_de_un_representante_de_la_ficha_bloquea_hasta_que_cumplimiento_la_descarta()
    {
        var e = await PrepararAsync();
        await PublicarTextosAsync(e);
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", ListasRequeridas);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Aceros del Sur S.A.S.", correo);
        var sesion = await AceptarInvitacionAsync(vinculo, correo);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).ExpedienteAbiertoId.ShouldNotBeNull();

        // El tercero declara en su ficha legal a su representante; la reconsulta lo incluye y el simulado lo encuentra en OFAC.
        await TerceroAsync("PUT", "/public/v1/ficha/FICHA_LEGAL", sesion.AccessToken, new GuardarSeccionDto(JsonDocument.Parse("""
            { "personas": [ { "rol": "RepresentanteLegal", "tipo_identificacion": "CC", "numero": "1020304050", "nombres": "Ana", "apellidos": "Coincidencia",
                              "pep": false, "vinculo_pep": false } ] }
            """).RootElement.Clone()), 200);
        var reconsultado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/verificaciones/ListasRestrictivas/consultar",
            Delegado(e, e.Carlos), null, 200);
        var listas = reconsultado.Verificaciones!.Single(v => v.Tipo == "ListasRestrictivas");
        listas.Estado.ShouldBe("CoincidenciaPorAnalizar");
        listas.Personas.Select(p => p.Rol).ShouldBe(["Tercero", "RepresentanteLegal"]);
        var coincidencia = listas.Coincidencias.ShouldHaveSingleItem();
        (coincidencia.Persona, coincidencia.Lista, coincidencia.Rol).ShouldBe(("Ana Coincidencia", "OFAC (SDN)", "RepresentanteLegal"));

        // Las áreas aprueban; la coincidencia sin resolver impide cerrar.
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 200);
        var bloqueado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos),
            new VeredictoDto("Aprobado"), 200);
        bloqueado.Estado.ShouldBe("EnRevision");
        bloqueado.EsperandoVerificaciones.ShouldBeTrue();

        // Solo Cumplimiento resuelve y siempre con justificación.
        var ruta = $"/api/v1/expedientes/{expedienteId}/verificaciones/ListasRestrictivas/coincidencias/{coincidencia.Id}/resolver";
        await ApiAsync("POST", ruta, Delegado(e, e.Laura), new ResolverCoincidenciaDto("Descartada", "Homónimo"), 403);
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), new ResolverCoincidenciaDto("Descartada", " "), 422))).ShouldBe("justificacion-requerida");
        var aprobado = await ApiAsync<ExpedienteDto>("POST", ruta, Delegado(e, e.Carlos),
            new ResolverCoincidenciaDto("Descartada", "Homónimo: la cédula del listado es otra."), 200);
        aprobado.Estado.ShouldBe("Aprobado");
        var resuelta = aprobado.Verificaciones!.Single().Coincidencias.Single();
        (resuelta.Decision, resuelta.ResueltaPor).ShouldBe(("Descartada", e.Carlos.Usuario.Id.ToString()));
        resuelta.VigenteHasta!.Value.ShouldBe(resuelta.ResueltaEn!.Value.AddMonths(12));
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), new ResolverCoincidenciaDto("Confirmada", "x"), 409))).ShouldBe("estado-invalido");

        (await EsperarEventosAsync(e, "expediente.coincidencia_resuelta", 1)).Single().GetProperty("datos").GetProperty("decision").GetString()
            .ShouldBe("Descartada");
        // Cumplimiento se enteró por correo de la coincidencia.
        await Correos.EsperarAsync(e.Carlos.Usuario.Correo, m => m.Asunto.Contains("Coincidencia en listas"));
    }

    [Fact]
    public async Task Un_estudio_vencido_vuelve_a_pedirse_antes_de_cerrar()
    {
        var e = await PrepararAsync();
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", EstudioRequerido with { VigenciaDias = 1 });
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Aceros del Este S.A.S.");
        var abierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);

        var conEstudio = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/verificaciones/estudio-cumplimiento", Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(1, false, "Simple", "Favorable"), 200);
        conEstudio.Verificaciones!.Single().Estado.ShouldBe("Satisfecha");

        try
        {
            // Pasan dos días: el procesador lo expira y el expediente no cierra aunque las áreas aprueben.
            fixture.Reloj.Desfase = TimeSpan.FromDays(2);
            var vencido = await EsperarExpedienteAsync(e, abierto.Id, x => x.Verificaciones!.Single().Estado == "Expirada");
            vencido.Verificaciones!.Single().VigenciaDias.ShouldBe(1);
            // El reloj vuelve (las aserciones delegadas se firman con la hora real): lo expirado sigue expirado.
            fixture.Reloj.Desfase = TimeSpan.Zero;
            await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura),
                new VeredictoDto("Aprobado"), 200);
            var esperando = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/areas/{e.Cumplimiento.Id}/veredicto",
                Delegado(e, e.Carlos), new VeredictoDto("Aprobado"), 200);
            esperando.Estado.ShouldBe("EnRevision");
            (await ApiAsync<List<FilaCumplimientoDto>>("GET", "/api/v1/cumplimiento/bandeja", Delegado(e, e.Carlos), null, 200))
                .ShouldHaveSingleItem().Pendientes.ShouldHaveSingleItem().Estado.ShouldBe("Expirada");

            var aprobado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/verificaciones/estudio-cumplimiento", Delegado(e, e.Carlos),
                new RegistrarEstudioCumplimientoDto(2, false, "Simple", "Favorable", "Renovado."), 200);
            aprobado.Estado.ShouldBe("Aprobado");
            aprobado.Verificaciones!.Single().Anteriores.ShouldHaveSingleItem();
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }
    }

    [Fact]
    public async Task El_barrido_inmediato_de_Plataforma_vence_el_estudio_con_fecha_simulada_solo_en_el_grupo_indicado()
    {
        var e = await PrepararAsync();
        var otro = await PrepararAsync();
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", EstudioRequerido with { VigenciaDias = 1 });
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Aceros del Oeste S.A.S.");
        var abierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/verificaciones/estudio-cumplimiento", Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(1, false, "Simple", "Favorable"), 200)).Verificaciones!.Single().Estado.ShouldBe("Satisfecha");

        // Dentro de dos días (fecha simulada) el estudio de un día ya venció; pero el barrido de otro grupo no lo toca.
        var plataforma = new Credencial(ApiFixture.ClavePlataforma);
        var dentroDeDosDias = DateTimeOffset.UtcNow.AddDays(2);
        (await ApiAsync<ResultadoRevisionVerificacionesDto>("POST", "/api/v1/plataforma/verificaciones/revisar", plataforma,
            new RevisarVerificacionesDto(otro.TenantId, dentroDeDosDias), 200)).Expiraciones.ShouldBe(0);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{abierto.Id}", Admin(e), null, 200)).Verificaciones!.Single().Estado.ShouldBe("Satisfecha");

        // El barrido de su grupo, con esa fecha, lo expira (como el procesador cuando pasan los días de verdad); repetirlo no cambia nada.
        (await ApiAsync<ResultadoRevisionVerificacionesDto>("POST", "/api/v1/plataforma/verificaciones/revisar", plataforma,
            new RevisarVerificacionesDto(e.TenantId, dentroDeDosDias), 200)).Expiraciones.ShouldBe(1);
        var vencido = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{abierto.Id}", Admin(e), null, 200);
        vencido.Verificaciones!.Single().Estado.ShouldBe("Expirada");
        (await ApiAsync<ResultadoRevisionVerificacionesDto>("POST", "/api/v1/plataforma/verificaciones/revisar", plataforma,
            new RevisarVerificacionesDto(e.TenantId, dentroDeDosDias), 200)).Expiraciones.ShouldBe(0);
    }

    [Fact]
    public async Task La_empresa_configura_infolaft_con_clave_de_solo_escritura_y_ve_su_consumo_y_el_informe_sagrilaft()
    {
        var e = await PrepararAsync();
        var inicial = await ApiAsync<ListasRestrictivasConfigDto>("GET", "/api/v1/configuracion/listas-restrictivas", Admin(e), null, 200);
        inicial.ProveedorEfectivo.ShouldBe("Simulado");
        var infolaft = inicial.Proveedores.First();
        (infolaft.Nombre, infolaft.Principal, infolaft.HabilitadoEnInstalacion, infolaft.Configurado).ShouldBe(("Infolaft", true, false, false));

        var conClave = await ApiAsync("PUT", "/api/v1/configuracion/listas-restrictivas", Admin(e),
            new ActualizarListasRestrictivasDto(Credenciales: new CredencialesListasDto("Infolaft", "sinco-norte", "clave-super-secreta-9876", Plan: "Empresarial",
                CuotaMensual: 500)), 200);
        var texto = await conClave.ReadAsTextAsync();
        texto.ShouldNotContain("clave-super-secreta");
        var configurado = JsonSerializer.Deserialize<ListasRestrictivasConfigDto>(texto, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        var conInfolaft = configurado.Proveedores.Single(p => p.Nombre == "Infolaft");
        (conInfolaft.Configurado, conInfolaft.Usuario, conInfolaft.UltimosCuatroClave, conInfolaft.CuotaMensual, conInfolaft.ModoBusqueda)
            .ShouldBe((true, "sinco-norte", "9876", 500, "PorNombre"));
        // La instalación de pruebas no habilita proveedores reales: nunca se llama a Infolaft.
        (await CodigoAsync(await ApiAsync("PUT", "/api/v1/configuracion/listas-restrictivas", Admin(e), new ActualizarListasRestrictivasDto("Infolaft"), 422)))
            .ShouldBe("proveedor-no-habilitado");
        var auditoria = await ApiAsync("GET", "/api/v1/auditoria?entidad=ListasRestrictivasEmpresa", Admin(e), null, 200);
        (await auditoria.ReadAsTextAsync()).ShouldNotContain("clave-super-secreta");

        // Una consulta cuenta en el consumo del mes del proveedor que la hizo.
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", ListasRequeridas);
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Proveedor Coincidencia Ltda.");
        var abierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        await EsperarExpedienteAsync(e, abierto.Id, x => x.Verificaciones!.Single().Estado == "CoincidenciaPorAnalizar");
        var uso = (await ApiAsync<ListasRestrictivasConfigDto>("GET", "/api/v1/configuracion/listas-restrictivas", Admin(e), null, 200)).Uso.ShouldHaveSingleItem();
        (uso.Proveedor, uso.Consultas, uso.Busquedas, uso.ConCoincidencias).ShouldBe(("Simulado", 1, 1, 1));

        // Informe SAGRILAFT (el oficial tiene auditoria:leer) en JSON y en Excel.
        var informe = await ApiAsync<InformeCumplimientoDto>("GET", "/api/v1/informes/cumplimiento", Delegado(e, e.Carlos), null, 200);
        var resumen = informe.Verificaciones.ShouldHaveSingleItem();
        (resumen.Tipo, resumen.Proveedor, resumen.Total, resumen.ConCoincidencias, resumen.Pendientes).ShouldBe(("ListasRestrictivas", "Simulado", 1, 1, 1));
        informe.Filas.ShouldHaveSingleItem().Tercero.ShouldBe("Proveedor Coincidencia Ltda.");
        var excel = await ApiAsync("GET", "/api/v1/informes/cumplimiento/excel", Delegado(e, e.Carlos), null, 200);
        excel.Context.Response.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        await ApiAsync("GET", "/api/v1/informes/cumplimiento", Delegado(e, e.Laura), null, 403);
    }

    // ---------- utilidades ----------

    private async Task CrearFlujoConVerificacionesAsync(Escenario e, string tipo, params VerificacionRequeridaDto[] verificaciones)
    {
        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(e),
            new CrearFlujoDto("VER_" + tipo + "_" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "Con verificaciones " + tipo, tipo,
                [new EtapaDto(1, [new AreaDeEtapaDto(e.Aprobador.Id), new AreaDeEtapaDto(e.Cumplimiento.Id, PuedeRechazarExpediente: true)])],
                Verificaciones: verificaciones), 201);
        flujo.Borrador!.Verificaciones!.Count.ShouldBe(verificaciones.Length);
        (await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(e), new { }, 200)).Publicada!.Verificaciones!.Count
            .ShouldBe(verificaciones.Length);
    }

    private Task<VinculoDto> CrearVinculoConNombreAsync(Escenario e, string tipo, string nombre, string? correo = null)
    {
        var nit = "8" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        correo ??= CorreoNuevo();
        return ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(e), new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", nombre, null, [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            TipoVinculo: tipo,
            Atributos: null,
            CorreoInvitacion: correo,
            ReferenciaExterna: null), 201);
    }

    private async Task<ExpedienteDto> EsperarExpedienteAsync(Escenario e, Guid id, Func<ExpedienteDto, bool> condicion)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var expediente = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{id}", Admin(e), null, 200);
            if (condicion(expediente))
                return expediente;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"El expediente {expediente.Folio} no llegó al estado esperado: {JsonSerializer.Serialize(expediente.Verificaciones)}");
            await Task.Delay(200);
        }
    }

    private async Task<IReadOnlyList<JsonElement>> EsperarEventosAsync(Escenario e, string tipo, int cuantos)
    {
        var limite = DateTime.UtcNow.AddSeconds(30);
        while (true)
        {
            var pagina = await ApiAsync<JsonElement>("GET", $"/api/v1/eventos?tipos={tipo}&limite=200", Admin(e), null, 200);
            var datos = pagina.GetProperty("datos").EnumerateArray().Select(x => x.Clone()).ToList();
            if (datos.Count >= cuantos)
                return datos;
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"No llegaron {cuantos} eventos {tipo} (hay {datos.Count}).");
            await Task.Delay(200);
        }
    }
}
