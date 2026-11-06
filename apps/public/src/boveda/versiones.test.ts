import { describe, expect, it } from 'vitest';
import i18n from '../i18n';
import { extraerDeCorreo } from '../dev/correos';
import { detalleComparticion, mensajeDevolucion, sePuedeDescargar, sePuedeRetirar } from './versiones';

const t = i18n.t.bind(i18n);
const empresa = { id: 'e1', nombre: 'Constructora Norte S.A.S.', logoUrl: null };

describe('versiones de la bóveda', () => {
  it('explica por qué se devolvió una versión, con qué hacer', () => {
    expect(mensajeDevolucion({ motivoDevolucion: 'pdf-con-contenido-activo', detalleDevolucion: null }, t)).toMatch(/contenido activo/);
    expect(mensajeDevolucion({ motivoDevolucion: 'demasiadas-paginas', detalleDevolucion: null }, t)).toMatch(/más de 50 páginas/);
    expect(mensajeDevolucion({ motivoDevolucion: 'otro', detalleDevolucion: 'Detalle de la API' }, t)).toBe('Detalle de la API');
  });

  it('dice qué tiene cada empresa: la vigente, una anterior o nada', () => {
    const base = { vinculoId: 'v1', empresa, compartidaEn: '2026-11-06T12:00:00Z' };
    expect(detalleComparticion({ ...base, compartido: true, version: 3, versionActualCompartida: true }, t)).toMatch(/Tiene esta versión desde el 06\/11\/2026/);
    expect(detalleComparticion({ ...base, compartido: true, version: 2, versionActualCompartida: false }, t)).toMatch(/Tiene la versión 2/);
    expect(detalleComparticion({ ...base, compartido: false, version: null, versionActualCompartida: false }, t)).toMatch(/No la recibe/);
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
