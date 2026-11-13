# Manual de Validasoft

Documentación de cada flujo del producto, **generada a partir de recorridos reales** de la aplicación (pruebas Playwright en `e2e/manual/`) y del contrato OpenAPI. No se edita a mano: si el producto cambia, se vuelve a recorrer y a generar (`pnpm manual:todo`).

Los pantallazos no se guardan en git: se generan en `salida/manual/` junto con el sitio navegable (`salida/manual/index.html`, con buscador, modo claro y oscuro y vista para celular). Este directorio guarda el texto en Markdown.

## Recorridos por dominio

- [Acceso y cuenta](acceso/README.md) — Cómo entran las personas: ingreso directo sin contraseña, passkeys, inicio de sesión único (SSO) y Mi cuenta.
- [Portal del tercero](tercero/README.md) — Todo lo que hace el tercero: invitación, Mis empresas, revisión, documentos, ficha, cuentas designadas y avisos.
- [Vínculos y terceros](vinculos/README.md) — Invitar, buscar y seguir a los terceros de la empresa, su ficha y el ciclo de vida del vínculo.
- [Revisión de expedientes](revision/README.md) — Bandeja de revisión, decisión de cada área, observaciones, rechazos, desaprobaciones y revalidaciones.
- [Documentos y vigencias](documentos/README.md) — Documentos del tercero, versiones, vigencias, vencimientos y renovaciones.
- [Configuración](configuracion/README.md) — Cada sección de Configuración del grupo y de la empresa, con sus variantes.
- [Cumplimiento y SAGRILAFT](cumplimiento/README.md) — Listas restrictivas, estudio de cumplimiento, matriz de riesgo, declaración PEP y monitoreo.
- [Informes, auditoría y certificados](informes/README.md) — Informes y exportes, auditoría de accesos y certificados verificables.
- [Operación masiva y continua](operacion/README.md) — Invitación por lotes, campañas de renovación, plazos de revisión, recordatorios y delegaciones.
- [IA y revisión automática](ia/README.md) — Plan y uso de IA, niveles de revisión automática y lo que ve cada persona.
- [Datos personales](privacidad/README.md) — Habeas data: solicitudes de consulta, corrección y supresión; consentimientos y privacidad.
- [Consola de Plataforma](plataforma/README.md) — Grupos, empresas, planes, operadores, secretos, listas y accesos de soporte.
- [Integración](integracion/README.md) — API, llaves, sistemas integradores, webhooks, eventos, sandbox, SSO de sistemas e importación.
- [Uso embebido](embebido/README.md) — Validasoft dentro de otro sistema: app de la empresa en un iframe y sesiones del tercero embebidas.
- [Verificación pública](publico/README.md) — Páginas sin sesión: verificación de certificados y enlaces públicos.
- [Experiencia móvil](movil/README.md) — Recorridos pensados para el celular: navegación, subida de documentos y avisos.

## Actores

- **Plataforma**: Operadores de la consola de Plataforma: dan de alta grupos y empresas, administran planes, listas, secretos y soporte.
- **Administrador de empresa**: Administra el grupo empresarial: configura tipos de documento, flujos, áreas, personas, integraciones y plantillas.
- **Revisor/área**: Persona de un área que revisa expedientes: aprueba, observa, rechaza y atiende su bandeja.
- **Oficial de cumplimiento**: Responsable de SAGRILAFT: listas restrictivas, estudios de cumplimiento, matriz de riesgo y monitoreo.
- **Tercero**: Proveedor, cliente o aliado que valida su información: acepta invitaciones, sube documentos y completa su ficha.
- **Sistema integrador**: Sistema externo (ERP, SRM, portal) que invita terceros, consulta certificaciones y recibe eventos por API y webhooks.
- **Público**: Cualquier persona sin sesión: verifica un certificado o lee las páginas públicas.

## Guías

Lo que no sale de un recorrido (se escribe en `e2e/manual/guias/`):

- **Arquitectura**: [Contexto y contenedores](guias/arquitectura-contexto-y-contenedores.md) · [Módulos de la solución](guias/arquitectura-modulos.md) · [Datos, multi-tenencia y event sourcing](guias/arquitectura-datos-y-tenencia.md) · [Mensajería, eventos y tiempo real](guias/arquitectura-mensajeria-y-tiempo-real.md) · [Seguridad e identidad](guias/arquitectura-seguridad.md) · [Almacenamiento de documentos](guias/arquitectura-documentos.md) · [Ciclos de vida: documento, vínculo, expediente y certificado](guias/arquitectura-ciclos-de-vida.md) · [Despliegue y operación](guias/arquitectura-despliegue.md)
- **Flujo de punta a punta**: [Del vínculo a la validación, de punta a punta](guias/flujo-del-vinculo-a-la-validacion.md)
- **Configuración y glosario**: [Glosario](guias/glosario.md) · [Configuración del grupo](guias/configuracion-del-grupo.md) · [Configuración de la empresa](guias/configuracion-de-la-empresa.md) · [Configuración de Plataforma](guias/configuracion-de-plataforma.md) · [Variables de entorno del despliegue](guias/variables-de-entorno.md)
- **Integración**: [Guía de integración, resumida](guias/guia-de-integracion.md)

## Referencia de la API

- [API de Validasoft](api/README.md): cada recurso con sus operaciones, permisos, parámetros, cuerpos, respuestas y ejemplos, generada de `packages/api-client/openapi/v1.json`.

## Cómo se genera

```bash
pnpm manual:recorrer [--grep <texto>]   # corre los recorridos y deja manifiestos e imágenes en salida/manual-recorridos/
pnpm manual                             # genera el sitio HTML (salida/manual/) y este Markdown
pnpm manual:todo                        # las dos cosas
```

La plantilla para escribir un recorrido nuevo está en [`e2e/manual/README.md`](../../e2e/manual/README.md); la lista de lo que falta documentar, en [`e2e/manual/INVENTARIO.md`](../../e2e/manual/INVENTARIO.md).
