// @vitest-environment node
import { describe, expect, it } from 'vitest';
import { CERTIFICADO_REAL, CLAVES_REAL } from '../pruebas/certificados';
import { aHex, desdeBase64, desdeHex, importarClaveEd25519, sha256, verificarEd25519 } from './cripto';
import { bytesCanonicos } from './jcs';

const texto = (s: string) => new TextEncoder().encode(s);

describe('SHA-256', () => {
  it('vectores de FIPS 180-2 en hexadecimal minúsculo', async () => {
    expect(aHex(await sha256(texto('abc')))).toBe('ba7816bf8f01cfea414140de5dae2223b00361a396177a9cb410ff61f20015ad');
    expect(aHex(await sha256(texto('')))).toBe('e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855');
  });

  it('el JCS + SHA-256 de un certificado real de la API da su hashContenido', async () => {
    expect(aHex(await sha256(bytesCanonicos(CERTIFICADO_REAL.contenidoCanonico)))).toBe(CERTIFICADO_REAL.hashContenido);
  });
});

describe('codificaciones', () => {
  it('hexadecimal y base64 (estándar y url, con o sin relleno)', () => {
    expect(Array.from(desdeHex('00ff10') ?? [])).toEqual([0, 255, 16]);
    expect(desdeHex('abc')).toBeNull();
    expect(desdeHex('zz')).toBeNull();
    expect(Array.from(desdeBase64('+/8=') ?? [])).toEqual([251, 255]);
    expect(Array.from(desdeBase64('-_8') ?? [])).toEqual([251, 255]);
    expect(desdeBase64('no es base64!')).toBeNull();
  });
});

describe('Ed25519 con WebCrypto', () => {
  it('verifica una firma hecha con un par generado en la prueba y rechaza otro mensaje', async () => {
    const par = await crypto.subtle.generateKey({ name: 'Ed25519' }, true, ['sign', 'verify']);
    const { x } = await crypto.subtle.exportKey('jwk', par.publicKey);
    const mensaje = await sha256(texto('contenido canónico'));
    const firma = new Uint8Array(await crypto.subtle.sign({ name: 'Ed25519' }, par.privateKey, mensaje));

    const clave = await importarClaveEd25519(x ?? '');
    expect(await verificarEd25519(clave, firma, mensaje)).toBe(true);
    expect(await verificarEd25519(clave, firma, await sha256(texto('otro contenido')))).toBe(false);
    expect(await verificarEd25519(clave, firma.slice(0, 63), mensaje)).toBe(false);
  });

  it('verifica el sello real de la API sobre los 32 bytes del hash (no sobre el texto hexadecimal)', async () => {
    const clave = await importarClaveEd25519(CLAVES_REAL.claves[0]!.jwk.x);
    const firma = desdeBase64(CERTIFICADO_REAL.firma.valor)!;
    expect(await verificarEd25519(clave, firma, desdeHex(CERTIFICADO_REAL.hashContenido)!)).toBe(true);
    expect(await verificarEd25519(clave, firma, texto(CERTIFICADO_REAL.hashContenido))).toBe(false);
  });

  it('una clave que no tiene 32 bytes no se importa', async () => {
    await expect(importarClaveEd25519('AAAA')).rejects.toThrow(RangeError);
  });
});
