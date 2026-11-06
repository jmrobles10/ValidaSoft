import { Rol, type RolAsignadoDto } from '@validasoft/api-client';

export const ROLES = Object.values(Rol);

/** Alcance en el formulario: id de empresa o `TENANT` (rol de todo el grupo, `empresaId: null`). */
export const ALCANCE_TENANT = 'TENANT';

export interface FilaRol {
  rol: Rol;
  alcance: string;
}

export function aFilas(roles: readonly RolAsignadoDto[]): FilaRol[] {
  return roles.map((r) => ({ rol: r.rol, alcance: r.empresaId ?? ALCANCE_TENANT }));
}

/** Filas del formulario → roles para la API, sin duplicados. */
export function aRoles(filas: readonly FilaRol[]): RolAsignadoDto[] {
  const vistos = new Set<string>();
  const roles: RolAsignadoDto[] = [];
  for (const fila of filas) {
    const clave = `${fila.rol}|${fila.alcance}`;
    if (vistos.has(clave)) continue;
    vistos.add(clave);
    roles.push({ rol: fila.rol, empresaId: fila.alcance === ALCANCE_TENANT ? null : fila.alcance });
  }
  return roles;
}
