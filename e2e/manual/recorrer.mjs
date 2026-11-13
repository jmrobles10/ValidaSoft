// Corre recorridos del manual:  pnpm manual:recorrer [<recorrido>…] [opciones de Playwright]
//
// Envuelve `playwright test -c playwright.manual.config.ts` con una diferencia: lo que se escribe sin guion se entiende como NOMBRE EXACTO de recorrido, no como
// una parte de la ruta (Playwright filtra por coincidencia parcial y `registrar-consulta` corría también otro recorrido de otro dominio).
//
//   pnpm manual:recorrer tercero/aceptar-invitacion-persona-juridica   el recorrido de ese archivo (`dominio/archivo`, con o sin `.manual.ts`)
//   pnpm manual:recorrer aceptar-invitacion-persona-juridica           ese nombre de archivo en cualquier dominio (si se repite en varios, corre todos)
//   pnpm manual:recorrer tercero                                       todos los de un dominio (también `tercero/` o `tercero/*`)
//   pnpm manual:recorrer --grep "\[tercero\]"                          los demás filtros de Playwright siguen igual (--grep, --workers, --repeat-each…)
//
// Si el nombre no existe, avisa con los parecidos y no corre nada.
import { spawn } from 'node:child_process';
import { readdirSync } from 'node:fs';
import { createRequire } from 'node:module';
import { dirname, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const aqui = dirname(fileURLToPath(import.meta.url));
const carpetaE2e = join(aqui, '..');

/** Opciones de Playwright que llevan su valor en el argumento siguiente (`--grep x`): ese valor no es un nombre de recorrido. */
const CON_VALOR = new Set([
  '-c', '--config', '-g', '--grep', '--grep-invert', '--project', '-j', '--workers', '--repeat-each', '--retries', '--reporter', '--timeout', '--output',
  '--shard', '--max-failures', '--global-timeout', '--trace', '--tsconfig', '--ui-host', '--ui-port', '--tag',
]);

/** Todos los recorridos: `{ dominio, archivo }` por cada `manual/<dominio>/<archivo>.manual.ts`. */
function listarRecorridos(raiz = aqui) {
  const lista = [];
  for (const dominio of readdirSync(raiz, { withFileTypes: true })) {
    if (!dominio.isDirectory() || dominio.name === 'nucleo' || dominio.name === 'generador' || dominio.name === 'guias') continue;
    for (const archivo of readdirSync(join(raiz, dominio.name))) {
      if (archivo.endsWith('.manual.ts')) lista.push({ dominio: dominio.name, archivo: archivo.slice(0, -'.manual.ts'.length) });
    }
  }
  return lista;
}

/** Los recorridos que nombra `pedido`: `dominio/archivo`, `archivo`, `dominio`, `dominio/` o `dominio/*`. Vacío si no existe. */
function resolverNombre(pedido, recorridos) {
  const limpio = pedido
    .replaceAll('\\', '/')
    .replace(/^(\.\/)?(e2e\/)?(manual\/)?/, '')
    .replace(/\.manual\.ts$/, '')
    .replace(/\/\*$/, '/');
  if (limpio.endsWith('/')) return recorridos.filter((r) => r.dominio === limpio.slice(0, -1));
  if (limpio.includes('/')) {
    const [dominio, archivo] = limpio.split('/');
    return recorridos.filter((r) => r.dominio === dominio && r.archivo === archivo);
  }
  const porArchivo = recorridos.filter((r) => r.archivo === limpio);
  return porArchivo.length > 0 ? porArchivo : recorridos.filter((r) => r.dominio === limpio);
}

/** Expresión (como texto) que Playwright compara con la ruta absoluta del archivo: exacta, sin coincidir con otros nombres que la contengan. */
function filtroDeArchivo({ dominio, archivo }) {
  return String.raw`[\\/]${dominio}[\\/]${archivo}\.manual\.ts$`;
}

function main() {
  const entrada = process.argv.slice(2);
  const opciones = [];
  const nombres = [];
  for (let i = 0; i < entrada.length; i++) {
    const a = entrada[i];
    if (a.startsWith('-')) {
      opciones.push(a);
      if (!a.includes('=') && CON_VALOR.has(a) && i + 1 < entrada.length) opciones.push(entrada[++i]);
    } else nombres.push(a);
  }

  const recorridos = listarRecorridos();
  const filtros = [];
  for (const nombre of nombres) {
    const hallados = resolverNombre(nombre, recorridos);
    if (hallados.length === 0) {
      const texto = nombre.replace(/^.*\//, '').replace(/\.manual\.ts$/, '');
      const parecidos = recorridos.filter((r) => r.archivo.includes(texto) || r.dominio.includes(texto)).slice(0, 10);
      console.error(`No existe el recorrido «${nombre}». Usa «dominio/archivo» (por ejemplo ${recorridos[0] ? `${recorridos[0].dominio}/${recorridos[0].archivo}` : 'tercero/aceptar-invitacion-persona-juridica'}) o un dominio entero.`);
      if (parecidos.length > 0) console.error(`Parecidos:\n${parecidos.map((r) => `  ${r.dominio}/${r.archivo}`).join('\n')}`);
      process.exit(1);
    }
    if (hallados.length > 1 && !nombre.includes('/') && !recorridos.some((r) => r.dominio === nombre)) {
      console.error(`Aviso: «${nombre}» existe en ${String(hallados.length)} dominios (${hallados.map((r) => r.dominio).join(', ')}); se corren todos.`);
    }
    for (const r of hallados) filtros.push(filtroDeArchivo(r));
  }

  const cli = createRequire(import.meta.url).resolve('@playwright/test/cli');
  const hijo = spawn(process.execPath, [cli, 'test', '-c', 'playwright.manual.config.ts', ...opciones, ...filtros], { cwd: carpetaE2e, stdio: 'inherit' });
  hijo.on('exit', (codigo, senal) => process.exit(codigo ?? (senal ? 1 : 0)));
}

main();
