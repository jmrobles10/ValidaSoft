# 06 — Frontend y experiencia de usuario

| Estado | Fecha | Responsable |
|---|---|---|
| Borrador | 2026-10-28 | Arquitectura |

**Propósito.** Definir las dos aplicaciones React de Validasoft (revisor/administrador y tercero/público), el design system derivado de la marca, el mapa de pantallas por actor, los flujos críticos UI↔API y los contratos de lectura que el backend debe proveer. El modelo de negocio que la UI debe hacer visible (una sola versión vigente por tercero, revalidación por empresa, vigencia en dos capas, revisión automática por niveles) está en `02-modelo-documental-compartido.md`.

**Relacionados:** [02-modelo-documental-compartido.md](02-modelo-documental-compartido.md) · [03-dominio-y-eventos.md](03-dominio-y-eventos.md) · [04-arquitectura-plataforma.md](04-arquitectura-plataforma.md) · [05-api-contratos-y-webhooks.md](05-api-contratos-y-webhooks.md) · [07-seguridad-identidad-y-tenancy.md](07-seguridad-identidad-y-tenancy.md) · [09-plan-de-hitos-y-responsabilidades.md](09-plan-de-hitos-y-responsabilidades.md) · [decisiones/registro.md](decisiones/registro.md) · ADR-0006, ADR-0008

> Actualizado el 2026-10-28 con las decisiones del registro: tenant = grupo con **empresas internas aisladas** (P-11-2, ADR-0008; la UI siempre trabaja "en una empresa"), panel `/plataforma` (P-15-2), formularios con esquema JSON (P-3-1), cuentas bancarias globales designadas por el tercero (P-3-3), identificación multipaís (P-1-1), límites de archivo (P-16-2/3), IA opcional y niveles (P-1-7, P-6-x).

Estados canónicos (dueños: documentos 03 y 05; esta UI los usa tal cual): `EstadoVersion { Recibida, EnAnalisis, Actual, Superada, Devuelta, Retirada }` · `EstadoRequisito { Pendiente, Cargado, EnRevision, Observado, Aprobado, Eximido, NoAplica, Vencido, Desactualizado }` · `Veredicto { Aprobado, Observado }` (+ `noAdmitirEstaVersion`; `Rechazado` solo existe para el expediente, B-7) · `EstadoVinculo { Invitado, EnValidacion, Activo, PorVencer, Vencido, Bloqueado, Desvinculado, Anulado }` · `EstadoExpediente { Abierto, EnRevision, Observado, Aprobado, Rechazado, Anulado, Caducado }` · `EstadoCertificado { Vigente, ConPendientes, Suspendido, Vencido, Revocado, Reemplazado }` · `EstadoAceptacion { AlDia, Desactualizada, PorVencer, Vencida }` · `EstadoCuentaDesignada { Propuesta, Aprobada, Rechazada, Reemplazada }`.

---

## 1. Arquitectura del frontend

### 1.1 Dos apps y paquetes compartidos en el monorepo

| Opción | Pros | Contras | Decisión |
|---|---|---|---|
| Una sola SPA con layouts público/privado | Un build, un deploy | El bundle del tercero (abre en 3G desde un celular) arrastraría DataGrid Pro, editor de flujos y cliente OIDC; código de administración servido a anónimos; CSP y PWA distintas en un mismo origen | Descartada |
| **`apps/web` + `apps/public` + `packages/*`** | Bundles separados (público < 250 KB gz), CSP/PWA independientes, licencia MUI X solo en `web`, design system y cliente API sin duplicar | Dos artefactos de despliegue | **Elegida** (ADR-0006) |

```
apps/
├─ web/                          # Revisor/Admin (por tenant, OIDC PKCE)
│  └─ src/
│     ├─ app/                    # providers, router, AppShell, guards, TenantProvider, EmpresaProvider
│     ├─ features/
│     │  ├─ inicio/              # {api,components,hooks,pages,store}
│     │  ├─ flujos/              # lista, detalle, editor de FlujoVersion
│     │  ├─ expedientes/         # bandeja, detalle, revisión
│     │  ├─ revalidaciones/      # BandejaRevalidaciones + ColaDeExcepciones
│     │  ├─ terceros/            # ficha 360 (por empresa) + personas relacionadas (ADR-0010)
│     │  ├─ certificados/
│     │  ├─ vencimientos/        # informe + campañas (H8)
│     │  ├─ configuracion/       # empresas del tenant, usuarios/roles por empresa, áreas, tipos-documento, formularios, revisión automática, plantillas, marca, api-keys, webhooks, integraciones
│     │  ├─ plataforma/          # rol Plataforma: tenants, empresas, catálogo canónico, esquemas, uso y costes de IA, soporte
│     │  └─ auditoria/
│     ├─ shared/                 # hooks genéricos, formato de fechas/montos, permisos
│     └─ main.tsx
└─ public/                       # Tercero + verificador + embed (PWA)
   └─ src/
      ├─ app/                    # router, PublicShell, EmbedShell
      ├─ features/
      │  ├─ enlace/              # landing del magic link, OTP
      │  ├─ boveda/              # mis documentos con estado por empresa (razón social)
      │  ├─ cuentas/             # mis cuentas bancarias y qué cuenta uso con cada empresa
      │  ├─ personas/            # mis personas relacionadas (representantes, socios, UBO) con su propia bóveda (ADR-0010)
      │  ├─ expediente/          # requisitos, subida, formularios, observaciones, ratificar
      │  ├─ vinculos/            # mis empresas
      │  ├─ certificado/
      │  ├─ verificar/           # ruta UI /verificar/:folio → API GET /public/v1/verificar/{folio}
      │  └─ embed/               # puente postMessage
      └─ main.tsx
packages/
├─ ui/            # tema MUI, tokens, componentes Validasoft, Storybook
├─ forms/         # FormularioDinamico: renderizador JSON Schema (@rjsf/mui) + widgets Validasoft + DiffCampos
├─ api-client/    # generado con orval desde /openapi/v1.json + fetcher ProblemDetails (X-Empresa; X-Tenant solo en /plataforma/*)
├─ sdk-ts/        # @validasoft/sdk para integradores (doc 08)
├─ i18n/          # es-CO base, en; claves tipadas
├─ domain/        # enums/estados compartidos (EstadoRequisito, EstadoExpediente, NivelRevision…) y helpers puros
└─ config/        # eslint, tsconfig, vitest presets
```

Convención por feature: `api/` (hooks TanStack sobre el cliente generado + query keys), `components/`, `hooks/`, `pages/` (lazy), `store/` (Zustand solo si hay estado UI transversal). Se abandona el patrón `DTO/service/store/view` del SRM: los DTOs los genera orval y el "service" es el hook de query.

### 1.2 Decisiones transversales

| Tema | Decisión | Por qué |
|---|---|---|
| Router | React Router 8 (data mode), `lazy` por ruta, loaders que precargan queries | Code splitting natural, `errorElement` por rama |
| Tenant y empresa | `web`: prefijo `/t/{tenant}/e/{empresa}/…` **solo en la SPA** (T-5), resuelto por `TenantProvider` + `EmpresaProvider` → cabecera `X-Empresa` en la API (el tenant va implícito en el token; `X-Tenant` solo lo envía el rol `Plataforma` en `/plataforma/*`); el token lleva el claim `tenant` y las empresas con rol; el backend verifica coherencia y responde **404** a recursos de otra empresa o tenant (T-4). **Toda pantalla de operación trabaja en una empresa** (razón social); un `SelectorEmpresa` en el AppShell cambia de empresa y recarga las queries; las empresas del grupo **no ven** las decisiones de las otras (ADR-0008) | Aislamiento legal por razón social con administración unificada del grupo |
| Formularios | Renderizador JSON Schema `@rjsf/mui` (v6, compatible con MUI v6) con tema y widgets Validasoft (identificación multipaís, cuenta bancaria, fecha, archivo-soporte); el esquema llega del backend como `esquemaCanonico + extensionTenant` ya fusionados; los `uiSchema` viven en `packages/forms` por tipo canónico. Descartado el renderizador propio: RJSF cubre validación, condicionales y arrays; si un widget no existe se implementa como *custom widget*, no un motor nuevo | P-3-1/3-2: un solo motor para archivos y datos |
| Identificación | `SelectorIdentificacion`: país → tipos de identificación de ese país (`GET /catalogos/tipos-identificacion?pais=`) → validador y normalización en cliente (solo dígitos, DV aparte) y en servidor | P-1-1 multipaís |
| Auth `web` | OIDC Authorization Code + PKCE con `oidc-client-ts` **contra el OpenIddict de Validasoft** (`/connect/authorize`); 2FA lo resuelve el servidor; tokens solo en memoria + silent renew | Sin IdP externo; la autorización efectiva siempre la decide el backend |
| Auth `public` | `/e/{token}` canjea el magic link por sesión corta (cookie HttpOnly SameSite=Lax); OTP por correo si la política del flujo lo exige; el token del enlace nunca se guarda en el navegador | Sin contraseñas (regla 8 del legado) |
| Estado servidor | TanStack Query v5; `staleTime` 30 s en bandejas; invalidación por SSE (`/api/v1/eventos/stream`, `/public/v1/expedientes/{id}/stream`, `/public/v1/boveda/stream`) | El backend es event-sourced: la UI reacciona a eventos, no hace polling |
| Estado UI | Zustand con `persist` solo para tema, filtros guardados y layout del visor | Nada del dominio vive en Zustand |
| Cliente API | orval (`react-query` mode) desde `openapi.json`; fetcher propio; `zod` opcional en dev | Tipado end-to-end; CI falla si el contrato cambia |
| Errores | ProblemDetails RFC 9457 → `ErrorMapper` traduce `codigo` a mensaje con siguiente paso; `errores[]` por campo | Nunca códigos crudos al usuario |
| i18n | i18next, namespaces por feature, `Intl` para fechas/números, **`es-CO` en el MVP, `en` preparado** (claves tipadas; sin traducción cargada hasta que Producto lo pida, P-16-1) | Proveedores extranjeros |
| Fechas | DayJS `utc` + `timezone` del tenant; el backend entrega ISO-8601 UTC | Evita fechas sin zona del SRM |
| Tipografía | Geist y Geist Mono self-hosted (`@fontsource-variable/geist`) | Sin CDN, cumple CSP |
| Iconos | Lucide React (trazo 1.75) | Coherente con la "V" redondeada; Material Icons descartado |
| Accesibilidad | **WCAG 2.1 AA en `apps/public`; AA en `apps/web` con excepciones documentadas** (DataGrid Pro) (P-16-5): contrastes verificados en dark/light, foco visible azul, `aria-live` en subidas, `eslint-plugin-jsx-a11y`, axe en Playwright | Terceros con lectores de pantalla y celulares viejos; clientes del Estado |
| Subida de archivos | Límite **20 MB, PDF/JPG/PNG/HEIC, 50 páginas** (P-16-2); HEIC→JPEG y varias fotos→un PDF **en el cliente** (`pdf-lib` + `heic2any`), con recorte y rotación básicos (P-16-3); sha256 en cliente para detectar duplicado antes de subir | Tercero desde el celular |
| Aceptaciones | T&C y autorización de datos (incluida la cláusula de IA externa, P-6-5): clic + OTP + registro de versión del texto, fecha, IP (`AceptacionConEvidencia`); trazo en pantalla opcional en H8 (P-16-4) | Evidencia de firma electrónica simple |
| Marca por tenant/empresa | Logo y colores de la **empresa** en `public` y en correos desde el MVP; dominio propio en H8 (P-11-3); contacto de soporte de la empresa visible en el pie de todas las pantallas del tercero (P-15-4) | El tercero reconoce a quien le pide los documentos |
| Rendimiento | `pdfjs-dist` en chunk lazy con worker propio; DataGrid Pro solo en `web`; imágenes vía URL prefirmada con miniaturas del backend | El público no descarga pdf.js hasta abrir un visor |
| PWA | Solo `apps/public`: `vite-plugin-pwa`, cola offline de subidas (IndexedDB + Background Sync) | Tercero en obra sin señal |
| Tests | Vitest + Testing Library (MSW solo en `import.meta.env.DEV` y tests), Playwright, Storybook con `@storybook/test` | — |
| Lint | ESLint flat + typescript-eslint `strict-type-checked`, `no-explicit-any: error`, `exhaustive-deps`, boundaries entre features, regla que prohíbe importar `mocks/` fuera de tests | Evita el dashboard con datos fijos del SRM |

---

## 2. Design system Validasoft (`packages/ui`)

### 2.1 Tokens

| Token | Dark (por defecto) | Light |
|---|---|---|
| `bg.default` | #060A14 | #FAFBFD |
| `bg.surface1 / surface2` | #0B1120 / #111A2E | #FFFFFF / #F1F4FA |
| `primary` (en curso) | #3D6BFF | #2F55D9 (AA sobre blanco) |
| `primary.deep` | #0E2A6B | #0E2A6B |
| `success` (aprobado/validado) | #22D38A | #159C64 |
| `warning` (observado / desactualizado / por vencer) | #F5B544 | #B77A0C |
| `error` (rechazado / vencido) | #FF5C6C | #D6303F |
| `info` (automático / IA) | #7BA7FF | #3F63C9 |
| `text.primary / secondary / disabled` | #EEF1F7 / #8791A7 / #5A6480 | #0A0F1C / #4C5670 / #8791A7 |
| `gradient.brand` | `linear-gradient(135deg,#3D6BFF,#22D38A)` | igual |
| Radios | `lg` 18 (tarjetas, modales) · `md` 12 (inputs, botones) · `sm` 9 (chips, celdas) |
| Espaciado | base 8; densidad `comfortable` (público) / `compact` (bandejas) |
| Tipografía | Geist: h1 32/600 tracking −0.02em, h2 24/600, h3 18/600, body 14/400, caption 12/500; Geist Mono 13 para folio, hash, fechas en tablas |

`createValidasoftTheme(mode)` extiende `createTheme` con `palette.estado.*` (module augmentation) y overrides (botones sin mayúsculas, chips radio 9, DataGrid sin bordes verticales).

### 2.2 Componentes propios

| Componente | Props clave | Notas |
|---|---|---|
| `ChipEstado` | `estado: EstadoRequisito \| EstadoExpediente \| EstadoVinculo \| EstadoCertificado \| EstadoVersion \| EstadoAceptacion`, `fecha?`, `automatico?`, `bloqueante?` | Mapa único estado→color/icono/texto (incluye `Desactualizado`, `Vencido`, `Bloqueado`, `ConPendientes` informativo/bloqueante, `Suspendido`, `Devuelta`; sin `Rechazado` para requisitos, B-7); tooltip con fecha en mono ("Venció el 28/09/2026"); ícono de chispa cuando lo decidió `Sistema (IA)` |
| `TarjetaPersonaRelacionada` | `relacion: RelacionEntreTercerosDto`, `documentos[]` | Representante/socio/UBO/miembro con su identificación, rol, principal, vigencia y estado de su cédula/antecedentes (su propia bóveda, ADR-0010) |
| `SelectorEmpresa` | `empresas[]{id,nombre,logo}`, `actual` | En el AppShell; cambia `X-Empresa`, invalida queries, persiste la última elegida por usuario |
| `SelectorIdentificacion` | `pais`, `tipo`, `numero`, `onChange` | País → tipos válidos → máscara, normalización y DV; mensaje de error con el formato esperado |
| `FormularioDinamico` (`packages/forms`) | `esquema`, `uiSchema`, `valor`, `camposCriticos[]`, `modo:'editar'\|'ver'\|'diff'` | RJSF + MUI; marca en el formulario qué campos son críticos ("si cambias esto, todas tus empresas revalidan"); soporte adjunto por campo cuando el esquema lo pide |
| `DiffCampos` | `anterior`, `actual`, `camposCriticos[]` | Diff de campos de una versión de formulario o de los campos clave extraídos de un archivo; críticos en rojo, menores en ámbar |
| `TarjetaCuentaBancaria` (public) | `cuenta`, `certificacion`, `empresasQueLaUsan[]` | Banco, tipo, número enmascarado, estado de la certificación, y con qué empresas está designada |
| `AceptacionConEvidencia` (public) | `texto{version,html}`, `requiereOtp`, `onAceptar` | Clic + OTP; muestra "aceptaste la versión X el …" después |
| `BarraProgresoEtapas` | `etapas: {nombre, estado:'ok'\|'en_curso'\|'observado'\|'pendiente'}[]` | N-aria (flujos configurables); `aria-label` con la etapa actual |
| `Folio` | `valor`, `copiable` | Geist Mono, copiar, truncado medio en móvil |
| `TarjetaCertificado` | `certificado: CertificadoPublico`, `variante` | Borde degradado 1.5 px, pill "Verificado", QR (`qrcode`), huella mono, aviso "revalidación pendiente" si aplica |
| `TarjetaDocumento` | `requisito: RequisitoResumen` | **Obligatorio**: versión actual (n, fecha de carga), vigencia para esta empresa (`vigenteHasta`, `motivoVigencia`), estado de aceptación (`aceptada vN`, `desactualizada desde`), hechos IA resumidos |
| `TarjetaDocumentoBoveda` (public) | `documento: DocumentoBoveda` | Versión **vigente** del tercero + fila por **empresa (razón social)** con una sola frase de lo que tiene y lo que falta (RT-5: «Tiene la versión 7, la vigente.», «Revisando la versión 7.», «Aprobó la versión 8, que retiraste. Debe revisar la versión 7: revalidación abierta.»), su propio vencimiento solo si difiere del de la cabecera y con su porqué; «Vigente» es la única palabra para la versión que vale hoy (también en el listado de versiones); botón "Pedir revisión humana" cuando la decisión fue automática (P-6-8) |
| `VisorDocumento` | `fuente`, `comparar?`, `anotaciones?` | PDF/imagen, zoom, páginas, lado a lado sincronizado con resaltado de `camposClave` cambiados |
| `LineaDeTiempo` | `eventos: EventoTimeline[]` | Agrupa por día; actor humano, tercero o `Sistema (IA)`; antes/después |
| `PanelVeredicto` | `onAprobar(observacion?)`, `onObservar(motivoId, comentario, camposAfectados?, noAdmitirEstaVersion)`, `motivos`, `sugerencia?` | Sobre una versión solo **Aprobar** u **Observar** (con la casilla "no volver a admitir esta versión en este vínculo"); comentario obligatorio al observar; preview del texto que verá el tercero; en `Asistido` muestra la sugerencia y el botón "Confirmar". **Rechazar el expediente** es una acción aparte del área con `PuedeRechazarExpediente` (`RechazarExpedienteDto`, motivos + justificación, terminal) fuera de este panel (B-7) |
| `PanelIA` | `hechos: HechosDeVersion \| null`, `comparacion: Comparacion`, `nivel: NivelRevision`, `decisionAutomatica?` | Tipo detectado, legible, confianza por campo, **diff con la versión anterior** (`DiffCampos`), coherencia formulario↔soporte, señales de manipulación, frescura vs regla de la empresa, regla aplicada si hubo decisión automática, botón "Revertir". Si `hechos` es `null` (IA no disponible o sin consentimiento) muestra "Sin análisis automático: revisión manual" y solo los hechos básicos (hash, páginas, antivirus) |
| `Dropzone` | `multiple`, `captura:'camara'\|'archivo'`, `convertirAPdf`, `limites{mb:20,paginas:50,formatos}` | Móvil: "Tomar foto", varias fotos → PDF (`pdf-lib`), HEIC→JPEG, recorte/rotación, progreso, reintento, detección de duplicado por hash antes de subir; rechaza en cliente lo que supere los límites con mensaje claro |
| `KpiCard` | `titulo`, `valor`, `delta?`, `cargando` | Valor en mono; skeleton; sin valores por defecto |
| `TablaExpedientes` | `columnasArea: AreaFlujo[]`, `rows` | DataGrid Pro; columnas de área generadas; pinned folio; selección masiva |
| `Bandeja` | `filtros`, `filtrosGuardados`, `vistas` | Filtros en URL (`?estado=&area=&mios=1&nivel=automatico`) + guardados en Zustand |
| `AvisoConsolidado` (public) | `documento`, `empresas[]{nombre, venceEl}` | "Renueva antes del 15/11 y quedas al día con 3 empresas" |

### 2.3 Estados vacíos, carga y error (tono de voz)

| Estado | Patrón | Ejemplo |
|---|---|---|
| Vacío | Icono lineal + frase + acción | "No tienes revalidaciones pendientes en Legal." |
| Carga | Skeleton con la forma del contenido (tabla, tarjetas, formulario…); nunca el texto «Cargando…» ni un spinner de página o de sección (UI-1) | — |
| Error recuperable | Inline con reintento | "No pudimos cargar la bandeja. Reintentar." |
| Error de acción | Snackbar con siguiente paso | "No se notificó al tercero por correo. Copia el enlace y envíalo tú." |
| Decisión automática | Banner informativo | "Aprobado automáticamente: sin cambios frente a la versión del 02/03. Puedes revertirlo." |
| Sin permiso | Página 403 propia | "Esta sección es de Configuración. Pide acceso a un administrador." |

### 2.4 Reglas visuales de toda la aplicación (UI-1…UI-9)

Normas del proyecto para `apps/web`, `apps/public` y el tema (`packages/ui/src/tema.ts`); detalle y motivos en el [registro](decisiones/registro.md#5-novotricies-reglas-visuales-de-toda-la-aplicación-2026-11-10). Las comprueban `packages/ui/src/pulido.test.tsx` y `e2e/pruebas/pulido-visual.spec.ts`.

1. **Cargando = esqueleto** (`esqueletos.tsx`: tarjetas, formulario, tabla, detalle, historial, opciones de autocompletar). Nunca «Cargando…» ni spinners de página, sección o lista; el único spinner es el de `IconoCarga`, dentro de un botón que guarda.
2. **Botones compactos**: `medium` por omisión (web 32 px, tercero 36 en escritorio y 40 en el celular, con el área táctil de 44 px en un pseudoelemento). `large` solo para la acción principal; `small` en avisos, filas y tarjetas. Sin `minHeight` de 44/48 en un botón.
3. **Selects y autocompletar** con el ícono dentro del campo y los textos de MUI en español (`esES` + «Abrir», «Cerrar», «Limpiar», «Sin resultados»).
4. **Nada cuadrado suelto**: acordeones y papeles con el radio de las tarjetas (12 px) y sin la línea superior de MUI.
5. **Sin layouts volcados a la izquierda**: `DosColumnas` desde `lg` (Plan de IA, Nuevo grupo); a 1920×1080 una pantalla de configuración cabe sin desplazarse.
6. **Avisos de documento más nuevo**: uno por documento, con las secciones que afecta; en el wizard, un bloque por documento dentro de un solo aviso (ver 9).
7. **Ningún control nativo del navegador** (fecha, hora, mes, número con flechas, `select`): fecha con `CampoFecha` (`@mui/x-date-pickers` + `dayjs`, `dd/mm/aaaa`, calendario en español, valor ISO `aaaa-mm-dd`), entero con `CampoNumero` (solo dígitos) y listas con el `Select`/`Autocomplete` de MUI. Lo vigila `pulido.test.tsx`.
8. **Los campos cortos no ocupan todo el ancho**: un año, una fecha, un código, un porcentaje o un NIT van en columnas (dos por fila) o con ancho acotado; texto largo, listas y sí/no ocupan la fila.
9. **Aviso de documentos más nuevos**: cada documento es un bloque con su borde (nombre y versión, estado en chips —«No pudimos leerlo»—, secciones que afecta como chips y la acción pequeña a la derecha) y el subtítulo del aviso cabe en una línea.
10. **Estados claros y sin contradicciones** (RT-5): una pantalla no muestra dos verdades a la vez (la versión del tercero y la de cada empresa se explican en la misma frase) y un solo término por concepto («Vigente», no «Actual»); textos en lenguaje llano que dicen qué pasó y qué sigue.
11. **Acciones con consecuencias se avisan antes** (RT-1): si lo que hace el tercero dispara procesos en las empresas (retirar una versión que una empresa aprobó o revisa), el diálogo de confirmación lo cuenta —a quién y qué pasará— antes de pedir el motivo, y ofrece la alternativa («Subir una versión corregida»).

---

## 3. Mapa de pantallas

### 3.1 Revisor / Administrador (`apps/web`, prefijo `/t/{tenant}/e/{empresa}`)

Todas las rutas de operación están **dentro de una empresa**; las de `/configuracion/tenant/*` y `/plataforma/*` son del grupo o de la plataforma.

| Ruta | Propósito | Datos (proyección) | Acciones |
|---|---|---|---|
| `/inicio` | KPIs de la empresa (y agregado del grupo sin detalle de decisiones), mis pendientes por área, **revalidaciones pendientes**, excepciones de la revisión automática, vencimientos próximos, ahorro por revisión automática, consumo de IA del mes | `KpisTenant` (filtrada por empresa), `MisPendientes`, `BandejaRevalidaciones?resumen`, `ColaDeExcepciones?resumen`, `VencimientosProximos?dias=30`, `UsoTenant?mes=` | Ir a expediente, notificar |
| `/flujos` | Lista de flujos con versión publicada y expedientes activos | `FlujosResumen[]` | Crear, duplicar, archivar |
| `/flujos/:id` | KPIs y expedientes del flujo | `KpisFlujo`, `BandejaRevision?flujoId=` | "Editar flujo", "Solicitar documentos" |
| `/flujos/:id/editor` | Editor: reglas de documentos (condiciones), áreas/etapas/SLA, `ReglaFrescura` y `NivelRevision` por tipo, verificaciones externas, plantilla de certificado; pestaña **"Reglas de selección"** (orden, condición con el mismo editor de condiciones, flujo destino, flujo por defecto por tipo de vínculo, simulador "¿qué flujo le tocaría a…?"); pestaña "Vista del tercero" (iframe de `public` con `?preview=`) | `FlujoDefinicion`, `ReglaSeleccionFlujoDto[]` (`GET /flujos/reglas-seleccion`) | Guardar borrador, publicar versión, **"Aplicar a expedientes abiertos"** (primero `?simular=true` → diálogo con `ImpactoAplicacionDto`: N expedientes, requisitos que se agregan/quitan, veredictos conservados → confirmar con justificación) |
| `/expedientes` | Bandeja con columnas por área; pestañas Vinculación / Renovaciones / **Revalidaciones** / Todos; filtro "decididos por IA" | `BandejaRevision`, `AreasFlujo` | Recordatorio, asignar revisor, exportar |
| `/expedientes/:id` | Cabecera (folio `-E-`, estado, progreso, flujo y versión con origen de la selección), requisitos (incluidos los "de la persona relacionada con rol X"), visor + `PanelIA` + `PanelVeredicto`, pestañas Áreas / Timeline / Observaciones / Historial | `ExpedienteDetalle`, `RequisitoDetalle`, `TimelineExpediente` | Aprobar u observar (con "no admitir esta versión"), **Rechazar expediente** (acción del área con permiso, terminal), confirmar sugerencia, revertir automático, comparar versiones, **"Cambiar flujo"** (solo Administrador, solo `Abierto`/`Observado`: selector de flujo publicado + justificación obligatoria + vista previa de requisitos que se conservan/agregan/quitan; `POST /expedientes/{id}/cambiar-flujo`) |
| `/revalidaciones` | Bandeja específica: vínculo × tipo con requisito `Desactualizado`, versión anterior aceptada vs actual, diff IA, nivel de revisión, días restantes del plazo | `BandejaRevalidaciones` | Aprobar en lote (si `SinCambios`), abrir expediente |
| `/excepciones` | Lo que la revisión automática no pudo decidir (campos críticos, baja confianza, manipulación), muestras de auditoría y **solicitudes de revisión humana** del tercero | `ColaDeExcepciones` | Decidir, revertir, responder al tercero |
| `/terceros`, `/terceros/:id` | Ficha 360: identificación (país/tipo/número), **personas relacionadas** (representantes, socios, UBO, miembros con su propia bóveda y el estado de su cédula/antecedentes, ADR-0010), vínculos **con esta empresa** y existencia de vínculos con otras empresas del grupo (sin sus decisiones), documentos actuales con vigencia **para esta empresa**, histórico de aceptaciones, cuenta designada, bloqueo | `Tercero360` | Nueva solicitud, reenviar enlace, bloquear/desbloquear (motivo; bloquear **suspende** el certificado y desbloquear lo restablece, B-5), aprobar cambio de cuenta designada (4 ojos), registrar relación |
| `/vinculos/:vinculoId/ficha` | **Ficha aceptada (FD-3).** Lo que **esta** empresa aceptó del tercero en el vínculo, sección por sección, con su versión, su expediente y su soporte (y los datos a pedido), separado de lo que el tercero guardó después: un solo aviso agrupado dice cuántas secciones tienen una versión nueva pendiente de revisión y, en cada una, un bloque propio con borde muestra qué cambió frente a lo aceptado, el soporte (o el aviso «Cambio sin soporte», crítico si toca la identidad o al representante legal) y el enlace a la revalidación. A la derecha, el resumen. Solo lectura. Entra por la tarjeta «Ficha aceptada» del detalle del vínculo. Términos: «Aceptada» y «Versión nueva pendiente de revisión»; el documento que respalda es el «soporte» |
| `/certificados`, `/certificados/:folio` | Lista y detalle; `EstadoCertificado` (`Vigente`, `ConPendientes` informativo o bloqueante, `Suspendido`, `Vencido`, `Revocado`, `Reemplazado`) con historial de emisiones por vínculo (folio `-C-…-{n}`) | `Certificados`, `CertificadoDetalle` | Revocar (motivo), reemitir, copiar enlace |
| `/vencimientos` | Informe por documento y vínculo (`Vencido` / `PorVencer` / vigente) con `motivoVigencia`, Excel, notificación; **campañas de renovación** (H8) | `VencimientosProximos`, `Campanas` | Notificar seleccionados, crear campaña |
| `/configuracion/empresa/*` | Datos de **esta** empresa: NIT, logo, colores, `Reply-To`, contacto de soporte, prefijo de folio; áreas y revisores; **tipos-documento (overrides por empresa, B-4)**: `reglaFrescura`, `nivelRevision`, `diasRevalidacion`, `politicaDesactualizada`, `aprobadoresRequeridos`, `porcentajeMuestreo`, `camposCuyoCambioExigeHumano` que sobreescriben los defaults del tenant (y a su vez los sobreescribe la regla del flujo); **delegaciones** (H8); separación de funciones y conflicto de intereses (H8) | respectivas | CRUD |
| `/configuracion/tenant/*` | Del grupo: **empresas** (alta/baja de razones sociales), usuarios y roles **por empresa o para todo el tenant**, tipos-documento (defaults del tenant: mapeo canónico, restricción de ámbito, `reglaFrescura`, `diasAviso`, `diasGracia`, `diasRevalidacion`, `politicaDesactualizada`), **formularios** (extensión de campos privados), **revisión automática** (umbrales, muestreo, presupuesto de decisiones y consumo; el nivel vive en el tipo de documento), motivos, plantillas (cabecera/pie en MVP; Liquid en H8), api-keys (alcance por empresa, sandbox), webhooks (entregas y reintento), integraciones, plan y uso, **accesos de soporte** (conceder a Plataforma acceso temporal con alcance y horas; lista de concesiones vigentes y auditoría, B-6) | respectivas | CRUD |
| `/informes/estado-expedientes`, `/informes/vinculos` (**implementadas**, IF-1) | Informes de la empresa en pestañas: **estado de expedientes** (filtros por fechas de apertura o cierre, estado, tipo, área, flujo, tipo de vínculo y tercero; resumen, tiempos por área y por expediente: carga, con el tercero, revisión, SLA) y **terceros y vínculos** (estado, certificado, expediente en curso, vencimientos, secciones por validar). Inicio enlaza al informe con su misma ventana | `GET /informes/estado-expedientes`, `GET /informes/vinculos` | Descargar Excel (los dos) y PDF (estado), con los mismos filtros |
| `/auditoria` | Registro unificado de **esta empresa** (y del tenant para Administrador/Auditor con alcance `*`) con pestañas por `tipo`: **Dominio** (timelines, decisiones automáticas y reversiones), **Configuración** (cambios de flujos, tipos, áreas, políticas, usuarios, API keys, webhooks con visor de **diff antes/después** por campo), **Identidad** (logins, 2FA, bloqueos, uso de API keys, token exchange, enlaces canjeados) y **Accesos** (quién descargó qué versión); filtros por entidad, `entidadId`, actor, fechas; detalle con `RegistroAuditoriaDto` | `GET /auditoria?tipo=…` (`PaginaDto<RegistroAuditoriaDto>`) | Exportar ZIP firmado (`POST /auditoria/exportar`, H8; en H1 solo consulta), copiar `correlationId` |
| (sin sesión) — **ingreso** | **Ingreso directo (ING-1…ING-3)**: fuera de un sistema integrado y sin sesión, `apps/web` muestra el ingreso: correo → código de 6 dígitos (seis casillas) → (elegir cuenta) → passkey cuando la cuenta la tiene o la exige, o «Entrar con una passkey» sin correo ni código. Ver §3.4 | `POST /ingreso/*` | Entrar |
| `/mi-cuenta` (también `/plataforma/mi-cuenta`) | **Mi cuenta (ING-3)**, solo con sesión directa: perfil (correo, grupo, roles, cómo entré), **passkeys** (agregar, renombrar, eliminar; la última no si el rol la exige) y **sesiones abiertas** (cerrar las de otros dispositivos o esta) | `GET /mi-cuenta`, `/mi-cuenta/passkeys`, `/mi-cuenta/sesiones` | Agregar passkey, cerrar sesión |
| `/configuracion/ingreso` | **Ingreso del grupo (ING-1)**: modos (`Delegado`, `Directo`, ambos), duración máxima e inactividad de la sesión directa y «exigir passkey a todas las personas»; advierte qué se cierra al apagar un modo | `GET/PUT /configuracion/ingreso` | Guardar |
| `/configuracion/usuarios` (ING-5) | Con el modo `Directo`: columna **Passkeys**, «Enviar una invitación por correo» al agregar a una persona, «Reenviar invitación» y **«Passkeys y sesiones»** (panel lateral para revocar passkeys y cerrar sesiones) | `GET/POST /usuarios`, `/usuarios/{id}/passkeys`, `/sesiones`, `/invitar` | Invitar, revocar, cerrar |
| `/plataforma/operadores` (ING-4) | **Operadores de Plataforma**: alta (correo y nombre), cómo entra cada uno, passkeys, desactivar y el panel de passkeys y sesiones; recuerda que la clave de arranque solo sirve para el primero | `GET/POST /plataforma/operadores` | Nuevo, desactivar, revocar |
| `/plataforma/*` (rol `Plataforma`) | Tenants (crear, región, plan, clave KMS), empresas de cada tenant, **catálogo canónico** de tipos de documento y esquemas de formulario (versionar, aprobar propuestas de tenants), catálogo de identificación por país, uso y costes de IA por tenant, dead letters y entregas fallidas, **accesos de soporte concedidos** (solo entra a un tenant con una concesión vigente del Administrador de ese tenant; alcance y expiración visibles; cada entrada deja `DocumentoAccedido`/auditoría con actor `Plataforma`, B-6) | `PlataformaTenants`, `CatalogoCanonico`, `UsoPlataforma` | CRUD; **nunca** ve decisiones ni documentos por defecto |

**Recordar al tercero (`apps/web`, NT-7…NT-9; implementado).** Con permiso de vínculos: en el detalle de un expediente **observado**, el aviso amarillo trae «Recordar al tercero» (pide confirmación; deshabilitado si ya se le recordó hoy, con la fecha y el total del último recordatorio a la vista); en la **bandeja**, una columna de selección en los expedientes observados, «Seleccionar observados», «Recordar (N)» y «Recordar a los observados» (con el filtro de área de la pantalla), y al terminar un resumen de a quiénes se les recordó y por qué se omitió el resto («1 ya se les recordó hoy»). En **Configuración › Plantillas**, el correo `recordatorio-observaciones` se edita como los demás y, arriba, la tarjeta «Recordatorio automático de observaciones» (interruptor y «cada N días»).

### 3.2 Tercero (`apps/public`)

| Ruta | Propósito | Datos | Acciones |
|---|---|---|---|
| `/e/:token` | Landing: "Constructora Norte te pide 7 documentos para Habilitación de proveedores. Plazo 15/11." OTP si aplica; primera vez: aceptación de la autorización de la plataforma (incluye IA externa) con `AceptacionConEvidencia` | `EnlaceContexto` | Continuar |
| `/registro` (autorregistro, P-1-9; **H4**, T-13) | País + tipo de identificación (`SelectorIdentificacion`), datos básicos, RUT/cédula, aceptación de la autorización de la plataforma; el tercero queda **invisible** hasta que una empresa lo vincule | — | Registrarse |
| `/personas` | **Mis personas relacionadas**: representantes legales (uno principal), suplentes, socios/UBO, miembros de consorcio; cada una es un tercero persona natural con su propia bóveda (cédula, antecedentes) que se valida una vez para todas las empresas (ADR-0010) | `GET /public/v1/tercero/relaciones` | Agregar relación, marcar principal, terminar relación, subir documentos de la persona |
| `/boveda` | **Mis documentos**: por tipo global, versión vigente y, por cada **empresa (razón social)**, qué tiene y qué falta en una frase (RT-5); «Retirar esta versión» abre el **diálogo de impacto** (RT-1): qué versión queda vigente, qué empresas la aprobaron o revisan y qué les pasará, qué secciones de Mi información la usan de soporte, «Subir una versión corregida» y, recién entonces, el motivo; documentos `PorVinculo` agrupados por empresa; formularios con `FormularioDinamico` en modo ver | `BovedaTercero` | Subir versión nueva, editar formulario (avisa si el cambio es crítico), ratificar, ver observación, pedir revisión humana, exportar mi bóveda (ZIP, H8) |
| `/cuentas` | **Mis cuentas bancarias** (N, cada una con su certificación y estado) y **qué cuenta uso con cada empresa**: el tercero designa; el cambio queda "pendiente de aprobación de la empresa" hasta que sus dos aprobadores lo confirmen (P-3-3, P-4-7) | `CuentasTercero` | Agregar cuenta + certificación, designar por empresa |
| `/expediente/:id` | Requisitos de una empresa con `ChipEstado`, siguiente paso y progreso; aceptaciones `PorVinculo` (T&C, autorización de datos de esa empresa) | `ExpedientePublico` | Subir, diligenciar formulario, aceptar, ver observación |
| `/expediente/:id/requisito/:rid` | Ayuda + plantilla versionada, `Dropzone` o `FormularioDinamico`, resultado IA en < 60 s ("Tu Cámara tiene 45 días; Constructora Norte exige máximo 30") o "sin análisis automático, lo revisará la empresa", aviso "esta versión también la verán B y C" | `RequisitoPublico`, `HechosDeVersion` | Subir, reemplazar, ratificar (si el tipo lo permite), pedir revisión humana |
| `/vinculos` | Mis empresas (razones sociales) y estado de cada vínculo; contacto de soporte de cada una | `VinculosTercero` | Entrar |
| `/certificado/:folio` | Mi certificado: tarjeta, compartir, PDF | `CertificadoPublico` | Compartir |
| `/verificar/:folio` | Verificador público: estado, titular, flujo, emisor, fechas, huella; sin datos sensibles | `CertificadoPublico` | — |
| `/embed/:componente` (`boveda`, `expediente`, `ficha`) | **Implementado (IO-6).** La misma app del tercero sin marca, navegación ni pie, con la sesión que el sistema anfitrión entrega por `postMessage` (nunca el token en la URL, nunca el formulario de «Entrar con mi correo»); lleva a `/boveda`, `/expediente/{abierto}` o `/ficha`. Detalle en [integracion.md §8.1](integracion.md#81-el-tercero-embebido-bóveda-expediente-y-ficha) | `PaginaEmbebida`, `PaginaBoveda`, `PaginaExpediente`, `PaginaFicha` | — |

**Campana del tercero (NT-1…NT-6, NT-12; implementada).** En la cabecera de todas las pantallas con sesión, antes de «Salir»: un botón de campana con el contador de no leídas de **todas** sus empresas y, al abrirlo, un panel (popover de 400 px, a lo ancho de la pantalla en el celular) con la lista más reciente primero: cada aviso lleva el **logo de su empresa** (y su color de marca como borde), el título, el texto y «hace cuánto»; los sin leer van en negrita con un punto y la etiqueta «Sin leer» para el lector de pantalla; los que no son de ninguna empresa (una propuesta desde un documento) llevan el isotipo de Validasoft. Un clic marca el aviso como leído y **va a lo que hay que hacer** (`/expediente/{id}` con observaciones, recordatorio, resultado o plazo; `/boveda` para un documento por vencer o vencido; `/ficha` para una propuesta; `/empresas` o `/empresas/{vínculo}` para el certificado y las invitaciones); «Marcar todas como leídas» y el engranaje de **Preferencias de correo** (un interruptor por tipo que va por correo, incluidos «Tu validación volvió a revisión» y «Tu vínculo se reintegró» de NT-18; la campana no se apaga). Carga con esqueletos, estado vacío («No tienes notificaciones») y error con «Reintentar»; **llega al instante** por el stream de su sesión (un aviso nuevo entra arriba y enciende el contador sin recargar) y, si el stream no logra conectar, se refresca cada 30 s.

**Tiempo real en las pantallas (NT-12…NT-15; implementado).** Lo que cambia la otra parte aparece en la pantalla ya abierta, sin recargar: en la web de empresa, la ficha del tercero, el vínculo (con la tarjeta de la cuenta bancaria designada), el expediente y las bandejas; en el portal, la revisión del expediente, «Mi bóveda», «Mi información» y «Mis cuentas». Un solo stream por pestaña; si se cae se reconecta con espera creciente (1 s, 2 s… hasta 30 s) y al volver se refresca todo lo que la pantalla muestra; sin stream, el refresco cada 30 s hace de respaldo. **Lo que se está escribiendo no se pisa**: con cambios sin guardar en una sección de la ficha o en la respuesta al expediente, la pantalla no se refresca sola; muestra un aviso (`role="status"`) «Hay cambios nuevos.» que dice si al actualizar lo no guardado se pierde (ficha) o se conserva (respuesta al expediente), con el botón **Actualizar**, y lo aplica solo al terminar de editar. Cuando la empresa pasa un expediente a «Observado» o aprueba la cuenta, el portal lo muestra sin recargar y la campana se enciende; cuando el tercero designa una cuenta, a quienes la aprueban les cambia la tarjeta del vínculo y les llega «Cuenta bancaria por aprobar».

**Embed.** *(Diseño original; lo implementado es el siguiente párrafo.)* El host abre `<iframe src="https://public…/embed/expediente/{token}">` o el Web Component `<validasoft-expediente token>`. Eventos (origen validado contra la allowlist del tenant): `validasoft:ready`, `validasoft:documento-subido {requisitoId, version}`, `validasoft:expediente-completo`, `validasoft:resize {height}`. Entrada: `validasoft:tema {mode}`.

**Embebido genérico del tercero (IO-6, implementado).** El backend del sistema pide la sesión (`POST /api/v1/sesiones-tercero` con la API key del sistema: vínculo, componente y el origen exacto de la página); el iframe es `{UrlPublica}/embed/{componente}` **sin token en la URL**. Protocolo `postMessage` v1: iframe → página `validasoft:ready {version}`, `validasoft:sesion-requerida {motivo: inicial|por-vencer|vencida|rechazada}` y `validasoft:resize {height}`; página → iframe `validasoft:sesion {token}` (solo al origen de Validasoft) y `validasoft:tema {mode}`. El iframe acepta el token **solo** de su ventana padre cuando el origen de esta es uno de los permitidos (`GET /public/v1/embed/origenes-permitidos`) y el token fue emitido para ese mismo origen; si no, muestra «Esta pantalla solo se muestra dentro de tu sistema» y ningún dato. Lado del anfitrión: `montarEmbebido` de `@validasoft/sdk` o el ejemplo sin librerías `docs/ejemplos/anfitrion-tercero`. Los eventos de negocio del iframe (`documento-subido`, `expediente-completo`) del diseño original no se emiten todavía.

### 3.3 Barra superior de la web de empresa (`apps/web`, NAV-1…NAV-7)

Una sola fila de 48 px (también dentro del iframe del anfitrión): isotipo, `SelectorEmpresa`, **secciones**, y a la derecha «Sesión desde …», la campana, **Configuración** (engranaje) y el tema. Con las nueve secciones de un administrador (Inicio, Bandeja, Revalidaciones, Vencimientos, Cumplimiento, Certificados, Vínculos, Informes, Auditoría) las pestañas no caben en un iframe angosto, y una barra de scroll horizontal bajo ellas se veía mal: la barra **nunca se desplaza en horizontal**.

| Elemento | Comportamiento |
|---|---|
| Pestañas con «Más» adaptativo (patrón «priority+») | Se muestran, en su orden, las pestañas que caben en el ancho que le sobra a la barra; las demás pasan a un botón **«Más ▾»** con un menú (MUI `Menu`). Si caben todas, no hay «Más». Se recalcula al cambiar el ancho de la ventana o del iframe (`ResizeObserver`), antes de pintar: sin parpadeo. |
| Medición | Una sola vez por cambio de tamaño: los anchos de todas las pestañas y del botón se leen de una **fila oculta** (fuera del flujo, sin texto duplicado) y la función pura `cuantasPestanasCaben` (`app/pestanasAdaptativas.ts`) decide cuántas caben. La barra ocupa lo que sobra del encabezado (`flex: 1 1 0`), no lo que contiene, así que lo que se muestra no altera lo medido: no hay bucles. Sin layout (jsdom, contenedor oculto) se muestran todas. |
| Sección activa dentro de «Más» | El botón se subraya como una pestaña activa, lleva el nombre de la sección («Auditoría ▾» en lugar de «Más ▾») y `aria-current="page"`; reservar ese ancho puede dejar fuera una pestaña más (nunca devuelve la activa a la fila). El menú la marca (`selected`, negrita y `aria-current`). |
| Configuración | Engranaje (`Settings`) con tooltip «Configuración», enlace a su primera sección visible; activo (tinte y color de acento) en cualquiera de sus secciones, y entonces ninguna pestaña lo está. Conserva su barra lateral (`LayoutConfiguracion`). Sin permisos de configuración no aparece. |
| Accesibilidad | `nav` con su nombre («Secciones»); «Más» es un botón con `aria-haspopup="menu"`, `aria-expanded` y `aria-controls`; los ítems del menú son **enlaces** (`role="menuitem"`, `aria-current="page"` en el activo) con la navegación por teclado de `MenuList` (Enter abre y enfoca el primero, flechas, Esc cierra y devuelve el foco). El menú no bloquea el scroll de la ventana, para que no cambie el ancho (y con él cuántas pestañas caben) mientras está abierto. |
| Misma apariencia | Mismo alto, tipografía y subrayado de las pestañas anteriores, en modo claro y oscuro y en el modo embebido (altura fija, puente `postMessage`): el cambio es solo de distribución. |

La consola de Plataforma (`ShellPlataforma`, tres pestañas: Grupos y empresas, Operadores y Secretos) conserva su barra: no hay riesgo de desborde.

**Menú de cuenta (ING-3).** Solo con sesión directa, a la derecha de la barra: el avatar de la persona abre un menú con su nombre y correo, «Mi cuenta» y «Cerrar sesión». Con la identidad delegada no aparece: ahí la persona se autentica y sale en su sistema. Tras entrar solo con el código (una cuenta sin passkey) una franja descartable bajo la barra sugiere «Protege tu cuenta con una passkey» con la acción «Registrar».

### 3.4 Ingreso directo y sesión (`apps/web`, ING-1…ING-3)

Fuera de un contenedor la pantalla de sesión es el **ingreso** (dentro de uno, la sesión llega por `postMessage`, sin cambios). Una tarjeta centrada de 420 px, sin spinner de página (el esqueleto de la tarjeta mientras se busca la sesión), con **una acción principal grande** y lo demás en botones compactos:

| Paso | Qué ve la persona |
|---|---|
| Correo | Campo de correo y «Continuar»; si el navegador admite passkeys, un «Entrar con una passkey» (sin escribir nada: el navegador ofrece las del sitio). Es la pantalla de `capturas/ingreso-correo.png` |
| Código | «Revisa tu correo»: seis casillas (`CampoCodigo` de `packages/ui`, que comparte con el portal del tercero), verificación al completar los seis dígitos, intentos que quedan, «Reenviar código» tras 30 s y «Usar otro correo». Un código vencido o agotado ofrece pedir otro |
| Cuenta | Si el correo tiene cuentas en varios grupos (o también es operador), una lista para elegir con cuál entra |
| Passkey | «Confirma que eres tú» (la cuenta tiene passkey: se intenta sola al llegar y se repite con el botón) o «Registra tu passkey» (la cuenta la exige y no tiene: nombre sugerido «Chrome en Windows»); el navegador pide la huella, el rostro o el PIN |
| Sesión | Entra a la app. La cookie `vs_usuario` la restaura al recargar; una renovación silenciosa mantiene el token de acceso **mientras haya actividad** (clics, teclas, desplazamiento) y, sin ella, la sesión queda «en pausa» («Pausamos tu sesión… Continuar») hasta que el servidor la cierre por inactividad |

Al terminar una sesión la pantalla de ingreso dice por qué: «Cerraste la sesión», «Tu sesión terminó» (duración o inactividad del grupo) o «Cerraron tu sesión» (otro dispositivo o un administrador, o un rol que ahora exige passkey). **En desarrollo** (`vite dev`) el ingreso sigue ofreciendo, abajo, «Usar la sesión de desarrollo» (el formulario de aserciones de siempre, que recuerda la elección en la pestaña) y un enlace a los correos de desarrollo (Mailpit); ese formulario no entra al build de producción.

**Plataforma e identidad (PI-1…PI-19, 2026-11-11).** Lo que se suma a esas pantallas:

| Pantalla | Qué ve la persona |
|---|---|
| Correo (SSO y passkeys) | Al escribir un correo cuyo dominio tiene SSO activo aparece «Entrar con {empresa}» junto al código; con el SSO exclusivo es el único botón (sin «Continuar» ni passkey). Si el navegador lo admite, las passkeys del sitio se ofrecen en el autocompletado del propio campo (conditional UI). El navegador sale hacia el proveedor y vuelve a `#/ingreso?sso=…`: «Terminando tu ingreso» y, si algo falla, el motivo en español con el campo de correo de nuevo |
| Passkey › «No tengo mi passkey» | Pide un código de recuperación `XXXXX-XXXXX` (se escribe en mayúsculas con el guion puesto) y lleva a registrar una passkey nueva |
| Reautenticación | Diálogo único «Confirma que eres tú» que dice qué acción se confirma («Para rotar una API key, confirma con tu passkey»): se abre solo cuando una acción sensible responde `reautenticacion-requerida`, hace la ceremonia y la acción continúa sin repetir nada a mano; sin ninguna passkey explica cómo registrar una (Mi cuenta) |
| Mi cuenta › Códigos de recuperación | Cuántos quedan y desde cuándo; «Generar códigos» confirma, muestra los diez una sola vez (copiar, imprimir) y no deja cerrar hasta marcar que se guardaron |
| Configuración › Inicio de sesión único | Dos columnas: **Proveedor de identidad** (OIDC o WorkOS, nombre del botón, emisor, client id, secreto de solo escritura —«termina en wxyz», cambiar o quitar—, la URL de retorno que se copia al proveedor y «Probar la conexión») y **Dominios** (agregar, el texto a publicar y dónde, verificar, quitar); **Cuándo se usa** (activar y convivencia con el código: complementario o exclusivo) y **Personas y roles** (crear al primer ingreso, roles por omisión, reglas «grupo del proveedor → rol» y «el proveedor manda sobre los roles»). Lo que falta antes de guardar se dice en un solo aviso |
| Plataforma › Uso de IA | Mes, búsqueda, totales del mes, la tarifa del adicional (editable) y la tabla por grupo con sus empresas desplegables (incluidas, adicionales, tokens, costo del proveedor, valor del adicional, precio propio), exportable a Excel o CSV |
| Plataforma › Ingresos | La auditoría de ingresos de los operadores, con filtros por operador, evento, resultado y fechas, y «Ver más» |
| Plataforma › Secretos | Tarjeta «Clave de arranque» (estado, operadores con passkey, desactivar o reactivar) y los tipos de cuenta en eventos y vínculos |
| Plataforma › Operadores y grupos | Operadores desactivados se reactivan; cada grupo ofrece «Agregar empresa» y «Sistemas» (desactivar y reactivar); el plan de IA de una empresa suma el precio propio por adicional |

---

## 4. Flujos críticos (UI ↔ API)

**(a) Solicitar documentos.** `/flujos/:id` → diálogo (tercero nuevo o existente, atributos, plazo) → `POST /api/v1/expedientes` → 201 con folio y requisitos → `POST /expedientes/{id}/invitaciones` → Snackbar "Solicitud enviada a maria@…. Copia el enlace si prefieres compartirlo tú." → invalidación de `BandejaRevision`. El diálogo usa `SelectorIdentificacion` (país + tipo + número) y, si el tercero ya existe en la bóveda, muestra solo `requisitosCubribles` ("N requisitos podrían quedar cubiertos cuando el tercero acepte"), **sin nombres de documentos ni versiones**: la compartición con esta empresa ocurre al aceptar la invitación (`POST /public/v1/invitaciones/{expedienteId}/aceptar` registra consentimientos y comparte, ADR-0009).

**(b) El tercero sube.** `Dropzone` valida límites (20 MB / 50 páginas / formatos), convierte HEIC o fotos a PDF y calcula sha256 en cliente → si coincide con la versión actual: "Es el mismo archivo. ¿Quieres ratificar?" → si no: `POST …/requisitos/{rid}/documentos` → PUT prefirmado → `POST /documentos/{docId}/versiones/{n}/confirmar` → SSE (eventos públicos de 05 §7): `documento.version_devuelta` ("Esto no parece un RUT. Adjunta el PDF descargado de la DIAN.") **o** `documento.hechos_registrados` + `documento.version_nueva` → la UI muestra "Listo. 3 empresas lo están revisando" y, por empresa, el resultado según su nivel: "Norte: aprobado automáticamente", "Sur: en revisión", "C: observado: tu documento tiene 45 días y C exige 30". Si la IA no está disponible: "Recibido. Lo revisará cada empresa." Si tarda más de 60 s: "Te avisamos por correo."

**(c) Observación y nueva versión.** Revisor de A observa v2 → correo + campana → tercero sube v3 → **todas** las empresas que exigen el tipo reciben `VersionActualCambiada`: A revalida v3; B, que había aprobado v2, ve en `/revalidaciones` "RUT: versión nueva del 28/10, antes validaste la del 02/03; sin cambios en campos clave" y, si está en `Automatico`, ya aparece aprobada con actor `Sistema (IA)`. El timeline de cada expediente muestra `VeredictosInvalidadosPorNuevaVersion`.

**(d) Certificado.** Última área aprueba → SSE → "Certificado emitido. Compártelo con este enlace." → `/certificado/:folio` en `public`; entrega del webhook visible en Configuración → Webhooks.

**(e) Vencimiento con avisos consolidados.** Job diario → `documento.por_vencer` en A (30 días) → el tercero recibe **un** aviso: "Tu Cámara de comercio vence para A el 15/11 y para B el 30/12. Súbela una vez y quedas al día con ambas." → sube v4 → (b) y (c). Si no actúa: A pasa a vencido (chip rojo, certificado marcado), B sigue vigente.

**(f) Revalidación automática vs manual.** Misma versión, tres empresas: Norte (`Automatico`, Cámara ≤ 30 días) → aprobada en segundos; Sur (`Asistido`; otra razón social del mismo grupo que Norte, con su propio override de nivel y frescura (B-4), y decide aparte) → sugerencia "Aprobar: sin cambios" y botón Confirmar; C (`Manual`) → cola humana con `PanelIA` completo. El tercero ve los tres estados en `/boveda`. Si Norte observa automáticamente, el tercero puede "Pedir revisión humana" y la solicitud aparece en `/excepciones` de Norte.

**(g) Cuenta bancaria designada.** El tercero en `/cuentas` agrega una cuenta con su certificación (`POST /public/v1/boveda/cuentas-bancarias` + subida en dos pasos) → la IA cruza titular/número con el formulario (`CoherenciaFormularioSoporte`) → designa esa cuenta para Constructora Norte (`POST /public/v1/vinculos/{id}/cuenta-designada`) → en `web`, dos aprobadores de Norte ven la solicitud en `/excepciones`/ficha del tercero y confirman (`POST /vinculos/{id}/cuenta-designada/aprobar`) → `CuentaDesignadaAprobada` → webhook `vinculo.cuenta_designada` al ERP de Norte. Hasta entonces la UI del tercero muestra "pendiente de aprobación de Constructora Norte".

**(h) Edición de un formulario.** El tercero edita su información tributaria (`FormularioDinamico`) → al guardar, el diff resalta si tocó campos críticos → crea versión siempre; si hubo campo crítico, todas sus empresas revalidan (como b/c); si no, las empresas reciben una novedad "cambio menor" sin abrir revalidación (P-3-6).

**(i) El tercero retira una versión (RT).** `/boveda` → «…» → «Retirar esta versión» → `GET …/versiones/{n}/retiro/impacto` (esqueleto mientras carga) → el diálogo dice «Tu RUT vigente pasará a ser la versión 7», «Constructora Norte la aprobó: tendrá que revisar de nuevo tu RUT versión 7» (a quien la revisa, con su folio) y qué secciones de Mi información la usan de soporte; si a alguna empresa le toca revisar de nuevo, ofrece «Subir una versión corregida» y avisa «Les avisamos con tu motivo» → motivo (obligatorio) → `POST …/retirar`. La empresa recibe campana y correo («Versión retirada por el tercero»), su línea de tiempo dice «El tercero retiró la versión 8 de RUT (motivo: …); vuelve a la versión 7.» y la revalidación muestra el motivo (no se aprueba en lote). La tarjeta del tercero pasa a «Aprobó la versión 8, que retiraste. Debe revisar la versión 7: revalidación abierta.» y, en Mi información, la sección que usaba esa versión de soporte dice «Tu soporte (…) fue retirado» sin perder sus datos.

---

## 5. Contratos de lectura que el frontend necesita

| Proyección | Campos |
|---|---|
| `BandejaRevision` (`GET /expedientes`) | `empresaId, expedienteId, folio, tipoExpediente, tercero{id,nombre,identificacion{pais,tipo,numero}}, flujo{id,nombre,version}, etapaActual, etapas[]{nombre,estado}, estadoPorArea[]{areaId,estado:'esperando'\|'pendiente'\|'aprobado'\|'observado'\|'rechazado',por?,automatico,fecha?}, vencimientoMasProximo{requisito,fecha,dias}, revisorAsignado?, diasEnEtapa, requisitosPendientes, decididosPorIA, ultimaActividad` |
| `BandejaRevalidaciones` (`GET /revalidaciones`, `RevalidacionDto` de 05 §4) | `empresaId, vinculoId, tercero, tipoDocumento, versionAceptada{n,fecha}, versionActual{n,fecha}, comparacion{resultado,camposCambiados[],camposCriticosCambiados[]}, nivelRevision, hechosDisponibles, sugerenciaAutomatica?, plazoHasta, diasRestantes, bloqueante, expedienteId` |
| `ColaDeExcepciones` (`GET /excepciones`, `ExcepcionDto`) | `empresaId, items[]{expedienteId,requisitoId,tipo: 'Critico'\|'Confianza'\|'Manipulacion'\|'Muestreo'\|'RevisionSolicitada'\|'CuentaDesignada'\|'CoherenciaSoporte', motivo, hechos, solicitadoPorTercero?, fecha}` |
| `CuentasTercero` (public, `GET /public/v1/boveda/cuentas-bancarias`) | `cuentas[]{id,banco,tipo,numeroEnmascarado,certificacion{versionActual,estado},designadaEn[]{empresaId,empresa,estado: EstadoCuentaDesignada}}` |
| `UsoTenant` | `mes, porEmpresa[]{empresaId, usuariosActivos, vinculosActivos, gb, lecturasIA, decisionesAutomaticas, decisionesAsistidas, webhooks}, presupuestoIA{limiteUsd,consumidoUsd}` |
| `ExpedienteDetalle` | cabecera + `plazo, atributos, requisitos[]{id,tipo,seccion,obligatorio,estado,versionActual{n,fechaCarga,hash},aceptada{n,vigenteDesde,vigenteHasta,motivoVigencia,desactualizadaDesde?},areasQueAprueban[],asignadoA?,puedeAprobar,puedeObservar,puedeEximir}, enlaceTercero{url,expira}, puedeReabrir` |
| `RequisitoDetalle` | `versiones[]{n,archivo{urlFirmada,mime,paginas,hash},cargadoPor,fecha,esActual}, veredictos[]{tipo,area,actor{nombre,rol,esSistema},motivo,comentario,fecha,versionN,reglaAplicada?}, hechos{tipoDetectado,legible,confianza,datos[],comparacionConVersionAnterior}, nivelRevision, ayuda{texto,plantillaUrl}, vigencia{regla,vigenteHasta,motivo}` |
| `TimelineExpediente` | `eventos[]{tipo,fecha,actor{nombre,rol,esTercero,esSistema},requisito?,antes?,despues?,comentario?}` |
| `KpisTenant` / `KpisFlujo` / `KpisEmpresa` (`GET /kpis`, `KpisDto`) | `activos, tiempoPromedioDias, pctAprobacionAutomatica, precisionIa, slaCumplidoPorArea, porEtapa[], decisionesAutomaticas{aprobadas,observadas,rechazadas,revertidas,muestreoRevisado,horasAhorradasEstimadas}, consumoIa{lecturas,decisiones,costoUsd,presupuestoUsd}, equidad{porTipoPersona,porRegion,porTipoDocumento,alertas}` |
| `MisPendientes` (`GET /mis-pendientes`) | `porArea[]{areaId,cantidad,masAntiguoDias}, revalidaciones{cantidad,conCriticos}, excepciones, cuentasPorAprobar` |
| `VencimientosProximos` (`GET /vencimientos/proximos?dias=`) | `items[]{vinculoId,folio,tercero,tipoDocumento,vigenteHasta,dias,estado,motivoVigencia,permiteRatificar,ultimaNotificacion?}` |
| `FlujosResumen` (`GET /flujos/resumen`) | `flujos[]{id,codigo,nombre,versionPublicada,expedientesActivos,tiempoPromedioDias,pctAprobacionAutomatica}` |
| `Tercero360` (`GET /terceros/{id}/ficha`) | `identificacion{pais,tipo,numero,dv}, nombre, contactos[], relaciones[]{terceroRelacionado{id,nombre,identificacion}, tipo: TipoRelacionTerceros, principal, vigenteDesde, vigenteHasta, documentos[]{tipo,estado}}, atributos, vinculoConEstaEmpresa{flujo,expedienteId,folio,estado,certificado?,cuentaDesignada,bloqueo?}, otrasEmpresasDelGrupo[]{empresaId,estadoVinculo} (sin decisiones), documentos[]{tipo,versionActual,aceptadaPorMi{n,vigenteHasta,motivoVigencia},estado}` |
| `BovedaTercero` (public, `GET /public/v1/boveda`) | `documentos[]{tipoCanonico,nombre,ambito,clase:'Archivo'\|'Formulario',versionActual{n,fecha,hechosResumen?},porEmpresa[]{empresaId,empresa,estadoRequisito,versionAceptadaN,venceEl?,motivoVigencia?,observacion?,decisionAutomatica?},permiteRatificar,camposCriticos[],proximoVencimiento{fecha,empresas[]}}` |
| `CertificadoPublico` (`CertificadoPublicoDto` de 05 §4) | `folio, estado: EstadoCertificado ('Vigente'\|'ConPendientes'\|'Suspendido'\|'Vencido'\|'Revocado'\|'Reemplazado'), pendientes[]{tipo, bloqueante}, bloqueante: boolean (A-2: `ConPendientes` pasa a bloqueante tras `diasRevalidacion`; `ChipEstado` lo pinta ámbar informativo o rojo bloqueante; `Suspendido` por bloqueo de la empresa, B-5), titular{nombrePublico, identificacionEnmascarada}, emisor{empresa,logoUrl}, flujo, emitidoEl, vigenteHasta?, huella, contenidoCanonico, firma{algoritmo:'Ed25519', valor, clavePublicaUrl}, urlVerificacion` (sin fechas por etapa ni nombres de revisores, P-8-1) |
| `EnlaceContexto` | `emisor, flujo{nombre,descripcion}, tercero{nombreParcial,correoEnmascarado}, requiereOtp, plazo, cantidadRequisitos` |
| `ExpedientePublico` | subconjunto sin datos internos: `requisitos[]{…,siguientePaso,observacionVigente?,otrasEmpresasQueLoVeran[]}` |
| `FlujoDefinicion` | `version, reglasDocumentos[]{tipoDocId,condicion,obligatorio,critico,reglaFrescura,nivelRevision}, areas[]{id,nombre,orden,paralelo,slaHoras,requisitos[]}, verificaciones[], plantillaCertificado, motivos[]` |

Además: SSE `/api/v1/eventos/stream` (filtrado por empresa activa), `/public/v1/expedientes/{id}/stream` y `/public/v1/boveda/stream` (la bóveda escucha antes de que exista expediente); OpenAPI en `/openapi/v1.json` y `/openapi/public-v1.json` para orval; `GET /catalogos/tipos-identificacion?pais=` para `SelectorIdentificacion`; evidencia de aceptaciones como `AceptacionDto`; `GET /tipos-documento/{id}/esquema` devuelve el JSON Schema fusionado para `FormularioDinamico`.

---

## 5-bis. Catálogo de pantallas por hito (coincide con el documento 09)

| Hito | `apps/web` | `apps/public` | `packages` |
|---|---|---|---|
| H1 | Login OIDC + 2FA; AppShell con `SelectorEmpresa`; `/plataforma` (tenants, empresas, catálogo de identificación, accesos de soporte concedidos); `/configuracion/tenant/usuarios`, `/api-keys` básicas, `/configuracion/tenant/accesos-soporte` (B-6) | Shell, `/verificar/:folio` placeholder | `ui` (tema, `ChipEstado`, `Folio`, `KpiCard`, `BarraProgresoEtapas`, `SelectorEmpresa`, `SelectorIdentificacion`), `api-client`, `i18n`, `config` |
| H2 | `/plataforma/catalogo` (tipos canónicos, esquemas); `/terceros/:id` básico (documentos, hechos, **personas relacionadas**) | `/e/:token` (aceptación de la plataforma; enlace de demo emitido por la semilla hasta H4), `/boveda` básica (subir, ver estado por empresa, hechos), `/personas` | `forms` (`FormularioDinamico`, `DiffCampos`), `Dropzone`, `PanelIA`, `VisorDocumento`, `TarjetaPersonaRelacionada` |
| H3 | `/flujos`, `/flujos/:id/editor` (reglas, áreas, frescura, nivel, formularios, plantillas, **pestaña "Reglas de selección" y flujo por defecto**), `/configuracion/tenant/tipos-documento`, `/configuracion/empresa/tipos-documento` (overrides, B-4), `/configuracion/tenant/formularios` | Vista previa del tercero (`?preview=`) | — |
| H4 (adición) | En `/expedientes/:id` acción **"Cambiar flujo"** (admin) y en el editor **"Aplicar a expedientes abiertos"** con simulación de impacto | — | — |
| H1 (adición) | `/auditoria` pestañas Identidad y Configuración con diff (consulta); exportación en H8 | — | — |
| H4 | `/expedientes` (bandeja por área), `/expedientes/:id` (visor + `PanelIA` + `PanelVeredicto` + rechazo de expediente + timeline), `/configuracion/empresa/areas`, bloqueo en `/terceros/:id` | `/e/:token` con aceptación de invitación y consentimientos por empresa (ADR-0009), `/registro` (autorregistro, T-13), `/expediente/:id`, `/expediente/:id/requisito/:rid`, formularios, `AceptacionConEvidencia`, campana | `PanelVeredicto`, `LineaDeTiempo`, `TablaExpedientes`, `Bandeja` |
| H5 | `/certificados`, `/configuracion/tenant/webhooks`, `/configuracion/tenant/plantillas` (cabecera/pie, marca), notificaciones | `/certificado/:folio`, `/verificar/:folio` real, `/vinculos` | `TarjetaCertificado` |
| H6 | `/revalidaciones`, `/vencimientos`, ficha 360 completa (aceptaciones, cuenta designada), `/configuracion/tenant/tipos-documento` (vigencias) | `/cuentas`, bóveda con vencimientos y `AvisoConsolidado`, ratificar | `TarjetaCuentaBancaria`, `AvisoConsolidado` |
| H7 | `/excepciones`, `/configuracion/tenant/revision-automatica`, verificaciones (listas, estudio, UBO/PEP) en `/expedientes/:id`, certificado `Suspendido` por bloqueo en `/certificados` (B-5), informe SAGRILAFT | "Pedir revisión humana", formularios UBO/PEP | — |
| H8 | Invitación masiva, campañas, delegaciones, conflicto de intereses, `/auditoria` con exportación ZIP, plantillas Liquid, dominio propio, planes y uso, matriz de riesgo, `/configuracion/empresa` completa | Exportar mi bóveda, PWA offline, embed `/embed/*`, trazo opcional | — |
| H9–H11 | Sandbox de API keys (interruptor «Llave de sandbox» en Configuración › API keys; el buzón y el reinicio, por API y SDK), vistas de entregas por integración, panel de dead letters | Sin pantallas nuevas | `sdk-ts` |

---

## 6. Riesgos de UX y errores del pasado

| Riesgo / error del pasado | Mitigación |
|---|---|
| Datos mock en producción | `KpiCard` exige valor de una query; MSW solo en DEV/tests; regla ESLint contra `mocks/` |
| Iframes legacy | Solo el embed propio, versionado, con contrato postMessage |
| Documento sin versión/fecha/vigencia | Campos obligatorios en el tipo de `TarjetaDocumento`; el contrato no admite `undefined` |
| Permisos ocultos en el front | La UI oculta por claims como conveniencia; cada acción usa `puede*` de la proyección; el backend decide |
| Confusión "¿qué versión estoy viendo?" | Siempre se muestra "versión actual del tercero" y "la que validaste" con fechas; nunca una lista de versiones para elegir |
| Desconfianza en la decisión automática | `PanelIA` muestra la regla aplicada y el diff; "Revertir" a un clic; muestreo visible en Inicio |
| Tercero bombardeado por N empresas | `AvisoConsolidado` y `/boveda` con una sola acción por documento |
| Columnas por área con flujos distintos | Columnas desde `AreasFlujo`; en bandejas mixtas, columna "Áreas" resumida + popover |
| Celular viejo o sin señal | PWA con cola offline, fotos comprimidas, pdf.js lazy |
| Magic link filtrado | Un solo uso, sesión corta, OTP configurable, nunca en localStorage |
| Fatiga de revisión | Atajos A/O/R, flechas entre requisitos, aprobación en lote de `SinCambios`, `Asistido` |
| Usuario con rol en varias empresas del grupo actúa en la equivocada | `SelectorEmpresa` siempre visible con logo y NIT; el folio incluye el prefijo de la empresa; confirmación al aprobar si cambió de empresa hace < 10 s |
| El tercero no entiende por qué una empresa del mismo grupo le vuelve a pedir lo mismo | `/boveda` muestra cada razón social por separado con su estado y explica "cada empresa valida por su cuenta; tu archivo es el mismo" |
| Formularios distintos por empresa rompen la compartición | El núcleo del formulario es canónico; los campos privados se muestran en una sección "Solo para {empresa}" |

---

## Decisiones abiertas
1. **Web Component vs iframe puro para el embed.** *Propuesta:* iframe con Web Component envolvente opcional. *Quién decide:* Arquitectura + equipo del Portal.
2. **Storybook obligatorio** para `packages/ui` desde H1 o desde H4. *Propuesta:* desde H1 con los 5 componentes base. *Quién decide:* Frontend A.
3. **Aprobación en lote de revalidaciones `SinCambios`** en nivel `Manual` (¿se permite un clic para N?). *Propuesta:* sí, con confirmación y registro individual. *Quién decide:* Producto.
4. **Modo claro por defecto** para tenants que lo pidan. *Propuesta:* configurable por tenant; oscuro por defecto. *Quién decide:* Producto. (La marca por empresa —logo y colores— ya está decidida: P-11-3.)
5. **Nombre público de la "empresa interna"** en la UI del tercero ("empresa", "razón social", "compañía"). *Propuesta:* "empresa", mostrando NIT y nombre del grupo en tooltip. *Quién decide:* Producto.
6. ~~Idiomas, límites de archivo, aceptación con OTP, accesibilidad~~ **Cerradas** (registro P-16-1/2/3/4/5).

## Fuentes verificadas
Ver [01-analisis-legado-srm-portal.md](01-analisis-legado-srm-portal.md) (pantallas actuales del SRM y del Portal: `Frontend\React\src\pages\Provedores\EstadoSolicitudes\…`, `SolicitudesActualizacion\components\DocumentReview\…`, `frontend\reactapp\src\Pages\{Wizard,Vencimiento,Pendientes}`).
