/**
 * Primitivas del sello con WebCrypto, sin dependencias: SHA-256 y Ed25519 (RFC 8032). La API sella con Ed25519 los
 * 32 bytes del SHA-256 del contenido canónico (no el texto hexadecimal) y publica la clave como JWK OKP (RFC 8037).
 */

/** No hay `crypto.subtle` (contexto no seguro: http fuera de localhost) o el navegador no implementa el algoritmo. */
export class SinSoporteCriptografico extends Error {
  override readonly name = 'SinSoporteCriptografico';
}

function subtle(): SubtleCrypto {
  // En un contexto no seguro `crypto.subtle` no existe aunque los tipos del DOM digan lo contrario.
  const disponible = (globalThis.crypto as Crypto | undefined)?.subtle;
  if (!disponible) throw new SinSoporteCriptografico('Este navegador no expone WebCrypto (crypto.subtle).');
  return disponible;
}

export async function sha256(bytes: Uint8Array<ArrayBuffer>): Promise<Uint8Array<ArrayBuffer>> {
  return new Uint8Array(await subtle().digest('SHA-256', bytes));
}

export function aHex(bytes: Uint8Array): string {
  return Array.from(bytes, (b) => b.toString(16).padStart(2, '0')).join('');
}

/** Hexadecimal par a bytes; `null` si no es hexadecimal. */
export function desdeHex(hex: string): Uint8Array<ArrayBuffer> | null {
  if (hex.length % 2 !== 0 || !/^[0-9a-fA-F]*$/.test(hex)) return null;
  const bytes = new Uint8Array(hex.length / 2);
  for (let i = 0; i < bytes.length; i++) bytes[i] = Number.parseInt(hex.slice(i * 2, i * 2 + 2), 16);
  return bytes;
}

/** Base64 estándar o base64url, con o sin relleno; `null` si no lo es. */
export function desdeBase64(texto: string): Uint8Array<ArrayBuffer> | null {
  const normal = texto.trim().replace(/-/g, '+').replace(/_/g, '/');
  if (!/^[A-Za-z0-9+/]*={0,2}$/.test(normal)) return null;
  try {
    const binario = atob(normal.padEnd(Math.ceil(normal.length / 4) * 4, '='));
    return Uint8Array.from(binario, (c) => c.charCodeAt(0));
  } catch {
    return null;
  }
}

/** El navegador no implementa el algoritmo (Safari o Chrome antiguos con Ed25519). */
function esNoSoportado(error: unknown): boolean {
  return error instanceof DOMException && error.name === 'NotSupportedError';
}

/**
 * Importa la clave pública Ed25519 desde su coordenada `x` (base64url, 32 bytes) como JWK mínimo: se omiten `alg`,
 * `use` y `kid` para no depender de cómo valida cada navegador esos campos opcionales.
 * Lanza `SinSoporteCriptografico` si el navegador no soporta Ed25519 y `RangeError` si la clave no es válida.
 */
export async function importarClaveEd25519(x: string): Promise<CryptoKey> {
  const bytes = desdeBase64(x);
  if (bytes?.length !== 32) throw new RangeError('La clave pública Ed25519 debe tener 32 bytes.');
  try {
    return await subtle().importKey('jwk', { kty: 'OKP', crv: 'Ed25519', x }, { name: 'Ed25519' }, false, ['verify']);
  } catch (error) {
    if (error instanceof SinSoporteCriptografico || esNoSoportado(error)) throw new SinSoporteCriptografico('Este navegador no verifica Ed25519.');
    throw new RangeError('La clave pública publicada no es una clave Ed25519 válida.');
  }
}

/** Verifica una firma Ed25519 (64 bytes) sobre `mensaje`. Lanza `SinSoporteCriptografico` si el navegador no la soporta. */
export async function verificarEd25519(clave: CryptoKey, firma: Uint8Array<ArrayBuffer>, mensaje: Uint8Array<ArrayBuffer>): Promise<boolean> {
  if (firma.length !== 64) return false;
  try {
    return await subtle().verify({ name: 'Ed25519' }, clave, firma, mensaje);
  } catch (error) {
    if (esNoSoportado(error)) throw new SinSoporteCriptografico('Este navegador no verifica Ed25519.');
    return false;
  }
}
