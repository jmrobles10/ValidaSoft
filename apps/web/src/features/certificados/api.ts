import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  descargarCertificado,
  esErrorApi,
  getListarCertificadosQueryOptions,
  getObtenerCertificadoVinculoQueryOptions,
  revocarCertificado,
  type ListarCertificadosParams,
} from '@validasoft/api-client';
import type { Estado } from '@validasoft/ui';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';

/** Estados de una emisión (CE-5): el chip usa el estado canónico del mismo nombre. */
export const ESTADOS_CERTIFICADO = ['Vigente', 'EnVerificacion', 'ConPendientes', 'Suspendido', 'Revocado', 'Vencido', 'Reemplazado'] as const satisfies readonly Estado[];
export type EstadoCertificado = (typeof ESTADOS_CERTIFICADO)[number];
export const esEstadoCertificado = (v: string): v is EstadoCertificado => ESTADOS_CERTIFICADO.some((e) => e === v);

/**
 * Lo que se ve de un certificado (VR-11): con la empresa revisando lo que el tercero actualizó es «En verificación» (sigue vigente, no bloquea)
 * aunque su estado de dominio siga siendo `ConPendientes`. Con los pendientes ya bloqueantes (venció el plazo) manda «Con pendientes».
 */
export function estadoVisibleDelCertificado(certificado: { estado: string; enVerificacion?: boolean }): string {
  return certificado.enVerificacion ? 'EnVerificacion' : certificado.estado;
}

/** Certificado del vínculo (emisión actual e historial) en la empresa activa; `Actual` nulo si nunca se aprobó. */
export function useCertificadoVinculo(empresaId: string, vinculoId: string, habilitado: boolean) {
  const opciones = getObtenerCertificadoVinculoQueryOptions(vinculoId, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

/** Certificados de la empresa activa (`GET /certificados`, la emisión actual de cada vínculo); se piden al abrir la pantalla. */
export function useCertificados(empresaId: string, params: ListarCertificadosParams, habilitado: boolean) {
  const opciones = getListarCertificadosQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    placeholderData: (previo) => previo,
    enabled: habilitado,
  });
}

/** `409 pdf-pendiente`: el PDF aún se genera (se reintenta en unos segundos). */
export const pdfPendiente = (error: unknown) => esErrorApi(error) && error.codigo === 'pdf-pendiente';

/** Descarga auditada del PDF (URL prefirmada de 5 minutos), en otra pestaña. */
export function useDescargarCertificado(empresaId: string) {
  return useMutation({
    mutationFn: async (folio: string) => {
      const descarga = await descargarCertificado(folio, { empresa: empresaId });
      window.open(descarga.url, '_blank', 'noopener');
      return descarga;
    },
  });
}

export function useRevocarCertificado(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ folio, justificacion }: { folio: string; justificacion: string }) => revocarCertificado(folio, { justificacion }, { empresa: empresaId }),
    onSuccess: () => Promise.all([invalidarRuta(cliente, '/api/v1/vinculos'), invalidarRuta(cliente, '/api/v1/certificados')]),
  });
}
