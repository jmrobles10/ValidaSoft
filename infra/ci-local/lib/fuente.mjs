// Copia de trabajo para el contenedor: un tar con lo que Git versionaría (archivos seguidos más los nuevos sin ignorar), sin
// node_modules, bin, obj ni nada de .gitignore, y sin los archivos locales privados. Es lo que `actions/checkout` le daría al
// runner, más los cambios sin confirmar del directorio (así se puede probar un fallo antes de hacer commit).
// El tar se escribe aquí mismo (USTAR + PAX para rutas largas o con tildes): no depende del `tar` de la máquina, que en
// Windows puede ser el de Git Bash (GNU) o el del sistema (bsdtar).
import { execFile, spawn } from 'node:child_process';
import { createWriteStream } from 'node:fs';
import { lstat, readFile } from 'node:fs/promises';
import { join } from 'node:path';
import { once } from 'node:events';
import { promisify } from 'node:util';

const execFileP = promisify(execFile);

/** Nunca viajan al contenedor aunque Git no los ignore (privados de la máquina del usuario). */
const PRIVADOS = [/(^|\/)\.semilla\.local\.json$/, /(^|\/)\.git\//];
export const esPrivado = (ruta) => PRIVADOS.some((p) => p.test(ruta));

const octal = (valor, longitud) => valor.toString(8).padStart(longitud - 1, '0') + '\0';

function cabecera({ nombre, tamano, modo, mtime, tipo }) {
  const b = Buffer.alloc(512);
  b.write(nombre, 0, 100, 'utf8');
  b.write(octal(modo, 8), 100, 8, 'ascii');
  b.write(octal(0, 8), 108, 8, 'ascii');
  b.write(octal(0, 8), 116, 8, 'ascii');
  b.write(octal(tamano, 12), 124, 12, 'ascii');
  b.write(octal(mtime, 12), 136, 12, 'ascii');
  b.write('        ', 148, 8, 'ascii');
  b.write(tipo, 156, 1, 'ascii');
  b.write('ustar\0', 257, 6, 'ascii');
  b.write('00', 263, 2, 'ascii');
  let suma = 0;
  for (const byte of b) suma += byte;
  b.write(suma.toString(8).padStart(6, '0') + '\0 ', 148, 8, 'ascii');
  return b;
}

function registroPax(clave, valor) {
  const cuerpo = ` ${clave}=${valor}\n`;
  let longitud = Buffer.byteLength(cuerpo);
  longitud += String(longitud + String(longitud).length).length;
  return Buffer.from(`${String(longitud)}${cuerpo}`);
}

const relleno = (n) => Buffer.alloc((512 - (n % 512)) % 512);

/** Escribe un archivo en el tar (con cabecera PAX si la ruta no cabe en 100 bytes ASCII). */
async function escribirEntrada(salida, ruta, contenido, modo, mtime) {
  const enviar = async (parte) => {
    if (!salida.write(parte)) await once(salida, 'drain');
  };
  const simple = Buffer.byteLength(ruta) < 100 && /^[\x20-\x7e]*$/.test(ruta);
  if (!simple) {
    const pax = registroPax('path', ruta);
    await enviar(cabecera({ nombre: `PaxHeader/${String(pax.length)}`, tamano: pax.length, modo: 0o644, mtime, tipo: 'x' }));
    await enviar(pax);
    await enviar(relleno(pax.length));
  }
  await enviar(cabecera({ nombre: simple ? ruta : ruta.slice(0, 50).replace(/[^\x20-\x7e]/g, '_'), tamano: contenido.length, modo, mtime, tipo: '0' }));
  await enviar(contenido);
  await enviar(relleno(contenido.length));
}

/** Rutas versionadas y nuevas (sin ignorar) de un directorio Git, con su modo. */
export async function listarArchivos(directorio) {
  const git = (args) => execFileP('git', ['-C', directorio, ...args], { encoding: 'buffer', maxBuffer: 256 * 1024 * 1024 }).then((r) => r.stdout.toString('utf8'));
  const seguidos = (await git(['ls-files', '-z', '-s'])).split('\0').filter(Boolean);
  const nuevos = (await git(['ls-files', '-z', '-o', '--exclude-standard'])).split('\0').filter(Boolean);
  const mapa = new Map();
  for (const linea of seguidos) {
    const tab = linea.indexOf('\t');
    const modo = linea.slice(0, linea.indexOf(' '));
    if (modo !== '100644' && modo !== '100755') continue; // enlaces simbólicos y submódulos no viajan
    mapa.set(linea.slice(tab + 1), modo === '100755' ? 0o755 : 0o644);
  }
  for (const ruta of nuevos) mapa.set(ruta, 0o644);
  return [...mapa].filter(([ruta]) => !esPrivado(ruta));
}

/** Escribe `destino` (un .tar) con el contenido de trabajo del directorio. Devuelve cuántos archivos y bytes. */
export async function tarDeDirectorio(directorio, destino) {
  const archivos = await listarArchivos(directorio);
  const salida = createWriteStream(destino);
  let cuantos = 0;
  let bytes = 0;
  // Se leen varios archivos a la vez (en Windows cada lectura pesa más que escribirla) pero se escriben en orden.
  const leerArchivo = async ([ruta, modo]) => {
    try {
      const completa = join(directorio, ruta);
      const info = await lstat(completa);
      if (!info.isFile()) return null;
      return { ruta, modo, contenido: await readFile(completa), mtime: Math.floor(info.mtimeMs / 1000) };
    } catch {
      return null; // borrado en el directorio de trabajo y aún en el índice
    }
  };
  const ventana = 48;
  const pendientes = [];
  let siguiente = 0;
  const llenar = () => {
    while (pendientes.length < ventana && siguiente < archivos.length) pendientes.push(leerArchivo(archivos[siguiente++]));
  };
  llenar();
  while (pendientes.length > 0) {
    const leido = await pendientes.shift();
    llenar();
    if (!leido) continue;
    await escribirEntrada(salida, leido.ruta, leido.contenido, leido.modo, leido.mtime);
    cuantos++;
    bytes += leido.contenido.length;
  }
  salida.write(Buffer.alloc(1024));
  salida.end();
  await once(salida, 'finish');
  return { archivos: cuantos, bytes };
}

/** Escribe `destino` con el contenido confirmado de una rama o commit (`git archive`). */
export async function tarDeRama(repositorio, rama, destino) {
  const salida = createWriteStream(destino);
  const proceso = spawn('git', ['-C', repositorio, 'archive', '--format=tar', rama], { stdio: ['ignore', 'pipe', 'pipe'] });
  proceso.stdout.pipe(salida);
  let errores = '';
  proceso.stderr.on('data', (d) => (errores += d.toString()));
  const cerrado = once(salida, 'close'); // se engancha antes: el flujo puede cerrarse antes de que `git` termine de despedirse
  const [codigo] = await once(proceso, 'close');
  await cerrado;
  if (codigo !== 0) throw new Error(`git archive ${rama} falló: ${errores.trim()}`);
}

/** Contenido de un archivo en el objetivo: del directorio de trabajo o de la rama indicada. */
export async function leerDelObjetivo(objetivo, ruta) {
  if (objetivo.rama) {
    const { stdout } = await execFileP('git', ['-C', objetivo.repositorio, 'show', `${objetivo.rama}:${ruta}`], { encoding: 'utf8', maxBuffer: 64 * 1024 * 1024 });
    return stdout;
  }
  return readFile(join(objetivo.directorio, ruta), 'utf8');
}
