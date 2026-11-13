---
id: "configuracion/tipos-de-vinculo"
titulo: "Crea un tipo de vínculo y lo desactiva"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora crea el tipo de vínculo «Contratista» con dos exigencias: cuenta bancaria designada y un código por correo en el enlace de invitación. Después lo desactiva para que no se usen más flujos con él."
pasos: 6
---

# Crea un tipo de vínculo y lo desactiva

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora crea el tipo de vínculo «Contratista» con dos exigencias: cuenta bancaria designada y un código por correo en el enlace de invitación. Después lo desactiva para que no se usen más flujos con él.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y los tipos de vínculo base (Proveedor, Cliente).
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Tipos de vínculo
2. Escribe el código y el nombre del tipo
3. Activa las exigencias del vínculo
4. Crea el tipo y revísalo en la tabla
5. Desactiva el tipo
6. Comprueba que el tipo quedó inactivo

## Paso 1. Abre Configuración › Tipos de vínculo

Un tipo de vínculo es la naturaleza de la relación con un tercero: proveedor, cliente, empleado. La tabla muestra cada tipo, cuántos atributos registra, si exige cuenta designada o código en el enlace, y si está activo. Los flujos aprobatorios se arman por tipo de vínculo, así que un tipo nuevo no sirve hasta que tenga su flujo.

> **Pantalla:** `/e/{empresa}/tipos-vinculo`

![Paso 1: Abre Configuración › Tipos de vínculo (escritorio)](../../../salida/manual/img/configuracion/tipos-de-vinculo/01-abre-configuracion-tipos-de-vinculo-claro.webp)

**Configuración que lo afecta**

- **Tipos de vínculo** (`Configuración › Tipos de vínculo (/tipos-vinculo)`): Cada invitación y cada flujo pertenecen a un tipo de vínculo.

## Paso 2. Escribe el código y el nombre del tipo

El código empieza con letra y solo lleva mayúsculas, números y guion bajo; mientras escribes se convierte a mayúsculas. Una vez creado no se puede cambiar, porque los flujos y las invitaciones lo usan. El nombre es el que ves en la tabla y en los flujos.

> **Pantalla:** `/e/{empresa}/tipos-vinculo`

![Paso 2: Escribe el código y el nombre del tipo (escritorio)](../../../salida/manual/img/configuracion/tipos-de-vinculo/02-escribe-el-codigo-y-el-nombre-del-tipo-claro.webp)

**Variantes**

- **El código ya existe en el grupo:** Al crear, la pantalla dice que ya existe un tipo de vínculo con ese código y no lo guarda.

## Paso 3. Activa las exigencias del vínculo

«Exige cuenta bancaria designada» hace que, para cerrar el vínculo, haya una cuenta designada aprobada. «Exige un código por correo en el enlace» pide al tercero, además del enlace de la invitación, un código que le llega a su correo. Son reglas del tipo: todos los vínculos de este tipo las cumplen, sin importar el flujo que usen.

> **Pantalla:** `/e/{empresa}/tipos-vinculo`

![Paso 3: Activa las exigencias del vínculo (escritorio)](../../../salida/manual/img/configuracion/tipos-de-vinculo/03-activa-las-exigencias-del-vinculo-claro.webp)

**Variantes**

- **Activas «Exige código en el enlace»:** Al aceptar la invitación, el tercero escribe el código que le llega al correo antes de ver el expediente.
- **No activas ninguna:** El tercero acepta solo con el enlace de la invitación, como en los demás vínculos.

## Paso 4. Crea el tipo y revísalo en la tabla

Al crear, el panel se cierra y el nuevo tipo aparece activo, con sus exigencias en la columna «Exige». Para cambiar el nombre o las exigencias más tarde, abre el menú de la fila y elige «Editar»; el código ya no se puede editar.

> **Pantalla:** `/e/{empresa}/tipos-vinculo`

![Paso 4: Crea el tipo y revísalo en la tabla (escritorio)](../../../salida/manual/img/configuracion/tipos-de-vinculo/04-crea-el-tipo-y-revisalo-en-la-tabla-claro.webp)

## Paso 5. Desactiva el tipo

Desactivar impide crear flujos nuevos con este tipo. Lo que ya existe no cambia: los vínculos y los flujos que ya lo usan siguen igual. La fila queda atenuada con el estado «Inactivo» y puede reactivarse desde el mismo menú.

> **Pantalla:** `/e/{empresa}/tipos-vinculo`

![Paso 5: Desactiva el tipo (escritorio)](../../../salida/manual/img/configuracion/tipos-de-vinculo/05-desactiva-el-tipo-claro.webp)

**Variantes**

- **El tipo está inactivo:** El menú de la fila ofrece «Activar tipo» en lugar de «Desactivar tipo».

## Paso 6. Comprueba que el tipo quedó inactivo

La tabla ordena primero los tipos activos y el tipo desactivado queda al final con su estado «Inactivo». Desde ahora no aparece como opción al crear un flujo nuevo.

> **Pantalla:** `/e/{empresa}/tipos-vinculo`

![Paso 6: Comprueba que el tipo quedó inactivo (escritorio)](../../../salida/manual/img/configuracion/tipos-de-vinculo/06-comprueba-que-el-tipo-quedo-inactivo-claro.webp)

## Recorridos relacionados

- [Configura un tipo de documento y publica un flujo que lo pide](tipo-de-documento-y-flujo.md)
