---
id: "plataforma/secretos"
titulo: "Revisa los secretos en reposo y la clave de arranque"
actor: "Plataforma"
dominio: "plataforma"
resumen: "El operador de Plataforma revisa cómo están guardados los secretos de la plataforma: las llaves de OpenAI, los secretos de webhooks, los números de cuenta y las credenciales de listas. Verifica que estén con la clave maestra vigente y revisa el estado de la clave de arranque."
pasos: 3
---

# Revisa los secretos en reposo y la clave de arranque

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

El operador de Plataforma revisa cómo están guardados los secretos de la plataforma: las llaves de OpenAI, los secretos de webhooks, los números de cuenta y las credenciales de listas. Verifica que estén con la clave maestra vigente y revisa el estado de la clave de arranque.

**Antes de empezar**

- Un operador de Plataforma con la consola abierta.
- Secretos guardados con la clave maestra vigente.

**Pasos**

1. Abre Secretos en reposo
2. Lee la clave vigente y las anteriores
3. Revisa la clave de arranque

## Paso 1. Abre Secretos en reposo

Algunos datos sensibles se guardan cifrados con la clave maestra de la plataforma: las llaves de OpenAI, los secretos de los webhooks, los números de cuenta de los terceros y las credenciales de las listas restrictivas. La tabla cuenta por tipo cuántos secretos hay, cuántos están con la clave vigente, cuántos esperan re-cifrado y cuántos son ilegibles. Ningún valor se muestra: solo se ven conteos.

> **Pantalla:** `/plataforma/secretos`

![Paso 1: Abre Secretos en reposo (escritorio)](../../../salida/manual/img/plataforma/secretos/01-abre-secretos-en-reposo-claro.webp)

**Configuración que lo afecta**

- **Clave maestra de secretos** (`Plataforma › Secretos (/plataforma/secretos)`): Decide con qué clave se cifran los secretos guardados. Al rotarla, hay que re-cifrar lo que quedó con la anterior.

## Paso 2. Lee la clave vigente y las anteriores

Arriba se indica cuál es la clave maestra vigente y cuántas claves anteriores están configuradas. Si todo está cifrado con la vigente, la pantalla dice que todo está con la clave vigente. Con «Revisar de nuevo» vuelves a contar los secretos sin salir de la pantalla.

> **Pantalla:** `/plataforma/secretos`

![Paso 2: Lee la clave vigente y las anteriores (escritorio)](../../../salida/manual/img/plataforma/secretos/02-lee-la-clave-vigente-y-las-anteriores-claro.webp)

**Variantes**

- **Hay secretos con una clave anterior:** Aparecen como «Por re-cifrar» y se pueden re-cifrar con la clave vigente.
- **Hay secretos ilegibles:** La pantalla pide agregar su clave a la configuración de claves anteriores; no se pueden re-cifrar hasta entonces.

## Paso 3. Revisa la clave de arranque

La clave de arranque es la llave de configuración del servidor que da de alta al primer operador de Plataforma. Aquí ves si está activa y cuántos operadores ya entran con su passkey. Mientras no haya al menos un operador con passkey, no se puede desactivar: nadie podría volver a entrar a la consola.

> **Pantalla:** `/plataforma/secretos`

![Paso 3: Revisa la clave de arranque (escritorio)](../../../salida/manual/img/plataforma/secretos/03-revisa-la-clave-de-arranque-claro.webp)

**Variantes**

- **Hay operadores con passkey:** Aparece el aviso «Ya hay operadores con passkey: puedes desactivar la clave de arranque» y el botón «Desactivar» se habilita.
- **No hay operadores con passkey:** El botón «Desactivar» queda deshabilitado y el aviso pide registrar una passkey antes.

**Configuración que lo afecta**

- **Clave de arranque** (`Plataforma › Secretos › Clave de arranque`): Sirve solo para dar de alta al primer operador; desactivarla cierra ese acceso de servidor.

## Recorridos relacionados

- [Crea un operador de la consola y le quita el acceso](operadores.md)
- [Configura el plan de IA de una empresa y su llave de OpenAI](plan-de-ia-y-llave-openai.md)
- [Configura la cuenta de Infolaft de la plataforma](listas-restrictivas-de-la-plataforma.md)
