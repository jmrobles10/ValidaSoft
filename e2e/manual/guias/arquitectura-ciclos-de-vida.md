---
titulo: "Ciclos de vida: documento, vínculo, expediente y certificado"
seccion: "Arquitectura"
orden: 16
resumen: "Los estados de cada cosa y sus transiciones, dibujados y explicados: qué las dispara, qué regla las limita y qué evento o webhook producen."
---

Cada agregado de Validasoft es una máquina de estados pequeña y explícita: el estado es un texto, cada transición la produce un método del dominio y deja un evento. Las transiciones que el dominio no permite responden 409 con un `codigo` estable. Esta guía reúne los cuatro ciclos; el vocabulario está en el [Glosario](guia:glosario).

## Documento

![Ciclo de vida de un documento: los tres pasos de la subida, los estados de una versión y los de su vigencia](diagramas/ciclo-documento.svg)

Los estados son de una **versión**; un documento tiene una versión **Actual** a la vez.

| De | A | Qué lo dispara | Regla |
|---|---|---|---|
| (carga) | Recibida → En análisis | El tercero confirma la subida. | Se verifica el SHA-256 declarado, el tipo real y las páginas. Con el mismo SHA-256 no hay versión nueva. |
| En análisis | Actual | Los hechos básicos están bien. | Con `ExigirAntivirus` espera hasta que un antivirus la declare limpia. La anterior pasa a Superada. |
| En análisis | Devuelta | PDF con contenido activo, más páginas que las del tipo o antivirus que la detecta. | Va a `cuarentena/` y nunca se comparte; el tercero ve el motivo. |
| Actual | Superada | Llega otra versión que pasa a Actual. | La superada sigue siendo la versión que alguna empresa aprobó. |
| Actual | Vencida | Pasó `vigenteHasta` más los días de gracia **para todos** los vínculos con vigencia. | Sigue siendo la actual; renovar o ratificar la devuelve a Actual. |
| Actual, Superada o Vencida | Retirada | El tercero la retira con un motivo. | Si era la actual, la superada más reciente la sustituye; cada empresa que la había aprobado debe revalidar. |

La **vigencia** se calcula por empresa y vínculo: Vigente, Por vencer (llegó un día de aviso), En gracia (pasó la fecha) y Vencida (terminó la gracia); un documento sin fecha ni regla está Sin vencimiento. Los avisos salen a las 13:00 UTC (08:00 en Colombia) y generan los eventos `documento.por_vencer` y `documento.vencido`.

## Vínculo

![Ciclo de vida de un vínculo: invitado, en validación, activo, por vencer, vencido, bloqueado, desvinculado y anulado](diagramas/ciclo-vinculo.svg)

| De | A | Qué lo dispara | Evento y webhook |
|---|---|---|---|
| Invitado | EnValidacion | Se abre un expediente inicial: al aceptar la invitación o por apertura manual. | `ExpedienteVinculado`; `expediente.abierto` |
| EnValidacion | Invitado | El expediente se rechaza, se anula o caduca. | `ExpedienteCerrado`; `expediente.rechazado`, `expediente.caducado` |
| EnValidacion | Activo | El expediente se aprueba. | `VinculoActivado`; `vinculo.activado` |
| Activo | EnValidacion | Un administrador desaprueba el último expediente aprobado (sin otro expediente abierto y con el vínculo vigente: ni bloqueado ni terminado). | `VinculoDesaprobado`; `expediente.desaprobado` |
| Activo | PorVencer | Llega un día de aviso de la vigencia del flujo; se abre la renovación del tercero (ratificar o actualizar las secciones que pide el flujo). | `VinculoPorVencer`; `vinculo.por_vencer` |
| PorVencer, Vencido | Activo | Se aprueba la renovación. | `VinculoActivado`; `vinculo.activado` |
| Activo, PorVencer | Vencido | Un documento obligatorio venció o se cumplió la vigencia más la gracia; la renovación queda abierta. | `VinculoVencido`; `vinculo.vencido` |
| cualquier estado vivo | Bloqueado | Bloquear con un motivo y, opcionalmente, una fecha de fin; es privado de la empresa. Suspende el certificado y congela la compartición hacia adelante. | `VinculoBloqueado`; `vinculo.bloqueado`, `certificado.suspendido` |
| Bloqueado | el estado previo | Desbloquear con una justificación, a mano o al llegar la fecha. Restablece el certificado y revalida: abre una actualización si lo aprobado difiere de lo actual. | `VinculoDesbloqueado`; `vinculo.desbloqueado` |
| cualquier estado vivo, incluido Bloqueado | Desvinculado | Desvincular con un motivo; anula el expediente abierto y revoca el certificado. | `VinculoDesvinculado`; `vinculo.desvinculado`, `certificado.revocado` |
| Desvinculado | EnValidacion, Invitado, Activo o PorVencer | Reintegrar: con revalidación vuelve a EnValidacion (si ya fue aprobado) o a Invitado (si nunca lo fue); **sin revalidar** restituye el estado que tenía, solo si estaba Activo o PorVencer. | `VinculoReintegrado`; `vinculo.reintegrado` |
| cualquier estado vivo | Anulado | Anular con un motivo. Terminal. | `VinculoAnulado`; `vinculo.anulado` |

**«En revisión» no es un estado.** Un vínculo Activo, Por vencer o Vencido con un expediente de **actualización** abierto (abierto, en revisión u observado) se muestra «En revisión» al tercero y a la empresa, y su certificado «En verificación», pero su `estado` sigue siendo el mismo: los ERP que consultan `estadoVinculo` no ven un valor nuevo. Un vínculo bloqueado muestra «Bloqueado», y uno terminado no tiene nada en revisión (decisiones VR-1 a VR-13 del registro).

## Expediente

![Ciclo de vida de un expediente: estados, qué pasa dentro de la revisión y estados de un requisito](diagramas/ciclo-expediente.svg)

| De | A | Qué lo dispara | Regla |
|---|---|---|---|
| Abierto | EnRevision | Todos los requisitos obligatorios están cargados (o el flujo no pide requisitos). | Se activa la primera etapa: todas sus áreas a la vez; las áreas automáticas aprueban de inmediato. |
| EnRevision | Observado | Un área observa un requisito con motivos. | Con la política **Inmediata** se devuelve al instante; con **AlCerrarArea**, cuando todas las áreas de la etapa se pronunciaron. |
| Observado | EnRevision | El tercero corrige y reenvía, o la última observación vigente se anula. | Solo los requisitos observados vuelven a revisión; reenviar sin corregirlos responde 409 `requisitos-sin-corregir`. |
| EnRevision | Aprobado | Todas las etapas aprobadas **y** las verificaciones bloqueantes satisfechas y vigentes. | Una etapa se aprueba cuando todas sus áreas aprobaron con sus aprobadores; la siguiente solo empieza entonces. |
| EnRevision, Observado | Rechazado | Un área que puede rechazar lo rechaza, con motivos y justificación; o el oficial de cumplimiento lo rechaza. | Terminal. El tercero ve los motivos que se le comunicaron. |
| Aprobado | EnRevision | Un administrador desaprueba: una etapa, la fase inicial o el expediente completo. | El mismo expediente vuelve a revisión y el vínculo a EnValidacion; exige una justificación. |
| Abierto, EnRevision, Observado | Anulado | Anular con un motivo. | Terminal. También ocurre solo al anular o desvincular el vínculo. |
| Abierto, Observado | Caducado | Venció el plazo de cargue (30 días por omisión) con el expediente en manos del tercero. | La empresa puede reabrirlo con un plazo nuevo: vuelve al estado que tenía. |

Las **áreas** de una etapa tienen su propio estado (Esperando, Pendiente, Aprobada, Observada) y los **requisitos** también: Pendiente, Cargado, EnRevision, Observado, Aprobado, Eximido (la empresa lo dispensa), NoAplica (la condición de exigencia no lo pide), Vencido y Desactualizado. Un requisito se aprueba cuando todas las áreas que lo revisan y todos sus aprobadores lo aprueban.

Hay tres **tipos** de expediente: Inicial, Renovación (al acercarse o llegar el vencimiento del vínculo, o al reintegrarlo con revalidación) y Actualización (una versión nueva cambia lo que la empresa ya aprobó). Solo hay un expediente abierto por vínculo.

## Certificado

![Ciclo de vida de un certificado: vigente, con pendientes, suspendido, vencido, revocado y reemplazado](diagramas/ciclo-certificado.svg)

Un certificado es **uno por vínculo**; cada aprobación suma una **emisión** con folio propio, y la anterior queda Reemplazada pero sigue siendo verificable.

| De | A | Qué lo dispara | Evento y webhook |
|---|---|---|---|
| (aprobación) | Vigente | Se aprueba el expediente de un vínculo. La emisión ocurre en la misma transacción de la aprobación; el PDF se genera después. | `CertificadoEmitido`; `certificado.emitido` |
| Vigente | ConPendientes | Se abre una actualización: una versión nueva de algo que la empresa ya había aprobado. | `CertificadoConPendientes`; `certificado.con_pendientes` |
| ConPendientes | Vigente | La actualización se cierra sin aprobarse (rechazada o anulada): lo ya aprobado sigue valiendo. | `PendientesDeRevalidacionResueltos`; `certificado.restablecido` |
| Vigente, ConPendientes | Reemplazado | Se aprueba una renovación o actualización: nace una emisión nueva. | `CertificadoReemplazado`; `certificado.reemplazado` |
| ConPendientes | (bloquea) | Venció el plazo de revalidación (15 días o el de la política del grupo): el certificado deja de ser «En verificación» y pasa a bloqueante (`bloqueado: true`, `PlazoRevalidacion`). El vínculo no cambia de estado. | `PlazoDeRevalidacionVencido` |
| Vigente, ConPendientes | Suspendido | El vínculo se bloquea. | `CertificadoSuspendido`; `certificado.suspendido` |
| Suspendido | el estado previo o Vencido | Se desbloquea el vínculo; si la fecha del certificado pasó durante el bloqueo, queda Vencido. | `CertificadoRestablecido`; `certificado.restablecido` |
| Vigente, ConPendientes | Vencido | Pasó su fecha de vigencia. | `CertificadoVencido`; `certificado.vencido` |
| cualquier estado, salvo Revocado | Revocado | Un administrador lo revoca con una justificación, o el sistema al anular o desvincular el vínculo. **Irreversible.** | `CertificadoRevocado`; `certificado.revocado` |

**«En verificación»** es el `ConPendientes` de una actualización abierta mientras su plazo no vence: el certificado sigue **vigente hasta su fecha**, no se suspende ni se marca bloqueado, y el PDF se puede descargar. Para quien consulta la certificación desde un ERP, la regla de oro no cambia: **pagar solo si `certificado: true` y `bloqueado: false`**.
