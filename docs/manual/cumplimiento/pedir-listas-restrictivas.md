---
id: "cumplimiento/pedir-listas-restrictivas"
titulo: "Revisa las listas restrictivas consultadas al pasar a revisión"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "El oficial de cumplimiento abre Cumplimiento y encuentra el expediente de un proveedor cuyas listas restrictivas ya se consultaron solas al llegar a revisión. Revisa a quién se consultó, cuándo y con qué resultado."
pasos: 3
---

# Revisa las listas restrictivas consultadas al pasar a revisión

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

El oficial de cumplimiento abre Cumplimiento y encuentra el expediente de un proveedor cuyas listas restrictivas ya se consultaron solas al llegar a revisión. Revisa a quién se consultó, cuándo y con qué resultado.

**Antes de empezar**

- Un grupo con Constructora Norte, con sus áreas Compras (Rubén) y Cumplimiento (Olga).
- El flujo de proveedores de Norte pide la verificación de listas restrictivas como bloqueante.
- Un proveedor que ya entregó su RUT y su Cámara de comercio y quedó «En revisión».

**Pasos**

1. Abre el expediente desde Cumplimiento
2. Revisa el resultado de las listas
3. Reconsulta o descarga la evidencia

## Paso 1. Abre el expediente desde Cumplimiento

La pestaña Cumplimiento lista los expedientes de la empresa que tienen verificaciones pendientes o por decidir. Abre el de «Aceros del Norte S.A.S.» para ver sus listas restrictivas. La consulta no la pediste tú: Validasoft la hizo sola cuando el expediente pasó a revisión.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre el expediente desde Cumplimiento (escritorio)](../../../salida/manual/img/cumplimiento/pedir-listas-restrictivas/01-abre-el-expediente-desde-cumplimiento-claro.webp)

**Configuración que lo afecta**

- **Flujo de proveedores con verificación de listas** (`Configuración › Flujos aprobatorios (/flujos)`): Solo se consultan las listas que el flujo pide como verificación.

## Paso 2. Revisa el resultado de las listas

La sección «Listas restrictivas» muestra el estado («Satisfecha» si no hay coincidencias), el proveedor con el que se consultó, la fecha y quién la hizo («Sistema» en una consulta automática), hasta cuándo sirve y las personas consultadas: el tercero y, si los hay, su representante legal, socios y PEP vinculados. Si ninguna persona aparece en las listas, lo dice en una línea.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Revisa el resultado de las listas (escritorio)](../../../salida/manual/img/cumplimiento/pedir-listas-restrictivas/02-revisa-el-resultado-de-las-listas-claro.webp)

**Variantes**

- **Hay coincidencias:** Aparecen debajo, una por una, y el estado cambia a «Coincidencia por analizar» hasta que alguien decida (ver el recorrido de coincidencias).
- **La última consulta automática falló:** Aparece un aviso con el error y la hora del próximo reintento; el sistema vuelve a consultar solo.
- **Se hizo una consulta manual por fuera:** La fuente aparece como manual y se indica quién la registró.

**Configuración que lo afecta**

- **Proveedor de listas restrictivas y cuota mensual** (`Configuración › Listas restrictivas (/listas-restrictivas)`): Define con qué proveedor se consulta (el de la instalación o uno propio de la empresa) y cuántas búsquedas puede hacer el grupo al mes.

## Paso 3. Reconsulta o descarga la evidencia

El botón «Evidencia (PDF)» descarga el soporte de la consulta, para guardarlo en el expediente. «Reconsultar» vuelve a consultar las listas ahora, sin esperar al monitoreo periódico; la consulta vale hasta la fecha «Vigente hasta» y, cuando vence, el sistema la rehace sola.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Reconsulta o descarga la evidencia (escritorio)](../../../salida/manual/img/cumplimiento/pedir-listas-restrictivas/03-reconsulta-o-descarga-la-evidencia-claro.webp)

**Variantes**

- **Eres revisor y no oficial de cumplimiento:** Ves las listas y la evidencia, pero no puedes reconsultar ni decidir coincidencias.

## Recorridos relacionados

- [Analiza una coincidencia en las listas y la descarta por homónimo](analizar-una-coincidencia.md)
