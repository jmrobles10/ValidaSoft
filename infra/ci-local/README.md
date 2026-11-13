# Réplica local del CI

Corre en esta máquina (Windows con Docker Desktop y WSL2) **lo mismo que `.github/workflows/ci.yml` (backend, frontend e imagen) y, bajo demanda, `.github/workflows/e2e.yml` (las pruebas de punta a punta), en contenedores Linux**. Los errores de entorno (rutas con mayúsculas, el SDK de .NET de Linux, Node 22, Testcontainers, Playwright en su imagen, servicios sin `localhost`) aparecen aquí y no en GitHub. No usa `act` ni descarga nada que no sea una imagen oficial. Decisiones CI-1 a CI-9 y E2E-1 (la e2e fuera del CI, bajo demanda) en `docs/decisiones/registro.md`; flujo de trabajo en `docs/13-flujo-de-trabajo-con-claude-code.md` §10.

```bash
infra/ci-local/ci-local.sh                          # backend y frontend sobre esta carpeta, como ci.yml (Git Bash)
.\infra\ci-local\ci-local.ps1                       # lo mismo en PowerShell
infra/ci-local/ci-local.sh --solo frontend          # un trabajo (backend, frontend, e2e, imagen)
infra/ci-local/ci-local.sh --solo e2e                # la e2e, bajo demanda (lee e2e.yml)
infra/ci-local/ci-local.sh --solo backend,frontend --paralelo
infra/ci-local/ci-local.sh --rama origin/main       # lo confirmado en una rama o commit, sin tocar ninguna carpeta
infra/ci-local/ci-local.sh --directorio C:/Fuentes_Git/Validasoft/.claude/worktrees/otro --salida <carpeta>
infra/ci-local/ci-local.sh --plan                   # qué haría, leído de ci.yml y e2e.yml
infra/ci-local/ci-local.sh --limpiar                # restos de corridas muertas
infra/ci-local/ci-local.sh --ayuda
```

Requisitos: Docker Desktop encendido, Node ≥ 22 y Git en el `PATH`. La primera vez descarga las imágenes oficiales (`mcr.microsoft.com/dotnet/sdk:10.0-noble`, `node:22-bookworm`, `mcr.microsoft.com/playwright:v<versión>-noble`) y arma una imagen local que junta las dos primeras; después todo está en caché.

| Opción | Qué hace |
|---|---|
| `--solo a,b` | Trabajos a correr: `backend`, `frontend`, `e2e`, `imagen`. Por omisión `backend` y `frontend`: lo que corre `ci.yml` en cada push y pull request menos la imagen. **La e2e no corre por omisión** (decisión E2E-1: solo se corre cuando hace falta): `--solo e2e` |
| `--directorio <ruta>` | Carpeta (worktree) a probar, **con sus cambios sin confirmar**. Por omisión, la del propio script |
| `--rama <rama o commit>` | Prueba lo confirmado (`git archive`), sin tocar ninguna carpeta. Sirve para `origin/main` o una rama rebasada |
| `--salida <ruta>` | Logs y reportes. Por omisión `<temp>/validasoft-ci-local/<id>` (se imprime al final) |
| `--paralelo` | Los trabajos a la vez, como el CI. Por omisión uno tras otro (la máquina se comparte; en paralelo las pruebas de interfaz y de integración dan más falsos rojos por tiempos) |
| `--cpus <n>` | CPU de cada contenedor de trabajo. Por omisión 4, lo del runner de GitHub (0 = sin límite) |
| `--e2e-args "<args>"` | Argumentos extra para `playwright test` (p. ej. `pruebas/x.spec.ts` o `--grep <texto>`, el equivalente de la entrada `grep` de `e2e.yml`). Con `--solo e2e` |
| `--testcontainers-host <host>` | `TESTCONTAINERS_HOST_OVERRIDE` del backend (por omisión `host.docker.internal`) |
| `--verboso` | Muestra la salida de cada paso en vivo (siempre queda en los logs) |
| `--conservar` | No borra contenedores ni la copia de trabajo (para entrar con `docker exec`; luego `--limpiar`) |

Sale con 0 si todo queda verde, 1 si algún trabajo queda en rojo y 2 si hay un error de uso, de entorno (Docker apagado) o de sincronía con `ci.yml` o `e2e.yml`.

## Qué imprime y dónde deja las cosas

Una línea por paso y, al final, un resumen corto:

```
=== Resumen ci-local ci… ===
Origen: rama origin/main (7413fbfe0)
VERDE  frontend  …
         - Pruebas: OK · 1096 pruebas en 158 archivos: 1096 pasan, 0 fallan, 0 omitidas
ROJO   backend   …
         - Probar (…): FALLA · Salió con código 1 · 1233 pruebas en 4 proyectos: 1232 pasan, 1 fallan, 0 omitidas
--- backend › Probar (…)
1 falla(s):
  - Validasoft.Dominio.Tests.AlgoTests.Hace_algo
  …(el final del log)
RESULTADO: ROJO
```

Todo queda en la carpeta de salida:

```
<salida>/resumen.txt, resumen.json
<salida>/<trabajo>/NN-<paso>.log          salida completa de cada paso
<salida>/backend/reportes/TestResults     .trx
<salida>/e2e/reportes/e2e_playwright-report, e2e_test-results, api-e2e.log
```

Los reportes se copian **siempre**, no solo cuando falla (ci.yml los sube solo si falla).

## Qué corre cada flujo (la e2e, bajo demanda)

| Flujo | Se dispara | Trabajos | Aquí |
|---|---|---|---|
| `ci.yml` | push y pull request | backend, frontend, imagen | `backend` y `frontend` por omisión; `imagen` con `--solo imagen` |
| `e2e.yml` | solo a mano (`workflow_dispatch`, con el filtro opcional `grep`) | e2e (Playwright) | solo con `--solo e2e` |

La e2e salió del CI el 2026-11-12 (decisión E2E-1): tarda ≈ 17 min y las reglas se prueban con las pruebas de dominio, de integración y de Vitest. Se corre cuando un cambio toca la conexión entre las piezas (arranque, autenticación, configuración del stack) o antes de una entrega. El filtro del flujo (`grep`) se replica con `--e2e-args "--grep <texto>"`.

## Cómo replica cada trabajo

Los flujos se **leen** (`lib/plan.mjs`): los comandos de cada paso son los de `ci.yml` o `e2e.yml`, tal cual, ejecutados con `bash -e` como GitHub. De cada acción de GitHub se toma el equivalente local:

| En ci.yml | Aquí |
|---|---|
| `actions/checkout` | Copia de trabajo: los archivos que Git versionaría más los nuevos sin ignorar (incluye cambios sin confirmar; nada de `node_modules`, `bin`, `obj` ni `.semilla.local.json`), o `git archive` con `--rama` |
| `setup-dotnet` `10.0.x` | `mcr.microsoft.com/dotnet/sdk:10.0-noble` (Ubuntu 24.04, como el runner) |
| `setup-node` `22` | `node:22-bookworm` |
| `pnpm/action-setup` | `npm install -g pnpm@<packageManager de package.json>` |
| `actions/cache` y `cache: pnpm` | Volúmenes de Docker (`validasoft-cilocal-*`) con NuGet y la tienda de pnpm, compartidos entre corridas |
| `upload-artifact` | Se copia a la carpeta de salida |
| `docker compose up postgres mailpit minio` | El mismo comando con un proyecto de compose único por corrida y **sin puertos del host** |
| Playwright en `mcr.microsoft.com/playwright:v<versión de e2e/package.json>-noble` | El mismo `docker run` del flujo, con `--network host` reemplazado por la red de la API |

- **backend** (SDK .NET 10): `dotnet restore`, `build` en Release con advertencias como error, `test` de dominio, SDK, adaptador e integración, `pack` de los SDK y el informe de vulnerabilidades, igual que el flujo. Testcontainers dentro del contenedor usa el socket de Docker de la máquina (*Docker outside of Docker*): se monta `/var/run/docker.sock`; PostgreSQL, MinIO y Ryuk de las pruebas de integración corren como contenedores hermanos con puertos aleatorios del anfitrión y se alcanzan por `host.docker.internal` (`TESTCONTAINERS_HOST_OVERRIDE`; sin eso Ryuk no conecta y todas las pruebas de integración fallan con «Initialization has been cancelled»).
- **frontend** (Node 22): `pnpm install --frozen-lockfile`, `lint`, `typecheck`, `test`, `build` y el paquete npm del SDK. Incluye `sincronia.test.mjs`.
- **e2e** (`e2e.yml`, bajo demanda): PostgreSQL, Mailpit y MinIO van en una **red Docker propia** (`validasoft-ci-<id>_default`), con nombres de servicio (`postgres`, `mailpit`, `minio`) y sin publicar puertos: no chocan con la API del usuario en :5080, Mailpit :8025, MinIO :9000 ni las bases que ya corren. La API (`node e2e/soporte/api.mjs`, tal cual) corre en un contenedor con el SDK de .NET 10 y Node 22 juntos (`Dockerfile.dotnet-node`, dos imágenes oficiales) y ve los servicios por nombre con cuatro variables de entorno (`ConnectionStrings__Validasoft`, `Validasoft__Correo__Host`/`Puerto`, `Validasoft__Almacen__Endpoint`). Playwright corre en **su imagen oficial de la versión exacta de `e2e/package.json`**, compartiendo la red del contenedor de la API (el equivalente de `--network host` del runner: `localhost:5090` es la API y los webhooks de las pruebas la alcanzan), con `CI=true` (2 trabajadores, 1 reintento, como en GitHub) y `E2E_MAILPIT` apuntando al Mailpit de la red.
- **imagen** (solo con `--solo imagen`): construye `src/Validasoft.Api/Dockerfile` sin publicarlo y descarta el resultado.

## Varios agentes a la vez

Cada corrida tiene un identificador propio (`ci<hora><azar>`): contenedores `validasoft-ci-<id>-*` con etiquetas `ci-local.*`, proyecto de compose `validasoft-ci-<id>`, carpeta de trabajo `/cache/runs/<id>` dentro del volumen compartido y carpeta de salida propia. Nada usa puertos del host. Los volúmenes de caché (NuGet y pnpm) se comparten: es seguro, solo acelera. Al terminar, o al interrumpir con Ctrl+C, se borra todo lo de la corrida. Si una corrida murió sin limpiar (se cerró la terminal, se cayó la máquina), `ci-local.sh --limpiar` quita los restos de las corridas cuyo proceso ya no existe y respeta las vivas.

Cada contenedor ve 4 CPU (`--cpus`): sin ese tope vitest y dotnet usan las 14 de la máquina, dejan sin CPU a los demás agentes y las pruebas de interfaz (que esperan 20 s) fallan por tiempo agotado.

## Que no se desincronice con ci.yml

- Los comandos, las versiones de .NET, Node y pnpm, la versión de Playwright, los servicios, las rutas de reportes y las condiciones de cada paso **se leen de `ci.yml` y `e2e.yml`**: cambiarlos allí cambia lo que corre aquí.
- Lo único fijo es el mapa «acción de GitHub → equivalente local» y la forma de tres pasos (compose, Playwright, artefactos). Si el flujo cambia de forma (un trabajo o una acción nueva, otra opción de `docker run`, un servicio nuevo que la API use por `localhost`), la corrida local termina con `SINCRONÍA con los flujos (ci.yml, e2e.yml): …` (código 2) y **`sincronia.test.mjs` falla en el CI y en `pnpm test`**, diciendo qué actualizar (`lib/plan.mjs`). Esa prueba también comprueba que la versión de .NET del flujo concuerde con `Directory.Build.props` y la de Node con `engines`, y que **la e2e siga fuera de `ci.yml`** (un trabajo `e2e` allí, o un `needs` que la espere, la hace fallar) y que `e2e.yml` solo se dispare con `workflow_dispatch`.
- Las imágenes siguen las etiquetas de `ci.yml` (`10.0.x` → `10.0-noble`, `22` → `22-bookworm`, Playwright exacta), tan «flotantes» como `setup-dotnet` y `setup-node` en el runner. Las que se descargan son solo las oficiales de Microsoft (`mcr.microsoft.com/dotnet/sdk`, `mcr.microsoft.com/playwright`) y de Docker Hub (`node`, y las que ya usa el compose del repositorio).

## Diferencias con el runner (las que quedan)

- El runner de GitHub trae muchas herramientas preinstaladas (Python, Java, `jq`…); aquí los contenedores solo traen lo de las imágenes oficiales. Si algo del repositorio dependiera de una de ellas, fallará aquí y en un runner «limpio», que es lo que se quiere saber.
- El volumen de trabajo vive en el sistema de archivos de Linux de Docker (rápido), no en el disco de Windows: la copia de trabajo se transfiere por un tar y los reportes se copian de vuelta.
- La API del e2e ve los servicios por nombre y no por `localhost`; es la única variable de entorno que cambia y la prueba de sincronía vigila que no queden otras.
- Los topes de tiempo de `ci.yml` se avisan en el resumen y solo cortan la corrida al doble (la máquina se comparte con otros agentes).
- La máquina de desarrollo suele estar cargada con otras corridas: una prueba de interfaz con espera corta (1 s) o de integración con tiempos justos puede dar un rojo que se va al repetir solo ese trabajo. Si pasa, repite con `--solo` ese trabajo antes de investigar; si vuelve a fallar, es real.

## Cuánto tarda

Medido el 2026-11-11 sobre `origin/main` (`7413fbf`), un trabajo tras otro, con las cachés de NuGet y de pnpm ya llenas y la máquina compartida con otros agentes (es el resultado de `ci-local.sh --rama origin/main`, todo en verde):

| Trabajo | Tiempo | Desglose |
|---|---|---|
| frontend | 16 min 02 s | instalar 4 s · lint 5 min 10 s · tipos 1 min 01 s · pruebas 7 min 39 s (1096 pruebas en 158 archivos) · build 1 min 58 s · paquete npm 1 s |
| backend | 9 min 52 s | restore 19 s · build 1 min 17 s · pruebas 7 min 46 s (1232 pruebas en 4 proyectos) · pack 7 s · vulnerables 19 s |
| e2e (bajo demanda) | 16 min 40 s | servicios 6 s · instalar 3 s · compilar las apps y publicar la API 1 min 36 s · Playwright 14 min 47 s (106 pruebas) |
| imagen (opcional, `--solo imagen`) | 3 min 20 s | construir `src/Validasoft.Api/Dockerfile` sin publicarlo (medido aparte) |
| **Todo seguido (frontend, backend y e2e)** | **42 min 47 s** | |
| **Lo que corre por omisión (frontend y backend, sin e2e)** | **≈ 26 min** | 16 min 02 s + 9 min 52 s de la medición de arriba |

- La primera vez suma las descargas (imágenes oficiales, paquetes NuGet, tienda de pnpm): en la primera corrida el `restore` tardó 3 min 37 s en vez de 9-19 s.
- `--paralelo` acorta el total al del trabajo más largo, pero con tres trabajos de 4 CPU a la vez en una máquina compartida aparecen falsos rojos por tiempos (una prueba de integración de Sandbox con 500 y una de interfaz con espera de 1 s, ambas verdes al repetir el trabajo solo). Para la puerta de subida, uno tras otro.
- Para depurar un caso: `--solo e2e --e2e-args pruebas/x.spec.ts` (unos 2-4 min de preparación más lo que tarde el archivo) o `--solo frontend`.

## Archivos

- `ci-local.sh`, `ci-local.ps1`: entradas (comprueban Node y Docker y llaman a `ci-local.mjs`).
- `ci-local.mjs`: la orquestación (contenedores, redes, copia de trabajo, resumen, limpieza).
- `lib/yaml.mjs`: lector de YAML mínimo (sin dependencias). `lib/plan.mjs`: de `ci.yml` y `e2e.yml` al plan. `lib/fuente.mjs`: copia de trabajo (tar). `lib/docker.mjs`, `lib/resumen.mjs`.
- `Dockerfile.dotnet-node`: SDK de .NET + Node para la API del e2e.
- `sincronia.test.mjs`: la prueba que vigila que la réplica y los flujos (`ci.yml`, `e2e.yml`) coincidan (corre con `pnpm test`).
