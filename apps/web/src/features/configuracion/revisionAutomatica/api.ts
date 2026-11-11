import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarRevisionAutomatica,
  getObtenerRevisionAutomaticaQueryKey,
  getObtenerRevisionAutomaticaQueryOptions,
  type ActualizarRevisionAutomaticaDto,
} from '@validasoft/api-client';
import { conEmpresa } from '../../../shared/consultas';

/** Revisión automática de la empresa activa (`GET /configuracion/revision-automatica`): nivel, parámetros, uso del mes y lo que se puede elegir. */
export function useRevisionAutomatica(empresaId: string, habilitado: boolean) {
  const opciones = getObtenerRevisionAutomaticaQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', enabled: habilitado });
}

/** Reemplaza la configuración (`PUT`); la respuesta (ya con el uso del mes) reemplaza la de la caché. */
export function useActualizarRevisionAutomatica(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (cuerpo: ActualizarRevisionAutomaticaDto) => actualizarRevisionAutomatica(cuerpo, { empresa: empresaId }),
    onSuccess: (config) => cliente.setQueryData(conEmpresa(getObtenerRevisionAutomaticaQueryKey(), empresaId), config),
  });
}
