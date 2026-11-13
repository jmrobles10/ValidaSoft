---
id: "ia/escalamiento-a-modelo-superior"
titulo: "Revisa una lectura dudosa que la IA escaló a un modelo superior"
actor: "Revisor/área"
dominio: "ia"
resumen: "Cuando la lectura de un documento es dudosa, la IA vuelve a analizarlo con un modelo de mayor capacidad. Sofía, de Revisión, ve la confianza final, que el documento fue escalado y que sigue dudoso; la decisión es suya."
pasos: 2
---

# Revisa una lectura dudosa que la IA escaló a un modelo superior

**Actor:** Revisor/área · **Dominio:** IA y revisión automática

Cuando la lectura de un documento es dudosa, la IA vuelve a analizarlo con un modelo de mayor capacidad. Sofía, de Revisión, ve la confianza final, que el documento fue escalado y que sigue dudoso; la decisión es suya.

**Antes de empezar**

- Un grupo con Constructora Sur, la IA activa y revisión automática en nivel Asistido.
- Un cliente que sube un RUT con lectura dudosa (confianza 55 %) y una certificación bancaria limpia.

**Pasos**

1. Revisa la confianza final del análisis
2. Decide el documento escalado

## Paso 1. Revisa la confianza final del análisis

La confianza del análisis aparece junto al documento. Cuando la lectura es dudosa, el sistema la analiza de nuevo con un modelo de mayor capacidad y lo indica con «Revisado por un modelo de mayor capacidad». Aun así, si la confianza sigue baja, el bloque de decisión dice «Confianza baja» y la decisión es de una persona.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Revisa la confianza final del análisis (escritorio)](../../../salida/manual/img/ia/escalamiento-a-modelo-superior/01-revisa-la-confianza-final-del-analisis-claro.webp)

**Configuración que lo afecta**

- **Confianza mínima de la revisión automática** (`Configuración › Revisión automática (/revision-automatica)`): Por debajo de esa confianza, el sistema no sugiere ni aprueba: lo revisa una persona.

## Paso 2. Decide el documento escalado

El escalamiento no aprueba nada: el documento sigue con la sugerencia de la IA y la persona decide. El botón «Aprobar área» queda disponible como siempre. El tercero no ve la confianza, el modelo ni el escalamiento: esa información es solo de la empresa.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Decide el documento escalado (escritorio)](../../../salida/manual/img/ia/escalamiento-a-modelo-superior/02-decide-el-documento-escalado-claro.webp)

## Recorridos relacionados

- [Atiende una excepción de la revisión asistida](revision-asistida-y-excepciones.md)
- [Revisa el plan y el uso de IA de la empresa](plan-y-uso-de-ia.md)
