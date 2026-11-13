---
id: "revision/eximir-un-documento"
titulo: "Exime a un tercero de un documento"
actor: "Administrador de empresa"
dominio: "revision"
resumen: "Ferretería Eximida es una persona jurídica sin Cámara de comercio, porque está inscrita en otro país. Ana, administradora, exime ese documento con un motivo: el expediente deja de pedirlo, las áreas no lo revisan y el proveedor lo ve como eximido."
pasos: 4
---

# Exime a un tercero de un documento

**Actor:** Administrador de empresa · **Dominio:** Revisión de expedientes

Ferretería Eximida es una persona jurídica sin Cámara de comercio, porque está inscrita en otro país. Ana, administradora, exime ese documento con un motivo: el expediente deja de pedirlo, las áreas no lo revisan y el proveedor lo ve como eximido.

**Antes de empezar**

- Un grupo con Constructora Norte, con la Cámara de comercio pedida en el flujo de proveedores. Ana es administradora.
- Un proveedor invitado por Norte que ya subió su RUT y todavía le falta la Cámara de comercio.

**Pasos**

1. Localiza el documento que no aplica
2. Escribe el motivo de la exención
3. Revisa el documento eximido
4. Así lo ve el proveedor

## Paso 1. Localiza el documento que no aplica

En «Documentos que pide el flujo» aparece cada documento con su estado. El de Cámara de comercio no tiene versión porque el proveedor no lo tiene. Con el botón «Eximir» quien administra la empresa puede quitar ese requisito para este expediente, con un motivo.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Localiza el documento que no aplica (escritorio)](../../../salida/manual/img/revision/eximir-un-documento/01-localiza-el-documento-que-no-aplica-claro.webp)

**Configuración que lo afecta**

- **Documento obligatorio del flujo**: Un documento obligatorio bloquea la revisión hasta que el proveedor lo suba o se exima con motivo.

## Paso 2. Escribe el motivo de la exención

El diálogo explica que el documento deja de exigirse en este expediente y que sus veredictos dejan de contar. Sin justificación no se puede eximir. La justificación queda en el historial del expediente con tu nombre.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Escribe el motivo de la exención (escritorio)](../../../salida/manual/img/revision/eximir-un-documento/02-escribe-el-motivo-de-la-exencion-claro.webp)

## Paso 3. Revisa el documento eximido

El documento muestra «Eximido por» con tu nombre, la fecha y el motivo que escribiste. Ya no tiene botón de eximir y el expediente sigue su curso. Las áreas no lo revisan: solo se les pide lo que falta por decidir.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Revisa el documento eximido (escritorio)](../../../salida/manual/img/revision/eximir-un-documento/03-revisa-el-documento-eximido-claro.webp)

## Paso 4. Así lo ve el proveedor

El proveedor ve la Cámara de comercio como eximida: ya no le piden subirla y su expediente queda en revisión con lo que sí se exige. El motivo de la exención es interno y no se le muestra.

> **Pantalla:** `/expediente/{id}`

![Paso 4: Así lo ve el proveedor (escritorio)](../../../salida/manual/img/revision/eximir-un-documento/04-asi-lo-ve-el-proveedor-claro.webp)

## Recorridos relacionados

- [Aprueba el área de Compras](aprobar-un-area.md)
- [Lee el historial de un expediente](historial-del-expediente.md)
