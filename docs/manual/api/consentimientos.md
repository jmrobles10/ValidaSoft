# API · Consentimientos

4 operaciones en 1 recurso.

## Textos legales

Textos legales versionados: aviso de la plataforma y autorización/términos de cada empresa.

### `GET /api/v1/empresas/{id}/textos-legales`

**Obtener textos legales empresa.**

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `obtenerTextosLegalesEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — TextosLegalesEmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `empresaId` | uuid | sí |  |
| `vigentes` | lista de TextoLegalDto | sí |  |
| `vigentes[].id` | uuid | sí |  |
| `vigentes[].ambito` | texto | sí |  |
| `vigentes[].empresaId` | uuid o null | no |  |
| `vigentes[].tipo` | texto | sí |  |
| `vigentes[].version` | entero | sí |  |
| `vigentes[].hash` | texto | sí |  |
| `vigentes[].contenido` | texto | sí |  |
| `vigentes[].publicadoEn` | fecha y hora | sí |  |
| `vigentes[].publicadoPor` | texto | sí |  |
| `faltantes` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/textos-legales" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vigentes": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "ambito": "texto",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": "texto",
      "version": 1,
      "hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "contenido": "Texto de ejemplo.",
      "publicadoEn": "2026-11-12T15:00:00Z",
      "publicadoPor": "texto"
    }
  ],
  "faltantes": [
    "texto"
  ]
}
```

</details>

### `PUT /api/v1/empresas/{id}/textos-legales/{tipo}`

**Publicar texto legal empresa.** `{tipo}`: `autorizacion` | `terminos` (o el nombre del enum). Cada publicación es una versión nueva.

**Permiso configuracion:escribir · API (`/api/v1`) · SDK: `publicarTextoLegalEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `tipo` | path | texto | sí |  |

**Cuerpo de la petición** (`application/json`, PublicarTextoLegalDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `contenido` | texto | sí |  |

**Respuestas**

- `200` OK — TextoLegalDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `ambito` | texto | sí |  |
| `empresaId` | uuid o null | no |  |
| `tipo` | texto | sí |  |
| `version` | entero | sí |  |
| `hash` | texto | sí |  |
| `contenido` | texto | sí |  |
| `publicadoEn` | fecha y hora | sí |  |
| `publicadoPor` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/textos-legales/texto" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"contenido":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "ambito": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipo": "texto",
  "version": 1,
  "hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "contenido": "Texto de ejemplo.",
  "publicadoEn": "2026-11-12T15:00:00Z",
  "publicadoPor": "texto"
}
```

</details>

### `GET /api/v1/plataforma/textos-legales/aviso`

**Obtener aviso plataforma.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerAvisoPlataforma`**

**Respuestas**

- `200` OK — TextoLegalDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `ambito` | texto | sí |  |
| `empresaId` | uuid o null | no |  |
| `tipo` | texto | sí |  |
| `version` | entero | sí |  |
| `hash` | texto | sí |  |
| `contenido` | texto | sí |  |
| `publicadoEn` | fecha y hora | sí |  |
| `publicadoPor` | texto | sí |  |

**Errores de negocio que lanza**

- `404` `texto-legal-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/textos-legales/aviso" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "ambito": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipo": "texto",
  "version": 1,
  "hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "contenido": "Texto de ejemplo.",
  "publicadoEn": "2026-11-12T15:00:00Z",
  "publicadoPor": "texto"
}
```

</details>

### `PUT /api/v1/plataforma/textos-legales/aviso`

**Publicar aviso plataforma.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `publicarAvisoPlataforma`**

**Cuerpo de la petición** (`application/json`, PublicarTextoLegalDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `contenido` | texto | sí |  |

**Respuestas**

- `200` OK — TextoLegalDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `ambito` | texto | sí |  |
| `empresaId` | uuid o null | no |  |
| `tipo` | texto | sí |  |
| `version` | entero | sí |  |
| `hash` | texto | sí |  |
| `contenido` | texto | sí |  |
| `publicadoEn` | fecha y hora | sí |  |
| `publicadoPor` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/plataforma/textos-legales/aviso" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Content-Type: application/json" \
  -d '{"contenido":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "ambito": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tipo": "texto",
  "version": 1,
  "hash": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
  "contenido": "Texto de ejemplo.",
  "publicadoEn": "2026-11-12T15:00:00Z",
  "publicadoPor": "texto"
}
```

</details>
