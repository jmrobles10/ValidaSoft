import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  cancelarCampania,
  crearCampania,
  descargarArchivo,
  getListarCampaniasQueryOptions,
  getListarFilasCampaniaQueryOptions,
  getObtenerCampaniaQueryOptions,
  pausarCampania,
  previsualizarCampania,
  reanudarCampania,
  reintentarCampania,
  type ArchivoDescargado,
  type CampaniaDto,
  type CrearCampaniaDto,
  type FiltroDeCampaniaDto,
  type ListarCampaniasParams,
  type ListarFilasCampaniaParams,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';
import { enCurso, type AccionDeCampania } from './modelo';

/** Cada cuánto se refresca una campaña en proceso (avance por tandas). */
export const REFRESCO_EN_CURSO_MS = 2000;

export function useCampanias(empresaId: string, params: ListarCampaniasParams, habilitado = true) {
  const opciones = getListarCampaniasQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    placeholderData: (previo) => previo,
    enabled: habilitado,
    // Mientras alguna avanza, la lista se refresca sola.
    refetchInterval: (consulta) => (consulta.state.data?.datos.some(enCurso) ? REFRESCO_EN_CURSO_MS : false),
  });
}

export function useCampania(empresaId: string, id: string, habilitado = true) {
  const opciones = getObtenerCampaniaQueryOptions(id, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    enabled: Boolean(id) && habilitado,
    refetchInterval: (consulta) => (consulta.state.data && enCurso(consulta.state.data) ? REFRESCO_EN_CURSO_MS : false),
  });
}

/** Filas de la campaña; con la campaña en proceso se refrescan con ella (`version` en la clave). */
export function useFilasCampania(empresaId: string, campania: CampaniaDto | undefined, params: ListarFilasCampaniaParams) {
  const opciones = getListarFilasCampaniaQueryOptions(campania?.id ?? '', params, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: [...conEmpresa(opciones.queryKey, empresaId), campania?.version ?? 0],
    enabled: Boolean(campania),
    placeholderData: (previo) => previo,
  });
}

/** A quiénes afecta el filtro, sin crear nada. */
export function usePrevisualizarCampania(empresaId: string) {
  return useMutation({ mutationFn: (filtro: FiltroDeCampaniaDto) => previsualizarCampania(filtro, { empresa: empresaId }) });
}

export function useCrearCampania(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: CrearCampaniaDto) => crearCampania(dto, { empresa: empresaId }),
    onSettled: () => invalidarRuta(cliente, '/api/v1/campanias'),
  });
}

const ACCIONES: Record<AccionDeCampania, (id: string, opciones: { empresa: string }) => Promise<CampaniaDto>> = {
  pausar: pausarCampania,
  reanudar: reanudarCampania,
  cancelar: cancelarCampania,
  reintentar: reintentarCampania,
};

export function useAccionCampania(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ accion, id }: { accion: AccionDeCampania; id: string }) => ACCIONES[accion](id, { empresa: empresaId }),
    onSettled: () => invalidarRuta(cliente, '/api/v1/campanias'),
  });
}

/** Informe por vínculo en Excel: descarga binaria con la sesión y `X-Empresa`. */
export function useDescargarInformeCampania(empresaId: string) {
  return useMutation({
    mutationFn: (id: string): Promise<ArchivoDescargado> => descargarArchivo({ url: `/api/v1/campanias/${id}/informe` }, { empresa: empresaId }),
  });
}
