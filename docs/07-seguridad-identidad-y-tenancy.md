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

Validasoft es su propio proveedor OIDC (módulo `Identidad`, esquema `identidad`). Keycloak descartado en el MVP (ADR-0003); federable después (§2.5).

### 2.1 Flujos

| Cliente | Flujo | Tokens |
|---|---|---|
| `apps/web` (revisor/admin/plataforma) | Authorization Code + PKCE | Access JWT 15 min; refresh rotativo 8 h deslizante con detección de reuso; solo en memoria |
| `apps/public` (tercero) | Grant `urn:validasoft:magic-link` | Access JWT 30 min ligado a `terceroId` y vínculos; refresh deslizante máx. 8 h; cookie `HttpOnly; Secure; SameSite=Lax` |
| SRM/Portal dentro del marco ERP | Grant `urn:validasoft:erp-token` (token exchange) | Access JWT 15 min con `tenant`, `empresas[]`, `sub`, roles |
| Integraciones servidor-a-servidor | `X-Api-Key` | §4 |
| CLI de importación | Client Credentials con `importacion:escribir` | 15 min |

### 2.2 Grant `urn:validasoft:magic-link`
1. Token opaco de 256 bits; se guarda solo su hash SHA-256 con `terceroId`, `vinculoId?`, `expedienteId?`, `correoDestino`, `expira` (72 h), `requiereOtp`, `usado=false`.
2. Correo `https://{public}/e/{token}`; el token nunca va a logs ni analytics.
3. `POST /public/v1/enlaces/{token}/canjear`; con OTP: código de 6 dígitos (10 min, 5 intentos) al mismo correo, `POST …/otp`.
4. `usado=true` al canjear; segundo canje ⇒ `410 enlace-usado` y ofrece enlace nuevo.
5. Claims: `tipo=tercero`, `terceroId`, `vinculos[] { vinculoId, tenantId, empresaId, invitacionAceptada }` (todos los vínculos cuyo contacto coincide con el correo), `expedienteOrigen`. Canjear el enlace **solo abre sesión**; no comparte nada.
6. **Aceptar la invitación** (`POST /public/v1/invitaciones/{expedienteId}/aceptar`) registra `ConsentimientosRegistrados` —aviso de la plataforma la primera vez (incluye expresamente el procesamiento por IA externa y la transferencia internacional, P-6-5), autorización de datos y T&C de **esa** empresa— con versión del texto, fecha, IP, user-agent y OTP como evidencia (`AceptacionDto`; P-3-5, P-16-4). Solo entonces se comparte con esa empresa la versión actual de cada tipo global que exige (B-1). Hasta que el tercero no haya aceptado el aviso de plataforma, sus versiones se procesan **sin IA** (B-2/B-8).

### 2.3 Grant `urn:validasoft:erp-token`
Validación del token del marco contra el endpoint de introspección del ERP del tenant (cache 5 min) o firma compartida rotable; mapeo a usuario Validasoft del tenant con roles por empresa según mapeo configurado; nunca se guarda el token del ERP.

### 2.4 Usuarios internos
- Contraseñas Argon2id; mínimo 12 caracteres; bloqueo progresivo.
- **2FA TOTP obligatorio** para `Administrador`, `Auditor`, `Plataforma` y revisores del área **Cumplimiento**; opcional (recomendado) para el resto; el tenant puede exigirlo a todos (P-14-4). Códigos de recuperación; passkeys en H8 (decisión abierta).
- Recuperación por enlace de un solo uso (30 min). Sesiones listables y revocables.
- **Delegación** temporal por usuario (fechas, área, empresa) auditada (P-4-6).

### 2.5 Federación futura
Proveedor externo OIDC/SAML por tenant; mapeo de grupos a roles por empresa; cuentas locales desactivables.

---

## 3. Autorización: roles, alcance y permisos

### 3.1 Roles y alcance

| Rol | Alcance posible | Descripción |
|---|---|---|
| `Plataforma` | Global (`tenant_id = NULL`) | Crea tenants y empresas, administra el catálogo canónico y las propuestas, ve uso y costos agregados; **nunca accede a documentos ni decisiones por defecto**. Para un incidente, un `Administrador` del tenant le concede un **acceso temporal de soporte** (`AccesoSoporteDto`: alcance empresa/expediente/tercero, 1–72 h, justificación) que se registra con actor `Plataforma` en auditoría de configuración y en `DocumentoAccedido`; expira solo y es revocable (B-6). Sección `/plataforma` (P-15-2). |
| `Administrador` | Tenant o empresa | Configuración, usuarios, API keys, webhooks, override con justificación, bloqueos, reaperturas |
| `Revisor` | Empresa + área(s) | Veredictos en sus áreas; ve expedientes cuyos flujos incluyen un área suya |
| `Auditor` | Tenant o empresa | Solo lectura total, auditoría de accesos, exportaciones |
| `Cumplimiento` | Empresa | `Revisor` del área Cumplimiento + verificaciones externas + resolución de coincidencias; 2FA obligatorio |
| `Integracion` | API key con `empresas[]` | Scopes |
| `Tercero` | Sesión | Lo propio |

Un rol se asigna **por empresa** (`RolAsignado { rol, empresaId, areas[] }`) o **para todo el tenant** (`empresaId = *`). Un usuario puede pertenecer a varios tenants (claim `tenants[]`).

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
| `vencimientos:leer` / `escribir` | — | ✓ / ✓ | ✓ / ✓ | ✓ / ✓ | ✓ / — | scope | — |
| `auditoria:leer` (dominio, configuración, identidad y accesos; alcance por empresa o tenant) | — | ✓ | — | ✓ | ✓ | scope (solo referencias) | — |
| `auditoria:exportar` (ZIP firmado) | — | ✓ | — | — | ✓ | — | — |
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
| **Alcance por empresa** | `empresas[]` o `*`; una llave con `[A1]` no ve nada de `A2` aunque sea del mismo tenant: la API responde **`404 no-encontrado`** (T-4), nunca un 403 que revele existencia |
| **Precedencia con webhooks** | El alcance efectivo de una suscripción es `apiKey.empresas[] ∩ webhook.empresas[]`; intersección vacía ⇒ `422 empresas-fuera-de-alcance` al crear/editar; si una rotación o edición posterior de la llave la deja vacía, la suscripción se deshabilita (`webhook.deshabilitado`, causa `empresas-fuera-de-alcance`) y el administrador recibe correo |
| Rotación / revocación | Solapamiento 24 h (0–72); revocación inmediata; cache 60 s invalidado por evento |
| Registro | `ultimoUsoEn`, `ultimaIp`, contador; alerta por uso de llave inactiva 90 días |
| Restricción | IPs/CIDR, `expiraEn`; **sandbox** por llave de prueba (`vsk_test_…`) en H9 (P-11-4) |
| Prohibido | API keys en navegadores; el embed usa `POST /sesiones-tercero` servidor a servidor |

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
| Secretos | `VALIDASOFT__*` / `_FILE` / Key Vault; test que falla si `appsettings` contiene patrones de clave |
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
| Suplantación | Token del ERP falsificado | Introspección/firma por tenant |
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
