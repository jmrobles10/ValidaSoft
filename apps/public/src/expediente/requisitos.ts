import type { ExpedientePublicoDto, ObservacionPublicaDto, RequisitoPublicoDto } from '@validasoft/api-client';

/** Lo que el tercero puede hacer con un documento que le piden. */
export type AccionRequisito = 'subir' | 'corregir' | 'reemplazar' | null;

export function accionDeRequisito(requisito: Pick<RequisitoPublicoDto, 'estado'>, expediente: Pick<ExpedientePublicoDto, 'estado'>): AccionRequisito {
  if (!['Abierto', 'EnRevision', 'Observado'].includes(expediente.estado)) return null;
  switch (requisito.estado) {
    case 'Pendiente':
      return 'subir';
    case 'Observado':
      return 'corregir';
    case 'Cargado':
    case 'EnRevision':
      return 'reemplazar';
    default:
      return null; // aprobado o eximido
  }
}

/**
 * Orden de lo que ve el tercero: primero lo que le toca (observado, por subir), luego lo que revisa la empresa y al
 * final lo listo (design system P2PubExpediente: «Te toca», «Lo revisa la empresa», «Listo»).
 */
export type GrupoRequisito = 'te-toca' | 'en-revision' | 'listo';

export function grupoDeRequisito(requisito: Pick<RequisitoPublicoDto, 'estado'>): GrupoRequisito {
  if (requisito.estado === 'Observado' || requisito.estado === 'Pendiente') return 'te-toca';
  if (requisito.estado === 'Cargado' || requisito.estado === 'EnRevision') return 'en-revision';
  return 'listo';
}

export function requisitosPorGrupo(expediente: Pick<ExpedientePublicoDto, 'requisitos'>): Record<GrupoRequisito, RequisitoPublicoDto[]> {
  const grupos: Record<GrupoRequisito, RequisitoPublicoDto[]> = { 'te-toca': [], 'en-revision': [], listo: [] };
  for (const r of expediente.requisitos ?? []) grupos[grupoDeRequisito(r)].push(r);
  // Dentro de «te toca», primero lo observado: es lo que frena la revisión.
  grupos['te-toca'].sort((a, b) => Number(b.estado === 'Observado') - Number(a.estado === 'Observado'));
  return grupos;
}

/** Observaciones del expediente que no son de un documento concreto (las de cada documento van en su tarjeta). */
export function observacionesGenerales(expediente: Pick<ExpedientePublicoDto, 'observaciones'>): ObservacionPublicaDto[] {
  return expediente.observaciones.filter((o) => !o.requisito);
}

/** Observaciones de un documento: las de la tarjeta y, si el backend no las adjunta, las del expediente con su código. */
export function observacionesDeRequisito(requisito: RequisitoPublicoDto, expediente: Pick<ExpedientePublicoDto, 'observaciones'>): ObservacionPublicaDto[] {
  if (requisito.observaciones.length > 0) return requisito.observaciones;
  return expediente.observaciones.filter((o) => o.requisito === requisito.tipoDocumento.codigo);
}

/**
 * Enlace a «Subir documento» para este requisito: un canónico global va a la bóveda; un tipo propio de la empresa
 * necesita el vínculo. `volver` trae al tercero de regreso a su revisión.
 */
export function enlaceDeSubida(requisito: RequisitoPublicoDto, vinculoId: string | null, volver: string): string | null {
  const parametros = new URLSearchParams({ nombre: requisito.tipoDocumento.nombre, volver });
  if (requisito.tipoDocumento.tipoCanonico && requisito.tipoDocumento.ambito === 'Global') {
    parametros.set('tipo', requisito.tipoDocumento.tipoCanonico);
  } else if (vinculoId) {
    parametros.set('tipoDocumentoTenantId', requisito.tipoDocumento.id);
    parametros.set('vinculoId', vinculoId);
  } else {
    return null;
  }
  return `/boveda/subir?${parametros.toString()}`;
}

/** Mientras falten documentos o esté devuelto, la versión subida llega al expediente por una cola: se refresca solo. */
export function debeRefrescar(expediente: Pick<ExpedientePublicoDto, 'estado'> | undefined): number | false {
  return expediente && (expediente.estado === 'Abierto' || expediente.estado === 'Observado') ? 4000 : false;
}
