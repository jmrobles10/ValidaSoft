# 05 — API, contratos y webhooks

| Estado | Fecha | Responsable |
|---|---|---|
| Borrador (decisiones del registro propagadas) | 2026-10-28 | Arquitectura |

**Propósito.** Especificar la API pública v1 de Validasoft (REST + OpenAPI 3.1) que consumen las dos aplicaciones React, los SDKs y los sistemas integradores (SRM, Portal, Marco, terceros): recursos con permisos, códigos y eventos emitidos, convenciones transversales, **catálogo de DTOs**, ejemplos de payload, **catálogo de alertas y notificaciones**, catálogo de eventos de webhook, firma, reintentos, modo *pull*, versionado y compatibilidad. **Este documento es el dueño de los nombres de endpoints, DTOs y eventos públicos.** No hay gRPC en el MVP (ADR-0007).

**Relacionados.** [03-dominio-y-eventos.md](03-dominio-y-eventos.md) · [04-arquitectura-plataforma.md](04-arquitectura-plataforma.md) · [07-seguridad-identidad-y-tenancy.md](07-seguridad-identidad-y-tenancy.md) · [08-integracion-y-migracion.md](08-integracion-y-migracion.md) · [06-frontend-y-ux.md](06-frontend-y-ux.md) · [decisiones/registro.md](decisiones/registro.md)

---

## 1. Convenciones

| Tema | Regla |
|---|---|
| Base | `https://{host}/api/v1` (autenticado) y `https://{host}/public/v1` (sesión de tercero o anónimo). Versionado por URL. |
| Formato | JSON `camelCase`; fechas ISO-8601 UTC con `Z`; fechas sin hora como `YYYY-MM-DD`; identificadores UUID (v7 nuevos, v5 importados). |
| Especificación | OpenAPI 3.1 en `/openapi/v1.json`; snapshot en CI (GitHub Actions); los SDKs se generan de ahí. |
| Autenticación | `Authorization: Bearer <jwt>` (usuarios, terceros, CLI, **sesiones embebidas** de `POST /sesiones-usuario` y **sesiones directas** de `/ingreso`, §2.10) o `X-Api-Key: vsk_<entorno>_<tenant>_<aleatorio>` (integraciones). Nunca ambas. |
| Identidad delegada | Con la API key de un **sistema integrado**, la cabecera `X-Usuario-Delegado: <jwt>` actúa en nombre de una persona (ADR-0011): JWT RS256/ES256 firmado con la clave privada del sistema, `iss` = código del sistema, `aud` = `validasoft`, `exp` ≤ 5 min, `sub`, `name`, `email` y **`jti` obligatorio y de un solo uso** (SE-4: se firma una aserción por petición). El permiso efectivo es la intersección de la llave y los roles del usuario ([07 §2.6](07-seguridad-identidad-y-tenancy.md)). Fallos ⇒ `401` con `codigo` `asercion-*` (incluye `asercion-sin-jti` y `asercion-reutilizada`) o `usuario-inactivo`. |
| Tenant y empresa | **Tenant implícito en la credencial.** La **empresa** (razón social del grupo, ADR-0008) se indica con el header `X-Empresa: <empresaId>` en los recursos de empresa (vínculos, expedientes, certificados, cuentas designadas, bloqueos); si la credencial solo tiene alcance a una empresa, el header es opcional (T-5: cabecera, nunca segmento de ruta en la API). El header `X-Tenant: <slug>` lo usa **solo** el rol `Plataforma` en `/plataforma/*`. Un recurso de otro tenant o de otra empresa responde **`404 no-encontrado`** (T-4): no se revela su existencia. |
| Advertencias | Una operación que tuvo éxito nunca devuelve 4xx. Avisos no bloqueantes (p. ej. presupuesto de IA agotado ⇒ la carga continúa en nivel Manual) viajan en el cuerpo `2xx` como `advertencias: [{codigo, mensaje}]` y en la cabecera `Validasoft-Advertencias: presupuesto-ia-agotado`. |
| Idioma | Rutas y campos en español sin tildes. Enums `PascalCase` textual (`"EnRevision"`), exactamente los de [03 §3.1](03-dominio-y-eventos.md). |
| Nulos | Campo ausente = no aplica; `null` explícito = sin valor. |
| Idempotencia | `Idempotency-Key` obligatorio en todo `POST` que crea o dispara efectos; cadena libre ≤ 128 caracteres. §3.1 |
| Paginación | Cursor opaco `?limite=50&cursor=…` → `PaginaDto`. Máximo 200. |
| Filtros | Parámetros planos; listas separadas por coma; rangos `desde`/`hasta`. |
| Errores | RFC 9457 ProblemDetails (`ProblemaDto`). §3.3 |
| Concurrencia | `ETag` en agregados; `If-Match` obligatorio en `PUT`/`PATCH` y veredictos; `412`. |
| Correlación | `X-Correlation-Id` y `traceparent` propagados a metadatos y a `traceId`. |
| Rate limit | Por credencial (`RateLimit-*`, `429`); `/public/v1/verificar` por IP. |
| Binarios | Nunca por la API: URLs prefirmadas de MinIO (5–15 min). Límite 20 MB, PDF/JPG/PNG/WebP (HEIC convertido en cliente), 50 páginas (P-16-2). |
| Tiempo real | SSE `/api/v1/eventos/stream` (integraciones), `/api/v1/notificaciones/stream` (web de empresa) y `/public/v1/notificaciones/stream` (portal del tercero) con avisos de «cambió X» (§3.4, NT-12…NT-15); `/public/v1/expedientes/{id}/stream` (tercero). Resultado de IA en < 60 s p95 (P-6-9). |

---

## 2. Recursos

Columnas: **Permiso/scope** (ver [07 §3](07-seguridad-identidad-y-tenancy.md)), **Respuestas** (éxito y errores propios además de 400/401/403/404/429), **Emite** (eventos públicos de §7).

### 2.1 Plataforma, tenant y empresas

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `POST /plataforma/tenants` · `GET /plataforma/tenants` · `GET /plataforma/tenants/{id}` · `PATCH /plataforma/tenants/{id}` | Alta y configuración del tenant (`slug`, `region`, `claveKms`, `plan`, `presupuestoIa`, políticas) | `plataforma:tenants` | 201/200 | `tenant.creado` |
| `GET /plataforma/uso?tenant=&desde=&hasta=` | Medición de uso (`UsoTenantDto`) | `plataforma:uso` | 200 | — |
| `POST /plataforma/tenants` (alta completa) | **Implementado (PT-5).** Además de `slug`, `nombre`, `region` y `zonaHoraria` admite `empresa` (`CrearEmpresaDto`), `administrador` (`{ nombre, correo, idExterno? }`, rol Administrador de todo el tenant) y `sistema` (`CrearSistemaIntegradoDto`): en una sola transacción deja el grupo operable. `TenantCreadoDto` agrega `empresa`, `administrador`, `sistema` y `apiKeySistema` (su valor, una sola vez; todos los permisos del tenant). La respuesta no se guarda en el registro de idempotencia | `plataforma:tenants` | 201; 409 `slug-duplicado`; 422 `sistema-requerido` y las de cada parte | — |
| `GET /plataforma/grupos?q=&limite=&cursor=` | **Implementado (PT-5, PT-9).** Una página de grupos con sus empresas y el estado del plan de IA de cada una: `PaginaDto<GrupoDto> { datos[] { tenant, empresas[] { id, nombre, identificacion, prefijoFolio, activa, planIaConfigurado, planIaActivo, llaveConfigurada, ultimosCuatro } }, siguienteCursor }`. Orden estable por nombre del grupo e id; `limite` por omisión 20 (máximo 100); `cursor` opaco (`siguienteCursor` de la página anterior; nulo en la última). `q`: parte del nombre o del identificador del grupo, o de la razón social o el número de identificación de alguna de sus empresas (el grupo sale con todas las suyas). Solo se leen las empresas y los planes de los grupos de la página | `plataforma:tenants` | 200; 422 `cursor-invalido` | — |
| `GET /plataforma/grupos/{id}` | **Implementado (PT-10).** Un grupo con sus empresas y su plan de IA (`GrupoDto`) | `plataforma:tenants` | 200; 404 `tenant-no-encontrado` | — |
| `GET /plataforma/sistemas` · `PUT /plataforma/sistemas/{codigo}` | **Implementado (PT-4).** Sistemas del backoffice de Plataforma (`SistemaDePlataformaDto`: código, nombre, clave pública PEM, `origenesPermitidos[]`): firman las aserciones de los operadores y sus orígenes pueden alojar la consola. `PUT` crea o reemplaza (reemplazar la clave invalida las aserciones con la anterior) | `plataforma:tenants` | 200; 422 `codigo-invalido`, `clave-publica-invalida`, `origen-invalido` | — |
| `POST /plataforma/sesiones` | **Implementado (PT-4).** Canjea la credencial de Plataforma (`X-Api-Key`) y la aserción `X-Usuario-Delegado` de un operador (`iss` = código de un sistema de Plataforma activo) por una sesión de 30 min sin tenant (`SesionUsuarioDto`; actor `Plataforma`, solo `plataforma:tenants`) | credencial de Plataforma + aserción | 200; 401 `asercion-*`, `sistema-inactivo`, `operador-inactivo`; 403 `credencial-de-plataforma-requerida`; 422 `usuario-delegado-requerido` | — |
| `GET /plataforma/tenants/{t}/empresas/{e}/plan-ia` · `PUT …/plan-ia` · `POST …/plan-ia/probar-llave` | **Implementado (IA-8, PT-6).** Plan de IA de la empresa (`PlanIaEmpresaDto`); la API key de OpenAI es de solo escritura (nula conserva, vacía quita; solo vuelven `llaveConfigurada` y `ultimosCuatro`). `probar-llave` (`ProbarLlaveIaDto { apiKeyOpenAi? }`) verifica la del cuerpo o la guardada contra el proveedor sin consumir tokens: `PruebaDeLlaveIaDto { resultado: valida \| invalida \| limite \| sin-llave \| error, mensaje, ultimosCuatro, probadaEn }` | `plataforma:tenants` | 200; 404 `tenant-no-encontrado`, `empresa-no-encontrada`; 422 `api-key-invalida` y las del plan | — |
| `GET /plataforma/secretos` · `POST /plataforma/secretos/recifrar` | **Implementado (PT-3).** Estado de los secretos en reposo por tipo (`EstadoDeSecretosDto`: clave vigente, anteriores, total, vigentes, re-cifrados, ilegibles) y re-cifrado con la clave maestra vigente ([07 §6.3](07-seguridad-identidad-y-tenancy.md)) | `plataforma:tenants` | 200 | — |
| `GET /plataforma/tipos-documento/canonicos` · `POST` · `PUT /{id}` · `POST /{id}/publicar` · `GET /{id}/versiones` | Catálogo canónico versionado (`TipoDocumentoCanonicoDto`) | `plataforma:catalogo` | 200/201 | `catalogo.publicado` |
| `GET /plataforma/propuestas-tipo` · `POST /plataforma/propuestas-tipo/{id}/aprobar` | Propuestas de tipo canónico de los tenants | `plataforma:catalogo` | 200 | — |
| `GET /catalogos/tipos-identificacion?pais=CO` | Tipos de identificación por país con validador (`TipoIdentificacionDto`) | público autenticado | 200 | — |
| `GET /catalogos/paises` · `GET /catalogos/motivos-base` · `GET /catalogos/esquemas-formulario` | Catálogos de plataforma | autenticado | 200 | — |
| `GET /tenant` · `PATCH /tenant` | Configuración del propio tenant (zona horaria, políticas por defecto, SMTP, marca) | `configuracion:escribir` | 200 | — |
| `GET /empresas` · `POST /empresas` · `GET /empresas/{id}` · `PATCH /empresas/{id}` · `POST /empresas/{id}/desactivar` | Razones sociales del tenant (`EmpresaDto`: NIT, nombre, logo, `replyTo`, prefijo de folio) | `empresas:administrar` | 200/201 | `empresa.creada` |
| `GET /api-keys` · `POST /api-keys` · `POST /api-keys/{id}/rotar` · `DELETE /api-keys/{id}` | Llaves con `scopes[]`, `empresas[]` (`*` = todas), `expiraEn`, IPs y `sistemaId` (sistema integrado dueño; sin él la llave no delega ni registra referencias externas) | `api-keys:administrar` | 200/201; 422 `sistema-invalido` | — |
| `GET /sistemas` · `POST /sistemas` · `GET /sistemas/{id}` · `PUT /sistemas/{id}/clave-publica` · `PUT /sistemas/{id}/origenes` · `POST /sistemas/{id}/desactivar` | Sistemas integrados del tenant (`SistemaIntegradoDto`: código en mayúsculas único, nombre, **clave pública** PEM RSA ≥ 2048 o EC P-256, **`origenesPermitidos[]`**). Rotar la clave invalida de inmediato las aserciones firmadas con la anterior; desactivar el sistema deja sin efecto sus llaves y sus orígenes (ADR-0011). Los orígenes (§2.9.4) dicen desde qué sitios puede embeber las pantallas | `sistemas:administrar` | 200/201; 409 `sistema-duplicado`, `sistema-inactivo`; 422 `clave-publica-invalida`, `origen-invalido`, `origenes-excesivos` | — |
| `GET /usuarios?correo=` · `POST /usuarios` · `GET /usuarios/{id}` · `PUT /usuarios/{id}/roles` · `POST /usuarios/{id}/desactivar` | Usuarios y roles **por empresa o por tenant** (`RolAsignadoDto { rol, empresaId? }`). El alta anticipada permite asignar roles antes del primer acceso; un usuario de sistema integrado que llega sin alta se crea solo, **sin roles** (ID-2). `PUT …/roles` reemplaza todos los roles; una credencial limitada a ciertas empresas solo toca roles de esas empresas | `usuarios:administrar` | 200/201; 403 `alcance-insuficiente`; 409 `usuario-duplicado`, `identidad-duplicada`, `usuario-inactivo`; 422 `rol-invalido`, `empresas-invalidas` | — |
| `PATCH /usuarios/{id}` · `POST /usuarios/{id}/2fa/reiniciar` · `POST /usuarios/{id}/delegaciones` | Cuentas locales con 2FA y delegaciones temporales (hito posterior, ADR-0011) | `usuarios:administrar` | 200/201 | — |
| `POST /sesiones-usuario` | Canjea la identidad delegada (API key + `X-Usuario-Delegado`) por una **sesión de Validasoft** de 30 min para pantallas embebidas (`SesionUsuarioDto`); se envía como `Authorization: Bearer` (ID-3) | API key de sistema + aserción | 200; 401 `asercion-*`; 403 `modo-de-ingreso-no-admitido` (el grupo no admite el modo `Delegado`, ING-8); 422 `usuario-delegado-requerido` | — |
| `GET /roles` · `GET /permisos` | Catálogo | `usuarios:administrar` | 200 | — |
| `GET /yo` | **Implementado (RQ-9).** Quién hace la petición (`YoDto`): `actorTipo`, `actorId`, `tenantId`, `usuario {id, nombre}` si es una persona (identidad delegada o sesión), sus `roles[]` en las empresas a su alcance, `permisos[]` efectivos en la empresa activa (`X-Empresa`), `sistema` de la credencial, `empresas[] {id, nombre}` que puede usar y `empresaActiva` | cualquier credencial de tenant | 200 | — |
| `GET /usuarios/resumen?ids=a,b,c` | **Implementado (RQ-9).** Nombre visible de usuarios del tenant (`UsuarioResumenDto { id, nombre }`, sin correo ni roles; activos o no; hasta 100 ids, los inexistentes se omiten) para mostrar quién decidió en expedientes y bandejas | `expedientes:leer` **o** `vinculos:leer` | 200; 403 `permiso-insuficiente`; 422 `ids-invalidos` | — |
| `GET /health/live` · `GET /health/ready` | Sin auth | — | 200/503 | — |
| `GET /health` | Detalle | `operacion:leer` | 200 | — |
| `GET /openapi/v1.json` · `GET /openapi/public-v1.json` | Contratos OpenAPI 3.1 (fuente de orval y de los SDKs; snapshot en CI) | sin auth | 200 | — |
| `GET /plataforma/operacion/dead-letters` · `POST /plataforma/operacion/dead-letters/{id}/reintentar` · `POST /plataforma/operacion/proyecciones/{nombre}/rebuild` · `GET /plataforma/operacion/salud` | Operación de la plataforma: cola de errores de Wolverine, reconstrucción de proyecciones, salud detallada por módulo (`DeadLetterDto`, `RebuildDto`) | `plataforma:operacion` | 200/202 | — |
| `GET /tenant/plan` · `GET /tenant/uso?mes=` | Plan contratado y límites (`PlanDto`); medición del propio tenant (`UsoTenantDto`, por empresa) | `configuracion:leer` | 200 | — |
| `GET /tenant/revision-automatica` · `PUT /tenant/revision-automatica` | Política de **decisión** automática del tenant (`RevisionAutomaticaConfigDto`: habilitada, modelo por tipo, umbrales, muestreo, presupuesto mensual de decisiones, alerta). La **lectura** de hechos no se configura aquí: es coste de plataforma (B-2) | `configuracion:escribir` | 200; 422 `umbral-invalido` | — |
| `GET /tenant/accesos-soporte` · `POST /tenant/accesos-soporte` · `DELETE /tenant/accesos-soporte/{id}` | Un `Administrador` del tenant concede a Plataforma un **acceso temporal de soporte** (`AccesoSoporteDto`: alcance empresa/expediente/tercero, horas, justificación); revocable; expira solo (B-6) | `empresas:administrar` | 200/201; 422 `alcance-invalido` | `tenant.acceso_soporte_concedido`, `tenant.acceso_soporte_expirado` |
| `PUT /tenant/matriz-riesgo` · `GET /tenant/matriz-riesgo` | Matriz de riesgo configurable del estudio de cumplimiento (`MatrizRiesgoDto`, H8) | `configuracion:escribir` | 200 | — |
| `GET /empresas/{id}/dominio-correo` · `PUT /empresas/{id}/dominio-correo` · `POST /empresas/{id}/dominio-correo/verificar` | Dominio propio de correo con registros DKIM/SPF a publicar y verificación (`DominioCorreoDto`, H8) | `empresas:administrar` | 200; 422 `dkim-no-verificado` | — |

### 2.2 Configuración del tenant

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `GET /tipos-documento` · `POST` · `PUT /{id}` · `GET /{id}/esquema` | Tipos del tenant (`TipoDocumentoTenantDto`: **defaults del tenant**); `esquema` devuelve el JSON Schema canónico + campos privados (`EsquemaFormularioDto`) | `configuracion:escribir` | 200/201; 422 `ambito-no-ampliable` | — |
| `GET /empresas/{id}/tipos-documento` · `GET /empresas/{id}/tipos-documento/{tipoId}` · `PUT /empresas/{id}/tipos-documento/{tipoId}` · `DELETE /empresas/{id}/tipos-documento/{tipoId}` | **Override por empresa** (`TipoDocumentoEmpresaDto`): sobreescribe `reglaFrescura`, `nivelRevision`, `diasRevalidacion`, `politicaDesactualizada`, `aprobadoresRequeridos`, `porcentajeMuestreo`, `permiteRatificacion` (solo hacia más estricto). Precedencia: tenant → empresa → regla del flujo (B-4). `GET` devuelve el valor efectivo y su origen | `configuracion:escribir` (alcance a esa empresa) | 200; 422 `override-mas-laxo` | — |
| `GET /flujos/por-defecto` · `PUT /flujos/por-defecto` | Flujo por defecto por `(empresa, tipoVinculo)` (`FlujoPorDefectoDto[]`); obligatorio para cada tipo de vínculo activo de la empresa | `configuracion:escribir` | 200; 422 `falta-flujo-por-defecto` | — |
| `POST /tipos-documento/{id}/plantilla` · `GET /tipos-documento/{id}/plantilla` | Sube (dos pasos, como un documento) o descarga la plantilla versionada del tipo (`PlantillaDto`); cada versión cargada guarda `plantillaVersionId` (P-2-6) | `configuracion:escribir` / tercero y revisores | 201/200 | — |
| `GET /tipos-vinculo/{id}` · `PUT /tipos-vinculo/{id}` | Detalle y edición (`TipoVinculoDto`, incluye `exigeOtpEnEnlace`: el enlace de sus invitaciones pide OTP; semilla: solo `PROVEEDOR`) | `configuracion:escribir` | 200 | — |
| `GET /empresas/{id}/textos-legales` · `PUT /empresas/{id}/textos-legales/{tipo}` | Autorización de datos y T&C de la empresa (`tipo` = `autorizacion` \| `terminos`, o `AutorizacionEmpresa` \| `TerminosEmpresa`). Cada `PUT` (`PublicarTextoLegalDto { contenido }`) crea una **versión nueva inmutable** (`TextoLegalDto`: versión, hash SHA-256) con auditoría; `GET` devuelve las vigentes y las `faltantes`. Sin ambas publicadas, las invitaciones de la empresa no se pueden aceptar | `configuracion:escribir` / `configuracion:leer` | 200; 422 `tipo-texto-invalido`, `contenido-requerido` | — |
| `GET /plataforma/textos-legales/aviso` · `PUT /plataforma/textos-legales/aviso` | Aviso de tratamiento de la plataforma (incluye IA externa y transferencia internacional, P-6-5), versionado igual y con auditoría; al arrancar se siembra la v1 **provisional** si no existe | `plataforma:tenants` | 200 | — |
| `POST /tipos-documento/propuestas` | Proponer un tipo canónico a la plataforma | `configuracion:escribir` | 202 | — |
| `GET /areas` · `POST` · `GET /{id}` · `PUT /{id}` · `PUT /{id}/miembros` | Áreas revisoras de la empresa activa (`X-Empresa`; `AreaDto`); código inmutable y único por empresa. Un miembro debe ser un usuario activo con rol en esa empresa o en todo el tenant | `configuracion:escribir` (lectura: `configuracion:leer`) | 200/201; 409 `area-duplicada`, `area-inactiva`; 422 `miembros-invalidos` | — |
| `GET /motivos` · `POST` · `PUT /{id}` | Motivos propios (`MotivoDto`) | `configuracion:escribir` | 200/201 | — |
| `GET /tipos-vinculo` · `POST` | Proveedor, cliente, empleado… | `configuracion:escribir` | 200/201 | — |
| `GET /flujos` · `POST` · `GET /{id}` · `PUT /{id}` · `POST /{id}/publicar` · `GET /{id}/versiones` · `GET /{id}/versiones/{n}` | Flujos (`FlujoDefinicionDto`); al publicar, si no existe flujo por defecto para `(empresa, tipoVinculo)` el nuevo lo es (`esPorDefecto`) | `configuracion:escribir` | 200/201; 422 `falta-flujo-por-defecto` | `flujo.publicado` |
| `POST /flujos/{id}/versiones/{n}/aplicar-a-abiertos?simular=true\|false` | Aplica la versión a expedientes abiertos con filtro (`AplicarVersionDto`); con `simular=true` devuelve `ImpactoAplicacionDto` sin cambiar nada | `configuracion:escribir` + `Administrador` | 200 (simulación) / 202 | `flujo.aplicado_a_abiertos`, `expediente.requisitos_recalculados` |
| `GET /flujos/reglas-seleccion` · `PUT /flujos/reglas-seleccion` | Reglas ordenadas de selección automática de flujo por empresa (`ReglaSeleccionFlujoDto[]`); se evalúan cuando `POST /expedientes` llega sin `flujo` (I-12) | `configuracion:escribir` | 200; 422 `falta-flujo-por-defecto` | — |
| `POST /flujos/reglas-seleccion/simular` | Qué flujo elegirían las reglas para un contexto (`SimulacionDto` → `{flujoId, origen, reglaId}`) | `configuracion:leer` | 200 | — |
| `POST /flujos/{id}/simular` | Requisitos resultantes para un contexto (`SimulacionDto`) | `configuracion:leer` | 200 | — |
| `GET /plantillas-notificacion` · `PUT /plantillas-notificacion/{evento}/{canal}` | Cabecera/pie en MVP; Liquid restringido en H8 | `configuracion:escribir` | 200 | — |
| `GET /campanias` · `POST /campanias` · `POST /campanias/{id}/iniciar` | Campañas de renovación por tipo (H8) | `vencimientos:escribir` | 201 | `campania.iniciada` |

### 2.3 Terceros, cuentas y vínculos

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `GET /terceros` · `POST /terceros` · `GET /terceros/{id}` · `PATCH /terceros/{id}` | Datos globales (`TerceroDto`); `PATCH` solo campos privados del tenant; los maestros los edita el tercero | `terceros:leer`/`escribir` | 200/201; 409 `tercero-duplicado` | `tercero.creado` |
| `GET /terceros/por-identificacion?pais=CO&tipo=NIT&numero=900123456` | Búsqueda exacta (número normalizado) | `terceros:leer` | 200/404 | — |
| `GET /terceros/{id}/documentos` | Bóveda vista por la empresa: **solo versiones compartidas con ella** (tras la aceptación de la invitación, B-1), hechos, estado de aceptación en sus vínculos (`DocumentoVistaEmpresaDto[]`) | `documentos:leer` | 200 | — |
| `GET /terceros/{id}/relaciones` · `POST /terceros/{id}/relaciones` · `DELETE /terceros/{id}/relaciones/{rid}` | **Personas relacionadas** (B-3): representante legal, suplente, socio, beneficiario final, miembro de consorcio. Cada una es un `Tercero` persona natural con bóveda propia; la relación es tipada y con vigencia (`RelacionEntreTercerosDto`). `POST` crea el tercero relacionado si no existe (reconciliación por `IdentificacionDto`). La empresa solo puede **proponer**; el tercero confirma desde su bóveda (o la IA lo detecta en la Cámara) | `terceros:escribir` | 200/201; 409 `relacion-duplicada` | `tercero.relacion_cambiada` |
| `GET /terceros/{id}/cuentas-bancarias` | Cuentas globales con certificación (`CuentaBancariaDto[]`) | `terceros:leer` | 200 | — |
| `GET /terceros/{id}/frescura` | `FrescuraDocumentoTercero` para las empresas del tenant | `vencimientos:leer` | 200 | — |
| `GET /vinculos` · `POST /vinculos` · `GET /vinculos/{id}` · `PATCH /vinculos/{id}/atributos` | Vínculos de la empresa (`VinculoDto`). Al crear un vínculo con un tercero que ya tiene bóveda, la respuesta trae `requisitosCubribles: n` (cuántos requisitos podrían quedar cubiertos) **sin nombres de documentos ni versiones**; la compartición ocurre cuando el tercero acepta la invitación (B-1). **Una validación por `(empresa, tercero, tipo)` compartida entre sistemas (I-0):** el sistema de `referenciaExterna` lo fija la API key (el cuerpo puede omitirlo); si el vínculo ya existe y lo pide **otro** sistema, se agrega su referencia (`ReferenciaExternaAgregada`) y responde **200** con el vínculo existente; la misma referencia ⇒ 200 idempotente; el mismo sistema con otro id ⇒ 409 `referencia-en-conflicto`; sin referencia ⇒ 409 `vinculo-duplicado` | `vinculos:leer`/`escribir` | 200/201; 409 `vinculo-duplicado`, `referencia-en-conflicto`, `referencia-duplicada`, `tercero-bloqueado`; 422 `referencia-sistema-invalido`, `referencia-sin-sistema` | `vinculo.creado`, `vinculo.atributos_actualizados` |
| `GET /vinculos/por-referencia?sistema=SRM&id=ADP-00123` | Vínculo de la empresa activa por la referencia de un sistema integrado | `vinculos:leer` | 200/404 | — |
| `POST /vinculos/{id}/desvincular` · `/reintegrar` · `/anular` · `/bloquear` · `/desbloquear` · `GET /vinculos/{id}/reintegro` | Ciclo de vida y bloqueo privado. `reintegrar` recibe `ReintegrarVinculoDto { justificacion, revalidar = true }` (§2.4.3, DS-3) y `reintegro` dice si se puede reintegrar sin revalidar. **Bloquear suspende** el certificado y congela la compartición futura; **desbloquear restituye** certificado y compartición si siguen vigentes, o abre `Renovacion` si algo venció (B-5) | `vinculos:escribir` / `vinculos:bloquear` | 200; 409 `estado-invalido` | `vinculo.estado_cambiado`, `vinculo.bloqueado` + `certificado.suspendido`, `vinculo.desbloqueado` + `certificado.restablecido` |
| `GET /vinculos/{id}/versiones-aceptadas` | `VersionAceptadaDto[]` | `vinculos:leer` | 200 | — |
| `GET /vinculos/{id}/cuenta-designada` · `POST /vinculos/{id}/cuenta-designada/aprobar` · `POST /vinculos/{id}/cuenta-designada/rechazar` | La propone el tercero (public); la empresa aprueba (2 aprobadores por defecto) | `cuentas:aprobar` | 200; 409 `aprobador-repetido` | `vinculo.cuenta_designada_propuesta`, `vinculo.cuenta_designada` |
| `GET /vinculos/{id}/ficha` | **Ficha aceptada por la empresa** en el vínculo (F3, FM-35), para crear o actualizar el proveedor en el ERP (`FichaDelVinculoDto`): `secciones[]` solo con lo que la empresa **aceptó** (`seccion`, `nombre`, `periodo`, `version`, `aceptadaEn`, `expedienteId`, `folio`, `huella`, `contenido` — el mismo `ContenidoDeSeccionDto` del evento `seccion.aceptada`, §6.6 —, `versionVigente` y `versionNuevaPendiente`), `tipoPersona`, `referencias[]`, `representanteLegal` (el que recibe AyF, de la General y la Legal aceptadas) y `cuentaDesignada` (la aprobada; `numero` completo solo con `cuentas:aprobar`, CB-2) | `vinculos:leer` **y** `terceros:leer` | 200; 404 `vinculo-no-encontrado` (vínculo de otra empresa) | — |
| `GET /vinculos/{id}/historial` | `TimelineVinculo` | `vinculos:leer` | 200 | — |

### 2.4 Expedientes y requisitos

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `POST /expedientes` | Crea tercero/vínculo si no existen y abre el expediente (`CrearExpedienteDto` → `ExpedienteDto`). `flujo` es **opcional**: si falta, se aplican las reglas de selección y el flujo por defecto de `(empresa, tipoVinculo)`; la respuesta indica `origenFlujo` (I-12). Con tercero existente, la respuesta trae `requisitosCubribles` y los requisitos en `Pendiente` **sin** `versionActual` hasta que el tercero acepte la invitación (B-1) | `expedientes:escribir` | 201; 409 `expediente-abierto-existente`, `tercero-bloqueado`; 422 `falta-flujo-por-defecto` | `expediente.abierto`, `expediente.flujo_seleccionado`, (`vinculo.creado`) |
| `GET /expedientes` | Bandeja (`PaginaDto<BandejaExpedienteDto>`): `estado`, `tipo`, `flujoId`, `area`, `revisor`, `misPendientes`, `vencimiento`, `empresaId` | `expedientes:leer` | 200 | — |
| `GET /expedientes/{id}` · `GET /expedientes/{id}/requisitos` · `GET /expedientes/{id}/requisitos/{rid}` | `ExpedienteDto`, `RequisitoDto` | `expedientes:leer` | 200 | — |
| `POST /expedientes/{id}/invitaciones` | Enlace de acceso (`InvitacionDto`) | `expedientes:escribir` | 201 | `expediente.invitacion_enviada` |
| `POST /expedientes/{id}/requisitos/{rid}/documentos` | **Paso 1** carga de archivo (`IniciarCargaDto` → `CargaPrefirmadaDto`) | tercero / `documentos:escribir` | 201; 200 `duplicado`; 413; 415 | — |
| `POST /documentos/{docId}/versiones/{n}/confirmar` | **Paso 2** | tercero / `documentos:escribir` | 202; 422 `hash-no-coincide` | `documento.hechos_registrados`, `documento.version_nueva` o `documento.version_devuelta` |
| `POST /expedientes/{id}/requisitos/{rid}/formulario` | Envía un formulario (`EnviarFormularioDto` → `VersionDocumentoDto`) | tercero | 201; 400 `esquema-invalido` | `documento.version_nueva` (con `requiereRevalidacion`) |
| `POST /expedientes/{id}/requisitos/{rid}/ratificar` | Ratificación | tercero | 200; 422 `ratificacion-no-permitida` | `documento.ratificado` |
| `POST /expedientes/{id}/requisitos/{rid}/eximir` | `EximirDto` | `requisitos:eximir` | 200 | `expediente.requisito_eximido` |
| `POST /expedientes/{id}/enviar-a-revision` · `/reenviar` · `/reabrir` | Tercero completa / corrige; administrador reabre caducado (`ReabrirExpedienteDto`; vuelve al estado en que caducó con plazo nuevo y enlace nuevo al tercero, LT-3) | tercero / `expedientes:administrar` (reabrir) | 200; 409 `estado-invalido` | `expediente.enviado_a_revision`, `expediente.reenviado`, `expediente.reabierto` |
| `POST /plataforma/plazos-de-cargue/revisar` | Corre ya el job de plazos de cargue (`RevisarPlazosDto { tenantId?, fecha? }`; `fecha` solo en Development) → `ResultadoRevisionPlazosDto` (LT-4) | Plataforma | 200; 422 `fecha-simulada-no-permitida` | — |
| `POST /expedientes/{id}/requisitos/{rid}/veredicto` | `VeredictoDto` (`Aprobado` u `Observado`; al observar puede marcarse `noAdmitirEstaVersion`, B-7); `If-Match` | `veredictos:emitir:area:{X}` | 200; 409 `version-superada`, `version-no-admitida-en-vinculo`, `requisito-no-en-alcance`; 412 | `expediente.requisito_veredicto` |
| `POST /expedientes/{id}/areas/{areaId}/aprobar` · `POST /expedientes/{id}/areas/{areaId}/rechazar` | Confirmación de área / **rechazo terminal del expediente** (`RechazarExpedienteDto`; solo áreas con `PuedeRechazarExpediente`, B-7) | `veredictos:emitir:area:{X}` | 200; 403 `area-no-puede-rechazar` | `expediente.area_aprobada`, `expediente.rechazado` |
| `POST /expedientes/{id}/devolver-al-tercero` | Con `PoliticaDevolucion = AlCerrarArea` | revisor | 200 | `expediente.con_observaciones` |
| `POST /expedientes/{id}/asignaciones` · `DELETE /expedientes/{id}/asignaciones/{aid}` | Asignar revisor (`AsignacionDto`); 409 si conflicto de intereses | `expedientes:asignar` | 201; 409 `conflicto-de-interes` | — |
| `POST /expedientes/{id}/veredictos/{vid}/anular` · `POST /expedientes/{id}/areas/{areaId}/aprobar-como-administrador` | Override con justificación | Administrador | 200 | `expediente.requisito_veredicto` (origen Administrador) |
| `POST /expedientes/{id}/verificaciones/{tipo}` · `POST /expedientes/{id}/verificaciones/{tipo}/coincidencias/{cid}/resolver` | `VerificacionDto`, `ResolverCoincidenciaDto` | `verificaciones:escribir` | 201/200 | `expediente.verificacion_registrada` |
| `POST /expedientes/{id}/recalcular-requisitos` · `POST /expedientes/{id}/anular` | Administración | Administrador | 200 | `expediente.requisitos_recalculados`, `expediente.anulado` |
| `POST /expedientes/{id}/cambiar-flujo` | `CambiarFlujoDto { flujoVersionId*, justificacion* }`; solo `Abierto` u `Observado`; conserva veredictos de requisitos que persisten; auditado | Administrador | 200; 409 `estado-invalido` | `expediente.flujo_cambiado`, `expediente.requisitos_recalculados` |
| `POST /expedientes/{id}/exportar` | ZIP firmado (`ExportacionDto`) | `expedientes:leer` + `auditoria:leer` | 202 | `exportacion.lista` |
| `GET /expedientes/{id}/historial` | `TimelineExpediente` | `expedientes:leer` | 200 | — |
| `GET /revalidaciones` | `BandejaRevalidaciones` (`PaginaDto<RevalidacionDto>`) | `expedientes:leer` | 200 | — |
| `GET /excepciones` · `POST /excepciones/{id}/resolver` | `ColaDeExcepciones` (`PaginaDto<ExcepcionDto>`, `ResolverExcepcionDto`) | revisor | 200 | — |
| `GET /mis-pendientes` | `MisPendientes` del usuario en la empresa activa | sesión | 200 | — |
| `GET /flujos/resumen` | `FlujosResumen` (versión publicada, expedientes activos, tiempo promedio) | `configuracion:leer` | 200 | — |
| `GET /terceros/{id}/ficha` | `Tercero360` para la empresa activa | `terceros:leer` | 200 | — |
| `GET /public/v1/ficha/propuestas` · `GET /public/v1/ficha/{seccion}/propuestas?periodo=` (**F5**, FM-54) | **Actualizar desde el documento.** `PropuestaDeSeccionDto[] { seccion, nombreSeccion, periodo, documento { documentoId, version, tipoDocumento, nombreDocumento, cargadaEn }, disponible, motivo (`sin-lectura-automatica`), campos[] { campo, etiqueta, actual, propuesto }, datos? }`; `datos` (la sección con lo propuesto, solo en la de una sección) prellena el wizard. Solo lectura: no guarda nada. Sin la confianza, las alertas ni el costo del análisis | sesión tercero | 200 | — |
| `GET /invitaciones/lotes/plantilla?formato=` · `POST /invitaciones/lotes/vista-previa` · `POST /invitaciones/lotes` · `GET /invitaciones/lotes` · `GET /invitaciones/lotes/{id}` · `GET /invitaciones/lotes/{id}/filas?estado=&cursor=` · `POST /invitaciones/lotes/{id}/pausar` · `/reanudar` · `/cancelar` · `/reintentar` · `GET /invitaciones/lotes/{id}/informe` | Invitación masiva (`CrearLoteDto` → `LoteDto`; `VistaPreviaLoteDto`; `PaginaDto<FilaLoteDto>`; informe y plantilla en Excel/CSV); saga `InvitacionMasiva` por tandas (H8, LT-5…LT-10) | `vinculos:escribir` (leer: `vinculos:leer`) | 202/200; 422 `lote-sin-filas-validas`, `plantilla-invalida`, `archivo-invalido`, `lote-demasiado-grande`; 409 `lote-en-estado-terminal`, `estado-invalido`, `sin-filas-fallidas` | `invitaciones.lote_terminado` |

#### 2.4.1 Estado de implementación — fase «Flujos aprobatorios» (2026-11-06)

Implementado sin requisitos documentales ni verificaciones externas: **cada área se pronuncia sobre el expediente completo** (decisiones FL-1…FL-14 del [registro](decisiones/registro.md)). Todo con `X-Empresa`. Sin `If-Match`, las escrituras sobre un mismo expediente se serializan en el servidor (dos áreas que deciden a la vez no reciben 412); con `If-Match` se respeta la concurrencia optimista (FL-12).

| Ruta | Permiso | Notas y diferencias con §2.2/§2.4 |
|---|---|---|
| `GET /motivos` · `POST /motivos` · `PUT /motivos/{id}` | `configuracion:leer` / `configuracion:escribir` | Los 8 motivos base de plataforma (fijos, `base: true`, id determinista) más los propios del tenant (código en minúsculas con guiones, único, inmutable), con auditoría. El DTO se llama `MotivoTipificadoDto` (mismos campos que `MotivoDto` de §4; el nombre `MotivoDto` ya es el cuerpo `{ motivo }` de anular/desvincular). Editar un motivo base ⇒ 409 `motivo-base`. |
| `POST /flujos` · `GET /flujos?tipoVinculo=` · `GET /flujos/{id}` · `PUT /flujos/{id}` · `POST /flujos/{id}/publicar` · `GET /flujos/{id}/versiones` · `GET /flujos/{id}/versiones/{n}` | `flujos:leer` / `flujos:escribir` | Scopes propios en lugar de `configuracion:*`. `CrearFlujoDto { codigo, nombre, tipoVinculo (código), etapas?, politicaDevolucion? (Inmediata\|AlCerrarArea), separacionDeFunciones }`; sin `etapas` ⇒ una etapa con todas las áreas activas en paralelo. `EtapaDto { orden, areas[] { areaId, aprobadoresRequeridos (1–10), puedeRechazarExpediente, automatica, slaHoras? } }`. Nace en borrador v1; `PUT` (`EditarFlujoDto`, campos nulos conservan la última versión) edita el borrador o crea uno nuevo n+1; publicar congela y retira la publicada anterior. `FlujoDto { …, esPorDefecto, publicada, borrador }`; `FlujoVersionDto.estado` = `Borrador\|Publicada\|Retirada` (no `Archivado`). Desde la fase «Requisitos» (§2.4.2) llevan `requisitos[]`. Aún no existen `reglas`, `verificacionesRequeridas`, `politicaNuevaVersion`, plazos ni plantilla. |
| `GET /flujos/por-defecto` · `PUT /flujos/por-defecto` | `flujos:leer` / `flujos:escribir` | `FlujoPorDefectoDto { tipoVinculoId, flujoId, empresaId? }` (la empresa sale de `X-Empresa`); el flujo debe ser de la empresa, del mismo tipo y estar publicado (422 `flujo-sin-version-publicada`). Al publicar el primer flujo de `(empresa, tipo)` queda por defecto. No se exige todavía un flujo por defecto por cada tipo activo. |
| `POST /vinculos` | `vinculos:escribir` | `CrearVinculoDto.flujoId` opcional (flujo explícito del vínculo, FL-4). `VinculoDto` añade `expedienteAbiertoId` y `aperturaDeExpedientePendiente`. |
| `POST /vinculos/{id}/expedientes` | `expedientes:administrar` | **Nueva**: abre el expediente `Inicial` a mano (`AbrirExpedienteDto { flujoId? }`), sin exigir que el tercero haya aceptado; sustituye por ahora a `POST /expedientes` (crear tercero y vínculo a la vez llega después). 201 `ExpedienteDto`; 409 `expediente-abierto-existente`, `vinculo-bloqueado`, `estado-invalido`; 422 `falta-flujo-por-defecto`, `flujo-invalido`, `flujo-sin-version-publicada`. |
| `GET /expedientes?estado=&area=&vinculoId=&limite=&cursor=` · `GET /expedientes/{id}` · `GET /expedientes/{id}/historial` | `expedientes:leer` | `ExpedienteDto` reducido (los `requisitos[]` llegan en §2.4.2): sin `verificaciones`, `enlaceTercero`, `puede`, `plazoCargueHasta`; añade `politicaDevolucion`, `etapaActual`, `etapas[].areas[] { estado (Esperando\|Pendiente\|Aprobada\|Observada), aprobadoresRequeridos, aprobaciones, aprobacionesFaltantes, miembrosPendientes[], veredictos[] }`, `reenvios`, `ultimaRespuesta`, `rechazo`. `ETag` con la versión del stream. Cursor por posición. |
| `POST /expedientes/{id}/areas/{areaId}/veredicto` | `expedientes:revisar` + miembro del área | `VeredictoDto { veredicto: Aprobado\|Observado, motivos[], observacion }` (sin `area` ni `sobreVersion`: el área va en la ruta). Esto es el flujo sin requisitos (FL-1); con requisitos la misma ruta es la **decisión del área** sobre sus documentos (§2.4.2, DA-1). Solo una persona (identidad delegada o su sesión; si no, 422 `usuario-delegado-requerido`), miembro activo del área con rol en la empresa (403 `no-es-miembro-del-area`). 409 `expediente-observado`, `aprobador-repetido`, `area-ya-se-pronuncio`, `area-no-activa`, `area-automatica`, `vinculo-bloqueado`; 403 `conflicto-de-intereses`, `separacion-de-funciones`; 422 `motivo-requerido`, `motivo-invalido`, `motivos-solo-al-observar`. |
| `POST /expedientes/{id}/rechazar` | `expedientes:revisar` + miembro | `RechazarExpedienteDto { areaId, motivos[], justificacion }` (sin `notificarTercero`); ruta sin `areas/{areaId}`. Terminal. Se permite también con el expediente `Observado` (FL-14). 403 `area-no-puede-rechazar`. |
| `POST /expedientes/{id}/reenviar` | `expedientes:administrar` | El integrador responde en nombre del tercero (`ReenviarExpedienteDto { respuesta? }`). |
| `POST /expedientes/{id}/anular` | `expedientes:administrar` | `{ motivo }`; el vínculo en validación vuelve a `Invitado`. |
| `POST /expedientes/{id}/areas/{areaId}/aprobar-como-administrador` · `POST /expedientes/{id}/areas/{areaId}/anular-veredicto` | `expedientes:administrar` | `{ justificacion }` / `AnularVeredictoDto { justificacion, veredictoId? }` (sin id, el vigente más reciente). Sustituye a `POST /expedientes/{id}/veredictos/{vid}/anular`. Anular la última observación vigente de un expediente `Observado` lo devuelve a `EnRevision` (evento `RevisionReanudada`, FL-13). No revierte un rechazo (409 `estado-invalido`). |
| `GET /bandeja?area=&estado=&limite=&cursor=` | `expedientes:leer` | **Nueva** (la bandeja de §2.4 era `GET /expedientes`): proyección `BandejaRevision`, `FilaBandejaDto` por expediente × área (folio, tercero, tipo de vínculo, área, etapa, `estadoArea`, `estadoExpediente`, `aprobaciones`/`aprobadoresRequeridos`, `desde`) de expedientes `EnRevision` u `Observado`; por defecto oculta las áreas `Esperando`. Un usuario delegado solo ve las áreas donde es miembro. Con requisitos añade `requisitosPendientes` (los del alcance del área que aún esperan su decisión). |
| `GET /bandeja/aperturas-pendientes` | `expedientes:administrar` | **Nueva**: vínculos cuyo tercero aceptó sin que se pudiera abrir el expediente por falta de flujo (FL-6). |
| `POST /public/v1/invitaciones/{vinculoId}/aceptar` | sesión tercero | Además de lo de §2.9.1, abre el expediente inicial en la misma transacción (vínculo `EnValidacion`); sin flujo publicado la aceptación responde 200 igual y el vínculo queda con apertura pendiente. `VinculoPublicoDto.expedienteId`. |
| `GET /public/v1/expedientes/{id}` · `POST /public/v1/expedientes/{id}/reenviar` | sesión tercero | `ExpedientePublicoDto { id, folio, empresa, tipoVinculo, estado, observaciones[] { motivos[] { codigo, texto }, observacion, fecha }, puedeReenviar, reenvios, actualizadoEn }`, sin áreas ni revisores; solo expedientes de vínculos de la sesión (otro ⇒ 404). Reenviar `{ respuesta? }`. Con requisitos, ver §2.4.2. |

- **Correo de observación**: al quedar `Observado`, el tercero recibe por el outbox (cola `correo`) los textos de los motivos y la observación; nunca nombres de revisores ni áreas.
- **Concurrencia**: `If-Match` es opcional (como en vínculos) y, si llega, se exige; las respuestas llevan `ETag`. Apertura manual y aceptación se reintentan solas si pierden la carrera del folio o del vínculo.
- **Idempotencia**: `POST /motivos` y `POST /flujos` exigen `Idempotency-Key`; el resto de los `POST` nuevos la aceptan.

#### 2.4.2 Estado de implementación — fase «Requisitos» (H2, segunda parte, 2026-11-06)

Cada área aprueba u observa **documentos concretos** y su aprobación se deriva de ellos (decisiones RQ-1…RQ-10 del [registro](decisiones/registro.md)). En pantalla, como en el SRM, el área decide **una vez** («Aprobar área» u «Observar» marcando documentos); por dentro se guarda un veredicto por documento y versión (DA-1…DA-5). Un flujo **sin requisitos** conserva todo lo de §2.4.1 (FL-1). Sin reglas de exigencia por condiciones (§12 de [03](03-dominio-y-eventos.md)): los requisitos de una versión de flujo son fijos y se pueden eximir.

| Ruta | Permiso | Notas y diferencias con §2.4 |
|---|---|---|
| `POST /flujos` · `PUT /flujos/{id}` · `GET /flujos/{id}` · `GET /flujos/{id}/versiones[/{n}]` | `flujos:leer` / `flujos:escribir` | `CrearFlujoDto`/`EditarFlujoDto` y `FlujoVersionDto` ganan `requisitos[]: RequisitoDelFlujoDto { id?, tipoDocumentoTenantId? o tipoDocumento? (código), obligatorio (true), areasQueRevisan[] (vacío = todas las áreas del flujo), aprobadoresRequeridos? (nulo = los del área) }`. Se editan en el borrador (`null` conserva, `[]` quita) y se congelan al publicar; al leer vienen `id`, `tipoDocumentoTenantId` y `tipoDocumento`. 422 `requisito-invalido` (tipo inexistente o inactivo), `clase-no-soportada` (formulario), `requisito-duplicado`, `requisito-area-invalida`, `area-sin-requisitos` (con requisitos, toda área debe revisar al menos uno), `aprobadores-invalidos`. Sin `tiposAceptados`, `critico`, reglas ni overrides por requisito. |
| `GET /expedientes/{id}` (y la lista) | `expedientes:leer` | `ExpedienteDto.requisitos[]: RequisitoExpedienteDto { id, tipoDocumento {id, codigo, nombre, tipoCanonico, ambito}, obligatorio, estado (Pendiente, Cargado, EnRevision, Observado, Aprobado o Eximido), versionActual {documentoId, n, sha256, cargadaEn}?, reenvios, exencion {por, justificacion, fecha}?, areas[] { areaId, codigo, estado (Esperando, Pendiente, Aprobado, Observado o Eximido), aprobadoresRequeridos, aprobaciones, veredictos[] } }`; `VeredictoEmitidoDto` gana `sobreVersion`, `invalidado` y `derivado`; `AreaExpedienteDto` gana `requisitosPorDecidir` y `documentosPorDecidir[] { requisitoId, versionVigente }` (lo que «Aprobar área» aprobaría **a quien consulta** si es una persona, o lo que el área aún no decidió si es una integración; vacío si el expediente no está `EnRevision`, si la etapa del área no está activa, si el área es automática o sin requisitos; DA-3). Con requisitos el expediente nace `Abierto` (fase de carga) si falta algún obligatorio y pasa solo a `EnRevision` cuando todos están `Cargado` o `Eximido`. Sin `GET /expedientes/{id}/requisitos[/{rid}]` aparte. |
| `POST /expedientes/{id}/requisitos/{rid}/areas/{areaId}/veredicto` | `expedientes:revisar` + miembro del área | **Nueva** (en lugar de `…/requisitos/{rid}/veredicto` con `area` en el cuerpo); se mantiene junto a la decisión del área (DA-4). `VeredictoDto { veredicto, motivos[], observacion, sobreVersion* (número de la versión revisada) }`; mismas reglas de persona, conflicto de intereses y separación de funciones que §2.4.1. 404 `requisito-no-encontrado`; 409 `requisito-no-en-alcance`, `version-superada`, `aprobador-repetido`, `requisito-ya-decidido`, `requisito-eximido`, `requisito-sin-version`, `expediente-observado`, `area-no-activa`, `area-automatica`, `sin-requisitos` (flujo sin requisitos), `vinculo-bloqueado`; 422 `sobre-version-requerida`, `motivo-requerido`, `motivo-invalido`, `motivos-solo-al-observar`. El área queda aprobada cuando aprobó todo su alcance: el sistema emite `VeredictoDeAreaEmitido` derivado (origen `Administrador`, `Humano` o `Sistema` según quién aprobó los requisitos, RQ-5) y sigue la cascada (etapa, expediente, vínculo `Activo`). Una observación devuelve según `politicaDevolucion` (con `AlCerrarArea`, cuando cada área decidió todo su alcance). Sin `noAdmitirEstaVersion` ni `camposAfectados`. |
| `POST /expedientes/{id}/requisitos/{rid}/eximir` | `expedientes:administrar` | `JustificacionDto { justificacion }` (en lugar de `EximirDto` y del permiso `requisitos:eximir`, RQ-6). 409 `requisito-eximido`, `estado-invalido` (aprobado o expediente cerrado); 422 `justificacion-requerida`. Si era lo último que faltaba, pasa a revisión; si era la última observación de un expediente devuelto, reanuda la revisión. |
| `POST /expedientes/{id}/areas/{areaId}/veredicto` | `expedientes:revisar` + miembro del área | **Decisión del área** con requisitos (DA-1; sin requisitos, igual que §2.4.1). `VeredictoDto` gana dos campos opcionales: `versiones?: { [requisitoId]: número }` y `requisitos?: [ { requisitoId, sobreVersion, motivos[], observacion? } ]` (`motivos`/`observacion` de primer nivel no se usan con requisitos). **`Aprobado` («Aprobar área»)**: aprueba de una vez, como el revisor (origen `Humano`), todos los documentos del alcance del área que esa persona aún no decidió y que el área aún no tiene decididos (= `documentosPorDecidir`; con N aprobadores cuenta una aprobación de esta persona en cada uno). `versiones` debe traer cada uno con su versión vigente: falta alguno ⇒ 422 `versiones-requeridas`; alguna distinta a la vigente ⇒ 409 `version-superada`; ambos con la extensión `requisitos: [ { requisitoId, versionVigente } ]` y **todo o nada**. Versiones de documentos ya decididos se ignoran; un id fuera del alcance ⇒ 409 `requisito-no-en-alcance` (inexistente ⇒ 404 `requisito-no-encontrado`). Nada por decidir ⇒ 409 `area-ya-se-pronuncio`; con una observación vigente del área (posible con `AlCerrarArea`) ⇒ 409 `area-observada`. **`Observado`**: `requisitos[]` obligatorio (422 `requisitos-requeridos`; repetido ⇒ 422 `requisito-duplicado`); cada documento con las reglas del veredicto por documento (alcance, `sobreVersion` vigente, ≥1 motivo, `aprobador-repetido`, `requisito-ya-decidido`…; las versiones superadas se informan juntas en `requisitos`); emite un `Observado` por documento en un solo paso: **una** devolución (`ExpedienteObservado` con todos) y **un** correo con todas las observaciones; los no listados no se tocan. Lo demás como §2.4.1: 403 `no-es-miembro-del-area`, `conflicto-de-intereses`, `separacion-de-funciones`; 409 `vinculo-bloqueado`, `expediente-observado`, `area-no-activa`, `area-automatica`; bloqueo exclusivo sin `If-Match` (FL-12). Ya no existe el 409 `veredicto-por-requisito`. |
| `POST /expedientes/{id}/areas/{areaId}/aprobar-como-administrador` · `…/anular-veredicto` | `expedientes:administrar` | Con requisitos, aprobar como administrador aprueba (origen `Administrador`) los requisitos que el área aún no decidió; anular también alcanza los veredictos de requisito del área. |
| `POST /expedientes/{id}/reenviar` · `POST /public/v1/expedientes/{id}/reenviar` | `expedientes:administrar` / sesión tercero | Antes de reenviar adjunta las versiones ya compartidas. Cada requisito observado necesita una versión nueva o, si no, `respuesta` (409 `requisitos-sin-corregir`); solo los observados vuelven a revisión, los demás conservan sus veredictos. |
| `GET /public/v1/expedientes/{id}` | sesión tercero | `ExpedientePublicoDto` gana `requisitos[]: RequisitoPublicoDto { id, tipoDocumento, obligatorio, estado, versionActual?, observaciones[] }` y `requisitosFaltantes` (obligatorios sin cargar); `observaciones[].requisito` (código del documento). Sin áreas, revisores ni veredictos internos; una observación solo se ve cuando el expediente se devuelve. |
| `GET /bandeja` | `expedientes:leer` | `FilaBandejaDto.requisitosPendientes`. Las filas siguen siendo de expedientes `EnRevision` u `Observado` (uno `Abierto` en fase de carga no aparece). |

- **Eventos nuevos** del stream `Expediente-{id}` (compatibles con los guardados): `RequisitosCalculados`, `VersionAdjuntadaARequisito`, `VeredictosInvalidadosPorNuevaVersion`, `VeredictoDeRequisitoEmitido`, `RequisitoEximido`, `RequisitosAgregados` (FM-49); campos nuevos con valor por defecto en `VeredictoDeAreaEmitido.derivadoDeRequisitos`, `ExpedienteObservado.requisitosObservados`, `ExpedienteReenviado.requisitosReabiertos` y `RevisionReanudada.requisitoEximidoId`.
- **Versión nueva**: una versión que pasa a `Actual` (o se restituye) y se comparte con el vínculo se adjunta sola a los requisitos de su expediente abierto por el outbox (cola local durable `requisitos`, mensaje idempotente); si el requisito tenía veredictos, se invalidan solo los suyos (`PoliticaNuevaVersion = InvalidarSoloRequisito`, RQ-8). Un requisito ya aprobado por una etapa cerrada conserva su versión.
- **Compartición** (RQ-3): ver §2.9.2.
- **Pendiente**: eventos públicos y webhooks (`expediente.*`), SSE, SLA y escalamiento, asignaciones, delegaciones, reglas de selección, cambio de flujo y aplicación a abiertos, caducidad y reapertura.

#### 2.4.3 Estado de implementación — desaprobar, reintegrar sin revalidar y reinicio tras un rechazo (DS, 2026-11-10)

Decisiones DS-1…DS-10 del [registro](decisiones/registro.md). Todo con `X-Empresa`; las escrituras aceptan `If-Match` y devuelven `ETag` como el resto de los expedientes. La justificación es **siempre obligatoria** y queda en el historial.

| Ruta | Permiso | Notas |
|---|---|---|
| `POST /expedientes/{id}/etapas/{orden}/reabrir` | `expedientes:administrar` | `JustificacionDto { justificacion }` → `ExpedienteDto`. Reabre una etapa **ya aprobada** de un expediente `EnRevision` u `Observado` (`DesaprobarAFlujo`, DS-1): ella y las posteriores vuelven a esperar y sus veredictos vigentes quedan `revertido`. 404 `etapa-no-encontrada`; 409 `estado-invalido`, `etapa-no-aprobada`, `vinculo-bloqueado`; 422 `justificacion-requerida`. |
| `POST /expedientes/{id}/fase-inicial` | `expedientes:administrar` | `JustificacionDto`. Repite todo el flujo desde la etapa 1 (`DesaprobarAEstadoInicial`); en un expediente aprobado es además una desaprobación (DS-2). 409 `ya-esta-en-fase-inicial`, `estado-invalido`. |
| `POST /expedientes/{id}/desaprobar` | `expedientes:administrar` | `DesaprobarExpedienteDto { justificacion, etapa? }` (`DesaprobarTercero`, DS-2). Solo un `Aprobado`; vuelve a `EnRevision` desde `etapa` (por omisión la última). **En la misma transacción** el vínculo pasa a `EnValidacion` con este expediente abierto y su certificado se revoca. 409 `estado-invalido`, `vinculo-bloqueado`, `estado-terminal`, `expediente-abierto-existente`, `no-es-el-ultimo-aprobado`. |
| `GET /expedientes?desaprobados=true` | `expedientes:leer` | Filtro nuevo: los expedientes que un administrador devolvió a revisión alguna vez. `ExpedienteDto` gana `desaprobaciones` y `ultimaDesaprobacion { etapa, alcance: Etapa\|FaseInicial\|ExpedienteAprobado, usuario, justificacion, fecha }`; `VeredictoEmitidoDto.revertido`; `FilaBandejaDto.desaprobado`. |
| `POST /vinculos/{id}/reintegrar` | `vinculos:escribir` | `ReintegrarVinculoDto { justificacion, revalidar = true }` → `VinculoDto`. Revalidar abre una renovación con todo el flujo (422 `falta-flujo-por-defecto` si no hay flujo); sin revalidar restituye el estado y reemite el certificado. 409 `estado-invalido`, `reintegro-sin-aprobacion-vigente`, `reintegro-requiere-revalidacion` (con `motivos[]` en el problema). DS-3, DS-5. |
| `GET /vinculos/{id}/reintegro` | `vinculos:leer` | `ViabilidadDeReintegroDto { puedeSinRevalidar, motivos[] }`: solo lectura; los motivos van en texto para la persona (DS-6). |
| `PUT /flujos/{id}` · `POST /flujos` | `flujos:escribir` | `CrearFlujoDto`/`EditarFlujoDto`/`FlujoVersionDto.reintento { permitido, diasEspera }` (DS-4): por omisión no permitido; 0–365 días (422 `reintento-invalido`). |
| `GET /public/v1/vinculos` | sesión tercero | `VinculoPublicoDto.reintento { rechazadoEn, motivos[] { codigo, texto }, permitido, disponible, desde }` cuando el último expediente del vínculo se rechazó (textos al tercero; nunca áreas, revisores ni la justificación). `VinculoDto.ultimoResultado` para la empresa. |
| `POST /public/v1/vinculos/{id}/reintentar` | sesión tercero | «Volver a postularme»: abre un expediente `Inicial` nuevo → `ExpedientePublicoDto`. 409 `reintento-no-disponible`, `reintento-en-espera` (con `desde`), `vinculo-bloqueado`; 403 `reintento-no-permitido`. |

- **Eventos nuevos de dominio:** `EtapaReabierta` y `ExpedienteDesaprobado` (stream `Expediente-{id}`, mismos campos: `Orden`, `FaseInicial`, `AdministradorId`, `Justificacion`, `Fecha`), `VinculoDesaprobado(ExpedienteId, EstadoPrevio, Justificacion)` y `VinculoReintegrado(…, SinRevalidar)` (stream `Vinculo-{id}`). Públicos: **`expediente.desaprobado`** y **`vinculo.reintegrado`** (§7); el certificado revocado al desaprobar viaja en `certificado.revocado`.
- **Pendiente:** correo y campana al tercero por una desaprobación o un reintegro; la política de reintento se lee del flujo vigente, no de la versión que rechazó.

### 2.5 Documentos y versiones

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `POST /documentos` | **Carga directa a la bóveda sin expediente** (paso 1; `IniciarCargaDto` con `tipoCanonico*` y `terceroId*`): la usan el importador (H2, `importacion:escribir`) y, desde H9, integraciones con `documentos:escribir` en nombre del tercero (auditado). La versión sigue el mismo ciclo (`Recibida → EnAnalisis → Actual/Devuelta`) y, si es `Actual`, se comparte con todos los vínculos que exigen el tipo | `documentos:escribir` / `importacion:escribir` | 201; 200 `duplicado`; 413; 415 | `documento.hechos_registrados`, `documento.version_nueva` o `documento.version_devuelta` (tras confirmar) |
| `GET /documentos/{id}` · `GET /documentos/{id}/versiones` · `GET /documentos/{id}/versiones/{n}` | `DocumentoDto`, `VersionDocumentoDto` | `documentos:leer` | 200; 403 `version-no-compartida` | — |
| `GET /documentos/{id}/versiones/{n}/descarga` | URL prefirmada (`DescargaDto`); registra `DocumentoAccedido` | `documentos:leer` | 200 | — |
| `GET /documentos/{id}/versiones/{n}/previsualizacion?pagina=` | PNG prefirmado | `documentos:leer` | 200 | — |
| `GET /documentos/{id}/versiones/{n}/hechos` | `HechosDto` | `documentos:leer` | 200 | — |
| `PATCH /documentos/{id}/versiones/{n}/datos` | Corrección humana (`CorreccionDatoDto`) | `documentos:corregir-datos` | 200 | `documento.datos_corregidos` |
| `POST /documentos/{id}/versiones/{n}/retirar` | Retiro | tercero / Administrador | 200; 409 `version-referenciada` | — |

### 2.6 Certificados y consultas

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `GET /certificados` · `GET /certificados/{folio}` · `GET /certificados/{folio}/pdf` · `GET /certificados/{folio}/emisiones` | `CertificadoDto`; PDF/A | `certificados:leer` | 200 | — |
| `POST /certificados/{folio}/revocar` | `RevocarDto` | `certificados:revocar` | 200 | `certificado.revocado` |
| `GET /public/v1/verificar/{folio}` | **Público** (`CertificadoPublicoDto`) | — | 200/404 | — |
| `GET /consultas/certificacion?pais=CO&tipoIdentificacion=NIT&numero=900123456&tipoVinculo=proveedor` | `ConsultaCertificacionDto` desde `EstadoCertificacionTercero` para la empresa de `X-Empresa`; número **normalizado sin DV**; si no hay vínculo responde `200` con `estadoVinculo: "SinVinculo"`; `bloqueado` + `motivoBloqueo` (`Bloqueo` \| `PlazoRevalidacion` \| `Vencido`); `ETag`, `max-age=300`; `fuenteRespaldo`/`antiguedadSegundos` los rellena el integrador cuando sirve desde su tabla local | `consultas:leer` | 200/304 | — |

#### 2.6.1 Estado de implementación — fase «Certificado verificable» (H5, 2026-11-08)

Implementado el certificado del vínculo con su verificación pública (decisiones CE-1…CE-10 del [registro](decisiones/registro.md); dominio en [03 §7](03-dominio-y-eventos.md)). Todas las rutas de empresa piden `X-Empresa`; un folio de otra empresa o de otro tenant responde 404 `certificado-no-encontrado` (T-4).

| Ruta | Permiso | Contrato |
|---|---|---|
| `GET /api/v1/vinculos/{id}/certificado` | `certificados:leer` | `CertificadoDelVinculoDto { vinculoId, actual: CertificadoDto?, emisiones: EmisionDelCertificadoDto[] }`. `actual` es la emisión más reciente en el estado que tenga (nula si el vínculo nunca se aprobó); `emisiones` de la más reciente a la primera: `{ folio, emision, estado, emitidoEn, vigenteHasta?, huella, expedienteFolio, pdfDisponible }`. 404 `vinculo-no-encontrado`. |
| `GET /api/v1/certificados/{folio}` | `certificados:leer` | `CertificadoDto` de esa emisión (`…-{n}`) o de la actual (folio raíz `{Prefijo}-C-{Año}-{Secuencia}`). 404 `certificado-no-encontrado` (también con un folio mal formado). |
| `POST /api/v1/certificados/{folio}/descarga` | `certificados:leer` | Sin cuerpo. `DescargaDto { url, expira, nombre: "{folio}.pdf", mime: "application/pdf" }` (URL prefirmada de 5 min, `attachment`). Cada entrega queda en el stream del certificado (`CertificadoDescargado`: emisión, actor, IP, user-agent). Si el PDF aún no existe se genera en la petición. `Idempotency-Key` opcional. |
| `POST /api/v1/certificados/{folio}/revocar` | `expedientes:administrar` | `RevocarCertificadoDto { justificacion }` → 200 `CertificadoDto` (`estado: Revocado`, `revocacion { motivo, origen: Administrador, fecha }`). 422 `justificacion-requerida`; 409 `certificado-revocado` (ya lo estaba), `emision-no-actual` (folio de una emisión reemplazada). Irreversible. |
| `GET /public/v1/verificar/{folio}?huella=` | **anónimo** (límite por IP de `/public/v1`) | `CertificadoPublicoDto` de la emisión (o de la actual con el folio raíz). `huella` (opcional): los 12 primeros hexadecimales del hash, con o sin separadores (`5a7e·0c1d·9f22`); con ella también se encuentra una emisión anterior por el folio raíz. El folio solo es único dentro de un tenant: si hay más de uno, 409 `folio-ambiguo` y hay que enviar la huella (CE-8). 404 `certificado-no-encontrado` (folio inexistente, mal formado u otra huella); 422 `huella-invalida`. `Cache-Control: no-store`. |
| `POST /public/v1/verificar/{folio}/descarga?huella=` | **anónimo** (límite por IP) | `DescargaDto` del PDF **solo de una emisión `Vigente`** (otro estado ⇒ 409 `certificado-no-vigente`); auditada como descarga pública (`CertificadoDescargado { publico: true, actorTipo: "Publico", ip, userAgent }`). |
| `GET /.well-known/validasoft-certificados.json` | **anónimo** | `ClavesDeCertificadosDto { emisor, algoritmo: "Ed25519", mensajeFirmado, esquemas: ["validasoft.certificado.v1"], claves: [{ kid, algoritmo, clavePublica (base64, 32 bytes), jwk: { kty: "OKP", crv: "Ed25519", x (base64url), kid, alg: "EdDSA", use: "sig" }, vigente }] }` con la clave vigente y las anteriores (rotación, CE-4). `Cache-Control: public, max-age=3600`. |

**`CertificadoDto`** (empresa): `id`, `folio` (con emisión), `folioRaiz`, `emision`, `esLaActual`, `estado` (`Vigente | Suspendido | Vencido | Revocado | Reemplazado`; `ConPendientes` llega con H6), `bloqueante` (suspendido, revocado o vencido: no debe aceptarse), `empresa { id, nombre, logoUrl? }`, `vinculoId`, `expedienteId`, `expedienteFolio`, `tercero { nombre, identificacionEnmascarada, pais, tipoPersona }`, `tipoVinculo { codigo, nombre }`, `flujo { codigo, nombre, version }`, `emitidoEn`, `vigenteHasta?` (`YYYY-MM-DD`), `huella` (`xxxx·xxxx·xxxx`), `hashContenido` (64 hex), `urlVerificacion`, `requisitos[] { codigo, nombre, version?, sha256?, vigenteHasta?, eximido, justificacion? }`, `areas[] { area, codigo, origen: Humano | Sistema | Administrador, fecha }`, `pendientes: []`, `suspension? { motivo, desde }`, `revocacion? { motivo, origen: Administrador | Sistema, fecha }`, `pdf? { sha256, tamano, generadoEn }` (nulo mientras se genera), `descargas`, `firma { algoritmo: "Ed25519", kid, clavePublicaUrl, valor (base64) }`, `contenidoCanonico` (objeto: lo que se hasheó), `version`.

**`CertificadoPublicoDto`**: `folio`, `emision`, `estado`, `bloqueante`, `titular { nombre, identificacionEnmascarada }`, `emisor { empresa, logoUrl? }`, `flujo { codigo, nombre, version }`, `emitidoEn`, `vigenteHasta?`, `huella`, `hashContenido`, `contenidoCanonico` (objeto `CertificadoContenidoDto`), `urlVerificacion`, `pendientes: []`, `firma { algoritmo, kid, clavePublicaUrl, valor }`, `estadoDesde`, `reemplazadoPor?` (folio de la emisión actual si esta fue reemplazada), `pdfDescargable` (`true` solo si `Vigente`). Sin revisores, contactos, documentos ni el motivo del bloqueo.

**`CertificadoContenidoDto`** (lo que se hashea; esquema `validasoft.certificado.v1`): `esquema`, `folio` (con emisión), `emision`, `empresa { id, nombre, identificacion { pais, tipo, numero, digitoVerificacion? } }`, `tercero { nombre, identificacionEnmascarada, pais, tipoPersona }`, `tipoVinculo { codigo, nombre }`, `flujo { codigo, nombre, version }`, `requisitos[] { codigo, nombre, versionN?, sha256?, vigenteHasta?, eximido, justificacion? }` ordenados por `codigo` (eximido ⇒ sin versión ni hash, con justificación), `verificaciones: []`, `areas[] { area, codigo, origen, fecha }`, `emitidoEn` (UTC al segundo, `…Z`), `vigenteHasta?` (`YYYY-MM-DD`: la menor vigencia de los documentos aceptados que tienen una). Los nulos se omiten.

**Cómo verifica un cliente** (la página pública `{UrlPublica}/verificar/{folio}?huella=…`, a la que lleva el QR del PDF): (1) `GET /public/v1/verificar/{folio}?huella=…`; (2) canonicalizar `contenidoCanonico` con RFC 8785 (JCS) y calcular su SHA-256 ⇒ debe ser `hashContenido` (y empezar por la huella impresa); (3) tomar de `/.well-known/validasoft-certificados.json` la clave con `kid = firma.kid` e importar su `jwk` (WebCrypto `Ed25519`); (4) verificar `firma.valor` (base64) sobre los **32 bytes** del SHA-256 (no sobre el texto hexadecimal). Con contenido de solo texto y enteros, `JSON.stringify` con las claves ordenadas recursivamente da el mismo resultado que JCS.

**Cuándo se emite y qué lo cambia** (eventos del stream `Certificado-{id}`, uno por vínculo; los webhooks `certificado.*` de §7 los publicará la fase de notificaciones):

| Hecho | Evento del certificado |
|---|---|
| El expediente se aprueba y el vínculo queda `Activo` (cualquier camino: veredicto, decisión de área, aprobación del administrador, exención, área automática, aceptación con todo automático, sincronización de requisitos) | `CertificadoEmitido` (emisión `-1`, misma transacción). Un expediente aprobado nuevo de un vínculo que ya tenía certificado ⇒ `CertificadoReemplazado` + `CertificadoEmitido(-{n+1})` (aún no ocurre: solo hay expedientes iniciales). |
| `POST /vinculos/{id}/bloquear` | `CertificadoSuspendido` (si estaba vigente) |
| `POST /vinculos/{id}/desbloquear` | `CertificadoRestablecido` (o `CertificadoVencido` si su vigencia pasó durante el bloqueo) |
| `POST /vinculos/{id}/anular` · `/desvincular` | `CertificadoRevocado { origen: Sistema, motivo: "Vínculo anulado: …" \| "Vínculo desvinculado: …" }` |
| `POST /certificados/{folio}/revocar` | `CertificadoRevocado { origen: Administrador }` |
| Pasa `vigenteHasta` (procesador de certificados) | `CertificadoVencido` |
| PDF generado (después del commit) | `PdfDeCertificadoGenerado { emision, bucket, clave, sha256, tamano }` |
| Descarga del PDF (empresa o pública) | `CertificadoDescargado` |

**Diferencias con §2.6 y §4** (pendiente o cambiado): no existen todavía `GET /certificados` (lista), `GET /certificados/{folio}/emisiones` (el historial va en `GET /vinculos/{id}/certificado`) ni `GET /certificados/{folio}/pdf` (es `POST …/descarga`, porque escribe la auditoría, como BO-7); revocar pide `expedientes:administrar` (no se creó `certificados:revocar`); `VinculoDto.certificado`, `VinculoPublicoDto.certificado`, `GET /public/v1/certificados/{folio}` (vista del tercero) y `GET /consultas/certificacion` (`ConsultaCertificacionDto`) siguen pendientes; `ConPendientes`, `pendientes` y `verificaciones` van vacíos hasta H6/H7; `contenidoCanonico` agrega `nombre` en requisitos, `codigo` en áreas y `nombre` en flujo y tipo de vínculo, y no separa `hashDatos` (en una versión solo de formulario `sha256` ya es la huella de sus datos, TD-4); `urlVerificacion` lleva la emisión y `?huella=` (CE-8); el PDF no es PDF/A-2b (CE-6).

#### 2.6.2 Estado de implementación — integración con SRM y Marco (H9, 2026-11-10)

Decisiones CN-1…CN-7 del [registro](decisiones/registro.md); guía para el integrador en [08 §2.1](08-integracion-y-migracion.md).

| Ruta | Permiso | Contrato |
|---|---|---|
| `GET /api/v1/consultas/certificacion?pais=CO&tipoIdentificacion=&numero=&tipoVinculo=` o `?vinculoId=` | `consultas:leer` (nuevo; en `Permisos.DelTenant` y en las lecturas de todos los roles) | `X-Empresa` obligatoria (422 `empresa-requerida`). El número se normaliza (puntos, guiones y DV se aceptan; 422 `numero-identificacion-invalido`); `pais` por omisión `CO`; `tipoVinculo` = código (sin él sirve si el tercero tiene un solo vínculo con la empresa; si tiene varios, 422 `tipo-vinculo-requerido`); sin identificación ni `vinculoId`, 422 `consulta-incompleta`; `vinculoId` de otra empresa, 404 `vinculo-no-encontrado`. Tercero desconocido o sin vínculo con la empresa ⇒ 200 `SinVinculo` (no revela si existe). `ConsultaCertificacionDto` (§4) con `estado` de la emisión actual, `vigenteHasta`, `revalidacionPendiente[]` (requisitos sin aprobar del expediente de actualización abierto, `plazo` = apertura + 15 días, `bloqueante` = pendientes vencidos), `documentosVencidos[]` (códigos del contenido del certificado con vigencia anterior a hoy), `bloqueado` + `motivoBloqueo` (CN-2), `cuentaDesignada` (solo la aprobada), `legacy: null` (pendiente). `ETag: "c-{32 hex}"` = SHA-256 del cuerpo; `If-None-Match` igual (o `*`) ⇒ 304 sin cuerpo; `Cache-Control: private, max-age=300`, `Vary: X-Empresa, X-Api-Key, Authorization`. |
| `POST /api/v1/api-keys/{id}/rotar` | `api-keys:administrar` | `RotarApiKeyDto { anteriorValidaHasta? }` ⇒ **201** `ApiKeyDto` de la **nueva** con `valor` (una sola vez) y `rotadaDe`; copia nombre, scopes, empresas, sistema, sandbox y expiración. La anterior queda con `reemplazadaPor` y `expiraEn = anteriorValidaHasta` (nulo = 24 h; ahora = corte inmediato; nunca más allá de su expiración). 404 `api-key-no-encontrada` (o revocada), 409 `api-key-ya-rotada`, 409 `api-key-vencida`, 422 `solapamiento-invalido` (pasada o más de 30 días), 403 `alcance-insuficiente`. Auditoría: `Rotado` (la anterior) + `Creado` (la nueva). La respuesta **no** se guarda para idempotencia (como los secretos de webhook): repetirla responde 409. |
| `GET /api/v1/eventos?empresaId=` | `eventos:leer` | Nuevo filtro `empresaId` (equivale a `X-Empresa`; útil para una llave de varias empresas). Fuera del alcance de la credencial, o distinto de `X-Empresa`, 403 `alcance-insuficiente`. |

**Caché de API keys** (CN-3): la verificación Argon2 se guarda 5 minutos, pero vence con el reloj de Validasoft (también al llegar el `expiraEn` de la llave) y se **olvida en el nodo** al rotar o revocar; en otros nodos, como mucho 5 minutos después.

**Diferencias con §2.6 y §4**: sin proyección `EstadoCertificacionTercero` (consulta indexada, CN-1); `motivoBloqueo` agrega `Revocado` (CN-2); `Cache-Control` es `private`; `legacy` va nulo; la rotación no tiene `ipsPermitidas` ni `ultimoUsoEn` (no existen aún en la llave) y no deshabilita webhooks por `empresas-fuera-de-alcance` (la rotación copia el alcance, así que no lo reduce). El modo sandbox con datos aislados sigue pendiente.

### 2.7 Vencimientos, notificaciones y auditoría

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `GET /vencimientos?desde&hasta&estado&tipo&empresaId` | `VencimientoDto[]` | `vencimientos:leer` | 200 | — |
| `POST /vencimientos/notificar` | Recordatorio consolidado (`NotificarVencimientosDto`) | `vencimientos:escribir` | 202 | — |
| `GET /notificaciones` · `GET /notificaciones/{id}/entregas` | Envíos y evidencia de entrega (`EntregaNotificacionDto`) | `auditoria:leer` | 200 | — |
| `GET /novedades` · `POST /novedades/{id}/leer` | Campana | sesión | 200 | — |
| `GET /auditoria?tipo=dominio\|configuracion\|identidad\|acceso&entidad=&entidadId=&actor=&desde=&hasta=` | Registro unificado (`PaginaDto<RegistroAuditoriaDto>`): eventos de dominio (timelines), **cambios de configuración** (`auditoria_configuracion`, con `antes`/`despues`/`diff`), **identidad** (login, 2FA, bloqueos, uso de API key, token exchange ERP, magic link canjeado) y **accesos a documentos** (`DocumentoAccedido`); siempre filtrado por tenant y, si el rol lo exige, por empresa | `auditoria:leer` | 200 | — |
| `GET /auditoria/excel?tipo=&entidad=&entidadId=&actor=&desde=&hasta=` (**implementado**, IF-4) | La auditoría con los filtros de la pantalla en Excel (hasta 50 000 registros; 422 `exportacion-demasiado-grande`); la exportación queda en Accesos (IF-6) | `auditoria:leer` + `auditoria:exportar` | 200 | — |
| `POST /auditoria/exportar` · `GET /auditoria/exportaciones/{id}` | Exportación ZIP firmado (JSONL por tipo + índice + hash) de un rango (`ExportarAuditoriaDto` → `ExportacionDto`) | `auditoria:exportar` | 202/200 | `exportacion.lista` |
| `GET /kpis?flujoId=&empresaId=&periodo=` | `KpisDto` (proyecciones `KpisTenant`/`KpisFlujo`/`KpisEmpresa`; incluye `decisionesAutomaticas` y `equidad`) | `expedientes:leer` | 200 | — |
| `GET /vencimientos/proximos?dias=30` | `VencimientosProximos` (vista de Inicio; mismo `VencimientoDto`) | `vencimientos:leer` | 200 | — |
| `GET /usuarios/me/preferencias-notificacion` · `PUT /usuarios/me/preferencias-notificacion` | `PreferenciasNotificacionDto` (por evento y canal, digest) | sesión | 200 | — |
| `GET /informes/estado-expedientes?desde=&hasta=&fecha=Apertura\|Cierre&estado=&tipo=&areaId=&flujoId=&tipoVinculoId=&terceroId=&limite=&cursor=` (**implementado**, IF-1/IF-2) | `InformeEstadoExpedientesDto { resumen: ResumenEstadoExpedientesDto, datos: FilaEstadoExpedienteDto[], siguienteCursor }`: por expediente, días totales, en carga, con el tercero y en revisión, y por área (`TiempoDeAreaDto`: días, rondas, SLA); el resumen es de todas las filas. Hasta 5 000 expedientes (422 `informe-demasiado-grande`) | `expedientes:leer` | 200; 422 `estado-invalido`, `tipo-invalido`, `fecha-invalida`, `rango-invalido` | — |
| `GET /informes/estado-expedientes/excel` · `GET /informes/estado-expedientes/pdf` (**implementados**, IF-3; mismos filtros) | Libro con Expedientes, Tiempos por área y Resumen; PDF horizontal con el estilo del certificado. El `POST …/exportar` asíncrono queda para volúmenes mayores | `expedientes:leer` | 200 | — |
| `GET /informes/vinculos?estado=&tipoVinculoId=&terceroId=&limite=&cursor=` · `GET /informes/vinculos/excel` (**implementados**, IF-1) | `InformeVinculosDto`: por vínculo, estado, certificado (`CertificadoDelInformeDto`), expediente en curso, vigencia de documentos (`DocumentosDelInformeDto`, nula sin `documentos:leer`) y secciones de la ficha por validar | `vinculos:leer` | 200 | — |
| `GET /expedientes/{id}/exportar` (**implementado**, IF-5) | ZIP del expediente: documentos aprobados (versión vigente de cada requisito aprobado), `resumen.pdf`, `linea-de-tiempo.json` e `indice.json` con el SHA-256 de cada archivo; cada documento queda como `DocumentoAccedido` y la exportación en Accesos (IF-6) | `expedientes:exportar` + `documentos:descargar` | 200; 404 `expediente-no-encontrado` | — |
| `GET /informes/cumplimiento?desde=&hasta=&riesgo=` · `POST /informes/cumplimiento/exportar` | `InformeCumplimiento` (`InformeCumplimientoDto`); exportación | `auditoria:leer` o rol `Cumplimiento` | 200/202 | `exportacion.lista` |
| `GET /exportaciones/{id}` | Estado y URL prefirmada de una exportación (`ExportacionDto`) | quien la pidió | 200 | — |
| `GET /solicitudes-supresion?estado=` · `GET /solicitudes-supresion/{id}` · `POST /solicitudes-supresion/{id}/resolver` | Lado empresa del habeas data (adición: la saga `SupresionDatos` escala a humano las retenciones dudosas): `SolicitudSupresionDto`, `ResolverSupresionDto { decision*: Suprimir\|Retener, baseLegal, hasta }` | `supresiones:resolver` | 200; 409 `solicitud-ya-resuelta` | `habeas.solicitud_resuelta` |

#### 2.7.1 Estado de implementación — verificaciones externas y cumplimiento (H7, 2026-11-10)

Decisiones VE-5…VE-18 del [registro](decisiones/registro.md). Todo con `X-Empresa`; las decisiones las toma una persona (`usuario-delegado-requerido`). Las acciones sobre el expediente devuelven el `ExpedienteDto` (ahora con `verificaciones[]: VerificacionDto` y `esperandoVerificaciones`).

| Método y ruta | Permiso | Contrato | Respuestas |
|---|---|---|---|
| `POST /expedientes/{id}/verificaciones/{tipo}/consultar` | `verificaciones:escribir` | Consulta (o reconsulta) ahora las listas del tercero y de su representante legal, suplentes, socios y PEP de la ficha, con el proveedor de la empresa | 200; 409 `estado-invalido`; 422 `tipo-no-consultable`, `cuota-listas-agotada`, `credenciales-requeridas`; 503 `verificacion-no-disponible` |
| `POST /expedientes/{id}/verificaciones/{tipo}/coincidencias/{cid}/resolver` | `cumplimiento:decidir` | `ResolverCoincidenciaDto { decision: Descartada\|Confirmada, justificacion }` | 200; 403 `conflicto-de-intereses`; 404 `coincidencia-no-encontrada`; 409 `coincidencia-ya-resuelta`; 422 `justificacion-requerida` |
| `POST /expedientes/{id}/verificaciones/estudio-cumplimiento` | `cumplimiento:decidir` | `RegistrarEstudioCumplimientoDto { nivelRiesgo 1–3, conflictoDeInteres, debidaDiligencia: Simple\|Ampliada, concepto: Favorable\|Desfavorable, observaciones?, evidencias?: {documentoId, version}[] }` | 200; 422 `nivel-riesgo-invalido`, `debida-diligencia-ampliada-requerida`, `observaciones-requeridas`, `evidencia-invalida` |
| `POST /expedientes/{id}/verificaciones/{tipo}/evidencia/descarga?referencia=` | `documentos:descargar` | `DescargaEvidenciaDto { url, expira, nombreArchivo }` (5 min; queda `EvidenciaDeVerificacionDescargada`) | 200; 404 `evidencia-no-encontrada` |
| `GET /cumplimiento/bandeja` | `expedientes:leer` | `FilaCumplimientoDto[] { origen: Expediente\|Monitoreo, expedienteId?, folio, vinculoId, tercero, pendientes[] {tipo, estado, bloqueante}, coincidenciasPorAnalizar, esperandoSoloVerificaciones, desde }` | 200 |
| `GET /vinculos/{id}/monitoreo-listas` · `POST …/coincidencias/{cid}/resolver` · `POST …/evidencia/descarga` | `expedientes:leer` · `cumplimiento:decidir` · `documentos:descargar` | `MonitoreoListasDto { vinculoId, expedienteId, folio, frecuenciaDias, proximaConsulta, listas: VerificacionDto }` | 200; 404 `monitoreo-no-encontrado` |
| `GET /configuracion/listas-restrictivas?mes=` · `PUT /configuracion/listas-restrictivas` | `configuracion:leer` · `configuracion:escribir` | `ListasRestrictivasConfigDto { proveedor, proveedorEfectivo, proveedores[] ProveedorListasDto {nombre, real, principal, habilitadoEnInstalacion, configurado, usuario, ultimosCuatroClave, urlBase, plan, cuotaMensual, modoBusqueda}, uso[] UsoListasDto, diasMonitoreo, vigenciaEstudioDias }`; `ActualizarListasRestrictivasDto { proveedor? ("" = el de la instalación), credenciales?: {proveedor, usuario?, clave? (null conserva, "" quita), urlBase?, plan?, cuotaMensual?, modoBusqueda?} }` | 200; 422 `proveedor-invalido`, `proveedor-no-habilitado`, `credenciales-requeridas`, `usuario-requerido`, `clave-requerida`, `clave-invalida`, `url-invalida`, `cuota-invalida`, `mes-invalido` |
| `GET /informes/cumplimiento?desde=&hasta=` · `GET /informes/cumplimiento/excel?desde=&hasta=` | `auditoria:leer` | `InformeCumplimientoDto { desde, hasta, verificaciones[] {tipo, proveedor, total, conCoincidencias, confirmadas, pendientes}, riesgo[] {nivel, cantidad}, estudiosVencidos, pendientesReconsulta, filas[] }`; el Excel, una fila por verificación | 200; 422 `periodo-invalido` |

`VerificacionDto` (implementado): `tipo`, `estado`, `bloqueante`, `requeridaPorElFlujo`, `proveedor`, `resultado`, `referenciaExterna`, `registradaEn`, `registradaPor` (usuario o `sistema`), `vigenteHasta`, `evidencia {nombreArchivo, tipoContenido, tamano, sha256, delProveedor, sha256Respuesta}`, `personas[] {rol, nombre, identificacion}`, `coincidencias[] {id, persona, rol, identificacion, lista, nombreEncontrado, detalle, puntaje, decision, justificacion, resueltaPor, resueltaEn, vigenteHasta, heredada}`, `estudio: EstudioCumplimientoDto`, `anteriores[]`, `ultimoError`, `reintentarDesde`, `vigenciaDias`, `frecuenciaReconsultaMeses`. **Diferencias**: el informe se exporta con un `GET` síncrono (no `POST …/exportar` 202) y la debida diligencia es `Simple|Ampliada` (VE-9, VE-14).

#### 2.7.2 Estado de implementación — campana del tercero y recordatorio de observaciones (NT, 2026-11-10)

Decisiones NT-1…NT-11 del [registro](decisiones/registro.md). Lado tercero (sesión de tercero, `Authorization: Bearer`, solo lo de los vínculos de su sesión):

| Método y ruta | Contrato | Respuestas |
|---|---|---|
| `GET /public/v1/notificaciones?soloNoLeidas=&limite=&cursor=` | `BandejaNotificacionesTerceroDto { datos: NotificacionTerceroDto[], siguienteCursor, noLeidas }` (más recientes primero; `noLeidas` es el total de **todas** sus empresas). `NotificacionTerceroDto { id, tipo, titulo, mensaje, empresa?: EmpresaPublicaDto (nombre, logo, color), expedienteId?, vinculoId?, folio?, enlace? (ruta de `apps/public`), creadaEn, leidaEn? }` | 200 |
| `POST /public/v1/notificaciones/{id}/leida` · `POST /public/v1/notificaciones/leidas` | `NotificacionTerceroDto` · `NotificacionesMarcadasDto { marcadas }` | 200; 404 `notificacion-no-encontrada` (de otro tercero o de un vínculo que no es de su sesión) |
| `GET /public/v1/preferencias-notificacion` · `PUT /public/v1/preferencias-notificacion` | `PreferenciasNotificacionDto { correo: {tipo: bool}, tiposConCorreo[] }` · `ActualizarPreferenciasNotificacionDto { correo }` (solo los tipos que cambian) | 200; 422 `tipo-notificacion-invalido` |
| `GET /public/v1/notificaciones/stream` | SSE con la sesión del tercero (`Authorization: Bearer`, sin `X-Empresa`): `event: notificacion` (`data` = `NotificacionTerceroDto`, `id` = su orden), `event: no_leidas` (el total de todas sus empresas) y `event: cambio` (`CambioEnVivoDto`, §3.4) de sus vínculos. Se cierra al vencer el token (NT-13) | 200 `text/event-stream`; 401 sin sesión |

Tipos (`NotificacionTerceroDto.tipo`; con correo: ✉): `tercero.observado` ✉, `tercero.recordatorio` ✉, `tercero.resultado` ✉, `tercero.documento_por_vencer` ✉, `tercero.documento_vencido` ✉, `tercero.certificado` ✉, `tercero.plazo_de_cargue` ✉, `tercero.desaprobado` ✉, `tercero.reintegrado` ✉, `tercero.actualizacion`, `tercero.propuesta`, `tercero.invitacion`, `tercero.cuenta` (la empresa aprobó o rechazó la cuenta que designó). La campana nunca se apaga; el correo de cada ✉ se respeta al enviarlo (`PreferenciasDelTercero`, documento global). Cada aviso es de la empresa que lo origina (vive en su tenant); `tercero.propuesta` no es de ninguna (sin `empresa` ni `vinculoId`). Los disparadores están en el registro (NT-3): salen de los eventos de dominio por la misma suscripción que alimenta la campana de las personas.

Lado empresa (con `X-Empresa`; permiso `vinculos:escribir` salvo la configuración):

| Método y ruta | Contrato | Respuestas |
|---|---|---|
| `POST /expedientes/{id}/recordar-observaciones` | Sin cuerpo. Evento `RecordatorioDeObservacionesEnviado`, correo (plantilla `recordatorio-observaciones`, con un enlace de acceso nuevo) y aviso en la campana del tercero. Devuelve `ExpedienteDto` con `recordatorioObservaciones { ultimoEn, total }` | 200; 409 `expediente-no-observado`; 409 `recordatorio-ya-enviado-hoy` (uno por día y expediente); 404 |
| `POST /expedientes/recordar-observaciones` | `RecordarObservacionesDto { expedientes?: uuid[] (≤ 200), todos?: bool (≤ 500), area?: uuid (solo los devueltos por los que pasa esa área) }` → `RecordatoriosEnviadosDto { enviados, omitidos, detalle[] ResultadoRecordatorioDto { expedienteId, folio?, resultado: Enviado \| SoloCampana \| NoObservado \| YaRecordadoHoy \| NoEncontrado } }`. Lo que no aplica se omite sin error | 200; 422 `lote-vacio`, `lote-demasiado-grande` |
| `GET /configuracion/recordatorio-observaciones` · `PUT /configuracion/recordatorio-observaciones` | `RecordatorioAutomaticoDto { activo, cadaDias (1–60), actualizadoEn? }` · `ActualizarRecordatorioAutomaticoDto { activo, cadaDias }`; auditado (`RecordatorioDeObservaciones`) | 200; 422 `dias-invalidos`; `configuracion:leer` / `configuracion:escribir` |
| `POST /plataforma/recordatorios-observaciones/revisar` | `RevisarRecordatoriosDto { tenantId?, fecha? }` (fecha simulada solo en Development) → `ResultadoRevisionRecordatoriosDto { revisados, enviados }` | 200; 422 `fecha-simulada-no-permitida`; `plataforma:tenants` |

Plantillas: el catálogo suma `recordatorio-observaciones`, `desaprobacion` y `reintegro` (variables `{{empresa}}`, `{{folio}}`; NT-18). La empresa devolvió a revisión una validación aprobada (`expediente.desaprobado` con causa `ExpedienteAprobado`) o reintegró el vínculo (`vinculo.reintegrado`): el tercero lo ve en su campana (`tercero.desaprobado`, `tercero.reintegrado`, con enlace a lo que sigue) y por correo con esa plantilla, respetando su preferencia, nunca con la justificación interna. Reabrir una etapa o volver a la fase inicial de un expediente abierto no le avisa. `GET /expedientes/{id}` suma `recordatorioObservaciones`. «El tercero respondió» (`expediente.tercero_respondio`) llega ahora también a las áreas que revisan los documentos reabiertos (NT-4). La campana de las personas suma `vinculo.cuenta_propuesta` (§6.5; NT-12).

### 2.8 Webhooks y eventos

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `GET /webhooks` · `POST` · `PUT /{id}` · `DELETE /{id}` | `WebhookDto` (`url`, `eventos[]`, `empresas[]`, `version`, `secreto` solo al crear/rotar). **Precedencia de alcance**: el alcance efectivo es `apiKey.empresas[] ∩ webhook.empresas[]` (con `*` = todas las del tenant); si la intersección queda vacía al crear o editar ⇒ `422 empresas-fuera-de-alcance`; si una rotación o edición posterior de la API key deja la intersección vacía, la suscripción se deshabilita y se emite `webhook.deshabilitado { causa: "empresas-fuera-de-alcance" }` | `webhooks:administrar` | 200/201; 422 `empresas-fuera-de-alcance` | `webhook.deshabilitado` |
| `POST /webhooks/{id}/rotar-secreto` · `POST /webhooks/{id}/probar` | Rotación 24 h; `prueba.ping` | `webhooks:administrar` | 200 | `prueba.ping` |
| `GET /webhooks/{id}/entregas` · `GET …/entregas/{eid}` · `POST …/entregas/{eid}/reenviar` | `EntregaWebhookDto` | `webhooks:administrar` | 200 | — |
| `GET /eventos?desde={cursor}&tipos=&empresaId=` | **Modo pull** (`PaginaDto<EventoPublicoDto>`), orden por secuencia del outbox del tenant | `eventos:leer` | 200 | — |

> **Implementado (NW, 2026-11-08)**: contrato exacto y diferencias con esta tabla y con §2.7 (campana en `/notificaciones`, no `/novedades`) en **§6.5**.

### 2.9 Público (tercero) e importación

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `POST /public/v1/enlaces/{token}/canjear` · `POST /public/v1/enlaces/{token}/otp` | Magic link y OTP (`SesionTerceroDto`); solo abre sesión, **no comparte nada**. Con OTP (tipo de vínculo con `exigeOtpEnEnlace`) el canje responde 202 `{ requiereOtp, expira }` y envía el código al mismo correo; `otp` recibe `{ codigo }` | público (rate limit por IP) | 200; 202 (OTP enviado); 404 `enlace-no-encontrado`; 410 `enlace-usado`, `enlace-vencido`, `otp-vencido`; 422 `otp-invalido` (con `intentosRestantes`); 429 `otp-bloqueado` | — |
| `POST /public/v1/invitaciones/{expedienteId}/aceptar` | El tercero **acepta la invitación** de una empresa (`AceptarInvitacionDto`: aviso de plataforma —solo la primera vez—, autorización de datos y T&C de esa empresa, con evidencia). En ese momento se registran los consentimientos y se **comparte** con esa empresa la versión actual de cada tipo global que exige (B-1); hasta entonces la empresa solo ve `requisitosCubribles`. **Implementado hoy como `POST /public/v1/invitaciones/{vinculoId}/aceptar`** (§2.9.1) | sesión tercero | 200; 403 `otp-requerido`; 409 `invitacion-ya-aceptada`, `textos-legales-faltantes`, `texto-desactualizado`; 422 `consentimiento-faltante`, `consentimiento-rechazado` | `expediente.invitacion_aceptada`, `documento.compartido` (uno por documento) |
| `GET /public/v1/tercero/relaciones` · `POST /public/v1/tercero/relaciones` · `POST /public/v1/tercero/relaciones/{rid}/confirmar` · `DELETE /public/v1/tercero/relaciones/{rid}` | El tercero declara o confirma sus **personas relacionadas** (B-3): representante legal principal y suplentes, socios/beneficiarios finales, miembros del consorcio; cada una recibe su propio enlace para subir cédula y antecedentes a **su** bóveda | sesión tercero | 200/201; 409 `relacion-duplicada` | `tercero.relacion_cambiada` |
| `POST /public/v1/autorregistro` | Autorregistro invisible (`AutorregistroDto`): responde **siempre 202 con el mismo cuerpo**, exista o no el tercero; si no existe lo crea sin tenant con el aviso de la plataforma aceptado; envía un enlace de acceso sin vínculo solo si el correo es de un contacto del tercero | público (rate limit) | 202; 422 `consentimiento-requerido` y validaciones de datos | `tercero.autorregistrado` |
| `POST /public/v1/acceso` | **Volver a entrar con el correo** (`SolicitarAccesoDto { correo }`): responde **siempre 202 con el mismo cuerpo**; si hay vínculos no terminales con ese correo envía un enlace de acceso por tercero (máx. 3 por hora por correo). Ver §2.9.3 | público (rate limit por IP y por correo) | 202; 422 `correo-invalido` | — |
| `POST /public/v1/sesion/renovar` · `POST /public/v1/sesion/cerrar` | Renueva la sesión del tercero con la cookie `HttpOnly` `vs_tercero` (rota el token de refresco y devuelve un `SesionTerceroDto` nuevo) o la cierra. Exigen `X-Validasoft-Cliente: public`. Ver §2.9.3 | cookie de refresco (público para Bearer; rate limit por IP) | 200 / 204; 401 `sesion-expirada`, `sesion-revocada`; 403 `cliente-invalido`; 409 `renovacion-concurrente` | — |
| `GET /public/v1/boveda` · `GET /public/v1/boveda/documentos/{id}` · `POST /public/v1/boveda/exportar` · `GET /public/v1/exportaciones/{id}` | `BovedaTercero` con estado por empresa; exportación ZIP firmado y su estado (`ExportacionDto`) | sesión tercero | 200/202 | `exportacion.lista` |
| `POST /public/v1/boveda/documentos` | **Carga directa a la bóveda sin expediente** por el tercero (paso 1; `IniciarCargaDto` con `tipoCanonico*`); paso 2 con `POST /documentos/{docId}/versiones/{n}/confirmar`. Si la versión queda `Actual` se comparte con todos los vínculos que exigen el tipo (sin exclusión, A-8) | sesión tercero | 201; 200 `duplicado`; 413; 415 | tras confirmar: `documento.hechos_registrados`, `documento.version_nueva` o `documento.version_devuelta` |
| `GET /public/v1/boveda/stream` | **SSE de la bóveda** (antes de que exista expediente y durante toda la vida del tercero): eventos `documento.hechos_registrados`, `documento.version_nueva`, `documento.version_devuelta`, `vinculo.revalidado`, `expediente.requisito_veredicto`, `documento.por_vencer`, `vinculo.cuenta_designada`, `exportacion.lista`; `Last-Event-ID` como cursor, reenvío 24 h | sesión tercero | 200 (`text/event-stream`) | — |
| `GET /public/v1/boveda/cuentas-bancarias` · `POST /public/v1/boveda/cuentas-bancarias` · `POST /public/v1/vinculos/{id}/cuenta-designada` | Listar (`CuentasTercero`), registrar cuenta (`CuentaBancariaDto` + certificación en dos pasos); designar cuenta para una empresa (`DesignarCuentaDto`) | sesión tercero | 200/201/202 | `vinculo.cuenta_designada_propuesta` |
| `POST /public/v1/tercero/datos-maestros` | Propone cambio de datos maestros → versión de formulario `DATOS_MAESTROS` (IA valida si está disponible) | sesión tercero | 202 | `documento.version_nueva` |
| `GET /public/v1/expedientes/{id}` · `GET /public/v1/vinculos` · `GET /public/v1/certificados/{folio}` · `GET /public/v1/frescura` | Vistas del tercero (`ExpedientePublicoDto`, `VinculoPublicoDto`, `CertificadoDto`, `FrescuraDto`) | sesión tercero | 200 | — |
| `POST /public/v1/expedientes/{id}/requisitos/{rid}/revision-humana` | Derecho a revisión humana | sesión tercero | 202 | `expediente.revision_humana_solicitada` |
| `POST /public/v1/solicitudes-supresion` · `GET /public/v1/solicitudes-supresion/{id}` | Habeas data (`SolicitudSupresionDto`) | sesión tercero | 202/200 | `habeas.solicitud_recibida`, `habeas.solicitud_resuelta` |
| `POST /sesiones-tercero` | Token corto para embeber (`SesionEmbedDto`) | `sesiones-tercero:crear` | 201 | — |
| `POST /importacion/{entidad}` (`tipos-documento`, `terceros`, `relaciones`, `cuentas`, `vinculos`, `documentos`, `expedientes`, `vencimientos`, `certificados`) · `GET /importacion/lotes/{id}` · `GET /importacion/lotes/{id}/rechazos` | Upserts idempotentes por `claveLegado` (lotes ≤ 500). **Cuerpo** = `LoteImportacionDto`; **respuesta** = `ImportacionLoteDto`. El tenant sale de la credencial y la empresa de `X-Empresa` (nunca en el cuerpo). Rechazos por fila (`RechazoImportacionDto[]`, descargable como CSV con `Accept: text/csv`) | `importacion:escribir` | 202/200 | `importacion.lote_terminado` |
| `POST /importacion/binarios` | **Única semántica**: registro de binarios que el importador ya copió a MinIO con `mc mirror` (`BinarioImportadoDto`); no devuelve URL prefirmada ni sube bytes. La fila de `documentos` que lo referencia usa el mismo `sha256` | `importacion:escribir` | 202; 409 `hash-ya-registrado` | — |

#### 2.9.1 Estado de implementación — fase «Validación por el interesado» (2026-11-06)

Implementado (sin expedientes ni documentos todavía; decisiones IN-1…IN-12 del [registro](decisiones/registro.md)):

| Ruta | Notas y diferencias con la tabla anterior |
|---|---|
| `POST /public/v1/enlaces/{token}/canjear` · `/otp` | Como §2.9. El enlace nace con cada `InvitacionEnviada` (al crear el vínculo y en `POST /vinculos/{id}/invitaciones`) con la vigencia de la invitación; reenviar borra los enlaces no usados del vínculo (el anterior responde 404). Los 5 intentos de OTP son por enlace y no se reinician al volver a canjear. |
| `GET /public/v1/vinculos` | `VinculoPublicoDto` reducido: `id`, `empresa {id, nombre, logoUrl}`, `tipoVinculo {id, codigo, nombre}`, `estado`, `invitacionAceptada`, `invitacionAceptadaEn` (sin `certificado`, `pendientes` ni `cuentaDesignada` hasta H4/H6). |
| `GET /public/v1/invitaciones/{vinculoId}` | **Nueva.** `InvitacionPublicaDto`: el vínculo, `textosPorAceptar[] {tipo, textoVersion, hashTexto, contenido, publicadoEn}` (el aviso de la plataforma solo si el tercero no aceptó su versión vigente), `textosFaltantes[]` y `puedeAceptar`. |
| `POST /public/v1/invitaciones/{vinculoId}/aceptar` | **Por `vinculoId`** mientras no existan expedientes (pasará a `{expedienteId}` con H4). Registra `ConsentimientosRegistrados` (tipo, versión, id y hash de cada texto; evidencia: IP, user-agent, `otpVerificado`, canal `Web`\|`Embed` según la cabecera `X-Canal`, correo) y luego `InvitacionAceptada`, con actor `Tercero`. Respuesta `{ aceptacion: AceptacionDto[], vinculo: VinculoPublicoDto }`, **sin `compartidos` ni `requisitosCubiertos`** (no hay documentos aún); `AceptacionDto` lleva además `tipo` y `textoLegalId`, y `documentoVersionId` queda nulo. `otpCodigo` está reservado: el OTP se verifica al canjear y un vínculo cuyo tipo exige OTP no se acepta con una sesión sin OTP (403 `otp-requerido`). |
| `POST /public/v1/autorregistro` | `AutorregistroDto` sin cargas (`rut`, `cedula`, `camara` llegan con documentos). |

- **Sesión del tercero** (`SesionTerceroDto`: `accessToken`, `expira`, `terceroId`, `otpVerificado`, `vinculos[] {id, empresa, estado}`): JWT HS256 de 30 min, audiencia `validasoft:tercero`. Va en el cuerpo y se envía como `Authorization: Bearer`; desde §2.9.3 el canje pone además la cookie de refresco `vs_tercero` para renovarla. Las rutas `/public/v1/*` **solo** aceptan este token (ni API keys ni sesiones de usuario) y `/api/v1` no lo acepta (401 en ambos casos). No requieren `Idempotency-Key`.
- **Correo**: invitación (`{UrlPublica}/e/{token}`, empresa y tipo de vínculo), código OTP y enlace de autorregistro salen por el outbox de Wolverine **después** de confirmar la transacción (Mailpit en desarrollo).
- **Límite de peticiones**: `/public/v1/*` por IP, ventana fija (`Validasoft:LimitePublico:{Peticiones, VentanaSegundos}`, 60/min por defecto); excedido ⇒ 429 `demasiadas-peticiones` con `Retry-After`. `/api/v1` aún sin límite.
- **Historial** (`GET /vinculos/{id}/historial`): `datos` usa las convenciones de la API (camelCase, enums como texto). `POST /vinculos/{id}/invitaciones` devuelve el número real de envíos.
- **Aceptar comparte** (desde la fase «Bóveda», §2.9.2): la respuesta de `POST /public/v1/invitaciones/{vinculoId}/aceptar` es `{ aceptacion, vinculo, compartidos: CompartidoDto[] }`; ya no aplica la nota de IN-7 sobre `compartidos`.

#### 2.9.3 Estado de implementación — fase «Reingreso» (H2-reingreso, 2026-11-06)

El tercero puede volver a entrar sin depender de la empresa (decisiones RE-1…RE-7 del [registro](decisiones/registro.md); seguridad en [07 §2.2](07-seguridad-identidad-y-tenancy.md)).

| Ruta | Cuerpo y respuestas |
|---|---|
| `POST /public/v1/enlaces/{token}/canjear` (sin OTP) · `POST /public/v1/enlaces/{token}/otp` | Igual que §2.9.1 y, al abrir la sesión, cabecera `Set-Cookie: vs_tercero={token de refresco}; Path=/public/v1/sesion; Expires=…; Secure; SameSite=Lax; HttpOnly`. El token de refresco es opaco (`{familia}.{256 bits base64url}`) y **nunca** va en el cuerpo. La cookie vence a las 8 h del canje (absoluto). `Secure` se controla con `Validasoft:SesionTercero:CookieSegura` (`true` por defecto; `false` en `appsettings.Development.json`). |
| `POST /public/v1/sesion/renovar` | Sin cuerpo; cabecera **obligatoria** `X-Validasoft-Cliente: public` (sin ella `403 cliente-invalido`); la cookie la envía el navegador: `apps/public` debe llamar al API desde el mismo origen (proxy de Vite en desarrollo, mismo dominio en producción), porque el API no habilita CORS. `200 SesionTerceroDto` con un access token nuevo de 30 min cuyos `vinculos` se **recalculan** (entran los creados después del canje) y `otpVerificado` conservado; rota la cookie (nuevo `Set-Cookie`). Sin cookie, mal formada, desconocida o vencida ⇒ `401 sesion-expirada`. Cookie ya rotada (reuso) ⇒ se revoca la familia y `401 sesion-revocada`; desde ese momento también la cookie vigente responde `401 sesion-revocada`. Dos renovaciones simultáneas con la misma cookie ⇒ una `200` y la otra `409 renovacion-concurrente` (no revoca: el navegador ya recibió la cookie nueva y puede reintentar **una** vez). |
| `POST /public/v1/sesion/cerrar` | Sin cuerpo; misma cabecera (`403 cliente-invalido`). Revoca la familia (si la cookie es de una) y borra la cookie (`Set-Cookie: vs_tercero=; expires=Thu, 01 Jan 1970…; path=/public/v1/sesion`). `204` siempre, con o sin cookie. |
| `POST /public/v1/acceso` | `SolicitarAccesoDto { correo }` ⇒ **siempre** `202 { "mensaje": "Si tu correo está registrado, te enviamos un enlace para entrar." }`, exista o no. Correo vacío o mal formado ⇒ `422 correo-invalido` (validación de forma, no de existencia). Con vínculos **no terminales** (ni `Anulado` ni `Desvinculado`) de cualquier tenant cuyo `correoTercero` es ese, envía **un** enlace por tercero (`{UrlPublica}/e/{token}`, 1 h, sin vínculo concreto: la sesión que abre incluye todos los vínculos de ese tercero con ese correo); `requiereOtp` si alguno de esos vínculos es de un tipo con `exigeOtpEnEnlace`. Plantilla nueva «Tu enlace para entrar a Validasoft». Como máximo **3 envíos por hora por correo** (además del límite por IP); superado, la respuesta es idéntica y no se envía nada. No requiere `Idempotency-Key`. |

- **Correo de observación**: trae un enlace de acceso nuevo de un solo uso (`{UrlPublica}/e/{token}`, 72 h, del vínculo observado, con OTP si su tipo lo exige) creado en la misma transacción del veredicto; el token viaja cifrado por el outbox (IN-3). Si venció, el correo remite a pedir uno con el correo.
- **Reenviar la invitación** (`POST /api/v1/vinculos/{id}/invitaciones`) no cambia: solo para vínculos `Invitado` (RE-6). Para cualquier otro estado el tercero pide acceso con su correo.
- **Uso esperado desde `apps/public`**: guardar el access token solo en memoria; al cargar la página llamar `renovar` (si responde `401`, mostrar «pedir acceso con tu correo»); renovar antes de que venza el access token; **serializar** las renovaciones entre pestañas (p. ej. Web Locks o `BroadcastChannel`), porque presentar una cookie ya rotada se trata como reuso y cierra la sesión; `cerrar` al salir.

#### 2.9.2 Estado de implementación — fase «Bóveda de documentos» (H2, primera parte, 2026-11-06)

Implementado **sin IA, sin antivirus y sin requisitos en el expediente** (decisiones BO-1…BO-10 del [registro](decisiones/registro.md)). Solo clase `Archivo`: un tipo de clase `Formulario` responde 422 `clase-no-soportada` (desde TD-4, §2.9.4, un tipo del tenant con campos y `PorVinculo` sí se entrega). Los binarios nunca pasan por el API: MinIO/S3 con URLs prefirmadas.

| Ruta | Permiso | Notas y diferencias con §2.2, §2.5 y §2.9 |
|---|---|---|
| `GET /api/v1/catalogos/tipos-documento` | autenticado | Catálogo canónico del Anexo A de [03](03-dominio-y-eventos.md) (`TipoDocumentoCanonicoDto`), en código. Sin `id`: el canónico se identifica por `codigo` en toda la API. Añade `cargaSoportada` (solo `Archivo`). |
| `GET /api/v1/tipos-documento` · `POST` · `GET /{id}` · `PUT /{id}` | `configuracion:leer` / `configuracion:escribir` | `CrearTipoDocumentoDto { codigo, nombre?, tipoCanonico?, ambito?, reglaFrescura?, diasAviso?, diasGracia?, diasRevalidacion?, textoAyuda? }`; `ActualizarTipoDocumentoDto` igual sin `codigo` ni `tipoCanonico` (inmutables) y con `activo`; los nulos conservan el valor. **Ya no hay `exigencias`** (BO-3 reemplazada por RQ-3: lo que se pide a cada vínculo lo dicen los requisitos de su flujo, §2.4.2; un `exigencias` en el cuerpo se ignora). `TipoDocumentoTenantDto` reducido: `tipoCanonico` (código) en lugar de `tipoCanonicoId`; sin `plantillaVersionId`, `nivelRevision`, `politicaDesactualizada`, `camposCuyoCambioExigeHumano`, `porcentajeMuestreo`, `aprobadoresRequeridos`, `retencionAnios` ni `camposPrivados`. Frescura y plazos calculan la vigencia de cada versión desde TD-6 (§2.9.4), que también añade reglas de archivo, modo, campos y plantilla. Sin canónico ⇒ `PorVinculo`; ampliar el ámbito ⇒ 422 `ambito-no-ampliable`; código repetido ⇒ 409 `tipo-documento-duplicado`; 422 `frescura-invalida`, `dias-invalidos`, `ambito-invalido`, `codigo-invalido`. Auditoría `TipoDocumentoTenant`. `POST` exige `Idempotency-Key`. Al crear un tenant se siembran `RUT`, `CAMARA_COMERCIO`, `CEDULA`, `ESTADOS_FINANCIEROS`, `CERTIFICACION_BANCARIA` y `POLIZA` (ningún flujo los pide hasta configurarlo). Aún no existen el override por empresa (`/empresas/{id}/tipos-documento`), `esquema` ni propuestas (las plantillas llegan con TD-5, §2.9.4). |
| `POST /public/v1/boveda/documentos` | sesión tercero | Paso 1. `IniciarCargaDto { nombreArchivo, tamanoBytes, tipoMime, tipoCanonico?, tipoDocumentoTenantId?, vinculoId?, sha256? }`: un canónico global va a la bóveda del tercero; un tipo de la empresa necesita `vinculoId` (de la sesión) y, si es `PorVinculo` (o el canónico lo es, p. ej. `POLIZA`), el documento solo existe para ese vínculo. `sha256` es **opcional** (si llega, detecta el duplicado aquí y se verifica al confirmar). 201 `CargaPrefirmadaDto { documentoId, version, duplicado: false, carga { metodo: PUT, url, cabeceras { Content-Type }, expira } }` (15 min); 200 `duplicado: true` sin `carga`; 413 `archivo-demasiado-grande` (tamaño declarado); 415 `tipo-archivo-no-soportado` (MIME declarado fuera de PDF/JPEG/PNG/WebP); 422 `clase-no-soportada`, `tipo-documento-requerido`, `tipo-documento-invalido`, `vinculo-requerido`, `sha256-invalido`, `tamano-invalido`, `nombre-archivo-requerido`; 404 vínculo fuera de la sesión; 409 `estado-invalido` (vínculo anulado o desvinculado). La URL **no** fija `x-amz-checksum-sha256`. Una sola carga pendiente por documento: iniciar otra la reemplaza (BO-5). |
| `POST /public/v1/boveda/documentos/{documentoId}/versiones/{n}/confirmar` | sesión tercero | Paso 2 (ruta bajo `/public/v1/boveda`, no `/documentos/...`). El servidor lee el objeto: tamaño real > máximo ⇒ 413; tipo real por **magic bytes** no permitido ⇒ 415 (la extensión y el MIME declarado no bastan); SHA-256 distinto del declarado ⇒ 422 `hash-no-coincide`; mismo archivo ya registrado ⇒ 200 `duplicado: true` (evento `VersionDuplicadaDetectada`). PDF con JavaScript, acciones automáticas, lanzadores, envío de formularios, XFA o adjuntos embebidos ⇒ versión `Devuelta` con `motivoDevolucion: pdf-con-contenido-activo` (más de 50 páginas ⇒ `demasiadas-paginas`). **Sincrónico**: 200 `ConfirmacionCargaDto { duplicado, version: VersionDocumentoDto, compartidos[] }` con el estado final (`Actual` o `Devuelta`), no 202 `EnAnalisis`. 404 `carga-no-encontrada`; 409 `archivo-no-cargado`, `version-ya-confirmada`, `carga-desactualizada`. |
| `GET /public/v1/boveda` · `GET /public/v1/boveda/documentos/{id}` | sesión tercero | `BovedaDto { terceroId, documentos[] { id, tipo {codigo, nombre, tipoDocumentoTenantId}, ambito, clase, vinculoId, versionActual, versiones, porEmpresa[] } }` y `DocumentoDto` (todas las versiones). `porEmpresa[] { vinculoId, empresa, compartido, version, versionActualCompartida, compartidaEn }` solo con los vínculos de la sesión (un `PorVinculo`, solo con el suyo). Documento de otro tercero ⇒ 404. Sin estado de aceptación por empresa (llega con requisitos). |
| `POST /public/v1/boveda/documentos/{id}/versiones/{n}/descarga` | sesión tercero | **Nueva.** `DescargaDto` (5 min, `Content-Disposition: attachment`); registra `DocumentoAccedido` con el tercero como actor. |
| `POST /public/v1/boveda/documentos/{id}/versiones/{n}/retirar` | sesión tercero | `RetirarVersionDto { motivo }` → `RetiroDto { documento, binarioEliminado, compartidos }`. Solo `Actual` o `Superada` (409 `estado-invalido`); si era la actual, la superada más reciente vuelve (`VersionActualRestituida`) y se comparte con quien no la tenía. El binario se elimina solo si ninguna empresa la recibió ni la descargó (BO-8). Aún sin 409 `version-referenciada` (no hay aceptaciones ni certificados). |
| `GET /api/v1/vinculos/{id}/documentos` · `GET /api/v1/vinculos/{id}/documentos/{documentoId}` | `documentos:leer` | **Nuevas** (en lugar de `GET /terceros/{id}/documentos` y `GET /documentos/{id}`). `DocumentoVistaEmpresaDto { documentoId, tipo, ambito, versionActual (solo si le fue compartida), versionesCompartidas[] }`: solo versiones compartidas con ese vínculo, que existen únicamente tras la aceptación (B-1). Antes de aceptar la lista es vacía y el detalle 404 (T-4, nunca 403). Sin `aceptacionEnMisVinculos`. |
| `POST /api/v1/vinculos/{id}/documentos/{documentoId}/versiones/{n}/descarga` | `documentos:descargar` | **Nueva** (POST porque registra auditoría; sustituye a `GET /documentos/{id}/versiones/{n}/descarga`). URL prefirmada GET de 5 min y evento `DocumentoAccedido { numero, tenantId, empresaId, vinculoId, actorTipo, actorId, ip, userAgent, motivo }`. Versión no compartida con el vínculo ⇒ 404 `version-no-encontrada` / `documento-no-encontrado`. |

- **Compartición** (B-1, ADR-0009, A-8; fuente desde RQ-3): al **aceptar** se comparte la versión actual de cada tipo global que piden los **requisitos del flujo resuelto** para el vínculo (el suyo publicado o el por defecto) y los `PorVinculo` ya cargados para el vínculo; sin flujo publicado no se comparte ningún global hasta que se abra el expediente (la apertura manual comparte lo que pide). Cuando una versión pasa a `Actual` se comparte con **todos** los vínculos aceptados del tercero cuyo expediente abierto o flujo vigente pide ese tipo (y un `PorVinculo`, solo con su vínculo), y se adjunta a sus requisitos (§2.4.2). Un vínculo bloqueado, desvinculado o anulado no recibe versiones nuevas; lo ya compartido sigue legible salvo en uno anulado. Evento `VersionCompartida` con actor y origen (`AlAceptarInvitacion` \| `Automatica`).
- **Almacenamiento** (docs/02 §10.2): bucket `boveda` → `terceros/{terceroId}/{sha256}`; bucket `tenant-{tenantId}` → `empresas/{empresaId}/vinculos/{vinculoId}/{sha256}`; subida a `cargas/{cargaId}` y copia a la clave definitiva; devueltas en `cuarentena/{documentoId}/v{n}`. Un mismo archivo es un solo objeto.
- **Configuración**: `Validasoft:Almacen { Endpoint, EndpointPublico, Region, AccessKey, SecretKey, CrearBucketsAlIniciar }` y `Validasoft:Boveda { TamanoMaximoBytes (20 MB), MaximoPaginas (50), MinutosSubida (15), MinutosDescarga (5), ExigirAntivirus (false) }`.
- **Scopes nuevos** `documentos:leer` y `documentos:descargar` en todos los roles y en la llave de administración de los tenants nuevos (las llaves ya emitidas no los tienen).
- **Pendiente**: hechos de IA y antivirus (ClamAV), previsualizaciones, formularios, `POST /documentos` (importador e integraciones), `requisitosCubribles`, eventos públicos y webhooks `documento.*`, SSE de la bóveda, `ComparticionCongelada`/`Reanudada`, override por empresa, cifrado SSE-KMS por tenant, Object Lock y purga por retención.

#### 2.9.4 Estado de implementación — fase «Seguridad para embeber» (2026-11-08)

Lo necesario para que el SRM (u otro sistema) embeba las pantallas de `apps/web` desde **otro dominio**. Decisiones SE-1…SE-7 del [registro](decisiones/registro.md); seguridad en [07 §2.6](07-seguridad-identidad-y-tenancy.md).

| Endpoint / cambio | Credencial | Contrato |
|---|---|---|
| `POST /api/v1/sistemas` | `sistemas:administrar` | `CrearSistemaIntegradoDto` gana `origenesPermitidos?: string[]` (opcional). |
| `PUT /api/v1/sistemas/{id}/origenes` | `sistemas:administrar` | **Nuevo.** Cuerpo `FijarOrigenesDto { origenes: string[] }`: **reemplaza** la lista (vacía = nadie embebe). Cada origen se normaliza a `esquema://host[:puerto]` (minúsculas, IDN en punycode, sin puerto por defecto ni barra final) y se descartan repetidos; máximo 20. Solo `https`; `http` únicamente hacia `localhost`/`127.0.0.1`/`[::1]` (cualquier puerto) cuando `Validasoft:Embebido:PermitirHttpLocal` está activo (solo `appsettings.Development.json`). Sin ruta, consulta, fragmento, usuario ni comodines ⇒ si no, `422 origen-invalido`; más de 20 ⇒ `422 origenes-excesivos`; sistema desactivado ⇒ `409 sistema-inactivo`. `200 SistemaIntegradoDto`. Queda en la auditoría de configuración (`Modificado`, antes y después). Sin `Idempotency-Key` (PUT). |
| `SistemaIntegradoDto` | — | Gana `origenesPermitidos: string[]` (siempre presente; `[]` si no tiene). |
| `GET /public/v1/embed/origenes-permitidos` | **público**, sin credencial (rate limit por IP) | **Nuevo.** `200 OrigenesEmbebidoDto { origenes: string[], vigenciaSegundos: 60 }` con `Cache-Control: public, max-age=60`: la **unión** de los orígenes de los sistemas **activos** de todos los tenants, la misma lista de `frame-ancestors`. Sin tenant, sistema, código ni nombre (SE-2). La app embebida la lee al arrancar para decidir a qué contenedor responder por `postMessage`. |
| Cabecera `X-Usuario-Delegado` | API key de sistema | **`jti` obligatorio** (≤ 128 caracteres imprimibles; un UUID). Sin `jti` ⇒ `401 asercion-sin-jti`. Validasoft guarda `(sistema, jti)` hasta `exp` + 30 s y rechaza un segundo uso, aunque llegue en paralelo, con `401 asercion-reutilizada`. El mismo `jti` en **otro** sistema es otra aserción. Consecuencia: el backend del sistema firma **una aserción por petición** (también para `POST /sesiones-usuario`), y un reintento con la misma `Idempotency-Key` lleva una aserción nueva (la idempotencia compara método, ruta y cuerpo, no cabeceras). |
| Páginas de `apps/web` servidas por la API (`/app/…`) | — | `Content-Security-Policy: frame-ancestors 'self' {orígenes}; base-uri 'self'; object-src 'none'` con la misma unión (caché de 60 s por nodo, invalidada al crear, editar orígenes o desactivar un sistema). `apps/public` (`/`): `frame-ancestors 'self'`. Ver [04 §10](04-arquitectura-plataforma.md). |
| CORS | — | Apagado por defecto (se recomienda el mismo origen). `Validasoft:Cors:OrigenesPermitidos[]` (orígenes exactos, validados como los de los sistemas; sin comodines) lo enciende para `GET, POST, PUT, PATCH, DELETE` con las cabeceras `Accept`, `Accept-Language`, `Authorization`, `Content-Type`, `If-Match`, `Idempotency-Key`, `X-Correlation-Id`, `X-Empresa`, `X-Validasoft-Cliente` (**nunca** `X-Api-Key` ni `X-Usuario-Delegado`) y expone `ETag`, `Idempotent-Replayed`, `Location`, `Retry-After`, `X-Correlation-Id`. `Validasoft:Cors:PermitirCredenciales` (falso por defecto) añade `Access-Control-Allow-Credentials` para la cookie `vs_tercero` entre subdominios. |
| Límite por IP de `/public/v1` | — | Cuenta la IP real (`X-Forwarded-For`) **solo** si la petición llega desde un proxy de `Validasoft:Proxies:Confiables[]` (IP o CIDR); `Validasoft:Proxies:Saltos` (1) proxies como máximo. Sin proxies configurados la cabecera se ignora. |

Contrato `postMessage` sin cambios (docs/06 §3.2); solo cambia de dónde salen los orígenes permitidos: `apps/web/src/sesion/puenteEmbed.ts` (`cargarOrigenesPermitidos`) los lee de `GET /public/v1/embed/origenes-permitidos` y, mientras no llegan o si la consulta falla, usa `VITE_ORIGENES_CONTENEDOR` (que en desarrollo se suma siempre).

#### 2.9.4 Estado de implementación — fase «Tipos de documento configurables» (TD, 2026-11-08)

Cada tipo de documento del tenant configura cuatro grupos de reglas (decisiones TD-1…TD-9 del [registro](decisiones/registro.md)). Todos los campos nuevos son **opcionales y compatibles**: un cliente que no los envía conserva el comportamiento anterior. Precisa §2.9.2 (la nota «solo clase `Archivo`» ya no aplica a los tipos con campos).

**Configuración** (`POST /api/v1/tipos-documento` · `GET` · `GET /{id}` · `PUT /{id}`, mismos permisos y auditoría `TipoDocumentoTenant`):

| Campo (crear y actualizar) | Significado |
|---|---|
| `formatosPermitidos?: string[]` | MIME o nombre corto (`pdf`, `jpg`/`jpeg`, `png`, `webp`); se guardan y devuelven como MIME. Vacío = los del canónico (o toda la lista blanca). Debe ser subconjunto del canónico: 422 `formato-mas-laxo`; fuera de la lista blanca ⇒ 422 `formato-invalido`. |
| `tamanoMaximoMb?: int` | 1–1024; `0` en un `PUT` vuelve al global. Por encima del global (`Validasoft:Boveda:TamanoMaximoBytes`) ⇒ 422 `limite-sobre-el-global`; por encima del canónico ⇒ `limite-mas-laxo`; fuera de rango ⇒ `limite-invalido`. |
| `paginasMaximas?: int` | 1–1000; `0` vuelve al global; mismas reglas (`MaximoPaginas` global). |
| `modo?: string` | `Archivo` (por defecto; `Formulario` si el canónico es de esa clase) · `Formulario` (solo campos, sin binario) · `ArchivoYFormulario` (archivo y campos en la misma versión). Un modo con campos sin campos ⇒ 422 `campos-requeridos`; `Archivo` con campos o en un canónico formulario ⇒ `modo-invalido`. |
| `campos?: CampoFormularioDto[]` | `{ clave, etiqueta, tipo: Texto\|Numero\|Fecha\|Lista\|SiNo, obligatorio, opciones?, ayuda? }`, en el orden de la lista (máx. 50). `clave` en snake_case única (`campo-invalido`, `campo-duplicado`); `opciones` solo y obligatorias en `Lista` (`campo-invalido`). Claves **reservadas** (tipo `Fecha`, si no ⇒ `campo-reservado`): `fecha_expedicion` (base de la vigencia) y `fecha_vencimiento` (vencimiento propio del documento). Un tipo con campos es siempre `PorVinculo` (422 `formulario-solo-por-vinculo`). En un `PUT`, `null` conserva y `[]` quita (solo con `modo: Archivo`). |

`TipoDocumentoTenantDto` añade `formatosPermitidos`, `tamanoMaximoMb?`, `paginasMaximas?`, `modo`, `campos[]`, `reglasEfectivas: ReglasArchivoDto { formatos[], tamanoMaximoBytes, paginasMaximas, descripcion }` (lo más estricto entre global, canónico y tipo; `descripcion` es el texto para el tercero, p. ej. «Certificado de existencia y representación legal admite solo PDF de hasta 5 MB»), `plantilla?: PlantillaVersionDto` (la vigente) y `cargaSoportada` (el tercero puede entregarlo: archivo de clase `Archivo`, o campos en un `PorVinculo`). Cambiar `reglaFrescura`, `diasAviso`, `diasGracia` o el ámbito recalcula las vigencias por mensaje. `TipoDocumentoCanonicoDto` añade `formatosPermitidos`, `tamanoMaximoMb?` y `paginasMaximas?` (techo de los tipos del tenant): `RUT`, `CAMARA_COMERCIO`, `ESTADOS_FINANCIEROS` y `DECLARACION_RENTA` solo PDF. En los flujos (`requisitos[]`) se puede pedir cualquier tipo con `cargaSoportada` (antes: solo clase `Archivo`); un formulario canónico global sin campos sigue respondiendo 422 `clase-no-soportada`.

**Reglas de archivo en la carga** (TD-1): en el paso 1 (`POST /public/v1/boveda/documentos`) el MIME declarado fuera de lo admitido por el tipo ⇒ 415 `tipo-archivo-no-soportado` y el tamaño declarado mayor ⇒ 413 `archivo-demasiado-grande`; en el paso 2 se vuelven a aplicar con el tipo real (magic bytes), el tamaño real y las páginas reales (más páginas ⇒ versión `Devuelta` con `demasiadas-paginas`). Si el tipo restringe algo, el `detail` del ProblemDetails es la `descripcion` del tipo («Certificado Norte admite solo PDF de hasta 1 MB.»); si no, los mensajes globales de siempre. Un canónico global usa las reglas del tipo que configuró la empresa del `vinculoId` de contexto (si viene) o las del canónico.

**Plantilla descargable** (TD-5; bucket del tenant, `plantillas/{tipoId}/{sha256}`; PDF, DOCX o XLSX hasta el tamaño global):

| Ruta | Permiso | Contrato |
|---|---|---|
| `PUT /api/v1/tipos-documento/{id}/plantilla` | `configuracion:escribir` | Paso 1. `IniciarPlantillaDto { nombreArchivo, tamanoBytes, tipoMime, sha256? }` → 200 `CargaDePlantillaDto { tipoDocumentoId, version, carga: InstruccionCargaDto }` (PUT prefirmado, 15 min). 413 / 415 (tipo declarado no es PDF/DOCX/XLSX) / 422 `tamano-invalido`, `sha256-invalido`. Iniciar otra reemplaza la pendiente. |
| `POST /api/v1/tipos-documento/{id}/plantilla/{version}/confirmar` | `configuracion:escribir` | Paso 2: verifica tamaño (413), SHA-256 (422 `hash-no-coincide`) y tipo real (415: `%PDF-` o ZIP de Office con `word/document.xml`/`xl/workbook.xml`). 200 `ConfirmacionPlantillaDto { duplicado, plantilla: PlantillaVersionDto }` (mismo archivo que la vigente ⇒ `duplicado: true`, sin versión nueva). 404 `carga-no-encontrada`; 409 `archivo-no-cargado`. Auditoría `PlantillaTipoDocumento` (`Publicado`). |
| `GET /api/v1/tipos-documento/{id}/plantilla` | `configuracion:leer` | `PlantillaTipoDocumentoDto { tipoDocumentoId, vigente?, historial[] }` con `PlantillaVersionDto { version, nombreOriginal, mime, tamanoBytes, sha256, cargadaEn, cargadaPor, vigente, retiradaEn? }` (historial de la más reciente a la más antigua). |
| `GET /api/v1/tipos-documento/{id}/plantilla/descarga?version=` | `configuracion:leer` | `DescargaDto` (5 min) de la vigente o de esa versión. 404 `plantilla-no-encontrada`. |
| `DELETE /api/v1/tipos-documento/{id}/plantilla` | `configuracion:escribir` | Deja el tipo sin vigente (el historial queda); 200 `PlantillaTipoDocumentoDto`. Auditoría `Archivado`. 404 `plantilla-no-encontrada`. |
| `GET /public/v1/expedientes/{id}/requisitos/{requisitoId}/plantilla` | sesión tercero | `DescargaDto` de la plantilla vigente del tipo del requisito. Expediente de un vínculo que no es de su sesión ⇒ 404 `expediente-no-encontrado` (T-4); 404 `requisito-no-encontrado`, `plantilla-no-encontrada`. |

**Formularios** (TD-4): `IniciarCargaDto` gana `datos?: { [clave]: valor }` (texto, número, fecha `aaaa-mm-dd`, opción, `true`/`false`) para tipos `ArchivoYFormulario`: se validan en el paso 1 y viajan con la versión al confirmar; en un tipo `Formulario` el paso 1 responde 422 `archivo-no-permitido` y con `datos` en un tipo de solo archivo, `datos-no-permitidos`. Nuevo:

| Ruta | Permiso | Contrato |
|---|---|---|
| `POST /public/v1/boveda/formularios` | sesión tercero | `EnviarFormularioDto { vinculoId, datos, tipoDocumentoTenantId? \| tipoCanonico? }` para un tipo `Formulario` del vínculo: crea una **versión** del documento `PorVinculo` (sin binario, `clase: Formulario`, `mime: application/json`) que pasa a `Actual`, se comparte con el vínculo, se adjunta a su expediente y calcula su vigencia. 200 `ConfirmacionCargaDto` (mismos datos ⇒ `duplicado: true`). 422 `campo-requerido`, `valor-invalido`, `campo-desconocido` (el ProblemDetails trae `campo` con la clave), `archivo-requerido` (el tipo pide también archivo: va por la carga en dos pasos con `datos`), `clase-no-soportada`, `tipo-documento-invalido`, `tipo-documento-requerido`, `vinculo-requerido`; 404 vínculo fuera de la sesión; 409 `estado-invalido`. |

Deduplicación: misma versión = mismo SHA-256 del archivo **y** misma huella de datos (SHA-256 del JSON canónico de pares `[clave, valor]` ordenados por clave). La descarga de una versión sin archivo ⇒ 409 `version-sin-archivo`. `VersionDocumentoDto` (bóveda, documentos del vínculo) gana `datosDeclarados?: DatoDeclaradoDto[] { clave, etiqueta, tipo, valor }` (valores normalizados: número invariante, fecha ISO, `true`/`false`) y `vigencia?`.

**Vigencia y avisos** (TD-6, TD-7): `VigenciaDto { vigenteHasta?, base, estado, diasAviso[], diasGracia, finDeGracia?, vencidaEn? }` con `base` `FechaExpedicion|FechaCarga|VencimientoPropio|SinVencimiento|PeriodoNoCalculado` y `estado` `Vigente|PorVencer|EnGracia|Vencida|SinVencimiento` (hoy, UTC). Aparece en `ExpedienteDto.requisitos[].versionActual.vigencia` y `ExpedientePublicoDto.requisitos[].versionActual.vigencia` (la de ese vínculo), en `DocumentoVistaEmpresaDto.versionesCompartidas[].vigencia` (la del vínculo de la ruta), en la bóveda del tercero (`versionActual.vigencia`: la más próxima; `porEmpresa[].vigencia`: la de cada empresa). Cálculo: `MaxDiasDesdeExpedicion(n)` desde `fecha_expedicion` declarada o, si no hay, desde la carga; `MaxDiasDesdeAprobacion(n)` desde la carga; `fecha_vencimiento` declarada acota si llega antes; `PeriodoMinimo` aún sin fecha; `Ninguna` no vence. Avisos: un correo por tercero y fecha a las 13:00 UTC de cada día `vigenteHasta − diasAviso` con todos sus documentos que vencen («vence para A el … y para B el …»). Vencimiento: el día siguiente a `vigenteHasta + diasGracia` (05:00 UTC) la versión queda `Vencida` para ese vínculo (evento `VersionVencida` del stream del documento; `VersionDocumentoDto.estado` pasa a `Vencida` cuando vence para todas las empresas con vigencia). **No** cambia el estado del vínculo ni abre renovaciones (H6).

`ExpedienteDto.requisitos[]` y `ExpedientePublicoDto.requisitos[]`: `tipoDocumento` (`TipoDocumentoRequisitoDto`) gana `modo?`, `campos?`, `archivo?: ReglasArchivoDto` (nulo en un tipo solo de formulario), `tienePlantilla` y `textoAyuda?`; `versionActual` (`VersionRequisitoDto`) gana `datosDeclarados?`, `vigencia?`, `estado?` (de la versión en la bóveda) y `clase?`.

- **Pendiente** (H6 y siguientes): `PorVencer`/`Vencido` del vínculo y expediente de renovación; fin de periodo de `PeriodoMinimo`; base `MaxDiasDesdeAprobacion` desde la aprobación de cada empresa; esquema canónico de formularios globales y campos privados por tenant; override por empresa de las reglas; zona horaria del tenant para fechas y avisos.

---

### 2.10 Ingreso directo sin contraseñas (2026-11-11, ING-1…ING-9)

Las rutas de `/api/v1/ingreso` son **anónimas** (el ingreso es lo que da la credencial), con un cupo propio por IP (`Validasoft:LimiteIngreso`, 30 por minuto) y sin `Idempotency-Key`; ninguna revela si un correo tiene cuenta. El token de acceso viaja en el cuerpo; el de refresco, solo en la cookie `vs_usuario` (`HttpOnly; SameSite=Strict; Path=/api/v1/ingreso/sesion`). El diseño está en [07 §2.7](07-seguridad-identidad-y-tenancy.md) y las decisiones, en ING-1…ING-9 del [registro](decisiones/registro.md).

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `POST /ingreso/codigo` | `SolicitarCodigoDto { correo }` ⇒ `CodigoSolicitadoDto { desafio, expira, mensaje }`, **idéntico** exista o no la cuenta (señuelo). Código de 6 dígitos al correo: 5 min, un solo uso; 5 por correo y hora | público (cupo por IP) | 200; 422 `correo-invalido`; 429 `demasiadas-peticiones` | — |
| `POST /ingreso/codigo/verificar` | `VerificarCodigoDto { desafio, codigo }` ⇒ `PasoDeIngresoDto { estado, desafio, cuentas?, opciones?, sesion? }`. `estado`: `ElegirCuenta`, `PasskeyRequerida` (con `opciones` para firmar), `RegistrarPasskey` (con `opciones` para crear) o `Sesion` (`SesionDirectaDto { token, expira, sesionId, metodo, sugerirPasskey }` y cookie `vs_usuario`) | público | 200; 404 `desafio-no-encontrado`; 410 `codigo-vencido`, `desafio-usado`; 422 `codigo-invalido` (con `intentosRestantes`), `sin-cuenta`; 429 `codigo-bloqueado` | — |
| `POST /ingreso/cuenta` | `ElegirCuentaDto { desafio, cuenta }`: con varias cuentas para el correo (`u:{grupo}:{usuario}` u `o:{operador}`), continúa con la elegida ⇒ `PasoDeIngresoDto` | público | 200; 422 `cuenta-invalida`; 410 | — |
| `POST /ingreso/passkey/opciones` | Opciones de WebAuthn para **entrar solo con passkey** (credencial detectable: sin credenciales permitidas; no revela quién tiene una) ⇒ `OpcionesDePasskeyDto { desafio, opciones }` | público | 200 | — |
| `POST /ingreso/passkey/verificar` | `VerificarPasskeyDto { desafio, respuesta }` (la credencial serializada, binarios en base64url): ingreso solo con passkey (`metodo = Passkey`) o segundo paso tras el código (`CodigoYPasskey`) ⇒ `PasoDeIngresoDto` con `Sesion` | público | 200; 401 `passkey-invalida`, `passkey-revocada`, `passkey-clonada`; 403 `cuenta-no-disponible`; 410 `desafio-usado`, `desafio-vencido` | — |
| `POST /ingreso/passkey/registro` | `RegistrarPasskeyDto { desafio, respuesta, nombre? }`: registra la primera passkey de una cuenta que la exige y abre la sesión (`CodigoYPasskey`) | público | 200; 401 `passkey-invalida`; 409 `passkey-duplicada`, `passkeys-excesivas` | — |
| `POST /ingreso/sesion/renovar` · `POST /ingreso/sesion/cerrar` | Renueva el token de acceso con la cookie (rota la cookie; `SesionDirectaDto`) o revoca la sesión y borra la cookie. Exigen `X-Validasoft-Cliente: web` | cookie `vs_usuario` | 200 / 204; 401 `sin-sesion`, `sesion-expirada`, `sesion-inactiva`, `sesion-revocada` (también por reuso), `passkey-requerida`; 403 `cliente-invalido`; 409 `renovacion-concurrente` (token anterior dentro de 10 s) | — |
| `GET /mi-cuenta` | `MiCuentaDto { tipo, id, nombre, correo, grupo?, roles[], exigePasskey, metodo, sesionId }` de la persona de la sesión directa | sesión directa | 200; 403 `solo-ingreso-directo`; 401 `sesion-revocada` | — |
| `GET /mi-cuenta/passkeys` · `POST /mi-cuenta/passkeys/opciones` · `POST /mi-cuenta/passkeys` · `PUT /mi-cuenta/passkeys/{id}` · `DELETE /mi-cuenta/passkeys/{id}` | Mis passkeys (`PasskeyDto`): listar, pedir opciones de registro (excluye las ya registradas), registrar (`RegistrarPasskeyDto`), renombrar (`RenombrarPasskeyDto`) y eliminar (la última no, si la cuenta la exige: 409 `passkey-obligatoria`) | sesión directa | 200 / 204; 401 `passkey-invalida`; 404 `passkey-no-encontrada`; 409 `passkey-duplicada`, `passkeys-excesivas`, `passkey-obligatoria` | — |
| `GET /mi-cuenta/sesiones` · `POST /mi-cuenta/sesiones/{id}/cerrar` | Mis sesiones abiertas (`SesionDeUsuarioDto { id, metodo, creadaEn, ultimaActividad, expiraEn, ip, dispositivo, actual, activa }`) y cierre de una (también la actual) | sesión directa | 200 / 204; 404 `sesion-no-encontrada` | — |
| `GET /usuarios/{id}/sesiones` · `POST /usuarios/{id}/sesiones/{sesionId}/cerrar` | Sesiones abiertas de una persona del grupo y cierre de una (motivo `administrador`) | `usuarios:administrar` | 200 / 204; 404 `usuario-no-encontrado`, `sesion-no-encontrada` | — |
| `GET /usuarios/{id}/passkeys` · `POST /usuarios/{id}/passkeys/{passkeyId}/revocar` | Passkeys de una persona (activas y revocadas) y su revocación (`RevocarPasskeyDto { motivo? }`): deja de valer, cierra las sesiones de la persona y queda auditada | `usuarios:administrar` | 200; 404; 409 `passkey-revocada` | — |
| `POST /usuarios/{id}/invitar` · `POST /usuarios` con `invitar: true` | Correo `InvitacionDeUsuario` (sin secreto) que remite a la app; `UsuarioDto` suma `exigePasskey`, `passkeys`, `invitadoEn` | `usuarios:administrar` | 200/201; 409 `ingreso-directo-apagado`, `usuario-inactivo`; 422 `ingreso-directo-apagado` (al crear con `invitar`) | — |
| `GET /configuracion/ingreso` · `PUT /configuracion/ingreso` | Política de ingreso del grupo (`ConfiguracionDeIngresoDto { modos[Delegado\|Directo], sesionHoras, inactividadMinutos, passkeyParaTodos }`); al habilitar `Directo` se completa el grupo de los usuarios anteriores y al apagarlo se cierran las sesiones directas. Auditoría `IngresoDelGrupo` | `usuarios:administrar` | 200; 422 `modos-de-ingreso-invalidos`, `sesion-invalida`, `inactividad-invalida` | — |
| `PUT /plataforma/tenants/{id}/ingreso` · `POST /plataforma/tenants` con `ingreso` | La misma política desde Plataforma; al crear, un grupo con `Directo` envía la invitación a su administrador | `plataforma:tenants` | 200/201 | — |
| `POST /plataforma/operadores` · `GET /plataforma/operadores` · `POST /plataforma/operadores/{id}/desactivar` | Operadores de ingreso directo (`CrearOperadorDto { nombre, correo }` ⇒ `OperadorDePlataformaDto`; id `DIRECTO:{correo}`; la lista incluye los de identidad delegada) | `plataforma:tenants` | 200/201; 409 `operador-duplicado`; 404 `operador-no-encontrado` | — |
| `GET /plataforma/operadores/{id}/passkeys` · `POST …/passkeys/{passkeyId}/revocar` · `GET …/sesiones` · `POST …/sesiones/{sesionId}/cerrar` | Passkeys y sesiones de un operador, con revocar y cerrar (el `{id}` va codificado: `DIRECTO%3Aana%40validasoft.co`) | `plataforma:tenants` | 200 / 204 | — |
| `GET /auditoria?tipo=Identidad&entidad=Ingreso\|Passkey\|Sesion&entidadId=&actor=` | Auditoría de identidad del grupo (ING-6): ingresos exitosos y fallidos, códigos, passkeys y sesiones, con IP y navegador | `auditoria:leer` | 200 | — |

**Errores propios.** 401 `sin-sesion` · `sesion-inactiva` · `passkey-invalida` · `passkey-revocada` · `passkey-clonada` · `passkey-requerida` · `cuenta-no-disponible`; 403 `modo-de-ingreso-no-admitido` · `solo-ingreso-directo`; 404 `desafio-no-encontrado` · `passkey-no-encontrada` · `sesion-no-encontrada` · `operador-no-encontrado`; 409 `ingreso-directo-apagado` · `passkey-duplicada` · `passkeys-excesivas` · `passkey-obligatoria` · `operador-duplicado` · `renovacion-concurrente`; 410 `codigo-vencido` · `desafio-usado` · `desafio-vencido`; 422 `codigo-invalido` · `sin-cuenta` · `cuenta-invalida` · `modos-de-ingreso-invalidos` · `sesion-invalida` · `inactividad-invalida`; 429 `codigo-bloqueado`.

## 3. Transversales

### 3.1 Idempotencia
Tabla `comandos_idempotentes(tenant_id, clave, hash_cuerpo, codigo_respuesta, cuerpo_respuesta, creado_en, expira_en = +24 h)` en la misma transacción Marten. Misma clave + mismo cuerpo ⇒ misma respuesta y `Idempotent-Replayed: true`; cuerpo distinto ⇒ `422 clave-idempotencia-reutilizada`; ausente ⇒ `400 clave-idempotencia-requerida`. Recomendación: `{sistema}-{entidad}-{id}-{accion}`.

### 3.2 Concurrencia
`ETag: "v{versionStream}"`; `If-Match` en `PUT`, `PATCH` y veredictos ⇒ `412 version-desactualizada`; veredicto sobre versión superada ⇒ `409 version-superada`.

### 3.3 Errores (ProblemDetails)

```json
{
  "type": "https://docs.validasoft.co/errores/requisito-no-en-alcance",
  "title": "El requisito no está en el alcance del área",
  "status": 409,
  "detail": "El área 'financiera' no revisa 'CAMARA_COMERCIO' en el flujo 'habilitacion-proveedores' v3.",
  "instance": "/api/v1/expedientes/01J9…/requisitos/01J9…/veredicto",
  "codigo": "requisito-no-en-alcance",
  "traceId": "00-4bf9…-01",
  "errores": [ { "campo": "motivos", "mensaje": "Debe indicar al menos un motivo para observar." } ]
}
```

| HTTP | `codigo` |
|---|---|
| 400 | `validacion` · `clave-idempotencia-requerida` · `esquema-invalido` · `identificacion-invalida` |
| 401 | `no-autenticado` · `sesion-expirada` · `sesion-revocada` (refresco del tercero, §2.9.3) · `llave-sin-sistema` · `sistema-inactivo` · `asercion-invalida` · `asercion-firma-invalida` · `asercion-vencida` · `asercion-vigencia-excesiva` · `asercion-sin-jti` · `asercion-reutilizada` (§2.9.4) · `asercion-incompleta` · `usuario-inactivo` (identidad delegada, ADR-0011) |
| 403 | `sin-permiso` · `cliente-invalido` (falta `X-Validasoft-Cliente`, §2.9.3) · `alcance-insuficiente` · `version-no-compartida` · `area-no-puede-rechazar` · `acceso-soporte-no-concedido` · `otp-requerido` · `no-es-miembro-del-area` · `conflicto-de-intereses` · `separacion-de-funciones` · `permiso-insuficiente` (solo dentro de la propia empresa; otro tenant/empresa ⇒ `404`, T-4) |
| 404 | `no-encontrado` · `enlace-no-encontrado` · `documento-no-encontrado` · `version-no-encontrada` · `carga-no-encontrada` · `tipo-documento-no-encontrado` · `requisito-no-encontrado` (incluye recursos de otro tenant/empresa: nunca se revela su existencia) |
| 409 | `estado-invalido` · `expediente-abierto-existente` · `version-superada` · `requisito-no-en-alcance` · `version-no-admitida-en-vinculo` · `version-referenciada` · `vinculo-duplicado` · `tercero-duplicado` · `relacion-duplicada` · `tercero-bloqueado` · `aprobador-repetido` · `conflicto-de-interes` · `invitacion-ya-aceptada` · `referencia-en-conflicto` · `referencia-duplicada` · `sistema-duplicado` · `usuario-duplicado` · `identidad-en-conflicto` · `identidad-duplicada` · `area-duplicada` · `textos-legales-faltantes` · `texto-desactualizado` · `invitacion-expirada` · `sin-invitacion` · `otp-no-solicitado` · `expediente-observado` · `area-no-activa` · `area-ya-se-pronuncio` · `area-automatica` · `area-inactiva` · `vinculo-bloqueado` · `expediente-no-abierto` · `sin-borrador` · `flujo-duplicado` · `motivo-duplicado` · `motivo-base` · `tipo-documento-duplicado` · `archivo-no-cargado` · `version-ya-confirmada` · `carga-desactualizada` · `version-duplicada` · `area-observada` · `sin-requisitos` · `requisito-ya-decidido` · `requisito-eximido` · `requisito-sin-version` · `requisitos-sin-corregir` · `renovacion-concurrente` |
| 410 | `enlace-usado` · `enlace-vencido` · `otp-vencido` |
| 412 | `version-desactualizada` |
| 413 | `archivo-demasiado-grande` |
| 415 | `tipo-archivo-no-soportado` |
| 422 | `origen-invalido` · `origenes-excesivos` (§2.9.4) · `clave-idempotencia-reutilizada` · `frescura-incumplida` · `ratificacion-no-permitida` · `hash-no-coincide` · `ambito-no-ampliable` · `override-mas-laxo` · `empresas-fuera-de-alcance` · `umbral-invalido` · `dkim-no-verificado` · `falta-flujo-por-defecto` · `alcance-invalido` · `clave-publica-invalida` · `referencia-sistema-invalido` · `referencia-sin-sistema` · `usuario-delegado-requerido` · `rol-invalido` · `miembros-invalidos` · `otp-invalido` (con `intentosRestantes`) · `consentimiento-faltante` · `consentimiento-rechazado` · `consentimiento-duplicado` · `consentimiento-requerido` · `tipo-consentimiento-invalido` · `tipo-texto-invalido` · `motivo-requerido` · `motivo-invalido` · `motivos-solo-al-observar` · `justificacion-requerida` · `veredicto-invalido` · `flujo-invalido` · `flujo-sin-version-publicada` · `area-invalida` · `area-repetida` · `etapas-requeridas` · `etapa-sin-areas` · `orden-duplicado` · `aprobadores-invalidos` · `sla-invalido` · `area-automatica-no-rechaza` · `politica-invalida` · `clase-no-soportada` · `tipo-documento-requerido` · `tipo-documento-invalido` · `vinculo-requerido` · `requisito-invalido` · `requisito-duplicado` · `requisito-area-invalida` · `area-sin-requisitos` · `sobre-version-requerida` · `versiones-requeridas` · `requisitos-requeridos` · `ids-invalidos` · `frescura-invalida` · `dias-invalidos` · `ambito-invalido` · `sha256-invalido` · `tamano-invalido` · `nombre-archivo-requerido` · `correo-invalido` (no existe el 422 "informativo": `presupuesto-ia-agotado` viaja como advertencia en un `2xx`, §1) |
| 429 | `limite-excedido` · `demasiadas-peticiones` (rate limit de `/public/v1`) · `otp-bloqueado` |
| 503 | `verificacion-no-disponible` |

Extensiones con datos (IN-12): `otp-invalido` lleva `intentosRestantes`; `version-superada` y `versiones-requeridas` llevan `requisitos: [ { requisitoId, versionVigente } ]` con los documentos afectados o faltantes y su versión vigente (DA-2).

### 3.4 SSE
`id` = cursor (`Last-Event-ID`), `event` = tipo público, `data` = payload de §7. Reenvío hasta 24 h. Tres streams: `GET /eventos/stream` (usuarios de `apps/web`, filtrado por la empresa activa), `GET /public/v1/expedientes/{id}/stream` (un expediente del tercero) y `GET /public/v1/boveda/stream` (toda la bóveda del tercero; ver §2.9).

**Streams de la campana y avisos de cambio (NT-12, NT-13).** `GET /api/v1/notificaciones/stream` (web de empresa, con `X-Empresa`) y `GET /public/v1/notificaciones/stream` (portal, `Authorization: Bearer` de la sesión del tercero) llevan, además de `event: notificacion` y `event: no_leidas`, `event: cambio` con `CambioEnVivoDto { recurso, id, vinculoId? }`: `recurso` es `expediente`, `vinculo`, `cuenta`, `documento`, `ficha` o `certificado`. **Solo avisa qué cambió, nunca datos**: el cliente vuelve a pedir lo que ya mostraba con sus permisos. La web solo recibe los de la empresa activa y su tenant; el portal, los de los vínculos de su sesión (y, sin vínculo, los de su ficha). El del portal se cierra cuando vence el token de la sesión (el cliente reconecta con el renovado). Los avisos viven 30 minutos (`AvisoDeCambio`): un stream que se cae y vuelve a conectar no recupera lo perdido, el cliente refresca todo. Consulta cada 2 s; `id` solo en `notificacion`.

---

## 4. Catálogo de DTOs

Tipos base: `uuid`, `string`, `int`, `decimal`, `bool`, `date` (`YYYY-MM-DD`), `datetime` (ISO UTC), `enum` (textual de [03 §3.1](03-dominio-y-eventos.md)), `json`. `*` = obligatorio.

| DTO | Campos |
|---|---|
| `PaginaDto<T>` | `datos*: T[]`, `siguienteCursor: string?` |
| `ProblemaDto` | `type*`, `title*`, `status*`, `detail`, `instance`, `codigo*`, `traceId*`, `errores: {campo, mensaje}[]` |
| `IdentificacionDto` | `pais*: string (ISO 3166-1 alpha-2)`, `tipo*: string (catálogo por país)`, `numero*: string (normalizado, solo dígitos/letras, sin DV)`, `digitoVerificacion: string?` |
| `TipoIdentificacionDto` | `pais*`, `codigo*`, `nombre*`, `aplicaA*: enum(Natural\|Juridica\|Ambas)`, `patron: string?`, `tieneDv*: bool` |
| `TerceroDto` | `id*`, `identificacion*: IdentificacionDto`, `tipoPersona*: enum(Natural\|Juridica\|Consorcio)`, `nombre*`, `nombreComercial`, `direccionPrincipal: DireccionDto`, `contactos: ContactoDto[]`, `relaciones: RelacionEntreTercerosDto[]` (representantes, socios/UBO, miembros; B-3), `sucursales: SucursalDto[]`, `sucesorDe: uuid?`, `esExtranjero*: bool`, `atributosPrivados: json` (del tenant), `creadoEn*`, `version*: int` |
| `RelacionEntreTercerosDto` | `id*`, `terceroRelacionadoId*` (persona natural con bóveda propia), `identificacion*: IdentificacionDto`, `nombre*`, `tipo*: enum(RepresentanteLegal\|Suplente\|Socio\|BeneficiarioFinal\|MiembroDe)`, `principal*: bool`, `participacion: decimal?` (socios/UBO/miembros), `cargo`, `vigenteDesde*: date`, `vigenteHasta: date?`, `estado*: enum(Propuesta\|Confirmada\|Finalizada)`, `origen*: enum(Tercero\|Empresa\|Ia\|Importada)`, `verificaciones: {tipo, estado, vigenteHasta}[]` (cédula, antecedentes, listas de esa persona) |
| `RepresentanteDto` | Vista derivada de `RelacionEntreTercerosDto` con `tipo ∈ {RepresentanteLegal, Suplente}`: `terceroRelacionadoId*`, `identificacion*`, `nombre*`, `principal*: bool`, `cargo`, `verificadoHasta: date?` |
| `ContactoDto` | `nombre*`, `correo*`, `telefono`, `rol*`, `asignadoPorEmpresa: bool` |
| `DireccionDto` | `pais*`, `region`, `ciudad*`, `linea1*`, `linea2`, `codigoPostal` |
| `SucursalDto` | `id*`, `nombre*`, `direccion*: DireccionDto` |
| `EmpresaDto` | `id*`, `tenantId*`, `identificacion*`, `nombre*`, `logoUrl`, `replyTo`, `prefijoFolio*`, `activa*` |
| `UsoTenantDto` | `tenantId*`, `periodo*`, `usuarios*`, `empresas*`, `vinculosActivos*`, `gbAlmacenados*`, `lecturasIa*` (informativo: lecturas de versiones de terceros con vínculo en el tenant; **coste de plataforma**, no se factura por tenant, B-2), `decisionesIa*` (facturables: automáticas + asistidas), `webhooksEntregados*`, `costoDecisionesUsd*`, `porEmpresa*: {empresaId, vinculosActivos, decisionesIa}[]` |
| `TipoDocumentoCanonicoDto` | `id*`, `codigo*`, `version*`, `nombre*`, `ambito*`, `clase*`, `aplicaA*`, `tieneVencimientoIntrinseco*`, `tieneFechaExpedicion*`, `esPeriodico*`, `periodicidad`, `permiteRatificacion*`, `camposClave*: string[]`, `camposCriticos*: string[]`, `esquemaDatos: json`, `sensibilidad*: enum(Identidad\|Financiera\|Personal)`, `reglaFrescuraSugerida: ReglaFrescuraDto`, `retencionAniosSugerida*` |
| `TipoDocumentoTenantDto` | **Defaults del tenant** (B-4): `id*`, `codigo*`, `nombre*`, `tipoCanonicoId`, `versionCanonico`, `ambito*`, `clase*`, `textoAyuda`, `plantillaVersionId`, `reglaFrescura*: ReglaFrescuraDto`, `diasAviso*: int[]`, `diasGracia*` (default 15; 0 para críticos de cumplimiento), `politicaDesactualizada*: enum(SoloAviso\|BloquearTrasGracia)` (default `BloquearTrasGracia`, A-2), `diasRevalidacion*` (default 15), `nivelRevision*`, `camposCuyoCambioExigeHumano: string[]`, `porcentajeMuestreo*`, `aprobadoresRequeridos*`, `permiteRatificacion*`, `retencionAnios*`, `camposPrivados: json` (formularios). No existe `iaHechos`: la lectura de hechos es de plataforma (B-2) |
| `TipoDocumentoEmpresaDto` | **Override por empresa** (B-4), todos opcionales y solo hacia más estricto: `empresaId*`, `tipoDocumentoTenantId*`, `reglaFrescura`, `diasAviso`, `diasGracia`, `politicaDesactualizada`, `diasRevalidacion`, `nivelRevision`, `camposCuyoCambioExigeHumano`, `porcentajeMuestreo`, `aprobadoresRequeridos`, `permiteRatificacion`; en lectura añade `efectivo*: {campo: {valor, origen: enum(Tenant\|Empresa\|Regla)}}` |
| `FlujoPorDefectoDto` | `empresaId*`, `tipoVinculoId*`, `flujoId*` |
| `ReglaFrescuraDto` | `tipo*: enum(Ninguna\|MaxDiasDesdeExpedicion\|PeriodoMinimo\|MaxDiasDesdeAprobacion)`, `dias: int?`, `regla: string?` |
| `AreaDto` | `id*`, `empresaId*`, `codigo*`, `nombre*`, `miembros*: uuid[]` (usuarios), `activa*`, `responsableId`, `escalamientoA: uuid[]` (H4) |
| `MotivoDto` | `id*`, `codigo*`, `nombre*`, `categoria*: enum(Documento\|Formulario\|Contacto\|Otro)`, `aplicaA*: enum(Observacion\|Rechazo\|Ambos)`, `textoAlTercero*`, `base*: bool` |
| `FlujoDefinicionDto` | `id*`, `codigo*`, `nombre*`, `tipoVinculoId*`, `version*`, `estado*: enum(Borrador\|Publicado\|Archivado)`, `etapas*: EtapaDto[]`, `reglas*: ReglaExigenciaDto[]`, `verificacionesRequeridas: VerificacionRequeridaDto[]`, `politicaDevolucion*`, `politicaNuevaVersion*`, `politicaVigenciaVinculo*`, `plazoCargueDias*`, `recordatoriosCargue*: int[]`, `separacionDeFunciones*`, `plantillaCertificado` |
| `EtapaDto` | `orden*`, `areas*: {areaId*, rolesPermitidos, usuariosPermitidos, slaHoras, puedeEximir, puedeRechazarExpediente, requisitosEnAlcance}[]` |
| `ReglaExigenciaDto` | `id*`, `tipoDocumentoTenantId*`, `tiposAceptados: uuid[]`, `obligatorio*`, `critico*`, `siempreEnRenovacion*`, `areasQueRevisan*: uuid[]`, `dePersonaRelacionada: enum(RepresentanteLegal\|Suplente\|Socio\|BeneficiarioFinal\|MiembroDe)?` (el requisito exige el documento de la persona relacionada con ese rol, B-3), `override: {reglaFrescura, nivelRevision, diasRevalidacion, politicaDesactualizada, aprobadoresRequeridos, porcentajeMuestreo}` (tercer nivel de la jerarquía tenant → empresa → regla, B-4), `plantillaVersionId`, `textoAyudaOverride`, `condicion: CondicionDto` |
| `CondicionDto` | `tipo*: enum(Y\|O\|No\|Predicado)`, `hijos: CondicionDto[]`, `atributo`, `operador`, `valor: json` |
| `VerificacionRequeridaDto` | `tipo*`, `proveedor`, `bloqueante*`, `vigenciaDias`, `etapa`, `frecuenciaReconsultaMeses` |
| `SimulacionDto` | `contexto*: {tercero: json, vinculo: json}` → `requisitos*: RequisitoCalculadoDto[]` |
| `CuentaBancariaDto` | `id*`, `pais*`, `banco*`, `tipoCuenta*`, `numeroEnmascarado*`, `titular*`, `identificacionTitular*`, `moneda*`, `certificacion: {documentoId, version, estadoHechos, identidadCoincide}`, `estado*` |
| `DesignarCuentaDto` | `cuentaId*` |
| `CuentaDesignadaDto` | `cuentaId*`, `estado*: EstadoCuentaDesignada`, `propuestaEn*`, `aprobadaPor: string[]`, `cuentaAnteriorId` |
| `VinculoDto` | `id*`, `empresaId*`, `terceroId*`, `tipoVinculo*`, `flujoId*`, `estado*`, `atributos*: json`, `vigenteHasta`, `invitacionAceptadaEn: datetime?` (hasta entonces nada está compartido con esta empresa, B-1), `requisitosCubribles: int?` (solo antes de la aceptación), `revalidacionPendiente*: bool`, `revalidaciones: {tipo, versionNueva, plazo, bloqueante}[]`, `certificado: {folio, estado}`, `cuentaDesignada: CuentaDesignadaDto`, `bloqueo: {motivo, desde, hasta}`, `referencias*: ReferenciaExternaDto[]` (una por sistema integrado que comparte el vínculo, I-0), `version*` |
| `ReferenciaExternaDto` | `sistema*` (código del `SistemaIntegrado`; al crear lo fija la API key), `id*` (identificador en ese sistema) |
| `VersionAceptadaDto` | `tipo*`, `documentoId*`, `version*`, `vigenteDesde*`, `vigenteHasta`, `motivoVigencia*`, `estado*: EstadoAceptacion`, `aceptadaEn*`, `aceptadaPor*`, `expedienteId*`, `versionAnterior` |
| `CrearExpedienteDto` | `empresaId*` (o header), `tercero*: {identificacion*, tipoPersona*, nombre*, contactos*}` o `terceroId*`, `vinculo*: {tipo*, atributos}`, `flujo: {codigo*, version}` (**opcional**: sin él se aplican reglas de selección y flujo por defecto, I-12), `referenciaExterna`, `invitacion: {canal*: enum(Correo), vigenciaHoras, requiereOtp}`, `plazoCargueDias` |
| `ExpedienteDto` | `id*`, `folio*` (formato `{PrefijoEmpresa}-E-{Año}-{Secuencia}`, T-9), `empresaId*`, `vinculoId*`, `terceroId*`, `tipo*`, `estado*`, `flujo*: {id, codigo, version}`, `origenFlujo*: enum(Explicito\|Regla\|PorDefecto)`, `reglaSeleccionId`, `plazoCargueHasta`, `reaperturas*`, `invitacionAceptadaEn`, `requisitosCubribles` (antes de la aceptación, B-1), `etapas*: {orden, estado, areas: {areaId, estado, slaVence, revisor}[]}[]`, `requisitos*: RequisitoDto[]`, `verificaciones*: VerificacionDto[]`, `enlaceTercero: {url, expira}`, `puede*: {aprobar, observar, rechazarExpediente, eximir, asignar, reabrir, anular, cambiarFlujo}`, `advertencias: {codigo, mensaje}[]`, `version*` |
| `RequisitoDto` | `id*`, `tipo*: {id, codigo, nombre, clase, ambito}`, `tiposAceptados: string[]`, `dePersonaRelacionada: {rol, terceroRelacionadoId, nombre}` (B-3), `obligatorio*`, `critico*`, `estado*: EstadoRequisito` (sin `Rechazado`, B-7), `nivelRevision*` (valor efectivo tenant → empresa → regla), `aprobadoresRequeridos*`, `versionActual: VersionDocumentoDto` (solo tras la aceptación de la invitación), `versionAceptada: VersionAceptadaDto`, `versionesNoAdmitidas: int[]`, `hechosAplicados: {frescuraCumplida, vigenciaCalculada, comparacion, camposCriticosCambiados, confianza, conIa}`, `sugerenciaAutomatica: {veredicto, razon}`, `veredictos*: VeredictoEmitidoDto[]`, `reenvios*`, `ayuda: {texto, plantillaUrl}`, `reglaFrescura: ReglaFrescuraDto`, `siguientePaso` |
| `VeredictoDto` | `area*`, `veredicto*: enum(Aprobado\|Observado)` (B-7: el rechazo es del expediente, no de la versión), `sobreVersion*: int`, `motivos: string[]` (obligatorio si `Observado`), `camposAfectados: string[]`, `observacion`, `noAdmitirEstaVersion: bool` (solo con `Observado`; la versión no vuelve a aceptarse en este vínculo) |
| `VeredictoEmitidoDto` | `id*`, `area*`, `veredicto*: enum(Aprobado\|Observado)`, `noAdmitida*: bool`, `origen*: OrigenVeredicto`, `usuario`, `numeroAprobador`, `fecha*`, `sobreVersion*`, `motivos`, `camposAfectados`, `observacion`, `evidencia: EvidenciaIaDto`, `anuladoPor` |
| `RechazarExpedienteDto` | `areaId*`, `motivos*: string[]`, `justificacion*`, `notificarTercero*: bool` (default `true`) |
| `AceptarInvitacionDto` | `consentimientos*: {tipo*: enum(AvisoPlataforma\|AutorizacionEmpresa\|TerminosEmpresa), textoVersion*, aceptado*: bool}[]`, `otpCodigo` (si la política lo exige) → respuesta `{ compartidos*: {tipoCanonico, version}[], requisitosCubiertos*: int, aceptacion*: AceptacionDto }` |
| `AccesoSoporteDto` | `id*`, `alcance*: {tipo*: enum(Empresa\|Expediente\|Tercero), id*}`, `horas*: int (1–72)`, `justificacion*`, `concedidoPor*`, `concedidoEn*`, `expiraEn*`, `revocadoEn`, `usos*: int` |
| `EvidenciaIaDto` | `modelo*`, `confianza*`, `camposComparados*: {campo, anterior, nuevo, cambio}[]`, `hashPrompt*`, `costoUsd*` |
| `EximirDto` | `justificacion*` |
| `AsignacionDto` | `areaId*`, `requisitoId`, `usuarioId*` |
| `VerificacionDto` | `tipo*`, `proveedor*`, `resultado*: enum(SinCoincidencias\|Coincidencias\|Aprobado\|Rechazado)`, `referenciaExterna*`, `evidenciaVersionId`, `vigenteHasta`, `coincidencias: {id, nombre, lista, decision, justificacion, vigenteHasta}[]`, `estado*: EstadoVerificacion` |
| `ResolverCoincidenciaDto` | `decision*: enum(Descartada\|Confirmada)`, `justificacion*` |
| `IniciarCargaDto` | `nombreArchivo*`, `tamanoBytes*`, `tipoMime*`, `sha256*`, `datosDeclarados: {fechaExpedicion, fechaVencimiento, periodo, numero}`, `tipoCanonico: string` (obligatorio en carga directa a bóveda), `terceroId: uuid` (obligatorio en `POST /documentos`), `plantillaVersionId` |
| `CargaPrefirmadaDto` | `documentoId*`, `version*`, `duplicado*: bool`, `carga: {metodo*, url*, cabeceras*: json, expira*}` |
| `EnviarFormularioDto` | `datos*: json`, `esquemaVersion*`, `soporte: {documentoId, version}` |
| `DocumentoDto` | `id*`, `terceroId*`, `tipoCanonico*`, `clase*`, `ambito*`, `versionActual: int?`, `versiones*: VersionDocumentoDto[]`, `retirado*` |
| `VersionDocumentoDto` | `documentoId*`, `n*`, `estado*: EstadoVersion`, `cargadaEn*`, `cargadaPor*`, `clase*`, `nombreOriginal`, `mime`, `tamanoBytes`, `paginas`, `sha256`, `hashDatos`, `esquemaVersion`, `datos: json` (formulario), `datosDeclarados`, `hechos: HechosDto` |
| `HechosDto` | `conIa*`, `hashVerificado*`, `antivirus*`, `mimeReal*`, `paginas`, `comparacion*: Comparacion`, `camposCambiados: string[]`, `camposCriticosCambiados: string[]`, `tipoDetectado`, `coincideTipoEsperado`, `legibilidad`, `identidadCoincide`, `fechaExpedicion`, `fechaVencimientoDoc`, `periodo`, `camposClave: json`, `confianza`, `confianzaPorCampo: json`, `senalesManipulacion: string[]`, `modelo`, `pendienteRevisionHumana*` |
| `CorreccionDatoDto` | `campo*`, `valor*`, `justificacion` |
| `DescargaDto` | `url*`, `expira*`, `nombre*`, `mime*` |
| `DocumentoVistaEmpresaDto` | `documentoId*`, `tipo*`, `versionActual*: VersionDocumentoDto`, `versionesCompartidas*: int[]`, `aceptacionEnMisVinculos*: {vinculoId, version, estado, vigenteHasta}[]` |
| `CertificadoDto` | **Implementado con otra forma: ver §2.6.1.** `folio*` (formato `{PrefijoEmpresa}-C-{Año}-{Secuencia}-{n}`, T-9), `emision*`, `estado*: EstadoCertificado` (incluye `Suspendido`, B-5), `empresa*: {id, nombre}`, `vinculoId*`, `expedienteFolio*`, `tercero*: {nombre, identificacionEnmascarada}`, `flujo*`, `emitidoEn*`, `vigenteHasta`, `huella*`, `urlVerificacion*`, `requisitos*: {codigo, version, vigenteHasta, eximido}[]`, `areas*: {area, origen: enum(Humano\|Sistema\|Administrador), fecha}[]`, `pendientes: {causa, tipo, bloqueante}[]`, `suspension: {motivo, desde}` |
| `CertificadoPublicoDto` | **Implementado con otra forma: ver §2.6.1.** `folio*`, `estado*: EstadoCertificado`, `bloqueante*: bool` (A-2/B-5), `titular*: {nombre, identificacionEnmascarada}`, `emisor*: {empresa, logoUrl}`, `flujo*`, `emitidoEn*`, `vigenteHasta`, `huella*`, `contenidoCanonico*: CertificadoContenidoDto` (lo que se hashea; sin contactos ni nombres de revisores), `urlVerificacion*`, `pendientes: string[]`, `firma*: {algoritmo: "Ed25519", clavePublicaUrl, valor}` |
| `RevocarDto` | `motivo*` |
| `ConsultaCertificacionDto` | `certificado*: bool`, `folio`, `estado: EstadoCertificado`, `estadoVinculo*: EstadoVinculo \| "SinVinculo"`, `vinculoId`, `terceroId`, `vigenteHasta`, `revalidacionPendiente*: {tipo, plazo, bloqueante}[]`, `documentosVencidos*: string[]`, `bloqueado*: bool`, `motivoBloqueo: enum(Bloqueo\|PlazoRevalidacion\|Vencido)?` (B-5/A-2), `cuentaDesignada: {numeroEnmascarado, banco}`, `urlVerificacion`, `legacy: {estadoTercero: int}`, `fuenteRespaldo*: bool`, `antiguedadSegundos: int?` (solo con `fuenteRespaldo = true`) |
| `VencimientoDto` | `vinculoId*`, `empresaId*`, `folio`, `tercero*`, `tipo*`, `version*`, `vigenteHasta*`, `dias*`, `estado*: enum(Vencido\|PorVencer\|Vigente)`, `critico*`, `permiteRatificar*`, `ultimaNotificacion` |
| `NotificarVencimientosDto` | `vinculoIds*: uuid[]`, `plantilla` |
| `FrescuraDto` | `documentos*: {tipo, versionActual, proximoVencimiento, porEmpresa: {empresa, vigenteHasta, estado}[], permiteRatificar}[]` |
| `EntregaNotificacionDto` | `id*`, `evento*`, `canal*`, `destinatario*`, `enviadoEn*`, `estado*: enum(Enviado\|Entregado\|Abierto\|Rebotado\|Fallido)`, `evidencia: json` |
| `WebhookDto` | `id*`, `url*`, `eventos*: string[]`, `empresas*: string[]`, `version*`, `activo*`, `cabecerasExtra`, `secreto` (solo al crear/rotar), `fallosConsecutivos*` |
| `EntregaWebhookDto` | `id*`, `eventoId*`, `tipo*`, `intento*`, `estado*`, `codigoHttp`, `enviadoEn*`, `proximoIntento`, `requestTruncado`, `responseTruncado` |
| `EventoPublicoDto` | sobre común de §6.2 |
| `InvitacionDto` | `canal*: enum(Correo)`, `vigenciaHoras*`, `requiereOtp*`, `correoDestino` → `{url, expira}` |
| `SesionTerceroDto` | `accessToken*`, `expira*`, `terceroId*`, `otpVerificado*: bool`, `vinculos*: {id, empresa, estado}[]`, `expedienteOrigen` |
| `SesionEmbedDto` | `vinculoId` o `expedienteId`*, `expiraMinutos` → `{token, url, expira}` |
| `AutorregistroDto` | `identificacion*`, `tipoPersona*`, `nombre*`, `correo*`, `consentimientoPlataforma*: bool`, `rut: carga`, `cedula: carga`, `camara: carga` |
| `ExpedientePublicoDto` | subconjunto de `ExpedienteDto` sin áreas internas: `requisitos[] {…, siguientePaso*, observacionVigente, resultadoAutomatico}` |
| `VinculoPublicoDto` | `id*`, `empresa*`, `tipoVinculo*`, `estado*`, `certificado`, `pendientes*: int`, `cuentaDesignada` |
| `SolicitudSupresionDto` | `id*`, `alcance*: enum(Todo\|Documentos\|Datos)`, `motivo`, `estado*: enum(Recibida\|EnEvaluacion\|Resuelta)`, `resultado: {suprimidos, retenidosPorLey: {tipo, baseLegal, hasta}[]}`, `constanciaUrl` |
| `ExportacionDto` | `id*`, `estado*`, `url`, `hashZip`, `expira` |
| `KpisDto` | `periodo*`, `alcance*: enum(Tenant\|Empresa\|Flujo)`, `activos*`, `tiempoPromedioDias*`, `tiempoMedianaDias*` (días hábiles según calendario del tenant), `avisosPorDocumento*` (promedio de correos de vencimiento por documento global; meta 1), `pctAprobacionAutomatica*` (decisiones `Automatico` sin intervención; las `Asistido` confirmadas y el muestreo cuentan aparte), `precisionIa*`, `slaCumplidoPorArea*: json`, `porEtapa*: json`, `decisionesAutomaticas*: {aprobadas, observadas, rechazadas, revertidas, muestreoRevisado, horasAhorradasEstimadas}`, `consumoIa*: {lecturas, decisiones, costoUsd, presupuestoUsd}`, `equidad*: {porTipoPersona: json, porRegion: json, porTipoDocumento: json, alertas: int}` (campos de `KpisTenant` en [03 §14](03-dominio-y-eventos.md)) |
| `AceptacionDto` | Evidencia de una aceptación (T&C, autorización de la empresa, aviso de la plataforma; P-3-5, P-16-4): `documentoVersionId*` (versión `PorVinculo` que la registra), `textoVersion*` (id de la versión del texto aceptado), `hashTexto*: sha256`, `fecha*: datetime`, `ip*`, `userAgent*`, `otpVerificado*: bool`, `canal*: enum(Web\|Embed)`, `trazoUrl` (H8), `aceptadoPor*: {terceroId, correo}` |
| `TenantDto` | `id*`, `slug*`, `nombre*`, `region*`, `plan*`, `claveKmsId*`, `zonaHoraria*`, `idioma*`, `activo*`, `creadoEn*` |
| `TenantConfigDto` | `zonaHoraria*`, `calendarioHabil*: {pais, festivosExtra: date[]}`, `politicasPorDefecto*: {reglaFrescura, diasAviso, diasGracia, diasRevalidacion, politicaDesactualizada, nivelRevision, aprobadoresRequeridos, porcentajeMuestreo}` (mismos nombres que `TipoDocumentoTenantDto`, B-4), `smtp: {host, puerto, usuario, remitente}` (secreto nunca se devuelve), `marca: {logoUrl, colorPrimario, colorSecundario, modoClaro}` (la marca por empresa va en `EmpresaDto`, T-18), `separacionDeFunciones*: bool`, `consentimientoIa*: {versionTexto, obligatorio}` |
| `PaisDto` | `codigo*: string (ISO 3166-1 alpha-2)`, `nombre*`, `tiposIdentificacion*: TipoIdentificacionDto[]`, `monedaPorDefecto` |
| `ApiKeyDto` | `id*`, `prefijo*`, `nombre*`, `scopes*: string[]`, `empresas*: string[]`, `ipsPermitidas: string[]`, `expiraEn`, `ultimoUsoEn`, `creadaEn*`, `valor` (solo al crear/rotar), `sandbox*: bool` (única forma; no existe `modo`), `sistemaId: uuid?` (ADR-0011), `rotadaDe: uuid?`, `reemplazadaPor: uuid?` (CN-3) |
| `SistemaIntegradoDto` | `id*`, `codigo*` (mayúsculas, único en el tenant; es el `iss` de las aserciones), `nombre*`, `clavePublicaPem*` (SPKI PEM, RSA ≥ 2048 o EC P-256), `activo*`, `creadoEn*` |
| `SesionUsuarioDto` | `token*` (JWT de Validasoft, HS256, 30 min, con tenant, usuario, sistema, scopes y empresas efectivos), `expira*` |
| `IdentidadExternaDto` | `sistemaId*`, `idExterno*` (`sub` de la aserción); única por tenant |
| `UsuarioDto` | `id*`, `correo*` (normalizado, único en el tenant), `nombre*`, `identificacion: IdentificacionDto?` (para el cruce de conflicto de intereses contra contactos y personas relacionadas del tercero, I-21), `activo*`, `identidades*: IdentidadExternaDto[]`, `roles*: RolAsignadoDto[]`, `creadoEn*`, `ultimoAcceso`; con cuentas locales: `dosFactores*: enum(Pendiente\|Activo\|Exento)`, `delegaciones: DelegacionDto[]` |
| `RolAsignadoDto` | `rol*: enum(Administrador\|Revisor\|OficialCumplimiento\|Auditor)`, `empresaId: uuid?` (nulo = todo el tenant); `areas: uuid[]` (H4). `Plataforma` no es un rol de tenant |
| `RolDto` | `codigo*`, `nombre*`, `permisos*: string[]` |
| `PermisoDto` | `codigo*`, `descripcion*`, `requiereArea*: bool` |
| `DelegacionDto` | `id*`, `delegadoId*`, `desde*`, `hasta*`, `empresaId*`, `areas*: uuid[]`, `activa*` |
| `PropuestaTipoDto` | `id*`, `tenantId*`, `codigoPropuesto*`, `nombre*`, `justificacion*`, `tipoTenantId*` (opera como `PorVinculo` mientras tanto), `estado*: enum(Pendiente\|Aprobada\|Rechazada)`, `tipoCanonicoId` (al aprobar), `comentarioPlataforma` |
| `EsquemaFormularioDto` | `tipoCanonicoId*`, `versionEsquema*`, `esquemaCanonico*: json (JSON Schema)`, `camposPrivadosTenant: json`, `esquemaFusionado*: json`, `camposClave*: string[]`, `camposCriticos*: string[]`, `uiSchema: json` |
| `DatosMaestrosDto` | Formulario global `DATOS_MAESTROS` que propone el tercero: `nombre*`, `nombreComercial`, `direccionPrincipal*: DireccionDto`, `contactos*: ContactoDto[]`, `actividadEconomica`, `regimenTributario`; las personas relacionadas van por `/public/v1/tercero/relaciones` (B-3); `cambiosCriticos` lo calcula el servidor. Si la IA de plataforma está disponible y el tercero consintió, se contrasta contra el RUT actual; si no, queda pendiente de revalidación manual en cada empresa (P-1-7) |
| `TipoVinculoDto` | `id*`, `codigo*`, `nombre*`, `atributosPermitidos*: {clave, tipo, obligatorio, opciones}[]`, `exigeCuentaDesignada*`, `exigeOtpEnEnlace*: bool`, `activo*` (el flujo por defecto es por empresa: `FlujoPorDefectoDto`) |
| `RequisitoCalculadoDto` | `tipoDocumentoTenantId*`, `codigo*`, `obligatorio*`, `critico*`, `reglaId*`, `motivoAplicacion*: string` (predicado que lo activó), `tiposAceptados: string[]` |
| `BandejaExpedienteDto` | Fila de `BandejaRevision`: `empresaId*`, `expedienteId*`, `folio*`, `tipoExpediente*`, `tercero*: {id, nombre, identificacion}`, `flujo*: {id, nombre, version}`, `etapaActual*`, `etapas*: {nombre, estado}[]`, `estadoPorArea*: {areaId, estado, por, automatico, fecha}[]`, `vencimientoMasProximo: {requisito, fecha, dias}`, `revisorAsignado`, `diasEnEtapa*`, `requisitosPendientes*`, `decididosPorIa*`, `ultimaActividad*` |
| `NovedadDto` | `id*`, `tipo*`, `titulo*`, `cuerpo*` (texto plano; sin HTML del tercero), `enlace*`, `empresaId`, `leida*`, `fecha*` |
| `TimelineEventoDto` | `id*`, `tipo*`, `fecha*`, `actor*: {nombre, rol, esTercero, esSistema}`, `requisitoId`, `antes: json`, `despues: json`, `comentario` |
| `PlantillaNotificacionDto` | `evento*`, `canal*`, `asunto*`, `cabecera`, `pie`, `cuerpoLiquid` (H8), `variablesPermitidas*: string[]`, `version*` |
| `PreferenciasNotificacionDto` | `porEvento*: {evento, correo: bool, campana: bool}[]`, `digestDiario*: bool`, `horaDigest`, `idioma` |
| `RevalidacionDto` | Fila de `BandejaRevalidaciones`: `empresaId*`, `vinculoId*`, `expedienteId*`, `tercero*`, `tipoDocumento*`, `versionAceptada*: {n, fecha}`, `versionActual*: {n, fecha}`, `comparacion*: {resultado: Comparacion, camposCambiados, hayCriticos}`, `nivelRevision*`, `hechosDisponibles*: bool`, `decisionAutomatica`, `plazoHasta*`, `diasRestantes*`, `bloqueante*: bool` |
| `RevisionAutomaticaConfigDto` | Configuración de **decisión** (la lectura es de plataforma, B-2): `habilitada*`, `modeloPorDefecto*`, `modeloPorTipo: {tipoCanonico, modelo}[]`, `umbralAprobar*` (0,95), `umbralAsistido*` (0,80), `porcentajeMuestreo*` (5), `muestraMinimaSemanal*` (1 por tipo, P-6-3), `presupuestoMensualUsd*` (decisiones), `alertaAlPorcentaje*: int` (80). La acción al agotar el presupuesto no la edita el tenant: vive en `PlanDto.accionAlAgotar` (I-23) |
| `ExcepcionDto` | `id*`, `empresaId*`, `expedienteId*`, `requisitoId`, `tipo*: enum(Critico\|Confianza\|Manipulacion\|Muestreo\|RevisionSolicitada\|CuentaDesignada\|CoherenciaSoporte)`, `motivo*`, `hechos: HechosDto`, `solicitadoPorTercero*: bool`, `fecha*`, `asignadoA` |
| `ResolverExcepcionDto` | `decision*: enum(Aprobar\|Observar\|Rechazar\|MantenerAutomatica)`, `motivos`, `comentario` |
| `EstudioCumplimientoDto` | `expedienteId*`, `verificadoEnListas*: bool`, `fechaProximaConsulta`, `conflictoDeInteres*: bool`, `debidaDiligencia*: enum(SinNovedad\|Descartada\|Monitoreo\|Intensificada)`, `nivelRiesgo*: int (1-3)`, `puntajeMatriz`, `observaciones`, `evidencias*: {documentoId, version}[]`, `vigenteHasta*`, `pep*: bool`, `beneficiariosFinales*: {identificacion, nombre, participacion, verificadoEnListas}[]` |
| `CrearLoteDto` | `nombre`, `archivo: ArchivoDeLoteDto {nombre*, contenidoBase64*}` **o** `filas: FilaDeLoteEntradaDto[]` (≤ 10 000; `pais`, `tipoIdentificacion*`, `numeroIdentificacion*`, `digitoVerificacion`, `tipoPersona`, `nombre*`, `nombreComercial`, `correo*`, `tipoVinculo*`, `flujo`, `contacto`, `telefono`), `vigenciaHoras` (168). La empresa sale de `X-Empresa` (LT-6, LT-7) |
| `VistaPreviaLoteDto` | `archivo`, `total*`, `validas*`, `invalidas*`, `duplicadas*`, `filas*: {n*, datos*, estado*: enum(Valida\|Invalida\|Duplicada), errores*: ErrorDeFilaDto[] {campo, codigo*, mensaje*}, duplicadaDe, accionPrevista: enum(Invitado\|Reinvitado\|SinCambios)}[]` |
| `LoteDto` | `id*`, `nombre*`, `archivo`, `estado*: enum(Procesando\|Pausado\|Completado\|CompletadoConErrores\|Cancelado)`, `totales*: {filas, procesadas, pendientes, exitosas, fallidas, duplicadas, invalidas}`, `vigenciaHoras*`, `creadoPor*`, `creadoEn*`, `actualizadoEn*`, `terminadoEn`, `reintentos*`, `version*` |
| `FilaLoteDto` | `n*`, `identificacion*`, `nombre`, `correo`, `tipoVinculo`, `estado*: enum(Pendiente\|Exitosa\|Fallida\|Duplicada\|Invalida\|Cancelada)`, `accion: enum(Invitado\|Reinvitado\|SinCambios)`, `detalle`, `errores*`, `duplicadaDe`, `terceroId`, `vinculoId`, `terceroCreado*`, `intentos*`, `procesadaEn` |
| `CampaniaDto` | `id*`, `nombre*`, `tipoDocumentoTenantId*`, `empresaId*`, `ventana*: {desde, hasta}`, `recordatorios*: int[]`, `estado*`, `totales*: {convocados, renovados, pendientes}` |
| `PlanDto` | `codigo*`, `nombre*`, `limites*: {usuarios, vinculosActivos, gb, decisionesIaMes, webhooks}`, `accionAlAgotar*: enum(DegradarAManual\|Continuar)` (administrado por Plataforma; `Continuar` factura excedente, I-23), `consumo*: UsoTenantDto`, `vigenteHasta` |
| `DominioCorreoDto` | `dominio*`, `estado*: enum(Pendiente\|Verificado\|Fallido)`, `registrosDns*: {tipo, nombre, valor}[]`, `verificadoEn`, `remitente*` |
| `MatrizRiesgoDto` | `version*`, `factores*: {factor: enum(Pais\|Actividad\|Pep\|Monto\|TipoPersona), valores: {valor, puntos}[]}[]`, `umbrales*: {bajo, medio, alto}`, `accionPorNivel*: {nivel, debidaDiligencia}[]` |
| `LoteImportacionDto` | **Cuerpo** de `POST /importacion/{entidad}`: `fuente*: enum(Portal\|SRM)`, `filas*: json[]` (cada fila con `claveLegado*`), `modo*: enum(DryRun\|Aplicar)`. Tenant y empresa salen de la credencial y de `X-Empresa` (#16) |
| `ImportacionLoteDto` | **Respuesta**: `id*`, `entidad*`, `estado*`, `totales*: {filas, insertadas, actualizadas, rechazadas}`, `rechazosUrl`, `inicio*`, `fin` |
| `RechazoImportacionDto` | `claveLegado*`, `fila*`, `codigo*`, `mensaje*`, `datos: json` |
| `BinarioImportadoDto` | `claveLegado*`, `sha256*`, `bucket*`, `clave*`, `mime*`, `tamano*`, `nombreOriginal` |
| `AceptacionImportadaDto` | Fila de `importacion/expedientes` para aceptaciones históricas: `claveLegado*`, `vinculoClaveLegado*`, `tipoCanonico*`, `documentoClaveLegado*`, `versionSha256*`, `aceptadaEn*`, `aceptadaPor*`, `vigenteHasta`, `esActual*: bool` (si `false`, nace `Desactualizada`), `plazoRevalidacionDias*: int` (default **90** para importadas divergentes, B-8; nunca bloquean antes) |
| `RelacionImportadaDto` | Fila de `importacion/relaciones`: `claveLegado*`, `terceroClaveLegado*`, `personaIdentificacion*: IdentificacionDto`, `personaNombre*`, `tipo*`, `principal*`, `vigenteDesde*`, `cedulaSha256` (si el legado tenía la cédula, se importa a la bóveda de la persona) |
| `ExpedienteImportadoDto` | `claveLegado*`, `vinculoClaveLegado*`, `tipo*`, `estadoLegado*: int`, `estado*: EstadoExpediente`, `abiertoEn*`, `cerradoEn`, `veredictos*: {area, usuario, veredicto, fecha, motivos}[]`, `aceptaciones*: AceptacionImportadaDto[]` |
| `PlantillaDto` | `tipoDocumentoTenantId*`, `version*`, `nombreArchivo*`, `sha256*`, `url*` (prefirmada), `vigente*: bool` |
| `InformeCumplimientoDto` | `periodo*`, `verificaciones*: {tipo, proveedor, total, conCoincidencias, confirmadas}[]`, `riesgo*: {nivel, cantidad}[]`, `estudiosVencidos*`, `pendientesReconsulta*` |
| `DeadLetterDto` | `id*`, `cola*`, `mensaje*`, `error*`, `intentos*`, `fecha*` |
| `RebuildDto` | `proyeccion*`, `estado*`, `progreso*`, `inicio*` |
| `RegistroAuditoriaDto` | `id*`, `tipo*: enum(Dominio\|Configuracion\|Identidad\|Acceso)`, `fecha*`, `tenantId*`, `empresaId`, `actor*: {tipo: enum(Usuario\|Tercero\|Integracion\|Sistema\|Plataforma), id, nombre, ip, userAgent}`, `entidad*: {tipo, id, nombre}`, `accion*` (`Creado\|Modificado\|Publicado\|Archivado\|Eliminado\|Rotado\|Revocado\|Login\|LoginFallido\|DosFactoresActivado\|Bloqueado\|ApiKeyUsada\|TokenErpCanjeado\|EnlaceCanjeado\|…`), `antes: json`, `despues: json`, `diff: DiffCampoDto[]`, `justificacion`, `correlationId*`, `traceId` |
| `DiffCampoDto` | `campo*` (ruta JSON), `antes: json`, `despues: json`, `tipoCambio*: enum(Agregado\|Modificado\|Eliminado)` |
| `ExportarAuditoriaDto` | `tipos*: string[]`, `desde*`, `hasta*`, `empresaId`, `entidad`, `formato*: enum(Jsonl\|Csv)` |
| `ReglaSeleccionFlujoDto` | `id*`, `empresaId*`, `orden*: int`, `nombre*`, `condicion*: CondicionDto` (atributos `tercero.*`, `vinculo.*` incl. `tipo`, `categorias`, `montoEstimado`, `nivelRiesgo`), `flujoId*`, `activa*` ; la respuesta de `GET` incluye además `porDefecto*: {tipoVinculoId, flujoId}[]` |
| `CambiarFlujoDto` | `flujoVersionId*`, `justificacion*` |
| `AplicarVersionDto` | `filtro: {estados: EstadoExpediente[], flujoVersionAnterior, empresaId, desde}`, `justificacion*` |
| `ImpactoAplicacionDto` | `expedientesAfectados*: int`, `detalle*: {expedienteId, folio, requisitosAgregados: string[], requisitosQuitados: string[], veredictosConservados: int}[]`, `simulacion*: bool` |
| `CertificadoContenidoDto` | **Implementado (§2.6.1).** Contenido **canónico** que se hashea (ver [03 §7.1](03-dominio-y-eventos.md)): `esquema*: "validasoft.certificado.v1"`, `folio*`, `emision*`, `empresa*: {id, nombre, identificacion}`, `tercero*: {nombre, identificacionEnmascarada, pais, tipoPersona}`, `tipoVinculo*`, `flujo*: {codigo, version}`, `requisitos*: {codigo, versionN, sha256 \| hashDatos, vigenteHasta, eximido, justificacion}[]` (ordenados por `codigo`), `verificaciones*: {tipo, proveedor, resultado, fecha}[]`, `areas*: {area, origen, fecha}[]`, `emitidoEn*`, `vigenteHasta`; **excluye** contactos, direcciones, URLs de descarga y nombres de revisores |

---

## 5. Ejemplos de payload

### 5.1 El SRM invita a un proveedor

```http
POST /api/v1/expedientes
X-Api-Key: vsk_live_acme_7f3a…
X-Empresa: 01J9E…A1
Idempotency-Key: srm-solicitud-00123-invitar

{
  "tercero": { "identificacion": { "pais": "CO", "tipo": "NIT", "numero": "900123456", "digitoVerificacion": "7" },
               "tipoPersona": "Juridica", "nombre": "Ferretería El Tornillo SAS",
               "contactos": [ { "nombre": "María Pérez", "correo": "maria@eltornillo.co", "rol": "Representante" } ] },
  "vinculo": { "tipo": "proveedor", "atributos": { "categorias": ["ferreteria"], "montoEstimado": 250000000, "nivelRiesgo": "Medio" } },
  "flujo": { "codigo": "habilitacion-proveedores", "version": 3 },
  "referenciaExterna": { "sistema": "SRM", "id": "ADP-00123" },
  "invitacion": { "canal": "Correo", "vigenciaHoras": 72, "requiereOtp": false },
  "plazoCargueDias": 30
}
```

```http
HTTP/1.1 201 Created
ETag: "v2"
{ "id": "01J9ZK3M8Q…", "folio": "VS-ACME1-E-2026-000418", "empresaId": "01J9E…A1", "vinculoId": "01J9ZK3M7A…", "terceroId": "01J9ZK2P…",
  "tipo": "Inicial", "estado": "Abierto", "flujo": { "codigo": "habilitacion-proveedores", "version": 3 }, "origenFlujo": "Explicito",
  "plazoCargueHasta": "2026-11-27T04:59:59Z", "invitacionAceptadaEn": null, "requisitosCubribles": 1,
  "enlaceTercero": { "url": "https://valida.acme.co/e/eyJ…", "expira": "2026-10-31T13:20:00Z" },
  "requisitos": [
    { "id": "01J9…R1", "tipo": { "codigo": "RUT", "clase": "Archivo", "ambito": "Global" }, "obligatorio": true, "critico": true,
      "estado": "Pendiente", "nivelRevision": "Automatico" },
    { "id": "01J9…R2", "tipo": { "codigo": "CAMARA_COMERCIO" }, "obligatorio": true, "critico": true, "estado": "Pendiente",
      "reglaFrescura": { "tipo": "MaxDiasDesdeExpedicion", "dias": 30 } },
    { "id": "01J9…R5", "tipo": { "codigo": "CEDULA" }, "dePersonaRelacionada": { "rol": "RepresentanteLegal" }, "obligatorio": true, "estado": "Pendiente" },
    { "id": "01J9…R3", "tipo": { "codigo": "CUENTA_BANCARIA", "clase": "Formulario" }, "obligatorio": true, "aprobadoresRequeridos": 2, "estado": "Pendiente" },
    { "id": "01J9…R4", "tipo": { "codigo": "FORMATO_VINCULACION_ACME", "ambito": "PorVinculo" }, "obligatorio": true, "estado": "Pendiente",
      "ayuda": { "plantillaUrl": "https://…/plantillas/…" } } ] }
```

El tercero ya tenía un RUT en su bóveda (`requisitosCubribles: 1`), pero la empresa **no ve** `versionActual` hasta que el tercero acepte la invitación (`POST /public/v1/invitaciones/{expedienteId}/aceptar`, B-1); en ese momento llega `documento.compartido` y R1 pasa a `Cargado`.

### 5.2 Subida en dos pasos

```http
POST /api/v1/expedientes/01J9ZK3M8Q…/requisitos/01J9…R2/documentos
Authorization: Bearer <sesión del tercero>
Idempotency-Key: tercero-r2-2026-10-28T13:25:00Z
{ "nombreArchivo": "camara-oct-2026.pdf", "tamanoBytes": 482133, "tipoMime": "application/pdf", "sha256": "9f2c7e…",
  "datosDeclarados": { "fechaExpedicion": "2026-10-25" } }
→ 201 { "documentoId": "01J9…D7", "version": 3, "duplicado": false,
        "carga": { "metodo": "PUT", "url": "https://minio…/boveda/terceros/01J9ZK2P…/9f2c7e…?X-Amz-…",
                   "cabeceras": { "Content-Type": "application/pdf", "x-amz-checksum-sha256": "nyx+…" }, "expira": "2026-10-28T13:40:00Z" } }

POST /api/v1/documentos/01J9…D7/versiones/3/confirmar
Idempotency-Key: tercero-r2-confirmar-3
→ 202 { "estado": "EnAnalisis", "stream": "/public/v1/expedientes/01J9ZK3M8Q…/stream" }
```

Por SSE llegan `documento.hechos_registrados` y luego `documento.version_nueva` (promovida; arranca la revalidación en todas las empresas que exigen el tipo) o `documento.version_devuelta`.

### 5.3 Formulario bancario con cuenta designada por el tercero

```http
POST /public/v1/boveda/cuentas-bancarias
{ "pais": "CO", "banco": "Bancolombia", "tipoCuenta": "Ahorros", "numero": "031-123456-78", "titular": "Ferretería El Tornillo SAS",
  "identificacionTitular": { "pais": "CO", "tipo": "NIT", "numero": "900123456" }, "moneda": "COP",
  "certificacion": { "documentoId": "01J9…C1", "version": 1 } }
→ 201 { "id": "01JB…CT", "estado": "Activa", "certificacion": { "estadoHechos": "Actual", "identidadCoincide": true } }

POST /public/v1/vinculos/01J9ZK3M7A…/cuenta-designada
{ "cuentaId": "01JB…CT" }
→ 202 { "cuentaId": "01JB…CT", "estado": "Propuesta", "propuestaEn": "2026-10-28T14:00:00Z", "aprobadaPor": [] }
```

La empresa aprueba con dos usuarios distintos en `POST /api/v1/vinculos/{id}/cuenta-designada/aprobar`; al segundo, `vinculo.cuenta_designada` viaja al ERP.

### 5.4 Veredicto de área

```http
POST /api/v1/expedientes/01J9ZK3M8Q…/requisitos/01J9…R2/veredicto
If-Match: "v14"   Idempotency-Key: ui-9c2e-veredicto-r2
{ "area": "legal", "veredicto": "Observado", "sobreVersion": 3, "motivos": ["incompleto"], "camposAfectados": ["objetoSocial"],
  "observacion": "Falta la página 2 con el objeto social." }
→ 200  ETag: "v15"
{ "requisito": { "id": "01J9…R2", "estado": "Observado", "veredictos": [ { "area": "legal", "veredicto": "Observado", "origen": "Humano",
    "usuario": "ana@acme.co", "numeroAprobador": 1, "fecha": "2026-10-28T13:41:02Z", "sobreVersion": 3 } ] },
  "expediente": { "estado": "Observado", "etapaActual": 1 }, "notificaciones": { "tercero": "enviada", "webhook": "encolado" } }
```

### 5.5 Revalidación (empresa B, nivel Asistido)

```http
GET /api/v1/revalidaciones?estado=Pendiente
X-Empresa: 01J9E…B1
→ 200 { "datos": [ { "empresaId": "01J9E…B1", "vinculoId": "01J7…", "expedienteId": "01JA…",
    "tercero": { "id": "01J9ZK2P…", "nombre": "Ferretería El Tornillo SAS" }, "tipoDocumento": "RUT",
    "versionAceptada": { "n": 2, "fecha": "2026-03-02T15:10:00Z" }, "versionActual": { "n": 3, "fecha": "2026-10-28T13:30:00Z" },
    "comparacion": { "resultado": "CambiosDetectados", "camposCambiados": ["direccion"], "hayCriticos": false },
    "nivelRevision": "Asistido", "hechosDisponibles": true,
    "decisionAutomatica": { "veredicto": "Aprobado", "razon": "Solo cambió un campo no crítico (direccion)." },
    "plazoHasta": "2026-11-12", "diasRestantes": 15, "bloqueante": false } ],
  "siguienteCursor": null }
```

La fila sigue exactamente `RevalidacionDto` (§4); `bloqueante` pasa a `true` cuando vence `plazoHasta` sin revalidar (A-2).

### 5.6 Consulta de certificación (Marco)

```http
GET /api/v1/consultas/certificacion?pais=CO&tipoIdentificacion=NIT&numero=900123456&tipoVinculo=proveedor
X-Api-Key: vsk_live_acme_…   X-Empresa: 01J9E…A1   If-None-Match: "c-01JA…-7"
→ 200  ETag: "c-5a7e0c1d9f22…"  Cache-Control: private, max-age=300
{ "certificado": true, "folio": "VS-ACME1-C-2026-000418-2", "estado": "ConPendientes", "estadoVinculo": "Activo",
  "vinculoId": "01J9ZK3M7A…", "terceroId": "01J9ZK2P…", "vigenteHasta": "2027-04-30",
  "revalidacionPendiente": [ { "tipo": "RUT", "plazo": "2026-11-12", "bloqueante": false } ], "documentosVencidos": [],
  "bloqueado": false, "motivoBloqueo": null,
  "cuentaDesignada": { "numeroEnmascarado": "****6578", "banco": "Bancolombia" },
  "urlVerificacion": "https://valida.acme.co/verificar/VS-ACME1-C-2026-000418",
  "legacy": { "estadoTercero": 3 }, "fuenteRespaldo": false }
```

Sin vínculo: `{ "certificado": false, "estadoVinculo": "SinVinculo", "bloqueado": false, "fuenteRespaldo": false }`. Marco, al servir desde su tabla local, devuelve el mismo contrato con `fuenteRespaldo: true` y `antiguedadSegundos` ([08 §5](08-integracion-y-migracion.md)).

---

## 6. Webhooks

### 6.1 Entrega

Cabeceras: `Validasoft-Event`, `Validasoft-Delivery` (id de **intento**; cambia en cada reintento), `Validasoft-Event-Id` (id del evento; **única clave válida para dedupe**, T-6), `Validasoft-Timestamp`, `Validasoft-Signature: v1=…[,v1=…]`, `X-Correlation-Id`. Firma `HMAC-SHA256(secreto, "{timestamp}.{cuerpo}")` hex; tolerancia 300 s. Respuesta `2xx` en < 10 s. Reintentos 1 m, 5 m, 30 m, 2 h, 12 h, 24 h, 24 h ⇒ `Fallida` + reenvío manual + alerta. Orden garantizado solo por `secuencia` dentro del agregado. Suscripción deshabilitada tras 7 días de fallos. Destinos solo `https`; sin IPs privadas.

### 6.2 Sobre común (`EventoPublicoDto`)

```json
{ "id": "01JA4Q7…", "tipo": "certificado.emitido", "version": 1, "ocurridoEn": "2026-10-28T14:03:11Z",
  "tenant": "acme", "empresaId": "01J9E…A1", "secuencia": 42,
  "agregado": { "tipo": "Expediente", "id": "01J9ZK3M8Q…" }, "actor": { "tipo": "Sistema" },
  "referenciaExterna": { "sistema": "SRM", "id": "ADP-00123" }, "legacy": { "estadoTercero": 3 }, "datos": { } }
```

`legacy.estadoTercero` solo en coexistencia ([08](08-integracion-y-migracion.md)). `tenant` es el **slug** del tenant (no un id). `empresaId` siempre presente en eventos de empresa; `null` en eventos de tenant (`empresa.creada`, `flujo.publicado`).

### 6.3 `certificado.emitido` (canónico; sustituye al antiguo `expediente.certificado`, T-15)

```json
{ "tipo": "certificado.emitido", "empresaId": "01J9E…A1", "datos": {
    "expedienteId": "01J9ZK3M8Q…", "expedienteFolio": "VS-ACME1-E-2026-000418", "vinculoId": "01J9ZK3M7A…", "tipoVinculo": "proveedor", "tipoExpediente": "Inicial",
    "tercero": { "id": "01J9ZK2P…", "identificacion": { "pais": "CO", "tipo": "NIT", "numero": "900123456", "digitoVerificacion": "7" }, "nombre": "Ferretería El Tornillo SAS" },
    "certificado": { "folio": "VS-ACME1-C-2026-000418-1", "emision": 1, "estado": "Vigente", "hash": "sha256:5a7e…", "vigenteHasta": "2027-04-30", "urlVerificacion": "https://valida.acme.co/verificar/VS-ACME1-C-2026-000418" },
    "versionesAceptadas": [ { "tipo": "RUT", "documentoId": "01J8…", "version": 2, "vigenteHasta": null },
                            { "tipo": "CAMARA_COMERCIO", "documentoId": "01J9…D7", "version": 3, "vigenteHasta": "2026-11-24", "motivoVigencia": "ReglaFrescura" } ],
    "cuentaDesignada": { "cuentaId": "01JB…CT", "numeroEnmascarado": "****6578", "banco": "Bancolombia", "tipoCuenta": "Ahorros" },
    "cambios": [ { "campo": "representanteLegal.principal", "anterior": null, "nuevo": { "identificacion": "CC 79…", "nombre": "Carlos Ruiz" }, "aplicaEnErp": true } ],
    "verificaciones": [ { "tipo": "ListasRestrictivas", "proveedor": "Stradata", "resultado": "SinCoincidencias", "fecha": "2026-10-27" } ] } }
```

`cambios[]` (también en `vinculo.revalidado`) lista los datos maestros que el ERP debe aplicar (razón social, representante principal, cuenta designada); `aplicaEnErp` lo decide la configuración de la empresa.

### 6.4 `documento.version_nueva` (arranca la revalidación)

```json
{ "tipo": "documento.version_nueva", "empresaId": "01J9E…A1", "actor": { "tipo": "Tercero", "id": "01J9ZK2P…" }, "legacy": { "estadoTercero": 4 },
  "datos": { "terceroId": "01J9ZK2P…", "documentoId": "01J9…D7", "tipo": "CAMARA_COMERCIO", "clase": "Archivo", "ambito": "Global",
    "versionNueva": 3, "versionAnterior": 2,
    "hechos": { "conIa": true, "fechaExpedicion": "2026-10-25", "comparacion": "CambiosDetectados", "camposCambiados": ["representanteLegal"], "camposCriticosCambiados": ["representanteLegal"], "confianza": 0.95 },
    "efectoEnEmpresa": { "vinculoId": "01J9ZK3M7A…", "requiereRevalidacion": true, "aceptacionAnterior": { "version": 2, "estado": "Desactualizada" },
      "expedienteRevalidacionId": "01JA…", "plazoRevalidacion": "2026-11-12", "sugerenciaAutomatica": null, "requiereHumano": true,
      "razon": "Cambió un campo crítico: representanteLegal", "verificacionesExpiradas": [ { "tipo": "CEDULA", "persona": "representante saliente" }, { "tipo": "ListasRestrictivas", "persona": "representante saliente" } ] } } }
```

Los `hechos` son globales (la bóveda solo emite `camposCambiados` y `camposCriticosCambiados` según el canónico, T-7); `requiereRevalidacion` vive en `efectoEnEmpresa` porque lo decide la saga de **cada** tenant (críticos canónicos **o** `camposCuyoCambioExigeHumano` propios). Cada empresa recibe **su** copia; nunca ve efectos en otras empresas (ni del mismo tenant).

### 6.5 Estado de implementación — fase «Notificaciones y webhooks» (NW, 2026-11-08)

Decisiones NW-1…NW-12 del [registro](decisiones/registro.md). Lo que sigue es el **contrato vigente**; donde difiere de §6.1–§6.4, §7 u §8, manda esta sección (las diferencias se listan al final).

**Origen de los eventos públicos (NW-3).** Una suscripción de Marten (`eventos-publicos`, daemon asíncrono en modo *hot/cold*) lee el event store en orden y, por cada evento de dominio del catálogo, registra un `EventoPublico` (documento global con su tenant), crea una entrega por cada suscripción de webhook que lo acepta y planea las notificaciones a personas. Todo eso, los mensajes de envío y el avance de la suscripción se confirman **en una transacción** (outbox de Wolverine): no se pierden ni se duplican. El **cursor** es la secuencia global del evento de dominio de origen: monotónico por tenant, con huecos, y siempre un prefijo (el modo pull no se salta ninguno). El `id` del evento público es el del evento de dominio (estable; sirve para deduplicar).

**Sobre** (`EventoPublicoDto`, el mismo JSON en el webhook, en `GET /eventos` y en el SSE):

```json
{ "id": "0196…", "tipo": "expediente.observado", "version": 1, "ocurridoEn": "2026-11-08T15:03:11.123456+00:00",
  "tenantId": "0196…", "empresaId": "0196…", "cursor": "4211",
  "datos": { "expedienteId": "0196…", "folio": "NORTE-E-2026-000001", "vinculoId": "0196…",
             "referencias": [ { "sistema": "SRM", "id": "ADP-00123" } ], "tipoExpediente": "Inicial", "estado": "Observado",
             "causa": null, "requisitos": [ { "requisitoId": "0196…", "codigo": "RUT" } ], "motivos": null, "justificacion": null,
             "terceroId": null, "tipoVinculo": null } }
```

**Catálogo v1 implementado** (`GET /api/v1/eventos/tipos` → `TipoEventoPublicoDto[] { tipo, descripcion }`). Todos los `datos` llevan `vinculoId` y `referencias[] { sistema, id }` (una por sistema integrado que comparte el vínculo) para que cada sistema ubique su registro; nunca llevan revisores, áreas, correos, IP, nombres de personas ni URLs de binarios.

| Tipo | Evento de dominio | `datos` |
|---|---|---|
| `vinculo.creado` | `VinculoCreado` | `DatosVinculoCreadoDto { vinculoId, referencias, terceroId, identificacion { pais, tipo, numero, digitoVerificacion }, tipoVinculo (código), flujoId, origen, estado: "Invitado" }` |
| `vinculo.invitacion_enviada` | `InvitacionEnviada` | `DatosInvitacionEnviadaDto { vinculoId, referencias, canal, expira }` (sin el correo) |
| `vinculo.invitacion_aceptada` | `InvitacionAceptada` | `DatosInvitacionAceptadaDto { vinculoId, referencias, aceptadaEn, consentimientos[] { tipo, version } }` (sin la evidencia) |
| `vinculo.activado` · `vinculo.bloqueado` · `vinculo.desbloqueado` · `vinculo.desvinculado` · `vinculo.anulado` | `VinculoActivado`, `VinculoBloqueado`, `VinculoDesbloqueado`, `VinculoDesvinculado`, `VinculoAnulado` | `DatosEstadoVinculoDto { vinculoId, referencias, estado, estadoAnterior?, motivo? (o justificación del desbloqueo), hasta?, expedienteId? (activado) }` |
| `vinculo.referencia_agregada` | `ReferenciaExternaAgregada` | `DatosReferenciaAgregadaDto { vinculoId, referencias, referencia { sistema, id } }` |
| `expediente.abierto` · `expediente.en_revision` · `expediente.observado` · `expediente.reenviado` · `expediente.aprobado` · `expediente.rechazado` | `ExpedienteAbierto`; `ExpedienteEnviadoARevision` (`causa: CargaCompleta`) o `RevisionReanudada` (`causa: RevisionReanudada`); `ExpedienteObservado`; `ExpedienteReenviado` (`causa: Tercero\|Integrador`); `ExpedienteAprobado`; `ExpedienteRechazado` | `DatosExpedienteDto { expedienteId, folio, vinculoId, referencias, tipoExpediente, estado (tras el evento), causa?, requisitos? [{ requisitoId, codigo }] (observados o reabiertos), motivos? [{ codigo, texto }] (rechazo, texto tal como se comunicó al tercero), justificacion? (rechazo), terceroId? y tipoVinculo? (abierto) }` |
| `documento.compartido` · `documento.por_vencer` · `documento.vencido` | `VersionCompartida`; `VencimientoAvisado` (nuevo evento del stream del documento, uno por documento y vínculo en el aviso consolidado del día, TD-7); `VersionVencida` | `DatosDocumentoDto { documentoId, terceroId, tipo (canónico o código del tipo del tenant), version, vinculoId, referencias, causa? (compartido: InvitacionAceptada\|VersionNueva\|Importada), vigenteHasta?, diasRestantes? (por vencer), diasGracia? (vencido) }` |
| `certificado.emitido` · `certificado.reemplazado` · `certificado.suspendido` · `certificado.restablecido` · `certificado.revocado` · `certificado.vencido` | `CertificadoEmitido`; `CertificadoReemplazado` (emisión anterior); `CertificadoSuspendido`/`Restablecido` (bloqueo y desbloqueo, B-5); `CertificadoRevocado`; `CertificadoVencido` (NW-13) | `DatosCertificadoDto { certificadoId, vinculoId, referencias, terceroId, certificado { folio, emision, estado (tras el evento), hash (SHA-256 hex), vigenteHasta?, urlVerificacion }, estadoAnterior?, motivo? (suspendido, restablecido, revocado, reemplazado), expedienteId? y expedienteFolio? (emitido), folioReemplazo? (reemplazado) }`. Versión reducida de §6.3: `versionesAceptadas`, `cuentaDesignada`, `cambios` y `verificaciones` llegan con H6–H7 |
| `expediente.verificacion_registrada` · `expediente.coincidencia_resuelta` · `expediente.verificacion_expirada` | `VerificacionExternaRegistrada` y `EstudioDeCumplimientoRegistrado`; `CoincidenciaResuelta`; `VerificacionExpirada` (H7, VE-13) | `DatosVerificacionDto { expedienteId, folio, vinculoId, referencias, tipo, proveedor?, resultado?, estado, bloqueante, vigenteHasta?, coincidencias, coincidenciasPorAnalizar, decision? (resuelta), nivelRiesgo? (estudio) }`: sin nombres de personas, listas ni justificaciones |
| `seccion.aceptada` | `SeccionAceptadaPorEmpresa` (stream del expediente, al aprobarse; uno por sección no eximida) | `DatosSeccionAceptadaDto { vinculoId, referencias, terceroId, empresaId, seccion, periodo? (financiera), version, aceptadaEn, expedienteId, folio, huella, contenido }` (§6.6) |

**Desaprobar y reintegrar (DS-7).** `expediente.desaprobado` (`EtapaReabierta` o `ExpedienteDesaprobado`; `DatosExpedienteDto` con `estado: EnRevision`, `causa` = `EtapaReabierta`, `FaseInicial` o `ExpedienteAprobado`, `justificacion` y `etapa`) y `vinculo.reintegrado` (`VinculoReintegrado`, `DatosEstadoVinculoDto` con el estado resultante y `estadoAnterior: Desvinculado`) se suman al catálogo; antes de DS el catálogo de §7 listaba `vinculo.reintegrado` pero no se publicaba.

Agregar tipos es registrar otro `IMapeadorDeEventos` en DI (NW-2); su catálogo se suma solo a la validación de suscripciones y a `GET /eventos/tipos`.

**Modo pull y SSE** (`eventos:leer`; el tenant sale de la credencial; con `X-Empresa` solo esa empresa; si no, las empresas de la credencial o todas si tiene `*`):

| Ruta | Contrato |
|---|---|
| `GET /api/v1/eventos?desde={cursor}&tipos=a,b&limite=50` | `PaginaEventosDto { datos: EventoPublicoDto[], cursor, hayMas }` en orden de cursor (máx. 200). `cursor` = el último devuelto (o el mismo `desde` si no hubo nada): se pasa tal cual en la siguiente consulta. Sin `desde` empieza por el primero. 422 `cursor-invalido`. |
| `GET /api/v1/eventos/stream?desde=&tipos=` | SSE (§3.4): `id` = cursor (reanuda con `Last-Event-ID`), `event` = tipo, `data` = `EventoPublicoDto`. Consulta cada 2 s; comentario de vida cada 15 s. |
| `GET /api/v1/eventos/tipos` | Catálogo (cualquier credencial). |

**Webhooks** (`webhooks:administrar`; una llave de un sistema integrado solo ve y gestiona las suscripciones de su sistema —las demás responden 404—; una llave sin sistema ve todas e indica `sistemaId` al crear):

| Ruta | Contrato |
|---|---|
| `POST /api/v1/webhooks` | `CrearWebhookDto { url, tipos[] ("*" = todos), empresas[]? (ids o "*"; nulo = todas las del alcance), sistemaId? (solo con llave sin sistema), descripcion? }` → **201** `WebhookDto` con `secreto` (`whsec_…`, 256 bits; **se muestra una sola vez**). 422 `url-invalida`, `url-no-permitida` (no https, IP privada/local/reservada o `localhost`; `http` solo hacia `localhost`/`127.0.0.1`/`[::1]` con `Validasoft:Webhooks:PermitirHttpLocal`, desarrollo), `tipos-requeridos`, `tipos-invalidos`, `empresas-invalidas`, `empresas-fuera-de-alcance` (alcance = empresas pedidas ∩ las de la credencial; `*` con una credencial limitada se reduce a sus empresas), `sistema-requerido`, `sistema-invalido`. Esta ruta y `rotar-secreto` **no guardan respuesta de idempotencia** (dejaría el secreto en claro 24 h): un POST repetido crea otra suscripción. |
| `GET /api/v1/webhooks` · `GET /api/v1/webhooks/{id}` | `WebhookDto { id, sistemaId, sistema, url, descripcion, tipos[], empresas[], version, activa, fallosSeguidos, deshabilitadaEn?, causaDeshabilitacion? (fallos-consecutivos\|manual), creadaEn, ultimaEntregaEn?, secretoAnteriorVence?, secreto: null }`. 404 `webhook-no-encontrado`. |
| `PUT /api/v1/webhooks/{id}` | `EditarWebhookDto { url?, tipos?, empresas?, descripcion? }` (nulo = sin cambio), mismas validaciones. |
| `POST /api/v1/webhooks/{id}/rotar-secreto` | 200 `WebhookDto` con el `secreto` nuevo; el anterior sigue firmando hasta `secretoAnteriorVence` (`Validasoft:Webhooks:HorasSolapamientoRotacion`, 24 h). |
| `POST /api/v1/webhooks/{id}/activar` · `POST …/desactivar` | 200 `WebhookDto`. Activar reinicia `fallosSeguidos`; 409 `webhook-inactivo` al desactivar una inactiva. |
| `GET /api/v1/webhooks/{id}/entregas?estado=&limite=50&cursor=` | `PaginaDto<EntregaWebhookDto>`, más recientes primero. `EntregaWebhookDto { id, webhookId, eventoId, tipoEvento, cursor, empresaId, estado (Pendiente\|Entregada\|Reintentando\|Fallida\|Cancelada), intentos, creadaEn, entregadaEn?, proximoIntentoEn?, historial[] IntentoEntregaDto { numero, intentoId, fecha, codigoHttp?, duracionMs, resultado (Exitoso\|Fallido), error? (HTTP nnn, timeout, error-de-conexion, destino-no-permitido), manual } }`. |
| `GET /api/v1/webhooks/{id}/entregas/{entregaId}` | `EntregaWebhookDto`. 404 `entrega-no-encontrada`. |
| `POST /api/v1/webhooks/{id}/entregas/{entregaId}/reenviar` | **202** `EntregaWebhookDto` (`estado: Pendiente`): un intento manual más, ahora, aunque la entrega esté entregada, fallida o cancelada y aunque la suscripción esté desactivada; si falla no programa reintentos. |

Todas las altas y cambios quedan en la auditoría de configuración (entidad `Webhook`; el secreto nunca se audita, solo `secretoAnteriorVence`). El secreto se guarda cifrado (AES-256-GCM con la clave derivada de `Validasoft:Sesiones:ClaveFirma`, como IN-3): **rotar esa clave invalida los secretos de webhook** (hay que rotarlos); la clave por tenant de docs/07 §5 sigue pendiente.

**Entrega** (NW-5): `POST` a la URL con `Content-Type: application/json`, `User-Agent: Validasoft-Webhooks/1.0`, el sobre como cuerpo y las cabeceras:

| Cabecera | Valor |
|---|---|
| `Validasoft-Event-Id` | `id` del evento: **la clave de deduplicación** (un reintento o un reenvío manual repiten el mismo). |
| `Validasoft-Event-Type` | `tipo`. |
| `Validasoft-Delivery` | id del **intento** (cambia en cada reintento). |
| `Validasoft-Timestamp` | segundos Unix del envío. |
| `Validasoft-Signature` | `t={timestamp},v1={hex(HMAC-SHA256(secreto, "{timestamp}.{cuerpo}"))}`; durante una rotación, un `v1=` por cada secreto vigente (`t=…,v1=…,v1=…`). El receptor recalcula con su secreto, compara en tiempo constante contra cualquier `v1` y rechaza timestamps a más de 5 min. `FirmaDeWebhook.Verificar` (dominio) es la referencia. |

Éxito = `2xx` antes del timeout (`Validasoft:Webhooks:TimeoutSegundos`, **5 s**; responde rápido y procesa después). Sin redirecciones ni cookies. Si falla: reintentos con backoff **1 min, 5 min, 30 min, 2 h y 6 h** (`Validasoft:Webhooks:Reintentos`; 6 intentos) como mensajes programados en la misma transacción; luego `Fallida` y queda el reenvío manual. Cada intento fallido suma a `fallosSeguidos` de la suscripción (se reinicia con un éxito); al llegar a **20** (`Validasoft:Webhooks:FallosParaDeshabilitar`) se **deshabilita** (`causa: fallos-consecutivos`), se audita y los administradores del tenant reciben la notificación `webhook.deshabilitado` (campana y correo); sus reintentos pendientes quedan `Cancelada` y los eventos nuevos no generan entregas mientras siga inactiva. **Orden**: la cola `webhooks` es secuencial, así un receptor sano recibe los eventos en el orden del cursor; un reintento llega después de eventos más nuevos, así que el receptor ordena por `cursor` y deduplica por `Validasoft-Event-Id`. **SSRF**: la URL se valida al crear y la IP se vuelve a comprobar **al conectar** (todas las direcciones del DNS deben ser públicas y se conecta a esas mismas: sin DNS rebinding); quedan fuera loopback, RFC 1918, enlace local (incluye `169.254.169.254`), CGNAT, ULA, multicast, documentación y reservadas, también dentro de IPv6 (mapeadas, NAT64, 6to4). `PermitirHttpLocal` admite loopback y `PermitirDestinosPrivados` las redes privadas (solo desarrollo u on-premise).

**Notificaciones a personas** (NW-8…NW-11; las genera la misma suscripción):

| Tipo | Hecho | Quién | Correo |
|---|---|---|---|
| `expediente.nuevo_en_area` «Nuevo expediente en tu área» | `EtapaActivada` (al pasar a revisión o al activarse una etapa siguiente) | miembros activos con rol en la empresa de las áreas de la etapa | sí (consolidado) |
| `expediente.tercero_respondio` «El tercero respondió» | `ExpedienteReenviado` | miembros de las áreas que vuelven a revisar | sí (consolidado) |
| `expediente.aprobado` · `expediente.rechazado` | `ExpedienteAprobado` · `ExpedienteRechazado` | miembros de todas las áreas del flujo | no |
| `vinculo.cuenta_propuesta` «Cuenta bancaria por aprobar» (NT-12) | `CuentaDesignadaPropuesta` | personas activas con `cuentas:aprobar` en la empresa del vínculo (enlace al vínculo) | no |
| `documento.por_vencer` «Documento por vencer» | `VencimientoAvisado` | miembros de las áreas del expediente abierto (o el último) del vínculo | no |
| `webhook.deshabilitado` | deshabilitación por fallos | usuarios activos con rol `Administrador` | sí |

**Consolidación**: una notificación y, si va por correo, **un correo por persona y hecho** aunque esté en varias áreas (el correo nombra sus áreas y enlaza a `{Validasoft:Correo:UrlApp}#/e/{empresa}/expedientes/{id}`; por defecto `UrlApp` = `{UrlPublica}/app/`). Hechos con más de 24 h (`Validasoft:Notificaciones:HorasAntiguedadMaxima`) no notifican a personas (al encender la suscripción sobre una base con historia no salen correos viejos). **Constancia al tercero** (plantilla `consentimiento-empresa`): al registrarse `ConsentimientosRegistrados` sale al correo con el que aceptó «Constancia de tu aceptación con {empresa}» con cada texto (título, versión y huella SHA-256) y la fecha; sin IP ni otros datos de la evidencia.

| Ruta (cualquier credencial de **persona**: identidad delegada o su sesión; una integración sin persona ⇒ 422 `usuario-delegado-requerido`) | Contrato |
|---|---|
| `GET /api/v1/notificaciones?soloNoLeidas=&limite=30&cursor=` | `BandejaNotificacionesDto { datos: NotificacionDto[], siguienteCursor?, noLeidas }`, más recientes primero (máx. 100). `NotificacionDto { id, tipo, titulo, mensaje, empresaId?, expedienteId?, vinculoId?, folio?, enlace? (ruta de apps/web sin host, p. ej. /e/{empresa}/expedientes/{id}), creadaEn, leidaEn? }`. Con `X-Empresa`, solo las de esa empresa y las del tenant (`empresaId: null`). |
| `POST /api/v1/notificaciones/{id}/leida` | 200 `NotificacionDto`; 404 `notificacion-no-encontrada` (también si es de otra persona). |
| `POST /api/v1/notificaciones/leidas` | 200 `NotificacionesMarcadasDto { marcadas }` (las no leídas visibles con el mismo filtro). |
| `GET /api/v1/notificaciones/stream` | SSE: `event: notificacion` (`data` = `NotificacionDto`, `id` = su orden) por cada nueva desde que se conectó (o desde `Last-Event-ID`) y `event: no_leidas` (`data` = el total) cuando cambia, más `event: cambio` (`data` = `CambioEnVivoDto`, §3.4) de la empresa activa (`X-Empresa`). Consulta cada 2 s. |
| `GET /api/v1/usuarios/me/preferencias-notificacion` · `PUT` | `PreferenciasNotificacionDto { correo: { [tipo]: bool }, tiposConCorreo[] }`; el `PUT` (`ActualizarPreferenciasNotificacionDto { correo }`) cambia solo los tipos que envía; un tipo que no va por correo ⇒ 422 `tipo-notificacion-invalido`. Por defecto todos `true`. La campana no se apaga. |

**Permisos**: `eventos:leer` y `webhooks:administrar` se suman a los scopes del tenant y al rol `Administrador` (no al Auditor todavía). Las llaves ya emitidas no los tienen (como BO-9): se crea una llave nueva con ellos.

**Diferencias con §6.1–§8** (y por qué): (1) `Validasoft-Event-Type` en lugar de `Validasoft-Event` y el timestamp también dentro de la firma (`t=`, como el resto de la industria); no se envía `X-Correlation-Id`. (2) El sobre lleva `tenantId` (no el slug `tenant`), `cursor` (no `secuencia`) y las referencias dentro de `datos`; no lleva `agregado`, `actor` ni `legacy` todavía. (3) Nombres del catálogo pedidos por Producto: `vinculo.invitacion_enviada/aceptada` (no `expediente.invitacion_*`), `vinculo.activado` y los de estado por separado (no `vinculo.estado_cambiado`), `expediente.en_revision` (no `enviado_a_revision`) y `expediente.observado` (no `con_observaciones`). (4) Reintentos hasta 6 h (no 24 h) y deshabilitación por **N fallos seguidos** (no 7 días); timeout 5 s (no 10 s). (5) El pull devuelve `PaginaEventosDto` (con el cursor siempre presente) en lugar de `PaginaDto`. (6) La campana vive en `/notificaciones` (no `/novedades`); la auditoría de envíos que §2.7 ponía en `GET /notificaciones/{id}/entregas` queda pendiente con otro nombre. (7) Preferencias solo de correo por tipo (sin digest).

**Pendiente**: `POST /webhooks/{id}/probar` (`prueba.ping`), `DELETE /webhooks/{id}`, el evento público `webhook.deshabilitado` y la deshabilitación por `empresas-fuera-de-alcance` al cambiar una API key; `certificado.con_pendientes` (H6); `legacy.estadoTercero`; Data Protection por tenant para los secretos; evidencia de entrega SMTP (`EntregasNotificacion`); plantillas editables por tenant (Fluid); digest diario; avisos a revisores de observación automática, SLA y revalidación; partición de la cola `webhooks` por suscripción si un receptor lento llega a frenar a los demás.

---

### 6.6 `seccion.aceptada` (F3, FM-32…FM-36)

Sale cuando una empresa **acepta** una versión de una sección de la ficha del tercero: al aprobarse el expediente (inicial, renovación o la actualización de esa sección), uno por sección no eximida, después de `expediente.aprobado`. Lo publica `MapeadorDeLaFicha` desde el evento de dominio `SeccionAceptadaPorEmpresa`. La versión es inmutable, así que un reproceso publica lo mismo; solo los nombres de los catálogos se leen al mapear. Lo que la empresa observa no lo emite (FM-3).

```json
{ "id": "0196…", "tipo": "seccion.aceptada", "version": 1, "ocurridoEn": "2026-11-09T21:10:03.120000+00:00",
  "tenantId": "0196…", "empresaId": "0196…", "cursor": "5120",
  "datos": { "vinculoId": "0196…", "referencias": [ { "sistema": "SRM", "id": "ADP-00123" } ], "terceroId": "0196…", "empresaId": "0196…",
             "seccion": "FICHA_TRIBUTARIA", "periodo": null, "version": 3, "aceptadaEn": "2026-11-09T21:10:03+00:00",
             "expedienteId": "0196…", "folio": "NORTE-E-2026-000014", "huella": "9f2c…",
             "contenido": { "general": null, "legal": null, "financiera": null, "sst": null, "especialidades": null,
               "tributaria": {
                 "responsabilidades": [ { "codigo": "05", "nombre": "Impuesto sobre la renta y complementarios régimen ordinario" },
                                        { "codigo": "07", "nombre": "Retención en la fuente a título de renta" },
                                        { "codigo": "48", "nombre": "Impuesto sobre las ventas (IVA)" }, { "codigo": "52", "nombre": "Facturador electrónico" } ],
                 "calidades": { "responsableIva": true, "autorretenedor": false, "declaranteRenta": true, "granContribuyente": false,
                                "autorretenedorIca": false, "regimenSimple": false, "granContribuyenteBogota": null, "regimenEspecial": false,
                                "autorretenedorRft": null, "descuentoSenaFic": null, "profesionalIndependiente": null, "exentoRetencion": null,
                                "agenteRetenedorIva": false, "obligadoAFacturar": true },
                 "calidadesSinDerivar": [ { "calidad": "granContribuyenteBogota", "nota": "Calidad distrital (Secretaría de Hacienda de Bogotá)…" } ],
                 "resolucionGranContribuyente": null, "resolucionAutorretenedor": null, "responsableIca": true, "autorretenedorIca": false,
                 "ica": [ { "municipio": { "codigo": "11001", "nombre": "Bogotá, D.C.", "codigoDepartamento": "11", "departamento": "Bogotá, D.C.", "pais": "CO" },
                            "ciiu": { "codigo": "4663", "nombre": "Comercio al por mayor de materiales de construcción…" }, "tarifaPorMil": null } ] } } } }
```

**`contenido`** (`ContenidoDeSeccionDto`): solo viene la propiedad de la sección; las demás van en `null`. Es **lo aceptado**, normalizado para el ERP. Los códigos de catálogo llevan su nombre (`null` si el código ya no está activo).

| Sección | Propiedad | Contenido |
|---|---|---|
| `FICHA_GENERAL` | `general` | `tipoPersona`, `tipoIdentificacion`, `numeroIdentificacion`, `dv`, `nombre` (razón social o nombres y apellidos), `razonSocial`, `nombres`, `apellidos`, `direccion` (texto) y `direccionEstructurada` (formato AyF, si la hay), `ciudad { codigo DANE, nombre, codigoDepartamento, departamento, pais }`, `pais`, `telefono`, `correo`, `ciiuPrincipal { codigo, nombre }`, `ciiuSecundarias[]`, `paginaWeb`, `certificadoIso`, `anioInicio` y, **solo en persona natural**, `representanteLegal` (el propio tercero) |
| `FICHA_TRIBUTARIA` | `tributaria` | `responsabilidades[] { codigo, nombre }` (casilla 53); `calidades` (las 13 del PUT de AyF más `autorretenedorRft`, derivadas según FM-33); `calidadesSinDerivar[] { calidad, nota }` (las que van en `null`); `resolucionGranContribuyente` y `resolucionAutorretenedor { numero, fecha }`; `responsableIca`, `autorretenedorIca` e `ica[] { municipio, ciiu, tarifaPorMil }` |
| `FICHA_LEGAL` | `legal` | `representanteLegal`: el principal que usa AyF (FM-34), con `origen`, `indice` en `personas`, `nombre`, identificación, `tipoRepresentacion`, `cargo`, `fechaExpedicion`, `correo` y `telefono`. Además `personas[]` (todas, con rol, identificación, porcentaje y PEP) y `revisorFiscalFirma` |
| `FICHA_FINANCIERA` | `financiera` | Tal cual, del periodo (`periodo` también va en `datos`): cifras, fecha de corte y moneda extranjera |
| `FICHA_SST` | `sst` | Las ocho preguntas sí/no |
| `FICHA_ESPECIALIDADES` | `especialidades` | `especialidades[] { codigo, nombre }` |

**Calidades tributarias (FM-33).** Cada calidad derivable es `true` si las responsabilidades traen alguno de sus códigos y `false` si no. Va en `null` si no se puede derivar de la ficha; en ese caso el SRM conserva lo que AyF ya tiene. AyF recibe `NoObligadoFacturar` como la negación de `obligadoAFacturar`.

| Calidad | Campo de AyF | Códigos DIAN (casilla 53) |
|---|---|---|
| `responsableIva` | `RegimenIva` | 48 (y el histórico 11); con 49 o 53 es `false` |
| `autorretenedor` | `AutoRetenedor` | 15 |
| `declaranteRenta` | `DeclaranteRenta` | 05, 04 |
| `granContribuyente` | `GranContribuyente` | 13 |
| `autorretenedorIca` | `AutoRetenedorICA` | Ninguno: sale del dato `autorretenedor_ica` de la sección, porque ICA es municipal |
| `regimenSimple` | `RegimenSimple` | 47 |
| `granContribuyenteBogota` | `GranContribuyenteBta` | `null`: calidad distrital que no está en la casilla 53 |
| `regimenEspecial` | `RegimenEspecial` | 04 |
| `autorretenedorRft` | Ninguno (AyF no la recibe) | `null`: la autorretención especial de renta no tiene código propio |
| `descuentoSenaFic` | `DescuentoSenaFic` | `null` |
| `profesionalIndependiente` | `ProfesionalIndependiente` | `null` |
| `exentoRetencion` | `ExentoRetefuente` | `null` |
| `agenteRetenedorIva` | `AgenteRetenedorIVA` | 09, 23 (el 07 es retención a título de renta y **no** cuenta) |
| `obligadoAFacturar` | `NoObligadoFacturar` (negado) | 52, 16 y el histórico 37 |

**Cuenta designada.** No viaja en `seccion.aceptada`: tiene su propio evento, `vinculo.cuenta_designada` (CB-3), con el número **enmascarado**. El número completo se lee en `GET /vinculos/{id}/cuenta-designada` o en `cuentaDesignada` de `GET /vinculos/{id}/ficha`, solo con `cuentas:aprobar` (FM-36).

## 7. Catálogo de eventos públicos

> **Implementado (NW, 2026-11-08)**: 18 tipos de vínculo, expediente y documento con sus `datos` exactos en §6.5 (algunos nombres difieren de esta tabla; ver allí). El resto de la tabla es el objetivo.

| Tipo | Origen (03) | `datos` mínimo |
|---|---|---|
| `tenant.creado` · `empresa.creada` · `flujo.publicado` · `catalogo.publicado` | Plataforma / configuración | ids y versión |
| `tenant.acceso_soporte_concedido` · `tenant.acceso_soporte_expirado` | `AccesoSoporteConcedido/Expirado` (B-6) | `accesoId, alcance{tipo, id}, horas, concedidoPor, expiraEn` |
| `flujo.aplicado_a_abiertos` | `VersionDeFlujoAplicada` | `flujoId, flujoVersion, expedientesAfectados, impacto{agregados, quitados}` |
| `expediente.flujo_cambiado` · `expediente.flujo_seleccionado` | `FlujoDeExpedienteCambiado`, `FlujoSeleccionado` | `expedienteId, flujoVersionAnterior, flujoVersionNuevo, origen (Explicito\|Regla\|PorDefecto), reglaId, justificacion` |
| `configuracion.cambiada` · `identidad.evento` | `RegistroDeCambioDeConfiguracion`, `RegistroDeIdentidad` (solo a webhooks con scope `auditoria:leer`; sin `antes/despues`, solo referencia) | `registroId, tipo, entidad, accion, actor, fecha` |
| `tercero.creado` · `tercero.autorregistrado` · `tercero.datos_maestros_cambiados` | Terceros / `VersionActualCambiada` de `DATOS_MAESTROS` | `terceroId, cambios[]` |
| `tercero.relacion_cambiada` | `RelacionEntreTercerosRegistrada/Confirmada/Finalizada` (B-3) | `terceroId, relacionId, terceroRelacionadoId, tipo, principal, estado, origen` |
| `vinculo.creado` · `vinculo.atributos_actualizados` | `VinculoCreado`, `AtributosDeVinculoActualizados` | `vinculoId, empresaId, terceroId, tipoVinculo, flujo, origen` |
| `vinculo.estado_cambiado` | Cualquier cambio de `EstadoVinculo` (una revalidación vencida **no** cambia el estado del vínculo, T-8: su alerta es `certificado.con_pendientes { bloqueante: true }`) | `vinculoId, estadoAnterior, estadoNuevo, causa: enum(Vigencia\|Expediente\|Bloqueo\|Desvinculacion\|Reintegro\|Anulacion), vigenteHasta` |
| `vinculo.revalidacion_pendiente` · `vinculo.revalidado` · `vinculo.cambio_menor` | `AceptacionDesactualizada`, `VersionAceptadaRegistrada` (revalidación), `CambioMenorRegistrado` | `vinculoId, tipo, versionNueva, plazoRevalidacion, expedienteRevalidacionId` |
| `vinculo.cuenta_designada_propuesta` · `vinculo.cuenta_designada` · `vinculo.cuenta_designada_rechazada` | `CuentaDesignada*` | `vinculoId, cuenta (enmascarada), aprobadaPor[]` |
| `seccion.aceptada` | `SeccionAceptadaPorEmpresa` (F3, FM-32) | `vinculoId, terceroId, empresaId, seccion, periodo?, version, aceptadaEn, contenido` (lo aceptado normalizado para el ERP, §6.6) |
| `vinculo.bloqueado` · `vinculo.desbloqueado` · `vinculo.desvinculado` · `vinculo.reintegrado` · `vinculo.anulado` | `VinculoBloqueado`, `VinculoDesbloqueado`, `VinculoDesvinculado`, `VinculoReintegrado` (`estado` = el resultante; `estadoAnterior = Desvinculado`; `Activo` si fue sin revalidar, `EnValidacion` o `Invitado` si revalida), `VinculoAnulado` | `vinculoId, motivo` |
| `expediente.abierto` · `expediente.reabierto` · `expediente.invitacion_enviada` · `expediente.invitacion_aceptada` | `ExpedienteAbierto`, `ExpedienteReabierto`, `InvitacionEnviada`, `InvitacionAceptada` | `expedienteId, vinculoId, tipo, flujo, requisitos[]` |
| `expediente.recordatorio_cargue` | Scheduled message `RecordatorioCargue` (15/7/1 días antes de `plazoCargueHasta`, P-4-2) | `expedienteId, vinculoId, empresaId, diasRestantes, requisitosPendientes[], plazoCargueHasta` |
| `invitaciones.lote_terminado` | Saga `InvitacionMasiva` | `loteId, empresaId, totales{filas, exitosas, fallidas, duplicadas}` |
| `expediente.documento_cargado` | `VersionAdjuntadaARequisito` | `expedienteId, requisitoId, tipo, documentoId, version` |
| `expediente.enviado_a_revision` · `expediente.reenviado` · `expediente.etapa_activada` · `expediente.area_aprobada` | — | `expedienteId, etapa, area, sla` |
| `expediente.requisito_veredicto` | `VeredictoDeRequisitoEmitido` / `VeredictoAutomaticoEmitido` / `VeredictoAnuladoPorAdministrador` | `expedienteId, requisitoId, tipo, area, veredicto: enum(Aprobado\|Observado), noAdmitida: bool, origen, motivos[], camposAfectados[], sobreVersion` (B-7) |
| `expediente.requisito_eximido` · `expediente.requisitos_recalculados` · `expediente.requisito_desactualizado` · `expediente.requisito_vencido` | — | `expedienteId, requisitoId, justificacion \| cambios` |
| `expediente.con_observaciones` | `ExpedienteObservado` | `expedienteId, requisitosObservados[]` |
| `expediente.revision_humana_solicitada` · `expediente.reenvios_excedidos` | `RevisionHumanaSolicitada`, `AlertaReenviosEmitida` | `expedienteId, requisitoId` |
| `expediente.verificacion_registrada` · `expediente.coincidencia_resuelta` | `VerificacionExternaRegistrada`, `CoincidenciaResuelta` | `expedienteId, tipo, proveedor, resultado, bloqueante, vigenteHasta` |
| `expediente.aprobado` · `expediente.rechazado` · `expediente.caducado` · `expediente.anulado` | `ExpedienteAprobado`, `ExpedienteRechazado`, `ExpedienteCaducado`, `ExpedienteAnulado` (el certificado viaja en `certificado.emitido`, T-15) | `expedienteId, vinculoId, motivos[], justificacion` |
| `expediente.desaprobado` | `EtapaReabierta`, `ExpedienteDesaprobado` (DS-1, DS-2) | `expedienteId, folio, vinculoId, referencias, tipoExpediente, estado: EnRevision, causa: EtapaReabierta\|FaseInicial\|ExpedienteAprobado, justificacion, etapa` (la etapa desde la que se revisa de nuevo) |
| `expediente.sla_incumplido` · `expediente.sla_escalado` | `SlaIncumplido`, `SlaEscalado` | `expedienteId, area, nivel, vencioEn` |
| `documento.version_nueva` · `documento.version_devuelta` · `documento.hechos_registrados` · `documento.datos_corregidos` | `VersionActualCambiada`, `VersionDevueltaAlTercero`, `HechosDeVersionRegistrados`, `DatosDeVersionCorregidos` | §6.4 / `documentoId, version, motivo` (no existe exclusión por el tercero, A-8; la revocación por desvinculación viaja en `vinculo.desvinculado`) |
| `documento.compartido` | `VersionCompartida` al aceptar la invitación o al promover una versión para vínculos ya aceptados (B-1) | `terceroId, documentoId, tipo, version, vinculoId, causa: enum(InvitacionAceptada\|VersionNueva\|Restablecimiento)` |
| `documento.ratificado` · `documento.por_vencer` · `documento.vencido` | `RequisitoRatificado`, job de vigencias | `vinculoId, tipo, version, vigenteHasta, diasRestantes, critico` |
| `certificado.emitido` · `certificado.con_pendientes` · `certificado.suspendido` · `certificado.restablecido` · `certificado.reemplazado` · `certificado.revocado` · `certificado.vencido` | `Certificado` (`CertificadoSuspendido`/`Restablecido` por bloqueo y desbloqueo, B-5) | `folio, emision, estado, causa, vigenteHasta, hash`; `certificado.emitido` según §6.3; `certificado.con_pendientes` añade `bloqueante: bool` y `plazoRevalidacion` (A-2: `false` al marcar, `true` al vencer `diasRevalidacion`); `certificado.suspendido` añade `motivo` |
| `notificacion.entregada` · `notificacion.fallida` | Notificaciones | `notificacionId, canal, destinatario, estado` |
| `webhook.deshabilitado` · `ia.presupuesto_80` · `ia.presupuesto_agotado` | Operación | `webhookId \| tenant, consumido, limite` |
| `habeas.solicitud_recibida` · `habeas.solicitud_resuelta` | Habeas data | `solicitudId, alcance, resultado` |
| `campania.iniciada` · `exportacion.lista` · `importacion.lote_terminado` · `prueba.ping` | — | ids, totales |

Los eventos son **por empresa** (o por tenant cuando no tienen empresa): un integrador recibe los de las empresas en el alcance de su API key. Nunca viajan decisiones de otras empresas ni URLs de binarios.

---

## 8. Alertas y notificaciones (catálogo P-10-1)

> **Implementado (NW, 2026-11-08)**: campana (`/notificaciones`), correo consolidado a revisores (nuevo expediente en el área y respuesta del tercero), aviso a administradores por webhook deshabilitado, constancia al tercero (`consentimiento-empresa`) y preferencias de correo por tipo; detalle en §6.5. Lo demás de esta tabla sigue pendiente.

> **Implementado (NT, 2026-11-10)**: campana del tercero en `apps/public` (`/public/v1/notificaciones`, filas «campana» de la columna *Tercero* de la tabla de abajo: observación, resultado, por vencer, vencido, certificado, plazo, actualización, propuesta e invitación), preferencias de correo del tercero por tipo (la campana no se apaga) y recordatorio de observaciones pendientes (manual, en lote y automático por empresa). Ver §2.7.2.

Canales MVP: correo, campana, webhook (P-10-2). Consolidación: máximo un correo por tercero y hecho por día; una secuencia de avisos por documento; digest diario opcional para revisores (P-10-3). Remitente: dominio de la plataforma, nombre de la empresa, `Reply-To` de la empresa (P-10-4). Evidencia de entrega en `EntregasNotificacion` (adición 4).

| Hecho | Evento público | Plantilla | Tercero | Revisor del área | Administrador | Webhook |
|---|---|---|---|---|---|---|
| Invitación / enlace | `expediente.invitacion_enviada` | `invitacion` | correo | — | — | ✓ |
| Recordatorio de cargue (15/7/1 días) | `expediente.recordatorio_cargue` | `recordatorio-cargue` | correo | — | — | ✓ |
| Documento cargado | `expediente.documento_cargado` | — | campana | campana | — | ✓ |
| Resultado IA: observado automático / devuelto | `expediente.requisito_veredicto` (Sistema) / `documento.version_devuelta` | `observacion-automatica` | correo + campana | — | — | ✓ |
| Requisito observado por humano | `expediente.con_observaciones` | `observacion` (con enlace de acceso de un solo uso, §2.9.3) | correo + campana | — | — | ✓ |
| Invitación aceptada por el tercero (consentimientos registrados, bóveda compartida) | `expediente.invitacion_aceptada` + `documento.compartido` | `consentimiento-empresa` (constancia al tercero) | correo | campana | — | ✓ |
| Primer acceso a la plataforma (aviso de tratamiento, IA) | `expediente.invitacion_aceptada` (primera vez) | `consentimiento-plataforma` | correo | — | — | — |
| Expediente aprobado | `expediente.aprobado` | `aprobado` | campana | campana | — | ✓ |
| Certificado emitido | `certificado.emitido` | `certificado` | correo + campana | campana | — | ✓ |
| Certificado revocado / suspendido | `certificado.revocado` / `certificado.suspendido` | `certificado-revocado` | correo | — | campana | ✓ |
| Certificado con pendientes (vencido no crítico / revalidación pendiente) | `certificado.con_pendientes { bloqueante: false }` | `certificado-pendientes` | campana | — | digest | ✓ |
| Expediente rechazado | `expediente.rechazado` | `rechazo` | correo | — | campana | ✓ |
| Expediente caducado | `expediente.caducado` | `caducidad` | correo | — | campana | ✓ |
| Versión nueva de documento global | `vinculo.revalidacion_pendiente` | `revalidacion` | — | campana | digest | ✓ |
| Cambio menor | `vinculo.cambio_menor` | — | — | campana | — | ✓ |
| Cuenta designada propuesta / aprobada | `vinculo.cuenta_designada_propuesta` / `vinculo.cuenta_designada` | `cuenta-designada` | correo (aprobada) | campana (aprobar) | campana | ✓ |
| Verificación de la persona relacionada expirada (cambio de representante) | `documento.version_nueva` con `verificacionesExpiradas[]` | `verificacion-expirada` | correo (pide cédula/listas del nuevo) | campana | — | ✓ |
| Por vencer (30/15/5; 60/30/7) | `documento.por_vencer` | `por-vencer` (consolidado) | correo consolidado | — | digest | ✓ |
| Vencido | `documento.vencido` / `vinculo.estado_cambiado` | `vencido` | correo | campana | digest | ✓ |
| Revalidación no atendida (plazo vencido ⇒ certificado bloqueante) | `certificado.con_pendientes { bloqueante: true }` (T-8: el vínculo no cambia de estado) | `revalidacion-vencida` | — | correo | correo | ✓ |
| SLA incumplido / escalado | `expediente.sla_incumplido` / `expediente.sla_escalado` | `sla` | — | correo | correo | ✓ |
| Tercer reenvío del mismo requisito | `expediente.reenvios_excedidos` | — | — | — | campana | ✓ |
| Revisión humana solicitada | `expediente.revision_humana_solicitada` | — | — | campana | — | ✓ |
| Coincidencia en listas | `expediente.verificacion_registrada` (Coincidencias) | `coincidencia` | — | correo (cumplimiento) | — | ✓ |
| Webhook fallido definitivamente | `webhook.deshabilitado` | `webhook-fallido` | — | — | correo | — |
| Presupuesto IA 80 % / agotado | `ia.presupuesto_*` | `presupuesto-ia` | — | — | correo | — |
| Solicitud de supresión | `habeas.solicitud_*` | `habeas` | correo (constancia) | — | correo | ✓ |
| Campaña de renovación | `campania.iniciada` | `campania` | correo | — | — | ✓ |
| Verificación de cuenta de usuario / enrolamiento 2FA | `identidad.evento` (`Login`, `DosFactoresActivado`) | `cuenta-verificacion` / `cuenta-2fa` | — | correo (al propio usuario) | — | — |
| Autorregistro recibido (invisible hasta que una empresa vincule) | `tercero.autorregistrado` | `autorregistro` | correo | — | — | — |
| El tercero pide entrar con su correo (§2.9.3; implementado) | — | `acceso` («Tu enlace para entrar a Validasoft») | correo | — | — | — |
| Lote de invitación terminado | `invitaciones.lote_terminado` | `lote` | — | — | campana + correo | ✓ |
| Límite del plan alcanzado (80 % / 100 %) | `ia.presupuesto_80` / `ia.presupuesto_agotado` (y límites de `PlanDto`) | `plan` | — | — | correo | — |
| Exportación lista (expediente, bóveda, auditoría, informe) | `exportacion.lista` | `exportacion` | correo (si la pidió el tercero) | correo (quien la pidió) | — | ✓ |
| Digest diario del revisor | agregación de `expediente.*`, `vinculo.revalidacion_pendiente`, `documento.*` del día | `digest` | — | correo (opt-in) | correo (opt-in) | — |
| Integración degradada (webhooks fallando, pull atrasado > 1 h) | `webhook.deshabilitado` / métrica de entregas | `integracion-degradada` | — | — | correo | — |
| Importación terminada (dry-run o real) | `importacion.lote_terminado` | `importacion-terminada` | — | — | correo (Plataforma y Administrador) | ✓ |
| Bienvenida tras migración | `vinculo.creado { origen: Migracion }` consolidado por tercero | `migracion-bienvenida` | correo (uno por tercero y día) | — | — | — |
| Incidente de plataforma (mantenimiento, degradación) | manual desde `/plataforma/operacion` | `plataforma-incidente` | campana | campana | correo | — |

Preferencias por usuario (`PreferenciasNotificacion`): por evento y canal, digest. Límite anti-spam al tercero: un correo por hecho y día, salvo invitación y certificado. Toda plantilla de esta tabla existe en `/plantillas-notificacion` con cabecera/pie editables (P-10-5) y la CI del documento 11 valida que no haya plantillas fuera de este catálogo.

---

## 9. Versionado y compatibilidad

| Elemento | Política |
|---|---|
| `/api/v1`, `/public/v1` | Cambios aditivos sin versión nueva; incompatibles ⇒ `v2` con 12 meses de convivencia (`Deprecation`, `Sunset`). |
| Eventos | `version` por tipo; incompatibles ⇒ `version+1`; la suscripción elige versión; 12 meses. |
| `legacy.*` | Solo coexistencia; retiro por tenant al completar migración. |
| SDKs | `Validasoft.Sdk` multi-target **`net7.0;net8.0;net10.0`** (el SRM es .NET 7, T-16) y `@validasoft/sdk` (TS) regenerados en el mismo PR; SemVer `1.x`; toleran campos desconocidos. |
| Enums | Valores desconocidos ⇒ "otro". |
| gRPC | No en v1 (ADR-0007); reevaluar solo para `consultas/certificacion` si REST no cumple p99 < 50 ms a 500 rps. |

---

## Decisiones abiertas

1. ~~Formato de `Idempotency-Key`~~ **Cerrada**: cadena libre ≤ 128 caracteres.
2. **Alias en inglés de recursos** para integradores no hispanohablantes. Propuesta: español en v1. Quién decide: Producto.
3. ~~Exposición del titular en `/verificar`~~ **Cerrada** por P-8-1: nombre e identificación enmascarada.
4. ~~Orden en modo pull~~ **Cerrada**: cursor por secuencia del outbox del tenant; sin garantía entre agregados.
5. ~~Límite de tamaño~~ **Cerrada** por P-16-2: 20 MB, 50 páginas; XLSX para EEFF en H7.
6. ~~`X-Empresa` vs segmento de ruta~~ **Cerrada** (T-5): cabecera `X-Empresa` en la API; el segmento `/e/{empresa}` existe solo en las rutas de la SPA.
7. ~~Nombre del veredicto de versión no admitida~~ **Cerrada** (B-7): `Observado` + `noAdmitirEstaVersion`; `Rechazado` solo para el expediente.

## Fuentes verificadas

Ver [01-analisis-legado-srm-portal.md](01-analisis-legado-srm-portal.md) (endpoints legado reemplazados) y [03-dominio-y-eventos.md](03-dominio-y-eventos.md) (eventos de dominio de origen).
