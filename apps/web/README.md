# Frontend de Validasoft (H1: base y pantallas embebidas)

Workspace pnpm en la raíz del repo con una app y dos paquetes (docs/06 §1.1):

| Ruta | Qué es |
|---|---|
| `apps/web` | Pantallas de Validasoft **embebidas** en el sistema integrado (ID-3, ADR-0011): vínculos, áreas, usuarios y roles, sistemas integrados. |
| `packages/api-client` | Cliente generado con **orval** desde el snapshot `openapi/v1.json` + fetcher, errores tipados, lectura del token de sesión y reglas de acciones del vínculo. |
| `packages/ui` | Tema de marca (`createValidasoftTheme`) y componentes base: `ChipEstado`, `SelectorEmpresa`, `LineaDeTiempo`, `TablaConCursor` + `usePaginacionCursor`. |

Stack: React 18, TypeScript estricto (sin `any`), Vite 6, MUI v6 (solo `@mui/material`), TanStack Query v5, Zustand v5, React Router 7 (hash), i18next (es-CO), Vitest + Testing Library.

## Comandos (desde la raíz del repo)

```bash
pnpm install
pnpm lint          # ESLint strict-type-checked, --max-warnings 0
pnpm typecheck     # tsc en los tres proyectos
pnpm test          # Vitest en todos los proyectos
pnpm build         # build de apps/web (dist/ sin la demo ni el formulario de desarrollo)
pnpm generar:api   # regenera packages/api-client/src/generado desde el snapshot
pnpm --filter @validasoft/api-client openapi:actualizar   # baja un snapshot nuevo de la API levantada
```

## Probar contra la API local

```bash
docker compose -f infra/compose/compose.yaml up -d                       # PostgreSQL
ASPNETCORE_ENVIRONMENT=Development dotnet run --project src/Validasoft.Api   # API en :5080
pnpm semilla:dev                                                          # tenant, empresas, sistema DEMO, usuarios, vínculos
pnpm dev                                                                  # Vite en :5173 (si está ocupado: pnpm --filter @validasoft/web exec vite --port 5180)
```

- `http://localhost:5173/embed-demo.html`: página que hace de sistema integrado y aloja el iframe. Elige la persona (administradora, revisor, oficial de cumplimiento, auditor), la empresa sugerida y el tema; muestra los mensajes `postMessage`.
- `http://localhost:5173/`: fuera de un iframe y solo en desarrollo, un formulario abre la sesión (firma la aserción con la clave del sistema y la canjea). Se prellena con la semilla.

La semilla crea un tenant nuevo en cada ejecución y escribe las credenciales en `apps/web/.semilla.local.json` (ignorado por git); Vite lo sirve en `/__dev/semilla.json` solo en `vite dev`. La API no publica CORS: en desarrollo Vite hace de proxy de `/api` (`VALIDASOFT_API_URL`, por defecto `http://localhost:5080`).

## Sesión embebida (contrato `postMessage`, `src/sesion/puenteEmbed.ts`)

1. El sistema integrado abre `https://…/index.html#/` en un iframe.
2. Validasoft envía `{ tipo: 'validasoft:ready', version: 1 }` y `{ tipo: 'validasoft:sesion-requerida', motivo: 'inicial' }`.
3. El **backend** del sistema canjea API key + aserción `X-Usuario-Delegado` en `POST /api/v1/sesiones-usuario` y el contenedor responde `{ tipo: 'validasoft:sesion', token, empresaId? }`.
4. Mientras no tenga sesión, Validasoft repite `sesion-requerida` cada 5 s. Dos minutos antes de vencer (30 min) la pide con `por-vencer`; si vence o la API responde 401, con `vencida`. Tras `validasoft:cerrar-sesion` no pide otra.
5. Otros mensajes: `validasoft:resize { height }` (salida), `validasoft:tema { mode }` y `validasoft:cerrar-sesion` (entrada).

Solo se aceptan mensajes del propio origen y de `VITE_ORIGENES_CONTENEDOR` (lista separada por comas), y solo se responde al contenedor si su origen está en esa lista. El token vive en memoria; nunca en `localStorage`.

## Decisiones de implementación

- **Empresa en la URL** (`/e/:empresaId/…`) y en la clave de cada consulta (`conEmpresa`), además de `X-Empresa`: dos empresas nunca comparten caché (ADR-0008). Al cambiar de persona se vacía la caché.
- **Fetcher** (`packages/api-client/src/fetcher.ts`): `Authorization: Bearer`, `X-Empresa`, `X-Correlation-Id`; `Idempotency-Key` automática en los POST que crean (o la de la llamada, una por formulario abierto); `If-Match: "n"` con `VinculoDto.version` en las acciones; ProblemDetails → `ErrorApi` con `codigo`, `traceId`, `errores` por campo y atajos (`esConflictoDeVersion`, `esNoAutenticado`…); fallas de red → `ErrorDeRed`.
- **Permisos**: la UI lee los scopes del token (sin verificar la firma) solo para ocultar lo que no aplica; las acciones del vínculo replican las guardas del agregado (`accionesDisponibles`). La API decide siempre.
- **Contrato**: `openapi/transformar.ts` corrige el snapshot antes de generar (ver "Lo que falta") y **falla** si la API trae un endpoint que el cliente no conoce o deja de traer uno.
- `TablaConCursor` usa la tabla de MUI (no DataGrid Pro: la licencia aún no está en este repo).

## Lo que falta

**Pedidos al backend** (se compensan en el cliente, pero conviene corregirlos en la API):

1. Los POST que devuelven `Results.Created` se publican como `IResult` en OpenAPI y todas las respuestas aparecen como 200/404; tampoco hay `operationId` útiles. Declarar `Produces<T>(201)`, errores ProblemDetails y nombres de operación.
2. `estado`, `origen` y `rol` se publican como `string` y los enteros como `integer | string`.
3. `GET /vinculos/{id}/historial`: `datos` sale en PascalCase y con enums numéricos (`EstadoPrevio: 0`), distinto del resto de la API; el actor viene solo como id (falta su nombre).
4. `VinculoDto` no trae nombre ni identificación del tercero: el listado consulta `GET /terceros/{id}` por fila (con caché).
5. `POST /vinculos/{id}/invitaciones` respondió `envios: 3` cuando el vínculo pasaba de 1 a 2 envíos (posible conteo doble).
6. No hay CORS ni cabeceras CSP/`frame-ancestors` para servir el embed desde otro origen; tampoco un endpoint "quién soy" (nombre de la persona de la sesión).

**Frontend pendiente**: Storybook de `packages/ui` (decisión abierta 06-2); pruebas de accesibilidad con axe/Playwright; `en` cargado; zona horaria del tenant (hoy fija `America/Bogota`); Web Component envolvente (decisión abierta 06-1); SSE para invalidar consultas; crear vínculos y editar atributos desde la UI; pantallas de API keys, empresas y auditoría; `apps/public` y los paquetes `forms`, `i18n`, `domain`, `config` y `sdk-ts` de docs/06.
