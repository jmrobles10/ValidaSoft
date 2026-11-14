// @vitest-environment node
import { afterEach, describe, expect, it, vi } from 'vitest';
import type { ClavePublicaDeCertificadosDto, ClavesDeCertificadosDto } from '@validasoft/api-client';
import { CERTIFICADO_REAL, CLAVES_REAL, certificadoSellado } from '../pruebas/certificados';
import { leerClaves } from './claves';
import { claveRetiradaVale, conclusion, normalizarHuella, verificarCertificado } from './verificacion';

afterEach(() => vi.restoreAllMocks());

/** «Hoy» de las pruebas de claves retiradas: después de la emisión del certificado de prueba (2026-11-09T00:02:30Z). */
const AHORA = new Date('2026-12-15T12:00:00Z');

/** Las mismas claves, pero retiradas: `vigente: false` con las fechas de `fechas` (CL-2). */
function retirada(claves: ClavesDeCertificadosDto, fechas: Partial<ClavePublicaDeCertificadosDto>): ClavesDeCertificadosDto {
  return { ...claves, claves: claves.claves.map((c) => ({ ...c, vigente: false, ...fechas })) };
}

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

    // Clave retirada con su fecha de retiro posterior a la emisión (2026-11-09T00:02:30Z): sigue verificando (CL-2).
    const anterior = await verificarCertificado(certificado, retirada(claves, { retiradaEn: '2026-12-01T00:00:00Z' }), null, AHORA);
    expect(anterior).toMatchObject({ clave: 'anterior', sello: 'ok' });
    expect(conclusion(anterior)).toBe('autentico');

    const sinClaves = await verificarCertificado(certificado, null, null);
    expect(sinClaves).toMatchObject({ contenido: 'ok', clave: 'no-disponible', sello: 'sin-clave' });
    expect(conclusion(sinClaves)).toBe('incompleto');
  });

  describe('claves retiradas: solo validan emisiones anteriores a su retiro y dentro de su plazo (CL-2)', () => {
    const verificar = async (fechas: Partial<ClavePublicaDeCertificadosDto>, ahora = AHORA) => {
      const { certificado, claves } = await certificadoSellado(); // emitido 2026-11-09T00:02:30Z
      return verificarCertificado(certificado, retirada(claves, fechas), null, ahora);
    };

    it('emitido antes del retiro (o en el mismo segundo) y plazo vigente: la clave anterior valida', async () => {
      for (const retiradaEn of ['2026-12-01T00:00:00Z', '2026-11-09T00:02:30Z', '2026-11-09T00:02:30.9000000+00:00']) {
        const r = await verificar({ retiradaEn, verificableHasta: '2028-12-01T00:00:00Z' });
        expect(r).toMatchObject({ clave: 'anterior', sello: 'ok' });
        expect(conclusion(r)).toBe('autentico');
      }
    });

    it('sin verificableHasta no hay plazo que vencer; solo cuenta el retiro', async () => {
      const r = await verificar({ retiradaEn: '2026-12-01T00:00:00Z', verificableHasta: null }, new Date('2040-01-01T00:00:00Z'));
      expect(r).toMatchObject({ clave: 'anterior', sello: 'ok' });
    });

    it('emitido después del retiro: la clave ya no sellaba, así que el certificado no es auténtico aunque la firma cuadre', async () => {
      const r = await verificar({ retiradaEn: '2026-11-09T00:02:29Z', verificableHasta: '2028-12-01T00:00:00Z' });
      expect(r).toMatchObject({ contenido: 'ok', clave: 'fuera-de-plazo', sello: 'sin-clave' });
      expect(conclusion(r)).toBe('no-autentico');
    });

    it('plazo de verificación vencido: no valida ni una emisión anterior al retiro', async () => {
      const r = await verificar({ retiradaEn: '2026-12-01T00:00:00Z', verificableHasta: '2026-12-10T00:00:00Z' });
      expect(r).toMatchObject({ clave: 'fuera-de-plazo', sello: 'sin-clave' });
      expect(conclusion(r)).toBe('no-autentico');
      // El mismo certificado un día antes del límite todavía valida.
      expect((await verificar({ retiradaEn: '2026-12-01T00:00:00Z', verificableHasta: '2026-12-10T00:00:00Z' }, new Date('2026-12-09T00:00:00Z'))).clave).toBe('anterior');
    });

    it('una retirada sin retiradaEn, o con fechas ilegibles, no valida: no se sabe hasta cuándo sellaba', async () => {
      expect((await verificar({})).clave).toBe('fuera-de-plazo');
      expect((await verificar({ retiradaEn: null })).clave).toBe('fuera-de-plazo');
      expect((await verificar({ retiradaEn: 'ayer' })).clave).toBe('fuera-de-plazo');
      expect((await verificar({ retiradaEn: '2026-12-01T00:00:00Z', verificableHasta: 'mañana' })).clave).toBe('fuera-de-plazo');
    });

    it('la clave vigente no tiene fecha límite', async () => {
      const { certificado, claves } = await certificadoSellado();
      const r = await verificarCertificado(certificado, claves, null, new Date('2099-01-01T00:00:00Z'));
      expect(r).toMatchObject({ clave: 'ok', sello: 'ok' });
    });

    it('usa la emisión sellada y no el campo emitidoEn del DTO, que la firma no cubre', async () => {
      const { certificado, claves } = await certificadoSellado();
      const [clave] = retirada(claves, { retiradaEn: '2026-11-01T00:00:00Z' }).claves;
      if (!clave) throw new Error('La prueba necesita una clave.');
      expect(claveRetiradaVale(clave, certificado, AHORA)).toBe(false);
      // Falsear el campo del DTO para que parezca anterior al retiro no cambia el resultado (además, rompe «datos distintos»).
      const falseado = { ...certificado, emitidoEn: '2026-10-01T00:00:00Z' };
      expect(claveRetiradaVale(clave, falseado, AHORA)).toBe(false);
      expect((await verificarCertificado(falseado, retirada(claves, { retiradaEn: '2026-11-01T00:00:00Z' }), null, AHORA)).contenido).toBe('datos-distintos');
    });
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
