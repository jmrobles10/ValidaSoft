---
id: "revision/aprobar-un-area"
titulo: "Aprueba el área de Compras"
actor: "Revisor/área"
dominio: "revision"
resumen: "Rubén abre el expediente de un proveedor que llegó a su bandeja, revisa los documentos de su área y aprueba el área Compras. El vínculo solo se activa cuando todas las áreas aprueban."
pasos: 3
---

# Aprueba el área de Compras

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

Rubén abre el expediente de un proveedor que llegó a su bandeja, revisa los documentos de su área y aprueba el área Compras. El vínculo solo se activa cuando todas las áreas aprueban.

**Antes de empezar**

- Un grupo con Constructora Norte, con las áreas Compras y Cumplimiento. Rubén es miembro de Compras y Olga de Cumplimiento.
- Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión.

**Pasos**

1. Abre el expediente del proveedor
2. Confirma la aprobación del área
3. Verifica el estado de las demás áreas

## Paso 1. Abre el expediente del proveedor

El expediente muestra el tercero y, en «Decisión de cada área», una sección por área. Tu área, Compras, tiene el botón «Aprobar área» con los documentos que le quedan por decidir. Las demás áreas aparecen también, pero sin botones de decisión si no eres miembro de ellas.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre el expediente del proveedor (escritorio)](../../../salida/manual/img/revision/aprobar-un-area/01-abre-el-expediente-del-proveedor-claro.webp)

**Variantes**

- **Eres miembro de otra área:** Esa área muestra «No eres miembro de esta área.» y no tiene botones de decisión.
- **Eres administrador y no eres miembro del área:** Puedes aprobar en nombre del área con su menú de administrador; el veredicto queda registrado a tu nombre.

## Paso 2. Confirma la aprobación del área

La confirmación dice cuántos documentos aprueba tu área. Se aprueban en la versión que estás viendo: si el tercero sube una versión nueva después, esa versión vuelve a revisarse. Al confirmar, tu veredicto queda registrado con tu nombre y la fecha.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Confirma la aprobación del área (escritorio)](../../../salida/manual/img/revision/aprobar-un-area/02-confirma-la-aprobacion-del-area-claro.webp)

## Paso 3. Verifica el estado de las demás áreas

Tu área queda como «Aprobada». El tercero sigue «En revisión» mientras falte alguna área: el vínculo se activa solo cuando todas aprueban. Cumplimiento sigue pendiente y decide por su cuenta.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Verifica el estado de las demás áreas (escritorio)](../../../salida/manual/img/revision/aprobar-un-area/03-verifica-el-estado-de-las-demas-areas-claro.webp)

## Recorridos relacionados

- [Revisa lo que te toca desde Inicio](inicio-lo-que-te-toca.md)
- [Aprueba el expediente documento por documento](aprobar-documento-por-documento.md)
- [Observa un documento con motivos](observar-con-motivos.md)
