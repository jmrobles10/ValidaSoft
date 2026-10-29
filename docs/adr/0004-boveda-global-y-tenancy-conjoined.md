# ADR-0004 — Bóveda global del tercero (`boveda`) + multi-tenant conjoined (`validacion`)

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 | — |

## Contexto
Un tercero se relaciona con muchas empresas (tenants) y sus documentos de identidad son hechos propios, no de cada empresa. Un aislamiento estricto por tenant obligaría a duplicar binarios y reproduciría la "copia espejo" del Portal. A la vez, las decisiones de cada empresa deben ser privadas y aisladas.

## Decisión
Tres particiones lógicas (esquemas) en la misma PostgreSQL y **un solo `DocumentStore` de Marten** con eventos *conjoined* (T-1, T-2): los streams de `validacion` usan el `tenant_id` real; los de la bóveda el tenant técnico `boveda`; los de plataforma el tenant técnico `plataforma`. Así se conserva la transacción única evento + proyección + outbox; se valida con un *spike* en H1. La lista exacta de tablas por esquema vive en 03 §2.2 y los demás documentos la copian.
- **`plataforma`** (tenant técnico `plataforma`): `tenant`, `empresa`, `plan`, `uso_tenant`, `tipo_documento_canonico`, `esquema_formulario`, `catalogo_tipo_identificacion`, `concesion_acceso_soporte`.
- **`boveda`** (global, clave = tercero; incluye a las personas relacionadas como terceros, ADR-0010): `tercero`, `relacion_entre_terceros`, `consentimiento_tercero`, `cuenta_bancaria`, `documento`, `version_documento`, `verificacion_version`, `comparticion_version`, `acceso_documento`, streams `Documento-{id}`. El acceso de una empresa a una versión se autoriza **solo** mediante `comparticion_version` (que solo existe tras `InvitacionAceptada`, ADR-0009) o propiedad del vínculo en documentos `PorVinculo`.
- **`validacion`** (multi-tenant con *conjoined tenancy* de Marten, `tenant_id` en toda fila, **más filtro obligatorio `empresa_id`** vía `ISesionDeEmpresa`, T-3): `tipo_documento_tenant`, `tipo_documento_empresa`, `vinculo`, `expediente`, `requisito`, `version_aceptada`, `decision_version`, `version_no_admitida`, `cuenta_designada`, `bloqueo`, `certificado`, streams `Vinculo-{id}`, `Expediente-{id}`, `Certificado-{id}`.
- Binarios: bucket `boveda` (`terceros/{terceroId}/{sha256}`) para globales, cifrado con **clave de plataforma**; bucket `tenant-{id}` (`empresas/{empresaId}/vinculos/{vinculoId}/{sha256}`) para `PorVinculo`, cifrado con **SSE-KMS y clave propia del tenant** (P-14-3, permite borrado criptográfico al cerrar un tenant); solo URLs prefirmadas tras autorizar; cada emisión se audita.
- Toda sesión de `validacion` se abre con el tenant de la credencial (middleware); un test arquitectónico prohíbe consultas sin tenant.
- **Precisión posterior (ADR-0008):** el tenant es un grupo empresarial con empresas internas; dentro de `validacion` el aislamiento es doble, `tenant_id` + `empresa_id`, y la bóveda comparte versiones con vínculos (es decir, por empresa).

## Alternativas descartadas
- **Tenant por base de datos / esquema por tenant**: aísla mejor pero hace imposible compartir la bóveda sin replicación.
- **Todo en un solo espacio con `tenant_id` nullable**: mezcla hechos y decisiones y facilita fugas entre empresas.
- **Tenant = tercero para la bóveda**: fuerza a cambiar de tenant en cada petición y complica las proyecciones cruzadas.

## Consecuencias
- Un archivo, muchas decisiones; cero duplicación.
- Dos modelos de autorización claramente separados: *compartición* (bóveda) y *tenant* (validación).
- Las proyecciones que cruzan particiones (`FrescuraDocumentoTercero`, `EstadoCertificacionTercero`) se alimentan por eventos, no por joins entre esquemas.

## Cómo se revierte
Si un cliente exigiera aislamiento físico total, su `validacion` puede moverse a otra base conservando la bóveda compartida; los documentos `PorVinculo` ya viven en su bucket.
