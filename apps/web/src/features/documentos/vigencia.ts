import type { VigenciaDto } from '@validasoft/api-client';
import type { Estado } from '@validasoft/ui';
import type { TFunction } from 'i18next';
import { formatearFechaDeclarada } from './formatoDatos';

/** Estados de la vigencia de una versión para un vínculo (TD-6; calculados hoy, en UTC). */
export const ESTADOS_VIGENCIA = ['Vigente', 'PorVencer', 'EnGracia', 'Vencida', 'SinVencimiento'] as const;
export type EstadoVigencia = (typeof ESTADOS_VIGENCIA)[number];

/** De dónde sale la fecha de vencimiento. */
export const BASES_VIGENCIA = ['FechaExpedicion', 'FechaCarga', 'VencimientoPropio', 'SinVencimiento', 'PeriodoNoCalculado'] as const;
export type BaseVigencia = (typeof BASES_VIGENCIA)[number];

/** El cliente generado tipa `estado` y `base` como texto: se estrechan aquí y lo desconocido se ignora. */
function esUnoDe<T extends string>(valores: readonly T[], valor: string): valor is T {
  return (valores as readonly string[]).includes(valor);
}

export function estadoVigencia(vigencia: Pick<VigenciaDto, 'estado'>): EstadoVigencia | null {
  return esUnoDe(ESTADOS_VIGENCIA, vigencia.estado) ? vigencia.estado : null;
}

export function baseVigencia(vigencia: Pick<VigenciaDto, 'base'>): BaseVigencia | null {
  return esUnoDe(BASES_VIGENCIA, vigencia.base) ? vigencia.base : null;
}

/**
 * Estado canónico del design system que pinta cada estado de vigencia (ícono y tono; el texto va en `etiqueta`).
 * `EnGracia` ya venció pero aún se acepta: advertencia con un ícono distinto al de «Por vencer» para leerse sin color.
 */
export const CHIP_VIGENCIA: Readonly<Record<EstadoVigencia, Estado>> = {
  Vigente: 'Vigente',
  PorVencer: 'PorVencer',
  EnGracia: 'ConPendientes',
  Vencida: 'Vencida',
  SinVencimiento: 'NoAplica',
};

/** Estados que piden un aviso visible a quien revisa. */
export function requiereAviso(vigencia: Pick<VigenciaDto, 'estado'>): boolean {
  const estado = estadoVigencia(vigencia);
  return estado === 'PorVencer' || estado === 'EnGracia' || estado === 'Vencida';
}

/** Severidad del aviso: vencida es error; por vencer y en gracia, advertencia. */
export function severidadAviso(vigencia: Pick<VigenciaDto, 'estado'>): 'warning' | 'error' {
  return estadoVigencia(vigencia) === 'Vencida' ? 'error' : 'warning';
}

/** Las fechas de la vigencia son días calendario (`aaaa-mm-dd`): no se corren por la zona del tenant. */
const fecha = (valor: string) => formatearFechaDeclarada(valor);

/** «Vence el 30/11/2026», «Venció el 30/11/2026» o «Sin fecha de vencimiento». */
export function textoVencimiento(vigencia: VigenciaDto, t: TFunction): string {
  if (!vigencia.vigenteHasta) return t('expedientes.vigencia.sinFecha');
  const estado = estadoVigencia(vigencia);
  return estado === 'EnGracia' || estado === 'Vencida'
    ? t('expedientes.vigencia.vencio', { fecha: fecha(vigencia.vigenteHasta) })
    : t('expedientes.vigencia.vence', { fecha: fecha(vigencia.vigenteHasta) });
}

/** Texto corto del chip: el estado y, si la hay, la fecha que importa (en gracia, hasta cuándo se acepta). */
export function etiquetaVigencia(vigencia: VigenciaDto, t: TFunction): string {
  const estado = estadoVigencia(vigencia);
  if (!estado) return textoVencimiento(vigencia, t);
  const nombre = t(`expedientes.vigencia.estados.${estado}`);
  if (estado === 'EnGracia' && vigencia.finDeGracia) return `${nombre} · ${t('expedientes.vigencia.hasta', { fecha: fecha(vigencia.finDeGracia) })}`;
  if (estado === 'SinVencimiento' || !vigencia.vigenteHasta) return nombre;
  return `${nombre} · ${fecha(vigencia.vigenteHasta)}`;
}

/** Explicación de la fecha (tooltip o texto secundario): su base y los días de gracia. */
export function explicacionVigencia(vigencia: VigenciaDto, t: TFunction): string {
  const base = baseVigencia(vigencia);
  const partes: string[] = [];
  if (base) partes.push(t(`expedientes.vigencia.base.${base}`));
  if (vigencia.diasGracia > 0 && vigencia.vigenteHasta) partes.push(t('expedientes.vigencia.gracia', { count: vigencia.diasGracia }));
  return partes.join(' ');
}

/** Texto del aviso visible (`null` si la vigencia no lo pide). */
export function textoAvisoVigencia(vigencia: VigenciaDto, t: TFunction): string | null {
  const hasta = vigencia.vigenteHasta ? fecha(vigencia.vigenteHasta) : null;
  switch (estadoVigencia(vigencia)) {
    case 'PorVencer':
      return hasta ? t('expedientes.vigencia.avisoPorVencer', { fecha: hasta }) : null;
    case 'EnGracia':
      return hasta && vigencia.finDeGracia
        ? t('expedientes.vigencia.avisoEnGracia', { fecha: hasta, finDeGracia: fecha(vigencia.finDeGracia) })
        : t('expedientes.vigencia.estados.EnGracia');
    case 'Vencida':
      return hasta ? t('expedientes.vigencia.avisoVencida', { fecha: hasta }) : t('expedientes.vigencia.avisoVencidaSinFecha');
    default:
      return null;
  }
}
