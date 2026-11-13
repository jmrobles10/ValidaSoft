---
id: "operacion/ayuda-de-documentos"
titulo: "Escribe la ayuda que ve el tercero al subir un documento"
actor: "Administrador de empresa"
dominio: "operacion"
resumen: "La administradora escribe, para su empresa, la ayuda que el tercero lee al entregar el RUT: qué debe traer y de qué fecha. Si no escribe nada, el tercero ve la ayuda del grupo."
pasos: 4
---

# Escribe la ayuda que ve el tercero al subir un documento

**Actor:** Administrador de empresa · **Dominio:** Operación masiva y continua

La administradora escribe, para su empresa, la ayuda que el tercero lee al entregar el RUT: qué debe traer y de qué fecha. Si no escribe nada, el tercero ve la ayuda del grupo.

**Antes de empezar**

- Un grupo con Constructora Norte, el tipo de documento RUT y una ayuda del grupo para ese documento.

**Pasos**

1. Abre la ayuda de documentos
2. Escribe la ayuda de la empresa
3. Comprueba que la ayuda es de la empresa
4. Lee la ayuda al entregar el documento

## Paso 1. Abre la ayuda de documentos

La tabla lista cada tipo de documento con la ayuda que ve el tercero y de quién es: «Propia» si la escribió esta empresa, «Del grupo» si viene de todo el grupo o «Sin ayuda» si nadie la escribió. Quien no puede cambiarla ve la lista en solo lectura.

> **Pantalla:** `/e/{empresa}/ayuda-de-documentos`

![Paso 1: Abre la ayuda de documentos (escritorio)](../../../salida/manual/img/operacion/ayuda-de-documentos/01-abre-la-ayuda-de-documentos-claro.webp)

**Variantes**

- **Eres revisor sin permiso de configuración:** Ves las ayudas, pero no puedes cambiarlas.

**Configuración que lo afecta**

- **Ayuda de documentos** (`Configuración › Ayuda de documentos (/ayuda-de-documentos)`): La ayuda aparece al tercero cuando entrega ese documento en un expediente de esta empresa.

## Paso 2. Escribe la ayuda de la empresa

Al editar el documento, escribe lo que el tercero necesita saber: qué debe traer, de qué fecha y firmado por quién. El texto de la empresa reemplaza al del grupo para esta empresa. Si lo dejas vacío, el tercero vuelve a ver la ayuda del grupo.

> **Pantalla:** `/e/{empresa}/ayuda-de-documentos`

![Paso 2: Escribe la ayuda de la empresa (escritorio)](../../../salida/manual/img/operacion/ayuda-de-documentos/02-escribe-la-ayuda-de-la-empresa-claro.webp)

**Variantes**

- **Dejas vacío el texto y guardas:** El tercero ve la ayuda del grupo, o ninguna si el grupo no escribió una.

## Paso 3. Comprueba que la ayuda es de la empresa

La fila del RUT pasa a «Propia». Desde ese momento, los terceros de la empresa que entreguen el RUT ven este texto al lado del documento.

> **Pantalla:** `/e/{empresa}/ayuda-de-documentos`

![Paso 3: Comprueba que la ayuda es de la empresa (escritorio)](../../../salida/manual/img/operacion/ayuda-de-documentos/03-comprueba-que-la-ayuda-es-de-la-empresa-claro.webp)

## Paso 4. Lee la ayuda al entregar el documento

Cuando el tercero abre el expediente, la ayuda aparece junto al documento que tiene que entregar, en su portal. Así sabe qué traer antes de subir el archivo, sin tener que escribirle a la empresa.

> **Pantalla:** `/expediente/{id}`

![Paso 4: Lee la ayuda al entregar el documento (escritorio)](../../../salida/manual/img/operacion/ayuda-de-documentos/04-lee-la-ayuda-al-entregar-el-documento-claro.webp)

## Recorridos relacionados

- [Acepta la invitación y completa tu información como persona jurídica](../tercero/aceptar-invitacion-persona-juridica.md)
