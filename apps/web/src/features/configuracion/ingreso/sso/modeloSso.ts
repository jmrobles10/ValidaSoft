import { Rol, type ConfiguracionDeSsoDto, type FijarSsoDto, type ReglaDeGrupoSsoDto, type RolAsignadoDto } from '@validasoft/api-client';

/** Proveedores de identidad que ofrece la instalación: OIDC genérico (Entra, Google Workspace…) y WorkOS (si está configurado). */
export const PROVEEDORES_SSO = ['oidc', 'workos'] as const;
export type ProveedorSso = (typeof PROVEEDORES_SSO)[number];

export const ROLES_SSO: readonly Rol[] = Object.values(Rol);

/** El valor de un selector de rol como `Rol` (lo desconocido cae en Revisor, el de menos alcance). */
export const aRolSso = (valor: string): Rol => ROLES_SSO.find((r) => r === valor) ?? Rol.Revisor;

export type ModoSso = 'Complementario' | 'Exclusivo';

/** Una fila de rol: sin empresa vale en todo el grupo. */
export interface FilaDeRol {
  rol: Rol;
  empresaId: string;
}

export interface FilaDeRegla extends FilaDeRol {
  grupo: string;
}

/** Lo que el formulario edita (el secreto se escribe aparte: nunca vuelve de la API). */
export interface FormularioSso {
  activo: boolean;
  proveedor: ProveedorSso;
  nombre: string;
  autoridad: string;
  clientId: string;
  organizacionWorkOs: string;
  conexionWorkOs: string;
  reclamoDeGrupos: string;
  aprovisionamientoJit: boolean;
  rolesPorOmision: FilaDeRol[];
  reglas: FilaDeRegla[];
  sincronizarRoles: boolean;
  modo: ModoSso;
}

export const RECLAMO_DE_GRUPOS = 'groups';

export function formularioDesde(dto: ConfiguracionDeSsoDto): FormularioSso {
  return {
    activo: dto.activo,
    proveedor: dto.proveedor === 'workos' ? 'workos' : 'oidc',
    nombre: dto.nombre,
    autoridad: dto.autoridad ?? '',
    clientId: dto.clientId ?? '',
    organizacionWorkOs: dto.organizacionWorkOs ?? '',
    conexionWorkOs: dto.conexionWorkOs ?? '',
    reclamoDeGrupos: dto.reclamoDeGrupos || RECLAMO_DE_GRUPOS,
    aprovisionamientoJit: dto.aprovisionamientoJit,
    rolesPorOmision: dto.rolesPorOmision.map((r) => ({ rol: r.rol, empresaId: r.empresaId ?? '' })),
    reglas: dto.reglas.map((r) => ({ grupo: r.grupo, rol: aRolSso(r.rol), empresaId: r.empresaId ?? '' })),
    sincronizarRoles: dto.sincronizarRoles,
    modo: dto.modo === 'Exclusivo' ? 'Exclusivo' : 'Complementario',
  };
}

const aRol = (fila: FilaDeRol): RolAsignadoDto => ({ rol: fila.rol, empresaId: fila.empresaId || null });
const aRegla = (fila: FilaDeRegla): ReglaDeGrupoSsoDto => ({ grupo: fila.grupo.trim(), rol: fila.rol, empresaId: fila.empresaId || null });

/**
 * El cuerpo de `PUT /configuracion/sso`. `secreto` nulo conserva el actual (la API nunca lo devuelve); vacío lo quita. Con OIDC se envían el emisor y el client id; con
 * WorkOS, la organización o la conexión.
 */
export function cuerpoDeSso(f: FormularioSso, secreto: string | null): FijarSsoDto {
  const oidc = f.proveedor === 'oidc';
  return {
    activo: f.activo,
    proveedor: f.proveedor,
    nombre: f.nombre.trim(),
    autoridad: oidc ? f.autoridad.trim() : null,
    clientId: oidc ? f.clientId.trim() : null,
    secretoDelCliente: oidc ? secreto : null,
    reclamoDeGrupos: f.reclamoDeGrupos.trim() || RECLAMO_DE_GRUPOS,
    organizacionWorkOs: oidc ? null : f.organizacionWorkOs.trim() || null,
    conexionWorkOs: oidc ? null : f.conexionWorkOs.trim() || null,
    aprovisionamientoJit: f.aprovisionamientoJit,
    rolesPorOmision: f.rolesPorOmision.map(aRol),
    reglas: f.reglas.filter((r) => r.grupo.trim().length > 0).map(aRegla),
    sincronizarRoles: f.sincronizarRoles,
    modo: f.modo,
  };
}

/** Errores de llenado que la persona puede corregir antes de guardar (la API valida lo mismo y más). */
export type ProblemaSso = 'nombre' | 'autoridad' | 'clientId' | 'conexionWorkOs' | 'rolPorOmision' | 'dominio';

export function problemasDeSso(f: FormularioSso, tieneDominioVerificado: boolean): ProblemaSso[] {
  const problemas: ProblemaSso[] = [];
  if (f.nombre.trim().length === 0) problemas.push('nombre');
  if (f.proveedor === 'oidc') {
    if (!/^https?:\/\/[^\s/]+/i.test(f.autoridad.trim())) problemas.push('autoridad');
    if (f.clientId.trim().length === 0) problemas.push('clientId');
  } else if (f.organizacionWorkOs.trim().length === 0 && f.conexionWorkOs.trim().length === 0) problemas.push('conexionWorkOs');
  if (f.aprovisionamientoJit && f.rolesPorOmision.length === 0 && f.reglas.every((r) => r.grupo.trim().length === 0)) problemas.push('rolPorOmision');
  if (f.activo && !tieneDominioVerificado) problemas.push('dominio');
  return problemas;
}

/** Las dos descripciones iguales (ignora el orden de las filas): evita ofrecer «Guardar» cuando no cambió nada. */
export function sinCambiosDeSso(a: FormularioSso, b: FormularioSso, secretoNuevo: string | null): boolean {
  return secretoNuevo === null && JSON.stringify(normalizar(a)) === JSON.stringify(normalizar(b));
}

function normalizar(f: FormularioSso): unknown {
  return {
    ...f,
    nombre: f.nombre.trim(),
    autoridad: f.autoridad.trim().replace(/\/$/, ''),
    clientId: f.clientId.trim(),
    rolesPorOmision: [...f.rolesPorOmision].sort((x, y) => `${x.rol}${x.empresaId}`.localeCompare(`${y.rol}${y.empresaId}`)),
    reglas: [...f.reglas].sort((x, y) => `${x.grupo}${x.rol}${x.empresaId}`.localeCompare(`${y.grupo}${y.rol}${y.empresaId}`)),
  };
}

/** Resumen de a quién afecta cada modo (lo que el administrador debe saber antes de elegirlo). */
export function dominiosVerificados(dto: ConfiguracionDeSsoDto): string[] {
  return dto.dominios.filter((d) => d.verificado).map((d) => d.dominio);
}

/** Texto de un dominio con su `@`: «@empresa.co». */
export const conArroba = (dominio: string): string => `@${dominio}`;

/** Qué se hace con el secreto del cliente: conservar el guardado, escribir uno nuevo o quitarlo (la API nunca lo devuelve). */
export type AccionDeSecreto = 'conservar' | 'cambiar' | 'quitar';

/** Lo que viaja en `secretoDelCliente`: nulo conserva, vacío quita, texto reemplaza. */
export function secretoParaEnviar(accion: AccionDeSecreto, valor: string): string | null {
  if (accion === 'quitar') return '';
  if (accion === 'cambiar') return valor.trim();
  return null;
}

/** El secreto nuevo que se escribió es válido (8 a 2000 caracteres, sin espacios), igual que en la API. */
export function secretoValido(valor: string): boolean {
  const limpio = valor.trim();
  return limpio.length >= 8 && limpio.length <= 2000 && !/\s/.test(limpio);
}
