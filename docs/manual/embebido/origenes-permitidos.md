---
id: "embebido/origenes-permitidos"
titulo: "Declara qué páginas pueden mostrar Validasoft"
actor: "Sistema integrador"
dominio: "embebido"
resumen: "Tu sistema declara los orígenes (dirección y puerto) desde los que se muestran sus pantallas de Validasoft. Solo esos orígenes pueden embeber la app y pedir sesiones para ella: un origen que no declaraste recibe un rechazo y su página no muestra nada del tercero."
pasos: 4
---

# Declara qué páginas pueden mostrar Validasoft

**Actor:** Sistema integrador · **Dominio:** Uso embebido

Tu sistema declara los orígenes (dirección y puerto) desde los que se muestran sus pantallas de Validasoft. Solo esos orígenes pueden embeber la app y pedir sesiones para ella: un origen que no declaraste recibe un rechazo y su página no muestra nada del tercero.

**Antes de empezar**

- Un grupo con Constructora Norte, un tercero que aceptó su invitación de cliente y el sistema integrado E2E con la llave de administración.

**Pasos**

1. Declara los orígenes permitidos
2. Revisa los orígenes que acepta la app
3. Un origen ajeno no obtiene sesión
4. Un origen declarado sí obtiene sesión

## Paso 1. Declara los orígenes permitidos

Con PUT /api/v1/sistemas/{id}/origenes reemplazas la lista de orígenes de tu sistema. Cada origen es esquema, dominio y puerto, como «https://portal.tuempresa.co», sin ruta ni comodines: Validasoft rechaza una página o un asterisco. Esa misma lista decide qué páginas pueden embeber la app y qué orígenes pueden pedir sesiones para ella.

![Paso 1: Declara los orígenes permitidos (escritorio)](../../../salida/manual/img/embebido/origenes-permitidos/01-declara-los-origenes-permitidos-claro.webp)

**Variantes**

- **Escribes una ruta o un comodín:** La llamada se rechaza: el origen es el sitio, no una página.
- **Cambias la lista:** Los cambios valen desde ya; la página que embebe la app recibe la lista nueva en la siguiente consulta.

## Paso 2. Revisa los orígenes que acepta la app

La app consulta esta lista en tiempo de ejecución y la usa para dos cosas: acepta mensajes solo de esos orígenes y, en la cabecera de seguridad frame-ancestors, solo esos sitios pueden alojarla en un marco. La consulta se guarda en caché unos 60 segundos, así que un cambio puede tardar ese tiempo en llegar.

![Paso 2: Revisa los orígenes que acepta la app (escritorio)](../../../salida/manual/img/embebido/origenes-permitidos/02-revisa-los-origenes-que-acepta-la-app-claro.webp)

## Paso 3. Un origen ajeno no obtiene sesión

Cuando una página pide una sesión para un origen que no está en la lista, la API responde 403 «origen-no-permitido» y no emite ningún token. Sin token, la página no puede mostrar la bóveda ni la ficha del tercero, aunque cargue el marco de Validasoft.

![Paso 3: Un origen ajeno no obtiene sesión (escritorio)](../../../salida/manual/img/embebido/origenes-permitidos/03-un-origen-ajeno-no-obtiene-sesion-claro.webp)

## Paso 4. Un origen declarado sí obtiene sesión

Si la página está en la lista, la API emite el token para ese origen y para esa pantalla. El token solo sirve en el origen para el que se emitió: otro origen declarado no puede usarlo, aunque sea del mismo sistema.

![Paso 4: Un origen declarado sí obtiene sesión (escritorio)](../../../salida/manual/img/embebido/origenes-permitidos/04-un-origen-declarado-si-obtiene-sesion-claro.webp)

## Recorridos relacionados

- [Registra un sistema integrado con su clave pública](../integracion/sistema-integrador.md)
- [Muestra la bóveda y la ficha del tercero dentro de tu sistema](tercero-embebido.md)
- [Abre la app de tu empresa dentro de tu sistema](app-de-la-empresa-en-un-iframe.md)
