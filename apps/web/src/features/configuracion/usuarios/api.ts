import { useQuery } from '@tanstack/react-query';
import { getListarUsuariosQueryOptions, getResumirUsuariosQueryOptions, SCOPES, type EventoHistorialDto } from '@validasoft/api-client';
import { useMemo } from 'react';
import { conEmpresa } from '../../../shared/consultas';
import { usePermisos } from '../../../shared/usePermisos';

/** Usuarios del tenant (requiere `usuarios:administrar`). */
export function useUsuarios(habilitado = true) {
  return useQuery({ ...getListarUsuariosQueryOptions(undefined, { request: { empresa: null } }), enabled: habilitado });
}

const VACIO: ReadonlyMap<string, string> = new Map();
/** La API acepta hasta 100 ids por llamada (RQ-9). */
const MAXIMO_IDS = 100;

/**
 * Nombres de las personas que aparecen en pantalla (`GET /usuarios/resumen?ids=`, RQ-9): basta `expedientes:leer` o
 * `vinculos:leer`, así que un revisor ve el nombre de quien aprobó en vez de «Persona del área». Sin ninguno de los
 * dos permisos, o si la llamada falla, el mapa queda vacío y la UI usa su texto de respaldo.
 */
export function useNombresUsuarios(empresaId: string, ids: readonly string[]): ReadonlyMap<string, string> {
  const permisos = usePermisos(empresaId);
  const puede = permisos.puede(SCOPES.expedientesLeer) || permisos.puede(SCOPES.vinculosLeer);
  const unicos = useMemo(() => [...new Set(ids)].sort().slice(0, MAXIMO_IDS), [ids]);
  const opciones = getResumirUsuariosQueryOptions({ ids: unicos.join(',') }, { request: { empresa: empresaId } });
  const { data } = useQuery({
    ...opciones,
    queryKey: conEmpresa(opciones.queryKey, empresaId),
    enabled: puede && unicos.length > 0,
    staleTime: 10 * 60_000,
  });
  return useMemo(() => (data ? new Map(data.map((u) => [u.id, u.nombre])) : VACIO), [data]);
}

/** Personas que actuaron en un historial (actor `Usuario`). */
export function actoresDeHistorial(eventos: readonly EventoHistorialDto[] | undefined): string[] {
  return (eventos ?? []).filter((e) => e.actorTipo === 'Usuario' && e.actorId).map((e) => e.actorId ?? '');
}
