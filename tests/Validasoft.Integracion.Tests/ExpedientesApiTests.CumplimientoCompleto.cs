using System.Net.Http.Headers;
using System.Text;
using System.Text.Json;
using Alba;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Cumplimiento completo (H7, CU-15…CU-21) de punta a punta con el proveedor de listas simulado: la matriz de riesgo por empresa
/// y el estudio que deriva de ella el nivel y la debida diligencia, los archivos del oficial, la consulta de listas hecha por
/// fuera, el rechazo directo, la expiración de las listas al cambiar el representante, el correo de resultado cuando cierra el
/// procesador y el informe SAGRILAFT completo.
/// </summary>
public sealed partial class ExpedientesApiTests
{
    private static readonly byte[] PdfDeCumplimiento = Encoding.ASCII.GetBytes("%PDF-1.4\n1 0 obj<< /Type /Catalog >>endobj\ntrailer<<>>\n%%EOF");

    private async Task<MatrizDeRiesgoDto> ActivarMatrizAsync(Escenario e, Func<MatrizDeRiesgoDto, ActualizarMatrizDeRiesgoDto>? ajustar = null)
    {
        var plantilla = await ApiAsync<MatrizDeRiesgoDto>("GET", "/api/v1/configuracion/matriz-riesgo", Admin(e), null, 200);
        var dto = ajustar?.Invoke(plantilla) ?? new ActualizarMatrizDeRiesgoDto(true, plantilla.Factores, plantilla.LimiteBajo, plantilla.LimiteMedio, plantilla.Niveles);
        return await ApiAsync<MatrizDeRiesgoDto>("PUT", "/api/v1/configuracion/matriz-riesgo", Admin(e), dto, 200);
    }

    private async Task<ExpedienteDto> ExpedienteConEstudioAsync(Escenario e, string nombre)
    {
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", EstudioRequerido);
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", nombre);
        var abierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 200);
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos), new VeredictoDto("Aprobado"), 200);
        return abierto;
    }

    private async Task<AdjuntoDeCumplimientoDto> SubirAdjuntoAsync(Escenario e, Persona quien, Guid expedienteId, string proposito, string nombre = "consulta.pdf",
        byte[]? contenido = null)
    {
        contenido ??= PdfDeCumplimiento;
        var ruta = $"/api/v1/expedientes/{expedienteId}/cumplimiento/adjuntos";
        var carga = await ApiAsync<CargaDeAdjuntoDto>("PUT", ruta, Delegado(e, quien), new IniciarAdjuntoDeCumplimientoDto(nombre, contenido.Length, "application/pdf", proposito), 200);
        using var http = new HttpClient();
        using var put = new HttpRequestMessage(HttpMethod.Put, carga.Carga.Url) { Content = new ByteArrayContent(contenido) };
        put.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(carga.Carga.Cabeceras["Content-Type"]);
        (await http.SendAsync(put)).EnsureSuccessStatusCode();
        return await ApiAsync<AdjuntoDeCumplimientoDto>("POST", $"{ruta}/{carga.CargaId}/confirmar", Delegado(e, quien), new ConfirmarAdjuntoDeCumplimientoDto(nombre, proposito), 200);
    }

    // ---------- CU-17: matriz de riesgo ----------

    [Fact]
    public async Task La_matriz_de_riesgo_es_de_la_empresa_y_el_estudio_deriva_de_ella_el_nivel_y_la_debida_diligencia()
    {
        var e = await PrepararAsync();

        // Sin matriz propia se ofrece la plantilla (inactiva); todos la leen y solo el administrador la escribe.
        var plantilla = await ApiAsync<MatrizDeRiesgoDto>("GET", "/api/v1/configuracion/matriz-riesgo", Delegado(e, e.Carlos), null, 200);
        (plantilla.EsPorDefecto, plantilla.Activa, plantilla.Version, plantilla.Factores.Count, plantilla.Niveles.Count).ShouldBe((true, false, 0, 5, 3));
        plantilla.Niveles.Select(n => n.DebidaDiligencia).ShouldBe(["Simple", "Ordinaria", "Ampliada"]);
        var actualizar = new ActualizarMatrizDeRiesgoDto(true, plantilla.Factores, plantilla.LimiteBajo, plantilla.LimiteMedio, plantilla.Niveles);
        await ApiAsync("PUT", "/api/v1/configuracion/matriz-riesgo", Delegado(e, e.Carlos), actualizar, 403);
        (await CodigoAsync(await ApiAsync("PUT", "/api/v1/configuracion/matriz-riesgo", Admin(e),
            actualizar with { Factores = [.. plantilla.Factores.Select(f => f with { Peso = 0 })] }, 422))).ShouldBe("peso-invalido");
        (await CodigoAsync(await ApiAsync("PUT", "/api/v1/configuracion/matriz-riesgo", Admin(e),
            actualizar with { Niveles = [.. plantilla.Niveles.Select(n => n.Nivel == 3 ? n with { DebidaDiligencia = "Ordinaria" } : n)] }, 422))).ShouldBe("niveles-invalidos");

        // La empresa la ajusta: el sector pesa más y las opciones de riesgo alto suben a 100.
        var guardada = await ActivarMatrizAsync(e, p => new ActualizarMatrizDeRiesgoDto(true,
            [.. p.Factores.Select(f => f.Codigo == "sector" ? f with { Peso = 25 } : f)], 30, 60, p.Niveles));
        (guardada.EsPorDefecto, guardada.Activa, guardada.Version, guardada.LimiteBajo, guardada.LimiteMedio).ShouldBe((false, true, 1, 30, 60));
        var auditoria = await (await ApiAsync("GET", "/api/v1/auditoria?entidad=MatrizDeRiesgoEmpresa", Admin(e), null, 200)).ReadAsTextAsync();
        auditoria.ShouldContain("MatrizDeRiesgoEmpresa");

        var expediente = await ExpedienteConEstudioAsync(e, "Aceros del Centro S.A.S.");
        var preview = await ApiAsync<ResultadoDeMatrizDto>("POST", $"/api/v1/expedientes/{expediente.Id}/cumplimiento/matriz/calcular", Delegado(e, e.Carlos),
            new CalcularMatrizDto([new RespuestaDeMatrizDto("sector", "medio"), new RespuestaDeMatrizDto("operacion", "medio")]), 200);
        (preview.Puntaje, preview.Nivel, preview.DebidaDiligencia, preview.MesesRevision, preview.VersionMatriz).ShouldBe((19.0m, 1, "Simple", 24, 1));
        preview.Desglose.Select(d => d.Origen).ShouldBe(["Automatico", "Automatico", "Automatico", "Manual", "Manual"]);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/cumplimiento/matriz/calcular", Delegado(e, e.Carlos),
            new CalcularMatrizDto([new RespuestaDeMatrizDto("sector", "medio")]), 422))).ShouldBe("respuestas-incompletas");

        // El oficial solo responde los factores manuales; nivel y debida diligencia salen del cálculo.
        var respuestas = new[] { new RespuestaDeMatrizDto("sector", "medio"), new RespuestaDeMatrizDto("operacion", "medio") };
        var ruta = $"/api/v1/expedientes/{expediente.Id}/verificaciones/estudio-cumplimiento";
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), new RegistrarEstudioCumplimientoDto(2, false, null, "Favorable", Respuestas: respuestas), 422)))
            .ShouldBe("justificacion-de-nivel-requerida");
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(2, false, "Simple", "Favorable", Respuestas: respuestas, JustificacionDeNivel: "Sector regulado que la matriz no refleja."), 422)))
            .ShouldBe("debida-diligencia-insuficiente");
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(null, false, null, "Favorable", Respuestas: [respuestas[0]]), 422))).ShouldBe("respuestas-incompletas");

        var aprobado = await ApiAsync<ExpedienteDto>("POST", ruta, Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(2, false, null, "Favorable", "Ajustado por el sector.", Respuestas: respuestas,
                JustificacionDeNivel: "Sector regulado que la matriz no refleja."), 200);
        aprobado.Estado.ShouldBe("Aprobado");
        var estudio = aprobado.Verificaciones!.Single(v => v.Tipo == "EstudioDeCumplimiento").Estudio.ShouldNotBeNull();
        (estudio.NivelRiesgo, estudio.DebidaDiligencia, estudio.JustificacionDeNivel).ShouldBe((2, "Ordinaria", "Sector regulado que la matriz no refleja."));
        (estudio.Matriz!.Nivel, estudio.Matriz.Puntaje, estudio.Matriz.VersionMatriz).ShouldBe((1, 19.0m, 1));
        estudio.Matriz.Desglose.Count.ShouldBe(5);

        // Cambiar la matriz después no toca lo ya decidido: el estudio conserva su cálculo y su versión.
        await ActivarMatrizAsync(e, p => new ActualizarMatrizDeRiesgoDto(false, p.Factores, 10, 20, p.Niveles));
        var releido = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expediente.Id}", Admin(e), null, 200);
        releido.Verificaciones!.Single(v => v.Tipo == "EstudioDeCumplimiento").Estudio!.Matriz!.VersionMatriz.ShouldBe(1);
        (await CodigoAsync(await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/cumplimiento/matriz/calcular", Delegado(e, e.Carlos),
            new CalcularMatrizDto(respuestas), 422))).ShouldBe("matriz-no-activa");
    }

    // ---------- CU-16: archivos del oficial y consulta hecha por fuera ----------

    [Fact]
    public async Task El_oficial_sube_archivos_al_estudio_y_quedan_en_el_expediente_con_descarga_auditada()
    {
        var e = await PrepararAsync();
        var expediente = await ExpedienteConEstudioAsync(e, "Aceros del Oriente S.A.S.");

        // Quien no es de cumplimiento no sube archivos; tipo, tamaño y propósito se validan.
        var ruta = $"/api/v1/expedientes/{expediente.Id}/cumplimiento/adjuntos";
        await ApiAsync("PUT", ruta, Delegado(e, e.Laura), new IniciarAdjuntoDeCumplimientoDto("a.pdf", 10, "application/pdf", "Estudio"), 403);
        (await CodigoAsync(await ApiAsync("PUT", ruta, Delegado(e, e.Carlos), new IniciarAdjuntoDeCumplimientoDto("a.exe", 10, "application/x-msdownload", "Estudio"), 422)))
            .ShouldBe("adjunto-invalido");
        (await CodigoAsync(await ApiAsync("PUT", ruta, Delegado(e, e.Carlos), new IniciarAdjuntoDeCumplimientoDto("a.pdf", 10, "application/pdf", "Otro"), 422)))
            .ShouldBe("proposito-invalido");

        // Un archivo que dice ser PDF y no lo es se rechaza al confirmar (el tipo declarado no basta).
        var falsa = await ApiAsync<CargaDeAdjuntoDto>("PUT", ruta, Delegado(e, e.Carlos), new IniciarAdjuntoDeCumplimientoDto("falso.pdf", 12, "application/pdf", "Estudio"), 200);
        using (var http = new HttpClient())
        {
            using var put = new HttpRequestMessage(HttpMethod.Put, falsa.Carga.Url) { Content = new ByteArrayContent("no es un pdf"u8.ToArray()) };
            put.Content.Headers.ContentType = MediaTypeHeaderValue.Parse(falsa.Carga.Cabeceras["Content-Type"]);
            (await http.SendAsync(put)).EnsureSuccessStatusCode();
        }

        (await CodigoAsync(await ApiAsync("POST", $"{ruta}/{falsa.CargaId}/confirmar", Delegado(e, e.Carlos), new ConfirmarAdjuntoDeCumplimientoDto("falso.pdf", "Estudio"), 422)))
            .ShouldBe("adjunto-invalido");
        (await CodigoAsync(await ApiAsync("POST", $"{ruta}/{Guid.NewGuid()}/confirmar", Delegado(e, e.Carlos), new ConfirmarAdjuntoDeCumplimientoDto("x.pdf", "Estudio"), 409)))
            .ShouldBe("archivo-no-cargado");

        var soporte = await SubirAdjuntoAsync(e, e.Carlos, expediente.Id, "Estudio", "soporte-estudio.pdf");
        (soporte.Nombre, soporte.TipoContenido, soporte.Proposito, soporte.Tamano, soporte.SubidoPor).ShouldBe(("soporte-estudio.pdf", "application/pdf", "Estudio",
            PdfDeCumplimiento.LongLength, e.Carlos.Usuario.Id.ToString()));
        soporte.Sha256.Length.ShouldBe(64);

        // Solo se cita en el estudio un archivo de ese propósito; el estudio lo guarda con su nombre.
        var deConsulta = await SubirAdjuntoAsync(e, e.Carlos, expediente.Id, "ConsultaManual");
        var ruta2 = $"/api/v1/expedientes/{expediente.Id}/verificaciones/estudio-cumplimiento";
        (await CodigoAsync(await ApiAsync("POST", ruta2, Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(1, false, "Simple", "Favorable", Adjuntos: [deConsulta.Id]), 422))).ShouldBe("adjunto-invalido");
        var aprobado = await ApiAsync<ExpedienteDto>("POST", ruta2, Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(1, false, "Simple", "Favorable", Adjuntos: [soporte.Id]), 200);
        aprobado.AdjuntosDeCumplimiento!.Select(a => a.Nombre).ShouldBe(["soporte-estudio.pdf", "consulta.pdf"]);
        aprobado.Verificaciones!.Single().Estudio!.Adjuntos.ShouldHaveSingleItem().Id.ShouldBe(soporte.Id);

        var descarga = await ApiAsync<DescargaEvidenciaDto>("POST", $"{ruta}/{soporte.Id}/descarga", Admin(e), null, 200);
        descarga.NombreArchivo.ShouldBe("soporte-estudio.pdf");
        using (var http = new HttpClient())
            (await http.GetByteArrayAsync(descarga.Url)).ShouldBe(PdfDeCumplimiento);
        var historial = (await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{expediente.Id}/historial", Admin(e), null, 200)).Select(h => h.Tipo).ToList();
        historial.Count(t => t == "adjunto_de_cumplimiento_subido").ShouldBe(2);
        historial.ShouldContain("evidencia_de_verificacion_descargada");
        (await CodigoAsync(await ApiAsync("POST", $"{ruta}/{Guid.NewGuid()}/descarga", Admin(e), null, 404))).ShouldBe("adjunto-no-encontrado");
    }

    [Fact]
    public async Task Una_consulta_de_listas_hecha_por_fuera_entra_con_su_pdf_la_coincidencia_se_analiza_y_el_expediente_se_aprueba()
    {
        var e = await PrepararAsync();
        await PublicarTextosAsync(e);
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", ListasRequeridas);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Aceros del Litoral S.A.S.", correo);
        var sesion = await AceptarInvitacionAsync(vinculo, correo);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).ExpedienteAbiertoId.ShouldNotBeNull();
        await EsperarExpedienteAsync(e, expedienteId, x => x.Verificaciones!.Any(v => v.Estado == "Satisfecha"));
        await TerceroAsync("PUT", "/public/v1/ficha/FICHA_LEGAL", sesion.AccessToken, new GuardarSeccionDto(JsonDocument.Parse("""
            { "personas": [ { "rol": "RepresentanteLegal", "tipo_identificacion": "CC", "numero": "1020304051", "nombres": "Luis", "apellidos": "Homonimo",
                              "pep": false, "vinculo_pep": false } ] }
            """).RootElement.Clone()), 200);
        await EsperarExpedienteAsync(e, expedienteId, x => x.Verificaciones!.Single().Estado == "Satisfecha" &&
                                                           x.Verificaciones!.Single().Personas.Any(p => p.Rol == "RepresentanteLegal"));

        // Las personas que cubre la consulta salen de la ficha: sobre ellas se reportan las coincidencias.
        var personas = await ApiAsync<PersonasParaConsultaDto>("GET", $"/api/v1/expedientes/{expedienteId}/verificaciones/ListasRestrictivas/personas",
            Delegado(e, e.Carlos), null, 200);
        personas.Claves.Count.ShouldBe(2);
        var clave = personas.Claves[1];

        var pdf = await SubirAdjuntoAsync(e, e.Carlos, expedienteId, "ConsultaManual", "infolaft-consulta.pdf");
        var ruta = $"/api/v1/expedientes/{expedienteId}/verificaciones/ListasRestrictivas/consulta-manual";
        var hoy = fixture.Reloj.GetUtcNow();
        await ApiAsync("POST", ruta, Delegado(e, e.Laura), new RegistrarConsultaManualDto(pdf.Id, "Infolaft web", hoy), 403);
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), new RegistrarConsultaManualDto(pdf.Id, "x", hoy), 422))).ShouldBe("fuente-invalida");
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), new RegistrarConsultaManualDto(pdf.Id, "Infolaft web", hoy.AddDays(-40)), 422)))
            .ShouldBe("consulta-fuera-de-plazo");
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), new RegistrarConsultaManualDto(Guid.NewGuid(), "Infolaft web", hoy), 422)))
            .ShouldBe("adjunto-invalido");
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos),
            new RegistrarConsultaManualDto(pdf.Id, "Infolaft web", hoy, [new CoincidenciaManualDto("CO|CC|1", "OFAC (SDN)", "X")]), 422))).ShouldBe("persona-invalida");

        var conCoincidencia = await ApiAsync<ExpedienteDto>("POST", ruta, Delegado(e, e.Carlos),
            new RegistrarConsultaManualDto(pdf.Id, "Infolaft web", hoy, [new CoincidenciaManualDto(clave, "OFAC (SDN)", "LUIS HOMONIMO", "Otra fecha de nacimiento.", 97m)], 6), 200);
        var listas = conCoincidencia.Verificaciones!.Single();
        (listas.Proveedor, listas.Estado, listas.Resultado).ShouldBe(("Manual · Infolaft web", "CoincidenciaPorAnalizar", "Coincidencias"));
        listas.Evidencia!.NombreArchivo.ShouldBe("infolaft-consulta.pdf");
        listas.VigenteHasta!.Value.ShouldBe(hoy.AddMonths(6), TimeSpan.FromSeconds(2));
        listas.RegistradaPor.ShouldBe(e.Carlos.Usuario.Id.ToString());

        // La evidencia es el PDF que subió el oficial.
        var descarga = await ApiAsync<DescargaEvidenciaDto>("POST", $"/api/v1/expedientes/{expedienteId}/verificaciones/ListasRestrictivas/evidencia/descarga",
            Admin(e), null, 200);
        using (var http = new HttpClient())
            (await http.GetByteArrayAsync(descarga.Url)).ShouldBe(PdfDeCumplimiento);

        // Las áreas aprueban, pero la coincidencia por analizar impide cerrar.
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 200);
        (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos), new VeredictoDto("Aprobado"), 200))
            .Estado.ShouldBe("EnRevision");

        // Decide Cumplimiento (descarta el homónimo) y, al ser lo último que faltaba, el expediente se aprueba.
        var aprobado = await ApiAsync<ExpedienteDto>("POST",
            $"/api/v1/expedientes/{expedienteId}/verificaciones/ListasRestrictivas/coincidencias/{listas.Coincidencias.Single().Id}/resolver", Delegado(e, e.Carlos),
            new ResolverCoincidenciaDto("Descartada", "Homónimo: la cédula del listado es otra."), 200);
        aprobado.Estado.ShouldBe("Aprobado");
    }

    // ---------- CU-18: rechazo directo del oficial ----------

    [Fact]
    public async Task El_oficial_rechaza_directamente_solo_ante_un_hallazgo_y_el_tercero_recibe_el_correo_del_rechazo()
    {
        var e = await PrepararAsync();
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", ListasRequeridas, EstudioRequerido);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Proveedor Coincidencia S.A.S.", correo);
        var abierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        var consultado = await EsperarExpedienteAsync(e, abierto.Id, x => x.Verificaciones!.Any(v => v.Tipo == "ListasRestrictivas" && v.Estado == "CoincidenciaPorAnalizar"));
        var ruta = $"/api/v1/expedientes/{abierto.Id}/cumplimiento/rechazar";
        var dto = new RechazarPorCumplimientoDto(["no-corresponde-al-tercero"], "Coincidencia confirmada en OFAC.");

        // Sin hallazgo confirmado, ni quien no es de cumplimiento: no se rechaza.
        await ApiAsync("POST", ruta, Delegado(e, e.Laura), dto, 403);
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), dto, 409))).ShouldBe("rechazo-sin-hallazgo");

        var id = consultado.Verificaciones!.Single(v => v.Tipo == "ListasRestrictivas").Coincidencias.Single().Id;
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{abierto.Id}/verificaciones/ListasRestrictivas/coincidencias/{id}/resolver", Delegado(e, e.Carlos),
            new ResolverCoincidenciaDto("Confirmada", "Es la misma persona: coincide el documento."), 200);

        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), dto with { Motivos = [] }, 422))).ShouldBe("motivo-requerido");
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), dto with { Justificacion = " " }, 422))).ShouldBe("justificacion-requerida");
        var rechazado = await ApiAsync<ExpedienteDto>("POST", ruta, Delegado(e, e.Carlos), dto, 200);

        rechazado.Estado.ShouldBe("Rechazado");
        (rechazado.Rechazo!.Origen, rechazado.Rechazo.AreaId, rechazado.Rechazo.Usuario, rechazado.Rechazo.Justificacion)
            .ShouldBe(("Cumplimiento", Guid.Empty, e.Carlos.Usuario.Id.ToString(), "Coincidencia confirmada en OFAC."));
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).Estado.ShouldNotBe("Activo");
        await Correos.EsperarAsync(correo, m => m.Asunto.Contains("sin aprobarla"));
        await EsperarEventosAsync(e, "expediente.rechazado", 1);
        (await CodigoAsync(await ApiAsync("POST", ruta, Delegado(e, e.Carlos), dto, 409))).ShouldBe("estado-invalido");
    }

    // ---------- CU-15: expirar las listas al cambiar el representante ----------

    [Fact]
    public async Task Cambiar_el_representante_legal_expira_las_listas_y_se_vuelven_a_consultar_solas_con_la_persona_nueva()
    {
        var e = await PrepararAsync();
        await PublicarTextosAsync(e);
        await CrearFlujoConVerificacionesAsync(e, "CLIENTE", ListasRequeridas with { Bloqueante = false });
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Aceros del Valle S.A.S.", correo);
        var sesion = await AceptarInvitacionAsync(vinculo, correo);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).ExpedienteAbiertoId.ShouldNotBeNull();
        var inicial = await EsperarExpedienteAsync(e, expedienteId, x => x.Verificaciones!.Any(v => v.Estado == "Satisfecha"));
        inicial.Verificaciones!.Single().Personas.ShouldHaveSingleItem().Rol.ShouldBe("Tercero");

        // Un representante nuevo en la ficha legal: la consulta no lo cubría, se expira y se repite incluyéndolo.
        await TerceroAsync("PUT", "/public/v1/ficha/FICHA_LEGAL", sesion.AccessToken, new GuardarSeccionDto(JsonDocument.Parse("""
            { "personas": [ { "rol": "RepresentanteLegal", "tipo_identificacion": "CC", "numero": "1020304052", "nombres": "Ana", "apellidos": "Coincidencia",
                              "pep": false, "vinculo_pep": false } ] }
            """).RootElement.Clone()), 200);
        var repetido = await EsperarExpedienteAsync(e, expedienteId, x => x.Verificaciones!.Single().Estado == "CoincidenciaPorAnalizar");
        repetido.Verificaciones!.Single().Personas.Select(p => p.Rol).ShouldBe(["Tercero", "RepresentanteLegal"]);
        repetido.Verificaciones!.Single().Anteriores.ShouldHaveSingleItem(); // la primera consulta quedó en el historial
        var historial = (await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{expedienteId}/historial", Admin(e), null, 200)).Select(h => h.Tipo).ToList();
        historial.ShouldContain("verificacion_expirada");
        historial.Count(t => t == "verificacion_externa_registrada").ShouldBe(2);
    }

    // ---------- CU-19: correo de resultado cuando cierra el procesador ----------

    [Fact]
    public async Task Si_el_cierre_lo_provoca_la_consulta_automatica_de_listas_el_tercero_recibe_el_correo_de_resultado()
    {
        var e = await PrepararAsync();
        // La única área aprueba sola: lo único que falta para cerrar son las listas, y las consulta el procesador, no una persona.
        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(e),
            new CrearFlujoDto("AUTO_" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "Solo listas", "CLIENTE",
                [new EtapaDto(1, [new AreaDeEtapaDto(e.Aprobador.Id, Automatica: true)])], Verificaciones: [ListasRequeridas]), 201);
        await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(e), new { }, 200);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoConNombreAsync(e, "CLIENTE", "Aceros del Pacifico S.A.S.", correo);
        var abierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);

        var aprobado = await EsperarExpedienteAsync(e, abierto.Id, x => x.Estado == "Aprobado");
        aprobado.Verificaciones!.Single().RegistradaPor.ShouldBe("sistema");

        var mensaje = await Correos.EsperarAsync(correo, m => m.Asunto.Contains("aprobó tu validación"));
        mensaje.Asunto.ShouldContain(aprobado.Folio);
    }

    // ---------- Informe SAGRILAFT completo ----------

    [Fact]
    public async Task El_informe_sagrilaft_completo_junta_listas_estudio_riesgo_y_proxima_revision_de_cada_tercero_y_sale_en_excel()
    {
        var e = await PrepararAsync();
        await ActivarMatrizAsync(e);
        var expediente = await ExpedienteConEstudioAsync(e, "Aceros del Caribe S.A.S.");
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/verificaciones/estudio-cumplimiento", Delegado(e, e.Carlos),
            new RegistrarEstudioCumplimientoDto(null, false, null, "Favorable", "Sin novedades.",
                Respuestas: [new RespuestaDeMatrizDto("sector", "alto"), new RespuestaDeMatrizDto("operacion", "alto")]), 200);

        var informe = await ApiAsync<InformeSagrilaftDto>("GET", "/api/v1/informes/sagrilaft", Delegado(e, e.Carlos), null, 200);

        (informe.Terceros, informe.ConEstudioVigente, informe.EstudiosVencidos, informe.SinEstudio).ShouldBe((1, 1, 0, 0));
        informe.Tendencia.Count.ShouldBe(12);
        informe.Tendencia[^1].Estudios.ShouldBe(1);
        var fila = informe.Filas.ShouldHaveSingleItem();
        (fila.Tercero, fila.EstadoDebidaDiligencia, fila.EstadoListas, fila.Concepto, fila.Oficial).ShouldBe(("Aceros del Caribe S.A.S.", "Vigente", "SinConsulta", "Favorable",
            e.Carlos.Usuario.Id.ToString()));
        // Puntaje 35 con la matriz por omisión (sector y operación en riesgo alto): Medio, con revisión a los 12 meses.
        (fila.NivelRiesgo, fila.DebidaDiligencia).ShouldBe((2, "Ordinaria"));
        fila.ProximaRevision!.Value.ShouldBe(fila.FechaEstudio!.Value.AddMonths(12), TimeSpan.FromSeconds(1));
        informe.Riesgo.ShouldHaveSingleItem().ShouldBe(new RiesgoDto(2, 1));

        var excel = await ApiAsync("GET", "/api/v1/informes/sagrilaft/excel", Delegado(e, e.Carlos), null, 200);
        excel.Context.Response.ContentType.ShouldBe("application/vnd.openxmlformats-officedocument.spreadsheetml.sheet");
        var personas = await ApiAsync<PersonasDelTerceroDto>("GET", $"/api/v1/informes/sagrilaft/terceros/{fila.TerceroId}/personas", Delegado(e, e.Carlos), null, 200);
        personas.Tercero.ShouldBe("Aceros del Caribe S.A.S.");

        // El informe es de la empresa y de quien puede leer la auditoría.
        await ApiAsync("GET", "/api/v1/informes/sagrilaft", Delegado(e, e.Laura), null, 403);
        await ApiAsync("GET", "/api/v1/informes/sagrilaft/excel", Delegado(e, e.Laura), null, 403);
    }
}
