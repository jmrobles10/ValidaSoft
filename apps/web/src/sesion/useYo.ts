import { useQuery } from '@tanstack/react-query';
import { getObtenerYoQueryOptions } from '@validasoft/api-client';
import { useSesion } from './sesionStore';

/**
 * Quién soy (`GET /api/v1/yo`, RQ-9): la persona de la sesión, sus roles y permisos efectivos. Se pide una vez por
 * sesión (el token cambia al renovarse, pero la persona no) y sin empresa activa.
 */
export function useYo() {
  const usuarioId = useSesion((s) => s.sesion?.usuarioId ?? null);
  const opciones = getObtenerYoQueryOptions({ request: { empresa: null } });
  return useQuery({ ...opciones, queryKey: [...opciones.queryKey, usuarioId], enabled: Boolean(usuarioId), staleTime: Infinity });
}
