/**
 * Orígenes permitidos de un sistema integrado: los sitios desde los que se puede incrustar Validasoft en un iframe
 * (`frame-ancestors` y el puente `postMessage`). Misma regla que la API (`OrigenWeb.Normalizar`), para avisar antes de
 * guardar; la API sigue siendo quien decide (422 `origen-invalido`, `origenes-excesivos`):
 * - `https://host[:puerto]`, sin ruta, consulta, fragmento, usuario ni comodines; esquema y host en minúsculas y sin el
 *   puerto por defecto.
 * - `http` solo hacia localhost, 127.0.0.1 o [::1], y la API solo lo acepta en desarrollo.
 * - Hasta {@link MAXIMO_ORIGENES} por sistema; los repetidos no cuentan.
 */
export const MAXIMO_ORIGENES = 20;

const HOSTS_LOCALES = new Set(['localhost', '127.0.0.1', '[::1]']);

export type ErrorOrigen = 'vacio' | 'formato' | 'solo-https' | 'repetido' | 'excesivos';

export type ResultadoOrigen = { origen: string } | { error: ErrorOrigen };

/** Normaliza lo escrito («https://SRM.cliente.co/» → «https://srm.cliente.co») o dice por qué no es un origen. */
export function normalizarOrigen(texto: string): ResultadoOrigen {
  let limpio = texto.trim();
  if (!limpio) return { error: 'vacio' };
  if (limpio.endsWith('/')) limpio = limpio.slice(0, -1);
  if (limpio.includes('*') || !/^https?:\/\/[^/?#]+$/i.test(limpio)) return { error: 'formato' };
  let url: URL;
  try {
    url = new URL(limpio);
  } catch {
    return { error: 'formato' };
  }
  if (url.username || url.password || !url.hostname) return { error: 'formato' };
  if (url.protocol === 'http:' && !HOSTS_LOCALES.has(url.hostname)) return { error: 'solo-https' };
  // `URL` ya deja el host en minúsculas (y en punycode) y quita el puerto por defecto.
  return { origen: url.origin };
}

/** Agrega un origen a la lista (normalizado) o dice por qué no se puede. */
export function agregarOrigen(lista: readonly string[], texto: string): { lista: string[] } | { error: ErrorOrigen } {
  const resultado = normalizarOrigen(texto);
  if ('error' in resultado) return resultado;
  if (lista.includes(resultado.origen)) return { error: 'repetido' };
  if (lista.length >= MAXIMO_ORIGENES) return { error: 'excesivos' };
  return { lista: [...lista, resultado.origen] };
}

/** Varias líneas (al registrar un sistema): los válidos normalizados, sin repetidos, y la primera línea con error. */
export function origenesDeTexto(texto: string): { origenes: string[]; error: { linea: string; error: ErrorOrigen } | null } {
  let origenes: string[] = [];
  for (const linea of texto.split(/[\n,]/).map((l) => l.trim()).filter(Boolean)) {
    const paso = agregarOrigen(origenes, linea);
    if ('lista' in paso) origenes = paso.lista;
    else if (paso.error !== 'repetido') return { origenes, error: { linea, error: paso.error } };
  }
  return { origenes, error: null };
}

export function mismosOrigenes(a: readonly string[], b: readonly string[]): boolean {
  return a.length === b.length && a.every((o, i) => o === b[i]);
}
