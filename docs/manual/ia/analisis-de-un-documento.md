---
id: "ia/analisis-de-un-documento"
titulo: "Lee el análisis automático de un documento"
actor: "Revisor/área"
dominio: "ia"
resumen: "Cada documento que sube el tercero se lee con IA: Validasoft muestra si es legible, qué tipo de documento detectó, los datos que extrajo y su confianza. Sofía, de Revisión, usa ese análisis para decidir, sin que la IA decida por ella."
pasos: 2
---

# Lee el análisis automático de un documento

**Actor:** Revisor/área · **Dominio:** IA y revisión automática

Cada documento que sube el tercero se lee con IA: Validasoft muestra si es legible, qué tipo de documento detectó, los datos que extrajo y su confianza. Sofía, de Revisión, usa ese análisis para decidir, sin que la IA decida por ella.

**Antes de empezar**

- Un grupo con Constructora Sur, la IA activa y un flujo de Cliente que pide el RUT al área Revisión.

**Pasos**

1. Lee el análisis del documento
2. Revisa los datos que extrajo la IA

## Paso 1. Lee el análisis del documento

Debajo del documento aparece el bloque «Revisión automática» con la confianza de la lectura, la fecha y el modelo que lo analizó, si el archivo es legible y el tipo de documento detectado. Si el tipo no coincide con el que se pidió, un aviso lo señala para que revises que el tercero subió el documento correcto.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Lee el análisis del documento (escritorio)](../../../salida/manual/img/ia/analisis-de-un-documento/01-lee-el-analisis-del-documento-claro.webp)

**Variantes**

- **El archivo no es legible:** El análisis dice «No: revisa el archivo a mano» y no se usa para decidir.
- **La confianza está bajo el umbral de la empresa:** Conviene revisar los datos con más cuidado; el sistema no los sugiere ni aprueba.

## Paso 2. Revisa los datos que extrajo la IA

La sección «Datos extraídos» lista los campos que la IA leyó del documento, como el NIT, la razón social, el representante legal o el tipo de contribuyente. Con «Ver más datos» despliegas el resto. Compara estos datos con lo que declaró el tercero antes de aprobar el área.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Revisa los datos que extrajo la IA (escritorio)](../../../salida/manual/img/ia/analisis-de-un-documento/02-revisa-los-datos-que-extrajo-la-ia-claro.webp)

## Recorridos relacionados

- [Compara la información del tercero con su RUT](cruce-de-la-ficha-con-los-soportes.md)
- [Atiende una excepción de la revisión asistida](revision-asistida-y-excepciones.md)
