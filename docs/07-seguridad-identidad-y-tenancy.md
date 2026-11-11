# 07 — Seguridad, identidad y tenancy

| Estado | Fecha | Responsable |
|---|---|---|
| Borrador (decisiones del registro propagadas) | 2026-10-28 | Arquitectura |

**Propósito.** Definir cómo se autentican y autorizan usuarios, terceros e integraciones; cómo se aísla cada tenant (grupo empresarial) **y cada empresa dentro del tenant** (ADR-0008) mientras se comparte la bóveda del tercero; cómo se protegen binarios, secretos y datos personales; qué se registra; y qué amenazas se mitigan. **Este documento es el dueño de los nombres de roles y permisos.**

**Relacionados.** [02-modelo-documental-compartido.md](02-modelo-documental-compartido.md) · [03-dominio-y-eventos.md](03-dominio-y-eventos.md) · [04-arquitectura-plataforma.md](04-arquitectura-plataforma.md) · [05-api-contratos-y-webhooks.md](05-api-contratos-y-webhooks.md) · [08-integracion-y-migracion.md](08-integracion-y-migracion.md) · [decisiones/registro.md](decisiones/registro.md) · ADR-0003, ADR-0004, ADR-0008

---

## 1. Principios

1. **Todo endpoint exige autenticación y autorización por defecto.** Excepciones explícitas: `/health/live|ready`, `/public/v1/verificar/{folio}`, canje de enlaces/OTP, autorregistro (con rate limit). No existe "modo desarrollo" que fije un usuario.
2. **Tenant y empresa salen de la credencial** (y del header `X-Empresa` validado contra ella). Recurso de otro tenant o empresa ⇒ `404`.
3. **El tercero no tiene contraseña.** Enlace de un solo uso, OTP opcional, sesión corta.
4. **Secretos fuera del repositorio y de tablas en claro.**
5. **Los binarios nunca pasan por la API.** URLs prefirmadas auditadas.
6. **Lo que una empresa decide es privado**, incluso frente a otras empresas del mismo tenant.
7. **Fail-closed en seguridad, fail-soft en conveniencia.** Sin IA, la carga del tercero nunca se bloquea (P-6-4); sin antivirus en prod, el documento espera (T-11).
8. **Nada se comparte con una empresa antes de que el tercero la acepte.** Crear un vínculo o expediente no da acceso a la bóveda: la compartición nace al aceptar la invitación y registrar los consentimientos de esa empresa (B-1).
9. **El soporte de la plataforma no ve datos de los tenants** salvo acceso temporal concedido por un Administrador del tenant, con alcance, duración y auditoría (B-6).

---

## 2. Identidad: OpenIddict + ASP.NET Identity

Validasoft es su propio proveedor OIDC (módulo `Identidad`, esquema `identidad`). Keycloak descartado en el MVP (ADR-0003); federable después (§2.5). Las personas que ya iniciaron sesión en un **sistema integrado** (SRM, Marco, Portal, sistemas del cliente) no tienen cuenta propia: actúan por **identidad delegada** (§2.6, ADR-0011, que precisa ADR-0003).

### 2.1 Flujos

| Cliente | Flujo | Tokens |
|---|---|---|
| `apps/web` (revisor/admin/plataforma) | Authorization Code + PKCE | Access JWT 15 min; refresh rotativo 8 h deslizante con detección de reuso; solo en memoria |
| `apps/public` (tercero) | Grant `urn:validasoft:magic-link` | Access JWT 30 min ligado a `terceroId` y vínculos; refresh deslizante máx. 8 h; cookie `HttpOnly; Secure; SameSite=Lax` (**implementado**: §2.2 «Reingreso», RE-1…RE-7) |
| SRM/Portal dentro del marco ERP | Grant `urn:validasoft:erp-token` (token exchange) | Access JWT 15 min con `tenant`, `empresas[]`, `sub`, roles |
| Integraciones servidor-a-servidor | `X-Api-Key` | §4 |
| Persona de un sistema integrado (SRM, Marco, Portal…) | `X-Api-Key` del sistema + `X-Usuario-Delegado` (JWT ≤ 5 min firmado por el sistema) | §2.6; pantallas embebidas: sesión de Validasoft HS256 de 30 min (`POST /sesiones-usuario`) |
| Operador de Plataforma (consola `/plataforma`) | Credencial de Plataforma (`X-Api-Key`, clave de arranque) + `X-Usuario-Delegado` firmado por un **sistema de Plataforma** | **Implementado (PT-4):** `POST /plataforma/sesiones` ⇒ sesión HS256 de 30 min sin tenant ni empresas, actor `Plataforma`, solo `plataforma:tenants`; el operador queda registrado para la auditoría. Mientras llega el login OIDC con 2FA (§2.4) |
| CLI de importación | Client Credentials con `importacion:escribir` | 15 min |

### 2.2 Grant `urn:validasoft:magic-link`
1. Token opaco de 256 bits; se guarda solo su hash SHA-256 con `terceroId`, `vinculoId?`, `expedienteId?`, `correoDestino`, `expira` (72 h), `requiereOtp`, `usado=false`.
2. Correo `https://{public}/e/{token}`; el token nunca va a logs ni analytics.
3. `POST /public/v1/enlaces/{token}/canjear`; con OTP: código de 6 dígitos (10 min, 5 intentos) al mismo correo, `POST …/otp`.
4. `usado=true` al canjear; segundo canje ⇒ `410 enlace-usado` y ofrece enlace nuevo.
5. Claims: `tipo=tercero`, `terceroId`, `vinculos[] { vinculoId, tenantId, empresaId, invitacionAceptada }` (todos los vínculos cuyo contacto coincide con el correo), `expedienteOrigen`. Canjear el enlace **solo abre sesión**; no comparte nada.
6. **Aceptar la invitación** (`POST /public/v1/invitaciones/{expedienteId}/aceptar`) registra `ConsentimientosRegistrados` —aviso de la plataforma la primera vez (incluye expresamente el procesamiento por IA externa y la transferencia internacional, P-6-5), autorización de datos y T&C de **esa** empresa— con versión del texto, fecha, IP, user-agent y OTP como evidencia (`AceptacionDto`; P-3-5, P-16-4). Solo entonces se comparte con esa empresa la versión actual de cada tipo global que exige (B-1). Hasta que el tercero no haya aceptado el aviso de plataforma, sus versiones se procesan **sin IA** (B-2/B-8).

**Implementado (2026-11-06, fase «Validación por el interesado»; IN-1…IN-12 del [registro](decisiones/registro.md)).**
- `EnlaceDeAcceso` global (esquema `identidad`): `Id` = SHA-256 hex del token de 256 bits (base64url), `tenantId?`, `vinculoId?`, `terceroId`, `correoDestino`, `expira`, `requiereOtp`, `usado`, hash del OTP, intentos; concurrencia optimista. Cada `InvitacionEnviada` crea uno con la vigencia de la invitación y **borra** los no usados del vínculo (el anterior responde `404 enlace-no-encontrado`). `requiereOtp` sale de `TipoVinculo.exigeOtpEnEnlace` (semilla: solo `PROVEEDOR`).
- Canje: usado ⇒ `410 enlace-usado`; vencido ⇒ `410 enlace-vencido`; con OTP ⇒ `202 { requiereOtp, expira }` y código de 6 dígitos (hash ligado al enlace, 10 min) al mismo correo; `…/otp`: malo ⇒ `422 otp-invalido` con `intentosRestantes`, agotado ⇒ `429 otp-bloqueado` (5 fallos **por enlace**; volver a canjear da otro código pero no reinicia la cuenta), vencido ⇒ `410 otp-vencido`.
- Sesión del tercero: JWT HS256 de 30 min con `Validasoft:Sesiones:ClaveFirma` y audiencia `validasoft:tercero`; reclamos `tipo=tercero`, `terceroId`, `correo`, `otpVerificado` y `vinculos` (`vinculoId|tenantId|empresaId`: los de ese tercero cuyo correo de invitación coincide con el del enlace, en todos los tenants, fijados al emitir). Esquema `Tercero` solo en `/public/v1/*` (el selector de esquema es por ruta): ahí no valen API keys ni sesiones de usuario, y `/api/v1` no acepta el token del tercero. El access token viaja en el cuerpo y se usa como `Authorization: Bearer` (en memoria del navegador); lo que sobrevive a recargar es el **token de refresco** en la cookie `HttpOnly` `vs_tercero` (ver «Reingreso» abajo). Los vínculos ya no quedan fijados por toda la sesión: se recalculan en cada renovación.
- Correo: `IEnviadorDeCorreo` (MailKit/SMTP, `Validasoft:Correo:{Host, Puerto, Seguridad, Remitente, UrlPublica}`); en desarrollo **Mailpit** (`infra/compose`: SMTP 1025, bandeja en http://localhost:8025). Los correos salen por el **outbox de Wolverine** (cola local durable `correo`) después del commit; el token y el OTP viajan **cifrados** (AES-256-GCM; desde PT-1 con la clave maestra de §6.3, antes con una clave HKDF de `ClaveFirma`) en el sobre, y el segmento del token se enmascara en logs y en `instance` de los ProblemDetails.
- Aceptación por `vinculoId` (no hay expedientes aún): exige autorización y T&C **vigentes** de la empresa (si no están publicados ⇒ `409 textos-legales-faltantes`) y el aviso de la plataforma si el tercero no tiene su versión vigente; versión distinta ⇒ `409 texto-desactualizado`; no aceptado ⇒ `422`. Si el tipo de vínculo exige OTP y la sesión no lo verificó ⇒ `403 otp-requerido`. Eventos `ConsentimientosRegistrados` + `InvitacionAceptada` con actor `Tercero`; el aviso aceptado queda también en el `Tercero`. Aún no se comparte nada porque no hay documentos.
- Rate limit por IP en `/public/v1/*` (ventana fija, 60/min por defecto); detrás de un proxy cuenta la IP real de `X-Forwarded-For` solo si la petición viene de un proxy confiable (§2.6 punto 8, SE-6). Pendiente: constancia por correo de la aceptación (`consentimiento-empresa`).

**Reingreso (2026-11-06, H2-reingreso; RE-1…RE-7 del [registro](decisiones/registro.md)).** En una demo el proveedor observado no podía volver a entrar: el enlace era de un solo uso, la sesión vivía en memoria del navegador y el correo de observación no traía enlace.
- **Sesión de refresco** (`SesionDeRefresco`, global, esquema `identidad`, concurrencia optimista): familia (`Id`), hash SHA-256 del token **vigente**, `terceroId`, `correo`, `otpVerificado`, `creadaEn`, `expiraEn` (= canje + 8 h, **absoluto**), `rotadaEn`, `rotaciones`, `revocadaEn` y `motivoRevocacion` (`reuso` \| `cierre`). El token es opaco: `{familia}.{secreto de 256 bits en base64url}`; en la BD solo queda su hash.
- **Cookie `vs_tercero`**: `HttpOnly; SameSite=Lax; Path=/public/v1/sesion; Secure` (configurable con `Validasoft:SesionTercero:CookieSegura`, `false` solo en `appsettings.Development.json` porque en local es http), `Expires` = vencimiento absoluto. La ponen el canje sin OTP y la verificación del OTP (misma transacción que marca el enlace usado). Por su `Path` el navegador solo la envía a renovar y cerrar.
- **Renovar** (`POST /public/v1/sesion/renovar`): exige `X-Validasoft-Cliente: public` (defensa CSRF adicional a `SameSite`; sin ella `403 cliente-invalido`). Con el token vigente rota la cookie y devuelve un `SesionTerceroDto` nuevo con los vínculos **recalculados** y `otpVerificado` conservado. Sin cookie, mal formada, desconocida o vencida ⇒ `401 sesion-expirada`. Un token de la familia que no es el vigente (ya rotado) ⇒ **reuso**: se revoca la familia completa y `401 sesion-revocada` (también para el token vigente, que quizá tiene quien la robó). Dos renovaciones simultáneas con el mismo token: la concurrencia optimista deja ganar a una; la otra recibe `409 renovacion-concurrente` sin revocar. No hay ventana de inactividad propia: la sesión dura lo que dure el navegador renovando, hasta 8 h desde el canje.
- **Cerrar** (`POST /public/v1/sesion/cerrar`, misma cabecera): revoca la familia y borra la cookie; `204` siempre (idempotente).
- **Acceso con el correo** (`POST /public/v1/acceso { correo }`): siempre `202` con el mismo cuerpo, exista o no. Si hay vínculos **no terminales** (de cualquier tenant) con ese `correoTercero`, envía **un** enlace por tercero (sin vínculo, 1 h, motivo `AccesoPorCorreo`, `requiereOtp` si alguno de esos tipos lo exige) con la plantilla «Tu enlace para entrar a Validasoft». Límite: 3 envíos por hora por correo (`LimiteDeAccesoPorCorreo`, id = SHA-256 del correo), además del límite por IP; superado, responde igual y no envía. Correo mal formado ⇒ `422 correo-invalido` (no depende de que exista).
- **Correo de observación** con enlace: en la misma transacción del veredicto se crea un `EnlaceDeAcceso` de un solo uso (motivo `Observacion`, del vínculo, 72 h, OTP según el tipo) y su token viaja **cifrado** en `CorreoDeObservacionPendiente` (IN-3). Ningún correo vuelve a pedir «ingresa con el enlace de tu invitación».
- Los mensajes de `410 enlace-usado`/`enlace-vencido` y `429 otp-bloqueado` ahora remiten a pedir un enlace con el correo.

### 2.3 Grant `urn:validasoft:erp-token`
Validación del token del marco contra el endpoint de introspección del ERP del tenant (cache 5 min) o firma compartida rotable; mapeo a usuario Validasoft del tenant con roles por empresa según mapeo configurado; nunca se guarda el token del ERP. **Precisado por ADR-0011:** Marco es un sistema integrado más y usa la identidad delegada de §2.6; los roles no vienen del ERP.

### 2.4 Usuarios internos
- Contraseñas Argon2id; mínimo 12 caracteres; bloqueo progresivo.
- **2FA TOTP obligatorio** para `Administrador`, `Auditor`, `Plataforma` y revisores del área **Cumplimiento**; opcional (recomendado) para el resto; el tenant puede exigirlo a todos (P-14-4). Códigos de recuperación; passkeys en H8 (decisión abierta).
- Recuperación por enlace de un solo uso (30 min). Sesiones listables y revocables.
- **Delegación** temporal por usuario (fechas, área, empresa) auditada (P-4-6).

### 2.5 Federación futura
Proveedor externo OIDC/SAML por tenant; mapeo de grupos a roles por empresa; cuentas locales desactivables.

### 2.6 Identidad delegada desde sistemas integrados (ADR-0011)
1. **Sistemas integrados.** El administrador registra cada sistema (`POST /sistemas`: código en mayúsculas único en el tenant, nombre y **clave pública** PEM RSA ≥ 2048 o EC P-256; se rechazan claves privadas). Las API keys del sistema llevan su `sistemaId`. Validasoft no guarda nada que permita suplantar al sistema; rotar la clave (`PUT /sistemas/{id}/clave-publica`) invalida al instante las aserciones con la anterior y desactivar el sistema deja sin efecto sus llaves (como mucho 5 min por la caché de llaves).
2. **Aserción.** Con su API key, el sistema envía `X-Usuario-Delegado`: JWT RS256/ES256 firmado con su clave privada, `iss` = código del sistema, `aud` = `validasoft`, `exp` no vencido y a lo sumo 5 min en el futuro (tolerancia 30 s), `sub`, `name`, `email` y `jti`. Firma, emisor, audiencia o vigencia inválidos, llave sin sistema, sistema inactivo o usuario desactivado ⇒ `401` ProblemDetails (`asercion-*`, `llave-sin-sistema`, `sistema-inactivo`, `usuario-inactivo`).
   - **Un solo uso (SE-4).** Sin `jti` ⇒ `401 asercion-sin-jti`. Tras verificar la firma y la vigencia (para que nadie llene el registro con aserciones falsas) y antes de tocar al usuario, Validasoft inserta `AsercionUsada` (global, esquema `identidad`, `Id` = `{sistemaId}:{jti}`, `ExpiraEn` = `exp` + 30 s, índice por `ExpiraEn`): la llave primaria hace que un segundo uso —aunque llegue en paralelo— responda `401 asercion-reutilizada`. Un servicio en segundo plano borra las vencidas cada `Validasoft:AsercionesUsadas:MinutosLimpieza` (10) minutos. Una aserción robada (logs, proxy) ya no sirve para repetir la petición dentro de sus 5 minutos.
3. **Usuario.** Se busca por la identidad externa `(sistema, sub)` (única por tenant). Si no existe se **enlaza** al usuario con el mismo correo (alta anticipada por el administrador u otro sistema) o se **crea sin roles** (JIT, ID-2). El sistema es la fuente de nombre y correo; `ultimoAcceso` se actualiza como mucho una vez por minuto. Altas y enlaces quedan en la auditoría de configuración con la API key del sistema como actor.
4. **Permiso efectivo = intersección.** Scopes = scopes de la llave ∩ scopes de los roles del usuario (§3.1); empresas = empresas de la llave ∩ empresas donde el usuario tiene rol (`*` si tiene un rol de todo el tenant). Los scopes de un rol por empresa solo valen cuando `X-Empresa` es esa empresa; la administración del tenant (`usuarios`, `sistemas`, `api-keys`, `empresas`) solo se obtiene con un rol de todo el tenant. Sin roles, el usuario no ve ninguna empresa (`404`).
5. **Actor.** La petición queda con actor `Usuario` (`actorId` = id del usuario) y el sistema de origen; los eventos guardan `actorTipo`, `actorId` y `sistema` en sus metadatos, y la auditoría de configuración registra `sistema`.
6. **Sesiones embebidas (ID-3).** El sistema canjea, servidor a servidor, la aserción por una sesión de Validasoft (`POST /sesiones-usuario` ⇒ `{ token, expira }`): JWT HS256 de **30 min** firmado con `Validasoft:Sesiones:ClaveFirma` (≥ 32 bytes, secreto de configuración) que copia tenant, usuario, sistema, scopes y empresas efectivos. El iframe o Web Component lo envía como `Authorization: Bearer`; `X-Empresa`, idempotencia y autorización funcionan igual que con la API key. Un cambio de roles o la desactivación del usuario se aplican a la siguiente sesión (la vigente expira sola).
7. **Referencias externas.** El sistema de la referencia de un vínculo lo fija la credencial, no el cuerpo; un vínculo guarda una referencia por sistema y todos comparten la misma validación (I-0, [05 §2.3](05-api-contratos-y-webhooks.md)).
8. **Embebido desde otro dominio (2026-11-08, SE-1…SE-6; contrato en [05 §2.9.4](05-api-contratos-y-webhooks.md)).**
   - **Orígenes del sistema (SE-1).** `SistemaIntegrado.origenesPermitidos[]` (`PUT /sistemas/{id}/origenes`, auditado): orígenes `https://host[:puerto]` normalizados como los reporta el navegador; `http` solo hacia `localhost` en desarrollo (`Validasoft:Embebido:PermitirHttpLocal`). Un sistema desactivado deja de contar.
   - **`frame-ancestors` (SE-1).** Las páginas de `apps/web` que sirve la API llevan `Content-Security-Policy: frame-ancestors 'self' {unión de los orígenes de los sistemas activos}` (caché de 60 s por nodo, invalidada en ese nodo al crear, editar o desactivar un sistema; los demás nodos la ven al vencer). Es el navegador quien impide que un sitio no registrado enmarque Validasoft (clickjacking). `apps/public` solo se deja enmarcar por Validasoft (`'self'`).
   - **Orígenes en tiempo de ejecución (SE-2).** `GET /public/v1/embed/origenes-permitidos` (sin credencial) devuelve **la misma unión** que ya publica `frame-ancestors`, sin tenant, sistema ni nombre: no revela nada que la cabecera no revele. La app embebida solo acepta `postMessage` (y solo responde) a un origen de esa lista; `VITE_ORIGENES_CONTENEDOR` queda como respaldo. Los orígenes no son secretos (cualquier página que embebe expone el suyo); lo que no se publica es a quién pertenecen. Con muchos tenants la cabecera crece: si llega a ser un problema, la alternativa es servir la app por tenant (`/app/t/{slug}/`) con la lista de ese tenant.
   - **Un solo origen recomendado; CORS opcional (SE-5).** La API puede servir las apps (SE-3) y así no hace falta CORS. Para dominios separados, `Validasoft:Cors:OrigenesPermitidos[]` con orígenes exactos (sin comodines; con `PermitirCredenciales` nunca se combina `*`) y sin admitir `X-Api-Key` ni `X-Usuario-Delegado` en el preflight: las credenciales de sistema no pueden viajar desde un navegador.
   - **IP real detrás de un proxy (SE-6).** `X-Forwarded-For` / `X-Forwarded-Proto` se leen solo si la conexión llega desde `Validasoft:Proxies:Confiables[]` (IP o CIDR; se quitan los valores por defecto de loopback), tomando como máximo `Validasoft:Proxies:Saltos` (1) entradas desde la derecha: lo que el cliente anteponga en la cabecera no cuenta. Sin proxies configurados el middleware ni se registra.

---

## 3. Autorización: roles, alcance y permisos

### 3.1 Roles y alcance

| Rol | Alcance posible | Descripción |
|---|---|---|
| `Plataforma` | Global (`tenant_id = NULL`) | Crea tenants y empresas, administra el catálogo canónico y las propuestas, ve uso y costos agregados; **nunca accede a documentos ni decisiones por defecto**. Para un incidente, un `Administrador` del tenant le concede un **acceso temporal de soporte** (`AccesoSoporteDto`: alcance empresa/expediente/tercero, 1–72 h, justificación) que se registra con actor `Plataforma` en auditoría de configuración y en `DocumentoAccedido`; expira solo y es revocable (B-6). Sección `/plataforma` (P-15-2). **Implementado (PT-4…PT-7):** consola con grupos y empresas, alta de un grupo con su empresa, administrador y sistema integrado, plan de IA de cada empresa con su llave de OpenAI de solo escritura y secretos en reposo. |
| `Administrador` | Tenant o empresa | Configuración, usuarios, API keys, webhooks, override con justificación, bloqueos, reaperturas |
| `Revisor` | Empresa + área(s) | Veredictos en sus áreas; ve expedientes cuyos flujos incluyen un área suya |
| `Auditor` | Tenant o empresa | Solo lectura total, auditoría de accesos, exportaciones |
| `Cumplimiento` | Empresa | `Revisor` del área Cumplimiento + verificaciones externas + resolución de coincidencias; 2FA obligatorio |
| `Integracion` | API key con `empresas[]` | Scopes |
| `Tercero` | Sesión | Lo propio |

Un rol se asigna **por empresa** (`RolAsignado { rol, empresaId, areas[] }`) o **para todo el tenant** (`empresaId = *`). Un usuario puede pertenecer a varios tenants (claim `tenants[]`).

Mapeo rol → scopes vigente (H1, `Permisos.DeRol`): `Administrador` = todos los del tenant; `Revisor` = `configuracion:leer`, `terceros:leer`, `vinculos:leer` + `vinculos:escribir`; `OficialCumplimiento` (el `Cumplimiento` de esta tabla) = `Revisor` + `vinculos:bloquear`; `Auditor` = lecturas + `auditoria:leer`. En peticiones delegadas se intersecan con la API key (§2.6).

### 3.2 Permisos (claims finos; la UI solo oculta botones)

| Permiso | Plataforma | Administrador | Revisor (área X) | Cumplimiento | Auditor | Integración | Tercero |
|---|---|---|---|---|---|---|---|
| `plataforma:tenants` · `plataforma:catalogo` · `plataforma:uso` | ✓ | — | — | — | — | — | — |
| `empresas:administrar` | — | ✓ (tenant) | — | — | — | — | — |
| `configuracion:leer` / `escribir` | — | ✓ / ✓ | ✓ / — | ✓ / — | ✓ / — | scope | — |
| `usuarios:administrar` · `api-keys:administrar` · `webhooks:administrar` | — | ✓ | — | — | — | scope (webhooks) | — |
| `terceros:leer` / `escribir` (campos privados) | — | ✓ / ✓ | ✓ / — | ✓ / — | ✓ / — | scope | propio (maestros) |
| `vinculos:leer` / `escribir` | — | ✓ / ✓ | ✓ / — | ✓ / — | ✓ / — | scope | propios |
| `vinculos:bloquear` | — | ✓ | — | ✓ | — | — | — |
| `expedientes:leer` / `escribir` (crear, invitar, reabrir, anular) | — | ✓ / ✓ | ✓ / — | ✓ / — | ✓ / — | scope | propios / — |
| `veredictos:emitir:area:{X}` | — | ✓ (todas, con justificación como Administrador) | ✓ (X) | ✓ (Cumplimiento) | — | — | — |
| `requisitos:eximir` | — | ✓ | si `PuedeEximir` | si `PuedeEximir` | — | — | — |
| `expedientes:asignar` | — | ✓ | — | — | — | — | — |
| `cuentas:aprobar` | — | ✓ | ✓ (área Financiera/Tesorería) | — | — | — | — |
| `documentos:leer` (descargar) | — | ✓ | ✓ (en alcance) | ✓ | ✓ | scope | propios |
| `documentos:escribir` (cargar) | — | — | — | — | — | scope (en nombre del tercero, auditado) | ✓ |
| `documentos:corregir-datos` | — | ✓ | ✓ | ✓ | — | — | — |
| `verificaciones:escribir` | — | ✓ | — | ✓ | — | scope | — |
| `certificados:leer` / `revocar` | — | ✓ / ✓ | ✓ / — | ✓ / — | ✓ / — | scope / — | propios |
| `consultas:leer` · `eventos:leer` · `sesiones-tercero:crear` | — | ✓ | ✓ | ✓ | ✓ | scope | — |

> `sesiones-tercero:crear` (IO-6) solo sirve a la API key de un **sistema integrador**: abre la sesión de un tercero cuyo vínculo lleva la referencia de ese sistema, para un origen de los que el sistema registró, con un token de ≤ 15 minutos ligado a ese vínculo, origen y componente. Las llaves ya emitidas no la tienen: se crea o rota una con ella.
| `vencimientos:leer` / `escribir` | — | ✓ / ✓ | ✓ / ✓ | ✓ / ✓ | ✓ / — | scope | — |
| `auditoria:leer` (dominio, configuración, identidad y accesos; alcance por empresa o tenant) | — | ✓ | — | ✓ | ✓ | scope (solo referencias) | — |
| `auditoria:exportar` (Excel con los filtros de la pantalla, IF-4; ZIP firmado pendiente) | — | ✓ | — | — | ✓ | — | — |
| `expedientes:exportar` (expediente completo en ZIP con sus documentos; pide además `documentos:descargar`, IF-5) | — | ✓ | — | ✓ | ✓ | scope | — |
| `supresiones:resolver` (habeas data lado empresa: retenciones dudosas que la saga `SupresionDatos` escala) | — | ✓ | — | ✓ | — | — | — |
| `plataforma:operacion` (dead letters, rebuild de proyecciones) | ✓ | — | — | — | — | — | — |
| `plataforma:soporte` (solo dentro de un acceso temporal vigente; lectura de documentos/decisiones del alcance concedido; nunca escritura) | ✓ (con concesión) | concede/revoca (`empresas:administrar`) | — | — | ve las concesiones | — | — |
| `relaciones:escribir` (proponer personas relacionadas del tercero, B-3) | — | ✓ | ✓ (en alcance) | ✓ | — | scope | propias (declara/confirma) |
| `operacion:leer` | — | ✓ | — | — | — | — | — |
| `importacion:escribir` | — | — | — | — | — | scope dedicado | — |

Reglas: la **separación de funciones** (quien invita no aprueba) es configurable por tenant, apagada por defecto (P-4-8); el **conflicto de intereses** bloquea asignar o emitir veredicto a un usuario que es contacto o representante del tercero (cruce por identificación, adición 7); `AprobadoresRequeridos = 2` exige dos usuarios distintos.

---

## 4. API keys

| Aspecto | Regla |
|---|---|
| Formato | `vsk_{entorno}_{tenantSlug}_{32 bytes base62}`; prefijo visible; valor completo solo al crear/rotar |
| Almacenamiento | Hash Argon2id + prefijo en claro |
| Scopes | Lista cerrada de [05 §2](05-api-contratos-y-webhooks.md); `importacion:escribir` no se combina |
| **Sistema integrado** | `sistemaId` opcional (ADR-0011): fija el sistema de las referencias externas y permite `X-Usuario-Delegado`; la llave del alta del tenant no tiene sistema |
| **Alcance por empresa** | `empresas[]` o `*`; una llave con `[A1]` no ve nada de `A2` aunque sea del mismo tenant: la API responde **`404 no-encontrado`** (T-4), nunca un 403 que revele existencia |
| **Precedencia con webhooks** | El alcance efectivo de una suscripción es `apiKey.empresas[] ∩ webhook.empresas[]`; intersección vacía ⇒ `422 empresas-fuera-de-alcance` al crear/editar; si una rotación o edición posterior de la llave la deja vacía, la suscripción se deshabilita (`webhook.deshabilitado`, causa `empresas-fuera-de-alcance`) y el administrador recibe correo |
| Rotación / revocación | `POST /api-keys/{id}/rotar`: la anterior vale 24 h por defecto (de 0 a 30 días, CN-3); revocación inmediata; la caché de verificaciones (5 min) vence con el reloj de Validasoft y se olvida en el nodo al rotar o revocar (otros nodos, ≤ 5 min) |
| Registro | `ultimoUsoEn`, `ultimaIp`, contador; alerta por uso de llave inactiva 90 días |
| Restricción | IPs/CIDR, `expiraEn`; **sandbox** por llave de prueba (`vsk_test_…`) en H9 (P-11-4) |
| Prohibido | API keys en navegadores; el embed usa `POST /sesiones-usuario` servidor a servidor. El CORS opcional (SE-5) no admite `X-Api-Key` ni `X-Usuario-Delegado` |

---

## 5. Tenancy: tres niveles y la bóveda global

### 5.1 Particiones

| Partición | Esquema | Aislamiento | Contenido |
|---|---|---|---|
| Plataforma | `plataforma` | Global | `tenant`, `empresa`, `uso_tenant`, `plan`, `tipo_documento_canonico`, `esquema_formulario`, `catalogo_tipo_identificacion`, `acceso_soporte` (T-1; la lista exacta es la de [03 §2](03-dominio-y-eventos.md)) |
| Bóveda del tercero | `boveda` | Global por `tercero_id`; acceso por compartición | `tercero`, `relacion_entre_terceros` (B-3), `cuenta_bancaria`, `documento`, `version_documento`, `comparticion_version(tenant_id, empresa_id, vinculo_id)`, `verificacion_version`, `acceso_documento`, streams `Documento-*` |
| Validación | `validacion` | **Marten conjoined por `tenant_id`** + filtro obligatorio por **`empresa_id`** | `vinculo`, `expediente`, `version_aceptada`, `decision_version`, `certificado`, `cuenta_designada`, `bloqueo`, `tipo_documento_tenant` (defaults) y `tipo_documento_empresa` (override, B-4), configuración del tenant (sin `empresa_id`), streams `Vinculo-*`, `Expediente-*`, `Certificado-*` |
| Identidad | `identidad` | `tenant_id`; plataforma con `NULL` | Identity, OpenIddict, roles por empresa |

Reglas técnicas: un solo `DocumentStore` de Marten con eventos *conjoined* (los streams de la bóveda usan el tenant técnico `boveda` y los de plataforma `plataforma`, T-2); `IDocumentSession` de `validacion` se abre siempre con el tenant de la credencial (middleware Wolverine); todo documento de empresa implementa `IDeEmpresa { EmpresaId }` y una sesión envuelta `ISesionDeEmpresa` añade el predicado `EmpresaId ∈ empresasPermitidas` de la credencial (Marten no tiene filtros globales, T-3); un test arquitectónico (NetArchTest) falla si una entidad de empresa no implementa `IDeEmpresa`, si un handler usa `IDocumentStore`/`IQuerySession` crudos fuera de infraestructura o si consulta `validacion` sin tenant; `IAuthorizationHandler` por recurso verifica `tenant_id + empresa_id`.

### 5.2 Acceso cruzado a la bóveda

```
Puede(empresa E del tenant T, leer versión V del documento D del tercero X) ⇔
    ∃ comparticion_version (V, T, E) con compartida_en ≠ null
    ∧ ∃ vínculo (T, E, X) con invitacion_aceptada_en ≠ null y estado ∉ {Anulado}
    ∧ (D.ambito = Global ∨ D.vinculo_id ∈ vínculos de (T, E))
```

- La compartición se escribe por **vínculo** (y por tanto por empresa) en dos momentos: (1) cuando el tercero **acepta la invitación** de esa empresa, para la versión actual de cada tipo global que exige (B-1); (2) al promover una versión nueva, para todos los vínculos **ya aceptados** que exigen el tipo. Crear el vínculo o el expediente no comparte nada: la empresa solo ve `requisitosCubribles`.
- El tercero no puede excluir empresas (A-8).
- **Congelar ≠ revocar.** Desvincular o bloquear deja de compartir versiones **futuras** (`comparticion_congelada_desde` en el vínculo); las filas de `comparticion_version` existentes **no** se tocan, así que la empresa conserva **lectura** de lo que ya le fue compartido y aceptó durante su retención legal (obs. 5). Desbloquear reanuda la compartición de versiones futuras y comparte la actual si cambió entretanto (B-5).
- Una empresa nueva recibe la versión actual, nunca versiones superadas.
- El tercero ve todo lo suyo; nunca `decision_version` ni el detalle de `version_aceptada`, solo el estado agregado por empresa (`FrescuraDocumentoTercero`, `BovedaTercero`).
- **Dos empresas del mismo tenant** (A1, A2) no comparten compartición ni decisiones: cada una tiene su fila de `comparticion_version` y su `version_aceptada`.

### 5.3 Matriz de visibilidad

| Dato | Tercero | Empresa A1 | Empresa A2 (mismo tenant) | Empresa B1 (otro tenant) | Integración de A1 | Auditor del tenant A | Plataforma |
|---|---|---|---|---|---|---|---|
| Datos maestros del tercero | ✓ propone (IA valida si disponible) | ✓ lee | ✓ lee | ✓ lee | ✓ lee | ✓ | — (salvo acceso temporal concedido, B-6) |
| Bóveda antes de que el tercero acepte la invitación de la empresa | ✓ | solo `requisitosCubribles` | ídem | ídem | ídem | ídem | — |
| Documentos de personas relacionadas (cédula/antecedentes del representante, B-3) | ✓ (la persona ve los suyos; el tercero jurídico ve el estado) | ✓ compartidas vía el vínculo con el tercero jurídico | ✓ ídem | ✓ ídem | ✓ | ✓ | — |
| Campos privados del tercero (código ERP, categoría) | — | ✓ los suyos | ✓ los suyos | ✓ los suyos | ✓ | ✓ | — |
| Versión actual/superadas de un documento global | ✓ | ✓ compartidas | ✓ compartidas | ✓ compartidas | ✓ | ✓ | — |
| Hechos compartidos | ✓ (lenguaje llano) | ✓ | ✓ | ✓ | ✓ | ✓ | métricas |
| Documento `PorVinculo` de A1 | ✓ | ✓ | ✗ | ✗ | ✓ | ✓ | — |
| Decisiones/veredictos de A1 | estado agregado | ✓ | **✗** | ✗ | ✓ | ✓ | — |
| Versión aceptada por A1 y su vigencia | ✓ (por empresa) | ✓ | ✗ | ✗ | ✓ | ✓ | — |
| Existencia y estado del vínculo con A1 | ✓ | ✓ | ✓ (solo existencia/estado) | ✗ | ✓ | ✓ | conteo |
| Bloqueo del tercero en A1 | ✗ (ve "no disponible") | ✓ | ✗ | ✗ | ✓ | ✓ | — |
| Cuenta designada para A1 | ✓ la propone | ✓ | ✗ | ✗ | ✓ | ✓ | — |
| Certificado de A1 | ✓ el suyo | ✓ | `/verificar` público | `/verificar` | ✓ | ✓ | — |
| Configuración del tenant (flujos, tipos, áreas, usuarios) | lo que ve en su expediente | ✓ | ✓ | ✗ | scope | ✓ | — |
| KPIs | — | los suyos | los suyos | ✗ | scope | agregados del tenant sin detalle de decisiones | agregados por tenant |
| Auditoría de accesos a documentos | accesos a sus documentos (empresa, fecha, motivo) | los de A1 | los de A2 | los de B1 | — | ✓ | — |
| Prompts/respuestas de IA | ✗ | evidencia resumida | evidencia resumida | ídem | — | ✓ completa | sin contenido |

---

## 6. Protección de datos y binarios

| Capa | Medida |
|---|---|
| En tránsito | TLS 1.2+; HSTS; la API rechaza HTTP |
| Binarios | MinIO con **SSE-KMS y clave por tenant** para `tenant-{id}` (P-14-3); `boveda` con clave de plataforma; buckets separados con IAM; versionado; Object Lock governance sobre versiones referenciadas (prod). Cierre de tenant ⇒ borrado criptográfico de su clave |
| Región | Atributo del tenant (P-14-2); un despliegue por región; on-premise = instancia del cliente; la IA externa se declara en la autorización |
| Base de datos | Cifrado de volumen; columnas sensibles (SMTP, secretos de webhook, credenciales de verificadores, números de cuenta) con Data Protection **por tenant**; claves protegidas por certificado X.509 o KMS |
| Secretos | `VALIDASOFT__*` / `_FILE` / Key Vault; test que falla si `appsettings` contiene patrones de clave. Los secretos en reposo (llaves de OpenAI, secretos de webhook, números de cuenta) van cifrados con la clave maestra `Validasoft:Secretos:ClaveMaestra`, rotable (§6.3). El sello de los certificados es una semilla Ed25519 (`Validasoft:Certificados:ClavePrivada`, 32 bytes en base64; en `appsettings.Development.json` solo una de desarrollo): su `kid`, la publicación de la pública en `/.well-known/validasoft-certificados.json` y la rotación (la anterior pasa a `ClavesAnteriores`) están en CE-4 del [registro](decisiones/registro.md) |
| Logs | Sin tokens, llaves ni cuerpos; redacción de correos e identificaciones |
| Backups | Cifrados; restauración probada en pipeline (GitHub Actions) |

### 6.1 Ingesta segura
1. Paso 1 declara `sha256`, `tamanoBytes`, `tipoMime`; límites **20 MB / 50 páginas** (P-16-2) y lista blanca (`application/pdf`, `image/jpeg`, `image/png`, `image/webp`; HEIC convertido en cliente; XLSX para EEFF en H7).
2. URL prefirmada fija `Content-Type`, checksum y tamaño.
3. Worker: existencia y hash; magic bytes; ClamAV (obligatorio en prod); PDF con JavaScript/formularios activos/adjuntos ⇒ `Devuelta`; previsualización aislada con timeout; **señales de manipulación** (capas, fuentes, metadatos de edición, fecha de creación posterior a expedición) como hecho (adición 6).
4. Cuarentena `cuarentena/` 30 días; nunca se comparte.
5. Solo lo limpio llega a `VersionActualCambiada`.

### 6.2 URLs prefirmadas y auditoría de acceso
Emisión solo tras autorizar (§5.2); 5 min descarga / 15 min subida; claves por hash; `Content-Disposition` fijo; cada emisión escribe `DocumentoAccedido(VersionId, TenantId?, EmpresaId?, ActorId, ActorTipo, Ip, UserAgent, Motivo, ExpedienteId?)` en `auditoria-accesos-{terceroId}` con la **misma retención que el documento** (P-14-7).

### 6.3 Clave maestra de los secretos en reposo (PT-1…PT-3)

Las llaves de OpenAI de cada empresa, los secretos de los webhooks, los números de cuenta bancaria y los tokens y códigos que esperan en el outbox se cifran con AES-256-GCM y la **clave maestra** `Validasoft:Secretos:ClaveMaestra` (32 bytes aleatorios en base64). Cada texto cifrado lleva el identificador de su clave: `vs1.{kid}.{datos}`, donde `kid` son los 8 primeros caracteres hexadecimales del SHA-256 de la clave (lo muestra `GET /api/v1/plataforma/secretos` y la consola de Plataforma › Secretos). La clave ya no depende de `Validasoft:Sesiones:ClaveFirma`: rotar la de sesiones no toca los secretos.

**Generarla** (una por entorno; nunca en el repositorio ni en `appsettings.json`, que la deja vacía):

```bash
openssl rand -base64 32
# o: python -c "import os,base64; print(base64.b64encode(os.urandom(32)).decode())"
# o (PowerShell): [Convert]::ToBase64String([Security.Cryptography.RandomNumberGenerator]::GetBytes(32))
```

Se entrega como secreto de configuración: `VALIDASOFT__SECRETOS__CLAVEMAESTRA` (o `Validasoft__Secretos__ClaveMaestra`), archivo `_FILE` o Key Vault. Sin una clave válida la API **no arranca** (mensaje con el motivo, nunca con la clave). En desarrollo hay una de solo desarrollo en `appsettings.Development.json` y en `infra/compose/compose.yaml`: no hace falta configurar nada.

**Rotarla:**

1. Genera la nueva y pon la actual en `Validasoft:Secretos:ClavesAnteriores` (arreglo; solo descifra): `ClaveMaestra = nueva`, `ClavesAnteriores:0 = actual`. Despliega todas las réplicas con esa configuración.
2. En la consola de Plataforma › Secretos (o `POST /api/v1/plataforma/secretos/recifrar`) re-cifra con la vigente las llaves de OpenAI, los secretos de webhook y los números de cuenta. Es idempotente; lo que quede con «Ilegibles» es porque su clave no está configurada.
3. Espera a que se vacíe el outbox de correo (los sobres viven minutos) y revisa `GET /api/v1/plataforma/secretos`: «Por re-cifrar» en cero.
4. Mantén la anterior en `ClavesAnteriores` mientras existan **copias congeladas en eventos** con ella: la cuenta designada de un vínculo (`CuentaParaDesignar.NumeroProtegido`) no se reescribe porque los eventos son inmutables. Quitarla deja esas copias ilegibles para siempre.

**Compatibilidad con lo cifrado antes de la clave maestra** (sin `vs1.`): se descifra con la clave derivada (HKDF-SHA256) de la `ClaveFirma` de sesiones vigente. Si se va a rotar la clave de sesiones antes de re-cifrar, la anterior va en `Validasoft:Secretos:ClavesFirmaHeredadas` (arreglo). Tras el primer re-cifrado lo único que queda en ese formato son las copias en eventos y los sobres del outbox.

**Desarrollo y pruebas:** `Validasoft:IA:Proveedor = Simulado` no llama a OpenAI (la e2e lo usa); por defecto es `OpenAi`, así que basta cargar la llave real de una empresa en su plan de IA para que sus documentos se analicen con ella.

---

## 7. Webhooks e integraciones salientes
Firma HMAC con timestamp; secretos de 256 bits con rotación solapada; payloads sin binarios ni decisiones ajenas; `empresas[]` por suscripción; solo `https`, sin rangos privados, DNS pinning; verificadores externos con credenciales por tenant cifradas, timeouts, circuit breaker, sin reintento en no idempotentes.

---

## 8. Datos personales, retención y habeas data

- **Base legal.** Primer acceso: aviso de tratamiento de la plataforma (incluye IA externa y transferencia internacional, P-6-5) + autorización de la empresa (`AUTORIZACION_TRATAMIENTO_DATOS`, `PorVinculo`) + T&C de la empresa; evidencia versión/fecha/IP/OTP. **Contrato de encargo** tenant ↔ plataforma: contrato marco + anexo técnico aceptado al crear el tenant (P-14-6).
- **Minimización.** `/verificar` expone nombre e identificación enmascarada (P-8-1); webhooks con lo mínimo.
- **Retención (P-14-1).** `tipo_documento_tenant.RetencionAnios`: 10 años para documentos de la relación comercial y de cumplimiento desde el fin del vínculo; identidad del tercero mientras exista vínculo + 10 años; evidencias de listas 10 años; configurable hacia arriba; Legal valida. Prompts y respuestas de IA con la misma retención que la versión (P-6-6). Versiones no referenciadas: gracia 90 días. Los registros de **auditoría de configuración e identidad** (`auditoria_configuracion`, `auditoria_identidad`, [03 §2.1](03-dominio-y-eventos.md)) se conservan como los accesos a documentos (P-14-7): la retención más larga del tenant, append-only, exportables y nunca editables; un intento de borrado directo es a su vez un evento de identidad.
- **Derechos (Ley 1581 de 2012, Decreto 1377 de 2013).** Acceso: bóveda completa + auditoría de accesos + exportación ZIP firmada (`POST /public/v1/boveda/exportar`). Rectificación: versión nueva o corrección con rastro. Supresión: **flujo formal** (P-14-5) `POST /public/v1/solicitudes-supresion` → evaluación automática de retención por documento → supresión de lo **no referenciado**; lo referenciado por una `version_aceptada` o un certificado de cualquier empresa **nunca se anonimiza ni se altera** (rompería el hash del certificado): se marca `RetencionLegalMarcada` con la **retención más larga** entre las empresas que lo referencian, se bloquea para nuevas comparticiones y se documenta la base legal; las retenciones dudosas las escala la saga `SupresionDatos` a cada empresa (`supresiones:resolver`) y la decisión más restrictiva prevalece; respuesta con constancia dentro de 15 días hábiles del calendario del tenant. Portabilidad: el ZIP.
- **Responsable y encargado.** Cada empresa es responsable de los datos de sus vínculos; la plataforma es encargada.

---

## 9. Inteligencia artificial: qué sale y qué se guarda

| Aspecto | Regla |
|---|---|
| Qué se envía | Binario o datos de la versión, tipo esperado, reglas del tipo, datos declarados mínimos (identificación, nombre). Nunca decisiones de empresas ni datos de otros terceros |
| Cuándo se envía | **La lectura de hechos es un servicio de plataforma** (B-2): toda versión nueva se lee una vez si (a) la IA está habilitada a nivel plataforma (clave configurada; on-premise puede no tenerla) y (b) el tercero aceptó el aviso de tratamiento que incluye la IA externa (P-6-5). Sin (a) o (b): hechos mínimos sin IA (hash, antivirus, MIME real, páginas, comparación por hash; P-1-7). No depende de ningún tenant ni de su presupuesto |
| Quién paga | La lectura va en el cargo por vínculo activo de la plataforma; el **presupuesto por tenant** limita solo las **decisiones** automáticas/asistidas (`RevisionAutomaticaConfigDto`) |
| Proveedor | Anthropic API; sin entrenamiento con datos del cliente; "zero data retention" cuando esté disponible; región declarada |
| Qué se guarda | `verificacion_version` (modelo, hash de prompt, respuesta, confianza, tokens, coste) + prompt/respuesta íntegros en `ia/{terceroId}/{documentoId}/v{n}.json` con la retención de la versión |
| Acceso | Revisor: evidencia resumida; Auditor: completa; Plataforma: métricas (contenido solo con acceso temporal de soporte, B-6) |
| Presupuesto de decisiones | Por tenant; 80 % alerta; agotado ⇒ decisiones en `Manual` con alerta, la carga y la lectura continúan (P-6-4); `Continuar` facturando excedente solo si el plan lo contempla (`PlanDto.accionAlAgotar`, I-23) |
| Históricos migrados | Solo se leen con IA los terceros que ya aceptaron el aviso en su primer ingreso; hasta entonces revalidación manual o por hash (B-8) |
| Derechos | Revisión humana a petición del tercero (P-6-8); métricas de equidad (adición 10) |

---

## 10. Amenazas y mitigaciones (STRIDE)

| Amenaza | Ejemplo | Mitigación |
|---|---|---|
| Suplantación | Magic link interceptado | Un solo uso, 72 h, OTP, sesión ligada, `410` |
| Suplantación | Token de refresco del tercero robado (XSS, equipo compartido) | Cookie `HttpOnly; Secure; SameSite=Lax` con `Path=/public/v1/sesion`; rotación en cada renovación y **detección de reuso** que revoca la familia (RE-3); máximo absoluto 8 h; cierre explícito |
| Suplantación | CSRF contra renovar o cerrar | `SameSite=Lax` + cabecera obligatoria `X-Validasoft-Cliente: public` (un formulario de otro sitio no puede ponerla; sin ella `403`) |
| Divulgación | Enumerar correos con «pedir acceso» | Respuesta idéntica (`202`) exista o no; límite por IP y 3 envíos por hora por correo sin revelarlo; la carrera entre dos solicitudes se resuelve sin cambiar la respuesta |
| Denegación | Bombardear el buzón de un tercero con «pedir acceso» | 3 enlaces por hora por correo (RE-2) además de 60/min por IP |
| Suplantación | Token del ERP falsificado | Introspección/firma por tenant |
| Suplantación | Aserción `X-Usuario-Delegado` capturada (log, proxy) y repetida dentro de sus 5 min | `jti` obligatorio de un solo uso por sistema (SE-4): segundo uso ⇒ `401 asercion-reutilizada`, también en paralelo |
| Manipulación | Clickjacking: un sitio ajeno enmarca las pantallas de Validasoft | `frame-ancestors 'self'` + orígenes registrados de sistemas activos (SE-1); la app del tercero solo `'self'`, salvo `/embed/{componente}` (la ruta de su iframe: IO-5), que admite además esos mismos orígenes |
| Suplantación | Página ajena que aloja el iframe le entrega una sesión o recibe sus mensajes | El puente solo acepta y envía `postMessage` a orígenes publicados por la API (SE-2) |
| Denegación | Evadir el límite por IP falseando `X-Forwarded-For` | La cabecera solo cuenta desde proxies confiables configurados y se toma la entrada que agregó el proxy (SE-6) |
| Suplantación | Cambio de cuenta bancaria fraudulento | Solo el tercero propone; certificación con `IdentidadCoincide`; 2 aprobadores de la empresa; evento al ERP |
| Manipulación | Binario ya aceptado | Hash, Object Lock, hash en certificado |
| Manipulación | Documento editado | Señales de manipulación como hecho; humano en caso de duda |
| Manipulación | Webhook falsificado | HMAC + timestamp + dedupe |
| Repudio | "Yo no aprobé" | Event store con actor/IP; veredictos automáticos con evidencia |
| Divulgación | Empresa A2 ve decisiones de A1 | `empresa_id` obligatorio + `ISesionDeEmpresa` + test arquitectónico |
| Divulgación | **Cosecha por NIT**: una empresa crea vínculos masivos con terceros existentes para leer sus EEFF/RUT | Nada se comparte hasta que el tercero acepta la invitación (B-1); `requisitosCubribles` sin nombres de documentos; rate limit de creación de vínculos/expedientes por API key (p. ej. 60/h) y alerta a Plataforma y al Administrador del tenant por creación masiva de vínculos con terceros ya existentes sin aceptación (> 20 en 24 h); métrica `vinculos_sin_aceptar_ratio` |
| Divulgación | Soporte de plataforma lee datos de un tenant | Sin acceso por defecto; concesión temporal por el tenant con alcance y auditoría (B-6) |
| Divulgación | URL prefirmada compartida | 5 min, auditoría, `Content-Disposition` |
| Divulgación | API key en navegador | Prohibido; sesiones cortas servidor a servidor |
| Denegación | Subidas masivas / PDF malicioso | Límites, rate limit, antivirus, aislamiento, cuarentena |
| Denegación | `/verificar` o autorregistro abusados | Rate limit por IP, caché |
| Elevación | Revisor fuera de su área o empresa | Permiso por área **y** alcance por empresa |
| Elevación | Administrador sin rastro | Comandos `…ComoAdministrador` con justificación |
| SSRF | Webhook a metadata | Solo https, bloqueo de privados, DNS pinning |

---

## 11. Operación de seguridad
**Implementado (2026-11-08, SE-7):** `.github/workflows/ci.yml` (build con advertencias como error, pruebas con Testcontainers, frontend completo, imagen Docker sin publicar y `dotnet list package --vulnerable` informativo) y `.github/dependabot.yml` (NuGet, npm/pnpm, Actions y Docker, semanal). Pendiente: `pnpm audit`, SBOM, Trivy, cosign, ZAP.

Dependabot, `dotnet list package --vulnerable`, `pnpm audit` en GitHub Actions; SBOM (Syft), Trivy, cosign, no root, FS de solo lectura; CSP estricta; suite de autorización negativa generada desde OpenAPI (endpoint × rol × empresa); pruebas de tenancy cruzada (dos tenants, dos empresas por tenant, un tercero); ZAP baseline; respuesta a incidentes (rotación masiva, revocación de sesiones, export firmado).

---

## Decisiones abiertas

1. ~~2FA para `Revisor`~~ **Cerrada** por P-14-4: obligatorio Administrador/Auditor/Cumplimiento; opcional para el resto; política por tenant.
2. **Passkeys/WebAuthn.** Propuesta: H8. Quién decide: Producto.
3. ~~Retención de binarios no referenciados~~ **Cerrada**: 90 días.
4. ~~Región del proveedor de IA y cláusulas~~ **Cerrada** por P-6-5/P-14-2: consentimiento expreso; región del tenant; Legal redacta el texto.
5. ~~Exclusión de empresas por el tercero~~ **Cerrada** por A-8: sin exclusión para ningún tipo.
6. **Validación del token del ERP.** Propuesta: introspección por tenant; si no existe, firma compartida rotable. Quién decide: Arquitectura con el equipo del ERP.
7. ~~Acceso de soporte de Plataforma~~ **Cerrada** por B-6: nunca por defecto; acceso temporal concedido por el tenant.
8. ~~Momento de la compartición~~ **Cerrada** por B-1: al aceptar la invitación.

## Fuentes verificadas

Ver [01-analisis-legado-srm-portal.md](01-analisis-legado-srm-portal.md): API key del Portal expuesta por `GET /TokenAdproveedor`, contraseñas en claro en correos y `InvitacionCorreoQueue.Password`, callbacks validados por `OrigenPortal.StartsWith("PORTAL")`, `AddJwtBearer()` sin autorización efectiva, `Enviroment: "Desarrollo"` que fija usuario 50.
