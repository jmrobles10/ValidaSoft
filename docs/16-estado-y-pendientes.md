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
| `feature/vigencia-vinculo` | 2, 5 y desbloqueo | Vencimiento periódico del vínculo (días desde la última aprobación, avisos, gracia), estado `PorVencer`, vigencia opcional por sección, renovación con **ratificación de secciones**; plazo de revalidación configurable (`DiasRevalidacion`); desbloqueo automático al llegar la fecha `Hasta`. **Terminada y verificada; falta su PR.** Registro VV- (sección «5-unquadragies») |
| `feature/desaprobar-y-reintegrar` | 3, reintegrar y reinicio | Reabrir una etapa ya aprobada, desaprobar un expediente aprobado y volver a la fase inicial, con justificación; reintegrar sin revalidar; «Volver a postularme» tras un rechazo. Más los e2e que faltaban: varias etapas, dos aprobadores, separación de funciones, anular veredicto, desvincular, reintegrar, anular. Registro DS- |
| `feature/novedades-tercero` | 4 | Campana del tercero en el portal (de todas sus empresas), preferencias de correo, recordatorio de observaciones pendientes individual y masivo (uno al día por expediente), y aviso a las áreas cuando el tercero responde. Registro NT- |

### 3.2 SRM — aplicar en AyF (rama local, en espera)
Rama **local** `feature/validasoft-ayf` en `C:\Fuentes_Git\SRM` (sale de `Feature_INT_NOVIEMBRE_SRM`, 6 commits, **sin
push**). Por decisión de Producto queda en espera hasta validar todo el alcance de Validasoft; no se sube ni se crea
WI. Detalle en `BackEnd\INTEGRACION_VALIDASOFT.md` de esa rama.
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
   `feature/vigencia-vinculo` (VV-1..VV-8, [03 §5.4](03-dominio-y-eventos.md)): el flujo configura días, avisos y gracia
   (`dias = 0` mantiene el comportamiento anterior); el vínculo pasa a `PorVencer` en el primer aviso y a `Vencido` al
   terminar la gracia, con su certificado; la renovación pide **ratificar** (sin cambiar datos) o **actualizar** las
   secciones de la ficha y los documentos obligatorios no vigentes; cada sección puede traer su vigencia.
3. **Desaprobar una etapa ya aprobada o volver a la fase inicial:** en curso.
4. **Campana del tercero y recordatorio de observaciones:** en curso.
5. **Plazo de revalidación configurable:** **resuelto** en la misma rama (VV-7): rige el menor `diasRevalidacion` de los
   requisitos de la actualización (tipo de documento o sección del flujo), 15 por omisión.
6. **Desbloqueo automático, reintegrar sin revalidar, reinicio tras rechazo:** el desbloqueo automático está **resuelto**
   en `feature/vigencia-vinculo` (VV-8: mensaje programado de Wolverine al bloquear con fecha, respaldado por el barrido
   del job de vigencias); reintegrar sin revalidar y el reinicio tras rechazo siguen en curso.

**Pendiente de lo resuelto en `feature/vigencia-vinculo`:** abrir la renovación en el primer aviso (ratificar **antes** de
vencer) es decisión de Producto; un vínculo vencido por dos documentos a la vez solo abre la renovación con el primero
(RN-1); el aviso de vigencia aún no es una plantilla personalizable (PL-1); los vínculos con vigencia o bloqueo anteriores
al cambio se indexan con su próximo evento (o reconstruyendo la proyección del vínculo).

### 4.2 F6 — sin empezar
- Personas relacionadas (representantes, socios) con **bóveda propia**: su cédula y sus documentos se comparten entre
  las empresas como los del tercero.
- **Cargo del contacto por empresa**: el mismo tercero tiene contactos distintos para cada empresa.

### 4.3 Ficha — pendientes (FM-38, FM-55)
- Banco con código CENIT (catálogo `bancos`), SWIFT y contacto de pagos, que hoy envía el SRM a AyF.
- Lugar de expedición del documento del representante (AyF `Expedicion`). `ExentoReteCREE`.
- Prellenar General con lo que ya se sabe del tercero (razón social y correo de la invitación).
- Incluir las observaciones en el DTO de la ficha; hoy el portal hace N peticiones.
- La web aún no muestra la ficha del vínculo ni el evento `seccion.aceptada` en la configuración de webhooks.
- Tras F5:
  - la revisión automática de H7 aún no usa el cruce;
  - la certificación bancaria no está en el mapa de respaldo;
  - una sección que cambia sin documento nuevo no se marca «sin respaldo» (FM-15);
  - la cédula respalda solo la General.

### 4.4 H7 — pendientes
- Revisión automática con IA (motor de decisión, excepciones, presupuesto y revisión humana) y la cola `/excepciones`.
- Expirar las verificaciones al cambiar el representante.
- Evidencia manual: subir el PDF de una consulta hecha por fuera y que el oficial adjunte archivos al estudio.
- Matriz de riesgo.
- Cifrar las credenciales con una clave por tenant (hoy usan la de la instalación).
- Si el cierre lo provoca el procesador, al tercero no le llega el correo de resultado.
- **Infolaft:**
  - confirmar el formato de la respuesta, la búsqueda por documento y si hay PDF por API;
  - pedir un usuario de pruebas;
  - rotar la credencial expuesta en ADPRO ([15 §2](15-infolaft.md)).

### 4.5 H9 — pendientes
- Sandbox con datos aislados (hoy `sandbox` solo cambia el prefijo de la llave).
- Embebido del tercero para el Portal: `POST /sesiones-tercero` y el componente `<validasoft-expediente>`.
- `POST /documentos` para integraciones.
- Proyección `EstadoCertificacionTercero` medida con k6 (500 rps, p99 < 100 ms).
- Publicar los paquetes NuGet y npm (el SRM portó el verificador de firma porque el SDK no está publicado).
- La caché de API keys se invalida al instante solo en el nodo que rota.

### 4.6 Plataforma e identidad
- Login propio de Plataforma con OIDC y 2FA; hoy entra con la clave de arranque más identidad delegada.
- 2FA de usuarios (P-14-4): definir si lo resuelve el sistema integrador o Validasoft.
- Desactivar sistemas y operadores de Plataforma por API; agregar empresas a un grupo desde la consola.
- `POST /api-keys` y `POST /sesiones-usuario` todavía guardan su respuesta en el registro de idempotencia 24 h.
- Ayuda por documento **por empresa** (hoy es por tenant, B-4).

### 4.7 H8 restante
- Campañas de renovación.
- Delegación de revisión.
- Habeas Data y supresión.
- ZIP firmado de la bóveda.
- Plan y límites por tenant.
- Dominio de correo propio.
- SLAs con alertas.
- Rendimiento de un lote de 1.000 filas.

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
- **Persona natural y jurídica en el wizard:** la natural no ve Legal ni Financiera y la jurídica exige socio.
- **Multiempresa con la ficha:** dos empresas validan la misma sección y una revalida tras un cambio.
- **Integración externa completa con el SDK:** sistema → invitación → webhooks → consulta de certificación →
  embebido.
- **Alta de una empresa nueva hasta certificar a un proveedor:** consola → invitación → carga → aprobación →
  certificado.
- **IA con el proveedor simulado:** cupo agotado y escalamiento.
- **Aprobación de revalidaciones en lote** y eximir un requisito.
- **Verificaciones:** coincidencia confirmada (rechazo), estudio vencido y monitoreo.

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
1. 2FA: ¿en Validasoft o en el sistema integrador? (§4.6)
2. Infolaft: ¿proveedor único para los clientes de Sinco o uno por cliente? ¿Se usa desde el SRM durante la
   coexistencia? ([15](15-infolaft.md))
3. ¿El oficial de cumplimiento puede rechazar el expediente directamente? (§4.4)
4. Señal entre empresas: hoy el bloqueo o rechazo de una empresa es privado y no se ve en otra. ¿Debe compartirse
   algún hecho grave (p. ej. una coincidencia confirmada en listas restrictivas) sin revelar quién decidió?
5. Integración con el SRM (§3.2): cuándo se sube la rama, quién aporta la cuenta de servicio de AyF, y cómo se
   representa «Bloqueado» en el SRM y en AyF.
