import { afterEach, describe, expect, it } from 'vitest';
import { simularNavegadorConPasskeys, OPCIONES_DE_FIRMA, type NavegadorSimulado } from '../pruebas/navegadorSimulado';
import { esErrorDePasskey, firmarConPasskey, soportaPasskeysCondicional } from './passkeys';

let navegador: NavegadorSimulado | null = null;

afterEach(() => {
  navegador?.restaurar();
  navegador = null;
});

describe('UI condicional de passkeys (PI-14)', () => {
  it('sin passkeys en el navegador no se ofrece', async () => {
    await expect(soportaPasskeysCondicional()).resolves.toBe(false);
  });

  it('un navegador con passkeys pero sin la consulta condicional tampoco', async () => {
    navegador = simularNavegadorConPasskeys();
    await expect(soportaPasskeysCondicional()).resolves.toBe(false);
  });

  it('se ofrece solo si el navegador dice que puede', async () => {
    navegador = simularNavegadorConPasskeys({ condicional: true });
    await expect(soportaPasskeysCondicional()).resolves.toBe(true);
    navegador.restaurar();
    navegador = simularNavegadorConPasskeys({ condicional: false });
    await expect(soportaPasskeysCondicional()).resolves.toBe(false);
  });

  it('pide la firma condicional con mediation conditional y la señal para poder abortarla', async () => {
    navegador = simularNavegadorConPasskeys({ condicional: true });
    const control = new AbortController();

    const respuesta = await firmarConPasskey(OPCIONES_DE_FIRMA, control.signal, true);

    expect(navegador.llamadas).toEqual([{ mediation: 'conditional', conSenal: true }]);
    expect(respuesta).toMatchObject({ id: 'cred-1', type: 'public-key', response: { signature: 'CAk' } });
  });

  it('la firma de siempre (el botón) no es condicional', async () => {
    navegador = simularNavegadorConPasskeys({ condicional: true });
    await firmarConPasskey(OPCIONES_DE_FIRMA);
    expect(navegador.llamadas).toEqual([{ mediation: undefined, conSenal: false }]);
  });

  it('que el navegador aborte la petición condicional es una cancelación, no un fallo', async () => {
    navegador = simularNavegadorConPasskeys({ condicional: true });
    navegador.get.mockRejectedValueOnce(new DOMException('abortada', 'AbortError'));

    const error = await firmarConPasskey(OPCIONES_DE_FIRMA, new AbortController().signal, true).catch((e: unknown) => e);

    expect(esErrorDePasskey(error)).toBe(true);
    expect(error).toMatchObject({ causa: 'cancelada' });
  });
});
