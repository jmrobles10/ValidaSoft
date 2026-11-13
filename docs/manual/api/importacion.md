# API · Importación

5 operaciones en 1 recurso.

## Importación

Importador genérico de la ficha y sincronizador de coexistencia (F4, FM-39…FM-43; docs/08 §6.6). Rol Plataforma. El sistema de origen es el `{origen}` de la ruta: el código de un adaptador registrado (`IAdaptadorDeOrigen`); sin ese adaptador, 404 `origen-no-registrado`.

### `POST /api/v1/plataforma/importacion/{origen}`

**Importar terceros.** Encola una corrida (202 con su estado): `dryRun`, fuente `Archivo` (con el JSON en `archivo`) o `Conectado` (el origen configurado del adaptador), mapa de unidades ⇒ empresas para las aceptaciones y tamaño de lote. 422 `fuente-invalida`, `archivo-requerido`, `archivo-invalido`, `tamano-lote-invalido`, `unidad-duplicada`, `empresa-no-encontrada`; 404 `origen-no-registrado`; 409 `origen-no-configurado`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `importarTerceros`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `origen` | path | texto | sí |  |

**Cuerpo de la petición** (`application/json`, ImportarTercerosDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `dryRun` | booleano o null | no |  |
| `fuente` | texto o null | no |  |
| `archivo` | cualquiera | no |  |
| `unidades` | lista de UnidadEmpresaDto o null | no |  |
| `unidades[].unidad` | texto | sí |  |
| `unidades[].empresaId` | uuid | sí |  |
| `tamanoLote` | entero o null | no |  |
| `importarCatalogos` | booleano o null | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

**Errores de negocio que lanza**

- `409` `origen-no-configurado`
- `422` `archivo-requerido`
- `422` `empresa-no-encontrada`
- `422` `fuente-invalida`
- `422` `tamano-lote-invalido`
- `422` `unidad-duplicada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/importacion/texto" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"dryRun":true,"fuente":"texto","archivo":null,"unidades":[{"unidad":"texto","empresaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}],"tamanoLote":1,"importarCatalogos":true}'
```

</details>

### `GET /api/v1/plataforma/importacion/{origen}/{id}`

**Obtener importación.** Estado y resumen de la corrida (404 `corrida-no-encontrada`).

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerImportacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `origen` | path | texto | sí |  |
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CorridaImportacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `origen` | texto | sí |  |
| `estado` | texto | sí |  |
| `dryRun` | booleano | sí |  |
| `fuente` | texto | sí |  |
| `tamanoLote` | entero | sí |  |
| `importarCatalogos` | booleano | sí |  |
| `unidades` | lista de UnidadEmpresaDto | sí |  |
| `unidades[].unidad` | texto | sí |  |
| `unidades[].empresaId` | uuid | sí |  |
| `cursor` | texto o null | no |  |
| `lotes` | entero | sí |  |
| `creadaEn` | fecha y hora | sí |  |
| `actualizadaEn` | fecha y hora | sí |  |
| `terminadaEn` | fecha y hora o null | no |  |
| `error` | texto o null | no |  |
| `resumen` | ResumenImportacionDto | sí |  |
| `resumen.tercerosLeidos` | entero | sí |  |
| `resumen.tercerosCreados` | entero | sí |  |
| `resumen.tercerosEncontrados` | entero | sí |  |
| `resumen.tercerosRechazados` | entero | sí |  |
| `resumen.secciones` | lista de ResumenSeccionImportadaDto | sí |  |
| `resumen.secciones[].seccion` | texto | sí |  |
| `resumen.secciones[].creadas` | entero | sí |  |
| `resumen.secciones[].sinCambios` | entero | sí |  |
| `resumen.secciones[].rechazadas` | entero | sí |  |
| `resumen.aceptaciones` | entero | sí |  |
| `resumen.aceptacionesExistentes` | entero | sí |  |
| `resumen.sinVinculo` | entero | sí |  |
| `resumen.catalogos` | lista de ResultadoImportacionCatalogoDto | sí |  |
| `resumen.catalogos[].catalogo` | texto | sí |  |
| `resumen.catalogos[].creados` | entero | sí |  |
| `resumen.catalogos[].actualizados` | entero | sí |  |
| `resumen.catalogos[].sinCambios` | entero | sí |  |
| `resumen.catalogos[].rechazados` | lista de FilaRechazadaDto | sí |  |
| `resumen.catalogos[].rechazados[].fila` | entero | sí |  |
| `resumen.catalogos[].rechazados[].codigo` | texto o null | no |  |
| `resumen.catalogos[].rechazados[].motivo` | texto | sí |  |
| `resumen.advertencias` | entero | sí |  |
| `resumen.rechazos` | entero | sí |  |
| `resumen.detalleAdvertencias` | lista de AvisoImportacionDto | sí |  |
| `resumen.detalleAdvertencias[].claveLegado` | texto | sí |  |
| `resumen.detalleAdvertencias[].clave` | texto | sí |  |
| `resumen.detalleAdvertencias[].seccion` | texto o null | no |  |
| `resumen.detalleAdvertencias[].periodo` | entero o null | no |  |
| `resumen.detalleAdvertencias[].campo` | texto | sí |  |
| `resumen.detalleAdvertencias[].codigo` | texto | sí |  |
| `resumen.detalleAdvertencias[].mensaje` | texto | sí |  |
| `resumen.detalleAdvertencias[].dato` | texto o null | no |  |
| `resumen.detalleAdvertencias[].accionSugerida` | texto o null | no |  |
| `resumen.detalleRechazos` | lista de AvisoImportacionDto | sí | mismos campos que `resumen.detalleAdvertencias[]` |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/importacion/texto/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "origen": "texto",
  "estado": "texto",
  "dryRun": true,
  "fuente": "texto",
  "tamanoLote": 1,
  "importarCatalogos": true,
  "unidades": [
    {
      "unidad": "texto",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "cursor": "texto",
  "lotes": 1,
  "creadaEn": "2026-11-12T15:00:00Z",
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "terminadaEn": "2026-11-12T15:00:00Z",
  "error": "texto",
  "resumen": {
    "tercerosLeidos": 1,
    "tercerosCreados": 1,
    "tercerosEncontrados": 1,
    "tercerosRechazados": 1,
    "secciones": [
      {
        "seccion": "texto",
        "creadas": 1,
        "sinCambios": 1,
        "rechazadas": 1
      }
    ],
    "aceptaciones": 1,
    "aceptacionesExistentes": 1,
    "sinVinculo": 1,
    "catalogos": [
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
    ],
    "advertencias": 1,
    "rechazos": 1,
    "detalleAdvertencias": [
      {
        "claveLegado": "texto",
        "clave": "texto",
        "seccion": "texto",
        "periodo": 1,
        "campo": "texto",
        "codigo": "CODIGO_EJEMPLO",
        "mensaje": "Texto de ejemplo.",
        "dato": "texto",
        "accionSugerida": "texto"
      }
    ],
    "detalleRechazos": [
      {
        "claveLegado": "texto",
        "clave": "texto",
        "seccion": "texto",
        "periodo": 1,
        "campo": "texto",
        "codigo": "CODIGO_EJEMPLO",
        "mensaje": "Texto de ejemplo.",
        "dato": "texto",
        "accionSugerida": "texto"
      }
    ]
  }
}
```

</details>

### `POST /api/v1/plataforma/importacion/{origen}/{id}/reanudar`

**Reanudar importación.** Vuelve a encolar una corrida fallida o interrumpida; sigue desde su cursor (202). 409 `corrida-completada`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `reanudarImportacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `origen` | path | texto | sí |  |
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

**Errores de negocio que lanza**

- `409` `corrida-completada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/importacion/texto/3fa85f64-5717-4562-b3fc-2c963f66afa6/reanudar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

</details>

### `GET /api/v1/plataforma/importacion/{origen}/{id}/rechazos`

**Descargar rechazos importación.** CSV de rechazos (UTF-8 con BOM, separado por comas): `claveLegado, clave, seccion, periodo, campo, codigo, motivo, dato, accionSugerida`, una fila por dato que no se pudo importar.

**Operador de Plataforma · API (`/api/v1`) · SDK: `descargarRechazosImportacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `origen` | path | texto | sí |  |
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/importacion/texto/3fa85f64-5717-4562-b3fc-2c963f66afa6/rechazos" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

</details>

### `POST /api/v1/plataforma/importacion/{origen}/terceros/{clave}/sincronizar`

**Sincronizar tercero de origen.** Sincronizador de coexistencia (FM-11, FM-43), sistema de origen ⇒ Validasoft, un solo sentido: reimporta un tercero desde el origen configurado del adaptador con su mismo traductor. Si una sección cambió nace una versión con origen `SistemaExterno` y, en la misma transacción, se piden las sincronizaciones de requisitos de F2: la empresa que ya la aceptó abre su revalidación. Sin cambios no hace nada (idempotente). No importa catálogos ni acepta: usa los catálogos de plataforma ya cargados. 404 `origen-no-registrado`, `tercero-origen-no-encontrado`; 409 `origen-no-configurado`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `sincronizarTerceroDeOrigen`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `origen` | path | texto | sí |  |
| `clave` | path | texto | sí |  |

**Respuestas**

- `200` OK — SincronizacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `origen` | texto | sí |  |
| `clave` | texto | sí |  |
| `claveLegado` | texto | sí |  |
| `terceroId` | uuid o null | no |  |
| `terceroCreado` | booleano | sí |  |
| `secciones` | lista de SeccionSincronizadaDto | sí |  |
| `secciones[].seccion` | texto | sí |  |
| `secciones[].periodo` | entero o null | no |  |
| `secciones[].version` | entero o null | no |  |
| `secciones[].creada` | booleano | sí |  |
| `secciones[].rechazada` | booleano | sí |  |
| `advertencias` | lista de AvisoImportacionDto | sí |  |
| `advertencias[].claveLegado` | texto | sí |  |
| `advertencias[].clave` | texto | sí |  |
| `advertencias[].seccion` | texto o null | no |  |
| `advertencias[].periodo` | entero o null | no |  |
| `advertencias[].campo` | texto | sí |  |
| `advertencias[].codigo` | texto | sí |  |
| `advertencias[].mensaje` | texto | sí |  |
| `advertencias[].dato` | texto o null | no |  |
| `advertencias[].accionSugerida` | texto o null | no |  |
| `rechazos` | lista de AvisoImportacionDto | sí | mismos campos que `advertencias[]` |

**Errores de negocio que lanza**

- `404` `tercero-origen-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/importacion/texto/terceros/texto/sincronizar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "origen": "texto",
  "clave": "texto",
  "claveLegado": "texto",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "terceroCreado": true,
  "secciones": [
    {
      "seccion": "texto",
      "periodo": 1,
      "version": 1,
      "creada": true,
      "rechazada": true
    }
  ],
  "advertencias": [
    {
      "claveLegado": "texto",
      "clave": "texto",
      "seccion": "texto",
      "periodo": 1,
      "campo": "texto",
      "codigo": "CODIGO_EJEMPLO",
      "mensaje": "Texto de ejemplo.",
      "dato": "texto",
      "accionSugerida": "texto"
    }
  ],
  "rechazos": [
    {
      "claveLegado": "texto",
      "clave": "texto",
      "seccion": "texto",
      "periodo": 1,
      "campo": "texto",
      "codigo": "CODIGO_EJEMPLO",
      "mensaje": "Texto de ejemplo.",
      "dato": "texto",
      "accionSugerida": "texto"
    }
  ]
}
```

</details>
