---
id: "acceso/ingreso-con-passkey"
titulo: "Registra una passkey y entra con tu huella o tu rostro"
actor: "Revisor/área"
dominio: "acceso"
resumen: "Rita registra una passkey en Mi cuenta: la clave de su dispositivo, que se usa con la huella, el rostro o el PIN. Desde entonces entra con «Entrar con una passkey», sin esperar un código por correo."
pasos: 4
---

# Registra una passkey y entra con tu huella o tu rostro

**Actor:** Revisor/área · **Dominio:** Acceso y cuenta

Rita registra una passkey en Mi cuenta: la clave de su dispositivo, que se usa con la huella, el rostro o el PIN. Desde entonces entra con «Entrar con una passkey», sin esperar un código por correo.

**Antes de empezar**

- Un grupo con ingreso directo y Rita como revisora, con su primera entrada por código ya hecha (sin passkey).
- Un autenticador de prueba del navegador que simula la huella o el PIN del dispositivo.

**Pasos**

1. Abre Mi cuenta y revisa tus passkeys
2. Agrega una passkey y ponle un nombre
3. Revisa la passkey registrada
4. Entra solo con tu passkey

## Paso 1. Abre Mi cuenta y revisa tus passkeys

Desde el menú de tu cuenta, abre «Mi cuenta». La sección «Passkeys» lista las que tienes registradas; hoy está vacía. Una passkey usa la huella, el rostro o el PIN de tu dispositivo (o una llave de seguridad), así que no necesitas recordar nada.

> **Pantalla:** `/e/{empresa}/mi-cuenta`

![Paso 1: Abre Mi cuenta y revisa tus passkeys (escritorio)](../../../salida/manual/img/acceso/ingreso-con-passkey/01-abre-mi-cuenta-y-revisa-tus-passkeys-claro.webp)

**Variantes**

- **Tu grupo exige passkey para todos:** Validasoft te pide registrar una la próxima vez que ingreses.

## Paso 2. Agrega una passkey y ponle un nombre

Al agregar una passkey, escribe un nombre para reconocerla (por ejemplo, «Mi teléfono») y pulsa «Registrar passkey». El navegador te pedirá confirmar con tu huella, tu rostro o el PIN del dispositivo. Validasoft te avisa por correo cada vez que se registra una passkey en tu cuenta.

> **Pantalla:** `/e/{empresa}/mi-cuenta`

![Paso 2: Agrega una passkey y ponle un nombre (escritorio)](../../../salida/manual/img/acceso/ingreso-con-passkey/02-agrega-una-passkey-y-ponle-un-nombre-claro.webp)

## Paso 3. Revisa la passkey registrada

La lista muestra la passkey con su nombre. Desde ahí puedes cambiarle el nombre o eliminarla. Mientras tu cuenta no exija passkey, puedes eliminar la última sin restricción.

> **Pantalla:** `/e/{empresa}/mi-cuenta`

![Paso 3: Revisa la passkey registrada (escritorio)](../../../salida/manual/img/acceso/ingreso-con-passkey/03-revisa-la-passkey-registrada-claro.webp)

## Paso 4. Entra solo con tu passkey

En la pantalla de ingreso pulsa «Entrar con una passkey». El navegador te pide tu huella, tu rostro o el PIN, y entras directo a la app, sin correo ni código. Si la passkey no está disponible en este dispositivo, usa el código de tu correo como antes.

> **Pantalla:** `/e/{empresa}/inicio`

![Paso 4: Entra solo con tu passkey (escritorio)](../../../salida/manual/img/acceso/ingreso-con-passkey/04-entra-solo-con-tu-passkey-claro.webp)

## Recorridos relacionados

- [Entra a Validasoft con tu correo y un código](ingreso-directo-por-correo.md)
- [Revisa tus sesiones abiertas y cierra la que no reconoces](mi-cuenta-sesiones-y-codigos.md)
- [Genera códigos de recuperación por si pierdes tu passkey](codigos-de-recuperacion.md)
