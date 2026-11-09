import {
  esErrorApi,
  type AreaDeRequisitoDto,
  type AreaExpedienteDto,
  type DocumentoPorDecidirDto,
  type ExpedienteDto,
  type ObservacionDeRequisitoDto,
  type RequisitoExpedienteDto,
  type VeredictoDto,
} from '@validasoft/api-client';
import { bloqueoVeredicto, type BloqueoVeredicto, type ContextoRevisor } from './reglas';
import { requisitosDe } from './requisitos';

/**
 * Decisión por área «como en el SRM» (opción mixta, `feature/H2-decision-area`): en pantalla cada área toma una sola
 * decisión —«Aprobar área» u «Observar» marcando qué documentos tienen problema— y por dentro se guarda documento por
 * documento (una versión nueva solo reabre ese documento). Va por `POST /expedientes/{id}/areas/{areaId}/veredicto`:
 * - Aprobado: `versiones { requisitoId: versión vigente }` con exactamente los documentos que la persona aún no decidió
 *   (`documentosPorDecidir`); todo o nada (422 `versiones-requeridas`, 409 `version-superada`, 409 `area-ya-se-pronuncio`).
 * - Observado: `requisitos[] { requisitoId, sobreVersion, motivos[], observacion? }`, al menos uno y cada uno con al menos
 *   un motivo (422 `requisitos-requeridos`); una sola devolución y un solo correo al tercero.
 */

/** Lo que «Aprobar área» aprobaría a quien consulta: `ExpedienteDto.etapas[].areas[].documentosPorDecidir[]`. */
export type DocumentoPorDecidir = DocumentoPorDecidirDto;
export type ObservacionDeDocumento = ObservacionDeRequisitoDto;
/** Cuerpo de la decisión del área (`VeredictoDto` con `versiones` o `requisitos`). */
export type DecisionDeArea = Pick<VeredictoDto, 'veredicto' | 'versiones' | 'requisitos'>;

/** Un documento que revisa el área, con lo que el área lleva decidido sobre él. */
export interface DocumentoDelArea {
  requisito: RequisitoExpedienteDto;
  areaRequisito: AreaDeRequisitoDto;
}

export function documentosDelArea(expediente: Pick<ExpedienteDto, 'requisitos'>, areaId: string): DocumentoDelArea[] {
  return requisitosDe(expediente).flatMap((requisito) => {
    const areaRequisito = requisito.areas.find((a) => a.areaId === areaId);
    return areaRequisito ? [{ requisito, areaRequisito }] : [];
  });
}

/**
 * Lo que la persona aún debe decidir en el área, según la API: vacío si el expediente no está `EnRevision`, si la etapa
 * del área no está activa o si el área es automática.
 */
export function documentosPorDecidir(area: Pick<AreaExpedienteDto, 'documentosPorDecidir'>): DocumentoPorDecidir[] {
  return area.documentosPorDecidir ?? [];
}

/** `versiones` de «Aprobar área»: exactamente los documentos por decidir, con su versión vigente. */
export function versionesParaAprobar(porDecidir: readonly DocumentoPorDecidir[]): Record<string, number> {
  return Object.fromEntries(porDecidir.map((d) => [d.requisitoId, d.versionVigente]));
}

/** Por qué la persona no puede decidir el área; `null` = puede. Con requisitos no aplica `ya-voto` sino `nada-por-decidir`. */
export type BloqueoDecisionArea = Exclude<BloqueoVeredicto, 'ya-voto'> | 'nada-por-decidir';

export function bloqueoDecisionArea(expediente: ExpedienteDto, area: AreaExpedienteDto, contexto: ContextoRevisor): BloqueoDecisionArea | null {
  const bloqueo = bloqueoVeredicto(expediente, area, { ...contexto, usuarioId: null });
  if (bloqueo && bloqueo !== 'ya-voto') return bloqueo;
  return documentosPorDecidir(area).length === 0 ? 'nada-por-decidir' : null;
}

/** Lo marcado en «Observar»: por documento, sus motivos y su nota. */
export type MarcasDeObservacion = ReadonlyMap<string, { motivos: readonly string[]; observacion: string }>;

export type ErrorObservacion = { tipo: 'sin-documentos' } | { tipo: 'sin-motivos'; requisitoId: string };

/** Validación de «Observar», igual que la API: al menos un documento y cada uno con al menos un motivo. */
export function validarObservacion(marcas: MarcasDeObservacion): ErrorObservacion[] {
  if (marcas.size === 0) return [{ tipo: 'sin-documentos' }];
  return [...marcas].filter(([, m]) => m.motivos.length === 0).map(([requisitoId]) => ({ tipo: 'sin-motivos' as const, requisitoId }));
}

/** `requisitos[]` de «Observar», cada uno sobre la versión vigente que se está viendo. */
export function requisitosParaObservar(marcas: MarcasDeObservacion, expediente: Pick<ExpedienteDto, 'requisitos'>): ObservacionDeDocumento[] {
  return [...marcas].flatMap(([requisitoId, m]) => {
    const version = requisitosDe(expediente).find((r) => r.id === requisitoId)?.versionActual?.n;
    if (version === undefined) return [];
    return [{ requisitoId, sobreVersion: version, motivos: [...m.motivos], observacion: m.observacion.trim() || null }];
  });
}

/** Errores de la decisión con tratamiento propio en el diálogo. */
export type FalloDecision =
  | { tipo: 'version-superada'; requisitos: string[] }
  | { tipo: 'expediente-observado' }
  | { tipo: 'area-ya-se-pronuncio' }
  | { tipo: 'area-observada' }
  | { tipo: 'otro' };

function idsDe(valor: unknown): string[] {
  if (!Array.isArray(valor)) return [];
  return valor.flatMap((v: unknown) => {
    if (typeof v === 'string') return [v];
    if (v && typeof v === 'object' && 'requisitoId' in v && typeof v.requisitoId === 'string') return [v.requisitoId];
    return [];
  });
}

export function falloDeDecision(error: unknown): FalloDecision {
  if (!esErrorApi(error)) return { tipo: 'otro' };
  switch (error.codigo) {
    case 'version-superada':
    case 'versiones-requeridas': {
      // `requisitos: [{ requisitoId, versionVigente }]` en el primer nivel del ProblemDetails (también en
      // `versiones-requeridas`: llegó un documento por decidir que no estaba). Se aceptan ids o `{ requisitoId }`.
      const datos = error.extensiones.datos;
      const requisitos = idsDe(error.extensiones.requisitos);
      return { tipo: 'version-superada', requisitos: requisitos.length > 0 ? requisitos : idsDe(datos && typeof datos === 'object' && 'requisitos' in datos ? datos.requisitos : null) };
    }
    case 'expediente-observado':
      return { tipo: 'expediente-observado' };
    case 'area-ya-se-pronuncio':
      return { tipo: 'area-ya-se-pronuncio' };
    case 'area-observada':
      return { tipo: 'area-observada' };
    default:
      return { tipo: 'otro' };
  }
}
