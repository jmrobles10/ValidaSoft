import { SCOPES } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { expediente, requisito } from '../expedientes/datosPrueba';
import { listas, estudio } from './datosPrueba';
import { accionesDeVerificacion, claveRiesgo, ESTUDIO_VACIO, estudioADto, evidenciasPosibles, validarEstudio, verificacionesQueBloquean } from './modelo';

describe('Estudio del oficial de cumplimiento (VE-9)', () => {
  it('pide riesgo y concepto, ampliada con riesgo alto y observaciones con conflicto o desfavorable', () => {
    expect(validarEstudio(ESTUDIO_VACIO)).toEqual(['nivel-riesgo-requerido', 'concepto-requerido']);
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 3, concepto: 'Favorable' })).toEqual(['debida-diligencia-ampliada-requerida']);
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 1, concepto: 'Desfavorable' })).toEqual(['observaciones-requeridas']);
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 2, concepto: 'Favorable', conflictoDeInteres: true, observaciones: ' ' })).toEqual([
      'observaciones-requeridas',
    ]);
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 3, debidaDiligencia: 'Ampliada', concepto: 'Favorable' })).toEqual([]);
  });

  it('arma el cuerpo con las evidencias como documento y versión', () => {
    expect(
      estudioADto({ ...ESTUDIO_VACIO, nivelRiesgo: 2, concepto: 'Favorable', observaciones: '  Sin hallazgos. ', evidencias: ['doc-1:3'] }),
    ).toEqual({
      nivelRiesgo: 2,
      conflictoDeInteres: false,
      debidaDiligencia: 'Simple',
      concepto: 'Favorable',
      observaciones: 'Sin hallazgos.',
      evidencias: [{ documentoId: 'doc-1', version: 3 }],
    });
    expect(claveRiesgo(3)).toBe('r3');
    expect(claveRiesgo(7)).toBe('r1');
  });

  it('cita como evidencia solo los documentos del expediente con versión', () => {
    const conDocumento = { ...expediente('EnRevision'), requisitos: [requisito({}), requisito({ id: 'r2', versionActual: null })] };
    const posibles = evidenciasPosibles(conDocumento);
    expect(posibles).toHaveLength(1);
    expect(posibles[0]?.clave).toMatch(/^.+:\d+$/);
  });
});

describe('Acciones de verificación', () => {
  it('consultar con verificaciones:escribir, decidir con cumplimiento:decidir, solo con el expediente abierto', () => {
    const oficial = new Set([SCOPES.verificacionesEscribir, SCOPES.cumplimientoDecidir, SCOPES.documentosDescargar]);
    expect(accionesDeVerificacion({ estado: 'EnRevision' }, oficial)).toEqual({ consultar: true, decidir: true, descargar: true });
    expect(accionesDeVerificacion({ estado: 'Aprobado' }, oficial)).toEqual({ consultar: false, decidir: false, descargar: true });
    expect(accionesDeVerificacion({ estado: 'EnRevision' }, new Set([SCOPES.expedientesLeer]))).toEqual({
      consultar: false,
      decidir: false,
      descargar: false,
    });
  });

  it('bloquean la aprobación las bloqueantes que no están satisfechas', () => {
    const verificaciones = [listas({ estado: 'Satisfecha' }), estudio({ estado: 'Expirada' }), listas({ tipo: 'ListasRestrictivas', bloqueante: false, estado: 'Pendiente' })];
    expect(verificacionesQueBloquean({ verificaciones }).map((v) => v.tipo)).toEqual(['EstudioDeCumplimiento']);
  });
});
