---
id: "vinculos/bloquear-y-desbloquear"
titulo: "Bloquea un vínculo y luego lo desbloquea"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora bloquea un vínculo con un motivo y una fecha de fin para suspender al tercero sin borrar su historial, y después lo desbloquea con una justificación para que vuelva al estado anterior."
pasos: 5
---

# Bloquea un vínculo y luego lo desbloquea

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora bloquea un vínculo con un motivo y una fecha de fin para suspender al tercero sin borrar su historial, y después lo desbloquea con una justificación para que vuelva al estado anterior.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Norte» y una administradora del grupo.
- Un vínculo de proveedor en estado Invitado.

**Pasos**

1. Abre el menú de más acciones
2. Escribe el motivo y la fecha del bloqueo
3. Comprueba que el vínculo quedó bloqueado
4. Desbloquea con una justificación
5. Comprueba que el vínculo volvió a su estado

## Paso 1. Abre el menú de más acciones

Las acciones que cambian el vínculo están en «Más acciones». Lo que aparece depende del estado del vínculo: un vínculo Invitado permite bloquear, desvincular y anular; un bloqueado ofrece desbloquear. Solo quien tiene permiso para bloquear ve la opción.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Abre el menú de más acciones (escritorio)](../../../salida/manual/img/vinculos/bloquear-y-desbloquear/01-abre-el-menu-de-mas-acciones-claro.webp)

**Variantes**

- **Tu rol solo puede leer vínculos:** El menú no aparece y el detalle es de solo lectura.

**Configuración que lo afecta**

- **Usuarios y roles** (`Configuración › Usuarios y roles (/usuarios)`): El permiso de bloquear vínculos lo da el rol de cada persona.

## Paso 2. Escribe el motivo y la fecha del bloqueo

El bloqueo suspende la relación con tu empresa sin borrar el historial. El motivo queda en la auditoría de la empresa; el tercero no lo ve. «Bloquear hasta» es opcional: si la dejas vacía, el bloqueo dura hasta que lo quites. Mientras esté bloqueado, los sistemas integrados reciben el cambio.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Escribe el motivo y la fecha del bloqueo (escritorio)](../../../salida/manual/img/vinculos/bloquear-y-desbloquear/02-escribe-el-motivo-y-la-fecha-del-bloqueo-claro.webp)

## Paso 3. Comprueba que el vínculo quedó bloqueado

El detalle muestra el aviso «Vínculo bloqueado» con desde cuándo y hasta cuándo rige. La lista de vínculos lo marca con el estado Bloqueado, y el tercero deja de poder avanzar con esta empresa mientras dure el bloqueo.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Comprueba que el vínculo quedó bloqueado (escritorio)](../../../salida/manual/img/vinculos/bloquear-y-desbloquear/03-comprueba-que-el-vinculo-quedo-bloqueado-claro.webp)

## Paso 4. Desbloquea con una justificación

Desbloquear devuelve el vínculo al estado que tenía antes del bloqueo y deja registrada tu justificación en el historial. No borra el motivo original: queda como antecedente.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 4: Desbloquea con una justificación (escritorio)](../../../salida/manual/img/vinculos/bloquear-y-desbloquear/04-desbloquea-con-una-justificacion-claro.webp)

## Paso 5. Comprueba que el vínculo volvió a su estado

El vínculo vuelve a «Invitado», el estado previo al bloqueo, y el historial registra el bloqueo y el desbloqueo con sus fechas. Si el vínculo ya estaba aprobado, volvería a ese estado.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 5: Comprueba que el vínculo volvió a su estado (escritorio)](../../../salida/manual/img/vinculos/bloquear-y-desbloquear/05-comprueba-que-el-vinculo-volvio-a-su-estado-claro.webp)

## Recorridos relacionados

- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
- [Ve un vínculo con una actualización en revisión](vinculo-con-actualizacion-pendiente.md)
