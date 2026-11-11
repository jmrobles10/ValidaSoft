import type { ResolverSolicitudHabeasDto, SolicitudHabeasDto } from '@validasoft/api-client';
import type { Estado } from '@validasoft/ui';

/** Qué solicitudes se listan (el servidor las filtra): las abiertas primero, la que vence antes arriba. */
export const FILTROS_DE_SOLICITUDES = ['abiertas', 'vencidas', 'resueltas', 'todas'] as const;
export type FiltroDeSolicitudes = (typeof FILTROS_DE_SOLICITUDES)[number];

export const TAMANO_PAGINA_SOLICITUDES = 50;

/** Largo de la respuesta que recibe el titular: lo mismo que valida el servidor. */
export const MINIMO_RESPUESTA = 10;
export const MAXIMO_RESPUESTA = 4000;
export const MAXIMO_BASE_LEGAL = 500;

export type DecisionDeSolicitud = 'Atender' | 'Rechazar';

export type EstadoDePlazo = 'EnPlazo' | 'PorVencer' | 'Vencida' | 'Resuelta';

export function estadoDePlazo(s: Pick<SolicitudHabeasDto, 'plazo'>): EstadoDePlazo {
  return s.plazo === 'PorVencer' || s.plazo === 'Vencida' || s.plazo === 'Resuelta' ? s.plazo : 'EnPlazo';
}

/** El chip de la solicitud: el resultado si ya se respondió; si no, cómo va el plazo. */
export function chipDeSolicitud(s: SolicitudHabeasDto): { estado: Estado; clave: 'enPlazo' | 'porVencer' | 'vencida' | 'atendida' | 'parcial' | 'rechazada' } {
  if (s.estado === 'Resuelta') {
    if (s.resultado === 'Rechazada') return { estado: 'Rechazado', clave: 'rechazada' };
    return { estado: 'Aprobado', clave: s.resultado === 'Parcial' ? 'parcial' : 'atendida' };
  }
  const plazo = estadoDePlazo(s);
  if (plazo === 'Vencida') return { estado: 'Vencido', clave: 'vencida' };
  if (plazo === 'PorVencer') return { estado: 'PorVencer', clave: 'porVencer' };
  return { estado: 'EnRevision', clave: 'enPlazo' };
}

export interface BorradorDeRespuesta {
  decision: DecisionDeSolicitud;
  respuesta: string;
  baseLegal: string;
}

export const BORRADOR_DE_RESPUESTA_VACIO: BorradorDeRespuesta = { decision: 'Atender', respuesta: '', baseLegal: '' };

export type ErrorDeRespuesta = 'respuesta-corta' | 'respuesta-larga' | 'base-legal-larga';

/** Lo que falta o sobra antes de responder (el primero va en el aviso del botón). */
export function validarRespuesta(b: BorradorDeRespuesta): ErrorDeRespuesta[] {
  const errores: ErrorDeRespuesta[] = [];
  const respuesta = b.respuesta.trim().length;
  if (respuesta < MINIMO_RESPUESTA) errores.push('respuesta-corta');
  if (respuesta > MAXIMO_RESPUESTA) errores.push('respuesta-larga');
  if (b.baseLegal.trim().length > MAXIMO_BASE_LEGAL) errores.push('base-legal-larga');
  return errores;
}

/** La base legal solo se cita al rechazar: al atender no viaja. */
export function cuerpoDeRespuesta(b: BorradorDeRespuesta): ResolverSolicitudHabeasDto {
  return {
    decision: b.decision,
    respuesta: b.respuesta.trim(),
    baseLegal: b.decision === 'Rechazar' && b.baseLegal.trim() ? b.baseLegal.trim() : null,
  };
}

/** Sugerencia al rechazar: lo que dice la ley sobre la conservación por deber legal (editable). */
export const BASE_LEGAL_SUGERIDA = 'Ley 1581 de 2012, art. 15 y 16, y deber legal de conservación';

export function estaAbierta(s: Pick<SolicitudHabeasDto, 'estado'>): boolean {
  return s.estado === 'Recibida';
}

/** Para el título de la tarjeta: quién pidió y qué, sin repetir el radicado. */
export function esSupresionDeTodo(s: Pick<SolicitudHabeasDto, 'tipo' | 'alcance'>): boolean {
  return s.tipo === 'Supresion' && (s.alcance === null || s.alcance === 'Todo');
}

/** Lo que cuenta el aviso de arriba: solo abiertas, por vencer y vencidas (las resueltas no urgen). */
export function hayUrgentes(r: { porVencer: number; vencidas: number } | undefined): boolean {
  return r !== undefined && (r.porVencer > 0 || r.vencidas > 0);
}

/** Los derechos que puede pedir el titular y lo que puede abarcar la supresión: el servidor los devuelve como texto. */
export const TIPOS_DE_SOLICITUD = ['Consulta', 'Correccion', 'Supresion'] as const;
export type TipoDeSolicitud = (typeof TIPOS_DE_SOLICITUD)[number];
export const ALCANCES_DE_SUPRESION = ['Todo', 'Documentos', 'Datos'] as const;
export type AlcanceDeSupresion = (typeof ALCANCES_DE_SUPRESION)[number];
export const ACCIONES_DE_SUPRESION = ['Suprimir', 'Retener', 'Anonimizar'] as const;
export type AccionDeSupresion = (typeof ACCIONES_DE_SUPRESION)[number];

export function comoTipo(valor: string): TipoDeSolicitud | null {
  return (TIPOS_DE_SOLICITUD as readonly string[]).includes(valor) ? (valor as TipoDeSolicitud) : null;
}

export function comoAlcance(valor: string | null | undefined): AlcanceDeSupresion | null {
  return valor !== null && valor !== undefined && (ALCANCES_DE_SUPRESION as readonly string[]).includes(valor) ? (valor as AlcanceDeSupresion) : null;
}

export function comoAccion(valor: string): AccionDeSupresion | null {
  return (ACCIONES_DE_SUPRESION as readonly string[]).includes(valor) ? (valor as AccionDeSupresion) : null;
}
