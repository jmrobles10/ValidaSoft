# API · Verificaciones

22 operaciones en 7 recursos.

## Adjuntos de cumplimiento

### `PUT /api/v1/expedientes/{id}/cumplimiento/adjuntos`

**Iniciar adjunto de cumplimiento.** Paso 1: URL PUT del archivo. 422 `adjunto-invalido` (tamaño o formato); 409 `estado-invalido` con el expediente cerrado.

**Permiso verificaciones:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `iniciarAdjuntoDeCumplimiento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, IniciarAdjuntoDeCumplimientoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombreArchivo` | texto | sí |  |
| `tamanoBytes` | entero | sí |  |
| `tipoMime` | texto | sí |  |
| `proposito` | PropositoDeAdjunto (texto) | sí | valores: Estudio · ConsultaManual |

**Respuestas**

- `200` OK — CargaDeAdjuntoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `cargaId` | uuid | sí |  |
| `carga` | InstruccionCargaDto | sí |  |
| `carga.metodo` | texto | sí |  |
| `carga.url` | texto | sí |  |
| `carga.cabeceras` | mapa de texto → texto | sí |  |
| `carga.expira` | fecha y hora | sí |  |

**Errores de negocio que lanza**

- `409` `estado-invalido`
- `422` `adjunto-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/cumplimiento/adjuntos" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"nombreArchivo":"Nombre de ejemplo","tamanoBytes":1,"tipoMime":"texto","proposito":"Estudio"}'
```

Respuesta `200`:

```json
{
  "cargaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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

### `POST /api/v1/expedientes/{id}/cumplimiento/adjuntos/{adjuntoId}/descarga`

**Descargar adjunto de cumplimiento.** URL prefirmada (5 min) de un archivo del oficial; la descarga queda en el historial del expediente. 404 `adjunto-no-encontrado`.

**Permiso documentos:descargar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `descargarAdjuntoDeCumplimiento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `adjuntoId` | path | uuid | sí |  |

**Respuestas**

- `200` OK — DescargaEvidenciaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `url` | texto | sí |  |
| `expira` | fecha y hora | sí |  |
| `nombreArchivo` | texto | sí |  |

**Errores de negocio que lanza**

- `404` `adjunto-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/cumplimiento/adjuntos/3fa85f64-5717-4562-b3fc-2c963f66afa6/descarga" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "url": "https://ejemplo.co/recurso",
  "expira": "2026-11-12T15:00:00Z",
  "nombreArchivo": "Nombre de ejemplo"
}
```

</details>

### `POST /api/v1/expedientes/{id}/cumplimiento/adjuntos/{cargaId}/confirmar`

**Confirmar adjunto de cumplimiento.** Paso 2: lee el objeto, verifica tamaño y el tipo real por sus bytes (422 `adjunto-invalido`) y lo registra en el expediente. 404 `carga-no-encontrada` si no hay carga; 409 `archivo-no-cargado` si aún no llegó el archivo.

**Permiso verificaciones:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `confirmarAdjuntoDeCumplimiento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `cargaId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ConfirmarAdjuntoDeCumplimientoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombreArchivo` | texto | sí |  |
| `proposito` | PropositoDeAdjunto (texto) | sí | valores: Estudio · ConsultaManual |

**Respuestas**

- `200` OK — AdjuntoDeCumplimientoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `nombre` | texto | sí |  |
| `tipoContenido` | texto | sí |  |
| `tamano` | entero | sí |  |
| `sha256` | texto | sí |  |
| `proposito` | PropositoDeAdjunto (texto) | sí | valores: Estudio · ConsultaManual |
| `subidoPor` | texto | sí |  |
| `fecha` | fecha y hora | sí |  |

**Errores de negocio que lanza**

- `409` `archivo-no-cargado`
- `422` `adjunto-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/cumplimiento/adjuntos/3fa85f64-5717-4562-b3fc-2c963f66afa6/confirmar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"nombreArchivo":"Nombre de ejemplo","proposito":"Estudio"}'
```

Respuesta `200`:

```json
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
```

</details>

### `POST /api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/consulta-manual`

**Registrar consulta manual de listas.** Registra una consulta de listas hecha por fuera con su PDF ya subido (`propósito ConsultaManual`). Entra como una consulta más: sin coincidencias queda satisfecha; con ellas, por analizar. No cuenta contra la cuota del proveedor. 422 `adjunto-invalido`, `fuente-invalida`, `consulta-fuera-de-plazo`, `persona-invalida`, `vigencia-invalida`; 403 `conflicto-de-intereses`.

**Permiso verificaciones:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `registrarConsultaManualDeListas`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, RegistrarConsultaManualDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `adjuntoId` | uuid | sí |  |
| `fuente` | texto | sí |  |
| `consultadaEn` | fecha y hora | sí |  |
| `coincidencias` | lista de CoincidenciaManualDto o null | no |  |
| `coincidencias[].clavePersona` | texto | sí |  |
| `coincidencias[].lista` | texto | sí |  |
| `coincidencias[].nombreEncontrado` | texto | sí |  |
| `coincidencias[].detalle` | texto o null | no |  |
| `coincidencias[].puntaje` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `mesesVigencia` | entero o null | no |  |

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

- `422` `adjunto-invalido`
- `422` `fuente-invalida`
- `422` `persona-invalida`
- `422` `vigencia-invalida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/verificaciones/ListasRestrictivas/consulta-manual" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"adjuntoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","fuente":"texto","consultadaEn":"2026-11-12T15:00:00Z","coincidencias":[{"clavePersona":"texto","lista":"texto","nombreEncontrado":"Nombre de ejemplo","detalle":"Texto de ejemplo.","puntaje":1}],"mesesVigencia":1}'
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

### `GET /api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/personas`

**Listar personas de consulta de listas.** Las personas que cubre hoy una consulta de listas (el tercero, sus representantes, socios y PEP), para reportar sobre ellas lo que dio la consulta hecha por fuera.

**Permiso verificaciones:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarPersonasDeConsultaDeListas`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — PersonasParaConsultaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `personas` | lista de PersonaConsultadaDto | sí |  |
| `personas[].rol` | texto | sí |  |
| `personas[].nombre` | texto | sí |  |
| `personas[].identificacion` | texto | sí |  |
| `claves` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/verificaciones/ListasRestrictivas/personas" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "personas": [
    {
      "rol": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "identificacion": "900123456"
    }
  ],
  "claves": [
    "texto"
  ]
}
```

</details>

## Informe cumplimiento

Informe SAGRILAFT (docs/05 §2.7, VE-14): las verificaciones de los expedientes de la empresa en el periodo, con totales por verificación y proveedor, la distribución del riesgo, los estudios vencidos y las listas por reconsultar. Se calcula al consultar (la proyección `InformeCumplimiento` llega cuando el volumen lo pida); Excel con `HojaDeCalculo`.

### `GET /api/v1/informes/cumplimiento`

**Obtener informe cumplimiento.** Periodo `desde`–`hasta` (fechas; por omisión los últimos 90 días). 422 `periodo-invalido`.

**Permiso auditoria:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerInformeCumplimiento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `desde` | query | fecha | no |  |
| `hasta` | query | fecha | no |  |

**Respuestas**

- `200` OK — InformeCumplimientoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desde` | fecha | sí |  |
| `hasta` | fecha | sí |  |
| `verificaciones` | lista de ResumenVerificacionesDto | sí |  |
| `verificaciones[].tipo` | texto | sí |  |
| `verificaciones[].proveedor` | texto o null | no |  |
| `verificaciones[].total` | entero | sí |  |
| `verificaciones[].conCoincidencias` | entero | sí |  |
| `verificaciones[].confirmadas` | entero | sí |  |
| `verificaciones[].pendientes` | entero | sí |  |
| `riesgo` | lista de RiesgoDto | sí |  |
| `riesgo[].nivel` | entero | sí |  |
| `riesgo[].cantidad` | entero | sí |  |
| `estudiosVencidos` | entero | sí |  |
| `pendientesReconsulta` | entero | sí |  |
| `filas` | lista de FilaInformeCumplimientoDto | sí |  |
| `filas[].expedienteId` | uuid | sí |  |
| `filas[].folio` | texto | sí |  |
| `filas[].tercero` | texto | sí |  |
| `filas[].identificacion` | texto | sí |  |
| `filas[].tipoVinculo` | texto | sí |  |
| `filas[].estadoExpediente` | texto | sí |  |
| `filas[].verificacion` | texto | sí |  |
| `filas[].proveedor` | texto o null | no |  |
| `filas[].estado` | texto | sí |  |
| `filas[].resultado` | texto o null | no |  |
| `filas[].fecha` | fecha y hora o null | no |  |
| `filas[].personas` | entero | sí |  |
| `filas[].coincidencias` | entero | sí |  |
| `filas[].confirmadas` | entero | sí |  |
| `filas[].descartadas` | entero | sí |  |
| `filas[].nivelRiesgo` | entero o null | no |  |
| `filas[].conflictoDeInteres` | booleano o null | no |  |
| `filas[].debidaDiligencia` | texto o null | no |  |
| `filas[].concepto` | texto o null | no |  |
| `filas[].vigenteHasta` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/cumplimiento" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "desde": "2026-11-12",
  "hasta": "2026-11-12",
  "verificaciones": [
    {
      "tipo": "texto",
      "proveedor": "texto",
      "total": 1,
      "conCoincidencias": 1,
      "confirmadas": 1,
      "pendientes": 1
    }
  ],
  "riesgo": [
    {
      "nivel": 1,
      "cantidad": 1
    }
  ],
  "estudiosVencidos": 1,
  "pendientesReconsulta": 1,
  "filas": [
    {
      "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "folio": "texto",
      "tercero": "texto",
      "identificacion": "900123456",
      "tipoVinculo": "texto",
      "estadoExpediente": "texto",
      "verificacion": "texto",
      "proveedor": "texto",
      "estado": "texto",
      "resultado": "texto",
      "fecha": "2026-11-12T15:00:00Z",
      "personas": 1,
      "coincidencias": 1,
      "confirmadas": 1,
      "descartadas": 1,
      "nivelRiesgo": 1,
      "conflictoDeInteres": true,
      "debidaDiligencia": "texto",
      "concepto": "texto",
      "vigenteHasta": "2026-11-12T15:00:00Z"
    }
  ]
}
```

</details>

### `GET /api/v1/informes/cumplimiento/excel`

**Exportar informe cumplimiento.** El mismo informe en Excel (una fila por verificación de cada expediente).

**Permiso auditoria:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `exportarInformeCumplimiento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `desde` | query | fecha | no |  |
| `hasta` | query | fecha | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/informes/cumplimiento/excel" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

</details>

## Listas restrictivas

### `GET /api/v1/configuracion/listas-restrictivas`

**Obtener listas restrictivas.** Configuración y consumo del mes `mes` (`yyyy-MM`, por omisión el actual). 422 `mes-invalido`.

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerListasRestrictivas`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `mes` | query | texto | no |  |

**Respuestas**

- `200` OK — ListasRestrictivasConfigDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `proveedor` | texto o null | no |  |
| `proveedorEfectivo` | texto o null | no |  |
| `proveedores` | lista de ProveedorListasDto | sí |  |
| `proveedores[].nombre` | texto | sí |  |
| `proveedores[].real` | booleano | sí |  |
| `proveedores[].principal` | booleano | sí |  |
| `proveedores[].habilitadoEnInstalacion` | booleano | sí |  |
| `proveedores[].configurado` | booleano | sí |  |
| `proveedores[].usuario` | texto o null | no |  |
| `proveedores[].ultimosCuatroClave` | texto o null | no |  |
| `proveedores[].urlBase` | texto o null | no |  |
| `proveedores[].plan` | texto o null | no |  |
| `proveedores[].cuotaMensual` | entero o null | no |  |
| `proveedores[].modoBusqueda` | texto o null | no |  |
| `proveedores[].deLaPlataforma` | booleano | no | por omisión false |
| `uso` | lista de UsoListasDto | sí |  |
| `uso[].mes` | texto | sí |  |
| `uso[].proveedor` | texto | sí |  |
| `uso[].consultas` | entero | sí |  |
| `uso[].busquedas` | entero | sí |  |
| `uso[].conCoincidencias` | entero | sí |  |
| `uso[].cuotaMensual` | entero o null | no |  |
| `diasMonitoreo` | entero | sí |  |
| `vigenciaEstudioDias` | entero | sí |  |

**Errores de negocio que lanza**

- `422` `mes-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/configuracion/listas-restrictivas" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "proveedor": "texto",
  "proveedorEfectivo": "texto",
  "proveedores": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "real": true,
      "principal": true,
      "habilitadoEnInstalacion": true,
      "configurado": true,
      "usuario": "texto",
      "ultimosCuatroClave": "texto",
      "urlBase": "https://ejemplo.co/recurso",
      "plan": "texto",
      "cuotaMensual": 1,
      "modoBusqueda": "texto",
      "deLaPlataforma": false
    }
  ],
  "uso": [
    {
      "mes": "texto",
      "proveedor": "texto",
      "consultas": 1,
      "busquedas": 1,
      "conCoincidencias": 1,
      "cuotaMensual": 1
    }
  ],
  "diasMonitoreo": 1,
  "vigenciaEstudioDias": 1
}
```

</details>

### `PUT /api/v1/configuracion/listas-restrictivas`

**Actualizar listas restrictivas.** Cambia las credenciales de un proveedor real y/o el proveedor de la empresa (`proveedor: ""` = el de la instalación). 422 `proveedor-invalido`, `proveedor-no-habilitado` (la instalación no lo habilitó), `credenciales-requeridas` (la empresa no cargó las suyas, o la plataforma no configuró la de Infolaft), `credenciales-de-plataforma` (Infolaft no lleva credenciales de la empresa), `usuario-requerido`, `clave-requerida`, `clave-invalida`, `url-invalida`, `cuota-invalida`, `modo-busqueda-invalido`.

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarListasRestrictivas`**

**Cuerpo de la petición** (`application/json`, ActualizarListasRestrictivasDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `proveedor` | texto o null | no |  |
| `credenciales` | CredencialesListasDto o null | no |  |
| `credenciales.proveedor` | texto | sí |  |
| `credenciales.usuario` | texto o null | no |  |
| `credenciales.clave` | texto o null | no |  |
| `credenciales.urlBase` | texto o null | no |  |
| `credenciales.plan` | texto o null | no |  |
| `credenciales.cuotaMensual` | entero o null | no |  |
| `credenciales.modoBusqueda` | texto o null | no |  |

**Respuestas**

- `200` OK — ListasRestrictivasConfigDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ListasRestrictivasConfigDto` que se detallan en `GET /api/v1/configuracion/listas-restrictivas`.

**Errores de negocio que lanza**

- `422` `modo-busqueda-invalido`
- `422` `proveedor-no-habilitado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/configuracion/listas-restrictivas" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"proveedor":"texto","credenciales":{"proveedor":"texto","usuario":"texto","clave":"texto","urlBase":"https://ejemplo.co/recurso","plan":"texto","cuotaMensual":1,"modoBusqueda":"texto"}}'
```

Respuesta `200`:

```json
{
  "proveedor": "texto",
  "proveedorEfectivo": "texto",
  "proveedores": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "real": true,
      "principal": true,
      "habilitadoEnInstalacion": true,
      "configurado": true,
      "usuario": "texto",
      "ultimosCuatroClave": "texto",
      "urlBase": "https://ejemplo.co/recurso",
      "plan": "texto",
      "cuotaMensual": 1,
      "modoBusqueda": "texto",
      "deLaPlataforma": false
    }
  ],
  "uso": [
    {
      "mes": "texto",
      "proveedor": "texto",
      "consultas": 1,
      "busquedas": 1,
      "conCoincidencias": 1,
      "cuotaMensual": 1
    }
  ],
  "diasMonitoreo": 1,
  "vigenciaEstudioDias": 1
}
```

</details>

## Matriz

### `GET /api/v1/configuracion/matriz-riesgo`

**Obtener matriz de riesgo.** La matriz guardada o, si la empresa aún no tiene una, la plantilla por omisión (`esPorDefecto`, inactiva).

**Permiso configuracion:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerMatrizDeRiesgo`**

**Respuestas**

- `200` OK — MatrizDeRiesgoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `activa` | booleano | sí |  |
| `esPorDefecto` | booleano | sí |  |
| `version` | entero | sí |  |
| `factores` | lista de FactorDeRiesgoDto | sí |  |
| `factores[].codigo` | texto | sí |  |
| `factores[].nombre` | texto | sí |  |
| `factores[].origen` | OrigenDelFactor (texto) | sí | valores: Manual · Automatico |
| `factores[].peso` | entero | sí |  |
| `factores[].opciones` | lista de OpcionDeFactorDto | sí |  |
| `factores[].opciones[].codigo` | texto | sí |  |
| `factores[].opciones[].nombre` | texto | sí |  |
| `factores[].opciones[].puntaje` | entero | sí |  |
| `factores[].opciones[].nivelMinimo` | entero o null | no |  |
| `factores[].ayuda` | texto o null | no |  |
| `limiteBajo` | entero | sí |  |
| `limiteMedio` | entero | sí |  |
| `niveles` | lista de NivelDeRiesgoDto | sí |  |
| `niveles[].nivel` | entero | sí |  |
| `niveles[].nombre` | texto | sí |  |
| `niveles[].debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `niveles[].mesesRevision` | entero | sí |  |
| `actualizadaEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/configuracion/matriz-riesgo" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "activa": true,
  "esPorDefecto": true,
  "version": 1,
  "factores": [
    {
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S.",
      "origen": "Manual",
      "peso": 1,
      "opciones": [
        {
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "puntaje": 1,
          "nivelMinimo": 1
        }
      ],
      "ayuda": "texto"
    }
  ],
  "limiteBajo": 1,
  "limiteMedio": 1,
  "niveles": [
    {
      "nivel": 1,
      "nombre": "Constructora Norte S.A.S.",
      "debidaDiligencia": "Simple",
      "mesesRevision": 1
    }
  ],
  "actualizadaEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `PUT /api/v1/configuracion/matriz-riesgo`

**Actualizar matriz de riesgo.** Guarda la matriz completa. 422: `factores-invalidos`, `peso-invalido`, `opciones-invalidas`, `factor-automatico-invalido`, `limites-invalidos`, `niveles-invalidos` u `origen-invalido`.

**Permiso configuracion:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `actualizarMatrizDeRiesgo`**

**Cuerpo de la petición** (`application/json`, ActualizarMatrizDeRiesgoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `activa` | booleano | sí |  |
| `factores` | lista de FactorDeRiesgoDto | sí |  |
| `factores[].codigo` | texto | sí |  |
| `factores[].nombre` | texto | sí |  |
| `factores[].origen` | OrigenDelFactor (texto) | sí | valores: Manual · Automatico |
| `factores[].peso` | entero | sí |  |
| `factores[].opciones` | lista de OpcionDeFactorDto | sí |  |
| `factores[].opciones[].codigo` | texto | sí |  |
| `factores[].opciones[].nombre` | texto | sí |  |
| `factores[].opciones[].puntaje` | entero | sí |  |
| `factores[].opciones[].nivelMinimo` | entero o null | no |  |
| `factores[].ayuda` | texto o null | no |  |
| `limiteBajo` | entero | sí |  |
| `limiteMedio` | entero | sí |  |
| `niveles` | lista de NivelDeRiesgoDto | sí |  |
| `niveles[].nivel` | entero | sí |  |
| `niveles[].nombre` | texto | sí |  |
| `niveles[].debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `niveles[].mesesRevision` | entero | sí |  |

**Respuestas**

- `200` OK — MatrizDeRiesgoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `MatrizDeRiesgoDto` que se detallan en `GET /api/v1/configuracion/matriz-riesgo`.

**Errores de negocio que lanza**

- `422` `niveles-invalidos`
- `422` `origen-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/configuracion/matriz-riesgo" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Content-Type: application/json" \
  -d '{"activa":true,"factores":[{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S.","origen":"Manual","peso":1,"opciones":[{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S.","puntaje":1,"nivelMinimo":1}],"ayuda":"texto"}],"limiteBajo":1,"limiteMedio":1,"niveles":[{"nivel":1,"nombre":"Constructora Norte S.A.S.","debidaDiligencia":"Simple","mesesRevision":1}]}'
```

Respuesta `200`:

```json
{
  "activa": true,
  "esPorDefecto": true,
  "version": 1,
  "factores": [
    {
      "codigo": "CODIGO_EJEMPLO",
      "nombre": "Constructora Norte S.A.S.",
      "origen": "Manual",
      "peso": 1,
      "opciones": [
        {
          "codigo": "CODIGO_EJEMPLO",
          "nombre": "Constructora Norte S.A.S.",
          "puntaje": 1,
          "nivelMinimo": 1
        }
      ],
      "ayuda": "texto"
    }
  ],
  "limiteBajo": 1,
  "limiteMedio": 1,
  "niveles": [
    {
      "nivel": 1,
      "nombre": "Constructora Norte S.A.S.",
      "debidaDiligencia": "Simple",
      "mesesRevision": 1
    }
  ],
  "actualizadaEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /api/v1/expedientes/{id}/cumplimiento/matriz/calcular`

**Calcular matriz de riesgo.** Vista previa del cálculo para el expediente con las respuestas a los factores manuales, sin registrar nada: los automáticos los evalúa el sistema con lo que ya sabe. 422 `matriz-no-activa`, `respuestas-incompletas`, `opcion-invalida`.

**Permiso cumplimiento:decidir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `calcularMatrizDeRiesgo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, CalcularMatrizDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `respuestas` | lista de RespuestaDeMatrizDto o null | no |  |
| `respuestas[].factor` | texto | sí |  |
| `respuestas[].opcion` | texto | sí |  |

**Respuestas**

- `200` OK — ResultadoDeMatrizDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `puntaje` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `nivel` | entero | sí |  |
| `nivelPorPuntaje` | entero | sí |  |
| `forzadoPor` | texto o null | no |  |
| `debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `mesesRevision` | entero | sí |  |
| `versionMatriz` | entero | sí |  |
| `desglose` | lista de DesgloseDeMatrizDto | sí |  |
| `desglose[].factor` | texto | sí |  |
| `desglose[].factorNombre` | texto | sí |  |
| `desglose[].origen` | OrigenDelFactor (texto) | sí | valores: Manual · Automatico |
| `desglose[].peso` | entero | sí |  |
| `desglose[].opcion` | texto | sí |  |
| `desglose[].opcionNombre` | texto | sí |  |
| `desglose[].puntaje` | entero | sí |  |
| `desglose[].nivelMinimo` | entero o null | no |  |

**Errores de negocio que lanza**

- `422` `matriz-no-activa`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/cumplimiento/matriz/calcular" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"respuestas":[{"factor":"texto","opcion":"texto"}]}'
```

Respuesta `200`:

```json
{
  "puntaje": 1,
  "nivel": 1,
  "nivelPorPuntaje": 1,
  "forzadoPor": "texto",
  "debidaDiligencia": "Simple",
  "mesesRevision": 1,
  "versionMatriz": 1,
  "desglose": [
    {
      "factor": "texto",
      "factorNombre": "Nombre de ejemplo",
      "origen": "Manual",
      "peso": 1,
      "opcion": "texto",
      "opcionNombre": "Nombre de ejemplo",
      "puntaje": 1,
      "nivelMinimo": 1
    }
  ]
}
```

</details>

## Procesamiento inmediato de verificaciones

Ejecución inmediata del procesamiento de verificaciones (Plataforma), con fecha simulada solo en Development.

### `POST /api/v1/plataforma/verificaciones/revisar`

**Revisar verificaciones.** Corre ya lo que el procesador periódico haría (consultas automáticas de listas, expiración de lo vencido con el expediente abierto y monitoreo de los vínculos activos) y espera su resultado: de un tenant o de todos. `fecha` simula «ahora» y solo se admite en Development (pruebas de punta a punta: así se prueba un estudio vencido o la reconsulta periódica sin esperar 30 días; 422 `fecha-simulada-no-permitida` en otro entorno).

**Operador de Plataforma · API (`/api/v1`) · SDK: `revisarVerificaciones`**

**Cuerpo de la petición** (`application/json`, RevisarVerificacionesDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tenantId` | uuid o null | no |  |
| `fecha` | fecha y hora o null | no |  |

**Respuestas**

- `200` OK — ResultadoRevisionVerificacionesDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `consultas` | entero | sí |  |
| `expiraciones` | entero | sí |  |
| `monitoreos` | entero | sí |  |

**Errores de negocio que lanza**

- `422` `fecha-simulada-no-permitida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/verificaciones/revisar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","fecha":"2026-11-12T15:00:00Z"}'
```

Respuesta `200`:

```json
{
  "consultas": 1,
  "expiraciones": 1,
  "monitoreos": 1
}
```

</details>

## Rechazo de cumplimiento

Rechazo directo del oficial de cumplimiento (CU-18). En el SRM el oficial aprueba o rechaza su estudio y, con la configuración `RechazoOficial`, el rechazo cierra la solicitud; aquí también puede cerrarla, pero solo ante un hallazgo ya registrado (coincidencia confirmada o estudio desfavorable), con motivos del catálogo y justificación, y nunca de forma automática (f-4).

### `POST /api/v1/expedientes/{id}/cumplimiento/rechazar`

**Rechazar por cumplimiento.** Rechaza el expediente. 409 `rechazo-sin-hallazgo` sin una verificación fallida; 409 `estado-invalido` fuera de revisión; 422 `motivo-requerido`, `justificacion-requerida`; 403 `conflicto-de-intereses`.

**Permiso cumplimiento:decidir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `rechazarPorCumplimiento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, RechazarPorCumplimientoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `motivos` | lista de texto | sí |  |
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `POST /api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/consulta-manual`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/cumplimiento/rechazar" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"motivos":["Texto de ejemplo."],"justificacion":"Texto de ejemplo."}'
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

## Verificaciones

Verificaciones externas del expediente (H7, docs/05 §2.7; VE-5…VE-13), todo con `X-Empresa`: consultar o reconsultar listas (`verificaciones:escribir`), resolver una coincidencia y registrar el estudio SAGRILAFT (`cumplimiento:decidir`, siempre una persona), descargar la evidencia (auditada) y la bandeja del oficial. Cada decisión pasa por la cascada del expediente: si era lo último que faltaba, el expediente se aprueba en la misma transacción (vínculo activo y certificado).

### `GET /api/v1/cumplimiento/bandeja`

**Listar bandeja cumplimiento.** Bandeja del oficial de cumplimiento: expedientes abiertos con alguna verificación que no está satisfecha (pendiente, con coincidencias, fallida o vencida) y vínculos activos cuyo monitoreo encontró coincidencias nuevas. Lo más antiguo primero.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `listarBandejaCumplimiento`**

**Respuestas**

- `200` OK — lista de FilaCumplimientoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de FilaCumplimientoDto | sí |  |
| `[].origen` | OrigenFilaCumplimiento (texto) | sí | valores: Expediente · Monitoreo |
| `[].expedienteId` | uuid o null | no |  |
| `[].folio` | texto | sí |  |
| `[].vinculoId` | uuid | sí |  |
| `[].tercero` | TerceroResumenDto | sí |  |
| `[].tercero.id` | uuid | sí |  |
| `[].tercero.nombre` | texto | sí |  |
| `[].tercero.identificacion` | texto | sí |  |
| `[].tipoVinculo` | texto o null | no |  |
| `[].estadoExpediente` | texto o null | no |  |
| `[].pendientes` | lista de VerificacionPendienteDto | sí |  |
| `[].pendientes[].tipo` | TipoVerificacion (texto) | sí | valores: ListasRestrictivas · EstudioDeCumplimiento |
| `[].pendientes[].estado` | EstadoVerificacion (texto) | sí | valores: Pendiente · CoincidenciaPorAnalizar · Satisfecha · Fallida · Expirada |
| `[].pendientes[].bloqueante` | booleano | sí |  |
| `[].coincidenciasPorAnalizar` | entero | sí |  |
| `[].esperandoSoloVerificaciones` | booleano | sí |  |
| `[].desde` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/cumplimiento/bandeja" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
[
  {
    "origen": "Expediente",
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
    "pendientes": [
      {
        "tipo": "ListasRestrictivas",
        "estado": "Pendiente",
        "bloqueante": true
      }
    ],
    "coincidenciasPorAnalizar": 1,
    "esperandoSoloVerificaciones": true,
    "desde": "2026-11-12T15:00:00Z"
  }
]
```

</details>

### `POST /api/v1/expedientes/{id}/verificaciones/{tipo}/coincidencias/{coincidenciaId}/resolver`

**Resolver coincidencia.** Cumplimiento descarta o confirma una coincidencia con justificación (P-9-4). Confirmada ⇒ la verificación queda `Fallida` y el expediente no cierra (f-4). 404 `coincidencia-no-encontrada`; 409 `coincidencia-ya-resuelta`; 403 `conflicto-de-intereses`; 422 `justificacion-requerida`, `decision-invalida`.

**Permiso cumplimiento:decidir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `resolverCoincidencia`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `tipo` | path | texto | sí |  |
| `coincidenciaId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ResolverCoincidenciaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `decision` | DecisionCoincidencia (texto) | sí | valores: Descartada · Confirmada |
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `POST /api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/consulta-manual`.

**Errores de negocio que lanza**

- `422` `tipo-no-consultable`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/verificaciones/texto/coincidencias/3fa85f64-5717-4562-b3fc-2c963f66afa6/resolver" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"decision":"Descartada","justificacion":"Texto de ejemplo."}'
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

### `POST /api/v1/expedientes/{id}/verificaciones/{tipo}/consultar`

**Consultar verificación.** Consulta (o reconsulta) ahora las listas del tercero y de sus personas relacionadas con el proveedor de la empresa. 422 `tipo-no-consultable` (solo `ListasRestrictivas`), `cuota-listas-agotada`, `credenciales-requeridas`; 409 `estado-invalido` con el expediente cerrado; 503 `verificacion-no-disponible`.

**Permiso verificaciones:escribir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `consultarVerificacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `tipo` | path | texto | sí |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `POST /api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/consulta-manual`.

**Errores de negocio que lanza**

- `409` `estado-invalido`
- `422` `tipo-no-consultable`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/verificaciones/texto/consultar" \
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

### `POST /api/v1/expedientes/{id}/verificaciones/{tipo}/evidencia/descarga`

**Descargar evidencia verificación.** URL prefirmada (5 min) del PDF de evidencia de la consulta vigente o de una anterior (`referencia`). La descarga queda en el historial del expediente (`EvidenciaDeVerificacionDescargada` con actor, IP y user-agent).

**Permiso documentos:descargar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `descargarEvidenciaVerificacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `tipo` | path | texto | sí |  |
| `referencia` | query | texto | no |  |

**Respuestas**

- `200` OK — DescargaEvidenciaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `url` | texto | sí |  |
| `expira` | fecha y hora | sí |  |
| `nombreArchivo` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/verificaciones/texto/evidencia/descarga" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "url": "https://ejemplo.co/recurso",
  "expira": "2026-11-12T15:00:00Z",
  "nombreArchivo": "Nombre de ejemplo"
}
```

</details>

### `POST /api/v1/expedientes/{id}/verificaciones/estudio-cumplimiento`

**Registrar estudio cumplimiento.** Registra el estudio del Oficial de Cumplimiento (SAGRILAFT, VE-9) con la vigencia del flujo o la de la configuración (30 días). Las evidencias deben ser versiones de documentos del expediente (422 `evidencia-invalida`).

**Permiso cumplimiento:decidir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `registrarEstudioCumplimiento`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, RegistrarEstudioCumplimientoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nivelRiesgo` | entero o null | no |  |
| `conflictoDeInteres` | booleano | sí |  |
| `debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `concepto` | ConceptoCumplimiento (texto) | sí | valores: Favorable · Desfavorable |
| `observaciones` | texto o null | no |  |
| `evidencias` | lista de EvidenciaEstudioDto o null | no |  |
| `evidencias[].documentoId` | uuid | sí |  |
| `evidencias[].version` | entero | sí |  |
| `evidencias[].nombre` | texto o null | no |  |
| `respuestas` | lista de RespuestaDeMatrizDto o null | no |  |
| `respuestas[].factor` | texto | sí |  |
| `respuestas[].opcion` | texto | sí |  |
| `justificacionDeNivel` | texto o null | no |  |
| `adjuntos` | lista de uuid o null | no |  |

**Respuestas**

- `200` OK — ExpedienteDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ExpedienteDto` que se detallan en `POST /api/v1/expedientes/{id}/verificaciones/ListasRestrictivas/consulta-manual`.

**Errores de negocio que lanza**

- `422` `concepto-invalido`
- `422` `debida-diligencia-insuficiente`
- `422` `debida-diligencia-invalida`
- `422` `debida-diligencia-requerida`
- `422` `evidencia-invalida`
- `422` `nivel-riesgo-requerido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/expedientes/3fa85f64-5717-4562-b3fc-2c963f66afa6/verificaciones/estudio-cumplimiento" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"nivelRiesgo":1,"conflictoDeInteres":true,"debidaDiligencia":"Simple","concepto":"Favorable","observaciones":"Texto de ejemplo.","evidencias":[{"documentoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","version":1,"nombre":"Constructora Norte S.A.S."}],"respuestas":[{"factor":"texto","opcion":"texto"}],"justificacionDeNivel":"Texto de ejemplo.","adjuntos":["3fa85f64-5717-4562-b3fc-2c963f66afa6"]}'
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

### `GET /api/v1/vinculos/{id}/monitoreo-listas`

**Obtener monitoreo listas.** Monitoreo de listas del vínculo (VE-12): última reconsulta periódica y la próxima. 404 `monitoreo-no-encontrado`.

**Permiso expedientes:leer · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `obtenerMonitoreoListas`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — MonitoreoListasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `vinculoId` | uuid | sí |  |
| `expedienteId` | uuid | sí |  |
| `folio` | texto | sí |  |
| `frecuenciaDias` | entero | sí |  |
| `proximaConsulta` | fecha y hora o null | no |  |
| `listas` | VerificacionDto | sí |  |
| `listas.tipo` | TipoVerificacion (texto) | sí | valores: ListasRestrictivas · EstudioDeCumplimiento |
| `listas.estado` | EstadoVerificacion (texto) | sí | valores: Pendiente · CoincidenciaPorAnalizar · Satisfecha · Fallida · Expirada |
| `listas.bloqueante` | booleano | sí |  |
| `listas.requeridaPorElFlujo` | booleano | sí |  |
| `listas.proveedor` | texto o null | no |  |
| `listas.resultado` | texto o null | no |  |
| `listas.referenciaExterna` | texto o null | no |  |
| `listas.registradaEn` | fecha y hora o null | no |  |
| `listas.registradaPor` | texto o null | no |  |
| `listas.vigenteHasta` | fecha y hora o null | no |  |
| `listas.evidencia` | EvidenciaVerificacionDto o null | no |  |
| `listas.evidencia.nombreArchivo` | texto | sí |  |
| `listas.evidencia.tipoContenido` | texto | sí |  |
| `listas.evidencia.tamano` | entero | sí |  |
| `listas.evidencia.sha256` | texto | sí |  |
| `listas.evidencia.delProveedor` | booleano | sí |  |
| `listas.evidencia.sha256Respuesta` | texto o null | no |  |
| `listas.personas` | lista de PersonaConsultadaDto | sí |  |
| `listas.personas[].rol` | texto | sí |  |
| `listas.personas[].nombre` | texto | sí |  |
| `listas.personas[].identificacion` | texto | sí |  |
| `listas.coincidencias` | lista de CoincidenciaDto | sí |  |
| `listas.coincidencias[].id` | uuid | sí |  |
| `listas.coincidencias[].persona` | texto | sí |  |
| `listas.coincidencias[].rol` | texto | sí |  |
| `listas.coincidencias[].identificacion` | texto | sí |  |
| `listas.coincidencias[].lista` | texto | sí |  |
| `listas.coincidencias[].nombreEncontrado` | texto | sí |  |
| `listas.coincidencias[].detalle` | texto o null | no |  |
| `listas.coincidencias[].puntaje` | number \| string o null | no | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `listas.coincidencias[].decision` | texto o null | no |  |
| `listas.coincidencias[].justificacion` | texto o null | no |  |
| `listas.coincidencias[].resueltaPor` | texto o null | no |  |
| `listas.coincidencias[].resueltaEn` | fecha y hora o null | no |  |
| `listas.coincidencias[].vigenteHasta` | fecha y hora o null | no |  |
| `listas.coincidencias[].heredada` | booleano | sí |  |
| `listas.estudio` | EstudioCumplimientoDto o null | no |  |
| `listas.estudio.verificadoEnListas` | booleano | sí |  |
| `listas.estudio.conflictoDeInteres` | booleano | sí |  |
| `listas.estudio.debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `listas.estudio.nivelRiesgo` | entero | sí |  |
| `listas.estudio.observaciones` | texto o null | no |  |
| `listas.estudio.evidencias` | lista de EvidenciaEstudioDto | sí |  |
| `listas.estudio.evidencias[].documentoId` | uuid | sí |  |
| `listas.estudio.evidencias[].version` | entero | sí |  |
| `listas.estudio.evidencias[].nombre` | texto o null | no |  |
| `listas.estudio.concepto` | ConceptoCumplimiento (texto) | sí | valores: Favorable · Desfavorable |
| `listas.estudio.oficial` | texto | sí |  |
| `listas.estudio.fecha` | fecha y hora | sí |  |
| `listas.estudio.vigenteHasta` | fecha y hora | sí |  |
| `listas.estudio.matriz` | ResultadoDeMatrizDto o null | no |  |
| `listas.estudio.matriz.puntaje` | number \| string | sí | patrón ^-?(?:0\|[1-9]\d*)(?:\.\d+)?$ |
| `listas.estudio.matriz.nivel` | entero | sí |  |
| `listas.estudio.matriz.nivelPorPuntaje` | entero | sí |  |
| `listas.estudio.matriz.forzadoPor` | texto o null | no |  |
| `listas.estudio.matriz.debidaDiligencia` | DebidaDiligencia (texto) | sí | valores: Simple · Ordinaria · Ampliada |
| `listas.estudio.matriz.mesesRevision` | entero | sí |  |
| `listas.estudio.matriz.versionMatriz` | entero | sí |  |
| `listas.estudio.matriz.desglose` | lista de DesgloseDeMatrizDto | sí |  |
| `listas.estudio.matriz.desglose[].factor` | texto | sí |  |
| `listas.estudio.matriz.desglose[].factorNombre` | texto | sí |  |
| `listas.estudio.matriz.desglose[].origen` | OrigenDelFactor (texto) | sí | valores: Manual · Automatico |
| `listas.estudio.matriz.desglose[].peso` | entero | sí |  |
| `listas.estudio.matriz.desglose[].opcion` | texto | sí |  |
| `listas.estudio.matriz.desglose[].opcionNombre` | texto | sí |  |
| `listas.estudio.matriz.desglose[].puntaje` | entero | sí |  |
| `listas.estudio.matriz.desglose[].nivelMinimo` | entero o null | no |  |
| `listas.estudio.justificacionDeNivel` | texto o null | no |  |
| `listas.estudio.adjuntos` | lista de AdjuntoDeCumplimientoDto o null | no |  |
| `listas.estudio.adjuntos[].id` | uuid | sí |  |
| `listas.estudio.adjuntos[].nombre` | texto | sí |  |
| `listas.estudio.adjuntos[].tipoContenido` | texto | sí |  |
| `listas.estudio.adjuntos[].tamano` | entero | sí |  |
| `listas.estudio.adjuntos[].sha256` | texto | sí |  |
| `listas.estudio.adjuntos[].proposito` | PropositoDeAdjunto (texto) | sí | valores: Estudio · ConsultaManual |
| `listas.estudio.adjuntos[].subidoPor` | texto | sí |  |
| `listas.estudio.adjuntos[].fecha` | fecha y hora | sí |  |
| `listas.anteriores` | lista de ConsultaAnteriorDto | sí |  |
| `listas.anteriores[].proveedor` | texto | sí |  |
| `listas.anteriores[].referenciaExterna` | texto | sí |  |
| `listas.anteriores[].resultado` | texto | sí |  |
| `listas.anteriores[].fecha` | fecha y hora | sí |  |
| `listas.anteriores[].coincidencias` | entero | sí |  |
| `listas.anteriores[].conEvidencia` | booleano | sí |  |
| `listas.ultimoError` | texto o null | no |  |
| `listas.reintentarDesde` | fecha y hora o null | no |  |
| `listas.vigenciaDias` | entero o null | no |  |
| `listas.frecuenciaReconsultaMeses` | entero o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/monitoreo-listas" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID"
```

Respuesta `200`:

```json
{
  "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "frecuenciaDias": 1,
  "proximaConsulta": "2026-11-12T15:00:00Z",
  "listas": {
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
            "factor": "texto",
            "factorNombre": "Nombre de ejemplo",
            "origen": "Manual",
            "peso": 1,
            "opcion": "texto",
            "opcionNombre": "Nombre de ejemplo",
            "puntaje": 1,
            "nivelMinimo": 1
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
}
```

</details>

### `POST /api/v1/vinculos/{id}/monitoreo-listas/coincidencias/{coincidenciaId}/resolver`

**Resolver coincidencia monitoreo.** Resuelve una coincidencia del monitoreo con las mismas reglas que en el expediente; queda en la auditoría (`MonitoreoDeListas`). Confirmada: el administrador decide bloquear o desvincular (nunca automático).

**Permiso cumplimiento:decidir · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `resolverCoincidenciaMonitoreo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `coincidenciaId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ResolverCoincidenciaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `decision` | DecisionCoincidencia (texto) | sí | valores: Descartada · Confirmada |
| `justificacion` | texto | sí |  |

**Respuestas**

- `200` OK — MonitoreoListasDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `MonitoreoListasDto` que se detallan en `GET /api/v1/vinculos/{id}/monitoreo-listas`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/monitoreo-listas/coincidencias/3fa85f64-5717-4562-b3fc-2c963f66afa6/resolver" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"decision":"Descartada","justificacion":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "folio": "texto",
  "frecuenciaDias": 1,
  "proximaConsulta": "2026-11-12T15:00:00Z",
  "listas": {
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
            "factor": "texto",
            "factorNombre": "Nombre de ejemplo",
            "origen": "Manual",
            "peso": 1,
            "opcion": "texto",
            "opcionNombre": "Nombre de ejemplo",
            "puntaje": 1,
            "nivelMinimo": 1
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
}
```

</details>

### `POST /api/v1/vinculos/{id}/monitoreo-listas/evidencia/descarga`

**Descargar evidencia monitoreo.** URL prefirmada de la evidencia de la última reconsulta del monitoreo (la entrega queda en la auditoría).

**Permiso documentos:descargar · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `descargarEvidenciaMonitoreo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — DescargaEvidenciaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `url` | texto | sí |  |
| `expira` | fecha y hora | sí |  |
| `nombreArchivo` | texto | sí |  |

**Errores de negocio que lanza**

- `404` `evidencia-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/vinculos/3fa85f64-5717-4562-b3fc-2c963f66afa6/monitoreo-listas/evidencia/descarga" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "url": "https://ejemplo.co/recurso",
  "expira": "2026-11-12T15:00:00Z",
  "nombreArchivo": "Nombre de ejemplo"
}
```

</details>
