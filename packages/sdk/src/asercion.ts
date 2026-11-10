/**
 * Aserción `X-Usuario-Delegado` (ADR-0011, SE-4): JWT RS256 (clave RSA ≥ 2048) o ES256 (EC P-256) con `iss` = código del
 * sistema, `aud` = `validasoft`, `sub`, `name`, `email`, un `jti` nuevo y vencimiento de como mucho 5 minutos. Validasoft
 * rechaza un `jti` repetido: se firma **una por petición** (el cliente lo hace solo). La clave privada va en PEM PKCS#8
 * (`-----BEGIN PRIVATE KEY-----`); la pública correspondiente se registra en `POST /api/v1/sistemas`.
 */

/** Persona del sistema integrado en cuyo nombre se actúa (`sub` = su id en el sistema). */
export interface UsuarioDelegado {
  sub: string;
  nombre: string;
  correo: string;
}

export const AUDIENCIA = 'validasoft';
export const VIGENCIA_MAXIMA_SEGUNDOS = 300;

export interface FirmanteDeAserciones {
  readonly codigoSistema: string;
  firmar(usuario: UsuarioDelegado, vigenciaSegundos?: number): Promise<string>;
}

const codificador = new TextEncoder();

function base64Url(datos: Uint8Array | string): string {
  const bytes = typeof datos === 'string' ? codificador.encode(datos) : datos;
  let binario = '';
  for (const b of bytes) binario += String.fromCharCode(b);
  return btoa(binario).replace(/=+$/, '').replace(/\+/g, '-').replace(/\//g, '_');
}

function derDePem(pem: string): Uint8Array<ArrayBuffer> {
  if (!pem.includes('BEGIN PRIVATE KEY')) throw new Error('La clave privada debe ir en PEM PKCS#8 (-----BEGIN PRIVATE KEY-----).');
  const base64 = pem.replace(/-----[^-]+-----/g, '').replace(/\s+/g, '');
  const binario = atob(base64);
  const der = new Uint8Array(binario.length);
  for (let i = 0; i < binario.length; i++) der[i] = binario.charCodeAt(i);
  return der;
}

interface ClaveImportada {
  clave: CryptoKey;
  alg: 'RS256' | 'ES256';
  firma: AlgorithmIdentifier | EcdsaParams;
}

async function importar(pem: string): Promise<ClaveImportada> {
  const der = derDePem(pem);
  try {
    const clave = await crypto.subtle.importKey('pkcs8', der, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);
    const bits = (clave.algorithm as RsaHashedKeyAlgorithm).modulusLength;
    if (bits < 2048) throw new RangeError('La clave RSA debe tener al menos 2048 bits.');
    return { clave, alg: 'RS256', firma: { name: 'RSASSA-PKCS1-v1_5' } };
  } catch (error) {
    if (error instanceof RangeError) throw error;
    // JWS (RFC 7518 §3.4) usa r||s de 64 bytes: es justo lo que devuelve Web Crypto con ECDSA.
    const clave = await crypto.subtle.importKey('pkcs8', der, { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign']);
    return { clave, alg: 'ES256', firma: { name: 'ECDSA', hash: 'SHA-256' } };
  }
}

/**
 * Firmante con la clave privada del sistema (`codigoSistema` = el registrado en Validasoft, p. ej. `SRM`).
 * `reloj` permite fijar la hora en pruebas.
 */
export function crearFirmanteDeAserciones(codigoSistema: string, clavePrivadaPem: string, reloj: () => Date = () => new Date()): FirmanteDeAserciones {
  const codigo = codigoSistema.trim();
  if (!codigo) throw new Error('Indica el código del sistema integrado.');
  const importada = importar(clavePrivadaPem);
  // Si la clave no sirve, el error sale en la primera firma; se evita un rechazo sin manejar mientras tanto.
  importada.catch(() => undefined);

  return {
    codigoSistema: codigo,
    async firmar(usuario, vigenciaSegundos = 120) {
      if (vigenciaSegundos <= 0 || vigenciaSegundos > VIGENCIA_MAXIMA_SEGUNDOS) throw new RangeError('La aserción debe vencer en 5 minutos o menos.');
      const { clave, alg, firma } = await importada;
      const ahora = Math.floor(reloj().getTime() / 1000);
      const cabecera = base64Url(JSON.stringify({ alg, typ: 'JWT' }));
      const carga = base64Url(
        JSON.stringify({
          iss: codigo,
          aud: AUDIENCIA,
          sub: usuario.sub,
          name: usuario.nombre,
          email: usuario.correo,
          jti: crypto.randomUUID(),
          iat: ahora,
          nbf: ahora - 5,
          exp: ahora + vigenciaSegundos,
        }),
      );
      const datos = `${cabecera}.${carga}`;
      const sello = new Uint8Array(await crypto.subtle.sign(firma, clave, codificador.encode(datos)));
      return `${datos}.${base64Url(sello)}`;
    },
  };
}
