import type { EstadoVinculo } from './generado/modelos/estadoVinculo';
import type { VinculoDto } from './generado/modelos/vinculoDto';

/** Scopes de docs/07 §3.2 (Permisos en Validasoft.Aplicacion.Comun). */
export const SCOPES = {
  plataformaTenants: 'plataforma:tenants',
  empresasAdministrar: 'empresas:administrar',
  apiKeysAdministrar: 'api-keys:administrar',
  sistemasAdministrar: 'sistemas:administrar',
  usuariosAdministrar: 'usuarios:administrar',
  configuracionLeer: 'configuracion:leer',
  configuracionEscribir: 'configuracion:escribir',
  tercerosLeer: 'terceros:leer',
  tercerosEscribir: 'terceros:escribir',
  vinculosLeer: 'vinculos:leer',
  vinculosEscribir: 'vinculos:escribir',
  vinculosBloquear: 'vinculos:bloquear',
  auditoriaLeer: 'auditoria:leer',
  flujosLeer: 'flujos:leer',
  flujosEscribir: 'flujos:escribir',
  expedientesLeer: 'expedientes:leer',
  expedientesRevisar: 'expedientes:revisar',
  expedientesAdministrar: 'expedientes:administrar',
  documentosLeer: 'documentos:leer',
  documentosDescargar: 'documentos:descargar',
  certificadosLeer: 'certificados:leer',
  eventosLeer: 'eventos:leer',
  webhooksAdministrar: 'webhooks:administrar',
} as const;

export type Scope = (typeof SCOPES)[keyof typeof SCOPES];

/** Estados sin salida (Vinculo.Terminales): no admiten bloquear, desvincular ni anular. */
export const ESTADOS_TERMINALES: readonly EstadoVinculo[] = ['Anulado', 'Desvinculado'];

export type AccionVinculo = 'invitar' | 'bloquear' | 'desbloquear' | 'desvincular' | 'reintegrar' | 'anular';

export interface DefinicionAccion {
  accion: AccionVinculo;
  scope: Scope;
  /** Qué texto exige la API: motivo, justificación o ninguno. */
  requiere: 'motivo' | 'justificacion' | 'correo';
  /** Acción irreversible o de alto impacto: pide confirmación explícita. */
  destructiva: boolean;
}

export const ACCIONES_VINCULO: readonly DefinicionAccion[] = [
  { accion: 'invitar', scope: SCOPES.vinculosEscribir, requiere: 'correo', destructiva: false },
  { accion: 'bloquear', scope: SCOPES.vinculosBloquear, requiere: 'motivo', destructiva: true },
  { accion: 'desbloquear', scope: SCOPES.vinculosBloquear, requiere: 'justificacion', destructiva: false },
  { accion: 'desvincular', scope: SCOPES.vinculosEscribir, requiere: 'motivo', destructiva: true },
  { accion: 'reintegrar', scope: SCOPES.vinculosEscribir, requiere: 'justificacion', destructiva: false },
  { accion: 'anular', scope: SCOPES.vinculosEscribir, requiere: 'motivo', destructiva: true },
];

/**
 * ¿El estado del vínculo admite la acción? Replica las guardas del agregado `Vinculo` para no ofrecer botones que
 * la API rechazaría con 409; si la regla cambia en el backend, la API sigue siendo la que decide.
 */
export function accionPermitidaPorEstado(
  accion: AccionVinculo,
  vinculo: Pick<VinculoDto, 'estado' | 'invitacionAceptadaEn'>,
): boolean {
  const terminal = ESTADOS_TERMINALES.includes(vinculo.estado);
  switch (accion) {
    case 'invitar':
      return vinculo.estado === 'Invitado' && !vinculo.invitacionAceptadaEn;
    case 'bloquear':
      return vinculo.estado !== 'Bloqueado' && !terminal;
    case 'desbloquear':
      return vinculo.estado === 'Bloqueado';
    case 'desvincular':
    case 'anular':
      return !terminal;
    case 'reintegrar':
      return vinculo.estado === 'Desvinculado';
  }
}

/** Acciones que la persona puede ejecutar: las que el estado admite y sus scopes cubren. */
export function accionesDisponibles(
  vinculo: Pick<VinculoDto, 'estado' | 'invitacionAceptadaEn'>,
  scopes: ReadonlySet<string>,
): DefinicionAccion[] {
  return ACCIONES_VINCULO.filter((d) => scopes.has(d.scope) && accionPermitidaPorEstado(d.accion, vinculo));
}
