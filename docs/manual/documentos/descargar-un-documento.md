---
id: "documentos/descargar-un-documento"
titulo: "Descarga un documento del proveedor y comprueba su registro"
actor: "Administrador de empresa"
dominio: "documentos"
resumen: "La administradora abre el vínculo de un proveedor, ve el RUT que compartió, lo descarga desde el visor y comprueba que la descarga quedó registrada en Auditoría › Accesos, con quién la hizo y cuándo."
pasos: 5
---

# Descarga un documento del proveedor y comprueba su registro

**Actor:** Administrador de empresa · **Dominio:** Documentos y vigencias

La administradora abre el vínculo de un proveedor, ve el RUT que compartió, lo descarga desde el visor y comprueba que la descarga quedó registrada en Auditoría › Accesos, con quién la hizo y cuándo.

**Antes de empezar**

- Un grupo con Constructora Sur y una administradora con permiso para ver y descargar documentos.
- Un proveedor invitado por Sur que ya subió su RUT y su certificado de SST.

**Pasos**

1. Encuentra el documento en el vínculo
2. Revisa el documento en el visor
3. Descarga el archivo
4. Comprueba el registro en Auditoría › Accesos
5. Abre el detalle del acceso

## Paso 1. Encuentra el documento en el vínculo

En el detalle del vínculo, «Documentos compartidos» lista lo que el proveedor tiene en su bóveda para esta empresa: el tipo de documento, la versión, la fecha de carga y la huella SHA-256 del archivo. «Ver o descargar» (1) abre esa versión exacta. La sección solo aparece a quien tiene permiso para leer documentos, y el botón, a quien puede descargarlos.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Encuentra el documento en el vínculo (escritorio)](../../../salida/manual/img/documentos/descargar-un-documento/01-encuentra-el-documento-en-el-vinculo-claro.webp)

**Variantes**

- **La versión es un formulario sin archivo:** El botón dice «Ver datos»: no hay archivo para descargar y se ven los datos que declaró el tercero.
- **Tu rol no puede descargar documentos:** Ves la lista, pero no el botón de ver o descargar.

**Configuración que lo afecta**

- **Permisos del rol (documentos: leer y descargar)** (`Configuración › Usuarios y roles (/usuarios)`): Decide quién ve la lista de documentos y quién puede abrirlos.

## Paso 2. Revisa el documento en el visor

El visor abre los PDF y las imágenes dentro de la pantalla, con el nombre del documento y su versión. Pedirle al servidor el archivo ya cuenta como un acceso y queda registrado. El botón «Descargar» (1) guarda una copia en tu equipo; los tipos de archivo que el visor no muestra se abren solo descargándolos.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Revisa el documento en el visor (escritorio)](../../../salida/manual/img/documentos/descargar-un-documento/02-revisa-el-documento-en-el-visor-claro.webp)

## Paso 3. Descarga el archivo

El navegador guarda el archivo original del proveedor (aquí, «rut-descargable.pdf») tal como lo subió: no se modifica. Cada descarga queda registrada en la auditoría de la empresa, para poder demostrar después quién consultó qué documento y cuándo.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Descarga el archivo (escritorio)](../../../salida/manual/img/documentos/descargar-un-documento/03-descarga-el-archivo-claro.webp)

## Paso 4. Comprueba el registro en Auditoría › Accesos

La pestaña «Accesos» de Auditoría lista las descargas de documentos y certificados de la empresa: la fecha y hora, quién las hizo (aquí, Ana, desde el sistema integrado), la entidad «Documento» y la acción «Descargado». Haz clic en una fila para ver el detalle.

> **Pantalla:** `/e/{empresa}/auditoria`

![Paso 4: Comprueba el registro en Auditoría › Accesos (escritorio)](../../../salida/manual/img/documentos/descargar-un-documento/04-comprueba-el-registro-en-auditoria-accesos-claro.webp)

**Configuración que lo afecta**

- **Permiso de auditoría (leer)** (`Configuración › Usuarios y roles (/usuarios)`): Quien lo tiene ve esta pestaña y puede exportarla a Excel.

## Paso 5. Abre el detalle del acceso

El detalle muestra qué documento y qué versión se descargó (1), el motivo (2), quién lo hizo y desde dónde: la dirección IP y el navegador. Es la evidencia para responder «¿quién bajó este documento?» sin depender de la memoria de nadie.

> **Pantalla:** `/e/{empresa}/auditoria`

![Paso 5: Abre el detalle del acceso (escritorio)](../../../salida/manual/img/documentos/descargar-un-documento/05-abre-el-detalle-del-acceso-claro.webp)

## Recorridos relacionados

- [Abre un documento para verlo y descargarlo](../revision/visor-y-datos-de-un-documento.md)
- [Revisa quién cambió la configuración en Auditoría](../informes/auditoria.md)
- [Exporta un expediente completo](../informes/exportar-un-expediente.md)
