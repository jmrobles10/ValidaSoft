# Cambios del SDK de Validasoft

Los tres paquetes —`Validasoft.Contratos` y `Validasoft.Sdk` (NuGet) y `@validasoft/sdk` (npm)— salen juntos y comparten versión.
Sigue [Keep a Changelog](https://keepachangelog.com/es-ES/1.1.0/) y el [versionado semántico](https://semver.org/lang/es/): una
versión mayor solo cambia cuando la API v1 rompe compatibilidad (ver «Versionado y deprecación» en la guía de integración).

## [Sin publicar]

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
