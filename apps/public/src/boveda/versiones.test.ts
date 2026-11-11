import type { ComparticionPublicaDto, VigenciaDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import { extraerDeCorreo } from '../dev/correos';
import { detalleComparticion, empresaConLaFechaMasProxima, mensajeDevolucion, sePuedeDescargar, sePuedeRetirar, venceEnCabecera } from './versiones';

const t = i18n.t.bind(i18n);
const empresa = { id: 'e1', nombre: 'Constructora Norte S.A.S.', logoUrl: null };

const vigencia = (vigenteHasta: string, base = 'FechaExpedicion', estado = 'Vigente'): VigenciaDto => ({
  vigenteHasta,
  base,
  estado,
  diasAviso: [],
  diasGracia: 0,
  finDeGracia: vigenteHasta,
  vencidaEn: null,
});

const fila = (cambios: Partial<ComparticionPublicaDto>): ComparticionPublicaDto => ({
  vinculoId: 'v1',
  empresa,
  compartido: true,
  version: 7,
  versionActualCompartida: true,
  compartidaEn: '2026-11-06T12:00:00Z',
  situacion: 'TieneVigente',
  otraVersion: null,
  ...cambios,
});

describe('versiones de la bóveda', () => {
  it('explica por qué se devolvió una versión, con qué hacer', () => {
    expect(mensajeDevolucion({ motivoDevolucion: 'pdf-con-contenido-activo', detalleDevolucion: null }, t)).toMatch(/contenido activo/);
    expect(mensajeDevolucion({ motivoDevolucion: 'demasiadas-paginas', detalleDevolucion: null }, t)).toMatch(/más de 50 páginas/);
    expect(mensajeDevolucion({ motivoDevolucion: 'otro', detalleDevolucion: 'Detalle de la API' }, t)).toBe('Detalle de la API');
  });

  it('dice qué tiene cada empresa y qué falta, con una sola palabra para la versión que vale hoy', () => {
    expect(detalleComparticion(fila({}), t)).toBe('Tiene la versión 7, la vigente.');
    expect(detalleComparticion(fila({ situacion: 'AproboVigente' }), t)).toBe('Aprobó la versión 7, la vigente.');
    expect(detalleComparticion(fila({ situacion: 'Revisando' }), t)).toBe('Revisando la versión 7.');
    expect(detalleComparticion(fila({ situacion: 'Observada' }), t)).toBe('Te pidió corregir la versión 7.');
    expect(detalleComparticion(fila({ situacion: 'RevalidaRetirada', otraVersion: 8 }), t)).toBe(
      'Aprobó la versión 8, que retiraste. Debe revisar la versión 7: revalidación abierta.',
    );
    expect(detalleComparticion(fila({ situacion: 'Revalida', otraVersion: 6 }), t)).toBe('Aprobó la versión 6. Debe revisar la versión 7, la vigente: revalidación abierta.');
    expect(detalleComparticion(fila({ situacion: 'PorRevalidar', otraVersion: 8 }), t)).toBe(
      'Aprobó la versión 8, que retiraste. Tiene la 7, la vigente: tendrá que revalidarla.',
    );
    expect(detalleComparticion(fila({ situacion: 'TieneOtra', version: 6, versionActualCompartida: false, otraVersion: 7 }), t)).toBe(
      'Tiene la versión 6. No recibe la 7, la vigente: hoy no se la exige.',
    );
    expect(detalleComparticion(fila({ situacion: 'SinVigente', compartido: false, version: null, versionActualCompartida: false, otraVersion: 8 }), t)).toBe(
      'Tenía la versión 8, que retiraste. Sube una nueva para que la reciba.',
    );
    expect(detalleComparticion(fila({ situacion: 'NoRecibe', compartido: false, version: null, versionActualCompartida: false }), t)).toMatch(/No la recibe/);
  });

  it('nunca dice «la nueva» ni «actual»: la versión es una sola y se llama vigente', () => {
    const textos = ['TieneVigente', 'AproboVigente', 'Revisando', 'RevalidaRetirada', 'Revalida', 'PorRevalidar', 'TieneOtra', 'SinVigente', 'NoRecibe']
      .map((situacion) => detalleComparticion(fila({ situacion, otraVersion: 8 }), t))
      .join(' ');
    expect(textos).not.toMatch(/la nueva|cuando la exija|actual\b/i);
  });

  it('una situación que la API agregue después se lee por lo que ya se sabe', () => {
    expect(detalleComparticion(fila({ situacion: 'Nueva' }), t)).toBe('Tiene la versión 7, la vigente.');
    expect(detalleComparticion(fila({ situacion: 'Nueva', compartido: false, version: null }), t)).toMatch(/No la recibe/);
  });

  it('el vencimiento propio de la empresa solo se dice si difiere de la cabecera y siempre con su porqué', () => {
    const delaCabecera = fila({ vigencia: vigencia('2027-11-09') });
    expect(detalleComparticion(delaCabecera, t, '2027-11-09')).toBe('Tiene la versión 7, la vigente.');

    const distinta = fila({ vigencia: vigencia('2027-11-10', 'FechaExpedicion') });
    expect(detalleComparticion(distinta, t, '2027-11-09')).toBe(
      'Tiene la versión 7, la vigente. Vence el 10/11/2027: su regla de vigencia cuenta desde la fecha de expedición.',
    );
    expect(detalleComparticion(fila({ vigencia: vigencia('2027-11-10', 'FechaCarga') }), t, '2027-11-09')).toMatch(/desde la fecha de carga\.$/);

    // Con otra versión (no recibe la vigente) la fecha sería de una versión que no es la de la cabecera: no se mezcla.
    expect(detalleComparticion(fila({ situacion: 'TieneOtra', versionActualCompartida: false, otraVersion: 8, vigencia: vigencia('2027-01-01') }), t, '2027-11-09')).not.toMatch(
      /Vence/,
    );
    // Lo vencido o en gracia siempre se dice.
    expect(detalleComparticion(fila({ vigencia: vigencia('2026-10-01', 'FechaCarga', 'Vencida') }), t, '2026-10-01')).toMatch(/Venció el 01\/10\/2026\.$/);
  });

  it('si las fechas difieren, la cabecera dice la más próxima y de quién es', () => {
    const norte = fila({ vigencia: vigencia('2027-11-10') });
    const sur = fila({ vinculoId: 'v2', empresa: { id: 'e2', nombre: 'Constructora Sur', logoUrl: null }, vigencia: vigencia('2027-11-09') });
    expect(empresaConLaFechaMasProxima({ porEmpresa: [norte, sur] })).toBe('Constructora Sur');
    expect(empresaConLaFechaMasProxima({ porEmpresa: [norte, { ...sur, vigencia: vigencia('2027-11-10') }] })).toBeNull();
    expect(empresaConLaFechaMasProxima({ porEmpresa: [norte] })).toBeNull();
    expect(venceEnCabecera({ versionActual: null })).toBeNull();
  });

  it('solo se retira o descarga lo vigente o superado', () => {
    expect(sePuedeRetirar({ estado: 'Actual' })).toBe(true);
    expect(sePuedeRetirar({ estado: 'Devuelta' })).toBe(false);
    expect(sePuedeDescargar({ estado: 'Superada' })).toBe(true);
    expect(sePuedeDescargar({ estado: 'Retirada' })).toBe(false);
  });
});

describe('correos de desarrollo', () => {
  it('saca la ruta del enlace de la invitación y el código del correo de OTP', () => {
    expect(extraerDeCorreo('Abre este enlace:\nhttp://localhost:5181/e/kKeR7NC_aL-4\n\nVence el 9 de noviembre de 2026')).toEqual({ enlace: '/e/kKeR7NC_aL-4', codigo: null });
    expect(extraerDeCorreo('Tu código es 481729. Vence en 10 minutos.')).toEqual({ enlace: null, codigo: '481729' });
  });
});
