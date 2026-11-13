---
id: "operacion/lote-pausar-reanudar-cancelar"
titulo: "Pausa, reanuda y cancela un lote, y reintenta sus filas fallidas"
actor: "Administrador de empresa"
dominio: "operacion"
resumen: "La administradora sigue un lote grande mientras se procesa, lo pausa, lo reanuda y lo cancela antes de que termine. Con otro lote que quedó con filas fallidas por el límite del plan, ve qué falló, amplía el plan y reintenta solo las fallidas."
pasos: 9
---

# Pausa, reanuda y cancela un lote, y reintenta sus filas fallidas

**Actor:** Administrador de empresa · **Dominio:** Operación masiva y continua

La administradora sigue un lote grande mientras se procesa, lo pausa, lo reanuda y lo cancela antes de que termine. Con otro lote que quedó con filas fallidas por el límite del plan, ve qué falló, amplía el plan y reintenta solo las fallidas.

**Antes de empezar**

- Un grupo con Constructora Norte y su administradora Ana.
- Un lote de 3000 invitaciones recién creado, todavía en proceso.
- Un segundo lote de 8 filas cuyo grupo llegó al límite de vínculos del plan a mitad de camino.

**Pasos**

1. Sigue un lote mientras se procesa
2. Pausa el lote
3. Reanuda el lote
4. Cancela el lote antes de que termine
5. Revisa lo que quedó sin invitar
6. Reconoce un lote que terminó con errores
7. Mira por qué fallaron las filas
8. Reintenta solo las filas fallidas
9. Descarga el informe por fila

## Paso 1. Sigue un lote mientras se procesa

Un lote nuevo se procesa solo, en segundo plano, de a tandas de filas. El estado «Procesando» (1) y la barra (2) dicen cuántas filas van de cuántas, y la pantalla se refresca sola mientras el lote está en curso; abajo, los totales por estado: invitadas, fallidas, pendientes, con errores y repetidas. Mientras corre puedes pausarlo o cancelarlo.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 1: Sigue un lote mientras se procesa (escritorio)](../../../salida/manual/img/operacion/lote-pausar-reanudar-cancelar/01-sigue-un-lote-mientras-se-procesa-claro.webp)

**Configuración que lo afecta**

- **Permiso de vínculos (escribir)** (`Configuración › Usuarios y roles (/usuarios)`): Sin él se ve el avance del lote, pero no los botones de pausar, reanudar, cancelar ni reintentar.

## Paso 2. Pausa el lote

Con «Pausar», el lote deja de tomar filas nuevas: la fila que se estaba procesando termina y las demás quedan «Pendientes». El estado pasa a «Pausado» (1) y en lugar de «Pausar» aparece «Reanudar» (2). Úsalo para detener un envío masivo mientras revisas algo, sin perder lo ya invitado.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 2: Pausa el lote (escritorio)](../../../salida/manual/img/operacion/lote-pausar-reanudar-cancelar/02-pausa-el-lote-claro.webp)

## Paso 3. Reanuda el lote

«Reanudar» continúa con las filas pendientes desde donde quedó, sin repetir las que ya se invitaron. El estado vuelve a «Procesando» (1) y el avance sigue subiendo.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 3: Reanuda el lote (escritorio)](../../../salida/manual/img/operacion/lote-pausar-reanudar-cancelar/03-reanuda-el-lote-claro.webp)

## Paso 4. Cancela el lote antes de que termine

Cancelar pide confirmación y avisa qué pasa: las filas que aún no se procesaron no recibirán invitación y lo ya invitado se mantiene. Un lote cancelado no se puede reanudar; si quedaron terceros por invitar, se crea otro lote con ellos.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 4: Cancela el lote antes de que termine (escritorio)](../../../salida/manual/img/operacion/lote-pausar-reanudar-cancelar/04-cancela-el-lote-antes-de-que-termine-claro.webp)

## Paso 5. Revisa lo que quedó sin invitar

El lote queda «Cancelado» (1) y las filas que no se alcanzaron a procesar figuran como «Cancelada» (2) en la tabla y en el informe. Los botones de pausar, reanudar y cancelar desaparecen: el estado es definitivo. El filtro «Estado» (3) separa las filas por su resultado; con «Pendiente» se ven justo las que no se alcanzaron a procesar.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 5: Revisa lo que quedó sin invitar (escritorio)](../../../salida/manual/img/operacion/lote-pausar-reanudar-cancelar/05-revisa-lo-que-quedo-sin-invitar-claro.webp)

## Paso 6. Reconoce un lote que terminó con errores

La lista de lotes muestra cada uno con su estado, su avance y su resultado. El lote de mantenimiento terminó «Con errores» (1): se invitaron 3 filas y 5 fallaron. Una fila falla cuando la regla de negocio no la deja pasar al procesarla; aquí, porque el grupo llegó al límite de vínculos de su plan. Abre el lote para ver cuáles y por qué.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes`

![Paso 6: Reconoce un lote que terminó con errores (escritorio)](../../../salida/manual/img/operacion/lote-pausar-reanudar-cancelar/06-reconoce-un-lote-que-termino-con-errores-claro.webp)

**Variantes**

- **El lote terminó sin fallas:** Su estado es «Completado» y no aparece el botón de reintentar.

## Paso 7. Mira por qué fallaron las filas

Filtrando por «Fallida» (1), la columna «Detalle» (2) dice el motivo de cada fila: aquí, que el plan permite cierto número de vínculos activos y el grupo ya los tiene, y que hay que pedir a Plataforma ampliar el plan. Mientras el lote tenga filas fallidas aparece «Reintentar 5 fallidas» (3), que vuelve a procesar solo esas filas.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 7: Mira por qué fallaron las filas (escritorio)](../../../salida/manual/img/operacion/lote-pausar-reanudar-cancelar/07-mira-por-que-fallaron-las-filas-claro.webp)

**Variantes**

- **El vínculo del tercero está bloqueado o terminado:** La fila falla con el motivo del conflicto: no se puede invitar a ese tercero hasta resolverlo.
- **Ocurre un error inesperado al procesar la fila:** Queda fallida con «No se pudo procesar la fila; reinténtala.» y se puede reintentar.

**Configuración que lo afecta**

- **Plan del grupo (límite de vínculos)** (`Configuración › Plan del grupo (/plan-grupo)`): Lo fija Plataforma; con el bloqueo activo, las filas que pasan del límite fallan hasta que se amplíe.

## Paso 8. Reintenta solo las filas fallidas

Después de que Plataforma amplió el plan, «Reintentar fallidas» devolvió las 5 filas a «Pendiente» y las procesó otra vez; las 3 que ya estaban invitadas no se tocaron. El lote pasó a «Completado» (1) con las 8 filas procesadas (2) y el botón de reintentar desapareció.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 8: Reintenta solo las filas fallidas (escritorio)](../../../salida/manual/img/operacion/lote-pausar-reanudar-cancelar/08-reintenta-solo-las-filas-fallidas-claro.webp)

## Paso 9. Descarga el informe por fila

«Descargar informe» (1) baja un Excel con una línea por fila: identificación, razón social, correo, tipo de vínculo, estado, acción (invitado, reenviado o sin cambios), detalle del error, fila de la que es repetida, el vínculo creado o reutilizado y cuándo se procesó. Sirve para conciliar el lote con tu archivo original.

> **Pantalla:** `/e/{empresa}/invitaciones/lotes/{id}`

![Paso 9: Descarga el informe por fila (escritorio)](../../../salida/manual/img/operacion/lote-pausar-reanudar-cancelar/09-descarga-el-informe-por-fila-claro.webp)

## Recorridos relacionados

- [Invita a varios terceros con un archivo](invitacion-por-lotes.md)
- [Revisa el plan del grupo y su uso del mes](../configuracion/plan-del-grupo.md)
- [Revisa la lista de vínculos y filtra por estado y tipo](../vinculos/lista-y-filtros.md)
