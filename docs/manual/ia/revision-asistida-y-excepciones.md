---
id: "ia/revision-asistida-y-excepciones"
titulo: "Atiende una excepción de la revisión asistida"
actor: "Revisor/área"
dominio: "ia"
resumen: "Con el nivel Asistido la IA lee el RUT y sugiere, pero nunca aprueba sola. Cuando la lectura es dudosa, la IA sugiere revisar y muestra el motivo en el expediente; Sofía, de Revisión, lo abre y decide ella."
pasos: 2
---

# Atiende una excepción de la revisión asistida

**Actor:** Revisor/área · **Dominio:** IA y revisión automática

Con el nivel Asistido la IA lee el RUT y sugiere, pero nunca aprueba sola. Cuando la lectura es dudosa, la IA sugiere revisar y muestra el motivo en el expediente; Sofía, de Revisión, lo abre y decide ella.

**Antes de empezar**

- Un grupo con Constructora Sur, la IA activa, un flujo de Cliente que pide el RUT al área Revisión y la revisión automática en nivel Asistido.
- Un cliente que sube un RUT con lectura dudosa (confianza 55 %).

**Pasos**

1. Abre el expediente con la sugerencia de la IA
2. Decide el área después de revisar el documento

## Paso 1. Abre el expediente con la sugerencia de la IA

En Asistido, la IA lee el documento y deja en cada expediente su sugerencia y su confianza. Cuando la lectura es dudosa, el bloque «Decisión automática» dice «La IA sugiere revisar» y el motivo, por ejemplo «Confianza baja». La IA nunca aprueba sola: la decisión es de quien revisa.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre el expediente con la sugerencia de la IA (escritorio)](../../../salida/manual/img/ia/revision-asistida-y-excepciones/01-abre-el-expediente-con-la-sugerencia-de-la-ia-claro.webp)

**Variantes**

- **La lectura es limpia:** La sugerencia aparece igual, pero la aprobación sigue siendo de una persona.

## Paso 2. Decide el área después de revisar el documento

Revisa el documento y sus datos extraídos. Si todo está bien, «Aprobar área» registra la decisión de Revisión con tu nombre. Si no, usa las acciones de observar o rechazar del área como con cualquier expediente.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Decide el área después de revisar el documento (escritorio)](../../../salida/manual/img/ia/revision-asistida-y-excepciones/02-decide-el-area-despues-de-revisar-el-documento-claro.webp)

## Recorridos relacionados

- [Elige el nivel de revisión automática con IA](revision-automatica-niveles.md)
- [Revisa un documento que el sistema aprobó solo](revision-automatica-aprueba-sola.md)
