---
id: "vinculos/reenviar-invitacion"
titulo: "Reenvía la invitación a un tercero"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora reenvía la invitación a un proveedor que aún no ha aceptado, elige el correo y la vigencia del enlace, y confirma el envío."
pasos: 3
---

# Reenvía la invitación a un tercero

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora reenvía la invitación a un proveedor que aún no ha aceptado, elige el correo y la vigencia del enlace, y confirma el envío.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Norte» y una administradora del grupo.
- Un vínculo de proveedor invitado que no ha sido aceptado.

**Pasos**

1. Abre la acción «Reenviar invitación»
2. Confirma el correo y la vigencia del enlace
3. Comprueba el nuevo envío en el detalle

## Paso 1. Abre la acción «Reenviar invitación»

Mientras el vínculo está en «Invitado», el detalle muestra el botón «Reenviar invitación» junto al nombre. Úsalo cuando el tercero no encontró el correo, el enlace venció o cambiaste el correo de contacto. Cada envío aumenta el contador de «Envíos» en Datos del vínculo.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Abre la acción «Reenviar invitación» (escritorio)](../../../salida/manual/img/vinculos/reenviar-invitacion/01-abre-la-accion-reenviar-invitacion-claro.webp)

**Variantes**

- **El tercero ya aceptó la invitación:** Ya no aparece el botón: el vínculo ya no está en «Invitado».

## Paso 2. Confirma el correo y la vigencia del enlace

El diálogo propone el correo de contacto del tercero; puedes cambiarlo si el tercero usa otra dirección. «Vigencia del enlace (horas)» indica cuánto tiempo sirve el enlace nuevo: pasado ese plazo, el enlace deja de abrir y hay que reenviar. El tercero recibe un correo con el enlace para completar su información.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Confirma el correo y la vigencia del enlace (escritorio)](../../../salida/manual/img/vinculos/reenviar-invitacion/02-confirma-el-correo-y-la-vigencia-del-enlace-claro.webp)

## Paso 3. Comprueba el nuevo envío en el detalle

Validasoft confirma con un aviso que la invitación se envió a ese correo y el detalle actualiza «Invitación» con el número de envíos. El historial registra el reenvío con su fecha y quién lo hizo.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Comprueba el nuevo envío en el detalle (escritorio)](../../../salida/manual/img/vinculos/reenviar-invitacion/03-comprueba-el-nuevo-envio-en-el-detalle-claro.webp)

## Recorridos relacionados

- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
- [Qué pasa cuando un enlace de invitación ya se usó, venció o no existe](../tercero/invitacion-vencida-o-usada.md)
