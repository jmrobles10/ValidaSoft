# API · Interesado

9 operaciones en 5 recursos.

## Acceso por correo

Volver a entrar con el correo (RE-1): el tercero pide un enlace y lo recibe si tiene vínculos vigentes con ese correo. La respuesta nunca revela si el correo existe.

### `POST /public/v1/acceso`

**Solicitar acceso.** Siempre 202 con el mismo cuerpo. Si hay vínculos no terminales (de cualquier tenant) cuyo correo de invitación es este, envía un enlace de acceso sin vínculo por cada tercero (con OTP si alguno de sus tipos lo exige), como máximo 3 por hora por correo. Correo vacío o mal formado ⇒ 422 `correo-invalido` (no depende de que exista).

**Pública (sin sesión) · Portal del tercero (`/public/v1`) · SDK: `solicitarAcceso`**

**Cuerpo de la petición** (`application/json`, SolicitarAccesoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | texto o null | no |  |

**Respuestas**

- `202` Aceptado — SolicitudRecibidaDto

Campos de la respuesta `202`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `mensaje` | texto | sí |  |

**Errores de negocio que lanza**

- `422` `correo-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/acceso" \
  -H "Content-Type: application/json" \
  -d '{"correo":"contacto@empresa.co"}'
```

Respuesta `202`:

```json
{
  "mensaje": "Texto de ejemplo."
}
```

</details>

## Autorregistro

Autorregistro invisible (docs/05 §2.9): el tercero existe en la plataforma pero ninguna empresa lo ve hasta vincularlo.

### `POST /public/v1/autorregistro`

**Autorregistrar tercero.** Responde siempre 202 con el mismo cuerpo, exista o no el tercero. Si no existe lo crea (sin tenant) con el aviso de la plataforma aceptado; envía un enlace sin vínculo solo si el correo es de un contacto del tercero.

**Pública (sin sesión) · Portal del tercero (`/public/v1`) · SDK: `autorregistrarTercero`**

**Cuerpo de la petición** (`application/json`, AutorregistroDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `tipoPersona` | texto | sí |  |
| `nombre` | texto | sí |  |
| `correo` | texto | sí |  |
| `consentimientoPlataforma` | booleano | sí |  |

**Respuestas**

- `202` Aceptado — SolicitudRecibidaDto

Campos de la respuesta `202`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `mensaje` | texto | sí |  |

**Errores de negocio que lanza**

- `422` `consentimiento-requerido`
- `422` `tipo-persona-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/autorregistro" \
  -H "Content-Type: application/json" \
  -d '{"identificacion":{"pais":"CO","tipo":"texto","numero":"900123456","digitoVerificacion":"texto"},"tipoPersona":"texto","nombre":"Constructora Norte S.A.S.","correo":"contacto@empresa.co","consentimientoPlataforma":true}'
```

Respuesta `202`:

```json
{
  "mensaje": "Texto de ejemplo."
}
```

</details>

## Enlaces públicos

### `POST /public/v1/enlaces/{token}/canjear`

**Canjear enlace.** Sin OTP ⇒ 200 con la sesión y la cookie de refresco. Con OTP ⇒ 202 y el código al mismo correo. Usado/vencido ⇒ 410; inexistente ⇒ 404.

**Pública (sin sesión) · Portal del tercero (`/public/v1`) · SDK: `canjearEnlace`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `token` | path | texto | sí |  |

**Respuestas**

- `200` OK — SesionTerceroDto
- `202` Aceptado — OtpRequeridoDto

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `accessToken` | texto | sí |  |
| `expira` | fecha y hora | sí |  |
| `terceroId` | uuid | sí |  |
| `otpVerificado` | booleano | sí |  |
| `vinculos` | lista de VinculoDeSesionDto | sí |  |
| `vinculos[].id` | uuid | sí |  |
| `vinculos[].empresa` | EmpresaPublicaDto | sí |  |
| `vinculos[].empresa.id` | uuid | sí |  |
| `vinculos[].empresa.nombre` | texto | sí |  |
| `vinculos[].empresa.logoUrl` | texto o null | no |  |
| `vinculos[].empresa.colorMarca` | texto o null | no |  |
| `vinculos[].empresa.contactoSoporte` | texto o null | no |  |
| `vinculos[].estado` | EstadoVinculo (texto) | sí | valores: Invitado · EnValidacion · Activo · PorVencer · Vencido · Bloqueado · Desvinculado · Anulado |
| `expedienteOrigen` | uuid o null | no |  |
| `vinculoOrigen` | uuid o null | no |  |

Campos de la respuesta `202`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `requiereOtp` | booleano | sí |  |
| `expira` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/enlaces/vsk_prueba_ejemplo/canjear"
```

Respuesta `200`:

```json
{
  "accessToken": "vsk_prueba_ejemplo",
  "expira": "2026-11-12T15:00:00Z",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "otpVerificado": true,
  "vinculos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresa": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "logoUrl": "https://ejemplo.co/recurso",
        "colorMarca": "texto",
        "contactoSoporte": "texto"
      },
      "estado": "Invitado"
    }
  ],
  "expedienteOrigen": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vinculoOrigen": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

</details>

### `POST /public/v1/enlaces/{token}/otp`

**Verificar otp enlace.** Código correcto ⇒ sesión con `otpVerificado` y la cookie de refresco. Incorrecto ⇒ 422 `otp-invalido` (con `intentosRestantes`); agotado ⇒ 429.

**Pública (sin sesión) · Portal del tercero (`/public/v1`) · SDK: `verificarOtpEnlace`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `token` | path | texto | sí |  |

**Cuerpo de la petición** (`application/json`, VerificarOtpDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |

**Respuestas**

- `200` OK — SesionTerceroDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `SesionTerceroDto` que se detallan en `POST /public/v1/enlaces/{token}/canjear`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/enlaces/vsk_prueba_ejemplo/otp" \
  -H "Content-Type: application/json" \
  -d '{"codigo":"CODIGO_EJEMPLO"}'
```

Respuesta `200`:

```json
{
  "accessToken": "vsk_prueba_ejemplo",
  "expira": "2026-11-12T15:00:00Z",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "otpVerificado": true,
  "vinculos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresa": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "logoUrl": "https://ejemplo.co/recurso",
        "colorMarca": "texto",
        "contactoSoporte": "texto"
      },
      "estado": "Invitado"
    }
  ],
  "expedienteOrigen": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vinculoOrigen": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

</details>

## Invitaciones públicas

### `GET /public/v1/invitaciones/{vinculoId}`

**Obtener invitación pública.** Empresa, tipo, estado y los textos vigentes por aceptar; `textosFaltantes` si la empresa no los ha publicado.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerInvitacionPublica`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `vinculoId` | path | uuid | sí |  |

**Respuestas**

- `200` OK — InvitacionPublicaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `vinculo` | VinculoPublicoDto | sí |  |
| `vinculo.id` | uuid | sí |  |
| `vinculo.empresa` | EmpresaPublicaDto | sí |  |
| `vinculo.empresa.id` | uuid | sí |  |
| `vinculo.empresa.nombre` | texto | sí |  |
| `vinculo.empresa.logoUrl` | texto o null | no |  |
| `vinculo.empresa.colorMarca` | texto o null | no |  |
| `vinculo.empresa.contactoSoporte` | texto o null | no |  |
| `vinculo.tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `vinculo.tipoVinculo.id` | uuid | sí |  |
| `vinculo.tipoVinculo.codigo` | texto | sí |  |
| `vinculo.tipoVinculo.nombre` | texto | sí |  |
| `vinculo.estado` | EstadoVinculo (texto) | sí | valores: Invitado · EnValidacion · Activo · PorVencer · Vencido · Bloqueado · Desvinculado · Anulado |
| `vinculo.invitacionAceptada` | booleano | sí |  |
| `vinculo.invitacionAceptadaEn` | fecha y hora o null | no |  |
| `vinculo.expedienteId` | uuid o null | no |  |
| `vinculo.reintento` | ReintentoPublicoDto o null | no |  |
| `vinculo.reintento.rechazadoEn` | fecha y hora | sí |  |
| `vinculo.reintento.motivos` | lista de MotivoPublicoDto | sí |  |
| `vinculo.reintento.motivos[].codigo` | texto | sí |  |
| `vinculo.reintento.motivos[].texto` | texto | sí |  |
| `vinculo.reintento.permitido` | booleano | sí |  |
| `vinculo.reintento.disponible` | booleano | sí |  |
| `vinculo.reintento.desde` | fecha y hora o null | no |  |
| `vinculo.contactos` | lista de ContactoDeEmpresaDto o null | no |  |
| `vinculo.contactos[].nombre` | texto | sí |  |
| `vinculo.contactos[].cargo` | texto o null | no |  |
| `vinculo.contactos[].correo` | texto | sí |  |
| `vinculo.contactos[].telefono` | texto o null | no |  |
| `vinculo.contactos[].rol` | texto | sí |  |
| `vinculo.vigenteHasta` | fecha o null | no |  |
| `vinculo.enRevision` | booleano | no | por omisión false |
| `vinculo.actualizacionNoAprobada` | ActualizacionNoAprobadaPublicaDto o null | no |  |
| `vinculo.actualizacionNoAprobada.resultado` | texto | sí |  |
| `vinculo.actualizacionNoAprobada.fecha` | fecha y hora o null | no |  |
| `vinculo.actualizacionNoAprobada.motivos` | lista de MotivoPublicoDto | sí | mismos campos que `vinculo.reintento.motivos[]` |
| `textosPorAceptar` | lista de TextoLegalPublicoDto | sí |  |
| `textosPorAceptar[].tipo` | TipoTextoLegal (texto) | sí | valores: AvisoPlataforma · AutorizacionEmpresa · TerminosEmpresa |
| `textosPorAceptar[].textoVersion` | entero | sí |  |
| `textosPorAceptar[].hashTexto` | texto | sí |  |
| `textosPorAceptar[].contenido` | texto | sí |  |
| `textosPorAceptar[].publicadoEn` | fecha y hora | sí |  |
| `textosFaltantes` | lista de texto | sí |  |
| `puedeAceptar` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/invitaciones/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
{
  "vinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
    "estado": "Invitado",
    "invitacionAceptada": true,
    "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "reintento": {
      "rechazadoEn": "2026-11-12T15:00:00Z",
      "motivos": [
        {
          "codigo": "CODIGO_EJEMPLO",
          "texto": "Texto de ejemplo."
        }
      ],
      "permitido": true,
      "disponible": true,
      "desde": "2026-11-12T15:00:00Z"
    },
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
      "resultado": "texto",
      "fecha": "2026-11-12T15:00:00Z",
      "motivos": [
        {
          "codigo": "CODIGO_EJEMPLO",
          "texto": "Texto de ejemplo."
        }
      ]
    }
  },
  "textosPorAceptar": [
    {
      "tipo": "AvisoPlataforma",
      "textoVersion": 1,
      "hashTexto": "Texto de ejemplo.",
      "contenido": "Texto de ejemplo.",
      "publicadoEn": "2026-11-12T15:00:00Z"
    }
  ],
  "textosFaltantes": [
    "Texto de ejemplo."
  ],
  "puedeAceptar": true
}
```

</details>

### `POST /public/v1/invitaciones/{vinculoId}/aceptar`

**Aceptar invitación.** Registra `ConsentimientosRegistrados` y luego `InvitacionAceptada` en el stream del vínculo (actor = tercero) y comparte con la empresa la versión actual de cada documento global que piden los requisitos del flujo resuelto para el vínculo (RQ-3; más los `PorVinculo` ya cargados para él); la respuesta trae esos `compartidos`. Idempotente por su propia regla: repetir ⇒ 409 `invitacion-ya-aceptada`. En la misma transacción abre el expediente inicial con ese flujo (FL-5), con los requisitos cubiertos ya `Cargado`; si no hay flujo publicado no se comparte ningún global, la aceptación se registra igual y el vínculo queda con apertura pendiente (FL-6): nunca falla por configuración de la empresa.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `aceptarInvitacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `vinculoId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, AceptarInvitacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `consentimientos` | lista de ConsentimientoDto | sí |  |
| `consentimientos[].tipo` | TipoTextoLegal (texto) | sí | valores: AvisoPlataforma · AutorizacionEmpresa · TerminosEmpresa |
| `consentimientos[].textoVersion` | entero | sí |  |
| `consentimientos[].aceptado` | booleano | sí |  |
| `otpCodigo` | texto o null | no |  |

**Respuestas**

- `200` OK — InvitacionAceptadaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `aceptacion` | lista de AceptacionDto | sí |  |
| `aceptacion[].tipo` | TipoTextoLegal (texto) | sí | valores: AvisoPlataforma · AutorizacionEmpresa · TerminosEmpresa |
| `aceptacion[].textoLegalId` | uuid | sí |  |
| `aceptacion[].textoVersion` | entero | sí |  |
| `aceptacion[].hashTexto` | texto | sí |  |
| `aceptacion[].fecha` | fecha y hora | sí |  |
| `aceptacion[].ip` | texto | sí |  |
| `aceptacion[].userAgent` | texto | sí |  |
| `aceptacion[].otpVerificado` | booleano | sí |  |
| `aceptacion[].canal` | texto | sí |  |
| `aceptacion[].aceptadoPor` | AceptadoPorDto | sí |  |
| `aceptacion[].aceptadoPor.terceroId` | uuid | sí |  |
| `aceptacion[].aceptadoPor.correo` | texto | sí |  |
| `aceptacion[].documentoVersionId` | uuid o null | no |  |
| `vinculo` | VinculoPublicoDto | sí |  |
| `vinculo.id` | uuid | sí |  |
| `vinculo.empresa` | EmpresaPublicaDto | sí |  |
| `vinculo.empresa.id` | uuid | sí |  |
| `vinculo.empresa.nombre` | texto | sí |  |
| `vinculo.empresa.logoUrl` | texto o null | no |  |
| `vinculo.empresa.colorMarca` | texto o null | no |  |
| `vinculo.empresa.contactoSoporte` | texto o null | no |  |
| `vinculo.tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `vinculo.tipoVinculo.id` | uuid | sí |  |
| `vinculo.tipoVinculo.codigo` | texto | sí |  |
| `vinculo.tipoVinculo.nombre` | texto | sí |  |
| `vinculo.estado` | EstadoVinculo (texto) | sí | valores: Invitado · EnValidacion · Activo · PorVencer · Vencido · Bloqueado · Desvinculado · Anulado |
| `vinculo.invitacionAceptada` | booleano | sí |  |
| `vinculo.invitacionAceptadaEn` | fecha y hora o null | no |  |
| `vinculo.expedienteId` | uuid o null | no |  |
| `vinculo.reintento` | ReintentoPublicoDto o null | no |  |
| `vinculo.reintento.rechazadoEn` | fecha y hora | sí |  |
| `vinculo.reintento.motivos` | lista de MotivoPublicoDto | sí |  |
| `vinculo.reintento.motivos[].codigo` | texto | sí |  |
| `vinculo.reintento.motivos[].texto` | texto | sí |  |
| `vinculo.reintento.permitido` | booleano | sí |  |
| `vinculo.reintento.disponible` | booleano | sí |  |
| `vinculo.reintento.desde` | fecha y hora o null | no |  |
| `vinculo.contactos` | lista de ContactoDeEmpresaDto o null | no |  |
| `vinculo.contactos[].nombre` | texto | sí |  |
| `vinculo.contactos[].cargo` | texto o null | no |  |
| `vinculo.contactos[].correo` | texto | sí |  |
| `vinculo.contactos[].telefono` | texto o null | no |  |
| `vinculo.contactos[].rol` | texto | sí |  |
| `vinculo.vigenteHasta` | fecha o null | no |  |
| `vinculo.enRevision` | booleano | no | por omisión false |
| `vinculo.actualizacionNoAprobada` | ActualizacionNoAprobadaPublicaDto o null | no |  |
| `vinculo.actualizacionNoAprobada.resultado` | texto | sí |  |
| `vinculo.actualizacionNoAprobada.fecha` | fecha y hora o null | no |  |
| `vinculo.actualizacionNoAprobada.motivos` | lista de MotivoPublicoDto | sí | mismos campos que `vinculo.reintento.motivos[]` |
| `compartidos` | lista de CompartidoDto | sí |  |
| `compartidos[].documentoId` | uuid | sí |  |
| `compartidos[].tipo` | texto | sí |  |
| `compartidos[].version` | entero | sí |  |
| `compartidos[].vinculoId` | uuid | sí |  |
| `compartidos[].empresaId` | uuid | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/invitaciones/3fa85f64-5717-4562-b3fc-2c963f66afa6/aceptar" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO" \
  -H "Content-Type: application/json" \
  -d '{"consentimientos":[{"tipo":"AvisoPlataforma","textoVersion":1,"aceptado":true}],"otpCodigo":"codigo-de-ejemplo"}'
```

Respuesta `200`:

```json
{
  "aceptacion": [
    {
      "tipo": "AvisoPlataforma",
      "textoLegalId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "textoVersion": 1,
      "hashTexto": "Texto de ejemplo.",
      "fecha": "2026-11-12T15:00:00Z",
      "ip": "texto",
      "userAgent": "texto",
      "otpVerificado": true,
      "canal": "texto",
      "aceptadoPor": {
        "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "correo": "contacto@empresa.co"
      },
      "documentoVersionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "vinculo": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
    "estado": "Invitado",
    "invitacionAceptada": true,
    "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "reintento": {
      "rechazadoEn": "2026-11-12T15:00:00Z",
      "motivos": [
        {
          "codigo": "CODIGO_EJEMPLO",
          "texto": "Texto de ejemplo."
        }
      ],
      "permitido": true,
      "disponible": true,
      "desde": "2026-11-12T15:00:00Z"
    },
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
      "resultado": "texto",
      "fecha": "2026-11-12T15:00:00Z",
      "motivos": [
        {
          "codigo": "CODIGO_EJEMPLO",
          "texto": "Texto de ejemplo."
        }
      ]
    }
  },
  "compartidos": [
    {
      "documentoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "tipo": "texto",
      "version": 1,
      "vinculoId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ]
}
```

</details>

### `GET /public/v1/vinculos`

**Listar vínculos públicos.**

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `listarVinculosPublicos`**

**Respuestas**

- `200` OK — lista de VinculoPublicoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de VinculoPublicoDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].empresa` | EmpresaPublicaDto | sí |  |
| `[].empresa.id` | uuid | sí |  |
| `[].empresa.nombre` | texto | sí |  |
| `[].empresa.logoUrl` | texto o null | no |  |
| `[].empresa.colorMarca` | texto o null | no |  |
| `[].empresa.contactoSoporte` | texto o null | no |  |
| `[].tipoVinculo` | TipoVinculoResumenDto | sí |  |
| `[].tipoVinculo.id` | uuid | sí |  |
| `[].tipoVinculo.codigo` | texto | sí |  |
| `[].tipoVinculo.nombre` | texto | sí |  |
| `[].estado` | EstadoVinculo (texto) | sí | valores: Invitado · EnValidacion · Activo · PorVencer · Vencido · Bloqueado · Desvinculado · Anulado |
| `[].invitacionAceptada` | booleano | sí |  |
| `[].invitacionAceptadaEn` | fecha y hora o null | no |  |
| `[].expedienteId` | uuid o null | no |  |
| `[].reintento` | ReintentoPublicoDto o null | no |  |
| `[].reintento.rechazadoEn` | fecha y hora | sí |  |
| `[].reintento.motivos` | lista de MotivoPublicoDto | sí |  |
| `[].reintento.motivos[].codigo` | texto | sí |  |
| `[].reintento.motivos[].texto` | texto | sí |  |
| `[].reintento.permitido` | booleano | sí |  |
| `[].reintento.disponible` | booleano | sí |  |
| `[].reintento.desde` | fecha y hora o null | no |  |
| `[].contactos` | lista de ContactoDeEmpresaDto o null | no |  |
| `[].contactos[].nombre` | texto | sí |  |
| `[].contactos[].cargo` | texto o null | no |  |
| `[].contactos[].correo` | texto | sí |  |
| `[].contactos[].telefono` | texto o null | no |  |
| `[].contactos[].rol` | texto | sí |  |
| `[].vigenteHasta` | fecha o null | no |  |
| `[].enRevision` | booleano | no | por omisión false |
| `[].actualizacionNoAprobada` | ActualizacionNoAprobadaPublicaDto o null | no |  |
| `[].actualizacionNoAprobada.resultado` | texto | sí |  |
| `[].actualizacionNoAprobada.fecha` | fecha y hora o null | no |  |
| `[].actualizacionNoAprobada.motivos` | lista de MotivoPublicoDto | sí | mismos campos que `[].reintento.motivos[]` |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/vinculos" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
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
    "estado": "Invitado",
    "invitacionAceptada": true,
    "invitacionAceptadaEn": "2026-11-12T15:00:00Z",
    "expedienteId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "reintento": {
      "rechazadoEn": "2026-11-12T15:00:00Z",
      "motivos": [
        {
          "codigo": "CODIGO_EJEMPLO",
          "texto": "Texto de ejemplo."
        }
      ],
      "permitido": true,
      "disponible": true,
      "desde": "2026-11-12T15:00:00Z"
    },
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
      "resultado": "texto",
      "fecha": "2026-11-12T15:00:00Z",
      "motivos": [
        {
          "codigo": "CODIGO_EJEMPLO",
          "texto": "Texto de ejemplo."
        }
      ]
    }
  }
]
```

</details>

## Sesión pública

### `POST /public/v1/sesion/cerrar`

**Cerrar sesión tercero.** 204: revoca la familia de la cookie (si la hay) y la borra. Idempotente.

**Pública (sin sesión) · Portal del tercero (`/public/v1`) · SDK: `cerrarSesionTercero`**

**Respuestas**

- `204` Sin contenido

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/sesion/cerrar"
```

</details>

### `POST /public/v1/sesion/renovar`

**Renovar sesión tercero.** 200 con un `SesionTerceroDto` nuevo (vínculos recalculados, `otpVerificado` conservado) y rota la cookie. Sin cookie, vencida o desconocida ⇒ 401 `sesion-expirada`; token ya rotado ⇒ revoca la familia y 401 `sesion-revocada`; dos renovaciones simultáneas con el mismo token ⇒ una gana y la otra recibe 409 `renovacion-concurrente` sin revocar (su navegador ya recibió la cookie nueva).

**Pública (sin sesión) · Portal del tercero (`/public/v1`) · SDK: `renovarSesionTercero`**

**Respuestas**

- `200` OK — SesionTerceroDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `SesionTerceroDto` que se detallan en `POST /public/v1/enlaces/{token}/canjear`.

**Errores de negocio que lanza**

- `401` `sesion-revocada`
- `409` `renovacion-concurrente`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/public/v1/sesion/renovar"
```

Respuesta `200`:

```json
{
  "accessToken": "vsk_prueba_ejemplo",
  "expira": "2026-11-12T15:00:00Z",
  "terceroId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "otpVerificado": true,
  "vinculos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "empresa": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "nombre": "Constructora Norte S.A.S.",
        "logoUrl": "https://ejemplo.co/recurso",
        "colorMarca": "texto",
        "contactoSoporte": "texto"
      },
      "estado": "Invitado"
    }
  ],
  "expedienteOrigen": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "vinculoOrigen": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

</details>
