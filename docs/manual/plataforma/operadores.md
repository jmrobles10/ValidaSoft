---
id: "plataforma/operadores"
titulo: "Crea un operador de la consola y le quita el acceso"
actor: "Plataforma"
dominio: "plataforma"
resumen: "Un operador de Plataforma da de alta a otra persona que operará la consola. Después la desactiva al dejar el puesto: se cierran sus sesiones y se revocan sus passkeys; luego, si hace falta, la reactiva para que registre una passkey nueva."
pasos: 5
---

# Crea un operador de la consola y le quita el acceso

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

Un operador de Plataforma da de alta a otra persona que operará la consola. Después la desactiva al dejar el puesto: se cierran sus sesiones y se revocan sus passkeys; luego, si hace falta, la reactiva para que registre una passkey nueva.

**Antes de empezar**

- Un operador de Plataforma con la consola abierta.
- Un correo que no esté registrado como operador.

**Pasos**

1. Abre la lista de operadores
2. Da de alta al nuevo operador
3. Revisa el operador en la lista
4. Desactiva a un operador
5. Reactiva al operador

## Paso 1. Abre la lista de operadores

Los operadores son las personas que manejan la consola de Plataforma. La tabla muestra a cada uno con su correo, cómo entra, cuántas passkeys tiene, su último ingreso y su estado. Un operador de ingreso directo entra con su correo, un código y una passkey obligatoria; uno que entra desde un sistema integrado aparece con el nombre de ese sistema.

> **Pantalla:** `/plataforma/operadores`

![Paso 1: Abre la lista de operadores (escritorio)](../../../salida/manual/img/plataforma/operadores/01-abre-la-lista-de-operadores-claro.webp)

**Variantes**

- **Ingreso directo:** Entra con «Correo, código y passkey» y la columna de passkeys cuenta las que tiene registradas.
- **Ingreso desde un sistema:** Entra con «Backoffice» y el nombre del sistema integrado que lo autentica.

**Configuración que lo afecta**

- **Operadores de la consola** (`Plataforma › Operadores (/plataforma/operadores)`): Quien aparece aquí puede entrar a la consola de Plataforma según su forma de ingreso.

## Paso 2. Da de alta al nuevo operador

Escribe el nombre y el correo de la persona. Al crearla, Validasoft le envía un correo para entrar: no hay contraseña que compartir. Entra con su correo, un código y una passkey obligatoria, que registra en su primer ingreso.

> **Pantalla:** `/plataforma/operadores`

![Paso 2: Da de alta al nuevo operador (escritorio)](../../../salida/manual/img/plataforma/operadores/02-da-de-alta-al-nuevo-operador-claro.webp)

**Variantes**

- **Falta el nombre:** El campo se marca en rojo y no se puede crear.

## Paso 3. Revisa el operador en la lista

El operador aparece en la lista como activo, con el correo con el que entrará. Su último ingreso dice «Nunca» hasta que entre por primera vez. Desde el menú de su fila puedes desactivarlo cuando deje el puesto.

> **Pantalla:** `/plataforma/operadores`

![Paso 3: Revisa el operador en la lista (escritorio)](../../../salida/manual/img/plataforma/operadores/03-revisa-el-operador-en-la-lista-claro.webp)

## Paso 4. Desactiva a un operador

Desactivar a un operador le quita la entrada a la consola de inmediato: sus sesiones se cierran, sus passkeys se revocan y su token deja de servir. Su historial se conserva en los ingresos de los operadores. Esto sirve cuando alguien deja el puesto o pierde su acceso.

> **Pantalla:** `/plataforma/operadores`

![Paso 4: Desactiva a un operador (escritorio)](../../../salida/manual/img/plataforma/operadores/04-desactiva-a-un-operador-claro.webp)

**Configuración que lo afecta**

- **Desactivar un operador**: Cierra sus sesiones y revoca sus passkeys; no se pierde su historial.

## Paso 5. Reactiva al operador

Reactivarlo le permite volver a entrar a la consola. Sus sesiones anteriores no se recuperan: recibe un correo para entrar y registrar una passkey nueva. Mientras esté desactivado, su estado aparece como «Desactivado» en la lista.

> **Pantalla:** `/plataforma/operadores`

![Paso 5: Reactiva al operador (escritorio)](../../../salida/manual/img/plataforma/operadores/05-reactiva-al-operador-claro.webp)

**Configuración que lo afecta**

- **Reactivar un operador**: Le envía un correo para entrar y registrar una passkey nueva.

## Recorridos relacionados

- [Entra a la consola de Plataforma con correo, código y passkey](ingreso-del-operador.md)
- [Revisa quién entró a la consola y filtra los ingresos fallidos](ingresos-de-los-operadores.md)
- [Revisa los secretos en reposo y la clave de arranque](secretos.md)
