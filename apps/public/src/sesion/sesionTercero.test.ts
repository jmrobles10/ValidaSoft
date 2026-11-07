import { ErrorApi, ErrorDeRed, type SesionTerceroDto } from '@validasoft/api-client';
import { afterEach, beforeEach, describe, expect, it, vi } from 'vitest';
import { envolverConRenovacion } from './renovacion';
import { correoValido } from './correo';
import { ANTICIPO_RENOVACION_MS, crearSesionTercero, REINTENTO_RENOVACION_MS, type ApiSesion } from './sesionTercero';

const AHORA = new Date('2026-11-06T20:00:00Z').getTime();
const sesion = (token: string, minutos = 30): SesionTerceroDto => ({
  accessToken: token,
  expira: new Date(Date.now() + minutos * 60_000).toISOString(),
  terceroId: 't1',
  otpVerificado: true,
  vinculos: [],
  expedienteOrigen: null,
});
const expirada = () => new ErrorApi(401, 'sesion-expirada', 'La sesión venció.', null, null);

function preparar(renovar: ApiSesion['renovar'], cerrar: ApiSesion['cerrar'] = () => Promise.resolve()) {
  const api = { renovar: vi.fn(renovar), cerrar: vi.fn(cerrar) };
  return { api, store: crearSesionTercero(api) };
}

beforeEach(() => {
  vi.useFakeTimers();
  vi.setSystemTime(AHORA);
});
afterEach(() => vi.useRealTimers());

describe('restaurar al arrancar', () => {
  it('con la cookie vigente entra directo (la persona recargó la página)', async () => {
    const { store } = preparar(() => Promise.resolve(sesion('a1')));
    expect(store.getState().estado).toBe('restaurando');
    await store.getState().restaurar();
    expect(store.getState()).toMatchObject({ estado: 'activa', sesion: { accessToken: 'a1' } });
  });

  it('sin cookie (401) muestra la entrada', async () => {
    const { store } = preparar(() => Promise.reject(expirada()));
    await store.getState().restaurar();
    expect(store.getState()).toMatchObject({ estado: 'sin-sesion', sesion: null });
  });

  it('si entró por un enlace mientras tanto, gana esa sesión', async () => {
    let resolver: (s: SesionTerceroDto) => void = () => undefined;
    const { store } = preparar(() => new Promise((r) => (resolver = r)));
    const restauracion = store.getState().restaurar();
    store.getState().establecer(sesion('del-enlace'));
    resolver(sesion('de-la-cookie'));
    await restauracion;
    expect(store.getState().sesion?.accessToken).toBe('del-enlace');
  });
});

describe('renovar antes de vencer', () => {
  it('pide el access nuevo dos minutos antes de que expire', async () => {
    const { api, store } = preparar(() => Promise.resolve(sesion('a2')));
    store.getState().establecer(sesion('a1'));
    await vi.advanceTimersByTimeAsync(30 * 60_000 - ANTICIPO_RENOVACION_MS - 1);
    expect(api.renovar).not.toHaveBeenCalled();
    await vi.advanceTimersByTimeAsync(1);
    expect(api.renovar).toHaveBeenCalledTimes(1);
    expect(store.getState().sesion?.accessToken).toBe('a2');
  });

  it('sin red reintenta mientras el access sirva; con 401 la sesión termina', async () => {
    const { api, store } = preparar(() => Promise.reject(new ErrorDeRed('sin red', null)));
    store.getState().establecer(sesion('a1'));
    await vi.advanceTimersByTimeAsync(30 * 60_000 - ANTICIPO_RENOVACION_MS);
    expect(store.getState().estado).toBe('activa');
    api.renovar.mockImplementation(() => Promise.reject(expirada()));
    await vi.advanceTimersByTimeAsync(REINTENTO_RENOVACION_MS);
    expect(api.renovar).toHaveBeenCalledTimes(2);
    expect(store.getState()).toMatchObject({ estado: 'vencida', sesion: null });
  });

  it('varias llamadas a la vez comparten una sola renovación (la cookie rota y no se puede usar dos veces)', async () => {
    const { api, store } = preparar(() => Promise.resolve(sesion('a2')));
    store.getState().establecer(sesion('a1'));
    const [x, y] = await Promise.all([store.getState().renovarAhora(), store.getState().renovarAhora()]);
    expect(api.renovar).toHaveBeenCalledTimes(1);
    expect([x, y]).toEqual(['a2', 'a2']);
  });
});

describe('reintento tras 401', () => {
  const json = (estado: number) => new Response('{}', { status: estado, headers: { 'Content-Type': 'application/json' } });

  it('renueva una vez y repite la petición con el token nuevo', async () => {
    const base = vi.fn<typeof fetch>().mockResolvedValueOnce(json(401)).mockResolvedValueOnce(json(200));
    const renovar = vi.fn(() => Promise.resolve('nuevo'));
    const respuesta = await envolverConRenovacion(base, renovar)('/public/v1/boveda', { headers: { Authorization: 'Bearer viejo' } });
    expect(respuesta.status).toBe(200);
    expect(renovar).toHaveBeenCalledTimes(1);
    expect(new Headers(base.mock.calls[1]?.[1]?.headers).get('Authorization')).toBe('Bearer nuevo');
  });

  it('si no se pudo renovar devuelve el 401 original; las rutas de sesión no se reintentan', async () => {
    const base = vi.fn<typeof fetch>().mockResolvedValue(json(401));
    expect((await envolverConRenovacion(base, () => Promise.resolve(null))('/public/v1/boveda')).status).toBe(401);
    expect(base).toHaveBeenCalledTimes(1);
    const renovar = vi.fn(() => Promise.resolve('x'));
    await envolverConRenovacion(base, renovar)('/public/v1/sesion/renovar', { method: 'POST' });
    await envolverConRenovacion(base, renovar)('/public/v1/enlaces/tok/canjear', { method: 'POST' });
    expect(renovar).not.toHaveBeenCalled();
  });
});

describe('salir y entrar con el correo', () => {
  it('«Salir» cierra en el servidor (borra la cookie) y aquí; sin red igual sale', async () => {
    const { api, store } = preparar(() => Promise.resolve(sesion('a2')), () => Promise.reject(new ErrorDeRed('sin red', null)));
    store.getState().establecer(sesion('a1'));
    await store.getState().salir();
    expect(api.cerrar).toHaveBeenCalledTimes(1);
    expect(store.getState()).toMatchObject({ estado: 'salio', sesion: null });
    await vi.advanceTimersByTimeAsync(60 * 60_000);
    expect(api.renovar).not.toHaveBeenCalled(); // ya no se renueva nada
  });

  it('valida el correo antes de pedir el enlace', () => {
    expect(correoValido(' contacto@aceros.co ')).toBe(true);
    expect(correoValido('contacto@aceros')).toBe(false);
    expect(correoValido('con tacto@aceros.co')).toBe(false);
  });
});
