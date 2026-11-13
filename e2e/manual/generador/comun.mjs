// Utilidades compartidas del generador del manual: texto, rutas, PNG y lectura de los manifiestos de los recorridos.
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join } from 'node:path';

export const escapar = (texto) =>
  String(texto).replaceAll('&', '&amp;').replaceAll('<', '&lt;').replaceAll('>', '&gt;').replaceAll('"', '&quot;');

/** Sin tildes y en minúsculas: para comparar y buscar. */
export const sinTildes = (texto) =>
  String(texto)
    .normalize('NFD')
    .replace(/[̀-ͯ]/g, '')
    .toLowerCase();

/** `Hola, Mundo` → `hola-mundo` (anclas y nombres de archivo). */
export const aSlug = (texto) =>
  sinTildes(texto)
    .replace(/[^a-z0-9]+/g, '-')
    .replace(/^-+|-+$/g, '') || 'x';

/** Separa un texto en párrafos (línea en blanco). */
export const parrafos = (texto) =>
  String(texto)
    .split(/\n\s*\n/)
    .map((p) => p.replace(/\s*\n\s*/g, ' ').trim())
    .filter(Boolean);

/**
 * Texto de las explicaciones a HTML: `**negrita**`, `` `código` `` y `[[dominio/archivo]]` (enlace a otro recorrido). `enlace(id)` devuelve
 * `{ href, titulo }` o `null` si el recorrido no existe (entonces se muestra como texto).
 */
export function textoEnHtml(texto, enlace = () => null) {
  return escapar(texto)
    .replace(/`([^`]+)`/g, '<code>$1</code>')
    .replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>')
    .replace(/\[\[([a-z0-9-]+\/[a-z0-9-]+)\]\]/g, (_, id) => {
      const destino = enlace(id);
      return destino ? `<a href="${escapar(destino.href)}">${escapar(destino.titulo)}</a>` : `<em>${escapar(id)}</em>`;
    });
}

/** Lo mismo para Markdown: se conserva el formato y los enlaces a otros recorridos pasan a enlaces relativos. */
export function textoEnMarkdown(texto, enlace = () => null) {
  return String(texto).replace(/\[\[([a-z0-9-]+\/[a-z0-9-]+)\]\]/g, (_, id) => {
    const destino = enlace(id);
    return destino ? `[${destino.titulo}](${destino.href})` : `*${id}*`;
  });
}

/** Nombre de la imagen en la salida: con `extension` (`.webp`) cambia la del pantallazo; sin ella, se deja como está. */
export const nombreDeImagen = (nombre, extension) => (extension ? String(nombre).replace(/\.png$/i, extension) : nombre);

/** Ancho y alto de un PNG (cabecera IHDR), o `null` si no lo es. */
export function medidasPng(buffer) {
  if (buffer.length < 24 || buffer.readUInt32BE(0) !== 0x89504e47) return null;
  return { ancho: buffer.readUInt32BE(16), alto: buffer.readUInt32BE(20) };
}

/** Fecha legible en español (zona de Colombia). */
export const fechaLarga = (fecha) =>
  new Intl.DateTimeFormat('es-CO', { dateStyle: 'long', timeStyle: 'short', timeZone: 'America/Bogota' }).format(fecha);

export function plural(n, uno, varios) {
  return `${String(n)} ${n === 1 ? uno : varios}`;
}

/**
 * Lee los manifiestos `<carpeta>/<dominio>/<archivo>/manifiesto.json`. Los recorridos se ordenan por el orden del dominio en el catálogo y
 * luego por nombre de archivo. Devuelve también los avisos de lo que se descartó.
 */
export function leerRecorridos(carpeta, catalogo) {
  const recorridos = [];
  const avisos = [];
  if (!existsSync(carpeta)) return { recorridos, avisos };
  for (const dominio of readdirSync(carpeta, { withFileTypes: true })) {
    if (!dominio.isDirectory()) continue;
    for (const archivo of readdirSync(join(carpeta, dominio.name), { withFileTypes: true })) {
      if (!archivo.isDirectory()) continue;
      const base = join(carpeta, dominio.name, archivo.name);
      const ruta = join(base, 'manifiesto.json');
      if (!existsSync(ruta)) {
        avisos.push(`${dominio.name}/${archivo.name}: sin manifiesto (el recorrido falló o no terminó); se omite.`);
        continue;
      }
      let manifiesto;
      try {
        manifiesto = JSON.parse(readFileSync(ruta, 'utf8'));
      } catch (error) {
        avisos.push(`${dominio.name}/${archivo.name}: manifiesto ilegible (${error.message}); se omite.`);
        continue;
      }
      const problema = validarManifiesto(manifiesto, catalogo);
      if (problema) {
        avisos.push(`${dominio.name}/${archivo.name}: ${problema}; se omite.`);
        continue;
      }
      recorridos.push({ ...manifiesto, carpeta: base });
    }
  }
  const ordenDominio = (slug) => catalogo.dominios.findIndex((d) => d.slug === slug);
  recorridos.sort((a, b) => ordenDominio(a.dominio) - ordenDominio(b.dominio) || a.archivo.localeCompare(b.archivo));
  return { recorridos, avisos };
}

/** Texto del problema del manifiesto, o `null` si está bien. */
export function validarManifiesto(m, catalogo) {
  if (!m || typeof m !== 'object') return 'no es un objeto';
  if (m.version !== 1) return `versión de manifiesto desconocida (${String(m.version)})`;
  for (const campo of ['id', 'dominio', 'archivo', 'titulo', 'actor', 'resumen']) {
    if (typeof m[campo] !== 'string' || m[campo] === '') return `falta «${campo}»`;
  }
  if (!catalogo.dominios.some((d) => d.slug === m.dominio)) return `dominio desconocido «${m.dominio}»`;
  if (!catalogo.actores.some((a) => a.nombre === m.actor)) return `actor desconocido «${m.actor}»`;
  if (!Array.isArray(m.pasos) || m.pasos.length === 0) return 'sin pasos';
  for (const p of m.pasos) {
    if (typeof p.titulo !== 'string' || typeof p.explicacion !== 'string' || typeof p.imagenes?.claro !== 'string') return `paso ${String(p.orden)} incompleto`;
  }
  return null;
}
