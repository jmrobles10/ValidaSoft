---
id: "revision/anular-un-veredicto"
titulo: "Anula el veredicto de un área"
actor: "Administrador de empresa"
dominio: "revision"
resumen: "Cumplimiento aprobó por error el expediente de un aliado, antes de que su revisión estuviera completa. Ana, administradora, anula ese veredicto desde el menú del área: el área vuelve a estar pendiente y la decisión queda registrada con la justificación."
pasos: 3
---

# Anula el veredicto de un área

**Actor:** Administrador de empresa · **Dominio:** Revisión de expedientes

Cumplimiento aprobó por error el expediente de un aliado, antes de que su revisión estuviera completa. Ana, administradora, anula ese veredicto desde el menú del área: el área vuelve a estar pendiente y la decisión queda registrada con la justificación.

**Antes de empezar**

- Un grupo con Constructora Norte, con las áreas Compras (Rubén) y Cumplimiento (Olga), que revisan en la misma etapa. Ana es administradora.
- Un expediente de un aliado en el que Cumplimiento ya emitió un veredicto de aprobación y Compras aún no decide.

**Pasos**

1. Localiza el veredicto que quieres anular
2. Anula el veredicto con justificación
3. Verifica que el área volvió a estar pendiente

## Paso 1. Localiza el veredicto que quieres anular

En la tarjeta del área aparece su veredicto con quién lo emitió y cuándo. Las acciones de administrador de esa área están en su menú, con «Anular veredicto». Anular deja sin efecto el veredicto más reciente del área; no borra el historial.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Localiza el veredicto que quieres anular (escritorio)](../../../salida/manual/img/revision/anular-un-veredicto/01-localiza-el-veredicto-que-quieres-anular-claro.webp)

**Variantes**

- **El veredicto era una observación:** Anularla reanuda la revisión sin esperar a que el proveedor responda, si era la última observación pendiente.
- **No eres administrador:** No ves el menú de acciones de administrador en el área.

## Paso 2. Anula el veredicto con justificación

El diálogo dice qué veredicto se va a anular, con su tipo. La justificación es obligatoria y queda en el historial con tu nombre. Al confirmar, el área vuelve a estar pendiente y puede volver a decidir.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Anula el veredicto con justificación (escritorio)](../../../salida/manual/img/revision/anular-un-veredicto/02-anula-el-veredicto-con-justificacion-claro.webp)

## Paso 3. Verifica que el área volvió a estar pendiente

Cumplimiento ya no aparece como aprobada: su veredicto queda como «Anulado» en el historial del área y el área puede decidir otra vez. El expediente sigue en revisión hasta que todas las áreas aprueben.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Verifica que el área volvió a estar pendiente (escritorio)](../../../salida/manual/img/revision/anular-un-veredicto/03-verifica-que-el-area-volvio-a-estar-pendiente-claro.webp)

## Recorridos relacionados

- [Reabre una etapa ya aprobada](desaprobar-y-reabrir-una-etapa.md)
- [Aprueba el área de Compras](aprobar-un-area.md)
