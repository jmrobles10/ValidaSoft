import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  aprobarInvitacionVinculo,
  aprobarLote,
  esErrorApi,
  getListarInvitacionesPorAprobarQueryOptions,
  rechazarInvitacionVinculo,
  rechazarLote,
  type ListarInvitacionesPorAprobarParams,
} from '@validasoft/api-client';
import type { TFunction } from 'i18next';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';
import { mensajeDeError } from '../../shared/errores';

/** Lo que decide quien aprueba: aprobar (sale la invitación) o rechazar con un motivo (el vínculo o el lote se cancela). */
export type Decision = { decision: 'aprobar' } | { decision: 'rechazar'; motivo: string };

/** Invitaciones retenidas de la empresa (bandeja de quienes aprueban, decisión AI); `soloLasMias` deja las que quien consulta puede decidir. */
export function useInvitacionesPorAprobar(empresaId: string, params: ListarInvitacionesPorAprobarParams, habilitado = true) {
  const opciones = getListarInvitacionesPorAprobarQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    placeholderData: (previo) => previo,
    enabled: habilitado,
  });
}

/** Tras decidir cambia la bandeja, el vínculo o el lote, el historial y la campana de quien pidió: se recargan todos. */
function invalidarTodo(cliente: ReturnType<typeof useQueryClient>) {
  return Promise.all([
    invalidarRuta(cliente, '/api/v1/invitaciones'),
    invalidarRuta(cliente, '/api/v1/vinculos'),
    invalidarRuta(cliente, '/api/v1/notificaciones'),
  ]);
}

/**
 * Aprueba o rechaza la invitación retenida de un vínculo. Con `version` viaja `If-Match` (el detalle sabe qué versión leyó; la bandeja no, y entonces se bloquea la
 * fila del vínculo mientras se decide).
 */
export function useDecidirInvitacion(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ vinculoId, version, ...decision }: { vinculoId: string; version?: number } & Decision) => {
      const opciones = version === undefined ? { empresa: empresaId } : { empresa: empresaId, ifMatch: version };
      return decision.decision === 'aprobar'
        ? aprobarInvitacionVinculo(vinculoId, opciones)
        : rechazarInvitacionVinculo(vinculoId, { motivo: decision.motivo }, opciones);
    },
    onSettled: () => invalidarTodo(cliente),
  });
}

/** Aprueba o rechaza un lote completo (una sola decisión para todas sus filas). */
export function useDecidirLote(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ loteId, ...decision }: { loteId: string } & Decision) =>
      decision.decision === 'aprobar' ? aprobarLote(loteId, { empresa: empresaId }) : rechazarLote(loteId, { motivo: decision.motivo }, { empresa: empresaId }),
    onSettled: () => invalidarTodo(cliente),
  });
}

/**
 * El error de una decisión. `separacion-de-funciones` y `conflicto-de-intereses` son los códigos de los veredictos (el texto general habla de expedientes): aquí se dice
 * lo que pasó con la invitación.
 */
export function mensajeDeErrorDeDecision(error: unknown, t: TFunction): string {
  if (esErrorApi(error)) {
    if (error.codigo === 'separacion-de-funciones') return t('invitaciones.errores.separacion');
    if (error.codigo === 'conflicto-de-intereses') return t('invitaciones.errores.conflicto');
  }
  return mensajeDeError(error, t);
}
