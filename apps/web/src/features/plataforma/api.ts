import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarPlanIaEmpresa,
  crearTenant,
  getListarGruposQueryOptions,
  getObtenerEstadoSecretosQueryOptions,
  getObtenerPlanIaEmpresaQueryOptions,
  probarLlaveIaEmpresa,
  recifrarSecretos,
  type ActualizarPlanIaEmpresaDto,
  type CrearTenantDto,
} from '@validasoft/api-client';
import { invalidarRuta } from '../../shared/consultas';

/** La consola de Plataforma no tiene empresa activa ni tenant (PT-4). */
const SIN_EMPRESA = { empresa: null } as const;

export function useGrupos() {
  return useQuery(getListarGruposQueryOptions({ request: SIN_EMPRESA }));
}

/** La respuesta trae los valores de las dos API keys: solo se ven esta vez. */
export function useCrearGrupo(claveIdempotencia: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (cuerpo: CrearTenantDto) => crearTenant(cuerpo, { ...SIN_EMPRESA, idempotencyKey: claveIdempotencia }),
    onSuccess: () => Promise.all([invalidarRuta(cliente, '/api/v1/plataforma/grupos'), invalidarRuta(cliente, '/api/v1/plataforma/tenants')]),
  });
}

export function usePlanIa(tenantId: string, empresaId: string) {
  return useQuery(getObtenerPlanIaEmpresaQueryOptions(tenantId, empresaId, { request: SIN_EMPRESA }));
}

export function useGuardarPlanIa(tenantId: string, empresaId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (cuerpo: ActualizarPlanIaEmpresaDto) => actualizarPlanIaEmpresa(tenantId, empresaId, cuerpo, SIN_EMPRESA),
    onSuccess: (plan) => {
      cliente.setQueryData(getObtenerPlanIaEmpresaQueryOptions(tenantId, empresaId).queryKey, plan);
      return invalidarRuta(cliente, '/api/v1/plataforma/grupos');
    },
  });
}

/** Solo cuando el operador lo pide: con `llave` prueba esa (sin guardarla); sin ella, la guardada. */
export function useProbarLlave(tenantId: string, empresaId: string) {
  return useMutation({
    mutationFn: (llave: string | null) => probarLlaveIaEmpresa(tenantId, empresaId, { apiKeyOpenAi: llave }, SIN_EMPRESA),
  });
}

export function useEstadoSecretos() {
  return useQuery(getObtenerEstadoSecretosQueryOptions({ request: SIN_EMPRESA }));
}

export function useRecifrarSecretos() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: () => recifrarSecretos(SIN_EMPRESA),
    onSuccess: () => invalidarRuta(cliente, '/api/v1/plataforma/secretos'),
  });
}
