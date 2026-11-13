---
id: "informes/plazos-de-revision"
titulo: "Mide el cumplimiento de los plazos de revisión"
actor: "Administrador de empresa"
dominio: "informes"
resumen: "La administradora de Constructora Norte configura un plazo de 10 horas para Compras y luego abre el informe de plazo de revisión: ve cuántas revisiones se resolvieron a tiempo, cuántas quedaron fuera de plazo y cuál es el cumplimiento de cada área."
pasos: 2
---

# Mide el cumplimiento de los plazos de revisión

**Actor:** Administrador de empresa · **Dominio:** Informes, auditoría y certificados

La administradora de Constructora Norte configura un plazo de 10 horas para Compras y luego abre el informe de plazo de revisión: ve cuántas revisiones se resolvieron a tiempo, cuántas quedaron fuera de plazo y cuál es el cumplimiento de cada área.

**Antes de empezar**

- Un grupo con Constructora Norte, con las áreas Compras y Cumplimiento, y un flujo de proveedor con RUT y Cámara de comercio.
- Un proveedor cuyo expediente llegó a revisión de Compras: el plazo de 10 horas empieza a correr desde ese momento.

**Pasos**

1. Abre el informe de plazo de revisión
2. Compara el cumplimiento de cada área

## Paso 1. Abre el informe de plazo de revisión

El resumen cuenta las revisiones que tenían plazo: «A tiempo» las que se decidieron dentro del plazo, «Fuera de plazo» las decididas después, y «Vencidos ahora» las que siguen esperando al área pasado su plazo. Con el porcentaje de cumplimiento. Solo cuentan las áreas que tienen un plazo configurado.

> **Pantalla:** `/e/{empresa}/informes/sla`

![Paso 1: Abre el informe de plazo de revisión (escritorio)](../../../salida/manual/img/informes/plazos-de-revision/01-abre-el-informe-de-plazo-de-revision-claro.webp)

**Configuración que lo afecta**

- **Plazo de revisión (horas) por área** (`Configuración › Plazos de revisión (/plazos-de-revision)`): Las horas que tiene cada área para decidir. Al acercarse el plazo se avisa por correo; al vencer, la bandeja marca la revisión como vencida.

## Paso 2. Compara el cumplimiento de cada área

La tabla «Cumplimiento por área» muestra el plazo de cada área en horas, cuántas revisiones tuvo, cuántas fueron a tiempo y cuántas fuera de plazo, con su cumplimiento, el promedio de días y los vencidos. Un área sin plazo aparece como «Sin plazo» y no entra en el cálculo.

> **Pantalla:** `/e/{empresa}/informes/sla`

![Paso 2: Compara el cumplimiento de cada área (escritorio)](../../../salida/manual/img/informes/plazos-de-revision/02-compara-el-cumplimiento-de-cada-area-claro.webp)

## Recorridos relacionados

- [Crea un área, asígnale personas y desactívala](../configuracion/areas-y-miembros.md)
- [Filtra la bandeja de revisión y abre un expediente](../revision/bandeja-de-revision.md)
- [Consulta el estado de los expedientes y sus tiempos](estado-de-expedientes.md)
