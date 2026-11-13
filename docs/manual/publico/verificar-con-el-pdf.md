---
id: "publico/verificar-con-el-pdf"
titulo: "Compara la huella impresa en el PDF con la del certificado"
actor: "Público"
dominio: "publico"
resumen: "Quien recibe el PDF del certificado escanea su código QR. Si el enlace no trae la huella completa, la página verifica el contenido y pide comparar la huella que muestra con la impresa en el PDF; las dos deben coincidir para confiar en el documento."
pasos: 2
---

# Compara la huella impresa en el PDF con la del certificado

**Actor:** Público · **Dominio:** Verificación pública

Quien recibe el PDF del certificado escanea su código QR. Si el enlace no trae la huella completa, la página verifica el contenido y pide comparar la huella que muestra con la impresa en el PDF; las dos deben coincidir para confiar en el documento.

**Antes de empezar**

- Un proveedor aprobado por Constructora Sur con su certificado vigente y su PDF descargado.
- Quien verifica abre el enlace sin sesión, desde una ventana nueva del navegador.

**Pasos**

1. Lee la huella que muestra la página
2. Compara la huella con la impresa en el PDF

## Paso 1. Lee la huella que muestra la página

Con el certificado vigente, la página muestra su «Huella», los 12 caracteres que identifican ese certificado, y la sección de autenticidad. Si el enlace no trae la huella, la comprobación «Huella del enlace» queda en aviso y te pide compararla con la impresa en el PDF.

> **Pantalla:** `/verificar/S137F6-C-2026-000001-1`

![Paso 1: Lee la huella que muestra la página (escritorio)](../../../salida/manual/img/publico/verificar-con-el-pdf/01-lee-la-huella-que-muestra-la-pagina-claro.webp)

## Paso 2. Compara la huella con la impresa en el PDF

Abre el PDF del certificado y busca la huella impresa; debe ser igual a la que muestra la página, carácter por carácter. Si coincide, el documento que tienes es el que Validasoft emitió. Si no coincide, o si el PDF no trae huella, no confíes en el documento: pide a la empresa que te envíe el certificado actual.

> **Pantalla:** `/verificar/S137F6-C-2026-000001-1`

![Paso 2: Compara la huella con la impresa en el PDF (escritorio)](../../../salida/manual/img/publico/verificar-con-el-pdf/02-compara-la-huella-con-la-impresa-en-el-pdf-claro.webp)

![Paso 2: Compara la huella con la impresa en el PDF (celular)](../../../salida/manual/img/publico/verificar-con-el-pdf/02-compara-la-huella-con-la-impresa-en-el-pdf-celular.webp)

**Variantes**

- **Abres el enlace completo del código QR:** El enlace trae la huella y la página compara sola el contenido: no tienes que hacerlo a mano.
- **La huella del enlace no es la de este certificado:** La página dice «puede tratarse de otro documento».

## Recorridos relacionados

- [Verifica que un certificado es auténtico](verificar-certificado-autentico.md)
- [Comprueba un certificado que no existe o tiene una huella mal escrita](verificar-certificado-invalido.md)
