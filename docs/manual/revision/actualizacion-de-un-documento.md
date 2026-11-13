---
id: "revision/actualizacion-de-un-documento"
titulo: "Revisa una versión nueva que llega mientras decides"
actor: "Revisor/área"
dominio: "revision"
resumen: "Olga está a punto de aprobar el área Cumplimiento sobre el RUT versión 1 cuando el proveedor sube la versión 2. La pantalla se entera sola, el diálogo deja de permitir la aprobación y Olga aprueba sobre la versión nueva."
pasos: 3
---

# Revisa una versión nueva que llega mientras decides

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

Olga está a punto de aprobar el área Cumplimiento sobre el RUT versión 1 cuando el proveedor sube la versión 2. La pantalla se entera sola, el diálogo deja de permitir la aprobación y Olga aprueba sobre la versión nueva.

**Antes de empezar**

- Un grupo con Constructora Norte, con Cumplimiento (Olga es miembro) y Compras.
- Un proveedor invitado por Norte que ya entregó su RUT (versión 1) y su Cámara de comercio: su expediente está en revisión.

**Pasos**

1. Confirma la aprobación con la versión que ves
2. Entérate de que hubo una versión nueva
3. Aprueba la versión vigente

## Paso 1. Confirma la aprobación con la versión que ves

El diálogo muestra los documentos que aprobarás, cada uno con su versión: aquí, el RUT versión 1. La aprobación se hace sobre la versión que ves en pantalla. Si el proveedor sube otra versión mientras el diálogo está abierto, la pantalla se actualiza sola.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Confirma la aprobación con la versión que ves (escritorio)](../../../salida/manual/img/revision/actualizacion-de-un-documento/01-confirma-la-aprobacion-con-la-version-que-ves-claro.webp)

## Paso 2. Entérate de que hubo una versión nueva

Aparece el aviso «El expediente cambió y esta decisión ya no aplica a su estado actual» y el botón de aprobar queda deshabilitado. No puedes aprobar una versión que ya no es la vigente: cierra el diálogo y vuelve a decidir sobre la nueva.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Entérate de que hubo una versión nueva (escritorio)](../../../salida/manual/img/revision/actualizacion-de-un-documento/02-enterate-de-que-hubo-una-version-nueva-claro.webp)

## Paso 3. Aprueba la versión vigente

La sección del área ya muestra la versión 2 del RUT. Aprueba el área sobre esa versión: el veredicto queda registrado sobre la v2 y no sobre la anterior. La versión 1 deja de contar para Cumplimiento.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Aprueba la versión vigente (escritorio)](../../../salida/manual/img/revision/actualizacion-de-un-documento/03-aprueba-la-version-vigente-claro.webp)

## Recorridos relacionados

- [Aprueba el expediente documento por documento](aprobar-documento-por-documento.md)
- [Revisa lo que corrigió el proveedor tras una observación](responder-con-lo-corregido.md)
