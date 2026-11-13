---
titulo: "Seguridad e identidad"
seccion: "Arquitectura"
orden: 14
resumen: "Cómo entra cada actor, qué credencial usa, qué permisos tiene, cómo se aíslan los datos y qué se cifra: código por correo, passkeys, SSO, API keys, identidad delegada y confirmación con passkey."
---

Validasoft no usa contraseñas. Cada actor entra por un camino distinto, con una credencial propia y permisos propios; todos terminan en el mismo contexto de petición: un **grupo**, una **empresa activa** y un conjunto de **permisos**.

## Cómo entra cada actor

| Actor | Camino | Credencial en cada petición |
|---|---|---|
| **Tercero** | Abre el enlace del correo (`/e/{token}`), de un solo uso. Si el tipo de vínculo lo exige, escribe un código de 6 dígitos que le llega al mismo correo. También puede pedir un enlace nuevo con su correo en `/entrar`. | Sesión del tercero: un token firmado de 30 minutos, con renovación por una sesión de refresco que rota en cada uso. Solo vale en `/public/v1`. |
| **Persona de la empresa, ingreso directo** | Correo, código de 6 dígitos y passkey (WebAuthn). | Token de acceso de 15 minutos ligado a una sesión de hasta 8 horas y cierre por inactividad de 30; se puede revocar en segundos. |
| **Persona de la empresa, SSO** | Proveedor de identidad del cliente: OIDC genérico o WorkOS, con dominios verificados. | La misma sesión directa, tras el retorno del proveedor. |
| **Persona de la empresa, dentro de un sistema integrado** | El sistema canjea desde su backend la identidad firmada de la persona. | Sesión delegada de 30 minutos, que se corta si el sistema se desactiva. |
| **Sistema integrador** | API key de su backend. | `X-Api-Key`, con permisos y empresas de alcance; más `X-Usuario-Delegado` si actúa como una persona. |
| **Operador de Plataforma** | Ingreso directo con passkey obligatoria, o sesión del backoffice. | Token de acceso como el de una persona sin grupo, con solo `plataforma:tenants`; o la clave de arranque. |
| **Público** | Ninguno. | Ninguna: `/public/v1/verificar/{folio}` con límite por IP. |

La política por omisión de toda la API exige una credencial; solo lo marcado como anónimo (salud, documentación, canje del enlace, autorregistro y verificación pública) no la pide. Los esquemas de autenticación se eligen por la ruta y la cabecera: `/public/v1` solo acepta la sesión del tercero; en el resto, una `X-Api-Key` o un `Authorization: Bearer`.

## Ingreso directo, passkeys y SSO

- **Código por correo:** vale 5 minutos y admite 5 intentos; pedir otro no reinicia los fallos. Un correo sin cuenta recibe un desafío señuelo, para no revelar quién existe.
- **Passkeys (WebAuthn, FIDO2):** se registran por persona (hasta 10), se pueden revocar (lo que cierra todas sus sesiones y le avisa por correo) y el ingreso en un dispositivo ya conocido las ofrece de forma condicional. El Administrador, el Oficial de cumplimiento, el Auditor y los operadores de Plataforma **deben** tener una; el grupo puede exigirla a todos. Sin passkey, el código del correo solo sirve para registrar una.
- **Códigos de recuperación:** diez, de un solo uso, para quien pierde sus dispositivos; generarlos pide confirmar con la passkey.
- **SSO:** un grupo configura un proveedor (OIDC con PKCE o WorkOS), verifica los dominios de correo de sus personas publicando un archivo `.well-known`, y decide si convive con el código (`Complementario`) o lo reemplaza (`Exclusivo`). Puede crear a las personas en su primer ingreso y asignarles roles según los grupos del proveedor. El cliente HTTP del SSO no entra a redes internas.
- **Modos de ingreso del grupo:** Delegado y/o Directo; la duración de la sesión (hasta 24 horas) y la inactividad (de 5 a 480 minutos) se fijan por grupo ([Configuración del grupo](guia:configuracion-del-grupo#ingreso)).
- **Límites:** el ingreso y los enlaces del tercero están limitados por IP (por omisión 30 peticiones por minuto) y `/public/v1` también (60 por minuto); la IP real solo se toma de `X-Forwarded-For` desde proxies confiables.

## Permisos y roles

Cada operación exige un **permiso** (*scope*) con nombre `recurso:acción`, por ejemplo `vinculos:bloquear`. Los **roles** son conjuntos de permisos (31 en total para el Administrador) y se asignan por persona, para todo el grupo o para una empresa; las **API keys** llevan los permisos que se marquen al crearlas. El permiso efectivo de una persona que llega por identidad delegada es la **intersección** entre lo que permite la llave del sistema y lo que permiten sus roles. Las tablas completas están en [Configuración del grupo](guia:configuracion-del-grupo#usuarios-y-roles).

- La **separación de funciones** es opcional por flujo: quien invitó al tercero o abrió el expediente no puede decidir sobre él. El **conflicto de intereses** (una persona de la empresa con la misma identificación que el tercero) siempre aplica.
- Un **recurso de otro grupo o de otra empresa responde 404**, nunca 403 ([Datos y multi-tenencia](guia:arquitectura-datos-y-tenencia)).
- Las decisiones de una empresa son privadas: bloquear o rechazar en una no se ve ni afecta a otra.

## API keys

`X-Api-Key: vsk_{entorno}_{grupo}_{64 caracteres hexadecimales}`, con `live` (producción), `dev` (desarrollo) o `test` (sandbox). El secreto son 32 bytes aleatorios; **solo se guarda su hash Argon2id** y un prefijo de 8 caracteres para buscarla. La clave completa se muestra una sola vez.

- Una llave puede ser de un **sistema integrado**; entonces fija sus referencias externas y valida sus aserciones, y se revoca si el sistema se desactiva.
- **Rotar** emite una llave nueva con los mismos permisos y deja la anterior valiendo un solapamiento (24 horas por omisión, hasta 30 días); **revocar** es inmediato en el nodo que lo hace y, como mucho, 5 minutos después en los demás (la caché de verificación).
- Crear, rotar y revocar piden confirmar con passkey; una credencial del sandbox no administra llaves.

## Identidad delegada

Para que un sistema actúe como una persona suya, cada petición lleva su API key y la cabecera `X-Usuario-Delegado` con una **aserción**: un JWT firmado (RS256 o ES256) con la clave **privada** del sistema, que Validasoft verifica con la **pública** registrada. Vale 5 minutos (`exp`) y **una sola vez** (`jti`); `iss` es el código del sistema y `aud` es `validasoft`. Rotar la clave del sistema invalida de inmediato las aserciones anteriores. La primera vez que llega una persona nueva, se crea o se enlaza con quien ya tiene ese correo. Los errores y el detalle están en la [guía de integración](guia:guia-de-integracion#identidad-delegada-y-pantallas-embebidas).

## Confirmar con passkey

Las acciones sensibles piden una confirmación con la passkey dentro de los últimos **5 minutos**: cambiar roles, desactivar a una persona, revocar una passkey, crear o rotar llaves, rotar la clave de un sistema, guardar el SSO y, en Plataforma, operadores, clave de arranque y listas. Solo aplica a quien entró de forma directa; las sesiones delegadas y las llaves no la piden. Sin confirmación reciente la API responde 403 `reautenticacion-requerida` y la web abre el diálogo y repite la petición.

## Embeber Validasoft en otro sistema

Un sistema integrado registra los **orígenes** desde los que puede mostrar Validasoft en un `<iframe>`: de ahí salen la cabecera `frame-ancestors` y la lista de orígenes permitidos para `postMessage`. La sesión de 30 minutos la canjea el backend del sistema; el navegador nunca ve la API key. Si el sistema se desactiva, sus sesiones dejan de valer en segundos y sus orígenes salen de la lista.

## Qué se cifra y cómo

| Qué | Cómo |
|---|---|
| Secretos en reposo (llaves de OpenAI, secretos de webhook, números de cuenta, credenciales de listas) | AES-GCM con la **clave maestra** (`Validasoft:Secretos:ClaveMaestra`); el texto cifrado lleva el identificador de la clave, así se rota con re-cifrado desde la consola. Sin clave maestra la API no arranca. |
| Credenciales de Stradata y Compliance, y campos privados de un grupo | AES-GCM con una clave derivada **por grupo** de la clave maestra. |
| Lo personal que viaja en eventos | Cifrado con una **llave de datos por tercero**: destruirla anula esos datos (*crypto-shredding*) sin reescribir el event store. |
| Sesiones y tokens | JWT firmados con `Validasoft:Sesiones:ClaveFirma` (al menos 32 bytes). |
| Certificados | Contenido canónico (RFC 8785) sellado con Ed25519; las claves públicas están en `/.well-known/validasoft-certificados.json` ([Documentos](guia:arquitectura-documentos)). |
| Webhooks | Cada entrega va firmada con HMAC-SHA256; el destino se resuelve y se rechazan las IP privadas, locales y reservadas (protección contra SSRF) al conectar. |
| Documentos | Nunca pasan por la API: URLs prefirmadas, claves por SHA-256 y descargas auditadas. |

## Habeas data y retención

Los titulares pueden pedir consulta, corrección y supresión (Ley 1581): las solicitudes tienen plazos en días hábiles con avisos, y la supresión **conserva lo que la ley exige** (por omisión 10 años, configurable) con su base legal, elimina lo demás y anonimiza. Cada descarga de un documento y cada exportación de la bóveda quedan registradas.
