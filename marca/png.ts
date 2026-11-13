import { inflateSync } from 'node:zlib';

/** Imagen decodificada: píxeles RGBA de 8 bits, fila por fila. */
export interface ImagenRgba {
  ancho: number;
  alto: number;
  rgba: Uint8Array;
}

const FIRMA = [0x89, 0x50, 0x4e, 0x47, 0x0d, 0x0a, 0x1a, 0x0a];

/** Predictor de Paeth (filtro 4 de PNG). */
function paeth(a: number, b: number, c: number): number {
  const p = a + b - c;
  const pa = Math.abs(p - a);
  const pb = Math.abs(p - b);
  const pc = Math.abs(p - c);
  if (pa <= pb && pa <= pc) return a;
  return pb <= pc ? b : c;
}

/**
 * Decodifica un PNG de 8 bits por canal sin entrelazado (gris, gris con alfa, RGB, RGBA), que es lo que escribe Chromium al capturar
 * una pantalla. Sirve para pasar las capturas del generador de marca al codificador de GIF sin librerías.
 */
export function decodificarPng(bytes: Uint8Array): ImagenRgba {
  const vista = new DataView(bytes.buffer, bytes.byteOffset, bytes.byteLength);
  if (FIRMA.some((valor, i) => bytes[i] !== valor)) throw new Error('No es un PNG.');

  let ancho = 0;
  let alto = 0;
  let tipoDeColor = 0;
  const datos: Uint8Array[] = [];
  let posicion = 8;
  while (posicion < bytes.length) {
    const largo = vista.getUint32(posicion);
    const tipo = String.fromCharCode(...bytes.subarray(posicion + 4, posicion + 8));
    const contenido = bytes.subarray(posicion + 8, posicion + 8 + largo);
    if (tipo === 'IHDR') {
      ancho = vista.getUint32(posicion + 8);
      alto = vista.getUint32(posicion + 12);
      if (bytes[posicion + 16] !== 8 || bytes[posicion + 20] !== 0) throw new Error('Solo se leen PNG de 8 bits sin entrelazado.');
      tipoDeColor = bytes[posicion + 17] ?? 0;
    } else if (tipo === 'IDAT') {
      datos.push(contenido);
    } else if (tipo === 'IEND') {
      break;
    }
    posicion += 12 + largo;
  }

  const canales = ({ 0: 1, 2: 3, 4: 2, 6: 4 } as Record<number, number | undefined>)[tipoDeColor];
  if (!canales) throw new Error(`Tipo de color PNG no admitido: ${String(tipoDeColor)}.`);
  const crudo = inflateSync(Buffer.concat(datos));
  const fila = ancho * canales;
  const anterior = new Uint8Array(fila);
  const actual = new Uint8Array(fila);
  const rgba = new Uint8Array(ancho * alto * 4);

  for (let y = 0; y < alto; y += 1) {
    const inicio = y * (fila + 1);
    const filtro = crudo[inicio] ?? 0;
    for (let x = 0; x < fila; x += 1) {
      const valor = crudo[inicio + 1 + x] ?? 0;
      const izquierda = x >= canales ? (actual[x - canales] ?? 0) : 0;
      const arriba = anterior[x] ?? 0;
      const arribaIzquierda = x >= canales ? (anterior[x - canales] ?? 0) : 0;
      switch (filtro) {
        case 0:
          actual[x] = valor;
          break;
        case 1:
          actual[x] = (valor + izquierda) & 0xff;
          break;
        case 2:
          actual[x] = (valor + arriba) & 0xff;
          break;
        case 3:
          actual[x] = (valor + ((izquierda + arriba) >> 1)) & 0xff;
          break;
        case 4:
          actual[x] = (valor + paeth(izquierda, arriba, arribaIzquierda)) & 0xff;
          break;
        default:
          throw new Error(`Filtro PNG desconocido: ${String(filtro)}.`);
      }
    }
    for (let x = 0; x < ancho; x += 1) {
      const origen = x * canales;
      const destino = (y * ancho + x) * 4;
      if (canales === 1 || canales === 2) {
        const gris = actual[origen] ?? 0;
        rgba.set([gris, gris, gris, canales === 2 ? (actual[origen + 1] ?? 255) : 255], destino);
      } else {
        rgba.set([actual[origen] ?? 0, actual[origen + 1] ?? 0, actual[origen + 2] ?? 0, canales === 4 ? (actual[origen + 3] ?? 255) : 255], destino);
      }
    }
    anterior.set(actual);
  }
  return { ancho, alto, rgba };
}
