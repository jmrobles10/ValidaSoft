# 16 — Estado del proyecto, alcance y pendientes (corte 2026-11-11)

> **Para qué sirve:** retomar el trabajo sin reconstruir el contexto. Dice qué se quiere lograr, qué está hecho en
> `main`, qué está en curso, qué falta con detalle y cómo continuar.
> **Fuentes:** [registro de decisiones](decisiones/registro.md) (la verdad sobre cada decisión), [09](09-plan-de-hitos-y-responsabilidades.md)
> y [11](11-trazabilidad-por-hito.md) (plan por hito), [14](14-ficha-maestra-del-tercero.md) (ficha) y [15](15-infolaft.md) (Infolaft).

## 1. Qué se quiere lograr

Validasoft reemplaza la validación de terceros que hoy está repartida entre el **SRM** (dentro del ERP Marco, una
instancia por empresa) y el **Portal de Proveedores**. Es un producto aparte, multi-tenant y consumible por API.

**Objetivo 1 — paridad con el SRM, primero.** Nada de lo que el SRM hace hoy puede faltar, y donde lo hace mal se
mejora. El checklist de paridad es [01 §10](01-analisis-legado-srm-portal.md). Concretamente:
- invitar terceros, uno a uno y por lote, y cargar sus documentos con observaciones y reenvío;
- exigir documentos según reglas (tipo de persona, país, tipo de vínculo, condiciones);
- aprobación por áreas en secuencia o en paralelo, con motivos tipificados, observar, rechazar, desaprobar, anular,
  reintegrar y desvincular;
- vigencias de documentos y del vínculo, avisos, vencimientos, renovación y ratificación;
- actualizaciones posteriores a la aprobación, revisadas por cada empresa;
- la ficha del tercero por secciones con sus soportes (lo que hoy es el «wizard» del Portal);
- listas restrictivas y estudio SAGRILAFT del oficial de cumplimiento como condición de cierre;
- certificado verificable, informes y exportes, y auditoría;
- integración con el SRM, el Portal y Marco durante la coexistencia (incluida la aplicación en AyF), e importación de
  lo existente.

**Objetivo 2 — sin repetir los errores del legado.** No hay copias espejo de documentos: una versión vive una sola vez
en la bóveda del tercero y cada empresa decide sobre ella. Las decisiones de cada empresa son privadas: el bloqueo o
rechazo de una no afecta ni se ve en otra. Los estados son textuales y los eventos están firmados. La completitud se
calcula en el servidor y queda guardada. Los secretos van cifrados.

**Objetivo 3 — lo que el SRM no tiene:**
- IA que revisa los documentos y cruza la ficha con sus soportes, con cupo por empresa y la clave de OpenAI de cada empresa;
- consola de Plataforma para dar de alta grupos y empresas;
- SDKs para que cualquier sistema se integre;
- tiempos por área (SLAs).

**Criterio de terminado:** cada flujo del checklist funciona de punta a punta, con pruebas de dominio, de integración y
e2e en verde, en una instalación limpia y con una empresa creada desde la consola de Plataforma.

## 2. Qué está en `main`

| Bloque | PR | Contenido |
|---|---|---|
| H1–H6 | ≤ #28 | Fundación técnica, bóveda del tercero, configuración y reglas de exigencia, expediente y veredictos por área, certificado, notificaciones y webhooks, vigencias de documentos, revalidación y versiones nuevas |
| P2 | #29–#36 | Inicio, Certificados, Tercero 360, Auditoría, Plantillas, aviso de versión nueva |
| IA | #38 | Análisis de documentos con OpenAI, plan y cupo por empresa, pantallas «Plan y uso» (ADR-0012) |
| Fechas | #39 | Calendario del negocio en la zona de Bogotá (FE-1, FE-2) |
| Ficha F1–F4 | #40–#42, #44 | Secciones con soporte y completitud guardada; secciones como requisitos del flujo validadas por empresa; `seccion.aceptada` y ficha aceptada para el SRM y AyF; importador y sincronizador desde el Portal |
| Wizard | #43 | Panel lateral fijo, barra de guardar siempre visible, vista previa de la completitud (FM-46), aviso de salida, animaciones |
| E2E aislada | #45 | En local las pruebas usan su propia base y su propio Mailpit |
| Infolaft | #46 | [docs/15](15-infolaft.md): por qué no se usa la suscripción y el plan |
| Plataforma | #47 | Consola `/plataforma`, alta completa de grupos, plan de IA con la llave de OpenAI por empresa, clave maestra de secretos con rotación (PT-1..PT-8) |
| H9, H7, H8 | #48 | Integración con el SRM y Marco: consulta de certificación con ETag, eventos por pull, rotación de API keys, SDKs .NET y TS, guía de integración (CN-1..CN-7). Listas restrictivas con Infolaft como proveedor principal y credenciales por empresa, estudio SAGRILAFT, bloqueo del cierre, bandeja del oficial e informe (VE-5..VE-18). Invitación masiva por lote y caducidad por plazo de cargue (LT-1..LT-10). Informes de estado y de vínculos, exportación de auditoría y ZIP del expediente (IF-1..IF-8) |
| F5 | #49 | Cruce sección ↔ documento con IA, «Usar como soporte», «Actualizar desde mi RUT / Cámara…» en el wizard, justificación ante discrepancias críticas; **la actualización abierta se amplía** con lo que cambia después (FM-48..FM-55) |
| Navegación | #50 | Pestañas con «Más» adaptativo y Configuración como engranaje (NAV-1..NAV-7) |
| Consola de grupos | #51 | Lista de grupos paginada por cursor con buscador, scroll infinito virtualizado, sin salto lateral (PT-9..PT-12) |
| Reglas visuales | #52 | Skeleton al cargar, botones compactos, selects y autocompletar en español, acordeones con radio, Plan de IA a dos columnas, avisos de documento agrupados (UI-1..UI-6) |

**Verificación de `main`** (última corrida completa, #52): Vitest 699/699, e2e aislada 57/57; dominio 658/658 e
integración 150/150 en #51. Todo PR se fusiona solo con el CI en verde (lint, tipos, build, dominio, integración,
e2e).

## 3. En curso

### 3.1 Tres ramas de Validasoft (agentes en paralelo)
Cierran los huecos del proceso de validación frente al SRM que encontró la auditoría del 2026-11-10 (§4.1). Cada una
llega con su verificación completa; se revisa, se abre su PR y se fusiona con el CI en verde.

| Rama | Huecos | Contenido |
|---|---|---|
| `feature/vigencia-vinculo` | 2, 5 y desbloqueo | Vencimiento periódico del vínculo (días desde la última aprobación, avisos, gracia), estado `PorVencer`, vigencia opcional por sección, renovación con **ratificación de secciones** que se abre en el primer aviso (**pre-vencimiento**); plazo de revalidación configurable (`DiasRevalidacion`); desbloqueo automático al llegar la fecha `Hasta`. **Terminada y verificada; falta su PR.** Registro VV- (sección «5-unquadragies») |
| `feature/desaprobar-y-reintegrar` | 3, reintegrar y reinicio | **Terminada, con la verificación completa (§4.10).** Reabrir una etapa ya aprobada, desaprobar un expediente aprobado y volver a la fase inicial, con justificación; reintegrar sin revalidar; «Volver a postularme» tras un rechazo. Más los e2e que faltaban: varias etapas, dos aprobadores, separación de funciones, anular veredicto, desvincular, reintegrar, anular. Registro DS- |
| `feature/integracion-abierta` | Principio de producto | **Integración abierta (IO-1…IO-10).** El núcleo no nombra ni asume el SRM, el Portal ni AyF: importador genérico con adaptador de origen (el Portal es un adaptador opcional), referencias externas por sistema (tercero y vínculo, idempotentes y privadas de cada tenant), contactos por empresa (nombre, cargo, correo, teléfono, rol), datos de pago genéricos (banco del catálogo con código de compensación y SWIFT, contacto de pagos, lugar de expedición, `ExentoReteCree`) y embebido genérico del tercero (`POST /sesiones-tercero`, `/embed/:componente`, `montarEmbebido`). Guía «integrar un sistema nuevo en 5 pasos» y contrato del interruptor en [integracion.md](integracion.md) §1.1 y §8.1 y en [08](08-integracion-y-migracion.md) §3.4; auditoría de acoplamiento en §9 |
| `feature/cumplimiento-h7` | H7 completo | **Terminada, a la espera de su PR (§4.4).** Matriz de riesgo, archivos del oficial, consulta de listas por fuera, rechazo directo, expiración por cambio de personas, correo de cierre, claves por grupo, Infolaft configurable, informe SAGRILAFT completo y revisión automática con IA con su bandeja `/excepciones`. Registro CU- |
| `feature/novedades-tercero` | 4 | Campana del tercero en el portal (de todas sus empresas), preferencias de correo, recordatorio de observaciones pendientes individual y masivo (uno al día por expediente), y aviso a las áreas cuando el tercero responde. Registro NT- |

### 3.1.1 Integración por API (rama `feature/integracion-sandbox`)
Deja **bien definida la forma de integrarse** (SB-1…SB-10, [registro](decisiones/registro.md)): **sandbox** con datos aislados (tenant hermano, buzón de correo, IA y listas simuladas, reinicio), **SDK publicables** (versión única, README/CHANGELOG, ESM + CJS, workflow `sdk-publicar.yml`; no se publicó nada), **prueba de carga k6** de la consulta de certificación (consulta de cuatro viajes a uno; el p99 < 100 ms a 500 rps queda por certificar en un entorno dedicado), la **[guía de integración](integracion.md)** de punta a punta y la **e2e de integración completa con el SDK** en el sandbox. Detalle en §4.5.

### 3.1.2 Retiro de versión por el tercero (rama `feature/aviso-retiro-version`)
El tercero ve el **impacto antes de retirar** una versión de su bóveda (qué versión queda vigente, qué empresas ya la recibieron, aprobaron o revisan y qué
revalidaciones se abren) y la empresa recibe el aviso (campana y correo, plantilla `retiro-version`) con el motivo, que también ve en la línea de tiempo, el requisito y
las revalidaciones; una sección cuyo soporte se retiró queda con una advertencia, no como faltante (RT-1…RT-6, sección 5-sexquadragies del [registro](decisiones/registro.md)).

### 3.1.3 Lote de integración (`integracion/lote-2026-11-11`)
Las cinco ramas anteriores —`feature/aviso-retiro-version` (RT), `feature/integracion-sandbox` (SB), `feature/integracion-abierta` (IO), `feature/vigencia-vinculo` (VV) y
`feature/ingreso-sin-contrasena` (ING; §3.3)— se fusionaron en ese orden sobre `main` en una sola rama y se verificó **la mezcla**, no cada rama por separado. Lo que la mezcla
obligó a decidir: los correos a terceros llevan a la vez la preferencia del tercero (NT-5) y el tenant que los origina (el sandbox los retiene, SB-4); el permiso
`sesiones-tercero:crear` (IO-6) se ofrece en el diálogo de API keys junto a `sandbox:administrar`; el embebido del tercero y la guía de cinco pasos viven en
[integracion.md §8.1 y §1.1](integracion.md) (08 §2.3 es solo un puntero); y el stream de la campana se corta cuando se cierra o se revoca la sesión directa (ING-3).
Verificación de la mezcla: 929 pruebas web, 790 de dominio, 20 del SDK, 72 del adaptador del Portal, 233 de integración y 99 e2e, con lint, tipos, build y compilación .NET sin advertencias.

### 3.1.4 Operación de la revisión (rama `feature/operacion-revision`, 2026-11-11)
Sale del lote de integración. Decisiones **OP-1…OP-21** en el [registro](decisiones/registro.md#5-novemquadragies-operación-de-la-revisión-2026-11-11); contratos en [05 §2.7.3](05-api-contratos-y-webhooks.md).
- **Campañas de renovación masivas:** la empresa elige vínculos por filtro (vencen en N días, tipo de vínculo, estado), ve a quién afecta antes de confirmar y abre su renovación en lote; se procesa por tandas con el patrón de los lotes, es reanudable (pausar, reanudar, cancelar, reintentar) y deja informe por vínculo.
- **Delegación de revisión:** quien revisa delega sus áreas (o todas) en otra persona entre dos fechas; la persona delegada ve la bandeja y los avisos del área y decide, y la separación de funciones y el conflicto de intereses se le siguen aplicando a ella. Todo queda auditado.
- **Plazos de revisión (SLA):** plazo por área en el flujo (ya existía) y por defecto en la empresa, alertas en la campana y por correo al acercarse y al vencer (y a los administradores), indicador en la bandeja, informe de cumplimiento y eventos públicos `expediente.sla_por_vencer` / `expediente.sla_incumplido`.
- **Ayuda por documento por empresa**, **plantillas editables y preferencias del tercero** para los avisos de pre-vencimiento y vigencia (más la campana del tercero), **recordatorio de observaciones desde Revalidaciones y Vencimientos** y **recordatorio automático por flujo**.
- **Pendiente:** OP-6 y OP-21 (campañas programadas, delegación por expediente, SLA en días hábiles, escalamiento en dos niveles, filtro de la bandeja por plazo).

### 3.2 SRM — aplicar en AyF (rama local, en espera)
Rama **local** `feature/validasoft-ayf` en `C:\Fuentes_Git\SRM` (sale de `Feature_INT_NOVIEMBRE_SRM`, 6 commits, **sin
push**). Por decisión de Producto (G-4, 2026-11-11) queda en espera: primero se prueba Validasoft solo, sin integrarlo en
ningún sistema; no se sube ni se crea WI hasta que Producto lo pida. Detalle en `BackEnd\INTEGRACION_VALIDASOFT.md` de esa rama.
- **Qué hace:**
  - receptor de webhooks de Validasoft con firma verificada e idempotencia;
  - worker con reintentos;
  - mapa evento → acción: habilitar y aplicar en AyF General, Tributaria, Legal y Bancaria con lo aceptado por la
    empresa; bloquear, desvincular y anular;
  - cliente HTTP con API key por constructora cifrada;
  - flag `ValidacionDocumental = Legacy | Validasoft` (con `Legacy` nada cambia).
- **Verificación:** compila; 318 pruebas nuevas y 433 de regresión del código de AyF tocado; scripts SQL probados solo
  en una LocalDB temporal.
- **Decisiones que necesita** (del equipo de Marco y de Producto):
  - cuenta de servicio para escribir en AyF sin usuario conectado;
  - estado «Bloqueado» en el SRM (se usó −11) y bloqueo real en AyF;
  - equivalencias de bancos (nombre → `BcoCod`);
  - lugar de expedición del representante;
  - cifrar los secretos que el SRM guarda en claro.

### 3.3 Ingreso sin contraseñas (`feature/ingreso-sin-contrasena`, 2026-11-11)
Rama lista para revisar, **sin push ni PR**. Decisiones **ING-1…ING-9** en el [registro](decisiones/registro.md); diseño en
[07 §2.7](07-seguridad-identidad-y-tenancy.md), contrato en [05 §2.10](05-api-contratos-y-webhooks.md).
- **Qué hace:**
  - ingreso directo por correo, código de 6 dígitos y **passkey** (WebAuthn, Fido2NetLib 4.3.2), sin contraseñas;
  - la passkey sola vale por dos factores; es obligatoria para `Administrador`, `OficialCumplimiento`, `Auditor` y
    Plataforma (y para todos si el grupo lo pide);
  - modos por grupo `Delegado`, `Directo` o ambos (los grupos existentes quedan en `Delegado`); duración e
    inactividad de la sesión por grupo; refresco rotativo con detección de reuso en la cookie `vs_usuario`;
  - operadores de Plataforma con ingreso directo (la clave de arranque solo da de alta al primero);
  - administración: invitar, desactivar, ver y cerrar sesiones, ver y revocar passkeys (la recuperación);
  - web: pantalla de ingreso, registro guiado de la passkey, «Mi cuenta», Configuración › Usuarios y operadores;
  - auditoría de identidad (ingresos exitosos y fallidos, passkeys, sesiones, con dispositivo e IP) y abstracción
    `IProveedorDeIdentidad` lista para OIDC/WorkOS (no integrado).
- **Verificación** (rebasada sobre `main` en `3b81cd4`): dominio 758/758; integración 205/206 (la que falla,
  `Reintegrar_avisa_al_tercero_segun_el_modo…`, también falla en `main` sin esta rama); Vitest 815/815; e2e de ingreso,
  tiempo real, novedades del tercero y decisiones 17/17 (63/63 antes del rebase); lint, tipos y build en verde. El e2e usa el
  autenticador virtual de Chrome (CDP) para registrar y firmar de verdad. Se arregló de paso la intermitencia de
  `decisiones.spec` (el token del enlace podía contener «Olga»): el chequeo de «no nombra áreas» ahora ignora las direcciones.
- **Queda fuera** (§4.6): WorkOS/OIDC, autocompletado de passkeys en el campo del correo (conditional UI), reautenticación
  para acciones sensibles, avisos por correo de cambios de passkey y consulta de la auditoría de ingreso de Plataforma.

### 3.4 Declaración jurada PEP (rama `feature/declaracion-pep`, 2026-11-12)
Rama lista para revisar (con push, **sin PR**). Cierra «lo mismo de la HU 49093» del SRM y el Portal; decisiones **DP-1…DP-17** en el [registro](decisiones/registro.md#5-sexquinquagies-declaración-jurada-pep-en-la-ficha-hu-49093-2026-11-12) (con la tabla de paridad HU ↔ Validasoft), diseño en [14 §4.2](14-ficha-maestra-del-tercero.md) y contrato en [05 §2.3](05-api-contratos-y-webhooks.md).
- **Qué hace:** «Sí» a «¿Es PEP?» abre el diálogo «Declaración jurada PEP» (datos básicos, condición PEP, vínculo con otra PEP, juramento y firma dibujada o subida), en la General de la natural y en cada persona de la legal; el **backend** exige la declaración (422 `declaracion-pep-requerida`), sella la firma (huella, fecha, IP) y la versiona con la sección; la empresa la ve en la revisión, la ficha aceptada y Tercero 360 con **Descargar PDF** (por persona, estilo de los PDF de Validasoft); el informe SAGRILAFT muestra «Sin declaración» y el Excel suma columnas; el ZIP del expediente lleva `declaraciones-pep/`; el importador del Portal lee la declaración activa; el canónico `DECLARACION_PEP` se retira.
- **Pendiente** (DP-16): logo en el PDF, descarga por el propio tercero, importar la imagen de las firmas del Portal (hoy solo su huella), y el «Registro automático por RUT» de la HU.

## 3-bis. Qué está en la rama `feature/novedades-tercero` (aún no en `main`)

Sale de `main` y cierra los huecos con el SRM + Portal sobre las novedades del tercero. **No tiene PR.** Detalle en el
[registro](decisiones/registro.md#5-septquadragies-novedades-del-tercero-y-tiempo-real-2026-11-11)
(NT-1..NT-17) y en [docs/05 §2.7.2](05-api-contratos-y-webhooks.md).

| Bloque | Estado | Registro |
|---|---|---|
| **Campana del tercero** en `apps/public` (todas sus empresas, cada aviso con su marca, no leídas, marcar leída, enlace a lo que hay que hacer) y sus preferencias de correo por tipo | Terminado | NT-1, NT-2, NT-3, NT-5 |
| **Recordatorio de observaciones pendientes**: desde el expediente, en lote desde la bandeja (seleccionados o «todos los observados» con filtro de área), uno por día, plantilla editable y aviso en la campana | Terminado | NT-7, NT-8 |
| **Recordatorio automático** cada N días mientras siga observado | Terminado, **por empresa** (no por flujo) | NT-9 |
| **Aviso a las áreas cuando el tercero responde**: también a las que revisan los documentos reabiertos | Terminado y cubierto con e2e | NT-4 |
| **Avisos al tercero al desaprobar y reintegrar** (DS-10): campana y correo con plantilla editable, según preferencias, sin la justificación interna | Terminado, con integración y e2e | NT-18 |
| **Tiempo real** entre la empresa y el tercero (SSE de «cambió X» en la web y en el portal, campana del tercero al instante, aviso cuando se designa una cuenta, «Hay cambios nuevos» sin pisar lo que se escribe, reconexión y respaldo) | Terminado y cubierto con e2e de dos navegadores | NT-12…NT-16 |

Pendiente de esta rama: pasar el recordatorio automático a **por flujo** (cuando se integre el plazo de cargue por flujo
de la otra rama, NT-9), la acción masiva desde Revalidaciones y Vencimientos, la propuesta desde un documento que aún no se compartió con ninguna
empresa (NT-11) y la guardia de edición en otros formularios que no sean la ficha y la respuesta del portal (NT-17).

## 4. Qué falta, con detalle

### 4.1 Proceso de validación frente al SRM (auditoría 2026-11-10)
**Ya cubierto en `main`:**
- **Aprobaciones:** por documento y por área, etapas en secuencia y áreas en paralelo, aprobadores requeridos,
  aprobación del administrador con justificación, cierre automático y certificado.
- **Novedades:** observar con motivos tipificados, respuesta y reenvío solo de lo observado, invalidación por versión
  nueva y avisos a las áreas. En el SRM una «novedad» es un rechazo con motivos que el proveedor corrige.
- **Rechazos:** rechazo terminal del expediente por un área autorizada, con motivos y justificación; bloqueo y
  desbloqueo con suspensión del certificado.
- **Vencimientos:** vigencia de documentos por empresa con avisos y gracia, recordatorios, caducidad por plazo de cargue
  con reapertura, renovación y ratificación de documentos, informe y Excel.
- **Actualizaciones:** revalidación por documento y por sección, ampliación de la actualización abierta, bandeja de
  revalidaciones, comparación de versiones y cruce con IA.

**Huecos** (los numerados están en curso, §3):
1. **Aplicación en AyF:** en espera (§3.2).
2. **Vencimiento periódico del vínculo y de las secciones; `PorVencer` nunca se asigna:** **resuelto** en
   `feature/vigencia-vinculo` (VV-1..VV-12, [03 §5.4](03-dominio-y-eventos.md)): el flujo configura días, avisos y gracia
   (`dias = 0` mantiene el comportamiento anterior); el vínculo pasa a `PorVencer` en el primer aviso (**pre-vencimiento**) y
   ahí se abre la renovación, que pide **ratificar** (sin cambiar datos) o **actualizar** las secciones de la ficha y los
   documentos obligatorios no vigentes; si se aprueba antes de la fecha el vínculo nunca vence y la vigencia nueva cuenta
   desde esa aprobación; si no, vence con su certificado y la misma renovación sigue abierta; cada sección puede traer su vigencia.
3. **Desaprobar una etapa ya aprobada o volver a la fase inicial:** resuelto en `feature/desaprobar-y-reintegrar`
   (§4.10), a la espera de su PR.
4. **Campana del tercero y recordatorio de observaciones:** en curso.
5. **Plazo de revalidación configurable:** **resuelto** en la misma rama (VV-7): rige el menor `diasRevalidacion` de los
   requisitos de la actualización (tipo de documento o sección del flujo), 15 por omisión.
6. **Desbloqueo automático, reintegrar sin revalidar, reinicio tras rechazo:** el desbloqueo automático está **resuelto**
   en `feature/vigencia-vinculo` (VV-8: mensaje programado de Wolverine al bloquear con fecha, respaldado por el barrido
   del job de vigencias); reintegrar sin revalidar y el reinicio tras un rechazo están resueltos en
   `feature/desaprobar-y-reintegrar` (§4.10).

**Pendiente de lo resuelto en `feature/vigencia-vinculo`:** un vínculo vencido por dos documentos a la vez solo abre la renovación con el primero
(RN-1); el aviso de vigencia aún no es una plantilla personalizable (PL-1); los vínculos con vigencia o bloqueo anteriores
al cambio se indexan con su próximo evento (o reconstruyendo la proyección del vínculo).

### 4.2 F6 — sin empezar
- Personas relacionadas (representantes, socios) con **bóveda propia**: su cédula y sus documentos se comparten entre
  las empresas como los del tercero.
- ~~**Cargo del contacto por empresa**: el mismo tercero tiene contactos distintos para cada empresa.~~ Resuelto en `feature/integracion-abierta` (IO-4): contactos por vínculo con cargo, correo, teléfono y rol.

### 4.3 Ficha — pendientes (FM-38, FM-55)
- ~~Banco con código CENIT (catálogo `bancos`), SWIFT y contacto de pagos; lugar de expedición del documento del representante; `ExentoReteCREE`.~~ Resueltos en `feature/integracion-abierta` (IO-5) como datos genéricos, no «de AyF»: salen en `GET /vinculos/{id}/ficha`.
- ~~Lugar y fecha de expedición cuando el representante es el propio tercero (persona natural).~~ Resuelto en FD-8: la General de la persona natural los captura (opcionales) y salen en el representante del ERP.
- ~~Prellenar General con lo que ya se sabe del tercero (razón social y correo de la invitación).~~ Resuelto en FD-1.
- ~~Incluir las observaciones en el DTO de la ficha; hoy el portal hace N peticiones.~~ Resuelto en FD-2.
- ~~La web aún no muestra la ficha del vínculo ni el evento `seccion.aceptada` en la configuración de webhooks.~~ Resuelto en FD-3 (ficha aceptada del vínculo) y FD-7 (grupos del catálogo de webhooks).
- ~~Tras F5: la certificación bancaria no está en el mapa de respaldo; una sección que cambia sin documento nuevo no se marca «sin respaldo» (FM-15); la cédula respalda solo la General.~~ Resueltos en FD-5, FD-4 y FD-6.
- Persona natural frente a jurídica: ajustada con su tabla en FD-8. **Abiertas para Producto:** ¿el PEP de la natural es obligatorio?; ¿se capturan primer y segundo nombre y apellido por separado?
- Sigue pendiente:
  - la revisión automática de H7 ya usa el cruce (CU-19); falta que lea también el cruce de las cuentas y el «sin soporte» (se ven en la revisión manual);
  - el cruce de las cuentas con la certificación solo se ve en el requisito de la certificación: falta mostrarlo en la tarjeta de la **cuenta designada** para quien la aprueba (CB-2) y que no se guarde como hecho compartido;
  - la configuración por flujo de FM-15 («exigir siempre un documento nuevo»): hoy se cubre observando la sección y señalando el soporte;
  - sin plan de IA (o sin cupo) ningún tercero recibe valores propuestos ni cruce: queda la revisión manual.

### 4.4 H7 — cumplimiento completo (rama `feature/cumplimiento-h7`, decisiones CU-1..CU-22)

Hecho en la rama, a la espera de su PR. **Paridad con el oficial del SRM** (revisado solo en lectura: `BackEnd/SRM.Services.Proveedores/OficialCumplimiento`, `Integraciones/ApiSagrilaft`, `Frontend/React/src/pages/Provedores/OficialCumplimiento`):

| Lo que el oficial hace en el SRM | En Validasoft |
|---|---|
| Bandeja por estado (pendiente, aprobado, rechazado, vencido) con filtros por proveedor y riesgo | Bandeja del oficial y, en el informe SAGRILAFT, una fila por tercero con filtros por estado y nivel. **Cubierto** (CU-12) |
| Estadísticas: tiempos, % de cumplimiento, pendientes y vencidas | Tablero y tiempos del informe. **Cubierto** (CU-12) |
| Iniciar el estudio (botón, automático o migración por Excel) | Automático al pasar a revisión según el flujo. **Cubierto**; la migración por Excel queda para H10 |
| Consultar listas (un proveedor por constructora, el resultado es un PDF) | Proveedor por empresa con evidencia PDF y respuesta cruda con hash, cuota y reconsulta. **Mejorado** (VE-7..VE-11) y consulta hecha por fuera con su PDF (CU-6) |
| Registrar coincidencias y descartar (texto libre) | Coincidencias estructuradas con decisión, justificación y vigencia. **Mejorado** (VE-17) |
| Estudio: riesgo 1–3, conflicto de interés, debida diligencia, observaciones, vigencia | Estudio VE-9 y **matriz de riesgo** (el SRM no tiene: allí el nivel es una estrella a mano) (CU-2..CU-4) |
| Adjuntar evidencias | Archivos del estudio, sin pisarse entre proveedores (CU-5) |
| Aprobar | El estudio favorable satisface; el expediente cierra solo. **Cubierto** |
| Rechazar (`RechazoOficial`, estado −11) | Rechazo directo ante un hallazgo, con motivos y justificación (CU-7) |
| Bloquear o desbloquear | No es del oficial en el SRM; aquí el rol puede bloquear el vínculo. **Mejorado** |
| Reintegrar un estudio vencido | El estudio expirado vuelve solo a la bandeja. **Cubierto** |
| Informes: tablero, informe general y Excel (con información legal, PEP y beneficiario final) | Informe SAGRILAFT completo y Excel de tres hojas (CU-12) |
| Alertas y recordatorios | Avisos de coincidencia, estudio pendiente y expiración (VE-13); el aviso previo a la próxima revisión queda pendiente (CU-22) |
| Monitoreo periódico (solo recalcula estados; nunca vuelve a consultar) | Reconsulta periódica automática de los vínculos activos (VE-12) y expiración al cambiar las personas (CU-8). **Mejorado** |
| PEP (banderas leídas del Portal) | PEP consultado en listas y factor automático de la matriz. **Mejorado** |
| Cualquier usuario con sesión decide | Permisos, miembro del área, conflicto de intereses y separación de funciones. **Mejorado** |

**Qué se agregó:**
- Matriz de riesgo configurable por empresa (factores, pesos, pisos, niveles y debida diligencia simplificada, ordinaria o intensificada con su periodicidad).
- Archivos del oficial y consulta de listas hecha por fuera.
- Rechazo directo del oficial. **Decisión abierta 3, resuelta:** sí puede, solo ante un hallazgo (CU-7).
- Expirar las listas al cambiar las personas relacionadas.
- Correo de resultado al tercero también cuando cierra un procesador.
- Credenciales de los proveedores de listas con una clave por grupo.
- Adaptador de Infolaft configurable y listo para su respuesta real.
- Informe SAGRILAFT completo.
- **Revisión automática con IA** (CU-13..CU-21): niveles Manual, Asistido y Automático, excepciones, tope mensual, muestreo y revisión humana, uso del cruce ficha↔documento de F5, y la bandeja `/excepciones`.

**Pendiente de H7:**
- Aviso previo a la próxima revisión de un tercero activo y renovación del estudio sin abrir un expediente.
- Medir la precisión del modo Automático con documentos reales (IA-7).
- **Infolaft** (lo que falta confirmar está en [15 §5](15-infolaft.md)):
  - formato de la respuesta, búsqueda por documento y si entrega PDF;
  - un usuario de pruebas;
  - rotar la credencial expuesta en ADPRO ([15 §2](15-infolaft.md)).
  - **cuenta única de la plataforma (G-2):** Infolaft es uno solo para todos los clientes; las credenciales de los proveedores de listas pasan de cada empresa (hoy, cifradas con la clave del grupo, CU-10) a la configuración de la plataforma, y el consumo por empresa (VE-11) se conserva para atribuir el costo.

### 4.5 H9 — pendientes
- ~~Sandbox con datos aislados~~ **Hecho (SB-1…SB-6, 2026-11-11):** tenant hermano con la configuración del grupo, buzón de correo, IA y listas simuladas y reinicio ([05 §2.6.3](05-api-contratos-y-webhooks.md), [07 §5.4](07-seguridad-identidad-y-tenancy.md)). Falta, como mejora, una pantalla en la consola con el buzón y el botón de reinicio (hoy es por API y SDK; la llave de prueba sí se emite desde Configuración › API keys).
- ~~Embebido del tercero para el Portal: `POST /sesiones-tercero` y el componente `<validasoft-expediente>`.~~ Resuelto en `feature/integracion-abierta` (IO-6) para cualquier sistema: `POST /sesiones-tercero`, `/embed/:componente` (bóveda, expediente, ficha) y `montarEmbebido` en el SDK. Pendiente: los eventos de negocio del iframe (`documento-subido`, `expediente-completo`) y un Web Component empaquetado.
- `POST /documentos` para integraciones.
- **Carga de la consulta de certificación:** el script k6 (`tests/carga`) y su medida local están hechos; la consulta pasó de cuatro viajes a la base a uno (p50 secuencial 23,5 → 8,1 ms), pero **p99 < 100 ms a 500 rps no se pudo certificar** en la máquina de desarrollo compartida ([11, H9](11-trazabilidad-por-hito.md)). Falta repetirla en un entorno dedicado; si no llega, la proyección persistida `EstadoCertificacionTercero` es el siguiente paso.
- **Publicar los paquetes NuGet y npm:** el empaquetado, la versión única, los README/CHANGELOG, ESM + CJS y el workflow `sdk-publicar.yml` están listos y probados sin publicar. Faltan **la decisión de licencia de Legal** (hoy `LICENCIA-SDK.md` es provisional), crear el entorno `publicacion-sdk` y los secretos `NUGET_API_KEY` y `NPM_TOKEN`, y poner el primer tag `sdk-v1.0.0` ([08 §2.4](08-integracion-y-migracion.md)). El SRM portó el verificador de firma porque el SDK no está publicado: cuando lo esté, debe adoptarlo.
- La caché de API keys se invalida al instante solo en el nodo que rota.
- Sandbox: las llaves de prueba emitidas antes de SB-2 (sin sandbox creado) responden 401 hasta que se cree con `POST /api/v1/sandbox`; no hay límite de tamaño del buzón ni retención automática del sandbox.

### 4.6 Plataforma e identidad
- ~~Ayuda por documento **por empresa**.~~ Hecho (OP-15, OP-16).
- **Hecho en `feature/plataforma-identidad` (PI-1…PI-19, 2026-11-11; sin PR):** SSO con el proveedor del cliente (OIDC genérico y WorkOS tras configuración, dominios verificados, JIT, reglas de grupos a roles, modo exclusivo); desactivar y reactivar sistemas y operadores; agregar empresas a un grupo desde la consola; idempotencia sin secretos; cuenta de los eventos cifrada con la clave del grupo; invalidación inmediata de llaves y sesiones en todos los servidores (PostgreSQL `LISTEN/NOTIFY`); uso de IA por cliente con su tarifa y exportación; step-up con passkey; passkeys en el campo del correo; aviso por correo de passkeys; auditoría de ingresos de los operadores y clave de arranque desde la consola; códigos de recuperación. Diseño en [07 §2.8](07-seguridad-identidad-y-tenancy.md) y contrato en [05 §2.11](05-api-contratos-y-webhooks.md).
- **Sigue pendiente (PI-19):** probar el SSO contra un proveedor real (Entra ID, Google Workspace) y WorkOS real —hoy solo hay simuladores—; SAML nativo y SCIM; TOTP; login propio de Plataforma con OIDC (PT-8); KMS por tenant (P-14-3: la clave del grupo se deriva de la maestra); y una prueba de integración del `409 sin-operadores-con-passkey` (la base de pruebas es compartida).
- Operación: cuando el primer operador tenga su passkey, **desactivar la clave de arranque desde Secretos › Clave de arranque**; los grupos que usen SSO deben registrar la URL de retorno en su proveedor.

### 4.7 H8 restante
- ~~Campañas de renovación.~~ Hecho en `feature/operacion-revision` (OP-1…OP-6, §3.1.4).
- ~~Delegación de revisión.~~ Hecho (OP-7…OP-9).
- ~~SLAs con alertas.~~ Hecho (OP-9…OP-14): plazo por área, alertas, indicador en la bandeja e informe de cumplimiento.
- ~~Habeas Data y supresión, ZIP firmado de la bóveda, plan y límites por tenant, dominio de correo propio y rendimiento de un lote de 1.000 filas.~~ Hechos en `feature/derechos-plan-correo` (OD-1…OD-24, abajo).

**Hecho en la rama `feature/derechos-plan-correo` (OD-1…OD-24, [registro §5-quinquagies](decisiones/registro.md),
[docs/11](11-trazabilidad-por-hito.md)):**
- Habeas data: solicitudes del tercero (consulta, corrección, supresión) a la plataforma o a una empresa, plazos de 10 y
  15 días hábiles con alertas, supresión con plan previo y retención por ley, crypto-shredding del texto personal y
  constancia sellada.
- ZIP firmado de la bóveda del tercero (manifiesto con SHA-256 y firma Ed25519), generado en segundo plano.
- Plan y límites por grupo (usuarios, empresas, vínculos activos, almacenamiento y llamadas), avisos al 80 % y al 100 %
  y bloqueo suave configurable; pantalla en Plataforma y «Plan del grupo» en Configuración.
- Dominio de correo propio por empresa con SPF, DKIM y DMARC verificados (proveedor simulado) y remitente de respaldo.
- Prueba de rendimiento de 1.000 filas: 12 s de procesamiento, 0 reintentos, +57 MB de memoria; sin cuellos de botella.

**Pendiente de esto (OD-12, OD-22):** prórroga de los plazos del habeas data; portabilidad más allá del ZIP; proveedor de
dominios y DNS real con revalidación periódica; facturación por plan; el almacenamiento solo bloquea los documentos propios
de un vínculo; el crypto-shredding cubre el texto de la solicitud, no lo declarado en los eventos de un documento retenido.

### 4.8 Piloto y producción (H10, H11)
- **H10:** piloto con un tenant real. El importador existe (F4), pero `OrigenPortalSqlServer` no se ha probado contra
  una base real.
- **H11:**
  - backups y restauración automatizada;
  - k6 y dashboards;
  - Object Lock y ClamAV;
  - retiro de los endpoints del legado.

### 4.9 E2E de todas las variantes
La suite tiene unas 25 specs (57 pruebas) y las ramas en curso suman las del proceso de validación. Faltan, como mínimo:
- ~~**Persona natural y jurídica en el wizard:** la natural no ve Legal ni Financiera y la jurídica exige socio.~~ **Hecha** (`e2e/pruebas/asistente-tipo-persona.spec.ts`, 2026-11-11): cada tipo recorre su asistente completo por la UI hasta pasar a revisión; lo pedido (cédula frente a Cámara; Legal y Financiera solo a la jurídica) y los campos de la general cambian con el tipo.
- ~~**Multiempresa con la ficha:** dos empresas validan la misma sección y una revalida tras un cambio.~~ **Hecha** (`e2e/pruebas/multiempresa-documento-global.spec.ts`): el mismo RUT y la misma información validados por dos empresas; con una versión nueva una revalida y la otra ve su aceptación desactualizada sin enterarse de la decisión de la primera (G-1).
- ~~**Integración externa completa con el SDK:** sistema → invitación → webhooks → consulta de certificación →
  embebido.~~ **Hecha en el sandbox** (`e2e/pruebas/integracion-sandbox.spec.ts`, 2026-11-11).
- ~~**Alta de una empresa nueva hasta certificar a un proveedor:** consola → invitación → carga → aprobación → certificado.~~ **Hecha** (`e2e/pruebas/alta-empresa-certificacion.spec.ts`): grupo y empresa desde la consola, configuración mínima, invitación, carga, aprobación y certificado verificable en `/verificar`.
- ~~**«Volver a postularme» de punta a punta:**~~ **Hecho** (`e2e/pruebas/volver-a-postularme.spec.ts`): rechazo con documentos, motivo visible para el tercero, nueva revisión, versión corregida, aprobación de las dos áreas y certificado.
- ~~**IA con el proveedor simulado:** cupo agotado y escalamiento.~~ **Hecha** (`e2e/pruebas/ia-cupo-y-escalamiento.spec.ts`, HI-1, HI-4).
- ~~**Aprobación de revalidaciones en lote** y eximir un requisito.~~ **Hecha** (`e2e/pruebas/revalidaciones-y-exencion.spec.ts`, HI-4).
- ~~**Verificaciones:** coincidencia confirmada (rechazo), estudio vencido y monitoreo.~~ **Hecha** (`e2e/pruebas/cumplimiento-vigilancia.spec.ts`, HI-2, HI-3, HI-4): encontró que el monitoreo no se podía ver ni resolver en el vínculo (ahora tiene su tarjeta) y que sin cupo de IA el documento esperaba diez minutos antes de pasar a una persona (HI-1).

### 4.10 Desaprobar, reintegrar sin revalidar y reinicio tras un rechazo (DS) — terminado en `feature/desaprobar-y-reintegrar`
Cierra tres huecos frente al SRM (decisiones DS-1..DS-10 del [registro](decisiones/registro.md)):
- **Desaprobar:** reabrir una etapa ya aprobada, volver a la fase inicial y desaprobar un expediente aprobado (el mismo
  expediente vuelve a revisión, el vínculo a `EnValidacion` y el certificado se revoca; al aprobarse de nuevo sale la
  emisión siguiente). Solo administradores, con justificación y con los veredictos revertidos en el historial.
- **Reintegrar sin revalidar** (`documental = 0` del SRM) solo si lo aprobado sigue vigente; si no, la pantalla explica
  por qué. Reintegrar revalidando ahora abre la renovación que el diseño prometía.
- **Reinicio tras un rechazo:** el flujo permite (con días de espera) que el tercero vea los motivos y vuelva a
  postularse; la empresa reabre cuando quiera desde el vínculo.
- E2E nuevos de esta zona: anular veredicto, desvincular, reintegrar con y sin revalidar, anular expediente y vínculo,
  varias etapas en secuencia, dos aprobadores y separación de funciones.

Pendiente:
- Correo y campana al tercero por una desaprobación o un reintegro (hoy solo se publican `expediente.desaprobado` y
  `vinculo.reintegrado`).
- El reintegro sin revalidar no compara contra requisitos que el flujo agregó después de aprobar.
- La política de reintento se lee del flujo vigente del vínculo, no de la versión que rechazó, y no limita cuántas veces.
- «Reintegrar sin revalidar» de un vínculo desvinculado antes de este cambio no tiene `EstadoAlDesvincular` guardado:
  se trata como «no se sabe» y se pide revalidar.

## 5. Clave de OpenAI por empresa (lista para cargar)

- **Dónde:** consola `/plataforma` → la empresa → «Plan de IA» → API key. También se puede con
  `PUT /api/v1/plataforma/tenants/{t}/empresas/{e}/plan-ia`.
- **Cómo se guarda:**
  - cifrada con AES-256-GCM y la clave maestra `Validasoft:Secretos:ClaveMaestra`, versionada y rotable (docs/07
    §6.3);
  - nunca vuelve en claro; solo se muestran los últimos 4 caracteres;
  - no puede ser un hash, porque hay que enviarla a OpenAI.
- **Probarla:** «Probar llave» consulta `GET /models` de OpenAI y no consume tokens.
- **En producción:** configurar `ClaveMaestra`. Sin ella, la API no arranca.
- **Sin llave no hay análisis de IA ni cruce de la ficha:** la revisión es manual.

## 6. Cómo trabajar y retomar

- **Quién escribe el código:**
  - un agente con Sonnet 5.5 en esfuerzo xhigh implementa, incluidos los arreglos de fusiones y de pruebas;
  - el modelo del chat valida (diff, compilación, pruebas, capturas), opera git y los PR y documenta;
  - cuando el límite semanal no lo permite, el código lo escribe el modelo del chat.
- **Reglas visuales obligatorias (UI-1..UI-6):**
  - skeleton al cargar;
  - botones compactos;
  - selects bien hechos y en español;
  - nada cuadrado suelto;
  - sin layouts volcados a la izquierda;
  - avisos agrupados.
- **Ramas:** una por tarea, desde `main`. Los PR se fusionan con el CI en verde, sin esperar (autorizado para este
  proyecto). Si `main` avanzó, se rebasa antes de fusionar.
- **Compilar:** con `--artifacts-path` propio y `MSBUILDDISABLENODEREUSE=1`, porque la API de desarrollo bloquea los
  DLL.
- **Cliente:** regenerarlo con una API temporal: `openapi:actualizar` y luego `generar:api`.
- **E2E:** aislada por defecto. Para correr dos suites a la vez: `E2E_PUERTO` y `E2E_BASE_DATOS` distintos.
- **Entorno local del usuario:**
  - API en :5080, web de empresa en :5180 y portal en :5181, todo desde el checkout `h1-frontend` en `main`;
  - ese checkout tiene la `.semilla.local.json` de la web, que no se toca;
  - para ver otra rama, mover ese checkout a ella;
  - la API se recompila en una carpeta de artefactos nueva y se reinicia.

## Decisiones abiertas

**Sección G cerrada (2026-11-11).** Producto resolvió las decisiones 2, 4 y 5 (registro [G-1…G-4](decisiones/registro.md#5-tresquinquagies-decisiones-de-producto-de-la-sección-g-2026-11-11)); solo queda abierta la 1.

1. 2FA: ¿en Validasoft o en el sistema integrador? (§4.6)
2. ~~Infolaft: ¿proveedor único para los clientes de Sinco o uno por cliente? ¿Se usa desde el SRM durante la coexistencia? ([15](15-infolaft.md))~~ Resuelta en G-2 y G-4: **uno solo para todos los clientes, con la cuenta de la plataforma**, y solo desde Validasoft. Pendiente de implementar: pasar las credenciales de los proveedores de listas de cada empresa a la configuración de la plataforma (§4.4).
3. ~~¿El oficial de cumplimiento puede rechazar el expediente directamente?~~ Resuelta en CU-7: sí, solo ante un hallazgo (§4.4).
4. ~~Señal entre empresas: ¿debe compartirse algún hecho grave sin revelar quién decidió?~~ Resuelta en G-1: **no se comparte ningún hecho grave; las decisiones de cada empresa son privadas.**
5. ~~Integración con el SRM (§3.2): cuándo se sube la rama, cuenta de servicio de AyF y «Bloqueado».~~ Resuelta en G-4: **todavía no; primero se prueba Validasoft solo, sin integrarlo en ningún sistema.** La rama del SRM sigue en espera (§3.2).
6. ~~Nueva vigencia tras renovar: ¿desde la aprobación o desde el vencimiento anterior?~~ Resuelta en G-3 (ya era VV-12): **desde la aprobación.**
