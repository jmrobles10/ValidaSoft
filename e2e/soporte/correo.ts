/** Bandeja de Mailpit (infra/compose): las invitaciones, códigos y avisos que envía Validasoft llegan aquí. */
const MAILPIT = process.env.E2E_MAILPIT ?? 'http://localhost:8025';

export interface Correo {
  id: string;
  asunto: string;
  texto: string;
  html: string;
}

interface ResumenMailpit {
  ID: string;
  Subject: string;
  Created: string;
}

/**
 * Espera el correo más reciente para `destinatario` cuyo asunto cumpla `asunto` (y que no esté en `excluir`, para
 * pedir «el siguiente»). Cada prueba usa direcciones únicas, así que no hace falta vaciar la bandeja.
 */
export async function esperarCorreo(destinatario: string, asunto: RegExp, { excluir = [] as string[], segundos = 30 } = {}): Promise<Correo> {
  const limite = Date.now() + segundos * 1000;
  let vistos: string[] = [];
  while (Date.now() < limite) {
    const respuesta = await fetch(`${MAILPIT}/api/v1/search?query=${encodeURIComponent(`to:"${destinatario}"`)}&limit=50`);
    if (respuesta.ok) {
      const { messages } = (await respuesta.json()) as { messages: ResumenMailpit[] };
      vistos = messages.map((m) => m.Subject);
      const candidato = messages
        .filter((m) => asunto.test(m.Subject) && !excluir.includes(m.ID))
        .sort((a, b) => b.Created.localeCompare(a.Created))[0];
      if (candidato) {
        const detalle = (await (await fetch(`${MAILPIT}/api/v1/message/${candidato.ID}`)).json()) as { Text: string; HTML: string; Subject: string };
        return { id: candidato.ID, asunto: detalle.Subject, texto: detalle.Text, html: detalle.HTML };
      }
    }
    await new Promise((r) => setTimeout(r, 500));
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
