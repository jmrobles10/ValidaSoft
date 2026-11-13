# API · Notificaciones

25 operaciones en 4 recursos.

## Eventos

### `GET /api/v1/eventos`

**Listar eventos.** `?desde={cursor}&tipos=a,b&empresaId=&limite=50` (máx. 200). Sin `desde` empieza por el primero. `empresaId` equivale a `X-Empresa` (para el pull de un integrador con varias empresas, CN-4); una empresa fuera del alcance de la credencial responde 403 `alcance-insuficiente`.

**Permiso eventos:leer · API (`/api/v1`) · SDK: `listarEventos`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `desde` | query | texto | no |  |
| `tipos` | query | texto | no |  |
| `empresaId` | query | uuid | no |  |
| `limite` | query | entero | no |  |

**Respuestas**

- `200` OK — PaginaEventosDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de EventoPublicoDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].tipo` | texto | sí |  |
| `datos[].version` | entero | sí |  |
| `datos[].ocurridoEn` | fecha y hora | sí |  |
| `datos[].tenantId` | uuid | sí |  |
| `datos[].empresaId` | uuid o null | no |  |
| `datos[].cursor` | texto | sí |  |
| `datos[].datos` | JsonElement | sí |  |
| `cursor` | texto | sí |  |
| `hayMas` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/eventos" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": "texto",
      "version": 1,
      "ocurridoEn": "2026-11-12T15:00:00Z",
      "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "cursor": "texto",
      "datos": {}
    }
  ],
  "cursor": "texto",
  "hayMas": true
}
```

</details>

### `GET /api/v1/eventos/stream`

**Stream eventos.** SSE (docs/05 §3.4): `id` = cursor (se reanuda con `Last-Event-ID` o `?desde=`), `event` = tipo, `data` = el mismo sobre del modo pull. Consulta cada 2 s y manda un comentario de vida cada 15 s.

**Permiso eventos:leer · API (`/api/v1`) · SDK: `streamEventos`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `desde` | query | texto | no |  |
| `tipos` | query | texto | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/eventos/stream" \
  -H "X-Api-Key: $API_KEY"
```

</details>

### `GET /api/v1/eventos/tipos`

**Listar tipos evento.** Catálogo v1 de tipos de evento público (para elegir al suscribirse).

**Acceso: ver contrato · API (`/api/v1`) · SDK: `listarTiposEvento`**

**Respuestas**

- `200` OK — lista de TipoEventoPublicoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de TipoEventoPublicoDto | sí |  |
| `[].tipo` | texto | sí |  |
| `[].descripcion` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/eventos/tipos" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "tipo": "texto",
    "descripcion": "Texto de ejemplo."
  }
]
```

</details>

## Notificaciones

Campana de la persona que consulta (identidad delegada o su sesión; una integración sin persona recibe 422 `usuario-delegado-requerido`) y sus preferencias de correo (NW-8, NW-11). Con `X-Empresa` solo se ven las de esa empresa y las del tenant (p. ej. webhooks).

### `GET /api/v1/notificaciones`

**Listar notificaciones.**

**Acceso: ver contrato · API (`/api/v1`) · SDK: `listarNotificaciones`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `soloNoLeidas` | query | booleano | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — BandejaNotificacionesDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de NotificacionDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].tipo` | texto | sí |  |
| `datos[].titulo` | texto | sí |  |
| `datos[].mensaje` | texto | sí |  |
| `datos[].empresaId` | uuid o null | no |  |
| `datos[].expedienteId` | uuid o null | no |  |
| `datos[].vinculoId` | uuid o null | no |  |
| `datos[].folio` | texto o null | no |  |
| `datos[].enlace` | texto o null | no |  |
| `datos[].creadaEn` | fecha y hora | sí |  |
| `datos[].leidaEn` | fecha y hora o null | no |  |
| `siguienteCursor` | texto o null | no |  |
| `noLeidas` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/notificaciones" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": "texto",
      "titulo": "texto",
      "mensaje": "Texto de ejemplo.",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "enlace": "https://ejemplo.co/recurso",
      "creadaEn": "2026-11-12T15:00:00Z",
      "leidaEn": "2026-11-12T15:00:00Z"
    }
  ],
  "siguienteCursor": "texto",
  "noLeidas": 1
}
```

</details>

### `POST /api/v1/notificaciones/{id}/leida`

**Marcar notificación leida.**

**Acceso: ver contrato · API (`/api/v1`) · SDK: `marcarNotificacionLeida`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — NotificacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `tipo` | texto | sí |  |
| `titulo` | texto | sí |  |
| `mensaje` | texto | sí |  |
| `empresaId` | uuid o null | no |  |
| `expedienteId` | uuid o null | no |  |
| `vinculoId` | uuid o null | no |  |
| `folio` | texto o null | no |  |
| `enlace` | texto o null | no |  |
| `creadaEn` | fecha y hora | sí |  |
| `leidaEn` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `404` `notificacion-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/notificaciones/3fa85f64-5717-4562-b3fc-2c963f66afa6/leida" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipo": "texto",
  "titulo": "texto",
  "mensaje": "Texto de ejemplo.",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "enlace": "https://ejemplo.co/recurso",
  "creadaEn": "2026-11-12T15:00:00Z",
  "leidaEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /api/v1/notificaciones/leidas`

**Marcar notificaciones leidas.** Marca todas las no leídas (de la empresa activa y del tenant si llega `X-Empresa`).

**Acceso: ver contrato · API (`/api/v1`) · SDK: `marcarNotificacionesLeidas`**

**Respuestas**

- `200` OK — NotificacionesMarcadasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `marcadas` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/notificaciones/leidas" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "marcadas": 1
}
```

</details>

### `GET /api/v1/notificaciones/stream`

**Stream notificaciones.** SSE de la campana: `event: notificacion` con cada `NotificacionDto` nueva (id = su orden) y `event: no_leidas` con el total cuando cambia. Con `X-Empresa` también `event: cambio` (`CambioEnVivoDto`: tipo de recurso e id) cada vez que cambia algo de esa empresa —un expediente, un vínculo, su cuenta, un documento, la ficha de un tercero, un certificado— para que la pantalla abierta lo refresque sola (NT-12); solo de la empresa activa y su tenant. Consulta cada 2 s.

**Acceso: ver contrato · API (`/api/v1`) · SDK: `streamNotificaciones`**

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/notificaciones/stream" \
  -H "X-Api-Key: $API_KEY"
```

</details>

### `GET /api/v1/usuarios/me/preferencias-notificacion`

**Obtener preferencias notificación.**

**Acceso: ver contrato · API (`/api/v1`) · SDK: `obtenerPreferenciasNotificacion`**

**Respuestas**

- `200` OK — PreferenciasNotificacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | mapa de texto → booleano | sí |  |
| `tiposConCorreo` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/usuarios/me/preferencias-notificacion" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "correo": {
    "clave": true
  },
  "tiposConCorreo": [
    "contacto@empresa.co"
  ]
}
```

</details>

### `PUT /api/v1/usuarios/me/preferencias-notificacion`

**Actualizar preferencias notificación.** Reemplaza el valor de los tipos indicados (los demás conservan el suyo); 422 `tipo-notificacion-invalido`.

**Acceso: ver contrato · API (`/api/v1`) · SDK: `actualizarPreferenciasNotificacion`**

**Cuerpo de la petición** (`application/json`, ActualizarPreferenciasNotificacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | mapa de texto → booleano | sí |  |

**Respuestas**

- `200` OK — PreferenciasNotificacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | mapa de texto → booleano | sí |  |
| `tiposConCorreo` | lista de texto | sí |  |

**Errores de negocio que lanza**

- `422` `tipo-notificacion-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/usuarios/me/preferencias-notificacion" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"correo":{"clave":true}}'
```

Respuesta `200`:

```json
{
  "correo": {
    "clave": true
  },
  "tiposConCorreo": [
    "contacto@empresa.co"
  ]
}
```

</details>

## Notificaciones del tercero

Campana del tercero y sus preferencias de correo (NT-1, sesión de tercero). Entre tenants, solo lo que pertenece a los vínculos de la sesión del tercero (o lo que no es de ningún vínculo, como una propuesta desde un documento): nunca ve lo de otra persona.

### `GET /public/v1/notificaciones`

**Listar notificaciones tercero.** Más recientes primero; `?soloNoLeidas=true`, `?limite=` (30 por omisión, máx. 100) y `?cursor=` de la página anterior.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `listarNotificacionesTercero`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `soloNoLeidas` | query | booleano | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — BandejaNotificacionesTerceroDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de NotificacionTerceroDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].tipo` | texto | sí |  |
| `datos[].titulo` | texto | sí |  |
| `datos[].mensaje` | texto | sí |  |
| `datos[].empresa` | EmpresaPublicaDto o null | no |  |
| `datos[].empresa.id` | uuid | sí |  |
| `datos[].empresa.nombre` | texto | sí |  |
| `datos[].empresa.logoUrl` | texto o null | no |  |
| `datos[].empresa.colorMarca` | texto o null | no |  |
| `datos[].empresa.contactoSoporte` | texto o null | no |  |
| `datos[].expedienteId` | uuid o null | no |  |
| `datos[].vinculoId` | uuid o null | no |  |
| `datos[].folio` | texto o null | no |  |
| `datos[].enlace` | texto o null | no |  |
| `datos[].creadaEn` | fecha y hora | sí |  |
| `datos[].leidaEn` | fecha y hora o null | no |  |
| `siguienteCursor` | texto o null | no |  |
| `noLeidas` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/notificaciones" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": "texto",
      "titulo": "texto",
      "mensaje": "Texto de ejemplo.",
      "empresa": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "logoUrl": "https://ejemplo.co/recurso",
        "colorMarca": "texto",
        "contactoSoporte": "texto"
      },
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "enlace": "https://ejemplo.co/recurso",
      "creadaEn": "2026-11-12T15:00:00Z",
      "leidaEn": "2026-11-12T15:00:00Z"
    }
  ],
  "siguienteCursor": "texto",
  "noLeidas": 1
}
```

</details>

### `POST /public/v1/notificaciones/{id}/leida`

**Marcar notificación tercero leida.** 404 `notificacion-no-encontrada` si no es de este tercero (o de uno de sus vínculos).

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `marcarNotificacionTerceroLeida`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — NotificacionTerceroDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `tipo` | texto | sí |  |
| `titulo` | texto | sí |  |
| `mensaje` | texto | sí |  |
| `empresa` | EmpresaPublicaDto o null | no |  |
| `empresa.id` | uuid | sí |  |
| `empresa.nombre` | texto | sí |  |
| `empresa.logoUrl` | texto o null | no |  |
| `empresa.colorMarca` | texto o null | no |  |
| `empresa.contactoSoporte` | texto o null | no |  |
| `expedienteId` | uuid o null | no |  |
| `vinculoId` | uuid o null | no |  |
| `folio` | texto o null | no |  |
| `enlace` | texto o null | no |  |
| `creadaEn` | fecha y hora | sí |  |
| `leidaEn` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `404` `notificacion-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/notificaciones/3fa85f64-5717-4562-b3fc-2c963f66afa6/leida" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipo": "texto",
  "titulo": "texto",
  "mensaje": "Texto de ejemplo.",
  "empresa": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S.",
    "logoUrl": "https://ejemplo.co/recurso",
    "colorMarca": "texto",
    "contactoSoporte": "texto"
  },
  "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "enlace": "https://ejemplo.co/recurso",
  "creadaEn": "2026-11-12T15:00:00Z",
  "leidaEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /public/v1/notificaciones/leidas`

**Marcar notificaciones tercero leidas.** Marca todas las no leídas del tercero (de todas sus empresas).

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `marcarNotificacionesTerceroLeidas`**

**Respuestas**

- `200` OK — NotificacionesMarcadasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `marcadas` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/notificaciones/leidas" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "marcadas": 1
}
```

</details>

### `GET /public/v1/notificaciones/stream`

**Stream notificaciones tercero.** SSE del portal del tercero (NT-12), con su sesión (`Authorization: Bearer`): `event: notificacion` con cada `NotificacionTerceroDto` nueva (id = su orden), `event: no_leidas` con el total cuando cambia y `event: cambio` (`CambioEnVivoDto`) cuando cambia algo de lo suyo —un expediente, un vínculo, una cuenta, un documento, su ficha, un certificado— para que lo que tiene abierto se refresque solo. Solo lo de los vínculos de su sesión. El stream se cierra cuando vence el token de la sesión: el cliente reconecta con el renovado. Consulta cada 2 s.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `streamNotificacionesTercero`**

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/notificaciones/stream" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

</details>

### `GET /public/v1/preferencias-notificacion`

**Obtener preferencias notificación tercero.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerPreferenciasNotificacionTercero`**

**Respuestas**

- `200` OK — PreferenciasNotificacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | mapa de texto → booleano | sí |  |
| `tiposConCorreo` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/preferencias-notificacion" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "correo": {
    "clave": true
  },
  "tiposConCorreo": [
    "contacto@empresa.co"
  ]
}
```

</details>

### `PUT /public/v1/preferencias-notificacion`

**Actualizar preferencias notificación tercero.** Reemplaza el valor de los tipos indicados (los demás conservan el suyo); 422 `tipo-notificacion-invalido`. La campana no se apaga.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `actualizarPreferenciasNotificacionTercero`**

**Cuerpo de la petición** (`application/json`, ActualizarPreferenciasNotificacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | mapa de texto → booleano | sí |  |

**Respuestas**

- `200` OK — PreferenciasNotificacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | mapa de texto → booleano | sí |  |
| `tiposConCorreo` | lista de texto | sí |  |

**Errores de negocio que lanza**

- `422` `tipo-notificacion-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/public/v1/preferencias-notificacion" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"correo":{"clave":true}}'
```

Respuesta `200`:

```json
{
  "correo": {
    "clave": true
  },
  "tiposConCorreo": [
    "contacto@empresa.co"
  ]
}
```

</details>

## Webhooks

Suscripciones de webhooks por sistema integrado (docs/05 §2.8, NW-4): una llave de un sistema solo ve y gestiona las de su sistema; una llave de administración (sin sistema) las de todo el tenant e indica el sistema al crear. Todo cambio queda en la auditoría de configuración; el secreto nunca se audita ni se guarda en claro.

### `GET /api/v1/webhooks`

**Listar webhooks.**

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `listarWebhooks`**

**Respuestas**

- `200` OK — lista de WebhookDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de WebhookDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].sistemaId` | uuid | sí |  |
| `[].sistema` | texto | sí |  |
| `[].url` | texto | sí |  |
| `[].descripcion` | texto o null | no |  |
| `[].tipos` | lista de texto | sí |  |
| `[].empresas` | lista de texto | sí |  |
| `[].version` | entero | sí |  |
| `[].activa` | booleano | sí |  |
| `[].fallosSeguidos` | entero | sí |  |
| `[].deshabilitadaEn` | fecha y hora o null | no |  |
| `[].causaDeshabilitacion` | texto o null | no |  |
| `[].creadaEn` | fecha y hora | sí |  |
| `[].ultimaEntregaEn` | fecha y hora o null | no |  |
| `[].secretoAnteriorVence` | fecha y hora o null | no |  |
| `[].secreto` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/webhooks" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "sistema": "texto",
    "url": "https://ejemplo.co/recurso",
    "descripcion": "Texto de ejemplo.",
    "tipos": [
      "texto"
    ],
    "empresas": [
      "texto"
    ],
    "version": 1,
    "activa": true,
    "fallosSeguidos": 1,
    "deshabilitadaEn": "2026-11-12T15:00:00Z",
    "causaDeshabilitacion": "texto",
    "creadaEn": "2026-11-12T15:00:00Z",
    "ultimaEntregaEn": "2026-11-12T15:00:00Z",
    "secretoAnteriorVence": "2026-11-12T15:00:00Z",
    "secreto": "vsk_prueba_ejemplo"
  }
]
```

</details>

### `POST /api/v1/webhooks`

**Crear webhook.** Crea la suscripción y devuelve el secreto una sola vez (201). URL https (http solo hacia localhost en desarrollo; nunca a IPs privadas), tipos del catálogo o `*`, empresas dentro del alcance de la credencial (422 `empresas-fuera-de-alcance`).

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `crearWebhook`**

**Cuerpo de la petición** (`application/json`, CrearWebhookDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `url` | texto | sí |  |
| `tipos` | lista de texto | sí |  |
| `empresas` | lista de texto o null | no |  |
| `sistemaId` | uuid o null | no |  |
| `descripcion` | texto o null | no |  |

**Respuestas**

- `201` Creado — WebhookDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `sistemaId` | uuid | sí |  |
| `sistema` | texto | sí |  |
| `url` | texto | sí |  |
| `descripcion` | texto o null | no |  |
| `tipos` | lista de texto | sí |  |
| `empresas` | lista de texto | sí |  |
| `version` | entero | sí |  |
| `activa` | booleano | sí |  |
| `fallosSeguidos` | entero | sí |  |
| `deshabilitadaEn` | fecha y hora o null | no |  |
| `causaDeshabilitacion` | texto o null | no |  |
| `creadaEn` | fecha y hora | sí |  |
| `ultimaEntregaEn` | fecha y hora o null | no |  |
| `secretoAnteriorVence` | fecha y hora o null | no |  |
| `secreto` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/webhooks" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"url":"https://ejemplo.co/recurso","tipos":["texto"],"empresas":["texto"],"sistemaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","descripcion":"Texto de ejemplo."}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistema": "texto",
  "url": "https://ejemplo.co/recurso",
  "descripcion": "Texto de ejemplo.",
  "tipos": [
    "texto"
  ],
  "empresas": [
    "texto"
  ],
  "version": 1,
  "activa": true,
  "fallosSeguidos": 1,
  "deshabilitadaEn": "2026-11-12T15:00:00Z",
  "causaDeshabilitacion": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "ultimaEntregaEn": "2026-11-12T15:00:00Z",
  "secretoAnteriorVence": "2026-11-12T15:00:00Z",
  "secreto": "vsk_prueba_ejemplo"
}
```

</details>

### `GET /api/v1/webhooks/{id}`

**Obtener webhook.**

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `obtenerWebhook`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — WebhookDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `WebhookDto` que se detallan en `POST /api/v1/webhooks`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/webhooks/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistema": "texto",
  "url": "https://ejemplo.co/recurso",
  "descripcion": "Texto de ejemplo.",
  "tipos": [
    "texto"
  ],
  "empresas": [
    "texto"
  ],
  "version": 1,
  "activa": true,
  "fallosSeguidos": 1,
  "deshabilitadaEn": "2026-11-12T15:00:00Z",
  "causaDeshabilitacion": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "ultimaEntregaEn": "2026-11-12T15:00:00Z",
  "secretoAnteriorVence": "2026-11-12T15:00:00Z",
  "secreto": "vsk_prueba_ejemplo"
}
```

</details>

### `PUT /api/v1/webhooks/{id}`

**Actualizar webhook.**

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `actualizarWebhook`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, EditarWebhookDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `url` | texto o null | no |  |
| `tipos` | lista de texto o null | no |  |
| `empresas` | lista de texto o null | no |  |
| `descripcion` | texto o null | no |  |

**Respuestas**

- `200` OK — WebhookDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `WebhookDto` que se detallan en `POST /api/v1/webhooks`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/webhooks/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"url":"https://ejemplo.co/recurso","tipos":["texto"],"empresas":["texto"],"descripcion":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistema": "texto",
  "url": "https://ejemplo.co/recurso",
  "descripcion": "Texto de ejemplo.",
  "tipos": [
    "texto"
  ],
  "empresas": [
    "texto"
  ],
  "version": 1,
  "activa": true,
  "fallosSeguidos": 1,
  "deshabilitadaEn": "2026-11-12T15:00:00Z",
  "causaDeshabilitacion": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "ultimaEntregaEn": "2026-11-12T15:00:00Z",
  "secretoAnteriorVence": "2026-11-12T15:00:00Z",
  "secreto": "vsk_prueba_ejemplo"
}
```

</details>

### `POST /api/v1/webhooks/{id}/activar`

**Activar webhook.** Reactiva (también una deshabilitada por fallos) y reinicia la cuenta de fallos.

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `activarWebhook`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — WebhookDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `WebhookDto` que se detallan en `POST /api/v1/webhooks`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/webhooks/3fa85f64-5717-4562-b3fc-2c963f66afa6/activar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistema": "texto",
  "url": "https://ejemplo.co/recurso",
  "descripcion": "Texto de ejemplo.",
  "tipos": [
    "texto"
  ],
  "empresas": [
    "texto"
  ],
  "version": 1,
  "activa": true,
  "fallosSeguidos": 1,
  "deshabilitadaEn": "2026-11-12T15:00:00Z",
  "causaDeshabilitacion": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "ultimaEntregaEn": "2026-11-12T15:00:00Z",
  "secretoAnteriorVence": "2026-11-12T15:00:00Z",
  "secreto": "vsk_prueba_ejemplo"
}
```

</details>

### `POST /api/v1/webhooks/{id}/desactivar`

**Desactivar webhook.**

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `desactivarWebhook`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — WebhookDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `WebhookDto` que se detallan en `POST /api/v1/webhooks`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/webhooks/3fa85f64-5717-4562-b3fc-2c963f66afa6/desactivar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistema": "texto",
  "url": "https://ejemplo.co/recurso",
  "descripcion": "Texto de ejemplo.",
  "tipos": [
    "texto"
  ],
  "empresas": [
    "texto"
  ],
  "version": 1,
  "activa": true,
  "fallosSeguidos": 1,
  "deshabilitadaEn": "2026-11-12T15:00:00Z",
  "causaDeshabilitacion": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "ultimaEntregaEn": "2026-11-12T15:00:00Z",
  "secretoAnteriorVence": "2026-11-12T15:00:00Z",
  "secreto": "vsk_prueba_ejemplo"
}
```

</details>

### `GET /api/v1/webhooks/{id}/entregas`

**Listar entregas webhook.** Entregas de la suscripción, más recientes primero (`?estado=Fallida&limite=50&cursor=`).

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `listarEntregasWebhook`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `estado` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfEntregaWebhookDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de EntregaWebhookDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].webhookId` | uuid | sí |  |
| `datos[].eventoId` | uuid | sí |  |
| `datos[].tipoEvento` | texto | sí |  |
| `datos[].cursor` | texto | sí |  |
| `datos[].empresaId` | uuid o null | no |  |
| `datos[].estado` | texto | sí |  |
| `datos[].intentos` | entero | sí |  |
| `datos[].creadaEn` | fecha y hora | sí |  |
| `datos[].entregadaEn` | fecha y hora o null | no |  |
| `datos[].proximoIntentoEn` | fecha y hora o null | no |  |
| `datos[].historial` | lista de IntentoEntregaDto | sí |  |
| `datos[].historial[].numero` | entero | sí |  |
| `datos[].historial[].intentoId` | uuid | sí |  |
| `datos[].historial[].fecha` | fecha y hora | sí |  |
| `datos[].historial[].codigoHttp` | entero o null | no |  |
| `datos[].historial[].duracionMs` | entero | sí |  |
| `datos[].historial[].resultado` | texto | sí |  |
| `datos[].historial[].error` | texto o null | no |  |
| `datos[].historial[].manual` | booleano | sí |  |
| `siguienteCursor` | texto o null | no |  |

**Errores de negocio que lanza**

- `422` `estado-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/webhooks/3fa85f64-5717-4562-b3fc-2c963f66afa6/entregas" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "webhookId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "eventoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoEvento": "texto",
      "cursor": "texto",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "estado": "texto",
      "intentos": 1,
      "creadaEn": "2026-11-12T15:00:00Z",
      "entregadaEn": "2026-11-12T15:00:00Z",
      "proximoIntentoEn": "2026-11-12T15:00:00Z",
      "historial": [
        {
          "numero": 1,
          "intentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "fecha": "2026-11-12T15:00:00Z",
          "codigoHttp": 1,
          "duracionMs": 1,
          "resultado": "texto",
          "error": "texto",
          "manual": true
        }
      ]
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/webhooks/{id}/entregas/{entregaId}`

**Obtener entrega webhook.**

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `obtenerEntregaWebhook`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `entregaId` | path | uuid | sí |  |

**Respuestas**

- `200` OK — EntregaWebhookDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `webhookId` | uuid | sí |  |
| `eventoId` | uuid | sí |  |
| `tipoEvento` | texto | sí |  |
| `cursor` | texto | sí |  |
| `empresaId` | uuid o null | no |  |
| `estado` | texto | sí |  |
| `intentos` | entero | sí |  |
| `creadaEn` | fecha y hora | sí |  |
| `entregadaEn` | fecha y hora o null | no |  |
| `proximoIntentoEn` | fecha y hora o null | no |  |
| `historial` | lista de IntentoEntregaDto | sí |  |
| `historial[].numero` | entero | sí |  |
| `historial[].intentoId` | uuid | sí |  |
| `historial[].fecha` | fecha y hora | sí |  |
| `historial[].codigoHttp` | entero o null | no |  |
| `historial[].duracionMs` | entero | sí |  |
| `historial[].resultado` | texto | sí |  |
| `historial[].error` | texto o null | no |  |
| `historial[].manual` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/webhooks/3fa85f64-5717-4562-b3fc-2c963f66afa6/entregas/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "webhookId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoEvento": "texto",
  "cursor": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "texto",
  "intentos": 1,
  "creadaEn": "2026-11-12T15:00:00Z",
  "entregadaEn": "2026-11-12T15:00:00Z",
  "proximoIntentoEn": "2026-11-12T15:00:00Z",
  "historial": [
    {
      "numero": 1,
      "intentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "fecha": "2026-11-12T15:00:00Z",
      "codigoHttp": 1,
      "duracionMs": 1,
      "resultado": "texto",
      "error": "texto",
      "manual": true
    }
  ]
}
```

</details>

### `POST /api/v1/webhooks/{id}/entregas/{entregaId}/reenviar`

**Reenviar entrega webhook.** Reenvío manual (202): un intento más, ahora, aunque la entrega ya se haya entregado, fallado o cancelado y aunque la suscripción esté desactivada. Si falla no programa reintentos automáticos.

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `reenviarEntregaWebhook`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `entregaId` | path | uuid | sí |  |

**Respuestas**

- `202` Aceptado — EntregaWebhookDto

Campos de la respuesta `202`:

Mismos campos de `EntregaWebhookDto` que se detallan en `GET /api/v1/webhooks/{id}/entregas/{entregaId}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/webhooks/3fa85f64-5717-4562-b3fc-2c963f66afa6/entregas/3fa85f64-5717-4562-b3fc-2c963f66afa6/reenviar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `202`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "webhookId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "eventoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoEvento": "texto",
  "cursor": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "texto",
  "intentos": 1,
  "creadaEn": "2026-11-12T15:00:00Z",
  "entregadaEn": "2026-11-12T15:00:00Z",
  "proximoIntentoEn": "2026-11-12T15:00:00Z",
  "historial": [
    {
      "numero": 1,
      "intentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "fecha": "2026-11-12T15:00:00Z",
      "codigoHttp": 1,
      "duracionMs": 1,
      "resultado": "texto",
      "error": "texto",
      "manual": true
    }
  ]
}
```

</details>

### `POST /api/v1/webhooks/{id}/rotar-secreto`

**Rotar secreto webhook.** Secreto nuevo (se muestra una vez); el anterior sigue firmando durante el solapamiento (24 h por defecto).

**Permiso webhooks:administrar · API (`/api/v1`) · SDK: `rotarSecretoWebhook`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — WebhookDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `WebhookDto` que se detallan en `POST /api/v1/webhooks`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/webhooks/3fa85f64-5717-4562-b3fc-2c963f66afa6/rotar-secreto" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "sistema": "texto",
  "url": "https://ejemplo.co/recurso",
  "descripcion": "Texto de ejemplo.",
  "tipos": [
    "texto"
  ],
  "empresas": [
    "texto"
  ],
  "version": 1,
  "activa": true,
  "fallosSeguidos": 1,
  "deshabilitadaEn": "2026-11-12T15:00:00Z",
  "causaDeshabilitacion": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "ultimaEntregaEn": "2026-11-12T15:00:00Z",
  "secretoAnteriorVence": "2026-11-12T15:00:00Z",
  "secreto": "vsk_prueba_ejemplo"
}
```

</details>
