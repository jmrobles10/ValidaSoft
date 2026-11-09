/** Lectura de Mailpit (solo desarrollo, proxy `/__mailpit` de Vite): enlace del tercero y código OTP de cada correo. */

export interface ResumenCorreo {
  id: string;
  para: string;
  asunto: string;
  fecha: string;
}

interface MensajeMailpit {
  ID: string;
  To: { Address: string }[];
  Subject: string;
  Created: string;
}

export async function listarCorreos(limite = 30): Promise<ResumenCorreo[]> {
  const respuesta = await fetch(`/__mailpit/api/v1/messages?limit=${String(limite)}`);
  if (!respuesta.ok) throw new Error(`Mailpit respondió ${String(respuesta.status)}`);
  const datos = (await respuesta.json()) as { messages?: MensajeMailpit[] };
  return (datos.messages ?? []).map((m) => ({ id: m.ID, para: m.To[0]?.Address ?? '—', asunto: m.Subject, fecha: m.Created }));
}

export async function leerCorreo(id: string): Promise<string> {
  const respuesta = await fetch(`/__mailpit/api/v1/message/${encodeURIComponent(id)}`);
  if (!respuesta.ok) throw new Error(`Mailpit respondió ${String(respuesta.status)}`);
  return ((await respuesta.json()) as { Text?: string }).Text ?? '';
}

/** Ruta `/e/{token}` del enlace (sin origen: se abre en esta app) y código de 6 dígitos, si el correo los trae. */
export function extraerDeCorreo(texto: string): { enlace: string | null; codigo: string | null } {
  const enlace = /https?:\/\/[^\s/]+(\/e\/[A-Za-z0-9_-]+)/.exec(texto)?.[1] ?? null;
  const codigo = /(?<![\d])(\d{6})(?![\d])/.exec(texto)?.[1] ?? null;
  return { enlace, codigo: enlace ? null : codigo };
}
