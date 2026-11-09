/**
 * Solo desarrollo y demo: hace en el navegador lo que en producción hace el **backend** del sistema integrado
 * (ADR-0011). Firma la aserción `X-Usuario-Delegado` con la clave privada del sistema y la canjea, junto con su API
 * key, por la sesión corta de Validasoft. Una clave privada nunca debe llegar a un navegador fuera de esta demo.
 */
import { crearSesionUsuario, type SesionUsuarioDto } from '@validasoft/api-client';

export interface PersonaDelegada {
  sub: string;
  nombre: string;
  correo: string;
}

export interface CredencialesSistema {
  apiKey: string;
  sistema: string;
  clavePrivadaPem: string;
}

/** Lo que `pnpm semilla:dev` deja en apps/web/.semilla.local.json. */
export interface SemillaDev {
  apiKey: string;
  clavePrivadaPem: string;
  sistema: { id: string; codigo: string };
  empresas: { id: string; nombre: string }[];
  usuarios: (PersonaDelegada & { descripcion: string })[];
}

const VIGENCIA_ASERCION_S = 300;

function base64Url(bytes: Uint8Array): string {
  let binario = '';
  for (const b of bytes) binario += String.fromCharCode(b);
  return btoa(binario).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

function textoABase64Url(texto: string): string {
  return base64Url(new TextEncoder().encode(texto));
}

function pemADer(pem: string): ArrayBuffer {
  const cuerpo = pem.replace(/-----(BEGIN|END) [A-Z ]+-----/g, '').replace(/\s+/g, '');
  const binario = atob(cuerpo);
  const bytes = new Uint8Array(binario.length);
  for (let i = 0; i < binario.length; i++) bytes[i] = binario.charCodeAt(i);
  return bytes.buffer;
}

async function importarClave(pem: string): Promise<{ clave: CryptoKey; alg: 'RS256' | 'ES256' }> {
  if (!pem.includes('BEGIN PRIVATE KEY')) {
    throw new Error('La clave privada debe estar en formato PEM PKCS#8 ("BEGIN PRIVATE KEY").');
  }
  const der = pemADer(pem);
  try {
    const clave = await crypto.subtle.importKey('pkcs8', der, { name: 'RSASSA-PKCS1-v1_5', hash: 'SHA-256' }, false, ['sign']);
    return { clave, alg: 'RS256' };
  } catch {
    const clave = await crypto.subtle.importKey('pkcs8', der, { name: 'ECDSA', namedCurve: 'P-256' }, false, ['sign']);
    return { clave, alg: 'ES256' };
  }
}

/**
 * JWT RS256/ES256 de 5 minutos: `iss` = código del sistema, `aud` = validasoft, `sub`, `name`, `email` y `jti`
 * aleatorio. Cada aserción vale **una sola vez** (SE-4): se firma una nueva para cada petición.
 */
export async function firmarAsercion(
  sistema: string,
  clavePrivadaPem: string,
  persona: PersonaDelegada,
  ahora = new Date(),
  jti: string = crypto.randomUUID(),
): Promise<string> {
  const { clave, alg } = await importarClave(clavePrivadaPem);
  const iat = Math.floor(ahora.getTime() / 1000);
  const cabecera = textoABase64Url(JSON.stringify({ alg, typ: 'JWT' }));
  const carga = textoABase64Url(
    JSON.stringify({
      iss: sistema,
      aud: 'validasoft',
      sub: persona.sub,
      name: persona.nombre,
      email: persona.correo,
      jti,
      iat,
      exp: iat + VIGENCIA_ASERCION_S,
    }),
  );
  const algoritmo = alg === 'RS256' ? { name: 'RSASSA-PKCS1-v1_5' } : { name: 'ECDSA', hash: 'SHA-256' };
  const firma = await crypto.subtle.sign(algoritmo, clave, new TextEncoder().encode(`${cabecera}.${carga}`));
  return `${cabecera}.${carga}.${base64Url(new Uint8Array(firma))}`;
}

/** `POST /api/v1/sesiones-usuario` con la API key del sistema y la aserción (sin token ni empresa). */
export function canjearSesion(apiKey: string, asercion: string): Promise<SesionUsuarioDto> {
  return crearSesionUsuario({
    token: null,
    empresa: null,
    headers: { 'X-Api-Key': apiKey, 'X-Usuario-Delegado': asercion },
  });
}

export async function abrirSesionDelegada(credenciales: CredencialesSistema, persona: PersonaDelegada): Promise<SesionUsuarioDto> {
  const asercion = await firmarAsercion(credenciales.sistema, credenciales.clavePrivadaPem, persona);
  return canjearSesion(credenciales.apiKey, asercion);
}

export async function cargarSemilla(): Promise<SemillaDev | null> {
  try {
    const respuesta = await fetch('/__dev/semilla.json');
    if (!respuesta.ok) return null;
    const datos = (await respuesta.json()) as Partial<SemillaDev>;
    return datos.apiKey && datos.clavePrivadaPem && datos.sistema && datos.usuarios ? (datos as SemillaDev) : null;
  } catch {
    return null;
  }
}
