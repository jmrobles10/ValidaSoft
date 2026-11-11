import type { SesionTerceroDto } from '@validasoft/api-client';
import { describe, expect, it } from 'vitest';
import { esComponente, evaluarSesion, reclamosEmbebidos, rutaDelComponente } from './embebido';

const ANFITRION = 'https://anfitrion.ficticio.test';
const AHORA = Date.UTC(2026, 10, 11, 12, 0, 0);

/** Un JWT con la carga dada (la firma no se verifica aquí: la verifica la API). */
function jwt(carga: Record<string, unknown>): string {
  const codificar = (valor: unknown) => Buffer.from(JSON.stringify(valor)).toString('base64url');
  return `${codificar({ alg: 'HS256', typ: 'JWT' })}.${codificar(carga)}.firma`;
}

const embebido = (cambios: Record<string, unknown> = {}) =>
  jwt({ embebido: 'true', sistema: 'ERPX', origen: ANFITRION, componente: 'boveda', exp: Math.floor(AHORA / 1000) + 600, ...cambios });

describe('reclamos de la sesión embebida', () => {
  it('lee origen, componente, sistema y vencimiento del token embebido', () => {
    expect(reclamosEmbebidos(embebido())).toEqual({ origen: ANFITRION, componente: 'boveda', sistema: 'ERPX', expira: AHORA + 600_000 });
  });

  it.each([
    ['un token de una sesión normal (sin la marca embebido)', jwt({ origen: ANFITRION, componente: 'boveda' })],
    ['sin origen', jwt({ embebido: 'true', componente: 'boveda' })],
    ['sin componente', jwt({ embebido: 'true', origen: ANFITRION })],
    ['algo que no es un JWT', 'no-es-un-token'],
    ['una carga que no es JSON', 'a.b.c'],
  ])('rechaza %s', (_caso, token) => {
    expect(reclamosEmbebidos(token)).toBeNull();
  });
});

describe('evaluar si se acepta el token que llegó del contenedor', () => {
  const permitidos = [ANFITRION, 'https://otro.ficticio.test'];

  it('lo acepta cuando viene del origen permitido que es el del contenedor y el token es para ese origen', () => {
    const veredicto = evaluarSesion(embebido(), ANFITRION, permitidos, ANFITRION, AHORA);
    expect(veredicto).toMatchObject({ aceptada: true, reclamos: { componente: 'boveda', sistema: 'ERPX' } });
    // Si no se conoce el contenedor (p. ej. sin ancestorOrigins ni referrer) vale el origen del mensaje permitido.
    expect(evaluarSesion(embebido(), ANFITRION, permitidos, null, AHORA).aceptada).toBe(true);
  });

  it('no lo acepta de un origen que no es de los permitidos, ni de uno distinto al del contenedor', () => {
    expect(evaluarSesion(embebido({ origen: 'https://intruso.test' }), 'https://intruso.test', permitidos, null, AHORA)).toEqual({ aceptada: false, motivo: 'origen-no-permitido' });
    expect(evaluarSesion(embebido(), ANFITRION, permitidos, 'https://otro.ficticio.test', AHORA)).toEqual({ aceptada: false, motivo: 'origen-no-permitido' });
    expect(evaluarSesion(embebido(), 'null', permitidos, null, AHORA)).toEqual({ aceptada: false, motivo: 'origen-no-permitido' });
  });

  it('no lo acepta si el token se emitió para otro origen, no es embebido o su componente es desconocido', () => {
    expect(evaluarSesion(embebido({ origen: 'https://otro.ficticio.test' }), ANFITRION, permitidos, ANFITRION, AHORA)).toEqual({ aceptada: false, motivo: 'token-ajeno' });
    expect(evaluarSesion(jwt({ origen: ANFITRION }), ANFITRION, permitidos, ANFITRION, AHORA)).toEqual({ aceptada: false, motivo: 'token-ajeno' });
    expect(evaluarSesion(embebido({ componente: 'bandeja' }), ANFITRION, permitidos, ANFITRION, AHORA)).toEqual({ aceptada: false, motivo: 'token-ajeno' });
  });

  it('no lo acepta vencido', () => {
    expect(evaluarSesion(embebido({ exp: Math.floor(AHORA / 1000) - 1 }), ANFITRION, permitidos, ANFITRION, AHORA)).toEqual({ aceptada: false, motivo: 'token-vencido' });
  });
});

describe('a dónde lleva cada componente', () => {
  const sesion = (vinculos: string[]): SesionTerceroDto => ({
    accessToken: 't',
    expira: '2026-11-11T12:10:00Z',
    terceroId: 'te1',
    otpVerificado: true,
    vinculos: vinculos.map((id) => ({ id, empresa: { id: `e-${id}`, nombre: 'Norte', logoUrl: null }, estado: 'Activo' })),
  });

  it('la bóveda y la ficha van a su pantalla; el expediente, a la revisión abierta o, si no hay, a la empresa', () => {
    expect(rutaDelComponente('boveda', sesion(['v1']), null)).toBe('/boveda');
    expect(rutaDelComponente('ficha', sesion(['v1']), null)).toBe('/ficha');
    expect(rutaDelComponente('expediente', sesion(['v1']), 'x9')).toBe('/expediente/x9');
    expect(rutaDelComponente('expediente', sesion(['v1']), null)).toBe('/empresas/v1');
    expect(rutaDelComponente('expediente', sesion([]), null)).toBe('/empresas');
  });

  it('solo existen los tres componentes del contrato', () => {
    expect(['boveda', 'expediente', 'ficha', 'bandeja', undefined].map((c) => esComponente(c))).toEqual([true, true, true, false, false]);
  });
});
