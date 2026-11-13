---
id: "cumplimiento/estudio-vencido-y-renovacion"
titulo: "Renueva un estudio de cumplimiento vencido"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "Un estudio de cumplimiento venció mientras el expediente seguía abierto. La oficial lo ve marcado como vencido, recibe el aviso, comprueba que el expediente no se puede aprobar con un estudio vencido y registra uno nuevo."
pasos: 4
---

# Renueva un estudio de cumplimiento vencido

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

Un estudio de cumplimiento venció mientras el expediente seguía abierto. La oficial lo ve marcado como vencido, recibe el aviso, comprueba que el expediente no se puede aprobar con un estudio vencido y registra uno nuevo.

**Antes de empezar**

- Un grupo con Constructora Norte y el flujo de proveedores con listas restrictivas y estudio de cumplimiento bloqueantes. El estudio vale 1 día.
- Un proveedor en revisión, con las listas satisfechas, y un estudio de riesgo bajo ya registrado por Cumplimiento que vence al pasar dos días.

**Pasos**

1. Revisa el estudio vencido
2. Recibe el aviso de vencimiento
3. El vencimiento bloquea la aprobación
4. Registra un estudio nuevo y aprueba

## Paso 1. Revisa el estudio vencido

Cuando un estudio vence, el expediente lo muestra como «Vencida» con el aviso «El estudio venció: hay que volver a hacerlo antes de aprobar». El botón «Registrar un estudio nuevo» abre el mismo formulario de siempre. Un estudio vence según los días que fija el flujo; si el expediente se cerró antes, el vencimiento no lo reabre.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Revisa el estudio vencido (escritorio)](../../../salida/manual/img/cumplimiento/estudio-vencido-y-renovacion/01-revisa-el-estudio-vencido-claro.webp)

**Configuración que lo afecta**

- **Vigencia del estudio de cumplimiento** (`Configuración › Flujos aprobatorios (/flujos)`): Los días de vigencia del estudio los fija cada flujo: al vencer, vuelve a pedirse.

## Paso 2. Recibe el aviso de vencimiento

Cumplimiento recibe una notificación en la campana: «Verificación vencida», con el nombre del tercero y la instrucción de volver a hacer el estudio antes de cerrar el expediente. El aviso se conserva en la campana hasta que lo leas.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Recibe el aviso de vencimiento (escritorio)](../../../salida/manual/img/cumplimiento/estudio-vencido-y-renovacion/02-recibe-el-aviso-de-vencimiento-claro.webp)

## Paso 3. El vencimiento bloquea la aprobación

Compras y Cumplimiento aprueban sus áreas, pero el expediente no se cierra: el aviso dice que falta el estudio de cumplimiento, que está vencido. Mientras el estudio esté vencido, el vínculo no se aprueba aunque todas las áreas hayan decidido.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: El vencimiento bloquea la aprobación (escritorio)](../../../salida/manual/img/cumplimiento/estudio-vencido-y-renovacion/03-el-vencimiento-bloquea-la-aprobacion-claro.webp)

## Paso 4. Registra un estudio nuevo y aprueba

El estudio nuevo reemplaza al vencido: queda vigente otra vez por los días del flujo. Como ya no falta nada, el expediente se aprueba y el vínculo queda activo; el tercero recibe el correo con el resultado.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 4: Registra un estudio nuevo y aprueba (escritorio)](../../../salida/manual/img/cumplimiento/estudio-vencido-y-renovacion/04-registra-un-estudio-nuevo-y-aprueba-claro.webp)

**Variantes**

- **El estudio nuevo es desfavorable:** La verificación queda fallida y el expediente no se aprueba: se rechaza por cumplimiento o se corrige.

## Recorridos relacionados

- [Registra el estudio de cumplimiento de un proveedor](estudio-de-cumplimiento.md)
- [Sigue el monitoreo de listas de un vínculo activo](monitoreo-periodico.md)
