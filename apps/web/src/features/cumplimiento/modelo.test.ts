import { SCOPES, type ResultadoDeMatrizDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { expediente, requisito } from '../expedientes/datosPrueba';
import { estudio, listas, matrizDePrueba, resultadoDeMatriz } from './datosPrueba';
import {
  accionesDeVerificacion,
  claveRiesgo,
  debidaEfectiva,
  debidaMinima,
  efectoDelEstudio,
  errorDeArchivo,
  ESTUDIO_VACIO,
  estudioADto,
  evidenciasPosibles,
  factoresManuales,
  formatearPeso,
  formatearPuntaje,
  hayHallazgoGrave,
  nivelEfectivo,
  respuestasADto,
  respuestasCompletas,
  tonoDeRiesgo,
  validarEstudio,
  verificacionesQueBloquean,
  type BorradorEstudio,
  type ContextoMatriz,
} from './modelo';

describe('Estudio del oficial de cumplimiento sin matriz (VE-9)', () => {
  it('pide riesgo y concepto, intensificada con riesgo alto y observaciones con conflicto o desfavorable', () => {
    expect(validarEstudio(ESTUDIO_VACIO)).toEqual(['nivel-riesgo-requerido', 'concepto-requerido']);
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 3, concepto: 'Favorable' })).toEqual(['debida-diligencia-ampliada-requerida']);
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 3, debidaDiligencia: 'Ordinaria', concepto: 'Favorable' })).toEqual(['debida-diligencia-ampliada-requerida']);
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 1, concepto: 'Desfavorable' })).toEqual(['observaciones-requeridas']);
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 2, concepto: 'Favorable', conflictoDeInteres: true, observaciones: ' ' })).toEqual([
      'observaciones-requeridas',
    ]);
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 3, debidaDiligencia: 'Ampliada', concepto: 'Favorable' })).toEqual([]);
    // Con riesgo medio vale cualquiera de las tres.
    expect(validarEstudio({ ...ESTUDIO_VACIO, nivelRiesgo: 2, debidaDiligencia: 'Ordinaria', concepto: 'Favorable' })).toEqual([]);
  });

  it('arma el cuerpo con las evidencias como documento y versión, y los archivos solo si hay', () => {
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
    expect(estudioADto({ ...ESTUDIO_VACIO, nivelRiesgo: 1, concepto: 'Favorable', adjuntos: ['a1', 'a2'] }).adjuntos).toEqual(['a1', 'a2']);
    expect(claveRiesgo(3)).toBe('r3');
    expect(claveRiesgo(7)).toBe('r1');
    expect(tonoDeRiesgo(1)).toBe('exito');
    expect(tonoDeRiesgo(2)).toBe('advertencia');
    expect(tonoDeRiesgo(3)).toBe('error');
  });

  it('cita como evidencia solo los documentos del expediente con versión', () => {
    const conDocumento = { ...expediente('EnRevision'), requisitos: [requisito({}), requisito({ id: 'r2', versionActual: null })] };
    const posibles = evidenciasPosibles(conDocumento);
    expect(posibles).toHaveLength(1);
    expect(posibles[0]?.clave).toMatch(/^.+:\d+$/);
  });
});

describe('Estudio con matriz de riesgo activa (CU-17)', () => {
  const matriz = matrizDePrueba({ activa: true, esPorDefecto: false, version: 3 });
  const manuales = factoresManuales(matriz);
  const contexto = (resultado: ResultadoDeMatrizDto | null = resultadoDeMatriz()): ContextoMatriz => ({ factoresManuales: manuales, niveles: matriz.niveles, resultado });
  const completo = (cambio: Partial<BorradorEstudio> = {}): BorradorEstudio => ({ ...ESTUDIO_VACIO, concepto: 'Favorable', respuestas: { sector: 'medio' }, ...cambio });

  it('el oficial solo responde los factores manuales y el cuerpo lleva sus respuestas', () => {
    expect(manuales.map((f) => f.codigo)).toEqual(['sector']);
    expect(respuestasCompletas(manuales, {})).toBe(false);
    expect(respuestasCompletas(manuales, { sector: 'otra' })).toBe(false);
    expect(respuestasCompletas(manuales, { sector: 'medio' })).toBe(true);
    expect(respuestasADto(manuales, { sector: 'medio', ajeno: 'x' })).toEqual([{ factor: 'sector', opcion: 'medio' }]);
  });

  it('pide responder todo, tener el cálculo y el concepto; sin ajuste no pide nivel ni justificación', () => {
    expect(validarEstudio(ESTUDIO_VACIO, contexto(null))).toEqual(['respuestas-incompletas', 'concepto-requerido']);
    expect(validarEstudio(completo(), contexto(null))).toEqual(['calculo-pendiente']);
    expect(validarEstudio(completo(), contexto())).toEqual([]);
  });

  it('con el nivel de la matriz el cuerpo no manda nivel: lo calcula el servidor; la debida diligencia es la del nivel', () => {
    const dto = estudioADto(completo(), contexto());
    expect(dto).toMatchObject({ nivelRiesgo: null, debidaDiligencia: 'Ordinaria', respuestas: [{ factor: 'sector', opcion: 'medio' }] });
    expect(dto.justificacionDeNivel).toBeUndefined();
  });

  it('ajustar el nivel exige una justificación de 10 caracteres y se manda con el nivel nuevo', () => {
    const ajustado = completo({ ajustarNivel: true, nivelRiesgo: 3 });
    expect(validarEstudio(ajustado, contexto())).toEqual(['justificacion-de-nivel-requerida']);
    expect(validarEstudio({ ...ajustado, justificacionDeNivel: 'corta' }, contexto())).toEqual(['justificacion-de-nivel-requerida']);
    const listo = { ...ajustado, justificacionDeNivel: '  Operación atípica con efectivo.  ' };
    expect(validarEstudio(listo, contexto())).toEqual([]);
    expect(estudioADto(listo, contexto())).toMatchObject({ nivelRiesgo: 3, debidaDiligencia: 'Ampliada', justificacionDeNivel: 'Operación atípica con efectivo.' });
    // «Ajustar» al mismo nivel que dio la matriz no es un ajuste: no pide justificación ni manda nivel.
    const igual = completo({ ajustarNivel: true, nivelRiesgo: 2 });
    expect(validarEstudio(igual, contexto())).toEqual([]);
    expect(estudioADto(igual, contexto()).nivelRiesgo).toBeNull();
  });

  it('la debida diligencia nunca queda más ligera que la del nivel, pero puede ser más intensa', () => {
    expect(debidaMinima(matriz.niveles, 1)).toBe('Simple');
    expect(debidaMinima(matriz.niveles, 3)).toBe('Ampliada');
    expect(debidaMinima([], 2)).toBe('Ordinaria');
    expect(debidaEfectiva({ debidaElegida: 'Simple' }, matriz.niveles, 2)).toBe('Ordinaria');
    expect(debidaEfectiva({ debidaElegida: 'Ampliada' }, matriz.niveles, 2)).toBe('Ampliada');
    expect(debidaEfectiva({ debidaElegida: null }, matriz.niveles, 1)).toBe('Simple');
    expect(nivelEfectivo({ ajustarNivel: false, nivelRiesgo: 3 }, { nivel: 2 })).toBe(2);
    expect(nivelEfectivo({ ajustarNivel: true, nivelRiesgo: 3 }, { nivel: 2 })).toBe(3);
    expect(estudioADto(completo({ debidaElegida: 'Ampliada' }), contexto()).debidaDiligencia).toBe('Ampliada');
  });

  it('conflicto o desfavorable piden observaciones también con matriz', () => {
    expect(validarEstudio(completo({ concepto: 'Desfavorable' }), contexto())).toEqual(['observaciones-requeridas']);
  });

  it('formatea el puntaje con coma decimal', () => {
    expect(formatearPuntaje(42.5)).toBe('42,5');
    expect(formatearPuntaje('7')).toBe('7,0');
  });
});

describe('Qué pasa al registrar el estudio', () => {
  const sinEstudio = (extra: Parameters<typeof listas>[0] = {}) => ({
    esperandoVerificaciones: true,
    verificaciones: [listas({ estado: 'Satisfecha', ...extra }), estudio()],
  });

  it('si es lo último que falta y el concepto es favorable, el expediente se aprueba', () => {
    expect(efectoDelEstudio(sinEstudio(), 'Favorable')).toEqual({ tipo: 'aprueba' });
    expect(efectoDelEstudio(sinEstudio(), null)).toEqual({ tipo: 'aprueba' });
  });

  it('con concepto desfavorable la verificación falla y no aprueba', () => {
    expect(efectoDelEstudio(sinEstudio(), 'Desfavorable')).toEqual({ tipo: 'desfavorable' });
  });

  it('si el expediente además espera la cuenta bancaria designada, el estudio no lo aprueba: queda esperando la cuenta', () => {
    expect(efectoDelEstudio({ ...sinEstudio(), esperandoCuentaDesignada: true }, 'Favorable')).toEqual({ tipo: 'espera-cuenta' });
    expect(efectoDelEstudio({ ...sinEstudio(), esperandoCuentaDesignada: false }, 'Favorable')).toEqual({ tipo: 'aprueba' });
    // Con un concepto desfavorable o con otra verificación pendiente manda lo suyo.
    expect(efectoDelEstudio({ ...sinEstudio(), esperandoCuentaDesignada: true }, 'Desfavorable')).toEqual({ tipo: 'desfavorable' });
    expect(efectoDelEstudio({ ...sinEstudio({ estado: 'CoincidenciaPorAnalizar' }), esperandoCuentaDesignada: true }, 'Favorable').tipo).toBe('espera-verificaciones');
  });

  it('si falta otra verificación bloqueante o las áreas, sigue en revisión', () => {
    const pendiente = efectoDelEstudio(sinEstudio({ estado: 'CoincidenciaPorAnalizar' }), 'Favorable');
    expect(pendiente.tipo).toBe('espera-verificaciones');
    expect(efectoDelEstudio({ esperandoVerificaciones: false, verificaciones: [listas({ estado: 'Satisfecha' }), estudio()] }, 'Favorable')).toEqual({ tipo: 'espera-areas' });
  });
});

describe('Acciones de verificación', () => {
  const oficial = new Set([SCOPES.verificacionesEscribir, SCOPES.cumplimientoDecidir, SCOPES.documentosDescargar]);

  it('consultar y registrar una consulta por fuera con verificaciones:escribir, decidir con cumplimiento:decidir, solo con el expediente abierto', () => {
    expect(accionesDeVerificacion({ estado: 'EnRevision' }, oficial)).toEqual({ consultar: true, consultaManual: true, decidir: true, rechazar: false, descargar: true });
    expect(accionesDeVerificacion({ estado: 'Aprobado' }, oficial)).toEqual({ consultar: false, consultaManual: false, decidir: false, rechazar: false, descargar: true });
    expect(accionesDeVerificacion({ estado: 'EnRevision' }, new Set([SCOPES.expedientesLeer]))).toEqual({
      consultar: false,
      consultaManual: false,
      decidir: false,
      rechazar: false,
      descargar: false,
    });
  });

  it('rechazar por cumplimiento: permiso, expediente en revisión u observado y una verificación fallida', () => {
    const fallida = { verificaciones: [listas({ estado: 'Fallida' }), estudio()] };
    expect(hayHallazgoGrave(fallida)).toBe(true);
    expect(hayHallazgoGrave({ verificaciones: [listas({ estado: 'CoincidenciaPorAnalizar' }), estudio()] })).toBe(false);
    expect(accionesDeVerificacion({ estado: 'EnRevision', ...fallida }, oficial).rechazar).toBe(true);
    expect(accionesDeVerificacion({ estado: 'Observado', ...fallida }, oficial).rechazar).toBe(true);
    expect(accionesDeVerificacion({ estado: 'Abierto', ...fallida }, oficial).rechazar).toBe(false);
    expect(accionesDeVerificacion({ estado: 'EnRevision', verificaciones: [listas({ estado: 'Satisfecha' })] }, oficial).rechazar).toBe(false);
    expect(accionesDeVerificacion({ estado: 'EnRevision', ...fallida }, new Set([SCOPES.verificacionesEscribir])).rechazar).toBe(false);
  });

  it('bloquean la aprobación las bloqueantes que no están satisfechas', () => {
    const verificaciones = [listas({ estado: 'Satisfecha' }), estudio({ estado: 'Expirada' }), listas({ tipo: 'ListasRestrictivas', bloqueante: false, estado: 'Pendiente' })];
    expect(verificacionesQueBloquean({ verificaciones }).map((v) => v.tipo)).toEqual(['EstudioDeCumplimiento']);
  });
});

describe('Archivos del oficial', () => {
  it('solo PDF e imágenes de hasta 15 MB', () => {
    expect(errorDeArchivo({ type: 'application/pdf', size: 1000 })).toBeNull();
    expect(errorDeArchivo({ type: 'image/png', size: 15 * 1024 * 1024 })).toBeNull();
    expect(errorDeArchivo({ type: 'application/zip', size: 1000 })).toBe('tipo');
    expect(errorDeArchivo({ type: 'application/pdf', size: 15 * 1024 * 1024 + 1 })).toBe('tamano');
    expect(errorDeArchivo({ type: 'application/pdf', size: 0 })).toBe('tamano');
  });

  it('muestra el peso en KB o MB', () => {
    expect(formatearPeso(204_800)).toBe('200 KB');
    expect(formatearPeso(10)).toBe('1 KB');
    expect(formatearPeso(3 * 1024 * 1024 + 512 * 1024)).toBe('3,5 MB');
  });
});
