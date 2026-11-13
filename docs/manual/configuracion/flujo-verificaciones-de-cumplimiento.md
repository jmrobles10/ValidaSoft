---
id: "configuracion/flujo-verificaciones-de-cumplimiento"
titulo: "Pide listas restrictivas y el estudio SAGRILAFT en el flujo"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora agrega al flujo de proveedores dos verificaciones de cumplimiento: la consulta de listas restrictivas, que bloquea la aprobación si hay coincidencias, y el estudio del oficial de cumplimiento, con vigencia y reconsulta propias."
pasos: 3
---

# Pide listas restrictivas y el estudio SAGRILAFT en el flujo

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora agrega al flujo de proveedores dos verificaciones de cumplimiento: la consulta de listas restrictivas, que bloquea la aprobación si hay coincidencias, y el estudio del oficial de cumplimiento, con vigencia y reconsulta propias.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con miembros, y el proveedor de listas de la plataforma disponible.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Pide las verificaciones de cumplimiento
2. Define si bloquea y cada cuánto se vuelve a consultar
3. Guarda las verificaciones en el borrador

## Paso 1. Pide las verificaciones de cumplimiento

Las verificaciones son lo que debe estar satisfecho antes de aprobar el expediente, además de las áreas. «Listas restrictivas» se consulta sola al pasar a revisión, con el tercero, su representante, suplentes, socios y PEP declarados en su ficha. «Estudio del oficial de cumplimiento» es la debida diligencia que hace el oficial: nivel de riesgo, conflicto de interés, observaciones y concepto.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 1: Pide las verificaciones de cumplimiento (escritorio)](../../../salida/manual/img/configuracion/flujo-verificaciones-de-cumplimiento/01-pide-las-verificaciones-de-cumplimiento-claro.webp)

**Variantes**

- **No activas ninguna:** El expediente se aprueba solo con las áreas, como en los flujos sin verificaciones.

**Configuración que lo afecta**

- **Verificaciones de cumplimiento del flujo** (`Configuración › Flujos aprobatorios (/flujos)`): Un expediente de este flujo no se aprueba hasta que estas verificaciones estén satisfechas.

## Paso 2. Define si bloquea y cada cuánto se vuelve a consultar

«Bloquea la aprobación» hace que una verificación pendiente, con coincidencias por analizar, fallida o vencida impida aprobar el expediente. Sin esa marca, la verificación solo informa. «Vigencia (días)» dice cuánto vale una consulta o un estudio antes de vencer; vacío son 30 días. «Reconsultar cada (meses)» es la frecuencia con que se vuelve a consultar al vínculo activo; vacío son 12 meses.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 2: Define si bloquea y cada cuánto se vuelve a consultar (escritorio)](../../../salida/manual/img/configuracion/flujo-verificaciones-de-cumplimiento/02-define-si-bloquea-y-cada-cuanto-se-vuelve-a-cons-claro.webp)

**Variantes**

- **La verificación no bloquea:** Aparece como «Informativa»: se muestra al oficial, pero no impide la aprobación.
- **Dejas la vigencia vacía:** La verificación vence a los 30 días de configuración.

## Paso 3. Guarda las verificaciones en el borrador

Con «Crear borrador» las verificaciones quedan en el borrador v1. Las consultas se hacen cuando el expediente pasa a revisión, no al guardar el flujo; el flujo empieza a usarse al publicarlo.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 3: Guarda las verificaciones en el borrador (escritorio)](../../../salida/manual/img/configuracion/flujo-verificaciones-de-cumplimiento/03-guarda-las-verificaciones-en-el-borrador-claro.webp)

## Recorridos relacionados

- [Configura la matriz de riesgo SAGRILAFT de la empresa](../cumplimiento/matriz-de-riesgo-configuracion.md)
- [Elige con qué proveedor se consultan las listas restrictivas](../cumplimiento/listas-restrictivas-de-la-empresa.md)
