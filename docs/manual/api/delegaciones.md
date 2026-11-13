# API · Delegaciones

4 operaciones en 1 recurso.

## Delegaciones

Delegación de revisión por ausencias (OP-7, OP-8): crear, consultar y revocar. Quien tiene `expedientes:revisar` delega lo suyo; un administrador de expedientes puede delegar en nombre de otra persona. Cada alta y cada revocación queda en la auditoría de configuración y cada decisión tomada en delegación lleva en el historial de eventos de quién era (`delegadoDe`).

### `GET /api/v1/delegaciones`

**Listar delegaciones.** Delegaciones de la empresa: todas para quien administra expedientes; las propias (que dio o recibió) para los demás. `estado` opcional.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarDelegaciones`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `estado` | query | texto | no |  |

**Respuestas**

- `200` OK — lista de DelegacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de DelegacionDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].deUsuarioId` | uuid | sí |  |
| `[].de` | texto | sí |  |
| `[].aUsuarioId` | uuid | sí |  |
| `[].a` | texto | sí |  |
| `[].areas` | lista de AreaDeDelegacionDto | sí |  |
| `[].areas[].areaId` | uuid | sí |  |
| `[].areas[].codigo` | texto | sí |  |
| `[].areas[].nombre` | texto | sí |  |
| `[].todasLasAreas` | booleano | sí |  |
| `[].desde` | fecha | sí |  |
| `[].hasta` | fecha | sí |  |
| `[].motivo` | texto o null | no |  |
| `[].estado` | EstadoDeDelegacion (texto) | sí | valores: Programada · Vigente · Vencida · Revocada |
| `[].creadaPor` | texto | sí |  |
| `[].creadaEn` | fecha y hora | sí |  |
| `[].revocadaEn` | fecha y hora o null | no |  |
| `[].motivoDeRevocacion` | texto o null | no |  |

**Errores de negocio que lanza**

- `422` `estado-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/delegaciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "deUsuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "de": "texto",
    "aUsuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "a": "texto",
    "areas": [
      {
        "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S."
      }
    ],
    "todasLasAreas": true,
    "desde": "2026-11-12",
    "hasta": "2026-11-12",
    "motivo": "Texto de ejemplo.",
    "estado": "Programada",
    "creadaPor": "texto",
    "creadaEn": "2026-11-12T15:00:00Z",
    "revocadaEn": "2026-11-12T15:00:00Z",
    "motivoDeRevocacion": "Texto de ejemplo."
  }
]
```

</details>

### `POST /api/v1/delegaciones`

**Crear delegación.** Delega la revisión. `deUsuarioId` es quien llama si no se indica; delegar lo de otra persona pide `expedientes:administrar` (403 `delegacion-ajena`). 422 si `a` no puede revisar (`delegado-sin-permiso-de-revision`), si quien delega no es miembro de un área (`no-es-miembro-del-area`) o no tiene áreas (`sin-areas-que-delegar`); 409 `delegacion-solapada` si ya hay una igual en esas fechas.

**Permiso expedientes:revisar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `crearDelegacion`**

**Cuerpo de la petición** (`application/json`, CrearDelegacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `aUsuarioId` | uuid | sí |  |
| `desde` | fecha | sí |  |
| `hasta` | fecha | sí |  |
| `areas` | lista de uuid o null | no |  |
| `deUsuarioId` | uuid o null | no |  |
| `motivo` | texto o null | no |  |

**Respuestas**

- `200` OK — DelegacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `deUsuarioId` | uuid | sí |  |
| `de` | texto | sí |  |
| `aUsuarioId` | uuid | sí |  |
| `a` | texto | sí |  |
| `areas` | lista de AreaDeDelegacionDto | sí |  |
| `areas[].areaId` | uuid | sí |  |
| `areas[].codigo` | texto | sí |  |
| `areas[].nombre` | texto | sí |  |
| `todasLasAreas` | booleano | sí |  |
| `desde` | fecha | sí |  |
| `hasta` | fecha | sí |  |
| `motivo` | texto o null | no |  |
| `estado` | EstadoDeDelegacion (texto) | sí | valores: Programada · Vigente · Vencida · Revocada |
| `creadaPor` | texto | sí |  |
| `creadaEn` | fecha y hora | sí |  |
| `revocadaEn` | fecha y hora o null | no |  |
| `motivoDeRevocacion` | texto o null | no |  |

**Errores de negocio que lanza**

- `403` `delegacion-ajena`
- `404` `area-no-encontrada`
- `409` `delegacion-solapada`
- `422` `delegado-sin-permiso-de-revision`
- `422` `no-es-miembro-del-area`
- `422` `sin-areas-que-delegar`
- `422` `usuario-invalido`
- `422` `usuario-requerido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/delegaciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"aUsuarioId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","desde":"2026-11-12","hasta":"2026-11-12","areas":["3fa85f64-5717-4562-b3fc-2c963f66afa6"],"deUsuarioId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","motivo":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "deUsuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "de": "texto",
  "aUsuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "a": "texto",
  "areas": [
    {
      "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S."
    }
  ],
  "todasLasAreas": true,
  "desde": "2026-11-12",
  "hasta": "2026-11-12",
  "motivo": "Texto de ejemplo.",
  "estado": "Programada",
  "creadaPor": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "revocadaEn": "2026-11-12T15:00:00Z",
  "motivoDeRevocacion": "Texto de ejemplo."
}
```

</details>

### `POST /api/v1/delegaciones/{id}/revocar`

**Revocar delegación.** Revoca una delegación programada o vigente: la puede revocar quien delegó, el delegado o un administrador de expedientes (409 `delegacion-no-revocable`).

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `revocarDelegacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, RevocarDelegacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `motivo` | texto o null | no |  |

**Respuestas**

- `200` OK — DelegacionDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `DelegacionDto` que se detallan en `POST /api/v1/delegaciones`.

**Errores de negocio que lanza**

- `404` `delegacion-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/delegaciones/3fa85f64-5717-4562-b3fc-2c963f66afa6/revocar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"motivo":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "deUsuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "de": "texto",
  "aUsuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "a": "texto",
  "areas": [
    {
      "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S."
    }
  ],
  "todasLasAreas": true,
  "desde": "2026-11-12",
  "hasta": "2026-11-12",
  "motivo": "Texto de ejemplo.",
  "estado": "Programada",
  "creadaPor": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "revocadaEn": "2026-11-12T15:00:00Z",
  "motivoDeRevocacion": "Texto de ejemplo."
}
```

</details>

### `GET /api/v1/delegaciones/candidatos`

**Listar candidatos de delegación.** Quienes pueden recibir una delegación: activos, con rol en la empresa y permiso de revisar.

**Permiso expedientes:revisar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarCandidatosDeDelegacion`**

**Respuestas**

- `200` OK — lista de CandidatoDeDelegacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de CandidatoDeDelegacionDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].nombre` | texto | sí |  |
| `[].correo` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/delegaciones/candidatos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S.",
    "correo": "contacto@empresa.co"
  }
]
```

</details>
