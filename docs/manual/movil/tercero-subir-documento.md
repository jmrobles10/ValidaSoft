---
id: "movil/tercero-subir-documento"
titulo: "Sube un documento pendiente desde el celular"
actor: "Tercero"
dominio: "movil"
resumen: "Desde el celular, el tercero abre la revisión de una empresa, toca «Subir» en el documento que falta, elige el archivo de su teléfono y vuelve a su revisión con la versión ya guardada en su bóveda."
pasos: 4
---

# Sube un documento pendiente desde el celular

**Actor:** Tercero · **Dominio:** Experiencia móvil

Desde el celular, el tercero abre la revisión de una empresa, toca «Subir» en el documento que falta, elige el archivo de su teléfono y vuelve a su revisión con la versión ya guardada en su bóveda.

**Antes de empezar**

- Un tercero invitado por Constructora Norte que ya entregó el RUT, pero no el certificado de existencia y representación legal.
- La pantalla se ve a 390 × 844 (celular).

**Pasos**

1. Localiza el documento que falta
2. Abre la pantalla para subir el archivo
3. Comprueba la versión guardada
4. Vuelve a tu revisión

## Paso 1. Localiza el documento que falta

En «Tu revisión con» cada requisito aparece como una tarjeta con su nombre y su estado. El certificado de existencia y representación legal está pendiente: la tarjeta trae el botón «Subir» para cargarlo. El RUT ya aparece como entregado y no tienes que volver a subirlo.

> **Pantalla:** `/expediente/{id}`

![Paso 1: Localiza el documento que falta (escritorio)](../../../salida/manual/img/movil/tercero-subir-documento/01-localiza-el-documento-que-falta-claro.webp)

![Paso 1: Localiza el documento que falta (celular)](../../../salida/manual/img/movil/tercero-subir-documento/01-localiza-el-documento-que-falta-celular.webp)

**Variantes**

- **El documento fue observado:** La tarjeta muestra la observación de la empresa y el botón cambia a una nueva versión.
- **Ya subiste el documento y está en revisión:** La tarjeta no ofrece «Subir»: solo muestra que la empresa lo está revisando.

## Paso 2. Abre la pantalla para subir el archivo

La pantalla «Subir» recuerda qué documento es y qué debe traer: formatos, tamaño y, si lo pide, los datos del formulario. Para cargar el archivo, toca «Elegir archivo» y selecciona el PDF guardado en tu teléfono.

> **Pantalla:** `/boveda/subir`

![Paso 2: Abre la pantalla para subir el archivo (escritorio)](../../../salida/manual/img/movil/tercero-subir-documento/02-abre-la-pantalla-para-subir-el-archivo-claro.webp)

![Paso 2: Abre la pantalla para subir el archivo (celular)](../../../salida/manual/img/movil/tercero-subir-documento/02-abre-la-pantalla-para-subir-el-archivo-celular.webp)

## Paso 3. Comprueba la versión guardada

Al terminar de subirlo, la pantalla dice «Es tu versión 1» del documento. Esa versión queda en tu bóveda y es la que la empresa revisa; si la vuelves a subir, se guarda como una versión nueva.

> **Pantalla:** `/boveda/subir`

![Paso 3: Comprueba la versión guardada (escritorio)](../../../salida/manual/img/movil/tercero-subir-documento/03-comprueba-la-version-guardada-claro.webp)

![Paso 3: Comprueba la versión guardada (celular)](../../../salida/manual/img/movil/tercero-subir-documento/03-comprueba-la-version-guardada-celular.webp)

## Paso 4. Vuelve a tu revisión

Con «Volver a mi revisión» regresas a la lista de requisitos de la empresa. La tarjeta del certificado ya muestra el documento que subiste; cuando todos los requisitos están entregados, la revisión sigue con la empresa.

> **Pantalla:** `/expediente/{id}`

![Paso 4: Vuelve a tu revisión (escritorio)](../../../salida/manual/img/movil/tercero-subir-documento/04-vuelve-a-tu-revision-claro.webp)

![Paso 4: Vuelve a tu revisión (celular)](../../../salida/manual/img/movil/tercero-subir-documento/04-vuelve-a-tu-revision-celular.webp)

## Recorridos relacionados

- [Acepta la invitación y completa tu información como persona jurídica](../tercero/aceptar-invitacion-persona-juridica.md)
- [Revisa tus documentos y sus versiones en el celular](tercero-boveda.md)
