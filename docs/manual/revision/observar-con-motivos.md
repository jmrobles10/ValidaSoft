---
id: "revision/observar-con-motivos"
titulo: "Observa un documento con motivos"
actor: "Revisor/área"
dominio: "revision"
resumen: "Rubén revisa el RUT del proveedor y encuentra dos fallas. Lo observa marcando el documento y los motivos tipificados, agrega una nota y el proveedor recibe todo en un solo correo, sin ver quién revisó."
pasos: 3
---

# Observa un documento con motivos

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

Rubén revisa el RUT del proveedor y encuentra dos fallas. Lo observa marcando el documento y los motivos tipificados, agrega una nota y el proveedor recibe todo en un solo correo, sin ver quién revisó.

**Antes de empezar**

- Un grupo con Constructora Norte, con el área Compras y Rubén como miembro. Los motivos de observación del grupo están configurados.
- Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión.

**Pasos**

1. Marca el documento y los motivos
2. Revisa cómo queda el expediente
3. Así le llegan las observaciones al proveedor

## Paso 1. Marca el documento y los motivos

Marca cada documento que quieres observar y, debajo de él, los motivos que aplican. Los motivos son textos fijos que configura la empresa; el proveedor los lee tal cual, y nunca ve tu nombre ni el del área. La nota es opcional y también la lee el proveedor. Después, el botón dice cuántos documentos observas.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Marca el documento y los motivos (escritorio)](../../../salida/manual/img/revision/observar-con-motivos/01-marca-el-documento-y-los-motivos-claro.webp)

**Variantes**

- **La política del flujo es «Se devuelve de inmediato»:** La observación devuelve el expediente al proveedor en ese momento y frena a las demás áreas hasta que responda.
- **La política es «Se devuelve cuando la etapa se pronuncia»:** El proveedor recibe la observación cuando todas las áreas de la etapa se hayan pronunciado.

**Configuración que lo afecta**

- **Si un área observa** (`Configuración › Flujos (/flujos)`): Decide cuándo se devuelve la observación al proveedor.

## Paso 2. Revisa cómo queda el expediente

El expediente queda «Devuelto al tercero»: nadie emite veredictos hasta que el proveedor responda. Al reenviar, solo vuelve a revisar tu área, que es la que observó. Las demás áreas no pueden aprobar ni observar en ese momento.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Revisa cómo queda el expediente (escritorio)](../../../salida/manual/img/revision/observar-con-motivos/02-revisa-como-queda-el-expediente-claro.webp)

## Paso 3. Así le llegan las observaciones al proveedor

El proveedor recibe un solo correo con los motivos de todos los documentos observados y la nota que escribiste. El correo no menciona a los revisores ni a las áreas. Desde el enlace del correo ve qué corregir y puede responder con una versión nueva del documento.

> **Pantalla:** `/`

![Paso 3: Así le llegan las observaciones al proveedor (escritorio)](../../../salida/manual/img/revision/observar-con-motivos/03-asi-le-llegan-las-observaciones-al-proveedor-claro.webp)

## Recorridos relacionados

- [Revisa lo que corrigió el proveedor tras una observación](responder-con-lo-corregido.md)
- [Aprueba el expediente documento por documento](aprobar-documento-por-documento.md)
- [Rechaza un expediente por un hallazgo grave](rechazar-expediente.md)
