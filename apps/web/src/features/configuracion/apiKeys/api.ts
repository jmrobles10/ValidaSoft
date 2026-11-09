import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { crearApiKey, getListarApiKeysQueryOptions, getListarSistemasQueryOptions, revocarApiKey } from '@validasoft/api-client';
import { invalidarRuta } from '../../../shared/consultas';
import { cuerpoCrearApiKey, type BorradorApiKey } from './modelo';

/** Las API keys son del grupo (tenant): sin empresa activa. */
const SIN_EMPRESA = { empresa: null } as const;

export function useApiKeys(habilitado: boolean) {
  return useQuery({ ...getListarApiKeysQueryOptions({ request: SIN_EMPRESA }), enabled: habilitado });
}

/** Sistemas integrados, para nombrar el de cada llave y elegirlo al crear (requiere `sistemas:administrar`). */
export function useSistemas(habilitado: boolean) {
  return useQuery({ ...getListarSistemasQueryOptions({ request: SIN_EMPRESA }), enabled: habilitado });
}

/** La respuesta trae `valor`: la clave completa, que solo se ve esta vez. */
export function useCrearApiKey(claveIdempotencia: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (b: BorradorApiKey) => crearApiKey(cuerpoCrearApiKey(b), { ...SIN_EMPRESA, idempotencyKey: claveIdempotencia }),
    onSuccess: () => invalidarRuta(cliente, '/api/v1/api-keys'),
  });
}

export function useRevocarApiKey() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (id: string) => revocarApiKey(id, SIN_EMPRESA),
    onSettled: () => invalidarRuta(cliente, '/api/v1/api-keys'),
  });
}
