/* Validasoft: paleta segura de una empresa (marca por empresa en public). Mismo algoritmo que Validasoft.derivarPaletaEmpresa del bundle. */
import { COLOR, type Modo } from './tokens';

export interface PaletaEmpresa {
  /** Color original: solo decorativo (fondo del logo, franja). Nunca texto. */
  marca: string;
  /** Botón primario. `textoAccion` sobre `accion` ≥ 4.5:1; `accion` sobre las superficies ≥ 3:1. */
  accion: string;
  textoAccion: string;
  /** Enlaces e ítem activo: ≥ 4.5:1 sobre superficie-nivel1 y superficie-nivel2. */
  enlace: string;
  /** Fondo de selección e indicador activo. */
  tinte: string;
}

const lin = (v: number) => (v <= 0.04045 ? v / 12.92 : ((v + 0.055) / 1.055) ** 2.4);
const delin = (v: number) => (v <= 0.0031308 ? 12.92 * v : 1.055 * v ** (1 / 2.4) - 0.055);
const aRgb = (hex: string): [number, number, number] => {
  const h = hex.replace('#', '');
  return [0, 2, 4].map((i) => parseInt(h.slice(i, i + 2), 16) / 255) as [number, number, number];
};
const aHex = (rgb: readonly number[]) =>
  '#' + rgb.map((v) => Math.round(Math.min(1, Math.max(0, v)) * 255).toString(16).padStart(2, '0')).join('').toUpperCase();
const luminancia = (hex: string) => {
  const [r, g, b] = aRgb(hex).map(lin) as [number, number, number];
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
};
export const contraste = (a: string, b: string) => {
  const x = luminancia(a);
  const y = luminancia(b);
  return (Math.max(x, y) + 0.05) / (Math.min(x, y) + 0.05);
};
const mezclar = (fg: string, bg: string, alfa: number) => {
  const f = aRgb(fg);
  const b = aRgb(bg);
  return aHex(f.map((v, i) => v * alfa + (b[i] ?? 0) * (1 - alfa)));
};

interface Oklch { L: number; C: number; H: number }

function aOklch(hex: string): Oklch {
  const [r, g, b] = aRgb(hex).map(lin) as [number, number, number];
  const l = Math.cbrt(0.4122214708 * r + 0.5363325363 * g + 0.0514459929 * b);
  const m = Math.cbrt(0.2119034982 * r + 0.6806995451 * g + 0.1073969566 * b);
  const s = Math.cbrt(0.0883024619 * r + 0.2817188376 * g + 0.6299787005 * b);
  const A = 1.9779984951 * l - 2.428592205 * m + 0.4505937099 * s;
  const B = 0.0259040371 * l + 0.7827717662 * m - 0.808675766 * s;
  return { L: 0.2104542553 * l + 0.793617785 * m - 0.0040720468 * s, C: Math.hypot(A, B), H: (Math.atan2(B, A) * 180) / Math.PI };
}

/** OKLCH → hex reduciendo croma hasta entrar en sRGB (conserva tono y luminosidad). */
function desdeOklch({ L, C, H }: Oklch): string {
  let c = C;
  for (let i = 0; i < 40; i++) {
    const A = c * Math.cos((H * Math.PI) / 180);
    const B = c * Math.sin((H * Math.PI) / 180);
    const l = (L + 0.3963377774 * A + 0.2158037573 * B) ** 3;
    const m = (L - 0.1055613458 * A - 0.0638541728 * B) ** 3;
    const s = (L - 0.0894841775 * A - 1.291485548 * B) ** 3;
    const rgb = [4.0767416621 * l - 3.3077115913 * m + 0.2309699292 * s, -1.2684380046 * l + 2.6097574011 * m - 0.3413193965 * s, -0.0041960863 * l - 0.7034186147 * m + 1.707614701 * s];
    if (rgb.every((v) => v >= -0.0005 && v <= 1.0005)) return aHex(rgb.map(delin));
    c *= 0.94;
  }
  return aHex([L, L, L]);
}

const TINTA = '#0A0F1C';

function derivarModo(hex: string, modo: Modo): PaletaEmpresa {
  const c = COLOR[modo];
  const superficies = [c.superficieNivel1, c.superficieNivel2, c.fondo];
  const base = aOklch(hex);
  const C = Math.min(base.C, 0.19);
  const peor = (color: string) => Math.min(...superficies.map((s) => contraste(color, s)));
  const buscar = (desde: number, paso: number, cumple: (color: string) => boolean): string => {
    for (let L = desde; L >= 0.05 && L <= 0.98; L += paso) {
      const color = desdeOklch({ L, C, H: base.H });
      if (cumple(color)) return color;
    }
    return paso < 0 ? TINTA : '#FFFFFF';
  };
  const claro = modo === 'light';
  const enlace = buscar(base.L, claro ? -0.005 : 0.005, (x) => peor(x) >= 4.5);
  const accion = claro
    ? buscar(base.L, -0.005, (x) => contraste('#FFFFFF', x) >= 4.5 && peor(x) >= 3)
    : buscar(Math.max(base.L, 0.55), 0.005, (x) => peor(x) >= 3 && (contraste(TINTA, x) >= 4.5 || contraste('#FFFFFF', x) >= 4.5));
  const textoAccion = claro || contraste('#FFFFFF', accion) >= 4.5 ? '#FFFFFF' : TINTA;
  return { marca: hex.toUpperCase(), accion, textoAccion, enlace, tinte: mezclar(claro ? accion : enlace, c.superficieNivel1, claro ? 0.1 : 0.16) };
}

const cache = new Map<string, PaletaEmpresa>();

/** Paleta de la empresa para un modo. Memoizada: se llama al crear el tema de public. */
export function derivarPaletaEmpresa(hex: string, modo: Modo): PaletaEmpresa {
  const clave = `${hex.toUpperCase()}|${modo}`;
  const previa = cache.get(clave);
  if (previa) return previa;
  const paleta = derivarModo(hex, modo);
  cache.set(clave, paleta);
  return paleta;
}
