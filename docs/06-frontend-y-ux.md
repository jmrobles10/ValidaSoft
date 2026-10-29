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
| Router | React Router 7 (data mode), `lazy` por ruta, loaders que precargan queries | Code splitting natural, `errorElement` por rama |
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
| `TarjetaDocumentoBoveda` (public) | `documento: DocumentoBoveda` | Versión actual del tercero + fila por **empresa (razón social)**: estado (`Aprobado vN`, `Desactualizado` = revalidando, `Observado` con motivo, `Vencido`, `vence el …`); botón "Pedir revisión humana" cuando la decisión fue automática (P-6-8) |
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
| Carga | Skeleton con la forma del contenido; nunca spinner global | — |
| Error recuperable | Inline con reintento | "No pudimos cargar la bandeja. Reintentar." |
| Error de acción | Snackbar con siguiente paso | "No se notificó al tercero por correo. Copia el enlace y envíalo tú." |
| Decisión automática | Banner informativo | "Aprobado automáticamente: sin cambios frente a la versión del 02/03. Puedes revertirlo." |
| Sin permiso | Página 403 propia | "Esta sección es de Configuración. Pide acceso a un administrador." |

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
| `/certificados`, `/certificados/:folio` | Lista y detalle; `EstadoCertificado` (`Vigente`, `ConPendientes` informativo o bloqueante, `Suspendido`, `Vencido`, `Revocado`, `Reemplazado`) con historial de emisiones por vínculo (folio `-C-…-{n}`) | `Certificados`, `CertificadoDetalle` | Revocar (motivo), reemitir, copiar enlace |
| `/vencimientos` | Informe por documento y vínculo (`Vencido` / `PorVencer` / vigente) con `motivoVigencia`, Excel, notificación; **campañas de renovación** (H8) | `VencimientosProximos`, `Campanas` | Notificar seleccionados, crear campaña |
| `/configuracion/empresa/*` | Datos de **esta** empresa: NIT, logo, colores, `Reply-To`, contacto de soporte, prefijo de folio; áreas y revisores; **tipos-documento (overrides por empresa, B-4)**: `reglaFrescura`, `nivelRevision`, `diasRevalidacion`, `politicaDesactualizada`, `aprobadoresRequeridos`, `porcentajeMuestreo`, `camposCuyoCambioExigeHumano` que sobreescriben los defaults del tenant (y a su vez los sobreescribe la regla del flujo); **delegaciones** (H8); separación de funciones y conflicto de intereses (H8) | respectivas | CRUD |
| `/configuracion/tenant/*` | Del grupo: **empresas** (alta/baja de razones sociales), usuarios y roles **por empresa o para todo el tenant**, tipos-documento (defaults del tenant: mapeo canónico, restricción de ámbito, `reglaFrescura`, `diasAviso`, `diasGracia`, `diasRevalidacion`, `politicaDesactualizada`), **formularios** (extensión de campos privados), **revisión automática** (umbrales, muestreo, presupuesto de decisiones y consumo; el nivel vive en el tipo de documento), motivos, plantillas (cabecera/pie en MVP; Liquid en H8), api-keys (alcance por empresa, sandbox), webhooks (entregas y reintento), integraciones, plan y uso, **accesos de soporte** (conceder a Plataforma acceso temporal con alcance y horas; lista de concesiones vigentes y auditoría, B-6) | respectivas | CRUD |
| `/auditoria` | Registro unificado de **esta empresa** (y del tenant para Administrador/Auditor con alcance `*`) con pestañas por `tipo`: **Dominio** (timelines, decisiones automáticas y reversiones), **Configuración** (cambios de flujos, tipos, áreas, políticas, usuarios, API keys, webhooks con visor de **diff antes/después** por campo), **Identidad** (logins, 2FA, bloqueos, uso de API keys, token exchange, enlaces canjeados) y **Accesos** (quién descargó qué versión); filtros por entidad, `entidadId`, actor, fechas; detalle con `RegistroAuditoriaDto` | `GET /auditoria?tipo=…` (`PaginaDto<RegistroAuditoriaDto>`) | Exportar ZIP firmado (`POST /auditoria/exportar`, H8; en H1 solo consulta), copiar `correlationId` |
| `/plataforma/*` (rol `Plataforma`) | Tenants (crear, región, plan, clave KMS), empresas de cada tenant, **catálogo canónico** de tipos de documento y esquemas de formulario (versionar, aprobar propuestas de tenants), catálogo de identificación por país, uso y costes de IA por tenant, dead letters y entregas fallidas, **accesos de soporte concedidos** (solo entra a un tenant con una concesión vigente del Administrador de ese tenant; alcance y expiración visibles; cada entrada deja `DocumentoAccedido`/auditoría con actor `Plataforma`, B-6) | `PlataformaTenants`, `CatalogoCanonico`, `UsoPlataforma` | CRUD; **nunca** ve decisiones ni documentos por defecto |

### 3.2 Tercero (`apps/public`)

| Ruta | Propósito | Datos | Acciones |
|---|---|---|---|
| `/e/:token` | Landing: "Constructora Norte te pide 7 documentos para Habilitación de proveedores. Plazo 15/11." OTP si aplica; primera vez: aceptación de la autorización de la plataforma (incluye IA externa) con `AceptacionConEvidencia` | `EnlaceContexto` | Continuar |
| `/registro` (autorregistro, P-1-9; **H4**, T-13) | País + tipo de identificación (`SelectorIdentificacion`), datos básicos, RUT/cédula, aceptación de la autorización de la plataforma; el tercero queda **invisible** hasta que una empresa lo vincule | — | Registrarse |
| `/personas` | **Mis personas relacionadas**: representantes legales (uno principal), suplentes, socios/UBO, miembros de consorcio; cada una es un tercero persona natural con su propia bóveda (cédula, antecedentes) que se valida una vez para todas las empresas (ADR-0010) | `GET /public/v1/tercero/relaciones` | Agregar relación, marcar principal, terminar relación, subir documentos de la persona |
| `/boveda` | **Mis documentos**: por tipo global, versión actual y, por cada **empresa (razón social)**, su estado (`Aprobado vN`, `Desactualizado`, `Observado` con motivo, `Vencido`, vence el …); documentos `PorVinculo` agrupados por empresa; formularios con `FormularioDinamico` en modo ver | `BovedaTercero` | Subir versión nueva, editar formulario (avisa si el cambio es crítico), ratificar, ver observación, pedir revisión humana, exportar mi bóveda (ZIP, H8) |
| `/cuentas` | **Mis cuentas bancarias** (N, cada una con su certificación y estado) y **qué cuenta uso con cada empresa**: el tercero designa; el cambio queda "pendiente de aprobación de la empresa" hasta que sus dos aprobadores lo confirmen (P-3-3, P-4-7) | `CuentasTercero` | Agregar cuenta + certificación, designar por empresa |
| `/expediente/:id` | Requisitos de una empresa con `ChipEstado`, siguiente paso y progreso; aceptaciones `PorVinculo` (T&C, autorización de datos de esa empresa) | `ExpedientePublico` | Subir, diligenciar formulario, aceptar, ver observación |
| `/expediente/:id/requisito/:rid` | Ayuda + plantilla versionada, `Dropzone` o `FormularioDinamico`, resultado IA en < 60 s ("Tu Cámara tiene 45 días; Constructora Norte exige máximo 30") o "sin análisis automático, lo revisará la empresa", aviso "esta versión también la verán B y C" | `RequisitoPublico`, `HechosDeVersion` | Subir, reemplazar, ratificar (si el tipo lo permite), pedir revisión humana |
| `/vinculos` | Mis empresas (razones sociales) y estado de cada vínculo; contacto de soporte de cada una | `VinculosTercero` | Entrar |
| `/certificado/:folio` | Mi certificado: tarjeta, compartir, PDF | `CertificadoPublico` | Compartir |
| `/verificar/:folio` | Verificador público: estado, titular, flujo, emisor, fechas, huella; sin datos sensibles | `CertificadoPublico` | — |
| `/embed/expediente/:token` | Misma UI de requisitos sin shell; postMessage | `ExpedientePublico` | — |

**Embed.** El host abre `<iframe src="https://public…/embed/expediente/{token}">` o el Web Component `<validasoft-expediente token>`. Eventos (origen validado contra la allowlist del tenant): `validasoft:ready`, `validasoft:documento-subido {requisitoId, version}`, `validasoft:expediente-completo`, `validasoft:resize {height}`. Entrada: `validasoft:tema {mode}`.

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
| H9–H11 | Sandbox de API keys, vistas de entregas por integración, panel de dead letters | Sin pantallas nuevas | `sdk-ts` |

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
