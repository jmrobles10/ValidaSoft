---
titulo: "Del vínculo a la validación, de punta a punta"
seccion: "Flujo de punta a punta"
resumen: "El camino completo: la empresa se configura y crea el vínculo, el tercero acepta y completa su expediente, las áreas y el oficial revisan, se emite el certificado y luego vienen las vigencias, las actualizaciones, los bloqueos y los webhooks de cada hito."
---

# Del vínculo a la validación, de punta a punta

El camino completo: la empresa se configura y crea el vínculo, el tercero acepta y completa su expediente, las áreas y el oficial revisan, se emite el certificado y luego vienen las vigencias, las actualizaciones, los bloqueos y los webhooks de cada hito.

Esta guía recorre, en orden, todo lo que pasa entre una empresa y un tercero, desde que la empresa se configura hasta que el vínculo vence, se actualiza o se bloquea. Cada etapa dice **quién hace qué**, **qué reglas aplican**, **qué estados y eventos produce** y **qué ajuste la controla**, y termina con los recorridos del manual que la muestran con pantallazos. Los recorridos que aún no existen aparecen subrayados con guiones: son los que faltan por documentar (la lista completa está en `e2e/manual/INVENTARIO.md`).

El mapa de estados de cada pieza está en [Ciclos de vida](arquitectura-ciclos-de-vida.md) y el vocabulario en el [Glosario](glosario.md).

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

> **Configuración:** [Configuración de la empresa](configuracion-de-la-empresa.md#flujos-aprobatorios), [del grupo](configuracion-del-grupo.md) y [de Plataforma](configuracion-de-plataforma.md).
>
> **Recorridos:** [Crea un grupo con su primera empresa](../plataforma/crear-grupo-y-empresa.md) · [Configura el plan de IA de una empresa y su llave de OpenAI](../plataforma/plan-de-ia-y-llave-openai.md) · [Agrega una empresa del grupo y publica sus textos legales](../configuracion/empresas-y-textos-legales.md) · [Configura un tipo de documento y publica un flujo que lo pide](../configuracion/tipo-de-documento-y-flujo.md) · [Crea un tipo de vínculo y lo desactiva](../configuracion/tipos-de-vinculo.md) · [Crea un área, asígnale personas y desactívala](../configuracion/areas-y-miembros.md) · [Arma una etapa con dos áreas en paralelo y una automática](../configuracion/flujo-etapas-y-areas-en-paralelo.md) · [Pide cada documento según el tipo de persona](../configuracion/flujo-condiciones-por-tipo-de-persona.md) · [Pide secciones de «Mi información» como requisito del flujo](../configuracion/flujo-secciones-de-la-ficha.md) · [Fija el plazo de cargue, los recordatorios y el reintento](../configuracion/flujo-plazo-reintento-y-recordatorios.md) · [Define la vigencia periódica del vínculo](../configuracion/flujo-vigencia-del-vinculo.md) · [Pide listas restrictivas y el estudio SAGRILAFT en el flujo](../configuracion/flujo-verificaciones-de-cumplimiento.md) · [Cambia un flujo publicado con un borrador nuevo](../configuracion/flujo-versiones-y-borradores.md) · [Cambia el flujo por defecto de un tipo de vínculo](../configuracion/flujo-por-defecto.md) · [Agrega una persona, dale roles y desactívala](../configuracion/usuarios-y-roles.md)

### 2. Se crea el vínculo

Un **vínculo** es la relación de una empresa con un tercero en un tipo. Se crea de dos maneras:

- **Desde la app**, en **Vínculos**, con **Invitar por lote** (una fila basta): se sube la plantilla con los terceros, se revisa la vista previa y se invita; las filas con errores o repetidas no se procesan.
- **Desde un sistema integrado**, con `POST /api/v1/vinculos` (con `X-Api-Key`, `X-Empresa` y `Idempotency-Key`): el cuerpo lleva la identificación del tercero, su tipo de persona y nombre, el tipo de vínculo, el correo de invitación y la **referencia externa** del sistema. Es idempotente: reintentar no duplica.

Validasoft **reconcilia al tercero por su identificación** (país, tipo y número normalizados): si ya existe en la plataforma, lo reutiliza sin tocar sus datos, y con él su bóveda. El vínculo nace `Invitado`, único por empresa, tercero y tipo, y se envía el correo con el **enlace de acceso**: de un solo uso y con una vigencia de 72 horas por omisión (de 1 hora a 30 días). La empresa puede **reenviar** la invitación. Si el plan del grupo está en su límite de vínculos con bloqueo, la creación responde 409.

También existe el **autorregistro** (`POST /public/v1/autorregistro`: el propio tercero se anota) y la **importación** de lo que ya existía en el Portal.

> **Configuración:** [Tipos de vínculo](configuracion-del-grupo.md#tipos-de-vinculo), [Plan del grupo](configuracion-del-grupo.md#plan-del-grupo) y [guía de integración](guia-de-integracion.md#invitar-a-un-tercero).
>
> **Recorridos:** [Invita a varios terceros con un archivo](../operacion/invitacion-por-lotes.md) · [Pausa, reanuda y cancela un lote, y reintenta sus filas fallidas](../operacion/lote-pausar-reanudar-cancelar.md) · [Invita a un proveedor desde tu sistema con la API](../integracion/invitar-por-api.md) · [Registra un sistema integrado con su clave pública](../integracion/sistema-integrador.md) · [Crea una API key con sus permisos y empresas, rótala y revócala](../integracion/api-keys.md) · [Revisa la lista de vínculos y filtra por estado y tipo](../vinculos/lista-y-filtros.md) · [Revisa el detalle de un vínculo](../vinculos/detalle-del-vinculo.md) · [Reenvía la invitación a un tercero](../vinculos/reenviar-invitacion.md) · [Abre el expediente sin esperar a que el tercero acepte](../vinculos/abrir-expediente-manual.md)

### 3. Invitación y aceptación

El tercero abre el enlace del correo (`/e/{token}`). El enlace se canjea **una sola vez** (si ya se usó o venció, la pantalla lo dice y explica cómo recibir otro). Si el tipo de vínculo lo exige, le llega al mismo correo un **código de 6 dígitos** (vale 10 minutos, 5 intentos). Entra a **Mis empresas**, donde ve cada empresa que lo invitó, y abre la invitación:

1. Lee y acepta los **textos legales**: el aviso de datos de Validasoft (solo si aún no lo aceptó) y la autorización y los términos de **esa empresa**. Cada texto tiene su versión; si cambió mientras lo leía, se le muestra la nueva.
2. Al aceptar se guarda como constancia la versión y el **hash** de cada texto, la fecha, la IP y el dispositivo, y si verificó el correo con el código.
3. Se publica `ConsentimientosRegistrados` e `InvitacionAceptada` en el vínculo y **se abre el expediente inicial** con el flujo del vínculo o, si no tiene uno propio, el flujo por defecto de su tipo. El vínculo pasa a `EnValidacion`.
4. Solo desde ese momento la empresa puede ver algo de la bóveda: se **comparten** con ella las versiones vigentes que el flujo exige y que el tercero ya tenía (por ejemplo un RUT que subió para otra empresa). Antes de aceptar, la empresa solo sabe cuántos requisitos podrían quedar cubiertos.

Un tercero **sin invitación a mano** puede entrar a `/entrar` con su correo y pedir un enlace de acceso nuevo (vale 1 hora).

> **Variantes:** según el tipo de vínculo hay código por correo o no; según el tipo de persona (jurídica o natural) el flujo pide otros documentos y secciones; si la empresa no ha publicado sus textos, la pantalla dice «aún no publica sus textos» y no se puede aceptar.
>
> **Recorridos:** [Acepta la invitación y completa tu información como persona jurídica](../tercero/aceptar-invitacion-persona-juridica.md) · [Acepta la invitación y completa tu información como persona natural](../tercero/aceptar-invitacion-persona-natural.md) · [Entra a Validasoft con tu correo cuando ya tienes una invitación](../tercero/entrar-sin-invitacion.md) · [Gestiona tus empresas cuando varias te invitaron](../tercero/mis-empresas-varias-invitaciones.md) · [Qué pasa cuando un enlace de invitación ya se usó, venció o no existe](../tercero/invitacion-vencida-o-usada.md) · [Lee y acepta los textos legales de una empresa](../privacidad/lo-que-acepte-y-textos-nuevos.md) · [Entra al portal desde el celular con tu correo y el código](../movil/tercero-entrar.md)

### 4. El asistente del tercero

La pantalla **Tu revisión con {empresa}** lista lo que falta: cada **documento** que el flujo pide y cada **sección** de la ficha («Mi información»). El tercero:

- **Sube documentos**: elige el archivo (PDF, JPG, PNG o WebP de hasta 20 MB y 50 páginas), el navegador lo sube directo al almacén y el servidor lo verifica ([Almacenamiento de documentos](arquitectura-documentos.md)). Un tipo puede ser de **solo formulario** (datos sin archivo), tener una **plantilla** para descargar, llenar, firmar y subir, y una **ayuda** escrita por la empresa. Cada documento nuevo es una versión; la anterior queda superada.
- **Llena su ficha** por secciones (información general, tributaria, legal, financiera por periodo, SST y especialidades), con sus soportes, sus personas relacionadas y, si aplica, la **declaración PEP**. Puede proponerse un dato desde un documento («Actualizar desde mi RUT») y se cruza cada sección con sus soportes. La barra siempre visible muestra lo que falta y avisa antes de salir sin guardar.
- Elige su **cuenta bancaria designada** y sus **contactos** por empresa.

El expediente **pasa solo a revisión** cuando todos los requisitos obligatorios están cargados (o enseguida si el flujo no pide requisitos): se publica `ExpedienteEnviadoARevision`, se activa la primera etapa y entra a la bandeja de sus áreas. Lo ya aprobado antes por la empresa para esa misma versión se aprueba al instante.

> **Variantes:** persona natural: no ve las secciones legal ni financiera; un requisito puede ser opcional, y los opcionales no impiden pasar a revisión; en celular las pantallas del tercero se adaptan (390 px).
>
> **Configuración:** [Tipos de documento](configuracion-del-grupo.md#tipos-de-documento), [Ayuda de documentos](configuracion-de-la-empresa.md#ayuda-de-documentos) y el [flujo](configuracion-de-la-empresa.md#documentos-y-secciones-que-pide).
>
> **Recorridos:** [Revisa tu bóveda de documentos](../tercero/boveda-mis-documentos.md) · [Sube un documento a tu bóveda](../tercero/boveda-subir-un-documento.md) · [Sube un documento que pide una fecha de expedición](../tercero/subir-documento-con-formulario.md) · [Respalda tu información con un documento de tu bóveda](../tercero/ficha-secciones-con-soporte.md) · [Actualiza tu información desde un RUT nuevo](../tercero/ficha-actualizar-desde-mi-rut.md) · [Completa tu información paso a paso](../tercero/ficha-barra-y-pendientes.md) · [Declara que eres una persona expuesta políticamente](../tercero/declaracion-pep-persona-natural.md) · [Declara la PEP de un socio y de sus personas relacionadas](../tercero/declaracion-pep-personas-relacionadas.md) · [Designa la cuenta en la que te paga una empresa](../tercero/cuentas-bancarias.md) · [Indica con quién hablar en cada empresa](../tercero/contactos-por-empresa.md) · [Escribe la ayuda que ve el tercero al subir un documento](../operacion/ayuda-de-documentos.md) · [Crea un documento con formulario y plantilla descargable](../configuracion/tipo-de-documento-solo-formulario-y-plantilla.md) · [Sube un documento pendiente desde el celular](../movil/tercero-subir-documento.md) · [Completa una sección de Mi información desde el celular](../movil/tercero-mi-informacion.md)

### 5. Revisión automática con IA

Cada versión nueva pasa por los **hechos básicos** (hash, tipo real, páginas) y, si hay IA disponible, se **analiza una sola vez** con OpenAI (cola `ia`): se extraen campos, se detecta el tipo, la legibilidad y las señales de alteración, y se compara con la versión anterior. Paga una de las empresas con las que está compartida que tenga **plan activo, llave y cupo**; si no hay quien pague, o la IA está deshabilitada, la versión sigue a revisión humana. Un segundo análisis con un modelo mayor ocurre si la confianza queda bajo el umbral, el documento es ilegible, hay alertas o cambia un campo crítico.

Cada empresa decide **cuánto confía** en ese análisis con su **nivel de revisión**:

| Nivel | Qué hace el sistema |
|---|---|
| **Manual** | Solo informa: los revisores ven los hechos del análisis y deciden todo. |
| **Asistido** | Sugiere «aprobar» o «revisar» con motivos en cada documento; nunca emite el veredicto. |
| **Automático** | Aprueba solo lo que no tiene excepciones (confianza sobre el umbral, tipos y áreas autorizados, sin alertas); un porcentaje de lo aprobado se muestra a un miembro del área para confirmarlo o revertirlo. Lo demás va a **Excepciones**. |

Los veredictos del sistema llevan origen `Sistema`, su confianza y la justificación. Sin cupo, el documento va de inmediato a una persona; con la IA apagada, todo es Manual. Una versión **reenviada** tras una observación siempre pasa por una persona.

> **Configuración:** [Revisión automática](configuracion-de-la-empresa.md#revision-automatica) y [Plan de IA de una empresa](configuracion-de-plataforma.md#plan-de-ia-de-una-empresa).
>
> **Recorridos:** [Revisa el plan y el uso de IA de la empresa](../ia/plan-y-uso-de-ia.md) · [Elige el nivel de revisión automática con IA](../ia/revision-automatica-niveles.md) · [Atiende una excepción de la revisión asistida](../ia/revision-asistida-y-excepciones.md) · [Revisa un documento que el sistema aprobó solo](../ia/revision-automatica-aprueba-sola.md) · [Atiende los documentos cuando se agota el cupo de IA](../ia/cupo-agotado.md) · [Revisa una lectura dudosa que la IA escaló a un modelo superior](../ia/escalamiento-a-modelo-superior.md) · [Compara la información del tercero con su RUT](../ia/cruce-de-la-ficha-con-los-soportes.md) · [Lee el análisis automático de un documento](../ia/analisis-de-un-documento.md)

### 6. Revisión por áreas y etapas

El flujo define **etapas en secuencia**; las **áreas** de una misma etapa revisan **en paralelo**, cada una con su número de **aprobadores** (personas distintas). Un revisor ve en **Inicio** lo que le toca y en la **Bandeja** los expedientes de las áreas de las que es miembro. Abre el expediente y, para cada documento o sección de su área, ve la versión (con su visor y los datos del análisis), los veredictos de las otras áreas y el historial. Puede:

- **aprobar** el requisito o toda el área;
- **observar** con motivos (ver la etapa 7);
- **eximir** un documento, si la empresa no lo necesita para ese tercero.

Un área aprueba cuando todos sus requisitos y todos sus aprobadores aprobaron; una etapa, cuando todas sus áreas lo hicieron; solo entonces **empieza la siguiente**. Reglas que aplican: la **separación de funciones** (quien invitó o abrió el expediente no decide, si el flujo la exige), el **conflicto de intereses** (siempre), la **delegación** por ausencias, las **áreas automáticas** (aprueban solas al activarse la etapa) y el **plazo de revisión** (SLA) de cada área, que avisa al consumir un porcentaje y al incumplirse. Un administrador puede **aprobar en nombre del área** con una justificación, **anular un veredicto** o **desaprobar** lo ya aprobado.

> **Configuración:** [Áreas](configuracion-de-la-empresa.md#areas), [Plazos de revisión](configuracion-de-la-empresa.md#plazos-de-revision) y [Delegación de revisión](configuracion-de-la-empresa.md#delegacion-de-revision).
>
> **Recorridos:** [Revisa lo que te toca desde Inicio](../revision/inicio-lo-que-te-toca.md) · [Filtra la bandeja de revisión y abre un expediente](../revision/bandeja-de-revision.md) · [Aprueba el área de Compras](../revision/aprobar-un-area.md) · [Aprueba el expediente documento por documento](../revision/aprobar-documento-por-documento.md) · [Sigue un expediente que pasa por tres etapas](../revision/etapas-en-secuencia.md) · [Aprueba un área que pide dos personas distintas](../revision/dos-aprobadores-y-separacion-de-funciones.md) · [Aprueba un área en nombre de sus miembros](../revision/aprobar-en-nombre-del-area.md) · [Exime a un tercero de un documento](../revision/eximir-un-documento.md) · [Abre un documento para verlo y descargarlo](../revision/visor-y-datos-de-un-documento.md) · [Lee el historial de un expediente](../revision/historial-del-expediente.md) · [Delega tu revisión mientras estás ausente](../operacion/delegacion-de-revision.md) · [Fija el plazo de revisión de cada área](../operacion/plazos-de-revision.md) · [Mide el cumplimiento de los plazos de revisión](../informes/plazos-de-revision.md)

### 7. Observaciones y reenvío

Cuando un área **observa**, elige uno o varios **motivos** del catálogo (los de la plataforma y los propios) por requisito, con un texto dirigido al tercero que **nunca nombra al revisor**. El expediente pasa a `Observado`:

- con la política **Inmediata**, al instante, y frena a las demás áreas; con **AlCerrarArea**, cuando todas las áreas de la etapa se pronunciaron;
- el tercero recibe un **correo** con un enlace de acceso (vale 72 horas) y una notificación en su **campana**; en su pantalla ve «Tienes observaciones» con los motivos por documento;
- **corrige** subiendo una versión nueva (o editando su ficha) y pulsa **Enviar respuesta**: solo los requisitos observados vuelven a revisión, y reenviar sin corregirlos responde 409 `requisitos-sin-corregir`;
- si la empresa lo configuró, un **recordatorio automático** le llega cada cierto número de días mientras el expediente siga en sus manos;
- el **plazo de cargue** (30 días por omisión) corre para completar el expediente inicial y para cada devolución; avisa 15, 7 y 1 días antes y, al vencer, el expediente pasa a `Caducado`: la empresa puede **reabrirlo** con un plazo nuevo.

Un **rechazo** es distinto: lo hace un área que puede rechazar (o el oficial, ante un hallazgo ya registrado) con motivos y justificación; es terminal para ese expediente. Si el flujo lo permite, el tercero ve los motivos y puede **volver a postularse** tras la espera configurada; si no, solo la empresa abre un expediente nuevo.

> **Configuración:** [Motivos](configuracion-del-grupo.md#motivos), [Plantillas de correo](configuracion-de-la-empresa.md#plantillas-de-correo) y [datos generales del flujo](configuracion-de-la-empresa.md#datos-generales).
>
> **Recorridos:** [Observa un documento con motivos](../revision/observar-con-motivos.md) · [Corrige lo que la empresa observó y responde](../tercero/responder-observaciones.md) · [Revisa lo que corrigió el proveedor tras una observación](../revision/responder-con-lo-corregido.md) · [Recuerda al proveedor sus observaciones pendientes](../revision/recordatorio-de-observaciones.md) · [Activa el recordatorio automático de observaciones](../operacion/recordatorio-automatico-por-flujo.md) · [Atiende un expediente caducado por falta de plazo y reábrelo](../operacion/plazo-de-cargue-y-caducidad.md) · [Rechaza un expediente por un hallazgo grave](../revision/rechazar-expediente.md) · [Vuelve a postularte después de un rechazo](../tercero/rechazo-y-volver-a-postularme.md)

### 8. Verificaciones de cumplimiento

Si el flujo las pide, al pasar a revisión se **consultan solas las listas restrictivas** (al tercero y a sus personas relacionadas: representantes, suplentes, socios y PEP) con el proveedor de la empresa. Cada consulta guarda su evidencia en PDF. Si hay **coincidencias**, la verificación queda `CoincidenciaPorAnalizar` y el **oficial de cumplimiento** las analiza en su **bandeja**: las **descarta** o las **confirma** (una confirmada deja la verificación `Fallida`). El **estudio de cumplimiento** (SAGRILAFT) lo registra el oficial: nivel de riesgo (calculado con la matriz de riesgo si la empresa la activó), conflicto de interés, debida diligencia y concepto favorable o desfavorable, con adjuntos; vence y vuelve a pedirse. Los vínculos activos se **reconsultan** periódicamente (monitoreo).

Una verificación marcada **bloqueante** (por omisión lo es) impide aprobar el expediente mientras esté pendiente, con coincidencias por analizar, fallida o vencida, aunque todas las áreas hayan aprobado. El oficial también puede **rechazar directamente** ante un hallazgo ya registrado.

> **Configuración:** [Listas restrictivas](configuracion-de-la-empresa.md#listas-restrictivas), [Matriz de riesgo](configuracion-de-la-empresa.md#matriz-de-riesgo) y [verificaciones del flujo](configuracion-de-la-empresa.md#verificaciones-de-cumplimiento).
>
> **Recorridos:** [Elige con qué proveedor se consultan las listas restrictivas](../cumplimiento/listas-restrictivas-de-la-empresa.md) · [Revisa las listas restrictivas consultadas al pasar a revisión](../cumplimiento/pedir-listas-restrictivas.md) · [Analiza una coincidencia en las listas y la descarta por homónimo](../cumplimiento/analizar-una-coincidencia.md) · [Confirma una coincidencia y rechaza el vínculo](../cumplimiento/coincidencia-confirmada.md) · [Registra el estudio de cumplimiento de un proveedor](../cumplimiento/estudio-de-cumplimiento.md) · [Renueva un estudio de cumplimiento vencido](../cumplimiento/estudio-vencido-y-renovacion.md) · [Atiende lo pendiente de Cumplimiento desde su bandeja](../cumplimiento/bandeja-del-oficial.md) · [Rechaza el expediente por cumplimiento](../cumplimiento/rechazo-directo-del-oficial.md) · [Configura la matriz de riesgo SAGRILAFT de la empresa](../cumplimiento/matriz-de-riesgo-configuracion.md) · [Sigue el monitoreo de listas de un vínculo activo](../cumplimiento/monitoreo-periodico.md) · [Consulta el informe SAGRILAFT de los terceros](../cumplimiento/informe-sagrilaft.md)

### 9. Aprobación

Cuando **todas las etapas están aprobadas, las verificaciones bloqueantes están satisfechas y vigentes y, si el tipo de vínculo exige cuenta bancaria designada, la cuenta del vínculo está aprobada**, el expediente pasa a `Aprobado` y, **en la misma transacción**:

- el vínculo pasa a `Activo`;
- las secciones de la ficha quedan **aceptadas por la empresa** (evento `seccion.aceptada`), que es lo que un ERP lee como la ficha vigente;
- se **emite el certificado** (etapa 10);
- el tercero recibe un correo y una notificación de resultado, y se publican los eventos `expediente.aprobado`, `vinculo.activado` y `certificado.emitido`.

Si el tipo de vínculo exige cuenta bancaria designada (el PROVEEDOR sembrado la exige) y la cuenta aún no está aprobada, el expediente **espera en `EnRevision`** aunque todas las áreas hayan aprobado: la empresa ve el aviso «Falta la cuenta bancaria designada» (con acceso a la cuenta del vínculo) y el tercero ve «Falta tu cuenta bancaria designada» (con acceso a «Mis cuentas»). Cuando dos personas de la empresa aprueban la cuenta, el expediente vuelve a evaluarse **en la misma transacción** y, si nada más falta, se aprueba solo. Rechazar la cuenta no rechaza el expediente: sigue esperando a que el tercero designe otra.

> **Recorridos:** [Aprueba la cuenta bancaria designada de un proveedor](../vinculos/cuenta-designada-aprobar.md) · [Designa la cuenta en la que te paga una empresa](../tercero/cuentas-bancarias.md)

Si algo de lo aprobado se ve mal más tarde, un administrador puede **desaprobar** el último expediente aprobado (el mismo vuelve a revisión y el vínculo a `EnValidacion`) o reabrir una etapa.

> **Recorridos:** [Ve que tu expediente quedó aprobado](../tercero/expediente-aprobado.md) · [Reabre una etapa ya aprobada](../revision/desaprobar-y-reabrir-una-etapa.md) · [Desaprueba un expediente aprobado](../revision/desaprobar-un-expediente-aprobado.md) · [Anula el veredicto de un área](../revision/anular-un-veredicto.md)

## Parte 2: del certificado a las vigencias

![Diagrama por actor: del certificado a las vigencias, la actualización y el bloqueo](diagramas/flujo-b.svg)

### 10. Certificado y verificación pública

El certificado de un vínculo aprobado lleva el folio `{PREFIJO}-C-{año}-{secuencia}-{emisión}`, el titular, el emisor, el flujo aprobado, las fechas, el **hash** del contenido canónico y un **sello Ed25519** de la plataforma. El **PDF** lo genera un proceso en segundo plano (con el folio, la URL de verificación, el hash, el sello y un QR). La empresa lo descarga; la empresa puede además **revocar** un certificado con justificación, y el bloqueo de un vínculo lo suspende.

Cualquiera, sin sesión, lo **verifica** en `/verificar/{folio}` (o por `GET /public/v1/verificar/{folio}`, con la `huella` si el folio existe en más de un grupo): ve su estado, su vigencia y qué se aprobó, y si está **Vigente** o «En verificación» puede descargar el PDF. La página **comprueba en el navegador** que el certificado es auténtico: recalcula el hash del contenido canónico y verifica el sello Ed25519 con las claves públicas de `/.well-known/validasoft-certificados.json`, así no depende solo de lo que diga el servidor. El QR y la URL del PDF abren esta página con la huella. Un certificado revocado, vencido, suspendido o reemplazado se muestra como tal, sin PDF.

Un **sistema integrador** se entera con `certificado.emitido` y, antes de pagar, **consulta la certificación**: paga solo si `certificado: true` y `bloqueado: false` ([guía de integración](guia-de-integracion.md#consulta-de-certificacion)).

> **Recorridos:** [Encuentra los certificados de la empresa](../informes/certificados.md) · [Bloquea, desbloquea o revoca un certificado](../informes/certificado-bloquear-y-revocar.md) · [Verifica que un certificado es auténtico](../publico/verificar-certificado-autentico.md) · [Comprueba un certificado que no existe o tiene una huella mal escrita](../publico/verificar-certificado-invalido.md) · [Lee el estado de un certificado suspendido o revocado](../publico/certificado-en-otros-estados.md) · [Compara la huella impresa en el PDF con la del certificado](../publico/verificar-con-el-pdf.md) · [Verifica un certificado desde el celular](../movil/verificar-certificado.md) · [Consulta la certificación de un proveedor con ETag](../integracion/consulta-de-certificacion.md)

### 11. Vigencias, avisos y renovación

Hay **dos capas** de vigencia y las dos las ejecuta el propio sistema, sin que nadie dispare nada:

- **Del documento** (por empresa y vínculo): cada tipo de documento dice cuánto sirve (desde la expedición o desde la carga, o la fecha que el documento declara). Llega un **aviso** a los días configurados (30, 15 y 5 por omisión; a las 08:00 de la zona horaria configurada, 13:00 UTC en Colombia), luego una **gracia** (15 días) y después el documento **vence**: `documento.por_vencer` y `documento.vencido`. El tercero ve un aviso consolidado y puede **subir una versión nueva** o **ratificar**.
- **Del vínculo**: si el flujo tiene vigencia (días desde la última aprobación), el primer aviso deja el vínculo `PorVencer` y **abre la renovación** del tercero: ratificar o actualizar las secciones que pide el flujo; al aprobarse, el vínculo vuelve a `Activo` y se reemite el certificado. Si se cumple la fecha más la gracia, queda `Vencido` (`vinculo.vencido`) con su renovación abierta, y el certificado vence. Un documento obligatorio vencido también vence el vínculo.

La empresa puede adelantarse con una **campaña de renovación** (masiva, por tandas, con pausa y reintento) o un **recordatorio en lote**, y ve todo lo que vence en **Vencimientos**.

> **Configuración:** [Vigencia de los tipos de documento](configuracion-del-grupo.md#tipos-de-documento) y [vigencia del vínculo](configuracion-de-la-empresa.md#datos-generales).
>
> **Recorridos:** [Revisa los documentos que vencen en la lista de vencimientos](../documentos/vencimientos.md) · [Sigue el ciclo de vigencia de un certificado](../documentos/ciclo-de-vigencia.md) · [Sigue la vigencia periódica de un vínculo](../documentos/vigencia-del-vinculo.md) · [Recuerda a un tercero un documento que vence](../documentos/recordatorio-desde-vencimientos.md) · [Atiende un documento que está por vencer](../tercero/documento-por-vencer-y-vencido.md) · [Ratifica tu información antes de que venza el vínculo](../tercero/vinculo-por-vencer-renovar.md) · [Abre la renovación de varios clientes a la vez](../operacion/campana-de-renovacion.md) · *Pausar y reintentar una campaña (recorrido pendiente)* · [Recuerda a varios terceros sus observaciones desde la bandeja](../operacion/recordatorio-en-lote.md) · [Entérate de los avisos desde la campana](../tercero/campana-y-avisos.md)

### 12. Actualización con una versión nueva

Cuando el tercero sube una **versión nueva** de un documento (o cambia una sección de la ficha) que una empresa ya había aprobado, la versión se comparte con **cada vínculo aceptado cuyo flujo exige ese tipo**, y cada empresa **revisa solo lo que cambió**:

1. Se abre un expediente de **actualización** en cada empresa a la que aplica, salvo que el vínculo no haya sido aprobado, no exija lo actualizado, esté bloqueado, desvinculado o anulado.
2. El **documento** y el **vínculo** se ven **«En revisión»** (para la empresa y para el tercero) mientras ese expediente esté abierto, en revisión u observado; el `estado` del vínculo **no cambia**.
3. El **certificado** sigue vigente hasta su fecha y se ve **«En verificación»**; no se suspende. Si pasa el plazo de revalidación (15 días o el de la política del grupo), pasa a bloqueante y `bloqueado: true` para quien consulta.
4. El nivel de revisión de cada empresa decide si la actualización la aprueba el sistema o una persona; en Asistido y Automático el documento sigue «En revisión» hasta que sale por un veredicto.
5. **Aprobada:** el vínculo vuelve a `Activo` y el certificado se **reemite** (`certificado.reemplazado`). **Observada:** el tercero ve «Tienes observaciones». **Rechazada o anulada:** el vínculo conserva su estado, el certificado vuelve a Vigente, y el tercero y la empresa ven «Actualización no aprobada», con los motivos al tercero; lo que ya estaba aprobado sigue valiendo.

El tercero también puede **retirar** una versión: cada empresa que la tenía debe revalidar. Una actualización abierta se **amplía** con lo que cambia después.

> **Recorridos:** [Sube una versión nueva de un documento ya aprobado](../tercero/subir-version-nueva.md) · [Mira tu vínculo cuando una actualización está en revisión](../tercero/vinculo-en-revision-por-actualizacion.md) · [Ve un vínculo con una actualización en revisión](../vinculos/vinculo-con-actualizacion-pendiente.md) · [Revisa una versión nueva que llega mientras decides](../revision/actualizacion-de-un-documento.md) · [Aprueba en lote las revalidaciones sin cambios](../revision/revalidaciones-en-lote.md) · [Retira una versión que subiste por error](../tercero/retirar-una-version.md) · [Cambia un dato de tu ficha después de la aprobación](../tercero/ficha-cambio-tras-aprobar.md)

### 13. Varias empresas: documentos compartidos, decisiones privadas

Un tercero es **uno solo** en toda la plataforma. Si lo invitan tres empresas, sube su RUT **una vez**: la versión vive en su bóveda y se comparte con cada vínculo aceptado cuyo flujo la exige. Cada empresa:

- lee **la misma versión** y los mismos hechos del análisis, pero **decide la suya**: aprueba, observa o rechaza en privado, con su propio flujo, sus áreas, su nivel de revisión y sus plazos;
- tiene sus propias **vigencias** (días de aviso y de gracia) y su propio **bloqueo**: lo que una empresa aprobó, observó o bloqueó no se ve en otra, ni siquiera entre las empresas de un mismo grupo (la ficha 360 solo dice que existe un vínculo y en qué estado);
- empieza a ver la bóveda solo cuando el tercero acepta **su** invitación.

El tercero ve en **Mis empresas** cada empresa y su avance, y en su bóveda quién recibió cada documento. Si una empresa no le exige un documento, no lo recibe.

> **Recorridos:** [Revisa una versión nueva de un documento que comparten dos empresas](../documentos/documento-compartido-entre-empresas.md) · [Entiende un documento que comparten dos empresas](../tercero/boveda-documento-compartido.md) · [Gestiona tus empresas cuando varias te invitaron](../tercero/mis-empresas-varias-invitaciones.md) · [Abre la ficha de un tercero que trabaja con dos empresas del grupo](../vinculos/tercero-360.md) · [Descarga un documento del proveedor y comprueba su registro](../documentos/descargar-un-documento.md)

### 14. Bloqueo, desbloqueo y terminación

- **Bloquear** (con un motivo y, si se quiere, una fecha de fin) suspende el vínculo **sin borrar su historial** y suspende su certificado; el tercero no ve el motivo, el bloqueo es privado de la empresa y los sistemas integrados reciben `vinculo.bloqueado` y `certificado.suspendido`. Un vínculo bloqueado **no recibe versiones nuevas**. **Desbloquear** (a mano o al llegar la fecha) restituye el estado previo y el certificado, y **revalida**: abre una actualización si lo aprobado difiere de lo actual.
- **Desvincular** termina la relación (anula el expediente abierto y revoca el certificado); se puede **reintegrar** con revalidación o, si estaba aprobado y vigente, **sin revalidar**.
- **Anular** es definitivo.
- Un administrador puede **revocar** un certificado con justificación, lo que es irreversible.

> **Recorridos:** [Bloquea un vínculo y luego lo desbloquea](../vinculos/bloquear-y-desbloquear.md) · [Bloquea un vínculo y lo desbloquea con una versión nueva pendiente](../vinculos/bloqueado-no-recibe-version.md) · [Desvincula un tercero y lo reintegra sin revalidar](../vinculos/desvincular-y-reintegrar-sin-revalidar.md) · [Reintegra un vínculo revalidando](../vinculos/reintegrar-revalidando.md) · [Anula un vínculo creado por error](../vinculos/anular-vinculo.md) · [Ve un documento eximido y un vínculo suspendido](../tercero/documento-eximido-y-vinculo-bloqueado.md)

### 15. Webhooks emitidos en cada hito

Cada hito de arriba deja un evento público que llega por **webhook firmado** (o por pull o SSE), con la referencia externa del vínculo para que cada sistema ubique su registro. Los eventos nunca llevan nombres de revisores, correos ni IP. La tabla de qué hito produce qué evento, la firma y los reintentos están en la [guía de integración](guia-de-integracion.md#webhooks-firmados); el catálogo de 42 tipos, en [Webhooks](configuracion-del-grupo.md#webhooks).

> **Recorridos:** [Crea un webhook, guarda su secreto y revisa las entregas firmadas](../integracion/webhooks.md) · [Revisa una entrega fallida y la reenvía cuando el sistema vuelve](../integracion/webhook-entrega-fallida-y-reenvio.md) · [Lee los eventos por pull o en vivo con un cursor](../integracion/eventos-pull-y-en-vivo.md) · [Prueba la integración en el sandbox antes de producción](../integracion/sandbox.md) · [Lee y vacía el buzón de correo de pruebas](../integracion/buzon-de-pruebas.md)

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
