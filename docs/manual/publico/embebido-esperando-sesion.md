---
id: "publico/embebido-esperando-sesion"
titulo: "Entiende la pantalla «Conectando con tu sistema» de una pantalla embebida"
actor: "Público"
dominio: "publico"
resumen: "Las pantallas del tercero embebidas (`/embed/…`) no tienen inicio de sesión propio: esperan a que el sistema que las aloja les entregue una sesión. Aquí se ve qué pasa al abrirlas sin sistema, cuando el sistema no responde y cuando entrega una sesión que no corresponde."
pasos: 3
---

# Entiende la pantalla «Conectando con tu sistema» de una pantalla embebida

**Actor:** Público · **Dominio:** Verificación pública

Las pantallas del tercero embebidas (`/embed/…`) no tienen inicio de sesión propio: esperan a que el sistema que las aloja les entregue una sesión. Aquí se ve qué pasa al abrirlas sin sistema, cuando el sistema no responde y cuando entrega una sesión que no corresponde.

**Antes de empezar**

- Un grupo con Constructora Norte, un tercero que aceptó su invitación de cliente y el sistema integrado E2E con dos orígenes permitidos.

**Pasos**

1. Abre una pantalla embebida sin sistema
2. Mira qué pide la pantalla al sistema anfitrión
3. Entrega una sesión que no corresponde

## Paso 1. Abre una pantalla embebida sin sistema

Las direcciones `/embed/…` no son para abrirlas en el navegador: las carga el sistema de la empresa dentro de un iframe, que es quien identifica a la persona. Si las abres directamente, no hay quien entregue la sesión y la pantalla se queda en «Conectando con tu sistema…», sin pedir correo ni mostrar datos del tercero.

> **Pantalla:** `/embed/boveda`

![Paso 1: Abre una pantalla embebida sin sistema (escritorio)](../../../salida/manual/img/publico/embebido-esperando-sesion/01-abre-una-pantalla-embebida-sin-sistema-claro.webp)

![Paso 1: Abre una pantalla embebida sin sistema (celular)](../../../salida/manual/img/publico/embebido-esperando-sesion/01-abre-una-pantalla-embebida-sin-sistema-celular.webp)

**Variantes**

- **La persona busca sus documentos:** Debe entrar por su correo en la dirección normal de Validasoft, no por una dirección /embed.

## Paso 2. Mira qué pide la pantalla al sistema anfitrión

Dentro del iframe de un sistema permitido, la pantalla avisa al sistema que está lista (`validasoft:ready`) y espera. El sistema debe responder con la sesión que su backend pidió a Validasoft (`validasoft:sesion`). Mientras no llegue, el iframe sigue en «Conectando con tu sistema…» (1) y la lista de mensajes (2) solo muestra lo que la pantalla pidió.

> **Pantalla:** `/`

![Paso 2: Mira qué pide la pantalla al sistema anfitrión (escritorio)](../../../salida/manual/img/publico/embebido-esperando-sesion/02-mira-que-pide-la-pantalla-al-sistema-anfitrion-claro.webp)

![Paso 2: Mira qué pide la pantalla al sistema anfitrión (celular)](../../../salida/manual/img/publico/embebido-esperando-sesion/02-mira-que-pide-la-pantalla-al-sistema-anfitrion-celular.webp)

**Configuración que lo afecta**

- **Orígenes permitidos del sistema** (`Configuración › Sistemas integrados (/sistemas)`): Solo las páginas de esos orígenes pueden alojar el iframe y recibir la sesión.

## Paso 3. Entrega una sesión que no corresponde

La sesión que entrega el sistema solo vale para el origen para el que se emitió. Aquí la página está en otro origen permitido del mismo sistema, pero el token se pidió para el primero: la pantalla lo rechaza, pide otra sesión al sistema (`validasoft:sesion-requerida` con motivo «rechazada») y muestra «Esta pantalla solo se muestra dentro de tu sistema» (1) con un botón «Reintentar» (2), sin mostrar nada del tercero. Hay que volver a pedir la sesión para el origen correcto.

> **Pantalla:** `/`

![Paso 3: Entrega una sesión que no corresponde (escritorio)](../../../salida/manual/img/publico/embebido-esperando-sesion/03-entrega-una-sesion-que-no-corresponde-claro.webp)

![Paso 3: Entrega una sesión que no corresponde (celular)](../../../salida/manual/img/publico/embebido-esperando-sesion/03-entrega-una-sesion-que-no-corresponde-celular.webp)

**Variantes**

- **El origen de la página no está registrado en el sistema:** El navegador ni siquiera dibuja el iframe: Validasoft solo permite que lo aloje un origen permitido, y no se ve nada del tercero.
- **Se abre la dirección fuera de un iframe:** Se queda en «Conectando con tu sistema…», como en el primer paso.

## Recorridos relacionados

- [Muestra la bóveda y la ficha del tercero dentro de tu sistema](../embebido/tercero-embebido.md)
- [Declara qué páginas pueden mostrar Validasoft](../embebido/origenes-permitidos.md)
- [Renueva una sesión que venció sin perder lo que estabas haciendo](../embebido/sesion-vencida.md)
