---
id: "revision/historial-del-expediente"
titulo: "Lee el historial de un expediente"
actor: "Revisor/área"
dominio: "revision"
resumen: "Un expediente pasó por una observación, una respuesta del proveedor y dos aprobaciones. Ana lo abre y lee el historial: quién hizo cada cosa, qué y cuándo, y la última respuesta del proveedor."
pasos: 2
---

# Lee el historial de un expediente

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

Un expediente pasó por una observación, una respuesta del proveedor y dos aprobaciones. Ana lo abre y lee el historial: quién hizo cada cosa, qué y cuándo, y la última respuesta del proveedor.

**Antes de empezar**

- Un grupo con Constructora Norte, con las áreas Compras (Rubén) y Cumplimiento (Olga). Ana es administradora.
- Un proveedor que subió su RUT y su Cámara; Compras observó el RUT, el proveedor subió la versión 2 y respondió, y las dos áreas aprobaron.

**Pasos**

1. Abre el historial del expediente
2. Revisa la última respuesta del proveedor

## Paso 1. Abre el historial del expediente

El historial es la línea de tiempo del expediente: cada observación, respuesta, aprobación y cambio de estado con quién lo hizo, qué fue y cuándo. Los veredictos de las áreas aparecen con su autor: «Tú» si fuiste tú, la persona del área, o un administrador cuando aprueba en nombre del área. Nada se borra: lo que se corrige queda como revertido o superado.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre el historial del expediente (escritorio)](../../../salida/manual/img/revision/historial-del-expediente/01-abre-el-historial-del-expediente-claro.webp)

**Variantes**

- **Un veredicto fue anulado o reemplazado:** Sigue en el historial con su marca de «Anulado» o «Superado por el reenvío»; no desaparece.

## Paso 2. Revisa la última respuesta del proveedor

Cuando el proveedor responde a una observación, su respuesta aparece en «Última respuesta del tercero» con su texto y la fecha. Si el proveedor reenvió por otro canal y lo hizo el sistema integrado en su nombre, la respuesta lo indica.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Revisa la última respuesta del proveedor (escritorio)](../../../salida/manual/img/revision/historial-del-expediente/02-revisa-la-ultima-respuesta-del-proveedor-claro.webp)

## Recorridos relacionados

- [Observa un documento con motivos](observar-con-motivos.md)
- [Revisa lo que corrigió el proveedor tras una observación](responder-con-lo-corregido.md)
