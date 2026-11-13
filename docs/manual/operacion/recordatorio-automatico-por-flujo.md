---
id: "operacion/recordatorio-automatico-por-flujo"
titulo: "Activa el recordatorio automático de observaciones"
actor: "Administrador de empresa"
dominio: "operacion"
resumen: "La administradora activa el recordatorio automático: mientras un expediente siga devuelto al tercero, Validasoft le recuerda sus observaciones cada cierto número de días. Cada flujo puede seguir lo de la empresa, apagarlo o fijar sus propios días."
pasos: 3
---

# Activa el recordatorio automático de observaciones

**Actor:** Administrador de empresa · **Dominio:** Operación masiva y continua

La administradora activa el recordatorio automático: mientras un expediente siga devuelto al tercero, Validasoft le recuerda sus observaciones cada cierto número de días. Cada flujo puede seguir lo de la empresa, apagarlo o fijar sus propios días.

**Antes de empezar**

- Un grupo con Constructora Sur y su administradora Ana, con el recordatorio automático apagado.

**Pasos**

1. Abre el recordatorio automático en Plantillas de correo
2. Activa el envío y elige cada cuántos días
3. Guarda el recordatorio automático

## Paso 1. Abre el recordatorio automático en Plantillas de correo

En «Plantillas de correo» aparece el bloque «Recordatorio automático de observaciones». Mientras un expediente siga devuelto al tercero, Validasoft le recuerda sus observaciones por correo y en su portal cada cierto número de días, a lo sumo una vez al día. Se detiene cuando el tercero responde.

> **Pantalla:** `/e/{empresa}/plantillas`

![Paso 1: Abre el recordatorio automático en Plantillas de correo (escritorio)](../../../salida/manual/img/operacion/recordatorio-automatico-por-flujo/01-abre-el-recordatorio-automatico-en-plantillas-de-claro.webp)

**Variantes**

- **Eres revisor, no administrador:** Ves el bloque, pero el interruptor y los días son solo de lectura.

**Configuración que lo afecta**

- **Recordatorio automático de observaciones** (`Configuración › Plantillas de correo (/plantillas)`): Es el valor de la empresa; cada flujo puede sobrescribirlo.

## Paso 2. Activa el envío y elige cada cuántos días

Enciende «Enviar recordatorios automáticos» y escribe en «Cada (días)» un número entero de 1 a 60. Los días cuentan desde que el expediente se devolvió o desde el último recordatorio. Con 3 días, el tercero recibe uno cada tres días mientras siga devuelto.

> **Pantalla:** `/e/{empresa}/plantillas`

![Paso 2: Activa el envío y elige cada cuántos días (escritorio)](../../../salida/manual/img/operacion/recordatorio-automatico-por-flujo/02-activa-el-envio-y-elige-cada-cuantos-dias-claro.webp)

**Variantes**

- **Escribes un número fuera de 1 a 60 o no entero:** El campo muestra «Escribe un número entero de 1 a 60» y el botón Guardar no se activa.
- **Apagas el interruptor:** El campo de días se desactiva y la empresa no envía recordatorios automáticos.

## Paso 3. Guarda el recordatorio automático

Al guardar, Validasoft confirma el cambio. El envío automático empieza a aplicarse a los expedientes devueltos desde ese momento, sin que nadie tenga que hacerlo.

> **Pantalla:** `/e/{empresa}/plantillas`

![Paso 3: Guarda el recordatorio automático (escritorio)](../../../salida/manual/img/operacion/recordatorio-automatico-por-flujo/03-guarda-el-recordatorio-automatico-claro.webp)

## Recorridos relacionados

- [Recuerda a varios terceros sus observaciones desde la bandeja](recordatorio-en-lote.md)
