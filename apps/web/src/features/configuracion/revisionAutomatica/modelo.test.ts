import type { RevisionAutomaticaConfigDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { aDto, borradorDe, efectivo, hayCambios, nivelDe, pideConfirmacion, umbralDelPlan, validar, type BorradorRevision } from './modelo';

const CONFIG: RevisionAutomaticaConfigDto = {
  configurada: true,
  nivel: 'Automatico',
  umbralConfianza: '0.95',
  porcentajeMuestreo: 10,
  topeMensualAprobaciones: 200,
  tiposAutorizados: ['CAMARA_COMERCIO', 'RUT'],
  areasAutorizadas: ['a-compras'],
  esperaIaMinutos: 10,
  actualizadaEn: '2026-11-10T12:00:00Z',
  iaDisponible: true,
  umbralDelPlan: 0.85,
  uso: { mes: '2026-11', aprobadas: 12, muestreadas: 2, excepciones: 3, sugerencias: 0, tope: 200 },
  tiposAnalizables: [
    { codigo: 'CAMARA_COMERCIO', nombre: 'Certificado de existencia y representación legal' },
    { codigo: 'RUT', nombre: 'Registro Único Tributario' },
  ],
  areas: [
    { id: 'a-compras', codigo: 'compras', nombre: 'Compras' },
    { id: 'a-cumplimiento', codigo: 'cumplimiento', nombre: 'Cumplimiento' },
  ],
};

const original = borradorDe(CONFIG);
const con = (cambio: Partial<BorradorRevision>): BorradorRevision => ({ ...original, ...cambio });

describe('borrador de la revisión automática', () => {
  it('pasa la configuración a lo que se edita: confianza en porcentaje, números como texto y el tope vacío si no hay', () => {
    expect(original).toEqual({
      nivel: 'Automatico',
      umbral: '95',
      muestreo: '10',
      tope: '200',
      espera: '10',
      tipos: ['CAMARA_COMERCIO', 'RUT'],
      areas: ['a-compras'],
    });
    expect(borradorDe({ ...CONFIG, topeMensualAprobaciones: null }).tope).toBe('');
    expect(borradorDe({ ...CONFIG, umbralConfianza: 0.85 }).umbral).toBe('85');
  });

  it('un nivel desconocido es Manual', () => {
    expect(nivelDe({ nivel: 'Otro' })).toBe('Manual');
    expect(nivelDe({ nivel: 'Asistido' })).toBe('Asistido');
  });

  it('el umbral del plan se muestra en porcentaje', () => {
    expect(umbralDelPlan({ umbralDelPlan: 0.9 })).toBe(90);
    expect(umbralDelPlan({ umbralDelPlan: '0.85' })).toBe(85);
  });
});

describe('validación local de lo que la API rechazaría', () => {
  it('un borrador correcto en Automático no tiene errores', () => {
    expect(validar(original)).toEqual({});
  });

  it('la confianza va de 85 a 100', () => {
    expect(validar(con({ umbral: '84' })).umbral).toBe('rango');
    expect(validar(con({ umbral: '101' })).umbral).toBe('rango');
    expect(validar(con({ umbral: '' })).umbral).toBe('rango');
    expect(validar(con({ umbral: '85' })).umbral).toBeUndefined();
    expect(validar(con({ umbral: '100' })).umbral).toBeUndefined();
  });

  it('la muestra va de 0 a 100; el tope, de 1 a un millón o vacío; la espera, de 1 a 1440', () => {
    expect(validar(con({ muestreo: '' })).muestreo).toBe('rango');
    expect(validar(con({ muestreo: '101' })).muestreo).toBe('rango');
    expect(validar(con({ muestreo: '0' })).muestreo).toBeUndefined();
    expect(validar(con({ tope: '0' })).tope).toBe('rango');
    expect(validar(con({ tope: '1000001' })).tope).toBe('rango');
    expect(validar(con({ tope: '' })).tope).toBeUndefined();
    expect(validar(con({ espera: '0' })).espera).toBe('rango');
    expect(validar(con({ espera: '1441' })).espera).toBe('rango');
    expect(validar(con({ espera: '1440' })).espera).toBeUndefined();
  });

  it('Automático necesita al menos un tipo y un área', () => {
    expect(validar(con({ tipos: [] })).tipos).toBe('vacio');
    expect(validar(con({ areas: [] })).areas).toBe('vacio');
  });

  it('cada nivel valida solo lo que usa: Manual nada, Asistido la confianza', () => {
    const roto = con({ umbral: '10', muestreo: '', espera: '0', tipos: [], areas: [] });
    expect(validar({ ...roto, nivel: 'Manual' })).toEqual({});
    expect(validar({ ...roto, nivel: 'Asistido' })).toEqual({ umbral: 'rango' });
    expect(Object.keys(validar({ ...roto, nivel: 'Automatico' }))).toEqual(['umbral', 'muestreo', 'espera', 'tipos', 'areas']);
  });
});

describe('lo que se guarda y cuándo hay cambios', () => {
  it('sin tocar nada no hay cambios', () => {
    expect(hayCambios(original, original)).toBe(false);
    expect(hayCambios(original, con({ tipos: ['RUT', 'CAMARA_COMERCIO'] }))).toBe(false);
  });

  it('cualquier parámetro de Automático cuenta', () => {
    expect(hayCambios(original, con({ muestreo: '20' }))).toBe(true);
    expect(hayCambios(original, con({ tope: '' }))).toBe(true);
    expect(hayCambios(original, con({ areas: ['a-compras', 'a-cumplimiento'] }))).toBe(true);
    expect(hayCambios(original, con({ nivel: 'Asistido' }))).toBe(true);
  });

  it('lo que el nivel no muestra queda como estaba: editar y cambiar de nivel no deja cambios ocultos', () => {
    const asistido = efectivo(con({ nivel: 'Asistido', muestreo: '99', tipos: [], umbral: '90' }), original);
    expect(asistido).toEqual({ ...original, nivel: 'Asistido', umbral: '90' });
    const manual = efectivo(con({ nivel: 'Manual', umbral: '90', muestreo: '99' }), original);
    expect(manual).toEqual({ ...original, nivel: 'Manual' });
    // En Manual, editar los parámetros de Automático (que no se ven) no es un cambio.
    const base = borradorDe({ ...CONFIG, nivel: 'Manual' });
    expect(hayCambios(base, { ...base, muestreo: '99', tipos: [] })).toBe(false);
  });

  it('arma el cuerpo del PUT: confianza como decimal, tope nulo si está vacío y las listas', () => {
    expect(aDto(original)).toEqual({
      nivel: 'Automatico',
      umbralConfianza: 0.95,
      porcentajeMuestreo: 10,
      topeMensualAprobaciones: 200,
      tiposAutorizados: ['CAMARA_COMERCIO', 'RUT'],
      areasAutorizadas: ['a-compras'],
      esperaIaMinutos: 10,
    });
    expect(aDto(con({ tope: '', umbral: '97' }))).toMatchObject({ topeMensualAprobaciones: null, umbralConfianza: 0.97 });
  });
});

describe('cuándo se avisa antes de guardar', () => {
  const manual = borradorDe({ ...CONFIG, nivel: 'Manual', tiposAutorizados: [], areasAutorizadas: [] });

  it('pasar a Automático avisa; pasar a Manual o a Asistido no', () => {
    expect(pideConfirmacion(manual, { ...manual, nivel: 'Automatico', tipos: ['RUT'], areas: ['a-compras'] })).toBe(true);
    expect(pideConfirmacion(original, con({ nivel: 'Asistido' }))).toBe(false);
    expect(pideConfirmacion(original, con({ nivel: 'Manual' }))).toBe(false);
  });

  it('ya en Automático, avisa cuando el sistema aprobaría más (más tipos o áreas, menos confianza o menos muestra)', () => {
    expect(pideConfirmacion(original, con({ tipos: [...original.tipos, 'POLIZA'] }))).toBe(true);
    expect(pideConfirmacion(original, con({ areas: [...original.areas, 'a-cumplimiento'] }))).toBe(true);
    expect(pideConfirmacion(original, con({ umbral: '90' }))).toBe(true);
    expect(pideConfirmacion(original, con({ muestreo: '5' }))).toBe(true);
  });

  it('ya en Automático, ajustes que no aumentan lo que se aprueba solo no avisan', () => {
    expect(pideConfirmacion(original, con({ tipos: ['RUT'] }))).toBe(false);
    expect(pideConfirmacion(original, con({ umbral: '98', muestreo: '30', tope: '100', espera: '30' }))).toBe(false);
  });
});
