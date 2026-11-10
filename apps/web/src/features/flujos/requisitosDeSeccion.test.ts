import { ErrorApi, type AreaDto, type FlujoVersionDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { areaNueva, borradorDesdeVersion, borradorVacio, requisitoDeSeccionNuevo, requisitoNuevo, requisitosADto, validarFlujo, type BorradorFlujo } from './editor';
import { errorDeApiEnRequisitos, seccionesQueRespalda } from './requisitosDeSeccion';

const AREAS: AreaDto[] = [{ id: 'juridica', empresaId: 'emp', codigo: 'JURIDICA', nombre: 'Jurídica', miembros: ['u1'], activa: true }];
const borrador = (requisitos: BorradorFlujo['requisitos']): BorradorFlujo => ({
  ...borradorVacio(),
  nombre: 'Proveedores',
  etapas: [{ clave: 'e1', areas: [areaNueva('juridica')] }],
  requisitos,
});
const errores = (b: BorradorFlujo) => validarFlujo(b, AREAS, false).map((e) => `${e.codigo}@${e.campo}`);

describe('requisitos de sección en el editor de flujos (FM-25)', () => {
  it('valida sección elegida, sin repetir y periodos solo en la financiera, entre 1 y 5', () => {
    expect(
      errores(
        borrador([
          requisitoDeSeccionNuevo(),
          requisitoDeSeccionNuevo('FICHA_GENERAL'),
          requisitoDeSeccionNuevo('FICHA_GENERAL'),
          { ...requisitoDeSeccionNuevo('FICHA_SST'), periodos: 2 },
          { ...requisitoDeSeccionNuevo('FICHA_FINANCIERA'), periodos: 6 },
        ]),
      ),
    ).toEqual(['seccion-invalida@requisito.0', 'requisito-duplicado@requisito.2', 'periodos-no-aplica@requisito.3.periodos', 'periodos-invalidos@requisito.4.periodos']);
    expect(errores(borrador([{ ...requisitoDeSeccionNuevo('FICHA_FINANCIERA'), periodos: 3, exigeSoporte: true }]))).toEqual([]);
  });

  it('va a la API sin tipo de documento, con periodos (1 por omisión en la financiera) y «Exige soporte»; y vuelve igual', () => {
    const dto = requisitosADto([
      { ...requisitoDeSeccionNuevo('FICHA_FINANCIERA'), exigeSoporte: true, areasQueRevisan: ['juridica'] },
      requisitoDeSeccionNuevo('FICHA_LEGAL'),
      requisitoNuevo('rut'),
    ]);
    expect(dto[0]).toEqual({
      id: null,
      tipoDocumentoTenantId: null,
      tipoDocumento: null,
      obligatorio: true,
      areasQueRevisan: ['juridica'],
      aprobadoresRequeridos: null,
      condicion: null,
      seccion: 'FICHA_FINANCIERA',
      periodos: 1,
      exigeSoporte: true,
    });
    expect(dto[1]).toMatchObject({ seccion: 'FICHA_LEGAL', periodos: null, exigeSoporte: false });
    // Un documento sigue yendo como antes, sin los campos de sección.
    expect(dto[2]).not.toHaveProperty('seccion');

    const version = { requisitos: dto.map((r, i) => ({ ...r, id: `r${String(i)}` })), etapas: [], politicaDevolucion: 'Inmediata', separacionDeFunciones: false } as unknown as FlujoVersionDto;
    const vuelta = borradorDesdeVersion({ codigo: 'P', nombre: 'P', tipoVinculo: 'PROVEEDOR' }, version).requisitos;
    expect(vuelta[0]).toMatchObject({ id: 'r0', seccion: 'FICHA_FINANCIERA', periodos: 1, exigeSoporte: true });
    expect(vuelta[2]).toMatchObject({ id: 'r2', seccion: null, tipoDocumentoTenantId: 'rut' });
  });

  it('un 422 de la API va al requisito de la posición que dice la API (los periodos, a su campo)', () => {
    const requisitos = [requisitoNuevo('rut'), requisitoDeSeccionNuevo('FICHA_FINANCIERA'), requisitoDeSeccionNuevo('FICHA_GENERAL'), requisitoDeSeccionNuevo('FICHA_FINANCIERA')];
    const error = (codigo: string, requisito?: number) => new ErrorApi(422, codigo, 'detalle', null, null, {}, requisito === undefined ? {} : { requisito });

    expect(errorDeApiEnRequisitos(error('requisito-duplicado', 3), requisitos)).toEqual({ codigo: 'requisito-duplicado', campo: 'requisito.3' });
    expect(errorDeApiEnRequisitos(error('periodos-invalidos', 3), requisitos)).toEqual({ codigo: 'periodos-invalidos', campo: 'requisito.3.periodos' });
    expect(errorDeApiEnRequisitos(error('seccion-invalida', 1), requisitos)).toEqual({ codigo: 'seccion-invalida', campo: 'requisito.1' });
    // Otros errores, sin posición o con una posición fuera del flujo, siguen arriba del formulario.
    expect(errorDeApiEnRequisitos(error('flujo-duplicado', 0), requisitos)).toBeNull();
    expect(errorDeApiEnRequisitos(error('requisito-invalido'), requisitos)).toBeNull();
    expect(errorDeApiEnRequisitos(error('requisito-duplicado', 9), requisitos)).toBeNull();
  });

  it('«Usar como soporte» de un documento va a la API tal como estaba (null = activo por omisión) y vuelve igual', () => {
    const dto = requisitosADto([
      requisitoNuevo('rut'),
      { ...requisitoNuevo('camara'), usarComoSoporte: false },
      { ...requisitoNuevo('cedula'), usarComoSoporte: true },
      requisitoDeSeccionNuevo('FICHA_GENERAL'),
    ]);
    expect(dto[0]).not.toHaveProperty('usarComoSoporte');
    expect(dto[1]).toMatchObject({ usarComoSoporte: false });
    expect(dto[2]).toMatchObject({ usarComoSoporte: true });
    expect(dto[3]).not.toHaveProperty('usarComoSoporte');

    const version = { requisitos: dto.map((r, i) => ({ ...r, id: `r${String(i)}` })), etapas: [], politicaDevolucion: 'Inmediata', separacionDeFunciones: false } as unknown as FlujoVersionDto;
    const vuelta = borradorDesdeVersion({ codigo: 'P', nombre: 'P', tipoVinculo: 'PROVEEDOR' }, version).requisitos;
    expect(vuelta.map((r) => r.usarComoSoporte)).toEqual([null, false, true, null]);
  });

  it('cada tipo respalda las secciones del mapa de la plataforma', () => {
    expect(seccionesQueRespalda('RUT')).toEqual(['FICHA_GENERAL', 'FICHA_TRIBUTARIA', 'FICHA_LEGAL']);
    expect(seccionesQueRespalda('camara_comercio')).toEqual(['FICHA_GENERAL', 'FICHA_LEGAL']);
    expect(seccionesQueRespalda('ESTADOS_FINANCIEROS')).toEqual(['FICHA_FINANCIERA']);
    expect(seccionesQueRespalda(null)).toEqual([]);
    expect(seccionesQueRespalda('SARLAFT')).toEqual([]);
  });
});
