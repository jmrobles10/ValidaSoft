---
id: "documentos/recordatorio-desde-vencimientos"
titulo: "Recuerda a un tercero un documento que vence"
actor: "Administrador de empresa"
dominio: "documentos"
resumen: "El administrador de Constructora Sur ve en Vencimientos el certificado de un proveedor que vence en pocos días, selecciona la fila y le envía un recordatorio por correo para que suba una versión nueva a tiempo."
pasos: 3
---

# Recuerda a un tercero un documento que vence

**Actor:** Administrador de empresa · **Dominio:** Documentos y vigencias

El administrador de Constructora Sur ve en Vencimientos el certificado de un proveedor que vence en pocos días, selecciona la fila y le envía un recordatorio por correo para que suba una versión nueva a tiempo.

**Antes de empezar**

- Un grupo con Constructora Sur, con el certificado de seguridad y salud en el trabajo como requisito del flujo de proveedor (30 días de vigencia desde la expedición, 10 de aviso y 5 de gracia).
- Un proveedor con el certificado expedido hace 25 días: vence en 5 días y ya está «Por vencer».

**Pasos**

1. Selecciona el documento que quieres recordar
2. Confirma el recordatorio
3. Verifica que el recordatorio salió

## Paso 1. Selecciona el documento que quieres recordar

Marca la casilla de la fila del documento que vence. Al marcarla, el botón «Recordar al tercero» cuenta cuántos documentos seleccionaste, y con ese botón se envía el recordatorio.

> **Pantalla:** `/e/{empresa}/vencimientos`

![Paso 1: Selecciona el documento que quieres recordar (escritorio)](../../../salida/manual/img/documentos/recordatorio-desde-vencimientos/01-selecciona-el-documento-que-quieres-recordar-claro.webp)

## Paso 2. Confirma el recordatorio

El diálogo dice a cuántos terceros les llega un correo y recuerda que a cada tercero se le recuerda a lo sumo una vez por día: lo que ya se recordó hoy se omite. El correo lleva el nombre del documento y la fecha en que vence para tu empresa, para que suba una versión nueva antes de esa fecha. Nada se envía hasta que pulses «Enviar recordatorio».

> **Pantalla:** `/e/{empresa}/vencimientos`

![Paso 2: Confirma el recordatorio (escritorio)](../../../salida/manual/img/documentos/recordatorio-desde-vencimientos/02-confirma-el-recordatorio-claro.webp)

## Paso 3. Verifica que el recordatorio salió

Aparece el aviso «Enviaste 1 recordatorio» con el destinatario, y el tercero recibe el correo con el documento y la fecha. El recordatorio solo avisa: no cambia el documento ni renueva nada; eso lo hace el tercero al subir una versión nueva.

> **Pantalla:** `/e/{empresa}/vencimientos`

![Paso 3: Verifica que el recordatorio salió (escritorio)](../../../salida/manual/img/documentos/recordatorio-desde-vencimientos/03-verifica-que-el-recordatorio-salio-claro.webp)

## Recorridos relacionados

- [Revisa los documentos que vencen en la lista de vencimientos](vencimientos.md)
- [Sigue el ciclo de vigencia de un certificado](ciclo-de-vigencia.md)
