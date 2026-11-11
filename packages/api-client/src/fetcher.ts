import { configuracionCliente } from './configuracion';
import { ErrorApi, ErrorDeRed, errorDesdeProblema } from './errores';

/** Lo que orval pasa al mutator en cada operación generada. */
export interface ConfigPeticion {
  url: string;
  method: 'GET' | 'POST' | 'PUT' | 'PATCH' | 'DELETE' | 'get' | 'post' | 'put' | 'patch' | 'delete';
  params?: object;
  data?: unknown;
  headers?: Record<string, string>;
  signal?: AbortSignal;
}

/** Opciones por llamada (segundo argumento de cada función generada, `request` en los hooks). */
export interface OpcionesPeticion {
  /** Versión del recurso leída antes (`VinculoDto.version`) ⇒ `If-Match: "n"`; la API responde 412 si cambió. */
  ifMatch?: number | string;
  /** Clave de idempotencia propia (p. ej. una por formulario abierto) para que los reintentos no dupliquen. */
  idempotencyKey?: string;
  /** Sobrescribe la empresa activa para esta llamada; `null` la omite. */
  empresa?: string | null;
  /** Sobrescribe el token de la sesión (p. ej. para canjear credenciales). `null` lo omite. */
  token?: string | null;
  /** Cabeceras adicionales (`X-Api-Key`, `X-Usuario-Delegado` al canjear la sesión). */
  headers?: Record<string, string>;
}

/**
 * POST que crean recursos y por eso exigen `Idempotency-Key` (lista de `MiddlewareDeIdempotencia` en la API).
 * Si la llamada no trae una clave propia, el fetcher genera una.
 */
export const RUTAS_QUE_CREAN: readonly string[] = [
  '/api/v1/plataforma/tenants',
  '/api/v1/empresas',
  '/api/v1/api-keys',
  '/api/v1/sistemas',
  '/api/v1/usuarios',
  '/api/v1/tipos-vinculo',
  '/api/v1/areas',
  '/api/v1/terceros',
  '/api/v1/vinculos',
  '/api/v1/motivos',
  '/api/v1/flujos',
  '/api/v1/tipos-documento',
];

export function construirUrl(baseUrl: string, ruta: string, params?: object): string {
  const busqueda = new URLSearchParams();
  for (const [clave, valor] of Object.entries(params ?? {}) as [string, unknown][]) {
    if (valor === undefined || valor === null || valor === '') continue;
    const valores: unknown[] = Array.isArray(valor) ? valor : [valor];
    for (const v of valores) busqueda.append(clave, v instanceof Date ? v.toISOString() : String(v));
  }
  const consulta = busqueda.toString();
  return `${baseUrl.replace(/\/$/, '')}${ruta}${consulta ? `?${consulta}` : ''}`;
}

function valorIfMatch(version: number | string): string {
  const limpio = String(version).replace(/^W\//, '').replace(/"/g, '');
  return `"${limpio}"`;
}

async function leerCuerpo(respuesta: Response): Promise<unknown> {
  if (respuesta.status === 204 || respuesta.status === 205) return undefined;
  const textoCrudo = await respuesta.text();
  if (!textoCrudo) return undefined;
  const tipo = respuesta.headers.get('content-type') ?? '';
  if (tipo.includes('json')) {
    try {
      return JSON.parse(textoCrudo) as unknown;
    } catch {
      return textoCrudo;
    }
  }
  return textoCrudo;
}

/**
 * Arma y envía la petición con `Authorization: Bearer`, `X-Empresa`, `Idempotency-Key`, `If-Match` y
 * `X-Correlation-Id`. Devuelve la respuesta exitosa sin leer su cuerpo; si no lo es, lanza `ErrorApi` (o `ErrorDeRed`
 * si no hubo respuesta).
 */
async function enviar(config: ConfigPeticion, opciones: OpcionesPeticion, aceptar: string): Promise<Response> {
  const cfg = configuracionCliente();
  const metodo = config.method.toUpperCase();
  const cabeceras = new Headers({ Accept: aceptar });

  const token = opciones.token === undefined ? cfg.obtenerToken() : opciones.token;
  if (token) cabeceras.set('Authorization', `Bearer ${token}`);

  const empresa = opciones.empresa === undefined ? cfg.obtenerEmpresa() : opciones.empresa;
  if (empresa) cabeceras.set('X-Empresa', empresa);

  const correlationId = cfg.generarId();
  cabeceras.set('X-Correlation-Id', correlationId);

  if (metodo === 'POST') {
    const clave = opciones.idempotencyKey ?? (RUTAS_QUE_CREAN.includes(config.url) ? cfg.generarId() : undefined);
    if (clave) cabeceras.set('Idempotency-Key', clave);
  }
  if (opciones.ifMatch !== undefined) cabeceras.set('If-Match', valorIfMatch(opciones.ifMatch));

  let cuerpo: string | undefined;
  if (config.data !== undefined) {
    cabeceras.set('Content-Type', 'application/json');
    cuerpo = JSON.stringify(config.data);
  }

  for (const [nombre, valor] of Object.entries({ ...config.headers, ...opciones.headers })) {
    cabeceras.set(nombre, valor);
  }

  const url = construirUrl(cfg.baseUrl, config.url, config.params);
  let respuesta: Response;
  try {
    respuesta = await cfg.fetch(url, {
      method: metodo,
      headers: cabeceras,
      body: cuerpo ?? null,
      signal: config.signal ?? null,
    });
  } catch (causa) {
    if (causa instanceof DOMException && causa.name === 'AbortError') throw causa;
    throw new ErrorDeRed('No pudimos comunicarnos con Validasoft. Revisa tu conexión e intenta de nuevo.', causa);
  }

  if (!respuesta.ok) {
    const datos = await leerCuerpo(respuesta);
    const error = errorDesdeProblema(respuesta.status, datos, respuesta.headers.get('X-Correlation-Id') ?? correlationId);
    if (error.esNoAutenticado) cfg.alNoAutenticado?.(error, url);
    throw error;
  }
  return respuesta;
}

/**
 * Mutator de orval: arma la petición con `Authorization: Bearer`, `X-Empresa`, `Idempotency-Key`, `If-Match` y
 * `X-Correlation-Id`, y convierte cualquier respuesta no exitosa en `ErrorApi` (o `ErrorDeRed` si no hubo respuesta).
 */
export async function clienteValidasoft<T>(config: ConfigPeticion, opciones: OpcionesPeticion = {}): Promise<T> {
  const respuesta = await enviar(config, opciones, 'application/json, application/problem+json');
  return (await leerCuerpo(respuesta)) as T;
}

/** Archivo que devolvió la API: el contenido y el nombre que propuso en `Content-Disposition` (`null` si no lo dijo). */
export interface ArchivoDescargado {
  blob: Blob;
  nombre: string | null;
}

/**
 * Nombre del archivo de `Content-Disposition`: prefiere `filename*` (RFC 5987, UTF-8) sobre `filename`. `null` si la
 * cabecera no trae nombre (o no se expuso por CORS).
 */
export function nombreDeArchivo(contentDisposition: string | null): string | null {
  if (!contentDisposition) return null;
  const extendido = /filename\*\s*=\s*([^']*)'[^']*'([^;]+)/i.exec(contentDisposition);
  if (extendido?.[2]) {
    try {
      return decodeURIComponent(extendido[2].trim().replace(/^"|"$/g, ''));
    } catch {
      // Codificación inválida: se intenta con `filename`.
    }
  }
  const simple = /filename\s*=\s*("([^"]*)"|[^;]+)/i.exec(contentDisposition);
  const nombre = (simple?.[2] ?? simple?.[1])?.trim();
  if (!nombre) return null;
  return nombre;
}

/**
 * Descarga binaria (informes en Excel, etc.) con la misma sesión, empresa y manejo de errores que el resto del cliente.
 * Los generados por orval tipan estas respuestas como JSON; para ellas se usa esta función.
 */
export async function descargarArchivo(
  config: Pick<ConfigPeticion, 'url' | 'params' | 'signal'>,
  opciones: OpcionesPeticion = {},
): Promise<ArchivoDescargado> {
  const respuesta = await enviar({ ...config, method: 'GET' }, opciones, '*/*');
  return { blob: await respuesta.blob(), nombre: nombreDeArchivo(respuesta.headers.get('Content-Disposition')) };
}

/** Tipos que orval toma del mutator para los errores y cuerpos de los hooks generados. */
export type ErrorType<_Error> = ErrorApi;
export type BodyType<Cuerpo> = Cuerpo;
