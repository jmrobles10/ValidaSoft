import { keepPreviousData, useInfiniteQuery, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  crearEmpresaDeGrupo,
  desactivarClaveDeArranque,
  desactivarSistemaDeGrupo,
  descargarArchivo,
  fijarTarifaIa,
  getObtenerClaveDeArranqueQueryOptions,
  getObtenerTarifaIaQueryOptions,
  listarIngresosOperadores,
  listarSistemasDeGrupo,
  obtenerConsumoIa,
  reactivarClaveDeArranque,
  reactivarOperadorPlataforma,
  reactivarSistemaDeGrupo,
  type ArchivoDescargado,
  type FijarTarifaDeIaDto,
  type ListarIngresosOperadoresParams,
} from '@validasoft/api-client';
import { invalidarRuta } from '../../shared/consultas';
import { cuerpoCrearEmpresa, type BorradorEmpresa } from '../configuracion/empresas/modelo';

/** La consola de Plataforma no tiene empresa activa ni tenant (PT-4). */
const SIN_EMPRESA = { empresa: null } as const;

export const TAMANO_PAGINA_CONSUMO = 25;
export const TAMANO_PAGINA_INGRESOS = 50;

// ---------- Consumo de IA por cliente (PI-12) ----------

export interface FiltroDeConsumo {
  periodo: string;
  q: string;
}

/** El consumo del mes por grupo, de a páginas: la siguiente se pide con el cursor de la última (los totales son los de todo el mes). */
export function useConsumoIa({ periodo, q }: FiltroDeConsumo) {
  const texto = q.trim() || undefined;
  return useInfiniteQuery({
    queryKey: ['/api/v1/plataforma/ia/consumo', { periodo, q: texto }],
    queryFn: ({ pageParam, signal }) => obtenerConsumoIa({ periodo, q: texto, limite: TAMANO_PAGINA_CONSUMO, cursor: pageParam }, SIN_EMPRESA, signal),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (ultima) => ultima.siguienteCursor ?? undefined,
    placeholderData: keepPreviousData,
  });
}

/** Excel o CSV con lo mismo que se ve (mismos filtros), por la descarga binaria del cliente. */
export function useExportarConsumoIa() {
  return useMutation({
    mutationFn: ({ periodo, q, formato }: FiltroDeConsumo & { formato: 'xlsx' | 'csv' }): Promise<ArchivoDescargado> =>
      descargarArchivo({ url: '/api/v1/plataforma/ia/consumo/exportar', params: { periodo, q: q.trim() || undefined, formato } }, SIN_EMPRESA),
  });
}

export function useTarifaIa() {
  return useQuery(getObtenerTarifaIaQueryOptions({ request: SIN_EMPRESA }));
}

export function useFijarTarifaIa() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (cuerpo: FijarTarifaDeIaDto) => fijarTarifaIa(cuerpo, SIN_EMPRESA),
    onSettled: () => invalidarRuta(cliente, '/api/v1/plataforma/ia'),
  });
}

// ---------- Ingresos de los operadores (PI-16) ----------

export type FiltroDeIngresos = Pick<ListarIngresosOperadoresParams, 'operador' | 'evento' | 'exitoso' | 'desde' | 'hasta'>;

/** La auditoría de ingreso de los operadores de Plataforma, de la más reciente a la más antigua, de a páginas. */
export function useIngresosOperadores(filtro: FiltroDeIngresos) {
  return useInfiniteQuery({
    queryKey: ['/api/v1/plataforma/auditoria-ingresos', filtro],
    queryFn: ({ pageParam, signal }) => listarIngresosOperadores({ ...filtro, limite: TAMANO_PAGINA_INGRESOS, cursor: pageParam }, SIN_EMPRESA, signal),
    initialPageParam: undefined as string | undefined,
    getNextPageParam: (ultima) => ultima.siguienteCursor ?? undefined,
    placeholderData: keepPreviousData,
  });
}

// ---------- Clave de arranque (PI-17) ----------

export function useClaveDeArranque() {
  return useQuery(getObtenerClaveDeArranqueQueryOptions({ request: SIN_EMPRESA }));
}

export function useCambiarClaveDeArranque() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (desactivar: boolean) => (desactivar ? desactivarClaveDeArranque(SIN_EMPRESA) : reactivarClaveDeArranque(SIN_EMPRESA)),
    onSettled: () => invalidarRuta(cliente, '/api/v1/plataforma/clave-de-arranque'),
  });
}

// ---------- Operadores, empresas y sistemas de un grupo (PI-7, PI-9) ----------

export function useReactivarOperador() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (operadorId: string) => reactivarOperadorPlataforma(operadorId, SIN_EMPRESA),
    onSettled: () => invalidarRuta(cliente, '/api/v1/plataforma/operadores'),
  });
}

/** Agrega una empresa a un grupo que ya existe (PI-9); lleva `Idempotency-Key`: repetir el envío no la duplica. */
export function useCrearEmpresaDeGrupo(tenantId: string, claveIdempotencia: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (borrador: BorradorEmpresa) => crearEmpresaDeGrupo(tenantId, cuerpoCrearEmpresa(borrador), { ...SIN_EMPRESA, idempotencyKey: claveIdempotencia }),
    onSuccess: () => invalidarRuta(cliente, '/api/v1/plataforma/grupos'),
  });
}

export function useSistemasDeGrupo(tenantId: string, habilitado: boolean) {
  return useQuery({ queryKey: [`/api/v1/plataforma/tenants/${tenantId}/sistemas`], queryFn: ({ signal }) => listarSistemasDeGrupo(tenantId, SIN_EMPRESA, signal), enabled: habilitado });
}

export function useCambiarSistemaDeGrupo(tenantId: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ sistemaId, desactivar }: { sistemaId: string; desactivar: boolean }) =>
      desactivar ? desactivarSistemaDeGrupo(tenantId, sistemaId, SIN_EMPRESA) : reactivarSistemaDeGrupo(tenantId, sistemaId, SIN_EMPRESA),
    onSettled: () => invalidarRuta(cliente, `/api/v1/plataforma/tenants/${tenantId}/sistemas`),
  });
}
