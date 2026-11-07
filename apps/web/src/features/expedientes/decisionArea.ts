import { esErrorApi, type AreaDeRequisitoDto, type AreaExpedienteDto, type ExpedienteDto, type RequisitoExpedienteDto } from '@validasoft/api-client';
import { bloqueoVeredicto, type BloqueoVeredicto, type ContextoRevisor } from './reglas';
import { requisitosDe, veredictosVigentesDeRequisito } from './requisitos';

/**
 * Decisión por área «como en el SRM» (opción mixta, `feature/H2-decision-area`): en pantalla cada área toma una sola
 * decisión —«Aprobar área» u «Observar» marcando qué documentos tienen problema— y por dentro se guarda documento por
 * documento (una versión nueva solo reabre ese documento). Va por `POST /expedientes/{id}/areas/{areaId}/veredicto`:
 * - Aprobado: `versiones { requisitoId: versión vigente }` con exactamente los documentos que la persona aún no decidió
 *   (`documentosPorDecidir`); todo o nada (422 `versiones-requeridas`, 409 `version-superada`, 409 `area-ya-se-pronuncio`).
 * - Observado: `requisitos[] { requisitoId, sobreVersion, motivos[], observacion? }`, al menos uno y cada uno con al menos
 *   un motivo (422 `requisitos-requeridos`); una sola devolución y un solo correo al tercero.
 */

/** `ExpedienteDto.etapas[].areas[].documentosPorDecidir[]` (nuevo en el contrato). */
export interface DocumentoPorDecidir {
  requisitoId: string;
  versionVigente: number;
}

export interface ObservacionDeDocumento {
  requisitoId: string;
  sobreVersion: number;
  motivos: string[];
  observacion?: string | null;
}

/** Cuerpo de la decisión del área (amplía `VeredictoDto`). */
export interface DecisionDeArea {
  veredicto: 'Aprobado' | 'Observado';
  versiones?: Record<string, number>;
  requisitos?: ObservacionDeDocumento[];
}

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
 * Lo que la persona aún debe decidir en el área. Usa `documentosPorDecidir` de la API; si no viene (API anterior), lo
 * deriva: documentos del área pendientes, con versión, sin un voto suyo sobre la versión vigente.
 */
export function documentosPorDecidir(expediente: ExpedienteDto, area: AreaExpedienteDto, usuarioId: string | null): DocumentoPorDecidir[] {
  const delServidor = (area as AreaExpedienteDto & { documentosPorDecidir?: DocumentoPorDecidir[] | null }).documentosPorDecidir;
  if (delServidor) return delServidor;
  return documentosDelArea(expediente, area.areaId).flatMap(({ requisito, areaRequisito }) => {
    const version = requisito.versionActual?.n;
    if (version === undefined || areaRequisito.estado !== 'Pendiente' || requisito.estado === 'Eximido') return [];
    const yaVoto = usuarioId && veredictosVigentesDeRequisito(areaRequisito).some((v) => v.usuario === usuarioId && v.sobreVersion === version);
    return yaVoto ? [] : [{ requisitoId: requisito.id, versionVigente: version }];
  });
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
  return documentosPorDecidir(expediente, area, contexto.usuarioId).length === 0 ? 'nada-por-decidir' : null;
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
    case 'version-superada': {
      // `datos.requisitos` llega como extensión del ProblemDetails (IN-12); se aceptan ids o `{ requisitoId }`.
      const datos = error.extensiones.datos;
      const requisitos = idsDe(error.extensiones.requisitos);
      return { tipo: 'version-superada', requisitos: requisitos.length > 0 ? requisitos : idsDe(datos && typeof datos === 'object' && 'requisitos' in datos ? datos.requisitos : null) };
    }
    case 'expediente-observado':
      return { tipo: 'expediente-observado' };
    case 'area-ya-se-pronuncio':
      return { tipo: 'area-ya-se-pronuncio' };
    default:
      return { tipo: 'otro' };
  }
}
