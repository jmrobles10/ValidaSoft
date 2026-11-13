---
id: "tercero/entrar-sin-invitacion"
titulo: "Entra a Validasoft con tu correo cuando ya tienes una invitación"
actor: "Tercero"
dominio: "tercero"
resumen: "Un proveedor que ya fue invitado no abre el enlace del correo: entra por «/entrar», escribe el correo de la invitación y recibe un enlace de un solo uso. Desde «Mis empresas» ve todas sus empresas y abre la que le interesa. Si el correo no está registrado, la pantalla responde igual."
pasos: 5
---

# Entra a Validasoft con tu correo cuando ya tienes una invitación

**Actor:** Tercero · **Dominio:** Portal del tercero

Un proveedor que ya fue invitado no abre el enlace del correo: entra por «/entrar», escribe el correo de la invitación y recibe un enlace de un solo uso. Desde «Mis empresas» ve todas sus empresas y abre la que le interesa. Si el correo no está registrado, la pantalla responde igual.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Proveedor publicado.
- Un tercero invitado por esa empresa, que nunca ha entrado a Validasoft con su correo.

**Pasos**

1. Entra con tu correo
2. Revisa tu correo
3. Abre el enlace del correo
4. Revisa tus empresas en «Mis empresas»
5. Un correo sin invitación recibe la misma respuesta

## Paso 1. Entra con tu correo

Desde la página de inicio entras con «Entrar con mi correo». Escribe el correo al que te llegó la invitación de la empresa: no hay contraseña. El botón «Enviarme el enlace» te manda a ese mismo correo un enlace para entrar; si la empresa lo pide, también un código.

> **Pantalla:** `/entrar`

![Paso 1: Entra con tu correo (escritorio)](../../../salida/manual/img/tercero/entrar-sin-invitacion/01-entra-con-tu-correo-claro.webp)

![Paso 1: Entra con tu correo (celular)](../../../salida/manual/img/tercero/entrar-sin-invitacion/01-entra-con-tu-correo-celular.webp)

**Variantes**

- **Escribes un correo que no es válido:** La pantalla te pide un correo válido, por ejemplo nombre@empresa.com, y no envía nada.
- **Ya tienes una sesión abierta:** No ves esta pantalla: Validasoft te lleva directo a «Mis empresas».

## Paso 2. Revisa tu correo

La pantalla dice «Revisa tu correo» y, si tu correo está registrado, te enviamos un enlace para entrar. El correo llega a la dirección de tu invitación; revisa también la carpeta de spam. El enlace sirve una sola vez: si ya lo usaste, pide otro con «Usar otro correo» o entra con el de la invitación.

> **Pantalla:** `/entrar`

![Paso 2: Revisa tu correo (escritorio)](../../../salida/manual/img/tercero/entrar-sin-invitacion/02-revisa-tu-correo-claro.webp)

![Paso 2: Revisa tu correo (celular)](../../../salida/manual/img/tercero/entrar-sin-invitacion/02-revisa-tu-correo-celular.webp)

## Paso 3. Abre el enlace del correo

El enlace te pide el código de 6 dígitos que llega al mismo correo, porque el tipo de vínculo Proveedor lo exige. Como todavía no aceptas la invitación de la empresa, el enlace te lleva a sus textos legales: los aceptas y entonces llegas a «Mis empresas», la lista de todas las empresas que te invitaron o con las que tienes un vínculo.

> **Pantalla:** `/empresas/{id}`

![Paso 3: Abre el enlace del correo (escritorio)](../../../salida/manual/img/tercero/entrar-sin-invitacion/03-abre-el-enlace-del-correo-claro.webp)

![Paso 3: Abre el enlace del correo (celular)](../../../salida/manual/img/tercero/entrar-sin-invitacion/03-abre-el-enlace-del-correo-celular.webp)

**Variantes**

- **La invitación ya fue aceptada:** El enlace te lleva directo a «Mis empresas».
- **El enlace ya se usó o venció:** En lugar de la invitación ves un aviso de enlace no válido; pide otro con tu correo.

## Paso 4. Revisa tus empresas en «Mis empresas»

«Mis empresas» lista todas las empresas que te invitaron o con las que tienes un vínculo. Cada tarjeta muestra la empresa, el tipo de vínculo (Proveedor, Cliente…) y el estado de tu revisión. Abre la tarjeta para continuar con esa empresa.

> **Pantalla:** `/empresas`

![Paso 4: Revisa tus empresas en «Mis empresas» (escritorio)](../../../salida/manual/img/tercero/entrar-sin-invitacion/04-revisa-tus-empresas-en-mis-empresas-claro.webp)

![Paso 4: Revisa tus empresas en «Mis empresas» (celular)](../../../salida/manual/img/tercero/entrar-sin-invitacion/04-revisa-tus-empresas-en-mis-empresas-celular.webp)

**Variantes**

- **Tienes invitaciones de varias empresas:** Ves una tarjeta por cada empresa y tipo de vínculo, cada una con su estado.

## Paso 5. Un correo sin invitación recibe la misma respuesta

Si escribes un correo que no está registrado en ninguna empresa, la pantalla responde lo mismo: «Revisa tu correo». Así nadie puede averiguar qué personas están invitadas. Si no te llega nada, revisa que escribiste el correo exacto con el que te invitó la empresa.

> **Pantalla:** `/entrar`

![Paso 5: Un correo sin invitación recibe la misma respuesta (escritorio)](../../../salida/manual/img/tercero/entrar-sin-invitacion/05-un-correo-sin-invitacion-recibe-la-misma-respues-claro.webp)

![Paso 5: Un correo sin invitación recibe la misma respuesta (celular)](../../../salida/manual/img/tercero/entrar-sin-invitacion/05-un-correo-sin-invitacion-recibe-la-misma-respues-celular.webp)

**Variantes**

- **Pides muchos enlaces seguidos:** La pantalla dice que pediste varios enlaces seguidos y te pide esperar unos minutos antes de volver a intentar.

## Recorridos relacionados

- [Acepta la invitación y completa tu información como persona jurídica](aceptar-invitacion-persona-juridica.md)
- [Qué pasa cuando un enlace de invitación ya se usó, venció o no existe](invitacion-vencida-o-usada.md)
