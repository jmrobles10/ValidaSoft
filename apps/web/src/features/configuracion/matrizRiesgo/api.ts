import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actualizarMatrizDeRiesgo, getObtenerMatrizDeRiesgoQueryKey, getObtenerMatrizDeRiesgoQueryOptions, type ActualizarMatrizDeRiesgoDto } from '@validasoft/api-client';
import { conEmpresa } from '../../../shared/consultas';

/** La matriz guardada de la empresa o, si aún no tiene, la plantilla sugerida (`esPorDefecto`). La leen la configuración y el estudio. */
export function useMatrizDeRiesgo(empresaId: string, habilitado = true) {
  const opciones = getObtenerMatrizDeRiesgoQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

/** Guarda la matriz completa; la respuesta reemplaza la de la caché para que el estudio la vea de inmediato. */
export function useGuardarMatriz(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: ActualizarMatrizDeRiesgoDto) => actualizarMatrizDeRiesgo(dto, { empresa: empresaId }),
    onSuccess: (matriz) => cliente.setQueryData(conEmpresa(getObtenerMatrizDeRiesgoQueryKey(), empresaId), matriz),
  });
}
