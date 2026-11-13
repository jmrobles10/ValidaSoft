---
id: "integracion/webhooks"
titulo: "Crea un webhook, guarda su secreto y revisa las entregas firmadas"
actor: "Administrador de empresa"
dominio: "integracion"
resumen: "La administradora registra la dirección de su sistema para que Validasoft le envíe cada evento con un POST firmado. Guarda el secreto una sola vez, y luego revisa en la pantalla las entregas, con sus intentos."
pasos: 7
---

# Crea un webhook, guarda su secreto y revisa las entregas firmadas

**Actor:** Administrador de empresa · **Dominio:** Integración

La administradora registra la dirección de su sistema para que Validasoft le envíe cada evento con un POST firmado. Guarda el secreto una sola vez, y luego revisa en la pantalla las entregas, con sus intentos.

**Antes de empezar**

- Un grupo empresarial con dos empresas y una administradora que puede administrar webhooks.
- Una dirección local que recibe las entregas (el «sistema integrado» de prueba, en su propio puerto).

**Pasos**

1. Abre Configuración › Webhooks
2. Escribe la dirección que recibe los eventos
3. Elige qué eventos recibe y de qué empresas
4. Copia el secreto de firma: se muestra una vez
5. Revisa las entregas del webhook
6. Mira los intentos de una entrega
7. Verifica la firma de cada entrega

## Paso 1. Abre Configuración › Webhooks

Un webhook es la dirección de tu sistema a la que Validasoft envía un aviso cada vez que pasa algo: un vínculo activo, un expediente aprobado, un certificado emitido. La tabla lista los webhooks del grupo con su destino, cuántos eventos reciben, su estado y la hora de la última entrega. Para registrar uno, pulsa «Nuevo webhook».

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 1: Abre Configuración › Webhooks (escritorio)](../../../salida/manual/img/integracion/webhooks/01-abre-configuracion-webhooks-claro.webp)

**Configuración que lo afecta**

- **Webhooks** (`Configuración › Webhooks (/webhooks)`): Cada webhook recibe solo los eventos y las empresas que marques.

## Paso 2. Escribe la dirección que recibe los eventos

La dirección debe ser https y responder con 2xx en menos de 5 segundos; si tu sistema tarda, procesa después y responde rápido. La descripción es opcional y sirve para reconocerlo en la lista, por ejemplo «ERP producción». En desarrollo se admite http solo hacia localhost.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 2: Escribe la dirección que recibe los eventos (escritorio)](../../../salida/manual/img/integracion/webhooks/02-escribe-la-direccion-que-recibe-los-eventos-claro.webp)

**Variantes**

- **La dirección es http hacia otro sitio:** Se rechaza: usa https.
- **Dejas la descripción vacía:** El webhook se identifica por su dirección.

## Paso 3. Elige qué eventos recibe y de qué empresas

Marca los eventos que quieres recibir, agrupados por vínculos, expedientes, documentos, certificados, información del tercero e invitaciones. Si marcas «Todos los eventos», también recibirá los que se agreguen después. Luego elige si el webhook sigue a todas las empresas del grupo o solo a algunas; un webhook sin evento ni empresa no se puede guardar.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 3: Elige qué eventos recibe y de qué empresas (escritorio)](../../../salida/manual/img/integracion/webhooks/03-elige-que-eventos-recibe-y-de-que-empresas-claro.webp)

**Variantes**

- **Marcas «Todos los eventos»:** Recibirá también los eventos nuevos que Validasoft agregue más adelante.
- **Eliges solo algunos eventos:** Los demás no se envían; un evento no marcado no llega nunca a esa dirección.

## Paso 4. Copia el secreto de firma: se muestra una vez

Al crear el webhook aparece su secreto de firma. Cópialo con «Copiar secreto» y guárdalo en el gestor de secretos de tu sistema: es la única vez que se muestra. Con él, tu sistema comprueba que cada entrega viene de Validasoft. Si lo pierdes, rota el secreto desde el webhook.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 4: Copia el secreto de firma: se muestra una vez (escritorio)](../../../salida/manual/img/integracion/webhooks/04-copia-el-secreto-de-firma-se-muestra-una-vez-claro.webp)

## Paso 5. Revisa las entregas del webhook

La sección «Entregas recientes» lista cada evento enviado, con su estado (pendiente, entregada, reintentando, fallida o cancelada), el número de intentos y la fecha. Puedes filtrarlas por estado. Una entrega que no tuvo éxito muestra cuándo será el próximo intento, y desde ahí puedes reenviarla a mano.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 5: Revisa las entregas del webhook (escritorio)](../../../salida/manual/img/integracion/webhooks/05-revisa-las-entregas-del-webhook-claro.webp)

## Paso 6. Mira los intentos de una entrega

Al abrir una entrega ves cada intento: su resultado (exitoso o fallido), la hora, el detalle y el tiempo de respuesta de tu sistema. Si un intento fue reenviado a mano, lo marca como manual.

> **Pantalla:** `/e/{empresa}/webhooks`

![Paso 6: Mira los intentos de una entrega (escritorio)](../../../salida/manual/img/integracion/webhooks/06-mira-los-intentos-de-una-entrega-claro.webp)

## Paso 7. Verifica la firma de cada entrega

Cada entrega trae la cabecera Validasoft-Signature con una hora (t) y una firma (v1): el HMAC-SHA256 de la hora y el cuerpo con tu secreto. Recalcúlala en tu sistema y rechaza las firmas que no coincidan o que tengan más de 5 minutos. Tu sistema responde 2xx y procesa el evento después.

> **Pantalla:** `/`

![Paso 7: Verifica la firma de cada entrega (escritorio)](../../../salida/manual/img/integracion/webhooks/07-verifica-la-firma-de-cada-entrega-claro.webp)

## Recorridos relacionados

- [Revisa una entrega fallida y la reenvía cuando el sistema vuelve](webhook-entrega-fallida-y-reenvio.md)
- [Lee los eventos por pull o en vivo con un cursor](eventos-pull-y-en-vivo.md)
