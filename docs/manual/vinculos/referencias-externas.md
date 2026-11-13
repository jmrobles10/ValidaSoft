---
id: "vinculos/referencias-externas"
titulo: "Localiza un vínculo por la referencia de un sistema externo"
actor: "Sistema integrador"
dominio: "vinculos"
resumen: "Un sistema integrado crea vínculos con su propio identificador, como el código de proveedor de su sistema. En Validasoft, la lista muestra esas referencias, el buscador abre un vínculo escribiendo el sistema y su referencia, y el detalle las muestra con su sistema."
pasos: 3
---

# Localiza un vínculo por la referencia de un sistema externo

**Actor:** Sistema integrador · **Dominio:** Vínculos y terceros

Un sistema integrado crea vínculos con su propio identificador, como el código de proveedor de su sistema. En Validasoft, la lista muestra esas referencias, el buscador abre un vínculo escribiendo el sistema y su referencia, y el detalle las muestra con su sistema.

**Antes de empezar**

- Un grupo con Constructora Norte y una administradora que consulta los vínculos.
- Dos vínculos de proveedor: uno creado por el sistema integrado con la referencia E2E · PRV-204, y otro creado a mano sin referencia.

**Pasos**

1. Mira las referencias externas en la lista
2. Escribe la referencia con su sistema
3. Revisa las referencias en el detalle

## Paso 1. Mira las referencias externas en la lista

La columna «Referencias externas» muestra, por cada vínculo, el sistema y el identificador que ese sistema le asignó al crearlo. Un vínculo que nadie creó desde un sistema integrado no tiene referencia y la celda dice «Sin referencias».

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 1: Mira las referencias externas en la lista (escritorio)](../../../salida/manual/img/vinculos/referencias-externas/01-mira-las-referencias-externas-en-la-lista-claro.webp)

## Paso 2. Escribe la referencia con su sistema

En el buscador escribe el sistema y la referencia separados por «·», por ejemplo «E2E · PRV-204». Al confirmar con Enter, se abre directamente el vínculo que tiene esa referencia. Si escribes solo la referencia, sin el sistema, el buscador te pide el formato correcto.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 2: Escribe la referencia con su sistema (escritorio)](../../../salida/manual/img/vinculos/referencias-externas/02-escribe-la-referencia-con-su-sistema-claro.webp)

## Paso 3. Revisa las referencias en el detalle

En el detalle, «Referencias externas» muestra el sistema que creó el vínculo y su referencia. Así sabes con qué código aparece el mismo proveedor en ese sistema.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Revisa las referencias en el detalle (escritorio)](../../../salida/manual/img/vinculos/referencias-externas/03-revisa-las-referencias-en-el-detalle-claro.webp)

## Recorridos relacionados

- [Revisa la lista de vínculos y filtra por estado y tipo](lista-y-filtros.md)
- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
