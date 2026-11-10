import { useQueries } from '@tanstack/react-query';
import { getObtenerExpedientePublicoQueryOptions, type ExpedientePublicoDto, type ObservacionPublicaDto } from '@validasoft/api-client';
import { observacionesDeRequisito } from '../expediente/requisitos';
import { useVinculosPublicos } from '../shared/api';

/** Lo que una empresa observó de la sección, con los campos que señaló. */
export interface ObservacionDeEmpresa {
  empresa: string;
  expedienteId: string;
  observacion: ObservacionPublicaDto;
}

/**
 * Observaciones abiertas de la sección en los expedientes del tercero (F2, FM-28): un requisito de esa sección (y ese
 * periodo, en la financiera) que alguna empresa tiene «Observado». Lee los expedientes públicos de sus vínculos (los
 * mismos que ve en «Mis empresas»); el DTO de la ficha no trae observaciones.
 */
export function observacionesDeSeccion(expediente: ExpedientePublicoDto, codigo: string, periodo: number | null): ObservacionDeEmpresa[] {
  return (expediente.requisitos ?? [])
    .filter((r) => r.estado === 'Observado' && r.seccion?.seccion === codigo && (periodo === null || r.seccion.periodo === null || r.seccion.periodo === periodo))
    .flatMap((r) => observacionesDeRequisito(r, expediente).map((observacion) => ({ empresa: expediente.empresa.nombre, expedienteId: expediente.id, observacion })));
}

export function useObservacionesDeSeccion(codigo: string, periodo: number | null): ObservacionDeEmpresa[] {
  const vinculos = useVinculosPublicos();
  const ids = [...new Set((vinculos.data ?? []).flatMap((v) => (v.expedienteId ? [v.expedienteId] : [])))];
  const expedientes = useQueries({ queries: ids.map((id) => getObtenerExpedientePublicoQueryOptions(id)) });
  return expedientes.flatMap((consulta) => (consulta.data ? observacionesDeSeccion(consulta.data, codigo, periodo) : []));
}
