---
titulo: "Guía de integración, resumida"
seccion: "Integración"
resumen: "Lo esencial para conectar un sistema a Validasoft: llave de API, idempotencia, webhooks firmados con su verificación, sandbox y los SDK de .NET y TypeScript, con enlaces a la guía completa y a la referencia de la API."
---

# Guía de integración, resumida

Lo esencial para conectar un sistema a Validasoft: llave de API, idempotencia, webhooks firmados con su verificación, sandbox y los SDK de .NET y TypeScript, con enlaces a la guía completa y a la referencia de la API.

Cualquier sistema (un ERP, un portal, un CRM) se conecta a Validasoft por **API REST**, **webhooks** firmados y, si quiere mostrar pantallas, **iframes** con una sesión corta. Esta página resume lo esencial; la guía completa de punta a punta es [docs/integracion.md](../../integracion.md) y cada operación está en la [referencia de la API](../api/README.md), que se genera del contrato OpenAPI.

> **Verdad:** todo lo de esta página se comprobó contra el código (cabeceras, tiempos, reintentos y firma) y contra `docs/integracion.md`. Donde `docs/05` §6.1 todavía cita otros valores (10 segundos, reintentos hasta 24 horas, deshabilitación a los 7 días, cabecera `Validasoft-Event`), manda el código: 5 segundos, 1 minuto a 6 horas, 20 fallos y `Validasoft-Event-Type`.

## Los cinco pasos

1. **Registrar el sistema y su llave.** Un administrador del grupo crea el sistema (código, nombre, clave pública PEM y orígenes) en [Sistemas integrados](configuracion-del-grupo.md#sistemas-integrados) y su API key, con los permisos mínimos y solo las empresas que integra ([API keys](configuracion-del-grupo.md#api-keys)).
2. **Identificar sus terceros y vínculos** con una **referencia externa** (`{ sistema, id }`): el sistema lo fija la llave, y las claves son privadas del grupo.
3. **Escuchar lo que pasa:** webhooks firmados o pull, con el mismo sobre.
4. **Consultar lo aceptado:** la ficha aceptada de un vínculo y la consulta de certificación antes de pagar.
5. **Mostrar las pantallas** del tercero o de la empresa con orígenes validados, si hace falta.

Sin el SRM ni el Portal todo esto funciona igual: son clientes de la API como cualquier otro.

## Autenticación con API key

La llave es `vsk_{entorno}_{grupo}_{64 caracteres}`: `live` (producción), `dev` (desarrollo) o `test` (**sandbox**). Solo se guarda su hash: si se pierde, se rota. Va en el **backend** y en un gestor de secretos, nunca en un navegador, una app móvil ni el repositorio.

| Cabecera | Para qué |
|---|---|
| `X-Api-Key` | La credencial. |
| `X-Empresa` | La empresa sobre la que se actúa; obligatoria salvo que la llave tenga una sola. |
| `Idempotency-Key` | Obligatoria en todo `POST` que crea algo (siguiente sección). |
| `X-Correlation-Id` | Opcional: un id propio que vuelve en la respuesta y en los registros. |
| `X-Usuario-Delegado` | Opcional: la aserción firmada de una persona (identidad delegada). |
| `If-Match` | Concurrencia optimista: el `ETag` de la lectura. 412 `version-desactualizada` si cambió. |
| `If-None-Match` | En la consulta de certificación, con el `ETag` que ya se tiene. |

- **Permisos (scopes):** la API responde 403 con lo que no esté en la llave. Un SRM necesita `vinculos:leer`, `vinculos:escribir`, `vinculos:bloquear`, `expedientes:leer`, `expedientes:administrar`, `certificados:leer` y `eventos:leer` o `webhooks:administrar`; un ERP de pagos, solo `consultas:leer`.
- **Alcance por empresa:** una llave con la empresa A no ve nada de la B aunque sea del mismo grupo: responde **404**, nunca 403.
- **Rotación sin cortes:** «Rotar» emite una llave nueva con los mismos permisos y la actual sigue valiendo el tiempo que se elija (24 horas por omisión, hasta 30 días). Procedimiento: rotar, poner la nueva en el gestor de secretos, desplegar, verificar y dejar que la anterior deje de valer sola (401).
- **Prueba rápida:** `curl -s https://{host}/api/v1/empresas -H "X-Api-Key: $VALIDASOFT_API_KEY"` lista las empresas que ve la llave.

## Idempotencia

Todo `POST` que crea algo lleva `Idempotency-Key` (hasta 100 caracteres), una cadena estable que identifica la operación en el sistema de origen (por ejemplo `srm-sol-00123`).

- Repetir la misma clave con el **mismo cuerpo** devuelve la misma respuesta con la cabecera `Idempotent-Replayed: true`: un reintento nunca duplica.
- La misma clave con **otro cuerpo** responde 422 `clave-idempotencia-reutilizada`; sin clave, 400 `clave-idempotencia-requerida`.
- Se recuerda **24 horas**.
- Si la respuesta original traía un **secreto** (el valor de una API key, el secreto de un webhook, un token de sesión), la repetida lo devuelve **sin él** y lo dice (`Validasoft-Secretos-Omitidos`): se guarda al recibirlo, porque no vuelve a mostrarse.

## Invitar a un tercero

El sistema crea el **vínculo** con su referencia y Validasoft reconcilia al tercero por su identificación (si ya existe en la plataforma, lo reutiliza sin tocar sus datos) y le envía la invitación por correo:

```bash
curl -s https://{host}/api/v1/vinculos \
  -H "X-Api-Key: $VALIDASOFT_API_KEY" -H "X-Empresa: $EMPRESA" -H "Idempotency-Key: srm-sol-00123" -H "Content-Type: application/json" \
  -d '{
    "tercero": { "identificacion": { "pais": "CO", "tipo": "NIT", "numero": "900123456" }, "tipoPersona": "Juridica", "nombre": "Aceros S.A.S.",
                 "contactos": [ { "nombre": "Ana Gómez", "correo": "ana@aceros.co", "rol": "Comercial" } ] },
    "tipoVinculo": "PROVEEDOR", "correoInvitacion": "ana@aceros.co",
    "referenciaExterna": { "sistema": "SRM", "id": "ADP-00123" }
  }'
```

El tercero abre el enlace del correo (de un solo uso), acepta los textos legales **de esa empresa**, sube sus documentos y lo revisan las áreas. El sistema se entera por los eventos (`vinculo.invitacion_aceptada`, `expediente.en_revision`, `expediente.observado`, `expediente.aprobado`, `certificado.emitido`…). Otras operaciones: reenviar la invitación, bloquear (`POST /vinculos/{id}/bloquear { motivo }`), desbloquear (`{ justificacion }`), desvincular, anular y buscar por referencia (`GET /vinculos/por-referencia?sistema=&clave=`). Todo evento trae las referencias de **todos** los sistemas que comparten el vínculo en `datos.referencias`. El flujo completo está en [Del vínculo a la validación](flujo-del-vinculo-a-la-validacion.md).

## Webhooks firmados

Validasoft avisa de cada evento con un `POST` firmado a la URL que se registra (`POST /api/v1/webhooks`, permiso `webhooks:administrar`, o en [Webhooks](configuracion-del-grupo.md#webhooks)). La URL debe ser `https` y pública: no se aceptan IP privadas, `localhost` ni redirecciones. El secreto (`whsec_…`) se muestra **una sola vez**. `tipos: ["*"]` recibe todos y el catálogo (42 tipos) sale de `GET /api/v1/eventos/tipos`.

**El mensaje** es un `POST` con `Content-Type: application/json`, el sobre como cuerpo y estas cabeceras:

| Cabecera | Valor |
|---|---|
| `Validasoft-Event-Id` | El `id` del evento: **la clave para deduplicar** (un reintento repite el mismo). |
| `Validasoft-Event-Type` | El tipo, por ejemplo `certificado.emitido`. |
| `Validasoft-Delivery` | El id del **intento** (cambia en cada reintento). |
| `Validasoft-Timestamp` | Segundos Unix del envío. |
| `Validasoft-Signature` | `t={timestamp},v1={hex(HMAC-SHA256(secreto, "{timestamp}.{cuerpo}"))}`; durante una rotación lleva un `v1=` por cada secreto vigente. |

```json
{ "id": "0196…", "tipo": "certificado.emitido", "version": 1, "ocurridoEn": "2026-11-11T14:03:11Z", "tenantId": "0196…", "empresaId": "0196…", "cursor": "4211",
  "datos": { "certificadoId": "0196…", "vinculoId": "0196…", "referencias": [ { "sistema": "SRM", "id": "ADP-00123" } ],
             "certificado": { "folio": "NORTE-C-2026-000001-1", "emision": 1, "estado": "Vigente", "hash": "…", "vigenteHasta": "2027-05-11", "urlVerificacion": "https://…/verificar/NORTE-C-2026-000001" } } }
```

**Verificar la firma, siempre, con el cuerpo crudo** (sin volver a serializarlo), en tiempo constante y rechazando timestamps de más de 5 minutos. Con los SDK es una línea; sin ellos, se recalcula el HMAC:

```csharp
// ASP.NET Core minimal API (net7.0 o superior).
var verificador = new VerificadorDeWebhooks(new[] { secretoActual, secretoAnterior });   // el anterior, solo durante una rotación
app.MapPost("/api/integracion/validasoft/webhook", async (HttpRequest request) =>
{
    var cuerpo = await new StreamReader(request.Body).ReadToEndAsync();
    EventoPublicoDto evento;
    try { evento = verificador.Validar(n => request.Headers[n].FirstOrDefault(), cuerpo); }
    catch (WebhookInvalidoException) { return Results.Unauthorized(); }
    if (await entregas.RegistrarAsync(evento.Id)) cola.Encolar(evento);                  // deduplica por el id del evento
    return Results.Ok();                                                                 // responde rápido; procesa en cola
});
```

```ts
// Express (Node 20 o superior): express.raw para tener el cuerpo tal cual.
app.post('/api/integracion/validasoft/webhook', express.raw({ type: 'application/json' }), async (req, res) => {
  try {
    const evento = await validarWebhook({ secretos: [secretoActual, secretoAnterior], firma: leerCabecera(req.headers, 'validasoft-signature'), cuerpo: req.body });
    if (await entregas.registrar(evento.id)) cola.encolar(evento);
    res.sendStatus(200);
  } catch {
    res.sendStatus(401);
  }
});
```

```bash
# A mano: recalcula la firma y compárala con el v1 de la cabecera.
T=$(echo "$FIRMA" | sed -E 's/^t=([0-9]+),.*/\1/')
printf '%s.%s' "$T" "$(cat cuerpo.json)" | openssl dgst -sha256 -hmac "$SECRETO" -hex | sed 's/^.* //'      # debe ser igual al v1=… de la cabecera
```

**Reglas del receptor:** responder `2xx` en **menos de 5 segundos** y procesar después; sin redirecciones; idempotente (cada evento puede llegar más de una vez, se deduplica por `Validasoft-Event-Id`); no asumir orden entre agregados distintos (el `cursor` ordena dentro del grupo).

**Reintentos.** Si no hay `2xx` a tiempo, Validasoft reintenta con espera creciente: **1 min, 5 min, 30 min, 2 h y 6 h** (seis intentos). Luego la entrega queda `Fallida` y se puede **reenviar a mano** (`POST /api/v1/webhooks/{id}/entregas/{entregaId}/reenviar`; la lista en `GET …/entregas`). Tras **20 fallos seguidos** la suscripción se **deshabilita** y los administradores reciben un aviso; al arreglar el receptor se activa (`POST /api/v1/webhooks/{id}/activar`) y se reenvía lo perdido o se lee por pull. **Rotar el secreto** (`POST /api/v1/webhooks/{id}/rotar-secreto`) deja el anterior firmando 24 horas, y el receptor acepta los dos mientras tanto.

### Qué hito produce qué evento

| Hito del flujo | Eventos |
|---|---|
| Se crea el vínculo y se invita | `vinculo.creado`, `vinculo.invitacion_enviada` |
| El tercero acepta | `vinculo.invitacion_aceptada`, `expediente.abierto`, `documento.compartido` |
| Pasa a revisión, se observa, se responde | `expediente.en_revision`, `expediente.observado`, `expediente.reenviado` |
| Verificaciones de cumplimiento | `expediente.verificacion_registrada`, `expediente.coincidencia_resuelta`, `expediente.verificacion_expirada` |
| Plazos de revisión y de cargue | `expediente.sla_por_vencer`, `expediente.sla_incumplido`, `expediente.caducado`, `expediente.reabierto` |
| Se aprueba o se rechaza | `expediente.aprobado`, `vinculo.activado`, `certificado.emitido`, `seccion.aceptada` / `expediente.rechazado` |
| Vigencias | `documento.por_vencer`, `documento.vencido`, `vinculo.por_vencer`, `vinculo.vencido` |
| Actualización y renovación | `certificado.con_pendientes`, `certificado.reemplazado`, `certificado.restablecido` |
| Bloqueo, terminación y reintegro | `vinculo.bloqueado`, `certificado.suspendido`, `vinculo.desbloqueado`, `vinculo.desvinculado`, `certificado.revocado`, `vinculo.reintegrado`, `vinculo.anulado` |
| Otros | `expediente.desaprobado`, `certificado.vencido`, `vinculo.cuenta_designada_*`, `vinculo.contactos_actualizados`, `vinculo.referencia_agregada`, `invitaciones.lote_terminado` |

## Eventos por pull

Si la red no acepta conexiones entrantes, se leen los **mismos eventos** con `GET /api/v1/eventos?desde={cursor}&tipos=a,b&limite=100` (`eventos:leer`; hasta 200 por página). Se guarda el `cursor` del último evento procesado **después** de procesarlo y se retoma desde ahí: el cursor solo crece y el pull no se salta ninguno. Sin `X-Empresa` se reciben las empresas de la llave; con él, solo esa. También hay un flujo SSE en `GET /api/v1/eventos/stream` ([Mensajería](arquitectura-mensajeria-y-tiempo-real.md#tiempo-real-sse)).

## Consulta de certificación

Antes de **pagar o contratar**, se pregunta si el tercero está certificado **en esa empresa** (`consultas:leer`, con `X-Empresa`):

```bash
curl -si "https://{host}/api/v1/consultas/certificacion?pais=CO&tipoIdentificacion=NIT&numero=900123456&tipoVinculo=PROVEEDOR" \
  -H "X-Api-Key: $LLAVE_DE_MARCO" -H "X-Empresa: $EMPRESA"
# 200 …  ETag: "c-9f2a…"   Cache-Control: private, max-age=300
```

La respuesta trae `certificado`, `folio`, `estado`, `estadoVinculo`, `vigenteHasta`, `revalidacionPendiente[]`, `documentosVencidos[]`, `bloqueado`, `motivoBloqueo`, `cuentaDesignada`, `enRevision` y `enVerificacion` (estas dos, informativas). **Regla de oro: pagar solo si `certificado: true` y `bloqueado: false`.** `bloqueado` es `true` por `Bloqueo` (vínculo bloqueado o certificado suspendido), `Revocado` (certificado revocado o vínculo anulado o desvinculado), `Vencido` o `PlazoRevalidacion` (un certificado con pendientes cuyo plazo venció). Sin vínculo con la empresa responde `200 { certificado: false, estadoVinculo: "SinVinculo" }` y no revela si el tercero está en la plataforma.

Con `ETag`: dentro de los 5 minutos (`max-age=300`) se usa la copia; después se pregunta con `If-None-Match` y un `304` confirma que sigue igual. Si Validasoft no responde (más de 2 segundos o 5xx), el sistema responde el mismo contrato desde su copia local, marcando `fuenteRespaldo: true` y la antigüedad, y la alimenta con los eventos.

## Identidad delegada y pantallas embebidas

Para llamar **como una persona** del sistema (porque se van a embeber pantallas o porque el permiso debe depender de sus roles), cada petición lleva la API key del **sistema** y `X-Usuario-Delegado` con una aserción: un JWT firmado con la clave **privada** del sistema (RS256 con RSA de 2048 bits o más, o ES256 con P-256), con `iss` = el código del sistema, `aud` = `validasoft`, `sub`, `name`, `email`, un `jti` nuevo por aserción y `exp` a 5 minutos o menos. Vale una sola vez. El permiso efectivo es la **intersección** entre la llave y los roles de la persona.

Para **embeber** pantallas en un `<iframe>`, el sistema registra el origen de su página; su backend canjea la identidad de la persona por una **sesión de 30 minutos** (`POST /api/v1/sesiones-usuario`) y la página se la pasa al iframe por `postMessage`. Hay dos embebidos: el de las personas de la empresa (bandeja, certificados, configuración) y el del **tercero** (bóveda, expediente y ficha, con `POST /api/v1/sesiones-tercero`). Los errores 401 frecuentes: `llave-sin-sistema`, `sistema-inactivo`, `asercion-firma-invalida`, `asercion-vencida`, `asercion-vigencia-excesiva`, `asercion-sin-jti`, `asercion-reutilizada`, `asercion-incompleta` y `usuario-inactivo`. Rotar la clave del sistema invalida las aserciones anteriores.

## Sandbox

El **sandbox** es un entorno de pruebas aislado del grupo, en la misma URL. Una llave `vsk_test_…` opera en él y nada de lo que se haga llega a producción. Se crea al emitir la primera llave de prueba (o con `POST /api/v1/sandbox`).

| Aspecto | En el sandbox |
|---|---|
| Configuración | Copia la de producción (empresas, tipos, áreas, flujos, motivos, plantillas, sistemas y sus claves públicas) y **ningún dato**. |
| Empresas | Cada una tiene una copia con **otro id**: el `X-Empresa` de una llave de prueba es el de la copia (`GET /api/v1/sandbox` lo da, con `produccionId` al lado). El nombre termina en «(sandbox)» y el prefijo de folio lleva una `T` por delante. |
| Terceros | Aislados: el mismo NIT en el sandbox es otro tercero, con su propia bóveda. |
| Correos | **No salen**: quedan en un buzón que se lee por API (`GET /api/v1/sandbox/correos`), con el enlace de acceso incluido. |
| IA y listas | **Simuladas**, sin red ni costo: la IA lee marcas del archivo (`[[clave=valor]]`) y un tercero con «coincidencia» en el nombre produce una coincidencia. |
| Certificados | **No son verificables** en público. |
| Respuestas | Llevan la cabecera `Validasoft-Entorno: sandbox`. |
| Reiniciar | `POST /api/v1/sandbox/reiniciar` borra todo lo hecho y siembra de nuevo con la configuración de producción de ese momento; conserva los ids de empresa, las llaves de prueba y los cursores. |

Una llave de producción no sirve en el sandbox ni al revés, y las llaves se administran siempre en producción. Operarlo requiere el permiso `sandbox:administrar`.

## SDK de .NET y de TypeScript

Los dos SDK se versionan juntos con la API (`1.x`) y ponen solos las cabeceras, el `Idempotency-Key` de cada `POST` (el mismo en los reintentos) y reintentan solo lo idempotente (`GET`, `PUT` y los `POST` con clave explícita) ante 5xx, 408, 429 o error de red.

| | .NET | TypeScript |
|---|---|---|
| Paquete | `dotnet add package Validasoft.Sdk` (y `Validasoft.Contratos`, los DTO) | `npm install @validasoft/sdk` |
| Plataformas | `net7.0`, `net8.0` y `net10.0`, sin dependencias fuera de la BCL | Node 20 o superior (o navegador): `fetch` y Web Crypto |
| Cliente | `ClienteValidasoft` con `OpcionesValidasoft { UrlBase, ApiKey, EmpresaId, Firmante }`; `ParaEmpresa(id)` y `ComoUsuario(usuario)` | `new ClienteValidasoft({ urlBase, apiKey, empresaId, firmante })`; `paraEmpresa(id)` y `comoUsuario(usuario)` |
| Webhooks | `VerificadorDeWebhooks` (varios secretos durante una rotación, tolerancia de 5 minutos) | `validarWebhook`, `verificarWebhook`, `leerCabecera` |
| Aserciones | `FirmanteDeAsercionesDelegadas(codigo, pem)` | `crearFirmanteDeAserciones(codigo, pem)` |
| Más | Vínculos, invitaciones, expedientes, certificados, consulta de certificación, eventos (`RecorrerAsync`), sandbox y sesiones embebidas | Lo mismo, más `montarEmbebido` para el `<iframe>` |

El código fuente y los `CHANGELOG` están en `src/Validasoft.Sdk` y `packages/sdk`. El contrato OpenAPI 3.1 está en `{host}/openapi/v1.json`, la fuente para generar un cliente en otro lenguaje.

## Errores y límites

Toda respuesta de error es `application/problem+json` (RFC 9457) con un `codigo` estable y un `traceId`: **se decide con el `codigo`, no con el texto**.

| HTTP | Significa | Qué hacer |
|---|---|---|
| 400 | Petición mal formada | Corregir y reenviar. |
| 401 | Credencial inválida, vencida o revocada, o aserción inválida | Revisar la llave y la aserción; no reintentar igual. |
| 403 | La credencial no tiene ese permiso | Pedir el permiso. |
| 404 | No existe **o no es del alcance** (grupo o empresa) | Revisar el id y `X-Empresa` (en el sandbox, el de la copia). |
| 409 | Conflicto con el estado actual | Leer el estado y decidir. |
| 410 | El enlace del tercero ya se usó o venció | Reenviar la invitación. |
| 412 | `If-Match` desactualizado | Releer y reintentar. |
| 422 | Regla de negocio o dato inválido | Corregir el dato. |
| 429 | Demasiadas peticiones o límite del plan | Esperar `Retry-After`. |
| 5xx | Falla de Validasoft | Reintentar con espera (el SDK lo hace con lo idempotente). |

| Límite | Valor |
|---|---|
| Aserción delegada | 5 minutos y una sola vez |
| Sesión embebida | 30 minutos |
| Caché de verificación de la llave | Hasta 5 minutos tras revocar o rotar en otro nodo |
| Rotación de una llave | La anterior puede seguir hasta 30 días (24 horas por omisión) |
| Paginación | `limite` de 1 a 200 (50 por omisión), cursor opaco |
| Archivos del tercero | 20 MB y 50 páginas por documento; los binarios nunca pasan por la API |
| Peticiones por credencial | `/api/v1` aún no tiene tope por credencial, pero cuenta para el plan del grupo; `/public/v1` limita por IP |

La API se versiona por URL (`/api/v1`): dentro de `v1` los cambios son **aditivos**, así que se deben tolerar campos y valores de enumeración desconocidos; un cambio incompatible llega como `v2` y `v1` sigue 12 meses. Lo que se retira se anuncia con las cabeceras `Deprecation` y `Sunset`.

## Lista para pasar a producción

- El flujo completo probado en el sandbox: invitación, carga, aprobación, `certificado.emitido`, consulta, bloqueo y consulta.
- La llave de producción emitida **por sistema**, con los permisos mínimos y solo las empresas del sistema, guardada en el gestor de secretos; el `X-Empresa` de producción en la configuración.
- Webhook de producción creado con `https`, verificación sobre el cuerpo crudo, deduplicación por `Validasoft-Event-Id` y respuesta en menos de 5 segundos.
- El cursor de pull (si se usa) guardado **después** de procesar cada evento.
- La consulta de certificación con `ETag`, caché de 5 minutos y respaldo local.
- Manejo de 404, 422, 429 y 5xx por `codigo`, con `Idempotency-Key` estable por operación.
- La rotación de la llave y del secreto del webhook, ensayada; alertas por webhook deshabilitado y por 401.
- Los orígenes registrados, si se embeben pantallas.
- La llave de prueba, fuera del código de producción.

La referencia completa de cada recurso está en la [API de Validasoft](../api/README.md), con las [convenciones comunes](../api/convenciones.md) y, por ejemplo, [vínculos](../api/vinculos.md), [expedientes](../api/expedientes.md), [certificados](../api/certificados.md), [integración](../api/integracion.md) y [sandbox](../api/sandbox.md).
