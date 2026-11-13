# API · IA

8 operaciones en 3 recursos.

## Consumo de IA

Medición del uso de IA por cliente para cobrar el adicional (PI-12): por grupo y empresa y por mes, las validaciones incluidas y adicionales consumidas, los análisis que pagó la llave de la empresa con sus tokens, modelo y costo, y el valor de lo adicional con el precio configurable (el de la empresa o, si no tiene, la tarifa general). Solo Plataforma; se ve en la consola y se exporta a Excel o CSV.

### `GET /api/v1/plataforma/ia/consumo`

**Obtener consumo IA.** Una página de grupos con el consumo de sus empresas en `periodo` (`yyyy-MM`; por omisión el mes en curso en la zona del negocio), los totales de todos los grupos que cumplen el filtro y la tarifa con la que se valoró. Filtros: `grupoId` y `q` (parte del nombre o del identificador del grupo, o de la razón social de una de sus empresas). Orden estable por nombre del grupo. 422 `periodo-invalido`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerConsumoIa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `periodo` | query | texto | no |  |
| `grupoId` | query | uuid | no |  |
| `q` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — ConsumoDeIaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `periodo` | texto | sí |  |
| `moneda` | texto | sí |  |
| `precioPorValidacionAdicional` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `totales` | ConsumoDeIaTotalesDto | sí |  |
| `totales.grupos` | entero | sí |  |
| `totales.empresas` | entero | sí |  |
| `totales.incluidasConsumidas` | entero | sí |  |
| `totales.adicionalesConsumidas` | entero | sí |  |
| `totales.analisisPagados` | entero | sí |  |
| `totales.tokensEntrada` | entero | sí |  |
| `totales.tokensSalida` | entero | sí |  |
| `totales.costoUsd` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `totales.valorAdicional` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `grupos` | lista de ConsumoDeIaGrupoDto | sí |  |
| `grupos[].grupoId` | uuid | sí |  |
| `grupos[].grupo` | texto | sí |  |
| `grupos[].slug` | texto | sí |  |
| `grupos[].incluidasConsumidas` | entero | sí |  |
| `grupos[].adicionalesConsumidas` | entero | sí |  |
| `grupos[].analisisPagados` | entero | sí |  |
| `grupos[].tokensEntrada` | entero | sí |  |
| `grupos[].tokensSalida` | entero | sí |  |
| `grupos[].costoUsd` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `grupos[].valorAdicional` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `grupos[].empresas` | lista de ConsumoDeIaEmpresaDto | sí |  |
| `grupos[].empresas[].empresaId` | uuid | sí |  |
| `grupos[].empresas[].empresa` | texto | sí |  |
| `grupos[].empresas[].validacionesIncluidasMes` | entero | sí |  |
| `grupos[].empresas[].incluidasConsumidas` | entero | sí |  |
| `grupos[].empresas[].adicionalesConsumidas` | entero | sí |  |
| `grupos[].empresas[].adicionalHabilitado` | booleano | sí |  |
| `grupos[].empresas[].analisisPagados` | entero | sí |  |
| `grupos[].empresas[].tokensEntrada` | entero | sí |  |
| `grupos[].empresas[].tokensEntradaEnCache` | entero | sí |  |
| `grupos[].empresas[].tokensSalida` | entero | sí |  |
| `grupos[].empresas[].costoUsd` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `grupos[].empresas[].precioAdicional` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `grupos[].empresas[].precioPropio` | booleano | sí |  |
| `grupos[].empresas[].valorAdicional` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `grupos[].empresas[].modelos` | lista de UsoPorModeloIaDto | sí |  |
| `grupos[].empresas[].modelos[].modelo` | texto | sí |  |
| `grupos[].empresas[].modelos[].llamadas` | entero | sí |  |
| `grupos[].empresas[].modelos[].tokensEntrada` | entero | sí |  |
| `grupos[].empresas[].modelos[].tokensEntradaEnCache` | entero | sí |  |
| `grupos[].empresas[].modelos[].tokensSalida` | entero | sí |  |
| `grupos[].empresas[].modelos[].costoUsd` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/ia/consumo" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "periodo": "texto",
  "moneda": "texto",
  "precioPorValidacionAdicional": 1,
  "totales": {
    "grupos": 1,
    "empresas": 1,
    "incluidasConsumidas": 1,
    "adicionalesConsumidas": 1,
    "analisisPagados": 1,
    "tokensEntrada": 1,
    "tokensSalida": 1,
    "costoUsd": 1,
    "valorAdicional": 1
  },
  "grupos": [
    {
      "grupoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "grupo": "texto",
      "slug": "grupo-ejemplo",
      "incluidasConsumidas": 1,
      "adicionalesConsumidas": 1,
      "analisisPagados": 1,
      "tokensEntrada": 1,
      "tokensSalida": 1,
      "costoUsd": 1,
      "valorAdicional": 1,
      "empresas": [
        {
          "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "empresa": "texto",
          "validacionesIncluidasMes": 1,
          "incluidasConsumidas": 1,
          "adicionalesConsumidas": 1,
          "adicionalHabilitado": true,
          "analisisPagados": 1,
          "tokensEntrada": 1,
          "tokensEntradaEnCache": 1,
          "tokensSalida": 1,
          "costoUsd": 1,
          "precioAdicional": 1,
          "precioPropio": true,
          "valorAdicional": 1,
          "modelos": [
            {
              "modelo": null,
              "llamadas": null,
              "tokensEntrada": null,
              "tokensEntradaEnCache": null,
              "tokensSalida": null,
              "costoUsd": null
            }
          ]
        }
      ]
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/plataforma/ia/consumo/exportar`

**Exportar consumo IA.** El mismo consumo (todos los grupos que cumplen el filtro, sin paginar) en un archivo: `formato=xlsx` (por omisión; una hoja por empresa y otra por modelo) o `formato=csv` (una fila por empresa). 422 `periodo-invalido`, `formato-invalido`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `exportarConsumoIa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `periodo` | query | texto | no |  |
| `grupoId` | query | uuid | no |  |
| `q` | query | texto | no |  |
| `formato` | query | texto | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

**Errores de negocio que lanza**

- `422` `formato-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/ia/consumo/exportar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

</details>

### `GET /api/v1/plataforma/ia/tarifa`

**Obtener tarifa IA.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerTarifaIa`**

**Respuestas**

- `200` OK — TarifaDeIaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `precioPorValidacionAdicional` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `moneda` | texto | sí |  |
| `actualizadaEn` | fecha y hora o null | no |  |
| `actualizadaPor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/ia/tarifa" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "precioPorValidacionAdicional": 1,
  "moneda": "texto",
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "actualizadaPor": "texto"
}
```

</details>

### `PUT /api/v1/plataforma/ia/tarifa`

**Fijar tarifa IA.** Fija la tarifa general: lo que se cobra por cada validación adicional y la moneda. Rige para todas las empresas que no tengan un precio propio en su plan. Queda en la auditoría. 422 `precio-adicional-invalido`, `moneda-invalida`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `fijarTarifaIa`**

**Cuerpo de la petición** (`application/json`, FijarTarifaDeIaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `precioPorValidacionAdicional` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `moneda` | texto | sí |  |

**Respuestas**

- `200` OK — TarifaDeIaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `precioPorValidacionAdicional` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `moneda` | texto | sí |  |
| `actualizadaEn` | fecha y hora o null | no |  |
| `actualizadaPor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/plataforma/ia/tarifa" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Content-Type: application/json" \
  -d '{"precioPorValidacionAdicional":1,"moneda":"texto"}'
```

Respuesta `200`:

```json
{
  "precioPorValidacionAdicional": 1,
  "moneda": "texto",
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "actualizadaPor": "texto"
}
```

</details>

## Plan IA

### `GET /api/v1/plataforma/tenants/{tenantId}/empresas/{empresaId}/plan-ia`

**Obtener plan IA empresa.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerPlanIaEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tenantId` | path | uuid | sí |  |
| `empresaId` | path | uuid | sí |  |

**Respuestas**

- `200` OK — PlanIaEmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `empresaId` | uuid | sí |  |
| `configurado` | booleano | sí |  |
| `validacionesIncluidasMes` | entero | sí |  |
| `adicionalHabilitado` | booleano | sí |  |
| `topeAdicionalMes` | entero o null | no |  |
| `llaveConfigurada` | booleano | sí |  |
| `ultimosCuatro` | texto o null | no |  |
| `modeloPorDefecto` | texto | sí |  |
| `modeloEscalamiento` | texto | sí |  |
| `umbralConfianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `activo` | booleano | sí |  |
| `actualizadoEn` | fecha y hora o null | no |  |
| `precioAdicional` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/plan-ia" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "configurado": true,
  "validacionesIncluidasMes": 1,
  "adicionalHabilitado": true,
  "topeAdicionalMes": 1,
  "llaveConfigurada": true,
  "ultimosCuatro": "texto",
  "modeloPorDefecto": "texto",
  "modeloEscalamiento": "texto",
  "umbralConfianza": 1,
  "activo": true,
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "precioAdicional": 1
}
```

</details>

### `PUT /api/v1/plataforma/tenants/{tenantId}/empresas/{empresaId}/plan-ia`

**Actualizar plan IA empresa.** Crea o reemplaza el plan. `apiKeyOpenAi` nulo conserva la llave, `""` la quita. 404 `tenant-no-encontrado` / `empresa-no-encontrada`; 422 `validaciones-incluidas-invalidas`, `tope-adicional-invalido`, `modelo-invalido`, `umbral-confianza-invalido`, `api-key-invalida`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `actualizarPlanIaEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tenantId` | path | uuid | sí |  |
| `empresaId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarPlanIaEmpresaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `validacionesIncluidasMes` | entero | sí |  |
| `adicionalHabilitado` | booleano | sí |  |
| `topeAdicionalMes` | entero o null | no |  |
| `apiKeyOpenAi` | texto o null | no |  |
| `modeloPorDefecto` | texto o null | no |  |
| `modeloEscalamiento` | texto o null | no |  |
| `umbralConfianza` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `activo` | booleano | no | por omisión true |
| `precioAdicional` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |

**Respuestas**

- `200` OK — PlanIaEmpresaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `empresaId` | uuid | sí |  |
| `configurado` | booleano | sí |  |
| `validacionesIncluidasMes` | entero | sí |  |
| `adicionalHabilitado` | booleano | sí |  |
| `topeAdicionalMes` | entero o null | no |  |
| `llaveConfigurada` | booleano | sí |  |
| `ultimosCuatro` | texto o null | no |  |
| `modeloPorDefecto` | texto | sí |  |
| `modeloEscalamiento` | texto | sí |  |
| `umbralConfianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `activo` | booleano | sí |  |
| `actualizadoEn` | fecha y hora o null | no |  |
| `precioAdicional` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/plan-ia" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Content-Type: application/json" \
  -d '{"validacionesIncluidasMes":1,"adicionalHabilitado":true,"topeAdicionalMes":1,"apiKeyOpenAi":"texto","modeloPorDefecto":"texto","modeloEscalamiento":"texto","umbralConfianza":1,"activo":true,"precioAdicional":1}'
```

Respuesta `200`:

```json
{
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "configurado": true,
  "validacionesIncluidasMes": 1,
  "adicionalHabilitado": true,
  "topeAdicionalMes": 1,
  "llaveConfigurada": true,
  "ultimosCuatro": "texto",
  "modeloPorDefecto": "texto",
  "modeloEscalamiento": "texto",
  "umbralConfianza": 1,
  "activo": true,
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "precioAdicional": 1
}
```

</details>

### `POST /api/v1/plataforma/tenants/{tenantId}/empresas/{empresaId}/plan-ia/probar-llave`

**Probar llave IA empresa.** «Probar llave» (PT-6): verifica contra el proveedor la llave del cuerpo (aún sin guardar) o, si viene nula, la guardada en el plan. Solo cuando Plataforma lo pide; no cambia el plan ni consume validaciones. Responde siempre 200 con el resultado (`valida`, `invalida`, `limite`, `sin-llave`, `error`) y solo los últimos cuatro de la llave. 404 `tenant-no-encontrado` / `empresa-no-encontrada`; 422 `api-key-invalida` (forma).

**Operador de Plataforma · API (`/api/v1`) · SDK: `probarLlaveIaEmpresa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tenantId` | path | uuid | sí |  |
| `empresaId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ProbarLlaveIaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `apiKeyOpenAi` | texto o null | no |  |

**Respuestas**

- `200` OK — PruebaDeLlaveIaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `resultado` | texto | sí |  |
| `mensaje` | texto | sí |  |
| `ultimosCuatro` | texto o null | no |  |
| `probadaEn` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/empresas/3fa85f64-5717-4562-b3fc-2c963f66afa6/plan-ia/probar-llave" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"apiKeyOpenAi":"texto"}'
```

Respuesta `200`:

```json
{
  "resultado": "texto",
  "mensaje": "Texto de ejemplo.",
  "ultimosCuatro": "texto",
  "probadaEn": "2026-11-12T15:00:00Z"
}
```

</details>

## Uso IA

Plan y uso de IA de la empresa activa (pantalla «Plan y uso», ADR-0012).

### `GET /api/v1/ia/uso`

**Obtener uso IA.** Plan resumido (sin la llave) y uso del mes `mes` (`yyyy-MM`; por defecto el actual en la zona horaria del tenant): validaciones incluidas y adicionales consumidas y restantes, análisis pagados con su llave, tokens, costo real en dólares y serie por día. 422 `mes-invalido`.

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerUsoIa`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `mes` | query | texto | no |  |

**Respuestas**

- `200` OK — UsoIaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `mes` | texto | sí |  |
| `plan` | PlanIaResumenDto | sí |  |
| `plan.configurado` | booleano | sí |  |
| `plan.validacionesIncluidasMes` | entero | sí |  |
| `plan.adicionalHabilitado` | booleano | sí |  |
| `plan.topeAdicionalMes` | entero o null | no |  |
| `plan.llaveConfigurada` | booleano | sí |  |
| `plan.modeloPorDefecto` | texto | sí |  |
| `plan.modeloEscalamiento` | texto | sí |  |
| `plan.umbralConfianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `plan.activo` | booleano | sí |  |
| `incluidasConsumidas` | entero | sí |  |
| `adicionalesConsumidas` | entero | sí |  |
| `incluidasRestantes` | entero | sí |  |
| `adicionalesRestantes` | entero o null | no |  |
| `analisisPagados` | entero | sí |  |
| `tokensEntrada` | entero | sí |  |
| `tokensEntradaEnCache` | entero | sí |  |
| `tokensSalida` | entero | sí |  |
| `costoUsd` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `porDia` | lista de UsoIaDiaDto | sí |  |
| `porDia[].fecha` | fecha | sí |  |
| `porDia[].incluidas` | entero | sí |  |
| `porDia[].adicionales` | entero | sí |  |
| `porDia[].analisisPagados` | entero | sí |  |
| `porDia[].tokensEntrada` | entero | sí |  |
| `porDia[].tokensSalida` | entero | sí |  |
| `porDia[].costoUsd` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |

**Errores de negocio que lanza**

- `422` `mes-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/ia/uso" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "mes": "texto",
  "plan": {
    "configurado": true,
    "validacionesIncluidasMes": 1,
    "adicionalHabilitado": true,
    "topeAdicionalMes": 1,
    "llaveConfigurada": true,
    "modeloPorDefecto": "texto",
    "modeloEscalamiento": "texto",
    "umbralConfianza": 1,
    "activo": true
  },
  "incluidasConsumidas": 1,
  "adicionalesConsumidas": 1,
  "incluidasRestantes": 1,
  "adicionalesRestantes": 1,
  "analisisPagados": 1,
  "tokensEntrada": 1,
  "tokensEntradaEnCache": 1,
  "tokensSalida": 1,
  "costoUsd": 1,
  "porDia": [
    {
      "fecha": "2026-11-12",
      "incluidas": 1,
      "adicionales": 1,
      "analisisPagados": 1,
      "tokensEntrada": 1,
      "tokensSalida": 1,
      "costoUsd": 1
    }
  ]
}
```

</details>
