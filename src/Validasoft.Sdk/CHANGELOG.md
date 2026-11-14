# Cambios del SDK de Validasoft

Los tres paquetes —`Validasoft.Contratos` y `Validasoft.Sdk` (NuGet) y `@validasoft/sdk` (npm)— salen juntos y comparten versión.
Sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el [versionado semántico](https://semver.org/lang/es/): una
versión mayor solo cambia cuando la API v1 rompe compatibilidad (ver «Versionado y deprecación» en la guía de integración).

## [Sin publicar]

### Agregado
- Aprobación antes de invitar (opt-in por tipo de vínculo; quien no la activa no ve ningún cambio). Con el tipo en `RequiereAprobacionParaInvitar`, `Vinculos.CrearAsync` y
  `Invitaciones.EnviarAsync` ya no envían la invitación: queda retenida hasta que otra persona del rol o área que el tipo designó la apruebe o la rechace.
  `VinculoDto.InvitacionPorAprobar` (`invitacionPorAprobar` en TypeScript) dice en qué va (`Pendiente`, `Aprobada` o `Rechazada`, con su motivo) y no cambia `Estado`; los eventos
  `vinculo.invitacion_por_aprobar`, `vinculo.invitacion_aprobada` y `vinculo.invitacion_rechazada` (y `invitaciones.lote_*` para lotes) la reflejan. Para distinguir el resultado de
  pedir la invitación: .NET `Invitaciones.SolicitarAsync` devuelve `ResultadoDeInvitacion` (`Enviada` o `PorAprobar`; `EnviarAsync` lanza `InvalidOperationException` si quedó retenida)
  y TypeScript `invitaciones.enviar` devuelve `EnlaceInvitacionDto | InvitacionPorAprobarDto` con la guarda `invitacionRetenida`.
- `ConsultaCertificacionDto.EnRevision` (`enRevision` en TypeScript): solo informativo. Es `true` mientras el tercero tiene una actualización abierta
  con la empresa (actualizó información que ya había aprobado y la empresa la está revisando). No cambia nada de lo demás: `estadoVinculo` sigue
  siendo `Activo`, `PorVencer` o `Vencido` y el certificado, la vigencia, `bloqueado` y `revalidacionPendiente` se calculan como siempre. Quien no lo conoce lo ignora.
- `ConsultaCertificacionDto.EnVerificacion` (`enVerificacion` en TypeScript): solo informativo. Es `true` mientras el certificado sigue vigente y la empresa revisa de nuevo lo
  que el tercero actualizó («En verificación»). `estado` sigue siendo `ConPendientes` y `bloqueado`, `vigenteHasta` y `revalidacionPendiente` no cambian; con el plazo de la
  revalidación vencido, o el certificado vencido, es `false`. Quien no lo conoce lo ignora.

## [1.0.0]

Primera versión publicada.

### Agregado
- Cliente de la API v1: vínculos, invitaciones, expedientes, certificados, consulta de certificación (con `ETag` y `If-None-Match`),
  eventos en modo pull (con recorrido por páginas) y sesiones para pantallas embebidas.
- Identidad delegada: firma de aserciones `X-Usuario-Delegado` (RS256 y ES256), una por petición.
- Verificación de webhooks firmados (tolerancia de 5 minutos, varios secretos durante una rotación).
- Sandbox: crear, consultar y reiniciar el entorno de pruebas del grupo, y leer el buzón de correos que no salieron
  (`sandbox.correos`, `sandbox.esperarCorreo`).
- `Idempotency-Key` automática en todo POST y reintentos solo de lo idempotente.
- .NET: `net7.0`, `net8.0` y `net10.0`, sin dependencias fuera de la BCL.
- TypeScript: ESM y CommonJS con tipos, sin dependencias (`fetch` y Web Crypto; Node 20+ o navegador).
