# API · Sandbox

6 operaciones en 1 recurso.

## Sandbox

El sandbox del grupo y su buzón de correo (SB-1…SB-6, docs/08 §2.2). Funcionan con las credenciales de producción y con las de prueba del mismo grupo, siempre sobre el sandbox: nunca tocan datos de producción. Piden `sandbox:administrar`.

### `GET /api/v1/sandbox`

**Obtener sandbox.** Estado del sandbox: si existe, sus empresas (con el id que se usa en `X-Empresa` con una llave de prueba), los códigos de sus sistemas y cuántos correos tiene en el buzón. Sin sandbox responde `existe: false`.

**Permiso sandbox:administrar · API (`/api/v1`) · SDK: `obtenerSandbox`**

**Respuestas**

- `200` OK — SandboxDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `existe` | booleano | sí |  |
| `entorno` | texto | sí |  |
| `tenantId` | uuid o null | no |  |
| `creadoEn` | fecha y hora o null | no |  |
| `reiniciadoEn` | fecha y hora o null | no |  |
| `empresas` | lista de EmpresaDeSandboxDto | sí |  |
| `empresas[].id` | uuid | sí |  |
| `empresas[].produccionId` | uuid | sí |  |
| `empresas[].nombre` | texto | sí |  |
| `empresas[].prefijoFolio` | texto | sí |  |
| `empresas[].activa` | booleano | sí |  |
| `sistemas` | lista de texto | sí |  |
| `correos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/sandbox" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "existe": true,
  "entorno": "texto",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "creadoEn": "2026-11-12T15:00:00Z",
  "reiniciadoEn": "2026-11-12T15:00:00Z",
  "empresas": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "produccionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "prefijoFolio": "texto",
      "activa": true
    }
  ],
  "sistemas": [
    "texto"
  ],
  "correos": 1
}
```

</details>

### `POST /api/v1/sandbox`

**Asegurar sandbox.** Crea el sandbox —un tenant hermano con la configuración del grupo y ningún dato— o, si ya existe, le agrega lo que le falte de producción (empresas nuevas, flujos…) sin tocar lo que ya tiene. 201 al crearlo, 200 si ya existía. 409 `tenant-inactivo`. Emitir la primera API key de prueba también lo crea.

**Permiso sandbox:administrar · API (`/api/v1`) · SDK: `asegurarSandbox`**

**Respuestas**

- `200` OK — SandboxDto
- `201` Creado — SandboxDto

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `existe` | booleano | sí |  |
| `entorno` | texto | sí |  |
| `tenantId` | uuid o null | no |  |
| `creadoEn` | fecha y hora o null | no |  |
| `reiniciadoEn` | fecha y hora o null | no |  |
| `empresas` | lista de EmpresaDeSandboxDto | sí |  |
| `empresas[].id` | uuid | sí |  |
| `empresas[].produccionId` | uuid | sí |  |
| `empresas[].nombre` | texto | sí |  |
| `empresas[].prefijoFolio` | texto | sí |  |
| `empresas[].activa` | booleano | sí |  |
| `sistemas` | lista de texto | sí |  |
| `correos` | entero | sí |  |

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `existe` | booleano | sí |  |
| `entorno` | texto | sí |  |
| `tenantId` | uuid o null | no |  |
| `creadoEn` | fecha y hora o null | no |  |
| `reiniciadoEn` | fecha y hora o null | no |  |
| `empresas` | lista de EmpresaDeSandboxDto | sí |  |
| `empresas[].id` | uuid | sí |  |
| `empresas[].produccionId` | uuid | sí |  |
| `empresas[].nombre` | texto | sí |  |
| `empresas[].prefijoFolio` | texto | sí |  |
| `empresas[].activa` | booleano | sí |  |
| `sistemas` | lista de texto | sí |  |
| `correos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/sandbox" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "existe": true,
  "entorno": "texto",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "creadoEn": "2026-11-12T15:00:00Z",
  "reiniciadoEn": "2026-11-12T15:00:00Z",
  "empresas": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "produccionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "prefijoFolio": "texto",
      "activa": true
    }
  ],
  "sistemas": [
    "texto"
  ],
  "correos": 1
}
```

</details>

### `GET /api/v1/sandbox/correos`

**Listar correos de prueba.** Los correos que el sandbox habría enviado, los más nuevos primero. `para`: solo los de ese destinatario; `asunto`: solo los que lo contienen. `limite` por omisión 50 (máximo 200); `cursor` es el opaco de la página anterior. 404 `sandbox-no-existe`.

**Permiso sandbox:administrar · API (`/api/v1`) · SDK: `listarCorreosDePrueba`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `para` | query | texto | no |  |
| `asunto` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfCorreoDePruebaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de CorreoDePruebaDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].para` | texto | sí |  |
| `datos[].asunto` | texto | sí |  |
| `datos[].texto` | texto | sí |  |
| `datos[].html` | texto | sí |  |
| `datos[].enlaces` | lista de texto | sí |  |
| `datos[].recibidoEn` | fecha y hora | sí |  |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/sandbox/correos" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "para": "texto",
      "asunto": "texto",
      "texto": "Texto de ejemplo.",
      "html": "texto",
      "enlaces": [
        "https://ejemplo.co/recurso"
      ],
      "recibidoEn": "2026-11-12T15:00:00Z"
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `DELETE /api/v1/sandbox/correos`

**Vaciar correos de prueba.** Vacía el buzón (los correos no se recuperan). 204.

**Permiso sandbox:administrar · API (`/api/v1`) · SDK: `vaciarCorreosDePrueba`**

**Respuestas**

- `204` Sin contenido

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X DELETE "https://{host}/api/v1/sandbox/correos" \
  -H "X-Api-Key: $API_KEY"
```

</details>

### `GET /api/v1/sandbox/correos/{id}`

**Obtener correo de prueba.** Un correo del buzón. 404 `correo-no-encontrado`.

**Permiso sandbox:administrar · API (`/api/v1`) · SDK: `obtenerCorreoDePrueba`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — CorreoDePruebaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `para` | texto | sí |  |
| `asunto` | texto | sí |  |
| `texto` | texto | sí |  |
| `html` | texto | sí |  |
| `enlaces` | lista de texto | sí |  |
| `recibidoEn` | fecha y hora | sí |  |

**Errores de negocio que lanza**

- `404` `correo-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/sandbox/correos/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "para": "texto",
  "asunto": "texto",
  "texto": "Texto de ejemplo.",
  "html": "texto",
  "enlaces": [
    "https://ejemplo.co/recurso"
  ],
  "recibidoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /api/v1/sandbox/reiniciar`

**Reiniciar sandbox.** Reinicia el sandbox: borra todo lo hecho en él y lo siembra de nuevo con la configuración de producción de ahora. Las empresas conservan su id y las llaves de prueba siguen valiendo. 404 `sandbox-no-existe`.

**Permiso sandbox:administrar · API (`/api/v1`) · SDK: `reiniciarSandbox`**

**Respuestas**

- `200` OK — SandboxDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `existe` | booleano | sí |  |
| `entorno` | texto | sí |  |
| `tenantId` | uuid o null | no |  |
| `creadoEn` | fecha y hora o null | no |  |
| `reiniciadoEn` | fecha y hora o null | no |  |
| `empresas` | lista de EmpresaDeSandboxDto | sí |  |
| `empresas[].id` | uuid | sí |  |
| `empresas[].produccionId` | uuid | sí |  |
| `empresas[].nombre` | texto | sí |  |
| `empresas[].prefijoFolio` | texto | sí |  |
| `empresas[].activa` | booleano | sí |  |
| `sistemas` | lista de texto | sí |  |
| `correos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/sandbox/reiniciar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "existe": true,
  "entorno": "texto",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "creadoEn": "2026-11-12T15:00:00Z",
  "reiniciadoEn": "2026-11-12T15:00:00Z",
  "empresas": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "produccionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "prefijoFolio": "texto",
      "activa": true
    }
  ],
  "sistemas": [
    "texto"
  ],
  "correos": 1
}
```

</details>
