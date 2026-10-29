# Glosario

| Estado | Fecha | Responsable |
|---|---|---|
| Borrador | 2026-10-28 | Arquitectura |

**Propósito.** Fijar el lenguaje ubicuo de Validasoft: un solo nombre por concepto, en español, usado igual en el código, la API, la interfaz y los documentos. Cada término trae definición, ejemplo y su equivalente en el legado (SRM/Portal) para quien viene de allí.

**Relacionados**
- [01 Análisis del legado SRM ↔ Portal](01-analisis-legado-srm-portal.md)
- [02 Modelo documental compartido](02-modelo-documental-compartido.md)
- [03 Dominio y eventos](03-dominio-y-eventos.md)

---

## 1. Organización y actores

| Término | Definición | Ejemplo | Equivalente en el legado |
|---|---|---|---|
| **Tenant (Grupo)** | Cliente de Validasoft: una organización o grupo empresarial con una o varias razones sociales. Comparte usuarios, catálogo, flujos, plantillas, API keys y facturación. Todo dato de `validacion` lleva `TenantId`. | Grupo Acme | `Constructoras` (Portal) agrupadas a mano; una instalación del SRM por razón social |
| **Empresa (interna)** | Razón social dentro del tenant que exige, revisa y certifica. Tiene sus vínculos y sus decisiones, **aisladas de las demás empresas del mismo tenant** (P-11-2, ADR-0008). Todo evento de validación lleva `EmpresaId`. | Acme Construcciones S.A.S. y Acme Inmobiliaria S.A.S., ambas del Grupo Acme | `Constructoras` (Portal); una instalación del SRM |
| **Plataforma** | Sincosoft como operador del SaaS: alta de tenants y empresas, catálogo canónico, catálogo de tipos de identificación por país, IA, soporte. Panel `/plataforma`. | — | No existía como rol |
| **Administrador** | Usuario del tenant con control total: empresas, flujos, áreas, usuarios y roles por empresa, API keys, webhooks, niveles de revisión. | Jefe de compras del grupo | Perfil Administrador del flujo + usuario 50 |
| **Revisor de área** | Usuario con rol en una o varias empresas del tenant que emite veredictos sobre los requisitos de su área. | Analista jurídico del área Legal de Acme Construcciones | `PerfilAprobacion` / usuario responsable en `SectoresActualizacion` |
| **Auditor** | Rol de solo lectura sobre todas las empresas del tenant, incluida la línea de tiempo y los accesos a documentos. | Revisoría fiscal | No existía |
| **Tercero** | Persona natural, jurídica o consorcio identificada por `(país, tipo de identificación, número normalizado)` sin dígito de verificación; **multipaís** mediante el catálogo de tipos de identificación. Única a nivel global del SaaS. Dueña de su bóveda. Las personas relacionadas también son terceros. | Ferretería El Tornillo S.A.S., CO·NIT·900123456 (DV 7) | `Terceros` (Portal) + `Solicitudes.SolNIT` (SRM) |
| **Persona relacionada / Relación entre terceros** | Tercero persona natural unido a otro tercero por una relación tipada `RepresentanteLegal`, `Suplente`, `Socio`, `BeneficiarioFinal` o `MiembroDe`, con `principal`, vigencia y fuente (declarada, extraída de documento o importada). Tiene **bóveda propia**: su cédula, antecedentes y PEP se validan una vez y sirven a todas las empresas y a todos los terceros donde figura (B-3, ADR-0010). | Juan Pérez, CC 80123456, representante legal principal de El Tornillo y suplente de Beta | Representante en `TerCamaraComercio` + contactos tipo `RepresentanteLegal` |
| **Plataforma (acceso de soporte)** | El rol Plataforma **nunca** ve documentos ni decisiones por defecto; un Administrador del tenant puede conceder acceso temporal con alcance (empresa, expediente, tercero) y horas, auditado con actor `Plataforma` (B-6). | Soporte revisa un expediente concreto durante 4 h | No existía |
| **Tipo de identificación** | Entrada del catálogo de plataforma por país con validador y normalización. | CO: NIT, CC, CE, PA; MX: RFC; AR: CUIT; PE: RUC | `TipoDocumento` texto libre |
| **Tipo de vínculo** | Naturaleza de la relación entre una empresa y un tercero. Catálogo por tenant. | Proveedor, Cliente, Empleado, Arrendatario, Paciente | Solo existía "proveedor" |
| **Vínculo** | Relación `(Tenant, Empresa, Tercero, Tipo de vínculo)`. Agregado de vida larga con su estado, su vigencia, sus versiones aceptadas y su cuenta designada. | El Tornillo como proveedor de Acme Construcciones | `TercerosConstructora` (Portal) + `Solicitudes` (SRM) |
| **Sistema integrador (Integración)** | Sistema externo con API key del tenant (con alcance a una o todas sus empresas) que crea vínculos, consulta certificación y recibe webhooks. | SRM de Acme, Marco, el Portal | `TokenAdproveedor`/`ApiKey` por constructora |

## 2. Configuración

| Término | Definición | Ejemplo | Equivalente en el legado |
|---|---|---|---|
| **Flujo** | Plantilla configurable por tenant y tipo de vínculo: reglas de exigencia, etapas y áreas, SLAs, vigencias, verificaciones y plantilla de certificado. | "Habilitación de proveedores" | `FlujosAprobacion` + `DocumentosRequeridos` + `SectoresVencimiento` |
| **FlujoVersion** | Versión publicada e inmutable de un flujo. Un expediente siempre referencia una versión concreta. | Habilitación de proveedores v3 | No existía (las reglas cambiaban "en caliente") |
| **Tipo de documento canónico** | Entrada del catálogo de plataforma que identifica un documento por su naturaleza, con `Codigo` estable, ámbito, vigencia intrínseca, si permite ratificación, si pertenece a una persona relacionada, campos clave y críticos. Es lo que permite que el "RUT" de Acme y el "RUT" de Beta sean el mismo documento compartible. **La lista cerrada es el Anexo A de 03.** | `RUT`, `CAMARA_COMERCIO`, `CEDULA`, `ESTADOS_FINANCIEROS`, `CERTIFICACION_BANCARIA`, `CUENTA_BANCARIA`, `DATOS_MAESTROS`, `POLIZA`, `AUTORIZACION_TRATAMIENTO_DATOS` | `TipoAdjuntoTercero` con ids mágicos (12, 18, 1011…) |
| **Tipo de documento de tenant / de empresa** | Cómo se exige un tipo: nombre propio, mapeo al canónico (o ninguno), regla de frescura, días de aviso y gracia, nivel de revisión, plazo y política de revalidación, aprobadores, muestreo, ayuda y plantilla. El **tenant** define los defaults del grupo; cada **empresa** puede sobreescribirlos; la **regla de exigencia** del flujo sobreescribe ambos (jerarquía tenant → empresa → regla, B-4). | "Cámara de comercio (máx. 30 días)" en Acme Construcciones y "máx. 90 días" en Acme Inmobiliaria, mismo tenant | `DocumentosRequeridos` + `DocumentosObservacionConstructora` + `DocumentosVencimientoConstructora` |
| **Configuración efectiva** | Parámetros de un requisito ya resueltos por la jerarquía y congelados en el expediente al abrirlo o recalcularlo. | — | No existía |
| **Clase de tipo** | `Archivo` (PDF/imagen) o `Formulario` (datos estructurados con **esquema JSON base canónico** versionado más **campos privados** del tenant; misma mecánica de versiones). Toda edición crea versión; solo un cambio en campos críticos dispara revalidación, el resto es "cambio menor". | Información bancaria es `Formulario` | Sectores del Portal (bancaria, tributaria, financiera, legal, SISO, general, T&C) |
| **Cuenta bancaria / Cuenta designada** | El tercero registra N cuentas globales (formulario `CUENTA_BANCARIA` con su certificación); **el tercero elige** cuál designa para cada vínculo; el cambio lo aprueba esa empresa con cuatro ojos y viaja a su ERP. | El Tornillo cobra a Acme en Bancolombia y a Beta en Davivienda | `TerCuentaBancaria` + `BancosConstructora` |
| **Tipos aceptados** | Lista de tipos canónicos cualquiera de los cuales satisface un requisito. | Cédula o pasaporte del representante | No existía |
| **Aprobadores requeridos (cuatro ojos)** | Número de revisores (1 o 2) que debe aprobar un requisito o cambio; 2 por defecto en certificación bancaria y cuenta designada. | — | No existía |
| **Ámbito** | `Global` (vive en la bóveda del tercero y se comparte con todas las empresas que lo exigen) o `PorVinculo` (privado de una relación). | RUT es Global; "Formato de vinculación de Acme" es PorVinculo | Tipos "por constructora" 1013/1015/1019/1183/1185 |
| **Regla de exigencia** | Condición (Y/O/No sobre atributos del tercero y del vínculo) que decide si un tipo de documento se exige en un flujo, con obligatoriedad, criticidad, áreas que revisan y vigencia. | "CAMARA_COMERCIO si tercero.tipoPersona = Jurídica" | `TipoDocumentoRequerido` Todos/Grupos/Categorias/Especialidad/TipoPersona/Etiquetas |
| **Área de revisión** | Grupo revisor configurable. | Legal, Financiera, Técnica, Cumplimiento, Oficial SAGRILAFT | `PerfilAprobacion` (perfil/rol) |
| **Etapa** | Conjunto de áreas que revisan en paralelo; las etapas van en secuencia. | Etapa 1: Legal + Financiera; Etapa 2: Cumplimiento | `OrdenApr` |
| **Motivo** | Causa tipificada de una observación o rechazo, por tenant, con categoría Documento/Formulario/Contacto/Otro. | "Documento ilegible", "Titular no coincide" | `LogAprobacionesRechazo.IdMotivo` + `EstadoTercero/rechazo/motivos` |
| **Verificación externa** | Comprobación enchufable del **expediente**, humana o automática, distinta a la revisión documental y a los hechos de la bóveda, que puede ser bloqueante del cierre. Una coincidencia confirmada en listas deja la verificación `Fallida` y exige decisión humana. | Consulta a listas restrictivas; estudio de cumplimiento; consulta ERP | `AdjuntosSarlaft`, `EstudioSeguridad`, flags `Sarlaft`/`SRM_ValidaOficial` |
| **Nivel de revisión** | Servicio contratable por empresa y tipo: `Manual` (el sistema solo muestra los hechos, sin sugerir), `Asistido` (la IA propone y el humano confirma en un clic), `Automatico` (la IA aprueba; el humano ve excepciones y una muestra). Sin IA disponible, todos se comportan como `Manual`. | Acme: RUT en Automático, EEFF en Asistido | No existía |
| **Fail-closed** | Un solo sentido en todo el proyecto: **el sistema nunca aprueba ni promueve por sí solo ante duda o ausencia de dato** (IA caída, confianza baja, antivirus ausente en producción). La carga del tercero, en cambio, nunca se bloquea por eso. | Sin IA, la versión llega a `Actual` pero nadie la aprueba automáticamente | No existía |
| **Campos clave** | Datos del documento que la IA extrae y compara entre versiones. | NIT, razón social, dirección, fecha de expedición | No existía |
| **Campos críticos** | Subconjunto de campos clave cuyo cambio exige revisión humana aunque el nivel sea Automático. | Representante legal, razón social, número de cuenta | No existía |
| **Porcentaje de muestreo** | Fracción de decisiones automáticas que se envía igualmente a revisión humana para auditoría. | 5 % | No existía |

## 3. Documentos y bóveda

| Término | Definición | Ejemplo | Equivalente en el legado |
|---|---|---|---|
| **Bóveda del tercero** | Repositorio único de los documentos globales de un tercero: un documento por tipo canónico, con versiones inmutables. Es la **fuente de verdad del archivo**. | La bóveda de El Tornillo: RUT (v3), Cámara (v5), EEFF 2025 (v1) | Disco `/Archivos/{tercero}` + `Adjuntos`/`AdjuntoTercero` (Portal) |
| **Documento** | Contenedor de versiones de un tipo canónico para un dueño (tercero si Global; vínculo si PorVinculo). | El RUT de El Tornillo | `AdjuntoTercero` agrupado por `AjdTerTipo` |
| **Versión de documento** | Binario inmutable (hash SHA-256, clave S3, mime, tamaño) con metadatos declarados y extraídos. Nunca se sobreescribe. | RUT v3, cargado el 28/10/2026 | Cada fila de `Adjuntos` |
| **Versión actual** | La única versión vigente de un documento global para el tercero. Las empresas no eligen versión: validan la actual. | RUT v3 | "Último adjunto" del Portal |
| **Hecho compartido** | Resultado objetivo sobre una versión, calculado **una vez** y visible para todas las empresas con compartición. **Sin IA** siempre existen: hash, tipo MIME real, antivirus, páginas, preview, comparación por hash. **Con IA** se añaden: tipo detectado, legibilidad, extracción de campos y fechas, comparación por campos (`CamposCambiados`, `CamposCriticosCambiados`), coherencia formulario-soporte, señales de manipulación. **La lectura por IA es coste de plataforma** (B-2): no depende del presupuesto de ningún tenant; solo falta si está deshabilitada a nivel plataforma o el tercero no consintió. La bóveda emite hechos; nunca decide revalidaciones (T-7). | "v3 es legible, expedida el 20/10/2026, SinCambios respecto a v2" | No existía (cada constructora revisaba a ciegas) |
| **Comparación con versión anterior** | Hecho compartido que clasifica una versión nueva frente a la actual: `SinAnterior` (primera versión), `Identica` (mismo hash), `Distinta` (solo por hash, sin IA), `SinCambios` (campos clave iguales), `CambiosDetectados{campos}`. Enum `Comparacion` en 03. | "Cambió la dirección" | No existía |
| **Devuelta** | Estado de una versión que no pasó los hechos compartidos (tipo incorrecto, ilegible, virus, identidad no coincide): vuelve al tercero sin alertar a ninguna empresa. | "Esto no parece un RUT" | No existía |
| **Cambio menor** | Versión nueva de un formulario en la que solo cambiaron campos no críticos: pasa a `Actual` y se notifica, pero no desactualiza aceptaciones ni abre revalidación. | Cambió el teléfono de contacto | No existía |
| **Compartición** | Registro de qué vínculo (empresa) puede ver una versión. Para tipos globales es **automática** con todas las empresas cuya invitación el tercero **ya aceptó** y que exigen ese tipo (B-1: nunca antes de aceptar; A-8: el tercero no puede excluir a ninguna). Se **congela** hacia adelante por desvinculación o bloqueo (`ComparticionCongelada`: no llegan versiones nuevas; lo ya compartido sigue legible por retención) y se reanuda al desbloquear. | RUT v3 compartido con Acme, Beta y Gama | Implícito: el Portal mostraba todo a todas |
| **Decisión privada** | Veredicto de una empresa sobre una versión. Nunca visible para otras empresas, **ni siquiera para otras empresas del mismo tenant**. | "Acme Construcciones observó v3: página 2 cortada" (Acme Inmobiliaria no lo ve) | `LogAprobaciones` + `AdjuntosConstructora` |
| **Versión aceptada** | Puntero por vínculo y tipo a la versión que esa empresa aprobó, con `vigente_desde`, `vigente_hasta` y `motivo_vigencia`. Reemplaza la copia espejo. | Acme aceptó RUT v3 el 29/10 | `AdjuntosConstructora.ACActivo=1` |
| **Desactualizada** | Marca de una versión aceptada cuando la versión actual del tercero ya es otra. La empresa debe revalidar; la aceptación anterior queda en el histórico. | Acme tenía v2 aceptada; llegó v3 → v2 Desactualizada | No existía |
| **Revalidación** | **Un solo sentido:** revisión que hace una empresa de la **versión actual** de un documento porque su aceptación quedó `Desactualizada` al cambiar esa versión. Se tramita como expediente de `Actualizacion` (abierto por la saga `Revalidacion`). Lo que ocurre por **vencimiento** no es revalidación sino **renovación**. | Acme revalida RUT v3 | "Actualización" por constructora |
| **Renovación** | **Un solo sentido:** expediente de tipo `Renovacion` que reabre los requisitos vencidos o por vencer de un vínculo; lo abre la saga al primer vencimiento de un requisito crítico, o al reintegrar/desbloquear con vencidos. Cuando el tercero sube una versión nueva, para la empresa donde el documento había vencido esa versión entra en su `Renovacion`; para las demás es una revalidación. | Renovación anual de Cámara y pólizas | Vuelta a `DocumentacionPendiente` + `VencimientosProveedor` Abierto |
| **Ratificación** | Declaración del tercero de que la versión actual sigue válida; renueva la vigencia en la empresa que la pidió **reiniciando la frescura** desde la fecha de ratificación, sin versión nueva. Solo si el tipo canónico y la configuración efectiva lo permiten y el documento no tiene vencimiento propio pasado. **Nunca pasa por un área ni la emite el sistema** (A-7): si algo cambió, es versión nueva. | "Mi RUT no ha cambiado" | `OrigenActualizacion = Ratificacion` en `VencimientosProveedor` |
| **ConPendientes (informativo / bloqueante)** | Estado del certificado cuando hay una aceptación `Desactualizada`, un documento no crítico vencido o una verificación expirada. Informativo durante `plazoRevalidacion` (15 días por defecto; 90 en aceptaciones importadas); después, si `politicaDesactualizada = BloquearTrasGracia` (por defecto), **bloqueante**: la consulta de certificación responde `bloqueado: true, motivo: RevalidacionVencida` hasta revalidar (A-2). El vínculo no cambia de estado por ello. | "Certificado vigente con pendientes: RUT por revalidar, 9 días" | No existía |
| **Bloqueado / bloqueante** | Tres cosas distintas: **`Bloqueado`** es un estado del vínculo decidido por una empresa (fraude, sanción) que **suspende** su certificado y congela la compartición (B-5); **bloqueante** es la marca `ConPendientes` tras vencer el plazo de revalidación; y `bloqueado: true` en la consulta de certificación cubre ambos casos y el vínculo `Vencido`, con `motivo` (`Bloqueo`, `RevalidacionVencida`, `Vencido`, `Suspendido`) para que Marco/SRM distingan fraude de desidia. | — | Lista negra informal |
| **Retiro de versión** | El tercero deja de ofrecer una versión para nuevas revisiones. No la borra si alguna versión aceptada o certificado la referencia (retención legal). Si retira la **actual** sin referencias, la anterior vuelve a ser actual y las revalidaciones abiertas se cancelan. | Retira un archivo subido por error | No existía |
| **Vigencia intrínseca** | Fecha de vencimiento propia del documento, extraída del archivo o declarada. Es un hecho compartido. | Póliza vigente hasta 31/12/2026 | `FechaVencimiento` solo en formularios |
| **Regla de frescura** | Exigencia de cada empresa sobre la antigüedad del documento: `MaxDiasDesdeExpedicion(n)`, `MaxDiasDesdeAprobacion(n)`, `PeriodoMinimo(regla)` o `Ninguna`. | Acme: Cámara ≤ 30 días; Beta: ≤ 90 | `DocumentosRequeridos.DiasVencimiento` (aplicado desde la fecha de carga) |
| **Vigencia (de la versión aceptada)** | `vigente_hasta = mín(vigencia intrínseca, fecha de expedición o de aprobación + regla de frescura efectiva, fin del periodo permitido)`. Es por empresa, sobre el mismo hecho. | La misma Cámara vence el 15/11 para Acme y el 14/01 para Beta | Tres mecanismos distintos (ver 01 §5.4) |
| **Vencimiento** | Momento en que `vigente_hasta` pasa. Afecta al requisito y al vínculo de esa empresa, no a las demás. Un crítico vencido abre `Renovacion`; el vínculo pasa a `Vencido` tras `diasGracia` (15; 0 si es crítico de cumplimiento). | Requisito `Vencido` en Acme | `Vencido=-6`, `EstadoVencimiento=Vencido` |
| **Frescura consolidada** | Vista por documento global con la vigencia por cada empresa; alimenta **una sola** secuencia de avisos al tercero con la fecha más temprana. | "Tu Cámara vence para Acme el 15/11 y para Beta el 14/01" | Un correo por constructora |

## 4. Validación

| Término | Definición | Ejemplo | Equivalente en el legado |
|---|---|---|---|
| **Invitación** | Acto de la empresa de crear un vínculo y pedir documentos al tercero; individual o por lote. | "Solicitar documentos" a El Tornillo | `Account/invitarproveedor` + `Solicitudes` |
| **Lote** | Conjunto de invitaciones procesadas en segundo plano con estado, pausa y reintentos. | 1 000 proveedores desde Excel | `InvitacionLote` |
| **Enlace de acceso (magic link)** | Token opaco de un solo uso y caducidad corta con el que el tercero entra sin contraseña; opcionalmente con OTP por correo. | `https://…/e/eyJ…` | Usuario + contraseña enviada por correo |
| **Expediente** | Instancia de un flujo para un vínculo, de vida corta. Tipos: `Inicial`, `Renovacion`, `Actualizacion`. Event-sourced. Folio propio `{PrefijoEmpresa}-E-{Año}-{Secuencia}`. | Expediente ACME1-E-2026-000418 | `Solicitudes` + `ActualizacionProveedor` |
| **Requisito** | Línea del expediente: "este expediente exige el tipo X", con snapshot de la regla que lo generó y el área que lo revisa. | RUT, obligatorio, revisa Legal | `DocumentacionPendiente` + tipos "requeridos" |
| **Revisión** | Trabajo de un área sobre un expediente. | Revisión de Legal | Turno del perfil (`QuienAprueba`) |
| **Veredicto** | Decisión sobre la versión de un requisito: solo `Aprobado` u `Observado` (B-7). Referencia la versión sobre la que se emitió y su origen (`Humano`, `Sistema`, `Administrador`). | Legal aprobó RUT v3 | `LogAprobaciones.LogIsApr` |
| **Veredicto automático** | Veredicto emitido por el actor `Sistema (IA)` según el nivel de revisión y las reglas de la empresa, con evidencia y reversible por un humano. Nunca ratifica. | "Aprobado automáticamente: SinCambios, frescura cumplida" | No existía |
| **Observación** | Veredicto que devuelve el requisito al tercero con motivo tipificado y texto. Recuperable: el tercero corrige y reenvía. Puede marcar **`noAdmitirEstaVersion`**: esa versión no volverá a aceptarse en ese vínculo (versión no admitida). | "Página 2 cortada, vuelve a subirlo" | Rechazo con `logAprResuelto=0` |
| **Rechazo** | **Solo del expediente**: cierre terminal decidido por un área con `PuedeRechazarExpediente`. No existe "rechazar una versión". | "Tercero en lista restrictiva" | `Rechazado=-1` |
| **Override de administrador** | Veredicto o anulación emitidos por un administrador en nombre de un área, siempre con justificación y rastro. | Admin aprueba Financiera por ausencia del revisor | `EsPerfilAdministrador`, `DesaprobarTercero` |
| **Cierre** | Evaluación automática: todas las etapas aprobadas y todas las verificaciones bloqueantes satisfechas y vigentes ⇒ `ExpedienteAprobado`. | — | `ValidarAprobacionCompleta` |
| **Caducidad** | Cierre del expediente por vencer el plazo de carga sin completar (30 días por defecto, recordatorios 15/7/1); se reabre en un clic. | 30 días sin documentos | `Vencido=-6` por plazo (180 días) |
| **Certificado** | Evidencia verificable del resultado de un expediente aprobado, **uno por vínculo** con historial de emisiones (folio + sufijo): folio, QR, huella, vigencia, historial de etapas. Estados `Vigente, ConPendientes, Suspendido, Vencido, Revocado, Reemplazado`. `Suspendido` = bloqueo del vínculo (reversible); `Revocado` = decisión humana irreversible. | Certificado de habilitación de proveedor | No existía ("Completada" + hoja de vida PDF) |
| **Bloqueo (del vínculo)** | Estado `Bloqueado` decidido por una empresa (fraude, sanción) con motivo, vigencia opcional y quién lo levanta; privado de esa empresa. **Suspende** el certificado y congela la compartición; al desbloquear se restituye lo que siga vigente y se abre `Renovacion` si algo venció (B-5). | Acme bloquea a un proveedor sancionado | Lista negra informal |
| **Folio** | Identificador único y legible: expediente `{PrefijoEmpresa}-E-{Año}-{Secuencia}`; certificado `{PrefijoEmpresa}-C-{Año}-{Secuencia}-{emisión}` (T-9). | `ACME1-C-2026-000418-1` | — |
| **Huella** | Hash SHA-256 del contenido del certificado, mostrado abreviado y verificable en `GET /public/v1/verificar/{folio}` (UI `/verificar/:folio`). | `9f3a·e71b·c21e` | — |
| **Novedad** | Aviso en la campana del tercero o del revisor con el siguiente paso. | "Acme observó tu RUT" | `Novedades*` (Portal) |
| **Webhook** | Notificación HTTP firmada (HMAC) que Validasoft envía a un sistema integrador cuando ocurre un evento público, con reintentos y log de entregas; el receptor deduplica por `Validasoft-Event-Id` (T-6). | `certificado.emitido` | Callbacks sin firma (`TercerosERP/estado`, `ActualizacionesERP/estado`) |
| **API key** | Credencial de un sistema integrador por tenant, con scopes y rotación; nunca en el navegador. | `vsk_live_acme_…` | `Token_Zuppla`, `ConsApiKey` |
| **Idempotency-Key** | Cabecera obligatoria en toda escritura de integración: repetir la misma clave con el mismo cuerpo devuelve la misma respuesta. | `srm-adp-00123-inv-7` | No existía (reintentos duplicaban) |

## 5. Estados

### 5.1 Enums del dominio (textuales, uno por agregado)

| Agregado | Enum | Valores |
|---|---|---|
| Versión de documento | `EstadoVersion` | `Recibida, EnAnalisis, Actual, Superada, Devuelta, Retirada` |
| Vínculo | `EstadoVinculo` | `Invitado, EnValidacion, Activo, PorVencer, Vencido, Bloqueado, Desvinculado, Anulado` (+ bandera derivada `RevalidacionPendiente` cuando hay aceptaciones desactualizadas) |
| Expediente | `EstadoExpediente` | `Abierto, EnRevision, Observado, Aprobado, Rechazado, Anulado, Caducado` |
| Expediente | `TipoExpediente` | `Inicial, Renovacion, Actualizacion` |
| Requisito | `EstadoRequisito` | `Pendiente, Cargado, EnRevision, Observado, Aprobado, Eximido, NoAplica, Vencido, Desactualizado` (sin `Rechazado`, B-7) |
| Veredicto | `Veredicto` | `Aprobado, Observado` (+ bandera `noAdmitirEstaVersion`; `Rechazado` solo del expediente) |
| Veredicto (origen) | `OrigenVeredicto` | `Humano, Sistema, Administrador` |
| Etapa | `EstadoEtapa` | `Esperando, Activa, Aprobada` |
| Verificación | `EstadoVerificacion` | `Pendiente, CoincidenciaPorAnalizar, Satisfecha, Fallida, Expirada` |
| Versión (hechos) | `Antivirus` · `Legibilidad` · bandera `PendienteRevisionHumana` · `HechosRegistrados.ConIa` | `Limpio, Infectado, Cuarentena, NoDisponible` · `Legible, Parcial, NoLegible` |
| Versión (comparación) | `Comparacion` | `SinAnterior, Identica, Distinta` (solo por hash, sin IA), `SinCambios, CambiosDetectados` (por campos, con IA) |
| Documento | `ClaseDocumento` · `AmbitoDocumento` | `Archivo, Formulario` · `Global, PorVinculo` |
| Relación entre terceros | `TipoRelacionTerceros` | `RepresentanteLegal, Suplente, Socio, BeneficiarioFinal, MiembroDe` |
| Aceptación (versión aceptada) | `EstadoAceptacion` | `AlDia, Desactualizada, PorVencer, Vencida` |
| Tipo de documento (tenant / empresa / regla) | `NivelRevision` · `PoliticaDesactualizada` | `Manual, Asistido, Automatico` · `BloquearTrasGracia, SoloAviso` |
| Certificado | `EstadoCertificado` | `Vigente, ConPendientes, Suspendido, Vencido, Revocado, Reemplazado` |
| Cuenta designada | `EstadoCuentaDesignada` | `Propuesta, Aprobada, Rechazada, Reemplazada` |
| Actor de un evento | `ActorTipo` | `Usuario, Tercero, Integracion, Sistema, Plataforma, Migracion` |
| UI | *(proyección, no enum de dominio)* | En curso, Aprobado, Observado, Rechazado, Por vencer, Vencido, Revalidación pendiente |

Evento que cruza de la bóveda a los vínculos: `VersionActualCambiada(CamposCambiados, CamposCriticosCambiados)` (sin bandera de revalidación: decide cada empresa). Congelamiento de compartición: `ComparticionCongelada` / `ComparticionReanudada`. Proyecciones nombradas igual en todos los documentos: `BovedaTercero`, `BandejaRevision`, `BandejaRevalidaciones`, `FrescuraDocumentoTercero`, `EstadoCertificacionTercero`, `ColaDeExcepciones`, `VencimientoProgramado`, `Tercero360`; `MisPendientes` es una consulta compuesta. Esquemas: `plataforma`, `boveda`, `validacion`, `identidad` ([03 §2.2](03-dominio-y-eventos.md)).

### 5.2 Mapeo del legado → nuevo

| Legado (Portal `EstadoTercero` / SRM `EstadoTerceros`) | `EstadoVinculo` | `EstadoExpediente` |
|---|---|---|
| `SinEstado (0)` | Invitado | — |
| `Anulado (-2)` | Anulado | Anulado |
| `DocumentacionPendiente (-3)` | EnValidacion | Abierto |
| `DocumentacionCompleta (-4)` | EnValidacion | EnRevision (etapa 1 activa) |
| `RequiereAprobacion (-5)`, `AprobacionInicial (-7)` | EnValidacion | EnRevision (veredictos reconstruidos desde `LogAprobaciones`) |
| `PendienteOficialCumplimiento (-9)` | EnValidacion | EnRevision + verificación `EstudioDeCumplimiento` Pendiente |
| `Rechazado (-1)` | Invitado | Rechazado (motivos con `logAprResuelto=0` → Observado; resto → Rechazado) |
| `RechazadoOficialCumplimiento (-11)` | Invitado | Rechazado |
| `Aprobado (1)`, `Validado (2)`, `Completada (3)` | Activo | Aprobado (+ `CertificadoEmitido` retroactivo con folio de migración) |
| `Actualizacion (4)` | Activo | Expediente `Actualizacion` abierto |
| `Vencido (-6)` por plazo de documentación | Invitado | Caducado |
| `Vencido (-6)` por vigencia / `EstadoVencimiento = Vencido` | Vencido | — |
| `EstadoVencimiento = PorVencer` | PorVencer | — |
| `Desvinculado (-8)` | Desvinculado | — |
| Lista negra informal / rechazo "definitivo" de una constructora | Bloqueado (privado de esa empresa) | — |
| `NovedadCompletada (-10)`, `-97, -98, -100, -200` | *(no son estados)* | Se convierten en eventos de la línea de tiempo |

Mapeo de requisitos y certificados:

| Legado | Nuevo |
|---|---|
| `VencimientosProveedor` Abierto por documento | Requisito `Vencido` (en esa empresa) |
| `AdjuntosConstructora` activo distinto del último adjunto del tercero | Versión aceptada `Desactualizada` ⇒ requisito `Desactualizado` + revalidación pendiente |
| Proveedor `Completada` con sector vencido no crítico | Certificado `ConPendientes` |

### 5.3 Otras equivalencias

| Legado | Nuevo |
|---|---|
| `TipoAdjuntoTercero` 12 / 18 | `CAMARA_COMERCIO` con condición `tercero.tipoPersona` |
| `TipoAdjuntoTercero` 1011 | `AUTORIZACION_TRATAMIENTO_DATOS`, ámbito `PorVinculo` |
| `BancosConstructora` (cuenta por constructora) | Cuenta global del tercero + `CuentaDesignada` por vínculo |
| `TerInfoTributaria`, `TerInfoFinanciera`, `TerCamaraComercio`, `TerSISO`, `TerInformacionGeneral` | Formularios canónicos `INFO_TRIBUTARIA`, `ESTADOS_FINANCIEROS`, `INFO_LEGAL` (+ relaciones), `SG_SST`, `DATOS_MAESTROS` (códigos del Anexo A de 03) |
| Representante legal / socios en `TerCamaraComercio` y contactos | `Tercero` persona natural + `RelacionEntreTerceros`; su `CEDULA` en su propia bóveda |
| Rechazo de un documento por una constructora | Observación con `noAdmitirEstaVersion` (la versión no se admite en ese vínculo) |
| `Terceros.TerTipoDocumento` texto libre | `catalogo_tipo_identificacion(pais, codigo)` |
| Cinco constructoras de un mismo grupo en el Portal | Un tenant con cinco empresas internas |
| 1013 / 1015 / 1019 / 1183 / 1185 | Tipos de tenant con ámbito `PorVinculo` |
| `TipoDocumentoRequerido.Todos` | Regla sin condición |
| `Grupos / Categorias / Especialidad / Etiquetas` | `vinculo.{categorias|especialidades|etiquetas} ContieneAlguno [...]` |
| `TipoPersona 'n'/'j'` | `tercero.tipoPersona En [Natural|Juridica]` |
| Sectores `Bancaria, Tributaria, Financiera, Legal, SISO, General, TerminosCondiciones, TratamientoDatos, Especialidades` | Tipos de documento clase `Formulario` agrupados por `Seccion` |
| `AdjuntosConstructora.ACActivo = 1` | `VersionAceptada` |
| Última versión en `AdjuntoTercero` | Versión actual del documento |
| `VencimientosProveedor` Abierto | Requisito `Vencido` + expediente `Renovacion` |
| `PerfilAprobacion` con `OrdenApr` | Áreas en etapas secuenciales |
| Flag `Sarlaft` del perfil | Verificación `ListasRestrictivas` bloqueante |
| `SRM_ValidaOficial = 2` | Verificación `EstudioDeCumplimiento` bloqueante |
| `LogAprobacionesRechazo.LogTipoMotivo` | Categoría del motivo (Documento/Formulario/Contacto) |
| `SolicitudesAuditoriaEstado/Detalle` | Línea de tiempo (proyección del event store) |
| `Token_Zuppla` / `ConsApiKey` | API key por tenant con scopes |
| `provportal` + 4 llamadas al ERP | Token exchange `urn:validasoft:erp-token` |

## Decisiones abiertas

1. **Nombre público de "Tercero".** En la UI del propio tercero, ¿se le llama "tu empresa", "tu perfil" o "tercero"? Propuesta: "Mi perfil" en la UI pública; "Tercero" en API y documentos. Quién decide: Producto/UX.
2. ~~"Observado" vs "Devuelto"~~ — **cerrada** por B-7: "Observado" sobre versiones (con `noAdmitirEstaVersion` opcional) y "Rechazado" solo para el cierre terminal del expediente; "Devuelta" se reserva para la versión que no pasó los hechos de la bóveda.
3. ~~Indicador de revalidación en el vínculo~~ — **cerrada**: [03](03-dominio-y-eventos.md) §5 lo define como bandera derivada (`RevalidacionPendiente = RevalidacionesPendientes.Count > 0`), no como estado; el vínculo sigue `Activo` siempre; lo que bloquea tras 15 días es el certificado (`ConPendientes` bloqueante, A-2).

## Fuentes verificadas

Ver [01 Análisis del legado](01-analisis-legado-srm-portal.md) §4 (modelo de datos y enums con sus rutas) y [03 Dominio y eventos](03-dominio-y-eventos.md) (definiciones de agregados y enums).
