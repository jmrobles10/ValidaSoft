# API · Plataforma

38 operaciones en 12 recursos.

## Auditoría de operadores

La auditoría de los ingresos de los operadores de Plataforma en la consola (PI-16): quién entró, cuándo, cómo y desde dónde; lo que falló y por qué. Nunca códigos ni tokens.

### `GET /api/v1/plataforma/auditoria-ingresos`

**Listar ingresos operadores.** Los registros de identidad de los operadores (ingresos exitosos y fallidos, passkeys, sesiones, reautenticaciones), de lo más reciente a lo más antiguo. Filtros: `operador` (id), `evento` (`IngresoExitoso`, `IngresoFallido`, `PasskeyRegistrada`…), `exitoso`, `desde`/`hasta`. 422 `evento-invalido`, `rango-invalido`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarIngresosOperadores`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `operador` | query | texto | no |  |
| `evento` | query | texto | no |  |
| `exitoso` | query | booleano | no |  |
| `desde` | query | fecha y hora | no |  |
| `hasta` | query | fecha y hora | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfIngresoDeOperadorDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de IngresoDeOperadorDto | sí |  |
| `datos[].id` | uuid | sí |  |
| `datos[].fecha` | fecha y hora | sí |  |
| `datos[].evento` | texto | sí |  |
| `datos[].exitoso` | booleano | sí |  |
| `datos[].operadorId` | texto o null | no |  |
| `datos[].correo` | texto o null | no |  |
| `datos[].metodo` | texto o null | no |  |
| `datos[].motivo` | texto o null | no |  |
| `datos[].ip` | texto o null | no |  |
| `datos[].dispositivo` | texto o null | no |  |
| `datos[].actorTipo` | texto | sí |  |
| `datos[].actorId` | texto | sí |  |
| `datos[].sesionId` | uuid o null | no |  |
| `datos[].passkeyId` | uuid o null | no |  |
| `siguienteCursor` | texto o null | no |  |

**Errores de negocio que lanza**

- `422` `evento-invalido`
- `422` `rango-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/auditoria-ingresos" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "fecha": "2026-11-12T15:00:00Z",
      "evento": "texto",
      "exitoso": true,
      "operadorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "correo": "contacto@empresa.co",
      "metodo": "texto",
      "motivo": "Texto de ejemplo.",
      "ip": "texto",
      "dispositivo": "texto",
      "actorTipo": "texto",
      "actorId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "sesionId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "passkeyId": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

## Catálogos

Catálogos de plataforma de solo lectura.

### `GET /api/v1/catalogos/paises`

**Listar paises.**

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `listarPaises`**

**Respuestas**

- `200` OK — lista de PaisDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de PaisDto | sí |  |
| `[].codigo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].tiposIdentificacion` | lista de TipoIdentificacionDto | sí |  |
| `[].tiposIdentificacion[].pais` | texto | sí |  |
| `[].tiposIdentificacion[].codigo` | texto | sí |  |
| `[].tiposIdentificacion[].nombre` | texto | sí |  |
| `[].tiposIdentificacion[].aplicaA` | texto | sí |  |
| `[].tiposIdentificacion[].patron` | texto | sí |  |
| `[].tiposIdentificacion[].tieneDv` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/catalogos/paises" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
[
  {
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "tiposIdentificacion": [
      {
        "pais": "CO",
        "codigo": "CODIGO_EJEMPLO",
        "nombre": "Constructora Norte S.A.S.",
        "aplicaA": "texto",
        "patron": "texto",
        "tieneDv": true
      }
    ]
  }
]
```

</details>

### `GET /api/v1/catalogos/tipos-identificacion`

**Listar tipos identificacion.** Tipos de identificación del país, incluidos los genéricos (pasaporte, id fiscal).

**Cualquier sesión autenticada · API (`/api/v1`) · SDK: `listarTiposIdentificacion`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `pais` | query | texto | no |  |

**Respuestas**

- `200` OK — lista de TipoIdentificacionDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de TipoIdentificacionDto | sí |  |
| `[].pais` | texto | sí |  |
| `[].codigo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].aplicaA` | texto | sí |  |
| `[].patron` | texto | sí |  |
| `[].tieneDv` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/catalogos/tipos-identificacion" \
  -H "Authorization: Bearer $TOKEN_DE_SESION"
```

Respuesta `200`:

```json
[
  {
    "pais": "CO",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "aplicaA": "texto",
    "patron": "texto",
    "tieneDv": true
  }
]
```

</details>

## Clave de arranque

### `GET /api/v1/plataforma/clave-de-arranque`

**Obtener clave de arranque.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerClaveDeArranque`**

**Respuestas**

- `200` OK — ClaveDeArranqueDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `configurada` | booleano | sí |  |
| `desactivada` | booleano | sí |  |
| `desactivadaEn` | fecha y hora o null | no |  |
| `desactivadaPor` | texto o null | no |  |
| `operadoresConPasskey` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/clave-de-arranque" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "configurada": true,
  "desactivada": true,
  "desactivadaEn": "2026-11-12T15:00:00Z",
  "desactivadaPor": "texto",
  "operadoresConPasskey": 1
}
```

</details>

### `POST /api/v1/plataforma/clave-de-arranque/desactivar`

**Desactivar clave de arranque.** Desactiva la clave de arranque: deja de autenticar de inmediato en todos los servidores. 409 `sin-operadores-con-passkey` si ningún operador activo tiene passkey (quedaría sin poder entrar a la consola), `clave-de-arranque-desactivada` si ya estaba apagada. Exige reautenticar con la passkey.

**Operador de Plataforma · API (`/api/v1`) · SDK: `desactivarClaveDeArranque`**

**Respuestas**

- `200` OK — ClaveDeArranqueDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `configurada` | booleano | sí |  |
| `desactivada` | booleano | sí |  |
| `desactivadaEn` | fecha y hora o null | no |  |
| `desactivadaPor` | texto o null | no |  |
| `operadoresConPasskey` | entero | sí |  |

**Errores de negocio que lanza**

- `409` `sin-operadores-con-passkey`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/clave-de-arranque/desactivar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "configurada": true,
  "desactivada": true,
  "desactivadaEn": "2026-11-12T15:00:00Z",
  "desactivadaPor": "texto",
  "operadoresConPasskey": 1
}
```

</details>

### `POST /api/v1/plataforma/clave-de-arranque/reactivar`

**Reactivar clave de arranque.** Vuelve a permitir la clave de arranque (si el servidor aún la tiene configurada). 409 `clave-de-arranque-activa`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `reactivarClaveDeArranque`**

**Respuestas**

- `200` OK — ClaveDeArranqueDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `configurada` | booleano | sí |  |
| `desactivada` | booleano | sí |  |
| `desactivadaEn` | fecha y hora o null | no |  |
| `desactivadaPor` | texto o null | no |  |
| `operadoresConPasskey` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/clave-de-arranque/reactivar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "configurada": true,
  "desactivada": true,
  "desactivadaEn": "2026-11-12T15:00:00Z",
  "desactivadaPor": "texto",
  "operadoresConPasskey": 1
}
```

</details>

## Empresas de grupo

### `POST /api/v1/plataforma/tenants/{tenantId}/empresas`

**Crear empresa de grupo.** Crea una empresa en el grupo (con su auditoría, a nombre de Plataforma). Con `Idempotency-Key` repetir la petición no la duplica. 404 `tenant-no-encontrado`; 409 `grupo-inactivo`, `empresa-duplicada` (misma identificación en el grupo), `prefijo-folio-duplicado`; 422 `prefijo-folio-invalido`, `nombre-requerido` y los de la identificación.

**Operador de Plataforma · API (`/api/v1`) · SDK: `crearEmpresaDeGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tenantId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, CrearEmpresaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `nombre` | texto | sí |  |
| `prefijoFolio` | texto | sí |  |
| `logoUrl` | texto o null | no |  |
| `replyTo` | texto o null | no |  |

**Respuestas**

- `201` Creado — EmpresaDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `tenantId` | uuid | sí |  |
| `identificacion` | IdentificacionDto | sí |  |
| `identificacion.pais` | texto | sí |  |
| `identificacion.tipo` | texto | sí |  |
| `identificacion.numero` | texto | sí |  |
| `identificacion.digitoVerificacion` | texto o null | no |  |
| `nombre` | texto | sí |  |
| `logoUrl` | texto o null | no |  |
| `replyTo` | texto o null | no |  |
| `prefijoFolio` | texto | sí |  |
| `activa` | booleano | sí |  |
| `colorMarca` | texto o null | no |  |

**Errores de negocio que lanza**

- `404` `tenant-no-encontrado`
- `409` `empresa-duplicada`
- `409` `grupo-inactivo`
- `409` `prefijo-folio-duplicado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/empresas" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"identificacion":{"pais":"CO","tipo":"texto","numero":"900123456","digitoVerificacion":"texto"},"nombre":"Constructora Norte S.A.S.","prefijoFolio":"texto","logoUrl":"https://ejemplo.co/recurso","replyTo":"texto"}'
```

Respuesta `201`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "identificacion": {
    "pais": "CO",
    "tipo": "texto",
    "numero": "900123456",
    "digitoVerificacion": "texto"
  },
  "nombre": "Constructora Norte S.A.S.",
  "logoUrl": "https://ejemplo.co/recurso",
  "replyTo": "texto",
  "prefijoFolio": "texto",
  "activa": true,
  "colorMarca": "texto"
}
```

</details>

## Listas de plataforma

Cuenta de la plataforma con los proveedores de listas restrictivas (G-2, PP-5): **Infolaft es uno solo para todos los clientes**, con la cuenta de Sinco, no uno por empresa. La configura Plataforma desde su consola; la clave es de solo escritura (vuelve «configurada · …1234») y se cifra con la clave maestra (`Proteger`), como los demás secretos de la plataforma. Las empresas consultan con ella sin verla ni cargarla; cada una conserva su consumo mensual (VE-11) para atribuirle el costo. Stradata y Compliance siguen por empresa. Todo cambio queda en la auditoría, sin la clave.

### `GET /api/v1/plataforma/listas-restrictivas`

**Obtener listas de plataforma.** Cuentas de la plataforma, si la instalación habilitó cada proveedor y cuántas empresas aún guardan credenciales propias de antes (migración).

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerListasDePlataforma`**

**Respuestas**

- `200` OK — ListasDePlataformaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `proveedores` | lista de ProveedorDePlataformaDto | sí |  |
| `proveedores[].nombre` | texto | sí |  |
| `proveedores[].habilitadoEnInstalacion` | booleano | sí |  |
| `proveedores[].configurado` | booleano | sí |  |
| `proveedores[].usuario` | texto o null | no |  |
| `proveedores[].ultimosCuatroClave` | texto o null | no |  |
| `proveedores[].urlBase` | texto o null | no |  |
| `proveedores[].plan` | texto o null | no |  |
| `proveedores[].cuotaMensualPorEmpresa` | entero o null | no |  |
| `proveedores[].modoBusqueda` | texto o null | no |  |
| `proveedores[].actualizadasEn` | fecha y hora o null | no |  |
| `proveedores[].empresasConCredencialesPropias` | entero | sí |  |
| `actualizadaEn` | fecha y hora o null | no |  |
| `actualizadaPor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/listas-restrictivas" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "proveedores": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "habilitadoEnInstalacion": true,
      "configurado": true,
      "usuario": "texto",
      "ultimosCuatroClave": "texto",
      "urlBase": "https://ejemplo.co/recurso",
      "plan": "texto",
      "cuotaMensualPorEmpresa": 1,
      "modoBusqueda": "texto",
      "actualizadasEn": "2026-11-12T15:00:00Z",
      "empresasConCredencialesPropias": 1
    }
  ],
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "actualizadaPor": "texto"
}
```

</details>

### `PUT /api/v1/plataforma/listas-restrictivas`

**Actualizar listas de plataforma.** Crea, cambia o quita (`clave: ""`) la cuenta de Infolaft de la plataforma: usuario, clave, URL del contrato, plan, la cuota mensual de **cada empresa** y el modo de búsqueda. Exige confirmar con la passkey (la clave es de todos los clientes). 422 `proveedor-invalido` (no es de la plataforma), `usuario-requerido`, `clave-requerida`, `clave-invalida`, `url-invalida`, `cuota-invalida`, `modo-busqueda-invalido`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `actualizarListasDePlataforma`**

**Cuerpo de la petición** (`application/json`, ActualizarListasDePlataformaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `credenciales` | CredencialesListasDto | sí |  |
| `credenciales.proveedor` | texto | sí |  |
| `credenciales.usuario` | texto o null | no |  |
| `credenciales.clave` | texto o null | no |  |
| `credenciales.urlBase` | texto o null | no |  |
| `credenciales.plan` | texto o null | no |  |
| `credenciales.cuotaMensual` | entero o null | no |  |
| `credenciales.modoBusqueda` | texto o null | no |  |

**Respuestas**

- `200` OK — ListasDePlataformaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `proveedores` | lista de ProveedorDePlataformaDto | sí |  |
| `proveedores[].nombre` | texto | sí |  |
| `proveedores[].habilitadoEnInstalacion` | booleano | sí |  |
| `proveedores[].configurado` | booleano | sí |  |
| `proveedores[].usuario` | texto o null | no |  |
| `proveedores[].ultimosCuatroClave` | texto o null | no |  |
| `proveedores[].urlBase` | texto o null | no |  |
| `proveedores[].plan` | texto o null | no |  |
| `proveedores[].cuotaMensualPorEmpresa` | entero o null | no |  |
| `proveedores[].modoBusqueda` | texto o null | no |  |
| `proveedores[].actualizadasEn` | fecha y hora o null | no |  |
| `proveedores[].empresasConCredencialesPropias` | entero | sí |  |
| `actualizadaEn` | fecha y hora o null | no |  |
| `actualizadaPor` | texto o null | no |  |

**Errores de negocio que lanza**

- `422` `modo-busqueda-invalido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/plataforma/listas-restrictivas" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Content-Type: application/json" \
  -d '{"credenciales":{"proveedor":"texto","usuario":"texto","clave":"texto","urlBase":"https://ejemplo.co/recurso","plan":"texto","cuotaMensual":1,"modoBusqueda":"texto"}}'
```

Respuesta `200`:

```json
{
  "proveedores": [
    {
      "nombre": "Constructora Norte S.A.S.",
      "habilitadoEnInstalacion": true,
      "configurado": true,
      "usuario": "texto",
      "ultimosCuatroClave": "texto",
      "urlBase": "https://ejemplo.co/recurso",
      "plan": "texto",
      "cuotaMensualPorEmpresa": 1,
      "modoBusqueda": "texto",
      "actualizadasEn": "2026-11-12T15:00:00Z",
      "empresasConCredencialesPropias": 1
    }
  ],
  "actualizadaEn": "2026-11-12T15:00:00Z",
  "actualizadaPor": "texto"
}
```

</details>

### `POST /api/v1/plataforma/listas-restrictivas/migrar-credenciales`

**Migrar credenciales de listas.** Migración de las credenciales que las empresas tenían de Infolaft antes de G-2: la **primera válida** (la clave se descifra con la de su grupo y se vuelve a cifrar con la maestra) pasa a ser la cuenta de la plataforma, si esta aún no tiene una; después se quitan las credenciales propias de Infolaft de todas las empresas. Si ninguna se puede leer o la plataforma no quedó configurada, no se quita nada. Idempotente: repetirla no cambia nada. La cuota por empresa no se hereda (la fija Plataforma). Cada empresa queda en la auditoría, sin la clave.

**Operador de Plataforma · API (`/api/v1`) · SDK: `migrarCredencialesDeListas`**

**Respuestas**

- `200` OK — ResultadoMigracionListasDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `proveedor` | texto | sí |  |
| `cuentaTomada` | booleano | sí |  |
| `empresaDeOrigen` | uuid o null | no |  |
| `empresasConCredenciales` | entero | sí |  |
| `empresasLimpiadas` | entero | sí |  |
| `noLegibles` | entero | sí |  |
| `cuotasQueNoSeHeredaron` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/listas-restrictivas/migrar-credenciales" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "proveedor": "texto",
  "cuentaTomada": true,
  "empresaDeOrigen": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "empresasConCredenciales": 1,
  "empresasLimpiadas": 1,
  "noLegibles": 1,
  "cuotasQueNoSeHeredaron": 1
}
```

</details>

## Operadores de plataforma

### `POST /api/v1/plataforma/sesiones`

**Crear sesión plataforma.** Canjea, servidor a servidor, la credencial de Plataforma (`X-Api-Key` con la clave de arranque) y la aserción `X-Usuario-Delegado` de un operador —firmada por un sistema de Plataforma activo, `iss` = su código— por una sesión de 30 minutos para la consola (sin tenant, solo `plataforma:tenants`). El operador queda registrado para la auditoría. 403 `credencial-de-plataforma-requerida` con otra credencial (una sesión no se renueva a sí misma); 422 `usuario-delegado-requerido`; 401 `asercion-*`, `sistema-inactivo`, `operador-inactivo`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `crearSesionPlataforma`**

**Respuestas**

- `200` OK — SesionUsuarioDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `token` | texto | sí |  |
| `expira` | fecha y hora | sí |  |

**Errores de negocio que lanza**

- `403` `credencial-de-plataforma-requerida`
- `422` `usuario-delegado-requerido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/sesiones" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
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

### `GET /api/v1/plataforma/sistemas`

**Listar sistemas plataforma.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarSistemasPlataforma`**

**Respuestas**

- `200` OK — lista de SistemaDePlataformaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de SistemaDePlataformaDto | sí |  |
| `[].codigo` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].clavePublicaPem` | texto | sí |  |
| `[].origenesPermitidos` | lista de texto | sí |  |
| `[].activo` | booleano | sí |  |
| `[].creadoEn` | fecha y hora | sí |  |
| `[].actualizadoEn` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/sistemas" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
[
  {
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "clavePublicaPem": "texto",
    "origenesPermitidos": [
      "texto"
    ],
    "activo": true,
    "creadoEn": "2026-11-12T15:00:00Z",
    "actualizadoEn": "2026-11-12T15:00:00Z"
  }
]
```

</details>

### `PUT /api/v1/plataforma/sistemas/{codigo}`

**Fijar sistema plataforma.** Crea o reemplaza el sistema `codigo` (nombre, clave pública PEM y orígenes que alojan la consola). Reemplazar la clave invalida al instante las aserciones firmadas con la anterior. 422 `codigo-invalido`, `clave-publica-invalida`, `origen-invalido`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `fijarSistemaPlataforma`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `codigo` | path | texto | sí |  |

**Cuerpo de la petición** (`application/json`, FijarSistemaDePlataformaDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `origenesPermitidos` | lista de texto o null | no |  |

**Respuestas**

- `200` OK — SistemaDePlataformaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `origenesPermitidos` | lista de texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `actualizadoEn` | fecha y hora | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/plataforma/sistemas/CODIGO_EJEMPLO" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Constructora Norte S.A.S.","clavePublicaPem":"texto","origenesPermitidos":["texto"]}'
```

Respuesta `200`:

```json
{
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "clavePublicaPem": "texto",
  "origenesPermitidos": [
    "texto"
  ],
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

## Operadores directos

Operadores de Plataforma de ingreso directo (ING-4): entran a la consola con correo, código y passkey (obligatoria) y se gestionan con la credencial de arranque —solo para dar de alta al primero— o desde la propia consola. Un operador revoca las passkeys y cierra las sesiones de otro cuando las pierde; todo queda en la auditoría de identidad.

### `GET /api/v1/plataforma/operadores`

**Listar operadores plataforma.** Los operadores de Plataforma (de ingreso directo y los de identidad delegada) con cuántas passkeys activas tiene cada uno.

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarOperadoresPlataforma`**

**Respuestas**

- `200` OK — lista de OperadorDePlataformaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de OperadorDePlataformaDto | sí |  |
| `[].id` | texto | sí |  |
| `[].sistema` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].correo` | texto | sí |  |
| `[].activo` | booleano | sí |  |
| `[].directo` | booleano | sí |  |
| `[].creadoEn` | fecha y hora | sí |  |
| `[].ultimoAcceso` | fecha y hora o null | no |  |
| `[].passkeys` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/operadores" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
[
  {
    "id": "texto",
    "sistema": "texto",
    "nombre": "Constructora Norte S.A.S.",
    "correo": "contacto@empresa.co",
    "activo": true,
    "directo": true,
    "creadoEn": "2026-11-12T15:00:00Z",
    "ultimoAcceso": "2026-11-12T15:00:00Z",
    "passkeys": 1
  }
]
```

</details>

### `POST /api/v1/plataforma/operadores`

**Crear operador plataforma.** Da de alta un operador de ingreso directo y le envía el correo para entrar. 409 `operador-duplicado`; 422 `correo-invalido`, `nombre-requerido`. La primera vez se hace con la credencial de arranque (`Validasoft:Plataforma:ClaveBootstrap`); después, con la sesión de un operador, y la clave de arranque se desactiva (docs/07 §2.4).

**Operador de Plataforma · API (`/api/v1`) · SDK: `crearOperadorPlataforma`**

**Cuerpo de la petición** (`application/json`, CrearOperadorDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `nombre` | texto | sí |  |
| `correo` | texto | sí |  |

**Respuestas**

- `201` Creado — OperadorDePlataformaDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | texto | sí |  |
| `sistema` | texto | sí |  |
| `nombre` | texto | sí |  |
| `correo` | texto | sí |  |
| `activo` | booleano | sí |  |
| `directo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `ultimoAcceso` | fecha y hora o null | no |  |
| `passkeys` | entero | sí |  |

**Errores de negocio que lanza**

- `409` `operador-duplicado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/operadores" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"nombre":"Constructora Norte S.A.S.","correo":"contacto@empresa.co"}'
```

Respuesta `201`:

```json
{
  "id": "texto",
  "sistema": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "correo": "contacto@empresa.co",
  "activo": true,
  "directo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "ultimoAcceso": "2026-11-12T15:00:00Z",
  "passkeys": 1
}
```

</details>

### `POST /api/v1/plataforma/operadores/{id}/desactivar`

**Desactivar operador plataforma.** Desactiva al operador (PI-7): ya no entra, **sus sesiones se cierran** —también las de identidad delegada, en todos los servidores— y **sus passkeys se revocan**. Reactivarlo no las restaura: entra con el código del correo y registra una nueva. Nadie puede desactivarse a sí mismo (409 `auto-desactivacion`). Exige reautenticar con la passkey (403 `reautenticacion-requerida`).

**Operador de Plataforma · API (`/api/v1`) · SDK: `desactivarOperadorPlataforma`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | texto | sí |  |

**Respuestas**

- `200` OK — OperadorDePlataformaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | texto | sí |  |
| `sistema` | texto | sí |  |
| `nombre` | texto | sí |  |
| `correo` | texto | sí |  |
| `activo` | booleano | sí |  |
| `directo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `ultimoAcceso` | fecha y hora o null | no |  |
| `passkeys` | entero | sí |  |

**Errores de negocio que lanza**

- `404` `operador-no-encontrado`
- `409` `auto-desactivacion`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/operadores/texto/desactivar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "texto",
  "sistema": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "correo": "contacto@empresa.co",
  "activo": true,
  "directo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "ultimoAcceso": "2026-11-12T15:00:00Z",
  "passkeys": 1
}
```

</details>

### `GET /api/v1/plataforma/operadores/{id}/passkeys`

**Listar passkeys operador.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarPasskeysOperador`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | texto | sí |  |

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
curl -X GET "https://{host}/api/v1/plataforma/operadores/texto/passkeys" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
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

### `POST /api/v1/plataforma/operadores/{id}/passkeys/{passkeyId}/revocar`

**Revocar passkey operador.** Revoca una passkey del operador (la perdió): deja de valer y se cierran sus sesiones; entra con el código y registra otra.

**Operador de Plataforma · API (`/api/v1`) · SDK: `revocarPasskeyOperador`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | texto | sí |  |
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
curl -X POST "https://{host}/api/v1/plataforma/operadores/texto/passkeys/3fa85f64-5717-4562-b3fc-2c963f66afa6/revocar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
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

### `POST /api/v1/plataforma/operadores/{id}/reactivar`

**Reactivar operador plataforma.** Vuelve a activar al operador (PI-7) y le envía el correo para entrar: como sus passkeys se revocaron al desactivarlo, entra con el código y registra una nueva. 409 `operador-activo` si ya estaba activo. Exige reautenticar con la passkey.

**Operador de Plataforma · API (`/api/v1`) · SDK: `reactivarOperadorPlataforma`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | texto | sí |  |

**Respuestas**

- `200` OK — OperadorDePlataformaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | texto | sí |  |
| `sistema` | texto | sí |  |
| `nombre` | texto | sí |  |
| `correo` | texto | sí |  |
| `activo` | booleano | sí |  |
| `directo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `ultimoAcceso` | fecha y hora o null | no |  |
| `passkeys` | entero | sí |  |

**Errores de negocio que lanza**

- `404` `operador-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/operadores/texto/reactivar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "id": "texto",
  "sistema": "texto",
  "nombre": "Constructora Norte S.A.S.",
  "correo": "contacto@empresa.co",
  "activo": true,
  "directo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "ultimoAcceso": "2026-11-12T15:00:00Z",
  "passkeys": 1
}
```

</details>

### `GET /api/v1/plataforma/operadores/{id}/sesiones`

**Listar sesiones operador.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarSesionesOperador`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | texto | sí |  |

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
curl -X GET "https://{host}/api/v1/plataforma/operadores/texto/sesiones" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
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

### `POST /api/v1/plataforma/operadores/{id}/sesiones/{sesionId}/cerrar`

**Cerrar sesión operador.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `cerrarSesionOperador`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | texto | sí |  |
| `sesionId` | path | uuid | sí |  |

**Respuestas**

- `204` Sin contenido

**Errores de negocio que lanza**

- `404` `sesion-no-encontrada`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/operadores/texto/sesiones/3fa85f64-5717-4562-b3fc-2c963f66afa6/cerrar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

</details>

## Plan del grupo

Plan y límites de cada grupo (OD-14…OD-17): Plataforma los fija; el grupo ve su plan y su uso. Sin plan configurado el grupo no tiene límites.

### `POST /api/v1/plataforma/planes/revisar`

**Revisar planes de grupos.** Mide ya a un grupo (o a todos) frente a su plan y avisa lo que toque (80 % y 100 %): lo mismo que hace el barrido periódico, para operar y para las pruebas.

**Operador de Plataforma · API (`/api/v1`) · SDK: `revisarPlanesDeGrupos`**

**Cuerpo de la petición** (`application/json`, RevisarPlanesDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tenantId` | uuid o null | no |  |

**Respuestas**

- `200` OK — ResultadoDeRevisionDePlanesDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `grupos` | entero | sí |  |
| `avisos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/planes/revisar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"tenantId":"3fa85f64-5717-4562-b3fc-2c963f66afa6"}'
```

Respuesta `200`:

```json
{
  "grupos": 1,
  "avisos": 1
}
```

</details>

### `GET /api/v1/plataforma/tenants/{tenantId}/plan`

**Obtener plan de un grupo.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerPlanDeUnGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tenantId` | path | uuid | sí |  |
| `mes` | query | texto | no |  |

**Respuestas**

- `200` OK — UsoDelGrupoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `mes` | texto | sí |  |
| `plan` | PlanDelGrupoDto | sí |  |
| `plan.tenantId` | uuid | sí |  |
| `plan.codigo` | texto | sí |  |
| `plan.limites` | LimitesDePlanDto | sí |  |
| `plan.limites.usuarios` | entero o null | no |  |
| `plan.limites.empresas` | entero o null | no |  |
| `plan.limites.vinculosActivos` | entero o null | no |  |
| `plan.limites.almacenamientoMb` | entero o null | no |  |
| `plan.limites.llamadasApiMes` | entero o null | no |  |
| `plan.bloqueoSuave` | booleano | sí |  |
| `plan.configurado` | booleano | sí |  |
| `plan.actualizadoEn` | fecha y hora o null | no |  |
| `mediciones` | lista de MedicionDePlanDto | sí |  |
| `mediciones[].metrica` | texto | sí |  |
| `mediciones[].usado` | entero | sí |  |
| `mediciones[].limite` | entero o null | no |  |
| `mediciones[].porcentaje` | entero o null | no |  |
| `mediciones[].nivel` | texto | sí |  |
| `alertas` | lista de texto | sí |  |
| `bloqueado` | booleano | sí |  |
| `almacenamientoMedidoEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/plan" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "mes": "texto",
  "plan": {
    "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "limites": {
      "usuarios": 1,
      "empresas": 1,
      "vinculosActivos": 1,
      "almacenamientoMb": 1,
      "llamadasApiMes": 1
    },
    "bloqueoSuave": true,
    "configurado": true,
    "actualizadoEn": "2026-11-12T15:00:00Z"
  },
  "mediciones": [
    {
      "metrica": "texto",
      "usado": 1,
      "limite": 1,
      "porcentaje": 1,
      "nivel": "texto"
    }
  ],
  "alertas": [
    "texto"
  ],
  "bloqueado": true,
  "almacenamientoMedidoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `PUT /api/v1/plataforma/tenants/{tenantId}/plan`

**Actualizar plan de un grupo.** Fija el plan del grupo: código, límites (vacío = sin límite; `0` = nada) y bloqueo suave. Queda en la auditoría y el código se refleja en el grupo. 404 `tenant-no-encontrado`; 422 `codigo-plan-invalido`, `limite-invalido`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `actualizarPlanDeUnGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tenantId` | path | uuid | sí |  |

**Cuerpo de la petición** (`application/json`, ActualizarPlanDelGrupoDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `limites` | LimitesDePlanDto | sí |  |
| `limites.usuarios` | entero o null | no |  |
| `limites.empresas` | entero o null | no |  |
| `limites.vinculosActivos` | entero o null | no |  |
| `limites.almacenamientoMb` | entero o null | no |  |
| `limites.llamadasApiMes` | entero o null | no |  |
| `bloqueoSuave` | booleano | sí |  |

**Respuestas**

- `200` OK — UsoDelGrupoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `UsoDelGrupoDto` que se detallan en `GET /api/v1/plataforma/tenants/{tenantId}/plan`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X PUT "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/plan" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Content-Type: application/json" \
  -d '{"codigo":"CODIGO_EJEMPLO","limites":{"usuarios":1,"empresas":1,"vinculosActivos":1,"almacenamientoMb":1,"llamadasApiMes":1},"bloqueoSuave":true}'
```

Respuesta `200`:

```json
{
  "mes": "texto",
  "plan": {
    "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "limites": {
      "usuarios": 1,
      "empresas": 1,
      "vinculosActivos": 1,
      "almacenamientoMb": 1,
      "llamadasApiMes": 1
    },
    "bloqueoSuave": true,
    "configurado": true,
    "actualizadoEn": "2026-11-12T15:00:00Z"
  },
  "mediciones": [
    {
      "metrica": "texto",
      "usado": 1,
      "limite": 1,
      "porcentaje": 1,
      "nivel": "texto"
    }
  ],
  "alertas": [
    "texto"
  ],
  "bloqueado": true,
  "almacenamientoMedidoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `GET /api/v1/tenant/plan`

**Obtener plan del grupo.** El plan del grupo de la credencial (límites y bloqueo suave); sin plan configurado, sin límites.

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `obtenerPlanDelGrupo`**

**Respuestas**

- `200` OK — PlanDelGrupoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tenantId` | uuid | sí |  |
| `codigo` | texto | sí |  |
| `limites` | LimitesDePlanDto | sí |  |
| `limites.usuarios` | entero o null | no |  |
| `limites.empresas` | entero o null | no |  |
| `limites.vinculosActivos` | entero o null | no |  |
| `limites.almacenamientoMb` | entero o null | no |  |
| `limites.llamadasApiMes` | entero o null | no |  |
| `bloqueoSuave` | booleano | sí |  |
| `configurado` | booleano | sí |  |
| `actualizadoEn` | fecha y hora o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/tenant/plan" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "codigo": "CODIGO_EJEMPLO",
  "limites": {
    "usuarios": 1,
    "empresas": 1,
    "vinculosActivos": 1,
    "almacenamientoMb": 1,
    "llamadasApiMes": 1
  },
  "bloqueoSuave": true,
  "configurado": true,
  "actualizadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `GET /api/v1/tenant/uso`

**Obtener uso del grupo.** Lo que el grupo lleva usado frente a su plan en el mes `mes` (`AAAA-MM`; por omisión el actual): usuarios, empresas, vínculos activos, almacenamiento (medido hace como mucho 5 minutos) y llamadas a la API (solo las de las API keys, no las de las pantallas ni las del sandbox). 422 `mes-invalido`.

**Permiso configuracion:leer · API (`/api/v1`) · SDK: `obtenerUsoDelGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `mes` | query | texto | no |  |

**Respuestas**

- `200` OK — UsoDelGrupoDto
- `404` No encontrado

Campos de la respuesta `200`:

Mismos campos de `UsoDelGrupoDto` que se detallan en `GET /api/v1/plataforma/tenants/{tenantId}/plan`.

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/tenant/uso" \
  -H "X-Api-Key: $API_KEY"
```

Respuesta `200`:

```json
{
  "mes": "texto",
  "plan": {
    "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "limites": {
      "usuarios": 1,
      "empresas": 1,
      "vinculosActivos": 1,
      "almacenamientoMb": 1,
      "llamadasApiMes": 1
    },
    "bloqueoSuave": true,
    "configurado": true,
    "actualizadoEn": "2026-11-12T15:00:00Z"
  },
  "mediciones": [
    {
      "metrica": "texto",
      "usado": 1,
      "limite": 1,
      "porcentaje": 1,
      "nivel": "texto"
    }
  ],
  "alertas": [
    "texto"
  ],
  "bloqueado": true,
  "almacenamientoMedidoEn": "2026-11-12T15:00:00Z"
}
```

</details>

## Secretos

Secretos en reposo y su clave maestra (PT-3): cuántos hay de cada tipo con la clave vigente y re-cifrado con ella de los que quedaron con una anterior o con el formato heredado (derivado de la clave de sesiones). El valor de un secreto nunca sale de aquí ni va a la auditoría.

### `GET /api/v1/plataforma/secretos`

**Obtener estado secretos.** Solo cuenta (no re-cifra): clave vigente, claves anteriores y, por tipo, cuántos secretos ya usan la vigente.

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerEstadoSecretos`**

**Respuestas**

- `200` OK — EstadoDeSecretosDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `claveVigente` | texto | sí |  |
| `clavesAnteriores` | lista de texto | sí |  |
| `simulado` | booleano | sí |  |
| `porTipo` | lista de SecretosPorTipoDto | sí |  |
| `porTipo[].tipo` | texto | sí |  |
| `porTipo[].total` | entero | sí |  |
| `porTipo[].vigentes` | entero | sí |  |
| `porTipo[].recifrados` | entero | sí |  |
| `porTipo[].fallidos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/secretos" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "claveVigente": "texto",
  "clavesAnteriores": [
    "texto"
  ],
  "simulado": true,
  "porTipo": [
    {
      "tipo": "texto",
      "total": 1,
      "vigentes": 1,
      "recifrados": 1,
      "fallidos": 1
    }
  ]
}
```

</details>

### `POST /api/v1/plataforma/secretos/recifrar`

**Recifrar secretos.** Re-cifra con la clave maestra vigente las llaves de OpenAI de los planes de IA, los secretos de los webhooks (vigente y anterior en solapamiento) y los números de las cuentas bancarias de la bóveda. Idempotente: lo que ya está con la vigente no se toca. Lo que no se puede descifrar (su clave ya no está configurada) se cuenta en `fallidos` y se deja igual. El número de cuenta congelado en los eventos de un vínculo (y en el documento que los proyecta) también pasa a la clave del grupo (PI-11): migración controlada del valor protegido, idempotente.

**Operador de Plataforma · API (`/api/v1`) · SDK: `recifrarSecretos`**

**Respuestas**

- `200` OK — EstadoDeSecretosDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `claveVigente` | texto | sí |  |
| `clavesAnteriores` | lista de texto | sí |  |
| `simulado` | booleano | sí |  |
| `porTipo` | lista de SecretosPorTipoDto | sí |  |
| `porTipo[].tipo` | texto | sí |  |
| `porTipo[].total` | entero | sí |  |
| `porTipo[].vigentes` | entero | sí |  |
| `porTipo[].recifrados` | entero | sí |  |
| `porTipo[].fallidos` | entero | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/secretos/recifrar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "claveVigente": "texto",
  "clavesAnteriores": [
    "texto"
  ],
  "simulado": true,
  "porTipo": [
    {
      "tipo": "texto",
      "total": 1,
      "vigentes": 1,
      "recifrados": 1,
      "fallidos": 1
    }
  ]
}
```

</details>

## Sistemas de grupo

### `GET /api/v1/plataforma/tenants/{tenantId}/sistemas`

**Listar sistemas de grupo.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarSistemasDeGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tenantId` | path | uuid | sí |  |

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

**Errores de negocio que lanza**

- `404` `tenant-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/sistemas" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
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

### `POST /api/v1/plataforma/tenants/{tenantId}/sistemas/{id}/desactivar`

**Desactivar sistema de grupo.** Desactiva el sistema del grupo y revoca sus llaves y sesiones. 409 `sistema-inactivo` si ya estaba desactivado.

**Operador de Plataforma · API (`/api/v1`) · SDK: `desactivarSistemaDeGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tenantId` | path | uuid | sí |  |
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
curl -X POST "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/sistemas/3fa85f64-5717-4562-b3fc-2c963f66afa6/desactivar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
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

### `POST /api/v1/plataforma/tenants/{tenantId}/sistemas/{id}/reactivar`

**Reactivar sistema de grupo.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `reactivarSistemaDeGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `tenantId` | path | uuid | sí |  |
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
curl -X POST "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6/sistemas/3fa85f64-5717-4562-b3fc-2c963f66afa6/reactivar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
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

## Sistemas de plataforma estado

### `POST /api/v1/plataforma/sistemas/{codigo}/desactivar`

**Desactivar sistema plataforma.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `desactivarSistemaPlataforma`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `codigo` | path | texto | sí |  |

**Respuestas**

- `200` OK — SistemaDePlataformaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `origenesPermitidos` | lista de texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `actualizadoEn` | fecha y hora | sí |  |

**Errores de negocio que lanza**

- `404` `sistema-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/sistemas/CODIGO_EJEMPLO/desactivar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "clavePublicaPem": "texto",
  "origenesPermitidos": [
    "texto"
  ],
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

### `POST /api/v1/plataforma/sistemas/{codigo}/reactivar`

**Reactivar sistema plataforma.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `reactivarSistemaPlataforma`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `codigo` | path | texto | sí |  |

**Respuestas**

- `200` OK — SistemaDePlataformaDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `codigo` | texto | sí |  |
| `nombre` | texto | sí |  |
| `clavePublicaPem` | texto | sí |  |
| `origenesPermitidos` | lista de texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `actualizadoEn` | fecha y hora | sí |  |

**Errores de negocio que lanza**

- `404` `sistema-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/sistemas/CODIGO_EJEMPLO/reactivar" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)"
```

Respuesta `200`:

```json
{
  "codigo": "CODIGO_EJEMPLO",
  "nombre": "Constructora Norte S.A.S.",
  "clavePublicaPem": "texto",
  "origenesPermitidos": [
    "texto"
  ],
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "actualizadoEn": "2026-11-12T15:00:00Z"
}
```

</details>

## Tenants

### `GET /api/v1/plataforma/grupos`

**Listar grupos.** Una página de grupos con sus empresas y el estado del plan de IA de cada una (configurado, activo, llave configurada y sus últimos cuatro), para la consola de Plataforma. Ordenados por nombre del grupo y su id (orden estable); las empresas de cada grupo, por nombre. `q`: parte del nombre o del identificador del grupo, o de la razón social o el número de identificación (NIT) de alguna de sus empresas (el grupo sale con todas las suyas). `limite` por omisión 20 (máximo 100); `cursor` es el opaco de la página anterior (`siguienteCursor`). Solo se cargan las empresas y los planes de los grupos de la página.

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarGrupos`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `q` | query | texto | no |  |
| `limite` | query | entero | no |  |
| `cursor` | query | texto | no |  |

**Respuestas**

- `200` OK — PaginaDtoOfGrupoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `datos` | lista de GrupoDto | sí |  |
| `datos[].tenant` | TenantDto | sí |  |
| `datos[].tenant.id` | uuid | sí |  |
| `datos[].tenant.slug` | texto | sí |  |
| `datos[].tenant.nombre` | texto | sí |  |
| `datos[].tenant.region` | texto | sí |  |
| `datos[].tenant.plan` | texto | sí |  |
| `datos[].tenant.zonaHoraria` | texto | sí |  |
| `datos[].tenant.idioma` | texto | sí |  |
| `datos[].tenant.activo` | booleano | sí |  |
| `datos[].tenant.creadoEn` | fecha y hora | sí |  |
| `datos[].tenant.ingreso` | ConfiguracionDeIngresoDto o null | no |  |
| `datos[].tenant.ingreso.modos` | lista de texto | sí |  |
| `datos[].tenant.ingreso.sesionHoras` | entero | sí |  |
| `datos[].tenant.ingreso.inactividadMinutos` | entero | sí |  |
| `datos[].tenant.ingreso.passkeyParaTodos` | booleano | sí |  |
| `datos[].empresas` | lista de EmpresaDeGrupoDto | sí |  |
| `datos[].empresas[].id` | uuid | sí |  |
| `datos[].empresas[].nombre` | texto | sí |  |
| `datos[].empresas[].identificacion` | IdentificacionDto | sí |  |
| `datos[].empresas[].identificacion.pais` | texto | sí |  |
| `datos[].empresas[].identificacion.tipo` | texto | sí |  |
| `datos[].empresas[].identificacion.numero` | texto | sí |  |
| `datos[].empresas[].identificacion.digitoVerificacion` | texto o null | no |  |
| `datos[].empresas[].prefijoFolio` | texto | sí |  |
| `datos[].empresas[].activa` | booleano | sí |  |
| `datos[].empresas[].planIaConfigurado` | booleano | sí |  |
| `datos[].empresas[].planIaActivo` | booleano | sí |  |
| `datos[].empresas[].llaveConfigurada` | booleano | sí |  |
| `datos[].empresas[].ultimosCuatro` | texto o null | no |  |
| `siguienteCursor` | texto o null | no |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/grupos" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "datos": [
    {
      "tenant": {
        "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
        "slug": "grupo-ejemplo",
        "nombre": "Constructora Norte S.A.S.",
        "region": "co",
        "plan": "texto",
        "zonaHoraria": "America/Bogota",
        "idioma": "texto",
        "activo": true,
        "creadoEn": "2026-11-12T15:00:00Z",
        "ingreso": {
          "modos": [
            "texto"
          ],
          "sesionHoras": 1,
          "inactividadMinutos": 1,
          "passkeyParaTodos": true
        }
      },
      "empresas": [
        {
          "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
          "nombre": "Constructora Norte S.A.S.",
          "identificacion": {
            "pais": "CO",
            "tipo": "texto",
            "numero": "900123456",
            "digitoVerificacion": "texto"
          },
          "prefijoFolio": "texto",
          "activa": true,
          "planIaConfigurado": true,
          "planIaActivo": true,
          "llaveConfigurada": true,
          "ultimosCuatro": "texto"
        }
      ]
    }
  ],
  "siguienteCursor": "texto"
}
```

</details>

### `GET /api/v1/plataforma/grupos/{id}`

**Obtener grupo.** Un grupo con sus empresas y el estado del plan de IA de cada una: lo que la consola necesita para rotular una empresa (la pantalla del plan) sin recorrer la lista de grupos. 404 `tenant-no-encontrado`.

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerGrupo`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — GrupoDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tenant` | TenantDto | sí |  |
| `tenant.id` | uuid | sí |  |
| `tenant.slug` | texto | sí |  |
| `tenant.nombre` | texto | sí |  |
| `tenant.region` | texto | sí |  |
| `tenant.plan` | texto | sí |  |
| `tenant.zonaHoraria` | texto | sí |  |
| `tenant.idioma` | texto | sí |  |
| `tenant.activo` | booleano | sí |  |
| `tenant.creadoEn` | fecha y hora | sí |  |
| `tenant.ingreso` | ConfiguracionDeIngresoDto o null | no |  |
| `tenant.ingreso.modos` | lista de texto | sí |  |
| `tenant.ingreso.sesionHoras` | entero | sí |  |
| `tenant.ingreso.inactividadMinutos` | entero | sí |  |
| `tenant.ingreso.passkeyParaTodos` | booleano | sí |  |
| `empresas` | lista de EmpresaDeGrupoDto | sí |  |
| `empresas[].id` | uuid | sí |  |
| `empresas[].nombre` | texto | sí |  |
| `empresas[].identificacion` | IdentificacionDto | sí |  |
| `empresas[].identificacion.pais` | texto | sí |  |
| `empresas[].identificacion.tipo` | texto | sí |  |
| `empresas[].identificacion.numero` | texto | sí |  |
| `empresas[].identificacion.digitoVerificacion` | texto o null | no |  |
| `empresas[].prefijoFolio` | texto | sí |  |
| `empresas[].activa` | booleano | sí |  |
| `empresas[].planIaConfigurado` | booleano | sí |  |
| `empresas[].planIaActivo` | booleano | sí |  |
| `empresas[].llaveConfigurada` | booleano | sí |  |
| `empresas[].ultimosCuatro` | texto o null | no |  |

**Errores de negocio que lanza**

- `404` `tenant-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/grupos/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "tenant": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slug": "grupo-ejemplo",
    "nombre": "Constructora Norte S.A.S.",
    "region": "co",
    "plan": "texto",
    "zonaHoraria": "America/Bogota",
    "idioma": "texto",
    "activo": true,
    "creadoEn": "2026-11-12T15:00:00Z",
    "ingreso": {
      "modos": [
        "texto"
      ],
      "sesionHoras": 1,
      "inactividadMinutos": 1,
      "passkeyParaTodos": true
    }
  },
  "empresas": [
    {
      "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
      "nombre": "Constructora Norte S.A.S.",
      "identificacion": {
        "pais": "CO",
        "tipo": "texto",
        "numero": "900123456",
        "digitoVerificacion": "texto"
      },
      "prefijoFolio": "texto",
      "activa": true,
      "planIaConfigurado": true,
      "planIaActivo": true,
      "llaveConfigurada": true,
      "ultimosCuatro": "texto"
    }
  ]
}
```

</details>

### `GET /api/v1/plataforma/tenants`

**Listar tenants.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `listarTenants`**

**Respuestas**

- `200` OK — lista de TenantDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `[]` | lista de TenantDto | sí |  |
| `[].id` | uuid | sí |  |
| `[].slug` | texto | sí |  |
| `[].nombre` | texto | sí |  |
| `[].region` | texto | sí |  |
| `[].plan` | texto | sí |  |
| `[].zonaHoraria` | texto | sí |  |
| `[].idioma` | texto | sí |  |
| `[].activo` | booleano | sí |  |
| `[].creadoEn` | fecha y hora | sí |  |
| `[].ingreso` | ConfiguracionDeIngresoDto o null | no |  |
| `[].ingreso.modos` | lista de texto | sí |  |
| `[].ingreso.sesionHoras` | entero | sí |  |
| `[].ingreso.inactividadMinutos` | entero | sí |  |
| `[].ingreso.passkeyParaTodos` | booleano | sí |  |

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/tenants" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
[
  {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slug": "grupo-ejemplo",
    "nombre": "Constructora Norte S.A.S.",
    "region": "co",
    "plan": "texto",
    "zonaHoraria": "America/Bogota",
    "idioma": "texto",
    "activo": true,
    "creadoEn": "2026-11-12T15:00:00Z",
    "ingreso": {
      "modos": [
        "texto"
      ],
      "sesionHoras": 1,
      "inactividadMinutos": 1,
      "passkeyParaTodos": true
    }
  }
]
```

</details>

### `POST /api/v1/plataforma/tenants`

**Crear tenant.** Crea el tenant, siembra sus tipos de vínculo y de documento y emite la primera API key de administración. Con `empresa`, `administrador` y `sistema` deja además el grupo operable de punta a punta (PT-5): su primera empresa, su administrador de todo el grupo y el sistema integrado con una API key de todos los permisos del tenant (que actúa en nombre de las personas con `X-Usuario-Delegado`). Todo en una sola transacción; los valores de las llaves solo aparecen en esta respuesta. 409 `slug-duplicado`; 422 de cada parte (`slug-invalido`, `prefijo-folio-invalido`, `clave-publica-invalida`, `origen-invalido`, `sistema-requerido`…).

**Operador de Plataforma · API (`/api/v1`) · SDK: `crearTenant`**

**Cuerpo de la petición** (`application/json`, CrearTenantDto)

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `slug` | texto | sí |  |
| `nombre` | texto | sí |  |
| `region` | texto o null | no |  |
| `zonaHoraria` | texto o null | no |  |
| `empresa` | CrearEmpresaDto o null | no |  |
| `empresa.identificacion` | IdentificacionDto | sí |  |
| `empresa.identificacion.pais` | texto | sí |  |
| `empresa.identificacion.tipo` | texto | sí |  |
| `empresa.identificacion.numero` | texto | sí |  |
| `empresa.identificacion.digitoVerificacion` | texto o null | no |  |
| `empresa.nombre` | texto | sí |  |
| `empresa.prefijoFolio` | texto | sí |  |
| `empresa.logoUrl` | texto o null | no |  |
| `empresa.replyTo` | texto o null | no |  |
| `administrador` | AdministradorInicialDto o null | no |  |
| `administrador.nombre` | texto | sí |  |
| `administrador.correo` | texto | sí |  |
| `administrador.idExterno` | texto o null | no |  |
| `sistema` | CrearSistemaIntegradoDto o null | no |  |
| `sistema.codigo` | texto | sí |  |
| `sistema.nombre` | texto | sí |  |
| `sistema.clavePublicaPem` | texto | sí |  |
| `sistema.origenesPermitidos` | lista de texto o null | no |  |
| `ingreso` | ConfiguracionDeIngresoDto o null | no |  |
| `ingreso.modos` | lista de texto | sí |  |
| `ingreso.sesionHoras` | entero | sí |  |
| `ingreso.inactividadMinutos` | entero | sí |  |
| `ingreso.passkeyParaTodos` | booleano | sí |  |

**Respuestas**

- `201` Creado — TenantCreadoDto

Campos de la respuesta `201`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `tenant` | TenantDto | sí |  |
| `tenant.id` | uuid | sí |  |
| `tenant.slug` | texto | sí |  |
| `tenant.nombre` | texto | sí |  |
| `tenant.region` | texto | sí |  |
| `tenant.plan` | texto | sí |  |
| `tenant.zonaHoraria` | texto | sí |  |
| `tenant.idioma` | texto | sí |  |
| `tenant.activo` | booleano | sí |  |
| `tenant.creadoEn` | fecha y hora | sí |  |
| `tenant.ingreso` | ConfiguracionDeIngresoDto o null | no |  |
| `tenant.ingreso.modos` | lista de texto | sí |  |
| `tenant.ingreso.sesionHoras` | entero | sí |  |
| `tenant.ingreso.inactividadMinutos` | entero | sí |  |
| `tenant.ingreso.passkeyParaTodos` | booleano | sí |  |
| `apiKeyAdministrador` | ApiKeyDto | sí |  |
| `apiKeyAdministrador.id` | uuid | sí |  |
| `apiKeyAdministrador.prefijo` | texto | sí |  |
| `apiKeyAdministrador.nombre` | texto | sí |  |
| `apiKeyAdministrador.scopes` | lista de texto | sí |  |
| `apiKeyAdministrador.empresas` | lista de texto | sí |  |
| `apiKeyAdministrador.expiraEn` | fecha y hora o null | no |  |
| `apiKeyAdministrador.creadaEn` | fecha y hora | sí |  |
| `apiKeyAdministrador.sandbox` | booleano | sí |  |
| `apiKeyAdministrador.valor` | texto o null | no |  |
| `apiKeyAdministrador.sistemaId` | uuid o null | no |  |
| `apiKeyAdministrador.rotadaDe` | uuid o null | no |  |
| `apiKeyAdministrador.reemplazadaPor` | uuid o null | no |  |
| `empresa` | EmpresaDto o null | no |  |
| `empresa.id` | uuid | sí |  |
| `empresa.tenantId` | uuid | sí |  |
| `empresa.identificacion` | IdentificacionDto | sí |  |
| `empresa.identificacion.pais` | texto | sí |  |
| `empresa.identificacion.tipo` | texto | sí |  |
| `empresa.identificacion.numero` | texto | sí |  |
| `empresa.identificacion.digitoVerificacion` | texto o null | no |  |
| `empresa.nombre` | texto | sí |  |
| `empresa.logoUrl` | texto o null | no |  |
| `empresa.replyTo` | texto o null | no |  |
| `empresa.prefijoFolio` | texto | sí |  |
| `empresa.activa` | booleano | sí |  |
| `empresa.colorMarca` | texto o null | no |  |
| `administrador` | UsuarioDto o null | no |  |
| `administrador.id` | uuid | sí |  |
| `administrador.nombre` | texto | sí |  |
| `administrador.correo` | texto | sí |  |
| `administrador.identificacion` | IdentificacionDto o null | no | mismos campos que `empresa.identificacion` |
| `administrador.activo` | booleano | sí |  |
| `administrador.identidades` | lista de IdentidadExternaDto | sí |  |
| `administrador.identidades[].sistemaId` | uuid | sí |  |
| `administrador.identidades[].idExterno` | texto | sí |  |
| `administrador.roles` | lista de RolAsignadoDto | sí |  |
| `administrador.roles[].rol` | Rol (texto) | sí | valores: Administrador · Revisor · OficialCumplimiento · Auditor |
| `administrador.roles[].empresaId` | uuid o null | no |  |
| `administrador.creadoEn` | fecha y hora | sí |  |
| `administrador.ultimoAcceso` | fecha y hora o null | no |  |
| `administrador.exigePasskey` | booleano | no | por omisión false |
| `administrador.passkeys` | entero | no | por omisión 0 |
| `administrador.invitadoEn` | fecha y hora o null | no |  |
| `sistema` | SistemaIntegradoDto o null | no |  |
| `sistema.id` | uuid | sí |  |
| `sistema.codigo` | texto | sí |  |
| `sistema.nombre` | texto | sí |  |
| `sistema.clavePublicaPem` | texto | sí |  |
| `sistema.activo` | booleano | sí |  |
| `sistema.creadoEn` | fecha y hora | sí |  |
| `sistema.origenesPermitidos` | lista de texto | sí |  |
| `apiKeySistema` | ApiKeyDto o null | no | mismos campos que `apiKeyAdministrador` |

**Errores de negocio que lanza**

- `409` `slug-duplicado`
- `422` `sistema-requerido`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X POST "https://{host}/api/v1/plataforma/tenants" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA" \
  -H "Idempotency-Key: $(uuidgen)" \
  -H "Content-Type: application/json" \
  -d '{"slug":"grupo-ejemplo","nombre":"Constructora Norte S.A.S.","region":"co","zonaHoraria":"America/Bogota","empresa":{"identificacion":{"pais":"CO","tipo":"texto","numero":"900123456","digitoVerificacion":"texto"},"nombre":"Constructora Norte S.A.S.","prefijoFolio":"texto","logoUrl":"https://ejemplo.co/recurso","replyTo":"texto"},"administrador":{"nombre":"Constructora Norte S.A.S.","correo":"contacto@empresa.co","idExterno":"REF-0001"},"sistema":{"codigo":"CODIGO_EJEMPLO","nombre":"Constructora Norte S.A.S.","clavePublicaPem":"texto","origenesPermitidos":["texto"]},"ingreso":{"modos":["texto"],"sesionHoras":1,"inactividadMinutos":1,"passkeyParaTodos":true}}'
```

Respuesta `201`:

```json
{
  "tenant": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "slug": "grupo-ejemplo",
    "nombre": "Constructora Norte S.A.S.",
    "region": "co",
    "plan": "texto",
    "zonaHoraria": "America/Bogota",
    "idioma": "texto",
    "activo": true,
    "creadoEn": "2026-11-12T15:00:00Z",
    "ingreso": {
      "modos": [
        "texto"
      ],
      "sesionHoras": 1,
      "inactividadMinutos": 1,
      "passkeyParaTodos": true
    }
  },
  "apiKeyAdministrador": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "prefijo": "texto",
    "nombre": "Constructora Norte S.A.S.",
    "scopes": [
      "texto"
    ],
    "empresas": [
      "texto"
    ],
    "expiraEn": "2026-11-12T15:00:00Z",
    "creadaEn": "2026-11-12T15:00:00Z",
    "sandbox": true,
    "valor": "vsk_prueba_ejemplo",
    "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "rotadaDe": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "reemplazadaPor": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  },
  "empresa": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "tenantId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "identificacion": {
      "pais": "CO",
      "tipo": "texto",
      "numero": "900123456",
      "digitoVerificacion": "texto"
    },
    "nombre": "Constructora Norte S.A.S.",
    "logoUrl": "https://ejemplo.co/recurso",
    "replyTo": "texto",
    "prefijoFolio": "texto",
    "activa": true,
    "colorMarca": "texto"
  },
  "administrador": {
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
  },
  "sistema": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "codigo": "CODIGO_EJEMPLO",
    "nombre": "Constructora Norte S.A.S.",
    "clavePublicaPem": "texto",
    "activo": true,
    "creadoEn": "2026-11-12T15:00:00Z",
    "origenesPermitidos": [
      "texto"
    ]
  },
  "apiKeySistema": {
    "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "prefijo": "texto",
    "nombre": "Constructora Norte S.A.S.",
    "scopes": [
      "texto"
    ],
    "empresas": [
      "texto"
    ],
    "expiraEn": "2026-11-12T15:00:00Z",
    "creadaEn": "2026-11-12T15:00:00Z",
    "sandbox": true,
    "valor": "vsk_prueba_ejemplo",
    "sistemaId": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "rotadaDe": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
    "reemplazadaPor": "3fa85f64-5717-4562-b3fc-2c963f66afa6"
  }
}
```

</details>

### `GET /api/v1/plataforma/tenants/{id}`

**Obtener tenant.**

**Operador de Plataforma · API (`/api/v1`) · SDK: `obtenerTenant`**

**Parámetros**

| Nombre | En | Tipo | Requerido | Detalle |
|---|---|---|---|---|
| `id` | path | uuid | sí |  |

**Respuestas**

- `200` OK — TenantDto
- `404` No encontrado

Campos de la respuesta `200`:

| Campo | Tipo | Requerido | Detalle |
|---|---|---|---|
| `id` | uuid | sí |  |
| `slug` | texto | sí |  |
| `nombre` | texto | sí |  |
| `region` | texto | sí |  |
| `plan` | texto | sí |  |
| `zonaHoraria` | texto | sí |  |
| `idioma` | texto | sí |  |
| `activo` | booleano | sí |  |
| `creadoEn` | fecha y hora | sí |  |
| `ingreso` | ConfiguracionDeIngresoDto o null | no |  |
| `ingreso.modos` | lista de texto | sí |  |
| `ingreso.sesionHoras` | entero | sí |  |
| `ingreso.inactividadMinutos` | entero | sí |  |
| `ingreso.passkeyParaTodos` | booleano | sí |  |

**Errores de negocio que lanza**

- `404` `tenant-no-encontrado`

<details><summary>Ejemplo de petición y respuesta</summary>

```bash
curl -X GET "https://{host}/api/v1/plataforma/tenants/3fa85f64-5717-4562-b3fc-2c963f66afa6" \
  -H "X-Api-Key: $CLAVE_DE_PLATAFORMA"
```

Respuesta `200`:

```json
{
  "id": "3fa85f64-5717-4562-b3fc-2c963f66afa6",
  "slug": "grupo-ejemplo",
  "nombre": "Constructora Norte S.A.S.",
  "region": "co",
  "plan": "texto",
  "zonaHoraria": "America/Bogota",
  "idioma": "texto",
  "activo": true,
  "creadoEn": "2026-11-12T15:00:00Z",
  "ingreso": {
    "modos": [
      "texto"
    ],
    "sesionHoras": 1,
    "inactividadMinutos": 1,
    "passkeyParaTodos": true
  }
}
```

</details>
