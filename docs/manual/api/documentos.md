# API · Documentos

24 operaciones en 8 recursos.

## Ayudas de documento

La ayuda por documento por empresa (OP-15, OP-16): cada empresa ve la de su grupo y puede escribir la suya; el tercero ve la de la empresa con la que valida (la del grupo si la empresa no escribió una). Con auditoría de configuración.

### `GET /api/v1/ayudas-de-documentos`

**Listar ayudas de documentos.**

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarAyudasDeDocumentos`**

**Respuestas**

- `200` OK — lista de AyudaDeDocumentoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de AyudaDeDocumentoDto | sí |  |
| `[].tipoDocumentoId` | uuid | sí |  |
| `[].codigo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].ayudaDelGrupo` | texto o null | no |  |
| `[].ayudaDeLaEmpresa` | texto o null | no |  |
| `[].ayudaEfectiva` | texto o null | no |  |
| `[].actualizadaEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/ayudas-de-documentos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
  {
    "tipoDocumentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "ayudaDelGrupo": "texto",
    "ayudaDeLaEmpresa": "texto",
    "ayudaEfectiva": "texto",
    "actualizadaEn": "2026-11-12T15:00:00Z"
  }
]
```

</details>

### `PUT /api/v1/ayudas-de-documentos/{tipoId}`

**Actualizar ayuda de documento.** Fija la ayuda de la empresa para un tipo; `texto` vacío la quita (vuelve a valer la del grupo). 404 `tipo-documento-no-encontrado`; 422 `texto-demasiado-largo`.

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarAyudaDeDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tipoId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarAyudaDeDocumentoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `texto` | texto o null | no |  |

**Respuestas**

- `200` OK — AyudaDeDocumentoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tipoDocumentoId` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `ayudaDelGrupo` | texto o null | no |  |
| `ayudaDeLaEmpresa` | texto o null | no |  |
| `ayudaEfectiva` | texto o null | no |  |
| `actualizadaEn` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `404` `tipo-documento-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/ayudas-de-documentos/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"texto":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "tipoDocumentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "ayudaDelGrupo": "texto",
  "ayudaDeLaEmpresa": "texto",
  "ayudaEfectiva": "texto",
  "actualizadaEn": "2026-11-12T15:00:00Z"
}
```

</details>

## Bóveda

### `GET /public/v1/boveda`

**Obtener bóveda.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerBoveda`**

**Respuestas**

- `200` OK — BovedaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `terceroId` | uuid | sí |  |
| `documentos` | lista de DocumentoBovedaDto | sí |  |
| `documentos[].id` | uuid | sí |  |
| `documentos[].tipo` | TipoDocumentoResumenDto | sí |  |
| `documentos[].tipo.codigo` | texto | sí |  |
| `documentos[].tipo.nombre` | texto | sí |  |
| `documentos[].tipo.tipoDocumentoTenantId` | uuid o null | no |  |
| `documentos[].ambito` | AmbitoDocumento (texto) | sí | valores: Global · PorVinculo |
| `documentos[].clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `documentos[].vinculoId` | uuid o null | no |  |
| `documentos[].versionActual` | VersionDocumentoDto o null | no |  |
| `documentos[].versionActual.documentoId` | uuid | sí |  |
| `documentos[].versionActual.n` | entero | sí |  |
| `documentos[].versionActual.estado` | EstadoVersion (texto) | sí | valores: Recibida · EnAnalisis · Actual · Superada · Devuelta · Retirada |
| `documentos[].versionActual.cargadaEn` | fecha y hora | sí |  |
| `documentos[].versionActual.cargadaPor` | texto | sí |  |
| `documentos[].versionActual.clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `documentos[].versionActual.nombreOriginal` | texto | sí |  |
| `documentos[].versionActual.mime` | texto | sí |  |
| `documentos[].versionActual.tamanoBytes` | entero | sí |  |
| `documentos[].versionActual.paginas` | entero o null | no |  |
| `documentos[].versionActual.sha256` | texto | sí |  |
| `documentos[].versionActual.motivoDevolucion` | texto o null | no |  |
| `documentos[].versionActual.detalleDevolucion` | texto o null | no |  |
| `documentos[].versionActual.hechos` | HechosDto o null | no |  |
| `documentos[].versionActual.hechos.conIa` | booleano | sí |  |
| `documentos[].versionActual.hechos.hashVerificado` | booleano | sí |  |
| `documentos[].versionActual.hechos.antivirus` | texto | sí |  |
| `documentos[].versionActual.hechos.mimeReal` | texto | sí |  |
| `documentos[].versionActual.hechos.paginas` | entero o null | no |  |
| `documentos[].versionActual.hechos.comparacion` | texto | sí |  |
| `documentos[].versionActual.hechos.camposCambiados` | lista de texto | sí |  |
| `documentos[].versionActual.hechos.camposCriticosCambiados` | lista de texto | sí |  |
| `documentos[].versionActual.hechos.pendienteRevisionHumana` | booleano | sí |  |
| `documentos[].versionActual.hechos.ia` | HechosIaDto o null | no |  |
| `documentos[].versionActual.hechos.ia.modelo` | texto | sí |  |
| `documentos[].versionActual.hechos.ia.escalado` | booleano | sí |  |
| `documentos[].versionActual.hechos.ia.confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `documentos[].versionActual.hechos.ia.legible` | booleano | sí |  |
| `documentos[].versionActual.hechos.ia.tipoDetectado` | texto | sí |  |
| `documentos[].versionActual.hechos.ia.campos` | objeto libre | sí |  |
| `documentos[].versionActual.hechos.ia.alertas` | lista de texto | sí |  |
| `documentos[].versionActual.hechos.ia.observaciones` | texto o null | no |  |
| `documentos[].versionActual.hechos.ia.paginasEnviadas` | entero o null | no |  |
| `documentos[].versionActual.hechos.ia.paginasOmitidas` | entero o null | no |  |
| `documentos[].versionActual.hechos.ia.fecha` | fecha y hora | sí |  |
| `documentos[].versionActual.datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `documentos[].versionActual.datosDeclarados[].clave` | texto | sí |  |
| `documentos[].versionActual.datosDeclarados[].etiqueta` | texto | sí |  |
| `documentos[].versionActual.datosDeclarados[].tipo` | texto | sí |  |
| `documentos[].versionActual.datosDeclarados[].valor` | texto | sí |  |
| `documentos[].versionActual.vigencia` | VigenciaDto o null | no |  |
| `documentos[].versionActual.vigencia.vigenteHasta` | fecha o null | no |  |
| `documentos[].versionActual.vigencia.base` | texto | sí |  |
| `documentos[].versionActual.vigencia.estado` | texto | sí |  |
| `documentos[].versionActual.vigencia.diasAviso` | lista de entero | sí |  |
| `documentos[].versionActual.vigencia.diasGracia` | entero | sí |  |
| `documentos[].versionActual.vigencia.finDeGracia` | fecha o null | no |  |
| `documentos[].versionActual.vigencia.vencidaEn` | fecha y hora o null | no |  |
| `documentos[].versiones` | entero | sí |  |
| `documentos[].porEmpresa` | lista de ComparticionPublicaDto | sí |  |
| `documentos[].porEmpresa[].vinculoId` | uuid | sí |  |
| `documentos[].porEmpresa[].empresa` | EmpresaPublicaDto | sí |  |
| `documentos[].porEmpresa[].empresa.id` | uuid | sí |  |
| `documentos[].porEmpresa[].empresa.nombre` | texto | sí |  |
| `documentos[].porEmpresa[].empresa.logoUrl` | texto o null | no |  |
| `documentos[].porEmpresa[].empresa.colorMarca` | texto o null | no |  |
| `documentos[].porEmpresa[].empresa.contactoSoporte` | texto o null | no |  |
| `documentos[].porEmpresa[].compartido` | booleano | sí |  |
| `documentos[].porEmpresa[].version` | entero o null | no |  |
| `documentos[].porEmpresa[].versionActualCompartida` | booleano | sí |  |
| `documentos[].porEmpresa[].compartidaEn` | fecha y hora o null | no |  |
| `documentos[].porEmpresa[].vigencia` | VigenciaDto o null | no | mismos campos que `documentos[].versionActual.vigencia` |
| `documentos[].porEmpresa[].situacion` | texto | no | por omisión "NoRecibe" |
| `documentos[].porEmpresa[].otraVersion` | entero o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/boveda" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "documentos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": {
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoDocumentoTenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
      },
      "ambito": "Global",
      "clase": "Archivo",
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "estado": "Recibida",
        "cargadaEn": "2026-11-12T15:00:00Z",
        "cargadaPor": "texto",
        "clase": "Archivo",
        "nombreOriginal": "Nombre de ejemplo",
        "mime": "texto",
        "tamanoBytes": 1,
        "paginas": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "motivoDevolucion": "Texto de ejemplo.",
        "detalleDevolucion": "Texto de ejemplo.",
        "hechos": {
          "conIa": true,
          "hashVerificado": true,
          "antivirus": "texto",
          "mimeReal": "texto",
          "paginas": 1,
          "comparacion": "texto",
          "camposCambiados": [
            "texto"
          ],
          "camposCriticosCambiados": [
            "texto"
          ],
          "pendienteRevisionHumana": true,
          "ia": {
            "modelo": "texto",
            "escalado": true,
            "confianza": 1,
            "legible": true,
            "tipoDetectado": "texto",
            "campos": {},
            "alertas": [
              null
            ],
            "observaciones": "Texto de ejemplo.",
            "paginasEnviadas": 1,
            "paginasOmitidas": 1,
            "fecha": "2026-11-12T15:00:00Z"
          }
        },
        "datosDeclarados": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "valor": "vsk_prueba_ejemplo"
          }
        ],
        "vigencia": {
          "vigenteHasta": "2026-11-12",
          "base": "texto",
          "estado": "texto",
          "diasAviso": [
            1
          ],
          "diasGracia": 1,
          "finDeGracia": "2026-11-12",
          "vencidaEn": "2026-11-12T15:00:00Z"
        }
      },
      "versiones": 1,
      "porEmpresa": [
        {
          "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "empresa": {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "logoUrl": "https://ejemplo.co/recurso",
            "colorMarca": "texto",
            "contactoSoporte": "texto"
          },
          "compartido": true,
          "version": 1,
          "versionActualCompartida": true,
          "compartidaEn": "2026-11-12T15:00:00Z",
          "vigencia": {
            "vigenteHasta": "2026-11-12",
            "base": "texto",
            "estado": "texto",
            "diasAviso": [
              null
            ],
            "diasGracia": 1,
            "finDeGracia": "2026-11-12",
            "vencidaEn": "2026-11-12T15:00:00Z"
          },
          "situacion": "NoRecibe",
          "otraVersion": 1
        }
      ]
    }
  ]
}
```

</details>

### `GET /public/v1/boveda/documentos/{documentoId}`

**Obtener documento bóveda.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerDocumentoBoveda`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `documentoId` | path | uuid | sí |  |

**Respuestas**

- `200` OK — DocumentoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `terceroId` | uuid | sí |  |
| `tipo` | TipoDocumentoResumenDto | sí |  |
| `tipo.codigo` | texto | sí |  |
| `tipo.nombre` | texto | sí |  |
| `tipo.tipoDocumentoTenantId` | uuid o null | no |  |
| `clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `ambito` | AmbitoDocumento (texto) | sí | valores: Global · PorVinculo |
| `vinculoId` | uuid o null | no |  |
| `versionActual` | entero o null | no |  |
| `versiones` | lista de VersionDocumentoDto | sí |  |
| `versiones[].documentoId` | uuid | sí |  |
| `versiones[].n` | entero | sí |  |
| `versiones[].estado` | EstadoVersion (texto) | sí | valores: Recibida · EnAnalisis · Actual · Superada · Devuelta · Retirada |
| `versiones[].cargadaEn` | fecha y hora | sí |  |
| `versiones[].cargadaPor` | texto | sí |  |
| `versiones[].clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `versiones[].nombreOriginal` | texto | sí |  |
| `versiones[].mime` | texto | sí |  |
| `versiones[].tamanoBytes` | entero | sí |  |
| `versiones[].paginas` | entero o null | no |  |
| `versiones[].sha256` | texto | sí |  |
| `versiones[].motivoDevolucion` | texto o null | no |  |
| `versiones[].detalleDevolucion` | texto o null | no |  |
| `versiones[].hechos` | HechosDto o null | no |  |
| `versiones[].hechos.conIa` | booleano | sí |  |
| `versiones[].hechos.hashVerificado` | booleano | sí |  |
| `versiones[].hechos.antivirus` | texto | sí |  |
| `versiones[].hechos.mimeReal` | texto | sí |  |
| `versiones[].hechos.paginas` | entero o null | no |  |
| `versiones[].hechos.comparacion` | texto | sí |  |
| `versiones[].hechos.camposCambiados` | lista de texto | sí |  |
| `versiones[].hechos.camposCriticosCambiados` | lista de texto | sí |  |
| `versiones[].hechos.pendienteRevisionHumana` | booleano | sí |  |
| `versiones[].hechos.ia` | HechosIaDto o null | no |  |
| `versiones[].hechos.ia.modelo` | texto | sí |  |
| `versiones[].hechos.ia.escalado` | booleano | sí |  |
| `versiones[].hechos.ia.confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `versiones[].hechos.ia.legible` | booleano | sí |  |
| `versiones[].hechos.ia.tipoDetectado` | texto | sí |  |
| `versiones[].hechos.ia.campos` | objeto libre | sí |  |
| `versiones[].hechos.ia.alertas` | lista de texto | sí |  |
| `versiones[].hechos.ia.observaciones` | texto o null | no |  |
| `versiones[].hechos.ia.paginasEnviadas` | entero o null | no |  |
| `versiones[].hechos.ia.paginasOmitidas` | entero o null | no |  |
| `versiones[].hechos.ia.fecha` | fecha y hora | sí |  |
| `versiones[].datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `versiones[].datosDeclarados[].clave` | texto | sí |  |
| `versiones[].datosDeclarados[].etiqueta` | texto | sí |  |
| `versiones[].datosDeclarados[].tipo` | texto | sí |  |
| `versiones[].datosDeclarados[].valor` | texto | sí |  |
| `versiones[].vigencia` | VigenciaDto o null | no |  |
| `versiones[].vigencia.vigenteHasta` | fecha o null | no |  |
| `versiones[].vigencia.base` | texto | sí |  |
| `versiones[].vigencia.estado` | texto | sí |  |
| `versiones[].vigencia.diasAviso` | lista de entero | sí |  |
| `versiones[].vigencia.diasGracia` | entero | sí |  |
| `versiones[].vigencia.finDeGracia` | fecha o null | no |  |
| `versiones[].vigencia.vencidaEn` | fecha y hora o null | no |  |
| `porEmpresa` | lista de ComparticionPublicaDto | sí |  |
| `porEmpresa[].vinculoId` | uuid | sí |  |
| `porEmpresa[].empresa` | EmpresaPublicaDto | sí |  |
| `porEmpresa[].empresa.id` | uuid | sí |  |
| `porEmpresa[].empresa.nombre` | texto | sí |  |
| `porEmpresa[].empresa.logoUrl` | texto o null | no |  |
| `porEmpresa[].empresa.colorMarca` | texto o null | no |  |
| `porEmpresa[].empresa.contactoSoporte` | texto o null | no |  |
| `porEmpresa[].compartido` | booleano | sí |  |
| `porEmpresa[].version` | entero o null | no |  |
| `porEmpresa[].versionActualCompartida` | booleano | sí |  |
| `porEmpresa[].compartidaEn` | fecha y hora o null | no |  |
| `porEmpresa[].vigencia` | VigenciaDto o null | no | mismos campos que `versiones[].vigencia` |
| `porEmpresa[].situacion` | texto | no | por omisión "NoRecibe" |
| `porEmpresa[].otraVersion` | entero o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/boveda/documentos/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipo": {
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "tipoDocumentoTenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  },
  "clase": "Archivo",
  "ambito": "Global",
  "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "versionActual": 1,
  "versiones": [
    {
      "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "n": 1,
      "estado": "Recibida",
      "cargadaEn": "2026-11-12T15:00:00Z",
      "cargadaPor": "texto",
      "clase": "Archivo",
      "nombreOriginal": "Nombre de ejemplo",
      "mime": "texto",
      "tamanoBytes": 1,
      "paginas": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "motivoDevolucion": "Texto de ejemplo.",
      "detalleDevolucion": "Texto de ejemplo.",
      "hechos": {
        "conIa": true,
        "hashVerificado": true,
        "antivirus": "texto",
        "mimeReal": "texto",
        "paginas": 1,
        "comparacion": "texto",
        "camposCambiados": [
          "texto"
        ],
        "camposCriticosCambiados": [
          "texto"
        ],
        "pendienteRevisionHumana": true,
        "ia": {
          "modelo": "texto",
          "escalado": true,
          "confianza": 1,
          "legible": true,
          "tipoDetectado": "texto",
          "campos": {},
          "alertas": [
            "texto"
          ],
          "observaciones": "Texto de ejemplo.",
          "paginasEnviadas": 1,
          "paginasOmitidas": 1,
          "fecha": "2026-11-12T15:00:00Z"
        }
      },
      "datosDeclarados": [
        {
          "clave": "texto",
          "etiqueta": "texto",
          "tipo": "texto",
          "valor": "vsk_prueba_ejemplo"
        }
      ],
      "vigencia": {
        "vigenteHasta": "2026-11-12",
        "base": "texto",
        "estado": "texto",
        "diasAviso": [
          1
        ],
        "diasGracia": 1,
        "finDeGracia": "2026-11-12",
        "vencidaEn": "2026-11-12T15:00:00Z"
      }
    }
  ],
  "porEmpresa": [
    {
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresa": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "logoUrl": "https://ejemplo.co/recurso",
        "colorMarca": "texto",
        "contactoSoporte": "texto"
      },
      "compartido": true,
      "version": 1,
      "versionActualCompartida": true,
      "compartidaEn": "2026-11-12T15:00:00Z",
      "vigencia": {
        "vigenteHasta": "2026-11-12",
        "base": "texto",
        "estado": "texto",
        "diasAviso": [
          1
        ],
        "diasGracia": 1,
        "finDeGracia": "2026-11-12",
        "vencidaEn": "2026-11-12T15:00:00Z"
      },
      "situacion": "NoRecibe",
      "otraVersion": 1
    }
  ]
}
```

</details>

### `POST /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/descarga`

**Descargar documento bóveda.** URL prefirmada GET de una versión propia; queda como `DocumentoAccedido` con el tercero como actor.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `descargarDocumentoBoveda`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `documentoId` | path | uuid | sí |  |
| `numero` | path | entero | sí |  |

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
curl -X POST "https://{host}/public/v1/boveda/documentos/3fa85f64-5717-4562-b3fc-2c963f66afa6/versiones/1/descarga" \
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

### `POST /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/retirar`

**Retirar version documento.** El tercero retira una versión (no se borra si alguna empresa la recibió o la descargó, docs/02 §10.3). Si era la actual vuelve la anterior, se comparte con quien corresponda y los expedientes abiertos que la tenían la reciben de vuelta (RQ-2). Devuelta o retirada ⇒ 409 `estado-invalido`.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `retirarVersionDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `documentoId` | path | uuid | sí |  |
| `numero` | path | entero | sí |  |

**Cuerpo de la petición** (`application/json`, RetirarVersionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `motivo` | texto | sí |  |

**Respuestas**

- `200` OK — RetiroDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `documento` | DocumentoDto | sí |  |
| `documento.id` | uuid | sí |  |
| `documento.terceroId` | uuid | sí |  |
| `documento.tipo` | TipoDocumentoResumenDto | sí |  |
| `documento.tipo.codigo` | texto | sí |  |
| `documento.tipo.nombre` | texto | sí |  |
| `documento.tipo.tipoDocumentoTenantId` | uuid o null | no |  |
| `documento.clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `documento.ambito` | AmbitoDocumento (texto) | sí | valores: Global · PorVinculo |
| `documento.vinculoId` | uuid o null | no |  |
| `documento.versionActual` | entero o null | no |  |
| `documento.versiones` | lista de VersionDocumentoDto | sí |  |
| `documento.versiones[].documentoId` | uuid | sí |  |
| `documento.versiones[].n` | entero | sí |  |
| `documento.versiones[].estado` | EstadoVersion (texto) | sí | valores: Recibida · EnAnalisis · Actual · Superada · Devuelta · Retirada |
| `documento.versiones[].cargadaEn` | fecha y hora | sí |  |
| `documento.versiones[].cargadaPor` | texto | sí |  |
| `documento.versiones[].clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `documento.versiones[].nombreOriginal` | texto | sí |  |
| `documento.versiones[].mime` | texto | sí |  |
| `documento.versiones[].tamanoBytes` | entero | sí |  |
| `documento.versiones[].paginas` | entero o null | no |  |
| `documento.versiones[].sha256` | texto | sí |  |
| `documento.versiones[].motivoDevolucion` | texto o null | no |  |
| `documento.versiones[].detalleDevolucion` | texto o null | no |  |
| `documento.versiones[].hechos` | HechosDto o null | no |  |
| `documento.versiones[].hechos.conIa` | booleano | sí |  |
| `documento.versiones[].hechos.hashVerificado` | booleano | sí |  |
| `documento.versiones[].hechos.antivirus` | texto | sí |  |
| `documento.versiones[].hechos.mimeReal` | texto | sí |  |
| `documento.versiones[].hechos.paginas` | entero o null | no |  |
| `documento.versiones[].hechos.comparacion` | texto | sí |  |
| `documento.versiones[].hechos.camposCambiados` | lista de texto | sí |  |
| `documento.versiones[].hechos.camposCriticosCambiados` | lista de texto | sí |  |
| `documento.versiones[].hechos.pendienteRevisionHumana` | booleano | sí |  |
| `documento.versiones[].hechos.ia` | HechosIaDto o null | no |  |
| `documento.versiones[].hechos.ia.modelo` | texto | sí |  |
| `documento.versiones[].hechos.ia.escalado` | booleano | sí |  |
| `documento.versiones[].hechos.ia.confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `documento.versiones[].hechos.ia.legible` | booleano | sí |  |
| `documento.versiones[].hechos.ia.tipoDetectado` | texto | sí |  |
| `documento.versiones[].hechos.ia.campos` | objeto libre | sí |  |
| `documento.versiones[].hechos.ia.alertas` | lista de texto | sí |  |
| `documento.versiones[].hechos.ia.observaciones` | texto o null | no |  |
| `documento.versiones[].hechos.ia.paginasEnviadas` | entero o null | no |  |
| `documento.versiones[].hechos.ia.paginasOmitidas` | entero o null | no |  |
| `documento.versiones[].hechos.ia.fecha` | fecha y hora | sí |  |
| `documento.versiones[].datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `documento.versiones[].datosDeclarados[].clave` | texto | sí |  |
| `documento.versiones[].datosDeclarados[].etiqueta` | texto | sí |  |
| `documento.versiones[].datosDeclarados[].tipo` | texto | sí |  |
| `documento.versiones[].datosDeclarados[].valor` | texto | sí |  |
| `documento.versiones[].vigencia` | VigenciaDto o null | no |  |
| `documento.versiones[].vigencia.vigenteHasta` | fecha o null | no |  |
| `documento.versiones[].vigencia.base` | texto | sí |  |
| `documento.versiones[].vigencia.estado` | texto | sí |  |
| `documento.versiones[].vigencia.diasAviso` | lista de entero | sí |  |
| `documento.versiones[].vigencia.diasGracia` | entero | sí |  |
| `documento.versiones[].vigencia.finDeGracia` | fecha o null | no |  |
| `documento.versiones[].vigencia.vencidaEn` | fecha y hora o null | no |  |
| `documento.porEmpresa` | lista de ComparticionPublicaDto | sí |  |
| `documento.porEmpresa[].vinculoId` | uuid | sí |  |
| `documento.porEmpresa[].empresa` | EmpresaPublicaDto | sí |  |
| `documento.porEmpresa[].empresa.id` | uuid | sí |  |
| `documento.porEmpresa[].empresa.nombre` | texto | sí |  |
| `documento.porEmpresa[].empresa.logoUrl` | texto o null | no |  |
| `documento.porEmpresa[].empresa.colorMarca` | texto o null | no |  |
| `documento.porEmpresa[].empresa.contactoSoporte` | texto o null | no |  |
| `documento.porEmpresa[].compartido` | booleano | sí |  |
| `documento.porEmpresa[].version` | entero o null | no |  |
| `documento.porEmpresa[].versionActualCompartida` | booleano | sí |  |
| `documento.porEmpresa[].compartidaEn` | fecha y hora o null | no |  |
| `documento.porEmpresa[].vigencia` | VigenciaDto o null | no | mismos campos que `documento.versiones[].vigencia` |
| `documento.porEmpresa[].situacion` | texto | no | por omisión "NoRecibe" |
| `documento.porEmpresa[].otraVersion` | entero o null | no |  |
| `binarioEliminado` | booleano | sí |  |
| `compartidos` | lista de CompartidoDto | sí |  |
| `compartidos[].documentoId` | uuid | sí |  |
| `compartidos[].tipo` | texto | sí |  |
| `compartidos[].version` | entero | sí |  |
| `compartidos[].vinculoId` | uuid | sí |  |
| `compartidos[].empresaId` | uuid | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/boveda/documentos/3fa85f64-5717-4562-b3fc-2c963f66afa6/versiones/1/retirar" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"motivo":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "documento": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "tipo": {
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S.",
      "tipoDocumentoTenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    },
    "clase": "Archivo",
    "ambito": "Global",
    "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionActual": 1,
    "versiones": [
      {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "estado": "Recibida",
        "cargadaEn": "2026-11-12T15:00:00Z",
        "cargadaPor": "texto",
        "clase": "Archivo",
        "nombreOriginal": "Nombre de ejemplo",
        "mime": "texto",
        "tamanoBytes": 1,
        "paginas": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "motivoDevolucion": "Texto de ejemplo.",
        "detalleDevolucion": "Texto de ejemplo.",
        "hechos": {
          "conIa": true,
          "hashVerificado": true,
          "antivirus": "texto",
          "mimeReal": "texto",
          "paginas": 1,
          "comparacion": "texto",
          "camposCambiados": [
            "texto"
          ],
          "camposCriticosCambiados": [
            "texto"
          ],
          "pendienteRevisionHumana": true,
          "ia": {
            "modelo": "texto",
            "escalado": true,
            "confianza": 1,
            "legible": true,
            "tipoDetectado": "texto",
            "campos": {},
            "alertas": [
              null
            ],
            "observaciones": "Texto de ejemplo.",
            "paginasEnviadas": 1,
            "paginasOmitidas": 1,
            "fecha": "2026-11-12T15:00:00Z"
          }
        },
        "datosDeclarados": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "valor": "vsk_prueba_ejemplo"
          }
        ],
        "vigencia": {
          "vigenteHasta": "2026-11-12",
          "base": "texto",
          "estado": "texto",
          "diasAviso": [
            1
          ],
          "diasGracia": 1,
          "finDeGracia": "2026-11-12",
          "vencidaEn": "2026-11-12T15:00:00Z"
        }
      }
    ],
    "porEmpresa": [
      {
        "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "empresa": {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "nombre": "Constructora Norte S.A.S.",
          "logoUrl": "https://ejemplo.co/recurso",
          "colorMarca": "texto",
          "contactoSoporte": "texto"
        },
        "compartido": true,
        "version": 1,
        "versionActualCompartida": true,
        "compartidaEn": "2026-11-12T15:00:00Z",
        "vigencia": {
          "vigenteHasta": "2026-11-12",
          "base": "texto",
          "estado": "texto",
          "diasAviso": [
            1
          ],
          "diasGracia": 1,
          "finDeGracia": "2026-11-12",
          "vencidaEn": "2026-11-12T15:00:00Z"
        },
        "situacion": "NoRecibe",
        "otraVersion": 1
      }
    ]
  },
  "binarioEliminado": true,
  "compartidos": [
    {
      "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": "texto",
      "version": 1,
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

</details>

### `GET /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/retiro/impacto`

**Obtener impacto de retiro.** Qué pasará si el tercero retira esta versión (RT-1), antes de pedirle el motivo: qué versión queda vigente, qué empresas la recibieron, aprobaron o revisan (solo eso, nada de sus decisiones), qué expedientes abiertos cambian de versión y qué secciones de su información la tienen de soporte. Solo lectura; una versión que no se puede retirar responde lo mismo que el retiro (409 `estado-invalido`).

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerImpactoDeRetiro`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `documentoId` | path | uuid | sí |  |
| `numero` | path | entero | sí |  |

**Respuestas**

- `200` OK — ImpactoDeRetiroDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `documentoId` | uuid | sí |  |
| `numero` | entero | sí |  |
| `esLaVigente` | booleano | sí |  |
| `versionQueQuedaVigente` | entero o null | no |  |
| `empresas` | lista de EmpresaAfectadaPorRetiroDto | sí |  |
| `empresas[].vinculoId` | uuid | sí |  |
| `empresas[].empresa` | EmpresaPublicaDto | sí |  |
| `empresas[].empresa.id` | uuid | sí |  |
| `empresas[].empresa.nombre` | texto | sí |  |
| `empresas[].empresa.logoUrl` | texto o null | no |  |
| `empresas[].empresa.colorMarca` | texto o null | no |  |
| `empresas[].empresa.contactoSoporte` | texto o null | no |  |
| `empresas[].situacion` | texto | sí |  |
| `empresas[].efecto` | texto | sí |  |
| `empresas[].folio` | texto o null | no |  |
| `secciones` | lista de SeccionAfectadaPorRetiroDto | sí |  |
| `secciones[].seccion` | texto | sí |  |
| `secciones[].nombre` | texto | sí |  |
| `secciones[].periodo` | entero o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/boveda/documentos/3fa85f64-5717-4562-b3fc-2c963f66afa6/versiones/1/retiro/impacto" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "numero": 1,
  "esLaVigente": true,
  "versionQueQuedaVigente": 1,
  "empresas": [
    {
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresa": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "logoUrl": "https://ejemplo.co/recurso",
        "colorMarca": "texto",
        "contactoSoporte": "texto"
      },
      "situacion": "texto",
      "efecto": "texto",
      "folio": "texto"
    }
  ],
  "secciones": [
    {
      "seccion": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "periodo": 1
    }
  ]
}
```

</details>

## Carga

### `POST /public/v1/boveda/documentos`

**Iniciar carga documento.** Paso 1. 201 con la URL de subida; 200 `duplicado` si el SHA-256 declarado (y los datos) ya son una versión del documento; 413 si el tamaño declarado supera el máximo del tipo; 415 si el tipo declarado no está admitido para el tipo; 422 `clase-no-soportada`, `archivo-no-permitido` (tipo solo de formulario), `datos-no-permitidos` y los de validación de campos (`campo-requerido`, `valor-invalido`, `campo-desconocido`).

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `iniciarCargaDocumento`**

**Cuerpo de la petición** (`application/json`, IniciarCargaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombreArchivo` | texto | sí |  |
| `tamanoBytes` | entero | sí |  |
| `tipoMime` | texto | sí |  |
| `tipoCanonico` | texto o null | no |  |
| `tipoDocumentoTenantId` | uuid o null | no |  |
| `vinculoId` | uuid o null | no |  |
| `sha256` | texto o null | no |  |
| `datos` | objeto libre o null | no |  |

**Respuestas**

- `200` OK — CargaPrefirmadaDto
- `201` Creado — CargaPrefirmadaDto

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `documentoId` | uuid | sí |  |
| `version` | entero | sí |  |
| `duplicado` | booleano | sí |  |
| `carga` | InstruccionCargaDto o null | no |  |
| `carga.metodo` | texto | sí |  |
| `carga.url` | texto | sí |  |
| `carga.cabeceras` | mapa de texto → texto | sí |  |
| `carga.expira` | fecha y hora | sí |  |

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `documentoId` | uuid | sí |  |
| `version` | entero | sí |  |
| `duplicado` | booleano | sí |  |
| `carga` | InstruccionCargaDto o null | no |  |
| `carga.metodo` | texto | sí |  |
| `carga.url` | texto | sí |  |
| `carga.cabeceras` | mapa de texto → texto | sí |  |
| `carga.expira` | fecha y hora | sí |  |

**Errores de negocio que lanza**

- `422` `tamano-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/boveda/documentos" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"nombreArchivo":"Nombre de ejemplo","tamanoBytes":1,"tipoMime":"texto","tipoCanonico":"texto","tipoDocumentoTenantId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","vinculoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","sha256":"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855","datos":{}}'
```

Respuesta `200`:

```json
{
  "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "version": 1,
  "duplicado": true,
  "carga": {
    "metodo": "texto",
    "url": "https://ejemplo.co/recurso",
    "cabeceras": {
      "clave": "vsk_prueba_ejemplo"
    },
    "expira": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `POST /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/confirmar`

**Confirmar carga documento.** Paso 2. El servidor lee el objeto subido: más grande que el máximo del tipo ⇒ 413; tipo real (magic bytes) no admitido para el tipo ⇒ 415; SHA-256 distinto al declarado ⇒ 422 `hash-no-coincide`; el mismo archivo con los mismos datos ya registrado ⇒ 200 `duplicado`. Un PDF con contenido activo o más páginas que las del tipo queda `Devuelta` con su motivo; si no, pasa a `Actual` y se comparte. Sincrónico: responde 200 con el estado final (sin IA no hay análisis en segundo plano). Cada vínculo que recibió la versión recibe además, por el outbox, la orden de adjuntarla a los requisitos de su expediente abierto (`SincronizarRequisitosDelVinculo`, RQ-2) y de calcular su vigencia (`CalcularVigenciasDelDocumento`, TD-6).

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `confirmarCargaDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `documentoId` | path | uuid | sí |  |
| `numero` | path | entero | sí |  |

**Respuestas**

- `200` OK — ConfirmacionCargaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `duplicado` | booleano | sí |  |
| `version` | VersionDocumentoDto | sí |  |
| `version.documentoId` | uuid | sí |  |
| `version.n` | entero | sí |  |
| `version.estado` | EstadoVersion (texto) | sí | valores: Recibida · EnAnalisis · Actual · Superada · Devuelta · Retirada |
| `version.cargadaEn` | fecha y hora | sí |  |
| `version.cargadaPor` | texto | sí |  |
| `version.clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `version.nombreOriginal` | texto | sí |  |
| `version.mime` | texto | sí |  |
| `version.tamanoBytes` | entero | sí |  |
| `version.paginas` | entero o null | no |  |
| `version.sha256` | texto | sí |  |
| `version.motivoDevolucion` | texto o null | no |  |
| `version.detalleDevolucion` | texto o null | no |  |
| `version.hechos` | HechosDto o null | no |  |
| `version.hechos.conIa` | booleano | sí |  |
| `version.hechos.hashVerificado` | booleano | sí |  |
| `version.hechos.antivirus` | texto | sí |  |
| `version.hechos.mimeReal` | texto | sí |  |
| `version.hechos.paginas` | entero o null | no |  |
| `version.hechos.comparacion` | texto | sí |  |
| `version.hechos.camposCambiados` | lista de texto | sí |  |
| `version.hechos.camposCriticosCambiados` | lista de texto | sí |  |
| `version.hechos.pendienteRevisionHumana` | booleano | sí |  |
| `version.hechos.ia` | HechosIaDto o null | no |  |
| `version.hechos.ia.modelo` | texto | sí |  |
| `version.hechos.ia.escalado` | booleano | sí |  |
| `version.hechos.ia.confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `version.hechos.ia.legible` | booleano | sí |  |
| `version.hechos.ia.tipoDetectado` | texto | sí |  |
| `version.hechos.ia.campos` | objeto libre | sí |  |
| `version.hechos.ia.alertas` | lista de texto | sí |  |
| `version.hechos.ia.observaciones` | texto o null | no |  |
| `version.hechos.ia.paginasEnviadas` | entero o null | no |  |
| `version.hechos.ia.paginasOmitidas` | entero o null | no |  |
| `version.hechos.ia.fecha` | fecha y hora | sí |  |
| `version.datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `version.datosDeclarados[].clave` | texto | sí |  |
| `version.datosDeclarados[].etiqueta` | texto | sí |  |
| `version.datosDeclarados[].tipo` | texto | sí |  |
| `version.datosDeclarados[].valor` | texto | sí |  |
| `version.vigencia` | VigenciaDto o null | no |  |
| `version.vigencia.vigenteHasta` | fecha o null | no |  |
| `version.vigencia.base` | texto | sí |  |
| `version.vigencia.estado` | texto | sí |  |
| `version.vigencia.diasAviso` | lista de entero | sí |  |
| `version.vigencia.diasGracia` | entero | sí |  |
| `version.vigencia.finDeGracia` | fecha o null | no |  |
| `version.vigencia.vencidaEn` | fecha y hora o null | no |  |
| `compartidos` | lista de CompartidoDto | sí |  |
| `compartidos[].documentoId` | uuid | sí |  |
| `compartidos[].tipo` | texto | sí |  |
| `compartidos[].version` | entero | sí |  |
| `compartidos[].vinculoId` | uuid | sí |  |
| `compartidos[].empresaId` | uuid | sí |  |

**Errores de negocio que lanza**

- `404` `carga-no-encontrada`
- `409` `archivo-no-cargado`
- `409` `version-ya-confirmada`
- `422` `hash-no-coincide`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/boveda/documentos/3fa85f64-5717-4562-b3fc-2c963f66afa6/versiones/1/confirmar" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "duplicado": true,
  "version": {
    "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "n": 1,
    "estado": "Recibida",
    "cargadaEn": "2026-11-12T15:00:00Z",
    "cargadaPor": "texto",
    "clase": "Archivo",
    "nombreOriginal": "Nombre de ejemplo",
    "mime": "texto",
    "tamanoBytes": 1,
    "paginas": 1,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "motivoDevolucion": "Texto de ejemplo.",
    "detalleDevolucion": "Texto de ejemplo.",
    "hechos": {
      "conIa": true,
      "hashVerificado": true,
      "antivirus": "texto",
      "mimeReal": "texto",
      "paginas": 1,
      "comparacion": "texto",
      "camposCambiados": [
        "texto"
      ],
      "camposCriticosCambiados": [
        "texto"
      ],
      "pendienteRevisionHumana": true,
      "ia": {
        "modelo": "texto",
        "escalado": true,
        "confianza": 1,
        "legible": true,
        "tipoDetectado": "texto",
        "campos": {},
        "alertas": [
          "texto"
        ],
        "observaciones": "Texto de ejemplo.",
        "paginasEnviadas": 1,
        "paginasOmitidas": 1,
        "fecha": "2026-11-12T15:00:00Z"
      }
    },
    "datosDeclarados": [
      {
        "clave": "texto",
        "etiqueta": "texto",
        "tipo": "texto",
        "valor": "vsk_prueba_ejemplo"
      }
    ],
    "vigencia": {
      "vigenteHasta": "2026-11-12",
      "base": "texto",
      "estado": "texto",
      "diasAviso": [
        1
      ],
      "diasGracia": 1,
      "finDeGracia": "2026-11-12",
      "vencidaEn": "2026-11-12T15:00:00Z"
    }
  },
  "compartidos": [
    {
      "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": "texto",
      "version": 1,
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

</details>

## Catálogo documental

### `GET /api/v1/catalogos/tipos-documento`

**Listar tipos documento canonicos.**

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `listarTiposDocumentoCanonicos`**

**Respuestas**

- `200` OK — lista de TipoDocumentoCanonicoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de TipoDocumentoCanonicoDto | sí |  |
| `[].codigo` | texto | sí |  |
| `[].version` | entero | sí |  |
| `[].nombre` | texto | sí |  |
| `[].ambito` | texto | sí |  |
| `[].clase` | texto | sí |  |
| `[].aplicaA` | texto | sí |  |
| `[].tieneVencimientoIntrinseco` | booleano | sí |  |
| `[].tieneFechaExpedicion` | booleano | sí |  |
| `[].esPeriodico` | booleano | sí |  |
| `[].periodicidad` | texto o null | no |  |
| `[].permiteRatificacion` | booleano | sí |  |
| `[].dePersonaRelacionada` | booleano | sí |  |
| `[].camposClave` | lista de texto | sí |  |
| `[].camposCriticos` | lista de texto | sí |  |
| `[].sensibilidad` | texto | sí |  |
| `[].reglaFrescuraSugerida` | ReglaFrescuraDto o null | no |  |
| `[].reglaFrescuraSugerida.tipo` | texto | sí |  |
| `[].reglaFrescuraSugerida.dias` | entero o null | no |  |
| `[].reglaFrescuraSugerida.regla` | texto o null | no |  |
| `[].retencionAniosSugerida` | entero | sí |  |
| `[].cargaSoportada` | booleano | sí |  |
| `[].formatosPermitidos` | lista de texto o null | no |  |
| `[].tamanoMaximoMb` | entero o null | no |  |
| `[].paginasMaximas` | entero o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/catalogos/tipos-documento" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
[
  {
    "codigo": "CODIGO_EJEMPLO",
    "version": 1,
    "nombre": "Constructora Norte S.A.S.",
    "ambito": "texto",
    "clase": "texto",
    "aplicaA": "texto",
    "tieneVencimientoIntrinseco": true,
    "tieneFechaExpedicion": true,
    "esPeriodico": true,
    "periodicidad": "texto",
    "permiteRatificacion": true,
    "dePersonaRelacionada": true,
    "camposClave": [
      "texto"
    ],
    "camposCriticos": [
      "texto"
    ],
    "sensibilidad": "texto",
    "reglaFrescuraSugerida": {
      "tipo": "texto",
      "dias": 1,
      "regla": "texto"
    },
    "retencionAniosSugerida": 1,
    "cargaSoportada": true,
    "formatosPermitidos": [
      "texto"
    ],
    "tamanoMaximoMb": 1,
    "paginasMaximas": 1
  }
]
```

</details>

## Documentos de vínculo

Documentos de un vínculo vistos por la empresa (`X-Empresa`): solo las versiones compartidas con ese vínculo, que existen únicamente tras la aceptación de la invitación (B-1). Lo no compartido responde 404 (T-4), nunca 403.

### `GET /api/v1/vinculos/{id}/documentos`

**Listar documentos vínculo.**

**Permiso documentos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarDocumentosVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — lista de DocumentoVistaEmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de DocumentoVistaEmpresaDto | sí |  |
| `[].documentoId` | uuid | sí |  |
| `[].tipo` | TipoDocumentoResumenDto | sí |  |
| `[].tipo.codigo` | texto | sí |  |
| `[].tipo.nombre` | texto | sí |  |
| `[].tipo.tipoDocumentoTenantId` | uuid o null | no |  |
| `[].ambito` | AmbitoDocumento (texto) | sí | valores: Global · PorVinculo |
| `[].versionActual` | entero o null | no |  |
| `[].versionesCompartidas` | lista de VersionDocumentoDto | sí |  |
| `[].versionesCompartidas[].documentoId` | uuid | sí |  |
| `[].versionesCompartidas[].n` | entero | sí |  |
| `[].versionesCompartidas[].estado` | EstadoVersion (texto) | sí | valores: Recibida · EnAnalisis · Actual · Superada · Devuelta · Retirada |
| `[].versionesCompartidas[].cargadaEn` | fecha y hora | sí |  |
| `[].versionesCompartidas[].cargadaPor` | texto | sí |  |
| `[].versionesCompartidas[].clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `[].versionesCompartidas[].nombreOriginal` | texto | sí |  |
| `[].versionesCompartidas[].mime` | texto | sí |  |
| `[].versionesCompartidas[].tamanoBytes` | entero | sí |  |
| `[].versionesCompartidas[].paginas` | entero o null | no |  |
| `[].versionesCompartidas[].sha256` | texto | sí |  |
| `[].versionesCompartidas[].motivoDevolucion` | texto o null | no |  |
| `[].versionesCompartidas[].detalleDevolucion` | texto o null | no |  |
| `[].versionesCompartidas[].hechos` | HechosDto o null | no |  |
| `[].versionesCompartidas[].hechos.conIa` | booleano | sí |  |
| `[].versionesCompartidas[].hechos.hashVerificado` | booleano | sí |  |
| `[].versionesCompartidas[].hechos.antivirus` | texto | sí |  |
| `[].versionesCompartidas[].hechos.mimeReal` | texto | sí |  |
| `[].versionesCompartidas[].hechos.paginas` | entero o null | no |  |
| `[].versionesCompartidas[].hechos.comparacion` | texto | sí |  |
| `[].versionesCompartidas[].hechos.camposCambiados` | lista de texto | sí |  |
| `[].versionesCompartidas[].hechos.camposCriticosCambiados` | lista de texto | sí |  |
| `[].versionesCompartidas[].hechos.pendienteRevisionHumana` | booleano | sí |  |
| `[].versionesCompartidas[].hechos.ia` | HechosIaDto o null | no |  |
| `[].versionesCompartidas[].hechos.ia.modelo` | texto | sí |  |
| `[].versionesCompartidas[].hechos.ia.escalado` | booleano | sí |  |
| `[].versionesCompartidas[].hechos.ia.confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `[].versionesCompartidas[].hechos.ia.legible` | booleano | sí |  |
| `[].versionesCompartidas[].hechos.ia.tipoDetectado` | texto | sí |  |
| `[].versionesCompartidas[].hechos.ia.campos` | objeto libre | sí |  |
| `[].versionesCompartidas[].hechos.ia.alertas` | lista de texto | sí |  |
| `[].versionesCompartidas[].hechos.ia.observaciones` | texto o null | no |  |
| `[].versionesCompartidas[].hechos.ia.paginasEnviadas` | entero o null | no |  |
| `[].versionesCompartidas[].hechos.ia.paginasOmitidas` | entero o null | no |  |
| `[].versionesCompartidas[].hechos.ia.fecha` | fecha y hora | sí |  |
| `[].versionesCompartidas[].datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `[].versionesCompartidas[].datosDeclarados[].clave` | texto | sí |  |
| `[].versionesCompartidas[].datosDeclarados[].etiqueta` | texto | sí |  |
| `[].versionesCompartidas[].datosDeclarados[].tipo` | texto | sí |  |
| `[].versionesCompartidas[].datosDeclarados[].valor` | texto | sí |  |
| `[].versionesCompartidas[].vigencia` | VigenciaDto o null | no |  |
| `[].versionesCompartidas[].vigencia.vigenteHasta` | fecha o null | no |  |
| `[].versionesCompartidas[].vigencia.base` | texto | sí |  |
| `[].versionesCompartidas[].vigencia.estado` | texto | sí |  |
| `[].versionesCompartidas[].vigencia.diasAviso` | lista de entero | sí |  |
| `[].versionesCompartidas[].vigencia.diasGracia` | entero | sí |  |
| `[].versionesCompartidas[].vigencia.finDeGracia` | fecha o null | no |  |
| `[].versionesCompartidas[].vigencia.vencidaEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/documentos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
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
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "estado": "Recibida",
        "cargadaEn": "2026-11-12T15:00:00Z",
        "cargadaPor": "texto",
        "clase": "Archivo",
        "nombreOriginal": "Nombre de ejemplo",
        "mime": "texto",
        "tamanoBytes": 1,
        "paginas": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "motivoDevolucion": "Texto de ejemplo.",
        "detalleDevolucion": "Texto de ejemplo.",
        "hechos": {
          "conIa": true,
          "hashVerificado": true,
          "antivirus": "texto",
          "mimeReal": "texto",
          "paginas": 1,
          "comparacion": "texto",
          "camposCambiados": [
            "texto"
          ],
          "camposCriticosCambiados": [
            "texto"
          ],
          "pendienteRevisionHumana": true,
          "ia": {
            "modelo": "texto",
            "escalado": true,
            "confianza": 1,
            "legible": true,
            "tipoDetectado": "texto",
            "campos": {},
            "alertas": [
              null
            ],
            "observaciones": "Texto de ejemplo.",
            "paginasEnviadas": 1,
            "paginasOmitidas": 1,
            "fecha": "2026-11-12T15:00:00Z"
          }
        },
        "datosDeclarados": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "valor": "vsk_prueba_ejemplo"
          }
        ],
        "vigencia": {
          "vigenteHasta": "2026-11-12",
          "base": "texto",
          "estado": "texto",
          "diasAviso": [
            1
          ],
          "diasGracia": 1,
          "finDeGracia": "2026-11-12",
          "vencidaEn": "2026-11-12T15:00:00Z"
        }
      }
    ]
  }
]
```

</details>

### `GET /api/v1/vinculos/{id}/documentos/{documentoId}`

**Obtener documento vínculo.**

**Permiso documentos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerDocumentoVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `documentoId` | path | uuid | sí |  |

**Respuestas**

- `200` OK — DocumentoVistaEmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `documentoId` | uuid | sí |  |
| `tipo` | TipoDocumentoResumenDto | sí |  |
| `tipo.codigo` | texto | sí |  |
| `tipo.nombre` | texto | sí |  |
| `tipo.tipoDocumentoTenantId` | uuid o null | no |  |
| `ambito` | AmbitoDocumento (texto) | sí | valores: Global · PorVinculo |
| `versionActual` | entero o null | no |  |
| `versionesCompartidas` | lista de VersionDocumentoDto | sí |  |
| `versionesCompartidas[].documentoId` | uuid | sí |  |
| `versionesCompartidas[].n` | entero | sí |  |
| `versionesCompartidas[].estado` | EstadoVersion (texto) | sí | valores: Recibida · EnAnalisis · Actual · Superada · Devuelta · Retirada |
| `versionesCompartidas[].cargadaEn` | fecha y hora | sí |  |
| `versionesCompartidas[].cargadaPor` | texto | sí |  |
| `versionesCompartidas[].clase` | ClaseDocumento (texto) | sí | valores: Archivo · Formulario |
| `versionesCompartidas[].nombreOriginal` | texto | sí |  |
| `versionesCompartidas[].mime` | texto | sí |  |
| `versionesCompartidas[].tamanoBytes` | entero | sí |  |
| `versionesCompartidas[].paginas` | entero o null | no |  |
| `versionesCompartidas[].sha256` | texto | sí |  |
| `versionesCompartidas[].motivoDevolucion` | texto o null | no |  |
| `versionesCompartidas[].detalleDevolucion` | texto o null | no |  |
| `versionesCompartidas[].hechos` | HechosDto o null | no |  |
| `versionesCompartidas[].hechos.conIa` | booleano | sí |  |
| `versionesCompartidas[].hechos.hashVerificado` | booleano | sí |  |
| `versionesCompartidas[].hechos.antivirus` | texto | sí |  |
| `versionesCompartidas[].hechos.mimeReal` | texto | sí |  |
| `versionesCompartidas[].hechos.paginas` | entero o null | no |  |
| `versionesCompartidas[].hechos.comparacion` | texto | sí |  |
| `versionesCompartidas[].hechos.camposCambiados` | lista de texto | sí |  |
| `versionesCompartidas[].hechos.camposCriticosCambiados` | lista de texto | sí |  |
| `versionesCompartidas[].hechos.pendienteRevisionHumana` | booleano | sí |  |
| `versionesCompartidas[].hechos.ia` | HechosIaDto o null | no |  |
| `versionesCompartidas[].hechos.ia.modelo` | texto | sí |  |
| `versionesCompartidas[].hechos.ia.escalado` | booleano | sí |  |
| `versionesCompartidas[].hechos.ia.confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `versionesCompartidas[].hechos.ia.legible` | booleano | sí |  |
| `versionesCompartidas[].hechos.ia.tipoDetectado` | texto | sí |  |
| `versionesCompartidas[].hechos.ia.campos` | objeto libre | sí |  |
| `versionesCompartidas[].hechos.ia.alertas` | lista de texto | sí |  |
| `versionesCompartidas[].hechos.ia.observaciones` | texto o null | no |  |
| `versionesCompartidas[].hechos.ia.paginasEnviadas` | entero o null | no |  |
| `versionesCompartidas[].hechos.ia.paginasOmitidas` | entero o null | no |  |
| `versionesCompartidas[].hechos.ia.fecha` | fecha y hora | sí |  |
| `versionesCompartidas[].datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `versionesCompartidas[].datosDeclarados[].clave` | texto | sí |  |
| `versionesCompartidas[].datosDeclarados[].etiqueta` | texto | sí |  |
| `versionesCompartidas[].datosDeclarados[].tipo` | texto | sí |  |
| `versionesCompartidas[].datosDeclarados[].valor` | texto | sí |  |
| `versionesCompartidas[].vigencia` | VigenciaDto o null | no |  |
| `versionesCompartidas[].vigencia.vigenteHasta` | fecha o null | no |  |
| `versionesCompartidas[].vigencia.base` | texto | sí |  |
| `versionesCompartidas[].vigencia.estado` | texto | sí |  |
| `versionesCompartidas[].vigencia.diasAviso` | lista de entero | sí |  |
| `versionesCompartidas[].vigencia.diasGracia` | entero | sí |  |
| `versionesCompartidas[].vigencia.finDeGracia` | fecha o null | no |  |
| `versionesCompartidas[].vigencia.vencidaEn` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `404` `documento-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/documentos/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
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
      "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "n": 1,
      "estado": "Recibida",
      "cargadaEn": "2026-11-12T15:00:00Z",
      "cargadaPor": "texto",
      "clase": "Archivo",
      "nombreOriginal": "Nombre de ejemplo",
      "mime": "texto",
      "tamanoBytes": 1,
      "paginas": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "motivoDevolucion": "Texto de ejemplo.",
      "detalleDevolucion": "Texto de ejemplo.",
      "hechos": {
        "conIa": true,
        "hashVerificado": true,
        "antivirus": "texto",
        "mimeReal": "texto",
        "paginas": 1,
        "comparacion": "texto",
        "camposCambiados": [
          "texto"
        ],
        "camposCriticosCambiados": [
          "texto"
        ],
        "pendienteRevisionHumana": true,
        "ia": {
          "modelo": "texto",
          "escalado": true,
          "confianza": 1,
          "legible": true,
          "tipoDetectado": "texto",
          "campos": {},
          "alertas": [
            "texto"
          ],
          "observaciones": "Texto de ejemplo.",
          "paginasEnviadas": 1,
          "paginasOmitidas": 1,
          "fecha": "2026-11-12T15:00:00Z"
        }
      },
      "datosDeclarados": [
        {
          "clave": "texto",
          "etiqueta": "texto",
          "tipo": "texto",
          "valor": "vsk_prueba_ejemplo"
        }
      ],
      "vigencia": {
        "vigenteHasta": "2026-11-12",
        "base": "texto",
        "estado": "texto",
        "diasAviso": [
          1
        ],
        "diasGracia": 1,
        "finDeGracia": "2026-11-12",
        "vencidaEn": "2026-11-12T15:00:00Z"
      }
    }
  ]
}
```

</details>

### `POST /api/v1/vinculos/{id}/documentos/{documentoId}/versiones/{numero}/descarga`

**Descargar documento vínculo.** URL prefirmada GET (5 min) de una versión compartida con el vínculo; registra `DocumentoAccedido` con el actor.

**Permiso documentos:descargar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `descargarDocumentoVinculo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `documentoId` | path | uuid | sí |  |
| `numero` | path | entero | sí |  |

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

- `404` `documento-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/documentos/3fa85f64-5717-4562-b3fc-2c963f66afa6/versiones/1/descarga" \
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

## Formularios

Versión solo de formulario (TD-4): el tercero envía los valores de los campos de un tipo `Formulario` de la empresa de su vínculo y nace una versión del documento `PorVinculo` con sus `DatosDeclarados`, sin binario. Misma mecánica que un archivo: deduplicación (por la huella de los datos), versión `Actual`, compartición con el vínculo, adjunción a su expediente abierto y vigencia.

### `POST /public/v1/boveda/formularios`

**Enviar formulario bóveda.** 200 `ConfirmacionCargaDto` (`duplicado: true` si los mismos datos ya son una versión). 422 `campo-requerido`, `valor-invalido`, `campo-desconocido` (con `campo`), `archivo-requerido` (el tipo pide también el archivo: va por la carga en dos pasos con `datos`), `clase-no-soportada`, `tipo-documento-invalido`; 404 vínculo fuera de la sesión; 409 `estado-invalido`.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `enviarFormularioBoveda`**

**Cuerpo de la petición** (`application/json`, EnviarFormularioDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `vinculoId` | uuid | sí |  |
| `datos` | objeto libre o null | no |  |
| `tipoDocumentoTenantId` | uuid o null | no |  |
| `tipoCanonico` | texto o null | no |  |

**Respuestas**

- `200` OK — ConfirmacionCargaDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ConfirmacionCargaDto` que se detallan en `POST /public/v1/boveda/documentos/{documentoId}/versiones/{numero}/confirmar`.

**Errores de negocio que lanza**

- `422` `archivo-requerido`
- `422` `clase-no-soportada`
- `422` `tipo-documento-requerido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/boveda/formularios" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"vinculoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","datos":{},"tipoDocumentoTenantId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","tipoCanonico":"texto"}'
```

Respuesta `200`:

```json
{
  "duplicado": true,
  "version": {
    "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "n": 1,
    "estado": "Recibida",
    "cargadaEn": "2026-11-12T15:00:00Z",
    "cargadaPor": "texto",
    "clase": "Archivo",
    "nombreOriginal": "Nombre de ejemplo",
    "mime": "texto",
    "tamanoBytes": 1,
    "paginas": 1,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "motivoDevolucion": "Texto de ejemplo.",
    "detalleDevolucion": "Texto de ejemplo.",
    "hechos": {
      "conIa": true,
      "hashVerificado": true,
      "antivirus": "texto",
      "mimeReal": "texto",
      "paginas": 1,
      "comparacion": "texto",
      "camposCambiados": [
        "texto"
      ],
      "camposCriticosCambiados": [
        "texto"
      ],
      "pendienteRevisionHumana": true,
      "ia": {
        "modelo": "texto",
        "escalado": true,
        "confianza": 1,
        "legible": true,
        "tipoDetectado": "texto",
        "campos": {},
        "alertas": [
          "texto"
        ],
        "observaciones": "Texto de ejemplo.",
        "paginasEnviadas": 1,
        "paginasOmitidas": 1,
        "fecha": "2026-11-12T15:00:00Z"
      }
    },
    "datosDeclarados": [
      {
        "clave": "texto",
        "etiqueta": "texto",
        "tipo": "texto",
        "valor": "vsk_prueba_ejemplo"
      }
    ],
    "vigencia": {
      "vigenteHasta": "2026-11-12",
      "base": "texto",
      "estado": "texto",
      "diasAviso": [
        1
      ],
      "diasGracia": 1,
      "finDeGracia": "2026-11-12",
      "vencidaEn": "2026-11-12T15:00:00Z"
    }
  },
  "compartidos": [
    {
      "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": "texto",
      "version": 1,
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

</details>

## Plantillas

Plantilla descargable de un tipo de documento (TD-5): la empresa la sube en dos pasos como la carga del tercero (URL prefirmada PUT a la zona temporal del bucket del tenant y confirmación que verifica tamaño, hash y tipo real), queda versionada (vigente e historial) y auditada. El tercero la descarga con URL prefirmada solo desde un requisito de su propio expediente.

### `GET /api/v1/tipos-documento/{id}/plantilla`

**Obtener plantilla tipo documento.**

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `obtenerPlantillaTipoDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — PlantillaTipoDocumentoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tipoDocumentoId` | uuid | sí |  |
| `vigente` | PlantillaVersionDto o null | no |  |
| `vigente.version` | entero | sí |  |
| `vigente.nombreOriginal` | texto | sí |  |
| `vigente.mime` | texto | sí |  |
| `vigente.tamanoBytes` | entero | sí |  |
| `vigente.sha256` | texto | sí |  |
| `vigente.cargadaEn` | fecha y hora | sí |  |
| `vigente.cargadaPor` | texto | sí |  |
| `vigente.vigente` | booleano | sí |  |
| `vigente.retiradaEn` | fecha y hora o null | no |  |
| `historial` | lista de PlantillaVersionDto | sí | mismos campos que `vigente` |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/tipos-documento/3fa85f64-5717-4562-b3fc-2c963f66afa6/plantilla" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "tipoDocumentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vigente": {
    "version": 1,
    "nombreOriginal": "Nombre de ejemplo",
    "mime": "texto",
    "tamanoBytes": 1,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "cargadaEn": "2026-11-12T15:00:00Z",
    "cargadaPor": "texto",
    "vigente": true,
    "retiradaEn": "2026-11-12T15:00:00Z"
  },
  "historial": [
    {
      "version": 1,
      "nombreOriginal": "Nombre de ejemplo",
      "mime": "texto",
      "tamanoBytes": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "cargadaEn": "2026-11-12T15:00:00Z",
      "cargadaPor": "texto",
      "vigente": true,
      "retiradaEn": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `PUT /api/v1/tipos-documento/{id}/plantilla`

**Iniciar plantilla tipo documento.** Paso 1: 200 con la URL PUT de la versión siguiente. 413 tamaño sobre el global; 415 si no es PDF, DOCX o XLSX.

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `iniciarPlantillaTipoDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, IniciarPlantillaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombreArchivo` | texto | sí |  |
| `tamanoBytes` | entero | sí |  |
| `tipoMime` | texto | sí |  |
| `sha256` | texto o null | no |  |

**Respuestas**

- `200` OK — CargaDePlantillaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tipoDocumentoId` | uuid | sí |  |
| `version` | entero | sí |  |
| `carga` | InstruccionCargaDto | sí |  |
| `carga.metodo` | texto | sí |  |
| `carga.url` | texto | sí |  |
| `carga.cabeceras` | mapa de texto → texto | sí |  |
| `carga.expira` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/tipos-documento/3fa85f64-5717-4562-b3fc-2c963f66afa6/plantilla" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"nombreArchivo":"Nombre de ejemplo","tamanoBytes":1,"tipoMime":"texto","sha256":"e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"}'
```

Respuesta `200`:

```json
{
  "tipoDocumentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "version": 1,
  "carga": {
    "metodo": "texto",
    "url": "https://ejemplo.co/recurso",
    "cabeceras": {
      "clave": "vsk_prueba_ejemplo"
    },
    "expira": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `DELETE /api/v1/tipos-documento/{id}/plantilla`

**Retirar plantilla tipo documento.** Deja el tipo sin plantilla vigente (el historial se conserva). 404 `plantilla-no-encontrada`.

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `retirarPlantillaTipoDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — PlantillaTipoDocumentoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tipoDocumentoId` | uuid | sí |  |
| `vigente` | PlantillaVersionDto o null | no |  |
| `vigente.version` | entero | sí |  |
| `vigente.nombreOriginal` | texto | sí |  |
| `vigente.mime` | texto | sí |  |
| `vigente.tamanoBytes` | entero | sí |  |
| `vigente.sha256` | texto | sí |  |
| `vigente.cargadaEn` | fecha y hora | sí |  |
| `vigente.cargadaPor` | texto | sí |  |
| `vigente.vigente` | booleano | sí |  |
| `vigente.retiradaEn` | fecha y hora o null | no |  |
| `historial` | lista de PlantillaVersionDto | sí | mismos campos que `vigente` |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X DELETE "https://{host}/api/v1/tipos-documento/3fa85f64-5717-4562-b3fc-2c963f66afa6/plantilla" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "tipoDocumentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vigente": {
    "version": 1,
    "nombreOriginal": "Nombre de ejemplo",
    "mime": "texto",
    "tamanoBytes": 1,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "cargadaEn": "2026-11-12T15:00:00Z",
    "cargadaPor": "texto",
    "vigente": true,
    "retiradaEn": "2026-11-12T15:00:00Z"
  },
  "historial": [
    {
      "version": 1,
      "nombreOriginal": "Nombre de ejemplo",
      "mime": "texto",
      "tamanoBytes": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "cargadaEn": "2026-11-12T15:00:00Z",
      "cargadaPor": "texto",
      "vigente": true,
      "retiradaEn": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/tipos-documento/{id}/plantilla/{version}/confirmar`

**Confirmar plantilla tipo documento.** Paso 2: lee el objeto y verifica tamaño (413), SHA-256 declarado (422 `hash-no-coincide`) y tipo real por sus bytes (415). La versión queda vigente y en el historial (mismo archivo que la vigente ⇒ `duplicado`). 404 `carga-no-encontrada`; 409 `archivo-no-cargado`.

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `confirmarPlantillaTipoDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `version` | path | entero | sí |  |

**Respuestas**

- `200` OK — ConfirmacionPlantillaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `duplicado` | booleano | sí |  |
| `plantilla` | PlantillaVersionDto | sí |  |
| `plantilla.version` | entero | sí |  |
| `plantilla.nombreOriginal` | texto | sí |  |
| `plantilla.mime` | texto | sí |  |
| `plantilla.tamanoBytes` | entero | sí |  |
| `plantilla.sha256` | texto | sí |  |
| `plantilla.cargadaEn` | fecha y hora | sí |  |
| `plantilla.cargadaPor` | texto | sí |  |
| `plantilla.vigente` | booleano | sí |  |
| `plantilla.retiradaEn` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `409` `archivo-no-cargado`
- `415` `tipo-archivo-no-soportado`
- `422` `hash-no-coincide`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/tipos-documento/3fa85f64-5717-4562-b3fc-2c963f66afa6/plantilla/1/confirmar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "duplicado": true,
  "plantilla": {
    "version": 1,
    "nombreOriginal": "Nombre de ejemplo",
    "mime": "texto",
    "tamanoBytes": 1,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "cargadaEn": "2026-11-12T15:00:00Z",
    "cargadaPor": "texto",
    "vigente": true,
    "retiradaEn": "2026-11-12T15:00:00Z"
  }
}
```

</details>

### `GET /api/v1/tipos-documento/{id}/plantilla/descarga`

**Descargar plantilla tipo documento.** URL prefirmada GET de la vigente o, con `?version=n`, de una del historial. 404 `plantilla-no-encontrada`.

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `descargarPlantillaTipoDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `version` | query | entero | no |  |

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
curl -X GET "https://{host}/api/v1/tipos-documento/3fa85f64-5717-4562-b3fc-2c963f66afa6/plantilla/descarga" \
  -H "X-Api-Key: $API_KEY"
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

### `GET /public/v1/expedientes/{id}/requisitos/{requisitoId}/plantilla`

**Descargar plantilla requisito.** El tercero descarga la plantilla vigente del tipo de un requisito de su expediente: el expediente debe ser de un vínculo de su sesión (si no, 404 `expediente-no-encontrado`, T-4); sin plantilla ⇒ 404 `plantilla-no-encontrada`.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `descargarPlantillaRequisito`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `requisitoId` | path | uuid | sí |  |

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

- `404` `expediente-no-encontrado`
- `404` `plantilla-no-encontrada`
- `404` `requisito-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/requisitos/3fa85f64-5717-4562-b3fc-2c963f66afa6/plantilla" \
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

## Tipos documento

Tipos de documento del tenant (docs/05 §2.2): defaults del grupo, enlace al canónico y, desde TD-1…TD-6, reglas de archivo, modo de captura con sus campos y la vigencia (frescura, avisos, gracia). Qué se pide a cada vínculo lo deciden los requisitos de su flujo (RQ-3). Todo cambio queda en la auditoría de configuración (R-1).

### `GET /api/v1/tipos-documento`

**Listar tipos documento.**

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `listarTiposDocumento`**

**Respuestas**

- `200` OK — lista de TipoDocumentoTenantDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de TipoDocumentoTenantDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].codigo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].tipoCanonico` | texto o null | no |  |
| `[].versionCanonico` | entero o null | no |  |
| `[].ambito` | texto | sí |  |
| `[].clase` | texto | sí |  |
| `[].textoAyuda` | texto o null | no |  |
| `[].reglaFrescura` | ReglaFrescuraDto | sí |  |
| `[].reglaFrescura.tipo` | texto | sí |  |
| `[].reglaFrescura.dias` | entero o null | no |  |
| `[].reglaFrescura.regla` | texto o null | no |  |
| `[].diasAviso` | lista de entero | sí |  |
| `[].diasGracia` | entero | sí |  |
| `[].diasRevalidacion` | entero | sí |  |
| `[].activo` | booleano | sí |  |
| `[].creadoEn` | fecha y hora | sí |  |
| `[].formatosPermitidos` | lista de texto o null | no |  |
| `[].tamanoMaximoMb` | entero o null | no |  |
| `[].paginasMaximas` | entero o null | no |  |
| `[].modo` | texto | no | por omisión "Archivo" |
| `[].campos` | lista de CampoFormularioDto o null | no |  |
| `[].campos[].clave` | texto | sí |  |
| `[].campos[].etiqueta` | texto | sí |  |
| `[].campos[].tipo` | texto | sí |  |
| `[].campos[].obligatorio` | booleano | no | por omisión false |
| `[].campos[].opciones` | lista de texto o null | no |  |
| `[].campos[].ayuda` | texto o null | no |  |
| `[].reglasEfectivas` | ReglasArchivoDto o null | no |  |
| `[].reglasEfectivas.formatos` | lista de texto | sí |  |
| `[].reglasEfectivas.tamanoMaximoBytes` | entero | sí |  |
| `[].reglasEfectivas.paginasMaximas` | entero | sí |  |
| `[].reglasEfectivas.descripcion` | texto | sí |  |
| `[].plantilla` | PlantillaVersionDto o null | no |  |
| `[].plantilla.version` | entero | sí |  |
| `[].plantilla.nombreOriginal` | texto | sí |  |
| `[].plantilla.mime` | texto | sí |  |
| `[].plantilla.tamanoBytes` | entero | sí |  |
| `[].plantilla.sha256` | texto | sí |  |
| `[].plantilla.cargadaEn` | fecha y hora | sí |  |
| `[].plantilla.cargadaPor` | texto | sí |  |
| `[].plantilla.vigente` | booleano | sí |  |
| `[].plantilla.retiradaEn` | fecha y hora o null | no |  |
| `[].cargaSoportada` | booleano | no | por omisión true |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/tipos-documento" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "tipoCanonico": "texto",
    "versionCanonico": 1,
    "ambito": "texto",
    "clase": "texto",
    "textoAyuda": "Texto de ejemplo.",
    "reglaFrescura": {
      "tipo": "texto",
      "dias": 1,
      "regla": "texto"
    },
    "diasAviso": [
      1
    ],
    "diasGracia": 1,
    "diasRevalidacion": 1,
    "activo": true,
    "creadoEn": "2026-11-12T15:00:00Z",
    "formatosPermitidos": [
      "texto"
    ],
    "tamanoMaximoMb": 1,
    "paginasMaximas": 1,
    "modo": "Archivo",
    "campos": [
      {
        "clave": "texto",
        "etiqueta": "texto",
        "tipo": "texto",
        "obligatorio": false,
        "opciones": [
          "texto"
        ],
        "ayuda": "texto"
      }
    ],
    "reglasEfectivas": {
      "formatos": [
        "texto"
      ],
      "tamanoMaximoBytes": 1,
      "paginasMaximas": 1,
      "descripcion": "Texto de ejemplo."
    },
    "plantilla": {
      "version": 1,
      "nombreOriginal": "Nombre de ejemplo",
      "mime": "texto",
      "tamanoBytes": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "cargadaEn": "2026-11-12T15:00:00Z",
      "cargadaPor": "texto",
      "vigente": true,
      "retiradaEn": "2026-11-12T15:00:00Z"
    },
    "cargaSoportada": true
  }
]
```

</details>

### `POST /api/v1/tipos-documento`

**Crear tipo documento.** 201 con el tipo. 409 `tipo-documento-duplicado`; 422 `codigo-invalido`, `ambito-no-ampliable`, `formato-invalido`, `formato-mas-laxo`, `limite-invalido`, `limite-mas-laxo`, `limite-sobre-el-global`, `modo-invalido`, `campos-requeridos`, `campo-invalido`, `campo-duplicado`, `campo-reservado`, `formulario-solo-por-vinculo`.

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `crearTipoDocumento`**

**Cuerpo de la petición** (`application/json`, CrearTipoDocumentoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `nombre` | texto o null | no |  |
| `tipoCanonico` | texto o null | no |  |
| `ambito` | texto o null | no |  |
| `reglaFrescura` | ReglaFrescuraDto o null | no |  |
| `reglaFrescura.tipo` | texto | sí |  |
| `reglaFrescura.dias` | entero o null | no |  |
| `reglaFrescura.regla` | texto o null | no |  |
| `diasAviso` | lista de entero o null | no |  |
| `diasGracia` | entero o null | no |  |
| `diasRevalidacion` | entero o null | no |  |
| `textoAyuda` | texto o null | no |  |
| `formatosPermitidos` | lista de texto o null | no |  |
| `tamanoMaximoMb` | entero o null | no |  |
| `paginasMaximas` | entero o null | no |  |
| `modo` | texto o null | no |  |
| `campos` | lista de CampoFormularioDto o null | no |  |
| `campos[].clave` | texto | sí |  |
| `campos[].etiqueta` | texto | sí |  |
| `campos[].tipo` | texto | sí |  |
| `campos[].obligatorio` | booleano | no | por omisión false |
| `campos[].opciones` | lista de texto o null | no |  |
| `campos[].ayuda` | texto o null | no |  |

**Respuestas**

- `201` Creado — TipoDocumentoTenantDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `tipoCanonico` | texto o null | no |  |
| `versionCanonico` | entero o null | no |  |
| `ambito` | texto | sí |  |
| `clase` | texto | sí |  |
| `textoAyuda` | texto o null | no |  |
| `reglaFrescura` | ReglaFrescuraDto | sí |  |
| `reglaFrescura.tipo` | texto | sí |  |
| `reglaFrescura.dias` | entero o null | no |  |
| `reglaFrescura.regla` | texto o null | no |  |
| `diasAviso` | lista de entero | sí |  |
| `diasGracia` | entero | sí |  |
| `diasRevalidacion` | entero | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `formatosPermitidos` | lista de texto o null | no |  |
| `tamanoMaximoMb` | entero o null | no |  |
| `paginasMaximas` | entero o null | no |  |
| `modo` | texto | no | por omisión "Archivo" |
| `campos` | lista de CampoFormularioDto o null | no |  |
| `campos[].clave` | texto | sí |  |
| `campos[].etiqueta` | texto | sí |  |
| `campos[].tipo` | texto | sí |  |
| `campos[].obligatorio` | booleano | no | por omisión false |
| `campos[].opciones` | lista de texto o null | no |  |
| `campos[].ayuda` | texto o null | no |  |
| `reglasEfectivas` | ReglasArchivoDto o null | no |  |
| `reglasEfectivas.formatos` | lista de texto | sí |  |
| `reglasEfectivas.tamanoMaximoBytes` | entero | sí |  |
| `reglasEfectivas.paginasMaximas` | entero | sí |  |
| `reglasEfectivas.descripcion` | texto | sí |  |
| `plantilla` | PlantillaVersionDto o null | no |  |
| `plantilla.version` | entero | sí |  |
| `plantilla.nombreOriginal` | texto | sí |  |
| `plantilla.mime` | texto | sí |  |
| `plantilla.tamanoBytes` | entero | sí |  |
| `plantilla.sha256` | texto | sí |  |
| `plantilla.cargadaEn` | fecha y hora | sí |  |
| `plantilla.cargadaPor` | texto | sí |  |
| `plantilla.vigente` | booleano | sí |  |
| `plantilla.retiradaEn` | fecha y hora o null | no |  |
| `cargaSoportada` | booleano | no | por omisión true |

**Errores de negocio que lanza**

- `409` `tipo-documento-duplicado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/tipos-documento" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S.","tipoCanonico":"texto","ambito":"texto","reglaFrescura":{"tipo":"texto","dias":1,"regla":"texto"},"diasAviso":[1],"diasGracia":1,"diasRevalidacion":1,"textoAyuda":"Texto de ejemplo.","formatosPermitidos":["texto"],"tamanoMaximoMb":1,"paginasMaximas":1,"modo":"texto","campos":[{"clave":"texto","etiqueta":"texto","tipo":"texto","obligatorio":false,"opciones":["texto"],"ayuda":"texto"}]}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "tipoCanonico": "texto",
  "versionCanonico": 1,
  "ambito": "texto",
  "clase": "texto",
  "textoAyuda": "Texto de ejemplo.",
  "reglaFrescura": {
    "tipo": "texto",
    "dias": 1,
    "regla": "texto"
  },
  "diasAviso": [
    1
  ],
  "diasGracia": 1,
  "diasRevalidacion": 1,
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "formatosPermitidos": [
    "texto"
  ],
  "tamanoMaximoMb": 1,
  "paginasMaximas": 1,
  "modo": "Archivo",
  "campos": [
    {
      "clave": "texto",
      "etiqueta": "texto",
      "tipo": "texto",
      "obligatorio": false,
      "opciones": [
        "texto"
      ],
      "ayuda": "texto"
    }
  ],
  "reglasEfectivas": {
    "formatos": [
      "texto"
    ],
    "tamanoMaximoBytes": 1,
    "paginasMaximas": 1,
    "descripcion": "Texto de ejemplo."
  },
  "plantilla": {
    "version": 1,
    "nombreOriginal": "Nombre de ejemplo",
    "mime": "texto",
    "tamanoBytes": 1,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "cargadaEn": "2026-11-12T15:00:00Z",
    "cargadaPor": "texto",
    "vigente": true,
    "retiradaEn": "2026-11-12T15:00:00Z"
  },
  "cargaSoportada": true
}
```

</details>

### `GET /api/v1/tipos-documento/{id}`

**Obtener tipo documento.**

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `obtenerTipoDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — TipoDocumentoTenantDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `TipoDocumentoTenantDto` que se detallan en `POST /api/v1/tipos-documento`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/tipos-documento/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "tipoCanonico": "texto",
  "versionCanonico": 1,
  "ambito": "texto",
  "clase": "texto",
  "textoAyuda": "Texto de ejemplo.",
  "reglaFrescura": {
    "tipo": "texto",
    "dias": 1,
    "regla": "texto"
  },
  "diasAviso": [
    1
  ],
  "diasGracia": 1,
  "diasRevalidacion": 1,
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "formatosPermitidos": [
    "texto"
  ],
  "tamanoMaximoMb": 1,
  "paginasMaximas": 1,
  "modo": "Archivo",
  "campos": [
    {
      "clave": "texto",
      "etiqueta": "texto",
      "tipo": "texto",
      "obligatorio": false,
      "opciones": [
        "texto"
      ],
      "ayuda": "texto"
    }
  ],
  "reglasEfectivas": {
    "formatos": [
      "texto"
    ],
    "tamanoMaximoBytes": 1,
    "paginasMaximas": 1,
    "descripcion": "Texto de ejemplo."
  },
  "plantilla": {
    "version": 1,
    "nombreOriginal": "Nombre de ejemplo",
    "mime": "texto",
    "tamanoBytes": 1,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "cargadaEn": "2026-11-12T15:00:00Z",
    "cargadaPor": "texto",
    "vigente": true,
    "retiradaEn": "2026-11-12T15:00:00Z"
  },
  "cargaSoportada": true
}
```

</details>

### `PUT /api/v1/tipos-documento/{id}`

**Actualizar tipo documento.** Código y canónico son inmutables; los campos nulos conservan su valor. Si cambian la frescura, los avisos o la gracia, las vigencias de los documentos del tipo se recalculan por mensaje (`RecalcularVigenciasDelTipo`).

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `actualizarTipoDocumento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarTipoDocumentoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombre` | texto o null | no |  |
| `ambito` | texto o null | no |  |
| `reglaFrescura` | ReglaFrescuraDto o null | no |  |
| `reglaFrescura.tipo` | texto | sí |  |
| `reglaFrescura.dias` | entero o null | no |  |
| `reglaFrescura.regla` | texto o null | no |  |
| `diasAviso` | lista de entero o null | no |  |
| `diasGracia` | entero o null | no |  |
| `diasRevalidacion` | entero o null | no |  |
| `textoAyuda` | texto o null | no |  |
| `activo` | booleano o null | no |  |
| `formatosPermitidos` | lista de texto o null | no |  |
| `tamanoMaximoMb` | entero o null | no |  |
| `paginasMaximas` | entero o null | no |  |
| `modo` | texto o null | no |  |
| `campos` | lista de CampoFormularioDto o null | no |  |
| `campos[].clave` | texto | sí |  |
| `campos[].etiqueta` | texto | sí |  |
| `campos[].tipo` | texto | sí |  |
| `campos[].obligatorio` | booleano | no | por omisión false |
| `campos[].opciones` | lista de texto o null | no |  |
| `campos[].ayuda` | texto o null | no |  |

**Respuestas**

- `200` OK — TipoDocumentoTenantDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `TipoDocumentoTenantDto` que se detallan en `POST /api/v1/tipos-documento`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/tipos-documento/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Constructora Norte S.A.S.","ambito":"texto","reglaFrescura":{"tipo":"texto","dias":1,"regla":"texto"},"diasAviso":[1],"diasGracia":1,"diasRevalidacion":1,"textoAyuda":"Texto de ejemplo.","activo":true,"formatosPermitidos":["texto"],"tamanoMaximoMb":1,"paginasMaximas":1,"modo":"texto","campos":[{"clave":"texto","etiqueta":"texto","tipo":"texto","obligatorio":false,"opciones":["texto"],"ayuda":"texto"}]}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "tipoCanonico": "texto",
  "versionCanonico": 1,
  "ambito": "texto",
  "clase": "texto",
  "textoAyuda": "Texto de ejemplo.",
  "reglaFrescura": {
    "tipo": "texto",
    "dias": 1,
    "regla": "texto"
  },
  "diasAviso": [
    1
  ],
  "diasGracia": 1,
  "diasRevalidacion": 1,
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "formatosPermitidos": [
    "texto"
  ],
  "tamanoMaximoMb": 1,
  "paginasMaximas": 1,
  "modo": "Archivo",
  "campos": [
    {
      "clave": "texto",
      "etiqueta": "texto",
      "tipo": "texto",
      "obligatorio": false,
      "opciones": [
        "texto"
      ],
      "ayuda": "texto"
    }
  ],
  "reglasEfectivas": {
    "formatos": [
      "texto"
    ],
    "tamanoMaximoBytes": 1,
    "paginasMaximas": 1,
    "descripcion": "Texto de ejemplo."
  },
  "plantilla": {
    "version": 1,
    "nombreOriginal": "Nombre de ejemplo",
    "mime": "texto",
    "tamanoBytes": 1,
    "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
    "cargadaEn": "2026-11-12T15:00:00Z",
    "cargadaPor": "texto",
    "vigente": true,
    "retiradaEn": "2026-11-12T15:00:00Z"
  },
  "cargaSoportada": true
}
```

</details>
