---
id: "cumplimiento/monitoreo-periodico"
titulo: "Sigue el monitoreo de listas de un vínculo activo"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "Un proveedor ya aprobado sigue en monitoreo: Validasoft lo reconsulta en las listas cada 30 días. Cuando el proveedor agrega a un socio que aparece en una lista, la novedad llega a la bandeja de Cumplimiento y la oficial la confirma."
pasos: 4
---

# Sigue el monitoreo de listas de un vínculo activo

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

Un proveedor ya aprobado sigue en monitoreo: Validasoft lo reconsulta en las listas cada 30 días. Cuando el proveedor agrega a un socio que aparece en una lista, la novedad llega a la bandeja de Cumplimiento y la oficial la confirma.

**Antes de empezar**

- Un grupo con Constructora Norte y el flujo de proveedores con listas restrictivas bloqueantes.
- Un proveedor aprobado sin coincidencias, cuyo vínculo está activo y en monitoreo.
- El proveedor agrega después a un socio cuyo nombre aparece en una entrada simulada de OFAC.

**Pasos**

1. Revisa el monitoreo del vínculo activo
2. La reconsulta automática corre sin novedades
3. La novedad llega a la bandeja de Cumplimiento
4. Confirma la coincidencia del socio

## Paso 1. Revisa el monitoreo del vínculo activo

Un vínculo activo tiene su propio apartado de «Monitoreo de listas»: el estado de la última consulta, cuándo se hizo, cuándo es la próxima y las personas que se reconsultan. Sin cambios, la consulta sale «Satisfecha» y se reconsulta sola cada 30 días; no hace falta que nadie la pida.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Revisa el monitoreo del vínculo activo (escritorio)](../../../salida/manual/img/cumplimiento/monitoreo-periodico/01-revisa-el-monitoreo-del-vinculo-activo-claro.webp)

**Configuración que lo afecta**

- **Días de reconsulta de listas del vínculo activo** (`Configuración › Listas restrictivas (/listas-restrictivas)`): Cada cuántos días se reconsulta un vínculo activo. El estudio de cumplimiento vence en su propio plazo.

## Paso 2. La reconsulta automática corre sin novedades

Al cumplirse el plazo, Validasoft reconsulta el vínculo sin que nadie lo pida. Si las personas siguen sin aparecer en las listas, no hay novedad: el monitoreo queda registrado como hecho por «Sistema» y la siguiente reconsulta se programa otros 30 días.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: La reconsulta automática corre sin novedades (escritorio)](../../../salida/manual/img/cumplimiento/monitoreo-periodico/02-la-reconsulta-automatica-corre-sin-novedades-claro.webp)

## Paso 3. La novedad llega a la bandeja de Cumplimiento

Cuando el monitoreo encuentra una coincidencia nueva, la fila aparece en la bandeja de Cumplimiento con el origen «Monitoreo del vínculo» y la coincidencia por analizar. Te llega también una notificación en la campana. La fila dice el número de coincidencias por atender; «Abrir» lleva al vínculo para decidirla.

> **Pantalla:** `/e/{empresa}/cumplimiento`

![Paso 3: La novedad llega a la bandeja de Cumplimiento (escritorio)](../../../salida/manual/img/cumplimiento/monitoreo-periodico/03-la-novedad-llega-a-la-bandeja-de-cumplimiento-claro.webp)

## Paso 4. Confirma la coincidencia del socio

Confirmar aquí deja la coincidencia como confirmada con tu justificación. El vínculo sigue activo: no se bloquea por sí solo. Es un administrador de la empresa quien decide si lo bloquea o lo desvincula, con su motivo. La coincidencia queda en el informe SAGRILAFT y sale de tu bandeja, porque ya no tienes nada por decidir.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 4: Confirma la coincidencia del socio (escritorio)](../../../salida/manual/img/cumplimiento/monitoreo-periodico/04-confirma-la-coincidencia-del-socio-claro.webp)

## Recorridos relacionados

- [Confirma una coincidencia y rechaza el vínculo](coincidencia-confirmada.md)
- [Atiende lo pendiente de Cumplimiento desde su bandeja](bandeja-del-oficial.md)
