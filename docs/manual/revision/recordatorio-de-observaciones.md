---
id: "revision/recordatorio-de-observaciones"
titulo: "Recuerda al proveedor sus observaciones pendientes"
actor: "Revisor/área"
dominio: "revision"
resumen: "Compras observó el RUT de un proveedor y este no ha respondido. Rubén envía un recordatorio individual desde el expediente: el proveedor recibe un correo con lo que falta corregir y un aviso en su portal. Solo se puede enviar uno al día por expediente."
pasos: 3
---

# Recuerda al proveedor sus observaciones pendientes

**Actor:** Revisor/área · **Dominio:** Revisión de expedientes

Compras observó el RUT de un proveedor y este no ha respondido. Rubén envía un recordatorio individual desde el expediente: el proveedor recibe un correo con lo que falta corregir y un aviso en su portal. Solo se puede enviar uno al día por expediente.

**Antes de empezar**

- Un grupo con Constructora Norte, con el área Compras (Rubén es miembro).
- Un expediente devuelto al proveedor por una observación de Compras sobre el RUT, sin respuesta todavía.

**Pasos**

1. Localiza el recordatorio en el expediente
2. Confirma el recordatorio
3. Revisa que el recordatorio quedó registrado

## Paso 1. Localiza el recordatorio en el expediente

Un expediente devuelto al proveedor muestra el botón «Recordar al tercero». Sirve para cuando el proveedor no ha respondido a tus observaciones. Si ya se le recordó hoy, el botón dice que ya se le recordó y no envía otro.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Localiza el recordatorio en el expediente (escritorio)](../../../salida/manual/img/revision/recordatorio-de-observaciones/01-localiza-el-recordatorio-en-el-expediente-claro.webp)

**Variantes**

- **El proveedor ya se le recordó hoy:** El botón queda como «Ya se le recordó hoy» y no se puede enviar otro recordatorio hasta el día siguiente.
- **El expediente no está devuelto al proveedor:** No hay botón: no hay observaciones que recordar.

## Paso 2. Confirma el recordatorio

Antes de enviar, el diálogo dice qué recibe el proveedor: un correo con lo que falta corregir y un enlace para responder, y un aviso en las notificaciones de su portal. Se puede enviar una vez al día por expediente.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Confirma el recordatorio (escritorio)](../../../salida/manual/img/revision/recordatorio-de-observaciones/02-confirma-el-recordatorio-claro.webp)

## Paso 3. Revisa que el recordatorio quedó registrado

El expediente muestra la fecha del último recordatorio, y el botón ya no deja enviar otro hoy. El proveedor sigue viendo las observaciones en su portal hasta que responda o cargue la versión corregida.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Revisa que el recordatorio quedó registrado (escritorio)](../../../salida/manual/img/revision/recordatorio-de-observaciones/03-revisa-que-el-recordatorio-quedo-registrado-claro.webp)

## Recorridos relacionados

- [Observa un documento con motivos](observar-con-motivos.md)
- [Revisa lo que corrigió el proveedor tras una observación](responder-con-lo-corregido.md)
