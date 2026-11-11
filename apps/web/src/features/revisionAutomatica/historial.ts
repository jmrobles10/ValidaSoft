import type { TFunction } from 'i18next';
import { campo, texto } from '../vinculos/historial';
import { etiquetaDeMotivo, porcentajeDeConfianza } from './modelo';

/** Nombres que el historial del expediente conoce: el del área y el del documento a partir de sus ids. */
export interface NombresDelHistorial {
  area: (id: unknown) => string;
  requisito: (id: unknown) => string;
}

/** Lo que `eventoExpedienteATimeline` toma de un evento de la revisión automática para su línea de tiempo. */
export interface FraseDeRevisionAutomatica {
  titulo: string;
  detalle: string | null;
  motivo: string | null;
}

const RESULTADOS = ['Sugerida', 'AprobadaAutomaticamente', 'AprobadaConMuestreo', 'Excepcion', 'SinAlcance'] as const;
const SUGERENCIAS = ['Aprobar', 'Revisar'] as const;
const DECISIONES = ['Confirmada', 'Revertida'] as const;

/** Un enum que Marten guarda como texto (lo normal) o, en datos viejos, como su posición. */
function enumDe<T extends string>(valor: unknown, orden: readonly T[]): T | null {
  if (typeof valor === 'number') return orden[valor] ?? null;
  return typeof valor === 'string' ? (orden.find((o) => o.toLowerCase() === valor.toLowerCase()) ?? null) : null;
}

function motivosDe(valor: unknown, t: TFunction): string | null {
  const lista = Array.isArray(valor) ? (valor as unknown[]) : [];
  const etiquetas = lista.flatMap((m) => {
    if (!m || typeof m !== 'object') return [];
    const datos = m as Record<string, unknown>;
    const codigo = texto(campo(datos, 'codigo'));
    return codigo ? [etiquetaDeMotivo({ codigo, texto: texto(campo(datos, 'texto')) ?? '' }, t)] : [];
  });
  return etiquetas.length > 0 ? t('revisionAutomatica.historial.motivos', { motivos: etiquetas.join(' · ') }) : null;
}

/**
 * Frases del historial del expediente para lo que hizo la revisión automática (`revision_automatica_registrada`: sugirió,
 * aprobó, aprobó con muestreo, mandó a una persona o quedó fuera de su alcance) y para lo que resolvió una persona del
 * muestreo (`revision_automatica_resuelta`: confirmó o revirtió).
 */
export function fraseDeRevisionAutomatica(
  tipo: 'revision_automatica_registrada' | 'revision_automatica_resuelta',
  datos: Record<string, unknown>,
  nombres: NombresDelHistorial,
  t: TFunction,
): FraseDeRevisionAutomatica {
  const documento = nombres.requisito(campo(datos, 'requisitoId'));
  const area = nombres.area(campo(datos, 'areaId'));

  if (tipo === 'revision_automatica_resuelta') {
    const revirtio = enumDe(campo(datos, 'decision'), DECISIONES) === 'Revertida';
    return {
      titulo: t(revirtio ? 'revisionAutomatica.historial.resueltaRevertida' : 'revisionAutomatica.historial.resueltaConfirmada', { documento, area }),
      detalle: null,
      motivo: texto(campo(datos, 'justificacion')),
    };
  }

  const porcentaje = porcentajeDeConfianza(campo(datos, 'confianza') as number | string | null | undefined) ?? '—';
  const motivos = motivosDe(campo(datos, 'motivos'), t);
  switch (enumDe(campo(datos, 'resultado'), RESULTADOS)) {
    case 'Sugerida': {
      const sugerencia = enumDe(campo(datos, 'sugerencia'), SUGERENCIAS) === 'Revisar' ? 'sugerenciaRevisar' : 'sugerenciaAprobar';
      return {
        titulo: t('revisionAutomatica.historial.registradaSugerencia', { sugerencia: t(`revisionAutomatica.historial.${sugerencia}`), documento, area }),
        detalle: sugerencia === 'sugerenciaRevisar' ? motivos : null,
        motivo: null,
      };
    }
    case 'AprobadaAutomaticamente':
      return { titulo: t('revisionAutomatica.historial.registradaAprobo', { documento, area, porcentaje }), detalle: null, motivo: null };
    case 'AprobadaConMuestreo':
      return { titulo: t('revisionAutomatica.historial.registradaAproboConMuestreo', { documento, area, porcentaje }), detalle: null, motivo: null };
    case 'Excepcion':
      return { titulo: t('revisionAutomatica.historial.registradaExcepcion', { documento, area }), detalle: motivos, motivo: null };
    case 'SinAlcance':
      return { titulo: t('revisionAutomatica.historial.registradaSinAlcance', { documento, area }), detalle: motivos, motivo: null };
    default:
      return { titulo: t('expedientes.historial.eventos.revision_automatica_registrada'), detalle: null, motivo: null };
  }
}
