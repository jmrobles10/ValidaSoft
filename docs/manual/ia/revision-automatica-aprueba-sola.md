---
id: "ia/revision-automatica-aprueba-sola"
titulo: "Revisa un documento que el sistema aprobó solo"
actor: "Revisor/área"
dominio: "ia"
resumen: "Con el nivel Automático, el RUT limpio de un cliente lo aprueba el sistema sin que nadie lo vea. Sofía, de Revisión, abre el expediente y encuentra la decisión automática, con su confianza y quién la tomó en el historial."
pasos: 2
---

# Revisa un documento que el sistema aprobó solo

**Actor:** Revisor/área · **Dominio:** IA y revisión automática

Con el nivel Automático, el RUT limpio de un cliente lo aprueba el sistema sin que nadie lo vea. Sofía, de Revisión, abre el expediente y encuentra la decisión automática, con su confianza y quién la tomó en el historial.

**Antes de empezar**

- Un grupo con Constructora Sur, la IA activa, un flujo de Cliente que pide el RUT al área Revisión y la revisión automática en nivel Automático con muestra 0 %.
- Un cliente que sube un RUT con lectura limpia (confianza 97 %).

**Pasos**

1. Abre el expediente aprobado por el sistema
2. Revisa quién registró la aprobación

## Paso 1. Abre el expediente aprobado por el sistema

El expediente ya está aprobado para el área Revisión, pero no lo aprobó una persona. El bloque «Decisión automática» dice que el sistema aprobó el RUT porque la lectura fue limpia y no tuvo excepciones, y muestra la confianza del análisis.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre el expediente aprobado por el sistema (escritorio)](../../../salida/manual/img/ia/revision-automatica-aprueba-sola/01-abre-el-expediente-aprobado-por-el-sistema-claro.webp)

**Variantes**

- **La lectura del documento es dudosa:** El sistema no lo aprueba: el documento pasa a Excepciones y una persona decide.
- **El nivel es Asistido o Manual:** Ningún documento lo aprueba el sistema; la decisión la toma una persona.

## Paso 2. Revisa quién registró la aprobación

En el historial del expediente, la aprobación del RUT figura como «Sistema (IA)», no como una persona. Así queda claro que nadie lo vio antes de aprobarse.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Revisa quién registró la aprobación (escritorio)](../../../salida/manual/img/ia/revision-automatica-aprueba-sola/02-revisa-quien-registro-la-aprobacion-claro.webp)

## Recorridos relacionados

- [Elige el nivel de revisión automática con IA](revision-automatica-niveles.md)
- [Atiende una excepción de la revisión asistida](revision-asistida-y-excepciones.md)
