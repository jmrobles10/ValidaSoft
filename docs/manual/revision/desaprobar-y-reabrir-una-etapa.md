---
id: "revision/desaprobar-y-reabrir-una-etapa"
titulo: "Reabre una etapa ya aprobada"
actor: "Administrador de empresa"
dominio: "revision"
resumen: "Ana, administradora, descubre que Compras aprobó sin revisar bien un documento. Reabre la etapa 1: Compras vuelve a decidir, la etapa 2 espera y la aprobación anterior queda en el historial como revertida."
pasos: 3
---

# Reabre una etapa ya aprobada

**Actor:** Administrador de empresa · **Dominio:** Revisión de expedientes

Ana, administradora, descubre que Compras aprobó sin revisar bien un documento. Reabre la etapa 1: Compras vuelve a decidir, la etapa 2 espera y la aprobación anterior queda en el historial como revertida.

**Antes de empezar**

- Un grupo con Constructora Norte, con las áreas Compras (Rubén) y Cumplimiento (Olga). Ana es administradora.
- Un flujo de Aliado con dos etapas en secuencia: Compras, luego Cumplimiento. Sin documentos que subir.
- Compras ya aprobó (etapa 1 aprobada) y el expediente está en la etapa 2.

**Pasos**

1. Localiza la etapa que quieres reabrir
2. Justifica la reapertura
3. Revisa cómo queda el expediente

## Paso 1. Localiza la etapa que quieres reabrir

En «Etapas y áreas», cada etapa aprobada tiene el botón «Reabrir» para volver a decidir sobre ella. Solo lo ve quien administra la empresa: los revisores de un área no tienen esta acción. Reabrir una etapa no borra lo que se aprobó: queda en el historial como revertido.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 1: Localiza la etapa que quieres reabrir (escritorio)](../../../salida/manual/img/revision/desaprobar-y-reabrir-una-etapa/01-localiza-la-etapa-que-quieres-reabrir-claro.webp)

**Variantes**

- **Eres revisor de un área y no administrador:** No ves el botón «Reabrir» ni las acciones de desaprobar.
- **La etapa aún está en curso:** No hay botón «Reabrir»: solo se reabren etapas ya aprobadas.

## Paso 2. Justifica la reapertura

Escribe por qué reabres la etapa: la justificación queda en el historial con tu nombre y la fecha. La etapa reabierta y las siguientes vuelven a revisarse; las áreas de la etapa 1 deciden otra vez.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 2: Justifica la reapertura (escritorio)](../../../salida/manual/img/revision/desaprobar-y-reabrir-una-etapa/02-justifica-la-reapertura-claro.webp)

## Paso 3. Revisa cómo queda el expediente

La etapa 1 vuelve a «En curso» y la etapa 2 a «En espera». La aprobación de Compras aparece como «Revertido por un administrador», y el aviso del expediente registra quién reabrió y cuándo. Compras decide otra vez, y cuando apruebe, la etapa 2 empieza de nuevo.

> **Pantalla:** `/e/{empresa}/expedientes/{id}`

![Paso 3: Revisa cómo queda el expediente (escritorio)](../../../salida/manual/img/revision/desaprobar-y-reabrir-una-etapa/03-revisa-como-queda-el-expediente-claro.webp)

## Recorridos relacionados

- [Desaprueba un expediente aprobado](desaprobar-un-expediente-aprobado.md)
- [Sigue un expediente que pasa por tres etapas](etapas-en-secuencia.md)
