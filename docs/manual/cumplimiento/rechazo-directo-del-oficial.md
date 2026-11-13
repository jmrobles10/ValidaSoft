---
id: "cumplimiento/rechazo-directo-del-oficial"
titulo: "Rechaza el expediente por cumplimiento"
actor: "Oficial de cumplimiento"
dominio: "cumplimiento"
resumen: "Tras confirmar una coincidencia de listas, la oficial de cumplimiento rechaza el expediente directamente desde Cumplimiento, sin esperar a que decida un área. Elige los motivos del catálogo y escribe la justificación. El rechazo es definitivo."
pasos: 2
---

# Rechaza el expediente por cumplimiento

**Actor:** Oficial de cumplimiento · **Dominio:** Cumplimiento y SAGRILAFT

Tras confirmar una coincidencia de listas, la oficial de cumplimiento rechaza el expediente directamente desde Cumplimiento, sin esperar a que decida un área. Elige los motivos del catálogo y escribe la justificación. El rechazo es definitivo.

**Antes de empezar**

- Un grupo con Constructora Norte, sus áreas Compras y Cumplimiento y el flujo de proveedores con listas restrictivas bloqueantes.
- Un proveedor en revisión cuya consulta de listas tiene una coincidencia por analizar.
- La empresa tiene motivos de rechazo configurados (Configuración › Motivos).

**Pasos**

1. Abre el rechazo por cumplimiento y elige los motivos
2. Confirma el rechazo

## Paso 1. Abre el rechazo por cumplimiento y elige los motivos

El rechazo se ofrece solo cuando hay un hallazgo grave confirmado: una coincidencia confirmada en listas o un estudio desfavorable. El diálogo lo dice: el expediente se cierra, el vínculo vuelve a Invitado y el tercero recibe por correo los textos de los motivos que elijas. Marca uno o más motivos del catálogo y escribe la justificación: queda en el historial y en el informe SAGRILAFT, pero el tercero no la lee.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Abre el rechazo por cumplimiento y elige los motivos (escritorio)](../../../salida/manual/img/cumplimiento/rechazo-directo-del-oficial/01-abre-el-rechazo-por-cumplimiento-y-elige-los-mot-claro.webp)

**Variantes**

- **El hallazgo es un estudio de cumplimiento desfavorable:** El diálogo cita ese hallazgo en lugar de la coincidencia y el rechazo se ofrece igual.
- **No hay ningún hallazgo grave:** El botón «Rechazar por cumplimiento» no aparece: solo se rechaza por una verificación fallida.
- **Eres revisor de un área:** No ves el botón de rechazo por cumplimiento; las áreas rechazan desde su propia decisión.

**Configuración que lo afecta**

- **Motivos de rechazo** (`Configuración › Motivos (/motivos)`): Son los motivos que puedes marcar al rechazar. Sin motivos configurados, el rechazo no se puede hacer.

## Paso 2. Confirma el rechazo

El expediente se cierra y el aviso queda con la fecha y «El vínculo volvió a Invitado». El tercero recibe por correo los textos de los motivos que elegiste. En el historial queda quién rechazó, cuándo y la justificación.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Confirma el rechazo (escritorio)](../../../salida/manual/img/cumplimiento/rechazo-directo-del-oficial/02-confirma-el-rechazo-claro.webp)

## Recorridos relacionados

- [Confirma una coincidencia y rechaza el vínculo](coincidencia-confirmada.md)
- [Registra el estudio de cumplimiento de un proveedor](estudio-de-cumplimiento.md)
