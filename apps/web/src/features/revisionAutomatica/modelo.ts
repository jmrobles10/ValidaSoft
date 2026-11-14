import type { ExcepcionDeRevisionDto, MotivoDeRevisionDto, RevisionAutomaticaDto } from '@validasoft/api-client';
import { haceCuanto, LOCALE } from '@validasoft/ui';
import type { TFunction } from 'i18next';

/** Niveles de la revisión automática de una empresa (ADR-0012): el sistema solo informa, sugiere o aprueba por sí solo. */
export type NivelDeRevision = 'Manual' | 'Asistido' | 'Automatico';
export const NIVELES: readonly NivelDeRevision[] = ['Manual', 'Asistido', 'Automatico'];

export function esNivel(valor: string): valor is NivelDeRevision {
  return (NIVELES as readonly string[]).includes(valor);
}

/** Códigos estables de los motivos por los que el sistema no aprueba solo (`MotivosDeRevision` del dominio). */
export const CODIGOS_DE_MOTIVO = [
  'tipo-no-autorizado',
  'area-no-autorizada',
  'area-con-varias-aprobaciones',
  'sin-analisis-ia',
  'cruce-pendiente',
  'confianza-baja',
  'ilegible',
  'alertas-de-manipulacion',
  'tipo-detectado-distinto',
  'campo-critico-cambiado',
  'discrepancia-critica-ficha',
  'discrepancias-con-la-ficha',
  'version-reenviada',
  'sin-presupuesto',
] as const;
export type CodigoDeMotivo = (typeof CODIGOS_DE_MOTIVO)[number];

/** Lo que en Automático manda siempre un documento a una persona (el alcance y el tope se explican aparte). */
export const MOTIVOS_QUE_LLEVAN_A_UNA_PERSONA: readonly CodigoDeMotivo[] = [
  'confianza-baja',
  'sin-analisis-ia',
  'ilegible',
  'alertas-de-manipulacion',
  'tipo-detectado-distinto',
  'campo-critico-cambiado',
  'discrepancia-critica-ficha',
  'discrepancias-con-la-ficha',
  'version-reenviada',
];

export function esCodigoDeMotivo(codigo: string): codigo is CodigoDeMotivo {
  return (CODIGOS_DE_MOTIVO as readonly string[]).includes(codigo);
}

/** Texto corto (para un chip) de un motivo; un código que esta versión no conoce usa el texto que manda el servidor. */
export function etiquetaDeMotivo(motivo: Pick<MotivoDeRevisionDto, 'codigo' | 'texto'>, t: TFunction): string {
  return esCodigoDeMotivo(motivo.codigo) ? t(`revisionAutomatica.motivos.${motivo.codigo}`) : motivo.texto.trim() || motivo.codigo;
}

/** El contrato admite los decimales de .NET como número o texto. */
export function numeroDe(valor: number | string | null | undefined): number | null {
  if (valor === null || valor === undefined || valor === '') return null;
  const numero = typeof valor === 'number' ? valor : Number(valor);
  return Number.isFinite(numero) ? numero : null;
}

const PORCENTAJE = new Intl.NumberFormat(LOCALE, { style: 'percent', maximumFractionDigits: 0 });

/** `0.97` → «97 %» (como el chip «Confianza 97 %» del análisis); sin dato, `null`. */
export function porcentajeDeConfianza(valor: number | string | null | undefined): string | null {
  const numero = numeroDe(valor);
  return numero === null ? null : PORCENTAJE.format(Math.min(1, Math.max(0, numero)));
}

export type TipoDeExcepcion = 'Excepcion' | 'Muestreo';
export type EstadoDeExcepciones = 'Pendientes' | 'Resueltas';

export function tipoDe(fila: Pick<ExcepcionDeRevisionDto, 'tipo'>): TipoDeExcepcion {
  return fila.tipo === 'Muestreo' ? 'Muestreo' : 'Excepcion';
}

/** `tipo` vacío = todas. */
export function filtrarPorTipo<T extends Pick<ExcepcionDeRevisionDto, 'tipo'>>(filas: readonly T[], tipo: TipoDeExcepcion | ''): T[] {
  return tipo === '' ? [...filas] : filas.filter((f) => tipoDe(f) === tipo);
}

/** Cuántas filas devuelve la API como máximo (`RevisionAutomaticaEndpoints.MaximoFilas`). */
export const MAXIMO_FILAS = 200;
/** Hasta aquí la lista se dibuja entera; con más filas solo se dibujan las visibles. */
export const FILAS_SIN_VIRTUALIZAR = 50;

export function debeVirtualizar(cantidad: number): boolean {
  return cantidad > FILAS_SIN_VIRTUALIZAR;
}

/** El contador de pendientes de la pestaña: «200+» cuando la API llegó a su máximo y puede haber más. */
export function textoDeConteo(cantidad: number): string {
  return cantidad >= MAXIMO_FILAS ? `${String(MAXIMO_FILAS)}+` : String(cantidad);
}

/** Clave estable de una fila: un documento puede esperar a una persona en varias áreas y versiones. */
export function claveDeFila(fila: Pick<ExcepcionDeRevisionDto, 'expedienteId' | 'requisitoId' | 'areaId' | 'version'>): string {
  return `${fila.expedienteId}|${fila.requisitoId}|${fila.areaId}|${String(fila.version)}`;
}

/** «hace 3 días»: cuánto lleva esperando la fila. */
export function haceCuantoEspera(desde: string, ahora: number): string {
  return haceCuanto(desde, ahora);
}

/** Estado de la fila de un muestreo ya resuelto: `Confirmada` o `Revertida` (otro valor del servidor se ignora). */
export function resolucionDe(valor: string | null | undefined): 'Confirmada' | 'Revertida' | null {
  return valor === 'Confirmada' || valor === 'Revertida' ? valor : null;
}

/** Qué se ve de lo que la revisión automática hizo con un documento en un área. */
export type VistaDeRevision =
  | { tipo: 'sugerida'; sugerencia: 'Aprobar' | 'Revisar'; motivos: readonly MotivoDeRevisionDto[] }
  | { tipo: 'aprobada'; confianza: string | null; muestreo: boolean; pendiente: boolean; resolucion: 'Confirmada' | 'Revertida' | null }
  | { tipo: 'excepcion'; abierta: boolean; motivos: readonly MotivoDeRevisionDto[] };

/**
 * Resultado de la revisión automática de un documento en un área, listo para mostrar. `SinAlcance` (el tipo o el área no están
 * autorizados: se revisa como siempre) y lo que esta versión no conozca no dicen nada: sin ruido en la tarjeta.
 */
export function vistaDeRevision(revision: Pick<RevisionAutomaticaDto, 'resultado' | 'sugerencia' | 'motivos' | 'confianza' | 'pendienteDeAtencion' | 'resolucion'>): VistaDeRevision | null {
  switch (revision.resultado) {
    case 'Sugerida':
      return { tipo: 'sugerida', sugerencia: revision.sugerencia === 'Revisar' ? 'Revisar' : 'Aprobar', motivos: revision.motivos };
    case 'AprobadaAutomaticamente':
      return { tipo: 'aprobada', confianza: porcentajeDeConfianza(revision.confianza), muestreo: false, pendiente: false, resolucion: null };
    case 'AprobadaConMuestreo':
      return {
        tipo: 'aprobada',
        confianza: porcentajeDeConfianza(revision.confianza),
        muestreo: true,
        pendiente: revision.pendienteDeAtencion,
        resolucion: resolucionDe(revision.resolucion),
      };
    case 'Excepcion':
      return { tipo: 'excepcion', abierta: revision.pendienteDeAtencion, motivos: revision.motivos };
    default:
      return null;
  }
}

/** Lo que una persona puede hacer con una aprobación por muestreo pendiente. */
export interface AccionesDeMuestreo {
  confirmar: boolean;
  revertir: boolean;
}

/**
 * Confirmar pide `expedientes:revisar` y ser miembro del área; revertir, además, que el expediente siga en revisión (si ya se
 * cerró, se confirma o se desaprueba el expediente). `esMiembro` `null` = no se sabe: la API decide.
 */
export function accionesDeMuestreo(entrada: { pendiente: boolean; estadoExpediente: string; puedeRevisar: boolean; esMiembro: boolean | null }): AccionesDeMuestreo {
  const puede = entrada.pendiente && entrada.puedeRevisar && entrada.esMiembro !== false;
  return { confirmar: puede, revertir: puede && entrada.estadoExpediente === 'EnRevision' };
}

/** `?tipo=muestreo|excepcion` de la URL; cualquier otra cosa es «todas». */
export function tipoDeUrl(valor: string | null): TipoDeExcepcion | '' {
  return valor === 'muestreo' ? 'Muestreo' : valor === 'excepcion' ? 'Excepcion' : '';
}

/** Lo contrario de `tipoDeUrl`: cómo se escribe el tipo en la URL. */
export function tipoParaUrl(tipo: TipoDeExcepcion): string {
  return tipo === 'Muestreo' ? 'muestreo' : 'excepcion';
}

/** Lo que identifica en la API una aprobación por muestreo. */
export interface DestinoDeMuestreo {
  expedienteId: string;
  requisitoId: string;
  areaId: string;
  documento: string;
  area: string;
  folio: string;
  tercero: string;
}
