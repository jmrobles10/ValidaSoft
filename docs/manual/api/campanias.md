# API · Campañas

10 operaciones en 1 recurso.

## Campañas

Campañas de renovación de la empresa activa (OP-1…OP-6): se elige a los vínculos por filtro (vencen en N días, tipo de vínculo y estado), la **vista previa** dice a quién afecta antes de confirmar y la campaña abre su renovación en lote, por tandas en segundo plano, reanudable y con informe por vínculo en Excel. Leer pide `expedientes:leer`; crear, pausar, reanudar, cancelar y reintentar, `expedientes:administrar` (abre expedientes).

### `GET /api/v1/campanias`

**Listar campañas.** Campañas de la empresa, de la más reciente a la más antigua; cursor opaco.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarCampanias`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfCampaniaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de CampaniaDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].nombre` | texto | sí |  |
| `datos[].filtro` | FiltroDeCampaniaDto | sí |  |
| `datos[].filtro.diasParaVencer` | entero | no | por omisión 30 |
| `datos[].filtro.tipoVinculoId` | uuid o null | no |  |
| `datos[].filtro.estados` | lista de texto o null | no |  |
| `datos[].estado` | EstadoCampania (texto) | sí | valores: Procesando · Pausada · Completada · CompletadaConErrores · Cancelada |
| `datos[].totales` | TotalesCampaniaDto | sí |  |
| `datos[].totales.filas` | entero | sí |  |
| `datos[].totales.procesadas` | entero | sí |  |
| `datos[].totales.pendientes` | entero | sí |  |
| `datos[].totales.exitosas` | entero | sí |  |
| `datos[].totales.fallidas` | entero | sí |  |
| `datos[].totales.omitidas` | entero | sí |  |
| `datos[].creadaPor` | texto | sí |  |
| `datos[].creadaEn` | fecha y hora | sí |  |
| `datos[].actualizadaEn` | fecha y hora | sí |  |
| `datos[].terminadaEn` | fecha y hora o null | no |  |
| `datos[].reintentos` | entero | sí |  |
| `datos[].version` | entero | sí |  |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/campanias" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "filtro": {
        "diasParaVencer": 30,
        "tipoVinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "estados": [
          "texto"
        ]
      },
      "estado": "Procesando",
      "totales": {
        "filas": 1,
        "procesadas": 1,
        "pendientes": 1,
        "exitosas": 1,
        "fallidas": 1,
        "omitidas": 1
      },
      "creadaPor": "texto",
      "creadaEn": "2026-11-12T15:00:00Z",
      "actualizadaEn": "2026-11-12T15:00:00Z",
      "terminadaEn": "2026-11-12T15:00:00Z",
      "reintentos": 1,
      "version": 1
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `POST /api/v1/campanias`

**Crear campaña.** Crea la campaña con todas sus filas (las que no aplican quedan `Omitida` con su motivo) y encola la primera tanda en la misma transacción (202). 422 `campania-sin-vinculos`, `campania-demasiado-grande`, `dias-invalidos`, `estado-invalido`.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `crearCampania`**

**Cuerpo de la petición** (`application/json`, CrearCampaniaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `filtro` | FiltroDeCampaniaDto | sí |  |
| `filtro.diasParaVencer` | entero | no | por omisión 30 |
| `filtro.tipoVinculoId` | uuid o null | no |  |
| `filtro.estados` | lista de texto o null | no |  |
| `nombre` | texto o null | no |  |

**Respuestas**

- `202` Aceptado — CampaniaDto

Campos de la respuesta `202`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `nombre` | texto | sí |  |
| `filtro` | FiltroDeCampaniaDto | sí |  |
| `filtro.diasParaVencer` | entero | no | por omisión 30 |
| `filtro.tipoVinculoId` | uuid o null | no |  |
| `filtro.estados` | lista de texto o null | no |  |
| `estado` | EstadoCampania (texto) | sí | valores: Procesando · Pausada · Completada · CompletadaConErrores · Cancelada |
| `totales` | TotalesCampaniaDto | sí |  |
| `totales.filas` | entero | sí |  |
| `totales.procesadas` | entero | sí |  |
| `totales.pendientes` | entero | sí |  |
| `totales.exitosas` | entero | sí |  |
| `totales.fallidas` | entero | sí |  |
| `totales.omitidas` | entero | sí |  |
| `creadaPor` | texto | sí |  |
| `creadaEn` | fecha y hora | sí |  |
| `actualizadaEn` | fecha y hora | sí |  |
| `terminadaEn` | fecha y hora o null | no |  |
| `reintentos` | entero | sí |  |
| `version` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/campanias" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"filtro":{"diasParaVencer":30,"tipoVinculoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","estados":["texto"]},"nombre":"Constructora Norte S.A.S."}'
```

Respuesta `202`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "filtro": {
    "diasParaVencer": 30,
    "tipoVinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "estados": [
      "texto"
    ]
  },
  "estado": "Procesando",
  "totales": {
    "filas": 1,
    "procesadas": 1,
    "pendientes": 1,
    "exitosas": 1,
    "fallidas": 1,
    "omitidas": 1
  },
  "creadaPor": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "terminadaEn": "2026-11-12T15:00:00Z",
  "reintentos": 1,
  "version": 1
}
```

</details>

### `GET /api/v1/campanias/{id}`

**Obtener campaña.**

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerCampania`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CampaniaDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CampaniaDto` que se detallan en `POST /api/v1/campanias`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/campanias/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "filtro": {
    "diasParaVencer": 30,
    "tipoVinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "estados": [
      "texto"
    ]
  },
  "estado": "Procesando",
  "totales": {
    "filas": 1,
    "procesadas": 1,
    "pendientes": 1,
    "exitosas": 1,
    "fallidas": 1,
    "omitidas": 1
  },
  "creadaPor": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "terminadaEn": "2026-11-12T15:00:00Z",
  "reintentos": 1,
  "version": 1
}
```

</details>

### `POST /api/v1/campanias/{id}/cancelar`

**Cancelar campaña.** Cancela la campaña: lo pendiente ya no se procesa. Lo ya abierto sigue su curso. Terminal.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `cancelarCampania`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CampaniaDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CampaniaDto` que se detallan en `POST /api/v1/campanias`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/campanias/3fa85f64-5717-4562-b3fc-2c963f66afa6/cancelar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "filtro": {
    "diasParaVencer": 30,
    "tipoVinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "estados": [
      "texto"
    ]
  },
  "estado": "Procesando",
  "totales": {
    "filas": 1,
    "procesadas": 1,
    "pendientes": 1,
    "exitosas": 1,
    "fallidas": 1,
    "omitidas": 1
  },
  "creadaPor": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "terminadaEn": "2026-11-12T15:00:00Z",
  "reintentos": 1,
  "version": 1
}
```

</details>

### `GET /api/v1/campanias/{id}/filas`

**Listar filas campaña.** Filas en orden, con filtro opcional por estado (`Pendiente`, `Exitosa`, `Fallida`, `Omitida`); el cursor es el número de la última fila leída.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarFilasCampania`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `estado` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfFilaCampaniaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de FilaCampaniaDto | sí |  |
| `datos[].n` | entero | sí |  |
| `datos[].vinculoId` | uuid | sí |  |
| `datos[].terceroId` | uuid | sí |  |
| `datos[].tercero` | texto | sí |  |
| `datos[].identificacion` | texto | sí |  |
| `datos[].tipoVinculo` | texto | sí |  |
| `datos[].estadoVinculo` | texto | sí |  |
| `datos[].venceEn` | fecha o null | no |  |
| `datos[].estado` | EstadoFilaCampania (texto) | sí | valores: Pendiente · Exitosa · Fallida · Omitida · Cancelada |
| `datos[].accion` | texto o null | no |  |
| `datos[].detalle` | texto o null | no |  |
| `datos[].expedienteId` | uuid o null | no |  |
| `datos[].folio` | texto o null | no |  |
| `datos[].errores` | lista de ErrorDeFilaDto | sí |  |
| `datos[].errores[].campo` | texto o null | no |  |
| `datos[].errores[].codigo` | texto | sí |  |
| `datos[].errores[].mensaje` | texto | sí |  |
| `datos[].intentos` | entero | sí |  |
| `datos[].procesadaEn` | fecha y hora o null | no |  |
| `siguienteCursor` | texto o null | no |  |

**Errores de negocio que lanza**

- `422` `cursor-invalido`
- `422` `estado-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/campanias/3fa85f64-5717-4562-b3fc-2c963f66afa6/filas" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "n": 1,
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tercero": "texto",
      "identificacion": "900123456",
      "tipoVinculo": "texto",
      "estadoVinculo": "texto",
      "venceEn": "2026-11-12",
      "estado": "Pendiente",
      "accion": "texto",
      "detalle": "Texto de ejemplo.",
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "errores": [
        {
          "campo": "texto",
          "codigo": "CODIGO_EJEMPLO",
          "mensaje": "Texto de ejemplo."
        }
      ],
      "intentos": 1,
      "procesadaEn": "2026-11-12T15:00:00Z"
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/campanias/{id}/informe`

**Descargar informe campaña.** Informe por vínculo en Excel: tercero, tipo, estado, vencimiento, resultado, expediente abierto y detalle.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `descargarInformeCampania`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/campanias/3fa85f64-5717-4562-b3fc-2c963f66afa6/informe" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>

### `POST /api/v1/campanias/{id}/pausar`

**Pausar campaña.** Pausa el procesamiento (el vínculo en curso termina). 409 `estado-invalido` o `campania-en-estado-terminal`.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `pausarCampania`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CampaniaDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CampaniaDto` que se detallan en `POST /api/v1/campanias`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/campanias/3fa85f64-5717-4562-b3fc-2c963f66afa6/pausar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "filtro": {
    "diasParaVencer": 30,
    "tipoVinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "estados": [
      "texto"
    ]
  },
  "estado": "Procesando",
  "totales": {
    "filas": 1,
    "procesadas": 1,
    "pendientes": 1,
    "exitosas": 1,
    "fallidas": 1,
    "omitidas": 1
  },
  "creadaPor": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "terminadaEn": "2026-11-12T15:00:00Z",
  "reintentos": 1,
  "version": 1
}
```

</details>

### `POST /api/v1/campanias/{id}/reanudar`

**Reanudar campaña.** Reanuda una campaña pausada: sigue con los vínculos pendientes.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `reanudarCampania`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CampaniaDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CampaniaDto` que se detallan en `POST /api/v1/campanias`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/campanias/3fa85f64-5717-4562-b3fc-2c963f66afa6/reanudar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "filtro": {
    "diasParaVencer": 30,
    "tipoVinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "estados": [
      "texto"
    ]
  },
  "estado": "Procesando",
  "totales": {
    "filas": 1,
    "procesadas": 1,
    "pendientes": 1,
    "exitosas": 1,
    "fallidas": 1,
    "omitidas": 1
  },
  "creadaPor": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "terminadaEn": "2026-11-12T15:00:00Z",
  "reintentos": 1,
  "version": 1
}
```

</details>

### `POST /api/v1/campanias/{id}/reintentar`

**Reintentar campaña.** Vuelve a procesar las filas fallidas de una campaña terminada (solo ellas). 409 `sin-filas-fallidas`, `estado-invalido` o `campania-en-estado-terminal`.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `reintentarCampania`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CampaniaDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CampaniaDto` que se detallan en `POST /api/v1/campanias`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/campanias/3fa85f64-5717-4562-b3fc-2c963f66afa6/reintentar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "filtro": {
    "diasParaVencer": 30,
    "tipoVinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "estados": [
      "texto"
    ]
  },
  "estado": "Procesando",
  "totales": {
    "filas": 1,
    "procesadas": 1,
    "pendientes": 1,
    "exitosas": 1,
    "fallidas": 1,
    "omitidas": 1
  },
  "creadaPor": "texto",
  "creadaEn": "2026-11-12T15:00:00Z",
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "terminadaEn": "2026-11-12T15:00:00Z",
  "reintentos": 1,
  "version": 1
}
```

</details>

### `POST /api/v1/campanias/vista-previa`

**Previsualizar campaña.** A quién afectaría el filtro, sin crear nada: por vínculo, qué se haría (abrir su renovación, recordarla u omitirlo y por qué).

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `previsualizarCampania`**

**Cuerpo de la petición** (`application/json`, FiltroDeCampaniaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `diasParaVencer` | entero | no | por omisión 30 |
| `tipoVinculoId` | uuid o null | no |  |
| `estados` | lista de texto o null | no |  |

**Respuestas**

- `200` OK — VistaPreviaCampaniaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `filtro` | FiltroDeCampaniaDto | sí |  |
| `filtro.diasParaVencer` | entero | no | por omisión 30 |
| `filtro.tipoVinculoId` | uuid o null | no |  |
| `filtro.estados` | lista de texto o null | no |  |
| `total` | entero | sí |  |
| `abririan` | entero | sí |  |
| `recordarian` | entero | sí |  |
| `omitidos` | entero | sí |  |
| `conCorreo` | entero | sí |  |
| `truncada` | booleano | sí |  |
| `filas` | lista de FilaVistaPreviaCampaniaDto | sí |  |
| `filas[].n` | entero | sí |  |
| `filas[].vinculoId` | uuid | sí |  |
| `filas[].tercero` | TerceroResumenDto | sí |  |
| `filas[].tercero.id` | uuid | sí |  |
| `filas[].tercero.nombre` | texto | sí |  |
| `filas[].tercero.identificacion` | texto | sí |  |
| `filas[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `filas[].tipoVinculo.id` | uuid | sí |  |
| `filas[].tipoVinculo.codigo` | texto | sí |  |
| `filas[].tipoVinculo.nombre` | texto | sí |  |
| `filas[].estadoVinculo` | texto | sí |  |
| `filas[].venceEn` | fecha o null | no |  |
| `filas[].accion` | texto | sí |  |
| `filas[].motivo` | texto o null | no |  |
| `filas[].conCorreo` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/campanias/vista-previa" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"diasParaVencer":30,"tipoVinculoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","estados":["texto"]}'
```

Respuesta `200`:

```json
{
  "filtro": {
    "diasParaVencer": 30,
    "tipoVinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "estados": [
      "texto"
    ]
  },
  "total": 1,
  "abririan": 1,
  "recordarian": 1,
  "omitidos": 1,
  "conCorreo": 1,
  "truncada": true,
  "filas": [
    {
      "n": 1,
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tercero": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "identificacion": "900123456"
      },
      "tipoVinculo": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S."
      },
      "estadoVinculo": "texto",
      "venceEn": "2026-11-12",
      "accion": "texto",
      "motivo": "Texto de ejemplo.",
      "conCorreo": true
    }
  ]
}
```

</details>
