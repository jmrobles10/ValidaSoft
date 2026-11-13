---
id: "revision/revalidaciones-en-lote"
titulo: "Aprueba en lote las revalidaciones sin cambios"
actor: "Revisor/área"
dominio: "revision"
resumen: "Un cliente ya aprobado sube una versión nueva de su RUT y de su certificación bancaria con los mismos datos, y otro cambia su razón social. Sofía revisa la lista de revalidaciones, selecciona en lote las que no cambiaron y deja aparte la que sí cambió."
pasos: 3
---

# Aprueba en lote las revalidaciones sin cambios

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

Un cliente ya aprobado sube una versión nueva de su RUT y de su certificación bancaria con los mismos datos, y otro cambia su razón social. Sofía revisa la lista de revalidaciones, selecciona en lote las que no cambiaron y deja aparte la que sí cambió.

**Antes de empezar**

- Un grupo con Constructora Sur, con el área Revisión (Sofía es miembro) y el análisis de documentos con IA activo.
- Dos clientes ya aprobados con RUT y certificación bancaria. Ambos suben versiones nuevas: uno con los mismos datos y otro con una razón social distinta.

**Pasos**

1. Revisa la lista de revalidaciones
2. Aprueba las revalidaciones sin cambios en lote
3. Revisa lo que queda pendiente

## Paso 1. Revisa la lista de revalidaciones

Cada versión nueva de un documento ya aprobado aparece en Revalidaciones con el resultado de la comparación: «Sin cambios» si los datos son los mismos, o «Con cambios» si cambió algo, con la versión anterior y la nueva. Las que dicen «Con cambios» se revisan una por una, en el expediente.

> **Pantalla:** `/e/{empresa}/revalidaciones`

![Paso 1: Revisa la lista de revalidaciones (escritorio)](../../../salida/manual/img/revision/revalidaciones-en-lote/01-revisa-la-lista-de-revalidaciones-claro.webp)

## Paso 2. Aprueba las revalidaciones sin cambios en lote

«Seleccionar las sin cambios» marca solo las que no cambiaron; la que sí cambió queda deshabilitada y no entra al lote. El diálogo dice que cada revalidación tendrá su propio registro en el historial del expediente.

> **Pantalla:** `/e/{empresa}/revalidaciones`

![Paso 2: Aprueba las revalidaciones sin cambios en lote (escritorio)](../../../salida/manual/img/revision/revalidaciones-en-lote/02-aprueba-las-revalidaciones-sin-cambios-en-lote-claro.webp)

## Paso 3. Revisa lo que queda pendiente

Las revalidaciones aprobadas salen de la lista. La que cambió sigue aquí, para revisarla en su expediente antes de aprobarla.

> **Pantalla:** `/e/{empresa}/revalidaciones`

![Paso 3: Revisa lo que queda pendiente (escritorio)](../../../salida/manual/img/revision/revalidaciones-en-lote/03-revisa-lo-que-queda-pendiente-claro.webp)

## Recorridos relacionados

- [Revisa una versión nueva que llega mientras decides](actualizacion-de-un-documento.md)
- [Exime a un tercero de un documento](eximir-un-documento.md)
