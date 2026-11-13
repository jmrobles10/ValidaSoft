# Convenciones de la API

## Base, formato y versionado

- Base: `https://{host}/api/v1` (autenticado) y `https://{host}/public/v1` (sesión de tercero o anónimo). El versionado va en la URL.
- Formato: JSON en `camelCase`; fechas ISO-8601 UTC con `Z`; fechas sin hora como `YYYY-MM-DD`; identificadores UUID.
- Rutas y campos en español sin tildes. Los enums son textuales en `PascalCase` (por ejemplo `"EnRevision"`).
- Campo ausente = no aplica; `null` explícito = sin valor.
- La especificación OpenAPI 3.1 se sirve en `/openapi/v1.json`; esta referencia sale de ese contrato.

## Autenticación y alcance

- `X-Api-Key: vsk_<entorno>_<tenant>_<aleatorio>` para integraciones, o `Authorization: Bearer <jwt>` para personas (sesiones embebidas y directas) y terceros. Nunca las dos.
- Con la API key de un sistema integrado, la cabecera `X-Usuario-Delegado: <jwt>` actúa en nombre de una persona: JWT RS256/ES256 firmado con la clave privada del sistema, `iss` = código del sistema, `aud` = `validasoft`, `exp` de 5 minutos como máximo, `sub`, `name`, `email` y `jti` obligatorio y de un solo uso. El permiso efectivo es la intersección de lo que permite la llave y los roles de la persona.
- El tenant va implícito en la credencial. La **empresa** (razón social del grupo) se indica con `X-Empresa: <empresaId>` en los recursos de empresa; si la credencial solo tiene alcance a una empresa, el encabezado es opcional. `X-Tenant: <slug>` lo usa solo Plataforma en `/plataforma/*`.
- Un recurso de otro tenant o de otra empresa responde `404 no-encontrado`: no se revela su existencia.

## Idempotencia, concurrencia y paginación

- `Idempotency-Key` (cadena libre de hasta 128 caracteres) acompaña a los `POST` que crean o disparan efectos. Misma clave y mismo cuerpo devuelven la misma respuesta con `Idempotent-Replayed: true`; el mismo cuerpo distinto responde `422 clave-idempotencia-reutilizada`; si falta, `400 clave-idempotencia-requerida`. La memoria dura 24 horas.
- Las respuestas que traen un secreto (`valor` de una API key, `secreto` de un webhook, `token` de una sesión) lo entregan una sola vez: al repetir la petición con la misma clave se omite el secreto y se indica en `secretosOmitidos`.
- Los agregados con versión devuelven `ETag: "v{n}"`; `If-Match` en `PUT`, `PATCH` y veredictos protege contra cambios simultáneos: `412 version-desactualizada` o, en un veredicto sobre una versión superada, `409 version-superada`.
- Paginación por cursor opaco: `?limite=50&cursor=…` (máximo 200). Los filtros son parámetros planos; las listas van separadas por coma y los rangos usan `desde` y `hasta`.
- Los binarios nunca viajan por la API: se suben y descargan con URLs prefirmadas de corta duración. Límite de 20 MB; PDF, JPG, PNG o WebP.

## Errores (ProblemDetails)

- Los errores siguen RFC 9457 (`application/problem+json`) con un `codigo` estable para programar contra él, `detail` en español para mostrar y, cuando aplica, `errores` por campo. `traceId` sirve para pedir soporte.
- Estados que usa la API: `400` solicitud inválida, `401` no autenticado o aserción inválida, `403` sin permiso, `404` no encontrado (también si es de otro tenant o empresa), `409` conflicto de estado, `410` enlace usado o vencido, `412` versión desactualizada, `413` archivo demasiado grande, `415` tipo de archivo no soportado, `422` regla de negocio o validación, `429` límite de peticiones, `503` servicio no disponible.
- Una operación que tuvo éxito nunca devuelve 4xx. Los avisos que no bloquean (por ejemplo, cupo de IA agotado) viajan en el cuerpo `2xx` como `advertencias: [{codigo, mensaje}]` y en la cabecera `Validasoft-Advertencias`.
- Cada operación lista debajo los códigos que lanza directamente su código; hay códigos comunes (por ejemplo `no-encontrado`, `sin-permiso`, `estado-invalido`) que pueden salir de reglas compartidas. El catálogo completo está en `docs/05-api-contratos-y-webhooks.md` §3.3.

```json
{
  "type": "https://docs.validasoft.co/errores/requisito-no-en-alcance",
  "title": "El requisito no está en el alcance del área",
  "status": 409,
  "detail": "El área 'financiera' no revisa 'CAMARA_COMERCIO' en el flujo 'habilitacion-proveedores' v3.",
  "instance": "/api/v1/expedientes/01J9…/requisitos/01J9…/veredicto",
  "codigo": "requisito-no-en-alcance",
  "traceId": "00-4bf9…-01",
  "errores": [ { "campo": "motivos", "mensaje": "Debe indicar al menos un motivo para observar." } ]
}
```

## Eventos, webhooks y tiempo real

- Las integraciones reciben los eventos públicos por webhooks firmados o en modo pull (`GET /api/v1/eventos`) y por SSE (`/api/v1/eventos/stream`). El catálogo de eventos y el formato de la firma están en `docs/05-api-contratos-y-webhooks.md` §6 y §7.
- Las pantallas se mantienen al día con streams SSE de avisos de cambio (`/api/v1/notificaciones/stream` y `/public/v1/notificaciones/stream`): solo avisan qué cambió, nunca datos.
- Límites: por credencial (`429`, cabeceras `RateLimit-*`) y, en `/public/v1`, por dirección IP. Cada respuesta lleva `X-Correlation-Id` para rastrear la petición.
