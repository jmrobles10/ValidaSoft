# API · Vínculos

36 operaciones en 13 recursos.

## Contactos de vínculo

### `PUT /api/v1/vinculos/{id}/contactos`

**Actualizar contactos vínculo.** La empresa reemplaza los contactos del tercero con ella (`vinculos:escribir`). 422 `contactos-demasiados`, `contacto-nombre-requerido`, `contacto-demasiado-largo`, `correo-invalido`, `contacto-rol-invalido`, `contacto-repetido`; 409 `vinculo-anulado`.

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarContactosVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarContactosDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `contactos` | lista de ContactoDeEmpresaDto | sí |  |
| `contactos[].nombre` | texto | sí |  |
| `contactos[].cargo` | texto o null | no |  |
| `contactos[].correo` | texto | sí |  |
| `contactos[].telefono` | texto o null | no |  |
| `contactos[].rol` | texto | sí |  |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `empresaId` | uuid | sí |  |
| `terceroId` | uuid | sí |  |
| `tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `tipoVinculo.id` | uuid | sí |  |
| `tipoVinculo.codigo` | texto | sí |  |
| `tipoVinculo.nombre` | texto | sí |  |
| `flujoId` | uuid o null | no |  |
| `estado` | EstadoVinculo (texto) | sí | valores: Invitado · EnValidacion · Activo · PorVencer · Vencido · Bloqueado · Desvinculado · Anulado |
| `atributos` | objeto libre | sí |  |
| `correoTercero` | texto | sí |  |
| `invitacion` | EnlaceInvitacionDto o null | no |  |
| `invitacion.correoDestino` | texto | sí |  |
| `invitacion.expira` | fecha y hora | sí |  |
| `invitacion.envios` | entero | sí |  |
| `invitacionAceptadaEn` | fecha y hora o null | no |  |
| `requisitosCubribles` | entero o null | no |  |
| `bloqueo` | BloqueoDto o null | no |  |
| `bloqueo.motivo` | texto | sí |  |
| `bloqueo.desde` | fecha y hora | sí |  |
| `bloqueo.hasta` | fecha y hora o null | no |  |
| `referencias` | lista de ReferenciaExternaDto | sí |  |
| `referencias[].sistema` | texto | sí |  |
| `referencias[].id` | texto | sí |  |
| `origen` | OrigenVinculo (texto) | sí | valores: Invitacion · Autorregistro · Migracion · Api |
| `creadoEn` | fecha y hora | sí |  |
| `actualizadoEn` | fecha y hora | sí |  |
| `version` | entero | sí |  |
| `expedienteAbiertoId` | uuid o null | no |  |
| `aperturaDeExpedientePendiente` | booleano | no | por omisión false |
| `ultimoResultado` | texto o null | no |  |
| `contactos` | lista de ContactoDeEmpresaDto o null | no |  |
| `contactos[].nombre` | texto | sí |  |
| `contactos[].cargo` | texto o null | no |  |
| `contactos[].correo` | texto | sí |  |
| `contactos[].telefono` | texto o null | no |  |
| `contactos[].rol` | texto | sí |  |
| `vigenteHasta` | fecha o null | no |  |
| `enRevision` | booleano | no | por omisión false |
| `actualizacionNoAprobada` | ActualizacionNoAprobadaDto o null | no |  |
| `actualizacionNoAprobada.expedienteId` | uuid o null | no |  |
| `actualizacionNoAprobada.resultado` | texto | sí |  |
| `actualizacionNoAprobada.fecha` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/contactos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"contactos":[{"nombre":"Constructora Norte S.A.S.","cargo":"texto","correo":"contacto@empresa.co","telefono":"+57 601 555 1234","rol":"texto"}]}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

## Contactos públicos

El tercero edita sus contactos con cada empresa desde su ficha (sesión de tercero; solo sus vínculos, el resto 404).

### `PUT /public/v1/vinculos/{id}/contactos`

**Actualizar contactos del tercero.** El tercero reemplaza los contactos que tiene con la empresa de ese vínculo y recibe la lista resultante. Solo después de aceptar la invitación (409 `invitacion-no-aceptada`): hasta entonces la empresa no ve nada de él (B-1). Mismas validaciones que la empresa.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `actualizarContactosDelTercero`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarContactosDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `contactos` | lista de ContactoDeEmpresaDto | sí |  |
| `contactos[].nombre` | texto | sí |  |
| `contactos[].cargo` | texto o null | no |  |
| `contactos[].correo` | texto | sí |  |
| `contactos[].telefono` | texto o null | no |  |
| `contactos[].rol` | texto | sí |  |

**Respuestas**

- `200` OK — lista de ContactoDeEmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de ContactoDeEmpresaDto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].cargo` | texto o null | no |  |
| `[].correo` | texto | sí |  |
| `[].telefono` | texto o null | no |  |
| `[].rol` | texto | sí |  |

**Errores de negocio que lanza**

- `404` `vinculo-no-encontrado`
- `409` `invitacion-no-aceptada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/public/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/contactos" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"contactos":[{"nombre":"Constructora Norte S.A.S.","cargo":"texto","correo":"contacto@empresa.co","telefono":"+57 601 555 1234","rol":"texto"}]}'
```

Respuesta `200`:

```json
[
  {
    "nombre": "Constructora Norte S.A.S.",
    "cargo": "texto",
    "correo": "contacto@empresa.co",
    "telefono": "+57 601 555 1234",
    "rol": "texto"
  }
]
```

</details>

## Cuenta designada

La empresa ve y decide la cuenta designada de un vínculo (CB-2).

### `GET /api/v1/vinculos/{id}/cuenta-designada`

**Obtener cuenta designada.**

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerCuentaDesignada`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CuentaDesignadaDelVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `actual` | CuentaDesignadaDto o null | no |  |
| `actual.cuentaId` | uuid | sí |  |
| `actual.banco` | texto | sí |  |
| `actual.tipoCuenta` | texto | sí |  |
| `actual.numeroEnmascarado` | texto | sí |  |
| `actual.numero` | texto o null | no |  |
| `actual.titular` | texto | sí |  |
| `actual.identificacionTitular` | texto | sí |  |
| `actual.moneda` | texto | sí |  |
| `actual.estado` | texto | sí |  |
| `actual.propuestaEn` | fecha y hora | sí |  |
| `actual.aprobaciones` | lista de AprobacionDeCuentaDto | sí |  |
| `actual.aprobaciones[].usuarioId` | texto | sí |  |
| `actual.aprobaciones[].nombre` | texto o null | no |  |
| `actual.aprobaciones[].fecha` | fecha y hora | sí |  |
| `actual.aprobadaEn` | fecha y hora o null | no |  |
| `actual.motivoRechazo` | texto o null | no |  |
| `actual.bancoCodigo` | texto o null | no |  |
| `actual.bancoSwift` | texto o null | no |  |
| `actual.contactoPagos` | ContactoDePagosDto o null | no |  |
| `actual.contactoPagos.nombre` | texto | sí |  |
| `actual.contactoPagos.correo` | texto | sí |  |
| `actual.contactoPagos.telefono` | texto o null | no |  |
| `propuesta` | CuentaDesignadaDto o null | no | mismos campos que `actual` |
| `rechazada` | CuentaDesignadaDto o null | no | mismos campos que `actual` |
| `aprobadoresRequeridos` | entero | sí |  |
| `puedesAprobar` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/cuenta-designada" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "actual": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "propuesta": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "rechazada": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "aprobadoresRequeridos": 1,
  "puedesAprobar": true
}
```

</details>

### `POST /api/v1/vinculos/{id}/cuenta-designada/aprobar`

**Aprobar cuenta designada.** Aprueba la cuenta propuesta (una persona con `cuentas:aprobar`); con la segunda persona queda designada y, si el expediente abierto del vínculo la exigía y esperaba por ella (CB-4), se aprueba en la misma transacción.

**Permiso cuentas:aprobar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `aprobarCuentaDesignada`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CuentaDesignadaDelVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CuentaDesignadaDelVinculoDto` que se detallan en `GET /api/v1/vinculos/{id}/cuenta-designada`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/cuenta-designada/aprobar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "actual": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "propuesta": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "rechazada": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "aprobadoresRequeridos": 1,
  "puedesAprobar": true
}
```

</details>

### `POST /api/v1/vinculos/{id}/cuenta-designada/rechazar`

**Rechazar cuenta designada.**

**Permiso cuentas:aprobar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `rechazarCuentaDesignada`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, MotivoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `motivo` | texto | sí |  |

**Respuestas**

- `200` OK — CuentaDesignadaDelVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CuentaDesignadaDelVinculoDto` que se detallan en `GET /api/v1/vinculos/{id}/cuenta-designada`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/cuenta-designada/rechazar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"motivo":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "actual": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "propuesta": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "rechazada": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "aprobadoresRequeridos": 1,
  "puedesAprobar": true
}
```

</details>

## Cuentas públicas

### `GET /public/v1/boveda/cuentas-bancarias`

**Listar cuentas bancarias.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `listarCuentasBancarias`**

**Respuestas**

- `200` OK — lista de CuentaBancariaPublicaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de CuentaBancariaPublicaDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].banco` | texto | sí |  |
| `[].tipoCuenta` | texto | sí |  |
| `[].numeroEnmascarado` | texto | sí |  |
| `[].titular` | texto | sí |  |
| `[].identificacionTitular` | texto | sí |  |
| `[].moneda` | texto | sí |  |
| `[].estado` | texto | sí |  |
| `[].registradaEn` | fecha y hora | sí |  |
| `[].designadaEn` | lista de DesignacionDeCuentaDto | sí |  |
| `[].designadaEn[].vinculoId` | uuid | sí |  |
| `[].designadaEn[].empresaId` | uuid | sí |  |
| `[].designadaEn[].empresa` | texto | sí |  |
| `[].designadaEn[].estado` | texto | sí |  |
| `[].designadaEn[].motivoRechazo` | texto o null | no |  |
| `[].bancoCodigo` | texto o null | no |  |
| `[].bancoSwift` | texto o null | no |  |
| `[].contactoPagos` | ContactoDePagosDto o null | no |  |
| `[].contactoPagos.nombre` | texto | sí |  |
| `[].contactoPagos.correo` | texto | sí |  |
| `[].contactoPagos.telefono` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/boveda/cuentas-bancarias" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "registradaEn": "2026-11-12T15:00:00Z",
    "designadaEn": [
      {
        "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "empresa": "texto",
        "estado": "texto",
        "motivoRechazo": "Texto de ejemplo."
      }
    ],
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  }
]
```

</details>

### `POST /public/v1/boveda/cuentas-bancarias`

**Registrar cuenta bancaria.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `registrarCuentaBancaria`**

**Cuerpo de la petición** (`application/json`, RegistrarCuentaBancariaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `banco` | texto o null | no |  |
| `tipoCuenta` | texto o null | no |  |
| `numero` | texto o null | no |  |
| `titular` | texto o null | no |  |
| `identificacionTitular` | texto o null | no |  |
| `moneda` | texto o null | no |  |
| `pais` | texto o null | no |  |
| `bancoCodigo` | texto o null | no |  |
| `bancoSwift` | texto o null | no |  |
| `contactoPagos` | ContactoDePagosDto o null | no |  |
| `contactoPagos.nombre` | texto | sí |  |
| `contactoPagos.correo` | texto | sí |  |
| `contactoPagos.telefono` | texto o null | no |  |

**Respuestas**

- `201` Creado — CuentaBancariaPublicaDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `banco` | texto | sí |  |
| `tipoCuenta` | texto | sí |  |
| `numeroEnmascarado` | texto | sí |  |
| `titular` | texto | sí |  |
| `identificacionTitular` | texto | sí |  |
| `moneda` | texto | sí |  |
| `estado` | texto | sí |  |
| `registradaEn` | fecha y hora | sí |  |
| `designadaEn` | lista de DesignacionDeCuentaDto | sí |  |
| `designadaEn[].vinculoId` | uuid | sí |  |
| `designadaEn[].empresaId` | uuid | sí |  |
| `designadaEn[].empresa` | texto | sí |  |
| `designadaEn[].estado` | texto | sí |  |
| `designadaEn[].motivoRechazo` | texto o null | no |  |
| `bancoCodigo` | texto o null | no |  |
| `bancoSwift` | texto o null | no |  |
| `contactoPagos` | ContactoDePagosDto o null | no |  |
| `contactoPagos.nombre` | texto | sí |  |
| `contactoPagos.correo` | texto | sí |  |
| `contactoPagos.telefono` | texto o null | no |  |

**Errores de negocio que lanza**

- `422` `banco-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/boveda/cuentas-bancarias" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"banco":"texto","tipoCuenta":"texto","numero":"900123456","titular":"texto","identificacionTitular":"900123456","moneda":"texto","pais":"CO","bancoCodigo":"codigo-de-ejemplo","bancoSwift":"texto","contactoPagos":{"nombre":"Constructora Norte S.A.S.","correo":"contacto@empresa.co","telefono":"+57 601 555 1234"}}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "banco": "texto",
  "tipoCuenta": "texto",
  "numeroEnmascarado": "900123456",
  "titular": "texto",
  "identificacionTitular": "900123456",
  "moneda": "texto",
  "estado": "texto",
  "registradaEn": "2026-11-12T15:00:00Z",
  "designadaEn": [
    {
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresa": "texto",
      "estado": "texto",
      "motivoRechazo": "Texto de ejemplo."
    }
  ],
  "bancoCodigo": "codigo-de-ejemplo",
  "bancoSwift": "texto",
  "contactoPagos": {
    "nombre": "Constructora Norte S.A.S.",
    "correo": "contacto@empresa.co",
    "telefono": "+57 601 555 1234"
  }
}
```

</details>

### `POST /public/v1/boveda/cuentas-bancarias/{id}/retirar`

**Retirar cuenta bancaria.** Retira una cuenta. Si está designada o propuesta con alguna empresa, 409 `cuenta-en-uso`: primero designa otra.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `retirarCuentaBancaria`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CuentaBancariaPublicaDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CuentaBancariaPublicaDto` que se detallan en `POST /public/v1/boveda/cuentas-bancarias`.

**Errores de negocio que lanza**

- `404` `cuenta-no-encontrada`
- `409` `cuenta-en-uso`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/boveda/cuentas-bancarias/3fa85f64-5717-4562-b3fc-2c963f66afa6/retirar" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "banco": "texto",
  "tipoCuenta": "texto",
  "numeroEnmascarado": "900123456",
  "titular": "texto",
  "identificacionTitular": "900123456",
  "moneda": "texto",
  "estado": "texto",
  "registradaEn": "2026-11-12T15:00:00Z",
  "designadaEn": [
    {
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresa": "texto",
      "estado": "texto",
      "motivoRechazo": "Texto de ejemplo."
    }
  ],
  "bancoCodigo": "codigo-de-ejemplo",
  "bancoSwift": "texto",
  "contactoPagos": {
    "nombre": "Constructora Norte S.A.S.",
    "correo": "contacto@empresa.co",
    "telefono": "+57 601 555 1234"
  }
}
```

</details>

### `POST /public/v1/vinculos/{id}/cuenta-designada`

**Designar cuenta.** El tercero designa una cuenta suya, activa, para cobrarle a la empresa de este vínculo. Exige tener la certificación bancaria en la bóveda (422 `certificacion-requerida`). Queda propuesta hasta que la empresa la apruebe.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `designarCuenta`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, DesignarCuentaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `cuentaId` | uuid | sí |  |

**Respuestas**

- `200` OK — CuentaBancariaPublicaDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CuentaBancariaPublicaDto` que se detallan en `POST /public/v1/boveda/cuentas-bancarias`.

**Errores de negocio que lanza**

- `404` `cuenta-no-encontrada`
- `404` `vinculo-no-encontrado`
- `409` `cuenta-retirada`
- `422` `certificacion-requerida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/cuenta-designada" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"cuentaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "banco": "texto",
  "tipoCuenta": "texto",
  "numeroEnmascarado": "900123456",
  "titular": "texto",
  "identificacionTitular": "900123456",
  "moneda": "texto",
  "estado": "texto",
  "registradaEn": "2026-11-12T15:00:00Z",
  "designadaEn": [
    {
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresa": "texto",
      "estado": "texto",
      "motivoRechazo": "Texto de ejemplo."
    }
  ],
  "bancoCodigo": "codigo-de-ejemplo",
  "bancoSwift": "texto",
  "contactoPagos": {
    "nombre": "Constructora Norte S.A.S.",
    "correo": "contacto@empresa.co",
    "telefono": "+57 601 555 1234"
  }
}
```

</details>

## Ficha aceptada del vínculo

La ficha aceptada de un vínculo, para la web de la empresa (FD-3): por cada sección que la empresa aceptó, la versión, cuándo y en qué expediente, los datos de esa versión (inmutables) y el soporte con que se aceptó; y, **aparte**, lo que el tercero cambió después (la versión vigente si es otra, campo por campo contra lo aceptado, con su soporte y la revalidación que la revisa). Es lo mismo que `GET /api/v1/vinculos/{id}/ficha` (FM-35) pero pensado para leerse: datos tal cual los guardó el tercero, no normalizados para un ERP. Lo que la empresa no aceptó no aparece (FM-3).

### `GET /api/v1/vinculos/{id}/ficha-aceptada`

**Obtener ficha aceptada del vínculo.**

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerFichaAceptadaDelVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — FichaAceptadaDelVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `vinculoId` | uuid | sí |  |
| `terceroId` | uuid | sí |  |
| `tipoPersona` | texto | sí |  |
| `secciones` | lista de SeccionAceptadaVistaDto | sí |  |
| `secciones[].seccion` | texto | sí |  |
| `secciones[].nombre` | texto | sí |  |
| `secciones[].periodo` | entero o null | no |  |
| `secciones[].aceptada` | LoAceptadoDeSeccionDto | sí |  |
| `secciones[].aceptada.version` | entero | sí |  |
| `secciones[].aceptada.aceptadaEn` | fecha y hora | sí |  |
| `secciones[].aceptada.expedienteId` | uuid o null | no |  |
| `secciones[].aceptada.folio` | texto o null | no |  |
| `secciones[].aceptada.origen` | texto o null | no |  |
| `secciones[].aceptada.datos` | JsonElement | sí |  |
| `secciones[].aceptada.soportes` | lista de SoporteSeccionDto | sí |  |
| `secciones[].aceptada.soportes[].documentoId` | uuid | sí |  |
| `secciones[].aceptada.soportes[].version` | entero | sí |  |
| `secciones[].aceptada.soportes[].tipoDocumento` | texto o null | no |  |
| `secciones[].aceptada.soportes[].retirado` | booleano | no | por omisión false |
| `secciones[].aceptada.soportes[].nombre` | texto o null | no |  |
| `secciones[].cambioPosterior` | CambioPosteriorDelTerceroDto o null | no |  |
| `secciones[].cambioPosterior.version` | entero | sí |  |
| `secciones[].cambioPosterior.fecha` | fecha y hora | sí |  |
| `secciones[].cambioPosterior.cambios` | lista de CambioDeCampoDto | sí |  |
| `secciones[].cambioPosterior.cambios[].campo` | texto | sí |  |
| `secciones[].cambioPosterior.cambios[].etiqueta` | texto | sí |  |
| `secciones[].cambioPosterior.cambios[].anterior` | texto o null | no |  |
| `secciones[].cambioPosterior.cambios[].nuevo` | texto o null | no |  |
| `secciones[].cambioPosterior.cambios[].tipoCambio` | TipoCambio (texto) | sí | valores: Agregado · Quitado · Modificado |
| `secciones[].cambioPosterior.soportes` | lista de SoporteSeccionDto | sí | mismos campos que `secciones[].aceptada.soportes[]` |
| `secciones[].cambioPosterior.sinSoporte` | SinSoporteDto o null | no |  |
| `secciones[].cambioPosterior.sinSoporte.critico` | booleano | sí |  |
| `secciones[].cambioPosterior.sinSoporte.cambios` | lista de CambioDeCampoDto | sí | mismos campos que `secciones[].cambioPosterior.cambios[]` |
| `secciones[].cambioPosterior.revalidacion` | RevalidacionDeSeccionDto o null | no |  |
| `secciones[].cambioPosterior.revalidacion.expedienteId` | uuid | sí |  |
| `secciones[].cambioPosterior.revalidacion.folio` | texto | sí |  |
| `secciones[].cambioPosterior.revalidacion.estado` | EstadoRequisito (texto) | sí | valores: Pendiente · Cargado · EnRevision · Observado · Aprobado · Eximido |
| `secciones[].cambioPosterior.revalidacion.version` | entero o null | no |  |
| `secciones[].cambioPosterior.revalidacion.plazoHasta` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/ficha-aceptada" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoPersona": "texto",
  "secciones": [
    {
      "seccion": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "periodo": 1,
      "aceptada": {
        "version": 1,
        "aceptadaEn": "2026-11-12T15:00:00Z",
        "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "folio": "texto",
        "origen": "texto",
        "datos": {},
        "soportes": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "tipoDocumento": "texto",
            "retirado": false,
            "nombre": "Constructora Norte S.A.S."
          }
        ]
      },
      "cambioPosterior": {
        "version": 1,
        "fecha": "2026-11-12T15:00:00Z",
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "soportes": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "tipoDocumento": "texto",
            "retirado": false,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "sinSoporte": {
          "critico": true,
          "cambios": [
            {
              "campo": null,
              "etiqueta": null,
              "anterior": null,
              "nuevo": null,
              "tipoCambio": null
            }
          ]
        },
        "revalidacion": {
          "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "folio": "texto",
          "estado": "Pendiente",
          "version": 1,
          "plazoHasta": "2026-11-12T15:00:00Z"
        }
      }
    }
  ]
}
```

</details>

## Ficha del vínculo

La ficha aceptada por la empresa en un vínculo (F3, FM-35), para que el SRM (o el Portal) cree o actualice el proveedor en el ERP: por cada sección que la empresa aceptó (`SeccionAceptada`), la versión, la fecha y el contenido normalizado (el mismo del evento `seccion.aceptada`), y si el tercero tiene una versión vigente más nueva. Además el representante legal que va a AyF y la cuenta designada aprobada. Lo que la empresa no aceptó no aparece (FM-3).

### `GET /api/v1/vinculos/{id}/ficha`

**Obtener ficha del vínculo.**

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerFichaDelVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — FichaDelVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `vinculoId` | uuid | sí |  |
| `empresaId` | uuid | sí |  |
| `terceroId` | uuid | sí |  |
| `referencias` | lista de ReferenciaExternaDto | sí |  |
| `referencias[].sistema` | texto | sí |  |
| `referencias[].id` | texto | sí |  |
| `tipoPersona` | texto | sí |  |
| `secciones` | lista de SeccionAceptadaDto | sí |  |
| `secciones[].seccion` | texto | sí |  |
| `secciones[].nombre` | texto | sí |  |
| `secciones[].periodo` | entero o null | no |  |
| `secciones[].version` | entero | sí |  |
| `secciones[].aceptadaEn` | fecha y hora | sí |  |
| `secciones[].expedienteId` | uuid | sí |  |
| `secciones[].folio` | texto | sí |  |
| `secciones[].huella` | texto | sí |  |
| `secciones[].contenido` | ContenidoDeSeccionDto | sí |  |
| `secciones[].contenido.general` | ContenidoGeneralDto o null | no |  |
| `secciones[].contenido.general.tipoPersona` | texto o null | no |  |
| `secciones[].contenido.general.tipoIdentificacion` | texto o null | no |  |
| `secciones[].contenido.general.numeroIdentificacion` | texto o null | no |  |
| `secciones[].contenido.general.dv` | texto o null | no |  |
| `secciones[].contenido.general.nombre` | texto o null | no |  |
| `secciones[].contenido.general.razonSocial` | texto o null | no |  |
| `secciones[].contenido.general.nombres` | texto o null | no |  |
| `secciones[].contenido.general.apellidos` | texto o null | no |  |
| `secciones[].contenido.general.direccion` | texto o null | no |  |
| `secciones[].contenido.general.direccionEstructurada` | DireccionEstructuradaDto o null | no |  |
| `secciones[].contenido.general.direccionEstructurada.tipoVia` | texto o null | no |  |
| `secciones[].contenido.general.direccionEstructurada.numeroVia` | texto o null | no |  |
| `secciones[].contenido.general.direccionEstructurada.letraVia` | texto o null | no |  |
| `secciones[].contenido.general.direccionEstructurada.bis` | booleano o null | no |  |
| `secciones[].contenido.general.direccionEstructurada.cuadrante` | texto o null | no |  |
| `secciones[].contenido.general.direccionEstructurada.numeroCruce` | texto o null | no |  |
| `secciones[].contenido.general.direccionEstructurada.letraCruce` | texto o null | no |  |
| `secciones[].contenido.general.direccionEstructurada.placa` | texto o null | no |  |
| `secciones[].contenido.general.direccionEstructurada.complemento` | texto o null | no |  |
| `secciones[].contenido.general.ciudad` | CiudadErpDto o null | no |  |
| `secciones[].contenido.general.ciudad.codigo` | texto | sí |  |
| `secciones[].contenido.general.ciudad.nombre` | texto o null | no |  |
| `secciones[].contenido.general.ciudad.codigoDepartamento` | texto | sí |  |
| `secciones[].contenido.general.ciudad.departamento` | texto o null | no |  |
| `secciones[].contenido.general.ciudad.pais` | texto o null | no |  |
| `secciones[].contenido.general.pais` | texto o null | no |  |
| `secciones[].contenido.general.telefono` | texto o null | no |  |
| `secciones[].contenido.general.correo` | texto o null | no |  |
| `secciones[].contenido.general.ciiuPrincipal` | CodigoNombreDto o null | no |  |
| `secciones[].contenido.general.ciiuPrincipal.codigo` | texto | sí |  |
| `secciones[].contenido.general.ciiuPrincipal.nombre` | texto o null | no |  |
| `secciones[].contenido.general.ciiuSecundarias` | lista de CodigoNombreDto | sí | mismos campos que `secciones[].contenido.general.ciiuPrincipal` |
| `secciones[].contenido.general.paginaWeb` | texto o null | no |  |
| `secciones[].contenido.general.certificadoIso` | booleano o null | no |  |
| `secciones[].contenido.general.anioInicio` | entero o null | no |  |
| `secciones[].contenido.general.representanteLegal` | RepresentanteLegalErpDto o null | no |  |
| `secciones[].contenido.general.representanteLegal.origen` | texto | sí |  |
| `secciones[].contenido.general.representanteLegal.indice` | entero o null | no |  |
| `secciones[].contenido.general.representanteLegal.nombre` | texto | sí |  |
| `secciones[].contenido.general.representanteLegal.nombres` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.apellidos` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.tipoIdentificacion` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.numero` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.dv` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.tipoRepresentacion` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.cargo` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.fechaExpedicion` | fecha o null | no |  |
| `secciones[].contenido.general.representanteLegal.correo` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.telefono` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.lugarExpedicion` | texto o null | no |  |
| `secciones[].contenido.general.representanteLegal.pep` | booleano o null | no |  |
| `secciones[].contenido.general.representanteLegal.vinculoPep` | booleano o null | no |  |
| `secciones[].contenido.general.pep` | booleano o null | no |  |
| `secciones[].contenido.general.vinculoPep` | booleano o null | no |  |
| `secciones[].contenido.general.fechaExpedicion` | fecha o null | no |  |
| `secciones[].contenido.general.lugarExpedicion` | texto o null | no |  |
| `secciones[].contenido.general.declaracionPep` | DeclaracionPepErpDto o null | no |  |
| `secciones[].contenido.general.declaracionPep.fechaFirma` | fecha o null | no |  |
| `secciones[].contenido.general.declaracionPep.firmaEnCalidad` | texto o null | no |  |
| `secciones[].contenido.general.declaracionPep.firmaHuella` | texto o null | no |  |
| `secciones[].contenido.tributaria` | ContenidoTributarioDto o null | no |  |
| `secciones[].contenido.tributaria.responsabilidades` | lista de CodigoNombreDto | sí | mismos campos que `secciones[].contenido.general.ciiuPrincipal` |
| `secciones[].contenido.tributaria.calidades` | CalidadesTributariasDto | sí |  |
| `secciones[].contenido.tributaria.calidades.responsableIva` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.autorretenedor` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.declaranteRenta` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.granContribuyente` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.autorretenedorIca` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.regimenSimple` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.granContribuyenteBogota` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.regimenEspecial` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.autorretenedorRft` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.descuentoSenaFic` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.profesionalIndependiente` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.exentoRetencion` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.agenteRetenedorIva` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.obligadoAFacturar` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidades.exentoReteCree` | booleano o null | no |  |
| `secciones[].contenido.tributaria.calidadesSinDerivar` | lista de CalidadSinDerivarDto | sí |  |
| `secciones[].contenido.tributaria.calidadesSinDerivar[].calidad` | texto | sí |  |
| `secciones[].contenido.tributaria.calidadesSinDerivar[].nota` | texto | sí |  |
| `secciones[].contenido.tributaria.resolucionGranContribuyente` | ResolucionDto o null | no |  |
| `secciones[].contenido.tributaria.resolucionGranContribuyente.numero` | texto o null | no |  |
| `secciones[].contenido.tributaria.resolucionGranContribuyente.fecha` | fecha o null | no |  |
| `secciones[].contenido.tributaria.resolucionAutorretenedor` | ResolucionDto o null | no | mismos campos que `secciones[].contenido.tributaria.resolucionGranContribuyente` |
| `secciones[].contenido.tributaria.responsableIca` | booleano o null | no |  |
| `secciones[].contenido.tributaria.autorretenedorIca` | booleano o null | no |  |
| `secciones[].contenido.tributaria.ica` | lista de IcaErpDto | sí |  |
| `secciones[].contenido.tributaria.ica[].municipio` | CiudadErpDto o null | no | mismos campos que `secciones[].contenido.general.ciudad` |
| `secciones[].contenido.tributaria.ica[].ciiu` | CodigoNombreDto o null | no | mismos campos que `secciones[].contenido.general.ciiuPrincipal` |
| `secciones[].contenido.tributaria.ica[].tarifaPorMil` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.legal` | ContenidoLegalDto o null | no |  |
| `secciones[].contenido.legal.representanteLegal` | RepresentanteLegalErpDto o null | no | mismos campos que `secciones[].contenido.general.representanteLegal` |
| `secciones[].contenido.legal.personas` | lista de PersonaRelacionadaDto | sí |  |
| `secciones[].contenido.legal.personas[].rol` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].tipoRepresentacion` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].tipoIdentificacion` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].numero` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].dv` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].nombres` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].apellidos` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].cargo` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].porcentaje` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.legal.personas[].pep` | booleano o null | no |  |
| `secciones[].contenido.legal.personas[].vinculoPep` | booleano o null | no |  |
| `secciones[].contenido.legal.personas[].fechaExpedicion` | fecha o null | no |  |
| `secciones[].contenido.legal.personas[].fechaNacimiento` | fecha o null | no |  |
| `secciones[].contenido.legal.personas[].nacionalidad` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].residencia` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].correo` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].telefono` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].lugarExpedicion` | texto o null | no |  |
| `secciones[].contenido.legal.personas[].declaracionPep` | DeclaracionPepErpDto o null | no | mismos campos que `secciones[].contenido.general.declaracionPep` |
| `secciones[].contenido.legal.revisorFiscalFirma` | FirmaRevisoraDto o null | no |  |
| `secciones[].contenido.legal.revisorFiscalFirma.nit` | texto o null | no |  |
| `secciones[].contenido.legal.revisorFiscalFirma.razonSocial` | texto o null | no |  |
| `secciones[].contenido.financiera` | ContenidoFinancieroDto o null | no |  |
| `secciones[].contenido.financiera.periodo` | entero o null | no |  |
| `secciones[].contenido.financiera.fechaCorte` | fecha o null | no |  |
| `secciones[].contenido.financiera.activos` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.financiera.pasivos` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.financiera.patrimonio` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.financiera.ingresosBrutos` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.financiera.ingresosNetos` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.financiera.gastosOperacionales` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.financiera.gastosNoOperacionales` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.financiera.otrosIngresos` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `secciones[].contenido.financiera.monedaExtranjera` | MonedaExtranjeraDto o null | no |  |
| `secciones[].contenido.financiera.monedaExtranjera.realiza` | booleano o null | no |  |
| `secciones[].contenido.financiera.monedaExtranjera.tipos` | lista de texto | sí |  |
| `secciones[].contenido.financiera.monedaExtranjera.paises` | lista de texto | sí |  |
| `secciones[].contenido.sst` | ContenidoSstDto o null | no |  |
| `secciones[].contenido.sst.programaSaludOcupacional` | booleano o null | no |  |
| `secciones[].contenido.sst.identificaFactoresDeRiesgo` | booleano o null | no |  |
| `secciones[].contenido.sst.comiteParitario` | booleano o null | no |  |
| `secciones[].contenido.sst.programaHigieneYSeguridad` | booleano o null | no |  |
| `secciones[].contenido.sst.gestionAmbiental` | booleano o null | no |  |
| `secciones[].contenido.sst.reglamentoInterno` | booleano o null | no |  |
| `secciones[].contenido.sst.politicaHseq` | booleano o null | no |  |
| `secciones[].contenido.sst.licenciasYPermisos` | booleano o null | no |  |
| `secciones[].contenido.especialidades` | ContenidoEspecialidadesDto o null | no |  |
| `secciones[].contenido.especialidades.especialidades` | lista de CodigoNombreDto | sí | mismos campos que `secciones[].contenido.general.ciiuPrincipal` |
| `secciones[].versionVigente` | entero o null | no |  |
| `secciones[].versionNuevaPendiente` | booleano | sí |  |
| `secciones[].origen` | OrigenDeAceptacion (texto) | no | valores: Expediente · Importacion |
| `representanteLegal` | RepresentanteLegalErpDto o null | no | mismos campos que `secciones[].contenido.general.representanteLegal` |
| `cuentaDesignada` | CuentaDesignadaDto o null | no |  |
| `cuentaDesignada.cuentaId` | uuid | sí |  |
| `cuentaDesignada.banco` | texto | sí |  |
| `cuentaDesignada.tipoCuenta` | texto | sí |  |
| `cuentaDesignada.numeroEnmascarado` | texto | sí |  |
| `cuentaDesignada.numero` | texto o null | no |  |
| `cuentaDesignada.titular` | texto | sí |  |
| `cuentaDesignada.identificacionTitular` | texto | sí |  |
| `cuentaDesignada.moneda` | texto | sí |  |
| `cuentaDesignada.estado` | texto | sí |  |
| `cuentaDesignada.propuestaEn` | fecha y hora | sí |  |
| `cuentaDesignada.aprobaciones` | lista de AprobacionDeCuentaDto | sí |  |
| `cuentaDesignada.aprobaciones[].usuarioId` | texto | sí |  |
| `cuentaDesignada.aprobaciones[].nombre` | texto o null | no |  |
| `cuentaDesignada.aprobaciones[].fecha` | fecha y hora | sí |  |
| `cuentaDesignada.aprobadaEn` | fecha y hora o null | no |  |
| `cuentaDesignada.motivoRechazo` | texto o null | no |  |
| `cuentaDesignada.bancoCodigo` | texto o null | no |  |
| `cuentaDesignada.bancoSwift` | texto o null | no |  |
| `cuentaDesignada.contactoPagos` | ContactoDePagosDto o null | no |  |
| `cuentaDesignada.contactoPagos.nombre` | texto | sí |  |
| `cuentaDesignada.contactoPagos.correo` | texto | sí |  |
| `cuentaDesignada.contactoPagos.telefono` | texto o null | no |  |
| `contactos` | lista de ContactoDeEmpresaDto o null | no |  |
| `contactos[].nombre` | texto | sí |  |
| `contactos[].cargo` | texto o null | no |  |
| `contactos[].correo` | texto | sí |  |
| `contactos[].telefono` | texto o null | no |  |
| `contactos[].rol` | texto | sí |  |
| `referenciasDelTercero` | lista de ReferenciaExternaDto o null | no | mismos campos que `referencias[]` |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/ficha" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "tipoPersona": "texto",
  "secciones": [
    {
      "seccion": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "periodo": 1,
      "version": 1,
      "aceptadaEn": "2026-11-12T15:00:00Z",
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "huella": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "contenido": {
        "general": {
          "tipoPersona": "texto",
          "tipoIdentificacion": "900123456",
          "numeroIdentificacion": "900123456",
          "dv": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "razonSocial": "Constructora Norte S.A.S.",
          "nombres": "Marta Lucía",
          "apellidos": "Ríos Duarte",
          "direccion": "Calle 45 # 12-08",
          "direccionEstructurada": {
            "tipoVia": "texto",
            "numeroVia": "900123456",
            "letraVia": "texto",
            "bis": true,
            "cuadrante": "texto",
            "numeroCruce": "900123456",
            "letraCruce": "texto",
            "placa": "texto",
            "complemento": "texto"
          },
          "ciudad": {
            "codigo": "CODIGO_EJEMPLO",
            "nombre": "Constructora Norte S.A.S.",
            "codigoDepartamento": "codigo-de-ejemplo",
            "departamento": "texto",
            "pais": "CO"
          },
          "pais": "CO",
          "telefono": "+57 601 555 1234",
          "correo": "contacto@empresa.co",
          "ciiuPrincipal": {
            "codigo": "CODIGO_EJEMPLO",
            "nombre": "Constructora Norte S.A.S."
          },
          "ciiuSecundarias": [
            {
              "codigo": null,
              "nombre": null
            }
          ],
          "paginaWeb": "texto",
          "certificadoIso": true,
          "anioInicio": 1,
          "representanteLegal": {
            "origen": "texto",
            "indice": 1,
            "nombre": "Constructora Norte S.A.S.",
            "nombres": "Marta Lucía",
            "apellidos": "Ríos Duarte",
            "tipoIdentificacion": "900123456",
            "numero": "900123456",
            "dv": "texto",
            "tipoRepresentacion": "texto",
            "cargo": "texto",
            "fechaExpedicion": "2026-11-12",
            "correo": "contacto@empresa.co",
            "telefono": "+57 601 555 1234",
            "lugarExpedicion": "texto",
            "pep": true,
            "vinculoPep": true
          },
          "pep": true,
          "vinculoPep": true,
          "fechaExpedicion": "2026-11-12",
          "lugarExpedicion": "texto",
          "declaracionPep": {
            "fechaFirma": "2026-11-12",
            "firmaEnCalidad": "texto",
            "firmaHuella": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
          }
        },
        "tributaria": {
          "responsabilidades": [
            {
              "codigo": null,
              "nombre": null
            }
          ],
          "calidades": {
            "responsableIva": true,
            "autorretenedor": true,
            "declaranteRenta": true,
            "granContribuyente": true,
            "autorretenedorIca": true,
            "regimenSimple": true,
            "granContribuyenteBogota": true,
            "regimenEspecial": true,
            "autorretenedorRft": true,
            "descuentoSenaFic": true,
            "profesionalIndependiente": true,
            "exentoRetencion": true,
            "agenteRetenedorIva": true,
            "obligadoAFacturar": true,
            "exentoReteCree": true
          },
          "calidadesSinDerivar": [
            {
              "calidad": null,
              "nota": null
            }
          ],
          "resolucionGranContribuyente": {
            "numero": "900123456",
            "fecha": "2026-11-12"
          },
          "resolucionAutorretenedor": {
            "numero": "900123456",
            "fecha": "2026-11-12"
          },
          "responsableIca": true,
          "autorretenedorIca": true,
          "ica": [
            {
              "municipio": null,
              "ciiu": null,
              "tarifaPorMil": null
            }
          ]
        },
        "legal": {
          "representanteLegal": {
            "origen": "texto",
            "indice": 1,
            "nombre": "Constructora Norte S.A.S.",
            "nombres": "Marta Lucía",
            "apellidos": "Ríos Duarte",
            "tipoIdentificacion": "900123456",
            "numero": "900123456",
            "dv": "texto",
            "tipoRepresentacion": "texto",
            "cargo": "texto",
            "fechaExpedicion": "2026-11-12",
            "correo": "contacto@empresa.co",
            "telefono": "+57 601 555 1234",
            "lugarExpedicion": "texto",
            "pep": true,
            "vinculoPep": true
          },
          "personas": [
            {
              "rol": null,
              "tipoRepresentacion": null,
              "tipoIdentificacion": null,
              "numero": null,
              "dv": null,
              "nombres": null,
              "apellidos": null,
              "cargo": null,
              "porcentaje": null,
              "pep": null,
              "vinculoPep": null,
              "fechaExpedicion": null,
              "fechaNacimiento": null,
              "nacionalidad": null,
              "residencia": null,
              "correo": null,
              "telefono": null,
              "lugarExpedicion": null,
              "declaracionPep": null
            }
          ],
          "revisorFiscalFirma": {
            "nit": "900123456",
            "razonSocial": "Constructora Norte S.A.S."
          }
        },
        "financiera": {
          "periodo": 1,
          "fechaCorte": "2026-11-12",
          "activos": 1,
          "pasivos": 1,
          "patrimonio": 1,
          "ingresosBrutos": 1,
          "ingresosNetos": 1,
          "gastosOperacionales": 1,
          "gastosNoOperacionales": 1,
          "otrosIngresos": 1,
          "monedaExtranjera": {
            "realiza": true,
            "tipos": [
              null
            ],
            "paises": [
              null
            ]
          }
        },
        "sst": {
          "programaSaludOcupacional": true,
          "identificaFactoresDeRiesgo": true,
          "comiteParitario": true,
          "programaHigieneYSeguridad": true,
          "gestionAmbiental": true,
          "reglamentoInterno": true,
          "politicaHseq": true,
          "licenciasYPermisos": true
        },
        "especialidades": {
          "especialidades": [
            {
              "codigo": null,
              "nombre": null
            }
          ]
        }
      },
      "versionVigente": 1,
      "versionNuevaPendiente": true,
      "origen": "Expediente"
    }
  ],
  "representanteLegal": {
    "origen": "texto",
    "indice": 1,
    "nombre": "Constructora Norte S.A.S.",
    "nombres": "Marta Lucía",
    "apellidos": "Ríos Duarte",
    "tipoIdentificacion": "900123456",
    "numero": "900123456",
    "dv": "texto",
    "tipoRepresentacion": "texto",
    "cargo": "texto",
    "fechaExpedicion": "2026-11-12",
    "correo": "contacto@empresa.co",
    "telefono": "+57 601 555 1234",
    "lugarExpedicion": "texto",
    "pep": true,
    "vinculoPep": true
  },
  "cuentaDesignada": {
    "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "banco": "texto",
    "tipoCuenta": "texto",
    "numeroEnmascarado": "900123456",
    "numero": "900123456",
    "titular": "texto",
    "identificacionTitular": "900123456",
    "moneda": "texto",
    "estado": "texto",
    "propuestaEn": "2026-11-12T15:00:00Z",
    "aprobaciones": [
      {
        "usuarioId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    ],
    "aprobadaEn": "2026-11-12T15:00:00Z",
    "motivoRechazo": "Texto de ejemplo.",
    "bancoCodigo": "codigo-de-ejemplo",
    "bancoSwift": "texto",
    "contactoPagos": {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234"
    }
  },
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "referenciasDelTercero": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ]
}
```

</details>

## Ficha tercero

Ficha 360 del tercero para la empresa activa (docs/03 §14 `Tercero360`, docs/06 `/terceros/:id`): identificación, contactos y cada vínculo con esta empresa con su certificado, su cuenta designada y los documentos compartidos con él. De las otras empresas del grupo solo dice que hay vínculo y en qué estado. Incluye las secciones vigentes de la ficha maestra (datos, completitud y soportes) y, por cada vínculo con la empresa activa, lo que aceptó de cada una, si hay una versión nueva pendiente y su revalidación (F2). Se arma al consultar.

### `GET /api/v1/terceros/{id}/ficha`

**Obtener ficha tercero.**

**Permiso terceros:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerFichaTercero`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — FichaTerceroDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tercero` | TerceroDto | sí |  |
| `tercero.id` | uuid | sí |  |
| `tercero.identificacion` | IdentificacionDto | sí |  |
| `tercero.identificacion.pais` | texto | sí |  |
| `tercero.identificacion.tipo` | texto | sí |  |
| `tercero.identificacion.numero` | texto | sí |  |
| `tercero.identificacion.digitoVerificacion` | texto o null | no |  |
| `tercero.tipoPersona` | texto | sí |  |
| `tercero.nombre` | texto | sí |  |
| `tercero.nombreComercial` | texto o null | no |  |
| `tercero.contactos` | lista de ContactoDto | sí |  |
| `tercero.contactos[].nombre` | texto | sí |  |
| `tercero.contactos[].correo` | texto | sí |  |
| `tercero.contactos[].telefono` | texto o null | no |  |
| `tercero.contactos[].rol` | texto | sí |  |
| `tercero.esExtranjero` | booleano | sí |  |
| `tercero.creadoEn` | fecha y hora | sí |  |
| `vinculos` | lista de VinculoEnFichaDto | sí |  |
| `vinculos[].vinculo` | VinculoDto | sí |  |
| `vinculos[].vinculo.id` | uuid | sí |  |
| `vinculos[].vinculo.empresaId` | uuid | sí |  |
| `vinculos[].vinculo.terceroId` | uuid | sí |  |
| `vinculos[].vinculo.tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `vinculos[].vinculo.tipoVinculo.id` | uuid | sí |  |
| `vinculos[].vinculo.tipoVinculo.codigo` | texto | sí |  |
| `vinculos[].vinculo.tipoVinculo.nombre` | texto | sí |  |
| `vinculos[].vinculo.flujoId` | uuid o null | no |  |
| `vinculos[].vinculo.estado` | EstadoVinculo (texto) | sí | valores: Invitado · EnValidacion · Activo · PorVencer · Vencido · Bloqueado · Desvinculado · Anulado |
| `vinculos[].vinculo.atributos` | objeto libre | sí |  |
| `vinculos[].vinculo.correoTercero` | texto | sí |  |
| `vinculos[].vinculo.invitacion` | EnlaceInvitacionDto o null | no |  |
| `vinculos[].vinculo.invitacion.correoDestino` | texto | sí |  |
| `vinculos[].vinculo.invitacion.expira` | fecha y hora | sí |  |
| `vinculos[].vinculo.invitacion.envios` | entero | sí |  |
| `vinculos[].vinculo.invitacionAceptadaEn` | fecha y hora o null | no |  |
| `vinculos[].vinculo.requisitosCubribles` | entero o null | no |  |
| `vinculos[].vinculo.bloqueo` | BloqueoDto o null | no |  |
| `vinculos[].vinculo.bloqueo.motivo` | texto | sí |  |
| `vinculos[].vinculo.bloqueo.desde` | fecha y hora | sí |  |
| `vinculos[].vinculo.bloqueo.hasta` | fecha y hora o null | no |  |
| `vinculos[].vinculo.referencias` | lista de ReferenciaExternaDto | sí |  |
| `vinculos[].vinculo.referencias[].sistema` | texto | sí |  |
| `vinculos[].vinculo.referencias[].id` | texto | sí |  |
| `vinculos[].vinculo.origen` | OrigenVinculo (texto) | sí | valores: Invitacion · Autorregistro · Migracion · Api |
| `vinculos[].vinculo.creadoEn` | fecha y hora | sí |  |
| `vinculos[].vinculo.actualizadoEn` | fecha y hora | sí |  |
| `vinculos[].vinculo.version` | entero | sí |  |
| `vinculos[].vinculo.expedienteAbiertoId` | uuid o null | no |  |
| `vinculos[].vinculo.aperturaDeExpedientePendiente` | booleano | no | por omisión false |
| `vinculos[].vinculo.ultimoResultado` | texto o null | no |  |
| `vinculos[].vinculo.contactos` | lista de ContactoDeEmpresaDto o null | no |  |
| `vinculos[].vinculo.contactos[].nombre` | texto | sí |  |
| `vinculos[].vinculo.contactos[].cargo` | texto o null | no |  |
| `vinculos[].vinculo.contactos[].correo` | texto | sí |  |
| `vinculos[].vinculo.contactos[].telefono` | texto o null | no |  |
| `vinculos[].vinculo.contactos[].rol` | texto | sí |  |
| `vinculos[].vinculo.vigenteHasta` | fecha o null | no |  |
| `vinculos[].vinculo.enRevision` | booleano | no | por omisión false |
| `vinculos[].vinculo.actualizacionNoAprobada` | ActualizacionNoAprobadaDto o null | no |  |
| `vinculos[].vinculo.actualizacionNoAprobada.expedienteId` | uuid o null | no |  |
| `vinculos[].vinculo.actualizacionNoAprobada.resultado` | texto | sí |  |
| `vinculos[].vinculo.actualizacionNoAprobada.fecha` | fecha y hora o null | no |  |
| `vinculos[].certificado` | CertificadoDelVinculoDto o null | no |  |
| `vinculos[].certificado.vinculoId` | uuid | sí |  |
| `vinculos[].certificado.actual` | CertificadoDto o null | no |  |
| `vinculos[].certificado.actual.id` | uuid | sí |  |
| `vinculos[].certificado.actual.folio` | texto | sí |  |
| `vinculos[].certificado.actual.folioRaiz` | texto | sí |  |
| `vinculos[].certificado.actual.emision` | entero | sí |  |
| `vinculos[].certificado.actual.esLaActual` | booleano | sí |  |
| `vinculos[].certificado.actual.estado` | texto | sí |  |
| `vinculos[].certificado.actual.bloqueante` | booleano | sí |  |
| `vinculos[].certificado.actual.empresa` | EmpresaDelCertificadoDto | sí |  |
| `vinculos[].certificado.actual.empresa.id` | uuid | sí |  |
| `vinculos[].certificado.actual.empresa.nombre` | texto | sí |  |
| `vinculos[].certificado.actual.empresa.logoUrl` | texto o null | no |  |
| `vinculos[].certificado.actual.vinculoId` | uuid | sí |  |
| `vinculos[].certificado.actual.expedienteId` | uuid | sí |  |
| `vinculos[].certificado.actual.expedienteFolio` | texto | sí |  |
| `vinculos[].certificado.actual.tercero` | TitularDelCertificadoDto | sí |  |
| `vinculos[].certificado.actual.tercero.nombre` | texto | sí |  |
| `vinculos[].certificado.actual.tercero.identificacionEnmascarada` | texto | sí |  |
| `vinculos[].certificado.actual.tercero.pais` | texto | sí |  |
| `vinculos[].certificado.actual.tercero.tipoPersona` | texto | sí |  |
| `vinculos[].certificado.actual.tipoVinculo` | TipoVinculoDelCertificadoDto | sí |  |
| `vinculos[].certificado.actual.tipoVinculo.codigo` | texto | sí |  |
| `vinculos[].certificado.actual.tipoVinculo.nombre` | texto | sí |  |
| `vinculos[].certificado.actual.flujo` | FlujoDelCertificadoDto | sí |  |
| `vinculos[].certificado.actual.flujo.codigo` | texto | sí |  |
| `vinculos[].certificado.actual.flujo.nombre` | texto | sí |  |
| `vinculos[].certificado.actual.flujo.version` | entero | sí |  |
| `vinculos[].certificado.actual.emitidoEn` | fecha y hora | sí |  |
| `vinculos[].certificado.actual.vigenteHasta` | fecha o null | no |  |
| `vinculos[].certificado.actual.huella` | texto | sí |  |
| `vinculos[].certificado.actual.hashContenido` | texto | sí |  |
| `vinculos[].certificado.actual.urlVerificacion` | texto | sí |  |
| `vinculos[].certificado.actual.requisitos` | lista de RequisitoDelCertificadoDto | sí |  |
| `vinculos[].certificado.actual.requisitos[].codigo` | texto | sí |  |
| `vinculos[].certificado.actual.requisitos[].nombre` | texto | sí |  |
| `vinculos[].certificado.actual.requisitos[].version` | entero o null | no |  |
| `vinculos[].certificado.actual.requisitos[].sha256` | texto o null | no |  |
| `vinculos[].certificado.actual.requisitos[].vigenteHasta` | fecha o null | no |  |
| `vinculos[].certificado.actual.requisitos[].eximido` | booleano | sí |  |
| `vinculos[].certificado.actual.requisitos[].justificacion` | texto o null | no |  |
| `vinculos[].certificado.actual.areas` | lista de AreaDelCertificadoDto | sí |  |
| `vinculos[].certificado.actual.areas[].area` | texto | sí |  |
| `vinculos[].certificado.actual.areas[].codigo` | texto | sí |  |
| `vinculos[].certificado.actual.areas[].origen` | texto | sí |  |
| `vinculos[].certificado.actual.areas[].fecha` | fecha y hora | sí |  |
| `vinculos[].certificado.actual.pendientes` | lista de texto | sí |  |
| `vinculos[].certificado.actual.suspension` | SuspensionDelCertificadoDto o null | no |  |
| `vinculos[].certificado.actual.suspension.motivo` | texto | sí |  |
| `vinculos[].certificado.actual.suspension.desde` | fecha y hora | sí |  |
| `vinculos[].certificado.actual.revocacion` | RevocacionDelCertificadoDto o null | no |  |
| `vinculos[].certificado.actual.revocacion.motivo` | texto | sí |  |
| `vinculos[].certificado.actual.revocacion.origen` | texto | sí |  |
| `vinculos[].certificado.actual.revocacion.fecha` | fecha y hora | sí |  |
| `vinculos[].certificado.actual.pdf` | PdfDelCertificadoDto o null | no |  |
| `vinculos[].certificado.actual.pdf.sha256` | texto | sí |  |
| `vinculos[].certificado.actual.pdf.tamano` | entero | sí |  |
| `vinculos[].certificado.actual.pdf.generadoEn` | fecha y hora | sí |  |
| `vinculos[].certificado.actual.descargas` | entero | sí |  |
| `vinculos[].certificado.actual.firma` | FirmaDelCertificadoDto | sí |  |
| `vinculos[].certificado.actual.firma.algoritmo` | texto | sí |  |
| `vinculos[].certificado.actual.firma.kid` | texto | sí |  |
| `vinculos[].certificado.actual.firma.clavePublicaUrl` | texto | sí |  |
| `vinculos[].certificado.actual.firma.valor` | texto | sí |  |
| `vinculos[].certificado.actual.contenidoCanonico` | JsonElement | sí |  |
| `vinculos[].certificado.actual.version` | entero | sí |  |
| `vinculos[].certificado.actual.enVerificacion` | booleano | no | por omisión false |
| `vinculos[].certificado.emisiones` | lista de EmisionDelCertificadoDto | sí |  |
| `vinculos[].certificado.emisiones[].folio` | texto | sí |  |
| `vinculos[].certificado.emisiones[].emision` | entero | sí |  |
| `vinculos[].certificado.emisiones[].estado` | texto | sí |  |
| `vinculos[].certificado.emisiones[].emitidoEn` | fecha y hora | sí |  |
| `vinculos[].certificado.emisiones[].vigenteHasta` | fecha o null | no |  |
| `vinculos[].certificado.emisiones[].huella` | texto | sí |  |
| `vinculos[].certificado.emisiones[].expedienteFolio` | texto | sí |  |
| `vinculos[].certificado.emisiones[].pdfDisponible` | booleano | sí |  |
| `vinculos[].cuentaDesignada` | CuentaDesignadaDelVinculoDto | sí |  |
| `vinculos[].cuentaDesignada.actual` | CuentaDesignadaDto o null | no |  |
| `vinculos[].cuentaDesignada.actual.cuentaId` | uuid | sí |  |
| `vinculos[].cuentaDesignada.actual.banco` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.tipoCuenta` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.numeroEnmascarado` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.numero` | texto o null | no |  |
| `vinculos[].cuentaDesignada.actual.titular` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.identificacionTitular` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.moneda` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.estado` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.propuestaEn` | fecha y hora | sí |  |
| `vinculos[].cuentaDesignada.actual.aprobaciones` | lista de AprobacionDeCuentaDto | sí |  |
| `vinculos[].cuentaDesignada.actual.aprobaciones[].usuarioId` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.aprobaciones[].nombre` | texto o null | no |  |
| `vinculos[].cuentaDesignada.actual.aprobaciones[].fecha` | fecha y hora | sí |  |
| `vinculos[].cuentaDesignada.actual.aprobadaEn` | fecha y hora o null | no |  |
| `vinculos[].cuentaDesignada.actual.motivoRechazo` | texto o null | no |  |
| `vinculos[].cuentaDesignada.actual.bancoCodigo` | texto o null | no |  |
| `vinculos[].cuentaDesignada.actual.bancoSwift` | texto o null | no |  |
| `vinculos[].cuentaDesignada.actual.contactoPagos` | ContactoDePagosDto o null | no |  |
| `vinculos[].cuentaDesignada.actual.contactoPagos.nombre` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.contactoPagos.correo` | texto | sí |  |
| `vinculos[].cuentaDesignada.actual.contactoPagos.telefono` | texto o null | no |  |
| `vinculos[].cuentaDesignada.propuesta` | CuentaDesignadaDto o null | no | mismos campos que `vinculos[].cuentaDesignada.actual` |
| `vinculos[].cuentaDesignada.rechazada` | CuentaDesignadaDto o null | no | mismos campos que `vinculos[].cuentaDesignada.actual` |
| `vinculos[].cuentaDesignada.aprobadoresRequeridos` | entero | sí |  |
| `vinculos[].cuentaDesignada.puedesAprobar` | booleano | sí |  |
| `vinculos[].documentos` | lista de DocumentoVistaEmpresaDto o null | no |  |
| `vinculos[].documentos[].documentoId` | uuid | sí |  |
| `vinculos[].documentos[].tipo` | TipoDocumentoResumenDto | sí |  |
| `vinculos[].documentos[].tipo.codigo` | texto | sí |  |
| `vinculos[].documentos[].tipo.nombre` | texto | sí |  |
| `vinculos[].documentos[].tipo.tipoDocumentoTenantId` | uuid o null | no |  |
| `vinculos[].documentos[].ambito` | AmbitoDocumento (texto) | sí | valores: Global · PorVinculo |
| `vinculos[].documentos[].versionActual` | entero o null | no |  |
| `vinculos[].documentos[].versionesCompartidas` | lista de VersionDocumentoDto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].documentoId` | uuid | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].n` | entero | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].estado` | EstadoVersion (texto) | sí | valores: Recibida · EnAnalisis · Actual · Superada · Devuelta · Retirada |
| `vinculos[].documentos[].versionesCompartidas[].cargadaEn` | fecha y hora | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].cargadaPor` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `vinculos[].documentos[].versionesCompartidas[].nombreOriginal` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].mime` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].tamanoBytes` | entero | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].paginas` | entero o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].sha256` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].motivoDevolucion` | texto o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].detalleDevolucion` | texto o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos` | HechosDto o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.conIa` | booleano | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.hashVerificado` | booleano | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.antivirus` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.mimeReal` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.paginas` | entero o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.comparacion` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.camposCambiados` | lista de texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.camposCriticosCambiados` | lista de texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.pendienteRevisionHumana` | booleano | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia` | HechosIaDto o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.modelo` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.escalado` | booleano | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.legible` | booleano | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.tipoDetectado` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.campos` | objeto libre | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.alertas` | lista de texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.observaciones` | texto o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.paginasEnviadas` | entero o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.paginasOmitidas` | entero o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].hechos.ia.fecha` | fecha y hora | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].datosDeclarados[].clave` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].datosDeclarados[].etiqueta` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].datosDeclarados[].tipo` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].datosDeclarados[].valor` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].vigencia` | VigenciaDto o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].vigencia.vigenteHasta` | fecha o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].vigencia.base` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].vigencia.estado` | texto | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].vigencia.diasAviso` | lista de entero | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].vigencia.diasGracia` | entero | sí |  |
| `vinculos[].documentos[].versionesCompartidas[].vigencia.finDeGracia` | fecha o null | no |  |
| `vinculos[].documentos[].versionesCompartidas[].vigencia.vencidaEn` | fecha y hora o null | no |  |
| `otrasEmpresasDelGrupo` | lista de VinculoEnOtraEmpresaDto | sí |  |
| `otrasEmpresasDelGrupo[].empresaId` | uuid | sí |  |
| `otrasEmpresasDelGrupo[].estado` | texto | sí |  |
| `secciones` | lista de SeccionFichaDto o null | no |  |
| `secciones[].seccion` | texto | sí |  |
| `secciones[].nombre` | texto | sí |  |
| `secciones[].periodo` | entero o null | no |  |
| `secciones[].version` | entero o null | no |  |
| `secciones[].datos` | JsonElement o null | no |  |
| `secciones[].soportes` | lista de SoporteSeccionDto | sí |  |
| `secciones[].soportes[].documentoId` | uuid | sí |  |
| `secciones[].soportes[].version` | entero | sí |  |
| `secciones[].soportes[].tipoDocumento` | texto o null | no |  |
| `secciones[].soportes[].retirado` | booleano | no | por omisión false |
| `secciones[].soportes[].nombre` | texto o null | no |  |
| `secciones[].completitud` | CompletitudSeccionDto o null | no |  |
| `secciones[].completitud.completa` | booleano | sí |  |
| `secciones[].completitud.faltantes` | lista de FaltanteSeccionDto | sí |  |
| `secciones[].completitud.faltantes[].campo` | texto o null | no |  |
| `secciones[].completitud.faltantes[].regla` | texto o null | no |  |
| `secciones[].completitud.faltantes[].mensaje` | texto | sí |  |
| `secciones[].completitud.reglaVersion` | entero | sí |  |
| `secciones[].huella` | texto o null | no |  |
| `secciones[].origen` | texto o null | no |  |
| `secciones[].fecha` | fecha y hora o null | no |  |
| `secciones[].versiones` | entero | sí |  |
| `secciones[].esquemaVersion` | entero | sí |  |
| `secciones[].aceptaciones` | lista de AceptacionDeSeccionDto o null | no |  |
| `secciones[].aceptaciones[].vinculoId` | uuid | sí |  |
| `secciones[].aceptaciones[].versionAceptada` | entero o null | no |  |
| `secciones[].aceptaciones[].aceptadaEn` | fecha y hora o null | no |  |
| `secciones[].aceptaciones[].expedienteId` | uuid o null | no |  |
| `secciones[].aceptaciones[].folio` | texto o null | no |  |
| `secciones[].aceptaciones[].versionNuevaPendiente` | booleano | sí |  |
| `secciones[].aceptaciones[].revalidacion` | RevalidacionDeSeccionDto o null | no |  |
| `secciones[].aceptaciones[].revalidacion.expedienteId` | uuid | sí |  |
| `secciones[].aceptaciones[].revalidacion.folio` | texto | sí |  |
| `secciones[].aceptaciones[].revalidacion.estado` | EstadoRequisito (texto) | sí | valores: Pendiente · Cargado · EnRevision · Observado · Aprobado · Eximido |
| `secciones[].aceptaciones[].revalidacion.version` | entero o null | no |  |
| `secciones[].aceptaciones[].revalidacion.plazoHasta` | fecha y hora | sí |  |
| `secciones[].aceptaciones[].origen` | OrigenDeAceptacion (texto) | no | valores: Expediente · Importacion |
| `secciones[].prellenado` | cualquiera | no |  |
| `secciones[].sinSoporte` | SinSoporteDto o null | no |  |
| `secciones[].sinSoporte.critico` | booleano | sí |  |
| `secciones[].sinSoporte.cambios` | lista de CambioDeCampoDto | sí |  |
| `secciones[].sinSoporte.cambios[].campo` | texto | sí |  |
| `secciones[].sinSoporte.cambios[].etiqueta` | texto | sí |  |
| `secciones[].sinSoporte.cambios[].anterior` | texto o null | no |  |
| `secciones[].sinSoporte.cambios[].nuevo` | texto o null | no |  |
| `secciones[].sinSoporte.cambios[].tipoCambio` | TipoCambio (texto) | sí | valores: Agregado · Quitado · Modificado |

**Errores de negocio que lanza**

- `404` `tercero-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/terceros/3fa85f64-5717-4562-b3fc-2c963f66afa6/ficha" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "tercero": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "identificacion": {
      "pais": "CO",
      "tipo": "texto",
      "numero": "900123456",
      "digitoVerificacion": "texto"
    },
    "tipoPersona": "texto",
    "nombre": "Constructora Norte S.A.S.",
    "nombreComercial": "Constructora Norte S.A.S.",
    "contactos": [
      {
        "nombre": "Constructora Norte S.A.S.",
        "correo": "contacto@empresa.co",
        "telefono": "+57 601 555 1234",
        "rol": "texto"
      }
    ],
    "esExtranjero": true,
    "creadoEn": "2026-11-12T15:00:00Z"
  },
  "vinculos": [
    {
      "vinculo": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "tipoVinculo": {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S."
        },
        "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "estado": "Invitado",
        "atributos": {},
        "correoTercero": "contacto@empresa.co",
        "invitacion": {
          "correoDestino": "contacto@empresa.co",
          "expira": "2026-11-12T15:00:00Z",
          "envios": 1
        },
        "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
        "requisitosCubribles": 1,
        "bloqueo": {
          "motivo": "Texto de ejemplo.",
          "desde": "2026-11-12T15:00:00Z",
          "hasta": "2026-11-12T15:00:00Z"
        },
        "referencias": [
          {
            "sistema": "texto",
            "id": "texto"
          }
        ],
        "origen": "Invitacion",
        "creadoEn": "2026-11-12T15:00:00Z",
        "actualizadoEn": "2026-11-12T15:00:00Z",
        "version": 1,
        "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "aperturaDeExpedientePendiente": false,
        "ultimoResultado": "texto",
        "contactos": [
          {
            "nombre": "Constructora Norte S.A.S.",
            "cargo": "texto",
            "correo": "contacto@empresa.co",
            "telefono": "+57 601 555 1234",
            "rol": "texto"
          }
        ],
        "vigenteHasta": "2026-11-12",
        "enRevision": false,
        "actualizacionNoAprobada": {
          "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z"
        }
      },
      "certificado": {
        "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "actual": {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "folio": "texto",
          "folioRaiz": "texto",
          "emision": 1,
          "esLaActual": true,
          "estado": "texto",
          "bloqueante": true,
          "empresa": {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "logoUrl": "https://ejemplo.co/recurso"
          },
          "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "expedienteFolio": "texto",
          "tercero": {
            "nombre": "Constructora Norte S.A.S.",
            "identificacionEnmascarada": "900123456",
            "pais": "CO",
            "tipoPersona": "texto"
          },
          "tipoVinculo": {
            "codigo": "CODIGO_EJEMPLO",
            "nombre": "Constructora Norte S.A.S."
          },
          "flujo": {
            "codigo": "CODIGO_EJEMPLO",
            "nombre": "Constructora Norte S.A.S.",
            "version": 1
          },
          "emitidoEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12",
          "huella": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
          "hashContenido": "Texto de ejemplo.",
          "urlVerificacion": "https://ejemplo.co/recurso",
          "requisitos": [
            {
              "codigo": null,
              "nombre": null,
              "version": null,
              "sha256": null,
              "vigenteHasta": null,
              "eximido": null,
              "justificacion": null
            }
          ],
          "areas": [
            {
              "area": null,
              "codigo": null,
              "origen": null,
              "fecha": null
            }
          ],
          "pendientes": [
            "texto"
          ],
          "suspension": {
            "motivo": "Texto de ejemplo.",
            "desde": "2026-11-12T15:00:00Z"
          },
          "revocacion": {
            "motivo": "Texto de ejemplo.",
            "origen": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          },
          "pdf": {
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "tamano": 1,
            "generadoEn": "2026-11-12T15:00:00Z"
          },
          "descargas": 1,
          "firma": {
            "algoritmo": "texto",
            "kid": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "clavePublicaUrl": "https://ejemplo.co/recurso",
            "valor": "vsk_prueba_ejemplo"
          },
          "contenidoCanonico": {},
          "version": 1,
          "enVerificacion": false
        },
        "emisiones": [
          {
            "folio": "texto",
            "emision": 1,
            "estado": "texto",
            "emitidoEn": "2026-11-12T15:00:00Z",
            "vigenteHasta": "2026-11-12",
            "huella": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "expedienteFolio": "texto",
            "pdfDisponible": true
          }
        ]
      },
      "cuentaDesignada": {
        "actual": {
          "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "banco": "texto",
          "tipoCuenta": "texto",
          "numeroEnmascarado": "900123456",
          "numero": "900123456",
          "titular": "texto",
          "identificacionTitular": "900123456",
          "moneda": "texto",
          "estado": "texto",
          "propuestaEn": "2026-11-12T15:00:00Z",
          "aprobaciones": [
            {
              "usuarioId": null,
              "nombre": null,
              "fecha": null
            }
          ],
          "aprobadaEn": "2026-11-12T15:00:00Z",
          "motivoRechazo": "Texto de ejemplo.",
          "bancoCodigo": "codigo-de-ejemplo",
          "bancoSwift": "texto",
          "contactoPagos": {
            "nombre": "Constructora Norte S.A.S.",
            "correo": "contacto@empresa.co",
            "telefono": "+57 601 555 1234"
          }
        },
        "propuesta": {
          "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "banco": "texto",
          "tipoCuenta": "texto",
          "numeroEnmascarado": "900123456",
          "numero": "900123456",
          "titular": "texto",
          "identificacionTitular": "900123456",
          "moneda": "texto",
          "estado": "texto",
          "propuestaEn": "2026-11-12T15:00:00Z",
          "aprobaciones": [
            {
              "usuarioId": null,
              "nombre": null,
              "fecha": null
            }
          ],
          "aprobadaEn": "2026-11-12T15:00:00Z",
          "motivoRechazo": "Texto de ejemplo.",
          "bancoCodigo": "codigo-de-ejemplo",
          "bancoSwift": "texto",
          "contactoPagos": {
            "nombre": "Constructora Norte S.A.S.",
            "correo": "contacto@empresa.co",
            "telefono": "+57 601 555 1234"
          }
        },
        "rechazada": {
          "cuentaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "banco": "texto",
          "tipoCuenta": "texto",
          "numeroEnmascarado": "900123456",
          "numero": "900123456",
          "titular": "texto",
          "identificacionTitular": "900123456",
          "moneda": "texto",
          "estado": "texto",
          "propuestaEn": "2026-11-12T15:00:00Z",
          "aprobaciones": [
            {
              "usuarioId": null,
              "nombre": null,
              "fecha": null
            }
          ],
          "aprobadaEn": "2026-11-12T15:00:00Z",
          "motivoRechazo": "Texto de ejemplo.",
          "bancoCodigo": "codigo-de-ejemplo",
          "bancoSwift": "texto",
          "contactoPagos": {
            "nombre": "Constructora Norte S.A.S.",
            "correo": "contacto@empresa.co",
            "telefono": "+57 601 555 1234"
          }
        },
        "aprobadoresRequeridos": 1,
        "puedesAprobar": true
      },
      "documentos": [
        {
          "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "tipo": {
            "codigo": "CODIGO_EJEMPLO",
            "nombre": "Constructora Norte S.A.S.",
            "tipoDocumentoTenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          },
          "ambito": "Global",
          "versionActual": 1,
          "versionesCompartidas": [
            {
              "documentoId": null,
              "n": null,
              "estado": null,
              "cargadaEn": null,
              "cargadaPor": null,
              "clase": null,
              "nombreOriginal": null,
              "mime": null,
              "tamanoBytes": null,
              "paginas": null,
              "sha256": null,
              "motivoDevolucion": null,
              "detalleDevolucion": null,
              "hechos": null,
              "datosDeclarados": null,
              "vigencia": null
            }
          ]
        }
      ]
    }
  ],
  "otrasEmpresasDelGrupo": [
    {
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "estado": "texto"
    }
  ],
  "secciones": [
    {
      "seccion": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "periodo": 1,
      "version": 1,
      "datos": {},
      "soportes": [
        {
          "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "version": 1,
          "tipoDocumento": "texto",
          "retirado": false,
          "nombre": "Constructora Norte S.A.S."
        }
      ],
      "completitud": {
        "completa": true,
        "faltantes": [
          {
            "campo": "texto",
            "regla": "texto",
            "mensaje": "Texto de ejemplo."
          }
        ],
        "reglaVersion": 1
      },
      "huella": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "origen": "texto",
      "fecha": "2026-11-12T15:00:00Z",
      "versiones": 1,
      "esquemaVersion": 1,
      "aceptaciones": [
        {
          "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "versionAceptada": 1,
          "aceptadaEn": "2026-11-12T15:00:00Z",
          "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "folio": "texto",
          "versionNuevaPendiente": true,
          "revalidacion": {
            "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "folio": "texto",
            "estado": "Pendiente",
            "version": 1,
            "plazoHasta": "2026-11-12T15:00:00Z"
          },
          "origen": "Expediente"
        }
      ],
      "prellenado": null,
      "sinSoporte": {
        "critico": true,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ]
      }
    }
  ]
}
```

</details>

## Referencias de tercero

### `GET /api/v1/terceros/{id}/referencias`

**Listar referencias tercero.** Las referencias del tercero en el tenant de la credencial (404 si el tercero no es visible para él).

**Permiso terceros:leer · API (`/api/v1`) · SDK: `listarReferenciasTercero`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — lista de ReferenciaExternaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de ReferenciaExternaDto | sí |  |
| `[].sistema` | texto | sí |  |
| `[].id` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/terceros/3fa85f64-5717-4562-b3fc-2c963f66afa6/referencias" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "sistema": "texto",
    "id": "texto"
  }
]
```

</details>

### `POST /api/v1/terceros/{id}/referencias`

**Agregar referencia tercero.** Registra la clave del tercero en el sistema de la credencial. Idempotente: la misma referencia responde 200; nueva, 201. Una clave que ya identifica a otro tercero es 409 `referencia-duplicada`. Un tercero puede tener varias claves en un mismo sistema (p. ej. una por instancia); cada clave identifica a un solo tercero.

**Permiso terceros:escribir · API (`/api/v1`) · SDK: `agregarReferenciaTercero`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ReferenciaExternaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `sistema` | texto | sí |  |
| `id` | texto | sí |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/terceros/3fa85f64-5717-4562-b3fc-2c963f66afa6/referencias" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"sistema":"texto","id":"texto"}'
```

</details>

### `GET /api/v1/terceros/por-referencia`

**Buscar tercero por referencia.** Busca el tercero por la clave que le dio un sistema integrador (404 si no la hay en el tenant o el tercero no es visible).

**Permiso terceros:leer · API (`/api/v1`) · SDK: `buscarTerceroPorReferencia`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `sistema` | query | texto | no |  |
| `clave` | query | texto | no |  |

**Respuestas**

- `200` OK — TerceroDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `tipoPersona` | texto | sí |  |
| `nombre` | texto | sí |  |
| `nombreComercial` | texto o null | no |  |
| `contactos` | lista de ContactoDto | sí |  |
| `contactos[].nombre` | texto | sí |  |
| `contactos[].correo` | texto | sí |  |
| `contactos[].telefono` | texto o null | no |  |
| `contactos[].rol` | texto | sí |  |
| `esExtranjero` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |

**Errores de negocio que lanza**

- `404` `tercero-no-encontrado`
- `422` `referencia-externa-invalida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/terceros/por-referencia" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "tipoPersona": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "nombreComercial": "Constructora Norte S.A.S.",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "esExtranjero": true,
  "creadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

## Referencias de vínculo

Referencias externas del vínculo (IO-2): agregar la clave de otro sistema a un vínculo que ya existe.

### `POST /api/v1/vinculos/{id}/referencias`

**Agregar referencia vínculo.** Agrega al vínculo la referencia del sistema de la credencial (I-0): comparte la validación y recibe sus eventos. Idempotente (200 con el vínculo); el mismo sistema con otra clave es 409 `referencia-en-conflicto`; una clave que ya identifica otro vínculo de la empresa, 409 `referencia-duplicada`.

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `agregarReferenciaVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ReferenciaExternaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `sistema` | texto | sí |  |
| `id` | texto | sí |  |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/referencias" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"sistema":"texto","id":"texto"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

## Terceros

### `POST /api/v1/terceros`

**Crear tercero.** Crea el tercero (201). Con `referenciaExterna` (el sistema lo fija la API key) la creación es idempotente: si esa clave ya identifica a un tercero con la misma identificación, responde 200 con él; si identifica a otro, 409 `referencia-duplicada`.

**Permiso terceros:escribir · API (`/api/v1`) · SDK: `crearTercero`**

**Cuerpo de la petición** (`application/json`, CrearTerceroDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `tipoPersona` | texto | sí |  |
| `nombre` | texto | sí |  |
| `nombreComercial` | texto o null | no |  |
| `contactos` | lista de ContactoDto o null | no |  |
| `contactos[].nombre` | texto | sí |  |
| `contactos[].correo` | texto | sí |  |
| `contactos[].telefono` | texto o null | no |  |
| `contactos[].rol` | texto | sí |  |
| `referenciaExterna` | ReferenciaExternaDto o null | no |  |
| `referenciaExterna.sistema` | texto | sí |  |
| `referenciaExterna.id` | texto | sí |  |

**Respuestas**

- `200` OK — TerceroDto
- `201` Creado — TerceroDto

Campos de la respuesta `200`:

Mismos campos de `TerceroDto` que se detallan en `GET /api/v1/terceros/por-referencia`.

Campos de la respuesta `201`:

Mismos campos de `TerceroDto` que se detallan en `GET /api/v1/terceros/por-referencia`.

**Errores de negocio que lanza**

- `409` `referencia-duplicada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/terceros" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"identificacion":{"pais":"CO","tipo":"texto","numero":"900123456","digitoVerificacion":"texto"},"tipoPersona":"texto","nombre":"Constructora Norte S.A.S.","nombreComercial":"Constructora Norte S.A.S.","contactos":[{"nombre":"Constructora Norte S.A.S.","correo":"contacto@empresa.co","telefono":"+57 601 555 1234","rol":"texto"}],"referenciaExterna":{"sistema":"texto","id":"texto"}}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "tipoPersona": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "nombreComercial": "Constructora Norte S.A.S.",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "esExtranjero": true,
  "creadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `GET /api/v1/terceros/{id}`

**Obtener tercero.**

**Permiso terceros:leer · API (`/api/v1`) · SDK: `obtenerTercero`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — TerceroDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `TerceroDto` que se detallan en `GET /api/v1/terceros/por-referencia`.

**Errores de negocio que lanza**

- `404` `tercero-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/terceros/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "tipoPersona": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "nombreComercial": "Constructora Norte S.A.S.",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "esExtranjero": true,
  "creadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `GET /api/v1/terceros/por-identificacion`

**Buscar tercero por identificacion.** Búsqueda exacta por identificación normalizada; 404 si no es visible para el tenant (anti-cosecha).

**Permiso terceros:leer · API (`/api/v1`) · SDK: `buscarTerceroPorIdentificacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `pais` | query | texto | no |  |
| `tipo` | query | texto | no |  |
| `numero` | query | texto | no |  |

**Respuestas**

- `200` OK — TerceroDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `TerceroDto` que se detallan en `GET /api/v1/terceros/por-referencia`.

**Errores de negocio que lanza**

- `404` `tercero-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/terceros/por-identificacion" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "tipoPersona": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "nombreComercial": "Constructora Norte S.A.S.",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "esExtranjero": true,
  "creadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

## Vencimientos

Vencimientos de la empresa activa (VE-1..VE-3, docs/06 `/vencimientos`): por vínculo y documento compartido, la vigencia que la bóveda calculó para esta empresa (TD-6), con su estado, su base y si se puede ratificar; el informe en Excel y el recordatorio al tercero. Se calcula al consultar sobre los documentos de los terceros de la empresa (aún no es la proyección `VencimientoProgramado`).

### `GET /api/v1/vencimientos`

**Listar vencimientos.** `dias`: los que vencen en los próximos N días más los que ya están en gracia o vencidos (30 por defecto; 0 = todos los que tienen fecha). `estado`: Vigente, PorVencer, EnGracia o Vencida.

**Permiso documentos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarVencimientos`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `dias` | query | entero | no |  |
| `estado` | query | texto | no |  |

**Respuestas**

- `200` OK — lista de VencimientoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de VencimientoDto | sí |  |
| `[].vinculoId` | uuid | sí |  |
| `[].documentoId` | uuid | sí |  |
| `[].tercero` | TerceroResumenDto | sí |  |
| `[].tercero.id` | uuid | sí |  |
| `[].tercero.nombre` | texto | sí |  |
| `[].tercero.identificacion` | texto | sí |  |
| `[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `[].tipoVinculo.id` | uuid | sí |  |
| `[].tipoVinculo.codigo` | texto | sí |  |
| `[].tipoVinculo.nombre` | texto | sí |  |
| `[].documento` | texto | sí |  |
| `[].version` | entero | sí |  |
| `[].vigenteHasta` | fecha | sí |  |
| `[].diasRestantes` | entero | sí |  |
| `[].estado` | texto | sí |  |
| `[].base` | texto | sí |  |
| `[].finDeGracia` | fecha o null | no |  |
| `[].permiteRatificar` | booleano | sí |  |
| `[].ultimoRecordatorio` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vencimientos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
  {
    "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
    "documento": "texto",
    "version": 1,
    "vigenteHasta": "2026-11-12",
    "diasRestantes": 1,
    "estado": "texto",
    "base": "texto",
    "finDeGracia": "2026-11-12",
    "permiteRatificar": true,
    "ultimoRecordatorio": "2026-11-12T15:00:00Z"
  }
]
```

</details>

### `GET /api/v1/vencimientos/excel`

**Exportar vencimientos.**

**Permiso documentos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `exportarVencimientos`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `dias` | query | entero | no |  |
| `estado` | query | texto | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vencimientos/excel" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>

### `POST /api/v1/vencimientos/recordar`

**Recordar vencimientos.** Recuerda al tercero los documentos elegidos: un correo por destinatario con cada documento y su fecha para esta empresa (el mismo aviso consolidado de TD-7). Lo ya recordado hoy se omite, para no repetir correos.

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `recordarVencimientos`**

**Cuerpo de la petición** (`application/json`, RecordarVencimientosDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `vencimientos` | lista de VencimientoARecordarDto | sí |  |
| `vencimientos[].vinculoId` | uuid | sí |  |
| `vencimientos[].documentoId` | uuid | sí |  |

**Respuestas**

- `200` OK — RecordatorioEnviadoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correos` | entero | sí |  |
| `documentos` | entero | sí |  |
| `omitidos` | entero | sí |  |

**Errores de negocio que lanza**

- `422` `lote-demasiado-grande`
- `422` `lote-vacio`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vencimientos/recordar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"vencimientos":[{"vinculoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","documentoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}]}'
```

Respuesta `200`:

```json
{
  "correos": 1,
  "documentos": 1,
  "omitidos": 1
}
```

</details>

## Vigencias de vínculos

Ejecución inmediata del job de vigencias (Plataforma).

### `POST /api/v1/plataforma/vigencias-de-vinculos/revisar`

**Revisar vigencias de vínculos.** Corre ya el job de vigencias de vínculos (Plataforma) y espera su resultado: de un tenant o de todos. `fecha` simula «ahora» y solo se admite en Development (pruebas de punta a punta; 422 `fecha-simulada-no-permitida` en otro entorno). Cada llamada avanza a lo sumo una transición por vínculo (aviso, vencimiento o fin de bloqueo).

**Operador de Plataforma · API (`/api/v1`) · SDK: `revisarVigenciasDeVinculos`**

**Cuerpo de la petición** (`application/json`, RevisarVigenciasDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tenantId` | uuid o null | no |  |
| `fecha` | fecha y hora o null | no |  |
| `incluirDocumentos` | booleano | no | por omisión false |

**Respuestas**

- `200` OK — ResultadoRevisionVigenciasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `revisados` | entero | sí |  |
| `avisados` | entero | sí |  |
| `vencidos` | entero | sí |  |
| `desbloqueados` | entero | sí |  |
| `documentosVencidos` | entero | no | por omisión 0 |

**Errores de negocio que lanza**

- `422` `fecha-requerida`
- `422` `fecha-simulada-no-permitida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/vigencias-de-vinculos/revisar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","fecha":"2026-11-12T15:00:00Z","incluirDocumentos":false}'
```

Respuesta `200`:

```json
{
  "revisados": 1,
  "avisados": 1,
  "vencidos": 1,
  "desbloqueados": 1,
  "documentosVencidos": 0
}
```

</details>

## Vínculos

Vínculos de la empresa activa (cabecera `X-Empresa`). Cada vínculo es un stream de eventos `Vinculo-{id}`; la lectura sale de su proyección inline.

### `GET /api/v1/vinculos`

**Listar vínculos.** Vínculos de la empresa, del más antiguo al más reciente. `estado` filtra por el estado del dominio (no cambia nunca por una actualización); `enRevision` (VR-1) por la situación derivada «en revisión por una actualización»: `true` solo los que tienen una actualización abierta, `false` excluye esos (así «Activo» en pantalla no repite lo que se ve «En revisión»).

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarVinculos`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `estado` | query | texto | no |  |
| `tipoVinculo` | query | texto | no |  |
| `terceroId` | query | uuid | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |
| `enRevision` | query | booleano | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de VinculoDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].empresaId` | uuid | sí |  |
| `datos[].terceroId` | uuid | sí |  |
| `datos[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `datos[].tipoVinculo.id` | uuid | sí |  |
| `datos[].tipoVinculo.codigo` | texto | sí |  |
| `datos[].tipoVinculo.nombre` | texto | sí |  |
| `datos[].flujoId` | uuid o null | no |  |
| `datos[].estado` | EstadoVinculo (texto) | sí | valores: Invitado · EnValidacion · Activo · PorVencer · Vencido · Bloqueado · Desvinculado · Anulado |
| `datos[].atributos` | objeto libre | sí |  |
| `datos[].correoTercero` | texto | sí |  |
| `datos[].invitacion` | EnlaceInvitacionDto o null | no |  |
| `datos[].invitacion.correoDestino` | texto | sí |  |
| `datos[].invitacion.expira` | fecha y hora | sí |  |
| `datos[].invitacion.envios` | entero | sí |  |
| `datos[].invitacionAceptadaEn` | fecha y hora o null | no |  |
| `datos[].requisitosCubribles` | entero o null | no |  |
| `datos[].bloqueo` | BloqueoDto o null | no |  |
| `datos[].bloqueo.motivo` | texto | sí |  |
| `datos[].bloqueo.desde` | fecha y hora | sí |  |
| `datos[].bloqueo.hasta` | fecha y hora o null | no |  |
| `datos[].referencias` | lista de ReferenciaExternaDto | sí |  |
| `datos[].referencias[].sistema` | texto | sí |  |
| `datos[].referencias[].id` | texto | sí |  |
| `datos[].origen` | OrigenVinculo (texto) | sí | valores: Invitacion · Autorregistro · Migracion · Api |
| `datos[].creadoEn` | fecha y hora | sí |  |
| `datos[].actualizadoEn` | fecha y hora | sí |  |
| `datos[].version` | entero | sí |  |
| `datos[].expedienteAbiertoId` | uuid o null | no |  |
| `datos[].aperturaDeExpedientePendiente` | booleano | no | por omisión false |
| `datos[].ultimoResultado` | texto o null | no |  |
| `datos[].contactos` | lista de ContactoDeEmpresaDto o null | no |  |
| `datos[].contactos[].nombre` | texto | sí |  |
| `datos[].contactos[].cargo` | texto o null | no |  |
| `datos[].contactos[].correo` | texto | sí |  |
| `datos[].contactos[].telefono` | texto o null | no |  |
| `datos[].contactos[].rol` | texto | sí |  |
| `datos[].vigenteHasta` | fecha o null | no |  |
| `datos[].enRevision` | booleano | no | por omisión false |
| `datos[].actualizacionNoAprobada` | ActualizacionNoAprobadaDto o null | no |  |
| `datos[].actualizacionNoAprobada.expedienteId` | uuid o null | no |  |
| `datos[].actualizacionNoAprobada.resultado` | texto | sí |  |
| `datos[].actualizacionNoAprobada.fecha` | fecha y hora o null | no |  |
| `siguienteCursor` | texto o null | no |  |

**Errores de negocio que lanza**

- `422` `estado-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoVinculo": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S."
      },
      "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "estado": "Invitado",
      "atributos": {},
      "correoTercero": "contacto@empresa.co",
      "invitacion": {
        "correoDestino": "contacto@empresa.co",
        "expira": "2026-11-12T15:00:00Z",
        "envios": 1
      },
      "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
      "requisitosCubribles": 1,
      "bloqueo": {
        "motivo": "Texto de ejemplo.",
        "desde": "2026-11-12T15:00:00Z",
        "hasta": "2026-11-12T15:00:00Z"
      },
      "referencias": [
        {
          "sistema": "texto",
          "id": "texto"
        }
      ],
      "origen": "Invitacion",
      "creadoEn": "2026-11-12T15:00:00Z",
      "actualizadoEn": "2026-11-12T15:00:00Z",
      "version": 1,
      "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "aperturaDeExpedientePendiente": false,
      "ultimoResultado": "texto",
      "contactos": [
        {
          "nombre": "Constructora Norte S.A.S.",
          "cargo": "texto",
          "correo": "contacto@empresa.co",
          "telefono": "+57 601 555 1234",
          "rol": "texto"
        }
      ],
      "vigenteHasta": "2026-11-12",
      "enRevision": false,
      "actualizacionNoAprobada": {
        "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "resultado": "texto",
        "fecha": "2026-11-12T15:00:00Z"
      }
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `POST /api/v1/vinculos`

**Crear vínculo.** Crea el vínculo (201). Si la empresa ya tiene ese `(tercero, tipo)` y lo pide **otro** sistema integrado, le agrega su referencia y responde 200 con el vínculo existente: una sola validación compartida (I-0). La misma referencia es idempotente (200); el mismo sistema con otro id es `referencia-en-conflicto`; sin referencia, `vinculo-duplicado`.

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `crearVinculo`**

**Cuerpo de la petición** (`application/json`, CrearVinculoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `terceroId` | uuid o null | no |  |
| `tercero` | CrearTerceroDto o null | no |  |
| `tercero.identificacion` | IdentificacionDto | sí |  |
| `tercero.identificacion.pais` | texto | sí |  |
| `tercero.identificacion.tipo` | texto | sí |  |
| `tercero.identificacion.numero` | texto | sí |  |
| `tercero.identificacion.digitoVerificacion` | texto o null | no |  |
| `tercero.tipoPersona` | texto | sí |  |
| `tercero.nombre` | texto | sí |  |
| `tercero.nombreComercial` | texto o null | no |  |
| `tercero.contactos` | lista de ContactoDto o null | no |  |
| `tercero.contactos[].nombre` | texto | sí |  |
| `tercero.contactos[].correo` | texto | sí |  |
| `tercero.contactos[].telefono` | texto o null | no |  |
| `tercero.contactos[].rol` | texto | sí |  |
| `tercero.referenciaExterna` | ReferenciaExternaDto o null | no |  |
| `tercero.referenciaExterna.sistema` | texto | sí |  |
| `tercero.referenciaExterna.id` | texto | sí |  |
| `tipoVinculo` | texto | sí |  |
| `atributos` | objeto libre o null | no |  |
| `correoInvitacion` | texto | sí |  |
| `referenciaExterna` | ReferenciaExternaDto o null | no | mismos campos que `tercero.referenciaExterna` |
| `enviarInvitacion` | booleano | no | por omisión true |
| `vigenciaInvitacionHoras` | entero | no | por omisión 72 |
| `flujoId` | uuid o null | no |  |

**Respuestas**

- `200` OK — VinculoDto
- `201` Creado — VinculoDto

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

Campos de la respuesta `201`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

**Errores de negocio que lanza**

- `422` `tipo-vinculo-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"terceroId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","tercero":{"identificacion":{"pais":"CO","tipo":"texto","numero":"900123456","digitoVerificacion":"texto"},"tipoPersona":"texto","nombre":"Constructora Norte S.A.S.","nombreComercial":"Constructora Norte S.A.S.","contactos":[{"nombre":"Constructora Norte S.A.S.","correo":"contacto@empresa.co","telefono":"+57 601 555 1234","rol":"texto"}],"referenciaExterna":{"sistema":"texto","id":"texto"}},"tipoVinculo":"texto","atributos":{},"correoInvitacion":"contacto@empresa.co","referenciaExterna":{"sistema":"texto","id":"texto"},"enviarInvitacion":true,"vigenciaInvitacionHoras":72,"flujoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `GET /api/v1/vinculos/{id}`

**Obtener vínculo.**

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `POST /api/v1/vinculos/{id}/anular`

**Anular vínculo.** Terminal; si tenía un expediente abierto, se anula en la misma transacción; su certificado se revoca (por el sistema).

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `anularVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, MotivoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `motivo` | texto | sí |  |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/anular" \
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
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `PATCH /api/v1/vinculos/{id}/atributos`

**Actualizar atributos vínculo.**

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarAtributosVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarAtributosDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `atributos` | objeto libre | sí |  |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PATCH "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/atributos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"atributos":{}}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `POST /api/v1/vinculos/{id}/bloquear`

**Bloquear vínculo.** Bloquear suspende el certificado en la misma transacción (B-5); no lo revoca. Con fecha de fin programa en la misma transacción el desbloqueo automático (`DesbloquearAlLlegarLaFecha`, VV-5), que hace lo mismo que el manual.

**Permiso vinculos:bloquear · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `bloquearVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, BloquearVinculoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `motivo` | texto | sí |  |
| `hasta` | fecha y hora o null | no |  |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/bloquear" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"motivo":"Texto de ejemplo.","hasta":"2026-11-12T15:00:00Z"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `POST /api/v1/vinculos/{id}/desbloquear`

**Desbloquear vínculo.** Desbloquear restablece el certificado suspendido (o lo deja vencido si su vigencia pasó durante el bloqueo) y pone al día al vínculo con lo que pasó mientras estuvo congelado (PP-9): en la misma transacción pide restituirle la compartición con la versión actual y abrir la actualización si lo aprobado difiere de ella (`RevalidarVinculoDesbloqueado`).

**Permiso vinculos:bloquear · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `desbloquearVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, JustificacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/desbloquear" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"justificacion":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `POST /api/v1/vinculos/{id}/desvincular`

**Desvincular vínculo.** Si tenía un expediente abierto, se anula en la misma transacción; su certificado se revoca (por el sistema).

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `desvincularVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, MotivoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `motivo` | texto | sí |  |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/desvincular" \
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
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `GET /api/v1/vinculos/{id}/historial`

**Obtener historial vínculo.** Línea de tiempo del vínculo, directamente del stream de eventos.

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerHistorialVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — lista de EventoHistorialDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de EventoHistorialDto | sí |  |
| `[].version` | entero | sí |  |
| `[].tipo` | texto | sí |  |
| `[].fecha` | fecha y hora | sí |  |
| `[].actorTipo` | texto o null | no |  |
| `[].actorId` | texto o null | no |  |
| `[].sistema` | texto o null | no |  |
| `[].datos` | objeto libre | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/historial" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
  {
    "version": 1,
    "tipo": "texto",
    "fecha": "2026-11-12T15:00:00Z",
    "actorTipo": "texto",
    "actorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "sistema": "texto",
    "datos": {}
  }
]
```

</details>

### `POST /api/v1/vinculos/{id}/invitaciones`

**Invitar vínculo.** Envía o reenvía la invitación: genera un enlace de acceso nuevo (el anterior no usado deja de servir) y el correo sale por el outbox después de confirmar la transacción.

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `invitarVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, InvitacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correoDestino` | texto o null | no |  |
| `vigenciaHoras` | entero | no | por omisión 72 |

**Respuestas**

- `201` Creado — EnlaceInvitacionDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correoDestino` | texto | sí |  |
| `expira` | fecha y hora | sí |  |
| `envios` | entero | sí |  |

**Errores de negocio que lanza**

- `404` `tipo-vinculo-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/invitaciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"correoDestino":"contacto@empresa.co","vigenciaHoras":72}'
```

Respuesta `201`:

```json
{
  "correoDestino": "contacto@empresa.co",
  "expira": "2026-11-12T15:00:00Z",
  "envios": 1
}
```

</details>

### `POST /api/v1/vinculos/{id}/reintegrar`

**Reintegrar vínculo.** Reintegra un vínculo desvinculado (DS-3). Por omisión **revalida**: vuelve a validación y se abre una renovación con todos los documentos del flujo (si nunca fue aprobado, vuelve a `Invitado` para invitarlo de nuevo). Con `revalidar: false` restituye el estado que tenía al desvincularse y emite su certificado de nuevo con lo ya aprobado, solo si esas versiones siguen vigentes (409 `reintegro-requiere-revalidacion` con los motivos si no).

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `reintegrarVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ReintegrarVinculoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto | sí |  |
| `revalidar` | booleano | no | por omisión true |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

**Errores de negocio que lanza**

- `409` `reintegro-requiere-revalidacion`
- `422` `falta-flujo-por-defecto`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/reintegrar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"justificacion":"Texto de ejemplo.","revalidar":true}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `GET /api/v1/vinculos/{id}/reintegro`

**Obtener viabilidad de reintegro.** Si el vínculo desvinculado se puede reintegrar sin revalidar (DS-3) y, si no, por qué: una versión cambió en la bóveda del tercero sin que la empresa la revisara, una aprobada ya venció o el vínculo no estaba aprobado y vigente al desvincularse.

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerViabilidadDeReintegro`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — ViabilidadDeReintegroDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `puedeSinRevalidar` | booleano | sí |  |
| `motivos` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/reintegro" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "puedeSinRevalidar": true,
  "motivos": [
    "Texto de ejemplo."
  ]
}
```

</details>

### `GET /api/v1/vinculos/por-referencia`

**Obtener vínculo por referencia.** Busca el vínculo de la empresa activa por la clave externa de un sistema integrador: `?sistema=SRM&clave=ADP-00123` (`id` es el nombre anterior del mismo parámetro y se sigue aceptando).

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerVinculoPorReferencia`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `sistema` | query | texto | no |  |
| `clave` | query | texto | no |  |
| `id` | query | texto | no |  |

**Respuestas**

- `200` OK — VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `VinculoDto` que se detallan en `PUT /api/v1/vinculos/{id}/contactos`.

**Errores de negocio que lanza**

- `404` `vinculo-no-encontrado`
- `422` `referencia-externa-invalida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/por-referencia" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "flujoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "estado": "Invitado",
  "atributos": {},
  "correoTercero": "contacto@empresa.co",
  "invitacion": {
    "correoDestino": "contacto@empresa.co",
    "expira": "2026-11-12T15:00:00Z",
    "envios": 1
  },
  "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
  "requisitosCubribles": 1,
  "bloqueo": {
    "motivo": "Texto de ejemplo.",
    "desde": "2026-11-12T15:00:00Z",
    "hasta": "2026-11-12T15:00:00Z"
  },
  "referencias": [
    {
      "sistema": "texto",
      "id": "texto"
    }
  ],
  "origen": "Invitacion",
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "expedienteAbiertoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "aperturaDeExpedientePendiente": false,
  "ultimoResultado": "texto",
  "contactos": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "cargo": "texto",
      "correo": "contacto@empresa.co",
      "telefono": "+57 601 555 1234",
      "rol": "texto"
    }
  ],
  "vigenteHasta": "2026-11-12",
  "enRevision": false,
  "actualizacionNoAprobada": {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "resultado": "texto",
    "fecha": "2026-11-12T15:00:00Z"
  }
}
```

</details>
