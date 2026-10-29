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
| Autenticación | `Authorization: Bearer <jwt>` (usuarios, terceros, CLI) o `X-Api-Key: vsk_<entorno>_<tenant>_<aleatorio>` (integraciones). Nunca ambas. |
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
| Tiempo real | SSE `/api/v1/eventos/stream` (usuarios) y `/public/v1/expedientes/{id}/stream` (tercero). Resultado de IA en < 60 s p95 (P-6-9). |

---

## 2. Recursos

Columnas: **Permiso/scope** (ver [07 §3](07-seguridad-identidad-y-tenancy.md)), **Respuestas** (éxito y errores propios además de 400/401/403/404/429), **Emite** (eventos públicos de §7).

### 2.1 Plataforma, tenant y empresas

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `POST /plataforma/tenants` · `GET /plataforma/tenants` · `GET /plataforma/tenants/{id}` · `PATCH /plataforma/tenants/{id}` | Alta y configuración del tenant (`slug`, `region`, `claveKms`, `plan`, `presupuestoIa`, políticas) | `plataforma:tenants` | 201/200 | `tenant.creado` |
| `GET /plataforma/uso?tenant=&desde=&hasta=` | Medición de uso (`UsoTenantDto`) | `plataforma:uso` | 200 | — |
| `GET /plataforma/tipos-documento/canonicos` · `POST` · `PUT /{id}` · `POST /{id}/publicar` · `GET /{id}/versiones` | Catálogo canónico versionado (`TipoDocumentoCanonicoDto`) | `plataforma:catalogo` | 200/201 | `catalogo.publicado` |
| `GET /plataforma/propuestas-tipo` · `POST /plataforma/propuestas-tipo/{id}/aprobar` | Propuestas de tipo canónico de los tenants | `plataforma:catalogo` | 200 | — |
| `GET /catalogos/tipos-identificacion?pais=CO` | Tipos de identificación por país con validador (`TipoIdentificacionDto`) | público autenticado | 200 | — |
| `GET /catalogos/paises` · `GET /catalogos/motivos-base` · `GET /catalogos/esquemas-formulario` | Catálogos de plataforma | autenticado | 200 | — |
| `GET /tenant` · `PATCH /tenant` | Configuración del propio tenant (zona horaria, políticas por defecto, SMTP, marca) | `configuracion:escribir` | 200 | — |
| `GET /empresas` · `POST /empresas` · `GET /empresas/{id}` · `PATCH /empresas/{id}` · `POST /empresas/{id}/desactivar` | Razones sociales del tenant (`EmpresaDto`: NIT, nombre, logo, `replyTo`, prefijo de folio) | `empresas:administrar` | 200/201 | `empresa.creada` |
| `GET /api-keys` · `POST /api-keys` · `POST /api-keys/{id}/rotar` · `DELETE /api-keys/{id}` | Llaves con `scopes[]`, `empresas[]` (`*` = todas), `expiraEn`, IPs | `api-keys:administrar` | 200/201 | — |
| `GET /usuarios` · `POST /usuarios` · `PATCH /usuarios/{id}` · `POST /usuarios/{id}/roles` · `DELETE /usuarios/{id}/roles/{rolId}` · `POST /usuarios/{id}/2fa/reiniciar` · `POST /usuarios/{id}/delegaciones` | Usuarios y roles **por empresa o por tenant** (`RolAsignadoDto { rol, empresaId? , area? }`); delegaciones temporales | `usuarios:administrar` | 200/201 | — |
| `GET /roles` · `GET /permisos` | Catálogo | `usuarios:administrar` | 200 | — |
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
| `GET /tipos-vinculo/{id}` · `PUT /tipos-vinculo/{id}` | Detalle y edición (`TipoVinculoDto`) | `configuracion:escribir` | 200 | — |
| `POST /tipos-documento/propuestas` | Proponer un tipo canónico a la plataforma | `configuracion:escribir` | 202 | — |
| `GET /areas` · `POST` · `PUT /{id}` | Áreas revisoras (`AreaDto`) | `configuracion:escribir` | 200/201 | — |
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
| `GET /vinculos` · `POST /vinculos` · `GET /vinculos/{id}` · `PATCH /vinculos/{id}/atributos` | Vínculos de la empresa (`VinculoDto`). Al crear un vínculo con un tercero que ya tiene bóveda, la respuesta trae `requisitosCubribles: n` (cuántos requisitos podrían quedar cubiertos) **sin nombres de documentos ni versiones**; la compartición ocurre cuando el tercero acepta la invitación (B-1) | `vinculos:leer`/`escribir` | 200/201; 409 `vinculo-duplicado`, `tercero-bloqueado` | `vinculo.creado`, `vinculo.atributos_actualizados` |
| `POST /vinculos/{id}/desvincular` · `/reintegrar` · `/anular` · `/bloquear` · `/desbloquear` | Ciclo de vida y bloqueo privado. **Bloquear suspende** el certificado y congela la compartición futura; **desbloquear restituye** certificado y compartición si siguen vigentes, o abre `Renovacion` si algo venció (B-5) | `vinculos:escribir` / `vinculos:bloquear` | 200; 409 `estado-invalido` | `vinculo.estado_cambiado`, `vinculo.bloqueado` + `certificado.suspendido`, `vinculo.desbloqueado` + `certificado.restablecido` |
| `GET /vinculos/{id}/versiones-aceptadas` | `VersionAceptadaDto[]` | `vinculos:leer` | 200 | — |
| `GET /vinculos/{id}/cuenta-designada` · `POST /vinculos/{id}/cuenta-designada/aprobar` · `POST /vinculos/{id}/cuenta-designada/rechazar` | La propone el tercero (public); la empresa aprueba (2 aprobadores por defecto) | `cuentas:aprobar` | 200; 409 `aprobador-repetido` | `vinculo.cuenta_designada_propuesta`, `vinculo.cuenta_designada` |
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
| `POST /expedientes/{id}/enviar-a-revision` · `/reenviar` · `/reabrir` | Tercero completa / corrige; administrador reabre caducado | tercero / `expedientes:escribir` | 200; 409 | `expediente.enviado_a_revision`, `expediente.reenviado`, `expediente.reabierto` |
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
| `POST /invitaciones/lotes` · `GET /invitaciones/lotes` · `GET /invitaciones/lotes/{id}` · `GET /invitaciones/lotes/{id}/filas?estado=` · `POST /invitaciones/lotes/{id}/pausar` · `/reanudar` · `/cancelar` | Invitación masiva (`CrearLoteDto` → `LoteDto`; `PaginaDto<FilaLoteDto>`); saga `InvitacionMasiva` (H8) | `expedientes:escribir` | 202/200; 409 `lote-en-estado-terminal` | `invitaciones.lote_terminado` |

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

### 2.7 Vencimientos, notificaciones y auditoría

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `GET /vencimientos?desde&hasta&estado&tipo&empresaId` | `VencimientoDto[]` | `vencimientos:leer` | 200 | — |
| `POST /vencimientos/notificar` | Recordatorio consolidado (`NotificarVencimientosDto`) | `vencimientos:escribir` | 202 | — |
| `GET /notificaciones` · `GET /notificaciones/{id}/entregas` | Envíos y evidencia de entrega (`EntregaNotificacionDto`) | `auditoria:leer` | 200 | — |
| `GET /novedades` · `POST /novedades/{id}/leer` | Campana | sesión | 200 | — |
| `GET /auditoria?tipo=dominio\|configuracion\|identidad\|acceso&entidad=&entidadId=&actor=&desde=&hasta=` | Registro unificado (`PaginaDto<RegistroAuditoriaDto>`): eventos de dominio (timelines), **cambios de configuración** (`auditoria_configuracion`, con `antes`/`despues`/`diff`), **identidad** (login, 2FA, bloqueos, uso de API key, token exchange ERP, magic link canjeado) y **accesos a documentos** (`DocumentoAccedido`); siempre filtrado por tenant y, si el rol lo exige, por empresa | `auditoria:leer` | 200 | — |
| `POST /auditoria/exportar` · `GET /auditoria/exportaciones/{id}` | Exportación ZIP firmado (JSONL por tipo + índice + hash) de un rango (`ExportarAuditoriaDto` → `ExportacionDto`) | `auditoria:exportar` | 202/200 | `exportacion.lista` |
| `GET /kpis?flujoId=&empresaId=&periodo=` | `KpisDto` (proyecciones `KpisTenant`/`KpisFlujo`/`KpisEmpresa`; incluye `decisionesAutomaticas` y `equidad`) | `expedientes:leer` | 200 | — |
| `GET /vencimientos/proximos?dias=30` | `VencimientosProximos` (vista de Inicio; mismo `VencimientoDto`) | `vencimientos:leer` | 200 | — |
| `GET /usuarios/me/preferencias-notificacion` · `PUT /usuarios/me/preferencias-notificacion` | `PreferenciasNotificacionDto` (por evento y canal, digest) | sesión | 200 | — |
| `GET /informes/estado-expedientes?flujoId=&estado=&desde=&hasta=` · `POST /informes/estado-expedientes/exportar` | Informe de estado (`PaginaDto<BandejaExpedienteDto>`); exportación Excel asíncrona (`ExportacionDto`) | `expedientes:leer` | 200/202 | `exportacion.lista` |
| `GET /informes/cumplimiento?desde=&hasta=&riesgo=` · `POST /informes/cumplimiento/exportar` | `InformeCumplimiento` (`InformeCumplimientoDto`); exportación | `auditoria:leer` o rol `Cumplimiento` | 200/202 | `exportacion.lista` |
| `GET /exportaciones/{id}` | Estado y URL prefirmada de una exportación (`ExportacionDto`) | quien la pidió | 200 | — |
| `GET /solicitudes-supresion?estado=` · `GET /solicitudes-supresion/{id}` · `POST /solicitudes-supresion/{id}/resolver` | Lado empresa del habeas data (adición: la saga `SupresionDatos` escala a humano las retenciones dudosas): `SolicitudSupresionDto`, `ResolverSupresionDto { decision*: Suprimir\|Retener, baseLegal, hasta }` | `supresiones:resolver` | 200; 409 `solicitud-ya-resuelta` | `habeas.solicitud_resuelta` |

### 2.8 Webhooks y eventos

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `GET /webhooks` · `POST` · `PUT /{id}` · `DELETE /{id}` | `WebhookDto` (`url`, `eventos[]`, `empresas[]`, `version`, `secreto` solo al crear/rotar). **Precedencia de alcance**: el alcance efectivo es `apiKey.empresas[] ∩ webhook.empresas[]` (con `*` = todas las del tenant); si la intersección queda vacía al crear o editar ⇒ `422 empresas-fuera-de-alcance`; si una rotación o edición posterior de la API key deja la intersección vacía, la suscripción se deshabilita y se emite `webhook.deshabilitado { causa: "empresas-fuera-de-alcance" }` | `webhooks:administrar` | 200/201; 422 `empresas-fuera-de-alcance` | `webhook.deshabilitado` |
| `POST /webhooks/{id}/rotar-secreto` · `POST /webhooks/{id}/probar` | Rotación 24 h; `prueba.ping` | `webhooks:administrar` | 200 | `prueba.ping` |
| `GET /webhooks/{id}/entregas` · `GET …/entregas/{eid}` · `POST …/entregas/{eid}/reenviar` | `EntregaWebhookDto` | `webhooks:administrar` | 200 | — |
| `GET /eventos?desde={cursor}&tipos=&empresaId=` | **Modo pull** (`PaginaDto<EventoPublicoDto>`), orden por secuencia del outbox del tenant | `eventos:leer` | 200 | — |

### 2.9 Público (tercero) e importación

| Método y ruta | Descripción | Permiso | Respuestas | Emite |
|---|---|---|---|---|
| `POST /public/v1/enlaces/{token}/canjear` · `POST /public/v1/enlaces/{token}/otp` | Magic link y OTP (`SesionTerceroDto`); solo abre sesión, **no comparte nada** | público | 200; 410 `enlace-usado` | — |
| `POST /public/v1/invitaciones/{expedienteId}/aceptar` | El tercero **acepta la invitación** de una empresa (`AceptarInvitacionDto`: aviso de plataforma —solo la primera vez—, autorización de datos y T&C de esa empresa, con evidencia). En ese momento se registran los consentimientos y se **comparte** con esa empresa la versión actual de cada tipo global que exige (B-1); hasta entonces la empresa solo ve `requisitosCubribles` | sesión tercero | 200; 409 `invitacion-ya-aceptada` | `expediente.invitacion_aceptada`, `documento.compartido` (uno por documento) |
| `GET /public/v1/tercero/relaciones` · `POST /public/v1/tercero/relaciones` · `POST /public/v1/tercero/relaciones/{rid}/confirmar` · `DELETE /public/v1/tercero/relaciones/{rid}` | El tercero declara o confirma sus **personas relacionadas** (B-3): representante legal principal y suplentes, socios/beneficiarios finales, miembros del consorcio; cada una recibe su propio enlace para subir cédula y antecedentes a **su** bóveda | sesión tercero | 200/201; 409 `relacion-duplicada` | `tercero.relacion_cambiada` |
| `POST /public/v1/autorregistro` | Autorregistro invisible (`AutorregistroDto`) | público (rate limit) | 202 | `tercero.autorregistrado` |
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

---

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
| 401 | `no-autenticado` |
| 403 | `sin-permiso` · `version-no-compartida` · `area-no-puede-rechazar` · `acceso-soporte-no-concedido` (solo dentro de la propia empresa; otro tenant/empresa ⇒ `404`, T-4) |
| 404 | `no-encontrado` (incluye recursos de otro tenant/empresa: nunca se revela su existencia) |
| 409 | `estado-invalido` · `expediente-abierto-existente` · `version-superada` · `requisito-no-en-alcance` · `version-no-admitida-en-vinculo` · `version-referenciada` · `vinculo-duplicado` · `tercero-duplicado` · `relacion-duplicada` · `tercero-bloqueado` · `aprobador-repetido` · `conflicto-de-interes` · `invitacion-ya-aceptada` |
| 410 | `enlace-usado` · `enlace-expirado` |
| 412 | `version-desactualizada` |
| 413 | `archivo-demasiado-grande` |
| 415 | `tipo-archivo-no-soportado` |
| 422 | `clave-idempotencia-reutilizada` · `frescura-incumplida` · `ratificacion-no-permitida` · `hash-no-coincide` · `ambito-no-ampliable` · `override-mas-laxo` · `empresas-fuera-de-alcance` · `umbral-invalido` · `dkim-no-verificado` · `falta-flujo-por-defecto` · `alcance-invalido` (no existe el 422 "informativo": `presupuesto-ia-agotado` viaja como advertencia en un `2xx`, §1) |
| 429 | `limite-excedido` |
| 503 | `verificacion-no-disponible` |

### 3.4 SSE
`id` = cursor (`Last-Event-ID`), `event` = tipo público, `data` = payload de §7. Reenvío hasta 24 h. Tres streams: `GET /eventos/stream` (usuarios de `apps/web`, filtrado por la empresa activa), `GET /public/v1/expedientes/{id}/stream` (un expediente del tercero) y `GET /public/v1/boveda/stream` (toda la bóveda del tercero; ver §2.9).

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
| `AreaDto` | `id*`, `codigo*`, `nombre*`, `responsableId`, `escalamientoA: uuid[]` |
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
| `VinculoDto` | `id*`, `empresaId*`, `terceroId*`, `tipoVinculo*`, `flujoId*`, `estado*`, `atributos*: json`, `vigenteHasta`, `invitacionAceptadaEn: datetime?` (hasta entonces nada está compartido con esta empresa, B-1), `requisitosCubribles: int?` (solo antes de la aceptación), `revalidacionPendiente*: bool`, `revalidaciones: {tipo, versionNueva, plazo, bloqueante}[]`, `certificado: {folio, estado}`, `cuentaDesignada: CuentaDesignadaDto`, `bloqueo: {motivo, desde, hasta}`, `referenciaExterna: {sistema, id}`, `version*` |
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
| `CertificadoDto` | `folio*` (formato `{PrefijoEmpresa}-C-{Año}-{Secuencia}-{n}`, T-9), `emision*`, `estado*: EstadoCertificado` (incluye `Suspendido`, B-5), `empresa*: {id, nombre}`, `vinculoId*`, `expedienteFolio*`, `tercero*: {nombre, identificacionEnmascarada}`, `flujo*`, `emitidoEn*`, `vigenteHasta`, `huella*`, `urlVerificacion*`, `requisitos*: {codigo, version, vigenteHasta, eximido}[]`, `areas*: {area, origen: enum(Humano\|Sistema\|Administrador), fecha}[]`, `pendientes: {causa, tipo, bloqueante}[]`, `suspension: {motivo, desde}` |
| `CertificadoPublicoDto` | `folio*`, `estado*: EstadoCertificado`, `bloqueante*: bool` (A-2/B-5), `titular*: {nombre, identificacionEnmascarada}`, `emisor*: {empresa, logoUrl}`, `flujo*`, `emitidoEn*`, `vigenteHasta`, `huella*`, `contenidoCanonico*: CertificadoContenidoDto` (lo que se hashea; sin contactos ni nombres de revisores), `urlVerificacion*`, `pendientes: string[]`, `firma*: {algoritmo: "Ed25519", clavePublicaUrl, valor}` |
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
| `SesionTerceroDto` | `accessToken*`, `expira*`, `terceroId*`, `vinculos*: {id, empresa, estado}[]`, `expedienteOrigen` |
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
| `ApiKeyDto` | `id*`, `prefijo*`, `nombre*`, `scopes*: string[]`, `empresas*: string[]`, `ipsPermitidas: string[]`, `expiraEn`, `ultimoUsoEn`, `creadaEn*`, `valor` (solo al crear/rotar), `sandbox*: bool` (única forma; no existe `modo`) |
| `UsuarioDto` | `id*`, `correo*`, `nombre*`, `identificacion: IdentificacionDto?` (para el cruce de conflicto de intereses contra contactos y personas relacionadas del tercero, I-21), `roles*: RolAsignadoDto[]`, `dosFactores*: enum(Pendiente\|Activo\|Exento)`, `activo*`, `ultimoAcceso`, `delegaciones: DelegacionDto[]` |
| `RolAsignadoDto` | `rol*: enum(Plataforma\|Administrador\|Revisor\|Cumplimiento\|Auditor)`, `empresaId: uuid \| "*"`, `areas: uuid[]` |
| `RolDto` | `codigo*`, `nombre*`, `permisos*: string[]` |
| `PermisoDto` | `codigo*`, `descripcion*`, `requiereArea*: bool` |
| `DelegacionDto` | `id*`, `delegadoId*`, `desde*`, `hasta*`, `empresaId*`, `areas*: uuid[]`, `activa*` |
| `PropuestaTipoDto` | `id*`, `tenantId*`, `codigoPropuesto*`, `nombre*`, `justificacion*`, `tipoTenantId*` (opera como `PorVinculo` mientras tanto), `estado*: enum(Pendiente\|Aprobada\|Rechazada)`, `tipoCanonicoId` (al aprobar), `comentarioPlataforma` |
| `EsquemaFormularioDto` | `tipoCanonicoId*`, `versionEsquema*`, `esquemaCanonico*: json (JSON Schema)`, `camposPrivadosTenant: json`, `esquemaFusionado*: json`, `camposClave*: string[]`, `camposCriticos*: string[]`, `uiSchema: json` |
| `DatosMaestrosDto` | Formulario global `DATOS_MAESTROS` que propone el tercero: `nombre*`, `nombreComercial`, `direccionPrincipal*: DireccionDto`, `contactos*: ContactoDto[]`, `actividadEconomica`, `regimenTributario`; las personas relacionadas van por `/public/v1/tercero/relaciones` (B-3); `cambiosCriticos` lo calcula el servidor. Si la IA de plataforma está disponible y el tercero consintió, se contrasta contra el RUT actual; si no, queda pendiente de revalidación manual en cada empresa (P-1-7) |
| `TipoVinculoDto` | `id*`, `codigo*`, `nombre*`, `atributosPermitidos*: {clave, tipo, obligatorio, opciones}[]`, `activo*` (el flujo por defecto es por empresa: `FlujoPorDefectoDto`) |
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
| `CrearLoteDto` | `flujo*: {codigo, version}`, `empresaId*`, `filas*: {identificacion*, tipoPersona*, nombre*, correo*, atributos}[]` (≤ 10 000), `programadoPara`, `invitacion*: InvitacionDto` |
| `LoteDto` | `id*`, `estado*: enum(Programado\|Procesando\|Pausado\|Completado\|CompletadoConErrores\|Cancelado)`, `totales*: {filas, procesadas, exitosas, fallidas, duplicadas, correosEnviados, correosPendientes}`, `creadoPor*`, `creadoEn*`, `inicio`, `fin` |
| `FilaLoteDto` | `n*`, `identificacion*`, `estado*: enum(Pendiente\|Exitosa\|Fallida\|Duplicada)`, `expedienteId`, `error`, `procesadaEn` |
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
| `CertificadoContenidoDto` | Contenido **canónico** que se hashea (ver [03 §7.1](03-dominio-y-eventos.md)): `esquema*: "validasoft.certificado.v1"`, `folio*`, `emision*`, `empresa*: {id, nombre, identificacion}`, `tercero*: {nombre, identificacionEnmascarada, pais, tipoPersona}`, `tipoVinculo*`, `flujo*: {codigo, version}`, `requisitos*: {codigo, versionN, sha256 \| hashDatos, vigenteHasta, eximido, justificacion}[]` (ordenados por `codigo`), `verificaciones*: {tipo, proveedor, resultado, fecha}[]`, `areas*: {area, origen, fecha}[]`, `emitidoEn*`, `vigenteHasta`; **excluye** contactos, direcciones, URLs de descarga y nombres de revisores |

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
→ 200  ETag: "c-01JA…-8"  Cache-Control: max-age=300
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

---

## 7. Catálogo de eventos públicos

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
| `vinculo.bloqueado` · `vinculo.desbloqueado` · `vinculo.desvinculado` · `vinculo.reintegrado` · `vinculo.anulado` | — | `vinculoId, motivo` |
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

Canales MVP: correo, campana, webhook (P-10-2). Consolidación: máximo un correo por tercero y hecho por día; una secuencia de avisos por documento; digest diario opcional para revisores (P-10-3). Remitente: dominio de la plataforma, nombre de la empresa, `Reply-To` de la empresa (P-10-4). Evidencia de entrega en `EntregasNotificacion` (adición 4).

| Hecho | Evento público | Plantilla | Tercero | Revisor del área | Administrador | Webhook |
|---|---|---|---|---|---|---|
| Invitación / enlace | `expediente.invitacion_enviada` | `invitacion` | correo | — | — | ✓ |
| Recordatorio de cargue (15/7/1 días) | `expediente.recordatorio_cargue` | `recordatorio-cargue` | correo | — | — | ✓ |
| Documento cargado | `expediente.documento_cargado` | — | campana | campana | — | ✓ |
| Resultado IA: observado automático / devuelto | `expediente.requisito_veredicto` (Sistema) / `documento.version_devuelta` | `observacion-automatica` | correo + campana | — | — | ✓ |
| Requisito observado por humano | `expediente.con_observaciones` | `observacion` | correo + campana | — | — | ✓ |
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
