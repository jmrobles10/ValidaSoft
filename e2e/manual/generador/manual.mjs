// Generador del manual de Validasoft:  node e2e/manual/generador/manual.mjs  (o `pnpm manual`).
//
// Entrada:  salida/manual-recorridos/<dominio>/<archivo>/{manifiesto.json, *.png}   (lo deja `pnpm manual:recorrer`)
//           packages/api-client/openapi/v1.json y el código de src/Validasoft.Aplicacion (permisos y descripciones)
// Salida:   salida/manual/            sitio HTML estático (gitignorado): recorridos, API, buscador, imágenes
//           docs/manual/<dominio>/    Markdown por recorrido (texto; las imágenes viven en la salida)
//           docs/manual/api/          referencia de la API en Markdown
//
// Guías:    e2e/manual/guias/*.md (arquitectura, flujo de punta a punta, configuración, integración) → salida/manual/g/ y docs/manual/guias/
// Opciones: --solo-api · --sin-api · --recorridos <carpeta> · --salida <carpeta> · --docs <carpeta>
//           --png (deja los pantallazos en PNG, sin comprimir) · --calidad <1-100> (WebP; 62 por omisión)
import { copyFileSync, mkdirSync, readFileSync, rmSync, statSync, writeFileSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { fechaLarga, leerRecorridos } from './comun.mjs';
import { construirModeloApi, htmlDeApi, markdownDeApi } from './api.mjs';
import { leerFuentes } from './api-fuente.mjs';
import { archivosDeGuias, construirSitioDeGuias, cssDeDiagramas, leerGuias, leerInventario, resolutorDeRecorridosEscritos, validarGuias } from './guias.mjs';
import { abrirConversor } from './imagenes.mjs';
import { indiceDeDominio, indiceGeneral, markdownDeRecorrido, recorridosEscritos } from './markdown.mjs';
import { construirSitioDeRecorridos, crearContexto } from './sitio.mjs';

const aqui = dirname(fileURLToPath(import.meta.url));
const RAIZ = resolve(aqui, '..', '..', '..');

function opciones(argv) {
  const o = { soloApi: false, sinApi: false, png: false, calidad: 62 };
  for (let i = 0; i < argv.length; i++) {
    const a = argv[i];
    if (a === '--solo-api') o.soloApi = true;
    else if (a === '--sin-api') o.sinApi = true;
    else if (a === '--png') o.png = true;
    else if (a === '--calidad') o.calidad = Math.min(100, Math.max(1, Number(argv[++i])));
    else if (['--recorridos', '--salida', '--docs'].includes(a)) o[a.slice(2)] = resolve(argv[++i] ?? '');
    else throw new Error(`Opción desconocida: ${a}. Usa --solo-api, --sin-api, --png, --calidad <1-100>, --recorridos <carpeta>, --salida <carpeta> o --docs <carpeta>.`);
  }
  return o;
}

function escribir(ruta, contenido) {
  mkdirSync(dirname(ruta), { recursive: true });
  writeFileSync(ruta, contenido);
}

/** Contrato crudo y, si se puede, el transformado que usa el cliente (nombres de operación y respuestas reales). */
async function leerContrato() {
  const bruto = JSON.parse(readFileSync(join(RAIZ, 'packages', 'api-client', 'openapi', 'v1.json'), 'utf8'));
  let transformado = bruto;
  try {
    const { transformarOpenApi } = await import(pathToFileURL(join(RAIZ, 'packages', 'api-client', 'openapi', 'transformar.ts')).href);
    transformado = transformarOpenApi(bruto);
  } catch (error) {
    console.warn(`Aviso: no se pudo aplicar transformar.ts (${error instanceof Error ? error.message.split('\n')[0] : String(error)}); se usa el contrato tal como lo sirve la API.`);
  }
  return { bruto, transformado };
}

async function main() {
  const o = opciones(process.argv.slice(2));
  const catalogo = JSON.parse(readFileSync(join(aqui, '..', 'catalogo.json'), 'utf8'));
  const carpetaRecorridos = o.recorridos ?? process.env.MANUAL_RECORRIDOS ?? join(RAIZ, 'salida', 'manual-recorridos');
  const carpetaSitio = o.salida ?? join(RAIZ, 'salida', 'manual');
  const carpetaDocs = o.docs ?? join(RAIZ, 'docs', 'manual');
  const generado = fechaLarga(new Date());

  const { recorridos, avisos } = o.soloApi ? { recorridos: [], avisos: [] } : leerRecorridos(carpetaRecorridos, catalogo);
  for (const aviso of avisos) console.warn(`Aviso: ${aviso}`);

  let modeloApi = null;
  if (!o.sinApi) {
    const { bruto, transformado } = await leerContrato();
    modeloApi = construirModeloApi({ bruto, transformado, fuentes: leerFuentes(join(RAIZ, 'src', 'Validasoft.Aplicacion')) });
  }

  // ---- Guías (e2e/manual/guias): lo que no sale de un recorrido ----
  const lecturaDeGuias = leerGuias(join(aqui, '..', 'guias'));
  for (const aviso of lecturaDeGuias.avisos) console.warn(`Aviso: ${aviso}`);
  for (const problema of validarGuias(lecturaDeGuias, {
    inventario: leerInventario(join(aqui, '..', 'INVENTARIO.md')),
    modulosApi: modeloApi ? new Set([...modeloApi.modulos.map((m) => m.slug), 'convenciones']) : undefined,
    recorridos: new Set(recorridos.map((r) => r.id)),
  }))
    console.warn(`Aviso (guías): ${problema}`);

  // ---- Sitio HTML ----
  rmSync(carpetaSitio, { recursive: true, force: true });
  // Los pantallazos se comprimen a WebP con el Chromium de Playwright (el sitio completo cabe así en una publicación de 64 MB); `--png` los deja como salen.
  const conversor = o.png || o.soloApi || recorridos.length === 0 ? null : await abrirConversor(RAIZ, { calidad: o.calidad });
  const extensionDeImagen = conversor ? '.webp' : '';
  const sitio = construirSitioDeRecorridos(recorridos, catalogo, { generado, totalEndpoints: modeloApi?.total ?? 0, seccionesDeGuias: lecturaDeGuias.secciones, extensionDeImagen });
  const guiasHtml = construirSitioDeGuias(lecturaDeGuias, { recorridos, generado });
  for (const alerta of guiasHtml.alertas) console.warn(`Aviso (guías): ${alerta}`);
  const api = modeloApi ? htmlDeApi(modeloApi, generado) : { paginas: [], indice: [] };
  for (const p of [...sitio.paginas, ...guiasHtml.paginas, ...api.paginas]) escribir(join(carpetaSitio, p.ruta), p.html);
  let bytesDeImagenes = 0;
  const pendientes = [...sitio.imagenes];
  const trabajador = async () => {
    for (let i = pendientes.shift(); i; i = pendientes.shift()) {
      const destino = join(carpetaSitio, i.destino);
      mkdirSync(dirname(destino), { recursive: true });
      if (conversor && i.destino.endsWith('.webp')) bytesDeImagenes += await conversor.convertir(i.origen, destino);
      else {
        copyFileSync(i.origen, destino);
        bytesDeImagenes += statSync(destino).size;
      }
    }
  };
  await Promise.all(Array.from({ length: conversor ? 4 : 1 }, trabajador));
  await conversor?.cerrar();
  mkdirSync(join(carpetaSitio, 'assets'), { recursive: true });
  escribir(join(carpetaSitio, 'assets', 'manual.css'), `${readFileSync(join(aqui, 'estilos.css'), 'utf8')}
${cssDeDiagramas({ sitio: true })}
`);
  escribir(join(carpetaSitio, 'assets', 'manual.js'), readFileSync(join(aqui, 'cliente.js'), 'utf8'));
  escribir(join(carpetaSitio, 'assets', 'buscador.js'), `window.MANUAL_INDICE = ${JSON.stringify([...sitio.indice, ...guiasHtml.indice, ...api.indice])};\n`);

  // ---- Markdown de los recorridos (solo los que se recorrieron; los demás se conservan) ----
  const carpetaImagenes = join(carpetaSitio, 'img');
  if (!o.soloApi) {
    const ctx = crearContexto(recorridos, catalogo);
    for (const r of recorridos) {
      const texto = markdownDeRecorrido(r, { dominio: ctx.dominio(r.dominio), carpetaDocs, carpetaImagenes, porId: ctx.porId, extensionDeImagen });
      escribir(join(carpetaDocs, r.dominio, `${r.archivo}.md`), texto);
    }
    for (const d of catalogo.dominios) escribir(join(carpetaDocs, d.slug, 'README.md'), indiceDeDominio(d, recorridosEscritos(carpetaDocs, d.slug)));
    escribir(join(carpetaDocs, 'README.md'), indiceGeneral(catalogo, lecturaDeGuias.secciones));
  }

  // ---- Markdown de las guías (se reescribe completo: un archivo que ya no existe en la fuente no debe sobrevivir) ----
  rmSync(join(carpetaDocs, 'guias'), { recursive: true, force: true });
  for (const a of archivosDeGuias(lecturaDeGuias, {
    raizDelRepositorio: RAIZ,
    carpetaGuias: join(carpetaDocs, 'guias'),
    carpetaDocs,
    ...resolutorDeRecorridosEscritos(recorridos, carpetaDocs),
  }))
    escribir(join(carpetaDocs, 'guias', a.ruta), a.texto);

  // ---- Markdown de la API ----
  if (modeloApi) {
    rmSync(join(carpetaDocs, 'api'), { recursive: true, force: true });
    for (const a of markdownDeApi(modeloApi)) escribir(join(carpetaDocs, 'api', a.ruta), a.texto);
  }

  const pasos = recorridos.reduce((n, r) => n + r.pasos.length, 0);
  console.log(`Manual generado: ${String(recorridos.length)} recorridos (${String(pasos)} pasos), ${String(lecturaDeGuias.guias.length)} guías${modeloApi ? `, ${String(modeloApi.total)} operaciones de la API en ${String(modeloApi.modulos.length)} módulos` : ''}.`);
  console.log(`  Sitio HTML:  ${join(carpetaSitio, 'index.html')}`);
  if (sitio.imagenes.length > 0) console.log(`  Imágenes:    ${String(sitio.imagenes.length)} (${extensionDeImagen === '.webp' ? `WebP, calidad ${String(o.calidad)}` : 'PNG'}), ${(bytesDeImagenes / 1048576).toFixed(1)} MB`);
  console.log(`  Markdown:    ${carpetaDocs}`);
}

main().catch((error) => {
  console.error(error instanceof Error ? error.message : error);
  process.exit(1);
});
