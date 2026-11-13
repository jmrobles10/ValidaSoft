// Pruebas de las páginas de guía del manual: lectura, Markdown, enlaces, diagramas, páginas HTML, buscador y, sobre todo, que las guías reales sean válidas
// (todo recorrido que citan está en el inventario, todo enlace y diagrama existe y los diagramas no llevan colores fijos).
import { existsSync, readdirSync } from 'node:fs';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { describe, expect, it } from 'vitest';
import {
  archivosDeGuias,
  bloquesDeMarkdown,
  celdasDeFila,
  construirSitioDeGuias,
  cssDeDiagramas,
  enLineaHtml,
  entradasDeBusquedaDeGuias,
  humanizar,
  leerGuias,
  leerInventario,
  markdownDeGuia,
  resolutorDeRecorridosEscritos,
  separarEncabezado,
  svgEnLinea,
  svgIndependiente,
  textoPlano,
  validarEncabezado,
  validarGuias,
} from './guias.mjs';

const aqui = dirname(fileURLToPath(import.meta.url));
const CARPETA_GUIAS = join(aqui, '..', 'guias');
const INVENTARIO = join(aqui, '..', 'INVENTARIO.md');
const DOCS_API = join(aqui, '..', '..', '..', 'docs', 'manual', 'api');

const ENCABEZADO = '---\ntitulo: "Una guía"\nseccion: "Arquitectura"\norden: 3\nresumen: "Un resumen de más de cuarenta caracteres para que sea válido."\n---\n';

const contexto = (extra = {}) => ({
  recorrido: (id) => (id === 'tercero/aceptar' ? { href: '../r/tercero/aceptar.html', titulo: 'Acepta la invitación' } : null),
  alertas: [],
  diagramas: new Map(),
  ...extra,
});

describe('encabezado de una guía', () => {
  it('separa los datos del cuerpo y acepta comillas y números', () => {
    const { datos, cuerpo } = separarEncabezado(`${ENCABEZADO}\nHola.`);
    expect(datos).toMatchObject({ titulo: 'Una guía', seccion: 'Arquitectura', orden: 3 });
    expect(cuerpo.trim()).toBe('Hola.');
    expect(validarEncabezado(datos)).toBeNull();
  });

  it('rechaza lo que falta o es muy corto', () => {
    expect(validarEncabezado({ titulo: 'A', seccion: 'B', orden: 1, resumen: 'corto' })).toMatch(/resumen/);
    expect(validarEncabezado({ titulo: 'A', seccion: 'B', resumen: 'x'.repeat(50) })).toMatch(/orden/);
    expect(validarEncabezado({ seccion: 'B', orden: 1, resumen: 'x'.repeat(50) })).toMatch(/titulo/);
  });
});

describe('bloques de Markdown', () => {
  it('reconoce títulos con ids únicos, listas, tablas, código, citas e imágenes', () => {
    const texto = [
      '## Uno',
      '',
      'Párrafo en',
      'dos líneas.',
      '',
      '- a',
      '- b con `x|y`',
      '',
      '1. primero',
      '2. segundo',
      '',
      '| A | B |',
      '|---|---|',
      '| `a` | b \\| c |',
      '',
      '```json',
      '{ "a": 1 }',
      '```',
      '',
      '> **Nota:** dentro',
      '',
      '![Un diagrama](diagramas/x.svg)',
      '',
      '## Uno',
    ].join('\n');
    const b = bloquesDeMarkdown(texto);
    expect(b.filter((x) => x.tipo === 'titulo').map((x) => x.id)).toEqual(['uno', 'uno-2']);
    expect(b.find((x) => x.tipo === 'parrafo').texto).toBe('Párrafo en dos líneas.');
    expect(b.filter((x) => x.tipo === 'lista').map((x) => [x.ordenada, x.items.length])).toEqual([[false, 2], [true, 2]]);
    expect(b.find((x) => x.tipo === 'tabla')).toMatchObject({ cabecera: ['A', 'B'], filas: [['`a`', 'b | c']] });
    expect(b.find((x) => x.tipo === 'codigo')).toMatchObject({ lenguaje: 'json', texto: '{ "a": 1 }' });
    expect(b.find((x) => x.tipo === 'cita').hijos[0].texto).toBe('**Nota:** dentro');
    expect(b.find((x) => x.tipo === 'imagen')).toMatchObject({ alt: 'Un diagrama', ruta: 'diagramas/x.svg' });
  });

  it('una celda puede llevar una barra escapada', () => {
    expect(celdasDeFila('| a \\| b | c |')).toEqual(['a | b', 'c']);
  });
});

describe('texto en línea', () => {
  it('aplica el formato, protege el código y escapa el HTML', () => {
    const c = contexto();
    expect(enLineaHtml('Ve a **Inicio**, *ahora* y `a<b` <script>', c)).toBe('Ve a <strong>Inicio</strong>, <em>ahora</em> y <code>a&lt;b</code> &lt;script&gt;');
  });

  it('un recorrido existente se enlaza y uno pendiente queda como texto con su etiqueta', () => {
    const c = contexto();
    expect(enLineaHtml('[[tercero/aceptar]]', c)).toBe('<a href="../r/tercero/aceptar.html">Acepta la invitación</a>');
    expect(enLineaHtml('[[tercero/falta|Falta aún]]', c)).toContain('<span class="pendiente"');
    expect(enLineaHtml('[[tercero/falta|Falta aún]]', c)).toContain('>Falta aún</span>');
    expect(enLineaHtml('[[tercero/falta-algo]]', c)).toContain('>Falta algo</span>');
  });

  it('resuelve los enlaces especiales y avisa de los rotos', () => {
    const c = contexto();
    expect(enLineaHtml('[otra](guia:glosario#estados)', c)).toBe('<a href="glosario.html#estados">otra</a>');
    expect(enLineaHtml('[API](api:)', c)).toBe('<a href="../api/index.html">API</a>');
    expect(enLineaHtml('[vínculos](api:vinculos)', c)).toBe('<a href="../api/vinculos.html">vínculos</a>');
    expect(enLineaHtml('[guía](repo:docs/integracion.md)', c)).toContain('guía (<code>docs/integracion.md</code>)');
    expect(enLineaHtml('[`docs/x.md`](repo:docs/x.md)', c)).toBe('<span class="ruta-repo" title="Archivo del repositorio"><code>docs/x.md</code></span>');
    expect(enLineaHtml('[web](https://example.com)', c)).toContain('rel="noopener noreferrer"');
    expect(c.alertas).toEqual([]);
    expect(enLineaHtml('[roto](archivo-local.md)', c)).toBe('roto');
    expect(c.alertas).toHaveLength(1);
  });

  it('el texto plano no deja la sintaxis', () => {
    expect(textoPlano('Mira [[a/b-c|Un recorrido]] y [el sitio](https://x.co) con `código` y **negrita** ![d](diagramas/d.svg)')).toBe('Mira Un recorrido y el sitio con código y negrita ');
    expect(humanizar('tercero/entrar-sin-invitacion')).toBe('Entrar sin invitacion');
  });
});

describe('diagramas', () => {
  const svg = '<?xml version="1.0"?><svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 10 10" class="diagrama" role="img" aria-labelledby="t"><title id="t">T</title><defs><marker id="f"><path class="d-marca"/></marker></defs><path marker-end="url(#f)"/></svg>';

  it('en la página los ids llevan prefijo y no se pisan entre diagramas', () => {
    const a = svgEnLinea(svg, 'g-uno');
    expect(a).not.toContain('<?xml');
    expect(a).toContain('id="g-uno-f"');
    expect(a).toContain('url(#g-uno-f)');
    expect(a).toContain('aria-labelledby="g-uno-t"');
  });

  it('el SVG suelto lleva sus estilos y su fondo, y el de la página sigue el tema del sitio', () => {
    const suelto = svgIndependiente(svg);
    expect(suelto).toContain('<style>');
    expect(suelto).toContain('prefers-color-scheme:dark');
    expect(suelto).not.toContain('data-tema');
    expect(suelto).toContain('class="d-fondo"');
    expect(cssDeDiagramas({ sitio: true })).toContain("[data-tema='oscuro'] svg.diagrama");
    expect(cssDeDiagramas({ sitio: true })).toContain('.diagrama .d-caja-a');
  });
});

describe('páginas de las guías', () => {
  const lectura = (() => {
    const guias = [
      { slug: 'una', titulo: 'Una guía', seccion: 'Arquitectura', orden: 1, resumen: 'Resumen de la primera guía con texto suficiente.', cuerpo: '## Primera\n\nTexto con [[tercero/aceptar]].\n\n> **Recorridos:** [[tercero/aceptar]]\n\n## Segunda\n\n| A | B |\n|---|---|\n| 1 | 2 |\n' },
      { slug: 'otra', titulo: 'Otra guía', seccion: 'Integración', orden: 2, resumen: 'Resumen de la segunda guía con texto suficiente.', cuerpo: '## Única\n\nHola.' },
    ];
    return { guias, secciones: [{ nombre: 'Arquitectura', slug: 'arquitectura', guias: [guias[0]] }, { nombre: 'Integración', slug: 'integracion', guias: [guias[1]] }], diagramas: new Map() };
  })();
  const recorridos = [{ id: 'tercero/aceptar', dominio: 'tercero', archivo: 'aceptar', titulo: 'Acepta la invitación' }];

  it('genera el índice y una página por guía con navegación, índice y sin alertas', () => {
    const sitio = construirSitioDeGuias(lectura, { recorridos, generado: 'hoy' });
    expect(sitio.paginas.map((p) => p.ruta)).toEqual(['g/index.html', 'g/una.html', 'g/otra.html']);
    const una = sitio.paginas[1].html;
    expect(una).toContain('<h1>Una guía</h1>');
    expect(una).toContain('aria-label="En esta página"');
    expect(una).toContain('<a href="../r/tercero/aceptar.html">Acepta la invitación</a>');
    expect(una).toContain('class="nota-guia recorridos"');
    expect(una).toContain('<div class="tabla-guia">');
    expect(una).toContain('href="otra.html" rel="next"');
    expect(una).toContain('aria-current="page">Guías</a>');
    expect(sitio.alertas).toEqual([]);
    expect(sitio.paginas[0].html).toContain('Resumen de la segunda guía');
  });

  it('indexa cada guía y cada sección para el buscador', () => {
    const entradas = entradasDeBusquedaDeGuias(lectura.guias);
    expect(entradas.map((e) => e.u)).toEqual(['g/una.html', 'g/una.html#primera', 'g/una.html#segunda', 'g/otra.html', 'g/otra.html#unica']);
    expect(entradas[1]).toMatchObject({ t: 'Una guía › Primera', c: 'Guía · Arquitectura', k: 'g' });
  });

  it('el Markdown resuelve los enlaces a rutas relativas y marca lo pendiente', () => {
    const guia = {
      ...lectura.guias[0],
      cuerpo: 'Ver [[tercero/aceptar]], [[tercero/falta|Falta]], [guía](guia:otra#unica), [API](api:vinculos), [archivo](repo:docs/integracion.md) y `[[no/toca]]`.\n\n```\n[[tampoco/toca]]\n```\n\n![d](diagramas/d.svg)',
    };
    const md = markdownDeGuia(guia, {
      raizDelRepositorio: '/repo',
      carpetaGuias: '/repo/docs/manual/guias',
      carpetaDocs: '/repo/docs/manual',
      existeRecorrido: (id) => id === 'tercero/aceptar',
      tituloDe: () => 'Acepta la invitación',
    });
    expect(md).toContain('[Acepta la invitación](../tercero/aceptar.md)');
    expect(md).toContain('*Falta (recorrido pendiente)*');
    expect(md).toContain('[guía](otra.md#unica)');
    expect(md).toContain('[API](../api/vinculos.md)');
    expect(md).toContain('[archivo](../../integracion.md)');
    expect(md).toContain('`[[no/toca]]`');
    expect(md).toContain('[[tampoco/toca]]');
    expect(md).toMatch(/^---\ntitulo: "Una guía"\nseccion: "Arquitectura"/);
  });

  it('archivosDeGuias entrega el índice, las guías y los diagramas', () => {
    const lectura2 = { ...lectura, diagramas: new Map([['d.svg', '<svg xmlns="http://www.w3.org/2000/svg" class="diagrama"></svg>']]) };
    const archivos = archivosDeGuias(lectura2, {
      raizDelRepositorio: '/repo',
      carpetaGuias: '/repo/docs/manual/guias',
      carpetaDocs: '/repo/docs/manual',
      existeRecorrido: () => false,
      tituloDe: () => '',
    });
    expect(archivos.map((a) => a.ruta)).toEqual(['README.md', 'una.md', 'otra.md', 'diagramas/d.svg']);
    expect(archivos[3].texto).toContain('<style>');
  });

  it('resolutorDeRecorridosEscritos usa la corrida y lo ya escrito en docs', () => {
    const r = resolutorDeRecorridosEscritos(recorridos, '/no/existe');
    expect(r.existeRecorrido('tercero/aceptar')).toBe(true);
    expect(r.existeRecorrido('tercero/otro')).toBe(false);
    expect(r.tituloDe('tercero/aceptar')).toBe('Acepta la invitación');
  });
});

describe('validarGuias', () => {
  const lectura = {
    guias: [
      { slug: 'a', titulo: 'A', seccion: 'S', orden: 1, resumen: 'x', cuerpo: '## Uno\n\n[[tercero/existe]] [[tercero/no-esta]] [x](guia:b#nada) [y](guia:c) [z](api:inexistente) ![d](diagramas/falta.svg)' },
      { slug: 'b', titulo: 'B', seccion: 'S', orden: 2, resumen: 'x', cuerpo: '## Otro' },
    ],
    diagramas: new Map([['sobra.svg', '<svg/>']]),
  };

  it('detecta recorridos fuera del inventario, enlaces, módulos y diagramas rotos y diagramas sin usar', () => {
    const problemas = validarGuias(lectura, { inventario: new Set(['tercero/existe']), modulosApi: new Set(['vinculos']) });
    expect(problemas.join('\n')).toContain('tercero/no-esta no está en INVENTARIO.md');
    expect(problemas.join('\n')).toContain('la guía b no tiene la sección #nada');
    expect(problemas.join('\n')).toContain('la guía c no existe');
    expect(problemas.join('\n')).toContain('el módulo de la API inexistente no existe');
    expect(problemas.join('\n')).toContain('falta el diagrama diagramas/falta.svg');
    expect(problemas.join('\n')).toContain('el diagrama sobra.svg no lo usa ninguna guía');
    expect(problemas).toHaveLength(6);
  });
});

describe('las guías del manual', () => {
  const lectura = leerGuias(CARPETA_GUIAS);
  const modulosApi = new Set([...readdirSync(DOCS_API).filter((n) => n.endsWith('.md') && n !== 'README.md').map((n) => n.slice(0, -3))]);

  it('se leen todas, sin avisos y con las cuatro secciones pedidas', () => {
    expect(lectura.avisos).toEqual([]);
    expect(lectura.guias.length).toBeGreaterThanOrEqual(14);
    expect(lectura.secciones.map((s) => s.nombre)).toEqual(['Arquitectura', 'Flujo de punta a punta', 'Configuración y glosario', 'Integración']);
  });

  it('cada recorrido que citan está en el inventario y cada enlace, módulo de la API y diagrama existe', () => {
    const inventario = leerInventario(INVENTARIO);
    expect(inventario.size).toBeGreaterThan(150);
    expect(validarGuias(lectura, { inventario, modulosApi })).toEqual([]);
  });

  it('el flujo de punta a punta enlaza recorridos de todos los dominios', () => {
    const flujo = lectura.guias.find((g) => g.slug === 'flujo-del-vinculo-a-la-validacion');
    const dominios = new Set([...flujo.cuerpo.matchAll(/\[\[([a-z]+)\//g)].map((m) => m[1]));
    for (const d of ['tercero', 'revision', 'vinculos', 'configuracion', 'cumplimiento', 'documentos', 'integracion', 'publico', 'operacion', 'ia', 'informes', 'plataforma']) expect(dominios.has(d)).toBe(true);
  });

  it('se renderizan todas sin alertas y con al menos una sección', () => {
    const sitio = construirSitioDeGuias(lectura, { recorridos: [], generado: 'hoy' });
    expect(sitio.alertas).toEqual([]);
    for (const g of lectura.guias) expect(bloquesDeMarkdown(g.cuerpo).filter((b) => b.tipo === 'titulo' && b.nivel === 2).length).toBeGreaterThanOrEqual(2);
  });

  it('incluye los diagramas pedidos: contexto, contenedores y los ciclos de documento, vínculo, expediente y certificado', () => {
    for (const nombre of ['contexto', 'contenedores', 'ciclo-documento', 'ciclo-vinculo', 'ciclo-expediente', 'ciclo-certificado', 'flujo-a', 'flujo-b', 'tenencia', 'mensajeria'])
      expect(lectura.diagramas.has(`${nombre}.svg`)).toBe(true);
  });

  it('los diagramas son SVG bien formados, accesibles y sin colores fijos (se leen en claro y oscuro)', () => {
    for (const [nombre, svg] of lectura.diagramas) {
      expect(svg.trimStart().startsWith('<svg'), nombre).toBe(true);
      expect(svg.trimEnd().endsWith('</svg>'), nombre).toBe(true);
      expect(svg, nombre).toContain('class="diagrama"');
      expect(svg, nombre).toContain('role="img"');
      expect(svg, nombre).toContain('viewBox=');
      expect(svg, nombre).toMatch(/<title id="titulo">[^<]+<\/title>/);
      expect(svg, nombre).not.toMatch(/(fill|stroke)="#/);
      expect(svg, nombre).not.toMatch(/style="[^"]*#[0-9a-f]{3,6}/i);
      const abiertas = (svg.match(/<(rect|text|path|circle|marker|defs|title|desc)\b/g) ?? []).length;
      const cerradas = (svg.match(/<\/(rect|text|path|circle|marker|defs|title|desc)>|\/>/g) ?? []).length;
      expect(cerradas, nombre).toBeGreaterThanOrEqual(abiertas);
    }
  });

  it('el Markdown de cada guía y cada diagrama está publicado en docs/manual/guias', () => {
    // No se compara el texto: los enlaces a recorridos dependen de los que ya estén escritos en docs/manual y cambian cuando otra rama agrega uno.
    const carpetaDocs = join(aqui, '..', '..', '..', 'docs', 'manual', 'guias');
    for (const g of lectura.guias) expect(existsSync(join(carpetaDocs, `${g.slug}.md`)), `falta docs/manual/guias/${g.slug}.md: corre «pnpm manual»`).toBe(true);
    for (const nombre of lectura.diagramas.keys()) expect(existsSync(join(carpetaDocs, 'diagramas', nombre)), `falta el diagrama ${nombre}: corre «pnpm manual»`).toBe(true);
    expect(existsSync(join(carpetaDocs, 'README.md'))).toBe(true);
  });
});
