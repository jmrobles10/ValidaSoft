import { useQuery } from '@tanstack/react-query';
import { getListarEmpresasQueryOptions } from '@validasoft/api-client';
import { useMemo } from 'react';

/** Nombre de cada empresa del grupo por su id (para rotular los roles de «Mi cuenta»); vacío si no aplica (operadores de Plataforma). */
export function useEmpresasPorId(habilitado: boolean): ReadonlyMap<string, string> {
  const consulta = useQuery({ ...getListarEmpresasQueryOptions({ request: { empresa: null } }), enabled: habilitado, staleTime: 5 * 60_000 });
  return useMemo(() => new Map((consulta.data ?? []).map((e) => [e.id, e.nombre])), [consulta.data]);
}
