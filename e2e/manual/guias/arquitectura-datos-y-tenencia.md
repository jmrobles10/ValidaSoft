---
titulo: "Datos, multi-tenencia y event sourcing"
seccion: "Arquitectura"
orden: 12
resumen: "Cómo se aíslan los grupos y las empresas, qué es global y qué es privado, qué agregados guardan eventos y cómo se leen."
---

Toda la información vive en una sola base PostgreSQL 17 y se guarda con **Marten**: documentos JSON para la configuración y las lecturas, y **eventos** (event sourcing) para los agregados del negocio. No hay ORM relacional ni migraciones por tabla: el esquema lo gestiona Marten (decisión ADR-0002 y ADR-0004).

## Tres ámbitos de datos

![Multi-tenencia: la bóveda del tercero es global, cada grupo es un tenant y dentro de él cada empresa es privada](diagramas/tenencia.svg)

| Ámbito | Qué guarda | Cómo se aísla |
|---|---|---|
| **Plataforma e identidad (global)** | Grupos y empresas, planes, catálogos de la ficha, textos legales, configuración de la instalación, operadores, sistemas de plataforma, enlaces de acceso del tercero, passkeys, sesiones de las personas y el registro de ingresos. | No pertenece a ningún grupo: el ingreso empieza sin saber de qué grupo es la persona. Solo lo toca Plataforma o el flujo de ingreso. |
| **Bóveda del tercero (global)** | El tercero (clave única por país, tipo y número), sus documentos y versiones, la ficha maestra, sus cuentas bancarias, sus solicitudes de habeas data y la llave de datos con la que se cifra lo personal. | Tenant técnico `boveda`. Una empresa solo lee una versión si hay una compartición hacia uno de sus vínculos, que se crea únicamente cuando el tercero acepta la invitación (ADR-0009). |
| **Por grupo (tenant)** | Vínculos, expedientes, certificados, flujos, áreas, tipos, motivos, usuarios, API keys, webhooks, plantillas, plan de IA, listas, lotes y campañas. | Cada fila y cada evento lleva el `tenant_id` del grupo (estilo *conjoined* de Marten); toda sesión se abre con el tenant de la credencial. Un recurso de otro grupo responde 404. |

**Un grupo es un tenant; una empresa es una razón social dentro de él.** `empresa_id` no es una dimensión de tenancy de Marten sino una columna indexada en cada documento y evento que es privado de una empresa (vínculo, expediente, decisión, certificado, bloqueo, bandeja…). Marten no tiene filtros globales, así que toda lectura de esos datos pasa por la `SesionDeEmpresa`, que añade el predicado de la empresa activa, y todo recurso cargado se comprueba contra ella: uno de otra empresa responde **404**, nunca 403, para no revelar que existe.

### Cómo se resuelve el tenant y la empresa de cada petición

1. La credencial fija el **grupo**: una API key trae su grupo, una sesión lo lleva en el token. Una sesión del tercero no tiene grupo.
2. La **empresa activa** llega en la cabecera `X-Empresa` (si la credencial tiene una sola empresa no hace falta). Se valida contra el grupo y contra el alcance de la credencial; si no, 404 `empresa-no-encontrada`.
3. Los permisos de los roles asignados a una empresa solo se suman cuando esa es la empresa activa.
4. Cada llamada a `/api/v1` con una API key de producción cuenta para el límite mensual del plan del grupo; el sandbox y las sesiones de pantalla no cuentan.

### El sandbox es otro grupo

El sandbox de un grupo es un **grupo hermano** creado a pedido: copia solo la configuración (con otros ids de empresa) y ningún dato. Sus terceros, vínculos y certificados están aislados de los reales y sus certificados no se verifican en público. Se describe en la [guía de integración](guia:guia-de-integracion#sandbox).

## Esquemas de PostgreSQL

Un solo `DocumentStore`, con los eventos en estilo *conjoined* para todos los streams, de modo que un comando que toca la bóveda y un grupo sigue siendo **una transacción**.

| Esquema | Contenido |
|---|---|
| `plataforma` | Grupos, empresas, planes y uso, tarifa de IA, catálogos de la ficha, textos legales, corridas del importador. |
| `boveda` | Terceros, documentos y versiones (streams `Documento-{id}`), cuentas bancarias, secciones de la ficha y su estado, solicitudes de habeas data, referencias externas de la plataforma. |
| `validacion` (esquema por defecto) | Todo lo privado de un grupo: vínculos, expedientes, certificados, flujos, áreas, webhooks, notificaciones, lotes y campañas. |
| `identidad` | API keys, sistemas integrados, usuarios, sesiones, passkeys, desafíos de ingreso, SSO, idempotencia y accesos de soporte. |
| `eventos` | El event store. |
| `wolverine` | Outbox, inbox y mensajes programados de Wolverine. |

Hay índices únicos que hacen cumplir las reglas del negocio: un tercero por identificación, un vínculo por empresa, tercero y tipo, un folio de expediente por grupo y un prefijo de folio distinto por empresa.

## Event sourcing: agregados y eventos

Un **agregado** es un stream de eventos (con identificador `Guid`) cuya foto actual se calcula al escribir y se guarda como un documento (proyección *snapshot* en línea): la foto es la lectura que usan las listas y los detalles, y los eventos son la verdad y la auditoría. Los eventos nunca se reescriben.

| Agregado | Dónde vive | Eventos principales |
|---|---|---|
| `Vinculo` | Grupo | `VinculoCreado`, `InvitacionEnviada`, `ConsentimientosRegistrados`, `InvitacionAceptada`, `ExpedienteVinculado`, `ExpedienteCerrado`, `VinculoActivado`, `VinculoPorVencer`, `VinculoVencido`, `VinculoBloqueado`, `VinculoDesbloqueado`, `VinculoDesvinculado`, `VinculoReintegrado`, `VinculoDesaprobado`, `VinculoAnulado`, cuenta designada y contactos. |
| `Expediente` | Grupo | `ExpedienteAbierto`, `FlujoSeleccionado`, `RequisitosCalculados`, `VersionAdjuntadaARequisito`, `ExpedienteEnviadoARevision`, `EtapaActivada`, `VeredictoDeRequisitoEmitido`, `VeredictoDeAreaEmitido`, `ExpedienteObservado`, `ExpedienteReenviado`, `EtapaAprobada`, `ExpedienteAprobado`, `ExpedienteRechazado`, `ExpedienteAnulado`, `ExpedienteCaducado`, `ExpedienteReabierto`, `CuentaDesignadaRequerida`, `CuentaDesignadaConfirmada`, verificaciones y estudios de cumplimiento. |
| `Documento` | Bóveda | `DocumentoCreado`, `VersionCargada`, `AnalisisDeVersionIniciado`, `HechosDeVersionRegistrados`, `VersionActualCambiada`, `VersionDevueltaAlTercero`, `VersionCompartida`, `VersionRatificada`, `VersionVencida`, `VersionRetirada`, `AnalisisDeIaRegistrado`, `DocumentoAccedido`. |
| `Certificado` | Grupo | `CertificadoEmitido`, `CertificadoReemplazado`, `CertificadoConPendientes`, `PendientesDeRevalidacionResueltos`, `PlazoDeRevalidacionVencido`, `CertificadoSuspendido`, `CertificadoRestablecido`, `CertificadoRevocado`, `CertificadoVencido`, `PdfDeCertificadoGenerado`, `CertificadoDescargado`. |
| `SeccionDeFicha` | Bóveda | Una versión por cambio de cada sección de la ficha del tercero. |
| `LoteDeInvitacion` y `CampaniaDeRenovacion` | Grupo | El avance de cada lote o campaña; sus filas son documentos aparte. |
| `SolicitudHabeas` | Bóveda | El ciclo de una solicitud del titular. |
| `SecuenciaDeFolios` | Grupo | Los folios consumidos: se bloquea el stream al consumir uno, así no hay saltos ni repetidos. |

Cada evento guarda en sus metadatos quién lo causó (`actorTipo`, `actorId`, `sistema`) y la correlación (`X-Correlation-Id`), no en su cuerpo. El `ETag` de una lectura es la versión del stream y vuelve en `If-Match` para escribir con concurrencia optimista.

### Lecturas y proyecciones

Hay dos clases de lectura que cruzan agregados. La **bandeja de revisión** (una fila por expediente y área, `FilaBandeja`) y las filas de los lotes y campañas no son proyecciones asíncronas: son documentos de Marten que el propio caso de uso escribe **en la misma transacción** que los eventos, a partir del estado resultante, con un identificador determinista que los hace idempotentes (`{expediente}:{área}`). Así la pantalla ve el cambio apenas confirma. Todo lo demás (el tercero 360, los indicadores del inicio, los informes) **se arma al consultar**. Lo único asíncrono es la suscripción de eventos públicos ([Mensajería](guia:arquitectura-mensajeria-y-tiempo-real)).

> **Verdad:** `docs/04` §4.2 describe proyecciones asíncronas de Marten (`BandejaRevision`, `Tercero360`, `KpisTenant`…) con un comando para reconstruirlas. En el código esas lecturas se escriben en la misma transacción (la bandeja) o se arman al consultar (el tercero 360 y los indicadores), y no existe la proyección `KpisTenant` ni el comando para reconstruir proyecciones.

### Auditoría

- La **configuración** guarda cada cambio con el antes y el después (`RegistroDeCambioDeConfiguracion`) por grupo.
- Los **accesos a documentos** son eventos del documento (`DocumentoAccedido`: quién, desde dónde, qué versión) y las exportaciones quedan registradas.
- Los **ingresos** de las personas y de los operadores quedan en el registro de identidad, con filtros en la consola.
- La **historia de un vínculo o de un expediente** es su propio stream de eventos: la app la muestra como el historial.

## Reglas de datos que conviene recordar

- **Fechas:** UTC en la base y en la API; el calendario del negocio (vencimientos, «hoy», plazos en días) usa la zona del grupo, por omisión `America/Bogota`.
- **Estados:** siempre texto (`Activo`, `EnRevision`), nunca números.
- **Identificaciones:** se normalizan (sin puntos, espacios ni guiones) y se validan por país y tipo; el dígito de verificación se calcula donde aplica (NIT, RUT, CUIT).
- **Secretos:** las llaves de OpenAI, los secretos de webhook, los números de cuenta y las credenciales de listas se guardan cifrados con la clave maestra (o con la clave del grupo); lo personal que viaja en eventos se cifra con una llave de datos por tercero, de modo que borrar esa llave anula esos datos (*crypto-shredding*).
- **Concurrencia:** optimista con `If-Match` en las escrituras de un cliente; con bloqueo de fila en las que también hacen los procesos de fondo, y reintento ante una carrera.
