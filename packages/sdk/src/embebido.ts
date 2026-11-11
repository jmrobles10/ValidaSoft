/**
 * Lado del sistema integrador del embebido del tercero (IO-5, docs/06 §3.2): monta el iframe de Validasoft y le entrega la sesión por
 * `postMessage`. Corre en el navegador del sistema anfitrión; **la API key nunca llega aquí**: la sesión
 * (`POST /api/v1/sesiones-tercero`, ver {@link ClienteValidasoft.sesiones}) la pide el backend del sistema y la página solo recibe su
 * resultado. Sin dependencias.
 *
 * Protocolo (versión 1), todo por `window.postMessage` entre el anfitrión y el iframe:
 *
 *   iframe → anfitrión   `{ tipo: 'validasoft:ready', version }`                       el iframe está listo para recibir la sesión
 *                        `{ tipo: 'validasoft:sesion-requerida', motivo }`             falta la sesión o está por vencer: pedir otra
 *                        `{ tipo: 'validasoft:resize', height }`                        alto del contenido, en píxeles
 *   anfitrión → iframe   `{ tipo: 'validasoft:sesion', token }`                        el token de la sesión embebida
 *                        `{ tipo: 'validasoft:tema', mode: 'light' | 'dark' }`
 *
 * Reglas de seguridad: el anfitrión solo atiende mensajes que vienen **de su propio iframe y del origen de Validasoft**, y solo le
 * envía el token a ese origen exacto (nunca `*`); el iframe, a su vez, solo acepta el token del origen para el que se emitió.
 */

export const VERSION_PROTOCOLO = 1;

export type ComponenteEmbebible = 'boveda' | 'expediente' | 'ficha';

export type MotivoSesionRequerida = 'inicial' | 'por-vencer' | 'vencida' | 'rechazada';

/** Lo que devuelve `POST /api/v1/sesiones-tercero` (el backend del sistema lo pasa a la página). */
export interface SesionEmbebidaDto {
  token: string;
  /** ISO 8601. */
  expira: string;
  /** URL del iframe (sin el token). */
  url: string;
  /** Origen de la página anfitriona para el que se emitió. */
  origen: string;
  componente: ComponenteEmbebible;
  vinculoId: string;
  terceroId: string;
}

export type MensajeDelEmbebido =
  | { tipo: 'validasoft:ready'; version: number }
  | { tipo: 'validasoft:sesion-requerida'; motivo: MotivoSesionRequerida }
  | { tipo: 'validasoft:resize'; height: number };

export type MensajeAlEmbebido = { tipo: 'validasoft:sesion'; token: string } | { tipo: 'validasoft:tema'; mode: 'light' | 'dark' };

const MOTIVOS: readonly string[] = ['inicial', 'por-vencer', 'vencida', 'rechazada'];

function esRegistro(valor: unknown): valor is Record<string, unknown> {
  return typeof valor === 'object' && valor !== null && !Array.isArray(valor);
}

/** Valida la forma de un mensaje que llega del iframe; cualquier otra cosa se ignora. */
export function interpretarMensajeDelEmbebido(datos: unknown): MensajeDelEmbebido | null {
  if (!esRegistro(datos) || typeof datos.tipo !== 'string') return null;
  switch (datos.tipo) {
    case 'validasoft:ready':
      return typeof datos.version === 'number' ? { tipo: 'validasoft:ready', version: datos.version } : null;
    case 'validasoft:sesion-requerida':
      return typeof datos.motivo === 'string' && MOTIVOS.includes(datos.motivo)
        ? { tipo: 'validasoft:sesion-requerida', motivo: datos.motivo as MotivoSesionRequerida }
        : null;
    case 'validasoft:resize':
      return typeof datos.height === 'number' && Number.isFinite(datos.height) && datos.height >= 0
        ? { tipo: 'validasoft:resize', height: Math.ceil(datos.height) }
        : null;
    default:
      return null;
  }
}

/** Origen (`esquema://host[:puerto]`) de una URL; lanza si no es absoluta. */
export function origenDeUrl(url: string): string {
  return new URL(url).origin;
}

export interface OpcionesDeMontaje {
  /** Elemento donde se inserta el iframe. */
  contenedor: HTMLElement;
  /** La sesión inicial (`POST /api/v1/sesiones-tercero` desde el backend). */
  sesion: SesionEmbebidaDto;
  /**
   * Pide una sesión nueva a **su backend** cuando la actual está por vencer o venció (o Validasoft la rechazó). Sin esta función la
   * sesión no se renueva y el iframe avisa por `alMensaje` con `validasoft:sesion-requerida`.
   */
  renovarSesion?: (motivo: MotivoSesionRequerida) => Promise<SesionEmbebidaDto>;
  /** Cada mensaje válido del iframe (listo, sesión requerida, alto…), por si el anfitrión quiere reaccionar. */
  alMensaje?: (mensaje: MensajeDelEmbebido) => void;
  /** Ajusta el alto del iframe al del contenido (por omisión sí). Con `false` el iframe conserva el alto que tenga. */
  ajustarAlto?: boolean;
  /** Título accesible del iframe. */
  titulo?: string;
  /** Alto inicial en píxeles (480 por omisión). */
  altoInicial?: number;
}

export interface EmbebidoMontado {
  readonly iframe: HTMLIFrameElement;
  /** Cambia el tema del contenido embebido. */
  enviarTema: (mode: 'light' | 'dark') => void;
  /** Quita el iframe y deja de escuchar. */
  cerrar: () => void;
}

/**
 * Monta el iframe y lo mantiene con sesión: espera su `ready`, le entrega el token, atiende `sesion-requerida` pidiendo otra a su
 * backend y (si se pide) ajusta el alto. Devuelve un manejador para cambiar el tema o desmontar.
 */
export function montarEmbebido(opciones: OpcionesDeMontaje): EmbebidoMontado {
  let sesion = opciones.sesion;
  let origenDelIframe = origenDeUrl(sesion.url);
  const iframe = document.createElement('iframe');
  iframe.title = opciones.titulo ?? 'Validasoft';
  iframe.src = sesion.url;
  iframe.style.cssText = `display:block;width:100%;height:${String(opciones.altoInicial ?? 480)}px;border:0`;
  iframe.setAttribute('referrerpolicy', 'origin');
  let renovando = false;

  const enviar = (mensaje: MensajeAlEmbebido) => iframe.contentWindow?.postMessage(mensaje, origenDelIframe);

  const entregarSesion = () => enviar({ tipo: 'validasoft:sesion', token: sesion.token });

  const alRecibir = (evento: MessageEvent) => {
    // Solo del iframe que montamos y del origen de Validasoft: cualquier otra ventana se ignora.
    if (evento.source !== iframe.contentWindow || evento.origin !== origenDelIframe) return;
    const mensaje = interpretarMensajeDelEmbebido(evento.data);
    if (!mensaje) return;

    if (mensaje.tipo === 'validasoft:ready') {
      entregarSesion();
    } else if (mensaje.tipo === 'validasoft:sesion-requerida') {
      if (mensaje.motivo === 'inicial') {
        entregarSesion();
      } else if (opciones.renovarSesion && !renovando) {
        renovando = true;
        opciones
          .renovarSesion(mensaje.motivo)
          .then((nueva) => {
            sesion = nueva;
            origenDelIframe = origenDeUrl(nueva.url);
            entregarSesion();
          })
          .catch(() => undefined)
          .finally(() => {
            renovando = false;
          });
      }
    } else if (opciones.ajustarAlto !== false) {
      iframe.style.height = `${String(mensaje.height)}px`;
    }

    opciones.alMensaje?.(mensaje);
  };

  window.addEventListener('message', alRecibir);
  opciones.contenedor.appendChild(iframe);

  return {
    iframe,
    enviarTema: (mode) => enviar({ tipo: 'validasoft:tema', mode }),
    cerrar: () => {
      window.removeEventListener('message', alRecibir);
      iframe.remove();
    },
  };
}

// ---------- Lado del iframe (las apps de Validasoft que se embeben) ----------

/** Ruta pública (sin credencial) con los orígenes que pueden alojar las pantallas embebidas. */
export const RUTA_ORIGENES_PERMITIDOS = '/public/v1/embed/origenes-permitidos';

/** Cada cuánto se vuelve a pedir la lista (la API la publica con `vigenciaSegundos: 60`). */
export const RECARGA_ORIGENES_MS = 60_000;

export function origenPermitido(origen: string, permitidos: readonly string[]): boolean {
  return origen !== 'null' && permitidos.includes(origen);
}

/**
 * Lista de orígenes permitidos: el propio (la demo embebida vive en el mismo origen), los que publica la API y los de
 * `VITE_ORIGENES_CONTENEDOR`. Estos últimos son respaldo: entran si aún no hay lista de la API (`remotos` nulo, porque falló o no
 * ha llegado) o si `incluirConfigurados` (desarrollo).
 */
export function origenesPermitidos(
  configurados: string | undefined,
  propio: string,
  remotos: readonly string[] | null = null,
  incluirConfigurados = false,
): string[] {
  const lista = (configurados ?? '')
    .split(',')
    .map((o) => o.trim().replace(/\/$/, ''))
    .filter(Boolean);
  const respaldo = remotos === null || incluirConfigurados ? lista : [];
  return [...new Set([propio, ...(remotos ?? []), ...respaldo])];
}

/** Pide a la API los orígenes permitidos. Devuelve `null` si no responde o la respuesta no tiene la forma esperada (se usa el respaldo). */
export async function cargarOrigenesPermitidos(baseUrl = '', fetchFn: typeof fetch = (...a) => globalThis.fetch(...a)): Promise<string[] | null> {
  try {
    const respuesta = await fetchFn(`${baseUrl}${RUTA_ORIGENES_PERMITIDOS}`, { headers: { Accept: 'application/json' } });
    if (!respuesta.ok) return null;
    const datos: unknown = await respuesta.json();
    if (!esRegistro(datos) || !Array.isArray(datos.origenes)) return null;
    return datos.origenes.filter((o): o is string => typeof o === 'string' && /^https?:\/\/[^/]+$/.test(o));
  } catch {
    return null;
  }
}

/**
 * Lista vigente tras una consulta: si falló, se queda la última buena (y si nunca hubo, `null` = respaldo); si no cambió, la misma
 * referencia, para no volver a anunciar `ready` al contenedor cada minuto.
 */
export function siguienteListaDeOrigenes(previa: readonly string[] | null | undefined, nueva: string[] | null): readonly string[] | null {
  if (nueva === null) return previa ?? null;
  if (previa?.length === nueva.length && previa.every((o, i) => o === nueva[i])) return previa;
  return nueva;
}

/**
 * Origen del contenedor: `ancestorOrigins` (Chromium) o el `referrer`. Solo se usa si está permitido, para no entregar mensajes a una
 * página que no debería alojar a Validasoft.
 */
export function origenDelContenedor(ventana: Pick<Window, 'location' | 'document'>, permitidos: readonly string[]): string | null {
  const ancestros = ventana.location.ancestorOrigins as DOMStringList | undefined;
  const candidato = ancestros?.[0] ?? (ventana.document.referrer ? new URL(ventana.document.referrer).origin : null);
  return candidato && origenPermitido(candidato, permitidos) ? candidato : null;
}

export function estaEmbebida(ventana: Window = window): boolean {
  try {
    return ventana.self !== ventana.top;
  } catch {
    return true; // acceder a `top` de otro origen lanza: estamos en un iframe
  }
}

/** Reclamos de un JWT sin verificar su firma (la verifica la API): lo que la app embebida necesita para comprobar a quién va dirigido. */
export function leerReclamosDeJwt(token: string): Record<string, unknown> | null {
  const partes = token.split('.');
  if (partes.length !== 3 || !partes[1]) return null;
  try {
    const base64 = partes[1].replace(/-/g, '+').replace(/_/g, '/');
    const bytes = Uint8Array.from(atob(base64.padEnd(Math.ceil(base64.length / 4) * 4, '=')), (c) => c.charCodeAt(0));
    const datos: unknown = JSON.parse(new TextDecoder().decode(bytes));
    return esRegistro(datos) ? datos : null;
  } catch {
    return null;
  }
}
