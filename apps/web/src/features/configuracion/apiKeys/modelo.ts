import type { ApiKeyDto, CrearApiKeyDto, EmpresaDto } from '@validasoft/api-client';
import { SCOPES_DEL_TENANT } from './scopes';

/**
 * API keys del tenant (docs/07 §4). Misma validación que `ApiKeysEndpoints.Crear` para avisar antes de enviar; la API
 * decide (422 `nombre-requerido`, `scopes-invalidos`, `expiracion-invalida`, `empresas-invalidas`, `sistema-invalido`;
 * 403 `alcance-insuficiente` si la credencial no alcanza todas las empresas pedidas).
 */
export const TODO_EL_GRUPO = '*';

/** Las fechas se leen en la zona del tenant por defecto (Bogotá, UTC−5, sin horario de verano). */
const DESFASE_BOGOTA = '-05:00';

export interface BorradorApiKey {
  nombre: string;
  scopes: string[];
  /** `['*']` = todo el grupo (también las empresas que se agreguen). */
  empresas: string[];
  /** `AAAA-MM-DD` o vacío (no expira). */
  expira: string;
  /** Id del sistema integrado o vacío. */
  sistemaId: string;
}

export type ErrorApiKey = 'nombre-requerido' | 'scopes-requeridos' | 'empresas-requeridas' | 'expiracion-invalida';

export function borradorApiKeyNuevo(): BorradorApiKey {
  return { nombre: '', scopes: [], empresas: [TODO_EL_GRUPO], expira: '', sistemaId: '' };
}

/** Fin del día elegido en Bogotá, en ISO (la llave vale todo ese día). */
export function finDelDia(fecha: string): string | null {
  if (!/^\d{4}-\d{2}-\d{2}$/.test(fecha)) return null;
  const instante = new Date(`${fecha}T23:59:59${DESFASE_BOGOTA}`);
  return Number.isNaN(instante.getTime()) ? null : instante.toISOString();
}

export function validarApiKey(b: BorradorApiKey, ahora: Date): ErrorApiKey[] {
  const errores: ErrorApiKey[] = [];
  if (!b.nombre.trim()) errores.push('nombre-requerido');
  if (b.scopes.length === 0) errores.push('scopes-requeridos');
  if (b.empresas.length === 0) errores.push('empresas-requeridas');
  if (b.expira) {
    const fin = finDelDia(b.expira);
    if (!fin || new Date(fin) <= ahora) errores.push('expiracion-invalida');
  }
  return errores;
}

export function cuerpoCrearApiKey(b: BorradorApiKey): CrearApiKeyDto {
  // Solo scopes del tenant y en orden estable (la API rechaza cualquier otro).
  const scopes = SCOPES_DEL_TENANT.filter((s) => b.scopes.includes(s));
  return {
    nombre: b.nombre.trim(),
    scopes,
    empresas: b.empresas.includes(TODO_EL_GRUPO) ? null : b.empresas,
    expiraEn: b.expira ? finDelDia(b.expira) : null,
    sistemaId: b.sistemaId || null,
  };
}

export function esDeTodoElGrupo(llave: Pick<ApiKeyDto, 'empresas'>): boolean {
  return llave.empresas.length === 0 || llave.empresas.includes(TODO_EL_GRUPO);
}

/** «Todo el grupo» o los nombres de las empresas de la llave (el id si la empresa ya no está a la vista). */
export function textoAlcance(llave: Pick<ApiKeyDto, 'empresas'>, empresas: readonly Pick<EmpresaDto, 'id' | 'nombre'>[], todoElGrupo: string): string {
  if (esDeTodoElGrupo(llave)) return todoElGrupo;
  return llave.empresas.map((id) => empresas.find((e) => e.id === id)?.nombre ?? id).join(', ');
}

/** El listado no trae las revocadas; una llave listada está vigente o ya venció. */
export function estadoApiKey(llave: Pick<ApiKeyDto, 'expiraEn'>, ahora: Date): 'vigente' | 'vencida' {
  return llave.expiraEn && new Date(llave.expiraEn) <= ahora ? 'vencida' : 'vigente';
}

/** Lo que se ve de la clave sin el secreto: su prefijo («vsk_live_3f9a…»). */
export function claveVisible(llave: Pick<ApiKeyDto, 'prefijo'>): string {
  return `${llave.prefijo}…`;
}
