using Validasoft.Contratos;
using Validasoft.Dominio.Cumplimiento;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Aplicacion.Verificaciones;

/// <summary>Verificaciones del expediente y del monitoreo a DTO (VE-5…VE-12). Solo para la empresa: el tercero no las ve.</summary>
public static class MapeosDeVerificaciones
{
    public static AdjuntoDeCumplimientoDto ADto(this AdjuntoDeCumplimiento a) =>
        new(a.Id, a.Nombre, a.TipoContenido, a.Tamano, a.Sha256, a.Proposito.ToString(), a.Por, a.Fecha);

    public static ResultadoDeMatrizDto ADto(this ResultadoDeMatriz r) =>
        new(r.Puntaje, r.Nivel, r.NivelPorPuntaje, r.ForzadoPor, r.DebidaDiligencia.ToString(), r.MesesRevision, r.VersionMatriz,
            [.. r.Desglose.Select(d => new DesgloseDeMatrizDto(d.Factor, d.FactorNombre, d.Origen.ToString(), d.Peso, d.Opcion, d.OpcionNombre, d.Puntaje, d.NivelMinimo))]);

    public static MatrizDeRiesgoDto ADto(this MatrizDeRiesgoEmpresa m, bool esPorDefecto) =>
        new(m.Activa, esPorDefecto, m.Version,
            [.. m.Factores.Select(f => new FactorDeRiesgoDto(f.Codigo, f.Nombre, f.Origen.ToString(), f.Peso,
                [.. f.Opciones.Select(o => new OpcionDeFactorDto(o.Codigo, o.Nombre, o.Puntaje, o.NivelMinimo))], f.Ayuda))],
            m.LimiteBajo, m.LimiteMedio, [.. m.Niveles.Select(n => new NivelDeRiesgoDto(n.Nivel, n.Nombre, n.DebidaDiligencia.ToString(), n.MesesRevision))],
            esPorDefecto ? null : m.ActualizadaEn);

    public static VerificacionDto ADto(this VerificacionDeExpediente v, IReadOnlyList<AdjuntoDeCumplimiento>? adjuntos = null)
    {
        var personas = v.Personas.GroupBy(p => p.Clave, StringComparer.OrdinalIgnoreCase).ToDictionary(g => g.Key, g => g.First(),
            StringComparer.OrdinalIgnoreCase);
        return new VerificacionDto(v.Tipo.ToString(), v.Estado.ToString(), v.Bloqueante, v.RequeridaPorElFlujo, v.Proveedor, v.Resultado?.ToString(),
            v.ReferenciaExterna, v.RegistradaEn, v.RegistradaPor, v.VigenteHasta,
            v.Evidencia is { } e ? new EvidenciaVerificacionDto(e.NombreArchivo, e.TipoContenido, e.Tamano, e.Sha256, e.DelProveedor, e.Sha256Respuesta) : null,
            [.. v.Personas.Select(p => new PersonaConsultadaDto(p.Rol, p.Nombre, p.Identificacion))],
            [
                .. v.Coincidencias.Select(c => new CoincidenciaDto(c.Id, c.NombrePersona, c.Rol,
                    personas.TryGetValue(c.ClavePersona, out var persona) ? persona.Identificacion : c.ClavePersona, c.Lista, c.NombreEncontrado,
                    c.Detalle, c.Puntaje, c.Resolucion?.Decision.ToString(), c.Resolucion?.Justificacion, c.Resolucion?.Por, c.Resolucion?.Fecha,
                    c.Resolucion?.VigenteHasta, c.Heredada))
            ],
            v.Estudio is { } estudio
                ? new EstudioCumplimientoDto(estudio.VerificadoEnListas, estudio.Datos.ConflictoDeInteres, estudio.Datos.DebidaDiligencia.ToString(),
                    estudio.Datos.NivelRiesgo, estudio.Datos.Observaciones,
                    [.. estudio.Datos.Evidencias.Select(x => new EvidenciaEstudioDto(x.DocumentoId, x.Version, x.Nombre))], estudio.Datos.Concepto.ToString(),
                    estudio.Por, estudio.Fecha, v.VigenteHasta ?? estudio.Fecha, estudio.Datos.Matriz?.ADto(), estudio.Datos.JustificacionDeNivel,
                    [.. (estudio.Datos.Adjuntos ?? []).Select(id => adjuntos?.FirstOrDefault(a => a.Id == id)).OfType<AdjuntoDeCumplimiento>().Select(a => a.ADto())])
                : null,
            [.. v.Anteriores.Select(a => new ConsultaAnteriorDto(a.Proveedor, a.ReferenciaExterna, a.Resultado.ToString(), a.Fecha, a.Coincidencias, a.Evidencia is not null))],
            v.UltimoError, v.ReintentarDesde, v.VigenciaDias, v.FrecuenciaReconsultaMeses);
    }

    public static MonitoreoListasDto ADto(this MonitoreoDeListas m) =>
        new(m.VinculoId, m.ExpedienteId, m.Folio, m.FrecuenciaDias, m.ProximaConsulta, m.Listas.ADto());
}
