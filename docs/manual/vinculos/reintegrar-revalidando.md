---
id: "vinculos/reintegrar-revalidando"
titulo: "Reintegra un vínculo revalidando"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora reintegra un aliado desvinculado que nunca llegó a aprobarse. El diálogo le explica que no puede reintegrarlo sin revalidar, así que elige revalidar y el vínculo vuelve a su primera etapa para empezar de nuevo."
pasos: 4
---

# Reintegra un vínculo revalidando

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora reintegra un aliado desvinculado que nunca llegó a aprobarse. El diálogo le explica que no puede reintegrarlo sin revalidar, así que elige revalidar y el vínculo vuelve a su primera etapa para empezar de nuevo.

**Antes de empezar**

- Un grupo con Constructora Norte, con el área Compras y una administradora.
- Un tipo de vínculo Aliado con un flujo de una etapa (Compras) y un vínculo con el expediente abierto, sin aprobar.

**Pasos**

1. Abre el reintegro del vínculo desvinculado
2. Lee por qué no se puede reintegrar sin revalidar
3. Elige revalidar y escribe la justificación
4. Comprueba que el vínculo volvió a Invitado

## Paso 1. Abre el reintegro del vínculo desvinculado

Un vínculo desvinculado muestra el botón «Reintegrar» en su encabezado. Reintegrar devuelve la relación con el tercero; antes de confirmar, el diálogo te dice qué puedes hacer según lo que el vínculo ya tenía aprobado.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Abre el reintegro del vínculo desvinculado (escritorio)](../../../salida/manual/img/vinculos/reintegrar-revalidando/01-abre-el-reintegro-del-vinculo-desvinculado-claro.webp)

**Variantes**

- **El vínculo estaba aprobado y sus documentos siguen vigentes:** Puedes elegir «Reintegrar sin revalidar» y el vínculo vuelve a Activo sin pedir documentos (ver el recorrido de desvincular y reintegrar).

## Paso 2. Lee por qué no se puede reintegrar sin revalidar

El diálogo lista la razón: el vínculo nunca fue aprobado, así que no hay una aprobación que restituir. La opción «Reintegrar sin revalidar» aparece deshabilitada. La única salida es revalidar, que abre el proceso de validación desde cero.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Lee por qué no se puede reintegrar sin revalidar (escritorio)](../../../salida/manual/img/vinculos/reintegrar-revalidando/02-lee-por-que-no-se-puede-reintegrar-sin-revalidar-claro.webp)

**Variantes**

- **El vínculo estaba aprobado y uno de sus documentos terminó su vigencia (o está en gracia):** El diálogo lo lista como «Su vigencia terminó: …» y la opción sin revalidar no se ofrece: lo aprobado ya no está vigente y hay que revalidar.
- **El tercero subió una versión nueva en su bóveda que la empresa no revisó:** El diálogo lo lista como «Cambió en la bóveda del tercero, sin que la empresa lo revisara: …» y tampoco se ofrece reintegrar sin revalidar.

## Paso 3. Elige revalidar y escribe la justificación

Revalidar deja el vínculo en validación con un expediente nuevo que pide todos los documentos del flujo. La justificación queda en el historial, y el tercero no la ve. Si nunca fue aprobado, el vínculo vuelve a Invitado para empezar de nuevo.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Elige revalidar y escribe la justificación (escritorio)](../../../salida/manual/img/vinculos/reintegrar-revalidando/03-elige-revalidar-y-escribe-la-justificacion-claro.webp)

## Paso 4. Comprueba que el vínculo volvió a Invitado

El vínculo vuelve al estado «Invitado» y el historial registra el reintegro con tu justificación. El tercero recibe el aviso de que se reintegró su vínculo y debe volver a aceptar la invitación para revalidar.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 4: Comprueba que el vínculo volvió a Invitado (escritorio)](../../../salida/manual/img/vinculos/reintegrar-revalidando/04-comprueba-que-el-vinculo-volvio-a-invitado-claro.webp)

## Recorridos relacionados

- [Desvincula un tercero y lo reintegra sin revalidar](desvincular-y-reintegrar-sin-revalidar.md)
- [Bloquea un vínculo y luego lo desbloquea](bloquear-y-desbloquear.md)
