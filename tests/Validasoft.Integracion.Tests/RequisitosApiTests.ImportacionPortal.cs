using System.Text.Json;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Integracion.Tests;

public sealed partial class RequisitosApiTests
{
    // ---------- importador genérico con el adaptador del Portal y sincronizador de coexistencia (F4: FM-39…; IO-1) ----------

    private const string RutaImportacion = "/api/v1/plataforma/importacion/portal";

    private sealed record FixturePortal(int TeridJ, int TeridN, int TeridE, string NitJ, string CcN, string NitE)
    {
        public string Json(string telefono = "6015551234", string direccion = "Calle 45 # 12-08") =>
            File.ReadAllText(Path.Combine(AppContext.BaseDirectory, "Fixtures", "portal-terceros.json"))
                .Replace("{{TERID_J}}", TeridJ.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{{TERID_N}}", TeridN.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{{TERID_E}}", TeridE.ToString(System.Globalization.CultureInfo.InvariantCulture), StringComparison.Ordinal)
                .Replace("{{NIT_J}}", NitJ, StringComparison.Ordinal)
                .Replace("{{CC_N}}", CcN, StringComparison.Ordinal)
                .Replace("{{NIT_E}}", NitE, StringComparison.Ordinal)
                .Replace("{{TELEFONO_J}}", telefono, StringComparison.Ordinal)
                .Replace("{{DIRECCION_J}}", direccion, StringComparison.Ordinal);
    }

    private static FixturePortal NuevoFixturePortal()
    {
        var baseId = Random.Shared.Next(1_000_000, 9_000_000);
        // El Portal trae DV 0 para el jurídico: se elige un NIT cuyo DV real no sea 0, así siempre hay DV que recalcular.
        var nitJ = Nit();
        while (Validasoft.Dominio.Identificaciones.DigitoVerificacion.NitColombia(nitJ) == "0")
            nitJ = Nit();
        return new FixturePortal(baseId, baseId + 1, baseId + 2, nitJ, Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Nit());
    }

    /// <summary>Encola una corrida con el JSON del fixture y espera (hasta 60 s) a que termine.</summary>
    private async Task<CorridaImportacionDto> ImportarPortalAsync(string json, bool dryRun, IReadOnlyList<UnidadEmpresaDto>? unidades = null)
    {
        var plataforma = new Credencial(ApiFixture.ClavePlataforma);
        var encolada = await ApiAsync<CorridaImportacionDto>("POST", RutaImportacion, plataforma,
            new ImportarTercerosDto(dryRun, "Archivo", JsonDocument.Parse(json).RootElement.Clone(), unidades, TamanoLote: 2), 202);
        encolada.Estado.ShouldBeOneOf("Encolada", "EnCurso", "Completada");
        var limite = DateTime.UtcNow.AddSeconds(60);
        while (true)
        {
            var corrida = await ApiAsync<CorridaImportacionDto>("GET", $"{RutaImportacion}/{encolada.Id}", plataforma, null, 200);
            if (corrida.Estado is "Completada" or "Fallida")
            {
                corrida.Error.ShouldBeNull();
                corrida.Estado.ShouldBe("Completada");
                return corrida;
            }

            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"La corrida {encolada.Id} no terminó (está {corrida.Estado}).");
            await Task.Delay(200);
        }
    }

    private static ResumenSeccionImportadaDto Seccion(CorridaImportacionDto c, string seccion) => c.Resumen.Secciones.Single(s => s.Seccion == seccion);

    private async Task<T?> CargarAsync<T>(object id, string? tenant = null) where T : class
    {
        await using var consulta = tenant is null
            ? Host.Services.GetRequiredService<IDocumentStore>().QuerySession()
            : Host.Services.GetRequiredService<IDocumentStore>().QuerySession(tenant);
        return id switch
        {
            Guid g => await consulta.LoadAsync<T>(g),
            string s => await consulta.LoadAsync<T>(s),
            _ => throw new ArgumentException("id")
        };
    }

    [Fact]
    public async Task Importador_generico_con_el_adaptador_del_portal_dry_run_corrida_idempotente_version_nueva_aceptacion_y_sincronizador_que_abre_la_revalidacion()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(Seccion: "FICHA_GENERAL")]);
        var f = NuevoFixturePortal();

        // La empresa ya tenía al jurídico como vínculo (sin aceptar): el importador lo encuentra por su NIT y no lo duplica.
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correo, f.NitJ);
        var constructoras = new[] { new UnidadEmpresaDto("7", norte.Datos.Id) };

        // Validaciones de la entrada.
        var plataforma = new Credencial(ApiFixture.ClavePlataforma);
        (await CodigoAsync(await ApiAsync("POST", RutaImportacion, plataforma, new ImportarTercerosDto(Fuente: "Excel"), 422))).ShouldBe("fuente-invalida");
        (await CodigoAsync(await ApiAsync("POST", RutaImportacion, plataforma, new ImportarTercerosDto(Fuente: "Archivo"), 422))).ShouldBe("archivo-requerido");
        (await CodigoAsync(await ApiAsync("POST", RutaImportacion, plataforma, new ImportarTercerosDto(Fuente: "Conectado"), 409)))
            .ShouldBe("origen-no-configurado");
        // Un sistema de origen sin adaptador registrado no existe: el núcleo funciona igual sin él.
        (await CodigoAsync(await ApiAsync("POST", "/api/v1/plataforma/importacion/erp-desconocido", plataforma,
            new ImportarTercerosDto(Archivo: JsonDocument.Parse(f.Json()).RootElement.Clone()), 404))).ShouldBe("origen-no-registrado");
        (await CodigoAsync(await ApiAsync("POST", RutaImportacion, plataforma,
            new ImportarTercerosDto(Archivo: JsonDocument.Parse(f.Json()).RootElement.Clone(), Unidades: [new UnidadEmpresaDto("7", Guid.NewGuid())]), 422)))
            .ShouldBe("empresa-no-encontrada");
        await ApiAsync("POST", RutaImportacion, Admin(norte), new ImportarTercerosDto(Fuente: "Archivo"), 403);

        // 1. Dry-run: reporta lo que haría y no escribe nada.
        var simulada = await ImportarPortalAsync(f.Json(), dryRun: true, constructoras);
        (simulada.Origen, simulada.DryRun, simulada.Lotes, simulada.Cursor).ShouldBe(("portal", true, 2, (string?)f.TeridE.ToString(System.Globalization.CultureInfo.InvariantCulture)));
        (simulada.Resumen.TercerosLeidos, simulada.Resumen.TercerosCreados, simulada.Resumen.TercerosEncontrados, simulada.Resumen.TercerosRechazados)
            .ShouldBe((3, 2, 1, 0));
        Seccion(simulada, "FICHA_GENERAL").Creadas.ShouldBe(3);
        Seccion(simulada, "FICHA_FINANCIERA").Creadas.ShouldBe(2);
        simulada.Resumen.Aceptaciones.ShouldBe(7); // el jurídico: general, tributaria, legal, financiera 2024 y 2025, SST y especialidades
        simulada.Resumen.SinVinculo.ShouldBe(1); // la natural estaba Completada en la constructora 7, pero Norte no la tiene vinculada
        simulada.Resumen.Catalogos.Single(c => c.Catalogo == "especialidades").Creados.ShouldBe(4);
        simulada.Resumen.Rechazos.ShouldBeGreaterThan(0);
        (await CargarAsync<Tercero>(IdDeLegado.Tercero("portal", f.TeridN.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ShouldBeNull();
        (await CargarAsync<SeccionDeFicha>(SeccionDeFicha.IdPara(vinculo.TerceroId, "FICHA_GENERAL", null), "boveda")).ShouldBeNull();
        (await CargarAsync<EntradaDeCatalogo>("Especialidades|PE9301")).ShouldBeNull();

        // 2. Corrida real: crea los terceros nuevos (id UUID v5 de portal:{clave}) y la versión 1 de cada sección, con origen Importada.
        var real = await ImportarPortalAsync(f.Json(), dryRun: false, constructoras);
        (real.Resumen.TercerosCreados, real.Resumen.TercerosEncontrados, real.Resumen.TercerosRechazados).ShouldBe((2, 1, 0));
        Seccion(real, "FICHA_GENERAL").ShouldBe(new ResumenSeccionImportadaDto("FICHA_GENERAL", 3, 0, 0));
        Seccion(real, "FICHA_TRIBUTARIA").Creadas.ShouldBe(2);
        (real.Resumen.Aceptaciones, real.Resumen.SinVinculo).ShouldBe((7, 1));
        (await CargarAsync<EntradaDeCatalogo>("Especialidades|PE9301")).ShouldNotBeNull().Padre.ShouldBe("PC9201");

        var natural = (await CargarAsync<Tercero>(IdDeLegado.Tercero("portal", f.TeridN.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ShouldNotBeNull();
        (natural.ClaveIdentificacion, natural.TipoPersona, natural.Nombre).ShouldBe(($"CO|CC|{f.CcN}", TipoPersona.Natural, "María López Díaz"));
        // La clave del tercero en el sistema de origen queda como su referencia externa de la plataforma (tenant técnico «boveda»).
        (await CargarAsync<ReferenciaDeTercero>($"PORTAL|{f.TeridN}", "boveda")).ShouldNotBeNull().TerceroId.ShouldBe(natural.Id);
        var juridico = (await CargarAsync<Tercero>(vinculo.TerceroId)).ShouldNotBeNull();
        (await CargarAsync<ReferenciaDeTercero>($"PORTAL|{f.TeridJ}", "boveda")).ShouldNotBeNull().TerceroId.ShouldBe(juridico.Id);
        // La referencia de la plataforma no la ve ningún tenant: viven en espacios distintos.
        (await CargarAsync<ReferenciaDeTercero>($"PORTAL|{f.TeridJ}", norte.Datos.TenantId.ToString("N"))).ShouldBeNull();
        (await CargarAsync<Tercero>(IdDeLegado.Tercero("portal", f.TeridJ.ToString(System.Globalization.CultureInfo.InvariantCulture)))).ShouldBeNull(); // no se duplicó con otro id
        real.Resumen.DetalleAdvertencias.ShouldContain(a => a.Clave == f.TeridJ.ToString(System.Globalization.CultureInfo.InvariantCulture) && a.Codigo == "dv-recalculado" && a.Dato == "0");
        real.Resumen.DetalleAdvertencias.ShouldContain(a => a.Clave == f.TeridJ.ToString(System.Globalization.CultureInfo.InvariantCulture) && a.Codigo == "persona-duplicada");

        // Lo importado del jurídico en Tercero 360: versión 1 de origen Importada, aceptada por importación (sin expediente).
        var ficha = await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(norte), null, 200);
        ficha.Secciones!.Select(s => (s.Seccion, s.Periodo, s.Version, s.Origen)).ShouldBe(
        [
            ("FICHA_GENERAL", (int?)null, (int?)1, "Importada"), ("FICHA_TRIBUTARIA", null, 1, "Importada"), ("FICHA_LEGAL", null, 1, "Importada"),
            ("FICHA_FINANCIERA", 2025, 1, "Importada"), ("FICHA_FINANCIERA", 2024, 1, "Importada"), ("FICHA_SST", null, 1, "Importada"),
            ("FICHA_ESPECIALIDADES", null, 1, "Importada")
        ]);
        ficha.Secciones!.ShouldAllBe(s => s.Completitud!.Completa);
        var general = ficha.Secciones!.Single(s => s.Seccion == "FICHA_GENERAL");
        (general.Datos!.Value.GetProperty("dv").GetString(), general.Datos.Value.GetProperty("ciudad").GetString()).ShouldBe(
            (Validasoft.Dominio.Identificaciones.DigitoVerificacion.NitColombia(f.NitJ), "11001"));
        var aceptada = Aceptacion(ficha, "FICHA_GENERAL");
        (aceptada.VersionAceptada, aceptada.ExpedienteId, aceptada.Folio, aceptada.Origen, aceptada.VersionNuevaPendiente).ShouldBe(
            ((int?)1, (Guid?)null, (string?)null, "Importacion", false));
        ficha.Secciones!.Single(s => s.Seccion == "FICHA_TRIBUTARIA").Datos!.Value.GetProperty("responsabilidades").EnumerateArray()
            .Select(x => x.GetString()).ShouldBe(["05", "13", "48", "52"]);

        // CSV de rechazos: solo el tercero con errores (ciudad sin DANE, correo y teléfono inválidos, fecha y porcentaje de texto).
        var csv = await (await ApiAsync("GET", $"{RutaImportacion}/{real.Id}/rechazos", plataforma, null, 200)).ReadAsTextAsync();
        var filas = csv.TrimStart('﻿').Split("\r\n", StringSplitOptions.RemoveEmptyEntries);
        filas[0].ShouldBe("claveLegado,clave,seccion,periodo,campo,codigo,motivo,dato,accionSugerida");
        filas.Skip(1).ShouldAllBe(x => x.StartsWith($"portal:{f.TeridE},{f.TeridE},", StringComparison.Ordinal));
        var codigos = filas.Skip(1).Select(x => x.Split(',')[5]).ToList();
        codigos.ShouldContain("ciudad-sin-dane");
        codigos.ShouldContain("fecha-invalida");
        codigos.ShouldContain("porcentaje-invalido");
        codigos.Count(c => c == "valor-invalido").ShouldBe(2); // correo y teléfono
        filas.Length.ShouldBe(real.Resumen.Rechazos + 1);

        // 3. Segunda corrida igual: no crea nada (misma huella) ni vuelve a aceptar.
        var otra = await ImportarPortalAsync(f.Json(), dryRun: false, constructoras);
        (otra.Resumen.TercerosCreados, otra.Resumen.TercerosEncontrados).ShouldBe((0, 3));
        otra.Resumen.Secciones.ShouldAllBe(s => s.Creadas == 0);
        Seccion(otra, "FICHA_GENERAL").SinCambios.ShouldBe(3);
        (otra.Resumen.Aceptaciones, otra.Resumen.AceptacionesExistentes).ShouldBe((0, 7));
        otra.Resumen.Catalogos.ShouldAllBe(c => c.Creados == 0 && c.Actualizados == 0);

        // 4. Cambia el teléfono del jurídico en el Portal ⇒ versión 2 solo de su general; lo aceptado sigue en la 1.
        var cambio = await ImportarPortalAsync(f.Json(telefono: "6017778899"), dryRun: false, constructoras);
        Seccion(cambio, "FICHA_GENERAL").ShouldBe(new ResumenSeccionImportadaDto("FICHA_GENERAL", 1, 2, 0));
        cambio.Resumen.Secciones.Where(s => s.Seccion != "FICHA_GENERAL").ShouldAllBe(s => s.Creadas == 0);
        var trasCambio = Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(norte), null, 200),
            "FICHA_GENERAL");
        (trasCambio.VersionAceptada, trasCambio.VersionNuevaPendiente, trasCambio.Origen).ShouldBe(((int?)1, true, "Importacion"));

        // 5. El proveedor acepta la invitación: el expediente toma la general vigente (v2) y Norte la aprueba.
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var expedienteId = await AbiertoEnAsync(norte, vinculo.Id);
        var expediente = await EsperarAsync(norte, expedienteId, x => x.Estado is "EnRevision" or "Aprobado");
        if (expediente.Estado == "EnRevision")
        {
            var area = expediente.Etapas.Single().Areas.Single().AreaId;
            (await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{area}/aprobar-como-administrador", Admin(norte),
                new JustificacionDto("Migrado del Portal."), 200)).Estado.ShouldBe("Aprobado");
        }

        var aprobada = Aceptacion(await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(norte), null, 200),
            "FICHA_GENERAL");
        (aprobada.VersionAceptada, aprobada.Origen, aprobada.ExpedienteId).ShouldBe(((int?)2, "Expediente", (Guid?)expedienteId));

        // 6. Coexistencia: el Portal guarda una dirección nueva y llama al sincronizador ⇒ versión 3 con origen SistemaExterno ⇒ la
        //    revalidación de F2 se abre solo con la general.
        var rutaSincronizar = $"{RutaImportacion}/terceros/{f.TeridJ}/sincronizar";
        await File.WriteAllTextAsync(fixture.ArchivoPortal, f.Json(telefono: "6017778899", direccion: "Carrera 7 # 80-12"));
        var sincronizado = await ApiAsync<SincronizacionDto>("POST", rutaSincronizar, plataforma, null, 200);
        (sincronizado.TerceroId, sincronizado.TerceroCreado, sincronizado.ClaveLegado).ShouldBe(((Guid?)vinculo.TerceroId, false, $"portal:{f.TeridJ}"));
        sincronizado.Origen.ShouldBe("portal");
        sincronizado.Secciones.Where(s => s.Creada).Select(s => (s.Seccion, s.Version)).ShouldBe([("FICHA_GENERAL", (int?)3)]);

        var actualizacionId = await AbiertoEnAsync(norte, vinculo.Id, expedienteId);
        var actualizacion = await EsperarAsync(norte, actualizacionId, x => x.Estado == "EnRevision");
        actualizacion.Tipo.ShouldBe("Actualizacion");
        var requisito = actualizacion.Requisitos!.ShouldHaveSingleItem();
        (requisito.TipoDocumento.Codigo, requisito.VersionActual!.N, requisito.Seccion!.VersionAceptada).ShouldBe(("FICHA_GENERAL", 3, (int?)2));
        requisito.Seccion.Cambios.ShouldBe([new CambioDeCampoDto("direccion", "Dirección", "Calle 45 # 12-08", "Carrera 7 # 80-12", "Modificado")]);
        (await ApiAsync<FichaTerceroDto>("GET", $"/api/v1/terceros/{vinculo.TerceroId}/ficha", Admin(norte), null, 200)).Secciones!
            .Single(s => s.Seccion == "FICHA_GENERAL").Origen.ShouldBe("SistemaExterno");

        // Sincronizar otra vez sin cambios no hace nada; un Terid que el Portal no tiene es 404.
        (await ApiAsync<SincronizacionDto>("POST", rutaSincronizar, plataforma, null, 200)).Secciones.ShouldAllBe(s => !s.Creada);
        (await CodigoAsync(await ApiAsync("POST", $"{RutaImportacion}/terceros/1/sincronizar", plataforma, null, 404))).ShouldBe("tercero-origen-no-encontrado");
        (await CodigoAsync(await ApiAsync("POST", $"{RutaImportacion}/{real.Id}/reanudar", plataforma, null, 409))).ShouldBe("corrida-completada");
    }

    [Fact]
    public async Task El_importador_trae_la_imagen_de_la_firma_si_su_huella_coincide_y_si_no_importa_la_declaracion_sin_imagen_con_aviso()
    {
        var baseFixture = NuevoFixturePortal();
        var catalogos = (JsonSerializer.Deserialize<Validasoft.Adaptadores.Portal.ArchivoDelPortal>(baseFixture.Json(), Validasoft.Adaptadores.Portal.OrigenPortalArchivo.Opciones)!.Catalogos ?? Validasoft.Adaptadores.Portal.CatalogosDelPortal.Vacios)
            // Sin los catálogos de especialidades (grupos, categorías y especialidades, que comparten el catálogo global «especialidades»): la prueba del
            // importador genérico cuenta cuántas entradas crea («Creados» = 4) y que aún no existen; si esta corrida real se adelanta (el orden de las
            // pruebas no está fijado), esa prueba vería menos.
            with { Grupos = null, Categorias = null, Especialidades = null };
        var imagen = Convert.FromBase64String(FirmaPngBase64());
        var huella = Convert.ToHexStringLower(System.Security.Cryptography.SHA256.HashData(imagen));

        Validasoft.Adaptadores.Portal.TerceroDelPortal Natural(int terid, string cc, byte[]? firma, string hash) => new(new Validasoft.Adaptadores.Portal.TercerosPortal(terid, 3),
            new Validasoft.Adaptadores.Portal.TerInformacionGeneral(2, "María", "López Díaz", "N", "CC", cc, null, "maria@correo.co", 2, "Carrera 7 # 1-2", 11, "3001234567", null, false, null, "Colombia"),
            InfoTributaria: new Validasoft.Adaptadores.Portal.TerInfoTributaria(),
            CamaraComercio: [new Validasoft.Adaptadores.Portal.TerCamaraComercio(1, 1, "CC", cc, "María", "López", TerCamEsPep: true, TerCamVinculoPep: false, TerFechaExpedicion: "20/05/2001", TerLugarExpedicion: "Medellín")],
            Siso: new Validasoft.Adaptadores.Portal.TerSISO(),
            DeclaracionesPep:
            [
                new Validasoft.Adaptadores.Portal.TerDeclaracionPep(TerCamId: 1, RelacionEmpresa: 1, Nacionalidad: "Colombia", Direccion: "Carrera 7 # 1-2", CargoDesempenado: "Alcalde municipal",
                    Institucion: "Alcaldía de Sopó", PaisInstitucion: "Colombia", FechaVinculacion: new DateTime(2018, 1, 1), FechaDesvinculacion: new DateTime(2022, 1, 1), AceptaJuramento: true,
                    FirmaEnCalidad: 1, FirmanteNombre: "María López Díaz", FirmanteTipoDocumento: "CC", FirmanteNumeroDocumento: cc, FechaFirma: new DateTime(2026, 10, 5), FirmaHash: hash,
                    FirmaRuta: @"C:\inetpub\Portal\DeclaracionesPEP\1\1\firma.png", FirmaImagen: firma)
            ]);

        var (teridBueno, teridMalo) = (baseFixture.TeridJ + 10, baseFixture.TeridJ + 11);
        var (ccBueno, ccMalo) = (Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture),
            Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture));
        var json = JsonSerializer.Serialize(new Validasoft.Adaptadores.Portal.ArchivoDelPortal(catalogos,
            [Natural(teridBueno, ccBueno, imagen, huella), Natural(teridMalo, ccMalo, imagen, new string('b', 64))]), Validasoft.Adaptadores.Portal.OrigenPortalArchivo.Opciones);

        var corrida = await ImportarPortalAsync(json, dryRun: false);

        // Las dos declaraciones se importan (ninguna se rechaza); la de la huella que no coincide, sin imagen y con un aviso visible en la corrida.
        (corrida.Resumen.TercerosCreados, corrida.Resumen.TercerosRechazados).ShouldBe((2, 0));
        string Clave(int terid) => terid.ToString(System.Globalization.CultureInfo.InvariantCulture);
        corrida.Resumen.DetalleAdvertencias.Where(a => a.Clave == Clave(teridBueno) && a.Codigo.StartsWith("firma-", StringComparison.Ordinal)).ShouldBeEmpty();
        var aviso = corrida.Resumen.DetalleAdvertencias.Single(a => a.Clave == Clave(teridMalo) && a.Codigo == "firma-huella-no-coincide");
        (aviso.Seccion, aviso.Campo).ShouldBe(("FICHA_GENERAL", "declaracion_pep.firma_png"));

        async Task<JsonElement> DeclaracionImportadaAsync(int terid)
        {
            var tercero = (await CargarAsync<Tercero>(IdDeLegado.Tercero("portal", Clave(terid)))).ShouldNotBeNull();
            var seccion = (await CargarAsync<SeccionDeFicha>(SeccionDeFicha.IdPara(tercero.Id, "FICHA_GENERAL", null), "boveda")).ShouldNotBeNull();
            return seccion.Vigente!.Datos.GetProperty("declaracion_pep");
        }

        var bueno = await DeclaracionImportadaAsync(teridBueno);
        bueno.GetProperty("firma_png").GetString().ShouldBe(FirmaPngBase64());
        bueno.GetProperty("firma_huella").GetString().ShouldBe(huella);
        bueno.GetProperty("fecha_firma").GetString().ShouldBe("2026-10-05");
        var malo = await DeclaracionImportadaAsync(teridMalo);
        malo.TryGetProperty("firma_png", out _).ShouldBeFalse();
        malo.GetProperty("firma_huella").GetString().ShouldBe(new string('b', 64)); // la del Portal: allá estaba firmada
        malo.GetProperty("cargo").GetString().ShouldBe("Alcalde municipal");
    }
}
