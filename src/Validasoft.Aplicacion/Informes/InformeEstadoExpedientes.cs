using JasperFx.Events;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Informes;

/// <summary>
/// Filtros del informe de estado. <see cref="Desde"/>/<see cref="Hasta"/> son días de calendario en la zona del negocio
/// (inclusive) sobre la apertura o, con <see cref="PorCierre"/>, sobre el cierre (aprobación, rechazo o anulación).
/// </summary>
public sealed record FiltrosEstadoExpedientes(DateOnly? Desde, DateOnly? Hasta, bool PorCierre, IReadOnlyList<EstadoExpediente> Estados,
    TipoExpediente? Tipo, Guid? AreaId, Guid? FlujoId, Guid? TipoVinculoId, Guid? TerceroId);

/// <summary>
/// Informe de estado de expedientes (IF-1, IF-2; docs/11 §H8 <c>/informes/estado-expedientes</c>): cada expediente de la
/// empresa con su estado, avance de requisitos y tiempos medidos sobre sus eventos (en carga, con el tercero, en revisión y
/// por área con SLA), y los totales. Se calcula al consultar (aún no es la proyección <c>InformeEstadoExpedientes</c>):
/// hasta <see cref="MaximoExpedientes"/> por consulta. Se descarga en Excel y en PDF con los mismos filtros.
/// </summary>
public static class InformeEstadoExpedientesEndpoints
{
    public const int MaximoExpedientes = 5000;

    /// <summary>Fila calculada con los tiempos sin redondear (para los promedios).</summary>
    internal sealed record Fila(Expediente Expediente, TiemposDeExpediente Tiempos, FilaEstadoExpedienteDto Dto);

    [WolverineGet("/api/v1/informes/estado-expedientes")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<InformeEstadoExpedientesDto> Consultar(SesionDeEmpresa empresa, TimeProvider reloj, DateOnly? desde, DateOnly? hasta,
        string? fecha, string? estado, string? tipo, Guid? areaId, Guid? flujoId, Guid? tipoVinculoId, Guid? terceroId, int? limite, string? cursor,
        CancellationToken ct)
    {
        var filtros = Leer(desde, hasta, fecha, estado, tipo, areaId, flujoId, tipoVinculoId, terceroId);
        var tamano = Math.Clamp(limite ?? 50, 1, 500);
        var salto = Peticiones.LeerDesplazamiento(cursor);
        var filas = await CalcularAsync(empresa, filtros, reloj.GetUtcNow(), ct);
        var siguiente = filas.Count > salto + tamano ? Peticiones.EscribirDesplazamiento(salto + tamano) : null;
        return new InformeEstadoExpedientesDto(Resumir(filas), [.. filas.Skip(salto).Take(tamano).Select(f => f.Dto)], siguiente);
    }

    /// <summary>Libro con tres hojas: Expedientes, Tiempos por área y Resumen.</summary>
    [WolverineGet("/api/v1/informes/estado-expedientes/excel")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<IResult> Excel(SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http, TimeProvider reloj, DateOnly? desde,
        DateOnly? hasta, string? fecha, string? estado, string? tipo, Guid? areaId, Guid? flujoId, Guid? tipoVinculoId, Guid? terceroId,
        CancellationToken ct)
    {
        var filtros = Leer(desde, hasta, fecha, estado, tipo, areaId, flujoId, tipoVinculoId, terceroId);
        var ahora = reloj.GetUtcNow();
        var filas = await CalcularAsync(empresa, filtros, ahora, ct);
        var resumen = Resumir(filas);
        var descripcion = await DescribirAsync(empresa, filtros, ct);

        var expedientes = new Hoja("Expedientes",
            ["Folio", "Tipo", "Estado", "Tercero", "Identificación", "Tipo de vínculo", "Flujo", "Versión del flujo", "Abierto", "Cerrado", "Requisitos",
                "Requisitos aprobados", "Devoluciones", "Días totales", "Días en carga", "Días con el tercero", "Días en revisión", "Días hasta aprobación",
                "SLA incumplido"],
            filas.Select(f => (IReadOnlyList<object?>)
            [
                f.Dto.Folio, TextoTipo(f.Dto.Tipo), TextoEstado(f.Dto.Estado), f.Dto.Tercero.Nombre, Identificacion(f.Dto.Tercero.Identificacion),
                f.Dto.TipoVinculo.Nombre, f.Dto.Flujo?.Nombre, f.Dto.Flujo?.Version, Exportaciones.Local(f.Dto.AbiertoEn),
                f.Dto.CerradoEn is { } c ? Exportaciones.Local(c) : null, f.Dto.Requisitos, f.Dto.RequisitosAprobados, f.Dto.Devoluciones,
                f.Dto.DiasTotales, f.Dto.DiasEnCarga, f.Dto.DiasConTercero, f.Dto.DiasEnRevision, f.Dto.DiasHastaAprobacion, f.Dto.SlaIncumplido ? "Sí" : "No"
            ]));
        var areas = new Hoja("Tiempos por área",
            ["Folio", "Tercero", "Etapa", "Área", "Estado del área", "Días en revisión", "Rondas", "SLA (horas)", "SLA incumplido"],
            filas.SelectMany(f => f.Dto.Areas.Select(a => (IReadOnlyList<object?>)
            [
                f.Dto.Folio, f.Dto.Tercero.Nombre, a.Etapa, a.Nombre, TextoEstadoArea(a.Estado), a.Dias, a.Rondas, a.SlaHoras, a.SlaIncumplido ? "Sí" : "No"
            ])));
        var hojaResumen = new Hoja("Resumen", ["Indicador", "Valor"],
        [
            .. descripcion.Select(d => (IReadOnlyList<object?>)[d.Etiqueta, d.Valor]),
            .. DatosDelResumen(resumen).Select(d => (IReadOnlyList<object?>)[d.Etiqueta, d.Valor]),
            .. resumen.PorArea.Select(a => (IReadOnlyList<object?>)[$"Área {a.Nombre}: promedio / mediana / máximo (días)",
                $"{Exportaciones.Dias(a.PromedioDias)} / {Exportaciones.Dias(a.MedianaDias)} / {Exportaciones.Dias(a.MaximoDias)} · {a.Expedientes} expedientes, {a.SlaIncumplidos} con SLA incumplido"])
        ]);

        var empresaDatos = await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct);
        var nombre = $"estado-expedientes-{Exportaciones.Slug(empresaDatos?.Nombre ?? "empresa")}-{CalculadoraDeVigenciaFecha(ahora)}.xlsx";
        return await Exportaciones.EntregarAsync(empresa, contexto, http, reloj,
            new ArchivoExportado("InformeEstadoExpedientes", "Excel", HojaDeCalculo.CrearLibro(expedientes, areas, hojaResumen), HojaDeCalculo.TipoContenido,
                nombre, filas.Count), ct);
    }

    /// <summary>El informe en PDF horizontal, con el estilo de los PDF del proyecto: resumen, tiempos por área y expedientes.</summary>
    [WolverineGet("/api/v1/informes/estado-expedientes/pdf")]
    [Authorize(Policy = Permisos.ExpedientesLeer)]
    public static async Task<IResult> Pdf(SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http, TimeProvider reloj,
        IGeneradorDePdfDeInformes generador, DateOnly? desde, DateOnly? hasta, string? fecha, string? estado, string? tipo, Guid? areaId, Guid? flujoId,
        Guid? tipoVinculoId, Guid? terceroId, CancellationToken ct)
    {
        var filtros = Leer(desde, hasta, fecha, estado, tipo, areaId, flujoId, tipoVinculoId, terceroId);
        var ahora = reloj.GetUtcNow();
        var filas = await CalcularAsync(empresa, filtros, ahora, ct);
        var resumen = Resumir(filas);
        var descripcion = await DescribirAsync(empresa, filtros, ct);
        var empresaDatos = await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct);

        var documento = new DocumentoPdf(empresaDatos?.Nombre ?? "Empresa", IdentificacionDe(empresaDatos), "Informe", "Estado de expedientes",
            string.Join(" · ", descripcion.Select(d => $"{d.Etiqueta}: {d.Valor}")), ahora,
            $"Generado el {Exportaciones.Momento(ahora)} · Validasoft", Horizontal: true,
            [
                new DatosPdf("Resumen", DatosDelResumen(resumen)),
                new TablaPdf("Tiempos por área (días en revisión)",
                    [new("Área", 3), new("Expedientes", 1.2), new("Pendientes", 1.2), new("Promedio", 1.2), new("Mediana", 1.2), new("Máximo", 1.2),
                        new("SLA incumplido", 1.4)],
                    [.. resumen.PorArea.Select(a => (IReadOnlyList<string>)
                    [
                        a.Nombre, a.Expedientes.ToString(), a.Pendientes.ToString(), Exportaciones.Dias(a.PromedioDias), Exportaciones.Dias(a.MedianaDias),
                        Exportaciones.Dias(a.MaximoDias), a.SlaIncumplidos.ToString()
                    ])],
                    "Ningún área ha revisado estos expedientes."),
                new TablaPdf("Expedientes",
                    [new("Folio", 2.3), new("Tercero", 3), new("Vínculo", 1.4), new("Tipo", 1.2), new("Estado", 1.3), new("Abierto", 1.5), new("Cerrado", 1.5),
                        new("Total", 0.8), new("Carga", 0.8), new("Tercero (obs.)", 1), new("Revisión", 0.9), new("SLA", 0.7)],
                    [.. filas.Select(f => (IReadOnlyList<string>)
                    [
                        f.Dto.Folio, $"{f.Dto.Tercero.Nombre} ({Identificacion(f.Dto.Tercero.Identificacion)})", f.Dto.TipoVinculo.Nombre, TextoTipo(f.Dto.Tipo),
                        TextoEstado(f.Dto.Estado), Exportaciones.Momento(f.Dto.AbiertoEn), f.Dto.CerradoEn is { } c ? Exportaciones.Momento(c) : "—",
                        Exportaciones.Dias(f.Dto.DiasTotales), Exportaciones.Dias(f.Dto.DiasEnCarga), Exportaciones.Dias(f.Dto.DiasConTercero),
                        Exportaciones.Dias(f.Dto.DiasEnRevision), f.Dto.SlaIncumplido ? "Sí" : "No"
                    ])],
                    "No hay expedientes con estos filtros."),
                new NotaPdf("Días con un decimal. Total = carga (el tercero aún cargaba) + con el tercero (devuelto por observaciones) + en revisión " +
                            "(en la empresa). Los tiempos del resumen son de los expedientes aprobados; los de cada área suman sus rondas de revisión.")
            ]);
        var nombre = $"estado-expedientes-{Exportaciones.Slug(empresaDatos?.Nombre ?? "empresa")}-{CalculadoraDeVigenciaFecha(ahora)}.pdf";
        return await Exportaciones.EntregarAsync(empresa, contexto, http, reloj,
            new ArchivoExportado("InformeEstadoExpedientes", "Pdf", generador.Generar(documento), Exportaciones.TipoPdf, nombre, filas.Count), ct);
    }

    internal static FiltrosEstadoExpedientes Leer(DateOnly? desde, DateOnly? hasta, string? fecha, string? estado, string? tipo, Guid? areaId,
        Guid? flujoId, Guid? tipoVinculoId, Guid? terceroId)
    {
        if (desde is not null && hasta is not null && desde > hasta)
            throw ReglaDeNegocioException.Validacion("rango-invalido", "La fecha inicial es posterior a la final.");
        var porCierre = fecha switch
        {
            null or "" => false,
            _ when fecha.Equals("Apertura", StringComparison.OrdinalIgnoreCase) => false,
            _ when fecha.Equals("Cierre", StringComparison.OrdinalIgnoreCase) => true,
            _ => throw ReglaDeNegocioException.Validacion("fecha-invalida", "El rango de fechas se aplica a la Apertura o al Cierre.")
        };

        var estados = new List<EstadoExpediente>();
        foreach (var valor in (estado ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            estados.Add(Enum.TryParse<EstadoExpediente>(valor, true, out var e) && Enum.IsDefined(e)
                ? e
                : throw ReglaDeNegocioException.Validacion("estado-invalido", $"'{valor}' no es un estado de expediente."));

        TipoExpediente? tipoExpediente = null;
        if (!string.IsNullOrWhiteSpace(tipo))
            tipoExpediente = Enum.TryParse<TipoExpediente>(tipo, true, out var t) && Enum.IsDefined(t)
                ? t
                : throw ReglaDeNegocioException.Validacion("tipo-invalido", $"'{tipo}' no es un tipo de expediente (Inicial, Renovacion o Actualizacion).");

        return new FiltrosEstadoExpedientes(desde, hasta, porCierre, estados, tipoExpediente, areaId, flujoId, tipoVinculoId, terceroId);
    }

    internal static async Task<IReadOnlyList<Fila>> CalcularAsync(SesionDeEmpresa empresa, FiltrosEstadoExpedientes f, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var consulta = empresa.Expedientes();
        if (f.Desde is { } d)
        {
            var inicio = Exportaciones.InicioDelDia(d);
            consulta = f.PorCierre ? consulta.Where(e => e.CerradoEn != null && e.CerradoEn >= inicio) : consulta.Where(e => e.AbiertoEn >= inicio);
        }
        if (f.Hasta is { } h)
        {
            var fin = Exportaciones.InicioDelDia(h.AddDays(1));
            consulta = f.PorCierre ? consulta.Where(e => e.CerradoEn != null && e.CerradoEn < fin) : consulta.Where(e => e.AbiertoEn < fin);
        }
        if (f.Estados.Count > 0)
        {
            var estados = f.Estados.ToArray();
            consulta = consulta.Where(e => e.Estado.IsOneOf(estados));
        }
        if (f.Tipo is { } tipo)
            consulta = consulta.Where(e => e.Tipo == tipo);
        if (f.AreaId is { } area)
            consulta = consulta.Where(e => e.AreaIds.Contains(area));
        if (f.TipoVinculoId is { } tipoVinculo)
            consulta = consulta.Where(e => e.TipoVinculoId == tipoVinculo);
        if (f.TerceroId is { } tercero)
            consulta = consulta.Where(e => e.TerceroId == tercero);

        var expedientes = (await consulta.OrderByDescending(e => e.AbiertoEn).ThenBy(e => e.Id).Take(MaximoExpedientes + 1).ToListAsync(ct))
            .Where(e => f.FlujoId is not { } flujo || e.Flujo?.FlujoId == flujo)
            .ToList();
        if (expedientes.Count > MaximoExpedientes)
            throw ReglaDeNegocioException.Validacion("informe-demasiado-grande",
                $"El informe admite hasta {MaximoExpedientes} expedientes: acota el rango de fechas o agrega filtros.");
        if (expedientes.Count == 0)
            return [];

        var ids = expedientes.Select(e => e.Id).ToArray();
        var eventos = (await empresa.Sesion.Events.QueryAllRawEvents().Where(e => e.StreamId.IsOneOf(ids)).ToListAsync(ct))
            .GroupBy(e => e.StreamId)
            .ToDictionary(g => g.Key, g => g.OrderBy(e => e.Version).Select(e => e.Data).ToList());

        return [.. expedientes.Select(e =>
        {
            var tiempos = Expediente.MedirTiempos(eventos.GetValueOrDefault(e.Id) ?? [], ahora);
            return new Fila(e, tiempos, ADto(e, tiempos));
        })];
    }

    private static FilaEstadoExpedienteDto ADto(Expediente e, TiemposDeExpediente t)
    {
        var requisitos = e.Requisitos.Where(r => r.Estado != EstadoRequisito.NoAplica).ToList();
        var areas = t.Areas.Select(a => new TiempoDeAreaDto(a.AreaId, a.Codigo, a.Nombre, a.Etapa, a.Estado.ToString(), Dias(a.Horas), a.Rondas, a.SlaHoras,
            a.SlaIncumplido)).ToList();
        return new FilaEstadoExpedienteDto(e.Id, e.Folio, e.Tipo.ToString(), e.Estado.ToString(), e.VinculoId,
            new TerceroResumenDto(e.TerceroId, e.TerceroNombre, e.TerceroIdentificacion),
            new TipoVinculoResumenDto(e.TipoVinculoId, e.TipoVinculoCodigo, e.TipoVinculoNombre),
            e.Flujo?.ADto(),
            e.AbiertoEn, e.CerradoEn, requisitos.Count, requisitos.Count(r => r.Estado is EstadoRequisito.Aprobado or EstadoRequisito.Eximido),
            t.Devoluciones, Dias(t.HorasTotales), Dias(t.HorasEnCarga), Dias(t.HorasConTercero), Dias(t.HorasEnRevision),
            t.HorasHastaAprobacion is { } aprobacion ? Dias(aprobacion) : null, areas.Any(a => a.SlaIncumplido), areas);
    }

    internal static ResumenEstadoExpedientesDto Resumir(IReadOnlyList<Fila> filas)
    {
        var aprobadas = filas.Where(f => f.Tiempos.HorasHastaAprobacion is not null).ToList();
        List<double> En(Func<TiemposDeExpediente, double> medida) => [.. aprobadas.Select(f => medida(f.Tiempos) / 24)];

        var porArea = filas
            .SelectMany(f => f.Tiempos.Areas.Select(a => (Fila: f, Area: a)))
            .GroupBy(x => x.Area.AreaId)
            .Select(g =>
            {
                var primero = g.First().Area;
                var revisadas = g.Where(x => x.Area.Rondas > 0).Select(x => x.Area.Horas / 24).ToList();
                return new ResumenDeAreaDto(g.Key, primero.Codigo, primero.Nombre, revisadas.Count,
                    g.Count(x => x.Fila.Expediente.Estado == EstadoExpediente.EnRevision && x.Area.Estado == EstadoAreaExpediente.Pendiente),
                    Estadistica.Promedio(revisadas), Estadistica.Mediana(revisadas), revisadas.Count == 0 ? null : Math.Round(revisadas.Max(), 1),
                    g.Count(x => x.Area.SlaIncumplido));
            })
            .OrderBy(a => a.Nombre, StringComparer.CurrentCulture)
            .ToList();

        return new ResumenEstadoExpedientesDto(filas.Count,
            [.. Enum.GetValues<EstadoExpediente>().Select(e => new ConteoDto(e.ToString(), filas.Count(f => f.Expediente.Estado == e))).Where(c => c.Cantidad > 0)],
            [.. Enum.GetValues<TipoExpediente>().Select(t => new ConteoDto(t.ToString(), filas.Count(f => f.Expediente.Tipo == t))).Where(c => c.Cantidad > 0)],
            aprobadas.Count,
            Estadistica.Promedio(En(t => t.HorasTotales)), Estadistica.Mediana(En(t => t.HorasTotales)),
            Estadistica.Promedio(En(t => t.HorasEnCarga)), Estadistica.Promedio(En(t => t.HorasConTercero)), Estadistica.Promedio(En(t => t.HorasEnRevision)),
            filas.Sum(f => f.Tiempos.Devoluciones), filas.Count(f => f.Dto.SlaIncumplido), porArea);
    }

    private static IReadOnlyList<DatoPdf> DatosDelResumen(ResumenEstadoExpedientesDto r) =>
    [
        new("Expedientes", r.Total.ToString()),
        new("Por estado", r.PorEstado.Count == 0 ? "—" : string.Join(", ", r.PorEstado.Select(c => $"{TextoEstado(c.Clave)}: {c.Cantidad}"))),
        new("Por tipo", r.PorTipo.Count == 0 ? "—" : string.Join(", ", r.PorTipo.Select(c => $"{TextoTipo(c.Clave)}: {c.Cantidad}"))),
        new("Aprobados", r.Aprobados.ToString()),
        new("Días hasta la aprobación (promedio)", Exportaciones.Dias(r.TiempoPromedioDias)),
        new("Días hasta la aprobación (mediana)", Exportaciones.Dias(r.TiempoMedianaDias)),
        new("De ellos, en carga / con el tercero / en revisión", $"{Exportaciones.Dias(r.PromedioEnCargaDias)} / {Exportaciones.Dias(r.PromedioConTerceroDias)} / {Exportaciones.Dias(r.PromedioEnRevisionDias)}"),
        new("Devoluciones al tercero", r.Devoluciones.ToString()),
        new("Expedientes con SLA incumplido", r.ConSlaIncumplido.ToString())
    ];

    /// <summary>Los filtros en palabras (con los nombres del área, el flujo, el tipo de vínculo y el tercero).</summary>
    private static async Task<IReadOnlyList<DatoPdf>> DescribirAsync(SesionDeEmpresa empresa, FiltrosEstadoExpedientes f, CancellationToken ct)
    {
        var lista = new List<DatoPdf>();
        var campo = f.PorCierre ? "Cerrados" : "Abiertos";
        if (f.Desde is not null || f.Hasta is not null)
            lista.Add(new(campo, $"{(f.Desde is { } d ? $"desde el {Exportaciones.Fecha(d)}" : string.Empty)}{(f.Desde is not null && f.Hasta is not null ? " " : string.Empty)}{(f.Hasta is { } h ? $"hasta el {Exportaciones.Fecha(h)}" : string.Empty)}"));
        if (f.Estados.Count > 0)
            lista.Add(new("Estado", string.Join(", ", f.Estados.Select(e => TextoEstado(e.ToString())))));
        if (f.Tipo is { } tipo)
            lista.Add(new("Tipo", TextoTipo(tipo.ToString())));
        if (f.AreaId is { } area)
            lista.Add(new("Área", (await empresa.CargarAreaAsync(area, ct)).Nombre));
        if (f.FlujoId is { } flujo)
            lista.Add(new("Flujo", (await empresa.CargarFlujoAsync(flujo, ct)).Nombre));
        if (f.TipoVinculoId is { } tipoVinculo)
            lista.Add(new("Tipo de vínculo", (await empresa.Sesion.LoadAsync<TipoVinculo>(tipoVinculo, ct))?.Nombre ?? tipoVinculo.ToString()));
        if (f.TerceroId is { } tercero)
            lista.Add(new("Tercero", (await empresa.Sesion.LoadAsync<Tercero>(tercero, ct))?.Nombre ?? tercero.ToString()));
        if (lista.Count == 0)
            lista.Add(new("Filtros", "Todos los expedientes"));
        return lista;
    }

    internal static string? IdentificacionDe(Empresa? empresa) =>
        empresa?.Identificacion is { } i ? $"{i.Tipo} {i.Numero}{(i.DigitoVerificacion is { } dv ? $"-{dv}" : string.Empty)}" : null;

    /// <summary><c>CO|NIT|900123456</c> → <c>NIT 900123456</c>.</summary>
    internal static string Identificacion(string clave) => clave.Split('|') is [_, var tipo, var numero] ? $"{tipo} {numero}" : clave;

    internal static string CalculadoraDeVigenciaFecha(DateTimeOffset ahora) =>
        Dominio.Documentos.CalculadoraDeVigencia.Fecha(ahora).ToString("yyyy-MM-dd", System.Globalization.CultureInfo.InvariantCulture);

    private static double Dias(double horas) => Math.Round(horas / 24, 1);

    internal static string TextoEstado(string estado) => estado switch
    {
        nameof(EstadoExpediente.Abierto) => "Abierto (en carga)",
        nameof(EstadoExpediente.EnRevision) => "En revisión",
        _ => estado
    };

    internal static string TextoTipo(string tipo) => tipo switch
    {
        nameof(TipoExpediente.Renovacion) => "Renovación",
        nameof(TipoExpediente.Actualizacion) => "Actualización",
        _ => tipo
    };

    private static string TextoEstadoArea(string estado) => estado switch
    {
        nameof(EstadoAreaExpediente.Esperando) => "Esperando su etapa",
        _ => estado
    };
}
