import { useQueries } from '@tanstack/react-query';
import { getObtenerExpedientePublicoQueryOptions, type ExpedientePublicoDto } from '@validasoft/api-client';
import { useVinculosPublicos } from '../shared/api';

/** Una sección que una empresa le pide ratificar al tercero en la renovación de su vínculo (VV-3). */
export interface RatificacionPendiente {
  empresa: string;
  expedienteId: string;
  requisitoId: string;
  seccion: string;
  periodo: number | null;
  /** Nombre de la sección («Información general», «Información financiera 2025»). */
  nombre: string;
}

/** Secciones que el expediente de renovación le deja al tercero por ratificar (la empresa ya las había aceptado y siguen sin versión nueva). */
export function ratificacionesPendientes(expediente: ExpedientePublicoDto): RatificacionPendiente[] {
  if (expediente.tipo !== 'Renovacion') return [];
  return (expediente.requisitos ?? []).flatMap((r) =>
    r.puedeRatificar && r.seccion
      ? [
          {
            empresa: expediente.empresa.nombre,
            expedienteId: expediente.id,
            requisitoId: r.id,
            seccion: r.seccion.seccion,
            periodo: r.seccion.periodo ?? null,
            nombre: r.tipoDocumento.nombre,
          },
        ]
      : [],
  );
}

/**
 * Lo que las empresas le piden ratificar al tercero, de todas sus secciones: lee los expedientes públicos de sus vínculos (los mismos
 * que ve en «Mis empresas»; el DTO de la ficha no trae la renovación) y se queda con las secciones que puede ratificar.
 */
export function useRatificacionesPendientes(): RatificacionPendiente[] {
  const vinculos = useVinculosPublicos();
  const ids = [...new Set((vinculos.data ?? []).flatMap((v) => (v.expedienteId ? [v.expedienteId] : [])))];
  const expedientes = useQueries({ queries: ids.map((id) => getObtenerExpedientePublicoQueryOptions(id)) });
  return expedientes.flatMap((consulta) => (consulta.data ? ratificacionesPendientes(consulta.data) : []));
}

/** Las que corresponden a una sección (y a su periodo, en la financiera). */
export function deLaSeccion(pendientes: readonly RatificacionPendiente[], codigo: string, periodo: number | null): RatificacionPendiente[] {
  return pendientes.filter((p) => p.seccion === codigo && (periodo === null || p.periodo === null || p.periodo === periodo));
}
