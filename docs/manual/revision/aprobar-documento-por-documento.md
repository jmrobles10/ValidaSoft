---
id: "revision/aprobar-documento-por-documento"
titulo: "Aprueba el expediente documento por documento"
actor: "Revisor/área"
dominio: "revision"
resumen: "Olga, de Cumplimiento, no aprueba el área de una sola vez: decide sobre cada documento que pide el flujo. Activa la decisión por documento, aprueba el RUT y la Cámara de comercio por separado, y el área se cierra al decidir el último."
pasos: 3
---

# Aprueba el expediente documento por documento

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

Olga, de Cumplimiento, no aprueba el área de una sola vez: decide sobre cada documento que pide el flujo. Activa la decisión por documento, aprueba el RUT y la Cámara de comercio por separado, y el área se cierra al decidir el último.

**Antes de empezar**

- Un grupo con Constructora Norte, con las áreas Compras y Cumplimiento. Olga es miembro de Cumplimiento.
- Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión.

**Pasos**

1. Activa la decisión por documento
2. Aprueba el RUT
3. Cierra el área al decidir el último documento

## Paso 1. Activa la decisión por documento

En «Documentos que pide el flujo» el botón «Decidir por documento» muestra un botón «Aprobar u observar» junto a cada documento que tu área revisa. Así decides documento por documento en vez de aprobar el área completa de una vez. Vuelve a tocarlo para ocultar esos botones.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Activa la decisión por documento (escritorio)](../../../salida/manual/img/revision/aprobar-documento-por-documento/01-activa-la-decision-por-documento-claro.webp)

**Variantes**

- **El tercero sube una versión nueva de un documento ya decidido:** Tu decisión anterior queda como «sin efecto» sobre la versión vieja y el documento vuelve a estar por decidir.

## Paso 2. Aprueba el RUT

El diálogo muestra el documento y la versión que vas a decidir (la versión 1). Si el tercero sube otra versión antes de que confirmes, la pantalla te avisa y tu decisión no se aplica a la nueva. Aprobar deja registrado tu veredicto sobre este documento, con tu nombre y la versión.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Aprueba el RUT (escritorio)](../../../salida/manual/img/revision/aprobar-documento-por-documento/02-aprueba-el-rut-claro.webp)

## Paso 3. Cierra el área al decidir el último documento

Cuando apruebas el último documento que tu área revisa, el área pasa a «Aprobada» sin un paso adicional. Los documentos que ya decidiste quedan con tu nombre en su historial. Si ya no hay documentos por decidir, el botón de aprobar área ya no aparece.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Cierra el área al decidir el último documento (escritorio)](../../../salida/manual/img/revision/aprobar-documento-por-documento/03-cierra-el-area-al-decidir-el-ultimo-documento-claro.webp)

## Recorridos relacionados

- [Aprueba el área de Compras](aprobar-un-area.md)
- [Observa un documento con motivos](observar-con-motivos.md)
