/**
 * Passkeys (WebAuthn) con la API nativa del navegador (`navigator.credentials`). El servidor entrega las opciones con los binarios en
 * base64url (Fido2NetLib) y espera la respuesta con los binarios en base64url: este módulo hace esa traducción y traduce los fallos del
 * navegador a causas que la pantalla sabe explicar. No guarda nada: la clave privada nunca sale del autenticador.
 */

export type CausaDePasskey = 'cancelada' | 'no-soportada' | 'ya-registrada' | 'otra';

/** Fallo de la ceremonia en el navegador (no de la API): la persona canceló, el dispositivo no puede o ya tenía la passkey. */
export class ErrorDePasskey extends Error {
  override readonly name = 'ErrorDePasskey';

  constructor(
    readonly causa: CausaDePasskey,
    mensaje: string,
  ) {
    super(mensaje);
  }
}

export function esErrorDePasskey(error: unknown): error is ErrorDePasskey {
  return error instanceof ErrorDePasskey;
}

/** `navigator.credentials` con `PublicKeyCredential` (HTTPS o localhost). */
export function soportaPasskeys(): boolean {
  return typeof window !== 'undefined' && 'PublicKeyCredential' in window && typeof navigator.credentials.create === 'function';
}

export function base64UrlABytes(texto: string): Uint8Array<ArrayBuffer> {
  const base64 = texto.replace(/-/g, '+').replace(/_/g, '/');
  const relleno = base64.length % 4 === 0 ? '' : '='.repeat(4 - (base64.length % 4));
  const binario = atob(base64 + relleno);
  const bytes = new Uint8Array(new ArrayBuffer(binario.length));
  for (let i = 0; i < binario.length; i += 1) bytes[i] = binario.charCodeAt(i);
  return bytes;
}

export function bytesABase64Url(bytes: ArrayBuffer | Uint8Array): string {
  const datos = bytes instanceof Uint8Array ? bytes : new Uint8Array(bytes);
  let binario = '';
  for (const byte of datos) binario += String.fromCharCode(byte);
  return btoa(binario).replace(/\+/g, '-').replace(/\//g, '_').replace(/=+$/, '');
}

type Json = Record<string, unknown>;

function objeto(valor: unknown, campo: string): Json {
  if (typeof valor !== 'object' || valor === null || Array.isArray(valor)) throw new ErrorDePasskey('otra', `Las opciones de la passkey no traen ${campo}.`);
  return valor as Json;
}

function texto(valor: unknown, campo: string): string {
  if (typeof valor !== 'string' || valor.length === 0) throw new ErrorDePasskey('otra', `Las opciones de la passkey no traen ${campo}.`);
  return valor;
}

function credenciales(valor: unknown): PublicKeyCredentialDescriptor[] {
  if (!Array.isArray(valor)) return [];
  return valor.map((item) => {
    const c = objeto(item, 'una credencial');
    const transportes = Array.isArray(c.transports) ? (c.transports as AuthenticatorTransport[]) : undefined;
    return { type: 'public-key', id: base64UrlABytes(texto(c.id, 'el id de la credencial')), ...(transportes && { transports: transportes }) };
  });
}

/** Opciones JSON del servidor → `PublicKeyCredentialCreationOptions` (desafío, usuario y credenciales excluidas como binarios). */
export function opcionesDeRegistro(json: Json): PublicKeyCredentialCreationOptions {
  const rp = objeto(json.rp, 'rp');
  const usuario = objeto(json.user, 'user');
  const seleccion = typeof json.authenticatorSelection === 'object' && json.authenticatorSelection !== null ? (json.authenticatorSelection as AuthenticatorSelectionCriteria) : undefined;
  const parametros: PublicKeyCredentialParameters[] = Array.isArray(json.pubKeyCredParams)
    ? (json.pubKeyCredParams as PublicKeyCredentialParameters[])
    : [{ type: 'public-key', alg: -7 }];

  return {
    rp: { id: typeof rp.id === 'string' ? rp.id : undefined, name: texto(rp.name, 'rp.name') },
    user: { id: base64UrlABytes(texto(usuario.id, 'user.id')), name: texto(usuario.name, 'user.name'), displayName: texto(usuario.displayName, 'user.displayName') },
    challenge: base64UrlABytes(texto(json.challenge, 'challenge')),
    pubKeyCredParams: parametros,
    timeout: typeof json.timeout === 'number' ? json.timeout : undefined,
    excludeCredentials: credenciales(json.excludeCredentials),
    ...(seleccion && { authenticatorSelection: seleccion }),
    attestation: json.attestation === 'direct' || json.attestation === 'indirect' || json.attestation === 'enterprise' ? json.attestation : 'none',
  };
}

/** Opciones JSON del servidor → `PublicKeyCredentialRequestOptions`. Sin credenciales permitidas, el navegador ofrece las del sitio. */
export function opcionesDeFirma(json: Json): PublicKeyCredentialRequestOptions {
  return {
    challenge: base64UrlABytes(texto(json.challenge, 'challenge')),
    rpId: typeof json.rpId === 'string' ? json.rpId : undefined,
    timeout: typeof json.timeout === 'number' ? json.timeout : undefined,
    allowCredentials: credenciales(json.allowCredentials),
    userVerification: json.userVerification === 'discouraged' || json.userVerification === 'preferred' ? json.userVerification : 'required',
  };
}

/** Lo que el servidor espera de un registro: `AuthenticatorAttestationRawResponse` con binarios en base64url. */
// `type` y no `interface`: el cliente generado pide JSON (`JsonElement`) y una interfaz no tiene la firma de índice implícita.
// eslint-disable-next-line @typescript-eslint/consistent-type-definitions
export type RespuestaDeRegistro = {
  id: string;
  rawId: string;
  type: 'public-key';
  response: { attestationObject: string; clientDataJSON: string; transports: string[] };
  clientExtensionResults: Record<string, unknown>;
};

/** Lo que el servidor espera de una firma: `AuthenticatorAssertionRawResponse` con binarios en base64url. */
// eslint-disable-next-line @typescript-eslint/consistent-type-definitions -- ver `RespuestaDeRegistro`
export type RespuestaDeFirma = {
  id: string;
  rawId: string;
  type: 'public-key';
  response: { authenticatorData: string; clientDataJSON: string; signature: string; userHandle: string | null };
  clientExtensionResults: Record<string, unknown>;
};

export function respuestaDeRegistro(credencial: PublicKeyCredential): RespuestaDeRegistro {
  const respuesta = credencial.response as AuthenticatorAttestationResponse;
  return {
    id: credencial.id,
    rawId: bytesABase64Url(credencial.rawId),
    type: 'public-key',
    response: {
      attestationObject: bytesABase64Url(respuesta.attestationObject),
      clientDataJSON: bytesABase64Url(respuesta.clientDataJSON),
      transports: typeof respuesta.getTransports === 'function' ? respuesta.getTransports() : [],
    },
    clientExtensionResults: {},
  };
}

export function respuestaDeFirma(credencial: PublicKeyCredential): RespuestaDeFirma {
  const respuesta = credencial.response as AuthenticatorAssertionResponse;
  return {
    id: credencial.id,
    rawId: bytesABase64Url(credencial.rawId),
    type: 'public-key',
    response: {
      authenticatorData: bytesABase64Url(respuesta.authenticatorData),
      clientDataJSON: bytesABase64Url(respuesta.clientDataJSON),
      signature: bytesABase64Url(respuesta.signature),
      userHandle: respuesta.userHandle && respuesta.userHandle.byteLength > 0 ? bytesABase64Url(respuesta.userHandle) : null,
    },
    clientExtensionResults: {},
  };
}

/** Traduce el fallo del navegador: cancelar o agotar el tiempo (`NotAllowedError`), passkey repetida (`InvalidStateError`), sin soporte. */
export function traducirFalloDelNavegador(error: unknown): ErrorDePasskey {
  if (esErrorDePasskey(error)) return error;
  const nombre = error instanceof DOMException || error instanceof Error ? error.name : '';
  if (nombre === 'NotAllowedError' || nombre === 'AbortError') return new ErrorDePasskey('cancelada', 'La persona canceló o se agotó el tiempo.');
  if (nombre === 'InvalidStateError') return new ErrorDePasskey('ya-registrada', 'El dispositivo ya tiene una passkey de esta cuenta.');
  if (nombre === 'NotSupportedError' || nombre === 'SecurityError') return new ErrorDePasskey('no-soportada', 'El navegador o el dispositivo no admiten passkeys para este sitio.');
  return new ErrorDePasskey('otra', error instanceof Error ? error.message : 'No se pudo completar la passkey.');
}

/** `navigator.credentials.create` con las opciones del servidor; devuelve la respuesta lista para enviar. */
export async function crearPasskey(opciones: Json): Promise<RespuestaDeRegistro> {
  if (!soportaPasskeys()) throw new ErrorDePasskey('no-soportada', 'Este navegador no admite passkeys.');
  try {
    const credencial = await navigator.credentials.create({ publicKey: opcionesDeRegistro(opciones) });
    if (!(credencial instanceof PublicKeyCredential)) throw new ErrorDePasskey('otra', 'El navegador no devolvió una passkey.');
    return respuestaDeRegistro(credencial);
  } catch (error) {
    throw traducirFalloDelNavegador(error);
  }
}

/** `navigator.credentials.get` con las opciones del servidor; devuelve la firma lista para enviar. */
export async function firmarConPasskey(opciones: Json, senal?: AbortSignal): Promise<RespuestaDeFirma> {
  if (!soportaPasskeys()) throw new ErrorDePasskey('no-soportada', 'Este navegador no admite passkeys.');
  try {
    const credencial = await navigator.credentials.get({ publicKey: opcionesDeFirma(opciones), ...(senal && { signal: senal }) });
    if (!(credencial instanceof PublicKeyCredential)) throw new ErrorDePasskey('otra', 'El navegador no devolvió una passkey.');
    return respuestaDeFirma(credencial);
  } catch (error) {
    throw traducirFalloDelNavegador(error);
  }
}

/** Nombre que se propone para la passkey según el navegador y el sistema («Chrome en Windows»). */
export function nombreSugeridoDePasskey(userAgent: string): string {
  const navegador = userAgent.includes('Edg/') ? 'Edge' : /OPR\/|Opera/.test(userAgent) ? 'Opera' : userAgent.includes('Firefox/') ? 'Firefox' : userAgent.includes('Chrome/') ? 'Chrome' : userAgent.includes('Safari/') ? 'Safari' : null;
  const sistema = userAgent.includes('Windows') ? 'Windows' : userAgent.includes('Android') ? 'Android' : /iPhone|iPad|iPod/.test(userAgent) ? 'iOS' : /Mac OS X|Macintosh/.test(userAgent) ? 'macOS' : userAgent.includes('Linux') ? 'Linux' : null;
  if (navegador && sistema) return `${navegador} en ${sistema}`;
  return navegador ?? sistema ?? 'Mi passkey';
}
