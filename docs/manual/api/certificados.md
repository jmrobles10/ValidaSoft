# API · Certificados

7 operaciones en 3 recursos.

## Certificados

### `GET /api/v1/certificados/{folio}`

**Obtener certificado.** Folio con emisión (`…-{n}`) ⇒ esa emisión; folio raíz ⇒ la actual.

**Permiso certificados:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerCertificado`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `folio` | path | texto | sí |  |

**Respuestas**

- `200` OK — CertificadoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `folio` | texto | sí |  |
| `folioRaiz` | texto | sí |  |
| `emision` | entero | sí |  |
| `esLaActual` | booleano | sí |  |
| `estado` | texto | sí |  |
| `bloqueante` | booleano | sí |  |
| `empresa` | EmpresaDelCertificadoDto | sí |  |
| `empresa.id` | uuid | sí |  |
| `empresa.nombre` | texto | sí |  |
| `empresa.logoUrl` | texto o null | no |  |
| `vinculoId` | uuid | sí |  |
| `expedienteId` | uuid | sí |  |
| `expedienteFolio` | texto | sí |  |
| `tercero` | TitularDelCertificadoDto | sí |  |
| `tercero.nombre` | texto | sí |  |
| `tercero.identificacionEnmascarada` | texto | sí |  |
| `tercero.pais` | texto | sí |  |
| `tercero.tipoPersona` | texto | sí |  |
| `tipoVinculo` | TipoVinculoDelCertificadoDto | sí |  |
| `tipoVinculo.codigo` | texto | sí |  |
| `tipoVinculo.nombre` | texto | sí |  |
| `flujo` | FlujoDelCertificadoDto | sí |  |
| `flujo.codigo` | texto | sí |  |
| `flujo.nombre` | texto | sí |  |
| `flujo.version` | entero | sí |  |
| `emitidoEn` | fecha y hora | sí |  |
| `vigenteHasta` | fecha o null | no |  |
| `huella` | texto | sí |  |
| `hashContenido` | texto | sí |  |
| `urlVerificacion` | texto | sí |  |
| `requisitos` | lista de RequisitoDelCertificadoDto | sí |  |
| `requisitos[].codigo` | texto | sí |  |
| `requisitos[].nombre` | texto | sí |  |
| `requisitos[].version` | entero o null | no |  |
| `requisitos[].sha256` | texto o null | no |  |
| `requisitos[].vigenteHasta` | fecha o null | no |  |
| `requisitos[].eximido` | booleano | sí |  |
| `requisitos[].justificacion` | texto o null | no |  |
| `areas` | lista de AreaDelCertificadoDto | sí |  |
| `areas[].area` | texto | sí |  |
| `areas[].codigo` | texto | sí |  |
| `areas[].origen` | texto | sí |  |
| `areas[].fecha` | fecha y hora | sí |  |
| `pendientes` | lista de texto | sí |  |
| `suspension` | SuspensionDelCertificadoDto o null | no |  |
| `suspension.motivo` | texto | sí |  |
| `suspension.desde` | fecha y hora | sí |  |
| `revocacion` | RevocacionDelCertificadoDto o null | no |  |
| `revocacion.motivo` | texto | sí |  |
| `revocacion.origen` | texto | sí |  |
| `revocacion.fecha` | fecha y hora | sí |  |
| `pdf` | PdfDelCertificadoDto o null | no |  |
| `pdf.sha256` | texto | sí |  |
| `pdf.tamano` | entero | sí |  |
| `pdf.generadoEn` | fecha y hora | sí |  |
| `descargas` | entero | sí |  |
| `firma` | FirmaDelCertificadoDto | sí |  |
| `firma.algoritmo` | texto | sí |  |
| `firma.kid` | texto | sí |  |
| `firma.clavePublicaUrl` | texto | sí |  |
| `firma.valor` | texto | sí |  |
| `contenidoCanonico` | JsonElement | sí |  |
| `version` | entero | sí |  |
| `enVerificacion` | booleano | no | por omisión false |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/certificados/texto" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
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
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S.",
      "version": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "vigenteHasta": "2026-11-12",
      "eximido": true,
      "justificacion": "Texto de ejemplo."
    }
  ],
  "areas": [
    {
      "area": "texto",
      "codigo": "CODIGO_EJEMPLO",
      "origen": "texto",
      "fecha": "2026-11-12T15:00:00Z"
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
}
```

</details>

### `POST /api/v1/certificados/{folio}/descarga`

**Descargar certificado.** URL prefirmada del PDF de la emisión (5 min, `attachment`). Cada entrega queda en el stream del certificado (`CertificadoDescargado` con actor, IP y user-agent). Si el PDF aún no se generó, se genera aquí.

**Permiso certificados:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `descargarCertificado`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `folio` | path | texto | sí |  |

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

**Errores de negocio que lanza**

- `409` `pdf-pendiente`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/certificados/texto/descarga" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
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

### `POST /api/v1/certificados/{folio}/revocar`

**Revocar certificado.** Revocación por decisión humana (fraude confirmado, error de emisión): `expedientes:administrar` y justificación. Irreversible; solo la emisión actual (una reemplazada ⇒ 409 `emision-no-actual`).

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `revocarCertificado`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `folio` | path | texto | sí |  |

**Cuerpo de la petición** (`application/json`, RevocarCertificadoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — CertificadoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `CertificadoDto` que se detallan en `GET /api/v1/certificados/{folio}`.

**Errores de negocio que lanza**

- `409` `emision-no-actual`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/certificados/texto/revocar" \
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
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S.",
      "version": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "vigenteHasta": "2026-11-12",
      "eximido": true,
      "justificacion": "Texto de ejemplo."
    }
  ],
  "areas": [
    {
      "area": "texto",
      "codigo": "CODIGO_EJEMPLO",
      "origen": "texto",
      "fecha": "2026-11-12T15:00:00Z"
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
}
```

</details>

### `GET /api/v1/vinculos/{id}/certificado`

**Obtener certificado vínculo.** Emisión actual (en el estado que tenga) e historial; sin certificado (nunca aprobado) ⇒ `actual: null`.

**Permiso certificados:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerCertificadoVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CertificadoDelVinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `vinculoId` | uuid | sí |  |
| `actual` | CertificadoDto o null | no |  |
| `actual.id` | uuid | sí |  |
| `actual.folio` | texto | sí |  |
| `actual.folioRaiz` | texto | sí |  |
| `actual.emision` | entero | sí |  |
| `actual.esLaActual` | booleano | sí |  |
| `actual.estado` | texto | sí |  |
| `actual.bloqueante` | booleano | sí |  |
| `actual.empresa` | EmpresaDelCertificadoDto | sí |  |
| `actual.empresa.id` | uuid | sí |  |
| `actual.empresa.nombre` | texto | sí |  |
| `actual.empresa.logoUrl` | texto o null | no |  |
| `actual.vinculoId` | uuid | sí |  |
| `actual.expedienteId` | uuid | sí |  |
| `actual.expedienteFolio` | texto | sí |  |
| `actual.tercero` | TitularDelCertificadoDto | sí |  |
| `actual.tercero.nombre` | texto | sí |  |
| `actual.tercero.identificacionEnmascarada` | texto | sí |  |
| `actual.tercero.pais` | texto | sí |  |
| `actual.tercero.tipoPersona` | texto | sí |  |
| `actual.tipoVinculo` | TipoVinculoDelCertificadoDto | sí |  |
| `actual.tipoVinculo.codigo` | texto | sí |  |
| `actual.tipoVinculo.nombre` | texto | sí |  |
| `actual.flujo` | FlujoDelCertificadoDto | sí |  |
| `actual.flujo.codigo` | texto | sí |  |
| `actual.flujo.nombre` | texto | sí |  |
| `actual.flujo.version` | entero | sí |  |
| `actual.emitidoEn` | fecha y hora | sí |  |
| `actual.vigenteHasta` | fecha o null | no |  |
| `actual.huella` | texto | sí |  |
| `actual.hashContenido` | texto | sí |  |
| `actual.urlVerificacion` | texto | sí |  |
| `actual.requisitos` | lista de RequisitoDelCertificadoDto | sí |  |
| `actual.requisitos[].codigo` | texto | sí |  |
| `actual.requisitos[].nombre` | texto | sí |  |
| `actual.requisitos[].version` | entero o null | no |  |
| `actual.requisitos[].sha256` | texto o null | no |  |
| `actual.requisitos[].vigenteHasta` | fecha o null | no |  |
| `actual.requisitos[].eximido` | booleano | sí |  |
| `actual.requisitos[].justificacion` | texto o null | no |  |
| `actual.areas` | lista de AreaDelCertificadoDto | sí |  |
| `actual.areas[].area` | texto | sí |  |
| `actual.areas[].codigo` | texto | sí |  |
| `actual.areas[].origen` | texto | sí |  |
| `actual.areas[].fecha` | fecha y hora | sí |  |
| `actual.pendientes` | lista de texto | sí |  |
| `actual.suspension` | SuspensionDelCertificadoDto o null | no |  |
| `actual.suspension.motivo` | texto | sí |  |
| `actual.suspension.desde` | fecha y hora | sí |  |
| `actual.revocacion` | RevocacionDelCertificadoDto o null | no |  |
| `actual.revocacion.motivo` | texto | sí |  |
| `actual.revocacion.origen` | texto | sí |  |
| `actual.revocacion.fecha` | fecha y hora | sí |  |
| `actual.pdf` | PdfDelCertificadoDto o null | no |  |
| `actual.pdf.sha256` | texto | sí |  |
| `actual.pdf.tamano` | entero | sí |  |
| `actual.pdf.generadoEn` | fecha y hora | sí |  |
| `actual.descargas` | entero | sí |  |
| `actual.firma` | FirmaDelCertificadoDto | sí |  |
| `actual.firma.algoritmo` | texto | sí |  |
| `actual.firma.kid` | texto | sí |  |
| `actual.firma.clavePublicaUrl` | texto | sí |  |
| `actual.firma.valor` | texto | sí |  |
| `actual.contenidoCanonico` | JsonElement | sí |  |
| `actual.version` | entero | sí |  |
| `actual.enVerificacion` | booleano | no | por omisión false |
| `emisiones` | lista de EmisionDelCertificadoDto | sí |  |
| `emisiones[].folio` | texto | sí |  |
| `emisiones[].emision` | entero | sí |  |
| `emisiones[].estado` | texto | sí |  |
| `emisiones[].emitidoEn` | fecha y hora | sí |  |
| `emisiones[].vigenteHasta` | fecha o null | no |  |
| `emisiones[].huella` | texto | sí |  |
| `emisiones[].expedienteFolio` | texto | sí |  |
| `emisiones[].pdfDisponible` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/certificado" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
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
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "version": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "vigenteHasta": "2026-11-12",
        "eximido": true,
        "justificacion": "Texto de ejemplo."
      }
    ],
    "areas": [
      {
        "area": "texto",
        "codigo": "CODIGO_EJEMPLO",
        "origen": "texto",
        "fecha": "2026-11-12T15:00:00Z"
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
}
```

</details>

## Lista de certificados

Lista de certificados de la empresa activa (CE-11, docs/06 P2 «Certificados»).

### `GET /api/v1/certificados`

**Listar certificados.** `estado`: Vigente, ConPendientes, Suspendido, Vencido, Revocado o `Bloqueante` (los que la consulta de certificación responde bloqueados). `enVerificacion` (VR-11): `true` solo los vigentes que la empresa revisa de nuevo por una actualización; `false` excluye esos (así «Con pendientes» en pantalla no repite lo que se ve «En verificación»); `estado` sigue filtrando por el estado de dominio. `q`: parte del nombre o la identificación del tercero, o del folio. Ordenados por la vigencia más próxima; cursor opaco.

**Permiso certificados:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarCertificados`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `estado` | query | texto | no |  |
| `q` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |
| `enVerificacion` | query | booleano | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfFilaCertificadoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de FilaCertificadoDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].folio` | texto | sí |  |
| `datos[].emision` | entero | sí |  |
| `datos[].vinculoId` | uuid | sí |  |
| `datos[].tercero` | TerceroResumenDto | sí |  |
| `datos[].tercero.id` | uuid | sí |  |
| `datos[].tercero.nombre` | texto | sí |  |
| `datos[].tercero.identificacion` | texto | sí |  |
| `datos[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `datos[].tipoVinculo.id` | uuid | sí |  |
| `datos[].tipoVinculo.codigo` | texto | sí |  |
| `datos[].tipoVinculo.nombre` | texto | sí |  |
| `datos[].estado` | texto | sí |  |
| `datos[].bloqueante` | booleano | sí |  |
| `datos[].emitidoEn` | fecha y hora | sí |  |
| `datos[].vigenteHasta` | fecha o null | no |  |
| `datos[].estadoDesde` | fecha y hora | sí |  |
| `datos[].enVerificacion` | booleano | no | por omisión false |
| `siguienteCursor` | texto o null | no |  |

**Errores de negocio que lanza**

- `422` `estado-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/certificados" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "emision": 1,
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
      "estado": "texto",
      "bloqueante": true,
      "emitidoEn": "2026-11-12T15:00:00Z",
      "vigenteHasta": "2026-11-12",
      "estadoDesde": "2026-11-12T15:00:00Z",
      "enVerificacion": false
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

## Verificación pública

### `GET /public/v1/verificar/{folio}`

**Verificar certificado.** `CertificadoPublicoDto` de la emisión (la actual con el folio raíz). `?huella=` (12 hexadecimales, con o sin separadores) es opcional salvo que el folio exista en más de un tenant (409 `folio-ambiguo`).

**Pública (sin sesión) · Portal del tercero (`/public/v1`) · SDK: `verificarCertificado`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `folio` | path | texto | sí |  |
| `huella` | query | texto | no |  |

**Respuestas**

- `200` OK — CertificadoPublicoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `folio` | texto | sí |  |
| `emision` | entero | sí |  |
| `estado` | texto | sí |  |
| `bloqueante` | booleano | sí |  |
| `titular` | TitularPublicoDto | sí |  |
| `titular.nombre` | texto | sí |  |
| `titular.identificacionEnmascarada` | texto | sí |  |
| `emisor` | EmisorPublicoDto | sí |  |
| `emisor.empresa` | texto | sí |  |
| `emisor.logoUrl` | texto o null | no |  |
| `flujo` | FlujoDelCertificadoDto | sí |  |
| `flujo.codigo` | texto | sí |  |
| `flujo.nombre` | texto | sí |  |
| `flujo.version` | entero | sí |  |
| `emitidoEn` | fecha y hora | sí |  |
| `vigenteHasta` | fecha o null | no |  |
| `huella` | texto | sí |  |
| `hashContenido` | texto | sí |  |
| `contenidoCanonico` | JsonElement | sí |  |
| `urlVerificacion` | texto | sí |  |
| `pendientes` | lista de texto | sí |  |
| `firma` | FirmaDelCertificadoDto | sí |  |
| `firma.algoritmo` | texto | sí |  |
| `firma.kid` | texto | sí |  |
| `firma.clavePublicaUrl` | texto | sí |  |
| `firma.valor` | texto | sí |  |
| `estadoDesde` | fecha y hora | sí |  |
| `reemplazadoPor` | texto o null | no |  |
| `pdfDescargable` | booleano | sí |  |
| `enVerificacion` | booleano | no | por omisión false |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/verificar/texto"
```

Respuesta `200`:

```json
{
  "folio": "texto",
  "emision": 1,
  "estado": "texto",
  "bloqueante": true,
  "titular": {
    "nombre": "Constructora Norte S.A.S.",
    "identificacionEnmascarada": "900123456"
  },
  "emisor": {
    "empresa": "texto",
    "logoUrl": "https://ejemplo.co/recurso"
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
  "contenidoCanonico": {},
  "urlVerificacion": "https://ejemplo.co/recurso",
  "pendientes": [
    "texto"
  ],
  "firma": {
    "algoritmo": "texto",
    "kid": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "clavePublicaUrl": "https://ejemplo.co/recurso",
    "valor": "vsk_prueba_ejemplo"
  },
  "estadoDesde": "2026-11-12T15:00:00Z",
  "reemplazadoPor": "texto",
  "pdfDescargable": true,
  "enVerificacion": false
}
```

</details>

### `POST /public/v1/verificar/{folio}/descarga`

**Descargar certificado verificado.** Descarga pública del PDF solo de una emisión `Vigente` o «En verificación» (otro estado ⇒ 409 `certificado-no-vigente`): quien verifica un certificado vigente puede guardar la copia; uno suspendido, vencido, revocado o reemplazado no se redistribuye. Queda auditada como descarga pública (IP y user-agent).

**Pública (sin sesión) · Portal del tercero (`/public/v1`) · SDK: `descargarCertificadoVerificado`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `folio` | path | texto | sí |  |
| `huella` | query | texto | no |  |

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

**Errores de negocio que lanza**

- `409` `certificado-no-vigente`
- `409` `pdf-pendiente`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/verificar/texto/descarga"
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
