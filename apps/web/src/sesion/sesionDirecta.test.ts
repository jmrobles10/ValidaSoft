import { ErrorApi } from '@validasoft/api-client';
import { afterEach, describe, expect, it } from 'vitest';
import { resultadoDeFallo } from './sesionDirecta';
import { useSesion } from './sesionStore';

const error = (codigo: string, estado = 401) => new ErrorApi(estado, codigo, 'detalle', null, null);

const token = (exp: number, extra: Record<string, unknown> = {}) =>
  `e30.${Buffer.from(JSON.stringify({ sub: 'u1', exp, 'validasoft:tenant': 't1', ...extra })).toString('base64url')}.firma`;

afterEach(() => useSesion.setState({ sesion: null, estado: 'sin-sesion', empresaSugerida: null, sugerirPasskey: false }));

describe('resultadoDeFallo', () => {
  it('sin cookie o con un fallo que no explica el cierre se ve el ingreso sin más', () => {
    expect(resultadoDeFallo(error('sin-sesion'))).toBe('sin-sesion');
    expect(resultadoDeFallo(error('demasiadas-peticiones', 429))).toBe('sin-sesion');
    expect(resultadoDeFallo(new Error('red'))).toBe('sin-sesion');
  });

  it('una cookie que existía y ya no sirve termina o se revoca según el código', () => {
    expect(resultadoDeFallo(error('sesion-expirada'))).toBe('terminada');
    expect(resultadoDeFallo(error('sesion-inactiva'))).toBe('terminada');
    expect(resultadoDeFallo(error('sesion-revocada'))).toBe('revocada');
    expect(resultadoDeFallo(error('passkey-requerida'))).toBe('revocada');
    expect(resultadoDeFallo(error('cuenta-no-disponible'))).toBe('revocada');
  });
});

describe('sesión directa en el store', () => {
  const futuro = Math.floor(Date.now() / 1000) + 600;

  it('guarda la familia y el método de la sesión directa y recuerda sugerir una passkey', () => {
    expect(useSesion.getState().establecer(token(futuro, { 'validasoft:sesion-id': 's1', 'validasoft:metodo': 'Codigo' }), null, { sugerirPasskey: true })).toBe(true);
    const { sesion, sugerirPasskey, estado } = useSesion.getState();
    expect(estado).toBe('activa');
    expect(sesion?.sesionId).toBe('s1');
    expect(sesion?.metodoIngreso).toBe('Codigo');
    expect(sugerirPasskey).toBe(true);
  });

  it('renovar el token no vuelve a sugerir lo que la persona ya descartó', () => {
    const { establecer, descartarSugerenciaDePasskey } = useSesion.getState();
    establecer(token(futuro, { 'validasoft:sesion-id': 's1' }), null, { sugerirPasskey: true });
    descartarSugerenciaDePasskey();
    establecer(token(futuro + 60, { 'validasoft:sesion-id': 's1' }), null, { sugerirPasskey: false });
    expect(useSesion.getState().sugerirPasskey).toBe(false);
    // Un token renovado sin la opción conserva lo que había.
    establecer(token(futuro + 120, { 'validasoft:sesion-id': 's1' }));
    expect(useSesion.getState().sugerirPasskey).toBe(false);
  });

  it('un token vencido deja la sesión como vencida y marcar el estado la limpia', () => {
    expect(useSesion.getState().establecer(token(1))).toBe(false);
    expect(useSesion.getState().estado).toBe('vencida');
    useSesion.getState().establecer(token(futuro));
    useSesion.getState().marcar('inactiva');
    expect(useSesion.getState().sesion).toBeNull();
    expect(useSesion.getState().estado).toBe('inactiva');
    useSesion.getState().marcar('terminada');
    expect(useSesion.getState().estado).toBe('terminada');
  });
});
