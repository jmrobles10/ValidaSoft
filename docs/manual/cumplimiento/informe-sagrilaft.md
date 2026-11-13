---
id: "cumplimiento/informe-sagrilaft"
titulo: "Consulta el informe SAGRILAFT de los terceros"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "El oficial de cumplimiento abre el informe SAGRILAFT de la empresa: una fila por tercero con su estudio y su riesgo, tarjetas con lo vigente, por vencer, vencido y sin estudio, la distribución del riesgo, los filtros y la descarga en Excel."
pasos: 5
---

# Consulta el informe SAGRILAFT de los terceros

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

El oficial de cumplimiento abre el informe SAGRILAFT de la empresa: una fila por tercero con su estudio y su riesgo, tarjetas con lo vigente, por vencer, vencido y sin estudio, la distribución del riesgo, los filtros y la descarga en Excel.

**Antes de empezar**

- Un grupo con Constructora Norte y un tipo de vínculo de cumplimiento con un flujo que pide listas restrictivas y el estudio de cumplimiento.
- Tres terceros de ese tipo: dos con estudio vigente (riesgo medio y alto) y uno sin estudio.

**Pasos**

1. Abre el informe SAGRILAFT
2. Revisa la distribución del riesgo y los tiempos
3. Lee una fila por tercero
4. Filtra por el estado del estudio
5. Descarga el informe en Excel

## Paso 1. Abre el informe SAGRILAFT

El informe muestra la situación de todos los terceros de la empresa frente al SAGRILAFT. Las tarjetas cuentan los terceros, los que tienen estudio vigente, los que lo tienen por vencer o vencido y los que no tienen estudio. Es la misma información que ves en la bandeja, pero completa y para todos los terceros, no solo los que esperan una decisión.

> **Pantalla:** `/e/{empresa}/cumplimiento`

![Paso 1: Abre el informe SAGRILAFT (escritorio)](../../../salida/manual/img/cumplimiento/informe-sagrilaft/01-abre-el-informe-sagrilaft-claro.webp)

**Variantes**

- **Eres revisor de un área:** No ves la pestaña del informe: el informe es del oficial de cumplimiento.

## Paso 2. Revisa la distribución del riesgo y los tiempos

La distribución cuenta cuántos terceros hay en cada nivel de riesgo (Bajo, Medio o Alto). Los tiempos de cumplimiento muestran qué tan a tiempo están los estudios, y el gráfico de los últimos 12 meses muestra la tendencia mes a mes.

> **Pantalla:** `/e/{empresa}/cumplimiento`

![Paso 2: Revisa la distribución del riesgo y los tiempos (escritorio)](../../../salida/manual/img/cumplimiento/informe-sagrilaft/02-revisa-la-distribucion-del-riesgo-y-los-tiempos-claro.webp)

## Paso 3. Lee una fila por tercero

Cada fila es un tercero con su estudio: el nivel de riesgo, la debida diligencia (Simple, Ordinaria o Intensificada) y el estado del estudio. Si el tercero no tiene estudio, la fila dice «Sin estudio». Por ejemplo, «Constructora Alta S.A.S.» aparece en riesgo alto con debida diligencia intensificada.

> **Pantalla:** `/e/{empresa}/cumplimiento`

![Paso 3: Lee una fila por tercero (escritorio)](../../../salida/manual/img/cumplimiento/informe-sagrilaft/03-lee-una-fila-por-tercero-claro.webp)

## Paso 4. Filtra por el estado del estudio

Los filtros reducen la tabla: con «Estudio» en «Sin estudio» quedan solo los terceros que no tienen estudio, para atenderlos. «Quitar filtros» vuelve a mostrar todos. El filtro de «Riesgo» funciona igual para encontrar, por ejemplo, los de riesgo alto.

> **Pantalla:** `/e/{empresa}/cumplimiento`

![Paso 4: Filtra por el estado del estudio (escritorio)](../../../salida/manual/img/cumplimiento/informe-sagrilaft/04-filtra-por-el-estado-del-estudio-claro.webp)

## Paso 5. Descarga el informe en Excel

El botón «Descargar Excel» baja el mismo informe en un archivo con tres hojas: Terceros, Personas y Verificaciones. Lo usas para entregarlo a un auditor o para archivarlo. La otra vista, «Consultas y estudios», conserva el informe de consultas y estudios del periodo y se exporta también a Excel.

> **Pantalla:** `/e/{empresa}/cumplimiento`

![Paso 5: Descarga el informe en Excel (escritorio)](../../../salida/manual/img/cumplimiento/informe-sagrilaft/05-descarga-el-informe-en-excel-claro.webp)

## Recorridos relacionados

- [Registra el estudio de cumplimiento de un proveedor](estudio-de-cumplimiento.md)
- [Atiende lo pendiente de Cumplimiento desde su bandeja](bandeja-del-oficial.md)
