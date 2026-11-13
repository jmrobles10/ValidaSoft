// De los flujos de CI (`.github/workflows/ci.yml` y `.github/workflows/e2e.yml`) a un plan que la réplica local sabe ejecutar en contenedores Linux.
// `ci.yml` trae backend, frontend e imagen; `e2e.yml` (solo `workflow_dispatch`: la e2e corre bajo demanda, decisión E2E-1) trae el trabajo e2e.
// Todo lo que se puede leer de los flujos se lee de ellos (comandos, versiones, rutas, condiciones); lo único fijo es el mapa de
// «acciones de GitHub → equivalente local» y la forma de tres pasos que no son comandos de shell (servicios con compose,
// Playwright en su imagen, artefactos). Si el flujo cambia de forma que este archivo no entiende, `planificar` lanza un error
// que empieza con «SINCRONÍA»: la prueba `sincronia.test.mjs` lo hace saltar en el CI, y la corrida local avisa en vez de
// ejecutar algo distinto de lo que corre GitHub.
import { leerYaml } from './yaml.mjs';

/** Trabajos de `ci.yml` (push y pull request) que la réplica sabe ejecutar. Uno nuevo hace fallar la prueba de sincronía hasta que se replique. */
export const TRABAJOS_DE_CI = ['frontend', 'backend', 'imagen'];

/** Trabajos de `e2e.yml` (solo bajo demanda). */
export const TRABAJOS_DE_E2E = ['e2e'];

/** Todos los trabajos que la réplica sabe ejecutar. */
export const TRABAJOS_CONOCIDOS = ['frontend', 'backend', 'e2e', 'imagen'];

/**
 * Trabajos que corren por omisión: los del CI normal. `e2e` (bajo demanda) e `imagen` solo con `--solo`: la e2e tarda ≈ 17 min y se corre cuando un
 * cambio toca la conexión entre las piezas o antes de una entrega; construir la imagen tarda y rara vez falla distinto.
 */
export const TRABAJOS_POR_OMISION = ['frontend', 'backend'];

/** Lo que `e2e.yml` agrega al final del comando de Playwright para el filtro opcional (entrada `grep`): la réplica lo quita (se filtra con `--e2e-args`). */
const FILTRO_OPCIONAL = '${FILTRO[@]}';

/** Variables que un paso puede declarar con `env:` sin que la réplica las ignore en silencio (el filtro de la e2e vale vacío aquí). */
const ENTORNO_DE_PASO_ADMITIDO = ['E2E_GREP'];

const sincronia = (mensaje) => new Error(`SINCRONÍA con los flujos (ci.yml, e2e.yml): ${mensaje}\n  Actualiza infra/ci-local (lib/plan.mjs) o el flujo para que vuelvan a coincidir.`);

// --- Versiones -------------------------------------------------------------------------------------------------------

/** `dotnet-version: 10.0.x` de setup-dotnet -> imagen oficial del SDK (Ubuntu noble, como el runner `ubuntu-latest`). */
export function imagenDotnet(version) {
  const m = /^(\d+)\.(\d+)\.(x|\d+)$/.exec(String(version));
  if (!m) throw sincronia(`dotnet-version «${String(version)}» no es de la forma 10.0.x ni 10.0.100.`);
  const etiqueta = m[3] === 'x' ? `${m[1]}.${m[2]}` : `${m[1]}.${m[2]}.${m[3]}`;
  return `mcr.microsoft.com/dotnet/sdk:${etiqueta}-noble`;
}

/** `node-version: 22` de setup-node -> imagen oficial de Node. */
export function imagenNode(version) {
  if (!/^\d+(\.\d+){0,2}$/.test(String(version))) throw sincronia(`node-version «${String(version)}» no es un número de versión (lts/* y similares no están soportados).`);
  return `node:${String(version)}-bookworm`;
}

/** Versión de pnpm: la de `with.version` o, sin ella, la de `packageManager` en package.json (como pnpm/action-setup). */
export function versionPnpm(paqueteRaiz, versionAccion) {
  if (versionAccion) return String(versionAccion);
  const m = /^pnpm@(\d+\.\d+\.\d+)/.exec(paqueteRaiz?.packageManager ?? '');
  if (!m) throw sincronia('package.json no declara «packageManager: pnpm@x.y.z» y pnpm/action-setup no trae `version`.');
  return m[1];
}

/** Versión exacta de Playwright de e2e/package.json (la misma cuenta que hace el flujo con `node -p`). */
export function versionPlaywright(paqueteE2e) {
  const declarada = paqueteE2e?.devDependencies?.['@playwright/test'];
  if (!declarada) throw sincronia('e2e/package.json no declara @playwright/test.');
  return String(declarada).replace(/^[^0-9]*/, '');
}

// --- Shell -----------------------------------------------------------------------------------------------------------

/** Parte un texto de shell en palabras (comillas simples y dobles, `\` + salto de línea como continuación). Sin expandir nada. */
export function palabrasDeShell(texto) {
  const unido = texto.replace(/\\\r?\n/g, ' ');
  const palabras = [];
  let actual = '';
  let hay = false;
  let comilla = null;
  for (let i = 0; i < unido.length; i++) {
    const c = unido[i];
    if (comilla) {
      if (c === comilla) comilla = null;
      else if (c === '\\' && comilla === '"' && i + 1 < unido.length && '"\\$`'.includes(unido[i + 1])) actual += unido[++i];
      else actual += c;
    } else if (c === '"' || c === "'") {
      comilla = c;
      hay = true;
    } else if (/\s/.test(c)) {
      if (hay || actual) palabras.push(actual);
      actual = '';
      hay = false;
    } else {
      actual += c;
      hay = true;
    }
  }
  if (comilla) throw sincronia('una comilla sin cerrar en un comando del flujo.');
  if (hay || actual) palabras.push(actual);
  return palabras;
}

/** `docker compose -f archivo up -d --wait a b c` -> piezas. Otra forma lanza error: no se replicaría igual. */
export function analizarCompose(script) {
  const lineas = script.trim().split('\n').filter((l) => l.trim() !== '');
  if (lineas.length !== 1) throw sincronia('el paso de docker compose tiene más de un comando.');
  const palabras = palabrasDeShell(lineas[0]);
  if (palabras[0] !== 'docker' || palabras[1] !== 'compose') throw sincronia('se esperaba «docker compose …».');
  let i = 2;
  const archivos = [];
  while (palabras[i] === '-f') {
    archivos.push(palabras[i + 1]);
    i += 2;
  }
  if (archivos.length !== 1 || palabras[i] !== 'up') throw sincronia('el paso de compose ya no tiene la forma «docker compose -f <archivo> up …».');
  i++;
  const opciones = [];
  const servicios = [];
  for (; i < palabras.length; i++) {
    if (palabras[i].startsWith('-')) {
      if (!['-d', '--detach', '--wait', '--build'].includes(palabras[i])) throw sincronia(`la opción «${palabras[i]}» de «docker compose up» no está replicada.`);
      opciones.push(palabras[i]);
    } else {
      servicios.push(palabras[i]);
    }
  }
  if (servicios.length === 0) throw sincronia('el paso de compose no nombra servicios.');
  return { archivo: archivos[0], opciones, servicios };
}

/** El paso de Playwright: algunas líneas previas (VERSION=…) y un `docker run` con sus banderas, la imagen y el comando. */
export function analizarDockerRun(script) {
  const lineas = script.split('\n');
  const inicio = lineas.findIndex((l) => /^\s*docker run\b/.test(l));
  if (inicio < 0) throw sincronia('el paso no tiene «docker run».');
  const previo = lineas.slice(0, inicio).join('\n').trim();
  const palabras = palabrasDeShell(lineas.slice(inicio).join('\n'));
  const banderas = { rm: false, init: false, ipc: null, red: null, volumenes: [], directorio: null, entorno: {} };
  let i = 2;
  for (; i < palabras.length && palabras[i].startsWith('-'); i++) {
    const p = palabras[i];
    const valor = () => {
      const v = palabras[++i];
      if (v === undefined) throw sincronia(`a «${p}» le falta el valor.`);
      return v;
    };
    if (p === '--rm') banderas.rm = true;
    else if (p === '--init') banderas.init = true;
    else if (p.startsWith('--ipc=')) banderas.ipc = p.slice('--ipc='.length);
    else if (p === '--network' || p === '--net') banderas.red = valor();
    else if (p.startsWith('--network=')) banderas.red = p.slice('--network='.length);
    else if (p === '-v' || p === '--volume') banderas.volumenes.push(valor());
    else if (p === '-w' || p === '--workdir') banderas.directorio = valor();
    else if (p === '-e' || p === '--env') {
      const par = valor();
      const igual = par.indexOf('=');
      if (igual < 0) throw sincronia(`«-e ${par}» no trae valor (heredar del entorno no está replicado).`);
      banderas.entorno[par.slice(0, igual)] = par.slice(igual + 1);
    } else throw sincronia(`la bandera «${p}» de «docker run» no está replicada.`);
  }
  const imagen = palabras[i];
  const palabrasDelComando = palabras.slice(i + 1);
  if (!imagen || palabrasDelComando.length === 0) throw sincronia('el «docker run» del flujo no trae imagen o comando.');
  // El filtro opcional (`"${FILTRO[@]}"`) va al final del comando: la réplica lo quita (por omisión está vacío) y lo avisa en `filtro`.
  const filtro = palabrasDelComando.at(-1) === FILTRO_OPCIONAL;
  const comando = palabrasDelComando.filter((p) => p !== FILTRO_OPCIONAL);
  if (comando.length !== palabrasDelComando.length - (filtro ? 1 : 0)) throw sincronia('el filtro opcional de Playwright solo se admite al final del comando.');
  return { previo, banderas, imagen, comando, filtro };
}

// --- Pasos -----------------------------------------------------------------------------------------------------------

/** `if:` del paso -> cuándo corre: `exito` (por omisión), `siempre` o `falla`. */
export function condicionDePaso(si) {
  if (si == null) return 'exito';
  const limpio = String(si).replace(/^\$\{\{\s*|\s*\}\}$/g, '').trim();
  if (limpio === 'always()') return 'siempre';
  if (limpio === 'failure()' || limpio === '!success()') return 'falla';
  if (limpio === 'success()') return 'exito';
  throw sincronia(`la condición «${String(si)}» no está replicada (solo success(), failure(), !success() y always()).`);
}

function accionDe(uses) {
  return String(uses).replace(/@.*$/, '');
}

function lineasDe(texto) {
  return String(texto ?? '')
    .split('\n')
    .map((l) => l.trim())
    .filter(Boolean);
}

/** Clasifica un paso del flujo en algo que la réplica sabe hacer. */
export function clasificarPaso(paso, indice) {
  const nombre = paso.name ?? paso.uses ?? `paso ${String(indice + 1)}`;
  const base = { nombre, condicion: condicionDePaso(paso.if) };
  if (paso.uses) {
    const accion = accionDe(paso.uses);
    const con = paso.with ?? {};
    switch (accion) {
      case 'actions/checkout':
        return { ...base, tipo: 'fuente' };
      case 'actions/setup-dotnet':
        return { ...base, tipo: 'configurar', dotnet: con['dotnet-version'] };
      case 'actions/setup-node':
        return { ...base, tipo: 'configurar', node: con['node-version'], cachePnpm: con.cache === 'pnpm' };
      case 'pnpm/action-setup':
        return { ...base, tipo: 'configurar', pnpm: con.version ?? null, pnpmDeclarado: true };
      case 'actions/cache':
        return { ...base, tipo: 'configurar', cache: lineasDe(con.path) };
      case 'actions/upload-artifact':
        return { ...base, tipo: 'artefactos', nombreArtefacto: con.name, rutas: lineasDe(con.path) };
      case 'docker/setup-buildx-action':
        return { ...base, tipo: 'ignorar' };
      case 'docker/build-push-action':
        return { ...base, tipo: 'construir', contexto: con.context ?? '.', archivo: con.file ?? 'Dockerfile', etiquetas: lineasDe(con.tags) };
      default:
        throw sincronia(`la acción «${String(paso.uses)}» no tiene réplica local.`);
    }
  }
  if (typeof paso.run !== 'string') throw sincronia(`el paso «${nombre}» no tiene ni uses ni run.`);
  for (const variable of Object.keys(paso.env ?? {})) {
    if (!ENTORNO_DE_PASO_ADMITIDO.includes(variable)) throw sincronia(`el paso «${nombre}» declara la variable de entorno «${variable}» y la réplica solo replica ${ENTORNO_DE_PASO_ADMITIDO.join(', ')}.`);
  }
  const script = paso.run.trim();
  if (/^docker compose\b/.test(script)) return { ...base, tipo: 'compose', ...analizarCompose(script) };
  if (/^\s*docker run\b/m.test(script)) return { ...base, tipo: 'docker-run', ...analizarDockerRun(script) };
  return { ...base, tipo: 'ejecutar', script };
}

/**
 * @param {{ ci: string, e2e: string, compose: string, paquete: string, paqueteE2e: string }} archivos contenido de los archivos del objetivo
 */
export function planificar(archivos) {
  const paquete = JSON.parse(archivos.paquete);
  const compose = leerYaml(archivos.compose);

  const trabajos = {};
  /** Lee los trabajos de un flujo: solo los esperados en él (la e2e fuera de e2e.yml, o un trabajo nuevo sin réplica, es desincronía). */
  const leerTrabajos = (nombreDelFlujo, texto, esperados) => {
    const flujo = leerYaml(texto);
    if (!flujo?.jobs) throw sincronia(`${nombreDelFlujo} no tiene \`jobs\`.`);
    for (const [id, trabajo] of Object.entries(flujo.jobs)) {
      if (!esperados.includes(id)) {
        const nota = id === 'e2e' ? ' (la e2e vive solo en e2e.yml, bajo demanda: decisión E2E-1)' : '';
        throw sincronia(`el trabajo «${id}» de ${nombreDelFlujo} no tiene réplica local${nota}.`);
      }
      trabajos[id] = { ...planDeTrabajo(id, trabajo, paquete), entorno: flujo.env ?? {} };
    }
    for (const id of esperados) {
      if (!trabajos[id]) throw sincronia(`${nombreDelFlujo} ya no tiene el trabajo «${id}» (la réplica local sí lo espera).`);
    }
  };
  leerTrabajos('ci.yml', archivos.ci, TRABAJOS_DE_CI);
  leerTrabajos('e2e.yml', archivos.e2e, TRABAJOS_DE_E2E);

  // Cada trabajo pide una imagen: SDK de .NET, Node, o las dos juntas (API del e2e); Playwright usa la suya.
  for (const t of Object.values(trabajos)) {
    if (t.dotnet && t.node) t.imagen = { tipo: 'dotnet-node', dotnet: imagenDotnet(t.dotnet), node: imagenNode(t.node) };
    else if (t.dotnet) t.imagen = { tipo: 'dotnet', nombre: imagenDotnet(t.dotnet) };
    else if (t.node) t.imagen = { tipo: 'node', nombre: imagenNode(t.node) };
    else t.imagen = null;
  }

  return {
    trabajos,
    servicios: serviciosDeCompose(compose),
    nombresDeServicios: Object.keys(compose?.services ?? {}),
    playwright: versionPlaywright(JSON.parse(archivos.paqueteE2e)),
  };
}

/** Un trabajo del flujo -> sus pasos clasificados y lo que configuran (versiones de .NET, Node y pnpm, cachés). */
function planDeTrabajo(id, trabajo, paquete) {
  const pasos = (trabajo.steps ?? []).map(clasificarPaso);
  const config = { dotnet: null, node: null, pnpm: null, cachePnpm: false, caches: [] };
  for (const p of pasos) {
    if (p.tipo !== 'configurar') continue;
    if (p.dotnet) config.dotnet = String(p.dotnet);
    if (p.node) config.node = String(p.node);
    if (p.pnpmDeclarado) config.pnpm = versionPnpm(paquete, p.pnpm);
    if (p.cachePnpm) config.cachePnpm = true;
    if (p.cache) config.caches.push(...p.cache);
  }
  return {
    id,
    nombre: trabajo.name ?? id,
    timeoutMin: Number(trabajo['timeout-minutes'] ?? 0),
    pasos,
    ...config,
  };
}

/** `ports: ["5432:5432"]` de cada servicio -> { servicio, puertoPublicado, puertoInterno } (para pasar de `localhost:p` a `servicio:p`). */
export function serviciosDeCompose(compose) {
  const salida = [];
  for (const [servicio, definicion] of Object.entries(compose?.services ?? {})) {
    for (const puerto of definicion.ports ?? []) {
      const m = /^(?:[\d.]+:)?(\d+):(\d+)(?:\/\w+)?$/.exec(String(puerto));
      if (!m) throw sincronia(`el puerto «${String(puerto)}» de ${servicio} en compose.yaml no es de la forma «publicado:interno».`);
      salida.push({ servicio, publicado: Number(m[1]), interno: Number(m[2]) });
    }
  }
  return salida;
}

/** Cambia `localhost:<puerto publicado>` por `<servicio>:<puerto interno>` (los servicios van en una red propia, sin puertos del host). */
export function haciaLaRed(texto, serviciosDeLaRed) {
  let resultado = texto;
  for (const { servicio, publicado, interno } of serviciosDeLaRed) {
    resultado = resultado.replace(new RegExp(`(?:localhost|127\\.0\\.0\\.1):${String(publicado)}(?!\\d)`, 'g'), `${servicio}:${String(interno)}`);
  }
  return resultado;
}

/**
 * Lo que `appsettings.Development.json` apunta a `localhost` (PostgreSQL, Mailpit, MinIO) y la API del e2e debe ver por nombre de
 * servicio en la red propia. El resto del archivo (claves, passkeys, etc.) no se toca. La prueba de sincronía comprueba que no
 * queden otros `localhost` sin cubrir.
 */
export function variablesDeLaApi() {
  return {
    ConnectionStrings__Validasoft: 'Host=postgres;Port=5432;Database=validasoft;Username=validasoft;Password=validasoft',
    Validasoft__Correo__Host: 'mailpit',
    Validasoft__Correo__Puerto: '1025',
    Validasoft__Almacen__Endpoint: 'http://minio:9000',
  };
}
