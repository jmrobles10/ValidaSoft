---
id: "vinculos/ficha-aceptada-del-vinculo"
titulo: "Consulta la ficha aceptada de un vínculo"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora abre la ficha aceptada de un proveedor cuya ficha completa ya fue aprobada por Constructora Sur. Ve, sección por sección, lo que la empresa aceptó, con su versión y el soporte con el que se aceptó."
pasos: 4
---

# Consulta la ficha aceptada de un vínculo

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora abre la ficha aceptada de un proveedor cuya ficha completa ya fue aprobada por Constructora Sur. Ve, sección por sección, lo que la empresa aceptó, con su versión y el soporte con el que se aceptó.

**Antes de empezar**

- Un grupo con Constructora Sur, con un flujo de proveedor que pide el RUT, la Cámara de comercio y las seis secciones de la ficha.
- Un proveedor cuya ficha quedó completa y su área de Revisión la aprobó.

**Pasos**

1. Abre la ficha aceptada desde el vínculo
2. Lee el resumen de la ficha aceptada
3. Revisa una sección aceptada y su versión
4. Revisa el soporte con que se aceptó

## Paso 1. Abre la ficha aceptada desde el vínculo

Cuando tu empresa aprueba secciones de la ficha de un tercero, el detalle del vínculo muestra la tarjeta «Ficha aceptada». Haz clic en «Ver la ficha aceptada» para ver exactamente lo que se aprobó.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Abre la ficha aceptada desde el vínculo (escritorio)](../../../salida/manual/img/vinculos/ficha-aceptada-del-vinculo/01-abre-la-ficha-aceptada-desde-el-vinculo-claro.webp)

**Configuración que lo afecta**

- **Ficha del tercero y flujo** (`Configuración › Flujos (/flujos)`): Las secciones que aparecen en la ficha aceptada son las que el flujo del vínculo pide al tercero.

## Paso 2. Lee el resumen de la ficha aceptada

El resumen cuenta las secciones aceptadas y las que tienen una versión nueva pendiente de revisión. Cada empresa acepta por su cuenta: lo que aceptó otra empresa del grupo no aparece aquí.

> **Pantalla:** `/e/{empresa}/vinculos/{id}/ficha`

![Paso 2: Lee el resumen de la ficha aceptada (escritorio)](../../../salida/manual/img/vinculos/ficha-aceptada-del-vinculo/02-lee-el-resumen-de-la-ficha-aceptada-claro.webp)

**Variantes**

- **El tercero guardó una versión nueva de una sección aceptada:** El resumen lo indica y la sección muestra lo aceptado y, aparte, lo que cambió. Lo nuevo solo vale cuando lo apruebes en la revalidación.

## Paso 3. Revisa una sección aceptada y su versión

Cada sección muestra la versión aceptada, la fecha de aceptación y el expediente donde se aprobó. Con «Ver los datos aceptados» abres los valores tal como se aprobaron.

> **Pantalla:** `/e/{empresa}/vinculos/{id}/ficha`

![Paso 3: Revisa una sección aceptada y su versión (escritorio)](../../../salida/manual/img/vinculos/ficha-aceptada-del-vinculo/03-revisa-una-seccion-aceptada-y-su-version-claro.webp)

## Paso 4. Revisa el soporte con que se aceptó

Dentro de la sección aparece el soporte con que se aceptó, con su nombre y su versión. Si una sección se aceptó sin soporte, dice «Sin soporte».

> **Pantalla:** `/e/{empresa}/vinculos/{id}/ficha`

![Paso 4: Revisa el soporte con que se aceptó (escritorio)](../../../salida/manual/img/vinculos/ficha-aceptada-del-vinculo/04-revisa-el-soporte-con-que-se-acepto-claro.webp)

## Recorridos relacionados

- [Respalda tu información con un documento de tu bóveda](../tercero/ficha-secciones-con-soporte.md)
- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
