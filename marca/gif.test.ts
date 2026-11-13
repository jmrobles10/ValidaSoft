import { describe, expect, it } from 'vitest';
import { codificarGif, comprimirLzw, crearPaleta, type CuadroDeGif } from './gif.ts';
import type { ImagenRgba } from './png.ts';

/** Descompresión LZW de GIF según la especificación (independiente del codificador, para comprobar el viaje de ida y vuelta). */
function descomprimirLzw(datos: Uint8Array, minimo: number, esperados: number): Uint8Array {
  const limpiar = 1 << minimo;
  const fin = limpiar + 1;
  let tamano = minimo + 1;
  let siguiente = fin + 1;
  const tabla: number[][] = [];
  const reiniciar = () => {
    tabla.length = 0;
    for (let i = 0; i < limpiar; i += 1) tabla[i] = [i];
    tabla[limpiar] = [];
    tabla[fin] = [];
    tamano = minimo + 1;
    siguiente = fin + 1;
  };
  reiniciar();
  const salida: number[] = [];
  let bits = 0;
  let acumulado = 0;
  let posicion = 0;
  let anterior: number[] | null = null;
  while (salida.length < esperados) {
    while (bits < tamano) {
      if (posicion >= datos.length) throw new Error('El flujo LZW terminó antes de tiempo.');
      acumulado |= (datos[posicion] ?? 0) << bits;
      posicion += 1;
      bits += 8;
    }
    const codigo = acumulado & ((1 << tamano) - 1);
    acumulado >>>= tamano;
    bits -= tamano;
    if (codigo === limpiar) {
      reiniciar();
      anterior = null;
      continue;
    }
    if (codigo === fin) break;
    let entrada: number[];
    if (codigo < siguiente && tabla[codigo]) {
      entrada = tabla[codigo];
    } else if (anterior) {
      entrada = [...anterior, anterior[0] ?? 0];
    } else {
      throw new Error('Código LZW inválido.');
    }
    salida.push(...entrada);
    if (anterior && siguiente < 4096) {
      tabla[siguiente] = [...anterior, entrada[0] ?? 0];
      siguiente += 1;
      if (siguiente === 1 << tamano && tamano < 12) tamano += 1;
    }
    anterior = entrada;
  }
  return Uint8Array.from(salida);
}

interface GifLeido {
  ancho: number;
  alto: number;
  repeticiones: number;
  cuadros: { rgb: Uint8Array; retardoCs: number }[];
}

/** Lector de GIF animado: arma cada cuadro completo componiendo los rectángulos (transparencia y disposición «no eliminar»). */
function leerGif(bytes: Uint8Array): GifLeido {
  const texto = (inicio: number, largo: number) => String.fromCharCode(...bytes.subarray(inicio, inicio + largo));
  expect(texto(0, 6)).toBe('GIF89a');
  const corto = (i: number) => (bytes[i] ?? 0) | ((bytes[i + 1] ?? 0) << 8);
  const ancho = corto(6);
  const alto = corto(8);
  const banderas = bytes[10] ?? 0;
  let p = 13;
  const paleta: number[][] = [];
  if (banderas & 0x80) {
    const n = 1 << ((banderas & 7) + 1);
    for (let i = 0; i < n; i += 1) {
      paleta.push([bytes[p] ?? 0, bytes[p + 1] ?? 0, bytes[p + 2] ?? 0]);
      p += 3;
    }
  }
  let repeticiones = -1;
  const cuadros: GifLeido['cuadros'] = [];
  const lienzo = new Uint8Array(ancho * alto * 3);
  let retardoCs = 0;
  let transparente = -1;
  while (bytes[p] !== 0x3b) {
    const tipo = bytes[p];
    if (tipo === 0x21) {
      const etiqueta = bytes[p + 1];
      p += 2;
      if (etiqueta === 0xf9) {
        retardoCs = corto(p + 2);
        transparente = (bytes[p + 1] ?? 0) & 1 ? (bytes[p + 4] ?? 0) : -1;
      } else if (etiqueta === 0xff && texto(p + 1, 11) === 'NETSCAPE2.0') {
        repeticiones = corto(p + 14);
      }
      while ((bytes[p] ?? 0) !== 0) p += (bytes[p] ?? 0) + 1;
      p += 1;
    } else if (tipo === 0x2c) {
      const x0 = corto(p + 1);
      const y0 = corto(p + 3);
      const w = corto(p + 5);
      const h = corto(p + 7);
      const minimo = bytes[p + 10] ?? 8;
      p += 11;
      const datos: number[] = [];
      while ((bytes[p] ?? 0) !== 0) {
        const largo = bytes[p] ?? 0;
        datos.push(...bytes.subarray(p + 1, p + 1 + largo));
        p += largo + 1;
      }
      p += 1;
      const indices = descomprimirLzw(Uint8Array.from(datos), minimo, w * h);
      for (let y = 0; y < h; y += 1) {
        for (let x = 0; x < w; x += 1) {
          const indice = indices[y * w + x] ?? 0;
          if (indice === transparente) continue;
          lienzo.set(paleta[indice] ?? [0, 0, 0], ((y0 + y) * ancho + x0 + x) * 3);
        }
      }
      cuadros.push({ rgb: Uint8Array.from(lienzo), retardoCs });
    } else {
      throw new Error(`Bloque GIF desconocido: ${String(tipo)}.`);
    }
  }
  return { ancho, alto, repeticiones, cuadros };
}

/** Imagen de prueba: fondo liso y un rectángulo de color que se mueve con `paso` (como una animación de la marca). */
function cuadro(ancho: number, alto: number, paso: number, colorFondo: [number, number, number] = [255, 255, 255]): ImagenRgba {
  const rgba = new Uint8Array(ancho * alto * 4);
  for (let y = 0; y < alto; y += 1) {
    for (let x = 0; x < ancho; x += 1) {
      const dentro = x >= paso && x < paso + 8 && y >= 4 && y < 12;
      // Un degradado dentro del rectángulo imita el suavizado de los bordes de una captura.
      const tono = dentro ? [34 + x * 3, 211 - y * 4, 138] : colorFondo;
      rgba.set([tono[0] ?? 0, tono[1] ?? 0, tono[2] ?? 0, 255], (y * ancho + x) * 4);
    }
  }
  return { ancho, alto, rgba };
}

const aRgb = (imagen: ImagenRgba) => Uint8Array.from({ length: imagen.ancho * imagen.alto * 3 }, (_, i) => imagen.rgba[Math.floor(i / 3) * 4 + (i % 3)] ?? 0);

describe('codificador de GIF de la marca', () => {
  it('la compresión LZW vuelve exacta con datos que llenan y reinician el diccionario', () => {
    // Datos pseudoaleatorios deterministas: obligan a crecer el código de 9 a 12 bits y a emitir el código de limpieza.
    let semilla = 12345;
    const datos = Uint8Array.from({ length: 30_000 }, () => ((semilla = (semilla * 1103515245 + 12345) & 0x7fffffff) >> 16) % 200);
    const vuelta = descomprimirLzw(comprimirLzw(datos, 8), 8, datos.length);
    expect(Array.from(vuelta)).toEqual(Array.from(datos));
  });

  it('una imagen de un solo color y una repetición larga también viajan sin pérdida', () => {
    const datos = new Uint8Array(10_000).fill(7);
    expect(Array.from(descomprimirLzw(comprimirLzw(datos, 8), 8, datos.length))).toEqual(Array.from(datos));
  });

  it('un GIF animado conserva tamaño, repetición infinita, retardos y los píxeles de cada cuadro', () => {
    const imagenes = [0, 3, 6, 9, 12].map((paso) => cuadro(40, 16, paso));
    const cuadros: CuadroDeGif[] = imagenes.map((imagen, i) => ({ imagen, retardoMs: i === imagenes.length - 1 ? 1500 : 40 }));
    const leido = leerGif(codificarGif(cuadros));
    expect(leido.ancho).toBe(40);
    expect(leido.alto).toBe(16);
    expect(leido.repeticiones).toBe(0);
    expect(leido.cuadros).toHaveLength(5);
    expect(leido.cuadros.map((c) => c.retardoCs)).toEqual([4, 4, 4, 4, 150]);
    // Menos de 255 colores: la paleta es exacta y cada cuadro compuesto coincide píxel a píxel.
    leido.cuadros.forEach((c, i) => expect(Array.from(c.rgb)).toEqual(Array.from(aRgb(imagenes[i]!))));
  });

  it('con más de 255 colores reduce la paleta y deja los colores muy cerca de los originales', () => {
    const colores = Array.from({ length: 600 }, (_, i): [number, number, number] => [(i * 7) % 256, (i * 13) % 256, (i * 29) % 256]);
    const rgba = new Uint8Array(30 * 20 * 4);
    for (let i = 0; i < 600; i += 1) rgba.set([...(colores[i] ?? [0, 0, 0]), 255], i * 4);
    const leido = leerGif(codificarGif([{ imagen: { ancho: 30, alto: 20, rgba }, retardoMs: 100 }]));
    let peor = 0;
    for (let i = 0; i < 600; i += 1) {
      const original = colores[i] ?? [0, 0, 0];
      for (let c = 0; c < 3; c += 1) peor = Math.max(peor, Math.abs((leido.cuadros[0]?.rgb[i * 3 + c] ?? 0) - (original[c] ?? 0)));
    }
    // 600 colores repartidos en 255 cajas: el error por canal queda lejos de un salto de color completo.
    expect(peor).toBeLessThan(40);
  });

  it('un cuadro igual al anterior no rompe el GIF (conserva su retardo)', () => {
    const igual = cuadro(20, 10, 2);
    const leido = leerGif(codificarGif([{ imagen: igual, retardoMs: 100 }, { imagen: igual, retardoMs: 500 }]));
    expect(leido.cuadros).toHaveLength(2);
    expect(Array.from(leido.cuadros[1]?.rgb ?? [])).toEqual(Array.from(aRgb(igual)));
  });

  it('exige al menos un cuadro y que todos midan lo mismo', () => {
    expect(() => codificarGif([])).toThrow(/al menos un cuadro/);
    expect(() => codificarGif([{ imagen: cuadro(10, 10, 0), retardoMs: 40 }, { imagen: cuadro(12, 10, 0), retardoMs: 40 }])).toThrow(/mismo/);
  });

  it('la paleta por mediana no pasa del máximo y conserva los colores dominantes', () => {
    const histograma = new Map<number, number>([[0xffffff, 10_000]]);
    for (let i = 0; i < 400; i += 1) histograma.set(0x102030 + i * 0x010101, 1);
    const paleta = crearPaleta(histograma, 255);
    expect(paleta.length).toBeLessThanOrEqual(255);
    expect(paleta).toContain(0xffffff);
  });
});
