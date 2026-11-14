import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  descargarArchivo,
  getListarVencimientosQueryOptions,
  recordarVencimientos,
  type ArchivoDescargado,
  type ListarVencimientosParams,
  type RecordarVencimientosDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';

/** Vencimientos de la empresa activa (`GET /vencimientos`); se vuelven a pedir al abrir la pantalla. */
export function useVencimientos(empresaId: string, params: ListarVencimientosParams, habilitado = true) {
  const opciones = getListarVencimientosQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    placeholderData: (previo) => previo,
    enabled: habilitado,
  });
}

/** Recordatorio al tercero; al terminar refresca la lista para mostrar el último recordatorio. */
export function useRecordarVencimientos(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: RecordarVencimientosDto) => recordarVencimientos(dto, { empresa: empresaId }),
    onSettled: () => invalidarRuta(cliente, '/api/v1/vencimientos'),
  });
}

/**
 * Informe en Excel con los mismos filtros (`GET /vencimientos/excel`). El cliente generado lo tipa como JSON, así que
 * se pide con la descarga binaria del cliente (misma sesión y `X-Empresa`).
 */
export function useDescargarExcelVencimientos(empresaId: string) {
  return useMutation({
    mutationFn: (params: ListarVencimientosParams): Promise<ArchivoDescargado> =>
      descargarArchivo({ url: '/api/v1/vencimientos/excel', params }, { empresa: empresaId }),
  });
}

/** Ofrece el archivo al navegador con un `<a download>` temporal (vive en el paquete compartido; se reexporta porque aquí lo importan las pantallas de informes). */
export { guardarArchivo } from '@validasoft/ui';
