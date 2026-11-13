---
id: "integracion/identidad-delegada"
titulo: "Entra a una persona de tu sistema con identidad delegada"
actor: "Sistema integrador"
dominio: "integracion"
resumen: "Tu sistema firma una aserción con su clave privada que dice quién es la persona, la envía con su llave de API y Validasoft devuelve una sesión de 30 minutos con los permisos de esa persona. Cada aserción vale una sola vez: para la siguiente petición, tu sistema firma otra."
pasos: 4
---

# Entra a una persona de tu sistema con identidad delegada

**Actor:** Sistema integrador · **Dominio:** Integración

Tu sistema firma una aserción con su clave privada que dice quién es la persona, la envía con su llave de API y Validasoft devuelve una sesión de 30 minutos con los permisos de esa persona. Cada aserción vale una sola vez: para la siguiente petición, tu sistema firma otra.

**Antes de empezar**

- Un grupo con Constructora Norte y el sistema integrado E2E registrado con su clave pública.
- Rubén, miembro del área Compras de Norte, como persona que entra desde el sistema.

**Pasos**

1. Firma quién es la persona
2. Canjea la aserción por una sesión
3. Una aserción no sirve dos veces
4. Entra a la app como esa persona

## Paso 1. Firma quién es la persona

Tu sistema firma con su clave privada un JWT con los datos de la persona: «sub» (su identificador en tu sistema), «name» y «email». El campo «iss» es el código de tu sistema y «aud» es validasoft. La aserción vale como máximo 5 minutos, y su «jti» identifica esta firma: Validasoft lo recuerda para que no se pueda reutilizar.

![Paso 1: Firma quién es la persona (escritorio)](../../../salida/manual/img/integracion/identidad-delegada/01-firma-quien-es-la-persona-claro.webp)

**Variantes**

- **La aserción vence en más de 5 minutos:** Validasoft la rechaza con 401 «asercion-vigencia-excesiva»: firma una nueva, con vigencia corta.
- **El sistema está desactivado:** Validasoft responde 401 «sistema-inactivo», aunque la aserción esté bien firmada.

## Paso 2. Canjea la aserción por una sesión

Tu sistema llama a POST /api/v1/sesiones-usuario con su llave de API y la aserción en la cabecera X-Usuario-Delegado. La respuesta trae un token de Validasoft que dura 30 minutos (campo «expira») y tiene el permiso efectivo de esa persona en la empresa. Ese token se envía como Authorization: Bearer; tu pantalla embebida lo usa hasta que vence.

![Paso 2: Canjea la aserción por una sesión (escritorio)](../../../salida/manual/img/integracion/identidad-delegada/02-canjea-la-asercion-por-una-sesion-claro.webp)

**Variantes**

- **La persona está desactivada en Validasoft:** El canje responde 401: un usuario desactivado no actúa por delegación, aunque su sistema lo autentique.

## Paso 3. Una aserción no sirve dos veces

Validasoft guarda el «jti» de cada aserción aceptada hasta que vence. Si llega la misma aserción otra vez, responde 401 «asercion-reutilizada». Por eso tu sistema firma una aserción nueva para cada sesión o petición, aunque la persona sea la misma.

![Paso 3: Una aserción no sirve dos veces (escritorio)](../../../salida/manual/img/integracion/identidad-delegada/03-una-asercion-no-sirve-dos-veces-claro.webp)

## Paso 4. Entra a la app como esa persona

La pantalla embebida pide sesión al sistema y el sistema firma una aserción nueva para Rubén y la canjea. Dentro de la app, Rubén ve lo que su rol le permite: aquí, la bandeja de revisión de Constructora Norte.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 4: Entra a la app como esa persona (escritorio)](../../../salida/manual/img/integracion/identidad-delegada/04-entra-a-la-app-como-esa-persona-claro.webp)

## Recorridos relacionados

- [Registra un sistema integrado con su clave pública](sistema-integrador.md)
- [Crea una API key con sus permisos y empresas, rótala y revócala](api-keys.md)
- [Abre la app de tu empresa dentro de tu sistema](../embebido/app-de-la-empresa-en-un-iframe.md)
