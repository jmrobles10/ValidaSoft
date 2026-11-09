import { configurarCliente, ErrorApi, ErrorDeRed, reiniciarCliente, type SesionTerceroDto } from '@validasoft/api-client';
import { afterEach, describe, expect, it, vi } from 'vitest';
import { canjearUnaVez, destinoTrasSesion, falloDeEnlace, interpretarCanje, intentosRestantesDe, normalizarCodigo, olvidarCanjes } from './canje';

const sesion = (vinculos: SesionTerceroDto['vinculos']): SesionTerceroDto => ({
  accessToken: 'a.b.c',
  expira: '2026-11-06T23:00:00Z',
  terceroId: 't1',
  otpVerificado: true,
  vinculos,
  expedienteOrigen: null,
});
const vinculo = (id: string) => ({ id, empresa: { id: 'e', nombre: 'Constructora Norte S.A.S.', logoUrl: null }, estado: 'Invitado' as const });
const error = (estado: number, codigo: string, extensiones: Record<string, unknown> = {}) => new ErrorApi(estado, codigo, 'detalle', null, null, {}, extensiones);

afterEach(() => {
  reiniciarCliente();
  olvidarCanjes();
});

describe('canje del enlace', () => {
  it('distingue la sesión del 202 que pide código', () => {
    expect(interpretarCanje(sesion([]))).toEqual({ tipo: 'sesion', sesion: sesion([]) });
    expect(interpretarCanje({ requiereOtp: true, expira: '2026-11-06T22:10:00Z' })).toEqual({ tipo: 'otp', expira: '2026-11-06T22:10:00Z' });
  });

  it('canjea una sola vez por token aunque el efecto corra dos veces (el enlace es de un solo uso)', async () => {
    const fetch = vi.fn(() => Promise.resolve(new Response(JSON.stringify(sesion([])), { status: 200, headers: { 'Content-Type': 'application/json' } })));
    configurarCliente({ fetch });
    const [a, b] = await Promise.all([canjearUnaVez('tok'), canjearUnaVez('tok')]);
    expect(fetch).toHaveBeenCalledTimes(1);
    expect(a).toBe(b);
  });

  it('un fallo de red no se guarda: se puede reintentar', async () => {
    const fetch = vi.fn().mockRejectedValueOnce(new TypeError('sin red')).mockResolvedValueOnce(new Response(JSON.stringify(sesion([])), { status: 200, headers: { 'Content-Type': 'application/json' } }));
    configurarCliente({ fetch });
    await expect(canjearUnaVez('tok')).rejects.toBeInstanceOf(ErrorDeRed);
    await expect(canjearUnaVez('tok')).resolves.toMatchObject({ tipo: 'sesion' });
  });

  it('cada fallo del enlace tiene su pantalla: 410 vencido o usado, 404, OTP vencido o bloqueado', () => {
    expect(falloDeEnlace(error(410, 'enlace-vencido'))).toBe('vencido');
    expect(falloDeEnlace(error(410, 'enlace-usado'))).toBe('usado');
    expect(falloDeEnlace(error(404, 'enlace-no-encontrado'))).toBe('no-encontrado');
    expect(falloDeEnlace(error(410, 'otp-vencido'))).toBe('otp-vencido');
    expect(falloDeEnlace(error(429, 'otp-bloqueado'))).toBe('otp-bloqueado');
    expect(falloDeEnlace(error(410, 'otro-codigo'))).toBe('vencido');
    expect(falloDeEnlace(new ErrorDeRed('sin red', null))).toBe('red');
  });

  it('lee los intentos restantes del 422 otp-invalido (extensión del ProblemDetails)', () => {
    expect(intentosRestantesDe(error(422, 'otp-invalido', { intentosRestantes: 3 }))).toBe(3);
    expect(intentosRestantesDe(error(410, 'otp-vencido'))).toBeNull();
  });

  it('el código acepta solo 6 dígitos aunque se pegue con espacios', () => {
    expect(normalizarCodigo(' 481 72-9 99')).toBe('481729');
  });

  it('un enlace de expediente (correo de observación) lleva directo a esa revisión', () => {
    expect(destinoTrasSesion({ ...sesion([vinculo('v1')]), expedienteOrigen: 'e9' })).toBe('/expediente/e9');
  });

  it('con un solo vínculo va directo a su invitación; con varios, a Mis empresas', () => {
    expect(destinoTrasSesion(sesion([vinculo('v1')]))).toBe('/empresas/v1');
    expect(destinoTrasSesion(sesion([vinculo('v1'), vinculo('v2')]))).toBe('/empresas');
    expect(destinoTrasSesion(sesion([]))).toBe('/empresas');
  });
});
