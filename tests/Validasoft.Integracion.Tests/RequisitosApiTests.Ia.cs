using System.Collections.Concurrent;
using System.Text.Json;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Validasoft.Aplicacion.Ia;
using Validasoft.Contratos;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Ia;
using Wolverine;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Analizador falso de las pruebas (nunca se llama a OpenAI). Responde según la llave de la empresa que paga: con
/// <c>-falla-</c> lanza la falla controlada de un HTTP 500; con <c>-baja-</c> el modelo por defecto devuelve confianza baja
/// (y el de escalamiento, alta); si no, hechos de un RUT con confianza alta. Si el archivo trae marcas <c>[[clave=valor]]</c>,
/// responde esos campos (como el analizador simulado de la API, F5). Cuenta las llamadas por llave.
/// </summary>
public sealed class AnalizadorFalso : IAnalizadorDeDocumentos
{
    public const string NitExtraido = "900123456";

    private readonly ConcurrentQueue<(string Llave, SolicitudDeAnalisis Solicitud)> _llamadas = new();

    public IReadOnlyList<SolicitudDeAnalisis> LlamadasCon(string llave) => [.. _llamadas.Where(l => l.Llave == llave).Select(l => l.Solicitud)];

    public Task<ResultadoDeAnalisis> AnalizarAsync(SolicitudDeAnalisis solicitud, string apiKey, CancellationToken ct)
    {
        _llamadas.Enqueue((apiKey, solicitud));
        if (apiKey.Contains("-falla-", StringComparison.Ordinal))
            throw new AnalisisDeIaException("http-500", "OpenAI respondió 500.");

        var confianza = apiKey.Contains("-baja-", StringComparison.Ordinal) && solicitud.Modelo == PlanDeIaEmpresa.ModeloPorDefectoInicial ? 0.5m : 0.97m;
        // F5: un archivo con marcas [[clave=valor]] responde lo que dicen (el mismo lector del analizador simulado).
        if (Validasoft.Infraestructura.IA.AnalizadorSimulado.Marcas(solicitud.Contenido).Count > 0)
        {
            var simulados = Validasoft.Infraestructura.IA.AnalizadorSimulado.Hechos(EsquemasDeAnalisis.Para(solicitud.TipoCanonico)!, solicitud.Contenido);
            return Task.FromResult(new ResultadoDeAnalisis(solicitud.Modelo, simulados with { Confianza = confianza }, new UsoDeTokens(1000, 200, 300),
                solicitud.Paginas, 0));
        }

        var campos = new Dictionary<string, JsonElement>
        {
            ["nit"] = JsonSerializer.SerializeToElement(NitExtraido),
            ["razonSocial"] = JsonSerializer.SerializeToElement("Aceros del Norte S.A.S.")
        };
        return Task.FromResult(new ResultadoDeAnalisis(solicitud.Modelo, new HechosExtraidos(solicitud.TipoCanonico, true, confianza, [], null, campos),
            new UsoDeTokens(1000, 200, 300), solicitud.Paginas, 0));
    }
}

public sealed partial class RequisitosApiTests
{
    // ---------- IA con OpenAI y cupo por empresa (ADR-0012) ----------

    /// <summary>Costo de una llamada del falso con gpt-6-luna: (800 × 0,10 + 200 × 0,01 + 300 × 0,50) / 1M.</summary>
    private const decimal CostoLuna = 0.000232m;

    [Fact]
    public async Task Con_plan_el_RUT_se_analiza_una_vez_y_cada_empresa_del_tenant_que_lo_recibe_consume_una_validacion()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var sur = await OtraEmpresaDelTenantAsync(norte, "Sur S.A.S.", "SUR", "899999068");
        var llave = LlaveIa();
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(10, false, ApiKeyOpenAi: llave));
        await PlanIaAsync(sur, new ActualizarPlanIaEmpresaDto(10, false)); // sin llave: recibe el análisis pero no paga llamadas
        var (vinculos, sesion) = await TerceroConEmpresasAsync(norte, sur);
        var (vinculoNorte, vinculoSur) = (vinculos[0], vinculos[1]);

        var rut = await SubirAsync(sesion, "RUT", Pdf("RUT"));
        rut.Version.Hechos!.ConIa.ShouldBeFalse(); // el tercero nunca ve el análisis
        await AceptarAsync(sesion, vinculoNorte.Id);

        var deNorte = await EsperarVersionAsync(norte, vinculoNorte.Id, "RUT", 1, v => v.Hechos!.ConIa);
        deNorte.Hechos!.PendienteRevisionHumana.ShouldBeFalse();
        var ia = deNorte.Hechos.Ia.ShouldNotBeNull();
        ia.Modelo.ShouldBe("gpt-6-luna");
        ia.Escalado.ShouldBeFalse();
        ia.TipoDetectado.ShouldBe("RUT");
        ia.Confianza.ShouldBe(0.97m);
        ia.Campos["nit"].GetString().ShouldBe(AnalizadorFalso.NitExtraido);

        await AceptarAsync(sesion, vinculoSur.Id);
        var deSur = await EsperarVersionAsync(sur, vinculoSur.Id, "RUT", 1, v => v.Hechos!.ConIa);
        deSur.Hechos!.Ia!.Campos["nit"].GetString().ShouldBe(AnalizadorFalso.NitExtraido);

        // Una sola llamada a OpenAI, con la llave de Norte; cada empresa consumió una validación incluida.
        fixture.Analizador.LlamadasCon(llave).ShouldHaveSingleItem().TipoCanonico.ShouldBe("RUT");
        var usoNorte = await UsoIaAsync(norte);
        (usoNorte.IncluidasConsumidas, usoNorte.AdicionalesConsumidas, usoNorte.IncluidasRestantes, usoNorte.AnalisisPagados).ShouldBe((1, 0, 9, 1));
        (usoNorte.TokensEntrada, usoNorte.TokensEntradaEnCache, usoNorte.TokensSalida, usoNorte.CostoUsd).ShouldBe((1000L, 200L, 300L, CostoLuna));
        var dia = usoNorte.PorDia.ShouldHaveSingleItem();
        (dia.Incluidas, dia.AnalisisPagados, dia.CostoUsd).ShouldBe((1, 1, CostoLuna));
        usoNorte.Plan.LlaveConfigurada.ShouldBeTrue();

        // Costo y tokens solo en la empresa que pagó la llamada.
        var usoSur = await UsoIaAsync(sur);
        (usoSur.IncluidasConsumidas, usoSur.AnalisisPagados, usoSur.TokensEntrada, usoSur.CostoUsd).ShouldBe((1, 0, 0L, 0m));
        usoSur.Plan.LlaveConfigurada.ShouldBeFalse();

        // Repetir el mensaje no vuelve a llamar ni a cobrar (idempotente).
        await AsentarIaAsync(rut.Version.DocumentoId, 1);
        fixture.Analizador.LlamadasCon(llave).Count.ShouldBe(1);
        (await UsoIaAsync(sur)).IncluidasConsumidas.ShouldBe(1);
    }

    [Fact]
    public async Task Sin_plan_o_sin_cupo_ni_adicional_la_empresa_no_ve_los_hechos_de_IA_y_no_consume()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var sur = await OtraEmpresaDelTenantAsync(norte, "Sur S.A.S.", "SUR", "899999068");
        var oriente = await OtraEmpresaDelTenantAsync(norte, "Oriente S.A.S.", "ORIENTE", "860001022");
        var llave = LlaveIa();
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(5, false, ApiKeyOpenAi: llave));
        await PlanIaAsync(sur, new ActualizarPlanIaEmpresaDto(0, false, ApiKeyOpenAi: LlaveIa())); // con llave pero sin cupo
        var (vinculos, sesion) = await TerceroConEmpresasAsync(norte, sur, oriente);
        var (vinculoNorte, vinculoSur, vinculoOriente) = (vinculos[0], vinculos[1], vinculos[2]);

        var rut = await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await AceptarAsync(sesion, vinculoSur.Id);
        await AceptarAsync(sesion, vinculoOriente.Id);
        await AceptarAsync(sesion, vinculoNorte.Id);

        await EsperarVersionAsync(norte, vinculoNorte.Id, "RUT", 1, v => v.Hechos!.ConIa);
        await AsentarIaAsync(rut.Version.DocumentoId, 1);

        foreach (var (empresa, vinculo) in new[] { (sur, vinculoSur), (oriente, vinculoOriente) })
        {
            var version = await EsperarVersionAsync(empresa, vinculo.Id, "RUT", 1, _ => true);
            version.Hechos!.ConIa.ShouldBeFalse();
            version.Hechos.Ia.ShouldBeNull();
            version.Hechos.PendienteRevisionHumana.ShouldBeTrue();
            version.Hechos.Comparacion.ShouldBe("SinAnterior");
            var uso = await UsoIaAsync(empresa);
            (uso.IncluidasConsumidas, uso.AdicionalesConsumidas, uso.AnalisisPagados).ShouldBe((0, 0, 0));
        }

        (await UsoIaAsync(oriente)).Plan.Configurado.ShouldBeFalse();
        fixture.Analizador.LlamadasCon(llave).Count.ShouldBe(1);
        (await UsoIaAsync(norte)).IncluidasConsumidas.ShouldBe(1);
    }

    [Fact]
    public async Task Agotadas_las_incluidas_se_cobran_adicionales_hasta_el_tope_y_despues_la_version_queda_sin_IA()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var llave = LlaveIa();
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(1, true, TopeAdicionalMes: 1, ApiKeyOpenAi: llave));
        await FlujoConAsync(norte, "CLIENTE",
        [
            new RequisitoDelFlujoDto(TipoDocumento: "RUT"), new RequisitoDelFlujoDto(TipoDocumento: "CAMARA_COMERCIO"),
            new RequisitoDelFlujoDto(TipoDocumento: "CERTIFICACION_BANCARIA")
        ]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);

        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await EsperarVersionAsync(norte, vinculo.Id, "RUT", 1, v => v.Hechos!.ConIa);
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        await EsperarVersionAsync(norte, vinculo.Id, "CAMARA_COMERCIO", 1, v => v.Hechos!.ConIa);
        var certificacion = await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación"));
        await AsentarIaAsync(certificacion.Version.DocumentoId, 1);

        (await EsperarVersionAsync(norte, vinculo.Id, "CERTIFICACION_BANCARIA", 1, _ => true)).Hechos!.ConIa.ShouldBeFalse();
        fixture.Analizador.LlamadasCon(llave).Select(s => s.TipoCanonico).ShouldBe(["RUT", "CAMARA_COMERCIO"]);
        var uso = await UsoIaAsync(norte);
        (uso.IncluidasConsumidas, uso.AdicionalesConsumidas, uso.IncluidasRestantes, uso.AdicionalesRestantes, uso.AnalisisPagados)
            .ShouldBe((1, 1, 0, (int?)0, 2));
        uso.CostoUsd.ShouldBe(2 * CostoLuna);
    }

    [Fact]
    public async Task Con_confianza_baja_se_escala_al_otro_modelo_suma_ambos_costos_y_la_version_nueva_se_compara_sin_otra_llamada()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var llave = LlaveIa("baja");
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(10, false, ApiKeyOpenAi: llave));
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);

        await SubirAsync(sesion, "RUT", Pdf("RUT v1"));
        var v1 = await EsperarVersionAsync(norte, vinculo.Id, "RUT", 1, v => v.Hechos!.ConIa);
        v1.Hechos!.Ia!.Escalado.ShouldBeTrue();
        v1.Hechos.Ia.Modelo.ShouldBe("gpt-6.1-terra");
        v1.Hechos.Ia.Confianza.ShouldBe(0.97m);
        v1.Hechos.PendienteRevisionHumana.ShouldBeFalse();
        fixture.Analizador.LlamadasCon(llave).Select(s => s.Modelo).ShouldBe(["gpt-6-luna", "gpt-6.1-terra"]);
        // Terra: (800 × 1 + 200 × 0,05 + 300 × 5) / 1M; el uso suma las dos llamadas y cuenta un solo análisis.
        var uso = await UsoIaAsync(norte);
        (uso.AnalisisPagados, uso.IncluidasConsumidas, uso.TokensEntrada, uso.CostoUsd).ShouldBe((1, 1, 2000L, CostoLuna + 0.00231m));

        await SubirAsync(sesion, "RUT", Pdf("RUT v2"));
        var v2 = await EsperarVersionAsync(norte, vinculo.Id, "RUT", 2, v => v.Hechos!.ConIa);
        v2.Hechos!.Comparacion.ShouldBe("SinCambios");
        v2.Hechos.CamposCambiados.ShouldBeEmpty();
        (await UsoIaAsync(norte)).IncluidasConsumidas.ShouldBe(2);
    }

    [Fact]
    public async Task Si_el_analizador_falla_la_version_queda_sin_IA_para_revision_humana_y_no_se_cobra_la_validacion()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var llave = LlaveIa("falla");
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(10, false, ApiKeyOpenAi: llave));
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);

        var rut = await SubirAsync(sesion, "RUT", Pdf("RUT"));
        var documentoId = rut.Version.DocumentoId;
        await EsperarAsync(async () => (await DocumentoDeBovedaAsync(documentoId))?.VersionRequerida(1).FalloIa is not null, "el fallo de IA");
        // La validación se consumió antes de llamar y se devuelve al fallar.
        await EsperarAsync(async () => (await UsoIaAsync(norte)).IncluidasConsumidas == 0, "la devolución de la validación");

        fixture.Analizador.LlamadasCon(llave).Count.ShouldBe(1);
        var fallo = (await EventosDeBovedaAsync(documentoId)).OfType<AnalisisDeIaFallido>().ShouldHaveSingleItem();
        fallo.Motivo.ShouldBe("http-500");
        fallo.EmpresaQuePago.ShouldBe(norte.Datos.Id);
        var version = await EsperarVersionAsync(norte, vinculo.Id, "RUT", 1, _ => true);
        version.Hechos!.ConIa.ShouldBeFalse();
        version.Hechos.PendienteRevisionHumana.ShouldBeTrue();
        var uso = await UsoIaAsync(norte);
        (uso.AnalisisPagados, uso.CostoUsd).ShouldBe((0, 0m));

        // Fail-closed: no se reintenta (ni consume) aunque el mensaje se repita.
        await AsentarIaAsync(documentoId, 1);
        fixture.Analizador.LlamadasCon(llave).Count.ShouldBe(1);
        (await UsoIaAsync(norte)).IncluidasConsumidas.ShouldBe(0);
    }

    [Fact]
    public async Task Al_llegar_al_80_por_ciento_de_las_incluidas_se_avisa_a_los_administradores_de_la_empresa()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var correoAdmin = $"admin.{Guid.NewGuid():N}@norte.co";
        await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", new Credencial(norte.Admin),
            new CrearUsuarioDto("Admin Norte", correoAdmin, null, [new RolAsignadoDto("Administrador", norte.Datos.Id)], null), 201);
        var llave = LlaveIa();
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(5, false, ApiKeyOpenAi: llave));
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);

        for (var n = 1; n <= 4; n++)
        {
            await SubirAsync(sesion, "RUT", Pdf($"RUT v{n}"));
            await EsperarVersionAsync(norte, vinculo.Id, "RUT", n, v => v.Hechos!.ConIa);
        }

        var aviso = await fixture.Correos.EsperarAsync(correoAdmin, m => m.Asunto.Contains("IA", StringComparison.Ordinal));
        aviso.Asunto.ShouldContain("80 %");
        aviso.Texto.ShouldContain("4 de 5");
        fixture.Correos.Para(correoAdmin).Count(m => m.Asunto.Contains("IA", StringComparison.Ordinal)).ShouldBe(1);
        (await UsoIaAsync(norte)).IncluidasRestantes.ShouldBe(1);
    }

    [Fact]
    public async Task Plataforma_configura_el_plan_y_la_llave_nunca_vuelve_ni_queda_en_la_auditoria()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        var otra = await EmpresaNuevaAsync("Otra S.A.S.", "OTRA");
        var llave = LlaveIa("secreta");
        var url = $"/api/v1/plataforma/tenants/{norte.Datos.TenantId}/empresas/{norte.Datos.Id}/plan-ia";
        var plataforma = new Credencial(ApiFixture.ClavePlataforma);

        var inicial = await ApiAsync<PlanIaEmpresaDto>("GET", url, plataforma, null, 200);
        (inicial.Configurado, inicial.Activo, inicial.LlaveConfigurada, inicial.ModeloPorDefecto, inicial.UmbralConfianza)
            .ShouldBe((false, false, false, "gpt-6-luna", 0.85m));

        var creado = await ApiAsync("PUT", url, plataforma, new ActualizarPlanIaEmpresaDto(200, true, 50, llave), 200);
        var texto = await creado.ReadAsTextAsync();
        texto.ShouldNotContain(llave);
        var plan = JsonSerializer.Deserialize<PlanIaEmpresaDto>(texto, JsonSerializerOptions.Web)!;
        (plan.Configurado, plan.LlaveConfigurada, plan.UltimosCuatro, plan.ValidacionesIncluidasMes, plan.TopeAdicionalMes, plan.ModeloEscalamiento)
            .ShouldBe((true, true, llave[^4..], 200, (int?)50, "gpt-6.1-terra"));

        // Nulo conserva la llave; vacío la quita.
        (await ApiAsync<PlanIaEmpresaDto>("PUT", url, plataforma, new ActualizarPlanIaEmpresaDto(300, false, UmbralConfianza: 0.9m), 200))
            .ShouldSatisfyAllConditions(p => p.LlaveConfigurada.ShouldBeTrue(), p => p.ValidacionesIncluidasMes.ShouldBe(300),
                p => p.UmbralConfianza.ShouldBe(0.9m));
        (await (await ApiAsync("GET", url, plataforma, null, 200)).ReadAsTextAsync()).ShouldNotContain(llave);
        (await ApiAsync<PlanIaEmpresaDto>("PUT", url, plataforma, new ActualizarPlanIaEmpresaDto(300, false, ApiKeyOpenAi: ""), 200))
            .LlaveConfigurada.ShouldBeFalse();

        // La empresa ve su plan y su uso sin la llave; la auditoría registra los tres cambios sin ella.
        var uso = await ApiAsync("GET", "/api/v1/ia/uso", Admin(norte), null, 200);
        (await uso.ReadAsTextAsync()).ShouldNotContain(llave);
        var resumen = (await uso.ReadAsJsonAsync<UsoIaDto>())!;
        resumen.Mes.ShouldBe(UsoMensualIa.MesDe(CalendarioDeIa.Dia(fixture.Reloj.GetUtcNow(), "America/Bogota")));
        (resumen.Plan.Configurado, resumen.Plan.ValidacionesIncluidasMes, resumen.IncluidasRestantes, resumen.AdicionalesRestantes)
            .ShouldBe((true, 300, 300, (int?)0));
        (await ApiAsync<UsoIaDto>("GET", "/api/v1/ia/uso?mes=2026-01", Admin(norte), null, 200)).Mes.ShouldBe("2026-01");
        var auditoria = await ApiAsync("GET", $"/api/v1/auditoria?entidad={PlanIaEndpoints.Entidad}", Admin(norte), null, 200);
        var registros = (await auditoria.ReadAsJsonAsync<PaginaDto<RegistroAuditoriaDto>>())!.Datos;
        registros.Select(r => r.Accion).ShouldBe(["Modificado", "Modificado", "Creado"]);
        registros.ShouldAllBe(r => r.Actor.Tipo == "Plataforma" && r.EmpresaId == norte.Datos.Id);
        JsonSerializer.Serialize(registros).ShouldNotContain(llave);

        // Validaciones, empresa de otro tenant, credencial sin el permiso de Plataforma y mes inválido.
        (await CodigoAsync(await ApiAsync("PUT", url, plataforma, new ActualizarPlanIaEmpresaDto(-1, false), 422))).ShouldBe("validaciones-incluidas-invalidas");
        (await CodigoAsync(await ApiAsync("PUT", url, plataforma, new ActualizarPlanIaEmpresaDto(1, false, UmbralConfianza: 2m), 422)))
            .ShouldBe("umbral-confianza-invalido");
        (await CodigoAsync(await ApiAsync("PUT", url, plataforma, new ActualizarPlanIaEmpresaDto(1, false, ApiKeyOpenAi: "corta"), 422))).ShouldBe("api-key-invalida");
        (await CodigoAsync(await ApiAsync("GET", $"/api/v1/plataforma/tenants/{norte.Datos.TenantId}/empresas/{otra.Datos.Id}/plan-ia", plataforma, null, 404)))
            .ShouldBe("empresa-no-encontrada");
        await ApiAsync("PUT", url, Admin(norte), new ActualizarPlanIaEmpresaDto(1, false), 403);
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/ia/uso?mes=2026-13", Admin(norte), null, 422))).ShouldBe("mes-invalido");
    }

    // ---------- preparación ----------

    /// <summary>Llave de prueba única (≥ 20 caracteres); el marcador cambia lo que responde el analizador falso.</summary>
    private static string LlaveIa(string marcador = "ok") => $"sk-prueba-{marcador}-{Guid.NewGuid():N}";

    private Task<PlanIaEmpresaDto> PlanIaAsync(Empresa empresa, ActualizarPlanIaEmpresaDto plan) =>
        ApiAsync<PlanIaEmpresaDto>("PUT", $"/api/v1/plataforma/tenants/{empresa.Datos.TenantId}/empresas/{empresa.Datos.Id}/plan-ia",
            new Credencial(ApiFixture.ClavePlataforma), plan, 200);

    private Task<UsoIaDto> UsoIaAsync(Empresa empresa) => ApiAsync<UsoIaDto>("GET", "/api/v1/ia/uso", Admin(empresa), null, 200);

    /// <summary>Otra empresa del mismo tenant (misma llave de administración y sistema), con sus textos legales publicados.</summary>
    private async Task<Empresa> OtraEmpresaDelTenantAsync(Empresa primera, string nombre, string prefijo, string nit)
    {
        var datos = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(primera.Admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", nit), nombre, prefijo, null, null), 201);
        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{datos.Id}/textos-legales/autorizacion", new Credencial(primera.Admin),
            new PublicarTextoLegalDto($"Autorizo a {nombre}"), 200);
        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{datos.Id}/textos-legales/terminos", new Credencial(primera.Admin),
            new PublicarTextoLegalDto($"Términos de {nombre}"), 200);
        return primera with { Datos = datos };
    }

    /// <summary>
    /// Un tercero (persona jurídica) invitado como CLIENTE por varias empresas cuyos flujos piden el RUT, y su sesión (que
    /// ya incluye todos sus vínculos).
    /// </summary>
    private async Task<(IReadOnlyList<VinculoDto> Vinculos, SesionTerceroDto Sesion)> TerceroConEmpresasAsync(params Empresa[] empresas)
    {
        var correo = CorreoNuevo();
        var nit = Nit();
        var vinculos = new List<VinculoDto>();
        foreach (var empresa in empresas)
        {
            await FlujoConAsync(empresa, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT")]);
            vinculos.Add(await CrearVinculoAsync(empresa, "CLIENTE", correo, nit));
        }

        return (vinculos, await SesionAsync(correo, empresas[0].Datos.Nombre));
    }

    /// <summary>El análisis llega por la cola <c>ia</c>: espera (hasta 20 s) a que la versión, vista por la empresa, cumpla la condición.</summary>
    private async Task<VersionDocumentoDto> EsperarVersionAsync(Empresa empresa, Guid vinculoId, string codigo, int numero,
        Func<VersionDocumentoDto, bool> condicion)
    {
        VersionDocumentoDto? version = null;
        await EsperarAsync(async () =>
        {
            var documentos = await ApiAsync<List<DocumentoVistaEmpresaDto>>("GET", $"/api/v1/vinculos/{vinculoId}/documentos", Admin(empresa), null, 200);
            version = documentos.FirstOrDefault(d => d.Tipo.Codigo == codigo)?.VersionesCompartidas.FirstOrDefault(v => v.N == numero);
            return version is not null && condicion(version);
        }, $"la versión {numero} de {codigo}");
        return version!;
    }

    private static async Task EsperarAsync(Func<Task<bool>> condicion, string que)
    {
        var limite = DateTime.UtcNow.AddSeconds(20);
        while (!await condicion())
        {
            if (DateTime.UtcNow > limite)
                throw new TimeoutException($"No se cumplió a tiempo: {que}.");
            await Task.Delay(100);
        }
    }

    /// <summary>Ejecuta el manejador en línea (idempotente): deja asentado lo que la cola aún estuviera procesando.</summary>
    private async Task AsentarIaAsync(Guid documentoId, int numero)
    {
        using var alcance = Host.Services.CreateScope();
        await alcance.ServiceProvider.GetRequiredService<IMessageBus>().InvokeAsync(new AnalizarVersionConIa(documentoId, numero));
    }

    private async Task<Documento?> DocumentoDeBovedaAsync(Guid documentoId)
    {
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession("boveda");
        return await consulta.LoadAsync<Documento>(documentoId);
    }

    private async Task<IReadOnlyList<object>> EventosDeBovedaAsync(Guid documentoId)
    {
        await using var consulta = Host.Services.GetRequiredService<IDocumentStore>().QuerySession("boveda");
        return [.. (await consulta.Events.FetchStreamAsync(documentoId)).Select(e => e.Data)];
    }
}
