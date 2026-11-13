# API · Informes

9 operaciones en 4 recursos.

## Exportación de expediente

Expediente completo en ZIP (IF-5, docs/11 §H8 `ExpedienteExportado`): los archivos de los documentos aprobados (la versión aceptada de cada requisito), `resumen.pdf` (datos, requisitos, áreas con tiempos, decisiones y línea de tiempo, con el estilo de los PDF del proyecto), `linea-de-tiempo.json` (el historial completo) e `indice.json` con el SHA-256 de cada archivo, y `declaraciones-pep/` con un PDF por cada persona PEP con declaración (HU 49093). Cada documento incluido queda como `DocumentoAccedido` (motivo «Exportación del expediente …») y la descarga como `ExportacionRegistrada` con la huella del ZIP.

### `GET /api/v1/expedientes/{id}/exportar`

**Exportar expediente.**

**Permiso expedientes:exportar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `exportarExpediente`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/exportar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>

## Informe estado expedientes

Informe de estado de expedientes (IF-1, IF-2; docs/11 §H8 `/informes/estado-expedientes`): cada expediente de la empresa con su estado, avance de requisitos y tiempos medidos sobre sus eventos (en carga, con el tercero, en revisión y por área con SLA), y los totales. Se calcula al consultar (aún no es la proyección `InformeEstadoExpedientes`): hasta `MaximoExpedientes` por consulta. Se descarga en Excel y en PDF con los mismos filtros.

### `GET /api/v1/informes/estado-expedientes`

**Obtener informe estado expedientes.**

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerInformeEstadoExpedientes`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `desde` | query | fecha | no |  |
| `hasta` | query | fecha | no |  |
| `fecha` | query | texto | no |  |
| `estado` | query | texto | no |  |
| `tipo` | query | texto | no |  |
| `areaId` | query | uuid | no |  |
| `flujoId` | query | uuid | no |  |
| `tipoVinculoId` | query | uuid | no |  |
| `terceroId` | query | uuid | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — InformeEstadoExpedientesDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `resumen` | ResumenEstadoExpedientesDto | sí |  |
| `resumen.total` | entero | sí |  |
| `resumen.porEstado` | lista de ConteoDto | sí |  |
| `resumen.porEstado[].clave` | texto | sí |  |
| `resumen.porEstado[].cantidad` | entero | sí |  |
| `resumen.porTipo` | lista de ConteoDto | sí | mismos campos que `resumen.porEstado[]` |
| `resumen.aprobados` | entero | sí |  |
| `resumen.tiempoPromedioDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `resumen.tiempoMedianaDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `resumen.promedioEnCargaDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `resumen.promedioConTerceroDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `resumen.promedioEnRevisionDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `resumen.devoluciones` | entero | sí |  |
| `resumen.conSlaIncumplido` | entero | sí |  |
| `resumen.porArea` | lista de ResumenDeAreaDto | sí |  |
| `resumen.porArea[].areaId` | uuid | sí |  |
| `resumen.porArea[].codigo` | texto | sí |  |
| `resumen.porArea[].nombre` | texto | sí |  |
| `resumen.porArea[].expedientes` | entero | sí |  |
| `resumen.porArea[].pendientes` | entero | sí |  |
| `resumen.porArea[].promedioDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `resumen.porArea[].medianaDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `resumen.porArea[].maximoDias` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `resumen.porArea[].slaIncumplidos` | entero | sí |  |
| `datos` | lista de FilaEstadoExpedienteDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].folio` | texto | sí |  |
| `datos[].tipo` | TipoExpediente (texto) | sí | valores: Inicial · Renovacion · Actualizacion |
| `datos[].estado` | EstadoExpediente (texto) | sí | valores: Abierto · EnRevision · Observado · Aprobado · Rechazado · Anulado · Caducado |
| `datos[].vinculoId` | uuid | sí |  |
| `datos[].tercero` | TerceroResumenDto | sí |  |
| `datos[].tercero.id` | uuid | sí |  |
| `datos[].tercero.nombre` | texto | sí |  |
| `datos[].tercero.identificacion` | texto | sí |  |
| `datos[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `datos[].tipoVinculo.id` | uuid | sí |  |
| `datos[].tipoVinculo.codigo` | texto | sí |  |
| `datos[].tipoVinculo.nombre` | texto | sí |  |
| `datos[].flujo` | FlujoDeExpedienteDto o null | no |  |
| `datos[].flujo.id` | uuid | sí |  |
| `datos[].flujo.versionId` | uuid | sí |  |
| `datos[].flujo.codigo` | texto | sí |  |
| `datos[].flujo.nombre` | texto | sí |  |
| `datos[].flujo.version` | entero | sí |  |
| `datos[].flujo.origen` | OrigenFlujo (texto) | sí | valores: Explicito · Regla · PorDefecto |
| `datos[].abiertoEn` | fecha y hora | sí |  |
| `datos[].cerradoEn` | fecha y hora o null | no |  |
| `datos[].requisitos` | entero | sí |  |
| `datos[].requisitosAprobados` | entero | sí |  |
| `datos[].devoluciones` | entero | sí |  |
| `datos[].diasTotales` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `datos[].diasEnCarga` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `datos[].diasConTercero` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `datos[].diasEnRevision` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `datos[].diasHastaAprobacion` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `datos[].slaIncumplido` | booleano | sí |  |
| `datos[].areas` | lista de TiempoDeAreaDto | sí |  |
| `datos[].areas[].areaId` | uuid | sí |  |
| `datos[].areas[].codigo` | texto | sí |  |
| `datos[].areas[].nombre` | texto | sí |  |
| `datos[].areas[].etapa` | entero | sí |  |
| `datos[].areas[].estado` | EstadoAreaExpediente (texto) | sí | valores: Esperando · Pendiente · Aprobada · Observada |
| `datos[].areas[].dias` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?(?:[eE][+-]?\d+)?$ |
| `datos[].areas[].rondas` | entero | sí |  |
| `datos[].areas[].slaHoras` | entero o null | no |  |
| `datos[].areas[].slaIncumplido` | booleano | sí |  |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/estado-expedientes" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "resumen": {
    "total": 1,
    "porEstado": [
      {
        "clave": "texto",
        "cantidad": 1
      }
    ],
    "porTipo": [
      {
        "clave": "texto",
        "cantidad": 1
      }
    ],
    "aprobados": 1,
    "tiempoPromedioDias": 1,
    "tiempoMedianaDias": 1,
    "promedioEnCargaDias": 1,
    "promedioConTerceroDias": 1,
    "promedioEnRevisionDias": 1,
    "devoluciones": 1,
    "conSlaIncumplido": 1,
    "porArea": [
      {
        "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "expedientes": 1,
        "pendientes": 1,
        "promedioDias": 1,
        "medianaDias": 1,
        "maximoDias": 1,
        "slaIncumplidos": 1
      }
    ]
  },
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "tipo": "Inicial",
      "estado": "Abierto",
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
      "flujo": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "versionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "version": 1,
        "origen": "Explicito"
      },
      "abiertoEn": "2026-11-12T15:00:00Z",
      "cerradoEn": "2026-11-12T15:00:00Z",
      "requisitos": 1,
      "requisitosAprobados": 1,
      "devoluciones": 1,
      "diasTotales": 1,
      "diasEnCarga": 1,
      "diasConTercero": 1,
      "diasEnRevision": 1,
      "diasHastaAprobacion": 1,
      "slaIncumplido": true,
      "areas": [
        {
          "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "etapa": 1,
          "estado": "Esperando",
          "dias": 1,
          "rondas": 1,
          "slaHoras": 1,
          "slaIncumplido": true
        }
      ]
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/informes/estado-expedientes/excel`

**Exportar informe estado expedientes.** Libro con tres hojas: Expedientes, Tiempos por área y Resumen.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `exportarInformeEstadoExpedientes`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `desde` | query | fecha | no |  |
| `hasta` | query | fecha | no |  |
| `fecha` | query | texto | no |  |
| `estado` | query | texto | no |  |
| `tipo` | query | texto | no |  |
| `areaId` | query | uuid | no |  |
| `flujoId` | query | uuid | no |  |
| `tipoVinculoId` | query | uuid | no |  |
| `terceroId` | query | uuid | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/estado-expedientes/excel" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>

### `GET /api/v1/informes/estado-expedientes/pdf`

**Exportar informe estado expedientes pdf.** El informe en PDF horizontal, con el estilo de los PDF del proyecto: resumen, tiempos por área y expedientes.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `exportarInformeEstadoExpedientesPdf`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `desde` | query | fecha | no |  |
| `hasta` | query | fecha | no |  |
| `fecha` | query | texto | no |  |
| `estado` | query | texto | no |  |
| `tipo` | query | texto | no |  |
| `areaId` | query | uuid | no |  |
| `flujoId` | query | uuid | no |  |
| `tipoVinculoId` | query | uuid | no |  |
| `terceroId` | query | uuid | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/estado-expedientes/pdf" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>

## Informe sagrilaft

Informe SAGRILAFT completo (CU-14 del informe; docs/01 §10): junta lo que el SRM reparte en dos informes —el tablero del oficial (totales, tendencia de 12 meses, distribución del riesgo, vencidos y por vencer) y el detalle por tercero (información general, oficial, debida diligencia, personas relacionadas con PEP y beneficiario final)— y agrega lo que no tenía: tiempos, la periodicidad que fija el nivel de riesgo y la evidencia de cada verificación. Es el estado **actual** de cada tercero de la empresa (su última consulta de listas y su último estudio); los que no tienen estudio salen como `SinEstudio`. Se calcula al consultar y es privado de la empresa. Excel con tres hojas: Terceros, Personas y Verificaciones.

### `GET /api/v1/informes/sagrilaft`

**Obtener informe sagrilaft.** El informe al día `hasta` (por omisión hoy; la tendencia son los 12 meses que terminan ese día).

**Permiso auditoria:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerInformeSagrilaft`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `hasta` | query | fecha | no |  |

**Respuestas**

- `200` OK — InformeSagrilaftDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desde` | fecha | sí |  |
| `hasta` | fecha | sí |  |
| `terceros` | entero | sí |  |
| `conEstudioVigente` | entero | sí |  |
| `estudiosPorVencer` | entero | sí |  |
| `estudiosVencidos` | entero | sí |  |
| `listasPorVencer` | entero | sí |  |
| `listasVencidas` | entero | sí |  |
| `sinEstudio` | entero | sí |  |
| `riesgo` | lista de RiesgoDto | sí |  |
| `riesgo[].nivel` | entero | sí |  |
| `riesgo[].cantidad` | entero | sí |  |
| `tendencia` | lista de MesDeCumplimientoDto | sí |  |
| `tendencia[].mes` | texto | sí |  |
| `tendencia[].estudios` | entero | sí |  |
| `tendencia[].consultas` | entero | sí |  |
| `tiempos` | TiemposDeCumplimientoDto | sí |  |
| `tiempos.promedioDiasEnResolver` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `tiempos.resueltas` | entero | sí |  |
| `tiempos.abiertas` | entero | sí |  |
| `tiempos.abiertasMasDe7Dias` | entero | sí |  |
| `filas` | lista de FilaTerceroSagrilafDto | sí |  |
| `filas[].vinculoId` | uuid | sí |  |
| `filas[].terceroId` | uuid | sí |  |
| `filas[].folio` | texto | sí |  |
| `filas[].tercero` | texto | sí |  |
| `filas[].tipoIdentificacion` | texto | sí |  |
| `filas[].identificacion` | texto | sí |  |
| `filas[].tipoPersona` | texto | sí |  |
| `filas[].tipoVinculo` | texto | sí |  |
| `filas[].pais` | texto o null | no |  |
| `filas[].ciudad` | texto o null | no |  |
| `filas[].correo` | texto o null | no |  |
| `filas[].actividad` | texto o null | no |  |
| `filas[].estadoVinculo` | texto | sí |  |
| `filas[].ultimaConsultaListas` | fecha y hora o null | no |  |
| `filas[].proveedorListas` | texto o null | no |  |
| `filas[].estadoListas` | texto | sí |  |
| `filas[].coincidenciasPorAnalizar` | entero | sí |  |
| `filas[].fechaEstudio` | fecha y hora o null | no |  |
| `filas[].oficial` | texto o null | no |  |
| `filas[].nivelRiesgo` | entero o null | no |  |
| `filas[].debidaDiligencia` | texto o null | no |  |
| `filas[].concepto` | texto o null | no |  |
| `filas[].conflictoDeInteres` | booleano o null | no |  |
| `filas[].proximaRevision` | fecha y hora o null | no |  |
| `filas[].diasParaRevision` | entero o null | no |  |
| `filas[].estadoDebidaDiligencia` | texto | sí |  |
| `filas[].personas` | entero | sí |  |
| `filas[].pep` | entero | sí |  |
| `filas[].pepSinDeclaracion` | entero | no | por omisión 0 |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/sagrilaft" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "desde": "2026-11-12",
  "hasta": "2026-11-12",
  "terceros": 1,
  "conEstudioVigente": 1,
  "estudiosPorVencer": 1,
  "estudiosVencidos": 1,
  "listasPorVencer": 1,
  "listasVencidas": 1,
  "sinEstudio": 1,
  "riesgo": [
    {
      "nivel": 1,
      "cantidad": 1
    }
  ],
  "tendencia": [
    {
      "mes": "texto",
      "estudios": 1,
      "consultas": 1
    }
  ],
  "tiempos": {
    "promedioDiasEnResolver": 1,
    "resueltas": 1,
    "abiertas": 1,
    "abiertasMasDe7Dias": 1
  },
  "filas": [
    {
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "tercero": "texto",
      "tipoIdentificacion": "900123456",
      "identificacion": "900123456",
      "tipoPersona": "texto",
      "tipoVinculo": "texto",
      "pais": "CO",
      "ciudad": "Bogotá",
      "correo": "contacto@empresa.co",
      "actividad": "texto",
      "estadoVinculo": "texto",
      "ultimaConsultaListas": "2026-11-12T15:00:00Z",
      "proveedorListas": "texto",
      "estadoListas": "texto",
      "coincidenciasPorAnalizar": 1,
      "fechaEstudio": "2026-11-12T15:00:00Z",
      "oficial": "texto",
      "nivelRiesgo": 1,
      "debidaDiligencia": "texto",
      "concepto": "texto",
      "conflictoDeInteres": true,
      "proximaRevision": "2026-11-12T15:00:00Z",
      "diasParaRevision": 1,
      "estadoDebidaDiligencia": "texto",
      "personas": 1,
      "pep": 1,
      "pepSinDeclaracion": 0
    }
  ]
}
```

</details>

### `GET /api/v1/informes/sagrilaft/excel`

**Exportar informe sagrilaft.** El mismo informe en Excel (hojas Terceros, Personas y Verificaciones).

**Permiso auditoria:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `exportarInformeSagrilaft`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `hasta` | query | fecha | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/sagrilaft/excel" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>

### `GET /api/v1/informes/sagrilaft/terceros/{terceroId}/personas`

**Obtener personas del tercero sagrilaft.** Las personas relacionadas de un tercero con su identificación, participación, PEP y si es beneficiario final (desde 5 %).

**Permiso auditoria:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerPersonasDelTerceroSagrilaft`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `terceroId` | path | uuid | sí |  |

**Respuestas**

- `200` OK — PersonasDelTerceroDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `terceroId` | uuid | sí |  |
| `tercero` | texto | sí |  |
| `personas` | lista de PersonaInformeDto | sí |  |
| `personas[].rol` | texto | sí |  |
| `personas[].tipoIdentificacion` | texto | sí |  |
| `personas[].identificacion` | texto | sí |  |
| `personas[].nombre` | texto | sí |  |
| `personas[].participacion` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `personas[].esPep` | booleano | sí |  |
| `personas[].vinculoPep` | booleano | sí |  |
| `personas[].beneficiarioFinal` | booleano | sí |  |
| `personas[].declaracionPep` | booleano | no | por omisión false |
| `personas[].indice` | entero | no | por omisión 0 |

**Errores de negocio que lanza**

- `404` `tercero-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/sagrilaft/terceros/3fa85f64-5717-4562-b3fc-2c963f66afa6/personas" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tercero": "texto",
  "personas": [
    {
      "rol": "texto",
      "tipoIdentificacion": "900123456",
      "identificacion": "900123456",
      "nombre": "Constructora Norte S.A.S.",
      "participacion": 1,
      "esPep": true,
      "vinculoPep": true,
      "beneficiarioFinal": true,
      "declaracionPep": false,
      "indice": 0
    }
  ]
}
```

</details>

## Informe vínculos

Informe de terceros y vínculos (IF-1): por vínculo de la empresa, su estado, el certificado vigente, el expediente en curso, la vigencia de los documentos compartidos (la misma de Vencimientos, TD-6) y las secciones de la ficha que el expediente en curso aún no aprueba. Se calcula al consultar; se descarga en Excel con los mismos filtros.

### `GET /api/v1/informes/vinculos`

**Obtener informe vínculos.**

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerInformeVinculos`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `estado` | query | texto | no |  |
| `tipoVinculoId` | query | uuid | no |  |
| `terceroId` | query | uuid | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — InformeVinculosDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `resumen` | ResumenVinculosDto | sí |  |
| `resumen.total` | entero | sí |  |
| `resumen.porEstado` | lista de ConteoDto | sí |  |
| `resumen.porEstado[].clave` | texto | sí |  |
| `resumen.porEstado[].cantidad` | entero | sí |  |
| `resumen.conCertificadoVigente` | entero | sí |  |
| `resumen.conDocumentosPorVencer` | entero | sí |  |
| `resumen.conDocumentosVencidos` | entero | sí |  |
| `resumen.conSeccionesPendientes` | entero | sí |  |
| `datos` | lista de FilaInformeVinculoDto | sí |  |
| `datos[].vinculoId` | uuid | sí |  |
| `datos[].tercero` | TerceroResumenDto | sí |  |
| `datos[].tercero.id` | uuid | sí |  |
| `datos[].tercero.nombre` | texto | sí |  |
| `datos[].tercero.identificacion` | texto | sí |  |
| `datos[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `datos[].tipoVinculo.id` | uuid | sí |  |
| `datos[].tipoVinculo.codigo` | texto | sí |  |
| `datos[].tipoVinculo.nombre` | texto | sí |  |
| `datos[].estado` | EstadoVinculo (texto) | sí | valores: Invitado · EnValidacion · Activo · PorVencer · Vencido · Bloqueado · Desvinculado · Anulado |
| `datos[].creadoEn` | fecha y hora | sí |  |
| `datos[].invitacionAceptada` | booleano | sí |  |
| `datos[].certificado` | CertificadoDelInformeDto o null | no |  |
| `datos[].certificado.folio` | texto | sí |  |
| `datos[].certificado.estado` | texto | sí |  |
| `datos[].certificado.bloqueante` | booleano | sí |  |
| `datos[].certificado.vigenteHasta` | fecha o null | no |  |
| `datos[].certificado.enVerificacion` | booleano | no | por omisión false |
| `datos[].expedienteEnCurso` | ExpedienteDelInformeDto o null | no |  |
| `datos[].expedienteEnCurso.id` | uuid | sí |  |
| `datos[].expedienteEnCurso.folio` | texto | sí |  |
| `datos[].expedienteEnCurso.tipo` | TipoExpediente (texto) | sí | valores: Inicial · Renovacion · Actualizacion |
| `datos[].expedienteEnCurso.estado` | EstadoExpediente (texto) | sí | valores: Abierto · EnRevision · Observado · Aprobado · Rechazado · Anulado · Caducado |
| `datos[].documentos` | DocumentosDelInformeDto o null | no |  |
| `datos[].documentos.conVencimiento` | entero | sí |  |
| `datos[].documentos.vigentes` | entero | sí |  |
| `datos[].documentos.porVencer` | entero | sí |  |
| `datos[].documentos.enGracia` | entero | sí |  |
| `datos[].documentos.vencidos` | entero | sí |  |
| `datos[].documentos.proximoVencimiento` | fecha o null | no |  |
| `datos[].seccionesPendientes` | lista de texto | sí |  |
| `datos[].enRevision` | booleano | no | por omisión false |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/vinculos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "resumen": {
    "total": 1,
    "porEstado": [
      {
        "clave": "texto",
        "cantidad": 1
      }
    ],
    "conCertificadoVigente": 1,
    "conDocumentosPorVencer": 1,
    "conDocumentosVencidos": 1,
    "conSeccionesPendientes": 1
  },
  "datos": [
    {
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
      "estado": "Invitado",
      "creadoEn": "2026-11-12T15:00:00Z",
      "invitacionAceptada": true,
      "certificado": {
        "folio": "texto",
        "estado": "texto",
        "bloqueante": true,
        "vigenteHasta": "2026-11-12",
        "enVerificacion": false
      },
      "expedienteEnCurso": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "folio": "texto",
        "tipo": "Inicial",
        "estado": "Abierto"
      },
      "documentos": {
        "conVencimiento": 1,
        "vigentes": 1,
        "porVencer": 1,
        "enGracia": 1,
        "vencidos": 1,
        "proximoVencimiento": "2026-11-12"
      },
      "seccionesPendientes": [
        "texto"
      ],
      "enRevision": false
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/informes/vinculos/excel`

**Exportar informe vínculos.**

**Permiso vinculos:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `exportarInformeVinculos`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `estado` | query | texto | no |  |
| `tipoVinculoId` | query | uuid | no |  |
| `terceroId` | query | uuid | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/vinculos/excel" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>
