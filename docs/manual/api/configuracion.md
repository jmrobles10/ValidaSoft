# API · Configuración

32 operaciones en 8 recursos.

## Accesos soporte

Accesos temporales de soporte de la Plataforma (B-6). Los concede y revoca un administrador del tenant (`empresas:administrar`); cada cambio queda en la auditoría de configuración. La Plataforma ve los suyos en `GET /plataforma/accesos-soporte` y entra con `X-Acceso-Soporte` (solo lectura, sin documentos).

### `GET /api/v1/accesos-soporte`

**Listar accesos soporte.** Los accesos del tenant, el más reciente primero (vigentes, expirados y revocados).

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `listarAccesosSoporte`**

**Respuestas**

- `200` OK — lista de AccesoSoporteDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de AccesoSoporteDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].empresaId` | uuid | sí |  |
| `[].empresa` | texto | sí |  |
| `[].justificacion` | texto | sí |  |
| `[].concedidoPor` | texto | sí |  |
| `[].concedidoEn` | fecha y hora | sí |  |
| `[].expiraEn` | fecha y hora | sí |  |
| `[].revocadoEn` | fecha y hora o null | no |  |
| `[].revocadoPor` | texto o null | no |  |
| `[].vigente` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/accesos-soporte" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "empresa": "texto",
    "justificacion": "Texto de ejemplo.",
    "concedidoPor": "texto",
    "concedidoEn": "2026-11-12T15:00:00Z",
    "expiraEn": "2026-11-12T15:00:00Z",
    "revocadoEn": "2026-11-12T15:00:00Z",
    "revocadoPor": "texto",
    "vigente": true
  }
]
```

</details>

### `POST /api/v1/accesos-soporte`

**Conceder acceso soporte.**

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `concederAccesoSoporte`**

**Cuerpo de la petición** (`application/json`, ConcederAccesoSoporteDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `empresaId` | uuid | sí |  |
| `horas` | entero | sí |  |
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

**Errores de negocio que lanza**

- `422` `empresa-inactiva`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/accesos-soporte" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"empresaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","horas":1,"justificacion":"Texto de ejemplo."}'
```

</details>

### `POST /api/v1/accesos-soporte/{id}/revocar`

**Revocar acceso soporte.**

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `revocarAccesoSoporte`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — AccesoSoporteDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `empresaId` | uuid | sí |  |
| `empresa` | texto | sí |  |
| `justificacion` | texto | sí |  |
| `concedidoPor` | texto | sí |  |
| `concedidoEn` | fecha y hora | sí |  |
| `expiraEn` | fecha y hora | sí |  |
| `revocadoEn` | fecha y hora o null | no |  |
| `revocadoPor` | texto o null | no |  |
| `vigente` | booleano | sí |  |

**Errores de negocio que lanza**

- `404` `acceso-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/accesos-soporte/3fa85f64-5717-4562-b3fc-2c963f66afa6/revocar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresa": "texto",
  "justificacion": "Texto de ejemplo.",
  "concedidoPor": "texto",
  "concedidoEn": "2026-11-12T15:00:00Z",
  "expiraEn": "2026-11-12T15:00:00Z",
  "revocadoEn": "2026-11-12T15:00:00Z",
  "revocadoPor": "texto",
  "vigente": true
}
```

</details>

### `GET /api/v1/plataforma/accesos-soporte`

**Listar accesos soporte concedidos.** Plataforma: los accesos vigentes que le concedieron los tenants, para entrar con `X-Acceso-Soporte`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarAccesosSoporteConcedidos`**

**Respuestas**

- `200` OK — lista de AccesoSoporteConcedidoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de AccesoSoporteConcedidoDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].tenantId` | uuid | sí |  |
| `[].grupo` | texto | sí |  |
| `[].empresaId` | uuid | sí |  |
| `[].empresa` | texto | sí |  |
| `[].justificacion` | texto | sí |  |
| `[].expiraEn` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/accesos-soporte" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "grupo": "texto",
    "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "empresa": "texto",
    "justificacion": "Texto de ejemplo.",
    "expiraEn": "2026-11-12T15:00:00Z"
  }
]
```

</details>

## API keys

### `GET /api/v1/api-keys`

**Listar API keys.**

**Permiso api-keys:administrar · API (`/api/v1`) · SDK: `listarApiKeys`**

**Respuestas**

- `200` OK — lista de ApiKeyDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de ApiKeyDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].prefijo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].scopes` | lista de texto | sí |  |
| `[].empresas` | lista de texto | sí |  |
| `[].expiraEn` | fecha y hora o null | no |  |
| `[].creadaEn` | fecha y hora | sí |  |
| `[].sandbox` | booleano | sí |  |
| `[].valor` | texto o null | no |  |
| `[].sistemaId` | uuid o null | no |  |
| `[].rotadaDe` | uuid o null | no |  |
| `[].reemplazadaPor` | uuid o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/api-keys" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "prefijo": "texto",
    "nombre": "Constructora Norte S.A.S.",
    "scopes": [
      "texto"
    ],
    "empresas": [
      "texto"
    ],
    "expiraEn": "2026-11-12T15:00:00Z",
    "creadaEn": "2026-11-12T15:00:00Z",
    "sandbox": true,
    "valor": "vsk_prueba_ejemplo",
    "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "rotadaDe": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "reemplazadaPor": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
]
```

</details>

### `POST /api/v1/api-keys`

**Crear API key.**

**Permiso api-keys:administrar · API (`/api/v1`) · SDK: `crearApiKey`**

**Cuerpo de la petición** (`application/json`, CrearApiKeyDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombre` | texto | sí |  |
| `scopes` | lista de texto | sí |  |
| `empresas` | lista de texto o null | no |  |
| `expiraEn` | fecha y hora o null | no |  |
| `sandbox` | booleano | no | por omisión false |
| `sistemaId` | uuid o null | no |  |

**Respuestas**

- `201` Creado — ApiKeyDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `prefijo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `scopes` | lista de texto | sí |  |
| `empresas` | lista de texto | sí |  |
| `expiraEn` | fecha y hora o null | no |  |
| `creadaEn` | fecha y hora | sí |  |
| `sandbox` | booleano | sí |  |
| `valor` | texto o null | no |  |
| `sistemaId` | uuid o null | no |  |
| `rotadaDe` | uuid o null | no |  |
| `reemplazadaPor` | uuid o null | no |  |

**Errores de negocio que lanza**

- `404` `tenant-no-encontrado`
- `422` `empresas-invalidas`
- `422` `expiracion-invalida`
- `422` `nombre-requerido`
- `422` `scopes-invalidos`
- `422` `sistema-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/api-keys" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Constructora Norte S.A.S.","scopes":["texto"],"empresas":["texto"],"expiraEn":"2026-11-12T15:00:00Z","sandbox":false,"sistemaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "prefijo": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "scopes": [
    "texto"
  ],
  "empresas": [
    "texto"
  ],
  "expiraEn": "2026-11-12T15:00:00Z",
  "creadaEn": "2026-11-12T15:00:00Z",
  "sandbox": true,
  "valor": "vsk_prueba_ejemplo",
  "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "rotadaDe": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "reemplazadaPor": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

</details>

### `DELETE /api/v1/api-keys/{id}`

**Revocar API key.**

**Permiso api-keys:administrar · API (`/api/v1`) · SDK: `revocarApiKey`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `204` Sin contenido

**Errores de negocio que lanza**

- `404` `api-key-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X DELETE "https://{host}/api/v1/api-keys/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY"
```

</details>

### `POST /api/v1/api-keys/{id}/rotar`

**Rotar API key.** Rotación con solapamiento (CN-3): emite una llave nueva con los mismos permisos, alcance, sistema y expiración, y deja la anterior válida hasta `AnteriorValidaHasta` (24 h por defecto) para que el sistema cambie la clave sin cortar el servicio. Devuelve la nueva con su `valor` (una sola vez; la respuesta no se guarda para idempotencia: rotar dos veces la misma responde 409 `api-key-ya-rotada`).

**Permiso api-keys:administrar · API (`/api/v1`) · SDK: `rotarApiKey`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, RotarApiKeyDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `anteriorValidaHasta` | fecha y hora o null | no |  |

**Respuestas**

- `201` Creado — ApiKeyDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `prefijo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `scopes` | lista de texto | sí |  |
| `empresas` | lista de texto | sí |  |
| `expiraEn` | fecha y hora o null | no |  |
| `creadaEn` | fecha y hora | sí |  |
| `sandbox` | booleano | sí |  |
| `valor` | texto o null | no |  |
| `sistemaId` | uuid o null | no |  |
| `rotadaDe` | uuid o null | no |  |
| `reemplazadaPor` | uuid o null | no |  |

**Errores de negocio que lanza**

- `404` `api-key-no-encontrada`
- `404` `tenant-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/api-keys/3fa85f64-5717-4562-b3fc-2c963f66afa6/rotar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"anteriorValidaHasta":"2026-11-12T15:00:00Z"}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "prefijo": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "scopes": [
    "texto"
  ],
  "empresas": [
    "texto"
  ],
  "expiraEn": "2026-11-12T15:00:00Z",
  "creadaEn": "2026-11-12T15:00:00Z",
  "sandbox": true,
  "valor": "vsk_prueba_ejemplo",
  "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "rotadaDe": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "reemplazadaPor": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

</details>

## Areas

Áreas revisoras de la empresa activa (cabecera `X-Empresa`) y sus miembros.

### `GET /api/v1/areas`

**Listar areas.**

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarAreas`**

**Respuestas**

- `200` OK — lista de AreaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de AreaDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].empresaId` | uuid | sí |  |
| `[].codigo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].miembros` | lista de uuid | sí |  |
| `[].activa` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/areas" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "miembros": [
      "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    ],
    "activa": true
  }
]
```

</details>

### `POST /api/v1/areas`

**Crear area.**

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `crearArea`**

**Cuerpo de la petición** (`application/json`, CrearAreaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |

**Respuestas**

- `201` Creado — AreaDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `empresaId` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `miembros` | lista de uuid | sí |  |
| `activa` | booleano | sí |  |

**Errores de negocio que lanza**

- `409` `area-duplicada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/areas" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S."}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "miembros": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  ],
  "activa": true
}
```

</details>

### `GET /api/v1/areas/{id}`

**Obtener area.**

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerArea`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — AreaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `empresaId` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `miembros` | lista de uuid | sí |  |
| `activa` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/areas/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "miembros": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  ],
  "activa": true
}
```

</details>

### `PUT /api/v1/areas/{id}`

**Actualizar area.** El código es inmutable; se editan nombre y estado.

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarArea`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarAreaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombre` | texto | sí |  |
| `activa` | booleano o null | no |  |

**Respuestas**

- `200` OK — AreaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `empresaId` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `miembros` | lista de uuid | sí |  |
| `activa` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/areas/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Constructora Norte S.A.S.","activa":true}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "miembros": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  ],
  "activa": true
}
```

</details>

### `PUT /api/v1/areas/{id}/miembros`

**Definir miembros area.** Reemplaza los miembros: cada uno debe ser un usuario activo con algún rol en esta empresa o en todo el tenant.

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `definirMiembrosArea`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, MiembrosAreaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `usuarios` | lista de uuid | sí |  |

**Respuestas**

- `200` OK — AreaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `empresaId` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `miembros` | lista de uuid | sí |  |
| `activa` | booleano | sí |  |

**Errores de negocio que lanza**

- `422` `miembros-invalidos`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/areas/3fa85f64-5717-4562-b3fc-2c963f66afa6/miembros" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"usuarios":["3fa85f64-5717-4562-b3fc-2c963f66afa6"]}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "miembros": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  ],
  "activa": true
}
```

</details>

## Dominio de correo

Dominio de correo propio de cada empresa (OD-18…OD-22, docs/11 §H8). La empresa registra su dominio, crea los registros DNS que se le muestran (SPF, DKIM, DMARC) y pide verificarlos; con el dominio verificado sus correos a terceros salen desde su dirección, y mientras no lo esté —o si lo quita— salen con el remitente de la plataforma. `empresas:administrar`.

### `GET /api/v1/empresas/{id}/dominio-correo`

**Obtener dominio de correo.**

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `obtenerDominioDeCorreo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — DominioDeCorreoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `empresaId` | uuid | sí |  |
| `configurado` | booleano | sí |  |
| `dominio` | texto o null | no |  |
| `parteLocal` | texto | sí |  |
| `nombreRemitente` | texto o null | no |  |
| `estado` | texto o null | no |  |
| `registros` | lista de RegistroDnsDto | sí |  |
| `registros[].finalidad` | texto | sí |  |
| `registros[].tipo` | texto | sí |  |
| `registros[].nombre` | texto | sí |  |
| `registros[].valor` | texto | sí |  |
| `verificacion` | lista de ComprobacionDeRegistroDnsDto | sí |  |
| `verificacion[].finalidad` | texto | sí |  |
| `verificacion[].correcto` | booleano | sí |  |
| `verificacion[].encontrado` | texto o null | no |  |
| `verificadoEn` | fecha y hora o null | no |  |
| `ultimaVerificacionEn` | fecha y hora o null | no |  |
| `remitenteEnUso` | texto | sí |  |
| `remitenteDeLaPlataforma` | texto | sí |  |
| `simulado` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/dominio-correo" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "configurado": true,
  "dominio": "texto",
  "parteLocal": "texto",
  "nombreRemitente": "Nombre de ejemplo",
  "estado": "texto",
  "registros": [
    {
      "finalidad": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "valor": "vsk_prueba_ejemplo"
    }
  ],
  "verificacion": [
    {
      "finalidad": "texto",
      "correcto": true,
      "encontrado": "texto"
    }
  ],
  "verificadoEn": "2026-11-12T15:00:00Z",
  "ultimaVerificacionEn": "2026-11-12T15:00:00Z",
  "remitenteEnUso": "texto",
  "remitenteDeLaPlataforma": "texto",
  "simulado": true
}
```

</details>

### `PUT /api/v1/empresas/{id}/dominio-correo`

**Fijar dominio de correo.** Registra (o cambia) el dominio: queda `Pendiente` con los registros DNS que debe crear la empresa. Si el dominio es el mismo, solo cambia el nombre o la parte local y conserva su estado. 422 `dominio-invalido`, `parte-local-invalida`, `nombre-remitente-invalido`; 503 `proveedor-de-dominios-no-configurado`.

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `fijarDominioDeCorreo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarDominioDeCorreoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `dominio` | texto | sí |  |
| `parteLocal` | texto o null | no |  |
| `nombreRemitente` | texto o null | no |  |

**Respuestas**

- `200` OK — DominioDeCorreoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `DominioDeCorreoDto` que se detallan en `GET /api/v1/empresas/{id}/dominio-correo`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/dominio-correo" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"dominio":"texto","parteLocal":"texto","nombreRemitente":"Nombre de ejemplo"}'
```

Respuesta `200`:

```json
{
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "configurado": true,
  "dominio": "texto",
  "parteLocal": "texto",
  "nombreRemitente": "Nombre de ejemplo",
  "estado": "texto",
  "registros": [
    {
      "finalidad": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "valor": "vsk_prueba_ejemplo"
    }
  ],
  "verificacion": [
    {
      "finalidad": "texto",
      "correcto": true,
      "encontrado": "texto"
    }
  ],
  "verificadoEn": "2026-11-12T15:00:00Z",
  "ultimaVerificacionEn": "2026-11-12T15:00:00Z",
  "remitenteEnUso": "texto",
  "remitenteDeLaPlataforma": "texto",
  "simulado": true
}
```

</details>

### `DELETE /api/v1/empresas/{id}/dominio-correo`

**Quitar dominio de correo.** Quita el dominio: los correos de la empresa vuelven a salir con el remitente de la plataforma. 404 si no tenía.

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `quitarDominioDeCorreo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — DominioDeCorreoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `DominioDeCorreoDto` que se detallan en `GET /api/v1/empresas/{id}/dominio-correo`.

**Errores de negocio que lanza**

- `404` `dominio-sin-registrar`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X DELETE "https://{host}/api/v1/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/dominio-correo" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "configurado": true,
  "dominio": "texto",
  "parteLocal": "texto",
  "nombreRemitente": "Nombre de ejemplo",
  "estado": "texto",
  "registros": [
    {
      "finalidad": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "valor": "vsk_prueba_ejemplo"
    }
  ],
  "verificacion": [
    {
      "finalidad": "texto",
      "correcto": true,
      "encontrado": "texto"
    }
  ],
  "verificadoEn": "2026-11-12T15:00:00Z",
  "ultimaVerificacionEn": "2026-11-12T15:00:00Z",
  "remitenteEnUso": "texto",
  "remitenteDeLaPlataforma": "texto",
  "simulado": true
}
```

</details>

### `POST /api/v1/empresas/{id}/dominio-correo/verificar`

**Verificar dominio de correo.** Verifica ya los registros DNS (SPF, DKIM y DMARC) contra el proveedor. Con todos correctos el dominio queda `Verificado` y los correos de la empresa salen desde él; si falta alguno queda `Fallido` con lo que se encontró y el correo sigue saliendo con el remitente de la plataforma. 409 `dominio-sin-registrar`.

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `verificarDominioDeCorreo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — DominioDeCorreoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `DominioDeCorreoDto` que se detallan en `GET /api/v1/empresas/{id}/dominio-correo`.

**Errores de negocio que lanza**

- `409` `dominio-sin-registrar`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/dominio-correo/verificar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "configurado": true,
  "dominio": "texto",
  "parteLocal": "texto",
  "nombreRemitente": "Nombre de ejemplo",
  "estado": "texto",
  "registros": [
    {
      "finalidad": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "valor": "vsk_prueba_ejemplo"
    }
  ],
  "verificacion": [
    {
      "finalidad": "texto",
      "correcto": true,
      "encontrado": "texto"
    }
  ],
  "verificadoEn": "2026-11-12T15:00:00Z",
  "ultimaVerificacionEn": "2026-11-12T15:00:00Z",
  "remitenteEnUso": "texto",
  "remitenteDeLaPlataforma": "texto",
  "simulado": true
}
```

</details>

## Empresas

### `GET /api/v1/empresas`

**Listar empresas.** Solo las empresas que la credencial puede usar.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `listarEmpresas`**

**Respuestas**

- `200` OK — lista de EmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de EmpresaDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].tenantId` | uuid | sí |  |
| `[].identificacion` | IdentificacionDto | sí |  |
| `[].identificacion.pais` | texto | sí |  |
| `[].identificacion.tipo` | texto | sí |  |
| `[].identificacion.numero` | texto | sí |  |
| `[].identificacion.digitoVerificacion` | texto o null | no |  |
| `[].nombre` | texto | sí |  |
| `[].logoUrl` | texto o null | no |  |
| `[].replyTo` | texto o null | no |  |
| `[].prefijoFolio` | texto | sí |  |
| `[].activa` | booleano | sí |  |
| `[].colorMarca` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/empresas" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "identificacion": {
      "pais": "CO",
      "tipo": "texto",
      "numero": "900123456",
      "digitoVerificacion": "texto"
    },
    "nombre": "Constructora Norte S.A.S.",
    "logoUrl": "https://ejemplo.co/recurso",
    "replyTo": "texto",
    "prefijoFolio": "texto",
    "activa": true,
    "colorMarca": "texto"
  }
]
```

</details>

### `POST /api/v1/empresas`

**Crear empresa.**

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `crearEmpresa`**

**Cuerpo de la petición** (`application/json`, CrearEmpresaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `nombre` | texto | sí |  |
| `prefijoFolio` | texto | sí |  |
| `logoUrl` | texto o null | no |  |
| `replyTo` | texto o null | no |  |

**Respuestas**

- `201` Creado — EmpresaDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `tenantId` | uuid | sí |  |
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `nombre` | texto | sí |  |
| `logoUrl` | texto o null | no |  |
| `replyTo` | texto o null | no |  |
| `prefijoFolio` | texto | sí |  |
| `activa` | booleano | sí |  |
| `colorMarca` | texto o null | no |  |

**Errores de negocio que lanza**

- `409` `empresa-duplicada`
- `409` `prefijo-folio-duplicado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/empresas" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"identificacion":{"pais":"CO","tipo":"texto","numero":"900123456","digitoVerificacion":"texto"},"nombre":"Constructora Norte S.A.S.","prefijoFolio":"texto","logoUrl":"https://ejemplo.co/recurso","replyTo":"texto"}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "nombre": "Constructora Norte S.A.S.",
  "logoUrl": "https://ejemplo.co/recurso",
  "replyTo": "texto",
  "prefijoFolio": "texto",
  "activa": true,
  "colorMarca": "texto"
}
```

</details>

### `GET /api/v1/empresas/{id}`

**Obtener empresa.**

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `obtenerEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — EmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `tenantId` | uuid | sí |  |
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `nombre` | texto | sí |  |
| `logoUrl` | texto o null | no |  |
| `replyTo` | texto o null | no |  |
| `prefijoFolio` | texto | sí |  |
| `activa` | booleano | sí |  |
| `colorMarca` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "nombre": "Constructora Norte S.A.S.",
  "logoUrl": "https://ejemplo.co/recurso",
  "replyTo": "texto",
  "prefijoFolio": "texto",
  "activa": true,
  "colorMarca": "texto"
}
```

</details>

### `PUT /api/v1/empresas/{id}`

**Actualizar empresa.** Datos y marca (contacto de soporte, color y logo). La identificación, la razón social y el prefijo no cambian.

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `actualizarEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarEmpresaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `contactoSoporte` | texto o null | no |  |
| `colorMarca` | texto o null | no |  |
| `logoUrl` | texto o null | no |  |

**Respuestas**

- `200` OK — EmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `tenantId` | uuid | sí |  |
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `nombre` | texto | sí |  |
| `logoUrl` | texto o null | no |  |
| `replyTo` | texto o null | no |  |
| `prefijoFolio` | texto | sí |  |
| `activa` | booleano | sí |  |
| `colorMarca` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"contactoSoporte":"texto","colorMarca":"texto","logoUrl":"https://ejemplo.co/recurso"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "nombre": "Constructora Norte S.A.S.",
  "logoUrl": "https://ejemplo.co/recurso",
  "replyTo": "texto",
  "prefijoFolio": "texto",
  "activa": true,
  "colorMarca": "texto"
}
```

</details>

### `POST /api/v1/empresas/{id}/desactivar`

**Desactivar empresa.**

**Permiso empresas:administrar · API (`/api/v1`) · SDK: `desactivarEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — EmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `tenantId` | uuid | sí |  |
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `nombre` | texto | sí |  |
| `logoUrl` | texto o null | no |  |
| `replyTo` | texto o null | no |  |
| `prefijoFolio` | texto | sí |  |
| `activa` | booleano | sí |  |
| `colorMarca` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/desactivar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "nombre": "Constructora Norte S.A.S.",
  "logoUrl": "https://ejemplo.co/recurso",
  "replyTo": "texto",
  "prefijoFolio": "texto",
  "activa": true,
  "colorMarca": "texto"
}
```

</details>

## Motivos

### `GET /api/v1/motivos`

**Listar motivos.** Base de la plataforma primero y luego los del tenant (activos e inactivos).

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `listarMotivos`**

**Respuestas**

- `200` OK — lista de MotivoTipificadoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de MotivoTipificadoDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].codigo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].categoria` | CategoriaMotivo (texto) | sí | valores: Documento · Formulario · Contacto · Otro |
| `[].aplicaA` | AplicaMotivo (texto) | sí | valores: Observacion · Rechazo · Ambos |
| `[].textoAlTercero` | texto | sí |  |
| `[].base` | booleano | sí |  |
| `[].activo` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/motivos" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "categoria": "Documento",
    "aplicaA": "Observacion",
    "textoAlTercero": "Texto de ejemplo.",
    "base": true,
    "activo": true
  }
]
```

</details>

### `POST /api/v1/motivos`

**Crear motivo.**

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `crearMotivo`**

**Cuerpo de la petición** (`application/json`, CrearMotivoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `categoria` | CategoriaMotivo (texto) | sí | valores: Documento · Formulario · Contacto · Otro |
| `aplicaA` | AplicaMotivo (texto) | sí | valores: Observacion · Rechazo · Ambos |
| `textoAlTercero` | texto | sí |  |

**Respuestas**

- `201` Creado — MotivoTipificadoDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `categoria` | CategoriaMotivo (texto) | sí | valores: Documento · Formulario · Contacto · Otro |
| `aplicaA` | AplicaMotivo (texto) | sí | valores: Observacion · Rechazo · Ambos |
| `textoAlTercero` | texto | sí |  |
| `base` | booleano | sí |  |
| `activo` | booleano | sí |  |

**Errores de negocio que lanza**

- `409` `motivo-duplicado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/motivos" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S.","categoria":"Documento","aplicaA":"Observacion","textoAlTercero":"Texto de ejemplo."}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "categoria": "Documento",
  "aplicaA": "Observacion",
  "textoAlTercero": "Texto de ejemplo.",
  "base": true,
  "activo": true
}
```

</details>

### `PUT /api/v1/motivos/{id}`

**Actualizar motivo.** El código es inmutable; los motivos de la plataforma no se editan (409 `motivo-base`).

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `actualizarMotivo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarMotivoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombre` | texto | sí |  |
| `categoria` | CategoriaMotivo (texto) | sí | valores: Documento · Formulario · Contacto · Otro |
| `aplicaA` | AplicaMotivo (texto) | sí | valores: Observacion · Rechazo · Ambos |
| `textoAlTercero` | texto | sí |  |
| `activo` | booleano o null | no |  |

**Respuestas**

- `200` OK — MotivoTipificadoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `categoria` | CategoriaMotivo (texto) | sí | valores: Documento · Formulario · Contacto · Otro |
| `aplicaA` | AplicaMotivo (texto) | sí | valores: Observacion · Rechazo · Ambos |
| `textoAlTercero` | texto | sí |  |
| `base` | booleano | sí |  |
| `activo` | booleano | sí |  |

**Errores de negocio que lanza**

- `404` `motivo-no-encontrado`
- `409` `motivo-base`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/motivos/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Constructora Norte S.A.S.","categoria":"Documento","aplicaA":"Observacion","textoAlTercero":"Texto de ejemplo.","activo":true}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "categoria": "Documento",
  "aplicaA": "Observacion",
  "textoAlTercero": "Texto de ejemplo.",
  "base": true,
  "activo": true
}
```

</details>

## Plantillas notificación

Plantillas de notificación de la empresa activa (docs/05 §2, docs/06 `/configuracion/.../plantillas`, PL-1): asunto, cabecera y pie de los correos que Validasoft le manda al tercero en nombre de la empresa, con auditoría y vista previa.

### `GET /api/v1/plantillas-notificacion`

**Listar plantillas notificación.** El catálogo completo con lo personalizado de la empresa (sin personalizar: textos nulos y versión 0).

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarPlantillasNotificacion`**

**Respuestas**

- `200` OK — lista de PlantillaNotificacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de PlantillaNotificacionDto | sí |  |
| `[].evento` | texto | sí |  |
| `[].canal` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].descripcion` | texto | sí |  |
| `[].asuntoPorDefecto` | texto | sí |  |
| `[].asunto` | texto o null | no |  |
| `[].cabecera` | texto o null | no |  |
| `[].pie` | texto o null | no |  |
| `[].variablesPermitidas` | lista de texto | sí |  |
| `[].version` | entero | sí |  |
| `[].actualizadaEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plantillas-notificacion" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
  {
    "evento": "texto",
    "canal": "texto",
    "nombre": "Constructora Norte S.A.S.",
    "descripcion": "Texto de ejemplo.",
    "asuntoPorDefecto": "texto",
    "asunto": "texto",
    "cabecera": "texto",
    "pie": "texto",
    "variablesPermitidas": [
      "texto"
    ],
    "version": 1,
    "actualizadaEn": "2026-11-12T15:00:00Z"
  }
]
```

</details>

### `PUT /api/v1/plantillas-notificacion/{evento}/{canal}`

**Actualizar plantilla notificación.** Guarda asunto, cabecera y pie (vacío ⇒ nulo; todo nulo = vuelve al predeterminado) y sube la versión. 404 `plantilla-no-encontrada` fuera del catálogo; 422 `canal-invalido`, `asunto-invalido`, `asunto-demasiado-largo`, `texto-demasiado-largo` o `variable-no-permitida` (con `campo`).

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarPlantillaNotificacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `evento` | path | texto | sí |  |
| `canal` | path | texto | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarPlantillaNotificacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `asunto` | texto o null | no |  |
| `cabecera` | texto o null | no |  |
| `pie` | texto o null | no |  |

**Respuestas**

- `200` OK — PlantillaNotificacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `evento` | texto | sí |  |
| `canal` | texto | sí |  |
| `nombre` | texto | sí |  |
| `descripcion` | texto | sí |  |
| `asuntoPorDefecto` | texto | sí |  |
| `asunto` | texto o null | no |  |
| `cabecera` | texto o null | no |  |
| `pie` | texto o null | no |  |
| `variablesPermitidas` | lista de texto | sí |  |
| `version` | entero | sí |  |
| `actualizadaEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/plantillas-notificacion/texto/texto" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"asunto":"texto","cabecera":"texto","pie":"texto"}'
```

Respuesta `200`:

```json
{
  "evento": "texto",
  "canal": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "descripcion": "Texto de ejemplo.",
  "asuntoPorDefecto": "texto",
  "asunto": "texto",
  "cabecera": "texto",
  "pie": "texto",
  "variablesPermitidas": [
    "texto"
  ],
  "version": 1,
  "actualizadaEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /api/v1/plantillas-notificacion/{evento}/{canal}/vista-previa`

**Previsualizar plantilla notificación.** El correo con lo que se está editando (sin guardar; mismas validaciones) armado con el redactor real y datos de ejemplo: la empresa activa, un folio, un tercero y un documento de ejemplo.

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `previsualizarPlantillaNotificacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `evento` | path | texto | sí |  |
| `canal` | path | texto | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarPlantillaNotificacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `asunto` | texto o null | no |  |
| `cabecera` | texto o null | no |  |
| `pie` | texto o null | no |  |

**Respuestas**

- `200` OK — VistaPreviaCorreoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `asunto` | texto | sí |  |
| `texto` | texto | sí |  |
| `html` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plantillas-notificacion/texto/texto/vista-previa" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"asunto":"texto","cabecera":"texto","pie":"texto"}'
```

Respuesta `200`:

```json
{
  "asunto": "texto",
  "texto": "Texto de ejemplo.",
  "html": "texto"
}
```

</details>

## Tipos vínculo

### `GET /api/v1/tipos-vinculo`

**Listar tipos vínculo.**

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `listarTiposVinculo`**

**Respuestas**

- `200` OK — lista de TipoVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de TipoVinculoDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].codigo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].atributosPermitidos` | lista de AtributoPermitidoDto | sí |  |
| `[].atributosPermitidos[].clave` | texto | sí |  |
| `[].atributosPermitidos[].tipo` | texto | sí |  |
| `[].atributosPermitidos[].obligatorio` | booleano | sí |  |
| `[].atributosPermitidos[].opciones` | lista de texto o null | no |  |
| `[].exigeCuentaDesignada` | booleano | sí |  |
| `[].activo` | booleano | sí |  |
| `[].exigeOtpEnEnlace` | booleano | no | por omisión false |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/tipos-vinculo" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "atributosPermitidos": [
      {
        "clave": "texto",
        "tipo": "texto",
        "obligatorio": true,
        "opciones": [
          "texto"
        ]
      }
    ],
    "exigeCuentaDesignada": true,
    "activo": true,
    "exigeOtpEnEnlace": false
  }
]
```

</details>

### `POST /api/v1/tipos-vinculo`

**Crear tipo vínculo.**

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `crearTipoVinculo`**

**Cuerpo de la petición** (`application/json`, CrearTipoVinculoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `atributosPermitidos` | lista de AtributoPermitidoDto o null | no |  |
| `atributosPermitidos[].clave` | texto | sí |  |
| `atributosPermitidos[].tipo` | texto | sí |  |
| `atributosPermitidos[].obligatorio` | booleano | sí |  |
| `atributosPermitidos[].opciones` | lista de texto o null | no |  |
| `exigeCuentaDesignada` | booleano | sí |  |
| `exigeOtpEnEnlace` | booleano | no | por omisión false |

**Respuestas**

- `201` Creado — TipoVinculoDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `atributosPermitidos` | lista de AtributoPermitidoDto | sí |  |
| `atributosPermitidos[].clave` | texto | sí |  |
| `atributosPermitidos[].tipo` | texto | sí |  |
| `atributosPermitidos[].obligatorio` | booleano | sí |  |
| `atributosPermitidos[].opciones` | lista de texto o null | no |  |
| `exigeCuentaDesignada` | booleano | sí |  |
| `activo` | booleano | sí |  |
| `exigeOtpEnEnlace` | booleano | no | por omisión false |

**Errores de negocio que lanza**

- `409` `tipo-vinculo-duplicado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/tipos-vinculo" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S.","atributosPermitidos":[{"clave":"texto","tipo":"texto","obligatorio":true,"opciones":["texto"]}],"exigeCuentaDesignada":true,"exigeOtpEnEnlace":false}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "atributosPermitidos": [
    {
      "clave": "texto",
      "tipo": "texto",
      "obligatorio": true,
      "opciones": [
        "texto"
      ]
    }
  ],
  "exigeCuentaDesignada": true,
  "activo": true,
  "exigeOtpEnEnlace": false
}
```

</details>

### `GET /api/v1/tipos-vinculo/{id}`

**Obtener tipo vínculo.**

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `obtenerTipoVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — TipoVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `atributosPermitidos` | lista de AtributoPermitidoDto | sí |  |
| `atributosPermitidos[].clave` | texto | sí |  |
| `atributosPermitidos[].tipo` | texto | sí |  |
| `atributosPermitidos[].obligatorio` | booleano | sí |  |
| `atributosPermitidos[].opciones` | lista de texto o null | no |  |
| `exigeCuentaDesignada` | booleano | sí |  |
| `activo` | booleano | sí |  |
| `exigeOtpEnEnlace` | booleano | no | por omisión false |

**Errores de negocio que lanza**

- `404` `tipo-vinculo-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/tipos-vinculo/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "atributosPermitidos": [
    {
      "clave": "texto",
      "tipo": "texto",
      "obligatorio": true,
      "opciones": [
        "texto"
      ]
    }
  ],
  "exigeCuentaDesignada": true,
  "activo": true,
  "exigeOtpEnEnlace": false
}
```

</details>

### `PUT /api/v1/tipos-vinculo/{id}`

**Actualizar tipo vínculo.** El código es inmutable; se editan nombre, atributos, cuenta designada, OTP en el enlace y estado.

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `actualizarTipoVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `activo` | query | booleano | no |  |

**Cuerpo de la petición** (`application/json`, CrearTipoVinculoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `atributosPermitidos` | lista de AtributoPermitidoDto o null | no |  |
| `atributosPermitidos[].clave` | texto | sí |  |
| `atributosPermitidos[].tipo` | texto | sí |  |
| `atributosPermitidos[].obligatorio` | booleano | sí |  |
| `atributosPermitidos[].opciones` | lista de texto o null | no |  |
| `exigeCuentaDesignada` | booleano | sí |  |
| `exigeOtpEnEnlace` | booleano | no | por omisión false |

**Respuestas**

- `200` OK — TipoVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `atributosPermitidos` | lista de AtributoPermitidoDto | sí |  |
| `atributosPermitidos[].clave` | texto | sí |  |
| `atributosPermitidos[].tipo` | texto | sí |  |
| `atributosPermitidos[].obligatorio` | booleano | sí |  |
| `atributosPermitidos[].opciones` | lista de texto o null | no |  |
| `exigeCuentaDesignada` | booleano | sí |  |
| `activo` | booleano | sí |  |
| `exigeOtpEnEnlace` | booleano | no | por omisión false |

**Errores de negocio que lanza**

- `404` `tipo-vinculo-no-encontrado`
- `422` `codigo-inmutable`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/tipos-vinculo/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S.","atributosPermitidos":[{"clave":"texto","tipo":"texto","obligatorio":true,"opciones":["texto"]}],"exigeCuentaDesignada":true,"exigeOtpEnEnlace":false}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "atributosPermitidos": [
    {
      "clave": "texto",
      "tipo": "texto",
      "obligatorio": true,
      "opciones": [
        "texto"
      ]
    }
  ],
  "exigeCuentaDesignada": true,
  "activo": true,
  "exigeOtpEnEnlace": false
}
```

</details>
