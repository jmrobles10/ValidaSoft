---
id: "integracion/eventos-pull-y-en-vivo"
titulo: "Lee los eventos por pull o en vivo con un cursor"
actor: "Sistema integrador"
dominio: "integracion"
resumen: "El sistema integrado pide los eventos de un tipo desde el último cursor que conoce, y guarda el nuevo cursor para la siguiente vez. Si prefiere tiempo real, abre el flujo de eventos (SSE) y se reanuda con Last-Event-ID si se corta."
pasos: 3
---

# Lee los eventos por pull o en vivo con un cursor

**Actor:** Sistema integrador · **Dominio:** Integración

El sistema integrado pide los eventos de un tipo desde el último cursor que conoce, y guarda el nuevo cursor para la siguiente vez. Si prefiere tiempo real, abre el flujo de eventos (SSE) y se reanuda con Last-Event-ID si se corta.

**Antes de empezar**

- Un grupo con Constructora Norte, su flujo de proveedor publicado y la llave del sistema integrado con permiso para leer eventos.
- Un vínculo creado desde la invitación, que genera el evento «vinculo.creado».

**Pasos**

1. Pide los eventos con un pull
2. Continúa desde el último cursor
3. Escucha los eventos en vivo

## Paso 1. Pide los eventos con un pull

Tu sistema pide GET /api/v1/eventos indicando desde qué cursor quiere leer (desde), qué tipos le interesan (tipos) y cuántos como máximo (limite). La respuesta trae los eventos en orden, cada uno con su cursor, y el cursor de la última página: guárdalo y pídelo desde ahí la próxima vez. Así no pierdes ni repites eventos aunque tu sistema se detenga un rato.

![Paso 1: Pide los eventos con un pull (escritorio)](../../../salida/manual/img/integracion/eventos-pull-y-en-vivo/01-pide-los-eventos-con-un-pull-claro.webp)

**Variantes**

- **Hay más eventos que el límite:** La respuesta marca «hayMas» en verdadero: vuelve a pedir desde el último cursor hasta que sea falso.
- **No pides tipos:** Recibes todos los tipos de evento que tu llave puede leer.

## Paso 2. Continúa desde el último cursor

Cuando ya leíste el último evento, pide de nuevo con el cursor que te devolvió la respuesta anterior. Si no hay nada nuevo de ese tipo, la lista llega vacía y el cursor no cambia. Es la forma de procesar los eventos una sola vez, en orden, sin volver a leer lo que ya tienes.

![Paso 2: Continúa desde el último cursor (escritorio)](../../../salida/manual/img/integracion/eventos-pull-y-en-vivo/02-continua-desde-el-ultimo-cursor-claro.webp)

## Paso 3. Escucha los eventos en vivo

El flujo de eventos (Server-Sent Events) envía cada evento en cuanto ocurre, sin que tu sistema pregunte. En cada evento, id es el cursor, event es el tipo y data trae el mismo sobre que devuelve el pull. Si la conexión se corta, vuelve a conectarte con el último id en la cabecera Last-Event-ID o con desde, y no pierdes eventos.

![Paso 3: Escucha los eventos en vivo (escritorio)](../../../salida/manual/img/integracion/eventos-pull-y-en-vivo/03-escucha-los-eventos-en-vivo-claro.webp)

**Variantes**

- **Tu sistema no puede mantener una conexión abierta:** Usa el pull del paso anterior: la información es la misma.

## Recorridos relacionados

- [Crea un webhook, guarda su secreto y revisa las entregas firmadas](webhooks.md)
- [Crea una API key con sus permisos y empresas, rótala y revócala](api-keys.md)
