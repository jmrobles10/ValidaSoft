# ADR-0008 — Tenant = grupo empresarial con empresas internas aisladas

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 (decisión de Producto, P-11-2 del cuestionario) | Precisa ADR-0004 |

## Contexto
Un cliente típico (constructora grande) opera con varias razones sociales (varios NIT) que comparten proveedores, usuarios administrativos y políticas, pero cada razón social responde legalmente por sus propias decisiones de vinculación y cumplimiento. La recomendación técnica era "un tenant por razón social agrupados en `GrupoEmpresarial`"; Producto eligió **un tenant por grupo con empresas internas** y, ante la pregunta de visibilidad, **decisiones aisladas por empresa interna** (ni se ven ni se heredan dentro del grupo).

## Decisión
- El **tenant** es el grupo empresarial: comparte usuarios, roles, catálogo de tipos de documento, flujos, plantillas, API keys, medición de uso y facturación; tiene región y clave KMS propias.
- Dentro del tenant existe la entidad **`Empresa`** (razón social: NIT, nombre, logo, `Reply-To`, configuración propia opcional). Un tenant tiene ≥ 1 empresa.
- El **vínculo** es `(tenantId, empresaId, terceroId, tipoVinculoId)`. `VersionAceptada`, `DecisionVersion`, `Expediente`, `Certificado`, `Bloqueo` y `CuentaDesignada` llevan `empresaId`.
- **Aislamiento de decisiones**: las consultas de decisiones, aceptaciones y bandejas filtran siempre por `empresaId`; un usuario ve solo las empresas a las que tiene rol; no hay herencia de aprobaciones entre empresas del grupo (cada una revalida).
- Lo que sí se comparte dentro del tenant: configuración, usuarios (con roles por empresa o para todo el tenant), catálogo, KPIs agregados (sin detalle de decisiones) y la vista "este tercero tiene vínculos con N empresas del grupo" (solo existencia y estado del vínculo, no veredictos).
- API keys por tenant con **alcance opcional por empresa** (`empresas:[…]` o `*`); los webhooks y eventos públicos incluyen `empresaId`.
- La bóveda del tercero (ADR-0004) no cambia: comparte versiones con **vínculos**, es decir, por empresa.

## Alternativas descartadas
- **Un tenant por razón social + `GrupoEmpresarial`** (recomendación técnica): aislamiento más simple por construcción, pero duplica administración de usuarios y catálogo; Producto prefirió la administración unificada.
- **Empresas internas con decisiones visibles o heredables**: Producto las descartó explícitamente.

## Consecuencias
- Toda consulta de `validacion` tiene dos claves de aislamiento: `tenant_id` (Marten conjoined) **y** `empresa_id` (filtro obligatorio verificado por test arquitectónico y por `IAuthorizationHandler`).
- La semántica de "decisión privada" del documento 02 se lee como "privada por empresa", dentro y fuera del grupo.
- La migración del Portal/SRM (donde cada constructora era independiente) crea un tenant por cliente y una empresa por constructora/NIT.
- El panel de plataforma crea tenants y empresas; el administrador del tenant gestiona empresas y roles.

## Cómo se revierte
Si un cliente exigiera aislamiento total entre razones sociales, cada empresa interna puede exportarse como tenant propio: las claves ya incluyen `empresaId` y la bóveda es independiente.
