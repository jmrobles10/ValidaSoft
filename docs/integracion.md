# Guía de integración con Validasoft

| Estado | Fecha | Responsable |
|---|---|---|
| Vigente (implementado: H9 y sandbox, SB-1…SB-12) | 2026-11-11 | Arquitectura |

**Para quién es.** Para el equipo de un sistema que se integra con Validasoft (un ERP, un portal de proveedores, un sistema de compras o de
pagos). Está escrita para que una persona externa se integre leyendo **solo este documento**: cada paso trae el ejemplo con `curl`, con el
SDK de .NET y con el de TypeScript. El detalle de cada endpoint y DTO está en [05 — API, contratos y webhooks](05-api-contratos-y-webhooks.md);
las decisiones de fondo, en el [registro](decisiones/registro.md).

**Qué hace Validasoft por ti.** Valida a terceros (proveedores, clientes, empleados…) para tus empresas: les pide los documentos, los revisa
con IA y con tus áreas, y emite un **certificado verificable** que tu sistema consulta antes de pagar o contratar. Tu sistema invita, escucha
(webhooks) o pregunta (pull, consulta de certificación) y, si quieres, embebe las pantallas de revisión en tu propia aplicación.

## Índice

1. [Conceptos en dos minutos](#1-conceptos-en-dos-minutos)
2. [Alta del sistema integrador desde la consola](#2-alta-del-sistema-integrador-desde-la-consola)
3. [Llaves: producción y sandbox](#3-llaves-producción-y-sandbox)
4. [El primer llamado](#4-el-primer-llamado)
5. [Sandbox: probar sin riesgo](#5-sandbox-probar-sin-riesgo)
6. [Invitar a un tercero](#6-invitar-a-un-tercero)
7. [Identidad delegada: actuar en nombre de una persona](#7-identidad-delegada-actuar-en-nombre-de-una-persona)
8. [Pantallas embebidas](#8-pantallas-embebidas)
9. [Webhooks](#9-webhooks)
10. [Eventos por pull](#10-eventos-por-pull)
11. [Consulta de certificación](#11-consulta-de-certificación)
12. [Errores](#12-errores)
13. [Límites](#13-límites)
14. [Versionado y deprecación](#14-versionado-y-deprecación)
15. [Lista de verificación para pasar a producción](#15-lista-de-verificación-para-pasar-a-producción)

---

## 1. Conceptos en dos minutos

| Concepto | Qué es |
|---|---|
| **Grupo** (tenant) | Tu cliente de Validasoft: un grupo empresarial. Toda credencial pertenece a un grupo. |
| **Empresa** | Una razón social del grupo. Cada empresa decide en privado (sus flujos, sus áreas, sus aprobaciones). Casi toda llamada es **de una empresa** y lo dice la cabecera `X-Empresa`. |
| **Sistema integrado** | Tu sistema (código `SRM`, `MARCO`…) registrado en el grupo con su **clave pública**. Fija quién eres en las referencias y permite actuar en nombre de personas. |
| **API key** | La credencial de tu backend: `X-Api-Key: vsk_{entorno}_{grupo}_{secreto}`. Lleva **permisos** (scopes) y **alcance** (empresas). |
| **Tercero** | La persona o empresa que se valida. Es única en toda la plataforma por su identificación (NIT, CC…): sube cada documento una sola vez y lo comparte con cada empresa que lo invita. |
| **Vínculo** | La relación de una empresa con un tercero (proveedor, cliente, empleado…). Tiene estado (`Invitado`, `EnValidacion`, `Activo`, `Bloqueado`…). |
| **Expediente** | La revisión de un vínculo: requisitos, veredictos de las áreas, aprobación. Al aprobarse emite el **certificado**. |
| **Certificado** | El comprobante verificable (folio, huella, PDF) con su estado: `Vigente`, `ConPendientes`, `Suspendido`, `Vencido`, `Revocado`. |
| **Evento** | Lo que pasó (`certificado.emitido`, `vinculo.bloqueado`…): llega por **webhook** firmado o se lee por **pull**. |

Hay **dos entornos**: **producción** (llaves `vsk_live_…`) y **sandbox** (llaves `vsk_test_…`), un entorno de pruebas aislado con la misma
URL. Tu código es el mismo en los dos; cambia la llave (y el `X-Empresa`, ver §5).

```
Tu backend ── X-Api-Key + X-Empresa ──▶ Validasoft API ──▶ invita al tercero (correo con enlace)
     ▲                                        │
     ├──── webhook firmado (certificado.emitido, vinculo.bloqueado…) ◀──┤
     ├──── pull GET /eventos?desde=cursor  ◀────────────────────────────┤
     └──── GET /consultas/certificacion (ETag/304) ─────────────────────┘
Tu aplicación ── <iframe> con la sesión que canjea tu backend ──▶ pantallas de revisión de Validasoft
```

## 2. Alta del sistema integrador desde la consola

La hace un **administrador del grupo** en la aplicación web de Validasoft (`{host}/app/`), sección **Configuración** (el engranaje).

1. **Registra tu sistema.** Configuración › **Sistemas** › «Registrar sistema»:
   - **Código**: mayúsculas, único en el grupo (`SRM`, `MARCO`, `PORTAL`).
   - **Nombre**: el que verán las personas.
   - **Clave pública** (PEM): RSA de 2048 bits o más, o EC P-256. **La clave privada no sale nunca de tu sistema.** Se rechazan las claves privadas.
   - **Orígenes permitidos**: solo si vas a **embeber** pantallas (§8): `https://srm.cliente.co`. Un origen por línea, `https`.

   Genera el par así (la pública va a la consola; la privada, a tu gestor de secretos):

   ```bash
   openssl genpkey -algorithm RSA -pkeyopt rsa_keygen_bits:2048 -out srm-privada.pem        # PKCS#8
   openssl pkey -in srm-privada.pem -pubout -out srm-publica.pem
   ```

   Por API: `POST /api/v1/sistemas { "codigo": "SRM", "nombre": "SRM del cliente", "clavePublicaPem": "-----BEGIN PUBLIC KEY-----…", "origenesPermitidos": ["https://srm.cliente.co"] }` (permiso `sistemas:administrar`).

2. **Emite su API key.** Configuración › **API keys** › «Nueva API key»: nombre («SRM producción»), el **sistema** que acabas de crear, **solo las empresas** que ese sistema atiende y los **permisos mínimos** (tabla de §3). La clave completa se muestra **una sola vez**: va al gestor de secretos del sistema, nunca al repositorio.
3. **Emite también su llave de prueba** (la misma pantalla, con el interruptor **Llave de sandbox (pruebas)**): crea tu sandbox (§5) y te deja integrar sin tocar producción.
4. **Registra el webhook** (opcional si usas pull): Configuración › **Webhooks** › «Nuevo webhook» (§9).
5. **Da roles a las personas** que usarán la pantalla embebida (Configuración › **Usuarios**), o déjalas entrar por identidad delegada (§7).

## 3. Llaves: producción y sandbox

**Formato:** `vsk_{entorno}_{grupo}_{64 caracteres}` — `live` (producción), `dev` (instalaciones de desarrollo) o `test` (**sandbox**). Solo se guarda su hash: si la pierdes, rótala.

**Permisos mínimos por tipo de sistema** (la API rechaza con `403` lo que no esté en la llave):

| Sistema | Permisos (scopes) |
|---|---|
| SRM (una instancia = una empresa) | `vinculos:leer`, `vinculos:escribir`, `vinculos:bloquear`, `expedientes:leer`, `expedientes:administrar`, `certificados:leer`, `eventos:leer` (pull) o `webhooks:administrar`, y los de lectura que use su pantalla embebida (`configuracion:leer`, `flujos:leer`) |
| Marco / ERP de pagos | `consultas:leer` (+ `eventos:leer` si mantiene una copia local por pull) |
| Portal | `vinculos:leer`, `documentos:leer`, `terceros:leer` |
| Probar en el sandbox | los de arriba + `sandbox:administrar` (consultar el buzón y reiniciar) |

**Alcance por empresa.** Una llave con `[A1]` no ve nada de `A2` aunque sea del mismo grupo: responde `404`, nunca `403`, para no revelar que existe. Emite una llave **por sistema y por entorno**, con las empresas de ese sistema.

**Rotar sin cortar el servicio** (Configuración › API keys › «Rotar», o `POST /api/v1/api-keys/{id}/rotar`): sale una llave nueva con los mismos permisos, alcance y sistema, y la actual **sigue valiendo** el tiempo que elijas (24 h por omisión, hasta 30 días; «ahora» corta de inmediato). Procedimiento: rotar → poner la nueva en el gestor de secretos y desplegar → verificar que el sistema la usa → la anterior deja de valer sola (`401`). Revocar es inmediato en el nodo que atiende y, como mucho, 5 minutos después en los demás.

**Nunca** pongas la API key en un navegador, una app móvil o el repositorio. Para las pantallas embebidas el navegador usa una **sesión corta** que canjea tu backend (§8).

## 4. El primer llamado

Todo llamado lleva:

| Cabecera | Para qué |
|---|---|
| `X-Api-Key` | Tu credencial. |
| `X-Empresa` | La empresa sobre la que actúas (obligatoria salvo que tu llave tenga una sola empresa). |
| `Idempotency-Key` | **Obligatoria en todo `POST` que crea algo**: una cadena que identifica la operación (`srm-sol-00123`). Repetir la misma clave con el mismo cuerpo devuelve la misma respuesta (`Idempotent-Replayed: true`), así un reintento nunca duplica; la misma clave con otro cuerpo da `422 clave-idempotencia-reutilizada`. Vale 24 horas. |
| `X-Correlation-Id` | Opcional: un id tuyo que vuelve en la respuesta y en los registros; cítalo al pedir soporte. |
| `If-None-Match` | En la consulta de certificación, con el `ETag` que ya tienes (§11). |

Prueba que tu llave funciona (lista las empresas que ve):

```bash
curl -s https://valida.cliente.co/api/v1/empresas -H "X-Api-Key: $VALIDASOFT_API_KEY"
```

```csharp
// .NET: un cliente tipado por IHttpClientFactory (net7.0, net8.0 o net10.0).
services.AddSingleton(new OpcionesValidasoft
{
    UrlBase = new Uri(config["Validasoft:UrlBase"]!),
    ApiKey = config["Validasoft:ApiKey"]!,
    EmpresaId = Guid.Parse(config["Validasoft:EmpresaId"]!),
    Firmante = new FirmanteDeAsercionesDelegadas("SRM", File.ReadAllText(config["Validasoft:ClavePrivadaPemArchivo"]!)) // solo para §7 y §8
});
services.AddHttpClient<IClienteValidasoft, ClienteValidasoft>(http => http.Timeout = TimeSpan.FromSeconds(5));
```

```ts
// TypeScript (backend con Node 20+; nunca en el navegador).
import { ClienteValidasoft, crearFirmanteDeAserciones } from '@validasoft/sdk';

const validasoft = new ClienteValidasoft({
  urlBase: process.env.VALIDASOFT_URL!,
  apiKey: process.env.VALIDASOFT_API_KEY!,
  empresaId: process.env.VALIDASOFT_EMPRESA_ID,
  firmante: crearFirmanteDeAserciones('SRM', process.env.VALIDASOFT_CLAVE_PRIVADA_PKCS8!), // solo para §7 y §8
});
```

Los SDK ponen solas las cabeceras, el `Idempotency-Key` de cada `POST` (el mismo en los reintentos) y reintentan solo lo idempotente
(`GET`, `PUT` y los `POST` con clave explícita) ante `5xx`, `408`, `429` o error de red. Se instalan con `dotnet add package Validasoft.Sdk` y
`npm install @validasoft/sdk`; sus README y CHANGELOG viven en `src/Validasoft.Sdk` y `packages/sdk`.

## 5. Sandbox: probar sin riesgo

El sandbox es un **entorno de pruebas aislado de tu grupo**, en la misma URL. Una llave `vsk_test_…` opera en él, y nada de lo que hagas ahí llega a producción.

**Qué es.** Un grupo hermano creado **cuando lo pides** (al emitir tu primera llave de prueba, o con `POST /api/v1/sandbox`). Hereda **tu configuración** —empresas, tipos de vínculo y de documento, áreas, flujos, motivos, plantillas, sistemas integrados y sus claves públicas— y **ningún dato**: ni terceros, ni vínculos, ni documentos, ni usuarios, ni llaves.

| Aspecto | En el sandbox |
|---|---|
| **Empresas** | Cada empresa tiene una **copia de pruebas con otro id**: el `X-Empresa` de una llave de prueba es el id de la copia (`GET /api/v1/sandbox` lo da: `empresas[].id`, con `produccionId` al lado). Su nombre termina en «(sandbox)» y su prefijo de folio lleva una `T` por delante (`TNORTE-C-…`). El id de la empresa real no existe para una llave de prueba (`404`), ni al revés. |
| **Terceros** | Aislados: el mismo NIT en el sandbox es **otro tercero** distinto del real (con su propia bóveda). Un tercero de producción nunca ve un vínculo de pruebas. |
| **Correos** | **No salen.** Cada correo que Validasoft habría enviado (invitación, código OTP, avisos…) queda en el **buzón del sandbox**, que lees por API con el enlace de acceso incluido. |
| **IA y listas restrictivas** | **Simuladas**, sin red ni costo. La IA lee marcas del propio archivo (`[[clave=valor]]` dentro del PDF o imagen); un tercero cuyo nombre contenga «coincidencia» produce una coincidencia en listas. |
| **Personas** | Quien llega por identidad delegada nace **administrador** del sandbox (acotado por los permisos de tu llave): no hay a quién pedirle los roles. |
| **Certificados** | **No son verificables** públicamente (`GET /public/v1/verificar/{folio}` responde 404) y no aparecen en la consola de Plataforma ni en sus cuentas. |
| **Webhooks y eventos** | Los del sandbox: crea las suscripciones **con tu llave de prueba**; sus eventos son del tenant del sandbox. |
| **Respuestas** | Todas llevan la cabecera `Validasoft-Entorno: sandbox`. |
| **Llaves** | Se administran en producción (también las de prueba): una credencial del sandbox no crea ni rota llaves (`422 no-disponible-en-sandbox`). |

**Operarlo** (permiso `sandbox:administrar`; funciona con la llave de producción y con la de prueba, siempre sobre el sandbox):

| Operación | Endpoint | SDK .NET · TypeScript |
|---|---|---|
| Estado, empresas y cuántos correos hay | `GET /api/v1/sandbox` | `Sandbox.ObtenerAsync()` · `sandbox.sandbox.obtener()` |
| Crear o ponerlo al día con producción (idempotente) | `POST /api/v1/sandbox` | `AsegurarAsync()` · `asegurar()` |
| **Reiniciar**: borra todo lo hecho y siembra de nuevo | `POST /api/v1/sandbox/reiniciar` | `ReiniciarAsync()` · `reiniciar()` |
| Leer el buzón (`para`, `asunto`, `limite`, `cursor`) | `GET /api/v1/sandbox/correos` | `CorreosAsync()` · `correos()` |
| Esperar un correo (con su enlace) | — | `EsperarCorreoAsync(para)` · `esperarCorreo(para)` |
| Un correo · vaciar el buzón | `GET /api/v1/sandbox/correos/{id}` · `DELETE /api/v1/sandbox/correos` | `CorreoAsync` · `VaciarCorreosAsync` |

Reiniciar conserva las empresas (mismo id), las llaves de prueba y los cursores de eventos; borra vínculos, expedientes, certificados,
documentos y archivos de los terceros de pruebas, usuarios, suscripciones de webhook, eventos y correos, y vuelve a sembrar la
configuración de producción **de ese momento** (así recoge un flujo nuevo). Si creas una empresa en producción después, `POST /sandbox` la copia.

```bash
# 1. Con la llave de producción del administrador: emite una llave de prueba (esto crea el sandbox)
curl -s https://valida.cliente.co/api/v1/api-keys -H "X-Api-Key: $LLAVE_ADMIN" -H "Idempotency-Key: srm-sandbox-1" -H "Content-Type: application/json" \
  -d '{ "nombre":"SRM sandbox", "scopes":["vinculos:leer","vinculos:escribir","expedientes:administrar","certificados:leer","consultas:leer","eventos:leer","webhooks:administrar","sandbox:administrar"],
        "empresas":null, "expiraEn":null, "sandbox":true, "sistemaId":"<id del sistema>" }'      # → "valor": "vsk_test_…"

# 2. Con la llave de prueba: ¿qué empresa uso en X-Empresa?
curl -s https://valida.cliente.co/api/v1/sandbox -H "X-Api-Key: $LLAVE_DE_PRUEBA"

# 3. Invitar (la invitación no sale: queda en el buzón) y leer el enlace
curl -s https://valida.cliente.co/api/v1/vinculos -H "X-Api-Key: $LLAVE_DE_PRUEBA" -H "X-Empresa: $EMPRESA_SANDBOX" -H "Idempotency-Key: prueba-1" -H "Content-Type: application/json" -d @vinculo.json
curl -s "https://valida.cliente.co/api/v1/sandbox/correos?para=ana@aceros.co" -H "X-Api-Key: $LLAVE_DE_PRUEBA"   # → datos[0].enlaces[0]
```

```csharp
var sandbox = new ClienteValidasoft(http, new OpcionesValidasoft { UrlBase = url, ApiKey = llaveDePrueba });
var estado = await sandbox.Sandbox.ObtenerAsync();
var empresa = estado.Empresas.Single(e => e.ProduccionId == empresaReal).Id;          // el X-Empresa del sandbox
var cliente = sandbox.ParaEmpresa(empresa);
await cliente.Vinculos.CrearAsync(nuevoVinculo, "prueba-1");
var correo = await cliente.Sandbox.EsperarCorreoAsync("ana@aceros.co");               // la invitación del buzón
var enlaceDelTercero = correo.Enlaces.First(e => e.Contains("/e/"));                   // ábrelo para seguir como el tercero
await cliente.Sandbox.ReiniciarAsync();                                                // vuelve a empezar
```

```ts
const sandbox = new ClienteValidasoft({ urlBase, apiKey: llaveDePrueba });
const { empresas } = await sandbox.sandbox.obtener();
const cliente = sandbox.paraEmpresa(empresas.find((e) => e.produccionId === empresaReal)!.id);
await cliente.vinculos.crear(nuevoVinculo, 'prueba-1');
const correo = await cliente.sandbox.esperarCorreo('ana@aceros.co');
const enlaceDelTercero = correo.enlaces.find((e) => e.includes('/e/'));
await cliente.sandbox.reiniciar();
```

**Diferencias que debes tener presentes:** el id de empresa (arriba), que los correos no salen, que los proveedores externos son simulados
y que una llave de producción no sirve en el sandbox ni al revés (`vsk_test_…` solo autentica en el sandbox). **Qué hacer si falla un paso
de la guía:** reinicia y repite; todo lo demás es idéntico a producción.

## 6. Invitar a un tercero

Tu sistema crea el **vínculo** con su referencia y Validasoft **reconcilia al tercero por su identificación** (si ya existe en la
plataforma lo reutiliza sin tocar sus datos) y le envía la invitación por correo. Usa una clave de idempotencia estable (el id de la solicitud en tu sistema).

```bash
curl -s https://valida.cliente.co/api/v1/vinculos \
  -H "X-Api-Key: $VALIDASOFT_API_KEY" -H "X-Empresa: $EMPRESA" -H "Idempotency-Key: srm-sol-00123" -H "Content-Type: application/json" \
  -d '{
    "tercero": { "identificacion": { "pais": "CO", "tipo": "NIT", "numero": "900123456" }, "tipoPersona": "Juridica", "nombre": "Aceros S.A.S.",
                 "contactos": [ { "nombre": "Ana Gómez", "correo": "ana@aceros.co", "rol": "Comercial" } ] },
    "tipoVinculo": "PROVEEDOR", "correoInvitacion": "ana@aceros.co",
    "referenciaExterna": { "sistema": "SRM", "id": "ADP-00123" }
  }'
```

```csharp
var vinculo = await validasoft.Vinculos.CrearAsync(new CrearVinculoDto(null,
    new CrearTerceroDto(new IdentificacionDto("CO", "NIT", "900123456"), "Juridica", "Aceros S.A.S.", null,
        [new ContactoDto("Ana Gómez", "ana@aceros.co", null, "Comercial")]),
    "PROVEEDOR", null, "ana@aceros.co", new ReferenciaExternaDto("SRM", "ADP-00123")), claveIdempotencia: "srm-sol-00123");
// Reenviar: await validasoft.Invitaciones.EnviarAsync(vinculo.Id, new InvitacionDto(null));
// Buscar después por tu referencia: await validasoft.Vinculos.PorReferenciaAsync("SRM", "ADP-00123");
```

```ts
const vinculo = await validasoft.vinculos.crear(
  { tercero: { identificacion: { pais: 'CO', tipo: 'NIT', numero: '900123456' }, tipoPersona: 'Juridica', nombre: 'Aceros S.A.S.',
      contactos: [{ nombre: 'Ana Gómez', correo: 'ana@aceros.co', rol: 'Comercial' }] },
    tipoVinculo: 'PROVEEDOR', correoInvitacion: 'ana@aceros.co', referenciaExterna: { sistema: 'SRM', id: 'ADP-00123' } },
  'srm-sol-00123',
);
```

Qué pasa después: el tercero abre el enlace del correo (un solo uso; los proveedores confirman además un código OTP que les llega al mismo
correo), acepta los textos legales de **esa empresa**, carga sus documentos y los revisan las áreas de la empresa. Tu sistema se entera por
los **eventos** (`vinculo.invitacion_aceptada`, `expediente.en_revision`, `expediente.observado`, `expediente.aprobado`,
`certificado.emitido`…). Si tu flujo no tiene requisitos ni áreas manuales, el expediente se abre y aprueba solo
(`POST /api/v1/vinculos/{id}/expedientes`). Otras operaciones del vínculo: bloquear (`POST /vinculos/{id}/bloquear { motivo }`), desbloquear
(`{ justificacion }`), desvincular, anular y consultar por referencia. Cada vínculo guarda **las referencias de todos los sistemas** que lo
comparten, y todo evento las trae en `datos.referencias`.

## 7. Identidad delegada: actuar en nombre de una persona

Para que tu sistema llame a la API **como una persona suya** (porque va a embeber pantallas, o porque quieres que el permiso dependa de sus
roles), cada petición lleva, además de la API key **del sistema**, la cabecera `X-Usuario-Delegado` con una **aserción**: un JWT firmado
con la **clave privada de tu sistema**.

| Claim | Valor |
|---|---|
| Algoritmo | `RS256` (clave RSA ≥ 2048) o `ES256` (EC P-256) |
| `iss` | El **código** de tu sistema (`SRM`) |
| `aud` | `validasoft` |
| `sub` | El id de la persona **en tu sistema** |
| `name` · `email` | Su nombre y su correo |
| `jti` | Un identificador **nuevo por aserción** (un UUID): cada una vale **una sola vez** |
| `iat` · `exp` | `exp` a **5 minutos o menos** de ahora |

El **permiso efectivo** es la **intersección** de lo que permite tu llave y lo que permiten los roles de la persona en Validasoft (y las
empresas, igual). La primera vez que llega alguien que no existe, se crea (o se enlaza con quien ya tiene ese correo); un administrador le asigna roles.
Los SDK firman **una aserción nueva en cada intento**:

```csharp
using var firmante = new FirmanteDeAsercionesDelegadas("SRM", File.ReadAllText("srm-privada.pem"));
var laura = new ClienteValidasoft(http, new OpcionesValidasoft { UrlBase = url, ApiKey = llave, EmpresaId = empresa, Firmante = firmante })
    .ComoUsuario(new UsuarioDelegado("srm-laura", "Laura Pérez", "laura@cliente.co"));
await laura.Vinculos.ObtenerAsync(vinculoId);          // como Laura
```

```ts
const firmante = crearFirmanteDeAserciones('SRM', clavePrivadaPkcs8);   // PEM PKCS#8
const laura = new ClienteValidasoft({ urlBase, apiKey, empresaId, firmante }).comoUsuario({ sub: 'srm-laura', nombre: 'Laura Pérez', correo: 'laura@cliente.co' });
await laura.vinculos.obtener(vinculoId);
```

Sin SDK (Bash + OpenSSL, RS256):

```bash
b64() { openssl base64 -A | tr '+/' '-_' | tr -d '='; }
AHORA=$(date +%s)
CABECERA=$(printf '{"alg":"RS256","typ":"JWT"}' | b64)
CARGA=$(printf '{"iss":"SRM","aud":"validasoft","sub":"srm-laura","name":"Laura Pérez","email":"laura@cliente.co","jti":"%s","iat":%s,"exp":%s}' \
  "$(uuidgen)" "$AHORA" "$((AHORA+300))" | b64)
FIRMA=$(printf '%s.%s' "$CABECERA" "$CARGA" | openssl dgst -sha256 -sign srm-privada.pem | b64)
curl -s https://valida.cliente.co/api/v1/vinculos/$VINCULO -H "X-Api-Key: $LLAVE_DEL_SISTEMA" -H "X-Empresa: $EMPRESA" -H "X-Usuario-Delegado: $CABECERA.$CARGA.$FIRMA"
```

Fallos: `401` con `codigo` `llave-sin-sistema` (la llave no es de un sistema), `sistema-inactivo`, `asercion-invalida`, `asercion-firma-invalida`
(la firma no corresponde a la clave pública registrada), `asercion-vencida`, `asercion-vigencia-excesiva` (`exp` a más de 5 min), `asercion-sin-jti`,
`asercion-reutilizada` (el `jti` ya se usó), `asercion-incompleta` (falta `sub`, `name` o `email`) o `usuario-inactivo`. **Rotar la clave** de tu sistema
(`PUT /sistemas/{id}/clave-publica`) invalida de inmediato las aserciones firmadas con la anterior.

## 8. Pantallas embebidas

Puedes mostrar dentro de tu aplicación las pantallas de Validasoft (la bandeja de revisión, los certificados, la configuración) en un `<iframe>`.
Las dos piezas: **tu backend** canjea la identidad de la persona por una **sesión de 30 minutos**, y **tu página** se la pasa al iframe por `postMessage`.

1. **Registra el origen de tu página** en el sistema (§2: `origenesPermitidos`). Solo esos sitios pueden alojar el iframe (cabecera `frame-ancestors`) y recibir los mensajes.
2. **El backend canjea la sesión** (la API key + la aserción de la persona; nunca desde el navegador):

   ```bash
   curl -s -X POST https://valida.cliente.co/api/v1/sesiones-usuario -H "X-Api-Key: $LLAVE_DEL_SISTEMA" -H "X-Usuario-Delegado: $ASERCION"
   # → { "token": "eyJ…", "expira": "2026-11-11T15:30:00Z" }
   ```

   ```csharp
   var sesion = await validasoft.ComoUsuario(laura).Sesiones.CrearAsync();      // { Token, Expira }
   ```

   ```ts
   const sesion = await validasoft.comoUsuario(laura).sesiones.crear();         // { token, expira }
   ```

3. **La página aloja el iframe y le pasa el token:**

   ```html
   <iframe id="validasoft" title="Validasoft" style="width:100%;height:80vh;border:0"
           src="https://valida.cliente.co/app/#/e/EMPRESA_ID/bandeja"></iframe>
   <script>
     const marco = document.getElementById('validasoft');
     window.addEventListener('message', async (evento) => {
       if (evento.origin !== 'https://valida.cliente.co') return;
       if (evento.data?.tipo === 'validasoft:sesion-requerida') {          // al abrir, y cuando la sesión está por vencer
         const { token } = await fetch('/mi-backend/validasoft/sesion', { method: 'POST' }).then((r) => r.json());   // tu backend canjea
         marco.contentWindow.postMessage({ tipo: 'validasoft:sesion', token, empresaId: 'EMPRESA_ID' }, 'https://valida.cliente.co');
       }
     });
   </script>
   ```

   | Validasoft → tu página | Contenido |
   |---|---|
   | `validasoft:ready` | `{ version: 1 }`: el iframe cargó |
   | `validasoft:sesion-requerida` | `{ motivo: 'inicial' \| 'por-vencer' \| 'vencida' \| 'rechazada' }`: manda un token nuevo |
   | `validasoft:resize` · `validasoft:visible` | `{ height }` · `{ top, height }`: para ajustar el alto del iframe |

   | Tu página → Validasoft | Contenido |
   |---|---|
   | `validasoft:sesion` | `{ token, empresaId? }` |
   | `validasoft:tema` | `{ mode: 'light' \| 'dark' }` |
   | `validasoft:cerrar-sesion` | — |

   La ruta del iframe (`#/e/{empresa}/{pantalla}`): `bandeja`, `certificados`, `vinculos`, `invitaciones`, `configuracion/…`. En el sandbox, la empresa es la copia de pruebas (§5) y el token se canjea con la llave de prueba.

## 9. Webhooks

Validasoft avisa a tu sistema de cada evento con un `POST` firmado a tu URL.

**Crear la suscripción** (Configuración › Webhooks › «Nuevo webhook», o por API con `webhooks:administrar`):

```bash
curl -s https://valida.cliente.co/api/v1/webhooks -H "X-Api-Key: $LLAVE_DEL_SISTEMA" -H "Content-Type: application/json" \
  -d '{ "url":"https://srm.cliente.co/api/integracion/validasoft/webhook", "tipos":["certificado.emitido","vinculo.bloqueado"], "empresas":["<empresaId>"], "descripcion":"SRM" }'
# → 201 { …, "secreto": "whsec_…" }   (el secreto se muestra UNA sola vez)
```

La URL debe ser `https` y pública (no se aceptan IP privadas, `localhost` ni redirecciones). `tipos: ["*"]` recibe todos; el catálogo está en `GET /api/v1/eventos/tipos`.

**El mensaje.** `POST` con `Content-Type: application/json`, el sobre como cuerpo y estas cabeceras:

| Cabecera | Valor |
|---|---|
| `Validasoft-Event-Id` | `id` del evento: **la clave para deduplicar** (un reintento repite el mismo). |
| `Validasoft-Event-Type` | El tipo (`certificado.emitido`). |
| `Validasoft-Delivery` | Id del **intento** (cambia en cada reintento). |
| `Validasoft-Timestamp` | Segundos Unix del envío. |
| `Validasoft-Signature` | `t={timestamp},v1={hex(HMAC-SHA256(secreto, "{timestamp}.{cuerpo}"))}`; durante una rotación trae un `v1=` por cada secreto vigente. |

```json
{ "id": "0196…", "tipo": "certificado.emitido", "version": 1, "ocurridoEn": "2026-11-11T14:03:11Z", "tenantId": "0196…", "empresaId": "0196…", "cursor": "4211",
  "datos": { "certificadoId": "0196…", "vinculoId": "0196…", "referencias": [ { "sistema": "SRM", "id": "ADP-00123" } ],
             "certificado": { "folio": "NORTE-C-2026-000001-1", "emision": 1, "estado": "Vigente", "hash": "…", "vigenteHasta": "2027-05-11", "urlVerificacion": "https://…/verificar/NORTE-C-2026-000001" } } }
```

**Verificar la firma — siempre, con el cuerpo CRUDO** (sin volver a serializarlo), en tiempo constante, rechazando timestamps de más de 5 minutos:

```csharp
// ASP.NET Core minimal API (net7.0+).
var verificador = new VerificadorDeWebhooks(new[] { secretoActual, secretoAnterior });   // el anterior solo mientras dura una rotación
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
// Express (Node 20+): express.raw para tener el cuerpo tal cual.
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

**Reglas del receptor:** responde `2xx` en **menos de 5 segundos** y procesa después; sin redirecciones; idempotente (deduplica por `Validasoft-Event-Id`, porque cada evento puede llegar más de una vez); no asumas orden entre agregados distintos (el `cursor` ordena dentro del grupo).

**Reintentos.** Si no responde `2xx` a tiempo, Validasoft reintenta con espera creciente: **1 min, 5 min, 30 min, 2 h y 6 h** (seis intentos). Luego la entrega queda `Fallida` y puedes **reenviarla a mano**
(`POST /api/v1/webhooks/{id}/entregas/{entregaId}/reenviar`; la lista en `GET …/entregas`). Tras **20 fallos seguidos** la suscripción se **deshabilita** y los administradores reciben un aviso; al
arreglar el receptor, actívala (`POST /api/v1/webhooks/{id}/activar`) y reenvía lo perdido (o léelo por pull, §10).

**Rotar el secreto** (`POST /api/v1/webhooks/{id}/rotar-secreto`): sale un secreto nuevo y el anterior sigue firmando 24 horas; mientras tanto el receptor acepta los dos (`new VerificadorDeWebhooks(nuevo, anterior)`).

## 10. Eventos por pull

Si tu red no acepta conexiones entrantes, lee los **mismos eventos** (el mismo sobre que el webhook) con `GET /api/v1/eventos` (`eventos:leer`). Guardas el `cursor` del último evento procesado y retomas desde ahí; el cursor solo crece y el modo pull **no se salta ninguno**.

```bash
curl -s "https://valida.cliente.co/api/v1/eventos?desde=4211&tipos=certificado.emitido,vinculo.bloqueado&limite=100" -H "X-Api-Key: $LLAVE" -H "X-Empresa: $EMPRESA"
# → { "datos": [ { …sobre… } ], "cursor": "4230", "hayMas": false }
```

```csharp
await foreach (var evento in validasoft.Eventos.RecorrerAsync(desde: cursorGuardado, tipos: ["certificado.emitido", "vinculo.bloqueado"]))
{
    Procesar(evento);               // el mismo mapa evento → acción que el webhook; deduplica por evento.Id
    cursorGuardado = evento.Cursor; // persístelo después de procesar
}
```

```ts
for await (const evento of validasoft.eventos.recorrer({ desde: cursorGuardado, tipos: ['certificado.emitido', 'vinculo.bloqueado'] })) {
  await procesar(evento);
  cursorGuardado = evento.cursor;
}
```

Sin `X-Empresa` recibes las empresas de tu llave; con él, solo esa. Cada minuto basta. Si reinicias el sandbox, tu cursor sigue valiendo (los nuevos tienen cursores mayores).

## 11. Consulta de certificación

Antes de **pagar o contratar**, pregunta si el tercero está certificado **en esa empresa** (`consultas:leer`, `X-Empresa`):

```bash
curl -si "https://valida.cliente.co/api/v1/consultas/certificacion?pais=CO&tipoIdentificacion=NIT&numero=900123456&tipoVinculo=PROVEEDOR" \
  -H "X-Api-Key: $LLAVE_DE_MARCO" -H "X-Empresa: $EMPRESA"
# 200 …  ETag: "c-9f2a…"   Cache-Control: private, max-age=300
```

El número se normaliza (puntos, guiones y dígito de verificación se aceptan). Sin `tipoVinculo` sirve si el tercero tiene un solo vínculo con la empresa (si tiene varios: `422 tipo-vinculo-requerido`); también se puede por `?vinculoId=`.

```json
{ "certificado": true, "folio": "NORTE-C-2026-000001-1", "estado": "Vigente", "estadoVinculo": "Activo", "vinculoId": "0196…", "terceroId": "0196…", "vigenteHasta": "2027-05-11",
  "revalidacionPendiente": [], "documentosVencidos": [], "bloqueado": false, "motivoBloqueo": null,
  "cuentaDesignada": { "numeroEnmascarado": "****6578", "banco": "Bancolombia" }, "urlVerificacion": "https://…", "legacy": null, "fuenteRespaldo": false }
```

| `bloqueado` y `motivoBloqueo` | Cuándo |
|---|---|
| `Bloqueo` | Vínculo bloqueado (lista restrictiva, fraude…) o certificado suspendido |
| `Revocado` | Certificado revocado, o vínculo anulado o desvinculado |
| `Vencido` | Vínculo o certificado vencido |
| `PlazoRevalidacion` | Certificado «con pendientes» cuyo plazo de revalidación venció |

**Regla de oro: paga solo si `certificado: true` y `bloqueado: false`.** Sin vínculo con la empresa (o tercero que no existe): `200 { "certificado": false, "estadoVinculo": "SinVinculo" }` — no revela si el tercero está en la plataforma.

**Caché con `ETag`.** Dentro de los 5 minutos (`max-age=300`) usa tu copia; después pregunta con `If-None-Match: "c-9f2a…"` y un **`304`** (sin cuerpo) confirma que sigue igual; si cambió, `200` con el `ETag` nuevo (que cambia **exactamente** cuando cambia la respuesta). La caché es **privada** (depende de tu credencial).

```csharp
var r = await validasoft.Consultas.CertificacionAsync(new ConsultaDeCertificacion("NIT", "900123456", "PROVEEDOR"), etag: copia?.ETag);
var datos = r.NoModificado ? copia!.Datos : r.Datos!;        // 304: sigue tu copia
if (datos.Bloqueado || !datos.Certificado) { /* no pagar */ }
```

```ts
const r = await validasoft.consultas.certificacion({ tipoIdentificacion: 'NIT', numero: '900123456', tipoVinculo: 'PROVEEDOR' }, copia?.etag);
const datos = r.noModificado ? copia!.datos : r.datos!;
```

**Si Validasoft no responde** (más de 2 s o `5xx`), tu sistema responde el mismo contrato desde su **copia local**, marcando `fuenteRespaldo: true` y `antiguedadSegundos`, y alimenta esa copia con los eventos (§9, §10). Nunca pagues con una copia vencida sin avisar de su antigüedad.

## 12. Errores

Toda respuesta de error es **RFC 9457** (`application/problem+json`):

```json
{ "type": "https://docs.validasoft.co/errores/vinculo-bloqueado", "title": "…", "status": 409, "detail": "El vínculo está bloqueado.",
  "instance": "/api/v1/vinculos/0196…/expedientes", "codigo": "vinculo-bloqueado", "traceId": "00-4bf9…-01" }
```

**Decide con `codigo` (estable), no con el texto.** El SDK lo expone (`ExcepcionValidasoft.Codigo`, `ErrorValidasoft.codigo`) junto con `Estado`, `Detalle` y `Correlacion`. Cita el `traceId` o tu `X-Correlation-Id` al pedir soporte.

| HTTP | Significa | Qué hacer | Códigos frecuentes |
|---|---|---|---|
| `400` | Petición mal formada | Corrige y reenvía | `validacion`, `clave-idempotencia-requerida`, `identificacion-invalida` |
| `401` | Credencial inválida, vencida, revocada o aserción inválida | Revisa la llave y la aserción (§7); no reintentes igual | `no-autenticado`, `asercion-*`, `llave-sin-sistema`, `sistema-inactivo` |
| `403` | La credencial no tiene ese permiso | Pide el scope (§3) | `sin-permiso`, `alcance-insuficiente` |
| `404` | No existe **o no es de tu alcance** (grupo o empresa) | Revisa el id y `X-Empresa` (en el sandbox, el de la copia) | `no-encontrado`, `empresa-no-encontrada`, `vinculo-no-encontrado`, `sandbox-no-existe` |
| `409` | Conflicto con el estado actual | Lee el estado y decide | `vinculo-duplicado`, `vinculo-bloqueado`, `invitacion-ya-aceptada`, `api-key-ya-rotada` |
| `410` | El enlace del tercero ya se usó o venció | Reenvía la invitación | `enlace-usado`, `enlace-vencido` |
| `412` | `If-Match` desactualizado | Relee y reintenta | `version-desactualizada` |
| `422` | Regla de negocio o dato inválido | Corrige el dato | `clave-idempotencia-reutilizada`, `tipo-vinculo-requerido`, `numero-identificacion-invalido`, `usuario-delegado-requerido`, `no-disponible-en-sandbox` |
| `429` | Demasiadas peticiones | Espera `Retry-After` | `demasiadas-peticiones`, `otp-bloqueado` |
| `5xx` | Falla de Validasoft | Reintenta con espera (el SDK lo hace con lo idempotente) | — |

El catálogo completo de códigos está en [05 §3.3](05-api-contratos-y-webhooks.md#33-errores-problemdetails).

## 13. Límites

| Qué | Límite |
|---|---|
| Idempotencia | `Idempotency-Key` de hasta 100 caracteres; se recuerda 24 horas; las respuestas con un secreto (webhook, llave rotada, alta de grupo) **no se guardan** |
| Aserción delegada | Vale 5 minutos y **una sola vez** (`jti`) |
| Sesión embebida | 30 minutos |
| Caché de verificación de la API key | Hasta 5 minutos: una llave revocada o rotada deja de valer de inmediato en el nodo que lo hizo y, como mucho, 5 minutos después en los demás |
| Rotación de una llave | La anterior puede seguir hasta 30 días (24 h por omisión) |
| Paginación | `limite` 1–200 (50 por omisión), cursor opaco |
| Webhook | Respuesta `2xx` en 5 s; 6 intentos (1 min a 6 h); deshabilitación tras 20 fallos seguidos; el secreto anterior firma 24 h tras rotar |
| Consulta de certificación | `max-age=300`; apunta a **p99 < 100 ms a 500 rps** con `ETag` ([medida](11-trazabilidad-por-hito.md), `tests/carga`) |
| Archivos del tercero | 20 MB y 50 páginas por documento; PDF, JPG, PNG o WebP; los binarios nunca pasan por la API (URL prefirmada de 5 a 15 min) |
| Peticiones por credencial | `/api/v1` **aún no** tiene un tope por credencial (llegará con `RateLimit-*` y `429`); sé amable: usa `ETag`, pull con espera y lotes de invitación. `/public/v1` (el tercero) limita por IP: `429 demasiadas-peticiones` con `Retry-After` |
| Sandbox | Un sandbox por grupo; el buzón retiene los correos hasta que lo vacíes o lo reinicies |

## 14. Versionado y deprecación

- **La API se versiona por URL** (`/api/v1`). Dentro de `v1` los cambios son **aditivos** (campos, endpoints, tipos de evento, valores nuevos): **tolera campos desconocidos y valores de enum desconocidos** (trátalos como «otro»). Un cambio incompatible llega como `v2` y **`v1` sigue 12 meses**.
- **Deprecación**: lo que se va a retirar se anuncia con **12 meses** de antelación en las notas de cambios y, en las respuestas del endpoint afectado, con las cabeceras `Deprecation` y `Sunset` (fecha de retiro). Un integrador no debería enterarse de un retiro por un error.
- **Eventos**: cada tipo lleva `version`; un cambio incompatible sube la versión y la suscripción elige cuál recibe, con 12 meses de convivencia.
- **SDK**: [versionado semántico](https://semver.org/lang/es/) alineado con la API (`1.x`): las menores y de parche no rompen nada; la mayor llega con una API nueva. Los tres paquetes (`Validasoft.Contratos`, `Validasoft.Sdk`, `@validasoft/sdk`) salen juntos con la misma versión, y su `CHANGELOG.md` dice qué cambió. Fija la versión menor en tu proyecto y actualiza con calma.
- **OpenAPI 3.1** en `{host}/openapi/v1.json`: la fuente para generar un cliente en otro lenguaje (los oficiales son .NET y TypeScript).

## 15. Lista de verificación para pasar a producción

- [ ] Flujo completo probado en el sandbox con tu llave de prueba: invitación → carga → aprobación → `certificado.emitido` → consulta → bloqueo → consulta.
- [ ] La llave de producción emitida **por sistema**, con los permisos mínimos y solo las empresas del sistema; guardada en el gestor de secretos.
- [ ] `X-Empresa` de producción en la configuración (no el de la copia del sandbox).
- [ ] Webhook de producción creado, con `https` válido; verificación de la firma sobre el cuerpo crudo, deduplicación por `Validasoft-Event-Id` y respuesta en < 5 s; secreto en el gestor de secretos.
- [ ] Cursor de pull (si lo usas) persistido **después** de procesar cada evento.
- [ ] Consulta de certificación con `ETag`, caché de 5 minutos y respaldo local (`fuenteRespaldo`).
- [ ] Manejo de `404`/`422`/`429`/`5xx` por `codigo`, con reintentos solo de lo idempotente y `Idempotency-Key` estable por operación.
- [ ] Procedimiento de **rotación** de la API key y del secreto del webhook ensayado.
- [ ] Orígenes del sistema registrados si embebes pantallas, y backend que canjea la sesión con la aserción de la persona.
- [ ] Alertas por webhook deshabilitado y por `401` (llave vencida o revocada).
- [ ] La llave de prueba no está en el código de producción.
