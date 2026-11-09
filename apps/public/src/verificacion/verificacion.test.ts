// @vitest-environment node
import { afterEach, describe, expect, it, vi } from 'vitest';
import { CERTIFICADO_REAL, CLAVES_REAL, certificadoSellado } from '../pruebas/certificados';
import { leerClaves } from './claves';
import { conclusion, normalizarHuella, verificarCertificado } from './verificacion';

afterEach(() => vi.restoreAllMocks());

describe('verificación del certificado en el navegador', () => {
  it('certificado real de la API con la clave publicada: auténtico en los cuatro pasos', async () => {
    const r = await verificarCertificado(CERTIFICADO_REAL, CLAVES_REAL, '26b45bd2c48a');
    expect(r).toEqual({ contenido: 'ok', huella: 'ok', clave: 'ok', sello: 'ok', hashCalculado: CERTIFICADO_REAL.hashContenido });
    expect(conclusion(r)).toBe('autentico');
  });

  it('par Ed25519 generado en la prueba: auténtico; sin huella en el enlace queda como aviso', async () => {
    const { certificado, claves } = await certificadoSellado();
    const r = await verificarCertificado(certificado, claves, null);
    expect(r).toMatchObject({ contenido: 'ok', huella: 'sin-huella', clave: 'ok', sello: 'ok' });
    expect(conclusion(r)).toBe('autentico');
  });

  it('contenido alterado: el hash no coincide (y el sello del hash declarado sigue siendo válido)', async () => {
    const { certificado, claves } = await certificadoSellado();
    const alterado = { ...certificado, contenidoCanonico: { ...certificado.contenidoCanonico, vigenteHasta: '2030-01-01' } };
    const r = await verificarCertificado(alterado, claves, null);
    expect(r.contenido).toBe('hash-distinto');
    expect(conclusion(r)).toBe('no-autentico');
  });

  it('datos mostrados distintos de los sellados', async () => {
    const { certificado, claves } = await certificadoSellado();
    const r = await verificarCertificado({ ...certificado, titular: { ...certificado.titular, nombre: 'Otra S.A.S.' } }, claves, null);
    expect(r.contenido).toBe('datos-distintos');
    expect(conclusion(r)).toBe('no-autentico');
  });

  it('firma de otra clave o alterada: sello inválido', async () => {
    const { certificado } = await certificadoSellado();
    const { claves: otras } = await certificadoSellado();
    expect((await verificarCertificado(certificado, otras, null)).sello).toBe('invalido');
    const { claves } = await certificadoSellado();
    const firma = { ...certificado.firma, valor: CERTIFICADO_REAL.firma.valor };
    expect((await verificarCertificado({ ...certificado, firma }, claves, null)).sello).toBe('invalido');
  });

  it('kid que la plataforma no publica, clave anterior (rotación) y claves no disponibles', async () => {
    const { certificado, claves } = await certificadoSellado();
    const desconocida = await verificarCertificado({ ...certificado, firma: { ...certificado.firma, kid: 'vs-otra' } }, claves, null);
    expect(desconocida).toMatchObject({ clave: 'desconocida', sello: 'sin-clave' });
    expect(conclusion(desconocida)).toBe('no-autentico');

    const anterior = await verificarCertificado(certificado, { ...claves, claves: claves.claves.map((c) => ({ ...c, vigente: false })) }, null);
    expect(anterior).toMatchObject({ clave: 'anterior', sello: 'ok' });
    expect(conclusion(anterior)).toBe('autentico');

    const sinClaves = await verificarCertificado(certificado, null, null);
    expect(sinClaves).toMatchObject({ contenido: 'ok', clave: 'no-disponible', sello: 'sin-clave' });
    expect(conclusion(sinClaves)).toBe('incompleto');
  });

  it('huella del enlace distinta de la del certificado', async () => {
    const r = await verificarCertificado(CERTIFICADO_REAL, CLAVES_REAL, '5a7e·0c1d·9f22');
    expect(r.huella).toBe('distinta');
    expect(conclusion(r)).toBe('no-autentico');
    expect((await verificarCertificado(CERTIFICADO_REAL, CLAVES_REAL, '26B4·5BD2·C48A')).huella).toBe('ok');
  });

  it('navegador sin Ed25519: «no soportado», sin romper el resto', async () => {
    vi.spyOn(crypto.subtle, 'importKey').mockRejectedValue(new DOMException('Ed25519', 'NotSupportedError'));
    const r = await verificarCertificado(CERTIFICADO_REAL, CLAVES_REAL, '26b45bd2c48a');
    expect(r).toMatchObject({ contenido: 'ok', huella: 'ok', clave: 'ok', sello: 'no-soportado' });
    expect(conclusion(r)).toBe('no-soportado');
  });
});

describe('utilidades', () => {
  it('normaliza la huella como la API (12 hexadecimales, sin separadores)', () => {
    expect(normalizarHuella('5a7e·0c1d·9f22')).toBe('5a7e0c1d9f22');
    expect(normalizarHuella('5A7E-0C1D 9F22ff')).toBe('5a7e0c1d9f22');
    expect(normalizarHuella('5a7e')).toBeNull();
    expect(normalizarHuella(null)).toBeNull();
  });

  it('valida el documento de claves: una página HTML no es una clave', () => {
    expect(leerClaves(CLAVES_REAL).claves).toHaveLength(1);
    expect(() => leerClaves('<!doctype html>')).toThrow();
    expect(leerClaves({ ...CLAVES_REAL, claves: [{ kid: 'x', vigente: true, jwk: { kty: 'RSA' } }] }).claves).toEqual([]);
  });
});
