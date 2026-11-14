import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarPreferenciasNotificacionTercero,
  getListarNotificacionesTerceroQueryKey,
  getListarNotificacionesTerceroQueryOptions,
  getObtenerPreferenciasNotificacionTerceroQueryKey,
  getObtenerPreferenciasNotificacionTerceroQueryOptions,
  marcarLeidaEnBandeja,
  marcarNotificacionesTerceroLeidas,
  marcarNotificacionTerceroLeida,
  marcarTodasEnBandeja,
  useCambioOptimistaDeBandeja,
  type BandejaNotificacionesTerceroDto,
  type ListarNotificacionesTerceroParams,
  type PreferenciasNotificacionDto,
} from '@validasoft/api-client';

/** Página de la campana: las 30 más recientes (el total de no leídas, de todas las empresas, viene aparte en `noLeidas`). */
export const PARAMETROS_BANDEJA: ListarNotificacionesTerceroParams = { limite: 30 };

/** Sin stream (proxys, el iframe del SRM) la campana se refresca cada tanto mientras la pestaña está a la vista. */
export const INTERVALO_REFRESCO = 30_000;

export const claveBandeja = getListarNotificacionesTerceroQueryKey(PARAMETROS_BANDEJA);

/** Con el stream conectado la bandeja se actualiza sola (`useStreamDelTercero`); en `respaldo` se pide cada 30 s. */
export function useBandejaDelTercero(respaldo: boolean) {
  return useQuery({
    ...getListarNotificacionesTerceroQueryOptions(PARAMETROS_BANDEJA),
    refetchInterval: respaldo ? INTERVALO_REFRESCO : false,
    refetchOnWindowFocus: true,
    staleTime: 10_000,
  });
}

export function useMarcarLeida() {
  const optimista = useCambioOptimistaDeBandeja<BandejaNotificacionesTerceroDto>(claveBandeja);
  return useMutation({
    mutationFn: (id: string) => marcarNotificacionTerceroLeida(id),
    onMutate: (id) => optimista.aplicar((b) => marcarLeidaEnBandeja(b, id, new Date().toISOString())),
    onError: (_error, _id, previa) => optimista.deshacer(previa),
  });
}

export function useMarcarTodas() {
  const optimista = useCambioOptimistaDeBandeja<BandejaNotificacionesTerceroDto>(claveBandeja);
  return useMutation({
    mutationFn: () => marcarNotificacionesTerceroLeidas(),
    onMutate: () => optimista.aplicar((b) => marcarTodasEnBandeja(b, new Date().toISOString())),
    onError: (_error, _variables, previa) => optimista.deshacer(previa),
  });
}

/** Preferencias de correo del tercero: valen para todas sus empresas. */
export function usePreferenciasDelTercero() {
  return useQuery(getObtenerPreferenciasNotificacionTerceroQueryOptions());
}

export function useGuardarPreferencias() {
  const cliente = useQueryClient();
  return useMutation({
    /** Solo los tipos que cambiaron: el PUT deja los demás como estaban. */
    mutationFn: (correo: Record<string, boolean>) => actualizarPreferenciasNotificacionTercero({ correo }),
    onSuccess: (respuesta: PreferenciasNotificacionDto) => cliente.setQueryData(getObtenerPreferenciasNotificacionTerceroQueryKey(), respuesta),
  });
}
