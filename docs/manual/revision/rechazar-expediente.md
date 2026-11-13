---
id: "revision/rechazar-expediente"
titulo: "Rechaza un expediente por un hallazgo grave"
actor: "Oficial de cumplimiento"
dominio: "revision"
resumen: "Olga, oficial de cumplimiento, encuentra que la Cámara de comercio no corresponde al proveedor. Como su área puede rechazar, cierra el expediente con motivos que el proveedor lee y una justificación interna que no ve."
pasos: 4
---

# Rechaza un expediente por un hallazgo grave

**Actor:** Oficial de cumplimiento · **Dominio:** Revisión de expedientes

Olga, oficial de cumplimiento, encuentra que la Cámara de comercio no corresponde al proveedor. Como su área puede rechazar, cierra el expediente con motivos que el proveedor lee y una justificación interna que no ve.

**Antes de empezar**

- Un grupo con Constructora Norte. Olga es miembro de Cumplimiento y su área tiene «Puede rechazar» en el flujo.
- Un proveedor invitado por Norte que ya entregó su RUT y su Cámara de comercio: su expediente está en revisión.

**Pasos**

1. Localiza la acción de rechazar
2. Elige los motivos y justifica el rechazo
3. Revisa el expediente rechazado
4. Así le llega el rechazo al proveedor

## Paso 1. Localiza la acción de rechazar

Tu área puede rechazar el expediente completo cuando encuentra un hallazgo grave. El botón «Rechazar» está en la barra de decisión del expediente. El rechazo es definitivo: el expediente se cierra y el vínculo vuelve a Invitado.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Localiza la acción de rechazar (escritorio)](../../../salida/manual/img/revision/rechazar-expediente/01-localiza-la-accion-de-rechazar-claro.webp)

**Variantes**

- **Tu área no tiene «Puede rechazar»:** No ves el botón «Rechazar»; solo puedes aprobar u observar.
- **El expediente está devuelto al proveedor por una observación:** Igual puedes rechazar: un hallazgo grave no espera a que el proveedor responda.

**Configuración que lo afecta**

- **Puede rechazar (por área, en el flujo)** (`Configuración › Flujos (/flujos)`): Solo las áreas marcadas con «Puede rechazar» ven el botón.

## Paso 2. Elige los motivos y justifica el rechazo

Marca los motivos: el proveedor lee el texto de cada uno. La justificación es interna y no sale en ningún correo: queda en el historial con tu nombre. Antes de confirmar, la pantalla recuerda que el rechazo cierra el expediente.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Elige los motivos y justifica el rechazo (escritorio)](../../../salida/manual/img/revision/rechazar-expediente/02-elige-los-motivos-y-justifica-el-rechazo-claro.webp)

## Paso 3. Revisa el expediente rechazado

El expediente queda cerrado y aparece el aviso de que el vínculo volvió a Invitado. Ya no se pueden emitir más veredictos sobre él, y la justificación queda visible solo para tu empresa.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Revisa el expediente rechazado (escritorio)](../../../salida/manual/img/revision/rechazar-expediente/03-revisa-el-expediente-rechazado-claro.webp)

## Paso 4. Así le llega el rechazo al proveedor

El proveedor recibe un correo que dice que la empresa cerró la revisión sin aprobarla, con los motivos en su texto. La justificación interna no aparece: el proveedor solo ve los motivos. Si tiene dudas, debe escribirle a la empresa.

> **Pantalla:** `/`

![Paso 4: Así le llega el rechazo al proveedor (escritorio)](../../../salida/manual/img/revision/rechazar-expediente/04-asi-le-llega-el-rechazo-al-proveedor-claro.webp)

## Recorridos relacionados

- [Aprueba el área de Compras](aprobar-un-area.md)
- [Observa un documento con motivos](observar-con-motivos.md)
