using System.Text.Json;
using Alba;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Contratos;
using Validasoft.Dominio.Ficha;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- ficha maestra del tercero (F1: FM-16…FM-23) ----------

    private const string RutaFicha = "/public/v1/ficha";

    private static JsonElement Datos(string json) => JsonDocument.Parse(json).RootElement.Clone();

    private const string GeneralCompleta = """
        { "razon_social": "Aceros del Norte S.A.S.", "correo": "compras@aceros.co", "telefono": "+57 601 555 1234", "pais": "CO",
          "ciudad": "11001", "direccion": "Calle 45 # 12-08", "ciiu_principal": "4663" }
        """;

    private async Task<SeccionGuardadaDto> GuardarSeccionAsync(SesionTerceroDto sesion, string seccion, string datos, params SoporteSeccionDto[] soportes) =>
        (await (await PutFichaAsync($"{RutaFicha}/{seccion}", sesion, new GuardarSeccionDto(Datos(datos), soportes), 200)).ReadAsJsonAsync<SeccionGuardadaDto>())!;

    /// <summary>PUT del tercero con la consulta (<c>?periodo=</c>) aparte: Alba no la toma de la URL en un PUT con cuerpo.</summary>
    private Task<IScenarioResult> PutFichaAsync(string url, SesionTerceroDto sesion, GuardarSeccionDto cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            var partes = url.Split('?', 2);
            var envio = s.Put.Json(cuerpo).ToUrl(partes[0]);
            foreach (var par in partes.Length > 1 ? partes[1].Split('&') : [])
            {
                var (clave, valor) = (par.Split('=')[0], par.Split('=')[1]);
                envio.QueryString(clave, valor);
            }

            s.WithRequestHeader("Authorization", "Bearer " + sesion.AccessToken);
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<(string? Codigo, string? Campo)> ErrorDeSeccionAsync(SesionTerceroDto sesion, string ruta, string datos,
        params SoporteSeccionDto[] soportes)
    {
        var json = await (await PutFichaAsync(ruta, sesion, new GuardarSeccionDto(Datos(datos), soportes), 422)).ReadAsJsonAsync<JsonElement>();
        return (json.GetProperty("codigo").GetString(), json.TryGetProperty("campo", out var campo) ? campo.GetString() : null);
    }

    private async Task<EstadoDeFicha?> EstadoGuardadoAsync(Guid terceroId)
    {
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession("boveda");
        return await consulta.LoadAsync<EstadoDeFicha>(terceroId);
    }

    [Fact]
    public async Task El_tercero_diligencia_la_ficha_general_con_borrador_soporte_del_cambio_y_la_empresa_la_ve()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo, "900373913");
        var sesion = await SesionAsync(correo);
        var terceroId = vinculo.TerceroId;

        // Leer no crea nada: la ficha vacía empieza por la general y no hay EstadoDeFicha guardado.
        var vacia = await TerceroAsync<EstadoFichaDto>("GET", RutaFicha, sesion.AccessToken, null, 200);
        vacia.Orden.ShouldBe(["FICHA_GENERAL", "FICHA_TRIBUTARIA", "FICHA_LEGAL", "FICHA_FINANCIERA", "FICHA_SST", "FICHA_ESPECIALIDADES"]);
        vacia.SiguienteSeccion.ShouldBe("FICHA_GENERAL");
        vacia.Completa.ShouldBeFalse();
        vacia.Secciones.ShouldAllBe(s => s.Version == null && !s.Completa);
        (await TerceroAsync<SeccionFichaDto>("GET", $"{RutaFicha}/FICHA_GENERAL", sesion.AccessToken, null, 200)).Version.ShouldBeNull();
        (await EstadoGuardadoAsync(terceroId)).ShouldBeNull();

        // El portal busca en los catálogos de plataforma (semilla de desarrollo).
        var ciudades = await TerceroAsync<PaginaDto<EntradaCatalogoDto>>("GET", "/public/v1/catalogos/ciudades?q=bogota", sesion.AccessToken, null, 200);
        ciudades.Datos.ShouldContain(c => c.Codigo == "11001" && c.Departamento == "Bogotá, D.C.");

        // Borrador: queda guardado con sus faltantes exactos y el wizard no avanza.
        var borrador = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", """{ "razon_social": "Aceros del Norte S.A.S.", "correo": "compras@aceros.co" }""");
        borrador.Creada.ShouldBeTrue();
        borrador.Version.ShouldBe(1);
        borrador.Completitud.Completa.ShouldBeFalse();
        borrador.Completitud.Faltantes.Select(f => f.Campo).ShouldBe(["telefono", "pais", "direccion", "ciiu_principal"]);
        borrador.SiguienteSeccion.ShouldBe("FICHA_GENERAL");
        borrador.Seccion.Datos!.Value.GetProperty("numero").GetString().ShouldBe("900373913");
        borrador.Seccion.Datos.Value.GetProperty("dv").GetString().ShouldBe("4");

        // Completa (sin soporte: completar un borrador no es un cambio) ⇒ siguiente: tributaria.
        var completa = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta);
        completa.Version.ShouldBe(2);
        completa.Completitud.Completa.ShouldBeTrue();
        completa.Completitud.ReglaVersion.ShouldBe(3);
        completa.SiguienteSeccion.ShouldBe("FICHA_TRIBUTARIA");

        // Lo mismo otra vez ⇒ 200 sin versión nueva.
        var igual = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", GeneralCompleta.Replace("compras@aceros.co", "Compras@Aceros.CO"));
        igual.Creada.ShouldBeFalse();
        igual.Version.ShouldBe(2);

        // Formato inválido ⇒ 422 con el campo; nada se guarda.
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_GENERAL", GeneralCompleta.Replace("compras@aceros.co", "no-es-correo")))
            .ShouldBe(("valor-invalido", "correo"));
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_GENERAL", """{ "color": "azul" }""")).ShouldBe(("campo-desconocido", "color"));
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_GENERAL", """{ "dv": "1" }""")).ShouldBe(("dv-no-editable", "dv"));
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_GENERAL", GeneralCompleta.Replace("11001", "99999"))).ShouldBe(("codigo-invalido", "ciudad"));

        // Cambio sin soporte ⇒ versión nueva incompleta por el soporte; el wizard vuelve a la general.
        var cambio = GeneralCompleta.Replace("Calle 45 # 12-08", "Carrera 7 # 80-12");
        var sinSoporte = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", cambio);
        sinSoporte.Version.ShouldBe(3);
        sinSoporte.Completitud.Completa.ShouldBeFalse();
        sinSoporte.Completitud.Faltantes.ShouldHaveSingleItem().ShouldBe(new FaltanteSeccionDto(null, "soporte-del-cambio", "Falta el soporte del cambio"));
        sinSoporte.SiguienteSeccion.ShouldBe("FICHA_GENERAL");

        // Soportes inválidos ⇒ 422: un id que no es de su bóveda, el RUT de otro tercero y un tipo que no respalda la sección.
        var rut = (await SubirAsync(sesion, "RUT", Pdf("RUT"))).Version;
        var ajeno = await OtroTerceroConRutAsync(e);
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_GENERAL", cambio, new SoporteSeccionDto(Guid.NewGuid(), 1)))
            .ShouldBe(("soporte-invalido", "soportes[0]"));
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_GENERAL", cambio, new SoporteSeccionDto(ajeno.DocumentoId, ajeno.N)))
            .ShouldBe(("soporte-invalido", "soportes[0]"));
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_GENERAL", cambio, new SoporteSeccionDto(rut.DocumentoId, 9)))
            .ShouldBe(("soporte-version-invalida", "soportes[0]"));
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_FINANCIERA?periodo=2025", "{}", new SoporteSeccionDto(rut.DocumentoId, rut.N)))
            .ShouldBe(("soporte-tipo-invalido", "soportes[0]"));

        // Con el RUT como soporte ⇒ completa.
        var conSoporte = await GuardarSeccionAsync(sesion, "FICHA_GENERAL", cambio, new SoporteSeccionDto(rut.DocumentoId, rut.N));
        conSoporte.Version.ShouldBe(4);
        conSoporte.Completitud.Completa.ShouldBeTrue();
        conSoporte.Seccion.Soportes.ShouldHaveSingleItem().ShouldBe(new SoporteSeccionDto(rut.DocumentoId, rut.N, "RUT", false, "Registro Único Tributario"));
        conSoporte.SiguienteSeccion.ShouldBe("FICHA_TRIBUTARIA");

        // Tributaria con un soporte ad hoc (SOPORTE_FICHA, subido por la carga de siempre).
        var adhoc = (await SubirAsync(sesion, "SOPORTE_FICHA", Pdf("Soporte tributario"))).Version;
        var tributaria = await GuardarSeccionAsync(sesion, "FICHA_TRIBUTARIA",
            """{ "responsabilidades": ["5", "7", "48", "52"], "responsable_ica": true, "ica": [ { "municipio": "11001", "ciiu": "4663" } ] }""",
            new SoporteSeccionDto(adhoc.DocumentoId, adhoc.N));
        tributaria.Completitud.Completa.ShouldBeTrue();
        tributaria.SiguienteSeccion.ShouldBe("FICHA_LEGAL");
        tributaria.Seccion.Datos!.Value.GetProperty("responsabilidades").EnumerateArray().Select(r => r.GetString()).ShouldBe(["05", "07", "48", "52"]);

        // La financiera va por periodo.
        (await CodigoAsync(await TerceroAsync("GET", $"{RutaFicha}/FICHA_FINANCIERA", sesion.AccessToken, null, 422))).ShouldBe("periodo-requerido");
        (await CodigoAsync(await TerceroAsync("GET", $"{RutaFicha}/FICHA_BANCARIA", sesion.AccessToken, null, 404))).ShouldBe("seccion-no-encontrada");
        var financiera = await GuardarSeccionAsync(sesion, "FICHA_FINANCIERA?periodo=2025", """{ "fecha_corte": "2025-12-31", "activos": 0 }""");
        financiera.Seccion.Periodo.ShouldBe(2025);
        financiera.Completitud.Faltantes.Select(f => f.Campo).ShouldContain("pasivos");
        financiera.Completitud.Faltantes.Select(f => f.Campo).ShouldNotContain("activos"); // 0 es un valor, no vacío

        // La proyección refleja todo y el wizard sigue en la legal.
        var ficha = await TerceroAsync<EstadoFichaDto>("GET", RutaFicha, sesion.AccessToken, null, 200);
        ficha.SiguienteSeccion.ShouldBe("FICHA_LEGAL");
        var general = ficha.Secciones.Single(s => s.Seccion == "FICHA_GENERAL");
        (general.Version, general.Completa, general.ReglaVersion).ShouldBe((4, true, 3));
        ficha.Secciones.Single(s => s.Seccion == "FICHA_FINANCIERA").Periodo.ShouldBe(2025);
        var guardado = (await EstadoGuardadoAsync(terceroId)).ShouldNotBeNull();
        guardado.Secciones.Select(s => (s.Seccion, s.Version, s.Completa)).ShouldBe(
            [("FICHA_GENERAL", 4, true), ("FICHA_TRIBUTARIA", 1, true), ("FICHA_FINANCIERA", 1, false)]);
        var vigente = await TerceroAsync<SeccionFichaDto>("GET", $"{RutaFicha}/FICHA_GENERAL", sesion.AccessToken, null, 200);
        (vigente.Version, vigente.Versiones).ShouldBe((4, 4));
        vigente.Datos!.Value.GetProperty("direccion").GetString().ShouldBe("Carrera 7 # 80-12");

        // La empresa vinculada ve las secciones vigentes en la ficha 360; otra empresa sin vínculo, no.
        var deEmpresa = await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{terceroId}/ficha", Admin(e.Norte), null, 200);
        deEmpresa.Secciones.ShouldNotBeNull().Select(s => (s.Seccion, s.Version, s.Completitud!.Completa)).ShouldBe(
            [("FICHA_GENERAL", (int?)4, true), ("FICHA_TRIBUTARIA", 1, true), ("FICHA_FINANCIERA", 1, false)]);
        deEmpresa.Secciones![0].Soportes.ShouldHaveSingleItem().TipoDocumento.ShouldBe("RUT");
        var otra = await EmpresaNuevaAsync("Otra S.A.S.", "OTRA");
        await ApiAsync("GET", $"/api/v1/terceros/{terceroId}/ficha", Admin(otra), null, 404);
    }

    /// <summary>RUT subido por otro tercero (de otra invitación de la misma empresa).</summary>
    private async Task<VersionDocumentoDto> OtroTerceroConRutAsync(Escenario e)
    {
        var correo = CorreoNuevo();
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        return (await SubirAsync(sesion, "RUT", Pdf("RUT ajeno"))).Version;
    }

    [Fact]
    public async Task Catalogos_se_importan_de_forma_idempotente_y_recalcular_actualiza_la_completitud()
    {
        var plataforma = new Credencial(ApiFixture.ClavePlataforma);
        var prefijo = "T" + Guid.NewGuid().ToString("N")[..6].ToUpperInvariant();
        var grupo = new FilaCatalogoDto(prefijo, "Pruebas", Nivel: 1);
        var categoria = new FilaCatalogoDto($"{prefijo}.CAT", "Categoría de prueba", Padre: prefijo, Nivel: 2);
        var especialidad = new FilaCatalogoDto($"{prefijo}.CAT.ESP", "Especialidad de prueba", Padre: $"{prefijo}.CAT", Nivel: 3);
        var ruta = "/api/v1/plataforma/catalogos/especialidades/importar";

        // El orden de las filas no importa (se procesan por nivel); una huérfana se rechaza sin detener el lote.
        var primera = await ApiAsync<ResultadoImportacionCatalogoDto>("POST", ruta, plataforma,
            new ImportarCatalogoDto([especialidad, categoria, grupo, new FilaCatalogoDto($"{prefijo}.X.Y", "Huérfana", Padre: $"{prefijo}.X", Nivel: 3)]), 200);
        (primera.Creados, primera.Actualizados, primera.SinCambios).ShouldBe((3, 0, 0));
        primera.Rechazados.ShouldHaveSingleItem().Fila.ShouldBe(3);

        var segunda = await ApiAsync<ResultadoImportacionCatalogoDto>("POST", ruta, plataforma, new ImportarCatalogoDto([grupo, categoria, especialidad]), 200);
        (segunda.Creados, segunda.Actualizados, segunda.SinCambios).ShouldBe((0, 0, 3));
        segunda.Rechazados.ShouldBeEmpty();

        (await CodigoAsync(await ApiAsync("POST", "/api/v1/plataforma/catalogos/monedas/importar", plataforma, new ImportarCatalogoDto([grupo]), 404)))
            .ShouldBe("catalogo-no-encontrado");
        var hijos = await ApiAsync<PaginaDto<EntradaCatalogoDto>>("GET", $"/api/v1/catalogos/especialidades?padre={prefijo}.CAT", plataforma, null, 200);
        hijos.Datos.ShouldHaveSingleItem().Codigo.ShouldBe($"{prefijo}.CAT.ESP");

        // Un tercero declara esa especialidad ⇒ completa.
        var e = await PrepararAsync();
        await ApiAsync("POST", ruta, Admin(e.Norte), new ImportarCatalogoDto([grupo]), 403); // solo Plataforma importa
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        var guardada = await GuardarSeccionAsync(sesion, "FICHA_ESPECIALIDADES", $$"""{ "especialidades": ["{{prefijo.ToLowerInvariant()}}.cat.esp"] }""");
        guardada.Completitud.Completa.ShouldBeTrue();
        (await ErrorDeSeccionAsync(sesion, $"{RutaFicha}/FICHA_ESPECIALIDADES", $$"""{ "especialidades": ["{{prefijo}}.CAT"] }"""))
            .ShouldBe(("codigo-invalido", "especialidades[0]"));

        // Plataforma la retira del catálogo y recalcula: la versión vigente queda incompleta, sin versión nueva.
        var retiro = await ApiAsync<ResultadoImportacionCatalogoDto>("POST", ruta, plataforma, new ImportarCatalogoDto([especialidad with { Activo = false }]), 200);
        retiro.Actualizados.ShouldBe(1);
        var recalculo = await ApiAsync<RecalculoFichasDto>("POST", "/api/v1/plataforma/fichas/recalcular", plataforma, null, 200);
        recalculo.Recalculadas.ShouldBeGreaterThanOrEqualTo(1);
        recalculo.Reglas.Single(r => r.Seccion == "FICHA_ESPECIALIDADES").ReglaVersion.ShouldBe(1);

        var ficha = await TerceroAsync<EstadoFichaDto>("GET", RutaFicha, sesion.AccessToken, null, 200);
        var estado = ficha.Secciones.Single(s => s.Seccion == "FICHA_ESPECIALIDADES");
        (estado.Version, estado.Completa).ShouldBe((1, false));
        estado.Faltantes.ShouldHaveSingleItem().Campo.ShouldBe("especialidades[0]");

        // Idempotente: otro recálculo no vuelve a tocar esa sección.
        await ApiAsync<RecalculoFichasDto>("POST", "/api/v1/plataforma/fichas/recalcular", plataforma, null, 200);
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession("boveda");
        var eventos = await consulta.Events.FetchStreamAsync(SeccionDeFicha.IdPara(vinculo.TerceroId, "FICHA_ESPECIALIDADES", null));
        eventos.Select(x => x.Data.GetType().Name).ShouldBe(["SeccionDeFichaIniciada", "VersionDeSeccionRegistrada", "CompletitudDeSeccionRecalculada"]);
    }
}
