---
id: "movil/verificar-certificado"
titulo: "Verifica un certificado desde el celular"
actor: "Público"
dominio: "movil"
resumen: "Quien escanea el código QR de un certificado con el celular llega a la página de verificación: ve el estado, comprueba la autenticidad en su propio teléfono y descarga el PDF, sin iniciar sesión."
pasos: 3
---

# Verifica un certificado desde el celular

**Actor:** Público · **Dominio:** Experiencia móvil

Quien escanea el código QR de un certificado con el celular llega a la página de verificación: ve el estado, comprueba la autenticidad en su propio teléfono y descarga el PDF, sin iniciar sesión.

**Antes de empezar**

- Un proveedor aprobado por Constructora Sur con su certificado vigente.
- Quien verifica escanea el enlace del código QR desde un celular, sin sesión.

**Pasos**

1. Lee el estado del certificado
2. Comprueba la autenticidad en tu teléfono
3. Descarga el PDF del certificado

## Paso 1. Lee el estado del certificado

Al escanear el QR se abre la verificación con el estado en grande: «Este certificado está vigente» y la empresa que validó al proveedor. Debajo, los datos del certificado en una columna: titular, folio, fecha de emisión y hasta cuándo vale.

> **Pantalla:** `/verificar/SMA643-C-2026-000001-1`

![Paso 1: Lee el estado del certificado (escritorio)](../../../salida/manual/img/movil/verificar-certificado/01-lee-el-estado-del-certificado-claro.webp)

![Paso 1: Lee el estado del certificado (celular)](../../../salida/manual/img/movil/verificar-certificado/01-lee-el-estado-del-certificado-celular.webp)

**Variantes**

- **El certificado está suspendido o revocado:** El título cambia al estado correspondiente y el PDF no se entrega.

## Paso 2. Comprueba la autenticidad en tu teléfono

La sección «Autenticidad» la calcula tu propio teléfono con las cuatro comprobaciones: contenido íntegro, huella del enlace, clave de la plataforma y sello válido. Si todas dicen «Correcto», el mensaje dice «Auténtico».

> **Pantalla:** `/verificar/SMA643-C-2026-000001-1`

![Paso 2: Comprueba la autenticidad en tu teléfono (escritorio)](../../../salida/manual/img/movil/verificar-certificado/02-comprueba-la-autenticidad-en-tu-telefono-claro.webp)

![Paso 2: Comprueba la autenticidad en tu teléfono (celular)](../../../salida/manual/img/movil/verificar-certificado/02-comprueba-la-autenticidad-en-tu-telefono-celular.webp)

## Paso 3. Descarga el PDF del certificado

El botón «Descargar PDF» baja el certificado para guardarlo o compartirlo. Mientras el certificado esté vigente, el PDF se entrega; si no, la página lo explica en lugar de bajarlo.

> **Pantalla:** `/verificar/SMA643-C-2026-000001-1`

![Paso 3: Descarga el PDF del certificado (escritorio)](../../../salida/manual/img/movil/verificar-certificado/03-descarga-el-pdf-del-certificado-claro.webp)

![Paso 3: Descarga el PDF del certificado (celular)](../../../salida/manual/img/movil/verificar-certificado/03-descarga-el-pdf-del-certificado-celular.webp)

## Recorridos relacionados

- [Verifica que un certificado es auténtico](../publico/verificar-certificado-autentico.md)
- [Lee el estado de un certificado suspendido o revocado](../publico/certificado-en-otros-estados.md)
