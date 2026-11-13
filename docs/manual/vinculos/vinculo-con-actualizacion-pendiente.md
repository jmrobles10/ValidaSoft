---
id: "vinculos/vinculo-con-actualizacion-pendiente"
titulo: "Ve un vínculo con una actualización en revisión"
actor: "Administrador de empresa"
dominio: "vinculos"
resumen: "El tercero, ya aprobado por Constructora Norte, sube un RUT nuevo. Mientras las áreas lo revisan, el vínculo sigue activo pero aparece «En revisión» en la lista, en el filtro y en el detalle, y su certificado se ve «En verificación»."
pasos: 3
---

# Ve un vínculo con una actualización en revisión

**Actor:** Administrador de empresa · **Dominio:** Vínculos y terceros

El tercero, ya aprobado por Constructora Norte, sube un RUT nuevo. Mientras las áreas lo revisan, el vínculo sigue activo pero aparece «En revisión» en la lista, en el filtro y en el detalle, y su certificado se ve «En verificación».

**Antes de empezar**

- Un grupo con Constructora Norte y Constructora Sur, con el tipo Aliado y un flujo que pide el RUT en cada empresa.
- Un tercero aprobado por las dos empresas, que luego sube una versión nueva del RUT que ambas exigen.

**Pasos**

1. Localiza el vínculo en la lista
2. Filtra los vínculos en revisión
3. Revisa el detalle y el certificado

## Paso 1. Localiza el vínculo en la lista

Cuando el tercero sube una versión nueva de un documento que tu empresa exige, el vínculo se muestra «En revisión» en la lista aunque siga activo. Así sabes que hay una actualización por decidir antes de dar el asunto por resuelto.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 1: Localiza el vínculo en la lista (escritorio)](../../../salida/manual/img/vinculos/vinculo-con-actualizacion-pendiente/01-localiza-el-vinculo-en-la-lista-claro.webp)

**Variantes**

- **El tercero no ha subido una versión nueva:** La fila muestra el estado normal del vínculo, por ejemplo «Activo».

## Paso 2. Filtra los vínculos en revisión

El filtro «Estado» con «En revisión» deja solo los vínculos con una actualización pendiente de decisión. Así ves de un vistazo qué tiene que revisar tu empresa.

> **Pantalla:** `/e/{empresa}/vinculos`

![Paso 2: Filtra los vínculos en revisión (escritorio)](../../../salida/manual/img/vinculos/vinculo-con-actualizacion-pendiente/02-filtra-los-vinculos-en-revision-claro.webp)

## Paso 3. Revisa el detalle y el certificado

El detalle muestra el vínculo como «En revisión» mientras las áreas revisan el RUT nuevo. Mientras dure la revisión, nada se suspende ni se bloquea: el certificado sigue vigente pero se ve «En verificación», igual que en la página pública de verificación.

> **Pantalla:** `/e/{empresa}/vinculos/{id}`

![Paso 3: Revisa el detalle y el certificado (escritorio)](../../../salida/manual/img/vinculos/vinculo-con-actualizacion-pendiente/03-revisa-el-detalle-y-el-certificado-claro.webp)

## Recorridos relacionados

- [Bloquea un vínculo y lo desbloquea con una versión nueva pendiente](bloqueado-no-recibe-version.md)
- [Revisa la lista de vínculos y filtra por estado y tipo](lista-y-filtros.md)
- [Revisa una versión nueva que llega mientras decides](../revision/actualizacion-de-un-documento.md)
