/**
 * JSON Canonicalization Scheme (RFC 8785), el mismo que usa la API para hashear el certificado
 * (`Validasoft.Dominio.Certificados.Jcs`): claves de cada objeto ordenadas por unidades de código UTF-16, sin espacios,
 * cadenas con el escape mínimo de ECMAScript (solo `"`, `\` y controles; lo demás va literal) y números con la
 * serialización de `Number.prototype.toString` (`-0` es `0`). Lo que no es JSON válido para JCS (números no finitos,
 * sustitutos UTF-16 sueltos, `undefined`, funciones…) lanza un error en lugar de producir otros bytes.
 */
export function canonicalizar(valor: unknown): string {
  if (valor === null) return 'null';
  switch (typeof valor) {
    case 'boolean':
      return valor ? 'true' : 'false';
    case 'number':
      return numero(valor);
    case 'string':
      return cadena(valor);
    case 'object':
      return Array.isArray(valor) ? arreglo(valor) : objeto(valor);
    default:
      throw new TypeError(`Valor no admitido en JSON canónico: ${typeof valor}.`);
  }
}

/** UTF-8 del JSON canónico: los bytes que entran al SHA-256. */
export function bytesCanonicos(valor: unknown): Uint8Array<ArrayBuffer> {
  return new TextEncoder().encode(canonicalizar(valor));
}

/** ES2015 §7.1.12.1, que es exactamente lo que hace `JSON.stringify` con un número finito (incluido `-0` ⇒ `0`). */
function numero(valor: number): string {
  if (!Number.isFinite(valor)) throw new RangeError('RFC 8785 no admite NaN ni infinitos.');
  return JSON.stringify(valor);
}

const SUSTITUTO_SUELTO = /[\uD800-\uDBFF](?![\uDC00-\uDFFF])|(?<![\uD800-\uDBFF])[\uDC00-\uDFFF]/;

/**
 * `JSON.stringify` de una cadena ya aplica el escape de JCS: `\b \t \n \f \r` cortos, otros controles `\u00xx` en
 * minúsculas, `"` y `\`; el resto (incluido U+2028/U+2029 y lo no ASCII) va literal. Un sustituto suelto no es Unicode
 * válido: `JSON.stringify` lo escaparía, JCS lo rechaza.
 */
function cadena(texto: string): string {
  if (SUSTITUTO_SUELTO.test(texto)) throw new RangeError('La cadena tiene un sustituto UTF-16 suelto: no es Unicode válido.');
  return JSON.stringify(texto);
}

function arreglo(valores: readonly unknown[]): string {
  return `[${valores.map(canonicalizar).join(',')}]`;
}

/**
 * El orden por defecto de `Array.prototype.sort` compara unidades de código UTF-16, que es el que pide RFC 8785 §3.2.3
 * (no el de los puntos de código ni el del idioma).
 */
function objeto(valor: object): string {
  const entradas = Object.entries(valor);
  for (const [, v] of entradas) {
    if (v === undefined) throw new TypeError('JSON no tiene `undefined`: el objeto no es canonicalizable.');
  }
  const claves = entradas.map(([clave]) => clave).sort();
  const porClave = new Map(entradas);
  return `{${claves.map((clave) => `${cadena(clave)}:${canonicalizar(porClave.get(clave))}`).join(',')}}`;
}
