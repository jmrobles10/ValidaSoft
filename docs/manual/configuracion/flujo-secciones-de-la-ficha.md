---
id: "configuracion/flujo-secciones-de-la-ficha"
titulo: "Pide secciones de «Mi información» como requisito del flujo"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora agrega a un flujo de proveedores dos secciones de «Mi información»: la financiera, por periodos, y la legal, solo para personas jurídicas. Define la vigencia y el plazo de revalidación de cada una y guarda el borrador."
pasos: 4
---

# Pide secciones de «Mi información» como requisito del flujo

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora agrega a un flujo de proveedores dos secciones de «Mi información»: la financiera, por periodos, y la legal, solo para personas jurídicas. Define la vigencia y el plazo de revalidación de cada una y guarda el borrador.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.», su área «Revisión» con miembros y los tipos de documento base.
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Agrega la sección financiera
2. Define periodos, soporte, vigencia y revalidación
3. Agrega la sección legal
4. Guarda el flujo con las secciones

## Paso 1. Agrega la sección financiera

«Agregar sección de la información» pide al tercero una sección de «Mi información» en lugar de un documento: la diligencia una sola vez y la empresa la revisa con las áreas que elijas. La sección financiera se diligencia por periodos: el campo «Periodos» dice cuántos cierres se piden, de 1 a 5. Vacío significa el último cierre.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 1: Agrega la sección financiera (escritorio)](../../../salida/manual/img/configuracion/flujo-secciones-de-la-ficha/01-agrega-la-seccion-financiera-claro.webp)

**Configuración que lo afecta**

- **Secciones de la información como requisito** (`Configuración › Flujos aprobatorios (/flujos)`): El tercero la llena en «Mi información» y la empresa la revisa con las áreas del flujo.

## Paso 2. Define periodos, soporte, vigencia y revalidación

«Exige soporte» pide que la sección venga con un documento que la respalde (por ejemplo, los estados financieros). «Vigencia (días)» dice cuánto vale lo que la empresa acepta: desde esa aceptación vence y se ratifica; vacío significa que no vence. «Plazo de revalidación (días)» es el tiempo que tiene el tercero para resolver una actualización que incluya esta sección; si lo dejas vacío, son 15 días.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 2: Define periodos, soporte, vigencia y revalidación (escritorio)](../../../salida/manual/img/configuracion/flujo-secciones-de-la-ficha/02-define-periodos-soporte-vigencia-y-revalidacion-claro.webp)

**Variantes**

- **La sección no es financiera:** El campo «Periodos» no aparece: solo la información financiera va por periodos.
- **Dejas la vigencia vacía:** La sección «No vence» y no se pide ratificarla.

## Paso 3. Agrega la sección legal

La sección legal (socios, representante legal, revisor fiscal) solo aplica a personas jurídicas. Al elegirla, la pantalla recuerda que a una persona natural no se le pide: el tercero natural nunca la ve. Las secciones de un flujo se pueden mezclar con documentos: cada fila se configura por separado.

> **Pantalla:** `/e/{empresa}/flujos/nuevo`

![Paso 3: Agrega la sección legal (escritorio)](../../../salida/manual/img/configuracion/flujo-secciones-de-la-ficha/03-agrega-la-seccion-legal-claro.webp)

**Variantes**

- **El tercero es persona natural:** La sección legal no se le pide y no aparece en su lista.

## Paso 4. Guarda el flujo con las secciones

Con «Crear borrador» las dos secciones quedan en el borrador v1. Si una fila tiene un dato mal, por ejemplo más de 5 periodos o una vigencia fuera de rango, el editor marca esa fila en rojo y dice qué corregir antes de guardar.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 4: Guarda el flujo con las secciones (escritorio)](../../../salida/manual/img/configuracion/flujo-secciones-de-la-ficha/04-guarda-el-flujo-con-las-secciones-claro.webp)

## Recorridos relacionados

- [Pide cada documento según el tipo de persona](flujo-condiciones-por-tipo-de-persona.md)
- [Define la vigencia periódica del vínculo](flujo-vigencia-del-vinculo.md)
