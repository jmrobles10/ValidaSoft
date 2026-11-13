---
id: "cumplimiento/analizar-una-coincidencia"
titulo: "Analiza una coincidencia en las listas y la descarta por homónimo"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "La consulta de listas encontró una persona parecida en OFAC. La oficial de cumplimiento compara los datos, comprueba que es un homónimo, lo descarta con una justificación y el expediente puede aprobarse."
pasos: 4
---

# Analiza una coincidencia en las listas y la descarta por homónimo

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

La consulta de listas encontró una persona parecida en OFAC. La oficial de cumplimiento compara los datos, comprueba que es un homónimo, lo descarta con una justificación y el expediente puede aprobarse.

**Antes de empezar**

- Un grupo con Constructora Norte, sus áreas Compras (Rubén) y Cumplimiento (Olga) y el flujo de proveedores con la verificación de listas bloqueante.
- Un proveedor cuyo nombre coincide con una entrada simulada de OFAC, que ya entregó sus documentos y quedó «En revisión».
- Compras ya aprobó: falta solo la decisión de Cumplimiento sobre la coincidencia.

**Pasos**

1. Abre la coincidencia encontrada
2. Aprueba tu área: el sistema lo impide
3. Descarta la coincidencia con su justificación
4. Revisa la decisión registrada

## Paso 1. Abre la coincidencia encontrada

La lista de la coincidencia muestra la persona consultada (tercero, representante, socio o PEP), su rol y su identificación, la lista donde apareció, el puntaje de parecido y el nombre con el que aparece en la lista. Mientras la coincidencia esté por analizar, la verificación no se satisface y el expediente no puede aprobarse.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre la coincidencia encontrada (escritorio)](../../../salida/manual/img/cumplimiento/analizar-una-coincidencia/01-abre-la-coincidencia-encontrada-claro.webp)

**Variantes**

- **La consulta es de un proveedor que muestra puntaje:** El porcentaje indica el parecido del nombre; un puntaje alto no decide nada por sí solo: lo decides tú con los datos.

## Paso 2. Aprueba tu área: el sistema lo impide

Al aprobar el área de Cumplimiento, Validasoft comprueba las verificaciones bloqueantes del expediente. Como la coincidencia sigue por analizar, el área queda aprobada pero el aviso dice que falta cumplimiento para aprobar el vínculo: «Listas restrictivas (coincidencia por analizar)». El expediente sigue en revisión hasta que decidas la coincidencia.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Aprueba tu área: el sistema lo impide (escritorio)](../../../salida/manual/img/cumplimiento/analizar-una-coincidencia/02-aprueba-tu-area-el-sistema-lo-impide-claro.webp)

## Paso 3. Descarta la coincidencia con su justificación

Descartar significa que la persona de la lista no es el tercero o su representante: es un homónimo. Compara el nombre y la identificación de la lista con los del expediente antes de decidir. La justificación es obligatoria (al menos 10 caracteres): queda en el historial y en el informe SAGRILAFT, y el tercero no la lee. La decisión vale 12 meses para las reconsultas.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Descarta la coincidencia con su justificación (escritorio)](../../../salida/manual/img/cumplimiento/analizar-una-coincidencia/03-descarta-la-coincidencia-con-su-justificacion-claro.webp)

**Variantes**

- **Eres revisor de un área (no de Cumplimiento):** No ves los botones «Descartar» ni «Confirmar»: solo Cumplimiento decide sobre las coincidencias.

## Paso 4. Revisa la decisión registrada

La coincidencia queda como «Descartada», con quién la decidió, cuándo y la justificación. La verificación pasa a «Satisfecha» y, como ninguna otra área pide algo, el expediente se aprueba y el vínculo queda activo. Si una coincidencia se confirma en lugar de descartarse, la verificación falla y el expediente no se aprueba (ver «Coincidencia confirmada»).

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 4: Revisa la decisión registrada (escritorio)](../../../salida/manual/img/cumplimiento/analizar-una-coincidencia/04-revisa-la-decision-registrada-claro.webp)

**Configuración que lo afecta**

- **Proveedor de listas restrictivas** (`Configuración › Listas restrictivas (/listas-restrictivas)`): Las coincidencias se analizan con el mismo proveedor con el que se consultó la lista.

## Recorridos relacionados

- [Revisa las listas restrictivas consultadas al pasar a revisión](pedir-listas-restrictivas.md)
- [Confirma una coincidencia y rechaza el vínculo](coincidencia-confirmada.md)
