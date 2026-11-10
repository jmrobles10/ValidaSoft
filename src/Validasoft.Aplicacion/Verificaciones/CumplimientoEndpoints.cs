using System.Text.RegularExpressions;
using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Contratos;
using Validasoft.Dominio.Auditoria;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Verificaciones;

/// <summary>
/// Listas restrictivas de la empresa activa (VE-7, VE-11): proveedor (Infolaft, el principal entre los reales), credenciales
/// propias cifradas con <see cref="IProtectorDeSecretos"/> (la clave es de solo escritura: vuelve «configurada · …1234») y
/// consumo del mes por proveedor. Todo cambio queda en la auditoría, sin la clave.
/// </summary>
public static partial class ListasRestrictivasEndpoints
{
    public const string Entidad = "ListasRestrictivasEmpresa";

    /// <summary>Configuración y consumo del mes <c>mes</c> (<c>yyyy-MM</c>, por omisión el actual). 422 <c>mes-invalido</c>.</summary>
    [WolverineGet("/api/v1/configuracion/listas-restrictivas")]
    [Authorize(Policy = Permisos.ConfiguracionLeer)]
    public static async Task<ListasRestrictivasConfigDto> Obtener(string? mes, SesionDeEmpresa empresa, ConsultaDeListas consulta, TimeProvider reloj,
        CancellationToken ct)
    {
        var periodo = string.IsNullOrWhiteSpace(mes) ? UsoMensualListas.MesDe(CalculadoraDeVigencia.Fecha(reloj.GetUtcNow())) : mes.Trim();
        if (!RegexMes().IsMatch(periodo))
            throw ReglaDeNegocioException.Validacion("mes-invalido", "El mes debe tener la forma AAAA-MM (por ejemplo 2026-11).");
        var configuracion = await empresa.Sesion.LoadAsync<ListasRestrictivasEmpresa>(empresa.EmpresaId, ct);
        return await DtoAsync(empresa, configuracion, consulta, periodo, ct);
    }

    /// <summary>
    /// Cambia las credenciales de un proveedor real y/o el proveedor de la empresa (<c>proveedor: ""</c> = el de la
    /// instalación). 422 <c>proveedor-invalido</c>, <c>proveedor-no-habilitado</c> (la instalación no lo habilitó),
    /// <c>credenciales-requeridas</c>, <c>usuario-requerido</c>, <c>clave-requerida</c>, <c>clave-invalida</c>, <c>url-invalida</c>,
    /// <c>cuota-invalida</c>, <c>modo-busqueda-invalido</c>.
    /// </summary>
    [WolverinePut("/api/v1/configuracion/listas-restrictivas")]
    [Authorize(Policy = Permisos.ConfiguracionEscribir)]
    public static async Task<ListasRestrictivasConfigDto> Actualizar(ActualizarListasRestrictivasDto dto, SesionDeEmpresa empresa, IContextoSolicitud contexto,
        ConsultaDeListas consulta, IProtectorDeSecretos protector, TimeProvider reloj, CancellationToken ct)
    {
        var ahora = reloj.GetUtcNow();
        var mes = UsoMensualListas.MesDe(CalculadoraDeVigencia.Fecha(ahora));
        var existente = await empresa.Sesion.LoadAsync<ListasRestrictivasEmpresa>(empresa.EmpresaId, ct);
        var antes = existente is null ? null : await DtoAsync(empresa, existente, consulta, mes, ct);
        var configuracion = existente ?? ListasRestrictivasEmpresa.Nueva(empresa.EmpresaId);

        if (dto.Credenciales is { } c)
        {
            ModoBusquedaListas? modo = string.IsNullOrWhiteSpace(c.ModoBusqueda) ? null
                : Enum.TryParse<ModoBusquedaListas>(c.ModoBusqueda, true, out var m) && Enum.IsDefined(m) ? m
                : throw ReglaDeNegocioException.Validacion("modo-busqueda-invalido", "El modo de búsqueda es PorNombre o PorDocumento.");
            configuracion.CambiarCredenciales(new CambioDeCredencialesDeListas(c.Proveedor, c.Usuario, c.Clave, c.UrlBase, c.Plan, c.CuotaMensual, modo),
                protector.Proteger, ahora);
        }

        if (dto.Proveedor is { } proveedor)
        {
            var nombre = string.IsNullOrWhiteSpace(proveedor) ? null : ProveedoresDeListas.Normalizar(proveedor) ?? proveedor;
            if (nombre is not null && ProveedoresDeListas.Normalizar(nombre) is { } conocido && !consulta.Opciones.Proveedor(conocido).Habilitado)
                throw ReglaDeNegocioException.Validacion("proveedor-no-habilitado", $"{conocido} no está habilitado en esta instalación.");
            configuracion.ElegirProveedor(nombre, ahora);
        }

        empresa.Sesion.Store(configuracion);
        var despues = await DtoAsync(empresa, configuracion, consulta, mes, ct);
        Auditoria.Registrar(empresa.Sesion, contexto, Entidad, empresa.EmpresaId.ToString(),
            existente is null ? AccionConfiguracion.Creado : AccionConfiguracion.Modificado, antes, despues, ahora, empresa.EmpresaId);
        await empresa.Sesion.SaveChangesAsync(ct);
        return despues;
    }

    private static async Task<ListasRestrictivasConfigDto> DtoAsync(SesionDeEmpresa empresa, ListasRestrictivasEmpresa? configuracion, ConsultaDeListas consulta,
        string mes, CancellationToken ct)
    {
        var empresaId = empresa.EmpresaId;
        var usos = await empresa.Sesion.Query<UsoMensualListas>().Where(u => u.EmpresaId == empresaId && u.Mes == mes).ToListAsync(ct);
        var opciones = consulta.Opciones;
        var proveedores = ProveedoresDeListas.Todos
            .Where(p => p != ProveedoresDeListas.Simulado || opciones.PermitirSimulado)
            .Select(p =>
            {
                var credenciales = configuracion?.CredencialesDe(p);
                return new ProveedorListasDto(p, ProveedoresDeListas.Reales.Contains(p), p == ProveedoresDeListas.Infolaft, opciones.Proveedor(p).Habilitado,
                    credenciales is not null || p == ProveedoresDeListas.Simulado, credenciales?.Usuario, credenciales?.UltimosCuatroClave, credenciales?.UrlBase,
                    credenciales?.Plan, credenciales?.CuotaMensual, credenciales?.ModoBusqueda.ToString());
            })
            .ToList();
        return new ListasRestrictivasConfigDto(configuracion?.Proveedor, consulta.Efectivo(configuracion), proveedores,
            [.. usos.OrderBy(u => u.Proveedor).Select(u => new UsoListasDto(u.Mes, u.Proveedor, u.Consultas, u.Busquedas, u.ConCoincidencias,
                configuracion?.CredencialesDe(u.Proveedor)?.CuotaMensual))],
            opciones.DiasMonitoreo, opciones.VigenciaEstudioDias);
    }

    [GeneratedRegex(@"^\d{4}-(0[1-9]|1[0-2])$")]
    private static partial Regex RegexMes();
}

/// <summary>
/// Informe SAGRILAFT (docs/05 §2.7, VE-14): las verificaciones de los expedientes de la empresa en el periodo, con totales por
/// verificación y proveedor, la distribución del riesgo, los estudios vencidos y las listas por reconsultar. Se calcula al
/// consultar (la proyección <c>InformeCumplimiento</c> llega cuando el volumen lo pida); Excel con <see cref="HojaDeCalculo"/>.
/// </summary>
public static class InformeCumplimientoEndpoints
{
    public const int MaximoExpedientes = 5000;

    /// <summary>Periodo <c>desde</c>–<c>hasta</c> (fechas; por omisión los últimos 90 días). 422 <c>periodo-invalido</c>.</summary>
    [WolverineGet("/api/v1/informes/cumplimiento")]
    [Authorize(Policy = Permisos.AuditoriaLeer)]
    public static async Task<InformeCumplimientoDto> Obtener(DateOnly? desde, DateOnly? hasta, SesionDeEmpresa empresa, TimeProvider reloj, CancellationToken ct) =>
        await CalcularAsync(empresa, desde, hasta, reloj.GetUtcNow(), ct);

    /// <summary>El mismo informe en Excel (una fila por verificación de cada expediente).</summary>
    [WolverineGet("/api/v1/informes/cumplimiento/excel")]
    [Authorize(Policy = Permisos.AuditoriaLeer)]
    public static async Task<IResult> Excel(DateOnly? desde, DateOnly? hasta, SesionDeEmpresa empresa, TimeProvider reloj, CancellationToken ct)
    {
        var informe = await CalcularAsync(empresa, desde, hasta, reloj.GetUtcNow(), ct);
        string[] encabezado =
        [
            "Folio", "Tercero", "Identificación", "Tipo de vínculo", "Estado del expediente", "Verificación", "Proveedor", "Estado", "Resultado", "Fecha",
            "Personas consultadas", "Coincidencias", "Confirmadas", "Descartadas", "Nivel de riesgo", "Conflicto de interés", "Debida diligencia", "Concepto",
            "Vigente hasta"
        ];
        var filas = informe.Filas.Select(f => (IReadOnlyList<object?>)
        [
            f.Folio, f.Tercero, f.Identificacion, f.TipoVinculo, f.EstadoExpediente, Nombre(f.Verificacion), f.Proveedor, f.Estado, f.Resultado,
            f.Fecha is { } fecha ? CalculadoraDeVigencia.Fecha(fecha) : null, f.Personas, f.Coincidencias, f.Confirmadas, f.Descartadas, f.NivelRiesgo,
            f.ConflictoDeInteres is { } conflicto ? (conflicto ? "Sí" : "No") : null, f.DebidaDiligencia, f.Concepto,
            f.VigenteHasta is { } vigente ? CalculadoraDeVigencia.Fecha(vigente) : null
        ]);
        var bytes = HojaDeCalculo.Crear("SAGRILAFT", encabezado, filas);
        return Results.File(bytes, HojaDeCalculo.TipoContenido, $"informe-sagrilaft-{informe.Desde:yyyyMMdd}-{informe.Hasta:yyyyMMdd}.xlsx");
    }

    private static string Nombre(string tipo) => tipo == nameof(TipoVerificacion.ListasRestrictivas) ? "Listas restrictivas" : "Estudio de cumplimiento";

    public static async Task<InformeCumplimientoDto> CalcularAsync(SesionDeEmpresa empresa, DateOnly? desde, DateOnly? hasta, DateTimeOffset ahora,
        CancellationToken ct)
    {
        var fin = hasta ?? CalculadoraDeVigencia.Fecha(ahora);
        var inicio = desde ?? fin.AddDays(-90);
        if (inicio > fin || fin.DayNumber - inicio.DayNumber > 3660)
            throw ReglaDeNegocioException.Validacion("periodo-invalido", "El periodo va de una fecha a otra igual o posterior (máximo 10 años).");

        var expedientes = await empresa.Expedientes().Where(e => e.ConVerificaciones).OrderBy(e => e.AbiertoEn).Take(MaximoExpedientes).ToListAsync(ct);
        bool EnPeriodo(DateTimeOffset? fecha) => fecha is { } f && CalculadoraDeVigencia.Fecha(f) is var d && d >= inicio && d <= fin;

        var filas = new List<FilaInformeCumplimientoDto>();
        foreach (var e in expedientes)
        {
            foreach (var v in e.Verificaciones)
            {
                // Del periodo: lo registrado en él, o lo pendiente de un expediente abierto en él.
                if (!EnPeriodo(v.RegistradaEn) && !(v.RegistradaEn is null && EnPeriodo(e.AbiertoEn)))
                    continue;
                var estudio = v.Estudio?.Datos;
                filas.Add(new FilaInformeCumplimientoDto(e.Id, e.Folio, e.TerceroNombre, e.TerceroIdentificacion, e.TipoVinculoNombre, e.Estado.ToString(),
                    v.Tipo.ToString(), v.Proveedor, v.Estado.ToString(), v.Resultado?.ToString(), v.RegistradaEn, v.Personas.Count, v.Coincidencias.Count,
                    v.Coincidencias.Count(c => c.Resolucion?.Decision == DecisionCoincidencia.Confirmada),
                    v.Coincidencias.Count(c => c.Resolucion?.Decision == DecisionCoincidencia.Descartada), estudio?.NivelRiesgo, estudio?.ConflictoDeInteres,
                    estudio?.DebidaDiligencia.ToString(), estudio?.Concepto.ToString(), v.VigenteHasta));
            }
        }

        var resumen = filas.GroupBy(f => (f.Verificacion, f.Proveedor))
            .OrderBy(g => g.Key.Verificacion).ThenBy(g => g.Key.Proveedor)
            .Select(g => new ResumenVerificacionesDto(g.Key.Verificacion, g.Key.Proveedor, g.Count(), g.Count(f => f.Coincidencias > 0), g.Count(f => f.Confirmadas > 0),
                g.Count(f => f.Estado != nameof(EstadoVerificacion.Satisfecha))))
            .ToList();
        var riesgo = filas.Where(f => f.NivelRiesgo is not null).GroupBy(f => f.NivelRiesgo!.Value).OrderBy(g => g.Key)
            .Select(g => new RiesgoDto(g.Key, g.Count())).ToList();
        var vencidos = filas.Count(f => f.Verificacion == nameof(TipoVerificacion.EstudioDeCumplimiento) &&
                                        (f.Estado == nameof(EstadoVerificacion.Expirada) || f.VigenteHasta is { } h && h <= ahora));
        var porReconsultar = filas.Count(f => f.Verificacion == nameof(TipoVerificacion.ListasRestrictivas) &&
                                              (f.Estado is nameof(EstadoVerificacion.Pendiente) or nameof(EstadoVerificacion.Expirada) || f.VigenteHasta is { } h && h <= ahora));
        return new InformeCumplimientoDto(inicio, fin, resumen, riesgo, vencidos, porReconsultar, filas);
    }
}
