# ADR-0001 — Monolito modular con dos roles de host (Api y Worker)

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 | — |

## Contexto
Validasoft debe instalarse on-premise en clientes con equipos de operación pequeños y también en nube; el dominio todavía está madurando (límites entre módulos pueden moverse en los primeros hitos); el flujo de validación requiere transacciones que combinan eventos, proyecciones y mensajes salientes.

## Decisión
Un solo código base organizado en **módulos aislados** (`Tenancy, Identidad, Catalogo, Terceros, Expedientes, Documentos, Verificacion, Certificados, Vencimientos, Notificaciones, Webhooks, Auditoria, Integracion`) desplegado con **dos roles** de la misma imagen: `Api` (HTTP) y `Worker` (colas, scheduled messages, sagas, previews, IA). Los módulos solo se comunican por mensajes (Wolverine) o por consultas de lectura explícitas; un test arquitectónico (NetArchTest) impide referencias cruzadas y consultas sin tenant.

## Alternativas descartadas
- **Microservicios desde el inicio**: 8–12 contenedores, sagas distribuidas para el caso más simple, coste operativo inasumible on-premise, límites de contexto aún inestables.
- **Monolito sin módulos**: repetiría la mezcla de responsabilidades del SRM legado.

## Consecuencias
- Una base de datos, una transacción para evento + outbox.
- Escalado horizontal por rol (más `Worker` para IA/previews, más `Api` para carga HTTP).
- Extraer un módulo a servicio aparte más adelante = cambiar el transporte local por RabbitMQ sin tocar handlers.

## Cómo se revierte
Un módulo con límites probados puede moverse a su propio host conservando sus mensajes; el ADR que lo decida enlazará a este.
