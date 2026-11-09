import { useMutation, useQuery, useQueryClient, type QueryKey } from '@tanstack/react-query';
import {
  actualizarPreferenciasNotificacion,
  getListarNotificacionesQueryOptions,
  getObtenerPreferenciasNotificacionQueryOptions,
  marcarNotificacionesLeidas,
  marcarNotificacionLeida,
  type BandejaNotificacionesDto,
  type ListarNotificacionesParams,
  type PreferenciasNotificacionDto,
} from '@validasoft/api-client';
import { conEmpresa } from '../../shared/consultas';
import { marcarLeidaEnBandeja, marcarTodasEnBandeja } from './bandeja';

/** Página de la campana: las 30 más recientes (el total de no leídas viene aparte en `noLeidas`). */
export const PARAMETROS_BANDEJA: ListarNotificacionesParams = { limite: 30 };

/** Sin stream, la campana se refresca cada tanto. */
export const INTERVALO_RESPALDO = 30_000;

function opcionesBandeja(empresaId: string) {
  return getListarNotificacionesQueryOptions(PARAMETROS_BANDEJA, { request: { empresa: empresaId } });
}

/** Clave de la bandeja en caché: con `X-Empresa` el servidor filtra, así que cada empresa tiene la suya. */
export function claveBandeja(empresaId: string): QueryKey {
  return conEmpresa(opcionesBandeja(empresaId).queryKey, empresaId);
}

export function useBandejaNotificaciones(empresaId: string, respaldo: boolean) {
  const opciones = opcionesBandeja(empresaId);
  return useQuery({ ...opciones, queryKey: claveBandeja(empresaId), refetchInterval: respaldo ? INTERVALO_RESPALDO : false });
}

/** Aplica un cambio optimista a la bandeja y devuelve con qué deshacerlo si la API lo rechaza. */
function useCambioOptimista(empresaId: string) {
  const cliente = useQueryClient();
  const clave = claveBandeja(empresaId);
  return {
    aplicar: async (cambio: (bandeja: BandejaNotificacionesDto) => BandejaNotificacionesDto) => {
      await cliente.cancelQueries({ queryKey: clave });
      const previa = cliente.getQueryData<BandejaNotificacionesDto>(clave);
      if (previa) cliente.setQueryData<BandejaNotificacionesDto>(clave, cambio(previa));
      return previa;
    },
    deshacer: (previa: BandejaNotificacionesDto | undefined) => {
      if (previa) cliente.setQueryData(clave, previa);
      void cliente.invalidateQueries({ queryKey: clave });
    },
  };
}

export function useMarcarLeida(empresaId: string) {
  const optimista = useCambioOptimista(empresaId);
  return useMutation({
    mutationFn: (id: string) => marcarNotificacionLeida(id, { empresa: empresaId }),
    onMutate: (id) => optimista.aplicar((b) => marcarLeidaEnBandeja(b, id, new Date().toISOString())),
    onError: (_error, _id, previa) => optimista.deshacer(previa),
  });
}

export function useMarcarTodas(empresaId: string) {
  const optimista = useCambioOptimista(empresaId);
  return useMutation({
    mutationFn: () => marcarNotificacionesLeidas({ empresa: empresaId }),
    onMutate: () => optimista.aplicar((b) => marcarTodasEnBandeja(b, new Date().toISOString())),
    onError: (_error, _variables, previa) => optimista.deshacer(previa),
  });
}

/** Preferencias de la persona: son del grupo, no de una empresa (sin `X-Empresa`). */
export function usePreferenciasNotificacion(habilitado: boolean) {
  const opciones = getObtenerPreferenciasNotificacionQueryOptions({ request: { empresa: null } });
  return useQuery({ ...opciones, enabled: habilitado });
}

export function useGuardarPreferencias() {
  const cliente = useQueryClient();
  const clave = getObtenerPreferenciasNotificacionQueryOptions().queryKey;
  return useMutation({
    /** Solo los tipos que cambiaron: el PUT deja los demás como estaban. */
    mutationFn: (correo: Record<string, boolean>) => actualizarPreferenciasNotificacion({ correo }, { empresa: null }),
    onSuccess: (respuesta: PreferenciasNotificacionDto) => cliente.setQueryData(clave, respuesta),
  });
}
