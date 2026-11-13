---
id: "integracion/webhook-entrega-fallida-y-reenvio"
titulo: "Revisa una entrega fallida y la reenvía cuando el sistema vuelve"
actor: "Administrador de empresa"
dominio: "integracion"
resumen: "Tu sistema responde con error y Validasoft deja la entrega en reintento, con su próximo intento. Cuando el sistema se recupera, la administradora reenvía la entrega desde la pantalla y llega firmada otra vez."
pasos: 4
---

# Revisa una entrega fallida y la reenvía cuando el sistema vuelve

**Actor:** Administrador de empresa · **Dominio:** Integración

Tu sistema responde con error y Validasoft deja la entrega en reintento, con su próximo intento. Cuando el sistema se recupera, la administradora reenvía la entrega desde la pantalla y llega firmada otra vez.

**Antes de empezar**

- Un grupo con dos empresas y un webhook de la empresa Sur que pide el evento de vínculo creado.
- Un sistema integrado de prueba que responde 500 hasta que se lo indiquemos.

**Pasos**

1. Encuentra la entrega que no llegó
2. Mira qué respondió tu sistema
3. Reenvía la entrega cuando tu sistema se recupere
4. Confirma que la entrega quedó entregada

## Paso 1. Encuentra la entrega que no llegó

Tu sistema respondió con un error, así que la entrega queda en «Reintentando»: Validasoft cuenta el intento y muestra la hora del próximo. Si tu sistema sigue fallando, Validasoft sigue reintentando. Si fallan 20 entregas seguidas, el webhook se desactiva por fallos y los eventos de ese tiempo no se reenvían solos.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 1: Encuentra la entrega que no llegó (escritorio)](../../../salida/manual/img/integracion/webhook-entrega-fallida-y-reenvio/01-encuentra-la-entrega-que-no-llego-claro.webp)

**Variantes**

- **La entrega dice «Reintentando»:** Validasoft lo volverá a intentar en la hora que muestra.
- **La entrega dice «Fallida»:** Ya no se reintenta sola: solo puedes reenviarla a mano.

## Paso 2. Mira qué respondió tu sistema

Cada intento muestra su resultado (fallido aquí), la hora, el detalle de la respuesta y el tiempo que tardó. Con eso ves si el problema es de tu sistema (un 500 o un tiempo de espera de más de 5 segundos) o de la dirección registrada.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 2: Mira qué respondió tu sistema (escritorio)](../../../salida/manual/img/integracion/webhook-entrega-fallida-y-reenvio/02-mira-que-respondio-tu-sistema-claro.webp)

## Paso 3. Reenvía la entrega cuando tu sistema se recupere

Cuando tu sistema vuelve a responder, pulsa «Reenviar» en la entrega. Validasoft la intenta en ese momento y el aviso confirma que el reenvío está en curso.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 3: Reenvía la entrega cuando tu sistema se recupere (escritorio)](../../../salida/manual/img/integracion/webhook-entrega-fallida-y-reenvio/03-reenvia-la-entrega-cuando-tu-sistema-se-recupere-claro.webp)

## Paso 4. Confirma que la entrega quedó entregada

La fila pasa a «Entregada» y el intento exitoso queda marcado como manual, porque lo disparaste tú. La entrega llega firmada con el secreto vigente del webhook, así que tu sistema puede verificarla igual que las anteriores.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 4: Confirma que la entrega quedó entregada (escritorio)](../../../salida/manual/img/integracion/webhook-entrega-fallida-y-reenvio/04-confirma-que-la-entrega-quedo-entregada-claro.webp)

## Recorridos relacionados

- [Crea un webhook, guarda su secreto y revisa las entregas firmadas](webhooks.md)
