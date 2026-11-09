# 12 — Plan de ejecución por hito: qué se construye, con qué modelo y con qué esfuerzo

| Estado | Fecha | Responsable |
|---|---|---|
| Borrador | 2026-10-29 | Arquitectura |

**Propósito.** Resumir cada hito del plan (09) en lo que realmente se construye, y proponer para cada tarea el **modelo de Claude** y el **nivel de esfuerzo de razonamiento** con los que conviene ejecutarla con Claude Code, además de una estimación humana en días-persona. Es la guía operativa para abrir los issues de GitHub.

**Relacionados.** [09-plan-de-hitos-y-responsabilidades.md](09-plan-de-hitos-y-responsabilidades.md) · [11-trazabilidad-por-hito.md](11-trazabilidad-por-hito.md) · [03-dominio-y-eventos.md](03-dominio-y-eventos.md) · [05-api-contratos-y-webhooks.md](05-api-contratos-y-webhooks.md) · [MAPA.md](MAPA.md).

## 0. Criterio para elegir modelo y esfuerzo

| Modelo | Úsalo cuando… | Esfuerzo típico |
|---|---|---|
| **Fable 5.1** (`claude-fable-5-1`) | La tarea define reglas de negocio con invariantes, máquinas de estado, sagas, seguridad, migración de datos con casos ambiguos, o es una **revisión/auditoría**. Es el modelo que más vale cuando equivocarse cuesta rehacer un hito. | `high` / `xhigh` |
| **Opus 5.5** (`claude-opus-5-5`) | Implementación con criterio: módulos completos (backend o frontend) a partir de un contrato ya definido, integración de librerías (Marten, Wolverine, OpenIddict, MinIO), pruebas de integración, infraestructura. | `medium` / `high` |
| **Sonnet 5.5** (`claude-sonnet-5-5`) | Implementación bien especificada y acotada: endpoints CRUD desde el 05, pantallas desde el 06 y el design system, pruebas unitarias de reglas ya escritas, plantillas, documentación de usuario. | `medium` |
| **Haiku 5.5** (`claude-haiku-5-5`) | Trabajo mecánico y repetitivo: generar clientes desde OpenAPI, fixtures, seeds, traducciones, scaffolding de carpetas, renombrados masivos, sincronizar tablas del 11. | `low` |

Reglas prácticas:
- **Diseñar con Fable, construir con Opus/Sonnet, revisar con Fable.** Cada hito abre con una sesión Fable que convierte los documentos en issues con criterios de aceptación, y cierra con una revisión Fable (`/code-review` a `high`) antes de la demo.
- Subir el esfuerzo **solo** cuando la tarea toca invariantes, concurrencia, seguridad o dinero; bajarlo en todo lo que sea copiar un contrato ya escrito.
- Un issue = una sesión de Claude Code con el documento dueño cargado (03 para dominio, 05 para API, 06 para UI, 11 para la lista exacta del hito).
- Estimaciones en **días-persona** con Claude Code como herramienta principal; suponen el equipo de 09 §2 (3–4 personas).

## 1. Resumen por hito

| Hito | Qué queda construido al cerrar | Semanas | Días-persona |
|---|---|---|---|
| H1 Fundación | Repo ejecutable en Docker: tenant + empresa, identidad (OIDC, 2FA, API keys), tres esquemas en Marten con un solo store, outbox, medición de uso, CI en GitHub, tema UI y login | 3 | 42 |
| H2 Bóveda | Terceros multipaís y personas relacionadas, documentos con versiones inmutables, hechos sin/con IA, versión única, compartición tras aceptar, formularios con esquema | 3 | 45 |
| H3 Configuración | Catálogo canónico y por tenant/empresa, áreas, motivos, flujos versionados, reglas de exigencia y de selección de flujo, editor | 3 | 36 |
| H4 Expediente | Vínculo, expediente inicial, etapas/áreas, veredictos, observación y reenvío, override, bloqueo, bandejas, apps del revisor y del tercero | 4 | 64 |
| H5 MVP | Certificado verificable, notificaciones, webhooks firmados y pull, eventos públicos v1, seed demo | 3 | 39 |
| H6 Vigencias | Dos capas de vigencia, avisos consolidados, revalidación obligatoria, renovación, ratificación, cuentas designadas | 4 | 52 |
| H7 Cumplimiento | Listas restrictivas, estudio de cumplimiento, UBO/PEP, niveles de revisión automática, excepciones, equidad | 3 | 39 |
| H8 Operación | Lotes, campañas, delegación, ficha 360, informes, auditoría exportable, habeas data, planes, marca blanca, embed, PWA | 3 | 42 |
| H9 Integración | SDKs, SRM con flag y receptor de webhooks, Portal embebido, Marco con consulta y respaldo | 3 | 36 |
| H10 Migración | Importador idempotente, piloto por empresa, sincronización inversa, rollback | 3 | 33 |
| H11 Endurecimiento | Carga, backups, observabilidad completa, retiro del legado | 4+ | 30 |

## 2. Tareas por hito

Columnas: **Tarea** · **Qué se entrega** · **Modelo** · **Esfuerzo** · **Días-persona** · **Documento dueño**.

### H1 — Fundación técnica

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 1.1 Abrir el hito: issues con criterios de aceptación | Épica H1 + issues en GitHub Projects, plantillas de issue/PR, CODEOWNERS, protección de `main` | Fable 5.1 | high | 1 | 09, 11 |
| 1.2 Estructura de la solución y monorepo | `Validasoft.sln`, proyectos Api/Worker/Dominio/Aplicacion/Infraestructura/Contratos, `apps/`, `packages/`, `Directory.Build.props`, analizadores | Haiku 5.5 | low | 2 | 04 §3 |
| 1.3 Docker Compose y Makefile | postgres, minio, mailpit, otel-lgtm (perfil), api, worker, web, public; `make up/test/seed/migrar`; `.env.example` | Opus 5.5 | medium | 2 | 04 §11 |
| 1.4 **Spike Marten: un solo store conjoined con tenants técnicos** (T-2) | Prueba que demuestra evento + proyección inline + outbox en una transacción para `boveda`, `plataforma` y `validacion` | Fable 5.1 | xhigh | 3 | 03 §2.2, ADR-0004 |
| 1.5 Tenancy y empresa interna | Entidades `Tenant`, `Empresa`, `uso_tenant`; middleware `X-Empresa`; `ISesionDeEmpresa` (T-3); NetArchTest que prohíbe consultas sin tenant/empresa | Fable 5.1 | high | 4 | 03 §2–3, 07 §5 |
| 1.6 Identidad: OpenIddict + Identity | Login PKCE, 2FA TOTP, roles por empresa/tenant, permisos finos, grants `magic-link` y `erp-token` (stub) | Opus 5.5 | high | 5 | 07 §2–3, ADR-0003 |
| 1.7 API keys con scopes y alcance por empresa | Hash Argon2id, prefijo, rotación con solapamiento, revocación, `ultimoUso` | Opus 5.5 | medium | 2 | 07 §4, 05 §2.1 |
| 1.8 Catálogo de tipos de identificación multipaís | Tabla + validadores (NIT, CC, CE, PA, RFC, CUIT…), normalización sin DV | Sonnet 5.5 | medium | 2 | 03 §4, 05 §4 |
| 1.9 Transversales de API | ProblemDetails, `Idempotency-Key`, cursor, rate limit, correlación, OpenAPI 3.1, 404 para otro tenant/empresa (T-4) | Opus 5.5 | medium | 3 | 05 §1, §3 |
| 1.10 Auditoría de configuración e identidad (base) | `auditoria_configuracion` con diff, `auditoria_identidad`, `GET /auditoria` básico | Opus 5.5 | medium | 2 | 03 §2.1, R-1 |
| 1.11 Medición de uso y región/KMS por tenant | `UsoTenant`, clave KMS por tenant, región como atributo | Opus 5.5 | medium | 2 | 07 §6, P-11-1 |
| 1.12 Observabilidad y health | OTel (ASP.NET, Npgsql, Marten, Wolverine), `/health/live|ready`, dashboard base | Sonnet 5.5 | medium | 2 | 04 §9 |
| 1.13 GitHub Actions | `ci.yml` (build, test, lint, contract snapshot), `imagenes.yml` (SBOM, Trivy, GHCR), Dependabot | Sonnet 5.5 | medium | 2 | 04 §11 |
| 1.14 Design system base y app shell | `createValidasoftTheme`, `ChipEstado`, `Folio`, `KpiCard`, `BarraProgresoEtapas`, `SelectorEmpresa`, `SelectorIdentificacion`, Storybook; `apps/web` login + `/plataforma` (tenants, empresas) | Opus 5.5 | medium | 6 | 06 §1–2 |
| 1.15 Cliente API generado | `packages/api-client` con orval desde `/openapi/v1.json`, fetcher ProblemDetails | Haiku 5.5 | low | 1 | 06 §1.2 |
| 1.16 Seed mínimo y demo | Tenant `demo` con dos empresas y usuarios por rol | Haiku 5.5 | low | 1 | 09 H1 |
| 1.17 Revisión de cierre | `/code-review` del hito, verificación de criterios, demo grabada, docs/ADR al día | Fable 5.1 | high | 2 | 09 |

### H2 — Bóveda del tercero

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 2.1 Abrir el hito | Issues H2 con criterios | Fable 5.1 | high | 1 | 09, 11 |
| 2.2 Agregado `Tercero` y `RelacionEntreTerceros` | Clave multipaís, sucesor, relaciones con rol/principal/vigencia, reconciliación de representantes | Fable 5.1 | xhigh | 4 | 03 §4.5, ADR-0010 |
| 2.3 Agregado `Documento` y versiones | `EstadoVersion`, `VersionCargada`, dedupe por hash, formularios con esquema versionado, `VersionActualCambiada` con campos cambiados, retiro con restitución | Fable 5.1 | xhigh | 5 | 03 §4 |
| 2.4 Subida en dos pasos y almacenamiento | URL prefirmada PUT/GET, verificación de hash, magic bytes, límites, claves S3 por ámbito, `DocumentoAccedido` | Opus 5.5 | high | 4 | 04 §6, 05 §2.4 |
| 2.5 Saga `VerificacionDocumental` | Antivirus (perfil), preview PDFium, preclasificación Haiku, extracción Opus/Sonnet con structured outputs, comparación con versión anterior, señales de manipulación básicas, fail-closed, consentimiento como precondición | Fable 5.1 | xhigh | 6 | 04 §7, 03 §4.3 |
| 2.6 Proveedor Claude y fixtures | `ClaudeVerificador`, `VerificadorFixture`, prompts por tipo canónico con caching, registro de coste; fixtures PDF anonimizados y respuestas grabadas | Opus 5.5 | high | 4 | 04 §7 |
| 2.7 Compartición tras aceptar y congelamiento | `CompartirConVinculo`, `ComparticionCongelada/Reanudada`, predicado de acceso, `Vinculo` mínimo con `InvitacionAceptada` | Fable 5.1 | high | 3 | 03 §4.3, ADR-0009 |
| 2.8 Catálogo canónico y esquemas de formulario | Semilla Anexo A, versionado, propuestas de tipo, conversión PorVinculo→Global | Sonnet 5.5 | medium | 3 | 03 Anexo A |
| 2.9 Proyecciones y lecturas | `BovedaTercero`, `ReferenciasDeVersion`, `TimelineDocumento`, SSE de bóveda | Opus 5.5 | medium | 3 | 03 §14 |
| 2.10 Endpoints de bóveda y públicos | `/terceros`, `/documentos`, `/public/v1/boveda/*` según 05 | Sonnet 5.5 | medium | 3 | 05 §2.3–2.5, 2.9 |
| 2.11 `apps/public`: bóveda y Dropzone móvil | `/boveda` con estado por empresa, `Dropzone` (cámara, fotos→PDF, HEIC), `TarjetaDocumentoBoveda`, `PanelIA` simplificado, personas relacionadas | Opus 5.5 | medium | 5 | 06 §3.2, §5 |
| 2.12 Pruebas de integración | Testcontainers Postgres+MinIO: dedupe, versión única, devolución, compartición tras aceptar, 403/404, sin IA | Opus 5.5 | high | 3 | 11 H2 |
| 2.13 Revisión de cierre | Como 1.17 | Fable 5.1 | high | 1 | 09 |

### H3 — Configuración y motor de exigencia

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 3.1 Abrir el hito | Issues H3 | Fable 5.1 | high | 1 | 09, 11 |
| 3.2 Tipos de documento por tenant y empresa | Mapeo canónico, restricción de ámbito, `reglaFrescura`, `nivelRevision`, `politicaDesactualizada`, overrides por empresa, configuración efectiva (B-4) | Opus 5.5 | high | 4 | 03 §10, 05 §4 |
| 3.3 Áreas, motivos y flujos versionados | `Flujo`/`FlujoVersion` publicable, etapas (paralelo por defecto), áreas con roles/usuarios, `AprobadoresRequeridos`, verificaciones requeridas, plantilla de certificado | Opus 5.5 | high | 4 | 03 §12–13 |
| 3.4 **`EvaluadorDeExigencia`** (función pura) | `ReglaDeExigencia`, `Condicion` Y/O/No, atributos `tercero.*`/`vinculo.*`, `TiposAceptados`, `DePersonaRelacionada`, tabla de casos exhaustiva | Fable 5.1 | xhigh | 4 | 03 §12 |
| 3.5 Reglas de selección de flujo y flujo por defecto | `ReglaDeSeleccionDeFlujo`, `FlujoPorDefecto` por (empresa, tipo de vínculo), `FlujoSeleccionado` | Fable 5.1 | high | 2 | 03 §12.1, R-2 |
| 3.6 Endpoints de configuración | `/tipos-documento`, `/empresas/{id}/tipos-documento`, `/areas`, `/motivos`, `/flujos` (+publicar, versiones, reglas-seleccion, por-defecto) | Sonnet 5.5 | medium | 3 | 05 §2.2 |
| 3.7 Editor de flujo en `apps/web` | Pasos, reglas con condiciones, áreas/etapas/SLA, vigencias, verificaciones, plantilla de certificado, reglas de selección, vista previa del tercero | Opus 5.5 | high | 6 | 06 §3.1 |
| 3.8 Semilla "Habilitación de proveedores" | Compras, Legal, Financiera, Cumplimiento; 2 aprobadores en bancaria; reglas por PN/PJ, monto y riesgo | Haiku 5.5 | low | 1 | 09 H3 |
| 3.9 Auditoría de configuración completa | Diff antes/después para cada entidad de configuración, invariante "sin registro no hay cambio" | Sonnet 5.5 | medium | 2 | R-1 |
| 3.10 Pruebas | Tabla de casos del evaluador 100 %, publicar congela, ámbito no ampliable, precedencia tenant→empresa→regla | Opus 5.5 | high | 2 | 11 H3 |
| 3.11 Revisión de cierre | Como 1.17 | Fable 5.1 | high | 1 | 09 |

### H4 — Expediente inicial y veredictos

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 4.1 Abrir el hito | Issues H4 | Fable 5.1 | high | 1 | 09, 11 |
| 4.2 **Agregado `Vinculo`** | Estados completos (incl. `Bloqueado` como suspensión), invariantes, invitación/aceptación/consentimientos, `requisitosCubribles`, tabla de casos "expediente abierto × evento" | Fable 5.1 | xhigh | 5 | 03 §5 |
| 4.3 **Agregado `Expediente`** | Snapshot de requisitos, etapas/áreas, veredicto `Aprobado|Observado` + `noAdmitirEstaVersion`, invalidación por versión nueva, `RechazarExpediente`, eximir, override con justificación, cierre automático, folio `-E-`, cambio de flujo, caducidad/reapertura | Fable 5.1 | xhigh | 7 | 03 §6 |
| 4.4 Saga `ProcesoDeValidacion` | Vínculo ↔ expediente ↔ compartición ↔ cierre; compensaciones; idempotencia | Fable 5.1 | xhigh | 3 | 03 §8 |
| 4.5 SLA, escalamiento, delegación y conflicto de intereses | Scheduled messages, `SlaEscalado`, `RevisionDelegada`, chequeo de identificación del revisor | Opus 5.5 | high | 3 | 03 §6.3 |
| 4.6 Enlace de acceso y autorregistro | Magic link de un solo uso, OTP, sesión corta, `POST /public/v1/autorregistro` (invisible), aceptación con evidencia (clic + OTP + IP) | Opus 5.5 | high | 3 | 07 §2.2, 05 §2.9 |
| 4.7 Endpoints de vínculos y expedientes | Todo 05 §2.3–2.4: crear (flujo opcional), requisitos, veredictos, rechazar, eximir, bloquear, cambiar flujo, reabrir, historial | Sonnet 5.5 | medium | 4 | 05 |
| 4.8 Proyecciones | `BandejaRevision`, `ExpedienteDetalle`, `TimelineExpediente/Vinculo`, `VinculoResumen`, `NovedadesDelTercero`, `MisPendientes` (consulta compuesta), KPIs básicos | Opus 5.5 | medium | 4 | 03 §14 |
| 4.9 `apps/web`: bandeja y expediente | Columnas por área, filtros, `VisorDocumento`, `PanelIA`, `PanelVeredicto`, `LineaDeTiempo`, asignación, cambio de flujo, SSE | Opus 5.5 | high | 8 | 06 §3.1 |
| 4.10 `apps/public`: expediente del tercero | `/e/:token`, requisitos con siguiente paso, formularios dinámicos (`@rjsf/mui`), observaciones, aceptación de T&C/autorización, novedades | Opus 5.5 | medium | 6 | 06 §3.2 |
| 4.11 Campana y correo de invitación | Notificación in-app + correo mínimo (el motor completo es H5) | Sonnet 5.5 | medium | 2 | 05 §8 |
| 4.12 Pruebas e2e | Playwright: solicitar → subir → observar → v2 → aprobar → `ExpedienteAprobado`; aislamiento entre empresas del mismo tenant; replay idempotente | Opus 5.5 | high | 4 | 11 H4 |
| 4.13 Revisión de cierre | Como 1.17 | Fable 5.1 | high | 2 | 09 |

### H5 — Certificado, notificaciones y webhooks (MVP)

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 5.1 Abrir el hito | Issues H5 | Fable 5.1 | high | 1 | 09, 11 |
| 5.2 Agregado `Certificado` y canonicalización | Contenido canónico (RFC 8785), hash, sello Ed25519, folio `-C-`, emisiones, suspensión/restablecimiento, PDF/A | Fable 5.1 | xhigh | 4 | 03 §7 |
| 5.3 Verificador público | `GET /public/v1/verificar/{folio}`, página `/verificar/:folio`, QR, `.well-known` de claves | Sonnet 5.5 | medium | 2 | 05 §2.9, 06 |
| 5.4 Motor de notificaciones | Fluid + MailKit, campana, SSE, catálogo completo de plantillas (05 §8), consolidación 1 correo/tercero/hecho/día, evidencia de entrega, preferencias | Opus 5.5 | high | 5 | 04 §8, 05 §8 |
| 5.5 Webhooks firmados y pull | Suscripciones, HMAC, `Validasoft-Event-Id`, reintentos con backoff, entregas, reenvío, deshabilitación, `GET /eventos`, precedencia API key ∩ webhook | Opus 5.5 | high | 4 | 05 §6–7 |
| 5.6 Catálogo de eventos públicos v1 | Sobre, payloads, `legacy.estadoTercero`, snapshot de contrato en CI | Opus 5.5 | medium | 2 | 05 §7 |
| 5.7 Saga `EmisionCertificado` | Completitud → folio → PDF → hash → Object Lock → webhook | Fable 5.1 | high | 2 | 04 §5 |
| 5.8 Pantallas de certificados | `/certificados`, detalle, revocar/reemitir, configuración de webhooks con log de entregas | Sonnet 5.5 | medium | 3 | 06 |
| 5.9 Seed demo y demo del MVP | Flujo completo con tres terceros, guion de demo, video | Haiku 5.5 | low | 2 | 09 H5 |
| 5.10 Pruebas | Webhook con firma válida/inválida, pull = webhooks, verificación compara hash, contract tests | Opus 5.5 | high | 3 | 11 H5 |
| 5.11 Revisión de cierre del MVP | Auditoría de seguridad básica + `/code-review` | Fable 5.1 | xhigh | 2 | 09 |

### H6 — Vigencias, revalidación y versiones nuevas

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 6.1 Abrir el hito | Issues H6 | Fable 5.1 | high | 1 | 09, 11 |
| 6.2 **Vigencia en dos capas** | `VersionAceptada` con `vigente_hasta` y `motivo`, cálculo con `reglaFrescura` efectiva, `EstadoAceptacion` | Fable 5.1 | xhigh | 3 | 02 §8, 03 §10 |
| 6.3 Scheduler de vencimientos | `VencimientoProgramado`, job diario por zona horaria del tenant, calendario hábil, avisos, gracia, `DocumentoVencidoEnVinculo`, recálculo del estado del vínculo | Fable 5.1 | high | 4 | 03 §10 |
| 6.4 **Saga `Revalidacion`** | Por empresa: críticos canónicos o propios, `Desactualizada`, plazo 15/90 días, `ConPendientes` informativo→bloqueante, cancelación, `EnValidacion` incluido | Fable 5.1 | xhigh | 5 | 03 §9, T-7 |
| 6.5 `Expediente(Renovacion)` y `Expediente(Actualizacion)` | Disparadores, alcance, convivencia con la invariante de un expediente abierto, ratificación (declaración, reinicio de frescura) | Fable 5.1 | xhigh | 4 | 03 §5–6 |
| 6.6 Cuentas bancarias y cuenta designada | `CuentaBancaria` global, `CuentaDesignada` propuesta por el tercero, saga `CambioCuentaDesignada` con 2 aprobadores, coherencia certificación↔formulario | Opus 5.5 | high | 4 | 03 §4.4, 05 |
| 6.7 Avisos consolidados | `FrescuraDocumentoTercero`, una secuencia por documento, plantillas de vencimiento | Opus 5.5 | medium | 2 | 02 §8.3 |
| 6.8 Endpoints y proyecciones | `/revalidaciones`, `/vencimientos`, `/ratificaciones`, `BandejaRevalidaciones`, `VencimientosProximos`, consulta de certificación con `bloqueado`/`motivoBloqueo` | Sonnet 5.5 | medium | 3 | 05 |
| 6.9 Pantallas | `/revalidaciones`, `/vencimientos` + Excel + notificar, comparar versiones, `/cuentas` del tercero, bóveda con vencimiento por empresa | Opus 5.5 | medium | 6 | 06 |
| 6.10 Pruebas de reloj simulado | T-15/T-5/T0, ratificar, renovar, dos tenants con aceptaciones distintas y un binario, A observa y B no lo ve, versión nueva alerta a todas una sola vez, E1–E10 del 02 | Opus 5.5 | high | 4 | 11 H6 |
| 6.11 Revisión de cierre | Como 1.17 | Fable 5.1 | high | 1 | 09 |

### H7 — Verificaciones externas, cumplimiento y revisión automática

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 7.1 Abrir el hito | Issues H7 | Fable 5.1 | high | 1 | 09, 11 |
| 7.2 `IVerificador` y adaptadores de listas restrictivas | Interfaz, proveedores con contrato vigente, evidencia PDF como versión, reconsulta periódica, `CoincidenciaPorAnalizar` → `Descartada|Confirmada` (confirmada suspende certificado) | Opus 5.5 | high | 5 | 03 §13 |
| 7.3 Estudio de cumplimiento | Campos del legado (listas, conflicto, riesgo 1–3, debida diligencia, evidencias, vigencia 30 días), bloqueante del cierre | Opus 5.5 | medium | 3 | 03 §13 |
| 7.4 UBO y PEP | Formularios globales, cadena de propiedad, cruce de listas por beneficiario, SARLAFT PorVinculo que los referencia | Fable 5.1 | high | 3 | 03 §4.5 |
| 7.5 Expiración por cambio de representante | `CambioDeRelacionDetectado` → confirmación humana o confianza alta → expiración de cédula y listas del saliente | Fable 5.1 | high | 2 | 03 §4.5 |
| 7.6 **Motor de decisión automática** | Niveles, umbrales 0,95/0,80, campos críticos, frescura, identidad, muestreo 5 %, presupuesto y degradación, `VeredictoAutomaticoEmitido` reversible, derecho a revisión humana | Fable 5.1 | xhigh | 5 | 02 §9, 04 §7.5 |
| 7.7 Detección de manipulación completa y equidad | Señales de alteración como hecho; métricas de equidad por tipo de persona/región | Opus 5.5 | high | 3 | 04 §7 |
| 7.8 Cola de excepciones y pantallas | `/excepciones`, `/configuracion/tenant/revision-automatica`, informe SAGRILAFT, PanelIA con nivel | Opus 5.5 | medium | 4 | 06 |
| 7.9 Pruebas | Verificación bloqueante impide cierre; `SinCambios` aprueba sola; cambio de representante va a humano; Cámara de 45 días se observa sola | Opus 5.5 | high | 3 | 11 H7 |
| 7.10 Revisión de cierre | Con Cumplimiento | Fable 5.1 | high | 1 | 09 |

### H8 — Operación masiva, informes y administración

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 8.1 Abrir el hito | Issues H8 | Fable 5.1 | high | 1 | 09, 11 |
| 8.2 Invitación masiva | `InvitacionMasivaSaga`, Excel/CSV, lote con estado, pausa/reanudación, reintentos de correo | Opus 5.5 | high | 4 | 03 §16-bis |
| 8.3 Campañas de renovación | Ventana, recordatorios, seguimiento | Sonnet 5.5 | medium | 2 | P-7-4 |
| 8.4 Ficha 360, informes y KPIs | `Tercero360`, informe de estado (Excel), `KpisTenant/Flujo/Empresa` con mediana y avisos por documento | Opus 5.5 | medium | 4 | 03 §14, 06 |
| 8.5 Auditoría consultable y exportable | `/auditoria` con pestañas y diff, ZIP firmado de expediente y bóveda | Opus 5.5 | medium | 3 | R-1 |
| 8.6 Habeas data formal | Solicitud, evaluación de retención, supresión/retiro, constancia, lado empresa | Fable 5.1 | high | 3 | 07 §8 |
| 8.7 Planes, límites, marca blanca y dominio de correo | `PlanDto`, límites con datos de medición, logo/colores por empresa, DKIM | Opus 5.5 | medium | 4 | P-11-x |
| 8.8 Plantillas Liquid restringidas y digest | Editor con vista previa, variables validadas, digest diario | Sonnet 5.5 | medium | 2 | 05 §8 |
| 8.9 Embed y PWA offline | `/embed/expediente/{token}` con postMessage, cola offline de subidas | Opus 5.5 | medium | 3 | 06 §3.3 |
| 8.10 Delegación, separación de funciones, acceso de soporte | Delegaciones con fechas, política de separación, concesión temporal a Plataforma | Sonnet 5.5 | medium | 2 | 07 |
| 8.11 Pruebas | Lote de 1 000, KPIs sin mocks, ZIP verificable, supresión con constancia | Opus 5.5 | high | 3 | 11 H8 |
| 8.12 Revisión de cierre | Como 1.17 | Fable 5.1 | high | 1 | 09 |

### H9 — SDKs e integración con SRM, Portal y Marco

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 9.1 Abrir el hito | Issues H9 (en los tres repos) | Fable 5.1 | high | 1 | 09, 11 |
| 9.2 `Validasoft.Contratos` y `Validasoft.Sdk` | NuGet multi-target net7/8/10, cliente generado, `WebhookVerifier`, sandbox por API key | Opus 5.5 | medium | 3 | 05 §9, 08 §2 |
| 9.3 `@validasoft/sdk` | TS generado + HMAC | Haiku 5.5 | low | 1 | 08 §2 |
| 9.4 **SRM: flag y cliente** | `ValidacionDocumental = Legacy|Validasoft` por empresa, invitación crea expediente, apagado de jobs legado | Fable 5.1 | high | 4 | 08 §3 |
| 9.5 **SRM: receptor de webhooks** | Endpoint firmado, dedupe por `Validasoft-Event-Id`, cola propia, mapa evento→acción, aplicación en AyF al certificar, `legacy.estadoTercero` | Fable 5.1 | xhigh | 5 | 08 §3.2 |
| 9.6 Portal: embed y retiro de escritura | Sesión de tercero, iframe de bóveda, retiro de `provportal` y de `Adjuntos*` | Opus 5.5 | high | 4 | 08 §4 |
| 9.7 Marco: consulta y respaldo | `GET /consultas/certificacion` con ETag, tabla local `adp_cs.validasoft_certificacion_local`, degradación `fuenteRespaldo` | Opus 5.5 | high | 3 | 08 §5 |
| 9.8 Pruebas de integración cruzada | Flag `Validasoft` vs `Legacy`; Portal no escribe adjuntos; Marco responde desde respaldo | Opus 5.5 | high | 3 | 11 H9 |
| 9.9 Revisión de cierre | Como 1.17 | Fable 5.1 | high | 1 | 09 |

### H10 — Importador y piloto de migración

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 10.1 Abrir el hito | Issues H10, elección del tenant piloto | Fable 5.1 | high | 1 | 09 |
| 10.2 **Mapeo legado → bóveda/validación** | `ClaveLegado` → UUID v5, versión actual = última del tercero, espejos divergentes `Desactualizada` a 90 días, huérfanos, usuarios por correo, relaciones, cuentas | Fable 5.1 | xhigh | 5 | 08 §6 |
| 10.3 CLI de importación | `--tenant --empresa --fuente --dry-run --reanudar`, CSV de rechazos, `/importacion/*` | Opus 5.5 | high | 4 | 08 §6 |
| 10.4 Binarios y re-verificación | Copia a MinIO con hash, Batches API solo con consentimiento | Opus 5.5 | medium | 2 | 08 §6.4 |
| 10.5 Sincronización inversa y rollback | Suscriptor que escribe en tablas legado 90 días, ensayo de rollback, tercero compartido en coexistencia | Fable 5.1 | high | 4 | 08 §7 |
| 10.6 Piloto en pruebas | Dry-run, corte, comunicación a proveedores, métricas | Opus 5.5 | medium | 3 | 08 §7 |
| 10.7 Revisión de cierre | Como 1.17 | Fable 5.1 | high | 1 | 09 |

### H11 — Endurecimiento y retiro del legado

| Tarea | Entrega | Modelo | Esfuerzo | d-p | Doc |
|---|---|---|---|---|---|
| 11.1 Pruebas de carga | k6: `/consultas/certificacion` 500 rps p99 < 50 ms, `/verificar` 100 rps | Sonnet 5.5 | medium | 2 | 04 §12 |
| 11.2 Backups y restauración automatizada | pgBackRest/WAL, `mc mirror`, prueba de restauración en pipeline | Opus 5.5 | high | 3 | 04 §10 |
| 11.3 Observabilidad y dashboards completos | Métricas de negocio e IA, alertas operativas | Sonnet 5.5 | medium | 2 | 04 §9 |
| 11.4 Seguridad: Object Lock compliance, ClamAV, sello de tiempo, auditoría externa | Perfiles de producción, RFC 3161 opcional, hallazgos cerrados | Fable 5.1 | high | 4 | 07 |
| 11.5 Retiro del legado por empresa migrada | Apagar `PortalProveedorERP/*`, `background/*`, `AdjuntosConstructora`, jobs del Portal | Opus 5.5 | medium | 3 | 08 §8 |
| 11.6 Opcionales | RabbitMQ, Helm, regiones adicionales | Opus 5.5 | medium | 4+ | 04 |
| 11.7 Revisión final | Auditoría de arquitectura y cierre de ADRs | Fable 5.1 | xhigh | 2 | 09 |

## 3. Cómo usar esta guía en Claude Code

1. Al abrir un hito: sesión **Fable 5.1 · high** con 09, 11 y el documento dueño; pedirle que cree los issues con criterios de aceptación y el orden de dependencia.
2. Por cada issue: sesión con el **modelo y esfuerzo de la tabla**, cargando solo el documento dueño y el bloque del 11 correspondiente; un PR por issue.
3. Antes de la demo: sesión **Fable 5.1 · high** con `/code-review` sobre el hito y verificación de los criterios de 09; actualizar 11 y, si hubo decisiones nuevas, el registro y un ADR.
4. Si una tarea marcada Sonnet/Haiku se complica (invariantes, concurrencia, seguridad), subir a Opus/Fable y a `high` en lugar de insistir.

## Decisiones abiertas

1. **Composición real del equipo y fecha de inicio** (A-26). *Propuesta:* 3 personas desde 2026-11-11. *Quién decide:* Producto.
2. **Si el presupuesto de modelo prioriza Fable para las revisiones de cierre o también para las implementaciones de agregados** (filas marcadas xhigh). *Propuesta:* Fable en ambas; es donde se evita rehacer. *Quién decide:* Arquitectura.

## Fuentes verificadas

Ver 09 y 11.
