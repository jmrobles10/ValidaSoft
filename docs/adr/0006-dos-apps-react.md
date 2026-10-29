# ADR-0006 — Dos aplicaciones React (`web` y `public`) con paquetes compartidos

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 | — |

## Contexto
Hay dos audiencias con necesidades opuestas: el revisor/administrador (escritorio, autenticado, DataGrid Pro, editor de flujos, visor con comparación) y el tercero (celular en obra, sin contraseña, 3G, cámara, a veces sin señal) más el verificador público de certificados y el embed en Portal/ERP.

## Decisión
Monorepo con `apps/web` (revisor/admin, OIDC PKCE contra OpenIddict) y `apps/public` (tercero PWA con cola offline, verificador (ruta UI `/verificar/:folio` sobre `GET /public/v1/verificar/{folio}`), embed `/embed/expediente/{token}` con `postMessage`), y paquetes compartidos `packages/ui` (design system de la marca sobre MUI), `packages/api-client` (generado con orval desde OpenAPI), `packages/sdk-ts`, `packages/i18n`, `packages/domain`, `packages/config`. Stack React 18 + TypeScript strict + Vite + MUI v6 + TanStack Query + Zustand, que el equipo domina.

## Alternativas descartadas
- **Una sola SPA con rutas públicas**: el bundle del tercero arrastraría DataGrid Pro, Quill y el editor; CSP y PWA distintas en el mismo origen; código de administración servido a anónimos.
- **Repositorio frontend separado**: rompe la generación del cliente desde el OpenAPI del backend en el mismo PR.
- **Next.js/SSR**: no aporta a una app autenticada ni a una PWA; añade runtime.

## Consecuencias
- Bundle público pequeño (< 250 KB gz objetivo), licencias MUI X solo en `web`.
- El contrato OpenAPI es la única fuente de tipos; un cambio de API rompe el build del cliente.
- Dos pipelines de despliegue estático.

## Cómo se revierte
Los paquetes compartidos permiten fusionar o dividir apps sin reescribir componentes.
