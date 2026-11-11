import type { FlujoVersionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { borradorDesdeVersion, borradorVacio, firmaBorrador, requisitoDeSeccionNuevo, requisitoNuevo, requisitosADto, validarFlujo } from './editor';
import { validarRequisitoDeSeccion } from './requisitosDeSeccion';
import { avisosDeVigencia, validarVigenciaDelVinculo, vigenciaADto, VIGENCIA_POR_DEFECTO } from './vigencia';

const conSeccion = (cambios: Partial<ReturnType<typeof requisitoDeSeccionNuevo>> = {}) => ({ ...requisitoDeSeccionNuevo('FICHA_GENERAL'), ...cambios });

describe('vigencia periódica del vínculo en el editor de flujos (VV-1)', () => {
  it('por omisión no vence por tiempo: 0 días, avisos a 30, 15 y 5 y sin gracia', () => {
    const borrador = borradorVacio();
    expect(VIGENCIA_POR_DEFECTO).toEqual({ dias: 0, avisos: [30, 15, 5], gracia: 0 });
    expect(vigenciaADto(borrador)).toEqual({ dias: 0, diasAviso: [30, 15, 5], diasGracia: 0 });
  });

  it('los avisos se leen separados por coma, espacio o punto y coma, sin repetir y de mayor a menor', () => {
    expect(avisosDeVigencia('5, 30 15;15')).toEqual([30, 15, 5]);
    expect(avisosDeVigencia('')).toEqual([]);
  });

  it('lleva a la API lo que escribió la persona; lo vacío es 0', () => {
    expect(vigenciaADto({ vigenciaDias: '365', vigenciaAvisos: '30, 5', vigenciaGracia: '10' })).toEqual({ dias: 365, diasAviso: [30, 5], diasGracia: 10 });
    expect(vigenciaADto({ vigenciaDias: '', vigenciaAvisos: '', vigenciaGracia: '' })).toEqual({ dias: 0, diasAviso: [], diasGracia: 0 });
  });

  it('con vigencia, el flujo debe pedir al menos una sección (lo que se ratifica al vencer)', () => {
    const base = { vigenciaDias: '365', vigenciaAvisos: '30', vigenciaGracia: '0' };
    expect(validarVigenciaDelVinculo({ ...base, requisitos: [requisitoNuevo('rut')] })).toEqual([{ codigo: 'vigencia-sin-secciones', campo: 'vigencia.dias' }]);
    expect(validarVigenciaDelVinculo({ ...base, requisitos: [requisitoNuevo('rut'), conSeccion()] })).toEqual([]);
    // Sin vigencia periódica no hace falta ninguna.
    expect(validarVigenciaDelVinculo({ vigenciaDias: '0', vigenciaAvisos: '30', vigenciaGracia: '0', requisitos: [] })).toEqual([]);
  });

  it('valida los rangos como el dominio: 0–3650 días, hasta 5 avisos menores que la vigencia y gracia de 0 a 365', () => {
    const requisitos = [conSeccion()];
    const errores = (vigenciaDias: string, vigenciaAvisos: string, vigenciaGracia: string) =>
      validarVigenciaDelVinculo({ vigenciaDias, vigenciaAvisos, vigenciaGracia, requisitos }).map((e) => `${e.codigo}@${e.campo}`);
    expect(errores('-1', '', '0')).toEqual(['vigencia-vinculo-invalida@vigencia.dias']);
    expect(errores('3651', '', '0')).toEqual(['vigencia-vinculo-invalida@vigencia.dias']);
    expect(errores('abc', '', '0')).toEqual(['vigencia-vinculo-invalida@vigencia.dias']);
    expect(errores('100', '', '-1')).toEqual(['gracia-vigencia-invalida@vigencia.gracia']);
    expect(errores('100', '', '366')).toEqual(['gracia-vigencia-invalida@vigencia.gracia']);
    expect(errores('30', '30', '0')).toEqual(['aviso-vigencia-invalido@vigencia.avisos']);
    expect(errores('365', '1, 2, 3, 4, 5, 6', '0')).toEqual(['aviso-vigencia-invalido@vigencia.avisos']);
    expect(errores('365', '30, 0', '0')).toEqual(['aviso-vigencia-invalido@vigencia.avisos']);
    expect(errores('365', '30, 15, 5', '15')).toEqual([]);
  });

  it('validarFlujo la incluye entre sus errores', () => {
    const borrador = { ...borradorVacio(), nombre: 'F', vigenciaDias: '100' };
    expect(validarFlujo(borrador, [], false).map((e) => e.codigo)).toContain('vigencia-sin-secciones');
  });

  it('cambiar la vigencia cuenta como cambio sin guardar', () => {
    const borrador = borradorVacio();
    expect(firmaBorrador({ ...borrador, vigenciaDias: '365' })).not.toBe(firmaBorrador(borrador));
    expect(firmaBorrador({ ...borrador, vigenciaGracia: '10' })).not.toBe(firmaBorrador(borrador));
  });

  it('lleva al editor la vigencia de una versión del servidor y, sin ella (versión anterior), la de por omisión', () => {
    const version = {
      id: 'v', version: 2, estado: 'Borrador', etapas: [], politicaDevolucion: 'Inmediata', separacionDeFunciones: false, creadaEn: '2026-11-10T00:00:00Z',
      vigenciaVinculo: { dias: 365, diasAviso: [30, 5], diasGracia: 10 },
    } as unknown as FlujoVersionDto;
    const base = { codigo: 'F', nombre: 'F', tipoVinculo: 'CLIENTE' };
    const borrador = borradorDesdeVersion(base, version);
    expect([borrador.vigenciaDias, borrador.vigenciaAvisos, borrador.vigenciaGracia]).toEqual(['365', '30, 5', '10']);
    const anterior = borradorDesdeVersion(base, { ...version, vigenciaVinculo: undefined });
    expect([anterior.vigenciaDias, anterior.vigenciaAvisos, anterior.vigenciaGracia]).toEqual(['0', '30, 15, 5', '0']);
  });
});

describe('vigencia propia y plazo de revalidación de una sección (VV-1, RV-4)', () => {
  const errores = (cambios: Partial<ReturnType<typeof requisitoDeSeccionNuevo>>) => validarRequisitoDeSeccion(conSeccion(cambios), 'requisito.0', new Set()).map((e) => `${e.codigo}@${e.campo}`);

  it('vacíos no validan nada; con valor, de 1 a 3650 días de vigencia y de 1 a 365 de revalidación', () => {
    expect(errores({})).toEqual([]);
    expect(errores({ vigenciaDias: 180, diasRevalidacion: 7 })).toEqual([]);
    expect(errores({ vigenciaDias: 0 })).toEqual(['vigencia-seccion-invalida@requisito.0.vigencia']);
    expect(errores({ vigenciaDias: 3651 })).toEqual(['vigencia-seccion-invalida@requisito.0.vigencia']);
    expect(errores({ diasRevalidacion: 0 })).toEqual(['dias-revalidacion-invalidos@requisito.0.revalidacion']);
    expect(errores({ diasRevalidacion: 366 })).toEqual(['dias-revalidacion-invalidos@requisito.0.revalidacion']);
  });

  it('van a la API solo en la sección; un documento no los lleva', () => {
    const [seccion, documento] = requisitosADto([conSeccion({ vigenciaDias: 180, diasRevalidacion: 7 }), requisitoNuevo('rut')]);
    expect(seccion).toMatchObject({ seccion: 'FICHA_GENERAL', vigenciaDias: 180, diasRevalidacion: 7 });
    expect(documento).not.toHaveProperty('vigenciaDias');
    expect(documento).not.toHaveProperty('diasRevalidacion');
  });
});
