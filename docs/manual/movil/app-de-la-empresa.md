---
id: "movil/app-de-la-empresa"
titulo: "Revisa un expediente desde el celular"
actor: "Revisor/área"
dominio: "movil"
resumen: "Un revisor de área abre la app de la empresa desde el celular: ve su bandeja de revisión, abre el expediente de un tercero y encuentra el detalle con sus documentos y las decisiones de cada área."
pasos: 2
---

# Revisa un expediente desde el celular

**Actor:** Revisor/área · **Dominio:** Experiencia móvil

Un revisor de área abre la app de la empresa desde el celular: ve su bandeja de revisión, abre el expediente de un tercero y encuentra el detalle con sus documentos y las decisiones de cada área.

**Antes de empezar**

- Un grupo con Constructora Norte, cuya área Compras tiene a Rubén como revisor.
- Un tercero que entregó sus documentos y quedó «En revisión» con Compras, por lo que aparece en la bandeja de Rubén.

**Pasos**

1. Abre tu bandeja de revisión
2. Abre el expediente del tercero

## Paso 1. Abre tu bandeja de revisión

La bandeja lista los expedientes que esperan la decisión de tu área, con el tercero, el tipo de vínculo y el estado. Solo ves los de las áreas de las que eres miembro. En el celular, la app ocupa toda la pantalla y las demás secciones están en el menú «Más».

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 1: Abre tu bandeja de revisión (escritorio)](../../../salida/manual/img/movil/app-de-la-empresa/01-abre-tu-bandeja-de-revision-claro.webp)

![Paso 1: Abre tu bandeja de revisión (celular)](../../../salida/manual/img/movil/app-de-la-empresa/01-abre-tu-bandeja-de-revision-celular.webp)

**Variantes**

- **Eres administrador y no miembro del área:** Ves también las acciones de administrador y puedes decidir en nombre del área.

## Paso 2. Abre el expediente del tercero

El expediente muestra cada área y su decisión, los documentos con su versión y lo que ya decidieron las demás áreas, para que decidas con todo a la vista.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Abre el expediente del tercero (escritorio)](../../../salida/manual/img/movil/app-de-la-empresa/02-abre-el-expediente-del-tercero-claro.webp)

![Paso 2: Abre el expediente del tercero (celular)](../../../salida/manual/img/movil/app-de-la-empresa/02-abre-el-expediente-del-tercero-celular.webp)

## Recorridos relacionados

- [Aprueba el área de Compras](../revision/aprobar-un-area.md)
