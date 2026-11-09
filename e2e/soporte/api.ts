import { createSign, generateKeyPairSync, randomUUID } from 'node:crypto';

/** Origen de Validasoft bajo prueba (el mismo de `use.baseURL`). */
export const API = process.env.E2E_BASE_URL ?? `http://localhost:${process.env.E2E_PUERTO ?? '5090'}`;

/** Clave de plataforma de desarrollo (appsettings.Development.json); en otro entorno, E2E_CLAVE_PLATAFORMA. */
export const CLAVE_PLATAFORMA = process.env.E2E_CLAVE_PLATAFORMA ?? 'dev-plataforma-solo-local-cambiar-0001';

export interface Peticion {
  llave: string;
  empresa?: string;
  cuerpo?: unknown;
  delegado?: string;
  ifMatch?: number;
}

export class ErrorDeApi extends Error {
  constructor(
    readonly estado: number,
    readonly codigo: string | undefined,
    mensaje: string,
  ) {
    super(mensaje);
  }
}

/** Llamada a la API como lo haría el backend de un sistema integrado (API key, X-Empresa, identidad delegada). */
export async function llamar<T>(metodo: string, ruta: string, { llave, empresa, cuerpo, delegado, ifMatch }: Peticion): Promise<T> {
  const cabeceras: Record<string, string> = { Accept: 'application/json', 'X-Api-Key': llave };
  if (cuerpo !== undefined) cabeceras['Content-Type'] = 'application/json';
  if (empresa) cabeceras['X-Empresa'] = empresa;
  if (delegado) cabeceras['X-Usuario-Delegado'] = delegado;
  if (ifMatch !== undefined) cabeceras['If-Match'] = `"${String(ifMatch)}"`;
  if (metodo === 'POST') cabeceras['Idempotency-Key'] = randomUUID();

  const respuesta = await fetch(`${API}${ruta}`, { method: metodo, headers: cabeceras, body: cuerpo === undefined ? undefined : JSON.stringify(cuerpo) });
  const texto = await respuesta.text();
  const datos: unknown = texto ? JSON.parse(texto) : undefined;
  if (!respuesta.ok) {
    const problema = (datos ?? {}) as { codigo?: string; detail?: string };
    throw new ErrorDeApi(respuesta.status, problema.codigo, `${metodo} ${ruta} → ${String(respuesta.status)} ${problema.codigo ?? ''}: ${problema.detail ?? texto}`);
  }
  return datos as T;
}

/** Par de claves RSA del sistema integrado (firma las aserciones de identidad delegada). */
export function clavesDeSistema(): { privada: string; publica: string } {
  const { privateKey, publicKey } = generateKeyPairSync('rsa', {
    modulusLength: 2048,
    publicKeyEncoding: { type: 'spki', format: 'pem' },
    privateKeyEncoding: { type: 'pkcs8', format: 'pem' },
  });
  return { privada: privateKey, publica: publicKey };
}

export interface PersonaDelSistema {
  sub: string;
  nombre: string;
  correo: string;
}

/** Aserción `X-Usuario-Delegado` (ADR-0011): JWT RS256 de 5 minutos con `jti` de un solo uso (SE-4). */
export function asercion(sistema: string, clavePrivada: string, persona: PersonaDelSistema): string {
  const b64 = (valor: string | Buffer) => Buffer.from(valor).toString('base64url');
  const ahora = Math.floor(Date.now() / 1000);
  const cabecera = b64(JSON.stringify({ alg: 'RS256', typ: 'JWT' }));
  const carga = b64(
    JSON.stringify({ iss: sistema, aud: 'validasoft', sub: persona.sub, name: persona.nombre, email: persona.correo, jti: randomUUID(), iat: ahora, exp: ahora + 300 }),
  );
  const firma = createSign('RSA-SHA256').update(`${cabecera}.${carga}`).sign(clavePrivada);
  return `${cabecera}.${carga}.${b64(firma)}`;
}

/** Dígito de verificación del NIT (DIAN). */
export function dvNit(numero: string): string {
  const pesos = [3, 7, 13, 17, 19, 23, 29, 37, 41, 43, 47, 53, 59, 67, 71];
  let suma = 0;
  for (let i = 0; i < numero.length; i++) suma += Number(numero.charAt(numero.length - 1 - i)) * (pesos[i] ?? 0);
  const resto = suma % 11;
  return String(resto > 1 ? 11 - resto : resto);
}

/** NIT aleatorio de persona jurídica (9 dígitos, empieza por 9) con su dígito de verificación. */
export function nitAleatorio(): { pais: string; tipo: string; numero: string; digitoVerificacion: string } {
  const numero = `9${String(Math.floor(Math.random() * 1e8)).padStart(8, '0')}`;
  return { pais: 'CO', tipo: 'NIT', numero, digitoVerificacion: dvNit(numero) };
}
