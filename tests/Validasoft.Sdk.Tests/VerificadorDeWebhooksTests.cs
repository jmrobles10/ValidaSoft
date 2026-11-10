using System.Text.Json;
using Validasoft.Aplicacion.Notificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Integraciones;
using Validasoft.Sdk;

namespace Validasoft.Sdk.Tests;

/// <summary>
/// El verificador del SDK acepta exactamente lo que firma el servidor (<c>EnviarEntregaWebhookHandler</c>: cuerpo de
/// <c>JsonEventos.Cuerpo</c> y cabecera de <c>FirmaDeWebhook.Cabecera</c>) y rechaza lo demás.
/// </summary>
public sealed class VerificadorDeWebhooksTests
{
    private const string Secreto = "whsec_secreto-actual-de-prueba-0123456789abcdef";
    private const string Anterior = "whsec_secreto-anterior-de-prueba-0123456789abcd";

    private static readonly DateTimeOffset Ahora = new(2026, 11, 9, 15, 0, 0, TimeSpan.Zero);

    private static (string Cuerpo, EventoPublicoDto Evento) Entrega()
    {
        using var datos = JsonDocument.Parse("""{"vinculoId":"0195f1c2-7d3e-7a00-8000-000000000001","certificado":{"folio":"N1-C-2026-000001-1"}}""");
        var evento = new EventoPublicoDto(Guid.CreateVersion7(), "certificado.emitido", 1, Ahora.AddSeconds(-3), Guid.CreateVersion7(),
            Guid.CreateVersion7(), "42", datos.RootElement.Clone());
        return (JsonEventos.Cuerpo(evento), evento);
    }

    private static VerificadorDeWebhooks Verificador(params string[] secretos) =>
        new(secretos.Length == 0 ? [Secreto] : secretos, reloj: () => Ahora);

    [Fact]
    public void Acepta_la_firma_del_servidor_y_devuelve_el_evento()
    {
        var (cuerpo, evento) = Entrega();
        var cabecera = FirmaDeWebhook.Cabecera([Secreto], Ahora.ToUnixTimeSeconds(), cuerpo);

        Verificador().FirmaValida(cabecera, cuerpo).ShouldBeTrue();
        var leido = Verificador().Validar(cabecera, cuerpo);
        (leido.Id, leido.Tipo, leido.Cursor, leido.EmpresaId).ShouldBe((evento.Id, "certificado.emitido", "42", evento.EmpresaId));
        leido.Datos.GetProperty("certificado").GetProperty("folio").GetString().ShouldBe("N1-C-2026-000001-1");

        // Y el servidor acepta lo que firma el SDK (mismo algoritmo en los dos sentidos).
        var delSdk = VerificadorDeWebhooks.Cabecera([Secreto], Ahora.ToUnixTimeSeconds(), cuerpo);
        delSdk.ShouldBe(cabecera);
        FirmaDeWebhook.Verificar(Secreto, delSdk, cuerpo, Ahora, TimeSpan.FromMinutes(5)).ShouldBeTrue();
    }

    [Fact]
    public void Durante_la_rotacion_del_secreto_vale_con_cualquiera_de_los_dos()
    {
        var (cuerpo, _) = Entrega();
        var cabecera = FirmaDeWebhook.Cabecera([Secreto, Anterior], Ahora.ToUnixTimeSeconds(), cuerpo);

        Verificador(Secreto).FirmaValida(cabecera, cuerpo).ShouldBeTrue();
        Verificador(Anterior).FirmaValida(cabecera, cuerpo).ShouldBeTrue();
        // El receptor que aún no cambió de secreto, ante una entrega firmada solo con el nuevo, la rechaza.
        var soloNuevo = FirmaDeWebhook.Cabecera([Secreto], Ahora.ToUnixTimeSeconds(), cuerpo);
        Verificador(Anterior).FirmaValida(soloNuevo, cuerpo).ShouldBeFalse();
        Verificador(Anterior, Secreto).FirmaValida(soloNuevo, cuerpo).ShouldBeTrue();
    }

    [Fact]
    public void Rechaza_otro_secreto_un_cuerpo_alterado_y_una_cabecera_mal_formada()
    {
        var (cuerpo, _) = Entrega();
        var cabecera = FirmaDeWebhook.Cabecera([Secreto], Ahora.ToUnixTimeSeconds(), cuerpo);

        Should.Throw<WebhookInvalidoException>(() => Verificador("whsec_otro").Validar(cabecera, cuerpo)).Motivo
            .ShouldBe(MotivoRechazoWebhook.FirmaNoCoincide);
        Should.Throw<WebhookInvalidoException>(() => Verificador().Validar(cabecera, cuerpo.Replace("000001-1", "000001-2"))).Motivo
            .ShouldBe(MotivoRechazoWebhook.FirmaNoCoincide);
        // Un espacio de más también cambia la firma: hay que verificar el cuerpo tal como llegó, sin volver a serializarlo.
        Verificador().FirmaValida(cabecera, cuerpo + " ").ShouldBeFalse();

        foreach (var mala in new[] { null, "", "v1=abc", $"t={Ahora.ToUnixTimeSeconds()}", "t=hoy,v1=" + new string('a', 64) })
            Should.Throw<WebhookInvalidoException>(() => Verificador().Validar(mala, cuerpo)).Motivo.ShouldBe(MotivoRechazoWebhook.CabeceraInvalida);
    }

    [Fact]
    public void Rechaza_una_firma_fuera_de_la_tolerancia_de_cinco_minutos()
    {
        var (cuerpo, _) = Entrega();
        var vieja = FirmaDeWebhook.Cabecera([Secreto], Ahora.AddMinutes(-6).ToUnixTimeSeconds(), cuerpo);
        var reciente = FirmaDeWebhook.Cabecera([Secreto], Ahora.AddMinutes(-4).ToUnixTimeSeconds(), cuerpo);
        var futura = FirmaDeWebhook.Cabecera([Secreto], Ahora.AddMinutes(6).ToUnixTimeSeconds(), cuerpo);

        Should.Throw<WebhookInvalidoException>(() => Verificador().Validar(vieja, cuerpo)).Motivo.ShouldBe(MotivoRechazoWebhook.FueraDeTolerancia);
        Should.Throw<WebhookInvalidoException>(() => Verificador().Validar(futura, cuerpo)).Motivo.ShouldBe(MotivoRechazoWebhook.FueraDeTolerancia);
        Verificador().FirmaValida(reciente, cuerpo).ShouldBeTrue();
        // Con una tolerancia mayor (configurable) la vieja pasa.
        new VerificadorDeWebhooks(Secreto, TimeSpan.FromMinutes(10), () => Ahora).FirmaValida(vieja, cuerpo).ShouldBeTrue();
    }

    [Fact]
    public void Un_cuerpo_firmado_que_no_es_un_evento_se_rechaza_como_cuerpo_invalido()
    {
        const string cuerpo = "no es json";
        var cabecera = VerificadorDeWebhooks.Cabecera([Secreto], Ahora.ToUnixTimeSeconds(), cuerpo);
        Should.Throw<WebhookInvalidoException>(() => Verificador().Validar(cabecera, cuerpo)).Motivo.ShouldBe(MotivoRechazoWebhook.CuerpoInvalido);
    }

    [Fact]
    public void Lee_la_firma_desde_las_cabeceras_de_la_peticion()
    {
        var (cuerpo, evento) = Entrega();
        var cabeceras = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase)
        {
            ["validasoft-signature"] = FirmaDeWebhook.Cabecera([Secreto], Ahora.ToUnixTimeSeconds(), cuerpo),
            [VerificadorDeWebhooks.CabeceraEventoId] = evento.Id.ToString()
        };
        Verificador().Validar(n => cabeceras.GetValueOrDefault(n), cuerpo).Id.ToString().ShouldBe(cabeceras[VerificadorDeWebhooks.CabeceraEventoId]);
    }
}
