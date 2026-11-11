import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  actualizarPlanIaEmpresa,
  crearTenant,
  getListarGruposQueryKey,
  getObtenerEstadoSecretosQueryOptions,
  getObtenerGrupoQueryOptions,
  getObtenerPlanIaEmpresaQueryOptions,
  listarGrupos,
  probarLlaveIaEmpresa,
  recifrarSecretos,
  type ActualizarPlanIaEmpresaDto,
  type CrearTenantDto,
} from '@validasoft/api-client';
import { invalidarRuta } from '../../shared/consultas';

/** La consola de Plataforma no tiene empresa activa ni tenant (PT-4). */
const SIN_EMPRESA = { empresa: null } as const;

/** Grupos por página del scroll infinito (el servidor admite hasta 100). */
export const TAMANO_PAGINA_GRUPOS = 20;

/**
 * Los grupos de a páginas (PT-9, PT-11): la siguiente se pide con el cursor de la última. `q` busca por grupo, empresa o NIT.
 * La clave empieza por la ruta de la API (para que `invalidarRuta` la alcance) y se distingue de la de un grupo suelto.
 */
export function useGrupos(q: string) {
  const texto = q.trim() || undefined;
  return useInfiniteQuery({
    queryKey: [...getListarGruposQueryKey({ q: texto, limite: TAMANO_PAGINA_GRUPOS }), 'paginas'],
    queryFn: ({ pageParam, signal }) => listarGrupos({ q: texto, limite: TAMANO_PAGINA_GRUPOS, cursor: pageParam }, SIN_EMPRESA, signal),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (ultima) => ultima.siguienteCursor ?? undefined,
    // Al cambiar la búsqueda se sigue viendo la lista anterior (atenuada) hasta que llega la nueva.
    placeholderData: keepPreviousData,
  });
}

/** Un grupo con sus empresas, para rotular la pantalla del plan sin recorrer la lista. */
export function useGrupo(tenantId: string) {
  return useQuery(getObtenerGrupoQueryOptions(tenantId, { request: SIN_EMPRESA }));
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
