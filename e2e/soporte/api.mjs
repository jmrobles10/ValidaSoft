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
const origen = `http://localhost:${puerto}`;

function ejecutar(comando, argumentos) {
  const resultado = spawnSync(comando, argumentos, { cwd: raiz, stdio: 'inherit', shell: process.platform === 'win32' });
  if (resultado.status !== 0) throw new Error(`Falló: ${comando} ${argumentos.join(' ')}`);
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
};

const api = spawn('dotnet', [join(salida, 'Validasoft.Api.dll')], { cwd: salida, env: entorno, stdio: 'inherit' });
const terminar = () => api.kill();
process.on('SIGINT', terminar);
process.on('SIGTERM', terminar);
api.on('exit', (codigo) => process.exit(codigo ?? 0));
