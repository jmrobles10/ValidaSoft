# ADR-0010 — Personas relacionadas (representantes, socios, beneficiarios finales, miembros) son terceros con bóveda propia

| Estado | Fecha | Reemplaza |
|---|---|---|
| Aceptado | 2026-10-28 (auditoría externa de negocio, B-3) | Precisa ADR-0004 |

## Contexto
La clave del documento global es `(tercero, tipo canónico)` y Producto decidió N representantes legales con uno principal (P-1-6). Con eso, la cédula del representante —el documento más básico de una vinculación— no tenía sitio: una segunda cédula sería "versión duplicada" o superaría a la primera, y la IA la devolvería porque su número no es el NIT del tercero. Lo mismo ocurría con antecedentes, cédulas de socios/UBO y miembros de consorcio.

## Decisión
1. El representante legal, los suplentes, los socios/beneficiarios finales y los miembros de un consorcio son **terceros persona natural** (clave `(país, tipo, número)`), con **bóveda propia**.
2. Se unen al tercero jurídico por `RelacionEntreTerceros { origen, destino, tipo: RepresentanteLegal | Suplente | Socio | BeneficiarioFinal | MiembroDe, principal, vigencia, fuente, confirmación }`.
3. Los documentos de la persona (`CEDULA`, `PASAPORTE`, `CERTIFICADO_ANTECEDENTES`, `DECLARACION_PEP`, `BENEFICIARIO_FINAL`) se validan **una vez** y sirven a todas las empresas y a todos los terceros jurídicos donde figura. `IdentidadCoincide` compara contra la persona.
4. Un requisito del flujo puede exigir "documento X de la persona relacionada con rol Y" (`DePersonaRelacionada`, con `TodasLasRelaciones` opcional); se resuelve contra la relación principal vigente al abrir o recalcular el expediente.
5. **Reconciliación de fuentes**: una relación la declara el tercero (`DATOS_MAESTROS`) o la extrae la IA de la Cámara de comercio. Un cambio detectado por la IA no modifica la relación por sí solo: emite `CambioDeRelacionDetectado(confianza)` y la relación cambia cuando el tercero lo confirma, un revisor lo confirma al revalidar, o la confianza es ≥ 0,98 y la Cámara nueva fue aprobada por alguna empresa. Solo entonces expiran las verificaciones del saliente en los vínculos.
6. `FORMULARIO_SARLAFT` (PorVinculo) **referencia** `BENEFICIARIO_FINAL` y `DECLARACION_PEP` globales de la persona en lugar de duplicarlos.

## Alternativas descartadas
- **Documento con sub-dueño `(tercero, persona)`** sin bóveda propia: más simple, pero no reutiliza la cédula cuando la misma persona representa a dos empresas y obliga a repetir listas restrictivas.
- **Cédula como adjunto `PorVinculo`**: repite el legado.

## Consecuencias
- `boveda.tercero` contiene personas naturales que no tienen vínculos propios; la retención y el habeas data aplican a ellas con base legal derivada de la relación.
- La semilla canónica marca `dePersonaRelacionada` en los tipos que corresponden (03 Anexo A).
- Las listas restrictivas cubren al tercero y a sus personas relacionadas vigentes.
- La importación crea personas desde `TerCamaraComercio`/contactos del Portal y normaliza su identificación.

## Cómo se revierte
Las relaciones pueden aplanarse a atributos del tercero jurídico exportando la bóveda de cada persona; es costoso y se descarta salvo exigencia legal.
