using Validasoft.Contratos;
using Validasoft.Dominio.Expedientes;

namespace Validasoft.Aplicacion.Verificaciones;

/// <summary>Verificaciones del expediente y del monitoreo a DTO (VE-5…VE-12). Solo para la empresa: el tercero no las ve.</summary>
public static class MapeosDeVerificaciones
{
    public static VerificacionDto ADto(this VerificacionDeExpediente v)
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
                    estudio.Por, estudio.Fecha, v.VigenteHasta ?? estudio.Fecha)
                : null,
            [.. v.Anteriores.Select(a => new ConsultaAnteriorDto(a.Proveedor, a.ReferenciaExterna, a.Resultado.ToString(), a.Fecha, a.Coincidencias, a.Evidencia is not null))],
            v.UltimoError, v.ReintentarDesde, v.VigenciaDias, v.FrecuenciaReconsultaMeses);
    }

    public static MonitoreoListasDto ADto(this MonitoreoDeListas m) =>
        new(m.VinculoId, m.ExpedienteId, m.Folio, m.FrecuenciaDias, m.ProximaConsulta, m.Listas.ADto());
}
