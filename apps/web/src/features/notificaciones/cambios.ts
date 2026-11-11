import type { MapaDeInvalidacion } from '@validasoft/api-client';

/**
 * Qué consultas de la web de empresa se refrescan cuando el stream avisa que cambió un recurso de la empresa activa (NT-12). Son
 * prefijos de la ruta de la consulta (la clave de orval empieza por la ruta): `/api/v1/vinculos` incluye la cuenta designada, los
 * documentos y la ficha del vínculo; `/api/v1/terceros`, la ficha y la vista 360 del tercero.
 */
export const ALCANCES_WEB = {
  expedientes: '/api/v1/expedientes',
  bandeja: '/api/v1/bandeja',
  revalidaciones: '/api/v1/revalidaciones',
  vinculos: '/api/v1/vinculos',
  terceros: '/api/v1/terceros',
  vencimientos: '/api/v1/vencimientos',
  certificados: '/api/v1/certificados',
  pendientes: '/api/v1/mis-pendientes',
  indicadores: '/api/v1/indicadores',
  cumplimiento: '/api/v1/cumplimiento',
} as const;

export const MAPA_WEB: MapaDeInvalidacion = {
  expediente: [
    ALCANCES_WEB.expedientes,
    ALCANCES_WEB.bandeja,
    ALCANCES_WEB.revalidaciones,
    ALCANCES_WEB.vinculos,
    ALCANCES_WEB.pendientes,
    ALCANCES_WEB.indicadores,
    ALCANCES_WEB.cumplimiento,
  ],
  vinculo: [ALCANCES_WEB.vinculos, ALCANCES_WEB.terceros, ALCANCES_WEB.bandeja, ALCANCES_WEB.indicadores],
  cuenta: [ALCANCES_WEB.vinculos, ALCANCES_WEB.terceros],
  documento: [ALCANCES_WEB.vinculos, ALCANCES_WEB.expedientes, ALCANCES_WEB.revalidaciones, ALCANCES_WEB.vencimientos],
  ficha: [ALCANCES_WEB.terceros, ALCANCES_WEB.vinculos, ALCANCES_WEB.expedientes, ALCANCES_WEB.revalidaciones],
  certificado: [ALCANCES_WEB.certificados, ALCANCES_WEB.vinculos, ALCANCES_WEB.terceros],
};
