---
id: "cumplimiento/estudio-de-cumplimiento"
titulo: "Registra el estudio de cumplimiento de un proveedor"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "La oficial de cumplimiento registra el estudio SAGRILAFT de un proveedor de riesgo alto: elige el nivel, declara si hay conflicto de interés, ajusta la debida diligencia y da un concepto. Al registrarlo, el último pendiente se cumple y el vínculo se aprueba."
pasos: 4
---

# Registra el estudio de cumplimiento de un proveedor

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

La oficial de cumplimiento registra el estudio SAGRILAFT de un proveedor de riesgo alto: elige el nivel, declara si hay conflicto de interés, ajusta la debida diligencia y da un concepto. Al registrarlo, el último pendiente se cumple y el vínculo se aprueba.

**Antes de empezar**

- Un grupo con Constructora Norte, sus áreas Compras (Rubén) y Cumplimiento (Olga), con un flujo de proveedores que pide listas restrictivas y el estudio de cumplimiento, ambos bloqueantes.
- Un proveedor sin coincidencias que ya entregó sus documentos y tiene aprobadas Compras y Cumplimiento: falta solo el estudio.

**Pasos**

1. Abre el estudio de cumplimiento
2. Elige el nivel de riesgo y el concepto
3. Declara conflictos y registra el concepto
4. Revisa el estudio registrado y el vínculo aprobado

## Paso 1. Abre el estudio de cumplimiento

El estudio es el análisis SAGRILAFT del tercero que hace el oficial. Un recuadro explica qué pasará al registrarlo: si es lo último que falta, el expediente se aprueba; si el concepto es desfavorable, la verificación queda fallida y el expediente no se aprueba. El estudio queda vigente el número de días que fija el flujo y, al vencer, hay que volver a hacerlo.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre el estudio de cumplimiento (escritorio)](../../../salida/manual/img/cumplimiento/estudio-de-cumplimiento/01-abre-el-estudio-de-cumplimiento-claro.webp)

**Configuración que lo afecta**

- **Vigencia del estudio de cumplimiento** (`Configuración › Flujos aprobatorios (/flujos)`): Define en el flujo cuántos días vale el estudio y cuándo se pide.

## Paso 2. Elige el nivel de riesgo y el concepto

Eliges el nivel de riesgo (Bajo, Medio o Alto) y el concepto (Favorable o Desfavorable). Si la empresa tiene una matriz de riesgo activa, el nivel sale del puntaje de sus factores y solo puedes ajustarlo con una justificación. Con riesgo alto, la debida diligencia no puede ser simplificada: el sistema lo impide y muestra el mensaje en rojo antes de registrar.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Elige el nivel de riesgo y el concepto (escritorio)](../../../salida/manual/img/cumplimiento/estudio-de-cumplimiento/02-elige-el-nivel-de-riesgo-y-el-concepto-claro.webp)

**Variantes**

- **La empresa tiene una matriz de riesgo activa:** Aparece el resultado de la matriz (puntaje, nivel, debida diligencia y revisión en meses) y, para cambiar el nivel, debes justificarlo.
- **No hay matriz activa:** Eliges el nivel a mano, sin cálculo, y la debida diligencia mínima depende del nivel.
- **El concepto es «Desfavorable»:** Debes escribir las observaciones y la verificación queda fallida: el expediente no se aprueba.

**Configuración que lo afecta**

- **Matriz de riesgo SAGRILAFT** (`Configuración › Matriz de riesgo`): Sus factores, pesos y límites de nivel calculan el riesgo del tercero en cada estudio.

## Paso 3. Declara conflictos y registra el concepto

Marca si hay conflicto de interés con alguien de la empresa y escribe las observaciones que sustentan el concepto; si declaras un conflicto, las observaciones son obligatorias. Con la debida diligencia intensificada el riesgo alto queda cubierto. Al registrar, el estudio se guarda con tu nombre y la fecha, y se muestra en la sección «Estudio de cumplimiento» del expediente.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Declara conflictos y registra el concepto (escritorio)](../../../salida/manual/img/cumplimiento/estudio-de-cumplimiento/03-declara-conflictos-y-registra-el-concepto-claro.webp)

**Variantes**

- **Marcas conflicto de interés:** El estudio queda registrado con esa declaración y el informe SAGRILAFT la muestra.

## Paso 4. Revisa el estudio registrado y el vínculo aprobado

Al registrar, el estudio queda con el nivel «3 · Alto» y su vigencia, y como era lo último que faltaba, el expediente se aprueba y el vínculo queda activo. El tercero recibe el correo con el resultado.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 4: Revisa el estudio registrado y el vínculo aprobado (escritorio)](../../../salida/manual/img/cumplimiento/estudio-de-cumplimiento/04-revisa-el-estudio-registrado-y-el-vinculo-aproba-claro.webp)

## Recorridos relacionados

- [Renueva un estudio de cumplimiento vencido](estudio-vencido-y-renovacion.md)
- [Adjunta los soportes al estudio de cumplimiento](adjuntos-del-estudio.md)
