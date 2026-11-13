import type { ImagenRgba } from './png.ts';

/**
 * Codificador de GIF animado mínimo y sin dependencias, para los GIF de éxito y de carga de la marca (correos y documentación).
 *
 * - Paleta global de hasta 255 colores por corte de mediana (el índice 255 queda como transparente para los cuadros).
 * - Cada cuadro después del primero solo guarda el rectángulo que cambió respecto al anterior, con los píxeles iguales como
 *   transparentes (disposición «no eliminar»): las animaciones de la marca tienen un fondo liso y pesan una fracción.
 * - LZW de GIF con código mínimo de 8 bits y limpieza al llenarse el diccionario.
 */

export interface CuadroDeGif {
  imagen: ImagenRgba;
  /** Cuánto se ve el cuadro, en milisegundos (el GIF los guarda en centésimas). */
  retardoMs: number;
}

export interface OpcionesDeGif {
  /** Veces que se repite; 0 = sin fin (por omisión). */
  repeticiones?: number;
}

const TRANSPARENTE = 255;

/** Escribe bytes con un acumulador que crece. */
class Bytes {
  private datos: number[] = [];
  byte(valor: number): void {
    this.datos.push(valor & 0xff);
  }
  corto(valor: number): void {
    this.byte(valor);
    this.byte(valor >> 8);
  }
  texto(valor: string): void {
    for (const letra of valor) this.byte(letra.charCodeAt(0));
  }
  bloques(contenido: Uint8Array): void {
    for (let i = 0; i < contenido.length; i += 255) {
      const parte = contenido.subarray(i, i + 255);
      this.byte(parte.length);
      for (const b of parte) this.byte(b);
    }
    this.byte(0);
  }
  salida(): Uint8Array {
    return Uint8Array.from(this.datos);
  }
}

/** Color empaquetado 0xRRGGBB. */
const empaquetar = (r: number, g: number, b: number): number => (r << 16) | (g << 8) | b;

/** Paleta de a lo más `maximo` colores por corte de mediana sobre el histograma de colores del conjunto de cuadros. */
export function crearPaleta(histograma: Map<number, number>, maximo: number): number[] {
  if (histograma.size <= maximo) return [...histograma.keys()];
  interface Caja {
    colores: [number, number][];
    total: number;
  }
  const canal = (color: number, desplazamiento: number) => (color >> desplazamiento) & 0xff;
  const cajas: Caja[] = [{ colores: [...histograma.entries()], total: [...histograma.values()].reduce((a, b) => a + b, 0) }];

  while (cajas.length < maximo) {
    // Se parte la caja con más píxeles y más de un color, por el canal de mayor rango.
    let elegida = -1;
    for (let i = 0; i < cajas.length; i += 1) {
      const caja = cajas[i];
      if (caja && caja.colores.length > 1 && (elegida < 0 || caja.total > (cajas[elegida]?.total ?? 0))) elegida = i;
    }
    if (elegida < 0) break;
    const caja = cajas[elegida];
    if (!caja) break;
    const rangos = [16, 8, 0].map((d) => {
      const valores = caja.colores.map(([c]) => canal(c, d));
      return { d, rango: Math.max(...valores) - Math.min(...valores) };
    });
    const mejor = rangos.reduce((a, b) => (b.rango > a.rango ? b : a));
    caja.colores.sort((a, b) => canal(a[0], mejor.d) - canal(b[0], mejor.d));
    let acumulado = 0;
    let corte = 0;
    for (let i = 0; i < caja.colores.length - 1; i += 1) {
      acumulado += caja.colores[i]?.[1] ?? 0;
      corte = i + 1;
      if (acumulado >= caja.total / 2) break;
    }
    const izquierda = caja.colores.slice(0, corte);
    const derecha = caja.colores.slice(corte);
    cajas.splice(
      elegida,
      1,
      { colores: izquierda, total: izquierda.reduce((a, [, n]) => a + n, 0) },
      { colores: derecha, total: derecha.reduce((a, [, n]) => a + n, 0) },
    );
  }

  return cajas.map((caja) => {
    let r = 0;
    let g = 0;
    let b = 0;
    for (const [color, n] of caja.colores) {
      r += canal(color, 16) * n;
      g += canal(color, 8) * n;
      b += canal(color, 0) * n;
    }
    return empaquetar(Math.round(r / caja.total), Math.round(g / caja.total), Math.round(b / caja.total));
  });
}

/** Compresión LZW de GIF de un cuadro de índices con código mínimo `minimo` (8). */
export function comprimirLzw(indices: Uint8Array, minimo: number): Uint8Array {
  const limpiar = 1 << minimo;
  const fin = limpiar + 1;
  let tamano = minimo + 1;
  let siguiente = fin + 1;
  const diccionario = new Map<number, number>();
  const salida: number[] = [];
  let acumulado = 0;
  let bits = 0;
  const emitir = (codigo: number) => {
    acumulado |= codigo << bits;
    bits += tamano;
    while (bits >= 8) {
      salida.push(acumulado & 0xff);
      acumulado >>>= 8;
      bits -= 8;
    }
    // Como giflib: tras escribir un código, si el siguiente libre ya llenó el tamaño actual, los códigos que siguen usan un bit más.
    if (siguiente >= 1 << tamano && tamano < 12) tamano += 1;
  };

  emitir(limpiar);
  let prefijo = indices[0] ?? 0;
  for (let i = 1; i < indices.length; i += 1) {
    const k = indices[i] ?? 0;
    const clave = (prefijo << 8) | k;
    const hallado = diccionario.get(clave);
    if (hallado !== undefined) {
      prefijo = hallado;
      continue;
    }
    emitir(prefijo);
    if (siguiente < 4095) {
      diccionario.set(clave, siguiente);
      siguiente += 1;
    } else {
      emitir(limpiar);
      diccionario.clear();
      tamano = minimo + 1;
      siguiente = fin + 1;
    }
    prefijo = k;
  }
  emitir(prefijo);
  emitir(fin);
  if (bits > 0) salida.push(acumulado & 0xff);
  return Uint8Array.from(salida);
}

/** Codifica los cuadros (todos del mismo tamaño) como un GIF animado. */
export function codificarGif(cuadros: readonly CuadroDeGif[], { repeticiones = 0 }: OpcionesDeGif = {}): Uint8Array {
  const primero = cuadros[0];
  if (!primero) throw new Error('Un GIF necesita al menos un cuadro.');
  const { ancho, alto } = primero.imagen;
  if (cuadros.some((c) => c.imagen.ancho !== ancho || c.imagen.alto !== alto)) throw new Error('Todos los cuadros deben medir lo mismo.');

  // Histograma de colores de todos los cuadros (el alfa se ignora: los cuadros llevan su fondo).
  const histograma = new Map<number, number>();
  for (const { imagen } of cuadros) {
    for (let i = 0; i < imagen.rgba.length; i += 4) {
      const color = empaquetar(imagen.rgba[i] ?? 0, imagen.rgba[i + 1] ?? 0, imagen.rgba[i + 2] ?? 0);
      histograma.set(color, (histograma.get(color) ?? 0) + 1);
    }
  }
  const paleta = crearPaleta(histograma, 255);

  const cercano = new Map<number, number>();
  const indiceDe = (color: number): number => {
    const conocido = cercano.get(color);
    if (conocido !== undefined) return conocido;
    const r = (color >> 16) & 0xff;
    const g = (color >> 8) & 0xff;
    const b = color & 0xff;
    let mejor = 0;
    let distancia = Infinity;
    for (let i = 0; i < paleta.length; i += 1) {
      const p = paleta[i] ?? 0;
      const d = (((p >> 16) & 0xff) - r) ** 2 + (((p >> 8) & 0xff) - g) ** 2 + ((p & 0xff) - b) ** 2;
      if (d < distancia) {
        distancia = d;
        mejor = i;
        if (d === 0) break;
      }
    }
    cercano.set(color, mejor);
    return mejor;
  };

  const indexados = cuadros.map(({ imagen }) => {
    const indices = new Uint8Array(ancho * alto);
    for (let i = 0; i < indices.length; i += 1) {
      indices[i] = indiceDe(empaquetar(imagen.rgba[i * 4] ?? 0, imagen.rgba[i * 4 + 1] ?? 0, imagen.rgba[i * 4 + 2] ?? 0));
    }
    return indices;
  });

  const gif = new Bytes();
  gif.texto('GIF89a');
  gif.corto(ancho);
  gif.corto(alto);
  gif.byte(0b1111_0111); // paleta global de 256 entradas, 8 bits por color
  gif.byte(0); // color de fondo
  gif.byte(0); // proporción de píxel
  for (let i = 0; i < 256; i += 1) {
    const color = paleta[i] ?? 0;
    gif.byte(color >> 16);
    gif.byte(color >> 8);
    gif.byte(color);
  }
  // Extensión de la aplicación: repetir (0 = sin fin).
  gif.byte(0x21);
  gif.byte(0xff);
  gif.byte(11);
  gif.texto('NETSCAPE2.0');
  gif.byte(3);
  gif.byte(1);
  gif.corto(repeticiones);
  gif.byte(0);

  indexados.forEach((indices, numero) => {
    const anterior = numero > 0 ? indexados[numero - 1] : undefined;
    // Rectángulo que cambió respecto al cuadro anterior (el primero va completo).
    let x0 = 0;
    let y0 = 0;
    let x1 = ancho - 1;
    let y1 = alto - 1;
    if (anterior) {
      x0 = ancho;
      y0 = alto;
      x1 = -1;
      y1 = -1;
      for (let y = 0; y < alto; y += 1) {
        for (let x = 0; x < ancho; x += 1) {
          if (indices[y * ancho + x] !== anterior[y * ancho + x]) {
            x0 = Math.min(x0, x);
            x1 = Math.max(x1, x);
            y0 = Math.min(y0, y);
            y1 = Math.max(y1, y);
          }
        }
      }
      // Sin cambios: se deja un píxel transparente para conservar el retardo.
      if (x1 < 0) [x0, y0, x1, y1] = [0, 0, 0, 0];
    }
    const w = x1 - x0 + 1;
    const h = y1 - y0 + 1;
    const recorte = new Uint8Array(w * h);
    for (let y = 0; y < h; y += 1) {
      for (let x = 0; x < w; x += 1) {
        const i = (y0 + y) * ancho + (x0 + x);
        recorte[y * w + x] = anterior && indices[i] === anterior[i] ? TRANSPARENTE : (indices[i] ?? 0);
      }
    }

    const centesimas = Math.max(2, Math.round((cuadros[numero]?.retardoMs ?? 40) / 10));
    gif.byte(0x21);
    gif.byte(0xf9);
    gif.byte(4);
    gif.byte(anterior ? 0b0000_0101 : 0b0000_0100); // disposición 1 («no eliminar»); transparencia solo después del primero
    gif.corto(centesimas);
    gif.byte(anterior ? TRANSPARENTE : 0);
    gif.byte(0);
    gif.byte(0x2c);
    gif.corto(x0);
    gif.corto(y0);
    gif.corto(w);
    gif.corto(h);
    gif.byte(0);
    gif.byte(8);
    gif.bloques(comprimirLzw(recorte, 8));
  });

  gif.byte(0x3b);
  return gif.salida();
}
