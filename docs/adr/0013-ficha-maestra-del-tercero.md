# ADR-0013 — Ficha maestra del tercero por secciones, en Validasoft

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-11-09 (decisiones FM-1 a FM-16) | Concreta P-3-1/P-3-2 (formularios canónicos) y B-3 (personas relacionadas); amplía ADR-0004 y ADR-0005 |

## Contexto
La información del tercero que hoy usa el SRM para validar, aprobar y crear el proveedor en el ERP vive en las tablas del Portal de Proveedores, organizada por **secciones**: general, tributaria, bancaria, financiera, legal (representantes, socios, revisor fiscal, contador), SISO/SST, especialidades, contactos, términos y tratamiento de datos (análisis en [docs/14](../14-ficha-maestra-del-tercero.md) §1). El SRM la usa para:

- decidir la **completitud** del proveedor;
- **rechazar con motivos** por sección;
- **aprobar por sección** las actualizaciones posteriores;
- llevar el **vencimiento por sector**;
- **aplicar en AyF** lo aprobado.

Validasoft hasta hoy solo guarda la identidad del tercero, sus contactos y sus cuentas bancarias. Los campos que llena el proveedor existen solo por vínculo (TD-4). El formulario canónico global decidido en P-3-1 no se había entregado.

El Portal tiene además problemas que Validasoft no debe heredar:

- **Sin versionado.** Cada guardado sobrescribe y el rechazo de una constructora **no revierte** el dato, que sigue vigente para todas.
- **Soportes genéricos.** No tienen dueño ni tipo documental.
- **Ningún cruce** entre lo declarado y el documento que lo respalda.
- **Datos poco confiables.** El DV puede valer 0 sin validar y `NoObligFacturar` tiene la semántica invertida.
- **Sin integridad.** No hay integridad referencial y cualquier ApiKey puede leer o editar a cualquier tercero.

Producto decidió que **la ficha maestra es de Validasoft y se migra desde el Portal** (2026-11-09).

## Decisión
- **Validasoft es el dueño de la ficha maestra del tercero.** Adopta las secciones y campos del Portal como modelo de referencia, normalizados. El Portal y el SRM pasan a leerla o a embeber la UI de Validasoft (H9). Durante la coexistencia se sincroniza en un solo sentido, del Portal a Validasoft (FM-11), hasta el corte de cada empresa.
- **Cada sección es un formulario canónico global de la bóveda**, con la misma mecánica de los documentos (ADR-0004/0005, M-8):
  - versiones inmutables y una sola vigente por tercero y sección;
  - compartida con las empresas vinculadas que la exigen;
  - **aceptada u observada por cada empresa con sus motivos**: la aceptación es privada de cada empresa y una versión nueva dispara la revalidación en todas.
  - Lo que una empresa rechaza **no** cambia lo que ya aceptó: sigue viendo su versión aceptada hasta aceptar una nueva. Así se corrige el «rechazo que no revierte» del Portal.
- **Las secciones son requisitos de los flujos**, igual que los documentos. La completitud, los veredictos por área, los motivos tipificados, la vigencia y avisos por sección, la revalidación y la auditoría reutilizan lo que ya existe.
- **El documento es la fuente de las secciones.** Un mismo documento respalda campos de varias secciones: el RUT respalda la general, la tributaria y los representantes; la Cámara, la legal; los estados financieros, la financiera; la certificación, la cuenta. Un documento que la empresa exige como requisito **también sirve de soporte** de esas secciones (opción por requisito, activa por omisión). La **actualización nace del documento**: el tercero sube el RUT nuevo, la IA propone los cambios de sus secciones, el tercero los confirma y cada empresa revisa una sola actualización con el documento como evidencia (FM-13, FM-14).
- **Cruce automático sección ↔ documento** con los hechos de IA del documento (ADR-0012). Se compara campo por campo, con resultado `Coincide`, `Discrepa` o `NoVerificable`. Es evidencia para el revisor y entrada para la revisión automática. El SRM no tiene este cruce: es valor agregado de Validasoft.
- **La completitud de cada sector es un dato guardado** (FM-16). La calcula una sola regla del dominio al guardar la versión y la publica una proyección por tercero y sección. El wizard avanza con ella, y de ella sale el motivo legible de cada empresa. Nunca se deduce en pantalla ni se fabrica al leer.
- **Hacia el ERP**: la aceptación de una sección por una empresa emite el evento público `seccion.aceptada` con el contenido aceptado. El SRM lo aplica en AyF con el mismo mapeo de hoy (H9).

## Alternativas descartadas
- **Leer el Portal en vivo** (el Portal sigue siendo dueño): hereda la falta de versionado y la aprobación por empresa queda fuera del control de Validasoft; los problemas de calidad del dato siguen.
- **Una tabla plana por sección en Validasoft sin versionado**: repetiría el error del Portal.
- **Secciones por vínculo** (como TD-4): duplicaría la información en cada empresa; la ficha es del tercero, solo la decisión es de cada empresa.

## Consecuencias
- Se entregan los **esquemas canónicos** de las secciones (P-3-1) y el render del formulario en el portal del tercero.
- Las personas relacionadas (B-3) se implementan con la sección legal: en una primera fase como filas de la sección y luego como terceros persona natural con bóveda propia.
- Las especialidades pasan a un **catálogo de plataforma** de tres niveles (grupo → categoría → especialidad), usable por las reglas de exigencia y por las integraciones (licitaciones).
- El importador (H10) carga las tablas `Ter*` del Portal como la versión 1 de cada sección y corrige lo conocido: recalcula el DV, normaliza la tributaria con los códigos DIAN y deduplica personas.
- El plan de hitos cambia: la ficha maestra entra antes de H9 (ver docs/14 §8).
