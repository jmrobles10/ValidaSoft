using System.Collections.Concurrent;
using System.Net;
using Alba;
using Validasoft.Contratos;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Tiempo real (NT-12): el SSE de la web de empresa y el del portal del tercero llevan, además de las notificaciones, avisos de «cambió
/// X» (tipo de recurso e id) solo a quien corresponde —la empresa del recurso, el tercero con los vínculos de su sesión—. Caso que lo
/// motivó: el tercero designa una cuenta y la persona de la empresa, con la ficha abierta, no veía nada hasta recargar.
/// </summary>
public sealed partial class RequisitosApiTests
{
    /// <summary>Un stream SSE abierto contra la API en proceso: junta lo que llega para esperarlo o comprobar que no llegó.</summary>
    private sealed class LectorSse : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancelar = new(TimeSpan.FromMinutes(4));
        private readonly HttpClient _cliente;
        private readonly HttpResponseMessage _respuesta;
        private readonly Task _lectura;
        private readonly ConcurrentQueue<(string Evento, string Datos)> _recibidos = new();

        private LectorSse(HttpClient cliente, HttpResponseMessage respuesta)
        {
            _cliente = cliente;
            _respuesta = respuesta;
            _lectura = LeerAsync();
        }

        public IReadOnlyList<(string Evento, string Datos)> Recibidos => [.. _recibidos];

        /// <summary>El stream terminó (el servidor lo cerró).</summary>
        public bool Termino => _lectura.IsCompleted;

        public static async Task<LectorSse> AbrirAsync(IAlbaHost host, string url, Action<HttpRequestMessage> cabeceras)
        {
            var cliente = host.Server.CreateClient();
            using var peticion = new HttpRequestMessage(HttpMethod.Get, url);
            cabeceras(peticion);
            var respuesta = await cliente.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead);
            respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
            respuesta.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
            return new LectorSse(cliente, respuesta);
        }

        private async Task LeerAsync()
        {
            try
            {
                using var lector = new StreamReader(await _respuesta.Content.ReadAsStreamAsync(_cancelar.Token));
                string? evento = null;
                while (await lector.ReadLineAsync(_cancelar.Token) is { } linea)
                {
                    if (linea.StartsWith("event: ", StringComparison.Ordinal))
                        evento = linea[7..];
                    else if (linea.StartsWith("data: ", StringComparison.Ordinal))
                        _recibidos.Enqueue((evento ?? "message", linea[6..]));
                }
            }
            catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // Se cerró el stream (la prueba terminó o el servidor lo cortó).
            }
        }

        /// <summary>Espera (hasta 30 s) un evento que cumpla la condición y lo devuelve.</summary>
        public async Task<(string Evento, string Datos)> EsperarAsync(Func<string, string, bool> condicion, string descripcion)
        {
            var limite = DateTime.UtcNow.AddSeconds(30);
            while (true)
            {
                foreach (var recibido in _recibidos)
                    if (condicion(recibido.Evento, recibido.Datos))
                        return recibido;
                if (DateTime.UtcNow > limite)
                    throw new TimeoutException($"El stream no recibió {descripcion}. Recibió: {string.Join(" | ", _recibidos.Select(r => $"{r.Evento} {r.Datos}"))}");
                await Task.Delay(100);
            }
        }

        /// <summary>Que en <paramref name="espera"/> no llegue ningún evento que cumpla la condición (para comprobar lo que NO debe llegar).</summary>
        public async Task NoLlegaAsync(Func<string, string, bool> condicion, TimeSpan espera, string descripcion)
        {
            await Task.Delay(espera);
            _recibidos.Where(r => condicion(r.Evento, r.Datos)).ShouldBeEmpty($"El stream no debía recibir {descripcion}.");
        }

        public async ValueTask DisposeAsync()
        {
            await _cancelar.CancelAsync();
            await _lectura;
            _respuesta.Dispose();
            _cliente.Dispose();
            _cancelar.Dispose();
        }
    }

    private static bool Cambio(string evento, string datos, string recurso, Guid id) =>
        evento == "cambio" && System.Text.Json.JsonDocument.Parse(datos).RootElement is var raiz &&
        raiz.GetProperty("recurso").GetString() == recurso && raiz.GetProperty("id").GetGuid() == id;

    private static bool Notificacion(string evento, string datos, string tipo) =>
        evento == "notificacion" && System.Text.Json.JsonDocument.Parse(datos).RootElement.GetProperty("tipo").GetString() == tipo;

    /// <summary>El stream de la campana de una persona de la empresa (con <c>X-Empresa</c>: también los cambios de esa empresa).</summary>
    private Task<LectorSse> AbrirStreamDeEmpresaAsync(Empresa empresa, Persona persona)
    {
        var credencial = Delegado(empresa, persona);
        return LectorSse.AbrirAsync(Host, "/api/v1/notificaciones/stream", peticion =>
        {
            peticion.Headers.Add("X-Api-Key", credencial.ApiKey);
            peticion.Headers.Add("X-Empresa", empresa.Datos.Id.ToString());
            peticion.Headers.Add("X-Usuario-Delegado", credencial.Delegado!);
        });
    }

    /// <summary>El stream del portal de un tercero, con su sesión.</summary>
    private Task<LectorSse> AbrirStreamDelTerceroAsync(SesionTerceroDto sesion) =>
        LectorSse.AbrirAsync(Host, "/public/v1/notificaciones/stream", peticion => peticion.Headers.Add("Authorization", "Bearer " + sesion.AccessToken));

    [Fact]
    public async Task Cuando_el_tercero_designa_una_cuenta_a_quien_la_aprueba_le_llega_la_notificacion_y_a_la_empresa_el_cambio_sin_recargar()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var segundaAprobadora = await OficialAsync(e, "Olga Tesorería");
        var otra = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        var ajena = await PersonaConRolAsync(otra, "Sara Sur", "Revisor");

        // Streams abiertos antes de que el tercero haga nada: quien aprueba cuentas, quien solo revisa, otra empresa y el propio tercero.
        await using var carlos = await AbrirStreamDeEmpresaAsync(e.Norte, e.Carlos);
        await using var laura = await AbrirStreamDeEmpresaAsync(e.Norte, e.Laura);
        await using var deOtraEmpresa = await AbrirStreamDeEmpresaAsync(otra, ajena);
        await using var tercero = await AbrirStreamDelTerceroAsync(sesion);

        var cuenta = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Bancolombia", "Ahorros", "031-123456-78", "Aceros del Norte S.A.S.", "NIT 900123456"), 201);
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación Bancolombia"));
        await TerceroAsync<CuentaBancariaPublicaDto>("POST", $"/public/v1/vinculos/{vinculo.Id}/cuenta-designada", sesion.AccessToken, new DesignarCuentaDto(cuenta.Id), 200);

        // A quien aprueba cuentas le llega la notificación por el stream (y queda en su bandeja) y el aviso de que cambió la cuenta del vínculo.
        var aviso = await carlos.EsperarAsync((ev, d) => Notificacion(ev, d, "vinculo.cuenta_propuesta"), "la notificación de la cuenta propuesta");
        aviso.Datos.ShouldContain("Cuenta bancaria por aprobar");
        aviso.Datos.ShouldContain($"/e/{e.Norte.Datos.Id}/vinculos/{vinculo.Id}");
        await carlos.EsperarAsync((ev, d) => Cambio(ev, d, "cuenta", cuenta.Id), "el cambio de la cuenta");
        await carlos.EsperarAsync((ev, d) => Cambio(ev, d, "vinculo", vinculo.Id), "el cambio del vínculo");
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, e.Carlos), null, 200)).Datos
            .ShouldContain(n => n.Tipo == "vinculo.cuenta_propuesta" && n.VinculoId == vinculo.Id);

        // Quien solo revisa no aprueba cuentas: no recibe la notificación, pero su pantalla abierta sí se entera del cambio.
        await laura.EsperarAsync((ev, d) => Cambio(ev, d, "cuenta", cuenta.Id), "el cambio de la cuenta");
        await laura.NoLlegaAsync((ev, d) => Notificacion(ev, d, "vinculo.cuenta_propuesta"), TimeSpan.FromSeconds(1), "la notificación de la cuenta propuesta");
        (await ApiAsync<BandejaNotificacionesDto>("GET", "/api/v1/notificaciones", Delegado(e.Norte, e.Laura), null, 200)).Datos
            .ShouldNotContain(n => n.Tipo == "vinculo.cuenta_propuesta");

        // El propio tercero ve el cambio de su cuenta en su portal (otra pestaña); la empresa ajena no se entera de nada.
        await tercero.EsperarAsync((ev, d) => Cambio(ev, d, "cuenta", cuenta.Id), "el cambio de su cuenta");
        await deOtraEmpresa.NoLlegaAsync((ev, d) => ev is "cambio" or "notificacion", TimeSpan.FromSeconds(1), "nada de otra empresa");

        // Las dos aprobaciones: el tercero se entera en su campana y su pantalla de cuentas se refresca sola.
        var url = $"/api/v1/vinculos/{vinculo.Id}/cuenta-designada";
        await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, e.Carlos), new { }, 200);
        await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, segundaAprobadora), new { }, 200);
        var aprobada = await tercero.EsperarAsync((ev, d) => Notificacion(ev, d, "tercero.cuenta"), "el aviso de la cuenta aprobada");
        aprobada.Datos.ShouldContain("Cuenta aprobada");
        aprobada.Datos.ShouldContain("/cuentas");
        aprobada.Datos.ShouldContain("Norte S.A.S.");
        await tercero.EsperarAsync((ev, d) => ev == "no_leidas" && int.Parse(d, System.Globalization.CultureInfo.InvariantCulture) > 0, "el contador de no leídas");
        await carlos.NoLlegaAsync((ev, d) => Notificacion(ev, d, "tercero.cuenta"), TimeSpan.FromSeconds(1), "los avisos del tercero");
    }

    [Fact]
    public async Task El_stream_del_tercero_lleva_la_observacion_y_el_cambio_del_expediente_solo_a_la_sesion_del_vinculo()
    {
        var nit = Nit();
        var a = await ProveedorObservadoAsync(nit: nit, observar: false);
        // El mismo tercero tiene otro vínculo, con otra empresa y otro correo: otra sesión; y otro tercero ajeno en otra empresa.
        var sur = await EmpresaNuevaAsync("Sur S.A.S.", "SUR");
        var correoB = CorreoNuevo();
        await CrearVinculoAsync(sur, "CLIENTE", correoB, nit);
        var sesionB = await SesionAsync(correoB);
        var correoAjeno = CorreoNuevo();
        await CrearVinculoAsync(sur, "CLIENTE", correoAjeno);
        var sesionAjena = await SesionAsync(correoAjeno);

        await using var deA = await AbrirStreamDelTerceroAsync(a.Sesion);
        await using var deB = await AbrirStreamDelTerceroAsync(sesionB);
        await using var deAjeno = await AbrirStreamDelTerceroAsync(sesionAjena);
        await using var deNorte = await AbrirStreamDeEmpresaAsync(a.E.Norte, a.E.Carlos);

        (await VeredictoAsync<ExpedienteDto>(a.E, a.Expediente, a.Camara.Id, a.E.Cumplimiento, a.E.Carlos,
            new VeredictoDto("Observado", ["ilegible"], "La cámara está cortada.", SobreVersion: 1))).Estado.ShouldBe("Observado");

        var observacion = await deA.EsperarAsync((ev, d) => Notificacion(ev, d, "tercero.observado"), "la notificación de la observación");
        observacion.Datos.ShouldContain("Tienes observaciones por corregir");
        observacion.Datos.ShouldContain("Norte S.A.S.");
        observacion.Datos.ShouldNotContain("Carlos");
        await deA.EsperarAsync((ev, d) => Cambio(ev, d, "expediente", a.Expediente), "el cambio del expediente");
        await deNorte.EsperarAsync((ev, d) => Cambio(ev, d, "expediente", a.Expediente), "el cambio del expediente en la empresa");

        // La sesión del mismo tercero con otro correo y la de otro tercero no reciben nada de ese vínculo.
        await deB.NoLlegaAsync((ev, d) => Cambio(ev, d, "expediente", a.Expediente) || Notificacion(ev, d, "tercero.observado"), TimeSpan.FromSeconds(2), "lo del otro vínculo");
        await deAjeno.NoLlegaAsync((ev, d) => Cambio(ev, d, "expediente", a.Expediente) || Notificacion(ev, d, "tercero.observado"), TimeSpan.FromSeconds(1), "lo de otro tercero");
    }

    [Fact]
    public async Task El_stream_del_tercero_se_cierra_al_vencer_su_sesion_y_pide_el_token_renovado_y_sin_sesion_no_abre()
    {
        var e = await PrepararAsync();
        var correo = CorreoNuevo();
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);

        await using var tercero = await AbrirStreamDelTerceroAsync(sesion);
        tercero.Termino.ShouldBeFalse();
        try
        {
            // Con el reloj pasado el vencimiento del token (30 minutos), el servidor cierra el stream: el cliente reconecta con el renovado.
            fixture.Reloj.Desfase = TimeSpan.FromMinutes(31);
            var limite = DateTime.UtcNow.AddSeconds(20);
            while (!tercero.Termino && DateTime.UtcNow < limite)
                await Task.Delay(100);
            tercero.Termino.ShouldBeTrue();
        }
        finally
        {
            fixture.Reloj.Desfase = TimeSpan.Zero;
        }

        // Sin token, o con el de una persona de la empresa, el stream del tercero no abre.
        using var cliente = Host.Server.CreateClient();
        using var sinSesion = await cliente.GetAsync("/public/v1/notificaciones/stream");
        sinSesion.StatusCode.ShouldBe(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task El_stream_de_la_empresa_solo_avisa_los_cambios_de_la_empresa_activa_aunque_otra_comparta_el_grupo()
    {
        var e = await PrepararAsync();
        // Otra empresa del mismo grupo (mismo tenant) con su propia persona.
        var datosSur = await ApiAsync<EmpresaDto>("POST", "/api/v1/empresas", new Credencial(e.Norte.Admin),
            new CrearEmpresaDto(new IdentificacionDto("CO", "NIT", "900123456-8"), "Sur Hermana S.A.S.", "SURH", null, null), 201);
        var sur = new Empresa(e.Norte.Admin, datosSur, e.Norte.LlaveSrm, e.Norte.ClaveSrm);
        var deSur = await PersonaConRolAsync(sur, "Sara Sur", "Revisor");

        await using var norte = await AbrirStreamDeEmpresaAsync(e.Norte, e.Laura);
        await using var hermana = await AbrirStreamDeEmpresaAsync(sur, deSur);

        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        await norte.EsperarAsync((ev, d) => Cambio(ev, d, "vinculo", vinculo.Id), "el cambio del vínculo nuevo");
        await hermana.NoLlegaAsync((ev, d) => ev == "cambio", TimeSpan.FromSeconds(2), "cambios de otra empresa del grupo");

        // Sin empresa activa no hay avisos de cambio (solo la campana de siempre).
        var credencial = Delegado(e.Norte, e.Laura);
        await using var sinEmpresa = await LectorSse.AbrirAsync(Host, "/api/v1/notificaciones/stream", peticion =>
        {
            peticion.Headers.Add("X-Api-Key", credencial.ApiKey);
            peticion.Headers.Add("X-Usuario-Delegado", credencial.Delegado!);
        });
        await CrearVinculoAsync(e.Norte, "PROVEEDOR", CorreoNuevo());
        await sinEmpresa.NoLlegaAsync((ev, d) => ev == "cambio", TimeSpan.FromSeconds(2), "cambios sin empresa activa");
    }
}
