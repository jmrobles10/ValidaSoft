import type { ExpedientePublicoDto, RequisitoPublicoDto, SeccionDeRequisitoDto } from '@validasoft/api-client';

/** Datos de prueba de las secciones de «Mi información» como requisitos del expediente del tercero (F2). */

function seccionDeRequisito(seccion: string, nombre: string, cambios: Partial<SeccionDeRequisitoDto> = {}): SeccionDeRequisitoDto {
  return { seccion, nombre, periodo: null, exigeSoporte: false, completitud: null, versionEvaluada: null, datos: null, soportes: [], versionAceptada: null, cambios: [], ...cambios };
}

/** La información general observada por la empresa, con la razón social y la dirección señaladas. */
export const GENERAL_OBSERVADA: RequisitoPublicoDto = {
  id: 'r-general',
  tipoRequisito: 'Seccion',
  tipoDocumento: { id: 'x', codigo: 'FICHA_GENERAL', nombre: 'Información general', tipoCanonico: 'FICHA_GENERAL', ambito: 'Global', modo: 'Formulario' },
  obligatorio: true,
  estado: 'Observado',
  versionActual: { documentoId: 'sec-general', n: 2, sha256: 'h', cargadaEn: '2026-11-02T15:00:00Z', clase: 'Formulario' },
  observaciones: [
    {
      motivos: [{ codigo: 'dato-incorrecto', texto: 'Un dato no coincide con el soporte.' }],
      observacion: 'La razón social no coincide con el RUT.',
      fecha: '2026-11-08T15:00:00Z',
      requisito: 'FICHA_GENERAL',
      campos: ['razon_social', 'direccion'],
    },
  ],
  seccion: seccionDeRequisito('FICHA_GENERAL', 'Información general', { completitud: { completa: true, faltantes: [], reglaVersion: 1 }, versionEvaluada: 2 }),
};

/** La financiera 2025, pendiente: aún le falta el soporte para la empresa. */
export const FINANCIERA_PENDIENTE: RequisitoPublicoDto = {
  id: 'r-financiera',
  tipoRequisito: 'Seccion',
  tipoDocumento: { id: 'x', codigo: 'FICHA_FINANCIERA', nombre: 'Información financiera 2025', tipoCanonico: 'FICHA_FINANCIERA', ambito: 'Global', modo: 'Formulario' },
  obligatorio: true,
  estado: 'Pendiente',
  versionActual: null,
  observaciones: [],
  seccion: seccionDeRequisito('FICHA_FINANCIERA', 'Información financiera', {
    periodo: 2025,
    exigeSoporte: true,
    completitud: { completa: false, faltantes: [{ campo: null, regla: 'soporte-requerido', mensaje: 'Falta el soporte de la información financiera.' }], reglaVersion: 1 },
    versionEvaluada: 1,
  }),
};

/**
 * La información general en la renovación periódica del vínculo (VV-3): la empresa ya la aceptó (versión 1) y sigue completa, pero no
 * cubre la renovación hasta que el tercero la ratifique o la actualice.
 */
export const GENERAL_POR_RATIFICAR: RequisitoPublicoDto = {
  id: 'r-general',
  tipoRequisito: 'Seccion',
  tipoDocumento: { id: 'x', codigo: 'FICHA_GENERAL', nombre: 'Información general', tipoCanonico: 'FICHA_GENERAL', ambito: 'Global', modo: 'Formulario' },
  obligatorio: true,
  estado: 'Pendiente',
  versionActual: null,
  observaciones: [],
  puedeRatificar: true,
  seccion: seccionDeRequisito('FICHA_GENERAL', 'Información general', {
    completitud: { completa: true, faltantes: [], reglaVersion: 1 },
    versionEvaluada: 1,
    versionAceptada: 1,
  }),
};

/** Expediente de renovación abierto que espera la ratificación del tercero. */
export function renovacionConSecciones(requisitos: RequisitoPublicoDto[]): ExpedientePublicoDto {
  return { ...expedienteConSecciones(requisitos, 'Abierto'), tipo: 'Renovacion', requisitosFaltantes: requisitos.length };
}

export function expedienteConSecciones(requisitos: RequisitoPublicoDto[], estado: ExpedientePublicoDto['estado'] = 'Observado'): ExpedientePublicoDto {
  return {
    id: 'e1',
    folio: 'NORTE-E-2026-000001',
    empresa: { id: 'emp', nombre: 'Constructora Norte', logoUrl: null },
    tipoVinculo: { id: 'tv', codigo: 'PROVEEDOR', nombre: 'Proveedor' },
    estado,
    observaciones: [],
    puedeReenviar: true,
    reenvios: 0,
    actualizadoEn: '2026-11-08T15:00:00Z',
    requisitos,
  };
}
