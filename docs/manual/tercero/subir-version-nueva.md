---
id: "tercero/subir-version-nueva"
titulo: "Sube una versión nueva de un documento ya aprobado"
actor: "Tercero"
dominio: "tercero"
resumen: "El tercero tiene su vínculo aprobado. Cuando su RUT cambia, sube otra versión desde la tarjeta del documento: el documento vuelve a «En revisión» y la empresa lo revisa de nuevo, sin que se pierda lo aprobado."
pasos: 3
---

# Sube una versión nueva de un documento ya aprobado

**Actor:** Tercero · **Dominio:** Portal del tercero

El tercero tiene su vínculo aprobado. Cuando su RUT cambia, sube otra versión desde la tarjeta del documento: el documento vuelve a «En revisión» y la empresa lo revisa de nuevo, sin que se pierda lo aprobado.

**Antes de empezar**

- Un grupo empresarial con «Constructora Sur S.A.S.», su flujo de Proveedor (RUT y certificado de seguridad y salud) y un área «Revisión» con una persona como miembro.
- Un tercero cuyo vínculo con Sur ya fue aprobado por esa área, con el RUT en su versión 1.

**Pasos**

1. Verifica que tu vínculo está aprobado
2. Abre tu bóveda y elige «Subir versión» del RUT
3. Confirma que la versión nueva está en revisión

## Paso 1. Verifica que tu vínculo está aprobado

La frase de arriba dice que la empresa aprobó tu información y que tu vínculo está activo. Cada documento aprobado sigue visible en «Lo que te pide», con su versión y su vigencia. Un documento aprobado puede cambiar en cualquier momento: si subes una versión nueva, la empresa la revisa de nuevo.

> **Pantalla:** `/expediente/{id}`

![Paso 1: Verifica que tu vínculo está aprobado (escritorio)](../../../salida/manual/img/tercero/subir-version-nueva/01-verifica-que-tu-vinculo-esta-aprobado-claro.webp)

![Paso 1: Verifica que tu vínculo está aprobado (celular)](../../../salida/manual/img/tercero/subir-version-nueva/01-verifica-que-tu-vinculo-esta-aprobado-celular.webp)

**Variantes**

- **La empresa aprobó tu información pero un documento vence:** El documento aparece como «Por vencer» o «Vencido» y debes subir uno vigente.

## Paso 2. Abre tu bóveda y elige «Subir versión» del RUT

En «Mi bóveda» cada documento aparece una sola vez, con su versión vigente y, en «Con tus empresas», la versión que cada una aprobó. Con «Subir versión» llevas el archivo nuevo a la pantalla de carga del documento. Al guardarlo, Validasoft lo registra como la versión 2 y las empresas que usan el RUT reciben el cambio para revisarlo.

> **Pantalla:** `/boveda`

![Paso 2: Abre tu bóveda y elige «Subir versión» del RUT (escritorio)](../../../salida/manual/img/tercero/subir-version-nueva/02-abre-tu-boveda-y-elige-subir-version-del-rut-claro.webp)

![Paso 2: Abre tu bóveda y elige «Subir versión» del RUT (celular)](../../../salida/manual/img/tercero/subir-version-nueva/02-abre-tu-boveda-y-elige-subir-version-del-rut-celular.webp)

**Configuración que lo afecta**

- **Vigencia y reglas del tipo de documento** (`Configuración › Tipos de documento (/tipos-documento)`)

## Paso 3. Confirma que la versión nueva está en revisión

El RUT aparece con «Versión 2» y estado «En revisión»: la empresa lo revisa de nuevo. Si la empresa lo observa, te llega un correo con los motivos y lo corriges desde la misma tarjeta.

> **Pantalla:** `/expediente/{id}`

![Paso 3: Confirma que la versión nueva está en revisión (escritorio)](../../../salida/manual/img/tercero/subir-version-nueva/03-confirma-que-la-version-nueva-esta-en-revision-claro.webp)

![Paso 3: Confirma que la versión nueva está en revisión (celular)](../../../salida/manual/img/tercero/subir-version-nueva/03-confirma-que-la-version-nueva-esta-en-revision-celular.webp)

## Recorridos relacionados

- [Ve que tu expediente quedó aprobado](expediente-aprobado.md)
- [Corrige lo que la empresa observó y responde](responder-observaciones.md)
