---
id: "operacion/recordatorio-en-lote"
titulo: "Recuerda a varios terceros sus observaciones desde la bandeja"
actor: "Revisor/área"
dominio: "operacion"
resumen: "Rubén, de Compras, tiene dos expedientes devueltos a terceros que no han respondido. Desde la bandeja selecciona los observados y envía un recordatorio a todos de una vez; cada tercero recibe un correo y un aviso en su portal, y solo se le puede recordar una vez al día."
pasos: 4
---

# Recuerda a varios terceros sus observaciones desde la bandeja

**Actor:** Revisor/área · **Dominio:** Operación masiva y continua

Rubén, de Compras, tiene dos expedientes devueltos a terceros que no han respondido. Desde la bandeja selecciona los observados y envía un recordatorio a todos de una vez; cada tercero recibe un correo y un aviso en su portal, y solo se le puede recordar una vez al día.

**Antes de empezar**

- Un grupo con Constructora Norte, Rubén en el área Compras y dos proveedores con el RUT observado por Compras.

**Pasos**

1. Abre la bandeja con los expedientes observados
2. Selecciona los expedientes y confirma
3. Comprueba el resultado del envío
4. Intenta recordar de nuevo el mismo día

## Paso 1. Abre la bandeja con los expedientes observados

La bandeja de revisión lista los expedientes que esperan decisión de tus áreas. Los que devolviste con observaciones quedan a la espera de la respuesta del tercero. Desde la barra superior puedes recordarles a todos los observados de una vez.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 1: Abre la bandeja con los expedientes observados (escritorio)](../../../salida/manual/img/operacion/recordatorio-en-lote/01-abre-la-bandeja-con-los-expedientes-observados-claro.webp)

## Paso 2. Selecciona los expedientes y confirma

«Seleccionar observados» marca los expedientes devueltos. El botón indica cuántos vas a recordar. Antes de enviar, el aviso dice que cada tercero recibe un correo con lo que falta corregir y un aviso en su portal, y que a quien ya se le recordó hoy no se le vuelve a enviar.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 2: Selecciona los expedientes y confirma (escritorio)](../../../salida/manual/img/operacion/recordatorio-en-lote/02-selecciona-los-expedientes-y-confirma-claro.webp)

**Variantes**

- **Un expediente ya se recordó hoy:** Se omite y el resultado lo dice: «1 omitido: ya se les recordó hoy».
- **Eliges más de 200 expedientes a la vez:** Validasoft no envía: usa «Recordar a los observados».

## Paso 3. Comprueba el resultado del envío

El resultado dice a cuántos terceros se recordó y cuántos se omitieron, con el motivo. Cada tercero recibe un correo con el texto «te recuerda las observaciones de tu validación», con lo que falta corregir y un enlace para responder, sin nombres internos de la empresa.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 3: Comprueba el resultado del envío (escritorio)](../../../salida/manual/img/operacion/recordatorio-en-lote/03-comprueba-el-resultado-del-envio-claro.webp)

## Paso 4. Intenta recordar de nuevo el mismo día

Como ya se les recordó hoy, un segundo envío no hace nada: Validasoft lo indica sin enviar correos. El recordatorio se puede repetir al día siguiente.

> **Pantalla:** `/e/{empresa}/bandeja`

![Paso 4: Intenta recordar de nuevo el mismo día (escritorio)](../../../salida/manual/img/operacion/recordatorio-en-lote/04-intenta-recordar-de-nuevo-el-mismo-dia-claro.webp)

## Recorridos relacionados

- [Activa el recordatorio automático de observaciones](recordatorio-automatico-por-flujo.md)
