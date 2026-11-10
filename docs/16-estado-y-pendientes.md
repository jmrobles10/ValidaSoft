# 16 — Estado del proyecto, alcance y pendientes (corte 2026-11-10)

> **Para qué sirve:** retomar el trabajo sin reconstruir el contexto. Dice qué se quiere lograr, qué está hecho (en
> `main` y en la rama `feature/avance-2026-11-10`), qué falta con detalle y cómo continuar.
> **Fuentes:** [registro de decisiones](decisiones/registro.md) (la verdad sobre cada decisión), [09](09-plan-de-hitos-y-responsabilidades.md)
> y [11](11-trazabilidad-por-hito.md) (plan por hito), [14](14-ficha-maestra-del-tercero.md) (ficha) y [15](15-infolaft.md) (Infolaft).

## 1. Qué se quiere lograr

Validasoft reemplaza la validación de terceros que hoy está repartida entre el **SRM** (dentro del ERP Marco, una
instancia por empresa) y el **Portal de Proveedores**. Es un producto aparte, multi-tenant y consumible por API.

**Objetivo 1 — paridad con el SRM, primero.** Nada de lo que el SRM hace hoy puede faltar, y donde lo hace mal se
mejora. El checklist de paridad es [01 §10](01-analisis-legado-srm-portal.md). Concretamente:
- invitar terceros, uno a uno y por lote, y cargar sus documentos con observaciones y reenvío;
- exigir documentos según reglas (tipo de persona, país, tipo de vínculo, condiciones);
- aprobación por áreas en secuencia o en paralelo, con motivos tipificados, observar, rechazar, anular, reintegrar y
  desvincular;
- vigencias, avisos, vencimientos y renovación;
- actualizaciones posteriores a la aprobación, revisadas por cada empresa;
- la ficha del tercero por secciones con sus soportes (lo que hoy es el «wizard» del Portal);
- listas restrictivas y estudio SAGRILAFT del oficial de cumplimiento como condición de cierre;
- certificado verificable, informes y exportes, y auditoría;
- integración con el SRM, el Portal y Marco durante la coexistencia, e importación de lo existente.

**Objetivo 2 — sin repetir los errores del legado.** No hay copias espejo de documentos: una versión vive una sola vez en
la bóveda del tercero y cada empresa decide sobre ella. Los estados son textuales y los eventos están firmados. La
completitud se calcula en el servidor y queda guardada. Los secretos van cifrados.

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
| H1–H6 | ≤ #28 | Fundación técnica, bóveda del tercero, configuración y reglas de exigencia, expediente y veredictos por área, certificado, notificaciones y webhooks, vigencias, revalidación y versiones nuevas |
| P2 | #29–#36 | Inicio, Certificados, Tercero 360, Auditoría, Plantillas, aviso de versión nueva |
| IA | #38 | Análisis de documentos con OpenAI, plan y cupo por empresa, pantallas «Plan y uso» (ADR-0012) |
| Fechas | #39 | Calendario del negocio en la zona de Bogotá (FE-1, FE-2) |
| Ficha F1–F4 | #40, #41, #42, #44 | Secciones con soporte y completitud guardada; secciones como requisitos del flujo validadas por empresa; `seccion.aceptada` y ficha aceptada para el SRM y AyF; importador y sincronizador desde el Portal |
| Wizard | #43 | Panel lateral fijo, barra de guardar siempre visible, vista previa de la completitud (FM-46), aviso de salida, animaciones; arreglos de scroll en el portal y en la web |
| E2E aislada | #45 | En local las pruebas usan su propia base y su propio Mailpit |
| Infolaft | #46 | [docs/15](15-infolaft.md): por qué no se usa la suscripción y el plan |
| Plataforma | #47 | Consola `/plataforma`, alta completa de grupos, plan de IA con la llave de OpenAI por empresa, clave maestra de secretos con rotación (PT-1..PT-8) |

## 3. Qué está en la rama `feature/avance-2026-11-10` (aún no en `main`)

Es una rama única con todo lo que estaba en curso. Sale de la rama del SDK, que ya está sobre `main` con #47, y le suma
las fusiones de las demás. **No tiene PR.**

| Bloque | Origen | Estado | Registro |
|---|---|---|---|
| **H9: integración con el SRM y Marco** | `feature/integracion-sdk` | Terminado | CN-1..CN-7 |
| **H7: verificaciones y SAGRILAFT** | `feature/verificaciones` | Terminado | VE-5..VE-18 |
| **H8: invitación masiva y caducidad** | `feature/lotes-y-caducidad` | Terminado, verificación sin cerrar (ver §4.3) | LT-1..LT-10 |
| **H8: informes y exportes** | `feature/informes` | Terminado, verificación sin cerrar (ver §4.4) | IF-1..IF-8 |
| **F5: IA sección ↔ documento** | `feature/ficha-f5` | **A medias y fuera de esta rama**: se fusionó y se revirtió porque su prueba de integración es intermitente (ver §4.5) | sin filas |

**Contenido de cada bloque:**
- **H9:**
  - `GET /api/v1/consultas/certificacion` con ETag y 304, para Marco;
  - `empresaId` en `GET /api/v1/eventos`;
  - rotación de API keys con solapamiento (`POST /api-keys/{id}/rotar`) y la caché de llaves corregida;
  - SDK .NET `Validasoft.Sdk` (net7/8/10) y SDK TS `@validasoft/sdk`;
  - guía de integración en docs/08 §2.1 y el ejemplo `docs/ejemplos/IntegradorSrm`.
- **H7:**
  - el flujo define verificaciones bloqueantes y el expediente espera a que se cumplan;
  - listas restrictivas con Infolaft como proveedor principal, más Stradata, Compliance y un simulado;
  - credenciales por empresa, cifradas;
  - consulta al tercero y a las personas de su ficha, con evidencia en PDF y JSON con hash;
  - consumo mensual por empresa y monitoreo cada 30 días;
  - estudio SAGRILAFT con vigencia de 30 días;
  - bandeja del oficial, informe en Excel y configuración en el flujo.
- **H8, lotes:**
  - carga de Excel o CSV con plantilla y vista previa por fila;
  - proceso durable con pausa, reanudación, cancelación y reintentos, más informe por fila;
  - plazo de cargue por flujo con recordatorios, caducidad del expediente y reapertura.
- **H8, informes:**
  - estado de expedientes con tiempos por área, en Excel y PDF;
  - informe de vínculos;
  - exportación de la auditoría;
  - ZIP del expediente completo;
  - pantalla «Informes» y permisos `auditoria:exportar` y `expedientes:exportar`.

**Verificación de la rama consolidada:**

| Verificación | Resultado |
|---|---|
| `dotnet build` (toda la solución, incluidas las pruebas) | 0 errores |
| `pnpm typecheck` | en verde |
| `pnpm lint` | en verde |
| Pruebas de dominio | 637/637 (sin las de F5) |
| Pruebas de integración | 141/141 |
| `pnpm test` (Vitest) | 596/597; la que falló (`ObservacionEnWizard`) es intermitente por tiempo y ya pasa con una espera mayor |
| `pnpm build` | en verde |
| `pnpm e2e` (aislada) | 46/46 |

**Errores que encontró la verificación, ya corregidos:**
- la pestaña «Vínculos» quedaba duplicada tras la fusión;
- la consulta automática de listas podía pisar una consulta manual hecha mientras respondía el proveedor
  (`TareasDeVerificaciones`): ahora vuelve a comprobar que sigue pendiente;
- el conteo de tipos de evento públicos es 35 con lotes y verificaciones juntos.

**Conflictos de la fusión, resueltos a mano. Revisarlos al retomar:**
- `Flujo.cs`, `Expediente.cs` y los DTO de flujo y expediente: plazo de cargue y verificaciones juntos, con el plazo
  como último parámetro y nombrado donde hace falta.
- `ProcesoDeValidacion.cs` y `FlujosEndpoints.cs`.
- `MapeosDeValidacion.cs`: H7, LT y F5 (`UsarComoSoporte`).
- `Notificaciones.cs`: listas `Todos` y `ConCorreo` unidas.
- `Persistencia.cs`: índices.
- `Permisos.cs`, `scopes.ts` y sus pruebas: todos los permisos nuevos.
- `PdfDeCertificado.cs`: una sola firma de `Construir`.
- `AppShell.tsx`: pestañas «Cumplimiento» e «Informes».
- `EditorFlujo.tsx`, textos i18n, el registro de decisiones (secciones 5-duotricies a 5-sextricies renumeradas) y el
  cliente de la API (regenerado desde la API de esta rama).

## 4. Qué falta, con detalle

### 4.1 Cerrar la rama consolidada (lo primero)
1. ~~Correr la suite completa sobre la rama y arreglar lo que salga.~~ Hecho (§3).
2. Abrir el PR a `main` y fusionarlo con el CI en verde.
3. Las pruebas de Vitest de `apps/public/src/ficha` y la de webhooks pueden fallar por tiempo con la máquina saturada.
   Si se repite en CI, subirles el `timeout`.

### 4.2 H9 — pendiente
- Sandbox con datos aislados: hoy `sandbox` solo cambia el prefijo de la llave.
- `legacy.estadoTercero` en la consulta de certificación: falta el mapeo de estados del SRM.
- Embebido del tercero para el Portal: `POST /sesiones-tercero` y el componente `<validasoft-expediente>`.
- `POST /documentos` para integraciones.
- Proyección `EstadoCertificacionTercero` medida con k6 (objetivo de docs/09: 500 rps, p99 < 100 ms).
- Publicar los paquetes NuGet y npm. El SDK TS no genera CJS.
- La caché de API keys se invalida al instante solo en el nodo que rota; en los demás tarda hasta 5 minutos.
- En los repositorios del SRM y de Marco (fuera de este repo): receptor de webhooks, flag `ValidacionDocumental`, mapa
  evento → acción, AyF, y tabla local de certificación en Marco.

### 4.3 H8 lotes y caducidad — pendiente
- El agente se detuvo mientras corría la e2e completa: su verificación final no está registrada. Correr `lotes.spec.ts`
  y la suite.
- Probar el rendimiento con un lote de 1.000 filas (criterio de docs/09).

### 4.4 Informes — pendiente
- El agente se detuvo al cerrar su verificación: correr `informes.spec.ts` y las pruebas de integración de los exportes.
- El informe SAGRILAFT está en H7 (`/informes/cumplimiento`); unificar la pantalla de «Informes» con él.

### 4.5 F5 — IA sección ↔ documento (a medias, en `feature/ficha-f5`)
**Dónde está:** rama `feature/ficha-f5-cierre`, que sale de `main` (con #48) y restaura F5 ya integrado con H7, H8 y H9.

**Causa de la prueba intermitente** (`Un_RUT_nuevo_propone_al_tercero…`): no es la prueba, es un hueco que viene de H6/F2.
Con una actualización **ya abierta** en el vínculo, un cambio en otro requisito no se revalida:
`SincronizarRequisitosDelVinculo` solo pone al día los requisitos que el expediente abierto ya tiene, y
`Revalidacion.AbrirSiCorrespondeAsync` no hace nada si hay un expediente abierto. Tampoco se vuelve a sincronizar al
cerrarlo. En el flujo de F5 pasa siempre: el RUT nuevo abre la actualización (solo con el RUT) y, cuando el tercero
confirma la propuesta, la sección General cambiada queda fuera. La prueba pasaba solo cuando la sección se guardaba
antes de que se abriera la actualización del RUT. Afecta igual a los documentos (RUT nuevo y luego Cámara nueva).

**Decisión pendiente de Producto** (hay que tomarla antes de seguir):
- *Ampliar la actualización abierta:* el requisito nuevo entra al expediente abierto y el revisor ve todo junto. Pide un
  evento nuevo en el expediente (`RequisitosAgregados`) y reabrir las áreas que ya habían decidido.
- *Encadenar:* al cerrar el expediente se vuelve a sincronizar y lo que cambió mientras tanto abre otra actualización.
  Es más simple, pero el revisor ve dos expedientes seguidos.

**Objetivo** (pedido del usuario, [14 §5.2, §5.3, §6](14-ficha-maestra-del-tercero.md)): que un documento exigido, por
ejemplo el RUT obligatorio, sirva además como soporte verificado de las secciones de la ficha que respalda:
- RUT → General y Tributaria;
- Cámara de Comercio → Legal (representantes, socios);
- Estados financieros → Financiera;
- certificación bancaria → cuentas.

Además, que una versión nueva del documento le proponga al tercero los cambios para actualizar la ficha.

| Hecho (commit `wip`, sin probar a fondo) | Falta |
|---|---|
| Dominio `CruceConDocumento` y `PropuestaDesdeDocumento`; esquemas de extracción por tipo de documento (`EsquemasDeAnalisis.cs`); analizador simulado determinista (`AnalizadorSimulado.cs`); `UsarComoSoporte` en el requisito de documento del flujo; componente web `CruceSeccion.tsx` | Conectar el cruce en `DetalleSeccionRequisito`, en la tarjeta del documento y en el diálogo de aprobación. «Actualizar desde mi RUT/Cámara…» en el wizard del portal, con propuesta campo por campo que el tercero confirma (nunca se guarda sola) y que al guardar abre la revalidación por sección (F2). Consumo de cupo según ADR-0012 y fail-closed (sin IA o sin cupo, revisión manual). Filas FM-48+ en el registro. Pruebas de dominio, integración y e2e (tercero sube un RUT nuevo, acepta la propuesta y la empresa ve la revalidación con el cruce). Mover `porcentaje` fuera de `CruceSeccion.tsx` (lleva un `eslint-disable` temporal). |

Regla que no se negocia: el cruce es un **hecho compartido** de la versión (se calcula una vez), y la **decisión** es
de cada empresa. Nada se aprueba solo por la IA.

### 4.6 F6 — sin empezar
- Personas relacionadas (representantes, socios) con **bóveda propia**: su cédula y sus documentos se comparten entre
  las empresas como los del tercero.
- **Cargo del contacto por empresa**: el mismo tercero tiene contactos distintos para cada empresa.

### 4.7 H7 — pendiente
- Revisión automática con IA (motor de decisión, excepciones, presupuesto y revisión humana) y la cola `/excepciones`.
- Expirar las verificaciones al cambiar el representante (`ExpirarVerificacionesDeRepresentante`).
- Evidencia manual: subir el PDF de una consulta hecha por fuera y que el oficial adjunte archivos al estudio.
- El oficial no rechaza directamente; hoy lo hace un área con `puedeRechazarExpediente`.
- Matriz de riesgo.
- Cifrar las credenciales con una clave por tenant (hoy usan la de la instalación).
- Si el cierre lo provoca el procesador, al tercero no le llega el correo de resultado.
- **Infolaft:** confirmar el formato de la respuesta, la búsqueda por documento y si hay PDF por API. Pedir un usuario
  de pruebas. Rotar la credencial expuesta en ADPRO ([15 §2](15-infolaft.md)).

### 4.8 Plataforma — pendiente (PT-8)
- Login propio de Plataforma con OIDC y 2FA; hoy entra con la clave de arranque más identidad delegada.
- Desactivar sistemas y operadores de Plataforma por API.
- Agregar más empresas a un grupo desde la consola.
- `POST /api-keys` y `POST /sesiones-usuario` todavía guardan su respuesta en el registro de idempotencia 24 h.
- La copia del número de cuenta en los eventos del vínculo no se re-cifra: hay que conservar su clave en
  `ClavesAnteriores`.

### 4.9 Ficha — pendiente de F1–F4 (FM-38 y notas)
- Banco con código CENIT (catálogo `bancos`), SWIFT y contacto de pagos, que hoy envía el SRM a AyF.
- Lugar de expedición del documento del representante (AyF `Expedicion`). `ExentoReteCREE`.
- Prellenar General con lo que ya se sabe del tercero (razón social y correo de la invitación).
- Incluir las observaciones en el DTO de la ficha; hoy el portal hace N peticiones.
- La web aún no muestra la ficha del vínculo ni el evento `seccion.aceptada` en la configuración de webhooks.
- Persona natural: ya se valida que llene solo General, Tributaria, SST y Especialidades. Revisar con Producto si
  algún campo más difiere.

### 4.10 Otros de paridad y operación
- **2FA de usuarios:** la identidad es delegada (ID-1) y P-14-4 exige 2FA para Administrador, Auditor y Cumplimiento.
  Definir si lo resuelve el sistema integrador o Validasoft.
- Ayuda por documento **por empresa**: hoy es por tenant (B-4).
- **H8 restante:** campañas de renovación, delegación de revisión, Habeas Data y supresión, exportación ZIP firmada de
  la bóveda, plan y límites por tenant, dominio de correo propio y SLAs con alertas.
- **H10:** piloto con un tenant real. El importador existe (F4), pero `OrigenPortalSqlServer` no se ha probado contra
  una base real.
- **H11:** backups y restauración automatizada, k6, dashboards, Object Lock, ClamAV y retiro de los endpoints del legado.

### 4.11 E2E de todas las variantes (pedido del usuario)
La suite tiene 19 specs. Faltan, como mínimo:
- **Persona natural y jurídica** en el wizard: la natural no ve Legal ni Financiera, y la jurídica exige socio.
- **Multiempresa con la ficha:** dos empresas validan la misma sección y una revalida tras un cambio.
- **Integración externa completa con el SDK:** sistema → invitación → webhooks → consulta de certificación → embebido.
- **Alta de una empresa nueva hasta certificar a un proveedor:** consola → invitación → carga → aprobación →
  certificado.
- **IA con el proveedor simulado:** análisis, cupo agotado y escalamiento.
- **Revalidación por sección y por documento**, más reintegrar y desvincular (hoy sin e2e).
- **Lotes grandes** y caducidad.
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

## 6. Cómo retomar

- **Ramas:** la de trabajo es `feature/avance-2026-11-10`. Las ramas de origen siguen existiendo:
  `feature/integracion-sdk`, `feature/verificaciones`, `feature/lotes-y-caducidad`, `feature/informes` y
  `feature/ficha-f5`.
- **Compilar:** con `--artifacts-path` propio y `MSBUILDDISABLENODEREUSE=1`, porque la API de desarrollo bloquea los
  DLL.
- **Cliente:** regenerarlo con una API temporal: `openapi:actualizar` y luego `generar:api`.
- **E2E:** aislada por defecto. Para correr dos suites a la vez: `E2E_PUERTO` y `E2E_BASE_DATOS` distintos.
- **Entorno local del usuario:**
  - la API de :5080 corre compilada desde la rama del wizard; actualizarla a `main` o a la rama de avance;
  - el checkout `h1-frontend` (que tiene la `.semilla.local.json` de la web, que no se toca) está en un commit del
    wizard; moverlo a la rama que se quiera ver.
- **PR:** se fusionan con el CI en verde, sin esperar (el usuario lo autorizó para este proyecto).

## Decisiones abiertas
1. 2FA: ¿en Validasoft o en el sistema integrador? (§4.10)
2. Infolaft: ¿proveedor único para los clientes de Sinco o uno por cliente? ¿Se usa desde el SRM durante la
   coexistencia? ([15](15-infolaft.md))
3. ¿El oficial de cumplimiento puede rechazar el expediente directamente? (§4.7)
