import { scopesEn, type Scope } from '@validasoft/api-client';
import { useMemo } from 'react';
import { useSesion } from '../sesion/sesionStore';

export interface Permisos {
  scopes: ReadonlySet<string>;
  puede: (scope: Scope) => boolean;
}

/**
 * Scopes de la sesión en la empresa activa. Es una conveniencia para ocultar lo que la persona no puede hacer
 * (docs/06 §6); la API vuelve a decidir en cada petición.
 */
export function usePermisos(empresaId: string | null): Permisos {
  const sesion = useSesion((s) => s.sesion);
  return useMemo(() => {
    const scopes = sesion ? scopesEn(sesion, empresaId) : new Set<string>();
    return { scopes, puede: (scope: Scope) => scopes.has(scope) };
  }, [sesion, empresaId]);
}
