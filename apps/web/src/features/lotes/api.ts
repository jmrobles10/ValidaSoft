import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cancelarLote,
  crearLote,
  descargarArchivo,
  getListarFilasLoteQueryOptions,
  getListarLotesQueryOptions,
  getObtenerLoteQueryOptions,
  pausarLote,
  previsualizarLote,
  reanudarLote,
  reintentarLote,
  type ArchivoDescargado,
  type CrearLoteDto,
  type ListarFilasLoteParams,
  type ListarLotesParams,
  type LoteDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';
import { enCurso } from './modelo';

/** Cada cuánto se refresca un lote en proceso (avance por tandas). */
export const REFRESCO_EN_CURSO_MS = 2000;

export function useLotes(empresaId: string, params: ListarLotesParams, habilitado = true) {
  const opciones = getListarLotesQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    placeholderData: (previo) => previo,
    enabled: habilitado,
    // Mientras alguno avanza, la lista se refresca sola.
    refetchInterval: (consulta) => (consulta.state.data?.datos.some(enCurso) ? REFRESCO_EN_CURSO_MS : false),
  });
}

export function useLote(empresaId: string, id: string) {
  const opciones = getObtenerLoteQueryOptions(id, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    enabled: Boolean(id),
    refetchInterval: (consulta) => (consulta.state.data && enCurso(consulta.state.data) ? REFRESCO_EN_CURSO_MS : false),
  });
}

/** Filas del lote; con el lote en proceso se refrescan con él (`version` en la clave). */
export function useFilasLote(empresaId: string, lote: LoteDto | undefined, params: ListarFilasLoteParams) {
  const opciones = getListarFilasLoteQueryOptions(lote?.id ?? '', params, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: [...conEmpresa(opciones.queryKey, empresaId), lote?.version ?? 0],
    enabled: Boolean(lote),
    placeholderData: (previo) => previo,
  });
}

/** Valida el archivo sin crear nada (vista previa por fila). */
export function usePrevisualizarLote(empresaId: string) {
  return useMutation({ mutationFn: (dto: CrearLoteDto) => previsualizarLote(dto, { empresa: empresaId }) });
}

export function useCrearLote(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: CrearLoteDto) => crearLote(dto, { empresa: empresaId }),
    onSettled: () => invalidarRuta(cliente, '/api/v1/invitaciones/lotes'),
  });
}

export type AccionLote = 'pausar' | 'reanudar' | 'cancelar' | 'reintentar';

const ACCIONES: Record<AccionLote, (id: string, opciones: { empresa: string }) => Promise<LoteDto>> = {
  pausar: pausarLote,
  reanudar: reanudarLote,
  cancelar: cancelarLote,
  reintentar: reintentarLote,
};

export function useAccionLote(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ accion, id }: { accion: AccionLote; id: string }) => ACCIONES[accion](id, { empresa: empresaId }),
    onSettled: () => invalidarRuta(cliente, '/api/v1/invitaciones/lotes'),
  });
}

/** Plantilla vacía (`xlsx` o `csv`) e informe por fila: descargas binarias con la sesión y `X-Empresa`. */
export function useDescargarLote(empresaId: string) {
  return useMutation({
    mutationFn: (pedido: { tipo: 'plantilla'; formato: 'xlsx' | 'csv' } | { tipo: 'informe'; id: string }): Promise<ArchivoDescargado> =>
      pedido.tipo === 'plantilla'
        ? descargarArchivo({ url: '/api/v1/invitaciones/lotes/plantilla', params: { formato: pedido.formato } }, { empresa: empresaId })
        : descargarArchivo({ url: `/api/v1/invitaciones/lotes/${pedido.id}/informe` }, { empresa: empresaId }),
  });
}
