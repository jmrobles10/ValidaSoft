# API · Habeas

18 operaciones en 5 recursos.

## Exportación de bóveda

### `POST /public/v1/boveda/exportar`

**Exportar bóveda.** Pide la exportación (202): un ZIP con los archivos de la bóveda, sus datos en JSON y un manifiesto firmado con el SHA-256 de cada archivo. 409 `exportacion-en-curso` si ya hay una generándose; 429 `demasiadas-exportaciones` pasado el tope diario.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `exportarBoveda`**

**Respuestas**

- `202` Aceptado — ExportacionBovedaDto

Campos de la respuesta `202`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `estado` | texto | sí |  |
| `solicitadaEn` | fecha y hora | sí |  |
| `terminadaEn` | fecha y hora o null | no |  |
| `expiraEn` | fecha y hora o null | no |  |
| `archivos` | entero | sí |  |
| `bytes` | entero o null | no |  |
| `sha256` | texto o null | no |  |
| `hashManifiesto` | texto o null | no |  |
| `kid` | texto o null | no |  |
| `codigoDeError` | texto o null | no |  |
| `descargas` | entero | sí |  |

**Errores de negocio que lanza**

- `409` `exportacion-en-curso`
- `429` `demasiadas-exportaciones`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/boveda/exportar" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `202`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "texto",
  "solicitadaEn": "2026-11-12T15:00:00Z",
  "terminadaEn": "2026-11-12T15:00:00Z",
  "expiraEn": "2026-11-12T15:00:00Z",
  "archivos": 1,
  "bytes": 1,
  "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "hashManifiesto": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "kid": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigoDeError": "codigo-de-ejemplo",
  "descargas": 1
}
```

</details>

### `GET /public/v1/exportaciones`

**Listar exportaciones bóveda.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `listarExportacionesBoveda`**

**Respuestas**

- `200` OK — lista de ExportacionBovedaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de ExportacionBovedaDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].estado` | texto | sí |  |
| `[].solicitadaEn` | fecha y hora | sí |  |
| `[].terminadaEn` | fecha y hora o null | no |  |
| `[].expiraEn` | fecha y hora o null | no |  |
| `[].archivos` | entero | sí |  |
| `[].bytes` | entero o null | no |  |
| `[].sha256` | texto o null | no |  |
| `[].hashManifiesto` | texto o null | no |  |
| `[].kid` | texto o null | no |  |
| `[].codigoDeError` | texto o null | no |  |
| `[].descargas` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/exportaciones" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "estado": "texto",
    "solicitadaEn": "2026-11-12T15:00:00Z",
    "terminadaEn": "2026-11-12T15:00:00Z",
    "expiraEn": "2026-11-12T15:00:00Z",
    "archivos": 1,
    "bytes": 1,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "hashManifiesto": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "kid": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigoDeError": "codigo-de-ejemplo",
    "descargas": 1
  }
]
```

</details>

### `GET /public/v1/exportaciones/{id}`

**Obtener exportación bóveda.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerExportacionBoveda`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — ExportacionBovedaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `estado` | texto | sí |  |
| `solicitadaEn` | fecha y hora | sí |  |
| `terminadaEn` | fecha y hora o null | no |  |
| `expiraEn` | fecha y hora o null | no |  |
| `archivos` | entero | sí |  |
| `bytes` | entero o null | no |  |
| `sha256` | texto o null | no |  |
| `hashManifiesto` | texto o null | no |  |
| `kid` | texto o null | no |  |
| `codigoDeError` | texto o null | no |  |
| `descargas` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/exportaciones/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "texto",
  "solicitadaEn": "2026-11-12T15:00:00Z",
  "terminadaEn": "2026-11-12T15:00:00Z",
  "expiraEn": "2026-11-12T15:00:00Z",
  "archivos": 1,
  "bytes": 1,
  "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "hashManifiesto": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "kid": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigoDeError": "codigo-de-ejemplo",
  "descargas": 1
}
```

</details>

### `POST /public/v1/exportaciones/{id}/descarga`

**Descargar exportación bóveda.** URL prefirmada (5 minutos) para bajar el ZIP; queda anotada la descarga (quién, cuándo, desde dónde). 409 `exportacion-no-disponible` si aún no está lista; 410 `exportacion-vencida`.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `descargarExportacionBoveda`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — DescargaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `url` | texto | sí |  |
| `expira` | fecha y hora | sí |  |
| `nombre` | texto | sí |  |
| `mime` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/exportaciones/3fa85f64-5717-4562-b3fc-2c963f66afa6/descarga" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "url": "https://ejemplo.co/recurso",
  "expira": "2026-11-12T15:00:00Z",
  "nombre": "Constructora Norte S.A.S.",
  "mime": "texto"
}
```

</details>

## Plazos de habeas

Ejecución inmediata del barrido (Plataforma): para operar y para las pruebas de punta a punta.

### `POST /api/v1/plataforma/habeas/revisar-plazos`

**Revisar plazos de habeas.** Revisa ya los plazos de todas las solicitudes abiertas y avisa lo que toque. `fecha` simula «ahora» y solo se admite en Development (422 `fecha-simulada-no-permitida` en otro entorno).

**Operador de Plataforma · API (`/api/v1`) · SDK: `revisarPlazosDeHabeas`**

**Cuerpo de la petición** (`application/json`, RevisarPlazosDeHabeasDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `fecha` | fecha y hora o null | no |  |

**Respuestas**

- `200` OK — ResultadoDeRevisionDeHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `revisadas` | entero | sí |  |
| `porVencer` | entero | sí |  |
| `vencidas` | entero | sí |  |

**Errores de negocio que lanza**

- `422` `fecha-simulada-no-permitida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/habeas/revisar-plazos" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"fecha":"2026-11-12T15:00:00Z"}'
```

Respuesta `200`:

```json
{
  "revisadas": 1,
  "porVencer": 1,
  "vencidas": 1
}
```

</details>

## Solicitudes habeas de la empresa

### `GET /api/v1/solicitudes-habeas`

**Listar solicitudes habeas de la empresa.** Las solicitudes de la empresa, las abiertas primero (las que vencen antes arriba). `estado`: `abiertas`, `vencidas`, `resueltas` o `todas` (por omisión). Paginadas por cursor.

**Permiso supresiones:resolver · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarSolicitudesHabeasDeLaEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `estado` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfSolicitudHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de SolicitudHabeasDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].radicado` | texto | sí |  |
| `datos[].tipo` | texto | sí |  |
| `datos[].alcance` | texto o null | no |  |
| `datos[].estado` | texto | sí |  |
| `datos[].resultado` | texto o null | no |  |
| `datos[].destino` | DestinoDeSolicitudHabeasDto | sí |  |
| `datos[].destino.empresaId` | uuid o null | no |  |
| `datos[].destino.nombre` | texto | sí |  |
| `datos[].recibidaEn` | fecha y hora | sí |  |
| `datos[].venceEl` | fecha | sí |  |
| `datos[].diasHabiles` | entero | sí |  |
| `datos[].diasHabilesRestantes` | entero | sí |  |
| `datos[].plazo` | texto | sí |  |
| `datos[].dentroDelPlazo` | booleano o null | no |  |
| `datos[].resueltaEn` | fecha y hora o null | no |  |
| `datos[].detalle` | texto o null | no |  |
| `datos[].respuesta` | texto o null | no |  |
| `datos[].contenidoSuprimido` | booleano | sí |  |
| `datos[].constanciaHash` | texto o null | no |  |
| `datos[].titular` | TitularDeSolicitudDto o null | no |  |
| `datos[].titular.nombre` | texto | sí |  |
| `datos[].titular.identificacion` | texto | sí |  |
| `datos[].titular.correo` | texto | sí |  |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/solicitudes-habeas" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "radicado": "texto",
      "tipo": "texto",
      "alcance": "texto",
      "estado": "texto",
      "resultado": "texto",
      "destino": {
        "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S."
      },
      "recibidaEn": "2026-11-12T15:00:00Z",
      "venceEl": "2026-11-12",
      "diasHabiles": 1,
      "diasHabilesRestantes": 1,
      "plazo": "texto",
      "dentroDelPlazo": true,
      "resueltaEn": "2026-11-12T15:00:00Z",
      "detalle": "Texto de ejemplo.",
      "respuesta": "texto",
      "contenidoSuprimido": true,
      "constanciaHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "titular": {
        "nombre": "Constructora Norte S.A.S.",
        "identificacion": "900123456",
        "correo": "contacto@empresa.co"
      }
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/solicitudes-habeas/{id}`

**Obtener solicitud habeas de la empresa.**

**Permiso supresiones:resolver · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerSolicitudHabeasDeLaEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — SolicitudHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `radicado` | texto | sí |  |
| `tipo` | texto | sí |  |
| `alcance` | texto o null | no |  |
| `estado` | texto | sí |  |
| `resultado` | texto o null | no |  |
| `destino` | DestinoDeSolicitudHabeasDto | sí |  |
| `destino.empresaId` | uuid o null | no |  |
| `destino.nombre` | texto | sí |  |
| `recibidaEn` | fecha y hora | sí |  |
| `venceEl` | fecha | sí |  |
| `diasHabiles` | entero | sí |  |
| `diasHabilesRestantes` | entero | sí |  |
| `plazo` | texto | sí |  |
| `dentroDelPlazo` | booleano o null | no |  |
| `resueltaEn` | fecha y hora o null | no |  |
| `detalle` | texto o null | no |  |
| `respuesta` | texto o null | no |  |
| `contenidoSuprimido` | booleano | sí |  |
| `constanciaHash` | texto o null | no |  |
| `titular` | TitularDeSolicitudDto o null | no |  |
| `titular.nombre` | texto | sí |  |
| `titular.identificacion` | texto | sí |  |
| `titular.correo` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/solicitudes-habeas/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "radicado": "texto",
  "tipo": "texto",
  "alcance": "texto",
  "estado": "texto",
  "resultado": "texto",
  "destino": {
    "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S."
  },
  "recibidaEn": "2026-11-12T15:00:00Z",
  "venceEl": "2026-11-12",
  "diasHabiles": 1,
  "diasHabilesRestantes": 1,
  "plazo": "texto",
  "dentroDelPlazo": true,
  "resueltaEn": "2026-11-12T15:00:00Z",
  "detalle": "Texto de ejemplo.",
  "respuesta": "texto",
  "contenidoSuprimido": true,
  "constanciaHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "titular": {
    "nombre": "Constructora Norte S.A.S.",
    "identificacion": "900123456",
    "correo": "contacto@empresa.co"
  }
}
```

</details>

### `POST /api/v1/solicitudes-habeas/{id}/resolver`

**Resolver solicitud habeas de la empresa.** La empresa responde: `Atender` o `Rechazar` con la respuesta que recibirá el titular (por correo y en su portal) y la constancia sellada. 409 `solicitud-ya-resuelta`; 422 `decision-invalida`, `respuesta-requerida`.

**Permiso supresiones:resolver · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `resolverSolicitudHabeasDeLaEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ResolverSolicitudHabeasDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `decision` | texto | sí |  |
| `respuesta` | texto | sí |  |
| `baseLegal` | texto o null | no |  |

**Respuestas**

- `200` OK — SolicitudHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `SolicitudHabeasDto` que se detallan en `GET /api/v1/solicitudes-habeas/{id}`.

**Errores de negocio que lanza**

- `404` `solicitud-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/solicitudes-habeas/3fa85f64-5717-4562-b3fc-2c963f66afa6/resolver" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"decision":"texto","respuesta":"texto","baseLegal":"texto"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "radicado": "texto",
  "tipo": "texto",
  "alcance": "texto",
  "estado": "texto",
  "resultado": "texto",
  "destino": {
    "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S."
  },
  "recibidaEn": "2026-11-12T15:00:00Z",
  "venceEl": "2026-11-12",
  "diasHabiles": 1,
  "diasHabilesRestantes": 1,
  "plazo": "texto",
  "dentroDelPlazo": true,
  "resueltaEn": "2026-11-12T15:00:00Z",
  "detalle": "Texto de ejemplo.",
  "respuesta": "texto",
  "contenidoSuprimido": true,
  "constanciaHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "titular": {
    "nombre": "Constructora Norte S.A.S.",
    "identificacion": "900123456",
    "correo": "contacto@empresa.co"
  }
}
```

</details>

### `GET /api/v1/solicitudes-habeas/resumen`

**Resumir solicitudes habeas de la empresa.**

**Permiso supresiones:resolver · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `resumirSolicitudesHabeasDeLaEmpresa`**

**Respuestas**

- `200` OK — ResumenDeSolicitudesHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `abiertas` | entero | sí |  |
| `porVencer` | entero | sí |  |
| `vencidas` | entero | sí |  |
| `resueltas` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/solicitudes-habeas/resumen" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "abiertas": 1,
  "porVencer": 1,
  "vencidas": 1,
  "resueltas": 1
}
```

</details>

## Solicitudes habeas de la plataforma

Lado plataforma del habeas data: la plataforma responde por la bóveda global del tercero (documentos, ficha, cuentas). La supresión se ejecuta sobre un plan: lo que ninguna aceptación ni certificado referencia se elimina; lo referenciado se conserva íntegro con su base legal y su fecha, y el titular recibe la constancia.

### `GET /api/v1/plataforma/solicitudes-habeas`

**Listar solicitudes habeas de plataforma.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarSolicitudesHabeasDePlataforma`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `estado` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfSolicitudHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `PaginaDtoOfSolicitudHabeasDto` que se detallan en `GET /api/v1/solicitudes-habeas`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/solicitudes-habeas" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "radicado": "texto",
      "tipo": "texto",
      "alcance": "texto",
      "estado": "texto",
      "resultado": "texto",
      "destino": {
        "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S."
      },
      "recibidaEn": "2026-11-12T15:00:00Z",
      "venceEl": "2026-11-12",
      "diasHabiles": 1,
      "diasHabilesRestantes": 1,
      "plazo": "texto",
      "dentroDelPlazo": true,
      "resueltaEn": "2026-11-12T15:00:00Z",
      "detalle": "Texto de ejemplo.",
      "respuesta": "texto",
      "contenidoSuprimido": true,
      "constanciaHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "titular": {
        "nombre": "Constructora Norte S.A.S.",
        "identificacion": "900123456",
        "correo": "contacto@empresa.co"
      }
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/plataforma/solicitudes-habeas/{id}`

**Obtener solicitud habeas de plataforma.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerSolicitudHabeasDePlataforma`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — SolicitudHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `SolicitudHabeasDto` que se detallan en `GET /api/v1/solicitudes-habeas/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/solicitudes-habeas/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "radicado": "texto",
  "tipo": "texto",
  "alcance": "texto",
  "estado": "texto",
  "resultado": "texto",
  "destino": {
    "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S."
  },
  "recibidaEn": "2026-11-12T15:00:00Z",
  "venceEl": "2026-11-12",
  "diasHabiles": 1,
  "diasHabilesRestantes": 1,
  "plazo": "texto",
  "dentroDelPlazo": true,
  "resueltaEn": "2026-11-12T15:00:00Z",
  "detalle": "Texto de ejemplo.",
  "respuesta": "texto",
  "contenidoSuprimido": true,
  "constanciaHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "titular": {
    "nombre": "Constructora Norte S.A.S.",
    "identificacion": "900123456",
    "correo": "contacto@empresa.co"
  }
}
```

</details>

### `GET /api/v1/plataforma/solicitudes-habeas/{id}/plan`

**Obtener plan de supresion.** Lo que ejecutaría la supresión sobre la bóveda del tercero, sin cambiar nada: qué se elimina, qué se conserva con su base legal y hasta cuándo, y qué se anonimiza. 409 `no-es-supresion`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerPlanDeSupresion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — PlanDeSupresionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `solicitudId` | uuid | sí |  |
| `alcance` | texto | sí |  |
| `suprimir` | entero | sí |  |
| `retener` | entero | sí |  |
| `anonimizar` | entero | sí |  |
| `elementos` | lista de ElementoDePlanDeSupresionDto | sí |  |
| `elementos[].tipo` | texto | sí |  |
| `elementos[].referencia` | texto | sí |  |
| `elementos[].descripcion` | texto | sí |  |
| `elementos[].accion` | texto | sí |  |
| `elementos[].baseLegal` | texto o null | no |  |
| `elementos[].retenidaHasta` | fecha o null | no |  |

**Errores de negocio que lanza**

- `409` `no-es-supresion`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/solicitudes-habeas/3fa85f64-5717-4562-b3fc-2c963f66afa6/plan" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "solicitudId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "alcance": "texto",
  "suprimir": 1,
  "retener": 1,
  "anonimizar": 1,
  "elementos": [
    {
      "tipo": "texto",
      "referencia": "REF-0001",
      "descripcion": "Texto de ejemplo.",
      "accion": "texto",
      "baseLegal": "texto",
      "retenidaHasta": "2026-11-12"
    }
  ]
}
```

</details>

### `POST /api/v1/plataforma/solicitudes-habeas/{id}/resolver`

**Resolver solicitud habeas de plataforma.** La plataforma responde. En una supresión, `Atender` ejecuta el plan (OD-6): elimina lo que ninguna empresa referencia y conserva —con su base legal— lo que sí; con el alcance «todo», además destruye la llave de datos del tercero, así lo que quedó cifrado en eventos se vuelve ilegible (OD-4). 409 `solicitud-ya-resuelta`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `resolverSolicitudHabeasDePlataforma`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ResolverSolicitudHabeasDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `decision` | texto | sí |  |
| `respuesta` | texto | sí |  |
| `baseLegal` | texto o null | no |  |

**Respuestas**

- `200` OK — SolicitudHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `SolicitudHabeasDto` que se detallan en `GET /api/v1/solicitudes-habeas/{id}`.

**Errores de negocio que lanza**

- `404` `solicitud-no-encontrada`
- `409` `solicitud-ya-resuelta`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/solicitudes-habeas/3fa85f64-5717-4562-b3fc-2c963f66afa6/resolver" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"decision":"texto","respuesta":"texto","baseLegal":"texto"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "radicado": "texto",
  "tipo": "texto",
  "alcance": "texto",
  "estado": "texto",
  "resultado": "texto",
  "destino": {
    "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S."
  },
  "recibidaEn": "2026-11-12T15:00:00Z",
  "venceEl": "2026-11-12",
  "diasHabiles": 1,
  "diasHabilesRestantes": 1,
  "plazo": "texto",
  "dentroDelPlazo": true,
  "resueltaEn": "2026-11-12T15:00:00Z",
  "detalle": "Texto de ejemplo.",
  "respuesta": "texto",
  "contenidoSuprimido": true,
  "constanciaHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "titular": {
    "nombre": "Constructora Norte S.A.S.",
    "identificacion": "900123456",
    "correo": "contacto@empresa.co"
  }
}
```

</details>

### `GET /api/v1/plataforma/solicitudes-habeas/resumen`

**Resumir solicitudes habeas de plataforma.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `resumirSolicitudesHabeasDePlataforma`**

**Respuestas**

- `200` OK — ResumenDeSolicitudesHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `abiertas` | entero | sí |  |
| `porVencer` | entero | sí |  |
| `vencidas` | entero | sí |  |
| `resueltas` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/solicitudes-habeas/resumen" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "abiertas": 1,
  "porVencer": 1,
  "vencidas": 1,
  "resueltas": 1
}
```

</details>

## Solicitudes habeas del tercero

### `GET /public/v1/solicitudes-habeas`

**Listar solicitudes habeas.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `listarSolicitudesHabeas`**

**Respuestas**

- `200` OK — lista de SolicitudHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de SolicitudHabeasDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].radicado` | texto | sí |  |
| `[].tipo` | texto | sí |  |
| `[].alcance` | texto o null | no |  |
| `[].estado` | texto | sí |  |
| `[].resultado` | texto o null | no |  |
| `[].destino` | DestinoDeSolicitudHabeasDto | sí |  |
| `[].destino.empresaId` | uuid o null | no |  |
| `[].destino.nombre` | texto | sí |  |
| `[].recibidaEn` | fecha y hora | sí |  |
| `[].venceEl` | fecha | sí |  |
| `[].diasHabiles` | entero | sí |  |
| `[].diasHabilesRestantes` | entero | sí |  |
| `[].plazo` | texto | sí |  |
| `[].dentroDelPlazo` | booleano o null | no |  |
| `[].resueltaEn` | fecha y hora o null | no |  |
| `[].detalle` | texto o null | no |  |
| `[].respuesta` | texto o null | no |  |
| `[].contenidoSuprimido` | booleano | sí |  |
| `[].constanciaHash` | texto o null | no |  |
| `[].titular` | TitularDeSolicitudDto o null | no |  |
| `[].titular.nombre` | texto | sí |  |
| `[].titular.identificacion` | texto | sí |  |
| `[].titular.correo` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/solicitudes-habeas" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "radicado": "texto",
    "tipo": "texto",
    "alcance": "texto",
    "estado": "texto",
    "resultado": "texto",
    "destino": {
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S."
    },
    "recibidaEn": "2026-11-12T15:00:00Z",
    "venceEl": "2026-11-12",
    "diasHabiles": 1,
    "diasHabilesRestantes": 1,
    "plazo": "texto",
    "dentroDelPlazo": true,
    "resueltaEn": "2026-11-12T15:00:00Z",
    "detalle": "Texto de ejemplo.",
    "respuesta": "texto",
    "contenidoSuprimido": true,
    "constanciaHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "titular": {
      "nombre": "Constructora Norte S.A.S.",
      "identificacion": "900123456",
      "correo": "contacto@empresa.co"
    }
  }
]
```

</details>

### `POST /public/v1/solicitudes-habeas`

**Crear solicitud habeas.** Presenta la solicitud. 422 `tipo-invalido`, `alcance-invalido`, `detalle-requerido` (la corrección), `detalle-demasiado-largo`; 404 `empresa-no-encontrada` si no tiene vínculo con esa empresa; 409 `solicitud-en-curso` si ya tiene una igual sin resolver.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `crearSolicitudHabeas`**

**Cuerpo de la petición** (`application/json`, CrearSolicitudHabeasDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tipo` | texto | sí |  |
| `alcance` | texto o null | no |  |
| `empresaId` | uuid o null | no |  |
| `detalle` | texto o null | no |  |

**Respuestas**

- `201` Creado — SolicitudHabeasDto

Campos de la respuesta `201`:

Mismos campos de `SolicitudHabeasDto` que se detallan en `GET /api/v1/solicitudes-habeas/{id}`.

**Errores de negocio que lanza**

- `404` `empresa-no-encontrada`
- `404` `tercero-no-encontrado`
- `409` `solicitud-en-curso`
- `422` `alcance-invalido`
- `422` `detalle-demasiado-largo`
- `422` `detalle-requerido`
- `422` `tipo-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/solicitudes-habeas" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"tipo":"texto","alcance":"texto","empresaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","detalle":"Texto de ejemplo."}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "radicado": "texto",
  "tipo": "texto",
  "alcance": "texto",
  "estado": "texto",
  "resultado": "texto",
  "destino": {
    "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S."
  },
  "recibidaEn": "2026-11-12T15:00:00Z",
  "venceEl": "2026-11-12",
  "diasHabiles": 1,
  "diasHabilesRestantes": 1,
  "plazo": "texto",
  "dentroDelPlazo": true,
  "resueltaEn": "2026-11-12T15:00:00Z",
  "detalle": "Texto de ejemplo.",
  "respuesta": "texto",
  "contenidoSuprimido": true,
  "constanciaHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "titular": {
    "nombre": "Constructora Norte S.A.S.",
    "identificacion": "900123456",
    "correo": "contacto@empresa.co"
  }
}
```

</details>

### `GET /public/v1/solicitudes-habeas/{id}`

**Obtener solicitud habeas.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerSolicitudHabeas`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — SolicitudHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `SolicitudHabeasDto` que se detallan en `GET /api/v1/solicitudes-habeas/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/solicitudes-habeas/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "radicado": "texto",
  "tipo": "texto",
  "alcance": "texto",
  "estado": "texto",
  "resultado": "texto",
  "destino": {
    "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S."
  },
  "recibidaEn": "2026-11-12T15:00:00Z",
  "venceEl": "2026-11-12",
  "diasHabiles": 1,
  "diasHabilesRestantes": 1,
  "plazo": "texto",
  "dentroDelPlazo": true,
  "resueltaEn": "2026-11-12T15:00:00Z",
  "detalle": "Texto de ejemplo.",
  "respuesta": "texto",
  "contenidoSuprimido": true,
  "constanciaHash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "titular": {
    "nombre": "Constructora Norte S.A.S.",
    "identificacion": "900123456",
    "correo": "contacto@empresa.co"
  }
}
```

</details>

### `GET /public/v1/solicitudes-habeas/{id}/constancia`

**Obtener constancia habeas.** Constancia de la respuesta, sin datos personales y con su huella SHA-256: qué se pidió, cuándo se respondió, qué se eliminó y qué se conserva por obligación legal. 409 `solicitud-sin-resolver` mientras no haya respuesta.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerConstanciaHabeas`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — ConstanciaHabeasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `radicado` | texto | sí |  |
| `tipo` | texto | sí |  |
| `alcance` | texto o null | no |  |
| `destino` | texto | sí |  |
| `recibidaEl` | fecha | sí |  |
| `resueltaEl` | fecha | sí |  |
| `dentroDelPlazo` | booleano | sí |  |
| `resultado` | texto | sí |  |
| `suprimidos` | lista de ElementoSuprimidoDto | sí |  |
| `suprimidos[].tipo` | texto | sí |  |
| `suprimidos[].referencia` | texto | sí |  |
| `suprimidos[].descripcion` | texto | sí |  |
| `retenidos` | lista de ElementoRetenidoDto | sí |  |
| `retenidos[].tipo` | texto | sí |  |
| `retenidos[].referencia` | texto | sí |  |
| `retenidos[].descripcion` | texto | sí |  |
| `retenidos[].baseLegal` | texto | sí |  |
| `retenidos[].retenidaHasta` | fecha o null | no |  |
| `anonimizados` | lista de texto | sí |  |
| `hash` | texto | sí |  |

**Errores de negocio que lanza**

- `409` `solicitud-sin-resolver`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/solicitudes-habeas/3fa85f64-5717-4562-b3fc-2c963f66afa6/constancia" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "radicado": "texto",
  "tipo": "texto",
  "alcance": "texto",
  "destino": "texto",
  "recibidaEl": "2026-11-12",
  "resueltaEl": "2026-11-12",
  "dentroDelPlazo": true,
  "resultado": "texto",
  "suprimidos": [
    {
      "tipo": "texto",
      "referencia": "REF-0001",
      "descripcion": "Texto de ejemplo."
    }
  ],
  "retenidos": [
    {
      "tipo": "texto",
      "referencia": "REF-0001",
      "descripcion": "Texto de ejemplo.",
      "baseLegal": "texto",
      "retenidaHasta": "2026-11-12"
    }
  ],
  "anonimizados": [
    "texto"
  ],
  "hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
}
```

</details>
