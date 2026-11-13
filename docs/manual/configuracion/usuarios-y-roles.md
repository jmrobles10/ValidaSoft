---
id: "configuracion/usuarios-y-roles"
titulo: "Agrega una persona, dale roles y desactívala"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora agrega a una persona al grupo con un rol en una empresa y otro para todo el grupo, cambia sus roles y después la desactiva. Las personas entran desde su sistema; aquí se decide qué puede hacer cada una."
pasos: 7
---

# Agrega una persona, dale roles y desactívala

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora agrega a una persona al grupo con un rol en una empresa y otro para todo el grupo, cambia sus roles y después la desactiva. Las personas entran desde su sistema; aquí se decide qué puede hacer cada una.

**Antes de empezar**

- Un grupo empresarial con las empresas «Constructora Norte S.A.S.» y «Constructora Sur S.A.S.», y personas con roles (Rubén, Olga, Sofía).
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre Configuración › Usuarios y roles
2. Agrega una persona con sus roles
3. Revisa la nueva persona en la tabla
4. Cambia los roles de la persona
5. Confirma los roles nuevos
6. Desactiva a la persona
7. Comprueba que la persona quedó desactivada

## Paso 1. Abre Configuración › Usuarios y roles

La tabla lista las personas del grupo con sus roles, agrupados por empresa: «Todo el grupo» aplica en todas las empresas y, si el rol es para una empresa, aparece con su nombre. La columna «Entra desde» muestra el sistema integrado por el que entra la persona. Un filtro separa las activas de las desactivadas.

> **Pantalla:** `/e/{empresa}/usuarios`

![Paso 1: Abre Configuración › Usuarios y roles (escritorio)](../../../salida/manual/img/configuracion/usuarios-y-roles/01-abre-configuracion-usuarios-y-roles-claro.webp)

**Variantes**

- **Una persona no tiene roles:** Aparece «Sin roles» y no puede hacer nada en Validasoft hasta que le agregues al menos uno.

**Configuración que lo afecta**

- **Usuarios y roles** (`Configuración › Usuarios y roles (/usuarios)`): Decide qué puede hacer cada persona en cada empresa del grupo.

## Paso 2. Agrega una persona con sus roles

Escribe el nombre y el correo de la persona. Luego agrega un rol por cada empresa o uno para todo el grupo: cada fila elige la empresa (o «Todo el grupo») y el rol. Los roles son Administrador, Revisor, Oficial de cumplimiento y Auditor. Aquí la persona es Revisora en Constructora Sur y Auditora de todo el grupo. Sin ningún rol la persona no puede hacer nada en Validasoft.

> **Pantalla:** `/e/{empresa}/usuarios`

![Paso 2: Agrega una persona con sus roles (escritorio)](../../../salida/manual/img/configuracion/usuarios-y-roles/02-agrega-una-persona-con-sus-roles-claro.webp)

**Variantes**

- **El grupo admite el ingreso directo:** Aparece la casilla «Enviar una invitación por correo», marcada por omisión: la persona recibe un correo para entrar con código y passkey.
- **El grupo no admite el ingreso directo:** La casilla aparece desactivada y dice que hay que activar el ingreso directo en Configuración › Ingreso.

**Configuración que lo afecta**

- **Roles por empresa** (`Configuración › Usuarios y roles (/usuarios)`): Los cambios aplican en el próximo ingreso de la persona desde su sistema y quedan en la auditoría.

## Paso 3. Revisa la nueva persona en la tabla

La persona aparece en la tabla con sus roles, cada uno con su empresa debajo. Como todavía no entró por su sistema, la columna «Último ingreso» dice «Nunca». Para cambiar sus roles más tarde, abre el menú de su fila y elige «Editar roles».

> **Pantalla:** `/e/{empresa}/usuarios`

![Paso 3: Revisa la nueva persona en la tabla (escritorio)](../../../salida/manual/img/configuracion/usuarios-y-roles/03-revisa-la-nueva-persona-en-la-tabla-claro.webp)

## Paso 4. Cambia los roles de la persona

El panel «Editar roles» muestra la persona y sus filas de roles, con los mismos controles que al agregarla. Quitar una fila con la X y agregar otra cambia sus permisos. En este caso cambiamos su rol para todo el grupo a Oficial de cumplimiento. El cambio no se aplica en su sesión actual: se nota en su próximo ingreso.

> **Pantalla:** `/e/{empresa}/usuarios`

![Paso 4: Cambia los roles de la persona (escritorio)](../../../salida/manual/img/configuracion/usuarios-y-roles/04-cambia-los-roles-de-la-persona-claro.webp)

**Variantes**

- **Quitas todas las filas de roles:** La pantalla avisa que sin roles la persona no puede hacer nada en Validasoft.

## Paso 5. Confirma los roles nuevos

Al guardar, el panel se cierra y la fila muestra los roles nuevos. Los cambios quedan registrados en la auditoría de configuración con quién los hizo.

> **Pantalla:** `/e/{empresa}/usuarios`

![Paso 5: Confirma los roles nuevos (escritorio)](../../../salida/manual/img/configuracion/usuarios-y-roles/05-confirma-los-roles-nuevos-claro.webp)

## Paso 6. Desactiva a la persona

Desactivar a una persona la deja sin acceso a ninguna empresa del grupo desde ningún sistema. Su historial se conserva: lo que hizo sigue en los expedientes y en la auditoría.

> **Pantalla:** `/e/{empresa}/usuarios`

![Paso 6: Desactiva a la persona (escritorio)](../../../salida/manual/img/configuracion/usuarios-y-roles/06-desactiva-a-la-persona-claro.webp)

**Variantes**

- **La persona está desactivada:** Su fila sale atenuada con «Desactivada» y ya no aparece en el filtro de activas.

## Paso 7. Comprueba que la persona quedó desactivada

Con el filtro (1) en «Desactivadas» se ve la persona (2) con el estado «Desactivada». Desde ese momento ya no puede entrar a ninguna empresa del grupo.

> **Pantalla:** `/e/{empresa}/usuarios`

![Paso 7: Comprueba que la persona quedó desactivada (escritorio)](../../../salida/manual/img/configuracion/usuarios-y-roles/07-comprueba-que-la-persona-quedo-desactivada-claro.webp)

## Recorridos relacionados

- [Comprueba qué secciones ve cada rol](permisos-por-rol.md)
- [Crea un área, asígnale personas y desactívala](areas-y-miembros.md)
