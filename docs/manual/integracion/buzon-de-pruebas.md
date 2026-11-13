---
id: "integracion/buzon-de-pruebas"
titulo: "Lee y vacía el buzón de correo de pruebas"
actor: "Sistema integrador"
dominio: "integracion"
resumen: "En el sandbox, los correos que Validasoft enviaría no salen a nadie: quedan en un buzón. El sistema integrador lista ese buzón, abre un correo para ver su contenido y lo vacía cuando termina sus pruebas."
pasos: 3
---

# Lee y vacía el buzón de correo de pruebas

**Actor:** Sistema integrador · **Dominio:** Integración

En el sandbox, los correos que Validasoft enviaría no salen a nadie: quedan en un buzón. El sistema integrador lista ese buzón, abre un correo para ver su contenido y lo vacía cuando termina sus pruebas.

**Antes de empezar**

- Un grupo con Constructora Norte y su flujo de proveedor publicado, con el sandbox creado por una llave de prueba.

**Pasos**

1. Lista los correos del buzón
2. Abre un correo para ver su contenido
3. Vacía el buzón cuando termines

## Paso 1. Lista los correos del buzón

El buzón muestra cada correo que el sandbox retuvo, con su id, su asunto y su destinatario. Son los mismos correos que recibiría una persona real en producción, pero aquí nadie los recibe. Úsalo para comprobar que una invitación o un aviso se envían con el asunto y al destinatario correctos.

![Paso 1: Lista los correos del buzón (escritorio)](../../../salida/manual/img/integracion/buzon-de-pruebas/01-lista-los-correos-del-buzon-claro.webp)

## Paso 2. Abre un correo para ver su contenido

Al abrir un correo ves su contenido completo, con los enlaces que traería. Puedes seguir el enlace de la invitación para probar cómo lo vería el proveedor, sin enviarle nada. El buzón conserva el correo hasta que lo vacíes o reinicies el sandbox.

![Paso 2: Abre un correo para ver su contenido (escritorio)](../../../salida/manual/img/integracion/buzon-de-pruebas/02-abre-un-correo-para-ver-su-contenido-claro.webp)

## Paso 3. Vacía el buzón cuando termines

Vaciar el buzón borra los correos retenidos del sandbox. No afecta a producción ni a los vínculos: solo quita los correos. Si vuelves a listar el buzón, la respuesta llega vacía.

![Paso 3: Vacía el buzón cuando termines (escritorio)](../../../salida/manual/img/integracion/buzon-de-pruebas/03-vacia-el-buzon-cuando-termines-claro.webp)

## Recorridos relacionados

- [Prueba la integración en el sandbox antes de producción](sandbox.md)
