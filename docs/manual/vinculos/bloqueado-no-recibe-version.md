---
id: "vinculos/bloqueado-no-recibe-version"
titulo: "Bloquea un vínculo y lo desbloquea con una versión nueva pendiente"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "La administradora bloquea a un proveedor por una revisión de sanciones. Mientras está bloqueado, el proveedor sube un RUT nuevo y el vínculo no abre revisión con él. Al desbloquearlo, la actualización se abre y el vínculo queda «En revisión»."
pasos: 4
---

# Bloquea un vínculo y lo desbloquea con una versión nueva pendiente

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

La administradora bloquea a un proveedor por una revisión de sanciones. Mientras está bloqueado, el proveedor sube un RUT nuevo y el vínculo no abre revisión con él. Al desbloquearlo, la actualización se abre y el vínculo queda «En revisión».

**Antes de empezar**

- Un grupo con Constructora Norte, con el tipo Aliado y un flujo que pide el RUT.
- Un proveedor aprobado por Compras (vínculo activo, certificado vigente) que, mientras está bloqueado, sube una versión nueva del RUT.

**Pasos**

1. Bloquea el vínculo con un motivo
2. Comprueba que el RUT nuevo no abrió revisión
3. Desbloquea con una justificación
4. Comprueba que el vínculo quedó en revisión

## Paso 1. Bloquea el vínculo con un motivo

Bloquear suspende la relación con el proveedor mientras dure la revisión. El motivo queda en la auditoría de la empresa y el proveedor no lo ve. Mientras el vínculo esté bloqueado, las versiones nuevas de sus documentos no abren una revisión.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 1: Bloquea el vínculo con un motivo (escritorio)](../../../salida/manual/img/vinculos/bloqueado-no-recibe-version/01-bloquea-el-vinculo-con-un-motivo-claro.webp)

## Paso 2. Comprueba que el RUT nuevo no abrió revisión

El vínculo sigue bloqueado con el mismo motivo y el certificado no pasa a «En verificación»: la versión nueva que subió el proveedor no abre una revisión mientras el bloqueo esté vigente. La revisión empieza cuando desbloqueas.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 2: Comprueba que el RUT nuevo no abrió revisión (escritorio)](../../../salida/manual/img/vinculos/bloqueado-no-recibe-version/02-comprueba-que-el-rut-nuevo-no-abrio-revision-claro.webp)

## Paso 3. Desbloquea con una justificación

Al desbloquear, el sistema abre la actualización pendiente del RUT nuevo. La justificación queda en el historial y el motivo del bloqueo no se borra.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Desbloquea con una justificación (escritorio)](../../../salida/manual/img/vinculos/bloqueado-no-recibe-version/03-desbloquea-con-una-justificacion-claro.webp)

## Paso 4. Comprueba que el vínculo quedó en revisión

El vínculo queda «En revisión» y su certificado se ve «En verificación» mientras las áreas revisan el RUT nuevo. El proveedor ve lo mismo en «Mis empresas», donde su revisión con Constructora Norte vuelve a estar en curso.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 4: Comprueba que el vínculo quedó en revisión (escritorio)](../../../salida/manual/img/vinculos/bloqueado-no-recibe-version/04-comprueba-que-el-vinculo-quedo-en-revision-claro.webp)

## Recorridos relacionados

- [Bloquea un vínculo y luego lo desbloquea](bloquear-y-desbloquear.md)
- [Ve un vínculo con una actualización en revisión](vinculo-con-actualizacion-pendiente.md)
