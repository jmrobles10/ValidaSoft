import { keepPreviousData, useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  getListarSolicitudesHabeasDeLaEmpresaQueryOptions,
  getListarSolicitudesHabeasDePlataformaQueryOptions,
  getObtenerPlanDeSupresionQueryOptions,
  getResumirSolicitudesHabeasDeLaEmpresaQueryOptions,
  getResumirSolicitudesHabeasDePlataformaQueryOptions,
  resolverSolicitudHabeasDeLaEmpresa,
  resolverSolicitudHabeasDePlataforma,
  type ResolverSolicitudHabeasDto,
} from '@validasoft/api-client';
import { conEmpresa, invalidarRuta } from '../../shared/consultas';
import { TAMANO_PAGINA_SOLICITUDES, type FiltroDeSolicitudes } from './modelo';

/** La consola de Plataforma no tiene empresa activa ni tenant (PT-4). */
const SIN_EMPRESA = { empresa: null } as const;

/** Quién atiende: una empresa (lo de sus vínculos) o la plataforma (la bóveda global del tercero). */
export type OrigenDeSolicitudes = { tipo: 'plataforma' } | { tipo: 'empresa'; empresaId: string };

const RUTA_EMPRESA = '/api/v1/solicitudes-habeas';
const RUTA_PLATAFORMA = '/api/v1/plataforma/solicitudes-habeas';

/** Una página de solicitudes con el filtro dado; al cambiar de página o de filtro se sigue viendo la anterior hasta que llega la nueva. */
export function useSolicitudesHabeas(origen: OrigenDeSolicitudes, filtro: FiltroDeSolicitudes, cursor: string | null, habilitado = true) {
  const parametros = { estado: filtro, limite: TAMANO_PAGINA_SOLICITUDES, cursor: cursor ?? undefined };
  const empresa = getListarSolicitudesHabeasDeLaEmpresaQueryOptions(parametros, { request: { empresa: origen.tipo === 'empresa' ? origen.empresaId : null } });
  const plataforma = getListarSolicitudesHabeasDePlataformaQueryOptions(parametros, { request: SIN_EMPRESA });
  const opciones = origen.tipo === 'empresa' ? { ...empresa, queryKey: conEmpresa(empresa.queryKey, origen.empresaId) } : plataforma;
  return useQuery({ ...opciones, placeholderData: keepPreviousData, enabled: habilitado, refetchInterval: 60_000 });
}

/** Cuántas hay abiertas, por vencer, vencidas y resueltas (el aviso de arriba). */
export function useResumenDeSolicitudes(origen: OrigenDeSolicitudes, habilitado = true) {
  const empresa = getResumirSolicitudesHabeasDeLaEmpresaQueryOptions({ request: { empresa: origen.tipo === 'empresa' ? origen.empresaId : null } });
  const plataforma = getResumirSolicitudesHabeasDePlataformaQueryOptions({ request: SIN_EMPRESA });
  const opciones = origen.tipo === 'empresa' ? { ...empresa, queryKey: conEmpresa(empresa.queryKey, origen.empresaId) } : plataforma;
  return useQuery({ ...opciones, enabled: habilitado, refetchInterval: 60_000 });
}

/** Lo que haría la supresión (sin cambiar nada): la plataforma lo muestra antes de confirmar. */
export function usePlanDeSupresion(solicitudId: string, habilitado: boolean) {
  return useQuery({ ...getObtenerPlanDeSupresionQueryOptions(solicitudId, { request: SIN_EMPRESA }), enabled: habilitado, staleTime: 0, gcTime: 0 });
}

export function useResolverSolicitud(origen: OrigenDeSolicitudes) {
  const cliente = useQueryClient();
  return useMutation({
    mutationFn: ({ id, cuerpo }: { id: string; cuerpo: ResolverSolicitudHabeasDto }) =>
      origen.tipo === 'empresa'
        ? resolverSolicitudHabeasDeLaEmpresa(id, cuerpo, { empresa: origen.empresaId })
        : resolverSolicitudHabeasDePlataforma(id, cuerpo, SIN_EMPRESA),
    onSuccess: () => invalidarRuta(cliente, origen.tipo === 'empresa' ? RUTA_EMPRESA : RUTA_PLATAFORMA),
  });
}
