import { generateKeyPairSync, createVerify } from 'node:crypto';
import { afterEach, describe, expect, it } from 'vitest';
import { firmarAsercion } from '../dev/firmarAsercion';
import { estaEmbebida, interpretarMensaje, origenDelContenedor, origenesPermitidos, origenPermitido } from './puenteEmbed';
import { useSesion } from './sesionStore';

const tokenFalso = (exp: number) =>
  `e30.${Buffer.from(JSON.stringify({ sub: 'u1', exp, 'validasoft:tenant': 't1' })).toString('base64url')}.firma`;

describe('interpretarMensaje', () => {
  it('acepta la sesión con o sin empresa sugerida', () => {
    expect(interpretarMensaje({ tipo: 'validasoft:sesion', token: 'a.b.c', empresaId: 'e1' })).toEqual({
      tipo: 'validasoft:sesion',
      token: 'a.b.c',
      empresaId: 'e1',
    });
    expect(interpretarMensaje({ tipo: 'validasoft:sesion', token: 'a.b.c' })).toMatchObject({ empresaId: null });
  });

  it('valida el tema y el cierre', () => {
    expect(interpretarMensaje({ tipo: 'validasoft:tema', mode: 'light' })).toEqual({ tipo: 'validasoft:tema', mode: 'light' });
    expect(interpretarMensaje({ tipo: 'validasoft:tema', mode: 'sepia' })).toBeNull();
    expect(interpretarMensaje({ tipo: 'validasoft:cerrar-sesion' })).toEqual({ tipo: 'validasoft:cerrar-sesion' });
  });

  it('ignora mensajes ajenos o mal formados', () => {
    expect(interpretarMensaje('validasoft:sesion')).toBeNull();
    expect(interpretarMensaje({ tipo: 'validasoft:sesion', token: 'no-es-jwt' })).toBeNull();
    expect(interpretarMensaje({ tipo: 'otro:evento' })).toBeNull();
    expect(interpretarMensaje(null)).toBeNull();
  });
});

describe('orígenes del contenedor', () => {
  const permitidos = origenesPermitidos(' https://srm.cliente.co/ , https://marco.cliente.co', 'https://validasoft.co');

  it('combina el origen propio con los configurados, sin barras finales', () => {
    expect(permitidos).toEqual(['https://validasoft.co', 'https://srm.cliente.co', 'https://marco.cliente.co']);
    expect(origenesPermitidos(undefined, 'http://localhost:5173')).toEqual(['http://localhost:5173']);
  });

  it('rechaza orígenes no listados y el origen opaco', () => {
    expect(origenPermitido('https://srm.cliente.co', permitidos)).toBe(true);
    expect(origenPermitido('https://atacante.co', permitidos)).toBe(false);
    expect(origenPermitido('null', [...permitidos, 'null'])).toBe(false);
  });

  it('solo responde al contenedor si es de confianza', () => {
    const ventana = (referrer: string) => ({ location: {} as Location, document: { referrer } as Document });
    expect(origenDelContenedor(ventana('https://srm.cliente.co/compras/proveedor/7'), permitidos)).toBe('https://srm.cliente.co');
    expect(origenDelContenedor(ventana('https://atacante.co/'), permitidos)).toBeNull();
    expect(origenDelContenedor(ventana(''), permitidos)).toBeNull();
  });

  it('detecta que la página de pruebas no está embebida', () => {
    expect(estaEmbebida(window)).toBe(false);
  });
});

describe('useSesion', () => {
  afterEach(() => useSesion.getState().marcar('sin-sesion'));

  it('activa un token vigente y guarda la empresa sugerida', () => {
    const exp = Math.floor(Date.now() / 1000) + 600;
    expect(useSesion.getState().establecer(tokenFalso(exp), 'e1')).toBe(true);
    expect(useSesion.getState()).toMatchObject({ estado: 'activa', empresaSugerida: 'e1' });
    expect(useSesion.getState().sesion?.usuarioId).toBe('u1');
  });

  it('marca vencido un token expirado y rechazado uno ilegible', () => {
    expect(useSesion.getState().establecer(tokenFalso(1000))).toBe(false);
    expect(useSesion.getState().estado).toBe('vencida');
    expect(useSesion.getState().establecer('basura')).toBe(false);
    expect(useSesion.getState().estado).toBe('rechazada');
  });
});

describe('firmarAsercion (solo demo)', () => {
  it('firma un JWT RS256 que verifica la clave pública del sistema', async () => {
    const { privateKey, publicKey } = generateKeyPairSync('rsa', {
      modulusLength: 2048,
      publicKeyEncoding: { type: 'spki', format: 'pem' },
      privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
    });
    const ahora = new Date('2026-11-06T15:00:00Z');

    const jwt = await firmarAsercion('SRM', privateKey, { sub: 'u-7', nombre: 'Marta Ruiz', correo: 'marta@cliente.co' }, ahora);

    const [cabecera = '', carga = '', firma = ''] = jwt.split('.');
    expect(JSON.parse(Buffer.from(cabecera, 'base64url').toString())).toEqual({ alg: 'RS256', typ: 'JWT' });
    expect(JSON.parse(Buffer.from(carga, 'base64url').toString())).toEqual({
      iss: 'SRM',
      aud: 'validasoft',
      sub: 'u-7',
      name: 'Marta Ruiz',
      email: 'marta@cliente.co',
      iat: ahora.getTime() / 1000,
      exp: ahora.getTime() / 1000 + 300,
    });
    const valida = createVerify('RSA-SHA256').update(`${cabecera}.${carga}`).verify(publicKey, Buffer.from(firma, 'base64url'));
    expect(valida).toBe(true);
  });

  it('exige PEM PKCS#8', async () => {
    await expect(firmarAsercion('SRM', '-----BEGIN RSA PRIVATE KEY-----', { sub: 'u', nombre: 'n', correo: 'c@c.co' })).rejects.toThrow(
      'PKCS#8',
    );
  });
});
