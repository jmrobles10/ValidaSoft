---
id: "movil/tercero-estado-de-la-revision"
titulo: "Revisa en el celular en qué va tu validación con cada empresa"
actor: "Tercero"
dominio: "movil"
resumen: "En el celular, Mis empresas muestra el estado de cada validación. El tercero abre una de ellas y ve, en una frase, en qué va la revisión y, en tarjetas, cada documento con su estado."
pasos: 3
---

# Revisa en el celular en qué va tu validación con cada empresa

**Actor:** Tercero · **Dominio:** Experiencia móvil

En el celular, Mis empresas muestra el estado de cada validación. El tercero abre una de ellas y ve, en una frase, en qué va la revisión y, en tarjetas, cada documento con su estado.

**Antes de empezar**

- Un tercero invitado por Constructora Norte y por Constructora Sur, que entregó sus documentos y quedó «En revisión» en ambas.
- La pantalla se ve a 390 × 844 (celular).

**Pasos**

1. Mira el estado de cada empresa
2. Lee la frase de estado
3. Revisa cada documento

## Paso 1. Mira el estado de cada empresa

Cada tarjeta de Mis empresas dice en qué va tu validación con esa empresa: «En revisión», «Observado», «Aprobado» u otro estado. Un toque sobre la tarjeta abre su revisión.

> **Pantalla:** `/empresas`

![Paso 1: Mira el estado de cada empresa (escritorio)](../../../salida/manual/img/movil/tercero-estado-de-la-revision/01-mira-el-estado-de-cada-empresa-claro.webp)

![Paso 1: Mira el estado de cada empresa (celular)](../../../salida/manual/img/movil/tercero-estado-de-la-revision/01-mira-el-estado-de-cada-empresa-celular.webp)

**Variantes**

- **La empresa te pidió corregir algo:** La tarjeta cambia a «Observado» y te lleva a corregirlo al abrirla.
- **Tu validación está aprobada:** La tarjeta dice «Aprobado» y tu vínculo queda activo.

## Paso 2. Lee la frase de estado

Arriba de la revisión, una frase dice en qué va: «Constructora Norte S.A.S. está revisando tu información». Debajo aparece el folio de tu proceso y la fecha de la última actualización.

> **Pantalla:** `/expediente/{id}`

![Paso 2: Lee la frase de estado (escritorio)](../../../salida/manual/img/movil/tercero-estado-de-la-revision/02-lee-la-frase-de-estado-claro.webp)

![Paso 2: Lee la frase de estado (celular)](../../../salida/manual/img/movil/tercero-estado-de-la-revision/02-lee-la-frase-de-estado-celular.webp)

**Variantes**

- **Te falta subir documentos:** La frase dice cuántos documentos faltan y te invita a subirlos.

## Paso 3. Revisa cada documento

Cada documento que te pide la empresa es una tarjeta con su estado: «Cargado», «Vigente» o «Pendiente». Si una fecha de vencimiento se acerca, la tarjeta lo dice con «Vence el» y la fecha.

> **Pantalla:** `/expediente/{id}`

![Paso 3: Revisa cada documento (escritorio)](../../../salida/manual/img/movil/tercero-estado-de-la-revision/03-revisa-cada-documento-claro.webp)

![Paso 3: Revisa cada documento (celular)](../../../salida/manual/img/movil/tercero-estado-de-la-revision/03-revisa-cada-documento-celular.webp)

## Recorridos relacionados

- [Sube un documento pendiente desde el celular](tercero-subir-documento.md)
- [Lee tus avisos desde la campana en el celular](tercero-campana.md)
