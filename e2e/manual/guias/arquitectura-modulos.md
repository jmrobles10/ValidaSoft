---
titulo: "Módulos de la solución"
seccion: "Arquitectura"
orden: 11
resumen: "Los proyectos .NET y sus dependencias, los módulos de la capa de aplicación, el patrón de un caso de uso, el modelo de errores y los paquetes del frontend."
---

Validasoft es un **monolito modular**: un solo repositorio, una sola base de datos y un solo proceso, organizado en proyectos que solo se pueden referenciar en un sentido (decisión ADR-0001, `docs/adr/0001-monolito-modular.md`). Extraer un módulo en el futuro sería cambiar el transporte local de mensajes por uno de red.

## La solución .NET

La solución es `Validasoft.slnx`. Todo es .NET 10 con `TreatWarningsAsErrors`, salvo `Contratos` y `Sdk`, que también compilan para .NET 7 y 8 mientras haya sistemas integrados que no han migrado.

| Proyecto | Qué contiene | Referencias |
|---|---|---|
| `src/Validasoft.Dominio` | Agregados, eventos, invariantes, enumeraciones textuales, cálculo de vigencias, evaluación de condiciones de exigencia, validadores de identificación por país. | Solo `JasperFx.Events` (las abstracciones de eventos de Marten). |
| `src/Validasoft.Contratos` | Los DTO públicos, los tipos de evento de webhook y los enumerados de la API. Es la superficie que ve un integrador. | Nada. |
| `src/Validasoft.Aplicacion` | Los casos de uso, agrupados por módulo, con sus endpoints de Wolverine.Http; consultas y escrituras con Marten; procesos periódicos. | `Dominio` y `Contratos`. |
| `src/Validasoft.Infraestructura` | Configuración de Marten y Wolverine, almacén S3, SMTP, OpenAI, PDF, QR, criptografía, passkeys, SSO, límites y middleware. | `Aplicacion`. |
| `src/Validasoft.Api` | El host: `Program.cs`, la composición de servicios, `appsettings` y el `Dockerfile`. | `Infraestructura` y el adaptador del Portal. |
| `src/Validasoft.Sdk` | El cliente .NET de la API v1: llaves, idempotencia, reintentos, identidad delegada, sandbox y verificador de webhooks. Sin dependencias fuera de la BCL. | `Contratos`. |
| `src/Validasoft.Adaptadores.Portal` | El adaptador de origen para importar los históricos del Portal de proveedores. Opcional: sin él, el importador responde 404. | `Aplicacion`. |
| `docs/ejemplos/IntegradorSrm` | Un ejemplo ejecutable de un sistema integrador. | El SDK. |

Pruebas: `tests/Validasoft.Dominio.Tests` (dominio puro), `tests/Validasoft.Integracion.Tests` (la API contra PostgreSQL y MinIO reales con Testcontainers y Alba), `tests/Validasoft.Sdk.Tests`, `tests/Validasoft.Adaptadores.Portal.Tests`, `tests/carga` (k6 sobre la consulta de certificación) y `e2e/` (Playwright; no corre en el CI, se lanza bajo demanda).

## Módulos de la capa de aplicación

Cada carpeta de `src/Validasoft.Aplicacion` es un módulo; la de `Dominio` con el mismo nombre guarda sus agregados.

| Módulo | Responsabilidad |
|---|---|
| `Vinculos` | Vínculos: crear, invitar, bloquear, desvincular, reintegrar, vigencias y vencimientos, contactos, cuenta designada, referencias externas. |
| `Expedientes` | El proceso de validación: abrir, requisitos, veredictos por área y etapa, observar y reenviar, bandeja, plazos de cargue, SLA, renovación y revalidación. |
| `Flujos` | El editor de flujos aprobatorios (versiones, borradores y publicación) y la selección del flujo de un vínculo. |
| `Documentos` | La bóveda del tercero: carga en dos pasos, versiones, compartición, vigencias, tipos de documento, plantillas y ayudas. |
| `Ficha` | La ficha maestra por secciones, las propuestas desde un documento, el cruce de la ficha con los soportes, la declaración PEP y los catálogos. |
| `Certificados` | Emisión, PDF, sello Ed25519, verificación pública y revocación. |
| `Verificaciones` | Listas restrictivas, estudio de cumplimiento, matriz de riesgo y monitoreo. |
| `Revision` | La revisión automática con IA (Manual, Asistido y Automático) y la bandeja de excepciones. |
| `Ia` | El análisis de documentos con OpenAI, el cupo por empresa y el consumo. |
| `Identidad` | Personas, roles, sistemas integrados, API keys, ingreso directo (código y passkeys), SSO e identidad delegada. |
| `Interesado` | Lo que ve el tercero al abrir un enlace: canje, código OTP, sesión y renovación. |
| `Consentimientos` | Textos legales versionados y los consentimientos que acepta el tercero. |
| `Configuracion` | Empresas, áreas, tipos de vínculo, motivos, plantillas de correo, dominio de correo y accesos de soporte. |
| `Invitaciones` y `Campanias` | Invitación masiva por lotes y campañas de renovación, con su procesamiento por tandas. |
| `Delegaciones` | Delegación de la revisión por ausencias. |
| `Notificaciones` | Eventos públicos, webhooks, campana, correos a personas y avisos de cambio en vivo. |
| `Informes` | Informes de estado y de vínculos, auditoría de accesos, exportaciones y ZIP del expediente. |
| `Habeas` | Solicitudes de los titulares, retención, supresión y exportación de la bóveda. |
| `Integracion` | La consulta de certificación para los ERP, con `ETag`. |
| `Plataforma` | La consola de Plataforma, los planes y límites de los grupos y la medición de uso. |
| `Importacion` | El importador genérico de históricos, con corridas que se reanudan desde su cursor. |
| `Sandbox` | El entorno de pruebas aislado de un grupo. |
| `Comun` | Lo transversal: el contexto de la petición, los permisos, el almacenamiento, el correo y los mapeos. |

> **Verdad:** `docs/04` §2 pide que un módulo no use tipos internos de otro y que las pruebas de arquitectura (NetArchTest) lo verifiquen. Hoy los módulos de `Aplicacion` comparten el espacio de nombres base y no hay un proyecto de pruebas de arquitectura: el aislamiento real es el de los proyectos (`Dominio` no conoce nada y `Aplicacion` no conoce la infraestructura) y el del acceso a datos por tenant.

## Cómo es un caso de uso

Un caso de uso (un *vertical slice*) es una clase estática con métodos marcados con el verbo y la ruta, que Wolverine.Http convierte en endpoint. Un archivo `*Endpoints.cs` agrupa los de un recurso:

```csharp
[WolverinePost("/api/v1/vinculos/{id}/bloquear")]
[Authorize(Policy = Permisos.VinculosBloquear)]
public static async Task<VinculoDto> Bloquear(Guid id, BloquearVinculoDto dto, SesionDeEmpresa empresa, ...)
{
    var stream = await empresa.VinculoParaEscribirAsync(id, VersionEsperada(http), ct);   // el stream del agregado
    var evento = stream.Aggregate!.Bloquear(dto.Motivo, dto.Hasta, ahora);                // la regla vive en el dominio
    await CicloDeCertificados.AlBloquearAsync(empresa.Sesion, id, evento.Motivo, ahora, ct);   // suspende el certificado
    return await GuardarAsync(empresa, stream, evento, ct);                               // eventos + outbox, una transacción
}
```

El método recibe la `SesionDeEmpresa` (que ya trae el grupo y la empresa activa y solo ve filas de esa empresa), carga el stream del agregado con la versión que el cliente leyó (cabecera `If-Match` con el `ETag` de la lectura: concurrencia optimista, 412 si cambió), llama al método de dominio, que devuelve el evento nuevo o lanza una `ReglaDeNegocioException`, y confirma todo en una sola transacción, con los mensajes del outbox. Las reglas viven en el agregado, no en el endpoint: así las prueba `Validasoft.Dominio.Tests` sin base de datos.

### Errores

Toda regla incumplida lanza `ReglaDeNegocioException(tipo, codigo, mensaje)`. El manejador global la convierte en `application/problem+json` (RFC 9457) con `codigo` estable y `traceId`:

| Tipo de falla | HTTP |
|---|---|
| `Validacion` | 422 |
| `NoEncontrado` | 404 (también cuando el recurso es de otro grupo o empresa) |
| `Conflicto` | 409 |
| `Prohibido` | 403 |
| `NoAutenticado` | 401 |
| `Expirado` | 410 |
| `DemasiadosIntentos` | 429 |
| `DemasiadoGrande` | 413 |
| `TipoNoSoportado` | 415 |
| `NoDisponible` | 503 |

Además: 400 para una petición mal formada, 412 `version-desactualizada` cuando falla la concurrencia optimista y 500 `error-interno` (solo con el `traceId`; en desarrollo trae el detalle). Quien integra decide con el `codigo`, no con el texto.

### Convenciones

- El dominio, las rutas, los eventos, las tablas y los textos van en español; el inglés solo aparece en identificadores que imponen las librerías.
- Los estados son enumeraciones textuales (`string` en la base), nunca enteros.
- Las fechas van en UTC en la base y en la API; el calendario del negocio (vencimientos, «hoy») usa la zona del grupo (por omisión `America/Bogota`).
- Los DTO públicos viven solo en `Contratos`.

## El frontend

El frontend es un monorepo de **pnpm** con dos apps y tres paquetes (`pnpm-workspace.yaml`):

| Paquete | Qué es |
|---|---|
| `apps/web` | La app de la empresa y la consola de Plataforma. React 19, TypeScript, MUI 9, TanStack Query 5 (estado del servidor), Zustand 5 (estado del cliente), i18next (todos los textos en `i18n/es-CO*.ts`) y `react-router` con rutas en el hash. Una carpeta por característica en `src/features` (vínculos, expedientes, flujos, configuración, informes, cumplimiento, plataforma…). |
| `apps/public` | El portal del tercero, el verificador de certificados y las pantallas embebidas. Mismo stack, rutas normales y bundle propio y liviano. |
| `packages/ui` | `@validasoft/ui`: el sistema de diseño compartido (componentes sobre MUI, selectores de fecha, esqueletos de carga, valores largos, estados de página). |
| `packages/api-client` | `@validasoft/api-client`: el cliente de la API generado con Orval desde `openapi/v1.json` (`pnpm generar:api`), más el manejo de sesión, errores y el cliente de tiempo real (SSE). |
| `packages/sdk` | `@validasoft/sdk`: el SDK de TypeScript para backends (Node 20+): llaves, idempotencia, sandbox, identidad delegada y verificador de webhooks. |
| `e2e` | `@validasoft/e2e`: pruebas de punta a punta con Playwright y los recorridos del manual. |
| `marca` | Generador de los recursos de marca (logotipo, íconos y favicon). |

Las dos apps usan la misma API; la diferencia es el esquema de autenticación: la app de la empresa usa la API key o la sesión de una persona en `/api/v1`, y el portal del tercero usa su sesión en `/public/v1` ([Seguridad](guia:arquitectura-seguridad)).
