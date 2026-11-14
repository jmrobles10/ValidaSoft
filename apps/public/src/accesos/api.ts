import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  aprobarAccesoSolicitado,
  cerrarSesionDelTercero,
  getListarAccesosSolicitadosQueryKey,
  getListarAccesosSolicitadosQueryOptions,
  getListarSesionesDelTerceroQueryKey,
  getObtenerContactosDelTerceroQueryOptions,
  getListarSesionesDelTerceroQueryOptions,
  rechazarAccesoSolicitado,
} from '@validasoft/api-client';

/** Las solicitudes de acceso de otros correos al tercero; solo las puede pedir quien ya es contacto (una sesión aislada recibe 403). */
export const useAccesosSolicitados = (habilitado = true) => useQuery({ ...getListarAccesosSolicitadosQueryOptions(), enabled: habilitado, retry: false });

/** Los contactos del tercero con su verificación y el historial de cómo llegaron (decisión AV); solo para quien tiene acceso completo. */
export const useContactosDelTercero = () => useQuery({ ...getObtenerContactosDelTerceroQueryOptions(), retry: false });

export function useDecidirAcceso() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ id, aprobar, motivo }: { id: string; aprobar: boolean; motivo?: string }) =>
      aprobar ? aprobarAccesoSolicitado(id) : rechazarAccesoSolicitado(id, { motivo: motivo?.trim() ? motivo.trim() : null }),
    // Aprobar cambia la bóveda y lo compartido con las empresas: se refresca todo lo del tercero.
    onSettled: () => cliente.invalidateQueries(),
  });
}

/** Las sesiones abiertas del tercero (la de este navegador marcada). */
export const useSesionesDelTercero = () => useQuery({ ...getListarSesionesDelTerceroQueryOptions(), retry: false });

export function useCerrarSesionDelTercero() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => cerrarSesionDelTercero(id),
    onSuccess: () => cliente.invalidateQueries({ queryKey: getListarSesionesDelTerceroQueryKey() }),
  });
}

export { getListarAccesosSolicitadosQueryKey };
