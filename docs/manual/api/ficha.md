# API · Ficha

12 operaciones en 6 recursos.

## Catálogos de ficha

Catálogos de plataforma de la ficha (FM-10, FM-22): importación por Plataforma y búsqueda desde la web y el portal.

### `GET /api/v1/catalogos/{catalogo}`

**Buscar en catálogo.** Busca en un catálogo (`q`: prefijo de código o parte del nombre sin tildes; `padre`; hasta 200 por página).

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `buscarEnCatalogo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `catalogo` | path | texto | sí |  |
| `q` | query | texto | no |  |
| `padre` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfEntradaCatalogoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de EntradaCatalogoDto | sí |  |
| `datos[].codigo` | texto | sí |  |
| `datos[].nombre` | texto | sí |  |
| `datos[].padre` | texto o null | no |  |
| `datos[].nivel` | entero o null | no |  |
| `datos[].departamento` | texto o null | no |  |
| `datos[].pais` | texto o null | no |  |
| `datos[].activo` | booleano | no | por omisión true |
| `datos[].swift` | texto o null | no |  |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/catalogos/texto" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S.",
      "padre": "texto",
      "nivel": 1,
      "departamento": "texto",
      "pais": "CO",
      "activo": true,
      "swift": "texto"
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `POST /api/v1/plataforma/catalogos/{catalogo}/importar`

**Importar catálogo.** Importa filas a un catálogo (`ciudades`, `actividades-economicas`, `bancos`, `responsabilidades-dian`, `especialidades`): upsert por código, idempotente. 404 `catalogo-no-encontrado`; 422 `filas-requeridas`, `demasiadas-filas`. Las filas inválidas no detienen el lote: vuelven en `rechazados`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `importarCatalogo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `catalogo` | path | texto | sí |  |

**Cuerpo de la petición** (`application/json`, ImportarCatalogoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `filas` | lista de FilaCatalogoDto o null | no |  |
| `filas[].codigo` | texto o null | no |  |
| `filas[].nombre` | texto o null | no |  |
| `filas[].departamento` | texto o null | no |  |
| `filas[].pais` | texto o null | no |  |
| `filas[].padre` | texto o null | no |  |
| `filas[].nivel` | entero o null | no |  |
| `filas[].activo` | booleano o null | no |  |
| `filas[].swift` | texto o null | no |  |

**Respuestas**

- `200` OK — ResultadoImportacionCatalogoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `catalogo` | texto | sí |  |
| `creados` | entero | sí |  |
| `actualizados` | entero | sí |  |
| `sinCambios` | entero | sí |  |
| `rechazados` | lista de FilaRechazadaDto | sí |  |
| `rechazados[].fila` | entero | sí |  |
| `rechazados[].codigo` | texto o null | no |  |
| `rechazados[].motivo` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/catalogos/texto/importar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"filas":[{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S.","departamento":"texto","pais":"CO","padre":"texto","nivel":1,"activo":true,"swift":"texto"}]}'
```

Respuesta `200`:

```json
{
  "catalogo": "texto",
  "creados": 1,
  "actualizados": 1,
  "sinCambios": 1,
  "rechazados": [
    {
      "fila": 1,
      "codigo": "CODIGO_EJEMPLO",
      "motivo": "Texto de ejemplo."
    }
  ]
}
```

</details>

### `GET /public/v1/catalogos/{catalogo}`

**Buscar en catálogo público.** La misma búsqueda para el portal del tercero (sesión del tercero).

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `buscarEnCatalogoPublico`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `catalogo` | path | texto | sí |  |
| `q` | query | texto | no |  |
| `padre` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfEntradaCatalogoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de EntradaCatalogoDto | sí |  |
| `datos[].codigo` | texto | sí |  |
| `datos[].nombre` | texto | sí |  |
| `datos[].padre` | texto o null | no |  |
| `datos[].nivel` | entero o null | no |  |
| `datos[].departamento` | texto o null | no |  |
| `datos[].pais` | texto o null | no |  |
| `datos[].activo` | booleano | no | por omisión true |
| `datos[].swift` | texto o null | no |  |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/catalogos/texto" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S.",
      "padre": "texto",
      "nivel": 1,
      "departamento": "texto",
      "pais": "CO",
      "activo": true,
      "swift": "texto"
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

## Declaraciones PEP

PDF de la declaración jurada PEP de una persona del tercero (HU 49093), para la empresa que lo tiene como proveedor: el comprador y el oficial de cumplimiento lo descargan junto a la sección de la ficha. Solo existe si la persona respondió «Sí» a «¿Es PEP?» y hay declaración en esa versión (la inactiva no se entrega). Cada descarga queda registrada como exportación.

### `GET /api/v1/terceros/{terceroId}/declaraciones-pep/pdf`

**Descargar declaración PEP.** `GET /api/v1/terceros/{terceroId}/declaraciones-pep/pdf?seccion=FICHA_GENERAL|FICHA_LEGAL&persona={índice}&version={n}`. Sin `version`, la vigente. Con vínculo con el tercero y solo versiones que la empresa puede ver: la vigente, la que aceptó y las que revisa en sus expedientes. 404 si no hay declaración activa de esa persona en esa versión.

**Permiso terceros:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `descargarDeclaracionPep`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `terceroId` | path | uuid | sí |  |
| `seccion` | query | texto | no |  |
| `persona` | query | entero | no |  |
| `version` | query | entero | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

**Errores de negocio que lanza**

- `404` `tercero-no-encontrado`
- `422` `seccion-sin-declaracion-pep`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/terceros/3fa85f64-5717-4562-b3fc-2c963f66afa6/declaraciones-pep/pdf" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>

## Declaraciones PEP del tercero

El tercero descarga el PDF de su propia declaración jurada PEP (HU 49093, DP-16): solo la de la versión vigente de su ficha, solo de quien responde «Sí» a «¿Es PEP?» y tiene declaración, y solo la suya (el tercero sale de su sesión: no hay forma de pedir la de otro, ni siquiera con un identificador cambiado). El PDF no está dirigido a ninguna empresa: lleva el nombre y el logo de Validasoft. Cada descarga queda en `AccesoADeclaracionPep`.

### `GET /public/v1/ficha/{seccion}/declaracion-pep/pdf`

**Descargar declaración PEP propia.** `GET /public/v1/ficha/{seccion}/declaracion-pep/pdf?persona={índice}` (`FICHA_GENERAL` sin índice, `FICHA_LEGAL` con el de la persona en `personas`). 404 `declaracion-pep-no-encontrada` si no hay declaración activa de esa persona en la versión vigente.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `descargarDeclaracionPepPropia`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `seccion` | path | texto | sí |  |
| `persona` | query | entero | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

**Errores de negocio que lanza**

- `404` `declaracion-pep-no-encontrada`
- `422` `seccion-sin-declaracion-pep`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/ficha/texto/declaracion-pep/pdf" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

</details>

## Ficha maestra

La ficha maestra vista por el tercero (sesión pública, FM-19): el estado de cada sección para el wizard, la versión vigente de una sección y el guardado (parcial permitido) que responde la siguiente sección del wizard.

### `GET /public/v1/ficha`

**Obtener ficha.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerFicha`**

**Respuestas**

- `200` OK — EstadoFichaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `terceroId` | uuid | sí |  |
| `tipoPersona` | texto | sí |  |
| `orden` | lista de texto | sí |  |
| `siguienteSeccion` | texto o null | no |  |
| `completa` | booleano | sí |  |
| `secciones` | lista de EstadoSeccionFichaDto | sí |  |
| `secciones[].seccion` | texto | sí |  |
| `secciones[].nombre` | texto | sí |  |
| `secciones[].periodo` | entero o null | no |  |
| `secciones[].obligatoria` | booleano | sí |  |
| `secciones[].version` | entero o null | no |  |
| `secciones[].completa` | booleano | sí |  |
| `secciones[].faltantes` | lista de FaltanteSeccionDto | sí |  |
| `secciones[].faltantes[].campo` | texto o null | no |  |
| `secciones[].faltantes[].regla` | texto o null | no |  |
| `secciones[].faltantes[].mensaje` | texto | sí |  |
| `secciones[].reglaVersion` | entero o null | no |  |
| `secciones[].actualizada` | fecha y hora o null | no |  |
| `secciones[].soportesRetirados` | lista de SoporteSeccionDto o null | no |  |
| `secciones[].soportesRetirados[].documentoId` | uuid | sí |  |
| `secciones[].soportesRetirados[].version` | entero | sí |  |
| `secciones[].soportesRetirados[].tipoDocumento` | texto o null | no |  |
| `secciones[].soportesRetirados[].retirado` | booleano | no | por omisión false |
| `secciones[].soportesRetirados[].nombre` | texto o null | no |  |
| `observaciones` | lista de ObservacionDeSeccionDto o null | no |  |
| `observaciones[].empresa` | texto | sí |  |
| `observaciones[].expedienteId` | uuid | sí |  |
| `observaciones[].seccion` | texto | sí |  |
| `observaciones[].periodo` | entero o null | no |  |
| `observaciones[].observacion` | ObservacionPublicaDto | sí |  |
| `observaciones[].observacion.motivos` | lista de MotivoPublicoDto | sí |  |
| `observaciones[].observacion.motivos[].codigo` | texto | sí |  |
| `observaciones[].observacion.motivos[].texto` | texto | sí |  |
| `observaciones[].observacion.observacion` | texto o null | no |  |
| `observaciones[].observacion.fecha` | fecha y hora | sí |  |
| `observaciones[].observacion.requisito` | texto o null | no |  |
| `observaciones[].observacion.campos` | lista de texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/ficha" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipoPersona": "texto",
  "orden": [
    "texto"
  ],
  "siguienteSeccion": "texto",
  "completa": true,
  "secciones": [
    {
      "seccion": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "periodo": 1,
      "obligatoria": true,
      "version": 1,
      "completa": true,
      "faltantes": [
        {
          "campo": "texto",
          "regla": "texto",
          "mensaje": "Texto de ejemplo."
        }
      ],
      "reglaVersion": 1,
      "actualizada": "2026-11-12T15:00:00Z",
      "soportesRetirados": [
        {
          "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "version": 1,
          "tipoDocumento": "texto",
          "retirado": false,
          "nombre": "Constructora Norte S.A.S."
        }
      ]
    }
  ],
  "observaciones": [
    {
      "empresa": "texto",
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "seccion": "texto",
      "periodo": 1,
      "observacion": {
        "motivos": [
          {
            "codigo": "CODIGO_EJEMPLO",
            "texto": "Texto de ejemplo."
          }
        ],
        "observacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z",
        "requisito": "texto",
        "campos": [
          "texto"
        ]
      }
    }
  ]
}
```

</details>

### `GET /public/v1/ficha/{seccion}`

**Obtener seccion ficha.** Versión vigente de la sección (datos, soportes y completitud) o vacía. 404 `seccion-no-encontrada`; la financiera exige `?periodo=`.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerSeccionFicha`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `seccion` | path | texto | sí |  |
| `periodo` | query | entero | no |  |

**Respuestas**

- `200` OK — SeccionFichaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `seccion` | texto | sí |  |
| `nombre` | texto | sí |  |
| `periodo` | entero o null | no |  |
| `version` | entero o null | no |  |
| `datos` | JsonElement o null | no |  |
| `soportes` | lista de SoporteSeccionDto | sí |  |
| `soportes[].documentoId` | uuid | sí |  |
| `soportes[].version` | entero | sí |  |
| `soportes[].tipoDocumento` | texto o null | no |  |
| `soportes[].retirado` | booleano | no | por omisión false |
| `soportes[].nombre` | texto o null | no |  |
| `completitud` | CompletitudSeccionDto o null | no |  |
| `completitud.completa` | booleano | sí |  |
| `completitud.faltantes` | lista de FaltanteSeccionDto | sí |  |
| `completitud.faltantes[].campo` | texto o null | no |  |
| `completitud.faltantes[].regla` | texto o null | no |  |
| `completitud.faltantes[].mensaje` | texto | sí |  |
| `completitud.reglaVersion` | entero | sí |  |
| `huella` | texto o null | no |  |
| `origen` | texto o null | no |  |
| `fecha` | fecha y hora o null | no |  |
| `versiones` | entero | sí |  |
| `esquemaVersion` | entero | sí |  |
| `aceptaciones` | lista de AceptacionDeSeccionDto o null | no |  |
| `aceptaciones[].vinculoId` | uuid | sí |  |
| `aceptaciones[].versionAceptada` | entero o null | no |  |
| `aceptaciones[].aceptadaEn` | fecha y hora o null | no |  |
| `aceptaciones[].expedienteId` | uuid o null | no |  |
| `aceptaciones[].folio` | texto o null | no |  |
| `aceptaciones[].versionNuevaPendiente` | booleano | sí |  |
| `aceptaciones[].revalidacion` | RevalidacionDeSeccionDto o null | no |  |
| `aceptaciones[].revalidacion.expedienteId` | uuid | sí |  |
| `aceptaciones[].revalidacion.folio` | texto | sí |  |
| `aceptaciones[].revalidacion.estado` | EstadoRequisito (texto) | sí | valores: Pendiente · Cargado · EnRevision · Observado · Aprobado · Eximido |
| `aceptaciones[].revalidacion.version` | entero o null | no |  |
| `aceptaciones[].revalidacion.plazoHasta` | fecha y hora | sí |  |
| `aceptaciones[].origen` | OrigenDeAceptacion (texto) | no | valores: Expediente · Importacion |
| `prellenado` | cualquiera | no |  |
| `sinSoporte` | SinSoporteDto o null | no |  |
| `sinSoporte.critico` | booleano | sí |  |
| `sinSoporte.cambios` | lista de CambioDeCampoDto | sí |  |
| `sinSoporte.cambios[].campo` | texto | sí |  |
| `sinSoporte.cambios[].etiqueta` | texto | sí |  |
| `sinSoporte.cambios[].anterior` | texto o null | no |  |
| `sinSoporte.cambios[].nuevo` | texto o null | no |  |
| `sinSoporte.cambios[].tipoCambio` | TipoCambio (texto) | sí | valores: Agregado · Quitado · Modificado |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/ficha/texto" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
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
```

</details>

### `PUT /public/v1/ficha/{seccion}`

**Guardar seccion ficha.** Guarda una versión si cambiaron los datos o los soportes (misma huella ⇒ 200 con `creada: false`). 422 de formato con `campo` (y `errores`): `campo-desconocido`, `valor-invalido`, `codigo-invalido`, `identificacion-no-editable`, `dv-no-editable`, `persona-duplicada`, los de soportes y los de periodo. Lo incompleto se guarda (borrador) con sus faltantes.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `guardarSeccionFicha`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `seccion` | path | texto | sí |  |
| `periodo` | query | entero | no |  |

**Cuerpo de la petición** (`application/json`, GuardarSeccionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | JsonElement o null | no |  |
| `soportes` | lista de SoporteSeccionDto o null | no |  |
| `soportes[].documentoId` | uuid | sí |  |
| `soportes[].version` | entero | sí |  |
| `soportes[].tipoDocumento` | texto o null | no |  |
| `soportes[].retirado` | booleano | no | por omisión false |
| `soportes[].nombre` | texto o null | no |  |

**Respuestas**

- `200` OK — SeccionGuardadaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `version` | entero | sí |  |
| `creada` | booleano | sí |  |
| `completitud` | CompletitudSeccionDto | sí |  |
| `completitud.completa` | booleano | sí |  |
| `completitud.faltantes` | lista de FaltanteSeccionDto | sí |  |
| `completitud.faltantes[].campo` | texto o null | no |  |
| `completitud.faltantes[].regla` | texto o null | no |  |
| `completitud.faltantes[].mensaje` | texto | sí |  |
| `completitud.reglaVersion` | entero | sí |  |
| `siguienteSeccion` | texto o null | no |  |
| `seccion` | SeccionFichaDto | sí |  |
| `seccion.seccion` | texto | sí |  |
| `seccion.nombre` | texto | sí |  |
| `seccion.periodo` | entero o null | no |  |
| `seccion.version` | entero o null | no |  |
| `seccion.datos` | JsonElement o null | no |  |
| `seccion.soportes` | lista de SoporteSeccionDto | sí |  |
| `seccion.soportes[].documentoId` | uuid | sí |  |
| `seccion.soportes[].version` | entero | sí |  |
| `seccion.soportes[].tipoDocumento` | texto o null | no |  |
| `seccion.soportes[].retirado` | booleano | no | por omisión false |
| `seccion.soportes[].nombre` | texto o null | no |  |
| `seccion.completitud` | CompletitudSeccionDto o null | no | mismos campos que `completitud` |
| `seccion.huella` | texto o null | no |  |
| `seccion.origen` | texto o null | no |  |
| `seccion.fecha` | fecha y hora o null | no |  |
| `seccion.versiones` | entero | sí |  |
| `seccion.esquemaVersion` | entero | sí |  |
| `seccion.aceptaciones` | lista de AceptacionDeSeccionDto o null | no |  |
| `seccion.aceptaciones[].vinculoId` | uuid | sí |  |
| `seccion.aceptaciones[].versionAceptada` | entero o null | no |  |
| `seccion.aceptaciones[].aceptadaEn` | fecha y hora o null | no |  |
| `seccion.aceptaciones[].expedienteId` | uuid o null | no |  |
| `seccion.aceptaciones[].folio` | texto o null | no |  |
| `seccion.aceptaciones[].versionNuevaPendiente` | booleano | sí |  |
| `seccion.aceptaciones[].revalidacion` | RevalidacionDeSeccionDto o null | no |  |
| `seccion.aceptaciones[].revalidacion.expedienteId` | uuid | sí |  |
| `seccion.aceptaciones[].revalidacion.folio` | texto | sí |  |
| `seccion.aceptaciones[].revalidacion.estado` | EstadoRequisito (texto) | sí | valores: Pendiente · Cargado · EnRevision · Observado · Aprobado · Eximido |
| `seccion.aceptaciones[].revalidacion.version` | entero o null | no |  |
| `seccion.aceptaciones[].revalidacion.plazoHasta` | fecha y hora | sí |  |
| `seccion.aceptaciones[].origen` | OrigenDeAceptacion (texto) | no | valores: Expediente · Importacion |
| `seccion.prellenado` | cualquiera | no |  |
| `seccion.sinSoporte` | SinSoporteDto o null | no |  |
| `seccion.sinSoporte.critico` | booleano | sí |  |
| `seccion.sinSoporte.cambios` | lista de CambioDeCampoDto | sí |  |
| `seccion.sinSoporte.cambios[].campo` | texto | sí |  |
| `seccion.sinSoporte.cambios[].etiqueta` | texto | sí |  |
| `seccion.sinSoporte.cambios[].anterior` | texto o null | no |  |
| `seccion.sinSoporte.cambios[].nuevo` | texto o null | no |  |
| `seccion.sinSoporte.cambios[].tipoCambio` | TipoCambio (texto) | sí | valores: Agregado · Quitado · Modificado |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/public/v1/ficha/texto" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"datos":{},"soportes":[{"documentoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","version":1,"tipoDocumento":"texto","retirado":false,"nombre":"Constructora Norte S.A.S."}]}'
```

Respuesta `200`:

```json
{
  "version": 1,
  "creada": true,
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
  "siguienteSeccion": "texto",
  "seccion": {
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
}
```

</details>

### `POST /public/v1/ficha/{seccion}/evaluacion`

**Evaluar seccion ficha.** Completitud que tendría la sección con estos datos y soportes, sin guardarla (FM-46): con ella el wizard habilita «Guardar y continuar» solo cuando la sección quedaría completa. Mismos 422 de formato que el guardado. Tiene su propio cupo en el límite público, porque el wizard la pide mientras el tercero escribe.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `evaluarSeccionFicha`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `seccion` | path | texto | sí |  |
| `periodo` | query | entero | no |  |

**Cuerpo de la petición** (`application/json`, GuardarSeccionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | JsonElement o null | no |  |
| `soportes` | lista de SoporteSeccionDto o null | no |  |
| `soportes[].documentoId` | uuid | sí |  |
| `soportes[].version` | entero | sí |  |
| `soportes[].tipoDocumento` | texto o null | no |  |
| `soportes[].retirado` | booleano | no | por omisión false |
| `soportes[].nombre` | texto o null | no |  |

**Respuestas**

- `200` OK — CompletitudSeccionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `completa` | booleano | sí |  |
| `faltantes` | lista de FaltanteSeccionDto | sí |  |
| `faltantes[].campo` | texto o null | no |  |
| `faltantes[].regla` | texto o null | no |  |
| `faltantes[].mensaje` | texto | sí |  |
| `reglaVersion` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/ficha/texto/evaluacion" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"datos":{},"soportes":[{"documentoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","version":1,"tipoDocumento":"texto","retirado":false,"nombre":"Constructora Norte S.A.S."}]}'
```

Respuesta `200`:

```json
{
  "completa": true,
  "faltantes": [
    {
      "campo": "texto",
      "regla": "texto",
      "mensaje": "Texto de ejemplo."
    }
  ],
  "reglaVersion": 1
}
```

</details>

## Propuestas de ficha

Actualizar desde el documento (F5, FM-14): lo que la versión actual de cada documento global del tercero que respalda secciones (RUT, Cámara, estados financieros, cédula) propone para ellas, con los campos que extrajo su análisis de IA. Solo lectura: el tercero confirma o corrige en el wizard y guarda como siempre (con el documento como soporte), lo que abre la revalidación por sección en cada empresa (F2). El tercero ve los valores leídos de su propio documento, nunca la confianza, las alertas, el costo ni quién pagó el análisis. Sin análisis (sin plan, sin cupo o fallido), la propuesta no está disponible y el tercero diligencia con el documento a la vista.

### `GET /public/v1/ficha/{seccion}/propuestas`

**Obtener propuestas seccion.** Las propuestas para una sección (y periodo, en la financiera), con los datos de la sección ya cambiados para prellenar el formulario.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerPropuestasSeccion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `seccion` | path | texto | sí |  |
| `periodo` | query | entero | no |  |

**Respuestas**

- `200` OK — lista de PropuestaDeSeccionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de PropuestaDeSeccionDto | sí |  |
| `[].seccion` | texto | sí |  |
| `[].nombreSeccion` | texto | sí |  |
| `[].periodo` | entero o null | no |  |
| `[].documento` | DocumentoDePropuestaDto | sí |  |
| `[].documento.documentoId` | uuid | sí |  |
| `[].documento.version` | entero | sí |  |
| `[].documento.tipoDocumento` | texto | sí |  |
| `[].documento.nombreDocumento` | texto | sí |  |
| `[].documento.cargadaEn` | fecha y hora | sí |  |
| `[].disponible` | booleano | sí |  |
| `[].motivo` | texto o null | no |  |
| `[].campos` | lista de CampoPropuestoDto | sí |  |
| `[].campos[].campo` | texto | sí |  |
| `[].campos[].etiqueta` | texto | sí |  |
| `[].campos[].actual` | texto o null | no |  |
| `[].campos[].propuesto` | texto o null | no |  |
| `[].datos` | cualquiera | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/ficha/texto/propuestas" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
[
  {
    "seccion": "texto",
    "nombreSeccion": "Nombre de ejemplo",
    "periodo": 1,
    "documento": {
      "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "version": 1,
      "tipoDocumento": "texto",
      "nombreDocumento": "Nombre de ejemplo",
      "cargadaEn": "2026-11-12T15:00:00Z"
    },
    "disponible": true,
    "motivo": "Texto de ejemplo.",
    "campos": [
      {
        "campo": "texto",
        "etiqueta": "texto",
        "actual": "texto",
        "propuesto": "texto"
      }
    ],
    "datos": null
  }
]
```

</details>

### `GET /public/v1/ficha/propuestas`

**Listar propuestas ficha.** Resumen para «Mi información»: una fila por documento y sección con cambios (o sin lectura automática y más nuevo que la sección).

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `listarPropuestasFicha`**

**Respuestas**

- `200` OK — lista de PropuestaDeSeccionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de PropuestaDeSeccionDto | sí |  |
| `[].seccion` | texto | sí |  |
| `[].nombreSeccion` | texto | sí |  |
| `[].periodo` | entero o null | no |  |
| `[].documento` | DocumentoDePropuestaDto | sí |  |
| `[].documento.documentoId` | uuid | sí |  |
| `[].documento.version` | entero | sí |  |
| `[].documento.tipoDocumento` | texto | sí |  |
| `[].documento.nombreDocumento` | texto | sí |  |
| `[].documento.cargadaEn` | fecha y hora | sí |  |
| `[].disponible` | booleano | sí |  |
| `[].motivo` | texto o null | no |  |
| `[].campos` | lista de CampoPropuestoDto | sí |  |
| `[].campos[].campo` | texto | sí |  |
| `[].campos[].etiqueta` | texto | sí |  |
| `[].campos[].actual` | texto o null | no |  |
| `[].campos[].propuesto` | texto o null | no |  |
| `[].datos` | cualquiera | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/ficha/propuestas" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
[
  {
    "seccion": "texto",
    "nombreSeccion": "Nombre de ejemplo",
    "periodo": 1,
    "documento": {
      "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "version": 1,
      "tipoDocumento": "texto",
      "nombreDocumento": "Nombre de ejemplo",
      "cargadaEn": "2026-11-12T15:00:00Z"
    },
    "disponible": true,
    "motivo": "Texto de ejemplo.",
    "campos": [
      {
        "campo": "texto",
        "etiqueta": "texto",
        "actual": "texto",
        "propuesto": "texto"
      }
    ],
    "datos": null
  }
]
```

</details>

## Recálculo de fichas

Recalcular la completitud de todas las fichas con las reglas vigentes (rol Plataforma).

### `POST /api/v1/plataforma/fichas/recalcular`

**Recalcular fichas.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `recalcularFichas`**

**Respuestas**

- `200` OK — RecalculoFichasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `secciones` | entero | sí |  |
| `recalculadas` | entero | sí |  |
| `terceros` | entero | sí |  |
| `reglas` | lista de ReglaSeccionDto | sí |  |
| `reglas[].seccion` | texto | sí |  |
| `reglas[].reglaVersion` | entero | sí |  |
| `reglas[].esquemaVersion` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/fichas/recalcular" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "secciones": 1,
  "recalculadas": 1,
  "terceros": 1,
  "reglas": [
    {
      "seccion": "texto",
      "reglaVersion": 1,
      "esquemaVersion": 1
    }
  ]
}
```

</details>
