---
id: "publico/verificar-certificado-autentico"
titulo: "Verifica que un certificado es auténtico"
actor: "Público"
dominio: "publico"
resumen: "Cualquier persona con el enlace del código QR o el folio de un certificado vigente lo consulta sin iniciar sesión: ve quién lo emite, a quién certifica y si la firma de Validasoft corresponde a su contenido, y descarga el PDF."
pasos: 3
---

# Verifica que un certificado es auténtico

**Actor:** Público · **Dominio:** Verificación pública

Cualquier persona con el enlace del código QR o el folio de un certificado vigente lo consulta sin iniciar sesión: ve quién lo emite, a quién certifica y si la firma de Validasoft corresponde a su contenido, y descarga el PDF.

**Antes de empezar**

- Un proveedor aprobado por Constructora Sur (una sola área) que queda con su certificado vigente.
- La consulta se hace sin sesión, desde una ventana nueva del navegador.

**Pasos**

1. Lee el estado del certificado
2. Comprueba la autenticidad
3. Descarga el PDF del certificado

## Paso 1. Lee el estado del certificado

La página abre con el estado: «Este certificado está vigente» y la empresa que validó al proveedor. Debajo están los datos del certificado: la empresa que lo emite, el titular, el folio, la fecha de emisión y hasta cuándo vale. El NIT aparece enmascarado y el correo del titular no se muestra; la consulta no te da datos que no necesitas para confirmar el estado.

> **Pantalla:** `/verificar/SF09A9-C-2026-000001-1`

![Paso 1: Lee el estado del certificado (escritorio)](../../../salida/manual/img/publico/verificar-certificado-autentico/01-lee-el-estado-del-certificado-claro.webp)

**Variantes**

- **El certificado tiene fecha de vencimiento:** Aparece «Vence el» con la fecha; si no tiene, dice que no tiene fecha de vencimiento.
- **El certificado ya venció:** El estado cambia a «Este certificado venció» y el PDF ya no se entrega.

## Paso 2. Comprueba la autenticidad

La sección «Autenticidad» la comprueba tu propio navegador: recalcula el contenido sellado, compara su huella con la del enlace y valida la firma con la clave pública de Validasoft. Si las cuatro comprobaciones salen en «Correcto», el mensaje dice «Auténtico»; si alguna falla, el certificado no debe aceptarse.

> **Pantalla:** `/verificar/SF09A9-C-2026-000001-1`

![Paso 2: Comprueba la autenticidad (escritorio)](../../../salida/manual/img/publico/verificar-certificado-autentico/02-comprueba-la-autenticidad-claro.webp)

![Paso 2: Comprueba la autenticidad (celular)](../../../salida/manual/img/publico/verificar-certificado-autentico/02-comprueba-la-autenticidad-celular.webp)

**Variantes**

- **Tu navegador no soporta la verificación:** La sección dice que no puede verificar el sello y te pide abrirlo en una versión reciente de Chrome, Edge, Firefox o Safari.
- **El contenido fue alterado:** El mensaje dice «No confíes en este certificado: la verificación falló».

## Paso 3. Descarga el PDF del certificado

El botón «Descargar PDF» entrega el certificado tal como se emitió. Solo se entrega mientras el certificado está vigente: si está suspendido o revocado, el botón no baja el archivo y la página lo explica.

> **Pantalla:** `/verificar/SF09A9-C-2026-000001-1`

![Paso 3: Descarga el PDF del certificado (escritorio)](../../../salida/manual/img/publico/verificar-certificado-autentico/03-descarga-el-pdf-del-certificado-claro.webp)

## Recorridos relacionados

- [Lee el estado de un certificado suspendido o revocado](certificado-en-otros-estados.md)
- [Compara la huella impresa en el PDF con la del certificado](verificar-con-el-pdf.md)
