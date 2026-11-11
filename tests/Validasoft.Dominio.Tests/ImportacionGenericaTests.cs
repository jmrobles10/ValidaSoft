using System.Text.Json;
using Validasoft.Aplicacion.Importacion;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Importador genérico y referencias externas (integración abierta, IO-1…IO-4): identidad del legado por sistema y clave, CSV de
/// rechazos, el registro de adaptadores, el origen de una versión de la ficha (con lo guardado antes de generalizarlo) y las
/// referencias externas de un tercero.
/// </summary>
public class ImportacionGenericaTests
{
    [Fact]
    public void El_uuid_v5_es_el_del_rfc_y_determinista_por_sistema_y_clave()
    {
        // Vector de prueba de RFC 9562 (anexo A.4): www.example.com en el espacio DNS.
        IdDeLegado.UuidV5("www.example.com", new Guid("6ba7b810-9dad-11d1-80b4-00c04fd430c8")).ShouldBe(new Guid("2ed6657d-e927-568b-95e1-2665a8aea6a2"));
        IdDeLegado.Clave("portal", "48112").ShouldBe("portal:48112");
        IdDeLegado.Clave(" PORTAL ", " 48112 ").ShouldBe("portal:48112"); // el sistema va en minúsculas y sin espacios
        IdDeLegado.Tercero("portal", "48112").ShouldBe(IdDeLegado.Tercero("PORTAL", "48112"));
        IdDeLegado.Tercero("portal", "48112").ShouldNotBe(IdDeLegado.Tercero("portal", "48113"));
        IdDeLegado.Tercero("portal", "48112").ShouldNotBe(IdDeLegado.Tercero("otro", "48112"));
        IdDeLegado.Tercero("portal", "48112").Version.ShouldBe(5);
    }

    [Fact]
    public void El_csv_de_rechazos_escapa_comas_comillas_y_formulas()
    {
        var csv = ImportacionEndpoints.Csv(
        [
            new AvisoImportacionDto("portal:1", "1", "FICHA_GENERAL", null, "ciudad", "ciudad-sin-dane", "Sin \"DANE\", revisar", "=HYPERLINK(1)", null)
        ]);
        csv.Split("\r\n")[0].ShouldBe("claveLegado,clave,seccion,periodo,campo,codigo,motivo,dato,accionSugerida");
        csv.Split("\r\n")[1].ShouldBe("portal:1,1,FICHA_GENERAL,,ciudad,ciudad-sin-dane,\"Sin \"\"DANE\"\", revisar\",'=HYPERLINK(1),");
    }

    private sealed class AdaptadorDePrueba(string codigo) : IAdaptadorDeOrigen
    {
        public string Codigo => codigo;

        public bool ConectadoConfigurado => false;

        public void ValidarArchivo(string json) { }

        public IOrigenDeTerceros DesdeArchivo(string json) => throw new NotSupportedException();

        public IOrigenDeTerceros Conectado() => throw new NotSupportedException();

        public IOrigenDeTerceros Configurado() => throw new NotSupportedException();
    }

    [Fact]
    public void El_registro_resuelve_el_adaptador_sin_importar_mayusculas_y_sin_adaptadores_responde_no_encontrado()
    {
        var registro = new RegistroDeAdaptadores([new AdaptadorDePrueba("Erp-Uno"), new AdaptadorDePrueba("crm")]);
        registro.Codigos.ShouldBe(["crm", "erp-uno"]);
        registro.Obtener("ERP-UNO").Codigo.ShouldBe("Erp-Uno");

        var ex = Should.Throw<ReglaDeNegocioException>(() => new RegistroDeAdaptadores([]).Obtener("portal"));
        ex.Codigo.ShouldBe("origen-no-registrado");
        ex.Tipo.ShouldBe(TipoFalla.NoEncontrado);
    }

    private sealed record Envoltorio(OrigenDeSeccion Origen);

    [Theory]
    [InlineData("\"Tercero\"", OrigenDeSeccion.Tercero)]
    [InlineData("\"Importada\"", OrigenDeSeccion.Importada)]
    [InlineData("\"SistemaExterno\"", OrigenDeSeccion.SistemaExterno)]
    [InlineData("\"Portal\"", OrigenDeSeccion.SistemaExterno)] // lo guardado antes de generalizar el origen
    public void El_origen_de_una_version_se_lee_tambien_en_su_forma_antigua(string json, OrigenDeSeccion esperado)
    {
        JsonSerializer.Deserialize<Envoltorio>("{\"origen\":" + json + "}", new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldBe(new Envoltorio(esperado));
        JsonSerializer.Serialize(new Envoltorio(esperado), new JsonSerializerOptions(JsonSerializerDefaults.Web)).ShouldBe("{\"origen\":\"" + esperado + "\"}");
    }

    [Fact]
    public void El_origen_desconocido_no_se_lee_en_silencio()
    {
        Should.Throw<JsonException>(() => JsonSerializer.Deserialize<Envoltorio>("{\"origen\":\"Marte\"}", new JsonSerializerOptions(JsonSerializerDefaults.Web)));
    }

    [Theory]
    [InlineData(OrigenDeSeccion.Tercero, true)]
    [InlineData(OrigenDeSeccion.Importada, false)]
    [InlineData(OrigenDeSeccion.SistemaExterno, false)]
    public void Solo_el_cambio_del_tercero_exige_soporte(OrigenDeSeccion origen, bool exige) =>
        SeccionDeFicha.ExigeSoporteDelCambio(origen).ShouldBe(exige);

    // ---------- referencias externas del tercero ----------

    [Fact]
    public void Una_referencia_de_tercero_normaliza_el_sistema_y_arma_el_id_unico_por_sistema_y_clave()
    {
        var terceroId = Guid.NewGuid();
        var r = ReferenciaDeTercero.Crear(terceroId, " srm ", " ADP-00123 ", DateTimeOffset.UnixEpoch);
        (r.Id, r.Sistema, r.Clave, r.TerceroId).ShouldBe(("SRM|ADP-00123", "SRM", "ADP-00123", terceroId));
        ReferenciaDeTercero.IdPara("Srm", "ADP-00123").ShouldBe(r.Id);
    }

    [Theory]
    [InlineData(null, "x")]
    [InlineData("SRM", null)]
    [InlineData(" ", "x")]
    [InlineData("SRM", "  ")]
    [InlineData("A|B", "x")]
    public void Una_referencia_de_tercero_sin_sistema_o_clave_validos_es_422(string? sistema, string? clave)
    {
        var ex = Should.Throw<ReglaDeNegocioException>(() => ReferenciaDeTercero.Crear(Guid.NewGuid(), sistema, clave, DateTimeOffset.UnixEpoch));
        ex.Codigo.ShouldBe("referencia-externa-invalida");
    }

    [Fact]
    public void Una_referencia_de_tercero_acota_el_largo_del_sistema_y_de_la_clave()
    {
        Should.Throw<ReglaDeNegocioException>(() => ReferenciaDeTercero.Crear(Guid.NewGuid(), new string('S', ReferenciaDeTercero.MaximoSistema + 1), "x", DateTimeOffset.UnixEpoch));
        Should.Throw<ReglaDeNegocioException>(() => ReferenciaDeTercero.Crear(Guid.NewGuid(), "SRM", new string('9', ReferenciaDeTercero.MaximoClave + 1), DateTimeOffset.UnixEpoch));
        ReferenciaDeTercero.Crear(Guid.NewGuid(), "SRM", new string('9', ReferenciaDeTercero.MaximoClave), DateTimeOffset.UnixEpoch).Clave.Length.ShouldBe(ReferenciaDeTercero.MaximoClave);
    }
}
