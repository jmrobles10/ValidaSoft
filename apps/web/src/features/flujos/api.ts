import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  crearFlujo,
  definirFlujoPorDefecto,
  editarFlujo,
  getListarFlujosPorDefectoQueryOptions,
  getListarFlujosQueryOptions,
  getListarVersionesFlujoQueryOptions,
  getObtenerFlujoQueryOptions,
  publicarFlujo,
  type CrearFlujoDto,
  type EditarFlujoDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';

export function useFlujos(empresaId: string, tipoVinculo?: string, habilitado = true) {
  const opciones = getListarFlujosQueryOptions(tipoVinculo ? { tipoVinculo } : undefined, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

export function useFlujo(empresaId: string, id: string) {
  const opciones = getObtenerFlujoQueryOptions(id, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: Boolean(id) });
}

export function useVersionesFlujo(empresaId: string, id: string) {
  const opciones = getListarVersionesFlujoQueryOptions(id, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: Boolean(id) });
}

export function useFlujosPorDefecto(empresaId: string, habilitado = true) {
  const opciones = getListarFlujosPorDefectoQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

function useInvalidarFlujos() {
  const cliente = useQueryClient();
  return () => invalidarRuta(cliente, '/api/v1/flujos');
}

export function useCrearFlujo(empresaId: string) {
  const invalidar = useInvalidarFlujos();
  return useMutation({ mutationFn: (dto: CrearFlujoDto) => crearFlujo(dto, { empresa: empresaId }), onSuccess: invalidar });
}

export function useEditarFlujo(empresaId: string, id: string) {
  const invalidar = useInvalidarFlujos();
  return useMutation({ mutationFn: (dto: EditarFlujoDto) => editarFlujo(id, dto, { empresa: empresaId }), onSuccess: invalidar });
}

export function usePublicarFlujo(empresaId: string, id: string) {
  const invalidar = useInvalidarFlujos();
  return useMutation({ mutationFn: () => publicarFlujo(id, { empresa: empresaId }), onSettled: invalidar });
}

export function useDefinirPorDefecto(empresaId: string) {
  const invalidar = useInvalidarFlujos();
  return useMutation({
    mutationFn: ({ tipoVinculoId, flujoId }: { tipoVinculoId: string; flujoId: string }) =>
      definirFlujoPorDefecto({ tipoVinculoId, flujoId, empresaId }, { empresa: empresaId }),
    onSettled: invalidar,
  });
}
