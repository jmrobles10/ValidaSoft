# API · Identidad

59 operaciones en 13 recursos.

## Gestión de ingreso

Gestión del ingreso de las personas del grupo por sus administradores (ING-5, `usuarios:administrar`, solo con un rol de todo el grupo): ver y cerrar sus sesiones, ver y revocar sus passkeys (la recuperación cuando pierden una), invitarlas a entrar y configurar los modos de ingreso. Cada acción queda en la auditoría de identidad.

### `GET /api/v1/configuracion/ingreso`

**Obtener configuración ingreso.** Modos de ingreso, duración e inactividad de la sesión directa y passkey para todos.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `obtenerConfiguracionIngreso`**

**Respuestas**

- `200` OK — ConfiguracionDeIngresoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `modos` | lista de texto | sí |  |
| `sesionHoras` | entero | sí |  |
| `inactividadMinutos` | entero | sí |  |
| `passkeyParaTodos` | booleano | sí |  |

**Errores de negocio que lanza**

- `404` `tenant-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/configuracion/ingreso" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "modos": [
    "texto"
  ],
  "sesionHoras": 1,
  "inactividadMinutos": 1,
  "passkeyParaTodos": true
}
```

</details>

### `PUT /api/v1/configuracion/ingreso`

**Actualizar configuración ingreso.** Fija la política de ingreso (auditada). Al habilitar el modo directo se completa el grupo de los usuarios anteriores; al apagarlo se cierran las sesiones directas abiertas. 422 `modos-de-ingreso-invalidos`, `sesion-invalida`, `inactividad-invalida`.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `actualizarConfiguracionIngreso`**

**Cuerpo de la petición** (`application/json`, ConfiguracionDeIngresoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `modos` | lista de texto | sí |  |
| `sesionHoras` | entero | sí |  |
| `inactividadMinutos` | entero | sí |  |
| `passkeyParaTodos` | booleano | sí |  |

**Respuestas**

- `200` OK — ConfiguracionDeIngresoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `modos` | lista de texto | sí |  |
| `sesionHoras` | entero | sí |  |
| `inactividadMinutos` | entero | sí |  |
| `passkeyParaTodos` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/configuracion/ingreso" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"modos":["texto"],"sesionHoras":1,"inactividadMinutos":1,"passkeyParaTodos":true}'
```

Respuesta `200`:

```json
{
  "modos": [
    "texto"
  ],
  "sesionHoras": 1,
  "inactividadMinutos": 1,
  "passkeyParaTodos": true
}
```

</details>

### `PUT /api/v1/plataforma/tenants/{id}/ingreso`

**Actualizar ingreso grupo.** La misma política desde la consola de Plataforma (`plataforma:tenants`).

**Operador de Plataforma · API (`/api/v1`) · SDK: `actualizarIngresoGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ConfiguracionDeIngresoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `modos` | lista de texto | sí |  |
| `sesionHoras` | entero | sí |  |
| `inactividadMinutos` | entero | sí |  |
| `passkeyParaTodos` | booleano | sí |  |

**Respuestas**

- `200` OK — ConfiguracionDeIngresoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `modos` | lista de texto | sí |  |
| `sesionHoras` | entero | sí |  |
| `inactividadMinutos` | entero | sí |  |
| `passkeyParaTodos` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/ingreso" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Content-Type: application/json" \
  -d '{"modos":["texto"],"sesionHoras":1,"inactividadMinutos":1,"passkeyParaTodos":true}'
```

Respuesta `200`:

```json
{
  "modos": [
    "texto"
  ],
  "sesionHoras": 1,
  "inactividadMinutos": 1,
  "passkeyParaTodos": true
}
```

</details>

### `POST /api/v1/usuarios/{id}/invitar`

**Invitar usuario.** Invita (o vuelve a invitar) a la persona a entrar directo: un correo con la dirección de la app, donde pide un código con su correo. 409 `ingreso-directo-apagado` si el grupo no admite el modo directo; 409 `usuario-inactivo`.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `invitarUsuario`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — UsuarioDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `nombre` | texto | sí |  |
| `correo` | texto | sí |  |
| `identificacion` | IdentificacionDto o null | no |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `activo` | booleano | sí |  |
| `identidades` | lista de IdentidadExternaDto | sí |  |
| `identidades[].sistemaId` | uuid | sí |  |
| `identidades[].idExterno` | texto | sí |  |
| `roles` | lista de RolAsignadoDto | sí |  |
| `roles[].rol` | Rol (texto) | sí | valores: Administrador · Revisor · OficialCumplimiento · Auditor |
| `roles[].empresaId` | uuid o null | no |  |
| `creadoEn` | fecha y hora | sí |  |
| `ultimoAcceso` | fecha y hora o null | no |  |
| `exigePasskey` | booleano | no | por omisión false |
| `passkeys` | entero | no | por omisión 0 |
| `invitadoEn` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `404` `tenant-no-encontrado`
- `404` `usuario-no-encontrado`
- `409` `ingreso-directo-apagado`
- `409` `usuario-inactivo`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/usuarios/3fa85f64-5717-4562-b3fc-2c963f66afa6/invitar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "correo": "contacto@empresa.co",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "activo": true,
  "identidades": [
    {
      "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "idExterno": "REF-0001"
    }
  ],
  "roles": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "creadoEn": "2026-11-12T15:00:00Z",
  "ultimoAcceso": "2026-11-12T15:00:00Z",
  "exigePasskey": false,
  "passkeys": 0,
  "invitadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `GET /api/v1/usuarios/{id}/passkeys`

**Listar passkeys usuario.** Las passkeys de la persona (activas y revocadas), de la más antigua a la más nueva.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `listarPasskeysUsuario`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — lista de PasskeyDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de PasskeyDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].nombre` | texto | sí |  |
| `[].creadaEn` | fecha y hora | sí |  |
| `[].ultimoUso` | fecha y hora o null | no |  |
| `[].sincronizada` | booleano | sí |  |
| `[].transportes` | lista de texto | sí |  |
| `[].activa` | booleano | sí |  |
| `[].revocadaEn` | fecha y hora o null | no |  |
| `[].revocadaPor` | texto o null | no |  |
| `[].motivoRevocacion` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/usuarios/3fa85f64-5717-4562-b3fc-2c963f66afa6/passkeys" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S.",
    "creadaEn": "2026-11-12T15:00:00Z",
    "ultimoUso": "2026-11-12T15:00:00Z",
    "sincronizada": true,
    "transportes": [
      "texto"
    ],
    "activa": true,
    "revocadaEn": "2026-11-12T15:00:00Z",
    "revocadaPor": "texto",
    "motivoRevocacion": "Texto de ejemplo."
  }
]
```

</details>

### `POST /api/v1/usuarios/{id}/passkeys/{passkeyId}/revocar`

**Revocar passkey usuario.** Revoca una passkey de la persona (la perdió o se la robaron): deja de valer de inmediato y se cierran sus sesiones; para volver a entrar usa el código del correo y registra otra. 409 `passkey-revocada` si ya lo estaba.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `revocarPasskeyUsuario`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `passkeyId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, RevocarPasskeyDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `motivo` | texto o null | no |  |

**Respuestas**

- `200` OK — PasskeyDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `nombre` | texto | sí |  |
| `creadaEn` | fecha y hora | sí |  |
| `ultimoUso` | fecha y hora o null | no |  |
| `sincronizada` | booleano | sí |  |
| `transportes` | lista de texto | sí |  |
| `activa` | booleano | sí |  |
| `revocadaEn` | fecha y hora o null | no |  |
| `revocadaPor` | texto o null | no |  |
| `motivoRevocacion` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/usuarios/3fa85f64-5717-4562-b3fc-2c963f66afa6/passkeys/3fa85f64-5717-4562-b3fc-2c963f66afa6/revocar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"motivo":"Texto de ejemplo."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "creadaEn": "2026-11-12T15:00:00Z",
  "ultimoUso": "2026-11-12T15:00:00Z",
  "sincronizada": true,
  "transportes": [
    "texto"
  ],
  "activa": true,
  "revocadaEn": "2026-11-12T15:00:00Z",
  "revocadaPor": "texto",
  "motivoRevocacion": "Texto de ejemplo."
}
```

</details>

### `GET /api/v1/usuarios/{id}/sesiones`

**Listar sesiones usuario.**

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `listarSesionesUsuario`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — lista de SesionDeUsuarioDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de SesionDeUsuarioDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].metodo` | texto | sí |  |
| `[].creadaEn` | fecha y hora | sí |  |
| `[].ultimaActividad` | fecha y hora | sí |  |
| `[].expiraEn` | fecha y hora | sí |  |
| `[].ip` | texto o null | no |  |
| `[].dispositivo` | texto o null | no |  |
| `[].actual` | booleano | sí |  |
| `[].activa` | booleano | sí |  |
| `[].revocadaEn` | fecha y hora o null | no |  |
| `[].motivoRevocacion` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/usuarios/3fa85f64-5717-4562-b3fc-2c963f66afa6/sesiones" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "metodo": "texto",
    "creadaEn": "2026-11-12T15:00:00Z",
    "ultimaActividad": "2026-11-12T15:00:00Z",
    "expiraEn": "2026-11-12T15:00:00Z",
    "ip": "texto",
    "dispositivo": "texto",
    "actual": true,
    "activa": true,
    "revocadaEn": "2026-11-12T15:00:00Z",
    "motivoRevocacion": "Texto de ejemplo."
  }
]
```

</details>

### `POST /api/v1/usuarios/{id}/sesiones/{sesionId}/cerrar`

**Cerrar sesión usuario.** Cierra una sesión de la persona (p. ej. de un dispositivo perdido).

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `cerrarSesionUsuario`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |
| `sesionId` | path | uuid | sí |  |

**Respuestas**

- `204` Sin contenido

**Errores de negocio que lanza**

- `404` `sesion-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/usuarios/3fa85f64-5717-4562-b3fc-2c963f66afa6/sesiones/3fa85f64-5717-4562-b3fc-2c963f66afa6/cerrar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

</details>

## Ingreso

Ingreso directo sin contraseñas (ING-1…ING-4, docs/05 §2.10): anónimo, con el límite de peticiones por IP de `/api/v1/ingreso` y el de códigos por correo. Ninguna respuesta revela si un correo tiene cuenta. El token de refresco viaja solo en la cookie `vs_usuario`; el de acceso, en el cuerpo (en memoria del navegador).

### `POST /api/v1/ingreso/codigo`

**Solicitar codigo ingreso.** Pide un código de 6 dígitos al correo (vence en 5 minutos, 5 por correo y hora). Responde siempre igual, tenga o no cuenta con ingreso directo ese correo; el desafío que devuelve es la capacidad para verificar. 422 `correo-invalido` solo por el formato.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `solicitarCodigoIngreso`**

**Cuerpo de la petición** (`application/json`, SolicitarCodigoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | texto | sí |  |

**Respuestas**

- `200` OK — CodigoSolicitadoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `expira` | fecha y hora | sí |  |
| `mensaje` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/codigo" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"correo":"contacto@empresa.co"}'
```

Respuesta `200`:

```json
{
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "expira": "2026-11-12T15:00:00Z",
  "mensaje": "Texto de ejemplo."
}
```

</details>

### `POST /api/v1/ingreso/codigo/verificar`

**Verificar codigo ingreso.** Verifica el código. Siguiente paso en `estado`: `ElegirCuenta`, `PasskeyRequerida`, `RegistrarPasskey` o `Sesion`. 422 `codigo-invalido` (con `intentosRestantes`), 410 `codigo-vencido`, 429 `codigo-bloqueado`, 404 `desafio-no-encontrado`.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `verificarCodigoIngreso`**

**Cuerpo de la petición** (`application/json`, VerificarCodigoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `codigo` | texto | sí |  |

**Respuestas**

- `200` OK — PasoDeIngresoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `estado` | texto | sí |  |
| `desafio` | uuid | sí |  |
| `cuentas` | lista de CuentaDeIngresoDto o null | no |  |
| `cuentas[].id` | texto | sí |  |
| `cuentas[].tipo` | texto | sí |  |
| `cuentas[].nombre` | texto | sí |  |
| `cuentas[].grupo` | texto o null | no |  |
| `cuentas[].exigePasskey` | booleano | sí |  |
| `cuentas[].tienePasskey` | booleano | sí |  |
| `opciones` | cualquiera | no |  |
| `sesion` | SesionDirectaDto o null | no |  |
| `sesion.token` | texto | sí |  |
| `sesion.expira` | fecha y hora | sí |  |
| `sesion.sesionId` | uuid | sí |  |
| `sesion.metodo` | texto | sí |  |
| `sesion.sugerirPasskey` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/codigo/verificar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"desafio":"3fa85f64-5717-4562-b3fc-2c963f66afa6","codigo":"CODIGO_EJEMPLO"}'
```

Respuesta `200`:

```json
{
  "estado": "texto",
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cuentas": [
    {
      "id": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "grupo": "texto",
      "exigePasskey": true,
      "tienePasskey": true
    }
  ],
  "opciones": null,
  "sesion": {
    "token": "vsk_prueba_ejemplo",
    "expira": "2026-11-12T15:00:00Z",
    "sesionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "metodo": "texto",
    "sugerirPasskey": true
  }
}
```

</details>

### `POST /api/v1/ingreso/cuenta`

**Elegir cuenta ingreso.** Con varias cuentas para el mismo correo (grupos distintos, operador de Plataforma), elige con cuál continúa el ingreso.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `elegirCuentaIngreso`**

**Cuerpo de la petición** (`application/json`, ElegirCuentaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `cuenta` | texto | sí |  |

**Respuestas**

- `200` OK — PasoDeIngresoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `PasoDeIngresoDto` que se detallan en `POST /api/v1/ingreso/codigo/verificar`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/cuenta" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"desafio":"3fa85f64-5717-4562-b3fc-2c963f66afa6","cuenta":"texto"}'
```

Respuesta `200`:

```json
{
  "estado": "texto",
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cuentas": [
    {
      "id": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "grupo": "texto",
      "exigePasskey": true,
      "tienePasskey": true
    }
  ],
  "opciones": null,
  "sesion": {
    "token": "vsk_prueba_ejemplo",
    "expira": "2026-11-12T15:00:00Z",
    "sesionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "metodo": "texto",
    "sugerirPasskey": true
  }
}
```

</details>

### `POST /api/v1/ingreso/passkey/opciones`

**Obtener opciones passkey ingreso.** Opciones de WebAuthn para entrar solo con una passkey (sin correo ni código): el navegador ofrece las que tenga para este sitio.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `obtenerOpcionesPasskeyIngreso`**

**Respuestas**

- `200` OK — OpcionesDePasskeyDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `opciones` | JsonElement | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/passkey/opciones" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "opciones": {}
}
```

</details>

### `POST /api/v1/ingreso/passkey/registro`

**Registrar passkey ingreso.** Registra la primera passkey de una cuenta que la exige (alta o recuperación) en el mismo ingreso y abre la sesión. 401 `passkey-invalida`; 409 `passkey-duplicada`.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `registrarPasskeyIngreso`**

**Cuerpo de la petición** (`application/json`, RegistrarPasskeyDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `respuesta` | JsonElement | sí |  |
| `nombre` | texto o null | no |  |

**Respuestas**

- `200` OK — PasoDeIngresoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `PasoDeIngresoDto` que se detallan en `POST /api/v1/ingreso/codigo/verificar`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/passkey/registro" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"desafio":"3fa85f64-5717-4562-b3fc-2c963f66afa6","respuesta":{},"nombre":"Constructora Norte S.A.S."}'
```

Respuesta `200`:

```json
{
  "estado": "texto",
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cuentas": [
    {
      "id": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "grupo": "texto",
      "exigePasskey": true,
      "tienePasskey": true
    }
  ],
  "opciones": null,
  "sesion": {
    "token": "vsk_prueba_ejemplo",
    "expira": "2026-11-12T15:00:00Z",
    "sesionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "metodo": "texto",
    "sugerirPasskey": true
  }
}
```

</details>

### `POST /api/v1/ingreso/passkey/verificar`

**Verificar passkey ingreso.** Verifica la passkey (ingreso solo con passkey, o el segundo paso tras el código) y abre la sesión. 401 `passkey-invalida`, `passkey-revocada` o `passkey-clonada`; 403 `cuenta-no-disponible`.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `verificarPasskeyIngreso`**

**Cuerpo de la petición** (`application/json`, VerificarPasskeyDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `respuesta` | JsonElement | sí |  |

**Respuestas**

- `200` OK — PasoDeIngresoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `PasoDeIngresoDto` que se detallan en `POST /api/v1/ingreso/codigo/verificar`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/passkey/verificar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"desafio":"3fa85f64-5717-4562-b3fc-2c963f66afa6","respuesta":{}}'
```

Respuesta `200`:

```json
{
  "estado": "texto",
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cuentas": [
    {
      "id": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "grupo": "texto",
      "exigePasskey": true,
      "tienePasskey": true
    }
  ],
  "opciones": null,
  "sesion": {
    "token": "vsk_prueba_ejemplo",
    "expira": "2026-11-12T15:00:00Z",
    "sesionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "metodo": "texto",
    "sugerirPasskey": true
  }
}
```

</details>

### `POST /api/v1/ingreso/recuperacion`

**Usar codigo recuperacion.** Código de recuperación (PI-18): con el código del correo ya verificado y la cuenta elegida, la persona que perdió su passkey usa uno de sus códigos de un solo uso y sigue el registro de una passkey nueva (`RegistrarPasskey`). 422 `codigo-de-recuperacion-invalido` (con `intentosRestantes`); 429 `recuperacion-bloqueada`.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `usarCodigoRecuperacion`**

**Cuerpo de la petición** (`application/json`, UsarCodigoDeRecuperacionDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `codigo` | texto | sí |  |

**Respuestas**

- `200` OK — PasoDeIngresoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `PasoDeIngresoDto` que se detallan en `POST /api/v1/ingreso/codigo/verificar`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/recuperacion" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"desafio":"3fa85f64-5717-4562-b3fc-2c963f66afa6","codigo":"CODIGO_EJEMPLO"}'
```

Respuesta `200`:

```json
{
  "estado": "texto",
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cuentas": [
    {
      "id": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "grupo": "texto",
      "exigePasskey": true,
      "tienePasskey": true
    }
  ],
  "opciones": null,
  "sesion": {
    "token": "vsk_prueba_ejemplo",
    "expira": "2026-11-12T15:00:00Z",
    "sesionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "metodo": "texto",
    "sugerirPasskey": true
  }
}
```

</details>

### `POST /api/v1/ingreso/sesion/cerrar`

**Cerrar sesión ingreso.** 204: revoca la sesión de la cookie (si la hay) y la borra. Idempotente.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `cerrarSesionIngreso`**

**Respuestas**

- `204` Sin contenido

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/sesion/cerrar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

</details>

### `POST /api/v1/ingreso/sesion/renovar`

**Renovar sesión ingreso.** Renueva el token de acceso con la cookie `vs_usuario` y la rota. Exige `X-Validasoft-Cliente: web` (defensa CSRF además de `SameSite=Strict`). 401 `sin-sesion` (no hay cookie), `sesion-expirada`, `sesion-revocada` (también por reuso de una cookie ya rotada) o `sesion-inactiva`; 409 `renovacion-concurrente`.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `renovarSesionIngreso`**

**Respuestas**

- `200` OK — SesionDirectaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `token` | texto | sí |  |
| `expira` | fecha y hora | sí |  |
| `sesionId` | uuid | sí |  |
| `metodo` | texto | sí |  |
| `sugerirPasskey` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/sesion/renovar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "token": "vsk_prueba_ejemplo",
  "expira": "2026-11-12T15:00:00Z",
  "sesionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "metodo": "texto",
  "sugerirPasskey": true
}
```

</details>

## Mi cuenta

### `GET /api/v1/mi-cuenta`

**Obtener mi cuenta.**

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `obtenerMiCuenta`**

**Respuestas**

- `200` OK — MiCuentaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tipo` | texto | sí |  |
| `id` | texto | sí |  |
| `nombre` | texto | sí |  |
| `correo` | texto | sí |  |
| `grupo` | texto o null | no |  |
| `roles` | lista de RolAsignadoDto | sí |  |
| `roles[].rol` | Rol (texto) | sí | valores: Administrador · Revisor · OficialCumplimiento · Auditor |
| `roles[].empresaId` | uuid o null | no |  |
| `exigePasskey` | booleano | sí |  |
| `metodo` | texto | sí |  |
| `sesionId` | uuid | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/mi-cuenta" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
{
  "tipo": "texto",
  "id": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "correo": "contacto@empresa.co",
  "grupo": "texto",
  "roles": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "exigePasskey": true,
  "metodo": "texto",
  "sesionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

</details>

### `GET /api/v1/mi-cuenta/passkeys`

**Listar mis passkeys.** Mis passkeys activas, de la más antigua a la más nueva.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `listarMisPasskeys`**

**Respuestas**

- `200` OK — lista de PasskeyDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de PasskeyDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].nombre` | texto | sí |  |
| `[].creadaEn` | fecha y hora | sí |  |
| `[].ultimoUso` | fecha y hora o null | no |  |
| `[].sincronizada` | booleano | sí |  |
| `[].transportes` | lista de texto | sí |  |
| `[].activa` | booleano | sí |  |
| `[].revocadaEn` | fecha y hora o null | no |  |
| `[].revocadaPor` | texto o null | no |  |
| `[].motivoRevocacion` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/mi-cuenta/passkeys" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S.",
    "creadaEn": "2026-11-12T15:00:00Z",
    "ultimoUso": "2026-11-12T15:00:00Z",
    "sincronizada": true,
    "transportes": [
      "texto"
    ],
    "activa": true,
    "revocadaEn": "2026-11-12T15:00:00Z",
    "revocadaPor": "texto",
    "motivoRevocacion": "Texto de ejemplo."
  }
]
```

</details>

### `POST /api/v1/mi-cuenta/passkeys`

**Registrar mi passkey.** Verifica la respuesta del navegador y guarda la passkey. 401 `passkey-invalida`; 409 `passkey-duplicada` o `passkeys-excesivas`.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `registrarMiPasskey`**

**Cuerpo de la petición** (`application/json`, RegistrarPasskeyDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `respuesta` | JsonElement | sí |  |
| `nombre` | texto o null | no |  |

**Respuestas**

- `200` OK — PasskeyDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `nombre` | texto | sí |  |
| `creadaEn` | fecha y hora | sí |  |
| `ultimoUso` | fecha y hora o null | no |  |
| `sincronizada` | booleano | sí |  |
| `transportes` | lista de texto | sí |  |
| `activa` | booleano | sí |  |
| `revocadaEn` | fecha y hora o null | no |  |
| `revocadaPor` | texto o null | no |  |
| `motivoRevocacion` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/mi-cuenta/passkeys" \
  -H "Authorization: Bearer $TOKEN_DE_SESION" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"desafio":"3fa85f64-5717-4562-b3fc-2c963f66afa6","respuesta":{},"nombre":"Constructora Norte S.A.S."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "creadaEn": "2026-11-12T15:00:00Z",
  "ultimoUso": "2026-11-12T15:00:00Z",
  "sincronizada": true,
  "transportes": [
    "texto"
  ],
  "activa": true,
  "revocadaEn": "2026-11-12T15:00:00Z",
  "revocadaPor": "texto",
  "motivoRevocacion": "Texto de ejemplo."
}
```

</details>

### `PUT /api/v1/mi-cuenta/passkeys/{id}`

**Renombrar mi passkey.**

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `renombrarMiPasskey`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, RenombrarPasskeyDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombre` | texto | sí |  |

**Respuestas**

- `200` OK — PasskeyDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `nombre` | texto | sí |  |
| `creadaEn` | fecha y hora | sí |  |
| `ultimoUso` | fecha y hora o null | no |  |
| `sincronizada` | booleano | sí |  |
| `transportes` | lista de texto | sí |  |
| `activa` | booleano | sí |  |
| `revocadaEn` | fecha y hora o null | no |  |
| `revocadaPor` | texto o null | no |  |
| `motivoRevocacion` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/mi-cuenta/passkeys/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer $TOKEN_DE_SESION" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Constructora Norte S.A.S."}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "creadaEn": "2026-11-12T15:00:00Z",
  "ultimoUso": "2026-11-12T15:00:00Z",
  "sincronizada": true,
  "transportes": [
    "texto"
  ],
  "activa": true,
  "revocadaEn": "2026-11-12T15:00:00Z",
  "revocadaPor": "texto",
  "motivoRevocacion": "Texto de ejemplo."
}
```

</details>

### `DELETE /api/v1/mi-cuenta/passkeys/{id}`

**Eliminar mi passkey.** Elimina (revoca) una de mis passkeys. Si mi cuenta exige passkey no puedo quedarme sin ninguna: 409 `passkey-obligatoria` (registra otra antes).

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `eliminarMiPasskey`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `204` Sin contenido

**Errores de negocio que lanza**

- `409` `passkey-obligatoria`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X DELETE "https://{host}/api/v1/mi-cuenta/passkeys/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

</details>

### `POST /api/v1/mi-cuenta/passkeys/opciones`

**Obtener opciones registro passkey.** Opciones de WebAuthn para registrar otra passkey (excluye las que ya tengo en ese dispositivo).

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `obtenerOpcionesRegistroPasskey`**

**Respuestas**

- `200` OK — OpcionesDePasskeyDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `opciones` | JsonElement | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/mi-cuenta/passkeys/opciones" \
  -H "Authorization: Bearer $TOKEN_DE_SESION" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "opciones": {}
}
```

</details>

### `GET /api/v1/mi-cuenta/sesiones`

**Listar mis sesiones.** Mis sesiones abiertas (la actual marcada), de la más reciente a la más antigua.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `listarMisSesiones`**

**Respuestas**

- `200` OK — lista de SesionDeUsuarioDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de SesionDeUsuarioDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].metodo` | texto | sí |  |
| `[].creadaEn` | fecha y hora | sí |  |
| `[].ultimaActividad` | fecha y hora | sí |  |
| `[].expiraEn` | fecha y hora | sí |  |
| `[].ip` | texto o null | no |  |
| `[].dispositivo` | texto o null | no |  |
| `[].actual` | booleano | sí |  |
| `[].activa` | booleano | sí |  |
| `[].revocadaEn` | fecha y hora o null | no |  |
| `[].motivoRevocacion` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/mi-cuenta/sesiones" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "metodo": "texto",
    "creadaEn": "2026-11-12T15:00:00Z",
    "ultimaActividad": "2026-11-12T15:00:00Z",
    "expiraEn": "2026-11-12T15:00:00Z",
    "ip": "texto",
    "dispositivo": "texto",
    "actual": true,
    "activa": true,
    "revocadaEn": "2026-11-12T15:00:00Z",
    "motivoRevocacion": "Texto de ejemplo."
  }
]
```

</details>

### `POST /api/v1/mi-cuenta/sesiones/{id}/cerrar`

**Cerrar mi sesión.** Cierra una de mis sesiones (en otro dispositivo, o esta misma).

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `cerrarMiSesion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `204` Sin contenido

**Errores de negocio que lanza**

- `404` `sesion-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/mi-cuenta/sesiones/3fa85f64-5717-4562-b3fc-2c963f66afa6/cerrar" \
  -H "Authorization: Bearer $TOKEN_DE_SESION" \
  -H "Idempotency-Key: $(uuidgen)"
```

</details>

## Mi cuenta seguridad

Seguridad de la cuenta de quien entró directo (Mi cuenta): reautenticación con la passkey para las acciones sensibles (step-up, PI-13) y códigos de recuperación de un solo uso que respaldan la passkey (PI-18). Solo con una sesión directa; con otra credencial, 403 `solo-ingreso-directo`.

### `GET /api/v1/mi-cuenta/codigos-de-recuperacion`

**Obtener estado codigos recuperacion.** Cuántos códigos de recuperación me quedan (nunca los códigos).

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `obtenerEstadoCodigosRecuperacion`**

**Respuestas**

- `200` OK — EstadoDeRecuperacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `disponibles` | entero | sí |  |
| `total` | entero | sí |  |
| `generadosEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/mi-cuenta/codigos-de-recuperacion" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
{
  "disponibles": 1,
  "total": 1,
  "generadosEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /api/v1/mi-cuenta/codigos-de-recuperacion`

**Generar codigos recuperacion.** Genera un juego nuevo de códigos de recuperación (los anteriores dejan de valer) y los muestra **una sola vez**. Exige reautenticar con la passkey (403 `reautenticacion-requerida`) y tener al menos una passkey (409 `sin-passkey`); avisa por correo.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `generarCodigosRecuperacion`**

**Respuestas**

- `200` OK — CodigosDeRecuperacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigos` | lista de texto | sí |  |
| `generadosEn` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/mi-cuenta/codigos-de-recuperacion" \
  -H "Authorization: Bearer $TOKEN_DE_SESION" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "codigos": [
    "codigo-de-ejemplo"
  ],
  "generadosEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `GET /api/v1/mi-cuenta/reautenticacion`

**Obtener estado reautenticacion.** Si la sesión está reautenticada ahora (y hasta cuándo): lo que la pantalla mira antes de ofrecer una acción sensible.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `obtenerEstadoReautenticacion`**

**Respuestas**

- `200` OK — EstadoDeReautenticacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `vigente` | booleano | sí |  |
| `hasta` | fecha y hora o null | no |  |
| `minutos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/mi-cuenta/reautenticacion" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
{
  "vigente": true,
  "hasta": "2026-11-12T15:00:00Z",
  "minutos": 1
}
```

</details>

### `POST /api/v1/mi-cuenta/reautenticacion`

**Confirmar reautenticacion.** Verifica la passkey y deja la sesión reautenticada unos minutos: las acciones sensibles (rotar llaves, cambiar roles, desactivar…) las aceptan hasta entonces. 401 `passkey-invalida`, `passkey-revocada`, `passkey-clonada`; 410 `desafio-usado`.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `confirmarReautenticacion`**

**Cuerpo de la petición** (`application/json`, VerificarPasskeyDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `respuesta` | JsonElement | sí |  |

**Respuestas**

- `200` OK — ReautenticacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `hasta` | fecha y hora | sí |  |
| `minutos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/mi-cuenta/reautenticacion" \
  -H "Authorization: Bearer $TOKEN_DE_SESION" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"desafio":"3fa85f64-5717-4562-b3fc-2c963f66afa6","respuesta":{}}'
```

Respuesta `200`:

```json
{
  "hasta": "2026-11-12T15:00:00Z",
  "minutos": 1
}
```

</details>

### `POST /api/v1/mi-cuenta/reautenticacion/opciones`

**Obtener opciones reautenticacion.** Opciones de WebAuthn para confirmar con una de mis passkeys. 409 `sin-passkey` si no tengo ninguna.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `obtenerOpcionesReautenticacion`**

**Respuestas**

- `200` OK — OpcionesDePasskeyDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `opciones` | JsonElement | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/mi-cuenta/reautenticacion/opciones" \
  -H "Authorization: Bearer $TOKEN_DE_SESION" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "opciones": {}
}
```

</details>

## Orígenes embebido

Lo que la app embebida consulta en tiempo de ejecución, en lugar de una lista fija al compilar (SE-2).

### `GET /public/v1/embed/origenes-permitidos`

**Listar orígenes embebido.** Público y sin credencial (lo pide el iframe antes de tener sesión). Devuelve exactamente lo que ya publica la cabecera `frame-ancestors`: orígenes sin tenant, sistema ni nombre, así que no revela nada nuevo. Pasa por el límite por IP de `/public/v1`.

**Pública (sin sesión) · Portal del tercero (`/public/v1`) · SDK: `listarOrigenesEmbebido`**

**Respuestas**

- `200` OK — OrigenesEmbebidoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `origenes` | lista de texto | sí |  |
| `vigenciaSegundos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/embed/origenes-permitidos"
```

Respuesta `200`:

```json
{
  "origenes": [
    "texto"
  ],
  "vigenciaSegundos": 1
}
```

</details>

## Quien soy

Lecturas de identidad para las pantallas (RQ-9): quién hace la petición (`GET /yo`) y el nombre visible de los usuarios que aparecen en expedientes y bandejas (`GET /usuarios/resumen`), sin exponer correos ni roles.

### `GET /api/v1/usuarios/resumen`

**Resumir usuarios.** `ids` separados por coma (hasta `MaximoIds`): `{ id, nombre }` de los usuarios del tenant que existan (activos o no: pueden haber decidido antes); los demás se omiten. Para cualquier credencial con `expedientes:leer` o `vinculos:leer` (si no, 403 `permiso-insuficiente`).

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `resumirUsuarios`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `ids` | query | texto | no |  |

**Respuestas**

- `200` OK — lista de UsuarioResumenDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de UsuarioResumenDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].nombre` | texto | sí |  |

**Errores de negocio que lanza**

- `403` `permiso-insuficiente`
- `422` `ids-invalidos`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/usuarios/resumen" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S."
  }
]
```

</details>

### `GET /api/v1/yo`

**Obtener yo.** Actor de la petición, su usuario (si es una persona: identidad delegada o sesión de Validasoft), sus roles en las empresas a su alcance, los permisos efectivos en la empresa activa (`X-Empresa`), el sistema integrado de la credencial y las empresas que puede usar. Cualquier credencial de un tenant.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `obtenerYo`**

**Respuestas**

- `200` OK — YoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `actorTipo` | texto | sí |  |
| `actorId` | texto | sí |  |
| `tenantId` | uuid o null | no |  |
| `usuario` | UsuarioResumenDto o null | no |  |
| `usuario.id` | uuid | sí |  |
| `usuario.nombre` | texto | sí |  |
| `roles` | lista de RolAsignadoDto | sí |  |
| `roles[].rol` | Rol (texto) | sí | valores: Administrador · Revisor · OficialCumplimiento · Auditor |
| `roles[].empresaId` | uuid o null | no |  |
| `permisos` | lista de texto | sí |  |
| `sistema` | texto o null | no |  |
| `empresas` | lista de EmpresaResumenDto | sí |  |
| `empresas[].id` | uuid | sí |  |
| `empresas[].nombre` | texto | sí |  |
| `empresaActiva` | uuid o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/yo" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
{
  "actorTipo": "texto",
  "actorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "usuario": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S."
  },
  "roles": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "permisos": [
    "texto"
  ],
  "sistema": "texto",
  "empresas": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S."
    }
  ],
  "empresaActiva": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
}
```

</details>

## Sesión actual del tercero

Lo que la app del tercero necesita al arrancar con un token que le entregaron (no con la cookie): quién es y qué vínculos ve.

### `GET /public/v1/sesion/actual`

**Obtener sesión actual.** La sesión del token recibido (los vínculos con su empresa y estado). La usa la app embebida tras recibir el token por `postMessage`.

**Sesión del tercero · Portal del tercero (`/public/v1`) · SDK: `obtenerSesionActual`**

**Respuestas**

- `200` OK — SesionTerceroDto
- `404` No encontrado

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

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/public/v1/sesion/actual" \
  -H "Authorization: Bearer $TOKEN_DE_LA_SESION_DEL_TERCERO"
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

## Sesiones tercero embebidas

### `POST /api/v1/sesiones-tercero`

**Crear sesión tercero.** Abre la sesión (201). 403 `sistema-requerido` (la llave no es de un sistema integrador), `sistema-inactivo`, `origen-no-permitido` (el origen no está entre los del sistema) y `vinculo-ajeno` (el vínculo no lleva la referencia del sistema); 404 si el vínculo no es de la empresa; 409 `invitacion-no-aceptada` y `vinculo-anulado`; 422 `componente-invalido`, `vigencia-invalida` y `origen-invalido`.

**Permiso sesiones-tercero:crear · Empresa activa (`X-Empresa`) · API (`/api/v1`) · SDK: `crearSesionTercero`**

**Cuerpo de la petición** (`application/json`, CrearSesionEmbebidaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `vinculoId` | uuid | sí |  |
| `componente` | texto | sí |  |
| `origen` | texto | sí |  |
| `minutos` | entero o null | no |  |

**Respuestas**

- `200` OK — el contrato publica esta respuesta sin esquema (`IResult`)
- `404` No encontrado

**Errores de negocio que lanza**

- `403` `origen-no-permitido`
- `403` `sistema-inactivo`
- `403` `sistema-requerido`
- `403` `vinculo-ajeno`
- `409` `invitacion-no-aceptada`
- `409` `vinculo-anulado`
- `422` `componente-invalido`
- `422` `vigencia-invalida`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/sesiones-tercero" \
  -H "X-Api-Key: $API_KEY" \
  -H "X-Empresa: $EMPRESA_ID" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"vinculoId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","componente":"texto","origen":"texto","minutos":1}'
```

</details>

## Sesiones usuario

Sesiones cortas para las pantallas embebidas en los sistemas integrados (ID-3).

### `POST /api/v1/sesiones-usuario`

**Crear sesión usuario.** Canjea la identidad delegada (API key + `X-Usuario-Delegado`) por un token de Validasoft de 30 minutos con el permiso efectivo de esa persona; el iframe o Web Component lo envía como `Authorization: Bearer`.

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `crearSesionUsuario`**

**Respuestas**

- `200` OK — SesionUsuarioDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `token` | texto | sí |  |
| `expira` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/sesiones-usuario" \
  -H "Authorization: Bearer $TOKEN_DE_SESION" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "token": "vsk_prueba_ejemplo",
  "expira": "2026-11-12T15:00:00Z"
}
```

</details>

## Sistemas

### `GET /api/v1/sistemas`

**Listar sistemas.**

**Permiso sistemas:administrar · API (`/api/v1`) · SDK: `listarSistemas`**

**Respuestas**

- `200` OK — lista de SistemaIntegradoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de SistemaIntegradoDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].codigo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].clavePublicaPem` | texto | sí |  |
| `[].activo` | booleano | sí |  |
| `[].creadoEn` | fecha y hora | sí |  |
| `[].origenesPermitidos` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/sistemas" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "clavePublicaPem": "texto",
    "activo": true,
    "creadoEn": "2026-11-12T15:00:00Z",
    "origenesPermitidos": [
      "texto"
    ]
  }
]
```

</details>

### `POST /api/v1/sistemas`

**Crear sistema.**

**Permiso sistemas:administrar · API (`/api/v1`) · SDK: `crearSistema`**

**Cuerpo de la petición** (`application/json`, CrearSistemaIntegradoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `origenesPermitidos` | lista de texto o null | no |  |

**Respuestas**

- `201` Creado — SistemaIntegradoDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `origenesPermitidos` | lista de texto | sí |  |

**Errores de negocio que lanza**

- `409` `sistema-duplicado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/sistemas" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S.","clavePublicaPem":"texto","origenesPermitidos":["texto"]}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "clavePublicaPem": "texto",
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "origenesPermitidos": [
    "texto"
  ]
}
```

</details>

### `GET /api/v1/sistemas/{id}`

**Obtener sistema.**

**Permiso sistemas:administrar · API (`/api/v1`) · SDK: `obtenerSistema`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — SistemaIntegradoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `origenesPermitidos` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/sistemas/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "clavePublicaPem": "texto",
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "origenesPermitidos": [
    "texto"
  ]
}
```

</details>

### `PUT /api/v1/sistemas/{id}/clave-publica`

**Rotar clave pública sistema.** Rotación de la clave pública: desde ya, solo valen las aserciones firmadas con la nueva.

**Permiso sistemas:administrar · API (`/api/v1`) · SDK: `rotarClavePublicaSistema`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, RotarClavePublicaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `clavePublicaPem` | texto | sí |  |

**Respuestas**

- `200` OK — SistemaIntegradoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `origenesPermitidos` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/sistemas/3fa85f64-5717-4562-b3fc-2c963f66afa6/clave-publica" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"clavePublicaPem":"texto"}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "clavePublicaPem": "texto",
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "origenesPermitidos": [
    "texto"
  ]
}
```

</details>

### `POST /api/v1/sistemas/{id}/desactivar`

**Desactivar sistema.** Desactiva el sistema (PI-7): **se revocan todas sus API keys** (también las de prueba), sus sesiones —las de identidad delegada y las embebidas del tercero— dejan de valer en segundos en todos los servidores, sus aserciones se rechazan y sus orígenes ya no pueden embeber. Reactivarlo no restaura las llaves revocadas: hay que emitir otras. Exige reautenticar con la passkey a quien entró directo (403 `reautenticacion-requerida`).

**Permiso sistemas:administrar · API (`/api/v1`) · SDK: `desactivarSistema`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — SistemaIntegradoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `origenesPermitidos` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/sistemas/3fa85f64-5717-4562-b3fc-2c963f66afa6/desactivar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "clavePublicaPem": "texto",
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "origenesPermitidos": [
    "texto"
  ]
}
```

</details>

### `PUT /api/v1/sistemas/{id}/origenes`

**Actualizar orígenes sistema.** Reemplaza los orígenes desde los que el sistema aloja las pantallas embebidas (SE-1). Cambian `frame-ancestors` y `GET /public/v1/embed/origenes-permitidos` (caché de 60 s por nodo).

**Permiso sistemas:administrar · API (`/api/v1`) · SDK: `actualizarOrigenesSistema`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, FijarOrigenesDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `origenes` | lista de texto | sí |  |

**Respuestas**

- `200` OK — SistemaIntegradoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `origenesPermitidos` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/sistemas/3fa85f64-5717-4562-b3fc-2c963f66afa6/origenes" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"origenes":["texto"]}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "clavePublicaPem": "texto",
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "origenesPermitidos": [
    "texto"
  ]
}
```

</details>

### `POST /api/v1/sistemas/{id}/reactivar`

**Reactivar sistema.** Vuelve a activar el sistema (PI-7). Sus llaves se revocaron al desactivarlo y no se restauran: emite otras. 409 `sistema-activo` si ya estaba activo.

**Permiso sistemas:administrar · API (`/api/v1`) · SDK: `reactivarSistema`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — SistemaIntegradoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `origenesPermitidos` | lista de texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/sistemas/3fa85f64-5717-4562-b3fc-2c963f66afa6/reactivar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "clavePublicaPem": "texto",
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "origenesPermitidos": [
    "texto"
  ]
}
```

</details>

## SSO configuración

### `GET /api/v1/configuracion/sso`

**Obtener configuración SSO.**

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `obtenerConfiguracionSso`**

**Respuestas**

- `200` OK — ConfiguracionDeSsoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `configurado` | booleano | sí |  |
| `activo` | booleano | sí |  |
| `proveedor` | texto | sí |  |
| `nombre` | texto | sí |  |
| `autoridad` | texto o null | no |  |
| `clientId` | texto o null | no |  |
| `secretoConfigurado` | booleano | sí |  |
| `ultimosCuatroSecreto` | texto o null | no |  |
| `alcances` | lista de texto | sí |  |
| `reclamoDeGrupos` | texto | sí |  |
| `organizacionWorkOs` | texto o null | no |  |
| `conexionWorkOs` | texto o null | no |  |
| `dominios` | lista de DominioDeSsoDto | sí |  |
| `dominios[].dominio` | texto | sí |  |
| `dominios[].verificado` | booleano | sí |  |
| `dominios[].token` | texto o null | no |  |
| `dominios[].instruccion` | texto o null | no |  |
| `dominios[].creadoEn` | fecha y hora | sí |  |
| `dominios[].verificadoEn` | fecha y hora o null | no |  |
| `aprovisionamientoJit` | booleano | sí |  |
| `rolesPorOmision` | lista de RolAsignadoDto | sí |  |
| `rolesPorOmision[].rol` | Rol (texto) | sí | valores: Administrador · Revisor · OficialCumplimiento · Auditor |
| `rolesPorOmision[].empresaId` | uuid o null | no |  |
| `reglas` | lista de ReglaDeGrupoSsoDto | sí |  |
| `reglas[].grupo` | texto | sí |  |
| `reglas[].rol` | texto | sí |  |
| `reglas[].empresaId` | uuid o null | no |  |
| `sincronizarRoles` | booleano | sí |  |
| `modo` | texto | sí |  |
| `urlDeRetorno` | texto | sí |  |
| `workOsDisponible` | booleano | sí |  |
| `actualizadoEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/configuracion/sso" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "configurado": true,
  "activo": true,
  "proveedor": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "autoridad": "texto",
  "clientId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "secretoConfigurado": true,
  "ultimosCuatroSecreto": "vsk_prueba_ejemplo",
  "alcances": [
    "texto"
  ],
  "reclamoDeGrupos": "texto",
  "organizacionWorkOs": "texto",
  "conexionWorkOs": "texto",
  "dominios": [
    {
      "dominio": "texto",
      "verificado": true,
      "token": "vsk_prueba_ejemplo",
      "instruccion": "texto",
      "creadoEn": "2026-11-12T15:00:00Z",
      "verificadoEn": "2026-11-12T15:00:00Z"
    }
  ],
  "aprovisionamientoJit": true,
  "rolesPorOmision": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "reglas": [
    {
      "grupo": "texto",
      "rol": "texto",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "sincronizarRoles": true,
  "modo": "texto",
  "urlDeRetorno": "https://ejemplo.co/recurso",
  "workOsDisponible": true,
  "actualizadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `PUT /api/v1/configuracion/sso`

**Actualizar configuración SSO.** Crea o reemplaza el SSO del grupo. `secretoDelCliente` nulo conserva el actual y vacío lo quita; nunca vuelve. 409 `ingreso-directo-apagado` (el SSO abre sesiones directas), `workos-no-configurado`; 422 `proveedor-sso-invalido`, `autoridad-invalida`, `client-id-requerido`, `sso-sin-dominios`, `rol-por-omision-requerido`, `regla-sso-invalida`, `empresas-invalidas`…

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `actualizarConfiguracionSso`**

**Cuerpo de la petición** (`application/json`, FijarSsoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `activo` | booleano | sí |  |
| `proveedor` | texto | sí |  |
| `nombre` | texto | sí |  |
| `autoridad` | texto o null | no |  |
| `clientId` | texto o null | no |  |
| `secretoDelCliente` | texto o null | no |  |
| `alcances` | lista de texto o null | no |  |
| `reclamoDeGrupos` | texto o null | no |  |
| `organizacionWorkOs` | texto o null | no |  |
| `conexionWorkOs` | texto o null | no |  |
| `aprovisionamientoJit` | booleano | no | por omisión false |
| `rolesPorOmision` | lista de RolAsignadoDto o null | no |  |
| `rolesPorOmision[].rol` | Rol (texto) | sí | valores: Administrador · Revisor · OficialCumplimiento · Auditor |
| `rolesPorOmision[].empresaId` | uuid o null | no |  |
| `reglas` | lista de ReglaDeGrupoSsoDto o null | no |  |
| `reglas[].grupo` | texto | sí |  |
| `reglas[].rol` | texto | sí |  |
| `reglas[].empresaId` | uuid o null | no |  |
| `sincronizarRoles` | booleano | no | por omisión false |
| `modo` | texto | no | por omisión "Complementario" |

**Respuestas**

- `200` OK — ConfiguracionDeSsoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ConfiguracionDeSsoDto` que se detallan en `GET /api/v1/configuracion/sso`.

**Errores de negocio que lanza**

- `404` `tenant-no-encontrado`
- `409` `ingreso-directo-apagado`
- `409` `workos-no-configurado`
- `422` `empresas-invalidas`
- `422` `modo-sso-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/configuracion/sso" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"activo":true,"proveedor":"texto","nombre":"Constructora Norte S.A.S.","autoridad":"texto","clientId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","secretoDelCliente":"vsk_prueba_ejemplo","alcances":["texto"],"reclamoDeGrupos":"texto","organizacionWorkOs":"texto","conexionWorkOs":"texto","aprovisionamientoJit":false,"rolesPorOmision":[{"rol":"Administrador","empresaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}],"reglas":[{"grupo":"texto","rol":"texto","empresaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}],"sincronizarRoles":false,"modo":"Complementario"}'
```

Respuesta `200`:

```json
{
  "configurado": true,
  "activo": true,
  "proveedor": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "autoridad": "texto",
  "clientId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "secretoConfigurado": true,
  "ultimosCuatroSecreto": "vsk_prueba_ejemplo",
  "alcances": [
    "texto"
  ],
  "reclamoDeGrupos": "texto",
  "organizacionWorkOs": "texto",
  "conexionWorkOs": "texto",
  "dominios": [
    {
      "dominio": "texto",
      "verificado": true,
      "token": "vsk_prueba_ejemplo",
      "instruccion": "texto",
      "creadoEn": "2026-11-12T15:00:00Z",
      "verificadoEn": "2026-11-12T15:00:00Z"
    }
  ],
  "aprovisionamientoJit": true,
  "rolesPorOmision": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "reglas": [
    {
      "grupo": "texto",
      "rol": "texto",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "sincronizarRoles": true,
  "modo": "texto",
  "urlDeRetorno": "https://ejemplo.co/recurso",
  "workOsDisponible": true,
  "actualizadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /api/v1/configuracion/sso/dominios`

**Agregar dominio SSO.** Registra un dominio de correo del grupo y devuelve lo que hay que publicar para demostrar que es suyo. Hasta que se verifica no sirve para iniciar sesión. 422 `dominio-invalido`; 409 `dominio-publico` (gmail.com y similares no son de nadie).

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `agregarDominioSso`**

**Cuerpo de la petición** (`application/json`, AgregarDominioSsoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `dominio` | texto | sí |  |

**Respuestas**

- `200` OK — DominioDeSsoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `dominio` | texto | sí |  |
| `verificado` | booleano | sí |  |
| `token` | texto o null | no |  |
| `instruccion` | texto o null | no |  |
| `creadoEn` | fecha y hora | sí |  |
| `verificadoEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/configuracion/sso/dominios" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"dominio":"texto"}'
```

Respuesta `200`:

```json
{
  "dominio": "texto",
  "verificado": true,
  "token": "vsk_prueba_ejemplo",
  "instruccion": "texto",
  "creadoEn": "2026-11-12T15:00:00Z",
  "verificadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `DELETE /api/v1/configuracion/sso/dominios/{dominio}`

**Quitar dominio SSO.** Quita el dominio del grupo (y su verificación). Con el SSO activo no puede quedar sin ningún dominio verificado: 409 `sso-sin-dominios`.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `quitarDominioSso`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `dominio` | path | texto | sí |  |

**Respuestas**

- `204` Sin contenido

**Errores de negocio que lanza**

- `404` `dominio-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X DELETE "https://{host}/api/v1/configuracion/sso/dominios/texto" \
  -H "X-Api-Key: $API_KEY"
```

</details>

### `POST /api/v1/configuracion/sso/dominios/{dominio}/verificar`

**Verificar dominio SSO.** Comprueba que el grupo controla el dominio (busca el token en `https://{dominio}/.well-known/validasoft-verificacion.txt`) y lo da por verificado. Un dominio verificado es de un solo grupo. 409 `dominio-no-verificado` (aún no se ve el token), `dominio-en-uso` (otro grupo ya lo verificó); 404 `dominio-no-encontrado`.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `verificarDominioSso`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `dominio` | path | texto | sí |  |

**Respuestas**

- `200` OK — DominioDeSsoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `dominio` | texto | sí |  |
| `verificado` | booleano | sí |  |
| `token` | texto o null | no |  |
| `instruccion` | texto o null | no |  |
| `creadoEn` | fecha y hora | sí |  |
| `verificadoEn` | fecha y hora o null | no |  |

**Errores de negocio que lanza**

- `404` `dominio-no-encontrado`
- `409` `dominio-en-uso`
- `409` `dominio-no-verificado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/configuracion/sso/dominios/texto/verificar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "dominio": "texto",
  "verificado": true,
  "token": "vsk_prueba_ejemplo",
  "instruccion": "texto",
  "creadoEn": "2026-11-12T15:00:00Z",
  "verificadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /api/v1/configuracion/sso/probar`

**Probar conexion SSO.** «Probar conexión»: con lo ya guardado, comprueba que el proveedor responde y publica su descubrimiento con el emisor esperado. No inicia sesión ni cambia nada; responde siempre 200 con el resultado. 404 `sso-no-configurado`.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `probarConexionSso`**

**Respuestas**

- `200` OK — PruebaDeSsoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correcto` | booleano | sí |  |
| `mensaje` | texto | sí |  |
| `emisor` | texto o null | no |  |
| `puntoDeAutorizacion` | texto o null | no |  |

**Errores de negocio que lanza**

- `404` `sso-no-configurado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/configuracion/sso/probar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "correcto": true,
  "mensaje": "Texto de ejemplo.",
  "emisor": "texto",
  "puntoDeAutorizacion": "texto"
}
```

</details>

### `POST /api/v1/plataforma/tenants/{id}/sso/desactivar`

**Desactivar SSO grupo.** Desactiva el SSO de un grupo desde Plataforma (válvula de emergencia: si el proveedor del cliente no responde y su administrador no puede entrar).

**Operador de Plataforma · API (`/api/v1`) · SDK: `desactivarSsoGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — ConfiguracionDeSsoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `ConfiguracionDeSsoDto` que se detallan en `GET /api/v1/configuracion/sso`.

**Errores de negocio que lanza**

- `404` `sso-no-configurado`
- `409` `sso-inactivo`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/sso/desactivar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "configurado": true,
  "activo": true,
  "proveedor": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "autoridad": "texto",
  "clientId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "secretoConfigurado": true,
  "ultimosCuatroSecreto": "vsk_prueba_ejemplo",
  "alcances": [
    "texto"
  ],
  "reclamoDeGrupos": "texto",
  "organizacionWorkOs": "texto",
  "conexionWorkOs": "texto",
  "dominios": [
    {
      "dominio": "texto",
      "verificado": true,
      "token": "vsk_prueba_ejemplo",
      "instruccion": "texto",
      "creadoEn": "2026-11-12T15:00:00Z",
      "verificadoEn": "2026-11-12T15:00:00Z"
    }
  ],
  "aprovisionamientoJit": true,
  "rolesPorOmision": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "reglas": [
    {
      "grupo": "texto",
      "rol": "texto",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "sincronizarRoles": true,
  "modo": "texto",
  "urlDeRetorno": "https://ejemplo.co/recurso",
  "workOsDisponible": true,
  "actualizadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

## SSO ingreso

### `GET /api/v1/ingreso/sso/callback`

**Retorno SSO ingreso.** Regreso del proveedor (la URI de redirección que se registra en él): canjea el código, valida la identidad y redirige a la app —con `?sso={desafio}` si salió bien o `?sso_error={código}` si no—. Siempre responde 302 a la app.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `retornoSsoIngreso`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `code` | query | texto | no |  |
| `state` | query | texto | no |  |
| `error` | query | texto | no |  |

**Respuestas**

- `302` Redirección

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/ingreso/sso/callback" \
  -H "X-Api-Key: $API_KEY"
```

</details>

### `POST /api/v1/ingreso/sso/completar`

**Completar SSO ingreso.** Completa el ingreso con la identidad ya verificada: crea a la persona si el grupo aprovisiona (JIT), pone al día sus roles con sus grupos del proveedor y devuelve el siguiente paso, como el código del correo (`Sesion`, `PasskeyRequerida` o `RegistrarPasskey`). 403 `sso-sin-cuenta`, `cuenta-no-disponible`, `modo-de-ingreso-no-admitido`.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `completarSsoIngreso`**

**Cuerpo de la petición** (`application/json`, CompletarSsoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |

**Respuestas**

- `200` OK — PasoDeIngresoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `PasoDeIngresoDto` que se detallan en `POST /api/v1/ingreso/codigo/verificar`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/sso/completar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"desafio":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}'
```

Respuesta `200`:

```json
{
  "estado": "texto",
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "cuentas": [
    {
      "id": "texto",
      "tipo": "texto",
      "nombre": "Constructora Norte S.A.S.",
      "grupo": "texto",
      "exigePasskey": true,
      "tienePasskey": true
    }
  ],
  "opciones": null,
  "sesion": {
    "token": "vsk_prueba_ejemplo",
    "expira": "2026-11-12T15:00:00Z",
    "sesionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "metodo": "texto",
    "sugerirPasskey": true
  }
}
```

</details>

### `POST /api/v1/ingreso/sso/descubrir`

**Descubrir SSO ingreso.** Si el correo es de un dominio verificado con el SSO activo. Es lo único que dice: no revela si el correo tiene cuenta.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `descubrirSsoIngreso`**

**Cuerpo de la petición** (`application/json`, DescubrirSsoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | texto | sí |  |

**Respuestas**

- `200` OK — SsoDescubiertoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `disponible` | booleano | sí |  |
| `nombre` | texto o null | no |  |
| `reemplazaElCodigo` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/sso/descubrir" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"correo":"contacto@empresa.co"}'
```

Respuesta `200`:

```json
{
  "disponible": true,
  "nombre": "Constructora Norte S.A.S.",
  "reemplazaElCodigo": true
}
```

</details>

### `POST /api/v1/ingreso/sso/iniciar`

**Iniciar SSO ingreso.** Empieza el inicio de sesión de la empresa: responde la dirección del proveedor (con `state`, `nonce` y PKCE) y deja una cookie corta que liga el regreso a este navegador. 403 `sso-no-disponible` si el correo no es de un dominio con SSO; 503 si el proveedor no responde.

**Pública (sin sesión) · API (`/api/v1`) · SDK: `iniciarSsoIngreso`**

**Cuerpo de la petición** (`application/json`, IniciarSsoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `correo` | texto | sí |  |

**Respuestas**

- `200` OK — SsoIniciadoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `desafio` | uuid | sí |  |
| `urlDeAutorizacion` | texto | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/ingreso/sso/iniciar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"correo":"contacto@empresa.co"}'
```

Respuesta `200`:

```json
{
  "desafio": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "urlDeAutorizacion": "https://ejemplo.co/recurso"
}
```

</details>

## Usuarios

### `GET /api/v1/usuarios`

**Listar usuarios.**

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `listarUsuarios`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `correo` | query | texto | no |  |

**Respuestas**

- `200` OK — lista de UsuarioDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de UsuarioDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].nombre` | texto | sí |  |
| `[].correo` | texto | sí |  |
| `[].identificacion` | IdentificacionDto o null | no |  |
| `[].identificacion.pais` | texto | sí |  |
| `[].identificacion.tipo` | texto | sí |  |
| `[].identificacion.numero` | texto | sí |  |
| `[].identificacion.digitoVerificacion` | texto o null | no |  |
| `[].activo` | booleano | sí |  |
| `[].identidades` | lista de IdentidadExternaDto | sí |  |
| `[].identidades[].sistemaId` | uuid | sí |  |
| `[].identidades[].idExterno` | texto | sí |  |
| `[].roles` | lista de RolAsignadoDto | sí |  |
| `[].roles[].rol` | Rol (texto) | sí | valores: Administrador · Revisor · OficialCumplimiento · Auditor |
| `[].roles[].empresaId` | uuid o null | no |  |
| `[].creadoEn` | fecha y hora | sí |  |
| `[].ultimoAcceso` | fecha y hora o null | no |  |
| `[].exigePasskey` | booleano | no | por omisión false |
| `[].passkeys` | entero | no | por omisión 0 |
| `[].invitadoEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/usuarios" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "nombre": "Constructora Norte S.A.S.",
    "correo": "contacto@empresa.co",
    "identificacion": {
      "pais": "CO",
      "tipo": "texto",
      "numero": "900123456",
      "digitoVerificacion": "texto"
    },
    "activo": true,
    "identidades": [
      {
        "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "idExterno": "REF-0001"
      }
    ],
    "roles": [
      {
        "rol": "Administrador",
        "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
      }
    ],
    "creadoEn": "2026-11-12T15:00:00Z",
    "ultimoAcceso": "2026-11-12T15:00:00Z",
    "exigePasskey": false,
    "passkeys": 0,
    "invitadoEn": "2026-11-12T15:00:00Z"
  }
]
```

</details>

### `POST /api/v1/usuarios`

**Crear usuario.**

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `crearUsuario`**

**Cuerpo de la petición** (`application/json`, CrearUsuarioDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombre` | texto | sí |  |
| `correo` | texto | sí |  |
| `identificacion` | IdentificacionDto o null | no |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `roles` | lista de RolAsignadoDto o null | no |  |
| `roles[].rol` | Rol (texto) | sí | valores: Administrador · Revisor · OficialCumplimiento · Auditor |
| `roles[].empresaId` | uuid o null | no |  |
| `identidades` | lista de IdentidadExternaDto o null | no |  |
| `identidades[].sistemaId` | uuid | sí |  |
| `identidades[].idExterno` | texto | sí |  |
| `invitar` | booleano o null | no |  |

**Respuestas**

- `201` Creado — UsuarioDto

Campos de la respuesta `201`:

Mismos campos de `UsuarioDto` que se detallan en `POST /api/v1/usuarios/{id}/invitar`.

**Errores de negocio que lanza**

- `409` `identidad-duplicada`
- `409` `usuario-duplicado`
- `422` `ingreso-directo-apagado`
- `422` `sistema-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/usuarios" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Constructora Norte S.A.S.","correo":"contacto@empresa.co","identificacion":{"pais":"CO","tipo":"texto","numero":"900123456","digitoVerificacion":"texto"},"roles":[{"rol":"Administrador","empresaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}],"identidades":[{"sistemaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6","idExterno":"REF-0001"}],"invitar":true}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "correo": "contacto@empresa.co",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "activo": true,
  "identidades": [
    {
      "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "idExterno": "REF-0001"
    }
  ],
  "roles": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "creadoEn": "2026-11-12T15:00:00Z",
  "ultimoAcceso": "2026-11-12T15:00:00Z",
  "exigePasskey": false,
  "passkeys": 0,
  "invitadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `GET /api/v1/usuarios/{id}`

**Obtener usuario.**

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `obtenerUsuario`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — UsuarioDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `UsuarioDto` que se detallan en `POST /api/v1/usuarios/{id}/invitar`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/usuarios/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "correo": "contacto@empresa.co",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "activo": true,
  "identidades": [
    {
      "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "idExterno": "REF-0001"
    }
  ],
  "roles": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "creadoEn": "2026-11-12T15:00:00Z",
  "ultimoAcceso": "2026-11-12T15:00:00Z",
  "exigePasskey": false,
  "passkeys": 0,
  "invitadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /api/v1/usuarios/{id}/desactivar`

**Desactivar usuario.** Un usuario desactivado no puede actuar por delegación (401) aunque su sistema lo autentique.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `desactivarUsuario`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — UsuarioDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `UsuarioDto` que se detallan en `POST /api/v1/usuarios/{id}/invitar`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/usuarios/3fa85f64-5717-4562-b3fc-2c963f66afa6/desactivar" \
  -H "X-Api-Key: $API_KEY" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "correo": "contacto@empresa.co",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "activo": true,
  "identidades": [
    {
      "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "idExterno": "REF-0001"
    }
  ],
  "roles": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "creadoEn": "2026-11-12T15:00:00Z",
  "ultimoAcceso": "2026-11-12T15:00:00Z",
  "exigePasskey": false,
  "passkeys": 0,
  "invitadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `PUT /api/v1/usuarios/{id}/roles`

**Asignar roles usuario.** Reemplaza los roles del usuario. Cada empresa debe ser del tenant y estar al alcance de quien asigna.

**Permiso usuarios:administrar · API (`/api/v1`) · SDK: `asignarRolesUsuario`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, AsignarRolesDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `roles` | lista de RolAsignadoDto | sí |  |
| `roles[].rol` | Rol (texto) | sí | valores: Administrador · Revisor · OficialCumplimiento · Auditor |
| `roles[].empresaId` | uuid o null | no |  |

**Respuestas**

- `200` OK — UsuarioDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `UsuarioDto` que se detallan en `POST /api/v1/usuarios/{id}/invitar`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/usuarios/3fa85f64-5717-4562-b3fc-2c963f66afa6/roles" \
  -H "X-Api-Key: $API_KEY" \
  -H "Content-Type: application/json" \
  -d '{"roles":[{"rol":"Administrador","empresaId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}]}'
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "nombre": "Constructora Norte S.A.S.",
  "correo": "contacto@empresa.co",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "activo": true,
  "identidades": [
    {
      "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "idExterno": "REF-0001"
    }
  ],
  "roles": [
    {
      "rol": "Administrador",
      "empresaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "creadoEn": "2026-11-12T15:00:00Z",
  "ultimoAcceso": "2026-11-12T15:00:00Z",
  "exigePasskey": false,
  "passkeys": 0,
  "invitadoEn": "2026-11-12T15:00:00Z"
}
```

</details>
