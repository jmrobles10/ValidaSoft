import type { AjusteDeConfiguracion, MetaDeRecorrido, OpcionesDePaso, Variante } from './tipos';

/**
 * Validaciones puras (sin Playwright) de lo que declara un recorrido: los mensajes dicen en español qué corregir, porque quien
 * escribe recorridos las lee directo en la terminal.
 */

export interface Catalogo {
  actores: { nombre: string; slug: string; descripcion: string }[];
  dominios: { slug: string; nombre: string; descripcion: string }[];
}

const MIN_EXPLICACION = 60;
const MAX_EXPLICACION = 1400;
const MAX_TITULO = 90;

/** Errores de la cabecera de un recorrido. `carpeta` es el nombre real de la carpeta del archivo y `archivo` su nombre sin `.manual.ts`. */
export function validarMeta(meta: MetaDeRecorrido, catalogo: Catalogo, carpeta: string, archivo: string): string[] {
  const errores: string[] = [];
  if (!catalogo.dominios.some((d) => d.slug === meta.dominio)) {
    errores.push(`El dominio «${meta.dominio}» no existe en e2e/manual/catalogo.json. Usa uno de: ${catalogo.dominios.map((d) => d.slug).join(', ')}.`);
  }
  if (meta.dominio !== carpeta) errores.push(`El archivo está en la carpeta «${carpeta}» pero declara dominio «${meta.dominio}»: deben coincidir.`);
  if (!catalogo.actores.some((a) => a.nombre === meta.actor)) {
    errores.push(`El actor «${meta.actor}» no existe. Usa uno de: ${catalogo.actores.map((a) => a.nombre).join(', ')}.`);
  }
  if (meta.titulo.trim().length < 8 || meta.titulo.length > MAX_TITULO) errores.push(`El título del recorrido debe tener entre 8 y ${String(MAX_TITULO)} caracteres.`);
  if (meta.resumen.trim().length < 30) errores.push('El resumen del recorrido es muy corto: di en una a tres frases qué logra la persona y cuándo lo hace.');
  if (!/^[a-z0-9]+(-[a-z0-9]+)*$/.test(archivo)) errores.push(`El nombre del archivo «${archivo}» debe ir en minúsculas con guiones (sin tildes ni espacios).`);
  for (const id of meta.relacionados ?? []) {
    if (!/^[a-z0-9-]+\/[a-z0-9-]+$/.test(id)) errores.push(`«${id}» en relacionados debe tener la forma dominio/archivo.`);
  }
  return errores;
}

/** Errores de un paso (`orden` empieza en 1). */
export function validarPaso(opciones: OpcionesDePaso, orden: number): string[] {
  const errores: string[] = [];
  const donde = `Paso ${String(orden)} «${opciones.titulo}»`;
  if (opciones.titulo.trim().length < 3 || opciones.titulo.length > MAX_TITULO) errores.push(`${donde}: el título debe tener entre 3 y ${String(MAX_TITULO)} caracteres.`);
  const largo = opciones.explicacion.trim().length;
  if (largo < MIN_EXPLICACION) errores.push(`${donde}: la explicación es muy corta (${String(largo)} caracteres; mínimo ${String(MIN_EXPLICACION)}). Cuenta qué ve la persona, qué hace y qué pasa después.`);
  if (largo > MAX_EXPLICACION) errores.push(`${donde}: la explicación es muy larga (máximo ${String(MAX_EXPLICACION)} caracteres): pártela en dos pasos.`);
  if (/\b(?:TODO|FIXME)\b/.test(opciones.explicacion) || /lorem ipsum/i.test(opciones.explicacion)) errores.push(`${donde}: la explicación tiene texto pendiente.`);
  for (const v of normalizarVariantes(opciones.variantes)) {
    if (!v.cuando.trim() || !v.entonces.trim()) errores.push(`${donde}: cada variante necesita «cuando» y «entonces» (como objeto, o como texto «Cuando X: Y»).`);
  }
  for (const c of normalizarConfiguracion(opciones.configuracion)) {
    if (!c.ajuste.trim()) errores.push(`${donde}: cada ajuste de configuración necesita un nombre.`);
    if (c.ruta && !/[/›]/.test(c.ruta)) errores.push(`${donde}: la ruta de configuración «${c.ruta}» debe ser una ruta de la app (/configuracion/…) o un camino de menú (Configuración › …).`);
  }
  return errores;
}

/** Una variante escrita como texto («Cuando X: Y» o «X: Y») pasa a su forma estructurada. */
export function normalizarVariantes(variantes: (Variante | string)[] | undefined): Variante[] {
  return (variantes ?? []).map((v) => {
    if (typeof v !== 'string') return { cuando: v.cuando.trim(), entonces: v.entonces.trim() };
    const corte = v.indexOf(':');
    if (corte < 0) return { cuando: v.trim(), entonces: '' };
    return { cuando: v.slice(0, corte).replace(/^cuando\s+/i, '').trim(), entonces: v.slice(corte + 1).trim() };
  });
}

export function normalizarConfiguracion(configuracion: OpcionesDePaso['configuracion']): AjusteDeConfiguracion[] {
  if (configuracion === undefined) return [];
  const lista = Array.isArray(configuracion) ? configuracion : [configuracion];
  return lista.map((c) => (typeof c === 'string' ? { ajuste: c.trim() } : { ajuste: c.ajuste.trim(), ruta: c.ruta?.trim(), efecto: c.efecto?.trim() }));
}

/** `Acepta la invitación…` → `acepta-la-invitacion` (para nombres de archivo). */
export function aSlug(texto: string, maximo = 48): string {
  const limpio = texto
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase()
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '');
  return limpio.slice(0, maximo).replace(/-+$/, '') || 'paso';
}

const UUID = /[0-9a-f]{8}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{4}-[0-9a-f]{12}/gi;

/**
 * Ruta de la app sin datos de la sesión: la ruta de hash de la app de la empresa (`/app/#/e/…/bandeja`) pasa a `/e/{empresa}/bandeja`, los
 * identificadores a `{id}`, el enlace de invitación a `{enlace}` y se descarta lo que viene después de «?».
 */
export function normalizarRuta(url: string): string | null {
  let ruta: string;
  try {
    const u = new URL(url);
    if (!/^https?:$/.test(u.protocol)) return null;
    ruta = u.hash.startsWith('#/') ? u.hash.slice(1) : u.pathname.replace(/^\/app(?=\/|$)/, '') || '/';
  } catch {
    return null;
  }
  ruta = ruta.split('?')[0] ?? ruta;
  ruta = ruta.replace(/^\/e\/[0-9a-f-]{36}(?=\/|$)/i, '/e/{empresa}').replace(UUID, '{id}');
  ruta = ruta.replace(/^\/e\/(?!\{)[^/]+/, '/e/{enlace}');
  return ruta || '/';
}
