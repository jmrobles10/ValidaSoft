# API · Común

2 operaciones en 1 recurso.

## Auditoría

### `GET /api/v1/auditoria`

**Listar auditoría.**

**Permiso auditoria:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarAuditoria`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tipo` | query | texto | no |  |
| `entidad` | query | texto | no |  |
| `entidadId` | query | texto | no |  |
| `actor` | query | texto | no |  |
| `desde` | query | fecha y hora | no |  |
| `hasta` | query | fecha y hora | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfRegistroAuditoriaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de RegistroAuditoriaDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].tipo` | texto | sí |  |
| `datos[].fecha` | fecha y hora | sí |  |
| `datos[].empresaId` | uuid o null | no |  |
| `datos[].actor` | ActorAuditoriaDto | sí |  |
| `datos[].actor.tipo` | texto | sí |  |
| `datos[].actor.id` | texto o null | no |  |
| `datos[].actor.nombre` | texto o null | no |  |
| `datos[].actor.sistema` | texto o null | no |  |
| `datos[].entidad` | EntidadAuditadaDto | sí |  |
| `datos[].entidad.tipo` | texto | sí |  |
| `datos[].entidad.id` | texto | sí |  |
| `datos[].entidad.nombre` | texto o null | no |  |
| `datos[].accion` | texto | sí |  |
| `datos[].antes` | JsonElement o null | no |  |
| `datos[].despues` | JsonElement o null | no |  |
| `datos[].diff` | lista de DiffCampoDto | sí |  |
| `datos[].diff[].campo` | texto | sí |  |
| `datos[].diff[].antes` | JsonElement o null | no |  |
| `datos[].diff[].despues` | JsonElement o null | no |  |
| `datos[].diff[].tipoCambio` | texto | sí |  |
| `datos[].justificacion` | texto o null | no |  |
| `datos[].detalle` | texto o null | no |  |
| `datos[].vinculoId` | uuid o null | no |  |
| `datos[].ip` | texto o null | no |  |
| `datos[].userAgent` | texto o null | no |  |
| `datos[].correlationId` | texto o null | no |  |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/auditoria" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": "texto",
      "fecha": "2026-11-12T15:00:00Z",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "actor": {
        "tipo": "texto",
        "id": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "sistema": "texto"
      },
      "entidad": {
        "tipo": "texto",
        "id": "texto",
        "nombre": "Constructora Norte S.A.S."
      },
      "accion": "texto",
      "antes": {},
      "despues": {},
      "diff": [
        {
          "campo": "texto",
          "antes": {},
          "despues": {},
          "tipoCambio": "texto"
        }
      ],
      "justificacion": "Texto de ejemplo.",
      "detalle": "Texto de ejemplo.",
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "ip": "texto",
      "userAgent": "texto",
      "correlationId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/auditoria/excel`

**Exportar auditoría.** La auditoría con los mismos filtros de la pantalla en Excel (IF-4), de lo más reciente a lo más antiguo, con el detalle de cada cambio en una celda. Pide `auditoria:exportar` y la exportación misma queda en la auditoría.

**Permiso auditoria:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `exportarAuditoria`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tipo` | query | texto | no |  |
| `entidad` | query | texto | no |  |
| `entidadId` | query | texto | no |  |
| `actor` | query | texto | no |  |
| `desde` | query | fecha y hora | no |  |
| `hasta` | query | fecha y hora | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

**Errores de negocio que lanza**

- `422` `exportacion-demasiado-grande`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/auditoria/excel" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>
