---
id: "embebido/sesion-vencida"
titulo: "Renueva una sesión que venció sin perder lo que estabas haciendo"
actor: "Revisor/área"
dominio: "embebido"
resumen: "La sesión de la app dura 30 minutos. Dos minutos antes de que venza, la app pide una sesión nueva a tu sistema; si el sistema no responde a tiempo, la app avisa «Tu sesión venció» y vuelve a pedirla. Lo que Rubén estaba mirando no se pierde."
pasos: 3
---

# Renueva una sesión que venció sin perder lo que estabas haciendo

**Actor:** Revisor/área · **Dominio:** Uso embebido

La sesión de la app dura 30 minutos. Dos minutos antes de que venza, la app pide una sesión nueva a tu sistema; si el sistema no responde a tiempo, la app avisa «Tu sesión venció» y vuelve a pedirla. Lo que Rubén estaba mirando no se pierde.

**Antes de empezar**

- Un grupo con Constructora Norte y Rubén como miembro del área Compras, abierto dentro de la página del sistema integrado.
- El reloj de la prueba se adelanta para no esperar 30 minutos reales.

**Pasos**

1. Deja que la app renueve la sesión antes de vencer
2. Avisa que la sesión venció
3. Vuelve a la pantalla con la sesión nueva

## Paso 1. Deja que la app renueve la sesión antes de vencer

La sesión dura 30 minutos. Dos minutos antes de que venza, la app pide una sesión nueva a tu sistema, y el sistema la entrega sin que Rubén haga nada. Por eso la pantalla sigue igual: el cambio es invisible mientras el sistema responda.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 1: Deja que la app renueve la sesión antes de vencer (escritorio)](../../../salida/manual/img/embebido/sesion-vencida/01-deja-que-la-app-renueve-la-sesion-antes-de-vence-claro.webp)

## Paso 2. Avisa que la sesión venció

Si la sesión vence y el sistema aún no entrega una nueva, la app muestra «Tu sesión venció». La explicación indica que ya se pidió una sesión nueva al sistema y que, si no llega, hay que volver a abrir Validasoft desde allí. No se pierde lo que se guardó: al entregar una sesión nueva, la app continúa donde estaba.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 2: Avisa que la sesión venció (escritorio)](../../../salida/manual/img/embebido/sesion-vencida/02-avisa-que-la-sesion-vencio-claro.webp)

**Variantes**

- **El sistema tarda más de 10 segundos en entregar la sesión:** La app sigue en espera y pide volver a abrirla desde el sistema.
- **El sistema rechaza la sesión:** La app muestra «No pudimos validar tu sesión» en lugar de renovarla.

## Paso 3. Vuelve a la pantalla con la sesión nueva

Cuando el sistema entrega la sesión nueva, el aviso desaparece y la app sigue en la misma pantalla. Rubén no tiene que ingresar de nuevo ni volver a abrir el expediente.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 3: Vuelve a la pantalla con la sesión nueva (escritorio)](../../../salida/manual/img/embebido/sesion-vencida/03-vuelve-a-la-pantalla-con-la-sesion-nueva-claro.webp)

## Recorridos relacionados

- [Abre la app de tu empresa dentro de tu sistema](app-de-la-empresa-en-un-iframe.md)
- [Entra a una persona de tu sistema con identidad delegada](../integracion/identidad-delegada.md)
