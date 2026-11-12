/** Medidas de la firma que se guarda: un PNG de 600 × 200 con fondo blanco (HU 49093). */
export const FIRMA_ANCHO = 600;
export const FIRMA_ALTO = 200;
export const FIRMA_MARGEN = 16;
/** El servidor admite hasta 96 KB; se apunta un poco por debajo. */
export const FIRMA_MAX_BYTES = 90 * 1024;
/** Imagen que se sube: PNG o JPG de hasta 2 MB y al menos 200 × 60 px. */
export const IMAGEN_MAX_BYTES = 2 * 1024 * 1024;
export const IMAGEN_MIN_ANCHO = 200;
export const IMAGEN_MIN_ALTO = 60;
const LADO_MAXIMO_DE_TRABAJO = 1600;

export type CodigoErrorFirma = 'formato' | 'tamano' | 'dimensiones' | 'lectura' | 'vacia';

/** Lo que impide usar la imagen como firma; `codigo` se traduce en `ficha.pep.firma.errores.<código>`. */
export class ErrorDeFirma extends Error {
  constructor(readonly codigo: CodigoErrorFirma) {
    super(codigo);
    this.name = 'ErrorDeFirma';
  }
}

export interface Caja {
  x: number;
  y: number;
  ancho: number;
  alto: number;
}

/** Píxeles RGBA como los entrega `getImageData` (estructural: así se prueba sin lienzo). */
export interface PixelesRgba {
  data: Uint8ClampedArray;
  width: number;
  height: number;
}

/** Bytes que pesa un base64 ya decodificado. */
export const bytesDeBase64 = (base64: string): number => Math.floor((base64.length * 3) / 4) - (base64.endsWith('==') ? 2 : base64.endsWith('=') ? 1 : 0);

/** `data:image/png;base64,AAA…` ⇒ `AAA…`. */
export const sinPrefijo = (dataUrl: string): string => dataUrl.slice(dataUrl.indexOf(',') + 1);

/** `data:image/png;base64,…` de una firma guardada (para mostrarla). */
export const conPrefijo = (base64: string): string => `data:image/png;base64,${base64}`;

/** Ajuste «contain»: la caja más grande con la proporción de `ancho × alto` que cabe en la caja menos el margen, centrada. */
export function encajar(ancho: number, alto: number, cajaAncho: number, cajaAlto: number, margen: number): Caja {
  const libreAncho = Math.max(1, cajaAncho - 2 * margen);
  const libreAlto = Math.max(1, cajaAlto - 2 * margen);
  const escala = Math.min(libreAncho / ancho, libreAlto / alto);
  const w = ancho * escala;
  const h = alto * escala;
  return { x: (cajaAncho - w) / 2, y: (cajaAlto - h) / 2, ancho: w, alto: h };
}

/** Caja que rodea lo que no es fondo (píxel opaco y no casi blanco); `null` si el lienzo está en blanco. */
export function recortarContenido(pixeles: PixelesRgba, umbral = 245): Caja | null {
  const { data, width, height } = pixeles;
  let minX = width;
  let minY = height;
  let maxX = -1;
  let maxY = -1;
  for (let y = 0; y < height; y++) {
    for (let x = 0; x < width; x++) {
      const i = (y * width + x) * 4;
      const opaco = (data[i + 3] ?? 0) > 10;
      if (opaco && ((data[i] ?? 255) < umbral || (data[i + 1] ?? 255) < umbral || (data[i + 2] ?? 255) < umbral)) {
        if (x < minX) minX = x;
        if (x > maxX) maxX = x;
        if (y < minY) minY = y;
        if (y > maxY) maxY = y;
      }
    }
  }
  return maxX < 0 ? null : { x: minX, y: minY, ancho: maxX - minX + 1, alto: maxY - minY + 1 };
}

/** Reduce cada canal a `niveles` valores (la firma pesa mucho menos como PNG). Modifica los píxeles. */
export function posterizar(pixeles: PixelesRgba, niveles: number): void {
  const paso = 255 / (niveles - 1);
  const { data } = pixeles;
  for (let i = 0; i < data.length; i += 4) {
    for (let c = 0; c < 3; c++) data[i + c] = Math.round((data[i + c] ?? 0) / paso) * paso;
  }
}

/** ¿Sirve el archivo elegido? PNG o JPG de hasta 2 MB. */
export function errorDeArchivo(archivo: { type: string; size: number }): CodigoErrorFirma | null {
  if (archivo.type !== 'image/png' && archivo.type !== 'image/jpeg') return 'formato';
  return archivo.size > IMAGEN_MAX_BYTES ? 'tamano' : null;
}

function lienzo(ancho: number, alto: number): { lienzo: HTMLCanvasElement; contexto: CanvasRenderingContext2D } {
  const el = document.createElement('canvas');
  el.width = Math.max(1, Math.round(ancho));
  el.height = Math.max(1, Math.round(alto));
  const contexto = el.getContext('2d', { willReadFrequently: true });
  if (!contexto) throw new ErrorDeFirma('lectura');
  contexto.fillStyle = '#ffffff';
  contexto.fillRect(0, 0, el.width, el.height);
  return { lienzo: el, contexto };
}

/** Intentos de menor a mayor reducción: escala del lienzo y niveles de color (`null` = colores originales). */
const INTENTOS: readonly (readonly [number, number | null])[] = [
  [1, null],
  [1, 16],
  [1, 4],
  [0.75, 4],
  [0.5, 4],
];

/**
 * Recorta lo dibujado, lo ajusta a 600 × 200 con fondo blanco y devuelve el PNG en base64 (sin prefijo). Si pesa más de
 * 90 KB reduce colores y, si hace falta, la escala hasta que quepa. Lienzo en blanco ⇒ `vacia`.
 */
export function normalizarFuente(fuente: CanvasImageSource, ancho: number, alto: number): string {
  // Una foto grande se baja antes de recortarla: así el trabajo no depende de los megapíxeles de la imagen.
  const k = Math.min(1, LADO_MAXIMO_DE_TRABAJO / Math.max(ancho, alto));
  const { lienzo: origen, contexto } = lienzo(ancho * k, alto * k);
  contexto.drawImage(fuente, 0, 0, origen.width, origen.height);
  const caja = recortarContenido(contexto.getImageData(0, 0, origen.width, origen.height));
  if (!caja) throw new ErrorDeFirma('vacia');

  for (const [escala, niveles] of INTENTOS) {
    const { lienzo: salida, contexto: ctx } = lienzo(FIRMA_ANCHO * escala, FIRMA_ALTO * escala);
    const destino = encajar(caja.ancho, caja.alto, salida.width, salida.height, FIRMA_MARGEN * escala);
    ctx.imageSmoothingQuality = 'high';
    ctx.drawImage(origen, caja.x, caja.y, caja.ancho, caja.alto, destino.x, destino.y, destino.ancho, destino.alto);
    if (niveles !== null) {
      const pixeles = ctx.getImageData(0, 0, salida.width, salida.height);
      posterizar(pixeles, niveles);
      ctx.putImageData(pixeles, 0, 0);
    }
    const base64 = sinPrefijo(salida.toDataURL('image/png'));
    if (bytesDeBase64(base64) <= FIRMA_MAX_BYTES) return base64;
  }
  throw new ErrorDeFirma('tamano');
}

/** La firma dibujada en un lienzo: `null` si no hay trazos. */
export function normalizarLienzo(el: HTMLCanvasElement): string | null {
  try {
    return normalizarFuente(el, el.width, el.height);
  } catch (error) {
    if (error instanceof ErrorDeFirma && error.codigo === 'vacia') return null;
    throw error;
  }
}

function cargarImagen(archivo: File): Promise<HTMLImageElement> {
  return new Promise((resolver, rechazar) => {
    const url = URL.createObjectURL(archivo);
    const imagen = new Image();
    imagen.onload = () => {
      URL.revokeObjectURL(url);
      resolver(imagen);
    };
    imagen.onerror = () => {
      URL.revokeObjectURL(url);
      rechazar(new ErrorDeFirma('lectura'));
    };
    imagen.src = url;
  });
}

/** Una imagen PNG o JPG subida como firma, ya normalizada. */
export async function normalizarArchivo(archivo: File): Promise<string> {
  const error = errorDeArchivo(archivo);
  if (error) throw new ErrorDeFirma(error);
  const imagen = await cargarImagen(archivo);
  if (imagen.naturalWidth < IMAGEN_MIN_ANCHO || imagen.naturalHeight < IMAGEN_MIN_ALTO) throw new ErrorDeFirma('dimensiones');
  return normalizarFuente(imagen, imagen.naturalWidth, imagen.naturalHeight);
}
