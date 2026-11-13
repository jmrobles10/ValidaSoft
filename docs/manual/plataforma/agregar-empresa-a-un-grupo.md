---
id: "plataforma/agregar-empresa-a-un-grupo"
titulo: "Agrega una empresa a un grupo existente"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma busca un grupo ya creado y le agrega otra empresa con el mismo formulario del alta, sin volver a crear el grupo. La empresa nueva aparece en la tabla del grupo, junto a las que ya tenía."
pasos: 3
---

# Agrega una empresa a un grupo existente

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma busca un grupo ya creado y le agrega otra empresa con el mismo formulario del alta, sin volver a crear el grupo. La empresa nueva aparece en la tabla del grupo, junto a las que ya tenía.

**Antes de empezar**

- Un grupo creado con al menos una empresa.
- Un operador de Plataforma con la consola abierta.

**Pasos**

1. Busca el grupo y pulsa «Agregar empresa»
2. Escribe los datos de la nueva empresa
3. Comprueba que la empresa está en el grupo

## Paso 1. Busca el grupo y pulsa «Agregar empresa»

Cada grupo tiene en su tarjeta el botón «Agregar una empresa». Con él no se crea un grupo nuevo: la empresa entra al grupo que ya existe. Si no ves el grupo, búscalo por su nombre o por el NIT de una de sus empresas.

> **Pantalla:** `/plataforma/grupos`

![Paso 1: Busca el grupo y pulsa «Agregar empresa» (escritorio)](../../../salida/manual/img/plataforma/agregar-empresa-a-un-grupo/01-busca-el-grupo-y-pulsa-agregar-empresa-claro.webp)

**Configuración que lo afecta**

- **Empresas de un grupo** (`Plataforma › Grupos y empresas › Agregar empresa`): Suma una empresa al grupo; la tabla del grupo la muestra con su NIT, su prefijo y su plan de IA.

## Paso 2. Escribe los datos de la nueva empresa

El diálogo usa el mismo formulario de la primera empresa del alta: país y tipo de identificación, número, razón social y prefijo de folio. El prefijo es el que aparece en los números de sus expedientes y no cambia después, así que elige uno corto y reconocible.

> **Pantalla:** `/plataforma/grupos`

![Paso 2: Escribe los datos de la nueva empresa (escritorio)](../../../salida/manual/img/plataforma/agregar-empresa-a-un-grupo/02-escribe-los-datos-de-la-nueva-empresa-claro.webp)

**Variantes**

- **El número no tiene la forma del tipo elegido:** El campo lo indica debajo y no deja agregar la empresa.

## Paso 3. Comprueba que la empresa está en el grupo

La tabla del grupo ahora incluye la empresa nueva junto a las que ya tenía, con su identificación, su prefijo y su plan de IA. Desde su fila puedes configurar ese plan igual que para las demás. El grupo no cambió de nombre ni de identificador: solo creció.

> **Pantalla:** `/plataforma/grupos`

![Paso 3: Comprueba que la empresa está en el grupo (escritorio)](../../../salida/manual/img/plataforma/agregar-empresa-a-un-grupo/03-comprueba-que-la-empresa-esta-en-el-grupo-claro.webp)

**Configuración que lo afecta**

- **Plan de IA por empresa** (`Plataforma › Grupos y empresas › Plan de IA`): Define el nivel de IA que puede usar esa empresa.

## Recorridos relacionados

- [Crea un grupo con su primera empresa](crear-grupo-y-empresa.md)
- [Busca un grupo y revisa sus empresas](lista-de-grupos.md)
