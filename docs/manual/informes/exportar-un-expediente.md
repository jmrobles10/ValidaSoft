---
id: "informes/exportar-un-expediente"
titulo: "Exporta un expediente completo"
actor: "Administrador de empresa"
dominio: "informes"
resumen: "La administradora de Constructora Sur abre un expediente aprobado desde el informe, lo exporta en un ZIP con su resumen, su índice, su línea de tiempo y los documentos, y comprueba que la exportación quedó registrada en la auditoría de accesos."
pasos: 3
---

# Exporta un expediente completo

**Actor:** Administrador de empresa · **Dominio:** Informes, auditoría y certificados

La administradora de Constructora Sur abre un expediente aprobado desde el informe, lo exporta en un ZIP con su resumen, su índice, su línea de tiempo y los documentos, y comprueba que la exportación quedó registrada en la auditoría de accesos.

**Antes de empezar**

- Un grupo con Constructora Sur, con el RUT y el certificado de seguridad y salud en el trabajo como requisitos del flujo de proveedor.
- Un proveedor cuyo expediente ya fue aprobado por el área Revisión.

**Pasos**

1. Abre el expediente aprobado
2. Descarga el expediente en ZIP
3. Revisa el registro en la auditoría

## Paso 1. Abre el expediente aprobado

Desde el informe, un clic en la fila lleva al expediente. Con el expediente aprobado abierto, el botón «Exportar expediente» genera el paquete completo de ese expediente, con su folio.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre el expediente aprobado (escritorio)](../../../salida/manual/img/informes/exportar-un-expediente/01-abre-el-expediente-aprobado-claro.webp)

## Paso 2. Descarga el expediente en ZIP

El archivo ZIP se llama «expediente» seguido del folio. Trae el resumen en PDF, un índice, la línea de tiempo del expediente en JSON y cada documento incluido con su versión (aquí, el RUT y el certificado de seguridad y salud en el trabajo). Cada documento que se incluye también queda registrado en la auditoría.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Descarga el expediente en ZIP (escritorio)](../../../salida/manual/img/informes/exportar-un-expediente/02-descarga-el-expediente-en-zip-claro.webp)

## Paso 3. Revisa el registro en la auditoría

En Auditoría, la pestaña «Accesos» registra quién exportó el expediente y cuándo: una fila «Exportado» con el folio, y una fila «Descargado» por cada documento que se incluyó. Así la empresa sabe qué información salió del sistema y quién la sacó.

> **Pantalla:** `/e/{empresa}/auditoria`

![Paso 3: Revisa el registro en la auditoría (escritorio)](../../../salida/manual/img/informes/exportar-un-expediente/03-revisa-el-registro-en-la-auditoria-claro.webp)

## Recorridos relacionados

- [Consulta el estado de los expedientes y sus tiempos](estado-de-expedientes.md)
- [Revisa quién cambió la configuración en Auditoría](auditoria.md)
- [Descarga un documento del proveedor y comprueba su registro](../documentos/descargar-un-documento.md)
