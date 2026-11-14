---
id: "integracion/webhook-ingreso-directo"
titulo: "Crea un webhook entrando directo a Validasoft y elige su sistema"
actor: "Administrador de empresa"
dominio: "integracion"
resumen: "La administradora entra directo a Validasoft, con su correo y un código, y crea un webhook. Como no viene de un sistema integrado, el panel le pide elegir cuál de los sistemas del grupo es el dueño del webhook: a ese sistema se le avisan sus eventos."
pasos: 2
---

# Crea un webhook entrando directo a Validasoft y elige su sistema

**Actor:** Administrador de empresa · **Dominio:** Integración

La administradora entra directo a Validasoft, con su correo y un código, y crea un webhook. Como no viene de un sistema integrado, el panel le pide elegir cuál de los sistemas del grupo es el dueño del webhook: a ese sistema se le avisan sus eventos.

**Antes de empezar**

- Un grupo con ingreso directo, una administradora que ya registró su passkey y dos sistemas integrados activos: «ERP de compras» y «Portal de proveedores».
- Una dirección local que recibe las entregas del webhook.

**Pasos**

1. Elige el sistema integrado dueño del webhook
2. Revisa el webhook creado

## Paso 1. Elige el sistema integrado dueño del webhook

Entraste directo a Validasoft, no desde un sistema integrado, así que el panel pregunta de qué sistema es el webhook: sus eventos se le avisan a ese sistema. La lista trae los sistemas integrados activos del grupo, con su nombre y su código. Sin elegir uno no puedes guardar. Si entras desde un sistema integrado, el panel no pregunta: el webhook queda a nombre del sistema desde el que entraste.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 1: Elige el sistema integrado dueño del webhook (escritorio)](../../../salida/manual/img/integracion/webhook-ingreso-directo/01-elige-el-sistema-integrado-dueno-del-webhook-claro.webp)

**Variantes**

- **El grupo tiene un solo sistema integrado activo:** El panel lo toma solo: no hay nada que elegir.
- **El grupo no tiene sistemas integrados activos:** El campo queda desactivado y explica que primero hay que crear uno en Configuración › Sistemas integrados.
- **Editas un webhook que ya existe:** No vuelve a pedir el sistema: el dueño de un webhook no cambia.

**Configuración que lo afecta**

- **Sistemas integrados del grupo** (`Configuración › Sistemas integrados (/sistemas)`): Solo los sistemas activos aparecen como dueños posibles de un webhook.

## Paso 2. Revisa el webhook creado

Al guardar, Validasoft muestra el secreto de firma una sola vez (como en [Crea un webhook, guarda su secreto y revisa las entregas firmadas](webhooks.md)) y el webhook aparece en la tabla, activo y con su dirección. El detalle del webhook muestra el «Sistema integrado» dueño; desde ahora los eventos que elegiste se envían a esa dirección y se le avisan a ese sistema.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 2: Revisa el webhook creado (escritorio)](../../../salida/manual/img/integracion/webhook-ingreso-directo/02-revisa-el-webhook-creado-claro.webp)

## Recorridos relacionados

- [Crea un webhook, guarda su secreto y revisa las entregas firmadas](webhooks.md)
- [Registra un sistema integrado con su clave pública](sistema-integrador.md)
- [Entra a Validasoft con tu correo y un código](../acceso/ingreso-directo-por-correo.md)
