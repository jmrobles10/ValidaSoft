/** Bandeja de Mailpit (infra/compose): las invitaciones, códigos y avisos que envía Validasoft llegan aquí. */
const MAILPIT = process.env.E2E_MAILPIT ?? 'http://localhost:8025';

export interface Correo {
  id: string;
  asunto: string;
  texto: string;
  html: string;
  /** Dirección del remitente (`From`). */
  desde: string;
}

interface ResumenMailpit {
  ID: string;
  Subject: string;
  Created: string;
}

const pausa = (ms: number) => new Promise((resolver) => setTimeout(resolver, ms));

/**
 * Los mensajes de la bandeja para `destinatario`, o `null` si Mailpit no contestó. Con la máquina cargada, el relevo de puertos de Docker corta alguna
 * conexión (`fetch failed`): eso no es un fallo de la aplicación, así que quien consulta decide si reintenta.
 */
async function consultar(destinatario: string): Promise<ResumenMailpit[] | null> {
  try {
    const respuesta = await fetch(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:"${destinatario}"`)}&limit=50`);
    return respuesta.ok ? ((await respuesta.json()) as { messages: ResumenMailpit[] }).messages : null;
  } catch {
    return null;
  }
}

/** Lo que ya llegó a `destinatario` (sin esperar). Reintenta si un corte momentáneo de la conexión con Mailpit impide leer la bandeja. */
export async function mensajesDe(destinatario: string): Promise<{ ID: string; Subject: string }[]> {
  for (let intento = 0; intento < 20; intento += 1) {
    const mensajes = await consultar(destinatario);
    if (mensajes) return mensajes;
    await pausa(500);
  }
  throw new Error(`No se pudo leer la bandeja de Mailpit (${MAILPIT}) para ${destinatario}.`);
}

/** El detalle de un mensaje, o `null` si Mailpit no contestó (un corte momentáneo de la conexión). */
async function detalleDe(id: string): Promise<{ Text: string; HTML: string; Subject: string; From?: { Address: string } } | null> {
  try {
    const respuesta = await fetch(`${MAILPIT}/api/v1/message/${id}`);
    return respuesta.ok ? ((await respuesta.json()) as { Text: string; HTML: string; Subject: string; From?: { Address: string } }) : null;
  } catch {
    return null;
  }
}

/**
 * Espera el correo más reciente para `destinatario` cuyo asunto cumpla `asunto` (y que no esté en `excluir`, para
 * pedir «el siguiente»). Cada prueba usa direcciones únicas, así que no hace falta vaciar la bandeja.
 */
export async function esperarCorreo(destinatario: string, asunto: RegExp, { excluir = [] as string[], segundos = 30 } = {}): Promise<Correo> {
  const limite = Date.now() + segundos * 1000;
  let vistos: string[] = [];
  while (Date.now() < limite) {
    const messages = await consultar(destinatario);
    if (messages) {
      vistos = messages.map((m) => m.Subject);
      const candidato = messages
        .filter((m) => asunto.test(m.Subject) && !excluir.includes(m.ID))
        .sort((a, b) => b.Created.localeCompare(a.Created))[0];
      const detalle = candidato ? await detalleDe(candidato.ID) : null;
      if (candidato && detalle) {
        return { id: candidato.ID, asunto: detalle.Subject, texto: detalle.Text, html: detalle.HTML, desde: detalle.From?.Address ?? '' };
      }
    }
    await pausa(500);
  }
  throw new Error(`No llegó a ${destinatario} un correo con asunto ${String(asunto)}. Asuntos recibidos: ${vistos.join(' | ') || '(ninguno)'}`);
}

/** Primer enlace del correo que contenga `fragmento` (p. ej. `/e/` del enlace mágico). */
export function enlace(correo: Correo, fragmento: string): string {
  const encontrado = [...correo.texto.matchAll(/https?:\/\/[^\s<>"')\]]+/g)].map((m) => m[0]).find((u) => u.includes(fragmento));
  if (!encontrado) throw new Error(`El correo «${correo.asunto}» no trae un enlace con ${fragmento}:\n${correo.texto}`);
  return encontrado;
}

/** Código numérico de un solo uso (OTP) del correo. */
export function codigo(correo: Correo): string {
  const encontrado = /\b(\d{6})\b/.exec(correo.texto)?.[1];
  if (!encontrado) throw new Error(`El correo «${correo.asunto}» no trae un código de 6 dígitos:\n${correo.texto}`);
  return encontrado;
}

/** Texto literal dentro de una expresión regular (nombres con puntos, paréntesis…). */
export function literal(texto: string): string {
  return texto.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');
}

/**
 * El texto del correo tal como lo lee la persona, sin las direcciones: los enlaces llevan un token aleatorio que, por azar, puede contener un
 * nombre («Olga») y hacer fallar una comprobación de «no nombra a nadie» que nada tiene que ver con el contenido.
 */
export function textoSinEnlaces(texto: string): string {
  return texto.replace(/https?:\/\/\S+/g, '');
}
