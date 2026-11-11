using System.Net;
using System.Net.Http.Headers;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// El SSE de la campana de la web de empresa con la sesión de ingreso directo (NT-12 + ING-3): como el token de acceso de una sesión
/// directa deja de valer en segundos al cerrarla o revocarla, el stream que esa sesión tenía abierto también se corta (antes solo se
/// comprobaba al conectar y el stream seguía mandando avisos hasta que la persona cerrara la pestaña).
/// </summary>
public sealed partial class IngresoApiTests
{
    [Fact]
    public async Task Cerrar_la_sesion_directa_corta_el_stream_de_la_campana_en_segundos_y_no_toca_el_de_otra_sesion()
    {
        var g = await GrupoAsync();
        var ana = await UsuarioAsync(g, "Revisor");
        var (primera, _) = await IngresarConCodigoAsync(ana.Correo);
        var (segunda, _) = await IngresarConCodigoAsync(ana.Correo, numero: 2);

        using var cliente = Host.Server.CreateClient();
        await using var deLaPrimera = await AbrirStreamAsync(cliente, primera.Sesion!.Token, g.Empresa.Id);
        await using var deLaSegunda = await AbrirStreamAsync(cliente, segunda.Sesion!.Token, g.Empresa.Id);
        deLaPrimera.Termino.ShouldBeFalse();

        // La segunda sesión cierra la primera (Mi cuenta): el stream de la primera termina y el de la segunda sigue abierto.
        await Anonimo($"/api/v1/mi-cuenta/sesiones/{primera.Sesion.SesionId}/cerrar", null, 204, bearer: segunda.Sesion.Token);
        await deLaPrimera.EsperarFinAsync(TimeSpan.FromSeconds(40));
        deLaSegunda.Termino.ShouldBeFalse();
        await Anonimo("/api/v1/notificaciones/stream", null, 401, metodo: "GET", bearer: primera.Sesion.Token, empresa: g.Empresa.Id);
    }

    private static async Task<StreamAbierto> AbrirStreamAsync(HttpClient cliente, string token, Guid empresa)
    {
        var peticion = new HttpRequestMessage(HttpMethod.Get, "/api/v1/notificaciones/stream");
        peticion.Headers.Authorization = new AuthenticationHeaderValue("Bearer", token);
        peticion.Headers.Add("X-Empresa", empresa.ToString());
        var respuesta = await cliente.SendAsync(peticion, HttpCompletionOption.ResponseHeadersRead);
        respuesta.StatusCode.ShouldBe(HttpStatusCode.OK);
        respuesta.Content.Headers.ContentType!.MediaType.ShouldBe("text/event-stream");
        return new StreamAbierto(peticion, respuesta);
    }

    /// <summary>Un stream SSE abierto: lee en segundo plano hasta que el servidor lo cierra.</summary>
    private sealed class StreamAbierto : IAsyncDisposable
    {
        private readonly CancellationTokenSource _cancelar = new(TimeSpan.FromMinutes(3));
        private readonly HttpRequestMessage _peticion;
        private readonly HttpResponseMessage _respuesta;
        private readonly Task _lectura;

        public StreamAbierto(HttpRequestMessage peticion, HttpResponseMessage respuesta)
        {
            _peticion = peticion;
            _respuesta = respuesta;
            _lectura = LeerAsync();
        }

        /// <summary>El servidor cerró el stream.</summary>
        public bool Termino => _lectura.IsCompleted;

        private async Task LeerAsync()
        {
            try
            {
                using var lector = new StreamReader(await _respuesta.Content.ReadAsStreamAsync(_cancelar.Token));
                while (await lector.ReadLineAsync(_cancelar.Token) is not null)
                {
                }
            }
            catch (Exception e) when (e is OperationCanceledException or IOException or ObjectDisposedException)
            {
                // Se cerró el stream (el servidor lo cortó o la prueba terminó).
            }
        }

        public async Task EsperarFinAsync(TimeSpan limite)
        {
            if (await Task.WhenAny(_lectura, Task.Delay(limite)) != _lectura)
                throw new TimeoutException($"El stream seguía abierto tras {limite.TotalSeconds:0} s.");
        }

        public async ValueTask DisposeAsync()
        {
            await _cancelar.CancelAsync();
            await _lectura;
            _respuesta.Dispose();
            _peticion.Dispose();
            _cancelar.Dispose();
        }
    }
}
