---
id: "acceso/ingreso-directo-por-correo"
titulo: "Entra a Validasoft con tu correo y un código"
actor: "Revisor/área"
dominio: "acceso"
resumen: "Rita entra sin contraseña: escribe su correo, recibe un código de seis caracteres en su bandeja y lo escribe en la pantalla. Después la sesión sigue abierta al recargar la página, hasta que cierra sesión."
pasos: 5
---

# Entra a Validasoft con tu correo y un código

**Actor:** Revisor/área · **Dominio:** Acceso y cuenta

Rita entra sin contraseña: escribe su correo, recibe un código de seis caracteres en su bandeja y lo escribe en la pantalla. Después la sesión sigue abierta al recargar la página, hasta que cierra sesión.

**Antes de empezar**

- Un grupo con ingreso directo (sin SSO) y Rita como revisora de su empresa, sin passkey registrada.

**Pasos**

1. Abre la pantalla de ingreso
2. Revisa tu correo y escribe el código
3. Entra a la app con el código
4. Recarga sin volver a pedir el código
5. Cierra la sesión

## Paso 1. Abre la pantalla de ingreso

Validasoft no pide contraseña. Escribe tu correo en el campo «Correo» y pulsa «Continuar». Si tu cuenta ya tiene una passkey, puedes usar el botón «Entrar con una passkey» en su lugar.

> **Pantalla:** `/`

![Paso 1: Abre la pantalla de ingreso (escritorio)](../../../salida/manual/img/acceso/ingreso-directo-por-correo/01-abre-la-pantalla-de-ingreso-claro.webp)

**Variantes**

- **Tu navegador no admite passkeys:** El botón de passkey no aparece y ves el aviso de usar el código del correo.
- **Tu empresa ingresa con su propio inicio de sesión:** La pantalla te pide entrar con tu cuenta de trabajo en lugar del código.

## Paso 2. Revisa tu correo y escribe el código

Validasoft envía un correo con el asunto «Tu código para ingresar a Validasoft». Escribe el código que trae en las casillas de esta pantalla; no hace falta pulsar nada más. El código entra solo al escribirlo completo.

> **Pantalla:** `/`

![Paso 2: Revisa tu correo y escribe el código (escritorio)](../../../salida/manual/img/acceso/ingreso-directo-por-correo/02-revisa-tu-correo-y-escribe-el-codigo-claro.webp)

## Paso 3. Entra a la app con el código

Con el código correcto entras directo a la app, con las secciones de tu rol. Como tu cuenta aún no tiene passkey, Validasoft te sugiere registrar una para entrar más rápido, sin código.

> **Pantalla:** `/e/{empresa}/inicio`

![Paso 3: Entra a la app con el código (escritorio)](../../../salida/manual/img/acceso/ingreso-directo-por-correo/03-entra-a-la-app-con-el-codigo-claro.webp)

## Paso 4. Recarga sin volver a pedir el código

Al recargar la página, Validasoft recupera tu sesión con la cookie de inicio y no te pide otro código. Así no tienes que volver a ingresar en cada pestaña.

> **Pantalla:** `/e/{empresa}/inicio`

![Paso 4: Recarga sin volver a pedir el código (escritorio)](../../../salida/manual/img/acceso/ingreso-directo-por-correo/04-recarga-sin-volver-a-pedir-el-codigo-claro.webp)

**Variantes**

- **Pasan más horas que la duración de la sesión del grupo:** La sesión vence y la pantalla de ingreso vuelve a pedir el correo.

## Paso 5. Cierra la sesión

Desde el menú de tu cuenta, «Cerrar sesión» termina la sesión en este navegador. Después, recargar la página ya no te devuelve a la app: hay que ingresar de nuevo con tu correo.

> **Pantalla:** `/`

![Paso 5: Cierra la sesión (escritorio)](../../../salida/manual/img/acceso/ingreso-directo-por-correo/05-cierra-la-sesion-claro.webp)

## Recorridos relacionados

- [Registra una passkey y entra con tu huella o tu rostro](ingreso-con-passkey.md)
- [Revisa tus sesiones abiertas y cierra la que no reconoces](mi-cuenta-sesiones-y-codigos.md)
