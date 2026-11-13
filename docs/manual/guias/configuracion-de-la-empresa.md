---
titulo: "Configuración de la empresa"
seccion: "Configuración y glosario"
resumen: "Cada ajuste de alcance empresa: datos y marca, correo, IA, listas restrictivas, matriz de riesgo, áreas, flujos aprobatorios, plazos y delegaciones, con valores por defecto y variantes."
---

# Configuración de la empresa

Cada ajuste de alcance empresa: datos y marca, correo, IA, listas restrictivas, matriz de riesgo, áreas, flujos aprobatorios, plazos y delegaciones, con valores por defecto y variantes.

Una **empresa** es una razón social del grupo y decide en privado: sus flujos, sus áreas, sus aprobaciones, sus bloqueos. Estas secciones de **Configuración** valen para la empresa activa (la que muestra el selector de la app). Las de todo el grupo están en [Configuración del grupo](configuracion-del-grupo.md).

> **Nota:** las rutas de la app son `/e/{empresa}/{sección}` dentro del hash de la app web (`/app/#/e/{empresa}/{sección}`). Lo que ve cada persona depende de su rol: `configuracion:leer` y `flujos:leer` los tienen los cuatro roles; `configuracion:escribir`, `flujos:escribir` y `expedientes:administrar`, solo el Administrador; `expedientes:revisar`, el Administrador, el Revisor y el Oficial; `supresiones:resolver`, el Administrador y el Oficial. `empresas:administrar` es solo de un rol asignado a todo el grupo ([roles y permisos](configuracion-del-grupo.md#usuarios-y-roles)).

## Datos y marca

Ruta `/e/{empresa}/datos-marca` · permiso `empresas:administrar`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Razón social, identificación y prefijo de folio | Solo lectura: se fijan al crear la empresa y el prefijo ya está en los folios emitidos. | Los de la creación | — |
| Contacto de soporte para terceros | Enlace `mailto:` al pie del portal del tercero. No se usa como `Reply-To` de los correos. | Sin valor (opcional) | Correo válido de hasta 254 caracteres; vacío lo quita (`contacto-soporte-invalido`). |
| Logo | Se ve en el portal del tercero y en los datos públicos del certificado, el PDF de la declaración PEP y la exportación del expediente. Los correos llevan siempre el logotipo de Validasoft. | Sin logo (se usa el de Validasoft) | Desde la pantalla: SVG o PNG de hasta 200 KB; la API acepta además una URL `https` de hasta 2048 caracteres. «Quitar logo». Errores `logo-invalido` y `logo-demasiado-grande`. |
| Color de marca | Deriva la paleta del portal del tercero (botones y enlaces) con contraste garantizado. | Sin color: tema de Validasoft | `#RRGGBB`, se guarda en mayúsculas; vacío lo quita (`color-marca-invalido`). |

Fuente: `apps/web/src/features/configuracion/datosMarca/modelo.ts`, `src/Validasoft.Dominio/Plataforma/Plataforma.cs`, `apps/public/src/shared/MarcaDeEmpresa.tsx`.

## Plantillas de correo

Ruta `/e/{empresa}/plantillas` · ver y vista previa con `configuracion:leer`; editar y restaurar con `configuracion:escribir`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Correo a personalizar | Cada correo que Validasoft envía en nombre de la empresa. | «Predeterminada», versión 0 | Catálogo cerrado de 14: `invitacion`, `observacion`, `resultado`, `certificado`, `caducidad`, `recordatorio-observaciones`, `desaprobacion`, `reintegro`, `consentimiento-empresa`, `vencido`, `retiro-version` (llega al personal de la empresa), `pre-vencimiento`, `vigencia-vencida` y `renovacion`. Cada uno admite sus propias variables (por ejemplo `empresa`, `tipoVinculo`, `folio`, `documento`, `fecha`). El código OTP, los enlaces de acceso, el autorregistro y el aviso consolidado de vencimiento no se personalizan. |
| Asunto | Reemplaza el asunto del correo. | Vacío: el asunto de siempre | Hasta 150 caracteres y una sola línea. |
| Cabecera y pie | Texto plano antes y después del cuerpo, que arma Validasoft. | Vacío | Hasta 2000 caracteres cada uno; variables como `{{empresa}}`, solo las permitidas del correo (`variable-no-permitida`). Hoy no hay Liquid. |
| Restaurar predeterminado | Quita asunto, cabecera y pie; la versión sube igual. | — | — |
| Enviar recordatorios automáticos | Mientras el expediente siga devuelto al tercero se le recuerda por correo y en su portal, a lo sumo una vez al día; se detiene cuando responde. Cada flujo puede sobrescribirlo. | Apagado | Encendido o apagado. |
| Cada (días) | Frecuencia del recordatorio, contada desde la devolución o desde el último recordatorio. | 3 | Entero de 1 a 60 (`dias-invalidos`). |

Fuente: `src/Validasoft.Dominio/Configuracion/PlantillaNotificacion.cs`, `src/Validasoft.Aplicacion/Configuracion/PlantillasNotificacionEndpoints.cs`, `src/Validasoft.Aplicacion/Expedientes/RecordatoriosDeObservaciones.cs`.

## Dominio de correo

Ruta `/e/{empresa}/dominio-correo` · permiso `empresas:administrar`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Dominio | Dominio desde el que salen los correos de la empresa; registrarlo o cambiarlo lo deja «Pendiente» con sus registros DNS. | Sin dominio: sale el remitente de la plataforma (`Validasoft:Correo:Remitente`) | Hasta 253 caracteres, minúsculas, con punto, sin `http://`, sin `@`, sin IP ni `localhost` (`dominio-invalido`). |
| Antes de la arroba | Parte local de la dirección. | `no-responder` | De 1 a 64 caracteres: letras, números, `.`, `_`, `+`, `-` (`parte-local-invalida`). |
| Nombre del remitente | Nombre que ve quien recibe el correo. | El nombre de la empresa | Hasta 80 caracteres, sin `<`, `>` ni caracteres de control. |
| Verificar ahora | Comprueba SPF, DKIM y DMARC. Si los tres están bien el correo sale desde el remitente propio; si falta alguno queda «Fallido» y el correo sigue saliendo con el remitente de la plataforma. | — | Estados: sin dominio, Pendiente de verificar, Verificado, Falta algún registro. Cada comprobación: Correcto, Distinto o No encontrado. |
| Registros DNS | Lo que la empresa debe crear: SPF (`v=spf1 include:… ~all`), DKIM (`vs1._domainkey.{dominio}`) y DMARC (`_dmarc.{dominio}`). | Los entrega el proveedor de dominios | Hoy el único proveedor es uno simulado (`Validasoft:Correo:Dominios:Proveedor`): «Verificado» no consulta un DNS real. |
| Quitar el dominio | El correo vuelve al remitente de la plataforma; los registros DNS creados no se borran. | — | 404 `dominio-sin-registrar` si no había. |

Fuente: `src/Validasoft.Dominio/Configuracion/DominioDeCorreoEmpresa.cs`, `src/Validasoft.Aplicacion/Configuracion/DominioDeCorreoEndpoints.cs`, `src/Validasoft.Infraestructura/Correo/ProveedorSimuladoDeDominios.cs`.

## Plan y uso de IA

Ruta `/e/{empresa}/uso-ia` · `configuracion:leer`. La pantalla de la empresa es de solo lectura: muestra cupo, uso, costo y si hay llave de OpenAI. El plan, el cupo, los modelos y la llave los configura **Plataforma** ([Configuración de Plataforma](configuracion-de-plataforma.md#plan-de-ia-de-una-empresa)).

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Mes | Período del consumo mostrado. | El mes actual | El actual y los 11 anteriores. |
| Qué ve la empresa | Cupo mensual, validaciones consumidas, adicionales, costo y si la llave está configurada. | — | La empresa no ve los modelos ni el umbral del plan. |

La IA analiza cada versión una sola vez como coste de plataforma y produce hechos que ven todas las empresas con compartición; cada empresa aplica después su nivel de revisión ([Revisión automática](configuracion-de-la-empresa.md#revision-automatica)). Sin plan activo con llave, la empresa no paga análisis y sus documentos se revisan a mano.

## Revisión automática

Ruta `/e/{empresa}/revision-automatica` · ver con `configuracion:leer`, guardar con `configuracion:escribir`. Rige para los documentos nuevos de todos los terceros de la empresa. Solo el nivel Automático exige plan de IA activo con llave (`ia-no-disponible`). La bandeja de lo que el sistema no decidió es otra pantalla, **Excepciones**.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Nivel | Cuánto hace el sistema por sí solo. | Manual | **Manual**: la IA analiza y solo informa. **Asistido**: sugiere «aprobar» (sin motivos) o «revisar» (con motivos) en cada documento, nunca emite veredicto: decide siempre una persona. **Automático**: aprueba solo lo que no tiene excepciones; lo demás va a Excepciones; exige plan activo con llave, al menos un tipo y al menos un área (`automatico-sin-alcance`). |
| Confianza mínima (%) | Bajo este valor el sistema no sugiere ni aprueba; el umbral efectivo es el mayor entre este y el del plan de IA. | 95 % | Entero de 85 a 100 en la pantalla. |
| Tipos de documento | Tipos canónicos que el sistema puede aprobar solo. | Ninguno | Solo tipos analizables con IA; en Automático, al menos uno. |
| Áreas cuya revisión hace el sistema | Áreas donde puede aprobar. | Ninguna | Áreas activas de la empresa; un área con varias aprobaciones se revisa como siempre. |
| Muestra para confirmar (%) | Porcentaje de lo aprobado que un miembro del área debe confirmar o revertir; se elige de forma determinista. | 10 | Entero de 0 a 100. |
| Tope mensual de aprobaciones | Al alcanzarlo, todo pasa a una persona hasta el mes siguiente (excepción `sin-presupuesto`). | Vacío: sin tope | Entero de 1 a 1 000 000. |
| Espera del análisis (minutos) | Si el análisis o el cruce con la ficha no llega a tiempo, el documento pasa a una persona. | 10 | Entero de 1 a 1440. |

Mandan siempre el documento a una persona: confianza baja, sin análisis de IA, ilegible, alertas de manipulación, tipo detectado distinto, campo crítico cambiado, cruce con la ficha pendiente, discrepancias con la ficha y versión reenviada.

Fuente: `src/Validasoft.Dominio/Revision/RevisionAutomaticaEmpresa.cs`, `src/Validasoft.Dominio/Revision/MotorDeRevision.cs`, `src/Validasoft.Aplicacion/Revision/RevisionAutomaticaEndpoints.cs`.

## Listas restrictivas

Ruta `/e/{empresa}/listas-restrictivas` · ver con `configuracion:leer`, cambiar con `configuracion:escribir`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Proveedor de la empresa | Con qué proveedor se consulta al tercero, sus representantes, suplentes, socios y PEP. Sin proveedor efectivo, las consultas quedan pendientes (`verificacion-no-disponible`). | El de la instalación (`Validasoft:Verificadores:ListasRestrictivas:ProveedorPorDefecto`) | Infolaft, Stradata, Compliance y Simulado; el simulado solo si la instalación lo permite. En el sandbox siempre se usa el simulado. |
| Cuenta de Infolaft y su cuota | Un solo contrato con la cuenta de la plataforma, para todos los clientes: la empresa no ve ni carga usuario ni clave (422 `credenciales-de-plataforma`). | Según la plataforma | Solo lectura: disponible o no, y cuota mensual de la empresa. |
| Usuario, clave, URL del contrato, plan y cuota (Stradata y Compliance) | Credenciales propias de la empresa para esos proveedores. La clave se cifra con la clave del grupo. | Sin credenciales; URL de la instalación | Usuario de 1 a 200 caracteres; clave de 4 a 512; URL `https`; cuota de 1 a 1 000 000 o vacío (`cuota-listas-agotada` al excederla). |
| Buscar por | Modo de búsqueda en el proveedor. | Por nombre | `PorNombre` o `PorDocumento`. |
| Quitar credenciales | Si era el proveedor elegido, la empresa vuelve al de la instalación. | — | — |
| Monitoreo (días) y vigencia del estudio | Cada cuántos días se reconsultan los vínculos activos y a los cuántos días vence el estudio de cumplimiento (cada flujo puede fijar la suya). | 30 y 30 | Solo lectura (instalación): monitoreo de 0 a 365 (0 lo apaga); vigencia del estudio de 1 a 3650. |

Fuente: `src/Validasoft.Dominio/Expedientes/ListasRestrictivasEmpresa.cs`, `src/Validasoft.Aplicacion/Verificaciones/CumplimientoEndpoints.cs`.

## Matriz de riesgo

Ruta `/e/{empresa}/matriz-riesgo` · ver con `configuracion:leer`, guardar con `configuracion:escribir`. Cada guardado sube la versión; los estudios ya registrados conservan su cálculo.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Calcular el riesgo con esta matriz | Activa: el nivel de riesgo y la debida diligencia salen de la matriz y el oficial solo los ajusta con una justificación de al menos 10 caracteres. Inactiva: el oficial los elige a mano. | Inactiva (hay una plantilla sugerida sin guardar) | Una matriz activa necesita al menos un factor. |
| Factores (nombre, peso, ayuda) | Cada factor pesa en el puntaje: promedio ponderado de 0 a 100. | Plantilla de 5: Jurisdicción (automático, 15), Coincidencias en listas (automático, 30), PEP (automático, 20), Sector (manual, 20) y Tipo y volumen de la operación (manual, 15) | Hasta 20 factores; nombre hasta 120 caracteres; peso de 1 a 100. Los automáticos disponibles son `jurisdiccion`, `coincidencias-listas` y `pep`. |
| Opciones (nombre, puntaje, piso) | Puntaje de cada respuesta; el piso fija un nivel mínimo aunque el puntaje sea bajo. | Ver la plantilla | De 2 a 8 por factor; puntaje de 0 a 100; piso: ninguno, mínimo Medio o mínimo Alto. |
| Límites entre niveles | Bajo si el puntaje es menor que el límite Bajo; Medio si es menor que el límite Medio; Alto desde ahí. | Bajo 34 y Medio 67 | Bajo de 1 a 99 y Medio de 2 a 100, con Bajo menor que Medio (`limites-invalidos`). |
| Debida diligencia por nivel | Intensidad que exige cada nivel. | Bajo: Simplificada; Medio: Ordinaria; Alto: Intensificada | `Simple`, `Ordinaria` o `Ampliada`; no baja al subir el riesgo; Alto siempre es Intensificada. |
| Revisar cada (meses) | Periodicidad de revisión de cada nivel. | Bajo 24, Medio 12, Alto 6 | Entero de 1 a 60. |

Fuente: `src/Validasoft.Dominio/Cumplimiento/MatrizDeRiesgo.cs`, `src/Validasoft.Aplicacion/Verificaciones/CumplimientoCompletoEndpoints.cs`.

## Habeas data

Ruta `/e/{empresa}/habeas-data` · permiso `supresiones:resolver`. No es una pantalla de ajustes: es la bandeja de solicitudes de consulta, corrección y supresión de los terceros; sus parámetros son fijos.

| Parámetro | Qué afecta | Valor | Variantes |
|---|---|---|---|
| Plazo de consulta y de reclamo | Fecha límite para responder, en días hábiles. | 10 días hábiles (consulta) y 15 (corrección o supresión) | Tipos: Consulta, Corrección y Supresión; alcance de la supresión: Todo, Documentos o Datos. |
| Aviso de plazo | «Vence pronto» con 2 días hábiles o menos y «Vencida» pasado el plazo, una vez cada uno, a los administradores y al oficial por campana y correo. | 2 días hábiles; barrido cada hora (`Validasoft:Habeas:Intervalo`) | Estados: En plazo, Vence pronto, Vencida, Atendida, Atendida con retenciones, Rechazada. |
| Respuesta al titular | Llega al titular por correo y a su portal con constancia sellada; no se cambia después. | — | Atender o Rechazar, con respuesta de 10 a 4000 caracteres; la base legal solo se agrega al rechazar. |

Fuente: `src/Validasoft.Dominio/Habeas/SolicitudHabeas.cs`, `src/Validasoft.Aplicacion/Habeas/PlazosDeHabeas.cs`.

## Áreas

Ruta `/e/{empresa}/areas` · ver con `configuracion:leer`, cambiar con `configuracion:escribir`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Código | Identificador del área; no se cambia. | Sin valor: debe configurarse | Mayúsculas, empieza por letra, `A-Z`, `0-9` y `_`, de 2 a 40 caracteres, único en la empresa (`area-duplicada`). |
| Nombre | Lo que ven los flujos y las personas. | Sin valor: debe configurarse | Obligatorio. |
| Activa | Una inactiva no recibe trabajo nuevo, no cambia de miembros (`area-inactiva`) ni entra en flujos nuevos. | Sí | Sí o no. |
| Miembros | Quiénes revisan por el área. | Ninguno | Usuarios activos con rol en la empresa o de todo el grupo (`miembros-invalidos`). La pantalla además pide `usuarios:administrar` para listar personas. |

Fuente: `src/Validasoft.Dominio/Configuracion/Area.cs`, `src/Validasoft.Aplicacion/Configuracion/ConfiguracionEndpoints.cs`.

## Flujos aprobatorios

Rutas `/e/{empresa}/flujos`, `/e/{empresa}/flujos/nuevo` y `/e/{empresa}/flujos/{flujo}` · ver con `flujos:leer`; crear, guardar borrador, publicar y marcar por defecto con `flujos:escribir`. Un flujo se versiona: Borrador, Publicada o Retirada. Publicar congela la versión y retira la anterior; los expedientes abiertos conservan la versión que copiaron.

### Datos generales

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Código, nombre y tipo de vínculo | Identifican el flujo y para qué tipo de vínculo aplica; el código y el tipo no se cambian. | Sin valor: deben configurarse | Código en mayúsculas, números y `_`, de 2 a 40 caracteres, único en la empresa (`flujo-duplicado`); solo tipos de vínculo activos. |
| Si un área observa | Cuándo vuelve el expediente al tercero tras una observación. | Inmediata | **Inmediata**: la primera observación lo devuelve y frena a las demás áreas. **AlCerrarArea**: las demás siguen revisando y se devuelve cuando todas las áreas de la etapa se pronunciaron. |
| Separación de funciones | Quien invitó al tercero o abrió el expediente no puede decidir sobre él (403 `separacion-de-funciones`). El conflicto de intereses por identificación igual a la del tercero siempre aplica. | No | Sí o no. |
| Plazo de cargue (días) | Días que tiene el tercero para completar el expediente inicial y corregir cada devolución; al vencer el expediente pasa a Caducado y la empresa puede reabrirlo. | 30 | De 0 a 365; 0 = sin plazo (`plazo-cargue-invalido`). |
| Recordatorios del plazo | Días antes de vencer en que se le recuerda al tercero. | 15, 7 y 1 | Hasta 5, cada uno entre 1 y los días menos 1; se ignoran con plazo 0. |
| Recordatorio automático de observaciones | Sobrescribe lo de la empresa para este flujo. | Lo que diga la empresa | `Empresa` (sigue a Plantillas), `Apagado` o `CadaDias` (de 1 a 60, aunque la empresa lo tenga apagado). |
| Permitir reintento tras un rechazo | El tercero rechazado ve los motivos y puede volver a postularse por su cuenta; si no, solo la empresa abre un expediente nuevo. | No | Sí o no. |
| Días de espera del reintento | Desde el rechazo. | 0 | De 0 a 365; 0 = de inmediato; solo con reintento permitido. |
| Vigencia del vínculo (días) | Días desde la última aprobación hasta que el vínculo vence. Con el primer aviso queda «Por vencer» y se abre la renovación del tercero (ratificar o actualizar las secciones que pide el flujo). | 0 (no vence por tiempo) | De 0 a 3650; con vigencia mayor que 0 el flujo debe pedir al menos una sección de la ficha (`vigencia-sin-secciones`). |
| Avisos y gracia de la vigencia | Días antes de vencer en que se avisa y días que sigue valiendo después de la fecha. | Avisos 30, 15 y 5; gracia 0 | Hasta 5 avisos entre 1 y vigencia menos 1; gracia de 0 a 365. |

### Etapas y áreas

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Etapas | Van en secuencia, en el orden de la lista; la siguiente empieza cuando todas las áreas de la anterior aprueban. | Una etapa con un área vacía | Mínimo 1; se pueden subir, bajar y quitar. |
| Área de la etapa | Las áreas de una misma etapa revisan en paralelo. | Sin elegir | Solo áreas activas; un área no se repite en el flujo (`area-repetida`). |
| Aprobadores | Personas distintas que deben aprobar en el área; con más aprobadores que miembros el área nunca se completaría. | 1 | De 1 a 10; un área automática cuenta como 1. |
| SLA (horas) | Plazo de revisión del área en este flujo; manda sobre el de [Plazos de revisión](configuracion-de-la-empresa.md#plazos-de-revision). | Vacío: usa el de la empresa | Entero mayor que 0. |
| Puede rechazar | Permite que el área rechace el expediente (terminal, con motivos y justificación). | No | Sí o no; no se combina con automática. |
| Automática | Aprueba sola al activarse la etapa (origen Sistema) y no rechaza. | No | Sí o no. |

### Documentos y secciones que pide

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Documento que pide (tipo) | Lo que pide el flujo decide qué se comparte con la empresa; sin requisitos, cada área decide sobre el expediente completo. | Ninguno | Tipo activo del grupo, sin repetir (`requisito-duplicado`). |
| Lo revisan | Áreas que revisan el requisito. | Todas las del flujo | Con requisitos, toda área debe revisar al menos uno (`area-sin-requisitos`). |
| Se pide a | Condición de exigencia: a quién se le pide. | Todos | Atajos: Todos, Solo personas jurídicas, Solo personas naturales. Atributos de una condición: `tercero.tipoPersona`, `tercero.pais`, `tercero.tipoIdentificacion`, `tercero.esExtranjero`, `vinculo.tipo` y `vinculo.atributos.{clave}`; operadores Igual, Distinto, En, NoEn, ContieneAlguno, ContieneTodos, Existe, Mayor, Menor y Entre; máximo 4 niveles y 20 comparaciones; al menos un requisito debe pedirse siempre. |
| Aprobadores del requisito | Personas que deben aprobarlo. | Los del área | De 1 a 10 o vacío. |
| Obligatorio | Los opcionales no impiden pasar a revisión; si se entregan, cuentan. | Sí | Sí o no. |
| Usar como soporte de las secciones | El documento (RUT, Cámara, Estados financieros, Cédula) se cruza con las secciones que respalda y el revisor ve las diferencias. | Activo | Sí o no. |
| Sección de la información | Pide una sección de la ficha del tercero en vez de un documento. | Sin elegir | Información general, tributaria, financiera, legal, SST y Especialidades; legal y financiera no aplican a persona natural; no se repite. |
| Periodos, exige soporte, vigencia y plazo de revalidación de la sección | Cierres financieros que se piden, si la versión debe traer soporte, días de vigencia desde que la empresa la aceptó y plazo para resolver una actualización. | 1 periodo, no, sin vigencia, 15 días | Periodos de 1 a 5; vigencia de 1 a 3650; revalidación de 1 a 365. |

### Verificaciones de cumplimiento

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Listas restrictivas | Se consultan solas al pasar a revisión, con el proveedor de la empresa. | No se pide | Una por tipo (`verificacion-duplicada`). |
| Estudio del oficial (SAGRILAFT) | Nivel de riesgo, conflicto de interés, debida diligencia y concepto del oficial; vence y vuelve a pedirse. | No se pide | Una por tipo. |
| Bloquea la aprobación | Una verificación bloqueante pendiente, con coincidencias por analizar, fallida o vencida impide aprobar aunque todas las áreas hayan aprobado. | Sí | Sí o no. |
| Vigencia del estudio y reconsulta de listas | Vigencia del resultado y cada cuántos meses se reconsultan las listas. | Estudio 30 días; reconsulta 12 meses | Vigencia de 1 a 3650 o vacío; reconsulta de 1 a 60 o vacío (solo listas). |

### Versiones y flujo por defecto

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Crear, guardar borrador y publicar | «Editar» sobre una versión publicada crea el borrador n+1; publicar vuelve a validar áreas y tipos. | — | Estados: Borrador, Publicada, Retirada; `sin-borrador` si no hay nada que publicar. |
| Flujo por defecto por tipo de vínculo | Flujo que se usa cuando el vínculo no trae uno propio. Sin flujo publicado, quienes aceptan la invitación quedan en «aperturas pendientes». Al publicar el primero de un tipo, queda por defecto. | Ninguno hasta publicar el primero | Solo flujos publicados del mismo tipo. |

> **Nota:** «aprobar en nombre del área» no es un ajuste del flujo sino una acción del administrador sobre un expediente (permiso `expedientes:administrar`, con justificación y solo si el área está Pendiente).

Fuente: `src/Validasoft.Dominio/Flujos/Flujo.cs`, `src/Validasoft.Dominio/Flujos/CondicionDeExigencia.cs`, `src/Validasoft.Aplicacion/Flujos/FlujosEndpoints.cs`, `apps/web/src/features/flujos/editor.ts`.

## Plazos de revisión

Ruta `/e/{empresa}/plazos-de-revision` · ver con `configuracion:leer`, guardar con `configuracion:escribir`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Avisar al consumir (%) | Quienes revisan el área reciben el aviso al consumirse ese porcentaje del plazo de la ronda. | 80 | Entero de 50 a 95 (`porcentaje-invalido`). |
| Avisar a los administradores cuando se incumple | Aviso por campana y correo cuando vence el plazo de un área. | Sí | Sí o no. |
| Plazo por defecto de cada área (horas) | Plazo de revisión cuando el flujo no pone uno. Se copia al expediente al abrirse: solo rige para expedientes nuevos. El del flujo manda. | Sin plazo (sin avisos) | De 1 a 2160 horas (90 días) o vacío. |

Fuente: `src/Validasoft.Aplicacion/Expedientes/SlaDeRevision.cs`, `src/Validasoft.Dominio/Expedientes/ExpedienteSla.cs`.

## Ayuda de documentos

Ruta `/e/{empresa}/ayuda-de-documentos` · ver con `configuracion:leer`, guardar con `configuracion:escribir`. La lista incluye solo tipos de documento activos.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Ayuda de la empresa (por tipo de documento) | Texto que lee el tercero al entregar ese documento, solo para los terceros de esta empresa; sobrescribe la del grupo. | Sin ayuda propia: el tercero ve la del grupo (o ninguna) | Hasta 2000 caracteres; vacío vuelve a la del grupo. Filtro de la lista: Propia, Del grupo, Sin ayuda. |
| Ayuda del grupo | La define el grupo en Tipos de documento. | — | Solo lectura aquí. |

Fuente: `src/Validasoft.Aplicacion/Documentos/AyudasDeDocumentoPorEmpresa.cs`.

## Delegación de revisión

Ruta `/e/{empresa}/delegaciones` · permiso `expedientes:revisar`. Administrador, Revisor y Oficial pueden delegar sus propias áreas; quien tiene `expedientes:administrar` ve todas y puede delegar por otra persona (403 `delegacion-ajena` si no).

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| En nombre de | Quién delega (solo el administrador puede elegir a otra persona). | Yo | Persona activa con rol en la empresa. |
| Quién revisa en su lugar | La persona delegada ve la bandeja y recibe los avisos de quien delega; la separación de funciones y el conflicto de intereses se le siguen aplicando. No se encadena. | Sin elegir | Persona activa con permiso de revisar; no a sí mismo (`delegacion-a-si-mismo`). |
| Desde y hasta | Fechas inclusivas de la delegación. | Desde hoy; hasta sin valor | Hasta no antes de hoy; desde no posterior a hasta; máximo 366 días; sin cruzarse con otra igual (`delegacion-solapada`). |
| Áreas que delegas | Qué áreas se delegan. | Vacío = todas las áreas activas de las que es miembro, incluidas las que le asignen luego | Solo áreas activas propias. |
| Motivo | Queda en el historial. | Vacío | Hasta 300 caracteres. |
| Revocar | La persona delegada deja de revisar desde ese momento. | — | Solo Programada o Vigente. Estados: Programada, Vigente, Vencida y Revocada. |

Fuente: `src/Validasoft.Dominio/Delegaciones/DelegacionDeRevision.cs`, `src/Validasoft.Aplicacion/Delegaciones/DelegacionesDeRevision.cs`.
