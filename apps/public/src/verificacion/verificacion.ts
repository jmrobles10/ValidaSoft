import type { CertificadoPublicoDto, ClavesDeCertificadosDto } from '@validasoft/api-client';
import { aHex, desdeBase64, desdeHex, importarClaveEd25519, sha256, SinSoporteCriptografico, verificarEd25519 } from './cripto';
import { bytesCanonicos } from './jcs';

/**
 * Verificación del certificado en el navegador (docs/05 §2.6.1, «Cómo verifica un cliente»), paso a paso:
 * 1. contenido: RFC 8785 + SHA-256 de `contenidoCanonico` = `hashContenido`, y los datos mostrados son los sellados;
 * 2. huella: la del enlace (QR) es el inicio de ese hash;
 * 3. clave: `firma.kid` es una clave publicada por la plataforma en `/.well-known/validasoft-certificados.json`;
 * 4. sello: Ed25519 de `firma.valor` (base64) sobre los 32 bytes de `hashContenido`.
 */
export type MotivoContenido = 'ok' | 'hash-distinto' | 'datos-distintos' | 'no-canonicalizable' | 'no-soportado';
export type MotivoHuella = 'ok' | 'distinta' | 'sin-huella';
export type MotivoClave = 'ok' | 'anterior' | 'desconocida' | 'no-disponible';
export type MotivoSello = 'ok' | 'invalido' | 'clave-invalida' | 'sin-clave' | 'no-soportado';

export type EstadoPaso = 'ok' | 'falla' | 'aviso' | 'no-soportado';
export type Conclusion = 'autentico' | 'no-autentico' | 'no-soportado' | 'incompleto';

export interface ResultadoVerificacion {
  contenido: MotivoContenido;
  huella: MotivoHuella;
  clave: MotivoClave;
  sello: MotivoSello;
  /** SHA-256 (hex) del JSON canónico recalculado aquí; `null` si no se pudo calcular. */
  hashCalculado: string | null;
}

/** Igual que `HuellaDeCertificado.Normalizar` de la API: los 12 primeros hexadecimales, sin separadores; `null` si no los hay. */
export function normalizarHuella(huella: string | null | undefined): string | null {
  const limpia = (huella ?? '').replace(/[^0-9a-fA-F]/g, '').toLowerCase();
  return limpia.length >= 12 ? limpia.slice(0, 12) : null;
}

function campo(raiz: unknown, ...ruta: string[]): unknown {
  let actual = raiz;
  for (const clave of ruta) {
    if (typeof actual !== 'object' || actual === null || Array.isArray(actual)) return undefined;
    actual = (actual as Record<string, unknown>)[clave];
  }
  return actual;
}

/** Instante al segundo (el contenido sellado trunca `emitidoEn` al segundo; el DTO lo trae con fracción). */
function segundos(fecha: unknown): number | null {
  if (typeof fecha !== 'string') return null;
  const ms = Date.parse(fecha.replace(/(\d{2}:\d{2}:\d{2})\.\d+/, '$1'));
  return Number.isNaN(ms) ? null : Math.floor(ms / 1000);
}

/**
 * Lo que la página muestra viene de campos del DTO que no están sellados: deben ser los del contenido canónico, si no
 * un sello válido respaldaría otros datos.
 */
export function datosCoinciden(c: CertificadoPublicoDto): boolean {
  const contenido = c.contenidoCanonico;
  const emitido = segundos(c.emitidoEn);
  return (
    campo(contenido, 'folio') === c.folio &&
    campo(contenido, 'emision') === c.emision &&
    campo(contenido, 'empresa', 'nombre') === c.emisor.empresa &&
    campo(contenido, 'tercero', 'nombre') === c.titular.nombre &&
    campo(contenido, 'tercero', 'identificacionEnmascarada') === c.titular.identificacionEnmascarada &&
    campo(contenido, 'flujo', 'nombre') === c.flujo.nombre &&
    (campo(contenido, 'vigenteHasta') ?? null) === (c.vigenteHasta ?? null) &&
    emitido !== null &&
    segundos(campo(contenido, 'emitidoEn')) === emitido
  );
}

async function verificarContenido(c: CertificadoPublicoDto): Promise<Pick<ResultadoVerificacion, 'contenido' | 'hashCalculado'>> {
  try {
    const hashCalculado = aHex(await sha256(bytesCanonicos(c.contenidoCanonico)));
    if (hashCalculado !== c.hashContenido.toLowerCase()) return { contenido: 'hash-distinto', hashCalculado };
    return { contenido: datosCoinciden(c) ? 'ok' : 'datos-distintos', hashCalculado };
  } catch (error) {
    return { contenido: error instanceof SinSoporteCriptografico ? 'no-soportado' : 'no-canonicalizable', hashCalculado: null };
  }
}

function verificarHuella(huellaUrl: string | null, hash: string): MotivoHuella {
  if (huellaUrl === null || huellaUrl.trim() === '') return 'sin-huella';
  const prefijo = normalizarHuella(huellaUrl);
  return prefijo !== null && hash.toLowerCase().startsWith(prefijo) ? 'ok' : 'distinta';
}

async function verificarSello(c: CertificadoPublicoDto, claves: ClavesDeCertificadosDto | null): Promise<Pick<ResultadoVerificacion, 'clave' | 'sello'>> {
  if (!claves) return { clave: 'no-disponible', sello: 'sin-clave' };
  const publicada = claves.claves.find((k) => k.kid === c.firma.kid);
  if (!publicada) return { clave: 'desconocida', sello: 'sin-clave' };
  const clave: MotivoClave = publicada.vigente ? 'ok' : 'anterior';

  let llave: CryptoKey;
  try {
    llave = await importarClaveEd25519(publicada.jwk.x);
  } catch (error) {
    return { clave, sello: error instanceof SinSoporteCriptografico ? 'no-soportado' : 'clave-invalida' };
  }

  const firma = desdeBase64(c.firma.valor);
  const mensaje = desdeHex(c.hashContenido);
  if (c.firma.algoritmo !== 'Ed25519' || !firma || mensaje?.length !== 32) return { clave, sello: 'invalido' };
  try {
    return { clave, sello: (await verificarEd25519(llave, firma, mensaje)) ? 'ok' : 'invalido' };
  } catch {
    return { clave, sello: 'no-soportado' };
  }
}

/** `claves = null`: no se pudieron obtener (el contenido y la huella se verifican igual). */
export async function verificarCertificado(
  c: CertificadoPublicoDto,
  claves: ClavesDeCertificadosDto | null,
  huellaUrl: string | null,
): Promise<ResultadoVerificacion> {
  const [contenido, sello] = await Promise.all([verificarContenido(c), verificarSello(c, claves)]);
  return { ...contenido, ...sello, huella: verificarHuella(huellaUrl, contenido.hashCalculado ?? c.hashContenido) };
}

export const ESTADO_DEL_PASO = {
  contenido: { ok: 'ok', 'hash-distinto': 'falla', 'datos-distintos': 'falla', 'no-canonicalizable': 'falla', 'no-soportado': 'no-soportado' },
  huella: { ok: 'ok', distinta: 'falla', 'sin-huella': 'aviso' },
  clave: { ok: 'ok', anterior: 'ok', desconocida: 'falla', 'no-disponible': 'aviso' },
  sello: { ok: 'ok', invalido: 'falla', 'clave-invalida': 'falla', 'sin-clave': 'aviso', 'no-soportado': 'no-soportado' },
} as const satisfies {
  contenido: Record<MotivoContenido, EstadoPaso>;
  huella: Record<MotivoHuella, EstadoPaso>;
  clave: Record<MotivoClave, EstadoPaso>;
  sello: Record<MotivoSello, EstadoPaso>;
};

export function estadosDePasos(r: ResultadoVerificacion): EstadoPaso[] {
  return [ESTADO_DEL_PASO.contenido[r.contenido], ESTADO_DEL_PASO.huella[r.huella], ESTADO_DEL_PASO.clave[r.clave], ESTADO_DEL_PASO.sello[r.sello]];
}

/** Una falla basta para no confiar; sin fallas, lo que no se pudo comprobar deja la conclusión abierta. */
export function conclusion(r: ResultadoVerificacion): Conclusion {
  const estados = estadosDePasos(r);
  if (estados.includes('falla')) return 'no-autentico';
  if (estados.includes('no-soportado')) return 'no-soportado';
  if (r.clave === 'no-disponible' || r.sello !== 'ok') return 'incompleto';
  return 'autentico';
}
