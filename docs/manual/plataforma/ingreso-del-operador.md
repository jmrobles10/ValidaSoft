---
id: "plataforma/ingreso-del-operador"
titulo: "Entra a la consola de Plataforma con correo, código y passkey"
actor: "Plataforma"
dominio: "plataforma"
resumen: "Un operador de Plataforma entra a la consola sin contraseña: pide un código a su correo, lo escribe, registra una passkey en su primer ingreso y, en los siguientes, entra con esa passkey."
pasos: 5
---

# Entra a la consola de Plataforma con correo, código y passkey

**Actor:** Plataforma · **Dominio:** Consola de Plataforma

Un operador de Plataforma entra a la consola sin contraseña: pide un código a su correo, lo escribe, registra una passkey en su primer ingreso y, en los siguientes, entra con esa passkey.

**Antes de empezar**

- Un operador de Plataforma dado de alta con su correo (ingreso directo).
- Acceso al correo del operador para leer el código.

**Pasos**

1. Escribe tu correo
2. Revisa tu correo
3. Registra tu passkey
4. Llega a la consola
5. Entra en otro ingreso con tu passkey

## Paso 1. Escribe tu correo

Validasoft no usa contraseña para los operadores. Escribe el correo con el que te dieron de alta y pulsa «Continuar». Si el correo no corresponde a un operador de ingreso directo, la pantalla responde lo mismo que para cualquier otro correo: así no se revela quién tiene acceso.

> **Pantalla:** `/`

![Paso 1: Escribe tu correo (escritorio)](../../../salida/manual/img/plataforma/ingreso-del-operador/01-escribe-tu-correo-claro.webp)

**Configuración que lo afecta**

- **Ingreso directo de operadores** (`Plataforma › Operadores (/plataforma/operadores)`): Solo quien está dado de alta como operador con ingreso directo recibe el código.

## Paso 2. Revisa tu correo

Validasoft te envía un correo con un código de seis casillas. Ábrelo y copia el código: vence pronto y, si te equivocas varias veces, hay que pedir uno nuevo. El código no es tu contraseña: sirve solo para este ingreso.

> **Pantalla:** `/`

![Paso 2: Revisa tu correo (escritorio)](../../../salida/manual/img/plataforma/ingreso-del-operador/02-revisa-tu-correo-claro.webp)

**Variantes**

- **El código venció:** La pantalla dice que el código venció y ofrece pedir uno nuevo.
- **Se agotaron los intentos:** Se bloquea ese código y hay que pedir uno nuevo.

## Paso 3. Registra tu passkey

En tu primer ingreso registras una passkey: una llave del dispositivo (huella, rostro o PIN) que reemplaza a la contraseña. Pulsa «Registrar passkey» y confirma con tu dispositivo. Para los operadores la passkey es obligatoria: sin ella no se puede entrar a la consola.

> **Pantalla:** `/`

![Paso 3: Registra tu passkey (escritorio)](../../../salida/manual/img/plataforma/ingreso-del-operador/03-registra-tu-passkey-claro.webp)

**Configuración que lo afecta**

- **Passkey obligatoria para operadores**: Sin una passkey registrada, la cuenta no puede entrar a la consola.

## Paso 4. Llega a la consola

Una cuenta de operador no pertenece a ninguna empresa: va directo a la consola de Plataforma,. Desde aquí administras grupos, operadores, uso de IA, ingresos y secretos.

> **Pantalla:** `/plataforma/grupos`

![Paso 4: Llega a la consola (escritorio)](../../../salida/manual/img/plataforma/ingreso-del-operador/04-llega-a-la-consola-claro.webp)

## Paso 5. Entra en otro ingreso con tu passkey

Al cerrar sesión vuelves a la pantalla de ingreso. Para entrar otra vez no necesitas código: pulsa «Entrar con una passkey» y confirma con tu dispositivo. Si perdiste la passkey, pide a otro operador que la revoque y registra una nueva.

> **Pantalla:** `/`

![Paso 5: Entra en otro ingreso con tu passkey (escritorio)](../../../salida/manual/img/plataforma/ingreso-del-operador/05-entra-en-otro-ingreso-con-tu-passkey-claro.webp)

## Recorridos relacionados

- [Crea un operador de la consola y le quita el acceso](operadores.md)
- [Revisa quién entró a la consola y filtra los ingresos fallidos](ingresos-de-los-operadores.md)
- [Registra una passkey y entra con tu huella o tu rostro](../acceso/ingreso-con-passkey.md)
