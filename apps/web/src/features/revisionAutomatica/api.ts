import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getListarExcepcionesDeRevisionQueryOptions,
  getObtenerExpedienteQueryKey,
  resolverRevisionAutomatica,
  type ExcepcionDeRevisionDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';
import { invalidarExpedientes } from '../expedientes/api';
import type { EstadoDeExcepciones } from './modelo';

/**
 * Excepciones de la revisión automática (`GET /revision-automatica/excepciones`): hasta 200 filas por estado. Se vuelve a
 * pedir al abrir la bandeja (otra persona pudo resolver mientras tanto); lo cacheado se ve mientras llega lo nuevo.
 */
export function useExcepciones(empresaId: string, estado: EstadoDeExcepciones, habilitado = true) {
  const opciones = getListarExcepcionesDeRevisionQueryOptions({ estado }, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', enabled: habilitado });
}

const CONTAR = (filas: readonly ExcepcionDeRevisionDto[]) => filas.length;

/**
 * Cuántas excepciones esperan a una persona, para el contador de la pestaña. Comparte caché con la bandeja (misma consulta
 * de «Pendientes»): abrir la pestaña no vuelve a pedir lo que ya se pidió. Sin permiso o con un error, no hay contador.
 */
export function useConteoExcepciones(empresaId: string, habilitado: boolean): number | null {
  const opciones = getListarExcepcionesDeRevisionQueryOptions({ estado: 'Pendientes' }, { request: { empresa: empresaId } });
  const consulta = useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado, select: CONTAR });
  return consulta.data ?? null;
}

export interface EntradaResolver {
  expedienteId: string;
  requisitoId: string;
  areaId: string;
  decision: 'Confirmar' | 'Revertir';
  /** Obligatoria al revertir. */
  justificacion?: string;
}

/**
 * Una persona del área confirma o revierte una aprobación por muestreo. La respuesta (el expediente ya resuelto) reemplaza
 * al de la caché y las listas se refrescan sin esperar: también ante un 409 (otra persona ya la resolvió).
 */
export function useResolverRevision(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (entrada: EntradaResolver) =>
      resolverRevisionAutomatica(
        entrada.expedienteId,
        entrada.requisitoId,
        { areaId: entrada.areaId, decision: entrada.decision, justificacion: entrada.justificacion?.trim() ? entrada.justificacion.trim() : null },
        { empresa: empresaId },
      ),
    onSuccess: (expediente) => {
      cliente.setQueryData(conEmpresa(getObtenerExpedienteQueryKey(expediente.id), empresaId), expediente);
    },
    onSettled: () => {
      void invalidarExpedientes(cliente);
      void invalidarRuta(cliente, '/api/v1/revision-automatica');
    },
  });
}
