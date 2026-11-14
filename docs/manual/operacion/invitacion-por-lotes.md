---
id: "operacion/invitacion-por-lotes"
titulo: "Invita a varios terceros con un archivo"
actor: "Administrador de empresa"
dominio: "operacion"
resumen: "La administradora descarga la plantilla, sube un archivo con varios terceros, revisa fila por fila qué está bien y qué tiene errores, y crea el lote. Las filas válidas se invitan en segundo plano."
pasos: 6
---

# Invita a varios terceros con un archivo

**Actor:** Administrador de empresa · **Dominio:** Operación masiva y continua

La administradora descarga la plantilla, sube un archivo con varios terceros, revisa fila por fila qué está bien y qué tiene errores, y crea el lote. Las filas válidas se invitan en segundo plano.

**Antes de empezar**

- Un grupo con Constructora Norte y su administradora Ana, sin lotes previos.

**Pasos**

1. Abre la invitación por lote
2. Descarga la plantilla
3. Revisa la vista previa fila por fila
4. Nombra el lote y confirma
5. Comprueba el resultado del lote
6. Abre el vínculo de un tercero del lote

## Paso 1. Abre la invitación por lote

Desde Vínculos, el botón «Invitar por lote» muestra los lotes de la empresa. «Nuevo lote» abre el asistente de tres pasos: descargar la plantilla, subir el archivo y revisar antes de invitar.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 1: Abre la invitación por lote (escritorio)](../../../salida/manual/img/operacion/invitacion-por-lotes/01-abre-la-invitacion-por-lote-claro.webp)

**Configuración que lo afecta**

- **Invitación por lote** (`Vínculos › Invitar por lote`): Cada fila crea o reutiliza un tercero y un vínculo de la empresa.

## Paso 2. Descarga la plantilla

La plantilla trae las columnas obligatorias: tipo y número de identificación, razón social o nombres, correo y tipo de vínculo. Las opcionales son país, DV, tipo de persona, nombre comercial, flujo, contacto y teléfono. Puedes bajarla en Excel o en CSV; las dos tienen las mismas columnas.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/nuevo`

![Paso 2: Descarga la plantilla (escritorio)](../../../salida/manual/img/operacion/invitacion-por-lotes/02-descarga-la-plantilla-claro.webp)

**Variantes**

- **El archivo no tiene una de las columnas obligatorias:** Validasoft lo rechaza: «Al archivo le faltan columnas de la plantilla».

## Paso 3. Revisa la vista previa fila por fila

Al subir el archivo se valida cada fila antes de invitar. El resumen cuenta filas válidas, con errores y repetidas. La fila con un correo mal escrito aparece con su motivo y no se invitará. Corrige el archivo en tu hoja de cálculo y vuelve a subirlo para revisar de nuevo.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/nuevo`

![Paso 3: Revisa la vista previa fila por fila (escritorio)](../../../salida/manual/img/operacion/invitacion-por-lotes/03-revisa-la-vista-previa-fila-por-fila-claro.webp)

**Variantes**

- **Ninguna fila es válida:** No se puede crear el lote: «Ninguna fila es válida: corrige el archivo y vuelve a subirlo».

## Paso 4. Nombra el lote y confirma

Escribe un nombre para reconocer el lote luego; si lo dejas vacío se usa el nombre del archivo. «Vigencia de cada invitación» dice cuántos días tiene cada tercero para aceptar (7 por omisión). El botón indica cuántos terceros se van a invitar: solo las filas válidas. Al crearlo, las invitaciones se envían en segundo plano y no tienes que esperar en esta pantalla.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/nuevo`

![Paso 4: Nombra el lote y confirma (escritorio)](../../../salida/manual/img/operacion/invitacion-por-lotes/04-nombra-el-lote-y-confirma-claro.webp)

## Paso 5. Comprueba el resultado del lote

El detalle del lote muestra el avance («2 de 2»), el estado final y una fila por cada registro del archivo. Las válidas quedan con estado «Exitosa» y detalle «Invitado», que enlaza a su vínculo; la que tenía errores queda «Con errores» y no recibió invitación. Cada tercero invitado recibe el correo de la empresa con su enlace.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 5: Comprueba el resultado del lote (escritorio)](../../../salida/manual/img/operacion/invitacion-por-lotes/05-comprueba-el-resultado-del-lote-claro.webp)

## Paso 6. Abre el vínculo de un tercero del lote

Desde la fila, el enlace «Invitado» lleva al vínculo del tercero, que queda en estado Invitado hasta que acepte. Desde ahí se sigue el proceso igual que con una invitación individual.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 6: Abre el vínculo de un tercero del lote (escritorio)](../../../salida/manual/img/operacion/invitacion-por-lotes/06-abre-el-vinculo-de-un-tercero-del-lote-claro.webp)

## Recorridos relacionados

- [Acepta la invitación y completa tu información como persona jurídica](../tercero/aceptar-invitacion-persona-juridica.md)
