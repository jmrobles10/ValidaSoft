---
id: "vinculos/detalle-del-vinculo"
titulo: "Revisa el detalle de un vínculo"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora abre un vínculo desde la lista y revisa sus datos, el estado de la invitación, las referencias que le dieron los sistemas integrados y el historial de eventos."
pasos: 4
---

# Revisa el detalle de un vínculo

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora abre un vínculo desde la lista y revisa sus datos, el estado de la invitación, las referencias que le dieron los sistemas integrados y el historial de eventos.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Norte» y una administradora del grupo.
- Un vínculo de proveedor invitado por el sistema integrado, con su referencia externa.

**Pasos**

1. Lee los datos del vínculo
2. Revisa el estado de la invitación
3. Revisa las referencias de cada sistema
4. Revisa el historial del vínculo

## Paso 1. Lee los datos del vínculo

El detalle abre con el tercero, su identificación, el tipo de vínculo y la empresa. «Contacto» es el correo al que se envían las invitaciones y los avisos de esta relación. «Creado» y «Última actualización» muestran cuándo se registró y cuándo cambió por última vez; el estado aparece junto al nombre del tercero.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Lee los datos del vínculo (escritorio)](../../../salida/manual/img/vinculos/detalle-del-vinculo/01-lee-los-datos-del-vinculo-claro.webp)

**Variantes**

- **El tercero ya tiene ficha en Validasoft:** El nombre es un enlace a su ficha, y la identificación se ve completa.
- **El vínculo tiene atributos definidos por el tipo:** Aparece una tarjeta «Atributos» con sus valores.

## Paso 2. Revisa el estado de la invitación

En «Invitación» ves cuántos correos se enviaron y hasta cuándo vale el enlace. Mientras el tercero no lo acepte, el vínculo sigue en «Invitado»; cuando lo acepta, aparece «Aceptada» con la fecha. Si el vínculo se creó sin enviar correo, dice «Sin invitación enviada».

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Revisa el estado de la invitación (escritorio)](../../../salida/manual/img/vinculos/detalle-del-vinculo/02-revisa-el-estado-de-la-invitacion-claro.webp)

**Variantes**

- **El vínculo lo creó un sistema integrado sin enviar correo:** La invitación dice «Sin invitación enviada» y el tercero no recibe nada hasta reenviarla.

## Paso 3. Revisa las referencias de cada sistema

Cuando un sistema integrado crea o enlaza el vínculo, su identificador queda en «Referencias externas» con el nombre del sistema y su referencia (aquí «E2E» y «PRO-923619687-ND2388»). Un mismo vínculo puede tener una referencia por cada sistema; con esa referencia se encuentra también desde la lista.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Revisa las referencias de cada sistema (escritorio)](../../../salida/manual/img/vinculos/detalle-del-vinculo/03-revisa-las-referencias-de-cada-sistema-claro.webp)

**Configuración que lo afecta**

- **Sistemas integrados** (`Configuración › Sistemas (/sistemas)`): Cada sistema que crea vínculos deja su referencia aquí.

## Paso 4. Revisa el historial del vínculo

El historial es la línea de tiempo de lo que pasó con el vínculo: cuándo se creó, cuándo se envió la invitación y cada cambio de estado o de datos, con quién lo hizo. Es lo primero que revisas cuando alguien pregunta qué pasó con un tercero.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 4: Revisa el historial del vínculo (escritorio)](../../../salida/manual/img/vinculos/detalle-del-vinculo/04-revisa-el-historial-del-vinculo-claro.webp)

## Recorridos relacionados

- [Revisa la lista de vínculos y filtra por estado y tipo](lista-y-filtros.md)
- [Reenvía la invitación a un tercero](reenviar-invitacion.md)
