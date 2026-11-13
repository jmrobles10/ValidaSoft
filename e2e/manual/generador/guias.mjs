// Páginas de guía del manual: las que NO salen de un recorrido (arquitectura, flujo de punta a punta, configuración, integración).
//
// Fuente:  e2e/manual/guias/<slug>.md           Markdown con encabezado (titulo, seccion, orden, resumen)
//          e2e/manual/guias/diagramas/*.svg     diagramas hechos a mano (SVG con las clases d-*), que el sitio incrusta en la página
// Salida:  salida/manual/g/<slug>.html          página del sitio (con índice, buscador y diagramas en línea, claro y oscuro)
//          docs/manual/guias/<slug>.md          el mismo texto con los enlaces resueltos; los diagramas van a docs/manual/guias/diagramas/
//
// Sintaxis admitida (un subconjunto de Markdown): títulos `##`/`###`/`####`, párrafos, listas `-` y `1.`, tablas con `|`, bloques de código con
// ```lenguaje, citas `>` (una nota: `> **Nota:** …`), `**negrita**`, `*cursiva*`, `código` y enlaces. Enlaces especiales:
//   [[dominio/archivo]] o [[dominio/archivo|Texto]]   recorrido del manual (el nombre de archivo debe estar en INVENTARIO.md; si aún no existe
//                          queda como «pendiente» con el Texto, y cuando exista se enlaza con su título real)
//   [texto](guia:slug#ancla)   otra guía · [texto](api:modulo)   módulo de la referencia de la API · [texto](repo:ruta)   archivo del repositorio
//   ![descripción](diagramas/x.svg)   diagrama
import { existsSync, readdirSync, readFileSync } from 'node:fs';
import { join, posix, relative, sep } from 'node:path';
import { aSlug, escapar, sinTildes } from './comun.mjs';
import { leerEncabezado } from './markdown.mjs';
import { pagina } from './plantilla.mjs';

export const rutaDeGuia = (g) => `g/${g.slug}.html`;
const SLUG = /^[a-z0-9]+(-[a-z0-9]+)*$/;
const ID_RECORRIDO = /\[\[([a-z0-9-]+\/[a-z0-9-]+)(?:\|([^\]]+))?\]\]/g;

// ---------------------------------------------------------------------------------------------------------------------
// Lectura
// ---------------------------------------------------------------------------------------------------------------------

/** Separa el encabezado `---` (claves `clave: valor`; el valor puede ir entre comillas JSON) del cuerpo. */
export function separarEncabezado(texto) {
  const coincidencia = /^---\r?\n([\s\S]*?)\r?\n---\r?\n?/.exec(texto);
  if (!coincidencia) return { datos: {}, cuerpo: texto.replace(/\r\n/g, '\n') };
  const datos = {};
  for (const linea of coincidencia[1].split(/\r?\n/)) {
    const corte = linea.indexOf(':');
    if (corte < 0) continue;
    const clave = linea.slice(0, corte).trim();
    let valor = linea.slice(corte + 1).trim();
    if (/^".*"$/.test(valor)) {
      try {
        valor = JSON.parse(valor);
      } catch {
        valor = valor.slice(1, -1);
      }
    } else if (/^-?\d+$/.test(valor)) valor = Number(valor);
    datos[clave] = valor;
  }
  return { datos, cuerpo: texto.slice(coincidencia[0].length).replace(/\r\n/g, '\n') };
}

/** Texto del problema del encabezado, o `null` si está bien. */
export function validarEncabezado(datos) {
  for (const campo of ['titulo', 'seccion', 'resumen']) {
    if (typeof datos[campo] !== 'string' || datos[campo].trim() === '') return `falta «${campo}»`;
  }
  if (typeof datos.orden !== 'number') return 'falta «orden» (un número)';
  if (datos.resumen.length < 40 || datos.resumen.length > 400) return 'el resumen debe tener de 40 a 400 caracteres';
  return null;
}

/**
 * Lee las guías de una carpeta. Devuelve las guías ordenadas (por sección y luego por `orden`), las secciones en ese mismo orden, los diagramas
 * (nombre de archivo → SVG) y los avisos de lo que se descartó.
 */
export function leerGuias(carpeta) {
  const guias = [];
  const avisos = [];
  const diagramas = new Map();
  if (!existsSync(carpeta)) return { guias, secciones: [], diagramas, avisos };
  for (const nombre of readdirSync(carpeta).filter((n) => n.endsWith('.md') && n !== 'README.md').sort()) {
    const slug = nombre.slice(0, -3);
    if (!SLUG.test(slug)) {
      avisos.push(`guías/${nombre}: el nombre debe ir en minúsculas y con guiones; se omite.`);
      continue;
    }
    const { datos, cuerpo } = separarEncabezado(readFileSync(join(carpeta, nombre), 'utf8'));
    const problema = validarEncabezado(datos);
    if (problema) {
      avisos.push(`guías/${nombre}: ${problema}; se omite.`);
      continue;
    }
    guias.push({ slug, titulo: datos.titulo, seccion: datos.seccion, orden: datos.orden, resumen: datos.resumen, cuerpo });
  }
  const carpetaDiagramas = join(carpeta, 'diagramas');
  if (existsSync(carpetaDiagramas)) {
    for (const nombre of readdirSync(carpetaDiagramas).filter((n) => n.endsWith('.svg')).sort())
      diagramas.set(nombre, readFileSync(join(carpetaDiagramas, nombre), 'utf8').replace(/\r\n/g, '\n'));
  }
  const primero = new Map();
  for (const g of guias) primero.set(g.seccion, Math.min(primero.get(g.seccion) ?? Infinity, g.orden));
  guias.sort((a, b) => primero.get(a.seccion) - primero.get(b.seccion) || a.seccion.localeCompare(b.seccion) || a.orden - b.orden || a.slug.localeCompare(b.slug));
  const secciones = [...primero.keys()]
    .sort((a, b) => primero.get(a) - primero.get(b) || a.localeCompare(b))
    .map((nombre) => ({ nombre, slug: aSlug(nombre), guias: guias.filter((g) => g.seccion === nombre) }));
  return { guias, secciones, diagramas, avisos };
}

/** Los recorridos que nombra el inventario (`e2e/manual/INVENTARIO.md`): la primera columna de sus tablas. */
export function leerInventario(ruta) {
  if (!existsSync(ruta)) return new Set();
  return new Set([...readFileSync(ruta, 'utf8').matchAll(/^\| `([a-z0-9-]+\/[a-z0-9-]+)`/gm)].map((m) => m[1]));
}

/** `tercero/aceptar-invitacion` → «Aceptar invitación» sin tildes perdidas: solo separa y capitaliza (nombre provisional de un recorrido pendiente). */
export function humanizar(id) {
  const texto = id.split('/')[1].replaceAll('-', ' ');
  return texto.charAt(0).toUpperCase() + texto.slice(1);
}

// ---------------------------------------------------------------------------------------------------------------------
// Bloques
// ---------------------------------------------------------------------------------------------------------------------

const esTabla = (linea, siguiente) => linea.trimStart().startsWith('|') && siguiente !== undefined && /^\s*\|?\s*:?-{2,}:?\s*(\|\s*:?-{2,}:?\s*)*\|?\s*$/.test(siguiente);

/** Celdas de una fila `| a | b |` (admite `\|` dentro de una celda). */
export function celdasDeFila(linea) {
  const texto = linea.trim().replace(/^\|/, '').replace(/\|$/, '').replaceAll('\\|', '\u0001');
  return texto.split('|').map((c) => c.replaceAll('\u0001', '|').trim());
}

/** Divide el Markdown en bloques: títulos, párrafos, listas, tablas, código, citas e imágenes. */
export function bloquesDeMarkdown(texto) {
  const lineas = texto.split('\n');
  const bloques = [];
  const usados = new Map();
  let i = 0;
  while (i < lineas.length) {
    const linea = lineas[i];
    if (linea.trim() === '') {
      i++;
      continue;
    }
    const valla = /^```\s*([\w-]*)\s*$/.exec(linea);
    if (valla) {
      const cuerpo = [];
      i++;
      while (i < lineas.length && !/^```\s*$/.test(lineas[i])) cuerpo.push(lineas[i++]);
      i++;
      bloques.push({ tipo: 'codigo', lenguaje: valla[1], texto: cuerpo.join('\n') });
      continue;
    }
    const titulo = /^(#{1,4})\s+(.+?)\s*#*\s*$/.exec(linea);
    if (titulo) {
      const base = aSlug(titulo[2].replace(/[`*]/g, ''));
      const n = (usados.get(base) ?? 0) + 1;
      usados.set(base, n);
      bloques.push({ tipo: 'titulo', nivel: titulo[1].length, texto: titulo[2], id: n === 1 ? base : `${base}-${String(n)}` });
      i++;
      continue;
    }
    if (esTabla(linea, lineas[i + 1])) {
      const cabecera = celdasDeFila(linea);
      const filas = [];
      i += 2;
      while (i < lineas.length && lineas[i].trimStart().startsWith('|')) filas.push(celdasDeFila(lineas[i++]));
      bloques.push({ tipo: 'tabla', cabecera, filas });
      continue;
    }
    if (/^>/.test(linea)) {
      const cuerpo = [];
      while (i < lineas.length && /^>/.test(lineas[i])) cuerpo.push(lineas[i++].replace(/^>\s?/, ''));
      bloques.push({ tipo: 'cita', hijos: bloquesDeMarkdown(cuerpo.join('\n')) });
      continue;
    }
    const item = /^(\s*)([-*]|\d+\.)\s+(.*)$/.exec(linea);
    if (item) {
      const ordenada = /\d/.test(item[2]);
      const items = [];
      while (i < lineas.length) {
        const actual = /^(\s*)([-*]|\d+\.)\s+(.*)$/.exec(lineas[i]);
        if (actual && actual[1].length === item[1].length) {
          items.push(actual[3]);
          i++;
        } else if (lineas[i].trim() !== '' && /^\s{2,}\S/.test(lineas[i]) && items.length > 0) {
          items[items.length - 1] += ` ${lineas[i].trim()}`;
          i++;
        } else break;
      }
      bloques.push({ tipo: 'lista', ordenada, items });
      continue;
    }
    const imagen = /^!\[(.*)\]\((\S+)\)\s*$/.exec(linea);
    if (imagen) {
      bloques.push({ tipo: 'imagen', alt: imagen[1], ruta: imagen[2] });
      i++;
      continue;
    }
    const parrafo = [];
    while (i < lineas.length && lineas[i].trim() !== '' && !/^(```|#{1,4}\s|>|!\[)/.test(lineas[i]) && !esTabla(lineas[i], lineas[i + 1]) && !(parrafo.length > 0 && /^(\s*)([-*]|\d+\.)\s+/.test(lineas[i])))
      parrafo.push(lineas[i++].trim());
    bloques.push({ tipo: 'parrafo', texto: parrafo.join(' ') });
  }
  return bloques;
}

// ---------------------------------------------------------------------------------------------------------------------
// Texto en línea
// ---------------------------------------------------------------------------------------------------------------------

/** Destino de un enlace especial según el formato (`html` o `md`). Devuelve `{ href }`, `{ ruta }` (archivo del repositorio) o `null` si es normal. */
function destinoEspecial(href, formato) {
  const guia = /^guia:([a-z0-9-]+)(#[\w-]+)?$/.exec(href);
  if (guia) return { href: `${guia[1]}.${formato === 'html' ? 'html' : 'md'}${guia[2] ?? ''}` };
  const api = /^api:([a-z0-9-]*)$/.exec(href);
  if (api) return { href: formato === 'html' ? `../api/${api[1] || 'index'}.html` : `../api/${api[1] ? `${api[1]}.md` : 'README.md'}` };
  const repo = /^repo:(.+)$/.exec(href);
  if (repo) return { ruta: repo[1] };
  return null;
}

/**
 * Texto en línea a HTML. `ctx.recorrido(id)` devuelve `{ href, titulo }` o `null` (recorrido pendiente); `ctx.alertas` recoge los enlaces rotos.
 */
export function enLineaHtml(texto, ctx) {
  const codigos = [];
  let t = texto.replace(/`([^`]+)`/g, (_, c) => {
    codigos.push(c);
    return `\uE000${String(codigos.length - 1)}\uE000`;
  });
  t = escapar(t);
  t = t.replace(ID_RECORRIDO, (_, id, etiqueta) => {
    const destino = ctx.recorrido(id);
    return destino
      ? `<a href="${escapar(destino.href)}">${escapar(destino.titulo)}</a>`
      : `<span class="pendiente" title="Recorrido pendiente de documentar: ${escapar(id)}">${escapar(etiqueta ?? humanizar(id))}</span>`;
  });
  t = t.replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (_, texto2, href) => {
    const real = href.replaceAll('&amp;', '&');
    const especial = destinoEspecial(real, 'html');
    if (especial?.ruta !== undefined)
      return texto2 === especial.ruta || texto2 === `\uE000${String(codigos.indexOf(especial.ruta))}\uE000`
        ? `<span class="ruta-repo" title="Archivo del repositorio"><code>${escapar(especial.ruta)}</code></span>`
        : `<span class="ruta-repo" title="Archivo del repositorio">${texto2} (<code>${escapar(especial.ruta)}</code>)</span>`;
    if (especial) return `<a href="${escapar(especial.href)}">${texto2}</a>`;
    if (/^https?:\/\//.test(real)) return `<a href="${escapar(real)}" rel="noopener noreferrer">${texto2}</a>`;
    if (real.startsWith('#')) return `<a href="${escapar(real)}">${texto2}</a>`;
    ctx.alertas.push(`enlace no válido: ${href}`);
    return texto2;
  });
  t = t.replace(/\*\*([^*]+)\*\*/g, '<strong>$1</strong>').replace(/(^|[\s(¿¡«])\*([^*\s][^*]*?)\*(?=[\s).,;:!?»]|$)/g, '$1<em>$2</em>');
  return t.replace(/\uE000(\d+)\uE000/g, (_, n) => `<code>${escapar(codigos[Number(n)])}</code>`);
}

/** Texto sin formato (para el buscador y las descripciones). */
export function textoPlano(texto) {
  return texto
    .replace(/!\[[^\]]*\]\([^)]*\)/g, '')
    .replace(ID_RECORRIDO, (_, id, etiqueta) => etiqueta ?? humanizar(id))
    .replace(/\[([^\]]+)\]\([^)]*\)/g, '$1')
    .replace(/[`*]/g, '');
}

// ---------------------------------------------------------------------------------------------------------------------
// Diagramas
// ---------------------------------------------------------------------------------------------------------------------

const CLARO = {
  fondo: '#ffffff',
  superficie: '#ffffff',
  superficie2: '#eef1f5',
  borde: '#9aa6b5',
  texto: '#1b2129',
  suave: '#596473',
  linea: '#5b6778',
  acento: '#1c5bd0',
  acentoFondo: '#e6eeff',
  ok: '#0b7a4b',
  okFondo: '#e3f4ea',
  aviso: '#a85a00',
  avisoFondo: '#fff3dc',
  peligro: '#bf2a2a',
  peligroFondo: '#fde8e8',
};
const OSCURO = {
  fondo: '#0d1117',
  superficie: '#161b22',
  superficie2: '#1e252e',
  borde: '#566274',
  texto: '#e5e9ef',
  suave: '#99a4b1',
  linea: '#8794a5',
  acento: '#78a6ff',
  acentoFondo: '#1b2a47',
  ok: '#4cc790',
  okFondo: '#12281c',
  aviso: '#f0a14a',
  avisoFondo: '#2a2312',
  peligro: '#f27a7a',
  peligroFondo: '#3a1a1a',
};

const tokens = (p) =>
  Object.entries(p)
    .map(([k, v]) => `--d-${k.replace(/[A-Z]/g, (m) => `-${m.toLowerCase()}`)}:${v}`)
    .join(';');

/**
 * Estilos de los diagramas (clases `d-*`). Con `sitio: true` siguen el tema del sitio (`data-tema`); sin él (el SVG suelto de docs/manual) siguen
 * `prefers-color-scheme`. Los diagramas se dibujan con estas clases y no con colores fijos, así se leen en claro y en oscuro.
 */
export function cssDeDiagramas({ sitio }) {
  const oscuro = tokens(OSCURO);
  return [
    `svg.diagrama{${tokens(CLARO)};font-family:system-ui,-apple-system,'Segoe UI',Roboto,Arial,sans-serif}`,
    sitio
      ? `@media (prefers-color-scheme:dark){:root:not([data-tema='claro']) svg.diagrama{${oscuro}}}\n:root[data-tema='oscuro'] svg.diagrama{${oscuro}}`
      : `@media (prefers-color-scheme:dark){svg.diagrama{${oscuro}}}`,
    '.diagrama .d-fondo{fill:var(--d-fondo)}',
    '.diagrama .d-caja{fill:var(--d-superficie);stroke:var(--d-borde);stroke-width:1.4}',
    '.diagrama .d-caja-gris{fill:var(--d-superficie2);stroke:var(--d-borde);stroke-width:1.4}',
    '.diagrama .d-caja-a{fill:var(--d-acento-fondo);stroke:var(--d-acento);stroke-width:1.5}',
    '.diagrama .d-caja-ok{fill:var(--d-ok-fondo);stroke:var(--d-ok);stroke-width:1.5}',
    '.diagrama .d-caja-aviso{fill:var(--d-aviso-fondo);stroke:var(--d-aviso);stroke-width:1.5}',
    '.diagrama .d-caja-peligro{fill:var(--d-peligro-fondo);stroke:var(--d-peligro);stroke-width:1.5}',
    '.diagrama .d-grupo{fill:none;stroke:var(--d-borde);stroke-width:1.3;stroke-dasharray:6 5}',
    '.diagrama .d-carril{fill:var(--d-superficie2);stroke:none}',
    '.diagrama .d-rotulo{fill:var(--d-superficie);stroke:none}',
    '.diagrama .d-t{fill:var(--d-texto);font-size:13px}',
    '.diagrama .d-t-s{fill:var(--d-suave);font-size:11.5px}',
    '.diagrama .d-t-b{fill:var(--d-texto);font-size:13.5px;font-weight:700}',
    '.diagrama .d-t-h{fill:var(--d-texto);font-size:15px;font-weight:700}',
    '.diagrama .d-t-a{fill:var(--d-acento);font-size:12px;font-weight:700}',
    '.diagrama .d-c{text-anchor:middle}',
    '.diagrama .d-num{fill:var(--d-acento);stroke:none}',
    '.diagrama .d-num-t{fill:var(--d-superficie);font-size:11px;font-weight:700;text-anchor:middle}',
    '.diagrama .d-l{fill:none;stroke:var(--d-linea);stroke-width:1.5}',
    '.diagrama .d-l-p{fill:none;stroke:var(--d-linea);stroke-width:1.5;stroke-dasharray:5 4}',
    '.diagrama .d-l-a{fill:none;stroke:var(--d-acento);stroke-width:1.7}',
    '.diagrama .d-l-ok{fill:none;stroke:var(--d-ok);stroke-width:1.7}',
    '.diagrama .d-l-peligro{fill:none;stroke:var(--d-peligro);stroke-width:1.7}',
    '.diagrama .d-marca{fill:var(--d-linea)}',
    '.diagrama .d-marca-a{fill:var(--d-acento)}',
    '.diagrama .d-marca-ok{fill:var(--d-ok)}',
    '.diagrama .d-marca-peligro{fill:var(--d-peligro)}',
  ].join('\n');
}

/** SVG suelto para docs/manual: lleva sus estilos y un fondo propio (no hay página alrededor que se los dé). */
export function svgIndependiente(svg) {
  const limpio = svg.replace(/^\s*<\?xml[^>]*\?>\s*/, '');
  return limpio.replace(/(<svg\b[^>]*>)/, `$1\n<style>\n${cssDeDiagramas({ sitio: false })}\n</style>\n<rect class="d-fondo" width="100%" height="100%"/>`);
}

/** SVG incrustado en la página: ids con prefijo para que dos diagramas de la misma página no choquen. */
export function svgEnLinea(svg, prefijo) {
  const limpio = svg.replace(/^\s*<\?xml[^>]*\?>\s*/, '').replace(/<!--[\s\S]*?-->/g, '');
  return limpio
    .replace(/\bid="([^"]+)"/g, `id="${prefijo}-$1"`)
    .replace(/url\(#([^)]+)\)/g, `url(#${prefijo}-$1)`)
    .replace(/\b(aria-labelledby|href|xlink:href)="#?([^"]+)"/g, (todo, atributo, valor) =>
      atributo === 'aria-labelledby' ? `aria-labelledby="${valor.split(/\s+/).map((v) => `${prefijo}-${v}`).join(' ')}"` : `${atributo}="#${prefijo}-${valor}"`,
    );
}

// ---------------------------------------------------------------------------------------------------------------------
// HTML
// ---------------------------------------------------------------------------------------------------------------------

function claseDeCita(hijos) {
  const primero = hijos[0];
  const etiqueta = primero?.tipo === 'parrafo' ? /^\*\*([^*:]+):?\*\*/.exec(primero.texto)?.[1] : undefined;
  if (!etiqueta) return 'nota-guia';
  const clave = sinTildes(etiqueta).replace(/[^a-z]+/g, '');
  return ['recorridos', 'importante', 'ojo', 'nota', 'verdad'].includes(clave) ? `nota-guia ${clave}` : 'nota-guia';
}

function htmlDeBloques(bloques, guia, ctx) {
  const linea = (t) => enLineaHtml(t, ctx);
  return bloques
    .map((b) => {
      switch (b.tipo) {
        case 'titulo':
          if (b.nivel === 1) return '';
          return `<h${String(b.nivel)} id="${b.id}">${linea(b.texto)}<a class="ancla" href="#${b.id}" aria-label="Enlace a esta sección">#</a></h${String(b.nivel)}>`;
        case 'parrafo':
          return `<p>${linea(b.texto)}</p>`;
        case 'lista': {
          const etiqueta = b.ordenada ? 'ol' : 'ul';
          return `<${etiqueta}>${b.items.map((t) => `<li>${linea(t)}</li>`).join('')}</${etiqueta}>`;
        }
        case 'tabla':
          return `<div class="tabla-guia"><table><thead><tr>${b.cabecera.map((c) => `<th scope="col">${linea(c)}</th>`).join('')}</tr></thead><tbody>${b.filas
            .map((f) => `<tr>${f.map((c) => `<td>${linea(c)}</td>`).join('')}</tr>`)
            .join('')}</tbody></table></div>`;
        case 'codigo':
          return `<pre data-copiable><code${b.lenguaje ? ` class="lenguaje-${escapar(b.lenguaje)}"` : ''}>${escapar(b.texto)}</code></pre>`;
        case 'cita':
          return `<aside class="${claseDeCita(b.hijos)}">${htmlDeBloques(b.hijos, guia, ctx)}</aside>`;
        case 'imagen': {
          const nombre = b.ruta.replace(/^diagramas\//, '');
          const svg = ctx.diagramas.get(nombre);
          if (!svg) {
            ctx.alertas.push(`diagrama inexistente: ${b.ruta}`);
            return `<div class="vacio">Falta el diagrama ${escapar(b.ruta)}.</div>`;
          }
          return `<figure class="diagrama-figura"><div class="diagrama-marco">${svgEnLinea(svg, `${guia.slug}-${aSlug(nombre)}`)}</div><figcaption>${escapar(b.alt)}</figcaption></figure>`;
        }
        default:
          return '';
      }
    })
    .join('\n');
}

function lateralDeGuias(ctx, actual) {
  return `<h2>Guías</h2><ul><li><a href="${ctx.raiz}g/index.html"${actual === 'indice' ? ' aria-current="page"' : ''}>Todas las guías</a></li></ul>${ctx.secciones
    .map(
      (s) =>
        `<h2>${escapar(s.nombre)}</h2><ul>${s.guias.map((g) => `<li><a href="${ctx.raiz}${rutaDeGuia(g)}"${actual === g.slug ? ' aria-current="page"' : ''}>${escapar(g.titulo)}</a></li>`).join('')}</ul>`,
    )
    .join('')}`;
}

/** Contexto de generación de las guías: qué recorridos existen, los diagramas y los avisos. `raiz` es el prefijo hasta la raíz del sitio. */
export function crearContextoDeGuias({ guias, secciones, diagramas, recorridosPorId, raiz }) {
  return {
    guias,
    secciones,
    diagramas,
    raiz,
    alertas: [],
    recorrido: (id) => {
      const r = recorridosPorId.get(id);
      return r ? { href: `../r/${r.dominio}/${r.archivo}.html`, titulo: r.titulo } : null;
    },
  };
}

function paginaDeGuia(ctx, guia, generado) {
  const bloques = bloquesDeMarkdown(guia.cuerpo);
  const indice = bloques.filter((b) => b.tipo === 'titulo' && b.nivel === 2);
  const posicion = ctx.guias.findIndex((g) => g.slug === guia.slug);
  const anterior = ctx.guias[posicion - 1];
  const siguiente = ctx.guias[posicion + 1];
  const toc = indice.length > 1 ? `<nav class="indice-pasos" aria-label="En esta página"><h2>En esta página</h2><ol>${indice.map((t) => `<li><a href="#${t.id}">${escapar(textoPlano(t.texto))}</a></li>`).join('')}</ol></nav>` : '';
  const vecinos = `<nav class="navegacion-recorridos" aria-label="Guías">${
    anterior ? `<a href="${anterior.slug}.html" rel="prev"><small>← Anterior</small>${escapar(anterior.titulo)}</a>` : '<span></span>'
  }${siguiente ? `<a class="siguiente" href="${siguiente.slug}.html" rel="next"><small>Siguiente →</small>${escapar(siguiente.titulo)}</a>` : ''}</nav>`;
  const contenido = `<header class="recorrido-cabecera"><div class="etiquetas"><span class="etiqueta">${escapar(guia.seccion)}</span><span class="etiqueta neutra">Guía</span></div><h1>${escapar(guia.titulo)}</h1><p class="resumen">${escapar(guia.resumen)}</p></header>${toc}<div class="guia">${htmlDeBloques(bloques, guia, ctx)}</div>${vecinos}`;
  return pagina({
    titulo: guia.titulo,
    descripcion: guia.resumen,
    raiz: '../',
    seccion: 'guias',
    lateral: lateralDeGuias(ctx, guia.slug),
    migas: [{ texto: 'Manual', href: '../index.html' }, { texto: 'Guías', href: 'index.html' }, { texto: guia.titulo }],
    contenido,
    generado,
  });
}

function paginaDeIndiceDeGuias(ctx, generado) {
  const secciones = ctx.secciones
    .map(
      (s) =>
        `<h2 class="seccion-titulo">${escapar(s.nombre)}</h2><div class="rejilla">${s.guias
          .map((g) => `<a class="tarjeta" href="${g.slug}.html"><h3>${escapar(g.titulo)}</h3><p>${escapar(g.resumen)}</p></a>`)
          .join('')}</div>`,
    )
    .join('');
  const contenido = `<div class="heroe"><h1>Guías</h1><p>Lo que no sale de un recorrido: cómo está construido Validasoft, el flujo completo del vínculo a la validación, la configuración con sus valores por defecto y la integración por API.</p><div class="cifras"><div class="cifra"><b>${String(ctx.guias.length)}</b><span>guías</span></div><div class="cifra"><b>${String(ctx.diagramas.size)}</b><span>diagramas</span></div></div></div>${secciones}`;
  return pagina({
    titulo: 'Guías',
    descripcion: 'Guías del manual de Validasoft: arquitectura, flujo de punta a punta, configuración e integración.',
    raiz: '../',
    seccion: 'guias',
    lateral: lateralDeGuias(ctx, 'indice'),
    migas: [{ texto: 'Manual', href: '../index.html' }, { texto: 'Guías' }],
    contenido,
    generado,
  });
}

/** Tarjetas de las guías para la portada (`raiz` = prefijo hasta la raíz, aquí `./`). */
export function bloqueDeGuiasParaPortada(secciones) {
  if (secciones.length === 0) return '';
  return `<h2 class="seccion-titulo">Guías</h2>${secciones
    .map(
      (s) =>
        `<h3 class="subseccion-titulo">${escapar(s.nombre)}</h3><div class="rejilla">${s.guias
          .map((g) => `<a class="tarjeta" href="${rutaDeGuia(g)}"><h3>${escapar(g.titulo)}</h3><p>${escapar(g.resumen)}</p></a>`)
          .join('')}</div>`,
    )
    .join('')}`;
}

/** Entradas del buscador: una por guía y una por cada sección `##`. */
export function entradasDeBusquedaDeGuias(guias) {
  const entradas = [];
  for (const g of guias) {
    const bloques = bloquesDeMarkdown(g.cuerpo);
    const textos = [];
    let actual = null;
    const cerrar = () => {
      if (actual) entradas.push({ t: `${g.titulo} › ${textoPlano(actual.titulo)}`, c: `Guía · ${g.seccion}`, u: `g/${g.slug}.html#${actual.id}`, x: textos.join(' '), k: 'g' });
    };
    const aplanar = (b) => {
      if (b.tipo === 'parrafo') return textoPlano(b.texto);
      if (b.tipo === 'lista') return b.items.map(textoPlano).join(' ');
      if (b.tipo === 'tabla') return [...b.cabecera, ...b.filas.flat()].map(textoPlano).join(' ');
      if (b.tipo === 'cita') return b.hijos.map(aplanar).join(' ');
      if (b.tipo === 'codigo') return b.texto;
      if (b.tipo === 'titulo') return textoPlano(b.texto);
      return '';
    };
    const todo = bloques.map(aplanar).join(' ');
    entradas.push({ t: g.titulo, c: `Guía · ${g.seccion}`, u: rutaDeGuia(g), x: `${g.resumen} ${todo}`, k: 'g' });
    for (const b of bloques) {
      if (b.tipo === 'titulo' && b.nivel === 2) {
        cerrar();
        actual = { titulo: b.texto, id: b.id };
        textos.length = 0;
      } else if (actual) textos.push(aplanar(b));
    }
    cerrar();
  }
  return entradas;
}

/**
 * Páginas del sitio para las guías. Devuelve `paginas` (`ruta` relativa al sitio y `html`), `indice` (buscador), `secciones` (para la portada)
 * y `alertas` (enlaces o diagramas rotos).
 */
export function construirSitioDeGuias(lectura, { recorridos, generado }) {
  const ctx = crearContextoDeGuias({ ...lectura, recorridosPorId: new Map(recorridos.map((r) => [r.id, r])), raiz: '../' });
  const paginas = lectura.guias.length === 0 ? [] : [{ ruta: 'g/index.html', html: paginaDeIndiceDeGuias(ctx, generado) }];
  for (const g of lectura.guias) paginas.push({ ruta: rutaDeGuia(g), html: paginaDeGuia(ctx, g, generado) });
  return { paginas, indice: entradasDeBusquedaDeGuias(lectura.guias), secciones: lectura.secciones, alertas: ctx.alertas };
}

// ---------------------------------------------------------------------------------------------------------------------
// Validación
// ---------------------------------------------------------------------------------------------------------------------

/**
 * Problemas de las guías: recorridos que no están en el inventario, guías, anclas, módulos de la API y diagramas que no existen, y diagramas sin
 * usar. `modulosApi` (un Set de slugs) es opcional.
 */
export function validarGuias(lectura, { inventario, modulosApi, recorridos = new Set() }) {
  const problemas = [];
  const anclas = new Map(lectura.guias.map((g) => [g.slug, new Set(bloquesDeMarkdown(g.cuerpo).filter((b) => b.tipo === 'titulo').map((b) => b.id))]));
  const usados = new Set();
  for (const g of lectura.guias) {
    const fuera = g.cuerpo.replace(/```[\s\S]*?```/g, '');
    for (const [, id] of fuera.matchAll(ID_RECORRIDO)) if (!inventario.has(id) && !recorridos.has(id)) problemas.push(`${g.slug}: el recorrido ${id} no está en INVENTARIO.md`);
    for (const [, slug, ancla] of fuera.matchAll(/\]\(guia:([a-z0-9-]+)(?:#([\w-]+))?\)/g)) {
      if (!anclas.has(slug)) problemas.push(`${g.slug}: la guía ${slug} no existe`);
      else if (ancla && !anclas.get(slug).has(ancla)) problemas.push(`${g.slug}: la guía ${slug} no tiene la sección #${ancla}`);
    }
    if (modulosApi) for (const [, modulo] of fuera.matchAll(/\]\(api:([a-z0-9-]*)\)/g)) if (modulo && !modulosApi.has(modulo)) problemas.push(`${g.slug}: el módulo de la API ${modulo} no existe`);
    for (const [, ruta] of fuera.matchAll(/!\[[^\]]*\]\((\S+)\)/g)) {
      const nombre = ruta.replace(/^diagramas\//, '');
      usados.add(nombre);
      if (!lectura.diagramas.has(nombre)) problemas.push(`${g.slug}: falta el diagrama ${ruta}`);
    }
  }
  for (const nombre of lectura.diagramas.keys()) if (!usados.has(nombre)) problemas.push(`el diagrama ${nombre} no lo usa ninguna guía`);
  return problemas;
}

// ---------------------------------------------------------------------------------------------------------------------
// Markdown (docs/manual/guias)
// ---------------------------------------------------------------------------------------------------------------------

const relativa = (desde, hacia) => relative(desde, hacia).split(sep).join(posix.sep) || '.';

/**
 * Markdown de una guía con los enlaces resueltos a rutas relativas de `docs/manual/guias/`. `existeRecorrido(id)` dice si el recorrido ya tiene su
 * Markdown (entonces se enlaza) y `tituloDe(id)` da su título.
 */
export function markdownDeGuia(guia, { raizDelRepositorio, carpetaGuias, carpetaDocs, existeRecorrido, tituloDe }) {
  const resolver = (linea) =>
    linea
      .split(/(`[^`]*`)/)
      .map((trozo) => {
        if (trozo.startsWith('`')) return trozo;
        return trozo
          .replace(ID_RECORRIDO, (_, id, etiqueta) => {
            if (!existeRecorrido(id)) return `*${etiqueta ?? humanizar(id)} (recorrido pendiente)*`;
            const [dominio, archivo] = id.split('/');
            return `[${tituloDe(id)}](${relativa(carpetaGuias, join(carpetaDocs, dominio, `${archivo}.md`))})`;
          })
          .replace(/\[([^\]]+)\]\(([^)\s]+)\)/g, (todo, texto, href) => {
            const especial = destinoEspecial(href, 'md');
            if (!especial) return todo;
            if (especial.ruta !== undefined) return `[${texto}](${relativa(carpetaGuias, join(raizDelRepositorio, especial.ruta))})`;
            return `[${texto}](${especial.href})`;
          });
      })
      .join('');
  let dentroDeCodigo = false;
  const cuerpo = guia.cuerpo
    .split('\n')
    .map((linea) => {
      if (/^```/.test(linea)) {
        dentroDeCodigo = !dentroDeCodigo;
        return linea;
      }
      return dentroDeCodigo ? linea : resolver(linea);
    })
    .join('\n');
  const encabezado = ['---', `titulo: ${JSON.stringify(guia.titulo)}`, `seccion: ${JSON.stringify(guia.seccion)}`, `resumen: ${JSON.stringify(guia.resumen)}`, '---', ''];
  return `${[...encabezado, `# ${guia.titulo}`, '', guia.resumen, '', cuerpo.trim()].join('\n').replace(/\n{3,}/g, '\n\n')}\n`;
}

/** README de `docs/manual/guias/`. */
export function indiceDeGuiasMarkdown(secciones) {
  const lineas = ['# Guías del manual', '', 'Lo que no sale de un recorrido: arquitectura, flujo de punta a punta, configuración e integración. Se escriben en `e2e/manual/guias/` y `pnpm manual` las publica aquí y en el sitio.', ''];
  for (const s of secciones) lineas.push(`## ${s.nombre}`, '', ...s.guias.map((g) => `- [${g.titulo}](${g.slug}.md) — ${g.resumen}`), '');
  return `${lineas.join('\n').trimEnd()}\n`;
}

/** Todos los archivos de `docs/manual/guias/`: el README, una guía por archivo y los diagramas. */
export function archivosDeGuias(lectura, opciones) {
  if (lectura.guias.length === 0) return [];
  return [
    { ruta: 'README.md', texto: indiceDeGuiasMarkdown(lectura.secciones) },
    ...lectura.guias.map((g) => ({ ruta: `${g.slug}.md`, texto: markdownDeGuia(g, opciones) })),
    ...[...lectura.diagramas].map(([nombre, svg]) => ({ ruta: `diagramas/${nombre}`, texto: svgIndependiente(svg) })),
  ];
}

/** `tituloDe` y `existeRecorrido` a partir de los recorridos de la corrida y de lo que ya hay escrito en `docs/manual/`. */
export function resolutorDeRecorridosEscritos(recorridos, carpetaDocs) {
  const porId = new Map(recorridos.map((r) => [r.id, r]));
  const escrito = (id) => {
    const [dominio, archivo] = id.split('/');
    return join(carpetaDocs, dominio, `${archivo}.md`);
  };
  return {
    existeRecorrido: (id) => porId.has(id) || existsSync(escrito(id)),
    tituloDe: (id) => porId.get(id)?.titulo ?? leerEncabezado(readFileSync(escrito(id), 'utf8'))?.titulo ?? humanizar(id),
  };
}

