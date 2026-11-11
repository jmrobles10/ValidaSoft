import {
  SCOPES,
  type AreaDeRequisitoDto,
  type ExpedienteDto,
  type HechosDto,
  type RequisitoExpedienteDto,
  type VeredictoEmitidoDto,
} from '@validasoft/api-client';
import { datosDe, esVersionSinArchivo } from '../documentos/formatoDatos';
import type { DocumentoEnVisor } from '../documentos/VisorDocumento';
import type { ContextoRevisor } from './reglas';

/**
 * Reglas de la UI de los requisitos documentales (docs/05 §2.4.2, RQ-1…RQ-10): cada área aprueba u observa
 * documentos concretos sobre una versión. Replican las guardas del agregado para no ofrecer lo que la API rechazaría.
 */

/** Requisitos del expediente (vacío en un flujo sin requisitos: entonces rige el veredicto por área, FL-1). */
export function requisitosDe(expediente: Pick<ExpedienteDto, 'requisitos'>): RequisitoExpedienteDto[] {
  return expediente.requisitos ?? [];
}

export function tieneRequisitos(expediente: Pick<ExpedienteDto, 'requisitos'>): boolean {
  return requisitosDe(expediente).length > 0;
}

/** Fase de carga: el expediente nace `Abierto` mientras falte algún obligatorio y pasa solo a revisión. */
export function enFaseDeCarga(expediente: Pick<ExpedienteDto, 'estado' | 'requisitos'>): boolean {
  return expediente.estado === 'Abierto' && tieneRequisitos(expediente);
}

/** Obligatorios que el tercero aún no carga. */
export function requisitosFaltantes(expediente: Pick<ExpedienteDto, 'requisitos'>): RequisitoExpedienteDto[] {
  return requisitosDe(expediente).filter((r) => r.obligatorio && r.estado === 'Pendiente');
}

/** Veredictos que cuentan: ni superados por un reenvío, ni anulados, ni invalidados por una versión nueva (RQ-8). */
export function veredictosVigentesDeRequisito(area: AreaDeRequisitoDto): VeredictoEmitidoDto[] {
  return area.veredictos.filter((v) => v.vigente && !v.invalidado && !v.anulado);
}

/** Lo que quedó de versiones anteriores: se muestra tachado con «sobre la versión k». */
export function veredictosInvalidados(area: AreaDeRequisitoDto): VeredictoEmitidoDto[] {
  return area.veredictos.filter((v) => v.invalidado);
}

/** Lo que un administrador devolvió a revisión (reapertura de etapa, fase inicial o desaprobación, DS-1): ya no cuenta. */
export function veredictosRevertidos(area: AreaDeRequisitoDto): VeredictoEmitidoDto[] {
  return area.veredictos.filter((v) => v.revertido);
}

/** Por qué la persona no puede decidir este requisito en esta área; `null` = puede. Cada valor tiene su texto. */
export type BloqueoRequisito =
  | 'sin-permiso'
  | 'expediente-en-carga'
  | 'expediente-cerrado'
  | 'expediente-observado'
  | 'vinculo-bloqueado'
  | 'sin-version'
  | 'requisito-cerrado'
  | 'area-esperando'
  | 'area-cerrada'
  | 'no-es-miembro'
  | 'ya-voto';

export function bloqueoRequisito(
  expediente: Pick<ExpedienteDto, 'estado'>,
  requisito: RequisitoExpedienteDto,
  area: AreaDeRequisitoDto,
  contexto: ContextoRevisor,
): BloqueoRequisito | null {
  if (!contexto.scopes.has(SCOPES.expedientesRevisar)) return 'sin-permiso';
  if (expediente.estado === 'Abierto') return 'expediente-en-carga';
  if (expediente.estado === 'Observado') return 'expediente-observado';
  if (expediente.estado !== 'EnRevision') return 'expediente-cerrado';
  if (contexto.vinculoBloqueado) return 'vinculo-bloqueado';
  if (requisito.estado === 'Eximido' || requisito.estado === 'Aprobado') return 'requisito-cerrado';
  if (!requisito.versionActual) return 'sin-version';
  if (area.estado === 'Esperando') return 'area-esperando';
  if (area.estado !== 'Pendiente') return 'area-cerrada';
  if (contexto.esMiembro === false) return 'no-es-miembro';
  if (contexto.usuarioId && votoSobreVersion(area, contexto.usuarioId, requisito.versionActual.n)) return 'ya-voto';
  return null;
}

/** El veredicto que la persona ya emitió sobre esta versión (cuenta para `aprobador-repetido`). */
export function votoSobreVersion(area: AreaDeRequisitoDto, usuarioId: string, version: number): VeredictoEmitidoDto | undefined {
  return veredictosVigentesDeRequisito(area).find((v) => v.origen === 'Humano' && v.usuario === usuarioId && v.sobreVersion === version);
}

/** Eximir (RQ-6): administrador, expediente abierto y requisito ni aprobado ni ya eximido. */
export function puedeEximir(expediente: Pick<ExpedienteDto, 'estado'>, requisito: RequisitoExpedienteDto, scopes: ReadonlySet<string>): boolean {
  return (
    scopes.has(SCOPES.expedientesAdministrar) &&
    ['Abierto', 'EnRevision', 'Observado'].includes(expediente.estado) &&
    requisito.estado !== 'Aprobado' &&
    requisito.estado !== 'Eximido'
  );
}

/** ¿La versión que se está revisando sigue siendo la vigente? Si llegó otra, el diálogo avisa y no deja decidir. */
export function versionVigente(expediente: Pick<ExpedienteDto, 'requisitos'>, requisitoId: string): number | null {
  return requisitosDe(expediente).find((r) => r.id === requisitoId)?.versionActual?.n ?? null;
}

/** Requisitos que un área aún tiene por decidir (los que la bandeja y la tarjeta del área cuentan). */
export function requisitosPorDecidirDeArea(expediente: Pick<ExpedienteDto, 'requisitos'>, areaId: string): RequisitoExpedienteDto[] {
  return requisitosDe(expediente).filter((r) => r.areas.some((a) => a.areaId === areaId && a.estado === 'Pendiente'));
}

/** Ids de personas que aparecen en el expediente, para pedir sus nombres (`GET /usuarios/resumen`). */
export function usuariosDelExpediente(expediente: ExpedienteDto): string[] {
  const ids = new Set<string>();
  const agregar = (id: string | null | undefined) => id && ids.add(id);
  for (const etapa of expediente.etapas) {
    for (const area of etapa.areas) {
      area.miembrosPendientes.forEach(agregar);
      for (const v of area.veredictos) {
        agregar(v.usuario);
        agregar(v.anuladoPor);
      }
    }
  }
  for (const r of requisitosDe(expediente)) {
    agregar(r.exencion?.por);
    for (const a of r.areas) for (const v of a.veredictos) agregar(v.usuario);
  }
  // Quién consultó listas, resolvió coincidencias o registró el estudio (H7); «sistema» no es un usuario.
  for (const v of expediente.verificaciones ?? []) {
    if (v.registradaPor !== 'sistema') agregar(v.registradaPor);
    agregar(v.estudio?.oficial);
    for (const c of v.coincidencias) if (c.resueltaPor !== 'sistema') agregar(c.resueltaPor);
  }
  agregar(expediente.rechazo?.usuario);
  return [...ids].sort();
}

/**
 * Lo que el visor necesita de la versión vigente del requisito (`null` sin versión): sus datos declarados, su vigencia
 * para este vínculo, si trae archivo (una versión de solo formulario no se descarga: 409 `version-sin-archivo`) y, si se
 * conocen, sus hechos con la revisión automática y el tipo canónico esperado.
 */
export function documentoEnVisor(requisito: RequisitoExpedienteDto, hechos?: HechosDto | null): DocumentoEnVisor | null {
  const version = requisito.versionActual;
  if (!version) return null;
  return {
    hechos: hechos ?? null,
    tipoEsperado: { codigo: requisito.tipoDocumento.tipoCanonico, nombre: requisito.tipoDocumento.nombre },
    documentoId: version.documentoId,
    tipo: requisito.tipoDocumento.nombre,
    n: version.n,
    cargadaEn: version.cargadaEn,
    datosDeclarados: datosDe(version),
    vigencia: version.vigencia ?? null,
    sinArchivo: esVersionSinArchivo(version, requisito.tipoDocumento.modo),
  };
}
