---
id: "informes/estado-de-expedientes"
titulo: "Consulta el estado de los expedientes y sus tiempos"
actor: "Administrador de empresa"
dominio: "informes"
resumen: "La administradora de Constructora Sur abre el informe de estado de expedientes: ve cada expediente con su folio y cuánto tardó cada área en decidir. Filtra por estado y baja el resultado en Excel con los mismos filtros."
pasos: 4
---

# Consulta el estado de los expedientes y sus tiempos

**Actor:** Administrador de empresa · **Dominio:** Informes, auditoría y certificados

La administradora de Constructora Sur abre el informe de estado de expedientes: ve cada expediente con su folio y cuánto tardó cada área en decidir. Filtra por estado y baja el resultado en Excel con los mismos filtros.

**Antes de empezar**

- Un grupo con Constructora Sur y su área Revisión con Sofía como miembro; el certificado de seguridad y salud en el trabajo es requisito del flujo de proveedor.
- Un proveedor cuyo expediente ya fue aprobado por Revisión, para que el informe muestre tiempos reales.

**Pasos**

1. Abre el informe de estado de expedientes
2. Lee cuánto tarda cada área
3. Filtra por estado
4. Descarga el informe en Excel

## Paso 1. Abre el informe de estado de expedientes

Informes muestra un expediente por fila, con su folio, el tercero, el estado y cuántos de sus requisitos están aprobados («2 de 2»). Las pestañas de arriba llevan a los terceros y vínculos y al plazo de revisión. Haz clic en una fila para llegar al expediente.

> **Pantalla:** `/e/{empresa}/informes/estado-expedientes`

![Paso 1: Abre el informe de estado de expedientes (escritorio)](../../../salida/manual/img/informes/estado-de-expedientes/01-abre-el-informe-de-estado-de-expedientes-claro.webp)

**Configuración que lo afecta**

- **Permisos del rol (leer expedientes)** (`Configuración › Usuarios y roles (/usuarios)`): Quien no tiene permiso para leer expedientes no ve el informe de estado: la pestaña que se abre por defecto es la de vínculos.

## Paso 2. Lee cuánto tarda cada área

La tabla «Tiempos por área» cuenta, para cada área, los expedientes que pasaron por ella, los que siguen pendientes, y el promedio, la mediana y el máximo de días que tardó en decidir. Los tiempos salen de los expedientes aprobados: la apertura hasta la aprobación se reparte entre la carga del tercero, la devolución y la revisión.

> **Pantalla:** `/e/{empresa}/informes/estado-expedientes`

![Paso 2: Lee cuánto tarda cada área (escritorio)](../../../salida/manual/img/informes/estado-de-expedientes/02-lee-cuanto-tarda-cada-area-claro.webp)

## Paso 3. Filtra por estado

El filtro «Estado» deja solo los expedientes en esa situación: en carga, en revisión, observado, aprobado, rechazado, anulado o caducado. Los filtros de tipo de vínculo y de fechas se combinan con este. «Limpiar filtros» los quita todos.

> **Pantalla:** `/e/{empresa}/informes/estado-expedientes`

![Paso 3: Filtra por estado (escritorio)](../../../salida/manual/img/informes/estado-de-expedientes/03-filtra-por-estado-claro.webp)

## Paso 4. Descarga el informe en Excel

«Descargar Excel» baja el informe con los mismos filtros que ves en pantalla, en un archivo que se llama «estado-expedientes» seguido del nombre de la empresa y la fecha. Si cambias los filtros antes de descargar, el archivo sale con los nuevos.

> **Pantalla:** `/e/{empresa}/informes/estado-expedientes`

![Paso 4: Descarga el informe en Excel (escritorio)](../../../salida/manual/img/informes/estado-de-expedientes/04-descarga-el-informe-en-excel-claro.webp)

## Recorridos relacionados

- [Exporta un expediente completo](exportar-un-expediente.md)
- [Mide el cumplimiento de los plazos de revisión](plazos-de-revision.md)
- [Aprueba el área de Compras](../revision/aprobar-un-area.md)
