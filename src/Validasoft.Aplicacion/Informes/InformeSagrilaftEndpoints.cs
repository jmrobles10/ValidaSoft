using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Verificaciones;
using Validasoft.Contratos;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Ficha;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Informes;

/// <summary>
/// Informe SAGRILAFT completo (CU-14 del informe; docs/01 §10): junta lo que el SRM reparte en dos informes —el tablero del oficial
/// (totales, tendencia de 12 meses, distribución del riesgo, vencidos y por vencer) y el detalle por tercero (información general,
/// oficial, debida diligencia, personas relacionadas con PEP y beneficiario final)— y agrega lo que no tenía: tiempos, la periodicidad
/// que fija el nivel de riesgo y la evidencia de cada verificación. Es el estado <b>actual</b> de cada tercero de la empresa (su
/// última consulta de listas y su último estudio); los que no tienen estudio salen como <c>SinEstudio</c>. Se calcula al consultar
/// y es privado de la empresa. Excel con tres hojas: Terceros, Personas y Verificaciones.
/// </summary>
public static class InformeSagrilaftEndpoints
{
    public const int MaximoExpedientes = 5000;
    public const int DiasDeAviso = 30;

    /// <summary>
    /// Si la empresa no tiene matriz, cada cuántos meses se vuelve a hacer la debida diligencia de un tercero (el SRM usaba un año
    /// por omisión: <c>Proxconsultsagrilaft</c> = 365).
    /// </summary>
    public const int MesesRevisionPorOmision = 12;

    /// <summary>El informe al día <c>hasta</c> (por omisión hoy; la tendencia son los 12 meses que terminan ese día).</summary>
    [WolverineGet("/api/v1/informes/sagrilaft")]
    [Authorize(Policy = Permisos.AuditoriaLeer)]
    public static async Task<InformeSagrilaftDto> Obtener(DateOnly? hasta, SesionDeEmpresa empresa, TimeProvider reloj, CancellationToken ct) =>
        (await CalcularAsync(empresa, hasta, reloj.GetUtcNow(), ct)).Informe;

    /// <summary>Las personas relacionadas de un tercero con su identificación, participación, PEP y si es beneficiario final (desde 5 %).</summary>
    [WolverineGet("/api/v1/informes/sagrilaft/terceros/{terceroId}/personas")]
    [Authorize(Policy = Permisos.AuditoriaLeer)]
    public static async Task<PersonasDelTerceroDto> Personas(Guid terceroId, SesionDeEmpresa empresa, CancellationToken ct)
    {
        var vinculo = await empresa.Vinculos().Where(v => v.TerceroId == terceroId).FirstOrDefaultAsync(ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "La empresa no tiene un vínculo con ese tercero.");
        var tercero = await empresa.Sesion.LoadAsync<Tercero>(terceroId, ct)
                      ?? throw ReglaDeNegocioException.NoEncontrado("tercero-no-encontrado", "El tercero no existe.");
        using var boveda = empresa.ConsultaBoveda();
        var personas = (await PersonasDeLosTercerosAsync(boveda, [vinculo.TerceroId], ct)).GetValueOrDefault(terceroId) ?? [];
        return new PersonasDelTerceroDto(terceroId, tercero.Nombre, personas);
    }

    /// <summary>El mismo informe en Excel (hojas Terceros, Personas y Verificaciones).</summary>
    [WolverineGet("/api/v1/informes/sagrilaft/excel")]
    [Authorize(Policy = Permisos.AuditoriaLeer)]
    public static async Task<IResult> Excel(DateOnly? hasta, SesionDeEmpresa empresa, TimeProvider reloj, CancellationToken ct)
    {
        var (informe, personas) = await CalcularAsync(empresa, hasta, reloj.GetUtcNow(), ct);
        var verificaciones = await InformeCumplimientoEndpoints.CalcularAsync(empresa, informe.Hasta.AddYears(-10), informe.Hasta, reloj.GetUtcNow(), ct);

        string[] encabezadoTerceros =
        [
            "Folio", "Tercero", "Tipo de identificación", "Identificación", "Tipo de persona", "Tipo de vínculo", "País", "Ciudad", "Correo", "Actividad (CIIU)",
            "Estado del vínculo", "Última consulta de listas", "Proveedor", "Estado de listas", "Coincidencias por analizar", "Fecha del estudio", "Oficial",
            "Nivel de riesgo", "Debida diligencia", "Concepto", "Conflicto de interés", "Próxima debida diligencia", "Días para la próxima", "Estado de la debida diligencia",
            "Personas", "PEP", "PEP sin declaración"
        ];
        var terceros = informe.Filas.Select(f => (IReadOnlyList<object?>)
        [
            f.Folio, f.Tercero, f.TipoIdentificacion, f.Identificacion, f.TipoPersona, f.TipoVinculo, f.Pais, f.Ciudad, f.Correo, f.Actividad, f.EstadoVinculo,
            Fecha(f.UltimaConsultaListas), f.ProveedorListas, f.EstadoListas, f.CoincidenciasPorAnalizar, Fecha(f.FechaEstudio), f.Oficial, f.NivelRiesgo,
            f.DebidaDiligencia, f.Concepto, f.ConflictoDeInteres is { } c ? (c ? "Sí" : "No") : null, Fecha(f.ProximaRevision), f.DiasParaRevision,
            f.EstadoDebidaDiligencia, f.Personas, f.Pep, f.PepSinDeclaracion
        ]);
        string[] encabezadoPersonas = ["Tercero", "Identificación del tercero", "Rol", "Tipo de identificación", "Identificación", "Nombre", "Participación (%)", "PEP", "Vínculo PEP", "Beneficiario final", "Declaración PEP"];
        var filasPersonas = informe.Filas
            .SelectMany(f => personas.GetValueOrDefault(f.TerceroId, []).Select(p => (IReadOnlyList<object?>)
            [
                f.Tercero, f.Identificacion, p.Rol, p.TipoIdentificacion, p.Identificacion, p.Nombre, p.Participacion, p.EsPep ? "Sí" : "No",
                p.VinculoPep ? "Sí" : "No", p.BeneficiarioFinal ? "Sí" : "No", p.EsPep ? (p.DeclaracionPep ? "Sí" : "No") : "—"
            ]));
        string[] encabezadoVerificaciones =
        [
            "Folio", "Tercero", "Identificación", "Verificación", "Proveedor", "Estado", "Resultado", "Fecha", "Personas consultadas", "Coincidencias", "Confirmadas",
            "Descartadas", "Nivel de riesgo", "Conflicto de interés", "Debida diligencia", "Concepto", "Vigente hasta"
        ];
        var filasVerificaciones = verificaciones.Filas.Select(f => (IReadOnlyList<object?>)
        [
            f.Folio, f.Tercero, f.Identificacion, f.Verificacion == nameof(TipoVerificacion.ListasRestrictivas) ? "Listas restrictivas" : "Estudio de cumplimiento",
            f.Proveedor, f.Estado, f.Resultado, Fecha(f.Fecha), f.Personas, f.Coincidencias, f.Confirmadas, f.Descartadas, f.NivelRiesgo,
            f.ConflictoDeInteres is { } c ? (c ? "Sí" : "No") : null, f.DebidaDiligencia, f.Concepto, Fecha(f.VigenteHasta)
        ]);

        var bytes = HojaDeCalculo.CrearLibro(new Hoja("Terceros", encabezadoTerceros, terceros), new Hoja("Personas", encabezadoPersonas, filasPersonas),
            new Hoja("Verificaciones", encabezadoVerificaciones, filasVerificaciones));
        return Results.File(bytes, HojaDeCalculo.TipoContenido, $"informe-sagrilaft-completo-{informe.Hasta:yyyyMMdd}.xlsx");
    }

    private static object? Fecha(DateTimeOffset? fecha) => fecha is { } f ? CalculadoraDeVigencia.Fecha(f) : null;

    public static async Task<(InformeSagrilaftDto Informe, IReadOnlyDictionary<Guid, IReadOnlyList<PersonaInformeDto>> Personas)> CalcularAsync(
        SesionDeEmpresa empresa, DateOnly? hasta, DateTimeOffset ahora, CancellationToken ct)
    {
        var fin = hasta ?? CalculadoraDeVigencia.Fecha(ahora);
        var inicio = fin.AddMonths(-11).AddDays(1 - fin.Day);

        var expedientes = await empresa.Expedientes().Where(e => e.ConVerificaciones).OrderBy(e => e.AbiertoEn).Take(MaximoExpedientes).ToListAsync(ct);
        var porVinculo = expedientes.GroupBy(e => e.VinculoId).ToList();
        var vinculoIds = porVinculo.Select(g => g.Key).ToArray();
        var vinculos = vinculoIds.Length == 0 ? [] : (await empresa.Sesion.LoadManyAsync<Vinculo>(ct, vinculoIds)).ToDictionary(v => v.Id);
        var terceroIds = porVinculo.Select(g => g.First().TerceroId).Distinct().ToArray();
        var terceros = terceroIds.Length == 0 ? [] : (await empresa.Sesion.LoadManyAsync<Tercero>(ct, terceroIds)).ToDictionary(t => t.Id);
        var monitoreos = vinculoIds.Length == 0 ? [] : (await empresa.Sesion.LoadManyAsync<MonitoreoDeListas>(ct, vinculoIds)).ToDictionary(m => m.Id);
        var matriz = await empresa.Sesion.LoadAsync<Dominio.Cumplimiento.MatrizDeRiesgoEmpresa>(empresa.EmpresaId, ct);

        using var boveda = empresa.ConsultaBoveda();
        var personas = await PersonasDeLosTercerosAsync(boveda, terceroIds, ct);
        var generales = await GeneralesAsync(boveda, terceroIds, ct);

        var filas = new List<FilaTerceroSagrilafDto>();
        foreach (var grupo in porVinculo)
        {
            var ultimo = grupo.OrderBy(e => e.AbiertoEn).Last();
            if (!vinculos.TryGetValue(grupo.Key, out var vinculo) || !terceros.TryGetValue(ultimo.TerceroId, out var tercero))
                continue;

            var listas = grupo.Select(e => e.BuscarVerificacion(TipoVerificacion.ListasRestrictivas)).OfType<VerificacionDeExpediente>()
                .Where(v => v.RegistradaEn is not null).OrderBy(v => v.RegistradaEn).LastOrDefault();
            if (monitoreos.TryGetValue(grupo.Key, out var monitoreo) && monitoreo.Listas.RegistradaEn is { } enMonitoreo && (listas?.RegistradaEn is not { } enListas || enMonitoreo > enListas))
                listas = monitoreo.Listas;
            var estudio = grupo.Select(e => e.BuscarVerificacion(TipoVerificacion.EstudioDeCumplimiento)).OfType<VerificacionDeExpediente>()
                .Where(v => v.Estudio is not null).OrderBy(v => v.Estudio!.Fecha).LastOrDefault()?.Estudio;

            var (estadoListas, _) = EstadoPorFecha(listas?.VigenteHasta, listas is null, ahora);
            DateTimeOffset? proxima = null;
            int? dias = null;
            var estadoDd = "SinEstudio";
            if (estudio is not null)
            {
                var meses = estudio.Datos.Matriz?.MesesRevision
                            ?? matriz?.Niveles.FirstOrDefault(n => n.Nivel == estudio.Datos.NivelRiesgo)?.MesesRevision ?? MesesRevisionPorOmision;
                proxima = estudio.Fecha.AddMonths(meses);
                dias = (int)Math.Floor((proxima.Value - ahora).TotalDays);
                (estadoDd, _) = EstadoPorFecha(proxima, false, ahora);
                if (estadoDd == "SinConsulta")
                    estadoDd = "SinEstudio";
            }

            var datos = generales.GetValueOrDefault(tercero.Id);
            var delTercero = personas.GetValueOrDefault(tercero.Id) ?? [];
            filas.Add(new FilaTerceroSagrilafDto(vinculo.Id, tercero.Id, ultimo.Folio, tercero.Nombre, tercero.Identificacion.Tipo, tercero.Identificacion.Numero,
                tercero.TipoPersona.ToString(), ultimo.TipoVinculoNombre, datos?.Pais ?? tercero.Identificacion.Pais, datos?.Ciudad, datos?.Correo ?? vinculo.CorreoTercero,
                datos?.CiiuPrincipal, vinculo.Estado.ToString(), listas?.RegistradaEn, listas?.Proveedor, estadoListas,
                listas?.Coincidencias.Count(x => !x.Resuelta) ?? 0, estudio?.Fecha, estudio?.Por, estudio?.Datos.NivelRiesgo, estudio?.Datos.DebidaDiligencia.ToString(),
                estudio?.Datos.Concepto.ToString(), estudio?.Datos.ConflictoDeInteres, proxima, dias, estadoDd, delTercero.Count, delTercero.Count(p => p.EsPep), delTercero.Count(p => p.EsPep && !p.DeclaracionPep)));
        }

        var riesgo = filas.Where(f => f.NivelRiesgo is not null).GroupBy(f => f.NivelRiesgo!.Value).OrderBy(g => g.Key).Select(g => new RiesgoDto(g.Key, g.Count())).ToList();
        var tendencia = Enumerable.Range(0, 12).Select(i => inicio.AddMonths(i)).Select(mes =>
        {
            var clave = $"{mes.Year:0000}-{mes.Month:00}";
            bool DelMes(DateTimeOffset? f) => f is { } x && CalculadoraDeVigencia.Fecha(x) is var d && d.Year == mes.Year && d.Month == mes.Month;
            var estudios = expedientes.SelectMany(e => e.Verificaciones).Where(v => v.Tipo == TipoVerificacion.EstudioDeCumplimiento)
                .Sum(v => (DelMes(v.Estudio?.Fecha) ? 1 : 0) + v.Anteriores.Count(a => a.Proveedor == "Oficial de cumplimiento" && DelMes(a.Fecha)));
            var consultas = expedientes.SelectMany(e => e.Verificaciones).Where(v => v.Tipo == TipoVerificacion.ListasRestrictivas)
                .Sum(v => (DelMes(v.RegistradaEn) ? 1 : 0) + v.Anteriores.Count(a => DelMes(a.Fecha)));
            return new MesDeCumplimientoDto(clave, estudios, consultas);
        }).ToList();

        var tiempos = Tiempos(expedientes, ahora);
        var informe = new InformeSagrilaftDto(inicio, fin, filas.Count, filas.Count(f => f.EstadoDebidaDiligencia is "Vigente" or "PorVencer"),
            filas.Count(f => f.EstadoDebidaDiligencia == "PorVencer"), filas.Count(f => f.EstadoDebidaDiligencia == "Vencida"),
            filas.Count(f => f.EstadoListas == "PorVencer"), filas.Count(f => f.EstadoListas == "Vencida"), filas.Count(f => f.EstadoDebidaDiligencia == "SinEstudio"),
            riesgo, tendencia, tiempos, filas.OrderBy(f => f.DiasParaRevision ?? int.MaxValue).ThenBy(f => f.Tercero).ToList());
        return (informe, personas);
    }

    /// <summary>Vigente, por vencer (dentro de <see cref="DiasDeAviso"/> días), vencida o sin consulta.</summary>
    private static (string Estado, int? Dias) EstadoPorFecha(DateTimeOffset? hasta, bool sinDato, DateTimeOffset ahora)
    {
        if (sinDato || hasta is null)
            return ("SinConsulta", null);
        var dias = (int)Math.Floor((hasta.Value - ahora).TotalDays);
        return hasta <= ahora ? ("Vencida", dias) : dias <= DiasDeAviso ? ("PorVencer", dias) : ("Vigente", dias);
    }

    private static TiemposDeCumplimientoDto Tiempos(IReadOnlyList<Expediente> expedientes, DateTimeOffset ahora)
    {
        var duraciones = expedientes.SelectMany(e => e.Verificaciones.Where(v => v.Estado == EstadoVerificacion.Satisfecha && v.RegistradaEn is not null)
                .Select(v => (v.RegistradaEn!.Value - e.AbiertoEn).TotalDays))
            .Where(d => d >= 0).ToList();
        var abiertas = expedientes.Where(e => e.PendienteDeCumplimiento).ToList();
        return new TiemposDeCumplimientoDto(duraciones.Count == 0 ? null : Math.Round((decimal)duraciones.Average(), 1), duraciones.Count, abiertas.Count,
            abiertas.Count(e => (ahora - e.AbiertoEn).TotalDays > 7));
    }

    internal static async Task<Dictionary<Guid, IReadOnlyList<PersonaInformeDto>>> PersonasDeLosTercerosAsync(IQuerySession boveda, Guid[] terceroIds, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, IReadOnlyList<PersonaInformeDto>>();
        if (terceroIds.Length == 0)
            return resultado;
        var secciones = await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId.IsOneOf(terceroIds) && s.Seccion == CodigosDeSeccion.Legal).ToListAsync(ct);
        foreach (var seccion in secciones.Where(s => s.Vigente is not null))
        {
            var datos = JsonDeFicha.Deserializar<DatosLegales>(seccion.Vigente!.Datos);
            resultado[seccion.TerceroId] =
            [
                .. (datos.Personas ?? []).Select((p, indice) => new PersonaInformeDto(p.Rol?.ToString() ?? "Persona", p.TipoIdentificacion ?? string.Empty, p.Numero ?? string.Empty,
                    string.Join(' ', new[] { p.Nombres, p.Apellidos }.Where(n => !string.IsNullOrWhiteSpace(n))).Trim(), p.Porcentaje, p.Pep == true, p.VinculoPep == true,
                    p.Rol == RolPersonaRelacionada.Socio && p.Porcentaje >= 5m, p.Pep == true && p.DeclaracionPep is not null, indice))
            ];
        }

        // La persona natural no tiene información legal: su PEP es el propio (la General) y es la persona que se muestra (DP-12).
        var generales = await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId.IsOneOf(terceroIds) && s.Seccion == CodigosDeSeccion.General).ToListAsync(ct);
        foreach (var seccion in generales.Where(s => s.Vigente is not null && !resultado.ContainsKey(s.TerceroId)))
        {
            var general = JsonDeFicha.Deserializar<DatosGenerales>(seccion.Vigente!.Datos);
            if (general.TipoPersona == TipoPersona.Natural && general.Pep == true)
                resultado[seccion.TerceroId] =
                [
                    new PersonaInformeDto("Tercero", general.TipoIdentificacion ?? string.Empty, general.Numero ?? string.Empty,
                        string.Join(' ', new[] { general.Nombres, general.Apellidos }.Where(n => !string.IsNullOrWhiteSpace(n))).Trim(), null, true,
                        general.VinculoPep == true, false, general.DeclaracionPep is not null, -1)
                ];
        }

        return resultado;
    }

    private static async Task<Dictionary<Guid, DatosGenerales>> GeneralesAsync(IQuerySession boveda, Guid[] terceroIds, CancellationToken ct)
    {
        var resultado = new Dictionary<Guid, DatosGenerales>();
        if (terceroIds.Length == 0)
            return resultado;
        var secciones = await boveda.Query<SeccionDeFicha>().Where(s => s.TerceroId.IsOneOf(terceroIds) && s.Seccion == CodigosDeSeccion.General).ToListAsync(ct);
        foreach (var seccion in secciones.Where(s => s.Vigente is not null))
            resultado[seccion.TerceroId] = JsonDeFicha.Deserializar<DatosGenerales>(seccion.Vigente!.Datos);
        return resultado;
    }
}
