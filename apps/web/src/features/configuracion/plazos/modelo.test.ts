import { ErrorApi, type ConfiguracionSlaDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { aActualizarDto, borradorDeSla, errorDelPorcentaje, errorDeHoras, errorDePorcentaje, esValido, hayCambios } from './modelo';

const CONFIGURACION: ConfiguracionSlaDto = {
  porcentajeAviso: 80,
  avisarAdministradores: true,
  areas: [
    { areaId: 'a1', horas: 48 },
    { areaId: 'a2', horas: null },
  ],
  actualizadaEn: '2026-11-10T12:00:00Z',
};

describe('plazos de revisión: modelo', () => {
  it('el borrador parte de lo guardado: horas vacías para el área sin plazo', () => {
    expect(borradorDeSla(CONFIGURACION)).toEqual({ porcentaje: '80', avisarAdministradores: true, horas: { a1: '48', a2: '' } });
  });

  it('el porcentaje va de 50 a 95 y es obligatorio', () => {
    expect(errorDePorcentaje('')).toBe('requerido');
    expect(errorDePorcentaje('49')).toBe('rango');
    expect(errorDePorcentaje('96')).toBe('rango');
    expect(errorDePorcentaje('50')).toBeNull();
    expect(errorDePorcentaje('95')).toBeNull();
  });

  it('el plazo de un área puede quedar vacío; si se escribe va de 1 hora a 90 días (2160 horas)', () => {
    expect(errorDeHoras('')).toBeNull();
    expect(errorDeHoras('0')).toBe('rango');
    expect(errorDeHoras('1')).toBeNull();
    expect(errorDeHoras('2160')).toBeNull();
    expect(errorDeHoras('2161')).toBe('rango');
  });

  it('solo es válido si el porcentaje y todos los plazos lo son', () => {
    const base = borradorDeSla(CONFIGURACION);
    expect(esValido(base)).toBe(true);
    expect(esValido({ ...base, porcentaje: '10' })).toBe(false);
    expect(esValido({ ...base, horas: { ...base.horas, a2: '0' } })).toBe(false);
  });

  it('detecta cambios en el porcentaje, el interruptor y cualquier plazo; vacío y ausente son lo mismo', () => {
    const base = borradorDeSla(CONFIGURACION);
    expect(hayCambios(base, base)).toBe(false);
    expect(hayCambios({ ...base, porcentaje: '90' }, base)).toBe(true);
    expect(hayCambios({ ...base, avisarAdministradores: false }, base)).toBe(true);
    expect(hayCambios({ ...base, horas: { ...base.horas, a2: '24' } }, base)).toBe(true);
    expect(hayCambios({ ...base, horas: { a1: '48' } }, base)).toBe(false);
  });

  it('manda todas las áreas: el plazo vacío viaja como null (sin plazo por defecto)', () => {
    const base = borradorDeSla(CONFIGURACION);
    expect(aActualizarDto({ ...base, porcentaje: '70', horas: { a1: '72', a2: '' } })).toEqual({
      porcentajeAviso: 70,
      avisarAdministradores: true,
      areas: [
        { areaId: 'a1', horas: 72 },
        { areaId: 'a2', horas: null },
      ],
    });
  });

  it('el error del porcentaje se reconoce por su código; los demás son generales', () => {
    expect(errorDelPorcentaje(new ErrorApi(422, 'porcentaje-invalido', 'x', null, null))).toBe(true);
    expect(errorDelPorcentaje(new ErrorApi(422, 'sla-invalido', 'x', null, null))).toBe(false);
    expect(errorDelPorcentaje(new Error('red'))).toBe(false);
  });
});
