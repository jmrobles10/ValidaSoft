---
titulo: "Contexto y contenedores"
seccion: "Arquitectura"
orden: 10
resumen: "Quién usa Validasoft, con qué sistemas se conecta y qué piezas lo forman: las dos apps, el proceso .NET, PostgreSQL, el almacén de archivos y los servicios externos."
---

Validasoft es una plataforma que valida a terceros (proveedores, clientes, empleados, aliados) para varias empresas a la vez. Esta guía y las siguientes de la sección **Arquitectura** describen cómo está construida. Todo lo que dicen se comprobó contra el código y contra `docs/04-arquitectura-plataforma.md`; donde el código difiere del diseño original, se dice.

> **Verdad:** el diseño original (`docs/04` §1) preveía un proceso `Api` y otro `Worker` con la misma imagen. Hoy **no existe un Worker separado**: la API, las colas, el daemon de eventos y los procesos periódicos corren en un solo proceso (`Validasoft.Api`) y una sola imagen Docker. Separarlos es posible más adelante porque la mensajería ya pasa por colas durables, pero no está hecho.

## Contexto: quién usa Validasoft

![Contexto de Validasoft: las personas y sistemas que lo usan y los servicios externos que consume](diagramas/contexto.svg)

| Actor | Cómo entra | Qué hace |
|---|---|---|
| Tercero | Portal del tercero (`/`): enlace de un solo uso del correo, con un código si el tipo de vínculo lo exige. Sesión de 30 minutos en cookie, con renovación. | Acepta invitaciones, sube documentos, llena su ficha, responde observaciones, descarga su certificado. |
| Personas de la empresa (administrador, revisor, oficial, auditor) | App web (`/app/`): ingreso directo (código por correo y passkey), SSO del cliente, o dentro de un `<iframe>` del sistema integrado con identidad delegada. | Configura, revisa expedientes, decide, bloquea, consulta informes y auditoría. |
| Operador de Plataforma | Consola `/plataforma` (`/app/#/plataforma`) de la misma app web: ingreso directo con passkey obligatoria o sesión del backoffice. | Da de alta grupos y empresas, fija planes, configura la IA, las listas y los secretos. |
| Sistema integrador | API con `X-Api-Key` (más `X-Empresa`); opcionalmente `X-Usuario-Delegado` para actuar como una persona suya. | Invita terceros, consulta certificaciones, recibe eventos por webhook o pull, embebe pantallas. |
| Público | Sin sesión: `/verificar/{folio}` y `GET /public/v1/verificar/{folio}`. | Comprueba que un certificado es auténtico, está vigente y no fue alterado. |

### Servicios externos detrás de interfaces

Ningún proveedor externo está cableado en el dominio: cada uno vive detrás de una interfaz de la capa de aplicación y se elige por configuración. Sin ellos, Validasoft funciona (la IA cae a revisión manual y las listas a un proveedor simulado en desarrollo).

| Servicio | Interfaz | Implementaciones |
|---|---|---|
| Análisis de documentos con IA | `IAnalizadorDeDocumentos` | `AnalizadorOpenAi` (la llave de OpenAI es de cada empresa) y `AnalizadorSimulado` (desarrollo, pruebas y sandbox, sin red). |
| Listas restrictivas | `IVerificadorDeListas` | `VerificadorInfolaft` (cuenta única de la plataforma), `VerificadorStradata`, `VerificadorCompliance` y `VerificadorSimulado`. |
| Identidad | `IProveedorDeIdentidad` | `ProveedorLocalDeCodigos` (código por correo), `ProveedorOidc` (cualquier OpenID Connect) y `ProveedorWorkOs`. |
| Correo | `ITransporteDeCorreo`, `IProveedorDeDominiosDeCorreo` | SMTP con MailKit (Mailpit en desarrollo); el dominio propio por empresa usa un proveedor simulado. |
| Almacén de archivos | `IAlmacenDeArchivos` | `AlmacenS3` sobre el SDK de AWS: sirve MinIO u otro S3 compatible. |
| Importación de históricos | `IAdaptadorDeOrigen` | El adaptador del Portal de proveedores (SQL Server de solo lectura o un archivo); opcional. |

## Contenedores: qué piezas lo forman

![Contenedores de Validasoft: apps React, proceso .NET único, PostgreSQL con Marten, almacén S3, SMTP y servicios externos](diagramas/contenedores.svg)

| Pieza | Tecnología | Responsabilidad |
|---|---|---|
| `apps/web` | React, TypeScript, MUI, TanStack Query, Zustand; `react-router` con rutas en el hash (`/app/#/e/{empresa}/…`) | App de la empresa y consola de Plataforma. Se puede embeber en un `<iframe>` de un sistema integrado. |
| `apps/public` | React, TypeScript, MUI; con manifest web (se instala en el celular) | Portal del tercero, verificador público de certificados y pantallas embebidas (`/embed/{componente}`). |
| `Validasoft.Api` | .NET 10, Wolverine.Http, OpenAPI | Un solo proceso: sirve la API (`/api/v1` para llaves y sesiones de personas, `/public/v1` para el tercero y el público), sirve las dos apps estáticas, corre las colas locales durables, el daemon de eventos y los procesos periódicos. Expone `/health/live`, `/health/ready` y `/openapi/v1.json`; en desarrollo, `/docs` (Scalar). |
| PostgreSQL 17 con Marten | Marten 9, Npgsql | Event store, documentos JSON, outbox e inbox de Wolverine y avisos entre servidores (`LISTEN/NOTIFY`). Un solo `DocumentStore`. |
| Almacén S3 | MinIO u otro S3 compatible | Los binarios de la bóveda. El API nunca proxea los bytes: entrega URLs prefirmadas. |
| SMTP | Cualquiera; Mailpit en desarrollo | Correo saliente. En el sandbox los correos no salen: quedan en un buzón por API. |
| Servicios externos | Ver arriba | IA, listas restrictivas e identidad del cliente. |

Por defecto la imagen única se publica con `src/Validasoft.Api/Dockerfile` (tres etapas: compila las dos apps con pnpm, publica la API y las junta en una imagen de ejecución) y escucha en el puerto 8080. Los detalles de arranque, perfiles y variables están en [Despliegue](guia:arquitectura-despliegue).

Las demás guías de arquitectura profundizan cada pieza: [módulos de la solución](guia:arquitectura-modulos), [datos y multi-tenencia](guia:arquitectura-datos-y-tenencia), [mensajería y tiempo real](guia:arquitectura-mensajeria-y-tiempo-real), [seguridad](guia:arquitectura-seguridad), [documentos](guia:arquitectura-documentos) y [ciclos de vida](guia:arquitectura-ciclos-de-vida).
