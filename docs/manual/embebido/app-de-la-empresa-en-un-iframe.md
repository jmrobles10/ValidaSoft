---
id: "embebido/app-de-la-empresa-en-un-iframe"
titulo: "Abre la app de tu empresa dentro de tu sistema"
actor: "Revisor/área"
dominio: "embebido"
resumen: "Rubén usa Validasoft sin ver una pantalla de ingreso: abre la página de su sistema, que muestra la app en un marco y le entrega la sesión por dentro. La app se abre en Constructora Norte y, al cerrar la sesión desde el sistema, la app lo avisa y deja de mostrar datos."
pasos: 4
---

# Abre la app de tu empresa dentro de tu sistema

**Actor:** Revisor/área · **Dominio:** Uso embebido

Rubén usa Validasoft sin ver una pantalla de ingreso: abre la página de su sistema, que muestra la app en un marco y le entrega la sesión por dentro. La app se abre en Constructora Norte y, al cerrar la sesión desde el sistema, la app lo avisa y deja de mostrar datos.

**Antes de empezar**

- Un grupo con Constructora Norte, su flujo de proveedor publicado y Rubén como miembro del área Compras.
- La página del sistema integrado (E2E) registrada en Validasoft como origen permitido.

**Pasos**

1. Abre Validasoft desde tu sistema
2. Revisa la empresa con la que trabajas
3. Cambia de empresa con el selector
4. Cierra la sesión desde tu sistema

## Paso 1. Abre Validasoft desde tu sistema

Tu sistema muestra Validasoft en un marco (iframe) dentro de sus propias pantallas. Rubén no ingresa con usuario y contraseña: cuando la app necesita sesión, el sistema le pide a su backend una sesión para Rubén y se la entrega a la app. La app aparece sin barra de ingreso y sin el marco de Validasoft, con la navegación propia de la empresa.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 1: Abre Validasoft desde tu sistema (escritorio)](../../../salida/manual/img/embebido/app-de-la-empresa-en-un-iframe/01-abre-validasoft-desde-tu-sistema-claro.webp)

**Variantes**

- **El sistema no entrega sesión:** La app se queda en «Conectando con tu sistema…» y, si tarda más de 10 segundos, pide volver a abrirla desde el sistema.

## Paso 2. Revisa la empresa con la que trabajas

La sesión lleva la empresa elegida por el sistema, así que la app abre directamente en Constructora Norte S.A.S. y su NIT aparece en la cabecera. Rubén solo ve los expedientes de las áreas de las que es miembro.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 2: Revisa la empresa con la que trabajas (escritorio)](../../../salida/manual/img/embebido/app-de-la-empresa-en-un-iframe/02-revisa-la-empresa-con-la-que-trabajas-claro.webp)

## Paso 3. Cambia de empresa con el selector

Quien tiene roles en varias empresas del grupo, como la administradora, ve en la cabecera un selector con el nombre y el NIT de la empresa activa (1). Al abrirlo (2) elige otra empresa y la app cambia de empresa sin salir del marco ni volver a pedir sesión. Al cambiar, se conserva la sección en la que estás (Inicio, Vínculos…), no el recurso abierto: un expediente de una empresa no existe en la otra.

> **Pantalla:** `/e/{empresa}/inicio`

![Paso 3: Cambia de empresa con el selector (escritorio)](../../../salida/manual/img/embebido/app-de-la-empresa-en-un-iframe/03-cambia-de-empresa-con-el-selector-claro.webp)

**Variantes**

- **Solo tienes rol en una empresa (como Rubén):** No hay selector: la cabecera muestra el nombre y el NIT de esa empresa como un dato fijo.
- **El grupo tiene muchas empresas:** El menú trae un cuadro «Buscar empresa» para encontrarla escribiendo.

## Paso 4. Cierra la sesión desde tu sistema

Cuando el sistema cierra la sesión (por ejemplo, al salir de su propia cuenta), envía el aviso a la app y esta deja de mostrar datos. Para volver a entrar hay que abrir Validasoft de nuevo desde el sistema.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 4: Cierra la sesión desde tu sistema (escritorio)](../../../salida/manual/img/embebido/app-de-la-empresa-en-un-iframe/04-cierra-la-sesion-desde-tu-sistema-claro.webp)

## Recorridos relacionados

- [Usa la barra de secciones en cualquier ancho y en modo oscuro](navegacion-adaptativa-y-tema.md)
- [Renueva una sesión que venció sin perder lo que estabas haciendo](sesion-vencida.md)
- [Entra a una persona de tu sistema con identidad delegada](../integracion/identidad-delegada.md)
