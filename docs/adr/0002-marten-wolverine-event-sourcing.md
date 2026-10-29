# ADR-0002 — Marten + Wolverine sobre PostgreSQL para event sourcing, mensajería y outbox

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 | — |

## Contexto
El flujo de validación necesita trazabilidad completa (quién hizo qué, sobre qué versión, cuándo), máquinas de estado explícitas y proyecciones de lectura (bandejas, informes). El legado perdió historial al borrar y recrear filas y repartió estados en tres enums. El equipo domina .NET y SQL; se decidió PostgreSQL como base nueva.

## Decisión
- **Marten** como event store y base documental sobre PostgreSQL 17: streams por agregado (`Vinculo`, `Expediente`, `Documento`, `Certificado`), proyecciones inline (lectura tras escritura) y async (bandejas, KPIs, vencimientos), *conjoined tenancy* en el esquema `validacion`.
- **Wolverine** como bus en proceso y transporte sobre PostgreSQL: outbox transaccional integrado con Marten, colas durables por módulo, *scheduled messages* (SLAs, enlaces, recordatorios), sagas persistidas (`ProcesoDeValidacion`, `VerificacionDocumental`, `EmisionCertificado`, `InvitacionMasiva`, `Revalidacion`), reintentos y cola de errores.
- Event sourcing **solo** donde aporta: expedientes, vínculos, documentos, certificados. Catálogos, usuarios y configuración son documentos CRUD con auditoría de cambios.

## Alternativas descartadas
- **SQL Server + EF Core + event store propio**: event sourcing a mano, sin proyecciones ni outbox integrados; perpetúa el acoplamiento con el mundo actual.
- **EventStoreDB/Kurrent + RabbitMQ**: dos sistemas más que operar on-premise; Marten cubre el caso con una sola base.
- **Event sourcing en todo**: complejidad innecesaria en catálogos.

## Consecuencias
- Un único `IDocumentSession` transaccional para evento + proyección inline + mensaje saliente.
- Replay de proyecciones y auditoría nativa (`TimelineExpediente` sale del stream).
- Requiere disciplina: eventos inmutables y versionados (upcasters), proyecciones idempotentes.
- RabbitMQ queda como transporte opcional sin cambiar handlers.

## Cómo se revierte
Los eventos son JSON en PostgreSQL; migrar a otro store es exportar streams. Las proyecciones se regeneran.
