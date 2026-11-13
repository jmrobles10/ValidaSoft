// Generador de los recursos de marca de Validasoft (MA-1, docs/marca.md). Reproducible: pinta los componentes REALES de
// `@validasoft/ui` (Logo, AnimacionExito, Cargando) en un navegador (Playwright + Vite), captura los cuadros y escribe:
//
//   marca/svg/     logotipo e ícono en claro y oscuro, y el favicon (con `prefers-color-scheme`)
//   splash         la carga de marca que ven las dos apps mientras llega su JavaScript (entre las marcas de `index.html`)
//   marca/png/     favicon 16/32/48, ícono de la app 192/512 (y maskable), apple-touch-icon, avatar, logotipos @2x y la hoja de la marca
//   marca/gif/     éxito y carga, en claro y oscuro (2× para nitidez, con el fondo de cada modo)
//
// y los copia a donde el producto los usa: `apps/web/public`, `apps/public/public` (con su manifest) y los recursos incrustados
// de los correos (`src/Validasoft.Aplicacion/Comun/RecursosDeMarca`).
//
//   pnpm --filter @validasoft/marca generar            # todo
//   pnpm --filter @validasoft/marca generar -- --solo svg,png   # solo una parte (svg, png, gif, splash)
//
// Requiere Node 23.6 o superior (lee `gif.ts` y `png.ts` quitando los tipos) y Chromium de Playwright
// (`pnpm --filter @validasoft/e2e navegadores`). El resultado se versiona; `sincronia.test.ts` vigila que las copias no se separen.
import { mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import react from '@vitejs/plugin-react';
import { chromium } from '@playwright/test';
import { createServer } from 'vite';

if (!process.features.typescript) {
  console.error('Este generador necesita Node 23.6 o superior (importa gif.ts y png.ts sin compilar).');
  process.exit(2);
}
const { codificarGif } = await import('./gif.ts');
const { decodificarPng } = await import('./png.ts');

const aqui = import.meta.dirname;
const repo = resolve(aqui, '..');
const argumentos = process.argv.slice(2);
const indiceSolo = argumentos.indexOf('--solo');
const partes = new Set((indiceSolo >= 0 ? (argumentos[indiceSolo + 1] ?? '') : 'svg,png,gif,splash').split(','));

/** Fondos de cada modo (superficie de nivel 1 del design system) para los GIF y los íconos con fondo. */
const FONDO = { light: '#FFFFFF', dark: '#0B1120' };
const MODOS = [
  ['claro', 'light'],
  ['oscuro', 'dark'],
];

/** Escribe un archivo creando su carpeta. */
function escribir(ruta, contenido) {
  mkdirSync(dirname(ruta), { recursive: true });
  writeFileSync(ruta, contenido);
  console.log(`  ${ruta.replace(`${repo}\\`, '').replace(`${repo}/`, '').replaceAll('\\', '/')}`);
}

const servidor = await createServer({ root: join(aqui, 'harness'), configFile: false, plugins: [react()], server: { port: 5399 }, logLevel: 'error' });
await servidor.listen();
const origen = servidor.resolvedUrls?.local[0];
if (!origen) throw new Error('El servidor del arnés no arrancó.');
const navegador = await chromium.launch();

/** Abre una escena del arnés y espera a que la tipografía y el dibujo estén asentados. */
async function abrir(parametros, { escala = 1 } = {}) {
  const contexto = await navegador.newContext({ viewport: { width: 1900, height: 900 }, deviceScaleFactor: escala });
  const pagina = await contexto.newPage();
  pagina.on('pageerror', (error) => console.error('Error en el arnés:', error.message));
  await pagina.goto(`${origen}?${new URLSearchParams(parametros).toString()}`);
  await pagina.waitForFunction('window.marcaLista === true');
  return { pagina, contexto };
}

/** PNG de la escena (solo el elemento #escena), con fondo transparente si la escena no lo pone. */
async function png(parametros, escala = 1) {
  const { pagina, contexto } = await abrir(parametros, { escala });
  const imagen = await pagina.locator('#escena').screenshot({ omitBackground: true });
  await contexto.close();
  return imagen;
}

const { pagina: paginaMarca, contexto: contextoMarca } = await abrir({ escena: 'logotipo', tamano: '100' });
const marca = await paginaMarca.evaluate('window.marca');
const medidasLogotipo = await paginaMarca.evaluate(() => {
  const v = document.querySelector('[data-parte="v"]')?.getBoundingClientRect();
  const texto = document.querySelector('[data-parte="texto"]')?.getBoundingClientRect();
  const raiz = document.querySelector('#escena [role="img"]')?.getBoundingClientRect();
  if (!v || !texto || !raiz) throw new Error('Faltan las partes del logotipo.');
  return { textoX: texto.left - raiz.left, anchoTexto: texto.width, ancho: raiz.width, alto: v.height };
});
await contextoMarca.close();

const FUENTE_SVG = "Geist, 'Geist Variable', 'Segoe UI', system-ui, sans-serif";
const caja = marca.cajaIcono;

/** SVG del logotipo (sin aro): la «V» y el texto con la tipografía de marca (Geist; sin ella, la fuente del sistema). */
function svgLogotipo(c) {
  const { ancho, alto, textoX, anchoTexto } = medidasLogotipo;
  const anchoV = (alto * marca.cajaV.ancho) / marca.cajaV.alto;
  const escala = alto / marca.cajaV.alto;
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 ${redondear(ancho)} ${redondear(alto)}" width="${redondear(ancho)}" height="${redondear(alto)}" role="img" aria-label="Validasoft">
  <title>Validasoft</title>
  <g transform="scale(${redondear(escala)}) translate(${-marca.cajaV.x} ${-marca.cajaV.y})"><path d="${marca.checkRelleno}" fill="${c.check}"/></g>
  <text x="${redondear(textoX)}" y="${redondear(alto)}" font-family="${FUENTE_SVG}" font-size="${redondear(alto)}" letter-spacing="-0.015em" textLength="${redondear(anchoTexto)}" lengthAdjust="spacing"><tspan font-weight="500" fill="${c.texto}">${marca.textoLogotipo.cuerpo}</tspan><tspan font-weight="400" fill="${c.soft}">${marca.textoLogotipo.marca}</tspan></text>
  <!-- anchoV ${redondear(anchoV)}: la «V» va pegada a la «a» (margen ${marca.margenV} del tamaño de letra) -->
</svg>
`;
}

/** SVG del ícono (aro abierto y check). */
function svgIcono(c) {
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="${caja.x} ${caja.y} ${caja.ancho} ${caja.alto}" width="${caja.ancho}" height="${caja.alto}" role="img" aria-label="Validasoft">
  <title>Validasoft</title>
  <path d="${marca.aroAbierto}" fill="none" stroke="${c.aro}" stroke-width="${marca.aro.grosor}" stroke-linecap="round"/>
  <path d="${marca.checkRelleno}" fill="${c.check}" transform="${marca.transformacionCheckIcono}"/>
</svg>
`;
}

/** SVG del favicon: círculo verde con el check blanco; en oscuro el verde de ese modo (`prefers-color-scheme`). */
function svgFavicon() {
  const { light, dark } = marca.colores;
  const f = marca.favicon;
  return `<svg xmlns="http://www.w3.org/2000/svg" viewBox="0 0 100 100" width="100" height="100">
  <title>Validasoft</title>
  <style>
    .disco { fill: ${light.check}; }
    .check { fill: ${light.blanco}; stroke: ${light.blanco}; stroke-width: ${f.contorno}; stroke-linejoin: round; }
    @media (prefers-color-scheme: dark) { .disco { fill: ${dark.check}; } }
  </style>
  <circle class="disco" cx="50" cy="50" r="${f.radio}"/>
  <path class="check" d="${marca.checkRelleno}" transform="${f.transformacion}"/>
</svg>
`;
}

const redondear = (valor) => Math.round(valor * 100) / 100;

if (partes.has('svg')) {
  console.log('SVG');
  for (const [nombre, modo] of MODOS) {
    escribir(join(aqui, 'svg', `logotipo-${nombre}.svg`), svgLogotipo(marca.colores[modo]));
    escribir(join(aqui, 'svg', `icono-${nombre}.svg`), svgIcono(marca.colores[modo]));
  }
  escribir(join(aqui, 'svg', 'favicon.svg'), svgFavicon());
}

/** Manifest de una app instalable (PWA): íconos con y sin máscara. */
function manifest({ nombre, corto, descripcion }) {
  return `${JSON.stringify(
    {
      name: nombre,
      short_name: corto,
      description: descripcion,
      lang: 'es-CO',
      start_url: './',
      scope: './',
      display: 'standalone',
      background_color: '#FAFBFD',
      theme_color: '#FFFFFF',
      icons: [
        { src: 'icon-192.png', sizes: '192x192', type: 'image/png', purpose: 'any' },
        { src: 'icon-512.png', sizes: '512x512', type: 'image/png', purpose: 'any' },
        { src: 'icon-maskable-512.png', sizes: '512x512', type: 'image/png', purpose: 'maskable' },
        { src: 'favicon.svg', sizes: 'any', type: 'image/svg+xml', purpose: 'any' },
      ],
    },
    null,
    2,
  )}\n`;
}

if (partes.has('png')) {
  console.log('PNG');
  const iconos = {};
  for (const tamano of [16, 32, 48]) {
    iconos[`favicon-${tamano}.png`] = await png({ escena: 'favicon', modo: 'light', tamano: String(tamano) });
  }
  // Íconos de la app: el aro abierto y el check, en los colores del modo oscuro, sobre el fondo oscuro de marca.
  const app = (tamano, proporcion, radio) => png({ escena: 'app', modo: 'dark', tamano: String(tamano), fondo: FONDO.dark, proporcion: String(proporcion), radio: String(radio) });
  iconos['apple-touch-icon.png'] = await app(180, 0.62, 0);
  iconos['icon-192.png'] = await app(192, 0.62, 0.22);
  iconos['icon-512.png'] = await app(512, 0.62, 0.22);
  // Maskable: el sistema recorta hasta un 20 % por lado; el ícono queda dentro del círculo de seguridad (80 %).
  iconos['icon-maskable-512.png'] = await app(512, 0.5, 0);
  iconos['avatar-512.png'] = await app(512, 0.62, 0);
  for (const [nombre, contenido] of Object.entries(iconos)) escribir(join(aqui, 'png', nombre), contenido);
  // Hoja de contacto de la marca (docs/marca.md): todas las piezas en claro y oscuro; la carga se congela en un cuadro fijo para que sea reproducible.
  const hoja = await abrir({ escena: 'hoja' });
  await hoja.pagina.evaluate(() => window.fijarInstante(600));
  escribir(join(aqui, 'png', 'hoja-de-marca.png'), await hoja.pagina.locator('#escena').screenshot());
  await hoja.contexto.close();
  for (const [nombre, modo] of MODOS) {
    escribir(join(aqui, 'png', `logotipo-${nombre}@2x.png`), await png({ escena: 'logotipo', modo, tamano: '32' }, 2));
  }

  const apps = [
    { carpeta: 'apps/web/public', nombre: 'Validasoft', corto: 'Validasoft', descripcion: 'Validación documental de terceros para las personas de la empresa.' },
    { carpeta: 'apps/public/public', nombre: 'Validasoft · Mis documentos', corto: 'Validasoft', descripcion: 'Tu información y tus documentos, una sola vez para todas tus empresas.' },
  ];
  const comunes = ['favicon-16.png', 'favicon-32.png', 'favicon-48.png', 'apple-touch-icon.png', 'icon-192.png', 'icon-512.png', 'icon-maskable-512.png'];
  console.log('Apps');
  for (const app of apps) {
    for (const nombre of comunes) escribir(join(repo, app.carpeta, nombre), iconos[nombre]);
    escribir(join(repo, app.carpeta, 'manifest.webmanifest'), manifest(app));
  }
}

/**
 * La carga de marca para `index.html`: el mismo dibujo y los mismos tiempos de `Cargando`, en HTML y CSS puros, para que se vea desde el
 * primer cuadro, antes de que llegue el JavaScript (React la reemplaza al montarse). `oscuro`: sigue el modo del sistema (la app del
 * tercero es solo clara).
 */
function splash({ oscuro }) {
  const { light, dark } = marca.colores;
  const t = marca.tiemposCarga;
  const c = marca.cajaIcono;
  const arco = marca.arcoDeCarga;
  const largoGuia = marca.longitudGuia;
  const modoOscuro = oscuro
    ? `
      @media (prefers-color-scheme: dark) { .vs-splash { background: #060A14; } .vs-aro { stroke: ${dark.aro}; } .vs-check { fill: ${dark.check}; } }`
    : '';
  return `
    <style>
      .vs-splash { position: fixed; inset: 0; display: grid; place-items: center; background: #FAFBFD; }
      .vs-splash svg { width: 56px; height: 56px; display: block; }
      .vs-aro { stroke: ${light.aro}; }
      .vs-check { fill: ${light.check}; }
      .vs-giro { transform-origin: 50px 50px; animation: vs-girar ${t.vuelta}ms linear infinite; }
      .vs-guia { stroke-dasharray: ${largoGuia} ${largoGuia}; stroke-dashoffset: ${largoGuia}; animation: vs-trazar ${t.check}ms ease-in-out infinite; }
      @keyframes vs-girar { to { transform: rotate(360deg); } }
      @keyframes vs-trazar { 0% { stroke-dashoffset: ${largoGuia}; } 45%, 60% { stroke-dashoffset: 0; } 100% { stroke-dashoffset: -${largoGuia}; } }
      @keyframes vs-pulso { 50% { opacity: 0.55; } }${modoOscuro}
      @media (prefers-reduced-motion: reduce) { .vs-splash svg { animation: vs-pulso 2s ease-in-out infinite; } .vs-giro { animation: none; } .vs-guia { animation: none; stroke-dashoffset: 0; } }
    </style>
    <div class="vs-splash" role="status" aria-label="Cargando Validasoft">
      <svg viewBox="${c.x} ${c.y} ${c.ancho} ${c.alto}" aria-hidden="true" focusable="false">
        <defs><mask id="vs-mascara" maskUnits="userSpaceOnUse" x="-20" y="-20" width="140" height="140"><path class="vs-guia" d="${marca.guiaCheck}" fill="none" stroke="#FFFFFF" stroke-width="${marca.grosorGuia}" stroke-linecap="round" stroke-linejoin="round"/></mask></defs>
        <circle class="vs-aro" cx="${marca.aro.cx}" cy="${marca.aro.cy}" r="${marca.aro.r}" fill="none" stroke-width="${marca.aro.grosor}" opacity="0.2"/>
        <circle class="vs-aro vs-giro" cx="${marca.aro.cx}" cy="${marca.aro.cy}" r="${marca.aro.r}" fill="none" stroke-width="${marca.aro.grosor}" stroke-linecap="round" stroke-dasharray="${arco.visible} ${arco.hueco}"/>
        <g transform="${marca.transformacionCheckIcono}"><path class="vs-check" d="${marca.checkRelleno}" mask="url(#vs-mascara)"/></g>
      </svg>
    </div>
    `;
}

if (partes.has('splash')) {
  console.log('Splash');
  for (const [carpeta, oscuro] of [['apps/web', true], ['apps/public', false]]) {
    const ruta = join(repo, carpeta, 'index.html');
    const original = readFileSync(ruta, 'utf8');
    const patron = /<!-- marca:splash:inicio -->[\s\S]*?<!-- marca:splash:fin -->/;
    if (!patron.test(original)) throw new Error(`Faltan las marcas de la carga de marca en ${ruta}.`);
    escribir(ruta, original.replace(patron, () => `<!-- marca:splash:inicio -->${splash({ oscuro })}<!-- marca:splash:fin -->`));
  }
}

if (partes.has('svg')) {
  const favicon = svgFavicon();
  for (const carpeta of ['apps/web/public', 'apps/public/public']) escribir(join(repo, carpeta, 'favicon.svg'), favicon);
}

/** Cuadros de una animación: la escena se congela en cada instante y se captura (2× para nitidez). */
async function cuadros(parametros, instantes, cicloDeCarga) {
  const { pagina, contexto } = await abrir(parametros, { escala: 2 });
  const resultado = [];
  for (const instante of instantes) {
    await pagina.evaluate(([ms, ciclo]) => window.fijarInstante(ms, ciclo), [instante, cicloDeCarga]);
    resultado.push(decodificarPng(await pagina.locator('#escena').screenshot()));
  }
  await contexto.close();
  return resultado;
}

if (partes.has('gif')) {
  console.log('GIF');
  const PASO = 40;
  const t = marca.tiemposExito;
  const duracionExito = t.aro + t.relleno + t.check + t.pop;
  for (const [nombre, modo] of MODOS) {
    const fondo = FONDO[modo];
    // Éxito: una vez de principio a fin y se queda a la vista un momento antes de repetirse.
    const instantesExito = [];
    for (let ms = 0; ms <= duracionExito; ms += PASO) instantesExito.push(ms);
    instantesExito.push(duracionExito);
    const exito = (await cuadros({ escena: 'exito', modo, tamano: '96', fondo, margen: '8' }, instantesExito)).map((imagen, i, todos) => ({
      imagen,
      retardoMs: i === 0 ? 300 : i === todos.length - 1 ? 1800 : PASO,
    }));
    escribir(join(aqui, 'gif', `exito-${nombre}.gif`), codificarGif(exito));

    // Carga: el arco da una vuelta por segundo y el check se revela y se oculta; el GIF necesita un ciclo común (2 s: dos vueltas y un ciclo
    // del check más lento que los 1,4 s de la app) para repetirse sin saltos.
    const ciclo = 2000;
    const instantesCarga = [];
    for (let ms = 0; ms < ciclo; ms += PASO) instantesCarga.push(ms);
    const carga = (await cuadros({ escena: 'carga', modo, tamano: '96', fondo, margen: '8' }, instantesCarga, ciclo)).map((imagen) => ({ imagen, retardoMs: PASO }));
    escribir(join(aqui, 'gif', `cargando-${nombre}.gif`), codificarGif(carga));
  }
}

if (partes.has('png') || partes.has('gif')) {
  // Recursos incrustados en la API para los correos (se adjuntan en línea con `cid:`; ver Comun/MarcoDeCorreo.cs).
  const correo = join(repo, 'src', 'Validasoft.Aplicacion', 'Comun', 'RecursosDeMarca');
  console.log('Correos');
  if (partes.has('png')) escribir(join(correo, 'logotipo.png'), readFileSync(join(aqui, 'png', 'logotipo-claro@2x.png')));
  if (partes.has('gif')) escribir(join(correo, 'exito.gif'), readFileSync(join(aqui, 'gif', 'exito-claro.gif')));
}

await navegador.close();
await servidor.close();
console.log('Listo.');
