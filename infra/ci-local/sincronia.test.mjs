// Que la réplica local del CI (infra/ci-local) no se desincronice de `.github/workflows/ci.yml` ni de `.github/workflows/e2e.yml`
// (la e2e corre solo bajo demanda: decisión E2E-1). Corre con `pnpm test` (también en el CI): si alguien cambia los flujos de una forma que la réplica no sabe repetir (un trabajo o una
// acción nueva, otra forma de levantar los servicios o de correr Playwright, un servicio nuevo que la API use por `localhost`),
// esta prueba falla y dice qué actualizar en infra/ci-local, en vez de que la réplica local deje de probar lo que prueba GitHub.
import { readFileSync } from 'node:fs';
import { describe, expect, it } from 'vitest';
import { esPrivado } from './lib/fuente.mjs';
import { leerYaml } from './lib/yaml.mjs';
import {
  analizarCompose,
  analizarDockerRun,
  clasificarPaso,
  condicionDePaso,
  haciaLaRed,
  imagenDotnet,
  imagenNode,
  palabrasDeShell,
  planificar,
  serviciosDeCompose,
  TRABAJOS_CONOCIDOS,
  TRABAJOS_DE_CI,
  TRABAJOS_DE_E2E,
  TRABAJOS_POR_OMISION,
  variablesDeLaApi,
  versionPlaywright,
  versionPnpm,
} from './lib/plan.mjs';

const raiz = new URL('../../', import.meta.url);
const leer = (ruta) => readFileSync(new URL(ruta, raiz), 'utf8');
const archivos = {
  ci: leer('.github/workflows/ci.yml'),
  e2e: leer('.github/workflows/e2e.yml'),
  compose: leer('infra/compose/compose.yaml'),
  paquete: leer('package.json'),
  paqueteE2e: leer('e2e/package.json'),
};
const plan = planificar(archivos);

describe('lector de YAML', () => {
  it('lee mapas, listas, comillas, comentarios y bloques de texto', () => {
    const documento = leerYaml(
      [
        '# comentario',
        'a: 1 # otro comentario',
        'b: "dos # no es comentario"',
        "c: 'tres''s'",
        'lista: [x, y]',
        'pasos:',
        '  - name: uno',
        '    run: >-',
        '      linea a',
        '      linea b',
        '  - name: dos',
        '    run: |',
        '      echo uno',
        '      echo dos',
        '  - solo texto',
        'vacio:',
      ].join('\n'),
    );
    expect(documento).toEqual({
      a: '1',
      b: 'dos # no es comentario',
      c: "tres's",
      lista: ['x', 'y'],
      pasos: [{ name: 'uno', run: 'linea a linea b' }, { name: 'dos', run: 'echo uno\necho dos\n' }, 'solo texto'],
      vacio: null,
    });
  });

  it('rechaza lo que no entiende en vez de leerlo mal', () => {
    expect(() => leerYaml('a: {b: 1}')).toThrow(/no están soportados/);
    expect(() => leerYaml('a: "sin cerrar')).toThrow(/sin cerrar/);
  });

  it('lee el flujo real: un comando de pruebas plegado queda en una sola línea', () => {
    const prueba = plan.trabajos.backend.pasos.find((p) => p.tipo === 'ejecutar' && p.script.startsWith('dotnet test'));
    expect(prueba?.script).toBeDefined();
    expect(prueba?.script).not.toContain('\n');
  });
});

describe('ci.yml y la réplica local', () => {
  it('replica todos los trabajos de los flujos y no espera otros', () => {
    expect(Object.keys(plan.trabajos).sort()).toEqual([...TRABAJOS_CONOCIDOS].sort());
    expect(Object.keys(leerYaml(archivos.ci).jobs).sort()).toEqual([...TRABAJOS_DE_CI].sort());
    expect(Object.keys(leerYaml(archivos.e2e).jobs).sort()).toEqual([...TRABAJOS_DE_E2E].sort());
    for (const id of TRABAJOS_POR_OMISION) expect(TRABAJOS_CONOCIDOS).toContain(id);
  });

  it('la e2e corre solo bajo demanda: ci.yml no la tiene y e2e.yml solo se dispara a mano', () => {
    const ci = leerYaml(archivos.ci);
    const e2e = leerYaml(archivos.e2e);
    // El CI normal (push y pull request) no trae la e2e ni depende de ella.
    expect(Object.keys(ci.on)).toEqual(expect.arrayContaining(['push', 'pull_request']));
    expect(Object.keys(ci.jobs)).not.toContain('e2e');
    for (const trabajo of Object.values(ci.jobs)) expect([trabajo.needs ?? []].flat(), 'ningún trabajo de ci.yml espera a la e2e').not.toContain('e2e');
    // e2e.yml: solo `workflow_dispatch` (con el filtro opcional `grep`), nunca push, pull_request ni programación.
    expect(Object.keys(e2e.on)).toEqual(['workflow_dispatch']);
    expect(Object.keys(e2e.on.workflow_dispatch.inputs)).toEqual(['grep']);
    expect(e2e.on.workflow_dispatch.inputs.grep.required).toBe('false');
    // La réplica local tampoco la corre por omisión, pero `--solo e2e` sigue funcionando.
    expect(TRABAJOS_POR_OMISION).not.toContain('e2e');
    expect(TRABAJOS_CONOCIDOS).toContain('e2e');
    expect(TRABAJOS_DE_E2E).toEqual(['e2e']);
  });

  it('la réplica rechaza la e2e dentro de ci.yml y el flujo de e2e sin su trabajo', () => {
    const conE2eEnCi = `${archivos.ci.trimEnd()}

  e2e:
    name: e2e
    runs-on: ubuntu-24.04
    steps:
      - uses: actions/checkout@v7
`;
    expect(() => planificar({ ...archivos, ci: conE2eEnCi })).toThrow(/SINCRONÍA.*«e2e» de ci\.yml/s);
    const sinElTrabajo = ['name: e2e', 'on:', '  workflow_dispatch:', 'jobs:', '  otro:', '    steps: []'].join('\n');
    expect(() => planificar({ ...archivos, e2e: sinElTrabajo })).toThrow(/SINCRONÍA/);
  });

  it('cada acción de GitHub que usan los flujos tiene su equivalente local', () => {
    for (const texto of [archivos.ci, archivos.e2e]) {
      const flujo = leerYaml(texto);
      for (const trabajo of Object.values(flujo.jobs)) {
        trabajo.steps.forEach((paso, i) => {
          expect(() => clasificarPaso(paso, i), `paso «${paso.name ?? paso.uses}»`).not.toThrow();
        });
      }
    }
  });

  it('un paso con variables de entorno que la réplica no replica es una desincronía', () => {
    expect(() => clasificarPaso({ name: 'x', run: 'echo', env: { E2E_GREP: '${{ inputs.grep }}' } }, 0)).not.toThrow();
    expect(() => clasificarPaso({ name: 'x', run: 'echo', env: { OTRA: '1' } }, 0)).toThrow(/SINCRONÍA/);
  });

  it('los flujos no usan condiciones que la réplica no sepa evaluar', () => {
    expect(condicionDePaso(null)).toBe('exito');
    expect(condicionDePaso('always()')).toBe('siempre');
    expect(condicionDePaso('failure()')).toBe('falla');
    expect(condicionDePaso('${{ !success() }}')).toBe('falla');
    expect(() => condicionDePaso("github.ref == 'refs/heads/main'")).toThrow(/SINCRONÍA/);
  });

  it('las versiones salen del flujo y concuerdan con el resto del repositorio', () => {
    const paquete = JSON.parse(archivos.paquete);
    // .NET: el SDK que instala el flujo es el de la plataforma de destino de Directory.Build.props.
    const tfm = /<TargetFramework>net(\d+\.\d+)<\/TargetFramework>/.exec(leer('Directory.Build.props'))?.[1];
    expect(plan.trabajos.backend.imagen.nombre).toBe(imagenDotnet(plan.trabajos.backend.dotnet));
    expect(plan.trabajos.backend.dotnet.startsWith(`${tfm ?? '?'}.`)).toBe(true);
    // Node: el del flujo cumple el `engines` de package.json.
    const minimoNode = /(\d+)/.exec(paquete.engines.node)?.[1];
    expect(Number(plan.trabajos.frontend.node)).toBeGreaterThanOrEqual(Number(minimoNode));
    expect(plan.trabajos.frontend.imagen.nombre).toBe(imagenNode(plan.trabajos.frontend.node));
    // pnpm: el de `packageManager` (el flujo no fija otro).
    expect(plan.trabajos.frontend.pnpm).toBe(versionPnpm(paquete, null));
    expect(plan.trabajos.e2e.pnpm).toBe(plan.trabajos.frontend.pnpm);
    // Playwright: la imagen es la versión EXACTA de e2e/package.json.
    expect(plan.playwright).toBe(versionPlaywright(JSON.parse(archivos.paqueteE2e)));
    expect(/^\d+\.\d+\.\d+$/.test(plan.playwright)).toBe(true);
  });

  it('el trabajo e2e (e2e.yml) levanta los servicios con compose y corre Playwright como la réplica sabe repetirlo', () => {
    const e2e = plan.trabajos.e2e;
    const compose = e2e.pasos.find((p) => p.tipo === 'compose');
    expect(compose?.archivo).toBe('infra/compose/compose.yaml');
    for (const servicio of compose?.servicios ?? []) expect(plan.nombresDeServicios).toContain(servicio);

    const corrida = e2e.pasos.find((p) => p.tipo === 'docker-run');
    expect(corrida?.banderas.red).toBe('host');
    expect(corrida?.imagen).toBe('mcr.microsoft.com/playwright:v${VERSION}-noble');
    expect(corrida?.banderas.volumenes).toEqual(['$GITHUB_WORKSPACE:$GITHUB_WORKSPACE']);
    expect(corrida?.banderas.entorno.CI).toBe('true');
    expect(corrida?.comando.join(' ')).toContain('playwright test');
    // El filtro opcional (entrada `grep`) llega por `E2E_GREP` y `"${FILTRO[@]}"`; la réplica lo deja fuera (se filtra con `--e2e-args`).
    expect(corrida?.filtro).toBe(true);
    expect(corrida?.comando).not.toContain('${FILTRO[@]}');
    expect(corrida?.previo).toContain('E2E_GREP');
    expect(corrida?.previo).toContain('VERSION=');

    // La API se levanta con el mismo script del repositorio y en el mismo puerto que usan las pruebas.
    const levantar = e2e.pasos.find((p) => p.tipo === 'ejecutar' && p.script.includes('api.mjs'));
    expect(levantar?.script).toContain('node e2e/soporte/api.mjs');
    expect(corrida?.banderas.entorno.E2E_BASE_URL).toMatch(/:5090$/);
    expect(levantar?.script).toContain(':5090/health/ready');
  });

  it('los reportes que el flujo sube como artefactos son carpetas del código', () => {
    const subidos = Object.values(plan.trabajos).flatMap((t) => t.pasos.filter((p) => p.tipo === 'artefactos').flatMap((p) => p.rutas));
    expect(subidos.length).toBeGreaterThan(0);
    for (const ruta of subidos) expect(ruta).toMatch(/^[\w./-]+$/);
  });
});

describe('servicios del e2e en una red propia', () => {
  it('lo que appsettings.Development.json apunta a localhost lo cubre la réplica', () => {
    const desarrollo = JSON.parse(leer('src/Validasoft.Api/appsettings.Development.json'));
    const aplanar = (objeto, prefijo = []) =>
      Object.entries(objeto).flatMap(([clave, valor]) =>
        valor !== null && typeof valor === 'object' && !Array.isArray(valor) ? aplanar(valor, [...prefijo, clave]) : [[[...prefijo, clave].join('__'), valor]],
      );
    const conLocalhost = aplanar(desarrollo).filter(([, valor]) => JSON.stringify(valor).includes('localhost'));
    const cubiertas = Object.keys(variablesDeLaApi());
    // Lo demás con «localhost» no es un servicio de la red: origen de las passkeys (el navegador) y enlaces que api.mjs reemplaza.
    const noSonServicios = ['Validasoft__Ingreso__Passkeys__RpId', 'Validasoft__Ingreso__Passkeys__Origenes', 'Validasoft__Correo__UrlPublica'];
    for (const [clave] of conLocalhost) {
      expect(cubiertas.concat(noSonServicios), `«${clave}» apunta a localhost: si es un servicio, agrégalo a variablesDeLaApi() en infra/ci-local/lib/plan.mjs`).toContain(clave);
    }
    // Y cada variable que la réplica cambia existe en el archivo (no queda una variable huérfana).
    const claves = aplanar(desarrollo).map(([clave]) => clave);
    for (const clave of cubiertas) expect(claves, clave).toContain(clave);
  });

  it('los puertos que la API usa en desarrollo son los que compose publica para cada servicio', () => {
    const desarrollo = JSON.parse(leer('src/Validasoft.Api/appsettings.Development.json'));
    const servicios = serviciosDeCompose(leerYaml(archivos.compose));
    const puerto = (servicio, interno) => servicios.find((s) => s.servicio === servicio && s.interno === interno)?.publicado;
    expect(desarrollo.ConnectionStrings.Validasoft).toContain(`Port=${String(puerto('postgres', 5432))}`);
    expect(desarrollo.Validasoft.Correo.Puerto).toBe(puerto('mailpit', 1025));
    expect(desarrollo.Validasoft.Almacen.Endpoint).toContain(`:${String(puerto('minio', 9000))}`);
    // …y la réplica los llama por nombre de servicio con el puerto interno.
    const variables = variablesDeLaApi();
    expect(variables.ConnectionStrings__Validasoft).toContain('Host=postgres;Port=5432');
    expect(variables.Validasoft__Correo__Host).toBe('mailpit');
    expect(variables.Validasoft__Almacen__Endpoint).toBe('http://minio:9000');
  });

  it('pasa de localhost:<puerto publicado> a <servicio>:<puerto interno>', () => {
    const servicios = serviciosDeCompose(leerYaml(archivos.compose));
    expect(haciaLaRed('http://localhost:8025', servicios)).toBe('http://mailpit:8025');
    expect(haciaLaRed('http://localhost:5090', servicios)).toBe('http://localhost:5090');
    expect(haciaLaRed('Host=127.0.0.1;Port=5432', servicios)).toBe('Host=127.0.0.1;Port=5432');
  });
});

describe('analizadores de shell', () => {
  it('separa palabras con comillas y continuaciones de línea', () => {
    expect(palabrasDeShell('a "b c" \'d e\' \\\n f')).toEqual(['a', 'b c', 'd e', 'f']);
  });

  it('analiza docker compose y rechaza otras formas', () => {
    expect(analizarCompose('docker compose -f x.yaml up -d --wait a b')).toEqual({ archivo: 'x.yaml', opciones: ['-d', '--wait'], servicios: ['a', 'b'] });
    expect(() => analizarCompose('docker compose -f x.yaml down')).toThrow(/SINCRONÍA/);
    expect(() => analizarCompose('docker compose -f x.yaml up --scale a=2 a')).toThrow(/SINCRONÍA/);
  });

  it('analiza docker run y rechaza banderas que no replica', () => {
    const r = analizarDockerRun('X=1\ndocker run --rm --network host -v "$A:$A" -e K=V img \\\n  cmd arg');
    expect(r).toMatchObject({ previo: 'X=1', imagen: 'img', comando: ['cmd', 'arg'] });
    expect(r.banderas).toMatchObject({ rm: true, red: 'host', volumenes: ['$A:$A'], entorno: { K: 'V' } });
    expect(() => analizarDockerRun('docker run --privileged img cmd')).toThrow(/SINCRONÍA/);
  });

  it('quita el filtro opcional del final del comando y rechaza uno en otro lugar', () => {
    const conFiltro = analizarDockerRun('docker run --rm img cmd arg "${FILTRO[@]}"');
    expect(conFiltro).toMatchObject({ comando: ['cmd', 'arg'], filtro: true });
    expect(analizarDockerRun('docker run --rm img cmd arg')).toMatchObject({ comando: ['cmd', 'arg'], filtro: false });
    expect(() => analizarDockerRun('docker run --rm img cmd "${FILTRO[@]}" arg')).toThrow(/SINCRONÍA/);
  });
});

describe('copia de trabajo', () => {
  it('nunca lleva al contenedor los archivos privados de la máquina', () => {
    expect(esPrivado('apps/web/.semilla.local.json')).toBe(true);
    expect(esPrivado('.semilla.local.json')).toBe(true);
    expect(esPrivado('apps/web/scripts/semilla-dev.mjs')).toBe(false);
    expect(esPrivado('.git/config')).toBe(true);
  });
});
