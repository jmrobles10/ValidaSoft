# Inventario de recorridos del manual

Lista **completa** de los recorridos que hacen falta para documentar todo Validasoft (todas las pantallas de `apps/web` y `apps/public`, la consola de
Plataforma, cada configuración con sus variantes, la integración por API, webhooks, sandbox y SSO, el cumplimiento, la operación masiva, los informes, la
verificación pública, lo embebido y lo móvil). Cada fila es **un archivo** `e2e/manual/<dominio>/<flujo>.manual.ts`. La plantilla y las reglas están en [`README.md`](README.md).

- **Lotes**: están repartidos para trabajar en paralelo sin pisarse: cada lote tiene archivos propios; si un lote necesita una siembra compartida, la agrega **al final** de
  `nucleo/semillas.ts` sin cambiar lo existente. Orden sugerido dentro de un lote: de arriba abajo (lo básico antes que las variantes).
- **Siembra**: lo que se crea por API (con `crearEscenario()` y los helpers de `e2e/soporte/`) para que el recorrido solo haga por la interfaz lo que documenta. «Spec» = la spec de
  `e2e/pruebas/` que ya hace algo parecido y de la que se copia la siembra y los locators.
- **Pasos esperados**: guía en una línea de lo que debe mostrar; el recorrido puede tener más pasos si hay variantes. Ningún recorrido debe pasar de ~18 pasos (si pasa, se parte).
- Estado: ✅ hecho · ☐ pendiente. Los pasos de los dos recorridos hechos son el modelo.
- **Variantes obligatorias** en todo recorrido: tipo de persona (natural/jurídica), configuración que cambia el resultado, rol (administrador, revisor, oficial, auditor) y plan o nivel de IA
  cuando aplique; van en `variantes` y `configuracion` del paso donde se nota.
- Los recorridos del **Sistema integrador** (API) muestran lo que esa persona ve en Validasoft (Configuración › Sistemas, API keys, Webhooks, Sandbox…) y la llamada real con `mostrarCodigo`.

Total: **187 recorridos** (2 hechos) en cuatro lotes: 1 → 49, 2 → 46, 3 → 44, 4 → 48.

---

## Lote 1 — El tercero, lo público y lo móvil (49)

Usa `soporte/proveedor.ts`, `soporte/ficha.ts`, `soporte/correo.ts`. Los del tercero llevan celular por omisión. Para lo que decide la empresa en medio del recorrido, usa `abrirEmbebido` + `soporte/empresa.ts` como siembra (no se documenta).

### tercero (28)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `tercero/aceptar-invitacion-persona-juridica` ✅ | Tercero | Grupo con flujo de Proveedor (RUT, Cámara, 6 secciones); invitación | Correo, código, textos legales, requisitos, «Mi información» completa, subir documentos, en revisión |
| `tercero/aceptar-invitacion-persona-natural` ☑ | Tercero | `publicarFlujoDeFicha` CLIENTE de Sur con RUT, cédula (`soloPara('Natural')`) y 4 secciones; `nuevaPersonaNatural` (spec `asistente-tipo-persona`) | Correo sin código (Cliente), textos, requisitos con cédula, «no aplica» en Legal y Financiera, general con expedición y PEP, 4 secciones, documentos, en revisión |
| `tercero/entrar-sin-invitacion` ☑ | Tercero | Tercero con una invitación pendiente (spec `ingreso`) | `/entrar` con el correo, «Revisa tu correo», enlace, código, Mis empresas; correo desconocido (misma respuesta); límite de intentos |
| `tercero/mis-empresas-varias-invitaciones` ☑ | Tercero | Invitado por Norte y Sur, y proveedor + cliente de una misma empresa (spec `multiempresa`) | Lista de tarjetas por empresa y tipo, estados, abrir una, el enlace de cada correo abre su invitación |
| `tercero/invitacion-vencida-o-usada` ☑ | Tercero | Invitación cuyo enlace ya se usó o venció (`enlace-usado`, `enlace-vencido`) | Pantalla de enlace no válido, cómo recibir otro, qué pasa con un enlace de otra empresa |
| `tercero/subir-documento-con-formulario` ☑ | Tercero | Flujo de Sur con CERT_SST (fecha de expedición, vence a 30 días; `SST` en `semilla.ts`) | Formulario con archivo y fecha, formato no admitido, archivo repetido, versión 1, vigencia y fecha de vencimiento |
| `tercero/responder-observaciones` ☑ | Tercero | Expediente observado por la empresa con `observar` (spec `decisiones`) | Frase «te pide corregir», motivos por documento, subir versión corregida, «Enviar respuesta» con explicación, vuelve a revisión |
| `tercero/subir-version-nueva` ☑ | Tercero | Vínculo aprobado (spec `actualizaciones`) | «Subir otra versión», versión 2, estado «En revisión» del documento y del vínculo, qué ve la empresa |
| `tercero/expediente-aprobado` ☑ | Tercero | Expediente aprobado por todas las áreas | Frase de aprobado, vínculo activo, documentos vigentes, certificado y su verificación (spec `certificado`) |
| `tercero/rechazo-y-volver-a-postularme` ☐ | Tercero | Rechazo con motivos y flujo con reintento (specs `volver-a-postularme`, `reintento`) | Motivos sin la justificación interna, espera en días, «Volver a postularme», documentos de la bóveda, nueva revisión |
| `tercero/boveda-mis-documentos` ☐ | Tercero | Tercero con varios documentos y dos empresas | Mi bóveda: tarjetas por documento, versiones, con quién se comparte, vigencia, descargar |
| `tercero/boveda-subir-un-documento` ☐ | Tercero | Tercero sin invitación abierta | `/boveda/subir`: elegir el tipo, archivo, datos del formulario, «Ya la tiene», confirmar |
| `tercero/boveda-documento-compartido` ☐ | Tercero | Dos empresas que piden el mismo RUT (spec `multiempresa-documento-global`) | Una sola versión en la bóveda, cada empresa la revisa, versión nueva y cada empresa revalida |
| `tercero/retirar-una-version` ☐ | Tercero | Versión subida (spec `retiro-version`) | Retirar, diálogo con las consecuencias, aviso a las empresas, qué queda |
| `tercero/ficha-secciones-con-soporte` ☐ | Tercero | Flujo con las 6 secciones; soporte en la bóveda (spec `ficha-f5`) | «Documento que respalda esta información», elegir o subir un soporte, aviso de coherencia |
| `tercero/ficha-actualizar-desde-mi-rut` ☐ | Tercero | RUT en la bóveda (spec `ficha-pendientes`) | «Actualizar desde mi RUT», lo que propone, aplicar, la PEP se conserva |
| `tercero/ficha-barra-y-pendientes` ☐ | Tercero | Ficha a medias | «Faltan N datos», guardar sin completar, volver; Legal sin socio y el aviso; sección que no aplica |
| `tercero/ficha-cambio-tras-aprobar` ☐ | Tercero | Ficha aceptada por una empresa (spec `revalidaciones-y-exencion`) | Cambiar dirección, la empresa ve el cambio posterior y debe volver a aceptar, qué ve el tercero |
| `tercero/declaracion-pep-persona-natural` ☐ | Tercero | Persona natural en la ficha (spec `declaracion-pep`) | «Sí» a PEP abre el diálogo, datos, condición, vínculo con otra PEP, juramento y firma dibujada o subida, guardar |
| `tercero/declaracion-pep-personas-relacionadas` ☐ | Tercero | Jurídica con representante y socios (spec `declaracion-pep`) | PEP por persona en Legal, varias PEP relacionadas (hasta 30), firma por apoderado |
| `tercero/descargar-mi-declaracion-pep` ☐ | Tercero | Declaración ya firmada (spec `declaracion-pep-descarga`) | Descargar PDF propio, qué incluye (logo de la empresa o de Validasoft), solo lo guardado |
| `tercero/cuentas-bancarias` ☐ | Tercero | Empresa cuyo tipo de vínculo exige cuenta designada (spec `cuentas`) | Mis cuentas: agregar, designar para una empresa, retirar, estado de aprobación (cuatro ojos) |
| `tercero/campana-y-avisos` ☐ | Tercero | Observación y avisos de la empresa (specs `novedades-tercero`, `tiempo-real`) | Campana con la marca de la empresa, abrir desde ahí, preferencias (apagar un tipo), avisos en vivo sin recargar |
| `tercero/contactos-por-empresa` ☐ | Tercero | Vínculo con dos contactos (spec `contactos-por-empresa`) | Editar los contactos de una empresa sin tocar los de otra |
| `tercero/documento-por-vencer-y-vencido` ☐ | Tercero | Documento con avisos y gracia (specs `vencimientos`, `vigencias`) | «Vigente», «Por vencer» con aviso, en gracia, vencido, subir uno nuevo |
| `tercero/vinculo-por-vencer-renovar` ☐ | Tercero | Flujo con vigencia periódica del vínculo y fecha simulada (spec `vigencias`) | Encabezado «Por vencer», renovación, vencido, qué pasa con el certificado |
| `tercero/vinculo-en-revision-por-actualizacion` ☐ | Tercero | Aprobado por dos empresas y versión nueva del RUT (spec `vinculo-en-revision`) | Encabezado de «Mis empresas» y de la revisión, tarjeta del documento, actualización rechazada y su aviso |
| `tercero/documento-eximido-y-vinculo-bloqueado` ☐ | Tercero | Documento eximido; vínculo bloqueado (specs `revalidaciones-y-exencion`, `pendientes-pequenos`) | El documento figura eximido; con el vínculo bloqueado no se puede enviar y el tercero no ve el motivo |

### privacidad, publico y movil (21)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `privacidad/solicitud-sobre-mis-datos` ☐ | Tercero | Tercero con vínculo (spec `derechos-plan-correo`) | Privacidad: nueva solicitud (consulta, corrección, supresión), radicado, estados, constancia |
| `privacidad/exportar-mi-boveda` ☐ | Tercero | Bóveda con documentos | Pedir la exportación, estado, descarga, qué incluye |
| `privacidad/lo-que-acepte-y-textos-nuevos` ☐ | Tercero | Empresa que publica una versión nueva de un texto legal | Textos aceptados y su versión, aviso de texto actualizado, volver a aceptar |
| `publico/verificar-certificado-autentico` ☐ | Público | Certificado emitido (spec `certificado`) | `/verificar/{folio}`: pasos de la verificación criptográfica, datos del certificado, descargar el PDF |
| `publico/verificar-certificado-invalido` ☐ | Público | Folio o huella inexistente | Mensaje claro, cómo reintentar, qué no revela |
| `publico/certificado-en-otros-estados` ☐ | Público | Certificados bloqueado, revocado, reemplazado, en verificación y con pendientes (specs `certificado`, `vinculo-en-revision`) | Cada estado con su aviso y qué significa para quien consulta |
| `publico/verificar-con-el-pdf` ☐ | Público | PDF del certificado | Subir el PDF, comparar la huella, resultado |
| `publico/pagina-no-encontrada` ☐ | Público | — | Ruta inexistente, enlace roto, volver |
| `publico/embebido-esperando-sesion` ☐ | Público | Página `/embed/{componente}` sin sesión | «Esperando sesión», qué pide al sistema anfitrión, error de origen |
| `movil/tercero-navegacion` ☐ | Tercero | Tercero con dos empresas | Barra inferior, cada pestaña, campana, salir (celular: 390 × 844) |
| `movil/tercero-subir-documento` ☐ | Tercero | Documento pendiente | Elegir archivo en el celular, versión guardada, volver |
| `movil/tercero-mi-informacion` ☐ | Tercero | Flujo con las 6 secciones | Selector «Paso N de 6», una sección completa, barra de guardar |
| `movil/tercero-declaracion-pep-firma` ☐ | Tercero | Natural con PEP | Firma dibujada con el dedo en el diálogo, guardar |
| `movil/tercero-campana` ☐ | Tercero | Avisos pendientes | Panel de la campana en pantalla chica, preferencias |
| `movil/verificar-certificado` ☐ | Público | Certificado emitido | La verificación pública en celular |
| `movil/app-de-la-empresa` ☐ | Revisor/área | Revisor con expediente en bandeja | La app de la empresa a 390 px: menú «Más», bandeja, expediente (usa `celular: true`) |
| `movil/tercero-estado-de-la-revision` ☐ | Tercero | Expedientes abiertos, observado y aprobado | Frase de estado y tarjetas de documentos en celular |
| `movil/tercero-boveda` ☐ | Tercero | Bóveda con varias versiones | Lista de documentos y sus versiones en celular |
| `movil/tercero-cuentas` ☐ | Tercero | Cuenta por designar | Mis cuentas y los diálogos en celular |
| `movil/tercero-privacidad` ☐ | Tercero | — | Privacidad y la solicitud en celular |
| `movil/tercero-entrar` ☐ | Tercero | Invitación | `/entrar` y el código de 6 recuadros en celular |

---

## Lote 2 — La empresa que revisa: vínculos, revisión, documentos e informes (46)

Todo en la app de la empresa (`abrirEmbebido`). Revisor/área: Rubén (Compras de Norte) o Sofía (Revisión de Sur); administradora: Ana. Para llegar al estado, siembra con `completarInvitacion`, `soporte/flujos.ts` y `soporte/devolucion.ts`.

### vinculos (14)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `vinculos/lista-y-filtros` ☐ | Administrador de empresa | Vínculos en varios estados (Invitado, En validación, Activo, Por vencer, Bloqueado) | Vínculos: columnas, filtros por estado y tipo, búsqueda, abrir uno |
| `vinculos/detalle-del-vinculo` ☐ | Administrador de empresa | Vínculo activo con referencias externas y contactos | Datos del tercero, estado, invitación, referencias por sistema, contactos, historial con sus rótulos |
| `vinculos/reenviar-invitacion` ☐ | Administrador de empresa | Vínculo invitado | «Reenviar invitación», correo y vigencia del enlace, aviso, el correo nuevo |
| `vinculos/bloquear-y-desbloquear` ☐ | Administrador de empresa | Vínculo activo | Bloquear con motivo y fecha, lo que ve el tercero (nada del motivo), certificado, desbloquear (spec `certificado`) |
| `vinculos/desvincular-y-reintegrar-sin-revalidar` ☐ | Administrador de empresa | Vínculo aprobado (spec `reintegro`) | Desvincular, certificado revocado, reintegrar restituyendo lo aprobado |
| `vinculos/reintegrar-revalidando` ☐ | Administrador de empresa | Vínculo desvinculado con documentos vencidos (spec `reintegro`) | El diálogo explica por qué no se puede sin revalidar, renovación completa |
| `vinculos/anular-vinculo` ☐ | Administrador de empresa | Vínculo con expediente abierto (spec `ciclo-vinculo`) | Anular con la palabra de confirmación, efecto en el expediente |
| `vinculos/abrir-expediente-manual` ☐ | Administrador de empresa | Vínculo invitado, sin aceptar (FL-6) | Abrir el expediente sin esperar al tercero, aparece en la bandeja |
| `vinculos/tercero-360` ☐ | Administrador de empresa | Tercero con dos empresas (spec `tercero360`) | Ficha del tercero, datos, vínculos con esta empresa, lo que no se comparte entre empresas |
| `vinculos/ficha-aceptada-del-vinculo` ☐ | Administrador de empresa | Ficha completa y aprobada (spec `ficha`) | «Ver la ficha aceptada»: por sección lo aceptado y lo que cambió después |
| `vinculos/cuenta-designada-aprobar` ☐ | Oficial de cumplimiento | Tipo de vínculo con cuenta designada (spec `cuentas`) | Cuenta por aprobar, cuatro ojos (quien la pidió no la aprueba), aprobar y rechazar |
| `vinculos/vinculo-con-actualizacion-pendiente` ☐ | Administrador de empresa | Aprobado por dos empresas y RUT nuevo (spec `vinculo-en-revision`) | «En revisión» en lista, filtro, inicio y detalle; certificado «En verificación»; aviso de la actualización no aprobada |
| `vinculos/bloqueado-no-recibe-version` ☐ | Administrador de empresa | Vínculo bloqueado y versión nueva (spec `pendientes-pequenos`) | El bloqueado no recibe el RUT nuevo; al desbloquear queda «En revisión» |
| `vinculos/referencias-externas` ☐ | Sistema integrador | Vínculo creado por un sistema con su referencia | Dónde se ve la referencia por sistema, buscar por referencia, varios sistemas |

### revision (19)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `revision/inicio-lo-que-te-toca` ☐ | Revisor/área | Expedientes en revisión y observados (spec `inicio`) | Inicio: cifras, «Lo que te toca», indicadores, ir a la bandeja |
| `revision/bandeja-de-revision` ☐ | Revisor/área | Varios expedientes de dos áreas | Columnas (tercero, tipo, área, plazo), filtros, indicador de plazo, abrir |
| `revision/aprobar-un-area` ☐ | Revisor/área | Proveedor «En revisión» en Norte (spec `decisiones`) | Abrir, revisar documentos, «Aprobar área», diálogo, estado de las demás áreas |
| `revision/aprobar-documento-por-documento` ☐ | Revisor/área | Expediente con documentos por área (spec `decisiones`) | Aprobar cada documento en la versión vista, el área se cierra al decidir todos |
| `revision/observar-con-motivos` ☐ | Revisor/área | Expediente en revisión (spec `decisiones`) | «Observar», documentos y motivos tipificados, nota, qué recibe el tercero (un solo correo) |
| `revision/rechazar-expediente` ☐ | Oficial de cumplimiento | Área con «Puede rechazar» (spec `decisiones`) | «Rechazar», motivos que lee el tercero, justificación interna, el vínculo vuelve a Invitado |
| `revision/aprobar-en-nombre-del-area` ☐ | Administrador de empresa | Área sin respuesta (spec `decisiones`) | Menú de administrador, justificación obligatoria, queda registrado |
| `revision/etapas-en-secuencia` ☐ | Revisor/área | Flujo de dos etapas (spec `secuencia`) | Cada área ve el expediente solo cuando le toca, la etapa siguiente empieza al aprobar todas |
| `revision/dos-aprobadores-y-separacion-de-funciones` ☐ | Revisor/área | Área con 2 aprobadores y separación activada (spec `secuencia`) | «1 de 2» aprobaciones, el mismo aprobador no cuenta dos veces, quien invitó no decide |
| `revision/desaprobar-y-reabrir-una-etapa` ☐ | Administrador de empresa | Expediente aprobado (spec `desaprobacion`) | Reabrir una etapa, volver a la fase inicial, solo el administrador ve las acciones |
| `revision/desaprobar-un-expediente-aprobado` ☐ | Administrador de empresa | Vínculo aprobado con certificado (spec `desaprobacion`) | Desaprobar, vínculo a validación, certificado revocado, otro certificado al aprobar de nuevo |
| `revision/anular-un-veredicto` ☐ | Administrador de empresa | Área que ya aprobó (spec `ciclo-vinculo`) | Anular el veredicto de un área, cierre manual de una observación |
| `revision/actualizacion-de-un-documento` ☐ | Revisor/área | Versión nueva durante la revisión (spec `actualizaciones`) | Aviso en tiempo real, el diálogo abierto no deja confirmar, ver el cambio |
| `revision/revalidaciones-en-lote` ☐ | Revisor/área | Revalidaciones sin cambios y una con cambios (spec `revalidaciones-y-exencion`) | Lista de Revalidaciones, selección en lote (la que cambió no se selecciona), efecto en el certificado |
| `revision/eximir-un-documento` ☐ | Administrador de empresa | Expediente con RUT requerido (spec `revalidaciones-y-exencion`) | Eximir con motivo, las áreas no lo revisan, el tercero lo ve eximido |
| `revision/historial-del-expediente` ☐ | Revisor/área | Expediente con observación, respuesta y aprobación | Línea de tiempo: quién, qué y cuándo, rótulos, valores largos |
| `revision/visor-y-datos-de-un-documento` ☐ | Revisor/área | Documento con campos y, si hay IA, análisis | Visor, versiones, datos declarados, vigencia, descargar (queda auditado) |
| `revision/recordatorio-de-observaciones` ☐ | Revisor/área | Expediente observado (spec `novedades-tercero`) | Recordatorio individual desde el expediente, qué llega al tercero (correo y campana) |
| `revision/responder-con-lo-corregido` ☐ | Revisor/área | El tercero respondió a una observación (spec `novedades-tercero`) | Aviso a las áreas que vuelven a revisar lo corregido, qué cambió |

### documentos (6) e informes (7)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `documentos/vencimientos` ☐ | Administrador de empresa | Documentos en distintos estados de vigencia (spec `vencimientos`) | Vencimientos: lista, filtros, por vencer, en gracia, vencidos |
| `documentos/ciclo-de-vigencia` ☐ | Administrador de empresa | Tipo con aviso y gracia y fecha simulada (specs `vencimientos`, `vigencias`) | Vigente → por vencer (aviso) → gracia → vencido, avisos que recibe el tercero |
| `documentos/vigencia-del-vinculo` ☐ | Administrador de empresa | Flujo con vigencia periódica (spec `vigencias`) | Por vencer, renovación, vencido y el certificado, qué cambia en la lista |
| `documentos/documento-compartido-entre-empresas` ☐ | Revisor/área | Dos empresas, mismo RUT (spec `multiempresa-documento-global`) | Una acepta la v2 y la otra ve su aceptación desactualizada, sin ver la decisión de la otra |
| `documentos/descargar-un-documento` ☐ | Revisor/área | Documento aprobado | Descargar, el registro en la auditoría de accesos |
| `documentos/recordatorio-desde-vencimientos` ☐ | Administrador de empresa | Documentos por vencer (spec `operacion`) | Recordatorio de observaciones desde Vencimientos y Revalidaciones |
| `informes/estado-de-expedientes` ☐ | Administrador de empresa | Expedientes en varios estados (spec `informes`) | Informe con tiempos por área, filtros, resumen, descargar Excel |
| `informes/informe-de-vinculos` ☐ | Administrador de empresa | Vínculos de varios tipos | Informe de vínculos, filtros y exportación |
| `informes/plazos-de-revision` ☐ | Administrador de empresa | Plazos por área y expedientes fuera de plazo (spec `operacion`) | Informe SLA, tablas por área y por persona, cumplimiento |
| `informes/exportar-un-expediente` ☐ | Auditor | Expediente aprobado con documentos (spec `informes`) | Exportar el ZIP, qué trae (documentos, resumen PDF, línea de tiempo), el registro en la auditoría |
| `informes/auditoria` ☐ | Administrador de empresa | Acciones de configuración y descargas (spec `auditoria`) | Auditoría: filtros, detalle de un cambio (antes/después), accesos a documentos, exportar a Excel |
| `informes/certificados` ☐ | Administrador de empresa | Certificados vigentes y revocados (spec `certificados`) | Lista de certificados, estado, PDF, abrir la ficha del tercero |
| `informes/certificado-bloquear-y-revocar` ☐ | Administrador de empresa | Certificado vigente (spec `certificado`) | Bloquear y desbloquear, revocar, qué ve quien verifica |

---

## Lote 3 — Configuración, operación masiva e IA (44)

Todos en la app de la empresa como administradora (Ana) salvo donde se indica. Cada sección de Configuración documenta su pantalla, cada campo, la validación y la variante que cambia el resultado.

### configuracion (22)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `configuracion/tipo-de-documento-y-flujo` ✅ | Administrador de empresa | Grupo base | Tipo de documento propio (formulario, archivo, vigencia) y flujo publicado por defecto |
| `configuracion/tipo-de-documento-solo-formulario-y-plantilla` ☐ | Administrador de empresa | Grupo base; archivo DOCX o PDF de plantilla | Modo «Formulario» con campos (texto, número, fecha, lista, Sí/No), subir plantilla, versiones, retirar |
| `configuracion/tipo-de-documento-de-la-plataforma` ☐ | Administrador de empresa | Grupo base | Partir de un documento de la plataforma (RUT, Cámara), techos de formato y peso, error por regla más laxa, ámbito compartido |
| `configuracion/tipos-de-vinculo` ☐ | Administrador de empresa | Grupo base | Crear un tipo, «Exige código en el enlace», «Cuenta designada», desactivar |
| `configuracion/flujo-etapas-y-areas-en-paralelo` ☐ | Administrador de empresa | Áreas Compras y Cumplimiento (`crearEscenario`) | Dos etapas, dos áreas en paralelo, aprobadores y SLA, «Puede rechazar» y «Automática» |
| `configuracion/flujo-condiciones-por-tipo-de-persona` ☐ | Administrador de empresa | Tipos RUT, CEDULA y CAMARA (spec `condiciones`) | «Se pide a»: todos, solo jurídicas, solo naturales, condición personalizada |
| `configuracion/flujo-secciones-de-la-ficha` ☐ | Administrador de empresa | Grupo base | Agregar secciones de «Mi información» como requisito, periodos de la financiera, vigencia y días de revalidación |
| `configuracion/flujo-plazo-reintento-y-recordatorios` ☐ | Administrador de empresa | Grupo base | Plazo de cargue y recordatorios, reintento con días de espera, recordatorio de observaciones |
| `configuracion/flujo-vigencia-del-vinculo` ☐ | Administrador de empresa | Flujo con secciones | Vigencia en días, avisos y gracia, qué exige (una sección) |
| `configuracion/flujo-verificaciones-de-cumplimiento` ☐ | Administrador de empresa | Grupo base | Pedir listas restrictivas y estudio SAGRILAFT, bloqueante, vigencia y reconsulta |
| `configuracion/flujo-versiones-y-borradores` ☐ | Administrador de empresa | Flujo publicado | Editar → borrador v2, descartar cambios, publicar, ver versiones y volver a la vigente |
| `configuracion/flujo-por-defecto` ☐ | Administrador de empresa | Dos flujos publicados del mismo tipo | Cambiar el flujo por defecto por tipo de vínculo, «Sin flujo por defecto» y aperturas pendientes |
| `configuracion/areas-y-miembros` ☐ | Administrador de empresa | Personas con rol en la empresa | Crear área, asignar miembros, desactivar, efecto en la bandeja |
| `configuracion/usuarios-y-roles` ☐ | Administrador de empresa | Sistema integrado con identidades | Crear una persona, roles por empresa o de todo el grupo (Administrador, Revisor, Oficial, Auditor), identidad en el sistema, desactivar |
| `configuracion/empresas-y-textos-legales` ☐ | Administrador de empresa | Grupo base | Crear una empresa (prefijo de folio), publicar autorización y términos, versión nueva, qué ve el tercero |
| `configuracion/datos-y-marca` ☐ | Administrador de empresa | Grupo base | Logo, nombre comercial, `reply-to`, dónde se ve (correos, PDF del certificado, declaración) |
| `configuracion/plantillas-de-correo` ☐ | Administrador de empresa | Grupo base (spec `plantillas`) | Lista de plantillas, editar asunto y cuerpo, variables, vista previa, restablecer |
| `configuracion/dominio-de-correo` ☐ | Administrador de empresa | Grupo base (spec `derechos-plan-correo`) | Fijar un dominio, registros DNS (SPF, DKIM), verificar, quitar |
| `configuracion/motivos` ☐ | Administrador de empresa | Grupo base | Motivos tipificados de observación y rechazo, crear, desactivar, cómo los lee el tercero |
| `configuracion/plan-del-grupo` ☐ | Administrador de empresa | Grupo con límites (spec `derechos-plan-correo`) | Plan y uso del grupo, aviso de límite que enlaza a esta pantalla |
| `configuracion/accesos-de-soporte` ☐ | Administrador de empresa | Grupo base | Conceder un acceso de soporte a Plataforma con vigencia, qué puede ver (nunca documentos), revocar |
| `configuracion/permisos-por-rol` ☐ | Administrador de empresa | Una persona por rol (Administrador, Revisor, Oficial, Auditor) | Qué secciones ve cada rol, «sin permiso», botones que desaparecen |

### operacion (10)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `operacion/invitacion-por-lotes` ☐ | Administrador de empresa | Grupo base (spec `lotes`) | Descargar la plantilla, subir el archivo, vista previa con errores por fila, confirmar, lote procesado |
| `operacion/lote-pausar-reanudar-cancelar` ☐ | Administrador de empresa | Lote grande en proceso | Pausar, reanudar, cancelar, reintentar fallidos, informe por fila |
| `operacion/campana-de-renovacion` ☐ | Administrador de empresa | Vínculos por vencer (spec `operacion`) | Filtro, vista previa de a quién afecta, aviso antes de confirmar, proceso en segundo plano, correo y campana del tercero |
| `operacion/campana-pausar-y-reintentar` ☐ | Administrador de empresa | Campaña en proceso | Detalle de la campaña, pausar, reanudar, cancelar, reintentar |
| `operacion/plazo-de-cargue-y-caducidad` ☐ | Administrador de empresa | Flujo con plazo corto y fecha simulada (spec `lotes`) | Caducidad por plazo de cargue, recordatorios, reapertura |
| `operacion/plazos-de-revision` ☐ | Administrador de empresa | Plazo por defecto y por área (spec `operacion`) | Configuración, avisos de plazo (campana y correo), indicador en la bandeja |
| `operacion/delegacion-de-revision` ☐ | Revisor/área | Rubén y Olga (spec `operacion`) | Delegar entre dos fechas, aviso, la bandeja de quien recibe, separación de funciones, revocar |
| `operacion/recordatorio-en-lote` ☐ | Revisor/área | Varios expedientes observados (spec `novedades-tercero`) | Recordatorio desde la bandeja (uno por día), correo y campana |
| `operacion/recordatorio-automatico-por-flujo` ☐ | Administrador de empresa | Flujo con recordatorio automático (spec `operacion`) | Modo «Empresa» o cada N días, lo que recibe el tercero |
| `operacion/ayuda-de-documentos` ☐ | Administrador de empresa | Tipos de documento (spec `operacion`) | Texto de ayuda por documento y empresa, lo que lee el tercero al subir, plantillas de pre-vencimiento y preferencias |

### ia (8), privacidad (2) y cumplimiento de configuración (2)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `ia/plan-y-uso-de-ia` ☐ | Administrador de empresa | `activarIa(e, empresa)` con proveedor simulado | Plan y uso: cupo del mes, consumo, avisos, qué ve el tercero (nada) |
| `ia/revision-automatica-niveles` ☐ | Administrador de empresa | IA activa (spec `revision-automatica`) | Manual, Asistido y Automático, qué cambia en cada uno, tope y muestreo |
| `ia/revision-asistida-y-excepciones` ☐ | Revisor/área | Nivel Asistido con un documento dudoso (spec `revision-automatica`) | Excepciones: documento con lectura dudosa, motivo, decidir |
| `ia/revision-automatica-aprueba-sola` ☐ | Revisor/área | Nivel Automático (spec `revision-automatica`) | Documento aprobado solo, «Sistema» en el historial, revisión humana por muestreo |
| `ia/cupo-agotado` ☐ | Administrador de empresa | Cupo en 100 % (spec `ia-cupo-y-escalamiento`) | «Sin análisis de IA», aviso por campana y correo, el tercero no ve nada |
| `ia/escalamiento-a-modelo-superior` ☐ | Revisor/área | Lectura dudosa (spec `ia-cupo-y-escalamiento`) | Confianza, «Revisado por un modelo de mayor capacidad», tokens y costo en «Plan y uso de IA» |
| `ia/cruce-de-la-ficha-con-los-soportes` ☐ | Revisor/área | IA activa y ficha con soporte (spec `ficha-f5`) | Cruce ficha ↔ documento, coincidencias y diferencias, qué decide la persona |
| `ia/analisis-de-un-documento` ☐ | Revisor/área | IA activa | Campos leídos, confianza, legibilidad, cómo se usa en la decisión |
| `privacidad/habeas-data-de-la-empresa` ☐ | Oficial de cumplimiento | Solicitud del tercero (spec `derechos-plan-correo`) | Habeas data: solicitudes con plazo, responder, plan de supresión, constancia |
| `privacidad/plazos-y-avisos-de-habeas` ☐ | Administrador de empresa | Solicitud cerca de vencer | Aviso de plazo (enlaza a esta pantalla), vencida, quién atiende |
| `cumplimiento/matriz-de-riesgo-configuracion` ☐ | Oficial de cumplimiento | Grupo base (spec `matriz-riesgo`) | Factores, límites y niveles del riesgo SAGRILAFT, guardar, efecto en el estudio |
| `cumplimiento/listas-restrictivas-de-la-empresa` ☐ | Administrador de empresa | Proveedor simulado (spec `verificaciones`) | Proveedor y credenciales de la empresa, consumo del mes, Infolaft «Disponible» de la plataforma |

---

## Lote 4 — Cumplimiento, Plataforma, integración, embebido y acceso (48)

### cumplimiento (11)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `cumplimiento/pedir-listas-restrictivas` ☐ | Oficial de cumplimiento | Flujo con verificación bloqueante; proveedor simulado (`pedirVerificaciones`, spec `verificaciones`) | La consulta corre sola al pasar a revisión, resultado «Satisfecha», quiénes se consultaron |
| `cumplimiento/analizar-una-coincidencia` ☐ | Oficial de cumplimiento | Nombre con coincidencia en OFAC (spec `verificaciones`) | Coincidencia por analizar, comparar datos, descartar con motivo, la verificación se satisface |
| `cumplimiento/coincidencia-confirmada` ☐ | Oficial de cumplimiento | Coincidencia que se confirma (spec `cumplimiento-vigilancia`) | Impide aprobar, termina en rechazo, la otra empresa del tercero no se toca |
| `cumplimiento/estudio-de-cumplimiento` ☐ | Oficial de cumplimiento | Estudio pedido (spec `cumplimiento-vigilancia`, `matriz-riesgo`) | Nivel de riesgo calculado, conflicto de interés, debida diligencia, concepto, registrar |
| `cumplimiento/estudio-vencido-y-renovacion` ☐ | Oficial de cumplimiento | Estudio vencido con fecha simulada (spec `cumplimiento-vigilancia`) | Aviso, bloqueo para aprobar, renovar |
| `cumplimiento/registrar-consulta-por-fuera` ☐ | Oficial de cumplimiento | Consulta hecha en otro sitio (spec `evidencia-manual`) | Subir el PDF, dónde y cuándo, coincidencias, quedan por analizar |
| `cumplimiento/monitoreo-periodico` ☐ | Oficial de cumplimiento | Vínculo activo con monitoreo (spec `cumplimiento-vigilancia`) | Reconsulta sola, la persona nueva llega a la bandeja del oficial, confirmar y salir de la bandeja |
| `cumplimiento/rechazo-directo-del-oficial` ☐ | Oficial de cumplimiento | Hallazgo grave confirmado (spec `evidencia-manual`) | Rechaza el expediente sin pasar por un área, motivos y justificación |
| `cumplimiento/bandeja-del-oficial` ☐ | Oficial de cumplimiento | Varias coincidencias y estudios | Página Cumplimiento: pendientes, filtros, abrir |
| `cumplimiento/informe-sagrilaft` ☐ | Oficial de cumplimiento | Estudios y declaraciones PEP (spec `informe-sagrilaft`) | Informe, «Sin declaración», filtros, descargar Excel con columnas PEP |
| `cumplimiento/adjuntos-del-estudio` ☐ | Oficial de cumplimiento | Estudio en curso | Adjuntar soportes al estudio, descargar, quitar |

### plataforma (14)

Siembra común: llave de Plataforma (`CLAVE_PLATAFORMA`) y `abrirEmbebidoCon` sin empresa (spec `plataforma`, `plataforma-identidad`, `soporte/plataforma.ts`).

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `plataforma/ingreso-del-operador` ☐ | Plataforma | Operador con passkey y clave de arranque (spec `plataforma-identidad`) | Entrar a la consola, passkey, qué puede ver cada rol de operador |
| `plataforma/crear-grupo-y-empresa` ☐ | Plataforma | — (spec `alta-empresa-certificacion`) | Nuevo grupo: datos, primera empresa, administrador, sistema y llaves; el resultado se ve una sola vez |
| `plataforma/lista-de-grupos` ☐ | Plataforma | Varios grupos | Grupos con sus empresas, buscar, abrir la tarjeta |
| `plataforma/agregar-empresa-a-un-grupo` ☐ | Plataforma | Grupo existente | Agregar una empresa, prefijo de folio, la empresa aparece en el grupo |
| `plataforma/sistemas-de-un-grupo` ☐ | Plataforma | Grupo con un sistema integrado | Ver, desactivar y reactivar un sistema, efecto sobre sus llaves y sesiones |
| `plataforma/plan-del-grupo` ☐ | Plataforma | Grupo con límites (spec `derechos-plan-correo`) | Plan, límites y uso, cambiar, aviso al grupo |
| `plataforma/plan-de-ia-y-llave-openai` ☐ | Plataforma | Empresa sin plan (spec `ia-cupo-y-escalamiento`) | Plan de IA, validaciones incluidas, llave de OpenAI (nunca visible), «Probar llave» |
| `plataforma/consumo-y-tarifa-de-ia` ☐ | Plataforma | Consumo de IA de varias empresas | Uso por cliente con su tarifa, exportar |
| `plataforma/operadores` ☐ | Plataforma | Operadores directos | Crear, desactivar y reactivar, passkey para cambios sensibles |
| `plataforma/secretos` ☐ | Plataforma | — | Secretos de la plataforma, qué se puede ver y cambiar, step-up con passkey |
| `plataforma/listas-restrictivas-de-la-plataforma` ☐ | Plataforma | Cuenta de Infolaft (spec `pendientes-pequenos`) | Configurar la cuenta, cuota; la empresa solo ve «Disponible» |
| `plataforma/solicitudes-de-habeas-de-la-plataforma` ☐ | Plataforma | Solicitud sobre la bóveda global | Atenderla desde la consola, plazos, constancia |
| `plataforma/ingresos-de-los-operadores` ☐ | Plataforma | Varios ingresos | Auditoría de ingresos, filtros, clave de arranque |
| `plataforma/acceso-de-soporte-a-una-empresa` ☐ | Plataforma | Acceso concedido por la empresa | Entrar a una empresa con acceso de soporte, solo lectura, nunca documentos |

### integracion (11)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `integracion/sistema-integrador` ☐ | Sistema integrador | Grupo base | Registrar un sistema: código, clave pública, orígenes permitidos, identidades de personas |
| `integracion/api-keys` ☐ | Sistema integrador | Sistema registrado | Crear una llave con sus permisos (scopes) y empresas, verla una sola vez, rotar y revocar |
| `integracion/webhooks` ☐ | Sistema integrador | Receptor de la prueba (`soporte/receptor.ts`, spec `eventos`) | Crear la suscripción, eventos, secreto, firma verificada, entregas en orden |
| `integracion/webhook-entrega-fallida-y-reenvio` ☐ | Sistema integrador | Receptor que falla (spec `eventos`) | Entrega fallida, reintentos, reenviar desde la pantalla |
| `integracion/invitar-por-api` ☐ | Sistema integrador | Llave del sistema (spec `integracion`) | `POST /vinculos` con `mostrarCodigo`, el correo, el vínculo en la lista de la empresa, referencia externa |
| `integracion/consulta-de-certificacion` ☐ | Sistema integrador | Tercero aprobado (spec `integracion`) | `GET /consultas/certificacion`, respuesta resumida, `If-None-Match` y 304 |
| `integracion/eventos-pull-y-en-vivo` ☐ | Sistema integrador | Eventos de un vínculo | `GET /eventos` con cursor, SSE, tipos de evento |
| `integracion/sandbox` ☐ | Sistema integrador | Grupo con sandbox (spec `integracion-sandbox`) | Crear, sincronizar y reiniciar el sandbox, llaves de prueba |
| `integracion/buzon-de-pruebas` ☐ | Sistema integrador | Sandbox con correos (spec `integracion-sandbox`) | Leer el buzón de correo de pruebas, vaciarlo |
| `integracion/importar-del-portal` ☐ | Administrador de empresa | Origen simulado del Portal | Importación: corrida, vista previa, resultado, errores por fila |
| `integracion/identidad-delegada` ☐ | Sistema integrador | Sistema con clave privada (`asercion` en `soporte/api.ts`) | `POST /sesiones-usuario` con `mostrarCodigo`, el JWT de 5 minutos, `jti` de un solo uso, entrar a la app |

### embebido (5) y acceso (7)

| Archivo | Actor | Siembra | Pasos esperados |
|---|---|---|---|
| `embebido/app-de-la-empresa-en-un-iframe` ☐ | Revisor/área | Página anfitriona (`soporte/embebido.ts`) | La app dentro del sistema, sesión por `postMessage`, selector de empresa, salir |
| `embebido/navegacion-adaptativa-y-tema` ☐ | Revisor/área | Anfitrión a 1536, 1100 y 700 px (spec `navegacion`) | «Más» adaptativo, engranaje de Configuración, modo oscuro |
| `embebido/tercero-embebido` ☐ | Tercero | Anfitrión del tercero (spec `embebido-tercero`) | `POST /sesiones-tercero`, bóveda, expediente y ficha en el iframe del sistema |
| `embebido/sesion-vencida` ☐ | Revisor/área | Sesión de corta duración | «Tu sesión venció», nueva sesión del sistema, lo guardado se conserva |
| `embebido/origenes-permitidos` ☐ | Sistema integrador | Sistema con orígenes | Qué origen puede embeber, error por origen no permitido |
| `acceso/ingreso-directo-por-correo` ☐ | Revisor/área | Persona con acceso directo (spec `ingreso`) | Ingreso sin contraseña: correo, enlace y código, sesión |
| `acceso/ingreso-con-passkey` ☐ | Revisor/área | Persona con passkey | Registrar una passkey, entrar con ella |
| `acceso/mi-cuenta-sesiones-y-codigos` ☐ | Revisor/área | Persona con sesiones | Mi cuenta: sesiones abiertas y cerrarlas, passkeys, códigos de recuperación |
| `acceso/ingreso-configuracion-del-grupo` ☐ | Administrador de empresa | Grupo base | Modos de ingreso, duración e inactividad de la sesión, passkey para todos |
| `acceso/sso-configurar` ☐ | Administrador de empresa | Proveedor OIDC simulado (spec `plataforma-identidad`) | Proveedor, dominios verificados, creación de personas, roles por grupo, modo exclusivo |
| `acceso/sso-ingresar` ☐ | Revisor/área | SSO configurado | Entrar con el proveedor del cliente, persona creada al vuelo con su rol |
| `acceso/codigos-de-recuperacion` ☐ | Administrador de empresa | Persona con passkey | Generar y usar un código de recuperación |

---

### Cómo reportar

Informe de cada agente (≤ 15 líneas): recorridos hechos, pasos totales, fallas sin resolver y defectos vistos. Lo que no se pueda documentar porque el producto no lo hace (o no lo hace como dice
esta lista), se anota aquí al lado de la fila en vez de inventarlo.
