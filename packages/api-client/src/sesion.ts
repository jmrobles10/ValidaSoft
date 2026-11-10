/**
 * Lectura del token de sesión de Validasoft (`POST /api/v1/sesiones-usuario`, JWT HS256 de 30 minutos).
 *
 * El navegador **no verifica** la firma: decodifica los claims solo para mostrar u ocultar acciones como
 * conveniencia (docs/06 §6, "Permisos ocultos en el front"). La autorización efectiva la decide siempre la API.
 */

/** Claims que emite `EmisorDeSesiones` (Reclamos en Validasoft.Infraestructura). */
export const RECLAMOS = {
  tenant: 'validasoft:tenant',
  scope: 'scope',
  scopeEmpresa: 'validasoft:scope-empresa',
  empresa: 'validasoft:empresa',
  actorTipo: 'validasoft:actor-tipo',
  sistema: 'validasoft:sistema',
  sistemaId: 'validasoft:sistema-id',
  sujeto: 'sub',
  nombre: 'name',
} as const;

/** Actor de las sesiones de la consola de Plataforma (PT-4): sin tenant ni empresas, solo `plataforma:tenants`. */
export const ACTOR_PLATAFORMA = 'Plataforma';

export interface SesionValidasoft {
  token: string;
  /** Vencimiento según el claim `exp`. */
  expira: Date;
  usuarioId: string;
  /** Nulo solo en la sesión de un operador de Plataforma (no pertenece a ningún tenant). */
  tenantId: string | null;
  /** Nombre visible que trae el token (lo emite la sesión de Plataforma; las de tenant lo piden a `GET /yo`). */
  nombre: string | null;
  /** Código del sistema integrado desde el que se abrió la sesión (SRM, MARCO…). */
  sistema: string | null;
  actorTipo: string | null;
  /** Scopes válidos en cualquier empresa efectiva y en las operaciones de tenant. */
  scopesDeTenant: ReadonlySet<string>;
  /** Scopes adicionales que solo valen con esa empresa activa (roles por empresa). */
  scopesPorEmpresa: ReadonlyMap<string, ReadonlySet<string>>;
  /** Empresas efectivas; `'*'` = todas las del tenant. */
  empresas: ReadonlySet<string> | '*';
}

export class TokenInvalido extends Error {
  override readonly name = 'TokenInvalido';
}

function base64UrlADatos(segmento: string): string {
  const base64 = segmento.replace(/-/g, '+').replace(/_/g, '/');
  const relleno = base64.length % 4 === 0 ? '' : '='.repeat(4 - (base64.length % 4));
  const binario = atob(base64 + relleno);
  const bytes = Uint8Array.from(binario, (c) => c.charCodeAt(0));
  return new TextDecoder().decode(bytes);
}

/** Un claim repetido llega como arreglo; uno solo, como texto. */
function lista(valor: unknown): string[] {
  if (Array.isArray(valor)) return valor.filter((v): v is string => typeof v === 'string');
  if (typeof valor === 'string') return valor.split(' ').filter(Boolean);
  return [];
}

function textoOpcional(valor: unknown): string | null {
  return typeof valor === 'string' && valor.length > 0 ? valor : null;
}

export function leerSesion(token: string): SesionValidasoft {
  const partes = token.split('.');
  const carga = partes[1];
  if (partes.length !== 3 || !carga) throw new TokenInvalido('El token de sesión no es un JWT.');

  let claims: Record<string, unknown>;
  try {
    claims = JSON.parse(base64UrlADatos(carga)) as Record<string, unknown>;
  } catch {
    throw new TokenInvalido('No se pudo leer el contenido del token de sesión.');
  }

  const exp = claims.exp;
  const usuarioId = textoOpcional(claims[RECLAMOS.sujeto]);
  const tenantId = textoOpcional(claims[RECLAMOS.tenant]);
  const actorTipo = textoOpcional(claims[RECLAMOS.actorTipo]);
  if (typeof exp !== 'number' || !usuarioId || (!tenantId && actorTipo !== ACTOR_PLATAFORMA)) {
    throw new TokenInvalido('El token de sesión no trae exp, sub o tenant.');
  }

  const scopesPorEmpresa = new Map<string, Set<string>>();
  for (const valor of lista(claims[RECLAMOS.scopeEmpresa])) {
    const separador = valor.lastIndexOf('|');
    if (separador <= 0) continue;
    const scope = valor.slice(0, separador);
    const empresa = valor.slice(separador + 1);
    const conjunto = scopesPorEmpresa.get(empresa) ?? new Set<string>();
    conjunto.add(scope);
    scopesPorEmpresa.set(empresa, conjunto);
  }

  const empresas = lista(claims[RECLAMOS.empresa]);
  return {
    token,
    expira: new Date(exp * 1000),
    usuarioId,
    tenantId,
    nombre: textoOpcional(claims[RECLAMOS.nombre]),
    sistema: textoOpcional(claims[RECLAMOS.sistema]),
    actorTipo,
    scopesDeTenant: new Set(lista(claims[RECLAMOS.scope])),
    scopesPorEmpresa,
    empresas: empresas.includes('*') ? '*' : new Set(empresas),
  };
}

/** Sesión de un operador de Plataforma (consola `/plataforma`, PT-4): sin tenant y con `plataforma:tenants`. */
export function esSesionDePlataforma(sesion: SesionValidasoft): boolean {
  return sesion.tenantId === null && sesion.actorTipo === ACTOR_PLATAFORMA && sesion.scopesDeTenant.has('plataforma:tenants');
}

/** Scopes que aplican con esa empresa activa (o sin empresa), como `PermisosEfectivos.EnEmpresa` en la API. */
export function scopesEn(sesion: SesionValidasoft, empresaId: string | null): ReadonlySet<string> {
  const adicionales = empresaId ? sesion.scopesPorEmpresa.get(empresaId) : undefined;
  if (!adicionales) return sesion.scopesDeTenant;
  return new Set([...sesion.scopesDeTenant, ...adicionales]);
}

export function puede(sesion: SesionValidasoft, scope: string, empresaId: string | null): boolean {
  return scopesEn(sesion, empresaId).has(scope);
}

export function accedeAEmpresa(sesion: SesionValidasoft, empresaId: string): boolean {
  return sesion.empresas === '*' || sesion.empresas.has(empresaId);
}

/** Milisegundos que le quedan a la sesión (negativo si ya venció). */
export function msRestantes(sesion: SesionValidasoft, ahora: Date = new Date()): number {
  return sesion.expira.getTime() - ahora.getTime();
}
