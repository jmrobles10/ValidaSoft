import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { aprobarRevalidaciones, getListarRevalidacionesQueryOptions, type AprobarRevalidacionesDto, type ListarRevalidacionesParams } from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';

/** Bandeja de revalidaciones (`GET /revalidaciones`); como la de revisión, se vuelve a pedir al abrirla. */
export function useRevalidaciones(empresaId: string, params: ListarRevalidacionesParams, habilitado = true) {
  const opciones = getListarRevalidacionesQueryOptions(params, { request: { empresa: empresaId } });
  return useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    refetchOnMount: 'always',
    placeholderData: (previo) => previo,
    enabled: habilitado,
  });
}

/** Aprobación en lote: al terminar refresca las revalidaciones, la bandeja y los expedientes tocados. */
export function useAprobarRevalidaciones(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: AprobarRevalidacionesDto) => aprobarRevalidaciones(dto, { empresa: empresaId }),
    onSettled: () =>
      Promise.all([
        invalidarRuta(cliente, '/api/v1/revalidaciones'),
        invalidarRuta(cliente, '/api/v1/bandeja'),
        invalidarRuta(cliente, '/api/v1/expedientes'),
      ]),
  });
}
