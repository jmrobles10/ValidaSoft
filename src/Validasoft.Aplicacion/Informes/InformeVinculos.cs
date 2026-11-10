using Marten;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Validasoft.Aplicacion.Comun;
using Validasoft.Aplicacion.Vinculos;
using Validasoft.Contratos;
using Validasoft.Dominio.Certificados;
using Validasoft.Dominio.Comun;
using Validasoft.Dominio.Configuracion;
using Validasoft.Dominio.Documentos;
using Validasoft.Dominio.Expedientes;
using Validasoft.Dominio.Plataforma;
using Validasoft.Dominio.Terceros;
using Validasoft.Dominio.Vinculos;
using Wolverine.Http;

namespace Validasoft.Aplicacion.Informes;

/// <summary>
/// Informe de terceros y vínculos (IF-1): por vínculo de la empresa, su estado, el certificado vigente, el expediente en
/// curso, la vigencia de los documentos compartidos (la misma de Vencimientos, TD-6) y las secciones de la ficha que el
/// expediente en curso aún no aprueba. Se calcula al consultar; se descarga en Excel con los mismos filtros.
/// </summary>
public static class InformeVinculosEndpoints
{
    public const int MaximoVinculos = 5000;

    [WolverineGet("/api/v1/informes/vinculos")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<InformeVinculosDto> Consultar(SesionDeEmpresa empresa, IContextoSolicitud contexto, TimeProvider reloj, string? estado,
        Guid? tipoVinculoId, Guid? terceroId, int? limite, string? cursor, CancellationToken ct)
    {
        var tamano = Math.Clamp(limite ?? 50, 1, 500);
        var salto = Peticiones.LeerDesplazamiento(cursor);
        var filas = await CalcularAsync(empresa, contexto, reloj, estado, tipoVinculoId, terceroId, ct);
        var siguiente = filas.Count > salto + tamano ? Peticiones.EscribirDesplazamiento(salto + tamano) : null;
        return new InformeVinculosDto(Resumir(filas), [.. filas.Skip(salto).Take(tamano)], siguiente);
    }

    [WolverineGet("/api/v1/informes/vinculos/excel")]
    [Authorize(Policy = Permisos.VinculosLeer)]
    public static async Task<IResult> Excel(SesionDeEmpresa empresa, IContextoSolicitud contexto, HttpContext http, TimeProvider reloj, string? estado,
        Guid? tipoVinculoId, Guid? terceroId, CancellationToken ct)
    {
        var filas = await CalcularAsync(empresa, contexto, reloj, estado, tipoVinculoId, terceroId, ct);
        var resumen = Resumir(filas);
        var vinculos = new Hoja("Vínculos",
            ["Tercero", "Identificación", "Tipo de vínculo", "Estado", "Creado", "Invitación aceptada", "Certificado", "Estado del certificado",
                "Certificado vigente hasta", "Expediente en curso", "Estado del expediente", "Documentos con vencimiento", "Vigentes", "Por vencer",
                "En gracia", "Vencidos", "Próximo vencimiento", "Secciones pendientes de validar"],
            filas.Select(f => (IReadOnlyList<object?>)
            [
                f.Tercero.Nombre, InformeEstadoExpedientesEndpoints.Identificacion(f.Tercero.Identificacion), f.TipoVinculo.Nombre, TextoEstado(f.Estado),
                Exportaciones.Local(f.CreadoEn), f.InvitacionAceptada ? "Sí" : "No", f.Certificado?.Folio, TextoCertificado(f.Certificado?.Estado),
                f.Certificado?.VigenteHasta, f.ExpedienteEnCurso?.Folio,
                f.ExpedienteEnCurso is { } x ? InformeEstadoExpedientesEndpoints.TextoEstado(x.Estado) : null,
                f.Documentos?.ConVencimiento, f.Documentos?.Vigentes, f.Documentos?.PorVencer, f.Documentos?.EnGracia, f.Documentos?.Vencidos,
                f.Documentos?.ProximoVencimiento, f.SeccionesPendientes.Count == 0 ? null : string.Join(", ", f.SeccionesPendientes)
            ]));
        var hojaResumen = new Hoja("Resumen", ["Indicador", "Valor"],
        [
            ["Vínculos", resumen.Total],
            .. resumen.PorEstado.Select(c => (IReadOnlyList<object?>)[$"Estado {TextoEstado(c.Clave)}", c.Cantidad]),
            ["Con certificado vigente", resumen.ConCertificadoVigente],
            ["Con documentos por vencer o en gracia", resumen.ConDocumentosPorVencer],
            ["Con documentos vencidos", resumen.ConDocumentosVencidos],
            ["Con secciones de la ficha pendientes de validar", resumen.ConSeccionesPendientes]
        ]);

        var ahora = reloj.GetUtcNow();
        var nombreEmpresa = (await empresa.Sesion.LoadAsync<Empresa>(empresa.EmpresaId, ct))?.Nombre ?? "empresa";
        var nombre = $"vinculos-{Exportaciones.Slug(nombreEmpresa)}-{InformeEstadoExpedientesEndpoints.CalculadoraDeVigenciaFecha(ahora)}.xlsx";
        return await Exportaciones.EntregarAsync(empresa, contexto, http, reloj,
            new ArchivoExportado("InformeVinculos", "Excel", HojaDeCalculo.CrearLibro(vinculos, hojaResumen), HojaDeCalculo.TipoContenido, nombre, filas.Count),
            ct);
    }

    internal static async Task<IReadOnlyList<FilaInformeVinculoDto>> CalcularAsync(SesionDeEmpresa empresa, IContextoSolicitud contexto,
        TimeProvider reloj, string? estado, Guid? tipoVinculoId, Guid? terceroId, CancellationToken ct)
    {
        var estados = new List<EstadoVinculo>();
        foreach (var valor in (estado ?? string.Empty).Split(',', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries))
            estados.Add(Enum.TryParse<EstadoVinculo>(valor, true, out var e) && Enum.IsDefined(e)
                ? e
                : throw ReglaDeNegocioException.Validacion("estado-invalido", $"'{valor}' no es un estado de vínculo."));

        var consulta = empresa.Vinculos();
        if (estados.Count > 0)
        {
            var arreglo = estados.ToArray();
            consulta = consulta.Where(v => v.Estado.IsOneOf(arreglo));
        }
        if (tipoVinculoId is { } tipo)
            consulta = consulta.Where(v => v.TipoVinculoId == tipo);
        if (terceroId is { } tercero)
            consulta = consulta.Where(v => v.TerceroId == tercero);

        var vinculos = await consulta.OrderByDescending(v => v.CreadoEn).ThenBy(v => v.Id).Take(MaximoVinculos + 1).ToListAsync(ct);
        if (vinculos.Count > MaximoVinculos)
            throw ReglaDeNegocioException.Validacion("informe-demasiado-grande", $"El informe admite hasta {MaximoVinculos} vínculos: agrega filtros.");
        if (vinculos.Count == 0)
            return [];

        var terceros = (await empresa.Sesion.LoadManyAsync<Tercero>(ct, [.. vinculos.Select(v => v.TerceroId).Distinct()])).ToDictionary(t => t.Id);
        var tipos = (await empresa.Sesion.Query<TipoVinculo>().ToListAsync(ct)).ToDictionary(t => t.Id);
        var empresaId = empresa.EmpresaId;
        var certificados = (await empresa.Sesion.Query<Certificado>().Where(c => c.EmpresaId == empresaId).ToListAsync(ct))
            .GroupBy(c => c.VinculoId).ToDictionary(g => g.Key, g => g.First());
        var abiertos = vinculos.Where(v => v.ExpedienteAbiertoId is not null).Select(v => v.ExpedienteAbiertoId!.Value).ToArray();
        var expedientes = abiertos.Length == 0
            ? new Dictionary<Guid, Expediente>()
            : (await empresa.Sesion.LoadManyAsync<Expediente>(ct, abiertos)).Where(e => e.EmpresaId == empresaId).ToDictionary(e => e.Id);

        // Las vigencias son documentos: sin documentos:leer el informe no las trae (la Plataforma de soporte no los ve, B-6).
        var veDocumentos = contexto.Scopes.Contains(Permisos.DocumentosLeer);
        var vencimientos = veDocumentos
            ? (await VencimientosEndpoints.Listar(empresa, reloj, 0, null, ct)).ToLookup(v => v.VinculoId)
            : null;

        return [.. vinculos.Select(v =>
        {
            var tercero = terceros.GetValueOrDefault(v.TerceroId);
            var tipoVinculo = tipos.GetValueOrDefault(v.TipoVinculoId);
            var certificado = certificados.GetValueOrDefault(v.Id) is { } c
                ? new CertificadoDelInformeDto(c.Emisiones.FirstOrDefault(x => x.Numero == c.EmisionActual)?.Folio ?? c.FolioRaiz, c.Estado.ToString(),
                    c.Bloqueante, c.Emisiones.FirstOrDefault(x => x.Numero == c.EmisionActual)?.VigenteHasta)
                : null;
            var expediente = v.ExpedienteAbiertoId is { } id ? expedientes.GetValueOrDefault(id) : null;
            var secciones = expediente is null
                ? []
                : expediente.Requisitos
                    .Where(r => r.EsSeccion && r.Estado is not (EstadoRequisito.Aprobado or EstadoRequisito.Eximido or EstadoRequisito.NoAplica))
                    .Select(r => r.Periodo is { } periodo ? $"{r.Nombre} {periodo}" : r.Nombre)
                    .ToList();
            DocumentosDelInformeDto? documentos = null;
            if (vencimientos is not null)
            {
                var propios = vencimientos[v.Id].ToList();
                int Cuenta(EstadoVigencia e) => propios.Count(x => x.Estado == e.ToString());
                documentos = new DocumentosDelInformeDto(propios.Count, Cuenta(EstadoVigencia.Vigente), Cuenta(EstadoVigencia.PorVencer),
                    Cuenta(EstadoVigencia.EnGracia), Cuenta(EstadoVigencia.Vencida),
                    propios.Where(x => x.Estado is nameof(EstadoVigencia.Vigente) or nameof(EstadoVigencia.PorVencer))
                        .Select(x => (DateOnly?)x.VigenteHasta).Min());
            }

            return new FilaInformeVinculoDto(v.Id,
                new TerceroResumenDto(v.TerceroId, tercero?.Nombre ?? string.Empty, tercero?.ClaveIdentificacion ?? string.Empty),
                new TipoVinculoResumenDto(v.TipoVinculoId, tipoVinculo?.Codigo ?? string.Empty, tipoVinculo?.Nombre ?? string.Empty),
                v.Estado.ToString(), v.CreadoEn, v.InvitacionAceptada, certificado,
                expediente is null ? null : new ExpedienteDelInformeDto(expediente.Id, expediente.Folio, expediente.Tipo.ToString(), expediente.Estado.ToString()),
                documentos, secciones);
        })];
    }

    internal static ResumenVinculosDto Resumir(IReadOnlyList<FilaInformeVinculoDto> filas) =>
        new(filas.Count,
            [.. Enum.GetValues<EstadoVinculo>().Select(e => new ConteoDto(e.ToString(), filas.Count(f => f.Estado == e.ToString()))).Where(c => c.Cantidad > 0)],
            filas.Count(f => f.Certificado is { Estado: nameof(EstadoCertificado.Vigente) }),
            filas.Count(f => f.Documentos is { } d && d.PorVencer + d.EnGracia > 0),
            filas.Count(f => f.Documentos is { Vencidos: > 0 }),
            filas.Count(f => f.SeccionesPendientes.Count > 0));

    private static string TextoEstado(string estado) => estado switch
    {
        nameof(EstadoVinculo.EnValidacion) => "En validación",
        nameof(EstadoVinculo.PorVencer) => "Por vencer",
        _ => estado
    };

    private static string? TextoCertificado(string? estado) => estado switch
    {
        nameof(EstadoCertificado.ConPendientes) => "Con pendientes",
        _ => estado
    };
}
