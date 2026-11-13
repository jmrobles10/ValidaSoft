---
id: "revision/aprobar-en-nombre-del-area"
titulo: "Aprueba un área en nombre de sus miembros"
actor: "Administrador de empresa"
dominio: "revision"
resumen: "Ana, administradora de la empresa, no es miembro de Compras y Rubén está de vacaciones. Abre el expediente, usa la acción de administrador para aprobar el área en nombre de Compras y deja escrita la razón."
pasos: 3
---

# Aprueba un área en nombre de sus miembros

**Actor:** Administrador de empresa · **Dominio:** Revisión de expedientes

Ana, administradora de la empresa, no es miembro de Compras y Rubén está de vacaciones. Abre el expediente, usa la acción de administrador para aprobar el área en nombre de Compras y deja escrita la razón.

**Antes de empezar**

- Un grupo con Constructora Norte, con el área Compras (miembro: Rubén) y Ana como administradora que no pertenece a ningún área.
- Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión y Compras no ha decidido.

**Pasos**

1. Abre las acciones de administrador del área
2. Escribe la justificación
3. Revisa el veredicto registrado

## Paso 1. Abre las acciones de administrador del área

Como administradora no eres miembro de Compras, así que no tienes el botón «Aprobar área». En la sección de Compras aparece el botón de acciones de administrador, con la opción de aprobar en nombre del área. Tu cuenta de administradora no es miembro de ningún área: lo que puedes hacer en un área es lo que te da el menú de administrador.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre las acciones de administrador del área (escritorio)](../../../salida/manual/img/revision/aprobar-en-nombre-del-area/01-abre-las-acciones-de-administrador-del-area-claro.webp)

**Variantes**

- **Eres miembro del área:** Decides con el botón «Aprobar área» como cualquier miembro; el menú de administrador no te cambia nada.

## Paso 2. Escribe la justificación

El diálogo explica que la aprobación queda en el historial con tu nombre y la justificación que escribas. La justificación es obligatoria: sin ella no se puede aprobar. Úsala para decir por qué el área no decide por sí misma; quien revise el historial después verá esa razón.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Escribe la justificación (escritorio)](../../../salida/manual/img/revision/aprobar-en-nombre-del-area/02-escribe-la-justificacion-claro.webp)

## Paso 3. Revisa el veredicto registrado

El área Compras queda «Aprobada» y su historial muestra que el veredicto fue en nombre del área, con tu nombre. Cumplimiento sigue decidiendo por su cuenta: aprobar en nombre de un área no cierra las demás. Si fue un error, el veredicto se puede anular desde el mismo historial.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Revisa el veredicto registrado (escritorio)](../../../salida/manual/img/revision/aprobar-en-nombre-del-area/03-revisa-el-veredicto-registrado-claro.webp)

## Recorridos relacionados

- [Aprueba el área de Compras](aprobar-un-area.md)
- [Reabre una etapa ya aprobada](desaprobar-y-reabrir-una-etapa.md)
