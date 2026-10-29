# Cuestionario de decisiones pendientes

| Estado | Fecha | Responsable |
|---|---|---|
| Borrador | 2026-10-28 | Arquitectura |

## Propósito

Reunir, en un solo lugar, todas las decisiones que **todavía no están tomadas** y que condicionan el alcance, el modelo o la construcción de Validasoft. Cada pregunta es cerrada, trae opciones con su consecuencia y una recomendación justificada desde la práctica de validación documental, cumplimiento (SAGRILAFT, debida diligencia, Ley 1581 de 2012) y producto SaaS B2B. Nadie decide aquí: decide Producto (o quien se indique), y la respuesta se registra.

Lo que ya está decidido no se vuelve a preguntar: stack (.NET 10, PostgreSQL, Marten, Wolverine, MinIO, Docker), dos apps React + MUI, OpenIddict propio, multi-tenant SaaS, Validasoft dueña de la UI del tercero con magic link, compartición automática de documentos globales, una sola versión vigente por tercero y tipo con revalidación obligatoria, decisiones privadas por empresa, hechos compartidos calculados una vez, vigencia en dos capas, revisión automática por niveles Manual/Asistido/Automático, IA Claude desde el MVP fail-closed, sin gRPC en el MVP, repo docs-first, hitos H0–H11.

## Cómo se usa

1. Producto responde cada pregunta (puede aceptar la recomendación con un "OK").
2. La respuesta se anota en `docs/decisiones/registro.md` con fecha, quién decidió y la opción elegida.
3. Arquitectura propaga la decisión a los documentos `00`–`10` y, si es estructural, a un ADR nuevo.
4. Una pregunta cerrada no se reabre sin ADR que lo justifique.

Prioridades:
- **B** — Bloqueante para H1–H2 (fundación y bóveda): sin respuesta no se puede modelar la base.
- **N** — Necesaria antes de H4–H5 (expediente, veredictos, certificado, MVP).
- **E** — Puede esperar a H6+ (vigencias, cumplimiento, integración, operación).

Relacionados: `../00-vision-y-alcance.md`, `../02-modelo-documental-compartido.md`, `../03-dominio-y-eventos.md`, `../07-seguridad-identidad-y-tenancy.md`, `../09-plan-de-hitos-y-responsabilidades.md`.

---

## Bloque 1 — Identidad del tercero y datos maestros

### P-1-1 ¿Cuál es la clave única del tercero?
- **Por qué importa**: define la unicidad en `boveda.tercero`, el dedupe de la migración y cómo se reconoce al mismo proveedor desde varias empresas. Afecta 02, 03, 08. H2.
- **Opciones**: (a) `(país, tipo de identificación, número)` sin dígito de verificación; (b) igual que (a) pero incluyendo el DV del NIT; (c) número normalizado (solo dígitos) + país, ignorando el tipo.
- **Recomendación**: (a), normalizando el número (sin puntos, guiones ni DV) y guardando el DV como atributo validado. El DV es derivable y en el legado viene inconsistente (`900123456-7`, `9001234567`); incluirlo en la clave crea duplicados.
- **Prioridad**: B

### P-1-2 ¿Qué pasa cuando cambia el número de identificación o la razón social (cambio de NIT por transformación societaria, fusión, escisión)?
- **Por qué importa**: en Colombia una SAS que se transforma conserva NIT, pero una fusión por absorción extingue uno. Afecta la continuidad de vínculos, certificados y documentos. Afecta 02, 03. H2.
- **Opciones**: (a) se crea un tercero nuevo y se enlaza como `SucesorDe` con traslado opcional de documentos vigentes; (b) se renombra el mismo tercero y se guarda el histórico de identificaciones; (c) no se soporta: el tercero nuevo empieza de cero.
- **Recomendación**: (a). Mantiene la trazabilidad legal (el certificado emitido a la empresa absorbida sigue siendo histórico de esa entidad) y permite a cada empresa decidir si reconoce al sucesor. Cambio de razón social sin cambio de NIT es (b).
- **Prioridad**: N

### P-1-3 ¿Las sucursales o establecimientos son terceros distintos o atributos del tercero?
- **Por qué importa**: un proveedor con sede en Bogotá y Medellín puede exigir documentos distintos (certificado de Cámara por jurisdicción). Afecta reglas de exigencia y el modelo de vínculo. 02, 03. H3.
- **Opciones**: (a) atributo `sucursales[]` del tercero y el vínculo elige una; (b) tercero hijo con `ParteDe`; (c) no se modela en el MVP.
- **Recomendación**: (a). Las sucursales no tienen personería jurídica propia; la mayoría de documentos (RUT, EEFF) son de la matriz. El vínculo apunta a la sucursal para dirección y contacto.
- **Prioridad**: E

### P-1-4 ¿Cómo se identifican terceros extranjeros sin NIT ni cédula?
- **Por qué importa**: proveedores del exterior (pasaporte, Tax ID, NIT de otro país) existen en licitaciones. Afecta la clave única y el catálogo de tipos de identificación por país. 02, 03. H2.
- **Opciones**: (a) catálogo de tipos de identificación por país (pasaporte, Tax ID, CUIT, RFC…) con validador por tipo; (b) un tipo genérico "Identificación extranjera" + país; (c) fuera de alcance del MVP.
- **Recomendación**: (b) en el MVP, con el catálogo (a) preparado como datos para no cambiar el modelo. El volumen inicial es pequeño y el validador por país es trabajo sin retorno inmediato.
- **Prioridad**: N

### P-1-5 ¿Los consorcios y uniones temporales son terceros?
- **Por qué importa**: en obras civiles son habituales; tienen NIT propio pero no personería; sus miembros deben acreditar documentos. Afecta reglas (exigir documentos de cada consorciado). 02, 03. H3.
- **Opciones**: (a) tercero con `TipoPersona = Consorcio` y relación `Miembros[]` hacia terceros existentes, con reglas que pueden exigir documentos de los miembros; (b) tercero normal, los documentos de los miembros se suben como adjuntos del consorcio; (c) fuera del MVP.
- **Recomendación**: (a), porque la compartición entonces es correcta: la Cámara de comercio del miembro X se valida una vez y sirve al consorcio y a los vínculos directos de X.
- **Prioridad**: E

### P-1-6 ¿Cuántos representantes legales se modelan y cuál viaja al ERP?
- **Por qué importa**: el legado solo acepta uno en AyF y eso generó reglas raras ("el Principal de menor id"). Afecta formulario legal, verificación de identidad y aplicación al ERP. 03, 08. H4.
- **Opciones**: (a) N representantes con uno marcado `Principal`, y el ERP recibe el principal; (b) solo uno; (c) N sin principal y la integración elige.
- **Recomendación**: (a). Las sociedades tienen suplentes y la consulta de listas restrictivas debe cubrirlos a todos; el ERP recibe uno sin ambigüedad.
- **Prioridad**: N

### P-1-7 ¿Quién puede editar los datos maestros del tercero (razón social, dirección, contactos)?
- **Por qué importa**: hoy los edita el proveedor en el Portal y la empresa aprueba por sector. Con muchas empresas, una edición afecta a todas. Afecta 02 (hechos vs decisiones), 03, 06. H2.
- **Opciones**: (a) solo el tercero, y el cambio se trata como una versión nueva de un formulario global que todas revalidan; (b) el tercero y cualquier empresa, con conflicto resuelto por "última escritura"; (c) el tercero propone, la plataforma valida contra el documento (RUT) con IA y solo entonces se publica.
- **Recomendación**: (c). Es coherente con la regla de versión única y evita que una empresa "corrija" datos que otra ya validó. La empresa solo puede anotar datos privados (código interno, categoría comercial).
- **Prioridad**: B

### P-1-8 ¿Qué datos del tercero son globales y cuáles privados de cada empresa?
- **Por qué importa**: la categoría de compras de A no es la de B; la dirección sí es una. Define el esquema de `tercero` y los atributos del vínculo. 02, 03. H2.
- **Opciones**: (a) globales: identificación, nombre, tipo de persona, país, dirección principal, contactos del tercero, representantes; privados por vínculo: categorías, etiquetas, especialidades, código ERP, contactos asignados por la empresa, atributos personalizados; (b) todo global; (c) todo privado salvo la identificación.
- **Recomendación**: (a). Es exactamente lo que ya hace el motor de reglas (`tercero.*` vs `vinculo.*`).
- **Prioridad**: B

### P-1-9 ¿Se permite el autorregistro de un tercero sin que ninguna empresa lo haya invitado?
- **Por qué importa**: el Portal lo permite con validación IA de RUT/cédula; genera "proveedores sin validar" en el directorio. Afecta identidad, consentimiento y el flujo de vínculo. 00, 03, 07. H4.
- **Opciones**: (a) sí, con verificación de identidad por IA y correo, y queda en un directorio público opt-in; (b) solo por invitación de una empresa; (c) autorregistro pero invisible hasta que una empresa lo vincule.
- **Recomendación**: (c) en el MVP. Evita el directorio público (riesgo de datos personales) y conserva la bóveda lista cuando llegue la primera invitación.
- **Prioridad**: N

---

## Bloque 2 — Catálogo canónico de documentos

### P-2-1 ¿Quién administra el catálogo canónico?
- **Por qué importa**: la compartición entre empresas depende de que "RUT" sea el mismo tipo en todas. Si cada tenant crea tipos libres, nada se comparte. 02, 03. H2.
- **Opciones**: (a) la plataforma (super-administrador) lo administra; los tenants mapean sus tipos a un canónico o crean tipos privados `PorVinculo`; (b) cualquier tenant puede crear canónicos; (c) catálogo fijo en código.
- **Recomendación**: (a), con un flujo de "proponer tipo canónico" desde el tenant que la plataforma aprueba. Es la única forma de mantener la semántica.
- **Prioridad**: B

### P-2-2 ¿El catálogo canónico se versiona?
- **Por qué importa**: cambiar `CamposClave` o `ReglaFrescura` por defecto altera cómo se comparan versiones ya cargadas. 02, 03. H2.
- **Opciones**: (a) sí: cada tipo canónico tiene versión y las versiones de documento guardan con cuál se extrajeron; (b) no, cambios en caliente.
- **Recomendación**: (a). Los hechos extraídos deben poder recalcularse (Batches API) cuando cambia el esquema del tipo, y el histórico debe saber con qué reglas se leyó.
- **Prioridad**: N

### P-2-3 ¿Cuál es la lista inicial de tipos canónicos y sus valores por defecto?
- **Por qué importa**: es la semilla del producto y de la migración. 02, 08. H2.
- **Opciones**: (a) lista base colombiana con defaults (tabla abajo); (b) solo los del legado (`TipoAdjuntoTercero`); (c) vacío, cada tenant define.
- **Recomendación**: (a). Propuesta de semilla (ajustable por tenant, nunca más laxo en `Ambito` ni en `PermiteRatificacion`):

| Código | Ámbito | Vencimiento intrínseco | Frescura por defecto | Ratificable | Campos clave | Campos críticos |
|---|---|---|---|---|---|---|
| `RUT` | Global | No | 365 días desde fecha de generación del PDF | Sí | NIT, razón social, actividad CIIU principal, responsabilidades, dirección | NIT, razón social, representante legal |
| `CAMARA_COMERCIO` | Global | No | 30 días desde expedición | No | NIT, razón social, matrícula, representante legal, objeto, capital, fecha expedición | Representante legal, razón social, estado de la matrícula |
| `CEDULA_REPRESENTANTE` | Global | No | Ninguna | Sí | Número, nombres, fecha de expedición | Número, nombres |
| `ESTADOS_FINANCIEROS` | Global | No | Periodo ≥ último año fiscal cerrado | No | Periodo, activos, pasivos, patrimonio, ingresos, firma contador/revisor | Periodo |
| `DECLARACION_RENTA` | Global | No | Periodo ≥ último año gravable | No | Año gravable, NIT, formulario | Año gravable |
| `CERTIFICACION_BANCARIA` | Global (ver P-3-3) | No | 90 días desde expedición | No | Banco, tipo, número, titular, NIT titular | Número, titular |
| `POLIZA` | PorVinculo | Sí (fecha fin) | — | No | Aseguradora, número, tomador, asegurado/beneficiario, amparos, vigencia desde/hasta, valor | Beneficiario, vigencia, amparos |
| `CERTIFICADO_ANTECEDENTES` (Procuraduría, Contraloría, Policía) | Global | No | 30 días | No | Número, nombre, resultado, fecha | Resultado |
| `SG_SST` (certificación o autoevaluación) | Global | Sí (vigencia anual) | — | No | Empresa, puntaje, fecha | Puntaje |
| `AUTORIZACION_TRATAMIENTO_DATOS` | PorVinculo | No | Ninguna | Sí | Firmante, fecha, empresa destinataria | Firmante |
| `TERMINOS_Y_CONDICIONES` | PorVinculo | No | Ninguna (cambia cuando la empresa publica T&C nuevos) | Sí | Versión de T&C aceptada | Versión |
| `FORMULARIO_SARLAFT` | PorVinculo | No | 365 días | No | PEP, origen de fondos, beneficiario final | PEP, beneficiario final |
| `HOJA_DE_VIDA` (empleado) | Global | No | Ninguna | Sí | Nombre, identificación | — |
| `TITULO_ACADEMICO` | Global | No | Ninguna | Sí | Institución, título, fecha | Título |
| `AFILIACION_SEGURIDAD_SOCIAL` | PorVinculo | Sí (mes) | Mes en curso | No | Periodo, EPS/AFP/ARL, estado | Estado |
| `PAZ_Y_SALVO` | PorVinculo | No | 30 días | No | Emisor, fecha, concepto | — |

- **Prioridad**: B

### P-2-4 ¿Un tenant puede cambiar el ámbito de un tipo canónico (por ejemplo pedir la Certificación bancaria `PorVinculo`)?
- **Por qué importa**: si A exige una cuenta distinta a la que el tercero usa con B, el documento deja de ser global. 02. H2.
- **Opciones**: (a) el tenant puede restringir Global → `PorVinculo` (más privado), nunca al revés; (b) ámbito fijo por canónico; (c) libre.
- **Recomendación**: (a). Restringir nunca rompe la compartición de los demás; ampliar sí.
- **Prioridad**: B

### P-2-5 ¿Un documento puede satisfacer dos tipos (una Cámara de comercio que también acredita representante legal)?
- **Por qué importa**: evita pedir dos veces lo mismo; complica el modelo de requisito. 03. H3.
- **Opciones**: (a) un requisito puede declarar `TiposAceptados[]` (cualquiera de ellos lo satisface); (b) un tipo por requisito; (c) relación "deriva de" entre tipos.
- **Recomendación**: (a). Simple y cubre el caso real (cédula del representante vs pasaporte).
- **Prioridad**: N

### P-2-6 ¿Se exige plantilla oficial descargable para documentos propios de la empresa (formatos de vinculación)?
- **Por qué importa**: hoy el SRM tiene `DocPlantilla` por regla. 03, 06. H3.
- **Opciones**: (a) plantilla por tipo de tenant, versionada, y la versión cargada guarda con qué plantilla se generó; (b) plantilla como simple adjunto de ayuda; (c) sin plantillas.
- **Recomendación**: (a). Permite que la IA compare contra la plantilla vigente y detecte formatos viejos.
- **Prioridad**: N

---

## Bloque 3 — Formularios (datos estructurados)

### P-3-1 ¿Los formularios (bancaria, tributaria, financiera, legal, SISO, contactos) se modelan como documentos de clase `Formulario` con esquema JSON versionado?
- **Por qué importa**: unifica versiones, compartición, vigencia, veredictos y revalidación con los archivos. Es la decisión estructural más grande después de la versión única. 02, 03, 05, 06. H2/H3.
- **Opciones**: (a) sí, clase `Formulario` con `EsquemaJson` versionado por tipo canónico y renderizado dinámico; (b) entidades CRUD propias por sector como en el Portal; (c) híbrido: formularios globales (tributaria, legal) como `Formulario`, bancaria como entidad.
- **Recomendación**: (a). Un solo motor evita repetir el "soporte por sector + tabla de actualización por constructora" del legado. El coste es un renderizador de esquemas JSON en el frontend (react-jsonschema-form o propio).
- **Prioridad**: B

### P-3-2 ¿Quién define el esquema de cada formulario?
- **Por qué importa**: si cada tenant define campos distintos de "información tributaria", deja de ser compartible. 02, 03. H3.
- **Opciones**: (a) esquema base canónico de plataforma + campos adicionales privados por tenant (`PorVinculo`); (b) esquema libre por tenant; (c) esquema fijo.
- **Recomendación**: (a). Igual que el catálogo de documentos: el núcleo se comparte, lo específico es privado.
- **Prioridad**: B

### P-3-3 ¿La cuenta bancaria es global del tercero o por empresa?
- **Por qué importa**: en el legado es por constructora (`BancosConstructora`) porque el proveedor cobra a cada una en una cuenta distinta; la certificación bancaria se asocia a esa cuenta. 02, 03, 08. H2.
- **Opciones**: (a) el tercero registra N cuentas globales (cada una con su certificación como documento global vinculado a la cuenta) y cada vínculo **elige** una (`CuentaDesignada`, dato privado del vínculo); (b) cuenta por vínculo, sin compartir; (c) una sola cuenta global.
- **Recomendación**: (a). La certificación se valida una vez; la elección es por empresa y viaja al ERP de esa empresa. Cambiar la cuenta designada es un cambio privado del vínculo que sí requiere aprobación de esa empresa (riesgo de fraude de cuenta).
- **Prioridad**: B

### P-3-4 ¿Un formulario con soporte exige que el soporte sea coherente con los datos (IA)?
- **Por qué importa**: hoy el proveedor digita la cuenta y adjunta la certificación; nadie cruza automáticamente. 04. H2.
- **Opciones**: (a) sí: los campos clave del formulario se contrastan con los extraídos del soporte y la discrepancia es un hecho compartido; (b) solo humano.
- **Recomendación**: (a). Es el mayor ahorro de revisión y el mayor control antifraude (titular de la cuenta ≠ tercero).
- **Prioridad**: N

### P-3-5 ¿La aceptación de T&C y la autorización de tratamiento de datos son por empresa?
- **Por qué importa**: legalmente la autorización de datos la otorga el titular a un responsable concreto (cada empresa) y la plataforma es encargada. 07. H4.
- **Opciones**: (a) por empresa (`PorVinculo`), con versión del texto aceptado, fecha, IP y evidencia; más una aceptación de la plataforma al crear la bóveda; (b) una sola aceptación a la plataforma que cubre a todas; (c) solo por empresa.
- **Recomendación**: (a). La Ley 1581 exige autorización previa, expresa e informada por responsable; la plataforma además necesita la suya como encargado.
- **Prioridad**: N

### P-3-6 ¿Cómo se versiona un formulario cuando cambia un solo campo?
- **Por qué importa**: cambiar el teléfono no debería disparar revalidación en 20 empresas; cambiar el representante legal sí. 02, 03. H3.
- **Opciones**: (a) toda edición crea versión nueva, pero solo los cambios en `CamposCriticos` disparan revalidación; el resto se notifica como "cambio menor"; (b) toda edición revalida; (c) campos no críticos editables sin versión.
- **Recomendación**: (a). Mantiene la trazabilidad y hace el flujo orgánico.
- **Prioridad**: B

---

## Bloque 4 — Expediente y flujo

### P-4-1 ¿Quién puede abrir un expediente?
- **Por qué importa**: define permisos y el origen del vínculo. 03, 05. H4.
- **Opciones**: (a) la empresa (usuario o API), el sistema (renovación/actualización automática) y el tercero solo para autorregistro; (b) solo la empresa; (c) también el tercero puede solicitar vincularse a una empresa.
- **Recomendación**: (a). El caso "el tercero pide vincularse" se cubre con autorregistro invisible (P-1-9) y un enlace de la empresa.
- **Prioridad**: N

### P-4-2 ¿Plazo por defecto para que el tercero complete un expediente y qué pasa al vencer?
- **Por qué importa**: el legado usa 180 días y pasa a "Vencido" sin más. 03. H4.
- **Opciones**: (a) 30 días configurables por flujo, con recordatorios a 15/7/1 días, y al vencer `Caducado` con reapertura en un clic por la empresa; (b) 180 días como el legado; (c) sin plazo.
- **Recomendación**: (a). 180 días esconde proveedores abandonados; la reapertura barata evita la fricción.
- **Prioridad**: N

### P-4-3 ¿Hay límite de reenvíos tras observación?
- **Por qué importa**: evita bucles infinitos y detecta terceros que no logran cumplir. 03. H4.
- **Opciones**: (a) sin límite, pero con alerta al administrador al tercer reenvío del mismo requisito; (b) límite duro de 3 y luego rechazo; (c) sin límite ni alerta.
- **Recomendación**: (a). El rechazo automático es injusto si la observación fue ambigua.
- **Prioridad**: E

### P-4-4 ¿Etapas en paralelo o en secuencia por defecto?
- **Por qué importa**: el legado es secuencial por `OrdenApr`, lo que alarga el ciclo. 03. H4.
- **Opciones**: (a) por defecto una sola etapa con todas las áreas en paralelo; la secuencia se configura cuando una área depende de otra (cumplimiento después de legal); (b) secuencial por defecto; (c) sin etapas, solo áreas.
- **Recomendación**: (a). Reduce el tiempo a certificado; la secuencia queda disponible.
- **Prioridad**: N

### P-4-5 ¿Qué pasa cuando una área incumple su SLA?
- **Por qué importa**: hoy nada. 03, 10 (alertas). H4.
- **Opciones**: (a) escalamiento: alerta al responsable del área, luego al administrador; nunca auto-aprobar; (b) auto-aprobación al vencer el SLA; (c) solo métrica.
- **Recomendación**: (a). Auto-aprobar por silencio es inaceptable en cumplimiento.
- **Prioridad**: N

### P-4-6 ¿Delegación de revisores (vacaciones, ausencias)?
- **Por qué importa**: sin delegación los expedientes se estancan. 03, 06. H8.
- **Opciones**: (a) delegación temporal por usuario con fechas y alcance (área), auditada; (b) reasignación manual por el administrador; (c) ambas.
- **Recomendación**: (c). La reasignación ya existe en el legado; la delegación evita depender del administrador.
- **Prioridad**: E

### P-4-7 ¿Se exige "cuatro ojos" (dos revisores) en algún caso?
- **Por qué importa**: cambios de cuenta bancaria y aprobaciones de alto monto suelen exigir doble aprobación. 03. H4.
- **Opciones**: (a) configurable por área y por tipo de requisito (`AprobadoresRequeridos = 1|2`); (b) nunca; (c) siempre para formularios bancarios.
- **Recomendación**: (a), con (c) como valor por defecto de la semilla para `CERTIFICACION_BANCARIA` y la cuenta designada.
- **Prioridad**: N

### P-4-8 ¿Separación de funciones: quien invita no aprueba?
- **Por qué importa**: control antifraude básico en compras. 07. H4.
- **Opciones**: (a) configurable por tenant, apagado por defecto; (b) siempre; (c) nunca.
- **Recomendación**: (a). Empresas pequeñas tienen una sola persona.
- **Prioridad**: E

### P-4-9 ¿Qué áreas vienen de semilla en el flujo "Habilitación de proveedores"?
- **Por qué importa**: es la demo del MVP y la referencia para migrar perfiles del SRM. 03, 09. H3.
- **Opciones**: (a) Compras (completitud), Legal, Financiera, Cumplimiento (SAGRILAFT), con SST opcional; (b) una sola área "Revisión"; (c) copiar los perfiles de cada constructora en la migración.
- **Recomendación**: (a) para la semilla, y (c) para la migración (los perfiles del SRM se convierten en áreas del tenant).
- **Prioridad**: N

---

## Bloque 5 — Veredictos y motivos

### P-5-1 ¿El catálogo de motivos de observación/rechazo es de plataforma o de tenant?
- **Por qué importa**: motivos estandarizados permiten métricas cruzadas y mensajes claros al tercero. 03, 06. H4.
- **Opciones**: (a) base de plataforma (ilegible, incompleto, vencido, no corresponde al tercero, datos no coinciden, formato no vigente, firma faltante) + motivos propios del tenant; (b) solo tenant; (c) solo plataforma.
- **Recomendación**: (a). Los motivos base alimentan la IA y los mensajes al tercero; los propios cubren particularidades.
- **Prioridad**: N

### P-5-2 ¿La observación es por documento o puede ser por campo?
- **Por qué importa**: en formularios, decir "el campo dirección no coincide" es más útil que "formulario observado". 03, 06. H4.
- **Opciones**: (a) por requisito con lista opcional de campos afectados; (b) solo por requisito; (c) por campo con veredicto independiente.
- **Recomendación**: (a). Mantiene un solo veredicto por requisito y da precisión al tercero.
- **Prioridad**: N

### P-5-3 ¿Existe el rechazo definitivo del tercero (lista negra por tenant)?
- **Por qué importa**: una empresa puede decidir no volver a trabajar con un tercero (fraude, sanción). Afecta vínculo, invitaciones y SAGRILAFT. 03, 07. H4.
- **Opciones**: (a) estado `Bloqueado` del vínculo por tenant, con motivo, vigencia opcional y quién lo levanta; privado de ese tenant; (b) no existe; (c) lista negra compartida entre tenants.
- **Recomendación**: (a). Una lista compartida tiene implicaciones legales (habeas data, derecho a la defensa) que exceden el producto.
- **Prioridad**: N

### P-5-4 ¿Quién puede eximir un requisito y con qué control?
- **Por qué importa**: eximir es la puerta trasera de cualquier sistema de cumplimiento. 03, 07. H4.
- **Opciones**: (a) solo roles con permiso `requisitos:eximir`, justificación obligatoria, visible en el certificado y reportable; (b) cualquier revisor del área; (c) no se puede eximir.
- **Recomendación**: (a). El certificado debe decir "requisito eximido por X con justificación".
- **Prioridad**: N

### P-5-5 ¿El override del administrador puede revertir un rechazo de expediente?
- **Por qué importa**: define si `Rechazado` es realmente terminal. 03. H4.
- **Opciones**: (a) no: el rechazo es terminal; el administrador puede abrir un expediente nuevo; (b) sí, con justificación.
- **Recomendación**: (a). Mantiene la máquina de estados limpia y el historial honesto.
- **Prioridad**: N

---

## Bloque 6 — Revisión automática e IA

### P-6-1 ¿Umbrales de confianza por defecto para decidir automáticamente?
- **Por qué importa**: define la calidad percibida del nivel Automático. 04. H7.
- **Opciones**: (a) ≥ 0,95 por campo clave para aprobar; 0,80–0,95 → Asistido; < 0,80 → humano; (b) un umbral global 0,90; (c) configurable sin defaults.
- **Recomendación**: (a), con defaults por tipo canónico y ajuste por tenant.
- **Prioridad**: E

### P-6-2 ¿Qué campos son críticos por defecto?
- **Por qué importa**: un cambio en campo crítico siempre va a humano. 02, 04. H2.
- **Opciones**: (a) la columna "campos críticos" de P-2-3; (b) todos los campos clave; (c) ninguno.
- **Recomendación**: (a).
- **Prioridad**: B

### P-6-3 ¿Porcentaje de muestreo humano en nivel Automático?
- **Por qué importa**: control de calidad y evidencia ante auditoría. 04. H7.
- **Opciones**: (a) 5 % por defecto, mínimo 1 por semana por tipo, configurable; (b) 0 %; (c) 10 %.
- **Recomendación**: (a).
- **Prioridad**: E

### P-6-4 ¿Qué pasa cuando el presupuesto de IA del tenant se agota?
- **Por qué importa**: fail-closed puede detener la operación del tenant. 04. H7.
- **Opciones**: (a) degradar a Manual con alerta al administrador y al comercial, nunca bloquear la carga del tercero; (b) bloquear; (c) continuar y facturar excedente.
- **Recomendación**: (a) por defecto, con (c) como opción contractual.
- **Prioridad**: E

### P-6-5 ¿Se requiere consentimiento explícito del tercero para procesar sus documentos con un proveedor de IA externo?
- **Por qué importa**: transferencia internacional de datos personales (Ley 1581, Decreto 1377): Anthropic procesa fuera de Colombia. 07. H2.
- **Opciones**: (a) sí: la autorización de tratamiento a la plataforma lo incluye expresamente (finalidad, encargado, transferencia internacional) y es condición para usar la bóveda; (b) no, se considera encargo técnico; (c) opt-out por tercero (sin IA → todo manual).
- **Recomendación**: (a), redactado por el área legal, con la opción de que el tenant active "zero data retention" del proveedor donde esté disponible.
- **Prioridad**: B

### P-6-6 ¿Cuánto tiempo se conserva el prompt y la respuesta de la IA?
- **Por qué importa**: evidencia de la decisión vs minimización de datos. 04, 07. H2.
- **Opciones**: (a) lo mismo que la versión del documento (vinculado a su retención); (b) 1 año; (c) solo el resultado estructurado, no el prompt.
- **Recomendación**: (a). Una decisión automática sin evidencia no sirve en una auditoría.
- **Prioridad**: N

### P-6-7 ¿Modelo de precios del servicio de IA?
- **Por qué importa**: define medición de uso y métricas. 04, 11. H7.
- **Opciones**: (a) por **decisión** automática/asistida por empresa (la lectura del documento es costo de plataforma, compartida); (b) por documento leído; (c) por vínculo activo con IA incluida; (d) incluido en el plan.
- **Recomendación**: (a) + un cargo por vínculo activo en el plan. Es coherente con "se lee una vez, cada empresa paga por decidir".
- **Prioridad**: E

### P-6-8 ¿El tercero tiene derecho a revisión humana de una decisión automática?
- **Por qué importa**: buena práctica y, en Europa, obligación; en Colombia, principio de debido proceso contractual. 06, 07. H7.
- **Opciones**: (a) sí: botón "Pedir revisión humana" en toda observación automática; la empresa debe responder; (b) no.
- **Recomendación**: (a).
- **Prioridad**: E

### P-6-9 ¿Qué latencia se promete para el resultado de la IA al tercero?
- **Por qué importa**: la UI del tercero espera "en segundos". 04, 06. H2.
- **Opciones**: (a) resultado en < 60 s para el 95 % y mensaje "te avisamos por correo" si tarda más; (b) síncrono con timeout 120 s; (c) siempre asíncrono.
- **Recomendación**: (a).
- **Prioridad**: N

---

## Bloque 7 — Vigencias y renovación

### P-7-1 ¿Días de aviso por defecto?
- **Por qué importa**: alimenta `DiasAviso[]` de la semilla. 03. H6.
- **Opciones**: (a) 30/15/5 días; (b) 60/30/7; (c) solo 15.
- **Recomendación**: (a), y 60/30/7 para documentos con trámite largo (pólizas, EEFF).
- **Prioridad**: E

### P-7-2 ¿Periodo de gracia tras el vencimiento antes de que el vínculo pase a `Vencido`?
- **Por qué importa**: evita cortar operación por un día de retraso. 03. H6.
- **Opciones**: (a) 15 días por defecto, configurable, 0 para críticos de cumplimiento; (b) sin gracia; (c) 30 días.
- **Recomendación**: (a).
- **Prioridad**: E

### P-7-3 ¿Qué pasa con el certificado cuando vence un documento no crítico?
- **Por qué importa**: define el significado del certificado. 03. H6.
- **Opciones**: (a) el certificado sigue vigente con anotación "con pendientes" visible en la verificación; vence solo por documentos críticos o por `DiasFijosDesdeAprobacion`; (b) cualquier vencimiento lo revoca; (c) el certificado nunca vence.
- **Recomendación**: (a).
- **Prioridad**: E

### P-7-4 ¿Renovación masiva anual (EEFF, declaración de renta)?
- **Por qué importa**: cada año, en el mismo mes, miles de terceros deben subir EEFF; el sistema debe soportarlo como campaña. 03, 08. H6.
- **Opciones**: (a) "campañas de renovación" por tenant y tipo con ventana, recordatorios y seguimiento; (b) vencimiento individual sin campaña.
- **Recomendación**: (a) en H8.
- **Prioridad**: E

### P-7-5 ¿La revalidación por versión nueva reinicia la vigencia de la empresa?
- **Por qué importa**: si A exige Cámara < 30 días y el tercero sube una nueva, la vigencia de A se recalcula desde la nueva expedición. 02, 03. H6.
- **Opciones**: (a) sí, siempre desde los hechos de la versión aceptada; (b) solo si la empresa lo marca.
- **Recomendación**: (a). Es consecuencia directa de las dos capas.
- **Prioridad**: N

---

## Bloque 8 — Certificado

### P-8-1 ¿Qué datos del tercero expone la verificación pública?
- **Por qué importa**: la URL del QR la ve cualquiera. 07. H5.
- **Opciones**: (a) razón social, NIT enmascarado (`900.***.456`), empresa emisora, flujo, estado, fecha de emisión y vigencia, huella; sin documentos ni datos de contacto; (b) todo lo del certificado; (c) solo estado y huella.
- **Recomendación**: (a).
- **Prioridad**: N

### P-8-2 ¿Un certificado por vínculo o por expediente?
- **Por qué importa**: define reemisión y folio. 03. H5.
- **Opciones**: (a) por vínculo, con historial de emisiones (cada revalidación o renovación emite una nueva versión del certificado que reemplaza la anterior y conserva el folio raíz + sufijo); (b) por expediente, folios independientes; (c) uno solo inmutable.
- **Recomendación**: (a). El consumidor (Marco) pregunta por el vínculo, no por el expediente.
- **Prioridad**: N

### P-8-3 ¿Firma electrónica del certificado?
- **Por qué importa**: valor probatorio ante terceros. 04, 07. H5.
- **Opciones**: (a) sello electrónico de la plataforma (certificado X.509 propio) + hash publicado + sello de tiempo RFC 3161 opcional en H11; (b) firma electrónica certificada por entidad acreditada (ONAC) desde el MVP; (c) solo hash y QR.
- **Recomendación**: (a). La verificación en línea es la prueba principal; la firma acreditada encarece y puede añadirse.
- **Prioridad**: E

### P-8-4 ¿Certificado parcial (algunas áreas aprobaron)?
- **Por qué importa**: algunas empresas quieren "habilitado comercialmente" antes de "habilitado en cumplimiento". 03. H5.
- **Opciones**: (a) no: el certificado solo existe al cerrar el expediente; los estados intermedios se consultan por API; (b) certificados por área.
- **Recomendación**: (a). Evita que un certificado parcial circule como completo.
- **Prioridad**: N

### P-8-5 ¿Formato e idioma del PDF?
- **Por qué importa**: archivo y accesibilidad. 04. H5.
- **Opciones**: (a) PDF/A-2b, español, con bloque bilingüe opcional; (b) PDF simple.
- **Recomendación**: (a).
- **Prioridad**: E

---

## Bloque 9 — Verificaciones externas

### P-9-1 ¿Qué proveedores de listas restrictivas se integran primero?
- **Por qué importa**: cada adaptador es trabajo; los contratos los tiene cada constructora. 04, 08. H7.
- **Opciones**: (a) los que hoy tengan contrato vigente las primeras constructoras a migrar (consultar: Compliance, Stradata, TusDatos son los más usados en el legado); (b) todos los siete del legado; (c) uno genérico por CSV manual.
- **Recomendación**: (a) + evidencia manual (subir PDF de una consulta hecha fuera) como fallback universal.
- **Prioridad**: E

### P-9-2 ¿Frecuencia de reconsulta de listas?
- **Por qué importa**: SAGRILAFT exige monitoreo periódico de contrapartes. 03. H7.
- **Opciones**: (a) al vincular, en cada renovación y cada 12 meses (configurable), más reconsulta masiva mensual opcional; (b) solo al vincular; (c) diaria por lote.
- **Recomendación**: (a).
- **Prioridad**: E

### P-9-3 ¿Quién paga la consulta cuando varias empresas comparten el tercero?
- **Por qué importa**: la consulta es un hecho compartible (misma persona, misma fecha) pero el contrato es de cada empresa. 02, 04. H7.
- **Opciones**: (a) cada empresa consulta con su contrato; el resultado no se comparte (datos del proveedor del servicio); (b) la plataforma consulta una vez y comparte; (c) se comparte solo si la otra empresa tiene el mismo proveedor.
- **Recomendación**: (a) en el MVP; (b) requiere negociar licencia de plataforma.
- **Prioridad**: E

### P-9-4 ¿Gestión de coincidencias (falsos positivos) en listas?
- **Por qué importa**: homónimos son frecuentes; el analista debe documentar el descarte. 03, 06. H7.
- **Opciones**: (a) estado `CoincidenciaPorAnalizar` con decisión `Descartada|Confirmada` justificada y vigente por 12 meses; (b) resultado binario.
- **Recomendación**: (a).
- **Prioridad**: E

### P-9-5 ¿Consultas a fuentes oficiales (RUES, DIAN, Registraduría) en el MVP?
- **Por qué importa**: validaría Cámara y RUT sin IA; pero RUES tiene API comercial y DIAN no tiene API pública. 04. H7+.
- **Opciones**: (a) fase posterior, como `Verificador` enchufable; (b) MVP con RUES.
- **Recomendación**: (a).
- **Prioridad**: E

### P-9-6 ¿Campos y niveles de riesgo del estudio de cumplimiento?
- **Por qué importa**: debe replicar como mínimo el legado (verificado en listas, conflicto de interés, nivel de riesgo 1–3, debida diligencia sin novedad/descartada/monitoreo, evidencias, vigencia 30 días). 03. H7.
- **Opciones**: (a) replicar el legado + matriz de riesgo configurable (país, actividad, PEP, monto) con puntaje; (b) solo legado; (c) solo matriz.
- **Recomendación**: (a), matriz en H8.
- **Prioridad**: E

---

## Bloque 10 — Notificaciones y alertas

### P-10-1 ¿Catálogo de eventos que generan alerta y a quién?
- **Por qué importa**: es la base del motor de notificaciones. 03, 04, 11. H4.
- **Opciones**: (a) catálogo propuesto (tabla); (b) definir por tenant desde cero.
- **Recomendación**: (a):

| Evento | Tercero | Revisor del área | Administrador | Integración (webhook) |
|---|---|---|---|---|
| Invitación / enlace | correo | — | — | sí |
| Documento cargado | campana | campana | — | sí |
| Resultado IA (observado automático) | correo + campana | — | — | sí |
| Requisito observado por humano | correo + campana | — | — | sí |
| Expediente aprobado / certificado | correo + campana | campana | — | sí |
| Expediente rechazado | correo | — | campana | sí |
| Versión nueva de documento global | — | campana (revalidar) | digest | sí |
| Por vencer (30/15/5) | correo consolidado | — | digest | sí |
| Vencido | correo | campana | digest | sí |
| SLA de área incumplido | — | correo | correo | sí |
| Webhook fallido definitivamente | — | — | correo | — |
| Presupuesto IA al 80 % / agotado | — | — | correo | — |

- **Prioridad**: N

### P-10-2 ¿Canales en el MVP?
- **Por qué importa**: WhatsApp/SMS tienen coste y proveedor. 04. H5.
- **Opciones**: (a) correo + campana + webhook; WhatsApp/SMS en H8+ como canal enchufable; (b) incluir SMS en el MVP.
- **Recomendación**: (a).
- **Prioridad**: N

### P-10-3 ¿Consolidación (digest) y límites anti-spam al tercero?
- **Por qué importa**: un tercero con 10 empresas puede recibir 10 correos por el mismo hecho. 04. H6.
- **Opciones**: (a) máximo 1 correo por tercero y hecho por día, consolidado por empresas; recordatorios de vencimiento una sola secuencia; digest diario opcional para revisores; (b) un correo por evento.
- **Recomendación**: (a).
- **Prioridad**: E

### P-10-4 ¿Remitente de los correos: dominio de la plataforma o de cada empresa?
- **Por qué importa**: entregabilidad (SPF/DKIM) y confianza del tercero. 04, 07. H5.
- **Opciones**: (a) dominio de la plataforma con nombre de la empresa en el remitente y `Reply-To` de la empresa; (b) dominio de cada empresa con verificación DKIM; (c) ambos, por tenant.
- **Recomendación**: (a) en el MVP, (c) en H8.
- **Prioridad**: N

### P-10-5 ¿Plantillas editables por el tenant?
- **Por qué importa**: marca de la empresa vs riesgo de romper variables. 04. H8.
- **Opciones**: (a) editable con editor Liquid restringido, vista previa y variables validadas; (b) solo texto de cabecera/pie; (c) no editables.
- **Recomendación**: (b) en el MVP, (a) en H8.
- **Prioridad**: E

---

## Bloque 11 — Multi-tenant y comercial

### P-11-1 ¿Planes y límites?
- **Por qué importa**: define medición de uso desde H1 (no se puede añadir después sin dolor). 04, 07. H1.
- **Opciones**: (a) planes Básico/Profesional/Empresarial con límites por usuarios, vínculos activos, GB, decisiones IA/mes, webhooks; medición desde H1; (b) sin planes al inicio; (c) solo medición, sin límites.
- **Recomendación**: (c) en H1 (medir todo), límites y planes en H8.
- **Prioridad**: B

### P-11-2 ¿Grupos empresariales (varias razones sociales que comparten proveedores)?
- **Por qué importa**: una constructora grande tiene 5 NIT; hoy son 5 constructoras en el Portal. 02, 03, 07. H2.
- **Opciones**: (a) `GrupoEmpresarial` que agrupa tenants y permite compartir catálogo, usuarios y ver vínculos del grupo; cada tenant sigue decidiendo por separado; (b) un tenant por grupo con "empresas" internas; (c) sin soporte.
- **Recomendación**: (a). Mantiene el aislamiento legal por razón social y da la vista consolidada.
- **Prioridad**: B

### P-11-3 ¿Marca blanca?
- **Por qué importa**: dominio y logo propios en la UI del tercero y en correos. 04, 06. H8.
- **Opciones**: (a) logo y colores por tenant en el MVP; dominio personalizado en H8; (b) todo en el MVP; (c) nunca.
- **Recomendación**: (a).
- **Prioridad**: E

### P-11-4 ¿Tenant de pruebas (sandbox) por cliente?
- **Por qué importa**: integradores necesitan probar sin afectar producción. 08. H9.
- **Opciones**: (a) entorno `sandbox` compartido con tenants de prueba y datos sintéticos; (b) modo sandbox dentro del mismo tenant (`X-Api-Key` de prueba); (c) ninguno.
- **Recomendación**: (b) (como Stripe) desde H9.
- **Prioridad**: E

---

## Bloque 12 — Integración

### P-12-1 ¿Qué sistema crea el tercero primero: el ERP o Validasoft?
- **Por qué importa**: define el dueño de la identidad y los conflictos. 08. H9.
- **Opciones**: (a) Validasoft es el maestro de identidad del tercero para la vinculación; el ERP recibe el tercero certificado por webhook y guarda `validasoftTerceroId`; (b) el ERP crea y Validasoft recibe; (c) cualquiera, con reconciliación por clave única.
- **Recomendación**: (c) con regla: la identidad se reconcilia por clave única (P-1-1); los datos maestros validados por documento prevalecen; cada sistema guarda el id externo del otro.
- **Prioridad**: N

### P-12-2 ¿Pull además de webhooks?
- **Por qué importa**: SRM on-premise tras firewall puede no recibir webhooks. 05. H5.
- **Opciones**: (a) ambos desde el MVP (`GET /eventos?desde=cursor`); (b) solo webhooks.
- **Recomendación**: (a).
- **Prioridad**: N

### P-12-3 ¿SDK oficial para qué lenguajes?
- **Por qué importa**: trabajo de mantenimiento. 08. H9.
- **Opciones**: (a) .NET y TypeScript generados desde OpenAPI; (b) también Java/Python; (c) solo OpenAPI.
- **Recomendación**: (a).
- **Prioridad**: E

### P-12-4 ¿Durante la coexistencia, el SRM sigue siendo la UI del revisor o se usa la de Validasoft?
- **Por qué importa**: define cuánto del SRM se toca en H9. 08. H9.
- **Opciones**: (a) la revisión se hace en Validasoft; el SRM solo recibe estados y aplica en AyF; (b) el SRM consume la API y mantiene su UI; (c) ambas.
- **Recomendación**: (a). Mantener dos UIs de revisión duplica el trabajo y perpetúa el legado.
- **Prioridad**: N

---

## Bloque 13 — Migración

### P-13-1 ¿Qué histórico se migra?
- **Por qué importa**: volumen de binarios y tiempo. 08. H10.
- **Opciones**: (a) terceros y vínculos activos con su última versión y la aceptada por cada constructora + 3 años de historial de decisiones; (b) todo; (c) solo vigentes.
- **Recomendación**: (a). El resto queda consultable en el legado en solo lectura.
- **Prioridad**: E

### P-13-2 ¿Documentos del Portal que ninguna constructora aprobó?
- **Por qué importa**: existen cargas huérfanas. 08. H10.
- **Opciones**: (a) se importan a la bóveda como versión actual sin `version_aceptada` y pasan por revalidación si alguna empresa los exige; (b) se descartan.
- **Recomendación**: (a).
- **Prioridad**: E

### P-13-3 ¿Usuarios del Portal: se migran o se reinvitan?
- **Por qué importa**: contraseñas en claro en el legado; Validasoft no usa contraseña para el tercero. 07, 08. H10.
- **Opciones**: (a) se migran correo y nombre; el acceso es por magic link (no hay contraseña que migrar); (b) se reinvita a todos.
- **Recomendación**: (a).
- **Prioridad**: E

### P-13-4 ¿Periodo de doble operación por tenant?
- **Por qué importa**: ventana de rollback. 08. H10.
- **Opciones**: (a) 90 días de sincronización inversa; (b) 30; (c) sin doble operación.
- **Recomendación**: (a).
- **Prioridad**: E

---

## Bloque 14 — Seguridad y cumplimiento

### P-14-1 ¿Retención por tipo de documento?
- **Por qué importa**: ley comercial colombiana: 10 años para documentos de comercio; datos personales solo mientras exista finalidad. 07. H2.
- **Opciones**: (a) por tipo canónico: 10 años para documentos de la relación comercial y de cumplimiento desde el fin del vínculo; documentos de identidad del tercero mientras exista vínculo + 10 años; evidencias SAGRILAFT 10 años; configurable por tenant hacia arriba; (b) indefinida; (c) 5 años.
- **Recomendación**: (a). Debe validarlo el área legal.
- **Prioridad**: B

### P-14-2 ¿Residencia de datos?
- **Por qué importa**: clientes del Estado o financieros pueden exigir datos en Colombia. 04, 07. H1.
- **Opciones**: (a) despliegue por región con la región como atributo del tenant; MVP en una región (Azure South Central US o AWS sa-east-1) + on-premise; (b) sin restricción.
- **Recomendación**: (a). La IA externa se declara en la autorización (P-6-5).
- **Prioridad**: B

### P-14-3 ¿Cifrado con claves por tenant?
- **Por qué importa**: aislamiento fuerte y "borrado criptográfico" al cerrar un tenant. 07. H1.
- **Opciones**: (a) SSE-KMS con clave por tenant en MinIO y Data Protection por tenant; (b) una clave de plataforma.
- **Recomendación**: (a).
- **Prioridad**: B

### P-14-4 ¿2FA obligatorio para quién?
- **Por qué importa**: equilibrio seguridad/fricción. 07. H1.
- **Opciones**: (a) obligatorio para Administrador, Auditor y revisores de Cumplimiento; opcional y recomendado para el resto; política por tenant puede exigirlo a todos; (b) a todos; (c) opcional.
- **Recomendación**: (a).
- **Prioridad**: N

### P-14-5 ¿Habeas data: cómo se atiende una solicitud de supresión del tercero?
- **Por qué importa**: obligación legal con plazos (15 días hábiles). 07. H2.
- **Opciones**: (a) flujo formal: solicitud registrada, evaluación automática de retención legal por documento, supresión de lo no retenido, anonimización del resto, respuesta al titular con constancia; (b) manual por soporte.
- **Recomendación**: (a) en H8, con el modelo preparado en H2 (`retirado`, retención).
- **Prioridad**: N

### P-14-6 ¿Acuerdo de tratamiento de datos entre tenant (responsable) y plataforma (encargado)?
- **Por qué importa**: exigido por la Ley 1581; define quién responde ante la SIC. 07. H1.
- **Opciones**: (a) contrato marco + anexo técnico generado por la plataforma y aceptado al crear el tenant; (b) solo contrato comercial.
- **Recomendación**: (a). Lo redacta legal.
- **Prioridad**: N

### P-14-7 ¿Retención de logs de acceso a documentos?
- **Por qué importa**: evidencia de quién vio qué. 07. H2.
- **Opciones**: (a) igual a la retención del documento; (b) 2 años; (c) 1 año.
- **Recomendación**: (a).
- **Prioridad**: E

---

## Bloque 15 — Operación y soporte

### P-15-1 ¿SLA de disponibilidad, RPO y RTO?
- **Por qué importa**: define arquitectura de respaldo y costo. 04. H1.
- **Opciones**: (a) 99,5 % mensual, RPO 15 min (WAL), RTO 4 h; (b) 99,9 %, RPO 5 min, RTO 1 h (requiere réplica caliente); (c) sin compromiso.
- **Recomendación**: (a) en el MVP, (b) como plan Empresarial.
- **Prioridad**: N

### P-15-2 ¿Panel de plataforma (super-administrador)?
- **Por qué importa**: crear tenants, catálogo canónico, ver uso y costes de IA, soporte. 06. H1.
- **Opciones**: (a) sección `/plataforma` en `apps/web` con rol `Plataforma`; (b) app separada; (c) solo CLI.
- **Recomendación**: (a).
- **Prioridad**: B

### P-15-3 ¿Entornos por tenant o compartidos?
- **Por qué importa**: on-premise vs SaaS. 04. H1.
- **Opciones**: (a) SaaS: dev/test/prod compartidos; on-premise: una instancia por cliente con sus propios entornos; (b) todo por tenant.
- **Recomendación**: (a).
- **Prioridad**: N

### P-15-4 ¿Soporte al tercero: quién lo atiende?
- **Por qué importa**: el tercero verá la marca Validasoft y escribirá a la plataforma, pero la relación es con la empresa. 00, 06. H5.
- **Opciones**: (a) la empresa es primer nivel (contacto visible en la UI del tercero); la plataforma atiende incidentes técnicos; (b) la plataforma atiende todo.
- **Recomendación**: (a).
- **Prioridad**: N

---

## Bloque 16 — UX y accesibilidad

### P-16-1 ¿Idiomas del MVP?
- **Por qué importa**: proveedores extranjeros. 06. H4.
- **Opciones**: (a) es-CO; en preparado; (b) es + en en el MVP.
- **Recomendación**: (a).
- **Prioridad**: N

### P-16-2 ¿Tamaño máximo y formatos permitidos?
- **Por qué importa**: límites de IA (32 MB / 100 páginas por PDF) y almacenamiento. 04. H2.
- **Opciones**: (a) 20 MB por archivo, PDF/JPG/PNG/HEIC (convertido), hasta 50 páginas; formularios XLSX para EEFF opcional en H7; (b) 50 MB; (c) 10 MB.
- **Recomendación**: (a).
- **Prioridad**: B

### P-16-3 ¿Varias fotos → un PDF en el cliente?
- **Por qué importa**: el tercero fotografía páginas sueltas desde el celular. 06. H4.
- **Opciones**: (a) sí, con recorte y rotación básicos en el navegador; (b) servidor compone; (c) solo archivos.
- **Recomendación**: (a).
- **Prioridad**: N

### P-16-4 ¿Firma en pantalla de formularios (T&C, autorización de datos)?
- **Por qué importa**: evidencia de aceptación. 06, 07. H4.
- **Opciones**: (a) aceptación con clic + OTP + registro de IP/fecha/versión (firma electrónica simple); trazo en pantalla opcional en H8; (b) trazo obligatorio; (c) solo clic.
- **Recomendación**: (a).
- **Prioridad**: N

### P-16-5 ¿Nivel de accesibilidad comprometido?
- **Por qué importa**: clientes del Estado exigen WCAG. 06. H4.
- **Opciones**: (a) WCAG 2.1 AA en `apps/public`, AA en `apps/web` con excepciones documentadas (DataGrid); (b) AA total; (c) A.
- **Recomendación**: (a).
- **Prioridad**: E

---

## Bloque 17 — Gobierno del proyecto

### P-17-1 ¿Quién es Producto y quién aprueba cada documento?
- **Por qué importa**: sin dueño no hay cierre de decisiones. 09. H0.
- **Opciones**: (a) una persona de negocio como Producto + Arquitectura como aprobador técnico; (b) comité.
- **Recomendación**: (a).
- **Prioridad**: B

### P-17-2 ¿Herramienta de seguimiento y convención de ramas?
- **Por qué importa**: alinear con la práctica actual (Azure DevOps `sincosoftsas`). 09. H0.
- **Opciones**: (a) proyecto nuevo en Azure DevOps, épica por hito, HU por entregable, ramas `feature/H<n>-<tema>` → `main` por PR con dos aprobaciones; commits convencionales en español; (b) reutilizar el proyecto ADPROVEEDOR; (c) GitHub.
- **Recomendación**: (a).
- **Prioridad**: B

### P-17-3 ¿Definición de hecho de un hito?
- **Por qué importa**: evita hitos "casi listos". 09. H0.
- **Opciones**: (a) criterios de aceptación cumplidos + pruebas automatizadas verdes + demo grabada + docs y ADRs actualizados + sin decisiones abiertas bloqueantes del siguiente hito; (b) solo demo.
- **Recomendación**: (a).
- **Prioridad**: B

### P-17-4 ¿Cadencia de revisión?
- **Por qué importa**: ritmo de decisiones. 09. H0.
- **Opciones**: (a) revisión semanal de 1 h con Producto + demo al cierre de cada hito; (b) solo al cierre de hito.
- **Recomendación**: (a).
- **Prioridad**: N

---

## Tabla resumen (ordenada por prioridad)

| Id | Bloque | Prioridad | Recomendación |
|---|---|---|---|
| P-1-1 | Identidad | B | Clave país+tipo+número normalizado |
| P-1-7 | Identidad | B | Tercero propone, IA valida, publica |
| P-1-8 | Identidad | B | Globales vs privados según tabla |
| P-2-1 | Catálogo | B | Plataforma administra, tenant propone |
| P-2-3 | Catálogo | B | Semilla colombiana con defaults |
| P-2-4 | Catálogo | B | Restringir ámbito sí, ampliar no |
| P-3-1 | Formularios | B | Clase Formulario con esquema JSON |
| P-3-2 | Formularios | B | Esquema base + campos privados |
| P-3-3 | Formularios | B | Cuentas globales, designación por vínculo |
| P-3-6 | Formularios | B | Versión siempre, revalida solo críticos |
| P-6-2 | IA | B | Críticos según tabla de semilla |
| P-6-5 | IA | B | Consentimiento expreso incluye IA externa |
| P-11-1 | Comercial | B | Medir todo desde H1 |
| P-11-2 | Comercial | B | Grupo empresarial agrupa tenants |
| P-14-1 | Seguridad | B | 10 años comerciales, legal valida |
| P-14-2 | Seguridad | B | Región como atributo del tenant |
| P-14-3 | Seguridad | B | Clave KMS por tenant |
| P-15-2 | Operación | B | Panel plataforma en apps/web |
| P-16-2 | UX | B | 20 MB, PDF/JPG/PNG/HEIC, 50 páginas |
| P-17-1 | Gobierno | B | Una persona de Producto |
| P-17-2 | Gobierno | B | Azure DevOps nuevo, ramas por hito |
| P-17-3 | Gobierno | B | Hecho = criterios+tests+demo+docs |
| P-1-2 | Identidad | N | Tercero sucesor enlazado |
| P-1-4 | Identidad | N | Tipo genérico extranjero + país |
| P-1-6 | Identidad | N | N representantes, uno principal |
| P-1-9 | Identidad | N | Autorregistro invisible hasta vínculo |
| P-2-2 | Catálogo | N | Catálogo versionado |
| P-2-5 | Catálogo | N | Requisito con tipos aceptados |
| P-2-6 | Catálogo | N | Plantilla versionada por tipo |
| P-3-4 | Formularios | N | IA cruza formulario y soporte |
| P-3-5 | Formularios | N | Autorización por empresa + plataforma |
| P-4-1 | Expediente | N | Empresa, sistema, autorregistro |
| P-4-2 | Expediente | N | 30 días, caduca, reabre fácil |
| P-4-4 | Expediente | N | Paralelo por defecto |
| P-4-5 | Expediente | N | Escalar, nunca auto-aprobar |
| P-4-7 | Expediente | N | Cuatro ojos configurable, bancario sí |
| P-4-9 | Expediente | N | Compras, Legal, Financiera, Cumplimiento |
| P-5-1 | Veredictos | N | Motivos base + propios |
| P-5-2 | Veredictos | N | Por requisito con campos afectados |
| P-5-3 | Veredictos | N | Bloqueo privado por tenant |
| P-5-4 | Veredictos | N | Eximir con permiso y justificación |
| P-5-5 | Veredictos | N | Rechazo terminal, expediente nuevo |
| P-6-6 | IA | N | Prompt retenido como el documento |
| P-6-9 | IA | N | < 60 s p95, aviso si tarda |
| P-7-5 | Vigencias | N | Revalidar recalcula vigencia |
| P-8-1 | Certificado | N | Datos mínimos, NIT enmascarado |
| P-8-2 | Certificado | N | Por vínculo con historial |
| P-8-4 | Certificado | N | Sin certificado parcial |
| P-10-1 | Alertas | N | Catálogo propuesto |
| P-10-2 | Alertas | N | Correo, campana, webhook |
| P-10-4 | Alertas | N | Dominio plataforma, Reply-To empresa |
| P-12-1 | Integración | N | Reconciliar por clave única |
| P-12-2 | Integración | N | Webhooks y pull desde MVP |
| P-12-4 | Integración | N | Revisión solo en Validasoft |
| P-14-4 | Seguridad | N | 2FA admins, auditores, cumplimiento |
| P-14-5 | Seguridad | N | Flujo formal de supresión |
| P-14-6 | Seguridad | N | Contrato marco + anexo técnico |
| P-15-1 | Operación | N | 99,5 %, RPO 15 min, RTO 4 h |
| P-15-3 | Operación | N | SaaS compartido, on-prem por cliente |
| P-15-4 | Operación | N | Empresa primer nivel |
| P-16-1 | UX | N | es-CO, en preparado |
| P-16-3 | UX | N | Fotos a PDF en cliente |
| P-16-4 | UX | N | Clic + OTP + evidencia |
| P-17-4 | Gobierno | N | Semanal + demo por hito |
| P-1-3 | Identidad | E | Sucursales como atributo |
| P-1-5 | Identidad | E | Consorcio con miembros |
| P-4-3 | Expediente | E | Sin límite, alerta al tercero |
| P-4-6 | Expediente | E | Delegación y reasignación |
| P-4-8 | Expediente | E | Separación configurable, apagada |
| P-6-1 | IA | E | 0,95 / 0,80 por campo |
| P-6-3 | IA | E | 5 % muestreo |
| P-6-4 | IA | E | Degradar a Manual con alerta |
| P-6-7 | IA | E | Cobro por decisión + vínculo |
| P-6-8 | IA | E | Derecho a revisión humana |
| P-7-1 | Vigencias | E | 30/15/5 y 60/30/7 |
| P-7-2 | Vigencias | E | Gracia 15 días, 0 críticos |
| P-7-3 | Vigencias | E | Certificado con pendientes |
| P-7-4 | Vigencias | E | Campañas de renovación |
| P-8-3 | Certificado | E | Sello propio + hash, firma después |
| P-8-5 | Certificado | E | PDF/A-2b español |
| P-9-1 | Verificaciones | E | Los contratados + evidencia manual |
| P-9-2 | Verificaciones | E | Vincular, renovar, 12 meses |
| P-9-3 | Verificaciones | E | Cada empresa con su contrato |
| P-9-4 | Verificaciones | E | Coincidencia analizada y justificada |
| P-9-5 | Verificaciones | E | RUES/DIAN fase posterior |
| P-9-6 | Verificaciones | E | Legado + matriz configurable |
| P-10-3 | Alertas | E | 1 correo/tercero/hecho/día |
| P-10-5 | Alertas | E | Cabecera/pie, Liquid después |
| P-11-3 | Comercial | E | Logo ahora, dominio después |
| P-11-4 | Comercial | E | Sandbox por API key |
| P-12-3 | Integración | E | SDK .NET y TS |
| P-13-1 | Migración | E | Activos + 3 años |
| P-13-2 | Migración | E | Huérfanos a bóveda sin aceptada |
| P-13-3 | Migración | E | Migrar correo, acceso por enlace |
| P-13-4 | Migración | E | 90 días doble operación |
| P-14-7 | Seguridad | E | Logs como el documento |
| P-16-5 | UX | E | AA público, AA web con excepciones |

Total: **97 preguntas** — 22 bloqueantes (B), 42 necesarias antes del MVP (N), 33 que pueden esperar (E).

## Lo que el experto añadiría al alcance y nadie ha mencionado

1. **Beneficiario final (UBO)**: SAGRILAFT exige identificar personas naturales con ≥ 5 % de propiedad o control. Debe ser un formulario global con cadena de propiedad y cruce de listas de cada beneficiario; hoy el legado solo tiene "socios" sin verificación.
2. **Personas expuestas políticamente (PEP)**: declaración del tercero y de sus representantes, con reconsulta anual y marca de riesgo; condiciona el nivel de debida diligencia (intensificada).
3. **Vigencia de la verificación de identidad del representante**: cuando cambia el representante legal (hecho detectado por IA en la Cámara nueva), deben expirar automáticamente la cédula aceptada y la consulta de listas del anterior.
4. **Evidencia de entrega de notificaciones**: para disputas ("nunca me avisaron") hay que guardar acuse de entrega SMTP, apertura y clic por correo, no solo el envío.
5. **Historial exportable del tercero**: el tercero debe poder descargar su bóveda completa (portabilidad de datos) y la empresa su expediente completo en un ZIP con índice firmado (auditorías externas, litigios).
6. **Detección de documentos manipulados**: la IA debe reportar señales de alteración (capas, fuentes inconsistentes, metadatos de edición, fecha de creación del PDF posterior a la de expedición) como hecho compartido; es el fraude más común en certificaciones bancarias y pólizas.
7. **Conflicto de intereses del revisor**: declaración y bloqueo automático si el revisor es contacto o representante del tercero (cruce por identificación).
8. **Reglas de exigencia por monto o riesgo del contrato**: el motor ya acepta `vinculo.atributos.*`; la semilla debería incluir "monto estimado" y "nivel de riesgo" para que un proveedor de bajo monto no pase por el flujo completo.
9. **Modo "solo lectura de emergencia"**: si Validasoft cae, Marco necesita seguir operando; la tabla local de respaldo por webhooks debe incluir el último certificado y su vigencia, y la consulta debe degradar a ella con bandera `fuenteRespaldo=true`.
10. **Métricas de equidad de la IA**: medir tasa de observación automática por tipo de persona, tamaño de empresa y región para detectar sesgo del modelo (documentos manuscritos de personas naturales rurales suelen fallar legibilidad); es parte del muestreo humano.

## Decisiones abiertas

Todas las de este documento; se cierran en `registro.md`.

## Fuentes verificadas

Ver `../01-analisis-legado-srm-portal.md` para las referencias al comportamiento actual citado en "Por qué importa" (perfiles de aprobación, `BancosConstructora`, `DocPlantilla`, 180 días de vencimiento, estudio de cumplimiento, autorregistro con IA, proveedores de listas restrictivas).
