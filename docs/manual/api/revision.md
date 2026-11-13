# API · Revisión

4 operaciones en 1 recurso.

## Revisión automatica

Revisión automática con IA de la empresa activa (ADR-0012): su configuración, la bandeja de excepciones y la resolución humana de las aprobaciones por muestreo. Las decisiones de una empresa son privadas.

### `GET /api/v1/configuracion/revision-automatica`

**Obtener revisión automatica.** Configuración, uso del mes, tipos analizables y áreas para elegir. Sin configuración guardada es Manual (el análisis solo informa).

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerRevisionAutomatica`**

**Respuestas**

- `200` OK — RevisionAutomaticaConfigDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `configurada` | booleano | sí |  |
| `nivel` | texto | sí |  |
| `umbralConfianza` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `porcentajeMuestreo` | entero | sí |  |
| `topeMensualAprobaciones` | entero o null | no |  |
| `tiposAutorizados` | lista de texto | sí |  |
| `areasAutorizadas` | lista de uuid | sí |  |
| `esperaIaMinutos` | entero | sí |  |
| `actualizadaEn` | fecha y hora o null | no |  |
| `iaDisponible` | booleano | sí |  |
| `umbralDelPlan` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `uso` | UsoRevisionAutomaticaDto | sí |  |
| `uso.mes` | texto | sí |  |
| `uso.aprobadas` | entero | sí |  |
| `uso.muestreadas` | entero | sí |  |
| `uso.excepciones` | entero | sí |  |
| `uso.sugerencias` | entero | sí |  |
| `uso.tope` | entero o null | no |  |
| `tiposAnalizables` | lista de TipoAnalizableDto | sí |  |
| `tiposAnalizables[].codigo` | texto | sí |  |
| `tiposAnalizables[].nombre` | texto | sí |  |
| `areas` | lista de AreaDisponibleDto | sí |  |
| `areas[].id` | uuid | sí |  |
| `areas[].codigo` | texto | sí |  |
| `areas[].nombre` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/configuracion/revision-automatica" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "configurada": true,
  "nivel": "texto",
  "umbralConfianza": 1,
  "porcentajeMuestreo": 1,
  "topeMensualAprobaciones": 1,
  "tiposAutorizados": [
    "texto"
  ],
  "areasAutorizadas": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  ],
  "esperaIaMinutos": 1,
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "iaDisponible": true,
  "umbralDelPlan": 1,
  "uso": {
    "mes": "texto",
    "aprobadas": 1,
    "muestreadas": 1,
    "excepciones": 1,
    "sugerencias": 1,
    "tope": 1
  },
  "tiposAnalizables": [
    {
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S."
    }
  ],
  "areas": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S."
    }
  ]
}
```

</details>

### `PUT /api/v1/configuracion/revision-automatica`

**Actualizar revisión automatica.** Reemplaza la configuración. 422 `nivel-invalido`, `umbral-invalido`, `muestreo-invalido`, `tope-invalido`, `espera-invalida`, `tipo-invalido` (no es un tipo analizable con IA), `area-invalida` (no es un área de la empresa), `automatico-sin-alcance` e `ia-no-disponible` (Automático exige el plan de IA activo y con llave).

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarRevisionAutomatica`**

**Cuerpo de la petición** (`application/json`, ActualizarRevisionAutomaticaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nivel` | texto | sí |  |
| `umbralConfianza` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `porcentajeMuestreo` | entero o null | no |  |
| `topeMensualAprobaciones` | entero o null | no |  |
| `tiposAutorizados` | lista de texto o null | no |  |
| `areasAutorizadas` | lista de uuid o null | no |  |
| `esperaIaMinutos` | entero o null | no |  |

**Respuestas**

- `200` OK — RevisionAutomaticaConfigDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `RevisionAutomaticaConfigDto` que se detallan en `GET /api/v1/configuracion/revision-automatica`.

**Errores de negocio que lanza**

- `422` `area-invalida`
- `422` `ia-no-disponible`
- `422` `tipo-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/configuracion/revision-automatica" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"nivel":"texto","umbralConfianza":1,"porcentajeMuestreo":1,"topeMensualAprobaciones":1,"tiposAutorizados":["texto"],"areasAutorizadas":["3fa85f64-5717-4562-b3fc-2c963f66afa6"],"esperaIaMinutos":1}'
```

Respuesta `200`:

```json
{
  "configurada": true,
  "nivel": "texto",
  "umbralConfianza": 1,
  "porcentajeMuestreo": 1,
  "topeMensualAprobaciones": 1,
  "tiposAutorizados": [
    "texto"
  ],
  "areasAutorizadas": [
    "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  ],
  "esperaIaMinutos": 1,
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "iaDisponible": true,
  "umbralDelPlan": 1,
  "uso": {
    "mes": "texto",
    "aprobadas": 1,
    "muestreadas": 1,
    "excepciones": 1,
    "sugerencias": 1,
    "tope": 1
  },
  "tiposAnalizables": [
    {
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S."
    }
  ],
  "areas": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S."
    }
  ]
}
```

</details>

### `POST /api/v1/expedientes/{id}/requisitos/{requisitoId}/revision-automatica/resolver`

**Resolver revisión automatica.** Una persona del área confirma o revierte una aprobación por muestreo. Revertir anula el veredicto del sistema y devuelve el documento a revisión humana (solo con el expediente en revisión y la etapa del área activa). 404 `revision-no-encontrada`; 409 `revision-ya-resuelta`, `veredicto-no-vigente`, `estado-invalido`, `area-no-activa`; 422 `decision-invalida`, `justificacion-requerida` (al revertir), `usuario-delegado-requerido`; 403 `no-es-miembro-del-area`, `conflicto-de-intereses`, `separacion-de-funciones`.

**Permiso expedientes:revisar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `resolverRevisionAutomatica`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `requisitoId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ResolverRevisionAutomaticaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `areaId` | uuid | sí |  |
| `decision` | texto | sí |  |
| `justificacion` | texto o null | no |  |

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

**Errores de negocio que lanza**

- `422` `decision-invalida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/requisitos/3fa85f64-5717-4562-b3fc-2c963f66afa6/revision-automatica/resolver" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"areaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","decision":"texto","justificacion":"Texto de ejemplo."}'
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

### `GET /api/v1/revision-automatica/excepciones`

**Listar excepciones de revisión.** Bandeja de excepciones de la empresa: documentos que el sistema no aprobó solo (`Excepcion`) y aprobaciones automáticas por confirmar (`Muestreo`). `estado`: `Pendientes` (por omisión) o `Resueltas` (las más recientes primero). Hasta 200 filas; lo más antiguo primero. 422 `estado-invalido`.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarExcepcionesDeRevision`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `estado` | query | texto | no |  |

**Respuestas**

- `200` OK — lista de ExcepcionDeRevisionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de ExcepcionDeRevisionDto | sí |  |
| `[].expedienteId` | uuid | sí |  |
| `[].folio` | texto | sí |  |
| `[].vinculoId` | uuid | sí |  |
| `[].tercero` | TerceroResumenDto | sí |  |
| `[].tercero.id` | uuid | sí |  |
| `[].tercero.nombre` | texto | sí |  |
| `[].tercero.identificacion` | texto | sí |  |
| `[].tipoVinculo` | texto | sí |  |
| `[].estadoExpediente` | texto | sí |  |
| `[].requisitoId` | uuid | sí |  |
| `[].requisito` | texto | sí |  |
| `[].tipoCanonico` | texto o null | no |  |
| `[].version` | entero | sí |  |
| `[].areaId` | uuid | sí |  |
| `[].areaCodigo` | texto | sí |  |
| `[].tipo` | texto | sí |  |
| `[].resultado` | texto | sí |  |
| `[].motivos` | lista de MotivoDeRevisionDto | sí |  |
| `[].motivos[].codigo` | texto | sí |  |
| `[].motivos[].texto` | texto | sí |  |
| `[].confianza` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `[].desde` | fecha y hora | sí |  |
| `[].resuelta` | booleano | sí |  |
| `[].resolucion` | texto o null | no |  |
| `[].resueltaPor` | texto o null | no |  |
| `[].resueltaEn` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `422` `estado-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/revision-automatica/excepciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
  {
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "folio": "texto",
    "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "tercero": {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "identificacion": "900123456"
    },
    "tipoVinculo": "texto",
    "estadoExpediente": "texto",
    "requisitoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "requisito": "texto",
    "tipoCanonico": "texto",
    "version": 1,
    "areaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "areaCodigo": "codigo-de-ejemplo",
    "tipo": "texto",
    "resultado": "texto",
    "motivos": [
      {
        "codigo": "CODIGO_EJEMPLO",
        "texto": "Texto de ejemplo."
      }
    ],
    "confianza": 1,
    "desde": "2026-11-12T15:00:00Z",
    "resuelta": true,
    "resolucion": "texto",
    "resueltaPor": "texto",
    "resueltaEn": "2026-11-12T15:00:00Z"
  }
]
```

</details>
