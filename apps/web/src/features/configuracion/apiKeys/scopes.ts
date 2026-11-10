import { SCOPES } from '@validasoft/api-client';

/**
 * Permisos que puede llevar una API key: exactamente `Permisos.DelTenant` (src/Validasoft.Aplicacion/Comun/Permisos.cs;
 * la de Plataforma no entra). La API rechaza cualquier otro con 422 `scopes-invalidos`. Se agrupan por lo que tocan para
 * elegirlos y para resumirlos en la tabla.
 */
export type ClavePermiso = Exclude<keyof typeof SCOPES, 'plataformaTenants'>;

export const GRUPOS_PERMISOS = [
  { grupo: 'administracion', claves: ['empresasAdministrar', 'apiKeysAdministrar', 'sistemasAdministrar', 'usuariosAdministrar'] },
  { grupo: 'configuracion', claves: ['configuracionLeer', 'configuracionEscribir', 'flujosLeer', 'flujosEscribir'] },
  { grupo: 'vinculos', claves: ['tercerosLeer', 'tercerosEscribir', 'vinculosLeer', 'vinculosEscribir', 'vinculosBloquear', 'cuentasAprobar'] },
  { grupo: 'revision', claves: ['expedientesLeer', 'expedientesRevisar', 'expedientesAdministrar', 'documentosLeer', 'documentosDescargar', 'certificadosLeer'] },
  { grupo: 'integracion', claves: ['eventosLeer', 'webhooksAdministrar', 'consultasLeer'] },
  { grupo: 'cumplimiento', claves: ['verificacionesEscribir', 'cumplimientoDecidir'] },
  { grupo: 'auditoria', claves: ['auditoriaLeer'] },
] as const satisfies readonly { grupo: string; claves: readonly ClavePermiso[] }[];

export type GrupoPermisos = (typeof GRUPOS_PERMISOS)[number]['grupo'];

/** Los scopes del tenant, en el orden de los grupos. */
export const SCOPES_DEL_TENANT: readonly string[] = GRUPOS_PERMISOS.flatMap((g) => g.claves.map((c) => SCOPES[c]));

const CLAVE_DE_SCOPE = new Map<string, ClavePermiso>(GRUPOS_PERMISOS.flatMap((g) => g.claves.map((c) => [SCOPES[c], c] as const)));

/** `vinculos:escribir` → `vinculosEscribir` (para su texto en i18n); `null` si no es un scope del tenant. */
export function claveDeScope(scope: string): ClavePermiso | null {
  return CLAVE_DE_SCOPE.get(scope) ?? null;
}

/** Marca o desmarca un scope sin repetir. */
export function alternarScope(actuales: readonly string[], scope: string): string[] {
  return actuales.includes(scope) ? actuales.filter((s) => s !== scope) : [...actuales, scope];
}

/** Marca todo el grupo o, si ya estaba completo, lo desmarca. */
export function alternarGrupo(actuales: readonly string[], grupo: GrupoPermisos): string[] {
  const delGrupo = scopesDeGrupo(grupo);
  const completo = delGrupo.every((s) => actuales.includes(s));
  return completo ? actuales.filter((s) => !delGrupo.includes(s)) : [...actuales, ...delGrupo.filter((s) => !actuales.includes(s))];
}

export function scopesDeGrupo(grupo: GrupoPermisos): string[] {
  return GRUPOS_PERMISOS.find((g) => g.grupo === grupo)?.claves.map((c) => SCOPES[c]) ?? [];
}

export interface ResumenPermisos {
  /** Tiene todos los scopes del tenant. */
  todos: boolean;
  /** Grupos con al menos un scope, en orden, y cuántos de cada uno. */
  grupos: { grupo: GrupoPermisos; cuantos: number; total: number }[];
  /** Scopes que la web no conoce (la API agregó uno nuevo): se muestran tal cual. */
  desconocidos: string[];
}

/** Resumen para la tabla: «Todos los permisos» o los grupos que toca la llave. */
export function resumirScopes(scopes: readonly string[]): ResumenPermisos {
  const unicos = new Set(scopes);
  const grupos = GRUPOS_PERMISOS.map((g) => ({ grupo: g.grupo, cuantos: g.claves.filter((c) => unicos.has(SCOPES[c])).length, total: g.claves.length })).filter(
    (g) => g.cuantos > 0,
  );
  return {
    todos: SCOPES_DEL_TENANT.every((s) => unicos.has(s)),
    grupos,
    desconocidos: [...unicos].filter((s) => !CLAVE_DE_SCOPE.has(s)),
  };
}
