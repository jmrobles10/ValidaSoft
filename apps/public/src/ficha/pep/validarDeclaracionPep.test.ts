import { describe, expect, it } from 'vitest';
import type { DeclaracionPep } from '../tipos';
import type { DatosBasicos } from './declaracion';
import { hayErrores, hoyIso, validarDeclaracionPep } from './validarDeclaracionPep';

const HOY = '2026-11-12';

const NATURAL: DatosBasicos = {
  nombre: 'Ana Gómez',
  tipoIdentificacion: 'CC',
  numero: '1020304050',
  relacion: 'Proveedor',
  relacionOtra: null,
  direccionFija: 'Carrera 7 # 32-16',
  nacionalidadFija: null,
  esNatural: true,
  soloNombrePropio: true,
};

const PERSONA: DatosBasicos = { ...NATURAL, direccionFija: null, esNatural: false, soloNombrePropio: false };

/** Una declaración que cumple todo (natural, vigente, sin cuentas ni vínculo). */
const completa = (cambios: Partial<DeclaracionPep> = {}): DeclaracionPep => ({
  nacionalidad: 'CO',
  cargo: 'Alcalde',
  institucion: 'Alcaldía de Soacha',
  pais: 'CO',
  fecha_vinculacion: '2020-01-01',
  vigente: true,
  cuentas_exterior: false,
  vinculo_con_pep: { tiene: false },
  acepta_juramento: true,
  firma_en_calidad: 'NombrePropio',
  firma_png: 'iVBORw0KGgo=',
  ...cambios,
});

const validar = (d: DeclaracionPep, basicos = NATURAL) => validarDeclaracionPep(d, basicos, HOY);

describe('validarDeclaracionPep', () => {
  it('una declaración completa no tiene errores', () => {
    expect(validar(completa())).toEqual({});
    expect(hayErrores(validar(completa()))).toBe(false);
  });

  it('sin nada diligenciado marca cada obligatorio', () => {
    expect(validar({}, NATURAL)).toEqual({
      nacionalidad: 'requerido',
      cargo: 'requerido',
      institucion: 'requerido',
      pais: 'requerido',
      fecha_vinculacion: 'requerido',
      fecha_desvinculacion: 'requerido',
      cuentas_exterior: 'requerido',
      'vinculo_con_pep.tiene': 'requerido',
      acepta_juramento: 'juramento',
      firma_png: 'firma',
    });
  });

  it('la persona de la información legal también escribe su dirección; la natural la trae fija', () => {
    expect(validar(completa({ nacionalidad: 'CO' }), PERSONA)).toEqual({ direccion: 'requerido' });
    expect(validar(completa({ direccion: 'Calle 1 # 2-3' }), PERSONA)).toEqual({});
  });

  it('con nacionalidad fija ya no se pide', () => {
    expect(validar(completa({ nacionalidad: null }), { ...NATURAL, nacionalidadFija: 'CO' })).toEqual({});
  });

  it.each([
    ['vigente: sin fecha de desvinculación', completa({ vigente: true, fecha_desvinculacion: null }), {}],
    ['no vigente: pide la desvinculación', completa({ vigente: false }), { fecha_desvinculacion: 'requerido' }],
    ['vinculación futura', completa({ fecha_vinculacion: '2026-11-13' }), { fecha_vinculacion: 'fechaFutura' }],
    ['desvinculación futura', completa({ vigente: false, fecha_desvinculacion: '2026-11-13' }), { fecha_desvinculacion: 'fechaFutura' }],
    ['desvinculación anterior a la vinculación', completa({ vigente: false, fecha_desvinculacion: '2019-12-31' }), { fecha_desvinculacion: 'finAnterior' }],
    ['desvinculación el mismo día de la vinculación', completa({ vigente: false, fecha_desvinculacion: '2020-01-01' }), {}],
  ])('periodo: %s', (_caso, d, esperado) => {
    expect(validar(d)).toEqual(esperado);
  });

  it('cuentas en el exterior: Sí pide al menos un país; No no pide ninguno', () => {
    expect(validar(completa({ cuentas_exterior: true, paises_cuentas_exterior: [] }))).toEqual({ paises_cuentas_exterior: 'paisesRequeridos' });
    expect(validar(completa({ cuentas_exterior: true, paises_cuentas_exterior: ['US'] }))).toEqual({});
    expect(validar(completa({ cuentas_exterior: false, paises_cuentas_exterior: [] }))).toEqual({});
  });

  it('el cónyuge es todo o nada', () => {
    expect(validar(completa({ conyuge: { nombre: '', tipo_identificacion: null, numero: null } }))).toEqual({});
    expect(validar(completa({ conyuge: { nombre: 'Luis Pérez', tipo_identificacion: 'CC', numero: '123' } }))).toEqual({});
    expect(validar(completa({ conyuge: { nombre: 'Luis Pérez' } }))).toEqual({ 'conyuge.tipo_identificacion': 'conyugeIncompleto', 'conyuge.numero': 'conyugeIncompleto' });
  });

  it('cada familiar es todo o nada y la fila vacía se descarta', () => {
    expect(validar(completa({ familiares: [{}, { nombre: '  ' }] }))).toEqual({});
    expect(validar(completa({ familiares: [{ nombre: 'Luis', parentesco: 'Hermano', tipo_identificacion: 'CC', numero: '5' }] }))).toEqual({});
    expect(validar(completa({ familiares: [{ nombre: 'Luis' }, {}, { parentesco: 'Hijo' }] }))).toEqual({
      'familiares[0].parentesco': 'filaIncompleta',
      'familiares[0].tipo_identificacion': 'filaIncompleta',
      'familiares[0].numero': 'filaIncompleta',
      'familiares[2].nombre': 'filaIncompleta',
      'familiares[2].tipo_identificacion': 'filaIncompleta',
      'familiares[2].numero': 'filaIncompleta',
    });
  });

  it('cada sociedad es todo o nada y «Otra» pide «¿Cuál?»', () => {
    expect(validar(completa({ sociedades: [{ nombre: 'Inversiones SAS', identificacion: '900', calidad: 'Socio' }] }))).toEqual({});
    expect(validar(completa({ sociedades: [{ nombre: 'Inversiones SAS', identificacion: '900', calidad: 'Otra' }] }))).toEqual({ 'sociedades[0].calidad_otra': 'filaIncompleta' });
    expect(validar(completa({ sociedades: [{ nombre: 'Inversiones SAS' }] }))).toEqual({ 'sociedades[0].identificacion': 'filaIncompleta', 'sociedades[0].calidad': 'filaIncompleta' });
  });

  it('admite hasta 30 familiares y 30 sociedades, no más', () => {
    const familiar = { nombre: 'Luis', parentesco: 'Primo', tipo_identificacion: 'CC', numero: '1' };
    const sociedad = { nombre: 'X SAS', identificacion: '9', calidad: 'Socio' as const };
    expect(validar(completa({ familiares: Array.from({ length: 30 }, () => familiar) }))).toEqual({});
    expect(validar(completa({ familiares: Array.from({ length: 31 }, () => familiar) }))).toEqual({ familiares: 'maxFilas' });
    expect(validar(completa({ sociedades: Array.from({ length: 31 }, () => sociedad) }))).toEqual({ sociedades: 'maxFilas' });
  });

  it('el vínculo con otra PEP: con Sí pide sus datos y su periodo; con No nada más', () => {
    expect(validar(completa({ vinculo_con_pep: { tiene: true } }))).toEqual({
      'vinculo_con_pep.nombre': 'requerido',
      'vinculo_con_pep.parentesco': 'requerido',
      'vinculo_con_pep.cargo_institucion': 'requerido',
      'vinculo_con_pep.fecha_vinculacion': 'requerido',
      'vinculo_con_pep.fecha_desvinculacion': 'requerido',
    });
    expect(
      validar(completa({ vinculo_con_pep: { tiene: true, nombre: 'Rosa', parentesco: 'Madre', cargo_institucion: 'Senadora', fecha_vinculacion: '2018-05-05', vigente: true } })),
    ).toEqual({});
  });

  it('el representante legal debe escribir quién firma; a nombre propio no', () => {
    expect(validar(completa({ direccion: 'Calle 1', firma_en_calidad: 'RepresentanteLegal' }), PERSONA)).toEqual({
      firmante_nombre: 'requerido',
      firmante_tipo_identificacion: 'requerido',
      firmante_numero: 'requerido',
    });
    expect(
      validar(completa({ direccion: 'Calle 1', firma_en_calidad: 'RepresentanteLegal', firmante_nombre: 'Pedro', firmante_tipo_identificacion: 'CC', firmante_numero: '9' }), PERSONA),
    ).toEqual({});
    expect(validar(completa({ direccion: 'Calle 1', firma_en_calidad: 'NombrePropio' }), PERSONA)).toEqual({});
  });

  it('la persona natural nunca escribe firmante aunque el borrador diga lo contrario', () => {
    expect(validar(completa({ firma_en_calidad: 'RepresentanteLegal' }))).toEqual({});
  });

  it('exige el juramento y la firma', () => {
    expect(validar(completa({ acepta_juramento: false }))).toEqual({ acepta_juramento: 'juramento' });
    expect(validar(completa({ firma_png: null }))).toEqual({ firma_png: 'firma' });
  });

  it('rechaza textos más largos que los del servidor', () => {
    expect(validar(completa({ cargo: 'x'.repeat(201) }))).toEqual({ cargo: 'largo' });
  });
});

describe('hoyIso', () => {
  it('escribe el día en la zona de Bogotá como aaaa-mm-dd', () => {
    expect(hoyIso(new Date('2026-11-12T03:00:00Z'))).toBe('2026-11-11');
    expect(hoyIso(new Date('2026-11-12T15:00:00Z'))).toBe('2026-11-12');
  });
});
