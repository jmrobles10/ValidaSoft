import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { actualizarConfiguracionSla, getObtenerConfiguracionSlaQueryOptions, type ActualizarConfiguracionSlaDto } from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../../shared/consultas';

/** Plazos de revisión de la empresa activa: porcentaje del aviso, avisar a administradores y plazo por defecto de cada área. */
export function useConfiguracionSla(empresaId: string, habilitado: boolean) {
  const opciones = getObtenerConfiguracionSlaQueryOptions({ request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), refetchOnMount: 'always', enabled: habilitado });
}

export function useGuardarConfiguracionSla(empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (dto: ActualizarConfiguracionSlaDto) => actualizarConfiguracionSla(dto, { empresa: empresaId }),
    onSettled: () => invalidarRuta(cliente, '/api/v1/configuracion/sla'),
  });
}
