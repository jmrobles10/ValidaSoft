using System.Security.Cryptography;
using System.Text;
using System.Text.Json;
using Marten;
using Microsoft.Extensions.DependencyInjection;
using Npgsql;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Ia;
using Validasoft.Infraestructura.Seguridad;

namespace Validasoft.Integracion.Tests;

/// <summary>
/// Plataforma e identidad, segunda tanda, lo que necesita el flujo de validación: el consumo de IA por cliente con su tarifa y su exportación (PI-12) y el número de cuenta
/// de los eventos del vínculo con la clave del grupo (PI-11).
/// </summary>
public sealed partial class RequisitosApiTests
{
    private static Credencial PlataformaPi => new(ApiFixture.ClavePlataforma);

    // ---------- consumo de IA por cliente ----------

    /// <summary>Una empresa con plan (llave «baja»: cada análisis escala a un segundo modelo) y dos análisis consumidos: una validación incluida y una adicional.</summary>
    private async Task<(Empresa Norte, string Mes)> EmpresaConConsumoDeIaAsync()
    {
        var norte = await EmpresaNuevaAsync("Norte S.A.S.", "NORTE");
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(1, true, TopeAdicionalMes: 5, ApiKeyOpenAi: LlaveIa("baja")));
        await FlujoConAsync(norte, "CLIENTE", [new RequisitoDelFlujoDto(TipoDocumento: "RUT"), new RequisitoDelFlujoDto(TipoDocumento: "CAMARA_COMERCIO")]);
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(norte, "CLIENTE", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        await SubirAsync(sesion, "RUT", Pdf("RUT"));
        await EsperarVersionAsync(norte, vinculo.Id, "RUT", 1, v => v.Hechos!.ConIa);
        await SubirAsync(sesion, "CAMARA_COMERCIO", Pdf("Cámara"));
        await EsperarVersionAsync(norte, vinculo.Id, "CAMARA_COMERCIO", 1, v => v.Hechos!.ConIa);
        var uso = await UsoIaAsync(norte);
        (uso.IncluidasConsumidas, uso.AdicionalesConsumidas, uso.AnalisisPagados).ShouldBe((1, 1, 2));
        return (norte, uso.Mes);
    }

    [Fact]
    public async Task El_consumo_de_IA_por_cliente_suma_por_empresa_y_modelo_y_valora_lo_adicional_con_la_tarifa_o_el_precio_propio()
    {
        var (norte, mes) = await EmpresaConConsumoDeIaAsync();
        var uso = await UsoIaAsync(norte);
        var tenant = norte.Datos.TenantId;

        // Con la tarifa general, lo adicional vale cuántas × el precio.
        var tarifa = await ApiAsync<TarifaDeIaDto>("PUT", "/api/v1/plataforma/ia/tarifa", PlataformaPi, new FijarTarifaDeIaDto(500m, " cop "), 200);
        (tarifa.PrecioPorValidacionAdicional, tarifa.Moneda, tarifa.ActualizadaEn is not null, tarifa.ActualizadaPor!.StartsWith("Plataforma:", StringComparison.Ordinal)).ShouldBe((500m, "COP", true, true));
        (await ApiAsync<TarifaDeIaDto>("GET", "/api/v1/plataforma/ia/tarifa", PlataformaPi, null, 200)).PrecioPorValidacionAdicional.ShouldBe(500m);

        var consumo = await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&grupoId={tenant}", PlataformaPi, null, 200);
        consumo.Periodo.ShouldBe(mes);
        var grupo = consumo.Grupos.ShouldHaveSingleItem();
        grupo.GrupoId.ShouldBe(tenant);
        var fila = grupo.Empresas.ShouldHaveSingleItem();
        (fila.EmpresaId, fila.ValidacionesIncluidasMes, fila.IncluidasConsumidas, fila.AdicionalesConsumidas, fila.AnalisisPagados).ShouldBe((norte.Datos.Id, 1, 1, 1, 2));
        (fila.PrecioAdicional, fila.PrecioPropio, fila.ValorAdicional).ShouldBe((500m, false, 500m));
        (fila.TokensEntrada, fila.TokensEntradaEnCache, fila.TokensSalida, fila.CostoUsd).ShouldBe((uso.TokensEntrada, uso.TokensEntradaEnCache, uso.TokensSalida, uso.CostoUsd));

        // Por modelo: con la llave «baja» cada análisis llama al modelo por defecto y escala al otro (4 llamadas); lo de los modelos suma el total de la empresa.
        fila.Modelos.Select(m => m.Modelo).ShouldBe([PlanDeIaEmpresa.ModeloPorDefectoInicial, PlanDeIaEmpresa.ModeloEscalamientoInicial], ignoreOrder: true);
        fila.Modelos.ShouldAllBe(m => m.Llamadas == 2);
        (fila.Modelos.Sum(m => m.TokensEntrada), fila.Modelos.Sum(m => m.TokensSalida), fila.Modelos.Sum(m => m.CostoUsd)).ShouldBe((fila.TokensEntrada, fila.TokensSalida, fila.CostoUsd));
        (grupo.AdicionalesConsumidas, grupo.ValorAdicional, grupo.CostoUsd).ShouldBe((1, 500m, fila.CostoUsd));
        (consumo.Totales.Grupos, consumo.Totales.Empresas, consumo.Totales.AdicionalesConsumidas, consumo.Totales.ValorAdicional).ShouldBe((1, 1, 1, 500m));

        // El precio propio de la empresa gana sobre la tarifa; vaciarlo vuelve a la general.
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(1, true, TopeAdicionalMes: 5, PrecioAdicional: 320.5m));
        var propio = (await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&grupoId={tenant}", PlataformaPi, null, 200)).Grupos.Single().Empresas.Single();
        (propio.PrecioAdicional, propio.PrecioPropio, propio.ValorAdicional).ShouldBe((320.5m, true, 320.5m));
        (await ApiAsync<PlanIaEmpresaDto>("GET", $"/api/v1/plataforma/tenants/{tenant}/empresas/{norte.Datos.Id}/plan-ia", PlataformaPi, null, 200)).PrecioAdicional.ShouldBe(320.5m);
        (await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&grupoId={tenant}", PlataformaPi, null, 200)).Grupos.Single().Empresas.Single().Modelos.Count.ShouldBe(2); // la llave se conservó
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(1, true, TopeAdicionalMes: 5));
        (await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&grupoId={tenant}", PlataformaPi, null, 200)).Grupos.Single().Empresas.Single().PrecioPropio.ShouldBeFalse();
        // Gratis es un precio, no «sin precio».
        await PlanIaAsync(norte, new ActualizarPlanIaEmpresaDto(1, true, TopeAdicionalMes: 5, PrecioAdicional: 0m));
        (await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&grupoId={tenant}", PlataformaPi, null, 200)).Grupos.Single().Empresas.Single().ValorAdicional.ShouldBe(0m);
        (await CodigoAsync(await ApiAsync("PUT", $"/api/v1/plataforma/tenants/{tenant}/empresas/{norte.Datos.Id}/plan-ia", PlataformaPi, new ActualizarPlanIaEmpresaDto(1, true, PrecioAdicional: -1m), 422)))
            .ShouldBe("precio-adicional-invalido");
    }

    [Fact]
    public async Task El_consumo_de_IA_se_filtra_por_periodo_y_busqueda_se_pagina_y_solo_lo_ve_Plataforma()
    {
        var (norte, mes) = await EmpresaConConsumoDeIaAsync();
        var tenant = norte.Datos.TenantId;
        var otra = await EmpresaNuevaAsync("Otra Empresa S.A.S.", "OTRA"); // otro grupo, sin plan ni consumo

        // Otro mes: la empresa aparece sin consumo.
        var vacio = (await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo=2020-01&grupoId={tenant}", PlataformaPi, null, 200)).Grupos.Single().Empresas.Single();
        (vacio.IncluidasConsumidas, vacio.AdicionalesConsumidas, vacio.CostoUsd, vacio.Modelos.Count).ShouldBe((0, 0, 0m, 0));

        // Búsqueda por nombre de empresa o del grupo (acotada al grupo: la base de pruebas es compartida y otras pruebas crean empresas con el mismo nombre); los totales
        // son los de todo lo que cumple el filtro, no solo los de la página.
        var porEmpresa = await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&grupoId={tenant}&q={Uri.EscapeDataString("Norte S.A.S.")}", PlataformaPi, null, 200);
        porEmpresa.Grupos.ShouldHaveSingleItem().GrupoId.ShouldBe(tenant);
        var porGrupo = await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&grupoId={tenant}&q={Uri.EscapeDataString("Grupo Norte")}", PlataformaPi, null, 200);
        porGrupo.Grupos.ShouldHaveSingleItem().GrupoId.ShouldBe(tenant);
        var sinCoincidencia = await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&grupoId={otra.Datos.TenantId}&q={Uri.EscapeDataString("Norte S.A.S.")}", PlataformaPi, null, 200);
        sinCoincidencia.Grupos.ShouldBeEmpty();
        sinCoincidencia.Totales.Grupos.ShouldBe(0);
        var primeraPagina = await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&limite=1", PlataformaPi, null, 200);
        primeraPagina.Grupos.Count.ShouldBe(1);
        primeraPagina.SiguienteCursor.ShouldNotBeNull();
        primeraPagina.Totales.Grupos.ShouldBeGreaterThanOrEqualTo(2);
        var segunda = await ApiAsync<ConsumoDeIaDto>("GET", $"/api/v1/plataforma/ia/consumo?periodo={mes}&limite=1&cursor={Uri.EscapeDataString(primeraPagina.SiguienteCursor!)}", PlataformaPi, null, 200);
        segunda.Grupos.Single().GrupoId.ShouldNotBe(primeraPagina.Grupos.Single().GrupoId);

        // Valores inválidos y credenciales: solo Plataforma.
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/plataforma/ia/consumo?periodo=2026-13", PlataformaPi, null, 422))).ShouldBe("periodo-invalido");
        (await CodigoAsync(await ApiAsync("GET", "/api/v1/plataforma/ia/consumo/exportar?formato=pdf", PlataformaPi, null, 422))).ShouldBe("formato-invalido");
        (await CodigoAsync(await ApiAsync("PUT", "/api/v1/plataforma/ia/tarifa", PlataformaPi, new FijarTarifaDeIaDto(-1m, "COP"), 422))).ShouldBe("precio-adicional-invalido");
        (await CodigoAsync(await ApiAsync("PUT", "/api/v1/plataforma/ia/tarifa", PlataformaPi, new FijarTarifaDeIaDto(1m, "PESOS"), 422))).ShouldBe("moneda-invalida");
        await ApiAsync("GET", "/api/v1/plataforma/ia/consumo", Admin(norte), null, 403);
        await ApiAsync("GET", "/api/v1/plataforma/ia/consumo/exportar", Admin(norte), null, 403);
        await ApiAsync("PUT", "/api/v1/plataforma/ia/tarifa", Admin(norte), new FijarTarifaDeIaDto(1m, "COP"), 403);
    }

    [Fact]
    public async Task El_consumo_de_IA_se_exporta_a_Excel_y_a_CSV_con_los_mismos_numeros_de_la_consola()
    {
        var (norte, mes) = await EmpresaConConsumoDeIaAsync();
        await ApiAsync<TarifaDeIaDto>("PUT", "/api/v1/plataforma/ia/tarifa", PlataformaPi, new FijarTarifaDeIaDto(750m, "COP"), 200);
        var tenant = norte.Datos.TenantId;
        var uso = await UsoIaAsync(norte);

        // CSV: una fila por empresa, con BOM para Excel, números con punto y sin celdas que se lean como fórmulas.
        var csv = await ApiAsync("GET", $"/api/v1/plataforma/ia/consumo/exportar?periodo={mes}&grupoId={tenant}&formato=csv", PlataformaPi, null, 200);
        csv.Context.Response.ContentType.ShouldStartWith("text/csv");
        csv.Context.Response.Headers.ContentDisposition.ToString().ShouldContain($"consumo-ia-{mes}.csv");
        var texto = Encoding.UTF8.GetString(CuerpoEnBytes(csv));
        texto.ShouldStartWith("﻿Grupo,Identificador del grupo,Empresa,Periodo");
        var lineas = texto.TrimEnd().Split('\n').Select(l => l.TrimEnd('\r')).ToList();
        lineas.Count.ShouldBe(2);
        lineas[1].ShouldContain("Norte S.A.S.");
        lineas[1].ShouldContain($",{mes},1,1,1,Sí,750");
        lineas[1].ShouldContain(",COP,750");
        lineas[1].ShouldContain($",No,2,{uso.TokensEntrada},{uso.TokensEntradaEnCache},{uso.TokensSalida},");

        // Excel: un libro con las hojas «Por empresa» y «Por modelo».
        var xlsx = await ApiAsync("GET", $"/api/v1/plataforma/ia/consumo/exportar?periodo={mes}&grupoId={tenant}", PlataformaPi, null, 200);
        xlsx.Context.Response.ContentType.ShouldBe(HojaDeCalculo.TipoContenido);
        var libro = CuerpoEnBytes(xlsx);
        libro[..2].ShouldBe([(byte)'P', (byte)'K']); // un zip (Office Open XML)
        using var zip = new System.IO.Compression.ZipArchive(new MemoryStream(libro));
        var hojas = zip.GetEntry("xl/workbook.xml")!.Open();
        using var lector = new StreamReader(hojas);
        var libroXml = await lector.ReadToEndAsync();
        libroXml.ShouldContain("Por empresa");
        libroXml.ShouldContain("Por modelo");
    }

    private static byte[] CuerpoEnBytes(Alba.IScenarioResult resultado)
    {
        using var copia = new MemoryStream();
        resultado.Context.Response.Body.CopyTo(copia);
        return copia.ToArray();
    }

    // ---------- el número de cuenta de los eventos, con la clave del grupo ----------

    private async Task<(long Seq, string Valor)[]> NumerosEnEventosAsync(Guid tenantId)
    {
        await using var conexion = new NpgsqlConnection(fixture.CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand("select seq_id, data #>> '{cuenta,numeroProtegido}' from eventos.mt_events where tenant_id = @t and data #>> '{cuenta,numeroProtegido}' is not null order by seq_id", conexion);
        comando.Parameters.AddWithValue("t", tenantId.ToString("N"));
        var filas = new List<(long, string)>();
        await using var lector = await comando.ExecuteReaderAsync();
        while (await lector.ReadAsync())
            filas.Add((lector.GetInt64(0), lector.GetString(1)));
        return [.. filas];
    }

    private async Task<string?> NumeroEnElVinculoAsync(Guid tenantId, Guid vinculoId, string ruta)
    {
        await using var conexion = new NpgsqlConnection(fixture.CadenaDeConexion);
        await conexion.OpenAsync();
        await using var comando = new NpgsqlCommand($"select data #>> '{{{ruta},cuenta,numeroProtegido}}' from validacion.mt_doc_vinculo where tenant_id = @t and id = @id", conexion);
        comando.Parameters.AddWithValue("t", tenantId.ToString("N"));
        comando.Parameters.AddWithValue("id", vinculoId);
        return await comando.ExecuteScalarAsync() as string;
    }

    private async Task EscribirNumeroAsync(Guid tenantId, Guid vinculoId, string ruta, string valor, bool enEventos)
    {
        await using var conexion = new NpgsqlConnection(fixture.CadenaDeConexion);
        await conexion.OpenAsync();
        var sql = enEventos
            ? "update eventos.mt_events set data = jsonb_set(data, '{cuenta,numeroProtegido}', to_jsonb(@v::text)) where tenant_id = @t and stream_id = @id and data #>> '{cuenta,numeroProtegido}' is not null"
            : $"update validacion.mt_doc_vinculo set data = jsonb_set(data, '{{{ruta},cuenta,numeroProtegido}}', to_jsonb(@v::text)) where tenant_id = @t and id = @id and data #>> '{{{ruta},cuenta,numeroProtegido}}' is not null";
        await using var comando = new NpgsqlCommand(sql, conexion);
        comando.Parameters.AddWithValue("v", valor);
        comando.Parameters.AddWithValue("t", tenantId.ToString("N"));
        comando.Parameters.AddWithValue("id", vinculoId);
        (await comando.ExecuteNonQueryAsync()).ShouldBeGreaterThan(0);
    }

    [Fact]
    public async Task El_numero_de_cuenta_de_los_eventos_del_vinculo_se_cifra_con_la_clave_del_grupo_y_lo_anterior_se_migra_sin_perderse()
    {
        var e = await PrepararAsync();
        var tenant = e.Norte.Datos.TenantId;
        var campos = Host.Services.GetRequiredService<IProtectorDeCamposDelGrupo>();
        var protector = Host.Services.GetRequiredService<IProtectorDeSecretos>();
        var correo = CorreoNuevo();
        var vinculo = await CrearVinculoAsync(e.Norte, "PROVEEDOR", correo);
        var sesion = await SesionAsync(correo);
        await AceptarAsync(sesion, vinculo.Id);
        var cuenta = await TerceroAsync<CuentaBancariaPublicaDto>("POST", "/public/v1/boveda/cuentas-bancarias", sesion.AccessToken,
            new RegistrarCuentaBancariaDto("Bancolombia", "Ahorros", "031-123456-78", "Aceros del Norte S.A.S.", "NIT 900123456"), 201);
        await SubirAsync(sesion, "CERTIFICACION_BANCARIA", Pdf("Certificación Bancolombia"));
        await TerceroAsync<CuentaBancariaPublicaDto>("POST", $"/public/v1/vinculos/{vinculo.Id}/cuenta-designada", sesion.AccessToken, new DesignarCuentaDto(cuenta.Id), 200);
        var url = $"/api/v1/vinculos/{vinculo.Id}/cuenta-designada";

        // El evento guardó el número con la clave del grupo (vg1): se lee con ella y no con la de otro grupo ni con la general de la plataforma.
        var enEventos = (await NumerosEnEventosAsync(tenant)).ShouldHaveSingleItem().Valor;
        enEventos.ShouldStartWith("vg1.");
        enEventos.ShouldNotContain("03112345678");
        campos.Desproteger(tenant, enEventos).ShouldBe("03112345678");
        Should.Throw<CryptographicException>(() => campos.Desproteger(Guid.NewGuid(), enEventos)); // moverlo a otro grupo no sirve
        Should.Throw<Exception>(() => protector.Desproteger(enEventos)); // tampoco con la clave general de la plataforma
        (await NumeroEnElVinculoAsync(tenant, vinculo.Id, "cuentaPropuesta")).ShouldBe(enEventos);
        // La bóveda del tercero conserva el suyo con la clave general: no cambia.
        campos.EstaVigente(tenant, enEventos).ShouldBeTrue();

        // Quien aprueba lo ve completo para cotejarlo; las dos aprobaciones dejan la cuenta designada y también quedan con la clave del grupo.
        (await ApiAsync<CuentaDesignadaDelVinculoDto>("GET", url, Delegado(e.Norte, e.Carlos), null, 200)).Propuesta!.Numero.ShouldBe("03112345678");
        await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, e.Carlos), new { }, 200);
        var otra = await OficialAsync(e, "Olga Tesorería");
        var designada = await ApiAsync<CuentaDesignadaDelVinculoDto>("POST", $"{url}/aprobar", Delegado(e.Norte, otra), new { }, 200);
        designada.Actual!.Numero.ShouldBe("03112345678");
        (await NumerosEnEventosAsync(tenant)).ShouldAllBe(n => n.Valor.StartsWith("vg1.", StringComparison.Ordinal));
        (await NumeroEnElVinculoAsync(tenant, vinculo.Id, "cuentaDesignada"))!.ShouldStartWith("vg1.");

        // Lo que ya existía con la clave general (antes de este cambio) se sigue leyendo: se simula dejando los eventos y el documento del vínculo como estaban.
        var antiguo = protector.Proteger("03112345678");
        await EscribirNumeroAsync(tenant, vinculo.Id, "cuentaDesignada", antiguo, enEventos: true);
        await EscribirNumeroAsync(tenant, vinculo.Id, "cuentaDesignada", antiguo, enEventos: false);
        (await NumerosEnEventosAsync(tenant)).ShouldAllBe(n => n.Valor.StartsWith("vs1.", StringComparison.Ordinal));
        (await ApiAsync<CuentaDesignadaDelVinculoDto>("GET", url, Delegado(e.Norte, e.Carlos), null, 200)).Actual!.Numero.ShouldBe("03112345678");
        // Quien no aprueba cuentas nunca ve el número, con cualquiera de las claves.
        (await ApiAsync<CuentaDesignadaDelVinculoDto>("GET", url, Delegado(e.Norte, e.Laura), null, 200)).Actual!.Numero.ShouldBeNull();

        // El estado de los secretos cuenta lo pendiente sin tocar nada; re-cifrar lo pasa a la clave del grupo, es idempotente y deja el valor legible.
        var conteo = await ApiAsync<EstadoDeSecretosDto>("GET", "/api/v1/plataforma/secretos", PlataformaPi, null, 200);
        conteo.Simulado.ShouldBeTrue();
        var enEventosPendientes = conteo.PorTipo.Single(t => t.Tipo == "cuenta-en-eventos");
        (enEventosPendientes.Total - enEventosPendientes.Vigentes).ShouldBeGreaterThanOrEqualTo(2); // la propuesta y la aprobación (las de otras pruebas ya están con la clave del grupo)
        enEventosPendientes.Recifrados.ShouldBe(0);
        (await NumerosEnEventosAsync(tenant)).ShouldAllBe(n => n.Valor.StartsWith("vs1.", StringComparison.Ordinal));

        var recifrado = await ApiAsync<EstadoDeSecretosDto>("POST", "/api/v1/plataforma/secretos/recifrar", PlataformaPi, null, 200);
        var eventosRecifrados = recifrado.PorTipo.Single(t => t.Tipo == "cuenta-en-eventos");
        var vinculosRecifrados = recifrado.PorTipo.Single(t => t.Tipo == "cuenta-en-vinculos");
        eventosRecifrados.Recifrados.ShouldBeGreaterThanOrEqualTo(2);
        vinculosRecifrados.Recifrados.ShouldBeGreaterThanOrEqualTo(1);
        (eventosRecifrados.Fallidos, vinculosRecifrados.Fallidos).ShouldBe((0, 0));
        (await NumerosEnEventosAsync(tenant)).ShouldAllBe(n => campos.EstaVigente(tenant, n.Valor));
        (await NumerosEnEventosAsync(tenant)).ShouldAllBe(n => campos.Desproteger(tenant, n.Valor) == "03112345678");
        (await NumeroEnElVinculoAsync(tenant, vinculo.Id, "cuentaDesignada"))!.ShouldStartWith("vg1.");
        (await ApiAsync<CuentaDesignadaDelVinculoDto>("GET", url, Delegado(e.Norte, e.Carlos), null, 200)).Actual!.Numero.ShouldBe("03112345678");

        var otraPasada = await ApiAsync<EstadoDeSecretosDto>("POST", "/api/v1/plataforma/secretos/recifrar", PlataformaPi, null, 200);
        otraPasada.PorTipo.Single(t => t.Tipo == "cuenta-en-eventos").Recifrados.ShouldBe(0);
        otraPasada.PorTipo.Single(t => t.Tipo == "cuenta-en-vinculos").Recifrados.ShouldBe(0);
        var estado = otraPasada.PorTipo.Single(t => t.Tipo == "cuenta-en-eventos");
        (estado.Total - estado.Vigentes - estado.Fallidos).ShouldBe(0);

        // Un valor ilegible (su clave ya no está) se cuenta como fallido y se deja igual.
        await EscribirNumeroAsync(tenant, vinculo.Id, "cuentaDesignada", "vg1.00000000.AAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAAA", enEventos: true);
        var conIlegible = await ApiAsync<EstadoDeSecretosDto>("POST", "/api/v1/plataforma/secretos/recifrar", PlataformaPi, null, 200);
        conIlegible.PorTipo.Single(t => t.Tipo == "cuenta-en-eventos").Fallidos.ShouldBeGreaterThanOrEqualTo(1);
        (await NumerosEnEventosAsync(tenant)).ShouldContain(n => n.Valor.StartsWith("vg1.00000000.", StringComparison.Ordinal));
    }
}
