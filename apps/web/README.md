# Frontend de Validasoft (H1: pantallas embebidas, bandeja, expedientes y flujos)

Workspace pnpm en la raíz del repo con una app y dos paquetes (docs/06 §1.1):

| Ruta | Qué es |
|---|---|
| `apps/web` | Pantallas de Validasoft **embebidas** en el sistema integrado (ID-3, ADR-0011): bandeja de revisión, expedientes, vínculos y configuración (usuarios y roles, sistemas integrados, motivos, áreas y flujos aprobatorios). |
| `packages/api-client` | Cliente generado con **orval** desde el snapshot `openapi/v1.json` + fetcher, errores tipados, lectura del token de sesión y reglas de acciones del vínculo. |
| `packages/ui` | Design system (dirección **Registro**): `tokens.ts`, `createValidasoftTheme(modo, { app, colorEmpresa })`, mapa canónico de estados (`estados.ts`), paleta segura por empresa (`marcaEmpresa.ts`) y componentes: `ChipEstado`, `SelectorEmpresa`, `LineaDeTiempo`, `TablaConCursor`, `PaginacionCursor`, `BarraFiltros`/`FiltroMenu`/`CampoBusqueda`, `EstadoVacio`, `PaginaEstado`, `ErrorEnLinea`, `RejillaDatos`/`Dato`, `AvatarActor`, `ReferenciaExterna`, `Isotipo`. |

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

## Design system

La fuente es el artifact *Design System* de Validasoft (README, tokens y `codigo/`). `packages/ui/src/tokens.ts`, `tema.ts`, `estados.ts` y `marcaEmpresa.ts` son copia de su carpeta `codigo/`: si cambian allá, se copian de nuevo y no se editan a mano (salvo correcciones que también se reporten al artifact).

- Tema **claro por defecto** (el anfitrión puede imponer el oscuro con `validasoft:tema`); `app: 'web'` = densidad compacta.
- Estados solo con `ChipEstado` (contorno, ícono en el tono del estado); los rellenos de color quedan para alertas y avisos.
- Cabecera embebida de una fila (48 px): isotipo, empresa, pestañas *Vínculos* y *Configuración* y "Sesión desde SRM". Configuración tiene barra lateral separada en grupo (usuarios, sistemas) y empresa activa (áreas).
- Texto solo para lectores de pantalla: usa `visualmenteOculto` (en `sx` de MUI `width: 1` es 100 %; el `ChipEstado` de referencia del artifact trae ese error).

## Bandeja, expedientes y flujos (fase «Flujos aprobatorios», docs/05 §2.4.1)

- **Bandeja** (`/e/:empresa/bandeja`, `expedientes:leer`): una fila por expediente × área con folio, tercero, tipo, área, estado del área, aprobaciones x/N y desde cuándo; filtros de área y estado en la URL; cursor. Es la primera pantalla de quien lee expedientes. El administrador ve el botón de **aperturas pendientes** (FL-6) y abre cada expediente desde ahí.
- **Expediente** (`/e/:empresa/expedientes/:id`): etapas en secuencia y, en cada una, las áreas en paralelo con su estado, aprobaciones, quién falta, veredictos (superados y anulados atenuados) y acciones; última respuesta del tercero; datos (flujo y versión, política de devolución, separación de funciones); historial.
  - Veredicto: Aprobar u Observar con motivos tipificados (`GET /motivos`, filtrados por `aplicaA`) y observación para el tercero. Rechazo (terminal) solo en áreas con permiso, también con el expediente devuelto (FL-14).
  - Administrador (`expedientes:administrar`): reenviar en nombre del tercero, anular el expediente, aprobar en nombre de un área y anular un veredicto, siempre con justificación.
  - `features/expedientes/reglas.ts` replica las guardas del agregado para ocultar o explicar ("Devuelto al tercero: espera su respuesta", "Ya emitiste tu veredicto…"). Las decisiones van **sin `If-Match`** (las áreas deciden en paralelo y el servidor serializa, FL-12); ante 409/403 el diálogo muestra el mensaje del código, el expediente se recarga detrás y, si la decisión ya no aplica, el botón se deshabilita.
- **Vínculo**: tarjeta *Expediente* con el expediente en curso, el aviso de apertura pendiente y, para el administrador, **Abrir expediente** (flujo por defecto o uno publicado).
- **Configuración › Motivos** (grupo): los 8 de la plataforma (fijos) y los propios, con código, categoría, uso y texto al tercero.
- **Configuración › Flujos aprobatorios** (empresa, `flujos:*`): lista con versión publicada y borrador; flujo por defecto por tipo de vínculo; editor con etapas (subir, bajar, quitar), áreas en paralelo con aprobadores (1–10), puede rechazar, automática y SLA, política de devolución y separación de funciones; guardar borrador, publicar con confirmación y ver versiones anteriores en solo lectura. `features/flujos/editor.ts` valida como `DefinicionDeFlujo.Validar` (más aprobadores que miembros es aviso, no error).
- Pestañas y secciones se muestran según los scopes del token: `flujos:leer|escribir`, `expedientes:leer|revisar|administrar`.

## Decisiones de implementación

- **Empresa en la URL** (`/e/:empresaId/…`) y en la clave de cada consulta (`conEmpresa`), además de `X-Empresa`: dos empresas nunca comparten caché (ADR-0008). Al cambiar de persona se vacía la caché.
- **Fetcher** (`packages/api-client/src/fetcher.ts`): `Authorization: Bearer`, `X-Empresa`, `X-Correlation-Id`; `Idempotency-Key` automática en los POST que crean (o la de la llamada, una por formulario abierto); `If-Match: "n"` con `VinculoDto.version` en las acciones; ProblemDetails → `ErrorApi` con `codigo`, `traceId`, `errores` por campo y atajos (`esConflictoDeVersion`, `esNoAutenticado`…); fallas de red → `ErrorDeRed`.
- **Permisos**: la UI lee los scopes del token (sin verificar la firma) solo para ocultar lo que no aplica; las acciones del vínculo replican las guardas del agregado (`accionesDisponibles`). La API decide siempre.
- **Contrato**: `openapi/transformar.ts` corrige el snapshot antes de generar (ver "Lo que falta") y **falla** si la API trae un endpoint que el cliente no conoce o deja de traer uno.
- `TablaConCursor` usa la tabla de MUI (no DataGrid Pro: la licencia aún no está en este repo). Los overrides de DataGrid del design system (`codigo/temaDataGrid.ts`) se agregan cuando llegue la licencia.
- Acciones del vínculo: las seguras como botones; las destructivas en "Más acciones" con su consecuencia. Ante un 412 el diálogo explica el conflicto y "Recargar" trae la versión vigente sin perder lo escrito; anular pide escribir ANULAR.

## Lo que falta

**Pedidos al backend** (se compensan en el cliente, pero conviene corregirlos en la API):

1. Los POST que devuelven `Results.Created` se publican como `IResult` en OpenAPI y todas las respuestas aparecen como 200/404; tampoco hay `operationId` útiles. Declarar `Produces<T>(201)`, errores ProblemDetails y nombres de operación.
2. `estado`, `origen` y `rol` se publican como `string` y los enteros como `integer | string`.
3. Historial (vínculo y expediente): el actor viene solo como id; sin `usuarios:administrar` la UI no puede mostrar su nombre.
4. `VinculoDto` no trae nombre ni identificación del tercero: el listado consulta `GET /terceros/{id}` por fila (con caché).
5. No hay CORS ni cabeceras CSP/`frame-ancestors` para servir el embed desde otro origen; tampoco un endpoint "quién soy" (nombre de la persona de la sesión).
6. `TerceroResumenDto.identificacion` (expediente y bandeja) llega como la clave interna `CO|NIT|900123456`, sin dígito de verificación; la UI muestra "NIT 900123456" (`identificacionResumida`). Pedir la identificación legible con su DV, como en `TerceroDto`.
7. `VeredictoEmitidoDto.usuario`, `AreaExpedienteDto.miembrosPendientes` y `RechazoDto.usuario` son ids: un revisor (sin `usuarios:administrar`) ve "Persona del área" en vez del nombre. Pedir los nombres en el DTO o un resumen de usuarios legible con `expedientes:leer`.
8. Los POST que crean flujo, motivo y expediente siguen publicándose como `IResult` y los enums de expedientes y flujos como `string`; `transformar.ts` los corrige.

**Frontend pendiente**: aperturas pendientes probadas solo con datos vacíos (falta un tercero que acepte sin flujo publicado); lista de expedientes cerrados del vínculo (`GET /expedientes?vinculoId=`); el design system no trae estado para un área `Esperando` ni para versiones de flujo (`Borrador`/`Publicada`): `ChipEstadoArea` y `VersionesFlujo` los pintan aparte y conviene llevarlos al artifact; pantallas de prioridad 2 del design system (inicio, terceros, certificados, auditoría completa) y `apps/public` con la marca por empresa (`createValidasoftTheme(modo, { app: 'public', colorEmpresa })` ya la deriva); Storybook de `packages/ui` (decisión abierta 06-2); pruebas de accesibilidad con axe/Playwright; `en` cargado; zona horaria del tenant (hoy fija `America/Bogota`); Web Component envolvente (decisión abierta 06-1); SSE para invalidar consultas; crear vínculos y editar atributos desde la UI; pantallas de API keys, empresas y auditoría; `apps/public` y los paquetes `forms`, `i18n`, `domain`, `config` y `sdk-ts` de docs/06.
