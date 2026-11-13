---
id: "vinculos/anular-vinculo"
titulo: "Anula un vínculo creado por error"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora anula un vínculo que se creó por error: escribe el motivo, confirma escribiendo la palabra ANULAR y el vínculo deja de estar activo para esta empresa, sin que pueda deshacerse."
pasos: 3
---

# Anula un vínculo creado por error

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora anula un vínculo que se creó por error: escribe el motivo, confirma escribiendo la palabra ANULAR y el vínculo deja de estar activo para esta empresa, sin que pueda deshacerse.

**Antes de empezar**

- Un grupo empresarial con la empresa «Constructora Norte» y una administradora del grupo.
- Un vínculo de proveedor en estado Invitado creado por error.

**Pasos**

1. Abre la acción Anular
2. Escribe el motivo y confirma con la palabra ANULAR
3. Comprueba que el vínculo quedó anulado

## Paso 1. Abre la acción Anular

Anular sirve para vínculos creados por error, por ejemplo una invitación enviada a la persona equivocada. Está en «Más acciones», junto a las demás acciones del estado del vínculo. Anular no es un bloqueo ni una desvinculación: no se puede deshacer, aunque si ese tercero vuelve a serlo se crea un vínculo nuevo.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Abre la acción Anular (escritorio)](../../../salida/manual/img/vinculos/anular-vinculo/01-abre-la-accion-anular-claro.webp)

**Variantes**

- **El vínculo ya tiene un expediente aprobado:** La acción Anular no aparece para ese vínculo: primero hay que desvincularlo.

## Paso 2. Escribe el motivo y confirma con la palabra ANULAR

El diálogo pide el motivo, que queda en el historial con tu nombre, y que escribas la palabra ANULAR tal cual para confirmar. Sin esa palabra el botón de anular no se activa. Mientras no lo confirmes, nada cambia: puedes cerrar el diálogo con Cancelar.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Escribe el motivo y confirma con la palabra ANULAR (escritorio)](../../../salida/manual/img/vinculos/anular-vinculo/02-escribe-el-motivo-y-confirma-con-la-palabra-anul-claro.webp)

## Paso 3. Comprueba que el vínculo quedó anulado

Validasoft confirma con el aviso «Vínculo anulado.» y se queda en el detalle, que ahora muestra el estado «Anulado» con su fecha (1). El historial (2) conserva la anulación con el estado anterior, el motivo y quién la hizo. El vínculo deja de aparecer entre los vigentes de la empresa y ya no recibe invitaciones ni expedientes.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Comprueba que el vínculo quedó anulado (escritorio)](../../../salida/manual/img/vinculos/anular-vinculo/03-comprueba-que-el-vinculo-quedo-anulado-claro.webp)

## Recorridos relacionados

- [Bloquea un vínculo y luego lo desbloquea](bloquear-y-desbloquear.md)
- [Revisa el detalle de un vínculo](detalle-del-vinculo.md)
