# Validasoft (nombre de trabajo)

Sistema de validación documental para cualquier vínculo comercial (proveedor, cliente, empleado, arrendatario, paciente…), multi-tenant, independiente del ERP y consumible por API. Reemplaza la validación documental repartida hoy entre el SRM y el Portal de Proveedores.

> **Estado:** en desarrollo activo, con código en `main`: API .NET, app de la empresa (embebible), portal del tercero, consola de Plataforma, SDK .NET y TypeScript, certificados verificables, IA de revisión, listas restrictivas y manual. Qué está hecho, qué falta y cómo continuar: [docs/16](docs/16-estado-y-pendientes.md). El nombre «Validasoft» es provisional (se reemplaza buscando `Validasoft`).

## Qué hay aquí

| Ruta | Qué es |
|---|---|
| `src/Validasoft.Api` | Host de la API (.NET 10, Wolverine HTTP). Sirve las dos apps estáticas (`/app/` y `/`) y corre los procesos de fondo. |
| `src/Validasoft.Dominio`, `Aplicacion`, `Infraestructura`, `Contratos` | Dominio (agregados event-sourced con Marten), casos de uso y endpoints, adaptadores (PostgreSQL, S3/MinIO, correo, IA, verificadores) y DTO públicos. |
| `src/Validasoft.Sdk`, `src/Validasoft.Adaptadores.Portal` | SDK .NET para integradores y adaptador de origen del importador (Portal de Proveedores). |
| `apps/web`, `apps/public` | App de la empresa (React 19, se embebe en el sistema integrado) y portal del tercero (móvil primero). |
| `packages/api-client`, `packages/ui`, `packages/sdk` | Cliente generado desde OpenAPI, design system (marca «Anillo») y SDK TypeScript. |
| `tests/` | Pruebas de dominio, de integración (Testcontainers), del SDK y del adaptador; `tests/carga` (k6). |
| `e2e/` | Pruebas de punta a punta con Playwright y los recorridos que generan el manual (`e2e/manual`). |
| `infra/` | `compose/` (PostgreSQL, Mailpit, MinIO y la API en contenedor), `ci-local/` (réplica local del CI) y `sdk/`. |
| `docs/` | Diseño, decisiones y manual (abajo). `marca/` genera los recursos del logo. |

Stack: PostgreSQL 17, Marten + Wolverine, MinIO (S3), Docker; React 19, TypeScript estricto, Vite, MUI, TanStack Query. Detalle en [docs/04](docs/04-arquitectura-plataforma.md).

## Levantarlo en local

Requisitos: .NET SDK 10, Node ≥ 22, pnpm 10 y Docker.

```bash
pnpm install
docker compose -f infra/compose/compose.yaml up -d      # PostgreSQL (:5432), Mailpit (:8025) y MinIO (:9000, consola :9001)
ASPNETCORE_ENVIRONMENT=Development Validasoft__Correo__UrlPublica=http://localhost:5181 dotnet run --project src/Validasoft.Api   # API en :5080
pnpm semilla:dev                                         # datos de ejemplo: grupo, empresas, sistema DEMO, usuarios y flujo
pnpm dev                                                 # app de la empresa en :5173 (demo embebida en /embed-demo.html)
pnpm dev:public                                          # portal del tercero en :5181
```

Para correr la API en un contenedor en vez de con `dotnet run`: `docker compose -f infra/compose/compose.yaml --profile app up -d --build` (API en :5080). Los comandos de cada app y la demo embebida están en [apps/web/README.md](apps/web/README.md) y [apps/public/README.md](apps/public/README.md).

> **Claves:** las de `appsettings.Development.json` y del compose son solo para desarrollo local. Fuera de `Development` la API **se niega a arrancar** con ellas o sin las obligatorias; cada entorno genera las suyas ([cómo](docs/manual/guias/variables-de-entorno.md#claves-propias-del-entorno)).

## Pruebas

```bash
dotnet build Validasoft.slnx                    # TreatWarningsAsErrors: 0 advertencias
dotnet test Validasoft.slnx                     # dominio + integración (necesita Docker: levanta PostgreSQL y MinIO con Testcontainers)
dotnet test tests/Validasoft.Dominio.Tests --filter "FullyQualifiedName~CertificadoTests"   # solo lo que tocaste
pnpm lint && pnpm typecheck && pnpm test        # frontend: ESLint estricto, tsc y Vitest
pnpm e2e                                        # Playwright contra la API real; bajo demanda, no corre en cada push (decisión E2E-1, e2e/README.md)
infra/ci-local/ci-local.sh                      # réplica local del CI (backend y frontend en contenedores Linux; infra/ci-local/README.md)
```

El CI de GitHub (`.github/workflows/ci.yml`) corre backend, frontend y la imagen Docker en cada push; la e2e es el flujo manual `e2e.yml`.

## Documentación

Empieza por [docs/MAPA.md](docs/MAPA.md): orden de lectura por rol y mapa del desarrollo hito a hito.

- **Diseño** (`docs/00`–`docs/16`): [visión y alcance](docs/00-vision-y-alcance.md), [análisis del legado](docs/01-analisis-legado-srm-portal.md), [modelo documental compartido](docs/02-modelo-documental-compartido.md) (el corazón), [dominio y eventos](docs/03-dominio-y-eventos.md), [arquitectura](docs/04-arquitectura-plataforma.md), [API y webhooks](docs/05-api-contratos-y-webhooks.md), [frontend y UX](docs/06-frontend-y-ux.md), [seguridad y tenancy](docs/07-seguridad-identidad-y-tenancy.md), [integración y migración](docs/08-integracion-y-migracion.md), [plan de hitos](docs/09-plan-de-hitos-y-responsabilidades.md), [glosario](docs/10-glosario.md), [trazabilidad](docs/11-trazabilidad-por-hito.md), [plan de ejecución](docs/12-plan-de-ejecucion-con-claude.md), [flujo de trabajo con Claude Code](docs/13-flujo-de-trabajo-con-claude-code.md), [ficha maestra](docs/14-ficha-maestra-del-tercero.md), [Infolaft](docs/15-infolaft.md) y [estado y pendientes](docs/16-estado-y-pendientes.md).
- **Para integradores:** [guía de integración](docs/integracion.md) (alta del sistema, llaves, identidad delegada, embebido, webhooks, consulta de certificación, SDK) y el ejemplo `docs/ejemplos/IntegradorSrm`.
- **ADR:** [decisiones de arquitectura](docs/adr/) (monolito modular, Marten/Wolverine, bóveda global, versión única, consentimiento antes de compartir…).
- **Decisiones:** [registro de decisiones](docs/decisiones/registro.md) (la verdad sobre cada decisión, por sección e id) y [cuestionario](docs/decisiones/cuestionario.md) con las 97 preguntas del experto.
- **Manual:** [docs/manual](docs/manual/README.md), generado de recorridos reales y del contrato OpenAPI (`pnpm manual`); las guías (arquitectura, configuración, [variables de entorno](docs/manual/guias/variables-de-entorno.md)) se escriben en `e2e/manual/guias/`.
- **Marca:** [docs/marca.md](docs/marca.md).

## Reglas de oro del producto (resumen)

1. Un documento global tiene **una sola versión vigente por tercero**; las empresas la validan, no eligen versión.
2. Una empresa ve la bóveda solo cuando el tercero acepta su invitación (ADR-0009).
3. Las decisiones son **privadas por empresa**, también dentro de un mismo grupo (ADR-0008).
4. Los hechos sobre el archivo se calculan una vez y se comparten; el juicio es de cada empresa.
5. Sobre una versión solo se aprueba u observa; rechazar es del expediente.
6. Las personas también son terceros, con su propia bóveda (ADR-0010).
7. La vigencia tiene dos capas: la del documento y la exigencia de frescura de cada empresa.
8. Nada depende de un llamador externo: vencimientos, avisos y reintentos los ejecuta el propio sistema.
9. Sin contraseñas para el tercero, sin secretos en el navegador; el soporte de plataforma no ve datos salvo acceso temporal del tenant.

## Convenciones

Todo en español, incluido el lenguaje del dominio (`Expediente`, `Vínculo`, `Veredicto`). Los documentos de `docs/` abren con estado, fecha y responsable; los diagramas van en Mermaid; un cambio de decisión se registra como fila o ADR nuevo, no se reescribe la historia. El frontend usa **pnpm**, nunca npm.

Referencias del legado: SRM en `C:\Fuentes_Git\SRM` (rama `Develop`) y Portal de Proveedores en `C:\Fuentes_Git\PortalProveedores`.
