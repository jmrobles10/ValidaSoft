import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  crearEmpresa,
  desactivarEmpresa,
  getListarPaisesQueryOptions,
  getObtenerTextosLegalesEmpresaQueryOptions,
  publicarTextoLegalEmpresa,
} from '@validasoft/api-client';
import { invalidarRuta } from '../../../shared/consultas';
import { cuerpoCrearEmpresa, type BorradorEmpresa, type RutaTexto } from './modelo';

/** Las empresas y sus textos legales son del grupo (tenant): sin empresa activa. */
const SIN_EMPRESA = { empresa: null } as const;

/** Países y sus tipos de identificación (catálogo fijo en H1). */
export function usePaises(habilitado: boolean) {
  return useQuery({ ...getListarPaisesQueryOptions({ request: SIN_EMPRESA }), enabled: habilitado, staleTime: 60 * 60_000 });
}

export function useTextosLegales(empresaId: string, habilitado: boolean) {
  return useQuery({ ...getObtenerTextosLegalesEmpresaQueryOptions(empresaId, { request: SIN_EMPRESA }), enabled: habilitado });
}

/** `invalidarRuta('/api/v1/empresas')` también refresca el selector de empresa de la barra superior. */
export function useCrearEmpresa(claveIdempotencia: string) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (b: BorradorEmpresa) => crearEmpresa(cuerpoCrearEmpresa(b), { ...SIN_EMPRESA, idempotencyKey: claveIdempotencia }),
    onSuccess: () => invalidarRuta(cliente, '/api/v1/empresas'),
  });
}

export function useDesactivarEmpresa() {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (empresaId: string) => desactivarEmpresa(empresaId, SIN_EMPRESA),
    onSettled: () => invalidarRuta(cliente, '/api/v1/empresas'),
  });
}

/** Cada publicación es una versión nueva (la anterior queda en el historial). */
export function usePublicarTexto(empresaId: string, ruta: RutaTexto) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: (contenido: string) => publicarTextoLegalEmpresa(empresaId, ruta, { contenido: contenido.trim() }, SIN_EMPRESA),
    onSuccess: () => invalidarRuta(cliente, `/api/v1/empresas/${empresaId}/textos-legales`),
  });
}
