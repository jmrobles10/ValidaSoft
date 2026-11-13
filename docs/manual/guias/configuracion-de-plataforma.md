---
titulo: "Configuración de Plataforma"
seccion: "Configuración y glosario"
resumen: "La consola /plataforma y los ajustes globales de la instalación: grupos, planes, IA, listas restrictivas, operadores, secretos y soporte, con valores por defecto y variantes."
---

# Configuración de Plataforma

La consola /plataforma y los ajustes globales de la instalación: grupos, planes, IA, listas restrictivas, operadores, secretos y soporte, con valores por defecto y variantes.

**Plataforma** es quien opera la instalación de Validasoft: da de alta grupos y empresas, fija planes y límites, configura la IA y la cuenta de listas restrictivas, administra operadores y secretos. Sus pantallas viven en `/plataforma` de la app web (`/app/#/plataforma/…`) y solo las ven los operadores de Plataforma. Los ajustes que cambian por despliegue y no desde la consola están en [Variables de entorno](variables-de-entorno.md).

## Reglas comunes de la consola

| Tema | Hallazgo |
|---|---|
| Acceso | Solo con una sesión sin grupo, actor Plataforma y permiso `plataforma:tenants`; con otra sesión redirige a `/`. Pestañas: Grupos y empresas, Habeas data, Uso de IA, Operadores, Ingresos, Secretos y Listas restrictivas. «Mi cuenta» está en el menú de la cuenta. |
| Permiso de los endpoints `/api/v1/plataforma/*` | `plataforma:tenants`: la sesión de un operador o la clave de arranque (`X-Api-Key`, actor `plataforma-bootstrap`). |
| Cómo entra un operador | **Directo**: correo, código y passkey obligatoria; sesión de 8 horas, inactividad de 30 minutos y token de acceso de 15 minutos (`Validasoft:Ingreso:Plataforma:SesionHoras`, `InactividadMinutos` y `Validasoft:Ingreso:MinutosAcceso`; no se editan desde la consola). **Delegado**: el backoffice canjea la aserción en `POST /plataforma/sesiones` con la clave de arranque y recibe un token de 30 minutos con solo `plataforma:tenants`. |
| Confirmar con passkey | Vale 5 minutos y solo aplica a quien entró directo; no aplica a operadores por identidad delegada, a la clave de arranque ni a API keys. Sin confirmación reciente: 403 `reautenticacion-requerida`, y la web abre el diálogo y repite la petición una vez. Las acciones están en [Acciones que piden passkey](configuracion-de-plataforma.md#acciones-que-piden-passkey). |
| Auditoría | Los cambios de plan, tarifa, plan de IA, operadores, sistemas, clave de arranque y listas quedan auditados; las llaves y claves nunca se guardan. |

Fuente: `src/Validasoft.Aplicacion/Plataforma/ConsolaEndpoints.cs`, `src/Validasoft.Infraestructura/Seguridad/IdentidadDelegada.cs`, `apps/web/src/features/plataforma/ShellPlataforma.tsx`.

## Grupos y empresas

Ruta `/plataforma/grupos`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Buscar por grupo, empresa o NIT | `q` busca en el nombre y el slug del grupo y en la razón social y el NIT de sus empresas (acepta puntos, espacios y dígito de verificación). | Sin filtro | Espera de 300 ms; desplazamiento infinito de 20 en 20 (la API admite de 1 a 100), por nombre y luego por id. Los sandbox no aparecen. |
| Tarjeta de grupo | Nombre, slug, «Inactivo», número de empresas y fecha de creación; con más de 5 empresas la tabla va plegada. | — | Estados del plan de IA de cada empresa: Sin plan, Plan inactivo, Activo sin llave y Activo con llave. |
| Plan y límites | Lleva a la pantalla del plan del grupo. | — | — |
| Sistemas | Lista los sistemas integrados del grupo. Desactivar uno revoca todas sus API keys (también las de prueba), corta sus sesiones y es inmediato entre servidores; reactivarlo no restaura las llaves. | — | Piden passkey; 409 `sistema-inactivo` si ya estaba desactivado. |
| Agregar empresa | `POST /plataforma/tenants/{id}/empresas` con `Idempotency-Key`. No consulta el límite de empresas del plan. | — | 404 `tenant-no-encontrado`; 409 `grupo-inactivo`, `empresa-duplicada` y `prefijo-folio-duplicado`; 422 `prefijo-folio-invalido` y `nombre-requerido`. Sin passkey. |

Fuente: `src/Validasoft.Aplicacion/Plataforma/PlataformaEndpoints.cs`, `apps/web/src/features/plataforma/PaginaGrupos.tsx`.

## Nuevo grupo

Ruta `/plataforma/grupos/nuevo` · `POST /plataforma/tenants` con `Idempotency-Key`. Todo se valida antes de escribir y se guarda en una sola transacción. Sin passkey.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Nombre del grupo | Nombre visible. | Sin valor: debe configurarse | Obligatorio. |
| Identificador (slug) | Forma parte de las API keys; es inmutable. | Se deriva del nombre | De 3 a 30 caracteres: minúsculas, números y guiones (409 `slug-duplicado`). |
| Zona horaria | Calendario del negocio (vencimientos, «hoy»). La región es fija (`co`) y el idioma `es-CO`. | `America/Bogota` | Debe ser una zona IANA (`zona-horaria-invalida`). |
| Primera empresa: país, tipo, número, prefijo de folio y razón social | La primera razón social del grupo. | Sin valor: debe configurarse | Prefijo de 2 a 10 caracteres `A-Z0-9`, único en el grupo; el número se valida según el tipo de identificación. |
| Administrador: nombre, correo y usuario en el sistema (opcional) | Rol Administrador de todo el grupo; con ingreso directo recibe un correo de invitación. | Sin valor: debe configurarse | Un id externo sin sistema: 422 `sistema-requerido`. |
| Cómo entran sus personas | Desde el sistema integrado (Delegado) y/o Directo a Validasoft. | La pantalla propone Delegado; la API sin `ingreso` crea solo Delegado | Al menos uno. |
| Política de sesión del grupo | Duración de la sesión, inactividad y passkey para todos; luego se cambian en Configuración › Ingreso. | 8 horas, 30 minutos, no | Sesión de 1 a 24 horas; inactividad de 5 a 480 minutos y no más que la sesión. |
| Sistema integrado (solo con Delegado): código, nombre, clave pública y sitios | El sistema que autentica a las personas. | Sin valor: debe configurarse | Código `A-Z`, `0-9` y `_` (2 a 20), que es el `iss` de las aserciones; clave RSA de 2048 bits o más, o EC P-256; hasta 20 orígenes `https://host[:puerto]`. |
| Resultado | Muestra **una sola vez** dos API keys: la de administración y la del sistema, ambas con todos los permisos del grupo, empresas `*` y sin expiración. También siembra tipos de vínculo y de documento. | — | Sin documento de plan el grupo nace sin límites, con la etiqueta «Inicial». |

Fuente: `src/Validasoft.Aplicacion/Plataforma/PlataformaEndpoints.cs`, `src/Validasoft.Dominio/Plataforma/Plataforma.cs`, `apps/web/src/features/plataforma/PaginaNuevoGrupo.tsx`.

## Plan de IA de una empresa

Ruta `/plataforma/grupos/{grupo}/empresas/{empresa}/plan-ia` · `PUT …/plan-ia`. Sin passkey, ni siquiera al cambiar la llave de OpenAI.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Validaciones incluidas por mes | Cupo mensual de análisis con IA de la empresa. Avisos al 80 % y al 100 % a sus administradores (campana y correo). | 0 | Entero de 0 a 1 000 000. |
| Permitir validaciones adicionales | Agotado el cupo, se sigue validando y se cobra aparte; apagado, los documentos pasan a revisión manual. | No | Sí o no. |
| Tope de adicionales por mes | Máximo de adicionales. | Vacío: sin tope | De 0 a 1 000 000 o vacío. |
| Precio propio por adicional | Sobrescribe la tarifa general para esta empresa. | Vacío: tarifa general | De 0 a 100 000 000 o vacío. |
| Modelo por defecto y modelo de escalamiento | Modelo del primer análisis y del segundo; solo escala si es distinto del primero. | `gpt-6-luna` y `gpt-6.1-terra` | Letras, números y `. _ : -`, hasta 100 caracteres. |
| Umbral de confianza | Se escala si la confianza queda bajo el umbral, el documento es ilegible, hay alertas de manipulación o cambia un campo crítico. También es el piso de la confianza mínima de la revisión automática de la empresa. | 0,85 | De 0 a 1. |
| Plan activo | Inactivo: la empresa no paga análisis y sus documentos se revisan a mano. Pagar requiere plan activo y llave. | Inactivo en la API; la pantalla propone «activo» cuando no hay plan | Sí o no. |
| API key de OpenAI | Paga los análisis de la empresa. Es de solo escritura: se cifra con la clave maestra y solo vuelve «Configurada · ••••XXXX». | Sin llave | De 20 a 512 caracteres sin espacios; sin valor conserva, vacía quita, un valor reemplaza. |
| Probar llave | Consulta `GET {UrlBase}/models` sin consumir tokens (15 s). | — | Responde `valida`, `invalida`, `limite`, `sin-llave` o `error`. |

Fuente: `src/Validasoft.Aplicacion/Ia/IaEndpoints.cs`, `src/Validasoft.Dominio/Ia/PlanDeIaEmpresa.cs`, `src/Validasoft.Dominio/Ia/UsoDeIa.cs`.

## Plan y límites del grupo

Ruta `/plataforma/grupos/{grupo}/plan` · `PUT /plataforma/tenants/{id}/plan`. Sin passkey. El grupo ve su plan y su uso en `GET /tenant/plan` y `GET /tenant/uso`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Código del plan | Etiqueta que se copia al grupo; no habilita funciones. | «Inicial»; sin documento, sin límites | De 2 a 40 caracteres. |
| Límites: usuarios, empresas y vínculos activos | Máximos por grupo. | Sin límite | Vacío = sin límite; **0 = no se permite nada**. |
| Almacenamiento (MB) y llamadas a la API por mes | Límites de documentos compartidos con el grupo y de peticiones del mes. | Sin límite | Hasta 1 000 000 000 000. |
| Bloquear al llegar al límite (bloqueo suave) | Ver la tabla siguiente. | No: solo avisa | Sí o no. |

| Métrica | Qué cuenta |
|---|---|
| `Usuarios` | Usuarios activos. |
| `Empresas` | Empresas activas del grupo. |
| `VinculosActivos` | Vínculos que no están Anulados ni Desvinculados. |
| `Almacenamiento` | Bytes de documentos de la bóveda compartidos con el grupo (cada objeto una vez, sin versiones suprimidas); se mide cada 5 minutos (`Validasoft:Plan:MinutosMedicionAlmacenamiento`, de 0 a 1440). |
| `LlamadasApi` | Peticiones `/api/v1` con API key de producción en el mes calendario del negocio, lecturas y escrituras; no cuentan las sesiones de pantalla, el sandbox ni Plataforma; se vuelcan cada 5 segundos. |

| Concepto | Comportamiento |
|---|---|
| Niveles | Normal: menos del 80 % o sin límite. Por agotar: del 80 % a menos del 100 %. Agotado: desde el límite. Con límite 0 el porcentaje es 100. |
| Con bloqueo suave y el límite alcanzado | 409 `limite-del-plan-alcanzado` al crear un usuario, una empresa (del grupo), un vínculo (API y lotes) y al cargar un documento propio de un vínculo; las escrituras con API key responden 429. Las lecturas y lo existente no se bloquean. |
| Lo que el bloqueo no cubre | Las empresas creadas por Plataforma y los usuarios aprovisionados por identidad delegada o SSO. |
| Avisos | Cada 15 minutos (`Validasoft:Plan:Intervalo`), al 80 % y al 100 % por métrica, uno por umbral y periodo, a los administradores por campana y correo; guardar el plan reinicia los avisos enviados. `POST /plataforma/planes/revisar` revisa de inmediato. |
| Derechos que habilita | Ninguno: el plan solo mide y limita estas cinco métricas. El dominio de correo y la marca no dependen del plan. |

Fuente: `src/Validasoft.Dominio/Plataforma/PlanDelGrupo.cs`, `src/Validasoft.Aplicacion/Plataforma/PlanDelGrupoEndpoints.cs`, `src/Validasoft.Infraestructura/Http/Http.cs`.

## Uso de IA y tarifa

Ruta `/plataforma/uso-ia`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Mes | Período del consumo por grupo y empresa (incluidas y adicionales, tokens, costo en USD, valor de lo adicional); exportable a Excel o CSV. | El mes actual | `AAAA-MM`; la pantalla ofrece 12 meses. |
| Tarifa general: precio | Vale para las empresas sin precio propio. | 0: lo adicional no se valora | De 0 a 100 000 000. Sin passkey. |
| Tarifa general: moneda | Moneda de la tarifa. | `COP` | Código ISO de 3 letras. |

Fuente: `src/Validasoft.Aplicacion/Ia/ConsumoDeIaEndpoints.cs`, `src/Validasoft.Dominio/Ia/TarifaDeIa.cs`.

## Habeas data de la plataforma

Ruta `/plataforma/habeas` · solicitudes de terceros dirigidas a la plataforma.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Filtro de estado | Qué solicitudes se listan. | Abiertas | Abiertas, Vencidas o Resueltas (la API admite también `todas`). |
| Plazos | 10 días hábiles para consulta y 15 para reclamo; aviso «vence pronto» a 2 días hábiles, a los correos de `Validasoft:Habeas:CorreosPlataforma`. | 10, 15 y 2 | Revisión horaria (`Validasoft:Habeas:Intervalo`). |
| Responder: atender o rechazar | Respuesta de 10 a 4000 caracteres; la base legal es opcional y solo se agrega al rechazar. Sin passkey. | — | Atender una supresión «Todo» ejecuta el plan: elimina lo no referenciado, conserva lo exigido por ley y anonimiza; es irreversible. |

Fuente: `src/Validasoft.Aplicacion/Habeas/SolicitudesHabeasEndpoints.cs`, `src/Validasoft.Dominio/Habeas/SolicitudHabeas.cs`.

## Operadores

Ruta `/plataforma/operadores`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Nuevo operador (nombre y correo) | Alta de un operador directo, que recibe un correo para entrar. El primero se da de alta con la clave de arranque. | — | 409 `operador-duplicado`; 422 `correo-invalido`. Pide passkey (`crear-operador`). |
| Desactivar | Cierra sesiones, revoca passkeys e invalida el token; nadie puede desactivarse a sí mismo (409 `auto-desactivacion`). | — | Pide passkey. |
| Reactivar | No restaura nada: entra con código y registra una passkey nueva. | — | Pide passkey. |
| Acceso (passkeys y sesiones) | Revocar una passkey o cerrar una sesión. | — | Revocar pide passkey; cerrar sesión no. |

Fuente: `src/Validasoft.Aplicacion/Plataforma/OperadoresDirectosEndpoints.cs`, `src/Validasoft.Dominio/Identidad/OperadorDePlataforma.cs`.

## Ingresos de los operadores

Ruta `/plataforma/ingresos` · solo lectura (`GET /plataforma/auditoria-ingresos`): filtros por operador, evento (los 14 de `EventoDeIngreso`), resultado y fechas; 50 por página (máximo 200), más reciente primero.

Fuente: `src/Validasoft.Aplicacion/Plataforma/PlataformaIdentidadEndpoints.cs`.

## Secretos y clave de arranque

Ruta `/plataforma/secretos`.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Clave maestra y claves anteriores | Solo lectura: muestra el `kid` (8 hex). Se cambian en el servidor (`Validasoft:Secretos:ClaveMaestra`, `ClavesAnteriores`, `ClavesFirmaHeredadas`). Sin clave válida la API no arranca. | Sin valor: debe configurarse | — |
| Re-cifrar con la clave vigente | Re-cifra llaves de OpenAI, secretos de webhook, números de cuenta y credenciales de listas; es idempotente y lo ilegible queda como «Ilegibles». Sin passkey. | — | El botón se deshabilita con 0 pendientes. |
| Clave de arranque: desactivar y reactivar | Único dato del documento `ConfiguracionDePlataforma`. Desactivar exige al menos un operador **directo** con passkey (409 `sin-operadores-con-passkey`) y vale en todos los servidores (caché de 15 s). | Activa | Reactivar sirve solo si el servidor aún tiene la clave (al menos 32 caracteres). Ambas piden passkey. |

> **Ojo:** desactivar la clave de arranque también impide emitir sesiones a los operadores del backoffice (`POST /plataforma/sesiones` la exige) y la entrada de Plataforma con `X-Acceso-Soporte`; la pantalla dice que los operadores «siguen entrando con correo, código y passkey», lo cual vale solo para los directos.

Fuente: `src/Validasoft.Aplicacion/Plataforma/ConsolaEndpoints.cs`, `src/Validasoft.Dominio/Plataforma/ConfiguracionDePlataforma.cs`.

## Listas restrictivas de la plataforma

Ruta `/plataforma/listas` · `PUT /plataforma/listas-restrictivas`. Infolaft es **uno solo para todos los clientes, con la cuenta de la plataforma**; Stradata y Compliance siguen configurándose por empresa.

| Ajuste | Qué afecta | Por defecto | Variantes y límites |
|---|---|---|---|
| Usuario de la cuenta | Usuario del contrato de Infolaft. | Sin valor: debe configurarse | De 1 a 200 caracteres. |
| Clave | Solo escritura: cifrada con la clave maestra; vuelve «Configurada · …1234». | Sin cuenta | De 4 a 512 caracteres; vacío conserva; «Quitar la cuenta» envía vacío. |
| URL del contrato y plan contratado | Se usa en lugar de la URL de la instalación; el plan es informativo. | URL de la instalación | URL `https`; plan hasta 100 caracteres. |
| Cuota mensual de cada empresa | Búsquedas al mes de cada empresa; al llegar, no consulta más ese mes. | Vacío: sin tope | De 1 a 1 000 000. Es una sola para todas. |
| Buscar por | Modo de búsqueda. | `PorNombre` | `PorNombre` o `PorDocumento`. |
| Guardar, quitar la cuenta y migrar | Cambiar o quitar la cuenta; «Migrar» toma la primera credencial legible de una empresa como cuenta de la plataforma (si no hay una) y borra las propias de Infolaft de todas, sin heredar cuotas. | — | Las tres piden passkey. |
| No habilitado en esta instalación | Depende de `Validasoft:Verificadores:ListasRestrictivas:Proveedores:Infolaft:Habilitado`, que no se cambia desde la consola. | `false` | — |

Fuente: `src/Validasoft.Aplicacion/Plataforma/ListasDePlataformaEndpoints.cs`, `src/Validasoft.Dominio/Expedientes/ListasRestrictivasEmpresa.cs`.

## Mi cuenta

Ruta `/plataforma/mi-cuenta` · perfil, passkeys, códigos de recuperación y sesiones del operador; solo con ingreso directo (403 `solo-ingreso-directo`). El operador exige al menos una passkey (409 `passkey-obligatoria` al eliminar la única) y admite hasta 10 (409 `passkeys-excesivas`). Generar los diez códigos de recuperación pide passkey.

## Ajustes solo por API

| Ajuste | Dónde y qué hace | Variantes y límites |
|---|---|---|
| Sistemas de plataforma (backoffice) | `PUT /plataforma/sistemas/{codigo}` y `GET /plataforma/sistemas`. Reemplazar la clave invalida al instante las aserciones anteriores. | Código `A-Z`, `0-9` y `_` (2 a 20); clave RSA de 2048 bits o más, o EC P-256; hasta 20 orígenes. Fijarlo no pide passkey; desactivar y reactivar sí. |
| Aserción del operador | JWT RS256 o ES256 en `X-Usuario-Delegado`: `iss` = código del sistema, `aud` = `validasoft`, `exp` de 5 minutos o menos, `jti` único y de un solo uso, `sub`, `name` y `email` obligatorios. Se canjea en `POST /plataforma/sesiones`. | Errores 401 `asercion-firma-invalida`, `asercion-vencida`, `asercion-vigencia-excesiva`, `asercion-sin-jti`, `asercion-incompleta`, `sistema-inactivo` y `operador-inactivo`. |
| Sandbox del grupo | No se gestiona desde Plataforma: lo crea o reinicia el administrador del grupo o una llave de prueba con `sandbox:administrar`. Es un grupo hermano que copia solo la configuración y no aparece en la consola ni en el consumo. | Ver la [guía de integración](guia-de-integracion.md#sandbox). |
| Accesos de soporte (lado Plataforma) | El administrador del grupo los concede; Plataforma solo lista los vigentes (`GET /plataforma/accesos-soporte`) y entra con la clave de arranque más `X-Acceso-Soporte`. Solo lectura, nunca documentos. | Sin pantalla en la consola. |
| Aviso de privacidad de la plataforma | `PUT` y `GET /plataforma/textos-legales/aviso`; cada publicación es una versión. | Se siembra la versión 1 (autorización de tratamiento de datos de ejemplo, Ley 1581 de 2012); cada operación puede publicar la suya. |
| Política de ingreso de un grupo | `PUT /plataforma/tenants/{id}/ingreso`; quitar el modo directo cierra las sesiones directas del grupo. | Mismos rangos que el alta del grupo. |
| Desactivar el SSO de un grupo | `POST /plataforma/tenants/{id}/sso/desactivar`: válvula de emergencia. | 409 `sso-inactivo`; pide passkey. |
| Barridos manuales | `POST /plataforma/{planes, verificaciones, vigencias-de-vinculos, sla, plazos-de-cargue, recordatorios-observaciones, habeas}/revisar`, `fichas/recalcular`, `catalogos/{c}/importar` e `importacion/{origen}`. | Operativos. |

Fuente: `src/Validasoft.Aplicacion/Plataforma/ConsolaEndpoints.cs`, `src/Validasoft.Aplicacion/Sandbox/SandboxEndpoints.cs`, `src/Validasoft.Aplicacion/Configuracion/AccesosSoporteEndpoints.cs`.

## Acciones que piden passkey

| Acción | Endpoint | Dónde se ve |
|---|---|---|
| `crear-operador`, `desactivar-operador`, `reactivar-operador` | `/plataforma/operadores…` | Operadores |
| `revocar-passkey` | `/plataforma/operadores/{id}/passkeys/{p}/revocar` | Operadores › Acceso |
| `desactivar-sistema`, `reactivar-sistema` | `/plataforma/tenants/{t}/sistemas/{id}/…` | Grupos › Sistemas |
| `desactivar-sistema-de-plataforma`, `reactivar-sistema-de-plataforma` | `/plataforma/sistemas/{código}/…` | Solo API |
| `desactivar-clave-de-arranque`, `reactivar-clave-de-arranque` | `/plataforma/clave-de-arranque/…` | Secretos |
| `configurar-listas-restrictivas`, `migrar-credenciales-de-listas` | `/plataforma/listas-restrictivas…` | Listas |
| `desactivar-sso` | `/plataforma/tenants/{id}/sso/desactivar` | Solo API |
| `codigos-de-recuperacion` | `/mi-cuenta/codigos-de-recuperacion` | Mi cuenta |

Del lado del grupo también piden passkey: `crear-api-key`, `revocar-api-key`, `rotar-api-key`, `rotar-clave-del-sistema`, `cambiar-roles`, `desactivar-usuario`, `revocar-passkey` y `configurar-sso`. **No** piden passkey en Plataforma: crear grupo, agregar empresa, plan del grupo, plan de IA y llave, tarifa, re-cifrar secretos, responder habeas (incluida la supresión irreversible), fijar un sistema de plataforma, la política de ingreso y el aviso de privacidad.

Fuente: `src/Validasoft.Aplicacion/Plataforma/PlataformaIdentidadEndpoints.cs`, `src/Validasoft.Aplicacion/Plataforma/OperadoresDirectosEndpoints.cs`, `apps/web/src/i18n/es-CO-seguridad.ts`.
