---
id: "informes/informe-de-vinculos"
titulo: "Consulta el informe de terceros y vínculos"
actor: "Administrador de empresa"
dominio: "informes"
resumen: "La administradora de Constructora Sur abre el informe de terceros y vínculos: ve un vínculo por fila con su estado, su certificado y lo que falta por validar. Filtra por estado y por tipo de vínculo, y lo descarga en Excel."
pasos: 3
---

# Consulta el informe de terceros y vínculos

**Actor:** Administrador de empresa · **Dominio:** Informes, auditoría y certificados

La administradora de Constructora Sur abre el informe de terceros y vínculos: ve un vínculo por fila con su estado, su certificado y lo que falta por validar. Filtra por estado y por tipo de vínculo, y lo descarga en Excel.

**Antes de empezar**

- Un grupo con Constructora Sur, con un tipo de vínculo de Proveedor y otro de Cliente.
- Dos vínculos invitados: un proveedor que aún no acepta y un cliente en la misma situación.

**Pasos**

1. Abre el informe de terceros y vínculos
2. Filtra por estado y tipo de vínculo
3. Descarga el informe en Excel

## Paso 1. Abre el informe de terceros y vínculos

La pestaña «Terceros y vínculos» trae un vínculo por fila, con el tercero, su estado, si tiene certificado vigente, el expediente en curso, los documentos por vencer o vencidos y las secciones que faltan por validar. Arriba, el resumen cuenta los vínculos y cuántos tienen algún pendiente. Haz clic en una fila para abrir el vínculo.

> **Pantalla:** `/e/{empresa}/informes/vinculos`

![Paso 1: Abre el informe de terceros y vínculos (escritorio)](../../../salida/manual/img/informes/informe-de-vinculos/01-abre-el-informe-de-terceros-y-vinculos-claro.webp)

## Paso 2. Filtra por estado y tipo de vínculo

El filtro «Estado» usa los mismos estados que la lista de vínculos (Invitado, En validación, En revisión, Activo, Por vencer, Vencido, Bloqueado, Desvinculado o Anulado). El filtro «Tipo de vínculo» separa a los proveedores de los clientes. Los dos se combinan: aquí solo queda el cliente invitado.

> **Pantalla:** `/e/{empresa}/informes/vinculos`

![Paso 2: Filtra por estado y tipo de vínculo (escritorio)](../../../salida/manual/img/informes/informe-de-vinculos/02-filtra-por-estado-y-tipo-de-vinculo-claro.webp)

**Configuración que lo afecta**

- **Tipos de vínculo** (`Configuración › Tipos de vínculo (/tipos-vinculo)`): Los tipos que existen en la empresa son las opciones de este filtro.

## Paso 3. Descarga el informe en Excel

«Descargar Excel» baja el informe con los filtros que tengas puestos en ese momento. El archivo trae las mismas filas que ves en la tabla, para trabajar con ellas fuera de Validasoft.

> **Pantalla:** `/e/{empresa}/informes/vinculos`

![Paso 3: Descarga el informe en Excel (escritorio)](../../../salida/manual/img/informes/informe-de-vinculos/03-descarga-el-informe-en-excel-claro.webp)

## Recorridos relacionados

- [Consulta el estado de los expedientes y sus tiempos](estado-de-expedientes.md)
- [Revisa la lista de vínculos y filtra por estado y tipo](../vinculos/lista-y-filtros.md)
- [Encuentra los certificados de la empresa](certificados.md)
