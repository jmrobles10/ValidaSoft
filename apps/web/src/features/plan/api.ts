import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarPlanDeUnGrupo,
  getObtenerPlanDeUnGrupoQueryOptions,
  getObtenerUsoDelGrupoQueryOptions,
  type ActualizarPlanDelGrupoDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';

/** La consola de Plataforma no tiene empresa activa ni tenant (PT-4). */
const SIN_EMPRESA = { empresa: null } as const;

/** Plan y uso del grupo de la credencial en el mes (`GET /tenant/uso`, OD-14): lo ve cualquier persona con `configuracion:leer`. */
export function useUsoDelGrupo(empresaId: string, habilitado: boolean) {
  const opciones = getObtenerUsoDelGrupoQueryOptions(undefined, { request: { empresa: empresaId } });
  return useQuery({ ...opciones, queryKey: conEmpresa(opciones.queryKey, empresaId), enabled: habilitado });
}

/** Plan y uso de un grupo, para Plataforma. */
export function usePlanDeUnGrupo(tenantId: string) {
  return useQuery(getObtenerPlanDeUnGrupoQueryOptions(tenantId, undefined, { request: SIN_EMPRESA }));
}

export function useGuardarPlanDeUnGrupo(tenantId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (cuerpo: ActualizarPlanDelGrupoDto) => actualizarPlanDeUnGrupo(tenantId, cuerpo, SIN_EMPRESA),
    onSuccess: () => Promise.all([invalidarRuta(cliente, '/api/v1/plataforma/tenants'), invalidarRuta(cliente, '/api/v1/tenant')]),
  });
}
