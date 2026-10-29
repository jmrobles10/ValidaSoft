# Validasoft (nombre de trabajo)

Sistema de validación documental para cualquier vínculo comercial (proveedor, cliente, empleado, arrendatario, paciente…), multi-tenant, independiente del ERP y consumible por API. Reemplaza la validación documental repartida hoy entre el SRM y el Portal de Proveedores.

> **Estado del repositorio: Hito 0 — documentación.** Aún no hay código. Los documentos de `docs/` definen alcance, modelo, arquitectura y plan por hitos; el código llega en los hitos siguientes (ver `docs/09-plan-de-hitos-y-responsabilidades.md`).
>
> El nombre "Validasoft" es provisional. Si cambia, se reemplaza en todos los documentos (buscar `Validasoft`).

## Documentos

> Empieza por [docs/MAPA.md](docs/MAPA.md): orden de lectura por rol y mapa del desarrollo hito a hito.

| # | Documento | Qué responde |
|---|---|---|
| 00 | [Visión y alcance](docs/00-vision-y-alcance.md) | Por qué, para quién, qué entra y qué no, métricas de éxito |
| 01 | [Análisis del legado SRM ↔ Portal](docs/01-analisis-legado-srm-portal.md) | Cómo funciona hoy, qué falla, qué hay que cubrir como mínimo |
| 02 | [Modelo documental compartido](docs/02-modelo-documental-compartido.md) | **El dolor principal**: documentos de muchos terceros con muchas empresas; versión única, decisiones por empresa, vigencias en dos capas, revisión automática, almacenamiento |
| 03 | [Dominio y eventos](docs/03-dominio-y-eventos.md) | Agregados, estados, comandos, eventos, reglas, proyecciones |
| 04 | [Arquitectura de plataforma](docs/04-arquitectura-plataforma.md) | Solución .NET 10, Marten/Wolverine, MinIO, IA, Docker, CI, testing |
| 05 | [API, contratos y webhooks](docs/05-api-contratos-y-webhooks.md) | REST v1, idempotencia, errores, eventos públicos, firma de webhooks |
| 06 | [Frontend y UX](docs/06-frontend-y-ux.md) | Apps del revisor y del tercero, design system, pantallas, flujos |
| 07 | [Seguridad, identidad y tenancy](docs/07-seguridad-identidad-y-tenancy.md) | OpenIddict, roles, API keys, magic links, aislamiento, retención |
| 08 | [Integración y migración](docs/08-integracion-y-migracion.md) | SDKs, SRM/Portal/Marco, importador, corte por empresa, rollback |
| 09 | [Plan de hitos y responsabilidades](docs/09-plan-de-hitos-y-responsabilidades.md) | Hitos H0–H11 con entregables, responsables y criterios de aceptación |
| 10 | [Glosario](docs/10-glosario.md) | Lenguaje ubicuo y equivalencias con el legado |
| 11 | [Trazabilidad por hito](docs/11-trazabilidad-por-hito.md) | Qué endpoints, DTOs, eventos, alertas y pantallas entrega cada hito |
| ADR | [Decisiones de arquitectura](docs/adr/) | Por qué se decidió cada cosa estructural |
| — | [Cuestionario](docs/decisiones/cuestionario.md) y [Registro de decisiones](docs/decisiones/registro.md) | Las 97 preguntas del experto y lo que Producto decidió |

## Reglas de oro del producto (resumen)

1. **Un documento global tiene una sola versión vigente por tercero.** Las empresas no eligen versión: validan la actual. Si el tercero sube una versión nueva válida, todas las empresas vinculadas reciben alerta y deben revalidar (15 días y luego el certificado bloquea).
2. **Una empresa ve la bóveda solo cuando el tercero acepta su invitación** y autoriza el tratamiento; antes solo sabe cuántos requisitos podrían quedar cubiertos (ADR-0009). El tercero no puede excluir empresas ya aceptadas.
3. **Las decisiones son privadas por empresa**, también entre razones sociales del mismo grupo (ADR-0008). Lo que A aprobó u observó no lo ve B.
4. **Los hechos sobre el archivo se calculan una vez, a costo de la plataforma** (legibilidad, tipo, fechas, comparación con la versión anterior) y se comparten; el **juicio** es de cada empresa con sus reglas y es lo único que paga por decisión.
5. **Sobre una versión solo se aprueba u observa** (con "no admitir esta versión"); **rechazar es del expediente** y solo lo hace un área con permiso.
6. **Las personas también son terceros**: representante legal, socios, beneficiario final y miembros tienen su propia bóveda y se validan una vez (ADR-0010).
7. **La vigencia tiene dos capas**: la del documento (fecha propia, expedición, periodo) y la exigencia de frescura de cada empresa (tenant → empresa → regla del flujo).
8. **Nada depende de un llamador externo**: vencimientos, avisos y reintentos los ejecuta el propio sistema.
9. **Sin contraseñas para el tercero, sin secretos en el navegador, sin estados mágicos; el soporte de plataforma no ve datos salvo acceso temporal concedido por el tenant.**

## Convenciones de los documentos

- Español, incluido el lenguaje del dominio (`Expediente`, `Vínculo`, `Veredicto`).
- Cada documento abre con `Estado` (borrador / en revisión / aprobado), fecha y responsable, y cierra con **Decisiones abiertas** y **Fuentes verificadas** (archivo:línea en `C:\Fuentes_Git\SRM` o `C:\Fuentes_Git\PortalProveedores` cuando se afirma algo del legado).
- Los diagramas van en Mermaid dentro del Markdown.
- Un cambio de decisión se registra como ADR nuevo; no se reescribe la historia.

## Referencias del legado

- SRM: `C:\Fuentes_Git\SRM` (rama `Develop`)
- Portal de Proveedores: `C:\Fuentes_Git\PortalProveedores`
- Marca: presentación "Validasoft — Presentación de marca 2026"
