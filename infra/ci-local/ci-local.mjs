#!/usr/bin/env node
// Réplica local del CI de Validasoft: corre en contenedores Linux lo mismo que `.github/workflows/ci.yml` (backend, frontend, imagen) y,
// con `--solo e2e`, lo de `.github/workflows/e2e.yml` (la e2e corre bajo demanda en GitHub, decisión E2E-1).
// Se usa desde `ci-local.sh` (Git Bash) o `ci-local.ps1` (PowerShell); el detalle está en infra/ci-local/README.md.
//
// Idea: los flujos se LEEN (lib/plan.mjs) y cada trabajo corre en un contenedor con la imagen oficial que corresponde a lo que el flujo
// instala en el runner (SDK de .NET 10.0, Node 22, la imagen de Playwright de la versión exacta de e2e/package.json). Los comandos
// son los del flujo, tal cual. Solo cambia lo que no puede ser igual en una máquina compartida: los servicios del e2e van en una
// red Docker propia (sin puertos del host) y cada corrida lleva un identificador propio, así dos agentes pueden correr a la vez.
import { execFile } from 'node:child_process';
import { randomBytes } from 'node:crypto';
import { existsSync, mkdirSync, readFileSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';
import { promisify } from 'node:util';
import { detenerHijos, docker, dockerOk } from './lib/docker.mjs';
import { leerDelObjetivo, tarDeDirectorio, tarDeRama } from './lib/fuente.mjs';
import { haciaLaRed, planificar, TRABAJOS_CONOCIDOS, TRABAJOS_POR_OMISION, variablesDeLaApi } from './lib/plan.mjs';
import { aSegundos, detalleDePaso, detalleDePlaywright, resumenDeFalla } from './lib/resumen.mjs';

const execFileP = promisify(execFile);
const AQUI = dirname(fileURLToPath(import.meta.url));
const RAIZ_DEL_SCRIPT = resolve(AQUI, '..', '..');
const VOLUMEN_CACHE = 'validasoft-cilocal-cache';
const PREFIJO = 'validasoft-ci';
const NOMBRE_DEL_ANFITRION = 'host.docker.internal';
const RUTA_CI = '.github/workflows/ci.yml';
const RUTA_E2E = '.github/workflows/e2e.yml';
const RUTA_COMPOSE = 'infra/compose/compose.yaml';

const AYUDA = `Réplica local del CI de Validasoft (ci.yml y e2e.yml) en contenedores Linux.

Uso: ci-local.sh [opciones]            (Git Bash)
     ci-local.ps1 [opciones]           (PowerShell)

  --solo <trabajo>[,<trabajo>]   backend, frontend, e2e o imagen (por omisión: backend y frontend, como ci.yml; la e2e es bajo demanda: --solo e2e)
  --directorio <ruta>            carpeta (worktree) a probar, con sus cambios sin confirmar (por omisión: la del propio script)
  --rama <rama|commit>           prueba lo confirmado en esa rama o commit, sin tocar ninguna carpeta (p. ej. origin/main)
  --salida <ruta>                dónde dejar logs y reportes (por omisión: <temp>/validasoft-ci-local/<id>)
  --paralelo                     corre los trabajos a la vez, como el CI (por omisión: uno tras otro)
  --e2e-args "<args>"            argumentos extra para «playwright test» (p. ej. pruebas/x.spec.ts o --grep texto), con --solo e2e
  --cpus <n>                     CPU de cada contenedor de trabajo (por omisión 4, lo del runner de GitHub; 0 = sin límite)
  --testcontainers-host <host>   TESTCONTAINERS_HOST_OVERRIDE del trabajo backend (por omisión host.docker.internal)
  --verboso                      muestra la salida de cada paso en vivo (siempre queda en los logs)
  --conservar                    no borra contenedores ni la copia de trabajo al terminar (para inspeccionar; luego --limpiar)
  --plan                         muestra qué haría (imágenes y pasos leídos de ci.yml y e2e.yml) y sale
  --limpiar                      borra restos de corridas que ya no están vivas (contenedores, redes, copias) y sale
  --ayuda                        esto

Salida: 0 todo verde, 1 algún trabajo en rojo, 2 error de uso, de entorno o de sincronía con ci.yml o e2e.yml.`;

// --- Argumentos ------------------------------------------------------------------------------------------------------

function leerArgumentos(argv) {
  const op = { cpus: 4, solo: [], verboso: false, paralelo: false, conservar: false, plan: false, limpiar: false, ayuda: false, e2eArgs: [] };
  const conValor = new Set(['--solo', '--directorio', '--rama', '--salida', '--e2e-args', '--testcontainers-host', '--cpus']);
  for (let i = 0; i < argv.length; i++) {
    let [clave, valor] = argv[i].includes('=') && argv[i].startsWith('--') ? [argv[i].slice(0, argv[i].indexOf('=')), argv[i].slice(argv[i].indexOf('=') + 1)] : [argv[i], undefined];
    if (conValor.has(clave) && valor === undefined) {
      valor = argv[++i];
      if (valor === undefined) throw new UsoIncorrecto(`A «${clave}» le falta el valor.`);
    }
    switch (clave) {
      case '--solo':
        op.solo.push(...valor.split(',').map((x) => x.trim()).filter(Boolean));
        break;
      case '--directorio':
        op.directorio = valor;
        break;
      case '--rama':
        op.rama = valor;
        break;
      case '--salida':
        op.salida = valor;
        break;
      case '--e2e-args':
        op.e2eArgs = valor.split(/\s+/).filter(Boolean);
        break;
      case '--testcontainers-host':
        op.testcontainersHost = valor;
        break;
      case '--cpus':
        op.cpus = Number(valor);
        if (!Number.isFinite(op.cpus) || op.cpus < 0) throw new UsoIncorrecto(`«--cpus ${valor}» no es un número.`);
        break;
      case '--paralelo':
        op.paralelo = true;
        break;
      case '--verboso':
      case '-v':
        op.verboso = true;
        break;
      case '--conservar':
        op.conservar = true;
        break;
      case '--plan':
        op.plan = true;
        break;
      case '--limpiar':
        op.limpiar = true;
        break;
      case '--ayuda':
      case '-h':
      case '--help':
        op.ayuda = true;
        break;
      default:
        throw new UsoIncorrecto(`Opción desconocida «${argv[i]}».`);
    }
  }
  for (const t of op.solo) {
    if (!TRABAJOS_CONOCIDOS.includes(t)) throw new UsoIncorrecto(`«${t}» no es un trabajo (${TRABAJOS_CONOCIDOS.join(', ')}).`);
  }
  if (op.rama && op.directorio && !existsSync(op.directorio)) throw new UsoIncorrecto(`No existe la carpeta ${op.directorio}.`);
  return op;
}

class UsoIncorrecto extends Error {}

// --- Utilidades ------------------------------------------------------------------------------------------------------

const ahora = () => new Date().toLocaleTimeString('es-CO', { hour12: false });
const escribir = (linea) => process.stdout.write(`${linea}\n`);
const slug = (texto) => texto.toLowerCase().normalize('NFD').replace(/[̀-ͯ]/g, '').replace(/[^a-z0-9]+/g, '-').replace(/^-|-$/g, '').slice(0, 48);
const ultimasLineas = (texto, n) => texto.trimEnd().split('\n').slice(-n).join('\n');

async function git(directorio, args) {
  const { stdout } = await execFileP('git', ['-C', directorio, ...args], { encoding: 'utf8' });
  return stdout.trim();
}

async function resolverObjetivo(op) {
  const base = resolve(op.directorio ?? RAIZ_DEL_SCRIPT);
  let raiz;
  try {
    raiz = resolve(await git(base, ['rev-parse', '--show-toplevel']));
  } catch {
    throw new UsoIncorrecto(`${base} no es una carpeta de un repositorio Git.`);
  }
  if (op.rama) {
    let sha;
    try {
      sha = await git(raiz, ['rev-parse', '--short=9', `${op.rama}^{commit}`]);
    } catch {
      throw new UsoIncorrecto(`No existe la rama o el commit «${op.rama}» en ${raiz} (¿falta un git fetch?).`);
    }
    return { rama: op.rama, repositorio: raiz, sha, descripcion: `rama ${op.rama} (${sha})` };
  }
  const sha = await git(raiz, ['rev-parse', '--short=9', 'HEAD']);
  const sucio = (await git(raiz, ['status', '--porcelain'])).split('\n').filter(Boolean).length;
  return { directorio: raiz, repositorio: raiz, sha, descripcion: `${raiz} @ ${sha}${sucio ? ` + ${String(sucio)} cambios sin confirmar` : ''}` };
}

// --- Plan ------------------------------------------------------------------------------------------------------------

async function cargarPlan(objetivo) {
  const leer = (ruta) => leerDelObjetivo(objetivo, ruta);
  const leerFlujoDeE2e = () =>
    leer(RUTA_E2E).catch(() => {
      throw new Error(`SINCRONÍA con los flujos (ci.yml, e2e.yml): ${objetivo.descripcion} no tiene ${RUTA_E2E} (¿falta rebasar sobre main? la e2e vive en su propio flujo, bajo demanda).`);
    });
  const archivos = {
    ci: await leer(RUTA_CI),
    e2e: await leerFlujoDeE2e(),
    compose: await leer(RUTA_COMPOSE),
    paquete: await leer('package.json'),
    paqueteE2e: await leer('e2e/package.json'),
  };
  const plan = planificar(archivos);
  for (const t of Object.values(plan.trabajos)) {
    for (const p of t.pasos) {
      if (p.tipo === 'compose' && p.archivo !== RUTA_COMPOSE) throw new Error(`SINCRONÍA con los flujos (ci.yml, e2e.yml): el flujo usa ${p.archivo} y la réplica espera ${RUTA_COMPOSE}.`);
    }
  }
  return { plan, archivos };
}

function describirPlan(plan, trabajos) {
  const lineas = [];
  for (const id of trabajos) {
    const t = plan.trabajos[id];
    const imagen = t.imagen?.tipo === 'dotnet-node' ? `${t.imagen.dotnet} + ${t.imagen.node}` : (t.imagen?.nombre ?? '(sin contenedor propio)');
    lineas.push(`${id}: ${t.nombre} · imagen ${imagen}${t.pnpm ? ` · pnpm ${t.pnpm}` : ''} · tope del CI ${String(t.timeoutMin)} min`);
    for (const p of t.pasos) {
      const que = {
        fuente: () => 'copia de trabajo (checkout)',
        configurar: () => 'se resuelve con la imagen',
        ejecutar: () => `en el contenedor: ${p.script.split('\n')[0].slice(0, 90)}`,
        compose: () => `servicios en red propia: ${p.servicios.join(', ')}`,
        'docker-run': () => `Playwright en ${p.imagen}`,
        artefactos: () => `reportes: ${p.rutas.join(', ')}`,
        construir: () => `docker build ${p.archivo}`,
        ignorar: () => 'no aplica en local',
      }[p.tipo]();
      lineas.push(`   - ${p.nombre}${p.condicion === 'exito' ? '' : ` [${p.condicion}]`}: ${que}`);
    }
  }
  if (trabajos.includes('e2e')) lineas.push(`playwright: v${plan.playwright}-noble`);
  return lineas;
}

// --- Imágenes --------------------------------------------------------------------------------------------------------

async function existeImagen(nombre) {
  return (await docker(['image', 'inspect', '--format', '{{.Id}}', nombre])).codigo === 0;
}

async function asegurarImagen(nombre) {
  if (await existeImagen(nombre)) return;
  escribir(`[${ahora()}] Descargando la imagen oficial ${nombre} (solo la primera vez)...`);
  await dockerOk(['pull', '-q', nombre]);
}

/** Nombre de la imagen de ejecución de un trabajo; construye la de .NET + Node si hace falta. */
async function imagenDelTrabajo(t) {
  if (!t.imagen) return null;
  if (t.imagen.tipo !== 'dotnet-node') {
    await asegurarImagen(t.imagen.nombre);
    return t.imagen.nombre;
  }
  const etiqueta = `validasoft-cilocal/dotnet-node:${slug(t.imagen.dotnet.split(':')[1])}-${slug(t.imagen.node.split(':')[1])}`;
  if (!(await existeImagen(etiqueta))) {
    await asegurarImagen(t.imagen.dotnet);
    await asegurarImagen(t.imagen.node);
    escribir(`[${ahora()}] Construyendo ${etiqueta} (SDK de .NET + Node, imágenes oficiales; solo la primera vez)...`);
    await dockerOk(['build', '-q', '-t', etiqueta, '--build-arg', `DOTNET_IMAGE=${t.imagen.dotnet}`, '--build-arg', `NODE_IMAGE=${t.imagen.node}`, '-f', join(AQUI, 'Dockerfile.dotnet-node'), AQUI]);
  }
  return etiqueta;
}

// --- Corrida ---------------------------------------------------------------------------------------------------------

class Corrida {
  constructor(op, objetivo, plan, archivos) {
    this.op = op;
    this.objetivo = objetivo;
    this.plan = plan;
    this.archivos = archivos;
    this.id = `ci${Date.now().toString(36)}${randomBytes(2).toString('hex')}`;
    this.salida = resolve(op.salida ?? join(tmpdir(), 'validasoft-ci-local', this.id));
    this.tar = join(this.salida, 'fuente.tar');
    this.contenedores = new Set();
    this.proyectosCompose = new Set();
    this.directoriosDeTrabajo = new Set();
  }

  /** Los contenedores de trabajo ven las CPU del runner de GitHub (4), no las 14 de la máquina: así el paralelismo de vitest o dotnet se parece al del CI y un agente no se come a los demás. */
  get limites() {
    return this.op.cpus > 0 ? ['--cpus', String(this.op.cpus)] : [];
  }

  get etiquetas() {
    return ['--label', 'ci-local=1', '--label', `ci-local.id=${this.id}`, '--label', `ci-local.pid=${String(process.pid)}`];
  }
}

async function prepararFuente(c) {
  mkdirSync(c.salida, { recursive: true });
  const inicio = Date.now();
  if (c.objetivo.rama) {
    await tarDeRama(c.objetivo.repositorio, c.objetivo.rama, c.tar);
    escribir(`[${ahora()}] Copia de trabajo de ${c.objetivo.rama} lista (${aSegundos(Date.now() - inicio)}).`);
  } else {
    const r = await tarDeDirectorio(c.objetivo.directorio, c.tar);
    escribir(`[${ahora()}] Copia de trabajo lista: ${String(r.archivos)} archivos, ${(r.bytes / 1048576).toFixed(1)} MB (${aSegundos(Date.now() - inicio)}).`);
  }
}

/** Contexto de un trabajo en marcha. */
class Trabajo {
  constructor(corrida, plan) {
    this.c = corrida;
    this.t = plan;
    this.ws = `/cache/runs/${corrida.id}/${plan.id}`;
    this.contenedor = null;
    this.red = null;
    this.imagen = null;
    this.carpeta = join(corrida.salida, plan.id);
    this.pasos = [];
    this.fallo = false;
    this.inicio = Date.now();
    this.numero = 0;
    mkdirSync(this.carpeta, { recursive: true });
  }

  /** El trabajo corre `dotnet test` (las pruebas de integración usan Testcontainers). */
  get usaTestcontainers() {
    return this.t.pasos.some((p) => p.tipo === 'ejecutar' && /^dotnet test\b/m.test(p.script));
  }

  get limiteMs() {
    return this.t.timeoutMin ? this.t.timeoutMin * 2 * 60_000 : 0;
  }

  eco() {
    return this.c.op.verboso ? (linea) => escribir(`[${this.t.id}] ${linea}`) : undefined;
  }

  variablesDeEntorno() {
    const env = {
      ...this.t.entorno,
      CI: 'true',
      GITHUB_WORKSPACE: this.ws,
      RUNNER_OS: 'Linux',
      NO_COLOR: '1',
      COREPACK_ENABLE_AUTO_PIN: '0',
    };
    if (this.t.cachePnpm) env.npm_config_store_dir = '/cache/pnpm-store';
    // La API del e2e ve los servicios por nombre en la red propia (en el runner son localhost); lo demás de appsettings.Development.json no cambia.
    if (this.t.id === 'e2e') Object.assign(env, variablesDeLaApi());
    if (this.usaTestcontainers) env.TESTCONTAINERS_HOST_OVERRIDE = this.c.op.testcontainersHost ?? NOMBRE_DEL_ANFITRION;
    return env;
  }

  async asegurarContenedor() {
    if (this.contenedor) return;
    this.imagen ??= await imagenDelTrabajo(this.t);
    const nombre = `${PREFIJO}-${this.c.id}-${this.t.id}`;
    const args = ['run', '-d', '--init', '--name', nombre, ...this.c.etiquetas, ...this.c.limites, '-v', `${VOLUMEN_CACHE}:/cache`, '-w', this.ws];
    for (const [k, v] of Object.entries(this.variablesDeEntorno())) args.push('-e', `${k}=${v}`);
    for (const ruta of this.t.caches) {
      const destino = ruta.replace(/^~/, '/root').replace(/\/$/, '');
      args.push('-v', `validasoft-cilocal-${slug(destino)}:${destino}`);
    }
    // Testcontainers (las pruebas de integración) levanta PostgreSQL y MinIO en el Docker de la máquina: DooD, mismo socket que en el
    // runner. Sus contenedores hermanos publican puertos en el anfitrión, no en este contenedor: se llegan por `host.docker.internal`.
    if (this.usaTestcontainers) args.push('-v', '/var/run/docker.sock:/var/run/docker.sock', '--add-host', `${NOMBRE_DEL_ANFITRION}:host-gateway`);
    if (this.red) args.push('--network', this.red);
    args.push(this.imagen, 'sleep', 'infinity');
    await dockerOk(args);
    this.contenedor = nombre;
    this.c.contenedores.add(nombre);
  }

  /** Corre un script de shell en el contenedor, como un `run:` de GitHub (`bash -e`). */
  async ejecutarScript(script, { log, limiteMs } = {}) {
    await this.asegurarContenedor();
    const archivo = `/tmp/paso-${String(++this.numero)}.sh`;
    await dockerOk(['exec', '-i', this.contenedor, 'sh', '-c', `cat > ${archivo}`], { entrada: script.endsWith('\n') ? script : `${script}\n` });
    return docker(['exec', '-w', this.ws, this.contenedor, 'bash', '--noprofile', '--norc', '-e', archivo], { log, eco: this.eco(), limiteMs });
  }
}

function restante(tr) {
  return tr.limiteMs ? Math.max(1000, tr.limiteMs - (Date.now() - tr.inicio)) : undefined;
}

async function pasoFuente(tr) {
  tr.imagen ??= await imagenDelTrabajo(tr.t);
  const imagen = tr.imagen;
  if (!imagen) return; // un trabajo sin contenedor propio (docker build) recibe el contexto por stdin
  tr.c.directoriosDeTrabajo.add(`/cache/runs/${tr.c.id}`);
  const r = await docker(
    ['run', '--rm', '-i', ...tr.c.etiquetas, '-v', `${VOLUMEN_CACHE}:/cache`, '--entrypoint', 'bash', imagen, '-c', `mkdir -p ${tr.ws} && tar -x -C ${tr.ws}`],
    { entradaDesde: tr.c.tar, log: join(tr.carpeta, '00-copia.log') },
  );
  if (r.codigo !== 0) throw new Error(`No se pudo copiar el código al contenedor:\n${ultimasLineas(r.salida, 15)}`);
}

async function pasoPnpm(tr, version) {
  const r = await tr.ejecutarScript(`npm install --global --silent pnpm@${version}\npnpm --version`, { log: join(tr.carpeta, '01-pnpm.log') });
  if (r.codigo !== 0) throw new Error(`No se pudo instalar pnpm ${version}:\n${ultimasLineas(r.salida, 15)}`);
}

async function pasoCompose(tr, paso) {
  const c = tr.c;
  const carpeta = join(c.salida, 'compose');
  mkdirSync(carpeta, { recursive: true });
  const proyecto = `${PREFIJO}-${c.id}`;
  const archivo = join(carpeta, 'compose.yaml');
  writeFileSync(archivo, c.archivos.compose);
  // Servicios en una red propia y sin puertos del host (nada choca con la API, Mailpit o las bases que ya corren en la máquina).
  const servicios = c.plan.nombresDeServicios;
  const etiquetas = [`ci-local=1`, `ci-local.id=${c.id}`, `ci-local.pid=${String(process.pid)}`];
  const anulaciones = servicios.map((s) => `  ${s}:\n    ports: !reset []\n    labels:\n${etiquetas.map((e) => `      - "${e}"`).join('\n')}`).join('\n');
  const anulacion = join(carpeta, 'sin-puertos.yaml');
  writeFileSync(anulacion, `services:\n${anulaciones}\n`);
  c.proyectosCompose.add({ proyecto, archivo, anulacion });
  const r = await docker(['compose', '-p', proyecto, '-f', archivo, '-f', anulacion, 'up', ...paso.opciones, ...paso.servicios], { log: join(tr.carpeta, `${String(++tr.numero).padStart(2, '0')}-servicios.log`), eco: tr.eco(), limiteMs: restante(tr) });
  if (r.codigo !== 0) throw new Error(`No levantaron los servicios:\n${ultimasLineas(r.salida, 20)}`);
  tr.red = `${proyecto}_default`;
  tr.servicios = c.plan.servicios.filter((s) => paso.servicios.includes(s.servicio));
  return `servicios ${paso.servicios.join(', ')} en la red ${tr.red}`;
}

async function pasoPlaywright(tr, paso) {
  const bandera = paso.banderas;
  if (bandera.red !== 'host') throw new Error('SINCRONÍA con los flujos (ci.yml, e2e.yml): el paso de Playwright ya no usa «--network host».');
  if (!tr.contenedor) await tr.asegurarContenedor();
  // La versión se calcula con las mismas líneas del flujo (VERSION=…), dentro del contenedor que ya tiene el código y Node.
  let version = String(tr.c.plan.playwright);
  if (paso.previo) {
    const r = await tr.ejecutarScript(`${paso.previo}\necho "::version::$VERSION"`);
    const m = /::version::(\S+)/.exec(r.salida);
    if (r.codigo !== 0 || !m) throw new Error(`No se pudo calcular la versión de Playwright:\n${ultimasLineas(r.salida, 10)}`);
    version = m[1];
  }
  const imagen = paso.imagen.replaceAll('${VERSION}', version).replaceAll('$VERSION', version);
  await asegurarImagen(imagen);
  const espacio = (texto) => texto.replaceAll('${GITHUB_WORKSPACE}', tr.ws).replaceAll('$GITHUB_WORKSPACE', tr.ws);
  const args = ['run', '--name', `${PREFIJO}-${tr.c.id}-playwright`, ...tr.c.etiquetas, ...tr.c.limites];
  if (bandera.rm) args.push('--rm');
  if (bandera.init) args.push('--init');
  if (bandera.ipc) args.push(`--ipc=${bandera.ipc}`);
  // `--network host` del runner -> la red del contenedor de la API: localhost:5090 sigue siendo la API (y los webhooks de la prueba llegan a ella).
  args.push('--network', `container:${tr.contenedor}`);
  for (const v of bandera.volumenes) {
    if (espacio(v) !== `${tr.ws}:${tr.ws}`) throw new Error(`SINCRONÍA con los flujos (ci.yml, e2e.yml): el volumen «${v}» del paso de Playwright no está replicado.`);
    args.push('-v', `${VOLUMEN_CACHE}:/cache`);
  }
  if (bandera.directorio) args.push('-w', espacio(bandera.directorio));
  for (const [k, v] of Object.entries(bandera.entorno)) args.push('-e', `${k}=${haciaLaRed(v, tr.servicios ?? [])}`);
  args.push(imagen, ...paso.comando, ...tr.c.op.e2eArgs);
  tr.c.contenedores.add(`${PREFIJO}-${tr.c.id}-playwright`);
  return { args, imagen };
}

/** Ejecuta un paso; devuelve un texto corto para el resumen. Lanza si falla. */
async function ejecutarPaso(tr, paso, indice) {
  const nn = String(indice + 1).padStart(2, '0');
  const log = join(tr.carpeta, `${nn}-${slug(paso.nombre)}.log`);
  switch (paso.tipo) {
    case 'fuente':
      await pasoFuente(tr);
      return null;
    case 'configurar':
      if (paso.pnpmDeclarado) await pasoPnpm(tr, tr.t.pnpm);
      return null;
    case 'compose':
      return pasoCompose(tr, paso);
    case 'ejecutar': {
      const r = await tr.ejecutarScript(paso.script, { log, limiteMs: restante(tr) });
      if (r.agotado) throw new Error(`Tiempo agotado (${String(tr.limiteMs / 60000)} min, el doble del tope del CI).`);
      const cuentas = detalleDePaso(paso.script, r.salida);
      if (r.codigo !== 0) throw Object.assign(new Error(`Salió con código ${String(r.codigo)}${cuentas ? ` · ${cuentas}` : ''}`), { log, salida: r.salida });
      return cuentas;
    }
    case 'docker-run': {
      const { args } = await pasoPlaywright(tr, paso);
      const r = await docker(args, { log, eco: tr.eco(), limiteMs: restante(tr) });
      if (r.agotado) throw new Error(`Tiempo agotado (${String(tr.limiteMs / 60000)} min, el doble del tope del CI).`);
      const detalle = detalleDePlaywright(r.salida);
      if (r.codigo !== 0) throw Object.assign(new Error(`Salió con código ${String(r.codigo)}${detalle ? ` (${detalle})` : ''}`), { log, salida: r.salida });
      return detalle;
    }
    case 'construir':
      return pasoConstruir(tr, paso, log);
    default:
      return null; // artefactos (se copian al final) e ignorados
  }
}

async function pasoConstruir(tr, paso, log) {
  const etiqueta = (paso.etiquetas[0] ?? 'validasoft/app:ci').replace('${{ github.sha }}', `local-${tr.c.id}`);
  const r = await docker(['build', '-f', paso.archivo, '-t', etiqueta, paso.contexto === '.' ? '-' : paso.contexto], { entradaDesde: tr.c.tar, log, eco: tr.eco(), limiteMs: restante(tr) });
  if (r.codigo !== 0) throw Object.assign(new Error(`docker build salió con código ${String(r.codigo)}`), { log, salida: r.salida });
  await docker(['image', 'rm', '-f', etiqueta]);
  return `imagen construida (${etiqueta}) y descartada`;
}

/** Copia a la carpeta de salida los reportes que ci.yml y e2e.yml suben como artefactos (`upload-artifact`), pase lo que pase. */
async function recolectar(tr) {
  if (!tr.contenedor) return;
  for (const p of tr.t.pasos.filter((x) => x.tipo === 'artefactos')) {
    for (const ruta of p.rutas) {
      const origen = ruta.replace(/\/$/, '');
      const destino = join(tr.carpeta, 'reportes', origen.replaceAll('/', '_'));
      mkdirSync(dirname(destino), { recursive: true });
      const esCarpeta = ruta.endsWith('/');
      await docker(['cp', `${tr.contenedor}:${tr.ws}/${origen}${esCarpeta ? '/.' : ''}`, destino]);
    }
  }
}

async function correrTrabajo(c, plan) {
  const tr = new Trabajo(c, plan);
  escribir(`[${ahora()}] ${plan.id}: empieza (${plan.nombre})`);
  try {
    for (let i = 0; i < plan.pasos.length; i++) {
      const paso = plan.pasos[i];
      const corre = paso.condicion === 'siempre' || (paso.condicion === 'exito' && !tr.fallo) || (paso.condicion === 'falla' && tr.fallo);
      if (!corre) continue;
      if (paso.tipo === 'ignorar' || paso.tipo === 'artefactos' || (paso.tipo === 'configurar' && !paso.pnpmDeclarado)) continue;
      const inicio = Date.now();
      let estado = 'ok';
      let detalle = null;
      let error = null;
      try {
        detalle = await ejecutarPaso(tr, paso, i);
      } catch (e) {
        estado = 'falla';
        error = e;
        tr.fallo = true;
      }
      const ms = Date.now() - inicio;
      if ((paso.tipo === 'fuente' || paso.tipo === 'configurar') && estado === 'ok') continue;
      tr.pasos.push({ nombre: paso.nombre, estado, ms, detalle, error: error ? { mensaje: error.message, log: error.log, cola: error.salida ? resumenDeFalla(error.salida) : null } : null });
      escribir(`[${ahora()}] ${plan.id}: ${paso.nombre} -> ${estado === 'ok' ? 'OK' : 'FALLA'} (${aSegundos(ms)})${detalle ? ` · ${detalle}` : ''}`);
    }
  } catch (e) {
    tr.fallo = true;
    tr.pasos.push({ nombre: 'preparación', estado: 'falla', ms: 0, detalle: null, error: { mensaje: e.message, log: null, cola: null } });
  } finally {
    await recolectar(tr).catch(() => undefined);
  }
  return { id: plan.id, nombre: plan.nombre, verde: !tr.fallo, ms: Date.now() - tr.inicio, pasos: tr.pasos, topeMin: plan.timeoutMin, carpeta: tr.carpeta };
}

// --- Limpieza --------------------------------------------------------------------------------------------------------

async function limpiarCorrida(c) {
  if (c.op.conservar) {
    escribir(`[${ahora()}] --conservar: contenedores «${PREFIJO}-${c.id}-*» y copia en ${[...c.directoriosDeTrabajo].join(', ')} se quedan (límpialos con --limpiar).`);
    return;
  }
  for (const nombre of c.contenedores) await docker(['rm', '-f', '-v', nombre]);
  for (const p of c.proyectosCompose) await docker(['compose', '-p', p.proyecto, '-f', p.archivo, '-f', p.anulacion, 'down', '-v', '--remove-orphans', '-t', '2']);
  for (const dir of c.directoriosDeTrabajo) await borrarDirectorios([dir]);
}

/** Una imagen ya descargada para correr comandos sueltos sobre el volumen de trabajo. */
async function imagenCualquiera() {
  const imagenes = (await docker(['image', 'ls', '--format', '{{.Repository}}:{{.Tag}}', 'node'])).salida.split('\n');
  return imagenes.find((l) => l && !l.includes('<none>')) ?? 'node:22-bookworm';
}

async function borrarDirectorios(directorios) {
  if (directorios.length === 0) return;
  await docker(['run', '--rm', '-v', `${VOLUMEN_CACHE}:/cache`, '--entrypoint', 'rm', await imagenCualquiera(), '-rf', ...directorios]);
}

const vivo = (pid) => {
  try {
    process.kill(Number(pid), 0);
    return true;
  } catch (e) {
    return e.code === 'EPERM';
  }
};

/** Quita lo que dejaron corridas interrumpidas (cuyo proceso ya no existe), sin tocar las que siguen vivas. */
async function limpiarRestos() {
  const filas = (await docker(['ps', '-a', '--filter', 'label=ci-local=1', '--format', '{{.Names}}|{{.Label "ci-local.id"}}|{{.Label "ci-local.pid"}}'])).salida.split('\n').filter(Boolean);
  const vivas = new Set();
  let borrados = 0;
  for (const fila of filas) {
    const [nombre, id, pid] = fila.split('|');
    if (vivo(pid)) {
      vivas.add(id);
      continue;
    }
    await docker(['rm', '-f', '-v', nombre]);
    borrados++;
  }
  const redes = (await docker(['network', 'ls', '--filter', `name=${PREFIJO}-`, '--format', '{{.Name}}'])).salida.split('\n').filter(Boolean);
  for (const red of redes) {
    const ocupada = (await docker(['network', 'inspect', '--format', '{{len .Containers}}', red])).salida.trim();
    if (ocupada === '0') await docker(['network', 'rm', red]);
  }
  const volumenes = (await docker(['volume', 'ls', '--filter', `name=${PREFIJO}-`, '--format', '{{.Name}}'])).salida.split('\n').filter(Boolean);
  for (const v of volumenes) await docker(['volume', 'rm', v]);
  // Copias de trabajo de corridas que ya no están vivas.
  // Solo las de más de 15 minutos: una corrida que está copiando su código todavía no tiene contenedores que la delaten.
  const listado = await docker(['run', '--rm', '-v', `${VOLUMEN_CACHE}:/cache`, '--entrypoint', 'find', await imagenCualquiera(), '/cache/runs', '-mindepth', '1', '-maxdepth', '1', '-type', 'd', '-mmin', '+15']);
  const muertas = listado.salida
    .split('\n')
    .map((d) => d.trim().replace('/cache/runs/', ''))
    .filter((d) => /^ci[0-9a-z]+$/.test(d) && !vivas.has(d))
    .map((d) => `/cache/runs/${d}`);
  await borrarDirectorios(muertas);
  escribir(`Limpieza: ${String(borrados)} contenedores, ${String(muertas.length)} copias de trabajo. Corridas vivas respetadas: ${String(vivas.size)}.`);
}

// --- Resumen ---------------------------------------------------------------------------------------------------------

function imprimirResumen(c, resultados, ms) {
  const lineas = ['', `=== Resumen ci-local ${c.id} ===`, `Origen: ${c.objetivo.descripcion}`];
  for (const r of resultados) {
    const aviso = r.topeMin && r.ms > r.topeMin * 60_000 ? ` (¡pasa del tope de ${String(r.topeMin)} min del CI!)` : '';
    lineas.push(`${r.verde ? 'VERDE' : 'ROJO '}  ${r.id.padEnd(9)} ${aSegundos(r.ms)}${aviso}`);
    for (const p of r.pasos) {
      if (p.detalle || p.estado !== 'ok') lineas.push(`         - ${p.nombre}: ${p.estado === 'ok' ? 'OK' : 'FALLA'} · ${p.detalle ?? p.error?.mensaje ?? ''}`);
    }
  }
  const fallos = resultados.flatMap((r) => r.pasos.filter((p) => p.estado !== 'ok').map((p) => ({ trabajo: r.id, ...p })));
  for (const f of fallos) {
    lineas.push('', `--- ${f.trabajo} › ${f.nombre}: ${f.error?.mensaje ?? ''}`);
    if (f.error?.cola) lineas.push(f.error.cola);
    if (f.error?.log) lineas.push(`(log completo: ${f.error.log})`);
  }
  lineas.push('', `Tiempo total: ${aSegundos(ms)} · reportes y logs: ${c.salida}`, resultados.every((r) => r.verde) ? 'RESULTADO: VERDE' : 'RESULTADO: ROJO');
  const texto = lineas.join('\n');
  writeFileSync(join(c.salida, 'resumen.txt'), `${texto}\n`);
  writeFileSync(join(c.salida, 'resumen.json'), JSON.stringify({ id: c.id, origen: c.objetivo.descripcion, ms, trabajos: resultados }, null, 2));
  escribir(texto);
}

// --- Principal -------------------------------------------------------------------------------------------------------

async function principal() {
  const op = leerArgumentos(process.argv.slice(2));
  if (op.ayuda) {
    escribir(AYUDA);
    return 0;
  }
  const version = await docker(['version', '--format', '{{.Server.Version}}']);
  if (version.codigo !== 0) throw new UsoIncorrecto('Docker no responde: abre Docker Desktop y espera a que diga «running».');
  if (op.limpiar) {
    await limpiarRestos();
    return 0;
  }
  const objetivo = await resolverObjetivo(op);
  const { plan, archivos } = await cargarPlan(objetivo);
  const trabajos = op.solo.length ? [...new Set(op.solo)] : TRABAJOS_POR_OMISION;
  if (op.plan) {
    escribir(`Origen: ${objetivo.descripcion}`);
    for (const l of describirPlan(plan, trabajos)) escribir(l);
    return 0;
  }

  const c = new Corrida(op, objetivo, plan, archivos);
  const inicio = Date.now();
  let interrumpida = false;
  const alInterrumpir = () => {
    if (interrumpida) process.exit(130);
    interrumpida = true;
    escribir(`\n[${ahora()}] Interrumpido: limpiando contenedores...`);
    detenerHijos();
    limpiarCorrida(c).finally(() => process.exit(130));
  };
  process.on('SIGINT', alInterrumpir);
  process.on('SIGTERM', alInterrumpir);
  escribir(`ci-local ${c.id} · ${objetivo.descripcion} · trabajos: ${trabajos.join(', ')}${op.paralelo ? ' (en paralelo)' : ''}`);

  let resultados = [];
  try {
    await prepararFuente(c);
    if (trabajos.includes('e2e')) await asegurarImagen(`mcr.microsoft.com/playwright:v${plan.playwright}-noble`);
    const correr = (id) => correrTrabajo(c, plan.trabajos[id]);
    resultados = op.paralelo ? await Promise.all(trabajos.map(correr)) : [];
    if (!op.paralelo) for (const id of trabajos) resultados.push(await correr(id));
  } finally {
    await limpiarCorrida(c);
  }
  imprimirResumen(c, resultados, Date.now() - inicio);
  return resultados.every((r) => r.verde) ? 0 : 1;
}

principal().then(
  (codigo) => process.exit(codigo),
  (e) => {
    if (e instanceof UsoIncorrecto) {
      process.stderr.write(`${e.message}\n\n${AYUDA}\n`);
      process.exit(2);
    }
    process.stderr.write(`${e.stack ?? e.message}\n`);
    process.exit(/^SINCRONÍA/.test(e.message) ? 2 : 1);
  },
);
