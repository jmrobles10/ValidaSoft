import { mkdirSync, readFileSync, rmSync, writeFileSync } from 'node:fs';
import { basename, dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';
import { test, type Page, type PlaywrightTestArgs, type PlaywrightWorkerArgs } from '@playwright/test';
import { capturar, CELULAR, ESCRITORIO, rutaDeLaApp } from './captura';
import type { ManifiestoDeRecorrido, MetaDeRecorrido, OpcionesDePaso, PasoDelManifiesto } from './tipos';
import { aSlug, normalizarConfiguracion, normalizarVariantes, validarMeta, validarPaso, type Catalogo } from './validacion';

/**
 * Recorridos del manual (e2e/manual/README.md): una prueba de Playwright que ejecuta un flujo real en la interfaz y, en cada paso,
 * llama a `paso(page, {…})`, que toma el pantallazo y lo anota en el manifiesto del recorrido.
 *
 * Salida (se genera de nuevo en cada corrida, gitignorada): `salida/manual-recorridos/<dominio>/<archivo>/` con `manifiesto.json` y los
 * PNG. El manifiesto solo se escribe si el recorrido termina bien: un recorrido que falla no deja un manual a medias.
 */

const RAIZ = fileURLToPath(new URL('../../../', import.meta.url));
const CARPETA_DE_SALIDA = process.env.MANUAL_RECORRIDOS ?? join(RAIZ, 'salida', 'manual-recorridos');
const CATALOGO = JSON.parse(readFileSync(fileURLToPath(new URL('../catalogo.json', import.meta.url)), 'utf8')) as Catalogo;
const ACTORES_CON_CELULAR = new Set(['Tercero', 'Público']);

interface EstadoDeRecorrido {
  meta: MetaDeRecorrido;
  id: string;
  archivo: string;
  carpeta: string;
  pasos: PasoDelManifiesto[];
}

/** Estado de cada recorrido en ejecución (una prueba por vez en cada proceso de trabajo, pero se indexa por prueba para no depender de ello). */
const estados = new Map<string, EstadoDeRecorrido>();
/** Archivo → título del recorrido que ya corrió en esta repetición: un archivo declara un solo recorrido. */
const archivosVistos = new Map<string, string>();

/** Lo que recibe el cuerpo de un recorrido: las mismas fixtures de una prueba de Playwright que se usan al escribir recorridos. */
export type FixturesDeRecorrido = Pick<PlaywrightTestArgs & PlaywrightWorkerArgs, 'page' | 'browser' | 'context' | 'request'>;

/**
 * Archivo `*.manual.ts` que llamó a `recorrido()`. Playwright registra la prueba con la ubicación de este módulo (donde se llama a `test`),
 * así que el archivo real se toma de la pila de llamadas.
 */
function archivoDelLlamador(): string | null {
  for (const linea of (new Error().stack ?? '').split('\n')) {
    const encontrado = /((?:file:\/\/\/)?[^\s()]+?\.manual\.ts)(?=:\d+)/.exec(linea)?.[1];
    if (encontrado) return encontrado.startsWith('file:') ? fileURLToPath(encontrado) : encontrado;
  }
  return null;
}

/**
 * Declara el recorrido de este archivo (`e2e/manual/<dominio>/<archivo>.manual.ts`: un archivo, un recorrido). `cuerpo` recibe las mismas
 * fixtures que una prueba de Playwright (`page`, `browser`…) y llama a {@link paso} en cada momento que merece un pantallazo.
 */
export function recorrido(meta: MetaDeRecorrido, cuerpo: (fixtures: FixturesDeRecorrido) => Promise<void>): void {
  const archivoDeclarado = archivoDelLlamador();
  test(`[${meta.dominio}] ${meta.titulo}`, async ({ page, browser, context, request }) => {
    const info = test.info();
    const ruta = (archivoDeclarado ?? info.file).replace(/\\/g, '/');
    const archivo = basename(ruta).replace(/\.manual\.ts$/, '');
    const carpetaDelArchivo = basename(dirname(ruta));
    const errores = validarMeta(meta, CATALOGO, carpetaDelArchivo, archivo);
    if (!ruta.endsWith('.manual.ts')) errores.push('El archivo del recorrido debe llamarse <flujo>.manual.ts.');
    const clave = `${ruta}|${String(info.repeatEachIndex)}|${String(info.retry)}`;
    const previo = archivosVistos.get(clave);
    if (previo !== undefined && previo !== meta.titulo) errores.push(`El archivo ya declara el recorrido «${previo}»: un archivo, un recorrido.`);
    archivosVistos.set(clave, meta.titulo);
    if (errores.length > 0) throw new Error(`El recorrido está mal declarado:\n - ${errores.join('\n - ')}`);

    // Con `--repeat-each` las repeticiones corren a la vez: solo la primera deja el manifiesto y las imágenes que lee el generador; las demás van a
    // una carpeta aparte (que el generador no mira) para no pisarse.
    const base = info.repeatEachIndex > 0 ? join(CARPETA_DE_SALIDA, '..', 'manual-repeticiones', String(info.repeatEachIndex)) : CARPETA_DE_SALIDA;
    const carpeta = join(base, meta.dominio, archivo);
    rmSync(carpeta, { recursive: true, force: true });
    mkdirSync(carpeta, { recursive: true });
    const estado: EstadoDeRecorrido = { meta, id: `${meta.dominio}/${archivo}`, archivo, carpeta, pasos: [] };
    estados.set(info.testId, estado);
    try {
      await cuerpo({ page, browser, context, request });
    } finally {
      estados.delete(info.testId);
    }
    if (estado.pasos.length === 0) throw new Error('El recorrido no llamó a paso(): no hay nada que documentar.');
    const manifiesto: ManifiestoDeRecorrido = {
      version: 1,
      id: estado.id,
      dominio: meta.dominio,
      archivo,
      titulo: meta.titulo,
      actor: meta.actor,
      resumen: meta.resumen,
      precondiciones: meta.precondiciones ?? [],
      relacionados: meta.relacionados ?? [],
      generado: new Date().toISOString(),
      pasos: estado.pasos,
    };
    writeFileSync(join(carpeta, 'manifiesto.json'), `${JSON.stringify(manifiesto, null, 2)}\n`);
  });
}

/**
 * Un paso del recorrido: toma el pantallazo de `page` (claro 1280 × 800 y, si corresponde, celular 390 × 844), resalta lo indicado y
 * agrega el paso al manifiesto. Va DESPUÉS de lo que deja la pantalla como se quiere mostrar (la acción hecha y su resultado visible).
 */
export async function paso(page: Page, opciones: OpcionesDePaso): Promise<void> {
  const estado = estados.get(test.info().testId);
  if (!estado) throw new Error('paso() solo puede llamarse dentro de un recorrido(...).');
  const orden = estado.pasos.length + 1;
  const errores = validarPaso(opciones, orden);
  if (errores.length > 0) throw new Error(`El paso está mal escrito:\n - ${errores.join('\n - ')}`);

  await test.step(`Paso ${String(orden)}: ${opciones.titulo}`, async () => {
    const numero = String(orden).padStart(2, '0');
    const base = `${numero}-${aSlug(opciones.titulo)}`;
    const completa = opciones.completa ?? false;
    const resaltar = opciones.resaltar === undefined ? [] : Array.isArray(opciones.resaltar) ? opciones.resaltar : [opciones.resaltar];
    const comun = { completa, resaltar, esperarCarga: opciones.esperarCarga ?? true, avisos: opciones.avisos ?? false, mantenerPuntero: opciones.mantenerPuntero ?? false };
    const ruta = await rutaDeLaApp(page);

    const claro = await capturar(page, join(estado.carpeta, `${base}-claro.png`), { ...comun, ...ESCRITORIO, resaltadoOpcional: false });
    const conCelular = opciones.celular ?? ACTORES_CON_CELULAR.has(estado.meta.actor);
    if (conCelular) await capturar(page, join(estado.carpeta, `${base}-celular.png`), { ...comun, ...CELULAR, resaltadoOpcional: true });

    estado.pasos.push({
      orden,
      titulo: opciones.titulo.trim(),
      explicacion: opciones.explicacion.trim(),
      variantes: normalizarVariantes(opciones.variantes),
      configuracion: normalizarConfiguracion(opciones.configuracion),
      ruta,
      imagenes: { claro: `${base}-claro.png`, celular: conCelular ? `${base}-celular.png` : null },
      resaltados: claro.resaltados,
      completa,
    });
  });
}
