// Markdown de los recorridos (docs/manual/<dominio>/<archivo>.md): texto con referencias a los pantallazos, sin binarios en git.
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, posix, relative, sep } from 'node:path';
import { textoEnMarkdown } from './comun.mjs';

/** Ruta relativa con barras normales (las imágenes viven en la salida gitignorada). */
const relativa = (desde, hacia) => relative(desde, hacia).split(sep).join(posix.sep);

/** Valor entre comillas de una línea del encabezado (JSON es YAML válido). */
const valor = (texto) => JSON.stringify(texto);

/**
 * Markdown de un recorrido. `carpetaDocs` es `docs/manual`, `carpetaImagenes` es `salida/manual/img` y `porId` resuelve los recorridos
 * relacionados. El texto no lleva fechas ni identificadores de la corrida: dos corridas iguales dan el mismo archivo.
 */
export function markdownDeRecorrido(r, { dominio, carpetaDocs, carpetaImagenes, porId }) {
  const carpetaDelArchivo = join(carpetaDocs, r.dominio);
  const enlace = (id) => {
    const destino = porId.get(id);
    return destino ? { href: relativa(carpetaDelArchivo, join(carpetaDocs, destino.dominio, `${destino.archivo}.md`)), titulo: destino.titulo } : null;
  };
  const lineas = [
    '---',
    `id: ${valor(r.id)}`,
    `titulo: ${valor(r.titulo)}`,
    `actor: ${valor(r.actor)}`,
    `dominio: ${valor(r.dominio)}`,
    `resumen: ${valor(r.resumen)}`,
    `pasos: ${String(r.pasos.length)}`,
    '---',
    '',
    `# ${r.titulo}`,
    '',
    `**Actor:** ${r.actor} · **Dominio:** ${dominio.nombre}`,
    '',
    r.resumen,
    '',
  ];
  if (r.precondiciones.length > 0) {
    lineas.push('**Antes de empezar**', '', ...r.precondiciones.map((p) => `- ${textoEnMarkdown(p, enlace)}`), '');
  }
  lineas.push('**Pasos**', '', ...r.pasos.map((p) => `${String(p.orden)}. ${p.titulo}`), '');
  for (const p of r.pasos) {
    lineas.push(`## Paso ${String(p.orden)}. ${p.titulo}`, '');
    lineas.push(...p.explicacion.split(/\n\s*\n/).flatMap((t) => [textoEnMarkdown(t.replace(/\s*\n\s*/g, ' ').trim(), enlace), '']));
    if (p.ruta) lineas.push(`> **Pantalla:** \`${p.ruta}\``, '');
    const imagen = (nombre) => relativa(carpetaDelArchivo, join(carpetaImagenes, r.dominio, r.archivo, nombre));
    lineas.push(`![Paso ${String(p.orden)}: ${p.titulo} (escritorio)](${imagen(p.imagenes.claro)})`, '');
    if (p.imagenes.celular) lineas.push(`![Paso ${String(p.orden)}: ${p.titulo} (celular)](${imagen(p.imagenes.celular)})`, '');
    if (p.variantes.length > 0) {
      lineas.push('**Variantes**', '', ...p.variantes.map((v) => `- **${textoEnMarkdown(v.cuando, enlace)}:** ${textoEnMarkdown(v.entonces, enlace)}`), '');
    }
    if (p.configuracion.length > 0) {
      lineas.push(
        '**Configuración que lo afecta**',
        '',
        ...p.configuracion.map((c) => `- **${textoEnMarkdown(c.ajuste, enlace)}**${c.ruta ? ` (\`${c.ruta}\`)` : ''}${c.efecto ? `: ${textoEnMarkdown(c.efecto, enlace)}` : ''}`),
        '',
      );
    }
  }
  const relacionados = r.relacionados.map((id) => ({ id, destino: enlace(id) })).filter((x) => x.destino);
  if (relacionados.length > 0) {
    lineas.push('## Recorridos relacionados', '', ...relacionados.map((x) => `- [${x.destino.titulo}](${x.destino.href})`), '');
  }
  return `${lineas.join('\n').replace(/\n{3,}/g, '\n\n').trimEnd()}\n`;
}

/** Datos del encabezado de un Markdown de recorrido (los que escribe {@link markdownDeRecorrido}), o `null` si no lo tiene. */
export function leerEncabezado(texto) {
  const coincidencia = /^---\n([\s\S]*?)\n---\n/.exec(texto);
  if (!coincidencia) return null;
  const datos = {};
  for (const linea of coincidencia[1].split('\n')) {
    const corte = linea.indexOf(': ');
    if (corte < 0) continue;
    const crudo = linea.slice(corte + 2);
    try {
      datos[linea.slice(0, corte)] = JSON.parse(crudo);
    } catch {
      datos[linea.slice(0, corte)] = crudo;
    }
  }
  return datos;
}

/** Lista los recorridos que ya están escritos en `docs/manual/<dominio>/` (con su encabezado), para armar los índices sin depender de la corrida. */
export function recorridosEscritos(carpetaDocs, dominio) {
  const carpeta = join(carpetaDocs, dominio);
  if (!existsSync(carpeta)) return [];
  return readdirSync(carpeta)
    .filter((n) => n.endsWith('.md') && n !== 'README.md')
    .sort()
    .map((nombre) => ({ nombre, datos: leerEncabezado(readFileSync(join(carpeta, nombre), 'utf8')) }))
    .filter((x) => x.datos);
}

/** README de un dominio: su descripción y los recorridos escritos. */
export function indiceDeDominio(dominio, escritos) {
  const lineas = [`# ${dominio.nombre}`, '', dominio.descripcion, ''];
  if (escritos.length === 0) lineas.push('_Todavía no hay recorridos en este dominio._', '');
  else lineas.push(...escritos.map((e) => `- [${e.datos.titulo}](${e.nombre}) — ${e.datos.actor}. ${e.datos.resumen}`), '');
  return `${lineas.join('\n').trimEnd()}\n`;
}

/** README general del manual: sin cifras ni fechas, para que varias personas puedan regenerarlo a la vez sin chocar. */
export function indiceGeneral(catalogo) {
  const lineas = [
    '# Manual de Validasoft',
    '',
    'Documentación de cada flujo del producto, **generada a partir de recorridos reales** de la aplicación (pruebas Playwright en `e2e/manual/`) y del contrato OpenAPI. No se edita a mano: si el producto cambia, se vuelve a recorrer y a generar (`pnpm manual:todo`).',
    '',
    'Los pantallazos no se guardan en git: se generan en `salida/manual/` junto con el sitio navegable (`salida/manual/index.html`, con buscador, modo claro y oscuro y vista para celular). Este directorio guarda el texto en Markdown.',
    '',
    '## Recorridos por dominio',
    '',
    ...catalogo.dominios.map((d) => `- [${d.nombre}](${d.slug}/README.md) — ${d.descripcion}`),
    '',
    '## Actores',
    '',
    ...catalogo.actores.map((a) => `- **${a.nombre}**: ${a.descripcion}`),
    '',
    '## Referencia de la API',
    '',
    '- [API de Validasoft](api/README.md): cada recurso con sus operaciones, permisos, parámetros, cuerpos, respuestas y ejemplos, generada de `packages/api-client/openapi/v1.json`.',
    '',
    '## Cómo se genera',
    '',
    '```bash',
    'pnpm manual:recorrer [--grep <texto>]   # corre los recorridos y deja manifiestos e imágenes en salida/manual-recorridos/',
    'pnpm manual                             # genera el sitio HTML (salida/manual/) y este Markdown',
    'pnpm manual:todo                        # las dos cosas',
    '```',
    '',
    'La plantilla para escribir un recorrido nuevo está en [`e2e/manual/README.md`](../../e2e/manual/README.md); la lista de lo que falta documentar, en [`e2e/manual/INVENTARIO.md`](../../e2e/manual/INVENTARIO.md).',
  ];
  return `${lineas.join('\n')}\n`;
}
