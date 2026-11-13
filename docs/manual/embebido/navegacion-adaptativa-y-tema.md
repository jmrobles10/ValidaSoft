---
id: "embebido/navegacion-adaptativa-y-tema"
titulo: "Usa la barra de secciones en cualquier ancho y en modo oscuro"
actor: "Revisor/área"
dominio: "embebido"
resumen: "La barra superior de la app se adapta al ancho del sistema que la aloja: cuando no caben todas las secciones, las que sobran van al botón «Más». El sistema también puede pedir el tema oscuro, y la app lo aplica sin que Rubén cambie nada."
pasos: 3
---

# Usa la barra de secciones en cualquier ancho y en modo oscuro

**Actor:** Revisor/área · **Dominio:** Uso embebido

La barra superior de la app se adapta al ancho del sistema que la aloja: cuando no caben todas las secciones, las que sobran van al botón «Más». El sistema también puede pedir el tema oscuro, y la app lo aplica sin que Rubén cambie nada.

**Antes de empezar**

- Un grupo con Constructora Norte y Rubén como miembro del área Compras, abierto dentro de la página del sistema integrado.
- El sistema de prueba puede cambiar el ancho de su página y enviar el tema a la app.

**Pasos**

1. Mira cómo se adapta la barra al ancho
2. Cambia de sección desde el selector del celular
3. Usa el tema oscuro del sistema

## Paso 1. Mira cómo se adapta la barra al ancho

En el ancho de escritorio (la captura principal, 1280 px) las secciones de la empresa caben todas en la barra superior y cada una se abre con un clic. Al achicar el ancho del sistema que aloja la app (capturas de 1100, 900 y 700 px), las últimas secciones pasan al botón «Más» y la barra se queda con las primeras. Se recalcula sola, sin recargar la página.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 1: Mira cómo se adapta la barra al ancho (escritorio)](../../../salida/manual/img/embebido/navegacion-adaptativa-y-tema/01-mira-como-se-adapta-la-barra-al-ancho-claro.webp)

![Paso 1: Mira cómo se adapta la barra al ancho (1100 px de ancho)](../../../salida/manual/img/embebido/navegacion-adaptativa-y-tema/01-mira-como-se-adapta-la-barra-al-ancho-1100.webp)

![Paso 1: Mira cómo se adapta la barra al ancho (900 px de ancho)](../../../salida/manual/img/embebido/navegacion-adaptativa-y-tema/01-mira-como-se-adapta-la-barra-al-ancho-900.webp)

![Paso 1: Mira cómo se adapta la barra al ancho (700 px de ancho)](../../../salida/manual/img/embebido/navegacion-adaptativa-y-tema/01-mira-como-se-adapta-la-barra-al-ancho-700.webp)

**Variantes**

- **Tu rol no tiene acceso a una sección:** Esa sección no aparece en la barra: la barra solo muestra lo que puedes usar.
- **La sección en la que estás quedó dentro de «Más»:** El botón lleva el nombre de esa sección (por ejemplo «Bandeja») en lugar de «Más» y se ve marcado.

## Paso 2. Cambia de sección desde el selector del celular

En un celular la barra no cabe: aparece un solo botón con el nombre de la sección en la que estás, en este caso Bandeja. Al abrirlo, el menú lista todas las secciones a las que tienes acceso para que elijas la que necesitas. La barra se recalcula sola al cambiar el ancho, sin recargar la página.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 2: Cambia de sección desde el selector del celular (escritorio)](../../../salida/manual/img/embebido/navegacion-adaptativa-y-tema/02-cambia-de-seccion-desde-el-selector-del-celular-claro.webp)

![Paso 2: Cambia de sección desde el selector del celular (celular)](../../../salida/manual/img/embebido/navegacion-adaptativa-y-tema/02-cambia-de-seccion-desde-el-selector-del-celular-celular.webp)

## Paso 3. Usa el tema oscuro del sistema

Si tu sistema tiene el tema oscuro activo, envía su preferencia a la app y Validasoft cambia a colores oscuros al momento. La app no guarda esa preferencia por su cuenta: sigue el tema que le indique el sistema que la aloja.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 3: Usa el tema oscuro del sistema (escritorio)](../../../salida/manual/img/embebido/navegacion-adaptativa-y-tema/03-usa-el-tema-oscuro-del-sistema-claro.webp)

**Variantes**

- **El sistema envía tema claro:** La app vuelve al tema claro.

## Recorridos relacionados

- [Abre la app de tu empresa dentro de tu sistema](app-de-la-empresa-en-un-iframe.md)
- [Comprueba qué secciones ve cada rol](../configuracion/permisos-por-rol.md)
