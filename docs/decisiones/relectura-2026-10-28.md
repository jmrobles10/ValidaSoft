# Relectura cruzada — 2026-10-28

| Estado | Fecha | Responsable |
|---|---|---|
| Vigente | 2026-10-28 | Arquitectura |

**Propósito.** Registrar la reconciliación final del conjunto `docs/` después de que tres propagaciones en paralelo (00/01/02/10, 03/05/07, 04/06/08) aplicaran las decisiones del [registro](registro.md). Fuente de verdad de nombres: [03](../03-dominio-y-eventos.md) (enums, eventos de dominio, proyecciones) y [05](../05-api-contratos-y-webhooks.md) (endpoints, DTOs, eventos públicos, alertas).

## 1. Hallazgos y correcciones aplicadas

| Documento | Dónde | Problema | Corrección |
|---|---|---|---|
| 03 | §3.1 enum `Comparacion` y `HechosRegistrados.ComparacionPorHash` | El enum no tenía el valor `Distinta` que usan 02 y 10 para la comparación por hash sin IA; el texto decía "distinto" en minúscula | Añadido `Distinta` al enum (con nota "solo por hash, sin IA") y corregido el campo |
| 03 | §14 `CertificadoPublico` | Endpoint sin prefijo `/verificar/{folio}` | `GET /public/v1/verificar/{folio}` + aclaración de la ruta UI `/verificar/:folio` |
| 02 | §4.1, §4.3 | Citaba D-1 y D-2 como abiertas | Referenciadas como cerradas por P-2-1 y P-2-3 |
| 02 | §5 diagrama de estados | Transiciones no coincidían con 03 (`VersionCargada` llevaba a `EnAnalisis`; faltaban `AnalisisDeVersionIniciado`, `HechosDeVersionRegistrados`, `PromoverAActual`, `VersionDevueltaAlTercero`) | Diagrama alineado con 03 §4.2–4.3 |
| 02 | §5 regla 4 | Hablaba de `estado_ia = Pendiente` (enum inexistente en 03) y dejaba D-3 abierta | Nueva regla 4 con la condición de promoción de 03 (sin IA / con IA) y regla 5 con `con_ia` + `pendiente_revision_humana` |
| 02 | §5.1 | Hecho `ComparacionVersionAnterior` con valores incompletos | Renombrado a `Comparacion`; valores `SinAnterior · Identica · Distinta` (hash) y `SinCambios · CambiosDetectados` (campos) |
| 02 | §9 tabla de decisión automática | "Identidad no coincide ⇒ Rechazado" contradecía a 03 (con IA la versión se devuelve en la bóveda); faltaban umbrales 0,95/0,80, señales de manipulación y `OrigenVeredicto` | Tabla alineada con 03 §11; actor `Sistema` expresado como `OrigenVeredicto` |
| 02 | §10.1 `version_documento`, `verificacion_version` | `estado_ia` y `comparacion_anterior` con valores obsoletos; `requiere_ia` en vez de `con_ia` | Columnas `con_ia`, `pendiente_revision_humana`, `legibilidad`, `identidad_coincide`, `comparacion` (5 valores), `campos_criticos_cambiados`, `senales_manipulacion`, `requiere_revalidacion`; `verificacion_version.con_ia` |
| 02 | §10.1 `version_aceptada` | Sin `EstadoAceptacion` ni origen del veredicto | Añadidos `estado_aceptacion (AlDia|Desactualizada|PorVencer|Vencida)` y `origen_veredicto` |
| 02 | §10.1 `cuenta_designada` | Estados `PendienteAprobacion|Aprobada|Rechazada` | `Propuesta|Aprobada|Rechazada|Reemplazada` (= `EstadoCuentaDesignada` de 03) + saga `CambioCuentaDesignada` y permiso `cuentas:aprobar` |
| 02 | §10.3 | Citaba D-6 como propuesta | Valores fijados por P-14-1 (10 años) y gracia 90 días configurable |
| 02 | E3, E5 | E3 dejaba D-3 abierta; E5 usaba la clave S3 vieja `tenant-A/vinculos/…` | E3 con `Legibilidad = Parcial`; E5 con `tenant-{id}/empresas/{empresaId}/vinculos/{vinculoId}/{sha256}` y KMS del tenant |
| 02 | §12 | `GET /consultas/certificacion?empresaId=` contradice a 05 (header `X-Empresa`, consulta por país/tipo/número) | Alineado al contrato de 05 |
| 02 | §13 invariantes | Numeración desordenada (11 y 12 en medio) y referencia a `estado_ia` | Reordenadas; añadida la invariante 13 (cambio menor no desactualiza) |
| 02 | Decisiones abiertas | D-3 abierta | Cerrada con la regla de 03 §4.3 |
| 04 | §Sagas `VerificacionDocumental`, diagrama §7 | Evento obsoleto `VersionPromovidaAActual` | Comando `PromoverAActual` → evento `VersionActualCambiada` |
| 04 | §Testing k6 | `/verificar/{folio}` sin prefijo | `/public/v1/verificar/{folio}` |
| 04 | §Riesgos | Keycloak citaba ADR-0002 | ADR-0003 |
| 06 | árbol `apps/public`, `PanelIA` | Ruta sin distinguir UI/API; tipo `ComparacionConVersionAnterior` | Comentario UI → API; tipo `Comparacion` |
| 09 | H1, H4, H5, H6, H7 | Faltaban `IDeEmpresa`, filtro global por `empresa_id`, permisos `empresas:administrar`, `requisitos:eximir`, `vinculos:bloquear`, `cuentas:aprobar`, rol `Cumplimiento`; `/verificar/{folio}` sin prefijo | Añadidos |
| 10 | §5.1 enums | Faltaban `OrigenVeredicto`, `EstadoEtapa`, `EstadoAceptacion`, `ClaseDocumento`, `AmbitoDocumento`, `NivelRevision`; `EstadoVerificacion` sin `CoincidenciaPorAnalizar`; enum inexistente `EstadoVerificacionIA`; `ComparacionConVersionAnterior` y `EstadoCuentaDesignada` con valores viejos; `/verificar/{folio}` | Tabla reescrita con los enums de 03 §3.1; definición de "Comparación con versión anterior" con `SinAnterior`; decisión abierta 3 cerrada |
| 00 | Decisiones abiertas | D-3 seguía abierta | Marcada cerrada |
| ADR-0004 | Decisión | No reflejaba la clave KMS por tenant (P-14-3) ni la precisión de ADR-0008 | Añadidos ambos puntos y la clave S3 por empresa |
| ADR-0006 | Contexto | `/verificar/{folio}` como si fuera el API | Ruta UI sobre `GET /public/v1/verificar/{folio}` |

Verificaciones sin hallazgos: ningún documento menciona ya `Proponer`/`Ignorar` como política, `propuesta_version`, `efectoEnTenant`, `CertificadoMarcado`, `ConRevalidacionPendiente`, `ConDocumentoVencido`, `GrupoEmpresarial` como entidad vigente ni Azure DevOps/Pipelines como herramienta (solo como alternativa descartada en el registro); todos los documentos `00`–`10` tienen encabezado estándar, `## Decisiones abiertas` y `## Fuentes verificadas`; todos los enlaces relativos apuntan a archivos existentes salvo `11-trazabilidad-por-hito.md`, en redacción.

## 2. Decisiones abiertas que quedan en el conjunto

| Id | Documento | Decisión | Propuesta | Quién decide |
|---|---|---|---|---|
| A-1 | 00 | Nombre definitivo del producto | Mantener "Validasoft" hasta registro de marca | Producto |
| ~~A-2 (= D-8)~~ | 00, 02 | Plazo de revalidación por defecto y si bloquea | **Cerrada (Producto, 2026-10-28)**: 15 días de aviso y luego el certificado `ConPendientes` pasa a bloqueante hasta revalidar; `desactualizadaBloquea = true` por defecto | — |
| A-3 | 00, 09 | Tenant piloto y fecha de corte | Constructora con menos proveedores activos y contrato de listas vigente; corte al cerrar H10 | Producto y Operaciones |
| A-4 | 01, 08 | Dónde vive el aplicador de cambios en AyF (SRM vs Marco) | SRM en H9; Marco cuando exponga API de terceros | Arquitectura + equipo Marco |
| A-5 | 01 | Proveedores de listas restrictivas a mantener | Solo los activos; el resto como adaptadores opcionales | Producto con Operaciones |
| A-6 | 01 | Retiro de las páginas HTML legacy del SRM | Reemplazarlas por enlaces a Validasoft con token exchange al activar el flag | Arquitectura + Producto |
| ~~A-7~~ | 03 | Ratificación con aprobación humana | **Cerrada (Producto, 2026-10-28)**: nunca pasa por área; "si no cambia ni la información ni el documento, con la declaración basta" | — |
| ~~A-8~~ | 02, 03, 07 | Exclusión de empresas por el tercero | **Cerrada (Producto, 2026-10-28)**: el tercero **no puede excluir** a ninguna empresa, de ningún tipo de documento | — |
| A-9 | 04 | Modelo de IA por defecto por tipo (Opus vs Sonnet) | Opus para Cámara/EEFF/pólizas; Sonnet para cédula/RUT; decidir tras H2 con fixtures | Producto + Arquitectura |
| A-10 | 04 | Observabilidad on-prem (LGTM vs Seq) | LGTM en compose; Seq documentado | DevOps |
| A-11 | 04 | Daemon async de Marten en alta disponibilidad | Un líder hasta H11 | Arquitectura |
| A-12 | 04 | Proveedor de KMS on-premise | MinIO KES + Vault en `prod`; clave local en portátil | DevOps/Seguridad |
| A-13 | 04 | Fallback de conversión HEIC/fotos→PDF en worker | Sí, mismo límite de 20 MB | Frontend B + Backend B |
| A-14 | 05 | Alias en inglés de recursos | Español en v1 | Producto |
| ~~A-15~~ | 05 | `X-Empresa` como header vs segmento de ruta | **Cerrada (T-5)**: cabecera en la API; segmento solo en la SPA | — |
| A-16 | 06 | Web Component vs iframe para el embed | iframe con Web Component envolvente opcional | Arquitectura + equipo Portal |
| A-17 | 06 | Storybook desde H1 o H4 | Desde H1 con los 5 componentes base | Frontend A |
| A-18 | 06 | Aprobación en lote de revalidaciones `SinCambios` en nivel Manual | Sí, con confirmación y registro individual | Producto |
| A-19 | 06 | Modo claro por defecto por tenant | Configurable; oscuro por defecto | Producto |
| A-20 | 06, 10 | Nombre público de "empresa interna" y de "tercero" en la UI | "empresa" (NIT + grupo en tooltip); "Mi perfil" para el tercero | Producto/UX |
| A-21 | 07 | Passkeys/WebAuthn | H8 | Producto |
| A-22 | 07 | Mecanismo de validación del token del ERP | Introspección por tenant; si no existe, firma compartida rotable | Arquitectura + equipo ERP |
| A-23 | 08 | Folio de certificados retroactivos en migración | Emitir con sufijo `-M` y fecha original | Producto |
| A-24 | 08 | Autorregistro del Portal en fase P1 | Crear el tercero en Validasoft desde P1 | Equipo Portal |
| A-25 | 08 | Agrupamiento de constructoras en tenants para la migración | Lista de Comercial confirmada por Producto antes del primer dry-run | Producto |
| A-26 | 09 | Fecha de inicio de H1 y equipo | 2026-11-03, 3 personas | Producto |
| A-27 | 09 | Vender la revisión automática desde el MVP o tras medir | Medir en H5–H6, vender en H8 | Producto/Comercial |
| A-28 | 10 | "Observado" vs "Devuelto" en la UI | "Observado" para el veredicto; "Devuelta" solo para la versión en la bóveda; "Rechazado" terminal | Producto |

Ninguna es bloqueante para H1. A-2, A-7 y A-8 quedaron cerradas por Producto el mismo día (ver `registro.md` §5-bis); quedan 25 abiertas, ninguna bloqueante para H2.

## 3. Efectos cruzados detectados

| Cambio origen | Obligó a cambiar |
|---|---|
| P-11-2 (tenant = grupo con empresas internas aisladas, ADR-0008) | `empresa_id` en todo `validacion` (02, 03, 05, 07), header `X-Empresa` (05, 06), clave S3 `empresas/{empresaId}/…` (02, 04, 07, ADR-0004), selector de empresa y rutas `/t/{tenant}/e/{empresa}` (06), importador "una empresa por constructora" (08), roles por empresa (07), hitos H1/H4 (09) |
| P-1-1 "multipaís" | `catalogo_tipo_identificacion` (02, 03), `IdentificacionDto` y `/catalogos/tipos-identificacion` (05), `SelectorIdentificacion` (06), normalización en importador (08), P-1-4 elevada a catálogo por país (registro) |
| P-1-7 "la IA puede no estar contratada" | Hechos sin IA vs con IA (02 §5.1, 03 `HechosRegistrados.ConIa`, 04 pipeline), condición de promoción D-3 (02, 03), `PanelIA` tolerante a IA ausente (06), `Comparacion.Distinta` (03, 10) |
| P-3-3 "el tercero elige la cuenta" | `CuentaBancaria` global + `CuentaDesignada` propuesta por el tercero (02, 03), endpoints públicos de cuentas (05), pantalla "Mis cuentas" (06), saga `CambioCuentaDesignada` y permiso `cuentas:aprobar` (04, 09), AyF recibe la designada (08) |
| P-3-1/P-3-6 (formularios como documentos; versión siempre, revalida solo críticos) | `RequiereRevalidacion` en `VersionActualCambiada` (03), invariante 13 y regla de cambio menor (02), `@rjsf/mui` y `DiffCampos` (06), eventos `vinculo.cambio_menor` (05) |
| P-17-2 GitHub | §CI de 04 reescrita (Actions, GHCR, CODEOWNERS), gobierno de 09, mención en 00 |
| Objeción "versión única" (ADR-0005) | Desaparecen `propuesta_version` y políticas `Proponer/Ignorar` (02, 03, 05, 06); aparecen `EstadoAceptacion.Desactualizada`, `BandejaRevalidaciones`, saga `Revalidacion` y `certificado.con_pendientes` |
| Vigencia en dos capas | `motivo_vigencia` y `estado_aceptacion` en `version_aceptada` (02), `VencimientoProgramado` con tipos de evento nuevos (03), `FrescuraDocumentoTercero` y avisos consolidados (02, 03, 05, 06) |
| Enum `Comparacion` con `Distinta` (hallazgo de esta relectura) | 02 §5.1 y §9, 10 §3 y §5.1, 06 `PanelIA` |

## 4. Contradicciones resueltas (decisión de Producto, 2026-10-28)

1. **A-8 — exclusión de empresas.** Resuelta: **sin exclusión** para ningún tipo. Se eliminaron `Exclusiones`, `ExcluirEmpresa/IncluirEmpresa`, el endpoint `/public/v1/boveda/documentos/{id}/exclusiones` y el evento `documento.comparticion_cambiada`; `ComparticionRevocada` solo por desvinculación o bloqueo. Propagado a 00, 02, 03, 05, 07, 09, 10, ADR-0005, 11.
2. **A-7 — ratificación.** Resuelta: nunca pasa por área; `RequisitoRatificado` directo. No existe "ratificación con aprobación" ni expediente de renovación pre-cargado para ratificar. Propagado a 02, 03, 09, 10, ADR-0005.
3. **A-2 — plazo de revalidación.** Resuelta: `diasRevalidacion = 15`, `PoliticaDesactualizada = BloquearTrasGracia` por defecto; el certificado pasa a `ConPendientes` bloqueante (`bloqueante: true`, consulta `bloqueado: true`), el vínculo sigue `Activo`. Propagado a 02, 03, 04, 05, 06, 09, 10, ADR-0005, 11.

## 5. Segunda relectura — auditorías externas (2026-10-28, noche)

Dos auditores sin contexto previo revisaron el conjunto: uno de **negocio** (30 incongruencias, 7 altas, recorrido de 10 escenarios) y otro **técnico** (62 hallazgos: 5 altos, 28 medios, 29 bajos). Producto cerró las decisiones B-1…B-8 y Arquitectura las T-1…T-18 (registro §5-quater y §5-quinquies); después se corrigieron 03/02/10/ADR, 05/07/08 y 04/06/09/00/01/11/MAPA/README.

### 5.1 Hallazgos de negocio cerrados

| Id | Hallazgo | Cierre |
|---|---|---|
| I-1 | Compartición antes del consentimiento (cosecha por NIT) | B-1 / ADR-0009: compartir solo al aceptar la invitación; `requisitosCubribles` sin nombres (02 §6.1, 03 §4.3, 05, 07 §10) |
| I-2 | Tres modelos de quién paga la lectura IA | B-2: lectura = coste de plataforma; presupuesto solo decisiones; sin `iaHechos` (02 §9, 03 §11, 04 §7, 05, 07 §9) |
| I-3 | La cédula del representante no cabía en `(tercero, tipo)` | B-3 / ADR-0010: personas relacionadas con bóveda propia y `RelacionEntreTerceros` (02 §4.5, 03 §4.5, 05, 06, 08, Anexo A) |
| I-4 | Configuración prometida "por empresa" solo existía por tenant | B-4: jerarquía tenant → empresa → regla, nombres únicos, `tipo_documento_empresa` (02 §4.3-bis, 03 §10, 05, 06) |
| I-5 | 08 y 11 H9 no hablaban el idioma de 05 | 08 §3–§5 reescritos con 05 §7 y `ConsultaCertificacionDto`; 11 H9 regenerado |
| I-6, I-7 | Migración: bloqueo masivo a los 15 días e IA sin consentimiento | B-8: 90 días para importadas; IA solo tras consentimiento; H9 depende de H7 |
| I-8 | Gracia 0 vs 15 | 02 §4.3 y E4 con 15 (P-7-2) |
| I-9 | 00 dejaba abierta A-2 | Cerrada en 00 |
| I-10 | Rechazo automático de identidad imposible | 02 E6 y 04 §7.5 sin rechazo automático; la bóveda devuelve la versión |
| I-11 | Archivo erróneo sin IA y retiro de la actual | `VersionActualRestituida` al retirar; riesgo reescrito en 00 §7 |
| I-12 | Selección automática de flujo sin camino por la API | `flujo` opcional en `CrearExpedienteDto` + `origenFlujo` |
| I-13 | Bloqueo sin camino de vuelta | B-5: `Suspendido`/`CertificadoRestablecido`, `CongelarComparticion`/`Reanudar` |
| I-14 | Ratificación circular y automática | Condición única (canónico + tenant + sin vencimiento propio pasado), reinicia frescura, nunca automática (A-7) |
| I-15 | Disparador de `Renovacion` y vínculo `Vencido` que no vuelve | `EstadoDeVinculoRecalculado` al cerrar cualquier expediente; tabla de casos expediente abierto × evento (03 §5) |
| I-16 | Acceso de soporte contradictorio | B-6: `plataforma:soporte` con concesión temporal del tenant |
| I-17 | `desactualizadaBloquea` vs `PoliticaDesactualizada` en tres sitios | Un nombre, una ubicación (tipo tenant/empresa/regla) |
| I-18 | Corte por tenant vs por empresa | T-18: por empresa en 00/09/MAPA; tercero compartido en 08 §7 |
| I-19 | "Rechazado" con dos sentidos | B-7: `Veredicto { Aprobado, Observado }` + `noAdmitirEstaVersion`; `Rechazado` solo del expediente |
| I-20 | UBO/PEP duplicados en SARLAFT | SARLAFT PorVinculo referencia los formularios globales (03 §4.5) |
| I-21 | Conflicto de intereses sin identificación del usuario | `UsuarioDto.identificacion?` + chequeo en `EmitirVeredictoDeRequisito` |
| I-22 | 422 informativo | 2xx con `advertencias[]` |
| I-23 | `Continuar` editable por el tenant | `accionAlAgotar` en `PlanDto` (Plataforma) |
| I-24, #53 | Gantt incoherente | H0 1 semana; H1 2026-11-03 |
| I-25…I-30 | Folio, códigos canónicos, rutas, ISO 3166-1, DTOs copiados en 11 | `PrefijoEmpresa`, Anexo A, ruta de confirmación única, 3166-1 alpha-2, 11 referencia a 05 §4 |

### 5.2 Hallazgos técnicos cerrados (resumen por número del informe)

#1 tres esquemas + `identidad` (T-1) · #2 `RevalidacionVencida` eliminado (T-8) · #3 nombres de 05 en 08 · #4 dedupe por `Validasoft-Event-Id` (T-6) · #5 `RequiereRevalidacion` → hechos + saga por empresa (T-7) · #6 gracia 15 · #7 `politicaDesactualizada` único · #8 folios `-E-`/`-C-` (T-9) · #9 códigos del Anexo A (T-10) · #10 `Antivirus` con cuatro valores (T-11) · #11 `ExcepcionDto.tipo` PascalCase + `CoherenciaSoporte` · #12 `EstadoCuentaDesignada` en 06 · #13 `RevalidacionDto` único · #14 `RevisionAutomaticaConfigDto` consolidado · #15 `EventoAuditoriaDto` retirado · #16 `LoteImportacionDto`/`ImportacionLoteDto` y `/importacion/binarios` · #17 404 para otro tenant/empresa (T-4) · #18 `CertificadoPublicoDto` completo · #19 proyecciones faltantes en 03 §14 (`MisPendientes` = consulta compuesta) · #20 eventos CRUD/sagas en 03 §16-bis · #21 17 plantillas en 05 §8 · #22 consulta de certificación alineada · #23 ruta de confirmación · #24 autorregistro en H4 (T-13) · #25 cuentas en H6 · #26 Ed25519 (T-12) · #27 corte por empresa (T-18) · #28 p99 < 50 ms en H11 · #29 reglas 2 y 4 del motor · #30 soporte (B-6) · #31 `NovedadDto.cuerpo` · #32 `ApiKeyDto` único · #33 saga incluye `EnValidacion` · #34–#62 nomenclatura, DDL, permisos, SDK multi-target (T-16), WebP (T-17), P-11-3/P-13-4 por empresa (T-18), plantilla ADR y excepciones de convención.

Observaciones de diseño atendidas: store único conjoined con tenants técnicos (T-2, spike en H1); `ISesionDeEmpresa` (T-3); `certificado.emitido` canónico (T-15); Object Lock compliance (T-14); `MisPendientes` como consulta compuesta; tabla de casos "expediente abierto × evento" (03 §5); `X-Empresa` como cabecera (T-5).

### 5.3 Decisiones abiertas tras la segunda relectura

Quedan las A-1, A-3 (ahora "empresa piloto", única en 09), A-4, A-5, A-6, A-9…A-14, A-16…A-28 y la decisión 1 de 11 (alcance de la regla de CI). A-15 (`X-Empresa`) quedó cerrada por T-5. Ninguna es bloqueante para H1.

### 5.4 Contradicciones no resueltas

Ninguna estructural. Pendientes de verificación en H1 (no de decisión): el *spike* de Marten T-2 (un solo store conjoined con tenants técnicos) y la implementación de `ISesionDeEmpresa` T-3.

## Fuentes verificadas

Ver [01](../01-analisis-legado-srm-portal.md) para el legado; esta relectura no introduce afirmaciones nuevas sobre el SRM o el Portal.
