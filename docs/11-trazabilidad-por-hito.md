# 11 — Trazabilidad por hito (contratos que entrega cada hito)

| Estado | Fecha | Responsable |
|---|---|---|
| Borrador | 2026-10-28 | Arquitectura |

**Propósito.** Garantizar que **nada queda por definir**: para cada hito H1–H11 del [plan](09-plan-de-hitos-y-responsabilidades.md) se enumeran, con los nombres exactos de [03](03-dominio-y-eventos.md) (agregados, eventos de dominio, proyecciones, sagas), [05](05-api-contratos-y-webhooks.md) (endpoints, DTOs, eventos públicos, alertas) y [06](06-frontend-y-ux.md) (pantallas, componentes), los contratos que nacen o cambian, las pruebas que lo demuestran y los efectos hacia atrás y hacia adelante. Es la lista de verificación de la **definición de hecho** (P-17-3) y el índice que la CI compara con el OpenAPI.

**Relacionados.** [09](09-plan-de-hitos-y-responsabilidades.md) · [03](03-dominio-y-eventos.md) · [05](05-api-contratos-y-webhooks.md) · [06](06-frontend-y-ux.md) · [04](04-arquitectura-plataforma.md) · [07](07-seguridad-identidad-y-tenancy.md) · [02](02-modelo-documental-compartido.md) · [decisiones/registro.md](decisiones/registro.md) · ADR-0007.

**Cómo se mantiene.**
1. Todo PR que toque un contrato (endpoint, DTO, evento, proyección, plantilla, pantalla, permiso, clave de configuración) actualiza este documento **en el mismo PR**; `CODEOWNERS` exige revisión de Arquitectura.
2. Regla de CI (`ci.yml`, job `trazabilidad`): un script lee `/openapi/v1.json` generado por el build y extrae `MÉTODO ruta` y nombres de esquemas; lee las tablas §3(a) y §2.x.3 de este archivo; **falla** si un endpoint u esquema del OpenAPI no aparece aquí, o si aparece aquí con un hito ya cerrado y no existe en el OpenAPI. Lo mismo para el catálogo de eventos públicos (`Validasoft.Contratos.Eventos`) contra §3(b) y para las plantillas de notificación (`Plantillas/*.liquid`) contra §3(c).
3. Al cerrar un hito, la columna "Estado" de §3 pasa de `Planificado` a `Entregado` con el tag de versión.

---

## 1. Convenciones de identificación

| Elemento | Forma | Ejemplo | Dueño del nombre |
|---|---|---|---|
| Endpoint | `MÉTODO /ruta` relativa a `/api/v1` o `/public/v1`; parámetros entre llaves | `POST /expedientes/{id}/requisitos/{rid}/veredicto` | 05 |
| DTO | `PascalCase` + sufijo `Dto`; `*` marca campo obligatorio | `CrearExpedienteDto` | 05 §4 |
| Evento de dominio | Pasado, `PascalCase`, sin sufijo | `VersionActualCambiada` | 03 |
| Evento público | `agregado.accion` en minúsculas con guion bajo | `expediente.requisito_veredicto` | 05 §7 |
| Proyección | `PascalCase` descriptivo | `BandejaRevalidaciones` | 03 §14 |
| Alerta / plantilla | Nombre de plantilla en kebab-case, por canal | `revalidacion` (correo, campana) | 05 §8 |
| Pantalla | `app:ruta` | `web:/t/{tenant}/e/{empresa}/revalidaciones`, `public:/boveda` | 06 §3 |
| Componente UI | `PascalCase` del paquete `ui`/`forms` | `PanelVeredicto` | 06 §2.2 |
| Permiso | `recurso:accion[:alcance]` | `veredictos:emitir:area:legal` | 07 §3 |
| Rol | `Plataforma`, `Administrador`, `Revisor`, `Cumplimiento`, `Auditor`, `Integracion`, `Tercero` | — | 07 |
| Saga / job / scheduled | `PascalCase` | `Revalidacion`, `ProcesarVencimientosDelDia`, `VencerSlaRevision` | 03 §8, 04 §5 |
| Configuración | `Seccion:Clave` (tenant) o campo del DTO de tipo de documento, en la jerarquía tenant → empresa → regla del flujo (B-4) | `Ia:PresupuestoMensualUsd`, `TipoDocumentoTenantDto.diasRevalidacion`, `TipoDocumentoEmpresaDto.politicaDesactualizada` | 04, 05 |

Todo endpoint de `/api/v1` que opera sobre recursos de empresa recibe la cabecera `X-Empresa` (ADR-0008, T-5); `X-Tenant` existe solo en `/plataforma/*`; un recurso de otro tenant u otra empresa responde **404** (T-4), nunca 403. Todo evento público lleva `empresaId` (o `null` en eventos de tenant). Idempotencia: `Idempotency-Key` obligatorio en todo `POST` creador o con efectos (05 §3.1): en las tablas, columna **Idem.** = Sí/No.

---

## 2. Por hito

### H1 — Fundación técnica

#### 2.1.1 Agregados/entidades y eventos
| Nombre | Campos clave | Invariantes probadas |
|---|---|---|
| `Tenant` (documento Marten, esquema `plataforma`) | `id, slug, region, claveKms, plan, presupuestoIa, politicasPorDefecto, zonaHoraria` | `slug` único; región inmutable tras el alta |
| `Empresa` (documento, esquema `plataforma`; T-1) | `id, tenantId, identificacion, nombre, logoUrl, replyTo, prefijoFolio, activa` | ≥ 1 empresa por tenant; `prefijoFolio` único por tenant; el logo y el `replyTo` son por empresa (T-18) |
| `Usuario`, `RolAsignado { rol, empresaId?, area? }` (ASP.NET Identity, esquema `identidad`) | `identificacion?` (para conflicto de intereses, I-21) | Rol `Administrador` exige 2FA activo (P-14-4) |
| `ConcesionAccesoSoporte` (`plataforma`, B-6) | `tenantId, concedidoPor, alcance, expiraEn, usadoEn[]` | Soporte nunca ve datos sin concesión vigente; expira sola |
| `ApiKey` | `prefijo, hashArgon2id, scopes[], empresas[], expiraEn, ips[], ultimoUso` | Hash nunca legible; `empresas[]` ⊆ empresas del tenant |
| `TipoIdentificacion` (catálogo `plataforma`) | `pais, codigo, nombre, aplicaA, patron, tieneDv` | Validador por país normaliza `numero` |
| `UsoTenant` (proyección async) | `tenantId, periodo, usuarios, empresas, vinculosActivos, gbAlmacenados, lecturasIa, decisionesIa, webhooksEntregados, costoIaUsd` | Contadores idempotentes por evento |
| Eventos de auditoría (CRUD con `AuditoriaConfiguracion`; streams `auditoria-configuracion-{tenantId}`, `auditoria_identidad`) | `TenantCreado`, `EmpresaCreada`, `EmpresaDesactivada`, `ApiKeyCreada/Rotada/Revocada`, `RolAsignado/Retirado`, `AccesoSoporteConcedido/Usado/Expirado` | — |

#### 2.1.2 Comandos y endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `POST /plataforma/tenants` · `GET /plataforma/tenants` · `GET /plataforma/tenants/{id}` · `PATCH /plataforma/tenants/{id}` | `plataforma:tenants` | `TenantDto` → `TenantDto` | 201/200/404 | `tenant.creado` | Sí (POST) |
| `GET /plataforma/uso?tenant=&desde=&hasta=` | `plataforma:uso` | — → `UsoTenantDto[]` | 200 | — | — |
| `GET /catalogos/tipos-identificacion?pais=` · `GET /catalogos/paises` | autenticado | — → `TipoIdentificacionDto[]` / `PaisDto[]` | 200 | — | — |
| `GET /tenant` · `PATCH /tenant` | `configuracion:escribir` | `TenantConfigDto` | 200 | — | No |
| `GET /tenant/accesos-soporte` · `POST /tenant/accesos-soporte` · `DELETE /tenant/accesos-soporte/{id}` (B-6) | `configuracion:escribir` (concede) / `plataforma:soporte` (usa) | `AccesoSoporteDto` | 200/201/204 | `tenant.acceso_soporte_concedido`, `tenant.acceso_soporte_expirado` | Sí (POST) |
| `GET /empresas` · `POST /empresas` · `GET /empresas/{id}` · `PATCH /empresas/{id}` · `POST /empresas/{id}/desactivar` | `empresas:administrar` | `EmpresaDto` | 200/201; 409 `estado-invalido` | `empresa.creada` | Sí (POST) |
| `GET /api-keys` · `POST /api-keys` · `POST /api-keys/{id}/rotar` · `DELETE /api-keys/{id}` | `api-keys:administrar` | `ApiKeyDto` (secreto solo al crear/rotar) | 200/201/204 | — | Sí |
| `GET /usuarios` · `POST /usuarios` · `PATCH /usuarios/{id}` · `POST /usuarios/{id}/roles` · `DELETE /usuarios/{id}/roles/{rolId}` · `POST /usuarios/{id}/2fa/reiniciar` | `usuarios:administrar` | `UsuarioDto`, `RolAsignadoDto` | 200/201/204 | — | Sí (POST) |
| `GET /roles` · `GET /permisos` | `usuarios:administrar` | → `RolDto[]`, `PermisoDto[]` | 200 | — | — |
| `GET /health/live` · `GET /health/ready` · `GET /health` | — / `operacion:leer` | — | 200/503 | — | — |
| OIDC: `/connect/authorize`, `/connect/token` (grants `authorization_code+PKCE`, `refresh_token`, `urn:validasoft:erp-token`, `urn:validasoft:magic-link`), `/connect/userinfo`, `/.well-known/openid-configuration` | — | OpenIddict estándar | 200/400 | — | — |

#### 2.1.3 DTOs nuevos
| DTO | Campos |
|---|---|
| `TenantDto` | `id*`, `slug*`, `nombre*`, `region*`, `plan*`, `presupuestoIaUsd`, `zonaHoraria*`, `politicasPorDefecto: json`, `creadoEn*` |
| `TenantConfigDto` | `zonaHoraria*`, `smtp: {host, puerto, usuario, remitente}`, `marca: {logoUrl, colorPrimario}`, `politicasPorDefecto: json` |
| `PaisDto` | `codigo*`, `nombre*`, `tiposIdentificacion*: string[]` |
| `ApiKeyDto` | `id*`, `prefijo*`, `nombre*`, `scopes*: string[]`, `empresas*: string[]`, `expiraEn`, `ips: string[]`, `ultimoUso`, `secreto` (solo creación/rotación) |
| `UsuarioDto` | `id*`, `correo*`, `nombre*`, `identificacion?: IdentificacionDto`, `roles*: RolAsignadoDto[]`, `dosFactoresActivo*: bool`, `activo*` |
| `AccesoSoporteDto` | `id*`, `alcance*: enum(Lectura\|Diagnostico)`, `motivo*`, `expiraEn*`, `concedidoPor*`, `usos: {fecha, actor}[]` |
| `RolAsignadoDto` | `rol*`, `empresaId`, `area` |
| `RolDto` / `PermisoDto` | `codigo*`, `nombre*`, `permisos*: string[]` / `codigo*`, `descripcion*` |
| Reutilizados de 05 | `IdentificacionDto`, `TipoIdentificacionDto`, `EmpresaDto`, `UsoTenantDto`, `PaginaDto`, `ProblemaDto` |

#### 2.1.4 Eventos públicos
`tenant.creado`, `empresa.creada`, `tenant.acceso_soporte_concedido`, `tenant.acceso_soporte_expirado` (`empresaId = null` en todos). Sin webhooks aún: quedan en el outbox y se exponen por `GET /eventos` desde H5.

#### 2.1.5 Proyecciones
`UsoTenant` (async) → `web:/plataforma/uso` y `web:/configuracion/tenant/plan`.

#### 2.1.6 Alertas
Ninguna de negocio. Técnicas: correo de verificación de cuenta y de 2FA (`cuenta-verificacion`, `cuenta-2fa`) por Mailpit en dev.

#### 2.1.7 Pantallas
| Pantalla | Componentes | Lecturas |
|---|---|---|
| `web:/login` (OIDC PKCE), 2FA | `ui` tema | — |
| `web:/t/{tenant}` AppShell con `SelectorEmpresa` | `SelectorEmpresa`, `ChipEstado`, `Folio`, `KpiCard`, `BarraProgresoEtapas` | `GET /empresas` |
| `web:/plataforma/tenants`, `/plataforma/tenants/:id/empresas`, `/plataforma/catalogos/identificacion` | `SelectorIdentificacion` | `GET /plataforma/tenants`, `GET /catalogos/tipos-identificacion` |
| `web:/configuracion/tenant/usuarios`, `/configuracion/tenant/api-keys`, `/configuracion/tenant/accesos-soporte` | tablas | `GET /usuarios`, `GET /api-keys`, `GET /tenant/accesos-soporte` |
| `public:` shell y `/verificar/:folio` placeholder | — | — |

#### 2.1.8 Jobs/sagas
Ninguna de negocio. `Worker` arranca con colas vacías; agregación diaria de `UsoTenant`.

#### 2.1.9 Configuración
| Clave | Tipo | Default |
|---|---|---|
| `Tenant.region` | enum región | `co-1` |
| `Tenant.claveKms` | referencia KMS | generada |
| `Tenant.presupuestoIaUsd` | decimal | `null` (sin IA) |
| `Seguridad:DosFactoresObligatorioRoles` | string[] | `[Administrador, Auditor, Cumplimiento]` |
| `Ingesta:LimiteMb`, `Ingesta:MaxPaginas`, `Ingesta:FormatosPermitidos` | int, int, string[] | `20`, `50`, `[pdf,jpg,png,webp]` (T-17) |
| `Marten:Esquemas` | `plataforma`, `boveda`, `validacion` (+ `identidad` en EF) con un solo `DocumentStore` conjoined y tenants técnicos `boveda`/`plataforma` (T-1, T-2; *spike* en H1) | fijo |

#### 2.1.10 Pruebas
- Unit: validadores de identificación por país (`NIT 900123456-7`, `CC 1.234.567`, `RFC`, `CUIT`).
- Arquitectura: `Dominio` sin dependencias; toda consulta a `validacion` pasa por `ISesionDeEmpresa` (T-3) que fija `tenant_id` y `empresa_id` (NetArchTest); ningún handler lee `X-Empresa` directamente.
- Integración: login + 2FA; crear tenant con dos empresas; API key con `empresas:[A]` recibe **404** al usar `X-Empresa: B` (T-4); soporte de plataforma sin concesión ⇒ 404 en cualquier recurso del tenant, con concesión ⇒ 200 y `AccesoSoporteUsado` (B-6).
- Spike T-2: un solo `DocumentStore` con streams de `boveda` (tenant técnico `boveda`) y de `validacion` (tenant = `tenantId`) en una misma transacción; si falla, se abre ADR de reemplazo antes de H2.
- Contract: snapshot inicial de `/openapi/v1.json`.
- Compose: `make up`, `GET /health/ready` = 200.

#### 2.1.11 Efectos
Hacia atrás: ninguno. Hacia adelante: toda entidad de `validacion` nace con `empresaId`; `UsoTenant` recibe contadores de H2 (GB, lecturas IA) y H7 (decisiones).

---

#### H1 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Endpoint | `GET /openapi/v1.json`, `GET /openapi/public-v1.json` (sin auth; fuente de orval/SDKs y del snapshot de CI) | 05 §2.1 |
| Endpoint | `GET /auditoria?tipo=dominio\|configuracion\|identidad\|acceso&entidad&entidadId&actor&desde&hasta` (consulta; la exportación llega en H8) | 05 §2.7 |
| Registro | `RegistroDeCambioDeConfiguracion`, `RegistroDeIdentidad` (append-only, misma transacción; invariante 14 de 02 §13; test arquitectónico) | 03 §2.1 |
| DTO | `TenantDto`, `TenantConfigDto`, `PaisDto`, `ApiKeyDto`, `UsuarioDto`, `RolAsignadoDto`, `RolDto`, `PermisoDto`, `RegistroAuditoriaDto`, `DiffCampoDto` | 05 §4 |
| Evento público | `configuracion.cambiada`, `identidad.evento` (solo referencia, scope `auditoria:leer`) | 05 §7 |
| Permiso | `auditoria:leer` (Administrador, Cumplimiento, Auditor), `plataforma:operacion`, `plataforma:soporte` (solo con concesión vigente, B-6) | 07 §3.2 |
| Infra | `ISesionDeEmpresa` (T-3) como único origen de `tenantId`/`empresaId` en `validacion`; esquemas `plataforma`/`boveda`/`validacion`/`identidad` (T-1); *spike* del store único conjoined (T-2) | 04 §4.1 |
| Pantalla | `/auditoria` con pestañas Dominio / Configuración (diff antes/después) / Identidad / Accesos | 06 §3 |
| Prueba | Arquitectura: todo comando de `Configuracion`/`Plataforma`/`Identidad` produce su registro; integración: rotar API key ⇒ `RegistroDeCambioDeConfiguracion(Rotado)` + `RegistroDeIdentidad(ApiKeyUsada)` en el primer uso | 03 §2.1 |

### H2 — Bóveda del tercero

#### 2.2.1 Agregados y eventos
| Nombre | Campos clave | Invariantes probadas |
|---|---|---|
| `Tercero` (CRUD + auditoría, `boveda`) | `identificacion (pais, tipo, numero, dv)`, `tipoPersona`, `nombre`, `direccionPrincipal`, `contactos[]`, `sucesorDe`, `esExtranjero` | Única `(pais, tipo, numero)`; eventos `TerceroCreado`, `TerceroActualizado`, `SucesorRegistrado`; país ISO 3166-1 alpha-2 |
| `RelacionEntreTerceros` (`boveda.relacion_entre_terceros`, ADR-0010 / B-3; 03 §4.5) | `RelacionId, OrigenTerceroId (persona natural), DestinoTerceroId, Tipo: TipoRelacionTerceros { RepresentanteLegal, Suplente, Socio, BeneficiarioFinal, MiembroDe }, Principal, VigenteDesde, VigenteHasta?, Fuente: DeclaradaPorTercero\|ExtraidaDeDocumento(VersionId)\|Importada, ConfirmadaPor?` | Las personas relacionadas son terceros con bóveda propia; una sola relación vigente por `(origen, destino, tipo)`; eventos `RelacionRegistrada`, `RelacionMarcadaPrincipal`, `RelacionTerminada`, `RelacionConfirmada`, `CambioDeRelacionDetectado(Confianza)` |
| `ConsentimientoTercero` (`boveda.consentimiento_tercero`) | `terceroId, alcance: Plataforma\|Ia, version, evidencia` (el de empresa viaja en `ConsentimientosRegistrados` del vínculo, H4) | Sin consentimiento de plataforma no se comparte ni se lee con IA (B-1, B-8); eventos `ConsentimientoPlataformaRegistrado`, `ConsentimientoIaRegistrado` |
| `TipoDocumentoCanonico` (CRUD versionado, `plataforma`) | 05 `TipoDocumentoCanonicoDto` | `codigo` único; publicar crea `version+1` |
| `Documento` (event-sourced) | 03 §4.1 | Única versión `Actual`; hash único por documento; `Devuelta` nunca se promueve |
| Eventos | `DocumentoCreado`, `VersionCargada`, `VersionDuplicadaDetectada`, `AnalisisDeVersionIniciado`, `HechosDeVersionRegistrados(ConIa)`, `VersionActualCambiada(VersionId, VersionAnteriorId?, Comparacion, CamposCambiados[], CamposCriticosCambiados[], ConIa)` (sin `RequiereRevalidacion`: decide la saga por empresa, T-7), `VersionActualRestituida` (al retirar la actual, I-11), `VersionDevueltaAlTercero(Motivo)`, `DatosDeVersionCorregidos`, `VersionCompartida` (solo al aceptar la invitación, ADR-0009), `ComparticionCongelada` / `ComparticionReanudada` (bloqueo y desbloqueo, B-5; sin exclusión por el tercero, A-8), `VersionRetirada`, `RetencionLegalMarcada` | Idempotencia de `HechosDeVersionRegistrados` por `(VersionId, TipoHecho)`; `Comparacion { SinAnterior, Identica, Distinta, SinCambios, CambiosDetectados }`; `Antivirus { Limpio, Infectado, Cuarentena, NoDisponible }` (T-11) |
| `CuentaBancaria` (entidad, `boveda`) | 03 §4.4 | `CuentaBancariaRegistrada`, `CertificacionAsociadaACuenta`, `CuentaBancariaRetirada` |
| `DocumentoAccedido` (auditoría) | `versionId, tenantId?, empresaId?, actor, ip, motivo` | Una fila por URL prefirmada emitida |

#### 2.2.2 Comandos y endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `GET /plataforma/tipos-documento/canonicos` · `POST` · `PUT /{id}` · `POST /{id}/publicar` · `GET /{id}/versiones` | `plataforma:catalogo` | `TipoDocumentoCanonicoDto` | 200/201 | `catalogo.publicado` | Sí |
| `GET /plataforma/propuestas-tipo` · `POST /plataforma/propuestas-tipo/{id}/aprobar` | `plataforma:catalogo` | `PropuestaTipoDto` | 200 | — | Sí |
| `GET /catalogos/esquemas-formulario` | autenticado | → `EsquemaFormularioDto[]` | 200 | — | — |
| `GET /terceros` · `POST /terceros` · `GET /terceros/{id}` · `PATCH /terceros/{id}` | `terceros:leer`/`escribir` | `TerceroDto` | 200/201; 409 `tercero-duplicado`; 400 `identificacion-invalida` | `tercero.creado` | Sí |
| `GET /terceros/por-identificacion?pais=&tipo=&numero=` | `terceros:leer` | → `TerceroDto` | 200/404 | — | — |
| `GET /terceros/{id}/documentos` | `documentos:leer` | → `DocumentoVistaEmpresaDto[]` | 200 | — | — |
| `GET /terceros/{id}/cuentas-bancarias` | `terceros:leer` | → `CuentaBancariaDto[]` | 200 | — | — |
| `GET /terceros/{id}/relaciones` · `POST /terceros/{id}/relaciones` · `PUT /terceros/{id}/relaciones/{rid}` · `POST /terceros/{id}/relaciones/{rid}/terminar` (ADR-0010) | `terceros:leer` / `relaciones:escribir` | `RelacionEntreTercerosDto` | 200/201; 409 `relacion-duplicada`; 404 tercero destino | `tercero.relacion_cambiada` | Sí |
| `POST /documentos` (carga directa a la bóveda, sin expediente; usada por el importador y las integraciones; el tercero usa `POST /public/v1/boveda/documentos`) | tercero / `documentos:escribir` | `IniciarCargaDto` + `tipoCanonico` → `CargaPrefirmadaDto` | 201; 200 `duplicado`; 413; 415 | — | Sí |
| `POST /documentos/{docId}/versiones/{n}/confirmar` | tercero / `documentos:escribir` | — → `{estado, stream}` | 202; 422 `hash-no-coincide` | `documento.hechos_registrados`, `documento.version_nueva` o `documento.version_devuelta` | Sí |
| `GET /documentos/{id}` · `GET /documentos/{id}/versiones` · `GET /documentos/{id}/versiones/{n}` · `GET …/hechos` | `documentos:leer` | → `DocumentoDto`, `VersionDocumentoDto`, `HechosDto` | 200; 403 `version-no-compartida` | — | — |
| `GET /documentos/{id}/versiones/{n}/descarga` · `GET …/previsualizacion?pagina=` | `documentos:leer` | → `DescargaDto` | 200 | — | — |
| `PATCH /documentos/{id}/versiones/{n}/datos` | `documentos:corregir-datos` | `CorreccionDatoDto` | 200 | `documento.datos_corregidos` | No (`If-Match`) |
| `POST /documentos/{id}/versiones/{n}/retirar` | tercero / Administrador | `{motivo}` | 200; 409 `version-referenciada` | — | Sí |
| `POST /public/v1/enlaces/{token}/canjear` · `POST /public/v1/enlaces/{token}/otp` | público | → `SesionTerceroDto` | 200; 410 `enlace-usado`/`enlace-expirado` | `expediente.invitacion_aceptada` (desde H4) | Sí |
| `GET /public/v1/boveda` · `GET /public/v1/boveda/documentos/{id}` | sesión tercero | → `BovedaTercero` | 200 | — | — |
| `POST /public/v1/tercero/datos-maestros` | sesión tercero | `DatosMaestrosDto` → versión de formulario `DATOS_MAESTROS` | 202 | `documento.version_nueva` / `documento.version_devuelta` | Sí |
| `GET /public/v1/tercero/relaciones` · `POST /public/v1/tercero/relaciones` · `POST …/{rid}/terminar` (el tercero declara representante, socios, UBO; cada persona recibe su propio enlace) | sesión tercero | `RelacionEntreTercerosDto` | 200/201 | `tercero.relacion_cambiada` | Sí |

Movidos fuera de H2 tras la segunda relectura: `POST /public/v1/autorregistro` → **H4** (T-13) y `POST /public/v1/boveda/cuentas-bancarias` → **H6** (cuentas designadas, #25).

#### 2.2.3 DTOs nuevos
| DTO | Campos |
|---|---|
| `PropuestaTipoDto` | `id*`, `tenantId*`, `codigoPropuesto*`, `nombre*`, `justificacion*`, `estado*: enum(Pendiente\|Aprobada\|Rechazada)`, `tipoCanonicoResultante` |
| `EsquemaFormularioDto` | `tipoCanonico*`, `version*`, `esquema*: json (JSON Schema)`, `camposClave*: string[]`, `camposCriticos*: string[]` |
| `DatosMaestrosDto` | `nombre`, `direccionPrincipal: DireccionDto`, `contactos: ContactoDto[]`, `soporte: {documentoId, version}` (los representantes van por `relaciones`, ADR-0010) |
| `RelacionEntreTercerosDto` | `id*`, `terceroRelacionadoId*` (o `identificacion*` para crearlo), `tipo*: TipoRelacionTerceros`, `desde*`, `hasta`, `participacion`, `confirmada*: bool`, `vinculoEnlace` (solo al crear) |
| Reutilizados de 05 | `TerceroDto` (con `relaciones[]`), `ContactoDto`, `DireccionDto`, `SucursalDto`, `TipoDocumentoCanonicoDto`, `ReglaFrescuraDto`, `CuentaBancariaDto`, `IniciarCargaDto`, `CargaPrefirmadaDto`, `DocumentoDto`, `VersionDocumentoDto`, `HechosDto`, `CorreccionDatoDto`, `DescargaDto`, `DocumentoVistaEmpresaDto`, `SesionTerceroDto` |

#### 2.2.4 Eventos públicos
`catalogo.publicado` · `tercero.creado` · `tercero.relacion_cambiada` · `tercero.datos_maestros_cambiados` · `documento.hechos_registrados` · `documento.version_nueva` (payload 05 §6.4; en H2 `efectoEnEmpresa` viene `null` porque aún no hay vínculos) · `documento.version_devuelta` · `documento.datos_corregidos`. SSE: `/public/v1/expedientes/{id}/stream` se sustituye en H2 por `/public/v1/boveda/stream` (ver huecos).

#### 2.2.5 Proyecciones
| Proyección | Tipo | Campos | Consumidor |
|---|---|---|---|
| `BovedaTercero` | Inline | 06 §5 | `public:/boveda`, `web:/terceros/:id` |
| `ReferenciasDeVersion` | Inline | `versionId, referencias[]{tipo: Aceptacion\|Certificado, tenantId, empresaId, id}` | `RetirarVersion` |
| `TimelineDocumento` | Async | `eventos[]{tipo, fecha, actor, version, detalle}` | `web:/terceros/:id` |
| `FrescuraDocumentoTercero` (estructura; se alimenta desde H6) | Async | 03 §10 | — |
| `UsoTenant` (+ `gbAlmacenados`, `lecturasIa`) | Async | — | `web:/plataforma/uso` |

#### 2.2.6 Alertas
| Hecho | Plantilla | Destinatario | Canal |
|---|---|---|---|
| Versión devuelta al tercero | `observacion-automatica` (variante "devuelta") | Tercero | correo + campana |
| Aceptación de la autorización de la plataforma (incluye lectura IA, P-6-5 / B-8: sin ella no hay lectura) | `consentimiento-plataforma` | Tercero | correo (constancia) |
| Persona relacionada invitada a su propia bóveda | `invitacion` (variante "persona relacionada") | Persona (tercero) | correo |

#### 2.2.7 Pantallas
| Pantalla | Componentes | Lecturas |
|---|---|---|
| `web:/plataforma/catalogo` (tipos canónicos, esquemas, propuestas) | tablas, `FormularioDinamico` (vista previa) | `GET /plataforma/tipos-documento/canonicos`, `GET /catalogos/esquemas-formulario` |
| `web:/terceros/:id` básico (identificación, documentos actuales, hechos, timeline, personas relacionadas) | `TarjetaDocumento`, `TarjetaPersonaRelacionada`, `PanelIA`, `VisorDocumento`, `LineaDeTiempo` | `GET /terceros/{id}`, `GET /terceros/{id}/documentos`, `GET /terceros/{id}/relaciones`, `GET /documentos/{id}/versiones/{n}/hechos` |
| `public:/e/:token` (canje, OTP, aceptación de la plataforma con `AceptacionConEvidencia`) | `AceptacionConEvidencia` | `POST /public/v1/enlaces/{token}/canjear` |
| `public:/boveda` básica (subir, ver versión actual y hechos; estado por empresa vacío hasta H4) | `Dropzone`, `PanelIA`, `TarjetaDocumentoBoveda`, `FormularioDinamico` | `GET /public/v1/boveda` |
| `public:/personas` (mis personas relacionadas; cada una con su propia bóveda) | `TarjetaPersonaRelacionada`, `SelectorIdentificacion` | `GET /public/v1/tercero/relaciones` |
| Paquete `forms` (`FormularioDinamico` sobre `@rjsf/mui`, `DiffCampos`) | — | `GET /catalogos/esquemas-formulario` |

#### 2.2.8 Jobs/sagas
| Nombre | Dispara | Pasos |
|---|---|---|
| Saga `VerificacionDocumental` | `VersionCargada` → `IniciarAnalisis(ConIa)` | hechos sin IA (hash, antivirus T-11, MIME real, páginas, preview WebP T-17, comparación por hash) → si IA habilitada **y el tercero dio consentimiento** (B-8; coste de plataforma, B-2): preclasificación (`claude-haiku-5-5`) → extracción (`claude-opus-5-5`/`sonnet-5-5` por tipo) → comparación por campos → señales básicas de manipulación → `PromoverAActual` o `DevolverVersion` |
| Worker `Previews` | `VersionCargada` | PDF→PNG, miniaturas, texto nativo |
| Agregación `UsoTenant` | diaria | GB y lecturas IA |

#### 2.2.9 Configuración
| Clave | Tipo | Default |
|---|---|---|
| `Ia:Habilitada` (plataforma) | bool | `false` hasta configurar clave |
| `Ia:ModeloPreclasificacion`, `Ia:ModeloExtraccion` | string | `claude-haiku-5-5`, `claude-opus-5-5` |
| `Ia:TimeoutSegundos`, `Ia:MaxReintentos` | int | `120`, `2` |
| `Ia:FailClosed` | bool | `true` |
| `Antivirus:Modo` | enum(`Obligatorio\|Opcional\|Deshabilitado`) | `Opcional` dev / `Obligatorio` prod |
| `Retencion:GraciaDiasNoReferenciadas` | int | `90` |
| `TipoDocumentoCanonicoDto.*` (semilla canónica, T-10) | — | **03 Anexo A** (`RUT`, `CAMARA_COMERCIO`, `CEDULA`, `PASAPORTE`, `CERTIFICADO_ANTECEDENTES`, `ESTADOS_FINANCIEROS`, `DECLARACION_RENTA`, `CERTIFICACION_BANCARIA`, `CUENTA_BANCARIA`, `DATOS_MAESTROS`, `INFO_TRIBUTARIA`, `INFO_LEGAL`, `BENEFICIARIO_FINAL`, `DECLARACION_PEP`, `SG_SST`, `HOJA_DE_VIDA`, `TITULO_ACADEMICO`, `POLIZA`, `AUTORIZACION_TRATAMIENTO_DATOS`, `TERMINOS_Y_CONDICIONES`, `FORMULARIO_SARLAFT`, `AFILIACION_SEGURIDAD_SOCIAL`, `PAZ_Y_SALVO`, `FORMATO_VINCULACION`) |

#### 2.2.10 Pruebas
- Unit: `Documento` — promoción sin IA (hash+AV+MIME), con IA (tipo+legible+identidad); dedupe por hash; `Devuelta` no promueve; comparación por campos (`Identica`/`SinCambios`/`CambiosDetectados`); retirar la actual ⇒ `VersionActualRestituida` a la anterior; `RelacionEntreTerceros` sin duplicados y con cierre por `hasta`.
- Integración: `Antivirus = Infectado` ⇒ cuarentena y `VersionDevueltaAlTercero`; `NoDisponible` ⇒ promoción con `pendienteRevisionHumana=true` (T-11); sin consentimiento del tercero la saga no llama a la IA (B-8).
- Integración (Testcontainers Postgres+MinIO): subida en dos pasos con hash verificado; `hash-no-coincide` → 422; descarga sin compartición → 403 `version-no-compartida` y con compartición registra `DocumentoAccedido`; versión ilegible (fixture) → `VersionDevueltaAlTercero` sin eventos a empresas.
- IA con `VerificadorFixture`: extrae `fechaExpedicion` de un RUT y detecta cambio de `direccion` entre v1 y v2; sin clave ⇒ `ConIa=false`, `pendienteRevisionHumana=true` y la carga no se bloquea.
- Contract: nuevos esquemas presentes en el snapshot.
- Carga básica: 50 subidas concurrentes de 20 MB sin degradar `GET /health/ready`.

#### 2.2.11 Efectos
Hacia atrás: `UsoTenant` (H1) suma GB y lecturas. Hacia adelante: define los hechos (`HechosDto`) que H4 cruza en `HechosDeVersionAplicadosARequisito` y H7 usa en el motor de decisión; `VersionActualCambiada` (con `CamposCriticosCambiados[]`) es el disparador de la saga `Revalidacion` (H6), que decide **por empresa** si la aceptación queda `Desactualizada` (T-7); las personas relacionadas (ADR-0010) son los terceros que H7 verifica (UBO/PEP) y H4 exige como requisito (`CEDULA` del representante).

---

#### H2 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Endpoint | `GET /public/v1/boveda/stream` (SSE de la bóveda: `documento.hechos_registrados`, `documento.version_nueva`, `documento.version_devuelta`, `vinculo.revalidado`, `expediente.requisito_veredicto`, `documento.por_vencer`, `vinculo.cuenta_designada`, `exportacion.lista`) | 05 §2.9, §3.4 |
| Endpoint | `POST /public/v1/boveda/documentos` (tercero) y `POST /documentos` (importador H2 con `importacion:escribir`; integraciones en H9 con `documentos:escribir`): carga directa a bóveda sin expediente, paso 1; `IniciarCargaDto.tipoCanonico*`, `terceroId*` | 05 §2.5, §2.9, §4 |
| Endpoint | `GET /public/v1/boveda/cuentas-bancarias` (`CuentasTercero`) | 05 §2.9 |
| DTO | `IniciarCargaDto` ampliado (`tipoCanonico`, `terceroId`, `plantillaVersionId`), `PropuestaTipoDto`, `EsquemaFormularioDto`, `DatosMaestrosDto`, `AceptacionDto` | 05 §4 |
| Saga | `VerificacionDocumental` arranca con `IniciarAnalisis → AnalisisDeVersionIniciado` | 04 §5 |
| Proyección | `CuentasTercero` (inline) | 03 §14 |
| Entidad / endpoint | `RelacionEntreTerceros` + `/terceros/{id}/relaciones*` + `/public/v1/tercero/relaciones*` (ADR-0010, B-3); `ConsentimientoTercero` con alcance `Ia` (B-8) | 02 §4.5, 03 §4.5, 05 |
| Semilla | 03 Anexo A sustituye la tabla del cuestionario; códigos retirados `CERT_BANCARIA`, `INFO_BANCARIA`, `TRATAMIENTO_DATOS`, `INFO_GENERAL`, `CEDULA_REPRESENTANTE`, `POLIZA_RC`, `INFO_FINANCIERA` | 03 Anexo A |

### H3 — Configuración y motor de exigencia

#### 2.3.1 Entidades y eventos
| Nombre | Campos clave | Invariantes probadas |
|---|---|---|
| `TipoDocumentoTenant` (CRUD, `validacion`) | 05 `TipoDocumentoTenantDto` (sin `iaHechos`, B-2) | Restringir ámbito Global→PorVinculo sí, ampliar ⇒ 422 `ambito-no-ampliable`; `permiteRatificacion` nunca más laxo que el canónico |
| `TipoDocumentoEmpresa` (override por empresa, `validacion.tipo_documento_empresa`, B-4) | `tipoDocumentoTenantId, empresaId, reglaFrescura?, diasRevalidacion?, politicaDesactualizada?, nivelRevision?` | Solo puede ser **más estricto** que el tenant ⇒ 422 `override-mas-laxo`; la regla del flujo puede ser más estricta que la empresa |
| `FlujoPorDefecto (empresa, tipoVinculo)` y `ReglaDeSeleccionDeFlujo` | 03 §12.1 | Obligatorio antes de publicar reglas ⇒ 422 `falta-flujo-por-defecto` |
| `Area`, `Motivo`, `TipoVinculo` | 05 DTOs | `codigo` único por tenant |
| `Flujo` / `FlujoVersion` (CRUD versionado) | 05 `FlujoDefinicionDto` | Publicar congela `reglas`, `etapas`, políticas; `estado` `Borrador→Publicado→Archivado` |
| `ReglaDeExigencia`, `Condicion` | 03 §12 | Evaluador puro: misma entrada ⇒ misma salida |
| `PlantillaDocumento` (versionada, `boveda`/`tenant-{id}`) | `tipoTenantId, version, claveS3, hash` | La versión cargada guarda `plantillaVersionId` (P-2-6) |
| Eventos de auditoría | `TipoDocumentoTenantCreado/Actualizado`, `FlujoPublicado(FlujoId, Version)`, `AreaCreada`, `MotivoCreado` | — |

#### 2.3.2 Endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `GET /tipos-documento` · `POST` · `PUT /{id}` · `GET /{id}/esquema` | `configuracion:escribir` (`leer` para GET) | `TipoDocumentoTenantDto`; `esquema` → `EsquemaFormularioDto` fusionado | 200/201; 422 `ambito-no-ampliable` | — | Sí |
| `POST /tipos-documento/propuestas` | `configuracion:escribir` | `PropuestaTipoDto` | 202 | — | Sí |
| `POST /tipos-documento/{id}/plantilla` (subida en dos pasos reutilizando `IniciarCargaDto`) | `configuracion:escribir` | → `CargaPrefirmadaDto` | 201 | — | Sí |
| `GET /areas` · `POST` · `PUT /{id}` | `configuracion:escribir` | `AreaDto` | 200/201 | — | Sí |
| `GET /motivos` · `POST` · `PUT /{id}` · `GET /catalogos/motivos-base` | `configuracion:escribir` | `MotivoDto` | 200/201 | — | Sí |
| `GET /tipos-vinculo` · `POST` | `configuracion:escribir` | `TipoVinculoDto` | 200/201 | — | Sí |
| `GET /empresas/{id}/tipos-documento` · `PUT /empresas/{id}/tipos-documento/{tipoId}` · `DELETE …/{tipoId}` (overrides B-4) | `configuracion:escribir` | `TipoDocumentoEmpresaDto` | 200; 422 `override-mas-laxo` | — | No (`If-Match`) |
| `GET /flujos/por-defecto` · `PUT /flujos/por-defecto` | `configuracion:escribir` | `FlujoPorDefectoDto[]` | 200; 422 `falta-flujo-por-defecto` | — | No |
| `GET /flujos` · `POST` · `GET /{id}` · `PUT /{id}` · `POST /{id}/publicar` · `GET /{id}/versiones` · `GET /{id}/versiones/{n}` | `configuracion:escribir` | `FlujoDefinicionDto` | 200/201; 409 `estado-invalido` | `flujo.publicado` | Sí |
| `POST /flujos/{id}/simular` | `configuracion:leer` | `SimulacionDto` → `RequisitoCalculadoDto[]` | 200 | — | No |
| `POST /flujos/{id}/versiones/{n}/aplicar-a-abiertos` (operativo desde H4) | Administrador | — | 202 | `expediente.requisitos_recalculados` | Sí |

#### 2.3.3 DTOs nuevos
| DTO | Campos |
|---|---|
| `TipoDocumentoEmpresaDto` | `tipoDocumentoTenantId*`, `empresaId*`, `reglaFrescura: ReglaFrescuraDto`, `diasRevalidacion`, `politicaDesactualizada: PoliticaDesactualizada`, `nivelRevision: NivelRevision` (solo lo que endurece) |
| `FlujoPorDefectoDto` | `empresaId*`, `tipoVinculoId*`, `flujoId*` |
| `RequisitoCalculadoDto` | `tipoDocumentoTenantId*`, `codigo*`, `tiposAceptados: string[]`, `obligatorio*`, `critico*`, `areasQueRevisan*: string[]`, `aprobadoresRequeridos*`, `nivelRevision*`, `reglaFrescura*: ReglaFrescuraDto`, `reglaId*`, `motivoAplicacion*: string` (predicado que la activó) |
| Reutilizados de 05 | `TipoDocumentoTenantDto`, `TipoVinculoDto`, `AreaDto`, `MotivoDto`, `FlujoDefinicionDto`, `EtapaDto`, `ReglaExigenciaDto`, `CondicionDto`, `VerificacionRequeridaDto`, `SimulacionDto` |

#### 2.3.4 Eventos públicos
`flujo.publicado` (`empresaId = null`).

#### 2.3.5 Proyecciones
`FlujosResumen` (`id, codigo, nombre, versionPublicada, expedientesActivos`) — async, para `web:/flujos` (los contadores se llenan desde H4).

#### 2.3.6 Alertas
Ninguna.

#### 2.3.7 Pantallas
| Pantalla | Componentes | Lecturas |
|---|---|---|
| `web:/flujos`, `web:/flujos/:id/editor` (reglas con `CondicionDto`, áreas/etapas/SLA, `ReglaFrescura`, `NivelRevision`, verificaciones, plantilla de certificado, "Vista del tercero" con `?preview=`) | editor de condiciones, `FormularioDinamico` (vista previa) | `GET /flujos/{id}`, `POST /flujos/{id}/simular` |
| `web:/configuracion/tenant/tipos-documento`, `/configuracion/tenant/formularios` (extensión de campos privados), `/configuracion/empresa/areas`, `/configuracion/empresa/tipos-documento` (overrides más estrictos, B-4), `/configuracion/empresa/flujos-por-defecto`, `/configuracion/tenant/motivos` | tablas y formularios | endpoints 2.3.2 |
| `public:/expediente/:id?preview=` (render de requisitos simulados) | `ChipEstado`, `BarraProgresoEtapas` | `POST /flujos/{id}/simular` |

#### 2.3.8 Jobs/sagas
Ninguno.

#### 2.3.9 Configuración
Semilla "Habilitación de proveedores": áreas `compras`, `legal`, `financiera`, `cumplimiento` (+ `sst` opcional); `aprobadoresRequeridos = 2` en `CERTIFICACION_BANCARIA`; `PlazoCargueDias 30`, `RecordatoriosCargue [15,7,1]`, `PoliticaDevolucion Inmediata`, `PoliticaNuevaVersion InvalidarSoloRequisito`, `PoliticaVigenciaVinculo MinimoDeRequisitosCriticos`, `PoliticaDesactualizada BloquearTrasGracia` (A-2, `diasRevalidacion 15`), `SeparacionDeFunciones false`. Atributos de vínculo de la semilla: `categorias`, `etiquetas`, `especialidades`, `montoEstimado`, `nivelRiesgo`.

#### 2.3.10 Pruebas
- Unit: tabla de casos de `IEvaluadorDeExigencia` (PN/PJ, país, etiquetas, tipo de vínculo, monto, consorcio con miembros) 100 % verde.
- Integración: publicar `FlujoVersion` y verificar inmutabilidad (PUT ⇒ 409); `PUT /tipos-documento` ampliando ámbito ⇒ 422; override de empresa más laxo ⇒ 422 `override-mas-laxo`; resolución efectiva tenant → empresa → regla devuelve siempre el valor más estricto (tabla de casos).
- E2E: editor crea flujo, publica y la vista previa del tercero muestra los requisitos simulados.

#### 2.3.11 Efectos
Hacia atrás: cualquier atributo nuevo del tercero (H2) debe exponerse en el contrato de atributos del evaluador. Hacia adelante: `RequisitosCalculados` (H4) es el snapshot de lo que aquí se define; `ReglaFrescura`, `DiasAviso`, `DiasGracia`, `diasRevalidacion`, `politicaDesactualizada` se consumen en H6; `nivelRevision`, `porcentajeMuestreo`, `camposCuyoCambioExigeHumano` en H7.

---

#### H3 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Entidad | `ReglaDeSeleccionDeFlujo { Orden, Condicion, FlujoId }`, `FlujoPorDefecto (empresa, tipoVinculo)` obligatorio; `IEvaluadorDeSeleccion` puro con tabla de casos | 03 §12.1 |
| Endpoint | `GET/PUT /flujos/reglas-seleccion`, `POST /flujos/reglas-seleccion/simular`, `POST /tipos-documento/{id}/plantilla` · `GET …/plantilla`, `GET /tipos-vinculo/{id}` · `PUT`, `GET /flujos/resumen` | 05 §2.2, §2.4 |
| DTO | `ReglaSeleccionFlujoDto`, `PlantillaDto`, `TipoVinculoDto`, `RequisitoCalculadoDto`, `FlujosResumen` | 05 §4, 03 §14 |
| Error | `422 falta-flujo-por-defecto` al publicar o guardar reglas sin flujo por defecto | 05 §2.2 |
| Pantalla | Editor de flujos: pestaña "Reglas de selección" + simulador; `/flujos` consume `FlujosResumen` | 06 §3 |
| Auditoría | Cada regla, flujo, tipo o área guardado ⇒ `RegistroDeCambioDeConfiguracion` con diff | 03 §2.1 |
| Prueba | Tabla de casos de selección (monto bajo → flujo simplificado; PEP → flujo intensificado; sin coincidencia → por defecto); publicar sin flujo por defecto ⇒ 422 | 03 §12.1 |

### H4 — Expediente inicial y veredictos

#### 2.4.1 Agregados y eventos
| Nombre | Eventos | Invariantes probadas |
|---|---|---|
| `Vinculo` | `VinculoCreado`, `InvitacionEnviada`, `InvitacionAceptada`, `ConsentimientosRegistrados`, `AtributosDeVinculoActualizados`, `ExpedienteAbierto`, `ExpedienteCerrado`, `VersionAceptadaRegistrada`, `VersionNoAdmitidaEnVinculo` (B-7), `EstadoDeVinculoRecalculado` (al cerrar cualquier expediente, I-15), `VinculoActivado`, `VinculoBloqueado` (⇒ `ComparticionCongelada` + `CertificadoSuspendido`, B-5), `VinculoDesbloqueado` (⇒ `ComparticionReanudada` + `CertificadoRestablecido`), `VinculoDesvinculado`, `VinculoReintegrado`, `VinculoAnulado` | Clave `(tenant, empresa, tercero, tipo)` única; un expediente abierto; versión no admitida no se re-propone (409 `version-no-admitida-en-vinculo`); nada legible fuera de `EmpresaId`; `EstadoVinculo { Invitado, EnValidacion, Activo, PorVencer, Vencido, Bloqueado, Desvinculado, Anulado }` |
| `Expediente` | `ExpedienteAbierto`, `RequisitosCalculados`, `RequisitosRecalculados`, `VersionAdjuntadaARequisito`, `VeredictosInvalidadosPorNuevaVersion`, `HechosDeVersionAplicadosARequisito`, `RequisitoEximido`, `ExpedienteEnviadoARevision`, `EtapaActivada`, `RevisorAsignado`, `ConflictoDeInteresDetectado`, `VeredictoDeRequisitoEmitido`, `VeredictoDeAreaEmitido`, `EtapaAprobada`, `ExpedienteObservado`, `ExpedienteReenviado`, `AlertaReenviosEmitida`, `ExpedienteRechazado(AreaId, Motivos[], Justificacion)`, `VeredictoAnuladoPorAdministrador`, `ExpedienteAprobado`, `ExpedienteCaducado`, `ExpedienteReabierto`, `ExpedienteAnulado`, `SlaIncumplido`, `SlaEscalado`, `RequisitoAgregadoAExpediente`, `FlujoSeleccionado(Origen)` | Veredicto sobre versión no actual ⇒ 409; `Veredicto { Aprobado, Observado }` + `NoAdmitirEstaVersion` (B-7): `Observado` con ≥ 1 motivo; **rechazar es del expediente**, solo por un área con `expedientes:rechazar` (403 `area-no-puede-rechazar`); `OrigenVeredicto { Humano, Sistema, Administrador }`; `AprobadoresRequeridos` distintos y sin conflicto de intereses (`UsuarioDto.identificacion` ≠ tercero ni persona relacionada, I-21); `Rechazado` terminal; `EvaluarCierre` exige etapas aprobadas y verificaciones bloqueantes satisfechas; `EstadoRequisito { Pendiente, Cargado, EnRevision, Observado, Aprobado, Eximido, NoAplica, Vencido, Desactualizado }` |
| `Certificado` (stub: emisión real en H5) | — | — |

#### 2.4.2 Endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `GET /vinculos` · `POST /vinculos` · `GET /vinculos/{id}` · `PATCH /vinculos/{id}/atributos` | `vinculos:leer`/`escribir` | `VinculoDto` | 200/201; 409 `vinculo-duplicado`, `tercero-bloqueado` | `vinculo.creado`, `vinculo.atributos_actualizados` | Sí |
| `POST /vinculos/{id}/desvincular` · `/reintegrar` · `/anular` · `/bloquear` · `/desbloquear` | `vinculos:escribir` / `vinculos:bloquear` | `{motivo, hasta?}` | 200; 409 `estado-invalido` | `vinculo.estado_cambiado`, `vinculo.bloqueado`, `vinculo.desbloqueado`, `vinculo.desvinculado`, `vinculo.reintegrado`, `vinculo.anulado` | Sí |
| `GET /vinculos/{id}/versiones-aceptadas` · `GET /vinculos/{id}/historial` | `vinculos:leer` | → `VersionAceptadaDto[]`, `TimelineVinculo` | 200 | — | — |
| `POST /expedientes` | `expedientes:escribir` | `CrearExpedienteDto` (`flujo` opcional: regla de selección o por defecto) → `ExpedienteDto` (`origenFlujo`, `requisitosCubribles` = cuántos requisitos cubriría la bóveda, **sin nombres ni contenido** hasta que el tercero acepte, ADR-0009) | 201; 409 `expediente-abierto-existente`, `tercero-bloqueado`; 422 `falta-flujo-por-defecto` | `expediente.abierto`, `expediente.flujo_seleccionado` (+`vinculo.creado`, `tercero.creado`) | Sí |
| `GET /expedientes` (filtros `estado, tipo, flujoId, area, revisor, misPendientes, vencimiento, empresaId`) | `expedientes:leer` | → `PaginaDto<BandejaExpedienteDto>` | 200 | — | — |
| `GET /expedientes/{id}` · `GET /expedientes/{id}/requisitos` · `GET /expedientes/{id}/requisitos/{rid}` · `GET /expedientes/{id}/historial` | `expedientes:leer` | → `ExpedienteDto`, `RequisitoDto`, `TimelineExpediente` | 200 | — | — |
| `POST /expedientes/{id}/invitaciones` | `expedientes:escribir` | `InvitacionDto` → `{url, expira}` | 201 | `expediente.invitacion_enviada` | Sí |
| `POST /public/v1/invitaciones/{expedienteId}/aceptar` (ADR-0009 / B-1: el tercero acepta y autoriza; recién entonces la bóveda se comparte con la empresa) | sesión tercero | `AceptarInvitacionDto` (consentimientos con evidencia) → `ExpedientePublicoDto` | 200; 409 `ya-aceptada` | `expediente.invitacion_aceptada`, `documento.compartido` (uno por versión compartida), `expediente.documento_cargado` (requisitos cubiertos desde la bóveda) | Sí |
| `POST /public/v1/autorregistro` (T-13: autorregistro invisible; el tercero existe en la bóveda sin vínculo hasta que una empresa lo invite) | público (rate limit) | `AutorregistroDto` | 202 | `tercero.autorregistrado` | Sí |
| `POST /expedientes/{id}/requisitos/{rid}/documentos` · `POST /documentos/{docId}/versiones/{n}/confirmar` (en contexto de requisito) | tercero / `documentos:escribir` | H2 | H2 | H2 + `expediente.documento_cargado` | Sí |
| `POST /expedientes/{id}/requisitos/{rid}/formulario` | tercero | `EnviarFormularioDto` → `VersionDocumentoDto` | 201; 400 `esquema-invalido` | `documento.version_nueva`, `expediente.documento_cargado` | Sí |
| `POST /expedientes/{id}/requisitos/{rid}/eximir` | `requisitos:eximir` | `EximirDto` | 200 | `expediente.requisito_eximido` | Sí |
| `POST /expedientes/{id}/enviar-a-revision` · `/reenviar` · `/reabrir` | tercero / `expedientes:escribir` | — | 200; 409 | `expediente.enviado_a_revision`, `expediente.reenviado`, `expediente.reabierto`, `expediente.etapa_activada` | Sí |
| `POST /expedientes/{id}/requisitos/{rid}/veredicto` (`If-Match`) | `veredictos:emitir:area:{X}` | `VeredictoDto { veredicto*: Aprobado\|Observado, motivos[], noAdmitirEstaVersion: bool }` → `{requisito, expediente, notificaciones}` (B-7) | 200; 400 veredicto distinto de los dos; 409 `version-superada`, `requisito-no-en-alcance`, `version-no-admitida-en-vinculo`, `conflicto-de-interes`; 412 | `expediente.requisito_veredicto` | Sí |
| `POST /expedientes/{id}/areas/{areaId}/aprobar` · `POST /expedientes/{id}/devolver-al-tercero` | `veredictos:emitir:area:{X}` | `{observacion?}` | 200 | `expediente.area_aprobada`, `expediente.con_observaciones`, `expediente.aprobado` | Sí |
| `POST /expedientes/{id}/areas/{areaId}/rechazar` (B-7: rechazo del **expediente**, terminal) | `expedientes:rechazar:area:{X}` | `RechazarExpedienteDto { motivos*, justificacion* }` | 200; 403 `area-no-puede-rechazar`; 409 `estado-invalido` | `expediente.rechazado` | Sí |
| `POST /expedientes/{id}/asignaciones` · `DELETE /expedientes/{id}/asignaciones/{aid}` | `expedientes:asignar` | `AsignacionDto` | 201/204; 409 `conflicto-de-interes` | — | Sí |
| `POST /expedientes/{id}/veredictos/{vid}/anular` · `POST /expedientes/{id}/areas/{areaId}/aprobar-como-administrador` | Administrador | `{justificacion*}` | 200 | `expediente.requisito_veredicto` (origen `Administrador`) | Sí |
| `POST /expedientes/{id}/recalcular-requisitos` · `POST /expedientes/{id}/anular` | Administrador | `{motivo}` | 200 | `expediente.requisitos_recalculados`, `expediente.anulado` | Sí |
| `GET /novedades` · `POST /novedades/{id}/leer` | sesión | → `NovedadDto[]` | 200 | — | No |
| `GET /public/v1/expedientes/{id}` · `GET /public/v1/vinculos` | sesión tercero | → `ExpedientePublicoDto`, `VinculoPublicoDto[]` | 200 | — | — |
| SSE `GET /api/v1/eventos/stream` (filtrado por empresa) · `GET /public/v1/expedientes/{id}/stream` | sesión | eventos de §7 | 200 | — | — |

#### 2.4.3 DTOs nuevos
| DTO | Campos |
|---|---|
| `BandejaExpedienteDto` | 06 `BandejaRevision`: `empresaId*`, `expedienteId*`, `folio*`, `tipoExpediente*`, `tercero*{id,nombre,identificacion}`, `flujo*{id,nombre,version}`, `etapaActual*`, `etapas*[]{nombre,estado}`, `estadoPorArea*[]{areaId,estado,por,automatico,fecha}`, `vencimientoMasProximo{requisito,fecha,dias}`, `revisorAsignado`, `diasEnEtapa*`, `requisitosPendientes*`, `decididosPorIA*`, `ultimaActividad*` |
| `NovedadDto` | `id*`, `tipo*`, `titulo*`, `cuerpo*` (texto plano; sin HTML, #31), `enlace`, `leida*`, `fecha*`, `empresaId` |
| `TimelineEventoDto` | `tipo*`, `fecha*`, `actor*{nombre, rol, esTercero, esSistema}`, `requisito`, `antes`, `despues`, `comentario` |
| `RechazarExpedienteDto` | `motivos*: string[]`, `justificacion*` |
| `AceptarInvitacionDto` | `aceptaciones*: AceptacionDto[]` (T&C, autorización de la empresa), `cuentaDesignadaId?` |
| Reutilizados de 05 | `VinculoDto`, `VersionAceptadaDto`, `CrearExpedienteDto` (`flujo?`), `ExpedienteDto` (`origenFlujo`, `requisitosCubribles`), `RequisitoDto`, `VeredictoDto` (`Aprobado\|Observado`, `noAdmitirEstaVersion`), `VeredictoEmitidoDto`, `EvidenciaIaDto`, `EximirDto`, `AsignacionDto`, `EnviarFormularioDto`, `InvitacionDto`, `ExpedientePublicoDto`, `VinculoPublicoDto`, `AutorregistroDto` |

#### 2.4.4 Eventos públicos
`vinculo.creado`, `vinculo.atributos_actualizados`, `vinculo.estado_cambiado`, `vinculo.bloqueado`, `vinculo.desbloqueado`, `vinculo.desvinculado`, `vinculo.reintegrado`, `vinculo.anulado`, `tercero.autorregistrado` (T-13), `documento.compartido` (al aceptar la invitación, ADR-0009), `expediente.abierto`, `expediente.flujo_seleccionado`, `expediente.reabierto`, `expediente.invitacion_enviada`, `expediente.invitacion_aceptada`, `expediente.documento_cargado`, `expediente.enviado_a_revision`, `expediente.reenviado`, `expediente.etapa_activada`, `expediente.area_aprobada`, `expediente.requisito_veredicto` (`veredicto: Aprobado|Observado`, `noAdmitirEstaVersion`), `expediente.requisito_eximido`, `expediente.requisitos_recalculados`, `expediente.con_observaciones`, `expediente.reenvios_excedidos`, `expediente.aprobado`, `expediente.rechazado` (`areaId`, `motivos[]`, `justificacion`), `expediente.caducado`, `expediente.anulado`, `expediente.sla_incumplido`, `expediente.sla_escalado`, `expediente.recordatorio_cargue`. Todos con `empresaId`. Se publican por SSE y outbox; webhooks salientes desde H5.

#### 2.4.5 Proyecciones
| Proyección | Tipo | Consumidor |
|---|---|---|
| `VinculoResumen` | Inline | `web:/terceros/:id`, `GET /vinculos` |
| `VinculosPorTerceroYTipo` | Inline (global) | saga `Revalidacion` (H6) |
| `ExpedienteDetalle` | Inline | `web:/expedientes/:id`, `public:/expediente/:id` |
| `BandejaRevision` | Async | `web:/expedientes` |
| `TimelineExpediente`, `TimelineVinculo` | Async | pestañas Timeline/Historial |
| `NovedadesDelTercero`, `NovedadesDeUsuario` | Async | campana |
| `MisPendientes` (`porArea[]{areaId, cantidad, masAntiguoDias}`) | **Consulta compuesta** sobre `BandejaRevision` + `BandejaRevalidaciones` + `ColaDeExcepciones` (no es proyección, #19) | `web:/inicio`, `GET /mis-pendientes` |
| `KpisTenant`, `KpisFlujo`, `KpisEmpresa` (básicos: activos, tiempo promedio, por etapa) | Async diaria | `web:/inicio`, `web:/flujos/:id` |
| `BovedaTercero.porEmpresa[]` (ahora con estado por empresa) | Inline | `public:/boveda` |

#### 2.4.6 Alertas
| Hecho | Plantilla | Destinatario | Canal |
|---|---|---|---|
| Invitación / enlace | `invitacion` | Tercero | correo |
| Recordatorio de cargue 15/7/1 | `recordatorio-cargue` | Tercero | correo |
| Documento cargado | — | Tercero, revisor del área | campana |
| Requisito observado por humano | `observacion` | Tercero | correo + campana |
| Expediente aprobado (sin certificado hasta H5) | `aprobado` (plantilla propia de 05 §8; desde H5 la acompaña `certificado`) | Tercero | campana |
| Autorregistro recibido (T-13) | `autorregistro` | Tercero | correo (verificación) |
| Expediente rechazado / caducado | `rechazo` / `caducidad` | Tercero (correo), Administrador (campana) | — |
| SLA incumplido / escalado | `sla` | Responsable del área → Administrador | correo |
| Tercer reenvío del mismo requisito | — | Administrador | campana |
| Consentimientos registrados (T&C y autorización de la empresa) | `consentimiento-empresa` | Tercero | correo (constancia) |

#### 2.4.7 Pantallas
| Pantalla | Componentes | Lecturas |
|---|---|---|
| `web:/expedientes` (columnas por área; pestañas Vinculación / Todos) | `TablaExpedientes`, `Bandeja`, `ChipEstado` | `GET /expedientes` |
| `web:/expedientes/:id` | `VisorDocumento`, `PanelIA`, `PanelVeredicto` (Aprobar / Observar + "no admitir esta versión"; "Rechazar expediente" aparte, solo si el área puede, B-7), `LineaDeTiempo`, `BarraProgresoEtapas`, `DiffCampos` | `GET /expedientes/{id}`, `GET …/requisitos/{rid}`, `GET …/historial` |
| `web:/terceros/:id` (bloqueo, vínculo con esta empresa) | — | `GET /vinculos`, `POST /vinculos/{id}/bloquear` |
| `web:/inicio` (KPIs reales, mis pendientes) | `KpiCard` | `GET /kpis`, `MisPendientes` |
| `web:/configuracion/empresa/areas` (revisores por área) | — | `GET /areas` |
| `public:/e/:token` → aceptar invitación ("esta empresa verá tu bóveda; cubre N requisitos", ADR-0009) | `AceptacionConEvidencia` | `POST /public/v1/invitaciones/{expedienteId}/aceptar` |
| `public:/expediente/:id`, `public:/expediente/:id/requisito/:rid` | `ChipEstado`, `Dropzone`, `FormularioDinamico`, `PanelIA` simplificado, `AceptacionConEvidencia` | `GET /public/v1/expedientes/{id}`, SSE |
| `public:/registro` (autorregistro invisible, T-13) | `SelectorIdentificacion` | `POST /public/v1/autorregistro` |
| `public:/vinculos` (básico) | — | `GET /public/v1/vinculos` |
| Campana en ambas apps | — | `GET /novedades` |

#### 2.4.8 Jobs/sagas/scheduled
| Nombre | Dispara | Pasos |
|---|---|---|
| Saga `ProcesoDeValidacion` | `ExpedienteAbierto` (Vinculo) | crear stream `Expediente` → `RequisitosCalculados` → al `InvitacionAceptada`: `CompartirConVinculo` por cada versión actual que cubre un requisito (`VersionCompartida`, ADR-0009) → al cerrar (`ExpedienteAprobado\|Rechazado\|Caducado\|Anulado`): `CerrarExpediente` + `RecalcularEstadoDeVinculo` → (H5) certificado → webhook; `VinculoBloqueado` ⇒ `CongelarComparticion`, `VinculoDesbloqueado` ⇒ `ReanudarComparticion` (B-5); compensa con `AperturaDeExpedienteFallida` |
| Scheduled `VencerSlaRevision` | `EtapaActivada` | `RegistrarIncumplimientoSla` → `EscalarSla(ResponsableArea)` → `EscalarSla(Administrador)` |
| Scheduled `ExpirarMagicLink` | `InvitacionEnviada` | invalida token |
| Scheduled `RecordatorioCargue` (15/7/1) y `CaducarExpediente` | `ExpedienteAbierto` | `expediente.recordatorio_cargue`; `ExpedienteCaducado` |

#### 2.4.9 Configuración
`FlujoDefinicionDto.plazoCargueDias` (30), `recordatoriosCargue` ([15,7,1]), `EtapaDto.areas[].slaHoras`, `escalamientoA`, `separacionDeFunciones` (false), `aprobadoresRequeridos` por regla; `Notificaciones:LimiteCorreosPorHechoYDia` (1).

#### 2.4.10 Pruebas
- Unit: máquinas de estado de `Vinculo` y `Expediente`; `EvaluarCierre`; `AprobadoresRequeridos = 2` con el mismo usuario ⇒ 409 `aprobador-repetido`; `Rechazado` terminal; tabla "expediente abierto × evento" de 03 §5; `noAdmitirEstaVersion` ⇒ `VersionNoAdmitidaEnVinculo` y re-adjuntar la misma versión ⇒ 409.
- Integración: `POST /expedientes` con `Idempotency-Key` repetida ⇒ `Idempotent-Replayed`; cuerpo distinto ⇒ 422; usuario con rol en empresa A no ve expedientes de empresa B del mismo tenant (404); antes de aceptar la invitación `GET /terceros/{id}/documentos` responde vacío y el expediente solo muestra `requisitosCubribles` (ADR-0009); rechazar desde un área sin `expedientes:rechazar` ⇒ 403; revisor cuya `identificacion` coincide con el tercero o con una persona relacionada ⇒ 409 `conflicto-de-interes`; autorregistro crea el tercero sin vínculo y no notifica a ninguna empresa (T-13).
- E2E Playwright (escenario **(a)+(b)+(c)**): solicitar (la bandeja muestra "cubriría 3 de 7") → correo en Mailpit → tercero canjea enlace, acepta la invitación y los consentimientos → la empresa ve ahora los 3 requisitos cubiertos desde la bóveda → sube RUT (fixture) → bandeja de Legal muestra "Pendiente" por SSE → Legal observa con motivo → tercero sube v2 → timeline muestra `VeredictosInvalidadosPorNuevaVersion` → todas las áreas aprueban → `ExpedienteAprobado`.
- E2E: SLA de 1 minuto (reloj simulado) escala al responsable y luego al administrador sin auto-aprobar.

#### 2.4.11 Efectos
Hacia atrás: `BovedaTercero` (H2) incorpora `porEmpresa[]`; `FlujosResumen` (H3) obtiene `expedientesActivos`. Hacia adelante: `VersionAceptadaRegistrada` alimenta `VencimientoProgramado` (H6) y `EstadoCertificacionTercero` (H5/H9); el catálogo de eventos de expediente queda congelado como v1 en H5.

---

#### H4 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Comando/evento | `SeleccionarFlujo → FlujoSeleccionado(Origen: Explicito\|Regla\|PorDefecto)` al abrir; `CambiarFlujoDeExpediente → FlujoDeExpedienteCambiado + RequisitosRecalculados` (admin, `Abierto`/`Observado`, conserva veredictos de tipos que persisten); `AplicarVersionDeFlujoAExpedientesAbiertos(Simular) → VersionDeFlujoAplicada` | 03 §6.3, §12.1 |
| Endpoint | `POST /expedientes/{id}/cambiar-flujo` (`CambiarFlujoDto`), `POST /flujos/{id}/versiones/{n}/aplicar-a-abiertos?simular=` (`AplicarVersionDto` → `ImpactoAplicacionDto`), `GET /mis-pendientes`, `GET /terceros/{id}/ficha` | 05 §2.2, §2.4 |
| Evento público | `expediente.flujo_seleccionado`, `expediente.flujo_cambiado`, `flujo.aplicado_a_abiertos`, `expediente.recordatorio_cargue` (ahora en el catálogo §7) | 05 §7 |
| DTO | `BandejaExpedienteDto`, `NovedadDto`, `TimelineEventoDto`, `AceptacionDto` (evidencia de T&C/autorización; `ConsentimientosRegistrados` con `HashTexto`, `UserAgent`, `OtpVerificado`, `Canal`) | 05 §4, 03 §5.3 |
| Proyección | `MisPendientes` (consulta compuesta por usuario, no proyección), `Tercero360` (async por empresa, con `relaciones[]`) | 03 §14 |
| Endpoint | `POST /public/v1/invitaciones/{expedienteId}/aceptar` (ADR-0009), `POST /public/v1/autorregistro` (T-13), `POST /expedientes/{id}/areas/{areaId}/rechazar` (B-7) | 05 §2.4, §2.9 |
| DTO | `RechazarExpedienteDto`, `AceptarInvitacionDto`, `AutorregistroDto`; `VeredictoDto.veredicto ∈ {Aprobado, Observado}` + `noAdmitirEstaVersion` | 05 §4 |
| Pantalla | `/expedientes/:id` acción "Cambiar flujo"; editor "Aplicar a expedientes abiertos" con diálogo de impacto | 06 §3 |
| Prueba | e2e: expediente creado sin flujo toma el de la regla por monto; admin cambia flujo en `Observado` y el veredicto del RUT se conserva; aplicar versión con `simular=true` no emite eventos | 03 §12.1 |

### H5 — Certificado, notificaciones y webhooks (MVP)

#### 2.5.1 Agregados y eventos
| Nombre | Eventos | Invariantes |
|---|---|---|
| `Certificado` | `CertificadoEmitido`, `CertificadoReemplazado`, `CertificadoRevocado` (+ en H6: `CertificadoMarcadoConPendientes`, `CertificadoSuspendido` (bloqueo, B-5), `CertificadoRestablecido`, `CertificadoVencido`) | Uno por vínculo; folio `{PrefijoEmpresa}-C-{Año}-{Secuencia}-{n}` (expediente: `{PrefijoEmpresa}-E-{Año}-{Secuencia}`, T-9); `HashContenido` reproducible; sello Ed25519 (T-12); sin certificado parcial; `EstadoCertificado { Vigente, ConPendientes, Suspendido, Vencido, Revocado, Reemplazado }` |
| `Webhook`, `EntregaWebhook` (documentos) | `WebhookCreado`, `SecretoRotado`, `EntregaIntentada`, `WebhookDeshabilitado` | Firma `HMAC-SHA256(secreto, "{ts}.{cuerpo}")`; cabecera `Validasoft-Event-Id` para dedupe en el receptor (T-6); destinos `https` sin IP privada |
| `Notificacion`, `EntregaNotificacion` | `NotificacionEncolada`, `NotificacionEnviada`, `NotificacionEntregada/Abierta/Rebotada/Fallida` | Un correo por tercero, hecho y día (salvo invitación y certificado) |
| `PreferenciasNotificacion` (por usuario) | — | — |

#### 2.5.2 Endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `GET /certificados` · `GET /certificados/{folio}` · `GET /certificados/{folio}/pdf` · `GET /certificados/{folio}/emisiones` | `certificados:leer` | → `CertificadoDto`, PDF/A-2b | 200 | — | — |
| `POST /certificados/{folio}/revocar` | `certificados:revocar` | `RevocarDto` | 200 | `certificado.revocado` | Sí |
| `GET /public/v1/verificar/{folio}` | público (rate limit por IP) | → `CertificadoPublicoDto` | 200/404 | — | — |
| `GET /public/v1/certificados/{folio}` | sesión tercero | → `CertificadoDto` | 200 | — | — |
| `GET /consultas/certificacion?pais&tipoIdentificacion&numero&tipoVinculo` (versión MVP; `revalidacionPendiente` vacío hasta H6) | `consultas:leer` | → `ConsultaCertificacionDto` (`estadoVinculo \| "SinVinculo"`, `estadoCertificado`, `bloqueado`, `motivoBloqueo`, `urlVerificacion`, `antiguedadSegundos`; `ETag`, `max-age=300`) | 200/304 (nunca 404 por tercero desconocido: `SinVinculo`) | — | — |
| `GET /webhooks` · `POST` · `PUT /{id}` · `DELETE /{id}` · `POST /{id}/rotar-secreto` · `POST /{id}/probar` | `webhooks:administrar` | `WebhookDto` | 200/201/204 | `prueba.ping` | Sí |
| `GET /webhooks/{id}/entregas` · `GET …/entregas/{eid}` · `POST …/entregas/{eid}/reenviar` | `webhooks:administrar` | → `EntregaWebhookDto` | 200 | — | Sí |
| `GET /eventos?desde={cursor}&tipos=&empresaId=` (modo pull) | `eventos:leer` | → `PaginaDto<EventoPublicoDto>` | 200 | — | — |
| `GET /notificaciones` · `GET /notificaciones/{id}/entregas` | `auditoria:leer` | → `EntregaNotificacionDto[]` | 200 | — | — |
| `GET /plantillas-notificacion` · `PUT /plantillas-notificacion/{evento}/{canal}` (cabecera/pie, marca) | `configuracion:escribir` | `PlantillaNotificacionDto` | 200 | — | No |
| `GET /usuarios/me/preferencias-notificacion` · `PUT …` | sesión | `PreferenciasNotificacionDto` | 200 | — | No |
| `POST /sesiones-tercero` (token corto para embed; UI del embed en H8) | `sesiones-tercero:crear` | `SesionEmbedDto` → `{token, url, expira}` | 201 | — | Sí |

#### 2.5.3 DTOs nuevos
| DTO | Campos |
|---|---|
| `PlantillaNotificacionDto` | `evento*`, `canal*: enum(Correo\|Campana)`, `asunto`, `cabeceraHtml`, `pieHtml`, `variablesDisponibles*: string[]`, `vistaPrevia` |
| `PreferenciasNotificacionDto` | `porEvento*: {evento, correo: bool, campana: bool}[]`, `digestDiario*: bool`, `horaDigest` |
| Reutilizados de 05 | `CertificadoDto`, `CertificadoPublicoDto`, `RevocarDto`, `ConsultaCertificacionDto`, `WebhookDto`, `EntregaWebhookDto`, `EventoPublicoDto`, `EntregaNotificacionDto`, `SesionEmbedDto` |

#### 2.5.4 Eventos públicos
`certificado.emitido` (payload 05 §6.3, con `expedienteFolio` y `cambios[]`; **único** evento de certificación, T-15 — no existe `expediente.certificado`), `certificado.reemplazado`, `certificado.revocado`, `notificacion.entregada`, `notificacion.fallida`, `webhook.deshabilitado`, `prueba.ping`. **Congelación v1**: desde H5 todos los eventos de H1–H5 son estables; los posteriores son aditivos.

#### 2.5.5 Proyecciones
| Proyección | Tipo | Consumidor |
|---|---|---|
| `CertificadoPublico` (= `CertificadoPublicoDto` completo: `folio, estado, empresa, tercero{nombre, identificacionEnmascarada}, tipoVinculo, emitidoEn, vigenteHasta, hashContenido, contenidoCanonico, firma, claveId`) | Inline | `public:/verificar/:folio` |
| `EstadoCertificacionTercero` (por empresa) | Inline | `GET /consultas/certificacion` |
| `EntregasNotificacion` | Async | `web:/configuracion/tenant/notificaciones` |
| `EntregasWebhook` (vista) | Async | `web:/configuracion/tenant/webhooks` |

#### 2.5.6 Alertas (motor completo según 05 §8)
| Hecho | Plantilla | Destinatario | Canal | Consolidación |
|---|---|---|---|---|
| Expediente aprobado / certificado | `certificado` | Tercero (correo + campana), revisor (campana) | — | sin límite |
| Certificado revocado | `certificado-revocado` | Tercero, Administrador | correo | — |
| Webhook fallido definitivamente | `webhook-fallido` | Administrador | correo | — |
| Todas las de H2/H4 | sus plantillas | — | — | 1 correo / tercero / hecho / día; remitente plataforma + `Reply-To` empresa; evidencia SMTP |

#### 2.5.7 Pantallas
| Pantalla | Componentes | Lecturas |
|---|---|---|
| `web:/certificados`, `web:/certificados/:folio` (emisiones, revocar) | `TarjetaCertificado`, `Folio` | `GET /certificados`, `GET /certificados/{folio}/emisiones` |
| `web:/configuracion/tenant/webhooks` (suscripciones, entregas, reintento, probar) | tablas | `GET /webhooks`, `GET /webhooks/{id}/entregas` |
| `web:/configuracion/tenant/plantillas` (cabecera/pie, marca por empresa) | editor con vista previa | `GET /plantillas-notificacion` |
| `web:/configuracion/tenant/notificaciones` (evidencia de entrega) | — | `GET /notificaciones` |
| `public:/certificado/:folio` (compartir, QR, PDF), `public:/verificar/:folio` real, `public:/vinculos` completo | `TarjetaCertificado` | `GET /public/v1/certificados/{folio}`, `GET /public/v1/verificar/{folio}` |

#### 2.5.8 Jobs/sagas
| Nombre | Dispara | Pasos |
|---|---|---|
| Saga `EmisionCertificado` | `ExpedienteAprobado` | completitud → folio `-C-` (secuencia por empresa, T-9) → PDF/A-2b → hash RFC 8785 → sello Ed25519 de plataforma (T-12) → Object Lock modo *compliance* (prod, T-14) → `CertificadoEmitido` → `certificado.emitido` (T-15) |
| Despachador de webhooks | outbox | firma, `Validasoft-Event-Id` (T-6), reintentos 1 m…24 h ×2, `WebhookDeshabilitado` tras 7 días |
| Despachador de notificaciones | eventos | Fluid + MailKit + campana; dedupe `(destinatario, evento, agregado, ventana)` |
| Seed `demo` | comando `-- seed demo` | tenant `demo` con 2 empresas, flujo "Habilitación de proveedores", usuarios por rol, 3 terceros |

#### 2.5.9 Configuración
`Certificados:ClaveEd25519` (referencia KMS; pública en `/.well-known/validasoft-certificados.json`, T-12), `Certificados:UrlVerificacionBase`, `Almacenamiento:ObjectLockModo` (`Compliance`, T-14), `Webhooks:Reintentos` (`[1m,5m,30m,2h,12h,24h,24h]`), `Webhooks:ToleranciaRelojSegundos` (300), `Notificaciones:RemitenteDominio`, `EmpresaDto.replyTo`, `Notificaciones:DigestHora` (07:00 tenant).

#### 2.5.10 Pruebas
- Unit: hash de contenido reproducible; folio `-C-` secuencial por empresa; firma HMAC válida/inválida; sello Ed25519 verificable con la clave pública publicada.
- Integración: receptor de prueba acepta firma válida y rechaza inválida; la misma entrega reintentada llega con el mismo `Validasoft-Event-Id` y el receptor de ejemplo la ignora (T-6); `GET /eventos?desde=` devuelve exactamente los mismos eventos que las entregas; un tercero con tres empresas recibe **un** correo por hecho y día; `GET /public/v1/verificar/{folio}` compara hash.
- E2E (escenario **(d)**): última área aprueba → SSE "Certificado emitido" → `public:/certificado/:folio` → entrega del webhook visible en Configuración.
- Contract: snapshot v1 congelado (cambio incompatible ⇒ build rojo).
- **Criterio de salida del MVP**: demo de punta a punta en Docker con el tenant `demo`.

#### 2.5.11 Efectos
Hacia atrás: la plantilla `aprobado` (H4) se conserva y se suma `certificado`; `ConsultaCertificacionDto` nace con `revalidacionPendiente: []` y `documentosVencidos: []`. Hacia adelante: todo evento nuevo (H6+) entra al catálogo como aditivo; los SDKs (H9) se generan de este snapshot.

---

#### H5 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Contrato | Contenido canónico del certificado (`validasoft.certificado.v1`, RFC 8785, `sha256`, sello Ed25519, clave pública en `/.well-known/validasoft-certificados.json`), qué entra y qué no, plantilla PDF/A-2b, verificación pública con `contenidoCanonico` | 03 §7.1 |
| DTO | `CertificadoContenidoDto`, `PlantillaNotificacionDto`, `PreferenciasNotificacionDto` | 05 §4 |
| Endpoint | `GET/PUT /usuarios/me/preferencias-notificacion` | 05 §2.7 |
| Regla | Alcance de webhooks = `apiKey.empresas[] ∩ webhook.empresas[]`; vacío ⇒ `422 empresas-fuera-de-alcance`; cambio posterior ⇒ `webhook.deshabilitado {causa}` | 05 §2.8, 07 §4 |

### H6 — Vigencias, revalidación y versiones nuevas

#### 2.6.1 Agregados y eventos
| Nombre | Eventos nuevos | Invariantes |
|---|---|---|
| `Vinculo` | `AceptacionDesactualizada(TipoTenantId, VersionAceptadaId, VersionNuevaId, PlazoRevalidacion, Motivo)`, `CambioMenorRegistrado`, `RequisitoRatificado(NuevaVigenteHasta)`, `DocumentoPorVencerEnVinculo`, `DocumentoVencidoEnVinculo(Critico)`, `VinculoPorVencer(Causa: Vigencia)` (única causa; la revalidación vencida **no** cambia el estado del vínculo, T-8), `VinculoVencido(CausaTipos[])`, `VinculoRenovado`, `EstadoDeVinculoRecalculado`, `CuentaDesignadaPropuesta`, `CuentaDesignadaAprobada(AprobadaPor[])`, `CuentaDesignadaRechazada`, `CuentaDesignadaReemplazada` | `vigente_hasta ≤ fecha_vencimiento_doc`; `desactualizada ⇔ version_actual ≠ version_aceptada` **y la empresa lo decidió así** (T-7); alerta a todos exactamente una vez por `(vinculo, tipo, version)`; `CuentaDesignada` solo la propone el tercero y requiere `AprobadoresRequeridos` distintos; `EstadoCuentaDesignada { Propuesta, Aprobada, Rechazada, Reemplazada }`; `EstadoAceptacion { AlDia, Desactualizada, PorVencer, Vencida }` |
| `Expediente` | `RequisitoDesactualizado`, `RequisitoVencido`, `RequisitoAgregadoAExpediente` (ya en H4), apertura directa en `EnRevision` para `Actualizacion` | `Renovacion` = vencidos/por vencer + `SiempreEnRenovacion`; `Actualizacion` = solo tipos cuya actual cambió |
| `Certificado` | `CertificadoMarcadoConPendientes(Causa, Bloqueante)`, `CertificadoSuspendido` (bloqueo del vínculo, B-5), `CertificadoRestablecido` (revalidación cerrada o desbloqueo), `CertificadoVencido`, `CertificadoReemplazado` por revalidación | `ConPendientes` no revoca; `Suspendido` tampoco: el desbloqueo restituye el certificado tal como estaba |
| `VencimientoProgramado` (proyección-tabla) | `TipoEvento { AvisoDocumento, DocumentoVencido, AvisoVinculo, VinculoVencido, CaducidadExpediente, RecordatorioCargue, VerificacionExpirada, PlazoRevalidacion, ReconsultaListas, RetencionCumplida }` | `ClaveIdempotencia` única |

#### 2.6.2 Endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `GET /revalidaciones?estado=&tipo=&empresaId=` | `expedientes:leer` | → `PaginaDto<RevalidacionDto>` | 200 | — | — |
| `POST /expedientes/{id}/requisitos/{rid}/ratificar` | tercero | — | 200; 422 `ratificacion-no-permitida` | `documento.ratificado` | Sí |
| `GET /vencimientos?desde&hasta&estado&tipo&empresaId` · `POST /vencimientos/notificar` | `vencimientos:leer`/`escribir` | → `VencimientoDto[]`; `NotificarVencimientosDto` | 200/202 | — | Sí (POST) |
| `GET /terceros/{id}/frescura` · `GET /public/v1/frescura` | `vencimientos:leer` / sesión tercero | → `FrescuraDto` | 200 | — | — |
| `GET /vinculos/{id}/cuenta-designada` · `POST /vinculos/{id}/cuenta-designada/aprobar` · `POST /vinculos/{id}/cuenta-designada/rechazar` | `cuentas:aprobar` | → `CuentaDesignadaDto`; `{motivo}` | 200; 409 `aprobador-repetido` | `vinculo.cuenta_designada`, `vinculo.cuenta_designada_rechazada` | Sí |
| `GET /public/v1/boveda/cuentas-bancarias` · `POST /public/v1/boveda/cuentas-bancarias` (cuenta global con certificación en dos pasos; trasladado desde H2, #25) | sesión tercero | `CuentaBancariaDto` (sin id) → `CuentasTercero` | 200/201 | — | Sí (POST) |
| `POST /public/v1/vinculos/{id}/cuenta-designada` | sesión tercero | `DesignarCuentaDto` → `CuentaDesignadaDto` | 202 | `vinculo.cuenta_designada_propuesta` | Sí |
| `GET /consultas/certificacion` (ahora con `revalidacionPendiente[]`, `documentosVencidos[]`, `cuentaDesignada`, `bloqueado`/`motivoBloqueo`) | `consultas:leer` | → `ConsultaCertificacionDto` | 200/304 | — | — |
| `GET /vencimientos/proximos?dias=` | `vencimientos:leer` | → `VencimientosProximos` | 200 | — | — |
| `POST /flujos/{id}/versiones/{n}/aplicar-a-abiertos` (operativo) | Administrador | — | 202 | `expediente.requisitos_recalculados` | Sí |

#### 2.6.3 DTOs nuevos
| DTO | Campos |
|---|---|
| `RevalidacionDto` | 05 §5.5: `expedienteId*`, `tipo*: TipoExpediente`, `vinculoId*`, `empresaId*`, `tercero*{nombre}`, `requisito*{tipo{codigo}, versionNueva, versionAceptadaAnterior, aceptacion{estado: EstadoAceptacion, plazoRevalidacion}}`, `hechos*{conIa, comparacion, camposCambiados[], camposCriticosCambiados[], frescuraCumplida, confianza}`, `sugerenciaAutomatica{veredicto, razon}`, `nivelRevision*`, `diasRestantes*`, `bloqueante*: bool` (único DTO de revalidación, #13) |
| Reutilizados de 05 | `VencimientoDto`, `NotificarVencimientosDto`, `FrescuraDto`, `CuentaDesignadaDto`, `DesignarCuentaDto`, `VersionAceptadaDto` (con `estado: EstadoAceptacion`, `motivoVigencia`) |

#### 2.6.4 Eventos públicos
`vinculo.revalidacion_pendiente`, `vinculo.revalidado`, `vinculo.cambio_menor`, `vinculo.cuenta_designada_propuesta`, `vinculo.cuenta_designada`, `vinculo.cuenta_designada_rechazada`, `expediente.requisito_desactualizado`, `expediente.requisito_vencido`, `documento.ratificado`, `documento.por_vencer`, `documento.vencido`, `certificado.con_pendientes { bloqueante }`, `certificado.suspendido`, `certificado.restablecido`, `certificado.vencido`; `documento.version_nueva` ahora con `efectoEnEmpresa` poblado (05 §6.4); `vinculo.estado_cambiado` solo con causa `Vigencia` (no existe `RevalidacionVencida`, T-8: una revalidación vencida se refleja en `certificado.con_pendientes { bloqueante: true }`, no en el estado del vínculo).

#### 2.6.5 Proyecciones
| Proyección | Tipo | Consumidor |
|---|---|---|
| `BandejaRevalidaciones` | Async | `web:/revalidaciones`, `web:/inicio` |
| `VencimientoProgramado` | Async | job diario, `web:/vencimientos` |
| `FrescuraDocumentoTercero` (activa) | Async global | `public:/boveda`, `AvisoConsolidado`, `GET /terceros/{id}/frescura` |
| `VinculoResumen` (+ `revalidacionPendiente`, `cuentaDesignada`, `vigenteHasta`) | Inline | ficha 360 |
| `EstadoCertificacionTercero` (+ revalidación, vencidos, cuenta) | Inline | Marco |
| `Tercero360` (`vinculoConEstaEmpresa`, `otrasEmpresasDelGrupo[]` sin decisiones, `documentos[].aceptadaPorMi`) | Async | `web:/terceros/:id` |
| `CuentasTercero` | Inline | `public:/cuentas` |

#### 2.6.6 Alertas
| Hecho | Plantilla | Destinatario | Canal | Consolidación |
|---|---|---|---|---|
| Versión nueva de documento global | `revalidacion` | Revisor del área (campana), Administrador (digest) | — | por empresa |
| Cambio menor | — | Revisor | campana | — |
| Por vencer 30/15/5 (60/30/7 pólizas y EEFF) | `por-vencer` | Tercero (correo **consolidado** por documento con todas las empresas), Administrador (digest) | — | una secuencia por documento |
| Vencido | `vencido` | Tercero (correo), revisor (campana), Administrador (digest) | — | — |
| Revalidación no atendida (plazo cumplido ⇒ `certificado.con_pendientes { bloqueante: true }`, T-8) | `revalidacion-vencida` (05 §8) | Revisor y Administrador; tercero (campana) | correo | — |
| Cuenta designada propuesta / aprobada / rechazada | `cuenta-designada` | Aprobadores (campana), tercero (correo al aprobar/rechazar) | — | — |
| Certificado con pendientes / restablecido | `certificado-pendientes` | Tercero, Administrador | campana | — |

#### 2.6.7 Pantallas
| Pantalla | Componentes | Lecturas |
|---|---|---|
| `web:/revalidaciones` (vínculo × tipo, anterior vs actual, diff, nivel, días) | `DiffCampos`, `PanelIA`, `VisorDocumento` (comparar lado a lado) | `GET /revalidaciones` |
| `web:/vencimientos` (informe, Excel, notificar) | tablas, exportación | `GET /vencimientos`, `POST /vencimientos/notificar` |
| `web:/terceros/:id` completo (aceptaciones, cuenta designada con aprobación de 2, vínculos del grupo sin decisiones) | `TarjetaCuentaBancaria` | `Tercero360`, `GET /vinculos/{id}/cuenta-designada` |
| `web:/expedientes` pestañas Renovaciones / Revalidaciones | `TablaExpedientes` | `GET /expedientes?tipo=` |
| `web:/configuracion/tenant/tipos-documento` (vigencias: `ReglaFrescura`, `DiasAviso`, `DiasGracia`, `diasRevalidacion`, `politicaDesactualizada`) y `/configuracion/empresa/tipos-documento` (override más estricto, B-4) | — | `PUT /tipos-documento/{id}`, `PUT /empresas/{id}/tipos-documento/{tipoId}` |
| `public:/boveda` con vencimiento por empresa, `AvisoConsolidado`, ratificar | `AvisoConsolidado`, `TarjetaDocumentoBoveda` | `GET /public/v1/boveda`, `GET /public/v1/frescura` |
| `public:/cuentas` (N cuentas, designar por empresa, estado pendiente) | `TarjetaCuentaBancaria` | `CuentasTercero`, `POST /public/v1/vinculos/{id}/cuenta-designada` |
| `public:/boveda` edición de formulario con aviso de campo crítico (flujo **(h)**) | `FormularioDinamico`, `DiffCampos` | `POST /expedientes/{id}/requisitos/{rid}/formulario` / `POST /public/v1/tercero/datos-maestros` |

#### 2.6.8 Jobs/sagas/scheduled
| Nombre | Dispara | Pasos |
|---|---|---|
| Saga `Revalidacion` | `VersionActualCambiada` (hechos: `Comparacion`, `CamposCambiados[]`, `CamposCriticosCambiados[]`) | `VinculosPorTerceroYTipo` → por vínculo `EnValidacion\|Activo\|PorVencer\|Vencido` (todos; sin exclusión, A-8) **la saga decide por empresa** (T-7) con la configuración efectiva tenant → empresa → regla (B-4): si `Comparacion ∈ {Distinta, CambiosDetectados}` y algún campo cambiado es crítico para esa empresa (o `Identica` pero la frescura exige nueva aceptación) ⇒ `MarcarAceptacionDesactualizada(Motivo)` + `AbrirExpediente(Actualizacion)` + `CertificadoMarcadoConPendientes`; si hay `Inicial`/`Renovacion` abierto: `MarcarRequisitoDesactualizado` + `VersionAdjuntadaARequisito` (tabla de casos 03 §5); en otro caso ⇒ `RegistrarCambioMenor`; `VersionActualRestituida` ⇒ `CancelarRevalidacion` (`RevalidacionCancelada`); idempotente por `(VinculoId, TipoTenantId, VersionId)` |
| Saga `CambioCuentaDesignada` | `CuentaDesignadaPropuesta` | verificar certificación `Actual` → esperar `AprobadoresRequeridos` distintos → `CuentaDesignadaAprobada` → `vinculo.cuenta_designada` al ERP; rechazo conserva la anterior |
| Job diario `ProcesarVencimientosDelDia` | cron 02:00 tenant | lee `VencimientoProgramado` y `FrescuraDocumentoTercero` → `MarcarDocumentoPorVencer/Vencido`, `MarcarPorVencer/MarcarVencido`, `EnviarAvisoConsolidado`, `CaducarExpediente`, `ExpirarVerificacion` |
| Scheduled `PlazoRevalidacionVencido` | `AceptacionDesactualizada` | tras `diasRevalidacion` (15; **90** para aceptaciones importadas, B-8): si `politicaDesactualizada = BloquearTrasGracia` (default, A-2) ⇒ `CertificadoMarcadoConPendientes(Bloqueante = true)` + `certificado.con_pendientes { bloqueante: true }` (la consulta de certificación responde `bloqueado: true`; el vínculo sigue `Activo`, T-8) + alerta `revalidacion-vencida`; si `SoloAviso`, solo la alerta |

#### 2.6.9 Configuración
`TipoDocumentoTenantDto.reglaFrescura`, `diasAviso` (`[30,15,5]`; `[60,30,7]` pólizas/EEFF), `diasGracia` (15; 0 críticos cumplimiento), `diasRevalidacion` (15), `politicaDesactualizada` (`BloquearTrasGracia`, A-2), `permiteRatificacion` (la ratificación nunca pasa por área, A-7); `FlujoDefinicionDto.politicaVigenciaVinculo` (`MinimoDeRequisitosCriticos`); `Vencimientos:HoraJobDiario` (02:00).

#### 2.6.10 Pruebas
- Unit: fórmula de `VigenteHasta`/`MotivoVigencia` (casos Cámara 30 vs 90, póliza con fecha fin, RUT 365 desde aprobación, EEFF periodo); la saga registra `CambioMenor` solo cuando ningún campo cambiado es crítico **para esa empresa** (T-7): la misma versión puede ser menor para A y revalidación para B.
- Integración con reloj simulado (`FakeTimeProvider`): aviso T-15 → vencido T0 → ratificar → `VinculoRenovado` + emisión `-{n+1}`.
- Escenarios E1–E10 del documento 02 automatizados en nivel `Manual`: dos tenants (y dos empresas del mismo tenant) con el mismo tercero: una versión nueva produce exactamente una alerta por vínculo; A observa y B aprueba sin verse; un solo objeto en MinIO por hash; bloqueo ⇒ `ComparticionCongelada` + `CertificadoSuspendido`, desbloqueo ⇒ `ComparticionReanudada` + `CertificadoRestablecido` (B-5); desvinculación congela hacia adelante; al cerrar un `Renovacion` el vínculo `Vencido` vuelve a `Activo` por `EstadoDeVinculoRecalculado` (I-15).
- Cuenta designada: dos aprobaciones del mismo usuario ⇒ 409; aprobada ⇒ webhook `vinculo.cuenta_designada` con `numeroEnmascarado`.
- E2E (escenario **(e)** y **(g)**).

#### 2.6.11 Efectos
Hacia atrás: `ConsultaCertificacionDto` (H5) se completa; `ExpedienteDto.requisitos[].versionAceptada` (H4) empieza a tener `estado: Desactualizada`; `BovedaTercero` muestra vencimientos. Hacia adelante: la saga `Revalidacion` aplica el motor de decisión de H7 por empresa; H8 añade campañas sobre `VencimientoProgramado`; H9 expone `vinculo.revalidacion_pendiente` al SRM.

---

#### H6 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Endpoint | `GET /vencimientos/proximos?dias=` (`VencimientosProximos`, vista sobre `VencimientoProgramado`) | 05 §2.7, 03 §14 |
| DTO | `RevalidacionDto` (con `bloqueante`) | 05 §4 |
| Nombres | Saga `CambioCuentaDesignada` emite `CuentaDesignadaAprobada` / `CuentaDesignadaRechazada`; rutas del tercero `POST /public/v1/boveda/cuentas-bancarias` y `POST /public/v1/vinculos/{id}/cuenta-designada` (ambas en H6, #25) | 04 §5, 06 §4(g) |
| Evento | `EstadoDeVinculoRecalculado` (I-15), `CertificadoSuspendido` (B-5), `RevalidacionCancelada`; eliminado `VinculoPorVencer(Causa: RevalidacionVencida)` (T-8) | 03 §5, §7 |

### H7 — Verificaciones externas, cumplimiento y revisión automática

#### 2.7.1 Agregados y eventos
| Nombre | Eventos nuevos | Invariantes |
|---|---|---|
| `Expediente` | `VerificacionExternaRegistrada`, `CoincidenciaResuelta(VigenteHasta=+12m)`, `VeredictoAutomaticoEmitido(Origen: Sistema, Evidencia)`, `VeredictoSugerido`, `RevisionHumanaSolicitada`, `VerificacionExpirada` | Verificación bloqueante `Pendiente\|CoincidenciaPorAnalizar\|Expirada` impide `ExpedienteAprobado`; veredicto automático solo con `NivelRevision = Automatico` y reglas 03 §11; reversible |
| `Tercero` / `RelacionEntreTerceros` (ADR-0010) y `Vinculo` | `CambioDeRelacionDetectado(Confianza)` (bóveda) → `RelacionConfirmada` (tercero) → `ExpirarVerificacionesDeRepresentante` ⇒ `VerificacionesDeRepresentanteExpiradas(Tipos[])` en cada vínculo (03 adición 3) | Cámara nueva con otro representante ⇒ el sistema **detecta** el cambio y el tercero lo confirma (I-10: sin rechazo automático); solo entonces la cédula aceptada del saliente queda `Desactualizada`, sus listas `Expirada` y el requisito de cédula del entrante nace `Pendiente` (o `Cargado` si su bóveda ya la tiene) |
| `IVerificador` (adaptadores) | `ListasRestrictivas` (proveedores con contrato + `EvidenciaManual`), `EstudioDeCumplimiento` (campos legado: verificado en listas, conflicto de interés, nivel de riesgo 1–3, debida diligencia, evidencias, vigencia 30 d) | Idempotente por `(ExpedienteId, Tipo, ReferenciaExterna)`; se consulta al tercero **y a cada persona relacionada** (UBO, PEP, representante) |
| Tipos canónicos activados en flujos (ya en Anexo A desde H2) | `BENEFICIARIO_FINAL`, `DECLARACION_PEP` (formularios globales que referencian personas relacionadas; `FORMULARIO_SARLAFT` PorVinculo los referencia sin duplicarlos, I-20) | — |
| `PresupuestoIa` (documento por tenant) | `PresupuestoAl80`, `PresupuestoAgotado` | agotado ⇒ aplica `PlanDto.accionAlAgotar` (`DegradarAManual` por defecto, fijado por Plataforma, I-23); la **lectura** nunca se detiene ni se cobra al tenant (B-2); nunca bloquea la carga |

#### 2.7.2 Endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `POST /expedientes/{id}/verificaciones/{tipo}` | `verificaciones:escribir` | `VerificacionDto` | 201 | `expediente.verificacion_registrada` | Sí |
| `POST /expedientes/{id}/verificaciones/{tipo}/coincidencias/{cid}/resolver` | `verificaciones:escribir` (rol `Cumplimiento`) | `ResolverCoincidenciaDto` | 200 | `expediente.coincidencia_resuelta` | Sí |
| `GET /excepciones?tipo=&empresaId=` · `POST /excepciones/{id}/resolver` | revisor | → `ColaDeExcepciones`; `{veredicto, motivos, observacion}` | 200 | `expediente.requisito_veredicto` | Sí |
| `POST /public/v1/expedientes/{id}/requisitos/{rid}/revision-humana` | sesión tercero | `{comentario}` | 202 | `expediente.revision_humana_solicitada` | Sí |
| `GET /tenant/revision-automatica` · `PUT /tenant/revision-automatica` | `configuracion:escribir` | `RevisionAutomaticaConfigDto` | 200 | — | No |
| `GET /kpis?flujoId=&empresaId=&periodo=` (ahora con `precisionIa`, `equidad`, `decisionesAutomaticas`) | `expedientes:leer` | → `KpisDto` | 200 | — | — |
| `GET /informes/cumplimiento?desde&hasta&empresaId` · `POST /informes/cumplimiento/exportar` | `auditoria:leer` (rol `Cumplimiento`) | → `InformeCumplimiento` / Excel | 200/202 | `exportacion.lista` | Sí (POST) |

#### 2.7.3 DTOs nuevos
| DTO | Campos |
|---|---|
| `RevisionAutomaticaConfigDto` (único, consolidado #14) | `nivelPorTipo*: {tipoDocumentoTenantId, nivelRevision}[]`, `umbralAprobar*: decimal (0,95)`, `umbralAsistido*: decimal (0,80)`, `porcentajeMuestreo*: int (5)`, `muestraMinimaSemanal*: int (1)`, `presupuestoMensualUsd`, `alertaPorcentaje*: int (80)` (la acción al agotar vive en `PlanDto.accionAlAgotar`, I-23) |
| `ExcepcionDto` | 06 `ColaDeExcepciones.items[]`: `id*`, `empresaId*`, `expedienteId*`, `requisitoId*`, `tipo*: enum(Critico\|Confianza\|Manipulacion\|Muestreo\|RevisionSolicitada\|CuentaDesignada\|CoherenciaSoporte)` (PascalCase, #11), `motivo*`, `hechos*: HechosDto`, `solicitadoPorTercero: bool`, `fecha*` |
| `EstudioCumplimientoDto` (resultado de `VerificacionDto` tipo `EstudioDeCumplimiento`) | `verificadoEnListas*: bool`, `fechaProximaConsulta*`, `conflictoDeInteres*: bool`, `debidaDiligencia*: enum(SinNovedad\|Descartada\|Monitoreo)`, `nivelRiesgo*: int(1-3)`, `observaciones`, `evidencias*: {documentoId, version}[]`, `vigenteHasta*` |
| Reutilizados de 05 | `VerificacionDto`, `ResolverCoincidenciaDto`, `KpisDto`, `EvidenciaIaDto` |

#### 2.7.4 Eventos públicos
`expediente.verificacion_registrada`, `expediente.coincidencia_resuelta`, `expediente.revision_humana_solicitada`, `expediente.requisito_veredicto` con `origen: Sistema`, `ia.presupuesto_80`, `ia.presupuesto_agotado`, `exportacion.lista`.

#### 2.7.5 Proyecciones
`ColaDeExcepciones` (async) → `web:/excepciones`; `InformeCumplimiento` (async) → informe SAGRILAFT; `KpisTenant` con `precisionIa`, `equidad` (tasa de observación automática por tipo de persona, tamaño, región), `decisionesAutomaticas{aprobadas, observadas, revertidas}`, `horasAhorradasEstimadas`, `tiempoMedianaDias`, `avisosPorDocumento`; `UsoTenant.decisionesIa` (facturable) y `lecturasIa` (solo informativo, B-2).

#### 2.7.6 Alertas
| Hecho | Plantilla | Destinatario | Canal |
|---|---|---|---|
| Resultado IA: observado automático | `observacion-automatica` | Tercero | correo + campana |
| Revisión humana solicitada | — | Revisor | campana |
| Coincidencia en listas | `coincidencia` | Revisor de Cumplimiento | correo |
| Verificación expirada / reconsulta debida | `verificacion-expirada` | Cumplimiento | campana |
| Presupuesto IA 80 % / agotado | `presupuesto-ia` | Administrador | correo |

#### 2.7.7 Pantallas
| Pantalla | Componentes | Lecturas |
|---|---|---|
| `web:/excepciones` (críticos, baja confianza, manipulación, muestreo, revisión solicitada, cuenta designada) | `PanelIA`, `PanelVeredicto`, `DiffCampos` | `GET /excepciones` |
| `web:/configuracion/tenant/revision-automatica` (nivel por tipo, umbrales, muestreo, presupuesto y consumo) | formularios, `KpiCard` | `GET /tenant/revision-automatica`, `UsoTenant` |
| `web:/expedientes/:id` pestaña Verificaciones (listas, estudio de cumplimiento con evidencias, UBO/PEP, coincidencias) | `VisorDocumento` | `GET /expedientes/{id}` |
| `web:/informes/cumplimiento` (SAGRILAFT) | tablas, Excel | `GET /informes/cumplimiento` |
| `public:` botón "Pedir revisión humana"; formularios `BENEFICIARIO_FINAL`, `DECLARACION_PEP` | `FormularioDinamico` | `POST /public/v1/expedientes/{id}/requisitos/{rid}/revision-humana` |

#### 2.7.8 Jobs/sagas/scheduled
| Nombre | Dispara | Pasos |
|---|---|---|
| Motor de decisión (dentro de `VerificacionDocumental`/`Revalidacion`) | `HechosDeVersionAplicadosARequisito` | tabla 03 §11 → `VeredictoAutomaticoEmitido` / `VeredictoSugerido` / `ColaDeExcepciones`; muestreo `PorcentajeMuestreo` |
| Adaptadores `IVerificador` (workers) | `VerificacionRequerida` al activar etapa | consulta al proveedor → `RegistrarVerificacionExterna` (evidencia PDF como versión `EVIDENCIA_LISTAS`) |
| `VencimientoProgramado.ReconsultaListas` | cada 12 meses / renovación | `VerificacionExpirada` → reconsulta |
| Batches API (re-verificación masiva) | comando CLI | re-extracción de hechos tras cambio de esquema canónico, **solo sobre terceros con consentimiento IA** (B-8); coste de plataforma (B-2) |

#### 2.7.9 Configuración
`RevisionAutomaticaConfigDto.*`; `VerificacionRequeridaDto.frecuenciaReconsultaMeses` (12); `Verificadores:ListasRestrictivas:Proveedores[]` (credenciales por tenant cifradas); `Verificadores:EstudioCumplimiento:VigenciaDias` (30); `Ia:ModeloPorTipo` (override `claude-sonnet-5-5`).

#### 2.7.10 Pruebas
- Unit: tabla de decisión automática (las reglas 1–7 de 04 §7.5, incluidas la 2 —versión idéntica a la aceptada— y la 4 —campo crítico cambiado⇒ humano—) y degradación por umbral; presupuesto agotado ⇒ `accionAlAgotar` del plan sin bloquear carga ni lectura; una coincidencia **confirmada** en listas nunca se aprueba automáticamente.
- Integración: verificación bloqueante pendiente impide `ExpedienteAprobado` aunque todas las áreas aprueben; coincidencia `CoincidenciaPorAnalizar` → `Descartada` con justificación; Cámara nueva con otro representante genera `CambioDeRelacionDetectado` y solo tras `RelacionConfirmada` se emite `VerificacionesDeRepresentanteExpiradas` (I-10, ADR-0010); listas se consultan también para cada persona relacionada.
- E2E (escenario **(f)**): misma versión, tres empresas con niveles distintos; muestreo del 5 % aparece en `/excepciones`; "Pedir revisión humana" llega a la empresa.
- Fixtures IA: documento con señales de manipulación va a excepciones.

#### 2.7.11 Efectos
Hacia atrás: `RequisitoDto.sugerenciaAutomatica` (H4) y `RevalidacionDto.sugerenciaAutomatica` (H6) se llenan; `UsoTenant` (H1) cuenta decisiones (base del modelo comercial). Hacia adelante: H8 define planes sobre `decisionesIa`; H9 expone `origen: Sistema` al SRM.

---

#### H7 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Endpoint | `GET/PUT /tenant/revision-automatica` (`RevisionAutomaticaConfigDto`), `GET /informes/cumplimiento` · `POST …/exportar`, `GET /excepciones` con `ExcepcionDto`/`ResolverExcepcionDto` | 05 §2.1, §2.4, §2.7 |
| Proyección | `KpisTenant/KpisFlujo/KpisEmpresa` con campos `decisionesAutomaticas`, `consumoIa`, `equidad` (sustituyen a `KpisRevisionAutomatica` y `KpisEquidadIA`) | 03 §14, 04 §4.2, 06 §5 |
| DTO | `EstudioCumplimientoDto`, `InformeCumplimientoDto`, `KpisDto` ampliado | 05 §4 |
| Evento / regla | `CambioDeRelacionDetectado` → `RelacionConfirmada` → `VerificacionesDeRepresentanteExpiradas` (I-10); UBO/PEP como personas relacionadas (I-20); reglas 2 y 4 del motor (#29); `accionAlAgotar` en `PlanDto` (I-23); H9 depende de H7 (B-8) | 03 §4.5, 04 §7.5, 05 §4, 09 |

### H8 — Operación masiva, informes y administración

#### 2.8.1 Entidades y eventos
| Nombre | Eventos | Invariantes |
|---|---|---|
| `LoteInvitacion` (saga `InvitacionMasiva`) | `LoteCreado`, `LoteFilaProcesada`, `LotePausado`, `LoteReanudado`, `LoteTerminado` | Lote ≤ 10 000 filas; validación multipaís por fila; informe por fila |
| `Campania` | `CampaniaCreada`, `CampaniaIniciada`, `CampaniaTerminada` | Programa `AvisoDocumento` masivo con ventana |
| `Delegacion` (Identity) | `RevisionDelegada` | Fechas y alcance (área); auditada |
| `SolicitudSupresion` (saga `SupresionDatos`) | `SolicitudSupresionRegistrada`, `RetencionLegalMarcada`, `SolicitudSupresionResuelta` | Respuesta ≤ 15 días hábiles con constancia |
| `Exportacion` | `ExpedienteExportado(HashZip)`, `BovedaExportada` | ZIP con índice firmado |
| `Plan` / límites | `LimiteAlcanzado` | Sobre `UsoTenant` |
| `Expediente` | `ConflictoDeInteresDetectado` (activo), separación de funciones | — |

#### 2.8.2 Endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `POST /invitaciones/lotes` · `GET /invitaciones/lotes` · `GET /invitaciones/lotes/{id}` · `GET /invitaciones/lotes/{id}/filas` · `POST /invitaciones/lotes/{id}/pausar` · `/reanudar` | `expedientes:escribir` | `CrearLoteDto` (CSV/XLSX prefirmado o filas) → `LoteDto` | 202/200 | `expediente.abierto` por fila | Sí |
| `GET /campanias` · `POST /campanias` · `POST /campanias/{id}/iniciar` | `vencimientos:escribir` | `CampaniaDto` | 201/200 | `campania.iniciada` | Sí |
| `POST /usuarios/{id}/delegaciones` · `DELETE …/{did}` | `usuarios:administrar` / propio | `DelegacionDto` | 201/204 | — | Sí |
| `POST /expedientes/{id}/exportar` · `GET /exportaciones/{id}` | `expedientes:leer` + `auditoria:leer` | → `ExportacionDto` | 202/200 | `exportacion.lista` | Sí |
| `POST /public/v1/boveda/exportar` · `GET /public/v1/exportaciones/{id}` | sesión tercero | → `ExportacionDto` | 202/200 | `exportacion.lista` | Sí |
| `POST /public/v1/solicitudes-supresion` · `GET /public/v1/solicitudes-supresion/{id}` · `GET /solicitudes-supresion` · `POST /solicitudes-supresion/{id}/resolver` | sesión tercero / `auditoria:leer` + Administrador | `SolicitudSupresionDto` | 202/200 | `habeas.solicitud_recibida`, `habeas.solicitud_resuelta` | Sí |
| `GET /auditoria?entidad=&actor=&desde=&hasta=` · `POST /auditoria/exportar` | `auditoria:leer` / `auditoria:exportar` | → `PaginaDto<RegistroAuditoriaDto>` (único DTO de auditoría, #15) | 200/202 | `exportacion.lista` | Sí (POST) |
| `PUT /plantillas-notificacion/{evento}/{canal}` (Liquid restringido con validación de variables) | `configuracion:escribir` | `PlantillaNotificacionDto` (+`cuerpoLiquid`) | 200; 400 `plantilla-invalida` | — | No |
| `GET /tenant/plan` · `GET /tenant/uso` | `configuracion:leer` | → `PlanDto`, `UsoTenantDto` | 200 | — | — |
| `PUT /empresas/{id}/dominio-correo` · `POST /empresas/{id}/dominio-correo/verificar` (DKIM) | `empresas:administrar` | `DominioCorreoDto` | 200 | — | Sí |
| `GET /informes/estado-expedientes` · `POST /informes/estado-expedientes/exportar` | `expedientes:leer` | → tabla / Excel | 200/202 | `exportacion.lista` | Sí |
| `PUT /tenant/matriz-riesgo` | `configuracion:escribir` (Cumplimiento) | `MatrizRiesgoDto` | 200 | — | No |

#### 2.8.3 DTOs nuevos
| DTO | Campos |
|---|---|
| `CrearLoteDto` | `empresaId*`, `flujo*{codigo, version}`, `archivo: {documentoId}` o `filas: {identificacion*, nombre*, correo*, atributos}[]`, `programarPara`, `invitacion*: InvitacionDto` |
| `LoteDto` | `id*`, `estado*: enum(Programado\|Procesando\|Pausado\|Completado\|CompletadoConErrores\|Fallido)`, `total*`, `procesadas*`, `exitosas*`, `fallidas*`, `duplicadas*`, `creadoEn*`, `inicio`, `fin` |
| `FilaLoteDto` | `n*`, `identificacion*`, `estado*: enum(Pendiente\|Exitosa\|Fallida\|Duplicada)`, `error`, `expedienteId` |
| `CampaniaDto` | `id*`, `tipoDocumentoTenantId*`, `empresas*: string[]`, `ventanaDesde*`, `ventanaHasta*`, `recordatorios*: int[]`, `estado*`, `totales: {vinculos, renovados, pendientes}` |
| `DelegacionDto` | `id*`, `deUsuarioId*`, `aUsuarioId*`, `areaId*`, `desde*`, `hasta*`, `motivo` |
| ~~`EventoAuditoriaDto`~~ | Eliminado (#15): se usa `RegistroAuditoriaDto` de H1 (`id, fecha, tipo, actor{tipo: ActorTipo, id, nombre}, empresaId, entidad{tipo, id}, diff: DiffCampoDto[], correlationId`) |
| `PlanDto` | `codigo*`, `limites*: {usuarios, vinculosActivos, gb, decisionesIaMes, webhooks}`, `accionAlAgotar*: enum(DegradarAManual\|Continuar)` (solo Plataforma la cambia, I-23), `consumo*: UsoTenantDto` (`lecturasIa` informativo, `decisionesIa` facturable), `alertas: string[]` |
| `DominioCorreoDto` | `dominio*`, `estado*: enum(Pendiente\|Verificado\|Fallido)`, `registrosDns*: {tipo, nombre, valor}[]` |
| `MatrizRiesgoDto` | `factores*: {atributo, pesos: json}[]`, `umbrales*: {bajo, medio, alto}`, `efecto*: {nivel, nivelDebidaDiligencia}[]` |
| Reutilizados de 05 | `SolicitudSupresionDto`, `ExportacionDto`, `PlantillaNotificacionDto` |

#### 2.8.4 Eventos públicos
`campania.iniciada`, `exportacion.lista`, `habeas.solicitud_recibida`, `habeas.solicitud_resuelta`, `importacion.lote_terminado` (reservado para H10).

#### 2.8.5 Proyecciones
`SolicitudesSupresion` (inline); `InformeEstadoExpedientes` (async); `Auditoria` (async, consultable por actor/entidad); `KpisEmpresa` completos; `UsoTenant` con límites del plan; `Tercero360` completa (histórico de aceptaciones, bloqueos, exportaciones).

#### 2.8.6 Alertas
| Hecho | Plantilla | Destinatario | Canal |
|---|---|---|---|
| Campaña de renovación | `campania` | Tercero | correo (consolidado) |
| Solicitud de supresión recibida / resuelta | `habeas` | Tercero (constancia), Administrador | correo |
| Lote terminado / con errores | `lote` | Usuario que lo creó | correo + campana |
| Límite de plan al 80 % / alcanzado | `plan` | Administrador | correo |
| Exportación lista | `exportacion` | Solicitante | campana |
| Digest diario para revisores (P-10-3) | `digest` | Revisor/Administrador | correo |

#### 2.8.7 Pantallas
| Pantalla | Componentes | Lecturas |
|---|---|---|
| `web:/invitaciones/lotes` (manual, Excel, consultar lote, pausar/reanudar) | tablas, `Dropzone` | `GET /invitaciones/lotes/{id}/filas` |
| `web:/vencimientos` campañas | — | `GET /campanias` |
| `web:/configuracion/empresa/*` completa (delegaciones, separación de funciones, conflicto de intereses, dominio de correo, logo y `replyTo` por empresa T-18); los accesos de soporte se conceden solo en `/configuracion/tenant/accesos-soporte` (B-6) | — | endpoints 2.8.2 |
| `web:/configuracion/tenant/plantillas` (Liquid restringido con vista previa), `/configuracion/tenant/plan` | editor | `GET /tenant/plan` |
| `web:/auditoria` (filtros, exportación ZIP firmada) | tablas | `GET /auditoria` |
| `web:/informes/estado-expedientes` | tablas, Excel | `GET /informes/estado-expedientes` |
| `web:/configuracion/tenant/matriz-riesgo` | — | `PUT /tenant/matriz-riesgo` |
| `public:/boveda` exportar; `public:/configuracion/privacidad` (solicitud de supresión); PWA offline (cola de subidas); `public:/embed/expediente/:token` + Web Component `<validasoft-expediente>`; trazo opcional en `AceptacionConEvidencia` | — | `POST /public/v1/boveda/exportar`, `POST /public/v1/solicitudes-supresion` |

#### 2.8.8 Jobs/sagas
| Nombre | Dispara | Pasos |
|---|---|---|
| Saga `InvitacionMasiva` | `LoteCreado` | validar filas → vínculos/expedientes por lotes de 100 → correos → contadores; pausa/reanudación |
| Saga `SupresionDatos` | `SolicitudSupresionRegistrada` | retención por documento → retirar no retenido → anonimizar → constancia ≤ 15 días hábiles |
| Job `Campania` | `CampaniaIniciada` | programa `AvisoDocumento` masivo |
| Job `Digest` | diario | agrupa campanas no leídas por usuario |
| Job `LimitesPlan` | diario | compara `UsoTenant` con `PlanDto.limites` |

#### 2.8.9 Configuración
`Lotes:MaxFilas` (10 000), `Lotes:ConcurrenciaCorreos` (10), `Plan:*` por tenant, `Notificaciones:DigestHabilitado` por usuario, `Empresa.dominioCorreo`, `Tenant.matrizRiesgo`, `Habeas:DiasHabilesRespuesta` (15), `Separacion:DeFunciones` por flujo, `ConflictoInteres:Activo` (true).

#### 2.8.10 Pruebas
- Integración: lote de 1 000 invitaciones con reintentos y pausa/reanudación; informe CSV por fila.
- Habeas data: solicitud con documentos referenciados ⇒ retención legal marcada, no referenciados eliminados, constancia generada.
- Exportación: ZIP con índice firmado verificable (`hashZip`).
- E2E: embed funciona en una página de prueba con `postMessage`; PWA sube una foto sin red y sincroniza al volver.
- KPIs desde proyección: un test rompe si `KpiCard` recibe valor sin query (regla ESLint `no-mock-in-prod`).

#### 2.8.11 Efectos
Hacia atrás: H5 plantillas pasan de cabecera/pie a Liquid; H1 `UsoTenant` recibe límites; H6 `VencimientoProgramado` recibe campañas. Hacia adelante: H10 reutiliza la mecánica de lotes para la importación.

---

#### H8 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Endpoint | `/invitaciones/lotes*` (crear, listar, filas, pausar, reanudar, cancelar), `GET /informes/estado-expedientes` · `POST …/exportar`, `GET /exportaciones/{id}`, `GET /public/v1/exportaciones/{id}`, `GET /solicitudes-supresion` · `GET /{id}` · `POST /{id}/resolver`, `GET /tenant/plan`, `GET /tenant/uso`, `/empresas/{id}/dominio-correo*`, `GET/PUT /tenant/matriz-riesgo`, `POST /auditoria/exportar` · `GET /auditoria/exportaciones/{id}` | 05 §2.1, §2.4, §2.7 |
| DTO | `CrearLoteDto`, `LoteDto`, `FilaLoteDto`, `CampaniaDto`, `DelegacionDto`, `RegistroAuditoriaDto` (en lugar de `EventoAuditoriaDto`, #15), `PlanDto` (con `accionAlAgotar`), `DominioCorreoDto`, `MatrizRiesgoDto`, `ResolverSupresionDto`, `ExportarAuditoriaDto` | 05 §4 |
| Evento público | `invitaciones.lote_terminado` | 05 §7 |
| Permiso | `supresiones:resolver` (Administrador, Cumplimiento), `auditoria:exportar` (Administrador, Auditor) | 07 §3.2 |
| Pantalla | `/auditoria` → exportación ZIP firmado | 06 §3 |

### H9 — SDKs e integración con SRM, Portal y Marco

#### 2.9.1 Entidades y eventos
| Nombre | Detalle |
|---|---|
| `Validasoft.Contratos` (NuGet) | DTOs de 05 §4 + `EventoPublicoDto` + enums |
| `Validasoft.Sdk` (NuGet multi-target `net7.0;net8.0;net10.0`, T-16: el SRM sigue en .NET 7) | `IValidasoftClient` tipado (Kiota/Refit desde OpenAPI), `Idempotency-Key` automático, Polly solo en idempotentes, `WebhookVerifier.Validar(headers, body, secreto)` + dedupe por `Validasoft-Event-Id` (T-6), `X-Empresa` por cliente |
| `@validasoft/sdk` | `openapi-typescript` + `openapi-fetch` + `verificarFirma()` |
| `ApiKey` modo sandbox | `vsk_test_*` opera sobre datos sintéticos del tenant; eventos marcados `sandbox: true` |
| Receptor SRM (`/api/integracion/validasoft/webhook` en el SRM) | firma HMAC, dedupe por `Validasoft-Event-Id`, cola propia, `empresaId` = instancia SRM |
| Tabla de respaldo en Marco | `adp_cs.validasoft_certificacion_local(identificacion, tipoVinculo, empresaId, folio, estadoVinculo, estadoCertificado, bloqueado, motivoBloqueo, vigenteHasta, actualizadoEn)` alimentada con los nombres de 05 §7 (`certificado.*`, `vinculo.*`); respuesta degradada con `fuenteRespaldo: true` y `antiguedadSegundos` |

#### 2.9.2 Endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `POST /api-keys` con `modo: Sandbox` | `api-keys:administrar` | `ApiKeyDto` (+`modo`) | 201 | — | Sí |
| `GET /consultas/certificacion` (definitivo; consumido por Marco con `If-None-Match`) | `consultas:leer` | → `ConsultaCertificacionDto` | 200/304 | — | — |
| `GET /eventos?desde=&tipos=&empresaId=` (pull para SRM tras firewall) | `eventos:leer` | → `PaginaDto<EventoPublicoDto>` | 200 | — | — |
| `POST /sesiones-tercero` (Portal embebe la bóveda) | `sesiones-tercero:crear` | `SesionEmbedDto` | 201 | — | Sí |
| `GET /openapi/v1.json` | público | OpenAPI 3.1 | 200 | — | — |
| **En el SRM**: `POST /api/integracion/validasoft/webhook`; flag `ValidacionDocumental = Legacy\|Validasoft` en `ADPConfig` por empresa; cliente SDK en `InvitarProveedorPortal` | — | mapa evento→acción (08 §SRM) | 200 rápido | — | dedupe |
| **En el Portal**: iframe `/embed/expediente/{token}`; se retiran escrituras en `Adjuntos*` y el usuario `provportal` | — | — | — | — | — |

#### 2.9.3 DTOs nuevos
Ninguno: `ApiKeyDto.modo: enum(Produccion|Sandbox)` ya forma parte del único `ApiKeyDto` de 05 §4 (#32) y en H9 se habilita `Sandbox`; `EventoPublicoDto.sandbox: bool` idem. El resto se publica sin cambios en los SDKs.

#### 2.9.4 Eventos públicos consumidos por sistema
| Evento | SRM | Portal | Marco |
|---|---|---|---|
| `expediente.abierto`, `expediente.documento_cargado`, `expediente.requisito_veredicto`, `expediente.con_observaciones` | recalcula estado del proveedor (reemplaza `novedades/requeridos`) | muestra estado | — |
| `certificado.emitido` (T-15; `cambios[]`, `expedienteFolio`), `certificado.reemplazado`, `certificado.revocado`, `certificado.con_pendientes { bloqueante }`, `certificado.suspendido`, `certificado.restablecido`, `certificado.vencido` | habilita proveedor, guarda folio, aplica AyF | — | actualiza tabla local |
| `tercero.relacion_cambiada` (ADR-0010) | actualiza representante legal / socios del proveedor | — | — |
| `expediente.rechazado`, `expediente.caducado`, `expediente.anulado` | cierra habilitación | — | — |
| `documento.version_nueva`, `vinculo.revalidacion_pendiente`, `vinculo.revalidado`, `vinculo.cambio_menor` | reemplaza `actualizacion-proveedor/*` | — | marca `revalidacionPendiente` |
| `documento.por_vencer`, `documento.vencido`, `vinculo.estado_cambiado` | reemplaza `background/vencimiento-proveedor/procesar` | — | tabla local |
| `vinculo.cuenta_designada` | aplica cuenta en AyF | — | — |
| `vinculo.bloqueado`, `tercero.datos_maestros_cambiados` | novedad al gestor | — | — |
Todos con `legacy.estadoTercero` durante la coexistencia.

#### 2.9.5 Proyecciones
`EstadoCertificacionTercero` (p99 < 100 ms con ETag en H9; < 50 ms es criterio de H11, #28); `EntregasWebhook` por integración.

#### 2.9.6 Alertas
`webhook-fallido` por integración; `integracion-degradada` (Administrador, correo) cuando el SRM responde 5xx de forma sostenida.

#### 2.9.7 Pantallas
`web:/configuracion/tenant/integraciones` (API keys sandbox/producción, entregas por integración, estado del receptor SRM, dead letters con reintento); sin pantallas nuevas en `public` (embed ya existe desde H8).

#### 2.9.8 Jobs
Despachador de webhooks con partición por integración; `desplegar.yml` por entorno de cliente; publicación de paquetes (`imagenes.yml` + GitHub Packages).

#### 2.9.9 Configuración
SRM: `ADPConfig.ValidacionDocumental`, `Validasoft.TenantId`, `Validasoft.EmpresaId`, `Validasoft.ApiKey` (cifrada), `Validasoft.WebhookSecret`, `Validasoft.ModoPull` (bool). Marco: `Validasoft.CacheSegundos` (300), `Validasoft.RespaldoActivo` (true). Validasoft: `Webhooks:ParticionesPorIntegracion`.

#### 2.9.10 Pruebas
- Contract: SDKs generados del snapshot v1 compilan y sus tests de serialización pasan.
- Integración SRM (entorno de pruebas): flag `Validasoft` **por empresa** (T-18) ⇒ invitar crea expediente; `certificado.emitido` habilita al proveedor y aplica AyF; flag `Legacy` ⇒ nada cambia; receptor rechaza firma inválida y deduplica reenvíos por `Validasoft-Event-Id`; el SDK compila y corre en el SRM (.NET 7, T-16).
- Prerrequisito (B-8): H7 cerrado, de modo que listas restrictivas/SAGRILAFT existen en Validasoft antes de encender el flag en ninguna empresa.
- Portal: embed carga la bóveda y ya no escribe en `Adjuntos*`.
- Marco: con Validasoft caído responde desde la tabla local con `fuenteRespaldo: true`.
- k6: `GET /consultas/certificacion` 500 rps p99 < 100 ms (objetivo < 50 ms en H11).

#### 2.9.11 Efectos
Hacia atrás: política de deprecación de 12 meses sobre v1 (H5). Hacia adelante: H10 importa con el mismo SDK; H11 apaga endpoints legado por tenant.

---

#### H9 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Tabla | `adp_cs.validasoft_certificacion_local` en Marco (columnas, eventos que la alimentan, dedupe por `ocurridoEn`, reconciliador nocturno del 5 %, degradación con `fuenteRespaldo: true` y `antiguedadSegundos`; > 24 h exige confirmación humana para pagos) | 08 §5 |
| Endpoint | `POST /documentos` habilitado para integraciones con `documentos:escribir` (decisión abierta 3 de este documento, cerrada) | 05 §2.5 |
| Dependencia / paquete | H9 depende de H6 **y de H7** (B-8); `Validasoft.Sdk` multi-target `net7.0;net8.0;net10.0` (T-16); flag y corte por empresa (T-18); nombres de eventos de 08 alineados con 05 §7 (#3) | 09, 08 §3–§5 |

### H10 — Importador y piloto de migración

#### 2.10.1 Entidades y eventos
| Nombre | Detalle |
|---|---|
| `Validasoft.Herramientas.Importacion` (CLI) | `--tenant --empresa --fuente portal\|srm --desde --dry-run --reanudar --solo <entidad>`; lee BD Portal + disco + BD Sinco; escribe por `/importacion/*`; UUID v5 de `ClaveLegado = {origen}:{tabla}:{id}` |
| `LoteImportacion` | `LoteImportacionCreado`, `LoteImportacionTerminado(total, ok, rechazadas)`; CSV de rechazos |
| Eventos de importación (streams con `ActorTipo.Migracion`) | `TerceroCreado`, `RelacionRegistrada` (representante legal y socios de `TerCamaraComercio` como personas relacionadas, ADR-0010), `VinculoCreado(Origen: Migracion)`, `ExpedienteAbierto` + `ExpedienteAprobado` reconstruidos, `VersionCargada(Origen: Importada)`, `VersionCompartida(Origen: Importada)` (las empresas ya vinculadas en el legado se consideran aceptadas), `VersionAceptadaRegistrada` (desde `AdjuntosConstructora.ACActivo=1`), `AceptacionDesactualizada(PlazoRevalidacion = 90)` cuando el espejo no coincide con la última versión del tercero (B-8), `RequisitoRatificado` (`OrigenActualizacion = Ratificacion`), `CertificadoEmitido` retroactivo con sufijo `-M` (folio `{PrefijoEmpresa}-C-{Año}-{Seq}-M`, T-9); **ninguna lectura IA** hasta que el tercero dé consentimiento en su primer ingreso (B-8) |
| Sincronización inversa | suscriptor de webhooks que escribe estado/aprobaciones en tablas legado 90 días con `legacy.estadoTercero`; corte **por empresa** (T-18): un tercero compartido entre una empresa migrada y otra no migrada se resuelve según 08 §7 |

#### 2.10.2 Endpoints
| Endpoint | Permiso | Entrada → Salida | Respuestas | Emite | Idem. |
|---|---|---|---|---|---|
| `POST /importacion/tipos-documento` · `/terceros` · `/relaciones` · `/cuentas` · `/vinculos` · `/documentos` · `/expedientes` · `/vencimientos` · `/certificados` (lotes ≤ 500) | `importacion:escribir` | `LoteImportacionDto` (cuerpo) → `ImportacionLoteDto` (respuesta) (#16) | 202; 207 por fila | `importacion.lote_terminado` | Sí (por `claveLegado`) |
| `GET /importacion/lotes/{id}` · `GET /importacion/lotes/{id}/rechazos` | `importacion:escribir` | → `ImportacionLoteDto`, CSV | 200 | — | — |
| `POST /importacion/binarios` (**registro** de binarios que el importador ya copió a MinIO con `mc mirror`; no sube bytes ni devuelve URL prefirmada, #16) | `importacion:escribir` | `BinarioImportadoDto` | 202; 409 `hash-ya-registrado` | — | Sí |

#### 2.10.3 DTOs nuevos
| DTO | Campos |
|---|---|
| `LoteImportacionDto` (**cuerpo**, 05 §4) | `fuente*: enum(Portal\|SRM)`, `filas*: json[]` (cada fila con `claveLegado*` y el DTO correspondiente: `TerceroDto`, `RelacionImportadaDto`, `VinculoDto`, `VersionDocumentoDto` + `sha256`, `AceptacionImportadaDto`, `ExpedienteImportadoDto`, `VencimientoImportadoDto`, `CertificadoImportadoDto`), `modo*: enum(DryRun\|Aplicar)`; tenant y empresa salen de la credencial y de `X-Empresa` |
| `RelacionImportadaDto` | `claveLegado*`, `terceroClaveLegado*`, `personaIdentificacion*: IdentificacionDto`, `personaNombre*`, `tipo*: TipoRelacionTerceros`, `desde`, `participacion` |
| `BinarioImportadoDto` | `sha256*`, `claveS3*`, `tamanoBytes*`, `mime*`, `origen*: {tabla, id}` |
| `AceptacionImportadaDto` | `claveLegado*`, `vinculoClaveLegado*`, `tipoCanonico*`, `versionClaveLegado*`, `aceptadaEn*`, `aceptadaPor`, `vigenteHasta`, `plazoRevalidacionDias*: int (90, B-8)` |
| `ExpedienteImportadoDto` | `claveLegado*`, `vinculoClaveLegado*`, `tipo*`, `estadoLegado*: int`, `estado*: EstadoExpediente`, `veredictos*: {area, veredicto, usuario, fecha, motivos[]}[]`, `fechas*{apertura, cierre}` |
| `ImportacionLoteDto` (**respuesta**, 05 §4) | `id*`, `entidad*`, `estado*`, `totales*: {filas, insertadas, actualizadas, rechazadas}`, `rechazosUrl`, `inicio*`, `fin` |

#### 2.10.4 Eventos públicos
`importacion.lote_terminado`; durante la inversa, todos los eventos de H4–H7 con `legacy.estadoTercero`.

#### 2.10.5 Proyecciones
`LotesImportacion` (inline) → `web:/plataforma/importacion`; todas las proyecciones existentes se reconstruyen tras la carga (`rebuild` por tenant).

#### 2.10.6 Alertas
`importacion-terminada` (Administrador de plataforma, correo); comunicación a proveedores: plantilla `migracion-bienvenida` (tercero, correo, con magic link; P-13-3).

#### 2.10.7 Pantallas
`web:/plataforma/importacion` (lotes, rechazos, reanudar, rebuild de proyecciones); `web:/plataforma/tenants/:id/corte` (checklist: solo lectura legado → delta → flag → inversa → rollback).

#### 2.10.8 Jobs
CLI por lotes; `rebuild` de proyecciones async; suscriptor de sincronización inversa (90 días); re-verificación masiva con Batches API para versiones importadas sin hechos.

#### 2.10.9 Configuración
`Importacion:TamanoLote` (500), `Importacion:Concurrencia` (4), `Importacion:AniosHistorialDecisiones` (3), `Inversa:Dias` (90), mapeo `constructora → (tenant, empresa)` en archivo del piloto.

#### 2.10.10 Pruebas
- Dry-run sobre copias de BD Portal + Sinco de pruebas sin rechazos críticos; reejecución idempotente (mismos UUID v5, cero duplicados); conteo de binarios por hash igual al origen.
- Espejos divergentes: una constructora con `AdjuntosConstructora` distinta de la última versión nace `Desactualizada` con revalidación pendiente, sin molestar al tercero.
- Piloto en pruebas: corte completo y rollback ensayado (volver el flag a `Legacy` con tablas legado al día por la inversa).

#### 2.10.11 Efectos
Hacia atrás: puede destapar tipos del legado sin canónico ⇒ ampliar semilla (H3) y catálogo (H2). Hacia adelante: H11 apaga lo legado por tenant migrado.

---

#### H10 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Endpoint | `POST /importacion/binarios` (`BinarioImportadoDto`), `GET /importacion/lotes/{id}/rechazos` (`RechazoImportacionDto[]`, CSV con `Accept: text/csv`) | 05 §2.9 |
| DTO | `LoteImportacionDto`, `ImportacionLoteDto`, `RechazoImportacionDto`, `AceptacionImportadaDto`, `ExpedienteImportadoDto` | 05 §4 |

### H11 — Endurecimiento y retiro del legado

#### 2.11.1–2.11.9
| Ámbito | Entregable |
|---|---|
| Rendimiento | k6: `GET /consultas/certificacion` 500 rps p99 < 50 ms; `GET /public/v1/verificar/{folio}` 100 rps; subida concurrente 200 archivos |
| Continuidad | pgBackRest + WAL (RPO 15 min), restauración automatizada en pipeline (RTO 4 h), `mc mirror` de MinIO, prueba trimestral |
| Seguridad | Object Lock modo *compliance* en prod (T-14), ClamAV obligatorio (T-11), sello de tiempo RFC 3161 opcional en certificados (`Certificados:SelloDeTiempo`), auditoría externa, rotación de claves KMS y de la clave Ed25519 (T-12) |
| Operación | Dashboards Grafana versionados (`infra/grafana`), alertas de plataforma (`dead-letters`, `webhooks-fallidos`, `ia-latencia`), Helm opcional, RabbitMQ opcional sin tocar handlers, regiones adicionales (`Tenant.region`) |
| Retiro del legado | Por **empresa** migrada (T-18) y, cuando todas las empresas de un tenant lo están, por tenant: apagar `PortalProveedorERP/*`, `background/*`, jobs del Portal (`ServiceBackgroudVencimiento`), tablas `AdjuntosConstructora`, `VencimientosProveedor`, `ActualizacionTerceroConstructora`; retirar `legacy.*` de los eventos |
| Endpoints nuevos | `GET /plataforma/operacion/dead-letters` · `POST …/{id}/reintentar` (`plataforma:operacion`), `GET /plataforma/operacion/salud` |
| Alertas | `plataforma-incidente` (Plataforma, correo) |
| Pantallas | `web:/plataforma/operacion` (dead letters, entregas fallidas, salud, costes) |
| Configuración | `Certificados:SelloDeTiempo:Url`, `Backups:*`, `Alertas:Umbrales:*` |

#### 2.11.10 Pruebas
Objetivos de carga alcanzados; restauración en pipeline verde; endpoints legado devuelven 410 por tenant migrado; auditoría de seguridad sin hallazgos críticos.

#### 2.11.11 Efectos
Cierra la coexistencia; `legacy.estadoTercero` desaparece del catálogo cuando no quede tenant en coexistencia (cambio aditivo inverso, anunciado 12 meses antes).

---

#### H11 — Cierre de huecos (2026-10-28)
| Tipo | Elemento | Definido en |
|---|---|---|
| Endpoint | `GET /plataforma/operacion/dead-letters`, `POST …/dead-letters/{id}/reintentar`, `POST …/proyecciones/{nombre}/rebuild`, `GET …/salud` (`DeadLetterDto`, `RebuildDto`; permiso `plataforma:operacion`) | 05 §2.1, 07 §3.2 |

## 3. Matrices globales

### 3(a) Endpoint → hito

| Grupo (05 §2) | Endpoints | Hito | Estado |
|---|---|---|---|
| Plataforma/tenant/empresas | `/plataforma/tenants*`, `/plataforma/uso`, `/catalogos/tipos-identificacion`, `/catalogos/paises`, `/tenant`, `/tenant/accesos-soporte*` (B-6), `/empresas*`, `/api-keys*`, `/usuarios*` (sin delegaciones), `/roles`, `/permisos`, `/health*`, OIDC (incl. `urn:validasoft:magic-link`), `/openapi/v1.json`, `/openapi/public-v1.json`, `GET /auditoria` (consulta; tipos configuración e identidad) | H1 | Planificado |
| Catálogo canónico y bóveda | `/plataforma/tipos-documento/canonicos*`, `/plataforma/propuestas-tipo*`, `/catalogos/esquemas-formulario`, `/terceros*` (sin `frescura`, sin `ficha`), `/terceros/{id}/documentos`, `/terceros/{id}/cuentas-bancarias`, `/terceros/{id}/relaciones*` (ADR-0010), `POST /documentos` (importador), `/documentos*`, `/public/v1/enlaces/*`, `/public/v1/boveda*` (sin `exportar`; incluye `documentos` POST, `stream`, `cuentas-bancarias` GET), `/public/v1/tercero/datos-maestros`, `/public/v1/tercero/relaciones*` | H2 | Planificado |
| Configuración | `/tipos-documento*`, `/tipos-documento/propuestas`, `/tipos-documento/{id}/plantilla`, `/empresas/{id}/tipos-documento*` (overrides B-4), `/areas*`, `/motivos*`, `/catalogos/motivos-base`, `/tipos-vinculo*`, `/flujos*`, `/flujos/{id}/simular`, `/flujos/reglas-seleccion*`, `/flujos/por-defecto`, `/flujos/resumen` | H3 | Planificado |
| Vínculos y expedientes | `/vinculos*` (sin cuenta designada), `/expedientes*` (sin `ratificar`, `verificaciones`, `exportar`), `/expedientes/{id}/areas/{areaId}/rechazar` (B-7), `/expedientes/{id}/cambiar-flujo`, `/flujos/{id}/versiones/{n}/aplicar-a-abiertos`, `/mis-pendientes`, `/terceros/{id}/ficha`, `/novedades*`, `/public/v1/expedientes/{id}`, `/public/v1/invitaciones/{expedienteId}/aceptar` (ADR-0009), `/public/v1/autorregistro` (T-13), `/public/v1/vinculos`, SSE | H4 | Planificado |
| Certificados, webhooks, notificaciones | `/certificados*`, `/public/v1/verificar/{folio}`, `/public/v1/certificados/{folio}`, `/consultas/certificacion` (MVP), `/webhooks*`, `/eventos`, `/notificaciones*`, `/plantillas-notificacion*` (cabecera/pie), `/usuarios/me/preferencias-notificacion`, `/sesiones-tercero` | H5 | Planificado |
| Vigencias y revalidación | `/revalidaciones`, `/expedientes/{id}/requisitos/{rid}/ratificar`, `/vencimientos*`, `/vencimientos/proximos`, `/terceros/{id}/frescura`, `/public/v1/frescura`, `/vinculos/{id}/cuenta-designada*`, `/public/v1/vinculos/{id}/cuenta-designada`, `/public/v1/boveda/cuentas-bancarias` POST | H6 | Planificado |
| Verificaciones y revisión automática | `/expedientes/{id}/verificaciones*`, `/excepciones*`, `/public/v1/expedientes/{id}/requisitos/{rid}/revision-humana`, `/tenant/revision-automatica`, `/kpis` (completo), `/informes/cumplimiento*` | H7 | Planificado |
| Operación masiva y administración | `/invitaciones/lotes*`, `/campanias*`, `/usuarios/{id}/delegaciones*`, `/expedientes/{id}/exportar`, `/exportaciones/{id}`, `/public/v1/boveda/exportar`, `/public/v1/exportaciones/{id}`, `/public/v1/solicitudes-supresion*`, `/solicitudes-supresion*`, `/auditoria/exportar`, `/auditoria/exportaciones/{id}`, `/plantillas-notificacion*` (Liquid), `/tenant/plan`, `/tenant/uso`, `/empresas/{id}/dominio-correo*`, `/informes/estado-expedientes*`, `/tenant/matriz-riesgo` | H8 | Planificado |
| Integración | `/api-keys` (sandbox), `POST /documentos` (integraciones con `documentos:escribir`), receptor SRM, tabla `validasoft_certificacion_local` en Marco | H9 | Planificado |
| Importación | `/importacion/*` (incl. `/importacion/relaciones`), `/importacion/lotes/{id}*`, `/importacion/binarios` (registro, no carga) | H10 | Planificado |
| Operación de plataforma | `/plataforma/operacion/*` | H11 | Planificado |

### 3(b) Evento público → hito → consumidores

| Evento | Hito | web | public | SRM | Portal | Marco |
|---|---|---|---|---|---|---|
| `tenant.creado`, `empresa.creada`, `tenant.acceso_soporte_concedido`, `tenant.acceso_soporte_expirado` | H1 | ✓ | — | — | — | — |
| `catalogo.publicado`, `tercero.creado`, `tercero.relacion_cambiada`, `tercero.datos_maestros_cambiados`, `documento.hechos_registrados`, `documento.version_nueva`, `documento.version_devuelta`, `documento.datos_corregidos` | H2 | ✓ | ✓ | ✓ (H9) | ✓ (H9) | — |
| `flujo.publicado`, `configuracion.cambiada`, `identidad.evento` (H1 para identidad y configuración básica) | H3 | ✓ | — | — | — | — |
| `expediente.flujo_seleccionado`, `expediente.flujo_cambiado`, `flujo.aplicado_a_abiertos` | H4 | ✓ | — | ✓ (H9) | — | — |
| `invitaciones.lote_terminado` | H8 | ✓ | — | — | — | — |
| `vinculo.creado`, `vinculo.atributos_actualizados`, `vinculo.estado_cambiado` (causa `Vigencia`), `vinculo.bloqueado`, `vinculo.desbloqueado`, `vinculo.desvinculado`, `vinculo.reintegrado`, `vinculo.anulado`, `tercero.autorregistrado`, `documento.compartido`, `expediente.abierto`, `expediente.reabierto`, `expediente.invitacion_enviada`, `expediente.invitacion_aceptada`, `expediente.documento_cargado`, `expediente.enviado_a_revision`, `expediente.reenviado`, `expediente.etapa_activada`, `expediente.area_aprobada`, `expediente.requisito_veredicto`, `expediente.requisito_eximido`, `expediente.requisitos_recalculados`, `expediente.con_observaciones`, `expediente.reenvios_excedidos`, `expediente.aprobado`, `expediente.rechazado`, `expediente.caducado`, `expediente.anulado`, `expediente.sla_incumplido`, `expediente.sla_escalado`, `expediente.recordatorio_cargue` | H4 | ✓ | ✓ | ✓ (H9) | ✓ (H9) | — |
| `certificado.emitido` (único evento de certificación, T-15), `certificado.reemplazado`, `certificado.revocado`, `notificacion.entregada`, `notificacion.fallida`, `webhook.deshabilitado`, `prueba.ping` | H5 | ✓ | ✓ | ✓ (H9) | — | ✓ (H9) |
| `vinculo.revalidacion_pendiente`, `vinculo.revalidado`, `vinculo.cambio_menor`, `vinculo.cuenta_designada_propuesta`, `vinculo.cuenta_designada`, `vinculo.cuenta_designada_rechazada`, `expediente.requisito_desactualizado`, `expediente.requisito_vencido`, `documento.ratificado`, `documento.por_vencer`, `documento.vencido`, `certificado.con_pendientes`, `certificado.suspendido`, `certificado.restablecido`, `certificado.vencido` | H6 | ✓ | ✓ | ✓ (H9) | ✓ (H9) | ✓ (H9) |
| `expediente.verificacion_registrada`, `expediente.coincidencia_resuelta`, `expediente.revision_humana_solicitada`, `ia.presupuesto_80`, `ia.presupuesto_agotado`, `exportacion.lista` | H7 | ✓ | ✓ | ✓ (H9) | — | — |
| `campania.iniciada`, `habeas.solicitud_recibida`, `habeas.solicitud_resuelta` | H8 | ✓ | ✓ | — | — | — |
| `importacion.lote_terminado` | H10 | ✓ | — | — | — | — |

### 3(c) Alerta (plantilla) → hito

| Plantilla | Hito | Canal |
|---|---|---|
| `cuenta-verificacion`, `cuenta-2fa` | H1 | correo |
| `observacion-automatica` (devuelta), `consentimiento-plataforma`, `invitacion` (persona relacionada) | H2 | correo, campana |
| `invitacion`, `recordatorio-cargue`, `observacion`, `rechazo`, `caducidad`, `sla`, `consentimiento-empresa`, `aprobado`, `autorregistro` | H4 | correo, campana |
| `certificado`, `certificado-revocado`, `webhook-fallido` | H5 | correo, campana |
| `revalidacion`, `por-vencer`, `vencido`, `revalidacion-vencida`, `cuenta-designada`, `certificado-pendientes` | H6 | correo, campana, digest |
| `observacion-automatica` (IA), `coincidencia`, `verificacion-expirada`, `presupuesto-ia` | H7 | correo, campana |
| `campania`, `habeas`, `lote`, `plan`, `exportacion`, `digest` | H8 | correo, campana |
| `integracion-degradada` | H9 | correo |
| `importacion-terminada`, `migracion-bienvenida` | H10 | correo |
| `plataforma-incidente` | H11 | correo |

### 3(d) Pantalla → hito

| Pantalla | Hito |
|---|---|
| `web:/login`, AppShell + `SelectorEmpresa`, `/plataforma/tenants`, `/plataforma/tenants/:id/empresas`, `/plataforma/catalogos/identificacion`, `/configuracion/tenant/usuarios`, `/configuracion/tenant/api-keys`, `/configuracion/tenant/accesos-soporte`; `public:` shell | H1 |
| `web:/plataforma/catalogo`, `/terceros/:id` básico (con personas relacionadas); `public:/e/:token`, `/boveda` básica, `/personas` | H2 |
| `web:/flujos`, `/flujos/:id/editor`, `/configuracion/tenant/tipos-documento`, `/configuracion/tenant/formularios`, `/configuracion/empresa/areas`, `/configuracion/empresa/tipos-documento`, `/configuracion/empresa/flujos-por-defecto`, `/configuracion/tenant/motivos`; `public:` vista previa | H3 |
| `web:/expedientes`, `/expedientes/:id`, `/inicio`, `/terceros/:id` (bloqueo); `public:/e/:token` aceptar invitación, `/expediente/:id`, `/expediente/:id/requisito/:rid`, `/registro`, `/vinculos` básico; campana | H4 |
| `web:/certificados`, `/certificados/:folio`, `/configuracion/tenant/webhooks`, `/configuracion/tenant/plantillas`, `/configuracion/tenant/notificaciones`; `public:/certificado/:folio`, `/verificar/:folio`, `/vinculos` | H5 |
| `web:/revalidaciones`, `/vencimientos`, `/terceros/:id` completo, pestañas Renovaciones/Revalidaciones; `public:/boveda` con vencimientos, `/cuentas`, edición de formularios | H6 |
| `web:/excepciones`, `/configuracion/tenant/revision-automatica`, pestaña Verificaciones, `/informes/cumplimiento`; `public:` revisión humana, UBO/PEP | H7 |
| `web:/invitaciones/lotes`, campañas, `/configuracion/empresa/*` completa, plantillas Liquid, `/configuracion/tenant/plan`, `/auditoria`, `/informes/estado-expedientes`, `/configuracion/tenant/matriz-riesgo`; `public:` exportar bóveda, `/configuracion/privacidad`, PWA offline, `/embed/expediente/:token` | H8 |
| `web:/configuracion/tenant/integraciones` | H9 |
| `web:/plataforma/importacion`, `/plataforma/tenants/:id/corte` | H10 |
| `web:/plataforma/operacion` | H11 |

### 3(e) Permiso → rol → hito

| Permiso | Roles | Hito |
|---|---|---|
| `plataforma:tenants`, `plataforma:uso`, `plataforma:catalogo`, `plataforma:operacion`, `plataforma:soporte` (solo con concesión vigente, B-6) | `Plataforma` | H1 (catalogo H2) |
| `empresas:administrar`, `api-keys:administrar`, `usuarios:administrar`, `configuracion:leer`, `configuracion:escribir`, `operacion:leer`, `auditoria:leer` | `Administrador` (auditoría también `Cumplimiento`, `Auditor`) | H1 (configuración H3) |
| `terceros:leer`, `terceros:escribir`, `relaciones:escribir`, `documentos:leer`, `documentos:escribir`, `documentos:corregir-datos` | `Administrador`, `Revisor` (leer), `Integracion` | H2 |
| `vinculos:leer`, `vinculos:escribir`, `vinculos:bloquear`, `expedientes:leer`, `expedientes:escribir`, `expedientes:asignar`, `expedientes:rechazar:area:{X}` (B-7), `requisitos:eximir`, `veredictos:emitir:area:{X}` | `Administrador`, `Revisor` (por área), `Integracion` (leer/escribir) | H4 |
| `certificados:leer`, `certificados:revocar`, `consultas:leer`, `webhooks:administrar`, `eventos:leer`, `sesiones-tercero:crear`, `auditoria:leer` | `Administrador`, `Auditor` (leer), `Integracion` | H5 |
| `vencimientos:leer`, `vencimientos:escribir`, `cuentas:aprobar` | `Administrador`, `Revisor` (financiera) | H6 |
| `verificaciones:escribir` | `Cumplimiento`, `Administrador` | H7 |
| `supresiones:resolver`, `auditoria:exportar` | `Administrador`, `Cumplimiento` / `Administrador`, `Auditor` | H8 |
| `importacion:escribir` | `Plataforma`, `Integracion` (migración) | H10 |
| Rol `Tercero` (sesión por magic link) | — | H2 |

### 3(f) Configuración → hito

| Clave / campo | Hito |
|---|---|
| `Tenant.region`, `Tenant.claveKms`, `Seguridad:DosFactoresObligatorioRoles`, `Ingesta:*`, `Marten:Esquemas` (T-1/T-2) | H1 |
| `Ia:Habilitada`, `Ia:Modelo*`, `Ia:FailClosed`, `Antivirus:Modo`, `Retencion:GraciaDiasNoReferenciadas`, semilla canónica (03 Anexo A) | H2 |
| `TipoDocumentoTenantDto.*` (ámbito, ayuda, plantilla), `TipoDocumentoEmpresaDto.*` (override B-4), `FlujoPorDefectoDto`, `FlujoDefinicionDto.*` (etapas, políticas, plazo), semilla "Habilitación de proveedores" | H3 |
| `slaHoras`, `escalamientoA`, `aprobadoresRequeridos`, `separacionDeFunciones`, `Notificaciones:LimiteCorreosPorHechoYDia` | H4 |
| `Certificados:ClaveEd25519` (T-12), `Almacenamiento:ObjectLockModo` (T-14), `Webhooks:*`, `Notificaciones:RemitenteDominio`, `EmpresaDto.replyTo`, `EmpresaDto.logoUrl` (T-18), `Notificaciones:DigestHora` | H5 |
| `reglaFrescura`, `diasAviso`, `diasGracia`, `diasRevalidacion`, `politicaDesactualizada`, `permiteRatificacion`, `politicaVigenciaVinculo`, `Vencimientos:HoraJobDiario` | H6 |
| `RevisionAutomaticaConfigDto.*`, `frecuenciaReconsultaMeses`, `Verificadores:*`, `Ia:ModeloPorTipo` | H7 |
| `Lotes:*`, `Plan:*` (incl. `PlanDto.accionAlAgotar`, I-23), `Empresa.dominioCorreo`, `Tenant.matrizRiesgo`, `Habeas:DiasHabilesRespuesta`, `ConflictoInteres:Activo` | H8 |
| SRM `ADPConfig.ValidacionDocumental` (por empresa, T-18), `Validasoft.*`; Marco `Validasoft.CacheSegundos`, `Validasoft.RespaldoActivo` | H9 |
| `Importacion:*`, `Importacion:PlazoRevalidacionDias` (90, B-8), `Inversa:Dias` (90) | H10 |
| `Certificados:SelloDeTiempo:*`, `Backups:*`, `Alertas:Umbrales:*` | H11 |

---

## 4. Qué viaja por dónde (protocolos)

| Necesidad | Mecanismo | Detalle |
|---|---|---|
| Comandos y consultas de usuarios, terceros e integradores | **REST JSON** `/api/v1`, `/public/v1` (OpenAPI 3.1) | `Idempotency-Key`, `ETag/If-Match`, cursor, ProblemDetails, cabecera `X-Empresa` (T-5; `X-Tenant` solo en `/plataforma/*`), 404 para recursos de otro tenant/empresa (T-4) |
| Binarios (subida y descarga) | **URL prefirmada S3/MinIO** | El API nunca proxea bytes; `x-amz-checksum-sha256`; `DocumentoAccedido` por emisión |
| Cambios de estado hacia sistemas externos | **Webhooks firmados** (HMAC-SHA256, reintentos, dedupe por `Validasoft-Event-Id`) | Por empresa; `legacy.estadoTercero` en coexistencia |
| Sistemas tras firewall | **Pull** `GET /eventos?desde=cursor` | Mismo sobre `EventoPublicoDto` y misma secuencia |
| Tiempo real en las SPAs | **SSE** `/api/v1/eventos/stream` (empresa), `/public/v1/boveda/stream` (tercero, desde H2) y `/public/v1/expedientes/{id}/stream` (un expediente) | `Last-Event-ID` = cursor; reenvío 24 h |
| Avisos a personas | **Correo** (MailKit, Fluid), **campana** (`Novedad` + SSE) | 1 correo por tercero, hecho y día; evidencia de entrega |
| Procesos internos | Wolverine sobre PostgreSQL (outbox, colas, scheduled, sagas) | Nunca HTTP entre módulos |
| **gRPC** | **No en el MVP (ADR-0007).** Criterio de reevaluación: solo si `GET /consultas/certificacion` con ETag y caché no cumple p99 < 50 ms a 500 rps en H11; se añadiría como fachada interna sobre el mismo caso de uso, sin tocar el dominio ni la API pública. |
| GraphQL / colas externas para integradores | No | Consumidores son sistemas; REST + SDK generado |

---

## 5. Huecos detectados y su cierre

Los 15 huecos originales, los 2 añadidos por Producto y los 4 que dejó la **segunda relectura** (auditorías de negocio y técnica; ver [decisiones/relectura-2026-10-28.md §5](decisiones/relectura-2026-10-28.md)) quedaron **cerrados el 2026-10-28**; cada fila indica dónde se definió el contrato y el bloque "Cierre de huecos" del hito correspondiente lo traza.

| # | Hueco | Resolución | Estado |
|---|---|---|---|
| 1 | SSE de la bóveda antes de existir expediente | `GET /public/v1/boveda/stream` (eventos listados) | **Cerrado en 05 §2.9, §3.4** |
| 2 | Carga directa a bóveda sin expediente | `POST /public/v1/boveda/documentos` (tercero) y `POST /documentos` (importador H2, integraciones H9); `IniciarCargaDto.tipoCanonico*`, `terceroId*` | **Cerrado en 05 §2.5, §2.9, §4** |
| 3 | 33 DTOs sin catálogo | Todos añadidos a 05 §4 con campos y obligatoriedad (`TenantDto` … `ExpedienteImportadoDto`) | **Cerrado en 05 §4** |
| 4 | 17 endpoints sin listar | Añadidos con permiso, DTOs, respuestas y eventos (plantilla de tipo, preferencias, revisión automática, informes, lotes, exportaciones, supresión lado empresa, plan/uso, dominio de correo, matriz de riesgo, binarios de importación, rechazos, operación de plataforma, OpenAPI) | **Cerrado en 05 §2.1–§2.9** |
| 5 | Rutas de 06 distintas a 05 | 06 alineado: `/public/v1/boveda/cuentas-bancarias`, `POST /public/v1/vinculos/{id}/cuenta-designada`, `/catalogos/tipos-identificacion`, `/public/v1/boveda`, `/public/v1/expedientes/{id}/stream` | **Cerrado en 06 §2, §4(g), §5** |
| 6 | Nombres en 04 | `VersionActualCambiada`, `CuentaDesignadaAprobada`/`Rechazada`; `AnalisisDeVersionIniciado` en la saga | **Cerrado en 04 §4.2, §5** |
| 7 | `expediente.recordatorio_cargue` fuera del catálogo | Fila añadida con payload | **Cerrado en 05 §7** |
| 8 | `KpisRevisionAutomatica`/`KpisEquidadIA` | Campos `decisionesAutomaticas`, `consumoIa`, `equidad` de `KpisTenant`; retirados los nombres sueltos en 04 y 06; `KpisDto` ampliado | **Cerrado en 03 §14, 04, 05 §4, 06 §5** |
| 9 | Proyecciones sin definir | `MisPendientes`, `Tercero360`, `CuentasTercero`, `FlujosResumen`, `VencimientosProximos` definidas con fuente, tipo, campos y endpoint | **Cerrado en 03 §14** |
| 10 | Supresión lado empresa | `GET /solicitudes-supresion`, `GET /{id}`, `POST /{id}/resolver`; permiso `supresiones:resolver` | **Cerrado en 05 §2.7, 07 §3.2** |
| 11 | Evidencia de aceptación | `AceptacionDto`; `ConsentimientosRegistrados` ampliado | **Cerrado en 05 §4, 03 §5.3** |
| 12 | Tabla de respaldo de Marco | `adp_cs.validasoft_certificacion_local` con esquema, alimentación, reconciliación y degradación | **Cerrado en 08 §5** |
| 13 | Plantilla del certificado y `HashContenido` | Esquema `validasoft.certificado.v1`, RFC 8785, sello Ed25519, PDF/A, verificación; `CertificadoContenidoDto` | **Cerrado en 03 §7.1, 05 §4** |
| 14 | Precedencia API key ∩ webhook | Intersección; `422 empresas-fuera-de-alcance`; deshabilitación posterior | **Cerrado en 05 §2.8, 07 §4** |
| 15 | `AnalisisDeVersionIniciado` ausente en 04 | Añadido al paso 1 de `VerificacionDocumental` | **Cerrado en 04 §5** |
| 16 | **Auditoría de configuración e identidad** | `RegistroDeCambioDeConfiguracion`, `RegistroDeIdentidad`, invariante "ningún cambio de configuración sin registro", `GET /auditoria?tipo=`, `POST /auditoria/exportar`, `RegistroAuditoriaDto`, `DiffCampoDto`, permisos `auditoria:leer`/`auditoria:exportar`, retención P-14-7, pantalla `/auditoria` con diff | **Cerrado en 03 §2.1, 02 §13 (inv. 14), 05 §2.7 y §4, 07 §3.2 y §6, 06 §3; trazado en H1 y H8** |
| 17 | **Selección automática y cambio de flujo** | `ReglaDeSeleccionDeFlujo`, flujo por defecto obligatorio, `FlujoSeleccionado`, `CambiarFlujoDeExpediente`, `AplicarVersionDeFlujoAExpedientesAbiertos(Simular)`; endpoints, DTOs y eventos; editor con pestaña "Reglas de selección" y acción "Aplicar a expedientes abiertos"; detalle con "Cambiar flujo" | **Cerrado en 03 §6.3 y §12.1, 05 §2.2, §2.4, §4, §7, 06 §3, 09 H3/H4; trazado en H3 y H4** |
| 18 | **Calendario hábil y zona horaria** (SLA, plazos de cargue y revalidación, respuesta habeas data) | Días hábiles por país del tenant (`Tenant.zonaHoraria`, `Tenant.calendarioHabil`); todo plazo "en días" de 03 se evalúa en días hábiles salvo vigencias documentales (calendario) | **Cerrado en 05 §4 (`TenantDto.zonaHoraria`, `TenantConfigDto.calendarioHabil {pais, festivosExtra}`); pendiente de que 03 §3 añada la convención "plazos en días hábiles del tenant salvo vigencias documentales" (hueco menor, H1); trazado en H1 (`TenantDto`) y H4/H6 (jobs)** |
| 19 | **Versionado de esquemas de formulario** (qué pasa con versiones cargadas con un esquema anterior) | `EsquemaFormularioDto.version`; una versión cargada guarda su `esquemaVersion`; publicar esquema nuevo no invalida versiones aceptadas; solo exige campos nuevos obligatorios en la siguiente actualización | **Cerrado en 05 §4 (`EsquemaFormularioDto.versionEsquema`) y 02 §4.4; pendiente de que 03 §4 enuncie la regla "publicar esquema nuevo no invalida versiones aceptadas" (hueco menor, H2); trazado en H2** |
| 20 | **Conversión de ámbito PorVinculo → Global** de un tipo de documento | Permitida solo por Plataforma sobre el canónico, con migración de versiones por tercero (la más reciente pasa a global, las demás quedan históricas por vínculo); nunca al revés sin restringir | **Cerrado en 03 §10; trazado en H2/H3 (`ambito-no-ampliable`)** |
| 21 | **Coincidencia confirmada en listas restrictivas** | `CoincidenciaResuelta(Resultado: Confirmada)` bloquea `ExpedienteAprobado`, suspende el certificado vigente (`CertificadoSuspendido`, B-5) y abre excepción para Cumplimiento; nunca se aprueba automáticamente | **Cerrado en 03 §11 y 04 §7.5; trazado en H7** |

<details><summary>Tabla original de huecos (histórico)</summary>

| # | Hueco | Nombre propuesto | Documento que debe definirlo | Hito |
|---|---|---|---|---|
| 1 | 05 no define el stream SSE de la bóveda del tercero (antes de existir expediente) ni el stream general del tercero | `GET /public/v1/boveda/stream` | 05 §1/§3.4 | H2 |
| 2 | `POST /documentos` (carga directa a bóveda sin expediente, usada por `public:/boveda` e importador) no está en 05 §2.5 | `POST /documentos` con `tipoCanonico` en `IniciarCargaDto` | 05 §2.5, §4 | H2 |
| 3 | DTOs de plataforma/tenant no están en el catálogo de 05: `TenantDto`, `TenantConfigDto`, `PaisDto`, `ApiKeyDto`, `UsuarioDto`, `RolDto`, `PermisoDto`, `PropuestaTipoDto`, `EsquemaFormularioDto`, `DatosMaestrosDto`, `TipoVinculoDto`, `RequisitoCalculadoDto`, `BandejaExpedienteDto`, `NovedadDto`, `TimelineEventoDto`, `PlantillaNotificacionDto`, `PreferenciasNotificacionDto`, `RevalidacionDto`, `RevisionAutomaticaConfigDto`, `ExcepcionDto`, `EstudioCumplimientoDto`, `CrearLoteDto`, `LoteDto`, `FilaLoteDto`, `CampaniaDto`, `DelegacionDto`, `EventoAuditoriaDto`, `PlanDto`, `DominioCorreoDto`, `MatrizRiesgoDto`, `ImportacionLoteDto`, `AceptacionImportadaDto`, `ExpedienteImportadoDto`, `LoteImportacionDto` | los de §2.x.3 de este documento | 05 §4 | H1–H10 |
| 4 | Endpoints usados aquí y no listados en 05: `POST /tipos-documento/{id}/plantilla`, `GET/PUT /usuarios/me/preferencias-notificacion`, `GET/PUT /tenant/revision-automatica`, `GET /informes/cumplimiento*`, `GET /informes/estado-expedientes*`, `/invitaciones/lotes*`, `GET /exportaciones/{id}`, `GET /public/v1/exportaciones/{id}`, `GET /solicitudes-supresion` + `/resolver` (lado empresa), `GET /tenant/plan`, `GET /tenant/uso`, `/empresas/{id}/dominio-correo*`, `PUT /tenant/matriz-riesgo`, `POST /importacion/binarios`, `GET /importacion/lotes/{id}/rechazos`, `/plataforma/operacion/*`, `GET /openapi/v1.json` | los indicados | 05 §2 | H3–H11 |
| 5 | 06 usa rutas distintas a 05: `GET /terceros/{id}/cuentas` (05: `/terceros/{id}/cuentas-bancarias`), `PUT /vinculos/{id}/cuenta-designada` (05: `POST /public/v1/vinculos/{id}/cuenta-designada`), `GET /catalogos/identificacion` (05: `/catalogos/tipos-identificacion`), `GET /tipos-documento/{id}/esquema` (coincide), `GET /boveda` (05: `/public/v1/boveda`) | alinear 06 a 05 | 06 §4(g), §5 | H2/H6 |
| 6 | 04 nombra `VersionPromovidaAActual` y `CuentaDesignadaCambiada`; 03 usa `VersionActualCambiada` y `CuentaDesignadaAprobada` | usar los de 03 | 04 §5 sagas, §6 | H2/H6 |
| 7 | Evento público `expediente.recordatorio_cargue` aparece en 05 §8 pero no en el catálogo §7 | añadirlo a §7 (origen scheduled `RecordatorioCargue`) | 05 §7 | H4 |
| 8 | 06 cita `KpisRevisionAutomatica` y `KpisEquidadIA` (04 §11-bis); 03 solo define `KpisTenant/Flujo/Empresa` | incluirlos como campos de `KpisTenant` (`decisionesAutomaticas`, `equidad`) y retirar los nombres sueltos | 03 §14, 04 §11-bis, 06 §5 | H7 |
| 9 | Proyecciones usadas por 06 sin entrada en 03 §14: `MisPendientes`, `Tercero360`, `CuentasTercero`, `FlujosResumen`, `VencimientosProximos` (equivale a `VencimientoProgramado` filtrada) | añadirlas a 03 §14 | 03 | H3–H6 |
| 10 | No hay endpoint para que la **empresa** consulte las solicitudes de supresión ni para resolverlas (saga `SupresionDatos` necesita decisión humana en retenciones dudosas) | `GET /solicitudes-supresion`, `POST /solicitudes-supresion/{id}/resolver` | 05 §2.7, 07 | H8 |
| 11 | No está definido el DTO de la **evidencia de aceptación** (T&C/autorización) que devuelve `AceptacionConEvidencia` | `AceptacionDto { documentoVersionId, textoVersion, ip, userAgent, fecha, otpVerificado, trazo? }` | 05 §4, 07 | H4 |
| 12 | `ConsultaCertificacionDto.fuenteRespaldo` lo "rellena el integrador" (05) pero Marco necesita saber qué campos trae la tabla local | esquema de `validasoft_certificacion_local` en 08 | 08 §Marco | H9 |
| 13 | Falta definir la **plantilla del certificado** (campos del PDF/A, posición del QR, bloque bilingüe) y el formato del `HashContenido` (canonicalización JSON) | `PlantillaCertificado` en `FlujoDefinicionDto` + anexo "Contenido canónico del certificado" | 03 §7, 04 | H5 |
| 14 | No hay endpoint de **webhooks por empresa** para integraciones que solo deben ver una empresa pero la API key tiene `*` | filtro `empresas[]` ya existe en `WebhookDto`; documentar precedencia API key ∩ webhook | 05 §2.8, 07 | H5 |
| 15 | 03 §4.3 nombra `AnalisisDeVersionIniciado` como evento, 04 no lo menciona en la saga | añadirlo al paso 1 de `VerificacionDocumental` | 04 §5 | H2 |

</details>

## 6. Verificación de completitud

**Método** (se repite en cada PR que toque un contrato; en CI es el job `trazabilidad` de `ci.yml`):
1. Extraer de `05 §2` todas las rutas (`MÉTODO /ruta`) y comprobar con `grep` que cada una aparece en una tabla de hito de este documento o en §3(a).
2. Extraer de `06 §3` todas las rutas de pantalla (`/t/...`, `/e/:token`, `/boveda`, …) y comprobar que aparecen en §3(d) o en el "Catálogo de pantallas por hito" de 06 §5-bis.
3. Extraer de `05 §7` los tipos de evento público y comprobar que aparecen en §3(b).
4. Extraer de `03 §14` las proyecciones y comprobar que cada una tiene una pantalla o endpoint consumidor en 06 o 05.
5. Cualquier DTO citado en 05 §2 debe existir en 05 §4; este documento **no copia** tablas de DTOs: cita 05 §4 y solo lista campos cuando el hito los introduce o los cambia (I-30).
6. Extraer de `03 §3` los enums y de `03 §16-bis` los eventos de dominio y comprobar que cada nombre usado en §2.x.1 existe allí con la misma ortografía (sin `RequiereRevalidacion`, `ComparticionRevocada`, `VersionRechazadaParaVinculo`, `RevalidacionVencida`).
7. Comprobar que cada decisión B-1…B-8 y T-1…T-18 del registro aparece al menos una vez en el hito que la implementa (grep por su código).

**Resultado del 2026-10-28 (manual, tras el cierre de huecos y la segunda relectura):**

| Comprobación | Resultado |
|---|---|
| Rutas de 05 §2 → 11 | 100 % presentes (las añadidas en el cierre están en los bloques "Cierre de huecos" y en §3(a)) |
| Pantallas de 06 §3 → 11 §3(d) / 06 §5-bis | 100 % (adiciones H1/H3/H4 registradas en 06 §5-bis) |
| Eventos públicos 05 §7 → 11 §3(b) | 100 % (`expediente.recordatorio_cargue`, `expediente.flujo_*`, `flujo.aplicado_a_abiertos`, `configuracion.cambiada`, `identidad.evento`, `invitaciones.lote_terminado` añadidos) |
| Proyecciones 03 §14 → consumidor | 100 % (`MisPendientes`, `Tercero360`, `CuentasTercero`, `FlujosResumen`, `VencimientosProximos` con endpoint y pantalla) |
| DTOs citados en 05 §2 → 05 §4 | 100 % (33 + 11 nuevos: `AceptacionDto`, `CertificadoContenidoDto`, `RegistroAuditoriaDto`, `DiffCampoDto`, `ExportarAuditoriaDto`, `ReglaSeleccionFlujoDto`, `CambiarFlujoDto`, `AplicarVersionDto`, `ImpactoAplicacionDto`, `BinarioImportadoDto`, `RechazoImportacionDto`) |
| Nombres obsoletos (`VersionPromovidaAActual`, `CuentaDesignadaCambiada`, `KpisRevisionAutomatica`, `KpisEquidadIA`, `/catalogos/identificacion`, `GET /boveda`, `/terceros/{id}/cuentas`) | 0 ocurrencias en 03–08 |
| Nombres retirados por la segunda relectura (`RequiereRevalidacion`, `ComparticionRevocada`, `VersionRechazadaParaVinculo`, `RevalidacionVencida`, `expediente.certificado`, `EventoAuditoriaDto`, `Certificados:SelloX509`, `fuera-de-empresa`/`fuera-de-tenant`, `presupuesto-ia-agotado`, `iaHechos`, `degradarAManualAlAgotar`, `DocumentoHistorial`, códigos `CERT_BANCARIA`/`INFO_BANCARIA`/`TRATAMIENTO_DATOS`/`INFO_GENERAL`/`CEDULA_REPRESENTANTE`/`POLIZA_RC`/`INFO_FINANCIERA`, "corte por tenant") | 0 ocurrencias vivas en 00–11 y MAPA (quedan solo menciones históricas marcadas como retiradas en 03 Anexo A, 05 y registro/relectura) |
| Decisiones B-1…B-8 y T-1…T-18 → hito | 100 % citadas en el hito que las implementa (B-1/ADR-0009 H4; B-2 H2/H7; B-3/ADR-0010 H2; B-4 H3; B-5 H4/H6; B-6 H1; B-7 H4; B-8 H7/H9/H10; T-1…T-4 H1; T-5 §1; T-6 H5; T-7/T-8 H6; T-9 H4/H5; T-10/T-11 H2; T-12/T-14/T-15 H5; T-13 H4; T-16/T-18 H9; T-17 H2) |

## Decisiones abiertas

1. **Alcance exacto de la regla de CI de trazabilidad**: ¿falla por endpoint ausente en este documento o solo advierte durante H1–H3? *Propuesta:* advertencia en H1–H3, fallo desde H4. *Quién decide:* Arquitectura.
2. ~~Formato canónico del contenido del certificado~~ **Cerrada**: RFC 8785 + sello Ed25519 (03 §7.1).
3. ~~`POST /documentos` para integraciones~~ **Cerrada**: tercero (`/public/v1/boveda/documentos`) e importador en H2; integraciones en H9 con `documentos:escribir`.

## Fuentes verificadas

Ver [03-dominio-y-eventos.md](03-dominio-y-eventos.md) (agregados, eventos, proyecciones, sagas), [05-api-contratos-y-webhooks.md](05-api-contratos-y-webhooks.md) (endpoints, DTOs, eventos públicos, alertas), [06-frontend-y-ux.md](06-frontend-y-ux.md) (pantallas, componentes, contratos de lectura), [04-arquitectura-plataforma.md](04-arquitectura-plataforma.md) (sagas, jobs, componentes por hito) y [09-plan-de-hitos-y-responsabilidades.md](09-plan-de-hitos-y-responsabilidades.md) (alcance por hito).
