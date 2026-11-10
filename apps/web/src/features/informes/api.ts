import { useMutation, useQuery } from '@tanstack/react-query';
import {
  descargarArchivo,
  getObtenerInformeEstadoExpedientesQueryOptions,
  getObtenerInformeVinculosQueryOptions,
  type ArchivoDescargado,
  type ObtenerInformeEstadoExpedientesParams,
  type ObtenerInformeVinculosParams,
} from '@validasoft/api-client';
import { conEmpresa } from '../../shared/consultas';

/** Informe de estado de expedientes (`GET /informes/estado-expedientes`); se vuelve a pedir al abrir la pantalla. */
export function useInformeEstado(empresaId: string, params: ObtenerInformeEstadoExpedientesParams, habilitado: boolean) {
  const opciones = getObtenerInformeEstadoExpedientesQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', placeholderData: (previo) => previo, enabled: habilitado });
}

/** Informe de terceros y vínculos (`GET /informes/vinculos`). */
export function useInformeVinculos(empresaId: string, params: ObtenerInformeVinculosParams, habilitado: boolean) {
  const opciones = getObtenerInformeVinculosQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', placeholderData: (previo) => previo, enabled: habilitado });
}

/**
 * Descarga binaria (Excel, PDF o ZIP) con la sesión y `X-Empresa`: el cliente generado tipa estas rutas como JSON, así que
 * se piden con la descarga del cliente. `params` son los mismos filtros de la consulta.
 */
export function useDescarga(empresaId: string) {
  return useMutation({
    mutationFn: ({ url, params }: { url: string; params?: object }): Promise<ArchivoDescargado> =>
      descargarArchivo({ url, params }, { empresa: empresaId }),
  });
}
