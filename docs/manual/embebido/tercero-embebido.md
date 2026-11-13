---
id: "embebido/tercero-embebido"
titulo: "Muestra la bóveda y la ficha del tercero dentro de tu sistema"
actor: "Tercero"
dominio: "embebido"
resumen: "Un sistema ajeno a Validasoft muestra al tercero su bóveda de documentos y su ficha dentro de sus propias pantallas. El tercero no ve marca, menú ni inicio de sesión de Validasoft: el sistema pide una sesión corta para cada pantalla y la entrega al marco."
pasos: 3
---

# Muestra la bóveda y la ficha del tercero dentro de tu sistema

**Actor:** Tercero · **Dominio:** Uso embebido

Un sistema ajeno a Validasoft muestra al tercero su bóveda de documentos y su ficha dentro de sus propias pantallas. El tercero no ve marca, menú ni inicio de sesión de Validasoft: el sistema pide una sesión corta para cada pantalla y la entrega al marco.

**Antes de empezar**

- Un grupo con Constructora Norte, un tercero que aceptó su invitación de cliente, y el sistema integrado E2E con su página de ejemplo.

**Pasos**

1. Abre la pantalla del tercero en tu sistema
2. Revisa la bóveda dentro de tu sistema
3. Abre la ficha del mismo tercero

## Paso 1. Abre la pantalla del tercero en tu sistema

La página de tu sistema tiene una lista con las pantallas de Validasoft que puede mostrar al tercero: la bóveda de documentos, el expediente y la ficha. Eliges una y pulsas «Abrir». Tu sistema pide para esa pantalla una sesión a su backend, que la pide a Validasoft con su llave de API.

> **Pantalla:** `/`

![Paso 1: Abre la pantalla del tercero en tu sistema (escritorio)](../../../salida/manual/img/embebido/tercero-embebido/01-abre-la-pantalla-del-tercero-en-tu-sistema-claro.webp)

![Paso 1: Abre la pantalla del tercero en tu sistema (celular)](../../../salida/manual/img/embebido/tercero-embebido/01-abre-la-pantalla-del-tercero-en-tu-sistema-celular.webp)

**Variantes**

- **Pides una pantalla de un origen que no registraste:** Validasoft no emite la sesión (403 «origen-no-permitido») y la pantalla no se abre.

## Paso 2. Revisa la bóveda dentro de tu sistema

La bóveda aparece en el marco de tu sistema, sin marca, menú ni botón de salir de Validasoft, y sin pedirle al tercero el correo ni una contraseña. El tercero ve sus documentos y puede subir los que le pida la empresa. Tu sistema recibe los mensajes de la pantalla, como el tamaño para ajustar el marco.

> **Pantalla:** `/boveda`

![Paso 2: Revisa la bóveda dentro de tu sistema (escritorio)](../../../salida/manual/img/embebido/tercero-embebido/02-revisa-la-boveda-dentro-de-tu-sistema-claro.webp)

![Paso 2: Revisa la bóveda dentro de tu sistema (celular)](../../../salida/manual/img/embebido/tercero-embebido/02-revisa-la-boveda-dentro-de-tu-sistema-celular.webp)

## Paso 3. Abre la ficha del mismo tercero

La ficha es «Mi información»: los datos generales, tributarios y financieros del tercero. Es la misma persona, pero cada pantalla tiene su propia sesión corta: tu sistema pidió una sesión para la bóveda y otra para la ficha, y las dos vencen en 10 minutos.

> **Pantalla:** `/ficha`

![Paso 3: Abre la ficha del mismo tercero (escritorio)](../../../salida/manual/img/embebido/tercero-embebido/03-abre-la-ficha-del-mismo-tercero-claro.webp)

![Paso 3: Abre la ficha del mismo tercero (celular)](../../../salida/manual/img/embebido/tercero-embebido/03-abre-la-ficha-del-mismo-tercero-celular.webp)

## Recorridos relacionados

- [Abre la app de tu empresa dentro de tu sistema](app-de-la-empresa-en-un-iframe.md)
- [Acepta la invitación y completa tu información como persona jurídica](../tercero/aceptar-invitacion-persona-juridica.md)
- [Entra a una persona de tu sistema con identidad delegada](../integracion/identidad-delegada.md)
