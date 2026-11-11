import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarPreferenciasNotificacionTercero,
  getListarNotificacionesTerceroQueryKey,
  getListarNotificacionesTerceroQueryOptions,
  getObtenerPreferenciasNotificacionTerceroQueryKey,
  getObtenerPreferenciasNotificacionTerceroQueryOptions,
  marcarNotificacionesTerceroLeidas,
  marcarNotificacionTerceroLeida,
  type BandejaNotificacionesTerceroDto,
  type ListarNotificacionesTerceroParams,
  type PreferenciasNotificacionDto,
} from '@validasoft/api-client';
import { marcarLeidaEnBandeja, marcarTodasEnBandeja } from './bandeja';

/** Página de la campana: las 30 más recientes (el total de no leídas, de todas las empresas, viene aparte en `noLeidas`). */
export const PARAMETROS_BANDEJA: ListarNotificacionesTerceroParams = { limite: 30 };

/** La campana se refresca cada tanto mientras la pestaña está a la vista (y al volver a ella). */
export const INTERVALO_REFRESCO = 30_000;

const claveBandeja = getListarNotificacionesTerceroQueryKey(PARAMETROS_BANDEJA);

export function useBandejaDelTercero() {
  return useQuery({
    ...getListarNotificacionesTerceroQueryOptions(PARAMETROS_BANDEJA),
    refetchInterval: INTERVALO_REFRESCO,
    refetchOnWindowFocus: true,
    staleTime: 10_000,
  });
}

/** Aplica un cambio optimista a la bandeja y devuelve con qué deshacerlo si la API lo rechaza. */
function useCambioOptimista() {
  const cliente = useQueryClient();
  return {
    aplicar: async (cambio: (bandeja: BandejaNotificacionesTerceroDto) => BandejaNotificacionesTerceroDto) => {
      await cliente.cancelQueries({ queryKey: claveBandeja });
      const previa = cliente.getQueryData<BandejaNotificacionesTerceroDto>(claveBandeja);
      if (previa) cliente.setQueryData<BandejaNotificacionesTerceroDto>(claveBandeja, cambio(previa));
      return previa;
    },
    deshacer: (previa: BandejaNotificacionesTerceroDto | undefined) => {
      if (previa) cliente.setQueryData(claveBandeja, previa);
      void cliente.invalidateQueries({ queryKey: claveBandeja });
    },
  };
}

export function useMarcarLeida() {
  const optimista = useCambioOptimista();
  return useMutation({
    mutationFn: (id: string) => marcarNotificacionTerceroLeida(id),
    onMutate: (id) => optimista.aplicar((b) => marcarLeidaEnBandeja(b, id, new Date().toISOString())),
    onError: (_error, _id, previa) => optimista.deshacer(previa),
  });
}

export function useMarcarTodas() {
  const optimista = useCambioOptimista();
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
