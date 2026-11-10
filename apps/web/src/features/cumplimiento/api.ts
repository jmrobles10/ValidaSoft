import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarListasRestrictivas,
  consultarVerificacion,
  descargarArchivo,
  descargarEvidenciaMonitoreo,
  descargarEvidenciaVerificacion,
  getListarBandejaCumplimientoQueryOptions,
  getObtenerExpedienteQueryKey,
  getObtenerInformeCumplimientoQueryOptions,
  getObtenerListasRestrictivasQueryOptions,
  getObtenerMonitoreoListasQueryOptions,
  registrarEstudioCumplimiento,
  resolverCoincidencia,
  resolverCoincidenciaMonitoreo,
  type ActualizarListasRestrictivasDto,
  type ArchivoDescargado,
  type DecisionCoincidencia,
  type ExpedienteDto,
  type ObtenerInformeCumplimientoParams,
  type RegistrarEstudioCumplimientoDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';
import { invalidarExpedientes } from '../expedientes/api';

/** Bandeja del oficial (`GET /cumplimiento/bandeja`): se vuelve a pedir al abrirla. */
export function useBandejaCumplimiento(empresaId: string, habilitado = true) {
  const opciones = getListarBandejaCumplimientoQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', enabled: habilitado });
}

export function useMonitoreoListas(empresaId: string, vinculoId: string, habilitado = true) {
  const opciones = getObtenerMonitoreoListasQueryOptions(vinculoId, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado && Boolean(vinculoId), retry: false });
}

/** Una acción de Cumplimiento sobre el expediente (cada una con lo que pide su endpoint). */
export type AccionVerificacion =
  | { tipo: 'consultar' }
  | { tipo: 'resolver'; coincidenciaId: string; decision: DecisionCoincidencia; justificacion: string }
  | { tipo: 'estudio'; estudio: RegistrarEstudioCumplimientoDto };

export function ejecutarVerificacion(empresaId: string, expedienteId: string, accion: AccionVerificacion): Promise<ExpedienteDto> {
  const opciones = { empresa: empresaId };
  switch (accion.tipo) {
    case 'consultar':
      return consultarVerificacion(expedienteId, 'ListasRestrictivas', opciones);
    case 'resolver':
      return resolverCoincidencia(
        expedienteId,
        'ListasRestrictivas',
        accion.coincidenciaId,
        { decision: accion.decision, justificacion: accion.justificacion.trim() },
        opciones,
      );
    case 'estudio':
      return registrarEstudioCumplimiento(expedienteId, accion.estudio, opciones);
  }
}

/** Como las decisiones de área: la respuesta reemplaza el expediente en caché y el resto (bandejas, vínculo) se refresca. */
export function useAccionVerificacion(empresaId: string, expedienteId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (accion: AccionVerificacion) => ejecutarVerificacion(empresaId, expedienteId, accion),
    onSuccess: (expediente) => cliente.setQueryData(conEmpresa(getObtenerExpedienteQueryKey(expedienteId), empresaId), expediente),
    onSettled: () => {
      void invalidarExpedientes(cliente);
      void invalidarRuta(cliente, '/api/v1/cumplimiento');
    },
  });
}

/** Descarga auditada de la evidencia (URL prefirmada de 5 minutos) en otra pestaña; `referencia` = una consulta anterior. */
export function useDescargarEvidencia(empresaId: string) {
  return useMutation({
    mutationFn: async ({ expedienteId, referencia }: { expedienteId: string; referencia?: string }) => {
      const descarga = await descargarEvidenciaVerificacion(expedienteId, 'ListasRestrictivas', referencia ? { referencia } : undefined, {
        empresa: empresaId,
      });
      window.open(descarga.url, '_blank', 'noopener');
      return descarga;
    },
  });
}

export function useDescargarEvidenciaMonitoreo(empresaId: string) {
  return useMutation({
    mutationFn: async (vinculoId: string) => {
      const descarga = await descargarEvidenciaMonitoreo(vinculoId, { empresa: empresaId });
      window.open(descarga.url, '_blank', 'noopener');
      return descarga;
    },
  });
}

export function useResolverCoincidenciaMonitoreo(empresaId: string, vinculoId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ coincidenciaId, decision, justificacion }: { coincidenciaId: string; decision: DecisionCoincidencia; justificacion: string }) =>
      resolverCoincidenciaMonitoreo(vinculoId, coincidenciaId, { decision, justificacion: justificacion.trim() }, { empresa: empresaId }),
    onSettled: () => Promise.all([invalidarRuta(cliente, '/api/v1/vinculos'), invalidarRuta(cliente, '/api/v1/cumplimiento')]),
  });
}

export function useListasRestrictivas(empresaId: string, habilitado = true) {
  const opciones = getObtenerListasRestrictivasQueryOptions(undefined, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

export function useActualizarListasRestrictivas(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: ActualizarListasRestrictivasDto) => actualizarListasRestrictivas(dto, { empresa: empresaId }),
    onSuccess: (configuracion) =>
      cliente.setQueryData(conEmpresa(getObtenerListasRestrictivasQueryOptions(undefined).queryKey, empresaId), configuracion),
  });
}

export function useInformeCumplimiento(empresaId: string, params: ObtenerInformeCumplimientoParams, habilitado = true) {
  const opciones = getObtenerInformeCumplimientoQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), placeholderData: (previo) => previo, enabled: habilitado });
}

/** Informe SAGRILAFT en Excel (`GET /informes/cumplimiento/excel`) por la descarga binaria del cliente. */
export function useDescargarInformeCumplimiento(empresaId: string) {
  return useMutation({
    mutationFn: (params: ObtenerInformeCumplimientoParams): Promise<ArchivoDescargado> =>
      descargarArchivo({ url: '/api/v1/informes/cumplimiento/excel', params }, { empresa: empresaId }),
  });
}
