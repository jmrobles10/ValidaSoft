import { describe, expect, it } from 'vitest';
import {
  base64UrlABytes,
  bytesABase64Url,
  ErrorDePasskey,
  nombreSugeridoDePasskey,
  opcionesDeFirma,
  opcionesDeRegistro,
  respuestaDeFirma,
  respuestaDeRegistro,
  traducirFalloDelNavegador,
} from './passkeys';

describe('base64url', () => {
  it('va y vuelve sin perder bytes (incluido lo que base64 escribe con + y /)', () => {
    const bytes = new Uint8Array([251, 255, 190, 0, 1, 2, 3, 250]);
    const texto = bytesABase64Url(bytes);
    expect(texto).toMatch(/^[A-Za-z0-9_-]+$/);
    expect([...base64UrlABytes(texto)]).toEqual([...bytes]);
  });

  it('no deja relleno y acepta cualquier largo', () => {
    for (const largo of [0, 1, 2, 3, 4, 31, 32, 33]) {
      const bytes = Uint8Array.from({ length: largo }, (_, i) => (i * 37) % 256);
      const texto = bytesABase64Url(bytes);
      expect(texto).not.toContain('=');
      expect([...base64UrlABytes(texto)]).toEqual([...bytes]);
    }
  });
});

describe('opciones del servidor', () => {
  const registro = {
    rp: { id: 'validasoft.co', name: 'Validasoft' },
    user: { id: bytesABase64Url(new Uint8Array([1, 2, 3])), name: 'ana@aceros.co', displayName: 'Ana' },
    challenge: bytesABase64Url(new Uint8Array([9, 9, 9, 9])),
    pubKeyCredParams: [{ type: 'public-key', alg: -7 }],
    timeout: 60_000,
    excludeCredentials: [{ type: 'public-key', id: bytesABase64Url(new Uint8Array([7, 7])) }],
    authenticatorSelection: { residentKey: 'required', userVerification: 'required' },
    attestation: 'none',
  };

  it('convierte desafío, usuario y credenciales excluidas a binarios', () => {
    const opciones = opcionesDeRegistro(registro);
    expect([...new Uint8Array(opciones.challenge as ArrayBuffer)]).toEqual([9, 9, 9, 9]);
    expect([...new Uint8Array(opciones.user.id as ArrayBuffer)]).toEqual([1, 2, 3]);
    expect(opciones.rp).toEqual({ id: 'validasoft.co', name: 'Validasoft' });
    expect(opciones.user.name).toBe('ana@aceros.co');
    expect(opciones.excludeCredentials).toHaveLength(1);
    expect([...new Uint8Array(opciones.excludeCredentials?.[0]?.id as ArrayBuffer)]).toEqual([7, 7]);
    expect(opciones.authenticatorSelection).toEqual({ residentKey: 'required', userVerification: 'required' });
    expect(opciones.attestation).toBe('none');
  });

  it('una opción sin lo indispensable no se inventa', () => {
    expect(() => opcionesDeRegistro({ ...registro, challenge: undefined })).toThrow(ErrorDePasskey);
    expect(() => opcionesDeRegistro({ ...registro, user: undefined })).toThrow(ErrorDePasskey);
    expect(() => opcionesDeRegistro({ ...registro, rp: { id: 'x' } })).toThrow(ErrorDePasskey);
  });

  it('las opciones de firma sin credenciales dejan que el navegador ofrezca las del sitio y exigen verificar al usuario', () => {
    const opciones = opcionesDeFirma({ challenge: bytesABase64Url(new Uint8Array([5, 5])), rpId: 'validasoft.co' });
    expect(opciones.allowCredentials).toEqual([]);
    expect(opciones.userVerification).toBe('required');
    expect(opciones.rpId).toBe('validasoft.co');

    const conCredencial = opcionesDeFirma({ challenge: bytesABase64Url(new Uint8Array([5])), allowCredentials: [{ type: 'public-key', id: bytesABase64Url(new Uint8Array([1])) }] });
    expect(conCredencial.allowCredentials).toHaveLength(1);
  });
});

describe('respuestas del navegador', () => {
  const credencial = (respuesta: object) =>
    ({ id: 'cred-1', rawId: new Uint8Array([1, 2, 3]).buffer, type: 'public-key', response: respuesta }) as unknown as PublicKeyCredential;

  it('serializa el registro con los binarios en base64url', () => {
    const resultado = respuestaDeRegistro(
      credencial({ attestationObject: new Uint8Array([4, 5]).buffer, clientDataJSON: new Uint8Array([6]).buffer, getTransports: () => ['internal'] }),
    );
    expect(resultado).toEqual({
      id: 'cred-1',
      rawId: bytesABase64Url(new Uint8Array([1, 2, 3])),
      type: 'public-key',
      response: { attestationObject: bytesABase64Url(new Uint8Array([4, 5])), clientDataJSON: bytesABase64Url(new Uint8Array([6])), transports: ['internal'] },
      clientExtensionResults: {},
    });
  });

  it('serializa la firma y manda el manejador solo si lo hay', () => {
    const base = { authenticatorData: new Uint8Array([1]).buffer, clientDataJSON: new Uint8Array([2]).buffer, signature: new Uint8Array([3]).buffer };
    expect(respuestaDeFirma(credencial({ ...base, userHandle: new Uint8Array([8, 8]).buffer })).response.userHandle).toBe(bytesABase64Url(new Uint8Array([8, 8])));
    expect(respuestaDeFirma(credencial({ ...base, userHandle: null })).response.userHandle).toBeNull();
    expect(respuestaDeFirma(credencial({ ...base, userHandle: new Uint8Array([]).buffer })).response.userHandle).toBeNull();
  });
});

describe('fallos del navegador', () => {
  const dom = (nombre: string) => new DOMException('x', nombre);

  it('traduce cancelar, repetir y no poder a causas que la pantalla explica', () => {
    expect(traducirFalloDelNavegador(dom('NotAllowedError')).causa).toBe('cancelada');
    expect(traducirFalloDelNavegador(dom('AbortError')).causa).toBe('cancelada');
    expect(traducirFalloDelNavegador(dom('InvalidStateError')).causa).toBe('ya-registrada');
    expect(traducirFalloDelNavegador(dom('NotSupportedError')).causa).toBe('no-soportada');
    expect(traducirFalloDelNavegador(dom('SecurityError')).causa).toBe('no-soportada');
    expect(traducirFalloDelNavegador(new Error('raro')).causa).toBe('otra');
    const propio = new ErrorDePasskey('cancelada', 'x');
    expect(traducirFalloDelNavegador(propio)).toBe(propio);
  });
});

describe('nombre sugerido', () => {
  it('combina navegador y sistema', () => {
    expect(nombreSugeridoDePasskey('Mozilla/5.0 (Windows NT 10.0; Win64; x64) AppleWebKit/537.36 (KHTML, like Gecko) Chrome/130.0 Safari/537.36')).toBe('Chrome en Windows');
    expect(nombreSugeridoDePasskey('Mozilla/5.0 (Windows NT 10.0) AppleWebKit/537.36 Chrome/130.0 Safari/537.36 Edg/130.0')).toBe('Edge en Windows');
    expect(nombreSugeridoDePasskey('Mozilla/5.0 (iPhone; CPU iPhone OS 18_0 like Mac OS X) AppleWebKit/605.1.15 Version/18.0 Mobile Safari/604.1')).toBe('Safari en iOS');
    expect(nombreSugeridoDePasskey('Mozilla/5.0 (X11; Linux x86_64; rv:132.0) Gecko/20100101 Firefox/132.0')).toBe('Firefox en Linux');
    expect(nombreSugeridoDePasskey('')).toBe('Mi passkey');
  });
});
