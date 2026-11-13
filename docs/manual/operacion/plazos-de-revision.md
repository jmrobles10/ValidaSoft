---
id: "operacion/plazos-de-revision"
titulo: "Fija el plazo de revisión de cada área"
actor: "Administrador de empresa"
dominio: "operacion"
resumen: "La administradora fija cuántas horas tiene cada área para revisar un expediente. Cuando el plazo se acerca o vence, el responsable recibe un aviso; la bandeja lo muestra y el informe de plazos mide el cumplimiento."
pasos: 4
---

# Fija el plazo de revisión de cada área

**Actor:** Administrador de empresa · **Dominio:** Operación masiva y continua

La administradora fija cuántas horas tiene cada área para revisar un expediente. Cuando el plazo se acerca o vence, el responsable recibe un aviso; la bandeja lo muestra y el informe de plazos mide el cumplimiento.

**Antes de empezar**

- Un grupo con Constructora Norte, con las áreas Compras y Cumplimiento, y un proveedor que ya entregó sus documentos y quedó en revisión.

**Pasos**

1. Abre los plazos de revisión
2. Guarda el plazo del área
3. Mira el plazo en la bandeja
4. Revisa el informe de cumplimiento de plazos

## Paso 1. Abre los plazos de revisión

En Configuración, «Plazos de revisión» define en horas el tiempo que tiene cada área para decidir un expediente que le llega. Cada área tiene su propio campo; el plazo corre desde que el expediente entra a revisión.

> **Pantalla:** `/e/{empresa}/plazos-de-revision`

![Paso 1: Abre los plazos de revisión (escritorio)](../../../salida/manual/img/operacion/plazos-de-revision/01-abre-los-plazos-de-revision-claro.webp)

**Configuración que lo afecta**

- **Plazo de revisión por área** (`Configuración › Plazos de revisión (/plazos-de-revision)`): Cuando se acerca o vence, el responsable recibe un aviso y la bandeja marca el plazo de la fila.

## Paso 2. Guarda el plazo del área

Con 10 horas en Compras, cada expediente que llegue a esa área tiene 10 horas para decidirse. Al guardar, Validasoft confirma el cambio y aplica el plazo a los expedientes que entren de ahí en adelante.

> **Pantalla:** `/e/{empresa}/plazos-de-revision`

![Paso 2: Guarda el plazo del área (escritorio)](../../../salida/manual/img/operacion/plazos-de-revision/02-guarda-el-plazo-del-area-claro.webp)

**Variantes**

- **Dejas vacío el plazo de un área:** Esa área no tiene plazo y sus expedientes no generan avisos de vencimiento.

## Paso 3. Mira el plazo en la bandeja

La bandeja tiene la columna «Plazo de revisión» con el estado de cada expediente: «A tiempo» mientras le queda plazo al área, y el indicador cambia cuando se acerca o vence. Quien revisa Compras ve el plazo de los expedientes de su área.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 3: Mira el plazo en la bandeja (escritorio)](../../../salida/manual/img/operacion/plazos-de-revision/03-mira-el-plazo-en-la-bandeja-claro.webp)

**Variantes**

- **El plazo ya venció:** La fila deja de decir «A tiempo» y el expediente aparece como vencido en el informe de plazos.

## Paso 4. Revisa el informe de cumplimiento de plazos

El informe de plazos de revisión agrupa por área cuántos expedientes se decidieron a tiempo y cuántos vencieron. Ana recibe el correo «Plazo de revisión vencido» cuando un área incumple; Rubén recibió antes el aviso de «Plazo de revisión por vencer».

> **Pantalla:** `/e/{empresa}/informes/sla`

![Paso 4: Revisa el informe de cumplimiento de plazos (escritorio)](../../../salida/manual/img/operacion/plazos-de-revision/04-revisa-el-informe-de-cumplimiento-de-plazos-claro.webp)

## Recorridos relacionados

- [Delega tu revisión mientras estás ausente](delegacion-de-revision.md)
- [Aprueba el área de Compras](../revision/aprobar-un-area.md)
