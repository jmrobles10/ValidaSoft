import { describe, expect, it } from 'vitest';
import { bytesDeBase64, conPrefijo, encajar, errorDeArchivo, IMAGEN_MAX_BYTES, posterizar, recortarContenido, sinPrefijo } from './normalizarFirma';

describe('normalizarFirma', () => {
  it('encaja la firma en 600 × 200 con margen, centrada y sin deformarla', () => {
    const ancha = encajar(300, 100, 600, 200, 16);
    expect(ancha.ancho / ancha.alto).toBeCloseTo(3);
    expect(ancha.alto).toBeCloseTo(168);
    expect(ancha.x + ancha.ancho / 2).toBeCloseTo(300);
    expect(ancha.y + ancha.alto / 2).toBeCloseTo(100);

    const cuadrada = encajar(100, 100, 600, 200, 16);
    expect(cuadrada.alto).toBe(168);
    expect(cuadrada.ancho).toBe(168);
    expect(cuadrada.x).toBe((600 - 168) / 2);
    expect(cuadrada.y).toBe(16);
  });

  it('recorta lo dibujado y detecta el lienzo en blanco', () => {
    const ancho = 6;
    const alto = 4;
    const data = new Uint8ClampedArray(ancho * alto * 4).fill(255);
    expect(recortarContenido({ data, width: ancho, height: alto })).toBeNull();
    // Un trazo negro opaco en (2,1) y (3,2).
    for (const [x, y] of [
      [2, 1],
      [3, 2],
    ] as const) {
      data.set([0, 0, 0, 255], (y * ancho + x) * 4);
    }
    expect(recortarContenido({ data, width: ancho, height: alto })).toEqual({ x: 2, y: 1, ancho: 2, alto: 2 });
  });

  it('un píxel transparente no cuenta como trazo', () => {
    const data = new Uint8ClampedArray(4 * 4).fill(0);
    expect(recortarContenido({ data, width: 2, height: 2 })).toBeNull();
  });

  it('posteriza reduciendo los valores de cada canal sin tocar la transparencia', () => {
    const pixeles = { data: new Uint8ClampedArray([10, 130, 250, 200]), width: 1, height: 1 };
    posterizar(pixeles, 2);
    expect([...pixeles.data]).toEqual([0, 255, 255, 200]);
  });

  it('cuenta los bytes de un base64 y quita o pone el prefijo', () => {
    expect(bytesDeBase64('QUJD')).toBe(3);
    expect(bytesDeBase64('QUI=')).toBe(2);
    expect(bytesDeBase64('QQ==')).toBe(1);
    expect(sinPrefijo('data:image/png;base64,QUJD')).toBe('QUJD');
    expect(conPrefijo('QUJD')).toBe('data:image/png;base64,QUJD');
  });

  it('solo acepta PNG o JPG de hasta 2 MB', () => {
    expect(errorDeArchivo({ type: 'image/png', size: 1000 })).toBeNull();
    expect(errorDeArchivo({ type: 'image/jpeg', size: IMAGEN_MAX_BYTES })).toBeNull();
    expect(errorDeArchivo({ type: 'application/pdf', size: 1000 })).toBe('formato');
    expect(errorDeArchivo({ type: 'image/gif', size: 1000 })).toBe('formato');
    expect(errorDeArchivo({ type: 'image/png', size: IMAGEN_MAX_BYTES + 1 })).toBe('tamano');
  });
});
