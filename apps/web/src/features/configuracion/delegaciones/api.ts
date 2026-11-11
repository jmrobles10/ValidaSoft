import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  crearDelegacion,
  getListarCandidatosDeDelegacionQueryOptions,
  getListarDelegacionesQueryOptions,
  revocarDelegacion,
  type CrearDelegacionDto,
  type ListarDelegacionesParams,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../../shared/consultas';

/** Delegaciones de la empresa: las propias, o todas si la persona administra expedientes (`GET /delegaciones`). */
export function useDelegaciones(empresaId: string, params: ListarDelegacionesParams, habilitado: boolean) {
  const opciones = getListarDelegacionesQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', placeholderData: (previo) => previo, enabled: habilitado });
}

/** Personas que pueden recibir una delegación: activas, de la empresa y con permiso de revisar. */
export function useCandidatosDeDelegacion(empresaId: string, habilitado: boolean) {
  const opciones = getListarCandidatosDeDelegacionQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

export function useCrearDelegacion(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: CrearDelegacionDto) => crearDelegacion(dto, { empresa: empresaId }),
    onSettled: () => invalidarRuta(cliente, '/api/v1/delegaciones'),
  });
}

export function useRevocarDelegacion(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ id, motivo }: { id: string; motivo?: string }) => revocarDelegacion(id, { motivo: motivo ?? null }, { empresa: empresaId }),
    onSettled: () => invalidarRuta(cliente, '/api/v1/delegaciones'),
  });
}
