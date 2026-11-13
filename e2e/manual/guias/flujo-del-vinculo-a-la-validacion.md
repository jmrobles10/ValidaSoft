---
titulo: "Del vínculo a la validación, de punta a punta"
seccion: "Flujo de punta a punta"
orden: 20
resumen: "El camino completo: la empresa se configura y crea el vínculo, el tercero acepta y completa su expediente, las áreas y el oficial revisan, se emite el certificado y luego vienen las vigencias, las actualizaciones, los bloqueos y los webhooks de cada hito."
---

Esta guía recorre, en orden, todo lo que pasa entre una empresa y un tercero, desde que la empresa se configura hasta que el vínculo vence, se actualiza o se bloquea. Cada etapa dice **quién hace qué**, **qué reglas aplican**, **qué estados y eventos produce** y **qué ajuste la controla**, y termina con los recorridos del manual que la muestran con pantallazos. Los recorridos que aún no existen aparecen subrayados con guiones: son los que faltan por documentar (la lista completa está en `e2e/manual/INVENTARIO.md`).

El mapa de estados de cada pieza está en [Ciclos de vida](guia:arquitectura-ciclos-de-vida) y el vocabulario en el [Glosario](guia:glosario).

## El panorama

| # | Etapa | Quién | Qué cambia | Hito para un sistema integrado |
|---|---|---|---|---|
| 1 | La empresa se configura | Administrador y Plataforma | Tipos, flujo, áreas, textos legales, IA y listas listos. | — |
| 2 | Se crea el vínculo | Administrador o sistema integrado | Vínculo `Invitado` con su invitación. | `vinculo.creado`, `vinculo.invitacion_enviada` |
| 3 | Invitación y aceptación | Tercero | Textos aceptados; se abre el expediente; vínculo `EnValidacion`. | `vinculo.invitacion_aceptada`, `expediente.abierto` |
| 4 | El asistente del tercero | Tercero | Documentos y ficha completos; el expediente pasa a revisión. | `expediente.en_revision` |
| 5 | Revisión automática con IA | Sistema | Hechos del documento; sugerencias o aprobaciones. | — |
| 6 | Revisión por áreas y etapas | Revisores | Veredictos por requisito y por área. | `expediente.sla_por_vencer`, `expediente.sla_incumplido` |
| 7 | Observaciones y reenvío | Revisores y tercero | El expediente va y vuelve. | `expediente.observado`, `expediente.reenviado` |
| 8 | Verificaciones de cumplimiento | Sistema y oficial | Listas y estudio satisfechos. | `expediente.verificacion_registrada` |
| 9 | Aprobación | Sistema | Expediente `Aprobado`, vínculo `Activo`. | `expediente.aprobado`, `vinculo.activado` |
| 10 | Certificado y verificación pública | Sistema y público | Certificado `Vigente`. | `certificado.emitido` |
| 11 | Vigencias, avisos y renovación | Sistema y tercero | `PorVencer`, `Vencido`, renovación. | `documento.por_vencer`, `vinculo.por_vencer`, `vinculo.vencido` |
| 12 | Actualización con una versión nueva | Tercero y empresa | Vínculo «En revisión», certificado «En verificación». | `certificado.con_pendientes`, `certificado.reemplazado` |
| 13 | Varias empresas | Todos | Un documento, varias decisiones privadas. | — |
| 14 | Bloqueo y terminación | Administrador y oficial | `Bloqueado`, `Desvinculado` o `Anulado`. | `vinculo.bloqueado`, `certificado.suspendido` |

## Parte 1: del alta a la aprobación

![Diagrama por actor: del alta del vínculo a la aprobación del expediente](diagramas/flujo-a.svg)

### 1. La empresa se configura

Antes de invitar a nadie, **Plataforma** da de alta el grupo y sus empresas (y, si quiere IA, carga el plan y la llave de OpenAI de cada empresa). El **administrador** del grupo deja listo, en Configuración:

- los **textos legales** de cada empresa (la autorización de datos y los términos): sin ellos el tercero no puede aceptar la invitación;
- los **tipos de documento** (RUT, Cámara de Comercio, formularios propios…) con su formato, vigencia y plantilla;
- los **tipos de vínculo** (Proveedor, Cliente…), que dicen si el enlace exige un código por correo;
- las **áreas** y sus miembros;
- el **flujo aprobatorio**: qué documentos y secciones de la ficha se piden (y a quién: persona jurídica o natural), las etapas en secuencia y las áreas en paralelo con sus aprobadores, los plazos, la vigencia del vínculo y las verificaciones de cumplimiento. Hay que **publicarlo** y dejarlo como flujo por defecto de su tipo de vínculo;
- opcionalmente la **revisión automática**, las **listas restrictivas**, la **matriz de riesgo**, las plantillas y el dominio de correo.

Sin un flujo publicado por defecto, el tercero que acepta la invitación queda esperando: el vínculo queda con la apertura de su expediente pendiente.

> **Configuración:** [Configuración de la empresa](guia:configuracion-de-la-empresa#flujos-aprobatorios), [del grupo](guia:configuracion-del-grupo) y [de Plataforma](guia:configuracion-de-plataforma).
>
> **Recorridos:** [[plataforma/crear-grupo-y-empresa|Plataforma crea el grupo y la empresa]] · [[plataforma/plan-de-ia-y-llave-openai|Plan de IA y llave de OpenAI]] · [[configuracion/empresas-y-textos-legales|Empresas y textos legales]] · [[configuracion/tipo-de-documento-y-flujo|Tipo de documento y flujo]] · [[configuracion/tipos-de-vinculo|Tipos de vínculo]] · [[configuracion/areas-y-miembros|Áreas y miembros]] · [[configuracion/flujo-etapas-y-areas-en-paralelo|Etapas y áreas en paralelo]] · [[configuracion/flujo-condiciones-por-tipo-de-persona|Condiciones por tipo de persona]] · [[configuracion/flujo-secciones-de-la-ficha|Secciones de la ficha]] · [[configuracion/flujo-plazo-reintento-y-recordatorios|Plazo, reintento y recordatorios]] · [[configuracion/flujo-vigencia-del-vinculo|Vigencia del vínculo]] · [[configuracion/flujo-verificaciones-de-cumplimiento|Verificaciones de cumplimiento]] · [[configuracion/flujo-versiones-y-borradores|Versiones y borradores]] · [[configuracion/flujo-por-defecto|Flujo por defecto]] · [[configuracion/usuarios-y-roles|Usuarios y roles]]

### 2. Se crea el vínculo

Un **vínculo** es la relación de una empresa con un tercero en un tipo. Se crea de dos maneras:

- **Desde la app**, en **Vínculos**, con **Invitar por lote** (una fila basta): se sube la plantilla con los terceros, se revisa la vista previa y se invita; las filas con errores o repetidas no se procesan.
- **Desde un sistema integrado**, con `POST /api/v1/vinculos` (con `X-Api-Key`, `X-Empresa` y `Idempotency-Key`): el cuerpo lleva la identificación del tercero, su tipo de persona y nombre, el tipo de vínculo, el correo de invitación y la **referencia externa** del sistema. Es idempotente: reintentar no duplica.

Validasoft **reconcilia al tercero por su identificación** (país, tipo y número normalizados): si ya existe en la plataforma, lo reutiliza sin tocar sus datos, y con él su bóveda. El vínculo nace `Invitado`, único por empresa, tercero y tipo, y se envía el correo con el **enlace de acceso**: de un solo uso y con una vigencia de 72 horas por omisión (de 1 hora a 30 días). La empresa puede **reenviar** la invitación. Si el plan del grupo está en su límite de vínculos con bloqueo, la creación responde 409.

También existe el **autorregistro** (`POST /public/v1/autorregistro`: el propio tercero se anota) y la **importación** de lo que ya existía en el Portal.

> **Configuración:** [Tipos de vínculo](guia:configuracion-del-grupo#tipos-de-vinculo), [Plan del grupo](guia:configuracion-del-grupo#plan-del-grupo) y [guía de integración](guia:guia-de-integracion#invitar-a-un-tercero).
>
> **Recorridos:** [[operacion/invitacion-por-lotes|Invitación por lotes]] · [[operacion/lote-pausar-reanudar-cancelar|Pausar, reanudar o cancelar un lote]] · [[integracion/invitar-por-api|Invitar por API]] · [[integracion/sistema-integrador|Registrar el sistema integrador]] · [[integracion/api-keys|API keys]] · [[vinculos/lista-y-filtros|Lista de vínculos y filtros]] · [[vinculos/detalle-del-vinculo|Detalle del vínculo]] · [[vinculos/reenviar-invitacion|Reenviar la invitación]] · [[vinculos/abrir-expediente-manual|Abrir el expediente a mano]]

### 3. Invitación y aceptación

El tercero abre el enlace del correo (`/e/{token}`). El enlace se canjea **una sola vez** (si ya se usó o venció, la pantalla lo dice y explica cómo recibir otro). Si el tipo de vínculo lo exige, le llega al mismo correo un **código de 6 dígitos** (vale 10 minutos, 5 intentos). Entra a **Mis empresas**, donde ve cada empresa que lo invitó, y abre la invitación:

1. Lee y acepta los **textos legales**: el aviso de datos de Validasoft (solo si aún no lo aceptó) y la autorización y los términos de **esa empresa**. Cada texto tiene su versión; si cambió mientras lo leía, se le muestra la nueva.
2. Al aceptar se guarda como constancia la versión y el **hash** de cada texto, la fecha, la IP y el dispositivo, y si verificó el correo con el código.
3. Se publica `ConsentimientosRegistrados` e `InvitacionAceptada` en el vínculo y **se abre el expediente inicial** con el flujo del vínculo o, si no tiene uno propio, el flujo por defecto de su tipo. El vínculo pasa a `EnValidacion`.
4. Solo desde ese momento la empresa puede ver algo de la bóveda: se **comparten** con ella las versiones vigentes que el flujo exige y que el tercero ya tenía (por ejemplo un RUT que subió para otra empresa). Antes de aceptar, la empresa solo sabe cuántos requisitos podrían quedar cubiertos.

Un tercero **sin invitación a mano** puede entrar a `/entrar` con su correo y pedir un enlace de acceso nuevo (vale 1 hora).

> **Variantes:** según el tipo de vínculo hay código por correo o no; según el tipo de persona (jurídica o natural) el flujo pide otros documentos y secciones; si la empresa no ha publicado sus textos, la pantalla dice «aún no publica sus textos» y no se puede aceptar.
>
> **Recorridos:** [[tercero/aceptar-invitacion-persona-juridica|Aceptar la invitación como persona jurídica]] · [[tercero/aceptar-invitacion-persona-natural|Aceptar la invitación como persona natural]] · [[tercero/entrar-sin-invitacion|Entrar sin invitación]] · [[tercero/mis-empresas-varias-invitaciones|Mis empresas con varias invitaciones]] · [[tercero/invitacion-vencida-o-usada|Invitación vencida o ya usada]] · [[privacidad/lo-que-acepte-y-textos-nuevos|Lo que aceptaste y textos nuevos]] · [[movil/tercero-entrar|Entrar desde el celular]]

### 4. El asistente del tercero

La pantalla **Tu revisión con {empresa}** lista lo que falta: cada **documento** que el flujo pide y cada **sección** de la ficha («Mi información»). El tercero:

- **Sube documentos**: elige el archivo (PDF, JPG, PNG o WebP de hasta 20 MB y 50 páginas), el navegador lo sube directo al almacén y el servidor lo verifica ([Almacenamiento de documentos](guia:arquitectura-documentos)). Un tipo puede ser de **solo formulario** (datos sin archivo), tener una **plantilla** para descargar, llenar, firmar y subir, y una **ayuda** escrita por la empresa. Cada documento nuevo es una versión; la anterior queda superada.
- **Llena su ficha** por secciones (información general, tributaria, legal, financiera por periodo, SST y especialidades), con sus soportes, sus personas relacionadas y, si aplica, la **declaración PEP**. Puede proponerse un dato desde un documento («Actualizar desde mi RUT») y se cruza cada sección con sus soportes. La barra siempre visible muestra lo que falta y avisa antes de salir sin guardar.
- Elige su **cuenta bancaria designada** y sus **contactos** por empresa.

El expediente **pasa solo a revisión** cuando todos los requisitos obligatorios están cargados (o enseguida si el flujo no pide requisitos): se publica `ExpedienteEnviadoARevision`, se activa la primera etapa y entra a la bandeja de sus áreas. Lo ya aprobado antes por la empresa para esa misma versión se aprueba al instante.

> **Variantes:** persona natural: no ve las secciones legal ni financiera; un requisito puede ser opcional, y los opcionales no impiden pasar a revisión; en celular las pantallas del tercero se adaptan (390 px).
>
> **Configuración:** [Tipos de documento](guia:configuracion-del-grupo#tipos-de-documento), [Ayuda de documentos](guia:configuracion-de-la-empresa#ayuda-de-documentos) y el [flujo](guia:configuracion-de-la-empresa#documentos-y-secciones-que-pide).
>
> **Recorridos:** [[tercero/boveda-mis-documentos|Mi bóveda de documentos]] · [[tercero/boveda-subir-un-documento|Subir un documento]] · [[tercero/subir-documento-con-formulario|Subir un documento con formulario]] · [[tercero/ficha-secciones-con-soporte|Secciones de la ficha con soporte]] · [[tercero/ficha-actualizar-desde-mi-rut|Actualizar desde mi RUT]] · [[tercero/ficha-barra-y-pendientes|La barra de la ficha y los pendientes]] · [[tercero/declaracion-pep-persona-natural|Declaración PEP de una persona natural]] · [[tercero/declaracion-pep-personas-relacionadas|Declaración PEP de personas relacionadas]] · [[tercero/cuentas-bancarias|Cuentas bancarias]] · [[tercero/contactos-por-empresa|Contactos por empresa]] · [[operacion/ayuda-de-documentos|Ayuda de documentos]] · [[configuracion/tipo-de-documento-solo-formulario-y-plantilla|Tipo de documento solo de formulario y plantilla]] · [[movil/tercero-subir-documento|Subir un documento desde el celular]] · [[movil/tercero-mi-informacion|Mi información desde el celular]]

### 5. Revisión automática con IA

Cada versión nueva pasa por los **hechos básicos** (hash, tipo real, páginas) y, si hay IA disponible, se **analiza una sola vez** con OpenAI (cola `ia`): se extraen campos, se detecta el tipo, la legibilidad y las señales de alteración, y se compara con la versión anterior. Paga una de las empresas con las que está compartida que tenga **plan activo, llave y cupo**; si no hay quien pague, o la IA está deshabilitada, la versión sigue a revisión humana. Un segundo análisis con un modelo mayor ocurre si la confianza queda bajo el umbral, el documento es ilegible, hay alertas o cambia un campo crítico.

Cada empresa decide **cuánto confía** en ese análisis con su **nivel de revisión**:

| Nivel | Qué hace el sistema |
|---|---|
| **Manual** | Solo informa: los revisores ven los hechos del análisis y deciden todo. |
| **Asistido** | Sugiere «aprobar» o «revisar» con motivos en cada documento; nunca emite el veredicto. |
| **Automático** | Aprueba solo lo que no tiene excepciones (confianza sobre el umbral, tipos y áreas autorizados, sin alertas); un porcentaje de lo aprobado se muestra a un miembro del área para confirmarlo o revertirlo. Lo demás va a **Excepciones**. |

Los veredictos del sistema llevan origen `Sistema`, su confianza y la justificación. Sin cupo, el documento va de inmediato a una persona; con la IA apagada, todo es Manual. Una versión **reenviada** tras una observación siempre pasa por una persona.

> **Configuración:** [Revisión automática](guia:configuracion-de-la-empresa#revision-automatica) y [Plan de IA de una empresa](guia:configuracion-de-plataforma#plan-de-ia-de-una-empresa).
>
> **Recorridos:** [[ia/plan-y-uso-de-ia|Plan y uso de IA]] · [[ia/revision-automatica-niveles|Niveles de la revisión automática]] · [[ia/revision-asistida-y-excepciones|Revisión asistida y excepciones]] · [[ia/revision-automatica-aprueba-sola|La revisión automática aprueba sola]] · [[ia/cupo-agotado|Cupo agotado]] · [[ia/escalamiento-a-modelo-superior|Escalamiento a un modelo superior]] · [[ia/cruce-de-la-ficha-con-los-soportes|Cruce de la ficha con los soportes]] · [[ia/analisis-de-un-documento|Análisis de un documento]]

### 6. Revisión por áreas y etapas

El flujo define **etapas en secuencia**; las **áreas** de una misma etapa revisan **en paralelo**, cada una con su número de **aprobadores** (personas distintas). Un revisor ve en **Inicio** lo que le toca y en la **Bandeja** los expedientes de las áreas de las que es miembro. Abre el expediente y, para cada documento o sección de su área, ve la versión (con su visor y los datos del análisis), los veredictos de las otras áreas y el historial. Puede:

- **aprobar** el requisito o toda el área;
- **observar** con motivos (ver la etapa 7);
- **eximir** un documento, si la empresa no lo necesita para ese tercero.

Un área aprueba cuando todos sus requisitos y todos sus aprobadores aprobaron; una etapa, cuando todas sus áreas lo hicieron; solo entonces **empieza la siguiente**. Reglas que aplican: la **separación de funciones** (quien invitó o abrió el expediente no decide, si el flujo la exige), el **conflicto de intereses** (siempre), la **delegación** por ausencias, las **áreas automáticas** (aprueban solas al activarse la etapa) y el **plazo de revisión** (SLA) de cada área, que avisa al consumir un porcentaje y al incumplirse. Un administrador puede **aprobar en nombre del área** con una justificación, **anular un veredicto** o **desaprobar** lo ya aprobado.

> **Configuración:** [Áreas](guia:configuracion-de-la-empresa#areas), [Plazos de revisión](guia:configuracion-de-la-empresa#plazos-de-revision) y [Delegación de revisión](guia:configuracion-de-la-empresa#delegacion-de-revision).
>
> **Recorridos:** [[revision/inicio-lo-que-te-toca|Inicio: lo que te toca]] · [[revision/bandeja-de-revision|Bandeja de revisión]] · [[revision/aprobar-un-area|Aprobar un área]] · [[revision/aprobar-documento-por-documento|Aprobar documento por documento]] · [[revision/etapas-en-secuencia|Etapas en secuencia]] · [[revision/dos-aprobadores-y-separacion-de-funciones|Dos aprobadores y separación de funciones]] · [[revision/aprobar-en-nombre-del-area|Aprobar en nombre del área]] · [[revision/eximir-un-documento|Eximir un documento]] · [[revision/visor-y-datos-de-un-documento|Visor y datos de un documento]] · [[revision/historial-del-expediente|Historial del expediente]] · [[operacion/delegacion-de-revision|Delegación de revisión]] · [[operacion/plazos-de-revision|Plazos de revisión]] · [[informes/plazos-de-revision|Informe de plazos de revisión]]

### 7. Observaciones y reenvío

Cuando un área **observa**, elige uno o varios **motivos** del catálogo (los de la plataforma y los propios) por requisito, con un texto dirigido al tercero que **nunca nombra al revisor**. El expediente pasa a `Observado`:

- con la política **Inmediata**, al instante, y frena a las demás áreas; con **AlCerrarArea**, cuando todas las áreas de la etapa se pronunciaron;
- el tercero recibe un **correo** con un enlace de acceso (vale 72 horas) y una notificación en su **campana**; en su pantalla ve «Tienes observaciones» con los motivos por documento;
- **corrige** subiendo una versión nueva (o editando su ficha) y pulsa **Enviar respuesta**: solo los requisitos observados vuelven a revisión, y reenviar sin corregirlos responde 409 `requisitos-sin-corregir`;
- si la empresa lo configuró, un **recordatorio automático** le llega cada cierto número de días mientras el expediente siga en sus manos;
- el **plazo de cargue** (30 días por omisión) corre para completar el expediente inicial y para cada devolución; avisa 15, 7 y 1 días antes y, al vencer, el expediente pasa a `Caducado`: la empresa puede **reabrirlo** con un plazo nuevo.

Un **rechazo** es distinto: lo hace un área que puede rechazar (o el oficial, ante un hallazgo ya registrado) con motivos y justificación; es terminal para ese expediente. Si el flujo lo permite, el tercero ve los motivos y puede **volver a postularse** tras la espera configurada; si no, solo la empresa abre un expediente nuevo.

> **Configuración:** [Motivos](guia:configuracion-del-grupo#motivos), [Plantillas de correo](guia:configuracion-de-la-empresa#plantillas-de-correo) y [datos generales del flujo](guia:configuracion-de-la-empresa#datos-generales).
>
> **Recorridos:** [[revision/observar-con-motivos|Observar con motivos]] · [[tercero/responder-observaciones|Responder las observaciones]] · [[revision/responder-con-lo-corregido|Responder con lo corregido]] · [[revision/recordatorio-de-observaciones|Recordatorio de observaciones]] · [[operacion/recordatorio-automatico-por-flujo|Recordatorio automático por flujo]] · [[operacion/plazo-de-cargue-y-caducidad|Plazo de cargue y caducidad]] · [[revision/rechazar-expediente|Rechazar un expediente]] · [[tercero/rechazo-y-volver-a-postularme|Rechazo y volver a postularme]]

### 8. Verificaciones de cumplimiento

Si el flujo las pide, al pasar a revisión se **consultan solas las listas restrictivas** (al tercero y a sus personas relacionadas: representantes, suplentes, socios y PEP) con el proveedor de la empresa. Cada consulta guarda su evidencia en PDF. Si hay **coincidencias**, la verificación queda `CoincidenciaPorAnalizar` y el **oficial de cumplimiento** las analiza en su **bandeja**: las **descarta** o las **confirma** (una confirmada deja la verificación `Fallida`). El **estudio de cumplimiento** (SAGRILAFT) lo registra el oficial: nivel de riesgo (calculado con la matriz de riesgo si la empresa la activó), conflicto de interés, debida diligencia y concepto favorable o desfavorable, con adjuntos; vence y vuelve a pedirse. Los vínculos activos se **reconsultan** periódicamente (monitoreo).

Una verificación marcada **bloqueante** (por omisión lo es) impide aprobar el expediente mientras esté pendiente, con coincidencias por analizar, fallida o vencida, aunque todas las áreas hayan aprobado. El oficial también puede **rechazar directamente** ante un hallazgo ya registrado.

> **Configuración:** [Listas restrictivas](guia:configuracion-de-la-empresa#listas-restrictivas), [Matriz de riesgo](guia:configuracion-de-la-empresa#matriz-de-riesgo) y [verificaciones del flujo](guia:configuracion-de-la-empresa#verificaciones-de-cumplimiento).
>
> **Recorridos:** [[cumplimiento/listas-restrictivas-de-la-empresa|Listas restrictivas de la empresa]] · [[cumplimiento/pedir-listas-restrictivas|Pedir las listas restrictivas]] · [[cumplimiento/analizar-una-coincidencia|Analizar una coincidencia]] · [[cumplimiento/coincidencia-confirmada|Coincidencia confirmada]] · [[cumplimiento/estudio-de-cumplimiento|Estudio de cumplimiento]] · [[cumplimiento/estudio-vencido-y-renovacion|Estudio vencido y renovación]] · [[cumplimiento/bandeja-del-oficial|Bandeja del oficial]] · [[cumplimiento/rechazo-directo-del-oficial|Rechazo directo del oficial]] · [[cumplimiento/matriz-de-riesgo-configuracion|Configurar la matriz de riesgo]] · [[cumplimiento/monitoreo-periodico|Monitoreo periódico]] · [[cumplimiento/informe-sagrilaft|Informe SAGRILAFT]]

### 9. Aprobación

Cuando **todas las etapas están aprobadas, las verificaciones bloqueantes están satisfechas y vigentes y, si el tipo de vínculo exige cuenta bancaria designada, la cuenta del vínculo está aprobada**, el expediente pasa a `Aprobado` y, **en la misma transacción**:

- el vínculo pasa a `Activo`;
- las secciones de la ficha quedan **aceptadas por la empresa** (evento `seccion.aceptada`), que es lo que un ERP lee como la ficha vigente;
- se **emite el certificado** (etapa 10);
- el tercero recibe un correo y una notificación de resultado, y se publican los eventos `expediente.aprobado`, `vinculo.activado` y `certificado.emitido`.

Si el tipo de vínculo exige cuenta bancaria designada (el PROVEEDOR sembrado la exige) y la cuenta aún no está aprobada, el expediente **espera en `EnRevision`** aunque todas las áreas hayan aprobado: la empresa ve el aviso «Falta la cuenta bancaria designada» (con acceso a la cuenta del vínculo) y el tercero ve «Falta tu cuenta bancaria designada» (con acceso a «Mis cuentas»). Cuando dos personas de la empresa aprueban la cuenta, el expediente vuelve a evaluarse **en la misma transacción** y, si nada más falta, se aprueba solo. Rechazar la cuenta no rechaza el expediente: sigue esperando a que el tercero designe otra.

> **Recorridos:** [[vinculos/cuenta-designada-aprobar|Aprueba la cuenta bancaria designada de un proveedor]] · [[tercero/cuentas-bancarias|Designa la cuenta en la que te paga una empresa]]

Si algo de lo aprobado se ve mal más tarde, un administrador puede **desaprobar** el último expediente aprobado (el mismo vuelve a revisión y el vínculo a `EnValidacion`) o reabrir una etapa.

> **Recorridos:** [[tercero/expediente-aprobado|El expediente aprobado, visto por el tercero]] · [[revision/desaprobar-y-reabrir-una-etapa|Desaprobar y reabrir una etapa]] · [[revision/desaprobar-un-expediente-aprobado|Desaprobar un expediente aprobado]] · [[revision/anular-un-veredicto|Anular un veredicto]]

## Parte 2: del certificado a las vigencias

![Diagrama por actor: del certificado a las vigencias, la actualización y el bloqueo](diagramas/flujo-b.svg)

### 10. Certificado y verificación pública

El certificado de un vínculo aprobado lleva el folio `{PREFIJO}-C-{año}-{secuencia}-{emisión}`, el titular, el emisor, el flujo aprobado, las fechas, el **hash** del contenido canónico y un **sello Ed25519** de la plataforma. El **PDF** lo genera un proceso en segundo plano (con el folio, la URL de verificación, el hash, el sello y un QR). La empresa lo descarga; la empresa puede además **revocar** un certificado con justificación, y el bloqueo de un vínculo lo suspende.

Cualquiera, sin sesión, lo **verifica** en `/verificar/{folio}` (o por `GET /public/v1/verificar/{folio}`, con la `huella` si el folio existe en más de un grupo): ve su estado, su vigencia y qué se aprobó, y si está **Vigente** o «En verificación» puede descargar el PDF. La página **comprueba en el navegador** que el certificado es auténtico: recalcula el hash del contenido canónico y verifica el sello Ed25519 con las claves públicas de `/.well-known/validasoft-certificados.json`, así no depende solo de lo que diga el servidor. El QR y la URL del PDF abren esta página con la huella. Un certificado revocado, vencido, suspendido o reemplazado se muestra como tal, sin PDF.

Un **sistema integrador** se entera con `certificado.emitido` y, antes de pagar, **consulta la certificación**: paga solo si `certificado: true` y `bloqueado: false` ([guía de integración](guia:guia-de-integracion#consulta-de-certificacion)).

> **Recorridos:** [[informes/certificados|Certificados]] · [[informes/certificado-bloquear-y-revocar|Bloquear y revocar un certificado]] · [[publico/verificar-certificado-autentico|Verificar un certificado auténtico]] · [[publico/verificar-certificado-invalido|Verificar un certificado inválido]] · [[publico/certificado-en-otros-estados|Certificado en otros estados]] · [[publico/verificar-con-el-pdf|Verificar con el PDF]] · [[movil/verificar-certificado|Verificar desde el celular]] · [[integracion/consulta-de-certificacion|Consulta de certificación]]

### 11. Vigencias, avisos y renovación

Hay **dos capas** de vigencia y las dos las ejecuta el propio sistema, sin que nadie dispare nada:

- **Del documento** (por empresa y vínculo): cada tipo de documento dice cuánto sirve (desde la expedición o desde la carga, o la fecha que el documento declara). Llega un **aviso** a los días configurados (30, 15 y 5 por omisión; a las 13:00 UTC, 08:00 en Colombia), luego una **gracia** (15 días) y después el documento **vence**: `documento.por_vencer` y `documento.vencido`. El tercero ve un aviso consolidado y puede **subir una versión nueva** o **ratificar**.
- **Del vínculo**: si el flujo tiene vigencia (días desde la última aprobación), el primer aviso deja el vínculo `PorVencer` y **abre la renovación** del tercero: ratificar o actualizar las secciones que pide el flujo; al aprobarse, el vínculo vuelve a `Activo` y se reemite el certificado. Si se cumple la fecha más la gracia, queda `Vencido` (`vinculo.vencido`) con su renovación abierta, y el certificado vence. Un documento obligatorio vencido también vence el vínculo.

La empresa puede adelantarse con una **campaña de renovación** (masiva, por tandas, con pausa y reintento) o un **recordatorio en lote**, y ve todo lo que vence en **Vencimientos**.

> **Configuración:** [Vigencia de los tipos de documento](guia:configuracion-del-grupo#tipos-de-documento) y [vigencia del vínculo](guia:configuracion-de-la-empresa#datos-generales).
>
> **Recorridos:** [[documentos/vencimientos|Vencimientos]] · [[documentos/ciclo-de-vigencia|Ciclo de vigencia]] · [[documentos/vigencia-del-vinculo|Vigencia del vínculo]] · [[documentos/recordatorio-desde-vencimientos|Recordatorio desde vencimientos]] · [[tercero/documento-por-vencer-y-vencido|Documento por vencer y vencido]] · [[tercero/vinculo-por-vencer-renovar|Vínculo por vencer: renovar]] · [[operacion/campana-de-renovacion|Campaña de renovación]] · [[operacion/campana-pausar-y-reintentar|Pausar y reintentar una campaña]] · [[operacion/recordatorio-en-lote|Recordatorio en lote]] · [[tercero/campana-y-avisos|La campana y los avisos del tercero]]

### 12. Actualización con una versión nueva

Cuando el tercero sube una **versión nueva** de un documento (o cambia una sección de la ficha) que una empresa ya había aprobado, la versión se comparte con **cada vínculo aceptado cuyo flujo exige ese tipo**, y cada empresa **revisa solo lo que cambió**:

1. Se abre un expediente de **actualización** en cada empresa a la que aplica, salvo que el vínculo no haya sido aprobado, no exija lo actualizado, esté bloqueado, desvinculado o anulado.
2. El **documento** y el **vínculo** se ven **«En revisión»** (para la empresa y para el tercero) mientras ese expediente esté abierto, en revisión u observado; el `estado` del vínculo **no cambia**.
3. El **certificado** sigue vigente hasta su fecha y se ve **«En verificación»**; no se suspende. Si pasa el plazo de revalidación (15 días o el de la política del grupo), pasa a bloqueante y `bloqueado: true` para quien consulta.
4. El nivel de revisión de cada empresa decide si la actualización la aprueba el sistema o una persona; en Asistido y Automático el documento sigue «En revisión» hasta que sale por un veredicto.
5. **Aprobada:** el vínculo vuelve a `Activo` y el certificado se **reemite** (`certificado.reemplazado`). **Observada:** el tercero ve «Tienes observaciones». **Rechazada o anulada:** el vínculo conserva su estado, el certificado vuelve a Vigente, y el tercero y la empresa ven «Actualización no aprobada», con los motivos al tercero; lo que ya estaba aprobado sigue valiendo.

El tercero también puede **retirar** una versión: cada empresa que la tenía debe revalidar. Una actualización abierta se **amplía** con lo que cambia después.

> **Recorridos:** [[tercero/subir-version-nueva|Subir una versión nueva]] · [[tercero/vinculo-en-revision-por-actualizacion|Vínculo en revisión por una actualización]] · [[vinculos/vinculo-con-actualizacion-pendiente|Vínculo con actualización pendiente]] · [[revision/actualizacion-de-un-documento|Actualización de un documento]] · [[revision/revalidaciones-en-lote|Revalidaciones en lote]] · [[tercero/retirar-una-version|Retirar una versión]] · [[tercero/ficha-cambio-tras-aprobar|Un cambio de la ficha tras aprobar]]

### 13. Varias empresas: documentos compartidos, decisiones privadas

Un tercero es **uno solo** en toda la plataforma. Si lo invitan tres empresas, sube su RUT **una vez**: la versión vive en su bóveda y se comparte con cada vínculo aceptado cuyo flujo la exige. Cada empresa:

- lee **la misma versión** y los mismos hechos del análisis, pero **decide la suya**: aprueba, observa o rechaza en privado, con su propio flujo, sus áreas, su nivel de revisión y sus plazos;
- tiene sus propias **vigencias** (días de aviso y de gracia) y su propio **bloqueo**: lo que una empresa aprobó, observó o bloqueó no se ve en otra, ni siquiera entre las empresas de un mismo grupo (la ficha 360 solo dice que existe un vínculo y en qué estado);
- empieza a ver la bóveda solo cuando el tercero acepta **su** invitación.

El tercero ve en **Mis empresas** cada empresa y su avance, y en su bóveda quién recibió cada documento. Si una empresa no le exige un documento, no lo recibe.

> **Recorridos:** [[documentos/documento-compartido-entre-empresas|Documento compartido entre empresas]] · [[tercero/boveda-documento-compartido|Mi bóveda: documento compartido]] · [[tercero/mis-empresas-varias-invitaciones|Mis empresas con varias invitaciones]] · [[vinculos/tercero-360|Tercero 360]] · [[documentos/descargar-un-documento|Descargar un documento]]

### 14. Bloqueo, desbloqueo y terminación

- **Bloquear** (con un motivo y, si se quiere, una fecha de fin) suspende el vínculo **sin borrar su historial** y suspende su certificado; el tercero no ve el motivo, el bloqueo es privado de la empresa y los sistemas integrados reciben `vinculo.bloqueado` y `certificado.suspendido`. Un vínculo bloqueado **no recibe versiones nuevas**. **Desbloquear** (a mano o al llegar la fecha) restituye el estado previo y el certificado, y **revalida**: abre una actualización si lo aprobado difiere de lo actual.
- **Desvincular** termina la relación (anula el expediente abierto y revoca el certificado); se puede **reintegrar** con revalidación o, si estaba aprobado y vigente, **sin revalidar**.
- **Anular** es definitivo.
- Un administrador puede **revocar** un certificado con justificación, lo que es irreversible.

> **Recorridos:** [[vinculos/bloquear-y-desbloquear|Bloquear y desbloquear]] · [[vinculos/bloqueado-no-recibe-version|Bloqueado no recibe versiones]] · [[vinculos/desvincular-y-reintegrar-sin-revalidar|Desvincular y reintegrar sin revalidar]] · [[vinculos/reintegrar-revalidando|Reintegrar revalidando]] · [[vinculos/anular-vinculo|Anular un vínculo]] · [[tercero/documento-eximido-y-vinculo-bloqueado|Documento eximido y vínculo bloqueado]]

### 15. Webhooks emitidos en cada hito

Cada hito de arriba deja un evento público que llega por **webhook firmado** (o por pull o SSE), con la referencia externa del vínculo para que cada sistema ubique su registro. Los eventos nunca llevan nombres de revisores, correos ni IP. La tabla de qué hito produce qué evento, la firma y los reintentos están en la [guía de integración](guia:guia-de-integracion#webhooks-firmados); el catálogo de 42 tipos, en [Webhooks](guia:configuracion-del-grupo#webhooks).

> **Recorridos:** [[integracion/webhooks|Webhooks]] · [[integracion/webhook-entrega-fallida-y-reenvio|Entrega fallida y reenvío]] · [[integracion/eventos-pull-y-en-vivo|Eventos por pull y en vivo]] · [[integracion/sandbox|Sandbox]] · [[integracion/buzon-de-pruebas|Buzón de pruebas]]

## Variantes que cambian el recorrido

| Qué cambia | Efecto |
|---|---|
| Tipo de persona del tercero | El flujo pide otros documentos y secciones: la persona natural no tiene las secciones legal y financiera. Se decide con la condición «Se pide a» de cada requisito. |
| Flujo sin requisitos ni áreas manuales | El expediente se abre y se aprueba solo (`POST /vinculos/{id}/expedientes`). |
| Tipo de vínculo con código por correo | El enlace de la invitación, de las observaciones y de la renovación pide además un código de 6 dígitos. |
| Nivel de revisión de la empresa | Manual, Asistido o Automático: cambia quién aprueba cada documento. |
| IA sin plan, llave o cupo | El documento va a una persona; el tercero nunca queda bloqueado por la IA. |
| Política de devolución | Inmediata o al cerrar la etapa. |
| Verificaciones bloqueantes o no | Pueden o no impedir el cierre del expediente. |
| Tipo de vínculo con cuenta designada | Aunque todas las áreas aprueben, el expediente queda en revisión hasta que la empresa apruebe la cuenta que designa el tercero; entonces se aprueba solo. |
| Vigencia del vínculo | Sin vigencia (0) el vínculo no vence por tiempo; con ella hay pre-vencimiento y renovación. |
| Entorno | En el sandbox los correos no salen, la IA y las listas son simuladas y los certificados no se verifican en público. |
| Integración | Un sistema integrado crea vínculos por API y los ve por webhooks; sin él, todo se hace desde la app. |
