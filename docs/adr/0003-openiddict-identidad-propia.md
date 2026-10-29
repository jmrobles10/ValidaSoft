# ADR-0003 — OpenIddict embebido + ASP.NET Identity en lugar de Keycloak

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 | — |

## Contexto
Hacen falta: usuarios y roles por tenant, 2FA, API keys con scopes, **acceso del tercero sin contraseña** (magic link + OTP), *token exchange* con el ERP Marco para SSO desde el iframe, federación futura con directorios de clientes, e instalación on-premise sencilla. El legado enviaba contraseñas en claro por correo y exponía la API key del Portal al navegador.

## Decisión
Módulo `Identidad` con **ASP.NET Identity + OpenIddict** dentro de `Validasoft.Api`, exponiendo OIDC estándar (Authorization Code + PKCE para `apps/web`) y dos *grants* propios: `urn:validasoft:magic-link` (canje de token opaco de un solo uso por sesión corta ligada al expediente) y `urn:validasoft:erp-token` (validación del token del marco contra el ERP del tenant y emisión de JWT propio). Tenant y permisos viajan como claims; la autorización efectiva siempre la decide el backend.

## Alternativas descartadas
- **Keycloak**: multi-tenant por realm es pesado de administrar; magic link y token exchange con el ERP requieren extensiones Java; +1 JVM (≥1 GB) y ciclo de upgrades propio on-premise. Sigue siendo federable después porque Validasoft habla OIDC estándar.
- **Sesión propia sin OIDC**: reinventa 2FA, recuperación y federación.

## Consecuencias
- Cero contenedores adicionales; una sola pila .NET.
- Magic links y API keys se modelan como ciudadanos de primera clase (hash Argon2id, scopes, rotación, auditoría).
- Un cliente con IdP propio (Entra ID, ADFS, Keycloak) se federa por tenant como proveedor externo.

## Cómo se revierte
Al ser OIDC estándar, las apps pueden apuntar a otro *authority*; los grants propios se reimplementarían como extensiones del IdP elegido.
