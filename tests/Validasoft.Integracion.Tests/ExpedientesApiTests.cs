using System.Net;
using System.Security.Claims;
using System.Security.Cryptography;
using System.Text.Json;
using System.Text.RegularExpressions;
using Alba;
using Microsoft.IdentityModel.JsonWebTokens;
using Microsoft.IdentityModel.Tokens;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Flujos aprobatorios y expedientes (H1-flujos): áreas en paralelo con revisores delegados, observación que frena todo,
/// correo al tercero sin revisores, reenvío, aprobación que activa el vínculo, áreas automáticas, rechazo terminal,
/// aceptación sin flujo, apertura manual, pertenencia al área, aprobadores distintos y folios concurrentes.
/// </summary>
[Collection("api")]
[Trait("Requiere", "Docker")]
public sealed partial class ExpedientesApiTests(ApiFixture fixture)
{
    private static readonly string[] ScopesDeSistema =
    [
        "configuracion:leer", "vinculos:leer", "flujos:leer", "expedientes:leer", "expedientes:revisar", "expedientes:administrar"
    ];

    private IAlbaHost Host => fixture.Host;

    private CorreosCapturados Correos => fixture.Correos;

    [GeneratedRegex(@"/e/([A-Za-z0-9_-]{43})")]
    private static partial Regex RegexToken();

    private sealed record Persona(UsuarioDto Usuario, string Sub);

    private sealed record Escenario(
        string Admin, EmpresaDto Empresa, string LlaveSrm, RSA ClaveSrm, AreaDto Aprobador, AreaDto Cumplimiento,
        Persona Laura, Persona Carlos, Persona Pedro, Persona Ajeno);

    // ---------- flujo principal ----------

    [Fact]
    public async Task Dos_areas_en_paralelo_observacion_reenvio_y_aprobacion_activan_el_vinculo()
    {
        var e = await PrepararAsync();
        await CrearFlujoAsync(e, "CLIENTE", [new EtapaDto(1, [new AreaDeEtapaDto(e.Aprobador.Id), new AreaDeEtapaDto(e.Cumplimiento.Id)])]);
        await PublicarTextosAsync(e);

        // El tercero acepta la invitación ⇒ expediente EnRevision y vínculo EnValidacion en la misma transacción.
        var correoTercero = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", correoTercero);
        var sesion = await AceptarInvitacionAsync(vinculo, correoTercero);
        var leido = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200);
        leido.Estado.ShouldBe("EnValidacion");
        leido.AperturaDeExpedientePendiente.ShouldBeFalse();
        var expedienteId = leido.ExpedienteAbiertoId.ShouldNotBeNull();

        var expediente = await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expedienteId}", Admin(e), null, 200);
        expediente.Estado.ShouldBe("EnRevision");
        expediente.Folio.ShouldBe($"NORTE-E-{DateTimeOffset.UtcNow.Year}-000001");
        expediente.Flujo!.Origen.ShouldBe("PorDefecto");
        expediente.Etapas.ShouldHaveSingleItem().Areas.Select(a => a.Estado).ShouldBe(["Pendiente", "Pendiente"]);

        // Ambas áreas lo ven en su bandeja al mismo tiempo; cada persona solo ve la suya.
        var deLaura = await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e, e.Laura), null, 200);
        var deCarlos = await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e, e.Carlos), null, 200);
        var filaLaura = deLaura.Datos.ShouldHaveSingleItem();
        var filaCarlos = deCarlos.Datos.ShouldHaveSingleItem();
        filaLaura.AreaCodigo.ShouldBe("APROBADOR_INICIAL");
        filaCarlos.AreaCodigo.ShouldBe("OFICIAL_CUMPLIMIENTO");
        filaLaura.ExpedienteId.ShouldBe(expedienteId);
        filaCarlos.ExpedienteId.ShouldBe(expedienteId);
        filaLaura.EstadoArea.ShouldBe("Pendiente");
        filaLaura.Desde.ShouldBe(filaCarlos.Desde);
        filaLaura.Aprobaciones.ShouldBe(0);
        filaLaura.AprobadoresRequeridos.ShouldBe(1);

        // Cumplimiento observa ⇒ todo se frena: el aprobador inicial recibe 409.
        var observado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Cumplimiento.Id}/veredicto",
            Delegado(e, e.Carlos), new VeredictoDto("Observado", ["incompleto"], "Falta el RUT actualizado."), 200);
        observado.Estado.ShouldBe("Observado");
        var frenado = await ApiAsync("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Aprobador.Id}/veredicto",
            Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 409);
        (await CodigoAsync(frenado)).ShouldBe("expediente-observado");

        // Correo de observación al tercero: motivos y observación, sin revisores ni áreas internas.
        var correo = await Correos.EsperarAsync(correoTercero, m => m.Asunto.Contains("observaciones"));
        correo.Asunto.ShouldContain(expediente.Folio);
        correo.Texto.ShouldContain("Falta el RUT actualizado.");
        correo.Texto.ShouldContain("La información o el documento está incompleto.");
        foreach (var prohibido in new[] { "Carlos", e.Carlos.Usuario.Correo, "Cumplimiento", "OFICIAL_CUMPLIMIENTO" })
        {
            correo.Texto.ShouldNotContain(prohibido);
            correo.Html.ShouldNotContain(prohibido);
        }

        // El tercero ve la observación (sin revisores) y reenvía.
        var publico = await TerceroAsync("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200);
        var textoPublico = await publico.ReadAsTextAsync();
        textoPublico.ShouldNotContain("Carlos");
        textoPublico.ShouldNotContain(e.Carlos.Usuario.Id.ToString());
        var vista = JsonSerializer.Deserialize<ExpedientePublicoDto>(textoPublico, new JsonSerializerOptions(JsonSerializerDefaults.Web))!;
        vista.Estado.ShouldBe("Observado");
        vista.PuedeReenviar.ShouldBeTrue();
        var observacion = vista.Observaciones.ShouldHaveSingleItem();
        observacion.Observacion.ShouldBe("Falta el RUT actualizado.");
        observacion.Motivos.ShouldHaveSingleItem().Codigo.ShouldBe("incompleto");

        var reenviado = await TerceroAsync<ExpedientePublicoDto>("POST", $"/public/v1/expedientes/{expedienteId}/reenviar", sesion.AccessToken,
            new ReenviarExpedienteDto("Adjunto el RUT de octubre."), 200);
        reenviado.Estado.ShouldBe("EnRevision");
        reenviado.Observaciones.ShouldBeEmpty();

        // Ambas aprueban ⇒ expediente Aprobado y vínculo Activo.
        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Aprobador.Id}/veredicto",
            Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 200);
        var aprobado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Cumplimiento.Id}/veredicto",
            Delegado(e, e.Carlos), new VeredictoDto("Aprobado"), 200);
        aprobado.Estado.ShouldBe("Aprobado");
        aprobado.Reenvios.ShouldBe(1);
        aprobado.UltimaRespuesta.ShouldBe("Adjunto el RUT de octubre.");

        var activo = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200);
        activo.Estado.ShouldBe("Activo");
        activo.ExpedienteAbiertoId.ShouldBeNull();

        var historial = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/expedientes/{expedienteId}/historial", Admin(e), null, 200);
        historial.Select(h => h.Tipo).ShouldBe(
        [
            "expediente_abierto", "flujo_seleccionado", "expediente_enviado_a_revision", "etapa_activada", "veredicto_de_area_emitido",
            "expediente_observado", "expediente_reenviado", "veredicto_de_area_emitido", "veredicto_de_area_emitido", "etapa_aprobada",
            "expediente_aprobado"
        ]);
        historial[0].ActorTipo.ShouldBe("Tercero");
        historial[4].ActorTipo.ShouldBe("Usuario");
        historial[4].ActorId.ShouldBe(e.Carlos.Usuario.Id.ToString());
        historial[6].ActorTipo.ShouldBe("Tercero");

        var delVinculo = await ApiAsync<List<EventoHistorialDto>>("GET", $"/api/v1/vinculos/{vinculo.Id}/historial", Admin(e), null, 200);
        delVinculo.Select(h => h.Tipo).TakeLast(3).ShouldBe(["expediente_vinculado", "expediente_cerrado", "vinculo_activado"]);

        (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e, e.Laura), null, 200)).Datos.ShouldBeEmpty();
    }

    // ---------- automáticas, rechazo, apertura ----------

    [Fact]
    public async Task Un_flujo_con_area_automatica_se_aprueba_solo()
    {
        var e = await PrepararAsync();
        var automatica = await ApiAsync<AreaDto>("POST", "/api/v1/areas", Admin(e), new CrearAreaDto("VALIDACION_AUTO", "Validación automática"), 201);
        var flujo = await CrearFlujoAsync(e, "EMPLEADO", [new EtapaDto(1, [new AreaDeEtapaDto(automatica.Id, Automatica: true)])]);

        var vinculo = await CrearVinculoAsync(e, "EMPLEADO", CorreoNuevo(), flujo.Id);
        vinculo.FlujoId.ShouldBe(flujo.Id);
        var expediente = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);

        expediente.Estado.ShouldBe("Aprobado");
        expediente.Flujo!.Origen.ShouldBe("Explicito");
        var area = expediente.Etapas.ShouldHaveSingleItem().Areas.ShouldHaveSingleItem();
        area.Estado.ShouldBe("Aprobada");
        area.Veredictos.ShouldHaveSingleItem().Origen.ShouldBe("Sistema");
        (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).Estado.ShouldBe("Activo");
    }

    [Fact]
    public async Task El_rechazo_de_cumplimiento_es_terminal_y_devuelve_el_vinculo_a_invitado()
    {
        var e = await PrepararAsync();
        await CrearFlujoAsync(e, "CLIENTE",
            [new EtapaDto(1, [new AreaDeEtapaDto(e.Aprobador.Id), new AreaDeEtapaDto(e.Cumplimiento.Id, PuedeRechazarExpediente: true)])]);
        var correoTercero = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", correoTercero);
        var expediente = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);

        // El aprobador inicial no puede rechazar; cumplimiento sí, con motivos y justificación.
        var noPuede = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/rechazar", Delegado(e, e.Laura),
            new RechazarExpedienteDto(e.Aprobador.Id, ["no-corresponde-al-tercero"], "No es el proveedor."), 403);
        (await CodigoAsync(noPuede)).ShouldBe("area-no-puede-rechazar");
        var sinJustificacion = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/rechazar", Delegado(e, e.Carlos),
            new RechazarExpedienteDto(e.Cumplimiento.Id, ["no-corresponde-al-tercero"], " "), 422);
        (await CodigoAsync(sinJustificacion)).ShouldBe("justificacion-requerida");

        var rechazado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/rechazar", Delegado(e, e.Carlos),
            new RechazarExpedienteDto(e.Cumplimiento.Id, ["no-corresponde-al-tercero"], "Aparece en listas restrictivas."), 200);
        rechazado.Estado.ShouldBe("Rechazado");
        rechazado.Rechazo!.Justificacion.ShouldBe("Aparece en listas restrictivas.");

        // El tercero se entera por correo con el texto del motivo; la justificación es interna.
        var aviso = await Correos.EsperarAsync(correoTercero, m => m.Asunto.Contains("sin aprobarla"));
        aviso.Texto.ShouldContain("El documento no corresponde a la persona o empresa que se está validando.");
        aviso.Texto.ShouldNotContain("listas restrictivas");

        var despues = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura),
            new VeredictoDto("Aprobado"), 409);
        (await CodigoAsync(despues)).ShouldBe("estado-invalido");
        await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/areas/{e.Aprobador.Id}/anular-veredicto", Admin(e),
            new AnularVeredictoDto("Revertir el rechazo"), 409);

        var leido = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200);
        leido.Estado.ShouldBe("Invitado");
        leido.ExpedienteAbiertoId.ShouldBeNull();
    }

    [Fact]
    public async Task Aceptar_sin_flujo_configurado_no_falla_y_queda_pendiente_hasta_la_apertura_manual()
    {
        var e = await PrepararAsync();
        await PublicarTextosAsync(e);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", correo);
        await AceptarInvitacionAsync(vinculo, correo);

        var pendiente = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200);
        pendiente.Estado.ShouldBe("Invitado");
        pendiente.InvitacionAceptadaEn.ShouldNotBeNull();
        pendiente.AperturaDeExpedientePendiente.ShouldBeTrue();
        pendiente.ExpedienteAbiertoId.ShouldBeNull();
        (await ApiAsync<List<VinculoDto>>("GET", "/api/v1/bandeja/aperturas-pendientes", Admin(e), null, 200))
            .Select(v => v.Id).ShouldBe([vinculo.Id]);

        var sinFlujo = await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 422);
        (await CodigoAsync(sinFlujo)).ShouldBe("falta-flujo-por-defecto");

        // La empresa configura el flujo y el administrador abre el expediente a mano.
        await CrearFlujoAsync(e, "CLIENTE", null);
        var abierto = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        abierto.Estado.ShouldBe("EnRevision");
        abierto.Etapas.ShouldHaveSingleItem().Areas.Select(a => a.Codigo).ShouldBe(["APROBADOR_INICIAL", "OFICIAL_CUMPLIMIENTO"]);
        abierto.Etapas[0].Areas[0].MiembrosPendientes.ShouldBe([e.Laura.Usuario.Id, e.Pedro.Usuario.Id], ignoreOrder: true);

        var leido = await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200);
        leido.Estado.ShouldBe("EnValidacion");
        leido.AperturaDeExpedientePendiente.ShouldBeFalse();
        (await ApiAsync<List<VinculoDto>>("GET", "/api/v1/bandeja/aperturas-pendientes", Admin(e), null, 200)).ShouldBeEmpty();

        // Un solo expediente abierto por vínculo.
        var otro = await ApiAsync("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 409);
        (await CodigoAsync(otro)).ShouldBe("expediente-abierto-existente");
    }

    [Fact]
    public async Task Dos_areas_que_aprueban_al_mismo_tiempo_no_reciben_412()
    {
        var e = await PrepararAsync();
        await CrearFlujoAsync(e, "CLIENTE", [new EtapaDto(1, [new AreaDeEtapaDto(e.Aprobador.Id), new AreaDeEtapaDto(e.Cumplimiento.Id)])]);

        // Varias rondas para que los dos veredictos coincidan de verdad en el servidor (FL-12: bloqueo del stream sin If-Match).
        for (var ronda = 0; ronda < 5; ronda++)
        {
            var vinculo = await CrearVinculoAsync(e, "CLIENTE", CorreoNuevo());
            var expediente = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);

            await Task.WhenAll(
                ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/areas/{e.Aprobador.Id}/veredicto", Delegado(e, e.Laura),
                    new VeredictoDto("Aprobado"), 200),
                ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos),
                    new VeredictoDto("Aprobado"), 200));

            (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expediente.Id}", Admin(e), null, 200)).Estado.ShouldBe("Aprobado");
            (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).Estado.ShouldBe("Activo");
        }
    }

    // ---------- pertenencia y aprobadores ----------

    [Fact]
    public async Task Solo_los_miembros_revisan_y_el_mismo_usuario_no_cuenta_como_dos_aprobadores()
    {
        var e = await PrepararAsync();
        await CrearFlujoAsync(e, "CLIENTE", [new EtapaDto(1, [new AreaDeEtapaDto(e.Aprobador.Id, AprobadoresRequeridos: 2)])]);
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", CorreoNuevo());
        var expediente = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        var url = $"/api/v1/expedientes/{expediente.Id}/areas/{e.Aprobador.Id}/veredicto";

        var ajeno = await ApiAsync("POST", url, Delegado(e, e.Ajeno), new VeredictoDto("Aprobado"), 403);
        (await CodigoAsync(ajeno)).ShouldBe("no-es-miembro-del-area");
        (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e, e.Ajeno), null, 200)).Datos.ShouldBeEmpty();

        var sinPersona = await ApiAsync("POST", url, Admin(e), new VeredictoDto("Aprobado"), 422);
        (await CodigoAsync(sinPersona)).ShouldBe("usuario-delegado-requerido");

        var una = await ApiAsync<ExpedienteDto>("POST", url, Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 200);
        var area = una.Etapas[0].Areas[0];
        area.Aprobaciones.ShouldBe(1);
        area.AprobacionesFaltantes.ShouldBe(1);
        area.MiembrosPendientes.ShouldBe([e.Pedro.Usuario.Id]);

        var repetida = await ApiAsync("POST", url, Delegado(e, e.Laura), new VeredictoDto("Aprobado"), 409);
        (await CodigoAsync(repetida)).ShouldBe("aprobador-repetido");
        var fila = (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e, e.Pedro), null, 200)).Datos.ShouldHaveSingleItem();
        fila.Aprobaciones.ShouldBe(1);

        var dos = await ApiAsync<ExpedienteDto>("POST", url, Delegado(e, e.Pedro), new VeredictoDto("Aprobado"), 200);
        dos.Estado.ShouldBe("Aprobado");
    }

    // ---------- motivos, versiones y vínculo terminado ----------

    [Fact]
    public async Task Motivos_propios_versiones_del_flujo_y_anulacion_por_vinculo_terminado()
    {
        var e = await PrepararAsync();
        var motivo = await ApiAsync<MotivoTipificadoDto>("POST", "/api/v1/motivos", Admin(e),
            new CrearMotivoDto("Poliza-Sin-Firma", "Póliza sin firma", "Documento", "Observacion", "La póliza no tiene la firma del tomador."), 201);
        motivo.Codigo.ShouldBe("poliza-sin-firma");
        var base_ = await ApiAsync("POST", "/api/v1/motivos", Admin(e), new CrearMotivoDto("vencido", "x", "Otro", "Ambos", "x"), 409);
        (await CodigoAsync(base_)).ShouldBe("motivo-duplicado");
        var motivos = await ApiAsync<List<MotivoTipificadoDto>>("GET", "/api/v1/motivos", Admin(e), null, 200);
        motivos.Count(m => m.Base).ShouldBe(8);
        motivos.ShouldContain(m => m.Codigo == "poliza-sin-firma" && !m.Base);

        // Editar un flujo publicado crea la versión 2 en borrador; la 1 sigue publicada.
        var flujo = await CrearFlujoAsync(e, "CLIENTE", null);
        var editado = await ApiAsync<FlujoDto>("PUT", $"/api/v1/flujos/{flujo.Id}", Admin(e),
            new EditarFlujoDto(PoliticaDevolucion: "AlCerrarArea"), 200);
        editado.Publicada!.Version.ShouldBe(1);
        editado.Borrador!.Version.ShouldBe(2);
        editado.Borrador.PoliticaDevolucion.ShouldBe("AlCerrarArea");
        editado.EsPorDefecto.ShouldBeTrue();
        (await ApiAsync<List<FlujoVersionDto>>("GET", $"/api/v1/flujos/{flujo.Id}/versiones", Admin(e), null, 200))
            .Select(v => v.Estado).ShouldBe(["Publicada", "Borrador"]);
        (await ApiAsync<List<FlujoPorDefectoDto>>("GET", "/api/v1/flujos/por-defecto", Admin(e), null, 200))
            .ShouldHaveSingleItem().FlujoId.ShouldBe(flujo.Id);

        // El motivo propio se usa al observar (el flujo abierto sigue con la versión 1: política inmediata).
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", CorreoNuevo());
        var expediente = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201);
        expediente.PoliticaDevolucion.ShouldBe("Inmediata");
        var invalido = await ApiAsync("POST", $"/api/v1/expedientes/{expediente.Id}/areas/{e.Cumplimiento.Id}/veredicto", Delegado(e, e.Carlos),
            new VeredictoDto("Observado", ["no-existe"]), 422);
        (await CodigoAsync(invalido)).ShouldBe("motivo-invalido");
        var observado = await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expediente.Id}/areas/{e.Cumplimiento.Id}/veredicto",
            Delegado(e, e.Carlos), new VeredictoDto("Observado", ["poliza-sin-firma"]), 200);
        observado.Estado.ShouldBe("Observado");

        // Desvincular anula el expediente abierto en la misma transacción.
        await ApiAsync<VinculoDto>("POST", $"/api/v1/vinculos/{vinculo.Id}/desvincular", Admin(e), new MotivoDto("Ya no es cliente"), 200);
        (await ApiAsync<ExpedienteDto>("GET", $"/api/v1/expedientes/{expediente.Id}", Admin(e), null, 200)).Estado.ShouldBe("Anulado");
        (await ApiAsync<PaginaDto<FilaBandejaDto>>("GET", "/api/v1/bandeja", Delegado(e, e.Carlos), null, 200)).Datos.ShouldBeEmpty();
    }

    // ---------- folios ----------

    [Fact]
    public async Task Los_folios_son_consecutivos_aunque_las_aperturas_sean_concurrentes()
    {
        var e = await PrepararAsync();
        await CrearFlujoAsync(e, "CLIENTE", null);
        var vinculos = new List<VinculoDto>();
        for (var i = 0; i < 6; i++)
            vinculos.Add(await CrearVinculoAsync(e, "CLIENTE", CorreoNuevo()));

        var abiertos = await Task.WhenAll(vinculos.Select(v =>
            ApiAsync<ExpedienteDto>("POST", $"/api/v1/vinculos/{v.Id}/expedientes", Admin(e), new AbrirExpedienteDto(), 201)));

        var anio = DateTimeOffset.UtcNow.Year;
        abiertos.Select(x => x.Folio).ShouldBe(Enumerable.Range(1, 6).Select(n => $"NORTE-E-{anio}-{n:D6}"), ignoreOrder: true);
    }

    [Fact]
    public async Task El_correo_de_observacion_trae_un_enlace_que_abre_la_sesion_del_tercero()
    {
        var e = await PrepararAsync();
        await CrearFlujoAsync(e, "CLIENTE", [new EtapaDto(1, [new AreaDeEtapaDto(e.Cumplimiento.Id)])]);
        await PublicarTextosAsync(e);
        var correoTercero = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e, "CLIENTE", correoTercero);
        await AceptarInvitacionAsync(vinculo, correoTercero);
        var expedienteId = (await ApiAsync<VinculoDto>("GET", $"/api/v1/vinculos/{vinculo.Id}", Admin(e), null, 200)).ExpedienteAbiertoId!.Value;

        await ApiAsync<ExpedienteDto>("POST", $"/api/v1/expedientes/{expedienteId}/areas/{e.Cumplimiento.Id}/veredicto",
            Delegado(e, e.Carlos), new VeredictoDto("Observado", ["incompleto"], "Falta el RUT actualizado."), 200);

        // El correo trae un enlace nuevo de un solo uso (antes solo decía «ingresa con el enlace de tu invitación»).
        var correo = await Correos.EsperarAsync(correoTercero, m => m.Asunto.Contains("observaciones"));
        correo.Texto.ShouldNotContain("enlace de tu invitación");
        correo.Html.ShouldContain($"{ApiFixture.UrlPublica}/e/");
        var token = RegexToken().Match(correo.Texto).Groups[1].Value;
        token.Length.ShouldBe(43);

        var canje = await PublicoAsync("POST", $"/public/v1/enlaces/{token}/canjear", null, 200);
        var sesion = (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;
        sesion.Vinculos.ShouldHaveSingleItem().Id.ShouldBe(vinculo.Id);
        canje.Context.Response.Headers.SetCookie.ToString().ShouldContain("vs_tercero=");
        var vista = await TerceroAsync<ExpedientePublicoDto>("GET", $"/public/v1/expedientes/{expedienteId}", sesion.AccessToken, null, 200);
        vista.Estado.ShouldBe("Observado");
        vista.PuedeReenviar.ShouldBeTrue();

        // De un solo uso.
        (await CodigoAsync(await PublicoAsync("POST", $"/public/v1/enlaces/{token}/canjear", null, 410))).ShouldBe("enlace-usado");
    }

    // ---------- preparación ----------

    private async Task<Escenario> PrepararAsync()
    {
        var slug = "t" + Guid.NewGuid().ToString("N")[..10];
        var tenant = await ApiAsync<TenantCreadoDto>("POST", "/api/v1/plataforma/tenants", new Credencial(ApiFixture.ClavePlataforma),
            new CrearTenantDto(slug, "Grupo Norte", "co", "America/Bogota"), 201);
        var admin = tenant.ApiKeyAdministrador.Valor!;
        var empresa = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "800197268-4"), "Norte S.A.S.", "NORTE", null, null), 201);
        var credencialAdmin = new Credencial(admin, empresa.Id);

        var rsa = RSA.Create(2048);
        var sistema = await ApiAsync<SistemaIntegradoDto>("POST", "/api/v1/sistemas", new Credencial(admin),
            new CrearSistemaIntegradoDto("SRM", "SRM del cliente", rsa.ExportSubjectPublicKeyInfoPem()), 201);
        var llave = await ApiAsync<ApiKeyDto>("POST", "/api/v1/api-keys", new Credencial(admin),
            new CrearApiKeyDto("Integración SRM", ScopesDeSistema, null, null, SistemaId: sistema.Id), 201);

        async Task<Persona> PersonaAsync(string nombre, string rol)
        {
            var correo = $"{nombre.Split(' ')[0].ToLowerInvariant()}.{Guid.NewGuid():N}@constructora.co";
            var usuario = await ApiAsync<UsuarioDto>("POST", "/api/v1/usuarios", new Credencial(admin),
                new CrearUsuarioDto(nombre, correo, null, [new RolAsignadoDto(rol, empresa.Id)], null), 201);
            return new Persona(usuario, "srm-" + Guid.NewGuid().ToString("N")[..8]);
        }

        var laura = await PersonaAsync("Laura Aprobadora", "Revisor");
        var pedro = await PersonaAsync("Pedro Aprobador", "Revisor");
        var carlos = await PersonaAsync("Carlos Cumplimiento", "OficialCumplimiento");
        var ajeno = await PersonaAsync("Andres Ajeno", "Revisor");

        var aprobador = await ApiAsync<AreaDto>("POST", "/api/v1/areas", credencialAdmin, new CrearAreaDto("APROBADOR_INICIAL", "Aprobador inicial"), 201);
        var cumplimiento = await ApiAsync<AreaDto>("POST", "/api/v1/areas", credencialAdmin,
            new CrearAreaDto("OFICIAL_CUMPLIMIENTO", "Oficial de cumplimiento"), 201);
        await ApiAsync<AreaDto>("PUT", $"/api/v1/areas/{aprobador.Id}/miembros", credencialAdmin,
            new MiembrosAreaDto([laura.Usuario.Id, pedro.Usuario.Id]), 200);
        await ApiAsync<AreaDto>("PUT", $"/api/v1/areas/{cumplimiento.Id}/miembros", credencialAdmin, new MiembrosAreaDto([carlos.Usuario.Id]), 200);

        return new Escenario(admin, empresa, llave.Valor!, rsa, aprobador, cumplimiento, laura, carlos, pedro, ajeno);
    }

    /// <summary>Crea y publica el flujo (el primero publicado de su tipo queda por defecto). Sin etapas: todas las áreas en paralelo.</summary>
    private async Task<FlujoDto> CrearFlujoAsync(Escenario e, string tipo, IReadOnlyList<EtapaDto>? etapas)
    {
        var flujo = await ApiAsync<FlujoDto>("POST", "/api/v1/flujos", Admin(e),
            new CrearFlujoDto("HAB_" + tipo + "_" + Guid.NewGuid().ToString("N")[..4].ToUpperInvariant(), "Habilitación " + tipo, tipo, etapas), 201);
        var publicado = await ApiAsync<FlujoDto>("POST", $"/api/v1/flujos/{flujo.Id}/publicar", Admin(e), new { }, 200);
        publicado.Publicada.ShouldNotBeNull();
        return publicado;
    }

    private async Task PublicarTextosAsync(Escenario e)
    {
        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{e.Empresa.Id}/textos-legales/autorizacion", new Credencial(e.Admin),
            new PublicarTextoLegalDto("Autorizo el tratamiento de mis datos por Norte S.A.S."), 200);
        await ApiAsync<TextoLegalDto>("PUT", $"/api/v1/empresas/{e.Empresa.Id}/textos-legales/terminos", new Credencial(e.Admin),
            new PublicarTextoLegalDto("Términos de Norte"), 200);
    }

    private Task<VinculoDto> CrearVinculoAsync(Escenario e, string tipo, string correo, Guid? flujoId = null)
    {
        var nit = "9" + Random.Shared.Next(10_000_000, 99_999_999).ToString(System.Globalization.CultureInfo.InvariantCulture);
        return ApiAsync<VinculoDto>("POST", "/api/v1/vinculos", Admin(e), new CrearVinculoDto(
            TerceroId: null,
            Tercero: new CrearTerceroDto(new IdentificacionDto("CO", "NIT", nit), "Juridica", "Tercero " + nit, null,
                [new ContactoDto("Ana Gómez", correo, null, "Comercial")]),
            TipoVinculo: tipo,
            Atributos: null,
            CorreoInvitacion: correo,
            ReferenciaExterna: null,
            FlujoId: flujoId), 201);
    }

    /// <summary>Canjea el enlace del correo de invitación y acepta los textos vigentes; devuelve la sesión del tercero.</summary>
    private async Task<SesionTerceroDto> AceptarInvitacionAsync(VinculoDto vinculo, string correo)
    {
        var invitacion = await Correos.EsperarAsync(correo);
        var token = RegexToken().Match(invitacion.Texto).Groups[1].Value;
        var canje = await PublicoAsync("POST", $"/public/v1/enlaces/{token}/canjear", null, 200);
        var sesion = (await canje.ReadAsJsonAsync<SesionTerceroDto>())!;

        var textos = await TerceroAsync<InvitacionPublicaDto>("GET", $"/public/v1/invitaciones/{vinculo.Id}", sesion.AccessToken, null, 200);
        var aceptada = await TerceroAsync<InvitacionAceptadaDto>("POST", $"/public/v1/invitaciones/{vinculo.Id}/aceptar", sesion.AccessToken,
            new AceptarInvitacionDto([.. textos.TextosPorAceptar.Select(t => new ConsentimientoDto(t.Tipo, t.TextoVersion, true))]), 200);
        aceptada.Vinculo.InvitacionAceptada.ShouldBeTrue();
        return sesion;
    }

    // ---------- utilidades HTTP ----------

    private sealed record Credencial(string ApiKey, Guid? Empresa = null, string? Delegado = null);

    private static Credencial Admin(Escenario e) => new(e.Admin, e.Empresa.Id);

    /// <summary>La persona actúa desde el SRM con una aserción firmada por la clave privada del sistema (ADR-0011).</summary>
    private static Credencial Delegado(Escenario e, Persona p)
    {
        var ahora = DateTime.UtcNow;
        var asercion = new JsonWebTokenHandler().CreateToken(new SecurityTokenDescriptor
        {
            Issuer = "SRM",
            Audience = "validasoft",
            // jti de un solo uso (SE-4): cada petición lleva su propia aserción.
            Subject = new ClaimsIdentity([new Claim("sub", p.Sub), new Claim("name", p.Usuario.Nombre), new Claim("email", p.Usuario.Correo),
                new Claim("jti", Guid.NewGuid().ToString())]),
            IssuedAt = ahora.AddSeconds(-5),
            NotBefore = ahora.AddSeconds(-5),
            Expires = ahora.AddMinutes(2),
            SigningCredentials = new SigningCredentials(new RsaSecurityKey(e.ClaveSrm), SecurityAlgorithms.RsaSha256)
        });
        return new Credencial(e.LlaveSrm, e.Empresa.Id, asercion);
    }

    private static string CorreoNuevo() => $"t{Guid.NewGuid():N}@prueba.co";

    private static async Task<string?> CodigoAsync(IScenarioResult resultado) =>
        (await resultado.ReadAsJsonAsync<JsonElement>()).GetProperty("codigo").GetString();

    private async Task<T> ApiAsync<T>(string metodo, string url, Credencial credencial, object? cuerpo, int esperado) =>
        (await (await ApiAsync(metodo, url, credencial, cuerpo, esperado)).ReadAsJsonAsync<T>())!;

    private Task<IScenarioResult> ApiAsync(string metodo, string url, Credencial credencial, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("X-Api-Key", credencial.ApiKey);
            if (credencial.Empresa is not null)
                s.WithRequestHeader("X-Empresa", credencial.Empresa.Value.ToString());
            if (credencial.Delegado is not null)
                s.WithRequestHeader("X-Usuario-Delegado", credencial.Delegado);
            if (metodo == "POST")
                s.WithRequestHeader("Idempotency-Key", Guid.NewGuid().ToString());
            s.StatusCodeShouldBe(esperado);
        });

    private Task<IScenarioResult> PublicoAsync(string metodo, string url, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        });

    private Task<IScenarioResult> TerceroAsync(string metodo, string url, string token, object? cuerpo, int esperado) =>
        Host.Scenario(s =>
        {
            Preparar(s, metodo, url, cuerpo);
            s.WithRequestHeader("Authorization", "Bearer " + token);
            IpAleatoria(s);
            s.StatusCodeShouldBe(esperado);
        });

    private async Task<T> TerceroAsync<T>(string metodo, string url, string token, object? cuerpo, int esperado) =>
        (await (await TerceroAsync(metodo, url, token, cuerpo, esperado)).ReadAsJsonAsync<T>())!;

    private static void IpAleatoria(Scenario s) =>
        s.ConfigureHttpContext(c => c.Connection.RemoteIpAddress =
            new IPAddress([10, (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(256), (byte)Random.Shared.Next(1, 255)]));

    private static void Preparar(Scenario s, string metodo, string url, object? cuerpo)
    {
        switch (metodo)
        {
            case "GET":
                s.Get.Url(url);
                break;
            case "PUT":
                s.Put.Json(cuerpo ?? new { }).ToUrl(url);
                break;
            case "POST" when cuerpo is null:
                s.Post.Url(url);
                break;
            default:
                s.Post.Json(cuerpo!).ToUrl(url);
                break;
        }
    }
}
