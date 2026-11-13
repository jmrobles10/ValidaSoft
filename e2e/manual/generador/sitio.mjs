// Páginas HTML de los recorridos: portada, listas por actor y por dominio, y la página de cada recorrido.
import { readFileSync } from 'node:fs';
import { join } from 'node:path';
import { escapar, medidasPng, nombreDeImagen, parrafos, plural, textoEnHtml } from './comun.mjs';
import { bloqueDeGuiasParaPortada } from './guias.mjs';
import { pagina } from './plantilla.mjs';

export const rutaDeRecorrido = (r) => `r/${r.dominio}/${r.archivo}.html`;
const rutaDeActor = (a) => `actor/${a.slug}.html`;
const rutaDeDominio = (d) => `dominio/${d.slug}.html`;

/** Contexto con búsquedas rápidas sobre el catálogo y los recorridos. */
export function crearContexto(recorridos, catalogo, { extensionDeImagen = '' } = {}) {
  const porId = new Map(recorridos.map((r) => [r.id, r]));
  return {
    recorridos,
    catalogo,
    porId,
    extensionDeImagen,
    dominio: (slug) => catalogo.dominios.find((d) => d.slug === slug),
    actor: (nombre) => catalogo.actores.find((a) => a.nombre === nombre),
    deDominio: (slug) => recorridos.filter((r) => r.dominio === slug),
    deActor: (nombre) => recorridos.filter((r) => r.actor === nombre),
  };
}

function lateral(ctx, raiz, actual) {
  const actores = ctx.catalogo.actores
    .map((a) => {
      const n = ctx.deActor(a.nombre).length;
      return `<li><a href="${raiz}${rutaDeActor(a)}"${actual === `actor:${a.slug}` ? ' aria-current="page"' : ''}>${escapar(a.nombre)}<span class="cuenta">${String(n)}</span></a></li>`;
    })
    .join('');
  const dominios = ctx.catalogo.dominios
    .map((d) => {
      const lista = ctx.deDominio(d.slug);
      const abierto = actual === `dominio:${d.slug}` || lista.some((r) => actual === `r:${r.id}`);
      const items = lista
        .map((r) => `<li><a href="${raiz}${rutaDeRecorrido(r)}"${actual === `r:${r.id}` ? ' aria-current="page"' : ''}>${escapar(r.titulo)}</a></li>`)
        .join('');
      return `<li><details${abierto ? ' open' : ''}><summary>${escapar(d.nombre)}<span class="cuenta">${String(lista.length)}</span></summary><ul><li><a href="${raiz}${rutaDeDominio(d)}"${actual === `dominio:${d.slug}` ? ' aria-current="page"' : ''}>Ver todo el dominio</a></li>${items}</ul></details></li>`;
    })
    .join('');
  return `<h2>Por actor</h2><ul>${actores}</ul><h2>Por dominio</h2><ul>${dominios}</ul>`;
}

function tarjetaDeRecorrido(ctx, r, raiz) {
  const dominio = ctx.dominio(r.dominio);
  return `<li><a href="${raiz}${rutaDeRecorrido(r)}"><h3>${escapar(r.titulo)}</h3><p>${escapar(r.resumen)}</p><div class="etiquetas"><span class="etiqueta">${escapar(r.actor)}</span><span class="etiqueta neutra">${escapar(dominio?.nombre ?? r.dominio)}</span><span class="etiqueta neutra">${plural(r.pasos.length, 'paso', 'pasos')}</span></div></a></li>`;
}

/** Entradas del buscador para los recorridos. */
function entradasDeBusqueda(ctx) {
  return ctx.recorridos.map((r) => {
    const dominio = ctx.dominio(r.dominio);
    const texto = [r.resumen, ...r.precondiciones, ...r.pasos.flatMap((p) => [p.titulo, p.explicacion, ...p.variantes.flatMap((v) => [v.cuando, v.entonces]), ...p.configuracion.flatMap((c) => [c.ajuste, c.ruta ?? '', c.efecto ?? '']), p.ruta ?? ''])].join(' ');
    return { t: r.titulo, c: `${r.actor} · ${dominio?.nombre ?? r.dominio}`, u: rutaDeRecorrido(r), x: texto, k: 'r' };
  });
}

function paginaPortada(ctx, { generado, totalEndpoints, seccionesDeGuias = [] }) {
  const pasos = ctx.recorridos.reduce((suma, r) => suma + r.pasos.length, 0);
  const actores = ctx.catalogo.actores
    .map((a) => `<a class="tarjeta" href="${rutaDeActor(a)}"><h3>${escapar(a.nombre)}</h3><p>${escapar(a.descripcion)}</p><span class="cuenta">${plural(ctx.deActor(a.nombre).length, 'recorrido', 'recorridos')}</span></a>`)
    .join('');
  const dominios = ctx.catalogo.dominios
    .map((d) => `<a class="tarjeta" href="${rutaDeDominio(d)}"><h3>${escapar(d.nombre)}</h3><p>${escapar(d.descripcion)}</p><span class="cuenta">${plural(ctx.deDominio(d.slug).length, 'recorrido', 'recorridos')}</span></a>`)
    .join('');
  const cuerpo =
    ctx.recorridos.length === 0
      ? '<div class="vacio"><strong>Todavía no hay recorridos.</strong><p>Corre <code>pnpm manual:recorrer</code> para ejecutar los recorridos y luego <code>pnpm manual</code> para generar el manual.</p></div>'
      : `<h2 class="seccion-titulo">Por actor</h2><div class="rejilla">${actores}</div><h2 class="seccion-titulo">Por dominio</h2><div class="rejilla">${dominios}</div>`;
  const api =
    totalEndpoints > 0
      ? `<h2 class="seccion-titulo">Referencia de la API</h2><div class="rejilla"><a class="tarjeta" href="api/index.html"><h3>API de Validasoft</h3><p>Cada recurso con sus operaciones, permisos, parámetros, cuerpos, respuestas y ejemplos, generados del contrato OpenAPI.</p><span class="cuenta">${plural(totalEndpoints, 'operación', 'operaciones')}</span></a></div>`
      : '';
  const contenido = `<div class="heroe"><h1>Manual de Validasoft</h1><p>Cada flujo del producto se ejecuta de verdad en la aplicación; los pantallazos, los pasos y las variantes salen de esa ejecución, así que el manual no se desactualiza en silencio.</p><div class="cifras"><div class="cifra"><b>${String(ctx.recorridos.length)}</b><span>recorridos</span></div><div class="cifra"><b>${String(pasos)}</b><span>pasos con pantallazo</span></div><div class="cifra"><b>${String(totalEndpoints)}</b><span>operaciones de la API</span></div></div></div>${cuerpo}${bloqueDeGuiasParaPortada(seccionesDeGuias)}${api}`;
  return pagina({ titulo: 'Portada', descripcion: 'Manual de Validasoft: recorridos documentados con pantallazos y referencia de la API.', raiz: './', seccion: 'recorridos', lateral: lateral(ctx, './', 'portada'), contenido, generado });
}

function paginaDeActor(ctx, actor, generado) {
  const lista = ctx.deActor(actor.nombre);
  const cuerpo = lista.length
    ? `<ul class="lista-recorridos">${lista.map((r) => tarjetaDeRecorrido(ctx, r, '../')).join('')}</ul>`
    : '<div class="vacio">Todavía no hay recorridos de este actor.</div>';
  const contenido = `<h1>${escapar(actor.nombre)}</h1><p class="modulo-intro">${escapar(actor.descripcion)}</p>${cuerpo}`;
  return pagina({ titulo: actor.nombre, descripcion: actor.descripcion, raiz: '../', seccion: 'recorridos', lateral: lateral(ctx, '../', `actor:${actor.slug}`), migas: [{ texto: 'Manual', href: '../index.html' }, { texto: actor.nombre }], contenido, generado });
}

function paginaDeDominio(ctx, dominio, generado) {
  const lista = ctx.deDominio(dominio.slug);
  const cuerpo = lista.length
    ? `<ul class="lista-recorridos">${lista.map((r) => tarjetaDeRecorrido(ctx, r, '../')).join('')}</ul>`
    : '<div class="vacio">Todavía no hay recorridos en este dominio.</div>';
  const contenido = `<h1>${escapar(dominio.nombre)}</h1><p class="modulo-intro">${escapar(dominio.descripcion)}</p>${cuerpo}`;
  return pagina({ titulo: dominio.nombre, descripcion: dominio.descripcion, raiz: '../', seccion: 'recorridos', lateral: lateral(ctx, '../', `dominio:${dominio.slug}`), migas: [{ texto: 'Manual', href: '../index.html' }, { texto: dominio.nombre }], contenido, generado });
}

function figura(r, imagen, tipo, alt, leyendaPropia, estilo = '', extension = '') {
  const medidas = (() => {
    try {
      return medidasPng(readFileSync(join(r.carpeta, imagen)));
    } catch {
      return null;
    }
  })();
  const src = `../../img/${r.dominio}/${r.archivo}/${nombreDeImagen(imagen, extension)}`;
  const tamano = medidas ? ` width="${String(medidas.ancho)}" height="${String(medidas.alto)}"` : '';
  const leyenda = leyendaPropia ?? (tipo === 'celular' ? 'Celular' : 'Escritorio');
  return `<figure class="captura ${tipo}"${estilo}><button type="button" data-zoom="${escapar(src)}" data-alt="${escapar(alt)}" aria-label="Ampliar el pantallazo: ${escapar(alt)}"><img src="${escapar(src)}"${tamano} loading="lazy" alt="${escapar(alt)}"></button><figcaption>${leyenda}</figcaption></figure>`;
}

function bloqueDePaso(ctx, r, p) {
  const enlace = (id) => {
    const destino = ctx.porId.get(id);
    return destino ? { href: `../../${rutaDeRecorrido(destino)}`, titulo: destino.titulo } : null;
  };
  const texto = parrafos(p.explicacion)
    .map((t) => `<p>${textoEnHtml(t, enlace)}</p>`)
    .join('');
  const ruta = p.ruta ? `<p class="pantalla">Pantalla: <code>${escapar(p.ruta)}</code></p>` : '';
  const variantes = p.variantes.length
    ? `<aside class="nota variantes"><h3>Variantes</h3><ul>${p.variantes.map((v) => `<li><strong>${textoEnHtml(v.cuando, enlace)}:</strong> ${textoEnHtml(v.entonces, enlace)}</li>`).join('')}</ul></aside>`
    : '';
  const configuracion = p.configuracion.length
    ? `<aside class="nota config"><h3>Configuración que lo afecta</h3><ul>${p.configuracion
        .map((c) => `<li><strong>${textoEnHtml(c.ajuste, enlace)}</strong>${c.ruta ? ` <code>${escapar(c.ruta)}</code>` : ''}${c.efecto ? ` — ${textoEnHtml(c.efecto, enlace)}` : ''}</li>`)
        .join('')}</ul></aside>`
    : '';
  const alt = `Paso ${String(p.orden)}: ${p.titulo}`;
  const extras = (p.imagenes.extras ?? []).length
    ? `<div class="capturas capturas-extra">${p.imagenes.extras.map((x) => figura(r, x.archivo, 'extra', `${alt} (${String(x.ancho)} px de ancho)`, `${String(x.ancho)} px de ancho`, ` style="flex: ${String(x.ancho)} 1 0; max-width: ${String(x.ancho)}px"`, ctx.extensionDeImagen)).join('')}</div>`
    : '';
  const capturas = `<div class="capturas">${figura(r, p.imagenes.claro, 'claro', `${alt} (escritorio)`, undefined, '', ctx.extensionDeImagen)}${p.imagenes.celular ? figura(r, p.imagenes.celular, 'celular', `${alt} (celular)`, undefined, '', ctx.extensionDeImagen) : ''}</div>${extras}`;
  return `<section class="paso" id="paso-${String(p.orden)}" aria-labelledby="titulo-paso-${String(p.orden)}"><header><span class="numero" aria-hidden="true">${String(p.orden)}</span><h2 id="titulo-paso-${String(p.orden)}">${escapar(p.titulo)}</h2></header><div class="paso-texto">${texto}${ruta}${variantes}${configuracion}</div>${capturas}</section>`;
}

function paginaDeRecorrido(ctx, r, generado) {
  const dominio = ctx.dominio(r.dominio);
  const mismos = ctx.deDominio(r.dominio);
  const posicion = mismos.findIndex((x) => x.id === r.id);
  const anterior = mismos[posicion - 1];
  const siguiente = mismos[posicion + 1];
  const antes = r.precondiciones.length
    ? `<section class="antes"><h2>Antes de empezar</h2><ul>${r.precondiciones.map((t) => `<li>${textoEnHtml(t)}</li>`).join('')}</ul></section>`
    : '';
  const indice = `<nav class="indice-pasos" aria-label="Pasos del recorrido"><h2>Pasos</h2><ol>${r.pasos.map((p) => `<li><a href="#paso-${String(p.orden)}">${escapar(p.titulo)}</a></li>`).join('')}</ol></nav>`;
  const relacionados = r.relacionados
    .map((id) => ctx.porId.get(id))
    .filter(Boolean)
    .map((x) => `<li><a href="../../${rutaDeRecorrido(x)}">${escapar(x.titulo)}</a> <span class="etiqueta neutra">${escapar(x.actor)}</span></li>`)
    .join('');
  const bloqueRelacionados = relacionados ? `<section class="relacionados"><h2>Recorridos relacionados</h2><ul>${relacionados}</ul></section>` : '';
  const vecinos = `<nav class="navegacion-recorridos" aria-label="Recorridos del dominio">${
    anterior ? `<a href="../../${rutaDeRecorrido(anterior)}" rel="prev"><small>← Anterior</small>${escapar(anterior.titulo)}</a>` : '<span></span>'
  }${siguiente ? `<a class="siguiente" href="../../${rutaDeRecorrido(siguiente)}" rel="next"><small>Siguiente →</small>${escapar(siguiente.titulo)}</a>` : ''}</nav>`;
  const contenido = `<header class="recorrido-cabecera"><div class="etiquetas"><a class="etiqueta" href="../../${rutaDeActor(ctx.actor(r.actor))}">${escapar(r.actor)}</a><a class="etiqueta neutra" href="../../${rutaDeDominio(dominio)}">${escapar(dominio.nombre)}</a><span class="etiqueta neutra">${plural(r.pasos.length, 'paso', 'pasos')}</span></div><h1>${escapar(r.titulo)}</h1><p class="resumen">${escapar(r.resumen)}</p></header>${antes}${indice}${r.pasos.map((p) => bloqueDePaso(ctx, r, p)).join('')}${bloqueRelacionados}${vecinos}`;
  return pagina({
    titulo: r.titulo,
    descripcion: r.resumen,
    raiz: '../../',
    seccion: 'recorridos',
    lateral: lateral(ctx, '../../', `r:${r.id}`),
    migas: [{ texto: 'Manual', href: '../../index.html' }, { texto: dominio.nombre, href: `../../${rutaDeDominio(dominio)}` }, { texto: r.titulo }],
    contenido,
    generado,
  });
}

/**
 * Todas las páginas de los recorridos. Devuelve las páginas (`ruta` relativa al sitio, `html`), las entradas del buscador y las imágenes a
 * copiar (`origen` absoluto, `destino` relativo al sitio).
 */
export function construirSitioDeRecorridos(recorridos, catalogo, { generado, totalEndpoints, seccionesDeGuias = [], extensionDeImagen = '' }) {
  const ctx = crearContexto(recorridos, catalogo, { extensionDeImagen });
  const paginas = [{ ruta: 'index.html', html: paginaPortada(ctx, { generado, totalEndpoints, seccionesDeGuias }) }];
  for (const a of catalogo.actores) paginas.push({ ruta: rutaDeActor(a), html: paginaDeActor(ctx, a, generado) });
  for (const d of catalogo.dominios) paginas.push({ ruta: rutaDeDominio(d), html: paginaDeDominio(ctx, d, generado) });
  const imagenes = [];
  for (const r of recorridos) {
    paginas.push({ ruta: rutaDeRecorrido(r), html: paginaDeRecorrido(ctx, r, generado) });
    for (const p of r.pasos) {
      for (const imagen of [p.imagenes.claro, p.imagenes.celular, ...(p.imagenes.extras ?? []).map((x) => x.archivo)]) {
        if (imagen) imagenes.push({ origen: join(r.carpeta, imagen), destino: `img/${r.dominio}/${r.archivo}/${nombreDeImagen(imagen, extensionDeImagen)}` });
      }
    }
  }
  return { paginas, indice: entradasDeBusqueda(ctx), imagenes };
}
