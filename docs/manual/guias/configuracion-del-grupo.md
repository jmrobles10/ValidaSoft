---
titulo: "Configuración del grupo"
seccion: "Configuración y glosario"
resumen: "Cada ajuste de alcance grupo en la app de la empresa: dónde está, qué cambia, su valor por defecto y sus variantes. Comprobado contra el código."
---

# Configuración del grupo

Cada ajuste de alcance grupo en la app de la empresa: dónde está, qué cambia, su valor por defecto y sus variantes. Comprobado contra el código.

El **grupo** (el tenant) es el cliente de Validasoft: una o varias empresas (razones sociales) con una administración común. Estas secciones de **Configuración** valen para todas las empresas del grupo. Las de alcance empresa están en [Configuración de la empresa](configuracion-de-la-empresa.md) y las de la consola de Plataforma en [Configuración de Plataforma](configuracion-de-plataforma.md).

> **Nota:** las rutas de la app son `/e/{empresa}/{sección}` dentro del hash de la app web (por ejemplo `/app/#/e/{empresa}/usuarios`); no existe el segmento `/configuracion/`. Una sección no aparece en la barra lateral si la persona no tiene su permiso. Los permisos de administración del grupo (`empresas:administrar`, `usuarios:administrar`, `api-keys:administrar`, `sistemas:administrar`, `sandbox:administrar`) solo los da un rol asignado a **todo el grupo**: un administrador de una sola empresa no los tiene.

**Confirmar con passkey (reautenticación).** Estas acciones piden confirmar con la passkey en los últimos 5 minutos si la persona entró de forma directa: cambiar roles, desactivar a una persona, revocar una passkey, crear, rotar o revocar una API key, rotar la clave de un sistema, desactivarlo o reactivarlo, guardar el SSO y quitar un dominio del SSO. Las sesiones delegadas (iframe de un sistema integrado) no la piden.

## Empresas del grupo

Ruta `/e/{empresa}/empresas` · permiso `empresas:administrar`. Los textos legales se leen con `configuracion:leer` y se publican con `configuracion:escribir`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| País | Decide los tipos de identificación disponibles. | CO | CO, CL, AR, MX, PE, EC, US. |
| Tipo de identificación | Define el patrón del número y si lleva dígito de verificación (DV). | NIT | CO: NIT, CC, CE, TI, PPT, PA. CL: RUT. AR: CUIT, DNI. MX: RFC, CURP. PE: RUC, DNI. EC: RUC, CI. US: EIN. Calculan DV: NIT, RUT y CUIT. |
| Número de identificación | Identifica a la empresa; se normaliza sin puntos, espacios ni guiones. | Sin valor: debe configurarse | Patrón por tipo (NIT: 6 a 15 dígitos). Con DV se acepta con o sin él y se compara. Repetido en el grupo: 409 `empresa-duplicada`. |
| Razón social | Nombre visible de la empresa en el grupo. | Sin valor: debe configurarse | Obligatoria. |
| Prefijo de folio | Encabeza todos los folios de expedientes y certificados de la empresa (`{PREFIJO}-E-{año}-{n}`). | Sin valor: debe configurarse | De 2 a 10 letras mayúsculas o números. No se puede cambiar después. Único en el grupo (409 `prefijo-folio-duplicado`). |
| Desactivar empresa | Las peticiones con `X-Empresa` de esa empresa responden 404; se conservan vínculos, expedientes, certificados e historial. | Activa | No hay acción de reactivar. Una empresa inactiva no admite accesos de soporte. |
| Límite de empresas del plan | Con el bloqueo suave activo y el tope alcanzado, el alta responde 409 `limite-del-plan-alcanzado`. | Sin límite si no hay plan | Cuentan solo las empresas activas. |
| Textos legales: autorización de tratamiento de datos y términos y condiciones | Es lo que acepta el tercero al vincularse. Cada publicación es una versión nueva, vigente de inmediato, y los terceros vinculados deben aceptarla de nuevo. | Sin valor: sin publicar, los terceros no pueden aceptar invitaciones. | De 1 a 100 000 caracteres. La pantalla impide publicar un texto igual al vigente. |

Fuente: `apps/web/src/features/configuracion/empresas/modelo.ts`, `src/Validasoft.Aplicacion/Configuracion/ConfiguracionEndpoints.cs`, `src/Validasoft.Dominio/Plataforma/Plataforma.cs`.

## Usuarios y roles

Ruta `/e/{empresa}/usuarios` · permiso `usuarios:administrar`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Nombre y correo | El correo es la cuenta de ingreso. | Sin valor: debe configurarse | Correo en minúsculas, de 3 a 254 caracteres, único en el grupo (409 `usuario-duplicado`). |
| Rol (una fila por rol) | Define los permisos (tabla siguiente). | Una persona nueva no tiene roles y no puede hacer nada; una fila nueva empieza en Revisor. | Administrador, Revisor, Oficial de cumplimiento, Auditor. |
| Empresa (alcance del rol) | Dónde vale el rol. | Todo el grupo o la primera empresa, según la fila. | «Todo el grupo» vale en todas las empresas y es el único alcance que da los permisos de administración del grupo. Un rol de todo el grupo elimina el mismo rol por empresa. |
| Passkey exigida (derivada del rol) | Administrador, Oficial de cumplimiento y Auditor deben registrar passkey; sin ella, el código del correo solo sirve para registrarla. | El Revisor no la exige, salvo «Exigir passkey a todas las personas» en Ingreso. | Solo se muestra si el grupo admite ingreso directo. |
| Enviar una invitación por correo | Envía la dirección de la app a la persona. | Marcado si el grupo admite ingreso directo. | Con ingreso directo apagado: 422 `ingreso-directo-apagado` al crear (409 al reenviar). |
| Passkeys y sesiones de la persona | Revocar una passkey la invalida, cierra todas las sesiones de la persona y le avisa por correo. | — | Motivo opcional; revocar pide reautenticación. |
| Desactivar persona | No vuelve a entrar y se cierran sus sesiones directas; se conserva el historial. | Activa | No hay acción de reactivar. |
| Límite de usuarios del plan | Con bloqueo suave y el tope alcanzado, el alta manual responde 409 `limite-del-plan-alcanzado`. | Sin límite si no hay plan | Cuentan los usuarios activos. El alta por SSO o por identidad delegada no pasa por este límite. |

| Rol | Permisos | Passkey |
|---|---|---|
| Administrador | Los 31 permisos del grupo (todos los de la tabla de API keys). | Sí |
| Revisor | Lecturas (`configuracion:leer`, `terceros:leer`, `vinculos:leer`, `flujos:leer`, `expedientes:leer`, `documentos:leer`, `documentos:descargar`, `certificados:leer`, `consultas:leer`) más `vinculos:escribir` y `expedientes:revisar`. | No |
| Oficial de cumplimiento | Lecturas más `vinculos:escribir`, `vinculos:bloquear`, `cuentas:aprobar`, `expedientes:revisar`, `expedientes:exportar`, `verificaciones:escribir`, `cumplimiento:decidir`, `auditoria:leer` y `supresiones:resolver`. | Sí |
| Auditor | Lecturas más `auditoria:leer`, `auditoria:exportar` y `expedientes:exportar`. | Sí |

Fuente: `src/Validasoft.Dominio/Identidad/Usuario.cs`, `src/Validasoft.Aplicacion/Comun/Permisos.cs`, `src/Validasoft.Aplicacion/Identidad/IdentidadEndpoints.cs`.

## Ingreso

Ruta `/e/{empresa}/ingreso` · permiso `usuarios:administrar`. Define cómo entran las personas del grupo ([Seguridad](arquitectura-seguridad.md)).

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Desde el sistema integrado (modo Delegado) | El sistema integrado (SRM, Marco, Portal) autentica a la persona con una identidad firmada; es lo que usan las pantallas embebidas. | Marcado | Apagado, las sesiones delegadas responden 403 `modo-de-ingreso-no-admitido`. Esas sesiones duran 30 minutos fijos. |
| Directo a Validasoft (modo Directo) | La persona entra con correo y código de 6 dígitos, y con passkey si aplica. | Desmarcado | Encenderlo completa el grupo de los usuarios antiguos; apagarlo cierra las sesiones directas abiertas. Debe haber al menos un modo (422 `modos-de-ingreso-invalidos`). Activar el SSO exige este modo. |
| Duración máxima de la sesión | Pasado ese tiempo hay que ingresar de nuevo; no se extiende al renovar. Solo vale para ingreso directo. | 8 horas | La pantalla ofrece 1, 2, 4, 8, 12 y 24 horas; la API acepta de 1 a 24 (422 `sesion-invalida`). |
| Cerrar por inactividad | Cierra la sesión si no hay actividad en ese tiempo. | 30 minutos | La pantalla ofrece 5, 10, 15, 30, 60, 120, 240 y 480; la API acepta de 5 a 480 y no más que la sesión (422 `inactividad-invalida`). |
| Exigir passkey a todas las personas del grupo | Todas deben registrar passkey en su próximo ingreso. | Apagado | Apagado: la exigen solo Administrador, Oficial y Auditor. Un ingreso por SSO con varios factores declarados no pide passkey. |

Parámetros fijos, no configurables: el código del correo vence a los 5 minutos y admite 5 intentos; la ceremonia dura 10 minutos; un correo sin cuenta recibe un desafío señuelo para no revelar quién existe.

Fuente: `src/Validasoft.Dominio/Plataforma/Plataforma.cs`, `src/Validasoft.Dominio/Identidad/Ingreso.cs`, `src/Validasoft.Aplicacion/Identidad/Ingreso/GestionEndpoints.cs`.

## Inicio de sesión único (SSO)

Ruta `/e/{empresa}/sso` · permiso `usuarios:administrar`; guardar pide reautenticación.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Activar el inicio de sesión único | Las personas de los dominios verificados ven «Entrar con {nombre}». | Apagado | Pide un dominio verificado (422 `sso-sin-dominios`) y que el grupo admita ingreso directo (409 `ingreso-directo-apagado`). |
| Proveedor | Con qué proveedor se entra. | `oidc` | `oidc` (Entra ID, Google Workspace u otro OpenID Connect) o `workos` (deshabilitado si la instalación no lo tiene: 409 `workos-no-configurado`). |
| Nombre del botón | Texto «Entrar con {nombre}». | Sin valor: debe configurarse | De 1 a 60 caracteres. |
| Emisor (issuer), Client ID y secreto del cliente (OIDC) | Dónde publica el proveedor su configuración y cómo se identifica Validasoft. El secreto se guarda cifrado y nunca vuelve a mostrarse. | Sin valor; secreto vacío (cliente público con PKCE) | Emisor `https` (http solo a localhost y en desarrollo) de hasta 500 caracteres; Client ID hasta 300; secreto de 8 a 2000 caracteres. |
| Organización o Conexión (WorkOS) | Identifican a la empresa en WorkOS. | Vacío | Al menos una de las dos (422 `conexion-workos-requerida`); hasta 100 caracteres cada una. |
| URL de retorno | Es el URI de redirección que se registra en el proveedor. | La de la instalación | Solo lectura. |
| Dominios de correo | Solo valen los correos de dominios verificados; un dominio verificado pertenece a un solo grupo. | Ninguno | Hasta 20. Se verifica publicando `validasoft-verificacion=…` en `https://{dominio}/.well-known/validasoft-verificacion.txt`. Rechaza dominios públicos (gmail, hotmail, outlook, yahoo, icloud, proton…: 409 `dominio-publico`). No se puede quitar el último verificado con el SSO activo. |
| Convivencia con el código del correo | Define si el SSO acompaña o reemplaza al código. | `Complementario` | `Complementario`: SSO o código. `Exclusivo`: las personas de los dominios verificados solo entran por SSO; el código y la passkey sola responden 403 `sso-obligatorio`. No afecta al ingreso delegado. |
| Crear a las personas en su primer ingreso (JIT) | Crea la cuenta al entrar por primera vez. | Apagado | Apagado y sin cuenta: 403 `sso-sin-cuenta`. Encendido exige un rol por omisión o una regla (422 `rol-por-omision-requerido`). |
| Roles por omisión, reglas por grupo del proveedor y reclamo de grupos | Qué roles recibe quien llega, según los grupos que declara el proveedor (se unen los de las reglas que coinciden). | Sin roles ni reglas; reclamo `groups` | Hasta 20 roles por omisión y 100 reglas. «El proveedor manda sobre los roles» reemplaza en cada ingreso los roles de la persona por los de sus grupos (apagado por defecto). |
| Alcances OIDC | Solo por API. | `openid`, `email`, `profile` | Debe incluir `openid`; hasta 20. |
| Probar la conexión | Comprueba el descubrimiento y el emisor con lo guardado; no inicia sesión. | — | Solo si hay algo guardado y sin cambios pendientes. |

Fuente: `src/Validasoft.Dominio/Identidad/Sso.cs`, `src/Validasoft.Aplicacion/Identidad/Sso/SsoEndpoints.cs`, `src/Validasoft.Aplicacion/Identidad/Ingreso/ServicioDeIngreso.Sso.cs`.

## Tipos de vínculo

Ruta `/e/{empresa}/tipos-vinculo` · se ve con `configuracion:leer` y se escribe con `configuracion:escribir`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Código | Identifica el tipo en vínculos y flujos. | Sin valor: debe configurarse | Empieza con letra; `A-Z`, `0-9` y `_`; de 2 a 40 caracteres; inmutable (409 `tipo-vinculo-duplicado`). |
| Nombre | Nombre visible. | Sin valor: debe configurarse | Obligatorio. |
| Exige un código por correo en el enlace | Además del enlace, el tercero escribe un código que le llega al correo (invitaciones, observaciones y renovaciones). | No (el PROVEEDOR sembrado: Sí) | Sí o No. |
| Exige cuenta bancaria designada | Solo se guarda y se devuelve: hoy ninguna regla de cierre lo consulta. | No (el PROVEEDOR sembrado: Sí) | Sí o No. |
| Atributos permitidos (clave, tipo, obligatorio, opciones) | Datos que la empresa registra en cada vínculo del tipo; los flujos los usan en «Se pide a» como `vinculo.atributos.{clave}`. | Ninguno | Tipo: Texto, Número, Fecha, Sí/No, Una opción o Varias opciones; las dos últimas exigen opciones; la clave no se repite. |
| Estado | Un tipo inactivo no se usa en flujos ni vínculos nuevos (422 `tipo-vinculo-invalido`); lo ya creado no cambia. | Activo | Activar o desactivar. |
| Tipos sembrados al crear el grupo | — | PROVEEDOR, CLIENTE, EMPLEADO, USUARIO | — |

Fuente: `src/Validasoft.Dominio/Configuracion/TipoVinculo.cs`, `src/Validasoft.Dominio/Configuracion/ValidadorDeAtributos.cs`.

## Tipos de documento

Ruta `/e/{empresa}/tipos-documento` · se ve con `configuracion:leer` y se crea o edita con `configuracion:escribir`. Un tipo de documento pertenece al grupo, no a una empresa.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Documento de la plataforma (canónico) | Lo hace compartible entre empresas y fija un techo de formatos y límites: el tipo puede ser más estricto, no más laxo (422 `formato-mas-laxo`, `limite-mas-laxo`). Solo al crear. | Ninguno: es un documento propio | La pantalla lista los 16 canónicos de archivo (RUT, CAMARA_COMERCIO, CEDULA, PASAPORTE, CERTIFICADO_ANTECEDENTES, ESTADOS_FINANCIEROS, DECLARACION_RENTA, CERTIFICACION_BANCARIA, SG_SST, HOJA_DE_VIDA, TITULO_ACADEMICO, POLIZA, AFILIACION_SEGURIDAD_SOCIAL, PAZ_Y_SALVO, SOPORTE_FICHA, FORMATO_VINCULACION). |
| Código y nombre | Identifican el tipo. El código es inmutable. | Sin valor: deben configurarse | Código: empieza con letra, `A-Z`, `0-9` y `_`, hasta 60 caracteres en la API (la pantalla limita a 50); 409 `tipo-documento-duplicado`. |
| Ámbito | `Compartido`: queda en la bóveda del tercero y se reutiliza con otras empresas. `Solo de este vínculo`: privado de la relación. | `PorVinculo` (con canónico, el del canónico) | Un tipo propio sin canónico es siempre PorVinculo (422 `ambito-no-ampliable`); con campos de formulario, también. |
| Indicaciones para el proveedor | Texto de ayuda que lee el tercero al entregar. | Vacío | Opcional. Cada empresa puede sobrescribirlo ([Ayuda de documentos](configuracion-de-la-empresa.md#ayuda-de-documentos)). |
| Activo | Un tipo inactivo no se puede pedir en flujos nuevos. | Activo | Solo al editar. |
| Se entrega como (modo) | Qué entrega el tercero. | `Archivo` | `Archivo`; `Formulario` (solo datos, sin archivo); `ArchivoYFormulario`. Con campos: ámbito PorVinculo y al menos 1 campo (422 `campos-requeridos`). |
| Campos del formulario | Lo que llena el tercero, en orden. | Ninguno | Hasta 50. Clave en `snake_case`; tipo Texto, Número, Fecha, Lista o Sí/No (la lista exige opciones sin repetir). `fecha_expedicion` y `fecha_vencimiento` están reservadas y deben ser fechas. |
| Formatos | Qué puede subir el tercero. | PDF, JPG, PNG o WebP | Con canónico, solo un subconjunto del suyo (RUT, CAMARA_COMERCIO, ESTADOS_FINANCIEROS y DECLARACION_RENTA: solo PDF). |
| Peso máximo (MB) y páginas máximas | Tope por archivo y de páginas de un PDF. | Los generales: 20 MB y 50 páginas | Peso de 1 a 1024; páginas de 1 a 1000; no pueden superar el límite general ni el del canónico (422 `limite-sobre-el-global`). |
| Vale (regla de frescura) | Cuánto sirve el documento; al cambiarla se recalculan las vigencias de los documentos existentes. | `Ninguna` (con canónico, la sugerida por él) | `Ninguna` (no vence, salvo que declare `fecha_vencimiento`); `MaxDiasDesdeExpedicion` (n días desde `fecha_expedicion` o, si falta, desde la carga); `MaxDiasDesdeAprobacion` (n días desde la carga); `PeriodoMinimo` (un texto; aún no calcula fecha). La vigencia es el menor entre el vencimiento declarado y la regla; una ratificación del tercero reinicia el conteo. |
| Avisar antes de vencer (días) | Días antes del vencimiento en que se escribe al tercero; el aviso sale a las 13:00 UTC (08:00 en Colombia). | 30, 15 y 5 (POLIZA y ESTADOS_FINANCIEROS: 60, 30 y 7) | Lista de enteros mayores que 0, ordenados de mayor a menor. |
| Días de gracia | Siguen valiendo tras vencer, hasta que queda vencido. | 15 | Mayor o igual que 0. |
| Plantilla para descargar | Formato que el tercero descarga, llena, firma y sube. | Sin plantilla | PDF, DOCX o XLSX hasta 20 MB; cada archivo distinto crea una versión nueva; retirarla conserva el historial. No aplica al modo Formulario. |
| Días de revalidación | Plazo de revalidación del tipo; se ajusta por requisito en los flujos. | 15 | Entero mayor que 0; solo por API. |
| Tipos sembrados al crear el grupo | — | RUT, CAMARA_COMERCIO, CEDULA, ESTADOS_FINANCIEROS, CERTIFICACION_BANCARIA, POLIZA | — |

> **Ojo:** con la regla `Ninguna`, un documento que declara `fecha_vencimiento` sí vence y sí genera avisos, aunque la pantalla diga «no pierde vigencia». No existen ajustes del tipo por empresa (los «overrides» de empresa que mencionan docs/02 y docs/03 no están construidos): lo único por empresa es la ayuda del documento y los ajustes del requisito dentro del flujo.

Fuente: `src/Validasoft.Dominio/Documentos/TipoDocumentoTenant.cs`, `src/Validasoft.Dominio/Documentos/CatalogoCanonico.cs`, `src/Validasoft.Dominio/Documentos/Vigencia.cs`, `src/Validasoft.Dominio/Documentos/Formularios.cs`.

## Motivos

Ruta `/e/{empresa}/motivos` · se ve con `configuracion:leer` y se escribe con `configuracion:escribir`. El catálogo es del grupo.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Código | Identifica el motivo en veredictos y rechazos. | Sin valor: debe configurarse | Minúsculas, números y guiones, hasta 60 caracteres; inmutable; no puede repetir uno de la plataforma (409 `motivo-duplicado`). |
| Nombre | Lo que ven los revisores. | Sin valor: debe configurarse | Obligatorio. |
| Categoría | Sobre qué recae el motivo. | Documento | Documento, Formulario, Contacto, Otro. |
| Se usa al | En qué decisión se ofrece. | Observar | Observar, Rechazar u Observar y rechazar (422 `motivo-invalido` si no cubre la decisión). |
| Texto al tercero | Lo que el tercero lee en el correo y en su vista; nunca nombra al revisor. | Sin valor: debe configurarse | Obligatorio; la pantalla limita a 500 caracteres. |
| Disponible para los revisores | Si está inactivo, ya no se resuelve en veredictos nuevos. | Activo | Activo o inactivo. |
| Motivos de la plataforma | Ocho fijos que no se editan (409 `motivo-base`): ilegible, incompleto, vencido, no corresponde al tercero, datos no coinciden, formato no vigente, firma faltante, frescura incumplida. | Siempre activos | Todos sirven para observar y para rechazar. |

Fuente: `src/Validasoft.Dominio/Configuracion/Motivo.cs`, `src/Validasoft.Aplicacion/Configuracion/MotivosEndpoints.cs`.

## Webhooks

Ruta `/e/{empresa}/webhooks` · permiso `webhooks:administrar`. El detalle del mensaje y de la firma está en la [Guía de integración](guia-de-integracion.md).

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Dirección (URL) | Destino del POST firmado de cada evento. | Sin valor: debe configurarse | Absoluta, hasta 2000 caracteres, `https` (http solo a localhost con `Validasoft:Webhooks:PermitirHttpLocal`); sin IP privadas ni locales (`PermitirDestinosPrivados`). 422 `url-invalida` o `url-no-permitida`. |
| Descripción | Para reconocer la suscripción. | Vacío | Hasta 200 caracteres. |
| Qué eventos recibe | Solo recibe los tipos marcados. | `*` (todos, también los futuros) | `*` o hasta 100 tipos del catálogo de 42 (abajo). Vacío: 422 `tipos-requeridos`; desconocido: 422 `tipos-invalidos`. |
| De qué empresas | Un evento sin empresa (de grupo) solo llega con `*`. | `*` (las del alcance de quien lo crea) | `*` o ids de empresas del grupo (422 `empresas-fuera-de-alcance`). |
| Secreto de firma y rotación | Cada entrega lleva `Validasoft-Signature: t={ts},v1={HMAC-SHA256}`; el secreto (`whsec_…`) se muestra una sola vez. | Se genera al crear | Al rotar, el anterior sigue firmando 24 horas por omisión (`HorasSolapamientoRotacion`, de 0 a 168); la cabecera lleva dos `v1` durante ese tiempo. |
| Tiempo de espera por intento | Éxito = respuesta 2xx dentro del plazo. | 5 s (`TimeoutSegundos`, de 1 a 30) | No editable en la pantalla. |
| Reintentos | Espera creciente tras cada fallo, con los eventos en orden de cursor. | 1 min, 5 min, 30 min, 2 h y 6 h (seis intentos) | `Validasoft:Webhooks:Reintentos`. Estados de entrega: Pendiente, Entregada, Reintentando, Fallida, Cancelada. |
| Deshabilitación automática | Al llegar al umbral de intentos fallidos seguidos la suscripción se deshabilita (causa `fallos-consecutivos`), se audita y se avisa a los administradores por campana y correo; sus reintentos pendientes quedan Cancelada. | 20 (`FallosParaDeshabilitar`) | Un éxito reinicia la cuenta. Activar de nuevo reinicia los fallos. |
| Reenviar entrega | Un intento manual ahora, aunque esté entregada, fallida o cancelada o la suscripción esté desactivada. | — | Si falla, no programa reintentos. |

**Catálogo de eventos suscribibles (42).** `vinculo.` (16): creado, invitacion_enviada, invitacion_aceptada, activado, bloqueado, desbloqueado, por_vencer, vencido, desvinculado, reintegrado, anulado, referencia_agregada, contactos_actualizados, cuenta_designada_propuesta, cuenta_designada, cuenta_designada_rechazada. `expediente.` (14): abierto, en_revision, observado, reenviado, aprobado, rechazado, caducado, reabierto, desaprobado, sla_por_vencer, sla_incumplido, verificacion_registrada, coincidencia_resuelta, verificacion_expirada. `documento.` (3): compartido, por_vencer, vencido. `certificado.` (7): emitido, reemplazado, con_pendientes, suspendido, restablecido, revocado, vencido. `seccion.aceptada` e `invitaciones.lote_terminado`. El catálogo vigente de una instalación sale de `GET /api/v1/eventos/tipos`.

> **Ojo:** el formulario de alta de la pantalla no envía `sistemaId`, y la API lo exige cuando la credencial no es de un sistema integrado (422 `sistema-requerido`); con una sesión de ingreso directo esa alta puede fallar. Se diagnosticó leyendo el código, no ejecutándolo.

Fuente: `src/Validasoft.Dominio/Integraciones/SuscripcionWebhook.cs`, `src/Validasoft.Aplicacion/Notificaciones/Webhooks.cs`, `src/Validasoft.Aplicacion/Notificaciones/EventosPublicos.cs`.

## Sistemas integrados

Ruta `/e/{empresa}/sistemas` · permiso `sistemas:administrar`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Código (iss) | Es el emisor de las aserciones del sistema y su código en las referencias externas. | Sin valor: debe configurarse | Empieza con letra; `A-Z`, `0-9` y `_`; hasta 20; inmutable (409 `sistema-duplicado`). |
| Nombre | Nombre visible. | Sin valor: debe configurarse | Obligatorio. |
| Clave pública (PEM) | Validasoft verifica con ella las identidades firmadas; solo se guarda la pública. | Sin valor: debe configurarse | RSA de 2048 bits o más, o EC P-256 (422 `clave-publica-invalida`). |
| Sitios que pueden incrustar Validasoft (orígenes) | Alimenta `frame-ancestors` y la lista de `postMessage` para las pantallas embebidas; se aplica en menos de un minuto. | Ninguno: nadie puede incrustar | Hasta 20, `https://host[:puerto]`, sin ruta, consulta, comodines ni usuario (422 `origen-invalido`, `origenes-excesivos`). |
| Rotar clave | Desde ya solo valen las aserciones firmadas con la clave nueva. | — | Solo si el sistema está activo; pide reautenticación. |
| Desactivar sistema | Revoca todas sus API keys (también las de prueba), corta sus sesiones y aserciones y quita sus orígenes; reactivarlo no restaura las llaves. | Activo | Desde Plataforma («Sistemas» de cada grupo) o por API; la acción pide reautenticación. |

Fuente: `src/Validasoft.Dominio/Identidad/SistemaIntegrado.cs`, `src/Validasoft.Aplicacion/Identidad/IdentidadEndpoints.cs`.

## API keys

Ruta `/e/{empresa}/api-keys` · permiso `api-keys:administrar`; crear, rotar y revocar piden reautenticación. Una credencial del sandbox no administra llaves (422 `no-disponible-en-sandbox`).

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Nombre | Para reconocer la llave. | Sin valor: debe configurarse | Obligatorio. |
| Llave de sandbox | Opera en el sandbox aislado del grupo, que se crea al emitir la primera. | Desactivada | Formato: `vsk_live_…` (producción), `vsk_dev_…` (desarrollo), `vsk_test_…` (sandbox). |
| Permisos (scopes) | Qué puede hacer la llave. | Ninguno: hay que marcar al menos uno (422 `scopes-invalidos`) | Los 31 del grupo, en 7 grupos: administración, configuración y flujos, terceros y vínculos, revisión, integración, cumplimiento y auditoría. `plataforma:tenants` no entra. |
| Empresas de alcance | Limita en qué empresas actúa la llave. | Todo el grupo (`*`, incluye las futuras) | `*` o ids de empresas (422 `empresas-invalidas`); no se puede dar más empresas que las propias (403 `alcance-insuficiente`). |
| Expira | Vale hasta el final de ese día (hora de Bogotá). | Vacío: no expira | Debe ser futura (422 `expiracion-invalida`). |
| Sistema integrado | La llave es de un sistema: fija sus referencias externas y valida sus aserciones; al desactivar el sistema se revoca. | Ninguno (llave de administración) | Solo sistemas activos (422 `sistema-invalido`). |
| Rotar | Emite una llave nueva con los mismos permisos, alcance, sistema y expiración; la anterior sigue valiendo el solapamiento elegido y la nueva se muestra una sola vez. | Solapamiento de 24 horas | La pantalla ofrece ahora, 1 hora, 24 horas y 7 días; la API acepta hasta 30 días y nunca más allá de la expiración. 409 `api-key-ya-rotada` o `api-key-vencida`. |
| Revocar | Deja de autenticar; se aplica en segundos (con la caché de verificación de 5 minutos como tope). | — | No se deshace. |

Fuente: `src/Validasoft.Dominio/Identidad/ApiKey.cs`, `src/Validasoft.Aplicacion/Configuracion/ConfiguracionEndpoints.cs`, `src/Validasoft.Infraestructura/Seguridad/Seguridad.cs`.

## Accesos de soporte

Ruta `/e/{empresa}/accesos-soporte` · permiso `empresas:administrar`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Empresa | Empresa a la que el soporte de Plataforma podrá entrar. | La empresa activa | Solo empresas activas (422 `empresa-inactiva`). |
| Duración | El acceso vence solo. | 4 horas | La pantalla ofrece 1, 4, 8, 24, 48 y 72; la API acepta de 1 a 72 (422 `duracion-invalida`). |
| Para qué lo necesitan | Queda en la auditoría. | Sin valor: debe configurarse | De 10 a 500 caracteres. |
| Revocar | El soporte deja de entrar de inmediato. | — | 409 `acceso-no-vigente` si ya expiró o se revocó. |

Mientras está vigente, el soporte solo lee (`configuracion:leer`, `flujos:leer`, `vinculos:leer`, `terceros:leer`, `expedientes:leer`, `certificados:leer`, `auditoria:leer`) y nunca ve documentos. Entra con la cabecera `X-Acceso-Soporte`.

Fuente: `src/Validasoft.Dominio/Identidad/AccesoSoporte.cs`, `src/Validasoft.Aplicacion/Configuracion/AccesosSoporteEndpoints.cs`.

## Plan del grupo

Ruta `/e/{empresa}/plan-grupo` · permiso `configuracion:leer`. Es de solo lectura para el grupo: el plan lo fija Plataforma ([Configuración de Plataforma](configuracion-de-plataforma.md#plan-y-limites-del-grupo)).

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Plan (código) | Nombre del plan; no habilita funciones. | `Inicial`; sin plan guardado se muestra «Sin plan asignado»: no hay límites y solo se mide. | De 2 a 40 caracteres. |
| Al llegar a un límite (bloqueo suave) | Con bloqueo, al 100 % no se permiten más altas de esa métrica y lo existente sigue funcionando; las lecturas nunca se bloquean. Sin bloqueo, solo se avisa. | Apagado | Bloquea el alta manual de usuarios, las empresas, los vínculos, los documentos nuevos (almacenamiento) y, en la API, las escrituras (429 `limite-del-plan-alcanzado`). |
| Usuarios, empresas y vínculos activos | Máximos por grupo. «Vínculos activos» cuenta los que no están anulados ni desvinculados. | Sin límite | Entero o vacío (sin límite); 0 es un límite real. |
| Almacenamiento (MB) | Suma los documentos de la bóveda compartidos con el grupo (cada objeto cuenta una vez; sin suprimidos); la medición puede tener hasta 5 minutos. | Sin límite | Entero o vacío. |
| Llamadas a la API por mes | Cuenta las peticiones del mes (calendario en la zona del grupo) con API key de producción; no cuentan sandbox ni las sesiones de las pantallas. | Sin límite | Entero o vacío. |
| Niveles de uso y avisos | Normal por debajo del 80 %; por agotar del 80 % al 100 %; agotado desde el 100 %. Cada umbral se avisa una vez por periodo a los administradores (campana y correo). | Evaluación cada 15 minutos | `Validasoft:Plan:Intervalo` (`00:00:00` la apaga). |

Fuente: `src/Validasoft.Dominio/Plataforma/PlanDelGrupo.cs`, `src/Validasoft.Aplicacion/Plataforma/PlanDelGrupoEndpoints.cs`.
