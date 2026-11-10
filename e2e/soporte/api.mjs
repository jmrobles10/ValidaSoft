// Levanta Validasoft para las pruebas e2e como en producción: un solo origen que sirve la API, la app embebible en
// /app/ y la del tercero en / (src/Validasoft.Api/Dockerfile). Lo usa `webServer` de playwright.config.ts cuando no
// se indica E2E_BASE_URL.
//
//   1. Compila las dos apps (pnpm build) y publica la API en e2e/.api (carpeta propia: no bloquea las DLL de bin/
//      mientras se desarrolla). Con E2E_SIN_COMPILAR=1 se salta este paso y usa lo ya publicado.
//   2. Arranca la API en E2E_PUERTO (5090) con PostgreSQL, Mailpit y MinIO de infra/compose/compose.yaml, los
//      enlaces de los correos apuntando a ese mismo origen y el límite de /public/v1 alto (muchas peticiones del
//      mismo IP en poco tiempo).
import { spawn, spawnSync } from 'node:child_process';
import { existsSync } from 'node:fs';
import { dirname, join, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const raiz = resolve(dirname(fileURLToPath(import.meta.url)), '..', '..');
const salida = join(raiz, 'e2e', '.api');
const puerto = process.env.E2E_PUERTO ?? '5090';
const aislado = process.env.E2E_AISLADO === '1';
// E2E_BASE_DATOS: otra base para correr dos suites a la vez (p. ej. desde dos worktrees) sin compartir colas.
const BASE_AISLADA = process.env.E2E_BASE_DATOS ?? 'validasoft_e2e';
const origen = `http://localhost:${puerto}`;

function ejecutar(comando, argumentos) {
  const resultado = spawnSync(comando, argumentos, { cwd: raiz, stdio: 'inherit', shell: process.platform === 'win32' });
  if (resultado.status !== 0) throw new Error(`Falló: ${comando} ${argumentos.join(' ')}`);
}

/** Docker sin shell (los argumentos llevan comillas y espacios). */
function docker(argumentos, opciones = {}) {
  return spawnSync('docker', ['compose', '-f', 'infra/compose/compose.yaml', ...argumentos], { cwd: raiz, encoding: 'utf8', ...opciones });
}

// Aislada (local): Mailpit propio y la base `validasoft_e2e`, creada si no existe (el esquema lo crea la API al arrancar).
if (aislado) {
  if (docker(['--profile', 'e2e', 'up', '-d', '--wait', 'mailpit-e2e'], { stdio: 'inherit' }).status !== 0) throw new Error('No se pudo levantar mailpit-e2e.');
  const existe = docker(['exec', '-T', 'postgres', 'psql', '-U', 'validasoft', '-d', 'postgres', '-tAc', `SELECT 1 FROM pg_database WHERE datname = '${BASE_AISLADA}'`]);
  if (existe.status !== 0) throw new Error(`No se pudo consultar PostgreSQL: ${existe.stderr}`);
  if (existe.stdout.trim() !== '1' && docker(['exec', '-T', 'postgres', 'createdb', '-U', 'validasoft', BASE_AISLADA], { stdio: 'inherit' }).status !== 0) {
    throw new Error(`No se pudo crear la base ${BASE_AISLADA}.`);
  }
}

if (process.env.E2E_SIN_COMPILAR !== '1' || !existsSync(join(salida, 'Validasoft.Api.dll'))) {
  ejecutar('pnpm', ['--filter', '@validasoft/web', '--filter', '@validasoft/public', 'build']);
  ejecutar('dotnet', ['publish', 'src/Validasoft.Api/Validasoft.Api.csproj', '-c', 'Release', '-o', salida, '--nologo', '-v', 'q']);
}

const entorno = {
  ...process.env,
  ASPNETCORE_ENVIRONMENT: 'Development',
  ASPNETCORE_URLS: origen,
  Validasoft__Apps__Web: join(raiz, 'apps', 'web', 'dist'),
  Validasoft__Apps__Publica: join(raiz, 'apps', 'public', 'dist'),
  Validasoft__Correo__UrlPublica: origen,
  Validasoft__LimitePublico__Peticiones: '100000',
  // El PDF del certificado se genera casi al instante (por defecto espera unos segundos entre pasadas).
  Validasoft__Certificados__SegundosProceso: '1',
  // La consulta automática de listas (proveedor simulado) corre casi al instante tras pasar a revisión.
  Validasoft__Verificadores__SegundosProceso: '1',
  ...(aislado && {
    ConnectionStrings__Validasoft: `Host=localhost;Port=5432;Database=${BASE_AISLADA};Username=validasoft;Password=validasoft`,
    Validasoft__Correo__Puerto: '1026',
  }),
};

const api = spawn('dotnet', [join(salida, 'Validasoft.Api.dll')], { cwd: salida, env: entorno, stdio: 'inherit' });
const terminar = () => api.kill();
process.on('SIGINT', terminar);
process.on('SIGTERM', terminar);
api.on('exit', (codigo) => process.exit(codigo ?? 0));
