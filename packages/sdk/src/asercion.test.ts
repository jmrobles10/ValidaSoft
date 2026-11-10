import { createPublicKey, generateKeyPairSync, verify } from 'node:crypto';
import { describe, expect, it } from 'vitest';
import { crearFirmanteDeAserciones } from './asercion';

const LAURA = { sub: 'srm-laura', nombre: 'Laura Revisora', correo: 'laura@constructora.co' };
const AHORA = new Date('2026-11-09T15:00:00Z');

function partes(jwt: string) {
  const [cabecera = '', carga = '', firma = ''] = jwt.split('.');
  const leer = (texto: string) => JSON.parse(Buffer.from(texto, 'base64url').toString()) as Record<string, unknown>;
  return { cabecera: leer(cabecera), carga: leer(carga), datos: Buffer.from(`${cabecera}.${carga}`), firma: Buffer.from(firma, 'base64url') };
}

describe('aserciones de identidad delegada', () => {
  it('con RSA firma RS256 con los reclamos que exige Validasoft y un jti por aserción', async () => {
    const { privateKey, publicKey } = generateKeyPairSync('rsa', {
      modulusLength: 2048,
      privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
      publicKeyEncoding: { type: 'spki', format: 'pem' },
    });
    const firmante = crearFirmanteDeAserciones('SRM', privateKey, () => AHORA);
    const una = partes(await firmante.firmar(LAURA));
    const otra = partes(await firmante.firmar(LAURA));

    expect(una.cabecera).toEqual({ alg: 'RS256', typ: 'JWT' });
    expect(una.carga).toMatchObject({ iss: 'SRM', aud: 'validasoft', sub: 'srm-laura', name: 'Laura Revisora', email: 'laura@constructora.co' });
    const exp = una.carga.exp as number;
    const iat = una.carga.iat as number;
    expect(exp - iat).toBeLessThanOrEqual(300);
    expect(una.carga.jti).not.toBe(otra.carga.jti);
    expect(verify('RSA-SHA256', una.datos, createPublicKey(publicKey), una.firma)).toBe(true);
  });

  it('con EC P-256 firma ES256 en formato JWS (r||s)', async () => {
    const { privateKey, publicKey } = generateKeyPairSync('ec', {
      namedCurve: 'P-256',
      privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
      publicKeyEncoding: { type: 'spki', format: 'pem' },
    });
    const jwt = partes(await crearFirmanteDeAserciones('MARCO', privateKey).firmar(LAURA));
    expect(jwt.cabecera.alg).toBe('ES256');
    expect(jwt.firma).toHaveLength(64);
    expect(verify('sha256', jwt.datos, { key: createPublicKey(publicKey), dsaEncoding: 'ieee-p1363' }, jwt.firma)).toBe(true);
  });

  it('no firma más de cinco minutos, ni con claves débiles o en otro formato', async () => {
    const rsa = generateKeyPairSync('rsa', { modulusLength: 2048, privateKeyEncoding: { type: 'pkcs8', format: 'pem' }, publicKeyEncoding: { type: 'spki', format: 'pem' } });
    await expect(crearFirmanteDeAserciones('SRM', rsa.privateKey).firmar(LAURA, 301)).rejects.toThrow(/5 minutos/);
    const debil = generateKeyPairSync('rsa', { modulusLength: 1024, privateKeyEncoding: { type: 'pkcs8', format: 'pem' }, publicKeyEncoding: { type: 'spki', format: 'pem' } });
    await expect(crearFirmanteDeAserciones('SRM', debil.privateKey).firmar(LAURA)).rejects.toThrow(/2048/);
    const pkcs1 = generateKeyPairSync('rsa', { modulusLength: 2048, privateKeyEncoding: { type: 'pkcs1', format: 'pem' }, publicKeyEncoding: { type: 'spki', format: 'pem' } });
    await expect(crearFirmanteDeAserciones('SRM', pkcs1.privateKey).firmar(LAURA)).rejects.toThrow(/PKCS#8/);
    expect(() => crearFirmanteDeAserciones(' ', rsa.privateKey)).toThrow(/código/);
  });
});
