import { useQuery } from '@tanstack/react-query';
import { getListarUsuariosQueryOptions, SCOPES } from '@validasoft/api-client';
import { useMemo } from 'react';
import { usePermisos } from '../../../shared/usePermisos';

/** Usuarios del tenant (requiere `usuarios:administrar`). */
export function useUsuarios(habilitado = true) {
  return useQuery({ ...getListarUsuariosQueryOptions(undefined, { request: { empresa: null } }), enabled: habilitado });
}

const VACIO: ReadonlyMap<string, string> = new Map();

/** Nombres de usuarios para el historial, solo si la persona puede listarlos; si no, un mapa vacío. */
export function useNombresUsuarios(empresaId: string): ReadonlyMap<string, string> {
  const permisos = usePermisos(empresaId);
  const { data } = useUsuarios(permisos.puede(SCOPES.usuariosAdministrar));
  return useMemo(() => (data ? new Map(data.map((u) => [u.id, u.nombre])) : VACIO), [data]);
}
