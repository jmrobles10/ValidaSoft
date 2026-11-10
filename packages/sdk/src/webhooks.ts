import type { EventoPublicoDto } from './tipos.js';

/**
 * Verificación de webhooks de Validasoft (docs/05 §6, NW-5), igual que `FirmaDeWebhook.Verificar` del servidor:
 * `Validasoft-Signature: t={unix},v1={hex(HMAC-SHA256(secreto, "{t}.{cuerpo}"))}[,v1=…]` (durante una rotación del secreto
 * llega un `v1` por cada secreto vigente), comparación en tiempo constante y `t` a menos de 5 minutos. El cuerpo debe ser
 * el texto **exacto** recibido (sin `JSON.parse` + `JSON.stringify`). Se deduplica por `evento.id` (= cabecera
 * `Validasoft-Event-Id`, T-6), nunca por `Validasoft-Delivery`. Usa Web Crypto: Node 20+ o navegador.
 */

export const CABECERA_FIRMA = 'validasoft-signature';
export const CABECERA_EVENTO_ID = 'validasoft-event-id';
export const CABECERA_TIPO = 'validasoft-event-type';
export const CABECERA_ENTREGA = 'validasoft-delivery';

export const TOLERANCIA_SEGUNDOS = 300;

export type MotivoRechazoWebhook = 'cabecera-invalida' | 'fuera-de-tolerancia' | 'firma-no-coincide' | 'cuerpo-invalido';

export class WebhookInvalidoError extends Error {
  constructor(
    readonly motivo: MotivoRechazoWebhook,
    mensaje: string,
  ) {
    super(mensaje);
    this.name = 'WebhookInvalidoError';
  }
}

export interface OpcionesVerificacion {
  /** El secreto actual y, mientras dura una rotación, el anterior (`whsec_…`): basta que coincida uno. */
  secretos: string | readonly string[];
  /** Valor de `Validasoft-Signature`. */
  firma: string | null | undefined;
  /** Cuerpo exacto recibido (texto o bytes). */
  cuerpo: string | Uint8Array;
  toleranciaSegundos?: number;
  /** Hora actual (para pruebas). */
  ahora?: Date;
}

const codificador = new TextEncoder();

/** Cabeceras de `fetch` (`Headers`) o de Node (`IncomingHttpHeaders`, en minúsculas) → valor de una cabecera. */
export function leerCabecera(cabeceras: Headers | Record<string, string | string[] | undefined>, nombre: string): string | undefined {
  if (typeof Headers !== 'undefined' && cabeceras instanceof Headers) return cabeceras.get(nombre) ?? undefined;
  const registro = cabeceras as Record<string, string | string[] | undefined>;
  const clave = Object.keys(registro).find((k) => k.toLowerCase() === nombre.toLowerCase());
  const valor = clave === undefined ? undefined : registro[clave];
  return Array.isArray(valor) ? valor[0] : valor;
}

/** `null` si la firma es válida; si no, el motivo. */
export async function comprobarFirma(opciones: OpcionesVerificacion): Promise<MotivoRechazoWebhook | null> {
  let timestamp: number | undefined;
  const firmas: string[] = [];
  for (const parte of (opciones.firma ?? '').split(',')) {
    const texto = parte.trim();
    const igual = texto.indexOf('=');
    if (igual <= 0) continue;
    const clave = texto.slice(0, igual);
    const valor = texto.slice(igual + 1);
    if (clave === 't' && /^\d+$/.test(valor)) timestamp = Number(valor);
    else if (clave === 'v1' && /^[0-9a-fA-F]{64}$/.test(valor)) firmas.push(valor.toLowerCase());
  }
  if (timestamp === undefined || firmas.length === 0) return 'cabecera-invalida';

  const ahora = Math.floor((opciones.ahora ?? new Date()).getTime() / 1000);
  if (Math.abs(ahora - timestamp) > (opciones.toleranciaSegundos ?? TOLERANCIA_SEGUNDOS)) return 'fuera-de-tolerancia';

  const cuerpo = typeof opciones.cuerpo === 'string' ? codificador.encode(opciones.cuerpo) : opciones.cuerpo;
  const prefijo = codificador.encode(`${String(timestamp)}.`);
  const mensaje = new Uint8Array(prefijo.length + cuerpo.length);
  mensaje.set(prefijo, 0);
  mensaje.set(cuerpo, prefijo.length);

  const secretos = typeof opciones.secretos === 'string' ? [opciones.secretos] : opciones.secretos;
  let coincide = false;
  for (const secreto of secretos.filter((s) => s.length > 0)) {
    const esperada = await hmacHex(secreto, mensaje);
    for (const firma of firmas) coincide = igualesEnTiempoConstante(esperada, firma) || coincide;
  }
  return coincide ? null : 'firma-no-coincide';
}

/** ¿La firma es válida y reciente para este cuerpo? */
export async function verificarWebhook(opciones: OpcionesVerificacion): Promise<boolean> {
  return (await comprobarFirma(opciones)) === null;
}

/** Verifica y devuelve el evento; lanza {@link WebhookInvalidoError} (responde 401) si no es auténtico. */
export async function validarWebhook<TDatos = Record<string, unknown>>(opciones: OpcionesVerificacion): Promise<EventoPublicoDto<TDatos>> {
  const motivo = await comprobarFirma(opciones);
  if (motivo !== null) {
    const mensajes: Record<MotivoRechazoWebhook, string> = {
      'cabecera-invalida': 'Falta la cabecera Validasoft-Signature o no tiene t= y v1=.',
      'fuera-de-tolerancia': 'La firma es demasiado vieja o del futuro (revisa el reloj del servidor).',
      'firma-no-coincide': 'La firma no corresponde a ningún secreto configurado.',
      'cuerpo-invalido': 'El cuerpo no es un evento público.',
    };
    throw new WebhookInvalidoError(motivo, mensajes[motivo]);
  }
  const texto = typeof opciones.cuerpo === 'string' ? opciones.cuerpo : new TextDecoder().decode(opciones.cuerpo);
  try {
    const evento = JSON.parse(texto) as Partial<EventoPublicoDto<TDatos>> | null;
    if (!evento || typeof evento.id !== 'string' || typeof evento.tipo !== 'string') throw new Error('sin id o tipo');
    return evento as EventoPublicoDto<TDatos>;
  } catch {
    throw new WebhookInvalidoError('cuerpo-invalido', 'El cuerpo no es un evento público.');
  }
}

/** Firma como lo hace Validasoft: para probar el receptor sin el servidor. */
export async function firmarWebhook(secretos: string | readonly string[], timestamp: number, cuerpo: string): Promise<string> {
  const mensaje = codificador.encode(`${String(timestamp)}.${cuerpo}`);
  const lista = typeof secretos === 'string' ? [secretos] : secretos;
  const v1 = await Promise.all(lista.map(async (s) => `v1=${await hmacHex(s, mensaje)}`));
  return `t=${String(timestamp)},${v1.join(',')}`;
}

async function hmacHex(secreto: string, mensaje: Uint8Array<ArrayBuffer>): Promise<string> {
  const clave = await crypto.subtle.importKey('raw', codificador.encode(secreto), { name: 'HMAC', hash: 'SHA-256' }, false, ['sign']);
  const firma = new Uint8Array(await crypto.subtle.sign('HMAC', clave, mensaje));
  return Array.from(firma, (b) => b.toString(16).padStart(2, '0')).join('');
}

function igualesEnTiempoConstante(a: string, b: string): boolean {
  if (a.length !== b.length) return false;
  let diferencia = 0;
  for (let i = 0; i < a.length; i++) diferencia |= a.charCodeAt(i) ^ b.charCodeAt(i);
  return diferencia === 0;
}
