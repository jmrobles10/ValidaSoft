import type { CoincidenciaDto, VerificacionDto } from '@validasoft/api-client';

/** Datos de prueba de las verificaciones (H7). */
export function coincidencia(parcial: Partial<CoincidenciaDto> = {}): CoincidenciaDto {
  return {
    id: 'c1',
    persona: 'Ana Coincidencia',
    rol: 'RepresentanteLegal',
    identificacion: 'CC 1020304050',
    lista: 'OFAC (SDN)',
    nombreEncontrado: 'ANA COINCIDENCIA',
    detalle: null,
    puntaje: 100,
    decision: null,
    justificacion: null,
    resueltaPor: null,
    resueltaEn: null,
    vigenteHasta: null,
    heredada: false,
    ...parcial,
  };
}

export function listas(parcial: Partial<VerificacionDto> = {}): VerificacionDto {
  return {
    tipo: 'ListasRestrictivas',
    estado: 'CoincidenciaPorAnalizar',
    bloqueante: true,
    requeridaPorElFlujo: true,
    proveedor: 'Simulado',
    resultado: 'Coincidencias',
    referenciaExterna: 'sim-1',
    registradaEn: '2026-11-10T14:00:00Z',
    registradaPor: 'sistema',
    vigenteHasta: '2027-11-10T14:00:00Z',
    evidencia: { nombreArchivo: 'listas.pdf', tipoContenido: 'application/pdf', tamano: 1000, sha256: 'a'.repeat(64), delProveedor: false, sha256Respuesta: null },
    personas: [
      { rol: 'Tercero', nombre: 'Aceros del Norte S.A.S.', identificacion: 'NIT 900123456' },
      { rol: 'RepresentanteLegal', nombre: 'Ana Coincidencia', identificacion: 'CC 1020304050' },
    ],
    coincidencias: [coincidencia()],
    estudio: null,
    anteriores: [],
    ultimoError: null,
    reintentarDesde: null,
    vigenciaDias: null,
    frecuenciaReconsultaMeses: null,
    ...parcial,
  };
}

export function estudio(parcial: Partial<VerificacionDto> = {}): VerificacionDto {
  return {
    ...listas(),
    tipo: 'EstudioDeCumplimiento',
    estado: 'Pendiente',
    proveedor: null,
    resultado: null,
    referenciaExterna: null,
    registradaEn: null,
    registradaPor: null,
    vigenteHasta: null,
    evidencia: null,
    personas: [],
    coincidencias: [],
    ...parcial,
  };
}
