---
id: "cumplimiento/registrar-consulta-por-fuera"
titulo: "Registra una consulta de listas hecha por fuera"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "Cuando el proveedor de listas falla o la consulta se hizo en otro sitio, la oficial de cumplimiento sube el PDF de esa consulta, dice dónde y cuándo la hizo y registra lo que encontró. Entra al expediente como una consulta más."
pasos: 4
---

# Registra una consulta de listas hecha por fuera

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

Cuando el proveedor de listas falla o la consulta se hizo en otro sitio, la oficial de cumplimiento sube el PDF de esa consulta, dice dónde y cuándo la hizo y registra lo que encontró. Entra al expediente como una consulta más.

**Antes de empezar**

- Un grupo con Constructora Norte y el flujo de proveedores con listas restrictivas bloqueantes.
- Un proveedor en revisión, con Compras ya aprobada. Las listas automáticas no la consultaron (o fallaron).
- Un PDF de la consulta hecha en el sitio del proveedor de listas.

**Pasos**

1. Sube el PDF y dónde hiciste la consulta
2. Indica si hubo coincidencias
3. Revisa la consulta registrada en el expediente
4. Descarta la coincidencia y el vínculo se aprueba

## Paso 1. Sube el PDF y dónde hiciste la consulta

Sube el PDF o la imagen que obtuviste (hasta 15 MB). Escribe dónde la hiciste (entre 3 y 100 caracteres), la fecha de la consulta (hasta 30 días atrás) y cuántos meses vale el resultado. Queda guardada con tu nombre como una consulta más, y el PDF se conserva como evidencia del expediente.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Sube el PDF y dónde hiciste la consulta (escritorio)](../../../salida/manual/img/cumplimiento/registrar-consulta-por-fuera/01-sube-el-pdf-y-donde-hiciste-la-consulta-claro.webp)

**Variantes**

- **La consulta se hizo hace más de 30 días:** El formulario no la acepta: hay que hacerla de nuevo.

## Paso 2. Indica si hubo coincidencias

Si la consulta no encontró a nadie, elige «No hubo» y las listas quedan satisfechas. Si sí hubo, por cada coincidencia indicas la persona (tercero, representante, socio o PEP), la lista, el nombre con el que aparece y un detalle opcional. Con coincidencias, las listas quedan por analizar: tú decides después si cada una se descarta o se confirma.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Indica si hubo coincidencias (escritorio)](../../../salida/manual/img/cumplimiento/registrar-consulta-por-fuera/02-indica-si-hubo-coincidencias-claro.webp)

**Variantes**

- **Elegiste «No hubo»:** Se registra sin coincidencias y la verificación queda satisfecha de inmediato.

## Paso 3. Revisa la consulta registrada en el expediente

La consulta aparece en «Listas restrictivas» con la marca «Manual · Infolaft (sitio web)» y el PDF subido por ti. Como tenía una coincidencia, la verificación queda por analizar y el expediente no se puede aprobar hasta que la decidas. Las consultas anteriores quedan debajo, para no perder el historial.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Revisa la consulta registrada en el expediente (escritorio)](../../../salida/manual/img/cumplimiento/registrar-consulta-por-fuera/03-revisa-la-consulta-registrada-en-el-expediente-claro.webp)

**Configuración que lo afecta**

- **Proveedor de listas restrictivas** (`Configuración › Listas restrictivas (/listas-restrictivas)`): Define con qué proveedor se consulta las listas y la cuota mensual de búsquedas del grupo.

## Paso 4. Descarta la coincidencia y el vínculo se aprueba

Descartar la coincidencia, con su justificación, deja las listas satisfechas. Como no falta ninguna otra verificación, al aprobar tu área el vínculo se aprueba y queda activo; el tercero recibe el correo con el resultado.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 4: Descarta la coincidencia y el vínculo se aprueba (escritorio)](../../../salida/manual/img/cumplimiento/registrar-consulta-por-fuera/04-descarta-la-coincidencia-y-el-vinculo-se-aprueba-claro.webp)

## Recorridos relacionados

- [Revisa las listas restrictivas consultadas al pasar a revisión](pedir-listas-restrictivas.md)
- [Analiza una coincidencia en las listas y la descarta por homónimo](analizar-una-coincidencia.md)
