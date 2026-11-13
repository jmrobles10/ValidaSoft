---
id: "cumplimiento/matriz-de-riesgo-configuracion"
titulo: "Configura la matriz de riesgo SAGRILAFT de la empresa"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "El oficial de cumplimiento ajusta la matriz de riesgo: cuánto pesa cada factor, cómo se clasifica el riesgo y qué debida diligencia exige cada nivel. Al guardarla, los próximos estudios de cumplimiento la usan; los ya registrados conservan su cálculo."
pasos: 4
---

# Configura la matriz de riesgo SAGRILAFT de la empresa

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

El oficial de cumplimiento ajusta la matriz de riesgo: cuánto pesa cada factor, cómo se clasifica el riesgo y qué debida diligencia exige cada nivel. Al guardarla, los próximos estudios de cumplimiento la usan; los ya registrados conservan su cálculo.

**Antes de empezar**

- Un grupo con Constructora Norte, sin matriz propia, con la plantilla de la plataforma como base.

**Pasos**

1. Abre la matriz de riesgo
2. Ajusta el peso de un factor
3. Confirma el cambio de la matriz
4. Comprueba la versión guardada

## Paso 1. Abre la matriz de riesgo

La matriz de riesgo SAGRILAFT tiene factores, como el sector o actividad económica y la jurisdicción del tercero. Cada factor dice si se calcula de forma «Manual» o «Automático». Mientras no guardes una matriz propia, la empresa usa la plantilla de la plataforma.

> **Pantalla:** `/e/{empresa}/matriz-riesgo`

![Paso 1: Abre la matriz de riesgo (escritorio)](../../../salida/manual/img/cumplimiento/matriz-de-riesgo-configuracion/01-abre-la-matriz-de-riesgo-claro.webp)

**Configuración que lo afecta**

- **Matriz de riesgo** (`Configuración › Matriz de riesgo (/matriz-riesgo)`): Define el nivel de riesgo y la debida diligencia que recibe cada tercero en su estudio.

## Paso 2. Ajusta el peso de un factor

El peso de cada factor dice cuánto influye en el riesgo del tercero. Puedes cambiarlo y, con el interruptor «Calcular el riesgo con esta matriz», decidir si el cálculo usa la matriz propia. Mientras haya cambios sin guardar, la pantalla lo indica.

> **Pantalla:** `/e/{empresa}/matriz-riesgo`

![Paso 2: Ajusta el peso de un factor (escritorio)](../../../salida/manual/img/cumplimiento/matriz-de-riesgo-configuracion/02-ajusta-el-peso-de-un-factor-claro.webp)

**Variantes**

- **Quitas el cálculo con esta matriz:** Los estudios nuevos no usan la matriz propia y siguen la plantilla de la plataforma.

## Paso 3. Confirma el cambio de la matriz

El aviso dice que la matriz queda activa y que los próximos estudios la usarán. Los estudios que ya se registraron conservan el cálculo con el que se hicieron, para que el historial no cambie.

> **Pantalla:** `/e/{empresa}/matriz-riesgo`

![Paso 3: Confirma el cambio de la matriz (escritorio)](../../../salida/manual/img/cumplimiento/matriz-de-riesgo-configuracion/03-confirma-el-cambio-de-la-matriz-claro.webp)

## Paso 4. Comprueba la versión guardada

La matriz queda guardada como «Versión 1», con la fecha en que se guardó. Los cambios posteriores quedan como versiones nuevas.

> **Pantalla:** `/e/{empresa}/matriz-riesgo`

![Paso 4: Comprueba la versión guardada (escritorio)](../../../salida/manual/img/cumplimiento/matriz-de-riesgo-configuracion/04-comprueba-la-version-guardada-claro.webp)

## Recorridos relacionados

- [Elige con qué proveedor se consultan las listas restrictivas](listas-restrictivas-de-la-empresa.md)
