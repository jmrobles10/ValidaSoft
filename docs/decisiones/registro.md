# Registro de decisiones

| Estado | Fecha | Responsable |
|---|---|---|
| Vigente | 2026-10-28 | Producto (usuario) · Arquitectura |

**Propósito.** Registro único de las respuestas al [cuestionario](cuestionario.md). Las 22 preguntas bloqueantes se respondieron una a una; las 75 restantes quedaron **aceptadas por recomendación** del experto (modo elegido por Producto el 2026-10-28) y pueden objetarse escribiendo aquí una nueva fila con fecha. Toda decisión se propaga a los documentos `00`–`11` y, si es estructural, a un ADR.

Leyenda de origen: **E** = respondida explícitamente por Producto · **R** = aceptada por recomendación.

## 1. Decisiones de marco previas al cuestionario (2026-10-28)

| Id | Decisión | Origen |
|---|---|---|
| M-1 | Stack .NET 10 + PostgreSQL 17 + Marten + Wolverine + MinIO + Docker | E |
| M-2 | Validasoft es dueña de la UI del tercero (magic link), de la UI del revisor y de la API; Portal/SRM/Marco integran por API + webhooks | E |
| M-3 | Multi-tenant SaaS | E |
| M-4 | IA (Claude) desde el MVP, revisable por humano, fail-closed | E |
| M-5 | Frontend React 18 + TS + Vite + MUI; dos apps + paquetes | E |
| M-6 | Identidad con OpenIddict + ASP.NET Identity | Arquitectura |
| M-7 | Compartición automática de documentos globales con todas las empresas que exigen el tipo; ~~el tercero puede excluir~~ → **sin exclusión** (A-8, misma fecha) | E |
| M-8 | **Una sola versión vigente** por documento global; versión nueva válida ⇒ alerta a todas y revalidación obligatoria; decisiones privadas | E (objeción del usuario) |
| M-9 | Vigencia en dos capas; avisos consolidados; revisión automática por niveles Manual/Asistido/Automático | E |
| M-10 | Sin gRPC en el MVP (REST + webhooks + SSE) | Arquitectura, confirmado |
| M-11 | Repo nuevo docs-first; nombre de trabajo "Validasoft" (renombrable) | E |

## 2. Preguntas bloqueantes (B)

| Id | Pregunta | Decisión | Observaciones de Producto | Origen |
|---|---|---|---|---|
| P-1-1 | Clave única del tercero | `(país, tipo de identificación, número normalizado sin DV)`; DV como atributo validado | **"Tiene que ser multipaís también"** ⇒ catálogo de tipos de identificación por país desde el MVP (eleva P-1-4 a la opción (a)) | E |
| P-1-7 | Quién edita datos maestros | El tercero propone; si la validación automática está disponible, la IA contrasta contra el documento (RUT) y se publica; en todo caso las empresas revalidan según su nivel | **"La IA puede estar o no contratada para la autovalidación"** ⇒ sin IA, el cambio se publica como versión pendiente de revalidación manual en cada empresa; nunca se bloquea al tercero | E |
| P-1-8 | Datos globales vs privados | Tabla propuesta: globales identificación, nombre, tipo de persona, país, dirección principal, contactos del tercero, representantes; privados por vínculo categorías, etiquetas, especialidades, código ERP, contactos asignados, atributos personalizados | — | E |
| P-2-1 | Administración del catálogo canónico | La plataforma administra; el tenant mapea o propone; tipos sin canónico son `PorVinculo` | — | E |
| P-2-3 | Semilla de tipos canónicos | Lista colombiana con defaults (tabla de `cuestionario.md` P-2-3) | — | E |
| P-2-4 | Cambio de ámbito por tenant | Restringir Global → PorVinculo sí; ampliar no | — | E |
| P-3-1 | Formularios | Clase `Formulario` con esquema JSON versionado, misma mecánica que archivos | — | E |
| P-3-2 | Esquema de formularios | Esquema base canónico de plataforma + campos privados por tenant | — | E |
| P-3-3 | Cuenta bancaria | N cuentas globales del tercero (cada una con su certificación validada una vez); **el tercero elige qué cuenta vincula a cada empresa** (`CuentaDesignada`, dato privado del vínculo); el cambio de cuenta designada requiere aprobación de esa empresa (antifraude) y viaja a su ERP | **"El tercero es el que elige qué cuenta vincula a la empresa"** | E |
| P-3-6 | Versionado de formularios | Toda edición crea versión; solo cambios en campos críticos disparan revalidación; el resto se notifica como cambio menor | — | E |
| P-6-2 | Campos críticos por defecto | Columna "críticos" de la semilla; ajustable por tenant hacia más estricto | — | E |
| P-6-5 | Consentimiento para IA externa | Sí: la autorización de tratamiento a la plataforma incluye finalidad, encargado y transferencia internacional; Legal redacta; opción de "zero data retention" del proveedor | — | E |
| P-11-1 | Planes y límites | Medir todo desde H1 (usuarios, vínculos, GB, lecturas y decisiones IA, webhooks); planes y límites en H8 | — | E |
| P-11-2 | Grupos empresariales | **Un tenant por grupo con empresas internas**: el tenant (grupo) comparte usuarios, catálogo, configuración y facturación; cada **empresa interna** (razón social) tiene sus vínculos; las **decisiones están aisladas por empresa interna** (no se ven ni se heredan dentro del grupo) | Elegido sobre la recomendación (grupo de tenants). Consecuencia estructural: `empresa_id` en `vinculo`, `version_aceptada`, `decision_version`, `certificado`; roles de usuario por empresa interna o por todo el tenant; API keys por tenant con restricción opcional por empresa. Requiere ADR-0008. | E |
| P-14-1 | Retención | 10 años comerciales desde el fin del vínculo; identidad mientras exista vínculo + 10; evidencias SAGRILAFT 10; configurable hacia arriba; Legal valida | — | E |
| P-14-2 | Residencia de datos | Región como atributo del tenant; MVP en una región + on-premise | — | E |
| P-14-3 | Cifrado | Clave KMS por tenant (SSE-KMS + Data Protection); bóveda global con clave de plataforma | — | E |
| P-15-2 | Panel de plataforma | Sección `/plataforma` en `apps/web` con rol `Plataforma` | — | E |
| P-16-2 | Límites de archivo | 20 MB, PDF/JPG/PNG/HEIC (convertido en cliente), 50 páginas; XLSX para EEFF opcional en H7 | — | E |
| P-17-1 | Producto y aprobaciones | Una persona de Producto (el usuario) aprueba alcance, modelo documental y plan; Arquitectura aprueba lo técnico | — | E |
| P-17-2 | Seguimiento y ramas | **GitHub**: repositorio, Issues/Projects (épica por hito, issue por entregable), ramas `feature/H<n>-<tema>` → `main` por PR con dos aprobaciones, commits convencionales en español, GitHub Actions como CI (reemplaza Azure Pipelines en el documento 04) | Elegido sobre la recomendación (Azure DevOps) | E |
| P-17-3 | Definición de hecho | Criterios de aceptación + pruebas automatizadas verdes + demo grabada + docs/ADRs actualizados + sin bloqueantes del siguiente hito | — | E |

## 3. Preguntas necesarias antes del MVP (N) — aceptadas por recomendación

| Id | Decisión registrada | Origen |
|---|---|---|
| P-1-2 | Cambio de NIT (fusión/escisión): tercero nuevo enlazado `SucesorDe` con traslado opcional de documentos vigentes; cambio de razón social sin cambio de NIT: mismo tercero con histórico | R |
| P-1-4 | Identificación de extranjeros: **catálogo de tipos de identificación por país** desde el MVP (elevado por P-1-1 "multipaís") | E (derivada) |
| P-1-6 | N representantes legales con uno `Principal`; el ERP recibe el principal; listas restrictivas cubren a todos | R |
| P-1-9 | Autorregistro permitido pero invisible hasta que una empresa lo vincule; sin directorio público | R |
| P-2-2 | Catálogo canónico versionado; cada versión de documento guarda con qué versión del tipo se extrajo | R |
| P-2-5 | Un requisito puede declarar `TiposAceptados[]` | R |
| P-2-6 | Plantilla versionada por tipo de tenant; la versión cargada guarda con qué plantilla se generó | R |
| P-3-4 | La IA cruza los campos clave del formulario con los extraídos del soporte; la discrepancia es un hecho compartido | R |
| P-3-5 | T&C y autorización de datos por empresa (`PorVinculo`) con versión, fecha, IP y evidencia + aceptación a la plataforma al crear la bóveda | R |
| P-4-1 | Abren expediente: la empresa (usuario o API), el sistema (renovación/actualización) y el tercero solo por autorregistro | R |
| P-4-2 | Plazo 30 días configurable por flujo, recordatorios 15/7/1, `Caducado` con reapertura en un clic | R |
| P-4-4 | Por defecto una etapa con todas las áreas en paralelo; secuencia configurable | R |
| P-4-5 | SLA incumplido ⇒ escalamiento (responsable del área → administrador); nunca auto-aprobar | R |
| P-4-7 | "Cuatro ojos" configurable por área y tipo; por defecto 2 aprobadores para `CERTIFICACION_BANCARIA` y cambio de cuenta designada | R |
| P-4-9 | Semilla "Habilitación de proveedores": Compras (completitud), Legal, Financiera, Cumplimiento; SST opcional; en migración los perfiles del SRM se convierten en áreas | R |
| P-5-1 | Motivos base de plataforma + motivos propios del tenant | R |
| P-5-2 | Observación por requisito con lista opcional de campos afectados | R |
| P-5-3 | Estado `Bloqueado` del vínculo por empresa con motivo, vigencia opcional y quién lo levanta; privado | R |
| P-5-4 | Eximir solo con permiso `requisitos:eximir`, justificación obligatoria, visible en el certificado | R |
| P-5-5 | El rechazo de expediente es terminal; el administrador abre uno nuevo | R |
| P-6-6 | Prompt y respuesta de IA se conservan lo mismo que la versión del documento | R |
| P-6-9 | Resultado de IA en < 60 s para el 95 %; si tarda, aviso por correo | R |
| P-7-5 | La revalidación por versión nueva recalcula la vigencia de la empresa desde los hechos de la versión aceptada | R |
| P-8-1 | Verificación pública: razón social, NIT enmascarado, empresa emisora, flujo, estado, emisión, vigencia, huella; sin documentos ni contactos | R |
| P-8-2 | Certificado por vínculo con historial de emisiones (folio raíz + sufijo) | R |
| P-8-4 | Sin certificado parcial | R |
| P-10-1 | Catálogo de alertas propuesto (tabla del cuestionario) | R |
| P-10-2 | Canales MVP: correo + campana + webhook; WhatsApp/SMS en H8+ | R |
| P-10-4 | Remitente: dominio de la plataforma con nombre de la empresa y `Reply-To` de la empresa; dominio propio en H8 | R |
| P-12-1 | Identidad del tercero reconciliada por clave única entre ERP y Validasoft; datos validados por documento prevalecen; cada sistema guarda el id externo del otro | R |
| P-12-2 | Webhooks y modo pull desde el MVP | R |
| P-12-4 | Durante la coexistencia la revisión se hace en Validasoft; el SRM recibe estados y aplica en AyF | R |
| P-14-4 | 2FA obligatorio para Administrador, Auditor y revisores de Cumplimiento; política por tenant puede exigirlo a todos | R |
| P-14-5 | Habeas data: flujo formal de supresión en H8, modelo preparado en H2 | R |
| P-14-6 | Contrato marco + anexo técnico de tratamiento de datos aceptado al crear el tenant | R |
| P-15-1 | SLA 99,5 % mensual, RPO 15 min, RTO 4 h en el MVP; 99,9 %/5 min/1 h como plan Empresarial | R |
| P-15-3 | SaaS con dev/test/prod compartidos; on-premise una instancia por cliente | R |
| P-15-4 | Soporte al tercero: la empresa es primer nivel; la plataforma atiende incidentes técnicos | R |
| P-16-1 | Idioma MVP es-CO; en preparado | R |
| P-16-3 | Varias fotos → un PDF en el cliente con recorte y rotación | R |
| P-16-4 | Aceptación de T&C/autorización: clic + OTP + IP/fecha/versión; trazo opcional en H8 | R |
| P-17-4 | Revisión semanal de 1 h con Producto + demo al cierre de cada hito | R |

## 4. Preguntas que pueden esperar (E) — aceptadas por recomendación

| Id | Decisión registrada | Origen |
|---|---|---|
| P-1-3 | Sucursales como atributo del tercero; el vínculo elige una | R |
| P-1-5 | Consorcios/uniones temporales como tercero `Consorcio` con `Miembros[]` | R |
| P-4-3 | Reenvíos sin límite con alerta al administrador al tercero del mismo requisito | R |
| P-4-6 | Delegación temporal + reasignación por administrador | R |
| P-4-8 | Separación de funciones configurable, apagada por defecto | R |
| P-6-1 | Umbrales ≥ 0,95 aprobar; 0,80–0,95 Asistido; < 0,80 humano; defaults por tipo | R |
| P-6-3 | Muestreo 5 %, mínimo 1 por semana por tipo | R |
| P-6-4 | Presupuesto IA agotado ⇒ degradar a Manual con alerta; nunca bloquear la carga | R |
| P-6-7 | Precio por decisión automática/asistida + cargo por vínculo activo | R |
| P-6-8 | Derecho a revisión humana de toda observación automática | R |
| P-7-1 | Avisos 30/15/5; 60/30/7 para pólizas y EEFF | R |
| P-7-2 | Gracia 15 días; 0 para críticos de cumplimiento | R |
| P-7-3 | Certificado sigue vigente "con pendientes" ante vencimiento no crítico | R |
| P-7-4 | Campañas de renovación por tenant y tipo en H8 | R |
| P-8-3 | Sello electrónico de la plataforma + hash; sello de tiempo RFC 3161 opcional en H11; firma acreditada después | R |
| P-8-5 | PDF/A-2b en español con bloque bilingüe opcional | R |
| P-9-1 | Listas restrictivas: los proveedores con contrato vigente de las primeras empresas + evidencia manual como fallback | R |
| P-9-2 | Reconsulta al vincular, en cada renovación y cada 12 meses; masiva mensual opcional | R |
| P-9-3 | Cada empresa consulta con su contrato; no se comparte el resultado | R |
| P-9-4 | Coincidencias con estado `CoincidenciaPorAnalizar` → `Descartada|Confirmada` justificada, vigente 12 meses | R |
| P-9-5 | RUES/DIAN/Registraduría como verificadores en fase posterior | R |
| P-9-6 | Estudio de cumplimiento: replicar legado + matriz de riesgo configurable en H8 | R |
| P-10-3 | Máximo 1 correo por tercero y hecho por día; digest diario opcional para revisores | R |
| P-10-5 | Plantillas: cabecera/pie editables en el MVP; Liquid restringido en H8 | R |
| P-11-3 | Logo y colores por tenant en el MVP; dominio propio en H8 | R |
| P-11-4 | Sandbox por API key de prueba dentro del tenant (H9) | R |
| P-12-3 | SDK oficial .NET y TypeScript | R |
| P-13-1 | Migrar activos con última versión y aceptada por constructora + 3 años de decisiones | R |
| P-13-2 | Documentos huérfanos del Portal → bóveda como actual sin aceptada | R |
| P-13-3 | Usuarios del Portal: se migran correo y nombre; acceso por magic link | R |
| P-13-4 | 90 días de sincronización inversa por tenant | R |
| P-14-7 | Logs de acceso con la misma retención que el documento | R |
| P-16-5 | WCAG 2.1 AA en `public`; AA en `web` con excepciones documentadas | R |

## 5. Adiciones del experto incorporadas al alcance

Se aceptan como alcance (hito indicado) las 10 adiciones de `cuestionario.md`: beneficiario final/UBO (H7), PEP (H7), expiración automática de cédula y listas del representante saliente (H7), evidencia de entrega de notificaciones (H5), exportación de bóveda y expediente en ZIP firmado (H8), detección de documentos manipulados como hecho compartido (H2 básico, H7 completo), conflicto de intereses del revisor (H8), reglas por monto/riesgo del contrato en la semilla (H3), modo solo lectura de emergencia en Marco (H9), métricas de equidad de la IA (H7).

## 5-bis. Decisiones de la relectura (2026-10-28, tarde)

| Id | Pregunta | Decisión | Origen |
|---|---|---|---|
| A-8 | ¿Puede el tercero excluir a una empresa de un documento global? | **No puede excluir nada.** La compartición automática con todas las empresas vinculadas que exigen el tipo es total, sin excepción por sensibilidad. Reemplaza la cláusula "el tercero puede excluir" de M-7; se eliminan `ComparticionRevocada` por decisión del tercero (solo queda por desvinculación/bloqueo), el endpoint de exclusiones y la UI correspondiente. La privacidad se garantiza porque solo reciben la versión las empresas con vínculo activo y requisito de ese tipo. | E |
| A-7 | ¿La ratificación requiere aprobación de un área? | **No.** "Si no cambia ni la información ni el documento, con la declaración basta": la ratificación del tercero renueva la vigencia en la empresa que la pidió sin pasar por revisión. Si algo cambió, no es ratificación: es versión nueva y aplica M-8. No existe la opción "ratificación con aprobación". | E |
| A-2 | Aceptación `Desactualizada` sin revalidar | **15 días de aviso y luego bloquea**: `diasRevalidacion = 15` (configurable por tenant) con el certificado `Vigente` + marca `ConPendientes` informativa; vencido el plazo, `ConPendientes` pasa a bloqueante para operaciones nuevas (consulta de certificación responde `bloqueado: true`) hasta revalidar. `desactualizadaBloquea = true` por defecto. | E |

## 5-ter. Requisitos transversales confirmados por Producto (2026-10-28, tarde)

| Id | Requisito | Cómo se cumple | Origen |
|---|---|---|---|
| R-1 | **De todo debe haber auditoría e historial** | Event sourcing en expediente/vínculo/documento/certificado (timeline); `DocumentoAccedido` por descarga; `decision_version` con actor/rol/evidencia; **`auditoria_configuracion`** (antes/después de flujos, tipos, áreas, usuarios, roles, API keys, webhooks, políticas) y **registro de identidad** (login, 2FA, API keys, token ERP, magic link); `/auditoria` con filtros y exportación ZIP firmado; retención igual al documento (P-14-7). Invariante: ninguna entidad de configuración cambia sin registro. | E |
| R-2 | **Múltiples flujos aprobatorios con aprobadores configurables** | `FlujoVersion` por tenant con etapas (secuencia) y áreas (paralelo), `RolesPermitidos`/`UsuariosPermitidos`, `AprobadoresRequeridos`, delegación, override auditado, SLA con escalamiento, separación de funciones, conflicto de intereses; **`ReglaDeSeleccionDeFlujo`** (flujo automático por empresa, tipo de vínculo, categoría, monto, riesgo) con flujo por defecto obligatorio; `CambiarFlujoDeExpediente` (admin, solo `Abierto`/`Observado`, con justificación y recálculo) y `AplicarVersionDeFlujoAExpedientesAbiertos` con simulación de impacto. | E |

## 5-quater. Decisiones de la auditoría externa de negocio (2026-10-28, noche)

| Id | Hallazgo de auditoría | Decisión | Origen |
|---|---|---|---|
| B-1 (I-1) | La bóveda se compartía con una empresa al crear el vínculo, antes de que el tercero aceptara (riesgo Ley 1581 y cosecha por NIT) | **La compartición con una empresa nueva ocurre al aceptar la invitación** (`InvitacionAceptada` + `ConsentimientosRegistrados` para esa empresa). Antes, la empresa solo ve "tercero existente; N requisitos podrían quedar cubiertos", sin nombres de documentos ni versiones. Las versiones futuras se comparten automáticamente con los vínculos ya aceptados (M-7/A-8 intactas). Precisa ADR-0005 (ADR-0009). | E |
| B-2 (I-2) | Tres modelos distintos de quién paga la lectura IA | **La lectura de hechos es coste de plataforma**: toda versión nueva se lee una vez, sin presupuesto de lectura por tenant; el coste va en el cargo por vínculo activo; los hechos IA los ven todas las empresas con compartición. El presupuesto del tenant limita solo decisiones automáticas/asistidas. Desaparece `iaHechos` por tenant; la IA puede estar deshabilitada **a nivel plataforma** (configuración u on-premise sin clave) o por **falta de consentimiento del tercero**. | E |
| B-3 (I-3) | La cédula del representante (y socios/UBO/miembros) no cabe en la clave `(tercero, tipo)` | **Persona relacionada con bóveda propia**: el representante, socio/UBO o miembro de consorcio es un `Tercero` persona natural con su propia bóveda y una relación tipada `RelacionEntreTerceros { Origen, Destino, Tipo (RepresentanteLegal|Suplente|Socio|BeneficiarioFinal|MiembroDe), Principal, Vigencia }`. Sus documentos se validan una vez y sirven a todas las empresas; `IdentidadCoincide` compara contra la persona. Los requisitos del flujo pueden exigir documentos "de la persona relacionada con rol X". | E |
| B-4 (I-4) | Frescura, nivel, plazo y aprobadores solo en el tipo de tenant | **Jerarquía tenant → empresa → regla del flujo**: `tipo_documento_tenant` define defaults; `tipo_documento_empresa` (override opcional por empresa interna) los sobreescribe; `ReglaDeExigencia` del flujo sobreescribe ambos. Un solo nombre por parámetro (`reglaFrescura`, `nivelRevision`, `diasRevalidacion`, `politicaDesactualizada`, `aprobadoresRequeridos`, `porcentajeMuestreo`). | E |
| B-5 (I-13) | Bloqueo revocaba certificado y compartición sin camino de vuelta | **Bloquear suspende**: `EstadoCertificado` gana `Suspendido`; la consulta responde `bloqueado: true, motivo: Bloqueo`; la compartición futura se congela. **Desbloquear restituye** certificado y compartición si siguen vigentes; si algo venció, abre `Renovacion`. Eventos `CertificadoSuspendido`, `CertificadoRestablecido`. | E |
| B-6 (I-16) | Acceso de soporte de Plataforma contradictorio entre 02/06 y 07 | **Nunca por defecto; acceso temporal concedido por un Administrador del tenant** con alcance (empresa, expediente o tercero) y duración en horas; queda en auditoría con actor `Plataforma`, justificación y `DocumentoAccedido`. Permiso `plataforma:soporte` + concesión `AccesoSoporteConcedido/Expirado`. | E |
| B-7 (I-19) | "Rechazado" significaba versión no admitida y expediente terminal | **Sobre una versión solo hay `Aprobado` u `Observado`**; al observar, el revisor puede marcar `noAdmitirEstaVersion` (la versión no vuelve a aceptarse en ese vínculo). **`Rechazado` queda reservado al expediente** (terminal, áreas con `PuedeRechazarExpediente`). `Veredicto { Aprobado, Observado }` + `RechazarExpediente`. `EstadoRequisito` pierde `Rechazado`. | E |
| B-8 (I-6/I-7) | Importadas divergentes bloquearían en masa a los 15 días; IA sobre históricos sin consentimiento | **Aceptaciones importadas divergentes nacen `Desactualizada` con `plazoRevalidacion = 90 días`** (no bloquean antes); la IA solo lee documentos de un tercero **después** de que acepte la autorización en su primer ingreso (hasta entonces revalidación manual o por hash). **H9 pasa a depender de H7** para no perder SAGRILAFT al activar el flag `Validasoft`. | E |

Correcciones de Arquitectura derivadas de la misma auditoría (sin decisión de Producto, se aplican directamente): I-5 (08 y 11 H9 alineados a 05), I-8 (gracia 15 en 02), I-9 (00 cierra A-2), I-10 (E6 y 04 §7.5 sin rechazo automático de identidad), I-11 (retiro de la versión actual: la anterior vuelve a `Actual` y se cancelan revalidaciones abiertas; riesgo sin IA documentado), I-12 (`flujo` opcional en `CrearExpedienteDto`), I-14 (ratificación: condición única y reinicio de frescura; sin ratificación automática), I-15 (disparador de `Renovacion` y recálculo de estado del vínculo al cerrar cualquier expediente), I-17 (`politicaDesactualizada` único), I-18 (corte por empresa en 00/09; tercero compartido en coexistencia), I-20 (SARLAFT referencia UBO/PEP globales), I-21 (identificación del usuario interno), I-22 (sin 422 informativo), I-23 (`Continuar` administrado por Plataforma), I-24 (Gantt), I-25–I-30 (nomenclatura, semilla única como anexo, rutas, ISO 3166-1, DTOs copiados en 11).

## 5-quinquies. Decisiones de Arquitectura tras la auditoría técnica (2026-10-28, noche)

| Id | Hallazgo | Decisión | Origen |
|---|---|---|---|
| T-1 (#1) | Partición de entidades repartida de forma distinta en 02/03/04/07/11 | Tres esquemas: **`plataforma`** (`tenant`, `empresa`, `uso_tenant`, `tipo_documento_canonico`, `esquema_formulario`, `catalogo_tipo_identificacion`, `plan`), **`boveda`** (tercero, relación entre terceros, documento, versión, hechos, compartición, acceso) y **`validacion`** (conjoined por tenant + `empresa_id`). 03 §2 es la fuente; los demás copian literal. | Arquitectura |
| T-2 (obs. 1) | Marten admite un solo `TenancyStyle` de eventos por store | **Un solo `DocumentStore` con eventos *conjoined***: los streams de la bóveda usan el tenant técnico `boveda` y los de plataforma el tenant `plataforma`; así se conserva la transacción única (evento + proyección + outbox). Se valida con un *spike* en H1 (criterio de aceptación). | Arquitectura |
| T-3 (obs. 2) | Marten no tiene filtros globales | El filtro por `empresa_id` se implementa con un `IQuerySession` envuelto (`ISesionDeEmpresa`) que añade el predicado, más NetArchTest que prohíbe `IQuerySession` crudo fuera de infraestructura. | Arquitectura |
| T-4 (#17) | 403 vs 404 al acceder a otra empresa/tenant | **404** para recursos de otro tenant o empresa (no se revela existencia); 403 solo por falta de permiso dentro de la propia empresa. | Arquitectura |
| T-5 (obs. 8) | `X-Empresa` cabecera vs segmento de ruta | **Cabecera `X-Empresa`** en la API; segmento `/e/{empresa}` solo en las rutas de la SPA. Cerrada la decisión abierta 6 de 05. | Arquitectura |
| T-6 (#4) | Dedupe de webhooks por `Validasoft-Delivery` en 08/ADR-0007 | Dedupe **siempre por `Validasoft-Event-Id`**; `Validasoft-Delivery` identifica el intento. | Arquitectura |
| T-7 (#5) | `RequiereRevalidacion` global dependía de configuración de tenant | La bóveda emite solo hechos: `CamposCambiados[]` y `CamposCriticosCambiados[]` según el canónico; la saga `Revalidacion` de cada tenant decide si abre revalidación (críticos canónicos **o** `camposCuyoCambioExigeHumano` propios). | Arquitectura |
| T-8 (#2) | `VinculoPorVencer(Causa: RevalidacionVencida)` contradecía A-2 | El vínculo **no cambia de estado** por revalidación vencida; la alerta sale de `certificado.con_pendientes { bloqueante: true }`. | Arquitectura |
| T-9 (#8) | Folio del expediente vs del certificado | El expediente tiene **folio propio** `{PrefijoEmpresa}-E-{Año}-{Secuencia}`; el certificado `{PrefijoEmpresa}-C-{Año}-{Secuencia}-{n}`. | Arquitectura |
| T-10 (#9/I-26) | Códigos canónicos divergentes | La semilla P-2-3 se publica como **anexo A de 03** con códigos definitivos (`CERTIFICACION_BANCARIA`, `CUENTA_BANCARIA`, `AUTORIZACION_TRATAMIENTO_DATOS`, `DATOS_MAESTROS`, `CEDULA`, …); todos los documentos la citan. | Arquitectura |
| T-11 (#10) | Antivirus con tres juegos de valores | `Antivirus { Limpio, Infectado, Cuarentena, NoDisponible }`; en prod sin AV la versión **espera** (`EnAnalisis`); en dev `NoDisponible` permite continuar. | Arquitectura |
| T-12 (#26) | Ed25519 vs X.509 para el sello | **Ed25519** con clave pública publicada en `/.well-known/validasoft-certificados.json`; X.509/RFC 3161 opcional en H11. | Arquitectura |
| T-13 (#24) | Hito del autorregistro | **H4** (necesita magic link, consentimientos y expediente). En H2 el tercero entra con un enlace emitido por el panel de plataforma/semilla (solo demo). | Arquitectura |
| T-14 (obs. 9) | Object Lock *governance* eludible | Versiones referenciadas por certificados: **compliance mode** con retención = política legal; el resto governance. | Arquitectura |
| T-15 (obs. 6) | `expediente.certificado` vs `certificado.emitido` | `certificado.emitido` es el canónico; `expediente.certificado` se elimina del catálogo (el SRM se suscribe a `certificado.emitido`). | Arquitectura |
| T-16 (#55) | SDK .NET y el SRM en .NET 7 | `Validasoft.Sdk` multi-target `net7.0;net8.0;net10.0` mientras exista el SRM. | Arquitectura |
| T-17 (#41) | WebP | Se acepta WebP además de PDF/JPG/PNG/HEIC (precisa P-16-2). | Arquitectura |
| T-18 (#27, #52) | R no actualizadas tras P-11-2 | P-13-4: corte y sincronización inversa **por empresa**. P-11-3: logo y colores **por empresa**, dominio por tenant en H8. | Arquitectura |

## 5-sexies. Confirmaciones de Producto (2026-10-29)

| Id | Pregunta | Decisión | Origen |
|---|---|---|---|
| C-1 | ¿Debe existir el concepto de invitación en la plataforma? | **Sí, tal cual está**: no es una entidad aparte sino el paso de solicitud + aceptación del vínculo (estado `Invitado`, eventos `InvitacionEnviada`/`InvitacionAceptada`, enlace de acceso, lotes). Se mantiene el nombre "invitación" en estados, eventos, endpoints y UI; se descartó renombrar a "Solicitud/Aceptación". Su razón de ser es el consentimiento por empresa (B-1/ADR-0009), el seguimiento de terceros sin respuesta y la activación del autorregistro invisible. | E |

## 5-septies. Validasoft como servicio integrable (2026-11-06)

| Id | Pregunta | Decisión | Origen |
|---|---|---|---|
| I-0 | Naturaleza del producto | **Validasoft es un servicio de validación que otros sistemas invocan** (Marco, SRM, Portal, sistemas de clientes). La validación de un vínculo es **una sola** por `(empresa, tercero, tipo de vínculo)` y su resultado aplica en **cada sistema que la referencia**: el vínculo guarda una referencia externa **por sistema** y todos reciben los mismos eventos y consultas. | E |
| ID-1 | ¿Quién autentica a la persona que revisa o aprueba desde un sistema integrado? | **Identidad delegada**: el sistema integrado autentica a su usuario y envía una aserción JWT firmada con su clave privada (Validasoft solo guarda la clave pública del sistema). Validasoft registra al usuario externo para auditoría y asignación de áreas; **no guarda contraseñas** de esos usuarios. Las **cuentas locales** (con 2FA) quedan como opción para clientes que usen Validasoft sin otro sistema (hito posterior). Precisa ADR-0003 (ADR-0011). | E |
| ID-2 | ¿Quién decide roles y áreas? | **Se asignan en Validasoft**, iguales para todos los sistemas; la aserción solo dice quién es la persona. Un usuario externo nuevo nace sin roles hasta que un administrador lo asigna. | E |
| ID-3 | ¿Dónde trabajan los revisores? | **Pantallas de Validasoft embebidas** en el sistema integrado (iframe o Web Component) con una sesión emitida a partir de la identidad delegada. La API completa sigue disponible para quien quiera su propia interfaz. | E |

## 5-octies. Implementación de la validación por el interesado (2026-11-06)

Decisiones de implementación tomadas al construir enlace de acceso, sesión del tercero, textos legales, aceptación y autorregistro (docs/05 §2.9.1, docs/07 §2.2). Origen **A** = Arquitectura (pueden objetarse aquí).

| Id | Tema | Decisión | Origen |
|---|---|---|---|
| IN-1 | Ruta de aceptación sin expedientes | `GET /public/v1/invitaciones/{vinculoId}` y `POST /public/v1/invitaciones/{vinculoId}/aceptar` mientras no existan expedientes; con H4 se agrega la variante por `expedienteId`. | A |
| IN-2 | Correo después del commit | Outbox de Marten + Wolverine: las sesiones que publican correo se abren con `OutboxedSessionFactory` (`ISesiones` con `IMessageBus`) y el mensaje `CorreoDeAccesoPendiente` va a la cola local durable `correo`; se despacha tras `SaveChangesAsync`. No se usa la cascada del endpoint porque las sesiones las abre `ISesiones` (tenant y metadatos de actor). Se activa `AutoApplyTransactions` para manejadores futuros. Fallo SMTP: reintentos 1 s/5 s/30 s y luego cola de errores. | A |
| IN-3 | Secretos en tránsito | El token y el OTP viajan en el sobre del outbox **cifrados** (AES-256-GCM, clave derivada con HKDF de `Validasoft:Sesiones:ClaveFirma`); el segmento `/enlaces/{token}` se enmascara en logs y en `instance` de los errores. | A |
| IN-4 | Reenvío e intentos | Reenviar la invitación borra los enlaces no usados del vínculo (404). Los 5 intentos de OTP son por enlace: volver a canjear envía otro código pero no reinicia la cuenta. OTP vencido ⇒ `410 otp-vencido`. | A |
| IN-5 | Concurrencia del enlace | `EnlaceDeAcceso` usa concurrencia optimista: dos verificaciones simultáneas no pierden un intento fallido (la segunda responde 412). | A |
| IN-6 | OTP y aceptación | El OTP se verifica al canjear. Si el tipo del vínculo exige OTP y la sesión no lo verificó (p. ej. entró por el enlace de otro vínculo o del autorregistro) ⇒ `403 otp-requerido`. `otpCodigo` de `AceptarInvitacionDto` queda reservado. | A |
| IN-7 | Respuesta de aceptar | `{ aceptacion: AceptacionDto[], vinculo }` sin `compartidos` ni `requisitosCubiertos` (no hay documentos). `AceptacionDto` añade `tipo` y `textoLegalId`; `documentoVersionId` nulo hasta el módulo de documentos. `textoVersion` es el número de versión del texto. | A |
| IN-8 | Alcance de la sesión | Vínculos del tercero cuyo `correoTercero` coincide con el correo del enlace, en todos los tenants (consulta `AnyTenant` de Marten), fijados al emitir el token; el tenant sale de la `Empresa`. Esquema `Tercero` elegido por ruta (`/public/v1`). Token en el cuerpo hasta `apps/public`. | A |
| IN-9 | Textos legales | Documento global `TextoLegal` (esquema `plataforma`) con `Serie` = `{tipo}|{empresa o plataforma}` y versión única por serie; hash SHA-256 del contenido recortado. La auditoría del aviso de plataforma queda en el tenant por defecto de Marten (no hay tenant). Al arrancar se siembra un aviso v1 **provisional** (menciona IA externa y transferencia internacional, P-6-5) que Legal debe reemplazar. | A |
| IN-10 | Límite de peticiones | Limitador global de ASP.NET Core, ventana fija por IP, solo para `/public/v1/*` (`Validasoft:LimitePublico`, 60/min); 429 `demasiadas-peticiones`. Pendiente: confiar en `X-Forwarded-For` del proxy de despliegue. | A |
| IN-11 | Autorregistro | Valida todos los datos antes de buscar (un dato inválido responde igual exista o no el tercero); `consentimientoPlataforma=false` ⇒ 422. El enlace de autorregistro no tiene vínculo ni OTP y vence en 72 h. | A |
| IN-12 | Nuevas fallas | `TipoFalla.Expirado` ⇒ 410 y `TipoFalla.DemasiadosIntentos` ⇒ 429; `ReglaDeNegocioException.Datos` viaja como extensiones del ProblemDetails (p. ej. `intentosRestantes`). | A |

## 5-nonies. Flujos aprobatorios y expedientes (2026-11-06)

Decisiones de implementación de la fase «Flujos aprobatorios» (docs/05 §2.4.1). Producto pidió: aprobador inicial y oficial de cumplimiento como en el SRM, no necesariamente en orden (pueden recibirlo todos a la vez), una observación o un rechazo frena el proceso hasta corregir, y opción automática. Origen **A** = Arquitectura (pueden objetarse aquí).

| Id | Tema | Decisión | Origen |
|---|---|---|---|
| FL-1 | Veredicto sin requisitos | Cada área se pronuncia sobre el **expediente completo** (`Aprobado` u `Observado`); con cero requisitos el expediente pasa a `EnRevision` al abrirse. `VeredictoDeAreaEmitido` lleva `RequisitoId` nulo: cuando existan requisitos (H2/H3) el veredicto de área se derivará de los de su alcance y los de requisito serán otro evento, sin reinterpretar los guardados. | A |
| FL-2 | Flujo versionado | Documento `Flujo` (por empresa y tipo de vínculo) con sus versiones embebidas: a lo sumo un borrador y una publicada; editar crea el borrador n+1, publicar congela y retira la anterior (y revalida que las áreas sigan activas). Sin etapas ⇒ una etapa con todas las áreas activas en paralelo. Un área aparece una sola vez en el flujo; una automática cuenta como un aprobador y no rechaza. El expediente copia la versión (con código y nombre de las áreas) en `FlujoSeleccionado`. El primer flujo publicado de `(empresa, tipo)` queda por defecto. | A |
| FL-3 | Reglas de veredicto | El estado de cada área (`EstadoAreaExpediente`) se deriva de sus veredictos vigentes: una observación manda; una aprobación del sistema o del administrador completa el área; si no, `AprobadoresRequeridos` usuarios distintos. Cada persona emite un veredicto por área y ronda (`aprobador-repetido`); un área que ya se pronunció queda cerrada en la ronda (`area-ya-se-pronuncio`). Con `Inmediata`, mientras el expediente está `Observado` nadie emite veredictos, rechaza ni aprueba como administrador (409 `expediente-observado`); con `AlCerrarArea` se devuelve cuando todas las áreas de la etapa se pronunciaron. Al reenviar, las áreas que observaron reinician **completas** (también sus aprobaciones parciales, porque el contenido cambió); las demás conservan lo suyo. Anular un veredicto se permite en `EnRevision` y `Observado` (no cambia el estado; un reenvío sin áreas observadas continúa la revisión). | A |
| FL-4 | Selección del flujo | Solo `Explicito` (el indicado al abrir a mano o el del vínculo, `CrearVinculoDto.flujoId` → `VinculoCreado.FlujoId`) o `PorDefecto`. El del vínculo sin versión publicada cae al por defecto. Las reglas de selección por condiciones (§12.1) quedan para después. | A |
| FL-5 | Proceso de validación síncrono | Vínculo y expediente se coordinan **en la misma transacción** (`ProcesoDeValidacion`, mismo tenant, misma sesión), no con una saga asíncrona: abrir ⇒ `ExpedienteVinculado` (vínculo `EnValidacion`); aprobado ⇒ `ExpedienteCerrado(Aprobado)` + `VinculoActivado` (`Activo`, `TuvoAprobacion`); rechazado o anulado ⇒ `ExpedienteCerrado` (vuelve a `Invitado`). Un vínculo anulado o desvinculado anula su expediente abierto. La concurrencia optimista del stream del vínculo garantiza un solo expediente abierto. La saga asíncrona llegará con certificados y webhooks. | A |
| FL-6 | Aceptación sin flujo | Aceptar la invitación nunca falla por configuración: sin flujo explícito ni por defecto publicado se registra la aceptación sin más eventos y el vínculo queda con `aperturaDeExpedientePendiente` (derivada: aceptada, `Invitado`, sin expediente abierto ni previo), visible en `VinculoDto` y en `GET /bandeja/aperturas-pendientes`; el administrador abre con `POST /vinculos/{id}/expedientes`. La ruta de aceptación sigue siendo por `vinculoId` (IN-1). | A |
| FL-7 | Folio | `{PrefijoFolio}-E-{Año}-{000000}` con el año en la zona horaria del tenant. `ContadorDeFolio` por `(empresa, E, año)` con concurrencia optimista en la transacción de apertura (sin huecos: si se revierte, no se consume) más índice único del folio; la apertura manual y la aceptación se reintentan solas (hasta 10 veces) si pierden la carrera, salvo que llegue `If-Match`. | A |
| FL-8 | Bandeja | `BandejaRevision` (fila expediente × área) se escribe en la misma transacción que los eventos desde el estado resultante, por el único punto de escritura del expediente (`ProcesoDeValidacion`), con id determinista; no es un `IProjection` de Marten (la proyección multi-documento quedó para un spike). Muestra expedientes `EnRevision`/`Observado`; un usuario delegado ve solo las áreas donde es miembro. Bandeja y lista de expedientes paginan con cursor por posición. | A |
| FL-9 | Permisos y revisores | Scopes `flujos:leer`, `flujos:escribir`, `expedientes:leer`, `expedientes:revisar`, `expedientes:administrar` (Administrador todo; Revisor y OficialCumplimiento leer + revisar; Auditor leer). Abrir, anular, reenviar como integrador y las acciones de administrador piden `expedientes:administrar`. Veredicto y rechazo los emite una **persona** (identidad delegada o su sesión) miembro activo del área y con rol en la empresa. Conflicto de intereses = identificación del usuario igual a la del tercero ⇒ 403 `conflicto-de-intereses` (contactos y personas relacionadas llegan con H7). Separación de funciones: no deciden quien abrió el expediente ni los usuarios que crearon el vínculo o enviaron sus invitaciones (metadatos del stream del vínculo). | A |
| FL-10 | Motivos y comunicación al tercero | Motivos base en código con id determinista; propios del tenant con auditoría. El evento guarda código **y texto al tercero** del motivo (evidencia de lo comunicado). El correo de observación (outbox, cola `correo`) y la vista pública llevan textos de motivos y observaciones, nunca revisores ni áreas internas. | A |
| FL-11 | Bloqueo | Con el vínculo `Bloqueado` el expediente no recibe veredictos, rechazos, reenvíos ni aprobaciones de administrador (409 `vinculo-bloqueado`); anularlo sí se permite. | A |
| FL-12 | Decisiones simultáneas | Las áreas revisan en paralelo, así que dos veredictos sobre el mismo expediente pueden llegar a la vez. Sin `If-Match`, la escritura toma el stream con bloqueo de fila (`FetchForExclusiveWriting`): la segunda petición espera y decide sobre el estado nuevo, sin 412. Con `If-Match` se conserva la concurrencia optimista que el cliente pidió. Revisión del 2026-11-06. | E |
| FL-13 | Anular la última observación | Si un administrador anula la última observación vigente de un expediente `Observado`, la revisión se reanuda (`RevisionReanudada`) sin esperar un reenvío; el área cuya observación se anuló queda pendiente. Con otra observación vigente, el expediente sigue devuelto. Revisión del 2026-11-06. | E |
| FL-14 | Rechazo durante la devolución | Un área con `PuedeRechazarExpediente` puede rechazar también con el expediente `Observado`: un hallazgo grave no espera la corrección del tercero. Los veredictos siguen bloqueados mientras esté devuelto. Revisión del 2026-11-06. | E |

## 5-decies. Bóveda de documentos (2026-11-06)

Decisiones de implementación de la fase «Bóveda» (H2, primera parte: sin IA, sin antivirus y sin requisitos en el expediente; docs/05 §2.9.2). Origen **A** = Arquitectura (pueden objetarse aquí).

| Id | Tema | Decisión | Origen |
|---|---|---|---|
| BO-1 | Stream y tenancy | Como fija 03 §2.2: todo `Documento-{id}` (global y `PorVinculo`) vive en el tenant técnico `boveda`; el `PorVinculo` lleva `TenantId`, `EmpresaId` y `VinculoId` como dato y su binario va al bucket del tenant. Snapshot inline `Documento` (esquema `boveda`) con índice único de la clave natural `G\|{tercero}\|{canónico}` o `V\|{vínculo}\|{tipo del tenant}`. Aceptar la invitación escribe el vínculo (tenant real) y las comparticiones (tenant `boveda`) en **una** transacción con `ForTenant("boveda")`. | A |
| BO-2 | Almacenamiento | `AWSSDK.S3` 4.0 contra MinIO (path-style, checksums solo cuando se exigen) detrás de `IAlmacenDeArchivos`. Claves de 02 §10.2 **sin extensión** (04 §6 se corrige): `boveda/terceros/{tercero}/{sha256}` y `tenant-{id}/empresas/{empresa}/vinculos/{vínculo}/{sha256}`; subida a `cargas/{cargaId}` y copia en el servidor a la clave definitiva; devueltas a `cuarentena/{documento}/v{n}`. Dos endpoints: interno (leer, copiar, borrar) y público (firmar las URLs que abre el navegador). La URL PUT fija `Content-Type` pero no `x-amz-checksum-sha256`: el `sha256` del paso 1 es opcional y se verifica al confirmar. En Compose y pruebas se usa la imagen `pgsty/minio` (fork con la misma línea de comandos) porque `minio/minio` ya no se publica en Docker Hub. Pendientes: SSE-KMS por tenant, Object Lock y purga por retención. | A |
| BO-3 | Exigencias | **Reemplazada por RQ-3 (2026-11-06)**: ya no existen las `exigencias` del tipo; lo que se pide (y se comparte) lo deciden los requisitos del flujo. Texto original: mientras no haya requisitos en el expediente, qué documentos recibe cada vínculo lo decide una lista `exigencias[] (empresaId, tipoVinculoId)` **dentro** del `TipoDocumentoTenant` (auditada con el tipo; no es una entidad aparte). Solo los tipos activos de ámbito `Global` con canónico comparten la bóveda; `PUT` reemplaza solo las exigencias de empresas al alcance de la credencial. El canónico vive en código y se referencia por **código** (no `tipoCanonicoId`). Frescura, avisos, gracia y revalidación se guardan sin aplicarse (H6). Semilla del tenant: seis canónicos de archivo sin exigencias. | A |
| BO-4 | Confirmación síncrona | El paso 2 lee el objeto, calcula el SHA-256 y valida: tamaño real (413, `Validasoft:Boveda:TamanoMaximoBytes`, 20 MB), tipo real por magic bytes PDF/JPEG/PNG y WebP (T-17) (415), PDF sin JavaScript, `Launch`, `AA`, `OpenAction` con acción, `SubmitForm`/`ImportData`, `XFA`, `RichMedia` ni adjuntos embebidos, mirando diccionarios y flujos de objetos comprimidos y decodificando `#xx` (si los tiene ⇒ `Devuelta` `pdf-con-contenido-activo`), y ≤ 50 páginas (`demasiadas-paginas`). Sin antivirus el hecho es `NoDisponible` y la versión se promueve salvo `ExigirAntivirus` (entonces espera en `EnAnalisis`, T-11). Registra `VersionCargada` → `AnalisisDeVersionIniciado` → `HechosDeVersionRegistrados` → `VersionActualCambiada`/`VersionDevueltaAlTercero` y responde 200 con el estado final (no 202). 413/415/422 no crean versión y borran la subida temporal. | A |
| BO-5 | Número de versión y dedupe | Una sola carga pendiente por documento: el paso 1 reserva `n = siguiente` con el stream bloqueado (FL-12) e iniciar otra reemplaza la anterior, así no hay huecos. Dedupe contra **todas** las versiones del documento (incluidas devueltas y retiradas): en el paso 1 si llega `sha256` (200 `duplicado` sin URL) y siempre en el paso 2 (`VersionDuplicadaDetectada`, 200 `duplicado: true`). | A |
| BO-6 | Compartición | (Desde RQ-3, «exigido» significa «pedido por el flujo resuelto o por el expediente abierto del vínculo».) Al aceptar se comparte la versión actual de cada tipo global exigido y los `PorVinculo` ya cargados para ese vínculo (concurrencia optimista; la aceptación se reintenta si una confirmación la adelanta). Al pasar una versión a `Actual` (o restituirse) se comparte, con el stream bloqueado, con todos los vínculos aceptados que la exigen, leídos después del bloqueo. Los `PorVinculo` también se comparten con su vínculo (`VersionCompartida`), de modo que la empresa siempre lee por compartición. Bloqueado, desvinculado o anulado no reciben versiones nuevas; la empresa ve lo compartido si aceptó y el vínculo no está anulado (07 §5.2). Aún sin eventos `ComparticionCongelada`/`Reanudada`. | A |
| BO-7 | Acceso auditado | `DocumentoAccedido { numero, tenant, empresa, vínculo, actorTipo, actorId, ip, userAgent, motivo }` se escribe en el stream `Documento-{id}` (con el bloqueo del documento) y no en `auditoria-accesos-{terceroId}`: los streams son Guid y así queda en el historial del documento. La descarga de la empresa es `POST …/vinculos/{id}/documentos/{doc}/versiones/{n}/descarga` porque escribe; la del tercero también se audita. URLs de 5 min con `Content-Disposition: attachment`. | A |
| BO-8 | Retiro | El tercero retira `Actual` o `Superada`; si era la actual vuelve la superada más reciente (`VersionActualRestituida`) y se comparte con quien no la tenía. El binario se borra solo si la versión nunca se compartió ni la descargó una empresa y ninguna otra versión del tercero apunta al mismo objeto (content-addressed); si no, se conserva (02 §10.3). | A |
| BO-9 | Permisos | Scopes `documentos:leer` y `documentos:descargar` en todos los roles (Administrador, Revisor, Oficial de cumplimiento, Auditor) y en la llave de administración de los tenants nuevos; las llaves ya emitidas no los tienen. Lo no compartido responde 404 (T-4). | A |
| BO-10 | Errores | `TipoFalla` gana `DemasiadoGrande` (413 `archivo-demasiado-grande`) y `TipoNoSoportado` (415 `tipo-archivo-no-soportado`). | A |

## 5-undecies. Requisitos documentales en el expediente (2026-11-06)

Decisiones de implementación de la fase «Requisitos» (H2, segunda parte; docs/05 §2.4.2). Producto decidió: lo que se comparte con una empresa lo decide el flujo (sus requisitos), no las exigencias por tipo de documento (BO-3 queda reemplazada; el encargo la llamó «BO-2»); las reglas de exigencia por condiciones (docs/03 §12) quedan para después: los requisitos de una versión de flujo son fijos y el revisor puede eximir; un flujo sin requisitos se comporta como FL-1. Origen **E** = Producto, **A** = Arquitectura (pueden objetarse aquí).

| Id | Tema | Decisión | Origen |
|---|---|---|---|
| RQ-1 | Requisitos en el flujo | `FlujoVersion.Requisitos[] { id, tipoDocumentoTenantId, codigoTipo, obligatorio, areasQueRevisan[], aprobadoresRequeridos? }`: se editan en el borrador (`CrearFlujoDto`/`EditarFlujoDto.requisitos`, por id o código del tipo; `null` conserva, `[]` quita) y se congelan al publicar. Validación al editar y al publicar: tipo del tenant activo y de clase `Archivo` (422 `requisito-invalido`, `clase-no-soportada`), sin tipos ni ids repetidos (`requisito-duplicado`), áreas del flujo (`requisito-area-invalida`), 1–10 aprobadores (`aprobadores-invalidos`). `areasQueRevisan` vacío ⇒ **todas las áreas del flujo**, cada una en su etapa (interpretación de «todas las de la etapa donde está el área»). Con requisitos, cada área debe revisar al menos uno (`area-sin-requisitos`): su aprobación se deriva de ellos. Los tipos son del tenant (no hay override por empresa todavía). | E/A |
| RQ-2 | Adjuntar versiones | La apertura emite `RequisitosCalculados` con la foto (tipo, canónico, ámbito, áreas resueltas, versión que lo cubre si ya está **compartida con el vínculo**). Una versión que pasa a `Actual` (o se restituye) y se comparte con el vínculo se adjunta a sus requisitos por el **outbox de Wolverine**: la confirmación de la carga publica `SincronizarRequisitosDelVinculo` (cola local durable `requisitos`) en la misma transacción que `VersionCompartida`. El manejador abre una sesión del tenant, toma el expediente con `FetchForExclusiveWriting` (FL-12) y lee el estado actual de la bóveda: es **idempotente y convergente** (solo adjunta si el requisito no tiene ya esa versión), así que reintentos, duplicados o desorden no repiten eventos ni retroceden versiones. Se eligió el outbox y no la misma transacción porque la carga es de la bóveda (tenant `boveda`, documento bloqueado) y el expediente es de otro tenant con su propio bloqueo: escribirlos juntos obligaba a concurrencia optimista sobre el expediente y a reintentar una confirmación que ya movió el binario cada vez que un revisor decidía a la vez. Para no depender del tiempo del mensaje, el reenvío (tercero o integrador) sincroniza en línea antes de reenviar. Un requisito ya aprobado por un área de una **etapa aprobada** conserva su versión (la nueva queda para la revalidación, H6); un retiro sin versión que restituir no cambia el requisito. | A |
| RQ-3 | Compartición por requisitos | Reemplaza BO-3. Al aceptar se comparte la versión actual de cada **tipo global que piden los requisitos del flujo resuelto** para el vínculo (el suyo publicado o el por defecto, FL-4) y los `PorVinculo` del vínculo; en la misma transacción se abre el expediente con esos requisitos ya `Cargado`. Sin flujo publicado no se comparte ningún global hasta que se abra el expediente (la apertura manual de un vínculo aceptado comparte lo que pide). Una versión nueva se comparte con los vínculos aceptados cuyo **expediente abierto o flujo vigente** pide ese canónico (consulta entre tenants). Se mantiene A-8: el tercero no excluye empresas. El campo `TipoDocumentoTenant.exigencias` y `ExigenciaDto` se **eliminan** (sin migración: no hay datos desplegados; si llega en el cuerpo se ignora). | E |
| RQ-4 | Veredicto por requisito | `POST /expedientes/{id}/requisitos/{rid}/areas/{areaId}/veredicto` emite `VeredictoDeRequisitoEmitido(requisito, área, Aprobado\|Observado, origen, revisor, sobreVersion, motivos, observación)`: solo áreas del alcance del requisito (409 `requisito-no-en-alcance`) en etapa activa; `sobreVersion` obligatorio (422 `sobre-version-requerida`) e igual a la vigente (409 `version-superada`); una persona por requisito y área (`aprobador-repetido`); decidido por el área al observarlo o al juntar `aprobadoresRequeridos` personas distintas (los del requisito o, si no, los del área; `requisito-ya-decidido`). Un requisito sin versión (opcional no cargado) no se decide (`requisito-sin-version`); los opcionales sin cargar no cuentan para nadie. Con requisitos, el veredicto sobre el expediente completo responde 409 `veredicto-por-requisito`; sin requisitos, el de requisito responde 409 `sin-requisitos`. No hay `noAdmitirEstaVersion` ni `camposAfectados` todavía. | A |
| RQ-5 | Área derivada | El área queda `Aprobada` cuando aprobó (o se eximieron) todos los requisitos de su alcance; `Observada` si observó alguno. No hay comando `AprobarArea`: el sistema emite `VeredictoDeAreaEmitido(Aprobado, RequisitoId = null, DerivadoDeRequisitos = true, RevisorId = null)` y la cascada existente sigue (etapa, expediente, vínculo `Activo`). Su **origen** dice quién aprobó los requisitos: `Administrador` si intervino (o solo hubo exenciones), si no `Humano`, si no `Sistema` (área automática); así el certificado podrá mostrar `areas[].origen` sin confundir una aprobación humana con una automática. Si el área pierde la aprobación (versión nueva), ese veredicto derivado deja de estar vigente. Las áreas automáticas aprueban sus requisitos (origen `Sistema`) al activarse la etapa y de nuevo si llega una versión nueva. Aprobar como administrador aprueba los requisitos pendientes del área; anular un veredicto alcanza también los de sus requisitos (FL-13 igual). | A |
| RQ-6 | Exención | `POST /expedientes/{id}/requisitos/{rid}/eximir { justificacion }` con **`expedientes:administrar`** (no se crea aún el permiso `requisitos:eximir` ni `PuedeEximir` por área: llegan con la configuración fina de áreas). Evento `RequisitoEximido(por, justificación)`; sus veredictos dejan de contar; si era lo último que faltaba cargar el expediente pasa a revisión, y si era la última observación vigente de un expediente devuelto la revisión se reanuda (`RevisionReanudada` con `requisitoEximidoId`). No se exime un requisito aprobado (409 `estado-invalido`) ni dos veces (`requisito-eximido`). | A |
| RQ-7 | Devolución y reenvío | La política (`Inmediata`/`AlCerrarArea`) se aplica igual; con `AlCerrarArea` un área «se pronunció» cuando decidió todos los requisitos de su alcance. `ExpedienteObservado.requisitosObservados`. Una versión nueva de un requisito observado invalida su observación (el requisito queda `Cargado` esperando el reenvío) sin reanudar sola la revisión. Reenviar exige que cada requisito observado tenga versión nueva o, si no, una respuesta escrita (409 `requisitos-sin-corregir`); `ExpedienteReenviado.requisitosReabiertos`: solo esos vuelven a revisión (todas sus áreas), los demás conservan sus veredictos. El tercero ve las observaciones solo cuando el expediente se le devuelve. | A |
| RQ-8 | Versión nueva | `PoliticaNuevaVersion` solo `InvalidarSoloRequisito` (la semilla de docs/11): `VersionAdjuntadaARequisito` + `VeredictosInvalidadosPorNuevaVersion(requisito, versiónAnterior, áreasAfectadas)` solo para ese requisito; el contador `reenvios` del requisito sube con cada versión nueva. Sin alerta al tercer reenvío ni `InvalidarEtapa` todavía. | A |
| RQ-9 | Lecturas para las pantallas | `GET /usuarios/resumen?ids=a,b` devuelve `{ id, nombre }` de usuarios del tenant (activos o no; hasta 100 ids; los inexistentes se omiten) a cualquier credencial con `expedientes:leer` **o** `vinculos:leer` (403 `permiso-insuficiente`; 422 `ids-invalidos`), sin correo ni roles. `GET /yo` (cualquier credencial de tenant): actor, usuario `{id, nombre}` si es una persona, sus roles en las empresas a su alcance, permisos efectivos en la empresa activa, sistema integrado y empresas que puede usar. | A |
| RQ-10 | Versión nueva con etapas en secuencia | Una versión nueva se adjunta al requisito mientras **alguna** de sus áreas siga en una etapa abierta (p. ej. una etapa posterior que lo observó) y solo invalida los veredictos de esas áreas; lo aprobado por etapas ya cerradas queda como aprobado sobre la versión anterior (no se reabre la etapa: eso es `PoliticaNuevaVersion = InvalidarEtapa`, pendiente). Si todas sus áreas están en etapas cerradas, la versión nueva no se adjunta (queda para la revalidación). Corrige el caso en que el tercero no podía corregir un documento observado por la etapa 2 que ya había aprobado la etapa 1. Revisión del 2026-11-06. | E |

## 6. Consecuencias estructurales a propagar

| Decisión | Documentos afectados | Acción |
|---|---|---|
| P-11-2 tenant = grupo con empresas internas aisladas | 02, 03, 04, 05, 06, 07, 08, 10 | Introducir `Empresa` dentro del tenant; `empresa_id` en vínculo, aceptaciones, decisiones, certificados; roles por empresa; API keys con alcance; **ADR-0008** |
| P-1-1/P-1-4 multipaís | 02, 03, 05, 10 | `catalogo_tipo_identificacion(pais, codigo, validador)`; validadores por país (NIT/CC/CE/PA/RFC/CUIT…) |
| P-1-7 IA opcional en autovalidación | 02, 03, 04 | La publicación de datos maestros no depende de la IA; sin IA, revalidación manual |
| P-3-3 el tercero elige la cuenta por empresa | 02, 03, 05, 06 | `CuentaDesignada` la fija el tercero; la empresa aprueba el cambio (4 ojos) |
| P-17-2 GitHub | 04, 09 | CI con GitHub Actions; Issues/Projects; reemplazar menciones a Azure Pipelines |
| P-3-1/P-3-2 Formulario | 02, 03, 05, 06 | Clase `Formulario` con esquema canónico + campos privados; renderizador JSON Schema |
| P-14-3 KMS por tenant | 04, 07 | Clave por tenant; bóveda con clave de plataforma |
| P-15-2 panel plataforma | 06, 07 | Rol `Plataforma`, rutas `/plataforma/*` |
