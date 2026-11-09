import { configuracionCliente, type ClavePublicaDeCertificadosDto, type ClavesDeCertificadosDto } from '@validasoft/api-client';

/**
 * Claves públicas del sello (`ClavesDeCertificadosDto`). La API las publica en `{UrlPublica}/.well-known/…`, el mismo
 * origen de esta página (no van en el cliente generado). Se piden siempre a esa ruta fija y no a `firma.clavePublicaUrl`:
 * si la respuesta del certificado estuviera alterada, también podría apuntar a otra clave.
 */
export const RUTA_CLAVES = '/.well-known/validasoft-certificados.json';

export class ClavesNoDisponibles extends Error {
  override readonly name = 'ClavesNoDisponibles';
}

function esObjeto(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

function esClave(valor: unknown): valor is ClavePublicaDeCertificadosDto {
  if (!esObjeto(valor) || typeof valor.kid !== 'string' || typeof valor.vigente !== 'boolean' || !esObjeto(valor.jwk)) return false;
  const { kty, crv, x } = valor.jwk;
  return kty === 'OKP' && crv === 'Ed25519' && typeof x === 'string';
}

/** Valida la forma del documento: una página HTML (p. ej. sin proxy en desarrollo) o un JSON ajeno no es una clave. */
export function leerClaves(valor: unknown): ClavesDeCertificadosDto {
  if (!esObjeto(valor) || !Array.isArray(valor.claves) || typeof valor.algoritmo !== 'string') {
    throw new ClavesNoDisponibles('El documento de claves no tiene la forma esperada.');
  }
  return {
    emisor: typeof valor.emisor === 'string' ? valor.emisor : '',
    algoritmo: valor.algoritmo,
    mensajeFirmado: typeof valor.mensajeFirmado === 'string' ? valor.mensajeFirmado : '',
    esquemas: Array.isArray(valor.esquemas) ? valor.esquemas.filter((e): e is string => typeof e === 'string') : [],
    claves: valor.claves.filter(esClave),
  };
}

/** Usa el `fetch` configurado del cliente (inyectable en pruebas), sin credenciales: el documento es público. */
export async function obtenerClaves(signal?: AbortSignal): Promise<ClavesDeCertificadosDto> {
  let respuesta: Response;
  try {
    respuesta = await configuracionCliente().fetch(RUTA_CLAVES, { headers: { Accept: 'application/json' }, credentials: 'omit', signal: signal ?? null });
  } catch (error) {
    throw new ClavesNoDisponibles(error instanceof Error ? error.message : 'Sin conexión.');
  }
  if (!respuesta.ok) throw new ClavesNoDisponibles(`Respuesta ${String(respuesta.status)} al pedir las claves.`);
  let cuerpo: unknown;
  try {
    cuerpo = await respuesta.json();
  } catch {
    throw new ClavesNoDisponibles('El documento de claves no es JSON.');
  }
  return leerClaves(cuerpo);
}
