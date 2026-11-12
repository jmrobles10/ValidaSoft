import type { VigenciaDto } from '@validasoft/api-client';
import { formatearDiaCivil, type Estado } from '@validasoft/ui';
import type { TFunction } from 'i18next';

/** Estado de la vigencia de una versión para una empresa (TD-6), calculado por la API con la fecha de hoy. */
export type EstadoVigencia = 'Vigente' | 'PorVencer' | 'EnGracia' | 'Vencida' | 'SinVencimiento';

const ESTADOS: readonly EstadoVigencia[] = ['Vigente', 'PorVencer', 'EnGracia', 'Vencida', 'SinVencimiento'];

/**
 * Estado del `ChipEstado` para cada vigencia: conserva el ícono y el tono del estado del design system y cambia el
 * texto. «En periodo de gracia» toma el de «Con pendientes» (advertencia): ya venció pero aún se acepta.
 */
export const ESTADO_DEL_CHIP: Readonly<Record<EstadoVigencia, Estado>> = {
  Vigente: 'Vigente',
  PorVencer: 'PorVencer',
  EnGracia: 'ConPendientes',
  Vencida: 'Vencida',
  SinVencimiento: 'NoAplica',
};

/** Un estado que la API agregue después no se muestra (mejor nada que un texto equivocado). */
export function estadoDeVigencia(vigencia: Pick<VigenciaDto, 'estado'> | null | undefined): EstadoVigencia | null {
  const estado = vigencia?.estado;
  return ESTADOS.find((e) => e === estado) ?? null;
}

/** Hay que hacer algo: subir una versión nueva antes de que venza, o porque ya venció. */
export function requiereAtencion(vigencia: Pick<VigenciaDto, 'estado'> | null | undefined): boolean {
  const estado = estadoDeVigencia(vigencia);
  return estado === 'PorVencer' || estado === 'EnGracia' || estado === 'Vencida';
}

/** «Vence el 28/09/2026» o «Venció el …»; `null` si no hay fecha que decir (sin vencimiento o periodo sin calcular). */
export function textoVigencia(vigencia: VigenciaDto, t: TFunction): string | null {
  const estado = estadoDeVigencia(vigencia);
  if (!estado || estado === 'SinVencimiento' || !vigencia.vigenteHasta) return null;
  const fecha = formatearDiaCivil(vigencia.vigenteHasta);
  return estado === 'EnGracia' || estado === 'Vencida' ? t('vigencia.vencioEl', { fecha }) : t('vigencia.venceEl', { fecha });
}

export interface AvisoVigencia {
  severidad: 'warning' | 'error';
  texto: string;
}

/** Qué hacer cuando está por vencer, en gracia o vencida; `null` si no hace falta nada. */
export function avisoDeVigencia(vigencia: VigenciaDto, t: TFunction): AvisoVigencia | null {
  switch (estadoDeVigencia(vigencia)) {
    case 'PorVencer':
      return {
        severidad: 'warning',
        texto: vigencia.vigenteHasta ? t('vigencia.avisos.porVencer', { fecha: formatearDiaCivil(vigencia.vigenteHasta) }) : t('vigencia.avisos.porVencerSinFecha'),
      };
    case 'EnGracia':
      return {
        severidad: 'warning',
        texto: vigencia.finDeGracia ? t('vigencia.avisos.enGracia', { fecha: formatearDiaCivil(vigencia.finDeGracia) }) : t('vigencia.avisos.enGraciaSinFecha'),
      };
    case 'Vencida':
      return { severidad: 'error', texto: t('vigencia.avisos.vencida') };
    default:
      return null;
  }
}
