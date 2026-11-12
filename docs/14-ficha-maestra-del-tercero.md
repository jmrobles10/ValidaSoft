# 14 — Ficha maestra del tercero

| Estado | Fecha | Responsable |
|---|---|---|
| Aprobado por Producto | 2026-11-09 | Arquitectura |

**Propósito.** Validasoft se vuelve el **dueño de la información del tercero**: la que hoy vive en las tablas del Portal de Proveedores y que el SRM usa para validar, aprobar y crear el proveedor en el ERP. Este documento cuenta lo que hay hoy (§1–§2), cómo lo modela Validasoft (§3–§7), cómo se migra (§8) y en qué orden se construye (§9). Las decisiones están en el [registro](decisiones/registro.md) (FM-1 a FM-16) y en [ADR-0013](adr/0013-ficha-maestra-del-tercero.md).

**Relacionados.** [01](01-analisis-legado-srm-portal.md) · [02](02-modelo-documental-compartido.md) · [03](03-dominio-y-eventos.md) · [08](08-integracion-y-migracion.md) · [ADR-0004](adr/0004-boveda-global-y-tenancy-conjoined.md) · [ADR-0005](adr/0005-version-unica-y-revalidacion.md) · [ADR-0010](adr/0010-personas-relacionadas-con-boveda-propia.md) · [ADR-0012](adr/0012-ia-openai-con-cupo-por-empresa.md).

---

## 1. Lo que guarda hoy el Portal (fuente de verdad del legado)
Casi todo es **global del tercero**: una sola verdad compartida por todas las constructoras. Las rutas son de `C:\Fuentes_Git\PortalProveedores\backend\librerias`.

| Sección | Tablas | Contenido | Por constructora |
|---|---|---|---|
| General | `TerInformacionGeneral`, `TerDireccionEstructurada` | Razón social o nombres y apellidos, tipo de persona (N/J), tipo y número de identificación, DV, correo, ciudad (DANE), dirección (formato AyF, estructurada), CIIU principal, teléfono, web, certificado ISO, país, año de inicio | — |
| Tributaria | `TerInfoTributaria`, `TerRetencionICA` (N) | 13 calidades S/N (responsable de IVA, autorretenedor con resolución y fecha, declarante, gran contribuyente con resolución y fecha, autorretenedor ICA, régimen simple, gran contribuyente Bogotá, régimen especial, autorretenedor RFT, descuento SENA/FIC, profesional independiente, exento de retención, agente retenedor de IVA, obligado a facturar); municipios y actividad para ICA | — |
| Bancaria | `TerCuentaBancaria` (N), `BancosConstructora` | Banco (catálogo), tipo, número, SWIFT, contacto de pagos | **Cuenta asociada** y su soporte |
| Financiera | `TerInfoFinanciera` (una fila por año), moneda extranjera | Activos, pasivos, patrimonio, ingresos brutos y netos, gastos operacionales y no operacionales, otros ingresos; transacciones en moneda extranjera por tipo y país | — |
| Legal | `TerCamaraComercio` (N) | Representantes (principal, suplente, apoderado), socios/accionistas/junta, revisor fiscal (o firma), contador; identificación, cargo, % de participación, PEP y vínculo PEP, expedición, nacimiento, residencia, nacionalidad, contacto; soporte de beneficiarios finales por socio PJ | — |
| Contactos | `TerDatosContacto`, `Usuario` | Tipo de contacto, nombre, documento, correo, teléfonos, ciudad, dirección | Cargo del contacto |
| SISO/SST | `TerSISO` | 8 preguntas S/N (programa de salud ocupacional, factores de riesgo, comité, higiene, ambiental, reglamento interno, política HSEQ, licencias) | — |
| Especialidades | `TerEspecialidad`, catálogo grupo → categoría → especialidad | Lo que el proveedor ofrece | — |
| Términos y tratamiento | `TercerosConstructora.Autorizo`, adjunto 1011 | Aceptación de términos; autorización de datos firmada | **Sí** |

**Problemas del legado que no se heredan** (detalle en el análisis del 2026-11-09):
- **Versionado:** cada guardado sobrescribe y un rechazo no revierte el dato.
- **Soportes:** no tienen dueño ni tipo documental y no se comprueba que respalden el dato.
- **Validaciones:** el DV acepta 0 o el valor del cliente, `NoObligFacturar` tiene la semántica invertida y el OCR mapea mal algunas casillas del RUT.
- **Tipos de dato:** fechas, lugares y porcentajes legales se guardan como texto.
- **Integridad:** no hay integridad referencial ni índice único por NIT.
- **Seguridad:** cualquier ApiKey lee o edita a cualquier tercero.

## 2. Cómo la usa el SRM
- **Completitud como regla de estado.** De «Documentación pendiente» a «completa» solo si:
  - General tiene sus obligatorios;
  - si es jurídica, hay al menos un representante legal y un socio;
  - hay al menos una especialidad;
  - existe la cuenta asociada a la constructora;
  - la tributaria está diligenciada;
  - los términos están aceptados.

  Cada faltante deja un motivo legible.
- **Rechazo con motivos de tipo sección, contacto o documento.** Cada motivo queda pendiente hasta que el proveedor corrige.
- **Aprobación del proveedor nuevo:** global por perfil. En las **actualizaciones posteriores**, aprueba **por sección** un responsable por sector, comparando el valor anterior con el nuevo y abriendo el soporte.
- **Aplicación en AyF al aprobar:**
  - General, Tributaria y el representante legal se envían en un PUT mezclado.
  - La cuenta bancaria queda como principal.
  - Financiera y Documentos no se envían.
  - Si AyF falla, la aprobación se revierte.
- **Vencimiento por sector** (días y preaviso por sección).
- **Datos que consumen otros módulos:**
  - especialidades: documentos requeridos, licitaciones y directorio;
  - Cámara: listas restrictivas;
  - ingresos: tamaño de empresa por UVT;
  - tributaria y bancaria: retenciones y pagos;
  - contactos de notificación;
  - hoja de vida en PDF.
- **Sin cruce automático entre lo declarado y el documento.** La IA del autorregistro del Portal, que lo hacía, está desactivada.

## 3. Modelo en Validasoft
- **Sección = formulario canónico global** (FM-2). Cada sección es un tipo canónico de clase `Formulario` con ámbito `Global` y esquema versionado de plataforma (P-3-1). Sus versiones viven en la bóveda del tercero (stream `Documento`, como un documento):
  - el tercero propone una versión nueva y la anterior queda superada;
  - la versión guarda los datos normalizados y la huella SHA-256 de los datos (TD-4).
- **Compartición y aceptación por empresa** (FM-3). Igual que un documento global:
  - se comparte con las empresas cuyo flujo la pide;
  - cada empresa la **acepta u observa** con motivos tipificados, en su expediente y por sus áreas;
  - su aceptación es privada; una versión nueva **revalida** en todas las empresas (RV-x), con el aviso de NW-17.
- **El documento como fuente de las secciones** (FM-5, FM-13). La relación es de muchos a muchos y está a nivel de campo (§5.1): un mismo RUT respalda General y Tributaria. La versión de la sección guarda `respaldadaPor[]: { documentoId, version, campos[] }`.
- **Personas relacionadas** (FM-7):
  - **Fase 1:** la sección `LEGAL` guarda filas tipadas: rol `RepresentanteLegal` / `Suplente` / `Apoderado` / `Socio` / `JuntaDirectiva` / `RevisorFiscal` / `Contador`, identificación, porcentaje y PEP.
  - **Fase 2:** cada persona se vuelve un tercero persona natural con bóveda propia (ADR-0010) y su cédula se valida una sola vez.
- **Ya cubierto, no cambia:**
  - **Bancaria:** cuentas globales y cuenta designada por vínculo con 4 ojos (CB-1..CB-3); se agrega el cruce con la certificación.
  - **Contactos:** se mantienen en el tercero; el cargo por empresa queda para después.
  - **Términos y tratamiento:** consentimientos por empresa (ADR-0009).

## 4. Secciones canónicas y sus campos
Esquemas base de plataforma. Las empresas pueden agregar **campos privados** por vínculo (P-3-2), que no viajan a otras empresas. Las claves van en snake_case; los catálogos son de plataforma.

| Código | Campos (resumen) | Varias filas | Respaldo |
|---|---|---|---|
| `FICHA_GENERAL` | razon_social / nombres + apellidos, tipo_persona, tipo_identificacion, numero, dv (calculado DIAN módulo 11; no editable), correo, telefono, pais (ISO), ciudad (DANE), direccion (+ estructura AyF), ciiu_principal, ciiu_secundarias, pagina_web, certificado_iso, anio_inicio. **Solo persona natural** (opcionales, FD-8): pep, vinculo_pep, fecha_expedicion, lugar_expedicion, declaracion_pep (obligatoria con pep = Sí, §4.2); lo que no aplica al tipo de persona se rechaza (`campo-no-aplica`) | — | RUT, Cámara, cédula |
| `FICHA_TRIBUTARIA` | responsabilidades (códigos DIAN de la casilla 53 del RUT), resoluciones de gran contribuyente y autorretenedor (número, fecha), ica[] (municipio DANE, CIIU); las 13 calidades del Portal **se derivan** de los códigos | ica[] | RUT |
| `FICHA_FINANCIERA` | periodo (año), fecha_corte, activos, pasivos, patrimonio, ingresos_brutos, ingresos_netos, gastos_operacionales, gastos_no_operacionales, otros_ingresos; validación activos = pasivos + patrimonio (con tolerancia); moneda_extranjera { realiza, tipos[], paises[] } | una versión por periodo | Estados financieros del periodo |
| `FICHA_LEGAL` | personas[] { rol, tipo_representacion, tipo_identificacion, numero, nombres, apellidos, cargo, porcentaje (0–100, suma de socios ≤ 100), pep, vinculo_pep, declaracion_pep (§4.2), fecha_expedicion, fecha_nacimiento, nacionalidad, residencia }; revisor_fiscal_firma? | personas[] | Cámara; RUT (representantes) |
| `FICHA_SST` | las 8 preguntas S/N | — | Certificado ARL/SG-SST (opcional por empresa) |
| `FICHA_ESPECIALIDADES` | especialidades[] (catálogo de plataforma de 3 niveles) | sí | — |

Reglas de completitud (FM-6), configurables por flujo:
- `FICHA_GENERAL` siempre.
- `FICHA_LEGAL` con al menos un representante legal y un socio si el tercero es persona jurídica.
- `FICHA_ESPECIALIDADES` con al menos una especialidad.
- La cuenta designada (CB-2) cuando el flujo pide pagos.

### 4.1 Completitud de cada sector: un dato guardado y exacto (FM-16)
El wizard pasa solo al siguiente sector cuando el actual está **completo**. Ese «completo» no lo deduce la pantalla ni se calcula distinto en cada consulta. Es un **dato persistido** que calcula **una sola regla en el servidor**, la misma que valida al guardar.

**Lo que no se repite del legado:**
- **Ceros fabricados.** El GET de la financiera del Portal insertaba filas en cero y la sección quedaba «completa» con solo abrirla.
- **Reglas duplicadas.** La completitud se calculaba distinto en el SRM (`ValidarEstadoTercero`), en el wizard nuevo del Portal (`RegistroEstadoService`) y en el front.
- **Estado inferido.** Los motivos se recalculaban borrando y recreando `DocumentacionPendiente`, y había estados que nadie sabía de dónde salían.

**Cómo se hace en Validasoft:**
- **Una regla por sección, versionada.** Cada esquema canónico trae sus reglas de completitud:
  - obligatorios por tipo de persona;
  - mínimos de filas: legal con al menos un representante legal y, si es jurídica, un socio; especialidades con al menos una; ICA cuando hay responsabilidad de ICA;
  - coherencias: activos = pasivos + patrimonio, con tolerancia; suma de participaciones ≤ 100 %; resolución y fecha cuando es gran contribuyente o autorretenedor.

  La regla vive en el dominio (C#). Es **la misma** que rechaza un guardado inválido y la que calcula la completitud, y tiene su tabla de casos en pruebas.
- **Vacío no es cero.** Un campo sin diligenciar es `null`. Una cifra en 0 es un valor declarado. Ninguna lectura crea datos.
- **Se guarda con la versión.** Cada versión de la sección guarda `completitud: { completa, faltantes[] { campo o regla, mensaje }, reglaVersion }`, calculada al guardarla. Además hay una proyección `EstadoDeFicha` por tercero, con una fila por sección: sección, versión vigente, completa, faltantes y fecha. El wizard y el SRM leen esa proyección; nadie recalcula.
- **Guardado parcial permitido.** El tercero puede guardar a medias, como borrador de la sección; la versión queda `completa = false` con sus faltantes exactos. El wizard avanza solo con `completa = true`, y el servidor lo confirma en la respuesta de guardado (`siguienteSeccion`).
- **Dos niveles:**
  - **completa (base):** cumple el esquema canónico; es la que mueve el wizard.
  - **completa para la empresa X:** además cumple lo que pide el flujo de esa empresa (campos privados obligatorios, periodos financieros adicionales, soporte exigido). Es la que alimenta la completitud del expediente de esa empresa y su motivo legible («Falta el soporte de la información bancaria»).
- **Si cambia la regla:** cuando se publica una versión nueva del esquema o de sus reglas, un proceso recalcula `EstadoDeFicha` de todos los terceros y deja registrada la versión de regla con que se calculó. Así ninguna marca queda desactualizada en silencio.
- **Pruebas:** tabla de casos por sección (persona natural y jurídica, filas mínimas, coherencias, null frente a 0) y pruebas de integración que guardan, leen la proyección y verifican que el wizard avance solo cuando corresponde.

### 4.2 Declaración jurada PEP (HU 49093, DP-1…DP-16)
Quien responde «Sí» a «¿Es PEP?» firma su **declaración jurada**: la de la persona natural va en la General (`declaracion_pep`) y, en la jurídica, una por cada persona de la legal (`personas[i].declaracion_pep`). No es un documento aparte: vive **dentro de los datos de la sección**, así que se versiona, se hashea, se compara y la revisa la empresa como el resto de la ficha, y nunca hay una respuesta sin su declaración en la misma versión.

- **Obligatoriedad.** Con `pep = true` el backend la exige (422 `declaracion-pep-requerida` al guardar y en la vista previa); lo que llega de un sistema de origen se guarda y queda como faltante. La otra pregunta (`vinculo_pep`) no la abre. Con «No» se conserva inactiva y nada la muestra.
- **Contenido** (los cuatro bloques de la plantilla): 1) datos básicos que fija el servidor (relación con la empresa según el rol, nombre, documento, nacionalidad y dirección); 2) condición PEP (cargo, institución, país, periodo o «vigente», cuentas en el exterior Sí/No con países, cónyuge todo-o-nada, familiares y sociedades fila completa, hasta 30 de cada una); 3) vínculo con otra PEP (Sí/No y, si es Sí, nombre, parentesco, cargo e institución y periodo); 4) juramento, firma en nombre propio o como representante legal, y la imagen de la firma.
- **Firma y sello.** La imagen (dibujada o subida; PNG de 600×200 normalizado por el navegador) viaja en base64 dentro de la declaración y el servidor la decodifica entera. Fija `firma_huella` (SHA-256), `fecha_firma` (día del negocio) y `firma_ip`: son la evidencia de la aceptación y se conservan mientras el contenido no cambie; una declaración reactivada (de «No» a «Sí») se firma de nuevo.
- **Lo que ve la empresa.** El bloque «Declaración jurada PEP» de la persona en la revisión de la sección, la ficha aceptada y Tercero 360, con **Descargar PDF** (`GET /api/v1/terceros/{id}/declaraciones-pep/pdf`), solo si `pep = true` y hay declaración; el detalle de personas del informe SAGRILAFT (con «Sin declaración» si falta) y el ZIP del expediente (`declaraciones-pep/`). El contenido para el ERP lleva un resumen (`declaracionPep`: fecha, calidad y huella de la firma).
- **Importación.** Del Portal se importa la declaración activa de un contacto con «Sí» (con la huella de la firma: la imagen está en el disco del Portal); un «Sí» sin declaración queda como faltante y el tercero la completa.
- **Versiones.** General y Legal pasan a esquema 2 y reglas 2 (FM-16): al recalcular, una ficha con «Sí» y sin declaración queda incompleta con su faltante exacto. El canónico `DECLARACION_PEP` se retira (DP-13): lo reemplaza este formulario.

## 5. El documento como fuente de las secciones

### 5.1 Qué respalda cada documento (mapa de plataforma, FM-13)
El catálogo canónico declara, por tipo de documento, **qué campos de qué secciones respalda**. Son los mismos campos que la IA extrae del documento (ADR-0012):

| Documento | Respalda |
|---|---|
| RUT | `FICHA_GENERAL`: identificación + DV, razón social / nombres, tipo de persona, dirección, ciudad, CIIU principal y secundarias, correo, teléfono. `FICHA_TRIBUTARIA`: responsabilidades (casilla 53). `FICHA_LEGAL`: representantes inscritos en el RUT |
| Cámara de comercio | `FICHA_LEGAL`: representantes legales y suplentes, revisor fiscal, socios y junta cuando aparecen, estado de la matrícula. `FICHA_GENERAL`: razón social, NIT, domicilio |
| Estados financieros | `FICHA_FINANCIERA` del periodo: las cifras |
| Certificación bancaria | Las **cuentas bancarias** del tercero (banco, tipo, número, titular y que esté activa), CB-1: no es una sección de la ficha sino su propia sección de cuentas (`CUENTAS_BANCARIAS`, FD-5) |
| Cédula | La identificación de la persona natural (`FICHA_GENERAL`) o del representante (la persona de `FICHA_LEGAL`, FD-6) |

### 5.2 Requerido como documento **y** usado como soporte (FM-13)
Hoy la constructora exige el RUT, pero el RUT no sirve para validar lo que el proveedor diligenció en el wizard. En Validasoft, cada requisito de documento del flujo tiene la opción **«Usar como soporte de las secciones»**, activa por omisión cuando el tipo canónico respalda alguna sección. Con la opción activa:
- **Al revisar el requisito**, el revisor ve el documento junto con los campos de las secciones que respalda y el cruce campo por campo (§6). Aprobar el documento aprueba también su papel de soporte.
- **Si hay discrepancias en campos críticos** (identificación, razón social, representante legal, cuenta), el requisito no se aprueba en lote ni automáticamente (H7). El revisor decide entre:
  - observar el documento (el tercero sube otro);
  - observar la sección (el tercero corrige el dato);
  - aceptar con justificación.
- **La empresa puede desactivar la opción** para un requisito: el documento se sigue pidiendo y revisando, pero no se cruza con las secciones.

**Construido en F5 ([registro](decisiones/registro.md) FM-51…FM-53):** la opción es `usarComoSoporte` del requisito del flujo (`null` = activa); el requisito de documento trae `soporteDeSecciones` (qué secciones respalda y el resultado de su cruce) y el de sección trae su `cruce`. Una discrepancia crítica pide una justificación de al menos 10 caracteres al aprobar (por documento y por área) y no se aprueba en lote. La empresa solo ve el cruce de los documentos cuyo análisis recibió (ADR-0012): sin análisis, revisión manual.

**Si el tercero retira el documento que respalda una sección (RT-4, [registro](decisiones/registro.md)).** FM-20 solo admite como soporte una versión `Actual`, `Superada` o `Vencida`; una `Retirada` ya no sirve. La sección **no se recalcula ni pierde datos** (una versión de sección es inmutable y su completitud ya se evaluó con el soporte que tenía) y no se reabre nada en las empresas que la aceptaron: es una **advertencia**, no un faltante. El tercero la ve en la sección («Tu soporte (Registro Único Tributario, versión 8) fue retirado»; el editor lo saca de lo que se reenvía) y en su resumen de Mi información; el diálogo de retiro (RT-1) nombra de antemano las secciones que la usan. Al guardar la sección de nuevo rige la regla de siempre: si los datos cambiaron y no hay un soporte válido, vuelve el faltante «Falta el soporte del cambio».

### 5.3 Actualizar desde el documento (FM-14)
Simplifica lo que hoy es la feature de **Actualizaciones** del SRM (`ActualizacionProveedor`). Hoy son un detalle por sector, la comparación del valor anterior con el nuevo y un soporte genérico. Con Validasoft la actualización nace del documento:
1. El tercero sube la versión nueva del RUT (o de la Cámara, los estados financieros o la certificación).
2. La IA extrae los campos (ADR-0012) y la plataforma **propone** los cambios a las secciones que ese documento respalda, campo por campo: valor actual, valor del documento y si cambió. Por ejemplo: «dirección: Cra 10 # 20-30 → Cl 45 # 12-08» o «Gran contribuyente: No → Sí».
3. El tercero **confirma** la propuesta, corrige lo que la IA leyó mal o deja algún campo como está. Sin IA, el tercero diligencia las secciones afectadas con el documento a la vista. Lo que confirma son versiones nuevas de esas secciones, respaldadas por el documento.
4. Cada empresa recibe **una sola actualización**: el documento y las secciones que cambió (NW-17, RV-x). La revisión muestra tres columnas, anterior → nuevo → lo que dice el documento, solo con los campos que cambiaron.
5. Cada **área responsable** aprueba u observa por sección, con motivos. Por ejemplo, Tesorería aprueba la bancaria y Contabilidad la tributaria: es el responsable por sector del SRM, configurado como áreas que revisan cada requisito.
6. Al aceptar, se emite `seccion.aceptada` (FM-9) y el SRM aplica en AyF lo aceptado.

**Construido en F5 (FM-48, FM-54):** `GET /public/v1/ficha/propuestas` y `GET /public/v1/ficha/{seccion}/propuestas`; en el portal, «Actualizar desde mi RUT / Cámara…» con la propuesta campo por campo, que el tercero confirma o corrige (nunca se guarda sola). Si ya había una actualización abierta (el RUT nuevo abre la suya), la sección cambiada **entra a esa misma actualización** en lugar de esperar a que se cierre: así cada empresa recibe **una sola actualización** con el documento y las secciones que cambiaron. El aviso al tercero es uno por documento (UI-6): en Mi información, una fila por documento con las secciones que afecta como chips; en el wizard, un solo aviso con una línea por documento y los cambios campo por campo a pedido.

Si el tercero cambia una sección **sin** documento nuevo (FM-15), la versión queda marcada **«sin soporte»** frente a los campos que el documento vigente no confirma. La empresa configura qué pasa entonces:
- se exige un documento nuevo para aceptarla (por omisión en los campos críticos);
- o se permite aceptarla con justificación.

**Construido (FD-4, [registro](decisiones/registro.md)):** la marca se llama **«sin soporte»** (un solo término: el documento que respalda una sección es su *soporte*) y es un hecho que se calcula de las versiones inmutables: los datos cambian respecto de la última versión completa (o de lo que la empresa aceptó) y la versión no trae ningún soporte nuevo. Si lo cambiado es crítico (razón social o nombres de la General, representante legal o suplente de la Legal), aprobarlo pide una justificación y no va en lote; la empresa que quiere el documento nuevo observa la sección señalando el soporte. La configuración por flujo («exigir siempre documento nuevo») queda pendiente.

## 6. Cruce sección ↔ documento
Cuando hay hechos de IA del documento de respaldo (ADR-0012), la plataforma compara campo por campo (FM-8):

| Sección | Campo | Contra | Regla |
|---|---|---|---|
| General | número + DV, razón social / nombres, tipo de persona, CIIU, dirección y ciudad | RUT; Cámara | Identificación exacta; nombre con normalización (mayúsculas, tildes, «S.A.S.»); ciudad por código DANE |
| Tributaria | responsabilidades | RUT casilla 53 | Igualdad de conjuntos |
| Legal | representantes (nombre, identificación), estado de la matrícula | Cámara; RUT | Cada representante declarado aparece en el documento y viceversa |
| Bancaria | banco, tipo, número, titular e identificación del titular | Certificación bancaria | Número exacto; titular = el tercero |
| Financiera | las cifras del periodo | Estados financieros del periodo | Diferencia ≤ 0,5 % |

- **Resultado por campo:** `Coincide`, `Discrepa` (con ambos valores) o `NoVerificable` (sin análisis, campo ausente o documento ilegible). El resultado se guarda en la versión de la sección, como hecho compartido por todas las empresas.
- **Para el revisor:** es evidencia, no decide nada por sí mismo.
- **En revisión automática (H7):** una sección con todos sus campos críticos en `Coincide` puede aprobarse sola según el nivel de la empresa; una `Discrepa` en un campo crítico va a humano.

## 7. Eventos e integración
- **Eventos de dominio:** los del documento (versión cargada, compartida, aceptada…), más `CruceDeSeccionRegistrado(numero, resultados[])` (en el stream de la sección) y `RequisitosAgregados(requisitos[])` (en el expediente de actualización, FM-49).
- **Evento público** `seccion.aceptada { terceroId, vinculoId, empresaId, seccion, periodo?, version, aceptadaEn, contenido }`, emitido cuando una empresa acepta una versión (FM-9, FM-32). `contenido` es lo aceptado normalizado para el ERP, con las 13 calidades derivadas de los códigos DIAN (FM-33) y el representante legal que usa AyF (FM-34); el detalle está en [05 §6.6](05-api-contratos-y-webhooks.md). El SRM lo aplica en AyF con el mapeo de hoy: General, Tributaria, el representante legal y la cuenta designada. Si AyF falla, el SRM lo reintenta y lo deja en su bandeja; la aceptación en Validasoft no se revierte, porque es una decisión de negocio, no una transacción distribuida.
- **API para el Portal, el SRM y Marco:**
  - `GET /api/v1/vinculos/{id}/ficha`: lo **aceptado** por esa empresa, sección por sección.
  - `GET /api/v1/terceros/{id}/ficha`: se amplía Tercero 360 con las secciones.
  - `GET /api/v1/vinculos/{id}/ficha-aceptada` (FD-3): lo aceptado por la empresa **para leerlo en la web** (datos tal como los guardó el tercero, versión, expediente y soporte), con la versión nueva del tercero aparte (`cambioPosterior`) y su marca «sin soporte».
  - `GET /public/v1/ficha` trae las observaciones de las empresas (FD-2) y `GET /public/v1/ficha/FICHA_GENERAL` la sugerencia `prellenado` mientras no haya versión (FD-1).

## 8. Migración y coexistencia
- **Importador (H10):** cada tabla `Ter*` del Portal se carga como la **versión 1** de su sección, con origen `Importada`. En cada empresa donde el tercero estaba `Completada` o en actualización, queda **aceptada** con su vigencia. Mientras se importa se aplican estas correcciones:
  - recalcular el DV;
  - traducir las 13 calidades tributarias a códigos DIAN (con `NoObligFacturar` normalizado);
  - convertir fechas y porcentajes de texto;
  - deduplicar personas por identificación;
  - mapear ciudades a DANE y bancos al catálogo.

  Lo que no se pueda convertir va al CSV de rechazos.

  Construido en F4: endpoints, tablas del traductor, idempotencia y aceptación importada en [08 §6.6](08-integracion-y-migracion.md) y FM-39…FM-45. Las versiones que llegan del legado (origen `Importada` o `Portal`) no exigen el soporte del cambio.
- **Coexistencia por empresa** (FM-11). Mientras una empresa no haya hecho el corte, el Portal sigue siendo su UI. Un sincronizador de un solo sentido, del Portal a Validasoft, lleva cada guardado de sección como una versión nueva, con origen `Portal`. Después del corte, el Portal lee la ficha de Validasoft o embebe su UI, y ya no escribe en `Ter*`. El sincronizador es `POST /api/v1/plataforma/importacion/portal/terceros/{terid}/sincronizar`, que el Portal llama tras cada guardado (FM-43, FM-44).

## 9. Orden de construcción
Producto pidió (2026-11-09) **primero la paridad con el SRM, sin IA**: los sectores de información con sus soportes, más la sección de documentos que ya existe. La parte de la información es global del tercero, y cada empresa hace su propia validación. La IA entra al final, sobre una base que ya funciona como hoy.

| Entrega | Contenido |
|---|---|
| F1 | **Sectores de información con soporte y su completitud guardada** (§4.1). Catálogos de plataforma: DANE, CIIU, bancos, responsabilidades DIAN y especialidades de 3 niveles. Esquemas canónicos de `FICHA_GENERAL`, `FICHA_TRIBUTARIA` (+ ICA), `FICHA_FINANCIERA` (por periodo + moneda extranjera), `FICHA_LEGAL` (personas, fase 1), `FICHA_SST` y `FICHA_ESPECIALIDADES`. El tercero los diligencia en el portal (el «wizard») y quedan como versiones globales en la bóveda. Cada versión guarda su **soporte**, como en el SRM: la versión de un documento de la bóveda (RUT, Cámara, certificación, estados financieros) o un archivo adjunto. Qué tipos sirven de soporte para cada sector sale del mapa de §5.1. |
| F2 | **Validación por empresa en paridad con el SRM.** Las secciones son requisitos del flujo, con completitud y su motivo legible. Cada empresa acepta u observa cada sección por área, con motivos. Al cambiar una sección aceptada, se revalida **solo esa sección**, con comparación anterior → nuevo y su soporte: son las Actualizaciones del SRM, con un responsable por sección configurado como áreas que revisan. La empresa ve la ficha en Tercero 360 y en el expediente. |
| F3 | **Integración con el SRM:** evento `seccion.aceptada` y `GET /api/v1/vinculos/{id}/ficha`, para que el SRM aplique en AyF lo aceptado (General, Tributaria, representante legal y cuenta designada). |
| F4 | **Importador** desde las tablas `Ter*` del Portal y **sincronizador** de coexistencia. |
| F5 | **IA:** cruce sección ↔ documento, la opción «Usar como soporte» con cruce automático en los requisitos de documento y la **actualización desde el documento** (§5.2, §5.3, §6). **Terminada (2026-11-10, FM-48…FM-55):** además de lo anterior, la actualización abierta se amplía con lo que cambia después. **Pendientes cerrados el 2026-11-11 (FD-1…FD-9):** la cédula respalda también la Legal, la certificación bancaria respalda las cuentas y un cambio sin documento nuevo se marca «sin soporte». |
| F6 | Personas relacionadas con bóveda propia (fase 2) y cargo de los contactos por empresa. |

## Decisiones abiertas
1. ~~¿Las 13 calidades tributarias se exponen al SRM como hoy (S/N) además de los códigos DIAN?~~ Resuelta en FM-33: sí, derivadas de los códigos, y `null` en las que no se pueden derivar.
2. ¿Cuántos periodos financieros se exigen? Propuesta: el último cierre, y el anterior si la empresa lo configura.
3. ¿La SST se respalda con un documento? Propuesta: opcional por empresa.
