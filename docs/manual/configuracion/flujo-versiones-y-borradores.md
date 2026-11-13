---
id: "configuracion/flujo-versiones-y-borradores"
titulo: "Cambia un flujo publicado con un borrador nuevo"
actor: "Administrador de empresa"
dominio: "configuracion"
resumen: "La administradora abre un flujo ya publicado, crea un borrador con cambios, lo guarda, descarta otros cambios sin guardar y publica la versión 2. La versión 1 queda retirada en el historial y se puede consultar en solo lectura."
pasos: 7
---

# Cambia un flujo publicado con un borrador nuevo

**Actor:** Administrador de empresa · **Dominio:** Configuración

La administradora abre un flujo ya publicado, crea un borrador con cambios, lo guarda, descarta otros cambios sin guardar y publica la versión 2. La versión 1 queda retirada en el historial y se puede consultar en solo lectura.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Sur S.A.S.» y un flujo de proveedores ya publicado (versión 1).
- Una persona con el rol Administrador que entra a Validasoft desde el sistema integrado.

**Pasos**

1. Abre un flujo publicado
2. Edita el flujo
3. Guarda el borrador v2
4. Descarta lo que no quieres guardar
5. Publica la versión 2
6. Revisa el historial de versiones
7. Consulta una versión anterior

## Paso 1. Abre un flujo publicado

Al abrir un flujo publicado ves sus datos y sus etapas, pero no se pueden cambiar. El aviso (1) dice que, al guardar, se crea el borrador v2 y que la versión publicada sigue vigente hasta que publiques; el botón «Editar» (2) empieza ese borrador. La versión publicada es la que usan los expedientes nuevos; los que ya están abiertos siguen con la versión con que empezaron.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 1: Abre un flujo publicado (escritorio)](../../../salida/manual/img/configuracion/flujo-versiones-y-borradores/01-abre-un-flujo-publicado-claro.webp)

**Configuración que lo afecta**

- **Versiones del flujo** (`Configuración › Flujos aprobatorios (/flujos)`): La versión publicada define el flujo de los expedientes nuevos.

## Paso 2. Edita el flujo

Con «Editar» el formulario se abre con una copia de la versión publicada. Cambias lo que necesites (aquí, el nombre (1)) y aparece «Guardar borrador» (2). Mientras no lo guardes, nada se crea en el servidor: si sales de la pantalla, los cambios se pierden.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 2: Edita el flujo (escritorio)](../../../salida/manual/img/configuracion/flujo-versiones-y-borradores/02-edita-el-flujo-claro.webp)

**Variantes**

- **Faltan datos obligatorios o hay un error:** Al guardar, el editor marca los campos con error, muestra «Revisa los campos marcados.» y no guarda.

## Paso 3. Guarda el borrador v2

Al guardar se crea el borrador v2. Aparece «Publicar v2» (1) y, en «Versiones» (2), la v2 con el estado «Borrador» junto a la v1 «Publicada». Mientras el borrador no se publique, la v1 sigue vigente y los expedientes nuevos siguen usándola.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 3: Guarda el borrador v2 (escritorio)](../../../salida/manual/img/configuracion/flujo-versiones-y-borradores/03-guarda-el-borrador-v2-claro.webp)

## Paso 4. Descarta lo que no quieres guardar

Si cambias algo y te arrepientes, «Descartar cambios» (1) deshace lo escrito desde el último guardado. Está disponible cuando el borrador ya existe y el formulario tiene cambios sin guardar; mientras tanto, «Publicar» desaparece para que no publiques una versión distinta de la guardada.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 4: Descarta lo que no quieres guardar (escritorio)](../../../salida/manual/img/configuracion/flujo-versiones-y-borradores/04-descarta-lo-que-no-quieres-guardar-claro.webp)

## Paso 5. Publica la versión 2

Publicar pide confirmación: los expedientes nuevos usarán esta versión y los que ya están abiertos siguen con la suya. La versión publicada anterior queda retirada.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 5: Publica la versión 2 (escritorio)](../../../salida/manual/img/configuracion/flujo-versiones-y-borradores/05-publica-la-version-2-claro.webp)

## Paso 6. Revisa el historial de versiones

La lista «Versiones» muestra la v2 «Publicada» (1) con su fecha y la v1 «Retirada» (2) con la fecha en que se retiró. El historial no se borra: cada versión se puede abrir para ver cómo era.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 6: Revisa el historial de versiones (escritorio)](../../../salida/manual/img/configuracion/flujo-versiones-y-borradores/06-revisa-el-historial-de-versiones-claro.webp)

**Variantes**

- **La empresa ya tenía expedientes abiertos con la v1:** Siguen con la v1 hasta terminar; solo los expedientes nuevos usan la v2.

## Paso 7. Consulta una versión anterior

Al elegir una versión de la lista, el formulario muestra esa versión en solo lectura y el aviso (1) dice cuál estás viendo. «Volver a la versión vigente» regresa a la actual. Ver una versión no cambia cuál está vigente.

> **Pantalla:** `/e/{empresa}/flujos/{id}`

![Paso 7: Consulta una versión anterior (escritorio)](../../../salida/manual/img/configuracion/flujo-versiones-y-borradores/07-consulta-una-version-anterior-claro.webp)

## Recorridos relacionados

- [Cambia el flujo por defecto de un tipo de vínculo](flujo-por-defecto.md)
- [Arma una etapa con dos áreas en paralelo y una automática](flujo-etapas-y-areas-en-paralelo.md)
