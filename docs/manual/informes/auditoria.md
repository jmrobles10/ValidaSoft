---
id: "informes/auditoria"
titulo: "Revisa quién cambió la configuración en Auditoría"
actor: "Administrador de empresa"
dominio: "informes"
resumen: "La administradora de Constructora Norte abre Auditoría para saber quién creó y cambió las áreas de la empresa. Abre un cambio y ve, campo por campo, qué cambió. Luego revisa la pestaña de accesos, donde quedan las descargas de documentos."
pasos: 3
---

# Revisa quién cambió la configuración en Auditoría

**Actor:** Administrador de empresa · **Dominio:** Informes, auditoría y certificados

La administradora de Constructora Norte abre Auditoría para saber quién creó y cambió las áreas de la empresa. Abre un cambio y ve, campo por campo, qué cambió. Luego revisa la pestaña de accesos, donde quedan las descargas de documentos.

**Antes de empezar**

- Un grupo con Constructora Norte, con el área Compras y sus miembros configurados (la siembra registra esos cambios).
- Una administradora del grupo con permiso para ver la auditoría.

**Pasos**

1. Abre la auditoría de configuración
2. Mira qué campo cambió
3. Revisa los accesos a documentos

## Paso 1. Abre la auditoría de configuración

Auditoría muestra cada cambio de configuración de la empresa, con la fecha y hora, quién lo hizo, qué entidad cambió (aquí, el área Compras), la acción (creado o modificado) y su alcance. La pestaña «Configuración» es la que se abre por defecto. Haz clic en una fila para ver el detalle.

> **Pantalla:** `/e/{empresa}/auditoria`

![Paso 1: Abre la auditoría de configuración (escritorio)](../../../salida/manual/img/informes/auditoria/01-abre-la-auditoria-de-configuracion-claro.webp)

**Configuración que lo afecta**

- **Permiso de auditoría (leer y exportar)** (`Configuración › Usuarios y roles (/usuarios)`): Quien tiene permiso de auditoría puede ver estos registros y exportarlos a Excel.

## Paso 2. Mira qué campo cambió

El detalle muestra «Cambios campo por campo»: cada campo que cambió, con su valor antes y su valor después. En el caso de Compras se ve el campo «miembros», con la lista antes y después (las personas aparecen por su identificador). Así no hace falta adivinar qué se tocó en un cambio.

> **Pantalla:** `/e/{empresa}/auditoria`

![Paso 2: Mira qué campo cambió (escritorio)](../../../salida/manual/img/informes/auditoria/02-mira-que-campo-cambio-claro.webp)

## Paso 3. Revisa los accesos a documentos

La pestaña «Accesos» registra quién descargó documentos o exportó expedientes. Sin descargas, aparece «No hay registros de auditoría». Los eventos de cada vínculo y de cada expediente están en su propio historial, no aquí.

> **Pantalla:** `/e/{empresa}/auditoria`

![Paso 3: Revisa los accesos a documentos (escritorio)](../../../salida/manual/img/informes/auditoria/03-revisa-los-accesos-a-documentos-claro.webp)

## Recorridos relacionados

- [Crea un área, asígnale personas y desactívala](../configuracion/areas-y-miembros.md)
- [Exporta un expediente completo](exportar-un-expediente.md)
