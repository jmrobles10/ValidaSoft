# 14 — Ficha maestra del tercero

| Estado | Fecha | Responsable |
|---|---|---|
| Propuesto (para revisión de Producto) | 2026-11-09 | Arquitectura |

**Propósito.** Validasoft se vuelve el **dueño de la información del tercero**: la que hoy vive en las tablas del Portal de Proveedores y que el SRM usa para validar, aprobar y crear el proveedor en el ERP. Este documento cuenta lo que hay hoy (§1–§2), cómo lo modela Validasoft (§3–§6), cómo se migra (§7) y en qué orden se construye (§8). Las decisiones están en el [registro](decisiones/registro.md) (FM-1 a FM-12) y en [ADR-0013](adr/0013-ficha-maestra-del-tercero.md).

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
- **Respaldo documental** (FM-5). Cada sección declara qué tipos de documento la respaldan. La versión de la sección guarda `respaldadaPor: { documentoId, version }`, que el tercero elige o que se toma de la versión vigente.
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
| `FICHA_GENERAL` | razon_social / nombres + apellidos, tipo_persona, tipo_identificacion, numero, dv (calculado DIAN módulo 11; no editable), correo, telefono, pais (ISO), ciudad (DANE), direccion (+ estructura AyF), ciiu_principal, ciiu_secundarias, pagina_web, certificado_iso, anio_inicio | — | RUT, Cámara |
| `FICHA_TRIBUTARIA` | responsabilidades (códigos DIAN de la casilla 53 del RUT), resoluciones de gran contribuyente y autorretenedor (número, fecha), ica[] (municipio DANE, CIIU); las 13 calidades del Portal **se derivan** de los códigos | ica[] | RUT |
| `FICHA_FINANCIERA` | periodo (año), fecha_corte, activos, pasivos, patrimonio, ingresos_brutos, ingresos_netos, gastos_operacionales, gastos_no_operacionales, otros_ingresos; validación activos = pasivos + patrimonio (con tolerancia); moneda_extranjera { realiza, tipos[], paises[] } | una versión por periodo | Estados financieros del periodo |
| `FICHA_LEGAL` | personas[] { rol, tipo_representacion, tipo_identificacion, numero, nombres, apellidos, cargo, porcentaje (0–100, suma de socios ≤ 100), pep, vinculo_pep, fecha_expedicion, fecha_nacimiento, nacionalidad, residencia }; revisor_fiscal_firma? | personas[] | Cámara; RUT (representantes) |
| `FICHA_SST` | las 8 preguntas S/N | — | Certificado ARL/SG-SST (opcional por empresa) |
| `FICHA_ESPECIALIDADES` | especialidades[] (catálogo de plataforma de 3 niveles) | sí | — |

Reglas de completitud (FM-6), configurables por flujo:
- `FICHA_GENERAL` siempre.
- `FICHA_LEGAL` con al menos un representante legal y un socio si el tercero es persona jurídica.
- `FICHA_ESPECIALIDADES` con al menos una especialidad.
- La cuenta designada (CB-2) cuando el flujo pide pagos.

## 5. Cruce sección ↔ documento
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

## 6. Eventos e integración
- **Eventos de dominio:** los del documento (versión cargada, compartida, aceptada…), más `CruceDeSeccionRegistrado(numero, resultados[])`.
- **Evento público** `seccion.aceptada { terceroId, vinculoId, seccion, version, datos }`, emitido cuando una empresa acepta una versión (FM-9). El SRM lo aplica en AyF con el mapeo de hoy: General, Tributaria, el representante legal y la cuenta designada. Si AyF falla, el SRM lo reintenta y lo deja en su bandeja; la aceptación en Validasoft no se revierte, porque es una decisión de negocio, no una transacción distribuida.
- **API para el Portal, el SRM y Marco:**
  - `GET /api/v1/vinculos/{id}/ficha`: lo **aceptado** por esa empresa, sección por sección.
  - `GET /api/v1/terceros/{id}/ficha`: se amplía Tercero 360 con las secciones.

## 7. Migración y coexistencia
- **Importador (H10):** cada tabla `Ter*` del Portal se carga como la **versión 1** de su sección, con origen `Importada`. En cada empresa donde el tercero estaba `Completada` o en actualización, queda **aceptada** con su vigencia. Mientras se importa se aplican estas correcciones:
  - recalcular el DV;
  - traducir las 13 calidades tributarias a códigos DIAN (con `NoObligFacturar` normalizado);
  - convertir fechas y porcentajes de texto;
  - deduplicar personas por identificación;
  - mapear ciudades a DANE y bancos al catálogo.

  Lo que no se pueda convertir va al CSV de rechazos.
- **Coexistencia por empresa** (FM-11). Mientras una empresa no haya hecho el corte, el Portal sigue siendo su UI. Un sincronizador de un solo sentido, del Portal a Validasoft, lleva cada guardado de sección como una versión nueva, con origen `Portal`. Después del corte, el Portal lee la ficha de Validasoft o embebe su UI, y ya no escribe en `Ter*`.

## 8. Orden de construcción (propuesto)
Cada entrega es un PR con pruebas y e2e.

| Entrega | Contenido |
|---|---|
| F1 | Catálogos de plataforma (DANE, CIIU, bancos, responsabilidades DIAN, especialidades de 3 niveles) y esquemas canónicos de `FICHA_GENERAL`, `FICHA_TRIBUTARIA` y `FICHA_ESPECIALIDADES` como formularios globales; el tercero las diligencia en el portal; versiones en la bóveda |
| F2 | Las secciones como requisitos del flujo: completitud, aceptación y observación por área con motivos, revalidación y aviso; la empresa ve la ficha en Tercero 360 y en el expediente |
| F3 | `FICHA_LEGAL` (fase 1) y `FICHA_FINANCIERA` por periodo |
| F4 | Cruce sección ↔ documento con los hechos de IA, visible para el revisor |
| F5 | Evento `seccion.aceptada` y `GET /vinculos/{id}/ficha` para el SRM (H9) |
| F6 | Importador desde las tablas del Portal y sincronizador de coexistencia (H10) |
| F7 | `FICHA_SST`, cargo de contactos por empresa y personas relacionadas con bóveda propia (fase 2) |

## Decisiones abiertas
1. ¿Las 13 calidades tributarias se exponen al SRM como hoy (S/N) además de los códigos DIAN? Propuesta: sí, derivadas, para no romper AyF.
2. ¿Cuántos periodos financieros se exigen? Propuesta: el último cierre, y el anterior si la empresa lo configura.
3. ¿La SST se respalda con un documento? Propuesta: opcional por empresa.
