# ADR-0011 — Identidad delegada desde los sistemas integrados

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-11-06 (decisiones I-0, ID-1, ID-2, ID-3) | Precisa ADR-0003 |

## Contexto
Validasoft no es una aplicación más con su propio login: es un **servicio de validación que otros sistemas invocan** (Marco, SRM, Portal, sistemas de clientes). La persona que revisa o aprueba ya inició sesión en el sistema que la integra. Pedirle otra contraseña rompe la experiencia y duplica la administración de usuarios. A la vez, la auditoría exige saber qué **persona** aprobó cada cosa, no solo qué sistema llamó.

## Decisión
- Cada tenant registra sus **sistemas integrados** (`SistemaIntegrado`: código, nombre, **clave pública** para verificar firmas). Las API keys pertenecen a un sistema; la referencia externa de un vínculo la fija el sistema de la credencial, no el cuerpo de la petición.
- Un sistema actúa **en nombre de una persona** enviando, junto con su API key, la cabecera `X-Usuario-Delegado` con un **JWT corto (≤ 5 min) firmado con su clave privada** (RS256/ES256): `iss` = código del sistema, `aud` = `validasoft`, `sub` = id del usuario en ese sistema, `name`, `email`. Validasoft verifica la firma con la clave pública registrada, crea o actualiza el `Usuario` (con la identidad externa `(sistema, sub)`) y lo registra como actor.
- **Roles y áreas se asignan en Validasoft** y valen igual para todos los sistemas. El permiso efectivo de una petición delegada es la **intersección** entre los scopes de la API key y los del usuario. Un usuario nuevo nace sin roles.
- Para las **pantallas embebidas**, el sistema integrado canjea la aserción por una **sesión corta de Validasoft** (`POST /api/v1/sesiones-usuario`, JWT emitido por Validasoft) que usa el iframe o Web Component.
- **OpenIddict** (ADR-0003) queda para cuando haga falta OIDC estándar: cuentas locales con 2FA para clientes sin sistema propio y federación con proveedores externos. El token del ERP (`erp-token`) se resuelve con este mismo mecanismo: Marco es un sistema integrado más.

## Alternativas descartadas
- **Cuentas propias obligatorias**: doble login y doble administración de usuarios en cada cliente.
- **Roles enviados por el sistema integrado**: dos sistemas podrían dar permisos distintos a la misma persona sobre el mismo flujo.
- **Secreto compartido (HMAC) para firmar aserciones**: Validasoft tendría que guardar secretos recuperables; con clave pública no guarda nada que permita suplantar al sistema.

## Consecuencias
- Validasoft no almacena contraseñas de usuarios de sistemas integrados.
- El SDK debe ofrecer un ayudante para firmar la aserción.
- La auditoría registra persona y sistema de origen en cada evento.
