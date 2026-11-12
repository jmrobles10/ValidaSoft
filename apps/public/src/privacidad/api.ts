import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  crearSolicitudHabeas,
  descargarExportacionBoveda,
  exportarBoveda,
  getListarExportacionesBovedaQueryKey,
  getListarExportacionesBovedaQueryOptions,
  getListarSolicitudesHabeasQueryKey,
  getListarSolicitudesHabeasQueryOptions,
  getObtenerConstanciaHabeasQueryOptions,
  type CrearSolicitudHabeasDto,
} from '@validasoft/api-client';
import { abrirDescarga } from '../shared/api';
import { intervaloDeRefresco } from './modelo';

/** Las exportaciones del tercero, la más reciente primero: se refrescan solas mientras una se prepara. */
export function useExportaciones() {
  return useQuery({
    ...getListarExportacionesBovedaQueryOptions(),
    refetchInterval: (consulta) => intervaloDeRefresco(consulta.state.data),
  });
}

export function useExportar() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: () => exportarBoveda(),
    onSuccess: () => cliente.invalidateQueries({ queryKey: getListarExportacionesBovedaQueryKey() }),
  });
}

/** Pide la URL prefirmada (5 minutos) y baja el ZIP; queda anotada la descarga. */
export function useDescargarExportacion() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: async (id: string) => abrirDescarga(await descargarExportacionBoveda(id)),
    onSuccess: () => cliente.invalidateQueries({ queryKey: getListarExportacionesBovedaQueryKey() }),
  });
}

/** Las solicitudes de habeas data del tercero con su radicado, su plazo y, si ya se respondieron, la respuesta. */
export const useSolicitudesHabeas = () => useQuery(getListarSolicitudesHabeasQueryOptions());

export function useCrearSolicitud() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (cuerpo: CrearSolicitudHabeasDto) => crearSolicitudHabeas(cuerpo),
    onSuccess: () => cliente.invalidateQueries({ queryKey: getListarSolicitudesHabeasQueryKey() }),
  });
}

/** La constancia de una solicitud respondida (sin datos personales, con su huella). */
export const useConstancia = (id: string, habilitada: boolean) => useQuery({ ...getObtenerConstanciaHabeasQueryOptions(id), enabled: habilitada });
