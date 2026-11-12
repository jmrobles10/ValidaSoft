import { describe, expect, it } from 'vitest';
import type { DeclaracionPep, PersonaRelacionada } from '../tipos';
import { datosBasicos, declaracionInicial, declaracionParaEntregar, faltasBasicas, relacionDeRol } from './declaracion';

const SOCIO: PersonaRelacionada = { rol: 'Socio', tipo_identificacion: 'CC', numero: '79555111', nombres: 'Jorge Iván', apellidos: 'Castro Pinto', nacionalidad: 'CO' };

describe('relación con la empresa', () => {
  it.each([
    ['RepresentanteLegal', 'Proveedor', null],
    ['Suplente', 'Proveedor', null],
    ['Apoderado', 'Proveedor', null],
    ['Socio', 'Socio', null],
    ['JuntaDirectiva', 'Socio', null],
    ['RevisorFiscal', 'Otro', 'Revisor fiscal'],
    ['Contador', 'Otro', 'Contador'],
  ] as const)('%s ⇒ %s', (rol, relacion, otra) => {
    expect(relacionDeRol(rol)).toEqual({ relacion, otra });
  });
});

describe('datos básicos', () => {
  it('la persona natural exige nombre y dirección de su información general', () => {
    expect(faltasBasicas({ tipo: 'natural', nombres: '', apellidos: null, tipoIdentificacion: 'CC', numero: '1', direccion: ' ' })).toEqual(['nombre', 'direccion']);
    expect(faltasBasicas({ tipo: 'natural', nombres: 'Ana', apellidos: 'Gómez', tipoIdentificacion: null, numero: null, direccion: 'Calle 1' })).toEqual([]);
  });

  it('una persona exige rol, tipo y número de identificación y nombre', () => {
    expect(faltasBasicas({ tipo: 'persona', persona: {} })).toEqual(['rol', 'tipoIdentificacion', 'numero', 'nombre']);
    expect(faltasBasicas({ tipo: 'persona', persona: SOCIO })).toEqual([]);
    // Una razón social (NIT) no lleva apellidos.
    expect(faltasBasicas({ tipo: 'persona', persona: { rol: 'Socio', tipo_identificacion: 'NIT', numero: '800', nombres: 'Inversiones SAS' } })).toEqual([]);
  });

  it('arma la sección 1 de una persona con su relación y su nacionalidad registrada', () => {
    expect(datosBasicos({ tipo: 'persona', persona: { ...SOCIO, rol: 'Contador' } })).toMatchObject({
      nombre: 'Jorge Iván Castro Pinto',
      tipoIdentificacion: 'CC',
      numero: '79555111',
      relacion: 'Otro',
      relacionOtra: 'Contador',
      nacionalidadFija: 'CO',
      direccionFija: null,
      soloNombrePropio: false,
    });
  });
});

describe('al abrir y al confirmar', () => {
  const basicos = datosBasicos({ tipo: 'persona', persona: SOCIO });

  it('el borrador abre con el juramento sin marcar aunque la declaración previa lo tuviera', () => {
    const previa: DeclaracionPep = { cargo: 'Alcalde', acepta_juramento: true, firma_en_calidad: 'RepresentanteLegal' };
    expect(declaracionInicial(basicos, previa)).toMatchObject({ cargo: 'Alcalde', acepta_juramento: false, firma_en_calidad: 'RepresentanteLegal', nacionalidad: 'CO' });
    expect(declaracionInicial(basicos, null)).toMatchObject({ firma_en_calidad: 'NombrePropio', acepta_juramento: false });
  });

  it('entrega la declaración sin sellos, sin filas vacías y sin lo que no aplica', () => {
    const entregada = declaracionParaEntregar(
      {
        cargo: 'Alcalde',
        vigente: true,
        fecha_desvinculacion: '2024-01-01',
        cuentas_exterior: false,
        paises_cuentas_exterior: ['US'],
        conyuge: { nombre: ' ' },
        familiares: [{}, { nombre: 'Luis', parentesco: 'Hijo', tipo_identificacion: 'CC', numero: '1' }],
        sociedades: [{ nombre: 'X SAS', identificacion: '9', calidad: 'Socio', calidad_otra: 'resto' }, {}],
        vinculo_con_pep: { tiene: false, nombre: 'resto' },
        firma_en_calidad: 'NombrePropio',
        firmante_nombre: 'resto',
        firma_png: 'AAA=',
        firma_huella: 'h',
        fecha_firma: '2026-11-01',
        firma_ip: '10.0.0.1',
      },
      basicos,
    );
    expect(entregada).toEqual({
      cargo: 'Alcalde',
      vigente: true,
      fecha_desvinculacion: null,
      cuentas_exterior: false,
      paises_cuentas_exterior: null,
      conyuge: null,
      familiares: [{ nombre: 'Luis', parentesco: 'Hijo', tipo_identificacion: 'CC', numero: '1' }],
      sociedades: [{ nombre: 'X SAS', identificacion: '9', calidad: 'Socio', calidad_otra: null }],
      vinculo_con_pep: { tiene: false },
      firma_en_calidad: 'NombrePropio',
      firmante_nombre: null,
      firmante_tipo_identificacion: null,
      firmante_numero: null,
      firma_png: 'AAA=',
      relacion_empresa: 'Socio',
      relacion_otra: null,
      nombre: 'Jorge Iván Castro Pinto',
      tipo_identificacion: 'CC',
      numero: '79555111',
      direccion: null,
      nacionalidad: 'CO',
    });
  });

  it('el representante legal conserva a quien firma', () => {
    const entregada = declaracionParaEntregar({ firma_en_calidad: 'RepresentanteLegal', firmante_nombre: 'Pedro', firmante_tipo_identificacion: 'CC', firmante_numero: '9' }, basicos);
    expect(entregada).toMatchObject({ firma_en_calidad: 'RepresentanteLegal', firmante_nombre: 'Pedro', firmante_tipo_identificacion: 'CC', firmante_numero: '9' });
  });
});
