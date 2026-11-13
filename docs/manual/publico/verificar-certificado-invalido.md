---
id: "publico/verificar-certificado-invalido"
titulo: "Comprueba un certificado que no existe o tiene una huella mal escrita"
actor: "Público"
dominio: "publico"
resumen: "Si el folio o la huella no corresponden a ningún certificado de Validasoft, la página no lo da por bueno: explica qué revisar en el PDF o en el código QR y no muestra datos de ninguna empresa ni de ningún titular."
pasos: 2
---

# Comprueba un certificado que no existe o tiene una huella mal escrita

**Actor:** Público · **Dominio:** Verificación pública

Si el folio o la huella no corresponden a ningún certificado de Validasoft, la página no lo da por bueno: explica qué revisar en el PDF o en el código QR y no muestra datos de ninguna empresa ni de ningún titular.

**Antes de empezar**

- Un grupo empresarial registrado en Validasoft, cuyo prefijo de folio se usa para armar un folio que no existe.
- La consulta se hace sin sesión, desde una ventana nueva del navegador.

**Pasos**

1. Consulta un folio que no existe
2. Consulta con una huella mal escrita

## Paso 1. Consulta un folio que no existe

Cuando el folio no corresponde a ningún certificado, la página dice «No encontramos este certificado» y te pide revisar que el folio y la huella estén completos, como aparecen en el PDF. Si escaneaste el código QR, puede que el documento no sea un certificado de Validasoft. La página no muestra datos de ninguna empresa ni de ningún titular.

> **Pantalla:** `/verificar/S7682C-C-2026-999999-1`

![Paso 1: Consulta un folio que no existe (escritorio)](../../../salida/manual/img/publico/verificar-certificado-invalido/01-consulta-un-folio-que-no-existe-claro.webp)

## Paso 2. Consulta con una huella mal escrita

La huella tiene 12 caracteres hexadecimales (0 a 9 y de la A a la F), por ejemplo 5a7e·0c1d·9f22. Si la escribes con otros caracteres, la página dice «La huella no es válida» y muestra un ejemplo. Revisa el enlace o vuelve a escanear el código QR del PDF; no hace falta iniciar sesión para intentarlo de nuevo.

> **Pantalla:** `/verificar/S7682C-C-2026-999999-1`

![Paso 2: Consulta con una huella mal escrita (escritorio)](../../../salida/manual/img/publico/verificar-certificado-invalido/02-consulta-con-una-huella-mal-escrita-claro.webp)

**Variantes**

- **Hay más de un certificado con el mismo folio y no traes la huella:** La página dice «Falta la huella» y pide abrir el enlace completo del código QR, que la incluye.

## Recorridos relacionados

- [Verifica que un certificado es auténtico](verificar-certificado-autentico.md)
