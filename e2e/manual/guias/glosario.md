---
titulo: "Glosario"
seccion: "Configuración y glosario"
orden: 30
resumen: "El vocabulario de Validasoft: quién es quién, qué es un vínculo, un expediente o un certificado, y los estados de cada cosa tal como los guarda el sistema."
---

Las palabras de esta página son las que usan la aplicación, la API y los documentos de diseño (`docs/10-glosario.md` es la referencia larga con las equivalencias del SRM y el Portal). Los estados se escriben como los guarda el sistema: son texto, nunca números.

## Quién es quién

| Término | Qué es |
|---|---|
| Grupo (tenant) | El cliente de Validasoft: un grupo empresarial con administración común. Toda credencial, usuario y configuración pertenece a un grupo. |
| Empresa | Una razón social del grupo. Cada empresa decide en privado: sus flujos, sus áreas, sus aprobaciones y sus bloqueos. Casi toda llamada a la API es de una empresa (cabecera `X-Empresa`). |
| Tercero | La persona o empresa que se valida (proveedor, cliente, empleado, aliado). Es **único en toda la plataforma** por país, tipo y número de identificación: sube cada documento una vez y lo comparte con cada empresa que lo invita. |
| Persona de la empresa | Quien trabaja en la app web: Administrador, Revisor, Oficial de cumplimiento o Auditor. |
| Operador de Plataforma | Quien opera la instalación desde la consola `/plataforma`. |
| Sistema integrado | Un sistema externo (SRM, Marco, Portal u otro ERP) registrado en el grupo con su clave pública: identifica sus referencias y puede actuar en nombre de personas. |
| Público | Cualquiera, sin sesión: verifica un certificado. |

## El modelo documental

| Término | Qué es |
|---|---|
| Bóveda | El conjunto de documentos y datos del tercero, globales: no pertenecen a ninguna empresa. Una empresa solo ve lo que el tercero le comparte al aceptar su invitación. |
| Documento y versión | Un documento (por ejemplo el RUT) tiene versiones inmutables numeradas. La versión **actual** es la vigente; las anteriores quedan **superadas**. |
| Compartición | La relación entre una versión y un vínculo: es la única puerta por la que una empresa ve un documento del tercero. |
| Hechos | Lo que la plataforma calcula una sola vez de una versión (SHA-256, tipo real, páginas, antivirus, comparación) y, si hay IA, los campos extraídos. Todas las empresas con compartición los ven. |
| Tipo de documento | La definición de un documento que una empresa pide: código, ámbito, formato, vigencia, formulario. Puede apoyarse en uno **canónico** de la plataforma. |
| Ámbito | `Compartido` (global, en la bóveda del tercero) o `Solo de este vínculo` (privado de la relación). |
| Ficha maestra («Mi información») | Los datos del tercero en seis secciones (información general, tributaria, legal, financiera por periodo, seguridad y salud en el trabajo y especialidades), con sus soportes, sus personas relacionadas y la declaración PEP. |
| Vigencia | Cuánto sirve un documento: se calcula por empresa y vínculo a partir del vencimiento declarado o de la regla del tipo, con días de aviso y de gracia. |
| Ratificar | El tercero confirma que un documento o una sección sigue igual, sin subir una versión nueva. |
| Revalidar, actualización | Cuando una versión nueva cambia lo que la empresa ya aprobó, la empresa vuelve a revisar solo lo que cambió: se abre un expediente de **actualización**. |

## Vínculos, expedientes y certificados

| Término | Qué es |
|---|---|
| Vínculo | La relación de una empresa con un tercero en un tipo (proveedor, cliente, empleado…). Es único por empresa, tercero y tipo. |
| Tipo de vínculo | Define el código, si exige un código por correo en el enlace y los atributos que la empresa registra en cada vínculo. |
| Expediente | La revisión de un vínculo: sus requisitos, los veredictos de las áreas y la aprobación. De tipo `Inicial`, `Renovacion` o `Actualizacion`. |
| Flujo aprobatorio | La receta de un expediente: qué documentos y secciones pide, quién los revisa y en qué orden, qué verificaciones exige, plazos y vigencia. Se versiona. |
| Etapa y área | Las etapas van en secuencia; las áreas de una misma etapa revisan en paralelo. Un área tiene miembros y un número de aprobadores. |
| Requisito | Un documento o una sección de la ficha que el flujo pide para ese vínculo. |
| Veredicto | La decisión de un área sobre un requisito: `Aprobado` u `Observado`. Su origen es `Humano`, `Sistema` (revisión automática) o `Administrador`. |
| Observación y motivo | Lo que un área le pide corregir al tercero. Cada observación lleva motivos tipificados con un texto dirigido al tercero que nunca nombra al revisor. |
| Rechazo | Cierre terminal del expediente por un área que puede rechazar, con motivos y justificación. |
| Desaprobar | Un administrador devuelve a revisión lo ya aprobado (una etapa, la fase inicial o el expediente completo). |
| Bloqueo | Decisión privada de una empresa que suspende un vínculo sin borrar su historial; puede tener fecha de fin. |
| Desvincular, reintegrar, anular | Terminar la relación (reintegrable), volver a ella (con o sin revalidar) o anularla definitivamente. |
| Certificado | El comprobante verificable de un vínculo aprobado: folio, huella, PDF y sello. Una emisión nueva reemplaza la anterior. |
| Folio raíz y emisión | El folio base (`{PREFIJO}-C-{año}-{secuencia de 6 dígitos}`) identifica el certificado del vínculo; cada emisión suma su número (`…-1`, `…-2`). |
| Huella | Los primeros 12 caracteres hexadecimales del hash del contenido; desambigua el folio entre grupos. |
| Sello | Firma Ed25519 de la plataforma sobre el contenido canónico del certificado. |
| Cuenta designada | La cuenta bancaria que el tercero elige para cobrarle a una empresa; la aprueba la empresa con cuatro ojos. Si el tipo de vínculo la exige, el expediente no se aprueba hasta que esté aprobada. |
| Referencia externa | La clave de un tercero o un vínculo en un sistema integrado (`{sistema, id}`). |

## Cumplimiento

| Término | Qué es |
|---|---|
| Listas restrictivas | Consulta automática del tercero y de sus personas relacionadas (representantes, suplentes, socios, PEP) contra listas de riesgo, con un proveedor configurable. |
| Estudio de cumplimiento | Lo que hace el oficial (SAGRILAFT): nivel de riesgo, conflicto de interés, debida diligencia y concepto favorable o desfavorable. Vence y vuelve a pedirse. |
| SAGRILAFT | Sistema colombiano de autocontrol y gestión del riesgo de lavado de activos y financiación del terrorismo. |
| PEP | Persona expuesta políticamente: se declara en una sección de la ficha. |
| Matriz de riesgo | Factores, pesos y límites con los que se calcula el nivel de riesgo del estudio. |
| Coincidencia | Un posible hallazgo en una lista; el oficial la descarta o la confirma. |
| Monitoreo | Reconsulta periódica de los vínculos activos. |

## IA y revisión automática

| Término | Qué es |
|---|---|
| Nivel de revisión | `Manual` (la IA solo informa), `Asistido` (sugiere, decide una persona) o `Automático` (aprueba solo lo que no tiene excepciones). Es por empresa. |
| Excepciones | Los documentos que la revisión automática no decidió y esperan a una persona, más una muestra de lo aprobado para confirmar. |
| Cupo de IA | Validaciones de IA incluidas al mes por empresa, con adicionales opcionales; la llave de OpenAI es de cada empresa. |
| Confianza | Qué tan seguro quedó el análisis de un documento; bajo el umbral no se sugiere ni se aprueba. |

## Integración y seguridad

| Término | Qué es |
|---|---|
| API key | Credencial del backend de un sistema: `X-Api-Key: vsk_{entorno}_{grupo}_{secreto}`, con permisos y empresas de alcance. |
| Permiso (scope) | Una capacidad concreta, por ejemplo `vinculos:escribir`. Los roles y las llaves son conjuntos de permisos. |
| Identidad delegada | El sistema integrado actúa en nombre de una persona suya con una aserción firmada (`X-Usuario-Delegado`). |
| Aserción | Un JWT firmado con la clave privada del sistema: vale 5 minutos y una sola vez. |
| Passkey | Credencial WebAuthn de una persona; Administrador, Oficial y Auditor deben tenerla. |
| Confirmar con passkey (reautenticación) | Repetir la passkey para acciones sensibles; vale 5 minutos. |
| Idempotency-Key | Cabecera que identifica una operación de creación para que un reintento nunca la duplique. |
| Evento público | Lo que pasó (`certificado.emitido`, `vinculo.bloqueado`…): llega por webhook firmado, por pull o en pantalla. |
| Cursor | Posición creciente de un evento en el grupo; sirve para leer por pull sin saltarse ninguno. |
| Sandbox | Un grupo hermano de pruebas, con configuración copiada y sin datos: correo retenido, IA y listas simuladas. |
| Habeas data | Los derechos del titular de datos personales (Ley 1581 de Colombia): consulta, corrección y supresión. |
| Plan del grupo | Los límites que Plataforma fija a un grupo: usuarios, empresas, vínculos, almacenamiento y llamadas a la API. |

## Estados, tal como los guarda el sistema

| Cosa | Estados |
|---|---|
| Vínculo | `Invitado`, `EnValidacion`, `Activo`, `PorVencer`, `Vencido`, `Bloqueado`, `Desvinculado`, `Anulado`. «En revisión» no es un estado: es una situación derivada de un expediente de actualización abierto. |
| Expediente | `Abierto`, `EnRevision`, `Observado`, `Aprobado`, `Rechazado`, `Anulado`, `Caducado`. |
| Área en un expediente | `Esperando`, `Pendiente`, `Aprobada`, `Observada`. Etapa: `Esperando`, `Activa`, `Aprobada`. |
| Requisito | `Pendiente`, `Cargado`, `EnRevision`, `Observado`, `Aprobado`, `Eximido`, `NoAplica`, `Vencido`, `Desactualizado`. |
| Versión de documento | `Recibida`, `EnAnalisis`, `Actual`, `Superada`, `Devuelta`, `Retirada`, `Vencida`. |
| Vigencia de un documento | `SinVencimiento`, `Vigente`, `PorVencer`, `EnGracia`, `Vencida`. |
| Certificado | `Vigente`, `ConPendientes` (se ve «En verificación» mientras no vence el plazo), `Suspendido`, `Vencido`, `Revocado`, `Reemplazado`. |
| Verificación de cumplimiento | `Pendiente`, `CoincidenciaPorAnalizar`, `Satisfecha`, `Fallida`, `Expirada`. |
| Entrega de webhook | `Pendiente`, `Entregada`, `Reintentando`, `Fallida`, `Cancelada`. |
| Delegación de revisión | `Programada`, `Vigente`, `Vencida`, `Revocada`. |
| Versión de un flujo | `Borrador`, `Publicada`, `Retirada`. |
