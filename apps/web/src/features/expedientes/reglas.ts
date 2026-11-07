import {
  SCOPES,
  type AreaDeRequisitoDto,
  type AreaExpedienteDto,
  type EstadoAreaExpediente,
  type EstadoExpediente,
  type ExpedienteDto,
  type MotivoTipificadoDto,
  type RequisitoExpedienteDto,
  type VeredictoEmitidoDto,
} from '@validasoft/api-client';
import type { Estado } from '@validasoft/ui';
import { bloqueoDecisionArea, documentosPorDecidir, type DocumentoPorDecidir } from './decisionArea';
import { bloqueoRequisito, puedeEximir, versionVigente } from './requisitos';

/**
 * Reglas de la UI del expediente: replican las guardas del agregado `Expediente` (FL-3, FL-9, FL-11, FL-13, FL-14)
 * para no ofrecer acciones que la API rechazaría. La API decide siempre; esto solo oculta o explica.
 */

export const ESTADOS_ABIERTOS: readonly EstadoExpediente[] = ['Abierto', 'EnRevision', 'Observado'];

/** Máximo de caracteres de observaciones, justificaciones y respuestas (`LongitudMaximaTexto` del dominio). */
export const MAXIMO_TEXTO_EXPEDIENTE = 4000;

export interface ContextoRevisor {
  scopes: ReadonlySet<string>;
  /** Usuario de la sesión. */
  usuarioId: string | null;
  /** ¿La persona es miembro activo del área? (lista de miembros de `GET /areas`; `null` si no se pudo saber). */
  esMiembro: boolean | null;
  /** El vínculo está bloqueado: el expediente no recibe decisiones (FL-11). */
  vinculoBloqueado?: boolean;
}

/** Por qué la persona no puede emitir veredicto en el área; `null` = puede. Cada valor tiene su texto en i18n. */
export type BloqueoVeredicto =
  | 'sin-permiso'
  | 'expediente-cerrado'
  | 'expediente-observado'
  | 'vinculo-bloqueado'
  | 'area-esperando'
  | 'area-automatica'
  | 'area-cerrada'
  | 'no-es-miembro'
  | 'ya-voto';

export function bloqueoVeredicto(expediente: Pick<ExpedienteDto, 'estado'>, area: AreaExpedienteDto, contexto: ContextoRevisor): BloqueoVeredicto | null {
  if (!contexto.scopes.has(SCOPES.expedientesRevisar)) return 'sin-permiso';
  if (expediente.estado === 'Observado') return 'expediente-observado';
  if (expediente.estado !== 'EnRevision') return 'expediente-cerrado';
  if (contexto.vinculoBloqueado) return 'vinculo-bloqueado';
  if (area.automatica) return 'area-automatica';
  if (area.estado === 'Esperando') return 'area-esperando';
  if (area.estado !== 'Pendiente') return 'area-cerrada';
  if (contexto.esMiembro === false) return 'no-es-miembro';
  if (contexto.usuarioId && votoVigenteDe(area, contexto.usuarioId)) return 'ya-voto';
  return null;
}

/** El veredicto vigente que la persona ya emitió en esta ronda (cuenta para `aprobador-repetido`). */
export function votoVigenteDe(area: AreaExpedienteDto, usuarioId: string): VeredictoEmitidoDto | undefined {
  return area.veredictos.find((v) => v.vigente && v.origen === 'Humano' && v.usuario === usuarioId);
}

/** Rechazo terminal: área con permiso de rechazo, miembro, expediente en revisión o devuelto (FL-14). */
export function puedeRechazar(expediente: Pick<ExpedienteDto, 'estado'>, area: AreaExpedienteDto, contexto: ContextoRevisor): boolean {
  return (
    contexto.scopes.has(SCOPES.expedientesRevisar) &&
    area.puedeRechazarExpediente &&
    !area.automatica &&
    area.estado !== 'Esperando' &&
    (expediente.estado === 'EnRevision' || expediente.estado === 'Observado') &&
    contexto.esMiembro !== false &&
    !contexto.vinculoBloqueado
  );
}

/** Aprobar en nombre del área: solo con el expediente en revisión y el área pendiente. */
export function puedeAprobarComoAdministrador(expediente: Pick<ExpedienteDto, 'estado'>, area: AreaExpedienteDto, contexto: ContextoRevisor): boolean {
  return (
    contexto.scopes.has(SCOPES.expedientesAdministrar) &&
    expediente.estado === 'EnRevision' &&
    area.estado === 'Pendiente' &&
    !contexto.vinculoBloqueado
  );
}

/** Anular un veredicto vigente del área (en revisión o devuelto). Anular la última observación reanuda la revisión (FL-13). */
export function puedeAnularVeredicto(expediente: Pick<ExpedienteDto, 'estado'>, area: AreaExpedienteDto, scopes: ReadonlySet<string>): boolean {
  return (
    scopes.has(SCOPES.expedientesAdministrar) &&
    (expediente.estado === 'EnRevision' || expediente.estado === 'Observado') &&
    area.veredictos.some((v) => v.vigente)
  );
}

export type AccionExpediente = 'reenviar' | 'anular';

/** Acciones de administración sobre el expediente completo. */
export function accionesDeExpediente(
  expediente: Pick<ExpedienteDto, 'estado'>,
  scopes: ReadonlySet<string>,
  vinculoBloqueado = false,
): AccionExpediente[] {
  if (!scopes.has(SCOPES.expedientesAdministrar)) return [];
  const acciones: AccionExpediente[] = [];
  if (expediente.estado === 'Observado' && !vinculoBloqueado) acciones.push('reenviar');
  if (ESTADOS_ABIERTOS.includes(expediente.estado)) acciones.push('anular');
  return acciones;
}

/** ¿Se puede abrir el expediente a mano desde el vínculo? (sin uno abierto y con el vínculo en `Invitado`). */
export function puedeAbrirExpediente(
  vinculo: { estado: string; expedienteAbiertoId?: string | null },
  scopes: ReadonlySet<string>,
): boolean {
  return scopes.has(SCOPES.expedientesAdministrar) && !vinculo.expedienteAbiertoId && vinculo.estado === 'Invitado';
}

/** Diálogo abierto en el detalle: cada decisión sobre un área, un documento (requisito) o el expediente completo. */
export type DialogoExpediente =
  | { tipo: 'veredicto' | 'rechazar' | 'aprobarComoAdministrador' | 'anularVeredicto'; area: AreaExpedienteDto }
  | { tipo: 'veredictoRequisito'; requisito: RequisitoExpedienteDto; areaRequisito: AreaDeRequisitoDto; nombreArea: string }
  | { tipo: 'eximir'; requisito: RequisitoExpedienteDto }
  // Decisión del área completa: «Aprobar área» con los documentos que vio, u «Observar» marcando documentos.
  | { tipo: 'aprobarArea'; area: AreaExpedienteDto; documentos: readonly DocumentoPorDecidir[] }
  | { tipo: 'observarArea'; area: AreaExpedienteDto }
  | { tipo: 'reenviar' | 'anular' };

/** Diálogos de decisión de área completa (tienen su propio componente, `DialogoDecisionArea`). */
export type DialogoDeArea = Extract<DialogoExpediente, { tipo: 'aprobarArea' | 'observarArea' }>;

export function esDialogoDeArea(dialogo: DialogoExpediente): dialogo is DialogoDeArea {
  return dialogo.tipo === 'aprobarArea' || dialogo.tipo === 'observarArea';
}

/** ¿La decisión del diálogo sigue aplicando al expediente vigente? (tras recargar puede haber cambiado). */
export function dialogoAplica(
  dialogo: DialogoExpediente,
  expediente: ExpedienteDto,
  contexto: (area: AreaExpedienteDto | null) => ContextoRevisor,
): boolean {
  if (dialogo.tipo === 'veredictoRequisito' || dialogo.tipo === 'eximir') {
    const requisito = expediente.requisitos?.find((r) => r.id === dialogo.requisito.id);
    if (!requisito) return false;
    if (dialogo.tipo === 'eximir') return puedeEximir(expediente, requisito, contexto(null).scopes);
    // Si llegó otra versión, la decisión era sobre la anterior: deja de aplicar (RQ-4, `version-superada`).
    if (versionVigente(expediente, requisito.id) !== dialogo.requisito.versionActual?.n) return false;
    const areaRequisito = requisito.areas.find((a) => a.areaId === dialogo.areaRequisito.areaId);
    const area = expediente.etapas.flatMap((e) => e.areas).find((a) => a.areaId === dialogo.areaRequisito.areaId) ?? null;
    return areaRequisito ? bloqueoRequisito(expediente, requisito, areaRequisito, contexto(area)) === null : false;
  }
  if (!('area' in dialogo)) {
    const ctx = contexto(null);
    return accionesDeExpediente(expediente, ctx.scopes, ctx.vinculoBloqueado).includes(dialogo.tipo);
  }
  const area = expediente.etapas.flatMap((e) => e.areas).find((a) => a.areaId === dialogo.area.areaId);
  if (!area) return false;
  const ctx = contexto(area);
  switch (dialogo.tipo) {
    case 'veredicto':
      return bloqueoVeredicto(expediente, area, ctx) === null;
    case 'rechazar':
      return puedeRechazar(expediente, area, ctx);
    case 'aprobarComoAdministrador':
      return puedeAprobarComoAdministrador(expediente, area, ctx);
    case 'anularVeredicto':
      return puedeAnularVeredicto(expediente, area, ctx.scopes);
    case 'observarArea':
      return bloqueoDecisionArea(expediente, area, ctx) === null;
    case 'aprobarArea': {
      // Todo o nada: si cambió alguna versión o lo que queda por decidir, la aprobación vista ya no es la vigente.
      const vigentes = documentosPorDecidir(expediente, area, ctx.usuarioId);
      const iguales =
        vigentes.length === dialogo.documentos.length &&
        vigentes.every((d) => dialogo.documentos.some((x) => x.requisitoId === d.requisitoId && x.versionVigente === d.versionVigente));
      return iguales && bloqueoDecisionArea(expediente, area, ctx) === null;
    }
  }
}

export type Decision = 'Observacion' | 'Rechazo';

/** Motivos activos que sirven para la decisión (`aplicaA` igual o `Ambos`), los propios del tenant al final. */
export function motivosPara(motivos: readonly MotivoTipificadoDto[], decision: Decision): MotivoTipificadoDto[] {
  return motivos
    .filter((m) => m.activo && (m.aplicaA === decision || m.aplicaA === 'Ambos'))
    .sort((a, b) => (a.base === b.base ? 0 : a.base ? -1 : 1) || a.nombre.localeCompare(b.nombre, 'es'));
}

/** Los veredictos que cuentan ahora (ni superados por un reenvío ni anulados). */
export function veredictosVigentes(area: AreaExpedienteDto): VeredictoEmitidoDto[] {
  return area.veredictos.filter((v) => v.vigente);
}

/** Estado del área → estado canónico del design system; `Esperando` no tiene uno (se pinta aparte). */
export function estadoCanonicoDeArea(estado: EstadoAreaExpediente): Estado | null {
  switch (estado) {
    case 'Pendiente':
      return 'Pendiente';
    case 'Aprobada':
      return 'Aprobada';
    case 'Observada':
      return 'Observado';
    case 'Esperando':
      return null;
  }
}

/** Áreas de la etapa activa que observaron: son las que vuelven a revisar al reenviar (FL-3). */
export function areasQueVuelvenARevisar(expediente: Pick<ExpedienteDto, 'etapas' | 'etapaActual'>): AreaExpedienteDto[] {
  const etapa = expediente.etapas.find((e) => e.orden === expediente.etapaActual);
  return etapa?.areas.filter((a) => a.estado === 'Observada') ?? [];
}

/** Áreas que esperan por la observación de otra (pendientes con el expediente devuelto). */
export function areasFrenadas(expediente: Pick<ExpedienteDto, 'estado' | 'etapas' | 'etapaActual'>): AreaExpedienteDto[] {
  if (expediente.estado !== 'Observado') return [];
  const etapa = expediente.etapas.find((e) => e.orden === expediente.etapaActual);
  return etapa?.areas.filter((a) => a.estado === 'Pendiente') ?? [];
}
