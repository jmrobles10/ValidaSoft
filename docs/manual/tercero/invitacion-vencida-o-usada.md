---
id: "tercero/invitacion-vencida-o-usada"
titulo: "Qué pasa cuando un enlace de invitación ya se usó, venció o no existe"
actor: "Tercero"
dominio: "tercero"
resumen: "Cada enlace de invitación sirve una sola vez y tiene fecha de vencimiento. Cuando el tercero intenta abrir uno usado, vencido o inexistente, Validasoft no lo deja entrar y le dice qué hacer: pedir a la empresa una invitación nueva."
pasos: 3
---

# Qué pasa cuando un enlace de invitación ya se usó, venció o no existe

**Actor:** Tercero · **Dominio:** Portal del tercero

Cada enlace de invitación sirve una sola vez y tiene fecha de vencimiento. Cuando el tercero intenta abrir uno usado, vencido o inexistente, Validasoft no lo deja entrar y le dice qué hacer: pedir a la empresa una invitación nueva.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de Proveedor que pide código por correo.
- Un tercero invitado cuya invitación ya fue abierta y aceptada una vez desde su correo.

**Pasos**

1. Usa el enlace del correo una sola vez
2. Intenta abrir un enlace que ya se usó
3. Un enlace que no existe

## Paso 1. Usa el enlace del correo una sola vez

Al abrir el enlace y aceptar la invitación, el enlace queda consumido: Validasoft lo guarda como usado. Desde ahora tu acceso a esta empresa es tu sesión, no el enlace. Si cierras el navegador, entra por «/entrar» con tu correo: no necesitas el enlace otra vez.

> **Pantalla:** `/expediente/{id}`

![Paso 1: Usa el enlace del correo una sola vez (escritorio)](../../../salida/manual/img/tercero/invitacion-vencida-o-usada/01-usa-el-enlace-del-correo-una-sola-vez-claro.webp)

![Paso 1: Usa el enlace del correo una sola vez (celular)](../../../salida/manual/img/tercero/invitacion-vencida-o-usada/01-usa-el-enlace-del-correo-una-sola-vez-celular.webp)

**Variantes**

- **El enlace ya se usó y cierras el navegador:** El enlace no sirve de nuevo: entra por «/entrar» con tu correo.

## Paso 2. Intenta abrir un enlace que ya se usó

La pantalla dice «Este enlace ya se usó» y explica que cada enlace sirve una sola vez. Si cerraste la página sin terminar, pide a la empresa que te reenvíe la invitación: te llegará un correo con un enlace nuevo. Validasoft no te deja entrar con un enlace usado, aunque sea tuyo.

> **Pantalla:** `/e/{enlace}`

![Paso 2: Intenta abrir un enlace que ya se usó (escritorio)](../../../salida/manual/img/tercero/invitacion-vencida-o-usada/02-intenta-abrir-un-enlace-que-ya-se-uso-claro.webp)

![Paso 2: Intenta abrir un enlace que ya se usó (celular)](../../../salida/manual/img/tercero/invitacion-vencida-o-usada/02-intenta-abrir-un-enlace-que-ya-se-uso-celular.webp)

**Variantes**

- **El enlace venció por fecha:** La pantalla dice «Este enlace venció» y pide a la empresa un enlace nuevo.

## Paso 3. Un enlace que no existe

Si el enlace está incompleto o ya no existe, la pantalla dice «No encontramos este enlace». Puede que la empresa haya enviado uno más reciente: busca el último correo de invitación. Si no lo tienes, pide a la empresa que te lo reenvíe.

> **Pantalla:** `/e/{enlace}`

![Paso 3: Un enlace que no existe (escritorio)](../../../salida/manual/img/tercero/invitacion-vencida-o-usada/03-un-enlace-que-no-existe-claro.webp)

![Paso 3: Un enlace que no existe (celular)](../../../salida/manual/img/tercero/invitacion-vencida-o-usada/03-un-enlace-que-no-existe-celular.webp)

## Recorridos relacionados

- [Acepta la invitación y completa tu información como persona jurídica](aceptar-invitacion-persona-juridica.md)
- [Entra a Validasoft con tu correo cuando ya tienes una invitación](entrar-sin-invitacion.md)
