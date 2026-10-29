# ADR-0009 — Consentimiento antes de compartir: la bóveda solo se abre a una empresa cuando el tercero acepta su invitación

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 (auditoría externa de negocio, B-1) | Precisa ADR-0005 punto 2 |

## Contexto
ADR-0005 compartía la versión actual con un vínculo **al crearse** el vínculo. Combinado con A-8 (el tercero no puede excluir), cualquier empresa podía crear un vínculo por NIT y leer RUT, estados financieros y certificaciones bancarias de un tercero que nunca la había autorizado: un vector de cosecha de información por NIT y una infracción de la Ley 1581 de 2012 (autorización previa, expresa e informada **por responsable**).

## Decisión
1. **Ninguna versión se comparte con un vínculo antes de `InvitacionAceptada` + `ConsentimientosRegistrados` para esa empresa.** No existe fila en `comparticion_version` para un vínculo sin `invitacion_aceptada_en`.
2. Entre la creación del vínculo y la aceptación, la empresa solo ve: "tercero existente en Validasoft; N de tus requisitos podrían quedar cubiertos con lo que ya tiene", sin nombres de documentos, versiones ni fechas.
3. Al aceptar, la saga `ProcesoDeValidacion` ejecuta `CompartirConVinculo` para cada tipo exigido con versión actual; si todos los requisitos obligatorios quedan `Cargado`, el expediente pasa solo a `EnRevision`.
4. Las versiones **futuras** se comparten automáticamente con todos los vínculos ya aceptados que exigen el tipo: M-7 y A-8 siguen intactas.
5. Desvinculación y bloqueo **congelan** la compartición hacia adelante (`ComparticionCongelada`): no llegan versiones nuevas, pero lo ya compartido sigue legible durante la retención de la empresa. El desbloqueo la reanuda.
6. Mitigaciones complementarias en 07: rate limit a la creación de vínculos sobre terceros existentes y alerta de creación masiva.

## Alternativas descartadas
- **Compartir al crear el vínculo** (como estaba): riesgo legal y de enumeración de NITs.
- **Aceptación + confirmación explícita "compartir mi bóveda con X"**: más fricción y contradice A-8.

## Consecuencias
- El criterio de H2 "dos empresas ven la misma versión" exige un vínculo con invitación aceptada: H2 incluye un `Vinculo` mínimo o el criterio se mueve a H4.
- `EnviarARevision` puede ser automático al aceptar la invitación.
- La migración marca `invitacion_aceptada_en` para los vínculos activos del legado (el proveedor ya había aceptado en el Portal).

## Cómo se revierte
Un ADR posterior podría permitir una "precompartición" de solo metadatos (existencia y vigencia, sin binarios) para empresas invitantes; hoy se descarta.
