---
titulo: "Mensajería, eventos y tiempo real"
seccion: "Arquitectura"
resumen: "El outbox de Wolverine, las colas locales durables, los procesos periódicos, la suscripción de eventos públicos con sus webhooks y el tiempo real por SSE."
---

# Mensajería, eventos y tiempo real

El outbox de Wolverine, las colas locales durables, los procesos periódicos, la suscripción de eventos públicos con sus webhooks y el tiempo real por SSE.

Validasoft separa lo que debe ser inmediato (la transacción de quien pide) de lo que puede esperar segundos (correos, análisis, vigencias, webhooks). Lo hace con **Wolverine** sobre Marten (ADR-0002): un caso de uso confirma sus eventos, sus documentos y los mensajes que genera **en una sola transacción de PostgreSQL**, y Wolverine despacha esos mensajes solo después del commit.

![Escritura transaccional, colas locales, suscripción de eventos públicos, webhooks, pull y SSE](diagramas/mensajeria.svg)

## Outbox, colas y reintentos

- **Outbox:** `ISesiones` abre una sesión de Marten enlazada al bus de la petición. Todo mensaje publicado queda guardado con los eventos; si la transacción falla, no sale nada, y si confirma, no se pierde ninguno.
- **Colas locales durables:** hoy los mensajes viajan por colas **locales** del propio proceso, con inbox durable (sobreviven a un reinicio). El diseño original (`docs/04` §5) preveía el transporte PostgreSQL y RabbitMQ más adelante; no están conectados.
- **Idempotencia:** los manejadores se escriben para poder repetirse (claves naturales como documento y versión, o ids deterministas).
- **Reintentos:** ante cualquier excepción se reintenta a 1 s, 5 s y 30 s y luego el mensaje pasa a la cola de errores.

| Cola | Qué procesa | Orden |
|---|---|---|
| `correo` | Todos los correos: invitaciones, observaciones, resultados, certificados, avisos de vencimiento, seguridad y notificaciones. | Paralela |
| `requisitos` | Sincronizar los requisitos de un vínculo cuando llega una versión (y revalidar al desbloquear), cruzar la ficha con los documentos, plazo de revalidación y renovación por vencimiento. | Paralela |
| `vigencias` | Calcular las vigencias de un documento o un vínculo, enviar avisos de vencimiento, vencer versiones y vínculos, desbloqueo por fecha. | Paralela |
| `webhooks` | Entregar un evento a un webhook. | **Secuencial**: un intento a la vez y en orden; los reintentos van programados y no la bloquean. |
| `ia` | Analizar una versión con OpenAI. | **Secuencial**: así dos mensajes de la misma versión no llaman dos veces. |
| `lotes` y `campanias` | Procesar una tanda de una invitación masiva o de una campaña de renovación; cada tanda encola la siguiente. | **Secuencial** |
| `plazos` | Barrido y revisión de plazos de cargue, recordatorios de observaciones y SLA de revisión. | Paralela |
| `habeas` y `exportaciones` | Plazos y solicitudes de habeas data; exportación de la bóveda de un tercero. | `habeas` paralela, `exportaciones` secuencial |
| `plan` | Revisar los planes y límites de los grupos. | Paralela |
| `importacion` | Ejecutar una corrida del importador. | **Secuencial**; se reanuda desde su cursor si el proceso cae. |

**Mensajes programados.** Se agendan para una fecha concreta, guardados con la misma transacción: los avisos y vencimientos de un documento (cola `vigencias`), el fin del plazo de revalidación de una actualización (`VencerPlazoDeRevalidacion`), el desbloqueo de un vínculo con fecha (`DesbloquearAlLlegarLaFecha`) y cada reintento de un webhook.

## Procesos periódicos

Lo recurrente no lo dispara un endpoint: son servicios en segundo plano del mismo proceso, con su intervalo en la [configuración del servidor](variables-de-entorno.md). Cada barrido solo mira lo que tiene algo pendiente (por ejemplo, los vínculos con `ProximaRevisionDeVigencia`):

| Proceso | Qué hace | Cada |
|---|---|---|
| Vigencias de vínculos | Pasa a Por vencer, Vencido, abre la renovación y termina los bloqueos con fecha. | 1 hora |
| Plazos de cargue | Recordatorios y caducidad de expedientes que esperan al tercero. | 1 hora |
| Recordatorios de observaciones | Recuerda al tercero lo devuelto, a lo sumo una vez al día. | 1 hora |
| SLA de revisión | Avisa cuando el plazo de un área está por vencer o se incumple. | 15 minutos |
| Certificados | Genera los PDF y vence los certificados cuya fecha pasó. | 10 segundos |
| Verificaciones | Consulta listas, reintenta las fallidas y monitorea los vínculos activos. | 30 segundos |
| Revisión automática | Aplica el nivel Asistido o Automático a los documentos analizados. | 30 segundos |
| Habeas y planes | Plazos de solicitudes y límites de los grupos. | 1 hora y 15 minutos |
| Limpiezas | Ingresos y aserciones usadas ya vencidos, exportaciones de la bóveda, avisos de cambio de más de 30 minutos. | Varias |

## Procesos de negocio

El diseño original (`docs/04` §5) describía sagas persistidas de Wolverine. **No hay sagas persistidas**: la coordinación entre agregados es código de aplicación, y se hace en la misma transacción siempre que se puede.

| Proceso | Qué hace | Dónde |
|---|---|---|
| Proceso de validación | Abre el expediente de un vínculo, calcula sus requisitos, lo cierra aprobado, activa el vínculo y emite el certificado. | **Misma transacción** que la petición o que el proceso de fondo que lo dispara (decisión FL-5 del registro). |
| Sincronización de requisitos | Cuando una versión se comparte con un vínculo, la adjunta a los requisitos del expediente abierto o, si el vínculo ya fue aprobado y la versión cambia lo aprobado, abre la actualización. | Cola `requisitos`. |
| Plazo de revalidación | Al vencer el plazo de una actualización, el certificado pasa de «En verificación» a bloqueante. | Mensaje programado. |
| Emisión de certificado | La emisión (folio, contenido canónico, hash y sello) ocurre en la transacción de la aprobación; el PDF y los vencimientos los hace `ProcesadorDeCertificados` después. | Transacción y proceso periódico. |
| Análisis con IA | Lee la versión con OpenAI y registra los hechos; consume el cupo de la empresa. | Cola `ia`. |
| Invitación masiva y campañas | Valida las filas, crea vínculos y expedientes por tandas (100 y 50 por omisión) y encola los correos; admiten pausa y reanudación. | Colas `lotes` y `campanias`. |

## Eventos públicos y webhooks

Todo lo que pasa en un vínculo, un expediente, un documento o un certificado es un evento del event store. Los que interesan afuera se convierten en **eventos públicos** con un catálogo de 42 tipos (`GET /api/v1/eventos/tipos`) y un sobre estable:

1. El **daemon asíncrono de Marten** corre en modo *hot/cold*: un nodo es el líder (por bloqueo consultivo de PostgreSQL) y lee el event store **en orden**; si cae, otro toma el relevo.
2. La **suscripción `eventos-publicos`** procesa cada lote y, **en la misma transacción que su avance**, registra el evento público con un `cursor` creciente por grupo, crea una entrega por cada webhook que lo acepta, escribe las notificaciones de la campana y los avisos de cambio, y encola los correos a personas. Un lote nunca queda a medias: no se pierden ni se duplican eventos, entregas ni correos.
3. La cola `webhooks` entrega cada evento con un `POST` firmado; si el receptor no responde 2xx a tiempo, programa un reintento (1 min, 5 min, 30 min, 2 h y 6 h) y tras 20 intentos fallidos seguidos deshabilita la suscripción y avisa a los administradores.
4. El mismo sobre se puede **leer por pull** (`GET /api/v1/eventos?desde={cursor}`, hasta 200 por página) o por SSE.

Los datos públicos nunca llevan revisores, correos, IP ni nombres de personas: solo ids, estados, códigos, los motivos tal como se comunicaron al tercero y las referencias externas del vínculo. Los hechos más viejos que 24 horas no generan notificaciones a personas (al encender la suscripción sobre una base con historia). El detalle de la firma y de la entrega está en la [guía de integración](guia-de-integracion.md#webhooks-firmados).

## Tiempo real (SSE)

Hay tres flujos de *Server-Sent Events*. Los tres consultan la base cada 2 segundos y mandan un comentario de vida cada 15; no hay WebSockets.

| Flujo | Quién | Qué envía |
|---|---|---|
| `GET /api/v1/eventos/stream` | Un integrador con `eventos:leer` | Los eventos públicos: `id` es el cursor (se reanuda con `Last-Event-ID` o `?desde=`), `event` es el tipo y `data` es el mismo sobre del pull. |
| `GET /api/v1/notificaciones/stream` | Una persona de la empresa | `notificacion` (cada una nueva), `no_leidas` (el total, cuando cambia) y, con `X-Empresa`, `cambio`: el tipo de recurso y su id (expediente, vínculo, cuenta, documento, ficha o certificado) cuando algo de la empresa activa cambió. |
| `GET /public/v1/notificaciones/stream` | El tercero | Lo mismo para su campana y los cambios de lo suyo. |

Un aviso de `cambio` **no lleva datos**: la pantalla abierta vuelve a pedir lo que ya veía a la API de siempre, con sus permisos. Un aviso vive 30 minutos; si el stream se corta y reconecta, la pantalla refresca todo en vez de recuperar lo perdido. Un ingreso directo cerrado o revocado también corta el stream (el cliente reconecta y recibe 401).
