# Guías del manual

Lo que no sale de un recorrido: arquitectura, flujo de punta a punta, configuración e integración. Se escriben en `e2e/manual/guias/` y `pnpm manual` las publica aquí y en el sitio.

## Arquitectura

- [Contexto y contenedores](arquitectura-contexto-y-contenedores.md) — Quién usa Validasoft, con qué sistemas se conecta y qué piezas lo forman: las dos apps, el proceso .NET, PostgreSQL, el almacén de archivos y los servicios externos.
- [Módulos de la solución](arquitectura-modulos.md) — Los proyectos .NET y sus dependencias, los módulos de la capa de aplicación, el patrón de un caso de uso, el modelo de errores y los paquetes del frontend.
- [Datos, multi-tenencia y event sourcing](arquitectura-datos-y-tenencia.md) — Cómo se aíslan los grupos y las empresas, qué es global y qué es privado, qué agregados guardan eventos y cómo se leen.
- [Mensajería, eventos y tiempo real](arquitectura-mensajeria-y-tiempo-real.md) — El outbox de Wolverine, las colas locales durables, los procesos periódicos, la suscripción de eventos públicos con sus webhooks y el tiempo real por SSE.
- [Seguridad e identidad](arquitectura-seguridad.md) — Cómo entra cada actor, qué credencial usa, qué permisos tiene, cómo se aíslan los datos y qué se cifra: código por correo, passkeys, SSO, API keys, identidad delegada y confirmación con passkey.
- [Almacenamiento de documentos](arquitectura-documentos.md) — Cómo se guardan los archivos: buckets y claves por SHA-256, subida en tres pasos con URL prefirmada, verificación, versiones inmutables, compartición, descargas auditadas y retención.
- [Ciclos de vida: documento, vínculo, expediente y certificado](arquitectura-ciclos-de-vida.md) — Los estados de cada cosa y sus transiciones, dibujados y explicados: qué las dispara, qué regla las limita y qué evento o webhook producen.
- [Despliegue y operación](arquitectura-despliegue.md) — La imagen Docker, el compose local con sus perfiles, el arranque de la base, la salud, las réplicas, qué respaldar y qué verifica el CI.

## Flujo de punta a punta

- [Del vínculo a la validación, de punta a punta](flujo-del-vinculo-a-la-validacion.md) — El camino completo: la empresa se configura y crea el vínculo, el tercero acepta y completa su expediente, las áreas y el oficial revisan, se emite el certificado y luego vienen las vigencias, las actualizaciones, los bloqueos y los webhooks de cada hito.

## Configuración y glosario

- [Glosario](glosario.md) — El vocabulario de Validasoft: quién es quién, qué es un vínculo, un expediente o un certificado, y los estados de cada cosa tal como los guarda el sistema.
- [Configuración del grupo](configuracion-del-grupo.md) — Cada ajuste de alcance grupo en la app de la empresa: dónde está, qué cambia, su valor por defecto y sus variantes. Comprobado contra el código.
- [Configuración de la empresa](configuracion-de-la-empresa.md) — Cada ajuste de alcance empresa: datos y marca, correo, IA, listas restrictivas, matriz de riesgo, áreas, flujos aprobatorios, plazos y delegaciones, con valores por defecto y variantes.
- [Configuración de Plataforma](configuracion-de-plataforma.md) — La consola /plataforma y los ajustes globales de la instalación: grupos, planes, IA, listas restrictivas, operadores, secretos y soporte, con valores por defecto y variantes.
- [Variables de entorno del despliegue](variables-de-entorno.md) — Toda la configuración de servidor de Validasoft: qué clave es obligatoria, qué hace cada una y su valor por defecto. Se leen del entorno, de appsettings o de ambos.

## Integración

- [Guía de integración, resumida](guia-de-integracion.md) — Lo esencial para conectar un sistema a Validasoft: llave de API, idempotencia, webhooks firmados con su verificación, sandbox y los SDK de .NET y TypeScript, con enlaces a la guía completa y a la referencia de la API.
