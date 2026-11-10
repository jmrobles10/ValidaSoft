using System.Text;
using Shouldly;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Invitaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Invitaciones;
using Validasoft.Dominio.Vinculos;

namespace Validasoft.Dominio.Tests;

/// <summary>
/// Lote de invitación masiva (LT-5…LT-8): ciclo de vida (avance por tandas, pausa, reanudación, cancelación, reintento de las
/// fallidas) y lectura de la plantilla (.xlsx y .csv, encabezados tolerantes) con la acción prevista por fila.
/// </summary>
public sealed class LoteDeInvitacionTests
{
    private static readonly DateTimeOffset Ahora = new(2026, 11, 6, 9, 0, 0, TimeSpan.Zero);
    private static readonly ActorDeLote Ana = new(ActorTipo.Usuario, Guid.NewGuid().ToString());

    private static LoteDeInvitacion Nuevo(int pendientes = 3, int invalidas = 1, int duplicadas = 1)
    {
        var creado = LoteDeInvitacion.Crear(Guid.NewGuid(), null, "proveedores.xlsx", 168,
            new TotalesDeLote(pendientes + invalidas + duplicadas, pendientes, 0, 0, duplicadas, invalidas), Ana, Ahora);
        return LoteDeInvitacion.Create(creado);
    }

    private static void Aplicar(LoteDeInvitacion lote, IEnumerable<object> eventos)
    {
        foreach (var evento in eventos)
            switch (evento)
            {
                case AvanceDeLoteRegistrado e: lote.Apply(e); break;
                case LotePausado e: lote.Apply(e); break;
                case LoteReanudado e: lote.Apply(e); break;
                case LoteCancelado e: lote.Apply(e); break;
                case FilasFallidasReintentadas e: lote.Apply(e); break;
                case LoteTerminado e: lote.Apply(e); break;
            }
    }

    private static string Codigo(Action accion) => Should.Throw<ReglaDeNegocioException>(accion).Codigo;

    [Fact]
    public void Nace_procesando_con_el_nombre_del_archivo_y_sin_filas_validas_no_se_crea()
    {
        var lote = Nuevo();
        lote.Estado.ShouldBe(EstadoLote.Procesando);
        lote.Nombre.ShouldBe("proveedores.xlsx");
        lote.Ejecucion.ShouldNotBeNull();

        Codigo(() => LoteDeInvitacion.Crear(Guid.NewGuid(), "x", null, 168, new TotalesDeLote(2, 0, 0, 0, 1, 1), Ana, Ahora))
            .ShouldBe("lote-sin-filas-validas");
        Codigo(() => LoteDeInvitacion.Crear(Guid.NewGuid(), "x", null, 0, new TotalesDeLote(1, 1, 0, 0, 0, 0), Ana, Ahora))
            .ShouldBe("vigencia-invalida");
    }

    [Fact]
    public void Avanza_por_tandas_y_termina_con_errores_si_alguna_fallo()
    {
        var lote = Nuevo();
        var avance = lote.RegistrarAvance(new TotalesDeLote(5, 1, 2, 0, 1, 1), Ahora);
        avance.ShouldHaveSingleItem().ShouldBeOfType<AvanceDeLoteRegistrado>();
        Aplicar(lote, avance);
        lote.Tandas.ShouldBe(1);

        var fin = lote.RegistrarAvance(new TotalesDeLote(5, 0, 2, 1, 1, 1), Ahora);
        fin.Count.ShouldBe(2);
        fin[1].ShouldBeOfType<LoteTerminado>().Estado.ShouldBe(EstadoLote.CompletadoConErrores);
        Aplicar(lote, fin);
        lote.EsTerminal.ShouldBeTrue();
        lote.Ejecucion.ShouldBeNull();
        lote.RegistrarAvance(new TotalesDeLote(5, 0, 3, 0, 1, 1), Ahora).ShouldBeEmpty(); // terminado: nada

        // Reintentar las fallidas: vuelve a procesar solo esas, con una ejecución nueva.
        var reintento = lote.ReintentarFallidas(1, Ahora);
        Aplicar(lote, [reintento]);
        lote.Estado.ShouldBe(EstadoLote.Procesando);
        lote.Totales.Pendientes.ShouldBe(1);
        lote.Totales.Fallidas.ShouldBe(0);
        lote.Ejecucion.ShouldBe(reintento.Ejecucion);
        Aplicar(lote, lote.RegistrarAvance(new TotalesDeLote(5, 0, 3, 0, 1, 1), Ahora));
        lote.Estado.ShouldBe(EstadoLote.Completado);
        Codigo(() => lote.ReintentarFallidas(0, Ahora)).ShouldBe("sin-filas-fallidas");
    }

    [Fact]
    public void Pausa_reanuda_con_otra_ejecucion_y_cancelado_es_terminal()
    {
        var lote = Nuevo();
        var primera = lote.Ejecucion;
        Codigo(() => lote.Reanudar(Ahora)).ShouldBe("estado-invalido");
        Aplicar(lote, [lote.Pausar(Ahora)]);
        lote.Estado.ShouldBe(EstadoLote.Pausado);
        lote.Ejecucion.ShouldBeNull();
        Codigo(() => lote.Pausar(Ahora)).ShouldBe("estado-invalido");
        Codigo(() => lote.ReintentarFallidas(1, Ahora)).ShouldBe("estado-invalido");

        Aplicar(lote, [lote.Reanudar(Ahora)]);
        lote.Estado.ShouldBe(EstadoLote.Procesando);
        lote.Ejecucion.ShouldNotBe(primera);

        Aplicar(lote, [lote.Cancelar(new TotalesDeLote(5, 2, 1, 0, 1, 1), Ahora)]);
        lote.Estado.ShouldBe(EstadoLote.Cancelado);
        Codigo(() => lote.Pausar(Ahora)).ShouldBe("lote-en-estado-terminal");
        Codigo(() => lote.Cancelar(lote.Totales, Ahora)).ShouldBe("lote-en-estado-terminal");
        Codigo(() => lote.ReintentarFallidas(1, Ahora)).ShouldBe("lote-en-estado-terminal");
    }

    [Fact]
    public void La_fila_registra_su_resultado()
    {
        var fila = new FilaDeLote { Id = FilaDeLote.Clave(Guid.NewGuid(), 7), N = 7, Estado = EstadoFilaLote.Pendiente };
        fila.Id.ShouldEndWith(":000007");
        fila.MarcarFallida(new ErrorDeFila(null, "error-inesperado", "x"), Ahora);
        fila.Estado.ShouldBe(EstadoFilaLote.Fallida);
        fila.Intentos.ShouldBe(1);
        fila.MarcarExitosa(AccionFilaLote.Invitado, Guid.NewGuid(), Guid.NewGuid(), true, null, Ahora);
        fila.Estado.ShouldBe(EstadoFilaLote.Exitosa);
        fila.Errores.ShouldBeEmpty();
        fila.Intentos.ShouldBe(2);
    }

    [Fact]
    public void Lee_la_plantilla_en_excel_con_los_encabezados_que_genera()
    {
        var (contenido, _, nombre) = ArchivoDeLote.Plantilla("xlsx");
        nombre.ShouldEndWith(".xlsx");
        LectorDeHojaDeCalculo.Leer(contenido).ShouldHaveSingleItem().ShouldBe(ArchivoDeLote.Columnas.Select(c => c.Encabezado));

        // Un libro con filas (el escritor del informe) se lee igual: texto, números y celdas vacías intermedias.
        var libro = HojaDeCalculo.Crear("Hoja", [.. ArchivoDeLote.Columnas.Select(c => c.Encabezado)],
        [
            ["NIT", 900123456, "Aceros S.A.S.", "compras@aceros.co", "PROVEEDOR", null, "7"],
            ["CC", "1020304050", "Ana Gómez", "ana@correo.co", "PROVEEDOR"]
        ]);
        var filas = ArchivoDeLote.Leer(new ArchivoDeLoteDto("lote.xlsx", Convert.ToBase64String(libro)));
        filas.Count.ShouldBe(2);
        filas[0].ShouldBe(new FilaDeLoteEntrada(null, "NIT", "900123456", "7", null, "Aceros S.A.S.", null, "compras@aceros.co", "PROVEEDOR", null, null, null));
        filas[1].NumeroIdentificacion.ShouldBe("1020304050");
    }

    [Fact]
    public void Lee_csv_con_punto_y_coma_comillas_y_encabezados_sin_tildes()
    {
        var csv = "﻿tipo_identificacion;NUMERO DE IDENTIFICACION;Razon social;e-mail;Tipo vínculo;Columna extra\r\n" +
                  "NIT;900.123.456-7;\"Aceros; y Cía\";compras@aceros.co;proveedor;ignorada\r\n" +
                  ";;;;;\r\n" +
                  "CC;1020304050;Ana;ana@correo.co;PROVEEDOR;\n";
        var filas = ArchivoDeLote.Leer(new ArchivoDeLoteDto("lote.csv", Convert.ToBase64String(Encoding.UTF8.GetBytes(csv))));
        filas.Count.ShouldBe(2);
        filas[0].Nombre.ShouldBe("Aceros; y Cía");
        filas[0].NumeroIdentificacion.ShouldBe("900.123.456-7");
        filas[0].TipoVinculo.ShouldBe("proveedor");
        filas[1].Correo.ShouldBe("ana@correo.co");
    }

    [Theory]
    [InlineData("lote.pdf", "formato-invalido")]
    [InlineData("lote.xlsx", "archivo-invalido")]
    public void Rechaza_lo_que_no_es_la_plantilla(string nombre, string codigo) =>
        Codigo(() => ArchivoDeLote.Leer(new ArchivoDeLoteDto(nombre, Convert.ToBase64String(Encoding.UTF8.GetBytes("hola")))))
            .ShouldBe(codigo);

    [Fact]
    public void Sin_las_columnas_obligatorias_dice_cuales_faltan()
    {
        var e = Should.Throw<ReglaDeNegocioException>(() =>
            ArchivoDeLote.Leer(new ArchivoDeLoteDto("lote.csv", Convert.ToBase64String(Encoding.UTF8.GetBytes("nit,correo\n1,a@b.co\n")))));
        e.Codigo.ShouldBe("plantilla-invalida");
        e.Message.ShouldContain("Tipo de vínculo");
    }

    [Fact]
    public void La_accion_prevista_depende_del_vinculo_que_ya_existe()
    {
        var vinculo = Vinculo.Create(Vinculo.Crear(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid(), null, new(), OrigenVinculo.Invitacion, null, "t@x.co", Ahora));
        ValidacionDeLote.AccionPrevista(null).Accion.ShouldBe("Invitado");
        ValidacionDeLote.AccionPrevista(vinculo).Accion.ShouldBe("Reinvitado");
        vinculo.Apply(new InvitacionAceptada("t@x.co", Ahora));
        ValidacionDeLote.AccionPrevista(vinculo).Accion.ShouldBe("SinCambios");
        vinculo.Apply(vinculo.Bloquear("Fraude", null, Ahora));
        ValidacionDeLote.AccionPrevista(vinculo).Error!.Codigo.ShouldBe("vinculo-no-invitable");
    }
}
