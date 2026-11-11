import { describe, expect, it } from 'vitest';
import i18n from '../../i18n';
import {
  accionesDeMuestreo,
  CODIGOS_DE_MOTIVO,
  claveDeFila,
  debeVirtualizar,
  esCodigoDeMotivo,
  etiquetaDeMotivo,
  filtrarPorTipo,
  MAXIMO_FILAS,
  MOTIVOS_QUE_LLEVAN_A_UNA_PERSONA,
  porcentajeDeConfianza,
  resolucionDe,
  textoDeConteo,
  tipoDe,
  tipoDeUrl,
  tipoParaUrl,
  vistaDeRevision,
} from './modelo';

const t = i18n.t.bind(i18n);

describe('motivos de la revisión automática', () => {
  it('cada código que manda el backend tiene su texto corto en español', () => {
    for (const codigo of CODIGOS_DE_MOTIVO) {
      const texto = etiquetaDeMotivo({ codigo, texto: 'texto del servidor' }, t);
      expect(texto).not.toBe('texto del servidor');
      expect(texto).not.toContain('revisionAutomatica.');
      expect(texto.length).toBeGreaterThan(3);
    }
    expect(etiquetaDeMotivo({ codigo: 'confianza-baja', texto: 'x' }, t)).toBe('Confianza baja');
    expect(etiquetaDeMotivo({ codigo: 'alertas-de-manipulacion', texto: 'x' }, t)).toBe('Señales de manipulación');
    expect(etiquetaDeMotivo({ codigo: 'sin-presupuesto', texto: 'x' }, t)).toBe('Tope mensual alcanzado');
  });

  it('un código que esta versión no conoce usa el texto del servidor, y sin texto, el código', () => {
    expect(esCodigoDeMotivo('otro-motivo')).toBe(false);
    expect(etiquetaDeMotivo({ codigo: 'otro-motivo', texto: ' Un motivo nuevo. ' }, t)).toBe('Un motivo nuevo.');
    expect(etiquetaDeMotivo({ codigo: 'otro-motivo', texto: '' }, t)).toBe('otro-motivo');
  });

  it('lo que manda siempre a una persona son códigos reales y no incluye el alcance ni el tope', () => {
    expect(MOTIVOS_QUE_LLEVAN_A_UNA_PERSONA.every(esCodigoDeMotivo)).toBe(true);
    expect(MOTIVOS_QUE_LLEVAN_A_UNA_PERSONA).not.toContain('tipo-no-autorizado');
    expect(MOTIVOS_QUE_LLEVAN_A_UNA_PERSONA).not.toContain('sin-presupuesto');
  });
});

describe('confianza y tiempos', () => {
  it('la confianza va en porcentaje, como el chip del análisis, y admite los decimales de .NET como texto', () => {
    expect(porcentajeDeConfianza(0.97)).toMatch(/^97\s?%$/);
    expect(porcentajeDeConfianza('0.855')).toMatch(/^86\s?%$/);
    expect(porcentajeDeConfianza(1.4)).toMatch(/^100\s?%$/);
    expect(porcentajeDeConfianza(null)).toBeNull();
    expect(porcentajeDeConfianza('no es un número')).toBeNull();
  });

  it('el contador de la pestaña dice «200+» cuando la API llegó a su máximo', () => {
    expect(textoDeConteo(3)).toBe('3');
    expect(textoDeConteo(MAXIMO_FILAS - 1)).toBe('199');
    expect(textoDeConteo(MAXIMO_FILAS)).toBe('200+');
  });

  it('la lista se virtualiza solo con más de 50 filas', () => {
    expect(debeVirtualizar(50)).toBe(false);
    expect(debeVirtualizar(51)).toBe(true);
  });
});

describe('filas de la bandeja', () => {
  const filas = [{ tipo: 'Excepcion' }, { tipo: 'Muestreo' }, { tipo: 'Excepcion' }];

  it('filtra por tipo y, sin tipo, devuelve todas', () => {
    expect(filtrarPorTipo(filas, 'Muestreo')).toHaveLength(1);
    expect(filtrarPorTipo(filas, 'Excepcion')).toHaveLength(2);
    expect(filtrarPorTipo(filas, '')).toHaveLength(3);
  });

  it('un tipo desconocido se trata como excepción; el tipo y la URL se corresponden', () => {
    expect(tipoDe({ tipo: 'Nuevo' })).toBe('Excepcion');
    expect(tipoDeUrl('muestreo')).toBe('Muestreo');
    expect(tipoDeUrl('excepcion')).toBe('Excepcion');
    expect(tipoDeUrl('otra')).toBe('');
    expect(tipoDeUrl(null)).toBe('');
    expect(tipoParaUrl('Muestreo')).toBe('muestreo');
    expect(tipoParaUrl('Excepcion')).toBe('excepcion');
  });

  it('la clave distingue el documento, el área y la versión', () => {
    const base = { expedienteId: 'e', requisitoId: 'r', areaId: 'a', version: 1 };
    expect(claveDeFila(base)).toBe('e|r|a|1');
    expect(claveDeFila({ ...base, areaId: 'b' })).not.toBe(claveDeFila(base));
    expect(claveDeFila({ ...base, version: 2 })).not.toBe(claveDeFila(base));
  });

  it('la resolución del muestreo es Confirmada, Revertida o nada', () => {
    expect(resolucionDe('Confirmada')).toBe('Confirmada');
    expect(resolucionDe('Revertida')).toBe('Revertida');
    expect(resolucionDe('Otra')).toBeNull();
    expect(resolucionDe(null)).toBeNull();
  });
});

describe('qué se ve de lo que hizo la revisión automática', () => {
  const motivos = [{ codigo: 'confianza-baja', texto: 'La confianza del análisis está bajo el umbral.' }];
  const base = { sugerencia: null, motivos: [], confianza: 0.97, pendienteDeAtencion: false, resolucion: null };

  it('Asistido: sugiere aprobar o revisar con sus motivos', () => {
    expect(vistaDeRevision({ ...base, resultado: 'Sugerida', sugerencia: 'Aprobar' })).toEqual({ tipo: 'sugerida', sugerencia: 'Aprobar', motivos: [] });
    expect(vistaDeRevision({ ...base, resultado: 'Sugerida', sugerencia: 'Revisar', motivos })).toEqual({ tipo: 'sugerida', sugerencia: 'Revisar', motivos });
  });

  it('Automático: aprobada, aprobada con muestreo pendiente o resuelta, y excepción abierta o cerrada', () => {
    expect(vistaDeRevision({ ...base, resultado: 'AprobadaAutomaticamente' })).toMatchObject({ tipo: 'aprobada', muestreo: false, pendiente: false });
    expect(vistaDeRevision({ ...base, resultado: 'AprobadaConMuestreo', pendienteDeAtencion: true })).toMatchObject({ tipo: 'aprobada', muestreo: true, pendiente: true, resolucion: null });
    expect(vistaDeRevision({ ...base, resultado: 'AprobadaConMuestreo', resolucion: 'Revertida' })).toMatchObject({ muestreo: true, pendiente: false, resolucion: 'Revertida' });
    expect(vistaDeRevision({ ...base, resultado: 'Excepcion', motivos, pendienteDeAtencion: true })).toEqual({ tipo: 'excepcion', abierta: true, motivos });
    expect(vistaDeRevision({ ...base, resultado: 'Excepcion', motivos })).toMatchObject({ abierta: false });
  });

  it('fuera del alcance (o un resultado que no se conoce) no dice nada', () => {
    expect(vistaDeRevision({ ...base, resultado: 'SinAlcance' })).toBeNull();
    expect(vistaDeRevision({ ...base, resultado: 'Algo nuevo' })).toBeNull();
  });
});

describe('quién puede confirmar o revertir un muestreo', () => {
  const base = { pendiente: true, estadoExpediente: 'EnRevision', puedeRevisar: true, esMiembro: true as boolean | null };

  it('un miembro del área con permiso confirma y revierte con el expediente en revisión', () => {
    expect(accionesDeMuestreo(base)).toEqual({ confirmar: true, revertir: true });
  });

  it('con el expediente ya cerrado solo se confirma: revertir no tiene dónde devolver el documento', () => {
    expect(accionesDeMuestreo({ ...base, estadoExpediente: 'Aprobado' })).toEqual({ confirmar: true, revertir: false });
  });

  it('sin permiso, sin ser miembro o ya resuelto no hay acciones; si no se sabe la membresía, decide la API', () => {
    expect(accionesDeMuestreo({ ...base, puedeRevisar: false })).toEqual({ confirmar: false, revertir: false });
    expect(accionesDeMuestreo({ ...base, esMiembro: false })).toEqual({ confirmar: false, revertir: false });
    expect(accionesDeMuestreo({ ...base, pendiente: false })).toEqual({ confirmar: false, revertir: false });
    expect(accionesDeMuestreo({ ...base, esMiembro: null })).toEqual({ confirmar: true, revertir: true });
  });
});
