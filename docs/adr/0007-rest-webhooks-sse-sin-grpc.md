# ADR-0007 — API REST + webhooks firmados + SSE; sin gRPC en el MVP

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 | — |

## Contexto
Los consumidores son el SRM (.NET 7), el Portal (.NET 8), el ERP Marco, integradores externos y dos SPAs. Deben integrarse "fácil" y recibir cambios de estado sin sondear. El legado usaba callbacks HTTP sin firma ni idempotencia y jobs disparados por el otro sistema.

## Decisión
- **REST JSON v1** (`/api/v1`, OpenAPI 3.1, camelCase, ProblemDetails RFC 9457, `Idempotency-Key`, paginación por cursor, `ETag/If-Match`, rate limit) como única API pública; SDKs .NET y TS **generados** del contrato.
- **Webhooks salientes firmados** (`Validasoft-Signature: v1=hmac_sha256(secreto, "{ts}.{body}")`; **dedupe en el receptor por `Validasoft-Event-Id`**, mientras `Validasoft-Delivery` identifica cada intento (T-6); reintentos con backoff, log de entregas, reenvío manual) y modo **pull** (`GET /eventos?desde=cursor`) para consumidores detrás de firewall. El evento canónico de emisión de certificado es **`certificado.emitido`**; no existe `expediente.certificado` (T-15).
- **SSE** para las dos SPAs (bandejas y expediente del tercero en vivo).
- **Sin gRPC en el MVP.** Se reevaluará (ADR nuevo) solo para una consulta interna de muy alto volumen (p. ej. `consultas/certificacion` desde el ERP) si REST con ETag y caché no alcanza el objetivo de p99 < 50 ms a 500 rps.

## Alternativas descartadas
- **gRPC como API pública**: consumidores .NET 7/8 y portales web se integran peor (grpc-web, proxies), no hay generación de SDK tan directa para integradores no .NET, y el valor (binario, streaming) no aplica a un flujo documental dominado por URLs prefirmadas.
- **GraphQL**: los consumidores son sistemas, no exploradores de datos; complica el caché y la autorización por tenant.
- **Polling como único mecanismo**: repite el modelo del legado.

## Consecuencias
- Un contrato, tres mecanismos (petición, evento saliente, flujo en vivo) con la misma semántica de eventos públicos.
- Los eventos públicos llevan, durante la coexistencia, el campo `legacy.estadoTercero` para que SRM/Portal no cambien sus enums en la primera fase.
- Métricas de entrega de webhooks y de latencia de REST forman parte de la observabilidad obligatoria.

## Cómo se revierte
gRPC puede añadirse como fachada interna sobre los mismos casos de uso sin tocar el dominio.
