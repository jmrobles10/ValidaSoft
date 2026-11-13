---
id: "configuracion/tipo-de-documento-solo-formulario-y-plantilla"
titulo: "Crea un documento con formulario y plantilla descargable"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora crea un tipo de documento que el proveedor entrega como archivo y como formulario con datos. Luego sube la plantilla que el proveedor descarga y llena, publica una segunda versión de esa plantilla y finalmente la retira."
pasos: 8
---

# Crea un documento con formulario y plantilla descargable

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora crea un tipo de documento que el proveedor entrega como archivo y como formulario con datos. Luego sube la plantilla que el proveedor descarga y llena, publica una segunda versión de esa plantilla y finalmente la retira.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y el flujo de Proveedor publicado.
- Un archivo PDF que servirá de plantilla (se siembra por la API del sistema de pruebas al subirlo en el paso correspondiente).
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Identifica el documento
2. Elige cómo lo entrega el proveedor y define sus campos
3. Fija el ámbito y el archivo que se acepta
4. Crea el tipo y revísalo en la tabla
5. Revisa la sección de plantilla
6. Sube la plantilla
7. Publica una versión nueva de la plantilla
8. Retira la plantilla

## Paso 1. Identifica el documento

El panel lateral sirve para crear el tipo. Escribe un código en mayúsculas (no cambia después) y el nombre que verá el proveedor. Como es un documento propio, no partes de un documento de la plataforma: defines tú el formato, los campos y la vigencia.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 1: Identifica el documento (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-solo-formulario-y-plantilla/01-identifica-el-documento-claro.webp)

## Paso 2. Elige cómo lo entrega el proveedor y define sus campos

En «Se entrega como» eliges si el proveedor sube un archivo, llena un formulario o hace las dos cosas. Con formulario aparece la lista de campos que llenará: cada uno tiene etiqueta, clave, tipo y si es obligatorio. Aquí se usan cinco campos de cinco tipos: texto, número, fecha, lista con sus opciones (una por línea) y Sí / No. Las claves se generan de la etiqueta y las mueves con las flechas del campo.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 2: Elige cómo lo entrega el proveedor y define sus campos (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-solo-formulario-y-plantilla/02-elige-como-lo-entrega-el-proveedor-y-define-sus-claro.webp)

**Variantes**

- **Se entrega como «Archivo»:** El proveedor solo sube el archivo; no hay campos ni formulario.
- **Se entrega como «Formulario»:** El proveedor llena los datos y no sube archivo: no aparecen los límites del archivo ni la sección de plantilla.
- **Se entrega como «Archivo y formulario»:** El proveedor sube el archivo y llena los datos en la misma entrega; la sección de plantilla sí aparece.

## Paso 3. Fija el ámbito y el archivo que se acepta

Con formulario, el ámbito queda en «Solo de este vínculo»: el documento no se comparte con otras empresas porque lleva datos propios del proveedor para tu relación. Los formatos y el peso del archivo siguen igual que en cualquier documento: aquí solo PDF y hasta 5 MB. El resumen de la tabla mostrará «Formulario (5 campos)» junto al archivo.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 3: Fija el ámbito y el archivo que se acepta (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-solo-formulario-y-plantilla/03-fija-el-ambito-y-el-archivo-que-se-acepta-claro.webp)

**Configuración que lo afecta**

- **Ámbito del documento**: Un documento con formulario siempre es «Solo de este vínculo» y no se reutiliza con otras empresas.

## Paso 4. Crea el tipo y revísalo en la tabla

Al crear, el panel se cierra y la tabla muestra el nuevo tipo con «Formulario (5 campos)» en la columna «Qué recibe». Para subir la plantilla, abre su fila: la sección «Plantilla para descargar» aparece dentro del tipo ya creado.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 4: Crea el tipo y revísalo en la tabla (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-solo-formulario-y-plantilla/04-crea-el-tipo-y-revisalo-en-la-tabla-claro.webp)

## Paso 5. Revisa la sección de plantilla

La sección «Plantilla para descargar» dice que el tipo no tiene plantilla. Es un formato que el proveedor descarga, llena, firma y sube de nuevo; puede ser PDF, Word (DOCX) o Excel (XLSX). Aparece solo en los tipos que reciben archivo: un documento que se entrega solo como formulario no tiene plantilla.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 5: Revisa la sección de plantilla (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-solo-formulario-y-plantilla/05-revisa-la-seccion-de-plantilla-claro.webp)

**Variantes**

- **Se entrega como «Formulario»:** La sección no aparece: el proveedor llena los datos en la pantalla de Validasoft.

## Paso 6. Sube la plantilla

Con «Subir plantilla» eliges el archivo desde tu equipo. Validasoft acepta PDF, DOCX o XLSX; otro formato se rechaza con un aviso. Al subirlo queda como versión 1 con su peso y la fecha de carga, y el proveedor la verá en la pantalla de carga del documento.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 6: Sube la plantilla (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-solo-formulario-y-plantilla/06-sube-la-plantilla-claro.webp)

**Variantes**

- **Subes un archivo que no es PDF, DOCX ni XLSX:** La plantilla no se guarda y aparece el aviso «La plantilla debe ser PDF, DOCX o XLSX.»
- **Subes el mismo archivo que ya es la plantilla vigente:** No se crea una versión nueva; el aviso dice que es el mismo archivo.

## Paso 7. Publica una versión nueva de la plantilla

Con «Subir una versión nueva» reemplazas la plantilla vigente sin perder la anterior. La versión 2 es la que descarga el proveedor desde ahora; las versiones anteriores quedan en «1 versión anterior», con su nombre, su fecha y un botón para descargarlas.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 7: Publica una versión nueva de la plantilla (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-solo-formulario-y-plantilla/07-publica-una-version-nueva-de-la-plantilla-claro.webp)

## Paso 8. Retira la plantilla

«Retirar» quita la plantilla vigente: el proveedor deja de verla y de poder descargarla, pero el tipo sigue activo y pide sus datos o su archivo igual. La versión retirada queda en el historial con la fecha en que se retiró, y puedes subir una plantilla nueva en cualquier momento.

> **Pantalla:** `/e/{empresa}/tipos-documento`

![Paso 8: Retira la plantilla (escritorio)](../../../salida/manual/img/configuracion/tipo-de-documento-solo-formulario-y-plantilla/08-retira-la-plantilla-claro.webp)

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](tipo-de-documento-y-flujo.md)
