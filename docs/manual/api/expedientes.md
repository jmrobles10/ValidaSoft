# API · Expedientes

37 operaciones en 14 recursos.

## Bandeja

### `GET /api/v1/bandeja`

**Listar bandeja.**

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarBandeja`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `area` | query | uuid | no |  |
| `estado` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfFilaBandejaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de FilaBandejaDto | sí |  |
| `datos[].expedienteId` | uuid | sí |  |
| `datos[].folio` | texto | sí |  |
| `datos[].vinculoId` | uuid | sí |  |
| `datos[].tercero` | TerceroResumenDto | sí |  |
| `datos[].tercero.id` | uuid | sí |  |
| `datos[].tercero.nombre` | texto | sí |  |
| `datos[].tercero.identificacion` | texto | sí |  |
| `datos[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `datos[].tipoVinculo.id` | uuid | sí |  |
| `datos[].tipoVinculo.codigo` | texto | sí |  |
| `datos[].tipoVinculo.nombre` | texto | sí |  |
| `datos[].areaId` | uuid | sí |  |
| `datos[].areaCodigo` | texto | sí |  |
| `datos[].areaNombre` | texto | sí |  |
| `datos[].etapa` | entero | sí |  |
| `datos[].estadoArea` | EstadoAreaExpediente (texto) | sí | valores: Esperando · Pendiente · Aprobada · Observada |
| `datos[].estadoExpediente` | EstadoExpediente (texto) | sí | valores: Abierto · EnRevision · Observado · Aprobado · Rechazado · Anulado · Caducado |
| `datos[].aprobaciones` | entero | sí |  |
| `datos[].aprobadoresRequeridos` | entero | sí |  |
| `datos[].desde` | fecha y hora o null | no |  |
| `datos[].actualizadoEn` | fecha y hora | sí |  |
| `datos[].requisitosPendientes` | entero | no | por omisión 0 |
| `datos[].desaprobado` | booleano | no | por omisión false |
| `datos[].slaHoras` | entero o null | no |  |
| `datos[].slaVence` | fecha y hora o null | no |  |
| `datos[].sla` | EstadoDeSla (texto) | no | valores: EnTiempo · PorVencer · Vencido |
| `siguienteCursor` | texto o null | no |  |

**Errores de negocio que lanza**

- `422` `estado-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/bandeja" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
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
      "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "areaCodigo": "codigo-de-ejemplo",
      "areaNombre": "Nombre de ejemplo",
      "etapa": 1,
      "estadoArea": "Esperando",
      "estadoExpediente": "Abierto",
      "aprobaciones": 1,
      "aprobadoresRequeridos": 1,
      "desde": "2026-11-12T15:00:00Z",
      "actualizadoEn": "2026-11-12T15:00:00Z",
      "requisitosPendientes": 0,
      "desaprobado": false,
      "slaHoras": 1,
      "slaVence": "2026-11-12T15:00:00Z",
      "sla": "EnTiempo"
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/bandeja/aperturas-pendientes`

**Listar aperturas pendientes.** Vínculos cuyo tercero aceptó la invitación pero no tienen expediente porque faltaba un flujo publicado (FL-6): el administrador lo abre con `POST /vinculos/{id}/expedientes`.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarAperturasPendientes`**

**Respuestas**

- `200` OK — lista de VinculoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de VinculoDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].empresaId` | uuid | sí |  |
| `[].terceroId` | uuid | sí |  |
| `[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `[].tipoVinculo.id` | uuid | sí |  |
| `[].tipoVinculo.codigo` | texto | sí |  |
| `[].tipoVinculo.nombre` | texto | sí |  |
| `[].flujoId` | uuid o null | no |  |
| `[].estado` | EstadoVinculo (texto) | sí | valores: Invitado · EnValidacion · Activo · PorVencer · Vencido · Bloqueado · Desvinculado · Anulado |
| `[].atributos` | objeto libre | sí |  |
| `[].correoTercero` | texto | sí |  |
| `[].invitacion` | EnlaceInvitacionDto o null | no |  |
| `[].invitacion.correoDestino` | texto | sí |  |
| `[].invitacion.expira` | fecha y hora | sí |  |
| `[].invitacion.envios` | entero | sí |  |
| `[].invitacionAceptadaEn` | fecha y hora o null | no |  |
| `[].requisitosCubribles` | entero o null | no |  |
| `[].bloqueo` | BloqueoDto o null | no |  |
| `[].bloqueo.motivo` | texto | sí |  |
| `[].bloqueo.desde` | fecha y hora | sí |  |
| `[].bloqueo.hasta` | fecha y hora o null | no |  |
| `[].referencias` | lista de ReferenciaExternaDto | sí |  |
| `[].referencias[].sistema` | texto | sí |  |
| `[].referencias[].id` | texto | sí |  |
| `[].origen` | OrigenVinculo (texto) | sí | valores: Invitacion · Autorregistro · Migracion · Api |
| `[].creadoEn` | fecha y hora | sí |  |
| `[].actualizadoEn` | fecha y hora | sí |  |
| `[].version` | entero | sí |  |
| `[].expedienteAbiertoId` | uuid o null | no |  |
| `[].aperturaDeExpedientePendiente` | booleano | no | por omisión false |
| `[].ultimoResultado` | texto o null | no |  |
| `[].contactos` | lista de ContactoDeEmpresaDto o null | no |  |
| `[].contactos[].nombre` | texto | sí |  |
| `[].contactos[].cargo` | texto o null | no |  |
| `[].contactos[].correo` | texto | sí |  |
| `[].contactos[].telefono` | texto o null | no |  |
| `[].contactos[].rol` | texto | sí |  |
| `[].vigenteHasta` | fecha o null | no |  |
| `[].enRevision` | booleano | no | por omisión false |
| `[].actualizacionNoAprobada` | ActualizacionNoAprobadaDto o null | no |  |
| `[].actualizacionNoAprobada.expedienteId` | uuid o null | no |  |
| `[].actualizacionNoAprobada.resultado` | texto | sí |  |
| `[].actualizacionNoAprobada.fecha` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/bandeja/aperturas-pendientes" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
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
]
```

</details>

## Configuración de recordatorios

### `GET /api/v1/configuracion/recordatorio-observaciones`

**Obtener recordatorio automatico.**

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerRecordatorioAutomatico`**

**Respuestas**

- `200` OK — RecordatorioAutomaticoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `activo` | booleano | sí |  |
| `cadaDias` | entero | sí |  |
| `actualizadoEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/configuracion/recordatorio-observaciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "activo": true,
  "cadaDias": 1,
  "actualizadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `PUT /api/v1/configuracion/recordatorio-observaciones`

**Actualizar recordatorio automatico.** 422 `dias-invalidos` fuera de 1 a 60.

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarRecordatorioAutomatico`**

**Cuerpo de la petición** (`application/json`, ActualizarRecordatorioAutomaticoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `activo` | booleano | sí |  |
| `cadaDias` | entero | sí |  |

**Respuestas**

- `200` OK — RecordatorioAutomaticoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `activo` | booleano | sí |  |
| `cadaDias` | entero | sí |  |
| `actualizadoEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/configuracion/recordatorio-observaciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"activo":true,"cadaDias":1}'
```

Respuesta `200`:

```json
{
  "activo": true,
  "cadaDias": 1,
  "actualizadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

## Configuración de SLA

### `GET /api/v1/configuracion/sla`

**Obtener configuración SLA.**

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerConfiguracionSla`**

**Respuestas**

- `200` OK — ConfiguracionSlaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `porcentajeAviso` | entero | sí |  |
| `avisarAdministradores` | booleano | sí |  |
| `areas` | lista de SlaDeAreaDto | sí |  |
| `areas[].areaId` | uuid | sí |  |
| `areas[].horas` | entero o null | no |  |
| `actualizadaEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/configuracion/sla" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "porcentajeAviso": 1,
  "avisarAdministradores": true,
  "areas": [
    {
      "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "horas": 1
    }
  ],
  "actualizadaEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `PUT /api/v1/configuracion/sla`

**Actualizar configuración SLA.** 422 `porcentaje-invalido`, `sla-invalido`; 404 `area-no-encontrada` si un área no es de la empresa.

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarConfiguracionSla`**

**Cuerpo de la petición** (`application/json`, ActualizarConfiguracionSlaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `porcentajeAviso` | entero | sí |  |
| `avisarAdministradores` | booleano | sí |  |
| `areas` | lista de SlaDeAreaDto o null | no |  |
| `areas[].areaId` | uuid | sí |  |
| `areas[].horas` | entero o null | no |  |

**Respuestas**

- `200` OK — ConfiguracionSlaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `porcentajeAviso` | entero | sí |  |
| `avisarAdministradores` | booleano | sí |  |
| `areas` | lista de SlaDeAreaDto | sí |  |
| `areas[].areaId` | uuid | sí |  |
| `areas[].horas` | entero o null | no |  |
| `actualizadaEn` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `404` `area-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/configuracion/sla" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"porcentajeAviso":1,"avisarAdministradores":true,"areas":[{"areaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","horas":1}]}'
```

Respuesta `200`:

```json
{
  "porcentajeAviso": 1,
  "avisarAdministradores": true,
  "areas": [
    {
      "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "horas": 1
    }
  ],
  "actualizadaEn": "2026-11-12T15:00:00Z"
}
```

</details>

## Expedientes

### `GET /api/v1/expedientes`

**Listar expedientes.** Lista con filtros por estado, área, vínculo y `desaprobados` (los devueltos a revisión por un administrador); cursor opaco.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarExpedientes`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `estado` | query | texto | no |  |
| `area` | query | uuid | no |  |
| `vinculoId` | query | uuid | no |  |
| `desaprobados` | query | booleano | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de ExpedienteDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].folio` | texto | sí |  |
| `datos[].empresaId` | uuid | sí |  |
| `datos[].vinculoId` | uuid | sí |  |
| `datos[].tercero` | TerceroResumenDto | sí |  |
| `datos[].tercero.id` | uuid | sí |  |
| `datos[].tercero.nombre` | texto | sí |  |
| `datos[].tercero.identificacion` | texto | sí |  |
| `datos[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `datos[].tipoVinculo.id` | uuid | sí |  |
| `datos[].tipoVinculo.codigo` | texto | sí |  |
| `datos[].tipoVinculo.nombre` | texto | sí |  |
| `datos[].tipo` | TipoExpediente (texto) | sí | valores: Inicial · Renovacion · Actualizacion |
| `datos[].estado` | EstadoExpediente (texto) | sí | valores: Abierto · EnRevision · Observado · Aprobado · Rechazado · Anulado · Caducado |
| `datos[].flujo` | FlujoDeExpedienteDto o null | no |  |
| `datos[].flujo.id` | uuid | sí |  |
| `datos[].flujo.versionId` | uuid | sí |  |
| `datos[].flujo.codigo` | texto | sí |  |
| `datos[].flujo.nombre` | texto | sí |  |
| `datos[].flujo.version` | entero | sí |  |
| `datos[].flujo.origen` | OrigenFlujo (texto) | sí | valores: Explicito · Regla · PorDefecto |
| `datos[].politicaDevolucion` | PoliticaDevolucion (texto) | sí | valores: Inmediata · AlCerrarArea |
| `datos[].separacionDeFunciones` | booleano | sí |  |
| `datos[].etapaActual` | entero o null | no |  |
| `datos[].etapas` | lista de EtapaExpedienteDto | sí |  |
| `datos[].etapas[].orden` | entero | sí |  |
| `datos[].etapas[].estado` | EstadoEtapa (texto) | sí | valores: Esperando · Activa · Aprobada |
| `datos[].etapas[].activadaEn` | fecha y hora o null | no |  |
| `datos[].etapas[].aprobadaEn` | fecha y hora o null | no |  |
| `datos[].etapas[].areas` | lista de AreaExpedienteDto | sí |  |
| `datos[].etapas[].areas[].areaId` | uuid | sí |  |
| `datos[].etapas[].areas[].codigo` | texto | sí |  |
| `datos[].etapas[].areas[].nombre` | texto | sí |  |
| `datos[].etapas[].areas[].estado` | EstadoAreaExpediente (texto) | sí | valores: Esperando · Pendiente · Aprobada · Observada |
| `datos[].etapas[].areas[].aprobadoresRequeridos` | entero | sí |  |
| `datos[].etapas[].areas[].aprobaciones` | entero | sí |  |
| `datos[].etapas[].areas[].aprobacionesFaltantes` | entero | sí |  |
| `datos[].etapas[].areas[].miembrosPendientes` | lista de uuid | sí |  |
| `datos[].etapas[].areas[].puedeRechazarExpediente` | booleano | sí |  |
| `datos[].etapas[].areas[].automatica` | booleano | sí |  |
| `datos[].etapas[].areas[].pendienteDesde` | fecha y hora o null | no |  |
| `datos[].etapas[].areas[].slaVence` | fecha y hora o null | no |  |
| `datos[].etapas[].areas[].veredictos` | lista de VeredictoEmitidoDto | sí |  |
| `datos[].etapas[].areas[].veredictos[].id` | uuid | sí |  |
| `datos[].etapas[].areas[].veredictos[].veredicto` | Veredicto (texto) | sí | valores: Aprobado · Observado |
| `datos[].etapas[].areas[].veredictos[].origen` | OrigenVeredicto (texto) | sí | valores: Humano · Sistema · Administrador |
| `datos[].etapas[].areas[].veredictos[].usuario` | texto o null | no |  |
| `datos[].etapas[].areas[].veredictos[].fecha` | fecha y hora | sí |  |
| `datos[].etapas[].areas[].veredictos[].motivos` | lista de texto | sí |  |
| `datos[].etapas[].areas[].veredictos[].observacion` | texto o null | no |  |
| `datos[].etapas[].areas[].veredictos[].justificacion` | texto o null | no |  |
| `datos[].etapas[].areas[].veredictos[].vigente` | booleano | sí |  |
| `datos[].etapas[].areas[].veredictos[].superado` | booleano | sí |  |
| `datos[].etapas[].areas[].veredictos[].anulado` | booleano | sí |  |
| `datos[].etapas[].areas[].veredictos[].anuladoPor` | texto o null | no |  |
| `datos[].etapas[].areas[].veredictos[].sobreVersion` | entero o null | no |  |
| `datos[].etapas[].areas[].veredictos[].invalidado` | booleano | no | por omisión false |
| `datos[].etapas[].areas[].veredictos[].derivado` | booleano | no | por omisión false |
| `datos[].etapas[].areas[].veredictos[].campos` | lista de texto o null | no |  |
| `datos[].etapas[].areas[].veredictos[].revertido` | booleano | no | por omisión false |
| `datos[].etapas[].areas[].requisitosPorDecidir` | entero | no | por omisión 0 |
| `datos[].etapas[].areas[].documentosPorDecidir` | lista de DocumentoPorDecidirDto o null | no |  |
| `datos[].etapas[].areas[].documentosPorDecidir[].requisitoId` | uuid | sí |  |
| `datos[].etapas[].areas[].documentosPorDecidir[].versionVigente` | entero | sí |  |
| `datos[].etapas[].areas[].documentosPorDecidir[].tipoRequisito` | TipoRequisito (texto) | no | valores: Documento · Seccion |
| `datos[].etapas[].areas[].documentosPorDecidir[].seccion` | texto o null | no |  |
| `datos[].etapas[].areas[].documentosPorDecidir[].periodo` | entero o null | no |  |
| `datos[].reenvios` | entero | sí |  |
| `datos[].ultimaRespuesta` | texto o null | no |  |
| `datos[].rechazo` | RechazoDto o null | no |  |
| `datos[].rechazo.areaId` | uuid | sí |  |
| `datos[].rechazo.usuario` | texto | sí |  |
| `datos[].rechazo.motivos` | lista de texto | sí |  |
| `datos[].rechazo.justificacion` | texto | sí |  |
| `datos[].rechazo.fecha` | fecha y hora | sí |  |
| `datos[].rechazo.origen` | texto o null | no |  |
| `datos[].motivoAnulacion` | texto o null | no |  |
| `datos[].abiertoEn` | fecha y hora | sí |  |
| `datos[].cerradoEn` | fecha y hora o null | no |  |
| `datos[].actualizadoEn` | fecha y hora | sí |  |
| `datos[].version` | entero | sí |  |
| `datos[].requisitos` | lista de RequisitoExpedienteDto o null | no |  |
| `datos[].requisitos[].id` | uuid | sí |  |
| `datos[].requisitos[].tipoDocumento` | TipoDocumentoRequisitoDto | sí |  |
| `datos[].requisitos[].tipoDocumento.id` | uuid | sí |  |
| `datos[].requisitos[].tipoDocumento.codigo` | texto | sí |  |
| `datos[].requisitos[].tipoDocumento.nombre` | texto | sí |  |
| `datos[].requisitos[].tipoDocumento.tipoCanonico` | texto o null | no |  |
| `datos[].requisitos[].tipoDocumento.ambito` | AmbitoDocumento (texto) | sí | valores: Global · PorVinculo |
| `datos[].requisitos[].tipoDocumento.modo` | texto o null | no |  |
| `datos[].requisitos[].tipoDocumento.campos` | lista de CampoFormularioDto o null | no |  |
| `datos[].requisitos[].tipoDocumento.campos[].clave` | texto | sí |  |
| `datos[].requisitos[].tipoDocumento.campos[].etiqueta` | texto | sí |  |
| `datos[].requisitos[].tipoDocumento.campos[].tipo` | texto | sí |  |
| `datos[].requisitos[].tipoDocumento.campos[].obligatorio` | booleano | no | por omisión false |
| `datos[].requisitos[].tipoDocumento.campos[].opciones` | lista de texto o null | no |  |
| `datos[].requisitos[].tipoDocumento.campos[].ayuda` | texto o null | no |  |
| `datos[].requisitos[].tipoDocumento.archivo` | ReglasArchivoDto o null | no |  |
| `datos[].requisitos[].tipoDocumento.archivo.formatos` | lista de texto | sí |  |
| `datos[].requisitos[].tipoDocumento.archivo.tamanoMaximoBytes` | entero | sí |  |
| `datos[].requisitos[].tipoDocumento.archivo.paginasMaximas` | entero | sí |  |
| `datos[].requisitos[].tipoDocumento.archivo.descripcion` | texto | sí |  |
| `datos[].requisitos[].tipoDocumento.tienePlantilla` | booleano | no | por omisión false |
| `datos[].requisitos[].tipoDocumento.textoAyuda` | texto o null | no |  |
| `datos[].requisitos[].obligatorio` | booleano | sí |  |
| `datos[].requisitos[].estado` | EstadoRequisito (texto) | sí | valores: Pendiente · Cargado · EnRevision · Observado · Aprobado · Eximido |
| `datos[].requisitos[].versionActual` | VersionRequisitoDto o null | no |  |
| `datos[].requisitos[].versionActual.documentoId` | uuid | sí |  |
| `datos[].requisitos[].versionActual.n` | entero | sí |  |
| `datos[].requisitos[].versionActual.sha256` | texto | sí |  |
| `datos[].requisitos[].versionActual.cargadaEn` | fecha y hora | sí |  |
| `datos[].requisitos[].versionActual.datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `datos[].requisitos[].versionActual.datosDeclarados[].clave` | texto | sí |  |
| `datos[].requisitos[].versionActual.datosDeclarados[].etiqueta` | texto | sí |  |
| `datos[].requisitos[].versionActual.datosDeclarados[].tipo` | texto | sí |  |
| `datos[].requisitos[].versionActual.datosDeclarados[].valor` | texto | sí |  |
| `datos[].requisitos[].versionActual.vigencia` | VigenciaDto o null | no |  |
| `datos[].requisitos[].versionActual.vigencia.vigenteHasta` | fecha o null | no |  |
| `datos[].requisitos[].versionActual.vigencia.base` | texto | sí |  |
| `datos[].requisitos[].versionActual.vigencia.estado` | texto | sí |  |
| `datos[].requisitos[].versionActual.vigencia.diasAviso` | lista de entero | sí |  |
| `datos[].requisitos[].versionActual.vigencia.diasGracia` | entero | sí |  |
| `datos[].requisitos[].versionActual.vigencia.finDeGracia` | fecha o null | no |  |
| `datos[].requisitos[].versionActual.vigencia.vencidaEn` | fecha y hora o null | no |  |
| `datos[].requisitos[].versionActual.estado` | texto o null | no |  |
| `datos[].requisitos[].versionActual.clase` | texto o null | no |  |
| `datos[].requisitos[].reenvios` | entero | sí |  |
| `datos[].requisitos[].exencion` | ExencionDto o null | no |  |
| `datos[].requisitos[].exencion.por` | texto | sí |  |
| `datos[].requisitos[].exencion.justificacion` | texto | sí |  |
| `datos[].requisitos[].exencion.fecha` | fecha y hora | sí |  |
| `datos[].requisitos[].areas` | lista de AreaDeRequisitoDto | sí |  |
| `datos[].requisitos[].areas[].areaId` | uuid | sí |  |
| `datos[].requisitos[].areas[].codigo` | texto | sí |  |
| `datos[].requisitos[].areas[].estado` | EstadoAreaRequisito (texto) | sí | valores: Esperando · Pendiente · Aprobado · Observado · Eximido |
| `datos[].requisitos[].areas[].aprobadoresRequeridos` | entero | sí |  |
| `datos[].requisitos[].areas[].aprobaciones` | entero | sí |  |
| `datos[].requisitos[].areas[].veredictos` | lista de VeredictoEmitidoDto | sí | mismos campos que `datos[].etapas[].areas[].veredictos[]` |
| `datos[].requisitos[].tipoRequisito` | TipoRequisito (texto) | no | valores: Documento · Seccion |
| `datos[].requisitos[].seccion` | SeccionDeRequisitoDto o null | no |  |
| `datos[].requisitos[].seccion.seccion` | texto | sí |  |
| `datos[].requisitos[].seccion.nombre` | texto | sí |  |
| `datos[].requisitos[].seccion.periodo` | entero o null | no |  |
| `datos[].requisitos[].seccion.exigeSoporte` | booleano | sí |  |
| `datos[].requisitos[].seccion.completitud` | CompletitudSeccionDto o null | no |  |
| `datos[].requisitos[].seccion.completitud.completa` | booleano | sí |  |
| `datos[].requisitos[].seccion.completitud.faltantes` | lista de FaltanteSeccionDto | sí |  |
| `datos[].requisitos[].seccion.completitud.faltantes[].campo` | texto o null | no |  |
| `datos[].requisitos[].seccion.completitud.faltantes[].regla` | texto o null | no |  |
| `datos[].requisitos[].seccion.completitud.faltantes[].mensaje` | texto | sí |  |
| `datos[].requisitos[].seccion.completitud.reglaVersion` | entero | sí |  |
| `datos[].requisitos[].seccion.versionEvaluada` | entero o null | no |  |
| `datos[].requisitos[].seccion.datos` | JsonElement o null | no |  |
| `datos[].requisitos[].seccion.soportes` | lista de SoporteSeccionDto | sí |  |
| `datos[].requisitos[].seccion.soportes[].documentoId` | uuid | sí |  |
| `datos[].requisitos[].seccion.soportes[].version` | entero | sí |  |
| `datos[].requisitos[].seccion.soportes[].tipoDocumento` | texto o null | no |  |
| `datos[].requisitos[].seccion.soportes[].retirado` | booleano | no | por omisión false |
| `datos[].requisitos[].seccion.soportes[].nombre` | texto o null | no |  |
| `datos[].requisitos[].seccion.versionAceptada` | entero o null | no |  |
| `datos[].requisitos[].seccion.cambios` | lista de CambioDeCampoDto | sí |  |
| `datos[].requisitos[].seccion.cambios[].campo` | texto | sí |  |
| `datos[].requisitos[].seccion.cambios[].etiqueta` | texto | sí |  |
| `datos[].requisitos[].seccion.cambios[].anterior` | texto o null | no |  |
| `datos[].requisitos[].seccion.cambios[].nuevo` | texto o null | no |  |
| `datos[].requisitos[].seccion.cambios[].tipoCambio` | TipoCambio (texto) | sí | valores: Agregado · Quitado · Modificado |
| `datos[].requisitos[].seccion.cruce` | CruceDeRequisitoDto o null | no |  |
| `datos[].requisitos[].seccion.cruce.estado` | texto | sí |  |
| `datos[].requisitos[].seccion.cruce.discrepanciaCritica` | booleano | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces` | lista de CruceDeSeccionDto | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].documentoId` | uuid | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].versionDocumento` | entero | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].tipoDocumento` | texto | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].nombreDocumento` | texto | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `datos[].requisitos[].seccion.cruce.cruces[].legible` | booleano | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].fecha` | fecha y hora | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].coincidencias` | entero | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].discrepancias` | entero | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].noVerificables` | entero | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].discrepanciaCritica` | booleano | sí |  |
| `datos[].requisitos[].seccion.cruce.cruces[].campos` | lista de CampoCruzadoDto | sí | no se expande más: ver el esquema en el contrato |
| `datos[].requisitos[].seccion.cruce.sinCruce` | lista de DocumentoSinCruceDto | sí |  |
| `datos[].requisitos[].seccion.cruce.sinCruce[].documentoId` | uuid | sí |  |
| `datos[].requisitos[].seccion.cruce.sinCruce[].version` | entero | sí |  |
| `datos[].requisitos[].seccion.cruce.sinCruce[].tipoDocumento` | texto | sí |  |
| `datos[].requisitos[].seccion.cruce.sinCruce[].nombreDocumento` | texto | sí |  |
| `datos[].requisitos[].seccion.cruce.sinCruce[].motivo` | texto | sí |  |
| `datos[].requisitos[].seccion.sinSoporte` | SinSoporteDto o null | no |  |
| `datos[].requisitos[].seccion.sinSoporte.critico` | booleano | sí |  |
| `datos[].requisitos[].seccion.sinSoporte.cambios` | lista de CambioDeCampoDto | sí | mismos campos que `datos[].requisitos[].seccion.cambios[]` |
| `datos[].requisitos[].soporteDeSecciones` | SoporteDeSeccionesDto o null | no |  |
| `datos[].requisitos[].soporteDeSecciones.sinAnalisis` | booleano | sí |  |
| `datos[].requisitos[].soporteDeSecciones.discrepanciaCritica` | booleano | sí |  |
| `datos[].requisitos[].soporteDeSecciones.secciones` | lista de SeccionRespaldadaDto | sí |  |
| `datos[].requisitos[].soporteDeSecciones.secciones[].seccion` | texto | sí |  |
| `datos[].requisitos[].soporteDeSecciones.secciones[].nombre` | texto | sí |  |
| `datos[].requisitos[].soporteDeSecciones.secciones[].periodo` | entero o null | no |  |
| `datos[].requisitos[].soporteDeSecciones.secciones[].version` | entero o null | no |  |
| `datos[].requisitos[].soporteDeSecciones.secciones[].cruce` | CruceDeSeccionDto o null | no | mismos campos que `datos[].requisitos[].seccion.cruce.cruces[]` |
| `datos[].requisitos[].retiro` | RetiroDeVersionDto o null | no |  |
| `datos[].requisitos[].retiro.numero` | entero | sí |  |
| `datos[].requisitos[].retiro.motivo` | texto | sí |  |
| `datos[].requisitos[].retiro.fecha` | fecha y hora | sí |  |
| `datos[].requisitos[].revisionesAutomaticas` | lista de RevisionAutomaticaDto o null | no |  |
| `datos[].requisitos[].revisionesAutomaticas[].areaId` | uuid | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].areaCodigo` | texto | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].sobreVersion` | entero | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].nivel` | texto | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].resultado` | texto | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].sugerencia` | texto o null | no |  |
| `datos[].requisitos[].revisionesAutomaticas[].motivos` | lista de MotivoDeRevisionDto | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].motivos[].codigo` | texto | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].motivos[].texto` | texto | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].confianza` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `datos[].requisitos[].revisionesAutomaticas[].fecha` | fecha y hora | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].pendienteDeAtencion` | booleano | sí |  |
| `datos[].requisitos[].revisionesAutomaticas[].resolucion` | texto o null | no |  |
| `datos[].requisitos[].revisionesAutomaticas[].resueltaPor` | texto o null | no |  |
| `datos[].requisitos[].revisionesAutomaticas[].resueltaEn` | fecha y hora o null | no |  |
| `datos[].requisitos[].revisionesAutomaticas[].justificacionResolucion` | texto o null | no |  |
| `datos[].verificaciones` | lista de VerificacionDto o null | no |  |
| `datos[].verificaciones[].tipo` | TipoVerificacion (texto) | sí | valores: ListasRestrictivas · EstudioDeCumplimiento |
| `datos[].verificaciones[].estado` | EstadoVerificacion (texto) | sí | valores: Pendiente · CoincidenciaPorAnalizar · Satisfecha · Fallida · Expirada |
| `datos[].verificaciones[].bloqueante` | booleano | sí |  |
| `datos[].verificaciones[].requeridaPorElFlujo` | booleano | sí |  |
| `datos[].verificaciones[].proveedor` | texto o null | no |  |
| `datos[].verificaciones[].resultado` | texto o null | no |  |
| `datos[].verificaciones[].referenciaExterna` | texto o null | no |  |
| `datos[].verificaciones[].registradaEn` | fecha y hora o null | no |  |
| `datos[].verificaciones[].registradaPor` | texto o null | no |  |
| `datos[].verificaciones[].vigenteHasta` | fecha y hora o null | no |  |
| `datos[].verificaciones[].evidencia` | EvidenciaVerificacionDto o null | no |  |
| `datos[].verificaciones[].evidencia.nombreArchivo` | texto | sí |  |
| `datos[].verificaciones[].evidencia.tipoContenido` | texto | sí |  |
| `datos[].verificaciones[].evidencia.tamano` | entero | sí |  |
| `datos[].verificaciones[].evidencia.sha256` | texto | sí |  |
| `datos[].verificaciones[].evidencia.delProveedor` | booleano | sí |  |
| `datos[].verificaciones[].evidencia.sha256Respuesta` | texto o null | no |  |
| `datos[].verificaciones[].personas` | lista de PersonaConsultadaDto | sí |  |
| `datos[].verificaciones[].personas[].rol` | texto | sí |  |
| `datos[].verificaciones[].personas[].nombre` | texto | sí |  |
| `datos[].verificaciones[].personas[].identificacion` | texto | sí |  |
| `datos[].verificaciones[].coincidencias` | lista de CoincidenciaDto | sí |  |
| `datos[].verificaciones[].coincidencias[].id` | uuid | sí |  |
| `datos[].verificaciones[].coincidencias[].persona` | texto | sí |  |
| `datos[].verificaciones[].coincidencias[].rol` | texto | sí |  |
| `datos[].verificaciones[].coincidencias[].identificacion` | texto | sí |  |
| `datos[].verificaciones[].coincidencias[].lista` | texto | sí |  |
| `datos[].verificaciones[].coincidencias[].nombreEncontrado` | texto | sí |  |
| `datos[].verificaciones[].coincidencias[].detalle` | texto o null | no |  |
| `datos[].verificaciones[].coincidencias[].puntaje` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `datos[].verificaciones[].coincidencias[].decision` | texto o null | no |  |
| `datos[].verificaciones[].coincidencias[].justificacion` | texto o null | no |  |
| `datos[].verificaciones[].coincidencias[].resueltaPor` | texto o null | no |  |
| `datos[].verificaciones[].coincidencias[].resueltaEn` | fecha y hora o null | no |  |
| `datos[].verificaciones[].coincidencias[].vigenteHasta` | fecha y hora o null | no |  |
| `datos[].verificaciones[].coincidencias[].heredada` | booleano | sí |  |
| `datos[].verificaciones[].estudio` | EstudioCumplimientoDto o null | no |  |
| `datos[].verificaciones[].estudio.verificadoEnListas` | booleano | sí |  |
| `datos[].verificaciones[].estudio.conflictoDeInteres` | booleano | sí |  |
| `datos[].verificaciones[].estudio.debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `datos[].verificaciones[].estudio.nivelRiesgo` | entero | sí |  |
| `datos[].verificaciones[].estudio.observaciones` | texto o null | no |  |
| `datos[].verificaciones[].estudio.evidencias` | lista de EvidenciaEstudioDto | sí |  |
| `datos[].verificaciones[].estudio.evidencias[].documentoId` | uuid | sí |  |
| `datos[].verificaciones[].estudio.evidencias[].version` | entero | sí |  |
| `datos[].verificaciones[].estudio.evidencias[].nombre` | texto o null | no |  |
| `datos[].verificaciones[].estudio.concepto` | ConceptoCumplimiento (texto) | sí | valores: Favorable · Desfavorable |
| `datos[].verificaciones[].estudio.oficial` | texto | sí |  |
| `datos[].verificaciones[].estudio.fecha` | fecha y hora | sí |  |
| `datos[].verificaciones[].estudio.vigenteHasta` | fecha y hora | sí |  |
| `datos[].verificaciones[].estudio.matriz` | ResultadoDeMatrizDto o null | no |  |
| `datos[].verificaciones[].estudio.matriz.puntaje` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `datos[].verificaciones[].estudio.matriz.nivel` | entero | sí |  |
| `datos[].verificaciones[].estudio.matriz.nivelPorPuntaje` | entero | sí |  |
| `datos[].verificaciones[].estudio.matriz.forzadoPor` | texto o null | no |  |
| `datos[].verificaciones[].estudio.matriz.debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `datos[].verificaciones[].estudio.matriz.mesesRevision` | entero | sí |  |
| `datos[].verificaciones[].estudio.matriz.versionMatriz` | entero | sí |  |
| `datos[].verificaciones[].estudio.matriz.desglose` | lista de DesgloseDeMatrizDto | sí |  |
| `datos[].verificaciones[].estudio.matriz.desglose[].factor` | texto | sí |  |
| `datos[].verificaciones[].estudio.matriz.desglose[].factorNombre` | texto | sí |  |
| `datos[].verificaciones[].estudio.matriz.desglose[].origen` | OrigenDelFactor (texto) | sí | valores: Manual · Automatico |
| `datos[].verificaciones[].estudio.matriz.desglose[].peso` | entero | sí |  |
| `datos[].verificaciones[].estudio.matriz.desglose[].opcion` | texto | sí |  |
| `datos[].verificaciones[].estudio.matriz.desglose[].opcionNombre` | texto | sí |  |
| `datos[].verificaciones[].estudio.matriz.desglose[].puntaje` | entero | sí |  |
| `datos[].verificaciones[].estudio.matriz.desglose[].nivelMinimo` | entero o null | no |  |
| `datos[].verificaciones[].estudio.justificacionDeNivel` | texto o null | no |  |
| `datos[].verificaciones[].estudio.adjuntos` | lista de AdjuntoDeCumplimientoDto o null | no |  |
| `datos[].verificaciones[].estudio.adjuntos[].id` | uuid | sí |  |
| `datos[].verificaciones[].estudio.adjuntos[].nombre` | texto | sí |  |
| `datos[].verificaciones[].estudio.adjuntos[].tipoContenido` | texto | sí |  |
| `datos[].verificaciones[].estudio.adjuntos[].tamano` | entero | sí |  |
| `datos[].verificaciones[].estudio.adjuntos[].sha256` | texto | sí |  |
| `datos[].verificaciones[].estudio.adjuntos[].proposito` | PropositoDeAdjunto (texto) | sí | valores: Estudio · ConsultaManual |
| `datos[].verificaciones[].estudio.adjuntos[].subidoPor` | texto | sí |  |
| `datos[].verificaciones[].estudio.adjuntos[].fecha` | fecha y hora | sí |  |
| `…` |  | no | Se omiten los campos restantes: el esquema es muy extenso. |
| `…` |  | no | Se omiten los campos restantes: el esquema es muy extenso. |
| `…` |  | no | Se omiten los campos restantes: el esquema es muy extenso. |

**Errores de negocio que lanza**

- `422` `estado-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/expedientes" \
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
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
      "tipo": "Inicial",
      "estado": "Abierto",
      "flujo": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "version": 1,
        "origen": "Explicito"
      },
      "politicaDevolucion": "Inmediata",
      "separacionDeFunciones": true,
      "etapaActual": 1,
      "etapas": [
        {
          "orden": 1,
          "estado": "Esperando",
          "activadaEn": "2026-11-12T15:00:00Z",
          "aprobadaEn": "2026-11-12T15:00:00Z",
          "areas": [
            {
              "areaId": null,
              "codigo": null,
              "nombre": null,
              "estado": null,
              "aprobadoresRequeridos": null,
              "aprobaciones": null,
              "aprobacionesFaltantes": null,
              "miembrosPendientes": null,
              "puedeRechazarExpediente": null,
              "automatica": null,
              "pendienteDesde": null,
              "slaVence": null,
              "veredictos": null,
              "requisitosPorDecidir": null,
              "documentosPorDecidir": null
            }
          ]
        }
      ],
      "reenvios": 1,
      "ultimaRespuesta": "texto",
      "rechazo": {
        "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "usuario": "texto",
        "motivos": [
          "Texto de ejemplo."
        ],
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z",
        "origen": "texto"
      },
      "motivoAnulacion": "Texto de ejemplo.",
      "abiertoEn": "2026-11-12T15:00:00Z",
      "cerradoEn": "2026-11-12T15:00:00Z",
      "actualizadoEn": "2026-11-12T15:00:00Z",
      "version": 1,
      "requisitos": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "tipoDocumento": {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "codigo": "CODIGO_EJEMPLO",
            "nombre": "Constructora Norte S.A.S.",
            "tipoCanonico": "texto",
            "ambito": "Global",
            "modo": "texto",
            "campos": [
              null
            ],
            "archivo": {
              "formatos": null,
              "tamanoMaximoBytes": null,
              "paginasMaximas": null,
              "descripcion": null
            },
            "tienePlantilla": false,
            "textoAyuda": "Texto de ejemplo."
          },
          "obligatorio": true,
          "estado": "Pendiente",
          "versionActual": {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "n": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "cargadaEn": "2026-11-12T15:00:00Z",
            "datosDeclarados": [
              null
            ],
            "vigencia": {
              "vigenteHasta": null,
              "base": null,
              "estado": null,
              "diasAviso": null,
              "diasGracia": null,
              "finDeGracia": null,
              "vencidaEn": null
            },
            "estado": "texto",
            "clase": "texto"
          },
          "reenvios": 1,
          "exencion": {
            "por": "texto",
            "justificacion": "Texto de ejemplo.",
            "fecha": "2026-11-12T15:00:00Z"
          },
          "areas": [
            {
              "areaId": null,
              "codigo": null,
              "estado": null,
              "aprobadoresRequeridos": null,
              "aprobaciones": null,
              "veredictos": null
            }
          ],
          "tipoRequisito": "Documento",
          "seccion": {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "exigeSoporte": true,
            "completitud": {
              "completa": null,
              "faltantes": null,
              "reglaVersion": null
            },
            "versionEvaluada": 1,
            "datos": {},
            "soportes": [
              null
            ],
            "versionAceptada": 1,
            "cambios": [
              null
            ],
            "cruce": {
              "estado": null,
              "discrepanciaCritica": null,
              "cruces": null,
              "sinCruce": null
            },
            "sinSoporte": {
              "critico": null,
              "cambios": null
            }
          },
          "soporteDeSecciones": {
            "sinAnalisis": true,
            "discrepanciaCritica": true,
            "secciones": [
              null
            ]
          },
          "retiro": {
            "numero": 1,
            "motivo": "Texto de ejemplo.",
            "fecha": "2026-11-12T15:00:00Z"
          },
          "revisionesAutomaticas": [
            {
              "areaId": null,
              "areaCodigo": null,
              "sobreVersion": null,
              "nivel": null,
              "resultado": null,
              "sugerencia": null,
              "motivos": null,
              "confianza": null,
              "fecha": null,
              "pendienteDeAtencion": null,
              "resolucion": null,
              "resueltaPor": null,
              "resueltaEn": null,
              "justificacionResolucion": null
            }
          ]
        }
      ],
      "verificaciones": [
        {
          "tipo": "ListasRestrictivas",
          "estado": "Pendiente",
          "bloqueante": true,
          "requeridaPorElFlujo": true,
          "proveedor": "texto",
          "resultado": "texto",
          "referenciaExterna": "REF-0001",
          "registradaEn": "2026-11-12T15:00:00Z",
          "registradaPor": "texto",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "evidencia": {
            "nombreArchivo": "Nombre de ejemplo",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "delProveedor": true,
            "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
          },
          "personas": [
            {
              "rol": null,
              "nombre": null,
              "identificacion": null
            }
          ],
          "coincidencias": [
            {
              "id": null,
              "persona": null,
              "rol": null,
              "identificacion": null,
              "lista": null,
              "nombreEncontrado": null,
              "detalle": null,
              "puntaje": null,
              "decision": null,
              "justificacion": null,
              "resueltaPor": null,
              "resueltaEn": null,
              "vigenteHasta": null,
              "heredada": null
            }
          ],
          "estudio": {
            "verificadoEnListas": true,
            "conflictoDeInteres": true,
            "debidaDiligencia": "Simple",
            "nivelRiesgo": 1,
            "observaciones": "Texto de ejemplo.",
            "evidencias": [
              null
            ],
            "concepto": "Favorable",
            "oficial": "texto",
            "fecha": "2026-11-12T15:00:00Z",
            "vigenteHasta": "2026-11-12T15:00:00Z",
            "matriz": {
              "puntaje": null,
              "nivel": null,
              "nivelPorPuntaje": null,
              "forzadoPor": null,
              "debidaDiligencia": null,
              "mesesRevision": null,
              "versionMatriz": null,
              "desglose": null
            },
            "justificacionDeNivel": "Texto de ejemplo.",
            "adjuntos": [
              null
            ]
          },
          "anteriores": [
            {
              "proveedor": null,
              "referenciaExterna": null,
              "resultado": null,
              "fecha": null,
              "coincidencias": null,
              "conEvidencia": null
            }
          ],
          "ultimoError": "texto",
          "reintentarDesde": "2026-11-12T15:00:00Z",
          "vigenciaDias": 1,
          "frecuenciaReconsultaMeses": 1
        }
      ],
      "esperandoVerificaciones": false,
      "plazoCargue": {
        "dias": 1,
        "recordatorios": [
          1
        ],
        "hasta": "2026-11-12T15:00:00Z",
        "recordatoriosEnviados": [
          1
        ],
        "caducadoEn": "2026-11-12T15:00:00Z",
        "estadoAlReabrir": "texto",
        "reaperturas": 1
      },
      "desaprobaciones": 0,
      "ultimaDesaprobacion": {
        "etapa": 1,
        "alcance": "texto",
        "usuario": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "recordatorioObservaciones": {
        "ultimoEn": "2026-11-12T15:00:00Z",
        "total": 1
      },
      "adjuntosDeCumplimiento": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "nombre": "Constructora Norte S.A.S.",
          "tipoContenido": "Texto de ejemplo.",
          "tamano": 1,
          "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
          "proposito": "Estudio",
          "subidoPor": "texto",
          "fecha": "2026-11-12T15:00:00Z"
        }
      ]
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/expedientes/{id}`

**Obtener expediente.** Estado, etapas y áreas con sus veredictos, aprobaciones y miembros que faltan por pronunciarse.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `folio` | texto | sí |  |
| `empresaId` | uuid | sí |  |
| `vinculoId` | uuid | sí |  |
| `tercero` | TerceroResumenDto | sí |  |
| `tercero.id` | uuid | sí |  |
| `tercero.nombre` | texto | sí |  |
| `tercero.identificacion` | texto | sí |  |
| `tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `tipoVinculo.id` | uuid | sí |  |
| `tipoVinculo.codigo` | texto | sí |  |
| `tipoVinculo.nombre` | texto | sí |  |
| `tipo` | TipoExpediente (texto) | sí | valores: Inicial · Renovacion · Actualizacion |
| `estado` | EstadoExpediente (texto) | sí | valores: Abierto · EnRevision · Observado · Aprobado · Rechazado · Anulado · Caducado |
| `flujo` | FlujoDeExpedienteDto o null | no |  |
| `flujo.id` | uuid | sí |  |
| `flujo.versionId` | uuid | sí |  |
| `flujo.codigo` | texto | sí |  |
| `flujo.nombre` | texto | sí |  |
| `flujo.version` | entero | sí |  |
| `flujo.origen` | OrigenFlujo (texto) | sí | valores: Explicito · Regla · PorDefecto |
| `politicaDevolucion` | PoliticaDevolucion (texto) | sí | valores: Inmediata · AlCerrarArea |
| `separacionDeFunciones` | booleano | sí |  |
| `etapaActual` | entero o null | no |  |
| `etapas` | lista de EtapaExpedienteDto | sí |  |
| `etapas[].orden` | entero | sí |  |
| `etapas[].estado` | EstadoEtapa (texto) | sí | valores: Esperando · Activa · Aprobada |
| `etapas[].activadaEn` | fecha y hora o null | no |  |
| `etapas[].aprobadaEn` | fecha y hora o null | no |  |
| `etapas[].areas` | lista de AreaExpedienteDto | sí |  |
| `etapas[].areas[].areaId` | uuid | sí |  |
| `etapas[].areas[].codigo` | texto | sí |  |
| `etapas[].areas[].nombre` | texto | sí |  |
| `etapas[].areas[].estado` | EstadoAreaExpediente (texto) | sí | valores: Esperando · Pendiente · Aprobada · Observada |
| `etapas[].areas[].aprobadoresRequeridos` | entero | sí |  |
| `etapas[].areas[].aprobaciones` | entero | sí |  |
| `etapas[].areas[].aprobacionesFaltantes` | entero | sí |  |
| `etapas[].areas[].miembrosPendientes` | lista de uuid | sí |  |
| `etapas[].areas[].puedeRechazarExpediente` | booleano | sí |  |
| `etapas[].areas[].automatica` | booleano | sí |  |
| `etapas[].areas[].pendienteDesde` | fecha y hora o null | no |  |
| `etapas[].areas[].slaVence` | fecha y hora o null | no |  |
| `etapas[].areas[].veredictos` | lista de VeredictoEmitidoDto | sí |  |
| `etapas[].areas[].veredictos[].id` | uuid | sí |  |
| `etapas[].areas[].veredictos[].veredicto` | Veredicto (texto) | sí | valores: Aprobado · Observado |
| `etapas[].areas[].veredictos[].origen` | OrigenVeredicto (texto) | sí | valores: Humano · Sistema · Administrador |
| `etapas[].areas[].veredictos[].usuario` | texto o null | no |  |
| `etapas[].areas[].veredictos[].fecha` | fecha y hora | sí |  |
| `etapas[].areas[].veredictos[].motivos` | lista de texto | sí |  |
| `etapas[].areas[].veredictos[].observacion` | texto o null | no |  |
| `etapas[].areas[].veredictos[].justificacion` | texto o null | no |  |
| `etapas[].areas[].veredictos[].vigente` | booleano | sí |  |
| `etapas[].areas[].veredictos[].superado` | booleano | sí |  |
| `etapas[].areas[].veredictos[].anulado` | booleano | sí |  |
| `etapas[].areas[].veredictos[].anuladoPor` | texto o null | no |  |
| `etapas[].areas[].veredictos[].sobreVersion` | entero o null | no |  |
| `etapas[].areas[].veredictos[].invalidado` | booleano | no | por omisión false |
| `etapas[].areas[].veredictos[].derivado` | booleano | no | por omisión false |
| `etapas[].areas[].veredictos[].campos` | lista de texto o null | no |  |
| `etapas[].areas[].veredictos[].revertido` | booleano | no | por omisión false |
| `etapas[].areas[].requisitosPorDecidir` | entero | no | por omisión 0 |
| `etapas[].areas[].documentosPorDecidir` | lista de DocumentoPorDecidirDto o null | no |  |
| `etapas[].areas[].documentosPorDecidir[].requisitoId` | uuid | sí |  |
| `etapas[].areas[].documentosPorDecidir[].versionVigente` | entero | sí |  |
| `etapas[].areas[].documentosPorDecidir[].tipoRequisito` | TipoRequisito (texto) | no | valores: Documento · Seccion |
| `etapas[].areas[].documentosPorDecidir[].seccion` | texto o null | no |  |
| `etapas[].areas[].documentosPorDecidir[].periodo` | entero o null | no |  |
| `reenvios` | entero | sí |  |
| `ultimaRespuesta` | texto o null | no |  |
| `rechazo` | RechazoDto o null | no |  |
| `rechazo.areaId` | uuid | sí |  |
| `rechazo.usuario` | texto | sí |  |
| `rechazo.motivos` | lista de texto | sí |  |
| `rechazo.justificacion` | texto | sí |  |
| `rechazo.fecha` | fecha y hora | sí |  |
| `rechazo.origen` | texto o null | no |  |
| `motivoAnulacion` | texto o null | no |  |
| `abiertoEn` | fecha y hora | sí |  |
| `cerradoEn` | fecha y hora o null | no |  |
| `actualizadoEn` | fecha y hora | sí |  |
| `version` | entero | sí |  |
| `requisitos` | lista de RequisitoExpedienteDto o null | no |  |
| `requisitos[].id` | uuid | sí |  |
| `requisitos[].tipoDocumento` | TipoDocumentoRequisitoDto | sí |  |
| `requisitos[].tipoDocumento.id` | uuid | sí |  |
| `requisitos[].tipoDocumento.codigo` | texto | sí |  |
| `requisitos[].tipoDocumento.nombre` | texto | sí |  |
| `requisitos[].tipoDocumento.tipoCanonico` | texto o null | no |  |
| `requisitos[].tipoDocumento.ambito` | AmbitoDocumento (texto) | sí | valores: Global · PorVinculo |
| `requisitos[].tipoDocumento.modo` | texto o null | no |  |
| `requisitos[].tipoDocumento.campos` | lista de CampoFormularioDto o null | no |  |
| `requisitos[].tipoDocumento.campos[].clave` | texto | sí |  |
| `requisitos[].tipoDocumento.campos[].etiqueta` | texto | sí |  |
| `requisitos[].tipoDocumento.campos[].tipo` | texto | sí |  |
| `requisitos[].tipoDocumento.campos[].obligatorio` | booleano | no | por omisión false |
| `requisitos[].tipoDocumento.campos[].opciones` | lista de texto o null | no |  |
| `requisitos[].tipoDocumento.campos[].ayuda` | texto o null | no |  |
| `requisitos[].tipoDocumento.archivo` | ReglasArchivoDto o null | no |  |
| `requisitos[].tipoDocumento.archivo.formatos` | lista de texto | sí |  |
| `requisitos[].tipoDocumento.archivo.tamanoMaximoBytes` | entero | sí |  |
| `requisitos[].tipoDocumento.archivo.paginasMaximas` | entero | sí |  |
| `requisitos[].tipoDocumento.archivo.descripcion` | texto | sí |  |
| `requisitos[].tipoDocumento.tienePlantilla` | booleano | no | por omisión false |
| `requisitos[].tipoDocumento.textoAyuda` | texto o null | no |  |
| `requisitos[].obligatorio` | booleano | sí |  |
| `requisitos[].estado` | EstadoRequisito (texto) | sí | valores: Pendiente · Cargado · EnRevision · Observado · Aprobado · Eximido |
| `requisitos[].versionActual` | VersionRequisitoDto o null | no |  |
| `requisitos[].versionActual.documentoId` | uuid | sí |  |
| `requisitos[].versionActual.n` | entero | sí |  |
| `requisitos[].versionActual.sha256` | texto | sí |  |
| `requisitos[].versionActual.cargadaEn` | fecha y hora | sí |  |
| `requisitos[].versionActual.datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `requisitos[].versionActual.datosDeclarados[].clave` | texto | sí |  |
| `requisitos[].versionActual.datosDeclarados[].etiqueta` | texto | sí |  |
| `requisitos[].versionActual.datosDeclarados[].tipo` | texto | sí |  |
| `requisitos[].versionActual.datosDeclarados[].valor` | texto | sí |  |
| `requisitos[].versionActual.vigencia` | VigenciaDto o null | no |  |
| `requisitos[].versionActual.vigencia.vigenteHasta` | fecha o null | no |  |
| `requisitos[].versionActual.vigencia.base` | texto | sí |  |
| `requisitos[].versionActual.vigencia.estado` | texto | sí |  |
| `requisitos[].versionActual.vigencia.diasAviso` | lista de entero | sí |  |
| `requisitos[].versionActual.vigencia.diasGracia` | entero | sí |  |
| `requisitos[].versionActual.vigencia.finDeGracia` | fecha o null | no |  |
| `requisitos[].versionActual.vigencia.vencidaEn` | fecha y hora o null | no |  |
| `requisitos[].versionActual.estado` | texto o null | no |  |
| `requisitos[].versionActual.clase` | texto o null | no |  |
| `requisitos[].reenvios` | entero | sí |  |
| `requisitos[].exencion` | ExencionDto o null | no |  |
| `requisitos[].exencion.por` | texto | sí |  |
| `requisitos[].exencion.justificacion` | texto | sí |  |
| `requisitos[].exencion.fecha` | fecha y hora | sí |  |
| `requisitos[].areas` | lista de AreaDeRequisitoDto | sí |  |
| `requisitos[].areas[].areaId` | uuid | sí |  |
| `requisitos[].areas[].codigo` | texto | sí |  |
| `requisitos[].areas[].estado` | EstadoAreaRequisito (texto) | sí | valores: Esperando · Pendiente · Aprobado · Observado · Eximido |
| `requisitos[].areas[].aprobadoresRequeridos` | entero | sí |  |
| `requisitos[].areas[].aprobaciones` | entero | sí |  |
| `requisitos[].areas[].veredictos` | lista de VeredictoEmitidoDto | sí | mismos campos que `etapas[].areas[].veredictos[]` |
| `requisitos[].tipoRequisito` | TipoRequisito (texto) | no | valores: Documento · Seccion |
| `requisitos[].seccion` | SeccionDeRequisitoDto o null | no |  |
| `requisitos[].seccion.seccion` | texto | sí |  |
| `requisitos[].seccion.nombre` | texto | sí |  |
| `requisitos[].seccion.periodo` | entero o null | no |  |
| `requisitos[].seccion.exigeSoporte` | booleano | sí |  |
| `requisitos[].seccion.completitud` | CompletitudSeccionDto o null | no |  |
| `requisitos[].seccion.completitud.completa` | booleano | sí |  |
| `requisitos[].seccion.completitud.faltantes` | lista de FaltanteSeccionDto | sí |  |
| `requisitos[].seccion.completitud.faltantes[].campo` | texto o null | no |  |
| `requisitos[].seccion.completitud.faltantes[].regla` | texto o null | no |  |
| `requisitos[].seccion.completitud.faltantes[].mensaje` | texto | sí |  |
| `requisitos[].seccion.completitud.reglaVersion` | entero | sí |  |
| `requisitos[].seccion.versionEvaluada` | entero o null | no |  |
| `requisitos[].seccion.datos` | JsonElement o null | no |  |
| `requisitos[].seccion.soportes` | lista de SoporteSeccionDto | sí |  |
| `requisitos[].seccion.soportes[].documentoId` | uuid | sí |  |
| `requisitos[].seccion.soportes[].version` | entero | sí |  |
| `requisitos[].seccion.soportes[].tipoDocumento` | texto o null | no |  |
| `requisitos[].seccion.soportes[].retirado` | booleano | no | por omisión false |
| `requisitos[].seccion.soportes[].nombre` | texto o null | no |  |
| `requisitos[].seccion.versionAceptada` | entero o null | no |  |
| `requisitos[].seccion.cambios` | lista de CambioDeCampoDto | sí |  |
| `requisitos[].seccion.cambios[].campo` | texto | sí |  |
| `requisitos[].seccion.cambios[].etiqueta` | texto | sí |  |
| `requisitos[].seccion.cambios[].anterior` | texto o null | no |  |
| `requisitos[].seccion.cambios[].nuevo` | texto o null | no |  |
| `requisitos[].seccion.cambios[].tipoCambio` | TipoCambio (texto) | sí | valores: Agregado · Quitado · Modificado |
| `requisitos[].seccion.cruce` | CruceDeRequisitoDto o null | no |  |
| `requisitos[].seccion.cruce.estado` | texto | sí |  |
| `requisitos[].seccion.cruce.discrepanciaCritica` | booleano | sí |  |
| `requisitos[].seccion.cruce.cruces` | lista de CruceDeSeccionDto | sí |  |
| `requisitos[].seccion.cruce.cruces[].documentoId` | uuid | sí |  |
| `requisitos[].seccion.cruce.cruces[].versionDocumento` | entero | sí |  |
| `requisitos[].seccion.cruce.cruces[].tipoDocumento` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].nombreDocumento` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `requisitos[].seccion.cruce.cruces[].legible` | booleano | sí |  |
| `requisitos[].seccion.cruce.cruces[].fecha` | fecha y hora | sí |  |
| `requisitos[].seccion.cruce.cruces[].coincidencias` | entero | sí |  |
| `requisitos[].seccion.cruce.cruces[].discrepancias` | entero | sí |  |
| `requisitos[].seccion.cruce.cruces[].noVerificables` | entero | sí |  |
| `requisitos[].seccion.cruce.cruces[].discrepanciaCritica` | booleano | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos` | lista de CampoCruzadoDto | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].campo` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].etiqueta` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].declarado` | texto o null | no |  |
| `requisitos[].seccion.cruce.cruces[].campos[].extraido` | texto o null | no |  |
| `requisitos[].seccion.cruce.cruces[].campos[].resultado` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].critico` | booleano | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].detalle` | texto o null | no |  |
| `requisitos[].seccion.cruce.sinCruce` | lista de DocumentoSinCruceDto | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].documentoId` | uuid | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].version` | entero | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].tipoDocumento` | texto | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].nombreDocumento` | texto | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].motivo` | texto | sí |  |
| `requisitos[].seccion.sinSoporte` | SinSoporteDto o null | no |  |
| `requisitos[].seccion.sinSoporte.critico` | booleano | sí |  |
| `requisitos[].seccion.sinSoporte.cambios` | lista de CambioDeCampoDto | sí | mismos campos que `requisitos[].seccion.cambios[]` |
| `requisitos[].soporteDeSecciones` | SoporteDeSeccionesDto o null | no |  |
| `requisitos[].soporteDeSecciones.sinAnalisis` | booleano | sí |  |
| `requisitos[].soporteDeSecciones.discrepanciaCritica` | booleano | sí |  |
| `requisitos[].soporteDeSecciones.secciones` | lista de SeccionRespaldadaDto | sí |  |
| `requisitos[].soporteDeSecciones.secciones[].seccion` | texto | sí |  |
| `requisitos[].soporteDeSecciones.secciones[].nombre` | texto | sí |  |
| `requisitos[].soporteDeSecciones.secciones[].periodo` | entero o null | no |  |
| `requisitos[].soporteDeSecciones.secciones[].version` | entero o null | no |  |
| `requisitos[].soporteDeSecciones.secciones[].cruce` | CruceDeSeccionDto o null | no | mismos campos que `requisitos[].seccion.cruce.cruces[]` |
| `requisitos[].retiro` | RetiroDeVersionDto o null | no |  |
| `requisitos[].retiro.numero` | entero | sí |  |
| `requisitos[].retiro.motivo` | texto | sí |  |
| `requisitos[].retiro.fecha` | fecha y hora | sí |  |
| `requisitos[].revisionesAutomaticas` | lista de RevisionAutomaticaDto o null | no |  |
| `requisitos[].revisionesAutomaticas[].areaId` | uuid | sí |  |
| `requisitos[].revisionesAutomaticas[].areaCodigo` | texto | sí |  |
| `requisitos[].revisionesAutomaticas[].sobreVersion` | entero | sí |  |
| `requisitos[].revisionesAutomaticas[].nivel` | texto | sí |  |
| `requisitos[].revisionesAutomaticas[].resultado` | texto | sí |  |
| `requisitos[].revisionesAutomaticas[].sugerencia` | texto o null | no |  |
| `requisitos[].revisionesAutomaticas[].motivos` | lista de MotivoDeRevisionDto | sí |  |
| `requisitos[].revisionesAutomaticas[].motivos[].codigo` | texto | sí |  |
| `requisitos[].revisionesAutomaticas[].motivos[].texto` | texto | sí |  |
| `requisitos[].revisionesAutomaticas[].confianza` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `requisitos[].revisionesAutomaticas[].fecha` | fecha y hora | sí |  |
| `requisitos[].revisionesAutomaticas[].pendienteDeAtencion` | booleano | sí |  |
| `requisitos[].revisionesAutomaticas[].resolucion` | texto o null | no |  |
| `requisitos[].revisionesAutomaticas[].resueltaPor` | texto o null | no |  |
| `requisitos[].revisionesAutomaticas[].resueltaEn` | fecha y hora o null | no |  |
| `requisitos[].revisionesAutomaticas[].justificacionResolucion` | texto o null | no |  |
| `verificaciones` | lista de VerificacionDto o null | no |  |
| `verificaciones[].tipo` | TipoVerificacion (texto) | sí | valores: ListasRestrictivas · EstudioDeCumplimiento |
| `verificaciones[].estado` | EstadoVerificacion (texto) | sí | valores: Pendiente · CoincidenciaPorAnalizar · Satisfecha · Fallida · Expirada |
| `verificaciones[].bloqueante` | booleano | sí |  |
| `verificaciones[].requeridaPorElFlujo` | booleano | sí |  |
| `verificaciones[].proveedor` | texto o null | no |  |
| `verificaciones[].resultado` | texto o null | no |  |
| `verificaciones[].referenciaExterna` | texto o null | no |  |
| `verificaciones[].registradaEn` | fecha y hora o null | no |  |
| `verificaciones[].registradaPor` | texto o null | no |  |
| `verificaciones[].vigenteHasta` | fecha y hora o null | no |  |
| `verificaciones[].evidencia` | EvidenciaVerificacionDto o null | no |  |
| `verificaciones[].evidencia.nombreArchivo` | texto | sí |  |
| `verificaciones[].evidencia.tipoContenido` | texto | sí |  |
| `verificaciones[].evidencia.tamano` | entero | sí |  |
| `verificaciones[].evidencia.sha256` | texto | sí |  |
| `verificaciones[].evidencia.delProveedor` | booleano | sí |  |
| `verificaciones[].evidencia.sha256Respuesta` | texto o null | no |  |
| `verificaciones[].personas` | lista de PersonaConsultadaDto | sí |  |
| `verificaciones[].personas[].rol` | texto | sí |  |
| `verificaciones[].personas[].nombre` | texto | sí |  |
| `verificaciones[].personas[].identificacion` | texto | sí |  |
| `verificaciones[].coincidencias` | lista de CoincidenciaDto | sí |  |
| `verificaciones[].coincidencias[].id` | uuid | sí |  |
| `verificaciones[].coincidencias[].persona` | texto | sí |  |
| `verificaciones[].coincidencias[].rol` | texto | sí |  |
| `verificaciones[].coincidencias[].identificacion` | texto | sí |  |
| `verificaciones[].coincidencias[].lista` | texto | sí |  |
| `verificaciones[].coincidencias[].nombreEncontrado` | texto | sí |  |
| `verificaciones[].coincidencias[].detalle` | texto o null | no |  |
| `verificaciones[].coincidencias[].puntaje` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `verificaciones[].coincidencias[].decision` | texto o null | no |  |
| `verificaciones[].coincidencias[].justificacion` | texto o null | no |  |
| `verificaciones[].coincidencias[].resueltaPor` | texto o null | no |  |
| `verificaciones[].coincidencias[].resueltaEn` | fecha y hora o null | no |  |
| `verificaciones[].coincidencias[].vigenteHasta` | fecha y hora o null | no |  |
| `verificaciones[].coincidencias[].heredada` | booleano | sí |  |
| `verificaciones[].estudio` | EstudioCumplimientoDto o null | no |  |
| `verificaciones[].estudio.verificadoEnListas` | booleano | sí |  |
| `verificaciones[].estudio.conflictoDeInteres` | booleano | sí |  |
| `verificaciones[].estudio.debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `verificaciones[].estudio.nivelRiesgo` | entero | sí |  |
| `verificaciones[].estudio.observaciones` | texto o null | no |  |
| `verificaciones[].estudio.evidencias` | lista de EvidenciaEstudioDto | sí |  |
| `verificaciones[].estudio.evidencias[].documentoId` | uuid | sí |  |
| `verificaciones[].estudio.evidencias[].version` | entero | sí |  |
| `verificaciones[].estudio.evidencias[].nombre` | texto o null | no |  |
| `verificaciones[].estudio.concepto` | ConceptoCumplimiento (texto) | sí | valores: Favorable · Desfavorable |
| `verificaciones[].estudio.oficial` | texto | sí |  |
| `verificaciones[].estudio.fecha` | fecha y hora | sí |  |
| `verificaciones[].estudio.vigenteHasta` | fecha y hora | sí |  |
| `verificaciones[].estudio.matriz` | ResultadoDeMatrizDto o null | no |  |
| `verificaciones[].estudio.matriz.puntaje` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `verificaciones[].estudio.matriz.nivel` | entero | sí |  |
| `verificaciones[].estudio.matriz.nivelPorPuntaje` | entero | sí |  |
| `verificaciones[].estudio.matriz.forzadoPor` | texto o null | no |  |
| `verificaciones[].estudio.matriz.debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `verificaciones[].estudio.matriz.mesesRevision` | entero | sí |  |
| `verificaciones[].estudio.matriz.versionMatriz` | entero | sí |  |
| `verificaciones[].estudio.matriz.desglose` | lista de DesgloseDeMatrizDto | sí |  |
| `verificaciones[].estudio.matriz.desglose[].factor` | texto | sí |  |
| `verificaciones[].estudio.matriz.desglose[].factorNombre` | texto | sí |  |
| `verificaciones[].estudio.matriz.desglose[].origen` | OrigenDelFactor (texto) | sí | valores: Manual · Automatico |
| `verificaciones[].estudio.matriz.desglose[].peso` | entero | sí |  |
| `verificaciones[].estudio.matriz.desglose[].opcion` | texto | sí |  |
| `verificaciones[].estudio.matriz.desglose[].opcionNombre` | texto | sí |  |
| `verificaciones[].estudio.matriz.desglose[].puntaje` | entero | sí |  |
| `verificaciones[].estudio.matriz.desglose[].nivelMinimo` | entero o null | no |  |
| `verificaciones[].estudio.justificacionDeNivel` | texto o null | no |  |
| `verificaciones[].estudio.adjuntos` | lista de AdjuntoDeCumplimientoDto o null | no |  |
| `verificaciones[].estudio.adjuntos[].id` | uuid | sí |  |
| `verificaciones[].estudio.adjuntos[].nombre` | texto | sí |  |
| `…` |  | no | Se omiten los campos restantes: el esquema es muy extenso. |
| `…` |  | no | Se omiten los campos restantes: el esquema es muy extenso. |
| `…` |  | no | Se omiten los campos restantes: el esquema es muy extenso. |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/anular`

**Anular expediente.** Anula el expediente abierto (el vínculo vuelve a `Invitado` si estaba en validación).

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `anularExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, MotivoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `motivo` | texto | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/anular" \
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
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/areas/{areaId}/anular-veredicto`

**Anular veredicto.** Anula un veredicto vigente del área (el indicado o el más reciente), con justificación. No revierte un rechazo.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `anularVeredicto`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `areaId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, AnularVeredictoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto | sí |  |
| `veredictoId` | uuid o null | no |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/areas/3fa85f64-5717-4562-b3fc-2c963f66afa6/anular-veredicto" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"justificacion":"Texto de ejemplo.","veredictoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/areas/{areaId}/aprobar-como-administrador`

**Aprobar como administrador.** Aprobación en nombre del área (origen `Administrador`), con justificación.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `aprobarComoAdministrador`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `areaId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, JustificacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/areas/3fa85f64-5717-4562-b3fc-2c963f66afa6/aprobar-como-administrador" \
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
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/areas/{areaId}/veredicto`

**Emitir veredicto.** Decisión de un miembro del área. Sin requisitos (FL-1) es el veredicto sobre el expediente completo (`Observado` exige motivos). Con requisitos (DA-1) es la decisión del área sobre sus documentos: `Aprobado` («Aprobar área») aprueba de una vez lo que a esta persona le queda por decidir, con `versiones` (todo o nada); `Observado` observa los documentos de `requisitos[]` en un solo paso. Una observación con política `Inmediata` devuelve el expediente al tercero (un solo correo por el outbox) y bloquea los demás veredictos (409).

**Permiso expedientes:revisar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `emitirVeredicto`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `areaId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, VeredictoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `veredicto` | Veredicto (texto) | sí | valores: Aprobado · Observado |
| `motivos` | lista de texto o null | no |  |
| `observacion` | texto o null | no |  |
| `sobreVersion` | entero o null | no |  |
| `versiones` | mapa de texto → integer \| string o null | no |  |
| `requisitos` | lista de ObservacionDeRequisitoDto o null | no |  |
| `requisitos[].requisitoId` | uuid | sí |  |
| `requisitos[].sobreVersion` | entero o null | no |  |
| `requisitos[].motivos` | lista de texto o null | no |  |
| `requisitos[].observacion` | texto o null | no |  |
| `requisitos[].campos` | lista de texto o null | no |  |
| `campos` | lista de texto o null | no |  |
| `justificacion` | texto o null | no |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/areas/3fa85f64-5717-4562-b3fc-2c963f66afa6/veredicto" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"veredicto":"Aprobado","motivos":["Texto de ejemplo."],"observacion":"Texto de ejemplo.","sobreVersion":1,"versiones":{"clave":1},"requisitos":[{"requisitoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","sobreVersion":1,"motivos":["Texto de ejemplo."],"observacion":"Texto de ejemplo.","campos":["texto"]}],"campos":["texto"],"justificacion":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/desaprobar`

**Desaprobar expediente.** Desaprueba un expediente ya aprobado (DS-2, `DesaprobarTercero` del SRM): el mismo expediente vuelve a revisión desde `etapa` (por omisión la última), el vínculo pasa a `EnValidacion` y su certificado se revoca, todo en la misma transacción. 409 si el vínculo está bloqueado o terminado, tiene otro expediente abierto o este no es su último aprobado.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `desaprobarExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, DesaprobarExpedienteDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto | sí |  |
| `etapa` | entero o null | no |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/desaprobar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"justificacion":"Texto de ejemplo.","etapa":1}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/etapas/{orden}/reabrir`

**Reabrir etapa expediente.** Reabre una etapa ya aprobada de un expediente en revisión o devuelto al tercero (DS-1, `DesaprobarAFlujo` del SRM): sus áreas vuelven a quedar pendientes y las etapas posteriores esperan; los veredictos de esas etapas dejan de contar (quedan en el historial). Justificación obligatoria.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `reabrirEtapaExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `orden` | path | entero | sí |  |

**Cuerpo de la petición** (`application/json`, JustificacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/etapas/1/reabrir" \
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
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/fase-inicial`

**Volver afase inicial expediente.** Devuelve todo el expediente a su fase inicial (DS-1, `DesaprobarAEstadoInicial` del SRM): los veredictos vigentes dejan de contar y las etapas se repiten desde la primera. En revisión, devuelto al tercero o ya aprobado (entonces es también una desaprobación, DS-2).

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `volverAFaseInicialExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, JustificacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/fase-inicial" \
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
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `GET /api/v1/expedientes/{id}/historial`

**Obtener historial expediente.**

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerHistorialExpediente`**

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
curl -X GET "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/historial" \
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

### `POST /api/v1/expedientes/{id}/rechazar`

**Rechazar expediente.** Rechazo terminal por un área con `puedeRechazarExpediente` (motivos y justificación).

**Permiso expedientes:revisar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `rechazarExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, RechazarExpedienteDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `areaId` | uuid | sí |  |
| `motivos` | lista de texto | sí |  |
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/rechazar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"areaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","motivos":["Texto de ejemplo."],"justificacion":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/reenviar`

**Reenviar expediente.** El integrador responde a la observación en nombre del tercero; solo lo observado vuelve a revisar. Antes adjunta las versiones que la bóveda ya compartió y aún no llegaron por el outbox (`ReenvioConVersionesAsync`).

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `reenviarExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ReenviarExpedienteDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `respuesta` | texto o null | no |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/reenviar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"respuesta":"texto"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/requisitos/{requisitoId}/areas/{areaId}/veredicto`

**Emitir veredicto requisito.** Veredicto de un miembro del área sobre un requisito de su alcance (RQ-4): `sobreVersion` obligatorio y igual a la versión vigente del requisito (si no, 409 `version-superada`). El área queda aprobada cuando aprobó (o se eximieron) todos los requisitos de su alcance; una observación devuelve el expediente según la política del flujo.

**Permiso expedientes:revisar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `emitirVeredictoRequisito`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `requisitoId` | path | uuid | sí |  |
| `areaId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, VeredictoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `veredicto` | Veredicto (texto) | sí | valores: Aprobado · Observado |
| `motivos` | lista de texto o null | no |  |
| `observacion` | texto o null | no |  |
| `sobreVersion` | entero o null | no |  |
| `versiones` | mapa de texto → integer \| string o null | no |  |
| `requisitos` | lista de ObservacionDeRequisitoDto o null | no |  |
| `requisitos[].requisitoId` | uuid | sí |  |
| `requisitos[].sobreVersion` | entero o null | no |  |
| `requisitos[].motivos` | lista de texto o null | no |  |
| `requisitos[].observacion` | texto o null | no |  |
| `requisitos[].campos` | lista de texto o null | no |  |
| `campos` | lista de texto o null | no |  |
| `justificacion` | texto o null | no |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/requisitos/3fa85f64-5717-4562-b3fc-2c963f66afa6/areas/3fa85f64-5717-4562-b3fc-2c963f66afa6/veredicto" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"veredicto":"Aprobado","motivos":["Texto de ejemplo."],"observacion":"Texto de ejemplo.","sobreVersion":1,"versiones":{"clave":1},"requisitos":[{"requisitoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","sobreVersion":1,"motivos":["Texto de ejemplo."],"observacion":"Texto de ejemplo.","campos":["texto"]}],"campos":["texto"],"justificacion":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/requisitos/{requisitoId}/eximir`

**Eximir requisito.** Exime un requisito con justificación (RQ-6, `expedientes:administrar`): sus veredictos dejan de contar y, si era lo último que faltaba, el expediente pasa a revisión o sus áreas quedan aprobadas.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `eximirRequisito`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `requisitoId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, JustificacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/requisitos/3fa85f64-5717-4562-b3fc-2c963f66afa6/eximir" \
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
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/vinculos/{id}/expedientes`

**Abrir expediente.** Abre manualmente el expediente inicial del vínculo (administrador o integrador), con el flujo indicado o el resuelto (del vínculo o por defecto). No exige que el tercero haya aceptado. Reintenta si pierde la carrera del folio.

**Permiso expedientes:administrar · API (`/api/v1`) · SDK: `abrirExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, AbrirExpedienteDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `flujoId` | uuid o null | no |  |

**Respuestas**

- `201` Creado — ExpedienteDto

Campos de la respuesta `201`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

**Errores de negocio que lanza**

- `422` `falta-flujo-por-defecto`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/expedientes" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"flujoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

## Expedientes públicos

El tercero ve sus expedientes (solo los de los vínculos de su sesión): qué documentos le piden, cuáles faltan, cuáles están observados y con qué motivo (sin áreas ni revisores), y responde a las observaciones.

### `GET /public/v1/expedientes/{id}`

**Obtener expediente público.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerExpedientePublico`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — ExpedientePublicoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `folio` | texto | sí |  |
| `empresa` | EmpresaPublicaDto | sí |  |
| `empresa.id` | uuid | sí |  |
| `empresa.nombre` | texto | sí |  |
| `empresa.logoUrl` | texto o null | no |  |
| `empresa.colorMarca` | texto o null | no |  |
| `empresa.contactoSoporte` | texto o null | no |  |
| `tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `tipoVinculo.id` | uuid | sí |  |
| `tipoVinculo.codigo` | texto | sí |  |
| `tipoVinculo.nombre` | texto | sí |  |
| `estado` | EstadoExpediente (texto) | sí | valores: Abierto · EnRevision · Observado · Aprobado · Rechazado · Anulado · Caducado |
| `observaciones` | lista de ObservacionPublicaDto | sí |  |
| `observaciones[].motivos` | lista de MotivoPublicoDto | sí |  |
| `observaciones[].motivos[].codigo` | texto | sí |  |
| `observaciones[].motivos[].texto` | texto | sí |  |
| `observaciones[].observacion` | texto o null | no |  |
| `observaciones[].fecha` | fecha y hora | sí |  |
| `observaciones[].requisito` | texto o null | no |  |
| `observaciones[].campos` | lista de texto o null | no |  |
| `puedeReenviar` | booleano | sí |  |
| `reenvios` | entero | sí |  |
| `actualizadoEn` | fecha y hora | sí |  |
| `requisitos` | lista de RequisitoPublicoDto o null | no |  |
| `requisitos[].id` | uuid | sí |  |
| `requisitos[].tipoDocumento` | TipoDocumentoRequisitoDto | sí |  |
| `requisitos[].tipoDocumento.id` | uuid | sí |  |
| `requisitos[].tipoDocumento.codigo` | texto | sí |  |
| `requisitos[].tipoDocumento.nombre` | texto | sí |  |
| `requisitos[].tipoDocumento.tipoCanonico` | texto o null | no |  |
| `requisitos[].tipoDocumento.ambito` | AmbitoDocumento (texto) | sí | valores: Global · PorVinculo |
| `requisitos[].tipoDocumento.modo` | texto o null | no |  |
| `requisitos[].tipoDocumento.campos` | lista de CampoFormularioDto o null | no |  |
| `requisitos[].tipoDocumento.campos[].clave` | texto | sí |  |
| `requisitos[].tipoDocumento.campos[].etiqueta` | texto | sí |  |
| `requisitos[].tipoDocumento.campos[].tipo` | texto | sí |  |
| `requisitos[].tipoDocumento.campos[].obligatorio` | booleano | no | por omisión false |
| `requisitos[].tipoDocumento.campos[].opciones` | lista de texto o null | no |  |
| `requisitos[].tipoDocumento.campos[].ayuda` | texto o null | no |  |
| `requisitos[].tipoDocumento.archivo` | ReglasArchivoDto o null | no |  |
| `requisitos[].tipoDocumento.archivo.formatos` | lista de texto | sí |  |
| `requisitos[].tipoDocumento.archivo.tamanoMaximoBytes` | entero | sí |  |
| `requisitos[].tipoDocumento.archivo.paginasMaximas` | entero | sí |  |
| `requisitos[].tipoDocumento.archivo.descripcion` | texto | sí |  |
| `requisitos[].tipoDocumento.tienePlantilla` | booleano | no | por omisión false |
| `requisitos[].tipoDocumento.textoAyuda` | texto o null | no |  |
| `requisitos[].obligatorio` | booleano | sí |  |
| `requisitos[].estado` | EstadoRequisito (texto) | sí | valores: Pendiente · Cargado · EnRevision · Observado · Aprobado · Eximido |
| `requisitos[].versionActual` | VersionRequisitoDto o null | no |  |
| `requisitos[].versionActual.documentoId` | uuid | sí |  |
| `requisitos[].versionActual.n` | entero | sí |  |
| `requisitos[].versionActual.sha256` | texto | sí |  |
| `requisitos[].versionActual.cargadaEn` | fecha y hora | sí |  |
| `requisitos[].versionActual.datosDeclarados` | lista de DatoDeclaradoDto o null | no |  |
| `requisitos[].versionActual.datosDeclarados[].clave` | texto | sí |  |
| `requisitos[].versionActual.datosDeclarados[].etiqueta` | texto | sí |  |
| `requisitos[].versionActual.datosDeclarados[].tipo` | texto | sí |  |
| `requisitos[].versionActual.datosDeclarados[].valor` | texto | sí |  |
| `requisitos[].versionActual.vigencia` | VigenciaDto o null | no |  |
| `requisitos[].versionActual.vigencia.vigenteHasta` | fecha o null | no |  |
| `requisitos[].versionActual.vigencia.base` | texto | sí |  |
| `requisitos[].versionActual.vigencia.estado` | texto | sí |  |
| `requisitos[].versionActual.vigencia.diasAviso` | lista de entero | sí |  |
| `requisitos[].versionActual.vigencia.diasGracia` | entero | sí |  |
| `requisitos[].versionActual.vigencia.finDeGracia` | fecha o null | no |  |
| `requisitos[].versionActual.vigencia.vencidaEn` | fecha y hora o null | no |  |
| `requisitos[].versionActual.estado` | texto o null | no |  |
| `requisitos[].versionActual.clase` | texto o null | no |  |
| `requisitos[].observaciones` | lista de ObservacionPublicaDto | sí | mismos campos que `observaciones[]` |
| `requisitos[].puedeRatificar` | booleano | no | por omisión false |
| `requisitos[].ratificadoEn` | fecha y hora o null | no |  |
| `requisitos[].tipoRequisito` | TipoRequisito (texto) | no | valores: Documento · Seccion |
| `requisitos[].seccion` | SeccionDeRequisitoDto o null | no |  |
| `requisitos[].seccion.seccion` | texto | sí |  |
| `requisitos[].seccion.nombre` | texto | sí |  |
| `requisitos[].seccion.periodo` | entero o null | no |  |
| `requisitos[].seccion.exigeSoporte` | booleano | sí |  |
| `requisitos[].seccion.completitud` | CompletitudSeccionDto o null | no |  |
| `requisitos[].seccion.completitud.completa` | booleano | sí |  |
| `requisitos[].seccion.completitud.faltantes` | lista de FaltanteSeccionDto | sí |  |
| `requisitos[].seccion.completitud.faltantes[].campo` | texto o null | no |  |
| `requisitos[].seccion.completitud.faltantes[].regla` | texto o null | no |  |
| `requisitos[].seccion.completitud.faltantes[].mensaje` | texto | sí |  |
| `requisitos[].seccion.completitud.reglaVersion` | entero | sí |  |
| `requisitos[].seccion.versionEvaluada` | entero o null | no |  |
| `requisitos[].seccion.datos` | JsonElement o null | no |  |
| `requisitos[].seccion.soportes` | lista de SoporteSeccionDto | sí |  |
| `requisitos[].seccion.soportes[].documentoId` | uuid | sí |  |
| `requisitos[].seccion.soportes[].version` | entero | sí |  |
| `requisitos[].seccion.soportes[].tipoDocumento` | texto o null | no |  |
| `requisitos[].seccion.soportes[].retirado` | booleano | no | por omisión false |
| `requisitos[].seccion.soportes[].nombre` | texto o null | no |  |
| `requisitos[].seccion.versionAceptada` | entero o null | no |  |
| `requisitos[].seccion.cambios` | lista de CambioDeCampoDto | sí |  |
| `requisitos[].seccion.cambios[].campo` | texto | sí |  |
| `requisitos[].seccion.cambios[].etiqueta` | texto | sí |  |
| `requisitos[].seccion.cambios[].anterior` | texto o null | no |  |
| `requisitos[].seccion.cambios[].nuevo` | texto o null | no |  |
| `requisitos[].seccion.cambios[].tipoCambio` | TipoCambio (texto) | sí | valores: Agregado · Quitado · Modificado |
| `requisitos[].seccion.cruce` | CruceDeRequisitoDto o null | no |  |
| `requisitos[].seccion.cruce.estado` | texto | sí |  |
| `requisitos[].seccion.cruce.discrepanciaCritica` | booleano | sí |  |
| `requisitos[].seccion.cruce.cruces` | lista de CruceDeSeccionDto | sí |  |
| `requisitos[].seccion.cruce.cruces[].documentoId` | uuid | sí |  |
| `requisitos[].seccion.cruce.cruces[].versionDocumento` | entero | sí |  |
| `requisitos[].seccion.cruce.cruces[].tipoDocumento` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].nombreDocumento` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].confianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `requisitos[].seccion.cruce.cruces[].legible` | booleano | sí |  |
| `requisitos[].seccion.cruce.cruces[].fecha` | fecha y hora | sí |  |
| `requisitos[].seccion.cruce.cruces[].coincidencias` | entero | sí |  |
| `requisitos[].seccion.cruce.cruces[].discrepancias` | entero | sí |  |
| `requisitos[].seccion.cruce.cruces[].noVerificables` | entero | sí |  |
| `requisitos[].seccion.cruce.cruces[].discrepanciaCritica` | booleano | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos` | lista de CampoCruzadoDto | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].campo` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].etiqueta` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].declarado` | texto o null | no |  |
| `requisitos[].seccion.cruce.cruces[].campos[].extraido` | texto o null | no |  |
| `requisitos[].seccion.cruce.cruces[].campos[].resultado` | texto | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].critico` | booleano | sí |  |
| `requisitos[].seccion.cruce.cruces[].campos[].detalle` | texto o null | no |  |
| `requisitos[].seccion.cruce.sinCruce` | lista de DocumentoSinCruceDto | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].documentoId` | uuid | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].version` | entero | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].tipoDocumento` | texto | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].nombreDocumento` | texto | sí |  |
| `requisitos[].seccion.cruce.sinCruce[].motivo` | texto | sí |  |
| `requisitos[].seccion.sinSoporte` | SinSoporteDto o null | no |  |
| `requisitos[].seccion.sinSoporte.critico` | booleano | sí |  |
| `requisitos[].seccion.sinSoporte.cambios` | lista de CambioDeCampoDto | sí | mismos campos que `requisitos[].seccion.cambios[]` |
| `requisitosFaltantes` | entero | no | por omisión 0 |
| `tipo` | texto | no | por omisión "Inicial" |
| `plazoCargueHasta` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `404` `expediente-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresa": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S.",
    "logoUrl": "https://ejemplo.co/recurso",
    "colorMarca": "texto",
    "contactoSoporte": "texto"
  },
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "estado": "Abierto",
  "observaciones": [
    {
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
  ],
  "puedeReenviar": true,
  "reenvios": 1,
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "observaciones": [
        {
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "observacion": "Texto de ejemplo.",
          "fecha": "2026-11-12T15:00:00Z",
          "requisito": "texto",
          "campos": [
            "texto"
          ]
        }
      ],
      "puedeRatificar": false,
      "ratificadoEn": "2026-11-12T15:00:00Z",
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      }
    }
  ],
  "requisitosFaltantes": 0,
  "tipo": "Inicial",
  "plazoCargueHasta": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /public/v1/expedientes/{id}/reenviar`

**Reenviar expediente público.** Responde a la observación (`{ respuesta }`): lo observado vuelve a revisión. Con requisitos, cada documento observado necesita una versión nueva o, si no, la respuesta (409 `requisitos-sin-corregir`).

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `reenviarExpedientePublico`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ReenviarExpedienteDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `respuesta` | texto o null | no |  |

**Respuestas**

- `200` OK — ExpedientePublicoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedientePublicoDto` que se detallan en `GET /public/v1/expedientes/{id}`.

**Errores de negocio que lanza**

- `404` `expediente-no-encontrado`
- `409` `vinculo-bloqueado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/reenviar" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"respuesta":"texto"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresa": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S.",
    "logoUrl": "https://ejemplo.co/recurso",
    "colorMarca": "texto",
    "contactoSoporte": "texto"
  },
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "estado": "Abierto",
  "observaciones": [
    {
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
  ],
  "puedeReenviar": true,
  "reenvios": 1,
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "observaciones": [
        {
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "observacion": "Texto de ejemplo.",
          "fecha": "2026-11-12T15:00:00Z",
          "requisito": "texto",
          "campos": [
            "texto"
          ]
        }
      ],
      "puedeRatificar": false,
      "ratificadoEn": "2026-11-12T15:00:00Z",
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      }
    }
  ],
  "requisitosFaltantes": 0,
  "tipo": "Inicial",
  "plazoCargueHasta": "2026-11-12T15:00:00Z"
}
```

</details>

## Informe SLA

Informe de cumplimiento del SLA (OP-14, docs/11 §H8): por área, cuántas rondas de revisión tenían plazo, cuántas se resolvieron a tiempo y cuántas fuera, con el promedio de días y las revisiones que hoy están vencidas. Se calcula al consultar sobre los eventos de los expedientes (como el informe de estado: mismos filtros de fechas, hasta 5 000 expedientes) y se descarga en Excel.

### `GET /api/v1/informes/sla`

**Obtener informe SLA.**

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerInformeSla`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `desde` | query | fecha | no |  |
| `hasta` | query | fecha | no |  |
| `areaId` | query | uuid | no |  |
| `tipoVinculoId` | query | uuid | no |  |

**Respuestas**

- `200` OK — InformeSlaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `resumen` | ResumenSlaDto | sí |  |
| `resumen.expedientes` | entero | sí |  |
| `resumen.rondas` | entero | sí |  |
| `resumen.aDentroDelPlazo` | entero | sí |  |
| `resumen.fueraDelPlazo` | entero | sí |  |
| `resumen.cumplimiento` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `resumen.vencidosAhora` | entero | sí |  |
| `porArea` | lista de CumplimientoSlaDeAreaDto | sí |  |
| `porArea[].areaId` | uuid | sí |  |
| `porArea[].codigo` | texto | sí |  |
| `porArea[].nombre` | texto | sí |  |
| `porArea[].slaHoras` | entero o null | no |  |
| `porArea[].rondas` | entero | sí |  |
| `porArea[].aDentroDelPlazo` | entero | sí |  |
| `porArea[].fueraDelPlazo` | entero | sí |  |
| `porArea[].cumplimiento` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `porArea[].promedioDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `porArea[].vencidosAhora` | entero | sí |  |
| `vencidos` | lista de SlaVencidoDto | sí |  |
| `vencidos[].expedienteId` | uuid | sí |  |
| `vencidos[].folio` | texto | sí |  |
| `vencidos[].areaId` | uuid | sí |  |
| `vencidos[].areaNombre` | texto | sí |  |
| `vencidos[].tercero` | texto | sí |  |
| `vencidos[].vence` | fecha y hora | sí |  |
| `vencidos[].horasDeRetraso` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/sla" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "resumen": {
    "expedientes": 1,
    "rondas": 1,
    "aDentroDelPlazo": 1,
    "fueraDelPlazo": 1,
    "cumplimiento": 1,
    "vencidosAhora": 1
  },
  "porArea": [
    {
      "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S.",
      "slaHoras": 1,
      "rondas": 1,
      "aDentroDelPlazo": 1,
      "fueraDelPlazo": 1,
      "cumplimiento": 1,
      "promedioDias": 1,
      "vencidosAhora": 1
    }
  ],
  "vencidos": [
    {
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "areaNombre": "Nombre de ejemplo",
      "tercero": "texto",
      "vence": "2026-11-12T15:00:00Z",
      "horasDeRetraso": 1
    }
  ]
}
```

</details>

### `GET /api/v1/informes/sla/excel`

**Descargar informe SLA excel.**

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `descargarInformeSlaExcel`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `desde` | query | fecha | no |  |
| `hasta` | query | fecha | no |  |
| `areaId` | query | uuid | no |  |
| `tipoVinculoId` | query | uuid | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/sla/excel" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>

## Inicio

Inicio de la empresa activa (IN-1, IN-2, docs/06 `/inicio`): lo que le toca a quien consulta y los indicadores de la empresa. Se calculan al consultar sobre la bandeja, los vínculos, los expedientes y los certificados de la empresa (aún no son las proyecciones `KpisEmpresa`/`MisPendientes` de docs/03 §14).

### `GET /api/v1/indicadores`

**Obtener indicadores.** `dias`: ventana de los indicadores de flujo (30 por defecto, de 1 a 365), en días de calendario de la zona del negocio: hoy y los `dias` - 1 anteriores. Es el mismo rango del informe de estado con `fecha=Cierre&desde=…`, y los tiempos salen de la misma medida (`Estadistica`, IF-7).

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerIndicadores`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `dias` | query | entero | no |  |

**Respuestas**

- `200` OK — IndicadoresDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `dias` | entero | sí |  |
| `vinculosActivos` | entero | sí |  |
| `vinculosEnValidacion` | entero | sí |  |
| `vinculosInvitados` | entero | sí |  |
| `vinculosVencidos` | entero | sí |  |
| `vinculosBloqueados` | entero | sí |  |
| `expedientesEnCurso` | entero | sí |  |
| `expedientesObservados` | entero | sí |  |
| `expedientesAprobados` | entero | sí |  |
| `expedientesRechazados` | entero | sí |  |
| `tiempoPromedioDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `tiempoMedianaDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `certificadosVigentes` | entero | sí |  |
| `certificadosConPendientes` | entero | sí |  |
| `certificadosNoValidos` | entero | sí |  |
| `vinculosPorVencer` | entero | no | por omisión 0 |
| `vinculosEnRevision` | entero | no | por omisión 0 |
| `certificadosEnVerificacion` | entero | no | por omisión 0 |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/indicadores" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "dias": 1,
  "vinculosActivos": 1,
  "vinculosEnValidacion": 1,
  "vinculosInvitados": 1,
  "vinculosVencidos": 1,
  "vinculosBloqueados": 1,
  "expedientesEnCurso": 1,
  "expedientesObservados": 1,
  "expedientesAprobados": 1,
  "expedientesRechazados": 1,
  "tiempoPromedioDias": 1,
  "tiempoMedianaDias": 1,
  "certificadosVigentes": 1,
  "certificadosConPendientes": 1,
  "certificadosNoValidos": 1,
  "vinculosPorVencer": 0,
  "vinculosEnRevision": 0,
  "certificadosEnVerificacion": 0
}
```

</details>

### `GET /api/v1/mis-pendientes`

**Obtener mis pendientes.**

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerMisPendientes`**

**Respuestas**

- `200` OK — MisPendientesDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `porArea` | lista de PendientesDeAreaDto | sí |  |
| `porArea[].areaId` | uuid | sí |  |
| `porArea[].codigo` | texto | sí |  |
| `porArea[].nombre` | texto | sí |  |
| `porArea[].cantidad` | entero | sí |  |
| `porArea[].masAntiguoDias` | entero | sí |  |
| `revalidaciones` | entero | sí |  |
| `revalidacionesBloqueantes` | entero | sí |  |
| `cuentasPorAprobar` | entero | sí |  |
| `aperturasPendientes` | entero | sí |  |
| `documentosPorVencer` | entero | sí |  |
| `documentosVencidos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/mis-pendientes" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "porArea": [
    {
      "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S.",
      "cantidad": 1,
      "masAntiguoDias": 1
    }
  ],
  "revalidaciones": 1,
  "revalidacionesBloqueantes": 1,
  "cuentasPorAprobar": 1,
  "aperturasPendientes": 1,
  "documentosPorVencer": 1,
  "documentosVencidos": 1
}
```

</details>

## Plazos de cargue

Reapertura del expediente caducado (empresa) y ejecución inmediata del job (Plataforma).

### `POST /api/v1/expedientes/{id}/reabrir`

**Reabrir expediente.** Reabre un expediente caducado (P-4-2, LT-3): vuelve al estado en que caducó con el plazo del flujo completo, el vínculo vuelve a `EnValidacion` y el tercero recibe un correo con un enlace de acceso nuevo (reinvitación). Lo que el tercero subió mientras estaba caducado se adjunta enseguida. 409 `estado-invalido` si no está caducado.

**Permiso expedientes:administrar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `reabrirExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ReabrirExpedienteDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `justificacion` | texto o null | no |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/reabrir" \
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
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/plataforma/plazos-de-cargue/revisar`

**Revisar plazos de cargue.** Corre ya el job de plazos (Plataforma) y espera su resultado: de un tenant o de todos. `fecha` simula «ahora» y solo se admite en Development (pruebas de punta a punta; 422 `fecha-simulada-no-permitida` en otro entorno).

**Operador de Plataforma · API (`/api/v1`) · SDK: `revisarPlazosDeCargue`**

**Cuerpo de la petición** (`application/json`, RevisarPlazosDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tenantId` | uuid o null | no |  |
| `fecha` | fecha y hora o null | no |  |

**Respuestas**

- `200` OK — ResultadoRevisionPlazosDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `revisados` | entero | sí |  |
| `recordatorios` | entero | sí |  |
| `caducados` | entero | sí |  |

**Errores de negocio que lanza**

- `422` `fecha-simulada-no-permitida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/plazos-de-cargue/revisar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","fecha":"2026-11-12T15:00:00Z"}'
```

Respuesta `200`:

```json
{
  "revisados": 1,
  "recordatorios": 1,
  "caducados": 1
}
```

</details>

## Ratificación

Ratificación del tercero en una renovación (RN-2).

### `POST /public/v1/expedientes/{id}/requisitos/{requisitoId}/ratificar`

**Ratificar requisito.** El tercero declara que el documento vencido no cambió (condición única I-14): el tipo canónico lo permite, la versión es la actual y no trae una fecha de vencimiento ya cumplida. La bóveda registra la ratificación y recalcula la vigencia desde hoy; el requisito de la renovación queda cubierto y sus áreas lo aprueban sin revisarlo (A-7). Si era lo único, la renovación se aprueba, el vínculo vuelve a `Activo` y el certificado se reemite. En una sección de la ficha (renovación periódica del vínculo, VV-1) ratificar es confirmar que su información sigue igual: no cambia ningún dato de la ficha, queda como `RequisitoRatificado` en el expediente y la sección vigente completa cubre el requisito.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `ratificarRequisito`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `requisitoId` | path | uuid | sí |  |

**Respuestas**

- `200` OK — ExpedientePublicoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedientePublicoDto` que se detallan en `GET /public/v1/expedientes/{id}`.

**Errores de negocio que lanza**

- `404` `expediente-no-encontrado`
- `404` `requisito-no-encontrado`
- `409` `documento-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/requisitos/3fa85f64-5717-4562-b3fc-2c963f66afa6/ratificar" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresa": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S.",
    "logoUrl": "https://ejemplo.co/recurso",
    "colorMarca": "texto",
    "contactoSoporte": "texto"
  },
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "estado": "Abierto",
  "observaciones": [
    {
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
  ],
  "puedeReenviar": true,
  "reenvios": 1,
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "observaciones": [
        {
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "observacion": "Texto de ejemplo.",
          "fecha": "2026-11-12T15:00:00Z",
          "requisito": "texto",
          "campos": [
            "texto"
          ]
        }
      ],
      "puedeRatificar": false,
      "ratificadoEn": "2026-11-12T15:00:00Z",
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      }
    }
  ],
  "requisitosFaltantes": 0,
  "tipo": "Inicial",
  "plazoCargueHasta": "2026-11-12T15:00:00Z"
}
```

</details>

## Recordatorios automaticos

Ejecución inmediata del barrido automático (Plataforma), con fecha simulada solo en Development.

### `POST /api/v1/plataforma/recordatorios-observaciones/revisar`

**Revisar recordatorios de observaciones.** Corre ya el barrido (Plataforma) y espera su resultado: de un tenant o de todos. `fecha` simula «ahora» y solo se admite en Development (pruebas de punta a punta; 422 `fecha-simulada-no-permitida` en otro entorno).

**Operador de Plataforma · API (`/api/v1`) · SDK: `revisarRecordatoriosDeObservaciones`**

**Cuerpo de la petición** (`application/json`, RevisarRecordatoriosDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tenantId` | uuid o null | no |  |
| `fecha` | fecha y hora o null | no |  |

**Respuestas**

- `200` OK — ResultadoRevisionRecordatoriosDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `revisados` | entero | sí |  |
| `enviados` | entero | sí |  |

**Errores de negocio que lanza**

- `422` `fecha-simulada-no-permitida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/recordatorios-observaciones/revisar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","fecha":"2026-11-12T15:00:00Z"}'
```

Respuesta `200`:

```json
{
  "revisados": 1,
  "enviados": 1
}
```

</details>

## Recordatorios de observaciones

### `POST /api/v1/expedientes/{id}/recordar-observaciones`

**Recordar observaciones.** Recuerda al tercero las observaciones pendientes de un expediente: correo con las observaciones vigentes y un enlace de acceso nuevo, y aviso en su campana. 409 `expediente-no-observado` si no está devuelto al tercero y 409 `recordatorio-ya-enviado-hoy` si ya se le recordó hoy. Responde el expediente con su último recordatorio.

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `recordarObservaciones`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `GET /api/v1/expedientes/{id}`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/recordar-observaciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
  "tipo": "Inicial",
  "estado": "Abierto",
  "flujo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "version": 1,
    "origen": "Explicito"
  },
  "politicaDevolucion": "Inmediata",
  "separacionDeFunciones": true,
  "etapaActual": 1,
  "etapas": [
    {
      "orden": 1,
      "estado": "Esperando",
      "activadaEn": "2026-11-12T15:00:00Z",
      "aprobadaEn": "2026-11-12T15:00:00Z",
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "aprobacionesFaltantes": 1,
          "miembrosPendientes": [
            "3fa85f64-5717-4562-b3fc-2c963f66afa6"
          ],
          "puedeRechazarExpediente": true,
          "automatica": true,
          "pendienteDesde": "2026-11-12T15:00:00Z",
          "slaVence": "2026-11-12T15:00:00Z",
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ],
          "requisitosPorDecidir": 0,
          "documentosPorDecidir": [
            {
              "requisitoId": null,
              "versionVigente": null,
              "tipoRequisito": null,
              "seccion": null,
              "periodo": null
            }
          ]
        }
      ]
    }
  ],
  "reenvios": 1,
  "ultimaRespuesta": "texto",
  "rechazo": {
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "usuario": "texto",
    "motivos": [
      "Texto de ejemplo."
    ],
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z",
    "origen": "texto"
  },
  "motivoAnulacion": "Texto de ejemplo.",
  "abiertoEn": "2026-11-12T15:00:00Z",
  "cerradoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "version": 1,
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "reenvios": 1,
      "exencion": {
        "por": "texto",
        "justificacion": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "estado": "Esperando",
          "aprobadoresRequeridos": 1,
          "aprobaciones": 1,
          "veredictos": [
            {
              "id": null,
              "veredicto": null,
              "origen": null,
              "usuario": null,
              "fecha": null,
              "motivos": null,
              "observacion": null,
              "justificacion": null,
              "vigente": null,
              "superado": null,
              "anulado": null,
              "anuladoPor": null,
              "sobreVersion": null,
              "invalidado": null,
              "derivado": null,
              "campos": null,
              "revertido": null
            }
          ]
        }
      ],
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      },
      "soporteDeSecciones": {
        "sinAnalisis": true,
        "discrepanciaCritica": true,
        "secciones": [
          {
            "seccion": "texto",
            "nombre": "Constructora Norte S.A.S.",
            "periodo": 1,
            "version": 1,
            "cruce": {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          }
        ]
      },
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      },
      "revisionesAutomaticas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "areaCodigo": "codigo-de-ejemplo",
          "sobreVersion": 1,
          "nivel": "texto",
          "resultado": "texto",
          "sugerencia": "texto",
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "confianza": 1,
          "fecha": "2026-11-12T15:00:00Z",
          "pendienteDeAtencion": true,
          "resolucion": "texto",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "justificacionResolucion": "Texto de ejemplo."
        }
      ]
    }
  ],
  "verificaciones": [
    {
      "tipo": "ListasRestrictivas",
      "estado": "Pendiente",
      "bloqueante": true,
      "requeridaPorElFlujo": true,
      "proveedor": "texto",
      "resultado": "texto",
      "referenciaExterna": "REF-0001",
      "registradaEn": "2026-11-12T15:00:00Z",
      "registradaPor": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z",
      "evidencia": {
        "nombreArchivo": "Nombre de ejemplo",
        "tipoContenido": "Texto de ejemplo.",
        "tamano": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "delProveedor": true,
        "sha256Respuesta": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855"
      },
      "personas": [
        {
          "rol": "texto",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": "900123456"
        }
      ],
      "coincidencias": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "persona": "texto",
          "rol": "texto",
          "identificacion": "900123456",
          "lista": "texto",
          "nombreEncontrado": "Nombre de ejemplo",
          "detalle": "Texto de ejemplo.",
          "puntaje": 1,
          "decision": "texto",
          "justificacion": "Texto de ejemplo.",
          "resueltaPor": "texto",
          "resueltaEn": "2026-11-12T15:00:00Z",
          "vigenteHasta": "2026-11-12T15:00:00Z",
          "heredada": true
        }
      ],
      "estudio": {
        "verificadoEnListas": true,
        "conflictoDeInteres": true,
        "debidaDiligencia": "Simple",
        "nivelRiesgo": 1,
        "observaciones": "Texto de ejemplo.",
        "evidencias": [
          {
            "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "version": 1,
            "nombre": "Constructora Norte S.A.S."
          }
        ],
        "concepto": "Favorable",
        "oficial": "texto",
        "fecha": "2026-11-12T15:00:00Z",
        "vigenteHasta": "2026-11-12T15:00:00Z",
        "matriz": {
          "puntaje": 1,
          "nivel": 1,
          "nivelPorPuntaje": 1,
          "forzadoPor": "texto",
          "debidaDiligencia": "Simple",
          "mesesRevision": 1,
          "versionMatriz": 1,
          "desglose": [
            {
              "factor": null,
              "factorNombre": null,
              "origen": null,
              "peso": null,
              "opcion": null,
              "opcionNombre": null,
              "puntaje": null,
              "nivelMinimo": null
            }
          ]
        },
        "justificacionDeNivel": "Texto de ejemplo.",
        "adjuntos": [
          {
            "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
            "nombre": "Constructora Norte S.A.S.",
            "tipoContenido": "Texto de ejemplo.",
            "tamano": 1,
            "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
            "proposito": "Estudio",
            "subidoPor": "texto",
            "fecha": "2026-11-12T15:00:00Z"
          }
        ]
      },
      "anteriores": [
        {
          "proveedor": "texto",
          "referenciaExterna": "REF-0001",
          "resultado": "texto",
          "fecha": "2026-11-12T15:00:00Z",
          "coincidencias": 1,
          "conEvidencia": true
        }
      ],
      "ultimoError": "texto",
      "reintentarDesde": "2026-11-12T15:00:00Z",
      "vigenciaDias": 1,
      "frecuenciaReconsultaMeses": 1
    }
  ],
  "esperandoVerificaciones": false,
  "plazoCargue": {
    "dias": 1,
    "recordatorios": [
      1
    ],
    "hasta": "2026-11-12T15:00:00Z",
    "recordatoriosEnviados": [
      1
    ],
    "caducadoEn": "2026-11-12T15:00:00Z",
    "estadoAlReabrir": "texto",
    "reaperturas": 1
  },
  "desaprobaciones": 0,
  "ultimaDesaprobacion": {
    "etapa": 1,
    "alcance": "texto",
    "usuario": "texto",
    "justificacion": "Texto de ejemplo.",
    "fecha": "2026-11-12T15:00:00Z"
  },
  "recordatorioObservaciones": {
    "ultimoEn": "2026-11-12T15:00:00Z",
    "total": 1
  },
  "adjuntosDeCumplimiento": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "tipoContenido": "Texto de ejemplo.",
      "tamano": 1,
      "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
      "proposito": "Estudio",
      "subidoPor": "texto",
      "fecha": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/recordar-observaciones`

**Recordar observaciones en lote.** Recordatorio en lote: los `expedientes` elegidos (hasta 200) o, con `todos`, los de la empresa devueltos al tercero (hasta 500, los observados hace más tiempo primero), opcionalmente solo aquellos por los que pasa el `area` (los que ve en su bandeja, como el filtro de la pantalla). Un correo y un aviso por expediente; lo que no se puede recordar se omite y se informa con su motivo.

**Permiso vinculos:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `recordarObservacionesEnLote`**

**Cuerpo de la petición** (`application/json`, RecordarObservacionesDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `expedientes` | lista de uuid o null | no |  |
| `todos` | booleano | no | por omisión false |
| `area` | uuid o null | no |  |

**Respuestas**

- `200` OK — RecordatoriosEnviadosDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `enviados` | entero | sí |  |
| `omitidos` | entero | sí |  |
| `detalle` | lista de ResultadoRecordatorioDto | sí |  |
| `detalle[].expedienteId` | uuid | sí |  |
| `detalle[].folio` | texto o null | no |  |
| `detalle[].resultado` | texto | sí |  |

**Errores de negocio que lanza**

- `422` `lote-demasiado-grande`
- `422` `lote-vacio`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/recordar-observaciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"expedientes":["3fa85f64-5717-4562-b3fc-2c963f66afa6"],"todos":false,"area":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}'
```

Respuesta `200`:

```json
{
  "enviados": 1,
  "omitidos": 1,
  "detalle": [
    {
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "resultado": "texto"
    }
  ]
}
```

</details>

## Reintento público

El tercero rechazado vuelve a postularse (DS-4) desde su portal.

### `POST /public/v1/vinculos/{id}/reintentar`

**Reintentar postulacion.** «Volver a postularme»: abre un expediente inicial nuevo con el flujo vigente del vínculo, con los documentos que el tercero ya tiene compartidos. Solo con un rechazo vigente (409 `reintento-no-disponible`), si la empresa lo permite (403 `reintento-no-permitido`), cumplida la espera (409 `reintento-en-espera` con la fecha en `desde`) y con el vínculo sin bloquear (409 `vinculo-bloqueado`). Responde el expediente nuevo.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `reintentarPostulacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — ExpedientePublicoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedientePublicoDto` que se detallan en `GET /public/v1/expedientes/{id}`.

**Errores de negocio que lanza**

- `403` `reintento-no-permitido`
- `404` `vinculo-no-encontrado`
- `409` `reintento-no-disponible`
- `409` `vinculo-bloqueado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/reintentar" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "empresa": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S.",
    "logoUrl": "https://ejemplo.co/recurso",
    "colorMarca": "texto",
    "contactoSoporte": "texto"
  },
  "tipoVinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S."
  },
  "estado": "Abierto",
  "observaciones": [
    {
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
  ],
  "puedeReenviar": true,
  "reenvios": 1,
  "actualizadoEn": "2026-11-12T15:00:00Z",
  "requisitos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipoDocumento": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "tipoCanonico": "texto",
        "ambito": "Global",
        "modo": "texto",
        "campos": [
          {
            "clave": "texto",
            "etiqueta": "texto",
            "tipo": "texto",
            "obligatorio": false,
            "opciones": [
              null
            ],
            "ayuda": "texto"
          }
        ],
        "archivo": {
          "formatos": [
            "texto"
          ],
          "tamanoMaximoBytes": 1,
          "paginasMaximas": 1,
          "descripcion": "Texto de ejemplo."
        },
        "tienePlantilla": false,
        "textoAyuda": "Texto de ejemplo."
      },
      "obligatorio": true,
      "estado": "Pendiente",
      "versionActual": {
        "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "n": 1,
        "sha256": "e3b0c44298fc1c149afbf4c8996fb92427ae41e4649b934ca495991b7852b855",
        "cargadaEn": "2026-11-12T15:00:00Z",
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
        },
        "estado": "texto",
        "clase": "texto"
      },
      "observaciones": [
        {
          "motivos": [
            {
              "codigo": null,
              "texto": null
            }
          ],
          "observacion": "Texto de ejemplo.",
          "fecha": "2026-11-12T15:00:00Z",
          "requisito": "texto",
          "campos": [
            "texto"
          ]
        }
      ],
      "puedeRatificar": false,
      "ratificadoEn": "2026-11-12T15:00:00Z",
      "tipoRequisito": "Documento",
      "seccion": {
        "seccion": "texto",
        "nombre": "Constructora Norte S.A.S.",
        "periodo": 1,
        "exigeSoporte": true,
        "completitud": {
          "completa": true,
          "faltantes": [
            {
              "campo": null,
              "regla": null,
              "mensaje": null
            }
          ],
          "reglaVersion": 1
        },
        "versionEvaluada": 1,
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
        "versionAceptada": 1,
        "cambios": [
          {
            "campo": "texto",
            "etiqueta": "texto",
            "anterior": "texto",
            "nuevo": "texto",
            "tipoCambio": "Agregado"
          }
        ],
        "cruce": {
          "estado": "texto",
          "discrepanciaCritica": true,
          "cruces": [
            {
              "documentoId": null,
              "versionDocumento": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "confianza": null,
              "legible": null,
              "fecha": null,
              "coincidencias": null,
              "discrepancias": null,
              "noVerificables": null,
              "discrepanciaCritica": null,
              "campos": null
            }
          ],
          "sinCruce": [
            {
              "documentoId": null,
              "version": null,
              "tipoDocumento": null,
              "nombreDocumento": null,
              "motivo": null
            }
          ]
        },
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
        }
      }
    }
  ],
  "requisitosFaltantes": 0,
  "tipo": "Inicial",
  "plazoCargueHasta": "2026-11-12T15:00:00Z"
}
```

</details>

## Revalidaciones

Bandeja de revalidaciones de la empresa activa (RV-5, docs/06 `/revalidaciones`): los documentos de los expedientes de actualización abiertos que la empresa aún no aprueba, con la versión que aceptó antes, la actual, la comparación de la bóveda y el plazo. Se calcula al consultar (no es proyección): son pocos y cruzan expediente, certificado y bóveda. Un usuario delegado ve solo los documentos que revisa alguna de sus áreas.

### `GET /api/v1/revalidaciones`

**Listar revalidaciones.**

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarRevalidaciones`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `area` | query | uuid | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfRevalidacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de RevalidacionDto | sí |  |
| `datos[].expedienteId` | uuid | sí |  |
| `datos[].folio` | texto | sí |  |
| `datos[].vinculoId` | uuid | sí |  |
| `datos[].requisitoId` | uuid | sí |  |
| `datos[].tercero` | TerceroResumenDto | sí |  |
| `datos[].tercero.id` | uuid | sí |  |
| `datos[].tercero.nombre` | texto | sí |  |
| `datos[].tercero.identificacion` | texto | sí |  |
| `datos[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `datos[].tipoVinculo.id` | uuid | sí |  |
| `datos[].tipoVinculo.codigo` | texto | sí |  |
| `datos[].tipoVinculo.nombre` | texto | sí |  |
| `datos[].tipoDocumentoCodigo` | texto | sí |  |
| `datos[].tipoDocumento` | texto | sí |  |
| `datos[].versionAceptada` | VersionResumenDto o null | no |  |
| `datos[].versionAceptada.numero` | entero | sí |  |
| `datos[].versionAceptada.fecha` | fecha y hora | sí |  |
| `datos[].versionActual` | VersionResumenDto | sí | mismos campos que `datos[].versionAceptada` |
| `datos[].comparacion` | texto | sí |  |
| `datos[].aprobableEnLote` | booleano | sí |  |
| `datos[].estadoRequisito` | texto | sí |  |
| `datos[].areas` | lista de uuid | sí |  |
| `datos[].desde` | fecha y hora | sí |  |
| `datos[].plazoHasta` | fecha y hora | sí |  |
| `datos[].diasRestantes` | entero | sí |  |
| `datos[].bloqueante` | booleano | sí |  |
| `datos[].retiro` | RetiroDeVersionDto o null | no |  |
| `datos[].retiro.numero` | entero | sí |  |
| `datos[].retiro.motivo` | texto | sí |  |
| `datos[].retiro.fecha` | fecha y hora | sí |  |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/revalidaciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "requisitoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
      "tipoDocumentoCodigo": "codigo-de-ejemplo",
      "tipoDocumento": "texto",
      "versionAceptada": {
        "numero": 1,
        "fecha": "2026-11-12T15:00:00Z"
      },
      "versionActual": {
        "numero": 1,
        "fecha": "2026-11-12T15:00:00Z"
      },
      "comparacion": "texto",
      "aprobableEnLote": true,
      "estadoRequisito": "texto",
      "areas": [
        "3fa85f64-5717-4562-b3fc-2c963f66afa6"
      ],
      "desde": "2026-11-12T15:00:00Z",
      "plazoHasta": "2026-11-12T15:00:00Z",
      "diasRestantes": 1,
      "bloqueante": true,
      "retiro": {
        "numero": 1,
        "motivo": "Texto de ejemplo.",
        "fecha": "2026-11-12T15:00:00Z"
      }
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `POST /api/v1/revalidaciones/aprobar`

**Aprobar revalidaciones.** Aprobación en lote (A-18): cada revalidación se aprueba con un veredicto propio del revisor en cada área suya que la revisa, como si la aprobara una a una. Solo las que la bóveda ve sin cambios (`Identica` o `SinCambios`); las demás vuelven en `fallidas` con su código y no frenan al resto.

**Permiso expedientes:revisar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `aprobarRevalidaciones`**

**Cuerpo de la petición** (`application/json`, AprobarRevalidacionesDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `revalidaciones` | lista de RevalidacionAAprobarDto | sí |  |
| `revalidaciones[].expedienteId` | uuid | sí |  |
| `revalidaciones[].requisitoId` | uuid | sí |  |
| `revalidaciones[].sobreVersion` | entero | sí |  |

**Respuestas**

- `200` OK — ResultadoDeLoteDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `aprobadas` | entero | sí |  |
| `fallidas` | lista de FalloDeLoteDto | sí |  |
| `fallidas[].expedienteId` | uuid | sí |  |
| `fallidas[].requisitoId` | uuid | sí |  |
| `fallidas[].codigo` | texto | sí |  |
| `fallidas[].detalle` | texto | sí |  |

**Errores de negocio que lanza**

- `422` `lote-demasiado-grande`
- `422` `lote-vacio`
- `422` `usuario-delegado-requerido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/revalidaciones/aprobar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"revalidaciones":[{"expedienteId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","requisitoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","sobreVersion":1}]}'
```

Respuesta `200`:

```json
{
  "aprobadas": 1,
  "fallidas": [
    {
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "requisitoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "codigo": "CODIGO_EJEMPLO",
      "detalle": "Texto de ejemplo."
    }
  ]
}
```

</details>

## Slas de revisión

### `POST /api/v1/plataforma/sla/revisar`

**Revisar SLA de revisión.** Corre ya el barrido (Plataforma) y espera su resultado: de un tenant o de todos. `fecha` simula «ahora» y solo se admite en Development (pruebas de punta a punta; 422 `fecha-simulada-no-permitida` en otro entorno).

**Operador de Plataforma · API (`/api/v1`) · SDK: `revisarSlaDeRevision`**

**Cuerpo de la petición** (`application/json`, RevisarSlaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tenantId` | uuid o null | no |  |
| `fecha` | fecha y hora o null | no |  |

**Respuestas**

- `200` OK — ResultadoRevisionSlaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `revisados` | entero | sí |  |
| `avisos` | entero | sí |  |
| `incumplidos` | entero | sí |  |

**Errores de negocio que lanza**

- `422` `fecha-simulada-no-permitida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/sla/revisar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","fecha":"2026-11-12T15:00:00Z"}'
```

Respuesta `200`:

```json
{
  "revisados": 1,
  "avisos": 1,
  "incumplidos": 1
}
```

</details>
